using System.Linq;
using FS.CameraSystem;
using FS.Math;
using PrimeTween;
using TimeUtils;
using UnityEditor;
using UnityEngine;

[AddComponentMenu("Free Skies/Camera/Simple 2D Camera")]
public class VCamMode_Simple2D : VirtualCameraMode
{
    [Header("Camera Positioning")]
    public float Distance;
    [Range(0, 1)] public float SmoothDampTime;
    public Vector2 Offset;
    public Vector2 VerticalDeadZone;

    [Header("Camera Aim")] public EulerAngles RotationOffset;
    
    public bool lockMovement = true;
    private PhysicsController m_physics;

    public Vector3 CamForward => transform.right;
    public Vector3 CamRight => transform.forward;
    public Vector3 CamUp => transform.up;

    public Vector3 TargetFollowPoint
    {
        get
        {
            var follow = m_physics.transform.position;
            follow += Offset.x * CamRight * Mathf.Sign(m_physics.transform.forward.Dot(CamRight)) + Offset.y * CamUp;
            return follow;
        }
    }
    
    private Vector3 m_currentFollowTarget;
    private Vector3 m_dampingSpeed;
    
    protected override void OnCameraActivated()
    {
        m_cameraOwner.TryGetComponent(out m_physics);
        if (lockMovement && m_physics)
        {
            // TODO: Add 'constraint' feature to physics controller for adding simple planar constraints
            m_physics.RegisterVelocityPostProcessModifier((physics) => !m_camera.enabled, (ref Vector3 velocity) =>
            {
                var planeAxis = transform.right.ProjectOnPlane(Vector3.up);
                velocity = velocity.ProjectOnPlane(planeAxis).normalized * velocity.magnitude;
            });
        }
        
        m_currentFollowTarget = TargetFollowPoint;
    }
    
    public override void UpdateCamera()
    {
        var planeAxis = transform.right.ProjectOnPlane(Vector3.up);

        FinalizeFollowPosition();

        cameraState.m_position = m_currentFollowTarget - planeAxis * Distance;
        cameraState.m_rotation = Quaternion.Slerp(cameraState.m_rotation,
            Quaternion.LookRotation(planeAxis, Vector3.up) * RotationOffset.ToQuaternion(), 10f * Time.deltaTime);

        DrawCameraModeGizmos();
    }

    private void DrawCameraModeGizmos()
    {
    }

    protected override void OnCameraDeactivated()
    {}
    
    private void FinalizeFollowPosition()
    {
        ApplyVerticalDeadZone();

        ApplyLookAhead();

        ApplyDamping();
    }

    private void ApplyVerticalDeadZone()
    {
        // We've got the current target and goal target, if grounded lerp the Y
        if (m_physics.State != PhysicsState.Air)
        {
            float currentY = m_currentFollowTarget.y;
            float goalY = TargetFollowPoint.y;
            
            m_currentFollowTarget.y = Mathf.Lerp(currentY, goalY, Easing.Evaluate(m_physics.m_timeSinceFoundGround/2.5f, Ease.OutQuad));
        }
        else
        {
            // Keep them within a certain vertical deadzone unless we surpass it
            float deltaY = TargetFollowPoint.y - m_currentFollowTarget.y;
            float yOffset = 0;
            if (deltaY > VerticalDeadZone.y) yOffset = deltaY - VerticalDeadZone.y;
            else if (deltaY < VerticalDeadZone.x)  yOffset = deltaY - VerticalDeadZone.x;
            m_currentFollowTarget.y += yOffset;
        }
    }

    private float lookAheadAmount;
    private void ApplyLookAhead()
    {
        float lookaheadSign = Mathf.Sign(m_physics.transform.forward.Dot(CamRight));
        if (Mathf.Abs(lookaheadSign) <= 0f) return;
        // Need a clear "planar direction" since this cam would support rotating a bit
        lookAheadAmount = Mathf.Lerp(lookAheadAmount, lookaheadSign * 4f, Time.deltaTime);
    }

    private void ApplyDamping()
    {
        float maxLagDistance = 3;
        var targetPoint = TargetFollowPoint.WithY(m_currentFollowTarget.y);
        //m_currentFollowTarget = targetPoint + CamRight * lookAheadAmount;
        //return;
        m_currentFollowTarget = Vector3.SmoothDamp(m_currentFollowTarget, targetPoint, ref m_dampingSpeed, SmoothDampTime);
        // Clamp to max distance
        Vector3 toTarget = targetPoint - m_currentFollowTarget;
        if (toTarget.magnitude > maxLagDistance)
        {
            m_currentFollowTarget = targetPoint - toTarget.normalized * maxLagDistance;
        }
    }
}