using System;
using UltEvents;
using UnityEngine;

namespace FS.CombatSystem
{
    // DamageReceiver is responsible for invoking events, hurtbox just calls on DamageReceiver
    public interface IDamageReceiver
    {
        public HitStunState GetHitStunState();
        public DamageScript GetActiveDamageScript();

        public bool IsInHitStun => GetHitStunState().IsInHitStun;
        
        public GameObject gameObject { get; }

        public event Func<DamageScript, Hurtbox, DamageFailReasons> CanDamageCheck;
        public event Action<GameObject, DamageScript, Hurtbox> OnDamaged;
        public event Action OnHitStunEnd;
        
        
        /// <summary>
        /// To be called at the start of the class implementing this interface. Meant to initialize the IDamageReceiver reference
        /// on all hurtboxes in the children of the owner GameObject, so they can forward damage requests to this DamageReceiver.
        /// </summary>
        public void InitializeDamageReceiver(GameObject owner)
        {
            var hurtBoxes = owner.GetComponentsInChildren<Hurtbox>();
            foreach (var hurtBox in hurtBoxes)
            {
                hurtBox.SetDamageReceiver(this);
            }
        }
        
        public DamageFailReasons CanDamage(DamageScript dmgScript, Hurtbox hurtbox);
        public void ApplyDamage(GameObject attacker, DamageScript dmgScript, Hurtbox hurtbox);

        public void OnHitStunOver();
    }
}