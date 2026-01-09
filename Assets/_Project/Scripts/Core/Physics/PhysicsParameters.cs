using System;
using Lightbug.CharacterControllerPro.Core;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public struct VerticalPhysicsParams
{
    [Title("Gravity")]
    
    [Tooltip("Gravity strength when rising (moving against gravity direction). " +
             "Higher values = shorter, snappier jumps. Lower values = floatier ascent. " +
             "Typical range: 10-20 for responsive feel, 5-10 for floaty platformer feel.")]
    [Range(0, 40)] 
    public float m_upGravity;
    
    [Tooltip("Gravity strength when falling (moving with gravity direction). " +
             "Higher values = faster, weightier falls. Usually set higher than upGravity for snappy landings. " +
             "Note: Reduced to 5 for first 0.2s after leaving ground when vertical speed < 5 (coyote time feel).")]
    [Range(0, 40)] 
    public float m_downGravity;
    
    [Tooltip("Maximum falling speed. Caps how fast you can fall regardless of gravity. " +
             "Higher = more dangerous long falls, lower = safer feeling. " +
             "Also used to clamp slide velocity on slopes.")]
    [Range(1, 40)] 
    public float m_terminalSpeed;
    
    [Title("Constraints")]
    
    [Tooltip("Maximum allowed upward speed. If rising faster than this, strong deceleration is applied. " +
             "Prevents excessive launch heights from ramps/boosts. " +
             "Set very high (60) for full physics freedom, lower (15-25) for controlled jump heights.")]
    [Range(1, 60)] 
    public float m_maxRiseSpeed;
    
    [Tooltip("How quickly to slow down when exceeding maxRiseSpeed. " +
             "Higher = harder speed cap, lower = softer rolloff allowing brief speed spikes. " +
             "Only applies when moving upward faster than maxRiseSpeed.")]
    [Range(5, 100)] 
    public float m_riseSpeedDeceleration;

    public void SetGravity(float grav) => m_upGravity = m_downGravity = grav;

    /// <summary>
    /// Disables all vertical constraints for perfect physics path following (ramps, launches, etc.)
    /// </summary>
    public void SetNoControl()
    {
        m_terminalSpeed = m_maxRiseSpeed = float.MaxValue;
        m_riseSpeedDeceleration = 0;
    }
}

[Serializable]
public struct LateralPhysicsParams
{
    [Tooltip("Downhill acceleration on slopes. Applied as gravity projected onto the ground plane. " +
             "Higher = faster acceleration going downhill, more slowdown going uphill. " +
             "Set to 0 for flat-feeling slopes. Does NOT apply on Vert surfaces when moving upward.")]
    [Range(0, 30)] 
    public float m_slopeGravity;
    
    [Title("Base Physics")]
    
    [Tooltip("Ground acceleration rate (units/sec²). How quickly you reach max speed from standstill. " +
             "Higher = snappier response, lower = sluggish/heavy feel. " +
             "Note: Always accelerates to at least 1 unit/sec minimum (analog dead zone).")]
    [Range(0, 50)] 
    public float m_acceleration;
    
    [Tooltip("Ground deceleration when releasing input or over top speed (units/sec²). " +
             "Higher = stops quickly (responsive), lower = slides/coasts longer. " +
             "Reduced when moving downhill to preserve momentum on slopes.")]
    [Range(0, 50)] 
    public float m_deceleration;

    [Tooltip("How quickly velocity direction aligns to input direction while holding input. " +
             "Higher = tighter turning, can change direction instantly. " +
             "Lower = wider turns, preserves momentum through direction changes. " +
             "In air, multiplied by airControl.")]
    [Range(0, 50)] 
    public float m_friction;

    [Title("Air Handling")]
    
    [Tooltip("Air acceleration rate (units/sec²). Usually lower than ground acceleration. " +
             "Higher = more air control, lower = committed jump arcs.")]
    [Range(0, 50)] 
    public float m_airAcceleration;
    
    [Tooltip("Air deceleration when releasing input (units/sec²). " +
             "Higher = can brake in air, lower = maintains air momentum.")]
    [Range(0, 50)] 
    public float m_airDeceleration;
    
    [Tooltip("Constant air resistance that slows lateral movement in air. Counteracts acceleration" +
             "Higher = jumps feel 'draggy', speed bleeds off. Lower = maintains launch speed.")]
    [Range(0, 50)] 
    public float m_airDrag;
    
    [Tooltip("Multiplier for friction while airborne (0-1). " +
             "1 = full air turning ability, 0 = no air turning. " +
             "Affects how much you can redirect mid-air.")]
    [Range(0, 1)] 
    public float m_airControl;
    
    [Title("Constraints")]
    
    [Tooltip("Target speed when accelerating on ground. Input acceleration stops at this speed. " +
             "Represents 'cruising speed' - comfortable movement pace. " +
             "Actual speed scaled by input magnitude for analog control.")]
    [Range(0, 50)] 
    public float m_maxSpeed;
    
    [Tooltip("Target speed when accelerating in air. Usually matches or slightly exceeds ground maxSpeed. " +
             "Scaled by input magnitude for analog control.")]
    [Range(0, 50)] 
    public float m_airMaxSpeed;
    
    [Tooltip("Absolute maximum lateral speed. Cannot accelerate past this, triggers deceleration if exceeded. " +
             "Represents 'boost speed' from ramps/special moves. Must be >= maxSpeed. " +
             "Speed between maxSpeed and topSpeed can only be achieved through external forces.")]
    [Range(0, 50), MinValue("m_maxSpeed")] 
    public float m_topSpeed;
    
    [Tooltip("Deceleration rate when speed exceeds topSpeed (units/sec²). " +
             "Higher = quickly bleeds excess speed, lower = maintains boost longer. " +
             "Separate from normal deceleration to fine-tune boost duration.")]
    [Range(0, 50)]
    public float m_overTopSpeedDeceleration;
    
    /// <summary>
    /// Disables all player lateral control for pure physics simulation
    /// </summary>
    public void SetNoControl()
    {
        m_acceleration = 0;
        m_airAcceleration = 0;
        m_deceleration = 0;
        m_airDeceleration = 0;
        m_friction = 0;
        m_airDrag = 0;
        m_overTopSpeedDeceleration = 0f;
        m_topSpeed = m_maxSpeed = m_airMaxSpeed = float.MaxValue;
    }
}

[Serializable]
public struct RotationPhysicsParams
{
    [Tooltip("How quickly the character's 'up' vector aligns to target (ground normal or gravity). " +
             "Higher = snaps to surfaces quickly, good for tight transitions. " +
             "Lower = smoother visual rotation, can feel disconnected from ground. " +
             "Auto-scales with speed on ground (up to 4x at topSpeed) for high-speed stability.")]
    [Range(0, 25)] 
    public float m_verticalRotationRate;
    
    [Tooltip("How quickly the character faces their velocity/movement direction. " +
             "Higher = snappy turning visuals, lower = gradual pivot. " +
             "Below ~2 units/sec speed, faces input direction instead of velocity.")]
    [Range(0, 25)] 
    public float m_lateralRotationRate;

    [Tooltip("When true, character rotates to align with gravity while airborne. " +
             "When false, maintains rotation from last grounded state in air. " +
             "Enable for characters that should always be upright, disable for trick/flip freedom.")]
    public bool m_bUpIsGravityInAir;
    
    [Tooltip("Rate at which 'up' returns to gravity direction while airborne. " +
             "Only used when bUpIsGravityInAir is true. " +
             "Higher = quickly rights itself, lower = slow self-correction.")]
    [Range(0, 25), ShowIf("m_bUpIsGravityInAir")] 
    public float m_revertToAirUpRate;
}

public struct PhysicsModificationRequest<T> where T : struct
{
    public delegate void Modification(ref T value);

    public bool HasAny => m_activeModifierCount > 0;
    
    private const int k_maxModifiers = 4;
    
    private Func<PhysicsController, bool>[] m_expirationConditions;
    private Modification[] m_modifiers;
    private string[] m_modifierIds;

    private int m_activeModifierCount;
    
    public static PhysicsModificationRequest<T> Create()
    {     
        return new PhysicsModificationRequest<T>
        {
            m_expirationConditions = new Func<PhysicsController, bool>[k_maxModifiers],
            m_modifiers = new Modification[k_maxModifiers],
            m_modifierIds = new string[k_maxModifiers],
            m_activeModifierCount = 0
        };
    }

    public void Add(Func<PhysicsController, bool> shouldExpire, Modification modifier, string id = null)
    {
        if (m_activeModifierCount >= k_maxModifiers)
        {
            Debug.LogError($"[PhysicsController] Exceeded max active modifier count! {k_maxModifiers}");
            return;
        }

        // If ID exists, update the existing modifier
        if (id != null)
        {
            for (int i = 0; i < m_activeModifierCount; i++)
            {
                if (string.Equals(id, m_modifierIds[i]))
                {
                    // Update modifier and expiration condition
                    m_expirationConditions[i] = shouldExpire;
                    m_modifiers[i] = modifier;
                    return;
                }
            }
        }

        id = id ?? $"mod_{DateTime.Now.ToLongTimeString()}";
        
        m_expirationConditions[m_activeModifierCount] = shouldExpire;
        m_modifiers[m_activeModifierCount] = modifier;
        m_modifierIds[m_activeModifierCount] = id;
        
        m_activeModifierCount++;
    }
    
    public void Apply(PhysicsController controller, ref T value)
    {
        for (int m = 0; m < m_activeModifierCount; m++)
        {
            if (m_expirationConditions[m] == null) continue;
            
            if (m_expirationConditions[m](controller))
            {
                RemoveModifierAt(m);
                m--; // Adjust the indices as we just shifted everything down by 1
            }
            else
                m_modifiers[m](ref value);
        }
    }

    private void RemoveModifierAt(int index)
    {
        // Shift all elements down to remove the item at index
        for (int i = index; i < m_activeModifierCount - 1; i++)
        {
            m_expirationConditions[i] = m_expirationConditions[i+1];
            m_modifiers[i] = m_modifiers[i+1];
            m_modifierIds[i] = m_modifierIds[i+1];
        }
    
        // Clear the last element and decrement count
        m_activeModifierCount--;
        m_expirationConditions[m_activeModifierCount] = null;
        m_modifiers[m_activeModifierCount] = null;
        m_modifierIds[m_activeModifierCount] = null;
    }

    public override string ToString()
    {
        string result = $"Active Modifiers: {m_activeModifierCount}\n";
        for (int i = 0; i < m_activeModifierCount; i++)
        {
            result += $"Modifier {i}: {m_modifierIds[i] ?? "NO ID"}\n";
        }
        return result;
    }
}

public struct GroundInfo
{
    public GameObject GroundObject { get; private set; }
    public Collider Collider { get; private set; }

    public Vector3 Normal => IsStable ? m_stableNormal : m_contactNormal;

    public Vector3 Point { get; private set; }

    public float GroundSlopeAngle => CollisionInfo.groundSlopeAngle;
    
    public float EdgeAngle { get; private set; }

    public bool IsOnEdge { get; private set; }

    public bool IsStable { get; private set; }
    
    public bool AnyGround { get; private set; }

    public float DynamicFriction => m_physicsMat?.dynamicFriction ?? 1;
    
    public float StaticFriction => m_physicsMat?.staticFriction ?? 1;
    
    public CharacterCollisionInfo CollisionInfo { get; private set; }
    
    public bool CompareTag(string tag) => GroundObject && GroundObject.CompareTag(tag);
    public bool CompareTag(TagHandle tag) => GroundObject && GroundObject.CompareTag(tag);

    public bool CompareLayer(int layerIdx) => GroundObject && GroundObject.layer == layerIdx;
    
    private PhysicsMaterial m_physicsMat;

    private Vector3 m_contactNormal;
    private Vector3 m_stableNormal;
    
    public static implicit operator GroundInfo(CharacterActor motor)
    {
        return new GroundInfo()
        {
            GroundObject = motor.GroundObject,
            Collider = motor.GroundCollider3D,
            CollisionInfo = motor.CharacterCollisionInfo,
            IsStable = motor.IsStable,
            AnyGround = motor.IsGrounded,
            IsOnEdge = motor.IsOnEdge,
            EdgeAngle = motor.EdgeAngle,
            Point = motor.GroundContactPoint,
            m_physicsMat = motor.GroundCollider3D ? motor.GroundCollider3D.material : null,
            m_contactNormal = motor.GroundContactNormal,
            m_stableNormal = motor.GroundStableNormal,
        };
    }

    public void SetNoGround()
    {
        GroundObject = null;
        Collider = null;
        Point = Vector3.zero;
        m_contactNormal = Vector3.up;
        m_stableNormal = Vector3.up;
        IsStable = false;
        AnyGround = false;
        IsOnEdge = false;
        EdgeAngle = 0f;
        CollisionInfo = default;
        m_physicsMat = null;
    }
}
