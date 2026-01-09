using System.Collections;
using System.Collections.Generic;
using FS.CameraSystem;
using FS.GameService;
using FS.GameService.Attributes;
using FS.Player;
using UnityEngine;

namespace FS.CombatSystem
{
    public enum HitStopStrength
    {
        Light, Medium, Heavy
    }
    
    [AutoRegisteredService(EGameServiceRegistrationOrder.Late)]
    public class CombatService : MonoBehaviour, IGameService
    {
        public static CombatService Instance => GameServiceLocator.Get<CombatService>();
        
        public static readonly Dictionary<HitStopStrength, float> HitStopDurations = new()
        {
            { HitStopStrength.Light, 0.025f },
            { HitStopStrength.Medium, 0.075f },
            { HitStopStrength.Heavy, 0.15f },
        };
        
        private Dictionary<GameObject, List<IDamageReceiver>> m_hitLists = new();
        
        public void Initialize()
        {}

        public void Shutdown()
        {
            m_hitLists.Clear();
        }


        public bool IsAlreadyInHitList(GameObject attacker, IDamageReceiver dmgReceiver)
        {
            return m_hitLists.ContainsKey(attacker) && m_hitLists[attacker].Contains(dmgReceiver);
        }

        public void AddToHitList(GameObject attacker, IDamageReceiver dmgReceiver)
        {
            if (!m_hitLists.ContainsKey(attacker))
            {
                m_hitLists[attacker] = new List<IDamageReceiver>();
            }
            m_hitLists[attacker].Add(dmgReceiver);
        }

        public void ClearHitList(GameObject attacker)
        {
            if (m_hitLists.TryGetValue(attacker, out var hitList)) hitList.Clear();
        }

        public void ApplyHitEffects(GameObject attacker, Hurtbox hurtbox, DamageScript dmgScript)
        {
            // The appropriate hit effect by priority is:
            // 1. DamageScript's HitEffect
            // 2. Hurtbox's default HitEffect
            // 3. Project default HitEffect (Not implemented yet)

            var hitEffect = hurtbox.GetHitEffect(dmgScript);
            if (hitEffect != null)
            {
                hitEffect.ApplyHitEffect(hurtbox);
            }
            else
            {
                Debug.Log("[CombatSystem] No HitEffect found on DamageScript or Hurtbox, and no project default implemented.");
            }
            
            // Do HitStop
            var attackerHitStop = attacker.GetComponent<HitStopController>();
            var hurtboxHitStop = hurtbox.m_hitStop;
            
            if (attackerHitStop != null) attackerHitStop.TriggerHitStop(dmgScript.m_hitStopFrames);
            if (hurtboxHitStop != null) hurtboxHitStop.TriggerHitStop(dmgScript.m_hitStopFrames);
            
            // Do camera shake (try hurtbox camera, then attacker camera)
            var playerCamera = PlayerManager.GetSystem<PlayerCameraSystem>(hurtbox.gameObject);
            if (playerCamera == null) playerCamera = PlayerManager.GetSystem<PlayerCameraSystem>(attacker.gameObject);
            var shakeParams = CameraShake.CreateShake(dmgScript.m_cameraShake);
            if (playerCamera != null) playerCamera.AddCameraShake(shakeParams);
        }
        
        // Utility Collision Methods
        private static Collider[] s_hitboxOverlaps = new Collider[32];
        
        public static void HitBoxDamage(Vector3 center, Vector3 halfExtents, Quaternion orientation, GameObject attacker, DamageScript dmgScript, CombatAffiliation affiliationTarget)
        {
            var numHits = Physics.OverlapBoxNonAlloc(center, halfExtents, s_hitboxOverlaps, orientation, 1 << PhysicsLayers.TargetingQuery);
            for (int h = 0; h < numHits; h++)
            {
                var hurtbox = s_hitboxOverlaps[h].GetComponent<Hurtbox>();
                if (hurtbox != null)
                {
                    hurtbox.ApplyDamage(attacker, dmgScript, affiliationTarget, out var failReasons);
                    // Failed damage, why?
                    if ((failReasons & DamageFailReasons.IFrames) != 0) return;
                }
            }
        }
    }

    public static class CombatExtensions
    {
        public static bool ApplyDamage(this GameObject gameObject, GameObject attacker, DamageScript dmgScript, CombatAffiliation affiliationTarget,
            out DamageFailReasons failReasons)
        {
            failReasons = DamageFailReasons.None;
            if (gameObject == null || !gameObject.TryGetComponent<Hurtbox>(out var hurtBox)) return false;
            
            return hurtBox.ApplyDamage(attacker, dmgScript, affiliationTarget, out failReasons);
        }
    }
}