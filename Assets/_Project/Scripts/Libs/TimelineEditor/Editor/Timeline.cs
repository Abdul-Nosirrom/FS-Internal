using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace FS.Editor.Timeline
{
    /// <summary>
    /// A reusable timeline editor widget. Subclass TimelineTrack to create custom track types.
    /// </summary>
    public class Timeline : IDisposable
    {
        #region Enums
        
        public enum DurationDisplay
        {
            Time,
            NormalizedTime,
            Frames
        }
        
        #endregion

        #region Layout Configuration
        
        public float VerticalScale = 1f;
        
        private int HEADER_HEIGHT => Mathf.FloorToInt(30 * VerticalScale);
        private int TIME_DISPLAY_HEIGHT => Mathf.FloorToInt(20 * VerticalScale);
        private int CLIP_SLOT_HEIGHT => Mathf.FloorToInt(40 * VerticalScale);
        
        private static readonly Color BorderColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        private static readonly Color HeaderBgColor = new Color(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color TimeDisplayBgColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color TrackBgColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color TrackAltBgColor = new Color(0.22f, 0.22f, 0.22f, 1f);
        
        #endregion

        #region Playback State
        
        private DurationDisplay m_durationDisplay = DurationDisplay.NormalizedTime;
        private bool m_isPlaying = false;
        private float m_duration;
        private int m_frameRate = 24;
        private float m_currentTime = 0f;
        private float m_playbackSpeed = 1f;
        private float m_previousTime;
        private bool m_scrubbingThrough = false;
        
        #endregion

        #region Snapping
        
        private bool m_snappingEnabled = true;
        private int m_snapFrameInterval = 1;
        private float m_snapTimeInterval = 0.1f;
        private float m_snapNormalizedTimeInterval = 0.01f;
        
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
        
        public bool SnappingEnabled => m_snappingEnabled && SnapInterval != 0f;
        
        #endregion

        #region Zoom & Scroll
        
        private List<float> m_horizontalZoomLevels = new() { 1f, 1.5f, 2f, 3f, 4f, 5f };
        private int m_currentHorizontalZoomIdx = 0;
        private float CurrentZoom => m_horizontalZoomLevels[m_currentHorizontalZoomIdx];
        
        private Vector2 m_timelineScroll = Vector2.zero;
        private Vector2 m_inspectorScroll = Vector2.zero;
        
        #endregion

        #region Collapse State
        
        private bool m_tracksCollapsed = false;
        private bool m_inspectorCollapsed = false;
        
        #endregion

        #region Tracks & Selection
        
        private List<TimelineTrack> m_tracks = new List<TimelineTrack>();
        private TimelineTrack m_selection = null;
        private Action<Vector2, Rect> HandleTimelineContextClick;
        
        #endregion

        #region Cached Layout Values
        
        public Rect TimelineRect { get; private set; }
        public float TimelineWidth { get; private set; }
        public float TimelineHeight { get; private set; }
        
        #endregion

        #region Public Properties
        
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
        
        public bool IsScrubbingThroughTimeline => m_scrubbingThrough;

        public bool IsDirty
        {
            get
            {
                if (m_isTimelineDirty) return true;
                foreach (var track in m_tracks)
                    if (track.IsDirty) return true;
                return false;
            }
            set
            {
                m_isTimelineDirty = value;
                foreach (var track in m_tracks) track.IsDirty = value;
            }
        }
        private bool m_isTimelineDirty = false;
        
        #endregion

        #region Events
        
        public event Action<TimelineTrack> OnTrackRemoved;
        public event Action<TimelineTrack> OnTrackAdded;
        public event Action OnPlay;
        public event Action OnPause;
        public event Action OnComplete;
        public event Action<float> OnTimelineScrubbing;
        
        #endregion

        #region Constructor & Lifecycle

        public Timeline(List<TimelineTrack> data, Action<Vector2, Rect> timelineContextClick, float duration = 10)
        {
            HandleTimelineContextClick = timelineContextClick;
            m_duration = duration;
            if (data != null) m_tracks = data;
        }

        public void Dispose()
        {
            foreach (var track in m_tracks)
                track.Dispose();
            m_tracks.Clear();
        }
        
        #endregion

        #region Track Management

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
            if (m_selection == clip) m_selection = null;
            if (m_tracks.Remove(clip)) clip.Dispose();
        }
        
        #endregion

        #region Utility

        public float SnapTimelineValue(float value)
        {
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
        
        #endregion

        #region Main GUI Entry Point

        public void DoGUI(Vector2 timelineSize)
        {
            Profiler.BeginSample("Timeline.DoGUI");
            
            // Calculate dimensions - account for padding and potential scrollbar
            const float sidePadding = 10f;
            const float scrollbarWidth = 14f;
            
            float availableWidth = timelineSize.x - (sidePadding * 2) - scrollbarWidth;
            TimelineWidth = Mathf.Max(400, availableWidth * CurrentZoom);
            TimelineHeight = timelineSize.y;
            
            // Calculate track area height
            float trackAreaHeight = m_tracksCollapsed ? 0 : Mathf.Max(m_tracks.Count * CLIP_SLOT_HEIGHT, 60);
            float totalContentHeight = HEADER_HEIGHT + TIME_DISPLAY_HEIGHT + trackAreaHeight;

            // === OUTER HORIZONTAL PADDING ===
            GUILayout.BeginHorizontal();
            GUILayout.Space(sidePadding);

            // === MAIN SCROLL VIEW (contains entire timeline as one unit) ===
            m_timelineScroll = GUILayout.BeginScrollView(
                m_timelineScroll, 
                CurrentZoom > 1f,  // horizontal scrollbar when zoomed
                totalContentHeight > TimelineHeight,             // no forced vertical scrollbar
                GUILayout.Height(TimelineHeight + 20) // +20 for scrollbar space
            );

            // === SELF-CONTAINED TIMELINE AREA ===
            // Using BeginArea ensures nothing else can be inserted between our components
            Rect timelineAreaRect = GUILayoutUtility.GetRect(TimelineWidth, totalContentHeight);
            
            // Draw outer border
            SirenixEditorGUI.DrawRoundRect(timelineAreaRect, BorderColor, 4);
            Rect innerRect = new Rect(timelineAreaRect.x + 2, timelineAreaRect.y + 2, 
                                       timelineAreaRect.width - 4, timelineAreaRect.height - 4);
            
            GUILayout.BeginArea(innerRect);
            {
                // All drawing inside here uses local coordinates (0,0 is top-left of innerRect)
                float yOffset = 0;
                
                // Header
                Rect headerRect = new Rect(0, yOffset, innerRect.width, HEADER_HEIGHT);
                DrawHeader(headerRect);
                yOffset += HEADER_HEIGHT;
                
                // Time display
                Rect timeDisplayRect = new Rect(0, yOffset, innerRect.width, TIME_DISPLAY_HEIGHT);
                DrawTimeDisplay(timeDisplayRect, trackAreaHeight);
                yOffset += TIME_DISPLAY_HEIGHT;
                
                // Track content (if not collapsed)
                if (!m_tracksCollapsed)
                {
                    Rect trackContentRect = new Rect(0, yOffset, innerRect.width, trackAreaHeight);
                    TimelineRect = trackContentRect; // Store for track position calculations
                    DrawTrackContent(trackContentRect);
                }
            }
            GUILayout.EndArea();

            GUILayout.EndScrollView();
            
            GUILayout.Space(2*sidePadding);
            GUILayout.EndHorizontal();

            // === INSPECTOR SECTION (separate from timeline scroll) ===
            DrawInspectorSection();

            Profiler.EndSample();
        }
        
        #endregion

        #region Header Drawing

        private void DrawHeader(Rect headerRect)
        {
            // Background
            SirenixEditorGUI.DrawSolidRect(headerRect, HeaderBgColor);
            
            // Bottom border
            Rect borderRect = new Rect(headerRect.x, headerRect.yMax - 1, headerRect.width, 1);
            SirenixEditorGUI.DrawSolidRect(borderRect, BorderColor);
            
            // Controls layout
            float buttonSize = HEADER_HEIGHT - 8;
            float xPos = 6;
            float yPos = headerRect.y + 4;
            var buttonStyle = SirenixGUIStyles.IconButton;

            // Collapse toggle
            Rect collapseRect = new Rect(xPos, yPos, buttonSize, buttonSize);
            SdfIconType collapseIcon = m_tracksCollapsed ? SdfIconType.ChevronRight : SdfIconType.ChevronDown;
            if (SirenixEditorGUI.SDFIconButton(collapseRect, collapseIcon, buttonStyle))
                m_tracksCollapsed = !m_tracksCollapsed;
            xPos += buttonSize + 4;

            // Label
            Rect labelRect = new Rect(xPos, headerRect.y + 5, 50, HEADER_HEIGHT - 10);
            GUI.Label(labelRect, "Preview", SirenixGUIStyles.BoldLabel);
            xPos += 54;

            // Play button
            Rect playRect = new Rect(xPos, yPos, buttonSize, buttonSize);
            SdfIconType playIcon = IsPlaying ? SdfIconType.PauseFill : SdfIconType.PlayFill;
            if (SirenixEditorGUI.SDFIconButton(playRect, playIcon, buttonStyle))
                IsPlaying = !IsPlaying;
            xPos += buttonSize + 8;

            // Zoom out
            GUI.enabled = m_currentHorizontalZoomIdx > 0;
            Rect zoomOutRect = new Rect(xPos, yPos, buttonSize, buttonSize);
            if (SirenixEditorGUI.SDFIconButton(zoomOutRect, SdfIconType.ZoomOut, buttonStyle))
                m_currentHorizontalZoomIdx--;
            GUI.enabled = true;
            xPos += buttonSize + 2;

            // Zoom reset
            Rect zoomResetRect = new Rect(xPos, yPos, buttonSize, buttonSize);
            if (SirenixEditorGUI.SDFIconButton(zoomResetRect, SdfIconType.Bullseye, buttonStyle))
                m_currentHorizontalZoomIdx = 0;
            xPos += buttonSize + 2;

            // Zoom in
            GUI.enabled = m_currentHorizontalZoomIdx < m_horizontalZoomLevels.Count - 1;
            Rect zoomInRect = new Rect(xPos, yPos, buttonSize, buttonSize);
            if (SirenixEditorGUI.SDFIconButton(zoomInRect, SdfIconType.ZoomIn, buttonStyle))
                m_currentHorizontalZoomIdx++;
            GUI.enabled = true;
            xPos += buttonSize + 4;

            // Zoom label
            Rect zoomLabelRect = new Rect(xPos, headerRect.y + 6, 28, HEADER_HEIGHT - 12);
            GUI.Label(zoomLabelRect, $"{CurrentZoom:0.#}x", EditorStyles.miniLabel);
            xPos += 32;

            // === RIGHT SIDE (draw from right edge) ===
            float rightX = headerRect.width - 6;

            // Inspector toggle (rightmost)
            rightX -= buttonSize;
            Rect inspectorRect = new Rect(rightX, yPos, buttonSize, buttonSize);
            SdfIconType inspectorIcon = m_inspectorCollapsed ? SdfIconType.ArrowBarDown : SdfIconType.ArrowBarUp;
            if (SirenixEditorGUI.SDFIconButton(inspectorRect, inspectorIcon, buttonStyle))
                m_inspectorCollapsed = !m_inspectorCollapsed;
            rightX -= 8;

            // Duration display dropdown
            rightX -= 90;
            Rect durationRect = new Rect(rightX, headerRect.y + 5, 90, HEADER_HEIGHT - 10);
            m_durationDisplay = (DurationDisplay)EditorGUI.EnumPopup(durationRect, m_durationDisplay);
            
            if (m_durationDisplay == DurationDisplay.Frames)
            {
                rightX -= 55;
                Rect fpsRect = new Rect(rightX, headerRect.y + 5, 30, HEADER_HEIGHT - 10);
                m_frameRate = Mathf.Clamp(EditorGUI.IntField(fpsRect, m_frameRate), 1, 240);
                Rect fpsLabelRect = new Rect(rightX + 32, headerRect.y + 6, 20, HEADER_HEIGHT - 12);
                GUI.Label(fpsLabelRect, "fps", EditorStyles.miniLabel);
            }
            rightX -= 8;

            // Speed label and slider
            rightX -= 28;
            Rect speedValueRect = new Rect(rightX, headerRect.y + 6, 28, HEADER_HEIGHT - 12);
            GUI.Label(speedValueRect, $"{m_playbackSpeed:0.0}x", EditorStyles.miniLabel);
            rightX -= 70;
            Rect speedSliderRect = new Rect(rightX, headerRect.y + 8, 68, HEADER_HEIGHT - 16);
            m_playbackSpeed = GUI.HorizontalSlider(speedSliderRect, m_playbackSpeed, 0f, 2f);
            rightX -= 8;

            // Snap interval field (if enabled)
            if (m_snappingEnabled)
            {
                rightX -= 45;
                Rect snapFieldRect = new Rect(rightX, headerRect.y + 5, 45, HEADER_HEIGHT - 10);
                SnapInterval = EditorGUI.FloatField(snapFieldRect, SnapInterval);
            }
            rightX -= 4;

            // Snap toggle
            rightX -= buttonSize;
            Rect snapRect = new Rect(rightX, yPos, buttonSize, buttonSize);
            SdfIconType snapIcon = m_snappingEnabled ? SdfIconType.Grid3x3GapFill : SdfIconType.Grid3x3Gap;
            if (SirenixEditorGUI.SDFIconButton(snapRect, snapIcon, buttonStyle))
                m_snappingEnabled = !m_snappingEnabled;
        }
        
        #endregion

        #region Time Display Drawing

        private void DrawTimeDisplay(Rect timeDisplayRect, float trackAreaHeight)
        {
            // Background
            SirenixEditorGUI.DrawSolidRect(timeDisplayRect, TimeDisplayBgColor);
            
            // Bottom border
            Rect borderRect = new Rect(timeDisplayRect.x, timeDisplayRect.yMax - 1, timeDisplayRect.width, 1);
            SirenixEditorGUI.DrawSolidRect(borderRect, BorderColor);

            // Scrubber head
            float scrubberX = NormalizedTime * timeDisplayRect.width;
            Rect scrubberHead = new Rect(timeDisplayRect.x + scrubberX - 5, timeDisplayRect.y + 2, 10, timeDisplayRect.height - 4);
            SirenixEditorGUI.DrawRoundRect(scrubberHead, Color.red, 2);

            // Scrubber line extending into tracks
            if (!m_tracksCollapsed && trackAreaHeight > 0)
            {
                Rect scrubberLine = new Rect(timeDisplayRect.x + scrubberX - 0.5f, timeDisplayRect.yMax, 1, trackAreaHeight);
                SirenixEditorGUI.DrawSolidRect(scrubberLine, new Color(1f, 0.2f, 0.2f, 0.6f));
            }

            // Time markers
            int numDivisions = Mathf.Max(4, Mathf.RoundToInt(timeDisplayRect.width / 60));
            if (m_durationDisplay == DurationDisplay.Frames)
                numDivisions = Mathf.Min(numDivisions, Mathf.Max(1, NumFrames));

            for (int i = 0; i <= numDivisions; i++)
            {
                float percent = i / (float)numDivisions;
                float xPos = timeDisplayRect.x + percent * timeDisplayRect.width;
                
                // Tick
                bool isMajor = (i % 2 == 0) || numDivisions <= 10;
                float tickHeight = isMajor ? 8 : 5;
                Rect tickRect = new Rect(xPos, timeDisplayRect.yMax - tickHeight, 1, tickHeight);
                SirenixEditorGUI.DrawSolidRect(tickRect, new Color(0.5f, 0.5f, 0.5f, 1f));

                // Label (major ticks only)
                if (isMajor)
                {
                    string label = m_durationDisplay switch
                    {
                        DurationDisplay.NormalizedTime => $"{percent:0.00}",
                        DurationDisplay.Time => $"{percent * Duration:0.0}s",
                        DurationDisplay.Frames => $"{Mathf.RoundToInt(percent * Duration * m_frameRate)}",
                        _ => ""
                    };
                    
                    float labelOffset = (i == numDivisions) ? -28 : (i == 0) ? 2 : -14;
                    Rect labelRect = new Rect(xPos + labelOffset, timeDisplayRect.y + 1, 40, 16);
                    GUI.Label(labelRect, label, EditorStyles.miniLabel);
                }
            }

            // Interaction
            EditorGUIUtility.AddCursorRect(timeDisplayRect, MouseCursor.MoveArrow);
            HandleScrubbingInput(timeDisplayRect);
        }

        private void HandleScrubbingInput(Rect timeDisplayRect)
        {
            Event e = Event.current;
            if (e == null) return;

            if (e.type == EventType.MouseDown && e.button == 0 && timeDisplayRect.Contains(e.mousePosition))
            {
                m_scrubbingThrough = true;
                float normalized = Mathf.Clamp01((e.mousePosition.x - timeDisplayRect.x) / timeDisplayRect.width);
                m_currentTime = normalized * m_duration;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && m_scrubbingThrough)
            {
                float normalized = Mathf.Clamp01((e.mousePosition.x - timeDisplayRect.x) / timeDisplayRect.width);
                m_currentTime = normalized * m_duration;
                e.Use();
            }
            else if (e.rawType == EventType.MouseUp && m_scrubbingThrough)
            {
                m_scrubbingThrough = false;
                e.Use();
            }

            if (m_scrubbingThrough && e.type == EventType.Repaint)
                OnTimelineScrubbing?.Invoke(m_currentTime);
        }
        
        #endregion

        #region Track Content Drawing

        private void DrawTrackContent(Rect contentRect)
        {
            // Background
            SirenixEditorGUI.DrawSolidRect(contentRect, TrackBgColor);
            
            Event e = Event.current;

            // Delete key
            if (m_selection != null && e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
            {
                RemoveClip(m_selection);
                e.Use();
                return;
            }

            bool anyHovered = false;

            for (int i = m_tracks.Count - 1; i >= 0; i--)
            {
                var track = m_tracks[i];
                Rect trackSlotRect = new Rect(contentRect.x, contentRect.y + i * CLIP_SLOT_HEIGHT, 
                                               contentRect.width, CLIP_SLOT_HEIGHT);
                
                // Alternating background
                Color bgColor = (i % 2 == 0) ? TrackBgColor : TrackAltBgColor;
                SirenixEditorGUI.DrawSolidRect(trackSlotRect, bgColor);

                // Track with inset
                Rect trackContentRect = new Rect(trackSlotRect.x + 2, trackSlotRect.y + 3, 
                                                  trackSlotRect.width - 4, trackSlotRect.height - 6);
                track.DrawGUI(trackContentRect);

                if (e != null)
                {
                    track.IsHovered = track.ClipRect.Contains(e.mousePosition);
                    anyHovered |= track.IsHovered;
                }

                HandleTrackInput(track, i, e);
            }

            // Context menu on empty space
            if (!anyHovered && e != null && e.type == EventType.MouseDown && e.button == 1 && contentRect.Contains(e.mousePosition))
            {
                HandleTimelineContextClick?.Invoke(e.mousePosition, contentRect);
                e.Use();
            }

            // Empty state
            if (m_tracks.Count == 0)
            {
                GUI.Label(contentRect, "Right-click to add tracks", SirenixGUIStyles.CenteredGreyMiniLabel);
            }
        }

        private void HandleTrackInput(TimelineTrack track, int index, Event e)
        {
            if (e == null) return;

            if (e.type == EventType.MouseDown && e.button == 1 && track.IsHovered)
            {
                int idx = index;
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Delete Track"), false, () => RemoveClip(m_tracks[idx]));
                menu.ShowAsContext();
                e.Use();
            }

            if (e.type == EventType.MouseDown && e.button == 0 && track.IsHovered && !track.IsSelected)
            {
                if (m_selection != null) m_selection.IsSelected = false;
                m_selection = track;
                m_selection.IsSelected = true;
                e.Use();
            }
        }
        
        #endregion

        #region Inspector Drawing

        private void DrawInspectorSection()
        {
            if (m_inspectorCollapsed) return;

            GUILayout.Space(4);
            
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);

            EditorGUILayout.BeginVertical(GUIStyles.GUIStyles.HelpBox);
            {
                GUILayout.Label("Inspector", SirenixGUIStyles.BoldLabel);
                
                if (m_selection != null)
                {
                    m_inspectorScroll = GUILayout.BeginScrollView(m_inspectorScroll, 
                        GUILayout.MinHeight(80), GUILayout.MaxHeight(250));
                    m_selection.OnInspectorGUI();
                    GUILayout.EndScrollView();
                }
                else
                {
                    EditorGUILayout.HelpBox("No track selected", MessageType.Info);
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
            GUILayout.EndHorizontal();
        }
        
        #endregion
    }
}