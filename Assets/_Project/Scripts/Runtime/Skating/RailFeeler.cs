using System;
using Drawing;
using FS.GameplayActions;
using FS.Math;
using FS.RuntimeDebug;
using TimeUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using ISplineProvider = FS.MeshProcessing.ISplineProvider;

public class RailFeeler : MonoBehaviour, IDebugProvider
{
    [SerializeField, Range(0, 1)] private float m_radius = 0.5f;
    [SerializeField] private float m_zipGrindOffset = 0f;
    [SerializeField, Range(0, 10)] private float m_railSwapDistance = 4;

    private Collider[] m_colliders = new Collider[8];
    private RaycastHit[] m_railSwapHits = new RaycastHit[8];
    private ISplineProvider m_railProvider;
    private ISplineProvider m_zipGrindProvider;

    private ISplineProvider m_leftRailProvider;
    private ISplineProvider m_rightRailProvider;

    private Vector3 m_leftSwapPoint;
    private Vector3 m_rightSwapPoint;

    private PhysicsController m_physics;
    private RailGrindAction m_railGrindAction;

    private Vector3 m_railQueryStart => m_railGrindAction is {IsActive: true} ? m_railGrindAction.RailPosition : transform.position;

    private Vector3 m_zipGrindVectorOffset => Application.isPlaying
        ? m_physics.HeadPositionOffset + m_physics.transform.up * m_zipGrindOffset
        : transform.position + transform.up * (1.6f + m_zipGrindOffset);

    private void Awake()
    {
        var ac = GetComponentInParent<ActionController>();
        m_railGrindAction = ac.GetActionSet<SkatingActionSet>().RailGrind; // no null checks, u must have this if u have a rail feeler lol
        m_physics = GetComponentInParent<PhysicsController>();
    }

    private void FixedUpdate()
    {
        m_railProvider = null;
        m_zipGrindProvider = null;

        // Check zip-grind rail swap/provider too
        if (m_railGrindAction is { IsActive: true })
        {
            switch (m_railGrindAction.GrindOrientation)
            {
                case RailGrindAction.OrientationState.Regular:
                    m_railProvider = m_railGrindAction.RailSpline;
                    break;
                case RailGrindAction.OrientationState.Zipline:
                    m_zipGrindProvider = m_railGrindAction.RailSpline;
                    break;
            }
        }
        else
        {
            bool result = TryQueryRailGrind(Vector3.zero, out m_railProvider) || TryQueryRailGrind(
                m_zipGrindVectorOffset, out m_zipGrindProvider);
        }

        m_leftRailProvider = null;
        m_rightRailProvider = null;
        if (m_railProvider != null && m_railGrindAction is { IsActive: true })
        {
            RailSwapSweep(out m_rightRailProvider, out m_rightSwapPoint, transform.right);
            RailSwapSweep(out m_leftRailProvider, out m_leftSwapPoint, -transform.right);
        }
    }

    private bool TryQueryRailGrind(Vector3 offset, out ISplineProvider railSpline)
    {
        railSpline = null;
        
        if (Physics.OverlapSphereNonAlloc(m_railQueryStart + offset, m_radius, m_colliders,
                1 << PhysicsLayers.RailGrind) > 0)
        {
            foreach (var possibleRail in m_colliders)
            {
                var splineProvider = GetSplineProvider(possibleRail);
                if (splineProvider != null)
                {
                    railSpline = splineProvider;
                    break;
                }
            }
        }

        return railSpline != null;
    }

    private void RailSwapSweep(out ISplineProvider splineProvider, out Vector3 nearestPoint, Vector3 direction)
    {
        splineProvider = null;
        nearestPoint = Vector3.zero;

        // Start the sweep offset and check directions later (counter fix to having a big radius)
        Ray castRay = new Ray(m_railQueryStart - direction * m_radius, direction);
        
        // Capsule params (we do a capsule sweep of a long capsule 2x player height
        var cp1 = m_railQueryStart - m_physics.CapsuleHeight * Vector3.up;
        var cp2 = m_railQueryStart + m_physics.CapsuleHeight * Vector3.up;
        var cpRad = m_physics.CapsuleRadius;
        
        // Capsule cast is better than sphere case here, small radius but tall-ish
        //int numHits = Physics.SphereCastNonAlloc(castRay, 0.36f, m_railSwapHits, m_railSwapDistance + m_radius, 1 << PhysicsLayers.RailGrind);
        int numHits = Physics.CapsuleCastNonAlloc(cp1, cp2, cpRad, direction, m_railSwapHits, m_railSwapDistance, 1 << PhysicsLayers.RailGrind);
        if (numHits > 0)
        {
            // Find the nearest rail swap hit that is not the current rail provider
            float nearestDistance = float.MaxValue;
            for (int n = 0; n < numHits; n++)
            {
                var hit = m_railSwapHits[n];
                
                // Not valid collider
                if (!hit.collider) continue;
                
                var maybeSplineProvider = GetSplineProvider(hit.collider);
                // Its the same rail provider or null
                if (maybeSplineProvider == null || maybeSplineProvider == m_railProvider) continue;

                // Could be opposite side given our ray starting point
                var hitDirection = hit.point - m_railQueryStart;
                if (hitDirection.Dot(direction) < 0) continue;
                
                float distance = Vector3.Distance(hit.point, m_railQueryStart);
                // If the distance is greater than the nearest found or too close, skip
                //if (distance < 1.5f * m_radius) continue; // Too close to the rail feeler
                if (distance >= nearestDistance) continue;

                splineProvider = maybeSplineProvider;
                nearestPoint = hit.point;
                nearestDistance = distance;
            }
        }
    }

    public bool TryGetRailSpline(out ISplineProvider spline)
    {
        spline = null;
        if (m_railProvider == null && m_zipGrindProvider == null) return false;
        spline = m_railProvider ?? m_zipGrindProvider;
        return true;
    }

    public RailGrindAction.OrientationState RailOrientationState => m_zipGrindProvider == null
        ? RailGrindAction.OrientationState.Regular
        : RailGrindAction.OrientationState.Zipline;

    public bool TryGetRailSwap(Vector3 dir, out ISplineProvider spline, out Vector3 swapPoint)
    {
        spline = null;
        swapPoint = Vector3.zero;
        if (dir == Vector3.zero) return false;
        if (m_leftRailProvider == null && m_rightRailProvider == null) return false;

        float dirAngle = Vector3.Angle(dir.normalized, transform.right);
        
        if (dirAngle < 35 && m_rightRailProvider != null)
        {
            spline = m_rightRailProvider;
            swapPoint = m_rightSwapPoint;
        }
        else if (dirAngle > (180-35) && m_leftRailProvider != null)
        {
            spline = m_leftRailProvider;
            swapPoint = m_leftSwapPoint;
        }
        else
        {
            return false;
        }
        
        return true;
    }

    private ISplineProvider GetSplineProvider(Collider other)
    {
        if (!other) return null;
        
        var splineProvider = other.GetComponent<ISplineProvider>();
        if (splineProvider == null)
        {
            Debug.LogError($"Rail grind object found without ISplineProvider: {other.name}");
        }
        return splineProvider;
    }

    public string DebugName => "Rail Feeler";
    
    public void OnDebugGUI()
    {
        var ogColor = GUI.backgroundColor;
        GUILayout.BeginHorizontal();

        GUI.backgroundColor = m_leftRailProvider != null ? Color.green : Color.red;
        GUILayout.Button("Left Rail");
        GUI.backgroundColor = m_railProvider != null ? Color.green : Color.red;
        GUILayout.Button("Rail");
        GUI.backgroundColor = m_rightRailProvider != null ? Color.green : Color.red;
        GUILayout.Button("Right Rail");
        
        GUILayout.EndHorizontal();
        GUI.backgroundColor = ogColor;
    }

    public void OnDebugDraw()
    {
        var noHitColor = new Color(0.8f, 0.1f, 0.1f, 1);
        var hitColor = new Color(0.1f, 0.8f, 0.1f, 1f);
        
        Draw.ingame.WireSphere(m_railQueryStart, m_radius, m_railGrindAction == null ? Color.red : Color.white);

        var color = m_railProvider == null ? noHitColor : hitColor;
        Draw.ingame.SolidBox(m_railQueryStart, m_radius, color);
        if (m_railProvider != null)
        {
            Draw.ingame.Label2D(m_railQueryStart + Vector3.up * 2f, $"Rail: {m_railProvider.GetSpline().transform.parent.parent.name}");
        }
        
        color = m_zipGrindProvider == null ? noHitColor : hitColor;
        Draw.ingame.SolidBox(m_railQueryStart + m_zipGrindVectorOffset, m_radius, color);
        if (m_zipGrindProvider != null)
        {
            Draw.ingame.Label2D(m_railQueryStart + m_zipGrindVectorOffset + Vector3.up * 2f, $"ZipGrind: {m_zipGrindProvider.GetSpline().transform.parent.parent.name}");
        }

        var capsuleBottom = m_railQueryStart - transform.up * m_physics.CapsuleHeight;
        var capsuleTop = m_railQueryStart + transform.up * m_physics.CapsuleHeight;
        
        color = m_rightRailProvider == null ? noHitColor : hitColor;
        Draw.ingame.WireCapsule(capsuleBottom + transform.right * m_railSwapDistance, capsuleTop + transform.right * m_railSwapDistance, m_physics.CapsuleRadius, color);
        //Draw.ingame.SolidBox(m_railQueryStart + transform.right * m_railSwapDistance, m_radius, color);
        color = Color.purple;
        if (m_rightRailProvider != null)
        {
            Draw.ingame.Label2D(capsuleTop + transform.right * m_railSwapDistance, $"Swap Rail: {m_rightRailProvider.GetSpline().gameObject.transform.parent.parent.name}");
            Draw.ingame.SolidBox(m_rightSwapPoint, 0.5f, color);
        }
        
        color = m_leftRailProvider == null ? noHitColor : hitColor;
        Draw.ingame.WireCapsule(capsuleBottom - transform.right * m_railSwapDistance, capsuleTop - transform.right * m_railSwapDistance, m_physics.CapsuleRadius, color);
        //Draw.ingame.SolidBox(m_railQueryStart - transform.right * m_railSwapDistance, m_radius, color);
        color = Color.purple;
        if (m_leftRailProvider != null)
        {
            Draw.ingame.Label2D(capsuleTop - transform.right * m_railSwapDistance, $"Swap Rail: {m_leftRailProvider.GetSpline().gameObject.transform.parent.parent.name}");
            Draw.ingame.SolidBox(m_leftSwapPoint, 0.5f, color);
        }
        
        var leftEndPoint = m_leftRailProvider != null ? m_leftSwapPoint : m_railQueryStart - transform.right * m_railSwapDistance;
        var rightEndPoint = m_rightRailProvider != null ? m_rightSwapPoint : m_railQueryStart + transform.right * m_railSwapDistance;
        
        Draw.ingame.DashedLine(m_railQueryStart, leftEndPoint, 0.25f, 0.1f, Color.white);
        Draw.ingame.DashedLine(m_railQueryStart, rightEndPoint, 0.25f, 0.1f, Color.white);
    }
}