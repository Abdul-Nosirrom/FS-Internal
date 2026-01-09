using Animancer;
using Drawing;
using FS.Animation;
using FS.GameplayActions;
using FS.Math;
using FS.MeshProcessing;
using Lightbug.Utilities;
using Unity.Mathematics;
using UnityEngine;

public class LedgeClimbConstraint : ActionConstraintBase
{
    public override bool EvaluateConstraint(GameplayAction action) => (action is not RailGrindAction && action is not WallSlide);
}

public class LedgeClimbAction : GameplayAction, IActionUpdateReciever, IActionPhysicsReciever, IActionGizmoReciever
{
    public override ActionChannel Channels => ActionChannel.Animation | ActionChannel.Physics | ActionChannel.Rotation;

    public const float k_maxForwardDetection = 0.75f;
    public const float k_maxLedgeHeight = 0.5f;
    public const float k_verticalDownSweepDelta = 0.4f;
    public const float k_forwardDownSweepDelta = 0.1f;
    public const float k_floorCheckDist = 1f;
    
    private HitInfo m_ledgeForwardHit;
    private HitInfo m_ledgeGroundHit;
    
    private AnimationReference m_ledgeClimbAnimation = AnimationReference.Get<ActionsAnimationSet>("LedgeClimb");

    public override ActionConstraintBase DefaultConstraint => m_ledgeClimbConstraint;
    private LedgeClimbConstraint m_ledgeClimbConstraint = ActionConstraintBase.Create<LedgeClimbConstraint>("LedgeClimbConstraint");

    private bool m_jumpRequested = false;
    
    public override void OnInitialize(GameObject owner)
    {
        m_actionController.ConstraintHandler.AddConstraint(m_ledgeClimbConstraint);
    }

    protected bool TryDetectLedges()
    {
        // Is there a floor nearly under us? If so we can't ledge grab
        bool hitFloor = m_physics.CharacterCollisionSweep(m_physics.Position, m_physics.GravityDir, k_floorCheckDist, out var groundHit);
        
        // Pick a ledge detection direction, we first use input, but then fallback to forward direction
        var forwardDir = m_physics.InputForwardDirection;
        
        // Trace forward to check for an obstruction that may be a ledge
        if (!m_physics.CharacterCollisionSweep(m_physics.Position, forwardDir, k_maxForwardDetection, out m_ledgeForwardHit))
            return false;
        
        // From the hit point, move upwards, a bit forwards, then cast downwards to see where we'd stand
        // If there's a hit, we know where the ledge grab is
        var verticalStart = (m_physics.HeadPosition + m_physics.UpDirection * m_physics.CapsuleRadius).Dot(m_physics.UpDirection);
        float verticalSweepDist = m_physics.CapsuleRadius + m_physics.CapsuleHeight / 2f; // sweep to center
        
        var downCastStart = m_ledgeForwardHit.point - m_ledgeForwardHit.normal * k_forwardDownSweepDelta;
        downCastStart = downCastStart.WithAxis(m_physics.UpDirection, verticalStart);
        if (!m_physics.CharacterCollisionSweep(downCastStart, -m_physics.UpDirection, verticalSweepDist, out m_ledgeGroundHit))
            return false;
        
        // (maybe) the two should be the same collider
        if (m_ledgeForwardHit.collider3D != m_ledgeGroundHit.collider3D)
            return false;

        // Is our downwards hit stable to stand on?
        var edgeAngle = Vector3.Angle(m_ledgeForwardHit.normal, m_ledgeGroundHit.normal);
        var floorAngle = Vector3.Angle(m_ledgeGroundHit.normal, -m_physics.GravityDir);
        if (edgeAngle < 60f || edgeAngle > 110f) return false; // More of a slope than an edge or a sorta weird convex shape
        if (floorAngle > 35f) return false; // Too steep to ledge grab to
        
        // Can we stand on it? (no penetration/overlap at our final 'standing' point)
        var queryPos = m_ledgeGroundHit.point + m_ledgeGroundHit.normal * (m_physics.CapsuleRadius + 0.1f);
        if (m_physics.CheckCharacterCapsule(queryPos)) return false;
        
        // Disallow ledge grabbing on the proper lip of a QP
        if (m_ledgeForwardHit.collider3D.CompareLayer(PhysicsLayers.Vert) && m_ledgeForwardHit.collider3D.TryGetComponent<ISplineProvider>(out var vertSpline))
        {
            var vertInfo = SkateUtility.GetVertInfoFromSpline(vertSpline, m_ledgeGroundHit.point, -m_physics.GravityDir, m_ledgeForwardHit.normal);
            float dot = vertInfo.constraintNormal.Dot(m_ledgeForwardHit.normal);
            if (dot > 0) return false; // Oriented towards where we wanna grab, this is a lip
        }
        
        return !hitFloor;
    }

    protected override bool StartCondition()
    {
        bool ledgesDetected = TryDetectLedges();
        // Are we falling? (if we're railgrinding, dont check for falling)
        if (m_physics.IsGrounded || (m_physics.Velocity.Dot(m_physics.GravityDir) < 0 && m_physics.State != PhysicsState.RailGrind))
            return false;
        
        // Any skate actions active?
        var skateSet = m_actionController.GetActionSet<SkatingActionSet>();
        if (skateSet == null || skateSet.Vert.IsActive || skateSet.AcidDrop.IsActive || skateSet.SpineTransfer.IsActive)
            return false;

        return ledgesDetected;
    }

    private Vector3 m_localLedgeGrabPos;
    private Quaternion m_localLedgeGrabRot;
    
    private Vector3 m_startPos;
    private Quaternion m_startRot;
    
    private static async void FadeOutAnimation(AnimancerState anim)
    {
        await Awaitable.WaitForSecondsAsync(anim.Duration);
        anim.Layer?.StartFade(0);
    }
    
    public override void OnStart()
    {
        //m_ledgeClimbConstraint.EnableConstraint();
        
        //FadeOutAnimation(m_ledgeClimbAnimation.Play(m_animation));
        m_ledgeClimbAnimation.Play(m_animator);

        var grabCharPos = (m_ledgeForwardHit.point + m_ledgeForwardHit.normal * (m_physics.CapsuleRadius + 0f*0.1f)).ProjectOnPlane(m_ledgeGroundHit.normal);
        grabCharPos += (m_ledgeGroundHit.point - m_ledgeGroundHit.normal * (0.25f + m_physics.CapsuleHeight)).ProjectOnto(m_ledgeGroundHit.normal);

        m_localLedgeGrabPos = m_ledgeGroundHit.transform.InverseTransformPoint(grabCharPos);
        
        var grabCharRot = Quaternion.LookRotation(-m_ledgeForwardHit.normal, m_ledgeGroundHit.normal);
        m_localLedgeGrabRot = Quaternion.Inverse(m_ledgeGroundHit.transform.rotation) * grabCharRot;

        m_physics.Velocity = Vector3.zero;
        
        m_startPos = m_physics.Position;
        m_startRot = m_physics.Rotation;

        m_physics.Position = grabCharPos;

        m_jumpRequested = false;
    }
    
    public void PosUpdate()
    {
        if (m_physics.IsGrounded) EndAction();
        //m_ledgeClimbAnimation.Play(m_animation).MoveTime(m_timeSinceStarted.Relative, false);

        float blendAlpha = m_timeSinceStarted / 0.1f;
        
        var grabWorldPos = m_ledgeGroundHit.transform.TransformPoint(m_localLedgeGrabPos);
        var grabWorldRot = m_ledgeGroundHit.transform.rotation * m_localLedgeGrabRot;
        grabWorldPos = Vector3.Lerp(m_startPos, grabWorldPos, blendAlpha);
        grabWorldRot = Quaternion.Slerp(m_startRot, grabWorldRot, blendAlpha);

        // NOTE: teleport during update to skip physics interpolation which the way we're doing it now if this was fixed update causes jitters if attached to a moving platform
        m_physics.Teleport(grabWorldPos, grabWorldRot);
    }

    public void OnUpdate()
    {
        PosUpdate();
        
        if (m_jumpRequested || m_input.GetButton(GameInput.Jump))
        {
            if (m_timeSinceStarted <= 0.1f)
            {
                if (!m_jumpRequested) m_input.ConsumeInput(GameInput.Jump);
                m_jumpRequested = true;
            }
            else
            {
                var platformVel = m_ledgeGroundHit.IsRigidbody
                    ? m_ledgeGroundHit.rigidbody3D.GetPointVelocity(m_physics.Position)
                    : Vector3.zero;
                m_physics.Velocity = platformVel;// + 10f * m_physics.UpDirection;
                ProjectileMotion.LaunchPhysicsControllerToHeight(m_physics, 4f, m_physics.VerticalPhysicsParams.m_upGravity, out _, out _);
                EndAction();
                m_input.ConsumeInput(GameInput.Jump);
                
                if (m_ledgeClimbAnimation.TryGetState(m_animator, out var state) && state is ControllerState controllerState)
                    controllerState.SetTrigger("LedgeJump");
            }
        }
    }
    
    public void OnDrawActionGizmos(bool isSelected)
    {
        if (!IsActive) return;
        
        using var duration = Draw.ingame.WithDuration(10f);
        Draw.ingame.WireSphere(m_ledgeForwardHit.point, 0.1f, Color.red);
        Draw.ingame.WireSphere(m_ledgeGroundHit.point, 0.1f, Color.green);
        Draw.ingame.Arrow(m_ledgeForwardHit.point, m_ledgeForwardHit.point + m_ledgeForwardHit.normal, Color.yellow);
        Draw.ingame.Arrow(m_ledgeGroundHit.point, m_ledgeGroundHit.point + m_ledgeGroundHit.normal, Color.red);

    }
}