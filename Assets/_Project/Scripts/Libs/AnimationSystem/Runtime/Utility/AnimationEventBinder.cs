using System;
using Animancer;
using FS.TagSystem;
using UnityEngine;

namespace FS.Animation
{
    /// <summary>
    /// Provides one-shot event binding for animation playback events.
    /// Callbacks automatically unbind after invocation or when the animation fades out.
    /// </summary>
    public static class AnimationEventBinder
    {
        /// <summary>
        /// Binds a one-time callback for when the animation begins fading out.
        /// </summary>
        public static void OnFadeOut(this AnimancerState state, Action callback)
        {
            BindOnce(state, callback, AnimationPlaybackEventManager.Type.BeginFadeOut);
        }

        /// <summary>
        /// Binds a one-time callback for when the animation finishes fading in.
        /// </summary>
        public static void OnFadeIn(this AnimancerState state, Action callback)
        {
            BindOnce(state, callback, AnimationPlaybackEventManager.Type.EndFadeIn);
        }

        /// <summary>
        /// Binds a one-time callback for when the animation is fully faded out.
        /// </summary>
        public static void OnFadedOut(this AnimancerState state, Action callback)
        {
            BindOnce(state, callback, AnimationPlaybackEventManager.Type.EndFadeOut);
        }

        /// <summary>
        /// Binds a one-time callback for a specific animation flag broadcast.
        /// Automatically unbinds if the animation fades out before the flag is triggered.
        /// Only invokes if the state is still active when the flag is broadcast.
        /// </summary>
        public static void OnFlag(this AnimancerState state, FSAnimator animator, TagSet flags, Action callback)
        {
            var binding = new FlagBinding(state, animator, flags, callback);
            binding.Subscribe();
        }

        private static void BindOnce(AnimancerState state, Action callback, AnimationPlaybackEventManager.Type type)
        {
            Action wrapper = null;
            wrapper = () =>
            {
                state.UnbindPlaybackEvent(wrapper, type);
                callback?.Invoke();
            };
            state.BindPlaybackEvent(wrapper, type);
        }
        
        private class FlagBinding
        {
            private readonly AnimancerState m_state;
            private readonly FSAnimator m_animator;
            private readonly TagSet m_flags;
            private readonly Action m_callback;
            private bool m_isCleanedUp;

            public FlagBinding(AnimancerState state, FSAnimator animator, TagSet flags, Action callback)
            {
                m_state = state;
                m_animator = animator;
                m_flags = flags;
                m_callback = callback;
                m_isCleanedUp = false;
            }

            public void Subscribe()
            {
                m_animator.OnAnimationFlagBroadcast += OnFlagBroadcasted;
                m_state.BindPlaybackEvent(Cleanup, AnimationPlaybackEventManager.Type.BeginFadeOut);
            }

            private void Cleanup()
            {
                if (m_isCleanedUp) return;
                m_isCleanedUp = true;

                m_animator.OnAnimationFlagBroadcast -= OnFlagBroadcasted;
                m_state.UnbindPlaybackEvent(Cleanup, AnimationPlaybackEventManager.Type.BeginFadeOut);
            }

            private void OnFlagBroadcasted(TagSet flag)
            {
                if (!m_state.IsValid())
                {
                    Cleanup();  // State destroyed, clean up subscription
                    return;
                }
    
                if (!m_flags.HasAny(flag)) return;
                if (!m_state.IsActive) return;

                Cleanup();
                m_callback?.Invoke();
            }
        }
    }
}