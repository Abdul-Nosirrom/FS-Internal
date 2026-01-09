using System;
using System.Collections.Generic;
using FS.Animation;
using FS.Extensions;
using FS.Rendering;
using UnityEngine;
#if UNITY_EDITOR
using FS.Animation.Editor;
#endif

[Serializable]
[EventPath("VFX")]
public class PlayVFX : IAnimationEvent
{
    public string Name => m_vfxPrefab ? $"VFX: {m_vfxPrefab.name}_{NameSuffix}" : "VFX";
    public bool IsRangedEvent => m_vfxPrefab != null && m_vfxPrefab.TryGetComponent<VFXBase>(out var controller) && controller.IsLooping;
    public bool NeedsAnimationEndCallback => m_shouldDeparentOnAnimationFadeOut;
    [VFXDropDown]
    public GameObject m_vfxPrefab;
    public List<string> m_vfxSockets = new();
    public bool m_shouldDeparentOnAnimationFadeOut = false;
    public VFXParams m_vfxParams;
    
    private string NameSuffix => IsRangedEvent ? "Looping" : "OneShot";

    public static VFXParams GetVFXParams(VFXParams ogParams, GameObject context, string socketName = "")
    {
        VFXParams vfxParams = ogParams;
        if (!string.IsNullOrEmpty(socketName))
        {
            var socketTransform = context.transform.FindChildRecursive(socketName);
            if (socketTransform != null)
                vfxParams.m_parent = socketTransform;
            else Debug.LogError($"[VFX Event] Socket {socketName} not found on {context.name}");
        }
        else vfxParams.m_parent = context.transform;
        return vfxParams;
    }

    private void PlayVFX_Internal(GameObject context)
    {
        if (m_vfxPrefab == null) return;
        if (!m_vfxPrefab.TryGetComponent<VFXBase>(out var controller)) return;
        
        if ((IsRangedEvent || m_shouldDeparentOnAnimationFadeOut) && !m_vfxInstances.ContainsKey(context)) m_vfxInstances[context] = new();

        if (m_vfxSockets.Count == 0)
        {
            var fxInst = VFXManager.Instance.PlayVFX(controller, GetVFXParams(m_vfxParams, context));
            if (IsRangedEvent || m_shouldDeparentOnAnimationFadeOut) m_vfxInstances[context].Add(fxInst);
        }
        else
        {
            foreach (var socket in m_vfxSockets)
            {
                var fxInst = VFXManager.Instance.PlayVFX(controller, GetVFXParams(m_vfxParams, context, socket));
                if (IsRangedEvent || m_shouldDeparentOnAnimationFadeOut) m_vfxInstances[context].Add(fxInst);
            }
        }
    }

    public void Execute(GameObject context, float normalizedTime) => PlayVFX_Internal(context);

    private Dictionary<GameObject, List<VFXBase>> m_vfxInstances = new();
    
    public void Start(GameObject context) => PlayVFX_Internal(context);

    public void End(GameObject context)
    {
        if (m_vfxInstances.TryGetValue(context, out var fxInst))
        {
            foreach (var inst in fxInst)
            {
                //Debug.LogError($"[VFX Event] Ending looping VFX on {inst.name}");
                if (inst != null && inst.IsActive && !inst.IsStopping)
                    inst.Stop();
            }
            m_vfxInstances.Remove(context);
        }
    }

    public void OnAnimationFadeOut(GameObject context)
    {
        if (m_shouldDeparentOnAnimationFadeOut)
        {
            if (m_vfxInstances.TryGetValue(context, out var fxInst))
            {
                foreach (var inst in fxInst)
                {
                    if (inst == null) continue;
                    inst.transform.SetParent(null);
                }
            }
        }
    }

#if UNITY_EDITOR
    
    private List<VFXBase> m_editorVFXInstance;
    private void ClearEditorInstances()
    {
        if (m_editorVFXInstance != null)
        {
            foreach (var fxInst in m_editorVFXInstance)
            {
                if (fxInst == null) continue;
                UnityEngine.Object.DestroyImmediate(fxInst.gameObject);
            }
            m_editorVFXInstance.Clear();
        }
    }
    
    private void PlayVFX_Editor(GameObject context, AnimationPreviewRender previewRender)
    {
        if (m_vfxPrefab == null) return;
        
        m_editorVFXInstance ??= new();
        ClearEditorInstances(); // Clear previous instances (only matters on Play for non-ranged events because they dont have an 'end' to cleanup in)
        
        if (m_vfxSockets.Count == 0)
        {
            var fxInst = previewRender.InstantiatePreviewable(m_vfxPrefab.gameObject);
            m_editorVFXInstance.Add(fxInst.GetComponent<VFXBase>());
            GetVFXParams(m_vfxParams, context).ConfigureFX(m_editorVFXInstance[0]);
        }
        else
        {
            foreach (var socket in m_vfxSockets)
            {
                var fxInst = previewRender.InstantiatePreviewable(m_vfxPrefab.gameObject);
                m_editorVFXInstance.Add(fxInst.GetComponent<VFXBase>());
                GetVFXParams(m_vfxParams, context, socket).ConfigureFX(m_editorVFXInstance[^1]);
            }
        }
    }
    
    public void Execute_Editor(GameObject context, float normalizedTime, AnimationPreviewRender previewRender) => PlayVFX_Editor(context, previewRender);
    public void Start_Editor(GameObject context, AnimationPreviewRender previewRender) => PlayVFX_Editor(context, previewRender);
    public void End_Editor(GameObject context, AnimationPreviewRender previewRender) => ClearEditorInstances();
    
#endif
}