using TimeUtils;
using UnityEngine;

namespace FS.CombatSystem
{
    /// <summary>
    /// Transient hit stun data, holding information about the current hit stun state of the owner IDamageReceiver.
    /// This is used to pass in to damage scripts to have all necessary info on the owner, as DamageScripts arent instantiated.
    /// Contains event bindings for hit stun milestones like when HitStun reaches 0 (Ends).
    ///
    /// DamageScript is responsible for managing the HitStunState, calling UpdateHitStun and EndHitStun (Not Start) at appropriate times.
    /// </summary>
    public class HitStunState
    {
        public bool IsInHitStun => m_hitStun > 0 && m_isInHitStun;
        public bool ShouldEndHitStun => m_hitStun == 0;

        /// <summary>
        /// Manual flag to indicate if we're in hitstun. This is separate from m_hitStun > 0 check, as some damage scripts
        /// </summary>
        private bool m_isInHitStun;

        public TimeSince m_sinceHitStunStarted;

        public Hurtbox m_damagedHurtbox;
        public GameObject m_attacker;
        public PhysicsController m_physics;

        /// <summary>
        /// Frames remaining in hit stun
        /// </summary>
        public uint m_hitStun;

        private IDamageReceiver m_owner;
        
        public void Setup(IDamageReceiver owner)
        {
            m_owner = owner;
            m_physics = owner.gameObject.GetComponent<PhysicsController>();
            m_hitStun = 0;
        }

        public bool StartHitStun(GameObject attacker, Hurtbox hurtbox, DamageScript dmgScript)
        {
            if (dmgScript == null)
            {
                Debug.LogError("[CombatSystem] Trying to init hit stun with null damage script!");
                return false;
            }

            m_hitStun = dmgScript.m_hitStunFrames;
            m_isInHitStun = true;
            m_sinceHitStunStarted = 0;

            m_damagedHurtbox = hurtbox;
            m_attacker = attacker;


            return true;
        }

        public void UpdateHitStun()
        {
            m_hitStun--;
        }

        public void EndHitStun()
        {
            m_isInHitStun = false;
            m_owner.OnHitStunOver();
        }
    }
}