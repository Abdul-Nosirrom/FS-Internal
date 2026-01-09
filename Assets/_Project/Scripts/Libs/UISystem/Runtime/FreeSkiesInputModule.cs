using Rewired.Integration.UnityUI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FS.UI
{
    public interface ITabHandler : IEventSystemHandler
    {
        void OnTabPressed(int direction);
    }
    
    public class FreeSkiesInputModule : MonoBehaviour
    {
        private RewiredStandaloneInputModule m_inputModule;
        private EventSystem m_eventSystem;

        private static readonly ExecuteEvents.EventFunction<ITabHandler> s_TabHandler = Execute;

        private AxisEventData m_tabEventData;
        
        private static void Execute(ITabHandler handler, BaseEventData eventData)
        {
            var tabEventData = eventData as AxisEventData;
            if (tabEventData != null)
            {
                handler.OnTabPressed((int)tabEventData.moveVector.x);
            }
        }
        
        private void Awake()
        {
            if (!m_inputModule) m_inputModule = GetComponent<RewiredStandaloneInputModule>();
            m_eventSystem = EventSystem.current; // This is per-player
        }

        public void SetPlayer(int playerId)
        {
            if (m_inputModule) m_inputModule.RewiredPlayerIds = new[] { playerId };
            
            m_inputModule.Process();
            if (m_tabEventData == null) m_tabEventData = new AxisEventData(m_eventSystem);

            for (int p = 0; p < m_inputModule.RewiredPlayerIds.Length; p++)
            {
                var player = Rewired.ReInput.players.GetPlayer(m_inputModule.RewiredPlayerIds[p]);
                if (GetButtonDown(player, "UI TabLeft"))
                {
                    m_tabEventData.moveDir = MoveDirection.Left;
                    ExecuteEvents.Execute(m_eventSystem.currentSelectedGameObject, m_tabEventData, s_TabHandler);
                }

                if (GetButtonDown(player, "UI TabRight"))
                {
                    m_tabEventData.moveDir = MoveDirection.Right;
                    ExecuteEvents.Execute(m_eventSystem.currentSelectedGameObject, m_tabEventData, s_TabHandler);
                }
            }
            
            m_tabEventData.Reset();
        }
        
        private bool GetButtonDown(Rewired.Player player, string actionId) {
            //if(actionId < 0) return false; // silence warnings
            return player.GetButtonDown(actionId);
        }
    }
}