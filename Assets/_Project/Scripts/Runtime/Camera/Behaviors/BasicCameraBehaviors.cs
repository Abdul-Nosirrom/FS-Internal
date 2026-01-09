
using FS.CameraSystem;
using FS.GameplayActions;
using FS.Math;
using PrimeTween;
using UnityEditor;
using UnityEngine;

public class CameraBehavior_DefaultOrbit : CameraBehavior
{
    public override bool ShouldActivate() => true;

    private float m_targetDistance;
    private PhysicsController m_physics;
    protected override void Initialize()
    {
        Priority = 0;
        m_physics = m_cameraController.GetComponent<PhysicsController>();
        m_targetDistance = m_cameraController.m_cameraVector.Distance;
    }

    public override void UpdateCamera(in CameraOrbitVector inOrbitVector)
    {
        // TODO: By setting pivot like this, we always override shit comin in from the blending, for behaviors that pop in later
        // this affects them as the pivot they try to read isnt accurate (vertical deadzone is affected if we want to only activate it in air,
        // causing the 'jitter' it gets when reactivating it while its blending out as it tries to reset to the camera controller pivot. Can solve this
        // specific issue i think by handling activation before updates so the orbit vector it gets during activation is the real orbit vector pre manipulation)
        m_cameraVector.Pivot = m_cameraController.PlayerPivot;
        m_cameraVector.Rotation += m_cameraController.LookInputDelta;

        // -1 to 1
        float pitch = m_cameraVector.Rotation.pitch / 90f;
        float targetDist = inOrbitVector.Distance;
        if (pitch < 0) targetDist = 0.25f * targetDist;
        else if (pitch > 0) targetDist = 1.5f * targetDist;

        if (m_physics.IsInSkateAction)
        {
            if (m_physics.Velocity.Dot(m_physics.GravityDir) > 0)
                targetDist = 4; //0.75f * inOrbitVector.Distance; // Falling, push in a bit
            else
                targetDist = 4;// inOrbitVector.Distance; // Raising
        }
        
        m_targetDistance = Mathf.Lerp(m_targetDistance, targetDist, 3f * Time.deltaTime);
        m_cameraVector.Distance = Mathf.Lerp(inOrbitVector.Distance, m_targetDistance, Easing.Evaluate(Mathf.Abs(pitch), Ease.InOutQuad));
        
        // float pitch = m_cameraVector.Rotation.pitch / 90f;
        // float targetDist = m_cameraVector.Distance;
        // if (pitch < 0) targetDist = 0.25f * targetDist;
        // else if (pitch > 0) targetDist = 1.5f * targetDist;
        //
        // if (m_physics.IsInSkateAction)
        // {
        //     if (m_physics.Velocity.Dot(m_physics.GravityDir) > 0)
        //         targetDist = 0.8f * m_cameraVector.Distance; // Falling, push in a bit
        //     else
        //         targetDist = m_cameraVector.Distance; // Raising
        // }
        //
        // m_cameraVector.Distance = Mathf.Lerp(inOrbitVector.Distance, targetDist, Easing.Evaluate(Mathf.Abs(pitch), Ease.InOutQuad));
    }
}

// TODO: The hitch we see w/ basis changes when we do a full loop? This behavior is the cause of it
public class CameraBehavior_FocusMoveDirection : CameraBehavior
{
    protected override void Initialize()
    {
        BehaviorType = CameraBehaviorType.PostProcess;
        m_railGrind = m_cameraController.gameObject.GetComponentInChildren<RailGrindAction>();
    }

    [SerializeField] private float m_focusFadeDuration = 5f;
    private Vector3 m_prevPos = Vector3.zero;
    private RailGrindAction m_railGrind;
    
    public override bool ShouldActivate() => true;
    
    public override void UpdateCamera(in CameraOrbitVector inOrbitVector)
    {
        if (m_prevPos == Vector3.zero) m_prevPos = inOrbitVector.Pivot;

        float focusFade = m_railGrind.IsActive ? 0.1f : m_focusFadeDuration;
        
        // TODO: Review w/ euler conversion
        float orbitFadeFactor = m_railGrind.IsActive ? 1 : Mathf.Clamp01(m_cameraController.TimeSinceLastInput / focusFade);
        
        Vector3 prevFocus = (m_prevPos - m_cameraController.CameraState.m_position).normalized;
        Vector3 newFocus = (inOrbitVector.Pivot - m_cameraController.CameraState.m_position).normalized;

        prevFocus = Vector3.ProjectOnPlane(prevFocus, UpVector);
        newFocus = Vector3.ProjectOnPlane(newFocus, UpVector);

        float yawDelta = Vector3.SignedAngle(newFocus, prevFocus, UpVector);
        m_cameraVector.Rotation = EulerAngles.Lerp(m_cameraVector.Rotation, m_cameraVector.Rotation - new EulerAngles(0, yawDelta, 0), orbitFadeFactor);
        
        m_prevPos = inOrbitVector.Pivot;
    }
}

public class CameraBehavior_PitchFraming : CameraBehavior
{
    protected override void Initialize()
    {
        BehaviorType = CameraBehaviorType.PostProcess;
        m_physics = m_cameraController.gameObject.GetComponent<PhysicsController>();
    }

    [SerializeField] private float m_focusFadeDuration = 5f;
    [SerializeField] private float m_interpSpeed = 2f;
    private PhysicsController m_physics;
    
    public override bool ShouldActivate() => !m_physics.IsInSkateAction; // skate actions want to control their pitch
    
    public override void UpdateCamera(in CameraOrbitVector inOrbitVector)
    {
        // match pitch of the ground (so long as its not vertical)
        float groundDotBasis = 90 - m_physics.transform.forward.SignedAngle(UpVector);
        float targetPitch = 15f;
        if (m_physics.IsGrounded && !m_physics.Ground.CompareLayer(PhysicsLayers.Vert) && groundDotBasis < 70) targetPitch -= groundDotBasis/2;
        
        float orbitFadeFactor = Mathf.Clamp01(m_cameraController.TimeSinceLastInput / m_focusFadeDuration);
        // Default pitch
        m_cameraVector.Pitch = Mathf.LerpAngle(inOrbitVector.Rotation.pitch, targetPitch, m_interpSpeed * orbitFadeFactor * Time.deltaTime);
    }
}