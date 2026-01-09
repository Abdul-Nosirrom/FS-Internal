using System;
using System.Collections.Generic;
using FS.GameplayActions;
using Unity.Behavior;
using Unity.Properties;
using UnityEditor;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace FS.AI
{
    [Serializable]
    public class AIActionRef
    {
        public ActionReference Action;
    }

    [Serializable]
    public class TestBlackboardValue : BlackboardVariable<AIActionRef>
    {
        public TestBlackboardValue() {}
        public TestBlackboardValue(AIActionRef actionRef)
            : base(actionRef)
        { }
    }
    
    [Serializable]
    public class BehaviorGraphAction : BlackboardVariable<string>
    {
        [SerializeField] public ActionReference m_action;
        public BehaviorGraphAction() {}
        public BehaviorGraphAction(string actionName)
            : base(actionName)
        { }
        
        // [SerializeField] protected ActionReference m_action;
        // public GameplayAction Get(ActionController controller) => m_action.GetAction(controller);
        //
        //
        // public override void SetObjectValueWithoutNotify(object newValue)
        // {
        //     base.SetObjectValueWithoutNotify(newValue);
        //     Debug.LogError("TEST");
        // }
        //
        //
        // private void AssignActionReference()
        // {
        //     if (Value.Equals(string.Empty)) return;
        //     
        //     Debug.LogError($"Attempting To Assign Action Reference for {Value}");
        //     string[] actionInfo = Value.Split('/');
        //     if (actionInfo.Length != 2)
        //     {
        //         Debug.LogError("Invalid Action Name format. Expected 'SetName/ActionName'");
        //         Value = string.Empty;
        //         return;
        //     }
        //     
        //     var setName = actionInfo[0];
        //     var actionName = actionInfo[1];
        //     
        //     m_action.SetName = setName;
        //     m_action.ActionName = actionName;
        //     if (!ActionReference.Validate(m_action, out _, out _, out var errorMsg))
        //     {
        //         Debug.LogError("[AI] " + errorMsg);
        //         Value = string.Empty;
        //     }
        // }
    }
    
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Run Gameplay Action",
        description: "Runs a gameplay action. This is a base class for all gameplay actions.",
        category: "Action/Gameplay Action",
        story: "[Agent] Performs [GameplayAction]")]
    public partial class BT_GameplayAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public TestBlackboardValue ActionToStart;
        [SerializeReference] public BehaviorGraphAction GameplayAction = new();
        
        [CreateProperty] public AIActionRef ActionRef;
        [CreateProperty] public float TestFloat;
        [CreateProperty] public ActionReference Action;
        
        private ActionController m_actionController;
        
        protected override Status OnStart()
        {
            m_actionController = Agent.Value.GetComponent<ActionController>();
            if (m_actionController == null)
                return Status.Failure;

            var action = Action.GetAction(m_actionController);
            return action.TryStartAction() 
                ? Status.Running : Status.Failure;
        }

        protected override Status OnUpdate()
        {
            var action = Action.GetAction(m_actionController);

            bool isRunning = action.IsActive;
            bool wasInterrupted = action.m_interruptorAction != null;
            // TODO: Going into hit-stun is also an interruption... should be something properly handled in the ActionController to have flags for cancellation reasons?
            return isRunning ? Status.Running :
                wasInterrupted ? Status.Interrupted : Status.Success;
        }

        protected override void OnEnd()
        {
        }
    }
}