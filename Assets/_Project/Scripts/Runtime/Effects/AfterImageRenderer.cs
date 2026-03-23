using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using FS.Animation;
using PrimeTween;
using Sirenix.OdinInspector;
using TimeUtils;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// After-Image renderer component, allowing for starting after images rendering of the skinned mesh
/// this is attached on.
///
/// Supports:
/// - Custom material overrides for after images
/// - Pooling for performance
/// - Custom material value animation (unique materials or MPBs? Lots of materials to create as this is per-image-instance)
/// 
/// </summary>
[RequireComponent(typeof(Animator))]
public class AfterImageRenderer : MonoBehaviour
{
    /// <summary> The default after-image material, can be overridden in Start function </summary>
    [SerializeField] protected AfterImageSettings m_afterImageDefaultSettings;
    [SerializeField, AssetsOnly] protected GameObject m_afterImageModel;
    
    private AfterImageSettings? m_afterImageOverridenSettings;
    private AfterImageSettings Settings => m_afterImageOverridenSettings ?? m_afterImageDefaultSettings;

    /// <summary> Cache of mesh to mesh renderers as each instance upon activation needs to walk down the meshes and disable inactive ones on its instance </summary>
    private Dictionary<Mesh, SkinnedMeshRenderer> m_meshRenderers;

    private AfterImagePool m_pool;
    private AfterImagePool Pool
    {
        get
        {
            m_pool ??= new AfterImagePool(this);
            return m_pool;
        }
    }

    private TimeSince m_sinceLastAfterImage;
    
    // Profiler Markers
    private static readonly ProfilerMarker s_LateUpdateMarker = new ProfilerMarker("AfterImageRenderer.LateUpdate");
    private static readonly ProfilerMarker s_configureAfterImage = new ProfilerMarker("AfterImageRenderer.NewInstance");
    private static readonly ProfilerMarker s_copyPoseMarker = new ProfilerMarker("AfterImageRenderer.CopyPose");
    //
    
    private void Awake()
    {
        // Initialize mesh renderers dictionary so instances know which renderers to just hide upfront
        m_meshRenderers = new();
        var meshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var meshRenderer in meshRenderers)
        {
            m_meshRenderers.Add(meshRenderer.sharedMesh, meshRenderer);
        }
        Pool.SetSettings(Settings);
        m_sinceLastAfterImage = 0f;
        
        // Start disabled
        enabled = false;
    }

    public void StartAfterImages(AfterImageSettings? settings = null)
    {
        enabled = true;
        m_afterImageOverridenSettings = settings;
        Pool.SetSettings(Settings);
    }

    public void StopAfterImages()
    {
        m_afterImageOverridenSettings = null;
        enabled = false;
    }

    private void LateUpdate()
    {
        using var _ = s_LateUpdateMarker.Auto();
        
        // Add Instances
        if (m_sinceLastAfterImage >= Settings.frequency)
        {
            Pool.Get(); // Creates new instance
            m_sinceLastAfterImage = 0f;
        }
    }

    [Serializable]
    public struct AfterImageSettings
    {
        public Material material;
        public float frequency;
        public float lifeTime;

        public Ease fadeEase;
        
        public string fadeMaterialProperty;
        public float startValue;
        public float endValue;
    }

    protected class AfterImageInstance : IDisposable
    {
        private readonly GameObject m_root;
        private readonly SkinnedMeshRenderer[] m_meshRenderers;
        private readonly Dictionary<SkinnedMeshRenderer, Material[]> m_meshMaterials;
        private readonly AfterImageRenderer m_renderer;
        private readonly AfterImagePool m_pool;
        private readonly MaterialPropertyBlock m_mpb;
        
        /// <summary> Cache of bone pairs from a source to target transform, generated once per after-image instance </summary>
        private readonly (Transform source, Transform targetTransform)[] m_bonePairsToCopy;

        /// <summary> Cached callback from Tween.Delay just to be able to clean it up during reset </summary>
        private Tween m_resetCallback;

        /// <summary> Cached tweens for material animations, to ensure it's cleaned up upon reset </summary>
        private readonly Tween[] m_materialAnimatorTweens;

        public AfterImageInstance(AfterImageRenderer owner, AfterImagePool pool)
        {
            m_renderer = owner;
            m_root = GameObject.Instantiate(m_renderer.m_afterImageModel, m_renderer.transform);
            m_meshRenderers = m_root.GetComponentsInChildren<SkinnedMeshRenderer>();
            m_pool = pool;
            m_mpb = new MaterialPropertyBlock();

            m_materialAnimatorTweens = new Tween[m_meshRenderers.Length];
            m_meshMaterials = new();
            
            // Set material property blocks
            foreach (var renderer in m_meshRenderers)
            {
                renderer.SetPropertyBlock(m_mpb);
                m_meshMaterials.Add(renderer, new Material[renderer.sharedMaterials.Length]);
            }
            
            // Initialize bone-mapping cache
            var sourceTransforms = m_renderer.GetComponentsInChildren<Transform>();
            var targetLookup = m_root.GetComponentsInChildren<Transform>()
                .ToDictionary(t => t.name, t => t);
    
            m_bonePairsToCopy = sourceTransforms
                .Where(source => targetLookup.ContainsKey(source.name))
                .Select(source => (source, targetLookup[source.name]))
                .ToArray();
        }

        public void Reset()
        {
            // Release the after-image instance back to the pool. Disabling it and setting its parent to the renderer
            m_root.transform.SetParent(m_renderer.transform, false);
            m_root.transform.localPosition = Vector3.zero;
            m_root.transform.localRotation = Quaternion.identity;
            m_root.transform.localScale = Vector3.one;
            m_mpb.Clear();
            
            if (m_resetCallback.isAlive) m_resetCallback.Stop();
            foreach (var matTween in m_materialAnimatorTweens)
            {
                if (matTween.isAlive) matTween.Stop();
            }
            
            m_root.SetActive(false);
        }

        public void SyncTransform()
        {
            // Unparent then copy over current transform data
            m_root.transform.SetParent(null);
            
            m_root.transform.position = m_renderer.transform.position;
            m_root.transform.rotation = m_renderer.transform.rotation;
            m_root.transform.localScale = m_renderer.transform.localScale;
        }

        public void CopyPose()
        {
            using var _ = AfterImageRenderer.s_copyPoseMarker.Auto();
            
            foreach (var bonePair in m_bonePairsToCopy)
            {
                bonePair.targetTransform.localPosition = bonePair.source.localPosition;
                bonePair.targetTransform.localRotation = bonePair.source.localRotation;
                bonePair.targetTransform.localScale = bonePair.source.localScale;
            }
        }

        public void ApplySettings(AfterImageSettings settings)
        {
            int matProp = Shader.PropertyToID(settings.fadeMaterialProperty);

            for (int rIdx = 0; rIdx < m_meshRenderers.Length; rIdx++)
            {
                var renderer = m_meshRenderers[rIdx];
                
                // If renderer in owner is disabled, don't enable it here, so we wanna match it
                if (m_renderer.m_meshRenderers.TryGetValue(renderer.sharedMesh, out var ownerRenderer))
                {
                    renderer.gameObject.SetActive(ownerRenderer.gameObject.activeSelf);
                    renderer.enabled = ownerRenderer.enabled;
                }
                
                // If renderer has multiple mats, set it on everyone
                for (int m = 0; m < m_meshMaterials[renderer].Length; m++)
                    m_meshMaterials[renderer][m] = settings.material;
                renderer.sharedMaterials = m_meshMaterials[renderer];

                // Initialize MPB tween
                m_materialAnimatorTweens[rIdx] = Tween.Custom(settings.startValue, settings.endValue, settings.lifeTime, newVal =>
                {
                    m_mpb.SetFloat(matProp, newVal);
                    renderer.SetPropertyBlock(m_mpb);
                }, settings.fadeEase);
            }

            m_resetCallback = Tween.Delay(settings.lifeTime, ReleaseInstance);
        }

        public void Activate()
        {
            m_root.SetActive(true);
        }

        private void ReleaseInstance()
        {
            m_pool.Release(this);
        }

        public void Dispose()
        {
            if (Application.isPlaying) Destroy(m_root);
            else DestroyImmediate(m_root);
        }
    }
    
    protected class AfterImagePool
    {
        private readonly UnityEngine.Pool.ObjectPool<AfterImageInstance> m_pool;
        private readonly AfterImageRenderer m_renderer;
        private AfterImageSettings m_settings;
        
        public AfterImagePool(AfterImageRenderer owner, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 50)
        {
            m_renderer = owner;
            m_pool = new UnityEngine.Pool.ObjectPool<AfterImageInstance>(CreatePooledAfterImage, GetPooledAfterImage, ReleasePooledAfterImage, DestroyPooledAfterImage, collectionCheck, defaultCapacity, maxSize);
        }

        public void SetSettings(AfterImageSettings settings)
        {
            m_settings = settings;
        }
        
        private void ConfigureAfterImage(AfterImageInstance afterImage)
        {
            using var _ = AfterImageRenderer.s_configureAfterImage.Auto();
            
            // Set its transforms to match the owner
            afterImage.SyncTransform();
            
            // Copy Pose
            afterImage.CopyPose();

            // Set material
            afterImage.ApplySettings(m_settings);

            // Set tweens/fade animations
            afterImage.Activate();
        }

        #region Pooling
        public AfterImageInstance Get() => m_pool.Get();
        public void Release(AfterImageInstance afterImage) => m_pool.Release(afterImage);
        public void Dispose() => m_pool.Dispose();
        public int CountAll => m_pool.CountAll;
        public int CountActive => m_pool.CountActive;
        public int CountInactive => m_pool.CountInactive;
    
        private AfterImageInstance CreatePooledAfterImage()
        {
            var afterImage = new AfterImageInstance(m_renderer, this);
            ConfigureAfterImage(afterImage);
            return afterImage;
        }
        
        private void GetPooledAfterImage(AfterImageInstance afterImage) => ConfigureAfterImage(afterImage);

        private void ReleasePooledAfterImage(AfterImageInstance afterImage) => afterImage.Reset();
        
        private void DestroyPooledAfterImage(AfterImageInstance afterImage) => afterImage.Dispose();
        
        #endregion
    }
}

#region Animation Event

[Serializable]
[EventPath("VFX")]
public class AfterImageEvent : IAnimationEvent
{
    public string Name => "AfterImage";
    public bool IsRangedEvent => true;

    [SerializeField] private bool overrideSettings = false;

    [SerializeField, ShowIf("overrideSettings")]
    private AfterImageRenderer.AfterImageSettings settings;
    
    private static readonly ConditionalWeakTable<GameObject, AfterImageRenderer> s_cache = new();

    private static bool TryGetAfterImageRenderer(GameObject go, out AfterImageRenderer renderer)
    {
        if (s_cache.TryGetValue(go, out renderer)) return true;
        
        renderer = go.GetComponentInChildren<AfterImageRenderer>() 
                   ?? go.GetComponentInParent<AfterImageRenderer>();
        
        if (renderer == null && go.scene.isLoaded)
            return false;
            
        if (renderer != null) s_cache.Add(go, renderer);
        return renderer != null;
    }

    public void Start(GameObject context)
    {
        if (TryGetAfterImageRenderer(context, out var renderer)) renderer.StartAfterImages(overrideSettings ? settings : null);
    }

    public void End(GameObject context)
    {
        if (TryGetAfterImageRenderer(context, out var renderer)) renderer.StopAfterImages();
    }
}

#endregion