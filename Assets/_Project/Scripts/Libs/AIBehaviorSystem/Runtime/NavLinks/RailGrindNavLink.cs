using System.Collections;
using Drawing;
using FS.GameplayActions;
using FS.Math;
using FS.MeshProcessing;
using Sirenix.OdinInspector;
using TimeUtils;
using UnityEngine;
using UnityEngine.Splines;
using ISplineProvider = FS.MeshProcessing.ISplineProvider;

namespace FS.AI
{
    public class RailGrindNavLink : BaseNavLink
    {
        [SerializeField] private GameObject m_railGrindObject;
        [Range(0, 0.1f), SerializeField] private float m_endsOffset = 0.05f;
        private ISplineProvider m_railGrind;

        protected override void Awake()
        {
            base.Awake();
            if (!m_railGrindObject || !EndTransform)
            {
                enabled = false;
                return;
            }
            
            m_railGrind = m_railGrindObject.GetComponent<ISplineProvider>();
            if (m_railGrind == null)
                enabled = false;
        }

        public override IEnumerator TraverseLink(PhysicsController physics, Vector3 startPoint, Vector3 endPoint)
        {
            var actionController = physics.gameObject.GetComponent<ActionController>();
            if (!actionController) yield break; // TODO: Need a way to say "navlink is false for this agent"

            var grindAction = actionController.GetActionSet<SkatingActionSet>()?.RailGrind;
            if (grindAction == null) yield break;
            
            m_railGrind.GetNearestPoint(StartTransform.position,
                out var nearestToStart, out var sT);
            if (sT > 0.9f) sT -= m_endsOffset;
            if (sT < 0.1f) sT += m_endsOffset;
            nearestToStart = m_railGrind.EvaluatePosition(sT);
            
            m_railGrind.GetNearestPoint(EndTransform.position,
                out var nearestToEnd, out var fT);
            if (fT > 0.9f) fT -= m_endsOffset;
            if (fT < 0.1f) fT += m_endsOffset;
            nearestToEnd = m_railGrind.EvaluatePosition(fT);

            var launchToStart = nearestToStart;
            var launchToEnd = nearestToEnd;
            if (Vector3.Distance(startPoint, EndTransform.position) < 1f)
            {
                launchToStart = nearestToEnd;
                launchToEnd = nearestToStart;
            }
            
            ProjectileMotion.LaunchPhysicsController(physics, physics.transform.position, launchToStart, k_gravity, k_launchSpeed, out var startJumpTime, out _);
            {
                using var thickness = Draw.WithLineWidth(4);
                using var duration = Draw.WithDuration(5);
                Draw.Cross(launchToStart, Color.cyan);
                Draw.Cross(launchToEnd, Color.cyan);
            }
            Debug.LogError($"Initial Jump To Rail: {startJumpTime} seconds");
            TimeSince sinceJumpStart = 0;
            while (sinceJumpStart <= startJumpTime)
            {
                grindAction.TryStartAction();
                if (!grindAction.IsActive) yield return new WaitForEndOfFrame();
                else break;
            }
            //yield return new WaitForSeconds(startJumpTime);

            // if (!grindAction.TryStartAction())
            // {
            //     Debug.LogError($"Failed to activate railgrind action for navlink");
            //     yield break;
            // }

            while (grindAction.IsActive)
            {
                grindAction.Speed = 10f * Mathf.Sign(grindAction.Speed);

                //if (Vector3.Distance(physics.transform.position, launchToEnd) < 1f)
                //{
                //    Debug.LogError("Prepping To Jump Off!");
                //    grindAction.TryEndAction();
                //}
                
                yield return new WaitForEndOfFrame();
            }
            // while (!grindAction.IsActive)
            // {
            //     Debug.LogError($"Waiting On Grind Action To Start");
            //     yield return new WaitForSeconds(0.2f);
            //     if (!grindAction.IsActive)
            //         yield break;
            // }
            
            // Wait until grind is over
            //yield return new WaitUntil(() => !grindAction.IsActive);
            
            Debug.LogError($"Grind Finished, Jumping Off Rail");
            
            // Jump to end point
            ProjectileMotion.LaunchPhysicsController(physics, physics.transform.position, endPoint, k_gravity, k_launchSpeed, out var endJumpTime, out _);
            yield return new WaitForSeconds(endJumpTime);
            
            //Debug.LogError($"Landed From Rail Grind NavLink In {endJumpTime}");
        }

        private const float k_gravity = 15;
        private const float k_launchSpeed = 5;
        private readonly Vector3[] debug_trajectoryViz = new Vector3[30];

        public override void DrawGizmos()
        {
            base.DrawGizmos();

            m_railGrind ??= m_railGrindObject ? m_railGrindObject.GetComponent<ISplineProvider>() : null;
            if (m_railGrind == null || !EndTransform)
            {
                using var thickness = Draw.WithLineWidth(4);
                Draw.Cross(transform.position + Vector3.up, Color.crimson);
                return;
            }

            m_railGrind.GetNearestPoint(StartTransform.position, out var nearestToStart, out var sT);
            if (sT > 0.9f) sT -= m_endsOffset;
            if (sT < 0.1f) sT += m_endsOffset;
            nearestToStart = m_railGrind.EvaluatePosition(sT);
            
            m_railGrind.GetNearestPoint(EndTransform.position,
                out var nearestToEnd, out var fT);
            if (fT > 0.9f) fT -= m_endsOffset;
            if (fT < 0.1f) fT += m_endsOffset;
            nearestToEnd = m_railGrind.EvaluatePosition(fT);

            var launchToStart = ProjectileMotion.CalculateLaunchVelocity(StartTransform.position, nearestToStart,
                Vector3.down * k_gravity, k_launchSpeed, out var tfStart);
            ProjectileMotion.GetTrajectoryPoints(StartTransform.position, launchToStart, Vector3.down * k_gravity,
                tfStart, debug_trajectoryViz);

            {
                using var thickness = Draw.WithLineWidth(4);
                Draw.Polyline(debug_trajectoryViz, Color.coral);
            }
            
            var launchToEnd = ProjectileMotion.CalculateLaunchVelocity(EndTransform.position, nearestToEnd,
                Vector3.down * k_gravity, k_launchSpeed, out var tfEnd);
            ProjectileMotion.GetTrajectoryPoints(EndTransform.position, launchToEnd, Vector3.down * k_gravity, tfEnd,
                debug_trajectoryViz);

            {
                using var thickness = Draw.WithLineWidth(4);
                Draw.Polyline(debug_trajectoryViz, Color.coral);
            }
        }
    }
}