using System.Collections;
using FluffyUnderware.Curvy;
using FS.Attributes;
using FS.CombatSystem;
using FS.Math;
using FS.Utility;
using Sirenix.OdinInspector;
using UnityEngine;

[LevelDesignCategory("Level Objects")]
public class BungeeRope : LevelObjectBase
{
    [SerializeField] private Material m_bungeeMaterial;
    [SerializeField, Required, ReadOnly] private CurvySpline m_spline;
    [SerializeField, Required, ChildGameObjectsOnly] private BoxCollider m_bungeeTrigger;
    [SerializeField, Required, ChildGameObjectsOnly] private TargetingQuery m_homingTarget;
    [SerializeField, Range(1, 25)] public float m_length;
    [SerializeField] private AnimationCurve m_bungeeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve m_resettleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private bool m_isStrongBounce;
    private Context m_activeContext;
    
#if UNITY_EDITOR
    public void OnValidate()
    {
        if (Application.isPlaying) return; // Only do this in editor (and not in OnEnable, which is called on play mode start)
            
        if (m_spline == null)
        {
            m_spline = CurvySpline.Create();
            m_spline.gameObject.name = "BungeeRopeSpline";
            m_spline.transform.SetParent(transform, false);
        }

        if (m_spline.ControlPointCount != 3) m_spline.SetControlPointCount(3);

        m_spline.ControlPointsList[0].transform.localPosition = Vector3.zero;
        m_spline.ControlPointsList[1].transform.localPosition = Vector3.right * (m_length / 2f);
        m_spline.ControlPointsList[2].transform.localPosition = Vector3.right * m_length;
        
        // Ensure spline is set to non-editable
        m_spline.gameObject.hideFlags = HideFlags.NotEditable;
        foreach (var child in m_spline.ControlPointsList) child.gameObject.hideFlags = HideFlags.NotEditable;


        if (m_bungeeTrigger)
        {
            m_bungeeTrigger.size = new Vector3(m_length, 0.5f, 0.5f);
            m_bungeeTrigger.center = new Vector3(m_length / 2f, 0, 0);
        }
        
        if (m_homingTarget)
            m_homingTarget.transform.localPosition = new Vector3(m_length / 2f, 0, 0);
    }
#endif

    protected override void OnPhysicsActorEnter(Context context)
    {
        if (m_activeContext != null) return; // Already active
        
        m_activeContext = context;
        m_isStrongBounce = false;

        if (m_activeContext.actionController)
        {
            m_isStrongBounce = m_activeContext.actionController.HasActiveAction<SpringStompBounce>();
            m_activeContext.actionController.DisableActions();
        }
        
        // Resettle might be active, just stop it
        StopAllCoroutines();
        // Start by 
        StartInteractionCoroutine(context, BungeeAnimator());
    }

    private IEnumerator BungeeAnimator()
    {
        var constraintPoint = m_spline[1];
        float elapsed = 0f;

        BeginInteraction(m_activeContext);

        // Down-up animation loop
        float downUpDuration = m_isStrongBounce ? 0.5f : 0.3f;
        float downUpHeight = m_isStrongBounce ? 3f : 1.5f;

        try
        {
            var startPos = m_activeContext.physics.Position.ProjectOnPlane(transform.up);
            while (elapsed < downUpDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / downUpDuration);
                float newHeightLocal = downUpHeight * m_bungeeCurve.Evaluate(t); // -1, 1 height curve

                // Update constraint point position
                constraintPoint.transform.localPosition = constraintPoint.transform.localPosition.WithY(newHeightLocal);
                //m_spline.Refresh();

                // Lerp player towards constraint point in first 0.3 s
                var targetPos = constraintPoint.transform.position;
                
                // Interpolate lateral position along local vector3.forward & right
                var targetLateralPos = targetPos.ProjectOnPlane(transform.up);
                var reorientAlpha = Mathf.Clamp01(elapsed / 0.3f);
                targetPos = Vector3.Lerp(startPos, targetLateralPos, reorientAlpha) +
                            targetPos.ProjectOnto(transform.up);
                
                if (m_activeContext.physics.HasLevelObject) m_activeContext.physics.Teleport(targetPos);
                //m_activeContext.physics.Position = targetPos;
                //Vector3.Lerp(m_activeContext.physics.Position, targetPos, 15f * Time.deltaTime);

                yield return Yields.WaitForNextFrame;
            }
        }
        finally
        {
            EndInteraction(m_activeContext);

            m_activeContext.actionController.EnableActions();

            // Launch player
            var launchHeight = m_isStrongBounce ? 14f : 6f;
            ProjectileMotion.LaunchPhysicsControllerToHeight(m_activeContext.physics, launchHeight, out _, out _);
            
            StartCoroutine(BungeeResettleAnimator(constraintPoint, downUpHeight));
        }
    }

    private IEnumerator BungeeResettleAnimator(CurvySplineSegment constraintPoint, float downUpHeight)
    {
        // Player shit is done, but complete resettle first before resetting
        float resettleDuration = 0.8f;
        float elapsed = 0f;
        while (elapsed <= resettleDuration)
        {
            if (elapsed > 0.1f) m_activeContext = null; // reset a bit in for quick retrigger
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / resettleDuration);
            float newHeightLocal = downUpHeight * m_resettleCurve.Evaluate(t); // -1, 1 height curve

            // Update constraint point position
            constraintPoint.transform.localPosition = constraintPoint.transform.localPosition.WithY(newHeightLocal);

            //m_spline.Refresh();
            yield return Yields.WaitForNextFrame;
        }
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(BungeeRope))]
public class BungeeRopeEditor : UnityEditor.Editor
{
    private void OnSceneGUI()
    {
        BungeeRope bungeeRope = (BungeeRope)target;
        
        var handleRot = Quaternion.LookRotation(bungeeRope.transform.right, bungeeRope.transform.up);
        var newLength = FS.MeshProcessing.Editor.HandlesUtility.LinearScaleHandle(bungeeRope.transform.position, handleRot, bungeeRope.m_length);
        if (!Mathf.Approximately(newLength, bungeeRope.m_length))
        {
            UnityEditor.Undo.RecordObject(bungeeRope, "Change Bungee Rope Length");
            bungeeRope.m_length = newLength;
            bungeeRope.OnValidate();
            UnityEditor.EditorUtility.SetDirty(bungeeRope);
        }
    }
}
#endif