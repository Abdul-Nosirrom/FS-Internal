using System;
using System.Collections.Generic;
using Animancer;
using Drawing;
using FS.Math;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using Sirenix.Utilities.Editor;
#endif

namespace FS.Animation
{
    [CreateAssetMenu(menuName = "FS/Animation/Locomotion/Locomotion With Leans Animation", fileName = "New Locomotion Animation")]
    public class LocomotionLeansAnimation : DirectionalBlendAnimationBase, ILocomotionAnimation
    {
        [Serializable]
        private struct LocomotionClip
        {
            /// <summary>
            /// Parameter where Y is 'speed', XY are lean thresholds
            /// </summary>
            public Vector3 Thresholds;
            
            public FSAnimationClip Forward;
            public FSAnimationClip LeftLean;
            public FSAnimationClip RightLean;
            
            public DirectionalMixerElement[] GetMixer() => new []
            {
                DirectionalMixerElement.Get(LeftLean, new Vector2(Thresholds.x, Thresholds.y), true),
                DirectionalMixerElement.Get(Forward, new Vector2(0, Thresholds.y), true),
                DirectionalMixerElement.Get(RightLean, new Vector2(Thresholds.z, Thresholds.y), true),
            };
            
            public bool IsValid() => Forward.m_clip && LeftLean.m_clip && RightLean.m_clip;
        }
        
        [SerializeField] private LocomotionClip Walk;
        [SerializeField] private LocomotionClip Run;

        protected override string VeritcalParameterName => "Speed";
        protected override string HorizontalParameterName => "Lean Amount";

        protected override DirectionalMixerElement[] GetMixerElements()
        {
            var walkMixer = Walk.GetMixer();
            var runMixer = Run.GetMixer();
            var result = new List<DirectionalMixerElement>();
            foreach (var walk in walkMixer) result.Add(walk);
            foreach (var run in runMixer) result.Add(run);
            return result.ToArray();
        }

        public void UpdateSpeedBlending(AnimancerState animState, PhysicsController physics)
        {
            var state = animState as Vector2MixerState;
    
            // Speed is straightforward
            state.ParameterY = physics.LateralVelocity.magnitude;
    
            // Lean represents turning: how perpendicular is velocity to forward direction?
            float lean = CalculateLean(physics);
            Draw.ingame.Label2D(physics.transform.position + physics.transform.up * physics.CapsuleHeight, $"Lean: {lean}");

            // Dont blend at the start so we get the right value
            if (state.Time <= 0.1f)
                state.ParameterX = lean;
            else state.ParameterX = Mathf.Lerp(state.ParameterX, lean, 3f * Time.deltaTime);
        }

        private float CalculateLean(PhysicsController physics)
        {
            var velocity = physics.LateralVelocity;
    
            // Need minimum speed to determine lean direction
            if (velocity.magnitude < 0.1f)
                return 0f;
    
            var forward = physics.transform.forward.ProjectOnPlane(physics.UpDirection).normalized;
            var right = Vector3.Cross(physics.UpDirection, forward).normalized;
            var velocityDir = velocity.normalized;
    
            // Lean is based on how much velocity deviates to the left/right
            // Dot with right vector gives us signed lean [-1 = left, 1 = right]
            float lean = Vector3.Dot(velocityDir, right);
    
            // Optional: Scale by turn sharpness (angular velocity)
            // This makes tighter turns lean more dramatically
            float turnIntensity = Mathf.Abs(physics.AngularVelocity);// / maxTurnRate; // normalize to [0,1]
            lean *= Mathf.Clamp01(turnIntensity) * 2f;
    
            return Mathf.Clamp(lean, -1f, 1f);
        }

        public override bool HasValidClips() => Walk.IsValid() && Run.IsValid();

#if UNITY_EDITOR
        public override void EditorPreviewParameterControl(AnimancerComponent animator, Rect previewRect, SerializedObject target, AnimancerState activeState)
        {
            var walkControls = target.FindProperty("Walk");
            var runControls = target.FindProperty("Run");

            var walkThresholds = walkControls.FindPropertyRelative("Thresholds");
            var runThresholds = runControls.FindPropertyRelative("Thresholds");
            
            var previewControlRect = new Rect(
                previewRect.x + 10,
                previewRect.yMax - 250,
                previewRect.width * 0.35f,
                200
            );

            Vector2[] GetThresholdsArray()
            {
                return new Vector2[]
                {
                    // Walk
                    new (walkThresholds.vector3Value.x, walkThresholds.vector3Value.y), new (0, walkThresholds.vector3Value.y), new (walkThresholds.vector3Value.z, walkThresholds.vector3Value.y),
                    // Run
                    new (runThresholds.vector3Value.x, runThresholds.vector3Value.y), new (0, runThresholds.vector3Value.y), new (runThresholds.vector3Value.z, runThresholds.vector3Value.y),
                };
            }
            
            var state = activeState as Vector2MixerState;
            if (state == null) return;
            state.SetThresholds(GetThresholdsArray());

            SirenixEditorGUI.DrawRoundRect(previewControlRect, Color.gray1, 4f);
            
            GUILayout.BeginArea(previewControlRect);
            {
                target.Update();
                EditorGUILayout.LabelField("Speed Thresholds", SirenixGUIStyles.WhiteLabelCentered);
                SirenixEditorGUI.HorizontalLineSeparator(Color.black, 2);
                
                EditorGUI.BeginChangeCheck();
                
                // Run Parameters
                float runSpeed = EditorGUILayout.Slider("Run Speed", runThresholds.vector3Value.y, runThresholds.vector3Value.y, 20);
                Vector2 runLeans = SirenixEditorFields.MinMaxSlider("Run Leans", new Vector2(runThresholds.vector3Value.x, runThresholds.vector3Value.z), new Vector2(-10, 10), true);

                SirenixEditorGUI.HorizontalLineSeparator(Color.rebeccaPurple, 2);

                // Walk Parameters
                float walkSpeed = EditorGUILayout.Slider("Walk Speed", walkThresholds.vector3Value.y, walkThresholds.vector3Value.y, 20);
                Vector2 walkLeans = SirenixEditorFields.MinMaxSlider("Walk Leans", new Vector2(walkThresholds.vector3Value.x, walkThresholds.vector3Value.z), new Vector2(-10, 10), true);

                if (EditorGUI.EndChangeCheck())
                {
                    runThresholds.vector3Value = new Vector3(runLeans.x, runSpeed, runLeans.y);
                    walkThresholds.vector3Value = new Vector3(walkLeans.x, walkSpeed, walkLeans.y);
                }

                target.ApplyModifiedPropertiesWithoutUndo();
                
                //GUILayout.FlexibleSpace();
                SirenixEditorGUI.HorizontalLineSeparator(Color.rebeccaPurple, 2);
                //GUILayout.FlexibleSpace();

                DoPreviewStateParameterControl(state);
                
                GUILayout.Space(10);
            }
            GUILayout.EndArea();
        }
#endif       
    }
}