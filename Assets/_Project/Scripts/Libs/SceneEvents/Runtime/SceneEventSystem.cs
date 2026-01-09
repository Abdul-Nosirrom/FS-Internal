
using System;
using System.Linq;
using FS.Patterns;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Handles broadcasting events to all components in the scene deriving from an event type
/// </summary>
public class SceneEventSystem : PersistentSingleton<SceneEventSystem>
{
    public void RunEvent<T>(Action<T> action) where T : ISceneEvent<T>
    {
        foreach (var c in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).OfType<T>())
        {
            try
            {
                action(c);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
            }
        }
    }
}

/// <summary>
/// Base interface for all scene events
/// </summary>
public interface ISceneEvent<T> where T : ISceneEvent<T>
{
    /// <summary>
    /// Posts an event to all matching components in a scene
    /// </summary>
    public static void Post(Action<T> action)
    {
        if (SceneEventSystem.Instance == null) return;
        SceneEventSystem.Instance.RunEvent<T>(action);
    }
    
    /// <summary>
    /// Posts an event to all valid components in a given game object
    /// </summary>
    public static void PostToGameObject(GameObject go, Action<T> action)
    {
        if (go == null) return;
        
        foreach (var component in go.GetComponents<MonoBehaviour>().OfType<T>())
        {
            try
            {
                action(component);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error in scene event: {e.Message}");
            }
        }
        
    }
}

public interface IPlayerEvents : ISceneEvent<IPlayerEvents>
{
    void OnPlayerHurt(GameObject player, float amount);
}

public class TestComp : MonoBehaviour, IPlayerEvents
{
    public void OnPlayerHurt(GameObject player, float amount)
    {
        Debug.Log($"Player hurt by {amount}");
    }

    private bool bDone = false;
    private void Update()
    {
        if (Time.time > 2 && !bDone)
        {
            PlayerDied();
        }
    }

    void PlayerDied()
    {
        IPlayerEvents.Post(x => x.OnPlayerHurt(gameObject, 10));
    }
}