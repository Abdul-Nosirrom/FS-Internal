using System.Collections;
using Animancer;
using FS.Animation;
using FS.GameplayActions;
using FS.Math;
using FS.MeshProcessing;
using FS.Utility;
using TimeUtils;
using UnityEngine;

/// <summary>
/// Handles rail grinding mechanics including spline following, rail swapping, 
/// zipline orientation changes, and wall rebound behavior.
/// </summary>
public class RailGrindAction : GameplayAction, IActionPhysicsReciever, IActionUpdateReciever
{
    #region Constants

    private const float k_gravityMultiplier = 15f;
    private const float k_minGrindSpeed = 12f;
    private const float k_maxGrindSpeed = 35f;
    private const float k_reboundSpeed = 20f;
    private const float k_rotationLerpSpeed = 15f;
    private const float k_orientationTransitionDuration = 0.2f;
    private const float k_swapParabolicHeight = 2f;
    private const float k_zipGrindAngleThreshold = 30f;
    private const float k_actionCooldown = 0.4f;
    
    #endregion
    
    #region Animation Properties

    public static readonly int k_animLeanAmount = Animator.StringToHash("LeanAmount");
    public static readonly int k_animRailSpeed = Animator.StringToHash("Speed");
    public static readonly int k_animRailSwapTrigger = Animator.StringToHash("RailSwap");
    public static readonly int k_animReboundTrigger = Animator.StringToHash("Rebound");
    public static readonly int k_animOrientationState = Animator.StringToHash("OrientationState");
    public static readonly int k_animRailSwapDirection = Animator.StringToHash("RailSwapDirection");
    
    #endregion

    #region Serialized Fields

    [SerializeField, Range(0, 1f)] 
    private float m_swapTime = 0.9f;
    
    [SerializeField, Range(0, 0.5f)] 
    private float m_positionAlignDuration = 0.1f;

    #endregion

    #region State Enums

    public enum GrindState
    {
        Default,
        Swap,
        Trick,
        Rebound
    }

    public enum OrientationState
    {
        Regular,
        Zipline
    }

    #endregion

    #region Runtime State

    [RuntimeData] private GrindState m_state = GrindState.Default;
    [RuntimeData] private OrientationState m_orientationState = OrientationState.Regular;
    [RuntimeData] private Vector3 m_zipGrindAnimationOffset;
    [RuntimeData] private Vector3 m_initialSwapOffsetVector;
    [RuntimeData] private TimeSince m_sinceSwapStarted;
    [RuntimeData] private TimeSince m_sinceOrientationChanged;

    #endregion

    #region Components & References

    protected RailFeeler m_feeler;
    private SplineFollower m_grindFollower = new SplineFollower();
    private IAnimation m_railAnimation;
    private IAnimation m_railTrickAnimation;
    //private IAnimation m_railSwapAnimation;
    
    #endregion

    #region Public Properties

    public override ActionChannel Channels => ActionChannel.Physics | ActionChannel.Rotation;

    public ISplineProvider RailSpline
    {
        get => m_grindFollower.m_spline;
        set => m_grindFollower.m_spline = value;
    }

    public ControllerState AnimRailState => (ControllerState)m_railAnimation?.GetState(m_animator, false);//{ get; private set; }
    
    /// <summary>
    /// Current grinding speed along the rail spline.
    /// </summary>
    public float Speed
    {
        get => m_grindFollower.m_speed;
        set => m_grindFollower.m_speed = value;
    }

    /// <summary>
    /// Surface normal at the current rail position.
    /// </summary>
    public Vector3 Normal => m_grindFollower.Normal;

    /// <summary>
    /// Current position on the rail spline.
    /// </summary>
    public Vector3 RailPosition => m_grindFollower.Position;

    /// <summary>
    /// Offset from the rail spline position when zip-grinding
    /// </summary>
    public float ZipGrindVerticalOffset => -m_physics.CapsuleHeight * 1.5f;

    /// <summary>
    /// Current grinding state (Default, Swap, or Rebound).
    /// </summary>
    public GrindState State
    {
        get => m_state;
        set
        {
            var prevState = m_state;
            m_state = value;
            OnStateChanged(prevState);
        }
    }

    /// <summary>
    /// Current body orientation relative to the rail (Regular or Zipline).
    /// </summary>
    public OrientationState GrindOrientation
    {
        get => m_orientationState;
        set
        {
            if (value == m_orientationState) return;
            m_orientationState = value;
            m_sinceOrientationChanged = 0;
            AnimRailState?.SetInteger(k_animOrientationState, (int)m_orientationState);
        }
    }

    #endregion

    #region Initialization

    public override void OnInitialize(GameObject owner)
    {
        m_feeler = owner.GetComponentInChildren<RailFeeler>();
        m_grindFollower.OnReachedEnd += EndAction;

        InitializeAnimations();
    }

    private void InitializeAnimations()
    {
        if (m_animator == null) return;
        
        // TODO: Uncomment when animation set is available
        m_railAnimation = m_animator.GetAnimationSet<ActionsAnimationSet>()?.RailGrind;
        m_railTrickAnimation = m_animator.GetAnimationSet<ActionsAnimationSet>()?.RailTrick;
        //m_railSwapAnimation = m_animator.GetAnimationSet<ActionsAnimationSet>()?.RailSwap;
    }

    #endregion

    #region Action Lifecycle

    protected override bool StartCondition()
    {
        // Must be airborne and past cooldown
        if (m_physics.IsGrounded || m_timeSinceEnded <= k_actionCooldown) 
            return false;

        // Must detect a valid rail
        if (!m_feeler.TryGetRailSpline(out m_grindFollower.m_spline)) 
            return false;

        // Ensure no wall obstruction ahead
        Vector3 wallCheckPos = m_physics.Position + m_physics.transform.forward * m_physics.CapsuleRadius;
        if (m_physics.CheckCharacterCapsule(wallCheckPos)) 
            return false;

        return true;
    }

    public override void OnStart()
    {
        //AnimRailState = m_railAnimation?.Play(m_animator) as ControllerState;
        m_railAnimation?.Play(m_animator);
        m_grindFollower.Init(m_grindFollower.m_spline, m_physics);

        // Reset state
        State = GrindState.Default;
        m_orientationState = m_feeler.RailOrientationState; // TODO: Support starting in zip-grind if top sphere overlap
        
        // Set animation start state
        AnimRailState?.SetInteger(k_animOrientationState, (int)m_orientationState);
        
        // Cache zip-grind animation offset and accumulate it as we handle our positioning via root motion
        m_zipGrindAnimationOffset = m_physics.RootMotionDeltaPosition;
    }

    public override void OnEnd()
    {
        FadeOutAnimation();
        m_physics.TrySnapToGround();
    }

    private void FadeOutAnimation()
    {
        m_railAnimation?.Stop(m_animator, AnimancerGraph.DefaultFadeDuration);
    }

    #endregion

    #region Update Loop

    public void OnUpdate()
    {
        if (TryHandleWallCollision()) return;
        if (State == GrindState.Rebound) return;

        UpdateLeanAmount();
        TryRailTrick();
        TryRailSwap();
        TryChangeZipOrientation();
        UpdatePosition();
    }

    /// <summary>
    /// Handles wall collision detection and rebound initiation.
    /// </summary>
    /// <returns>True if update should be halted due to collision handling.</returns>
    private bool TryHandleWallCollision()
    {
        if (!m_physics.WallContactCollision.gameObject) return false;
        if (State == GrindState.Rebound) return false;

        // Check if we're moving into the wall
        bool bMovingIntoWall = Vector3.Dot(m_physics.WallContactCollision.normal, m_physics.transform.forward) < 0;
        if (!bMovingIntoWall) return false;

        if (State == GrindState.Default)
        {
            State = GrindState.Rebound;
            return true;
        }
        else
        {
            // Hit wall during swap - just end the action
            EndAction();
            return true;
        }
    }
    
    /// <summary>
    /// Updates the lean amount animation flag
    /// </summary>
    private void UpdateLeanAmount()
    {
        var railRight = m_grindFollower.Rotation * Vector3.right;
        var leanInput = railRight.Dot(m_physics.MoveInput());
        var currentLeanAmount = AnimRailState?.GetFloat(k_animLeanAmount);
        leanInput = Mathf.Lerp(currentLeanAmount ?? 0, leanInput, 5f * Time.deltaTime);
        AnimRailState?.SetFloat(k_animLeanAmount, leanInput); // Can smooth it

        AnimRailState?.SetFloat(k_animRailSpeed, Mathf.InverseLerp(k_minGrindSpeed, k_maxGrindSpeed, Mathf.Abs(m_grindFollower.m_speed)));
    }

    #endregion

    #region Position & Alignment

    private void UpdatePosition()
    {
        Vector3 fromPos = m_physics.FootPosition;
        Vector3 offsetVector = CalculateTotalOffset();
        //Vector3 constantOffset = -m_grindFollower.Normal * 0.1f;
        Vector3 targetPoint = m_grindFollower.Position + offsetVector + m_zipGrindAnimationOffset;

        // Calculate alignment interpolation (skip during swap to avoid interference)
        float alignmentT = CalculateAlignmentT();
        
        // Accumulate root motion position (only matters if we're in zip-grind
        m_zipGrindAnimationOffset += m_physics.RootMotionDeltaPosition;
        m_zipGrindAnimationOffset = Vector3.zero; // NOTE: Temp

        targetPoint = Vector3.Lerp(fromPos, targetPoint, alignmentT);// + offsetVector + m_zipGrindAnimationOffset;
        m_physics.FootPosition = targetPoint;
    }

    private float CalculateAlignmentT()
    {
        if (m_positionAlignDuration == 0 || State == GrindState.Swap)
            return 1f;
        
        return m_timeSinceStarted / 0.25f;
    }

    /// <summary>
    /// Combines orientation offset and swap offset into final position offset.
    /// </summary>
    private Vector3 CalculateTotalOffset()
    {
        Vector3 offsetVector = CalculateOrientationOffset();
        
        if (State == GrindState.Swap)
        {
            offsetVector += CalculateSwapOffset();
        }

        return offsetVector;
    }

    private Vector3 CalculateOrientationOffset()
    {
        float targetOffset = GrindOrientation == OrientationState.Zipline ? ZipGrindVerticalOffset : 0f;
        float startOffset = GrindOrientation == OrientationState.Zipline ? 0f : ZipGrindVerticalOffset;

        float t = Mathf.Clamp01(m_sinceOrientationChanged / k_orientationTransitionDuration);
        float currentOffset = Mathf.Lerp(startOffset, targetOffset, t);

        return Normal * currentOffset;
    }

    private Vector3 CalculateSwapOffset()
    {
        float swapT = GetSwapProgress();
        
        // Lerp from initial offset to zero for smooth rail transition
        Vector3 positionOffset = Vector3.Lerp(m_initialSwapOffsetVector, Vector3.zero, swapT);
        
        // Add parabolic arc for visual appeal
        Vector3 parabolicOffset = Vector3.up * Mathf.Sin(swapT * Mathf.PI) * k_swapParabolicHeight;

        // Check if swap is complete
        if (swapT >= 1f)
        {
            State = GrindState.Default;
        }

        return positionOffset + parabolicOffset;
    }

    private float GetSwapProgress()
    {
        return m_sinceSwapStarted / 0.5f;
        //return m_railSwapAnimation?.GetState(m_animator)?.NormalizedTime 
        //       ?? m_sinceSwapStarted / m_swapTime;
    }

    #endregion

    #region Rail Swap

    private void TryRailSwap()
    {
        if (State != GrindState.Default) return;
        if (m_input == null || !m_input.GetButton(GameInput.Jump)) return;

        Vector3 swapDirection = m_physics.MoveInput();
        AnimRailState?.SetFloat(k_animRailSwapDirection, Mathf.Sign(swapDirection.Dot(m_physics.transform.right)));

        if (m_feeler.TryGetRailSwap(swapDirection, out var swapSpline, out var swapPoint))
        {
            ExecuteRailSwap(swapSpline, swapPoint);
        }
    }

    private void ExecuteRailSwap(ISplineProvider swapSpline, Vector3 swapPoint)
    {
        m_grindFollower.Init(swapSpline, m_physics, swapPoint);

        // Cache offset from current position to new rail for smooth transition
        Vector3 currentPos = GrindOrientation == OrientationState.Regular 
            ? m_physics.FootPosition 
            : m_physics.HeadPosition;
        m_initialSwapOffsetVector = currentPos - m_grindFollower.Position;

        State = GrindState.Swap;
        m_input.ConsumeInput(GameInput.Jump);
    }

    #endregion
    
    #region Rail Trick
    
    /// <summary>
    /// Simple one shot rail trick with slight boost
    /// </summary>
    private void TryRailTrick()
    {
        if (GrindOrientation != OrientationState.Regular) return;
        if (State != GrindState.Default) return;
        
        if (m_input.GetButton(GameInput.LightAttack))
        {
            m_input.ConsumeInput(GameInput.LightAttack);
            StartActionCoroutine(PerformSimpleRailTrick());
        }
    }

    private IEnumerator PerformSimpleRailTrick()
    {
        AnimRailState?.CrossFade("Rail Trick", 0.2f, 0, 0);
        //m_railTrickAnimation.Play(m_animator);
        //yield return m_railTrickAnimation.WaitForFadeOut(m_animator);
        yield return AnimRailState?.WaitForFlag(m_animator, AnimationFlags.SKID_END);
        //yield return m_railTrickAnimation.WaitForTime(m_animator, 0.7f);
        m_grindFollower.m_speed += 10 * m_grindFollower.DirectionSign;
        if (State == GrindState.Trick) State = GrindState.Default;
    }
    
    #endregion

    #region Zipline Orientation

    private void TryChangeZipOrientation()
    {
        // Can't swap orientation during transitions
        if (State != GrindState.Default) return;

        // Auto-correct if zipline position is blocked
        if (GrindOrientation == OrientationState.Zipline && !IsZipGrindPositionClear())
        {
            GrindOrientation = OrientationState.Regular;
            return;
        }

        // Force regular orientation on steep inclines
        float grindAngle = Vector3.Angle(m_grindFollower.Direction, m_physics.UpDirection);
        if (grindAngle <= k_zipGrindAngleThreshold)
        {
            GrindOrientation = OrientationState.Regular;
            return;
        }

        // Handle input-based orientation toggle
        if (m_sinceOrientationChanged > k_orientationTransitionDuration && m_input.GetButton(GameInput.VertSkip))
        {
            TryToggleOrientation();
        }
    }

    private void TryToggleOrientation()
    {
        // Validate we can switch to zipline if currently regular
        if (GrindOrientation == OrientationState.Regular && !IsZipGrindPositionClear())
            return;

        GrindOrientation = GrindOrientation == OrientationState.Regular 
            ? OrientationState.Zipline 
            : OrientationState.Regular;
        
        m_input.ConsumeInput(GameInput.VertSkip);
    }

    /// <summary>
    /// Checks if the zipline hanging position is free of collisions.
    /// </summary>
    private bool IsZipGrindPositionClear()
    {
        Vector3 queryPos = m_grindFollower.Position + m_grindFollower.Normal * ZipGrindVerticalOffset; // TODO: Use value from orientationOffset getter to have 1 source of truth
        return !m_physics.CheckCharacterCapsule(queryPos);
    }

    #endregion

    #region Rebound

    private IEnumerator PerformRebound()
    {
        //AnimRailState?.SetTrigger(k_animReboundTrigger);
        //yield return AnimRailState?.WaitForFlag(); or await
        // AnimRailState?.OnFlagInvoked(RAILGRIND_REBOUND, () =>
        // {
        //     // Launch in reverse direction
        //     m_grindFollower.m_speed = k_reboundSpeed * -originalDirectionSign;
        //     State = GrindState.Default;
        // });

        // Rotate 180 degrees to face away from wall
        m_physics.Rotation = Quaternion.AngleAxis(180f, m_physics.transform.up) * m_physics.Rotation;
        m_physics.SnapVisualRotation();
        
        // Align position to wall
        var contactPoint = m_physics.WallContactCollision.point;
        var contactNormal = m_grindFollower.Direction;
        var distToContact = -(contactPoint - m_physics.Position).Dot(contactNormal);
        m_physics.Position += contactNormal * (distToContact + m_physics.CapsuleRadius * 1.1f);

        // Pause movement during wind-up
        var originalDirectionSign = m_grindFollower.DirectionSign;
        yield return Yields.WaitForSeconds(0.5f);

        // Launch in reverse direction
        m_grindFollower.m_speed = k_reboundSpeed * -originalDirectionSign;
        State = GrindState.Default;
    }

    #endregion

    #region Physics Integration

    public void UpdateVelocity()
    {
        if (State == GrindState.Rebound) return;

        ApplyGravityToGrindSpeed();
        m_physics.Velocity = m_grindFollower.Velocity;
        m_grindFollower.UpdateFollower();
    }

    private void ApplyGravityToGrindSpeed()
    {
        // Project gravity onto rail tangent
        Vector3 tangentDirection = m_grindFollower.Direction * m_grindFollower.DirectionSign;
        float gravityAcceleration = Vector3.Dot(m_physics.GravityDir * k_gravityMultiplier, tangentDirection);
        
        m_grindFollower.m_speed += gravityAcceleration * Time.deltaTime;

        // Clamp speed while preserving direction
        float absSpeed = Mathf.Clamp(Mathf.Abs(m_grindFollower.m_speed), k_minGrindSpeed, k_maxGrindSpeed);
        m_grindFollower.m_speed = absSpeed * Mathf.Sign(m_grindFollower.m_speed);
    }

    public void UpdateRotation()
    {
        if (State == GrindState.Rebound) return;

        m_physics.Rotation = Quaternion.Slerp(
            m_physics.Rotation, 
            m_grindFollower.Rotation, 
            Time.deltaTime * k_rotationLerpSpeed);
    }

    #endregion

    #region State Management

    private void OnStateChanged(GrindState prevState)
    {
        // Since trick anim is played through a seperate mechanism, ensure we stop it
        if (prevState == GrindState.Trick && State != GrindState.Trick)
        {
            //m_railTrickAnimation.Stop(m_animator); maybe not needed
            //m_railAnimation?.Play(m_animator);
        }
        
        // TODO: State management & animation playbacks gonna change heavily once we've got anims in. Prolly Mecanim state machine responding to this ezpz since 
        // so we'd just worry about state management here, and then set the current state on the ControllerState of the anim.
        switch (m_state)
        {
            case GrindState.Default:
                //if (!AnimRailState.IsPlaying) m_railAnimation?.Play(m_animator);
                break;
            case GrindState.Rebound:
                // Play wall eject animation
                AnimRailState?.CrossFade("Rebound", 0, 0, 0.2f);
                StartCoroutine(PerformRebound());
                break;
            case GrindState.Swap:
                m_sinceSwapStarted = 0f;
                AnimRailState?.CrossFade("Rail Swap", 0, 0, 0);
                break;
        }
    }
    
    #endregion
}