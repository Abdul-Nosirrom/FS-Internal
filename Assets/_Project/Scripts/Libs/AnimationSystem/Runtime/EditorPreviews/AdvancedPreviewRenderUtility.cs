#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Object = UnityEngine.Object;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace FS.Animation.Editor
{
    public abstract class AdvancedPreviewRenderUtility : IDisposable
    {
        public PreviewRenderUtility PreviewScene => m_previewRenderUtility;
        private PreviewRenderUtility m_previewRenderUtility;

        private static HashSet<AdvancedPreviewRenderUtility> s_activePreviews = new();
        private HashSet<GameObject> m_objectsInPreview = new();
        
        public static bool TryGetActivePreviewForObject<T>(GameObject go, out T preview) where T : AdvancedPreviewRenderUtility
        {
            foreach (var activePreview in s_activePreviews)
            {
                if (activePreview is T typedPreview && activePreview.m_objectsInPreview.Contains(go))
                {
                    preview = typedPreview;
                    return true;
                }
            }

            preview = null;
            return false;
        }
        
        public void RegisterPreviewableObject(GameObject go)
        {
            if (go == null || m_objectsInPreview.Contains(go)) return;
            m_objectsInPreview.Add(go);
        }
        
        public void UnregisterPreviewableObject(GameObject go)
        {
            if (go == null || !m_objectsInPreview.Contains(go)) return;
            m_objectsInPreview.Remove(go);
        }
        
        private Mesh m_floorPlane;
        private Material m_floorGridMaterial;
        private Material m_shadowPlaneMaterial;
        private Material m_shadowMaskMaterial;

        private readonly Color m_ambientColor = new Color(.1f, .1f, .1f, 0);
        
        /// <summary>
        /// Option to draw custom GUI stuff in the Preview Rect, and modify the preview rect to account for it
        /// </summary>
        /// <param name="previewRect"></param>
        protected abstract void DoPreviewWindowControls(ref Rect previewRect);
        /// <summary>
        /// Option to draw custom GUI stuff in the Preview Rect, such as buttons, labels, etc.
        /// </summary>
        /// <param name="previewRect"></param>
        protected abstract void DoGUIOnPreviewRect(Rect previewRect);
        /// <summary>
        /// Renderable Bounds Of Preview Object, relevant for rendering shadow map
        /// </summary>
        protected abstract void SetupBounds();

        /// <summary>
        /// Respond to an object being dragged into the preview window.
        /// </summary>
        protected abstract bool ShouldAcceptDragObject(Object newPreviewObject);
        
        private float m_boundingVolumeScale = 5f;
        
        protected virtual Vector3 m_floorPosition => Vector3.zero;
        private const float k_floorScale = 5;
        private const float k_defaultIntensity = 1.4f;
        
        public AdvancedPreviewRenderUtility()
        {
            m_previewRenderUtility = new PreviewRenderUtility();
            m_previewRenderUtility.camera.fieldOfView = 30.0f;
            m_previewRenderUtility.camera.allowHDR = false;
            m_previewRenderUtility.camera.allowMSAA = false;
            m_previewRenderUtility.ambientColor = m_ambientColor;
            m_previewRenderUtility.lights[0].intensity = k_defaultIntensity;
            m_previewRenderUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0);
            m_previewRenderUtility.lights[1].intensity = k_defaultIntensity;
            
            s_activePreviews.Add(this);
        }
        
        public void Dispose()
        {
            s_activePreviews.Remove(this);
            m_previewRenderUtility?.Cleanup();
            m_previewRenderUtility = null;
        }
        
        private void InitResources()
        {
            m_floorPlane ??= Resources.GetBuiltinResource<Mesh>("New-Plane.fbx");

            if (!m_floorGridMaterial)
            {
                m_floorGridMaterial = new Material(Shader.Find("Hidden/Editor/PreviewPlane"));
                m_floorGridMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            
            if (!m_shadowMaskMaterial)
            {
                m_shadowMaskMaterial = new Material(EditorGUIUtility.LoadRequired("Previews/PreviewShadowMask.shader") as Shader);
                m_shadowMaskMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            if (!m_shadowPlaneMaterial)
            {
                m_shadowPlaneMaterial =
                    new Material(EditorGUIUtility.LoadRequired("Previews/PreviewShadowPlaneClip.shader") as Shader);
                m_shadowPlaneMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private readonly int m_previewHint = "Preview".GetHashCode();
        private readonly int m_PreviewSceneHint = "PreviewSene".GetHashCode();

        public virtual void DoPreviewSettings()
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.Toggle(Is2D, Styles.is2D, Styles.preButton);
            if (EditorGUI.EndChangeCheck())
            {
                Is2D = !Is2D;
                EditorPrefs.SetBool("Is2DAdvPrev", Is2D);
            }
        }

        public Rect DoRenderPreview(Rect previewRect, GUIStyle background, bool doControls = true)
        {
            InitResources();
            
            // Some virtual function that can modify the preview rect
            DoPreviewWindowControls(ref previewRect);

            int previewControlID = GUIUtility.GetControlID(m_previewHint, FocusType.Passive, previewRect);
            
            Event current = Event.current;
            if (current.GetTypeForControl(previewControlID) == EventType.Repaint)
                InternalPreviewRender(previewRect, background);
            
            // Draw handles event
            DrawHandles(previewRect); // TODO: Handles work perfectly in floating windows!

            if (doControls) DoPreviewControlsAndGUI(previewRect);

            return previewRect;
        }
        
        /// <summary>
        /// View controls split from render function to allow option to delay its call so you can draw
        /// other controls on the preview render without the events being eaten up by the camera controls
        /// </summary>
        public void DoPreviewControlsAndGUI(Rect previewRect)
        {
            int previewSceneControlID = GUIUtility.GetControlID(m_PreviewSceneHint, FocusType.Passive, previewRect);
            
            Event current = Event.current;

            EventType typeForControl = current.GetTypeForControl(previewSceneControlID);
            
            DoAssetDrag(current, typeForControl);
            HandleViewTool(current, typeForControl, previewSceneControlID, previewRect);
            HandleReframingFocus(current, typeForControl, previewRect);
            
            // TODO: More Utility found here
            
            if (current.type == EventType.Repaint)
                EditorGUIUtility.AddCursorRect(previewRect, CurrentCursor);
            
            // For stuff that is drawn ontop of the preview render and doesnt modify its rect
            DoGUIOnPreviewRect(previewRect);
        }
        
        public event Action OnDrawHandles;

        private void DrawHandles(Rect previewRect)
        {
            Camera camera = m_previewRenderUtility.camera;
            GUI.BeginGroup(previewRect);
            //camera.cameraType = CameraType.SceneView;
            
            Handles.SetCamera(previewRect, camera);
            Vector3 cameraScreenPoint = new Vector3(previewRect.x + previewRect.width * 0.9f, previewRect.y + previewRect.height * 0.8f, 10);
            var worldPoint = camera.ScreenToWorldPoint(cameraScreenPoint);
            Handles.ScaleHandle(Vector3.one, worldPoint, Quaternion.identity, Is2D ? 0.5f * m_zoomFactor : 0.5f);
            
            //OnDrawHandles?.Invoke();
            
            GUI.EndGroup(); 
        }
        
        #region Internal Rendering

        private Quaternion MostAligned2DView()
        {
            return Quaternion.LookRotation(m_previewRenderUtility.camera.transform.right, m_previewRenderUtility.camera.transform.forward);
        }
        
        private void InternalPreviewRender(Rect previewRect, GUIStyle background)
        {
            m_previewRenderUtility.BeginPreview(previewRect, background);
            {
                var probe = RenderSettings.ambientProbe;
                SetupPreviewLightingAndFx(probe);

                // Calculate floor height and alpha
                //Quaternion floorRot = Is2D ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity;
                Quaternion floorRot = Is2D ? MostAligned2DView() : Quaternion.identity;

                Vector3 floorPos = m_floorPosition;

                // Render shadow map
                Matrix4x4 shadowMatrix;
                RenderTexture shadowMap = RenderPreviewShadowmap(m_previewRenderUtility.lights[0], m_boundingVolumeScale / 2, m_focusPosition, floorPos, out shadowMatrix);
                SetupPreviewLightingAndFx(probe);

                float tempZoomFactor = (Is2D ? 1.0f : m_zoomFactor);
                // Position camera
                m_previewRenderUtility.camera.orthographic = Is2D;
                if (Is2D)
                    m_previewRenderUtility.camera.orthographicSize = 2.0f * m_zoomFactor;
                m_previewRenderUtility.camera.nearClipPlane = 0.5f * tempZoomFactor;
                m_previewRenderUtility.camera.farClipPlane = 100.0f * m_focusScale;
                Quaternion camRot = Quaternion.Euler(-m_previewDir.y, -m_previewDir.x, 0);

                // Add panning offset
                Vector3 camPos = camRot * (Vector3.forward * -5.5f * tempZoomFactor) + m_focusPosition + m_pivotPositionOffset;
                m_previewRenderUtility.camera.transform.position = camPos;
                m_previewRenderUtility.camera.transform.rotation = camRot;

                // Render main floor
                {
                    Material mat = m_floorGridMaterial;
                    Matrix4x4 matrix = Matrix4x4.TRS(floorPos, floorRot, Vector3.one * 50);// * k_floorScale * m_focusScale);
                    
                    mat.SetTexture("_ShadowTexture", shadowMap);
                    mat.SetMatrix("_ShadowTextureMatrix", shadowMatrix);
                    mat.SetColor("_clearColor", m_ambientColor);
                    mat.SetVector("_cameraRight", m_previewRenderUtility.camera.transform.right);
                    mat.SetVector("_cameraUp", m_previewRenderUtility.camera.transform.up);
                    mat.SetInteger("_isOrtho", Is2D ? 1 : 0);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
                    Graphics.DrawMesh(m_floorPlane, matrix, mat, 0, m_previewRenderUtility.camera, 0);
                }

                var clearMode = m_previewRenderUtility.camera.clearFlags;
                m_previewRenderUtility.camera.clearFlags = CameraClearFlags.Nothing;
                //m_previewRenderUtility.Render(true, false);
                m_previewRenderUtility.camera.Render();
                m_previewRenderUtility.camera.clearFlags = clearMode;
                RenderTexture.ReleaseTemporary(shadowMap);
            }
            
            m_previewRenderUtility.EndAndDrawPreview(previewRect);
        }
        
        private void SetupPreviewLightingAndFx(SphericalHarmonicsL2 probe)
        {
            m_previewRenderUtility.lights[0].intensity = k_defaultIntensity;
            m_previewRenderUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0);
            m_previewRenderUtility.lights[1].intensity = k_defaultIntensity;
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientLight = m_ambientColor;
            RenderSettings.ambientProbe = probe;
        }
        
        private RenderTexture RenderPreviewShadowmap(Light light, float scale, Vector3 center, Vector3 floorPos, out Matrix4x4 outShadowMatrix)
        {
            Assert.IsTrue(Event.current.type == EventType.Repaint);

            // Set ortho camera and position it
            var cam = m_previewRenderUtility.camera;
            cam.orthographic = true;
            cam.orthographicSize = scale * 2.0f;
            cam.nearClipPlane = 1 * scale;
            cam.farClipPlane = 25 * scale;
            cam.transform.rotation = Is2D ? Quaternion.identity : light.transform.rotation;
            cam.transform.position = center - light.transform.forward * (scale * 5.5f);

            // Clear to black
            CameraClearFlags oldFlags = cam.clearFlags;
            cam.clearFlags = CameraClearFlags.SolidColor;
            Color oldColor = cam.backgroundColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);

            // Create render target for shadow map
            const int kShadowSize = 512;
            RenderTexture oldRT = cam.targetTexture;
            RenderTexture rt = RenderTexture.GetTemporary(kShadowSize, kShadowSize, 16);
            rt.isPowerOfTwo = true;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.filterMode = FilterMode.Bilinear;
            cam.targetTexture = rt;

            // Enable character and render with camera into the shadowmap
            cam.Render();
            

            // Draw a quad, with shader that will produce white color everywhere
            // where something was rendered (via inverted depth test)
            RenderTexture.active = rt;
            GL.PushMatrix();
            GL.LoadOrtho();
            m_shadowMaskMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Vertex3(0, 0, -99.0f);
            GL.Vertex3(1, 0, -99.0f);
            GL.Vertex3(1, 1, -99.0f);
            GL.Vertex3(0, 1, -99.0f);
            GL.End();

            // Render floor with black color, to mask out any shadow from character
            // parts that are under the preview plane
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.LoadIdentity();
            GL.MultMatrix(cam.worldToCameraMatrix);
            m_shadowPlaneMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            float sc = k_floorScale * scale;
            GL.Vertex(floorPos + new Vector3(-sc, 0, -sc));
            GL.Vertex(floorPos + new Vector3(sc, 0, -sc));
            GL.Vertex(floorPos + new Vector3(sc, 0, sc));
            GL.Vertex(floorPos + new Vector3(-sc, 0, sc));
            GL.End();

            GL.PopMatrix();

            // Shadowmap sampling matrix, from world space into shadowmap space
            Matrix4x4 texMatrix = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity,
                new Vector3(0.5f, 0.5f, 0.5f));
            outShadowMatrix = texMatrix * cam.projectionMatrix * cam.worldToCameraMatrix;

            // Restore previous camera parameters
            cam.orthographic = false;
            cam.clearFlags = oldFlags;
            cam.backgroundColor = oldColor;
            cam.targetTexture = oldRT;

            return rt;
        }
        
        #endregion
        
        #region Preview Controls

        public Vector3 CameraPivot
        {
            get => m_focusPosition;
            set => m_focusPosition = value;
        }
        
        protected Vector3 m_focusPosition = Vector3.up;
        protected virtual float m_focusScale => 1f;
        
        protected MouseCursor CurrentCursor
        {
            get
            {
                switch (m_viewTool)
                {
                    case ViewTool.Pan:
                        return MouseCursor.Pan;
                    case ViewTool.Zoom:
                        return MouseCursor.Zoom;
                    case ViewTool.Orbit:
                        return MouseCursor.Orbit;
                    default:
                        return MouseCursor.Arrow;
                }
            }
        }
        
        protected enum ViewTool
        {
            None,
            Pan,
            Zoom,
            Orbit,
        }
        
        private ViewTool m_viewTool = ViewTool.None;
        protected ViewTool viewTool
        {
            get
            {
                Event evt = Event.current;
                if (m_viewTool == ViewTool.None)
                {
                    bool controlKeyOnMac = (evt.control && Application.platform == RuntimePlatform.OSXEditor);

                    // actionKey could be command key on mac or ctrl on windows
                    bool actionKey = EditorGUI.actionKey;

                    bool noModifiers = (!actionKey && !controlKeyOnMac && !evt.alt);

                    if ((evt.button <= 0 && noModifiers) || (evt.button <= 0 && actionKey) || evt.button == 2)
                        m_viewTool = ViewTool.Pan;
                    else if ((evt.button <= 0 && controlKeyOnMac) || (evt.button == 1 && evt.alt))
                        m_viewTool = ViewTool.Zoom;
                    else if (evt.button <= 0 && evt.alt || evt.button == 1)
                        m_viewTool = ViewTool.Orbit;
                }
                return m_viewTool;
            }
        }

        private Vector3 m_pivotPositionOffset = Vector3.zero;
        private Vector2 m_previewDir = new Vector2(120, -20);
        private float m_zoomFactor = 1f;

        private Vector2 PreviewDir2D
        {
            get
            {
                float angleStep = 90f;
                float theta = m_previewDir.x;
                float phi = m_previewDir.y;
                // Ensure theta / phi are multiples of 45
                theta = Mathf.Round(theta / angleStep) * angleStep;
                phi = Mathf.Round(phi / angleStep) * angleStep;
                return new Vector2(theta, phi);
            }
        }
        private bool m_is2D = false;
        protected virtual bool Is2D
        {
            get => m_is2D;
            set
            {
                m_is2D = value;
                if (!m_is2D) return;
                m_previewDir = PreviewDir2D; 
            }
        }
        
        private void DoAssetDrag(Event evt, EventType type)
        {
            if (type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                evt.Use();
            }
            else if (type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                var newPreviewObject = DragAndDrop.objectReferences[0];

                if (newPreviewObject && ShouldAcceptDragObject(newPreviewObject))
                {
                    DragAndDrop.AcceptDrag();
                }

                evt.Use();
            }
        }
        
        protected Vector3 GetCurrentMouseWorldPosition(UnityEngine.Event evt, Rect previewRect)
        {
            Camera camera = m_previewRenderUtility.camera;
            float scaleFactor = m_previewRenderUtility.GetScaleFactor(previewRect.width, previewRect.height);
            return camera.ScreenToWorldPoint(new Vector3((evt.mousePosition.x - previewRect.x) * scaleFactor, (previewRect.height - (evt.mousePosition.y - previewRect.y)) * scaleFactor, 0.0f)
            {
                z = Vector3.Distance(this.m_focusPosition, camera.transform.position)
            });
        }
        
        public void ResetPreviewFocus()
        {
            m_pivotPositionOffset = m_focusPosition;// - this.rootPosition;
        }
        
        private void HandleReframingFocus(Event evt, EventType type, Rect previewRect)
        {
            if (type == UnityEngine.EventType.KeyDown && evt.keyCode == KeyCode.F)
            {
                this.ResetPreviewFocus();
                this.m_zoomFactor = this.m_focusScale;
                evt.Use();
            }
            if (type != UnityEngine.EventType.KeyDown || UnityEngine.Event.current.keyCode != KeyCode.G)
                return;
            this.m_pivotPositionOffset = this.GetCurrentMouseWorldPosition(evt, previewRect) - this.m_focusPosition;
            evt.Use();
        }
        
        private void HandleViewTool(Event evt, EventType eventType, int id, Rect previewRect)
        {
            switch (eventType)
            {
                case EventType.MouseDown:
                    this.HandleMouseDown(evt, id, previewRect);
                    break;
                case EventType.MouseUp:
                    this.HandleMouseUp(evt, id);
                    break;
                case EventType.MouseDrag:
                    this.HandleMouseDrag(evt, id, previewRect);
                    break;
                case EventType.ScrollWheel:
                    this.DoAvatarPreviewZoom(evt, HandleUtility.niceMouseDeltaZoom * (evt.shift ? 2f : 0.5f));
                    break;
            }
        }
        

        private void HandleMouseDown(Event evt, int id, Rect previewRect)
        {
            if (this.viewTool == ViewTool.None || !previewRect.Contains(evt.mousePosition))
                return;
            EditorGUIUtility.SetWantsMouseJumping(1);
            evt.Use();
            GUIUtility.hotControl = id;
        }
        
        private void HandleMouseUp(Event evt, int id)
        {
            if (GUIUtility.hotControl != id)
                return;
            m_viewTool = ViewTool.None;
            GUIUtility.hotControl = 0;
            EditorGUIUtility.SetWantsMouseJumping(0);
            evt.Use();
        }
        
        private void HandleMouseDrag(Event evt, int id, Rect previewRect)
        {
            // if ((UnityEngine.Object) this.m_previewInstance == (UnityEngine.Object) null || GUIUtility.hotControl != id)
            //     return;
            switch (m_viewTool)
            {
                case ViewTool.Pan:
                    this.DoAvatarPreviewPan(evt);
                    break;
                case ViewTool.Zoom:
                    this.DoAvatarPreviewZoom(evt,
                        (float)(-(double)HandleUtility.niceMouseDeltaZoom * (evt.shift ? 2.0 : 0.5)));
                    break;
                case ViewTool.Orbit:
                    this.DoAvatarPreviewOrbit(evt, previewRect);
                    break;
                default:
                    Debug.Log((object)"Enum value not handled");
                    break;
            }
        }

        public void DoAvatarPreviewPan(UnityEngine.Event evt)
        {
            Camera camera = m_previewRenderUtility.camera;
            Vector3 position = camera.WorldToScreenPoint(this.m_focusPosition + this.m_pivotPositionOffset) + new Vector3(-evt.delta.x, evt.delta.y, 0.0f) * Mathf.Lerp(0.25f, 2f, this.m_zoomFactor * 0.5f);
            this.m_pivotPositionOffset += camera.ScreenToWorldPoint(position) - (this.m_focusPosition + this.m_pivotPositionOffset);
            evt.Use();
        }
        
        private void DoAvatarPreviewZoom(Event evt, float delta)
        {
            this.m_zoomFactor += this.m_zoomFactor * (float) (-(double) delta * 0.05000000074505806);
            this.m_zoomFactor = Mathf.Max(this.m_zoomFactor, this.m_focusScale / 10f);
            evt.Use();
        }
        
        public void DoAvatarPreviewOrbit(UnityEngine.Event evt, Rect previewRect)
        {
            if (Is2D)
                Is2D = false;
            this.m_previewDir -= evt.delta * (evt.shift ? 3f : 1f) / Mathf.Min(previewRect.width, previewRect.height) * 140f;
            this.m_previewDir.y = Mathf.Clamp(this.m_previewDir.y, -90f, 90f);
            evt.Use();
        }
        
        #endregion
        
        #region STYLES CACHE
        
        public static class Styles
        {
            public static GUIContent pivot = EditorGUIUtility.TrIconContent("AvatarPivot", "Displays avatar's pivot and mass center");
            public static GUIContent ik = EditorGUIUtility.TrTextContent("IK", "Toggles feet IK preview");
            public static GUIContent is2D = EditorGUIUtility.TrIconContent("SceneView2D", "Toggles 2D preview mode");
            public static GUIContent avatarIcon = EditorGUIUtility.TrIconContent("AvatarSelector", "Changes the model to use for previewing.");
            public static GUIContent rootMotionIcon = EditorGUIUtility.TrIconContent("BodyPartPicker", "Toggles root motion for preview");
            
            public static GUIStyle preButton = new GUIStyle("toolbarbutton");
            public static GUIStyle preSlider = new GUIStyle("preSlider");
            public static GUIStyle preSliderThumb = new GUIStyle("preSliderThumb");
        }
        #endregion 
    }
}
#endif
