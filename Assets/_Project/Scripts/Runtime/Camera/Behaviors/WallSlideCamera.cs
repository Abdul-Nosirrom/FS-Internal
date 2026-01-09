using FS.CameraSystem;
using FS.GameplayActions;
using FS.Math;
using PrimeTween;
using UnityEngine;

public class WallSlideCamera : CameraBehavior
{
    private WallSlide m_wallSlide;
    private PhysicsController m_physics;
    private Vector3 m_smoothedNormal;
    
    protected override void Initialize()
    {
        m_physics = m_cameraController.GetComponent<PhysicsController>();
        m_wallSlide = m_cameraController.GetComponentInChildren<WallSlide>();//.GetActionSet<TestingActionSet>().WallSlide;

        InheritedCameraValues = ~(CameraBehaviorInheritance.RotationOffset | CameraBehaviorInheritance.Pivot | CameraBehaviorInheritance.RotationOffset);
        
        BlendInParams = CameraBlendParams.Create(0.4f, Ease.InOutQuad, false);
        BlendOutParams = CameraBlendParams.Create(0.3f, Ease.InOutQuad, false);
    }

    public override bool ShouldActivate() => m_wallSlide.IsActive && m_wallSlide.m_state == WallSlide.State.Slide && m_wallSlide.m_timeSinceStarted > 0.25f; // Don't start right away

    protected override void OnCameraActivated()
    {
        m_smoothedNormal = m_wallSlide.WallNormal;
    }

    public override void UpdateCamera(in CameraOrbitVector inOrbitVector)
    {
        if (m_wallSlide.m_state == WallSlide.State.Jump)
        {
            EulerAngles resetRot = new EulerAngles(15f, ZeroYaw, 0);
            m_cameraVector.Rotation = EulerAngles.Lerp(inOrbitVector.Rotation, resetRot, 5f * Time.deltaTime);
            m_cameraVector.RotationOffset.roll = Mathf.Lerp(m_cameraVector.RotationOffset.roll, 0f, 5f * Time.deltaTime);
            m_cameraVector.Pivot = Vector3.Lerp(inOrbitVector.Pivot, m_cameraController.PlayerPivot, 5f * Time.deltaTime); // Slightly offset from the wall
            return;
        }
        
        m_smoothedNormal = Vector3.Slerp(m_smoothedNormal, m_wallSlide.WallNormal, 5f * Time.deltaTime);
        
        Vector3 queryPosition = m_physics.CenterPosition - m_physics.Velocity.normalized * 4f + m_smoothedNormal * 2f;
        Vector3 toVector = (m_physics.CenterPosition - queryPosition).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(toVector, m_physics.UpDirection);

        m_cameraVector.Pivot = Vector3.Lerp(inOrbitVector.Pivot, m_cameraController.PlayerPivot + m_smoothedNormal * 1f, 5f * Time.deltaTime); // Slightly offset from the wall
        m_cameraVector.Rotation = Quaternion.Slerp(inOrbitVector.Rotation.ToQuaternion(), targetRotation, 5f * Time.deltaTime);

        float dutchAngle = 10f;
        float directionSign = Mathf.Sign(m_wallSlide.WallNormal.Dot(m_physics.transform.right));

        m_cameraVector.RotationOffset.roll = Mathf.Lerp(m_cameraVector.RotationOffset.roll, directionSign * dutchAngle, 5f * Time.deltaTime);
    }
}