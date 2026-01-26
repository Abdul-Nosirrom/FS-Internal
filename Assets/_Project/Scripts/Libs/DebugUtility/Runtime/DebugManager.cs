using System;
using System.Collections.Generic;
using Drawing;
using FS.GameplayActions;
using FS.Math;
using FS.Patterns;
using FS.Player;
using FS.Rendering;
using Tayx.Graphy;
using TimeUtils;
using UnityEngine;
using UnityEngine.Assertions;

namespace FS.RuntimeDebug
{
    [DefaultExecutionOrder(100)] // Make it run after the camera system so that the free cam overrides it
    public class DebugManager : PersistentSingleton<DebugManager>//, IDrawGizmos
    {
        private class DebugProviderState : IEquatable<IDebugProvider>
        {
            public GameObject Context;
            public IDebugProvider Provider;

            public Rect WindowRect;
            public bool IsDrawEnabled;
            public bool IsWindowOpen;
            
            public int ID => Provider.GetHashCode();

            public string Title;
            
            public static DebugProviderState Create(GameObject context, IDebugProvider provider) =>
                new ()
                {
                    Context = context,
                    Provider = provider,
                    WindowRect = new Rect(0, 0, 300, 400),
                    Title = $"{provider.DebugName} - {context.name}",
                };

            public bool Equals(IDebugProvider other) => Provider == other;
            public override bool Equals(object obj) => obj is IDebugProvider provider && Equals(provider);
            public override int GetHashCode() => Provider?.GetHashCode() ?? -1;
            
            public void GUIDraw()
            {
                if (IsWindowOpen)
                    WindowRect = GUILayout.Window(ID, WindowRect, DrawWindow, Title);
            }
            
            private bool m_isDragging = false;
            private void DrawWindow(int windowID)
            {
                if (Provider != null)
                {
                    if (GUIWindowUtility.CloseWindowButton(WindowRect)) IsWindowOpen = false;
                    Provider.OnDebugGUI();
                }
                
                WindowRect = GUIWindowUtility.ResizeRect(WindowRect, 240, 64, ref m_isDragging);
            }
        }


        /// <summary>
        /// The current gameobject being inspected
        /// </summary>
        private GameObject m_debugContext;
        public bool HasDebugContext => m_debugContext;
        public bool IsDebugWindowOpen = false;
        private bool m_worldContextSelectionOpen = false;

        private bool m_debugCamEnabled = false;
        
        private bool m_debugNoClipEnabled = false;
        private PhysicsController m_debugNoClipController;
        private ActionController m_debugNoClipActionController;

        public bool IsDebugDrawEnabledForProvider(IDebugProvider provider)
        {
            if (!HasDebugContext) return false;
            if (!m_debugProviders.TryGetValue(m_debugContext, out var providers)) return false;
            return providers.Exists(p => p.Equals(provider) && p.IsDrawEnabled);
        }

        private class DebugNoClipActionConstraint : ActionConstraintBase
        {
            public override string Name => "Debug No Clip Action Constraint";

            protected override void OnEnable()
            {
                foreach (var action in m_controller.IterateActiveActions<GameplayAction>()) action.TryEndAction();
            }
            public override bool EvaluateConstraint(GameplayAction action) => false;
        }

        private DebugNoClipActionConstraint m_debugNoClipActionConstraint =
            ActionConstraintBase.Create<DebugNoClipActionConstraint>();
        
        public bool DebugNoClipEnabled
        {
            get => m_debugNoClipEnabled;
            set
            {
                if (value == m_debugNoClipEnabled) return;
                
                if (value && PlayerManager.Instance.GetPlayer(0, out var pd))
                {
                    m_debugNoClipController = pd.Instance.GetComponentInChildren<PhysicsController>();
                    m_debugNoClipActionController = pd.Instance.GetComponentInChildren<ActionController>();

                    m_debugNoClipActionController.ConstraintHandler.AddConstraint(m_debugNoClipActionConstraint, true);
                    m_debugNoClipController.ShouldBlockLevelObjects = true;

                    DebugFreeCamEnabled = true;
                }
                else if (value)
                {
                    Debug.LogWarning("No player found for No Clip mode.");
                }
                else
                {
                    m_debugNoClipActionController.ConstraintHandler.RemoveConstraint(m_debugNoClipActionConstraint);
                    m_debugNoClipController.ShouldBlockLevelObjects = false;
                    m_debugNoClipController = null;
                    m_debugNoClipActionController = null;
                }
                
                m_debugNoClipEnabled = value;
            }
        }

        public bool DebugFreeCamEnabled
        {
            get => m_debugCamEnabled;
            set
            {
                if (value && !m_debugCamEnabled)
                {
                    m_debugCameraPos = Camera.main.transform.position;
                    m_debugCameraRot = Camera.main.transform.rotation;
                }

                if (!value && DebugNoClipEnabled) DebugNoClipEnabled = false; 
                
                m_debugCamEnabled = value;
            }
        }

        private bool m_debugPause;

        public bool DebugPause
        {
            get => m_debugPause;
            set
            {
                if (value && !m_debugPause) DebugFreeCamEnabled = true; // Auto-enable free cam when pausing
                m_debugPause = value;
                Time.timeScale = m_debugPause ? 0f : 1f;
            }
        }
        
        private Vector3 m_debugCameraPos;
        private Quaternion m_debugCameraRot;
        private Camera m_debugCamera;
        private bool m_isIteratingToNextFrame = false;

        private bool m_debugGUIEnabled = false;

        public bool DebugGUIEnabled
        {
            get => m_debugGUIEnabled;
            set
            {
                m_debugGUIEnabled = value;
                m_performanceGraph.gameObject.SetActive(m_debugGUIEnabled);
                if (!m_debugGUIEnabled)
                {
                    IsDebugWindowOpen = false;
                    m_worldContextSelectionOpen = false;
                }
            }
        }

        private Dictionary<GameObject, List<DebugProviderState>> m_debugProviders = new();

        protected override void InitializeSingleton()
        {
            base.InitializeSingleton();
            
            // Spawn our debug camera
            var debugCamObject = new GameObject("Debug Camera", typeof(Camera));
            // Set parent to this
            debugCamObject.transform.SetParent(transform);
            m_debugCamera = debugCamObject.GetComponent<Camera>();
            
            // Disable
            m_debugCamera.enabled = false;
        }

        public void RegisterDebugProvider(GameObject context, IDebugProvider provider)
        {
            if (!m_debugProviders.TryGetValue(context, out var providers))
            {
                providers = new List<DebugProviderState>();
                m_debugProviders[context] = providers;
            }
            
            Assert.IsFalse(providers.Exists(p => p.Equals(provider)), 
                $"Debug provider {provider} is already registered for target {context.name}");

            providers.Add(DebugProviderState.Create(context, provider));
        }
        
        public void UnregisterDebugProvider(GameObject context)
        {
            // We'll assume unregistering is for the context not an individual provider (for now)
            if (!m_debugProviders.Remove(context))
            {
                Debug.LogWarning($"No debug providers registered for context {context.name}");
            }
        }

        private GraphyManager m_performanceGraph;
        private Rect m_debugManagerRect = new Rect(0, Screen.height - 430, 350, 400);
        private Vector2 m_debugWindowScrollPosition;

        protected override async void Awake()
        {
            base.Awake();

            //DrawingManager.Register(this);

            var fpsCounter = Resources.Load<GameObject>("[DEBUG] Performance Analysis");
            if (fpsCounter != null)
            {
                var fpsInstance = Instantiate(fpsCounter, transform);
                fpsInstance.name = "Graphy FPS Counter";

                await Awaitable.NextFrameAsync();

                m_performanceGraph = fpsInstance.GetComponent<GraphyManager>();
                m_performanceGraph.SetModuleMode(GraphyManager.ModuleType.AUDIO, GraphyManager.ModuleState.OFF);
                m_performanceGraph.SetModuleMode(GraphyManager.ModuleType.ADVANCED, GraphyManager.ModuleState.OFF);
            }
            else
            {
                Debug.LogWarning("Graphy FPS Counter prefab not found. FPS display may not work.");
            }
        }

        private void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) DebugGUIEnabled = !DebugGUIEnabled;
            
            // Hotkeys to enable mouse control
            if (Input.GetKeyDown(KeyCode.F1))
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                    ? CursorLockMode.None
                    : CursorLockMode.Locked;
                Cursor.visible = Cursor.lockState == CursorLockMode.None;
                m_worldContextSelectionOpen = !m_worldContextSelectionOpen;
            }
            
            // Enable Debug Free Cam
            if (Input.GetKeyDown(KeyCode.F3))
            {
                DebugFreeCamEnabled = !DebugFreeCamEnabled;
            }
            if (Input.GetKeyDown(KeyCode.F2)) // Debug No Clip
            {
                DebugNoClipEnabled = !DebugNoClipEnabled;
            }
            if (Input.GetKeyDown(KeyCode.F4)) // Debug Pause
            {
                DebugPause = !DebugPause;
            }
            
            if (m_isIteratingToNextFrame)
            {
                m_isIteratingToNextFrame = false;
                Time.timeScale = 0;
            }
            if (DebugPause && Input.GetKeyDown(KeyCode.F5)) // Debug pause next frame
            {
                {
                    m_isIteratingToNextFrame = true;
                    Time.timeScale = 1;
                }
            }

            if (DebugFreeCamEnabled || DebugNoClipEnabled)
            {
                bool isAccelerating = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool isSlowing = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.LeftAlt);
                
                // Position
                var rawForward = (Input.GetKey(KeyCode.W) ? 1f : 0) + (Input.GetKey(KeyCode.S) ? -1f : 0);
                var rawHorizontal = (Input.GetKey(KeyCode.D) ? 1f : 0) + (Input.GetKey(KeyCode.A) ? -1f : 0);
                var rawVertical = (Input.GetKey(KeyCode.Q) ? 1f : 0) + (Input.GetKey(KeyCode.E) ? -1f : 0);

                Vector3 moveInput = new Vector3(rawHorizontal, rawVertical, rawForward);
                moveInput = m_debugCameraRot * moveInput;
                
                float acceleration = isAccelerating ? 50f : (isSlowing ? 1f : 8f);
                
                m_debugCameraPos += moveInput * acceleration * Time.unscaledDeltaTime;
                
                // Rotation
                float rotationAccel = isAccelerating ? 360f : (isSlowing ? 60f : 180f);
                var rawYaw = Input.GetAxis("Mouse X") * 1.5f * rotationAccel * Time.unscaledDeltaTime;
                var rawPitch = -Input.GetAxis("Mouse Y") * rotationAccel * Time.unscaledDeltaTime;
                
                m_debugCameraRot = Quaternion.Euler(m_debugCameraRot.eulerAngles.x + rawPitch, m_debugCameraRot.eulerAngles.y + rawYaw, 0f);
                
                Camera.main.transform.position = m_debugCameraPos;
                Camera.main.transform.rotation = m_debugCameraRot;

                if (DebugNoClipEnabled)
                {
                    var targetPos = m_debugCameraPos + m_debugCameraRot * Vector3.forward * 6f;
                    m_debugNoClipController?.SetCenterPosition(targetPos);
                    m_debugNoClipController?.SetRotation(m_debugCameraRot);
                    m_debugNoClipController?.SetVelocity(Vector3.zero); // No movement while in free cam

                    {
                        using var lineWidth = Draw.ingame.WithLineWidth(2f);
                        Draw.ingame.Line(targetPos - Vector3.up * 1.5f, targetPos + Vector3.up * 1.5f, Color.green);
                        Draw.ingame.Line(targetPos - Vector3.right, targetPos + Vector3.right , Color.red);
                        Draw.ingame.Line(targetPos - Vector3.forward, targetPos + Vector3.forward , Color.blue);

                        var labelPos = targetPos + m_debugCameraRot * (Vector3.up + Vector3.right) * 0.75f;
                        var labelText = $"[Pos] - x: {targetPos.x:F1}, y: {targetPos.y:F1}, z: {targetPos.z:F1}\n[Rot] - pitch: {m_debugCameraRot.eulerAngles.x:F1}, yaw: {m_debugCameraRot.eulerAngles.y:F1}, roll: {m_debugCameraRot.eulerAngles.z:F1}";

                        Draw.ingame.Label2D(labelPos, labelText, 16f);
                    }
                }
            }
            
//#if !UNITY_EDITOR
            DrawGizmos(); // Otherwise handles by DrawManager for ALINE in editor
//#endif
        }
        
        public void DrawGizmos()
        {
            if (!HasDebugContext) return;
            
            // Draw label on the context object
            {
                Draw.ingame.SolidCircle(m_debugContext.transform.PositionWithLocalOffset(2*Vector3.up), Camera.main.transform.forward, 0.1f, Color.green);
            }
            
            var providers = m_debugProviders[m_debugContext];
            //
            foreach (var provider in providers)
            {
                if (provider.IsDrawEnabled)
                    provider.Provider.OnDebugDraw();
            }
        }
        
        private void OnGUI()
        {
            if (!DebugGUIEnabled) return;
            
            GUI.backgroundColor = Color.black;
            if (IsDebugWindowOpen)
            {
                m_debugManagerRect = GUILayout.Window(0, m_debugManagerRect, DrawManagerWindow, "🐛 Runtime Debug Manager");
            }

            if (m_worldContextSelectionOpen)
            {
                foreach (var context in m_debugProviders.Keys)
                {
                    if (!context || !context.activeInHierarchy) continue;
            
                    var contextScreenPoint =
                        Camera.main.WorldToScreenPoint(context.transform.position + context.transform.up * 1.8f);
            
                    var rect = new Rect(contextScreenPoint.x - 50, Screen.height - contextScreenPoint.y - 20, 100, 20);
                    if (GUI.Button(rect, context.name))
                    {
                        m_debugContext = context;
                        IsDebugWindowOpen = true; // Open the debug window when selecting a context
                    }
                }
            }

            if (HasDebugContext)
            {
                // Draw active windows
                foreach (var provider in m_debugProviders[m_debugContext])
                {
                    provider.GUIDraw();
                }
            }

            DrawStatusBar();
        }

        private bool m_isDragging = false;
        private bool m_performanceGraphDropdown = false;
        private Queue<float> m_frameTimeCache = new Queue<float>(256);
        private TimeSince m_timeSinceLastFrameCacheUpdate;
        private void DrawManagerWindow(int id)
        {
            if (GUIWindowUtility.CloseWindowButton(m_debugManagerRect)) IsDebugWindowOpen = false;
            
            if (m_debugContext)
            {
                m_debugWindowScrollPosition = GUILayout.BeginScrollView(m_debugWindowScrollPosition);
                foreach (var provider in m_debugProviders[m_debugContext])
                {
                    DrawProvider(provider);
                }

                GUILayout.EndScrollView();
            }

            if (DebugGUI.BeginFoldout("Performance Graph", ref m_performanceGraphDropdown))
            {
                var rect = GUILayoutUtility.GetAspectRect(4f);
                DebugGUI.Plot(m_frameTimeCache.ToArray(), rect, maxY: 0.01f, symmetric: false);
                DebugGUI.EndFoldout();
            }

            if (m_timeSinceLastFrameCacheUpdate > 0.05)
            {
                m_frameTimeCache.Enqueue(1/m_performanceGraph.CurrentFPS);
                if (m_frameTimeCache.Count > 256) m_frameTimeCache.Dequeue();
                m_timeSinceLastFrameCacheUpdate = 0;
            }

            m_debugManagerRect = GUIWindowUtility.ResizeRect(m_debugManagerRect, 240f, 240, ref m_isDragging);
        }

        private void DrawProvider(DebugProviderState provider)
        {
            var ogColor = GUI.backgroundColor;
            
            GUILayout.BeginHorizontal(GUI.skin.box);

            var labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontStyle = FontStyle.Bold;

            GUI.backgroundColor = Color.cyan;
            GUILayout.Label(provider.Title, labelStyle);
            GUI.backgroundColor = ogColor;
            GUILayout.FlexibleSpace();
            
            provider.IsDrawEnabled = GUILayout.Toggle(provider.IsDrawEnabled, "", GUILayout.Width(20f), GUILayout.Height(20f));

            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Window", GUILayout.Width(60), GUILayout.Height(20f)))
            {
                provider.IsWindowOpen = !provider.IsWindowOpen;
            }

            GUI.backgroundColor = ogColor;
            
            GUILayout.EndHorizontal();
        }

        private bool m_contextSelectionOpen = false;
        private Vector2 m_contextSelectionScrollPosition = Vector2.zero;

        private bool m_debugRenderSelectionOpen = false;
        
        private void DrawStatusBar()
        {
            float barHeight = 30f;
            Rect statusBarRect = new Rect(0, Screen.height - barHeight, Screen.width, barHeight);
            
            // Custom style for status bar background
            GUIStyle barStyle = new GUIStyle(GUI.skin.box);
            barStyle.normal.background = Texture2D.whiteTexture;
            var ogColor = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
            GUI.Box(statusBarRect, GUIContent.none, barStyle);
            GUI.color = ogColor; // Reset color
            
            
            // Create a custom label style
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            labelStyle.alignment = TextAnchor.MiddleLeft;

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            buttonStyle.alignment = TextAnchor.MiddleLeft;
            
            GUIStyle highlightStyle = new GUIStyle(labelStyle);
            highlightStyle.normal.textColor = new Color(0.56f, 0.74f, 1f); // Cyan blue
            
            // Content with better positioning
            float padding = 10f;
            float yOffset = (statusBarRect.yMax - 25f); // Center vertically
            
            // Left side
            float x = padding;
            if (GUI.Button(new Rect(x, yOffset, 20, 20), "☰"))
                IsDebugWindowOpen = !IsDebugWindowOpen;
            x += 30;

            var buttonRect = new Rect(x, yOffset, 60, 20);
            GUI.Label(buttonRect, "Context:", labelStyle);
            
            x += 60;
            buttonRect = new Rect(x, yOffset, 150, 20);
            
            if (GUI.Button(buttonRect, HasDebugContext ? m_debugContext.name : "None", highlightStyle))
            {
                m_contextSelectionOpen = !m_contextSelectionOpen;
            }

            if (m_contextSelectionOpen)
            {
                GUILayout.Window("ContextSelection".GetHashCode(),
                    new Rect(buttonRect.x - 90, buttonRect.y - 490, 240, 480),
                    (int windowID) =>
                    {
                        m_contextSelectionScrollPosition = GUILayout.BeginScrollView(m_contextSelectionScrollPosition);
                        foreach (var context in m_debugProviders.Keys)
                        {
                            if (GUILayout.Button(context.name))
                            {
                                m_debugContext = context;
                                m_contextSelectionOpen = false;
                                m_contextSelectionScrollPosition = Vector2.zero; // Reset scroll position when changing context
                            }
                        }
                        GUILayout.EndScrollView();
                        
                    }, "Select Context");
            }

            x += 150;
            GUI.Label(new Rect(x, yOffset, 120, 20), "|", labelStyle);
            x += 10;
            var hintStyle = new GUIStyle(labelStyle);
            hintStyle.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(x, yOffset, 600, 20), "[F1] Unlock Cursor | [F2] No Clip | [F3] Free Cam | [F4] Debug Pause | [F5] Next Frame", hintStyle);
            
            //GUI.Label(new Rect(x, yOffset, 150, 20), HasDebugContext ? m_debugContext.name : "None", highlightStyle);
            
            // Right side (work backwards from right edge)
            float rightX = Screen.width - padding;
            
            // Debug overhead
            rightX -= 60; // Width of "X.Xms"
            GUI.Label(new Rect(rightX, yOffset, 60, 20), $"{1/m_performanceGraph.AverageFPS:F3}ms", GetFPSStyle());
            
            rightX -= 30; // Width of "Debug Overhead: "
            GUI.Label(new Rect(rightX, yOffset, 30, 20), "ms:", labelStyle);
            
            rightX -= 20; // Separator
            GUI.Label(new Rect(rightX, yOffset, 20, 20), "|", labelStyle);
            
            // FPS
            rightX -= 60; // Width of "XX.X"
            GUI.Label(new Rect(rightX, yOffset, 60, 20), $"{m_performanceGraph.AverageFPS:F1}", GetFPSStyle());
            
            rightX -= 40; // Width of "FPS: "
            GUI.Label(new Rect(rightX, yOffset, 40, 20), "FPS:", labelStyle);
            
            // Debug Render Type Selection
            rightX -= 240;
            GUI.Label(new Rect(rightX, yOffset, 100, 20), "Render Type:", labelStyle);

            rightX += 100;
            buttonRect.x = rightX;
            buttonRect.width = 100;
            var ogBGColor = GUI.backgroundColor;
            GUI.backgroundColor = DebugRenderBlit.DebugType == DebugRenderBlit.Type.None ? Color.crimson : Color.forestGreen;
            if (GUI.Button(buttonRect, DebugRenderBlit.DebugType.ToString(), buttonStyle))
            {
                m_debugRenderSelectionOpen = !m_debugRenderSelectionOpen;
            }

            GUI.backgroundColor = ogBGColor;

            if (m_debugRenderSelectionOpen)
            {
                var windowRect = new Rect(buttonRect.x - buttonRect.width/2, buttonRect.y - 245, 240, 240);
                GUILayout.Window("RenderMode".GetHashCode(), windowRect,
                    (int windowID) =>
                    {
                        if (GUIWindowUtility.CloseWindowButton(windowRect)) m_debugRenderSelectionOpen = false;
                        
                        if (GUILayout.Button("None")) DebugRenderBlit.DebugType = DebugRenderBlit.Type.None;
                        if (GUILayout.Button("Depth")) DebugRenderBlit.DebugType = DebugRenderBlit.Type.Depth;
                        if (GUILayout.Button("Normals")) DebugRenderBlit.DebugType = DebugRenderBlit.Type.Normals;
                        if (GUILayout.Button("Motion Vectors")) DebugRenderBlit.DebugType = DebugRenderBlit.Type.MotionVectors;
                        if (GUILayout.Button("GameObject Tag"))
                            DebugRenderBlit.DebugType = DebugRenderBlit.Type.GameTag;
                        if (GUILayout.Button("Physics Layer")) DebugRenderBlit.DebugType = DebugRenderBlit.Type.PhysicsLayer;
                        if (GUILayout.Button("Collision")) DebugRenderBlit.DebugType = DebugRenderBlit.Type.Collision;
                        if (GUILayout.Button("Unique Meshes")) DebugRenderBlit.DebugType = DebugRenderBlit.Type.UniqueMeshes;

                    }, "Select Render Mode");
            }
            
        }
        
        // Helper to create colored label styles
        private GUIStyle GetFPSStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleLeft;
            float fps = 1 / Time.unscaledDeltaTime;
            if (fps >= 60) style.normal.textColor = Color.green;
            else if (fps >= 30) style.normal.textColor = Color.yellow;
            else style.normal.textColor = Color.red;
            return style;
        }
    }
}