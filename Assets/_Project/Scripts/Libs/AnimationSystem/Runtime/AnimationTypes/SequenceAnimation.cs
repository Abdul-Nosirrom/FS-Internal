using System;
using System.Collections.Generic;
using Animancer;
using UnityEditor;
using UnityEngine;

namespace FS.Animation
{
    [CreateAssetMenu(menuName = "FS/Animation/Sequence Animation", fileName = "New Sequence Animation")]
    public class SequenceAnimation : OneShotAnimation
    {
        [SerializeField] private List<FSAnimationClip> Sequence;
        
        [SerializeField, HideInInspector] private TransitionSequence m_transition = new TransitionSequence();

        private void OnValidate()
        {
            m_transition = new();
            m_transition.Transitions = new ITransition[1 + Sequence.Count];
            m_transition.Transitions[0] = Animation;
            
            // TODO: Are these neccesary to copy over? Or does the state just use each transitionns parameters when playing?
            m_transition.Speed = Animation.Speed;
            m_transition.FadeDuration = ((IAnimation)Animation).FadeDuration;
            m_transition.NormalizedStartTime = ((IAnimation)Animation).NormalizedStartTime;

            for (int c = 0; c < Sequence.Count; c++)
            {
                m_transition.Transitions[c + 1] = Sequence[c];
            }
        }

        // TODO: Why do we have to define this for this type?
        public override void GetAnimationClips(List<AnimationClip> clips)
        {
            clips.Clear();
            clips.Add(Animation.m_clip);
            foreach (var seq in Sequence)
            {
                if (seq != null && seq.m_clip != null)
                    clips.Add(seq.m_clip);
            }
        }

        public override ITransition GetTransition() => m_transition;

        public override AnimancerState Play(AnimancerComponent animator, float fadeDuration = -1)
        {
            if (!animator || !HasValidClips()) return null;
            
            fadeDuration = fadeDuration >= 0 ? fadeDuration : FadeDuration;
            SequenceState state = (SequenceState)animator.Layers[(int)Layer].Play(this, fadeDuration);

            for (int s = 0; s < state.ChildCount; s++)
            {
                var child = state.GetChild(s);
                if (child.Events(animator, out var childEvents))
                    SetupStateEvents(animator, state, childEvents, s);
            }
            
            return state;
        }

        public override AnimationEventHolder GetEventsFor(int childIdx)
        {
            if (childIdx == 0) return base.GetEventsFor(childIdx);
            return Sequence[childIdx - 1].AnimationEvents;
        }

        public override bool HasValidClips()
        {
            return base.HasValidClips() && 
                   Sequence.TrueForAll(clip => clip != null && clip.m_clip);
        }
        
#if UNITY_EDITOR
        public override void EditorPreviewParameterControl(AnimancerComponent animator, Rect previewRect, SerializedObject target, AnimancerState activeState)
        {
            var replayRect = new Rect(
                previewRect.x + 10,
                previewRect.yMax * 0.7f,
                previewRect.width * 0.25f,
                previewRect.height * 0.25f);

            if (GUILayout.Button("Replay", GUILayout.Width(replayRect.width)))
            {
                // events already setup for the editor preview so this is fine
                var state = Play(animator);
                state.Time = 0;
                //animator.Play(this);
            }
        }
#endif        
    }
}