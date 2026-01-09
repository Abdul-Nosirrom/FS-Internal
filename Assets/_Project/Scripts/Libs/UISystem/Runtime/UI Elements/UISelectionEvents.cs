using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FS.UI
{
    /// <summary>
    /// Component that adds selection/deselection animation support to any <see cref="Selectable"/>.
    /// 
    /// <para>
    /// <b>Purpose:</b>
    /// Bridges Unity's EventSystem selection events to the <see cref="UISelectableAnimation"/> system.
    /// Add this component alongside a Button, Slider, or other Selectable to get animated feedback
    /// when the element gains or loses focus.
    /// </para>
    /// 
    /// <para>
    /// <b>Events Handled:</b>
    /// <list type="bullet">
    ///   <item><b>Select:</b> Plays animation forward when element receives focus</item>
    ///   <item><b>Deselect:</b> Plays animation in reverse when element loses focus</item>
    ///   <item><b>PointerEnter:</b> Automatically selects the element (mouse hover = focus)</item>
    ///   <item><b>PointerExit:</b> Clears selection (mouse exit = lose focus)</item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Mouse-to-Gamepad Transition:</b>
    /// The PointerEnter/Exit handling ensures smooth transitions between input methods.
    /// When hovering with mouse, the element becomes selected just as it would with gamepad navigation.
    /// This means the same selection animation plays regardless of input method.
    /// </para>
    /// 
    /// <example>
    /// <code>
    /// // Setup in inspector:
    /// // 1. Add UISelectionEvents to a Button
    /// // 2. Assign a UIAnimator that scales up/glows
    /// // 3. The animation plays forward on select, reverse on deselect
    /// </code>
    /// </example>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Known Issue:</b> There's a TODO note about a double-select event when pointer exits.
    /// This is tracked in <see cref="UIFocusHistory.ValidateFocus"/>.
    /// </para>
    /// <para>
    /// Requires a <see cref="Selectable"/> component on the same GameObject.
    /// Will disable itself and log an error if no Selectable is found.
    /// </para>
    /// </remarks>
    /// <seealso cref="UISelectableAnimation"/>
    /// <seealso cref="UISelectableEvents"/>
    [AddComponentMenu("Free Skies/UI/Events/UI Selection Events")]
    public class UISelectionEvents : MonoBehaviour
    {
        /// <summary>
        /// The animation configuration for selection state changes.
        /// Plays forward on select, reverse (automatically set) on deselect.
        /// </summary>
        [SerializeField, HideLabel] private UISelectableAnimation OnElementSelected;
        
        private Selectable m_selectable;
        private EventTrigger m_eventTrigger;

        /// <summary>
        /// Lazy-initialized <see cref="EventTrigger"/> for hooking into UI events.
        /// Creates one if not already present on the GameObject.
        /// </summary>
        public EventTrigger UIEvents
        {
            get
            {
                if (m_eventTrigger != null) return m_eventTrigger;
                m_eventTrigger = GetComponent<EventTrigger>();
                if (!m_eventTrigger) m_eventTrigger = gameObject.AddComponent<EventTrigger>();
                return m_eventTrigger;
            }
        }

        private void Awake()
        {
            m_selectable = GetComponent<Selectable>();
            if (m_selectable == null)
            {
                Debug.LogError($"[UI] UISelectionEvents requires a Selectable component on the same GameObject. Disabling {nameof(UISelectionEvents)} on {gameObject.name}.", gameObject);
                enabled = false;
                return;
            }
            
            InitializeEventTrigger();
        }

        /// <summary>
        /// Sets up EventTrigger entries for all handled event types.
        /// </summary>
        private void InitializeEventTrigger()
        {
            // Selection event (gamepad/keyboard focus or programmatic)
            EventTrigger.Entry selectionEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.Select
            };
            selectionEntry.callback.AddListener(OnSelected);
            UIEvents.triggers.Add(selectionEntry);
            
            // Deselection event (lost focus)
            EventTrigger.Entry deselectionEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.Deselect
            };
            deselectionEntry.callback.AddListener(OnDeselected);
            UIEvents.triggers.Add(deselectionEntry);
            
            // Pointer enter (mouse hover starts)
            EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            pointerEnterEntry.callback.AddListener(OnPointerEnter);
            UIEvents.triggers.Add(pointerEnterEntry);
            
            // Pointer exit (mouse hover ends)
            EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerExit
            };
            pointerExitEntry.callback.AddListener(OnPointerExit);
            UIEvents.triggers.Add(pointerExitEntry);
        }
        
        /// <summary>
        /// Called when the element receives focus.
        /// Plays the selection animation forward.
        /// </summary>
        private void OnSelected(BaseEventData data)
        {
            OnElementSelected.m_reverse = false;
            _ = OnElementSelected.Play();
        }
        
        /// <summary>
        /// Called when the element loses focus.
        /// Plays the selection animation in reverse.
        /// </summary>
        private void OnDeselected(BaseEventData data)
        {
            OnElementSelected.m_reverse = true;
            _ = OnElementSelected.Play();
        }
        
        /// <summary>
        /// Called when the mouse pointer enters the element.
        /// Automatically selects the element to unify mouse hover with gamepad selection.
        /// </summary>
        /// <remarks>
        /// This makes mouse hover behave like gamepad focus - the element becomes selected,
        /// triggering the same selection animation. This provides consistent visual feedback
        /// regardless of input method.
        /// </remarks>
        private void OnPointerEnter(BaseEventData data)
        {
            if (data is PointerEventData pointerData)
            {
                // Find the selectable (might be on a child object)
                var sel = pointerData.pointerEnter.GetComponent<Selectable>();
                if (sel == null)
                {
                    sel = pointerData.pointerEnter.GetComponentInParent<Selectable>();
                }
                
                pointerData.selectedObject = sel.gameObject;
                pointerData.Use();
            }
        }
        
        /// <summary>
        /// Called when the mouse pointer exits the element.
        /// Clears selection to deselect the element.
        /// </summary>
        /// <remarks>
        /// This ensures that when the mouse leaves an element, it properly deselects,
        /// allowing <see cref="UIFocusHistory"/> to potentially restore focus to a
        /// previous element when switching back to gamepad input.
        /// </remarks>
        private void OnPointerExit(BaseEventData data)
        {
            if (data is PointerEventData pointerData)
            {
                pointerData.selectedObject = null;
                pointerData.Use();
            }
        }
    }
}