#if UNITY_EDITOR

using FS.Rendering;
using UnityEngine;

namespace FS.Animation.Editor
{
    [ExecuteInEditMode]
    public class EditorPreviewSimulator : MonoBehaviour
    {
        // Objects that need to be manually simulated/have no monobehavior callbacks
        private int m_frameCount = 0;
        private ParticleSystem[] m_particleSystems;
        private VFXController m_vfxController;
        
        private void Awake()
        {
            m_frameCount = 0;
            m_particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            m_vfxController = GetComponentInChildren<VFXController>(true);
            m_vfxController.InitPreviewData();
            
            Invoke("Awake");
        }

        private void Start() => Invoke("Start");
        private void OnEnable() => Invoke("OnEnable");
        private void OnDisable() => Invoke("OnDisable");
        private void LateUpdate() => Invoke("LateUpdate");
        private void FixedUpdate() => Invoke("FixedUpdate");

        private void Update()
        {
            if (m_isInvoking) return;
            
            if (PlaybackTime < InvocationTime) // We scrubbed backwards
            {
                DestroyImmediate(gameObject);
                return;
            }
            
            SimulatePreviewables();
            Invoke("Update");
            
            m_frameCount++;
        }

        public float InvocationTime = 0;
        public float PlaybackTime = 0;
        public float PrevPlaybackTime = 0;
        
        private void SimulatePreviewables()
        {
            // Simulate particles
            float curTime = PlaybackTime - InvocationTime; // TODO: If speed is negative, this should be reversed
            
            float deltaTime = curTime - PrevPlaybackTime;

            if (m_vfxController) m_vfxController.Simulate(curTime, deltaTime);
            else
            {
                foreach (var ps in m_particleSystems)
                {
                    if (m_frameCount == 0)
                    {
                        ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.randomSeed =
                            ps.randomSeed; // This is important! If the setter is not called it'll randomly get a seed every frame it seems from editor playback
                        ps.Play(false);
                    }

                    ps.Simulate(curTime, false, true);
                    ps.time = curTime;
                }
            }
            
            PrevPlaybackTime = curTime;
        }

        bool m_isInvoking = false;
        private void Invoke(string eventName)
        {
            // Prevent getting stuck in an infinite loop as this monobehaviors events are also gonna be recieved by BroadcastMessage
            if (m_isInvoking) return;
            
            m_isInvoking = true;
            //gameObject.BroadcastMessage(eventName, SendMessageOptions.DontRequireReceiver); TODO: Is this really necessary? Did i need it for VFX Graph before?
            m_isInvoking = false;
        }
    }
}

#endif