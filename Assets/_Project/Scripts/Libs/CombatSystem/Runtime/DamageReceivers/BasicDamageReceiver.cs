using System;
using UnityEngine;

namespace FS.CombatSystem
{
    /// <summary>
    /// Basic damange reciever class that just invokes the events defined in the IDamageReceiver interface, does not update state at all and takes all incoming damage
    /// </summary>
    public class BasicDamageReceiver : MonoBehaviour, IDamageReceiver
    {
        private HitStunState m_hitStunState = new();
        private DamageScript m_activeDamageScript;
        
        public HitStunState GetHitStunState() => m_hitStunState;
        public DamageScript GetActiveDamageScript() => m_activeDamageScript;

        public event Func<DamageScript, Hurtbox, DamageFailReasons> CanDamageCheck;
        public event Action<GameObject, DamageScript, Hurtbox> OnDamaged;
        public event Action OnHitStunEnd;

        public DamageFailReasons CanDamage(DamageScript dmgScript, Hurtbox hurtbox) => CanDamageCheck?.Invoke(dmgScript, hurtbox) ?? DamageFailReasons.None;

        public void ApplyDamage(GameObject attacker, DamageScript dmgScript, Hurtbox hurtbox)
        {
            OnDamaged?.Invoke(attacker, dmgScript, hurtbox);
        }

        public void OnHitStunOver()
        {
            OnHitStunEnd?.Invoke();
        }
    }
}