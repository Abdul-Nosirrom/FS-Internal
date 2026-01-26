
using System;
using FS.Rendering;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// WIP!
namespace FS.TimelineClips
{
    [Serializable]
    public class VFXClip : PlayableAsset, ITimelineClipAsset
    {
        [VFXDropDown] public VFXBase m_vfxPrefab;
        public VFXParams m_vfxParams;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<VFXBehavior>.Create(graph);
            var behavior = playable.GetBehaviour();
            behavior.m_vfxPrefab = m_vfxPrefab;
            behavior.m_vfxParams = m_vfxParams;
            behavior.m_vfxParams.m_parent = owner.transform;
            
            return playable;
        }

        public ClipCaps clipCaps => ClipCaps.None;
        
        public override double duration
        {
            get
            {
                if (m_vfxPrefab == null || m_vfxPrefab.IsLooping)
                {
                    return base.duration;
                }
                else
                {
                    return m_vfxPrefab.PlaybackDuration;
                }
            }
        }

    }

    public class VFXBehavior : PlayableBehaviour
    {
        public VFXBase m_vfxPrefab;
        public VFXParams m_vfxParams;

        private VFXBase m_vfxInstance;

        public override void OnPlayableCreate(Playable playable)
        {
            // Fetch parentage
        }

        /// <summary>
        /// Clip becomes active (scrub into it, playback reaches it)
        /// </summary>
        public override void OnBehaviourPlay(Playable playable, FrameData info) => SpawnInstance();
        
        /// <summary>
        /// Every frame while clip is active
        /// </summary>
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (m_vfxInstance == null) SpawnInstance();

#if UNITY_EDITOR            
            // Editor preview means manually simulating (tho we can later support explicit time-setting if there's any need to for runtime, but rn Simulate is editor only)
            if (!Application.isPlaying)
            {
                var localTime = (float)playable.GetTime();
                m_vfxInstance.Simulate(localTime, info.deltaTime);
            }
#endif            
        }

        /// <summary>
        /// Clip becomes inactive (scrub out, playback exits, graph stops)
        /// </summary>
        public override void OnBehaviourPause(Playable playable, FrameData info) => CleanupInstance();
        private void SpawnInstance()
        {
            if (m_vfxPrefab == null || m_vfxInstance != null) return;

            if (Application.isPlaying)
            {
                m_vfxInstance = VFXManager.Instance.PlayVFX(m_vfxPrefab, m_vfxParams);
            }
            else
            {
                m_vfxInstance = GameObject.Instantiate(m_vfxPrefab);
                m_vfxParams.ConfigureFX(m_vfxInstance);
            }
        }

        private void CleanupInstance()
        {
            if (m_vfxInstance == null) return;
            
            if (Application.isPlaying)
            {
                m_vfxInstance.Stop();
            }
            else
            {
                GameObject.DestroyImmediate(m_vfxInstance.gameObject);
            }
            
            m_vfxInstance = null;
        }
    }

    [TrackClipType(typeof(VFXClip))]
    [TrackColor(0.7f, 0.2f, 0.2f)]
    [TrackBindingType(typeof(Transform))]
    public class VFXTrack : TrackAsset
    {}
}