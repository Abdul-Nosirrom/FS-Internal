using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace FS.Editor
{
    public class MaterialBrowserEditorWindow : EditorWindow
    {
        [MenuItem("Window/Material Browser")]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow<MaterialBrowserEditorWindow>();
            window.titleContent = new GUIContent("Material Browser");
            window.minSize = new Vector2(300, 200);
            window.Show();
        }

        private const int k_batchSize = 10;
        
        private async Task CollectMaterialsAsync()
        {
            m_isFullyInitialized = false;
            m_initializationProgress = 0f;
            
            m_materialThumbnails.Clear();
            string[] guids = AssetDatabase.FindAssets("t:material");
            for (int i = 0; i < guids.Length; i += k_batchSize) {
                // Process batch
                int end = Mathf.Min(i + k_batchSize, guids.Length);
                for (int j = i; j < end; j++) {
                    string path = AssetDatabase.GUIDToAssetPath(guids[j]);
                    if (!path.Contains("Assets")) continue; // Only project materials
                    
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null) continue;
                    // Add to your collection

                    m_materialThumbnails[material] = null;
                    while (m_materialThumbnails[material] == null)
                    {
                        m_materialThumbnails[material] = material.mainTexture as Texture2D 
                                                         ?? AssetPreview.GetAssetPreview(material);
                        await Task.Delay(1); // Allow thumbnail to load
                    }
                }
                
                m_initializationProgress = Mathf.Clamp01((float)i / guids.Length);
                Repaint();
                await Task.Delay(1); // Allow editor to breathe
            }

            m_materialArray = m_materialThumbnails.Keys.ToArray();
            m_materialThumbnailsArray = m_materialThumbnails.Values.ToArray();
            m_isFullyInitialized = true;
            Repaint();
        }

        private bool m_isFullyInitialized = false;
        private float m_initializationProgress = 0f;
        private Vector2 m_materialScrollPosition = Vector2.zero;
        private float m_thumbnailSize = 128f;
        private int m_materialSelection = -1;
        private Dictionary<Material, Texture2D> m_materialThumbnails = new();
        private Texture2D[] m_materialThumbnailsArray;
        private Material[] m_materialArray;
        
        private async void OnEnable()
        {
            m_isFullyInitialized = false;
            // Start collecting materials when the window is opened
            await CollectMaterialsAsync();
        }

        private void OnGUI()
        {
            using var mainVertical = new EditorGUILayout.VerticalScope();

            FreeSkiesEditor.DrawLogo(200f, GUIStyles.GUIStyles.HelpBox);
            
            if (!m_isFullyInitialized)
            {
                using var progressBar = new EditorGUILayout.HorizontalScope();
                var progressBarRect = progressBar.rect;
                progressBarRect.height = 50f;
                progressBarRect.xMin += 5f;
                progressBarRect.xMax -= 5f;
                EditorGUI.ProgressBar(progressBarRect, m_initializationProgress, "Loading Materials...");
                return;
            }
            
            if (m_materialThumbnails.Count == 0)
            {
                EditorGUILayout.LabelField("No materials found...", GUIStyles.GUIStyles.WhiteLargeCenterLabel);
                return;
            }
            
            m_thumbnailSize = EditorGUILayout.Slider("Thumbnail Size", m_thumbnailSize, 64f, 512f);
            // Ensure thumbnail size is a power of 2
            m_thumbnailSize = Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(m_thumbnailSize));

            DrawMaterialGrid(m_thumbnailSize);
        }
        
        void DrawMaterialGrid(float thumbnailSize)
        {
            // Calculate grid parameters
            int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 10f) / (thumbnailSize)));
            int rows = Mathf.CeilToInt((float)m_materialThumbnailsArray.Length / columns);
            
            // Begin scroll view if needed
            // Calculate grid parameters
            float contentHeight = rows * (m_thumbnailSize + 4f);

            float nameLabelHeight = 20f;
    
            // Begin scroll view
            m_materialScrollPosition = EditorGUILayout.BeginScrollView(m_materialScrollPosition, 
                GUIStyles.GUIStyles.HelpBox);
            Rect scrollViewRect = 
                GUILayoutUtility.GetRect(position.width /2f, contentHeight, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            
            // Draw grid cells
            for (int i = 0; i < m_materialThumbnailsArray.Length; i++)
            {
                int row = i / columns;
                int col = i % columns;
                
                Rect cellRect = new Rect(
                    10 + col * (thumbnailSize + 4f),
                    10f + row * (thumbnailSize + 4f + nameLabelHeight),
                    thumbnailSize,
                    thumbnailSize
                );
                
                GUI.Label(cellRect, new GUIContent("", $"Mat: {m_materialArray[i].name}\nShader: {m_materialArray[i].shader.name}"));
                
                // Draw selection background if this is the selected material
                //if (i == m_materialSelection)
                {
                    Color oldColor = GUI.color;
                    GUI.color = i == m_materialSelection ? new Color(0.5f, 0.7f, 1f, 1f) : Color.black;
                    GUI.DrawTexture(new Rect(cellRect.x - 1, cellRect.y - 1, cellRect.width + 2, cellRect.height + 2), EditorGUIUtility.whiteTexture);
                    GUI.color = oldColor;
                }
                
                // Draw material thumbnail
                if (m_materialThumbnailsArray[i] == null)
                {
                    m_materialThumbnailsArray[i] = m_materialArray[i].mainTexture as Texture2D 
                                                   ?? AssetPreview.GetAssetPreview(m_materialArray[i]);
                }
                else GUI.DrawTexture(cellRect, m_materialThumbnailsArray[i], ScaleMode.ScaleToFit);
                
                // Draw name border
                Rect nameBorderRect = new Rect(cellRect.x - 1f, cellRect.y + cellRect.height, cellRect.width + 2f, nameLabelHeight);
                EditorGUI.DrawRect(nameBorderRect, new Color(0,0,0,i == m_materialSelection ? 0.75f : 0.35f));
                
                // Draw Name
                var labelStyle = GUIStyles.GUIStyles.WhiteLargeCenterLabel;
                GUI.Label(nameBorderRect, m_materialArray[i].name, GUIStyles.GUIStyles.WhiteLargeCenterLabel);

                // Handle click events
                if (Event.current.type == EventType.MouseDown && cellRect.Contains(Event.current.mousePosition))
                {
                    m_materialSelection = i;
                    GUI.changed = true;
    
                    // Prepare drag but don't start yet
                    if (Event.current.button == 0 && m_materialArray[i] != null)
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new UnityEngine.Object[] { m_materialArray[i] };
                        DragAndDrop.SetGenericData("MaterialDrag", m_materialArray[i].name);
                    }
    
                    Event.current.Use();
                }
                // Handle the drag start as a separate event
                else if (Event.current.type == EventType.MouseDrag && cellRect.Contains(Event.current.mousePosition) && 
                         m_materialSelection == i && DragAndDrop.objectReferences.Length > 0)
                {
                    // Now we can start the drag operation
                    DragAndDrop.StartDrag(DragAndDrop.GetGenericData("MaterialDrag") as string ?? "Material");
                    Event.current.Use();
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
    }
}