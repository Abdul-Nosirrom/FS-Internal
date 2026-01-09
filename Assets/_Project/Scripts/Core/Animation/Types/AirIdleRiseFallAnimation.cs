using Animancer;
using FS.Animation;
using FS.Math;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif

[CreateAssetMenu(menuName = "FS/Animation/AirIdle/Rise And Fall Blend", fileName = "New Air Idle Animation")]
public class AirIdleRiseFallAnimation : LinearBlendAnimationBase, IAirIdleAnimation
{
    [Required] public FSAnimationClip Rise;
    [Required] public FSAnimationClip Fall;
    
    /// <summary>
    /// Speed at which we begin transitioning from the rise to the fall animation.
    /// </summary>
    [SerializeField, HideInInspector] 
    private float m_transitionPoint = 2f;
    
    protected override LinearMixerElement[] GetMixerElements() => new[]
    {
        LinearMixerElement.Get(Fall, -m_transitionPoint, true),
        LinearMixerElement.Get(Rise, m_transitionPoint, true)
    };

    protected override string ParameterName => "FallSpeed";
    
    public void UpdateAirIdleFallSpeed(AnimancerState state, PhysicsController physics)
    {
        var riseState = state as LinearMixerState; // NOTE: Important that the state parameters are not clamped and going below/above will fuck with the visual! (jitter/fast movement shit)
        var verticalSpeed = Mathf.Clamp(physics.Velocity.Dot(-physics.GravityDir), -m_transitionPoint, m_transitionPoint);
        
        if (riseState.Time <= 0.1f)
            riseState.Parameter = verticalSpeed;
        else riseState.Parameter = Mathf.Lerp(riseState.Parameter, verticalSpeed, 5f * Time.deltaTime);
    }

#if UNITY_EDITOR
    private SerializedProperty m_transitionPointProperty;
    
    public override void EditorPreviewParameterControl(AnimancerComponent animator, Rect previewRect, SerializedObject target, AnimancerState activeState)
    {
        m_transitionPointProperty = target?.FindProperty("m_transitionPoint");

        if (target == null)
        {
            Debug.LogWarning("AirIdleRiseFallAnimation: m_transitionPoint property is null");
            return;
        }
        
        float negativeTransitionPoint = -m_transitionPointProperty.floatValue;
        float positiveTransitionPoint = m_transitionPointProperty.floatValue;
        
        var previewControlRect = new Rect(
            previewRect.x + 10,
            previewRect.yMax * 0.7f,
            previewRect.width * 0.35f,
            previewRect.height * 0.25f);

        SirenixEditorGUI.DrawRoundRect(previewControlRect, Color.gray1, 4f);
        
        GUILayout.BeginArea(previewControlRect);
        {
            target.Update();
            
            var state = activeState as LinearMixerState;
            GUILayout.Label("Air Idle Rise/Fall Transition Point", SirenixGUIStyles.WhiteLabelCentered);
            SirenixEditorGUI.HorizontalLineSeparator(Color.black, 2);
            //EditorGUILayout.MinMaxSlider("Transition Point",
            //    ref negativeTransitionPoint,
            //    ref positiveTransitionPoint,
            //    -10f, 10f);
            //
            m_transitionPointProperty.floatValue = EditorGUILayout.Slider("Transition Point:", m_transitionPointProperty.floatValue, 0.1f, 10f);
            
            // Set preview state thresholds
            state.SetThresholds(new []{-m_transitionPointProperty.floatValue, m_transitionPointProperty.floatValue});
            
            GUILayout.FlexibleSpace();
            SirenixEditorGUI.HorizontalLineSeparator(Color.rebeccaPurple, 2);
            GUILayout.FlexibleSpace();

            DoPreviewStateParameterControl(state);

            target.ApplyModifiedProperties();
        }
        GUILayout.EndArea();
        return;
        
        // Set the transition point properly
        target.Update();
        negativeTransitionPoint = Mathf.Abs(negativeTransitionPoint);
        positiveTransitionPoint = Mathf.Abs(positiveTransitionPoint);
        
        // Select based on which one changed
        if (!m_transitionPointProperty.floatValue.Equals(negativeTransitionPoint))
            m_transitionPointProperty.floatValue = negativeTransitionPoint;
        else if (!m_transitionPointProperty.floatValue.Equals(negativeTransitionPoint))
            m_transitionPointProperty.floatValue = positiveTransitionPoint;
        target.ApplyModifiedProperties();
    }
#endif    
}