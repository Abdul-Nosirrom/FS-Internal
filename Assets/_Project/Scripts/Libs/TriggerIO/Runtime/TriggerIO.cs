using System;
using System.Collections.Generic;
using Drawing;
using FS.Attributes;
using PrimeTween;
using UltEvents;
using UnityEngine;
using UnityEngine.Events;

namespace FS.Interaction
{
    [Experimental]
    [AddComponentMenu("Free Skies/Interaction/Trigger IO")]
    public class TriggerIO : MonoBehaviourGizmos
    {
        public enum TriggerType
        {
            OnStart,
            OnDestroy,
            OnEnable,
            OnDisable,
            OnTriggerEnter,
            OnTriggerExit,
            OnTriggerStay,
            OnCollisionEnter,
            OnCollisionStay,
            OnCollisionExit
        }
        
        [Serializable]
        public class TriggerAction
        {
            public TriggerIO m_owner;
            
            public TriggerType m_trigger = TriggerType.OnStart;
            public UltEvent m_event = new();
            
            public float m_delay = 0f;
            public bool m_once = false;
            
            private bool m_hasInvoked = false;

            public void Invoke()
            {
                if (m_hasInvoked && m_once) return;
                
                if (m_delay > 0)
                    Tween.Delay(m_delay, () => m_event?.Invoke());
                else
                    m_event?.Invoke();
                
                m_hasInvoked = true;
            }
        }

        public List<TriggerAction> m_actions = new();
        
        public void InvokeTrigger(TriggerType triggerType)
        {
            foreach (var action in m_actions)
            {
                if (action.m_trigger == triggerType)
                    action.Invoke();
            }
        }

        public void AddEventAction(TriggerAction action)
        {
            if (!action.m_event.HasCalls)
            {
                var call = new PersistentCall();
                call.SetMethod(null, this);
                action.m_event.PersistentCallsList.Add(call);
            }

            action.m_owner = this;
            m_actions.Add(action);
        }
        
        public void RemoveEventAction(TriggerAction action) => m_actions.Remove(action);

        private void Start() => InvokeTrigger(TriggerType.OnStart);
        private void OnDestroy() => InvokeTrigger(TriggerType.OnDestroy);
        private void OnEnable() => InvokeTrigger(TriggerType.OnEnable);
        private void OnDisable() => InvokeTrigger(TriggerType.OnDisable);
        private void OnTriggerEnter(Collider other) => InvokeTrigger(TriggerType.OnTriggerEnter);
        private void OnTriggerExit(Collider other) => InvokeTrigger(TriggerType.OnTriggerExit);
        private void OnTriggerStay(Collider other) => InvokeTrigger(TriggerType.OnTriggerStay);
        private void OnCollisionEnter(Collision collision) => InvokeTrigger(TriggerType.OnCollisionEnter);
        private void OnCollisionStay(Collision collision) => InvokeTrigger(TriggerType.OnCollisionStay);
        private void OnCollisionExit(Collision collision) => InvokeTrigger(TriggerType.OnCollisionExit);
        
#if UNITY_EDITOR
        public override void DrawGizmos()
        {
            var allConnections = new HashSet<GameObject>();
            foreach (var action in m_actions)
            {
                if (action.m_event == null) continue;
                
                foreach (var call in action.m_event.PersistentCallsList)
                {
                    if (call == null || call.Target == null) continue;
                    var targetGO = call.Target as GameObject;
                    if (targetGO == null) targetGO = (call.Target as Component)?.gameObject;
                    if (targetGO != null)
                    {
                        allConnections.Add(targetGO);
                    }
                }
            }

            foreach (var connection in allConnections)
            {
                Draw.DashedLine(transform.position, connection.transform.position, 0.25f, 0.1f);
            }
        }
#endif        
    }
}