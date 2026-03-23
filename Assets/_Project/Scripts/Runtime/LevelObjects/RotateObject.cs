using System;
using Drawing;
using FS.Attributes;
using FS.Math;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Experimental]
[RequireComponent(typeof(Rigidbody))]
public class RotateObject : MonoBehaviourGizmos
{
    public Rigidbody m_rigidbody;
    
    public float m_angularSpeed = 10f;
    
    /// <summary>
    /// Rotation pivot in local space
    /// </summary>
    public Vector3 m_pivot = Vector3.zero;
    
    /// <summary>
    /// Rotation axis in local space
    /// </summary>
    public Vector3 m_rotationAxis = Vector3.up;

    private void Awake()
    {
        if (m_rigidbody == null) m_rigidbody = GetComponent<Rigidbody>();

        if (m_rigidbody)
        {
            m_rigidbody.isKinematic = true;
            m_rigidbody.centerOfMass = m_pivot;
        }
    }

    private void FixedUpdate()
    {
        var worldSpacePivot = transform.TransformPoint(m_pivot);
        var worldSpaceAxis = transform.TransformDirection(m_rotationAxis).normalized;
    
        var deltaRotation = Quaternion.AngleAxis(m_angularSpeed * Time.fixedDeltaTime, worldSpaceAxis);
        var targetRotation = deltaRotation * m_rigidbody.rotation;
    
        // Rotate the offset by the delta rotation, not the target rotation
        var pivotOffset = m_rigidbody.position - worldSpacePivot;
        var rotatedOffset = deltaRotation * pivotOffset;
    
        m_rigidbody.MovePosition(worldSpacePivot + rotatedOffset);
        m_rigidbody.MoveRotation(targetRotation);
    }
    
#if UNITY_EDITOR
    public override void DrawGizmos()
    {
        if (!Selection.Contains(gameObject)) return;
        
        var worldSpacePivot = transform.TransformPoint(m_pivot);
        var worldSpaceAxis = transform.TransformDirection(m_rotationAxis).normalized;

        using var thickness = Draw.WithLineWidth(3);
        
        Draw.WireSphere(worldSpacePivot, 0.25f, Color.midnightBlue);
        Draw.DashedLine(transform.position, worldSpacePivot, 0.25f, 0.1f);
        Draw.Label2D(worldSpacePivot + Vector3.up, "Rotation Pivot");
        Draw.Arrow(worldSpacePivot, worldSpacePivot + worldSpaceAxis, Color.crimson);
    }
#endif    
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(RotateObject))]
public class RotateObjectEditor : UnityEditor.Editor
{
    private RotateObject m_target;
    private void OnEnable()
    {
        m_target = (RotateObject)target;
    }

    private void OnSceneGUI()
    {
        if (!Selection.Contains(m_target.gameObject)) return;
        
        Undo.RecordObject(m_target, "Modify Rotate Object Pivot");


        bool snapEnabled = EditorSnapSettings.gridSnapEnabled;
        EditorSnapSettings.gridSnapEnabled = false;
        var worldSpacePivot = m_target.transform.TransformPoint(m_target.m_pivot);
        var newWorldSpacePivot = Handles.PositionHandle(worldSpacePivot, m_target.transform.rotation);
        EditorSnapSettings.gridSnapEnabled = snapEnabled;
        
        if (worldSpacePivot != newWorldSpacePivot)
        {
            m_target.m_pivot = m_target.transform.InverseTransformPoint(newWorldSpacePivot);
            m_target.m_pivot = m_target.m_pivot.ProjectOnPlane(m_target.m_rotationAxis);
            if (EditorSnapSettings.gridSnapEnabled)
            {
                var snapIncrement = EditorSnapSettings.move;
                m_target.m_pivot.x = Mathf.RoundToInt(m_target.m_pivot.x / snapIncrement.x) * snapIncrement.x;
                m_target.m_pivot.y = Mathf.RoundToInt(m_target.m_pivot.y / snapIncrement.y) * snapIncrement.y;
                m_target.m_pivot.z = Mathf.RoundToInt(m_target.m_pivot.z / snapIncrement.z) * snapIncrement.z;
                //m_target.m_pivot = Handles.SnapValue(m_target.m_pivot, EditorSnapSettings.move);
            }
            EditorUtility.SetDirty(m_target);
        }
    }
}
#endif
