using Animancer;
using UnityEngine;

namespace FS.Animation
{
    [CreateAssetMenu(menuName = "FS/Animation/Single Animation", fileName = "New Single Animation")]
    public class OneShotAnimation : FSAnimation
    {
        public FSAnimationClip Animation;

        public override ITransition GetTransition() => Animation.GetTransition();
        public override AnimationEventHolder GetEventsFor(int childIdx) => Animation.AnimationEvents;
        public override bool HasValidClips() => Animation.m_clip;
    }
}