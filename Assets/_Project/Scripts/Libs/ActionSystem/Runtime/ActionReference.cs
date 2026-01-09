using System;
using System.Reflection;
using UnityEngine;

namespace FS.GameplayActions
{
    /// <summary>
    /// ActionSystem equivalent of <see cref="FS.Animation.AnimationReference"/>
    /// </summary>
    [Serializable]
    public struct ActionReference
    {
        // Editor will show a single drop-down for both the ActionSet and the Action, so we can select them from the inspector.
        [HideInInspector] public string SetName; // To an ActionSet
        [HideInInspector] public string ActionName; // To an GameplayAction
        
        private Type m_actionSetType;
        private FieldInfo m_actionField;
        
        public static ActionReference Get<T>(string actionName) where T : GameplayActionSet
        {
            ActionReference reference = new ActionReference()
            {
                SetName = typeof(T).FullName,
                ActionName = actionName,
                m_actionSetType = typeof(T),
                m_actionField = GetActionField(typeof(T), actionName)
            };
            if (reference.m_actionField == null)
            {
                Debug.LogError($"Action '{actionName}' does not exist in Action Set '{typeof(T).Name}'.");
            }
            return reference;
        }
        
        private static FieldInfo GetActionField(Type actionSetType, string actionName)
        {
            if (actionSetType == null || string.IsNullOrEmpty(actionName))
            {
                Debug.LogError("Invalid Action Set type or Action Name provided.");
                return null;
            }
            
            var field = actionSetType.GetField(actionName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field;
        }
        
        public static bool Validate(ActionReference reference, out Type outType, out FieldInfo outField, out string errorMsg)
        {
            errorMsg = String.Empty;
            
            if (reference.SetName == null || reference.ActionName == null)
            {
                errorMsg = "ActionReference is not initialized properly. SetName and ActionName must be set.";
                outType = null;
                outField = null;
                return false;
            }
            
            outType = Type.GetType(reference.SetName);
            if (outType == null)
            {
                errorMsg = $"Action Set type '{reference.SetName}' does not exist.";
                outField = null;
                return false;
            }
            
            outField = GetActionField(outType, reference.ActionName);
            if (outField == null)
            {
                errorMsg = $"Action '{reference.ActionName}' does not exist in Valid Action Set '{reference.SetName}'.";
                return false;
            }

            return true;
        }

        public void Reset()
        {
            Debug.Log("Called Reset on ActionReference");
            SetName = null;
            ActionName = null;
        }

        public GameplayAction GetAction(ActionController actionController)
        {
            if (GetAction_Internal(actionController, out var action)) return action;
            
            Debug.LogError($"Failed to get action for Set: {SetName}, Action: {ActionName}");
            return null;
        }

        private bool GetAction_Internal(ActionController actionController, out GameplayAction action)
        {
            action = null;
            
            if (m_actionField == null || m_actionSetType == null)
            {
                if (!Validate(this, out m_actionSetType, out m_actionField, out var errorMsg))
                {
                    Debug.LogError($"Failed to initialize ActionReference for Set: {SetName}, Action: {ActionName}\n Error: {errorMsg}");
                    return false;
                }
            }
            
            // Get the ActionSet instance from the actionController, and play the action on it
            var actionSet = actionController.GetActionSet(m_actionSetType);
            if (actionSet == null)
            {
                Debug.LogError($"Animator {actionController.gameObject.name} does not have ActionSet of type: {m_actionSetType}");
                return false;
            }
            
            action = m_actionField.GetValue(actionSet) as GameplayAction;

            return true;
        }
    }
}