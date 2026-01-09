using FS.CameraSystem;
using FS.Math;
using UnityEngine;

public class CameraLookAhead : CameraBehavior
{
    private PhysicsController m_physics;
    protected override void Initialize()
    {
        // Inherit all camera parameters except pivot
        InheritedCameraValues = CameraBehaviorInheritance.All; // We inherit pivot, we just add a delta to it representing the look ahead
        BehaviorType = CameraBehaviorType.PostProcess;

        m_physics = m_cameraController.GetComponent<PhysicsController>();
        
        m_cameraVector.Pivot = m_cameraController.PlayerPivot;
    }

    public override bool ShouldActivate() => true;

    private Vector3 m_previousPosition;

    protected override void OnCameraActivated()
    {
        m_previousPosition = m_physics.Position;
    }

    private Vector2 m_screenSpaceVelocity;
    private void UpdateScreenSpaveVelocity()
    {
        var wsVel = m_physics.Velocity;
        //wsVel = (m_physics.Position - m_previousPosition) / Time.fixedDeltaTime; TODO: During hit-stop, velocity is maybe not zeroed out but position is not updated, so this gives a wrong result
        // Project the velocity onto the camera's forward vector
        var camRot = m_cameraVector.Rotation.ToQuaternion();
        var camRight = camRot * Vector3.right;
        var camUp = camRot * Vector3.up;
        
        // Project the velocity onto the camera's right and up vectors
        var velRight = wsVel.Dot(camRight);
        var velUp = wsVel.Dot(camUp);
        var targetVel = new Vector2(velRight, velUp) / 4;

        float speedUpLerpSpeed = 0.3f * Time.deltaTime;
        float slowDownLerpSpeed = 2f * Time.deltaTime;
        
        m_screenSpaceVelocity.x = Mathf.Lerp(m_screenSpaceVelocity.x, targetVel.x, 
            Mathf.Abs(m_screenSpaceVelocity.x) > Mathf.Abs(targetVel.x) ? slowDownLerpSpeed : speedUpLerpSpeed);
        m_screenSpaceVelocity.y = Mathf.Lerp(m_screenSpaceVelocity.y, targetVel.y, 
            Mathf.Abs(m_screenSpaceVelocity.y) > Mathf.Abs(targetVel.y) ? slowDownLerpSpeed : speedUpLerpSpeed);
        
        // Clamp each axis to 1
        m_screenSpaceVelocity.x = Mathf.Clamp(m_screenSpaceVelocity.x, -1f, 1f);
        m_screenSpaceVelocity.y = Mathf.Clamp(m_screenSpaceVelocity.y, -1f, 1f);
        //m_screenSpaceVelocity = Vector2.ClampMagnitude(m_screenSpaceVelocity, 1f);
    }
    
    public override void UpdateCamera(in CameraOrbitVector inOrbitVector)
    {
        UpdateScreenSpaveVelocity();
        m_cameraVector.Pivot = m_cameraController.PlayerPivot + m_screenSpaceVelocity.x * CameraRight + m_screenSpaceVelocity.y * CameraUp;
        m_previousPosition = m_physics.Position;
    }
}