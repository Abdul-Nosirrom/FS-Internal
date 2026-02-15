using System;
using System.Linq;
using UnityEngine;

namespace FS.Editor.Timeline
{
    /// <summary>
    /// Event-driven view layer for the timeline editor.
    /// Port of DottView — orchestrates TimelineGUI drawing and emits events for all user interactions.
    /// Supports center-drag, edge-resize, offset tracking, and configurable time scale.
    /// </summary>
    public class TimelineView
    {
        #region State

        private bool m_isTimeDragging;
        private ElementDragState m_elementDragState = new();

        // Offset tracking for smooth dragging (mirrors DoTween's dragTweenTimeShift)
        private float? m_dragOffset;
        private float m_dragInitialDuration;

        public float TimeScale { get; private set; }
        public bool IsTimeDragging => m_isTimeDragging;
        public bool IsElementDragging => m_elementDragState.IsDragging;
        public ElementDragMode CurrentDragMode => m_elementDragState.Mode;
        public bool IsSnapping { get; set; }
        public bool IsTimelineVisible { get; set; } = true;

        /// <summary>
        /// If > 0, the timeline always shows this duration range (prevents auto-zoom).
        /// Set to your total duration (e.g. 1.0 for normalized, or m_totalDuration for seconds).
        /// If 0 (default), the timeline auto-scales to fit the elements.
        /// </summary>
        public float FixedDuration { get; set; } = 0f;

        /// <summary>
        /// Hacky way to fetch elements for snapping purposes, fetched from draw-loop
        /// </summary>
        private ITimelineElement[] m_elements;

        #endregion

        #region Add Menu Configuration

        public AddMenuItem[] AddMoreItems { get; set; }

        public struct AddMenuItem
        {
            public readonly GUIContent Content;
            public readonly Type Type;
            public readonly object UserData;

            public AddMenuItem(GUIContent content, Type type, object userData = null)
            {
                Content = content;
                Type = type;
                UserData = userData;
            }

            public AddMenuItem(string label, Type type) : this(new GUIContent(label), type) { }
        }

        #endregion

        #region Events

        // --- Time scrubbing ---
        public event Action<Event> TimeDragEnd;
        public event Action<float> TimeDrag;

        // --- Element interaction ---
        /// <summary>Fired when an element is clicked/selected. Null = deselection.</summary>
        public event Action<ITimelineElement> ElementSelected;

        /// <summary>
        /// Fired continuously while dragging an element.
        /// Provides offset-corrected (newStartTime, newEndTime, dragMode).
        /// The consumer simply applies these values to the element.
        /// </summary>
        public event Action<float, float, ElementDragMode> ElementDrag;

        /// <summary>Fired when element drag ends (mouse up). Use for undo flush, etc.</summary>
        public event Action ElementDragEnd;

        // --- Toolbar ---
        public event Action AddClicked;
        public event Action<Type> AddMore;
        public event Action RemoveClicked;
        public event Action DuplicateClicked;

        // --- Transport controls ---
        public event Action PlayClicked;
        public event Action StopClicked;
        public event Action<bool> LoopToggled;
        public event Action SnapToggled;
        public event Action PreviewDisabled;

        // --- Inspector reordering ---
        public event Action InspectorUpButtonClicked;
        public event Action InspectorDownButtonClicked;

        #endregion

        #region Drawing

        /// <summary>
        /// Main draw call — renders the full timeline widget.
        /// </summary>
        public void DrawTimeline(ITimelineElement[] elements, ITimelineElement selected,
            bool isPlaying, float currentPlayingTime, bool isLooping, bool isPaused)
        {
            m_elements = elements;
            
            var rect = TimelineGUI.GetTimelineControlRect(IsTimelineVisible ? elements.Length : 0);

            TimelineGUI.Background(rect);
            var headerRect = TimelineGUI.Header(rect);

            TimeScale = CalculateTimeScale(elements);
            var timeDragStarted = false;
            var timeRect = TimelineGUI.Time(rect, TimeScale, ref m_isTimeDragging,
                () => timeDragStarted = true, TimeDragEnd);

            // --- Element rows with edge-resize support ---
            var elementsRect = rect.ShiftY(TimelineGUI.k_timelineHeaderHeight + TimelineGUI.k_timeHeight).SetHeight(0);
            if (IsTimelineVisible)
            {
                var prevDragging = m_elementDragState.IsDragging;
                elementsRect = TimelineGUI.Elements(rect, elements, TimeScale, selected,
                    ref m_elementDragState, OnElementSelectedInternal);

                // Detect drag end (was dragging, now not)
                if (prevDragging && !m_elementDragState.IsDragging)
                {
                    m_dragOffset = null;
                    ElementDragEnd?.Invoke();
                }
            }

            // --- Bottom toolbar ---
            if (TimelineGUI.AddButton(rect))
                AddClicked?.Invoke();

            if (AddMoreItems != null && AddMoreItems.Length > 0)
                TimelineGUI.AddMoreButton(rect, AddMoreItems, item => AddMore?.Invoke(item.Type));

            if (selected != null && TimelineGUI.RemoveButton(rect))
                RemoveClicked?.Invoke();

            if (selected != null && TimelineGUI.DuplicateButton(rect))
                DuplicateClicked?.Invoke();

            // --- Playback playhead ---
            if (isPlaying || isPaused)
            {
                var scaledTime = currentPlayingTime * TimeScale;
                var verticalRect = timeRect.Add(elementsRect);
                TimelineGUI.TimeVerticalLine(verticalRect, scaledTime, isPaused);
                if (isPaused)
                    TimelineGUI.PlayheadLabel(timeRect, scaledTime, currentPlayingTime);
            }

            // --- Time scrub playhead ---
            if (m_isTimeDragging)
            {
                var scaledTime = TimelineGUI.GetScaledTimeUnderMouse(timeRect);
                var rawTime = scaledTime / TimeScale;
                TimelineGUI.TimeVerticalLine(timeRect.Add(elementsRect), scaledTime, underLabel: true);
                TimelineGUI.PlayheadLabel(timeRect, scaledTime, rawTime);

                if (Event.current.type is EventType.MouseDrag || timeDragStarted)
                    TimeDrag?.Invoke(rawTime);
            }

            // --- Element drag: compute offset-corrected times and fire event ---
            if (m_elementDragState.IsDragging && selected != null
                && Event.current.type == EventType.MouseDrag)
            {
                var scaledTime = TimelineGUI.GetScaledTimeUnderMouse(timeRect);
                var rawTime = scaledTime / TimeScale;
                HandleElementDrag(selected, rawTime, m_elementDragState.Mode);
            }

            // --- Transport buttons ---
            switch (isPlaying)
            {
                case true when TimelineGUI.StopButton(rect):
                    StopClicked?.Invoke();
                    break;
                case false when TimelineGUI.PlayButton(rect):
                    PlayClicked?.Invoke();
                    break;
            }

            IsTimelineVisible = TimelineGUI.VisibilityToggle(rect, IsTimelineVisible);

            var snapToggle = TimelineGUI.SnapToggle(rect, IsSnapping);
            if (snapToggle != IsSnapping)
            {
                IsSnapping = snapToggle;
                SnapToggled?.Invoke();
            }

            var loopResult = TimelineGUI.LoopToggle(rect, isLooping);
            if (loopResult != isLooping)
                LoopToggled?.Invoke(loopResult);

            if (TimelineGUI.PreviewEye(headerRect, isPlaying, isPaused, m_isTimeDragging))
                PreviewDisabled?.Invoke();

            // --- Click empty area to deselect ---
            if (Event.current.type == EventType.MouseDown)
            {
                if (selected != null && rect.Contains(Event.current.mousePosition))
                    ElementSelected?.Invoke(null);
            }
        }

        public void DrawInspector(string label, Action drawContent)
        {
            TimelineGUI.Inspector(label, drawContent, InspectorUpButtonClicked, InspectorDownButtonClicked);
        }

        public void DrawInspector(UnityEditor.Editor editor)
        {
            TimelineGUI.Inspector(editor, InspectorUpButtonClicked, InspectorDownButtonClicked);
        }

        #endregion

        #region Internal Drag Logic

        private void OnElementSelectedInternal(ITimelineElement element)
        {
            m_dragOffset = null; // Reset offset tracking on new selection
            ElementSelected?.Invoke(element);
        }

        /// <summary>
        /// Computes offset-corrected start/end times based on the drag mode, then fires ElementDrag.
        /// Offset is captured on the first drag frame so the element doesn't snap to the mouse.
        /// </summary>
        private void HandleElementDrag(ITimelineElement selected, float rawTime, ElementDragMode mode)
        {
            float newStart = selected.StartTime;
            float newEnd = selected.EndTime;

            switch (mode)
            {
                case ElementDragMode.Center:
                    if (!m_dragOffset.HasValue)
                    {
                        m_dragOffset = rawTime - selected.StartTime;
                        m_dragInitialDuration = selected.Duration;
                    }
                    newStart = rawTime - m_dragOffset.Value;
                    newStart = TrySnap(selected, newStart);
                    newEnd = newStart + m_dragInitialDuration;
                    break;

                case ElementDragMode.StartEdge:
                    if (!m_dragOffset.HasValue)
                        m_dragOffset = rawTime - selected.StartTime;
                    newStart = rawTime - m_dragOffset.Value;
                    newStart = TrySnap(selected, newStart);
                    // newEnd stays the same
                    break;

                case ElementDragMode.EndEdge:
                    if (!m_dragOffset.HasValue)
                        m_dragOffset = rawTime - selected.EndTime;
                    newEnd = rawTime - m_dragOffset.Value;
                    newEnd = TrySnap(selected, newEnd);
                    // newStart stays the same
                    break;
            }

            ElementDrag?.Invoke(newStart, newEnd, mode);
        }
        
        private float TrySnap(ITimelineElement selection, float time)
        {
            if (!IsSnapActive() || m_elements == null)
                return time;//(float)System.Math.Round(time, 2);

            var snapThreshold = 1f / 40f / TimeScale;
            var sel = selection;
            var snapPoints = m_elements
                .Where(e => !e.Equals(sel))
                .SelectMany(e => new[] { e.StartTime, e.EndTime })
                .Distinct().ToArray();

            if (snapPoints.Length == 0)
                return (float)System.Math.Round(time, 2);

            var nearest = snapPoints.OrderBy(sp => Mathf.Abs(sp - time)).First();
            if (System.Math.Abs(nearest - time) < snapThreshold)
                return nearest;

            return (float)System.Math.Round(time, 2);
        }

        private bool IsSnapActive()
        {
            var reverseSnap = Event.current.control;
            return reverseSnap ? !IsSnapping : IsSnapping;
        }

        #endregion

        #region Helpers

        private float CalculateTimeScale(ITimelineElement[] elements)
        {
            if (FixedDuration > 0f)
                return 1f / FixedDuration;

            var maxTime = elements.Length > 0
                ? elements.Max(e => e.EndTime)
                : 1f;
            maxTime = Mathf.Max(maxTime, 0.01f);
            return 1f / maxTime;
        }

        #endregion
    }
}
