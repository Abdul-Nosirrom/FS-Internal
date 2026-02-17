using System;
using System.Collections.Generic;
using Animancer.Editor;
using FluffyUnderware.Curvy;
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

    private CurvySpline m_splineGuide;

    void OnEnable()
    {
        m_splineGuide = CurvySpline.Create();
        m_splineGuide.transform.SetParent(transform);
        m_splineGuide.Clear(false);
        m_splineGuide.SetControlPointCount(m_boneChain.Count);
        this.AddGlobalCommandBuffer(RenderPassEvent.AfterRenderingOpaques);
    }

    void OnDisable()
    {
        DestroyImmediate(m_splineGuide);
        m_splineGuide = null;
        this.RemoveGlobalCommandBuffer();
    }

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
        
        SyncSplineGuide();
        var matrices = ComputeInstanceTransforms();
        if (matrices.Count == 0) return;
        Graphics.RenderMeshInstanced(RenderParams, m_mesh, 0, matrices);
    }

    private void SyncSplineGuide()
    {
        Debug.Log($"Length mismatch? {m_boneChain.Count} and {m_splineGuide.Count}");
        for (int b = 0; b < m_boneChain.Count; b++)
        {
            var boneTransform  = m_boneChain[b];
            var splineCP = m_splineGuide.ControlPointsList[b];

            splineCP.SetPosition(boneTransform.position);
            splineCP.SetRotation(boneTransform.rotation);
        }
    }

    private List<Matrix4x4> ComputeInstanceTransforms()
    {
        List<Matrix4x4> matrices = new List<Matrix4x4>();

        float splineDist = m_splineGuide.TFToDistance(1);
        // Limit to 250 instances
        if (m_instanceSpacing <= 0) return matrices;
        if (splineDist / m_instanceSpacing > 250) return matrices;
        
        float currentDist = 0f;
        int b = 0;
        while (currentDist <= splineDist)
        {
            Quaternion perInstVariation = b % 2 == 0 ? m_perInstanceRotOffset : Quaternion.identity;
            b++;
            float tf = m_splineGuide.DistanceToTF(currentDist);
            m_splineGuide.InterpolateAndGetTangentFast(tf, out var pos, out var tangent, Space.World);
            matrices.Add(Matrix4x4.TRS(pos, Quaternion.LookRotation(tangent) * perInstVariation * m_localRotation, m_scale));
            currentDist += m_instanceSpacing;
        }

        return matrices;
    }
}