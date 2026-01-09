using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace FS.Rendering
{
    public class GrabPassRenderFeature : ScriptableRendererFeature
    {
        private class GrabPass : ScriptableRenderPass
        {
            private const string k_GrabPassTag = "GrabPass";
            private readonly FilteringSettings m_FilteringSettings;
            
            public GrabPass()
            {
                profilingSampler = new ProfilingSampler(nameof(GrabPass));
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                
                // Set up filtering for transparent objects
                m_FilteringSettings = new FilteringSettings(RenderQueueRange.transparent);
                
                // Tell URP we need the color buffer
                ConfigureInput(ScriptableRenderPassInput.Color);
            }

            private class PassData
            {
                internal TextureHandle grabTextureHandle;
                internal RendererListHandle rendererListHandle;
            }

            private void InitRenderList(ContextContainer frameData, ref PassData passData, RenderGraph renderGraph)
            {
                UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                
                var sortFlags = cameraData.defaultOpaqueSortFlags;

                DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(new ShaderTagId(k_GrabPassTag),
                    universalRenderingData, cameraData, lightData, sortFlags);
                
                var param = new RendererListParams(universalRenderingData.cullResults, drawSettings, m_FilteringSettings);
                passData.rendererListHandle = renderGraph.CreateRendererList(param);
            }
            
            static void ExecutePass(PassData data, RasterGraphContext context)
            {
                //context.cmd.ClearRenderTarget(RTClearFlags.Color, Color.green, 1,0);
            
                // Set the grab texture for shaders to sample
                context.cmd.SetGlobalTexture("_GrabPassTexture", data.grabTextureHandle);
                    
                // Draw objects with the grab pass tag
                context.cmd.DrawRendererList(data.rendererListHandle);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
            
                // First, set up our grab texture
                var source = resourceData.cameraColor;
                var destinationDesc = renderGraph.GetTextureDesc(source);
                destinationDesc.name = $"{passName}-Copy";
                destinationDesc.clearBuffer = false;
            
                var grabTexture = renderGraph.CreateTexture(destinationDesc);

                // Add the copy pass first
                renderGraph.AddCopyPass(source, grabTexture, passName: "Grab Pass Copy");

                // Then add our render pass
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Grab Pass Render", out var passData))
                {
                    
                    InitRenderList(frameData, ref passData, renderGraph);
                
                    passData.grabTextureHandle = grabTexture;
                    
                    builder.UseTexture(grabTexture);
                    builder.UseRendererList(passData.rendererListHandle);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    
                    //builder.UseGlobalTexture(grabTexture, AccessFlags.Read);
                    //builder.UseGlobalTexture(Shader.PropertyToID("_GrabPassTexture"));
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc<PassData>(ExecutePass);
                }
            }
        }

        private GrabPass m_grabPass;
        
        public override void Create()
        {
            m_grabPass ??= new GrabPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_grabPass);
        }
    }
}