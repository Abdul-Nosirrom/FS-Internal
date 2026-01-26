
using FS.CameraSystem;
using FS.GameplayActions;
using FS.Math;
using PrimeTween;
using TimeUtils;
using UnityEditor;
using UnityEngine;

public class CameraBehavior_DefaultOrbit : CameraBehavior
{
    public override bool ShouldActivate() => true;

    private float m_targetDistance;
    private PhysicsController m_physics;
    protected override void Initialize()
    {
        Priority = 0;
        m_physics = m_cameraController.GetComponent<PhysicsController>();
        m_targetDistance = m_cameraController.m_cameraVector.Distance;
    }

    public override void UpdateCamera(in CameraOrbitVector inOrbitVector)
    {
        // TODO: By setting pivot like this, we always override shit comin in from the blending, for behaviors that pop in later
        // this affects them as the pivot they try to read isnt accurate (vertical deadzone is affected if we want to only activate it in air,
        // causing the 'jitter' it gets when reactivating it while its blending out as it tries to reset to the camera controller pivot. Can solve this
        // specific issue i think by handling activation before updates so the orbit vector it gets during activation is the real orbit vector pre manipulation)
        m_cameraVector.Pivot = m_cameraController.PlayerPivot;
        m_cameraVector.Rotation += m_cameraController.LookInputDelta;

        // -1 to 1
        float pitch = m_cameraVector.Rotation.pitch / 90f;
        float targetDist = inOrbitVector.Distance;
        if (pitch < 0) targetDist = 0.25f * targetDist;
        else if (pitch > 0) targetDist = 1.5f * targetDist;

        if (m_physics.IsInSkateAction)
        {
            if (m_physics.Velocity.Dot(m_physics.GravityDir) > 0)
                targetDist = 4; //0.75f * inOrbitVector.Distance; // Falling, push in a bit
            else
                targetDist = 4;// inOrbitVector.Distance; // Raising
        }
        
        m_targetDistance = Mathf.Lerp(m_targetDistance, targetDist, 3f * Time.deltaTime);
        m_cameraVector.Distance = Mathf.Lerp(inOrbitVector.Distance, m_targetDistance, Easing.Evaluate(Mathf.Abs(pitch), Ease.InOutQuad));
        
        // float pitch = m_cameraVector.Rotation.pitch / 90f;
        // float targetDist = m_cameraVector.Distance;
        // if (pitch < 0) targetDist = 0.25f * targetDist;
        // else if (pitch > 0) targetDist = 1.5f * targetDist;
        //
        // if (m_physics.IsInSkateAction)
        // {
        //     if (m_physics.Velocity.Dot(m_physics.GravityDir) > 0)
        //         targetDist = 0.8f * m_cameraVector.Distance; // Falling, push in a bit
        //     else
        //         targetDist = m_cameraVector.Distance; // Raising
        // }
        //
        // m_cameraVector.Distance = Mathf.Lerp(inOrbitVector.Distance, targetDist, Easing.Evaluate(Mathf.Abs(pitch), Ease.InOutQuad));
    }
}

// TODO: The hitch we see w/ basis changes when we do a full loop? This behavior is the cause of it
public class CameraBehavior_FocusMoveDirection : CameraBehavior
{
    [Header("Autocam Settings")]
    [SerializeField] private float m_minRotationSpeed = 30f;   // Degrees/sec when moving with camera
    [SerializeField] private float m_maxRotationSpeed = 270f;   // Degrees/sec when moving against camera
    [SerializeField] private float m_inputCooldown = 0.5f;     // Seconds to wait after manual input
    [SerializeField] private float m_minSpeedThreshold = 1f;   // Min speed to trigger autocam
    [SerializeField] private float m_maxSpeedReference = 15f;  // Speed at which we hit max rotation
    
    [Header("Obstruction Check")]
    [SerializeField] private bool m_enableObstructionCheck = true;
    [SerializeField] private float m_obstructionCheckRadius = 0.3f;
    
    // Dead zone: no autocam within inner angle, full autocam beyond outer angle
    const float innerDeadZone = 15f;  // No correction within this
    const float outerDeadZone = 45f;  // Full correction beyond this

    // Keep track of prev to give a buffer time if we 'switch directions' as to not be disorienting (when walking in circles)
    private float m_prevAppliedYawDelta;
    private TimeSince m_sinceYawDeltaSignSwitched;
    
    private PhysicsController m_physics;
    private RailGrindAction m_railGrind;
    
    protected override void Initialize()
    {
        BehaviorType = CameraBehaviorType.PostProcess;
        m_physics = m_cameraController.GetComponent<PhysicsController>();
        m_railGrind = m_cameraController.GetComponentInChildren<RailGrindAction>();
    }

    public override bool ShouldActivate() => true;

    public override void UpdateCamera(in CameraOrbitVector inOrbitVector)
    {
        // Fucks up vert cam a tad bit so disable it during
        // NOTE: There was some nice 'aiming' for vert with the previous iteration of this behavior,
        // so consider maybe falling back to that when verts active
        if (m_physics.IsInSkateAction) return;
        
        // Get horizontal velocity (projected onto the plane perpendicular to up)
        Vector3 velocity = m_physics.Velocity;
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, UpVector);
        float speed = horizontalVelocity.magnitude;
        
        // Early out if not moving enough
        if (speed < m_minSpeedThreshold) return;
        
        // Calculate input cooldown factor (0 = just had input, 1 = ready for autocam)
        float cooldown = m_inputCooldown;
        float inputTimeDeadZone = 0.5f;
        float inputFactor = Mathf.Clamp01((m_cameraController.TimeSinceLastInput - inputTimeDeadZone) / cooldown);
        if (inputFactor <= 0f) return;
        
        // Custom railgrind focus        
        if (m_railGrind.IsActive)
        {
            // Just slerp rot, custom rail behavior cuz smoothings important
            var targetRailYaw = m_cameraController.AimYaw(horizontalVelocity.normalized);
            float stateFade = Mathf.Clamp01(m_railGrind.m_timeSinceStarted / 0.5f);
            m_cameraVector.Yaw = Mathf.LerpAngle(m_cameraVector.Yaw, targetRailYaw, inputFactor * stateFade * 10f * Time.deltaTime);
            return;
        }
        
        // Yaw delta relaxation
        // float signSwitchFactor = Mathf.Clamp01(m_sinceYawDeltaSignSwitched / 1.5f);
        // if (signSwitchFactor < 1)
        // {
        //     Debug.LogError(signSwitchFactor);
        //     return;
        // }
        
        // Get current camera forward (horizontal only)
        Vector3 camForward = Vector3.ProjectOnPlane(
            m_cameraController.CameraState.m_rotation * Vector3.forward, 
            UpVector
        ).normalized;
        
        Vector3 moveDir = horizontalVelocity.normalized;
        
        // === THE KEY: Dot product for aggressiveness ===
        // dot = 1 when moving WITH camera, -1 when moving AGAINST
        float dot = Vector3.Dot(camForward, moveDir);
        
        // Remap: we want MORE rotation when dot is LOW (moving against camera)
        // aggressiveness: 0 when aligned (dot=1), 1 when opposite (dot=-1)
        float aggressiveness = (1f - dot) * 0.5f; // Maps [-1,1] to [1,0] then to [0,1]
        aggressiveness = Easing.Evaluate(aggressiveness, Ease.OutQuad); // Intermediate stronger
        
        // Speed factor: faster = more rotation
        float speedFactor = Mathf.Clamp01(speed / m_maxSpeedReference);
        
        // Deadzone factor: Fade the behavior out if we're moving more or less forward
        // Angle between camera forward and move direction
        float angleFromForward = Vector3.Angle(camForward, moveDir);
        float deadZoneFactor = Mathf.InverseLerp(innerDeadZone, outerDeadZone, angleFromForward);

        // Minimum factor so it always corrects *a little* just so the camera doesnt orient always to an angled forward
        //float minCorrectionFactor = 0.25f;
        //deadZoneFactor = Mathf.Max(deadZoneFactor, minCorrectionFactor);
        
        // Combined rotation speed (if railgrind is active we can be more aggressive)
        float rotationSpeed = Mathf.Lerp(m_minRotationSpeed, m_maxRotationSpeed, aggressiveness * speedFactor) * deadZoneFactor;
        if (m_railGrind.IsActive) rotationSpeed = Mathf.Lerp(0f, m_maxRotationSpeed * 0.5f, m_railGrind.m_timeSinceStarted/0.45f); // discount deadzone & speed factor here
        
        // Calculate target yaw (the direction we want to face)
        float targetYaw = m_cameraController.AimYaw(moveDir);
        float currentYaw = inOrbitVector.Rotation.yaw;
        
        // Calculate yaw delta (shortest path)
        float yawDelta = Mathf.DeltaAngle(currentYaw, targetYaw);
        
        // === OBSTRUCTION CHECK (Mario Sunshine trick) ===
        if (m_enableObstructionCheck && Mathf.Abs(yawDelta) > 5f)
        {
            // Preview where camera would be if we rotated toward move direction
            var previewRotation = new EulerAngles(inOrbitVector.Rotation.pitch, targetYaw, 0);
            var previewOrbit = inOrbitVector;
            previewOrbit.Rotation = previewRotation;
            Vector3 previewCamPos = previewOrbit.ToPosition(m_cameraController.Basis);
            
            Vector3 toPivot = (inOrbitVector.Pivot - previewCamPos).normalized;
            float distToPivot = Vector3.Distance(previewCamPos, inOrbitVector.Pivot);
            
            // If the ideal position would be blocked, skip autocam this frame
            if (Physics.SphereCast(previewCamPos, m_obstructionCheckRadius, toPivot, out var hit, distToPivot, PhysicsLayers.GetLayerCollisionMask(PhysicsLayers.CameraObstruction)))
            {
                if (hit.collider && hit.collider.gameObject != m_physics.gameObject)
                    return;
            }
        }
        
        // Apply rotation with speed-based lerp
        float maxDeltaThisFrame = rotationSpeed * inputFactor * Time.deltaTime;
        float appliedDelta = Mathf.Clamp(yawDelta, -maxDeltaThisFrame, maxDeltaThisFrame);
        
        //m_cameraVector.Yaw = currentYaw + appliedDelta;
        float lerpSpeed = Mathf.Lerp(1.5f, 3f, deadZoneFactor * aggressiveness);
        m_cameraVector.Yaw = Mathf.LerpAngle(currentYaw, targetYaw,
            lerpSpeed * speedFactor * inputFactor * Time.deltaTime);

        if (Mathf.Sign(m_prevAppliedYawDelta * appliedDelta) < 0f) m_sinceYawDeltaSignSwitched = 0f;
        m_prevAppliedYawDelta = appliedDelta;
    }
}

public class CameraBehavior_PitchFraming : CameraBehavior
{
    protected override void Initialize()
    {
        BehaviorType = CameraBehaviorType.PostProcess;
        m_physics = m_cameraController.gameObject.GetComponent<PhysicsController>();
    }

    [SerializeField] private float m_focusFadeDuration = 5f;
    [SerializeField] private float m_interpSpeed = 2f;
    private PhysicsController m_physics;
    
    public override bool ShouldActivate() => !m_physics.IsInSkateAction; // skate actions want to control their pitch
    
    public override void UpdateCamera(in CameraOrbitVector inOrbitVector)
    {
        // match pitch of the ground (so long as its not vertical)
        float groundDotBasis = 90 - m_physics.transform.forward.SignedAngle(UpVector);
        float targetPitch = m_physics.IsInSkateAction ? 15f : 10f; // be more forward aligned i think?
        if (m_physics.IsGrounded && !m_physics.Ground.CompareLayer(PhysicsLayers.Vert) && groundDotBasis < 70) targetPitch -= groundDotBasis/2;
        
        float orbitFadeFactor = Mathf.Clamp01(m_cameraController.TimeSinceLastInput / m_focusFadeDuration);
        // Default pitch
        m_cameraVector.Pitch = Mathf.LerpAngle(inOrbitVector.Rotation.pitch, targetPitch, m_interpSpeed * orbitFadeFactor * Time.deltaTime);
    }
}