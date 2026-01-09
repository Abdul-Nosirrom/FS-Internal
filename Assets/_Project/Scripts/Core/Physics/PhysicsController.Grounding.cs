using System;
using Drawing;
using FS.Math;
using Lightbug.CharacterControllerPro.Core;
using TimeUtils;
using UnityEngine;

// .Grounding.cs
public enum PhysicsState
{
    Ground, 
    Air, 
    Sliding, 
    RailGrind,
    WallSlide
}

public partial class PhysicsController
{
    
    #region Gravity

    /// <summary>
    /// For adventure style gravity, instead of "when on ground, grav = -floorNormal, when in air, slerp it back", we instead do
    /// "when on ground, grav = proper grav, then ON LEAVE GROUND, set it to last floor normal and slerp back". That way when we do momentum shit
    /// on ground we can reliably use gravity
    /// </summary>
    public Vector3 GravityDir
    {
        get => m_overrideGravDir ?? m_gravDir.normalized;
        set => m_overrideGravDir = value.normalized;
    }
    
    private Vector3? m_overrideGravDir = null;
    private Vector3 m_gravDir = Vector3.down;

    public void ResetCustomGravity() => m_overrideGravDir = null;

    public float GravityScale => Velocity.Dot(GravityDir) > 0
        ? VerticalPhysicsParams.m_downGravity
        : VerticalPhysicsParams.m_upGravity;
    
    #endregion
 
    #region Grounding & State
    
    public PhysicsState PrevState { get; private set; }
    public PhysicsState State { get; private set; }
    public event Action<PhysicsState, PhysicsState> OnPhysicsStateChanged;

    public bool IsGrounded => State == PhysicsState.Ground;


    public float GroundDenivelation => m_motor.EdgeAngle;
    public Vector3 GroundNormal => IsRailGrinding ? m_railGrind.Normal : Ground.Normal;

    public Vector3 UpDirection => IsGrounded || IsRailGrinding ? GroundNormal : -GravityDir;

    public bool FoundGroundThisFrame => m_motor.HasBecomeStable;
    public bool LostGroundThisFrame => m_motor.HasBecomeNotGrounded || m_motor.HasBecomeUnstable;
    
    public bool StateChangedThisFrame { get; private set; }
    
    public TimeSince m_timeSinceLostGround { get; private set; }
    public TimeSince m_timeSinceFoundGround { get; private set; }
    public TimeSince m_timeSinceStateChange { get; private set; }

    /// <summary>
    /// Current ground, always up-to-date
    /// </summary>
    public GroundInfo Ground { get; private set; }
    /// <summary>
    /// Last ground (any result that actually detected a ground, might be unstable)
    /// </summary>
    public GroundInfo LastGround { get; private set; }
    /// <summary>
    /// Last ground that was 'stable'
    /// </summary>
    public GroundInfo LastStableGround { get; private set; }

    public event Action OnLandedEvt;
    public event Action OnLostGroundEvt;

    public void UnGround()
    {
        m_motor.ForceNotGrounded();
        
        // Keep ground data up-to-date similar to ForceGround
        UpdatePhysicsState();
        GroundingUpdate();
    }
    
    public bool TrySnapToGround(Vector3 probePosition)
    {
        // If we're already grounded, return true?
        if (m_motor.CurrentState != CharacterActorState.NotGrounded) return true;
        
        // TODO: Our 'state' here is still behind, next physics update is where we'd call 'UpdatePhysicsState'
        var ogPosition = Position;
        Position = probePosition;
        m_motor.ForceGrounded();
        if (m_motor.CurrentState == CharacterActorState.NotGrounded)
        {
            Position = ogPosition; // Reset, no ground was found
            return false;
        }
        
        // Force a grounding update to get up-to-date ground info TODO: Bad, double call - should refactor
        UpdatePhysicsState();
        GroundingUpdate();
        
        return true;
    }
    public bool TrySnapToGround() => TrySnapToGround(Position);

    // Invoked in LateUpdate
    private void UpdatePhysicsState()
    {
        StateChangedThisFrame = false;
        var prevState = State;
        
        if (m_railGrind is { IsActive: true }) State = PhysicsState.RailGrind;
        else if (m_wallSlide is { IsActive: true } && m_wallSlide.m_state != WallSlide.State.Jump) State = PhysicsState.WallSlide;
        else if (m_motor.CurrentState == CharacterActorState.StableGrounded) State = PhysicsState.Ground;
        //else if (LastGround.UnStable && Ground.UnStable && Ground.) State = PhysicsState.Sliding; // TODO: Just checking Ground.Unstable leads to weird quirks where we enter 'sliding' and mess up our velocity upon leaving ground towards a ledge
        else State = PhysicsState.Air;

        if (State != prevState)
        {
            StateChangedThisFrame = true;
            m_timeSinceStateChange = 0;
            PrevState = prevState;
            OnPhysicsStateChanged?.Invoke(prevState, State);
        }
    }

    // Invoked by PostGroundingUpdate
    private float m_finalVertSpeed;

    private void GroundingUpdate()
    {
        if (IsInSkateAction && State != PhysicsState.Ground)
            m_finalVertSpeed = Speed;

        var prevGroundData = Ground;
        GroundInfo newGroundData = m_motor;
        
        // Check denivelation angle if we shold unground before we set ground info
        if (prevGroundData.IsStable && newGroundData.IsStable)
        {
            // If the dot product is positive, it conveys a change in direction that is 
            var isConvex = Velocity.Dot(newGroundData.Normal) > 0;
            var denivelation = Vector3.Angle(newGroundData.Normal, prevGroundData.Normal);

            float upwardDenivlation = prevGroundData.CompareLayer(PhysicsLayers.Vert) ? 15f : 45f; // Denivelation threshold when moving upwards
            float downwardDenivlation = 45; // Denivelation threshold when moving downwards
                
            float denivelationThreshold = newGroundData.GroundSlopeAngle < prevGroundData.GroundSlopeAngle // Going up -> Current more aligned w/ vertical 
                ? upwardDenivlation 
                : downwardDenivlation;
                
            if (denivelation > denivelationThreshold && isConvex)
            {
                UnGround();
            }
        }
        
        // Update grounding state & invoke events on ground recievers
        {
            if (Ground.IsStable) LastStableGround = Ground;
            if (Ground.AnyGround) LastGround = Ground;
            Ground = m_motor;

            // Invoke ground events
            {
                bool isNewGround = Ground.GroundObject != prevGroundData.GroundObject || (Ground.IsStable && !prevGroundData.IsStable);
                if (isNewGround && Ground.IsStable && 
                    Ground.GroundObject.TryGetComponent<GroundEventBroadcaster>(out var groundEvts))
                    groundEvts.OnBecomeGroundForController(this);
                if (isNewGround && prevGroundData.IsStable &&
                    prevGroundData.GroundObject.TryGetComponent<GroundEventBroadcaster>(out var lostGroundEvts))
                    lostGroundEvts.OnNoLongerGroundForController(this);
            }
        }
        
        // Update slopeLimit based on state
        {
            // m_motor.slopeLimit = 55f;
            // if ((m_motor.GroundCollider3D && Ground.CompareLayer(PhysicsLayers.Vert)) || IsInSkateAction)
            // {
            //     m_motor.slopeLimit = 90f;
            // }
            // else if (m_motor.IsStable && Velocity.magnitude > m_lateralPhysicsParams.m_maxSpeed+2)
            // {
            //     m_motor.slopeLimit = 180f;
            // }
        }
        
        // Call up grounding events 
        if (StateChangedThisFrame)
        {
            if (PrevState == PhysicsState.Ground && State == PhysicsState.Air) OnLostGround();
            else if (PrevState == PhysicsState.Air && State == PhysicsState.Ground) OnFoundGround();
        }

        // Check edge angle / denivelation & lose ground if so
        if (false && m_motor.IsOnEdge)
        {
            float edgeAngle = m_motor.EdgeAngle;
            // Is it a \_ or /-? We only wanna lose if case 2 (normal goes from less aligned w/ grav to more aligned w/ grav
            if (Velocity.Dot(-GravityDir) > 0 && edgeAngle > 35f) m_motor.ForceNotGrounded();
        }
    }
    
    private void OnFoundGround()
    {
        // Only reset if we found stable ground
        m_timeSinceFoundGround = 0;

        if (m_actionController)
        {
            foreach (var action in m_actionController.IterateActiveActions<IActionPhysicsReciever>())
                action.OnFoundGround();
        }
        
        if (Ground.CompareLayer(PhysicsLayers.Vert)) Debug.LogError($"Vert Landing Speed Change: {m_finalVertSpeed} -> {Speed}");
        
        OnLandedEvt?.Invoke();
    }

    private void OnLostGround()
    {
        // Only set timer if last ground is stable as thats when we really "lost ground"
        m_timeSinceLostGround = 0;

        if (m_actionController)
        {
            foreach (var action in m_actionController.IterateActiveActions<IActionPhysicsReciever>())
                action.OnLostGround();
        }
        
        OnLostGroundEvt?.Invoke();

        // Much better to attempt to start it from here, guarantees frame 0 start (shouldn't do it like this rn tho as our "state" isn't properly set
        m_vert?.TryStartAction();
    }
    #endregion
}