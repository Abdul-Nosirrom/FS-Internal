using System.Collections;
using FS.Animation;
using FS.CameraSystem;
using FS.CombatSystem;
using FS.GameplayActions;
using FS.Math;
using FS.Player;
using FS.Rendering;
using FS.RuntimeDebug;
using FS.TagSystem;
using FS.Utility;
using Lightbug.Utilities;
using Sirenix.OdinInspector;
using UnityEngine;

public class MeteorPunch : GameplayAction, IActionPhysicsReciever, IActionInputEvaluator
{
    public override ActionChannel Channels => ActionChannel.Physics;

    [Header("Dash Parameters")]
    [SerializeField] private float m_dashDuration = 0.8f;
    [SerializeField] private float m_dashGravity = 10f;
    [SerializeField] private Vector2 m_dashVelocity = new Vector2(14f, 2f);
    [SerializeField] private AnimationCurve m_dashAnimSpeed = AnimationCurve.Constant(0f, 1f, 1f);
    
    [Header("Homing Parameters")]
    [HelpBox("When input is held, we rush through, otherwise do a regular rebound. If false, we just rush through always", HelpBoxMessageType.Info)]
    [SerializeField] private bool m_performReboundWhenNoInput = true;

    [SerializeField, Min(0), ShowIf("m_performReboundWhenNoInput")] 
    private float m_verticalReboundImpulse = 10f;
    [SerializeField, Range(0, 1), ShowIf("m_performReboundWhenNoInput")] 
    private float m_lateralMomentumPreservation = 1;
    
    [Header("Homing & Dash VFX")]
    [SerializeField, VFXDropDown] private VFXBase m_homingDashVFX;
    [SerializeField, VFXDropDown] private VFXBase m_homingImpactFX;
    

    [RuntimeData] private float m_speedMultiplier = 1f;
    [RuntimeData] private RaycastHit[] m_forwardHits = new RaycastHit[4];
    
    private IAnimation m_meteorPunchAnimation;
    private IAnimation m_frontFlipRecovery;

    public static Tag ActivationTag => Tag.Action.Activation.StyleJumps.MeteorDash;

    private TargetingSettings m_homingAttackTargetingSettings;
    private TargetingResult m_homingAttackTarget;
    private Vector3 m_homingVelocity;
    
    public enum PunchState { Dash, HomingAttack }
    private PunchState m_punchState;
    
    public override void OnInitialize(GameObject owner)
    {
        m_physics.OnPhysicsStateChanged += TryResetDashCounter;
        m_meteorPunchAnimation = m_animator.GetAnimationSet<ActionsAnimationSet>().BoxingGloveMeteorDash;
        m_frontFlipRecovery = m_animator.GetAnimationSet<ActionsAnimationSet>().FrontFlip;

        m_homingAttackTargetingSettings = m_actionController.GetActionSet<PrototypeActionSet>().SpringKick
            .m_homingAttackTargetingSettings;
    }

    private void TryResetDashCounter(PhysicsState prevState, PhysicsState newState)
    {
        if (newState == PhysicsState.Air) Tags.ResetActivation(ActivationTag);
    }

    public bool InputEvaluate() => m_input.GetButton(GameInput.Jump);

    protected override bool StartCondition()
    {
        return m_physics.State == PhysicsState.Air && Tags.HasActivation(ActivationTag);
    }

    public override void OnStart()
    {
        // Can we home in?
        bool debugDraw = DebugManager.Instance.IsDebugDrawEnabledForProvider(m_actionController);
        bool hasHomingTarget = m_homingAttackTargetingSettings.PeformTargeting(
            m_physics.gameObject, 
            out m_homingAttackTarget,
            m_physics.gameObject, 
            debug: debugDraw);

        m_punchState = hasHomingTarget ? PunchState.HomingAttack : PunchState.Dash;
        
        m_meteorPunchAnimation.Play(m_animator);
        PlayerManager.GetSystem<PlayerCameraSystem>(m_physics.gameObject).AddCameraShake(CameraShake.CreateShake(CameraShakeStrength.Heavy, CameraShakeDuration.Short));
        //m_physics.GetComponent<CameraController>().AddCameraFX<CameraHoldPosition>();

        // Cancel vert if its running
        if (m_physics.m_vert.IsActive) m_physics.m_vert.TryEndAction(this);
        
        m_input.ConsumeInput(GameInput.Jump);
        Tags.ConsumeActivation(ActivationTag);

        var upVector = -m_physics.GravityDir;
        var forwardVector = m_physics.LateralVelocityDirection.IsNearlyZero(4) ? m_physics.transform.forward : m_physics.LateralVelocityDirection;
        m_speedMultiplier = 1f;

        var inputDir = m_physics.MoveInput();
        if (inputDir.sqrMagnitude > 0f)
        {
            m_speedMultiplier = Mathf.Clamp01(forwardVector.Dot(inputDir));
            forwardVector = inputDir.normalized;
        }

        float forwardBoostSpeed = m_dashVelocity.x + m_speedMultiplier * m_physics.Velocity.ProjectOnPlane(upVector).magnitude;

        float verticalSpeedDampen = 1f;
        if (m_physics.VerticalSpeed < 0)
            verticalSpeedDampen = 0.5f;
        
        if (forwardBoostSpeed > 25f && m_physics.LateralSpeed < 25f)
            forwardBoostSpeed = 25f; // clamp initial boost if we are going from 0 to something huge
        
        float targetVerticalSpeed = m_dashVelocity.y + verticalSpeedDampen * m_physics.VerticalSpeed;
        m_physics.Velocity = forwardVector * forwardBoostSpeed + upVector * targetVerticalSpeed;
        m_physics.VerticalSpeed = Mathf.Min(m_physics.VerticalSpeed, m_physics.VerticalPhysicsParams.m_maxRiseSpeed);

        m_homingVelocity = m_physics.Velocity;
        
        if (m_punchState != PunchState.HomingAttack) StartActionCoroutine(EndDashAnimation());
    }
    
    private IEnumerator EndDashAnimation()
    {
        try
        {
            while (m_timeSinceStarted < m_dashDuration)
            {
                if (m_physics.State != PhysicsState.Air) break;
                yield return Yields.WaitForNextFrame;
            }
        }
        finally
        {
            m_frontFlipRecovery.Play(m_animator);
        }
    }

    public void UpdateVelocity()
    {
        if (m_punchState == PunchState.HomingAttack)
        {
            HomingAttackVelocity();
            return;
        }
        
        float dashAlpha = Mathf.Clamp01(m_timeSinceStarted / m_dashDuration);
        float animSpeedMult = m_dashAnimSpeed.Evaluate(dashAlpha);
        m_meteorPunchAnimation.GetState(m_animator).Speed = animSpeedMult;
        
        if (m_physics.IsGrounded || (m_punchState == PunchState.Dash && m_timeSinceStarted >= m_dashDuration))
        {
            EndAction();
            return;
        }

        m_physics.VerticalSpeed -= m_dashGravity * Time.deltaTime;

        var inputDir = m_physics.MoveInput();
        var lateralVel = m_physics.LateralVelocity;
        if (inputDir.sqrMagnitude > 0f)
            lateralVel = Vector3.Slerp(lateralVel, m_physics.MoveInput(), 3 * Time.deltaTime).normalized * lateralVel.magnitude;
        m_physics.LateralVelocity = lateralVel;
        
        // TODO: For this function, we should just have a predeclared array for the hits in PhysicsController and we just dont chare about it here. Now we end up just declaring a lot of useless arrays
        if (m_physics.CharacterCollisionSweep(m_physics.transform.forward, m_physics.LateralSpeed * Time.deltaTime, out var closestHit))
        {
            var angle = Vector3.Angle(m_physics.transform.forward, -closestHit.normal);
            if (angle < 20f) EndAction();
        }
    }

    private bool m_homingEndedNaturally;
    private void HomingAttackVelocity()
    {
        m_homingEndedNaturally = false;
        var toTarget = m_homingAttackTarget.Target.transform.position - m_physics.transform.position;
        if (toTarget.sqrMagnitude < 1f)
        {
            m_frontFlipRecovery.Play(m_animator);
            Tags.ResetActivation(Tag.Action.Activation.StyleJumps.MeteorDash);
            VFXManager.Instance.PlayVFX(m_homingImpactFX, new VFXParams(){m_parent = m_homingAttackTarget.Target.transform, m_localScale = 0.1f * Vector3.one});
            m_homingEndedNaturally = true;
            
            bool isInputHeld = m_input.TimeHeld(GameInput.Jump) > 0f;
            if (!isInputHeld && !m_homingAttackTarget.Target.GetComponentInParent<LevelObjectBase>())
            {
                m_physics.Velocity = m_physics.UpDirection * m_verticalReboundImpulse +
                                     m_physics.LateralVelocity * m_lateralMomentumPreservation;
            }

            // NOTE: Given how vel is stored and applied post-hitstop, we gotta apply our rebound before then, Shitty ass race condition
            ApplyHomingAttackDamage();
            
            EndAction();
            return;
        }
        
        var speed = m_physics.Speed;

        // Vel direction rotates to target
        var velDirection = Vector3.Slerp(m_physics.VelocityDirection, toTarget.normalized, 8f * Time.deltaTime);

        // Speed accelerates if below threshold
        if (speed < 25f) speed += 30f * Time.deltaTime;

        m_physics.Velocity = velDirection * speed;
    }

    public override void OnEnd()
    {
        if (m_punchState == PunchState.HomingAttack)
        {
            if (!m_homingEndedNaturally) m_meteorPunchAnimation.Stop(m_animator);
        }
    }

    private void ApplyHomingAttackDamage()
    {
        m_homingAttackTarget.Target.gameObject.ApplyDamage(
            m_physics.gameObject,
            ObjectHazardKnockbackDamage.Create()
                .WithCameraShake(CameraShakeStrength.Medium)
                .WithHitStop(HitStopStrength.Medium),
            CombatAffiliation.Enemy, 
            out var failReasons);
        
        CombatService.Instance.ClearHitList(m_physics.gameObject);
    }
}