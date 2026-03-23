using System;
using System.Collections.Generic;
using System.Reflection;
using FluffyUnderware.Curvy;
using UnityEditor;
using UnityEngine;

namespace FS.Editor
{
    /// <summary>
    /// Applies [Icon] attribute icons to MonoScripts that Unity fails to handle natively.
    /// </summary>
    [InitializeOnLoad]
    static class ScriptIconHandler
    {
        // Manual icon overrides for types we can't add [Icon] to
        static readonly Dictionary<Type, string> _manualIcons = new Dictionary<Type, string>
        {
            { typeof(CurvySpline), "Packages/com.unity.splines/Editor/Editor Resources/Icons/SplineComponent.png" },
            { typeof(Player.Player), "CharacterLayer Icon"}
            // Add more as needed
        };
        
        static ScriptIconHandler()
        {
            var typesWithIcon = new HashSet<System.Type>(
                TypeCache.GetTypesWithAttribute<IconAttribute>());
            
            foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                Texture2D icon = null;
                
                var type = script.GetClass();
                if (type == null) continue;
                if (!typesWithIcon.Contains(type) && !_manualIcons.ContainsKey(type))
                    continue;

                // Skip if already has an icon applied
                if (EditorGUIUtility.GetIconForObject(script) != null)
                    continue;

                string iconPath = null;
                var iconAttr = type.GetCustomAttribute<IconAttribute>();
                if (iconAttr == null) _manualIcons.TryGetValue(type, out iconPath);
                else iconPath = iconAttr.path;

                if (iconPath == null) continue;

                icon = EditorGUIUtility.IconContent(iconPath)?.image as Texture2D;
                if (icon == null) continue;

                EditorGUIUtility.SetIconForObject(script, icon);
            }
        }
    }

    /// <summary>
    /// Provides Unity editor background colors for hierarchy row states.
    /// Sourced from decompiled Unity internals.
    /// </summary>
    public static class UnityEditorBackgroundColor
    {
        static readonly Color k_defaultColor = new Color(0.7843f, 0.7843f, 0.7843f);
        static readonly Color k_defaultProColor = new Color(0.2196f, 0.2196f, 0.2196f);

        static readonly Color k_selectedColor = new Color(0.22745f, 0.447f, 0.6902f);
        static readonly Color k_selectedProColor = new Color(0.1725f, 0.3647f, 0.5294f);

        static readonly Color k_selectedUnFocusedColor = new Color(0.68f, 0.68f, 0.68f);
        static readonly Color k_selectedUnFocusedProColor = new Color(0.3f, 0.3f, 0.3f);

        static readonly Color k_hoveredColor = new Color(0.698f, 0.698f, 0.698f);
        static readonly Color k_hoveredProColor = new Color(0.2706f, 0.2706f, 0.2706f);

        public static Color Get(bool isSelected, bool isHovered, bool isWindowFocused)
        {
            if (isSelected)
            {
                if (isWindowFocused)
                    return EditorGUIUtility.isProSkin ? k_selectedProColor : k_selectedColor;
                else
                    return EditorGUIUtility.isProSkin ? k_selectedUnFocusedProColor : k_selectedUnFocusedColor;
            }
            else if (isHovered)
            {
                return EditorGUIUtility.isProSkin ? k_hoveredProColor : k_hoveredColor;
            }
            else
            {
                return EditorGUIUtility.isProSkin ? k_defaultProColor : k_defaultColor;
            }
        }
    }

    /// <summary>
    /// Custom hierarchy window renderer. Replaces default icons with component-based icons,
    /// adds zebra striping, prefab badges, and proper text coloring for various GO states.
    /// NOTE: Only works with the legacy (IMGUI) hierarchy. Unity 6.3's new hierarchy (UI Toolkit)
    /// does not support hierarchyWindowItemOnGUI - a public API is expected in a future release.
    /// </summary>
    [InitializeOnLoad]
    public static class HierarchyIcons
    {
        // Icon cache: keyed by type name or special identifier
        private static readonly Dictionary<string, GUIContent> _iconCache = new();
        
        // Separate int-keyed cache for per-instance object icons - avoids string allocation
        private static readonly Dictionary<int, GUIContent> _objectIconCache = new();
        
        // Cached default icon textures - resolved once, reused every frame
        private static Texture _gameObjectIcon;
        private static readonly Dictionary<PrefabAssetType, Texture> _prefabIcons = new();

        // Reusable GUIContent to avoid allocating per-row. Never cache this - it's mutated each draw.
        private static readonly GUIContent _drawContent = new();

        // Reusable list to avoid GetComponents allocations
        private static readonly List<MonoBehaviour> _monoBehaviourBuffer = new();
        private static readonly List<Component> _componentBuffer = new();

        private static EditorWindow _hierarchyWindow;
        private static bool _hierarchyHasFocus;
        
        #region Styles

        private static GUIStyle _normalStyle;
        private static GUIStyle _normalInactiveStyle;
        private static GUIStyle _prefabStyle;
        private static GUIStyle _prefabInactiveStyle;
        private static GUIStyle _selectedStyle;
        private static GUIStyle _brokenStyle;

        // Standard label
        private static GUIStyle NormalStyle => _normalStyle ??= new GUIStyle(EditorStyles.label);

        // Dimmed for inactive GameObjects
        private static GUIStyle NormalInactiveStyle => _normalInactiveStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(1f, 1f, 1f, 0.4f) },
        };

        // Prefab blue text
        private static GUIStyle PrefabStyleInternal => _prefabStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.48f, 0.67f, 0.95f, 1f) },
            focused = { textColor = new Color(0.48f, 0.67f, 0.95f, 1f) },
        };

        // Prefab blue text, dimmed for inactive
        private static GUIStyle PrefabInactiveStyle => _prefabInactiveStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.48f, 0.67f, 0.95f, 0.4f) },
            focused = { textColor = new Color(0.48f, 0.67f, 0.95f, 0.4f) },
        };

        // White text for selected items (overrides prefab/normal coloring so label is legible)
        private static GUIStyle SelectedStyle => _selectedStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = Color.white },
            focused = { textColor = Color.white },
        };

        // Warning style for broken/missing prefabs
        private static GUIStyle BrokenStyle => _brokenStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(1f, 0.35f, 0.35f, 1f) },
        };

        #endregion

        static HierarchyIcons()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged += OnSelectionChanged;
        }

        /// <summary> Cache of selection IDs in hashset for O(1) lookup </summary>
        private static HashSet<int> _selectionSet = new();
        private static int _mouseDownInstanceID = -1;
        
        private static void OnSelectionChanged()
        {
            _selectionSet.Clear();
            foreach (int id in Selection.entityIds)
                _selectionSet.Add(id);
            _mouseDownInstanceID = -1;
        }

        private static void OnEditorUpdate()
        {
            _hierarchyHasFocus = EditorWindow.focusedWindow != null
                                 && EditorWindow.focusedWindow == _hierarchyWindow;
        }

        static void OnHierarchyGUI(int instanceID, Rect rect)
        {
            var go = EditorUtility.EntityIdToObject(instanceID) as GameObject;
            if (go == null) return;
            
            // Fetch here as it guarantees it exists
            if (_hierarchyWindow == null)
            {
                var windowType = System.Type.GetType("UnityEditor.SceneHierarchyWindow,UnityEditor");
                if (windowType != null)
                    _hierarchyWindow = EditorWindow.GetWindow(windowType);
            }
            
            Rect selectionRect = rect;
            selectionRect.x = 0;
            selectionRect.width = EditorGUIUtility.currentViewWidth;
            bool isHovering = selectionRect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && isHovering)
                _mouseDownInstanceID = instanceID;
            bool isSelected = _selectionSet.Contains(instanceID) || _mouseDownInstanceID == instanceID;

            // --- Draw Toggle (shift according to fold-out to not draw above) ---
            // Only draw toggle if either we're hovering over the row, its selected, or inactive
            if (!go.activeSelf || isHovering || isSelected)
            {
                Rect activeToggleRect = rect;
                bool anyChildren = go.transform.childCount > 0;
                activeToggleRect.x = rect.x - 14.5f * 
                    (anyChildren && go.transform.GetChild(0).gameObject.hideFlags == 0 
                        ? 2 : 1); // always left of where foldout would be
                activeToggleRect.width = 18.5f;

                EditorGUI.BeginChangeCheck();
                bool newActiveState = EditorGUI.Toggle(activeToggleRect, go.activeSelf);
                if (EditorGUI.EndChangeCheck()) go.SetActive(newActiveState);
            }
            
            // Everything else only matters on Repaint
            if (Event.current.type != EventType.Repaint) return;

            DrawBreadcrumbs(go, rect);
            
            // Cache prefab status once per item - used for style, badge, and fallback icon
            bool isPrefab = PrefabUtility.IsPartOfAnyPrefab(go);
            PrefabAssetType prefabType = isPrefab
                ? PrefabUtility.GetPrefabAssetType(go)
                : PrefabAssetType.NotAPrefab;

            // --- Background / zebra striping ---
            Color bgColor = DrawBackground(rect, isSelected, isHovering);

            // --- Determine icon ---
            Texture iconImage = null;
            string tooltip = null;

            // 1. Check MonoBehaviour components for [Icon] attribute
            go.GetComponents(_monoBehaviourBuffer);
            foreach (var component in _monoBehaviourBuffer)
            {
                if (component == null) continue;
                var componentType = component.GetType();
                if (TryGetCachedIcon(componentType, out var cached))
                {
                    iconImage = cached.image;
                    tooltip = componentType.Name;
                    break;
                }
            }
            _monoBehaviourBuffer.Clear();

            // 2. Check layer-based icons
            if (iconImage == null)
            {
                if (TryGetLayerCachedIcon(go.layer, out var layerContent))
                {
                    iconImage = layerContent.image;
                    tooltip = LayerMask.LayerToName(go.layer);
                }
            }

            // 3. Fallback to most relevant component icon (Camera, Light, MeshRenderer, etc.)
            if (iconImage == null)
            {
                if (TryGetObjectCachedIcon(go, out var objContent))
                {
                    iconImage = objContent.image;
                }
            }

            // 4. Final fallback: prefab or default GO icon - resolve once and reuse for badge
            Texture defaultIcon = GetDefaultIcon(isPrefab, prefabType);
            if (iconImage == null)
            {
                iconImage = defaultIcon;
            }

            // --- Prepare draw content (reuse, never cache this) ---
            _drawContent.image = iconImage;
            _drawContent.text = go.name;
            _drawContent.tooltip = tooltip;

            // --- Draw icon background to cover the default one ---
            Rect iconBgRect = rect;
            iconBgRect.width = 18.5f;
            EditorGUI.DrawRect(iconBgRect, bgColor);

            // --- Draw label with icon ---
            GUIStyle style = GetStyle(isSelected, isPrefab, prefabType, go.activeInHierarchy);
            EditorGUI.LabelField(rect, _drawContent, style);

            // --- Draw prefab badge overlay (only when showing a custom icon, not the default prefab one) ---
            if (isPrefab && iconImage != defaultIcon && defaultIcon != null)
            {
                Rect badgeRect = new Rect(rect.x + 8, rect.y + 8, 8, 8);
                GUI.DrawTexture(badgeRect, defaultIcon);
            }
        }

        /// <summary>
        /// Resolves the appropriate GUIStyle based on selection, prefab, and active state.
        /// </summary>
        private static GUIStyle GetStyle(bool isSelected, bool isPrefab, PrefabAssetType prefabType, bool isActive)
        {
            // Selected overrides everything - needs white text to be legible on blue bg
            if (isSelected)
                return SelectedStyle;

            // Broken/missing prefab
            if (isPrefab && prefabType == PrefabAssetType.MissingAsset)
                return BrokenStyle;

            // Prefab
            if (isPrefab)
                return isActive ? PrefabStyleInternal : PrefabInactiveStyle;

            // Normal
            return isActive ? NormalStyle : NormalInactiveStyle;
        }

        #region Background Drawing

        private static Color DrawBackground(Rect rect, bool isSelected, bool isHovering)
        {
            // Preserve foldout area - start just before the icon
            rect.x = rect.x - 2;
            //rect.xMax = EditorGUIUtility.currentViewWidth;
            
            Color color = UnityEditorBackgroundColor.Get(isSelected, isHovering, _hierarchyHasFocus);

            // Zebra stripe: derive from row position for stability across events
            int row = Mathf.RoundToInt(rect.y / rect.height);
            if (row % 2 == 1 && !isSelected)
                color *= new Color(0.8f, 0.8f, 0.8f, 1f);

            EditorGUI.DrawRect(rect, color);

            return color;
        }

        #endregion

        #region Icon Fetchers

        /// <summary>
        /// Checks for [Icon] attribute on the type. Results are cached by type name.
        /// </summary>
        static bool TryGetCachedIcon(System.Type type, out GUIContent iconContent)
        {
            iconContent = null;
            string key = type.FullName ?? type.Name;

            if (_iconCache.TryGetValue(key, out iconContent))
                return iconContent != null && iconContent.image != null;

            var attr = type.GetCustomAttribute<IconAttribute>();
            if (attr == null)
            {
                _iconCache[key] = null;
                return false;
            }

            // Clone - IconContent returns a shared instance that Unity reuses
            var shared = EditorGUIUtility.IconContent(attr.path);
            iconContent = shared != null ? new GUIContent(shared) : null;
            _iconCache[key] = iconContent;
            return iconContent != null && iconContent.image != null;
        }

        /// <summary>
        /// Checks for a layer-based icon named "[LayerName]Layer Icon" in Editor Default Resources.
        /// </summary>
        static bool TryGetLayerCachedIcon(int layerIdx, out GUIContent iconContent)
        {
            iconContent = null;
            var layerName = LayerMask.LayerToName(layerIdx);
            if (string.IsNullOrEmpty(layerName) || layerName.Equals("Character")) return false;

            var iconName = $"{layerName}Layer Icon";
            if (_iconCache.TryGetValue(iconName, out iconContent))
                return iconContent != null && iconContent.image != null;

            // Clone - IconContent returns a shared instance
            var shared = EditorGUIUtility.IconContent(iconName);
            iconContent = (shared != null && shared.image != null) ? new GUIContent(shared) : null;
            _iconCache[iconName] = iconContent;
            return iconContent != null && iconContent.image != null;
        }

        /// <summary>
        /// Priority list for fallback component icons. Lower = higher priority.
        /// </summary>
        static int GetComponentPriority(System.Type type)
        {
            if (typeof(Camera).IsAssignableFrom(type)) return 0;
            if (typeof(Light).IsAssignableFrom(type)) return 1;
            if (typeof(Animator).IsAssignableFrom(type)) return 2;
            if (typeof(SkinnedMeshRenderer).IsAssignableFrom(type)) return 3;
            if (typeof(MeshRenderer).IsAssignableFrom(type)) return 4;
            if (typeof(SpriteRenderer).IsAssignableFrom(type)) return 5;
            if (typeof(Collider).IsAssignableFrom(type)) return 6;
            if (typeof(Rigidbody).IsAssignableFrom(type)) return 7;
            if (typeof(AudioSource).IsAssignableFrom(type)) return 8;
            if (typeof(Canvas).IsAssignableFrom(type)) return 9;
            if (typeof(RectTransform).IsAssignableFrom(type)) return 10;
            if (typeof(TrailRenderer).IsAssignableFrom(type)) return 11;
            if (typeof(CurvySpline).IsAssignableFrom(type)) return 12;
            if (typeof(Player.Player).IsAssignableFrom(type)) return 13;
            if (typeof(Transform).IsAssignableFrom(type)) return 14;
            return int.MaxValue;
        }

        /// <summary>
        /// Picks the highest-priority built-in component on the GO and uses its icon.
        /// Cached per instance ID (int-keyed to avoid string allocation).
        /// </summary>
        static bool TryGetObjectCachedIcon(GameObject go, out GUIContent iconContent)
        {
            iconContent = null;
            int goID = go.GetInstanceID();

            if (_objectIconCache.TryGetValue(goID, out iconContent))
                return iconContent != null && iconContent.image != null;

            // Use non-allocating GetComponents
            go.GetComponents(_componentBuffer);

            Component best = null;
            int bestPriority = int.MaxValue;

            foreach (var component in _componentBuffer)
            {
                if (component == null) continue;
                int p = GetComponentPriority(component.GetType());
                if (p < bestPriority)
                {
                    bestPriority = p;
                    best = component;
                }
            }

            _componentBuffer.Clear();

            if (best == null || bestPriority == int.MaxValue)
            {
                _objectIconCache[goID] = null;
                return false;
            }

            // Clone - ObjectContent returns a shared instance
            var shared = EditorGUIUtility.ObjectContent(best, best.GetType());
            iconContent = new GUIContent(shared);
            _objectIconCache[goID] = iconContent;
            return iconContent.image != null;
        }

        /// <summary>
        /// Returns the default icon for a GameObject (cube, prefab, variant, model, or warning).
        /// Resolved once per icon type and cached for all subsequent frames.
        /// </summary>
        static Texture GetDefaultIcon(bool isPrefab, PrefabAssetType prefabType)
        {
            if (!isPrefab)
            {
                if (_gameObjectIcon == null)
                    _gameObjectIcon = EditorGUIUtility.IconContent("GameObject Icon")?.image;
                return _gameObjectIcon;
            }

            if (_prefabIcons.TryGetValue(prefabType, out var cached))
                return cached;

            string iconName = prefabType switch
            {
                PrefabAssetType.Model => "PrefabModel Icon",
                PrefabAssetType.Variant => "PrefabVariant Icon",
                PrefabAssetType.Regular => "Prefab Icon",
                PrefabAssetType.MissingAsset => "console.warnicon",
                _ => "Prefab Icon"
            };
            
            var icon = EditorGUIUtility.IconContent(iconName)?.image;
            _prefabIcons[prefabType] = icon;
            return icon;
        }

        #endregion
        
        static void DrawBreadcrumbs(GameObject go, Rect rect)
        {
            if (go.transform.parent == null && go.transform.childCount == 0) return;
            
            const float indentWidth = 14f;
            const float baseIndent = 60f; // approximate left edge for depth 0
    
            Color lineColor = new Color(1f, 1f, 1f, 0.25f);
    
            Transform current = go.transform;
            float x = rect.x - 8; // start just left of this item's icon
    
            // Draw horizontal line to this item
            float indentMul = current.childCount == 0 ? 1 : 0.6f;
            Rect hLine = new Rect(x - indentWidth, rect.y + rect.height * 0.5f, indentWidth * indentMul, 1);
            EditorGUI.DrawRect(hLine, lineColor);
    
            // Walk up the hierarchy, drawing vertical lines for each ancestor that has more siblings below
            Transform walk = current;
            float lineX = x - indentWidth;
    
            while (walk.parent != null)
            {
                bool isLastSibling = walk.GetSiblingIndex() == walk.parent.childCount - 1;
        
                if (!isLastSibling)
                {
                    // This ancestor has siblings below - draw a full vertical line at this depth
                    Rect vLine = new Rect(lineX, rect.y, 1, rect.height);
                    EditorGUI.DrawRect(vLine, lineColor);
                }
                else
                {
                    // Last sibling - draw half vertical line (top half only, ending at the horizontal)
                    Rect vLine = new Rect(lineX, rect.y, 1, rect.height * 0.5f);
                    EditorGUI.DrawRect(vLine, lineColor);
                }
        
                walk = walk.parent;
                lineX -= indentWidth;
            }
        }
    }
}