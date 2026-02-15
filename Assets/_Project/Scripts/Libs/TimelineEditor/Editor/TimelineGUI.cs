using System;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FS.Editor.Timeline
{
    /// <summary>
    /// Static GUI drawing methods for the timeline editor.
    /// Port of DottGUI — all rendering logic lives here, decoupled from any animation system.
    /// Layout: [Header | Time ruler | Element rows | Bottom toolbar].
    /// </summary>
    public static class TimelineGUI
    {
        #region Constants and Styles

        private const float k_rowHeight = 20;
        private const int k_bottomHeight = 30;
        public const int k_timeHeight = 20;
        public const int k_timelineHeaderHeight = 28;
        private const float k_minElementRectWidth = 16f;
        private const float k_edgeHitWidth = 8f;

        private static readonly Vector2 s_playButtonSize = new(44, 24);
        private static readonly Vector2 s_toggleSize = new(24, 24);
        private static readonly Color s_toggleFadeColor = new(1f, 1f, 1f, 0.7f);
        private static readonly Color s_playheadColor = new(0.19f, 0.44f, 0.89f);

        private static readonly Color[] s_elementColors =
        {
            Color.red, Color.green, Color.blue,
            Color.yellow, Color.cyan, Color.magenta
        };

        private static readonly GUIStyle s_inspectorHeaderStyle = new(EditorStyles.boldLabel)
            { alignment = TextAnchor.MiddleLeft };
        private static readonly Vector2 s_inspectorButtonSize = new(24f, 20f);
        private static readonly GUIContent s_inspectorDownButton = EditorGUIUtility.TrTextContent("\u2193", "Move Down");
        private static readonly GUIContent s_inspectorUpButton = EditorGUIUtility.TrTextContent("\u2191", "Move Up");
        private static readonly GUIStyle s_inspectorButtonStyle = new(EditorStyles.iconButton)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
            fixedWidth = 0, fixedHeight = 0
        };

        private static readonly GUIStyle s_timelineHeaderStyle = new(EditorStyles.boldLabel)
            { alignment = TextAnchor.MiddleCenter };

        private static readonly GUIStyle s_addButtonStyle = new(EditorStyles.miniButtonLeft) { fixedHeight = 0 };
        private static readonly GUIStyle s_addMoreButtonStyle = new(EditorStyles.miniButtonRight) { fixedHeight = 0 };

        #endregion

        #region Layout

        public static Rect GetTimelineControlRect(int elementCount)
        {
            return EditorGUILayout.GetControlRect(false,
                k_timelineHeaderHeight + k_timeHeight + elementCount * k_rowHeight + k_bottomHeight);
        }

        public static void Background(Rect rect)
        {
            RoundRect(rect, Color.black.SetAlpha(0.3f), borderRadius: 4);
            RoundRect(rect, Color.black, borderRadius: 4, borderWidth: 1);
        }

        public static Rect Header(Rect rect)
        {
            rect = rect.SetHeight(k_timelineHeaderHeight);
            GUI.Label(rect, "Timeline", s_timelineHeaderStyle);
            var bottomLine = new Rect(rect.x, rect.y + rect.height, rect.width, 1);
            EditorGUI.DrawRect(bottomLine, Color.black);
            return rect;
        }

        #endregion

        #region Preview Eye

        public static bool PreviewEye(Rect headerRect, bool isPlaying, bool isPaused, bool isTimeDragging)
        {
            if (!isPlaying && !isPaused && !isTimeDragging) return false;

            var iconSize = Vector2.one * 16f;
            var eyeShift = new Vector2(31f, 0f);
            var iconRect = new Rect(
                headerRect.x + headerRect.width * 0.5f + eyeShift.x,
                headerRect.y + (headerRect.height - iconSize.y) / 2 + eyeShift.y,
                iconSize.x, iconSize.y);

            var clickArea = isPlaying ? iconRect : headerRect.Expand(-48f, 0);
            var hover = !isTimeDragging && clickArea.Contains(Event.current.mousePosition);

            var eyeIcon = EditorGUIUtility.TrIconContent(
                hover ? "animationvisibilitytoggleoff" : "animationvisibilitytoggleon",
                "Disable scene preview mode.");

            using (new GUIColorScope(background: Color.white.SetAlpha(0f), content: Color.white.SetAlpha(0.3f)))
            {
                EditorGUIUtility.AddCursorRect(iconRect, MouseCursor.Link);
                if (GUI.Button(iconRect, eyeIcon, EditorStyles.iconButton))
                    return true;
            }

            if (Event.current.type == EventType.MouseDown && clickArea.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }

            return false;
        }

        #endregion

        #region Time Ruler

        public static Rect Time(Rect rect, float timeScale, ref bool isDragging, Action start, Action<Event> end)
        {
            rect = rect.ShiftY(k_timelineHeaderHeight).SetHeight(k_timeHeight);

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, normal = { textColor = Color.white.SetAlpha(0.5f) }
            };

            const int count = 10;
            const float step = 1f / count;
            for (var i = 0; i < count; i++)
            {
                var time = i * step;
                var position = new Rect(rect.x + i * step * rect.width, rect.y, step * rect.width, rect.height);
                time /= timeScale;
                GUI.Label(position, time.ToString("0.00"), style);
            }

            var bottomLine = new Rect(rect.x, rect.y + rect.height, rect.width, 1);
            EditorGUI.DrawRect(bottomLine, Color.black);

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            ProcessDragEvents(rect, ref isDragging, start, end);

            return rect;
        }

        public static float GetScaledTimeUnderMouse(Rect timeRect)
        {
            var time = (Event.current.mousePosition.x - timeRect.x) / timeRect.width;
            return Mathf.Clamp01(time);
        }

        public static void TimeVerticalLine(Rect rect, float scaledTime, bool underLabel)
        {
            var shift = underLabel ? 10 : 1;
            var verticalLine = new Rect(rect.x + scaledTime * rect.width, rect.y + shift, 1, rect.height - shift);
            EditorGUI.DrawRect(verticalLine, s_playheadColor);
        }

        public static void PlayheadLabel(Rect timeRect, float scaledTime, float rawTime)
        {
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };

            var position = new Vector2(timeRect.x + scaledTime * timeRect.width, timeRect.y);
            var labelContent = new GUIContent(rawTime.ToString("0.00"));

            const int yShift = 1;
            var labelRect = new Rect(position.x, position.y + yShift, 32, timeRect.height - yShift * 2);
            labelRect.x -= labelRect.width * 0.5f;
            const int maxXShift = 4;
            labelRect.x = Mathf.Clamp(labelRect.x, timeRect.x - maxXShift, timeRect.xMax - labelRect.width + maxXShift);

            RoundRect(labelRect, s_playheadColor, borderRadius: 8);
            GUI.Label(labelRect, labelContent, labelStyle);
        }

        #endregion

        #region Element Rows

        /// <summary>
        /// Draws all timeline element rows with full interaction: selection, center-drag, edge-resize.
        /// </summary>
        public static Rect Elements(Rect rect, ITimelineElement[] elements, float timeScale,
            ITimelineElement selected, ref ElementDragState dragState, Action<ITimelineElement> elementSelected)
        {
            rect = rect.ShiftY(k_timelineHeaderHeight + k_timeHeight).SetHeight(elements.Length * k_rowHeight);

            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                var rowRect = new Rect(rect.x, rect.y + i * k_rowHeight, rect.width, k_rowHeight);
                var isSelected = selected != null && selected.Equals(element);

                // Draw the element visuals
                var elementRect = DrawElement(element, rowRect, isSelected, timeScale);

                // --- Interaction: MouseDown → detect zone and start drag ---
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                    && elementRect.Contains(Event.current.mousePosition))
                {
                    var mode = DetectDragZone(element, elementRect, Event.current.mousePosition);
                    dragState.IsDragging = true;
                    dragState.Mode = mode;
                    elementSelected?.Invoke(element);
                    Event.current.Use();
                }

                // --- Cursor hints for edge resize ---
                if (!element.IsMarker && elementRect.width > k_edgeHitWidth * 3)
                {
                    var leftEdge = new Rect(elementRect.x, elementRect.y, k_edgeHitWidth, elementRect.height);
                    var rightEdge = new Rect(elementRect.xMax - k_edgeHitWidth, elementRect.y, k_edgeHitWidth, elementRect.height);
                    EditorGUIUtility.AddCursorRect(leftEdge, MouseCursor.ResizeHorizontal);
                    EditorGUIUtility.AddCursorRect(rightEdge, MouseCursor.ResizeHorizontal);
                }

                // Row separator
                var bottomLine = new Rect(rowRect.x, rowRect.y + rowRect.height, rowRect.width, 1);
                EditorGUI.DrawRect(bottomLine, Color.black);
            }

            // --- MouseUp → end any active drag ---
            if (dragState.IsDragging && Event.current.type == EventType.MouseUp)
            {
                dragState.IsDragging = false;
                dragState.Mode = ElementDragMode.None;
                Event.current.Use();
            }

            return rect;
        }

        /// <summary>
        /// Determines which part of the element was clicked: left edge, right edge, or center.
        /// </summary>
        private static ElementDragMode DetectDragZone(ITimelineElement element, Rect elementRect, Vector2 mousePos)
        {
            if (element.IsMarker)
                return ElementDragMode.Center;

            if (elementRect.width <= k_edgeHitWidth * 3)
                return ElementDragMode.Center; // Too narrow for edge handles

            float localX = mousePos.x - elementRect.x;
            if (localX < k_edgeHitWidth) return ElementDragMode.StartEdge;
            if (localX > elementRect.width - k_edgeHitWidth) return ElementDragMode.EndEdge;
            return ElementDragMode.Center;
        }

        private static Rect DrawElement(ITimelineElement element, Rect rowRect, bool isSelected, float timeScale)
        {
            if (element.IsMarker)
                return DrawMarkerElement(element, rowRect, isSelected, timeScale);
            return DrawRangedElement(element, rowRect, isSelected, timeScale);
        }

        #endregion

        #region Marker Element

        private static Texture2D s_defaultMarkerIcon;
        private static Texture2D DefaultMarkerIcon
        {
            get
            {
                if (s_defaultMarkerIcon == null)
                    s_defaultMarkerIcon = ImageFromBase64(k_defaultMarkerIconBase64);
                return s_defaultMarkerIcon;
            }
        }

        private static Rect DrawMarkerElement(ITimelineElement element, Rect rowRect, bool isSelected, float timeScale)
        {
            void DrawIcon(bool isHovered, Rect iconRect)
            {
                var iconColor = Color.white.SetAlpha(0.6f);
                if (isSelected) iconColor = new Color(0.2f, 0.6f, 1f);
                else if (isHovered) iconColor = Color.white.SetAlpha(0.5f);

                var icon = element.Icon != null ? element.Icon : DefaultMarkerIcon;
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true, 0, iconColor, 0, 0);
            }

            void DrawUnderline(bool isHovered, Rect textRect)
            {
                if (!isSelected && !isHovered) return;
                var underlineRect = new Rect(textRect.x, textRect.yMax - 4, textRect.width, 1);
                EditorGUI.DrawRect(underlineRect, isHovered ? Color.white.SetAlpha(0.7f) : Color.white);
            }

            var textStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold, fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                richText = true
            };

            var iconX = CalculateX(rowRect, element.StartTime, timeScale);
            var iconRect2 = new Rect(iconX, rowRect.y, 10, 20);
            var labelContent = new GUIContent(element.Label);

            textStyle.padding = new RectOffset((int)iconRect2.width + 4, 0, 0, 1);
            var textWidth = textStyle.CalcSize(labelContent).x;
            var hitRect = new Rect(iconRect2.x, rowRect.y, textWidth, rowRect.height);

            var onRightSide = hitRect.x > rowRect.x + rowRect.width * 0.5f;
            if (onRightSide && hitRect.xMax > rowRect.xMax)
            {
                (textStyle.padding.right, textStyle.padding.left) = (textStyle.padding.left, textStyle.padding.right);
                hitRect.x = iconRect2.xMax - textWidth;
            }

            var textOnlyRect = hitRect.Shift(textStyle.padding.left, 0, -textStyle.padding.horizontal, 0);
            var isHovered = hitRect.Contains(Event.current.mousePosition);

            DrawIcon(isHovered, iconRect2);
            DrawUnderline(isHovered, textOnlyRect);
            GUI.Label(hitRect, labelContent, textStyle);

            return hitRect;
        }

        #endregion

        #region Ranged Element

        private static Rect DrawRangedElement(ITimelineElement element, Rect rowRect, bool isSelected, float timeScale)
        {
            // Position from StartTime and EndTime
            var startX = CalculateX(rowRect, element.StartTime, timeScale);
            var endX = CalculateX(rowRect, element.EndTime, timeScale);
            var width = Mathf.Max(endX - startX, k_minElementRectWidth);

            var elementRect = new Rect(startX, rowRect.y, width, rowRect.height).Expand(-1);
            var alphaMultiplier = element.IsActive ? 1f : 0.4f;

            // Background fill
            RoundRect(elementRect, Color.gray.SetAlpha(0.3f * alphaMultiplier), borderRadius: 4);

            // Selection / hover border
            var mouseHover = elementRect.Contains(Event.current.mousePosition);
            if (isSelected)
                RoundRect(elementRect, Color.white.SetAlpha(0.9f * alphaMultiplier), borderRadius: 4, borderWidth: 2);
            else if (mouseHover)
                RoundRect(elementRect, Color.white.SetAlpha(0.9f), borderRadius: 4, borderWidth: 1);

            // Edge-resize handle visuals (subtle lines at edges, shown on hover/selection)
            if ((mouseHover || isSelected) && elementRect.width > k_edgeHitWidth * 3)
            {
                const float handleWidth = 2f;
                const float handlePadding = 3f;
                var handleHeight = elementRect.height - 6;
                var leftHandle = new Rect(elementRect.x + handlePadding, elementRect.y + 3, handleWidth, handleHeight);
                var rightHandle = new Rect(elementRect.xMax - handlePadding - handleWidth, elementRect.y + 3, handleWidth, handleHeight);
                RoundRect(leftHandle, Color.white.SetAlpha(0.5f * alphaMultiplier), borderRadius: 1);
                RoundRect(rightHandle, Color.white.SetAlpha(0.5f * alphaMultiplier), borderRadius: 1);
            }

            // Color accent line at bottom
            var colorLine = new Rect(elementRect.x + 1, elementRect.y + elementRect.height - 3, elementRect.width - 2, 2);
            Random.InitState(element.StableId);
            var color = s_elementColors.GetRandom();
            EditorGUI.DrawRect(colorLine, color.SetAlpha(0.6f * alphaMultiplier));

            // Label
            var label = new GUIContent(element.Label);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold, fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white.SetAlpha(alphaMultiplier) }
            };
            var labelWidth = style.CalcSize(label).x;
            var labelRect = elementRect;
            if (labelWidth > labelRect.width)
            {
                label.tooltip = element.Label;
                style.alignment = mouseHover ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                labelRect.xMin += 4f;
            }
            GUI.Label(labelRect, label, style);

            return elementRect;
        }

        #endregion

        #region Toolbar Buttons

        public static bool AddButton(Rect timelineRect)
        {
            var buttonRect = CalculateAddButtonRect(timelineRect);
            var content = EditorGUIUtility.TrIconContent("d_CreateAddNew", "Add element");
            return GUI.Button(buttonRect, content, s_addButtonStyle);
        }

        private static Rect CalculateAddButtonRect(Rect timelineRect)
        {
            var buttonSize = new Vector2(32, 24);
            var position = new Vector2(
                timelineRect.x + (k_bottomHeight - buttonSize.y) / 2,
                timelineRect.y + timelineRect.height - k_bottomHeight + (k_bottomHeight - buttonSize.y) / 2);
            return new Rect(position, buttonSize);
        }

        public static void AddMoreButton(Rect timelineRect, TimelineView.AddMenuItem[] items, Action<TimelineView.AddMenuItem> clicked)
        {
            const float buttonWidth = 20;
            var addButtonRect = CalculateAddButtonRect(timelineRect);
            var buttonRect = addButtonRect.ShiftX(addButtonRect.width).SetWidth(buttonWidth);
            var dropDownIcon = EditorGUIUtility.IconContent("icon dropdown");

            var backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = backgroundColor.SetAlpha(0.55f);
            var result = EditorGUI.DropdownButton(buttonRect, dropDownIcon, FocusType.Passive, s_addMoreButtonStyle);
            GUI.backgroundColor = backgroundColor;

            if (!result) return;

            var menu = new GenericMenu();
            foreach (var item in items)
                menu.AddItem(item.Content, false, userData => clicked?.Invoke((TimelineView.AddMenuItem)userData), item);
            menu.DropDown(addButtonRect.ShiftX(-4));
        }

        public static bool RemoveButton(Rect timelineRect)
        {
            var buttonSize = new Vector2(50, 24);
            var position = new Vector2(
                timelineRect.x + timelineRect.width - buttonSize.x - (k_bottomHeight - buttonSize.y) / 2,
                timelineRect.y + timelineRect.height - k_bottomHeight + (k_bottomHeight - buttonSize.y) / 2);
            return GUI.Button(new Rect(position, buttonSize), "Delete");
        }

        public static bool DuplicateButton(Rect rect)
        {
            var buttonSize = new Vector2(66, 24);
            var position = new Vector2(
                rect.x + rect.width - buttonSize.x - (k_bottomHeight - buttonSize.y) / 2 - 50 - 2,
                rect.y + rect.height - k_bottomHeight + (k_bottomHeight - buttonSize.y) / 2);
            return GUI.Button(new Rect(position, buttonSize), "Duplicate");
        }

        public static bool PlayButton(Rect rect)
        {
            var content = EditorGUIUtility.IconContent("d_PlayButton");
            //var position = rect.position + new Vector2(2, (k_timelineHeaderHeight - s_playButtonSize.y) / 2);
            var position = rect.position + new Vector2(2 + s_toggleSize.x, (k_timelineHeaderHeight - s_playButtonSize.y) / 2);
            var buttonRect = new Rect(position, s_playButtonSize);
            var contentColor = GUI.contentColor;
            GUI.contentColor = Color.cyan;
            var result = GUI.Button(buttonRect, content);
            GUI.contentColor = contentColor;
            return result;
        }

        public static bool StopButton(Rect rect)
        {
            //var position = rect.position + new Vector2(s_playButtonSize.x + 2, (k_timelineHeaderHeight - s_toggleSize.y) / 2);
            var position = rect.position + new Vector2(2 + s_toggleSize.x, (k_timelineHeaderHeight - s_playButtonSize.y) / 2);
            return GUI.Button(new Rect(position, s_playButtonSize), "\u25a0");
        }

        public static bool VisibilityToggle(Rect rect, bool value)
        {
            //var position = rect.position + new Vector2(s_playButtonSize.x + 2, (k_timelineHeaderHeight - s_toggleSize.y) / 2);
            var position = rect.position + new Vector2(2, (k_timelineHeaderHeight - s_toggleSize.y) / 2);
            var toggleRect = new Rect(position, s_toggleSize);
            //var iconType = value ? "d_icon dropdown@2x" : "d_PlayButton";
            var iconSDF = value ? SdfIconType.CaretDownFill : SdfIconType.CaretRightFill;
            //var iconContent = EditorGUIUtility.TrIconContent(iconType, "Hide/Show timeline content");
            var style = new GUIStyle(GUI.skin.label) { padding = new RectOffset(0, 0, 0, 0) };
            return SirenixEditorGUI.SDFIconButton(toggleRect, iconSDF, style) ? !value : value;
            //using var colorScope = new GUIColorScope(background: Color.black.SetAlpha(0), content: s_toggleFadeColor);
            //return GUI.Toggle(toggleRect, value, iconContent, style);
        }

        public static bool LoopToggle(Rect rect, bool value)
        {
            var position = rect.position + new Vector2(rect.width - s_toggleSize.x - 2, (k_timelineHeaderHeight - s_toggleSize.y) / 2);
            var toggleRect = new Rect(position, s_toggleSize);
            var iconContent = EditorGUIUtility.TrIconContent("preAudioLoopOff", "Toggle loop playback");
            var style = new GUIStyle(GUI.skin.button) { padding = new RectOffset(0, 0, 0, 0) };
            using var colorScope = new GUIColorScope(background: s_toggleFadeColor, content: s_toggleFadeColor);
            return GUI.Toggle(toggleRect, value, iconContent, style);
        }

        public static bool SnapToggle(Rect rect, bool value)
        {
            var position = rect.position + new Vector2(
                rect.width - (s_toggleSize.x + 1) * 2 - 2,
                (k_timelineHeaderHeight - s_toggleSize.y) / 2);
            var toggleRect = new Rect(position, s_toggleSize);
            var tooltipText = $"Toggle snapping\n\nHold <b>Ctrl</b> to temporarily {(value ? "disable" : "enable")} snapping.";
            var iconContent = EditorGUIUtility.TrIconContent("SceneViewSnap", tooltipText);
            var style = new GUIStyle(GUI.skin.button) { padding = new RectOffset(0, 0, 0, 0) };
            using var colorScope = new GUIColorScope(background: s_toggleFadeColor, content: s_toggleFadeColor);
            return GUI.Toggle(toggleRect, value, iconContent, style);
        }

        #endregion

        #region Inspector Section

        public static void Inspector(string label, Action drawContent, Action onMoveUp, Action onMoveDown)
        {
            EditorGUILayout.Space();
            
            Splitter(new Color(0.12f, 0.12f, 0.12f, 1.333f));

            var backgroundRect = GUILayoutUtility.GetRect(1f, 20f);
            var labelRect = backgroundRect;
            backgroundRect = ToFullWidth(backgroundRect);
            EditorGUI.DrawRect(backgroundRect, new Color(0.1f, 0.1f, 0.1f, 0.2f));
            EditorGUI.LabelField(labelRect, label, s_inspectorHeaderStyle);
            CreateInspectorButtons(backgroundRect, onMoveUp, onMoveDown);

            Splitter(new Color(0.19f, 0.19f, 0.19f, 1.333f));
            drawContent?.Invoke();
        }

        public static void Inspector(UnityEditor.Editor editor, Action onMoveUp, Action onMoveDown)
        {
            Inspector("Inspector", () => editor.OnInspectorGUI(), onMoveUp, onMoveDown);
        }

        private static void CreateInspectorButtons(Rect backgroundRect, Action onMoveUp, Action onMoveDown)
        {
            const int rightMargin = 6;
            var downButtonRect = new Rect(backgroundRect.xMax - s_inspectorButtonSize.x - rightMargin,
                backgroundRect.y, s_inspectorButtonSize.x, s_inspectorButtonSize.y);
            var upButtonRect = downButtonRect.ShiftX(-s_inspectorButtonSize.x);

            if (GUI.Button(upButtonRect, s_inspectorUpButton, s_inspectorButtonStyle))
                onMoveUp?.Invoke();
            if (GUI.Button(downButtonRect, s_inspectorDownButton, s_inspectorButtonStyle))
                onMoveDown?.Invoke();
        }

        #endregion

        #region Helpers

        private static float CalculateX(Rect rowRect, float time, float timeScale)
        {
            return rowRect.x + time * timeScale * rowRect.width;
        }

        private static void ProcessDragEvents(Rect rect, ref bool isDragging, Action start, Action<Event> end)
        {
            var current = Event.current;
            switch (current.type)
            {
                case EventType.MouseDown when !isDragging && rect.Contains(current.mousePosition):
                    isDragging = true;
                    start?.Invoke();
                    current.Use();
                    break;
                case EventType.MouseUp when isDragging:
                    isDragging = false;
                    end?.Invoke(current);
                    current.Use();
                    break;
            }
        }

        private static void RoundRect(Rect rect, Color color, float borderRadius, float borderWidth = 0)
        {
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, alphaBlend: false,
                imageAspect: 0, color, borderWidth, borderRadius);
        }

        private static void Splitter(Color color)
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f);
            rect = ToFullWidth(rect);
            EditorGUI.DrawRect(rect, color);
        }

        private static Rect ToFullWidth(Rect rect)
        {
            rect.xMin = 0f;
            rect.width += 4f;
            return rect;
        }

        #endregion

        #region Icons

        public static Texture2D ImageFromBase64(string base64)
        {
            var bytes = Convert.FromBase64String(base64);
            var texture = new Texture2D(1, 1);
            texture.LoadImage(bytes);
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private const string k_defaultMarkerIconBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAABQAAAAoCAYAAAD+MdrbAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAD4SURBVHgB7ZWxCoJQFIaPkkuOQVtDLQ02tPQGDrn6CvU+tbeH4NTmCzgLRWM0NGhBuCiBBXZO3eISFXppMLgf/ChX7ufPWQ6ApHIo3HsX08ZoUI4zZodZ84c9x3GmWZYleUmSJNnTXVboiUUfckGoCDqGJFKZsKbrehME0TRNBzYqFX6MFEqhFErhnwsvcRxvQZA0TQ/4OPFnHdu2RyJrAIts8O4YXnYK0cKYvu/Pi4hoj3ieN8M7Fty35VvqmD61jaJo+UkWhuGKtRpAwbV7a+u67oQfA9fKxDSgJPRng9oGQbCgsFYGfGmlFBDTGB4zijBHkEgqzhX38zVoGGkfagAAAABJRU5ErkJggg==";

        #endregion
    }
}
