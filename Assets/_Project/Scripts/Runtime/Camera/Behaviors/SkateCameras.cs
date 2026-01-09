using Drawing;
using FS.CameraSystem;
using FS.Math;
using PrimeTween;
using UnityEngine;

public class SkateCamera : CameraBehavior
{
    protected float m_postVertRecenterDuration = 0.75f; // How long it takes to recenter the camera after a vert action
    protected float m_camInterpSpeed = 2.5f; // How quickly the camera interpolates to the target rotation
    
    protected PhysicsController m_physics;
    
    private readonly CameraBlendParams m_vertBlendInParams = CameraBlendParams.Create(0.4f, Ease.InOutQuad, false);
    private readonly CameraBlendParams m_acidDropBlendInParams = CameraBlendParams.Create(0.7f, Ease.InOutQuad, false);
    
    protected override void Initialize()
    {
        InheritedCameraValues =  ~CameraBehaviorInheritance.PitchLimits;
        m_cameraVector.PitchLimits.y = 85;
        Priority = 0;
        BehaviorType = CameraBehaviorType.Regular;
        m_physics = m_cameraController.GetComponent<PhysicsController>();

        BlendInParams = CameraBlendParams.Create(0.4f, Ease.InOutQuad, false);
        BlendOutParams = CameraBlendParams.Create(0.3f, Ease.InOutQuad, false);
    }
    
    public override bool ShouldActivate() 
        => m_physics.m_vert.IsActive 
           || ((m_physics.IsFalling || m_physics.IsGrounded) && m_physics.m_acidDrop.IsActive) 
           || ((m_physics.IsFalling || m_physics.IsGrounded) && m_physics.m_spineTransfer.IsActive)  
           || m_physics.TimeSinceSkateActionEnded < m_postVertRecenterDuration;

    private Quaternion startRotation;
    protected override void OnCameraActivated()
    {
        base.OnCameraActivated();
        startRotation = m_cameraController.m_cameraVector.Rotation.ToQuaternion();
        
        // Longer blend in on acid drop/spine transfer
        if (m_physics.m_acidDrop.IsActive || m_physics.m_spineTransfer.IsActive) BlendInParams = m_acidDropBlendInParams;
        else BlendInParams = m_vertBlendInParams;
    }

    public override void UpdateCamera(in CameraOrbitVector inOrbitVector)
    {
        if (!m_physics.IsGrounded)
        {
            if (m_physics.m_vert.IsActive) DoVertCamera(in inOrbitVector);
            else if (m_physics.m_acidDrop.IsActive || m_physics.m_spineTransfer.IsActive)
                DoVertTransferCamera(in inOrbitVector);
        }
        else DoLandingCamera(in inOrbitVector);
    }
    
    private void DoVertCamera(in CameraOrbitVector inOrbitVector)
    {
        // Don't trigger the camera if we haven't surpassed the skip vert threshold
        //if (!m_physics.m_vert.HasSurpassedSkipVertThreshold) return;
        
        // Basically, we specify where we want our camera to be located (above and slightly behind the vert), and derive the rotation from that
        var vertNormal = m_physics.transform.up;//m_physics.m_vert.m_normal.normalized;
        Quaternion viewRotation = inOrbitVector.Rotation.ToQuaternion();
        Vector3 queryCamLocation = m_physics.transform.position + m_physics.UpDirection - vertNormal * 0.5f;//m_physics.transform.up * 0.25f;
        Vector3 toVector = (m_physics.transform.position - queryCamLocation).normalized;

        var TargetRotation = Quaternion.LookRotation(toVector, m_physics.transform.up);
        TargetRotation = Quaternion.Lerp(viewRotation, TargetRotation, 0.75f * m_camInterpSpeed * Time.deltaTime);
        //TargetRotation = Quaternion.Slerp(startRotation, TargetRotation, m_physics.m_vert.m_timeSinceStarted / 0.5f);

        //var rotationTarget = new EulerAngles(85, ZeroYaw - 180, 0);
        //m_cameraVector.Rotation = EulerAngles.Lerp(m_cameraVector.Rotation, rotationTarget, 0.75f * m_camInterpSpeed * Time.deltaTime);
        //m_cameraVector.Yaw = Mathf.LerpAngle(m_cameraVector.Yaw, ZeroYaw - 180, m_camInterpSpeed * Time.deltaTime);
        //m_cameraVector.Pitch = Mathf.LerpAngle(m_cameraVector.Pitch, 85f, m_camInterpSpeed * Time.deltaTime);

        m_cameraVector.Rotation = TargetRotation;
    }

    private void DoVertTransferCamera(in CameraOrbitVector inOrbitVector)
    {
        bool isSpineTransfer = m_physics.m_spineTransfer.IsActive && m_physics.m_spineTransfer.IsSpineTransfer;
        
        var vertNormal = isSpineTransfer ? m_physics.m_spineTransfer.m_vertInfo.constraintNormal : m_physics.m_acidDrop.m_vertInfo.constraintNormal;
        var targetRotation = m_physics.transform.rotation;

        var upBasis = m_cameraController.UpVector;
        var forward = targetRotation * Vector3.forward;
            
        // We're facing up so we don't wanna tilt the camera up, instead we aim forward in prep for going down
        if (upBasis.Dot(forward) > 0)
        {
            targetRotation = Quaternion.LookRotation(vertNormal, upBasis);
            if (m_physics.m_spineTransfer.IsActive)
                return;
        }

        // If our current rotation aims more down than our target rotation, dont apply the pitch delta!!!!! Prime cause for this cam feeling weird and annoying some times
        var currentForward = m_cameraVector.Rotation.ToQuaternion() * Vector3.forward;
        var targetForward = targetRotation * Vector3.forward;
        // if target-current dot grav is negative, dont apply
        var toNewDir = targetForward - currentForward;
        if (toNewDir.Dot(m_physics.GravityDir) < 0) return;
        
        targetRotation = Quaternion.Lerp(m_cameraVector.Rotation.ToQuaternion(), targetRotation, m_camInterpSpeed * Time.deltaTime);
        m_cameraVector.Rotation = targetRotation;
    }

    private Vector3 prevLoc;
    protected void DoLandingCamera(in CameraOrbitVector inOrbitVector)
    {
        var camUp = inOrbitVector.Rotation.ToQuaternion() * Vector3.up;
        var currentCamUp = (camUp).Dot(-m_physics.GravityDir);
        var playerUp = m_physics.UpDirection.Dot(-m_physics.GravityDir);
        //
        //// Which is most aligned? For example we don't wanna align to player up if we started moving back upwards the QP
        //bool isReversedUp = currentCamUp > playerUp;
        //
        //var targetUp = m_physics.UpDirection;//Vector3.up;//m_physics.UpDirection;
        //targetUp = Vector3.Slerp(camUp, targetUp, 2 * m_camInterpSpeed * Time.deltaTime);
        //
        //var targetRotation = Quaternion.LookRotation(
        //    (inOrbitVector.Rotation.ToQuaternion() * Vector3.forward).ProjectOnPlane(targetUp),
        //    targetUp);
        //
        //if (!isReversedUp || !m_physics.IsGrounded)
        //    m_cameraVector.Rotation = targetRotation;
        
        // If our ground normal
        var targetUp = m_physics.UpDirection;

        var targetPitch = Mathf.Max(Vector3.Angle(targetUp, m_cameraController.Basis * Vector3.up), 15f);// + 15f;
        if (m_physics.TimeSinceSkateActionEnded < 0.1f)
        {
            targetPitch = m_cameraVector.Pitch;
        }
        
        if (targetPitch < m_cameraVector.Pitch)
        {
            m_cameraVector.Pitch = Mathf.LerpAngle(m_cameraVector.Pitch, targetPitch, m_camInterpSpeed * Time.deltaTime);
            //if (m_physics.IsGrounded)
            //    m_cameraVector.Pitch = Mathf.Max(m_cameraVector.Pitch, 15f);
        }
        
        //if ()


        // if (!IsBlendingOut) return;
        // if (m_physics.State == PhysicsState.Air) return;
        // //return;
        //
        // Vector3 targetUp = Vector3.up;//.MostAligned(m_physics.UpDirection, m_physics.transform.up);
        // targetUp = Vector3.Slerp(inOrbitVector.Rotation.ToQuaternion() * Vector3.up, targetUp, 1.5f * m_camInterpSpeed * Time.deltaTime);
        //
        // Quaternion targetRotation = Quaternion.LookRotation((inOrbitVector.Rotation.ToQuaternion() * Vector3.forward).ProjectOnPlane(targetUp), targetUp);
        // m_cameraVector.Rotation = targetRotation;
    }
}