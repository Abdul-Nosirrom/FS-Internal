using System;
using Animancer;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

#if UNITY_EDITOR 
using Sirenix.Utilities.Editor;
#endif 

namespace FS.Animation
{
    /// <summary>
    /// Good practice to create base classes for the various core transition types to avoid too much repeated code,
    /// while still allowing for explicit declaration of animations you need in your subclasses
    /// </summary>
    public abstract class LinearBlendAnimationBase : FSAnimation
    {
        [Serializable]
        protected struct LinearMixerElement
        {
            public FSAnimationClip m_animation;
            public float m_threshold;
            public bool m_sychronize;
            
            public static LinearMixerElement Get(FSAnimationClip animation, float threshold, bool synchronize)
            => new LinearMixerElement { m_animation = animation, m_threshold = threshold, m_sychronize = synchronize };
        }
        
        protected abstract LinearMixerElement[] GetMixerElements();
        protected abstract string ParameterName { get; }
        
        [SerializeField, HideInInspector] private LinearMixerTransition m_transition = new LinearMixerTransition();
        [SerializeField, HideInInspector] private LinearMixerElement[] m_mixerElements = Array.Empty<LinearMixerElement>();
        
        [SerializeField, Range(0, 1)] protected float m_blendDuration = 0.25f;
        [SerializeField] protected AnimationEventHolder SharedEvents;
        
        private void OnValidate()
        {
            // Initialize Transition
            m_mixerElements = GetMixerElements();

            m_transition.Animations = new Object[m_mixerElements.Length];
            m_transition.Speeds = new float[m_mixerElements.Length];
            m_transition.SynchronizeChildren = new bool[m_mixerElements.Length];
            m_transition.Thresholds = new float[m_mixerElements.Length];

            m_transition.FadeDuration = m_blendDuration; // Default fade duration, can be overridden in subclasses
            
            for (int i = 0; i < m_mixerElements.Length; i++)
            {
                var mixerItem = m_mixerElements[i];
                
                if (Application.isPlaying)
                    Assert.IsNotNull(m_mixerElements[i].m_animation.m_clip, $"Animation clip for index {i} is null in {name}");
                
                m_transition.Animations[i] = mixerItem.m_animation.m_clip;
                m_transition.Speeds[i] = mixerItem.m_animation.Speed;
                m_transition.SynchronizeChildren[i] = mixerItem.m_sychronize;
                m_transition.Thresholds[i] = mixerItem.m_threshold;
            }
        }

        public sealed override ITransition GetTransition() => m_transition;
        public override AnimancerState CreateState()
        {
            var linearState = (LinearMixerState)base.CreateState();
            linearState.ParameterName = ParameterName;
            return linearState;
        }

        public override bool HasValidClips()
        {
            foreach (var mixerElement in m_mixerElements)
            {
                if (mixerElement.m_animation.m_clip == null)
                {
                    return false; // If any animation clip is null, the blend is invalid
                }
            }

            return true;
        }

        public override AnimationEventHolder GetEventsFor(int childIdx)
        {
            if (childIdx == -1) return SharedEvents;
            
            Assert.IsTrue(childIdx < m_mixerElements.Length, $"Child index {childIdx} is out of bounds for {name}. Valid range is 0 to {m_mixerElements.Length - 1}.");
            
            return m_mixerElements[childIdx].m_animation.AnimationEvents;
        }
        
#if UNITY_EDITOR
        private float m_cachedParameterPreview = 0f;
        protected void DoPreviewStateParameterControl(LinearMixerState state)
        {
            EditorGUILayout.LabelField("Preview Controls", SirenixGUIStyles.WhiteLabelCentered);
            SirenixEditorGUI.HorizontalLineSeparator(Color.black, 2);
            
            m_cachedParameterPreview = EditorGUILayout.Slider(m_cachedParameterPreview, state.MinThreshold, state.MaxThreshold);
            
            state.Parameter = m_cachedParameterPreview;
        }
#endif        
    }
}