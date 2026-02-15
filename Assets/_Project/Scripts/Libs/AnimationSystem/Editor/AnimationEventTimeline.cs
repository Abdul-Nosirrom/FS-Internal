// ============================================================================
// AnimationEvent integration with the generic timeline editor.
// Supports both ranged events (bars with edge-resize) and instant events (markers).
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FS.Editor.Timeline;
using FS.Extensions;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.Animation.Editor
{
    // ========================================================================
    // ITimelineElement adapter for FSAnimationEvent
    // ========================================================================
    public class AnimationEventElement : ITimelineElement
    {
        private readonly FSAnimationEvent m_event;
        private readonly InspectorProperty m_eventProp;

        public FSAnimationEvent Event => m_event;
        public InspectorProperty EventProperty => m_eventProp;

        public AnimationEventElement(FSAnimationEvent animEvent, InspectorProperty evtProp)
        {
            m_event = animEvent;
            m_eventProp = evtProp;
        }

        // --- ITimelineElement ---
        public string Label => m_event.Event?.Name ?? "None";
        public bool IsMarker => !(m_event.Event?.IsRangedEvent ?? false);

        public float StartTime
        {
            get => IsMarker ? m_event.TriggerTime : m_event.TriggerRange.x;
            set
            {
                if (IsMarker)
                    m_event.TriggerTime = Mathf.Clamp01(value);
                else
                    m_event.TriggerRange = new Vector2(Mathf.Clamp01(value), m_event.TriggerRange.y);
            }
        }

        public float EndTime
        {
            get => IsMarker ? m_event.TriggerTime : m_event.TriggerRange.y;
            set
            {
                if (!IsMarker)
                    m_event.TriggerRange = new Vector2(m_event.TriggerRange.x, Mathf.Clamp01(value));
            }
        }

        public bool IsActive => m_event.Event != null;
        public Texture2D Icon => null;
        public int StableId => m_event.GetHashCode();

        // REQUIRED: elements are recreated each frame
        public override bool Equals(object obj) => obj is AnimationEventElement other && ReferenceEquals(m_event, other.m_event);
        public override int GetHashCode() => m_event?.GetHashCode() ?? 0;
    }

    // ========================================================================
    // OdinValueDrawer using TimelineView (no playback — layout & editing only)
    // ========================================================================
    public class AnimationEventTimeline : OdinValueDrawer<AnimationEventHolder>
    {
        private TimelineView m_view;
        private TimelineSelection m_selection;
        private AnimationEventElement[] m_elements;
        private bool m_showTimeline;

        #region Event Type Discovery

        private struct EventTypeAndPath
        {
            public Type EventType;
            public string Path;
            public Texture2D Icon;
            public string FullPath => string.IsNullOrEmpty(Path) ? EventType.Name : $"{Path}/{EventType.Name}";

            public EventTypeAndPath(Type eventType, string path, Texture2D icon)
            {
                EventType = eventType;
                Path = path;
                Icon = icon;
            }
        }

        private static List<EventTypeAndPath> s_eventTypesAndPaths;

        [InitializeOnLoadMethod]
        public static void InitAnimationEventTypesAndPath()
        {
            s_eventTypesAndPaths = new();
            var allEvents = ReflectionUtility.GetAllDerivedTypes<IAnimationEvent>();
            foreach (var eventType in allEvents)
            {
                var attr = eventType.GetCustomAttribute<EventPathAttribute>();
                string path = attr?.Path ?? "";
                Texture2D icon = attr?.Icon ?? EditorIcons.UnityLogo;
                s_eventTypesAndPaths.Add(new EventTypeAndPath(eventType, path, icon));
            }
        }

        #endregion

        protected override void Initialize()
        {
            m_view = new TimelineView();
            m_selection = new TimelineSelection();

            // Fixed duration of 1.0 since animation events use normalized [0,1] time
            m_view.FixedDuration = 1.0f;

            m_view.ElementSelected += e => { m_selection.Set(e); GUIUtility.keyboardControl = 0; };
            m_view.ElementDrag += OnElementDrag;
            m_view.RemoveClicked += OnRemove;
            m_view.AddClicked += () => ShowAddEventSelector();

            Undo.undoRedoEvent += OnUndoRedo;
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (m_view == null)
            {
                EditorGUILayout.HelpBox("Failed to initialize timeline", MessageType.Error);
                return;
            }

            // // Toggle visibility
            // EditorGUI.BeginChangeCheck();
            // m_showTimeline = EditorGUILayout.Toggle("Show Event Timeline", m_showTimeline);
            // if (EditorGUI.EndChangeCheck()) GUI.changed = false;
            //
            // if (!m_showTimeline) return;

            RebuildElements();
            m_selection.Validate(m_elements);

            // No playback for event timelines
            m_view.DrawTimeline(m_elements, m_selection.Selected,
                isPlaying: false, currentPlayingTime: 0, isLooping: false, isPaused: false);

            // Inspector for selected event
            if (m_selection.Selected is AnimationEventElement selectedEvent && selectedEvent.Event.Event != null)
            {
                m_view.DrawInspector("Event Inspector", () =>
                {
                    var eventProp = selectedEvent.EventProperty?.Children[1];
                    if (eventProp == null) return;
                    foreach (var child in eventProp.Children[0].Children)
                        child?.Draw();
                });
            }
        }

        private void RebuildElements()
        {
            var events = ValueEntry.SmartValue.Events ?? new List<FSAnimationEvent>();
            var evtProperties = Property.Children["Events"];

            m_elements = new AnimationEventElement[events.Count];
            for (int i = 0; i < events.Count; i++)
                m_elements[i] = new AnimationEventElement(events[i], evtProperties.Children[i]);
        }

        #region Event Handlers

        /// <summary>
        /// Receives offset-corrected (newStart, newEnd) from the view. Applies with clamping.
        /// </summary>
        private void OnElementDrag(float newStart, float newEnd, ElementDragMode mode)
        {
            if (m_selection.Selected is not AnimationEventElement sel) return;

            Property.RecordForUndo("Drag Animation Event");

            if (sel.IsMarker)
            {
                sel.StartTime = Mathf.Clamp01(newStart);
            }
            else
            {
                switch (mode)
                {
                    case ElementDragMode.Center:
                        float duration = newEnd - newStart;
                        newStart = Mathf.Clamp(newStart, 0, 1f - duration);
                        sel.StartTime = newStart;
                        sel.EndTime = newStart + duration;
                        break;

                    case ElementDragMode.StartEdge:
                        sel.StartTime = Mathf.Clamp(newStart, 0, sel.EndTime - 0.01f);
                        break;

                    case ElementDragMode.EndEdge:
                        sel.EndTime = Mathf.Clamp(newEnd, sel.StartTime + 0.01f, 1f);
                        break;
                }
            }

            Property.MarkSerializationRootDirty();
        }

        private void OnRemove()
        {
            if (m_selection.Selected is not AnimationEventElement eventElement) return;

            Property.RecordForUndo("Remove Animation Event");
            var holder = ValueEntry.SmartValue;
            holder.Events.Remove(eventElement.Event);
            ValueEntry.SmartValue = holder;
            m_selection.Clear();
            Property.MarkSerializationRootDirty();
        }

        private void ShowAddEventSelector()
        {
            var selector = new GenericSelector<Type>("", false,
                s_eventTypesAndPaths.Select(x => new GenericSelectorItem<Type>(x.FullPath, x.EventType)));

            selector.DrawConfirmSelectionButton = false;
            selector.SelectionTree.Config.DrawScrollView = true;
            selector.EnableSingleClickToSelect();

            foreach (var menuItem in selector.SelectionTree.EnumerateTree())
            {
                if (menuItem.ChildMenuItems.Count > 0 && menuItem.Value == null)
                    menuItem.Icon = EditorIcons.Folder.Raw;
                else if (menuItem.Value != null)
                {
                    var eventType = (Type)menuItem.Value;
                    var typeAndPath = s_eventTypesAndPaths.FirstOrDefault(x => x.EventType == eventType);
                    if (typeAndPath.Icon != null) menuItem.Icon = typeAndPath.Icon;
                }
            }

            selector.SelectionConfirmed += selection =>
            {
                var type = selection.FirstOrDefault();
                if (type == null) return;

                var newEvt = new FSAnimationEvent()
                {
                    Event = (IAnimationEvent)Activator.CreateInstance(type),
                    TriggerTime = 0.5f,
                    TriggerRange = new Vector2(0.4f, 0.6f)
                };

                Property.RecordForUndo("Add Animation Event");
                var holder = ValueEntry.SmartValue;
                holder.Events.Add(newEvt);
                ValueEntry.SmartValue = holder;
                Property.MarkSerializationRootDirty();
            };

            selector.ShowInPopup();
        }

        private void OnUndoRedo(in UndoRedoInfo undo) => RebuildElements();

        #endregion
    }
}
