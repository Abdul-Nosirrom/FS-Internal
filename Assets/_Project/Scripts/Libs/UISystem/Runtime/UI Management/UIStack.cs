using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS.UI
{
    /// <summary>
    /// Stack-based container for managing active UI panels.
    /// 
    /// <para>
    /// <b>Purpose:</b>
    /// Implements a LIFO (Last-In-First-Out) stack for UI panels, which is the standard
    /// pattern for menu navigation. Only the topmost panel is considered "active" and
    /// receives input/focus.
    /// </para>
    /// 
    /// <para>
    /// <b>Lifecycle Orchestration:</b>
    /// The stack is responsible for calling the appropriate lifecycle methods on panels:
    /// <list type="bullet">
    ///   <item>Push: Previous top panel gets <see cref="UIPanel.OnLostFocus"/>, new panel gets <see cref="UIPanel.OnOpened"/></item>
    ///   <item>Pop: Removed panel gets <see cref="UIPanel.OnClosed"/>, new top panel gets <see cref="UIPanel.OnRegainedFocus"/></item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Duplicate Prevention:</b>
    /// The stack prevents the same panel from being pushed multiple times via <see cref="UIStackItem"/>
    /// which compares panels by their <see cref="UIPanel.PanelName"/>.
    /// </para>
    /// 
    /// <para>
    /// <b>Async Considerations:</b>
    /// Lifecycle methods are async but the stack pushes immediately before awaiting animations.
    /// This ensures <see cref="PeekPanel"/> always returns the correct panel even during transitions.
    /// </para>
    /// 
    /// <example>
    /// <code>
    /// var stack = new UIStack();
    /// 
    /// // Open main menu
    /// stack.PushPanel(mainMenuPanel);
    /// 
    /// // Open settings (main menu loses focus)
    /// stack.PushPanel(settingsPanel);
    /// 
    /// // Close settings (main menu regains focus)
    /// stack.PopPanel(out var closed); // closed == settingsPanel
    /// 
    /// // Check current panel
    /// var current = stack.PeekPanel(); // current == mainMenuPanel
    /// </code>
    /// </example>
    /// </summary>
    /// <seealso cref="UIPanel"/>
    /// <seealso cref="UIPlayerContext"/>
    public class UIStack
    {
        /// <summary>
        /// Wrapper struct for stack items that provides equality based on panel name.
        /// This prevents the same logical panel from being pushed multiple times.
        /// </summary>
        private struct UIStackItem : IEquatable<UIPanel>, IEquatable<UIStackItem>
        {
            public UIPanel m_panel;
            public string m_id { get; private set; }
            
            public UIStackItem(UIPanel panel)
            {
                m_panel = panel;
                if (m_panel == null)
                {
                    Debug.LogError($"[UI] Attempting to create a UIStackItem with a null panel.");
                    m_id = "INVALID";
                }
                else
                {
                    m_id = panel.PanelName;
                }
            }
            
            public static implicit operator UIStackItem(UIPanel panel) => new(panel);
            
            public override int GetHashCode() => m_id.GetHashCode();
            public bool Equals(UIStackItem other) => other.m_id.Equals(m_id);
            public bool Equals(UIPanel other) => other != null && other.PanelName.Equals(m_id);
        }
        
        private Stack<UIStackItem> m_uiStack = new();
        
        /// <summary>
        /// True while open/close animations are playing during push/pop operations.
        /// </summary>
        public bool IsTransitioning { get; private set; }
        
        /// <summary>
        /// Checks if a panel is currently in the stack.
        /// Comparison is based on <see cref="UIPanel.PanelName"/>.
        /// </summary>
        /// <param name="panel">The panel to check for.</param>
        /// <returns>True if the panel (or another panel with the same name) is in the stack.</returns>
        public bool Contains(UIPanel panel) => m_uiStack.Contains(panel);
        
        /// <summary>
        /// The number of panels currently in the stack.
        /// </summary>
        public int Count => m_uiStack.Count;
        
        /// <summary>
        /// Pushes a panel onto the stack.
        /// </summary>
        /// <param name="panel">The panel to push. Cannot be null or already in the stack.</param>
        /// <returns>True if the panel was successfully queued for pushing.</returns>
        /// <remarks>
        /// <para>
        /// The panel is added to the stack immediately (synchronously), but lifecycle
        /// animations play asynchronously. This means:
        /// <list type="bullet">
        ///   <item><see cref="PeekPanel"/> returns the new panel immediately</item>
        ///   <item><see cref="UIPanel.OnOpened"/> animation may still be playing</item>
        /// </list>
        /// </para>
        /// <para>
        /// The previous top panel (if any) receives <see cref="UIPanel.OnLostFocus"/>
        /// before the new panel receives <see cref="UIPanel.OnOpened"/>.
        /// </para>
        /// </remarks>
        public bool PushPanel(UIPanel panel)
        {
            if (IsTransitioning)
            {
                Debug.LogWarning($"[UI] Cannot push '{panel.PanelName}' - stack is transitioning");
                return false;
            }
            
            if (m_uiStack.Contains(panel))
            {
                Debug.LogError($"[UI] Attempting to push panel '{panel.name}' that is already in the stack.");
                return false;
            }
            
            if (panel == null)
            {
                Debug.LogError("[UI] Attempting to push a null panel to the stack.");
                return false;
            }

            _ = OnUIPushed(panel);
            
            return true;
        }

        /// <summary>
        /// Pops the topmost panel from the stack.
        /// </summary>
        /// <param name="panel">Receives the popped panel, or null if the stack was empty.</param>
        /// <returns>True if a panel was successfully popped.</returns>
        /// <remarks>
        /// <para>
        /// The panel is removed from the stack immediately (synchronously), but lifecycle
        /// animations play asynchronously. This means:
        /// <list type="bullet">
        ///   <item><see cref="PeekPanel"/> returns the new top panel immediately</item>
        ///   <item><see cref="UIPanel.OnClosed"/> animation may still be playing on the popped panel</item>
        /// </list>
        /// </para>
        /// <para>
        /// The popped panel receives <see cref="UIPanel.OnClosed"/> before the new top panel
        /// (if any) receives <see cref="UIPanel.OnRegainedFocus"/>.
        /// </para>
        /// </remarks>
        public bool PopPanel(out UIPanel panel)
        {
            panel = null;
            
            var topPanel = m_uiStack.Peek().m_panel;
            if (topPanel is HUDPanel)
            {
                Debug.LogWarning($"[UI] Cannot pop HUD panel '{topPanel.PanelName}' from the stack.");
                return false;
            }
            
            if (IsTransitioning)
            {
                Debug.LogWarning("[UI] Cannot pop - stack is transitioning");
                return false;
            }
            
            if (m_uiStack.Count == 0)
            {
                Debug.LogError("[UI] Attempting to pop panel from an empty stack.");
                return false;
            }

            panel = m_uiStack.Pop().m_panel;
            _ = OnUIPopped(panel);
            
            return true;
        }

        /// <summary>
        /// Closes all panels with animations, but skips regain focus on intermediate panels.
        /// </summary>
        public async Awaitable ClearStackAsync()
        {
            IsTransitioning = true;
            try
            {
                while (m_uiStack.Count > 0)
                {
                    var panel = m_uiStack.Pop().m_panel;
                    if (panel != null)
                    {
                        await panel.OnClosed();
                        panel.ClearContext();
                    }
                }
            }
            finally
            {
                IsTransitioning = false;
            }
        }

        /// <summary>
        /// Immediately clears all panels without animations.
        /// Use for scene transitions or emergency cleanup.
        /// </summary>
        public void ClearStackImmediate()
        {
            while (m_uiStack.Count > 0)
            {
                var panel = m_uiStack.Pop().m_panel;
                panel?.ClearContext();
            }
        }
        
        /// <summary>
        /// Returns the topmost panel without removing it.
        /// </summary>
        /// <returns>The active (topmost) panel, or null if the stack is empty.</returns>
        public UIPanel PeekPanel()
        {
            return m_uiStack.Count > 0 ? m_uiStack.Peek().m_panel : null;
        }
        
        #region Additional arbitrary stack queries
        private readonly List<UIStackItem> m_tempList = new();
        
        /// <summary>
        /// Checks if a panel of the given type exists in the stack.
        /// </summary>
        public bool ContainsPanel<T>() where T : UIPanel
        {
            foreach (var item in m_uiStack)
            {
                if (item.m_panel is T)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if a specific panel instance is in the stack.
        /// </summary>
        public bool ContainsPanel(UIPanel panel)
        {
            return m_uiStack.Contains(panel);
        }

        /// <summary>
        /// Gets the first panel of the given type, or null if not found.
        /// Searches from top to bottom.
        /// </summary>
        public T GetPanel<T>() where T : UIPanel
        {
            foreach (var item in m_uiStack)
            {
                if (item.m_panel is T typed)
                    return typed;
            }
            return null;
        }

        /// <summary>
        /// Gets all panels of the given type.
        /// </summary>
        public void GetPanels<T>(List<T> results) where T : UIPanel
        {
            results.Clear();
            foreach (var item in m_uiStack)
            {
                if (item.m_panel is T typed)
                    results.Add(typed);
            }
        }

        /// <summary>
        /// Gets the panel directly below the given panel in the stack.
        /// Returns null if panel is at bottom or not in stack.
        /// </summary>
        public UIPanel GetPanelBelow(UIPanel panel)
        {
            // Stack doesn't support indexed access, so we need to convert
            m_tempList.Clear();
            m_tempList.AddRange(m_uiStack);
            
            for (int i = 0; i < m_tempList.Count - 1; i++)
            {
                if (m_tempList[i].m_panel == panel)
                    return m_tempList[i + 1].m_panel; // Stack is reversed, so +1 is "below"
            }
            return null;
        }

        /// <summary>
        /// Removes a specific panel from anywhere in the stack.
        /// Use sparingly - this breaks the normal stack flow.
        /// </summary>
        public async Awaitable<bool> RemovePanelAsync(UIPanel panel)
        {
            if (IsTransitioning) 
            {
                Debug.LogWarning("[UI] Cannot remove panel - stack is transitioning");
                return false;
            }
            
            if (!ContainsPanel(panel))
                return false;

            // If it's on top, just pop normally
            if (PeekPanel() == panel)
            {
                PopPanel(out _);
                return true;
            }

            // Otherwise, rebuild the stack without this panel
            m_tempList.Clear();
            while (m_uiStack.Count > 0)
            {
                m_tempList.Add(m_uiStack.Pop());
            }

            // Close the removed panel
            await panel.OnClosed();
            panel.ClearContext();

            // Rebuild stack (reverse because we popped in reverse order)
            for (int i = m_tempList.Count - 1; i >= 0; i--)
            {
                if (m_tempList[i].m_panel != panel)
                    m_uiStack.Push(m_tempList[i]);
            }

            return true;
        }
        #endregion

        /// <summary>
        /// Handles the async lifecycle when a panel is pushed.
        /// </summary>
        /// <param name="panel">The panel being pushed.</param>
        private async Awaitable OnUIPushed(UIPanel panel)
        {
            IsTransitioning = true;
            
            try
            {
                // Cache the previous top panel before pushing
                var previousTop = PeekPanel();

                // Push immediately so PeekPanel() returns correct value during animations
                m_uiStack.Push(panel);

                // Notify previous panel it's losing focus
                if (previousTop != null)
                {
                    await previousTop.OnLostFocus();
                }

                // Open the new panel
                await panel.OnOpened();
            }
            finally
            {
                IsTransitioning = false;
            }
        }
        
        /// <summary>
        /// Handles the async lifecycle when a panel is popped.
        /// </summary>
        /// <param name="panel">The panel that was popped (already removed from stack).</param>
        private async Awaitable OnUIPopped(UIPanel panel)
        {
            IsTransitioning = true;
            try
            {
                // Close the popped panel
                await panel.OnClosed();

                // Notify new top panel it's regaining focus
                if (m_uiStack.Count > 0)
                {
                    var topPanel = m_uiStack.Peek().m_panel;
                    await topPanel.OnRegainedFocus();
                }
            }
            finally
            {
                IsTransitioning = false;
            }
        }
    }
}