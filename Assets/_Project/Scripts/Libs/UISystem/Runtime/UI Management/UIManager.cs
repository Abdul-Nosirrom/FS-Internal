using System;
using FS.GameService;
using FS.GameService.Attributes;
using Rewired.Integration.UnityUI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FS.UI
{
    /// <summary>
    /// Global UI manager for system-level UI that isn't owned by any specific player.
    /// 
    /// <para>
    /// <b>Purpose:</b>
    /// Manages UI panels that should respond to any player's input or operate outside
    /// the player system (e.g., title screen, system dialogs, loading screens).
    /// Uses Rewired's "System Player" for input handling.
    /// </para>
    /// 
    /// <para>
    /// <b>Comparison with <see cref="PlayerUISystem"/>:</b>
    /// <list type="bullet">
    ///   <item><see cref="UIManager"/>: Global/system UI, responds to any controller</item>
    ///   <item><see cref="PlayerUISystem"/>: Per-player UI, responds only to that player's input</item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Usage:</b>
    /// Access via the static <see cref="Instance"/> property. Registered automatically
    /// via <see cref="AutoRegisteredServiceAttribute"/>.
    /// </para>
    /// 
    /// <example>
    /// <code>
    /// // Opening system-level UI
    /// UIManager.Instance.OpenUI(titleScreenPanel);
    /// 
    /// // Or from anywhere in the game
    /// UIManager.Instance.OpenUI(systemDialogPanel);
    /// </code>
    /// </example>
    /// </summary>
    /// <remarks>
    /// The <see cref="Pause"/> and <see cref="UnPause"/> methods are stubbed for future
    /// implementation of pause-aware UI behavior.
    /// </remarks>
    /// <seealso cref="PlayerUISystem"/>
    /// <seealso cref="UIPlayerContext"/>
    [AutoRegisteredService(EGameServiceRegistrationOrder.Normal)]
    public class UIManager : MonoGameService
    {
        /// <summary>
        /// Singleton instance for global access.
        /// Null before <see cref="Initialize"/> and after <see cref="Shutdown"/>.
        /// </summary>
        public static UIManager Instance { get; private set; }
        
        /// <summary>
        /// The UI context for system-level UI.
        /// Uses Rewired's System Player for input, meaning any connected controller can interact.
        /// </summary>
        public UIPlayerContext UISystemContext { get; private set; }

        /// <summary>
        /// Initializes the UIManager and creates the system UI context.
        /// Called automatically by the game service system.
        /// </summary>
        public override void Initialize()
        {
            Instance = this;
            UISystemContext = new UIPlayerContext(Rewired.ReInput.players.GetSystemPlayer().id, gameObject);
        }

        /// <summary>
        /// Cleans up the UIManager on shutdown.
        /// </summary>
        public override void Shutdown()
        {
            UISystemContext?.Dispose();
            Instance = null;
        }

        /// <summary>
        /// Opens a UI panel in the system context.
        /// </summary>
        /// <param name="uiPanel">The panel to open.</param>
        /// <returns>True if the panel was successfully pushed to the stack.</returns>
        public bool OpenUI(UIPanel uiPanel) => UISystemContext.PushUIPanel(uiPanel);
        
        /// <summary>
        /// Creates a UI Panel from a given prefab and opens it for this player
        /// </summary>
        public T OpenUI<T>(T uiPrefab) where T : UIPanel
        {
            var panel = UIPanel.CreateRuntimePanel(uiPrefab, UISystemContext);
            if (UISystemContext.PushUIPanel(panel))
                return panel;
            
            // Push failed, cleanup
            Debug.LogWarning($"[UI] Failed to open runtime created panel of type '{typeof(T)}' for System Player. Destroying instance.");
            Destroy(panel.gameObject);
            return null;
        }

        private void Update() => UISystemContext.UpdateFocus();

        /// <summary>
        /// Called when the game is paused.
        /// Reserved for future implementation of pause-aware UI behavior.
        /// </summary>
        public void Pause()
        {
            // TODO: Implement pause behavior (e.g., stop UI animations, show pause indicator)
        }

        /// <summary>
        /// Called when the game is unpaused.
        /// Reserved for future implementation of pause-aware UI behavior.
        /// </summary>
        public void UnPause()
        {
            // TODO: Implement unpause behavior
        }
    }
}