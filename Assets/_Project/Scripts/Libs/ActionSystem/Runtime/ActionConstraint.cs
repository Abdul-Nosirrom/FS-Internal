using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS.GameplayActions
{
    /// <summary>
    /// Base class for action constraints.
    /// Action Constraints act as "global conditions" that are added to the action controller, and can be enabled/disabled.
    /// They are not tied to a singular action, they act as conditions on the control flow of the action system.
    /// </summary>
    /// <example>
    /// <code>
    /// bool CanStartAction(GameplayAction action)
    /// {
    ///     // Constraint will be checked for any action wanting to start
    ///     foreach (var constraint in m_constraints)
    ///     {
    ///         if (constraint.IsEnabled &amp;&amp; !constraint.Check(action))
    ///             return false;
    ///     }
    ///     return action.CanEnter();
    /// }
    /// </code>
    /// </example>
    public abstract class ActionConstraintBase
    {
        public static T Create<T>(string name = null) where T : ActionConstraintBase
        {
            var constraint = Activator.CreateInstance<T>();
            // Only set the name if it wasn't explicitly overridden with a getter-only property
            var nameProperty = typeof(T).GetProperty("Name");
            if (nameProperty != null && nameProperty.CanWrite)
            {
                constraint.Name = name;
            }
            return constraint;
        }

        /// <summary>
        /// Unique identifier for this instance of the ActionConstraint that exists on the ActionController
        /// </summary>
        public virtual string Name { get; protected set; }
        public bool IsEnabled { get; private set; }
        
        public ActionController m_controller { get; private set; }

        public void Initialize(ActionController controller)
        {
            m_controller = controller;
        }
        
        protected virtual void OnEnable() {}

        protected virtual void OnDisable() {}

        public virtual bool EvaluateConstraint(GameplayAction action) => true;
        
        public void EnableConstraint()
        {
            if (!IsEnabled)
            {
                IsEnabled = true;
                OnEnable();
            }
        }

        public void DisableConstraint()
        {
            if (IsEnabled)
            {
                IsEnabled = false;
                OnDisable();
            }
        }
    }

    public class ActionConstraintHandler
    {
        private Dictionary<string, ActionConstraintBase> m_constraints = new();
        private ActionController m_controller;
        
        /// <summary>
        /// Constructor registers event handlers for the provided actions, but does not activate them.
        /// </summary>
        public ActionConstraintHandler(ActionController controller, List<GameplayAction> actions)
        {
            foreach (var action in actions)
            {
                action.OnActionConditionEvaluation += EvaluateConstraints;
                
                // Add default constraint
                if (action.DefaultConstraint != null)
                {
                    AddConstraint(action.DefaultConstraint);
                    action.OnActionStarted += EnableDefaultConstraint;
                    action.OnActionEnded += DisableDefaultConstraint;
                }
            }

            m_controller = controller;
        }

        private void EnableDefaultConstraint(GameplayAction action) => m_constraints[action.DefaultConstraint.Name].EnableConstraint();

        private void DisableDefaultConstraint(GameplayAction action) => m_constraints[action.DefaultConstraint.Name].DisableConstraint();
        
        public void AddConstraint(ActionConstraintBase constraint, bool shouldEnable = false)
        {
            if (!m_constraints.TryAdd(constraint.Name, constraint))
                Debug.LogWarning($"[ActionSystem] Constraint with name {constraint.Name} already exists!");
            else
            {
                constraint.Initialize(m_controller);
                if (shouldEnable) constraint.EnableConstraint();
            }
            
        }

        public void RemoveConstraint(ActionConstraintBase constraint)
        {
            if (m_constraints.ContainsKey(constraint.Name))
            {
                if (constraint.IsEnabled) constraint.DisableConstraint();
                m_constraints.Remove(constraint.Name);
            }
            else 
                Debug.LogWarning($"[ActionSystem] Removing constraint with name {constraint.Name} does not exist!");
        }

        public ActionConstraintBase GetConstraint(string name)
        {
            if (m_constraints.TryGetValue(name, out var constraint)) return constraint;
            Debug.LogWarning($"[ActionSystem] Constraint with name {name} does not exist!");
            return null;
        }

        private bool EvaluateConstraints(GameplayAction action)
        {
            foreach (var constraint in m_constraints.Values)
            { 
                if (constraint.IsEnabled && !constraint.EvaluateConstraint(action))
                    return false;
            }
            return true;
        }
        
#if UNITY_EDITOR
        public List<ActionConstraintBase> Editor_Constraints => new List<ActionConstraintBase>(m_constraints.Values);
#endif        
    }
}