using System;
using System.Collections.Generic;
using System.Linq;
using FS.Extensions;
using FS.RuntimeDebug;
using UnityEditor;
using UnityEngine;

namespace FS.GameplayActions
{
    [DefaultExecutionOrder(-1000)]
    [Icon("ActionController Icon")]
    public class ActionController : MonoBehaviour, IDebugProvider
    {
        private class DisableActionsConstraint : ActionConstraintBase
        {
            public override string Name => "Disable All Actions Constraint";
            protected override void OnEnable() => m_controller.m_activeActions.CancelAllActions();
            public override bool EvaluateConstraint(GameplayAction action) => false;
        }
        
        private class DisableActionChannelConstraint : ActionConstraintBase
        {
            private ActionChannel m_disabledChannels;
            public DisableActionChannelConstraint(ActionChannel channels)
            {
                m_disabledChannels = channels;
            }
            public override string Name => $"Disable Action Channels Constraint: {m_disabledChannels}";
            protected override void OnEnable() 
            {
                m_controller.ActiveActions.CancelActionsInChannels(m_disabledChannels);
            }
            public override bool EvaluateConstraint(GameplayAction action) 
                => !action.Channels.HasFlag(m_disabledChannels);
        }
        
        private ActionConstraintHandler m_constraintHandler;
        private ActionChannelContainer m_activeActions;
        
        public ActionConstraintHandler ConstraintHandler => m_constraintHandler;
        public ActionChannelContainer ActiveActions => m_activeActions;

        private List<GameplayAction> m_allActions;
        
        private Dictionary<Type, GameplayActionSet> m_actionSetLookup = new();
        
        #region INITIALIZATION

        private void Awake()
        {
            m_allActions = GetComponentsInChildren<GameplayAction>().ToList();
            m_constraintHandler = new ActionConstraintHandler(this, m_allActions);
            m_activeActions = new ActionChannelContainer(m_allActions);

            var actionSets = GetComponentsInChildren<GameplayActionSet>();
            foreach (var actionSet in actionSets)
            {
                if (m_actionSetLookup.ContainsKey(actionSet.GetType()))
                {
                    Debug.LogError($"[Action System] Duplicate ActionSet of type {actionSet.GetType()} found on {gameObject.name}. There can only be one of each type.");
                    continue;
                }
                
                m_actionSetLookup.Add(actionSet.GetType(), actionSet);
            }
        }

        private void Start()
        {
            // Initialize actions in Start to ensure that all gameobject monobehaviors are ready & player systems
            foreach (var action in m_allActions)
            {
                action.OnInitialize(gameObject);
            }
        }

        private void OnEnable() => GameActionTicker.Instance.RegisterController(this);
        private void OnDisable() => GameActionTicker.Instance?.UnRegisterController(this);

        #endregion
        
        #region API

        /// <summary>
        /// Iterates all registered actions that implement the given interface.
        /// Zero-allocation struct enumerator — no state machine overhead.
        /// </summary>
        public FilteredActionEnumerator<T> IterateAllActions<T>()
            => new(m_allActions);
        
        /// <summary>
        /// Iterates currently active actions that implement the given interface.
        /// Zero-allocation struct enumerator with re-entrancy safe snapshot iteration.
        /// </summary>
        public FilteredActionEnumerator<T> IterateActiveActions<T>()
            => new(m_activeActions.Actions, m_activeActions);
        
        public bool HasActiveAction<T>() where T : GameplayAction
        {
            foreach (var action in m_activeActions.Actions)
            {
                if (action is T) return true;
            }
            return false;
        }

        public GameplayActionSet GetActionSet(Type actionSetType)
        {
            m_actionSetLookup.TryGetValue(actionSetType, out var actionSet);
            return actionSet;
        }
        public T GetActionSet<T>() where T : GameplayActionSet => GetActionSet(typeof(T)) as T;

        public bool AreActionsDisabled { get; private set; } = false;
        private DisableActionsConstraint m_disableActionsConstraint = ActionConstraintBase.Create<DisableActionsConstraint>();
        
        public void EnableActions()
        {
            if (!AreActionsDisabled) return;
            
            AreActionsDisabled = false;
            ConstraintHandler.RemoveConstraint(m_disableActionsConstraint);
        }
        public void DisableActions()
        {
            if (AreActionsDisabled) return;
            
            AreActionsDisabled = true;
            ConstraintHandler.AddConstraint(m_disableActionsConstraint, true);
        }

        public ActionChannel DisabledChannels { get; private set; } = 0;
        private Dictionary<ActionChannel, DisableActionChannelConstraint> m_disableChannelConstraints = new ();
        
        public void EnableActionChannels(ActionChannel channels)
        {
            // Enable only the channels that are currently disabled
            var channelsToEnable = channels.KeepOnly(DisabledChannels);
            
            foreach (var channel in ActionChannelUtils.IterateChannels(channelsToEnable))
            {
                if (!m_disableChannelConstraints.TryGetValue(channel, out var constraint))
                {
                    constraint = new DisableActionChannelConstraint(channel);
                    m_disableChannelConstraints[channel] = constraint;
                    ConstraintHandler.AddConstraint(constraint);
                }
                
                constraint.DisableConstraint();
                DisabledChannels &= ~channel;
            }
        }
        
        public void DisableActionChannels(ActionChannel channels)
        {
            // Disable only the channels that are currently enabled
            var channelsToDisable = channels.Remove(DisabledChannels);
            
            foreach (var channel in ActionChannelUtils.IterateChannels(channelsToDisable))
            {
                if (!m_disableChannelConstraints.TryGetValue(channel, out var constraint))
                {
                    constraint = new DisableActionChannelConstraint(channel);
                    m_disableChannelConstraints[channel] = constraint;
                    ConstraintHandler.AddConstraint(constraint);
                }
                
                constraint.EnableConstraint();
                DisabledChannels |= channel;
            }
        }
        
        #endregion

        #region UPDATE METHODS

        public void ExecuteFixedUpdate()
        {
            foreach (var action in IterateActiveActions<IActionUpdateReciever>())
            {
                action.OnFixedUpdate();
            }
        }
        
        public void ExecuteUpdate()
        {
            foreach (var action in IterateActiveActions<IActionUpdateReciever>())
            {
                action.OnUpdate();
            }
        }
        
        public void ExecuteLateUpdate()
        {
            foreach (var action in IterateActiveActions<IActionUpdateReciever>())
            {
                action.OnLateUpdate();
            }
        }
        
        #endregion
        
        #region EVENTS

        private void OnCollisionEnter(Collision other)
        {
            foreach (var action in IterateAllActions<IActionCollisionReciever>())
            {
                action.OnCollisionEnter(other);
            }
        }

        private void OnCollisionStay(Collision other)
        {
            foreach (var action in IterateAllActions<IActionCollisionReciever>())
            {
                action.OnCollisionStay(other);
            }
        }

        private void OnCollisionExit(Collision other)
        {
            foreach (var action in IterateAllActions<IActionCollisionReciever>())
            {
                action.OnCollisionExit(other);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            foreach (var action in IterateAllActions<IActionCollisionReciever>())
            {
                action.OnTriggerEnter(other);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            foreach (var action in IterateAllActions<IActionCollisionReciever>())
            {
                action.OnTriggerStay(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            foreach (var action in IterateAllActions<IActionCollisionReciever>())
            {
                action.OnTriggerExit(other);
            }
        }

        #endregion


        public string DebugName => "Action Controller";

        private Dictionary<ActionChannel, bool> debug_channelFoldouts = new();
        
        public void OnDebugGUI()
        {
            var ogColor = GUI.backgroundColor;
            GUILayout.BeginHorizontal();
            foreach (var channel in ActionChannelUtils.IterateChannels())
            {
                debug_channelFoldouts.TryAdd(channel, false);
                bool isOpen = debug_channelFoldouts[channel];
                var activeAction = m_activeActions.ContainsAnyChannel(channel) ? m_activeActions[channel] : null;
                GUI.backgroundColor = activeAction != null ? Color.green : Color.red;
                if (DebugGUI.BeginFoldout(channel.ToString(), ref isOpen))
                {
                    GUILayout.Label(activeAction != null ? $"[{activeAction.m_timeSinceStarted}]: {activeAction.name}" 
                        : "No active action found");

                    DebugGUI.EndFoldout();
                }
                GUI.backgroundColor = ogColor;
                debug_channelFoldouts[channel] = isOpen;
            }
            GUILayout.EndHorizontal();
            GUI.backgroundColor = ogColor;
        }

        // this is the same as IF UNITY_EDITOR
        //[Conditional("UNITY_EDITOR")]
        public void OnDebugDraw()
        {
            if (!Application.isPlaying) return;
            
            foreach (var action in IterateAllActions<IActionGizmoReciever>())
            {
#if UNITY_EDITOR 
                action.OnDrawActionGizmos(Selection.Contains(gameObject));         
#else                
                action.OnDrawActionGizmos(true);
#endif 
            }
        }
    }
}