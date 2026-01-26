using System;
using System.Collections.Generic;
using FS.Player;
using PrimeTween;
using TimeUtils;
using UnityEngine;
using UnityEditor;

namespace FS.CameraSystem
{
    /// <summary>
    /// Player system that has final say to camera positioning and rotation, responsible for:
    /// - Applying local camera shakes and <see cref="WorldCameraShake"/> to the camera
    /// - Handling blending between player cameras and <see cref="VirtualCamera"/>
    /// - Applying the final camera state to the player camera gameobject
    /// </summary>
    public class PlayerCameraSystem : PlayerSystem
    {
        private Camera m_camera;
        public Camera Camera => m_camera;
        
        private CameraState m_playerCameraState;
        private CameraState m_cameraState;
        
        public override void Initialize()
        {
            m_camera = m_playerData.Instance.GetComponentInChildren<Camera>();
            m_cameraState = new CameraState(m_camera);
            m_playerCameraState = m_playerData.Instance.GetComponentInChildren<CameraBehaviorController>().CameraState;
            m_blendFromCamera = null;
            m_blendToCamera = null;

            SplitScreenManager.Instance.OnCameraRectChanged += OnRectChanged;
        }

        public override void Shutdown()
        {
            if (SplitScreenManager.Instance != null) 
                SplitScreenManager.Instance.OnCameraRectChanged -= OnRectChanged;
        }

        private void LateUpdate()
        {
            // Update Camera Blends
            UpdateCameraSwitchBlends();
            
            // Apply active shakes
            ApplyCameraShakes();
            
            // Apply active impulses
            ApplyCameraImpulses();
            
            // Apply camera controller state
            m_cameraState.ApplyToCamera(m_camera);
        }
        
        #region Rect Changes

        private void OnRectChanged() => m_camera.rect = SplitScreenManager.Instance.GetSplitScreenRect(m_playerData.ID);

        #endregion
        
        #region Camera Switching
        /// <summary>
        /// Target virtual camera we're blending towards/sticking with, null if we're blending out or no blending is occuring
        /// </summary>
        public VirtualCamera VirtualCameraBlendTarget { get; private set; }
        /// <summary>
        /// Cached parameters describing how we should blend to the virtual camera target (or back), such as easing curve,
        /// duration, etc...
        /// </summary>
        public CameraBlendParams CameraSwitchBlendParams { get; private set; }
        public TimeSince CameraSwitchBlendProgress { get; private set; }

        private List<VirtualCamera> m_virtualCameraHistory = new();

        /// <summary>
        /// Event invoked right when we start blending back to the player camera. Useful for resetting the player camera
        /// rotation for example to ensure that when we blend back, it's facing forward (not facing where it last faced)
        /// TODO: Might be good to bake the "reset rotation" into it here
        /// </summary>
        public Action OnBeginBlendToPlayerCamera;
        
        private CameraState m_blendFromCamera;
        private CameraState m_blendToCamera;
        
        public bool IsBlending => m_blendFromCamera != null && m_blendToCamera != null;
        
        public void BlendToCamera(VirtualCamera virtualCamera, CameraBlendParams? blendParams = null)
        {
            if (virtualCamera == null)
            {
                Debug.LogError("Virtual camera is null");
                return;
            }
            if (virtualCamera == VirtualCameraBlendTarget) return;
            
            blendParams ??= CameraBlendParams.None;
            
            m_blendFromCamera = new CameraState(m_cameraState);
            m_blendToCamera = virtualCamera.m_cameraState;
            
            if (VirtualCameraBlendTarget != null) VirtualCameraBlendTarget.DeactivateCamera();
            
            VirtualCameraBlendTarget = virtualCamera;
            CameraSwitchBlendParams = blendParams.Value;
            CameraSwitchBlendProgress = 0f;
            
            // Add it to the history at the top (remove first if already present to re-prioritize)
            m_virtualCameraHistory.Remove(virtualCamera);
            m_virtualCameraHistory.Add(virtualCamera);

            // Invoke TODO: Improve this assignment
            VirtualCameraBlendTarget.ActivateCamera(m_playerData.Instance.GetComponentInChildren<PhysicsController>().gameObject);
        }

        /// <summary>
        /// Blends out of the specified virtual camera if its active or just removes it from the history.
        /// Will attempt to blend to the last added virtual camera or back to the player
        /// </summary>
        public void PopVirtualCamera(VirtualCamera virtualCamera, CameraBlendParams? blendParams = null)
        {
            // Is it in the history?
            if (!m_virtualCameraHistory.Contains(virtualCamera)) return; // Can't do much here
            
            // Is it the active virtual camera? If not we don't gotta worry about blending out just remove it from the list
            m_virtualCameraHistory.Remove(virtualCamera);
            if (VirtualCameraBlendTarget != virtualCamera) return;
            
            // Get the next virtual camera we'll be blending towards
            var nextVCam = m_virtualCameraHistory.Count > 0 ? m_virtualCameraHistory[^1] : null;
            if (nextVCam == null) BlendBackToPlayer(blendParams);
            else BlendToCamera(nextVCam, blendParams); // Deactivate is invoked here
        }

        public void BlendBackToPlayer(CameraBlendParams? blendParams = null)
        {
            // The player camera state is already our target
            if (VirtualCameraBlendTarget == null) return;
            
            blendParams ??= CameraBlendParams.None;

            m_blendFromCamera = new CameraState(m_cameraState);
            m_blendToCamera = m_playerCameraState;
            
            VirtualCameraBlendTarget = null;
            CameraSwitchBlendParams = blendParams.Value;
            CameraSwitchBlendProgress = 0f;
            
            // Clear history, we assume this function is used in the context of fully resetting the camera back to the player now
            m_virtualCameraHistory.Clear();
            
            OnBeginBlendToPlayerCamera?.Invoke();
        }
        
        private void UpdateCameraSwitchBlends()
        {
            if (!IsBlending)
            {
                // TODO: Supports dynamic cameras but not if we have multiple players whose virtual cameras are meant to be positioned differently
                m_cameraState.CopyFrom(VirtualCameraBlendTarget != null
                    ? VirtualCameraBlendTarget.m_cameraState
                    : m_playerCameraState);
                return;
            }
            
            float alpha = CameraSwitchBlendParams.Evaluate(CameraSwitchBlendProgress);
            CameraState.Blend(m_blendFromCamera, m_blendToCamera, alpha, m_cameraState);

            if (alpha >= 1f)
            {
                m_blendFromCamera = null;
                m_blendToCamera = null;
            }
        }
        #endregion

        #region Camera Shakes

        private List<CameraShake> m_activeShakes = new();
        public IReadOnlyList<CameraShake> ActiveShakes => m_activeShakes;
        
        public void AddCameraShake(CameraShake shake)
        {
            if (shake == null) return;

            // Hashset, so wont add duplicates. Instead we'll be restarting the shake
            m_activeShakes.Add(shake);
            shake.StartShake();
        }
        
        private void ApplyCameraShakes()
        {
            // Apply local space shakes
            for (int s = m_activeShakes.Count - 1; s >= 0; s--)
            {
                var shake = m_activeShakes[s];
                if (shake == null || !shake.IsActive)
                {
                    m_activeShakes.RemoveAt(s);
                    continue;
                }
                
                // Apply the shake to the camera state
                shake.ApplyShake(m_cameraState);
            }
            
            // Apply world-space shakes
            WorldCameraShakeManager.Instance.ApplyWorldShakes(m_cameraState);
        }

        #endregion
        
        #region Camera Impulse
        
        private List<CameraImpulse> m_activeImpulses = new();

        public IReadOnlyList<CameraImpulse> ActiveImpulses => m_activeImpulses;

        public void AddCameraImpulse(CameraImpulse impulse)
        {
            if (impulse == null) return;
            
            m_activeImpulses.Add(impulse);
            impulse.StartImpulse();
        }

        private void ApplyCameraImpulses()
        {
            for (int i = m_activeImpulses.Count - 1; i >= 0; i--)
            {
                var impulse = m_activeImpulses[i];
                if (impulse == null || !impulse.IsActive)
                {
                    m_activeImpulses.RemoveAt(i);
                    continue;
                }

                impulse.ApplyImpulse(m_cameraState);
            }
        }
        
        #endregion
        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            
            Matrix4x4 gizmoMatrix = Gizmos.matrix;
            var ogColor = Gizmos.color;
            Gizmos.matrix = Matrix4x4.TRS(m_playerCameraState.m_position, m_playerCameraState.m_rotation, Vector3.one);
            Gizmos.color = new Color(0.5f, 0f, 1f);
            Gizmos.DrawFrustum(Vector3.zero, m_playerCameraState.m_fov, m_playerCameraState.m_nearClipPlane, m_playerCameraState.m_farClipPlane, m_camera.aspect);
            Gizmos.matrix = gizmoMatrix;
            
            Handles.Label(m_playerCameraState.m_position, "Player Camera");

            if (IsBlending)
            {
                Gizmos.color = Color.white;
                Handles.DrawAAPolyLine(m_blendFromCamera.m_position, m_blendToCamera.m_position);
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(m_blendToCamera.m_position, 0.5f);
                Handles.Label(m_blendToCamera.m_position, "Blending To");
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(m_blendFromCamera.m_position, 0.5f);
                Handles.Label(m_blendFromCamera.m_position, "Blending From");
            }
            
            Gizmos.color = ogColor;
        }
#endif        
    }
}