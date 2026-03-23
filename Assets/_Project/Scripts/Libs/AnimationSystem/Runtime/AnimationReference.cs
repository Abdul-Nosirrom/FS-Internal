using System;
using System.Reflection;
using Animancer;
using UnityEngine;

namespace FS.Animation
{
    /// <summary>
    /// Data type to be able to generically reference an animation that exists on a FSAnimator through one of its animation sets.
    /// No asset reference needed towards an animation, so this is good to use when you want to play a "animation" on multiple
    /// rigs, as it'll correctly get the unique animation defined on the animator that is a field from a specific set
    ///
    /// You can also retrieve AnimancerStates using this type.
    /// </summary>
    /// <example>
    /// <code>
    /// public class LeverInteractable : MonoBehavior
    /// {
    ///     [SerializeField] private AnimationReference m_leverPullAnimation;
    ///     public void OnInteractionTriggered(GameObject interactor)
    ///     {
    ///         var animator = interactor.GetComponent[FSAnimator]();
    ///         m_leverPullAnimation.Play(animator);
    ///         // Alternatively, you can get the set directly w/out a variable reference if you know which anim to play
    ///         animator.GetAnimationSet[InteractionAnimSet]().PullLever.Play(animator);
    ///
    ///         var state = m_leverPullAnimation.GetState(animator);
    ///         state.Stop();
    ///     }
    /// }
    /// </code>
    /// </example>
    [Serializable]
    public struct AnimationReference : IAnimation
    {
        // Editor will show a single drop-down for both the AnimationSet and the Animation, so we can select them from the inspector.
        [HideInInspector] public string SetName; // To an AnimationSet
        [HideInInspector] public string AnimationName; // To an ITransition
        
        private Type m_animationSetType;
        private FieldInfo m_animationField;
        
        public static AnimationReference Get<T>(string animationName) where T : AnimationSet
        {
            AnimationReference reference = new AnimationReference()
            {
                SetName = typeof(T).FullName,
                AnimationName = animationName,
                m_animationSetType = typeof(T),
                m_animationField = GetAnimationField(typeof(T), animationName)
            };
            if (reference.m_animationField == null)
            {
                Debug.LogError($"Animation '{animationName}' does not exist in Animation Set '{typeof(T).Name}'.");
            }
            return reference;
        }
        
        private static FieldInfo GetAnimationField(Type animationSetType, string animationName)
        {
            if (animationSetType == null || string.IsNullOrEmpty(animationName))
            {
                Debug.LogError("Invalid Animation Set type or Animation Name provided.");
                return null;
            }
            
            var field = animationSetType.GetField(animationName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field;
        }
        
        public static bool Validate(AnimationReference reference, out Type outType, out FieldInfo outField, out string errorMsg)
        {
            errorMsg = String.Empty;
            
            if (reference.SetName == null || reference.AnimationName == null)
            {
                errorMsg = "AnimationReference is not initialized properly. SetName and AnimationName must be set.";
                outType = null;
                outField = null;
                return false;
            }
            
            outType = Type.GetType(reference.SetName);
            if (outType == null)
            {
                errorMsg = $"Animation Set type '{reference.SetName}' does not exist.";
                outField = null;
                return false;
            }
            
            outField = GetAnimationField(outType, reference.AnimationName);
            if (outField == null)
            {
                errorMsg = $"Animation '{reference.AnimationName}' does not exist in Valid Animation Set '{reference.SetName}'.";
                return false;
            }

            return true;
        }

        public void Reset()
        {
            Debug.Log("Called Reset on AnimationReference");
            SetName = null;
            AnimationName = null;
        }

        private bool GetTransition(FSAnimator animator, out ITransition transition)
        {
            transition = null;
            
            if (m_animationField == null || m_animationSetType == null)
            {
                if (!Validate(this, out m_animationSetType, out m_animationField, out var errorMsg))
                {
                    Debug.LogError($"Failed to initialize AnimationReference for Set: {SetName}, Animation: {AnimationName}\n Error: {errorMsg}");
                    return false;
                }
            }
            
            // Get the AnimationSet instance from the animator, and play the animation on it
            var animationSet = animator.GetAnimationSet(m_animationSetType);
            if (animationSet == null)
            {
                Debug.LogError($"Animator {animator.gameObject.name} does not have AnimationSet of type: {m_animationSetType}");
                return false;
            }
            
            transition = ((ITransition)m_animationField.GetValue(animationSet));//.GetTransition();

            return true;
        }
        
        // We are getting an animator that we don't know and we could get different animators for each play, so we need to retrieve the animation each time
        public AnimancerState Play(AnimancerComponent animator, float fadeDuration = -1)
        {
            if (!GetTransition(animator as FSAnimator, out var animationBase)) return null;
            if (animationBase is IAnimation fsAnim) return fsAnim.Play(animator, fadeDuration);
            return animator.Play(animationBase, fadeDuration);
        }
        
        public AnimancerState Play(AnimancerComponent animator, FSAnimationLayer layer, float fadeDuration = -1)
        {
            if (!GetTransition(animator as FSAnimator, out var animationBase)) return null;
            if (animationBase is IAnimation fsAnim) return fsAnim.Play(animator, layer, fadeDuration);
            return animator.Play(animationBase, fadeDuration);
        }

        public AnimancerState GetState(FSAnimator animator, bool createIfDoesntExist = false)
        {
            if (!GetTransition(animator, out var animationBase)) return null;

            
            if (createIfDoesntExist)
                return animator.States.GetOrCreate(animationBase);
            
            animator.States.TryGet(animationBase, out var state);
            return state; 
        }

        public bool TryGetState(FSAnimator animator, out AnimancerState state)
        {
            state = null;
            if (!GetTransition(animator, out var animationBase)) return false;

            return animator.States.TryGet(animationBase, out state);
        }
        

        public ITransition GetTransition()
        {
            throw new NotImplementedException("Use GetTransition(FSAnimator animator, out ITransition transition) instead, as AnimationReference needs an animator to resolve the correct animation.");
        }
    }
}