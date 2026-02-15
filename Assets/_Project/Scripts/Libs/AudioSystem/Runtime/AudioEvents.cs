using System;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using FS.Animation;
using Sirenix.OdinInspector;
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

        public bool AttachedAudio = true;
        
        [Range(0, 1)]
        public float m_volume = 1.0f;

        public AudioElement m_audioEvent; // Could prolly add volume & shit here
        
        public void Execute(GameObject context, float normalizedTime)
        {
            AudioHandle audio = null;
            if (AttachedAudio) audio = AudioManager.PlayAudio_Attached(m_audioEvent, context);
            else audio = AudioManager.PlayAudio_3D(m_audioEvent, context.transform.position);

            audio.AudioInstance.setVolume(m_volume); // TODO: Make these funcs on the audio handle so we don't interact with FMOD directly
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
        
        public bool AttachedAudio = true;
        [Range(0, 1)]
        public float m_volume = 1.0f;
        
        public AudioElement m_audioEvent;
        private AudioHandle m_audioInstance;
        
        public void Start(GameObject context)
        {
            m_audioInstance = AudioManager.CreateAudioInstance(m_audioEvent, true);
            if (AttachedAudio) AudioManager.AttachAudioInstance(m_audioInstance, context);
            else m_audioInstance.AudioInstance.set3DAttributes(RuntimeUtils.To3DAttributes(context.transform.position)); // TODO: Similarly make this a func on AudioHandle + allow passing in rigidbody for velocity
            
            m_audioInstance.AudioInstance.setVolume(m_volume);
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