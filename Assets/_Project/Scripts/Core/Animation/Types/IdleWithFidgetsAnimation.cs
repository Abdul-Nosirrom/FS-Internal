using System;
using Animancer;
using FS.Animation;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

[CreateAssetMenu(menuName = "FS/Animation/Idle/With Fidgets", fileName = "New Idle With Fidgets Animation")]
public class IdleWithFidgetsAnimation : IdleAnimation
{
    [Required] public FSAnimationClip[] Fidgets;
    
    public FSAnimationClip RandomFidget => Fidgets[Random.Range(0, Fidgets.Length)];

    public override AnimancerState Play(AnimancerComponent animator)
    {
        if (!animator) return null;
        if (!HasValidClips())
        {
            return null;
        }
        
        // Should we play this, if so what exactly should we play? Playing (this) will play the IdleBase animation and always reset to that
        // If any state is playing, choose that to play
        // If no state is playing, play the IdleBase animation
        if (animator.States.TryGet(IdleBase.GetTransition(), out var baseIdleState))
        {
            foreach (var fidget in Fidgets)
            {
                if (animator.States.TryGet(fidget.GetTransition(), out var fidgetState))
                {
                    // If we have a fidget state, we should play that instead of the base idle
                    if (fidgetState.IsPlaying)
                    {
                        //fidgetState.Play();
                        return fidgetState;
                    }
                }
            }
            if (baseIdleState.IsPlaying)
            {
                //baseIdleState.Play();
                return baseIdleState;
            }
        }
        

        // First time playing prolly so we don't need to worry about the fidget order as above
        var baseState = animator.Layers[(int)Layer].Play(this);

        if (baseState.Events(animator, out var parentEvents))
        {
            parentEvents.Add(0.955f, () =>
            {
                var state = AnimancerEvent.Current.State;
                if (state.RawTime > Random.Range(4, 8))
                    state.Layer.Play(RandomFidget);
            });
            
            SetupStateEvents(animator, baseState, parentEvents,-1);
        }

        
        int fidgetIdx = 0;
        foreach (var fidget in Fidgets)
        {
            var fidgetState = animator.Layers[(int)Layer].GetOrCreateState(fidget);
            if (fidgetState.Events(animator, out var fidgetEvents))
            {
                fidgetEvents.Add(0.95f, () => AnimancerEvent.Current.State.Layer.Play(IdleBase));
                SetupStateEvents(animator, fidgetState, fidgetEvents, fidgetIdx);
            }

            fidgetIdx++;
        }

        return baseState;
    }

    public override ITransition GetTransition() => IdleBase.GetTransition();
    public override AnimationEventHolder GetEventsFor(int childIdx)
    {
        if (childIdx == -1) return IdleBase.AnimationEvents;
        return Fidgets[childIdx]?.AnimationEvents;
    }

    public override bool HasValidClips() 
        => IdleBase.m_clip && Fidgets.Length > 0 && Array.TrueForAll(Fidgets, fidget => fidget.m_clip);
    
    // TODO: Editor w/ buttons to play one of the fidgets!
}