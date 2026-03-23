
using Animancer;

public interface IMecanimGenericAnimation : ILocomotionAnimation, IIdleAnimation, IAirIdleAnimation 
{
    public void UpdatePhysics(AnimancerState state, PhysicsController physics);    
}

public interface ILocomotionAnimation
{
    public void UpdateSpeedBlending(AnimancerState state, PhysicsController physics);
}

public interface IIdleAnimation {}

public interface IAirIdleAnimation
{
    public void UpdateAirIdleFallSpeed(AnimancerState state, PhysicsController physics);
}