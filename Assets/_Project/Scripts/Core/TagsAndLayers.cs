
using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameTags
{
    // Pre-defined tags
    public static readonly TagHandle None = TagHandle.GetExistingTag("Untagged");
    public static readonly TagHandle Respawn = TagHandle.GetExistingTag("Respawn");
    public static readonly TagHandle Finish = TagHandle.GetExistingTag("Finish");
    public static readonly TagHandle MainCamera = TagHandle.GetExistingTag("MainCamera");
    public static readonly TagHandle Player = TagHandle.GetExistingTag("Player");
    public static readonly TagHandle GameController = TagHandle.GetExistingTag("GameController");
    
    // User-defined tags
    // Surface Camera Affector
    public static readonly TagHandle CameraBasis = TagHandle.GetExistingTag("CameraBasis");
    
    // Gameplay Specific
    public static readonly TagHandle CannonDrop = TagHandle.GetExistingTag("CannonDrop");
}

public static class PhysicsLayers
{
    // Pre-defined layers
    public static readonly LayerMask Default = LayerMask.NameToLayer("Default");
    public static readonly LayerMask TransparentFX = LayerMask.NameToLayer("TransparentFX");
    public static readonly LayerMask IgnoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
    public static readonly LayerMask Water = LayerMask.NameToLayer("Water");
    public static readonly LayerMask UI = LayerMask.NameToLayer("UI");
    
    // User-defined layers
    public static readonly LayerMask CameraObstruction = LayerMask.NameToLayer("Camera Obstruction");
    public static readonly LayerMask Character = LayerMask.NameToLayer("Character");
    
    public static readonly LayerMask Feeler = LayerMask.NameToLayer("Feeler");
    public static readonly LayerMask LevelObject = LayerMask.NameToLayer("LevelObject");

    public static readonly LayerMask RailGrind = LayerMask.NameToLayer("RailGrind");
    public static readonly LayerMask Vert = LayerMask.NameToLayer("Vert");
    public static readonly LayerMask WallSlide = LayerMask.NameToLayer("WallSlide");

    public static readonly LayerMask TargetingQuery = LayerMask.NameToLayer("TargetingQuery");
    public static readonly LayerMask HitBox = LayerMask.NameToLayer("HitBox");
    
    // Collision Matrix Helper
    private static Dictionary<int, int> m_collisionMatrix;

    // Initialize physics settings
    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        m_collisionMatrix = null; // reset this
        Physics.queriesHitTriggers = false;
    }

    /// <summary>
    /// Returns a layermask that represents all the layers that the given layer can collide with.
    /// </summary>
    public static int GetLayerCollisionMask(LayerMask layer)
    {
        int outLayerMask = 0;
        
        if (m_collisionMatrix != null)
        {
            if (!m_collisionMatrix.TryGetValue(layer, out outLayerMask))
            {
                throw new ArgumentException($"{layer} not found in collision matrix");
            }
            return outLayerMask;
        }
        
        m_collisionMatrix = new Dictionary<int, int>();
        for (int i = 0; i < 32; i++)
        {
            int mask = 0;
            for (int j = 0; j < 32; j++)
            {
                if (!Physics.GetIgnoreLayerCollision(i, j)) mask |= 1 << j;
            }
            m_collisionMatrix.Add(i, mask);
        }

        if (!m_collisionMatrix.TryGetValue(layer, out outLayerMask))
        {
            throw new ArgumentException($"{layer} not found in collision matrix");
        }
        return outLayerMask;
    }
    
    public static bool CompareLayer(this Collider collider, int layerIdx) => collider && collider.gameObject.layer == layerIdx;
    
#if UNITY_EDITOR
    /// <summary>
    /// Find all gameobjects whose layer is party of the input layermask. Pretty slow function that requires looping over all gameobjects
    /// so its constrained to only editor use.
    /// </summary>
    public static GameObject[] FindGameObjectsWithLayer(LayerMask layer, bool includeInactive = false)
    {
        var allGOs = GameObject.FindObjectsByType<GameObject>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var results = new List<GameObject>();
        foreach (var go in allGOs)
        {
            if ((layer & (1 << go.layer)) != 0) 
                results.Add(go);
        }
        return results.ToArray();
    }
#endif    
}