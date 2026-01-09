using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Component that broadcasts events when a PhysicsController enters or exits ground contact.
/// </summary>
public class GroundEventBroadcaster : MonoBehaviour
{
    public UnityEvent OnBecomeGroundEvent;
    public UnityEvent OnNoLongerGroundEvent;
    
    public event Action<PhysicsController> OnBecomeGroundForGameObject;
    public event Action<PhysicsController> OnNoLongerGroundForGameObject;

    public event Action OnBecomeGround;
    public event Action OnNoLongerGround;

    private HashSet<PhysicsController> m_groundsForControllers = new();
    
    public void OnBecomeGroundForController(PhysicsController controller)
    {
        if (m_groundsForControllers.Count == 0)
        {
            OnBecomeGroundEvent?.Invoke();
            OnBecomeGround?.Invoke();
        }
        m_groundsForControllers.Add(controller);
        OnBecomeGroundForGameObject?.Invoke(controller);
    }
    
    public void OnNoLongerGroundForController(PhysicsController controller)
    {
        int initialCount = m_groundsForControllers.Count;
        m_groundsForControllers.Remove(controller);
        OnNoLongerGroundForGameObject?.Invoke(controller);
        if (initialCount == 1 && m_groundsForControllers.Count == 0)
        {
            OnNoLongerGroundEvent?.Invoke();
            OnNoLongerGround?.Invoke();
        }
    }
}