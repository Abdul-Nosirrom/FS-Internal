using System;
using System.Text;
using Drawing;
using FS.Animation;
using FS.CameraSystem;
using FS.CombatSystem;
using FS.GameplayActions;
using FS.Math;
using FS.Player;
using FS.RuntimeDebug;
using FS.TagSystem;
using Lightbug.Utilities;
using PrimeTween;
using TimeUtils;
using UnityEngine;

[Serializable]
public class SpringKick : GameplayAction, IActionPhysicsReciever, IActionInputEvaluator, IDebugProvider
{
    #region Constants
    
    private const float DASH_STARTUP_FREEZE_TIME = 0.06f;
    private const float KICK_BUFFER_TIME = 0.1f;
    //private const float WALL_LOOKAHEAD_TIME = 0.2f; TODO: Breaks when chaining wall kicks
    private float WALL_LOOKAHEAD_TIME => Time.deltaTime;
    private const float WALL_EJECT_DURATION = 0.5f;
    private const float DASH_GRAVITY_MULTIPLIER = 15f;
    private const float DASH_STEERING_RATE = 3f;
    private const float WALL_ROTATION_SPEED = 10f;
    private const float HOMING_IMPACT_ANIM_THRESHOLD = 0.2f;
    private const float HOMING_ARRIVAL_TIME_THRESHOLD = 0.01f;
    
    #endregion

    #region Serialized Fields - Dash
    
    [Header("Dash")]
    [Range(0, 1)]   public float m_dashDuration = 0.5f;
    [Range(0, 5)]   public float m_airBumpSpeed = 2f;
    [Range(0, 25)]  public float m_dashSpeed = 15f;
    
    #endregion

    #region Serialized Fields - Wall Kick
    
    [Header("Wall Kick")]
    public Vector2 m_wallEjectSpeed = new Vector2(10f, 5f);
    [Range(1, 5)]   public int m_wallKickCount = 3;
    [Range(5, 90)]  public float m_minWallEjectAngle = 10f;
    
    #endregion

    #region Serialized Fields - Homing Attack
    
    [Header("Homing Attack")]
    [Range(2, 40)]  public float m_homingAttackSpeed = 20f;
    [Range(0, 1)]   public float m_homingMomentumPreserved = 0.5f;
    [Range(0, 10)]  public float m_homingReboundSpeed = 5f;
    public TargetingSettings m_homingAttackTargetingSettings = new();
    
    #endregion

    #region Serialized Fields - Enemy Step
    
    [Header("Enemy Step")] 
    [Range(0, 1)]   public float m_enemyStepWindow = 0.4f;
    [Range(1, 10)]  public float m_enemyStepLaunchHeight = 6;
    
    #endregion

    #region State Machine
    
    public enum State { Dash, WallHold, WallEject, HomingAttack, EnemyStep }
    
    [RuntimeData] private State m_state = State.Dash;
    [RuntimeData] private TimeSince m_sinceStateChange;
    [RuntimeData] private Vector3 m_positionOnStateChange;
    [RuntimeData] private Vector3 m_dashInitialVelocity;
    [RuntimeData] private Vector3 m_velocityOnStateChange;
    
    public State CurrentState
    {
        get => m_state;
        private set
        {
            if (m_state == value) return;

            var prevState = m_state;
            m_state = value;
            if (m_state == State.WallHold) m_numKicksPerformed++;
            m_sinceStateChange = 0;
            m_positionOnStateChange = m_physics.transform.position;
            m_velocityOnStateChange = m_physics.Velocity;
            
            OnStateChange(prevState, m_state);
        }
    }
    
    #endregion

    #region Runtime Data - Wall Kick
    
    [RuntimeData] private int m_numKicksPerformed = 0;
    [RuntimeData] private Vector3 m_velocityIntoWall;
    [RuntimeData] private HitInfo m_wallHit;
    [RuntimeData] private Vector3 m_wallHitPoint;
    
    #endregion

    #region Runtime Data - Homing Attack
    
    [RuntimeData] private float m_homingStartSpeed;
    [RuntimeData] private TargetingResult m_homingAttackTarget;
    [RuntimeData] private bool m_isHomingOnLevelObject = false;
    [RuntimeData] private bool m_enemyStepTriggered = false;
    [RuntimeData] private bool m_homingAttackAnimTriggered = false;
    [RuntimeData] private Vector3 m_homingAttackDirection;
    
    private float HomingAttackTimeLeft => 
        (m_homingAttackTarget.Target.transform.position - m_physics.transform.position).magnitude / m_homingAttackSpeed;
    
    #endregion

    #region Animation References
    
    private readonly AnimationReference m_anim_dashLoop = 
        AnimationReference.Get<ActionsAnimationSet>(nameof(ActionsAnimationSet.SpringLegsDashLoop));
    private readonly AnimationReference m_anim_wallEject = 
        AnimationReference.Get<ActionsAnimationSet>(nameof(ActionsAnimationSet.SpringKickWallEject));
    private readonly AnimationReference m_anim_homingImpact = 
        AnimationReference.Get<ActionsAnimationSet>(nameof(ActionsAnimationSet.HomingAttackImpact));
    private FSAnimation m_anim_homingEnemyBounce => // TODO: Limitations of AnimationReference usage, ideally would like to solve it (GetTransition has no implementation, cant use GetTimeForFlag, etc...) 
        m_animator.GetAnimationSet<ActionsAnimationSet>().HomingAttackEnemyBounce;
    private readonly AnimationReference m_anim_frontTuck = 
        AnimationReference.Get<ActionsAnimationSet>(nameof(ActionsAnimationSet.FrontFlip));
    
    #endregion

    #region Action Properties
    
    public override ActionChannel Channels => ActionChannel.Physics | ActionChannel.Rotation;
    public override bool IsRetriggerable => m_state == State.WallEject;
    
    #endregion

    #region Lifecycle Methods
    
    public override void OnInitialize(GameObject owner)
    {
        m_physics.OnPhysicsStateChanged += OnPhysicsStateChanged;
    }

    public override void OnStart()
    {
        var state = m_anim_dashLoop.Play(m_animator);
        state.NormalizedTime = 0f; // Always reset
        m_input.ConsumeInput(GameInput.Jump);
        m_physics.m_vert?.TryEndAction(this);

        CurrentState = m_homingAttackTarget.Target ? State.HomingAttack : State.Dash;
        
        if (CurrentState == State.Dash)
        {
            InitializeDashVelocity();
            m_dashInitialVelocity = m_physics.Velocity;
        }
        else // Homing Attack
        {
            InitializeHomingAttackVelocity();
            m_numKicksPerformed = 0; // Reset wall-kick count on homing attack, refresh
        }

        m_sinceStateChange = 0;
        m_enemyStepTriggered = false;
        m_homingAttackAnimTriggered = false;
    }

    public override void OnEnd()
    {
        SetTransformInfluencerMode(TransformInfluencer.Mode.None);

        if (m_anim_dashLoop.TryGetState(m_animator, out var state) && state.TargetWeight >= 1f)
            state.Layer.StartFade(0f, 0.1f);
        
        switch (CurrentState)
        {
            case State.Dash:
                m_numKicksPerformed = m_wallKickCount; // Whiffed wall-kick - prevent further kicks

                if ((m_interruptorAction == null || m_interruptorAction is AcidDropAction) && m_physics.State == PhysicsState.Air) 
                    m_anim_frontTuck.Play(m_animator);
                break;
                
            case State.HomingAttack:
            case State.EnemyStep:
                ApplyHomingAttackDamage();
                break;
        }
    }

    private void OnStateChange(State prevState, State newState)
    {
        switch (newState)
        {
            case State.Dash:
                break;
            case State.WallHold:
                m_anim_wallEject.Play(m_animator).OnFlag(m_animator, Tag.Animation_.SpringKick_.WallEject, ExecuteWallEject);
                break;
            case State.WallEject:
                break;
            case State.HomingAttack:
                break;
            case State.EnemyStep:
                m_anim_homingEnemyBounce.Play(m_animator).OnFlag(m_animator, Tag.Animation_.SpringKick_.EnemyBounce, CompleteEnemyStep);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
        }
    }
    
    #endregion

    #region Input & Conditions
    
    public bool InputEvaluate() => m_input.GetButton(GameInput.Jump);

    protected override bool StartCondition()
    {
        if (m_timeSinceEnded < KICK_BUFFER_TIME) return false;
        if (m_physics.State != PhysicsState.Air) return false;

        bool debugDraw = DebugManager.Instance.IsDebugDrawEnabledForProvider(m_actionController);
        bool hasHomingTarget = m_homingAttackTargetingSettings.PeformTargeting(
            m_physics.gameObject, 
            out m_homingAttackTarget,
            m_physics.gameObject, 
            debug: debugDraw);
        
        return hasHomingTarget || m_numKicksPerformed < m_wallKickCount;
    }
    
    #endregion

    #region Physics Updates
    
    public void UpdateVelocity()
    {
        // Short start-up freeze before full on dash
        if (m_timeSinceStarted < DASH_STARTUP_FREEZE_TIME)
        {
            m_physics.Velocity = Vector3.zero;
            return;
        }
        else if (m_timeSinceStarted < DASH_STARTUP_FREEZE_TIME + Time.deltaTime)
        {
            m_physics.GetComponent<CameraController>().AddCameraFX<CameraHoldPosition>();
        }
        
        // If grounded, end action
        if (m_physics.IsGrounded)
        {
            EndAction();
            return;
        }
        
        UpdateTransformInfluencer();
        
        switch (CurrentState)
        {
            case State.Dash:
                UpdateDashState();
                break;
            case State.WallHold:
                UpdateWallHoldState();
                break;
            case State.WallEject:
                UpdateWallEjectState();
                break;
            case State.HomingAttack:
                UpdateHomingAttackState();
                break;
            case State.EnemyStep:
                UpdateEnemyStepState();
                break;
        }
    }

    public void UpdateRotation()
    {
        if (CurrentState == State.WallHold) return;
        m_physics.RotationPhysics();
    }
    
    #endregion

    #region State Updates - Dash
    
    private void UpdateDashState()
    {
        ApplyDashPhysics();
        m_velocityIntoWall = m_physics.Velocity;

        if (TryDetectWallCollision(WALL_LOOKAHEAD_TIME))
        {
            TransitionToWallHold();
        }
        else if (m_sinceStateChange > m_dashDuration + DASH_STARTUP_FREEZE_TIME)
        {
            EndAction();
        }
    }
    
    private void UpdateWallEjectState()
    {
        ApplyDashPhysics();
        
        if (m_sinceStateChange > WALL_EJECT_DURATION)
        {
            EndAction();
        }
    }
    
    private void ApplyDashPhysics()
    {
        if (m_physics.Velocity.sqrMagnitude <= 0f)
        {
            m_physics.Velocity = m_dashInitialVelocity;
            return;
        }
        
        var inputDir = m_physics.MoveInput();

        if (inputDir.sqrMagnitude > 0)
        {
            m_physics.LateralVelocity = Vector3.Slerp(
                m_physics.LateralVelocity, 
                inputDir, 
                DASH_STEERING_RATE * Time.deltaTime
            ).normalized * m_physics.LateralSpeed;
        }
        
        m_physics.VerticalVelocity += m_physics.GravityDir * DASH_GRAVITY_MULTIPLIER * Time.deltaTime;
    }
    
    #endregion

    #region State Updates - Wall
    
    private void UpdateWallHoldState()
    {
        m_physics.Velocity = Vector3.zero;
        AlignToWall();
    }
    
    private void AlignToWall()
    {
        var animState = m_anim_wallEject.GetState(m_animator);

        // Move us to the wall within the look-ahead time, should take 0.2 seconds
        var targetPos = m_wallHitPoint + m_wallHit.normal * 0.1f;// * (m_physics.CapsuleRadius);
        float alpha = Mathf.Clamp01(animState.Time / WALL_LOOKAHEAD_TIME);
        m_physics.SetPosition(Vector3.Lerp(m_positionOnStateChange, targetPos, alpha));
        
        var forwardDir = m_wallHit.normal;
        var upDir = m_physics.UpDirection.ProjectOnPlane(forwardDir);
        var startRot = Quaternion.LookRotation(-m_velocityIntoWall.ProjectOnPlane(upDir));
        var targetRotation = Quaternion.LookRotation(forwardDir, upDir);
        m_physics.Rotation = Quaternion.Slerp(startRot, targetRotation, alpha);
    }
    
    private void ExecuteWallEject()
    {
        var wallNormal = m_wallHit.normal.ProjectOnPlane(m_physics.UpDirection).normalized;
        var reflectedDir = Vector3.Reflect(m_velocityIntoWall, wallNormal).ProjectOnPlane(m_physics.UpDirection).normalized;
        
        reflectedDir = EnforceMinimumEjectAngle(reflectedDir, wallNormal);
        
        m_physics.Velocity = reflectedDir * m_wallEjectSpeed.x + m_physics.UpDirection * m_wallEjectSpeed.y;
        m_physics.Rotation = Quaternion.LookRotation(m_physics.Velocity);
        
        CurrentState = State.WallEject;
    }
    
    private Vector3 EnforceMinimumEjectAngle(Vector3 ejectDir, Vector3 wallNormal)
    {
        float ejectAngle = Vector3.Angle(ejectDir, wallNormal);
        float maxAngle = 90f - m_minWallEjectAngle;
        
        if (ejectAngle <= maxAngle) return ejectDir;
        
        var rotAxis = wallNormal.Cross(ejectDir).normalized;
        float angleDelta = maxAngle - Mathf.Abs(ejectAngle);
        
        return Quaternion.AngleAxis(angleDelta, rotAxis) * ejectDir;
    }
    
    #endregion

    #region State Updates - Homing Attack
    
    private void UpdateHomingAttackState()
    {
        var toTarget = m_homingAttackDirection;
        float timeLeft = HomingAttackTimeLeft;

        // TODO: Animation timing doesnt sync up if the target is moving
        TryPlayHomingImpactAnimation(timeLeft);
        TryProcessEnemyStepInput(timeLeft);

        if (timeLeft < HOMING_ARRIVAL_TIME_THRESHOLD)
        {
            OnHomingAttackArrival(toTarget);
        }
        else
        {
            toTarget = (m_homingAttackTarget.Target.transform.position - m_physics.transform.position).normalized;
            m_physics.Velocity = toTarget.normalized * m_homingAttackSpeed;
        }
    }
    
    private void TryPlayHomingImpactAnimation(float timeLeft)
    {
        if (timeLeft > HOMING_IMPACT_ANIM_THRESHOLD) return;
        //if (m_isHomingOnLevelObject) return;
        if (m_homingAttackAnimTriggered) return;
        
        var homingState = m_anim_homingImpact.Play(m_animator);
        homingState.Time = 0f;
        homingState.Speed = timeLeft > 0.1f ? 1f : 0.1f / (timeLeft + 0.01f);
        m_homingAttackAnimTriggered = true;
    }
    
    private void TryProcessEnemyStepInput(float timeLeft)
    {
        if (timeLeft > m_enemyStepWindow) return;
        if (m_enemyStepTriggered) return;
        if (!m_input.GetButton(GameInput.Jump)) return;
        
        m_enemyStepTriggered = true;
        m_input.ConsumeInput(GameInput.Jump);
    }
    
    private void OnHomingAttackArrival(Vector3 toTarget)
    {
        if (m_isHomingOnLevelObject)
        {
            EndAction();
            return;
        }

        if (m_enemyStepTriggered)
        {
            CurrentState = State.EnemyStep;
            m_anim_homingEnemyBounce.Play(m_animator);
        }
        else
        {
            ApplyHomingRebound(toTarget);
            EndAction();
        }
    }
    
    private void ApplyHomingRebound(Vector3 toTarget)
    {
        var toTargetPlanar = toTarget.ProjectOnPlane(m_physics.GravityDir).normalized;
        m_physics.Velocity = toTargetPlanar * m_homingStartSpeed * m_homingMomentumPreserved 
                           + m_physics.UpDirection * m_homingReboundSpeed;
    }
    
    #endregion

    #region State Updates - Enemy Step
    
    private void UpdateEnemyStepState()
    {
        m_physics.Velocity = Vector3.zero;
        
        AnimationFlag.TryGetFlagTime(m_anim_homingEnemyBounce, Tag.Animation_.SpringKick_.EnemyBounce, out var bounceTime);
        float alpha = Easing.Evaluate(m_sinceStateChange / bounceTime, Ease.OutQuad);
        var targetPos = Vector3.Lerp(m_positionOnStateChange, m_homingAttackTarget.Target.transform.position, alpha);
        m_physics.SetPosition(targetPos);
    }
    
    private void CompleteEnemyStep()
    {
        var cameraSystem = PlayerManager.Instance.GetSubsystem<PlayerCameraSystem>(m_physics.gameObject);
        cameraSystem.AddCameraShake(CameraShake.CreateShake());
        
        ProjectileMotion.LaunchPhysicsControllerToHeight(m_physics, m_enemyStepLaunchHeight, m_physics.VerticalPhysicsParams.m_downGravity, out _, out _);
        EndAction();
    }
    
    #endregion

    #region Helper Methods
    
    private void OnPhysicsStateChanged(PhysicsState prevState, PhysicsState newState)
    {
        if (newState == PhysicsState.Air)
        {
            m_numKicksPerformed = 0;
        }
    }
    
    private void InitializeDashVelocity()
    {
        var dashDirection = GetDashDirection();
        float velocityAlongDash = Mathf.Max(Vector3.Dot(m_physics.Velocity, dashDirection), 0);
        float cappedSpeed = Mathf.Min(m_physics.LateralPhysicsParams.m_topSpeed, m_dashSpeed + velocityAlongDash);
        
        m_physics.SetVelocity(dashDirection * cappedSpeed + m_physics.UpDirection * m_airBumpSpeed);
    }
    
    private void InitializeHomingAttackVelocity()
    {
        m_homingStartSpeed = m_physics.Velocity.ProjectOnPlane(m_physics.GravityDir).magnitude;
        var toTarget = m_homingAttackTarget.Target.transform.position - m_physics.transform.position;
        m_physics.SetVelocity(toTarget.normalized * m_homingAttackSpeed);
        m_homingAttackDirection = toTarget.normalized;
        
        m_isHomingOnLevelObject = m_homingAttackTarget.Target.GetComponentInChildren<LevelObjectBase>() 
                               || m_homingAttackTarget.Target.GetComponentInParent<LevelObjectBase>();
    }
    
    private Vector3 GetDashDirection()
    {
        var dir = m_physics.MoveInput();
        
        if (dir.sqrMagnitude > 0.1f)
            return dir.normalized;
        
        dir = m_physics.Velocity.ProjectOnPlane(m_physics.UpDirection);
        if (dir.sqrMagnitude > 0.1f)
            return dir.normalized;
        
        dir = m_physics.transform.forward.ProjectOnPlane(m_physics.UpDirection);
        if (dir.sqrMagnitude > 0.1f)
            return dir.normalized;
        
        return m_physics.transform.up.ProjectOnPlane(m_physics.UpDirection).normalized;
    }
    
    private bool TryDetectWallCollision(float lookAheadTime)
    {
        bool hitCollider = m_physics.CharacterCollisionSweep(
            m_physics.Velocity.normalized, 
            m_physics.Speed * lookAheadTime, 
            out m_wallHit);

        if (!hitCollider) return false;
        if (m_physics.IsHitStableGround(m_wallHit)) return false;
        if (m_wallHit.collider3D.CompareLayer(PhysicsLayers.WallSlide)) return false;

        bool isGroundBeneathUs = m_physics.CharacterCollisionSweep(
            m_physics.GravityDir, 
            m_physics.CapsuleHeight * 0.5f + 0.1f, 
            out var groundHit);
        if (isGroundBeneathUs) return false;
        
        m_wallHitPoint = m_physics.Position + m_wallHit.distance * m_physics.Velocity.normalized;
        
        return true;
    }
    
    private void TransitionToWallHold()
    {
        CurrentState = State.WallHold;
        
        // If wall hit time is less than Lookahead time, we need to fast-forward the animation
        var state = m_anim_wallEject.Play(m_animator);
        state.Weight = 1f;
        float hitTime = m_wallHit.distance / (m_physics.Speed * WALL_LOOKAHEAD_TIME);
        //if (hitTime < WALL_LOOKAHEAD_TIME) TODO: Skipping flip into pose for now
        {
            state.Time = 0.2f;
        }

        //m_physics.Rotation = Quaternion.AngleAxis(180f, m_physics.transform.up) * m_physics.Rotation; // rotate 180
        m_physics.Rotation = Quaternion.LookRotation(m_wallHit.normal, m_physics.UpDirection); // align to wall normal
        m_physics.SnapVisualRotation(); // Force mesh rotation to also instantly snap bypassing smoothing
    }
    
    private void UpdateTransformInfluencer()
    {
        bool useVelocityFacing = CurrentState == State.Dash || CurrentState == State.HomingAttack;
        SetTransformInfluencerMode(useVelocityFacing ? TransformInfluencer.Mode.VelocityFacing : TransformInfluencer.Mode.None);
    }
    
    private void SetTransformInfluencerMode(TransformInfluencer.Mode mode)
    {
        //m_animator.GetComponent<TransformInfluencer>().ResetBase();
        m_animator.GetComponent<TransformInfluencer>().SetActiveModeWithBlend(mode, 0f);
    }
    
    private void ApplyHomingAttackDamage()
    {
        m_homingAttackTarget.Target.gameObject.ApplyDamage(
            m_physics.gameObject,
            ObjectHazardKnockbackDamage.Create()
                .WithCameraShake(CameraShakeStrength.Medium)
                .WithHitStop(HitStopStrength.Light),
            CombatAffiliation.Enemy, 
            out var failReasons);
        
        CombatService.Instance.ClearHitList(m_physics.gameObject);
    }
    
    #endregion

    #region Debug

    public string DebugName => "Spring Kick";

    public void OnDebugDraw()
    {
        if (!IsActive) return;
        
        // var labelPos = m_physics.transform.position + Vector3.up;
        // var labelText = BuildDebugLabel();
        //
        // Draw.ingame.Label2D(labelPos, labelText, 18, LabelAlignment.Center);
        
        if (CurrentState == State.WallHold)
        {
            DrawWallHitGizmos();
        }
    }

    public void OnDebugGUI()
    {
        GUILayout.Label($"State: {CurrentState}");
        GUILayout.Label($"Num Kicks Performed: {m_numKicksPerformed}");
        GUILayout.Label(BuildDebugLabel());
    }
    
    private string BuildDebugLabel()
    {
        var sb = new StringBuilder();
        sb.AppendLine("--Spring Kick--");
        sb.AppendLine($"State: {CurrentState}");
        sb.AppendLine($"Kicks Performed: {m_numKicksPerformed}/{m_wallKickCount}");
        
        switch (CurrentState)
        {
            case State.HomingAttack:
                sb.AppendLine($"Target: {m_homingAttackTarget.Target.name}");
                sb.AppendLine($"Time Left: {HomingAttackTimeLeft:F2}s");
                break;
            case State.WallHold:
                sb.AppendLine($"Wall Hit: {m_wallHit.collider3D.name}");
                break;
        }
        
        return sb.ToString();
    }
    
    private void DrawWallHitGizmos()
    {
        Draw.ingame.WireSphere(m_wallHit.point, 0.5f, Color.red);
        Draw.ingame.Arrow(m_wallHit.point, m_wallHit.point + m_wallHit.normal, Color.green);
    }
    
    #endregion
}