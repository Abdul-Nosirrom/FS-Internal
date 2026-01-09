using System;
using FS.Rendering;
using TimeUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FS.CameraSystem
{
    /// <summary>
    /// Base class to imlpement FX like a Dolly Zoom, or visual screen-space effects that modify the specific camera
    /// This can be pooled
    /// </summary>
    public abstract class CameraFX // TODO: Pooling?
    {
        public enum Mode { OrbitVector, CameraState, Render }

        public abstract Mode FXMode { get; }
        public bool IsActive { get; private set; }
        public TimeSince TimeSinceStarted { get; private set; }
        
        private CameraBehaviorController m_cameraController;
        
        protected Camera m_camera => m_cameraController != null ? m_cameraController.m_camera : null;

        protected ref CameraOrbitVector m_cameraVector
        {
            get
            {
                if (FXMode != Mode.OrbitVector)
                    throw new InvalidOperationException($"[CameraSystem] Trying to access CameraOrbitVector on CameraFX of mode {FXMode}. This is only valid for CameraFX of mode OrbitVector.");
                return ref m_cameraController.m_cameraVector;
            }
        }
        
        protected CameraState m_cameraState 
        {
            get
            {
                if (FXMode != Mode.CameraState)
                    throw new InvalidOperationException($"[CameraSystem] Trying to access CameraState on CameraFX of mode {FXMode}. This is only valid for CameraFX of mode CameraState.");
                return m_cameraController.CameraState;
            }
        }

        protected virtual void OnStartFX() {}
        public virtual void OnUpdateFX() {}
        protected virtual void OnStopFX() {}

        public void StartFX(CameraBehaviorController cameraController)
        {
            // Do we restart or just skip calling Start? Assming this instance is the same one
            if (IsActive && m_cameraController == cameraController) StopFX();
            
            if (IsActive && m_cameraController != cameraController && m_cameraController != null)
                throw new InvalidOperationException($"[CameraSystem] Trying to start a CameraFX that is already active on a different CameraBehaviorController {m_cameraController.gameObject.name}. Stop it first.");

            IsActive = true;
            m_cameraController = cameraController;
            TimeSinceStarted = 0;
            OnStartFX();
        }
        
        public void StopFX()
        {
            if (!IsActive) return;
            
            OnStopFX();
            m_cameraController = null;
            IsActive = false;
        }
    }

    /// <summary>
    /// Camera FX that implements ICameraRenderFX, recieving callbacks from the camera FX renderer
    /// Important to call base.OnStart and base.OnStop with this as those automatically handle registration
    /// and unregistration from the renderer
    /// <code>
    /// public class CameraFXRenderEffect : CameraRenderingFX
    /// {
    ///     protected override void OnStartFX()
    ///     {
    ///         base.OnStartFX();
    ///         Tween.MaterialProperty(m_fxMat, Shader.PropertyToID(_Alpha), 0f, 1f, 1f, Ease.InOutQuad, 2,
    ///             CycleMode.Yoyo).OnComplete(StopFX);
    ///     }
    ///     
    ///     private Material m_fxMat;
    ///     public override void OnCameraRender(CommandBuffer cmd, TextureHandle source, TextureHandle dest)
    ///     {
    ///         Blitter.BlitCameraTexture(cmd, source, dest, m_fxMat, 0);
    ///     }
    /// }
    /// </code>
    /// </summary>
    public abstract class CameraRenderingFX : CameraFX, ICameraRenderPass, IDisposable
    {
        public sealed override Mode FXMode => Mode.Render;
        public bool ShouldRenderFX => IsActive;

        protected override void OnStartFX()
        {
            m_camera.AddCameraCommandBuffer(RenderPassEvent.AfterRenderingTransparents, this);
        }

        protected override void OnStopFX()
        {
            m_camera.RemoveCameraCommandBuffer(this);
        }

        public abstract void OnCameraRender(CommandBuffer cmd, TextureHandle source, TextureHandle dest);
        
        public virtual void Dispose() {}
    }
}