using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FS.UI
{
    /// <summary>
    /// Container panel for dynamic HUD elements. Sits at the bottom of the UI stack
    /// and provides anchor-based and world-space element placement.
    /// 
    /// <para>
    /// <b>Usage:</b>
    /// Push once during player initialization via PlayerUISystem. Access through
    /// <c>PlayerUISystem.HUD</c> to add/remove elements at runtime.
    /// </para>
    /// 
    /// <para>
    /// <b>Anchor System:</b>
    /// Assign RectTransforms in the inspector for each <see cref="HUDAnchor"/> position.
    /// Elements added to an anchor become children of that RectTransform.
    /// </para>
    /// 
    /// <para>
    /// <b>World Tracking:</b>
    /// Elements can track world-space positions. The HUD provides camera and canvas
    /// references; elements handle their own position updates.
    /// </para>
    /// </summary>
    public class HUDPanel : UIPanel
    {
        [Serializable]
        private struct AnchorMapping
        {
            public HUDAnchor Anchor;
            public RectTransform Transform;
        }

        [SerializeField, Title("Anchor Points")]
        [InfoBox("Assign RectTransforms for each anchor position where HUD elements can be placed.")]
        private AnchorMapping[] m_anchorMappings;

        [SerializeField, Title("Dimming")]
        [Tooltip("CanvasGroup used to dim the HUD when menus are open.")]
        private CanvasGroup m_canvasGroup;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Alpha value when HUD is dimmed (menu open above it).")]
        private float m_dimmedAlpha = 0.3f;

        private Dictionary<HUDAnchor, RectTransform> m_anchors;
        private HashSet<MonoBehaviour> m_activeElements = new();
        private RectTransform m_canvasRect;

        private void Awake()
        {
            BuildAnchorDictionary();
            CacheCanvasRect();
        }

        private void BuildAnchorDictionary()
        {
            m_anchors = new Dictionary<HUDAnchor, RectTransform>();

            if (m_anchorMappings == null) return;

            foreach (var mapping in m_anchorMappings)
            {
                if (mapping.Transform == null)
                {
                    Debug.LogWarning($"[HUD] Anchor '{mapping.Anchor}' has no transform assigned.");
                    continue;
                }

                if (m_anchors.ContainsKey(mapping.Anchor))
                {
                    Debug.LogWarning($"[HUD] Duplicate anchor mapping for '{mapping.Anchor}'.");
                    continue;
                }

                m_anchors[mapping.Anchor] = mapping.Transform;
            }
        }

        private void CacheCanvasRect()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                m_canvasRect = canvas.GetComponent<RectTransform>();
        }

        #region Add Methods

        /// <summary>
        /// Instantiates an element prefab at the specified anchor point.
        /// </summary>
        /// <typeparam name="T">Component type on the prefab root.</typeparam>
        /// <param name="prefab">The prefab to instantiate.</param>
        /// <param name="anchor">The anchor point to parent the element to.</param>
        /// <returns>The instantiated component, or null if anchor doesn't exist.</returns>
        public T Add<T>(T prefab, HUDAnchor anchor) where T : MonoBehaviour
        {
            if (!TryGetAnchor(anchor, out var anchorTransform))
                return null;

            var instance = Instantiate(prefab, anchorTransform);
            RegisterElement(instance);
            InitializeIfHUDElement(instance);
            ShowIfHUDElement(instance);

            return instance;
        }

        /// <summary>
        /// Instantiates an element prefab that tracks a world-space target.
        /// </summary>
        /// <typeparam name="T">Component type on the prefab root.</typeparam>
        /// <param name="prefab">The prefab to instantiate.</param>
        /// <param name="worldTarget">The transform to track in world space.</param>
        /// <param name="offset">Offset from the target position.</param>
        /// <returns>The instantiated component.</returns>
        public T Add<T>(T prefab, Transform worldTarget, Vector3 offset = default) where T : MonoBehaviour
        {
            var instance = Instantiate(prefab, transform);
            RegisterElement(instance);
            InitializeIfHUDElement(instance);
            ShowIfHUDElement(instance);

            // If it's a HUDElement, set up tracking automatically
            if (instance is HUDElement hudElement)
            {
                hudElement.TrackWorldTarget(worldTarget, offset);
            }
            else
            {
                Debug.LogWarning($"[HUD] Element '{prefab.name}' added with world target but doesn't inherit from HUDElement. It won't track automatically.");
            }

            return instance;
        }

        /// <summary>
        /// Instantiates an element prefab at a specific anchored position.
        /// </summary>
        /// <typeparam name="T">Component type on the prefab root.</typeparam>
        /// <param name="prefab">The prefab to instantiate.</param>
        /// <param name="anchoredPosition">The position relative to the HUD panel's rect.</param>
        /// <returns>The instantiated component.</returns>
        public T Add<T>(T prefab, Vector2 anchoredPosition) where T : MonoBehaviour
        {
            var instance = Instantiate(prefab, transform);
            RegisterElement(instance);
            InitializeIfHUDElement(instance);
            ShowIfHUDElement(instance);

            var rectTransform = instance.GetComponent<RectTransform>();
            if (rectTransform != null)
                rectTransform.anchoredPosition = anchoredPosition;
            

            return instance;
        }
        
        private void ShowIfHUDElement(MonoBehaviour element)
        {
            if (element is HUDElement hudElement)
                _ = hudElement.Show();
        }

        #endregion

        #region Remove Methods

        /// <summary>
        /// Removes an element from the HUD and destroys it.
        /// </summary>
        public void Remove(MonoBehaviour element)
        {
            if (element == null) return;

            m_activeElements.Remove(element);
            if (element is HUDElement hudElement)
                _ = RemoveWithAnimation(hudElement);
            else
                Destroy(element.gameObject);
        }

        /// <summary>
        /// Removes all dynamic elements from the HUD.
        /// </summary>
        public void RemoveAll()
        {
            foreach (var element in m_activeElements)
            {
                if (element != null)
                    Destroy(element.gameObject);
            }

            m_activeElements.Clear();
        }
        
        private async Awaitable RemoveWithAnimation(HUDElement element)
        {
            await element.Hide();
            Destroy(element.gameObject);
        }

        #endregion

        #region Utility

        /// <summary>
        /// Converts a world position to canvas-local position.
        /// Useful for elements that handle their own positioning.
        /// </summary>
        /// <param name="worldPosition">Position in world space.</param>
        /// <param name="canvasPosition">Resulting position in canvas space.</param>
        /// <returns>True if the position is in front of the camera.</returns>
        public bool WorldToCanvasPosition(Vector3 worldPosition, out Vector2 canvasPosition)
        {
            canvasPosition = Vector2.zero;

            if (Context?.Camera == null || m_canvasRect == null)
                return false;

            Vector3 screenPoint = Context.Camera.WorldToScreenPoint(worldPosition);

            if (screenPoint.z < 0)
                return false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_canvasRect,
                screenPoint,
                null,
                out canvasPosition
            );

            return true;
        }

        /// <summary>
        /// Gets the RectTransform for a specific anchor point.
        /// </summary>
        public bool TryGetAnchor(HUDAnchor anchor, out RectTransform anchorTransform)
        {
            if (m_anchors != null && m_anchors.TryGetValue(anchor, out anchorTransform))
                return true;

            Debug.LogWarning($"[HUD] Anchor '{anchor}' not found.");
            anchorTransform = null;
            return false;
        }

        /// <summary>
        /// Checks if an element is currently active on this HUD.
        /// </summary>
        public bool Contains(MonoBehaviour element) => m_activeElements.Contains(element);

        /// <summary>
        /// Number of active dynamic elements.
        /// </summary>
        public int ElementCount => m_activeElements.Count;

        #endregion

        #region Lifecycle

        public override async Awaitable OnLostFocus()
        {
            await base.OnLostFocus();

            if (m_canvasGroup != null)
                m_canvasGroup.alpha = m_dimmedAlpha;
        }

        public override async Awaitable OnRegainedFocus()
        {
            await base.OnRegainedFocus();

            if (m_canvasGroup != null)
                m_canvasGroup.alpha = 1f;
        }

        #endregion

        #region Private Helpers

        private void RegisterElement(MonoBehaviour element)
        {
            m_activeElements.Add(element);
        }

        private void InitializeIfHUDElement(MonoBehaviour element)
        {
            if (element is HUDElement hudElement)
            {
                hudElement.Initialize(this, Context?.Camera, m_canvasRect);
            }
        }

        #endregion
    }
}