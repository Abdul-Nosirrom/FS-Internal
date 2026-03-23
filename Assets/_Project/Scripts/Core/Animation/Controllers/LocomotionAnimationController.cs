using System;
using Sirenix.OdinInspector;
using UnityEngine;
using FS.Animation;
using FS.Animation.HFSM;
using AnimationState = FS.Animation.HFSM.AnimationState;

[Serializable]
public class LocomotionAnimationSet : AnimationSet
{
    public Animation<IIdleAnimation> Idle;
    public Animation<IAnimation> Landing;
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
    
    private AnimationStateMachine m_stateMachine;

    protected override void Awake()
    {
        base.Awake();
        m_styles = GetComponentInParent<WeaponExtensions>();
        if (m_styles && LocomotionSet.Idle.Value is AliyahIdleAnimation aliyahIdles)
            aliyahIdles.SetStylesController(m_styles);

        SetupStateMachine();
    }

    private void SetupStateMachine()
    {
        var locomotionCycles = AnimationState.Create(LocomotionSet.LocomotionCycles, Animator);
        var airIdle = AnimationState.Create(LocomotionSet.AirIdle, Animator);
        var vertIdle = AnimationState.Create(LocomotionSet.VertIdle, Animator);
        var idle = AnimationState.Create(LocomotionSet.Idle, Animator);
        var landing = AnimationState.Create(LocomotionSet.Landing, Animator);

        var groundSM = new AnimationStateMachine(Animator, landing, idle, locomotionCycles);
        var airSM = new AnimationStateMachine(Animator, airIdle, vertIdle);
        m_stateMachine = new AnimationStateMachine(Animator, airSM, groundSM); // Root
        
        locomotionCycles.AddTransition(idle, ShouldEnterIdle);
        idle.AddTransition(locomotionCycles, ShouldExitIdle);
        
        groundSM.AddTransition(airSM, IsInAir);
        airSM.AddTransition(groundSM, IsGrounded);

        landing.AddExitTransition(locomotionCycles, 0.2f, 0.65f, ShouldExitIdle);
        landing.AddExitTransition(idle, 0.4f, 0.75f, ShouldEnterIdle);
        
        m_stateMachine.Init();
    }
    
    protected override void AnimationUpdate()
    {
        // Update parameters
        if (m_physics.IsGrounded && LocomotionSet.LocomotionCycles.TryGetState(Animator, out var locoState))
            LocomotionSet.LocomotionCycles.Value.UpdateSpeedBlending(locoState, m_physics);
        else if (m_physics.State == PhysicsState.Air && LocomotionSet.AirIdle.TryGetState(Animator, out var airIdleState))
            LocomotionSet.AirIdle.Value.UpdateAirIdleFallSpeed(airIdleState, m_physics);
        
        // Update states after parameters been set
        m_stateMachine.Update();
    }

    private bool IsGrounded() => m_physics.IsGrounded;
    private bool IsInAir() => !m_physics.IsGrounded;
    private bool ShouldEnterIdle() => m_physics.Velocity.sqrMagnitude < 1;
    private bool ShouldExitIdle() => m_physics.Velocity.sqrMagnitude >= 1;
}