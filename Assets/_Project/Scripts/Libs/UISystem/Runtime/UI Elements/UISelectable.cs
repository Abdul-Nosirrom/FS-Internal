using System;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using UnityEngine.UI;

namespace FS.UI
{
    /// <summary>
    /// Enumeration of possible selectable UI element states.
    /// Maps to Unity's <see cref="Selectable.SelectionState"/> but as a public enum
    /// for use in the animation system.
    /// </summary>
    public enum UISelectableState
    {
        /// <summary>Default state when not interacted with.</summary>
        Normal,
        /// <summary>Mouse is hovering over the element (pointer input only).</summary>
        Highlighted,
        /// <summary>Element is being clicked/pressed.</summary>
        Pressed,
        /// <summary>Element has focus (gamepad/keyboard selection).</summary>
        Selected,
        /// <summary>Element is not interactable.</summary>
        Disabled
    }
    
    /// <summary>
    /// Configuration for an animation that plays when a selectable enters a specific state.
    /// 
    /// <para>
    /// Wraps a <see cref="UIAnimator"/> with directional control and lifecycle events.
    /// Used by <see cref="UISelectableEvents"/> to define per-state animations.
    /// </para>
    /// 
    /// <example>
    /// <code>
    /// // Typical usage: scale up on select, scale down on deselect
    /// var selectAnim = new UISelectableAnimation 
    /// {
    ///     m_animation = scaleAnimator,
    ///     m_reverse = false  // Play forward when entering state
    /// };
    /// 
    /// var deselectAnim = new UISelectableAnimation 
    /// {
    ///     m_animation = scaleAnimator,
    ///     m_reverse = true  // Play reverse when leaving state
    /// };
    /// </code>
    /// </example>
    /// </summary>
    [Serializable]
    public struct UISelectableAnimation
    {
        /// <summary>Whether to play the animation in reverse.</summary>
        [Title("Animation")]
        [SerializeField] public bool m_reverse;
        
        /// <summary>The animator to play. Can be null for no animation.</summary>
        [SerializeField] public UIAnimator m_animation;
        
        /// <summary>Event fired when the animation starts.</summary>
        [Title("Events")]
        [SerializeField] public UltEvent m_onStart;
        
        /// <summary>Event fired when the animation completes.</summary>
        [SerializeField] public UltEvent m_onComplete;

        /// <summary>
        /// Plays the animation with configured settings.
        /// </summary>
        /// <returns>Awaitable that completes when the animation finishes.</returns>
        public async Awaitable Play()
        {
            m_onStart?.Invoke();

            if (m_animation == null)
            {
                m_onComplete?.Invoke();
                return;
            }
                
            if (m_reverse) await m_animation.PlayReverseAsync();
            else await m_animation.PlayForwardAsync();
                
            m_onComplete?.Invoke();
        }
    }

    /// <summary>
    /// Collection of animations for each <see cref="UISelectableState"/>.
    /// 
    /// <para>
    /// Provides a tabbed inspector interface (via Odin) for configuring animations
    /// that play when a selectable transitions between states. Each state can have
    /// its own <see cref="UISelectableAnimation"/> with independent settings.
    /// </para>
    /// 
    /// <para>
    /// <b>Common Patterns:</b>
    /// <list type="bullet">
    ///   <item>Normal ↔ Selected: Scale or glow animation for focus indication</item>
    ///   <item>Normal ↔ Highlighted: Subtle hover effect for mouse users</item>
    ///   <item>Any → Pressed: Quick scale-down "click" feedback</item>
    ///   <item>Any → Disabled: Fade or desaturate effect</item>
    /// </list>
    /// </para>
    /// </summary>
    [Serializable]
    public struct UISelectableEvents
    {
        /// <summary>Animation played when entering the Normal state.</summary>
        [TabGroup("Normal State"), HideLabel]
        public UISelectableAnimation OnNormal;
        
        /// <summary>Animation played when entering the Highlighted state (mouse hover).</summary>
        [TabGroup("Highlighted State"), HideLabel]
        public UISelectableAnimation OnHighlighted;
        
        /// <summary>Animation played when entering the Pressed state.</summary>
        [TabGroup("Pressed State"), HideLabel]
        public UISelectableAnimation OnPressed;
        
        /// <summary>Animation played when entering the Selected state (focus).</summary>
        [TabGroup("Selected State"), HideLabel]
        public UISelectableAnimation OnSelected;
        
        /// <summary>Animation played when entering the Disabled state.</summary>
        [TabGroup("Disabled State"), HideLabel]
        public UISelectableAnimation OnDisabled;

        /// <summary>
        /// Executes the animation for the specified state.
        /// Fire-and-forget pattern - does not await completion.
        /// </summary>
        /// <param name="state">The state to trigger animation for.</param>
        public void Execute(UISelectableState state)
        {
            switch (state)
            {
                case UISelectableState.Normal:
                    _ = OnNormal.Play();
                    break;
                case UISelectableState.Highlighted:
                    _ = OnHighlighted.Play();
                    break;
                case UISelectableState.Pressed:
                    _ = OnPressed.Play();
                    break;
                case UISelectableState.Selected:
                    _ = OnSelected.Play();
                    break;
                case UISelectableState.Disabled:
                    _ = OnDisabled.Play();
                    break;
            }
        }
    }

    /// <summary>
    /// Extension methods for Unity's <see cref="Selectable"/> class to integrate
    /// with the panel-based UI system.
    /// </summary>
    public static class UISelectableExtensions
    {
        /// <summary>
        /// Finds the <see cref="UIPanel"/> that contains this selectable.
        /// 
        /// <para>
        /// Searches first on the same GameObject, then traverses up the hierarchy.
        /// Used by <see cref="UIFocusHistory"/> to validate that focused elements
        /// belong to the correct panel.
        /// </para>
        /// </summary>
        /// <param name="selectable">The selectable to find the panel for.</param>
        /// <param name="panel">Receives the found panel, or null if not found.</param>
        /// <returns>True if a panel was found in the hierarchy.</returns>
        public static bool GetUIPanel(this Selectable selectable, out UIPanel panel)
        {
            panel = selectable.GetComponent<UIPanel>();
            if (panel != null) return true;
            panel = selectable.GetComponentInParent<UIPanel>(true);
            return panel != null;
        }
    }
}