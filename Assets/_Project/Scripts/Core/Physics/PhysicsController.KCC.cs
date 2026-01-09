using FS.GameplayActions;
using FS.Math;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.Utilities;
using UnityEngine;

public partial class PhysicsController : ICharacterController
{
    //private HitStabilityReport m_hitStabilityReport;

    /// <summary>
    /// Disable internal velocity calculations, so whatevers set externally via Velocity is what is used. Useful for hitstun states.
    /// </summary>
    public bool VelocityCalculationsEnabled = true;
    /// <summary>
    /// Disable internal rotation calculations, so whatevers set externally via Rotation is what is used. Useful for hitstun states.
    /// </summary>
    public bool RotationCalculationsEnabled = true;


    /// <summary>
    /// Stabiliy evaluated along gravity direction when grounded and properly oriented, but against transform when in air
    /// </summary>
    public Vector3 StabilityUpReference => IsGrounded ? -GravityDir : m_motor.Up; 


    public void OnWallHit(Contact wallContact)
    {
        if (m_actionController && m_actionController.ActiveActions.ContainsAnyChannel(ActionChannel.Physics))
        {
            foreach (var physicsReciever in m_actionController.IterateActiveActions<IActionPhysicsReciever>())
            {
                physicsReciever.OnWallHit(wallContact);
            }
        }
    }

    public void UpdateVelocity()
    {
        Vector3 frameStartVelocity = Velocity;
        
        //if (IsGrounded) currentVelocity = currentVelocity.GetDirectionTangentToSurface(UpDirection, UpDirection) * currentVelocity.magnitude;

        // Allow to disable internal physics calculations to fix results to whatever is set externally via velocity & rotation
        if (!VelocityCalculationsEnabled) return;
        
        bool anyLateralPhysicsActions = m_actionController && m_actionController.ActiveActions.ContainsAnyChannel(ActionChannel.PhysicsLateral);
        bool anyVerticalPhysicsActions = m_actionController && m_actionController.ActiveActions.ContainsAnyChannel(ActionChannel.PhysicsVertical);
        
        if (anyLateralPhysicsActions || anyVerticalPhysicsActions)
        {
            foreach (var physicsReciever in m_actionController.IterateActiveActions<IActionPhysicsReciever>())
            {
                var action = physicsReciever as GameplayAction;
                // ReSharper disable once PossibleNullReferenceException
                if ((action.Channels | ActionChannel.Physics) > 0)
                    physicsReciever.UpdateVelocity();
            }
        }
        
        if (!anyLateralPhysicsActions)
        {
            LateralPhysics();
            //if (IsGrounded) currentVelocity = currentVelocity.GetDirectionTangentToSurface(UpDirection, UpDirection) * currentVelocity.magnitude;
        }

        if (!anyVerticalPhysicsActions)
        {
            // Allow this to run when we're not grounded to update the modifiers (since it removes them during modifier update)
            if (true || !IsGrounded) VerticalPhysics();
        }
        
        // Apply velocity modifiers, but do so before constraints as it makes sense constraitns are applied on modifiers as well
        var velocity = Velocity;
        m_velocityPostProcessModifiers.Apply(this, ref velocity);
        Velocity = velocity;
        
        // If we've got any physics constraints, apply them
        if (m_actionController && m_actionController.ActiveActions.ContainsAnyChannel(ActionChannel.PhysicsConstraint))
        {
            foreach (var physicsReciever in m_actionController.IterateActiveActions<IActionPhysicsContraintReceiver>())
            {
                var action = physicsReciever as GameplayAction;
                // ReSharper disable once PossibleNullReferenceException
                if ((action.Channels | ActionChannel.PhysicsConstraint) > 0)
                    physicsReciever.ApplyVelocityConstraints(frameStartVelocity);
            }
        }
    }
    
    public void UpdateRotation()
    {
        var initialRotation = Rotation;

        // Allow to disable internal physics calculations to fix results to whatever is set externally via velocity & rotation
        if (RotationCalculationsEnabled)
        {
            if (m_actionController && m_actionController.ActiveActions.ContainsAnyChannel(ActionChannel.Rotation))
            {
                if (m_actionController.ActiveActions[ActionChannel.Rotation] is IActionPhysicsReciever action)
                    action.UpdateRotation();
            }
            else
            {
                RotationPhysics();
            }

            var rotation = Rotation;
            m_rotationPostProcessModifiers.Apply(this, ref rotation);
            Rotation = rotation;
        }
        
        // TODO: Can we always have it so that the rotation pivot is the bottom hemi center when grounded, but capsule center when in air?
        // OR we keep the pivot at the center, and just use the bottom hemi as the pivot when grounded, so reverse of what i said above
        // Move the position to create a rotation around the bottom hemi center instead of around the pivot
        // NOTE: This avoids the issue of us jittering around a bottom slope-edge
        //Vector3 initialCharacterBottomHemiCenter = m_motor.TransientPosition + (currentUp * m_motor.Capsule.radius);

        // NOTE: This is great for leaving unstable ground!
        if (IsGrounded) return;
        // When grounded, we rotate around the bottom hemi. Otherwise, we rotate around the capsule center. We do this in a way
        // that makes the physics of rotation now independent of capsule-Y offset
        Vector3 pivotOffset = Vector3.up * CapsuleHeight/2f;
        Vector3 initialCharacterBottomHemiCenter = Position + (initialRotation * pivotOffset);
        Position = initialCharacterBottomHemiCenter - (Rotation * pivotOffset);
    }

    public void EvaluateGroundStability(CollisionInfo collision, ref bool isStable)
    {
        //bool wasStable = isStable;
        // TODO: I think what we're doing here isn't great, esp with steps-interactions. Noticed bad grounding when jumping towards steps
        if (IsGrounded && isStable) return; // NOTE: Whatever we're doing here fucks w/ steps so be wary
        if (IsInSkateAction && !collision.hitInfo.collider3D.CompareLayer(PhysicsLayers.Vert)) isStable = false;
        // If there's a large angle w/ our up direction, return false
        else isStable = isStable && collision.hitInfo.normal.Angle(UpDirection) <= SlopeLimit(collision);

        // DEBUG for the QP landing bug, issue here was the floating point comparison issue
        // if (TimeSinceSkateActionEnded < 0.1f && !isStable)
        // {
        //     Debug.LogError($"[Physics] Manual stability override post vert results in unstable ground, presumably bad slope limit: \n" +
        //                    $"Was stable before we overrode it? {wasStable}\n" +
        //                    $"Layer Of Collision: {LayerMask.LayerToName(collision.hitInfo.layer)}" +
        //                    $"Manually Computed Angle: {collision.hitInfo.normal.Angle(UpDirection)}\n" +
        //                    $"SlopeLimit: {SlopeLimit(collision)}\n" +
        //                    $"StabilityUp: {StabilityUpReference}\n" +
        //                    $"Character Collision Ground Slope: {m_motor.CharacterCollisionInfo.groundSlopeAngle}\n" +
        //                    $"Contact Slope Angle {Vector3.Angle(StabilityUpReference, m_motor.CharacterCollisionInfo.groundContactNormal)}");
        // }
    }

    public const float k_defaultSlopeLimit = 55f;
    public float SlopeLimit(CollisionInfo collision)
    {
        // DEBUG for the acid drop landing bug
        // if (TimeSinceSkateActionEnded < 0.1f && !IsGrounded)
        // {
        //     Debug.LogError($"[Physics] Evaluating bad landing: {Ground.GroundSlopeAngle}");
        //     using var dur = Drawing.Draw.ingame.WithDuration(5f);
        //     using var thickness = Drawing.Draw.ingame.WithLineWidth(2);
        //     Drawing.Draw.ingame.Arrow(collision.hitInfo.point, collision.hitInfo.point + collision.hitInfo.normal, Color.red);
        //     if (Ground.GroundSlopeAngle > 1) Debug.Break(); // consistent?
        // }
        if (IsInSkateAction && Velocity.Dot(-GravityDir) > 0f) return -1f; // No ground allowed
        if (collision.hitInfo.collider3D && collision.hitInfo.collider3D.CompareLayer(PhysicsLayers.Vert) || TimeSinceSkateActionEnded < 0.1f) // BUG FIX BANDAID, TIME SINCE CHECK [CL 927] (FIXES CASES WHERE QP TOP IS SEPERATED)
        {
            // BUG FIX: RETURN 91 INSTEAD OF 90, FIXES OTHER CASES OF BAD LANDING WHEN COMPUTED ANGLE IS LIKE 90.002 WHICH IS CONSIDERED UNSTABLE
            // Evaluate stabiltiy of it
            if (IsInSkateAction) return 91f; // Always stable on vert when skating / landing
            float denivelation = transform.up.Angle(collision.hitInfo.normal);
            if (denivelation < k_defaultSlopeLimit) return 91f;
            //return 90f;
        }

        // If last ground was vert and new one isnt, and they're last normals are both > k_defaultSlopeLimit, then dont allow it
        if (Ground.CompareLayer(PhysicsLayers.Vert) && !collision.hitInfo.collider3D.CompareLayer(PhysicsLayers.Vert))
        {
            if (Ground.GroundSlopeAngle > k_defaultSlopeLimit && collision.contactSlopeAngle > k_defaultSlopeLimit)
                return k_defaultSlopeLimit; // No ground allowed, stick to default as the intent is to unground here
        }

        return collision.GetSlopeLimit(this);
    }

    /// <summary>
    /// Upon hitting an unstable surface from stable ground, should we 'eject' from it or treat it as a wall?
    /// </summary>
    public bool ShouldEjectFromUnstableGroundTransition(Vector3 newNormal)
    {
        var angleDelta = GroundNormal.Angle(newNormal);
        return angleDelta < 70f;
    }

    private GroundMover m_cachedGroundMover = null; // we cache this to avoid repeated GetComponent calls, performance critical path w/ the get vel/rot functions
    public bool IsValidNonRigidbodyDynamicGround(GameObject groundObject)
    {
        if (m_cachedGroundMover == null || m_cachedGroundMover.gameObject != groundObject)
            groundObject.TryGetComponent<GroundMover>(out m_cachedGroundMover);
        return m_cachedGroundMover != null;
    }

    public Vector3 GetNoneRigidbodyGroundVelocity(GameObject groundObject, Vector3 contactPoint)
    {
        return m_cachedGroundMover?.GetGroundVelocity(this, contactPoint) ?? Vector3.zero;
    }

    public Vector3 GetNoneRigidbodyInheritedVelocity(Vector3 groundVelocity)
    {
        return m_cachedGroundMover?.GetInheritedVelocity(this, Position) ?? groundVelocity;
    }

    public Quaternion GetNoneRigidbodyGroundRotation(GameObject groundObject, Vector3 contactPoint)
    {
        return m_cachedGroundMover?.GetGroundRotation(this, contactPoint) ?? Quaternion.identity;
    }

    // TODO: This could do with plenty of improvements 
    public bool IsValidGround(CollisionInfo collision)
    {
        if (!collision.hitInfo.hit) return false;

        if (IsInSkateAction && !collision.hitInfo.collider3D.CompareLayer(PhysicsLayers.Vert)) return false;
        // If there's a large angle w/ our up direction, return false
        return collision.hitInfo.normal.Angle(UpDirection) <= SlopeLimit(collision);
    }

    // public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition,
    //     Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    // {
    //     // Evaluate stability of a vert surface
    //     if (hitCollider.gameObject.CompareTag(GameTags.Vert))
    //     {
    //         // Are we landing from a skate action?
    //         if (IsInSkateAction && !IsGrounded) hitStabilityReport.IsStable = true; // Always stable on landing
    //         else if (TimeSinceSkateActionEnded < 0.1f) hitStabilityReport.IsStable = true; // Ensure stability for a period of time
    //         else
    //         {
    //             if (IsGrounded)
    //             {
    //                 // TODO: There's a jitter/off projection that can happen if we just naively set vert surface = true for stability
    //                 // it's usually in the case of going up vert, so a reliable check is if the normals are orthogonal. This could be related
    //                 // to us using GroundNormal the way we are but i dont think so
    //                 if (hitStabilityReport is { FoundInnerNormal: true, FoundOuterNormal: true })
    //                 {
    //                     var denivelation = Vector3.Angle(hitStabilityReport.InnerNormal, hitStabilityReport.OuterNormal);
    //                     hitStabilityReport.IsStable = denivelation < 40f;
    //                 }
    //             }
    //         }
    //     }
    // }
}