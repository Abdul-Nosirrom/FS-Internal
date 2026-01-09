using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Assertions;

namespace FS.Animation
{
    
    /// <summary>
    /// Base class for controlling animation states. Multiple controllers can be used to manage different parts
    /// of the characters animations. For example, a LocomotionController for movement animation, and it'd update
    /// parameters of the locomotion animation state.
    /// </summary>
    public abstract class AnimationController : MonoBehaviour
    {
        [SerializeField, Required] protected FSAnimator Animator;

        public AnimationSet[] AnimationSets { get; private set;  }

        protected virtual void Awake()
        {
            Assert.IsNotNull(Animator, $"Animator is not assigned on {name}. Please assign a valid FSAnimator component.");

            AnimationSets = InitializeAnimationSets();
            if (AnimationSets == null || AnimationSets.Length == 0)
            {
                Debug.Log($"[AnimationSystem] Controller {name} has no AnimationSets defined.");
                return;
            }

            Animator.PushAnimationSets(this, AnimationSets);
        }

        protected abstract AnimationSet[] InitializeAnimationSets();
        protected abstract void AnimationUpdate();

        protected void LateUpdate()
        {
            AnimationUpdate();
        }
        
    }
}