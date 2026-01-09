using System;
using System.Collections.Generic;
using Drawing;
using FS.GameplayActions;
using PrimeTween;
using UnityEngine;

namespace FS.CombatSystem
{
    public class HitStunActionConstraint : ActionConstraintBase
    {
        public override string Name => "HitStun";

        /// <summary>
        /// Cancel all actions when this constraint is enabled. As we've just entered hitstun.
        /// </summary>
        protected override void OnEnable()
        {
            foreach (var action in m_controller.ActiveActions.Actions)
            {
                action.TryEndAction();
            }
        }

        /// <summary>
        /// Disallow any actions running when in hitstun, so this constraint always returns false.
        /// </summary>
        public override bool EvaluateConstraint(GameplayAction action) => false;
    }
    
    // TODO: Debug shit needed
    [RequireComponent(typeof(ActionController), typeof(HitStopController)), DisallowMultipleComponent]
    public class CharacterDamageReceiver : MonoBehaviour, IDamageReceiver
    {
        public event Func<DamageScript, Hurtbox, DamageFailReasons> CanDamageCheck;
        public event Action<GameObject, DamageScript, Hurtbox> OnDamaged;
        public event Action OnHitStunEnd;

        public HitReactAnimationController m_hitReactAnimations;
        
        private IDamageReceiver m_dmgReceiver;
        private ActionController m_actionController;
        private PhysicsController m_physics;
        private Renderer[] m_renderers;
        
        private HitStunActionConstraint m_hitStunConstraint;
        private HitStunState m_hitStunState = new HitStunState();
        private DamageScript m_activeDamageScript;

        private HitStopController m_hitStop;
        
        public HitStunState GetHitStunState() => m_hitStunState;
        public DamageScript GetActiveDamageScript() => m_activeDamageScript;

        private Dictionary<DamageScript, DamageScript> m_damageScriptInstancePool = new();

        
        private int m_hitStunFlashTimeID = Shader.PropertyToID("_HitStunFlashTime");
        
        protected bool IsInActiveHitStun => m_dmgReceiver.IsInHitStun && m_activeDamageScript != null;

        private void Start()
        {
            m_dmgReceiver = this;
            m_actionController = GetComponent<ActionController>();
            m_physics = GetComponent<PhysicsController>();
            m_hitStop = GetComponent<HitStopController>();
            
            m_renderers = GetComponentsInChildren<Renderer>();
            
            m_dmgReceiver.InitializeDamageReceiver(gameObject);
            m_hitStunConstraint = ActionConstraintBase.Create<HitStunActionConstraint>();
            m_actionController.ConstraintHandler.AddConstraint(m_hitStunConstraint);

            m_hitStunState.Setup(this);
            
            // Ground & Collision events are relevant
            m_physics.OnLandedEvt += OnGroundImpact;
        }

        //private void Update()
        //{
        //    Draw.ingame.Label2D(transform.position + 2*Vector3.up, $"InHitstun: {IsInActiveHitStun}\n" +
        //                                       $"HitstunFrames: {m_hitStunState.m_hitStun}\n" +
        //                                       $"ActiveDamageScript: {(m_activeDamageScript != null ? m_activeDamageScript.GetType().Name : "None")}");
        //}

        private void OnDestroy()
        {
            m_physics.OnLandedEvt -= OnGroundImpact;
        }

        public DamageFailReasons CanDamage(DamageScript dmgScript, Hurtbox hurtbox)
        {
            var evtReasons = CanDamageCheck?.Invoke(dmgScript, hurtbox);
            return evtReasons ?? DamageFailReasons.None;
        }

        public void ApplyDamage(GameObject attacker, DamageScript dmgScript, Hurtbox hurtbox)
        {
            // Enable constraint to block all actions until we enter 'recovery'
            m_hitStunConstraint.EnableConstraint();

            if (!m_damageScriptInstancePool.ContainsKey(dmgScript))
                m_damageScriptInstancePool.Add(dmgScript, (DamageScript)dmgScript.Clone());
            
            dmgScript = m_damageScriptInstancePool[dmgScript];
            
            m_activeDamageScript = dmgScript;
            m_activeDamageScript.StartHitStun(attacker, ref m_hitStunState, hurtbox);
            
            foreach (var subRenderer in m_renderers)
            {
                //Tween.MaterialProperty(subRenderer.sharedMaterial, m_hitStunFlashTimeID, 1f, 0f, 0.4f, Ease.OutCirc);
                subRenderer.sharedMaterial.SetFloat(m_hitStunFlashTimeID, Time.time);
            }
            
            OnDamaged?.Invoke(attacker, dmgScript, hurtbox);
        }

        private void FixedUpdate()
        {
            if (!IsInActiveHitStun || m_hitStop.HitStopActive) return;
            
            m_activeDamageScript.UpdateHitStun(ref m_hitStunState, Time.fixedDeltaTime);
            
            // TODO: In UE, the DamageScript is responsible for ending, not just when the hitstun counter = 0
            if (m_hitStunState.ShouldEndHitStun)
            {
                m_activeDamageScript.EndHitStun(ref m_hitStunState);
                m_activeDamageScript = null;
            }
        }

        public void OnHitStunOver()
        {
            m_hitStunConstraint.DisableConstraint();
            OnHitStunEnd?.Invoke();
        }
        
        private void OnGroundImpact()
        {
            if (!IsInActiveHitStun) return;
            
            m_activeDamageScript.OnGroundImpact(ref m_hitStunState, m_physics.Ground);
        }

        private void OnCollisionEnter(Collision other)
        {
            if (!IsInActiveHitStun) return;
            
            // We should only care about wall impacts, not ground, and it only matters if we're not grounded
            if (m_physics.IsGrounded) return;
            
            m_activeDamageScript.OnWallImpact(ref m_hitStunState, other);
        }
    }
}