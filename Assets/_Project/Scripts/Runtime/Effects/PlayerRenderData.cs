using FS.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerRenderData : MonoBehaviour, ICommandBufferPass
{
    private PhysicsController m_physics;
    private Camera m_camera;

    public static readonly int k_PlayerSpeedID = Shader.PropertyToID("_PlayerSpeed");
    public static readonly int k_PlayerLateralSpeedID = Shader.PropertyToID("_PlayerLateralSpeed");
    public static readonly int k_PlayerVerticalSpeedID = Shader.PropertyToID("_PlayerVerticalSpeed");

    public static readonly int k_PlayerVelocityID = Shader.PropertyToID("_PlayerVelocity");
    public static readonly int k_PlayerPositionID = Shader.PropertyToID("_PlayerPosition");
    public static readonly int k_PlayerRotationID = Shader.PropertyToID("_PlayerRotation");
    
    
    private void Awake()
    {
        m_camera = GetComponent<Camera>();
        if (m_camera == null) return;
        
        m_physics = transform.parent.GetComponentInChildren<PhysicsController>(); // NOTE: THis would break if we choose to change the hierarchy of camera/player root/player
        m_camera.AddCameraCommandBuffer(RenderPassEvent.BeforeRendering, this);
    }

    private void OnDestroy()
    {
        if (m_camera == null) return;
        m_camera.RemoveCameraCommandBuffer(this);
    }

    public void OnCameraRender(CommandBuffer cmd)
    {
        if (m_physics == null) return;
        
        cmd.EnableShaderKeyword("PLAYER_RENDER_DATA");
        
        // Smoothing for speeds/positions?
        
        cmd.SetGlobalFloat(k_PlayerSpeedID, m_physics.Speed);
        cmd.SetGlobalFloat(k_PlayerLateralSpeedID, m_physics.LateralSpeed);
        cmd.SetGlobalFloat(k_PlayerVerticalSpeedID, m_physics.VerticalSpeed);
        
        cmd.SetGlobalVector(k_PlayerVelocityID, m_physics.Velocity);
        cmd.SetGlobalVector(k_PlayerPositionID, m_physics.Position);
        cmd.SetGlobalVector(k_PlayerRotationID, m_physics.transform.forward);
    }
}