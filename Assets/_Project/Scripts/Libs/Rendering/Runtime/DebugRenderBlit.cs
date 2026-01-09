using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FS.Rendering
{
    public class DebugRenderBlit : ScriptableRendererFeature
    {
        public class DebugPass : ScriptableRenderPass
        {
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (DebugType == Type.None) return;

                if (DebugType == Type.GameTag || DebugType == Type.PhysicsLayer || DebugType == Type.UniqueMeshes || DebugType == Type.Collision)
                {
                    renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
                    // We need a DrawRenders to a RT w/ a replacement shader
                    AddLayerOrTagPass(renderGraph, frameData);
                    return;
                }

                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                
                m_debugViewMaterial.SetInt(m_debugViewMaterialPropertyId, (int)DebugType);
                
                var cameraData = frameData.Get<UniversalResourceData>();
                var target = cameraData.activeColorTexture;
                var debugTexture = DebugType switch
                {
                    Type.Depth => cameraData.cameraDepthTexture,
                    Type.Normals => cameraData.cameraNormalsTexture,
                    Type.MotionVectors => cameraData.motionVectorColor,
                    _ => cameraData.activeColorTexture
                };
                
                ConfigureInput((ScriptableRenderPassInput)DebugType); // Forces URP to generate the normals texture for example

                var blitParams =
                    new RenderGraphUtils.BlitMaterialParameters(debugTexture, target, m_debugViewMaterial, 0);

                renderGraph.AddBlitPass(blitParams, "Debug View");
            }

            private class ObjectTagLayerData
            {
                public RendererListHandle m_rendererList;
            }

            private void AddLayerOrTagPass(RenderGraph renderGraph, ContextContainer frameData)
            {
                var renderData = frameData.Get<UniversalRenderingData>();
                var resources = frameData.Get<UniversalResourceData>();
                var camData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();
    
                using var builder = renderGraph.AddRasterRenderPass<ObjectTagLayerData>($"Debug {DebugType.ToString()} RT", out var passData);
    
                // Setup drawing settings with override material
                // Create renderer list
                {
                    DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(new ShaderTagId("UniversalForward"), renderData, camData,
                        lightData, SortingCriteria.CommonTransparent);//camData.defaultOpaqueSortFlags);
                    drawSettings.overrideMaterial = s_debugTagLayerMaterial;
                    drawSettings.overrideMaterialPassIndex = 0;
                    FilteringSettings filterSettings = FilteringSettings.defaultValue;
                    filterSettings.layerMask = -1;
                    RendererListParams renderingParams =
                        new RendererListParams(renderData.cullResults, drawSettings, filterSettings);
                    passData.m_rendererList = renderGraph.CreateRendererList(renderingParams);
                    builder.UseRendererList(passData.m_rendererList);
                }
                
                builder.SetRenderAttachment(resources.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);

                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc<ObjectTagLayerData>((data, context) =>
                {
                    context.cmd.ClearRenderTarget(true, true, Color.clear);
                    if (DebugType != Type.Collision) context.cmd.DrawRendererList(data.m_rendererList);

                    // Draw colliders if our view mode is physics layers
                    foreach (var meshCollider in s_debugMeshColliders)
                    {
                        if (!meshCollider.IsValid) continue;
                        if (meshCollider.IsDynamic) meshCollider.UpdateTransformMatrices();
                        meshCollider.Draw(context.cmd);
                    }
                });
            }
        }

        private struct MeshColliderRenderer
        {
            public MeshColliderRenderer(Collider collider)
            {
                m_collider = collider;
                CollisionMesh = null;
                ObjectToWorld = m_capsuleBottomSphereMatrix = m_capsuleTopSphereMatrix = Matrix4x4.identity;
                m_transform = collider.transform;
                MPB = new MaterialPropertyBlock();
                MPB.SetTexture("_Matcap", s_matCap);
                MPB.SetColor("_DebugColor", GetGameObjectDebugColor(collider.gameObject));
                InitMeshDrawData(collider);
            }

            public void Draw(RasterCommandBuffer cmd)
            {
                if (!IsValid) return;
                cmd.DrawMesh(CollisionMesh, ObjectToWorld, s_debugTagLayerMaterial, 0, 0, MPB);
                // If capsule, draw the top and bottom spheres too
                if (CollisionMesh == s_cylinderMesh)
                {
                    cmd.DrawMesh(s_sphereMesh, m_capsuleTopSphereMatrix, s_debugTagLayerMaterial, 0, 0, MPB);
                    cmd.DrawMesh(s_sphereMesh, m_capsuleBottomSphereMatrix, s_debugTagLayerMaterial, 0, 0, MPB);
                }
            }

            private void InitMeshDrawData(Collider collider)
            {
                ObjectToWorld = m_transform.localToWorldMatrix;
                if (collider is MeshCollider meshCollider)
                {
                    CollisionMesh = meshCollider.sharedMesh;
                }
                else if (collider is BoxCollider box)
                {
                    Vector3 pos = box.transform.position +
                                  box.transform.rotation * Vector3.Scale(box.center, box.transform.lossyScale);
                    Vector3 extent = Vector3.Scale(box.size, box.transform.lossyScale);
            
                    ObjectToWorld = Matrix4x4.TRS(pos, box.transform.rotation, extent);
                    CollisionMesh = s_boxMesh;
                }
                else if (collider is SphereCollider sphere)
                {
                    Vector3 pos = sphere.transform.position + sphere.transform.rotation * Vector3.Scale(sphere.center, sphere.transform.lossyScale);
                    Vector3 scale = sphere.transform.lossyScale;
                    float trueScale = Mathf.Max(new[] { Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z) });
                    float trueRadius = sphere.radius * trueScale;

                    ObjectToWorld = Matrix4x4.TRS(pos, sphere.transform.rotation, trueRadius * Vector3.one);
                    CollisionMesh = s_sphereMesh;
                }
                else if (collider is CapsuleCollider capsule)
                {
                    float capRadius = capsule.radius;

                    Vector3 pos = capsule.transform.position + capsule.transform.rotation * Vector3.Scale(capsule.center, capsule.transform.lossyScale);
                    Vector3 scale = capsule.transform.lossyScale;
            
                    float trueScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    float trueRadius = capRadius * trueScale;
                    float trueHeight = Mathf.Max(2 * trueRadius, Mathf.Abs(scale.y * capsule.height));

                    float capHalfHeight = (trueHeight - 2 * trueRadius)/2;
                    Vector3 baseScale = new Vector3(trueRadius, capHalfHeight, trueRadius);
                    ObjectToWorld = Matrix4x4.TRS(pos, capsule.transform.rotation, baseScale);
                    m_capsuleTopSphereMatrix = Matrix4x4.TRS(pos + capsule.transform.up * (capHalfHeight), capsule.transform.rotation, trueRadius * Vector3.one);
                    m_capsuleBottomSphereMatrix = Matrix4x4.TRS(pos - capsule.transform.up * (capHalfHeight), capsule.transform.rotation, trueRadius * Vector3.one);
                    CollisionMesh = s_cylinderMesh;
                }
            }
            
            public void UpdateTransformMatrices()
            {
                InitMeshDrawData(m_collider);
            }
            
            private static Mesh s_sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            private static Mesh s_boxMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            private static Mesh s_cylinderMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

            public bool IsValid => CollisionMesh != null;
            public bool IsDynamic => !m_transform.gameObject.isStatic;
            public Mesh CollisionMesh;
            public MaterialPropertyBlock MPB;
            public Matrix4x4 ObjectToWorld;
            private Matrix4x4 m_capsuleTopSphereMatrix;
            private Matrix4x4 m_capsuleBottomSphereMatrix;

            private Collider m_collider;
            private Transform m_transform;
        }

        private DebugPass m_debugPass;
        private static Texture2D s_matCap;
        private static Material m_debugViewMaterial;
        private static Material s_debugTagLayerMaterial;
        private static int m_debugViewMaterialPropertyId = Shader.PropertyToID("_DebugViewMode");
        private static List<MeshColliderRenderer> s_debugMeshColliders;

        public enum Type
        {
            None = 0,
            Depth = 1,
            Normals = 2,
            MotionVectors = 8,
            GameTag = 16,
            PhysicsLayer = 32,
            Collision = 64,
            UniqueMeshes = 128
        }

        private static Type s_debugType = Type.None;

        public static Type DebugType
        {
            get => s_debugType;
            set
            {
                if (s_debugType == value) return;
                s_debugType = value;
                if (s_debugType == Type.GameTag || s_debugType == Type.PhysicsLayer || s_debugType == Type.UniqueMeshes || s_debugType == Type.Collision)
                    InitRenderers();
            }
        }
        
        public override void Create()
        {
            m_debugPass = new DebugPass();
            m_debugPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            
            m_debugViewMaterial = new Material(Shader.Find("Hidden/DebugView"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            s_debugTagLayerMaterial = new Material(Shader.Find("Hidden/DebugTagLayer"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            InitRenderers();
#if UNITY_EDITOR            
            ObjectChangeEvents.changesPublished += ObjectChangedEvent;
#endif            
        }

        private static void InitRenderers()
        {
            Debug.Log($"[Debug Renderer] Initializing Tag/Layer Renderers MPBs");
            s_matCap ??= Resources.Load<Texture2D>("debug_matCap");
            s_debugMeshColliders = new();
            HashSet<GameObject> meshCollidersToSkip = new();
            var mpb = new MaterialPropertyBlock();
            mpb.SetTexture("_Matcap", s_matCap);
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                mpb.SetColor("_DebugColor", GetGameObjectDebugColor(renderer.gameObject));
                renderer.SetPropertyBlock(mpb);
                meshCollidersToSkip.Add(renderer.gameObject); // We dont wanna draw mesh colliders that also have renderers
            }

            foreach (var terrain in FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                mpb.SetColor("_DebugColor", GetGameObjectDebugColor(terrain.gameObject));
                terrain.SetSplatMaterialPropertyBlock(mpb);
            }

            var colliderType = DebugType == Type.Collision ? typeof(Collider) : typeof(MeshCollider);
            foreach (var collider in (Collider[])FindObjectsByType(colliderType, FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (meshCollidersToSkip.Contains(collider.gameObject) && DebugType != Type.Collision) continue;
                s_debugMeshColliders.Add(new (collider));
            }
        }

        private static Color GetGameObjectDebugColor(GameObject go)
        {
            // TODO: I want custom defined colors cuz the randomly generated ones look LIKE ASS
            var meshRenderer = go.GetComponent<MeshFilter>();
            var mesh = meshRenderer ? meshRenderer.sharedMesh : null;
            var collider = go.GetComponent<Collider>();
            var randomSeed = DebugType switch 
            {
                Type.PhysicsLayer => go.layer,
                Type.GameTag => go.tag.GetHashCode(),
                Type.UniqueMeshes => mesh ? go.name.GetHashCode() : -1,
                Type.Collision => collider is MeshCollider meshCol ? (meshCol.sharedMesh ? meshCol.sharedMesh.name.GetHashCode() : -1) : (collider ? collider.GetType().Name.GetHashCode() : -1),
                _ => 0
            };
            Random.InitState(randomSeed);
            return new Color(Random.Range(0.2f, 1f), Random.Range(0.2f, 1f), Random.Range(0.2f, 1f), 1f);
        }

#if UNITY_EDITOR        
        private void ObjectChangedEvent(ref ObjectChangeEventStream stream)
        {
            if (DebugType != Type.GameTag && DebugType != Type.PhysicsLayer) return;
            var evtType = stream.GetEventType(0);
            switch (evtType)
            {
                case ObjectChangeKind.CreateGameObjectHierarchy:
                case ObjectChangeKind.ChangeGameObjectStructure:
                case ObjectChangeKind.DestroyGameObjectHierarchy:
                case ObjectChangeKind.UpdatePrefabInstances:
                case ObjectChangeKind.ChangeGameObjectOrComponentProperties:                    
                    InitRenderers();
                    break;
            }
        }
#endif        

        private void OnDestroy()
        {
            DestroyImmediate(m_debugViewMaterial);
            DestroyImmediate(s_debugTagLayerMaterial);
#if UNITY_EDITOR            
            ObjectChangeEvents.changesPublished -= ObjectChangedEvent;
#endif            
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (DebugType != Type.None) renderer.EnqueuePass(m_debugPass);
        }
    }
}