using System.Collections.Generic;
using FS.GameService;
using FS.GameService.Attributes;
using FS.Patterns;

namespace FS.GameplayActions
{
    [AutoRegisteredService]
    public class GameActionTicker : PersistentSingleton<GameActionTicker>
    {
        private List<ActionController> m_controllers = new();
#if UNITY_EDITOR 
        public List<ActionController> Editor_Controllers => m_controllers;
#endif

        //public static GameActionTicker Instance => GameServiceLocator.Get<GameActionTicker>();

        public void Initialize() {}
        public void Shutdown() {}

        public void RegisterController(ActionController controller)
        {
            m_controllers.Add(controller);
        }

        public void UnRegisterController(ActionController controller)
        {
            m_controllers.Remove(controller);
        }

        private bool CanControllerTick(ActionController controller)
        {
            // Check distance from the player
            // Check if the game object is in view
            // etc... various "LOD" type optimizations
            return true;
        }

        private void Update()       { foreach (var controller in m_controllers) if (CanControllerTick(controller)) controller?.ExecuteUpdate(); }
        private void FixedUpdate()  { foreach (var controller in m_controllers) if (CanControllerTick(controller)) controller?.ExecuteFixedUpdate(); }
        private void LateUpdate()   { foreach (var controller in m_controllers) if (CanControllerTick(controller)) controller?.ExecuteLateUpdate(); }
    }

}