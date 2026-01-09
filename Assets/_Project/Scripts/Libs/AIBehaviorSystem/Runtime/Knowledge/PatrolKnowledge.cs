using Drawing;
using UnityEngine;

namespace FS.AI
{
    public class PatrolKnowledge : AIKnowledgeModule
    {
        public override KnowledgeUpdateMode UpdateMode => KnowledgeUpdateMode.OnDemand;
        
        public PatrolFollowState PatrolState;

        public override void OnAddedToAgent(GameObject agent)
        {
            PatrolState.Initialize(agent);
        }

        public override void UpdateKnowledge(GameObject agent)
        {
            if (!PatrolState.HasValidPath)
                AIDirector.Instance.RequestPatrolPathForAgent(ref PatrolState);
        }

        public override void ClearKnowledge(GameObject agent)
        {
            PatrolState.ReleasePath();
        }
        
        #region Debug

        public override void DebugGUI(GameObject agent)
        {
            GUILayout.Label($"Patrol Path: {(PatrolState.HasValidPath ? "Assigned" : "NONE")}");
            if (PatrolState.HasValidPath)
            {
                GUILayout.Label($"Current Point: {PatrolState.m_currentIdx}");
                GUILayout.Label($"Waiting: {PatrolState.m_isWaiting} for {PatrolState.m_timeSinceReachedPoint:0.00}s");
            }
        }

        public override void DebugDraw(GameObject agent)
        {
            if (PatrolState.HasValidPath)
            {
                var pathPoint = PatrolState.CurrentPatrolPoint;
                {
                    using var thickness = Draw.ingame.WithLineWidth(3f);
                    Draw.ingame.Arrow(pathPoint.m_position + Vector3.up * 2, pathPoint.m_position + Vector3.up,
                        Color.forestGreen);
                }

                string pointData = PatrolState.m_isWaiting
                    ? $"Waiting For {PatrolState.m_timeSinceReachedPoint}s"
                    : "Moving Towards";
                Draw.ingame.Label2D(pathPoint.m_position + Vector3.up * 2.5f, pointData, Color.white);
                    
                Draw.ingame.Label2D(agent.transform.position + 2 * Vector3.up, $"Path Idx: {PatrolState.m_currentIdx}");
            }
        }

        #endregion
    }
}