using FluffyUnderware.Curvy;
using FS.Rendering;
using Sirenix.OdinInspector;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(TubeRenderer))]
[Icon("SplineTubeEmitter Icon")]
[AddComponentMenu("Free Skies/Effects/SplineTubeEmitter")]
public class SplineTubeEmitter : MonoBehaviour
{
    private TubeRenderer m_tubeRenderer;
    public TubeRenderer TubeRenderer
    {
        get
        {
            if (m_tubeRenderer == null) m_tubeRenderer = GetComponent<TubeRenderer>();
            return m_tubeRenderer;
        }
    }

    [SerializeField, Required, ValidateInput("ValidateSpline"), OnValueChanged("InitSplineBinding")] 
    private CurvySpline m_spline;
    [SerializeField, Range(0.1f, 10f)] 
    private float m_segmentSpacing = 0.5f;

    private void OnEnable()
    {
        InitSplineBinding();
    }

    private void OnDisable()
    {
        CleanupSplineBinding();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // null ref on spline first frame onValidate in playmode
        if (!Application.isPlaying) UpdateTubeRendererToSpline();
    }

    private Matrix4x4 m_prevSplineTransform;
    private void Update()
    {
        if (Application.isPlaying) return;
        if (m_spline == null) return;
        
        if (m_spline.transform.localToWorldMatrix != m_prevSplineTransform) UpdateTubeRendererToSpline();

        m_prevSplineTransform = m_spline.transform.localToWorldMatrix;
    }

#endif

    private bool ValidateSpline(CurvySpline spline)
    {
        // Just using it to cleanup bindings no actual validation is needed
        if (m_spline == null) return true;
        if (m_spline == spline) return true; // same spline
        CleanupSplineBinding(); // True, so cleanup existing binding to that splines refresh
        return true;
    }

    private void InitSplineBinding()
    {
        if (m_spline == null && !TryGetComponent(out m_spline)) return;

        m_spline.OnRefresh.AddListenerOnce(OnSplineUpdated);

#if UNITY_EDITOR
        m_prevSplineTransform = m_spline.transform.localToWorldMatrix;
#endif
        
        UpdateTubeRendererToSpline();
    }

    private void CleanupSplineBinding()
    {
        TubeRenderer.ClearSegments();
        if (m_spline == null && !TryGetComponent(out m_spline)) return;
        m_spline.OnRefresh.RemoveListener(OnSplineUpdated);
    }

    private void OnSplineUpdated(CurvySplineEventArgs e) => UpdateTubeRendererToSpline();

    private void UpdateTubeRendererToSpline()
    {
        if (m_spline == null) return;
        
        // Add points along spline subdivided
        float splineLength = m_spline.Length;
        if (splineLength <= 0f) return;
        
        var numSegments = Mathf.FloorToInt(splineLength / m_segmentSpacing);

        Vector3[] points = new Vector3[numSegments + 1];

        float dist = 0;
        for (int n = 0; n < numSegments; n++)
        {
            Vector3 pos = m_spline.InterpolateByDistance(dist, Space.World);
            points[n] = pos;
            dist = Mathf.Min(splineLength, dist + m_segmentSpacing);
        }
        
        // Always include the exact endpoint
        points[numSegments] = m_spline.InterpolateByDistance(splineLength, Space.World);
        
        TubeRenderer.SetPoints(points);
    }
}