using System;
using System.Collections.Generic;
using System.Linq;
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
        //private const int k_numChannels = 5;
        
        // Get the highest bit position in the enum (calculated at startup)
        private static readonly int k_maxBits = Enum.GetValues(typeof(ActionChannel))
            .Cast<ActionChannel>()
            .Where(v => v != 0)  // Skip zero if present
            .Select(v => GetHighestBitPosition((int)v))
            .DefaultIfEmpty(0)
            .Max() + 1;

        private static int GetHighestBitPosition(int value)
        {
            int position = 0;
            while (value > 0)
            {
                value >>= 1;
                position++;
            }
            return position;
        }
        
        /// <summary>
        /// Iterate over all defined ActionChannel enum values
        /// </summary>
        public static IEnumerable<ActionChannel> IterateChannels()
        {
            for (int i = 0; i < k_maxBits; i++)
            {
                ActionChannel flag = (ActionChannel)(1 << i);
            
                // Only check defined flags
                if (Enum.IsDefined(typeof(ActionChannel), flag))
                {
                    yield return flag;
                }
            }
        }
        
        /// <summary>
        /// Iterate over all defined ActionChannel enum values that are set in the provided channels
        /// E.g. if channels = Movement | Rotation, this will return Movement and Rotation
        /// </summary>
        /// <returns>All channels contained in the passed in channels parameter</returns>
        public static IEnumerable<ActionChannel> IterateChannels(ActionChannel channels)
        {
            for (int i = 0; i < k_maxBits; i++)
            {
                ActionChannel flag = (ActionChannel)(1 << i);
            
                // Only check defined flags
                if (Enum.IsDefined(typeof(ActionChannel), flag) && channels.HasFlag(flag))
                {
                    yield return flag;
                }
            }
        }
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
        
        public IEnumerable<ActionChannel> Channels => m_actionsByChannel.Keys;
        public IEnumerable<GameplayAction> Actions => m_activeActions.ToArray();

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
            => ActionChannelUtils.IterateChannels(channels).Any(channel => m_actionsByChannel.ContainsKey(channel));
        
        
        public bool ContainsAllChannels(ActionChannel channels) 
            => ActionChannelUtils.IterateChannels(channels).All(channel => m_actionsByChannel.ContainsKey(channel));

        public void CancelAllActions()
        {
            while (m_activeActions.Count > 0)
            {
                // HashSet doesn't have indexed access, but we can do this:
                var currentAction = m_activeActions.First(); // Struct enumerator, minimal allocation
        
                // Throw if failed to end the action
                if (!currentAction.TryEndAction(null))
                {
                    throw new NotSupportedException($"[ActionSystem] Action [{currentAction.actionName}] failed to end during cancellation");
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
                }
            }
        }
    }
}