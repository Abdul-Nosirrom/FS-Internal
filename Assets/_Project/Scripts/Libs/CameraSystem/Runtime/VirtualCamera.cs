using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FS.CameraSystem
{
    /// <summary>
    /// Acts essentially as a 'camera' that does no rendering. Just holds a CameraState and a Camera
    /// that MUST be attached to the same gameobject. This disables the camera its attached to OnAwake, it's primary purpose
    /// is to cache that cameras state to use for blending purposes
    /// </summary>
    //[RequireComponent(typeof(Camera))]
    public class VirtualCamera : MonoBehaviour
    {
        /// <summary>
        /// Cached camera for the behavior of this virtual cam, we disable it on awake as we only make use of its
        /// data to position it in editor and modify its params.
        /// </summary>
        [Required, SerializeField, HideInInspector] 
        private Camera m_camera;
        public CameraState m_cameraState;

        private GameObject m_cameraOwner;
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            m_camera ??= GetComponent<Camera>();
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            
            Matrix4x4 gizmoMatrix = Gizmos.matrix;
            Color originalColor = Gizmos.color;
            Gizmos.color = Color.red; // if active green, in active red. We'll keep
            Gizmos.matrix = Matrix4x4.TRS(m_cameraState.m_position, m_cameraState.m_rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, m_cameraState.m_fov, m_cameraState.m_nearClipPlane, m_cameraState.m_farClipPlane, m_camera == null ? 16f/9f : m_camera.aspect);
            Gizmos.matrix = gizmoMatrix;
            Gizmos.color = originalColor;
        }
#endif
        private List<VirtualCameraMode> m_cameraBehaviors = new List<VirtualCameraMode>();

        private void Awake()
        {
            if (m_camera == null)
                m_camera = GetComponent<Camera>();
            if (m_camera)
            {
                m_camera.enabled = false;
                m_cameraState = new CameraState(m_camera);
            }
            else
            {
                m_cameraState = new CameraState();
            }

            DeactivateCamera();

            GetComponentsInChildren(m_cameraBehaviors);

            foreach (var camBeh in m_cameraBehaviors) camBeh.Initialize(this);
        }

        public event Action OnCameraActivated;
        public event Action OnCameraDeactivated;
        
        public GameObject Owner => m_cameraOwner;

        public void ActivateCamera(GameObject owner)
        {
            enabled = true;
            m_cameraOwner = owner; // TODO: For local-multiplayer, this won't work if 2 players enter the same volume
            OnCameraActivated?.Invoke();
        }

        public void DeactivateCamera()
        {
            enabled = false;
            OnCameraDeactivated?.Invoke();
        }

        private void LateUpdate()
        {
            foreach (var cameraStateBehavior in m_cameraBehaviors)
            {
                cameraStateBehavior.UpdateCamera();
            }
        }
    }

    public abstract class VirtualCameraMode : MonoBehaviour
    {
        protected VirtualCamera m_camera;
        protected GameObject m_cameraOwner => m_camera.Owner;
        protected CameraState cameraState => m_camera.m_cameraState;
        
        public void Initialize(VirtualCamera virtualCamera)
        {
            m_camera = virtualCamera;
            m_camera.OnCameraActivated += OnCameraActivated;
            m_camera.OnCameraDeactivated += OnCameraDeactivated;
        }
        
        protected virtual void OnDestroy()
        {
            if (m_camera != null)
            {
                m_camera.OnCameraActivated -= OnCameraActivated;
                m_camera.OnCameraDeactivated -= OnCameraDeactivated;
            }
        }

        protected abstract void OnCameraActivated();
        public abstract void UpdateCamera();
        protected abstract void OnCameraDeactivated();
    }
}