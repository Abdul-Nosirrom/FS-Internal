using System;
using FS.GameService;
using FS.GameService.Attributes;
using FS.Player;
using UnityEngine;

namespace FS.CameraSystem
{
    // TODO: This needs further testing, its quite simple and 'works' though
    [AutoRegisteredService(EGameServiceRegistrationOrder.Late)]
    public class SplitScreenManager : IGameService
    {
        public static SplitScreenManager Instance => GameServiceLocator.Get<SplitScreenManager>();
        public Action OnCameraRectChanged;
        private int m_activePlayerCount = 0;
        
        public void Initialize()
        {
            PlayerManager.Instance.OnPlayerAdded += OnPlayerAdded;
            PlayerManager.Instance.OnPlayerRemoved += OnPlayerRemoved;
        }
        
        public void Shutdown()
        {
            PlayerManager.Instance.OnPlayerAdded -= OnPlayerAdded;
            PlayerManager.Instance.OnPlayerRemoved -= OnPlayerRemoved;
        }

        public Rect GetSplitScreenRect(int playerIdx)
        {
            Rect cameraRect = new Rect(0, 0, 1, 1);
            if (m_activePlayerCount <= 1) return cameraRect;
            
            if (m_activePlayerCount == 2)
            {
                cameraRect.width = 0.5f;
                cameraRect.x = playerIdx * 0.5f;
            }
            else if (m_activePlayerCount == 3)
            {
                cameraRect.width = 0.5f;
                cameraRect.x = playerIdx % 2 * 0.5f;
                cameraRect.height = 0.5f;
                cameraRect.y = playerIdx / 2f * 0.5f;
            }
            else if (m_activePlayerCount == 4)
            {
                cameraRect.width = 0.5f;
                cameraRect.x = playerIdx % 2 * 0.5f;
                cameraRect.height = 0.5f;
                cameraRect.y = playerIdx / 2f * 0.5f;
            }
            else
            {
                Debug.LogError($"SplitScreenManager: Unsupported number of players {m_activePlayerCount}");
            }

            return cameraRect;
        }

        private void OnPlayerRemoved(int playerid)
        {
            m_activePlayerCount--;
            OnCameraRectChanged?.Invoke();
        }

        private void OnPlayerAdded(int playerid)
        {
            m_activePlayerCount++;
            OnCameraRectChanged?.Invoke();
        }
    }
}