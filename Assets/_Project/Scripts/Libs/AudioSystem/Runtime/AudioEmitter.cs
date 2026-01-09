using System;
using FMODUnity;
using FS.MeshProcessing;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FS.Audio
{
    [AddComponentMenu("Free Skies/Audio/Audio Emitter")]
    public class AudioEmitter : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField]
        private AudioElement m_audioEvent;

        [SerializeField] private bool m_3DAudio = true;
        [SerializeField] private bool m_triggerOnce = false;

        [SerializeField, GameObjectTags, ShowIf("m_showCollisionTag")]
        private string m_collisionTag = null; // Used for collision/trigger events
        
        [SerializeField] private EmitterGameEvent m_playEvent = EmitterGameEvent.ObjectStart;
        [SerializeField] private EmitterGameEvent m_stopEvent = EmitterGameEvent.ObjectDestroy;
        
        private bool m_showCollisionTag => m_playEvent == EmitterGameEvent.CollisionEnter || 
                                           m_playEvent == EmitterGameEvent.CollisionExit ||
                                           m_stopEvent == EmitterGameEvent.CollisionEnter || 
                                           m_stopEvent == EmitterGameEvent.CollisionExit ||
                                           m_playEvent == EmitterGameEvent.TriggerEnter ||
                                           m_playEvent == EmitterGameEvent.TriggerExit ||
                                           m_stopEvent == EmitterGameEvent.TriggerEnter ||
                                           m_stopEvent == EmitterGameEvent.TriggerExit;

        private AudioHandle m_audioInstance;
        
        private bool m_wasTriggered = false;

        private void PlayAudio()
        {
            if (m_wasTriggered && m_triggerOnce) return;

            m_audioInstance = m_3DAudio 
                ? AudioManager.PlayAudio_Attached(m_audioEvent, gameObject) 
                : AudioManager.PlayAudio_2D(m_audioEvent);
            
            m_wasTriggered = true;
        }

        private void StopAudio()
        {
            if (m_audioInstance is { IsPlaying: true }) 
                AudioManager.StopAudioInstance(m_audioInstance);
        }
        
        private void MaybeHandleAudioEvent(EmitterGameEvent gameEvent)
        {
            if (m_playEvent == gameEvent) PlayAudio();
            if (m_stopEvent == gameEvent) StopAudio();
        }
        
        
        private void OnEnable() => MaybeHandleAudioEvent(EmitterGameEvent.ObjectEnable);
        private void OnDisable() => MaybeHandleAudioEvent(EmitterGameEvent.ObjectDisable);
        
        private void Start() => MaybeHandleAudioEvent(EmitterGameEvent.ObjectStart);

        private void OnDestroy()
        {
            MaybeHandleAudioEvent(EmitterGameEvent.ObjectDestroy);
            m_audioInstance = null; // Pool / Manager will handle disposing & cleanup, they own it
        }

        private void OnTriggerEnter(Collider other)
        {
            if (string.IsNullOrEmpty(m_collisionTag) || m_collisionTag.Equals("Untagged") || other.CompareTag(m_collisionTag))
                MaybeHandleAudioEvent(EmitterGameEvent.TriggerEnter);
        }

        private void OnTriggerExit(Collider other)         
        {
            if (string.IsNullOrEmpty(m_collisionTag) || m_collisionTag.Equals("Untagged") || other.CompareTag(m_collisionTag))
                MaybeHandleAudioEvent(EmitterGameEvent.TriggerExit);
        }
        private void OnCollisionEnter(Collision collision)        
        {
            if (string.IsNullOrEmpty(m_collisionTag) || m_collisionTag.Equals("Untagged") || collision.gameObject.CompareTag(m_collisionTag))
                MaybeHandleAudioEvent(EmitterGameEvent.CollisionEnter);
        }
        private void OnCollisionExit(Collision collision) 
        {
            if (string.IsNullOrEmpty(m_collisionTag) || m_collisionTag.Equals("Untagged") || collision.gameObject.CompareTag(m_collisionTag))
                MaybeHandleAudioEvent(EmitterGameEvent.CollisionExit);
        }
        private void OnMouseEnter() => MaybeHandleAudioEvent(EmitterGameEvent.ObjectMouseEnter);
        private void OnMouseExit() => MaybeHandleAudioEvent(EmitterGameEvent.ObjectMouseExit);
        
        private void OnMouseDown() => MaybeHandleAudioEvent(EmitterGameEvent.ObjectMouseDown);
        private void OnMouseUp() => MaybeHandleAudioEvent(EmitterGameEvent.ObjectMouseUp);
        
        public void OnPointerEnter(PointerEventData eventData) => MaybeHandleAudioEvent(EmitterGameEvent.UIMouseEnter);
        public void OnPointerExit(PointerEventData eventData) => MaybeHandleAudioEvent(EmitterGameEvent.UIMouseExit);
        public void OnPointerDown(PointerEventData eventData) => MaybeHandleAudioEvent(EmitterGameEvent.UIMouseDown);
        public void OnPointerUp(PointerEventData eventData) => MaybeHandleAudioEvent(EmitterGameEvent.UIMouseUp);
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position, "AudioSource Gizmo", true, Color.yellow);
        }
#endif
    }
}