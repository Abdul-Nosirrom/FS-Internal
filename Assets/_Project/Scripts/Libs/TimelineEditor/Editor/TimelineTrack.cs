using System;
using FS.Animation;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.Editor.Timeline
{
    public abstract class TimelineTrack : IDisposable
    {
        public Timeline Timeline { get; private set; }
        private bool m_isSelected = false;

        public bool IsDraggingCenter = false;
        public bool IsDraggingStart = false;
        public bool IsDraggingEnd = false;
        
        public bool IsSelected
        {
            get => m_isSelected;
            set => m_isSelected = value;
        }
        public bool IsHovered { get; set; } = false;

        public bool IsDirty = false;

        private Rect m_slotRect;
        public Rect SlotRect => m_slotRect;
        
        public TimelineTrack(Timeline owner)
        {
            Timeline = owner;
        }

        public abstract Rect ClipRect { get; }
        
        protected Rect MarkerRect(float time)
        {
            Rect clipRect = SlotRect;
            clipRect.x = (Timeline.TimelineRect.width * time) - 5;
            clipRect.width = 10;
            return clipRect;
        }

        protected Rect RangedClipRect(float startTime, float endTime)
        {
            Rect clipRect = SlotRect;
            clipRect.xMin = Timeline.TimelineRect.width * startTime;
            clipRect.xMax = Timeline.TimelineRect.width * endTime;
            return clipRect;
        }

        public abstract void DrawClipTimelineTrack();
        public abstract void DrawClipTrackContent();
        public abstract void OnInspectorGUI();

        public abstract void Dispose();

        public void DrawGUI(Rect trackRect) // Track rect specifies the height & y pos, x & width is determined by the clip
        {
            m_slotRect = trackRect;
            
            DrawClipTimelineTrack();

            //GUILayout.BeginArea(ClipRect);
            DrawClipTrackContent();
            //GUILayout.EndArea();
        }
        
        #region Default Drawing Helpers        
        // returns center move delta
        public bool DrawDefaultRangedClipSlot(Rect clipRect, ref float start, ref float center, ref float end)
        {
            var color = IsHovered ? Color.gray1 : Color.black;
            SirenixEditorGUI.DrawRoundRect(clipRect, color, 4);
            if (IsSelected) // selection rect
                SirenixEditorGUI.DrawRoundRect(clipRect, new Color(0,0,0,0), 6, Color.white, 2);

            int rangeControlWidth = 4;
            int rangeControlPadding = 8;
            var leftRect = new Rect(clipRect.x + rangeControlPadding, clipRect.y + 4, rangeControlWidth, clipRect.height - 8);
            var rightRect = new Rect(clipRect.xMax - rangeControlPadding - rangeControlWidth, clipRect.y + 4, rangeControlWidth, clipRect.height - 8);
            
            if (IsSelected && Event.current.type == EventType.MouseDown)
            {
                if (leftRect.Contains(Event.current.mousePosition) && !IsDraggingCenter &&
                    !IsDraggingEnd)
                {
                    IsDraggingStart = true;
                    Event.current.Use();
                }
                if (rightRect.Contains(Event.current.mousePosition) && !IsDraggingCenter &&
                    !IsDraggingStart)
                {
                    IsDraggingEnd = true;
                    Event.current.Use();
                }
                if (clipRect.Contains(Event.current.mousePosition) && !IsDraggingStart &&
                    !IsDraggingEnd)
                {
                    IsDraggingCenter = true;
                    Event.current.Use();
                }
            }
            else if (Event.current.rawType == EventType.MouseUp) // Mouse up or mouse left timeline track so we wont get a mouse up [hence why we use raw type] (event only happens if in region)
            {
                Debug.LogError($"Timeline Editor: Mouse up - stop dragging range control");
                //if (Event.current.type == EventType.MouseUp && (IsDraggingStart || IsDraggingCenter || IsDraggingEnd)) 
                //    Event.current.Use();
                IsDraggingStart = false;
                IsDraggingEnd = false;
                IsDraggingCenter = false;
            }

            float prevStart = start, prevCenter = center, prevEnd = end;
            bool movedStart = DrawRangeWidthControl(leftRect, IsDraggingStart, ref start);
            bool movedEnd = DrawRangeWidthControl(rightRect, IsDraggingEnd, ref end);

            bool movedCenter = IsDraggingCenter && DragRect(ref center);
            if (IsHovered)
            {
                EditorGUIUtility.AddCursorRect(clipRect, MouseCursor.Pan); // TODO: This changes cursor style when hovering over rect
            }

            bool isMovingCenterLeft = center < prevCenter;
            bool isMovingCenterRight = center > prevCenter;
            bool canMoveCenterLeft = start > 0f; // we're moving right OR
            bool canMoveCenterRight = end < 1f;
            if (movedCenter && ((isMovingCenterLeft && canMoveCenterLeft) || (isMovingCenterRight && canMoveCenterRight)))
            {
                // Set start & end based on new center
                var rangeSize = end - start;
                start = Timeline.SnapTimelineValue(center - rangeSize / 2);
                end = Timeline.SnapTimelineValue(center + rangeSize / 2);
            }
            
            // ensure start is always <= end (either by snap interval or some small value
            if (end-0.01f <= start)
            {
                if (movedStart) start = end - 0.01f;
                if (movedEnd) end = start + 0.01f;
            }

            start = Mathf.Clamp01(start);
            end = Mathf.Clamp01(end);
            
            return movedStart || movedCenter || movedEnd;
        }

        public bool DrawRangeWidthControl(Rect controlRect, bool isDragging, ref float pos)
        {
            var isHovered = controlRect.Contains(Event.current?.mousePosition ?? Vector2.negativeInfinity);
            var color = isHovered ? Color.white : Color.crimson;
            SirenixEditorGUI.DrawRoundRect(controlRect, color, 4);
            EditorGUIUtility.AddCursorRect(controlRect, MouseCursor.ResizeHorizontal); // TODO: This changes cursor style when hovering over rect

            return isDragging && DragRect(ref pos);
        }
        
        // returns new center position
        public bool DrawDefaultMarkerClipSlot(Rect clipRect, ref float pos)
        {
            clipRect.width = 12;
            var isHovered = clipRect.Contains(Event.current?.mousePosition ?? Vector2.negativeInfinity);
            var color = isHovered ? Color.white : Color.gray;
            SirenixEditorGUI.DrawRoundRect(clipRect, color, 6);
            
            if (Event.current?.type == EventType.MouseDown)
            {
                IsDraggingCenter = clipRect.Contains(Event.current.mousePosition);
            }
            else if (Event.current?.type == EventType.MouseUp)
            {
                IsDraggingCenter = false;
            }
            
            return IsDraggingCenter && DragRect(ref pos);
        }

        public bool DragRect(ref float pos)
        {
            var currentEvent = Event.current;
            if (currentEvent == null) return false;
            
            if (currentEvent.type == EventType.MouseDrag)// && rect.Contains(currentEvent.mousePosition))
            {
                currentEvent.Use();
                pos = Timeline.SnapTimelineValue(currentEvent.mousePosition.x / Timeline.TimelineWidth);
                return true;
            }
            return false;
        }
        #endregion
    }
    
    public class AnimationEventClip : TimelineTrack
    {
        private FSAnimationEvent m_event;
        private PropertyTree m_eventProp = null;
        
        public AnimationEventClip(Timeline timeline, FSAnimationEvent evt) : base(timeline)
        {
            m_event = evt;
            m_event.TimeRange = new Vector2(0.2f, 0.5f);
            m_eventProp = PropertyTree.Create(m_event);
        }

        public override void Dispose()
        {
            m_eventProp?.Dispose();
            m_eventProp = null;
        }

        public override Rect ClipRect
        {
            get
            {
                if (m_event.IsInstantEvent) return MarkerRect(m_event.Time);
                return RangedClipRect(m_event.TimeRange.x, m_event.TimeRange.y);
            }
        }
        

        public override void DrawClipTimelineTrack()
        {
            var clipRect = ClipRect;
            
            if (m_event.IsInstantEvent)
            {
                // Returns true if moved
                float pos = m_event.Time;
                if (DrawDefaultMarkerClipSlot(clipRect, ref pos))
                {
                    m_event.Time = pos;//Mathf.Clamp01(dx);
                }
            }
            else
            {
                // returns true if moved or resized
                float start = m_event.TimeRange.x;
                float center = (m_event.TimeRange.x + m_event.TimeRange.y) / 2;
                float end = m_event.TimeRange.y;
                if (DrawDefaultRangedClipSlot(clipRect, ref start, ref center, ref end))
                {
                    // Ensure we don't move left if start is 0, or right if end is 1
                    //if (m_event.TimeRange.x <= 0f && (dxStart < 0f || dxCenter < 0f || dxEnd < 0f)) return;//dxStart = dxCenter = dxEnd = 0f;
                    //if (m_event.TimeRange.y >= 1f && (dxStart > 0f || dxCenter > 0f || dxEnd > 0f)) return;//dxStart = dxCenter = dxEnd = 0f;

                    
                    m_event.TimeRange = new Vector2(start, end);
                }
            }
        }

        public override void DrawClipTrackContent()
        {
            var rect = ClipRect;
            rect.width = Mathf.Max(100, rect.width);
            EditorGUI.DropShadowLabel(rect, m_event.Event?.GetType().Name ?? "None", SirenixGUIStyles.BoldLabelCentered);
            return;
            GUILayout.BeginArea(ClipRect);
            GUILayout.FlexibleSpace();
            GUILayout.Label(m_event.Event?.GetType().Name ?? "None", SirenixGUIStyles.BoldLabelCentered);
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        public override void OnInspectorGUI()
        {
            m_eventProp?.Draw(false);
        }
    }
    
}