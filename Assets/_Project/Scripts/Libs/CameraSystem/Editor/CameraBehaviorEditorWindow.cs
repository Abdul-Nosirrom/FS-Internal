using System;
using System.Linq;
using FS.Editor;
using FS.Player;
using PrimeTween;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.CameraSystem.Editor
{
    public class CameraBehaviorEditorWindow : EditorWindow
    {
        private static CameraBehaviorEditorWindow s_instance;
        
        [MenuItem("Free Skies/Camera Behavior Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<CameraBehaviorEditorWindow>();
            window.titleContent = new GUIContent("Camera Behavior Editor");
            window.Show();
        }

        // component is whatever camera controller type we end up with
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawBehaviorGizmos(CameraBehaviorController cameraController, GizmoType gizmoType)
        {
            if (Application.isPlaying)
            {
                s_instance?.m_cameraBehavior?.OnDrawCameraGizmos(cameraController);
            }
            else
            {
                // Is there a way to even draw the gizmos of not in play mode? We can technically create
                // a behavior instance here and draw using that
                // Or we can add behaviors using SerializeReference. In that case then it doesnt really matter and we can
                // draw just fine
            }
        }

        private CameraBehavior m_cameraBehavior;
        private CameraBehaviorController m_cameraController;
        private PlayerCameraSystem m_cameraSystem;

        private OdinMenuTree m_behaviorMenuTree;
        private PropertyTree m_behaviorEditor;

        private void OnEnable()
        {
            EditorApplication.update += Update;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Update;
            m_behaviorEditor?.Dispose();
            m_behaviorEditor = null;
        }

        private void Update() => Repaint();
        
        private bool InitializeEditor()
        {
            if (m_cameraController != null && m_cameraSystem != null) return true;
            
            PlayerManager.Instance.GetPlayer(0, out var playerData);
            if (playerData != null)
            {
                m_cameraSystem = PlayerManager.GetSystem<PlayerCameraSystem>(0);
                m_cameraController = playerData.Instance.GetComponentInChildren<CameraBehaviorController>();
                if (m_cameraController != null) InitializeBehaviorMenuTree();
            }
            
            return m_cameraSystem != null && m_cameraController != null;
        }
        
        private void InitializeBehaviorMenuTree()
        {
            m_behaviorMenuTree = new OdinMenuTree();
            m_behaviorMenuTree.Config.DrawSearchToolbar = false;
            m_behaviorMenuTree.Selection.SupportsMultiSelect = false;

            int priorityIdx = 0;
            foreach (var behavior in m_cameraController.m_cameraBehaviors)
            {
                string behaviorName = behavior.Name;
                string typeName = behavior.BehaviorType.ToString();
                var priority = priorityIdx;//behavior.Priority;
                    
                string menuName = $"{typeName}/[{priority}] {behaviorName}";
                    
                m_behaviorMenuTree.Add(menuName, behavior).ForEach(item => item.OnDrawItem += DrawBehaviorMenuEntry);

                priorityIdx++;
            }

            m_behaviorMenuTree.Selection.SelectionChanged += (selectionType) =>
            {
                m_cameraBehavior = m_behaviorMenuTree.Selection.SelectedValue as CameraBehavior;
                if (m_cameraBehavior == null) return;
                m_behaviorEditor?.Dispose();
                m_behaviorEditor = PropertyTree.Create(m_cameraBehavior);
            };
        }

        private void OnGUI()
        {
            using (var mainVerticalLayout = new EditorGUILayout.VerticalScope())
            {
                var logoRect = FreeSkiesEditor.DrawLogo(200f, SirenixGUIStyles.BoxContainer);

                if (!Application.isPlaying)
                {
                    GUILayout.Label("Game is not running");
                    return;
                }

                if (!InitializeEditor())
                {
                    GUILayout.Label("No player cameras found");
                    return;
                }
                
                // DrawPlayerSelectionDropdown(); For now we work with 1 player

                DrawCameraShakeDisplay();
                
                DrawCameraStateDisplay();

                using (var horizPrimaryLayout = new EditorGUILayout.HorizontalScope())
                {
                    DrawBehaviorSelector();
                    
                    DrawBehaviorInfo();
                }
            }
        }

        private Vector2 m_worldShakesScrollbar;
        private Vector2 m_localShakesScrollbar;
        private bool m_shakesFoldout = false;
        /// <summary>
        /// 2 Column layout displaying world-camera shakes and their current attenuation & local camera shakes
        /// </summary>
        private void DrawCameraShakeDisplay()
        {
            if (m_cameraSystem == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("No Valid Camera System", GUIStyles.GUIStyles.WhiteLargeCenterLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            m_shakesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(m_shakesFoldout, "Camera Shakes");
            if (m_shakesFoldout)
            {
                using var mainHoriz = new EditorGUILayout.HorizontalScope(GUIStyles.GUIStyles.HelpBox);
                
                using (var worldShakesVertical =
                       new EditorGUILayout.ScrollViewScope(m_worldShakesScrollbar, GUIStyles.GUIStyles.HelpBox,
                           GUILayout.Width(0.48f * position.width)))
                {
                    m_worldShakesScrollbar = worldShakesVertical.scrollPosition;
                    GUILayout.Label("World Shakes", GUIStyles.GUIStyles.LargeBoldLabel);
                    SirenixEditorGUI.HorizontalLineSeparator();

                    foreach (var worldShake in WorldCameraShakeManager.Instance.WorldCameraShakes)
                    {
                        EditorGUILayout.Slider($"{worldShake.gameObject.name}",
                            worldShake.GetShakeAttenuation(Camera.main.transform.position), 0, 1);
                    }
                }

                using (var localShakesVertical = new EditorGUILayout.ScrollViewScope(m_localShakesScrollbar,
                           GUIStyles.GUIStyles.HelpBox,
                           GUILayout.Width(0.5f * position.width)))
                {
                    m_localShakesScrollbar = localShakesVertical.scrollPosition;
                    GUILayout.Label("Local Shakes", GUIStyles.GUIStyles.LargeBoldLabel);
                    SirenixEditorGUI.HorizontalLineSeparator();

                    foreach (var localShake in m_cameraSystem.ActiveShakes)
                    {
                        GUILayout.Button(localShake.ToString());
                    }
                }
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        /// <summary>
        /// Displaying camera system state in a 2-column layout.
        /// - Column 1: Active virtual camera, if any, and blend info related to it. Provides addition debug utility
        /// like seeing if any selected gameobject has a virtual cam and can force to blend to it. Also to blend out
        /// of the current active virtual cameracamera.
        /// - Column 2: Display of the current camera orbit vector (TODO: also Camera Shake info, but thats not setup yet)
        /// </summary>
        private void DrawCameraStateDisplay()
        {
            if (m_cameraSystem == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("No Valid Camera System", GUIStyles.GUIStyles.WhiteLargeCenterLabel);
                GUILayout.FlexibleSpace();
                return;
            }
            
            // Two column layout [Active Camera | Current Camera Orbit Vector]
            using var mainHorizLayout = new EditorGUILayout.HorizontalScope(GUIStyles.GUIStyles.HelpBox);

            using (var virtualCamColumn =
                   new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox,
                       GUILayout.Width(0.5f * position.width)))
            {
                GUILayout.Label("Active Camera", GUIStyles.GUIStyles.LargeBoldLabel);
                SirenixEditorGUI.HorizontalLineSeparator();
                
                VirtualCamera virtualCamera = m_cameraSystem.VirtualCameraBlendTarget;
                string activeCameraName = virtualCamera == null ? "Player Camera" : virtualCamera.gameObject.name;

                EditorGUILayout.LabelField("Active Virtual Camera:", activeCameraName, GUIStyles.GUIStyles.LargeBoldLabel);

                float blendWeight =
                    m_cameraSystem.CameraSwitchBlendParams.Evaluate(m_cameraSystem.CameraSwitchBlendProgress);

                GUILayout.Label($"Blend Weight: {blendWeight:0.00}", GUIStyles.GUIStyles.WhiteLargeCenterLabel);
                Rect blendWeightRect = GUILayoutUtility.GetLastRect();
                {
                    DrawCameraBlendRect(blendWeightRect, blendWeight);
                    GUI.Label(blendWeightRect, $"Blend Weight: {blendWeight:0.00}", GUIStyles.GUIStyles.WhiteLargeCenterLabel);
                }

                if (virtualCamera != null)
                {
                    if (GUILayout.Button($"Blend Out"))
                    {
                        m_cameraSystem.BlendBackToPlayer(CameraBlendParams.Create(0.5f, Ease.OutQuad, false));
                    }
                }
                
                var selectedObject = Selection.activeGameObject?.GetComponentInChildren<VirtualCamera>();
                if (selectedObject != null)
                {
                    // Option to blend into it
                    if (GUILayout.Button($"Blend Into Selection {selectedObject.gameObject.name}"))
                    {
                        m_cameraSystem.BlendToCamera(selectedObject, CameraBlendParams.Create(0.5f, Ease.OutQuad, false));
                    }
                }
            }

            using (var orbitCamColumn =
                   new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox,
                       GUILayout.Width(0.48f * position.width)))
            {
                GUILayout.Label("Player Orbit Camera", GUIStyles.GUIStyles.LargeBoldLabel);
                SirenixEditorGUI.HorizontalLineSeparator();

                var orbitVector = m_cameraController.m_cameraVector;
                EditorGUILayout.Vector3Field("Pivot", orbitVector.Pivot);
                EditorGUILayout.FloatField("Distance", orbitVector.Distance);
                EditorGUILayout.Slider("FOV", orbitVector.FOV, 0, 140);

                using (var rotationSlider = new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Rotation");
                    var rotation = orbitVector.Rotation;
                    EditorGUILayout.Slider(rotation.pitch, -180, 180);
                    EditorGUILayout.Slider(rotation.yaw + 180, 0, 360);
                    //EditorGUILayout.Slider(rotation.z > 180 ? rotation.z - 360 : rotation.z, -180, 180);
                }
                
                EditorGUILayout.LabelField(m_cameraController.LookInputDelta.ToString(), GUIStyles.GUIStyles.WhiteLargeLabel);
                EditorGUILayout.LabelField(m_cameraController.m_cameraVector.Rotation.ToString(), GUIStyles.GUIStyles.WhiteLargeLabel);

                EditorGUILayout.Vector2Field("View Offset", orbitVector.ViewOffset);
                
                using (var rotationOffsetSlider = new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Rotation Offset");
                    var rotation = orbitVector.RotationOffset;
                    EditorGUILayout.Slider(rotation.pitch, -180, 180);
                    EditorGUILayout.Slider(rotation.yaw + 180, 0, 360);
                    //EditorGUILayout.Slider(rotation.z > 180 ? rotation.z - 360 : rotation.z, -180, 180);
                }
            }
        }
        
        private void DrawBehaviorSelector()
        {
            using var selectionLayout = new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox, GUILayout.Width(0.3f * position.width), GUILayout.ExpandHeight(true));
            GUILayout.Label("Camera Behaviors", GUIStyles.GUIStyles.LargeBoldLabel);
            SirenixEditorGUI.HorizontalLineSeparator();
            m_behaviorMenuTree?.DrawMenuTree();
        }

        /// <summary>
        /// Odin Menu Tree callback, draws an additional rect over the MenuItem to represent behaviors blend state
        /// </summary>
        private void DrawBehaviorMenuEntry(OdinMenuItem behaviorEntry)
        {
            // Draw a 'progress bar' like border representing the blend-state of this behavior
            // Red:     xmin = lerp(xmin, xmax, (1 - alpha))
            // Green:   xmax = lerp(xmin, xmax, alpha)
            var behavior = behaviorEntry.Value as CameraBehavior;
            if (behavior == null) return; // Could just be foldout category

            Rect itemRect = behaviorEntry.LabelRect;
            DrawCameraBlendRect(itemRect, behavior.GetBlendAlpha());
            
            // Draw the behavior name (make it show up in front of the bar)
            EditorGUI.LabelField(behaviorEntry.LabelRect, behaviorEntry.Name);
        }
        
        /// <summary>
        /// Display info related to the selected CameraBehavior if any is displayed. While also displaying an editor
        /// for it to allow for customization in runtime
        /// </summary>
        private void DrawBehaviorInfo()
        {
            using var mainVerticalLayout = new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox, GUILayout.Width(0.68f * position.width));

            if (m_cameraBehavior == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("No Camera Behavior Selected", GUIStyles.GUIStyles.WhiteLargeCenterLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            var ogColor = GUI.backgroundColor;
            GUI.backgroundColor = m_cameraBehavior.IsActive ? FreeSkiesEditor.ToggleOnColor : FreeSkiesEditor.ToggleOffColor;
            EditorGUILayout.LabelField("Selected Behavior:", m_cameraBehavior.Name, GUIStyles.GUIStyles.LargeBoldLabel);
            GUI.backgroundColor = ogColor;
            //EditorGUI.DrawRect(GUILayoutUtility.GetLastRect(), m_cameraBehavior.IsActive ? FreeSkiesEditor.ToggleOnColor : FreeSkiesEditor.ToggleOffColor);
            if (m_cameraBehavior.IsActive) 
                EditorGUILayout.Slider("Weight", m_cameraBehavior.GetBlendAlpha(), 0, 1);
            
            SirenixEditorGUI.HorizontalLineSeparator();
            
            EditorGUILayout.LabelField("Inherited Elements:", GUIStyles.GUIStyles.LargeBoldLabel);
            using (var horizLayout = new EditorGUILayout.HorizontalScope())
            {
                FreeSkiesEditor.ToggleLabel("Pivot", m_cameraBehavior.IsInheritingCameraValue(CameraBehaviorInheritance.Pivot));
                FreeSkiesEditor.ToggleLabel("Distance", m_cameraBehavior.IsInheritingCameraValue(CameraBehaviorInheritance.Distance));
                FreeSkiesEditor.ToggleLabel("FOV", m_cameraBehavior.IsInheritingCameraValue(CameraBehaviorInheritance.FOV));
                FreeSkiesEditor.ToggleLabel("Rotation", m_cameraBehavior.IsInheritingCameraValue(CameraBehaviorInheritance.Rotation));
            }
            
            SirenixEditorGUI.HorizontalLineSeparator();
            m_behaviorEditor?.Draw(false);
        }

        private void DrawCameraBlendRect(Rect rect, float weight)
        {
            // For behaviors, when blending out we go from [1-0] & when blending in we go from [0-1]
            // For camera switched, theres no seperate blendout/in alphas just weight of current camera so [0-1]
            var invalidRect = rect;
            invalidRect.xMin = Mathf.Lerp(invalidRect.xMin, invalidRect.xMax, weight);
            var validRect = rect;
            validRect.xMax = Mathf.Lerp(validRect.xMin, validRect.xMax, weight);

            Color onColor = new Color(0f, 1f, 0f, 0.5f);
            Color offColor = new Color(1f, 0f, 0f, 0.5f);
            
            EditorGUI.DrawRect(invalidRect, offColor);
            EditorGUI.DrawRect(validRect, onColor);
        }
    }
}