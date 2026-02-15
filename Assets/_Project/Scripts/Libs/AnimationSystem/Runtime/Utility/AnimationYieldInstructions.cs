using System;
using Animancer;
using FS.TagSystem;
using UnityEngine;

namespace FS.Animation
{
    /// <summary>
    /// Extension methods providing fluent API for creating animation yield instructions.
    /// Usage: yield return state.WaitForFadeOut();
    /// </summary>
    public static class AnimationYieldExtensions
    {
        /// <summary>
        /// Wait until the animation begins fading out (TargetWeight becomes 0).
        /// </summary>
        public static WaitForAnimationFadeOut WaitForFadeOut(this AnimancerState state) => new(state);

        /// <summary>
        /// Wait until the animation is fully faded in (Weight reaches 1).
        /// </summary>
        public static WaitForEndFadeIn WaitForFadeIn(this AnimancerState state) => new(state);

        /// <summary>
        /// Wait until the animation reaches a specific normalized time (0-1).
        /// </summary>
        public static WaitForNormalizedTime WaitForTime(this AnimancerState state, float normalizedTime) => new(state, normalizedTime);

        /// <summary>
        /// Wait for a specific animation flag to be broadcast while this state is active.
        /// </summary>
        public static WaitForAnimationFlag<T> WaitForFlag<T>(this AnimancerState state, FSAnimator animator, T flag) where T : ITagSource => new(state, animator, flag);

        // IAnimation overloads - convenience for when you have the animation asset but not the state
        
        public static WaitForAnimationFadeOut WaitForFadeOut(this IAnimation anim, FSAnimator animator) => new(anim.GetState(animator));
        public static WaitForEndFadeIn WaitForFadeIn(this IAnimation anim, FSAnimator animator) => new(anim.GetState(animator));
        public static WaitForNormalizedTime WaitForTime(this IAnimation anim, FSAnimator animator, float normalizedTime) => new(anim.GetState(animator), normalizedTime);
        public static WaitForAnimationFlag<T> WaitForFlag<T>(this IAnimation anim, FSAnimator animator, T flag) where T : ITagSource => new(anim.GetState(animator), animator, flag);
    }

    /// <summary>
    /// Base class for animation-related yield instructions.
    /// Implements IDisposable to support proper cleanup when coroutines are stopped early
    /// via StopAndDisposeCoroutine.
    /// </summary>
    public abstract class AnimationYieldBase : CustomYieldInstruction, IDisposable
    {
        protected AnimancerState m_animancerState;
        private bool m_isDisposed;

        protected AnimationYieldBase(AnimancerState state)
        {
            m_animancerState = state;
        }

        /// <summary>
        /// Whether the underlying AnimancerState is still valid (not destroyed).
        /// </summary>
        protected bool IsStateValid => m_animancerState != null && m_animancerState.IsValid();

        /// <summary>
        /// Called exactly once when the yield instruction completes (normal or early termination).
        /// Override to unsubscribe from events or release resources.
        /// </summary>
        protected virtual void Cleanup() { }

        /// <summary>
        /// Performs cleanup and nulls references. Safe to call multiple times.
        /// Called automatically on normal completion or manually via Dispose() for early termination.
        /// </summary>
        protected void DoCleanup()
        {
            if (m_isDisposed) return;
            m_isDisposed = true;
            
            Cleanup();
            m_animancerState = null;
        }

        /// <summary>
        /// Call this when the coroutine is stopped early to ensure proper cleanup.
        /// Typically called via StopAndDisposeCoroutine extension.
        /// </summary>
        public void Dispose() => DoCleanup();
    }
    
    public abstract class WaitForAnimationPlaybackEvent : AnimationYieldBase
    {
        private bool m_triggered;
        private AnimationPlaybackEventManager.Type m_eventType;

        public WaitForAnimationPlaybackEvent(AnimancerState state, AnimationPlaybackEventManager.Type evt) : base(state)
        {
            m_eventType = evt;
            state.BindPlaybackEvent(OnFadeOut, evt);
        }

        public override bool keepWaiting
        {
            get
            {
                // Complete if triggered or state became invalid
                if (m_triggered || !IsStateValid)
                {
                    DoCleanup();
                    return false;
                }
                return true;
            }
        }

        private void OnFadeOut() => m_triggered = true;

        protected override void Cleanup()
        {
            m_animancerState?.UnbindPlaybackEvent(OnFadeOut, m_eventType);
        }
    }

    /// <summary>
    /// Waits until an animation begins fading out (TargetWeight becomes 0).
    /// Uses AnimationPlaybackEventManager for event-driven detection rather than polling.
    /// </summary>
    /// <example>
    /// yield return myState.WaitForFadeOut();
    /// Debug.Log("Animation started fading out!");
    /// </example>
    public class WaitForAnimationFadeOut : WaitForAnimationPlaybackEvent
    { 
        public WaitForAnimationFadeOut(AnimancerState state) : base(state, AnimationPlaybackEventManager.Type.BeginFadeOut) {}
    }

    /// <summary>
    /// Waits until an animation is fully faded in (Weight reaches 1).
    /// Useful for waiting until a blend is complete before taking further action.
    /// </summary>
    /// <example>
    /// var state = myAnimation.Play(animator);
    /// yield return state.WaitForFadeIn();
    /// Debug.Log("Animation fully blended in!");
    /// </example>
    public class WaitForEndFadeIn : WaitForAnimationPlaybackEvent
    {
        public WaitForEndFadeIn(AnimancerState state) : base(state, AnimationPlaybackEventManager.Type.EndFadeIn) {}
    }

    /// <summary>
    /// Waits until an animation reaches a specific normalized time.
    /// Uses polling since Animancer doesn't provide time-based events.
    /// </summary>
    /// <example>
    /// yield return myState.WaitForTime(0.5f);
    /// Debug.Log("Animation reached halfway point!");
    /// </example>
    public class WaitForNormalizedTime : AnimationYieldBase
    {
        private readonly float m_targetTime;

        /// <param name="state">The animation state to monitor.</param>
        /// <param name="normalizedTime">Target normalized time (0-1).</param>
        public WaitForNormalizedTime(AnimancerState state, float normalizedTime) : base(state)
        {
            m_targetTime = Mathf.Clamp01(normalizedTime);
        }

        public override bool keepWaiting
        {
            get
            {
                if (!IsStateValid || m_animancerState.NormalizedTime >= m_targetTime)
                {
                    DoCleanup();
                    return false;
                }
                return true;
            }
        }
    }

    /// <summary>
    /// Waits for a specific animation flag to be broadcast while the animation state is active.
    /// Flags are typically broadcast via animation events configured in the animation clips.
    /// 
    /// NOTE: If multiple flags are provided (Flag1 | Flag2), the callback won't specify which
    /// flag was triggered. Additionally, it's only guaranteed that the state was active when
    /// the flag was invoked, not necessarily that this specific state caused the flag.
    /// </summary>
    /// <example>
    /// yield return myState.WaitForFlag(animator, Tag.WallEject);
    /// Debug.Log("WallEject flag was broadcast!");
    /// </example>
    public class WaitForAnimationFlag<T> : AnimationYieldBase where T : ITagSource
    {
        private FSAnimator m_animator;
        private T m_flag;
        private bool m_wasFlagBroadcasted;

        public WaitForAnimationFlag(AnimancerState state, FSAnimator animator, T flag) : base(state)
        {
            m_animator = animator;
            m_flag = flag;
            m_animator.OnAnimationFlagBroadcast += OnFlagBroadcasted;
        }

        public override bool keepWaiting
        {
            get
            {
                // Complete if flag was broadcast or state became invalid
                if (m_wasFlagBroadcasted || !IsStateValid)
                {
                    DoCleanup();
                    return false;
                }
                return true;
            }
        }

        private void OnFlagBroadcasted(ITagSource flag)
        {
            // Check if any of our target flags were broadcast
            if (m_flag.HasAny(flag))
            {
                m_wasFlagBroadcasted = true;
            }
        }

        protected override void Cleanup()
        {
            if (m_animator != null)
            {
                m_animator.OnAnimationFlagBroadcast -= OnFlagBroadcasted;
                m_animator = null;
            }
            m_flag = default;
        }
    }
}