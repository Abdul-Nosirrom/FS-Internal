using System.Collections;
using Drawing;
using FS.Math;
using TimeUtils;
using UnityEngine;

namespace FS.AI
{
    public class JumpNavLink : BaseNavLink
    {
        public float m_gravity = 15f;
        public float m_launchSpeed = 10f;

        public override IEnumerator TraverseLink(PhysicsController physics, Vector3 startPoint, Vector3 endPoint)
        {
            ProjectileMotion.LaunchPhysicsController(physics, startPoint, endPoint, m_gravity, m_launchSpeed, out var launchTime, out var launchVelocity);
            
            Debug.LogWarning($"Traverse Time == {launchTime} seconds");

            yield return new WaitForSeconds(launchTime);
        }
        
        public override void DrawGizmos()
        {
            base.DrawGizmos();
            if (end == null)
            {
                Debug.Log("End Not Specified!");
                return;
            }

            var launchVel = ProjectileMotion.CalculateLaunchVelocity(StartTransform.position, EndTransform.position,
                -Vector3.up * m_gravity, m_launchSpeed, out float tf);

            Vector3[] points = new Vector3[51];

            Vector3 plane = launchVel.ProjectOnPlane(-Vector3.up);
            for (int i = 0; i <= 50; i++)
            {
                float t = Mathf.Lerp(0, tf, (float)i/50);
                Vector3 x = StartTransform.position.ProjectOnPlane(Vector3.up) + plane * t;
                float y = StartTransform.position.y + launchVel.y * t - 0.5f * m_gravity * t * t;

                points[i] = x + Vector3.up * y;//new Vector3(0, y, x);
            }

            using var thickness = Draw.WithLineWidth(3f);
            Draw.Polyline(points, Color.coral);
        }
    }
}