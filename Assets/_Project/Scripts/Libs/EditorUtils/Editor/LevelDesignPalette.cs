using System;
using System.Collections.Generic;
using System.Linq;
using FS.Attributes;
using FS.MeshProcessing;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FS.Editor
{
    /// <summary>
    /// Level Design Palette Window That Displays Assets For Level Design: (only showing stuff found under Assets/ & not in _External/)
    /// - Materials (sub-categorized by shader type)
    /// - Meshes (sub-categorized by asset labels (only show things that do have labels))
    /// - Prefabs/SOs (Categorized & sub-categorized by LevelDesignCategory attribute (i.e "LevelObject/Spring"))
    /// - Prefabs/SOs [not part of the above] (sub-categorized by physics layers)
    /// </summary>
    public class LevelDesignPalette : EditorWindow
    {
        [MenuItem("Free Skies/Tools/Level Design Palette")]
        public static void OpenWindow()
        {
            var window = GetWindow<LevelDesignPalette>();
            window.Show();
        }
        
        private struct PaletteCategory
        {
            public string name { get; private set; }
            public Dictionary<string, List<PaletteItem>> m_paletteItems; // Organized by sub-category
            public List<PaletteItem> m_flatPalletteItems; // Flat list of all items

            public static PaletteCategory Create(string name, string[] assetTypes, Func<Object, PaletteItem> onCreateItem)
            {
                var cateogry = new PaletteCategory();
                cateogry.name = name;
                cateogry.m_paletteItems = new();
                cateogry.m_flatPalletteItems = new();
                
                // Build a filter string with multiple types: "t:Prefab t:Material t:Texture"
                string filter = string.Join(" ", assetTypes.Select(type => $"t:{type}"));
                string[] guids = AssetDatabase.FindAssets(filter);
                
                var assets = guids.Select(guid => AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid))).ToList();
                foreach (var asset in assets)
                {
                    if (asset == null) continue;
                    var paletteItem = onCreateItem(asset);
                    if (!paletteItem.IsValid) continue;
                    if (!cateogry.m_paletteItems.ContainsKey(paletteItem.subcategory))
                        cateogry.m_paletteItems[paletteItem.subcategory] = new List<PaletteItem>();
                    cateogry.m_paletteItems[paletteItem.subcategory].Add(paletteItem);
                    cateogry.m_flatPalletteItems.Add(paletteItem);
                }
                
                return cateogry;
            }
        }
        
        private struct PaletteItem
        {
            public string subcategory;
            public string name;
            public Object asset;

            public bool IsValid => asset != null;

            public static PaletteItem Create(string name, Object asset)
            {
                var item = new PaletteItem
                {
                    subcategory = "Default",
                    name = name,
                    asset = asset,
                };
                return item;
            }

            public void Draw(EditorWindow editor, Rect rect)
            {
                var thumb = AssetThumbnail.GetThumbnail(asset, editor);
                var vizRect = rect;
                vizRect.height -= 4f;
                vizRect.width -= 4f;
                vizRect.x += 2f;
                vizRect.y += 2f;

                //GUI.color = AssetThumbnail.GetThumbnailTint(item.asset, this);
                GUI.DrawTexture(vizRect, thumb);
            
                // Draw asset name
                var labelRect = new Rect(vizRect.x, vizRect.yMax - 20f, vizRect.width, 20f);
                EditorGUI.DrawRect(labelRect, new Color(0,0,0,0.2f));
                labelRect.y -= labelRect.height / 4;
                EditorGUI.DropShadowLabel(labelRect, name);
            
                GUILayout.EndVertical();
            }
        }

        private PaletteCategory m_activeCategory;
        private List<PaletteCategory> m_paletteCategories = new List<PaletteCategory>();
        private bool m_displaySubCategories = true;
        private Dictionary<string, bool> m_subCategoryToggles = new Dictionary<string, bool>();
        private List<PaletteItem> m_filteredItems = new List<PaletteItem>();
        
        private void OnEnable()
        {
            titleContent = new GUIContent("Level Design Palette", EditorIcons.FileCabinet.Raw);
            InitPaletteAssets();

            EditorApplication.update += Update;
        }

        private void Update()
        {
            // Repaint every 10 frames
            if (Time.frameCount % 10 == 0)
                Repaint();
        }
        
        private void OnDisable()
        {
            EditorApplication.update -= Update;
            //AssetThumbnail.ClearCache();
        }

        private void InitPaletteAssets()
        {
            m_paletteCategories ??= new();
            m_paletteCategories.Clear();

            var levelObjectsPalette = PaletteCategory.Create("Level Objects", new []{"Prefab"}, CreateLevelObjectPaletteItem);
            var materialsPalette = PaletteCategory.Create("Materials", new []{"Material"}, CreateMaterialPaletteItem);
            var skatesPalette = PaletteCategory.Create("Skating", new []{"Prefab", "ScriptableObject"}, CreateSkatesPaletteItem);
            var meshesPalette = PaletteCategory.Create("Meshes", new[] { "Mesh" }, CreateMeshPaletteItem);
            
            m_paletteCategories.Add(materialsPalette);
            m_paletteCategories.Add(skatesPalette);
            m_paletteCategories.Add(levelObjectsPalette);
            m_paletteCategories.Add(meshesPalette);
            
            // Init active category to anything
            m_activeCategory = levelObjectsPalette;
        }
        
        private void UpdateFilteredPalette()
        {
            if (string.IsNullOrWhiteSpace(m_filterText))
            {
                //InitPaletteAssets();
                return;
            }
            
            m_filteredItems.Clear();
            foreach (var category in m_paletteCategories)
            {
                foreach (var subCategory in category.m_paletteItems)
                {
                    foreach (var item in subCategory.Value)
                    {
                        // Basic string filtering
                        if (item.name.IndexOf(m_filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            m_filteredItems.Add(item);
                        }
                    }
                }
            }
        }

        private Vector2 m_scrollPos;

        private float m_thumbnailSize = 128f;
        private float m_padding = 10f;

        private string m_filterText = string.Empty;

        private bool m_mouseOutsideOfPalette;
        
        private void OnGUI()
        {
            // If we're moving the mouse, repaint to update hover states
            if (Event.current.type == EventType.MouseMove)
                Repaint();
            
            FreeSkiesEditor.DrawLogo(150, GUIStyles.GUIStyles.HelpBox, new Color(0,0,0,0));
            
            GUILayout.Space(10f);
            
            GUILayout.BeginVertical(GUIStyles.GUIStyles.HelpBox);
            {
                GUILayout.Space(15f);
                EditorGUI.BeginChangeCheck();
                m_filterText = SirenixEditorGUI.ToolbarSearchField(m_filterText);
                if (EditorGUI.EndChangeCheck())
                    UpdateFilteredPalette();
                
                GUILayout.BeginHorizontal();
                {
                    // For newly added assets
                    if (GUILayout.Button("Refresh Palette"))
                        InitPaletteAssets();

                    FreeSkiesEditor.ToggleButton("Display Sub-Categories", ref m_displaySubCategories, options:GUILayout.Width(192f));
                }
                GUILayout.EndHorizontal();

            }
            GUILayout.EndVertical();
            
            // Category toolbar
            GUILayout.BeginHorizontal(GUIStyles.GUIStyles.HelpBox);
            {
                foreach (var category in m_paletteCategories)
                {
                    bool isActive = category.name.Equals(m_activeCategory.name);
                    FreeSkiesEditor.ToggleButton(category.name, ref isActive);
                    if (isActive) m_activeCategory = category;
                }
            }
            GUILayout.EndHorizontal();
            
            bool showAll = !string.IsNullOrWhiteSpace(m_filterText);
            
            float screenMin = position.yMin + 100;
            float screemMax = position.yMax - 112;
            m_mouseOutsideOfPalette = Event.current.mousePosition.y < screenMin || Event.current.mousePosition.y > screemMax;
            
            m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos, GUIStyles.GUIStyles.HelpBox);
            {
                // Sub-category toolbar toggle TODO: Maybe later
                
                
                int columns = (int)Mathf.Max(1, 1 + (int)(position.width - 20) / (m_thumbnailSize + m_padding));

                if (showAll || !m_displaySubCategories)
                {
                    var palleteItems = showAll ? m_filteredItems : m_activeCategory.m_flatPalletteItems;
                    
                    int rows = Mathf.CeilToInt((float)palleteItems.Count / columns);
                    
                    for (int row = 0; row < rows; row++)
                    {
                        EditorGUILayout.BeginHorizontal();

                        for (int col = 0; col < columns; col++)
                        {
                            int idx = row * columns + col;
                            if (idx >= palleteItems.Count) break;
                            var item = palleteItems[idx];

                            DrawPaletteItem(item);
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    foreach (var subCategory in m_activeCategory.m_paletteItems)
                    {
                        // Underlined drop-shadow label
                        GUILayout.Space(10f);
                        var labelStyle = new GUIStyle(EditorStyles.whiteBoldLabel);
                        labelStyle.alignment = TextAnchor.MiddleLeft;
                        labelStyle.fontSize += 4;
                        // Width based on text
                        var labelRect = GUILayoutUtility.GetRect(new GUIContent(subCategory.Key), labelStyle);
                        // Draw like this faded header rect
                        SirenixEditorGUI.DrawSolidRect(labelRect, Color.gray2);
                        EditorGUI.DropShadowLabel(labelRect, subCategory.Key, labelStyle);

                        GUILayout.Space(4);
                        
                        int rows = Mathf.CeilToInt((float)subCategory.Value.Count / columns);
                        for (int row = 0; row < rows; row++)
                        {
                            EditorGUILayout.BeginHorizontal();

                            for (int col = 0; col < columns; col++)
                            {
                                int idx = row * columns + col;
                                if (idx >= subCategory.Value.Count) break;
                                var item = subCategory.Value[idx];

                                DrawPaletteItem(item);
                            }

                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndScrollView();
            
            //GUILayout.FlexibleSpace();
            
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // Slide for zoom scale
            var width = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 32f;
            m_thumbnailSize = EditorGUILayout.Slider(new GUIContent(EditorIcons.MagnifyingGlass.Raw),
                m_thumbnailSize, 64f, 512f, GUILayout.MaxWidth(256));
            // Ensure thumbnail size is a power of 2
            m_thumbnailSize = Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(m_thumbnailSize));
            EditorGUIUtility.labelWidth = width;
            
            GUILayout.EndHorizontal();
        }

        private void DrawPaletteItem(PaletteItem item)
        {
            var thumbnailRect = new Rect(0, 0, m_thumbnailSize, m_thumbnailSize);
            
            GUILayout.BeginVertical(GUILayout.Width(thumbnailRect.width), GUILayout.Height(thumbnailRect.height));
            
            thumbnailRect = GUILayoutUtility.GetRect(thumbnailRect.width, thumbnailRect.height);
            
            var currentEvt = Event.current;
            
            if (currentEvt != null && thumbnailRect.Contains(currentEvt.mousePosition))// TODO: this check is bugged && !m_mouseOutsideOfPalette) // Also ensure mouse isn't below the scroll view region padding
            {
                Repaint();
                
                if (currentEvt.type == EventType.MouseDrag)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new Object[] { item.asset };
                    DragAndDrop.StartDrag(item.name);
                    currentEvt.Use();
                }
                else if (currentEvt.type == EventType.MouseDown && currentEvt.clickCount == 2)
                {
                    EditorGUIUtility.PingObject(item.asset);
                    currentEvt.Use();
                }
                SirenixEditorGUI.DrawRoundRect(thumbnailRect, Color.dodgerBlue, 4);
            }
            else
            {
                SirenixEditorGUI.DrawRoundRect(thumbnailRect, Color.black, 2);
            }

            item.Draw(this, thumbnailRect);
        }
        
        #region Individual Palette Item Creators
        private PaletteItem CreateMeshPaletteItem(Object arg)
        {
            if (arg is not Mesh mesh) return default;
            
            // Get asset labels
            string[] labels = AssetDatabase.GetLabels(mesh);
            
            // Get the MODEL asset instead of the mesh sub-asset for better previews
            string assetPath = AssetDatabase.GetAssetPath(mesh);
            Object modelAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            
            var paletteItem = PaletteItem.Create(mesh.name, modelAsset);
            var subCategory = paletteItem.subcategory;
            if (labels.Length > 0)
                subCategory = labels[0];
            paletteItem.subcategory = subCategory; // Use first label as sub-category or the original default one
            
            return paletteItem;
        }

        private PaletteItem CreateLevelObjectPaletteItem(Object obj)
        {
            if (obj is not GameObject go) return default;
            
            // Does it have a LevelObjectBase component?
            if (!go.TryGetComponent<LevelObjectBase>(out var levelObject)) return default;
            
            var paletteItem = PaletteItem.Create(go.name, go);
            
            // Try getting sub-category from LevelDesignCategory attribute
            var category = levelObject.GetType().GetCustomAttributes(typeof(LevelDesignCategoryAttribute), true).FirstOrDefault() as LevelDesignCategoryAttribute;
            string subCategory = paletteItem.subcategory;

            if (category != null)
                subCategory = category.CategoryName.Split('/').Last();

            paletteItem.subcategory = subCategory;
            
            return paletteItem;
        }

        private PaletteItem CreateMaterialPaletteItem(Object obj)
        {
            if (obj is not Material mat) return default;
            
            var paletteItem = PaletteItem.Create(mat.name, mat);
            var shaderName = mat.shader != null ? mat.shader.name : "Unknown";
            paletteItem.subcategory = shaderName;
            return paletteItem;
        }
        
        private PaletteItem CreateSkatesPaletteItem(Object obj)
        {
            bool IsValidSkateItem(int layer) => layer == PhysicsLayers.Vert || layer == PhysicsLayers.RailGrind || layer == PhysicsLayers.WallSlide;
            
            if (obj is GameObject go)
            {
                // Does it have vert/wallslide/railgrind physics layers? If so we dont consider it
                if (go.GetComponentInChildren<RailGrindSplineProvider>() == null) // special case rn
                    if (!IsValidSkateItem(go.layer)) return default;

                string layerName = LayerMask.LayerToName(go.layer);
                var paletteItem = PaletteItem.Create(obj.name, obj);
                paletteItem.subcategory = layerName;
                return paletteItem;
            }
            
            if (obj is MeshProfileConfig meshProfile)
            {
                if (!IsValidSkateItem(LayerMask.NameToLayer(meshProfile.m_defaultPhysicsLayer))) return default;
                
                var paletteItem = PaletteItem.Create(obj.name, obj);
                paletteItem.subcategory = meshProfile.m_defaultPhysicsLayer;
                return paletteItem;
            }

            return default;
        }
        #endregion
    }
}