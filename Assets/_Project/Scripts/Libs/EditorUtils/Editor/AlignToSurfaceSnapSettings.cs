using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace FS.Editor
{
    [FilePath("Library/AlignToSurfaceSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class AlignToSurfaceSettingsData : ScriptableSingleton<AlignToSurfaceSettingsData>
    {
        [SerializeField]
        internal bool m_alignToSurfaceEnabled;
        
        [SerializeField]
        internal bool m_alignRotation;
        
        [SerializeField]
        internal bool m_preserveYRotation;
        
        [SerializeField]
        internal LayerMask m_surfaceLayerMask;

        [SerializeField] 
        internal float m_normalOffset;

        [SerializeField] 
        internal float m_maxDistance;

        [SerializeField] 
        internal bool m_showPreview;

        private void OnDisable()
        {
            Save();
        }
        
        internal void Save() => Save(true);
    }

    public static class AlignToSurfaceSettings
    {
        internal static AlignToSurfaceSettingsData Instance => AlignToSurfaceSettingsData.instance;
        
        public static Action OnAlignToSurfaceEnabledChanged;
        
        public static bool AlignToSurfaceEnabled 
        {
            get => Instance.m_alignToSurfaceEnabled;
            set
            {
                if (Instance.m_alignToSurfaceEnabled != value)
                {
                    Instance.m_alignToSurfaceEnabled = value;
                    OnAlignToSurfaceEnabledChanged?.Invoke();
                }
            }
        }

        internal static void Save() => Instance.Save();
    }
    
    [InitializeOnLoad]
    public static class AlignToSurfaceEditor
    {
        static AlignToSurfaceEditor()
        {
            // Use delayCall for everything that accesses the instance
            EditorApplication.delayCall += () =>
            {
                InjectToolbarButton();
            
                // Subscribe to events AFTER initialization is complete
                EditorApplication.playModeStateChanged += (state) =>
                {
                    AlignToSurfaceSettings.Save();
                    InjectToolbarButton();
                };
                AssemblyReloadEvents.beforeAssemblyReload += AlignToSurfaceSettings.Save;
            };
        
            // This one is fine since it doesn't access Instance immediately
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;
            
            // Keyboard shortcut toggle
            if (e.type == EventType.KeyDown && e.shift && e.keyCode == KeyCode.X)
            {
                AlignToSurfaceSettings.AlignToSurfaceEnabled = !AlignToSurfaceSettings.AlignToSurfaceEnabled;
                e.Use();
                return;
            }
            
            if (!AlignToSurfaceSettings.AlignToSurfaceEnabled) return;
   
            // Only work with active selection
            if (Selection.activeTransform == null) return;
            
            // Only work when move tool is active
            if (Tools.current != Tool.Move && Tools.current != Tool.Transform) return;
            
            HandleSurfaceSnapping(sceneView, e);
        }
        
        private static bool m_isDragging = false;
        private static Vector2 m_lastMousePosition;
        
        private static RaycastHit[] m_hits = new RaycastHit[8];
        private static void HandleSurfaceSnapping(SceneView sceneView, Event e)
        {
            // Get ray from mouse position
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            
            int numHits = Physics.RaycastNonAlloc(
                ray, m_hits,
                AlignToSurfaceSettings.Instance.m_maxDistance, 
                AlignToSurfaceSettings.Instance.m_surfaceLayerMask);

            bool didHit = false;
            RaycastHit hit = default;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < numHits; i++)
            {
                // Ensure the hit is not one of the selected objects (or its parents in case we're moving the parent but collider is on child
                if (Selection.transforms.Any(t =>
                        m_hits[i].collider != null && 
                        (t.gameObject == m_hits[i].collider.gameObject || 
                         m_hits[i].collider.transform.IsChildOf(t))))
                    continue;
                
                // Get the closest hit
                float distance = Vector3.Distance(ray.origin, m_hits[i].point);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    hit = m_hits[i];
                    didHit = true;
                }
            }

            if (!didHit) return;

            // PREVIEW - Show where we'll snap to
            if (AlignToSurfaceSettings.Instance.m_showPreview)
            {
                DrawSnapPreview(hit, didHit);
                if (didHit)
                    sceneView.Repaint();
            }

            // INTERACTION HANDLING
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    HandleMouseDown(e);
                    break;
                    
                case EventType.MouseDrag when e.button == 0:
                    HandleMouseDrag(e, hit, didHit, sceneView);
                    break;
                    
                case EventType.MouseUp when e.button == 0:
                    HandleMouseUp(e);
                    break;
            }

            // Status overlay
            if (AlignToSurfaceSettings.AlignToSurfaceEnabled && AlignToSurfaceSettings.Instance.m_showPreview)
            {
                DrawStatusOverlay(sceneView, hit, didHit);
            }
        }

        private static void HandleMouseDown(Event e)
        {
            m_isDragging = false;
            m_lastMousePosition = e.mousePosition;
        }

        private static void HandleMouseDrag(Event e, RaycastHit hit, bool didHit, SceneView sceneView)
        {
            if (!didHit)
                return;

            // Check if we've actually moved the mouse (to differentiate from clicks)
            if (!m_isDragging)
            {
                float mouseDelta = Vector2.Distance(m_lastMousePosition, e.mousePosition);
                if (mouseDelta > 2f) // Small threshold to prevent accidental drags
                {
                    m_isDragging = true;
                }
                else
                {
                    return;
                }
            }

            // Actually perform the snap for all selected objects
            foreach (Transform selected in Selection.transforms)
            {
                Undo.RecordObject(selected, "Surface Snap");
                
                // Calculate target position with normal offset
                Vector3 targetPos = hit.point + hit.normal * AlignToSurfaceSettings.Instance.m_normalOffset;
                selected.position = targetPos;
                
                // Apply rotation if enabled
                if (AlignToSurfaceSettings.Instance.m_alignRotation)
                {
                    Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    
                    if (AlignToSurfaceSettings.Instance.m_preserveYRotation)
                    {
                        // Preserve Y rotation while aligning to surface
                        Quaternion currentYRotation = Quaternion.Euler(0, selected.eulerAngles.y, 0);
                        selected.rotation = surfaceRotation * currentYRotation;
                    }
                    else
                    {
                        // Full alignment to surface
                        selected.rotation = surfaceRotation;
                    }
                }
            }
            
            // Consume the event so Unity's built-in handles don't interfere
            e.Use();
            sceneView.Repaint();
        }

        private static void HandleMouseUp(Event e)
        {
            m_isDragging = false;
        }

        private static void DrawSnapPreview(RaycastHit hit, bool didHit)
        {
            if (!didHit)
                return;

            // Draw target surface indicator (disc)
            Handles.color = new Color(0.3f, 1f, 0.3f, 0.3f);
            Handles.DrawSolidDisc(hit.point, hit.normal, 0.5f);
            
            // Draw surface normal arrow
            Handles.color = Color.green;
            Vector3 normalEnd = hit.point + hit.normal * 2f;
            Handles.DrawLine(hit.point, normalEnd, 3f);
            
            // Draw arrow cap
            float arrowSize = 0.3f;
            Vector3 right = Vector3.Cross(hit.normal, Vector3.up);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(hit.normal, Vector3.forward);
            right = right.normalized * arrowSize;
            
            Vector3 arrowBase = normalEnd - hit.normal * arrowSize;
            Handles.DrawLine(normalEnd, arrowBase + right);
            Handles.DrawLine(normalEnd, arrowBase - right);
            
            // If we have an offset, show the actual placement position
            if (!Mathf.Approximately(AlignToSurfaceSettings.Instance.m_normalOffset, 0f))
            {
                Vector3 offsetPos = hit.point + hit.normal * AlignToSurfaceSettings.Instance.m_normalOffset;
                Handles.color = new Color(1f, 0.5f, 0f, 0.5f);
                Handles.DrawWireCube(offsetPos, Vector3.one * 0.5f);
                Handles.DrawDottedLine(hit.point, offsetPos, 2f);
            }
            
            // Draw connection line from selected object to hit point
            if (Selection.activeTransform != null)
            {
                float distance = Vector3.Distance(Selection.activeTransform.position, hit.point);
                Handles.color = new Color(1f, 1f, 0f, 0.4f);
                Handles.DrawDottedLine(Selection.activeTransform.position, hit.point, 4f);
                
                // Distance label
                Vector3 midPoint = (Selection.activeTransform.position + hit.point) * 0.5f;
                GUIStyle labelStyle = new GUIStyle(EditorStyles.whiteBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.yellow }
                };
                Handles.Label(midPoint, $"{distance:F2}m", labelStyle);
            }
        }

        private static void DrawStatusOverlay(SceneView sceneView, RaycastHit hit, bool didHit)
        {
            Handles.BeginGUI();
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.3f, 1f, 0.3f) },
                fontSize = 13,
                padding = new RectOffset(5, 5, 5, 5)
            };
            
            GUIStyle infoStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.white },
                padding = new RectOffset(5, 5, 2, 2)
            };
            
            float areaHeight = didHit ? 140f : 90f;
            Rect bgRect = new Rect(25f, 25f, 380, areaHeight);
            
            // Semi-transparent background
            EditorGUI.DrawRect(bgRect, new Color(0f, 0f, 0f, 0.7f));
            
            GUILayout.BeginArea(bgRect);
            
            GUILayout.Label("Surface Snap: ACTIVE", titleStyle);
            GUILayout.Label("Shift+X to toggle | Drag objects to snap", infoStyle);
            
            if (didHit)
            {
                GUILayout.Space(5);
                GUILayout.Label($"Target: {hit.collider.gameObject.name}", infoStyle);
                GUILayout.Label($"Distance: {hit.distance:F2}m", infoStyle);
                
                if (AlignToSurfaceSettings.Instance.m_alignRotation)
                {
                    string rotMode = AlignToSurfaceSettings.Instance.m_preserveYRotation ? "Align (Y preserved)" : "Align (full)";
                    GUILayout.Label($"Rotation: {rotMode}", infoStyle);
                }
            }
            else
            {
                GUILayout.Space(5);
                GUILayout.Label("No surface detected within range", infoStyle);
                GUILayout.Label($"Max distance: {AlignToSurfaceSettings.Instance.m_maxDistance:F1}m", infoStyle);
            }
            
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static void InjectToolbarButton()
        { 
            try
            {
                // Find the GridAndSnapToolBar type
                var gridSnapToolbarType = typeof(UnityEditor.Editor).Assembly.GetTypes()
                    .FirstOrDefault(t => t.Name == "GridAndSnapToolBar");
                if (gridSnapToolbarType == null)
                {
                    Debug.LogWarning("Could not find GridAndSnapToolBar type");
                    return;
                }

                // Get all overlays in scene views
                var sceneViews = Resources.FindObjectsOfTypeAll<SceneView>();
                foreach (var sceneView in sceneViews)
                {
                    // Get the overlay canvas
                    var overlayCanvas = sceneView.overlayCanvas;
                    if (overlayCanvas == null) continue;

                    // Find the Grid and Snap overlay instance
                    var overlays = overlayCanvas.overlays;
                    var gridSnapOverlay = overlays.FirstOrDefault(o => o.GetType() == gridSnapToolbarType);
                    
                    if (gridSnapOverlay == null) continue;

                    // Get the private m_ToolbarElementIds field
                    var toolbarElementIdsField = typeof(ToolbarOverlay).GetField(
                        "m_ToolbarElementIds", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (toolbarElementIdsField == null)
                    {
                        Debug.LogWarning("Could not find m_ToolbarElementIds field");
                        continue;
                    }

                    // Get current toolbar element IDs
                    var currentIds = toolbarElementIdsField.GetValue(gridSnapOverlay) as string[];
                    if (currentIds == null) continue;

                    // Check if our elements are already added (in case of domain reload)
                    if (currentIds.Contains(AlignToSurfaceToggle.k_ToolbarElementID))
                        continue;

                    // Create new array with our elements added
                    var newIds = new List<string>(currentIds)
                    {
                        AlignToSurfaceToggle.k_ToolbarElementID,
                    };

                    // Set the new array
                    toolbarElementIdsField.SetValue(gridSnapOverlay, newIds.ToArray());

                    // Rebuild the overlay to show new elements
                    var rebuildMethod = typeof(Overlay).GetMethod(
                        "RebuildContent", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (rebuildMethod != null)
                    {
                        rebuildMethod.Invoke(gridSnapOverlay, null);
                    }
                    else
                    {
                        // Alternative: try to force a repaint
                        sceneView.Repaint();
                    }
                }

                Debug.Log("Successfully injected Surface Snap into Grid and Snap toolbar");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to inject Surface Snap toolbar elements: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    [EditorToolbarElement(AlignToSurfaceToggle.k_ToolbarElementID, typeof(SceneView))]
    class AlignToSurfaceToggle : EditorToolbarDropdownToggle
    {
        public const string k_ToolbarElementID = "Tools/Align To Surface";

        private bool m_enabled = false;

        public AlignToSurfaceToggle()
        {
            name = "Align To Surface";
            tooltip =
                "Toggle aligning to surface on and off. Available when you set the tool handle rotation to global";
            icon = Resources.Load<Texture2D>("icon_alignToSurfaceIcon");
            UpdateAlignToSurfaceEnableValue();
            
            this.RegisterValueChangedCallback(OnAlignToSurfaceValueChanged);

            RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }
        
        private void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            AlignToSurfaceSettings.OnAlignToSurfaceEnabledChanged += UpdateAlignToSurfaceEnableValue;
            dropdownClicked += OnDropdownClicked;
        }
        
        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AlignToSurfaceSettings.OnAlignToSurfaceEnabledChanged -= UpdateAlignToSurfaceEnableValue;
            dropdownClicked -= OnDropdownClicked;
        }

        private void OnDropdownClicked()
        {
            UnityEditor.PopupWindow.Show(worldBound, new AlignToSurfaceSettingsWindow());
        }
        
        private void OnAlignToSurfaceValueChanged(ChangeEvent<bool> evt)
        {
            AlignToSurfaceSettings.AlignToSurfaceEnabled = !AlignToSurfaceSettings.AlignToSurfaceEnabled;
        }

        private void UpdateAlignToSurfaceEnableValue()
        {
            SetValueWithoutNotify(AlignToSurfaceSettings.AlignToSurfaceEnabled);
        }

        private class AlignToSurfaceSettingsWindow : PopupWindowContent
        {
            public override Vector2 GetWindowSize() => new(280f, 226f);

            public override void OnGUI(Rect rect)
            {
                GUILayout.BeginArea(rect);
                
                GUILayout.Label("Align To Surface Settings (Shift + X)", EditorStyles.whiteBoldLabel);
                GUILayout.Space(10f);

                AlignToSurfaceSettings.Instance.m_alignRotation = GUILayout.Toggle(AlignToSurfaceSettings.Instance.m_alignRotation, "Align Rotation");
                
                GUI.enabled = AlignToSurfaceSettings.Instance.m_alignRotation;
                AlignToSurfaceSettings.Instance.m_preserveYRotation = GUILayout.Toggle(AlignToSurfaceSettings.Instance.m_preserveYRotation, "Preserve Y Rotation");
                GUI.enabled = true;
                
                LayerMask tempMask = EditorGUILayout.MaskField( InternalEditorUtility.LayerMaskToConcatenatedLayersMask(AlignToSurfaceSettings.Instance.m_surfaceLayerMask), InternalEditorUtility.layers);
                AlignToSurfaceSettings.Instance.m_surfaceLayerMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
                
                AlignToSurfaceSettings.Instance.m_normalOffset = EditorGUILayout.Slider("Normal Offset", 
                    AlignToSurfaceSettings.Instance.m_normalOffset, -5f, 5f);
                
                AlignToSurfaceSettings.Instance.m_maxDistance = EditorGUILayout.Slider("Max Distance", 
                    AlignToSurfaceSettings.Instance.m_maxDistance, 1f, 100f);
                
                AlignToSurfaceSettings.Instance.m_showPreview = GUILayout.Toggle(AlignToSurfaceSettings.Instance.m_showPreview, "Show Preview");
                
                GUILayout.EndArea();
            }
        }
    }

}