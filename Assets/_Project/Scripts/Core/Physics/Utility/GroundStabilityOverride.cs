using System;
using Lightbug.CharacterControllerPro.Core;
using PrimeTween;
using UnityEngine;

[AddComponentMenu("Free Skies/Physics/Ground Stability Override")]
public class GroundStabilityOverride : MonoBehaviour
{
    [Range(0, 180)] public float m_slopeLimit;
    
    public virtual float GetSlopeLimit(PhysicsController physics)
    {
        return m_slopeLimit;
    }
}

public static class GroundStabilityOverrideExtensions
{
    public static float GetSlopeLimit(this CollisionInfo collision, PhysicsController physics)
    {
        var overrideComponent = collision.hitInfo.transform ? collision.hitInfo.transform.GetComponent<GroundStabilityOverride>() : null;
        if (overrideComponent != null)
        {
            return overrideComponent.GetSlopeLimit(physics);
        }

        float baseLimit = PhysicsController.k_defaultSlopeLimit;
        if (physics.State != PhysicsState.Ground) return baseLimit;
        
        float maxSpeed = physics.LateralPhysicsParams.m_maxSpeed;
        float topSpeedForMaxSlope = physics.LateralPhysicsParams.m_topSpeed * 0.8f; // 80% of top speed == full slope limit
        float speed = physics.LateralSpeed;
        
        float limitAlpha = Mathf.Clamp01(Mathf.InverseLerp(maxSpeed, topSpeedForMaxSlope, speed));
        limitAlpha = Easing.Evaluate(limitAlpha, Ease.InCubic);

        // Dont allow sudden shifts (so for example we're on slope = 0, and run fast into a 90 degree wall, our slope limit should not be 180 degrees), always do it as an increment
        float currentAngle = physics.Ground.GroundSlopeAngle;
        float currentLimit = Mathf.Max(baseLimit, Mathf.Min(180f, currentAngle + Mathf.Lerp(0f, 25f, limitAlpha)));
        return currentLimit;
        return Mathf.Lerp(baseLimit, 180f, limitAlpha);
    }
}