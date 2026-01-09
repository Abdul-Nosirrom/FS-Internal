using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Vector3 = UnityEngine.Vector3;

namespace FS.Editor
{
    [InitializeOnLoad]
    public static class TriggerVisualizer
    {
        private static readonly Material s_vizMaterial;
        
        private static readonly Mesh s_sphereMesh;
        private static readonly Mesh s_boxMesh;
        private static readonly Mesh s_cylinderMesh;

        private static Texture2D s_triggerTexture;

        
        private static readonly int k_insideShapeParam = Shader.PropertyToID("_InsideSign");
        private static readonly int k_shapeTypeParam = Shader.PropertyToID("_ShapeType");

        
        static TriggerVisualizer()
        {
            s_vizMaterial = new Material(Shader.Find("Hidden/Editor/TriggerVisualizer"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            s_sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            s_boxMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            s_cylinderMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            
            
            s_triggerTexture = Resources.Load<Texture2D>("T_Editor_TriggerViz");
            
            s_vizMaterial.SetTexture("_MainTex", s_triggerTexture);

            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        }

        private static void Cleanup()
        {
            if (s_vizMaterial == null) return;
            
            // Destroy the material to avoid memory leaks
            Object.DestroyImmediate(s_vizMaterial);
        }

        private static Vector3 CameraPosition
        {
            get
            {
                if (SceneView.currentDrawingSceneView != null)
                    return SceneView.currentDrawingSceneView.camera.transform.position;
                return Camera.main.transform.position;
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void OnSphereColliderGizmo(SphereCollider sphere, GizmoType type)
        {
            if (!sphere.isTrigger) return;
            Profiler.BeginSample("TriggerGizmo");

            // TODO: Bug here during playmode as SceneView.lastActiveSceneView is null

            Vector3 camPos = CameraPosition;//SceneView.lastActiveSceneView.camera.transform.position;
            Vector3 pos = sphere.transform.position + sphere.transform.rotation * Vector3.Scale(sphere.center, sphere.transform.lossyScale);
            Vector3 scale = sphere.transform.lossyScale;
            float trueScale = Mathf.Max(new[] { Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z) });
            float trueRadius = sphere.radius * trueScale;

            float insideShapeSign = Mathf.Sign(Vector3.Distance(pos, camPos) - trueRadius);
            Matrix4x4 matrix = Matrix4x4.TRS(pos, sphere.transform.rotation, trueRadius * Vector3.one);
            
            DrawTriggerVisualizer(s_sphereMesh, matrix, insideShapeSign, 0);
            Profiler.EndSample();
        }
        
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void OnBoxColliderGizmo(BoxCollider box, GizmoType type)
        {
            if (!box.isTrigger) return;

            Profiler.BeginSample("TriggerGizmo");
            
            Vector3 camPos = CameraPosition;

            Vector3 pos = box.transform.position +
                          box.transform.rotation * Vector3.Scale(box.center, box.transform.lossyScale);
            Vector3 extent = Vector3.Scale(box.size, box.transform.lossyScale);
            
            Matrix4x4 matrix = Matrix4x4.TRS(pos, box.transform.rotation, extent);
            
            float insideShapeSign = BoxOBBTest(camPos, matrix.inverse, Vector3.one);
            
            DrawTriggerVisualizer(s_boxMesh, matrix, insideShapeSign, 1);
            
            Profiler.EndSample();
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void OnCapsuleColliderGizmo(CapsuleCollider capsule, GizmoType type)
        {
            if (!capsule.isTrigger) return;
            
            Profiler.BeginSample("TriggerGizmo");

            float capRadius = capsule.radius;
            
            Vector3 pos = capsule.transform.position + capsule.transform.rotation * Vector3.Scale(capsule.center, capsule.transform.lossyScale);
            Vector3 scale = capsule.transform.lossyScale;
            Vector3 camPos = CameraPosition;
            
            float trueScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float trueRadius = capRadius * trueScale;
            float trueHeight = Mathf.Max(2 * trueRadius, Mathf.Abs(scale.y * capsule.height));

            float capHalfHeight = (trueHeight - 2 * trueRadius)/2;
            
            Vector3 topCapPos = pos + capHalfHeight * capsule.transform.up;
            Vector3 bottomCapPos = pos - capHalfHeight * capsule.transform.up;

            float cylinderInsideTest = CylinderInsideTest(camPos, capsule.transform.up, pos, capHalfHeight, trueRadius);
            float topSphereInsideTest = Mathf.Sign(Vector3.Distance(topCapPos, camPos) - trueRadius);
            float bottomSphereInsideTest = Mathf.Sign(Vector3.Distance(bottomCapPos, camPos) - trueRadius);

            float insideShapeSign = Mathf.Min(cylinderInsideTest, topSphereInsideTest, bottomSphereInsideTest);
            
            // Draw cylinder with y scale = height & xz scale = radius, shape type = 2
            {
                Vector3 baseScale = new Vector3(trueRadius, capHalfHeight, trueRadius);
                Matrix4x4 matrix = Matrix4x4.TRS(pos, capsule.transform.rotation, baseScale);
                
                DrawTriggerVisualizer(s_cylinderMesh, matrix, insideShapeSign, 2);
            }
            
            // Draw top sphere with uniform scale = radius, shape type = 3   (inside sign like a sphere)
            {
                Matrix4x4 matrix = Matrix4x4.TRS(topCapPos, capsule.transform.rotation, trueRadius * Vector3.one);
            
                DrawTriggerVisualizer(s_sphereMesh, matrix, insideShapeSign, 3);
            }
            
            // draw bottom sphere with uniform scale = radius, shape type = 4 (inside sign like a sphere)
            {
                Matrix4x4 matrix = Matrix4x4.TRS(bottomCapPos, capsule.transform.rotation, trueRadius * Vector3.one);
            
                DrawTriggerVisualizer(s_sphereMesh, matrix, insideShapeSign, 4);
            }

            Profiler.EndSample();
        }
        
        private static void DrawTriggerVisualizer(Mesh mesh, Matrix4x4 matrix, float insideShapeSign, int shapeType)
        {
            s_vizMaterial.SetInteger(k_shapeTypeParam, shapeType);
            s_vizMaterial.SetFloat(k_insideShapeParam, insideShapeSign);
            s_vizMaterial.SetPass(0);
            Graphics.DrawMeshNow(mesh, matrix);
            
            // Draw invisible gizmo for selection purposes only
            Gizmos.color = Color.clear;
            Gizmos.DrawMesh(mesh, matrix.GetPosition(), matrix.rotation, matrix.lossyScale);
        }


        private static float BoxOBBTest(Vector3 testPos, Matrix4x4 boxWorldToLocal, Vector3 boxExtent)
        {
            testPos = boxWorldToLocal.MultiplyPoint(testPos);
            
            bool isInside =
                Mathf.Abs(testPos.x) < boxExtent.x * 0.5f &&
                Mathf.Abs(testPos.y) < boxExtent.y * 0.5f &&
                Mathf.Abs(testPos.z) < boxExtent.z * 0.5f;
            
            return isInside ? -1 : 1;
        }

        private static float CylinderInsideTest(Vector3 testPos, Vector3 cylinderUp, Vector3 cylinderCenter,
            float cylinderHalfHeight, float cylinderRadius)
        {
            // Do the trivial test, is the projection distance greater than the radius?
            Vector3 testToCenter = testPos - cylinderCenter;
            Vector3 projTestToCenter = Vector3.ProjectOnPlane(testToCenter, cylinderUp);
            
            if (projTestToCenter.magnitude > cylinderRadius) return 1;
            
            // We know we're inside the bounds of the 'infinite cylinder', now check if we're within the correct height bounds
            float distToCenter = Mathf.Abs(Vector3.Dot(testToCenter, cylinderUp));
            if (distToCenter > cylinderHalfHeight) return 1;

            return -1;
        }
    }
}