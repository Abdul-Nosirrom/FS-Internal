using System;
using System.Collections.Generic;
using System.Linq;
using FS.GameService;
using FS.GameService.Attributes;
using UnityEngine;

namespace FS.CameraSystem
{
    /// <summary>
    /// Service responsible for pushing camera shakes with a world position
    /// </summary>
    [AutoRegisteredService]
    public class WorldCameraShakeManager : IGameService
    {
        public static WorldCameraShakeManager Instance => GameServiceLocator.Get<WorldCameraShakeManager>();
        
        private HashSet<WorldCameraShake> m_worldCameraShakes = new();
        public IReadOnlyCollection<WorldCameraShake> WorldCameraShakes => m_worldCameraShakes;
        
        public void Initialize() {}

        public void Shutdown() {}
        
        public void AddCameraShake(WorldCameraShake cameraShake)
        {
            if (cameraShake == null) throw new ArgumentNullException(nameof(cameraShake));
            m_worldCameraShakes.Add(cameraShake);
        }
        
        public void RemoveCameraShake(WorldCameraShake cameraShake)
        {
            if (cameraShake == null) throw new ArgumentNullException(nameof(cameraShake));
            m_worldCameraShakes.Remove(cameraShake);
        }

        public void ApplyWorldShakes(CameraState cameraState)
        {
            foreach (var cameraShake in m_worldCameraShakes)
            {
                cameraShake.ApplyShake(cameraState);
            }
        }
    }
    
    // TODO: This is a monobehavior, but maybe it's best to not be that? Why not just upload structs or regular C# objects
    // to the WorldCameraShakeManager?
    public class WorldCameraShake : MonoBehaviour
    {
        [SerializeField] private CameraShakeStrength m_strength;
        [SerializeField] private CameraShakeDuration m_duration;
        [SerializeField] private CameraShakeStrength m_frequency;

        [SerializeField, Range(0.1f, 25)] private float m_effectRadius;
        
        
        private CameraShake m_cameraShake;
        
        private void OnEnable() => WorldCameraShakeManager.Instance?.AddCameraShake(this);
        private void OnDisable() => WorldCameraShakeManager.Instance?.RemoveCameraShake(this);

        private void Awake()
        {
            m_cameraShake = CameraShake.CreateShake(m_strength, m_duration, m_frequency);
            m_cameraShake.StartShake();
        }

        public void ApplyShake(CameraState cameraState)
        {
            if (!m_cameraShake.IsActive) return;
            if (cameraState == null) throw new ArgumentNullException(nameof(cameraState));
            
            m_cameraShake.ApplyShake(cameraState, GetShakeAttenuation(cameraState.m_position));
        }

        public float GetShakeAttenuation(Vector3 position)
        {
            float dist = Vector3.Distance(position, transform.position);
            if (dist > m_effectRadius) return 0f;

            float normDist = Mathf.Clamp01(dist / m_effectRadius);
            float strength = Mathf.Pow(1 - normDist, 2);
            return strength;
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var ogColor = Gizmos.color;
            Gizmos.color = new Color(1, 0, 0, 0.2f);
            Gizmos.DrawSphere(transform.position, m_effectRadius);
            Gizmos.color = ogColor;
        }
#endif         
    }
}