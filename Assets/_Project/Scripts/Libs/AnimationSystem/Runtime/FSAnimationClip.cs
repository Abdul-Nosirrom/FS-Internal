using System;
using System.Collections.Generic;
using Animancer;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR 
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
#endif

namespace FS.Animation
{
    /// <summary>
    /// Wrapper for a singular animation clip that can be played via Animancer and supports our custom AnimationEvents
    /// </summary>
    [Serializable]
    public class FSAnimationClip : IAnimation
    {
        [SerializeField, AssetSelector, HideLabel, Required] public AnimationClip m_clip;
        
        [SerializeField, Range(0, 2)] private float m_playbackSpeed = 1;
        [SerializeField, Range(0, 1)] private float m_fadeDuration = 0.1f;
        
        [SerializeField] private bool m_hasNormalizedStartTime = false;
        [SerializeField, Range(0, 1), ShowIf("m_hasNormalizedStartTime")] 
        private float m_normalizedStartTime = 0;

        [SerializeField] private AnimationEventHolder m_animEvents;
        public AnimationEventHolder AnimationEvents => m_animEvents;
        
        [SerializeField, HideInInspector] 
        private ClipTransition m_transition = new();

        public AnimancerState Play(AnimancerComponent animator, FSAnimationLayer layer)
        {
            if (!animator) return null;
            
            AnimancerState state = animator.Layers[(int)layer].Play(this);
            if (state.Events(animator, out var events))
                m_animEvents?.Bind(animator, state, events);
            
            return state;
        }
        public AnimancerState Play(AnimancerComponent animator) => Play(animator, 0);
        
        
        public ITransition GetTransition()
        {
            m_transition ??= new();
            
            // Ensure parameter match-up
            if (m_transition.Clip != m_clip) m_transition.Clip = m_clip;
            m_transition.Speed = Speed;
            m_transition.NormalizedStartTime = m_hasNormalizedStartTime ? m_normalizedStartTime : Single.NaN;
            m_transition.FadeDuration = m_fadeDuration * (m_clip ? m_clip.length : 0) / m_playbackSpeed;

            return m_transition;
        }
        
        #region ITransition Implementation

        public AnimancerState CreateState()
        {
            var state = GetTransition().CreateState();
            state?.SetDebugName(m_clip?.name);
            return state;
        }
        
        public float Speed { get => m_playbackSpeed; set => m_playbackSpeed = value; }
        
        #endregion
    }
    
#if UNITY_EDITOR
    public class FSAnimationClipDrawer : OdinValueDrawer<FSAnimationClip>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var labelRect = EditorGUILayout.GetControlRect();
            
            using var scope = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);

            if (label != null)
            {
                var boxHeader = SirenixEditorGUI.BeginBoxHeader();
                EditorGUILayout.LabelField(label, SirenixGUIStyles.WhiteLabelCentered);
                //EditorGUI.DropShadowLabel(boxHeader, label);
                SirenixEditorGUI.EndBoxHeader();
            }
            
            //var clipVal = ValueEntry.SmartValue;
            //clipVal.m_clip = AssetSele
            
            foreach (var child in Property.Children)
                child.Draw();
        }
    }
#endif    
}