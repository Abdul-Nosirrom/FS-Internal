using System;
using System.Collections;
using Drawing;
using FS.Animation;
using FS.CameraSystem;
using FS.GameplayActions;
using FS.Math;
using FS.Player;
using FS.Rendering;
using FS.Utility;
using PrimeTween;
using TimeUtils;
using UnityEngine;

public class MeteorPunch : GameplayAction, IActionPhysicsReciever
{
    public override ActionChannel Channels => ActionChannel.Physics;

    [SerializeField] private float m_dashDuration = 0.8f;
    [SerializeField] private float m_dashGravity = 10f;
    [SerializeField] private Vector2 m_dashVelocity = new Vector2(14f, 2f);
    
    [SerializeField] private AnimationCurve m_dashAnimSpeed = AnimationCurve.Constant(0f, 1f, 1f);

    [RuntimeData] private float m_speedMultiplier = 1f;
    [RuntimeData] private bool m_dashReset = true;
    [RuntimeData] private RaycastHit[] m_forwardHits = new RaycastHit[4];
    
    private IAnimation m_meteorPunchAnimation;
    private IAnimation m_frontFlipRecovery;
    
    public override void OnInitialize(GameObject owner)
    {
        m_physics.OnPhysicsStateChanged += TryResetDashCounter;
        m_meteorPunchAnimation = m_animator.GetAnimationSet<ActionsAnimationSet>().BoxingGloveMeteorDash;
        m_frontFlipRecovery = m_animator.GetAnimationSet<ActionsAnimationSet>().FrontFlip;
    }

    private void TryResetDashCounter(PhysicsState prevState, PhysicsState newState)
    {
        if (newState == PhysicsState.Air) m_dashReset = true;
    }

    protected override bool StartCondition()
    {
        return m_dashReset && m_physics.State == PhysicsState.Air && m_input.GetButton(GameInput.Jump);
    }

    public override void OnStart()
    {
        m_meteorPunchAnimation.Play(m_animator);
        PlayerManager.GetSystem<PlayerCameraSystem>(m_physics.gameObject).AddCameraShake(CameraShake.CreateShake(CameraShakeStrength.Heavy, CameraShakeDuration.Short));
        //m_physics.GetComponent<CameraController>().AddCameraFX<CameraHoldPosition>();

        // Cancel vert if its running
        if (m_physics.m_vert.IsActive) m_physics.m_vert.TryEndAction(this);
        
        m_input.ConsumeInput(GameInput.Jump);
        m_dashReset = false;

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

        StartActionCoroutine(EndDashAnimation());
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
        float dashAlpha = Mathf.Clamp01(m_timeSinceStarted / m_dashDuration);
        float animSpeedMult = m_dashAnimSpeed.Evaluate(dashAlpha);
        m_meteorPunchAnimation.GetState(m_animator).Speed = animSpeedMult;
        
        if (m_physics.IsGrounded || m_timeSinceStarted >= m_dashDuration)
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
}