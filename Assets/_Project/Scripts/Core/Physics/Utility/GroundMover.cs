using Drawing;
using UnityEngine;

/// <summary>
/// Component to inject custom ground velocity for the physics controller to process while standing on this ground
/// For this to work, GroundMover must NOT be a rigidbody itself
/// </summary>
public abstract class GroundMover : MonoBehaviourGizmos
{
    /// <summary>
    /// Velocity to apply to the physics controller while standing on this ground mover
    /// </summary>
    public abstract Vector3 GetGroundVelocity(PhysicsController physics, Vector3 contactPoint);
    
    /// <summary>
    /// Rotation to apply to the physics controller while standing on this ground mover
    /// </summary>
    public abstract Quaternion GetGroundRotation(PhysicsController physics, Vector3 contactPoint);

    /// <summary>
    /// Velocity imparted to the physics controller when it leaves this ground mover
    /// </summary>
    public virtual Vector3 GetInheritedVelocity(PhysicsController physics, Vector3 contactPoint) => GetGroundVelocity(physics, contactPoint);
}