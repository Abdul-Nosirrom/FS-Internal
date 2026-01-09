using Drawing;
using UnityEngine;

namespace FS.AI
{
    public class PerceptionKnowledge : AIKnowledgeModule
    {
        public override KnowledgeUpdateMode UpdateMode => KnowledgeUpdateMode.Periodic;
        public override int UpdateInterval => 10; // Update every 10 frames

        public AIPerceptionResult PerceptionResult;
        private AISensors m_sensors;
        
        public override void OnAddedToAgent(GameObject agent)
        {
            m_sensors = agent.GetComponent<AISensors>();
            if (m_sensors == null)
                Debug.LogError($"PerceptionKnowledge requires AISensors component on {agent.name}");
        }
        
        public override void UpdateKnowledge(GameObject agent)
        {
            if (m_sensors == null) return;
            
            m_sensors.UpdateTargetPerception();
            PerceptionResult = m_sensors.PerceptionResult;
        }

        public override void ClearKnowledge(GameObject agent)
        {
            PerceptionResult.Clear();
        }
        
        #region Debug

        public override void DebugGUI(GameObject agent)
        {
            var ogColor = GUI.color;
            GUI.color = PerceptionResult.HasTarget ? Color.forestGreen : Color.crimson;
            GUILayout.Label($"Target: {(PerceptionResult.HasTarget ? PerceptionResult.Target.name : "NONE")}");
            GUILayout.Label($"Last Seen: {PerceptionResult.SinceLastSeen:0.00}s ago");
            GUILayout.Label($"Has Line of Sight: {PerceptionResult.HasLineOfSight}");
            GUI.color = ogColor;
        }

        public override void DebugDraw(GameObject agent)
        {
            if (PerceptionResult.HasTarget)
            {
                Draw.ingame.DashedLine(agent.transform.position, PerceptionResult.TargetPosition, 0.5f,
                    0.2f);
            }
            else if (!PerceptionResult.HasLineOfSight)
            {
                Draw.ingame.DashedLine(agent.transform.position, PerceptionResult.LineOfSightObstruction.point, 0.5f, 0.2f);
                Draw.ingame.WireSphere(PerceptionResult.LineOfSightObstruction.point, 0.5f, Color.purple);
            }
            else
            {
                Draw.ingame.DashedLine(agent.transform.position, PerceptionResult.LastKnownPosition, 0.5f,
                    0.2f);
                Draw.ingame.WireSphere(PerceptionResult.LastKnownPosition, 0.5f, Color.red);
                Draw.ingame.Label2D(PerceptionResult.LastKnownPosition + Vector3.up, $"Last Known Position Since {PerceptionResult.SinceLastSeen:0.00}s");
            }
        }

        #endregion
    }
}