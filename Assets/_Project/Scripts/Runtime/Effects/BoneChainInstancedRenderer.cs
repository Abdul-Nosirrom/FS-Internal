using System.Collections.Generic;
using FluffyUnderware.Curvy;
using FS.Rendering;
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

    private const int MAX_INSTANCE_COUNT = 1024;
    private Matrix4x4[] m_trsMatrices;

    void OnEnable()
    {
        m_trsMatrices = new Matrix4x4[MAX_INSTANCE_COUNT]; // Preallocate array to max count
        
        m_splineGuide = CurvySpline.Create();
        m_splineGuide.transform.SetParent(transform);
        m_splineGuide.Clear(false);
        m_splineGuide.SetControlPointCount(m_boneChain.Count);
        //this.AddGlobalCommandBuffer(RenderPassEvent.AfterRenderingOpaques);
    }

    void OnDisable()
    {
        DestroyImmediate(m_splineGuide);
        m_splineGuide = null;
        //this.RemoveGlobalCommandBuffer();
    }

    public void OnCameraRender(CommandBuffer cmd)
    {
        // if (m_boneChain.Count <= 1) return;
        // if (m_mesh == null || m_material == null) return;
        //
        // SyncSplineGuide();
        // int count = ComputeInstanceTransforms();
        // if (count == 0) return;
        // cmd.DrawMeshInstanced(); // TODO: Do our shaders support GPU instancing or only SRP Batching? Research this
        // Graphics.RenderMeshInstanced(RenderParams, m_mesh, 0, m_trsMatrices, count);

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
        int count = ComputeInstanceTransforms();
        if (count == 0) return;
        Graphics.RenderMeshInstanced(RenderParams, m_mesh, 0, m_trsMatrices, count);
    }

    private void SyncSplineGuide()
    {
        //Debug.Log($"Length mismatch? {m_boneChain.Count} and {m_splineGuide.Count}");
        for (int b = 0; b < m_boneChain.Count; b++)
        {
            var boneTransform  = m_boneChain[b];
            var splineCP = m_splineGuide.ControlPointsList[b];

            splineCP.SetPosition(boneTransform.position);
            splineCP.SetRotation(boneTransform.rotation);
        }
    }

    private int ComputeInstanceTransforms()
    {
        int count = 0;
        float splineDist = m_splineGuide.TFToDistance(1);
        // Limit to MAX_INSTANCE_COUNT instances
        if (m_instanceSpacing <= 0) return count;
        if (splineDist / m_instanceSpacing > MAX_INSTANCE_COUNT)
        {
            Debug.LogError($"[Chain Instance Rendering]: Exceeded {MAX_INSTANCE_COUNT} count. Predicted count was: {Mathf.Floor(splineDist / m_instanceSpacing)}");
            return count;
        }
        
        float currentDist = 0f;
        while (currentDist <= splineDist)
        {
            Quaternion perInstVariation = count % 2 == 0 ? m_perInstanceRotOffset : Quaternion.identity;
            float tf = m_splineGuide.DistanceToTF(currentDist);
            m_splineGuide.InterpolateAndGetTangentFast(tf, out var pos, out var tangent, Space.World);
            m_trsMatrices[count] = Matrix4x4.TRS(pos, Quaternion.LookRotation(tangent) * perInstVariation * m_localRotation, m_scale);
            currentDist += m_instanceSpacing;
            count++;
        }

        return count;
    }
}