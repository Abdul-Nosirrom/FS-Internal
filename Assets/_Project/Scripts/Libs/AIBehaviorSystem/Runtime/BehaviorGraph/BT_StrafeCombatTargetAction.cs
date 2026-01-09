using System;
using FS.AI;
using FS.CombatSystem;
using FS.Math;
using TimeUtils;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using Random = UnityEngine.Random;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Strafe Combat Target", story: "Strafe Around Combat Target", category: "Action", id: "969aa00656c5b2fa1eb48f4bc53591bd")]
public partial class BT_StrafeCombatTargetAction : Action
{
    AIKnowledge m_agentKnowledge;
    private AINavigation m_navigation;
    PerceptionKnowledge m_perceptionKnowledge;
    private GameObject m_target;

    private TimeUntil m_untilNewCombatPosRequest;
    private Vector3 m_combatPos;

    private AIDirector.CombatJob m_job = AIDirector.CombatJob.Idle;
    private DamageScript m_dmgScript = new ObjectHazardKnockbackDamage();
    private PhysicsController m_physics;

    protected override Status OnStart()
    {
        m_agentKnowledge ??= Parent.GameObject.GetComponent<AIKnowledge>();
        m_navigation ??= Parent.GameObject.GetComponent<AINavigation>();
        m_physics ??= Parent.GameObject.GetComponent<PhysicsController>();
        if (m_agentKnowledge == null || m_navigation == null)
            return Status.Failure;
        
        m_perceptionKnowledge ??= m_agentKnowledge.Get<PerceptionKnowledge>();
        bool success = m_perceptionKnowledge != null && m_perceptionKnowledge.PerceptionResult.HasTarget;
        if (success) m_target = m_perceptionKnowledge.PerceptionResult.Target;
        
        success &= AIDirector.Instance.RequestCombatPosition(m_target, Parent.GameObject, out m_combatPos);
        m_untilNewCombatPosRequest = 0.5f;
        
        m_job = AIDirector.CombatJob.Idle;
        
        return success ? Status.Running : Status.Failure;
    }

    protected override Status OnUpdate()
    {
        m_navigation.maxSpeed = m_job == AIDirector.CombatJob.Attack ? 14f : 6f;
        if (m_job == AIDirector.CombatJob.Attack)
        {
            Debug.LogError("Has Combat Job Attack");
            //if (true)//AstarPath.active.Linecast(Parent.GameObject.transform.position, m_target.transform.position))
            //{
            //    m_navigation.SetPath(null);
            //    var toTarget = (m_target.transform.position - Parent.GameObject.transform.position).normalized;
            //    m_physics.SetVelocity(toTarget.ProjectOnPlane(Vector3.up) * (m_navigation.maxSpeed * 1f));
            //}
            //else
            {
                m_navigation.destination = m_target.transform.position;
            }
            var distToTarget = Vector3.Distance(Parent.GameObject.transform.position, m_target.transform.position);
            if (distToTarget < 1.5f)
            {
                // Do attack
                m_target.GetComponentInChildren<Hurtbox>().ApplyDamage(Parent.GameObject, m_dmgScript, CombatAffiliation.Enemy | CombatAffiliation.Player | CombatAffiliation.Neutral);
                CombatService.Instance.ClearHitList(Parent.GameObject);
                AIDirector.Instance.ReleaseCombatJob(m_job);
                m_job = AIDirector.CombatJob.Idle;
            }
        }
        else
        {
            if (m_untilNewCombatPosRequest <= 0)
            {
                m_untilNewCombatPosRequest = Random.Range(0.45f, 2f);
                bool hasPos = AIDirector.Instance.RequestCombatPosition(m_target, Parent.GameObject, out m_combatPos);
                if (!hasPos)
                    return Status.Failure; // not good as we don't have a fallback behavior yet
            }

            float distToCombatPos = Vector3.Distance(Parent.GameObject.transform.position, m_combatPos);
            m_navigation.destination = m_combatPos;
            if (distToCombatPos < 2f)
            {
                AIDirector.Instance.RequestCombatJob(out m_job);
            }
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (AIDirector.Instance) AIDirector.Instance.ReleaseCombatPosition(m_target, Parent.GameObject);
    }
}

