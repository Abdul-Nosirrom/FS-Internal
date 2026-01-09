using UnityEngine;
using Drawing;
using FS.CameraSystem;
using FS.GameplayActions;
using FS.Math;
using PrimeTween;
using UnityEditor;

public class CameraBehavior_VerticalDeadzone : CameraBehavior
{
    private PhysicsController m_physics;
    private ActionController m_actionController;
    protected override void Initialize()
    {
        // Inherit all camera parameters except pivot
        InheritedCameraValues = ~CameraBehaviorInheritance.Pivot;
        BehaviorType = CameraBehaviorType.PostProcess;
        Priority = 10;
        
        m_physics = m_cameraController.GetComponent<PhysicsController>();
        m_actionController = m_cameraController.GetComponent<ActionController>();

        m_cameraVector.Pivot = m_cameraController.PlayerPivot;
        
        //BlendInParams = CameraBlendParams.Create(0.6f, Ease.InOutQuad, false);
        BlendOutParams = CameraBlendParams.Create(0.6f, Ease.OutQuad, false);
    }
    
    private Vector3 m_topPos => m_cameraVector.Pivot + UpVector * m_topLimit;
    private Vector3 m_bottomPos => m_cameraVector.Pivot + UpVector * m_bottomLimit;
    
    private float m_topLimit = 2f;
    private float m_bottomLimit = -1f;



    public override bool ShouldActivate() => ShouldApplyDeadZone;//!m_physics.IsInSkateAction;


    private bool ShouldApplyDeadZone => m_physics.State == PhysicsState.Air;// && !m_physics.IsInSkateAction;

    protected override void OnCameraActivated()
    {
        m_cameraVector.Pivot = m_cameraController.m_cameraVector.Pivot;
        //EditorApplication.isPaused = true;
    }

    public override void UpdateCamera(in CameraOrbitVector inOrbitVector)
    {
        var lateralPivot = inOrbitVector.Pivot.ProjectOnPlane(UpVector); // We always inherit lateral/never touch it
        var verticalPivot = inOrbitVector.Pivot.Dot(UpVector);

        var footVertical = verticalPivot;//m_physics.CenterPosition.Dot(UpVector); // Do not let the center position go below the bottom limit (as we rotate center translates)

        var ourVerticalPivot = m_cameraVector.Pivot.Dot(UpVector);

        var topLimitPos = m_topPos.Dot(UpVector);
        var bottomLimitPos = m_bottomPos.Dot(UpVector);

        float pivotDelta = 0f;
        // If vertical pivot is in-between bottom/top limit, we use our existing vertical positioning, otherwise we clamp between
        if (verticalPivot > topLimitPos) pivotDelta = verticalPivot - topLimitPos; // How much above the top limit are we? Add this to our current proper pivot
        else if (footVertical < bottomLimitPos) pivotDelta = footVertical - bottomLimitPos;

        m_cameraVector.Pivot = lateralPivot + (ourVerticalPivot + pivotDelta) * UpVector;

    }

    public override void OnDrawCameraGizmos(CameraBehaviorController camera)
    {
        using var thicknessScope = Draw.ingame.WithLineWidth(3f);
        
        // Draw the vertical stuff
        Draw.ingame.WireSphere(m_cameraVector.Pivot, 0.2f, Color.forestGreen);
        
        // Actual default pivot
        Draw.ingame.WireSphere(m_cameraController.PlayerPivot, 0.2f, Color.orange);
        Draw.ingame.WireSphere(m_cameraController.m_cameraVector.Pivot, 0.2f, Color.yellow);
        
        // Top Limit
        var arrowFacing = m_cameraController.m_camera.transform.forward;
        Draw.ingame.Arrowhead(m_topPos, UpVector, arrowFacing, 0.2f, Color.softRed);
        
        // Bottom Limit
        Draw.ingame.Arrowhead(m_bottomPos, -UpVector, arrowFacing, 0.2f, Color.softRed);
    }
}