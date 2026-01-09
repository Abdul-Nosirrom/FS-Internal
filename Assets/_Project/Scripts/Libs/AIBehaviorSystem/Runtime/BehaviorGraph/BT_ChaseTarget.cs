using System;
using FS.AI;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using Random = UnityEngine.Random;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Chase Target", story: "Chase Target", category: "Action", id: "4eec470a64ee91aee6e521e263a3cfa0")]
public partial class BT_ChaseTarget : Action
{
    AIKnowledge m_agentKnowledge;
    private AINavigation m_navigation;
    PerceptionKnowledge m_perceptionKnowledge;
    private GameObject m_target;

    private float m_targetDistance;
    

    protected override Status OnStart()
    {
        m_agentKnowledge ??= Parent.GameObject.GetComponent<AIKnowledge>();
        m_navigation ??= Parent.GameObject.GetComponent<AINavigation>();
        if (m_agentKnowledge == null || m_navigation == null)
            return Status.Failure;
        m_perceptionKnowledge ??= m_agentKnowledge.Get<PerceptionKnowledge>();
        bool success = m_perceptionKnowledge != null && m_perceptionKnowledge.PerceptionResult.HasTarget;
        if (success) m_target = m_perceptionKnowledge.PerceptionResult.Target;
        
        m_targetDistance = Mathf.Lerp(4f, 6f, Random.value);
        
        return success ? Status.Running : Status.Failure;
    }

    protected override Status OnUpdate()
    {
        float distToTarget = Vector3.Distance(Parent.GameObject.transform.position, m_target.transform.position);
        if (distToTarget < m_targetDistance)
            return Status.Success;
        //bool hasPos = AIDirector.Instance.RequestCombatPosition(m_target, Parent.GameObject, out var pos);
        m_navigation.destination = m_target.transform.position;//hasPos ? pos : m_target.transform.position;
        
        if (m_perceptionKnowledge.PerceptionResult.SinceLastSeen > 10f && !m_perceptionKnowledge.PerceptionResult.HasLineOfSight) 
            return Status.Failure;
        return Status.Running;
    }

    protected override void OnEnd()
    {
        //if (AIDirector.Instance) AIDirector.Instance.ReleaseCombatPosition(m_target, Parent.GameObject);
    }
}

