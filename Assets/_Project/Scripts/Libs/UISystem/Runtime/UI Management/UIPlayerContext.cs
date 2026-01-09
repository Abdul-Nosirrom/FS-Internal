using System;
using FS.CameraSystem;
using FS.Player;
using Rewired.Integration.UnityUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;

namespace FS.UI
{
    /// <summary>
    /// Central coordinator for a player's UI state, input handling, and panel stack management.
    /// 
    /// <para>
    /// <b>Core Responsibility:</b>
    /// Each player (or the system player for global UI) has one UIPlayerContext that:
    /// <list type="bullet">
    ///   <item>Owns a Rewired EventSystem and InputModule for that player's UI input</item>
    ///   <item>Manages a <see cref="UIStack"/> of active panels</item>
    ///   <item>Drives per-frame focus validation on the active panel</item>
    ///   <item>Provides input state queries for UI navigation</item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Rewired Integration:</b>
    /// Creates <see cref="RewiredEventSystem"/> and <see cref="RewiredStandaloneInputModule"/>
    /// components on the owner GameObject. These are configured to only respond to the
    /// specific player's input, enabling true per-player UI in local multiplayer.
    /// </para>
    /// 
    /// <para>
    /// <b>Input Action Names:</b>
    /// The context expects these Rewired actions to be defined:
    /// <list type="bullet">
    ///   <item><see cref="UI_SUBMIT"/>: Confirm/select action</item>
    ///   <item><see cref="UI_CANCEL"/>: Back/cancel action</item>
    ///   <item><see cref="UI_HORIZONTAL"/>/<see cref="UI_VERTICAL"/>: Navigation axes</item>
    ///   <item><see cref="UI_TAB_LEFT"/>/<see cref="UI_TAB_RIGHT"/>: Tab/bumper navigation</item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Camera Assignment:</b>
    /// When panels are pushed, their canvases are assigned the context's <see cref="Camera"/>.
    /// This enables per-player UI rendering in split-screen scenarios.
    /// </para>
    /// 
    /// <example>
    /// <code>
    /// // Creating a context for a player
    /// var context = new UIPlayerContext(playerId, playerGameObject, playerCamera);
    /// 
    /// // Opening UI
    /// context.PushUIPanel(menuPanel);
    /// 
    /// // Checking input (typically done in panel code)
    /// if (context.UICancel) context.PopUIPanel(out _);
    /// 
    /// // Per-frame update (called by PlayerUISystem or UIManager)
    /// context.UpdateFocus();
    /// </code>
    /// </example>
    /// </summary>
    /// <seealso cref="UIStack"/>
    /// <seealso cref="UIPanel"/>
    /// <seealso cref="PlayerUISystem"/>
    /// <seealso cref="UIManager"/>
    public class UIPlayerContext : IDisposable
    {
        #region Input Action Names
        /// <summary>Rewired action name for UI confirm/submit.</summary>
        public const string UI_SUBMIT = "UISubmit";
        
        /// <summary>Rewired action name for UI cancel/back.</summary>
        public const string UI_CANCEL = "UICancel";
        
        /// <summary>Rewired action name for horizontal navigation axis.</summary>
        public const string UI_HORIZONTAL = "UIHorizontal";
        
        /// <summary>Rewired action name for vertical navigation axis.</summary>
        public const string UI_VERTICAL = "UIVertical";
        
        /// <summary>Rewired action name for left tab/bumper navigation.</summary>
        public const string UI_TAB_LEFT = "UITabLeft";
        
        /// <summary>Rewired action name for right tab/bumper navigation.</summary>
        public const string UI_TAB_RIGHT = "UITabRight";
        #endregion
        
        private readonly int m_playerID;
        private readonly Rewired.Player m_inputHandler;
        private readonly GameObject m_ownerObject;
        private readonly Camera m_uiCamera;
        
        /// <summary>
        /// The type of the player's last active controller.
        /// Used by <see cref="UIFocusHistory"/> to determine focus restoration behavior.
        /// Defaults to Joystick if no controller is active.
        /// </summary>
        public Rewired.ControllerType InputType => m_inputHandler.controllers.GetLastActiveController()?.type ?? Rewired.ControllerType.Joystick;
        
        /// <summary>
        /// The Rewired EventSystem for this player's UI input.
        /// Used by <see cref="UIFocusHistory"/> to get/set the selected GameObject.
        /// </summary>
        public EventSystem EventSystem => m_eventSystem;
        
        /// <summary>
        /// Flag indicating whether the UI stack is currently transitioning (pushing/popping) so mid-way through running
        /// animations related to those. Useful if you want to trigger something but have to wait for this.
        /// </summary>
        public bool IsTransitioning => m_uiStack.IsTransitioning;
        
        private RewiredEventSystem m_eventSystem;
        private RewiredStandaloneInputModule m_inputModule;
        private UIStack m_uiStack;
        
        /// <summary>
        /// Fired after a panel is successfully pushed to the stack.
        /// Used by <see cref="PlayerUISystem"/> to sync input modes.
        /// </summary>
        public event Action<UIPanel> OnUIPushed;
        
        /// <summary>
        /// Fired after a panel is successfully popped from the stack.
        /// Used by <see cref="PlayerUISystem"/> to sync input modes.
        /// </summary>
        public event Action<UIPanel> OnUIPopped;

        /// <summary>
        /// The camera to assign to UI canvases for this player.
        /// Returns the explicitly assigned camera, or <see cref="Camera.main"/> as fallback.
        /// </summary>
        public Camera Camera => m_uiCamera ?? Camera.main;
        
        /// <summary>
        /// Creates a new UI context for a player.
        /// </summary>
        /// <param name="playerID">The Rewired player ID this context handles input for.</param>
        /// <param name="ownerObject">The GameObject to attach EventSystem components to.</param>
        /// <param name="uiCamera">Optional camera for UI rendering. Falls back to Camera.main.</param>
        public UIPlayerContext(int playerID, GameObject ownerObject, Camera uiCamera = null)
        {
            m_playerID = playerID;
            m_inputHandler = Rewired.ReInput.players.GetPlayer(playerID);
            m_ownerObject = ownerObject;
            m_uiCamera = uiCamera;
            m_uiStack = new UIStack();
            
            if (m_inputHandler == null || m_ownerObject == null)
            {
                Debug.LogError($"[UI] Could not find Rewired Player with ID '{m_playerID}' for UI Context");
                return;
            }
            
            // Create Rewired UI components
            m_eventSystem = m_ownerObject.AddComponent<RewiredEventSystem>();
            m_eventSystem.alwaysUpdate = true;
            m_inputModule = m_ownerObject.AddComponent<RewiredStandaloneInputModule>();

            // Configure input module for this specific player
            m_inputModule.UseRewiredSystemPlayer = m_playerID == Rewired.ReInput.players.GetSystemPlayer().id;
            m_inputModule.RewiredPlayerIds = new [] { m_inputHandler.id };
        }

        #region Input State Properties
        /// <summary>Current UI navigation direction from stick/dpad input.</summary>
        public Vector2 UINavigationDirection =>
            new Vector2(m_inputHandler.GetAxis(UI_HORIZONTAL), m_inputHandler.GetAxis(UI_VERTICAL));

        /// <summary>True on the frame the submit button is pressed.</summary>
        public bool UISubmit => m_inputHandler.GetButtonDown(UI_SUBMIT);
        
        /// <summary>True on the frame the cancel button is pressed.</summary>
        public bool UICancel => m_inputHandler.GetButtonDown(UI_CANCEL);
        
        /// <summary>True on the frame the left tab button is pressed.</summary>
        public bool UITabLeft => m_inputHandler.GetButtonDown(UI_TAB_LEFT);
        
        /// <summary>True on the frame the right tab button is pressed.</summary>
        public bool UITabRight => m_inputHandler.GetButtonDown(UI_TAB_RIGHT);
        #endregion
        
        /// <summary>
        /// Pushes a panel onto this context's UI stack.
        /// </summary>
        /// <param name="panel">The panel to push. Must not already be in any stack.</param>
        /// <returns>True if the panel was successfully pushed.</returns>
        /// <remarks>
        /// The panel's context is set before pushing. If context assignment fails
        /// (e.g., panel already belongs to another context), the push is aborted.
        /// </remarks>
        public bool PushUIPanel(UIPanel panel)
        {
            bool result = false;
            if (panel) result = panel.SetContext(this);
            result = result && m_uiStack.PushPanel(panel);
            if (result)
            {
                OnUIPushed?.Invoke(panel);
            }
            return result;
        }

        /// <summary>
        /// Pops the topmost panel from the stack.
        /// </summary>
        /// <param name="panel">Receives the popped panel, or null if stack was empty.</param>
        /// <returns>True if a panel was successfully popped.</returns>
        public bool PopUIPanel(out UIPanel panel)
        {
            bool result = m_uiStack.PopPanel(out panel);
            if (result)
            {
                OnUIPopped?.Invoke(panel);
                panel.ClearContext();
            }
            return result;
        }

        /// <summary>
        /// Returns the topmost panel without removing it.
        /// </summary>
        /// <returns>The active panel, or null if the stack is empty.</returns>
        public UIPanel PeekUIStack() => m_uiStack.PeekPanel();
        
        /// <summary>
        /// Immediately clears all panels without animations.
        /// Use for scene transitions or emergency cleanup.
        /// </summary>
        public void ClearUIStackImmediate() => m_uiStack.ClearStackImmediate();

        public bool ContainsPanel<T>() where T : UIPanel => m_uiStack.ContainsPanel<T>();
        public T GetPanel<T>() where T : UIPanel => m_uiStack.GetPanel<T>();
        public UIPanel GetPanelBelow(UIPanel panel) => m_uiStack.GetPanelBelow(panel);
        
        /// <summary>
        /// The number of panels currently in the stack.
        /// </summary>
        public int ActivePanelCount => m_uiStack.Count;
        
        /// <summary>
        /// Validates focus state for the active panel.
        /// Should be called every frame by the owning system (<see cref="PlayerUISystem"/> or <see cref="UIManager"/>).
        /// </summary>
        /// <remarks>
        /// Does nothing if the stack is empty. For the active panel, delegates to
        /// <see cref="UIPanel.ValidatePanelFocus"/> which in turn uses <see cref="UIFocusHistory"/>.
        /// </remarks>
        /// <exception cref="NullReferenceException">
        /// Thrown if the stack reports non-zero count but peek returns null (indicates bug).
        /// </exception>
        public void UpdateFocus()
        {
            if (ActivePanelCount == 0) return;
            
            var topPanel = PeekUIStack();
            if (!topPanel) // Handles destroyed objects
            {
                // Either pop the dead panel or log error
                Debug.LogError("[UI] Top panel was destroyed while in stack");
                m_uiStack.PopPanel(out _); // Clean up
                return;
            }

            if (topPanel.InputMode == InputMode.Game) return; // no focus here
            
            Profiler.BeginSample("UIPlayerContext.UpdateFocus");
            
            topPanel.ValidatePanelFocus();
            
            Profiler.EndSample();
        }
        
        /// <summary>
        /// Disposes of the context and its resources.
        /// </summary>
        /// <remarks>
        /// TODO: Should destroy the EventSystem and InputModule components,
        /// and potentially close all open panels.
        /// </remarks>
        public void Dispose()
        {
            // TODO: Destroy m_eventSystem and m_inputModule components
            // TODO: Pop all panels from stack?
        }
    }
}