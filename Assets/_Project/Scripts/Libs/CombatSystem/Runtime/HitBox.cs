using System;
using FS.Attributes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FS.CombatSystem
{
    // TODO: For testing and simple, could be improved
    public class HitBox : MonoBehaviour, ISelfValidator
    {
        [EnumFlag] public CombatAffiliation m_affiliationTarget;
        [SerializeReference] public DamageScript m_damageScript;
        
        public void Validate(SelfValidationResult result)
        {
            if (gameObject.layer != PhysicsLayers.HitBox)
            {
                result.AddError($"Incorrect layer for HitBox: {gameObject.layer}. Expected: [HitBox] {PhysicsLayers.HitBox.value}");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Hurtbox hurtbox = other.GetComponent<Hurtbox>();
            if (hurtbox != null)
            {
                hurtbox.ApplyDamage(gameObject, m_damageScript, m_affiliationTarget);
            }
            
            CombatService.Instance.ClearHitList(gameObject);
        }
    }
}