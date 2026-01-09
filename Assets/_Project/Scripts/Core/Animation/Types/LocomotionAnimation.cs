using Animancer;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif

namespace FS.Animation
{
    
    // Testing
    [CreateAssetMenu(menuName = "FS/Animation/Locomotion/Simple Locomotion Animation", fileName = "New Locomotion Animation")]
    public class LocomotionAnimation : LinearBlendAnimationBase, ILocomotionAnimation
    {
        public FSAnimationClip Walk;
        public FSAnimationClip Run;
        public FSAnimationClip Sprint;
        
        [SerializeField, HideInInspector] 
        private Vector3 m_speedThresholds = new Vector3(3, 8, 15); // Walk, Run, Sprint

        protected override LinearMixerElement[] GetMixerElements() => new[]
        {
            LinearMixerElement.Get(Walk, m_speedThresholds.x, true),
            LinearMixerElement.Get(Run, m_speedThresholds.y, true),
            LinearMixerElement.Get(Sprint, m_speedThresholds.z, true)
        };
        
        protected override string ParameterName => "Speed";

        public override bool HasValidClips()
        {
            return Walk.m_clip && Run.m_clip && Sprint.m_clip;
        }

        public void UpdateSpeedBlending(AnimancerState animState, PhysicsController physics)
        {
            var state = animState as LinearMixerState;
            state.Parameter = physics.Velocity.magnitude;
        }
        
#if UNITY_EDITOR
        private SerializedProperty m_speedThresholdsProperty;
        public override void EditorPreviewParameterControl(AnimancerComponent animator, Rect previewRect, SerializedObject target, AnimancerState activeState)
        {
            m_speedThresholdsProperty = target?.FindProperty("m_speedThresholds");

            if (target == null)
            {
                Debug.LogWarning("LocomotionAnimation: m_speedThresholds property is null");
                return;
            }
            
            var previewControlRect = new Rect(
                previewRect.x + 10,
                previewRect.yMax * 0.7f,
                previewRect.width * 0.35f,
                previewRect.height * 0.25f
            );
            
            var state = activeState as LinearMixerState;
            state.SetThresholds(new []{m_speedThresholds.x, m_speedThresholds.y, m_speedThresholds.z});

            SirenixEditorGUI.DrawRoundRect(previewControlRect, Color.gray1, 4f);
            
            GUILayout.BeginArea(previewControlRect);
            {
                target.Update();
                EditorGUILayout.LabelField("Speed Thresholds", SirenixGUIStyles.WhiteLabelCentered);
                SirenixEditorGUI.HorizontalLineSeparator(Color.black, 2);
                float z = EditorGUILayout.FloatField("Max Speed", m_speedThresholdsProperty.vector3Value.z);
                float x = EditorGUILayout.Slider("Walk Speed", m_speedThresholdsProperty.vector3Value.x, 0, z - 0.01f);
                float y = EditorGUILayout.Slider("Run Speed", m_speedThresholdsProperty.vector3Value.y, 0, z - 0.01f);
                y = Mathf.Clamp(y, x, z);
                m_speedThresholdsProperty.vector3Value = new Vector3(Mathf.Clamp(x, 0, y - 0.01f), Mathf.Clamp(y, x + 0.01f, z - 0.01f), z);
                target.ApplyModifiedPropertiesWithoutUndo();
                
                GUILayout.FlexibleSpace();
                SirenixEditorGUI.HorizontalLineSeparator(Color.rebeccaPurple, 2);
                GUILayout.FlexibleSpace();

                DoPreviewStateParameterControl(state);
            }
            GUILayout.EndArea();
        }
#endif        
    }
}