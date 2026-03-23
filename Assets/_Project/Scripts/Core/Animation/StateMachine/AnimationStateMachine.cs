using System;
using Animancer;
using HFSM;
using UnityEngine;

namespace FS.Animation.HFSM
{
    // public class RootStateMachine : StateMachine
    // {
    //     public RootStateMachine(params StateObject[] states) : base(states) {}
    // }
    
    public class AnimationStateMachine : StateMachine
    {
        private FSAnimator m_animator;
        
        public AnimationStateMachine(FSAnimator animator, params StateObject[] stateObjects) : base(stateObjects)
        {
            m_animator = animator;
        }

        protected override void OnEnter()
        {
            base.OnEnter();
        }
    }
    

    public class AnimationState : State
    {
        public static AnimationState Create(IAnimation animation, FSAnimator animator) => new AnimationState(animation, animator);

        private readonly IAnimation m_animation;
        private readonly FSAnimator m_animator;
        
        public AnimationState(IAnimation anim, FSAnimator animator)
        {
            m_animation = anim;
            m_animator = animator;
        }

        private float m_transitionFadeDuration = -1f;

        protected override void OnExit()
        {
            m_transitionFadeDuration = -1f; // reset
        }

        protected override void OnUpdate()
        {
            m_animation.Play(m_animator, m_transitionFadeDuration);

            // If we've completed fading in, reset the transition fade duration
            if (m_transitionFadeDuration >= 0 && m_animation.TryGetState(m_animator, out var state))
            {
                if (state.Weight >= 1) m_transitionFadeDuration = -1f;
            }
        }

        /// <summary>
        /// Transitions to a given state when the given conditions true and specifies fade duration
        /// </summary>
        public void AddFadedTransition(StateObject destState, float fadeDuration = -1f, params Func<bool>[] conditions) // how to set fadeDuration?
        {
            if (destState is not AnimationState animState) throw new ArgumentException("destState is not AnimationState", nameof(destState));
            
            AddTransition(destState, () => animState.SetFadeDuration(fadeDuration), conditions);
        }

        /// <summary>
        /// Transitions to a given state when state time is >= exitTime
        /// </summary>
        public void AddExitTransition(State destState, float exitTime = 1, float fadeDuration = -1, params Func<bool>[] conditions)
        {
            if (destState is not AnimationState animState) throw new ArgumentException("destState is not AnimationState", nameof(destState));
            
            AddTransition(destState, 
                () => animState.SetFadeDuration(fadeDuration), 
                () =>
                {
                    bool result = true;
                    foreach (var condition in conditions) result = result && condition();
                    bool doesStateExist = m_animation.TryGetState(animState.m_animator, out var properAnimState);
                    return result && doesStateExist &&
                           properAnimState.NormalizedTime >= exitTime;
                });
        }

        private void SetFadeDuration(float fadeDuration)
        {
            m_transitionFadeDuration = fadeDuration;
        }
    }
}