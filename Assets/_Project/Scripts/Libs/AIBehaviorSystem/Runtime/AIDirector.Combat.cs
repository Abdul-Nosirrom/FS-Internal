using System;
using System.Collections.Generic;
using FS.RuntimeDebug;
using TimeUtils;
using UnityEngine;
using UnityEngine.Profiling;
using Random = System.Random;

namespace FS.AI
{
    public partial class AIDirector : IDebugProvider
    {
        private Dictionary<int, CombatCircle> m_combatCircles = new();

        public enum CombatRole
        {
            Melee,
            Hybrid,
            Ranged
        }

        public enum CombatJob
        {
            Attack,
            Strafe,
            Idle
        }
        
        private Dictionary<CombatJob, int> m_combatJobOccupancy = new()
        {
            { CombatJob.Attack, 0 },
            { CombatJob.Strafe, 0 },
            { CombatJob.Idle, 0 }
        };

        protected override void Awake()
        {
            base.Awake();
            DebugManager.Instance.RegisterDebugProvider(gameObject, this);
        }

        private TimeSince m_sinceLastCombatJob;
        
        // Very basic job assignment for now
        public void RequestCombatJob(out CombatJob job)
        {
            if (m_combatJobOccupancy[CombatJob.Attack] == 0 && m_sinceLastCombatJob > UnityEngine.Random.Range(3f, 5f))
            {
                job = CombatJob.Attack;
                m_combatJobOccupancy[job]++;
            }
            else job = CombatJob.Strafe;
        }
        
        public void ReleaseCombatJob(CombatJob job)
        {
            m_combatJobOccupancy[job] = 0;// = Mathf.Max(0, m_combatJobOccupancy[job] - 1);
            m_sinceLastCombatJob = 0;
        }

        public bool RequestCombatPosition(GameObject target, GameObject agent, out Vector3 pos)
        {
            Profiler.BeginSample("Combat Position Request Calculation");
            
            if (!m_combatCircles.ContainsKey(target.GetInstanceID()))
                m_combatCircles[target.GetInstanceID()] = new CombatCircle(target.transform);
            
            var combatCircle = m_combatCircles[target.GetInstanceID()];
            var success = combatCircle.RequestPosition(agent, CombatRole.Melee, out pos);

            Profiler.EndSample();
            return success;
        }
        
        public void ReleaseCombatPosition(GameObject target, GameObject agent)
        {
            if (!m_combatCircles.ContainsKey(target.GetInstanceID()))
                return;
            
            var combatCircle = m_combatCircles[target.GetInstanceID()];
            combatCircle.ReleaseAgent(agent);
        }
        
        #region Debug

        public string DebugName => "AI Director";

        public void OnDebugDraw()
        {
            foreach (var combatCircle in m_combatCircles.Values)
            {
                combatCircle.DrawDebug();
            }
        }

        public void OnDebugGUI()
        {
            GUILayout.Label("Combat Job Occupancy");
            foreach (var job in m_combatJobOccupancy)
            {
                GUILayout.Label($"{job.Key}: {job.Value}");
            }
        }

        #endregion
    }
}