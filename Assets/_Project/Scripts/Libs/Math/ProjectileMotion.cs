using System.Linq;
using Drawing;
using FS.Extensions;
using FS.GameplayActions;
using TimeUtils;
using UnityEngine;

namespace FS.Math
{
    public static class ProjectileMotion
    {
        // Constraints for action system
        public class ProjectionLaunchConstraint : ActionConstraintBase
        {
            public override string Name => "Projectile Launch Constraint";

            protected override void OnEnable()
            {
                // TODO: Really the reusability of constrains needing to be per-instance is limited by this m_controller reference...
                
                // Cancel all active actions in physics channels
                foreach (var action in m_controller.ActiveActions.Actions)
                {
                    if ((action.Channels & ActionChannel.Physics) > 0)
                        action.TryEndAction();
                }
            }

            // Allow actions that dont modify physics fully only
            public override bool EvaluateConstraint(GameplayAction action) => (action.Channels & ActionChannel.Physics) == 0;
        }
        
        /// <summary>
        /// Launches a physics controller given the trajectory information provided
        /// </summary>
        public static void LaunchPhysicsController(PhysicsController physics, Vector3 start, Vector3 end,
            float gravity, float launchSpeed, out float time, out Vector3 launchVelocity)
        {
            launchVelocity = CalculateLaunchVelocity(start, end, gravity * physics.GravityDir, launchSpeed, out time);
            LaunchPhysicsController(physics, launchVelocity, gravity, time);
        }
        
        /// <summary>
        /// Launches a physics controller given the trajectory information provided
        /// </summary>
        public static void LaunchPhysicsControllerWithLaunchAngle(PhysicsController physics, Vector3 start, Vector3 end,
            float gravity, float launchAngle, out float time, out Vector3 launchVelocity)
        {
            launchVelocity = CalculateLaunchVelocityWithRelativeAngle(start, end, gravity * physics.GravityDir, launchAngle, out time);
            LaunchPhysicsController(physics, launchVelocity, gravity, time);
        }

        public static void LaunchPhysicsControllerToHeight(PhysicsController physics, float height, float gravity,
            out float time, out float launchSpeed)
        {
            launchSpeed = Mathf.Sqrt(2 * gravity * height);
            var lateralVel = physics.Velocity.WithAxis(physics.GravityDir, 0);
            physics.Velocity = lateralVel - physics.GravityDir * launchSpeed;
            physics.UnGround();

            // Try adding constraint
            if (physics.TryGetComponent<ActionController>(out var actionController))
                actionController.ActiveActions.CancelActionsInChannels(ActionChannel.Physics);

            time = Mathf.Sqrt(2 * height / gravity);
            float launchTime = time;
            TimeSince timeSinceLaunch = 0;
            physics.ModifyVerticalPhysics((controller) => timeSinceLaunch > launchTime || controller.State != PhysicsState.Air, (ref VerticalPhysicsParams value) =>
            {
                value.SetNoControl();
                value.m_upGravity = gravity;
            }, "Projectile Vertical Launch To Height");
        }

        public static void LaunchPhysicsControllerToHeight(PhysicsController physics, float height,
            out float time, out float launchSpeed) => LaunchPhysicsControllerToHeight(physics, height,
            Mathf.Abs(physics.VerticalPhysicsParams.m_upGravity), out time, out launchSpeed);

        private static void LaunchPhysicsController(PhysicsController physics, Vector3 vel, float gravity, float time)
        {
            physics.SetVelocity(vel);
            physics.UnGround();

            TimeSince launchTimer = 0f;
            
            // Try adding constraint
            if (physics.TryGetComponent<ActionController>(out var actionController))
                actionController.ActiveActions.CancelActionsInChannels(ActionChannel.Physics);
            
            // Should expire condition contain "IsGrounded"?
            physics.ModifyLateralPhysics((controller) => launchTimer > time || controller.State != PhysicsState.Air, ((ref LateralPhysicsParams value) =>
            {
                value.SetNoControl();
            }), "Projectile Launch Lateral No Control");
            physics.ModifyVerticalPhysics((controller) => launchTimer > time || controller.State != PhysicsState.Air, ((ref VerticalPhysicsParams value) =>
            {
                value.SetNoControl();
                value.SetGravity(gravity);
            }), "Projectile Launch Gravity");
        }
        
        public static void GetTrajectoryPoints(Vector3 start, Vector3 velocity, Vector3 gravity, float time, Vector3[] trajectoryPoints)
        {
            Vector3 verticalVel = velocity.ProjectOnto(gravity.normalized);
            Vector3 lateralVel = velocity.ProjectOnPlane(gravity.normalized);

            for (uint r = 0; r < trajectoryPoints.Length; r++)
            {
                float t = Mathf.Lerp(0, time, (float)r/(trajectoryPoints.Length-1));
                Vector3 lateralPos = start.ProjectOnPlane(gravity.normalized) + lateralVel * t;
                Vector3 verticalPos = start.ProjectOnto(gravity.normalized) + verticalVel * t + 0.5f * gravity * t * t;

                trajectoryPoints[r] = lateralPos + verticalPos;
            }
        }
        
        
        /// <summary>
        /// Calculates the launch velocity for simple projectile motion from Start-End with a given gravity.
        /// </summary>
        /// <param name="tf"> Time of flight </param>
        /// <returns></returns>
        public static Vector3 CalculateLaunchVelocity(Vector3 start, Vector3 end, Vector3 gravity, float launchSpeed, out float tf)
        {
            tf = (end - start).WithAxis(gravity, 0).magnitude / launchSpeed;

            Vector3 vx = launchSpeed * (end - start).WithAxis(gravity, 0).normalized;

            // Solve for vy in y = vy t + 0.5 g t^2
            Vector3 vy = ((end - start).ProjectOnto(gravity) - 0.5f * gravity * tf * tf) / tf;
            return vx + vy;
        }

        /// <summary>
        /// Calculates launch velocity to hit target at given angle relative to start→end direction
        /// </summary>
        /// <param name="launchAngle"> Angle in degrees relative to straight line to target (0° = direct, positive = arc above) </param>
        /// <param name="tf"> Time of flight </param>
        /// <returns></returns>
        public static Vector3 CalculateLaunchVelocityWithRelativeAngle(Vector3 start, Vector3 end, Vector3 gravity, float launchAngle, out float tf)
        {
            Vector3 toTarget = end - start;
            Vector3 targetDir = toTarget.normalized;
            Vector3 gravityDir = gravity.normalized;
            
            // Create rotation axis perpendicular to both target direction and gravity
            Vector3 rotationAxis = Vector3.Cross(targetDir, -gravityDir);
            
            if (rotationAxis.sqrMagnitude < 0.001f)
            {
                // Target is directly up or down - handle edge case
                rotationAxis = Vector3.Cross(targetDir, Vector3.right);
                if (rotationAxis.sqrMagnitude < 0.001f)
                    rotationAxis = Vector3.Cross(targetDir, Vector3.forward);
            }
            rotationAxis.Normalize();
            
            // Rotate target direction by launch angle to get launch direction
            Vector3 launchDir = Quaternion.AngleAxis(launchAngle, rotationAxis) * targetDir;
            
            // Now solve for speed needed in this direction
            Vector3 lateralDisplacement = toTarget.WithAxis(gravity, 0);
            Vector3 verticalDisplacement = toTarget.ProjectOnto(gravity);
            
            Vector3 launchDirLateral = launchDir.WithAxis(gravity, 0);
            Vector3 launchDirVertical = launchDir.ProjectOnto(-gravity);
            
            float dx = lateralDisplacement.magnitude;
            float dy = -verticalDisplacement.magnitude * Mathf.Sign(Vector3.Dot(verticalDisplacement, gravity));
            float g = gravity.magnitude;
            
            float vx_norm = launchDirLateral.magnitude; // cos component
            float vy_norm = launchDirVertical.magnitude; // sin component
            
            if (vx_norm < 0.001f)
            {
                Debug.LogError($"[ProjectileMotion] Launch angle too steep - no lateral component");
                tf = float.NaN;
                return Vector3.zero;
            }
            
            // From dy = (vy/vx)*dx - (g*dx²)/(2*vx²)
            // Let r = vy/vx, then: dy = r*dx - (g*dx²)/(2*vx²)
            // Solve for vx²: vx² = (g*dx²)/(2*(r*dx - dy))
            float r = vy_norm / vx_norm;
            float denominator = 2f * (r * dx - dy);
            
            if (denominator <= 0)
            {
                Debug.LogError($"[ProjectileMotion] No valid solution for angle {launchAngle}° from {start} to {end}");
                tf = float.NaN;
                return Vector3.zero;
            }
            
            float vx = Mathf.Sqrt((g * dx * dx) / denominator);
            float launchSpeed = vx / vx_norm;
            
            tf = dx / vx;
            
            return launchSpeed * launchDir;
        }

        /// <summary>
        /// Calculates the launch velocity for simple projectile motion from Start-End with a given gravity, ASSUMING that the
        /// end position is to be considered the apex
        /// </summary>
        /// <param name="tf"> Time of flight </param>
        /// <returns></returns>
        public static Vector3 CalculateLaunchVelocityIfApex(Vector3 start, Vector3 end, Vector3 gravity, out float tf)
        {
            var upDir = -gravity.normalized;
    
            // Vertical component of displacement to apex
            var yApexDelta = Vector3.Dot(end - start, upDir);
    
            // Initial vertical velocity (scalar)
            float vy0_magnitude = Mathf.Sqrt(2 * gravity.magnitude * yApexDelta);
            Vector3 vy0 = vy0_magnitude * upDir;
    
            // Time to reach apex
            tf = vy0_magnitude / gravity.magnitude;
    
            // Horizontal displacement
            Vector3 horizontalDisplacement = (end - start) - (yApexDelta * upDir);
    
            // Horizontal velocity (constant throughout flight)
            Vector3 vx0 = horizontalDisplacement / tf;
    
            return vx0 + vy0;
        }

        /// <summary>
        /// Solves quadratic equation, returning the number of solutions
        /// </summary>
        /// <param name="t1"> First Solution </param>
        /// <param name="t2"> Second Solution </param>
        /// <returns></returns>
        public static int SolveQuadratic(float a, float b, float c, out float t1, out float t2)
        {
            float discriminant = b * b - 4 * a * c;
            if (discriminant < 0)
            {
                t1 = t2 = float.NaN;
                return 0;
            }
            
            float sqrtDiscriminant = Mathf.Sqrt(discriminant);
            t1 = (-b + sqrtDiscriminant) / (2 * a);
            t2 = (-b - sqrtDiscriminant) / (2 * a);
            return discriminant == 0 ? 1 : 2;
        }

        /// <summary>
        /// Given an end-point and a current velocity for projectile motion, calculates the adjusted lateral speed
        /// to check how we could even reach our target point
        /// </summary>
        /// <param name="start"></param>
        /// <param name="currentVel"></param>
        /// <param name="targetPoint"></param>
        /// <param name="gravity"></param>
        /// <returns></returns>
        public static float CalculateRequiredTrajectorySpeed(Vector3 start, Vector3 currentVel, Vector3 targetPoint,
            Vector3 gravity, Vector3 xAxis, out float targetLateralSpeed)
        {
            targetLateralSpeed = -1f;
            
            Vector3 upDirection = -gravity.normalized;
            Vector3 forwardDirection = xAxis.ProjectOnPlane(upDirection).normalized;
            
            float g = gravity.Dot(upDirection);
            
            float startY = start.Dot(upDirection);
            float targetY = targetPoint.Dot(upDirection);
            
            float verticalSpeed = currentVel.Dot(upDirection);
            
            float lateralDist = (targetPoint - start).Dot(forwardDirection);
            
            var numSolutions = SolveQuadratic(0.5f * g, verticalSpeed, startY - targetY, out float t1, out float t2);
            if (numSolutions == 0) return -1f;

            float t = Mathf.Max(t1, t2);
            if (t < 0) return -1f;

            targetLateralSpeed = lateralDist / t;
            return t;
        }

        /// <summary>
        /// Evaluates collisions on the trajectory of a projectile. Returns true if a collision is detected.
        /// </summary>
        /// <returns>True if the trajectory path resulted in collisions</returns>
        public static bool EvaluateCollisionsOnTrajectory(Vector3 start, Vector3 velocity, Vector3 gravity, float projectileTime,
            LayerMask collisionLayer, Collider[] hits, GameObject[] ignoreObjects, out float timeOfCollision, float colliderRadius = 0.25f, uint resolution = 25)
        {
            timeOfCollision = projectileTime;

            Vector3 verticalVel = velocity.ProjectOnto(gravity.normalized);
            Vector3 lateralVel = velocity.ProjectOnPlane(gravity.normalized);

            //using var timeScope = Draw.WithDuration(10f);
            
            for (uint r = 0; r <= resolution; r++)
            {
                float alpha = r / (float)resolution;
                float t = alpha * projectileTime;
                
                Vector3 pos = start + t * lateralVel + t * verticalVel + 0.5f * gravity * t * t;

                var numOverlaps = Physics.OverlapSphereNonAlloc(pos, colliderRadius, hits, collisionLayer);
                
                
                {
                    for (int i = 0; i < numOverlaps; i++)
                    {
                        var col = hits[i];
                        if (col == null) continue;
                        if (ignoreObjects != null && ignoreObjects.Contains(col.gameObject)) continue;
                        
                        //Draw.WireSphere(pos, 0.25f, Color.red);
                        timeOfCollision = t;
                        return true;
                    }
                }
                //Draw.WireSphere(pos, 0.25f, Color.green);
            }
            
            return false;
        }

        public static void DrawProjectileGizmos(Vector3[] points, Vector3 start, Vector3 end, Vector3 gravity, float launchAngle, bool includeLabel = true, bool includeArrow = true, bool isEditor = true)
        {
            var drawing = isEditor ? Draw.editor : Draw.ingame;
            using var thickness = drawing.WithLineWidth(3);
            var launchVel = CalculateLaunchVelocityWithRelativeAngle(start, end, gravity, launchAngle, out float tf);
            GetTrajectoryPoints(start, launchVel, gravity, tf, points);
            drawing.Polyline(points, Color.crimson);
            drawing.DashedLine(start, end, 0.25f, 0.1f, Color.whiteSmoke);
            drawing.WireSphere(end, 0.4f, Color.forestGreen);
            
            if (includeArrow) drawing.Arrow(start, start + launchVel.normalized * 4f, Color.forestGreen);
            if (includeLabel) drawing.Label2D(start + Vector3.up, $"Flight Time: {tf:F2}s\n Launch Speed: {launchVel.magnitude:F1}", Color.white);
        }

#if UNITY_EDITOR
        public static bool TrajectoryAngleEndPointHandle(MonoBehaviour target, Vector3 start, ref Vector2 endLS, ref float angle)
        {
            var endWS = target.transform.TransformPoint(new Vector3(0f, endLS.y, endLS.x));
            return TrajectoryEndPointHandle(target, ref endLS) || TrajectoryAngleHandle(target, start, endWS, ref angle);
        }

        public static bool TrajectoryEndPointHandle(MonoBehaviour target, ref Vector2 endLS)
        {
            var endWS = target.transform.TransformPoint(new Vector3(0f, endLS.y, endLS.x));
            Vector3 newEndPoint = UnityEditor.Handles.PositionHandle(endWS, Quaternion.identity);
            if (newEndPoint != endWS)
            {
                UnityEditor.Undo.RecordObject(target, "Move Launch Spring End Point");
                var endLS3D = target.transform.InverseTransformPoint(newEndPoint);
                endLS = new Vector2(endLS3D.z, endLS3D.y);
                UnityEditor.EditorUtility.SetDirty(target);
                return true;
            }

            return false;
        }

        public static bool TrajectoryAngleHandle(MonoBehaviour target, Vector3 startWS, Vector3 endWS, ref float angle)
        {
            var newAngle = angle;
            
            var handleForward = (endWS - startWS).normalized;
            var handleRight = Vector3.Cross(handleForward, Vector3.up).normalized;
            var handleRot = Quaternion.LookRotation(handleForward, handleRight);
            
            bool snappingEnabled = UnityEditor.EditorSnapSettings.gridSnapEnabled;
            
            UnityEditor.EditorSnapSettings.gridSnapEnabled = false; // TODO: this should be included as part of ArcAngleHandle because it doesnt handle snapping well
            newAngle = MeshProcessing.Editor.HandlesUtility.ArcAngleHandle(
                startWS, handleRot, 4, newAngle, 89f);
            UnityEditor.EditorSnapSettings.gridSnapEnabled = snappingEnabled;
            
            if (!Mathf.Approximately(angle, newAngle))
            {
                UnityEditor.Undo.RecordObject(target, "Change Launch Angle");
                angle = Mathf.Clamp(newAngle, 1f, 89f);
                UnityEditor.EditorUtility.SetDirty(target);
                return true;
            }

            return false;
        }
#endif
    }
}