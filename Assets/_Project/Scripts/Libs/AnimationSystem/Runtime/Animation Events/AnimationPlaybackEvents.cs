using System;
using System.Collections.Generic;
using Animancer;
using TimeUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

// TODO: Fix broken BeginFadeOut invocation. Unsure if it's just a problem w/ our wallslide and our use of CrossFade
// Or if its a underlying problem. Need to do more testing. Committing this for the msg and attempts, but haven't changed the code
namespace FS.Animation
{
    public static class AnimancerStateExtensions
    {
        public static void BindPlaybackEvent(this AnimancerState state, Action callback, AnimationPlaybackEventManager.Type type)
        {
            AnimationPlaybackEventManager.RegisterEvent(state, callback, type);
        }

        public static void UnbindPlaybackEvent(this AnimancerState state, Action callback,
            AnimationPlaybackEventManager.Type type)
        {
            AnimationPlaybackEventManager.UnregisterEvent(state, callback, type);
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
            //private float m_previousWeight = 0f;
            private TimeSince m_timeSinceBound;
            
            public EventEntry(AnimancerState state, Action callback, Type type)
            {
                State = state;
                Callback = callback;
                EventType = type;

                UpdateParentLayerWeight();
                
                float targetWeight = TargetWeight;
                m_previousTargetWeight = targetWeight;
                //m_previousWeight = 0f;//Weight; First frame is off, for some reason next frame is instantly target weight 0 while this frame is 1, incorrectly it seems
        
                // Start active if the state is in a triggerable condition
                IsActive = type switch
                {
                    Type.BeginFadeIn or Type.EndFadeIn => targetWeight > k_weightEpsilon,
                    Type.BeginFadeOut or Type.EndFadeOut => targetWeight <= k_weightEpsilon,
                    _ => true
                };

                m_timeSinceBound = 0f;


                //if (EventType == Type.BeginFadeOut && State.DebugName.ToString().Contains("wall", StringComparison.InvariantCultureIgnoreCase))
                //{
                //    Debug.Log($"[AnimEventBinder]: {Time.time:F2} Updatable Target Weight: {targetWeight} | Weight: {Weight} | IsActive? {IsActive}");
                //}
            }

            public void TryInvoke()
            {
                UpdateParentLayerWeight();
                float currentTargetWeight = TargetWeight;
                float currentWeight = Weight;

                // Check for bug solving, is the CState in active/not contributing but we're still waiting on an event?
                // if ((EventType == Type.BeginFadeOut || EventType == Type.EndFadeOut) && !State.IsActive)
                // {
                //     if (IsActive)
                //     {
                //         //EditorApplication.isPaused = true;
                //         Debug.Log($"[AnimEvent] {State.DebugName} Waiting on animation event ({EventType.ToString()}), but animation is in-active " + "\n" +
                //                   $"Time Since Bound: {m_timeSinceBound}" + "\n" +
                //                   $"Weight: {State.Weight}" + "\n" +
                //                   $"TargetWeight: {State.TargetWeight}" + "\n" +
                //                   $"EffectiveWeight: {State.EffectiveWeight}" + "\n" +
                //                   $"LayerWeight: {State.Layer.Weight}" + "\n" +
                //                   $"LayerTargetWeight: {State.Layer.TargetWeight}" + "\n" +
                //                   $"EffectiveWeight: {State.Layer.EffectiveWeight}");
                //     }
                // }

                // Target weight isnt super responsive for some reason, at least for fade out need to look into it more
                // So rn, we just use this workaround by checking in on the sign of the weight delta, if negative that indicates we start fading out
                // Obv broken case: What if we don't fade out to weight 0 but go to 0.5? Utilizing target weight how we intended should solve this, but given
                // the problem of me not fully understanding the values it's giving (layer weight 0?) we just use this as in our use-case, that edge case is unlikely.
                //float deltaWeight = currentWeight - m_previousWeight;
        
                if (IsActive)
                {
                    bool shouldInvoke = EventType switch
                    {
                        Type.BeginFadeIn => currentTargetWeight >= k_weightEpsilon,
                        Type.EndFadeIn => currentTargetWeight >= 1f - k_weightEpsilon && currentWeight >= 1f - k_weightEpsilon,
                        Type.BeginFadeOut => /*deltaWeight < 0, // TODO: More robust way? Animancers weights are giving me weird values that don't work reliably primarily for this case so this is a workaround //*/
                            currentTargetWeight <= k_weightEpsilon || !State.IsActive,//currentTargetWeight <= k_weightEpsilon && currentWeight >= k_weightEpsilon,
                        Type.EndFadeOut => /*currentTargetWeight <= k_weightEpsilon &&*/ currentWeight <= k_weightEpsilon || !State.IsActive,
                        _ => false
                    };
                    
                    /*if (EventType == Type.BeginFadeOut && State.DebugName.ToString().Contains("wall", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Debug.Log($"[AnimEventBinder]: {Time.time:F2} IsActive Updatable Target Weight: {currentTargetWeight} | Weight: {currentWeight} | deltaWeight: {deltaWeight} | ShouldInvoke? {shouldInvoke}");
                    }*/

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
                        /*Type.BeginFadeOut => deltaWeight >= 0, // TODO: Similar workaround to above case*/
                        Type.BeginFadeOut or Type.EndFadeOut => 
                            m_previousTargetWeight > k_weightEpsilon && currentTargetWeight <= k_weightEpsilon,
                        _ => false
                    };
            
                    if (shouldReactivate)
                        IsActive = true;
                    
                    /*if (EventType == Type.BeginFadeOut && State.DebugName.ToString().Contains("wall", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Debug.Log($"[AnimEventBinder]: {Time.time:F2} ShouldReactivate Updatable Target Weight: {currentTargetWeight} | Weight: {currentWeight} | deltaWeight: {deltaWeight} | ShouldInvoke? {shouldReactivate}");
                    }*/
                }
        
                m_previousTargetWeight = currentTargetWeight;
                //m_previousWeight = currentWeight;
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

        public static void UnregisterEvent(AnimancerState state, Action callback, Type type)
        {
            if (s_eventManagers.TryGetValue(state.Graph, out var manager))
            {
                manager.m_eventEntries.RemoveAll((entry => entry.State == state && entry.Callback == callback && entry.EventType == type));
            }
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