using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace FS.Rendering
{
    public class EdgeDetectionOutlines : ScriptableRendererFeature
    {
        [Serializable]
        private struct EdgeSettings
        {
            public float m_thickness;

            [Range(0,1)] public float m_normalsThreshold;
            [Range(0,1)] public float m_depthThreshold;
        }
        
        private class Pass : ScriptableRenderPass
        {
            private EdgeSettings m_settings;

            private class PassData
            {
                public EdgeSettings m_settings;
                public TextureHandle m_destination;
                public TextureHandle m_intermediate;
            }

            public void Init(EdgeSettings settings) => m_settings = settings;
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
                
                var camData = frameData.Get<UniversalResourceData>();
                
                using var builder = renderGraph.AddUnsafePass<PassData>("Edge Detection", out var passData);

                passData.m_settings = m_settings;
                passData.m_destination = camData.cameraColor;
                
                passData.m_intermediate = renderGraph.CreateTexture(new TextureDesc(camData.cameraColor.GetDescriptor(renderGraph))
                {
                    name = "Edge Detection Intermediate",
                    clearBuffer = true,
                    clearColor = Color.black,
                    enableRandomWrite = true
                });

                builder.UseTexture(passData.m_intermediate, AccessFlags.ReadWrite);
                builder.UseTexture(passData.m_destination, AccessFlags.ReadWrite);
                
                builder.SetRenderFunc<PassData>(ExecutePass);
            }

            private void ExecutePass(PassData data, UnsafeGraphContext context)
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                
                m_edgeMaterial.SetFloat("_thickness", data.m_settings.m_thickness);
                m_edgeMaterial.SetFloat("_normalsThreshold", data.m_settings.m_normalsThreshold);
                m_edgeMaterial.SetFloat("_depthThreshold", data.m_settings.m_depthThreshold);

                m_edgeMaterial.SetFloat("_fogFactor", s_isSceneView ? 0f : 1f);

                Blitter.BlitCameraTexture(cmd, data.m_destination, data.m_intermediate, m_edgeMaterial, 0);
                Blitter.BlitCameraTexture(cmd, data.m_intermediate, data.m_destination);
            }
        }

        [SerializeField]
        private EdgeSettings m_settings = new EdgeSettings
        {
            m_thickness = 1f,
            m_normalsThreshold = 0.5f,
            m_depthThreshold = 0.5f
        };
        
        private Pass m_pass;
        private static Material m_edgeMaterial;
        protected static bool s_isSceneView;
        
        public override void Create()
        {
            m_pass = new();
            m_pass.Init(m_settings);
            m_pass.renderPassEvent = RenderPassEvent.BeforeRenderingSkybox;
            m_edgeMaterial = new Material(Shader.Find("Hidden/EdgeDetection"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        protected override void Dispose(bool disposing)
        {
            DestroyImmediate(m_edgeMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            s_isSceneView = renderingData.cameraData.isSceneViewCamera;
            renderer.EnqueuePass(m_pass);
        }
    }
}