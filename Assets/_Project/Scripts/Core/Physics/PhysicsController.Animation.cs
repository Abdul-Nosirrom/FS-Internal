using Lightbug.CharacterControllerPro.Core;
using UnityEngine;

public partial class PhysicsController
{
    public Vector3 RootMotionDeltaPosition => m_motor.RootMotionDeltaPosition;
    public Vector3 RootMotionDeltaVelocity => m_motor.RootMotionVelocity;
    public Quaternion RootMotionDeltaRotation => m_motor.RootMotionDeltaRotation;

    private CharacterRotationLerper m_rotationSmoother;

    public void SnapVisualRotation()
    {
        if (m_rotationSmoother == null) return;
        m_rotationSmoother.SnapVisualRotationToPhysics();
    }
    
    public void EnableRootMotionRotation()
    {
        m_motor.UseRootMotion = true;
        m_motor.UpdateRootRotation = true;
    }
    
    public void DisableRootMotionRotation()
    {
        m_motor.UseRootMotion = false;
        m_motor.UpdateRootRotation = false;
    }
}