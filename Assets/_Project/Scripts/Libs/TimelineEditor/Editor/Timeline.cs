using System;
using System.Collections.Generic;
using FS.RuntimeDebug;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace FS.Editor.Timeline
{
    public class Timeline : IDisposable
    {
        public enum DurationDisplay
        {
            Time,
            NormalizedTime,
            Frames
        }

        public float VerticalScale = 1f;
        private int HEADER_HEIGHT => Mathf.FloorToInt(30 * VerticalScale);
        private int TIME_DISPLAY_HEIGHT => Mathf.FloorToInt(20 * VerticalScale);
        private int CLIP_SLOT_HEIGHT => Mathf.FloorToInt(40 * VerticalScale);
        private int INSPECTOR_HEIGHT => Mathf.FloorToInt(600 * VerticalScale);
        
        private DurationDisplay m_durationDisplay = DurationDisplay.NormalizedTime;
        private bool m_isPlaying = false;
        private float m_duration;
        private int m_frameRate = 24;
        
        private bool m_snappingEnabled = true;
        private int m_snapFrameInterval = 1; // in frames
        private float m_snapTimeInterval = 0.1f; // in seconds
        private float m_snapNormalizedTimeInterval = 0.01f; // in normalized time (0-1)
        
        public float SnapInterval
        {
            get
            {
                if (m_durationDisplay == DurationDisplay.Time) return m_snapTimeInterval;
                if (m_durationDisplay == DurationDisplay.NormalizedTime) return m_snapNormalizedTimeInterval;
                if (m_durationDisplay == DurationDisplay.Frames) return m_snapFrameInterval;
                return 0f;
            }
            set
            {
                if (m_durationDisplay == DurationDisplay.Time) m_snapTimeInterval = value;
                if (m_durationDisplay == DurationDisplay.NormalizedTime) m_snapNormalizedTimeInterval = value;
                if (m_durationDisplay == DurationDisplay.Frames) m_snapFrameInterval = Mathf.RoundToInt(value);
            }
        }
        
        private float m_currentTime = 0f;
        private bool m_scrubbingThrough = false;
        private float m_playbackSpeed = 1f;

        private float m_previousTime;
        
        public bool SnappingEnabled => m_snappingEnabled && SnapInterval != 0f;
        public Rect TimelineRect { get; private set; }
        
        private Vector2 m_timelineScroll = Vector2.zero;
        private Vector2 m_trackInspectorScroll = Vector2.zero;

        private Action<Vector2, Rect> HandleTimelineContextClick;
        
        private List<TimelineTrack> m_tracks = new List<TimelineTrack>();
        private TimelineTrack m_selection = null;
        
        private List<float> m_horizontalZoomAmounts = new () { 1f, 1.5f, 2f, 3f, 4f, 5f };
        private int m_currentHorizontalZoomIdx = 0; // Start at 1x zoom
        
        public float TimelineWidth { get; private set; }
        public float TimelineHeight { get; private set; }

        public float CurrentTime
        {
            get => m_currentTime;
            set
            {
                if (value < 0f) m_currentTime = Duration;
                else if (value > Duration)
                {
                    m_currentTime = 0f;
                    OnComplete?.Invoke();
                }
                else m_currentTime = value;
            }
        }

        public float Duration
        {
            get => m_duration;
            set => m_duration = Mathf.Max(0f, value);
        }

        public float PlaybackSpeed
        {
            get => m_playbackSpeed;
            set => m_playbackSpeed = value;
        }
        
        public bool IsPlaying
        {
            get => m_isPlaying;
            set
            {
                if (m_isPlaying && !value) OnPause?.Invoke();
                if (!m_isPlaying && value) OnPlay?.Invoke();
                m_isPlaying = value;
            }
        }

        public float NormalizedTime
        {
            get => m_duration > 0f ? Mathf.Clamp01(m_currentTime / m_duration) : 0f;
            set => CurrentTime = value * m_duration;
        }

        public int Frame
        {
            get => Mathf.RoundToInt(m_currentTime * m_frameRate);
            set => CurrentTime = value / (float)m_frameRate;
        }
        
        public int NumFrames => Mathf.RoundToInt(m_duration * m_frameRate);
        
        // Probably better to have "remove" events
        public event Action<TimelineTrack> OnTrackRemoved;
        public event Action<TimelineTrack> OnTrackAdded;

        public event Action OnPlay;
        public event Action OnPause;
        public event Action OnComplete;

        public event Action<float> OnTimelineScrubbing;
        public bool IsScrubbingThroughTimeline => m_scrubbingThrough;
        
        public bool IsDirty
        {
            get
            {
                if (m_isTimelineDirty) return true;
                
                foreach (var track in m_tracks)
                {
                    if (track.IsDirty) return true;
                }

                return false;
            }
            set
            {
                m_isTimelineDirty = value;
                foreach (var track in m_tracks) track.IsDirty = value;
            }
        }

        private bool m_isTimelineDirty = false;

        public Timeline(List<TimelineTrack> data, Action<Vector2, Rect> timelineContextClick, float duration = 10)
        {
            HandleTimelineContextClick = timelineContextClick;
            m_duration = duration;

            if (data == null) return;
            m_tracks = data;
        }

        public void Dispose()
        {
            foreach (var track in m_tracks)
            {
                track.Dispose();
            }
            m_tracks.Clear();
        }

        public void AddClip(TimelineTrack newClip)
        {
            if (newClip == null || m_tracks.Contains(newClip)) return;
            
            if (m_selection != null)
                m_selection.IsSelected = false;

            m_selection = newClip;
            m_tracks.Add(newClip);

            m_isTimelineDirty = true;
            
            OnTrackAdded?.Invoke(newClip);
        }

        public void SetData(List<TimelineTrack> data)
        {
            m_selection = null;
            
            foreach (var track in m_tracks) track.Dispose();
            m_tracks.Clear();

            if (data != null) m_tracks.AddRange(data);
        }

        public void RemoveClip(TimelineTrack clip)
        {
            m_isTimelineDirty = true;
            
            OnTrackRemoved?.Invoke(clip);
            
            if (clip == null) return;
            if (m_selection == clip)
                m_selection = null;
            if (m_tracks.Remove(clip))
                clip.Dispose();
        }

        public float SnapTimelineValue(float value)
        {
            // Snapping method based on duration display, if frames, snap to nearest frame, if time then to snapTimeInterval, if normalizedTime then snap to snapNormalizedTimeInterval
            if (!SnappingEnabled) return value;
            float snapInterval = SnapInterval;
            if (m_durationDisplay == DurationDisplay.Frames) snapInterval /= m_frameRate;
            return Mathf.Round(value / snapInterval) * snapInterval;
        }
        
        public void Update()
        {
            float dT = (float)EditorApplication.timeSinceStartup - m_previousTime;
            CurrentTime += IsPlaying ? m_playbackSpeed * dT : 0f;
            m_previousTime = (float)EditorApplication.timeSinceStartup;
        }
        
        public void DoGUI(Vector2 timelineSize)
        {
            Profiler.BeginSample("Timeline.DoGUI");
            
            var ogColor = GUI.backgroundColor;
            GUI.backgroundColor = SirenixGUIStyles.DarkEditorBackground;
            TimelineWidth = Mathf.Max(600, timelineSize.x * m_horizontalZoomAmounts[m_currentHorizontalZoomIdx]);
            TimelineHeight = timelineSize.y;//Mathf.Max(400, timelineSize.y);

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            
            m_timelineScroll = GUILayout.BeginScrollView(m_timelineScroll, GUILayout.Height(TimelineHeight));
            
            //var totalRect = GUILayoutUtility.GetRect(timelineSize.x, timelineSize.y);
            //GUILayout.BeginArea(totalRect, GUIStyles.GUIStyles.HelpBox);

            var totalHeight = Mathf.Max(TimelineHeight, HEADER_HEIGHT + TIME_DISPLAY_HEIGHT + (m_tracks.Count * CLIP_SLOT_HEIGHT));
            TimelineWidth -= 40; // scroll padding
            GUILayout.BeginArea(GUILayoutUtility.GetRect(TimelineWidth, totalHeight), GUIStyles.GUIStyles.HelpBox);

            //TimelineWidth -= 10;
            {
                // Get rect for header
                //var controlRect = GUILayoutUtility.GetRect(TimelineWidth, HEADER_HEIGHT);
                //GUILayout.BeginArea(controlRect);
                var controlRect = new Rect(0, 0, TimelineWidth, HEADER_HEIGHT);
                DrawHeader(controlRect);
                //GUILayout.EndArea();

                //controlRect.y += HEADER_HEIGHT;
                //controlRect.height = TIME_DISPLAY_HEIGHT;
                var timedisplayRect = new Rect(0, HEADER_HEIGHT, TimelineWidth, TIME_DISPLAY_HEIGHT);

                // Get rect for time display
                //Rect timeDisplayRect = GUILayoutUtility.GetRect(timelineSize.x, TIME_DISPLAY_HEIGHT);
                //GUILayout.BeginArea(controlRect);
                DrawTimeDisplay(timedisplayRect);
                //GUILayout.EndArea();

                //GUILayout.Space(TIME_DISPLAY_HEIGHT);
                //controlRect.y += TIME_DISPLAY_HEIGHT;
                //controlRect.height = TimelineHeight - HEADER_HEIGHT - TIME_DISPLAY_HEIGHT;

                var timelineRect = new Rect(0, HEADER_HEIGHT + TIME_DISPLAY_HEIGHT, TimelineWidth, TimelineHeight - HEADER_HEIGHT - TIME_DISPLAY_HEIGHT);
                
                TimelineRect = timelineRect;

                // Get rect for track content
                //Rect contentRect = GUILayoutUtility.GetRect(timelineSize.x, timelineSize.y - HEADER_HEIGHT - TIME_DISPLAY_HEIGHT - TRACK_PADDING);
                //GUILayout.BeginArea(controlRect);
                DrawTrackContent(timelineRect);
                //GUILayout.EndArea();
                
                var mousePos = Event.current?.mousePosition;
                if (mousePos.HasValue)
                {
                    var mouseRect = new Rect(mousePos.Value.x - 5, mousePos.Value.y - 5, 10, 10);
                    SirenixEditorGUI.DrawRoundRect(mouseRect, Color.red, 4);
                }
            }
            //GUILayout.EndArea();
            
            GUILayout.EndArea();
            
            GUILayout.EndScrollView();
            GUI.backgroundColor = ogColor;
            
            // End padding horizontal scope
            GUILayout.Space(10);

            GUILayout.EndHorizontal();

            if (m_selection != null)
            {
                m_trackInspectorScroll =
                    GUILayout.BeginScrollView(m_trackInspectorScroll, GUIStyles.GUIStyles.HelpBox);//, GUILayout.Height(INSPECTOR_HEIGHT/3f));
                m_selection.OnInspectorGUI();
                GUILayout.EndScrollView();
            }
            
            Profiler.EndSample();
        }

        private void DrawSelectedClipInspector(Rect inspectorRect)
        {
            GUILayout.BeginArea(inspectorRect);
            m_selection?.OnInspectorGUI();
            GUILayout.EndArea();
        }

        private void DrawHeader(Rect headerRect)
        {
            float buttonHeight = headerRect.height / 1.25f;
            var sdfButtonStyle = SirenixGUIStyles.IconButton;
            
            GUILayout.BeginArea(headerRect);
            
            GUILayout.BeginHorizontal(GUIStyles.GUIStyles.HelpBox, GUILayout.Width(headerRect.width));
            
            var labelRect = EditorGUILayout.GetControlRect(GUILayout.Width(60), GUILayout.Height(headerRect.height/2));
            EditorGUI.DropShadowLabel(labelRect, "Preview");
            //GUILayout.Label("Preview", SirenixGUIStyles.BoldLabelCentered);
            
            SdfIconType playIcon = IsPlaying ? SdfIconType.PauseFill : SdfIconType.PlayFill;
            var playButtonRect = EditorGUILayout.GetControlRect(GUILayout.Width(buttonHeight), GUILayout.Height(buttonHeight));
            if (SirenixEditorGUI.SDFIconButton(playButtonRect, playIcon, sdfButtonStyle))
                IsPlaying = !IsPlaying;
            
            // Zoom amount selection dropdown from our array
            var zoomButtonRect = EditorGUILayout.GetControlRect(GUILayout.Width(buttonHeight), GUILayout.Height(buttonHeight));
            if (SirenixEditorGUI.SDFIconButton(zoomButtonRect, SdfIconType.ZoomOut, sdfButtonStyle))
                m_currentHorizontalZoomIdx = Mathf.Max(0, m_currentHorizontalZoomIdx - 1);
            zoomButtonRect.x += zoomButtonRect.width;
            if (SirenixEditorGUI.SDFIconButton(zoomButtonRect, SdfIconType.AlignMiddle, sdfButtonStyle))
                m_currentHorizontalZoomIdx = 0;
            zoomButtonRect.x += zoomButtonRect.width;
            if (SirenixEditorGUI.SDFIconButton(zoomButtonRect, SdfIconType.ZoomIn, sdfButtonStyle))
                m_currentHorizontalZoomIdx = Mathf.Min(m_horizontalZoomAmounts.Count - 1, m_currentHorizontalZoomIdx + 1);
            
            GUILayout.FlexibleSpace();

            if (m_snappingEnabled)
            {
                SnapInterval = EditorGUILayout.FloatField("", SnapInterval, GUILayout.Width(150));
            }

            SdfIconType snappingIcon = m_snappingEnabled ? SdfIconType.TicketDetailedFill : SdfIconType.TicketDetailed;
            var snappingButtonRect = EditorGUILayout.GetControlRect(GUILayout.Width(buttonHeight), GUILayout.Height(buttonHeight));
            if (SirenixEditorGUI.SDFIconButton(snappingButtonRect, snappingIcon, sdfButtonStyle))
                m_snappingEnabled = !m_snappingEnabled;
            
            GUILayout.Space(10);
            
            // PlaybackSpeed slider
            m_playbackSpeed = GUILayout.HorizontalSlider(m_playbackSpeed, 0f, 2f, GUILayout.Width(100));
            
            // Time related shenanigans
            m_durationDisplay = (DurationDisplay)SirenixEditorFields.EnumDropdown(m_durationDisplay, GUILayout.Width(60));
            switch (m_durationDisplay)
            {
                case DurationDisplay.Frames:
                    m_frameRate = SirenixEditorFields.IntField("Frame Rate", m_frameRate, GUILayout.Width(100));
                    m_frameRate = Mathf.Clamp(m_frameRate, 1, 240);
                    break;
            }

            var menuButtonRect = EditorGUILayout.GetControlRect(GUILayout.Width(buttonHeight), GUILayout.Height(buttonHeight));
            if (SirenixEditorGUI.SDFIconButton(menuButtonRect, SdfIconType.ThreeDotsVertical, sdfButtonStyle))
            {
                
            }
            
            
            GUILayout.EndHorizontal();
            
            GUILayout.EndArea();
        }

        private void DrawTimeMarker(int idx, int numDivisions, float yPos, Rect timeDisplayRect)
        {
            float percent = idx / (float)(numDivisions);
            
            float normalizedTime = percent;
            float currentTime = normalizedTime * Duration;
            int currentFrame = Mathf.RoundToInt(currentTime * m_frameRate);

            float textOffset = idx == numDivisions ? -6 : (idx == 0 ? 6 : 0);
            float markerOffset = idx == numDivisions ? -1 : 0;
            
            float xPos = idx * (timeDisplayRect.width / numDivisions);
            Handles.color = Color.black;
            Handles.DrawLine(new Vector3(xPos + markerOffset, yPos + 10, 0), new Vector3(xPos + markerOffset, yPos + timeDisplayRect.height, 0));
            Handles.color = Color.white;

            var timeRect = new Rect(xPos - 6 + textOffset, yPos - 5, 50, 20);

            if (m_durationDisplay == DurationDisplay.NormalizedTime)
                GUI.Label(timeRect, $"{normalizedTime:0.00}");
            else if (m_durationDisplay == DurationDisplay.Time)
                GUI.Label(timeRect, $"{currentTime:0.0}");
            else if (m_durationDisplay == DurationDisplay.Frames)
                GUI.Label(timeRect, $"{currentFrame}");
        }
        
        private void DrawTimeDisplay(Rect timeDisplayRect)
        {
            // Here's where we also get the scrubbing controls
            float scrubberX = NormalizedTime * timeDisplayRect.width;
            Rect scrubberRect = new Rect(scrubberX - 5, timeDisplayRect.y, 10, timeDisplayRect.height);
            EditorGUI.DrawRect(scrubberRect, Color.red);
            Rect scrubberLineRect = new Rect(scrubberX, timeDisplayRect.y, 1, TimelineHeight);
            EditorGUI.DrawRect(scrubberLineRect, Color.red);
            EditorGUIUtility.AddCursorRect(timeDisplayRect, MouseCursor.MoveArrow); // TODO: This changes cursor style when hovering over rect

            
            // Draw basically the time header w/ the appropriate time display (%, frames, time)
            // Later divisions would be based on "zoom" amount on the timeline horizontally
            // We need to calculate the number of divisions based on the width of the timeline width, ensuring that we've got adequate spacing between markers
            
            float yPos = timeDisplayRect.y + 2;
            int numDivisions = Mathf.RoundToInt(TimelineWidth / 50);
            if (m_durationDisplay == DurationDisplay.Frames) numDivisions = NumFrames;
            for (int i = 0; i <= numDivisions; i++)
            {
                DrawTimeMarker(i, numDivisions, yPos, timeDisplayRect);
            }

            Handles.color = Color.black;
            Handles.DrawLine(new Vector3(0, yPos + timeDisplayRect.height, 0), new Vector3(timeDisplayRect.width, yPos + timeDisplayRect.height, 0));
            Handles.color = Color.white;
            
            if (Event.current.type == EventType.MouseDown && timeDisplayRect.Contains(Event.current.mousePosition))
            {
                // Start scrubbing
                m_scrubbingThrough = true;
                float mouseX = Event.current.mousePosition.x;
                float newNormalizedTime = Mathf.Clamp01(mouseX / timeDisplayRect.width);
                m_currentTime = newNormalizedTime * m_duration;
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseDrag && m_scrubbingThrough)
            {
                // Start scrubbing
                float mouseX = Event.current.mousePosition.x;
                float newNormalizedTime = Mathf.Clamp01(mouseX / timeDisplayRect.width);
                m_currentTime = newNormalizedTime * m_duration;
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseUp && m_scrubbingThrough)
            {
                m_scrubbingThrough = false;
                Event.current.Use();
            }
            
            if (m_scrubbingThrough && Event.current.type == EventType.Repaint)
                OnTimelineScrubbing?.Invoke(m_currentTime);
        }
        
        private void DrawTrackContent(Rect contentRect)
        {
            var currentEvent = Event.current;
            // Selection delete
            if (m_selection != null && currentEvent is { type: EventType.KeyDown } && currentEvent.keyCode == KeyCode.Delete)
            {
                RemoveClip(m_selection);
                currentEvent.Use();
            }
            
            bool anyElementHovered = false;
            for (int c = m_tracks.Count - 1; c >= 0; c--)
            {
                var track = m_tracks[c];
                Rect trackRect = new Rect(0, contentRect.y + c * (CLIP_SLOT_HEIGHT), contentRect.width, CLIP_SLOT_HEIGHT);
                GUILayout.BeginArea(trackRect, GUIStyles.GUIStyles.HelpBox);

                trackRect.y = 5;
                trackRect.height -= 10;
                track.DrawGUI(trackRect);

                track.IsHovered = track.ClipRect.Contains(currentEvent.mousePosition);
                anyElementHovered |= track.IsHovered;
                
                // Right click for deletion
                if (currentEvent != null)
                {
                    if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1 && track.IsHovered)
                    {
                        int deletionIdx = c;
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Delete Track"), false, () =>
                        {
                            RemoveClip(m_tracks[deletionIdx]);
                        });
                        menu.ShowAsContext();
                        currentEvent.Use();
                    }

                    if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && track.IsHovered)
                    {
                        if (!track.IsSelected)
                        {
                            if (m_selection != null)
                                m_selection.IsSelected = false;
                            
                            m_selection = track;
                            m_selection.IsSelected = true;
                            currentEvent.Use();
                        }
                    }
                }
                
                GUILayout.EndArea();
                //GUILayout.Space(TRACK_PADDING);
            }

            if (!anyElementHovered)
            {
                var rect = new Rect(0, 0, contentRect.width, contentRect.height);
                //if (currentEvent is { type: EventType.ContextClick })// && rect.Contains(currentEvent.mousePosition))
                //{
                //    HandleTimelineContextClick?.Invoke(currentEvent.mousePosition, contentRect);
                //    currentEvent.Use();
                //}
                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1)
                {
                    HandleTimelineContextClick?.Invoke(currentEvent.mousePosition, contentRect);
                    currentEvent.Use();
                }
            }
        }
    }
}