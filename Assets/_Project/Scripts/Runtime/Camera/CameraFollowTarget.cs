using System;
using UnityEngine;

public class CameraFollowTarget : MonoBehaviour
{
    private PhysicsController m_physics;
    private Vector3 m_localPos;

    private Vector3 TargetPosition => m_physics.Position + transform.rotation * m_localPos;
    private Vector3 m_prevPos;
    
    private void Awake()
    {
        m_physics = GetComponentInParent<PhysicsController>();
        m_localPos = transform.localPosition;
        m_prevPos = transform.position;
    }

    private void LateUpdate()
    {
        // TODO: Obviously this is very basic and hard-coded, but implemeneted now, improve later
        //transform.localPosition = transform.InverseTransformVector(m_localPos);
        // Half aligned and half always up
        transform.localPosition = transform.InverseTransformVector(Vector3.up * m_physics.CapsuleHeight/2) +
                                  Vector3.up * m_physics.CapsuleHeight / 2;
        //transform.position = m_physics.transform.position - m_physics.GravityDir * m_localPos.y;
        return;
        transform.position = Vector3.Lerp(m_prevPos, TargetPosition, 10 * Time.deltaTime);
        m_prevPos = transform.position;
        Drawing.Draw.ingame.WireSphere(transform.position, 0.2f);
    }
}