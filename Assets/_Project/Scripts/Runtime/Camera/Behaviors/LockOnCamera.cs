using Drawing;
using FS.CameraSystem;
using FS.CombatSystem;
using FS.Math;
using UnityEngine;

public class LockOnCamera : CameraBehavior
{
    private LockOnController m_lockOn;

    protected override void Initialize()
    {
        m_lockOn = m_cameraController.GetComponent<LockOnController>();
        
        BlendInParams = CameraBlendParams.Default;
        BlendOutParams = CameraBlendParams.Default;
        
        InheritedCameraValues = ~(CameraBehaviorInheritance.Pivot | CameraBehaviorInheritance.Distance);
    }

    public override bool ShouldActivate() => m_lockOn && m_lockOn.IsLockOnActive;

    [SerializeField, Range(0, 1)] private float m_pivotCenterPct = 0.5f;
    [SerializeField, Range(0, 10)] private float m_minDistance = 3;
    [SerializeField, Range(0, 20)] private float m_maxDistance = 10;
    
    [SerializeField, Range(0, 1)] private float m_sideFactor = 0.7f;
    [SerializeField, Range(0, 1)] private float m_heightFactor = 0.3f;
    
    private Quaternion m_targetRotation;
    private Vector3 m_focusPoint;
    
    protected override void OnCameraActivated()
    {
        m_targetRotation = m_cameraController.transform.rotation;
        m_cameraVector.Pivot = m_cameraController.m_cameraVector.Pivot;
    }

    private float pivotAlpha;

    public override void UpdateCamera(in CameraOrbitVector inOrbitVector)
    {
        if (!m_lockOn.IsLockOnActive) return;

        var playerPos = m_cameraController.PlayerPivot;//inOrbitVector.Pivot;
        var targetTransform = m_lockOn.CurrentLockOnTarget.transform;
        
        float playerDistToCamera = (playerPos - m_cameraController.CameraState.m_position).Dot(m_cameraController.CameraState.m_rotation * Vector3.forward);
        float targetDistToCamera = (targetTransform.position - m_cameraController.CameraState.m_position).Dot(m_cameraController.CameraState.m_rotation * Vector3.forward);
        float whosCloser = playerDistToCamera < targetDistToCamera ? 0f : 1f; // 1 if player is closer, 0 if target is closer
        float targetPivotAlpha = Mathf.Lerp(0.3f, 0.7f, whosCloser);
        float pivotOffsetAlpha = Mathf.Lerp(0.1f, -0.1f, whosCloser); // Offset towards the player if player is closer, towards target if target is closer
        // Pivot is some point between the player and the target, based on m_pivotCenterPct
        //float pivotAlpha Basically shift between closer to player or closer to target based on which is closer to camera
        pivotAlpha = Mathf.Lerp(pivotAlpha, targetPivotAlpha, 1f * Time.deltaTime);
        var pivot = Vector3.Lerp(playerPos, targetTransform.position, pivotAlpha); // Pivot closer to the player
        var lookAtTarget = Vector3.Lerp(playerPos, targetTransform.position, pivotAlpha + pivotOffsetAlpha); // Look at the midpoint between player and target
        m_cameraVector.Pivot = pivot;

        // Adjust distance to keep both player and target in view
        float distanceToTarget = Vector3.Distance(playerPos, targetTransform.position);
        float cameraDistance = Mathf.Clamp(distanceToTarget, inOrbitVector.Distance, m_maxDistance);
        m_cameraVector.Distance = Mathf.Lerp(m_cameraVector.Distance, cameraDistance, 5f * Time.deltaTime);
        
        // Lerp rotation to look at target if no input is provided
        float lookAtAlpha = Mathf.Clamp01(m_cameraController.TimeSinceLastInput / 3f);

        //var forwardDir = (lookAtTarget - m_cameraController.CameraState.m_position).normalized.ProjectOnPlane(Vector3.up);
        //Quaternion targetRotation = Quaternion.LookRotation(forwardDir + 0.15f * Vector3.down, m_cameraController.Basis * Vector3.up);
        //m_targetRotation = Quaternion.Slerp(m_targetRotation, targetRotation, 5f * Time.deltaTime);
        
        m_targetRotation = Quaternion.Slerp(m_targetRotation, CalculateIdealRotation(playerPos, targetTransform.position, lookAtTarget), 5f * Time.deltaTime);
        
        // If we're too close to the player, dampen the rotation lerp as it can cause really big rotation changes over short distances
        float distToTarget = Vector3.Distance(playerPos, targetTransform.position);
        float lerpDistDampen = Mathf.Clamp01((distToTarget-4f) / 2f);
        m_cameraVector.Rotation = EulerAngles.Lerp(m_cameraVector.Rotation, m_targetRotation, lerpDistDampen * lookAtAlpha * 5f * Time.deltaTime);
    }


    private Quaternion CalculateIdealRotation(Vector3 playerPos, Vector3 targetPos, Vector3 pivot)
    {
        // Line intersecting the pivot creating a -30 or 30 degree angle aiming towards 
        float aimOffset = 15;
        
        var playerViewportPos = m_cameraController.m_camera.WorldToViewportPoint(playerPos);
        if (playerViewportPos.x < 0.5) aimOffset *= -1;
        
        var toTarget = (targetPos - pivot).normalized;
        var rotationAxis = (toTarget).Cross(m_cameraController.CameraState.m_rotation * Vector3.up).Cross(toTarget);
        Quaternion forwardRotation = Quaternion.AngleAxis(aimOffset, rotationAxis);

        return Quaternion.LookRotation(forwardRotation * toTarget - 0.1f * Vector3.up);
        
        return Quaternion.identity;
    }

    public override void OnDrawCameraGizmos(CameraBehaviorController camera)
    {
        if (!m_lockOn.IsLockOnActive) return;

        var targetTransform = m_lockOn.CurrentLockOnTarget.transform;
        var pivot = m_cameraVector.Pivot;
        
        // Draw the pivot point
        Draw.ingame.WireSphere(pivot, 0.1f, Color.yellow);
        
        // Draw the target point
        Draw.ingame.WireSphere(targetTransform.position, 0.1f, Color.red);
        Draw.ingame.WireSphere(camera.PlayerPivot, 0.1f, Color.blue);
        
        // Draw the line from pivot to target
        Draw.ingame.Line(camera.PlayerPivot, targetTransform.position, Color.black);
        
        // Draw ideal rotation arrow
        var idealRotation = CalculateIdealRotation(camera.PlayerPivot, targetTransform.position, pivot);
        Draw.ingame.Arrow(pivot, pivot + idealRotation * Vector3.forward, Color.red);
    }
}