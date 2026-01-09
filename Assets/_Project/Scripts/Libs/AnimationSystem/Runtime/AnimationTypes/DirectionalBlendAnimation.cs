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
    public abstract class DirectionalBlendAnimationBase : FSAnimation
    {
        [Serializable]
        protected struct DirectionalMixerElement
        {
            public FSAnimationClip m_animation;
            public Vector2 m_threshold;
            public bool m_synchronize;

            public static DirectionalMixerElement Get(FSAnimationClip animation, Vector2 threshold, bool synchronize)
                => new DirectionalMixerElement { m_animation = animation, m_threshold = threshold, m_synchronize = synchronize };
        }
        
        protected abstract string VeritcalParameterName { get; }
        protected abstract string HorizontalParameterName { get; }

        protected abstract DirectionalMixerElement[] GetMixerElements();
        
        [SerializeField, HideInInspector] private MixerTransition2D m_transition = new();
        [SerializeField, HideInInspector] private DirectionalMixerElement[] m_mixerElements;

        [SerializeField] protected MixerTransition2D.MixerType m_type = MixerTransition2D.MixerType.Cartesian;
        [SerializeField, Range(0, 1)] protected float m_blendDuration = 0.25f;
        [SerializeField] protected AnimationEventHolder SharedEvents;
        
        private void OnValidate()
        {
            m_mixerElements = GetMixerElements();

            m_transition.Type = m_type;
            m_transition.Animations = new Object[m_mixerElements.Length];
            m_transition.Speeds = new float[m_mixerElements.Length];
            m_transition.SynchronizeChildren = new bool[m_mixerElements.Length];
            m_transition.Thresholds = new Vector2[m_mixerElements.Length];

            m_transition.FadeDuration = m_blendDuration;
            
            for (int i = 0; i < m_mixerElements.Length; i++)
            {
                var mixerItem = m_mixerElements[i];
                
                if (Application.isPlaying)
                    Assert.IsNotNull(mixerItem.m_animation.m_clip, $"Animation clip for index {i} is null in {name}");
                
                m_transition.Animations[i] = mixerItem.m_animation.m_clip;
                m_transition.Speeds[i] = mixerItem.m_animation.Speed;
                m_transition.SynchronizeChildren[i] = mixerItem.m_synchronize;
                m_transition.Thresholds[i] = mixerItem.m_threshold;
            }
        }

        public override ITransition GetTransition() => m_transition;

        public override AnimancerState CreateState()
        {
            var directionalState = base.CreateState() as Vector2MixerState;
            directionalState.ParameterNameX = HorizontalParameterName;
            directionalState.ParameterNameY = VeritcalParameterName;
            return directionalState;
        }

        public override AnimationEventHolder GetEventsFor(int childIdx)
        {
            if (childIdx == -1) return SharedEvents;
            Assert.IsTrue(childIdx < m_mixerElements.Length, $"Child index {childIdx} is out of bounds for {name}. Valid range is 0 to {m_mixerElements.Length - 1}.");
            return m_mixerElements[childIdx].m_animation.AnimationEvents;
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
        
#if UNITY_EDITOR
        private Vector2 m_directionalParameterCache;
        protected void DoPreviewStateParameterControl(Vector2MixerState state)
        {
            EditorGUILayout.LabelField("Preview Controls", SirenixGUIStyles.WhiteLabelCentered);
            SirenixEditorGUI.HorizontalLineSeparator(Color.black, 2);

            state.GetThresholdBounds(out var minVal, out var maxVal, out var isAroundZero);
            
            float x = EditorGUILayout.Slider(m_directionalParameterCache.x, minVal.x, maxVal.x);
            float y = EditorGUILayout.Slider(m_directionalParameterCache.y, minVal.y, maxVal.y);

            m_directionalParameterCache = new(x, y);
            state.Parameter = m_directionalParameterCache;
        }
#endif
    }
}