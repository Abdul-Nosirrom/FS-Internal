using System.Threading.Tasks;
using UnityEngine;
using FS.CameraSystem;
using FS.GameplayActions;
using PrimeTween;

public class CameraController : CameraBehaviorController
{
    public PhysicsController m_physics { get; private set; }
    public ActionController m_actionController { get; private set; }

    private Tween m_recenterTween;
    
    protected override void Awake()
    {
        // TODO: There is some issue with the initialization of the CameraState in which values it picks out from the camera and the fact that the set fov is ignored on the camera controller
        base.Awake();
        
        m_physics = GetComponent<PhysicsController>();
        m_actionController = GetComponent<ActionController>();
    }

    protected override void SetupBehaviors()
    {
        
        // Action Cams
        AddBehavior<CameraBehavior_DefaultOrbit>("Default Orbit"); // Initializes Pivot & accumulates rotation deltas
        
        // General adjustments & background behaviors
        AddBehavior<CameraBehavior_PitchFraming>("Pitch Framing"); // Adjusts the pitch when no input
        AddBehavior<CameraBehavior_FocusMoveDirection>("Focus Move Direction"); // Adjusts the yaw when no input
        
        AddBehavior<CameraLookAhead>("Camera Look Ahead"); // Adjusts the pivot based on movement direction
        AddBehavior<CameraBehavior_VerticalDeadzone>("Vertical DeadZone"); // Adjusts the pivot based on vertical direction
        
        // Action cams
        AddBehavior<SkateCamera>("Skate Cams"); // TODO: Temporarily have to make it lower priority given that pitch limits aren't applied correctly (this is because they get applied in the RotationSetter so early behaviors clamp prev frame rotations)
        AddBehavior<WallSlideCamera>("Wall Slide Cams");
        
        // Other cams
        AddBehavior<LockOnCamera>("Lock On");
    }


    protected override void LateUpdate()
    {
        if (m_physics.IsGrounded && m_physics.Ground.CompareTag(GameTags.CameraBasis))
        {
            var targetRot = Quaternion.FromToRotation(Vector3.up, m_physics.GroundNormal);
            m_basis = Quaternion.Slerp(m_basis, targetRot, 5f * Time.deltaTime);
        }
        else
        {
            var targetRot = Quaternion.FromToRotation(Vector3.up, -m_physics.GravityDir);
            m_basis = Quaternion.Slerp(m_basis, targetRot, 10f * Time.deltaTime);
        }
        base.LateUpdate();
        
        // Override the yaw if recentering
        if (m_input.GetButton(GameInput.CameraRecenter) && RecenterCamera())
        {
            m_input.ConsumeInput(GameInput.CameraRecenter);
        }
    }

    public async Task<bool> RecenterCameraAfterDuration(float recenterDuration = 0.2f, float delay = 0f)
    {
        await Awaitable.WaitForSecondsAsync(delay);
        return RecenterCamera(recenterDuration);
    }
    
    public bool RecenterCamera(float duration = 0.2f)
    {
        if (m_recenterTween.isAlive) return false;
        m_recenterTween = Tween.Custom(m_cameraVector.Rotation.yaw, m_cameraVector.Rotation.yaw, duration, OnPerformingRecentering, Ease.OutQuad);
        return true;
    }

    private void OnPerformingRecentering(float val)
    {
        m_cameraVector.Yaw = Mathf.LerpAngle(val, ZeroYaw, m_recenterTween.interpolationFactor);
    }

    // private void Update()
    // {
    //     if (m_inputSystem.GetButton(4))
    //     {
    //         m_inputSystem.ConsumeInput(4);
    //         m_cameraSystem.AddCameraShake(CameraShake.CreateShake(strength: CameraShakeStrength.Heavy, duration: CameraShakeDuration.Short));
    //     }
    // }
}