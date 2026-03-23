using System;


/// <summary>
/// Provides one-shot event binding for physics events.
/// Callbacks automatically unbind after invocation.
/// </summary>
public static class PhysicsEventBinder
{
    /// <summary>
    /// Binds a one-time callback for the next time the character lands.
    /// </summary>
    public static void OnNextLanding(this PhysicsController physics, Action callback)
    {
        Action handler = null;
        handler = () =>
        {
            physics.OnLandedEvt -= handler;
            callback?.Invoke();
        };
        physics.OnLandedEvt += handler;
    }

    /// <summary>
    /// Binds a one-time callback for the next time the character becomes airborne.
    /// </summary>
    public static void OnNextAirborne(this PhysicsController physics, Action callback)
    {
        Action handler = null;
        handler = () =>
        {
            physics.OnLostGroundEvt -= handler;
            callback?.Invoke();
        };
        physics.OnLostGroundEvt += handler;
    }

    /// <summary>
    /// Binds a one-time callback for when the physics controller enters a specific state.
    /// Unbinds automatically after the target state is reached.
    /// </summary>
    public static void OnNextStateChange(this PhysicsController physics, PhysicsState targetState, Action callback)
    {
        // Already in that state, fire immediately
        if (physics.State == targetState)
        {
            callback?.Invoke();
            return;
        }

        Action<PhysicsState, PhysicsState> handler = null;
        handler = (prev, next) =>
        {
            if (next != targetState) return;
            physics.OnPhysicsStateChanged -= handler;
            callback?.Invoke();
        };
        physics.OnPhysicsStateChanged += handler;
    }

    /// <summary>
    /// Binds a one-time callback for when the physics controller leaves a specific state.
    /// Unbinds automatically after the state is exited.
    /// </summary>
    public static void OnNextStateExit(this PhysicsController physics, PhysicsState exitState, Action callback)
    {
        if (physics.State != exitState)
        {
            callback?.Invoke();
            return;
        }

        Action<PhysicsState, PhysicsState> handler = null;
        handler = (prev, next) =>
        {
            if (prev != exitState) return;
            physics.OnPhysicsStateChanged -= handler;
            callback?.Invoke();
        };
        physics.OnPhysicsStateChanged += handler;
    }
}
