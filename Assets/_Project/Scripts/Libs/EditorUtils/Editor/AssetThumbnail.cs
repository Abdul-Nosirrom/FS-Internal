using System.Collections.Generic;
using System.Linq;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.Editor
{
    public static class AssetThumbnail
    {
        private static Dictionary<Object, Texture2D> s_previewCache = new();
        private static PreviewRenderUtility s_previewRender;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            ClearCache();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ClearCache;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || 
                state == PlayModeStateChange.EnteredEditMode)
            {
                // Invalidate cached textures
                var keysToInvalidate = s_previewCache.Keys.ToList();
                foreach (var key in keysToInvalidate)
                {
                    s_previewCache[key] = null;
                }
                
                // Dispose preview render utility
                if (s_previewRender != null)
                {
                    s_previewRender.Cleanup();
                    s_previewRender = null;
                }
            }
        }

        public static Texture2D GetThumbnail(Object asset, EditorWindow editor = null)
        {
            // Check cache
            if (s_previewCache.TryGetValue(asset, out var cached))
            {
                if (cached != null && cached)
                    return cached;
                
                s_previewCache.Remove(asset);
            }

            Texture2D thumb = null;

            if (asset is GameObject go)
            {
                thumb = RenderPrefabThumbnail(go);
            }
            else if (asset is ScriptableObject so)
            {
                thumb = GetScriptableObjectPreview(so, editor);
            }
            else if (asset is Material || asset is Mesh)
            {
                // For materials and meshes, try Unity's preview first
                thumb = AssetPreview.GetAssetPreview(asset);
        
                // If null, check if it's still loading
                if (thumb == null && AssetPreview.IsLoadingAssetPreviews())
                {
                    // Use mini thumbnail as placeholder while loading
                    thumb = AssetPreview.GetMiniThumbnail(asset);
                    if (editor) editor.Repaint();
                    // Don't cache while loading
                    return thumb ?? EditorIcons.LoadingBar.Raw;
                }
        
                // If still null after loading, use mini thumbnail
                if (thumb == null)
                {
                    thumb = AssetPreview.GetMiniThumbnail(asset);
                }
            }
            else
            {
                thumb = AssetPreview.GetAssetPreview(asset);
                if (thumb == null)
                {
                    thumb = AssetPreview.GetMiniThumbnail(asset);
                    if (AssetPreview.IsLoadingAssetPreviews())
                    {
                        if (editor) editor.Repaint();
                    }
                }
            }

            // Cache valid thumbnails (but not placeholders)
            if (thumb != null && thumb && thumb != EditorIcons.LoadingBar.Raw)
                s_previewCache[asset] = thumb;

            return thumb ?? EditorIcons.LoadingBar.Raw;
        }

        private static Texture2D RenderPrefabThumbnail(GameObject prefab)
        {
            // Initialize preview render utility if needed
            if (s_previewRender == null)
            {
                s_previewRender = new PreviewRenderUtility();
                s_previewRender.camera.fieldOfView = 30f;
                s_previewRender.camera.nearClipPlane = 0.01f;
                s_previewRender.camera.farClipPlane = 100f;
                //s_previewRender.camera.clearFlags = CameraClearFlags.SolidColor;
                //s_previewRender.camera.backgroundColor = Color.darkSlateGray;
            }

            // Instantiate the prefab in the preview scene
            GameObject instance = Object.Instantiate(prefab);
            
            try
            {
                // Add to preview scene
                s_previewRender.AddSingleGO(instance);
                
                // Force update transforms and bounds
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                
                // Wait a frame for any Awake/Start initialization (for procedural meshes)
                // This is important for spline meshes that generate geometry on Awake
                foreach (var component in instance.GetComponents<MonoBehaviour>())
                {
                    if (component != null)
                    {
                        // Manually trigger Awake if it exists
                        try
                        {
                            //component.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
                        }
                        catch
                        {
                            // Should run behavior exceptions normally
                        }
                    }
                }
                
                // Calculate bounds of all renderers
                Bounds? compoundBounds = null;
                
                var renderers = instance.GetComponentsInChildren<Renderer>(true); // Include inactive
                
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    
                    // Skip disabled renderers
                    if (!renderer.enabled) continue;
                    
                    // Skip particle systems
                    if (renderer is ParticleSystemRenderer) continue;

                    if (!compoundBounds.HasValue) compoundBounds = renderer.bounds;
                    else compoundBounds.Value.Encapsulate(renderer.bounds);
                }

                if (!compoundBounds.HasValue)
                {
                    // No renderers, fall back to Unity's preview
                    Object.DestroyImmediate(instance);
                    return AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab);
                }
                
                var bounds = compoundBounds.Value;

                // Ensure bounds aren't too small
                Vector3 size = bounds.size;
                if (size.magnitude < 0.01f)
                {
                    size = Vector3.one * 0.5f;
                    bounds.size = size;
                }

                // Camera angle direction
                Vector3 direction = Quaternion.Euler(20, -120, 0) * Vector3.forward;
        
                // Calculate which dimensions are visible from this angle
                // Project the bounds onto the camera plane
                float fov = s_previewRender.camera.fieldOfView;
                float halfFOV = fov * 0.5f * Mathf.Deg2Rad;
        
                // Get the radius of a sphere that would encompass the bounds
                float radius = bounds.extents.magnitude;
        
                // Calculate distance needed to fit the sphere in view
                float distance = radius / Mathf.Tan(halfFOV);
        
                // Add padding (1.15x to have some breathing room)
                distance *= 1.15f;
        
                // Clamp to reasonable values
                distance = Mathf.Clamp(distance, 1f, 200f);
        
                // Position camera
                s_previewRender.camera.transform.position = bounds.center - direction * distance;
                s_previewRender.camera.transform.LookAt(bounds.center);
                
                // Set up lighting for good visibility
                s_previewRender.lights[0].intensity = 1.4f;
                s_previewRender.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
                s_previewRender.lights[0].color = Color.white;
                
                s_previewRender.lights[1].intensity = 0.4f;
                s_previewRender.lights[1].transform.rotation = Quaternion.Euler(-60, -30f, 0f);
                s_previewRender.lights[1].color = new Color(0.8f, 0.8f, 1f); // Slightly cool fill light

                // Render at desired resolution
                int resolution = 256;
                s_previewRender.BeginStaticPreview(new Rect(0, 0, resolution, resolution));
                
                // Render with lighting
                s_previewRender.Render(true);
                
                Texture2D thumbnail = s_previewRender.EndStaticPreview();

                return thumbnail;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to render preview for {prefab.name}: {e.Message}");
                Object.DestroyImmediate(instance);
                return AssetPreview.GetMiniThumbnail(prefab);
            }
            finally
            {
                // Clean up instance
                if (instance != null)
                    Object.DestroyImmediate(instance);
            }
        }
                
        private static Texture2D GetScriptableObjectPreview(ScriptableObject so, EditorWindow editor)
        {
            Texture2D thumb = null;
            
            // Try custom editor preview
            var soEditor = UnityEditor.Editor.CreateEditor(so);
            if (soEditor != null)
            {
                if (soEditor.HasPreviewGUI())
                {
                    thumb = soEditor.RenderStaticPreview(AssetDatabase.GetAssetPath(so), null, 128, 128);
                }
                Object.DestroyImmediate(soEditor);
            }

            // Fallback
            if (thumb == null)
            {
                thumb = AssetPreview.GetAssetPreview(so);
                if (thumb == null)
                {
                    thumb = AssetPreview.GetMiniThumbnail(so);
                    if (AssetPreview.IsLoadingAssetPreviews() && editor != null)
                    {
                        editor.Repaint();
                    }
                }
            }

            return thumb;
        }

        public static void ClearCache()
        {
            s_previewCache.Clear();
            
            if (s_previewRender != null)
            {
                s_previewRender.Cleanup();
                s_previewRender = null;
            }
        }
    }
}