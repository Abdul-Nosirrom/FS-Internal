using System;
using System.Linq;
using Drawing;
using FS.Player;
using TimeUtils;
using UnityEditor;
using UnityEngine;

namespace FS.AI
{
    public struct AIPerceptionResult
    {
        public GameObject Target;
        public Vector3 TargetPosition => Target != null ? Target.transform.position : Vector3.positiveInfinity;
        public bool HasTarget => Target != null;
        public TimeSince SinceLastSeen;
        public bool HasLineOfSight;
        public RaycastHit LineOfSightObstruction;
        public Vector3 LastKnownPosition;
        
        public void Clear()
        {
            Target = null;
            //SinceLastSeen = 0;
            HasLineOfSight = false;
        }
    }
    
    [RequireComponent(typeof(AIKnowledge))]
    public class AISensors : MonoBehaviourGizmos
    {
        /// <summary>
        /// The maximum distance at which the AI can detect objects or entities within its view.
        /// Represents the range of perception for AI sensors, allowing adjustments to fine-tune detection capabilities.
        /// </summary>
        [Header("Player View Settings")] [SerializeField, Range(1, 35)] private float m_viewDistance = 15f;

        /// <summary>
        /// The angular field of view, within which the AI can detect objects or entities.
        /// Defines the spread of the AI's vision cone in degrees, allowing adjustments to
        /// control how wide or narrow its perception is.
        /// </summary>
        [SerializeField, Range(1, 89)] private float m_viewAngle = 89f;

        /// <summary>
        /// The radius of the AI's peripheral vision. The target is considered detected if it enters this radius.
        /// </summary>
        [SerializeField, Range(1, 5)] private float m_peripheralRadius = 2f;

        public AIPerceptionResult PerceptionResult;


        public void UpdateTargetPerception()
        {
            foreach (var player in PlayerManager.Instance.Players)
            {
                if (player.Instance == null) continue;
                var playerPos = player.Instance.transform.GetChild(0).position;

                if (IsPositionInPeripheralView(playerPos) || IsPositionInViewCone(playerPos))
                {
                    
                    if (HasLineOfSightTo(playerPos))
                    {
                        PerceptionResult.Target = player.Instance.transform.GetChild(0).gameObject;
                        PerceptionResult.LastKnownPosition = playerPos;
                        PerceptionResult.SinceLastSeen = 0;
                        return;
                    }
                }
            }

            PerceptionResult.Clear();
        }

        public bool IsPositionInPeripheralView(Vector3 pos)
        {
            return Vector3.Distance(transform.position, pos) <= m_peripheralRadius;
        }

        public bool IsPositionInViewCone(Vector3 pos)
        {
            var toPos = pos - transform.position;
            if (toPos.sqrMagnitude > m_viewDistance * m_viewDistance)
                return false;
            toPos.Normalize();
            var angleToPos = Vector3.Angle(transform.forward, toPos);
            return (angleToPos <= m_viewAngle);
        }

        public bool HasLineOfSightTo(Vector3 targetPos)
        {
            Ray ray = new Ray(transform.position, (targetPos - transform.position).normalized);
            float dist = Vector3.Distance(transform.position, targetPos);
            PerceptionResult.HasLineOfSight = !Physics.Raycast(ray, out var hit, dist, PhysicsLayers.GetLayerCollisionMask(PhysicsLayers.CameraObstruction));
            PerceptionResult.LineOfSightObstruction = hit;
            return PerceptionResult.HasLineOfSight;
        }
        
        #region Gizmo Debug
        
        private (Vector3[] vertices, int[] triangles, Color[] colors) m_coneMeshData;
        private const int Debug_ConeSides = 32;
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Generate cone mesh vertices & tris for gizmos
            Vector3[] vertices = new Vector3[Debug_ConeSides + 1];
            Color[] colors = new Color[vertices.Length];
            int[] triangles = new int[Debug_ConeSides * 3 * 2]; // *2 for cone sides + cap

            float angleStep = 360f / Debug_ConeSides;
            float halfAngleRad = Mathf.Deg2Rad * (m_viewAngle);
            float coneRadius = Mathf.Tan(halfAngleRad) * m_viewDistance;

            // Cone tip
            vertices[0] = Vector3.zero;
            colors[0] = Color.white;

            // Circle vertices
            for (int i = 0; i < Debug_ConeSides; i++)
            {
                float angle = i * angleStep;
                float rad = Mathf.Deg2Rad * angle;
                float x = Mathf.Cos(rad) * coneRadius;
                float y = Mathf.Sin(rad) * coneRadius;

                vertices[i + 1] = new Vector3(x, y, 0) + Vector3.forward * m_viewDistance;
                colors[i + 1] = Color.white;
            }

            // Build cone side triangles
            for (int i = 0; i < Debug_ConeSides; i++)
            {
                int currentVertex = i + 1;
                int nextVertex = (i + 1 < Debug_ConeSides) ? i + 2 : 1; // Wrap to first circle vertex
    
                triangles[i * 3 + 0] = 0;           // Apex
                triangles[i * 3 + 1] = currentVertex; // Current
                triangles[i * 3 + 2] = nextVertex;   // Next
            }

            // Build cap triangles (fan from first circle vertex)
            int capStartIndex = Debug_ConeSides * 3; // Start after cone triangles
            for (int i = 0; i < Debug_ConeSides - 2; i++)
            {
                triangles[capStartIndex + i * 3 + 0] = 1;     // First circle vertex (pivot)
                triangles[capStartIndex + i * 3 + 1] = i + 2; // Current
                triangles[capStartIndex + i * 3 + 2] = i + 3; // Next
            }

            m_coneMeshData = (vertices, triangles, colors);
        }
#endif
        
        public override void DrawGizmos()
        {
            var coneColor = PerceptionResult.HasTarget ? Color.green : Color.red;
            coneColor.a = 0.3f;

            {
                using var space = Draw.WithMatrix(transform.localToWorldMatrix);
                using var color = Draw.WithColor(coneColor);

                Draw.SolidMesh(m_coneMeshData.vertices.ToList(), m_coneMeshData.triangles.ToList(),
                    m_coneMeshData.colors.ToList());
            }

            coneColor.a = 1;
            Draw.WireSphere(transform.position, m_peripheralRadius, coneColor);
        }

        #endregion
    }
}