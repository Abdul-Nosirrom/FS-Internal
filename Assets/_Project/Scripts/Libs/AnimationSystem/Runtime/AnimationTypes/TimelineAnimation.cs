using System;
using Animancer;
using UnityEngine;
using UnityEngine.Timeline;

namespace FS.Animation
{
    [CreateAssetMenu(menuName = "FS/Animation/Timeline Animation", fileName = "New Timeline Animation")]
    public class TimelineAnimation : FSAnimation
    {
        public TimelineAsset Animation;
        public AnimationEventHolder AnimationEvents;
        [SerializeField, HideInInspector] private PlayableAssetTransition m_transition = new();

        private void OnValidate()
        {
            // Generate the transition
            m_transition.Asset = Animation;
        }

        public override ITransition GetTransition() => m_transition;
        public override AnimationEventHolder GetEventsFor(int childIdx) => AnimationEvents;
        public override bool HasValidClips() => Animation != null;
    }
}