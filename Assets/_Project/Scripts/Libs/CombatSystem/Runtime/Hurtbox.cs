using System;
using FS.Attributes;
using UnityEngine;
using UnityEngine.Assertions;

namespace FS.CombatSystem
{
    [Flags]
    public enum CombatAffiliation
    {
        Player = 1 << 0,
        Enemy = 1 << 1,
        Neutral = 1 << 2,
    }
    
    // TODO: Debug shit needed
    public class Hurtbox : TargetingQuery
    {
        [SerializeField, Range(0, 1)] protected float m_damageResistance;
        [SerializeField, EnumFlag] protected CombatAffiliation m_combatAffiliation = CombatAffiliation.Enemy;
        
        [SerializeField] protected HitEffect m_defaultHitEffect;
        [SerializeField] protected Transform m_hitEffectTransform;
        
        protected bool m_isInvincible;
        public bool IsInvincible
        {
            get => m_isInvincible;
            set => m_isInvincible = value;
        }
        
        protected IDamageReceiver m_damageReceiver;
        public HitStopController m_hitStop { get; private set; }

        public void SetDamageReceiver(IDamageReceiver damageReceiver)
        {
            m_damageReceiver = damageReceiver;
            Assert.IsNotNull(m_damageReceiver, $"[Combat System] Hurtbox on {gameObject.name} was given a null IDamageReceiver. Please ensure there is a valid DamageReceiver assigned.");
            m_hitStop = m_damageReceiver.gameObject.GetComponentInChildren<HitStopController>();
        }

        private void Awake()
        {
            m_damageReceiver = GetComponentInParent<IDamageReceiver>();
            Assert.IsNotNull(m_damageReceiver, $"[Combat System] Hurtbox on {gameObject.name} could not find a parent with a component implementing IDamageReceiver. Please ensure there is a valid DamageReceiver in the parent hierarchy.");
        }

        public HitEffect GetHitEffect(DamageScript dmgScript)
        {
            if (dmgScript != null && dmgScript.m_hitEffect != null) return dmgScript.m_hitEffect;
            return m_defaultHitEffect; // TODO: Implement project default hit effect
        }
        /// <summary>
        /// Transform to spawn hit effects at. By default, this is the transform of the GameObject the Hurtbox is attached to.
        /// </summary>
        public Transform GetHitEffectTransform() => m_hitEffectTransform == null ? transform : m_hitEffectTransform;

        public bool ApplyDamage(GameObject attacker, DamageScript dmgScript, CombatAffiliation affiliationTarget) => ApplyDamage(attacker, dmgScript, affiliationTarget, out _);
        public bool ApplyDamage(GameObject attacker, DamageScript dmgScript, CombatAffiliation affiliationTarget, out DamageFailReasons failReasons)
        {
            failReasons = DamageFailReasons.None;
            if ((affiliationTarget & m_combatAffiliation) == 0) return false;
            
            // Check against hit list
            if (CombatService.Instance.IsAlreadyInHitList(attacker, m_damageReceiver)) return false;
            
            // Check if can damage
            failReasons = m_damageReceiver.CanDamage(dmgScript, this);
            if (IsInvincible) failReasons |= DamageFailReasons.IFrames;
            if (failReasons != DamageFailReasons.None) return false;
            
            // We're succesful, so apply the damage
            CombatService.Instance.AddToHitList(attacker, m_damageReceiver);
            
            m_damageReceiver.ApplyDamage(attacker, dmgScript, this);
            
            // Apply hit effects & hit stop
            CombatService.Instance.ApplyHitEffects(attacker, this, dmgScript);
            
            return true;
        }
    }
}