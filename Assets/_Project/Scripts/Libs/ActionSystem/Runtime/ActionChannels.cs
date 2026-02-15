using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace FS.GameplayActions
{
    [Flags]
    public enum ActionChannel
    {
        // We split the vertical physics and lateral physics channels for one reason. Locomotion won't be an action, and
        // knowing what kind of physics is currently being overridden helps it identify what to perform. E.g skip applying gravity
        // if vertical physics is occupied.
        PhysicsLateral = 1 << 0, // 0001
        PhysicsVertical = 1 << 1, // 0010
        Physics = PhysicsLateral | PhysicsVertical, // 0011
        
        Rotation = 1 << 2,

        Animation = 1 << 3,
        
        Tricks = 1 << 4,
        Combat = 1 << 5,
        
        PhysicsConstraint = 1 << 6
    }
    
    public static class ActionChannelUtils
    {
        // Get the highest bit position in the enum (calculated at startup)
        private static readonly int k_maxBits = CalculateMaxBits();

        /// <summary>
        /// Bitmask of all defined single-bit flags (excludes composites like Physics).
        /// Pre-computed at startup to avoid Enum.IsDefined per iteration.
        /// </summary>
        private static readonly int k_definedBitMask = CalculateDefinedBitMask();

        private static int CalculateMaxBits()
        {
            int max = 0;
            foreach (ActionChannel v in Enum.GetValues(typeof(ActionChannel)))
            {
                if (v == 0) continue;
                int pos = 0;
                int val = (int)v;
                while (val > 0) { val >>= 1; pos++; }
                if (pos > max) max = pos;
            }
            return max;
        }

        private static int CalculateDefinedBitMask()
        {
            int mask = 0;
            for (int i = 0; i < k_maxBits; i++)
            {
                int flag = 1 << i;
                if (Enum.IsDefined(typeof(ActionChannel), (ActionChannel)flag))
                {
                    mask |= flag;
                }
            }
            return mask;
        }
        
        /// <summary>
        /// Iterate over all defined ActionChannel enum values
        /// </summary>
        public static ChannelEnumerator IterateChannels()
            => new((ActionChannel)k_definedBitMask);
        
        /// <summary>
        /// Iterate over all defined ActionChannel enum values that are set in the provided channels.
        /// E.g. if channels = Movement | Rotation, this will return Movement and Rotation.
        /// </summary>
        /// <returns>All channels contained in the passed in channels parameter</returns>
        public static ChannelEnumerator IterateChannels(ActionChannel channels)
            => new(channels);

        /// <summary>
        /// Zero-allocation struct enumerator for iterating individual channel flags.
        /// Uses bit manipulation to extract set bits — no Enum.IsDefined or state machine overhead.
        /// </summary>
        public struct ChannelEnumerator
        {
            private int m_remaining;
            private ActionChannel m_current;

            internal ChannelEnumerator(ActionChannel channels)
            {
                // Mask to only defined single-bit flags (filters out composites like Physics)
                m_remaining = (int)channels & k_definedBitMask;
                m_current = 0;
            }

            public ActionChannel Current => m_current;

            public bool MoveNext()
            {
                if (m_remaining == 0) return false;
                
                // Extract lowest set bit
                int lowest = m_remaining & (-m_remaining);
                m_current = (ActionChannel)lowest;
                m_remaining &= ~lowest;
                return true;
            }

            public ChannelEnumerator GetEnumerator() => this;
        }
    }
    
    /// <summary>
    /// Zero-allocation struct enumerator that filters a list of actions by interface/type.
    /// Manages iteration depth on <see cref="ActionChannelContainer"/> automatically to prevent
    /// buffer rebuilds during iteration (re-entrancy safety).
    /// <para>foreach calls Dispose on loop exit (including break/return), which decrements the depth counter.</para>
    /// </summary>
    public struct FilteredActionEnumerator<T> : IDisposable
    {
        private readonly IReadOnlyList<GameplayAction> m_source;
        private readonly ActionChannelContainer m_container; // null for non-active-action iteration (e.g. m_allActions)
        private int m_index;
        private T m_current;
        private bool m_started;

        internal FilteredActionEnumerator(IReadOnlyList<GameplayAction> source, ActionChannelContainer container = null)
        {
            m_source = source;
            m_container = container;
            m_index = -1;
            m_current = default;
            m_started = false;
        }

        public T Current => m_current;

        public bool MoveNext()
        {
            // Begin tracking iteration depth on first MoveNext, not construction,
            // so there's no cost if enumerator is created but never iterated
            if (!m_started && m_container != null)
            {
                m_container.BeginIteration();
                m_started = true;
            }
            
            while (++m_index < m_source.Count)
            {
                if (m_source[m_index] is T typed)
                {
                    m_current = typed;
                    return true;
                }
            }

            // Exhausted — release depth lock
            Dispose();
            return false;
        }

        /// <summary>
        /// Called automatically by foreach on any exit path (completion, break, return, exception).
        /// Decrements iteration depth so the buffer can rebuild on next access.
        /// </summary>
        public void Dispose()
        {
            if (m_started && m_container != null)
            {
                m_container.EndIteration();
                m_started = false;
            }
        }

        public FilteredActionEnumerator<T> GetEnumerator() => this;
    }
    
    /// <summary>
    /// Container class responsible for managing the active actions in each channels.
    /// </summary>
    public class ActionChannelContainer
    {
        private readonly Dictionary<ActionChannel, GameplayAction> m_actionsByChannel = new();
        
        /// <summary>
        /// Cached active actions for quick iteration as a hashset, as dict.Values creates duplicates
        /// </summary>
        private readonly HashSet<GameplayAction> m_activeActions = new();
        
        /// <summary>
        /// Reusable buffer for safe iteration over active actions.
        /// Avoids allocations from ToArray() while preventing collection-modified exceptions
        /// when actions start/end during iteration. Only rebuilt when dirty AND no one is
        /// currently iterating (depth == 0).
        /// </summary>
        private readonly List<GameplayAction> m_iterationBuffer = new();
        private bool m_iterationBufferDirty = true;
        private int m_iterationDepth;
        
        public IEnumerable<ActionChannel> Channels => m_actionsByChannel.Keys;
        
        /// <summary>
        /// Returns a snapshot of currently active actions safe for iteration.
        /// Defers rebuilds while iteration is in progress to prevent re-entrancy corruption.
        /// The returned list is reused — do not cache across frames.
        /// </summary>
        public IReadOnlyList<GameplayAction> Actions
        {
            get
            {
                if (m_iterationBufferDirty && m_iterationDepth == 0)
                {
                    m_iterationBuffer.Clear();
                    m_iterationBuffer.AddRange(m_activeActions);
                    m_iterationBufferDirty = false;
                }

                return m_iterationBuffer;
            }
        }
        
        /// <summary>Increment iteration depth — prevents buffer rebuild while iterating.</summary>
        internal void BeginIteration() => m_iterationDepth++;
        
        /// <summary>Decrement iteration depth — allows buffer rebuild once all iterators are done.</summary>
        internal void EndIteration() => m_iterationDepth--;

        /// <summary>
        /// Constructor registers event handlers for the provided actions, but does not activate them.
        /// </summary>
        public ActionChannelContainer(List<GameplayAction> actions)
        {
            foreach (var action in actions)
            {
                action.OnActionStarted += OnActionStarted;
                action.OnActionEnded += OnActionEnded;
            }
        }

        public GameplayAction this[ActionChannel channel]
        {
            get => m_actionsByChannel[channel];
            private set => m_actionsByChannel[channel] = value;
        }

        public bool ContainsAnyChannel(ActionChannel channels)
        {
            foreach (var channel in ActionChannelUtils.IterateChannels(channels))
            {
                if (m_actionsByChannel.ContainsKey(channel)) return true;
            }
            return false;
        }        
        
        public bool ContainsAllChannels(ActionChannel channels)
        {
            foreach (var channel in ActionChannelUtils.IterateChannels(channels))
            {
                if (!m_actionsByChannel.ContainsKey(channel)) return false;
            }
            return true;
        }

        public void CancelAllActions()
        {
            while (m_activeActions.Count > 0)
            {
                // HashSet.GetEnumerator() returns a struct enumerator — no allocation
                using var enumerator = m_activeActions.GetEnumerator();
                if (!enumerator.MoveNext()) break;
                var currentAction = enumerator.Current;

                // Throw if failed to end the action
                if (!currentAction!.TryEndAction(null))
                {
                    throw new NotSupportedException(
                        $"[ActionSystem] Action [{currentAction.actionName}] failed to end during cancellation");
                }
        
                m_activeActions.Remove(currentAction);
            }
        }
        
        public int CancelActionsInChannels(ActionChannel channels)
        {
            int cancelledCount = 0;
            foreach (var channel in ActionChannelUtils.IterateChannels(channels))
            {
                if (m_actionsByChannel.TryGetValue(channel, out var occupyingAction))
                {
                    bool endedOccupyingAction = occupyingAction.TryEndAction(null); // NOTE: be wary of Assert stripping, i.e do not put the function itself inside the assert
                    if (!endedOccupyingAction)
                        Debug.LogError($"[ActionSystem] Action [{occupyingAction.actionName}] failed to end in channel during cancellation");
                    cancelledCount++;
                }
            }

            return cancelledCount;
        }

        private void OnActionStarted(GameplayAction startedAction)
        {
            // Ensure that action is occupying all places it needs to in the dictionary
            foreach (var channel in ActionChannelUtils.IterateChannels(startedAction.Channels))
            {
                m_actionsByChannel.TryGetValue(channel, out var occupyingAction);
                
                if (occupyingAction != startedAction)
                {
                    bool endedOccupyingAction = occupyingAction == null || occupyingAction.TryEndAction(startedAction); // NOTE: Same assert stripping note as below, this caused bugs in Release builds of not cancelling the occupying action lol
                    Assert.IsTrue(endedOccupyingAction, 
                        "[ActionSystem] Action failed to end in channel");
                    this[channel] = startedAction;
                    m_activeActions.Add(startedAction);
                    m_iterationBufferDirty = true;
                }
            }
        }
        private void OnActionEnded(GameplayAction endedAction)
        {
            // Ensure that action is no longer in the dictionary for the channels it occupies
            foreach (var channel in ActionChannelUtils.IterateChannels(endedAction.Channels))
            {
                // NOTE: be wary of Assert stripping, i.e do not put the function itself inside the assert
                bool isFound = m_actionsByChannel.TryGetValue(channel, out var occupyingAction);
                Assert.IsTrue(isFound, 
                    "[ActionSystem] Action not found in channel while we're ending it");
                
                if (occupyingAction == endedAction)
                {
                    m_actionsByChannel.Remove(channel);
                    m_activeActions.Remove(endedAction);
                    m_iterationBufferDirty = true;
                }
            }
        }
    }
}