using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Drawing;
using FS.Animation;
using FS.CameraSystem;
using FS.Extensions;
using FS.GameplayActions;
using FS.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Base class for level objects that interact with PhysicsController actors via triggers or collisions. Convenient reference counting
/// to ensure triggers arent invoked more than needed, and helps cache the refs for objects that will function on multiple interactors,
/// </summary>
[SelectionBase]
public abstract class LevelObjectBase : MonoBehaviourGizmos
{
    // TODO: Alongside the PhysicsController, we can set "Oh active state is LevelObject control", we should have a way to cancel out of it
    protected static Dictionary<Transform, Context> s_contextCache = new();
    
    // Cache of triggered physics actors to avoid multiple calls due to multiple colliders
    protected List<int> m_onEnterDirtyTransforms = new();
    protected List<int> m_onStayDirtyTransforms = new();
    protected List<int> m_onExitDirtyTransforms = new();
    
    // Events to be implemented by derived classes
    protected virtual void OnPhysicsActorEnter(Context context) {}
    protected virtual void OnPhysicsActorStay(Context context) {}
    protected virtual void OnPhysicsActorExit(Context context) {}
    
    /// <summary>
    /// Override to perform cleanup when interaction ends (naturally or cancelled).
    /// Called exactly once per interaction.
    /// </summary>
    protected virtual void OnInteractionCleanup(Context context) {}
    
    /// <summary>
    /// Register this level object as controlling the physics actor.
    /// </summary>
    protected void BeginInteraction(Context context)
    {
        context.physics.SetLevelObject(this);
    }
    
    /// <summary>
    /// End the interaction and clean up. Can be called internally (natural end) or externally (cancellation).
    /// Safe to call multiple times - will only cleanup once.
    /// </summary>
    public void EndInteraction(Context context)
    {
        // Only cleanup if this level object is actually the active one
        if (context.physics == null || context.physics.ActiveLevelObject != this)
            return;
        
        // Clear any active coroutines
        EndInteractionCoroutine(context);
        
        // Do the cleanup
        OnInteractionCleanup(context);
        
        // Clear from physics controller (doesn't call back to us)
        context.physics.ClearLevelObjectInternal();
    }

    /// <summary>
    /// Start up a coroutine and assign it to the context, ensuring proper cleanup later on,
    /// specifically when it comes to disposing of the coroutine (on end or destroy) to guarantee
    /// execution of any finally blocks.
    /// </summary>
    protected void StartInteractionCoroutine(Context context, IEnumerator coroutine)
    {
        context.activeCoroutine = coroutine;
        StartCoroutine(context.activeCoroutine);
    }

    /// <summary>
    /// Clears up any active coroutine on the context. Calling dispose on them to ensure finally blocks
    /// </summary>
    protected void EndInteractionCoroutine(Context context)
    {
        var coroutine = context.activeCoroutine;
        // Clear reference first to avoid re-entrancy issues
        context.activeCoroutine = null;
        if (coroutine != null)
            this.StopAndDisposeCoroutine(coroutine);
    }

    private void OnDestroy()
    {
        // Clear up ALL coroutines in all active contexts
        foreach (var kvp in s_contextCache)
        {
            var context = kvp.Value;
            if (context.activeCoroutine != null)
            {
                this.StopAndDisposeCoroutine(context.activeCoroutine);
            }
        }
    }

    public class Context
    {
        public Transform transform { get; private set; }
        public PhysicsController physics { get; private set; }
        public Rigidbody rigidBody { get; private set; }
        public ActionController actionController { get; private set; }
        public FSAnimator animator { get; private set; }
        public RenderVisibilityController meshVisibilityController { get; private set; }
        public TransformInfluencer visualTransformInfluencer { get; private set; }
        public CameraBehaviorController cameraController { get; private set; }
        public Dictionary<string, ActionConstraintBase> actionConstraints = new();
        
        public IEnumerator activeCoroutine;

        public int ID => physics.GetInstanceID();
        public bool IsValid => physics != null; // Only component we must guarantee having

        public bool IsPlayer { get; private set; }
        public PlayerInputSystem PlayerInput { get; private set; }
        public PlayerCameraSystem PlayerCamera { get; private set; }
        
        public static Context Get(Transform transform)
        {
            if (s_contextCache.TryGetValue(transform, out Context context))
            {
                if (context.IsValid)
                    return context;
                
                // Invalid, clear cache entry
                s_contextCache.Remove(transform);
            }

            context = new Context
            {
                transform = transform,
                physics = transform.GetComponent<PhysicsController>(),
                rigidBody = transform.GetComponent<Rigidbody>(),
                actionController = transform.GetComponent<ActionController>(),
                animator = transform.GetComponentInChildren<FSAnimator>(),
                meshVisibilityController = transform.GetComponentInChildren<RenderVisibilityController>(),
                visualTransformInfluencer = transform.GetComponentInChildren<TransformInfluencer>(),
                cameraController = transform.GetComponent<CameraBehaviorController>(),
                IsPlayer = transform.GetComponentInParent<Player>(),
                PlayerInput = PlayerManager.GetSystem<PlayerInputSystem>(transform.gameObject),
                PlayerCamera = PlayerManager.GetSystem<PlayerCameraSystem>(transform.gameObject)
            };

            s_contextCache[transform] = context;
            return context;
        }
    }
    
    // Clear dirty lists each physics update TODO: Could we make this static?? Eh not really
    private void FixedUpdate()
    {
        if (m_onEnterDirtyTransforms.Count != 0)
            m_onEnterDirtyTransforms.Clear();

        if (m_onStayDirtyTransforms.Count != 0)
            m_onStayDirtyTransforms.Clear();

        if (m_onExitDirtyTransforms.Count != 0)
            m_onExitDirtyTransforms.Clear();
    }
    
    protected void ProcessAction(Transform targetTransform, List<int> contextIDList, Action<Context> Action)
    {
        if (!enabled)
            return;

        Context context = Context.Get(targetTransform);

        if (!context.IsValid)
            return;
        
        // It might not want to interact right now
        if (context.physics.ShouldBlockLevelObjects)
            return;
        
        // A level object is already controlling this actor, skip
        if (context.physics.HasLevelObject)
        {
            // In general, dont invoke events if another LO is is controler
            if (context.physics.ActiveLevelObject != this)
                return;
            // If Enter/Stay and, check if its us to avoid retrigger
            if (Action == OnPhysicsActorEnter && context.physics.ActiveLevelObject == this)
                return;
        }

        // We don't want to trigger the logic more than once due to multiple colliders interacting with the trigger.
        if (contextIDList.Contains(context.ID))
            return;

        // The actor is not registered, add the ID to the list.
        contextIDList.Add(context.ID);
        
        Action?.Invoke(context);
    }
    
    private void TryTriggerAction(Collider other, List<int> dirtyList, Action<Context> action)
    {
        var rigidBody = other.attachedRigidbody;
        if (rigidBody == null)
            return;
        ProcessAction(rigidBody.transform, dirtyList, action);
    }

    private void OnTriggerEnter(Collider other) => TryTriggerAction(other, m_onEnterDirtyTransforms, OnPhysicsActorEnter);
    private void OnTriggerStay(Collider other) => TryTriggerAction(other, m_onStayDirtyTransforms, OnPhysicsActorStay);
    private void OnTriggerExit(Collider other) => TryTriggerAction(other, m_onExitDirtyTransforms, OnPhysicsActorExit);

    private void OnCollisionEnter(Collision other) => TryTriggerAction(other.collider, m_onEnterDirtyTransforms, OnPhysicsActorEnter);
    private void OnCollisionStay(Collision other) => TryTriggerAction(other.collider, m_onStayDirtyTransforms, OnPhysicsActorStay);
    private void OnCollisionExit(Collision other) => TryTriggerAction(other.collider, m_onExitDirtyTransforms, OnPhysicsActorExit);
    
    #region Periodic Static Cache Cleanup

    private static CancellationTokenSource s_cleanupCancellation;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void InitCleanup()
    {
        // Clear cache and cancel any existing cleanup task
        s_contextCache.Clear();
        s_cleanupCancellation?.Cancel();
        s_cleanupCancellation?.Dispose();
        s_cleanupCancellation = new CancellationTokenSource();
        
        // Setup scene unload handler
        SceneManager.sceneUnloaded -= OnSceneUnloaded; // Remove old handler if exists
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        
        // Start cleanup loop
        _ = CleanupLoopAsync(s_cleanupCancellation.Token);
    }

    private static void OnSceneUnloaded(Scene scene) => s_contextCache.Clear();

    private static async Awaitable CleanupLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Awaitable.WaitForSecondsAsync(30f, ct);
                
                var deadKeys = s_contextCache
                    .Where(kvp => kvp.Value.physics == null)
                    .Select(kvp => kvp.Key)
                    .ToList();
        
                foreach (var key in deadKeys)
                    s_contextCache.Remove(key);
                
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (deadKeys.Count > 0)
                    Debug.Log($"[LevelObjectBase] Cleaned {deadKeys.Count} stale context(s)");
#endif
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LevelObjectBase] Cleanup loop error: {ex}");
        }
    }
    
    #endregion
}