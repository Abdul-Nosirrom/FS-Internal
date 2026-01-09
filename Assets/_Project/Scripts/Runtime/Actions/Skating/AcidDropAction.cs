using Drawing;
using FS.Math;
using FS.GameplayActions;
using FS.MeshProcessing;
using PrimeTween;
using UnityEngine;

public class AcidDropAction : GameplayAction, IActionPhysicsReciever, IActionGizmoReciever, IActionInputEvaluator
{
    private class SkateTransferConstraint : ActionConstraintBase
    {
        public override bool EvaluateConstraint(GameplayAction action)
        {
            Debug.LogWarning($"Evaluating SkateTransferConstraint for {action.GetType().Name}");
            return action is not RailGrindAction or VertAction;
        }
    }
    
    public override ActionChannel Channels => ActionChannel.Physics | ActionChannel.Rotation;

    [SerializeField, Range(0, 50)] private float m_maxAcidDropDistance;

    private readonly ActionConstraintBase m_skateTransferConstraint = ActionConstraintBase.Create<SkateTransferConstraint>("Skate Transfer Vert Blocker");
    public override ActionConstraintBase DefaultConstraint => m_skateTransferConstraint;

    [RuntimeData] public SkateUtility.VertInfo m_vertInfo;
    [RuntimeData] public Vector3 m_targetLateralVel;
    [RuntimeData] public float m_acidDropTime;

    [RuntimeData] public Quaternion m_startRotation;
    [RuntimeData] public Vector3 m_startPos;
    [RuntimeData] public Vector3 m_startVelocity;
    
    private const float k_gravityMag = -25f;

    private Collider[] m_acidDropTrajectoryHits = new Collider[4];
    private GameObject[] m_ignoredCollisions = new GameObject[3];

    private VertAction m_vert;
    private bool FromVert => m_vert != null && m_vert.IsActive;
    public bool IsSpineTransfer { get; private set; }
    
    public override void OnInitialize(GameObject owner)
    {
        m_ignoredCollisions[0] = owner;

        m_vert = owner.GetComponent<ActionController>().GetActionSet<SkatingActionSet>().Vert;
    }

    public bool InputEvaluate() => m_input.GetButton(GameInput.Interact);

    protected override bool StartCondition()
    {
        if (m_physics.IsGrounded) return false;

        Vector3 lateralVel = m_physics.Velocity.ProjectOnPlane(m_physics.GravityDir);
        Vector3 verticalVel = m_physics.Velocity.ProjectOnto(m_physics.GravityDir);
        Vector3 velDirection = FromVert ? -m_physics.transform.up : lateralVel.normalized;
        if (velDirection == Vector3.zero) return false;
        
        // Cant trigger spine transfer if we're falling & not rising
        if (FromVert && verticalVel.Dot(m_physics.GravityDir) >= 0) return false;

        int physResolution = Mathf.FloorToInt(m_maxAcidDropDistance / 0.25f);
        RaycastHit? m_acidDropHit = null;

        m_ignoredCollisions[1] = null;
        if (FromVert)
        {
            // Spine transfer instead of acid drop
            m_ignoredCollisions[1] = m_vert.m_vertObject;
        }

        
        
        // Collision check to find possible acid drops
        for (int i = 0; i <= physResolution; i++)
        {
            float t = (float)i / physResolution;
            // TODO: Ideally our vertical offset would be at the peak of our trajectory
            Vector3 castPos = m_physics.FootPosition + velDirection * m_maxAcidDropDistance * t - m_physics.GravityDir * 5f; // Slight vertical offset
            Vector3 dir = m_physics.GravityDir;
            float maxCastDist = 40;

            Physics.queriesHitTriggers = true; // NOTE: Allow hitting triggers for level object queries
            bool acidDropQuery = Physics.SphereCast(castPos, 0.25f, dir, out var hit, maxCastDist, 1 << PhysicsLayers.Vert);
            Physics.queriesHitTriggers = false;

            if (acidDropQuery)
            {
                var vertCollider = hit.collider.GetComponent<ISplineProvider>();
                if (vertCollider == null && !hit.collider.CompareTag(GameTags.CannonDrop)) continue; // Neither a proper vert or level object
                m_ignoredCollisions[2] = hit.collider.gameObject;

                if (!TryEvaluateDropAsLevelObject(hit)) // Could be a level object we wanna drop into, if so we compte things differently as it has no spline n shit
                {
                    // We ignore top-hits, hoping to hit near the curve
                    var normalAngle = Vector3.Angle(hit.normal, m_physics.UpDirection);
                    if (normalAngle < 30f) continue;

                    //if (!vertCollider) continue;

                    // TODO: Better landing position result
                    // if (false && !FromVert && ProjectileMotion.FindTrajectoryPointClosestToDescendingHeight(m_physics.Position, m_physics.Velocity, Mathf.Abs(k_gravityMag) * m_physics.GravityDir, hit.point.Dot(m_physics.UpDirection), out var targetPos))
                    // {
                    //     // Change hit.point to be our projectile position at hit.points height
                    //     hit.point = targetPos;
                    //     Debug.Log($"Reaching oooout");
                    //     using var duration = Draw.ingame.WithDuration(5f);
                    //     Draw.ingame.WireSphere(hit.point, 0.2f, Color.cyan);
                    // }

                    m_vertInfo = SkateUtility.GetVertInfoFromSpline(vertCollider, hit.point, -m_physics.GravityDir,
                        m_physics.transform.up);
                }

                var toClosestEdge = m_vertInfo.lipPosition - m_physics.FootPosition;
                if (toClosestEdge.Dot(velDirection) <= 0) continue;
                if (m_vertInfo.constraintNormal.Dot(velDirection) <= 0) continue;
                
                // Evaluate necessary trajectory
                m_acidDropTime = ProjectileMotion.CalculateRequiredTrajectorySpeed(m_physics.FootPosition, m_physics.Velocity, m_vertInfo.lipPosition,
                    Mathf.Abs(k_gravityMag) * m_physics.GravityDir, velDirection, out var targetSpeed);

                if (m_acidDropTime <= 0) continue;

                // We couldn't make it to begin with and we're attempting an acid drop not a spine transfer
                if (!FromVert && targetSpeed > lateralVel.magnitude) return false; // NOTE: Check for later (24/12/2025) why does this return false and not continue?
        
                m_targetLateralVel = velDirection * targetSpeed;

                // Evaluate collisions along the trajectory path
                bool doesCollide = ProjectileMotion.EvaluateCollisionsOnTrajectory(m_physics.transform.position,
                    m_targetLateralVel + verticalVel, Mathf.Abs(k_gravityMag) * m_physics.GravityDir, m_acidDropTime,
                    PhysicsLayers.GetLayerCollisionMask(PhysicsLayers.Character), m_acidDropTrajectoryHits, m_ignoredCollisions,
                    out var timeOfCollision);
                
                if (doesCollide && timeOfCollision < m_acidDropTime)
                {
                    continue;
                }
                
                m_acidDropHit = hit;
                break;
            }
        }
        
        if (m_acidDropHit == null) return false;
        
        {
            // using var timeScope = Draw.WithDuration(10f);
            // using var thickScope = Draw.WithLineWidth(2f);
            //
            // Draw.Label2D(m_closestVertEdge, $"Drop Duration: {m_acidDropTime}");
            // Draw.SolidBox(m_closestVertEdge, 0.5f, Color.green);
            // Draw.Arrow(m_closestVertEdge, m_closestVertEdge + m_vertNormal, Color.purple);
            //
            // Draw.Arrow(m_physics.FootPosition, m_physics.FootPosition + lateralVel.normalized, Color.blue);
        }
        
        m_input.ConsumeInput(GameInput.Interact);

        IsSpineTransfer = FromVert;
        
        return true;
    }

    private bool TryEvaluateDropAsLevelObject(RaycastHit hit)
    {
        
        if (!hit.collider.gameObject.CompareTag(GameTags.CannonDrop)) return false;
        
        // These are always valid, we just gotta reconsutrct vert info
        m_vertInfo.lipPosition = hit.collider.transform.position - m_physics.transform.forward * m_physics.CapsuleHeight/2f; // center us
        m_vertInfo.constraintNormal = m_physics.transform.forward;
        m_vertInfo.normalSign = 1;
        m_vertInfo.tangent = m_physics.transform.up;
        return true;
    }

    private void QueryPossibleDropPoint(Vector3 targetPos)
    {
        
    }

    public override void OnStart()
    {
        if (m_vert != null && m_vert.IsActive) m_vert.TryEndAction();
        
        // Set our target lateral vel
        m_startVelocity = m_targetLateralVel + m_physics.Velocity.ProjectOnto(m_physics.GravityDir);
        m_physics.SetVelocity(m_startVelocity);
        
        m_startRotation = m_physics.transform.rotation;
        m_startPos = m_physics.FootPosition;
        
        // Disable collisions
        //m_physics.MotoDebug.SetCapsuleCollisionsActivation(false);
    }
    
    public override void OnEnd()
    {
        if (m_interruptorAction != null) return; // Makes no sense to do the below if we're interrupted

        var floorProbePos = m_vertInfo.lipPosition + m_vertInfo.constraintNormal * m_physics.CapsuleRadius * 0.1f;
        m_physics.Position = floorProbePos;
        if (m_physics.TrySnapToGround(floorProbePos)) // TODO: Problem if we do this, we don't have floor info till next frame!!!
        {
            //float verticalSpeed = m_startVelocity.Dot(-m_physics.GravityDir) + k_gravityMag * m_acidDropTime;
            //m_physics.SetVelocity(m_physics.GravityDir * Mathf.Abs(verticalSpeed));
            m_physics.Velocity =
                m_physics.Velocity.ProjectOnPlane(m_physics.Ground.Normal).normalized * m_physics.Speed;
        }
    }

    public void UpdateVelocity()
    {
        // Ensure velocity is correct for the trajectory
        //m_physics.Velocity = Vector3.zero;// m_targetLateralVel + currentVelocity.ProjectOnto(m_physics.GravityDir);
        
        Vector3 verticalVel = m_startVelocity.ProjectOnto(m_physics.GravityDir.normalized);
        Vector3 lateralVel = m_startVelocity.ProjectOnPlane(m_physics.GravityDir.normalized);
        
        float t = Mathf.Clamp(m_timeSinceStarted, 0f, m_acidDropTime);
        m_physics.Velocity = lateralVel + verticalVel - m_physics.GravityDir * (k_gravityMag * t);

        //Vector3 pos = m_startPos + t * lateralVel + t * verticalVel + 0.5f * Mathf.Abs(k_gravityMag) * m_physics.GravityDir * t * t;
        
        //currentVelocity += m_physics.GravityDir * Mathf.Abs(k_gravityMag) * deltaTime;
        
        float alpha = Mathf.Clamp01(t / m_acidDropTime);
        float fastAlpha = Easing.Evaluate(Mathf.Clamp01(t / (m_acidDropTime - 0.05f)), Ease.Linear); // Orient position 0.05s faster than full duration
        var pos = m_startPos.ProjectOnto(m_physics.GravityDir) + t * verticalVel + 0.5f * Mathf.Abs(k_gravityMag) * m_physics.GravityDir * t * t;

        var lateralPos = Vector3.Lerp(m_startPos.ProjectOnPlane(m_physics.GravityDir),
            m_vertInfo.lipPosition.ProjectOnPlane(m_physics.GravityDir), fastAlpha);
        var verticalPos = pos.ProjectOnto(m_physics.GravityDir);
        //pos += Vector3.Lerp(m_startPos.ProjectOnPlane(m_physics.GravityDir), m_vertInfo.lipPosition.ProjectOnPlane(m_physics.GravityDir), alpha);
        m_physics.SetFootPosition(lateralPos + verticalPos);
        if (alpha >= 1)
        {
            float verticalSpeed = Mathf.Abs(m_startVelocity.Dot(-m_physics.GravityDir) + k_gravityMag * m_acidDropTime);
            verticalSpeed = Mathf.Max(verticalSpeed, Mathf.Max(m_startVelocity.magnitude, m_physics.LateralPhysicsParams.m_topSpeed)); // Ensure some minimum bounce
            m_physics.Speed = verticalSpeed;
            EndAction();
        }
    }

    public void UpdateRotation()
    {
        float alpha = m_timeSinceStarted / m_acidDropTime;

        var forward = m_physics.GravityDir;
        if (!FromVert && false) // TODO: Better landing orientation
        {
            forward = Vector3.ProjectOnPlane(m_physics.Velocity, m_vertInfo.constraintNormal).normalized;
            if (forward.Dot(m_physics.UpDirection) >= 0)
                forward = m_physics.GravityDir;
        }

        var targetRot = Quaternion.LookRotation(forward, m_vertInfo.constraintNormal);
        m_physics.Rotation = Quaternion.Slerp(m_startRotation, targetRot, alpha);

    }

    public void OnFoundGround()
    {
        // Force set velocity to the analytical solution
        {
            //Debug.Log($"Found ground through AcidDrop!");
            //float verticalSpeed = m_startVelocity.Dot(-m_physics.GravityDir) + k_gravityMag * m_acidDropTime;
            //m_physics.SetVelocity(m_physics.GravityDir * Mathf.Abs(verticalSpeed));
        }
        //using var timeScope = Draw.ingame.WithDuration(10f);
        //
        //Draw.ingame.WireSphere(m_physics.Ground.Point, 0.1f, Color.red);
        //EndAction();
    }

    public void OnDrawActionGizmos(bool isSelected)
    {
        if (!IsActive) return;
        Draw.ingame.Label2D(m_vertInfo.lipPosition + Vector3.up, $"Alpha: {m_timeSinceStarted / m_acidDropTime}");

        Draw.ingame.WireSphere(m_vertInfo.lipPosition + m_vertInfo.constraintNormal * m_physics.CapsuleRadius, m_physics.CapsuleRadius,
            Color.crimson);
        Draw.ingame.Arrow(m_vertInfo.lipPosition, m_vertInfo.lipPosition + m_vertInfo.constraintNormal, Color.blue);
    }
}