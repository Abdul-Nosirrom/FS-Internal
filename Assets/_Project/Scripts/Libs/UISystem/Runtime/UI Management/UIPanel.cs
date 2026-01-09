using System;
using System.Collections;
using FS.Player;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using UnityEngine.UI;

namespace FS.UI
{
    /// <summary>
    /// Base class for all UI panels in the stack-based UI system.
    /// 
    /// <para>
    /// <b>Core Concept:</b>
    /// A UIPanel represents a discrete UI screen or overlay (menu, dialog, HUD element).
    /// Panels are managed in a stack by <see cref="UIPlayerContext"/> - only the topmost
    /// panel receives input and focus validation.
    /// </para>
    /// 
    /// <para>
    /// <b>Lifecycle Events:</b>
    /// <list type="bullet">
    ///   <item><see cref="OnOpened"/>: Panel pushed to stack (plays open animation)</item>
    ///   <item><see cref="OnClosed"/>: Panel popped from stack (plays close animation)</item>
    ///   <item><see cref="OnLostFocus"/>: Another panel pushed on top of this one</item>
    ///   <item><see cref="OnRegainedFocus"/>: Panel above this one was popped</item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Input Modes:</b>
    /// The <see cref="InputMode"/> determines how the game responds while this panel is active:
    /// <list type="bullet">
    ///   <item><see cref="InputMode.UI"/>: Full UI mode, game input disabled</item>
    ///   <item><see cref="InputMode.Game"/>: Game input enabled (for HUDs/overlays)</item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Focus Management:</b>
    /// Each panel maintains its own <see cref="UIFocusHistory"/> to track and restore focus.
    /// The <see cref="FirstSelectedElement"/> defines which element receives initial focus.
    /// </para>
    /// 
    /// <para>
    /// <b>Animation System:</b>
    /// Each lifecycle event can have an associated <see cref="UIAnimator"/> animation.
    /// Animations can optionally block focus during playback via <see cref="m_focusBlocked"/>.
    /// </para>
    /// 
    /// <example>
    /// <code>
    /// // Custom panel with additional behavior
    /// public class PauseMenu : UIPanel
    /// {
    ///     public override async Awaitable OnOpened()
    ///     {
    ///         await base.OnOpened();
    ///         Time.timeScale = 0f;
    ///     }
    ///     
    ///     public override async Awaitable OnClosed()
    ///     {
    ///         Time.timeScale = 1f;
    ///         await base.OnClosed();
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </summary>
    /// <seealso cref="UIStack"/>
    /// <seealso cref="UIPlayerContext"/>
    /// <seealso cref="UIFocusHistory"/>
    public class UIPanel : MonoBehaviour
    {
        /// <summary>
        /// Configuration for a panel lifecycle animation.
        /// Wraps a <see cref="UIAnimator"/> with additional options for focus blocking and completion events.
        /// </summary>
        [Serializable]
        private struct UIPanelAnimation
        {
            /// <summary>Event fired when the animation completes.</summary>
            [SerializeField] public UltEvent m_onComplete;
            
            /// <summary>If true, <see cref="UIPanel.m_focusBlocked"/> is set during animation playback.</summary>
            [SerializeField] public bool m_blockFocusUntilComplete;
            
            /// <summary>If true, plays the animation in reverse.</summary>
            [SerializeField] public bool m_reverse;
            
            /// <summary>If true, the lifecycle method awaits animation completion before returning.</summary>
            [SerializeField] public bool m_waitForCompletion;
            
            /// <summary>The animator component to play. Can be null for no animation.</summary>
            [SerializeField] public UIAnimator m_animation;

            /// <summary>
            /// Plays the animation with configured settings.
            /// </summary>
            /// <param name="panel">The panel owning this animation (for focus blocking).</param>
            public async Awaitable Play(UIPanel panel)
            {
                if (m_animation == null) return;
                
                panel.m_focusBlocked = m_blockFocusUntilComplete;
                
                if (m_reverse) await m_animation.PlayReverseAsync();
                else await m_animation.PlayForwardAsync();
                
                if (Application.isPlaying) m_onComplete?.Invoke();
                
                panel.m_focusBlocked = false;
            }
        }

        private bool m_destroyOnPop = false;

        public static T CreateRuntimePanel<T>(T prefab, UIPlayerContext context, Transform parent = null) where T : UIPanel
        {
            var instance = GameObject.Instantiate(prefab, parent);
            instance.m_destroyOnPop = true;
            var realParent = parent == null ? context.EventSystem.transform : parent; // for clear UI hierarchy under this go if none provided
            instance.transform.SetParent(realParent, false);
            return instance;
        }
        
        [SerializeField] private string m_panelName = "New Panel";
        [SerializeField] private InputMode m_inputMode = InputMode.UI;
        [SerializeField, Required] private Selectable m_firstSelectedElement;
        
        /// <summary>Display name for this panel, used for debugging and stack identification.</summary>
        public string PanelName => m_panelName;
        
        /// <summary>The input mode active while this panel is at the top of the stack.</summary>
        public InputMode InputMode => m_inputMode;
        
        /// <summary>The element that receives focus when this panel is opened or focus is reset.</summary>
        public Selectable FirstSelectedElement => m_firstSelectedElement;

        private UIPlayerContext m_context;
        
        /// <summary>
        /// The <see cref="UIPlayerContext"/> this panel belongs to.
        /// Set when the panel is pushed to a stack, cleared when popped.
        /// </summary>
        public UIPlayerContext Context => m_context;

        private UIFocusHistory m_focusHistory;
        
        /// <summary>
        /// When true, focus validation will force null focus regardless of input type.
        /// Used during animations to prevent input during transitions.
        /// </summary>
        /// <remarks>
        /// This field is public for access by <see cref="UIFocusHistory"/> but should
        /// generally only be modified by the animation system.
        /// </remarks>
        [ReadOnly] public bool m_focusBlocked;

        /// <summary>Event invoked when the panel is first pushed to the stack.</summary>
        public UltEvent OnPanelOpened;
        
        /// <summary>Event invoked when the panel is popped from the stack.</summary>
        public UltEvent OnPanelClosed;
        
        /// <summary>Event invoked when the panel regains focus (panel above was popped).</summary>
        public UltEvent OnPanelRegainedFocus;
        
        /// <summary>Event invoked when the panel loses focus (new panel pushed above).</summary>
        public UltEvent OnPanelLostFocus;

        [SerializeField] private UIPanelAnimation m_onOpenedAnimation;
        [SerializeField] private UIPanelAnimation m_onClosedAnimation;
        [SerializeField] private UIPanelAnimation m_onRegainedFocusAnimation;
        [SerializeField] private UIPanelAnimation m_onLostFocusAnimation;

        private void Awake()
        {
            // Auto-find first selectable if not assigned
            if (m_firstSelectedElement == null)
            {
                Debug.LogWarning($"[UI] No default focus element provided for panel '{name}', searching children...");
                var selectable = GetComponentInChildren<Selectable>();
                if (selectable != null)
                {
                    m_firstSelectedElement = selectable;
                }
                else
                {
                    Debug.LogWarning($"[UI] No Selectable element found in children of UIPanel '{name}'");
                }
            }
            
            m_focusHistory = UIFocusHistory.Create(this);
        }
        
        private async Awaitable PlayUIAnimation(UIPanelAnimation anim)
        {
            if (anim.m_blockFocusUntilComplete)
                m_focusBlocked = true;
                
            if (anim.m_waitForCompletion)
                await anim.Play(this);
            else 
                _ = anim.Play(this);
                
            if (anim.m_blockFocusUntilComplete)
                m_focusBlocked = false;
        }

        /// <summary>
        /// Pushes this panel onto its context's stack.
        /// Convenience method for use in UnityEvents.
        /// </summary>
        public void PushPanel() => m_context.PushUIPanel(this);
        
        /// <summary>
        /// Pops this panel from its context's stack.
        /// Convenience method for use in UnityEvents.
        /// </summary>
        public void PopPanel() => m_context.PopUIPanel(out _);

        /// <summary>
        /// Called by <see cref="UIPlayerContext.UpdateFocus"/> every frame while this panel is active.
        /// Delegates to the internal <see cref="UIFocusHistory"/> for actual validation.
        /// </summary>
        public void ValidatePanelFocus()
        {
            m_focusHistory.ValidateFocus(m_context.InputType, m_context.EventSystem);
        }
        
        /// <summary>
        /// Called when this panel is pushed onto the stack.
        /// Override to add custom open behavior (call base first).
        /// </summary>
        public virtual async Awaitable OnOpened()
        {
            if (Application.isPlaying) OnPanelOpened?.Invoke();
            await PlayUIAnimation(m_onOpenedAnimation);
            m_focusHistory.ResetFocus();
        }

        /// <summary>
        /// Called when this panel is popped from the stack.
        /// Override to add custom close behavior (call base last).
        /// </summary>
        public virtual async Awaitable OnClosed()
        {
            await PlayUIAnimation(m_onClosedAnimation);
            if (Application.isPlaying) OnPanelClosed?.Invoke();
            
            if (m_destroyOnPop)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Called when a panel above this one is popped, returning focus to this panel.
        /// Override to add custom behavior (call base first).
        /// </summary>
        public virtual async Awaitable OnRegainedFocus()
        {
            if (Application.isPlaying) OnPanelRegainedFocus?.Invoke();
            await PlayUIAnimation(m_onRegainedFocusAnimation);
        }
        
        /// <summary>
        /// Called when a new panel is pushed above this one.
        /// Override to add custom behavior (call base last).
        /// </summary>
        public virtual async Awaitable OnLostFocus()
        {
            await PlayUIAnimation(m_onLostFocusAnimation);
            if (Application.isPlaying) OnPanelLostFocus?.Invoke();
        }

        /// <summary>
        /// Associates this panel with a <see cref="UIPlayerContext"/>.
        /// Called automatically when the panel is pushed to a stack.
        /// </summary>
        /// <param name="context">The context to associate with, or null to clear.</param>
        /// <returns>True if the context was successfully set.</returns>
        /// <remarks>
        /// This method also:
        /// <list type="bullet">
        ///   <item>Recursively sets context on all child <see cref="UIPanel"/> components</item>
        ///   <item>Assigns the context's camera to any ScreenSpaceCamera or WorldSpace canvases</item>
        /// </list>
        /// A panel cannot be reassigned to a different context without first clearing it.
        /// </remarks>
        public bool SetContext(UIPlayerContext context)
        {
            if (context == null)
            {
                return false;
            }
            else if (context == m_context)
            {
                return true;
            }
            else if (m_context != null && m_context != context)
            {
                Debug.LogError($"[UI] Attempting to set context on UIPanel '{name}' that already has a context assigned\n" +
                               $"- Current Context: {m_context}\n" +
                               $"- New Context: {context}");
                return false;
            }
            
            // Set context on all child panels (including this one)
            var panels = GetComponentsInChildren<UIPanel>(true);
            foreach (var panel in panels)
            {
                if (panel.m_context != null && panel.m_context != context)
                {
                    Debug.LogError($"[UI] Failed to set context on child UIPanel '{panel.name}' of parent UIPanel '{name}'");
                    return false;
                }
                panel.m_context = context;
            }
            
            // Configure cameras on canvases
            var canvases = GetComponentsInChildren<Canvas>(true);
            Debug.LogError($"About to initialize canvases on panel '{gameObject.name}' for context camera");
            foreach (var canvas in canvases)
            {
                // NOTE: If set to camera but no camera was assigned it'll tell us its overlay. Should we force camera instead of overlay always to use PP effects?
                Debug.LogError($"[UI] Found Canvas With Render Mode '{canvas.renderMode}' on panel '{name}'");
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
                {
                    canvas.worldCamera = context.Camera;
                    canvas.planeDistance = context.Camera.nearClipPlane + 0.01f;
                }
            }

            return true;
        }
        
        /// <summary>
        /// Clears the context association from this panel and all child panels.
        /// Called automatically when the panel is popped from a stack.
        /// </summary>
        public void ClearContext()
        {
            m_context = null;
            var panels = GetComponentsInChildren<UIPanel>();
            foreach (var panel in panels) panel.ClearContext();
        }
    }
}