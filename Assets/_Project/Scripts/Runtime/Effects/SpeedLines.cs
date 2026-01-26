using FS.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class SpeedLines : MonoBehaviour, ICameraRenderPass
{
    public string Name => "Speed Lines";
    private Camera m_camera;
    private Material m_speedLinesMaterial;
    
    private void OnEnable()
    {
        m_camera = GetComponent<Camera>();
        if (!m_camera) return;
        m_camera.AddCameraCommandBuffer(RenderPassEvent.BeforeRenderingPostProcessing, this);
        if (m_speedLinesMaterial == null)
        {
            m_speedLinesMaterial = new Material(Shader.Find("FreeSkies/Effects/SpeedLines"))
            {
                hideFlags = HideFlags.DontSave
            };
        }
    }

    private void OnDisable()
    {
        if (m_speedLinesMaterial) DestroyImmediate(m_speedLinesMaterial);
        if (m_camera) m_camera.RemoveCameraCommandBuffer(this);
    }

    public void OnCameraRender(CommandBuffer cmd, TextureHandle source, TextureHandle dest)
    {
        Blitter.BlitCameraTexture(cmd,  source, dest, m_speedLinesMaterial, 0);
    }
}