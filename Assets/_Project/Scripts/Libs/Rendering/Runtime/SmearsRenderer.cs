using System;
using FS.Rendering.Utility;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace FS.Rendering
{
    public class SmearsRenderer : ScriptableRendererFeature
    {
        private class SmearsRenderPass : ScriptableRenderPass, IDisposable
        {
            private Material m_smearPostProcess;
            private readonly ShaderTagId k_SmearTag = new("SmearFramesPass");
            
            public SmearsRenderPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                m_smearPostProcess = CoreUtils.CreateEngineMaterial("Hidden/PostProcess/SmearFramesComposite");

                CreateSmearAccumulatorTextures();
            }

            public void Dispose()
            {
                CoreUtils.Destroy(m_smearPostProcess);
                DisposeSmearAccumulatorTextures();
            }
            
            RTHandle m_smearAccumRT1;
            RTHandle m_smearAccumRT2;
            
            private void CreateSmearAccumulatorTextures()
            {
                // If screen-size changes, recreate the RTs
                if (m_smearAccumRT1 != null || m_smearAccumRT2 != null)
                    DisposeSmearAccumulatorTextures();
                
                // We create 2 RTs for accumulating smears over frames
                // One is the current frame RT, the other is the previous frame RT which we blit from at the start of each frame
                // We declare them here to avoid a blit, instead just ping-ponging between them each frame
                var desc = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.ARGBFloat);
                desc.autoGenerateMips = false;
                desc.useMipMap = false; // Explicitly disable mipmaps
                
                var smearRT1 = new RenderTexture(desc)
                {
                    name = "Smear Accumulation RT 1",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                smearRT1.Create();
                m_smearAccumRT1 = RTHandles.Alloc(smearRT1);
                
                var smearRT2 = new RenderTexture(desc)
                {
                    name = "Smear Accumulation RT 2",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                smearRT2.Create();
                m_smearAccumRT2 = RTHandles.Alloc(smearRT2);
            }

            private void DisposeSmearAccumulatorTextures()
            {
                if (m_smearAccumRT1 != null)
                {
                    m_smearAccumRT1.Release();
                    m_smearAccumRT1 = null;
                }
                if (m_smearAccumRT2 != null)
                {
                    m_smearAccumRT2.Release();
                    m_smearAccumRT2 = null;
                }
            }

            private class SmearPassData
            {
                public TextureHandle CameraOpaqueTexture;
                public TextureHandle CameraDepthTexture;
                
                public TextureHandle SmearRT;
                
                public TextureHandle CurrentSmearFrame;
                public TextureHandle PrevSmearFrame;
                
                public RendererListHandle SmearRenderList;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigureInput(ScriptableRenderPassInput.Color);
                
                // Has resolution changed, recreate RTs
                if (m_smearAccumRT1 == null || m_smearAccumRT2 == null || (m_smearAccumRT1.rt.width != Screen.width || m_smearAccumRT1.rt.height != Screen.height))
                    CreateSmearAccumulatorTextures();
                
                var camData = frameData.Get<UniversalResourceData>();

                var smearRenderList = renderGraph.CreateRendererList(new() { k_SmearTag }, frameData, RenderQueueRange.all);

                using var smearPassBuilder =
                    renderGraph.AddUnsafePass<SmearPassData>("Smear Frames Pass", out var passData);

                smearPassBuilder.AllowPassCulling(false);
                smearPassBuilder.AllowGlobalStateModification(true);
                
                // Temporal accumulation shiiiiiiiiiiiiiiiiiiiiiiit
                passData.CurrentSmearFrame = renderGraph.ImportTexture(m_smearAccumRT1); // Current RT we'll accumulate too
                passData.PrevSmearFrame = renderGraph.ImportTexture(m_smearAccumRT2); // Past frame accumulation
                
                // Object mask drawing shit
                var smearRTDesc = camData.activeColorTexture.GetDescriptor(renderGraph);
                smearRTDesc.depthBufferBits = 0;
                smearRTDesc.autoGenerateMips = false;
                smearRTDesc.useMipMap = false; // Explicitly disable mipmaps
                smearRTDesc.name = "Smear Objects Mask";
                var smearMask = renderGraph.CreateTexture(smearRTDesc);
                
                passData.SmearRT = smearMask; // Mask RT we draw current frame smear mask to
                passData.SmearRenderList = smearRenderList; // Renderers that write to smear mask

                passData.CameraOpaqueTexture = camData.activeColorTexture;
                passData.CameraDepthTexture = camData.activeDepthTexture;
                
                smearPassBuilder.UseTexture(passData.CurrentSmearFrame, AccessFlags.ReadWrite);
                smearPassBuilder.UseTexture(passData.PrevSmearFrame, AccessFlags.ReadWrite);
                smearPassBuilder.UseTexture(passData.SmearRT, AccessFlags.ReadWrite);
                smearPassBuilder.UseRendererList(smearRenderList);
                
                smearPassBuilder.UseTexture(passData.CameraOpaqueTexture, AccessFlags.ReadWrite);
                smearPassBuilder.UseTexture(passData.CameraDepthTexture, AccessFlags.Read);
                
                smearPassBuilder.SetRenderFunc<SmearPassData>(ExecuteSmears);
            }

            private void ExecuteSmears(SmearPassData data, UnsafeGraphContext renderGraphContext)
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(renderGraphContext.cmd);

                // Draw the current player onto the smear mask RT
                cmd.SetRenderTarget(data.SmearRT);//, data.DepthTexture);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetGlobalTexture("_CameraTexture", data.CameraOpaqueTexture);
                cmd.DrawRendererList(data.SmearRenderList);
                
                // Blit the pervious frames accumulated smear mask onto the current one, using previous VP matrices to offset
                {
                    // Composite the prev frame (decay it) w/ the new current frame mask to create a temporal accumulation effect
                    cmd.SetGlobalTexture("_CurrentSmearFrame", data.SmearRT);
                    cmd.SetGlobalTexture("_PrevSmearFrame", data.PrevSmearFrame);
                    Blitter.BlitCameraTexture(cmd, data.SmearRT, data.CurrentSmearFrame, m_smearPostProcess, 1);
                }
                
                // Finally composite the smear texture over the main camera texture
                Blitter.BlitCameraTexture(cmd, data.CurrentSmearFrame, data.CameraOpaqueTexture, m_smearPostProcess, 0);
                
                // Ping-pong the RTs for next frame
                (m_smearAccumRT1, m_smearAccumRT2) = (m_smearAccumRT2, m_smearAccumRT1);
            }
        }

        private SmearsRenderPass m_pass;
        
        public override void Create()
        {
            m_pass = new SmearsRenderPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_pass);
        }
    }
}