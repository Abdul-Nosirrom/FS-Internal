using System;
using System.Collections.Generic;
using Drawing;
using FS.RuntimeDebug;
using UnityEngine;

namespace FS.AI
{
    public enum KnowledgeUpdateMode
    {
        /// <summary>
        /// Updated only when explicitly requested.
        /// </summary>
        OnDemand,
        /// <summary>
        /// Updated every N frames by the AI Knowledge system
        /// </summary>
        Periodic,
        /// <summary>
        /// Updated every frame by the AI Knowledge system
        /// </summary>
        Continuous
    }
    public abstract class AIKnowledgeModule
    {
        public virtual KnowledgeUpdateMode UpdateMode => KnowledgeUpdateMode.Continuous;
        public virtual int UpdateInterval => 1; // In frames, only used if UpdateMode is Periodic
        
        public abstract void UpdateKnowledge(GameObject agent);
        public abstract void ClearKnowledge(GameObject agent);
        
        public virtual void OnAddedToAgent(GameObject agent) {}
        
        public virtual void DebugDraw(GameObject agent) {}
        public virtual void DebugGUI(GameObject agent) {}
    }
    
    public class AIKnowledge : MonoBehaviour, IDebugProvider
    {
        private Dictionary<Type, AIKnowledgeModule> m_knowledgeModules = new();

        private void OnEnable() => AIDirector.Instance.RegisterAIKnowledge(this);
        private void OnDisable() => AIDirector.Instance?.UnregisterAIKnowledge(this);

        public bool Has<T>() where T :AIKnowledgeModule => m_knowledgeModules.ContainsKey(typeof(T));
        public T Get<T>() where T : AIKnowledgeModule => m_knowledgeModules.TryGetValue(typeof(T), out var value) ? (T) value : null;
        public T GetOrCreate<T>() where T : AIKnowledgeModule, new() => m_knowledgeModules.TryGetValue(typeof(T), out var value) ? (T) value : Add<T>();
        public T Add<T>() where T : AIKnowledgeModule, new()
        {
            var type = typeof(T);
            if (m_knowledgeModules.ContainsKey(type))
                throw new Exception($"Knowledge Module of type {type} already exists on {name}");
            
            var module = new T();
            m_knowledgeModules[type] = module;
            module.OnAddedToAgent(gameObject);
            return module;
        }

        public void UpdateKnowledge()
        {
            foreach (var module in m_knowledgeModules.Values)
            {
                switch (module.UpdateMode)
                {
                    case KnowledgeUpdateMode.OnDemand:
                    case KnowledgeUpdateMode.Periodic when Time.frameCount % module.UpdateInterval != 0:
                        continue;
                    default:
                        module.UpdateKnowledge(gameObject);
                        break;
                }
            }
        }

        #region DEBUG
        
        public string DebugName => "AI Knowledge";
        private Dictionary<Type, bool> debug_knowledgeFoldouts = new();
        
        public void OnDebugGUI()
        {
            GUILayout.BeginVertical();
            
            foreach (var module in m_knowledgeModules.Values)
            {
                if (!debug_knowledgeFoldouts.ContainsKey(module.GetType()))
                    debug_knowledgeFoldouts[module.GetType()] = false;
                
                var foldoutState = debug_knowledgeFoldouts[module.GetType()];
                if (DebugGUI.BeginFoldout(module.GetType().ToString(), ref foldoutState, GUI.skin.box))
                {
                    module.DebugGUI(gameObject);
                    DebugGUI.EndFoldout();
                }
                debug_knowledgeFoldouts[module.GetType()] = foldoutState;
            }
            
            GUILayout.EndVertical();
        }

        public void OnDebugDraw()
        {
            foreach (var module in m_knowledgeModules.Values)
            {
                module.DebugDraw(gameObject);
            }
        }
        
        #endregion
    }
}