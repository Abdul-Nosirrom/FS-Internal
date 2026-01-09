using System;
using TimeUtils;
using UnityEngine;
using FS.Animation;
using FS.Attributes;
using FS.Math;
using PrimeTween;

[Experimental]
public class TransformInfluencer : MonoBehaviour
{
    [SerializeField] private Transform m_targetTransform;

    private PhysicsController m_physics;

    private const float BLEND_IN_DURATION = 0.4f;
    private const float BLEND_OUT_DURATION = 0.4f;
    private const Ease EASING_CURVE = Ease.OutQuad;
    
    private const bool USE_DAMPING = true;
    private const float DAMPING_SPEED = 2.5f;
    
    private const float MAX_VELOCITY_FOR_LEAN = 10f;
    private const float MAX_ACCELERATION_FOR_LEAN = 20f;
    private const float MAX_LEAN_ANGLE = 25f;
    
    private readonly Vector3 ROTATION_PIVOT_OFFSET = new Vector3(0f, 1f, 0f);

    private Transform TargetTransform => m_targetTransform != null ? m_targetTransform : transform;

    public struct Result : IEquatable<Result>
    {
        public Quaternion RotationOffset;
        public Vector3 PositionOffset;
        
        public static Result Identity => new Result 
        { 
            RotationOffset = Quaternion.identity, 
            PositionOffset = Vector3.zero 
        };
        
        public static Result Lerp(Result a, Result b, float t)
        {
            return new Result
            {
                RotationOffset = Quaternion.Slerp(a.RotationOffset, b.RotationOffset, t),
                PositionOffset = Vector3.Lerp(a.PositionOffset, b.PositionOffset, t)
            };
        }
        
        public bool Equals(Result other) => 
            RotationOffset == other.RotationOffset && 
            PositionOffset == other.PositionOffset;
    }

    public enum Mode { None, VelocityLean, AccelerationLean, VelocityFacing, Custom }
    
    private float? m_modeBlendDuration = null;
    private Mode m_activeMode = Mode.None;
    public Mode ActiveMode
    {
        get => m_activeMode;
        set
        {
            // Warn if switching between non-None modes without clearing first
            if (m_activeMode != Mode.None && value != Mode.None && m_activeMode != value)
            {
                Debug.LogWarning($"[TransformInfluencer] Switching from {m_activeMode} to {value} without setting to None first. " +
                                 "This may cause unexpected blending behavior.", this);
            }
            
            if (m_activeMode == value) return;
            
            m_previousModeResult = m_currentResult;
            m_modeBlendDuration = null;
            m_activeMode = value;
            m_sinceModeChange = 0;
        }
    }

    public void SetActiveModeWithBlend(Mode newMode, float blendDuration)
    {
        ActiveMode = newMode;
        m_modeBlendDuration = blendDuration;
    }
    
    private TimeSince m_sinceModeChange;
    private Func<Result> m_customModeFunction = null;
    
    private Result m_currentResult = Result.Identity;
    private Result m_previousModeResult = Result.Identity;
    private Result m_dampedResult = Result.Identity;
    
    private Quaternion m_baseRotation;
    private Vector3 m_basePosition;
    private bool m_hasStoredBase;
    
    public void SetCustomMode(Func<Result> customFunction)
    {
        m_customModeFunction = customFunction;
        ActiveMode = Mode.Custom;
    }

    private void OnEnable()
    {
        m_physics = TargetTransform.GetComponentInParent<PhysicsController>();
        CaptureBaseTransform();
    }

    private void OnDisable()
    {
        // Restore to base when disabled
        if (m_hasStoredBase)
        {
            TargetTransform.localRotation = m_baseRotation;
            TargetTransform.localPosition = m_basePosition;
        }
    }

    private void CaptureBaseTransform()
    {
        m_baseRotation = TargetTransform.localRotation;
        m_basePosition = TargetTransform.localPosition;
        m_hasStoredBase = true;
    }

    public void ResetBase()
    {
        CaptureBaseTransform();
        m_currentResult = Result.Identity;
        m_dampedResult = Result.Identity;
    }

    private void LateUpdate()
    {
        // STEP 1: Get raw target result from active mode
        Result rawTargetResult = ActiveMode switch
        {
            Mode.None => Result.Identity,
            Mode.VelocityLean => GetVelocityLeanResult(),
            Mode.AccelerationLean => GetAccelerationLeanResult(),
            Mode.VelocityFacing => GetVelocityFacingLeanResult(),
            Mode.Custom => m_customModeFunction?.Invoke() ?? Result.Identity,
            _ => Result.Identity
        };
        
        // STEP 2: Apply damping (smooths rapid changes in the mode's output) [only if we're blending]
        Result dampedTarget = m_modeBlendDuration is null or > 0f && USE_DAMPING && ActiveMode != Mode.VelocityFacing // This mode doesnt need damping
            ? DampResult(m_dampedResult, rawTargetResult, DAMPING_SPEED * Time.deltaTime)
            : rawTargetResult;
        m_dampedResult = dampedTarget;
        
        // STEP 3: Blend between modes (smooths mode transitions)
        float blendDuration = m_modeBlendDuration ?? ((ActiveMode == Mode.None) ? BLEND_OUT_DURATION : BLEND_IN_DURATION);
        float blendT = blendDuration <= 0f ? 1 : Mathf.Clamp01(m_sinceModeChange / blendDuration);
        blendT = Easing.Evaluate(blendT, EASING_CURVE);
        
        Result finalResult = Result.Lerp(m_previousModeResult, dampedTarget, blendT);
        
        // STEP 4: Apply to transform
        if (!finalResult.Equals(m_currentResult) && m_hasStoredBase)
        {
            // Calculate position offset needed to rotate around pivot point
            Vector3 pivotCompensation = ROTATION_PIVOT_OFFSET - (finalResult.RotationOffset * ROTATION_PIVOT_OFFSET);
            Vector3 finalPositionOffset = finalResult.PositionOffset + pivotCompensation;
        
            TargetTransform.localRotation = m_baseRotation * finalResult.RotationOffset;
            TargetTransform.localPosition = m_basePosition + finalPositionOffset;
            m_currentResult = finalResult;
        }
    }

    private Result DampResult(Result current, Result target, float smoothing)
    {
        float t = Mathf.Clamp01(smoothing);
        return new Result
        {
            RotationOffset = Quaternion.Slerp(current.RotationOffset, target.RotationOffset, t),
            PositionOffset = Vector3.Lerp(current.PositionOffset, target.PositionOffset, t)
        };
    }

    private Result GetVelocityLeanResult()
    {
        if (m_physics == null) return Result.Identity;

        float forwardLeanFactor = AnimationParameterCalculator.CalculateAxisLeans(m_physics, m_physics.transform.forward.ProjectOnPlane(m_physics.UpDirection));
        float sideLeanFactor = AnimationParameterCalculator.CalculateSideLeans(m_physics);
    
        Vector3 localVelocity = TargetTransform.InverseTransformDirection(m_physics.Velocity);
    
        // Forward/backward lean (pitch)
        float forwardVel = localVelocity.z;
        float pitchAngle = (forwardVel / MAX_VELOCITY_FOR_LEAN) * MAX_LEAN_ANGLE;
    
        // Left/right lean (roll) - strafe leaning
        float lateralVel = localVelocity.x;
        float rollAngle = -(lateralVel / MAX_VELOCITY_FOR_LEAN) * MAX_LEAN_ANGLE * 0.5f; // Less pronounced

        pitchAngle = forwardLeanFactor * MAX_LEAN_ANGLE;
        rollAngle = sideLeanFactor * MAX_LEAN_ANGLE * 0.25f;
    
        Quaternion pitch = Quaternion.AngleAxis(pitchAngle, Vector3.right);
        Quaternion roll = Quaternion.AngleAxis(rollAngle, Vector3.forward);
    
        return new Result
        {
            RotationOffset = pitch * roll, // Combine both rotations
            PositionOffset = Vector3.zero
        };
    }

    private Result GetAccelerationLeanResult()
    {
        if (m_physics == null) return Result.Identity;
        
        // Calculate acceleration from velocity change
        Vector3 acceleration = m_physics.LocalAcceleration;
        
        // Forward/backward lean (pitch)
        float forwardAccel = acceleration.z * 2f;
        float pitchAngle = (forwardAccel / MAX_ACCELERATION_FOR_LEAN) * MAX_LEAN_ANGLE;
    
        // Left/right lean (roll) - strafe leaning
        float lateralAccel = acceleration.x;
        float rollAngle = -(lateralAccel / MAX_ACCELERATION_FOR_LEAN) * MAX_LEAN_ANGLE * 0.5f; // Less pronounced
    
        Quaternion pitch = Quaternion.AngleAxis(pitchAngle, Vector3.right);
        Quaternion roll = Quaternion.AngleAxis(rollAngle, Vector3.forward);
    
        return new Result
        {
            RotationOffset = pitch * roll, // Combine both rotations
            PositionOffset = Vector3.zero
        };
    }
    
    private Result GetVelocityFacingLeanResult()
    {
        if (m_physics == null) return Result.Identity;
    
        Vector3 velocity = m_physics.Velocity;
        if (velocity.sqrMagnitude < 0.01f) return Result.Identity;
    
        // Get the target world rotation we want
        Quaternion targetWorldRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
    
        // Convert to local space relative to parent
        Quaternion targetLocalRotation = TargetTransform.parent != null
            ? Quaternion.Inverse(TargetTransform.parent.rotation) * targetWorldRotation
            : targetWorldRotation;
    
        // Calculate the OFFSET from our base rotation
        Quaternion rotationOffset = Quaternion.Inverse(m_baseRotation) * targetLocalRotation;
    
        return new Result()
        {
            PositionOffset = Vector3.zero,
            RotationOffset = rotationOffset
        };
    }


#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !m_hasStoredBase) return;
        
        Transform target = TargetTransform;
        
        // Draw base transform (where it would be without influence)
        Gizmos.color = Color.green;
        Gizmos.matrix = Matrix4x4.TRS(
            target.parent != null ? target.parent.TransformPoint(m_basePosition) : m_basePosition,
            target.parent != null ? target.parent.rotation * m_baseRotation : m_baseRotation,
            Vector3.one
        );
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 0.1f);
        
        // Draw current influenced transform
        Gizmos.color = Color.cyan;
        Gizmos.matrix = Matrix4x4.TRS(target.position, target.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 0.12f);
    }
#endif
}

[Serializable]
[EventPath("Transform")]
public class TransformInfluencerEvent : IAnimationEvent
{
    public string Name => $"Transform Influencer: {Mode}";
    public bool IsRangedEvent => true;
    public TransformInfluencer.Mode Mode;

    private TransformInfluencer GetTransformInfluencer(GameObject context)
    {
        var influencer = context.GetComponent<TransformInfluencer>();
        if (influencer == null)
        {
            Debug.LogWarning($"[TransformInfluencerEvent] No TransformInfluencer found on {context.name}.");
        }
        return influencer;
    }
    
    public void Start(GameObject context)
    {
        var influencer = GetTransformInfluencer(context);
        if (influencer != null)
        {
            influencer.ActiveMode = Mode;
        }
    }

    public void End(GameObject context)
    {
        var influencer = GetTransformInfluencer(context);
        if (influencer != null)
        {
            influencer.ActiveMode = TransformInfluencer.Mode.None;
        }
    }
}