using System;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;
using FS.Animation;

[Serializable]
public class LocomotionAnimationSet : AnimationSet
{
    public Animation<IIdleAnimation> Idle;
    public Animation<ILocomotionAnimation> LocomotionCycles;
    public Animation<IAirIdleAnimation> AirIdle;
    public Animation<IAirIdleAnimation> VertIdle;
}
    
public class LocomotionAnimationController : AnimationController
{
    public LocomotionAnimationSet LocomotionSet;
    protected override AnimationSet[] InitializeAnimationSets() => new AnimationSet[] { LocomotionSet };

    [SerializeField, Required] 
    protected PhysicsController m_physics;

    private WeaponExtensions m_styles;

    protected override void Awake()
    {
        base.Awake();
        m_styles = GetComponentInParent<WeaponExtensions>();
        if (m_styles && LocomotionSet.Idle.Value is AliyahIdleAnimation aliyahIdles)
            aliyahIdles.SetStylesController(m_styles);
    }

    protected override void AnimationUpdate()
    {
        if (m_physics.IsGrounded)
        {
            if (m_physics.Velocity.sqrMagnitude > 1)
            {
                var state = LocomotionSet.LocomotionCycles.Play(Animator);
                LocomotionSet.LocomotionCycles.Value.UpdateSpeedBlending(state, m_physics);
            }
            else
            {
                LocomotionSet.Idle.Play(Animator);
            }
        }
        else
        {
            if (m_physics.IsInSkateAction && LocomotionSet.VertIdle.Value != null)
                LocomotionSet.VertIdle.Play(Animator);
            else
            {
                var state = LocomotionSet.AirIdle.Play(Animator);
                LocomotionSet.AirIdle.Value.UpdateAirIdleFallSpeed(state, m_physics);
            }
        }
    }
}