using System;
using Drawing;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FS.DevTools
{
    public class PhysicsResultsVisualizer : MonoBehaviourGizmos
    {
        private enum PhysicsTest
        {
            Sweep, Overlap
        }
        private enum ShapeType
        {
            Ray,
            Sphere,
            Box,
            Capsule
        }
        
        [SerializeField] private LayerMask m_layerMask = ~0;
        [SerializeField] private PhysicsTest m_test = PhysicsTest.Sweep;
        [SerializeField] private ShapeType m_shape = ShapeType.Ray;
        
        [SerializeField, Range(0, 50), HideIf("IsOverlapTest")] private float m_distance = 5f;
        [SerializeField,  HideIf("IsOverlapTest")] private bool m_sweepMulti = false;

        [SerializeField, Range(0, 10), HideIf("HideSphereSettings")]
        private float m_sphereRadius = 0.5f;
        
        [SerializeField, Range(0, 10), HideIf("HideCapsuleSettings")]
        private float m_capsuleHalfHeight = 0.5f;
        [SerializeField, Range(0, 10), HideIf("HideCapsuleSettings")]
        private float m_capsuleRadius = 0.25f;
        
        [SerializeField, HideIf("HideBoxSettings")]
        private Vector3 m_boxHalfExtents = Vector3.one * 0.5f;
        
        private bool IsOverlapTest => m_test == PhysicsTest.Overlap;
        private bool HideRaySettings => m_shape != ShapeType.Ray;
        private bool HideSphereSettings => m_shape != ShapeType.Sphere;
        private bool HideCapsuleSettings => m_shape != ShapeType.Capsule;
        private bool HideBoxSettings => m_shape != ShapeType.Box;

        private RaycastHit[] DebugRaycast(Vector3 start, Vector3 dir)
        {
            RaycastHit[] result = null;
            
            if (m_sweepMulti)
                result = Physics.RaycastAll(start, dir, m_distance, m_layerMask);
            else if (Physics.Raycast(start, dir, out var hit, m_distance, m_layerMask))
                result = new[] { hit };
            
            if (result != null && !m_sweepMulti)
            {
                var hit = result[0];
                Draw.Line(transform.position, hit.point, Color.red);
            }
            else 
                Draw.Line(start, start + dir * m_distance, Color.green);
            
            return result;
        }
        
        private RaycastHit[] DebugSphereCast(Vector3 start, Vector3 dir)
        {
            RaycastHit[] result = null;
            float radius = m_sphereRadius;
            
            if (m_sweepMulti)
                result = Physics.SphereCastAll(start, radius, dir, m_distance, m_layerMask);
            else if (Physics.SphereCast(start, radius, dir, out var hit, m_distance, m_layerMask))
                result = new[] { hit };
            
            Draw.WireSphere(start, radius, Color.green);
            
            if (result != null && !m_sweepMulti)
            {
                foreach (var hit in result)
                {
                    Draw.WireSphere(hit.point, radius, Color.red);

                    Draw.Line(transform.position, hit.point);
                }
            }
            
            return result;
        }
        
        private RaycastHit[] DebugBoxCast(Vector3 start, Vector3 dir)
        {
            RaycastHit[] result = null;
            Quaternion orientation = transform.rotation;
            Vector3 halfExtents = m_boxHalfExtents;
            
            if (m_sweepMulti)
                result = Physics.BoxCastAll(start, halfExtents, dir, orientation, m_distance, m_layerMask);
            else if (Physics.BoxCast(start, halfExtents, dir, out var hit, orientation, m_distance, m_layerMask))
                result = new[] { hit };
            
            Draw.WireBox(start, orientation, halfExtents * 2f, Color.green);
            
            if (result != null && !m_sweepMulti)
            {
                foreach (var hit in result)
                {
                    Vector3 shapeCenter = start + dir * hit.distance;
                    Draw.WireBox(shapeCenter, orientation, halfExtents * 2f, Color.red);

                    Draw.Line(transform.position, hit.point);
                }
            }
            
            return result;
        }
        
        private RaycastHit[] DebugCapsuleCast(Vector3 start, Vector3 dir)
        {
            RaycastHit[] result = null;
            Quaternion orientation = transform.rotation;
            float halfHeight = m_capsuleHalfHeight;
            float radius = m_capsuleRadius;
            Vector3 point1 = start + orientation * Vector3.up * halfHeight;
            Vector3 point2 = start + orientation * Vector3.down * halfHeight;
            
            if (m_sweepMulti)
                result = Physics.CapsuleCastAll(point1, point2, radius, dir, m_distance, m_layerMask);
            else if (Physics.CapsuleCast(point1, point2, radius, dir, out var hit, m_distance, m_layerMask))
                result = new[] { hit };

            Draw.WireCapsule(point1, point2, radius, Color.green);

            if (result != null)
            {
                foreach (var hit in result)
                {
                    Vector3 shapeCenter = start + dir * hit.distance;
                    var hitPoint1 = shapeCenter + orientation * Vector3.up * halfHeight;
                    var hitPoint2 = shapeCenter + orientation * Vector3.down * halfHeight;
                    Draw.WireCapsule(hitPoint1, hitPoint2, radius, Color.red);

                    Draw.Line(transform.position, hit.point, Color.red);
                }
            }
            
            
            return result;
        }

        private void DebugRaycastResults(RaycastHit[] hits)
        {
            if (hits == null) return;
            
            foreach (var hit in hits)
            {
                var normalEnd = hit.point + hit.normal;
                
                Draw.Arrow(hit.point, normalEnd, Color.blue);
                Draw.WireSphere(hit.point, 0.1f, Color.red);
                Draw.Label2D(normalEnd, $"Time = {hit.distance/m_distance}");
                
                if (hit.collider is MeshCollider mesh)
                {
                    var tris = mesh.sharedMesh.triangles;
                    var vertIdx = hit.triangleIndex;
                    var v0 = mesh.transform.TransformPoint(mesh.sharedMesh.vertices[tris[vertIdx * 3 + 0]]);
                    var v1 = mesh.transform.TransformPoint(mesh.sharedMesh.vertices[tris[vertIdx * 3 + 1]]);
                    var v2 = mesh.transform.TransformPoint(mesh.sharedMesh.vertices[tris[vertIdx * 3 + 2]]);
                    Draw.WireTriangle(v0, v1, v2, Color.cyan);

                    var v01 = (v1 - v0);
                    var v02 = (v2 - v0);
                    var faceNormal = Vector3.Cross(v01, v02).normalized;
                    Draw.Arrow(hit.point, hit.point + faceNormal, Color.crimson);
                }
            }
        }
        
        public override void DrawGizmos()
        {
            //if (!GizmoContext.InSelection(this)) return;

            using var thickness = Draw.WithLineWidth(2);

            if (IsOverlapTest)
            {
                
            }
            else
            {
                Draw.Line(transform.position, transform.position + transform.forward * m_distance, Color.yellow);
                switch (m_shape)
                {
                    case ShapeType.Ray:
                        var rayHits = DebugRaycast(transform.position, transform.forward);
                        DebugRaycastResults(rayHits);
                        break;
                    case ShapeType.Sphere:
                        var sphereHits = DebugSphereCast(transform.position, transform.forward);
                        DebugRaycastResults(sphereHits);
                        break;
                    case ShapeType.Box:
                        var boxHits = DebugBoxCast(transform.position, transform.forward);
                        DebugRaycastResults(boxHits);
                        break;
                    case ShapeType.Capsule:
                        var capsuleHits = DebugCapsuleCast(transform.position, transform.forward);
                        DebugRaycastResults(capsuleHits);
                        break;
                }
            }
        }
    }
}