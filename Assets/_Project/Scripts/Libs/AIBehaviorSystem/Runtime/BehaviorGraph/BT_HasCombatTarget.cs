using System;
using FS.AI;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Has Combat Target", story: "Agent has valid combat target", category: "Conditions", id: "05fa2cd6e932565f1156b8000da3366f")]
public partial class BT_HasCombatTarget : Condition
{
    private AIKnowledge m_agentKnowledge;
    private PerceptionKnowledge m_perceptionKnowledge;
    
    public override bool IsTrue()
    {
        if (m_perceptionKnowledge == null) return false;
        bool result = m_perceptionKnowledge.PerceptionResult.Target != null;
        return result;
    }

    public override void OnStart()
    {
        m_agentKnowledge ??= GameObject.GetComponent<AIKnowledge>();
        if (m_agentKnowledge == null)
            return;
        
        m_perceptionKnowledge ??= m_agentKnowledge.GetOrCreate<PerceptionKnowledge>();
    }
}
