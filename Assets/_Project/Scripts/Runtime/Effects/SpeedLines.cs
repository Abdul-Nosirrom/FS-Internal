using System;
using FS.CameraSystem;
using FS.Rendering;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class SpeedLines : MonoBehaviour, ICommandBufferPass
{
    [Range(3, 64), SerializeField] 
    private int m_numSegments = 16;

    [Range(0, 10), SerializeField] 
    private float m_cylinderSize = 1f;
    
    [Required] public Transform m_target;
    public Camera CameraTarget { get; set;  }

    private Mesh m_cylinderMesh;
    private Material m_speedLinesMaterial;

    private Vector3 m_prevFramePos;
    private PhysicsController m_physics;
    
    private void OnEnable()
    {
        CameraTarget = GetComponent<Camera>();
        if (!CameraTarget) return;
        CameraTarget.AddCameraCommandBuffer(RenderPassEvent.BeforeRenderingPostProcessing, this);
        if (m_speedLinesMaterial == null)
        {
            m_speedLinesMaterial = new Material(Shader.Find("FreeSkies/Effects/SpeedLines"));
            m_speedLinesMaterial.hideFlags = HideFlags.DontSave;
        }
        if (m_target) m_physics = m_target.GetComponent<PhysicsController>();
        GenerateCylinderMesh();
    }

    private void OnDisable()
    {
        if (m_cylinderMesh) DestroyImmediate(m_cylinderMesh);
        if (m_speedLinesMaterial) DestroyImmediate(m_speedLinesMaterial);
        if (CameraTarget) CameraTarget.RemoveCameraCommandBuffer(this);
    }
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        GenerateCylinderMesh();
    }
#endif    

    private void GenerateCylinderMesh()
    {
        if (m_cylinderMesh == null)
        {
            m_cylinderMesh = new Mesh();
            m_cylinderMesh.name = "SpeedLinesCylinder";
            m_cylinderMesh.hideFlags = HideFlags.DontSave;
        }
        
        var vertices = new Vector3[(m_numSegments + 1) * 2];
        var uvs = new Vector2[vertices.Length];
        var indices = new int[m_numSegments * 6];
        float angleStep = 2 * Mathf.PI / m_numSegments;
        for (int i = 0; i <= m_numSegments; i++)
        {
            float angle = i * angleStep;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(x, z, 3); // Top vertex
            vertices[i * 2 + 1] = new Vector3(2*x, 2*z, -3); // Bottom vertex
            uvs[i * 2] = new Vector2((float)i / m_numSegments, 1);
            uvs[i * 2 + 1] = new Vector2((float)i / m_numSegments, 0);

            if (i < m_numSegments)
            {
                int baseIndex = i * 6;
                int vertIndex = i * 2;
                indices[baseIndex] = vertIndex;
                indices[baseIndex + 1] = vertIndex + 1;
                indices[baseIndex + 2] = vertIndex + 2;

                indices[baseIndex + 3] = vertIndex + 2;
                indices[baseIndex + 4] = vertIndex + 1;
                indices[baseIndex + 5] = vertIndex + 3;
            }
        }
        
        m_cylinderMesh.vertices = vertices;
        m_cylinderMesh.uv = uvs;
        m_cylinderMesh.triangles = indices;
    }

    public string Name => "Speed Lines";
    public ScriptableRenderPassInput PassInput => ScriptableRenderPassInput.Depth;

    private Vector3 m_smoothedVelocity;

    private void Start()
    {
        m_smoothedVelocity = Vector3.zero;
        m_prevFramePos = m_target ? m_target.position : Vector3.zero; 
    }

    private TextureHandle m_camRT;
    private TextureHandle m_depthRT;

    public void DeclareResources(IUnsafeRenderGraphBuilder builder, RenderGraph renderGraph, ContextContainer frameData)
    {
        // Bind depth buffer (Wait how do we do it?)
        var camData = frameData.Get<UniversalResourceData>();
        m_camRT = camData.cameraColor;
        m_depthRT = camData.activeDepthTexture;
        builder.UseTexture(m_camRT, AccessFlags.ReadWrite);
        builder.UseTexture(m_depthRT, AccessFlags.Read);
    }

    private readonly int k_playerSpeedID = Shader.PropertyToID("_PlayerSpeed");

    private Vector3 targetPos => m_target.position + m_target.up;
    
    public void OnCameraRender(CommandBuffer cmd)
    {
        if (m_target == null) return;
        if (m_speedLinesMaterial == null) return;
        
        var velocity = (targetPos - m_prevFramePos) / Time.deltaTime;
        if (m_physics) velocity = m_physics.Velocity;
        m_smoothedVelocity = Vector3.Lerp(m_smoothedVelocity, velocity, 15f * Time.deltaTime);
        var speed = m_smoothedVelocity.magnitude;
        
        cmd.SetGlobalFloat(k_playerSpeedID, speed);

        if (m_smoothedVelocity.sqrMagnitude <= 0f) return;

        var targetForward = m_smoothedVelocity;
        Matrix4x4 matrix = Matrix4x4.TRS(targetPos, Quaternion.LookRotation(targetForward, Vector3.up), new Vector3(1, 1, 1) * m_cylinderSize);
        cmd.SetRenderTarget(
            m_camRT,                  // Color buffer
            RenderBufferLoadAction.Load,       // LOAD color - we want to draw on top of existing scene
            RenderBufferStoreAction.Store,     // STORE color - we need the result
            m_depthRT,                      // Depth buffer  
            RenderBufferLoadAction.Load,       // LOAD depth - we need existing depth for ZTest
            RenderBufferStoreAction.DontCare   // DONT CARE depth - we're not writing to it (ZWrite Off)
        );
        cmd.DrawMesh(m_cylinderMesh, matrix, m_speedLinesMaterial, 0);
        
        m_prevFramePos = targetPos;
    }

}