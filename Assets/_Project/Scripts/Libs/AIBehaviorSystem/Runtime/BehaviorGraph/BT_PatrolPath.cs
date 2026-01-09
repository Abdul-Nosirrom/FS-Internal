using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace FS.AI
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Patrol Path", story: "Patrols A Path", category: "Action", id: "4225f2967263c666facd3cbdd486f308")]
    public partial class BT_PatrolPath : Action
    {
        [SerializeReference] public BlackboardVariable<float> m_patrolSpeed = new(3f);
        
        private AIKnowledge m_agentKnowledge;
        private PatrolKnowledge m_patrolKnowledge;
        private float m_originalSpeed = -1f;
        
        protected override Status OnStart()
        {
            m_agentKnowledge ??= Parent.GameObject.GetComponent<AIKnowledge>();
            if (m_agentKnowledge == null)
                return Status.Failure;
            
            m_patrolKnowledge ??= m_agentKnowledge.GetOrCreate<PatrolKnowledge>();
            m_patrolKnowledge.UpdateKnowledge(Parent.GameObject);
            if (!m_patrolKnowledge.PatrolState.HasValidPath)
                return Status.Failure;

            m_originalSpeed = m_patrolKnowledge.PatrolState.m_navigation.maxSpeed;
            m_patrolKnowledge.PatrolState.m_navigation.maxSpeed = m_patrolSpeed.Value;

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            m_patrolKnowledge.UpdateKnowledge(Parent.GameObject);
            m_patrolKnowledge.PatrolState.UpdateTargetDestination();
            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (m_patrolKnowledge != null && m_patrolKnowledge.PatrolState.m_navigation != null)
                m_patrolKnowledge.PatrolState.m_navigation.maxSpeed = m_originalSpeed;
        }
    }
}