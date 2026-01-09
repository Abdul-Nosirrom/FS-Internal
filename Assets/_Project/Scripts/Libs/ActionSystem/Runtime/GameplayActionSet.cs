using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace FS.GameplayActions
{
    public abstract class GameplayActionSet : MonoBehaviour
    {
        protected List<GameplayAction> m_actions;

#if UNITY_EDITOR        
        private void OnValidate()
        {
            //var fields = GetType().GetFields();
            //foreach (var field in fields)
            //{
            //    if (!field.FieldType.IsSubclassOf(typeof(GameplayAction))) continue;
            //
            //    if (field.GetValue(this) is GameplayAction action)
            //    {
            //        // First check if an action was reassigned and reset its name
            //        var reassignedActions = transform.root.GetComponentsInChildren(field.FieldType, true);
            //        foreach (var reassigned in reassignedActions)
            //        {
            //            var reassignedAction = (GameplayAction)reassigned;
            //            if (reassignedAction.name == field.Name && reassignedAction != action)
            //                reassignedAction.actionName = $"[UnAssigned] {field.FieldType.Name}";
            //        }
            //        
            //        action.actionName = field.Name;
            //    }
            //}
        }
#endif        

        // TODO: Return m_actions if its been loaded during runtime/dont use reflection in runtime
        // public IEnumerable<GameplayAction> GetActions()
        // {
        //     // Lazy load
        //     if (m_actions != null)
        //         foreach (var action in m_actions) yield return action;
        //     else
        //     {
        //         m_actions = new();
        //         
        //         var fields = GetType().GetFields();
        //         foreach (var field in fields)
        //         {
        //             if (!field.FieldType.IsSubclassOf(typeof(GameplayAction))) continue;
        //
        //             if (field.GetValue(this) is GameplayAction action)
        //             {
        //                 m_actions.Add(action);
        //                 yield return action;
        //             }
        //         }
        //     }
        // }
    }
}