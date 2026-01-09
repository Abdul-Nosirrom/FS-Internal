using Drawing;
using FS.GameplayActions;
using FS.Math;
using FS.MeshProcessing;
using TimeUtils;
using Unity.Mathematics;
using UnityEngine;
using ISplineProvider = FS.MeshProcessing.ISplineProvider;

public class VertAction : GameplayAction, IActionPhysicsContraintReceiver, IActionPhysicsReciever, IActionGizmoReciever, IActionUpdateReciever
{
    private class VertConstraint : ActionConstraintBase
    {
        public override bool EvaluateConstraint(GameplayAction action) => action is not RailGrindAction;
    }
    
    public override ActionChannel Channels => ActionChannel.PhysicsConstraint | ActionChannel.Rotation;

    private readonly ActionConstraintBase m_constraint = ActionConstraintBase.Create<VertConstraint>("Vert Constraint");
    public override ActionConstraintBase DefaultConstraint => m_constraint;

    private ISplineProvider m_vertSpline;
    public GameObject m_vertObject { get; private set; }

    /// <summary>
    /// Wanna keep track of this as if we skip in the first 0.2 seconds then release the skipVert input, we can re-enter vert
    /// </summary>
    private TimeSince m_sinceSkippedVert;
    
    protected override bool StartCondition()
    {
        if (m_physics.State != PhysicsState.Air) return false;
        if (m_sinceSkippedVert < 0.2f) return false;
        if (m_physics.m_timeSinceLostGround > 0.2f) return false;
        if (m_physics.Velocity.Dot(m_physics.GravityDir) >= 0) return false; // Moving downwards
        
        if (m_physics.LastStableGround.CompareLayer(PhysicsLayers.Vert))
        {
            if (m_physics.LastStableGround.GroundSlopeAngle > 30f)
            {
                m_vertSpline = m_physics.LastStableGround.GroundObject.GetComponent<ISplineProvider>();
                m_vertObject = m_physics.LastStableGround.GroundObject;

                if (m_vertSpline != null)
                {
                    m_vertInfo = SkateUtility.GetVertInfoFromSpline(m_vertSpline, m_physics.FootPosition, -m_physics.GravityDir, m_physics.transform.up);
                    
                    if (m_physics.LastStableGround.Normal.Dot(m_vertInfo.constraintNormal) <= 0) return false;
                    
                    return !ShouldSkipVert();
                }
            }
            Debug.Log($"Failed to start vert due to bad normal angle {m_physics.LastGround.GroundSlopeAngle}");
        }
        
        return false;
    }

    public override void OnStart()
    {
        // Slight puuush
        if (m_physics.Velocity.y < 5)
            m_physics.AddVelocity(Vector3.up * 5);

        SkateUtility.UpdateVertInfoFromSpline(m_vertSpline, m_physics.FootPosition, -m_physics.GravityDir, ref m_vertInfo);

        // Disable air friction/decceleration
        m_physics.ModifyLateralPhysics((controller) => !IsActive, (ref LateralPhysicsParams param) =>
        {
            param.m_airDrag = 0;
            param.m_airDeceleration = 0f;
        }, "VertAirFric");
        
        // m_physics.ModifyVerticalPhysics((controller) => !IsActive, (ref VerticalPhysicsParams param, float time) =>
        // {
        //     param.SetGravity(5f);
        // }, "VertGrav");
    }

    public override void OnEnd()
    {}

    private RaycastHit[] m_vertSweepHits = new RaycastHit[4];
    public void OnFixedUpdate()
    {
        if (m_physics.IsGrounded)
        {
            EndAction();
            return;
        }

        // Only sweep if we're going down
        if (m_physics.Velocity.Dot(m_physics.UpDirection) > 0) return;
        
        // Check if we're gonna collide with something to force us out of vert depending on the collisions validity
        if (m_physics.CharacterCollisionSweep(m_physics.transform.position + m_vertInfo.constraintNormal * 0.2f, -m_physics.UpDirection, 2f, out var closestHit))
        {
            if (closestHit.collider3D.CompareLayer(PhysicsLayers.Vert))
            {
                var verticalToLip = Vector3.Project(m_vertInfo.lipPosition - m_physics.FootPosition, -m_physics.GravityDir);
                if (closestHit.collider3D.gameObject == m_vertObject ||
                    m_vertObject.transform.IsChildOf(closestHit.collider3D.transform))
                {
                    if (verticalToLip.magnitude < 0.5f)
                    {
                        m_physics.TrySnapToGround(m_vertInfo.lipPosition);
                        return;
                    }
                }
            }

            // For vert faces, the top-face might be considered an obstruction w/ this detection, we don't wanna consider it TODO
            if (Vector3.Distance(closestHit.point, m_vertInfo.lipPosition) < 1f) return;
            
            if (m_physics.IsHitStableGround(closestHit))
            {
                //m_physics.SetRotation(Quaternion.LookRotation(m_physics.transform.up, closestHit.normal));
                EndAction();
                
                // Speed up rotation alignment for a bit
                m_physics.ModifyRotationPhysics((physics) => m_timeSinceEnded > 0.2f,
                    (ref RotationPhysicsParams param) =>
                    {
                        param.m_revertToAirUpRate = 20;
                    }, "Vert");
            }
        }
    }

    [RuntimeData] public SkateUtility.VertInfo m_vertInfo;
    [RuntimeData] private bool ShouldCheckForNewVertSpline => m_vertSpline == null || !m_vertSpline.IsClosed() && (m_vertInfo.time is >= 1 or <= 0);
    [RuntimeData] public bool HasSurpassedSkipVertThreshold => m_timeSinceStarted > 0.3f;
    
    public void ApplyVelocityConstraints(Vector3 frameStartVelocity)
    {
        // Have we completed the spline we're on and encountered another one underneath us that we should align to?
        if (ShouldCheckForNewVertSpline)
        {
            float belowDistance = Vector3.Distance(m_vertInfo.lipPosition, m_physics.FootPosition) * 1.25f;
            var newSpline = SkateUtility.MaybeGetQuarterPipeBelowUs(m_physics.FootPosition, m_physics.GravityDir, m_physics.transform.up, belowDistance);
            if (newSpline != null) // Update data so we start following the new spline correctly
            {
                m_vertInfo = SkateUtility.GetVertInfoFromSpline(newSpline, m_physics.FootPosition, -m_physics.GravityDir, m_physics.transform.up);
                m_vertSpline = newSpline;
                m_vertObject = (m_vertSpline as MonoBehaviour)?.gameObject ?? m_vertObject;
            }
        }
        
        SkateUtility.UpdateVertInfoFromSpline(m_vertSpline, m_physics.FootPosition, -m_physics.GravityDir, ref m_vertInfo);
        
        var velDelta = (m_physics.Velocity - frameStartVelocity).ProjectOnPlane(m_vertInfo.constraintNormal);
        m_physics.Velocity = (frameStartVelocity.ProjectOnPlane(m_vertInfo.constraintNormal) + velDelta);
        //currentVelocity = Vector3.ProjectOnPlane(currentVelocity, m_normal).normalized * currentVelocity.magnitude;
        
        // Ensure feet position is properly aligned
        var planarPosition = Vector3.ProjectOnPlane(m_physics.FootPosition, m_vertInfo.constraintNormal);
        var currentPosition = Vector3.Project(m_physics.FootPosition, m_vertInfo.constraintNormal);
        var targetPosition = Vector3.Project(m_vertInfo.lipPosition, m_vertInfo.constraintNormal);
        //var currentPosition = Vector3.Project()

        targetPosition = Vector3.Lerp(currentPosition, targetPosition, 5f * Time.deltaTime); //Mathf.Clamp01(m_timeSinceStarted/0.2f));

        m_physics.SetFootPosition(planarPosition + targetPosition);
        
        // If we're vertically close to our target and falling, try snapping to it as ground:
        {
            var verticalToLip = Vector3.Project(m_vertInfo.lipPosition - m_physics.FootPosition, -m_physics.GravityDir);
            if (!ShouldCheckForNewVertSpline && verticalToLip.magnitude < 0.5f && m_physics.Velocity.Dot(m_physics.GravityDir) > 0)
            {
                //using var duration = Draw.ingame.WithDuration(5);
                bool col = m_physics.TrySnapToGround(m_vertInfo.lipPosition);
                //Draw.ingame.Label2D(m_vertInfo.lipPosition, "FORCED LANDING", col ? Color.green : Color.red);
                if (col)
                    EndAction();
            }
        }

        if (ShouldSkipVert()) EndAction();
    }

    public void UpdateRotation()
    {
        var targetRotation = Quaternion.LookRotation(m_physics.Velocity, m_vertInfo.constraintNormal);
        m_physics.Rotation = Quaternion.Slerp(m_physics.Rotation, targetRotation, 8f * Time.deltaTime);
    }

    public void OnFoundGround()
    {
        EndAction();
    }

    public bool ShouldSkipVert()
    {
        bool skipped = m_input.TimeHeld(GameInput.VertSkip) > 0;
        if (skipped)
        {
            m_sinceSkippedVert = 0;
            m_animator.GetAnimationSet<ActionsAnimationSet>()?.FrontFlip?.Play(m_animator);
        }
        return skipped;
    }

    public void OnDrawActionGizmos(bool isSelected)
    {
        if (!IsActive) return;

        using var thickness = Draw.ingame.WithLineWidth(4f);
        Draw.ingame.WireSphere(m_vertInfo.lipPosition, m_physics.CapsuleRadius, Color.green);
        Draw.ingame.Arrow(m_vertInfo.lipPosition, m_vertInfo.lipPosition + m_vertInfo.constraintNormal * 0.5f, Color.red);
    }
}