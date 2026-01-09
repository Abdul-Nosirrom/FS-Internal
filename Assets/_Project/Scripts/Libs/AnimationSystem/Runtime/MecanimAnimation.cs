using System;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
#endif

namespace FS.Animation
{
    [Serializable]
    public class MecanimAnimation : IAnimation
    {
        [SerializeField, AssetSelector, HideLabel, Required] public RuntimeAnimatorController m_animController;
                
        [SerializeField, Range(0, 2)] private float m_playbackSpeed = 1;
        [SerializeField, Range(0, 1)] private float m_fadeDuration = 0.1f;
        
        [SerializeField] private bool m_hasNormalizedStartTime = false;
        [SerializeField, Range(0, 1), ShowIf("m_hasNormalizedStartTime")] 
        private float m_normalizedStartTime = 0;

        // TODO: I think for MecanimAnimations, events should be done with an animationstate script on the anim controller, dont think i can do it well in here!
        //[SerializeField] private SerializableDictionary<AnimationClip, List<FSAnimationEvent>> m_clipEvents;
        
        //[SerializeField] 
        //private List<FSAnimationEvent> m_events; // TODO: Make a wrapper for list of events & use timeline editor for it
        //public FSAnimationEvent[] AnimationEvents => m_events.ToArray();
        
        [SerializeField, HideInInspector] 
        private ControllerTransition m_transition = new();

        // public void ValidateEvents()
        // {
        //     if (m_animController == null) return;
        //     
        //     // Ensure clipEvents match up with clips on the controller
        //     var clips = m_animController.animationClips.ToHashSet();
        //     foreach (var clip in clips)
        //     {
        //         if (m_clipEvents.ContainsKey(clip)) continue;
        //         m_clipEvents[clip] = new();
        //     }
        //     
        //     // Check any missing shit
        //     var storedClips = m_clipEvents.Keys;
        //     foreach (var storedClip in storedClips)
        //     {
        //         if (clips.Contains(storedClip)) continue;
        //         if (!clips.Contains(storedClip))
        //         {
        //             // There's a mismatch, remove this
        //             m_clipEvents.Remove(storedClip);
        //         }
        //     }
        // }
        
#if UNITY_EDITOR
        public ControllerState editor_recentlyPlayedState;
#endif        

        public AnimancerState Play(AnimancerComponent animator, FSAnimationLayer layer)
        {
            if (!animator) return null;

            // For Mecanim animations, events are placed on the anim controller and get manually invoked
            ControllerState state = (ControllerState)animator.Layers[(int)layer].Play(this);
            
            return state;
        }
        public AnimancerState Play(AnimancerComponent animator) => Play(animator, 0);
        
        
        public ITransition GetTransition()
        {
            m_transition ??= new ControllerTransition();
            
            // Ensure parameter match-up
            if (m_transition.Controller != m_animController) m_transition.Controller = m_animController;
            m_transition.Speed = Speed;
            m_transition.NormalizedStartTime = m_hasNormalizedStartTime ? m_normalizedStartTime : Single.NaN;
            m_transition.FadeDuration = m_fadeDuration / m_playbackSpeed;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                editor_recentlyPlayedState = m_transition.State;
            }
#endif         
            
            return m_transition;
        }
        
        #region ITransition Implementation

        public AnimancerState CreateState()
        {
            var state = GetTransition().CreateState();
            state?.SetDebugName(m_animController?.name);
            return state;
        }
        
        public float Speed { get => m_playbackSpeed; set => m_playbackSpeed = value; }
        
        #endregion
    }
    
#if UNITY_EDITOR
    public class MecanimAnimationDrawer : OdinValueDrawer<MecanimAnimation>
    {
        private bool m_parameterFoldout = false;
        
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
            
            // Draw preview controls
            var mecanimAnim = ValueEntry.SmartValue;
            var state = mecanimAnim.editor_recentlyPlayedState;
            if (state == null) return;
            var parameterCount = mecanimAnim.editor_recentlyPlayedState.GetParameterCount();
            
            EditorGUI.BeginChangeCheck();

            SirenixEditorGUI.HorizontalLineSeparator(Color.rebeccaPurple, 2);
            var lastRect = GUILayoutUtility.GetLastRect();

            EditorGUI.indentLevel++;
            m_parameterFoldout = EditorGUILayout.Foldout(m_parameterFoldout, "Parameters");
            EditorGUI.indentLevel--;
            
            if (m_parameterFoldout)
            {
                lastRect.y += EditorGUIUtility.singleLineHeight;
                lastRect.height = (0.5f + parameterCount) * EditorGUIUtility.singleLineHeight;
                var bgColor = new Color(0.1f, 0.1f, 0.1f, 1f);
                SirenixEditorGUI.DrawRoundRect(lastRect, bgColor, 4);
                
                for (int p = 0; p < parameterCount; p++)
                {
                    var param = state.GetParameter(p);
                    switch (param.type)
                    {
                        case AnimatorControllerParameterType.Float:
                            float newFloat =
                                EditorGUILayout.Slider(param.name, state.GetFloat(param.nameHash), -25f, 25f);
                            state.SetFloat(param.nameHash, newFloat);
                            break;
                        case AnimatorControllerParameterType.Int:
                            int newInt =
                                EditorGUILayout.IntSlider(param.name, state.GetInteger(param.nameHash), -10, 10);
                            state.SetInteger(param.nameHash, newInt);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            bool newBool = EditorGUILayout.Toggle(param.name, state.GetBool(param.nameHash));
                            state.SetBool(param.nameHash, newBool);
                            break;
                        case AnimatorControllerParameterType.Trigger:
                            if (GUILayout.Button($"[Trigger] {param.name}"))
                                state.SetTrigger(param.nameHash);
                            break;
                    }
                }

                //GUI.color = ogColor;
            }

            if (EditorGUI.EndChangeCheck())
            {
                GUI.changed = false; // HACKY BUT COOL - we don't wanna propogate ChangeCheck to FSAnimationEditor which'd cause a state reset, making it impossible to debug our previews
            }
        }
    }
#endif    
}