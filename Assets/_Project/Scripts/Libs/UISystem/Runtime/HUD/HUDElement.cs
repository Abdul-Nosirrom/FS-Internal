using System;
using Animancer;
using UnityEngine;
using UltEvent = UltEvents.UltEvent;

namespace FS.UI
{
    /// <summary>
    /// Predefined anchor points for HUD element placement.
    /// These correspond to RectTransforms placed in the HUDPanel hierarchy.
    /// </summary>
    public enum HUDAnchor
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    /// <summary>
    /// Optional base class for HUD elements that need world-space tracking or self-removal.
    /// 
    /// <para>
    /// Elements don't need to inherit from this - plain UI prefabs work fine for static
    /// anchored elements. Use this base when you need:
    /// <list type="bullet">
    ///   <item>Automatic world-to-screen position tracking</item>
    ///   <item>Self-removal capability</item>
    /// </list>
    /// </para>
    /// </summary>
    public class HUDElement : MonoBehaviour
    {
        private RectTransform m_rectTransform;
        private HUDPanel m_ownerHUD;
        private Camera m_gameCamera;
        private RectTransform m_canvasRect;
        private SoloAnimation m_animator;

        private Transform m_worldTarget;
        private Vector3 m_localOffset;
        private bool m_isTrackingWorld;
        
        public Transform WorldTarget => m_worldTarget;

        protected RectTransform RectTransform
        {
            get
            {
                if (m_rectTransform == null)
                    m_rectTransform = GetComponent<RectTransform>();
                return m_rectTransform;
            }
        }

        /// <summary>
        /// Called by HUDPanel after instantiation to provide necessary references.
        /// </summary>
        internal void Initialize(HUDPanel owner, Camera gameCamera, RectTransform canvasRect)
        {
            m_ownerHUD = owner;
            m_gameCamera = gameCamera;
            m_canvasRect = canvasRect;
            m_animator = GetComponentInChildren<SoloAnimation>();
            
            Debug.Log($"[UI] Initializing HUDElement {name} for HUDPanel {owner.name}. [Owner: {owner.PanelName} | Camera: {gameCamera.gameObject.name}]", this);
            
            OnInitialized();
        }

        private void OnEnable() => Canvas.willRenderCanvases += UpdateWorldTracking;
        private void OnDisable() => Canvas.willRenderCanvases -= UpdateWorldTracking;

        /// <summary>
        /// Override to perform setup after initialization.
        /// </summary>
        protected virtual void OnInitialized() { }

        [Serializable]
        private struct HUDElementAnimation
        {
            /// <summary>Event fired when the animation completes.</summary>
            [SerializeField] public UltEvent m_onComplete;
            /// <summary>If true, plays the animation in reverse.</summary>
            [SerializeField] public bool m_reverse;
            /// <summary>The animator component to play. Can be null for no animation.</summary>
            [SerializeField] public AnimationClip m_animation;

            /// <summary>
            /// Plays the animation with configured settings.
            /// </summary>
            /// <param name="panel">The panel owning this animation (for focus blocking).</param>
            public async Awaitable Play(SoloAnimation animator)
            {
                if (m_animation != null && animator)
                {
                    // in this case we don't restart, start from the current time
                    bool isSameClipAndPlaying = animator.Clip == m_animation && animator.IsPlaying;

                    float animLength;
                    animator.Play(m_animation);
                    
                    if (m_reverse)
                    {
                        animator.Speed = -1f;
                        if (!isSameClipAndPlaying) animator.Time = m_animation.length;
                        animLength = animator.Time;
                    }
                    else
                    {
                        animator.Speed = 1f;
                        if (!isSameClipAndPlaying) animator.Time = 0f;
                        animLength = m_animation.length - animator.Time;
                    }

                    await Awaitable.WaitForSecondsAsync(animLength);
                }

                if (Application.isPlaying) m_onComplete?.Invoke();
            }
        }
        
        [SerializeField] private HUDElementAnimation m_showAnimation;
        [SerializeField] private HUDElementAnimation m_hideAnimation;

        public event Action OnShown;
        public event Action OnHidden;

        public bool IsHidden { get; private set; } = true;

        /// <summary>
        /// Called by HUDPanel after element is added.
        /// </summary>
        internal async Awaitable Show()
        {
            if (!IsHidden) return;
            
            IsHidden = false;
            gameObject.SetActive(true);
            await m_showAnimation.Play(m_animator);
            if (IsHidden) return; // hide was called while we waited on the anim
            OnShown?.Invoke();
        }

        /// <summary>
        /// Called by HUDPanel before element is removed.
        /// </summary>
        internal async Awaitable Hide(bool immediate = false)
        {
            if (IsHidden) return;
            
            IsHidden = true;
            if (!immediate) await m_hideAnimation.Play(m_animator);
            if (!IsHidden) return; // show was called while we waited on the anim
            OnHidden?.Invoke();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Configures this element to track a world-space target.
        /// Position updates automatically in Canvas.willRenderCanvases.
        /// </summary>
        /// <param name="target">The transform to track.</param>
        /// <param name="offset">Local Offset from the target's position.</param>
        public void TrackWorldTarget(Transform target, Vector3 offset = default)
        {
            m_worldTarget = target;
            m_localOffset = offset;
            m_isTrackingWorld = target != null;
        }

        /// <summary>
        /// Stops tracking and optionally sets a static screen position.
        /// </summary>
        public void StopTracking(Vector2? staticPosition = null)
        {
            m_isTrackingWorld = false;
            m_worldTarget = null;

            if (staticPosition.HasValue)
                RectTransform.anchoredPosition = staticPosition.Value;
        }

        /// <summary>
        /// Removes this element from the HUD and destroys it.
        /// </summary>
        public void Remove()
        {
            if (m_ownerHUD != null)
                m_ownerHUD.Remove(this);
            else
                Destroy(gameObject);
        }

        private void UpdateWorldTracking()
        {
            if (!m_isTrackingWorld || m_worldTarget == null || m_gameCamera == null)
                return;

            Vector3 worldPos = m_worldTarget.position + m_worldTarget.TransformVector(m_localOffset);
            // Vector3 screenPoint = m_gameCamera.WorldToScreenPoint(worldPos);
            //
            // // Behind camera - hide element
            // if (screenPoint.z < 0)
            // {
            //     //gameObject.SetActive(false);
            //     return;
            // }
            //
            // //gameObject.SetActive(true); TODO: This shouldnt be handling activity, just hide & unhide [or better yet screen borders]
            //
            // RectTransformUtility.ScreenPointToLocalPointInRectangle(
            //     m_canvasRect,
            //     screenPoint,
            //     m_gameCamera, // null for ScreenSpace Overlay
            //     out Vector2 localPoint
            // );
            
            WorldSpaceUIUtility.WorldToCanvasLocalPoint(m_gameCamera, m_canvasRect, worldPos, out Vector2 canvasPoint);
            float depth = WorldSpaceUIUtility.CameraDepth(m_gameCamera, worldPos);
            float scale = WorldSpaceUIUtility.WorldSizeToUIScale(m_gameCamera, m_canvasRect, 0.025f, depth);

            RectTransform.anchoredPosition = canvasPoint;
            RectTransform.localScale = Vector3.one * scale;
        }
    }
}