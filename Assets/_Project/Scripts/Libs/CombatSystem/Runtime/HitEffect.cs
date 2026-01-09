using FS.Audio;
using FS.Rendering;
using UnityEngine;

namespace FS.CombatSystem
{
    // TODO: Complete this
    public class HitEffect : ScriptableObject
    {
        public VFXController m_vfx;
        public AudioElement m_sfx;

        public virtual void ApplyHitEffect(Hurtbox hurtbox)
        {
            VFXParams fxParams = new VFXParams
            {
                m_parent = hurtbox.GetHitEffectTransform(),
                m_shouldParent = false,
            };
            
            VFXManager.Instance.PlayVFX(m_vfx, fxParams);
            AudioManager.PlayAudio_3D(m_sfx, hurtbox.GetHitEffectTransform().position);
        }

        // For duration based effects like burning?
        public virtual void StartHitEffect(Hurtbox hurtbox)
        {
            
        }
        
        public virtual void StopHitEffect(Hurtbox hurtbox)
        {
            
        }
    }
}