using UnityEngine;

namespace FS.CameraSystem
{
    /// <summary>
    /// This is essentially 'local cameras' that are used to store reference states to a camera.
    /// For the players camera controller, they modify this - not the camera directly. This is because we need to
    /// be able to blend between multiple cameras (moving the camera gameobject), but still applying behaviors
    /// of the player camera. We don't want interference between those two, so we'll be doing camera blends between
    /// 2 CameraState objects and applying it to the camera game object.
    /// </summary>
    public class CameraState
    {
        public Vector3 m_position;
        public Quaternion m_rotation;
        public float m_fov;
        public float m_nearClipPlane;
        public float m_farClipPlane;

        public CameraState()
        {
            m_position = Vector3.zero;
            m_rotation = Quaternion.identity;
            m_fov = 70f;
            m_nearClipPlane = 0.3f;
            m_farClipPlane = 1000f;
        }
        
        public CameraState(Camera camera)
        {
            m_position = camera.transform.position;
            m_rotation = camera.transform.rotation;
            m_fov = camera.fieldOfView;
            m_nearClipPlane = camera.nearClipPlane;
            m_farClipPlane = camera.farClipPlane;
        }

        public CameraState(CameraState cameraState)
        {
            m_position = cameraState.m_position;
            m_rotation = cameraState.m_rotation;
            m_fov = cameraState.m_fov;
            m_nearClipPlane = cameraState.m_nearClipPlane;
            m_farClipPlane = cameraState.m_farClipPlane;
        }

        /// <summary>
        /// Converts an orbit vector to a world position/rotation and applies it to this camera state.
        /// </summary>
        public void ApplyOrbitVector(CameraOrbitVector orbitVector, Quaternion? basis = null)
        {
            basis ??= Quaternion.identity;
            m_position = orbitVector.ToPosition(basis);
            m_rotation = orbitVector.ToRotation(basis);
            m_fov = orbitVector.FOV;
        }

        /// <summary>
        /// Applies the camera state data to the given camera (position/rotation/fov/clip planes)
        /// </summary>
        public void ApplyToCamera(Camera camera)
        {
            camera.transform.position = m_position;
            camera.transform.rotation = m_rotation;
            camera.fieldOfView = m_fov;
            camera.nearClipPlane = m_nearClipPlane;
            camera.farClipPlane = m_farClipPlane;
        }

        /// <summary>
        /// Copies another camera state into this camera state
        /// </summary>
        public void CopyFrom(CameraState other)
        {
            m_position = other.m_position;
            m_rotation = other.m_rotation;
            m_fov = other.m_fov;
            m_nearClipPlane = other.m_nearClipPlane;
            m_farClipPlane = other.m_farClipPlane;
        }

        /// <summary>
        /// Peforms a blend from the given alpha value between States (a) and (b) and writes the result into 'result'
        /// </summary>
        public static void Blend(CameraState a, CameraState b, float t, CameraState result)
        {
            result.m_position = Vector3.Lerp(a.m_position, b.m_position, t);
            result.m_rotation = Quaternion.Slerp(a.m_rotation, b.m_rotation, t);
            result.m_fov = Mathf.Lerp(a.m_fov, b.m_fov, t);
            result.m_nearClipPlane = Mathf.Lerp(a.m_nearClipPlane, b.m_nearClipPlane, t);
            result.m_farClipPlane = Mathf.Lerp(a.m_farClipPlane, b.m_farClipPlane, t);
        }
    }
}