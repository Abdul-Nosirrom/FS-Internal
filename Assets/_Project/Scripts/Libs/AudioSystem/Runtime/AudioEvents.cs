using System;
using FMOD.Studio;
using FMODUnity;
using FS.Animation;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

#if UNITY_EDITOR
using FS.Animation.Editor;
#endif

namespace FS.Audio
{
    [Serializable]
    [EventPath("Audio/One Shot Simple")]
    public class PlaySFX_OneShot : IAnimationEvent
    {
        public string Name => "SFX One Shot";

        [Range(0, 1)]
        public float m_volume = 1.0f;

        public AudioElement m_audioEvent; // Could prolly add volume & shit here
        
        public void Execute(GameObject context, float normalizedTime)
        {
            AudioManager.PlayAudio_3D(m_audioEvent, context.transform.position);
        }
        
#if UNITY_EDITOR
        public void Execute_Editor(GameObject context, float normalizedTime, AnimationPreviewRender previewRender)
        {
            AudioManager.EditorPlayPreview(m_audioEvent, m_volume);
        }
#endif        
    }

    [Serializable]
    [EventPath("Audio/Looping SFX")]
    public class PlaySFX_Looping : IAnimationEvent
    {
        public string Name => "SFX Looping";
        public bool IsRangedEvent => true;
        
        [Range(0, 1)]
        public float m_volume = 1.0f;
        
        public AudioElement m_audioEvent;
        private AudioHandle m_audioInstance;
        
        public void Start(GameObject context)
        {
            m_audioInstance = AudioManager.CreateAudioInstance(m_audioEvent, true);
        }

        public void End(GameObject context)
        {
            AudioManager.StopAudioInstance(m_audioInstance, STOP_MODE.ALLOWFADEOUT);
        }

#if UNITY_EDITOR
        private EventInstance m_editorAudioInstance;
        public void Start_Editor(GameObject context, AnimationPreviewRender previewRender)
        {
            if (m_editorAudioInstance.isValid())
            {
                m_editorAudioInstance.setVolume(m_volume);
                m_editorAudioInstance.start();
            }
            else 
                m_editorAudioInstance = AudioManager.EditorPlayPreview(m_audioEvent, m_volume);
        }

        public void End_Editor(GameObject context, AnimationPreviewRender previewRender)
        {
            AudioManager.EditorStopPreview(m_editorAudioInstance);
        }
#endif
    }
}