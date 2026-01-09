using FS.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FS.UI
{
    /// <summary>
    /// Tracks focus state for a single <see cref="UIPanel"/>, ensuring focus is never
    /// unexpectedly lost when using gamepad/keyboard navigation.
    /// 
    /// <para>
    /// <b>Purpose:</b>
    /// Unity's EventSystem can lose focus in various scenarios (clicking empty space,
    /// disabling the selected object, etc.). This class maintains a focus history and
    /// automatically restores focus when appropriate, providing a console-like UI experience.
    /// </para>
    /// 
    /// <para>
    /// <b>Focus Restoration Rules:</b>
    /// <list type="bullet">
    ///   <item>Mouse input: Focus can be null (hovering over nothing is valid)</item>
    ///   <item>Gamepad/Keyboard: Focus is automatically restored to last known element</item>
    ///   <item>Focus blocked: Focus is forced to null during animations or transitions</item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Ownership:</b>
    /// Each <see cref="UIPanel"/> creates and owns one instance of this class.
    /// The owning panel calls <see cref="ValidateFocus"/> every frame while it's the
    /// active (topmost) panel in the stack.
    /// </para>
    /// </summary>
    /// <seealso cref="UIPanel"/>
    /// <seealso cref="UIPlayerContext.UpdateFocus"/>
    public class UIFocusHistory
    {
        /// <summary>
        /// The currently focused selectable element, or null if no focus.
        /// </summary>
        public Selectable m_focusedElement { get; private set; }
        
        /// <summary>
        /// The previously focused element. Used to restore focus when the current
        /// focused element becomes unavailable or when switching from mouse to gamepad.
        /// </summary>
        public Selectable m_lastFocusedElement { get; private set; }

        private UIPanel m_panel;
        private EventSystem m_eventSystem;
        
        /// <summary>
        /// Factory method to create a new focus history for a panel.
        /// Initializes with the panel's <see cref="UIPanel.FirstSelectedElement"/> as the default focus.
        /// </summary>
        /// <param name="panel">The panel that will own this focus history.</param>
        /// <returns>A new <see cref="UIFocusHistory"/> instance.</returns>
        public static UIFocusHistory Create(UIPanel panel)
        {
            var history = new UIFocusHistory
            {
                m_panel = panel,
                m_lastFocusedElement = null,
                m_focusedElement = panel.FirstSelectedElement
            };
            return history;
        }

        /// <summary>
        /// Validates and potentially corrects the focus state for this panel.
        /// Called every frame by <see cref="UIPlayerContext.UpdateFocus"/> for the active panel.
        /// 
        /// <para>
        /// <b>Validation Logic:</b>
        /// <list type="number">
        ///   <item>If panel uses <see cref="InputMode.Game"/>, skip validation (no UI focus needed)</item>
        ///   <item>If EventSystem has valid focus on an element in this panel, sync our state</item>
        ///   <item>If focus is blocked (<see cref="UIPanel.m_focusBlocked"/>), force null focus</item>
        ///   <item>If using gamepad/keyboard with no focus, restore from history</item>
        ///   <item>If using mouse with no focus, allow null (normal mouse behavior)</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="controllerType">The player's current input device type.</param>
        /// <param name="eventSystem">The EventSystem managing this player's input.</param>
        /// <returns>True if focus state is valid, false if there was an unrecoverable error.</returns>
        public bool ValidateFocus(Rewired.ControllerType controllerType, EventSystem eventSystem)
        {
            m_eventSystem = eventSystem;
            if (m_panel == null)
            {
                Debug.LogError($"[UI] Attempting to validate focus on a UIFocusHistory with no associated panel.");
                return false;
            }
            
            // Panel doesn't do UI input, nothing to do
            if (m_panel.InputMode == InputMode.Game) return true;
            
            // Debug visualization (disabled by default)
            #if false
            DrawFocusDebug();
            #endif
            
            // EventSystem has focus - sync our state if it's valid
            if (eventSystem.currentSelectedGameObject != null && !m_panel.m_focusBlocked)
            {
                var focusable = eventSystem.currentSelectedGameObject.GetComponent<Selectable>();
                if (focusable == m_focusedElement)
                    return true; // Already in sync
                
                if (focusable == null)
                {
                    Debug.LogWarning($"[UI] Current selected GameObject '{eventSystem.currentSelectedGameObject.name}' is not a Selectable.");
                    return false;
                }
                
                // Validate the focused element belongs to this panel
                if (!focusable.GetUIPanel(out var parentPanel))
                {
                    Debug.LogError($"[UI] Current selected GameObject '{eventSystem.currentSelectedGameObject.name}' does not belong to any UIPanel.");
                    return false;
                }
                
                if (parentPanel != m_panel)
                {
                    Debug.LogError($"[UI] Current selected GameObject '{eventSystem.currentSelectedGameObject.name}' is not part of panel '{m_panel.PanelName}'.");
                    return false;
                }
                
                SetFocus(focusable);
                return true;
            }
            
            // EventSystem has no focus - determine what to do based on context
            if (m_panel.m_focusBlocked)
            {
                SetFocus(null);
            }
            else if (controllerType != Rewired.ControllerType.Mouse)
            {
                var target = GetValidFocusTarget();
                if (target != null)
                    SetFocus(target);
                else
                    Debug.LogWarning($"[UI] No valid focus target found in panel '{m_panel.PanelName}'");            }
            else
            {
                // Mouse: allow null focus
                SetFocus(null);
            }
            
            return m_panel.m_focusBlocked || m_focusedElement != null;
        }
        
        /// <summary>
        /// Resets focus to the panel's initial state.
        /// Called when a panel is first opened to ensure consistent starting focus.
        /// </summary>
        public void ResetFocus()
        {
            m_lastFocusedElement = null;
            m_focusedElement = m_panel.FirstSelectedElement;
            m_eventSystem.SetSelectedGameObject(m_focusedElement?.gameObject);
        }
        
        /// <summary>
        /// Sets the current focus, updating both the EventSystem and internal history.
        /// </summary>
        /// <param name="focusable">The element to focus, or null to clear focus.</param>
        private void SetFocus(Selectable focusable)
        {
            m_eventSystem.SetSelectedGameObject(focusable?.gameObject);
            if (m_focusedElement == focusable)
                return;

            // Preserve last non-null focus for restoration
            m_lastFocusedElement = m_focusedElement ?? m_lastFocusedElement;
            m_focusedElement = focusable;
        }
        
        /// <summary>
        /// Finds the best valid selectable to focus, with fallback chain.
        /// </summary>
        private Selectable GetValidFocusTarget()
        {
            // Priority 1: Current focus if still valid
            if (IsSelectableValid(m_focusedElement)) 
                return m_focusedElement;
        
            // Priority 2: Last focused element
            if (IsSelectableValid(m_lastFocusedElement)) 
                return m_lastFocusedElement;
        
            // Priority 3: Panel's designated first element
            if (IsSelectableValid(m_panel.FirstSelectedElement)) 
                return m_panel.FirstSelectedElement;
        
            // Priority 4: Any valid selectable in the panel
            // GetComponentsInChildren with false = only active objects
            var selectables = m_panel.GetComponentsInChildren<Selectable>(false);
            foreach (var selectable in selectables)
            {
                if (IsSelectableValid(selectable))
                    return selectable;
            }
        
            // Nothing valid found
            return null;
        }

        /// <summary>
        /// Checks if a selectable can actually receive focus and input.
        /// </summary>
        private bool IsSelectableValid(Selectable selectable)
        {
            if (selectable == null) return false;
            if (!selectable.gameObject.activeInHierarchy) return false;
            if (!selectable.interactable) return false;
        
            // Optional: check if it's visible (not behind something, not zero scale)
            // This might be overkill
            // var canvasGroup = selectable.GetComponentInParent<CanvasGroup>();
            // if (canvasGroup != null && canvasGroup.alpha == 0) return false;
        
            return true;
        }
        
        #if false
        private void DrawFocusDebug()
        {
            if (m_focusedElement != null)
            {
                var rt = m_focusedElement.GetComponent<RectTransform>();
                if (rt != null)
                {
                    var corners = new Vector3[4];
                    rt.GetWorldCorners(corners);
                    var center = (corners[0] + corners[2]) * 0.5f;
                    var size = (corners[2] - corners[0]).magnitude * 0.5f;
                    Draw.SolidCircle(center, size, 20, Color.green);
                }
            }
            if (m_lastFocusedElement != null)
            {
                var rt = m_lastFocusedElement.GetComponent<RectTransform>();
                if (rt != null)
                {
                    var corners = new Vector3[4];
                    rt.GetWorldCorners(corners);
                    var center = (corners[0] + corners[2]) * 0.5f;
                    var size = (corners[2] - corners[0]).magnitude * 0.5f;
                    Draw.SolidCircle(center, size, 20, Color.red);
                }
            }
        }
        #endif
    }
}