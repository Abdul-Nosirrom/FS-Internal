using System;
using FS.Animation;
using FS.Audio;
using FS.Rendering;
using UnityEngine;

#if UNITY_EDITOR
using FS.Animation.Editor;
#endif

public class FootStepEffects : MonoBehaviour
{
    public enum Side { Left, Right }
    
    public Transform LeftFoot;
    public Transform RightFoot;

    public AudioElement SFX;
    public VFXBase VFX;
    

    public void PlayFootStepEffect(Side side)
    {
        var footTransform = GetFootTransform(side);
        AudioManager.PlayAudio_Attached(SFX, footTransform.gameObject);

        if (VFX == null) return;
        
        var fxParams = new VFXParams()
        {
            m_shouldParent = true,
            m_parent =  footTransform,
        };
        VFXManager.Instance.PlayVFX(VFX, fxParams);
    }
    
    private Transform GetFootTransform(Side side) => side == Side.Left ? LeftFoot : RightFoot;
}


[Serializable]
[EventPath("Movement")]
public class FootStepEvent : IAnimationEvent
{
    public string Name => $"Foot Step [{Foot}]";
    public bool IsRangedEvent => false;

    public FootStepEffects.Side Foot;

    private FootStepEffects m_effectsComponent;

    public void Execute(GameObject context, float normalizedTime)
    {
        if (m_effectsComponent == null)
        {
            // NOTE: GameObject passes in is always the one Animators on
            m_effectsComponent = context.GetComponentInChildren<FootStepEffects>();
        }

        if (m_effectsComponent == null) return;
        
        m_effectsComponent.PlayFootStepEffect(Foot);
    }
    
#if UNITY_EDITOR
    public void Execute_Editor(GameObject context, float normalizedTime, AnimationPreviewRender previewRender)
    {
        // No clean editor previewing due to FootStepEffects component reliance and no way to fetch its serialized data
    }
#endif    
}