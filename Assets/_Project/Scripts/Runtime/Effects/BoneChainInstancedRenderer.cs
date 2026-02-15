using System;
using System.Collections.Generic;
using Animancer.Editor;
using FS.Rendering;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class BoneChainInstancedRenderer : MonoBehaviour, ICommandBufferPass
{
    public float m_instanceSpacing = 0.25f;
    public Quaternion m_localRotation;
    public Quaternion m_perInstanceRotOffset;
    public Vector3 m_scale = Vector3.one;
    public List<Transform> m_boneChain;
    
    public Material m_material;
    public Mesh m_mesh;

    void OnEnable() => this.AddGlobalCommandBuffer(RenderPassEvent.AfterRenderingOpaques);
    void OnDisable() => this.RemoveGlobalCommandBuffer();

    public void OnCameraRender(CommandBuffer cmd)
    {
        // if (m_boneChain.Count <= 1) return;
        // if (m_mesh == null || m_material == null) return;
        //
        // var matrices = ComputeInstanceTransforms();
        // cmd.DrawMeshInstanced(m_mesh, 0, m_material, 0, matrices.ToArray());
    }

    RenderParams m_renderParams;

    RenderParams RenderParams
    {
        get
        {
            if (m_renderParams.material == null)
            {
                m_renderParams = new RenderParams(m_material);
                m_renderParams.receiveShadows = true;
                m_renderParams.shadowCastingMode = ShadowCastingMode.On;
            }
            return m_renderParams;
        }
    }

    private void Update()
    {
        if (m_boneChain.Count <= 1) return;
        if (m_mesh == null || m_material == null) return;
        
        var matrices = ComputeInstanceTransforms();
        if (matrices.Count == 0) return;
        Graphics.RenderMeshInstanced(RenderParams, m_mesh, 0, matrices);
    }

    private List<Matrix4x4> ComputeInstanceTransforms()
    {
        float prevDistWalked = 0;
        float distWalked = 0;
        float distSinceLastInstance = 0;
        
        List<Matrix4x4> matrices = new List<Matrix4x4>();
        
        // Define these in upper scope as we want to continue the interpolation as we go into the next pair of bones
        Vector3 pos;
        Quaternion rot;
        Vector3 scale; 
        
        // Walk bone chain - subdivide pairs along linear lines to create matrix bone chain
        for (int i = 0; i < m_boneChain.Count - 1; i++)
        {
            var curTransform = m_boneChain[i];
            var nextTransform = m_boneChain[i + 1];
            
            // Easy skip if distance between the two is less than spacing
            var dist = Vector3.Distance(curTransform.position, nextTransform.position);
            // if (dist + distSinceLastInstance < m_instanceSpacing)
            // {
            //     distSinceLastInstance += dist;
            //     prevDistWalked = distWalked;
            //     distWalked += dist;
            //     continue;
            // }
            
            // Subdivide walk by instanceSpacing (we should carry over dist since last instance, utilizing distance of last point to nextTransform)
            int numSteps = Mathf.FloorToInt(dist / m_instanceSpacing);
            
            for (int s = 0; s < numSteps; s++)
            {
                float t = s / (float)numSteps;

                // Testin this
                var perInstOffset = (matrices.Count % 2) == 0 ? Quaternion.identity : m_perInstanceRotOffset;
                
                pos = Vector3.Lerp(curTransform.position, nextTransform.position, t);
                rot = Quaternion.Slerp(curTransform.rotation, nextTransform.rotation, t) * (m_localRotation * perInstOffset);
                scale = m_scale;
                
                matrices.Add(Matrix4x4.TRS(pos, rot, scale));
            }
        }

        return matrices;
    }
}