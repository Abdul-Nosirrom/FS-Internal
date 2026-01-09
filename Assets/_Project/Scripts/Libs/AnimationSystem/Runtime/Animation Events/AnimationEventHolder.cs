using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

#if UNITY_EDITOR
using FS.Animation.Editor;
#endif

namespace FS.Animation
{
    public interface IAnimationEvent
    {
        string Name { get; }
        public bool IsRangedEvent => false;
        public bool NeedsAnimationEndCallback => false;
        public void OnAnimationFadeOut(GameObject context) {}
        
        public void Start(GameObject context) {}
        public void Execute(GameObject context, float normalizedTime) {}
        public void End(GameObject context) {}
        
#if UNITY_EDITOR
        public void Start_Editor(GameObject context, AnimationPreviewRender previewRender) {}
        public void Execute_Editor(GameObject context, float normalizedTime, AnimationPreviewRender previewRender) {}
        public void End_Editor(GameObject context, AnimationPreviewRender previewRender) {}
#endif
    }
    
    [Serializable]
    public class AnimationEventHolder
    {
        public List<FSAnimationEvent> Events = new();

        public void Bind(AnimancerComponent animator, AnimancerState state, AnimancerEvent.Sequence stateEvents)
        {
            foreach (var e in Events)
            {
                if (e == null) continue;
                e.Bind(animator, state, stateEvents);
            }
        }
    }
}