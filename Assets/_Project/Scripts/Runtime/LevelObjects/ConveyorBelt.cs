using System;
using Drawing;
using FS.MeshProcessing;
using Unity.Collections;
using UnityEngine;

[RequireComponent(typeof(MeshVertexPath))]
public class ConveyorBelt : GroundMover
{
    [Range(-10, 10), SerializeField]
    private float m_conveyorSpeed = 2f;
    
    private GroundEventBroadcaster m_groundEvents;
    private ISplineProvider m_groundSpline;
    private int m_colliderID = -1;

    private void Awake()
    {
        m_groundSpline = GetComponent<ISplineProvider>();
        
        var conveyorCollider = GetComponent<Collider>();
        if (conveyorCollider && m_groundSpline != null)
        {
            conveyorCollider.hasModifiableContacts = true;
            m_colliderID = conveyorCollider.GetInstanceID();
            Physics.ContactModifyEvent += ConveyorContactPhysics;
        }
    }

    private void OnDestroy()
    {
        if (m_colliderID != -1)
            Physics.ContactModifyEvent -= ConveyorContactPhysics;
    }

    private void ConveyorContactPhysics(PhysicsScene physicsScene, NativeArray<ModifiableContactPair> contactPairs)
    {
        foreach (var pair in contactPairs)
        {
            if (pair.colliderInstanceID != m_colliderID && pair.otherColliderInstanceID != m_colliderID)
                continue;

            if (pair.contactCount == 0)
                continue;

            var contact = pair.GetPoint(0); // We just do the first contact point for simplicity
            m_groundSpline.GetNearestPoint(contact, out var pos, out var time);
            var tangent = m_groundSpline.EvaluateTangent(time).normalized;
            for (int i = 0; i < pair.contactCount; i++)
                pair.SetTargetVelocity(i, tangent * m_conveyorSpeed);
        }
    }

    public override Vector3 GetGroundVelocity(PhysicsController physics, Vector3 contactPoint)
    {
        m_groundSpline.GetNearestPoint(contactPoint, out var pos, out var time);
        var tangent = m_groundSpline.EvaluateTangent(time).normalized;
        return tangent * m_conveyorSpeed;
    }

    public override Quaternion GetGroundRotation(PhysicsController physics, Vector3 contactPoint)
    {
        m_groundSpline.GetNearestPoint(contactPoint, out var pos, out var time);
        var tangent = m_groundSpline.EvaluateTangent(time).normalized;
        var upVector = m_groundSpline.EvaluateUpVector(time).normalized;
        return Quaternion.LookRotation(tangent, upVector);
    }

#if UNITY_EDITOR
    public override void DrawGizmos()
    {
        //if (!Selection.Contains(gameObject)) return;
        if (m_groundSpline == null && !TryGetComponent(out m_groundSpline)) return;
        
        using var thickness = Draw.WithLineWidth(2);
        
        float stepSize = 3f;
        float dist = 0f;

        var spline = m_groundSpline.GetSpline();
        if (spline == null || spline.Count == 0) return;
        
        // Display animating arrows that move along the spline to indicate direction and speed
        while (dist < m_groundSpline.GetLength())
        {
            float time = dist / m_groundSpline.GetLength();
            var tangent = m_groundSpline.EvaluateTangent(time).normalized;
            var pos = m_groundSpline.EvaluatePosition(time);
            
            pos += tangent * (Time.realtimeSinceStartup * m_conveyorSpeed % (stepSize));
            m_groundSpline.GetNearestPoint(pos, out pos, out time);
            
            tangent = m_groundSpline.EvaluateTangent(time).normalized;
            pos = m_groundSpline.EvaluatePosition(time) + m_groundSpline.EvaluateUpVector(time).normalized * 0.25f;

            var startPos = pos;
            var endPos = startPos + tangent * 1f * Math.Sign(m_conveyorSpeed);

            Draw.Arrow(startPos, endPos, Mathf.Sign(m_conveyorSpeed) < 0 ? Color.red : Color.deepSkyBlue);
            dist += stepSize;
        }
    }
#endif
}