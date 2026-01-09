using System;
using FS.CameraSystem;
using UnityEngine;

namespace FS.CombatSystem
{
    [Flags]
    public enum DamageFailReasons
    {
        None = 0,
        IFrames = 1 << 0,
        Parry = 1 << 1,
        Block = 1 << 2,
        Ignored = 1 << 3,
    }

    public enum HitAnimationDirection
    {
        NONE, FORWARD, BACK, LEFT, RIGHT, UP, DOWN, RADIAL
    }
    
    /// <summary>
    /// Essentially "Actions" for hit-reacts. They allow for defining different damage types and how their hit reaction
    /// behavior should play out. They also control the flow of the HitStun state and have access to modifying it.
    /// 
    /// RESPONSIBILITIES:
    ///     - HITSTUN STATE MANAGEMENT: Calling UpdateHitStun that's passed in and calling OnHitStunEnded on the IDamageReceiver
    ///     - Broadcast appropriate events to the IDamageReceiver (OnHit, OnBlockedHit, OnParriedHit, etc)
    ///     - ANIMATION: Define the appropriate animations to play on the IDamageReceivers HitReactAnimationController (if available)
    ///     - FOLLOWUPS: Damage scripts can trigger follow up scripts. E.g Ground Bounce/Wall Slam
    ///     - BEHAVIOR: Define the behavior of the hitstun. E.g with a grapple, managing victim position to grapple point, or with a launch, launch physics and possible follow-ups
    /// </summary>
    [System.Serializable]
    public abstract class DamageScript : ICloneable
    {
        public uint m_hitStunFrames = 10;
        public float m_damageAmount = 10;
        public HitAnimationDirection m_hitAnimDirection = HitAnimationDirection.NONE;
        public HitStopStrength m_hitStopFrames = HitStopStrength.Medium;
        public CameraShakeStrength m_cameraShake = CameraShakeStrength.Medium;
        public virtual DamageFailReasons IgnoreFailReasons => 0;
        public HitEffect m_hitEffect;

        protected Vector3 velocity;
        protected Quaternion rotation;
        
        public static T Create<T>() where T : DamageScript, new() => new T();
        
        public void StartHitStun(GameObject attacker, ref HitStunState state, Hurtbox hurtbox)
        {
            if (state.m_physics != null)
            {
                state.m_physics.VelocityCalculationsEnabled = state.m_physics.RotationCalculationsEnabled = false;
                velocity = state.m_physics.Velocity;
                rotation = state.m_physics.Rotation;
            }
            
            // Initialize hitstun state in prep for running the damage script
            state.StartHitStun(attacker, hurtbox, this);
            OnHitStunStart(ref state);

            UpdatePhysics(state.m_physics);
        }
        
        public void UpdateHitStun(ref HitStunState state, float deltaTime)
        {
            state.UpdateHitStun();
            OnHitStunUpdate(ref state, deltaTime);
            
            UpdatePhysics(state.m_physics);
        }

        public void EndHitStun(ref HitStunState state)
        {
            OnHitStunEnd(ref state);
            state.EndHitStun();
            
            UpdatePhysics(state.m_physics);

            if (state.m_physics != null)
            {
                state.m_physics.VelocityCalculationsEnabled = state.m_physics.RotationCalculationsEnabled = true;
            }
        }
        
        protected abstract void OnHitStunStart(ref HitStunState state);
        protected abstract void OnHitStunUpdate(ref HitStunState state, float deltaTime);
        protected abstract void OnHitStunEnd(ref HitStunState state);
        
        public virtual void OnGroundImpact(ref HitStunState state, GroundInfo groundInfo) { }
        public virtual void OnWallImpact(ref HitStunState state, Collision wallInfo) { }

        /// <summary>
        /// Sets internal velocity/rotation state for physics updates during hitstun.
        /// </summary>
        private void UpdatePhysics(PhysicsController physics)
        {
            if (!physics) return;
            physics.Velocity = velocity;
            physics.Rotation = rotation;
        }
        
        /// <summary>
        /// Returns a shallow copy of this DamageScript. Override if you have reference types that need deep copying.
        /// </summary>
        public object Clone() => this.MemberwiseClone();
    }

    /// <summary>
    /// Abstract generic self - for builder pattern convenience.
    /// </summary>
    [Serializable]
    public abstract class DamageScript<TSelf> : DamageScript where TSelf : DamageScript<TSelf>, new()
    {
        public static TSelf Create() => new TSelf();
        
        public TSelf WithHitStop(HitStopStrength strength)
        {
            m_hitStopFrames = strength;
            return (TSelf)this;
        }
    
        public TSelf WithCameraShake(CameraShakeStrength strength)
        {
            m_cameraShake = strength;
            return (TSelf)this;
        }
    
        public TSelf WithDamage(float amount)
        {
            m_damageAmount = amount;
            return (TSelf)this;
        }
        
        public TSelf WithHitStunFrames(uint frames)
        {
            m_hitStunFrames = frames;
            return (TSelf)this;
        }
        
        public TSelf WithDamageAmount(float amount)
        {
            m_damageAmount = amount;
            return (TSelf)this;
        }
        
        public TSelf WithHitAnimDirection(HitAnimationDirection direction)
        {
            m_hitAnimDirection = direction;
            return (TSelf)this;
        }
        
        public TSelf WithHitEffect(HitEffect hitEffect)
        {
            m_hitEffect = hitEffect;
            return (TSelf)this;
        }
    }

    [Serializable]
    public class ObjectHazardKnockbackDamage : DamageScript<ObjectHazardKnockbackDamage>
    {
        protected override void OnHitStunStart(ref HitStunState state)
        {
            var hurtVel = state.m_physics.Velocity;
            hurtVel = -hurtVel.normalized;
            velocity = hurtVel * 15f;
            velocity.y = 10f;
        }

        protected override void OnHitStunUpdate(ref HitStunState state, float deltaTime)
        {
            if (state.ShouldEndHitStun && state.m_physics.IsGrounded) EndHitStun(ref state);

            if (!state.m_physics.IsGrounded) velocity += state.m_physics.GravityDir * 30f * deltaTime;
            velocity.x *= 0.96f;
            velocity.z *= 0.96f;
        }

        protected override void OnHitStunEnd(ref HitStunState state)
        {
            if (state.m_physics.IsGrounded) velocity = Vector3.zero;
        }

        public override void OnGroundImpact(ref HitStunState state, GroundInfo groundInfo)
        {
            EndHitStun(ref state);
        }
    }

}