using System;
using System.Collections.Generic;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR 
using FS.Animation.Editor;
#endif

namespace FS.Animation
{
    [AttributeUsage(AttributeTargets.Class)]
    public class EventPathAttribute : Attribute
    {
        public Texture2D Icon { get; private set; }
        public string Path { get; private set; }
        
        public EventPathAttribute(string path)
        {
            Icon = null;    
            Path = path;
        }

        public EventPathAttribute(string path, Texture2D icon)
        {
            Icon = icon;
            Path = path;
        }
    }

    [Serializable]
    public sealed class FSAnimationEvent// : IAnimationEvent
    {
        [BoxGroup("Time", false)]
        [ShowIf("IsInstantEvent")]
        [HideLabel, Range(0, 1)] 
        public float Time;

        [BoxGroup("Time")]
        [ShowIf("IsRangedEvent")]
        [HideLabel, MinMaxSlider(0, 1)] 
        public Vector2 TimeRange;

        [BoxGroup("Event", false)] 
        [SerializeReference]
        public IAnimationEvent Event;

        public float TriggerTime { get => Time; set => Time = value; }

        public Vector2 TriggerRange { get => TimeRange; set => TimeRange = value; }

        public bool IsRangedEvent => Event?.IsRangedEvent ?? false;
        public bool IsInstantEvent => !Event?.IsRangedEvent ?? true;

        /// <summary>
        /// Track if the end event is triggered to avoid multiple calls, because we want to still call it on fade out
        /// only if it hasn't been called yet
        /// </summary>
        private class RangedEventState
        {
            public bool m_startEventTriggered; 
            public bool m_endEventTriggered;
            public void StartTriggered() { m_startEventTriggered = true; m_endEventTriggered = false; }
            public void EndTriggered() { m_startEventTriggered = false; m_endEventTriggered = true; }
        }
        
        public void Bind(AnimancerComponent owner, AnimancerState state, AnimancerEvent.Sequence Events)
        {
            if (IsRangedEvent)
            {
                TriggerRange = TimeRange;

                // NOTE: If we're looping and our end event is almost one, should we set it to be the exit event instead?
                float startTime = TimeRange.x;
                float endTime = TimeRange.y;
                
                // Capture this closure as it'll persist along with the state
                var rangedState = new RangedEventState();

                BindEvent(state, startTime, Events, () =>
                {
                    if (owner && !rangedState.m_startEventTriggered) Start(owner.gameObject);
                    rangedState.StartTriggered();
                });
                BindEvent(state, endTime, Events, () =>
                {
                    if (owner && !rangedState.m_endEventTriggered) End(owner.gameObject);
                    rangedState.EndTriggered();
                });
                
                // For ranged events, bind the end to the begin fade out as well to ensure cleanup
                if (endTime < 1f)
                    BindEvent(state, 1f, Events, () => 
                    {
                        if (owner && !rangedState.m_endEventTriggered) End(owner.gameObject);
                        rangedState.EndTriggered();
                    });
            }
            else
            {
                BindEvent(state, Time, Events, () =>
                {
                    if (owner)
                    {
                        float normalizedTime = 1f;
                        if (IsRangedEvent)
                        {
                            float evtDuration = TimeRange.y - TimeRange.x;
                            normalizedTime = (ActiveState.NormalizedTime%1f - TimeRange.x) / evtDuration;
                        }
                        Execute(owner.gameObject, normalizedTime);
                    }
                });
            }

            // Some events want a callback for when animation fade out starts (same as the above shit but this could be for specific behavior)
            if (Event?.NeedsAnimationEndCallback ?? false)
            {
                BindEvent(state, 1f, Events, () => Event.OnAnimationFadeOut(owner.gameObject));
            }
        }

        private void BindEvent(AnimancerState state, float triggerTime, AnimancerEvent.Sequence Events, Action callback)
        {
            if (triggerTime <= 0f) // Bind to custom start event (once fully faded in)
            {
                state.BindPlaybackEvent(callback, AnimationPlaybackEventManager.Type.BeginFadeIn);
            }
            else if (triggerTime >= 1f) // Bind to custom end event (once fully faded out)
            {
                state.BindPlaybackEvent(callback, AnimationPlaybackEventManager.Type.BeginFadeOut);
            }
            else // Regular binding
            {
                Events.Add(triggerTime, callback);
            }
        }

        private float k_triggerWeightThreshold = 0.3f; // Minimum state weight to trigger event
        private AnimancerState ActiveState => AnimancerEvent.Current.State;
        
        public void Start(GameObject context)
        {
#if UNITY_EDITOR
            if (AdvancedPreviewRenderUtility.TryGetActivePreviewForObject<AnimationPreviewRender>(context, out var previewRender))
                Event?.Start_Editor(context, previewRender);
            else
#endif
            {
                Event?.Start(context);
            }
        }

        public void Execute(GameObject context, float normalizedTime)
        {
            // Are we blending out and have a ranged event?
            if (ActiveState != null) // NOTE: THe way we execute events on mecanim via statemachinebehaviors means this is invalid in that case
            {
                if (ActiveState.FadeGroup is { TargetWeight: 0 } && IsRangedEvent)
                {
                    if (ActiveState.Weight < k_triggerWeightThreshold)
                    {
                        End(context);
                        return;
                    }
                }

                if (ActiveState.Weight < k_triggerWeightThreshold) return;
            }
            
#if UNITY_EDITOR
            if (AdvancedPreviewRenderUtility.TryGetActivePreviewForObject<AnimationPreviewRender>(context, out var previewRender))
                Event?.Execute_Editor(context, normalizedTime, previewRender);
            else
#endif      
            Event?.Execute(context, normalizedTime);
        }

        public void End(GameObject context)
        {
#if UNITY_EDITOR
            if (AdvancedPreviewRenderUtility.TryGetActivePreviewForObject<AnimationPreviewRender>(context, out var previewRender))
                Event?.End_Editor(context, previewRender);
            else
#endif            
                Event?.End(context);
        }
    }
}