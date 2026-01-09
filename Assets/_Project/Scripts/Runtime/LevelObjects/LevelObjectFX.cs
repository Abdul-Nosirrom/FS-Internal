using UltEvents;

/// <summary>
/// Simple class to invoke events based on LevelObject triggers. Useful for simple FX & SFX so you can add them
/// seperately from the main level object logic.
/// </summary>
public class LevelObjectFX : LevelObjectBase
{
    public UltEvent onPhysicsActorEnterEvent;
    public UltEvent onPhysicsActorStayEvent;
    public UltEvent onPhysicsActorExitEvent;
    
    protected override void OnPhysicsActorEnter(Context physicsController)
    {
        onPhysicsActorEnterEvent?.Invoke();
    }

    protected override void OnPhysicsActorStay(Context physicsController)
    {
        onPhysicsActorStayEvent?.Invoke();
    }

    protected override void OnPhysicsActorExit(Context physicsController)
    {
        onPhysicsActorExitEvent?.Invoke();   
    }
}