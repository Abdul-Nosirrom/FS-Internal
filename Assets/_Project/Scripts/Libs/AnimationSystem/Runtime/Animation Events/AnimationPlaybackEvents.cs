using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.Profiling;

namespace FS.Animation
{
    public static class AnimancerStateExtensions
    {
        public static void BindPlaybackEvent(this AnimancerState state, Action callback, AnimationPlaybackEventManager.Type type)
        {
            AnimationPlaybackEventManager.RegisterEvent(state, callback, type);
        }
    }

    /// <summary>
    /// Way to register callbacks for animation playback events such as begin/end fade in/out.
    /// Respects layer weights for override layers
    /// </summary>
    public class AnimationPlaybackEventManager : Updatable, IDisposable
    {
        public enum Type
        {
            /// <summary>
            /// Invoke when the animation starts playing/fading in. Condition is just target weight becomes > 0
            /// </summary>
            BeginFadeIn, 
            /// <summary>
            /// Invoke when the animation is fully faded in. Condition is target weight > 0 and weight >= 1
            /// </summary>
            EndFadeIn, 
            /// <summary>
            /// Invoke when the animation starts fading out. Condition is target weight becomes 0. So triggers even when it stops abruptly
            /// </summary>
            BeginFadeOut, 
            /// <summary>
            /// Invoke when the animation is fully faded out. Condition is target weight == 0 and weight <= 0
            /// </summary>
            EndFadeOut
        }

        private class EventEntry
        {
            public readonly AnimancerState State;
            public readonly Action Callback;
            public readonly Type EventType;
            public bool IsActive;

            public float TargetWeight => m_parentLayerWeightFactor * State.TargetWeight * State.Layer.TargetWeight;
            public float Weight => m_parentLayerWeightFactor * State.Weight * State.Layer.Weight;
            public float EffectiveWeight => m_parentLayerWeightFactor * State.EffectiveWeight * State.Layer.EffectiveWeight;

            private float m_parentLayerWeightFactor;
            private void UpdateParentLayerWeight()
            {
                m_parentLayerWeightFactor = 1f;
                int nextLayerIndex = State.LayerIndex + 1; // Start checking from the layer above the state's layer
                while (nextLayerIndex < State.Graph.Layers.Count) // Basically accumulate higher override layers
                {
                    var layer = State.Graph.Layers[nextLayerIndex];
                    if (layer.IsAdditive) continue;
                    if (State.Graph.Layers[nextLayerIndex].Weight > 0f)
                    {
                        m_parentLayerWeightFactor = 0f;
                        break;
                    }

                    nextLayerIndex++;
                }
            }

            private const float k_weightEpsilon = 0.01f;
            private float m_previousTargetWeight = 0f;
            
            public EventEntry(AnimancerState state, Action callback, Type type)
            {
                State = state;
                Callback = callback;
                EventType = type;

                UpdateParentLayerWeight();
                
                float targetWeight = TargetWeight;
                m_previousTargetWeight = targetWeight;
        
                // Start active if the state is in a triggerable condition
                IsActive = type switch
                {
                    Type.BeginFadeIn or Type.EndFadeIn => targetWeight > k_weightEpsilon,
                    Type.BeginFadeOut or Type.EndFadeOut => targetWeight <= k_weightEpsilon,
                    _ => true
                };
            }

            public void TryInvoke()
            {
                UpdateParentLayerWeight();
                float currentTargetWeight = TargetWeight;
                float currentWeight = Weight;
        
                if (IsActive)
                {
                    bool shouldInvoke = EventType switch
                    {
                        Type.BeginFadeIn => currentTargetWeight >= k_weightEpsilon,
                        Type.EndFadeIn => currentTargetWeight >= 1f - k_weightEpsilon && currentWeight >= 1f - k_weightEpsilon,
                        Type.BeginFadeOut => currentTargetWeight <= k_weightEpsilon,
                        Type.EndFadeOut => currentTargetWeight <= k_weightEpsilon && currentWeight <= k_weightEpsilon,
                        _ => false
                    };

                    if (shouldInvoke)
                    {
                        Callback?.Invoke();
                        IsActive = false;
                    }
                }
                else // Reactivation
                {
                    bool shouldReactivate = EventType switch
                    {
                        Type.BeginFadeIn or Type.EndFadeIn => 
                            m_previousTargetWeight <= k_weightEpsilon && currentTargetWeight > k_weightEpsilon,
                        Type.BeginFadeOut or Type.EndFadeOut => 
                            m_previousTargetWeight > k_weightEpsilon && currentTargetWeight <= k_weightEpsilon,
                        _ => false
                    };
            
                    if (shouldReactivate)
                        IsActive = true;
                }
        
                m_previousTargetWeight = currentTargetWeight;
            }
        }
        
        private static Dictionary<AnimancerGraph, AnimationPlaybackEventManager> s_eventManagers = new();

        private readonly List<EventEntry> m_eventEntries = new();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize() => s_eventManagers.Clear(); // Clear static data when reloading
        
        private AnimancerGraph m_graph;
        
        public static void RegisterEvent(AnimancerState state, Action callback, Type type)
        {
            if (!s_eventManagers.TryGetValue(state.Graph, out var manager))
            {
                manager = new AnimationPlaybackEventManager();
                manager.m_graph = state.Graph;
                state.Graph.RequirePostUpdate(manager);
                state.Graph.Disposables.Add(manager);
                s_eventManagers[state.Graph] = manager;
            }

            var entry = new EventEntry(state, callback, type);
            manager.m_eventEntries.Add(entry);
        }

        public static void DisposeManager(AnimancerGraph graph)
        {
            if (s_eventManagers.TryGetValue(graph, out var manager))
            {
                manager.m_eventEntries.Clear();
                graph.CancelPostUpdate(manager);
                s_eventManagers.Remove(graph);
            }
        }
        
        public override void Update()
        {
            Profiler.BeginSample("AnimationPlaybackEventManager.Update");
            
            for (int e = m_eventEntries.Count - 1; e >= 0; e--)
            {
                if (!m_eventEntries[e].State.IsValid()) m_eventEntries.RemoveAt(e); // This means the state has been destroyed (it or its graph) so we can safely remove it. Next time it gets played it'll be recrated and rebound
                else m_eventEntries[e].TryInvoke();
            }
            
            // Optional: Remove ourselves if no events remain
            if (m_eventEntries.Count == 0)
            {
                m_graph.CancelPostUpdate(this);
                s_eventManagers.Remove(m_graph);
            }
            
            Profiler.EndSample();
        }

        public void Dispose() => DisposeManager(m_graph);
    }
}