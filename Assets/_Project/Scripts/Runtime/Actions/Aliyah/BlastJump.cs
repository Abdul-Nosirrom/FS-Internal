using System;
using Drawing;
using FS.Animation;
using FS.CameraSystem;
using FS.GameplayActions;
using FS.Math;
using FS.Player;
using FS.TagSystem;
using TimeUtils;
using UnityEngine;

[PlayerOnly]
public class BlastJump : GameplayAction, IActionPhysicsReciever, IActionGizmoReciever
{
    public override ActionChannel Channels => ActionChannel.Animation | ActionChannel.Physics;

    [SerializeField] private float m_blastLateralDistance = 1f;
    [SerializeField] private float m_blastApex = 2f;
    [SerializeField] private float m_launchGravity = 30f;
    
    [RuntimeData] private float m_calculatedLaunchTime;
    
    private IAnimation m_blastJumpAnimation;

    public static Tag ActivationTag => Tag.Action.Activation.FingerGunBlast;
    
    public override void OnInitialize(GameObject owner)
    {
        gameObject.GetTags().Add(ActivationTag);
        
        m_physics.OnPhysicsStateChanged += TryResetJumpCounter;
        m_blastJumpAnimation = m_animator.GetAnimationSet<ActionsAnimationSet>().FingerGunBlastJump;
    }

    private void TryResetJumpCounter(PhysicsState prevState , PhysicsState newState)
    {
        if (newState == PhysicsState.Air) gameObject.GetTags().Add(ActivationTag); // Reset
        else m_blastJumpAnimation.Stop(m_animator);
    }

    protected override bool StartCondition()
    {
        return m_input.GetButton(GameInput.Jump) && m_physics.State == PhysicsState.Air && gameObject.HasTag(ActivationTag);
    }

    public override void OnStart()
    {
        // Consume double jump tag
        gameObject.GetTags().Remove(ActivationTag);
        
        // Play animation
        m_blastJumpAnimation.Play(m_animator);
        
        // Toss in some camera shake in there
        PlayerManager.GetSystem<PlayerCameraSystem>(m_physics.gameObject).AddCameraShake(CameraShake.CreateShake(CameraShakeStrength.Medium, CameraShakeDuration.Short));
        
        // Cancel constraints
        if (m_actionController.ActiveActions.ContainsAnyChannel(ActionChannel.PhysicsConstraint))
        {
            m_actionController.ActiveActions[ActionChannel.PhysicsConstraint].TryEndAction(this);
        }
        
        m_input.ConsumeInput(GameInput.Jump);
        
        var forwardDir = m_physics.MoveInput().ProjectOnPlane(m_physics.UpDirection);
        if (forwardDir.sqrMagnitude < 0.1f)
        {
            forwardDir = m_physics.transform.forward.ProjectOnPlane(m_physics.UpDirection);
        }
        
        var targetPos = m_physics.transform.position + m_physics.UpDirection * m_blastApex + forwardDir.normalized * m_blastLateralDistance;
        var targetVel = ProjectileMotion.CalculateLaunchVelocityIfApex(m_physics.transform.position, targetPos, m_physics.GravityDir * m_launchGravity, out m_calculatedLaunchTime);

        {
            // Lateral is additive if no directional shifts
            var currentlateralVel = m_physics.Velocity.ProjectOnPlane(m_physics.UpDirection);
            var targetLateralVel = targetVel.ProjectOnPlane(m_physics.UpDirection);
            
            var currentDotTarget = currentlateralVel.Dot(targetLateralVel.normalized);
            if (currentDotTarget > 0)
            {
                targetLateralVel = targetLateralVel.normalized * (targetLateralVel.magnitude + currentDotTarget);
            }
            
            targetVel = targetLateralVel + targetVel.ProjectOnto(m_physics.UpDirection);
        }
        
        m_physics.SetVelocity(targetVel);
        
        m_physics.ModifyVerticalPhysics((physics) => m_timeSinceStarted >= m_calculatedLaunchTime, // Time instead of IsActive in case if we cancel out early we dont go too high up (as gravity reverts to normal) 
            (ref VerticalPhysicsParams vPhys) =>
            {
                vPhys.SetNoControl();
                vPhys.SetGravity(m_launchGravity);
            }, "BlastJump");
        m_physics.ModifyLateralPhysics((physics) => m_timeSinceStarted >= m_calculatedLaunchTime, 
            (ref LateralPhysicsParams lPhys) =>
            {
                //lPhys.m_airMaxSpeed = lPhys.m_topSpeed = float.MaxValue;
                lPhys.m_airDeceleration = 0;
                lPhys.m_airDrag = 0;
            }, "BlastJump");
    }

    public void UpdateVelocity()
    {
        if (m_timeSinceStarted > m_calculatedLaunchTime) EndAction();
        m_physics.LateralPhysics();
        m_physics.VerticalPhysics();
    }

    public void OnDrawActionGizmos(bool isSelected)
    {
        var forwardDir = m_physics.MoveInput().ProjectOnPlane(m_physics.UpDirection);
        if (forwardDir.sqrMagnitude < 0.1f)
        {
            forwardDir = m_physics.transform.forward.ProjectOnPlane(m_physics.UpDirection);
        }
        
        var targetPos = m_physics.transform.position + m_physics.UpDirection * m_blastApex + forwardDir.normalized * m_blastLateralDistance;
        var targetVel = ProjectileMotion.CalculateLaunchVelocityIfApex(m_physics.transform.position, targetPos, m_physics.GravityDir * m_launchGravity, out var launchDuration);

        var prevPos = m_physics.transform.position;
        for (int i = 0; i <= 24; i++)
        {
            float pct = (float) i / 24;
            float t = launchDuration * pct;

            Vector3 lateral = m_physics.transform.position.ProjectOnPlane(m_physics.UpDirection) + targetVel.ProjectOnPlane(m_physics.UpDirection) * t;
            Vector3 vertical = m_physics.transform.position.ProjectOnto(m_physics.UpDirection) + targetVel.ProjectOnto(m_physics.UpDirection) * t + 0.5f * m_launchGravity * m_physics.GravityDir * t * t;
            var pos = lateral + vertical;
            
            Draw.ingame.Line(prevPos, pos, Color.red);
            prevPos = pos;
        }
        
        Draw.ingame.Label2D(prevPos, $"tf = {launchDuration}");
    }
}