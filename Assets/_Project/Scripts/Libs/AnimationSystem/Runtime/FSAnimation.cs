using Animancer;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
using FS.Animation.Editor;
#endif

namespace FS.Animation
{
    /// <summary>
    /// Base Class for creating custom animation assets that can be played via Animancer. Akin to an Animation Montage.
    /// Example would be creating a "LocomotionAnimation" asset that contains multiple animations for walking, running, etc.
    /// Included is also a custom editor for previewing the animation and its events.
    /// </summary>
    public abstract class FSAnimation : TransitionAssetBase, IAnimation
    {
        public FSAnimationLayer Layer;
        
        /// <summary>
        /// Events for all child FSAnimationClips. ChildIdx = -1 means its expecting the events for the parent mixer.
        /// </summary>
        public abstract AnimationEventHolder GetEventsFor(int childIdx);
        public abstract bool HasValidClips();
        
        public virtual AnimancerState Play(AnimancerComponent animator)
        {
            if (!animator) return null;
            if (!HasValidClips())
            {
                return null;
            }

            var state = animator.Layers[(int)Layer].Play(this);//GetTransition());
            
            if (state == null) return null;

            // NOTE: Can't bind events on Controller States, they get triggered on the whole anim controller - instead we use MecanimAnimationState
            // and manually invoke them via state machine behavior
            if (state is ControllerState) return state;

            if (state.Events(animator, out var parentEvents))
                SetupStateEvents(animator, state, parentEvents,-1);

            for (int c = 0; c < state.ChildCount; c++)
            {
                var child = state.GetChild(c);
                if (child.Events(animator, out var childEvents))
                    SetupStateEvents(animator, state, childEvents, c);
            }
            
            return state;
        }

        public AnimancerState Play(AnimancerComponent animator, FSAnimationLayer layer) => Play(animator);

        public override AnimancerState CreateState()
        {
            var state = base.CreateState();
            state?.SetDebugName(name);
            return state;
        }

        public virtual void SetupStateEvents(AnimancerComponent animator,
            AnimancerState state, AnimancerEvent.Sequence stateEvents, int childIdx)
        {
            var events = GetEventsFor(childIdx);
            events.Bind(animator, state, stateEvents);
        }
        
#if UNITY_EDITOR
        public virtual void EditorPreviewParameterControl(AnimancerComponent animator, Rect previewRect, SerializedObject target, AnimancerState activeState) {}
#endif        
    }
}