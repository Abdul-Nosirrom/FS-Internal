using System;
using FS.Attributes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FS.CombatSystem
{
    [Flags]
    public enum TargetingTags
    {
        None = 1 << 0, // No targeting tags
        HomingAttack = 1 << 1, // Targeting is homing
        LockOn = 1 << 2, // Targeting is lock-on
    }
    
    public class TargetingQuery : MonoBehaviour, ISelfValidator
    {
        [SerializeField, EnumFlag]
        protected TargetingTags m_targetingTags;

        public bool EvaluateTargetingTags(TargetingSettings settings)
        {
            return (m_targetingTags & settings.FlagsRequiredOnTarget) != 0;
        }

        public void Validate(SelfValidationResult result)
        {
            if (gameObject.layer != PhysicsLayers.TargetingQuery)
            {
                result.AddError($"Incorrect layer for TargetingQuery: {gameObject.layer}. Expected: [TargetingQuery] {PhysicsLayers.TargetingQuery.value}");
            }
        }
    }
}