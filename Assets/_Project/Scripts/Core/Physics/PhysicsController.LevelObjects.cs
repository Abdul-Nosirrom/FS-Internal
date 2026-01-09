using UnityEngine;

public partial class PhysicsController
{
    public bool HasLevelObject => ActiveLevelObject != null;
    public LevelObjectBase ActiveLevelObject { get; private set; }
    
    private bool m_areLevelObjectsBlocked;

    public bool ShouldBlockLevelObjects
    {
        get => m_areLevelObjectsBlocked;
        set
        {
            if (value && HasLevelObject) CancelLevelObjectInteraction();
            m_areLevelObjectsBlocked = value;
        }
    }
    
    public void SetLevelObject(LevelObjectBase levelObject)
    {
        // For how we're thinking of level objects, there should only ever be one active at a time. If there are two, log it because it might be a design issue
        if (HasLevelObject)
        {
            Debug.LogWarning($"[PhysicsController] Overwriting active level object [{ActiveLevelObject.name}] with [{levelObject.name}]");
            // Simple approach: just clear the previous one
            CancelLevelObjectInteraction();
        }
        ActiveLevelObject = levelObject;
    }

    /// <summary>
    /// Cancel the current level object interaction (damage, debug tools, etc.)
    /// </summary>
    public void CancelLevelObjectInteraction()
    {
        if (!HasLevelObject) return;
        
        // Tell the level object to end - it handles cleanup and calls ClearLevelObjectInternal
        var context = LevelObjectBase.Context.Get(transform);
        if (context != null)
            ActiveLevelObject.EndInteraction(context);
    }
    
    /// <summary>
    /// Internal: Clear the level object reference without calling cleanup.
    /// Only called by LevelObjectBase.EndInteraction after cleanup is done.
    /// </summary>
    internal void ClearLevelObjectInternal()
    {
        ActiveLevelObject = null;
    }
}