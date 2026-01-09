using System;
using FS.Math;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.Utilities;
using UnityEngine;

public partial class PhysicsController
{
    // TODO: This does not account for velocity of the base we're standing on
    public Vector3 Velocity
    {
        get => m_motor.Velocity;
        set => SetVelocity(value);
    }

    public Vector3 LateralVelocity
    {
        get => m_motor.Velocity.ProjectOnPlane(UpDirection);
        set => SetVelocity(value + VerticalVelocity);
    }
    
    public Vector3 VerticalVelocity
    {
        get => m_motor.Velocity.ProjectOnto(UpDirection);
        set => SetVelocity(value + LateralVelocity);
    }

    public Vector3 VelocityDirection
    {
        get => Velocity.normalized;
        set => Velocity = value * Velocity.magnitude;
    }

    // NOTE: Doesnt make sense to have a vertical velocity version of this function as its 1D, lateral is at least 2D
    public Vector3 LateralVelocityDirection
    {
        get => LateralVelocity.normalized;
        set => LateralVelocity = value * LateralVelocity.magnitude;
    }

    public float Speed
    {
        get => Velocity.magnitude;
        set => Velocity = (Velocity.sqrMagnitude <= 0f ? transform.forward : Velocity.normalized) * value;
    }

    public float LateralSpeed
    {
        get => LateralVelocity.magnitude;
        set => Velocity = VerticalVelocity + (LateralSpeed <= 0f ? transform.forward : LateralVelocity.normalized) * value;
    }

    public float VerticalSpeed
    {
        get => VerticalVelocity.magnitude * Mathf.Sign(VerticalVelocity.Dot(UpDirection));
        set => Velocity = LateralVelocity + (VerticalSpeed <= 0f ? UpDirection : VerticalVelocity.normalized) * value;
    }
    
    public Vector3 PreviousFrameVelocity { get; private set; }

    public float AngularVelocity { get; private set; }
    
    public Vector3 Acceleration { get; private set; }

    public Vector3 LocalAcceleration => transform.InverseTransformVector(Acceleration);
    
    public bool IsFalling => VerticalVelocity.Dot(GravityDir) > 0f;

    /// <summary>
    /// A forward direction that follows the logic:
    /// - Try get input direction, if not available then lateral velocity, if not available then transform.forward
    /// </summary>
    public Vector3 InputForwardDirection
    {
        get
        {
            var forward = MoveInput().ProjectOnPlane(UpDirection);
            if (forward.sqrMagnitude < 0.1f) forward = LateralVelocity;
            if (forward.sqrMagnitude < 0.1f) forward = transform.forward.ProjectOnPlane(UpDirection);
            return forward.normalized;
        }
    }
    
    public Vector3 Position
    {
        get => m_motor.Position;
        set => SetPosition(value);
    }

    public Vector3 FootPosition
    {
        get => m_motor.Bottom; 
        set => SetFootPosition(value);
    }
    public Vector3 FootPositionOffset => FootPosition - Position;

    public Vector3 HeadPosition
    {
        get => m_motor.Top; 
        set => SetHeadPosition(value);
    }
    public Vector3 HeadPositionOffset => HeadPosition - Position;

    public Vector3 CenterPosition
    {
        get => m_motor.Center;
        set => SetCenterPosition(value);
    }
    public Vector3 CenterPositionOffset => CenterPosition - Position;
    
    public Quaternion Rotation
    {
        get => m_motor.Rotation;
        set => SetRotation(value);
    }

    public float CapsuleRadius => m_motor.CharacterBody.BodySize.x/2f;
    public float CapsuleHeight => m_motor.CharacterBody.BodySize.y;

    public void SetVelocity(Vector3 velocity)
    {
        var preApplyVel = Velocity;
        m_motor.Velocity = velocity;
        // Are we grounded? If so should our newly added velocity launch us 
        if (IsGrounded)
        {
            var verticalVelDelta = (Velocity - preApplyVel).Dot(-GravityDir);
            var finalVerticalVel = Velocity.Dot(-GravityDir);

            // TODO: Unreliable to do this constantly, no intentionality in grounding logic esp when using grav dir
            // Unground if we added upward velocity AND the result is upward movement
            if (IsGrounded && verticalVelDelta > 0.1f && finalVerticalVel > 0.5f)
            {
                //UnGround();
            }
        }
    }
    public void AddVelocity(Vector3 velocity) => Velocity += velocity;
    
    [Obsolete("Can set rotation directly now")]
    public void SetRotation(Quaternion rotation) => m_motor.Rotation = rotation;
    [Obsolete("Can set rotation directly now")]
    public void AddRotation(Quaternion rotation) => SetRotation(rotation * Rotation);

    public void SetPosition(Vector3 position) => m_motor.Position = position;
    public void SetFootPosition(Vector3 position) => m_motor.Position = position - FootPositionOffset;
    public void SetHeadPosition(Vector3 position) => m_motor.Position = position - HeadPositionOffset;
    public void SetCenterPosition(Vector3 position) => m_motor.Position = position - CenterPositionOffset;

    public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        m_motor.Position = position;
        m_motor.Rotation = rotation;
    }

    /// <summary>
    /// Sets the rigidbody velocity based on a target position. The same can be achieved by setting the velocity value manually.
    /// </summary>
    public void MovePosition(Vector3 targetPosition) => m_motor.Move(targetPosition);
    public void RotateCharacter(Quaternion targetRotation) => m_motor.RigidbodyComponent.Rotate(targetRotation);

    public void Teleport(Vector3 position, Quaternion? rotation = null)
    {
        rotation ??= Rotation;
        m_motor.Teleport(position, rotation.Value);
    }
    
    public RigidbodyComponent Rigidbody => m_motor.RigidbodyComponent;

    /// <summary>
    /// We essentially get childed to this transform. Useful for ledge grabbing onto moving platforms for example.
    /// This is different from being 'grounded' to a moving platform which is handled automatically by the physics system.
    /// </summary>
    public Transform AttachmentParent { get; set; }
    
    // TODO: Re-implement this if we ever need to attach to moving platforms again (forcefully)
    //public Rigidbody AttachedRigidbody
    //{
    //    get => m_motor.GroundRigidbody3D;
    //    set => m_motor.AttachedRigidbodyOverride = value;
    //}
    
    #region Collision Detection
    public int CollidableLayers => m_motor.ObstaclesLayerMask;
    public Contact WallContactCollision => m_motor.WallContact;

    public bool CharacterCollisionSweep(Vector3 position,Vector3 direction, float distance, out HitInfo closestHit)
    {
        closestHit = m_motor.RigidbodyComponent.Sweep(position, direction, distance);
        return closestHit.hit;
    }

    public bool CharacterCollisionSweep(Vector3 direction, float distance,
        out HitInfo closestHit)
    {
        closestHit = m_motor.RigidbodyComponent.Sweep(Position, direction, distance);
        return closestHit.hit;
    }

    public bool CheckCharacterCapsule(Vector3 queryPos)
    {
        HitInfoFilter filter = new HitInfoFilter(
            CollidableLayers,
            false,
            true
        );
        return m_motor.CharacterCollisions.CheckOverlap(queryPos, 0f, in filter);
    }
    
    public bool IsHitStableGround(RaycastHit hit) => IsHitStableGround(hit.normal);
    public bool IsHitStableGround(HitInfo hit) => IsHitStableGround(hit.normal);
    public bool IsHitStableGround(Vector3 normal) => Vector3.Angle(-GravityDir, normal) <= m_motor.defaultSlopeLimit; // TODO: How do we incorporate our API function?
    
    #endregion
    
    #region Physics Modifications
    
    private PhysicsModificationRequest<LateralPhysicsParams> m_lateralPhysicsModifications = PhysicsModificationRequest<LateralPhysicsParams>.Create();
    private PhysicsModificationRequest<VerticalPhysicsParams> m_verticalPhysicsModifications = PhysicsModificationRequest<VerticalPhysicsParams>.Create();
    private PhysicsModificationRequest<RotationPhysicsParams> m_rotationPhysicsModifications = PhysicsModificationRequest<RotationPhysicsParams>.Create();
    
    public void ModifyLateralPhysics(Func<PhysicsController, bool> shouldExpireCondition, PhysicsModificationRequest<LateralPhysicsParams>.Modification modifier, string id = null) =>
        m_lateralPhysicsModifications.Add(shouldExpireCondition, modifier, id);
    public void ModifyVerticalPhysics(Func<PhysicsController, bool> shouldExpireCondition, PhysicsModificationRequest<VerticalPhysicsParams>.Modification modifier, string id = null) 
        => m_verticalPhysicsModifications.Add(shouldExpireCondition, modifier, id);
    public void ModifyRotationPhysics(Func<PhysicsController, bool> shouldExpireCondition, PhysicsModificationRequest<RotationPhysicsParams>.Modification modifier, string id = null) 
        => m_rotationPhysicsModifications.Add(shouldExpireCondition, modifier, id);
    
    private PhysicsModificationRequest<Vector3> m_velocityPostProcessModifiers = PhysicsModificationRequest<Vector3>.Create();
    private PhysicsModificationRequest<Quaternion> m_rotationPostProcessModifiers = PhysicsModificationRequest<Quaternion>.Create();

    public void RegisterVelocityPostProcessModifier(Func<PhysicsController, bool> shouldExpireCondition, PhysicsModificationRequest<Vector3>.Modification modifier, string id = null)
        => m_velocityPostProcessModifiers.Add(shouldExpireCondition, modifier, id);
    public void RegisterRotationPostProcessModifier(Func<PhysicsController, bool> shouldExpireCondition, PhysicsModificationRequest<Quaternion>.Modification modifier, string id = null)
        => m_rotationPostProcessModifiers.Add(shouldExpireCondition, modifier, id);
    
    #endregion
}