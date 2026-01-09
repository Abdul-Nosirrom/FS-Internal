using System;
using System.Collections.Generic;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FS.UI
{

    /// <summary>
    /// Here's the deal, this component collects all tweens into a timeline of a fixed duration
    /// - Each tween has a start time and end time within the timeline [0, 1], normalized to make things cleaner
    /// - Start time corresponds to a start delay, end time corresponds to a shortening of the instances duration
    ///     - StartDelay = startTime * totalDuration
    ///     - Duration = (endTime - startTime) * totalDuration
    /// </summary>

    [Serializable]
    public class TweenAnimationHolder
    {
        [SerializeReference] public TweenAnimation Animation;

        [Range(0, 1)] public float StartTime = 0f;
        [Range(0, 1)] public float EndTime = 1f;

        public Tween GetTween(float totalDuration, bool reverse = false)
        {
            if (Animation == null) return default;
            Animation.StartTime = StartTime;
            Animation.EndTime = EndTime;
            return Animation.GetTween(totalDuration, reverse);
        }
    }
    
    public class TweenAnimator : MonoBehaviour
    {
        public float m_totalDuration = 1f;
        public List<TweenAnimationHolder> m_tweenAnimations = new();

        private Sequence m_activeSequence;
        
        public bool IsPlaying => m_activeSequence.isAlive;
        public float ElapsedTime => m_activeSequence.elapsedTime;
        public ref Sequence ActiveSequence => ref m_activeSequence;

        [Button("Play Animation")]
        public void Play()
        {
            if (m_activeSequence.isAlive)
            {
                m_activeSequence.isPaused = false;
                return;
            }
            
            m_activeSequence = Sequence.Create();
            foreach (var anim in m_tweenAnimations)
            {
                m_activeSequence.Group(anim.GetTween(m_totalDuration));
            }

            m_activeSequence.OnComplete(OnSequenceComplete);
        }
        
        [Button("Reverse Animation")]
        public void Reverse()
        {
            if (m_activeSequence.isAlive)
            {
                m_activeSequence.isPaused = false;
                return;
            }
            
            m_activeSequence = Sequence.Create();
            foreach (var anim in m_tweenAnimations)
            {
                m_activeSequence.Group(anim.GetTween(m_totalDuration, true));
            }
            
            m_activeSequence.OnComplete(OnSequenceComplete);
        }

        public void Pause()
        {
            if (!m_activeSequence.isAlive) return;
            m_activeSequence.isPaused = true;
        }

        public void UpdateSequence()
        {
            if (!m_activeSequence.isAlive) return;
         
            var prevSeq = m_activeSequence;
            m_activeSequence = Sequence.Create();
            foreach (var anim in m_tweenAnimations)
            {
                m_activeSequence.Group(anim.GetTween(m_totalDuration));
            }
            
            m_activeSequence.elapsedTime = prevSeq.elapsedTime;
            m_activeSequence.isPaused = prevSeq.isPaused;
            
            prevSeq.Stop();
        }

        protected virtual void OnSequenceComplete() {}

        // public void CompileTweens()
        // {
        //     // Chain them and shit
        //     
        // }
        //
        // private void AllTweens()
        // {
        //     // Transform based tweens
        //     Tween.Position(target, startValue, endValue, duration, easeMode, cycles, cycleMode, startDelay, endDelay);
        //     Tween.PositionX(target, startValue, endValue, duration, easeMode, cycles, cycleMode, startDelay, endDelay);
        //     Tween.PositionY(target, startValue, endValue, duration, easeMode, cycles, cycleMode, startDelay, endDelay);
        //     Tween.PositionZ(target, startValue, endValue, duration, easeMode, cycles, cycleMode, startDelay, endDelay);
        //     
        // }
    }
}