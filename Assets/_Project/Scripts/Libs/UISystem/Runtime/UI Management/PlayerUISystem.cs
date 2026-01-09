using System;
using FS.Player;
using UnityEngine;

namespace FS.UI
{
    /// <summary>
    /// Per-player system responsible for managing UI state and input mode transitions.
    /// 
    /// <para>
    /// <b>Responsibilities:</b>
    /// <list type="bullet">
    ///   <item>Creates and owns a <see cref="UIPlayerContext"/> for this player</item>
    ///   <item>Manages the player's <see cref="HUDPanel"/> as the base UI layer</item>
    ///   <item>Listens to UI push/pop events to synchronize input modes with <see cref="PlayerInputSystem"/></item>
    ///   <item>Drives per-frame focus validation through the context</item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Lifecycle:</b>
    /// This system is instantiated per-player by the <see cref="PlayerManager"/>. It creates its own
    /// <see cref="UIPlayerContext"/> which in turn creates dedicated Rewired event system components
    /// on the player's GameObject.
    /// </para>
    /// 
    /// <para>
    /// <b>Input Mode Flow:</b>
    /// When a panel is pushed, this system sets the input mode to match the panel's <see cref="UIPanel.InputMode"/>.
    /// When popped, it reverts to the next panel's mode or <see cref="InputMode.Game"/> if the stack is empty.
    /// </para>
    /// 
    /// <example>
    /// <code>
    /// // Opening UI from gameplay code
    /// var uiSystem = PlayerManager.GetSystem&lt;PlayerUISystem&gt;(playerID);
    /// uiSystem.OpenUI(pauseMenuPanel);
    /// 
    /// // Adding a HUD element
    /// uiSystem.HUD.Add(damageIndicatorPrefab, HUDAnchor.MiddleCenter);
    /// </code>
    /// </example>
    /// </summary>
    /// <seealso cref="UIPlayerContext"/>
    /// <seealso cref="UIPanel"/>
    /// <seealso cref="HUDPanel"/>
    /// <seealso cref="PlayerInputSystem"/>
    public class PlayerUISystem : PlayerSystem
    {
        private PlayerInputSystem m_inputSystem;
        private UIPlayerContext m_uiContext;
        
        private HUDPanel m_hud;

        /// <summary>
        /// The player's HUD panel. Use to add/remove HUD elements at runtime.
        /// </summary>
        public HUDPanel HUD => m_hud;
        
        /// <summary>
        /// Initializes the UI system for this player.
        /// Creates the <see cref="UIPlayerContext"/> and subscribes to stack events.
        /// </summary>
        /// <remarks>
        /// Uses <c>Awaitable.EndOfFrameAsync()</c> to ensure other player systems (specifically
        /// <see cref="PlayerInputSystem"/>) are initialized before we try to reference them.
        /// This is a workaround for the lack of system initialization ordering.
        /// </remarks>
        public override async void Initialize()
        {
            try
            {
                m_uiContext = new UIPlayerContext(
                    m_playerData.ID,
                    gameObject,
                    m_playerData.Instance.GetComponentInChildren<Camera>()
                );

                m_uiContext.OnUIPushed += OnUIPanelPushed;
                m_uiContext.OnUIPopped += OnUIPanelPopped;

                await Awaitable.EndOfFrameAsync();

                m_inputSystem = PlayerManager.GetSystem<PlayerInputSystem>(m_playerData.ID);

                InitializeHUD();
            }
            catch (Exception e)
            {
                throw new Exception($"[UI] Failed to initialize PlayerUISystem for player '{m_playerData.ID}'", e);
            }
        }

        private void InitializeHUD()
        {
            m_hud = m_playerData.Instance.GetComponentInChildren<HUDPanel>();
            
            if (m_hud == null)
            {
                Debug.LogWarning($"[UI] No HUD prefab assigned to PlayerUISystem for player '{m_playerData.ID}'.");
                return;
            }

            OpenUI((UIPanel)m_hud);

            if (m_hud == null)
            {
                Debug.LogError($"[UI] Failed to initialize HUD for player '{m_playerData.ID}'.");
            }
        }

        private void OnUIPanelPushed(UIPanel panel)
        {
            m_inputSystem.SetInputMode(panel.InputMode);
        }
        
        private void OnUIPanelPopped(UIPanel panel)
        {
            var topPanel = m_uiContext.PeekUIStack();
            m_inputSystem.SetInputMode(topPanel != null ? topPanel.InputMode : InputMode.Game);
        }

        /// <summary>
        /// Opens a UI panel for this player, pushing it onto their UI stack.
        /// </summary>
        /// <param name="uiPanel">The panel to open. Must not already be in the stack.</param>
        public void OpenUI(UIPanel uiPanel) => m_uiContext.PushUIPanel(uiPanel);

        /// <summary>
        /// Creates a UI Panel from a given prefab and opens it for this player
        /// </summary>
        public T OpenUI<T>(T uiPrefab) where T : UIPanel
        {
            var panel = UIPanel.CreateRuntimePanel(uiPrefab, m_uiContext);
            if (m_uiContext.PushUIPanel(panel))
                return panel;
            
            // Push failed, cleanup
            Debug.LogWarning($"[UI] Failed to open runtime created panel of type '{typeof(T)}' for player '{m_playerData.ID}'. Destroying instance.");
            Destroy(panel.gameObject);
            return null;
        }

        private void Update() => m_uiContext.UpdateFocus();

        public override void Shutdown()
        {
            HUD?.RemoveAll();
            m_uiContext?.Dispose();
        }
    }
}