using Animancer;
using UnityEngine;

namespace FS.Animation
{
    // Exists on an FSAnimator not on an asset, so runtime data is fair game
    // TODO: Should this be responsible for updating 'state'? Instead of AnimationControllers? Then we can just plop this
    // onto the FSAnimator and it will handle all the animation updates in one place? (e.g w/ Initialize func & Update func)
    // E.G:
    //
    // So the animation set would be responsible for "state management" and updating the animations parameters through FSAnimation
    // They'd serve the purpose of the AnimationControllers and those wouldn't exist anymore, and now all AnimationSets are defined
    // generically on the FSAnimator.
    // public abstract class AnimationSet
    // {
    //     protected FSAnimator Animator;
    //     public void Initialize(FSAnimator animator)
    //     {
    //         Animator = animator;
    //         OnInitialize();
    //     }
    //
    //     public abstract void OnInitialize();
    //     public abstract void UpdateAnimationSet();
    // }
    
    /// <summary>
    /// Container class for FSAnimations and FSAnimationClips. Defined on AnimationControllers then later collected by the FSAnimator.
    /// Used to store all animations of a given character, this will also allow them to be retrieved generically through the use
    /// of <see cref="AnimationReference"/>. This is the base class for all animation sets, and should be extended
    /// </summary>
    public abstract class AnimationSet {}

}