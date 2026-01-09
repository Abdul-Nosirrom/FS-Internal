using Animancer;
using FS.Animation;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "FS/Animation/Idle/Regular Idle", fileName = "New Idle Animation")]
public class IdleAnimation : FSAnimation, IIdleAnimation
{
    [Required] public FSAnimationClip IdleBase;
        
    public override ITransition GetTransition() => IdleBase.GetTransition();
    public override AnimationEventHolder GetEventsFor(int childIdx) => IdleBase.AnimationEvents;
    public override bool HasValidClips() => IdleBase.m_clip;
}