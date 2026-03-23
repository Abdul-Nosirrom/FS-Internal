using Lightbug.CharacterControllerPro.Core;
using UnityEngine;

public partial class PhysicsController
{
    public Vector3 RootMotionDeltaPosition => m_motor.RootMotionDeltaPosition;
    public Vector3 RootMotionDeltaVelocity => m_motor.RootMotionVelocity;
    public Quaternion RootMotionDeltaRotation => m_motor.RootMotionDeltaRotation;

    public Vector3 RootMotionLocalPosition => RootBoneCompensator.AnimationRootLocalPosition;
    public Quaternion RootMotionLocalRotation => RootBoneCompensator.AnimationRootLocalRotation;

    private CharacterRotationLerper m_rotationSmoother;
    private AnimationRootCompensator m_animationRootCompensator;

    public AnimationRootCompensator RootBoneCompensator
    {
        get
        {
            if (m_animationRootCompensator == null)
                m_animationRootCompensator = GetComponentInChildren<AnimationRootCompensator>();
            return m_animationRootCompensator;
        }
    }

    public void SnapVisualRotation()
    {
        if (m_rotationSmoother == null) return;
        m_rotationSmoother.SnapVisualRotationToPhysics();
    }
    
    public void EnableRootMotionPosition()
    {
        m_motor.UseRootMotion = true;
        m_motor.UpdateRootPosition = true;
    }
    
    public void DisableRootMotionPosition()
    {
        m_motor.UseRootMotion = false;
        m_motor.UpdateRootPosition = false;
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