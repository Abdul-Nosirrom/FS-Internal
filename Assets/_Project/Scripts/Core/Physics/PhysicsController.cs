using FS.AI;
using FS.GameplayActions;
using FS.Math;
using FS.Player;
using Lightbug.CharacterControllerPro.Core;
using Sirenix.OdinInspector;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

[RequireComponent(typeof(CharacterActor))]
[Icon("PhysicsController Icon")]
public partial class PhysicsController : MonoBehaviour
{
    [SerializeField] private CharacterActor m_motor;
    
    private ActionController m_actionController;
    private PlayerInputSystem m_input;
    private AINavigation m_aiNavigation;
    private Camera m_camera;

    private Vector2 m_initialCollisionSize;

    private void Awake()
    {
        m_motor = GetComponent<CharacterActor>();
        m_actionController = GetComponent<ActionController>();
        m_aiNavigation = GetComponent<AINavigation>();
        if (transform.parent)
            m_camera = transform.parent.GetComponentInChildren<Camera>();
        m_initialCollisionSize = GetComponent<CharacterBody>().BodySize;
        m_rotationSmoother = GetComponentInChildren<CharacterRotationLerper>();
    }

    private void Start()
    {
        m_input = PlayerManager.Instance.GetSubsystem<PlayerInputSystem>(gameObject);
        FetchActions();
    }

    private static readonly ProfilerMarker s_fixedUpdateMarker = new ProfilerMarker("PhysicsController.FixedUpdate");

    public bool IsKinematic
    {
        get => m_motor.IsKinematic;
        set => m_motor.IsKinematic = value;
    }

    public bool HadJustExitedVertSurface
    {
        get
        {
            if (IsGrounded) return false;
            if (m_timeSinceLostGround > 0.5f) return false;
            bool layerCheck = LastGround.CompareLayer(PhysicsLayers.Vert);
            bool angleCheck = LastGround.GroundSlopeAngle >= 50f;
            return layerCheck && angleCheck;
        }
    }
    
    private void FixedUpdate()
    {
        if (IsKinematic) return;
        
        using var _ = s_fixedUpdateMarker.Auto();
        
        Acceleration = (Velocity - PreviousFrameVelocity) / Time.deltaTime;
        AngularVelocity = Mathf.Deg2Rad * Vector3.SignedAngle(PreviousFrameVelocity.ProjectOnPlane(UpDirection), LateralVelocity, UpDirection) / Time.deltaTime;
        PreviousFrameVelocity = Velocity;

        // I think this helps w/ acid drop landing, it might do some reprojection stuff that makes the landing frame a bit odd (even tho we 'overwrite it')
        m_motor.stablePostSimulationVelocity = m_motor.unstablePostSimulationVelocity = IsInSkateAction
            ? CharacterActor.CharacterVelocityMode.UseInputVelocity
            : CharacterActor.CharacterVelocityMode.UsePostSimulationVelocity;
        
        UpdatePhysicsState();
        GroundingUpdate();

        ModifyPhysicsParameters();
        
        // Did we lose grounding due to slowing down at a steep slope? If so push us away from the (unstable) ground normal
        if (m_motor.CurrentState == CharacterActorState.UnstableGrounded &&
            m_motor.PreviousState == CharacterActorState.StableGrounded)
        {
            // Are the normals pretty similar? If so thats our indicator that we just slowed down too much on a steep slope
            if (Ground.GroundSlopeAngle > k_defaultSlopeLimit && Vector3.Angle(LastStableGround.Normal, Ground.Normal) < 15f)
            {
                // Push us away from the ground normal a bit
                Velocity += LastStableGround.Normal * 10f;
            }
        }

        UpdateVelocity();
        UpdateRotation();
        
        // Detect ground when ascending only during skate actions (TODO: Update CCP to consider "ascending" as vel dot gravity, not our local up)
        //m_motor.detectGroundWhileAscending = IsInSkateAction;

        //if (Ground.CompareTag(GameTags.Vert) || IsInSkateAction)
        //{
        //    m_motor.SetSize(new Vector2(0.1f, 0.1f), CharacterActor.SizeReferenceType.Bottom);
        //}
        //else m_motor.SetSize(m_initialCollisionSize, CharacterActor.SizeReferenceType.Bottom);
    }

    public Vector3 MoveInput(Vector3? customUp = null)
    {
        if (m_aiNavigation) return m_aiNavigation.MoveDirection;
        
        if (!m_input)
        {
            return Vector3.zero;
        }
        
        // Camera relative input rotation
        var targetNormal = customUp ?? (State == PhysicsState.Sliding ? Ground.Normal : UpDirection);
        var camPlaneRotation = Quaternion.FromToRotation(m_camera.transform.up, targetNormal);

        var camForward = camPlaneRotation * m_camera.transform.forward;
        var camRight = camPlaneRotation * m_camera.transform.right;
        
        Vector3 moveInput = Vector3.ClampMagnitude(m_input.MoveVector.y * camForward + m_input.MoveVector.x * camRight, 1);
        moveInput = Vector3.ProjectOnPlane(moveInput, targetNormal);
        
        // If unstable ground, block input that tries to move use "towards" the 'unstable ground'
        // if (m_motor.CurrentState == CharacterActorState.UnstableGrounded)
        // {
        //     var groundNormal = m_motor.GroundContactNormal;
        //     // Zero out any component of moveInput that points along -groundNormal
        //     var dot = Vector3.Dot(moveInput, groundNormal);
        //     if (dot < 0) moveInput = Vector3.ProjectOnPlane(moveInput, groundNormal);
        // }

        return moveInput;
    } 
    
    /// <summary>
    /// Minimum speed we accelerate to
    /// </summary>
    private const float k_minAnalogSpeed = 1;
    /// <summary>
    /// Minimum speed needed to rotate towards the velocity direction
    /// </summary>
    private const float k_minSpeedForFacing = 0.1f;
    
    [SerializeField, TabGroup("Lateral Physics"), HideLabel]
    private LateralPhysicsParams m_lateralPhysicsParams;
    [SerializeField, TabGroup("Vertical Physics"), HideLabel]
    private VerticalPhysicsParams m_verticalPhysicsParams;
    [SerializeField, TabGroup("Rotation"), HideLabel]
    private RotationPhysicsParams m_rotationPhysicsParams;
    
    public VerticalPhysicsParams DefaultVerticalPhysicsParams => m_verticalPhysicsParams;
    public LateralPhysicsParams DefaultLateralPhysicsParams => m_lateralPhysicsParams;
    public RotationPhysicsParams DefaultRotationPhysicsParams => m_rotationPhysicsParams;

    // We store the physics params that are mutable seperately, and update them in fixed update so they reflect the modifications applied.
    // So when we try toa access them from somewhere else, we get the correctly modified values (and can use default if we want that specifically)
    public VerticalPhysicsParams VerticalPhysicsParams { get; private set; }
    public LateralPhysicsParams LateralPhysicsParams { get; private set; }
    public RotationPhysicsParams RotationPhysicsParams { get; private set; }
    
    private void ModifyPhysicsParameters()
    {
        var verticalParams = DefaultVerticalPhysicsParams;
        var lateralParams = DefaultLateralPhysicsParams;
        var rotationParams = DefaultRotationPhysicsParams;

        m_verticalPhysicsModifications.Apply(this, ref verticalParams);
        m_lateralPhysicsModifications.Apply(this, ref lateralParams);
        m_rotationPhysicsModifications.Apply(this, ref rotationParams);
        
        VerticalPhysicsParams = verticalParams;
        LateralPhysicsParams = lateralParams;
        RotationPhysicsParams = rotationParams;
    }
    
    public virtual void LateralPhysics(LateralPhysicsParams? inPhysParams = null)
    {
        if (State == PhysicsState.Sliding)
        {
            SlidePhysics();
            return;
        }
        
        var physParams = inPhysParams ?? LateralPhysicsParams;
        if (inPhysParams.HasValue) m_lateralPhysicsModifications.Apply(this, ref physParams); // Apply modifications to custom params passed in as well
        
        float acceleration = IsGrounded ? physParams.m_acceleration : physParams.m_airAcceleration;
        float deceleration = IsGrounded ? physParams.m_deceleration : physParams.m_airDeceleration;
        float maxSpeed = IsGrounded ? physParams.m_maxSpeed : physParams.m_airMaxSpeed;
        float topSpeed = physParams.m_topSpeed;
        float friction = IsGrounded ? physParams.m_friction : physParams.m_friction * physParams.m_airControl;
        float airDrag = physParams.m_airDrag;
        
        Vector3 inputDir = MoveInput();

        // When moving, we wanna accelerate to a 'desired' speed defined as maxSpeed * inputDir.magnitude
        maxSpeed *= inputDir.magnitude; // So if we hold the input forward a bit, we dont just accelerate to full max speed and isntead get to the point we wanna get to

        bool isOverTopSpeed = LateralVelocity.sqrMagnitude > topSpeed * topSpeed;
        bool shouldAccel = inputDir.sqrMagnitude > 0 && LateralVelocity.sqrMagnitude <= maxSpeed * maxSpeed;
        bool shouldDecel = inputDir.sqrMagnitude == 0 || isOverTopSpeed;
        
        if (isOverTopSpeed && inputDir.sqrMagnitude > 0) deceleration = physParams.m_overTopSpeedDeceleration;

        if (shouldAccel)
        {
            float prevMag = LateralVelocity.sqrMagnitude;
            LateralVelocity += inputDir * acceleration * Time.deltaTime;
            if (prevMag < maxSpeed * maxSpeed && LateralVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            {
                LateralVelocity = LateralVelocity.normalized * maxSpeed;
            }
            
            if (LateralVelocity.sqrMagnitude < k_minAnalogSpeed * k_minAnalogSpeed)
                LateralVelocity = inputDir.normalized * k_minAnalogSpeed;
            //LateralVelocity = LateralVelocity.normalized * Mathf.Max(LateralVelocity.magnitude, k_minAnalogSpeed);
        }
        else if (shouldDecel)
        {
            // If grounded, don't decellerate along gravity direction, such that if we're on steep slopes gravity can slide us down
            float decelGravFactor = 1;
            if (IsGrounded)
            {
                var gravDir = GravityDir.ProjectOnPlane(GroundNormal);
                decelGravFactor = Vector3.Dot(LateralVelocity.normalized, gravDir);
                if (decelGravFactor < 0.5)
                    decelGravFactor = 1f; // moving against gravity, so allow our regular physics to apply
                else decelGravFactor = 0.1f; // NOTE: BUG: This is behavior we want, but it interacts with denivelation weirdly? Comment this out on the slopey-area near the acid drops in the metric room to check
            }
            LateralVelocity -= LateralVelocity.normalized * Mathf.Min(deceleration * Time.deltaTime * decelGravFactor, LateralVelocity.magnitude);
        }

        if (inputDir.sqrMagnitude > 0 && LateralVelocity.sqrMagnitude > 0)
        {
            float preTurnSpeed = LateralVelocity.magnitude;
            LateralVelocity = LateralVelocity - (LateralVelocity - inputDir.normalized * LateralVelocity.magnitude) *
                Mathf.Min(friction * Time.deltaTime, 1);
            float postTurnSpeed = LateralVelocity.magnitude;

            float speedPreserveFactor = Mathf.InverseLerp(maxSpeed / 1.5f, maxSpeed, preTurnSpeed); // NOTE: Turn speed-preservation is hard-coded atm
            if (IsGrounded) LateralVelocity = LateralVelocity.normalized * Mathf.Lerp(postTurnSpeed, preTurnSpeed, speedPreserveFactor);
        }

        if (!IsGrounded)
        {
            LateralVelocity -= LateralVelocity.normalized * Mathf.Min(airDrag * Time.deltaTime, LateralVelocity.magnitude);
        }
        else
        {
            SlopePhysics(UpDirection, physParams.m_slopeGravity);
        }
        
        //LateralVelocity = Vector3.ClampMagnitude(LateralVelocity, topSpeed); // NOTE: Instead of clamping, we want to decelerate if we are over top speed
    }

    public virtual void SlopePhysics(Vector3 normal, float? slopeGravity = null)
    {
        // No slow-down going up on vert!
        if (Ground.CompareLayer(PhysicsLayers.Vert) && Velocity.Dot(-GravityDir) > 0) return;
        var gravity = slopeGravity ?? LateralPhysicsParams.m_slopeGravity;
        var gravOnPlane = Vector3.ProjectOnPlane(GravityDir * gravity, normal);
        Velocity += gravOnPlane * Time.deltaTime;
    }

    public virtual void VerticalPhysics(VerticalPhysicsParams? inPhysParams = null)
    {
        if (State == PhysicsState.Sliding)
        {
            return;
        }
        
        var physParams = inPhysParams ?? VerticalPhysicsParams;
        if (inPhysParams.HasValue) m_verticalPhysicsModifications.Apply(this, ref physParams);

        if (IsGrounded) return;

        //float upGravity = LastStableGround.CompareLayer(PhysicsLayers.Vert) && !m_actionController.ActiveActions.ContainsAnyChannel(ActionChannel.Physics) 
        //    ? Mathf.Lerp(5f, physParams.m_upGravity, Mathf.Clamp01(m_timeSinceLostGround/2f)) : physParams.m_upGravity;//15;
        float upGravity = physParams.m_upGravity;
        float downGravity = m_timeSinceLostGround < 0.2f && VerticalSpeed < 5f ? 5 : physParams.m_downGravity; //m_timeSinceLostGround < 0.2f ? 5 : 25;
        float terminalSpeed = physParams.m_terminalSpeed;//20;
        float maxRiseSpeed = physParams.m_maxRiseSpeed; //30;
        float riseDeceleration = physParams.m_riseSpeedDeceleration; //50;

        if (HadJustExitedVertSurface)
            upGravity = downGravity = 15f;
        
        VerticalVelocity += GravityDir * ((IsFalling ? downGravity : upGravity) * Time.deltaTime);
        
        //Debug.LogError($"Vertical Speed: {VerticalSpeed} | Is Above Max Rise Speed: {VerticalSpeed > maxRiseSpeed} | Is Falling: {IsFalling}");
        
        if (IsFalling) VerticalVelocity = Vector3.ClampMagnitude(VerticalVelocity, terminalSpeed);
        else if (VerticalSpeed > maxRiseSpeed)
        {
            // Slow down quickly
            float prevVerticalSpeed = VerticalSpeed;
            VerticalSpeed = Mathf.MoveTowards(VerticalSpeed, maxRiseSpeed, riseDeceleration * Time.deltaTime);
            //Debug.LogError($"Decelerated By {prevVerticalSpeed - VerticalSpeed}");
        }
    }
    
    // Slide physics implies that we have ground, but its unstable. We want to slide along it and steer along the gravity direction
    public virtual void SlidePhysics()
    {
        SlopePhysics(Ground.Normal, 5f);
        
        // Perform steering
        var gravDir = GravityDir.ProjectOnPlane(Ground.Normal).normalized;
        var moveInput = MoveInput();
        //if (moveInput.Dot(gravDir) < 0) return;

        var slideVel = Velocity.ProjectOnto(gravDir);
        var planarVel = Velocity.ProjectOnPlane(gravDir);

        var perpendicularMoveInput = moveInput.ProjectOnPlane(gravDir);
        
        if (perpendicularMoveInput.sqrMagnitude > 0)
            planarVel += perpendicularMoveInput * LateralPhysicsParams.m_acceleration * Time.deltaTime;
        else 
            planarVel -= planarVel.normalized * Mathf.Min(LateralPhysicsParams.m_deceleration * Time.deltaTime, planarVel.magnitude);
        
        planarVel.ClampMagnitude(LateralPhysicsParams.m_maxSpeed);
        Velocity = planarVel + slideVel.ClampMagnitude(VerticalPhysicsParams.m_terminalSpeed);
    }
    
    public virtual void RotationPhysics(RotationPhysicsParams? inPhysParams = null)
    {
        var currentForward = Rotation * Vector3.forward;
        var currentUp = Rotation * Vector3.up;
        
        // Orient towards ground & velocity
        RotationPhysicsParams physParams = inPhysParams ?? RotationPhysicsParams;
        if (inPhysParams.HasValue) m_rotationPhysicsModifications.Apply(this, ref physParams);
        
        float k_verticalRotationRate = physParams.m_verticalRotationRate;
        float k_lateralRotationRate = physParams.m_lateralRotationRate;

        #region UP DIRECTION
        // Figure out target up Direction
        Vector3 targetUp = UpDirection;
        
        // If we're barely moving and grounded, up is just up
        if (IsGrounded && !Ground.CompareLayer(PhysicsLayers.Vert) && Velocity.sqrMagnitude < 5f && Ground.GroundSlopeAngle < k_defaultSlopeLimit) targetUp = -GravityDir;
        if (!IsGrounded && !physParams.m_bUpIsGravityInAir) targetUp = currentUp;

        // Adjust up rotation based on speed or ground type to ensure proper movement solving
        if (IsGrounded)
        {
            if (Ground.CompareLayer(PhysicsLayers.Vert))
            {
                k_verticalRotationRate *= Mathf.Lerp(1f, 8f, Velocity.magnitude / LateralPhysicsParams.m_maxSpeed);
            }
            else
            {
                // Up the vertical rotation rate if grounded and moving fast
                // We wanna rotate faster at high speeds so we dont bump into high curvature floors (e.g small QPs)
                float rotAlpha = Velocity.magnitude > LateralPhysicsParams.m_maxSpeed
                    ? (Velocity.magnitude - LateralPhysicsParams.m_maxSpeed) /
                      (LateralPhysicsParams.m_topSpeed - LateralPhysicsParams.m_maxSpeed)
                    : 0;
                k_verticalRotationRate = Mathf.Lerp(k_verticalRotationRate, 4 * k_verticalRotationRate, rotAlpha);
            }
        }
        else
        {
            // Do we have revert to up feature?
            k_verticalRotationRate = physParams.m_bUpIsGravityInAir ? physParams.m_revertToAirUpRate : 0f;
        }

        targetUp = Vector3.Slerp(currentUp, targetUp, k_verticalRotationRate * Time.deltaTime);
        //targetUp = Vector3.Slerp(currentUp, targetUp, 1 - Mathf.Pow(2, -k_verticalRotationRate * Time.deltaTime)); (smooth fps independent)

        #endregion
        
        #region FORWARD DIRECTION

        currentForward = Quaternion.FromToRotation(currentUp, targetUp) * currentForward;
        var moveInput = MoveInput(targetUp);
        if (moveInput.IsZero()) moveInput = currentForward;

        // Figure out target forward, first option is velocity, second is to keep current facing
        Vector3 targetForward = State == PhysicsState.Air && false ? moveInput : Velocity.ProjectOnPlane(targetUp);
        if (targetForward.IsNearlyZero(k_minSpeedForFacing)) // If speed is too small, keep current orientation
        {
            targetForward = currentForward;
        }
        else if (LateralVelocity.sqrMagnitude <= 2f)
        {
            targetForward = moveInput; // Low speed, just face input dir
        }
        
        targetForward = Vector3.Slerp(currentForward, targetForward, k_lateralRotationRate * Time.deltaTime);
        //targetForward = Vector3.Slerp(currentForward, targetForward, 1 - Mathf.Pow(2, -k_lateralRotationRate * Time.deltaTime));

        #endregion
        
        Rotation = Quaternion.LookRotation(targetForward, targetUp);
    }
}
