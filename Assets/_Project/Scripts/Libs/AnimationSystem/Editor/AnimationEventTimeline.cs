using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FS.Editor;
using FS.Editor.Timeline;
using FS.Extensions;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.Animation.Editor
{
    public class AnimationEventTrack : TimelineTrack
    {
        public FSAnimationEvent m_event;
        private InspectorProperty m_eventProp;
        
        public AnimationEventTrack(Timeline owner, FSAnimationEvent animEvent, InspectorProperty evtProp) : base(owner)
        {
            m_event = animEvent;
            m_eventProp = evtProp;
        }

        public override Rect ClipRect
        {
            get
            {
                if (m_event.Event.IsRangedEvent) return RangedClipRect(m_event.TriggerRange.x, m_event.TriggerRange.y);
                return MarkerRect(m_event.TriggerTime);
            }
        }
        
        public override void DrawClipTimelineTrack()
        {
            if (m_event.Event.IsRangedEvent)
            {
                var start = m_event.TriggerRange.x;
                var end = m_event.TriggerRange.y;
                var center = (start + end) * 0.5f;
                if (DrawDefaultRangedClipSlot(ClipRect, ref start, ref center, ref end))
                {
                    m_event.TriggerRange = new Vector2(start, end);
                    IsDirty = true;
                }
            }
            else
            {
                var time = m_event.TriggerTime;
                if (DrawDefaultMarkerClipSlot(ClipRect, ref time))
                {
                    m_event.TriggerTime = time;
                    IsDirty = true;
                }
            }
        }

        public override void DrawClipTrackContent()
        {
            var rect = ClipRect;
            rect.width = Mathf.Max(100, rect.width);
            EditorGUI.DropShadowLabel(rect, m_event.Event?.Name, SirenixGUIStyles.BoldLabelCentered);
        }

        public override void OnInspectorGUI()
        {
            // TODO: If event is null, instead show a selector to set the event type
            if (m_event.Event == null)
            {
                EditorGUILayout.HelpBox("Event is null, please select an event type [WIP]", MessageType.Error);
                return;
            }
            
            var eventProper = m_eventProp?.Children[1];
            if (eventProper == null) return;
            foreach (var child in eventProper.Children[0].Children)
                child?.Draw();
        }

        public override void Dispose() {}
    }
    
    // Just a timeline to lay out events, doesnt support playback shit
    public class AnimationEventTimeline : OdinValueDrawer<AnimationEventHolder>
    {
        
        private Timeline m_timeline;
        
        #region Context Menu Utility
        
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
                var eventPathAttribute = eventType.GetCustomAttribute<EventPathAttribute>();
                string path = eventPathAttribute?.Path ?? "";
                Texture2D icon = eventPathAttribute?.Icon ?? EditorIcons.UnityLogo;
                
                s_eventTypesAndPaths.Add(new EventTypeAndPath(eventType, path, icon));
            }
        }
        
        #endregion


        protected override void Initialize()
        {
            m_timeline = new Timeline(null, OnContextClick, 1f);
            ValidateTimelineEventData();
            m_timeline.OnTrackRemoved += OnTrackRemoved;
            Undo.undoRedoEvent += OnUndoRedo;
        }

        private bool m_showTimeline = false;
        
        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (m_timeline == null)
            {
                EditorGUILayout.HelpBox("Failed to initialize timeline", MessageType.Error);
                return;
            }
            
            EditorGUI.BeginChangeCheck(); // Intercept this
            FreeSkiesEditor.ToggleButton("Show Event Timeline", ref m_showTimeline);
            if (EditorGUI.EndChangeCheck())
                GUI.changed = false; // Prevent animation from getting marked dirty
            
            if (!m_showTimeline) return;
            
            var eventCount = ValueEntry.SmartValue.Events.Count;
            float timelineHeight = Mathf.Clamp(eventCount * 50, 150, 300);
            Vector2 timelineSize = new Vector2(Screen.width, timelineHeight);
            m_timeline.IsPlaying = false;
            m_timeline.VerticalScale = 0.75f;
            m_timeline.DoGUI(timelineSize);
        }

        private async void ValidateTimelineEventData()
        {
            await Awaitable.WaitForSecondsAsync(0.1f);
            
            var events = ValueEntry.SmartValue.Events;
            events ??= new();
            
            var evtProperties = Property.Children["Events"];
            var data = new List<TimelineTrack>();
            
            for (int e = 0; e < events.Count; e++)
            {
                var animEvent = events[e];
                var animEventProp = evtProperties.Children[e];
                //var animEventProp = Property.Children[$"Events.${e}"];
                //evtProperties.Children[e];
                var track = new AnimationEventTrack(m_timeline, animEvent, animEventProp);
                data.Add(track);
            }

            m_timeline.SetData(data);
        }
        
        private void OnUndoRedo(in UndoRedoInfo undo)
        {
            ValidateTimelineEventData();
        }

        private void OnTrackRemoved(TimelineTrack removedTrack)
        {
            // TODO: Removing events doesnt update the parent animation
            GUI.changed = true;
            
            Property.RecordForUndo("Removing Animation Event");
            
            var animEventTrack = (AnimationEventTrack)removedTrack;
            var eventHolder = ValueEntry.SmartValue;
            eventHolder.Events.Remove(animEventTrack.m_event);
            
            ValueEntry.SmartValue = eventHolder;
            
            Property.MarkSerializationRootDirty();
            
            m_timeline.IsDirty = false;
        }

        private void OnContextClick(Vector2 clickPos, Rect contentRect)
        {
            // Initialize Selector
            var selector = new GenericSelector<Type>("", false, s_eventTypesAndPaths
                .Select(x => new GenericSelectorItem<Type>(x.FullPath, x.EventType)));

            //selector.CheckboxToggle = false;
            selector.DrawConfirmSelectionButton = false;
            selector.SelectionTree.Config.DrawScrollView = true;
            selector.EnableSingleClickToSelect();

            foreach (var menuItem in selector.SelectionTree.EnumerateTree())
            {
                // Assign folder items to drop down menu itmes
                if (menuItem.ChildMenuItems.Count > 0 && menuItem.Value == null)
                {
                    menuItem.Icon = EditorIcons.Folder.Raw;
                }
                else if (menuItem.Value  != null)  
                {
                    // Assign event type icons
                    var eventType = (Type)menuItem.Value;
                    var eventTypeAndPath = s_eventTypesAndPaths.FirstOrDefault(x => x.EventType == eventType);
                    if (eventTypeAndPath.Icon != null)
                    {
                        menuItem.Icon = eventTypeAndPath.Icon;
                    }
                }  
            }

            selector.SelectionConfirmed += (selection) =>
            {
                Type type = selection.Count() > 0 ? selection.First() : null;
                if (type != null)// && type != this.ValueEntry.TypeOfValue)
                {
                    var newEvt = new FSAnimationEvent()
                    {
                        Event = (IAnimationEvent)Activator.CreateInstance(type),
                        TriggerTime = clickPos.x / contentRect.width,
                        TriggerRange = new Vector2(clickPos.x / contentRect.width, clickPos.x / contentRect.width + 0.1f)
                    };
                    
                    Property.RecordForUndo("Added New Event");

                    var eventHolder = ValueEntry.SmartValue;
                    eventHolder.Events.Add(newEvt);
                    ValueEntry.SmartValue = eventHolder;
                
                    Property.MarkSerializationRootDirty();
                    ValidateTimelineEventData();
                }
            };
            
            var selectorRect = new Rect(clickPos.x - 125, clickPos.y - contentRect.height * m_timeline.VerticalScale/2f, 250, 100);

            selector.SetSelection(this.ValueEntry.TypeOfValue);
            selector.ShowInPopup(selectorRect, Vector2.zero);// new Vector2(rect.width, 240));
        }
    }
}