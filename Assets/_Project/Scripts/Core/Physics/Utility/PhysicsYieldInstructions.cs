using System;
using UnityEngine;

/// <summary>
/// Extension methods providing fluent API for creating physics yield instructions.
/// Usage: yield return m_physics.WaitForLanding();
/// </summary>
public static class PhysicsYieldExtensions
{
    /// <summary>
    /// Wait until the physics controller enters the specified state.
    /// </summary>
    public static WaitForPhysicsState WaitForState(this PhysicsController physics, PhysicsState targetState) => new(physics, targetState);

    /// <summary>
    /// Wait until the physics controller leaves the specified state.
    /// </summary>
    public static WaitForPhysicsStateExit WaitForStateExit(this PhysicsController physics, PhysicsState currentState) => new(physics, currentState);

    /// <summary>
    /// Wait until the character lands on ground. Event-driven via OnLandedEvt.
    /// </summary>
    public static WaitForLanding WaitForLanding(this PhysicsController physics) => new(physics);

    /// <summary>
    /// Wait until the character becomes airborne. Event-driven via OnLostGroundEvt.
    /// </summary>
    public static WaitForAirborne WaitForAirborne(this PhysicsController physics) => new(physics);

    /// <summary>
    /// Wait until lateral speed drops below a threshold.
    /// </summary>
    public static WaitForSpeedBelow WaitForSpeedBelow(this PhysicsController physics, float threshold) => new(physics, threshold);

    /// <summary>
    /// Wait until lateral speed exceeds a threshold.
    /// </summary>
    public static WaitForSpeedAbove WaitForSpeedAbove(this PhysicsController physics, float threshold) => new(physics, threshold);
}

/// <summary>
/// Base class for physics-related yield instructions.
/// Implements IDisposable to support proper cleanup when coroutines are stopped early
/// via StopAndDisposeCoroutine.
/// </summary>
public abstract class PhysicsYieldBase : CustomYieldInstruction, IDisposable
{
    protected PhysicsController m_physics;
    private bool m_isDisposed;

    protected PhysicsYieldBase(PhysicsController physics)
    {
        m_physics = physics;
    }

    /// <summary>
    /// Whether the underlying PhysicsController is still valid (not destroyed).
    /// </summary>
    protected bool IsValid => m_physics != null;

    /// <summary>
    /// Called exactly once when the yield instruction completes (normal or early termination).
    /// Override to unsubscribe from events or release resources.
    /// </summary>
    protected virtual void Cleanup() { }

    /// <summary>
    /// Performs cleanup and nulls references. Safe to call multiple times.
    /// Called automatically on normal completion or manually via Dispose() for early termination.
    /// </summary>
    protected void DoCleanup()
    {
        if (m_isDisposed) return;
        m_isDisposed = true;

        Cleanup();
        m_physics = null;
    }

    /// <summary>
    /// Call this when the coroutine is stopped early to ensure proper cleanup.
    /// Typically called via StopAndDisposeCoroutine extension.
    /// </summary>
    public void Dispose() => DoCleanup();
}

/// <summary>
/// Waits until the physics controller enters a specific state.
/// Event-driven via OnPhysicsStateChanged.
/// </summary>
/// <example>
/// yield return m_physics.WaitForState(PhysicsState.Ground);
/// Debug.Log("Character is now grounded!");
/// </example>
public class WaitForPhysicsState : PhysicsYieldBase
{
    private readonly PhysicsState m_targetState;
    private bool m_triggered;

    public WaitForPhysicsState(PhysicsController physics, PhysicsState targetState) : base(physics)
    {
        m_targetState = targetState;

        // Already in desired state, complete immediately
        if (physics.State == targetState)
        {
            m_triggered = true;
            return;
        }

        physics.OnPhysicsStateChanged += OnStateChanged;
    }

    public override bool keepWaiting
    {
        get
        {
            if (m_triggered || !IsValid)
            {
                DoCleanup();
                return false;
            }
            return true;
        }
    }

    private void OnStateChanged(PhysicsState prev, PhysicsState next)
    {
        if (next == m_targetState) m_triggered = true;
    }

    protected override void Cleanup()
    {
        if (m_physics != null)
            m_physics.OnPhysicsStateChanged -= OnStateChanged;
    }
}

/// <summary>
/// Waits until the physics controller leaves a specific state.
/// Event-driven via OnPhysicsStateChanged.
/// </summary>
/// <example>
/// yield return m_physics.WaitForStateExit(PhysicsState.RailGrind);
/// Debug.Log("No longer grinding!");
/// </example>
public class WaitForPhysicsStateExit : PhysicsYieldBase
{
    private readonly PhysicsState m_exitState;
    private bool m_triggered;

    public WaitForPhysicsStateExit(PhysicsController physics, PhysicsState exitState) : base(physics)
    {
        m_exitState = exitState;

        // Not even in this state, complete immediately
        if (physics.State != exitState)
        {
            m_triggered = true;
            return;
        }

        physics.OnPhysicsStateChanged += OnStateChanged;
    }

    public override bool keepWaiting
    {
        get
        {
            if (m_triggered || !IsValid)
            {
                DoCleanup();
                return false;
            }
            return true;
        }
    }

    private void OnStateChanged(PhysicsState prev, PhysicsState next)
    {
        if (prev == m_exitState) m_triggered = true;
    }

    protected override void Cleanup()
    {
        if (m_physics != null)
            m_physics.OnPhysicsStateChanged -= OnStateChanged;
    }
}

/// <summary>
/// Waits until the character lands on ground.
/// Event-driven via OnLandedEvt for zero-polling detection.
/// Completes immediately if already grounded.
/// </summary>
/// <example>
/// yield return m_physics.WaitForLanding();
/// SpawnLandingVFX();
/// </example>
public class WaitForLanding : PhysicsYieldBase
{
    private bool m_triggered;

    public WaitForLanding(PhysicsController physics) : base(physics)
    {
        if (physics.IsGrounded)
        {
            m_triggered = true;
            return;
        }

        physics.OnLandedEvt += OnLanded;
    }

    public override bool keepWaiting
    {
        get
        {
            if (m_triggered || !IsValid)
            {
                DoCleanup();
                return false;
            }
            return true;
        }
    }

    private void OnLanded() => m_triggered = true;

    protected override void Cleanup()
    {
        if (m_physics != null)
            m_physics.OnLandedEvt -= OnLanded;
    }
}

/// <summary>
/// Waits until the character becomes airborne.
/// Event-driven via OnLostGroundEvt for zero-polling detection.
/// Completes immediately if already airborne.
/// </summary>
/// <example>
/// yield return m_physics.WaitForAirborne();
/// EnableAirControl();
/// </example>
public class WaitForAirborne : PhysicsYieldBase
{
    private bool m_triggered;

    public WaitForAirborne(PhysicsController physics) : base(physics)
    {
        if (!physics.IsGrounded)
        {
            m_triggered = true;
            return;
        }

        physics.OnLostGroundEvt += OnLostGround;
    }

    public override bool keepWaiting
    {
        get
        {
            if (m_triggered || !IsValid)
            {
                DoCleanup();
                return false;
            }
            return true;
        }
    }

    private void OnLostGround() => m_triggered = true;

    protected override void Cleanup()
    {
        if (m_physics != null)
            m_physics.OnLostGroundEvt -= OnLostGround;
    }
}

/// <summary>
/// Waits until lateral speed drops below a threshold.
/// Uses polling since PhysicsController doesn't provide speed-based events.
/// </summary>
/// <example>
/// yield return m_physics.WaitForSpeedBelow(2f);
/// Debug.Log("Character nearly stopped");
/// </example>
public class WaitForSpeedBelow : PhysicsYieldBase
{
    private readonly float m_threshold;

    public WaitForSpeedBelow(PhysicsController physics, float threshold) : base(physics)
    {
        m_threshold = threshold;
    }

    public override bool keepWaiting
    {
        get
        {
            if (!IsValid || m_physics.LateralSpeed < m_threshold)
            {
                DoCleanup();
                return false;
            }
            return true;
        }
    }
}

/// <summary>
/// Waits until lateral speed exceeds a threshold.
/// Uses polling since PhysicsController doesn't provide speed-based events.
/// </summary>
/// <example>
/// yield return m_physics.WaitForSpeedAbove(15f);
/// Debug.Log("Character reached high speed");
/// </example>
public class WaitForSpeedAbove : PhysicsYieldBase
{
    private readonly float m_threshold;

    public WaitForSpeedAbove(PhysicsController physics, float threshold) : base(physics)
    {
        m_threshold = threshold;
    }

    public override bool keepWaiting
    {
        get
        {
            if (!IsValid || m_physics.LateralSpeed > m_threshold)
            {
                DoCleanup();
                return false;
            }
            return true;
        }
    }
}
