using Lightbug.Utilities;
using UnityEngine;

public interface IActionPhysicsReciever
{
    void UpdateRotation() {}
    void UpdateVelocity() {}
    
    void OnFoundGround() {}
    void OnLostGround() {}

    void OnWallHit(Contact wallContact) {}
}

public interface IActionPhysicsContraintReceiver
{
    /// <summary>
    /// Called at the end of UpdateVelocity callback from KCC to allow applying whatever constraints to computed velocity
    /// </summary>
    /// <param name="startVelocity">Velocity at the start of UpdateVelocity</param>
    /// <param name="currentVelocity">Velocity at the end of UpdateVelocity, passed in as ref for modifications</param>
    void ApplyVelocityConstraints(Vector3 startVelocity) {}
}