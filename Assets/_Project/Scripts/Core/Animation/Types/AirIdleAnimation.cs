
using Animancer;
using FS.Animation;
using UnityEngine;

[CreateAssetMenu(menuName = "FS/Animation/AirIdle/Simple", fileName = "New Air Idle Animation")]
public class AirIdleAnimation : OneShotAnimation, IAirIdleAnimation
{
    public void UpdateAirIdleFallSpeed(AnimancerState state, PhysicsController physics)
    {
        // no-op
    }
}