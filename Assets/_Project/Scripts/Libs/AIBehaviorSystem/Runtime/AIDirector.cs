using System;
using System.Collections.Generic;
using FS.AI;
using FS.Patterns;
using UnityEngine;
using UnityEngine.Profiling;

namespace FS.AI
{
    public partial class AIDirector : PersistentSingleton<AIDirector>
    {
        private void RegisterAIModule<T>(T module, List<T> list)
        {
            if (!list.Contains(module))
                list.Add(module);
        }
        
        private void UnregisterAIModule<T>(T module, List<T> list)
        {
            if (list.Contains(module))
                list.Remove(module);
        }
        
        #region AI Knowledge Updates

        private List<AIKnowledge> m_aiKnowledge = new();
        
        public void RegisterAIKnowledge(AIKnowledge knowledge) => RegisterAIModule(knowledge, m_aiKnowledge);
        public void UnregisterAIKnowledge(AIKnowledge knowledge) => UnregisterAIModule(knowledge, m_aiKnowledge);

        private void Update()
        {
            Profiler.BeginSample("AI Knowledge Updates");
            foreach (var knowledge in m_aiKnowledge)
            {
                knowledge.UpdateKnowledge();
            }
            Profiler.EndSample();
        }

        #endregion
        
        #region Patrol Path Requests
        
        private List<PatrolPath> m_patrolPaths = new();
        public void RegisterPatrolPath(PatrolPath path) => RegisterAIModule(path, m_patrolPaths);
        public void UnregisterPatrolPath(PatrolPath path) => UnregisterAIModule(path, m_patrolPaths);

        public bool RequestPatrolPathForAgent(ref PatrolFollowState patrolState)
        {
            if (patrolState.m_navigation == null)
            {
                Debug.LogError("[AI System] PatrolFollowState has no Navigation component assigned.");
                return false;
            }
            // Get closest patrol path
            PatrolPath bestPath = null;
            var bestDistance = float.MaxValue;
            foreach (var path in m_patrolPaths)
            {
                var nearestPos = path.GetNearstPatrolPoint(patrolState.m_navigation.transform.position);
                var distance = Vector3.Distance(patrolState.m_navigation.transform.position, nearestPos.m_position);
                if (distance < bestDistance && path.ClaimPath(ref patrolState))
                {
                    bestDistance = distance;
                    bestPath = path;
                }
            }

            return bestPath != null;
        }
        
        #endregion
    }
}