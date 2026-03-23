using System;
using UnityEngine;

namespace FS.Player
{
    /// <summary>
    /// Extension methods providing fluent API for creating input yield instructions.
    /// Usage: yield return m_input.WaitForPress(GameInput.Jump);
    /// </summary>
    public static class InputYieldExtensions
    {
        /// <summary>
        /// Wait for a button press. Automatically consumes the input when detected.
        /// Optionally specify a timeout; check WasPressed to determine if the press occurred.
        /// </summary>
        /// <param name="input">The input system to monitor.</param>
        /// <param name="action">The button action to wait for.</param>
        /// <param name="timeout">Max time to wait in seconds. 0 or negative = wait forever.</param>
        /// <param name="bAutoConsume">Whether to automatically consume the input when detected.</param>
        public static WaitForButtonPress WaitForPress(this PlayerInputSystem input, GameInput action, float timeout = 0f, bool bAutoConsume = true)
            => new(input, action, timeout, bAutoConsume);

        /// <summary>
        /// Wait for a button release.
        /// </summary>
        public static WaitForButtonRelease WaitForRelease(this PlayerInputSystem input, GameInput action, float timeout = 0f)
            => new(input, action, timeout);

        /// <summary>
        /// Wait for any of the specified buttons to be pressed.
        /// Check TriggeredInput to determine which button was pressed.
        /// </summary>
        public static WaitForAnyButtonPress WaitForAnyPress(this PlayerInputSystem input, float timeout, bool bAutoConsume, params GameInput[] actions)
            => new(input, actions, timeout, bAutoConsume);

        /// <summary>
        /// Wait for any of the specified buttons to be pressed. No timeout, auto-consumes.
        /// Check TriggeredInput to determine which button was pressed.
        /// </summary>
        public static WaitForAnyButtonPress WaitForAnyPress(this PlayerInputSystem input, params GameInput[] actions)
            => new(input, actions, 0f, true);

        /// <summary>
        /// Wait until the move stick exceeds the deadzone threshold.
        /// </summary>
        /// <param name="input">The input system to monitor.</param>
        /// <param name="deadzone">Minimum stick magnitude to count as input (default 0.2).</param>
        /// <param name="timeout">Max time to wait in seconds. 0 or negative = wait forever.</param>
        public static WaitForMoveInput WaitForMoveInput(this PlayerInputSystem input, float deadzone = 0.2f, float timeout = 0f)
            => new(input, deadzone, timeout);
    }

    /// <summary>
    /// Base class for input-related yield instructions.
    /// Implements IDisposable to support proper cleanup when coroutines are stopped early
    /// via StopAndDisposeCoroutine.
    /// </summary>
    public abstract class InputYieldBase : CustomYieldInstruction, IDisposable
    {
        protected PlayerInputSystem m_input;
        private bool m_isDisposed;

        protected InputYieldBase(PlayerInputSystem input)
        {
            m_input = input;
        }

        /// <summary>
        /// Whether the underlying PlayerInputSystem is still valid (not destroyed).
        /// </summary>
        protected bool IsValid => m_input != null;

        /// <summary>
        /// Called exactly once when the yield instruction completes (normal or early termination).
        /// Override to unsubscribe from events or release resources.
        /// </summary>
        protected virtual void Cleanup() { }

        /// <summary>
        /// Performs cleanup and nulls references. Safe to call multiple times.
        /// </summary>
        protected void DoCleanup()
        {
            if (m_isDisposed) return;
            m_isDisposed = true;

            Cleanup();
            m_input = null;
        }

        /// <summary>
        /// Call this when the coroutine is stopped early to ensure proper cleanup.
        /// Typically called via StopAndDisposeCoroutine extension.
        /// </summary>
        public void Dispose() => DoCleanup();
    }

    /// <summary>
    /// Waits for a specific button press, with optional timeout and auto-consume.
    /// Uses polling since PlayerInputSystem doesn't provide press events.
    /// </summary>
    /// <example>
    /// // Wait forever for jump
    /// yield return m_input.WaitForPress(GameInput.Jump);
    /// 
    /// // Wait with timeout, check result
    /// var wait = m_input.WaitForPress(GameInput.Jump, timeout: 0.5f);
    /// yield return wait;
    /// if (wait.WasPressed) { /* success */ }
    /// </example>
    public class WaitForButtonPress : InputYieldBase
    {
        private readonly GameInput m_action;
        private readonly float m_timeout;
        private readonly bool m_bAutoConsume;
        private float m_elapsed;

        /// <summary>
        /// Whether the button was actually pressed (false if timed out or invalidated).
        /// </summary>
        public bool WasPressed { get; private set; }

        public WaitForButtonPress(PlayerInputSystem input, GameInput action, float timeout, bool bAutoConsume) : base(input)
        {
            m_action = action;
            m_timeout = timeout;
            m_bAutoConsume = bAutoConsume;
        }

        public override bool keepWaiting
        {
            get
            {
                if (!IsValid)
                {
                    DoCleanup();
                    return false;
                }

                // Check for press
                if (m_input.GetButton(m_action))
                {
                    WasPressed = true;
                    if (m_bAutoConsume) m_input.ConsumeInput(m_action);
                    DoCleanup();
                    return false;
                }

                // Check timeout
                if (m_timeout > 0f)
                {
                    m_elapsed += Time.deltaTime;
                    if (m_elapsed >= m_timeout)
                    {
                        DoCleanup();
                        return false;
                    }
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Waits for a specific button to be released, with optional timeout.
    /// Useful for charge mechanics or held actions.
    /// </summary>
    /// <example>
    /// yield return m_input.WaitForRelease(GameInput.Jump);
    /// Debug.Log("Jump button released!");
    /// </example>
    public class WaitForButtonRelease : InputYieldBase
    {
        private readonly GameInput m_action;
        private readonly float m_timeout;
        private float m_elapsed;

        /// <summary>
        /// Whether the button was actually released (false if timed out or invalidated).
        /// </summary>
        public bool WasReleased { get; private set; }

        public WaitForButtonRelease(PlayerInputSystem input, GameInput action, float timeout) : base(input)
        {
            m_action = action;
            m_timeout = timeout;
        }

        public override bool keepWaiting
        {
            get
            {
                if (!IsValid)
                {
                    DoCleanup();
                    return false;
                }

                if (m_input.GetButtonRelease(m_action))
                {
                    WasReleased = true;
                    DoCleanup();
                    return false;
                }

                if (m_timeout > 0f)
                {
                    m_elapsed += Time.deltaTime;
                    if (m_elapsed >= m_timeout)
                    {
                        DoCleanup();
                        return false;
                    }
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Waits for any of the specified buttons to be pressed.
    /// Check TriggeredInput after completion to see which button was pressed.
    /// </summary>
    /// <example>
    /// var wait = m_input.WaitForAnyPress(GameInput.Jump, GameInput.Attack);
    /// yield return wait;
    /// if (wait.WasPressed)
    ///     Debug.Log($"Player pressed: {wait.TriggeredInput}");
    /// </example>
    public class WaitForAnyButtonPress : InputYieldBase
    {
        private readonly GameInput[] m_actions;
        private readonly float m_timeout;
        private readonly bool m_bAutoConsume;
        private float m_elapsed;

        /// <summary>
        /// Whether any button was actually pressed (false if timed out or invalidated).
        /// </summary>
        public bool WasPressed { get; private set; }

        /// <summary>
        /// Which button was pressed. Only valid when WasPressed is true.
        /// </summary>
        public GameInput TriggeredInput { get; private set; }

        public WaitForAnyButtonPress(PlayerInputSystem input, GameInput[] actions, float timeout, bool bAutoConsume) : base(input)
        {
            m_actions = actions;
            m_timeout = timeout;
            m_bAutoConsume = bAutoConsume;
        }

        public override bool keepWaiting
        {
            get
            {
                if (!IsValid)
                {
                    DoCleanup();
                    return false;
                }

                for (int i = 0; i < m_actions.Length; i++)
                {
                    if (m_input.GetButton(m_actions[i]))
                    {
                        WasPressed = true;
                        TriggeredInput = m_actions[i];
                        if (m_bAutoConsume) m_input.ConsumeInput(m_actions[i]);
                        DoCleanup();
                        return false;
                    }
                }

                if (m_timeout > 0f)
                {
                    m_elapsed += Time.deltaTime;
                    if (m_elapsed >= m_timeout)
                    {
                        DoCleanup();
                        return false;
                    }
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Waits until the move stick exceeds a deadzone threshold.
    /// Useful for "wait for player to choose a direction" patterns.
    /// </summary>
    /// <example>
    /// var wait = m_input.WaitForMoveInput();
    /// yield return wait;
    /// Vector2 direction = wait.MoveVector;
    /// </example>
    public class WaitForMoveInput : InputYieldBase
    {
        private readonly float m_deadzone;
        private readonly float m_timeout;
        private float m_elapsed;

        /// <summary>
        /// Whether move input was detected (false if timed out or invalidated).
        /// </summary>
        public bool WasDetected { get; private set; }

        /// <summary>
        /// The move vector at the moment input was detected. Only valid when WasDetected is true.
        /// </summary>
        public Vector2 MoveVector { get; private set; }

        public WaitForMoveInput(PlayerInputSystem input, float deadzone, float timeout) : base(input)
        {
            m_deadzone = deadzone;
            m_timeout = timeout;
        }

        public override bool keepWaiting
        {
            get
            {
                if (!IsValid)
                {
                    DoCleanup();
                    return false;
                }

                var move = m_input.MoveVector;
                if (move.sqrMagnitude > m_deadzone * m_deadzone)
                {
                    WasDetected = true;
                    MoveVector = move;
                    DoCleanup();
                    return false;
                }

                if (m_timeout > 0f)
                {
                    m_elapsed += Time.deltaTime;
                    if (m_elapsed >= m_timeout)
                    {
                        DoCleanup();
                        return false;
                    }
                }

                return true;
            }
        }
    }
}