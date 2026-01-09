using System.Collections;
using FS.Animation;
using FS.GameplayActions;
using FS.Utility;
using TimeUtils;
using UnityEngine;

public class RailGrindAction : GameplayAction, IActionPhysicsReciever, IActionUpdateReciever
{
    public override ActionChannel Channels => ActionChannel.Physics | ActionChannel.Rotation;

    [SerializeField, Range(0, 1f)] private float m_swapTime = 0.9f;
    [SerializeField, Range(0, 0.5f)] private float m_positionAlignDuration = 0.1f;

    protected RailFeeler m_feeler;
    
    SplineFollower m_grindFollower = new SplineFollower();

    public float Speed
    {
        get => m_grindFollower.m_speed;
        set => m_grindFollower.m_speed = value;
    }

    public Vector3 Normal => m_grindFollower.Normal;
    public Vector3 RailPosition => m_grindFollower.Position;
    
    private IAnimation m_railAnimation;
    private IAnimation m_railSwapAnimation;

    public enum GrindState
    {
        Default,
        Swap,
        Rebound
    }

    public enum OrientationState
    {
        Regular, Zipline
    }

    [RuntimeData] private Vector3 m_initialSwapOffsetVector;
    [RuntimeData] private GrindState m_state = GrindState.Default;
    [RuntimeData] private OrientationState m_orientationState = OrientationState.Regular;
    [RuntimeData] private TimeSince m_sinceSwapStarted;
    [RuntimeData] private TimeSince m_sinceOrientationChanged;
    
    public GrindState State
    {
        get => m_state;
        set
        {
            m_state = value;
            if (m_state == GrindState.Default) m_railAnimation?.Play(m_animator);
            else if (m_state == GrindState.Swap)
            {
                m_sinceSwapStarted = 0f;
                m_railSwapAnimation?.Play(m_animator);
                if (m_railSwapAnimation != null && m_railSwapAnimation.TryGetState(m_animator, out var state)) state.NormalizedTime = 0f;
            }
        }
    }
    public OrientationState GrindOrientation
    {
        get => m_orientationState;
        set
        {
            if (value == m_orientationState) return;
            m_orientationState = value;
            m_sinceOrientationChanged = 0;
        }
    }
    
    public override void OnInitialize(GameObject owner)
    {
        m_feeler = owner.GetComponentInChildren<RailFeeler>();

        if (m_animator)
        {
            //m_railAnimation = m_animator.GetAnimationSet<ActionsAnimationSet>()?.RailGrindBase;
            //m_railSwapAnimation = m_animator.GetAnimationSet<ActionsAnimationSet>()?.RailSwap;
        }

        m_grindFollower.OnReachedEnd += EndAction;
    }

    protected override bool StartCondition()
    {
        bool canRailGrind = !m_physics.IsGrounded && m_timeSinceEnded > 0.4f && m_feeler.TryGetRailSpline(out m_grindFollower.m_spline);
        canRailGrind &= !m_physics.CheckCharacterCapsule(m_physics.Position + m_physics.transform.forward * m_physics.CapsuleRadius); // Ensure there's no wall right in front of us
        return canRailGrind;
    }
    
    public override void OnStart()
    {
        m_railAnimation?.Play(m_animator);

        m_grindFollower.Init(m_grindFollower.m_spline, m_physics);

        State = GrindState.Default;
        m_orientationState = OrientationState.Regular;
    }

    public override void OnEnd()
    {
        m_railAnimation?.GetState(m_animator).Layer.StartFade(0);
        m_physics.TrySnapToGround();
    }

    public void OnUpdate()
    {
        if (m_physics.WallContactCollision.gameObject && State != GrindState.Rebound && Vector3.Dot(m_physics.WallContactCollision.normal, m_physics.transform.forward) < 0)
        {
            if (State == GrindState.Default)
            {
                State = GrindState.Rebound;
                StartCoroutine(PerformRebound());
            }
            else
            {
                EndAction();
                return;
            }
        }

        if (State == GrindState.Rebound) return;
        
        TryRailSwap();
        TryChangeZipOrientation();
        
        // Update swap-state & orientation, putting them in an offset-vector before finally applying it
        
        // Skip alignment when we're swapping rails, it fucks w/ it given that we're attempting to offset our position and assume alignment
        float t = m_positionAlignDuration == 0 || State == GrindState.Swap ? 1 : m_timeSinceStarted / 0.25f;
        Vector3 fromPos = m_physics.FootPosition;
        Vector3 targetPoint = m_grindFollower.Position;
        Vector3 offsetVector = Vector3.zero;

        // Handle zip-grind orientation switches
        float targetOrientationOffset = 0;
        float startOrientationOffset = -m_physics.CapsuleHeight;
        if (GrindOrientation == OrientationState.Zipline)
        {
            targetOrientationOffset = -m_physics.CapsuleHeight;
            startOrientationOffset = 0;
        }

        offsetVector = Normal * Mathf.Lerp(startOrientationOffset, targetOrientationOffset,
            Mathf.Clamp01(m_sinceOrientationChanged / 0.2f));
        
        // Our rail swap is done by caching our offset vector when we start the swap, and then lerping it to zero, adding it
        // to our target point. This way we can smoothly transition to the new rail without interrupting motion along the 
        // rail tangent. Simple and easy
        if (State == GrindState.Swap)
        {
            float railSwapT = m_railSwapAnimation?.GetState(m_animator)?.NormalizedTime ?? m_sinceSwapStarted / m_swapTime;
            offsetVector += Vector3.Lerp(m_initialSwapOffsetVector, Vector3.zero, railSwapT);
            
            // Add basic parabolic motion (its really as simple as this)
            var parabolicOffset = Vector3.up * Mathf.Sin(railSwapT * Mathf.PI) * 0.5f;
            offsetVector += parabolicOffset;
            
            if (railSwapT >= 1) State = GrindState.Default;
        }

        //offsetVector += m_grindFollower.Normal * m_orientationOffset;

        targetPoint = Vector3.Lerp(fromPos, targetPoint, t) + offsetVector;
        m_physics.FootPosition = (targetPoint);
    }

    private IEnumerator PerformRebound()
    {
        var wallEjectAnim = m_animator.GetAnimationSet<ActionsAnimationSet>().SpringKickWallEject;
        var animState = wallEjectAnim.Play(m_animator, FSAnimationLayer.Action);
        animState.Time = 0.2f;
        animState.Weight = 1f;
        
        m_physics.Rotation = Quaternion.AngleAxis(180f, m_physics.transform.up) * m_physics.Rotation;

        var dirSign = m_grindFollower.DirectionSign;
        while (animState.Time < 0.55f)
        {
            if (!IsActive) yield break;
            m_grindFollower.m_speed = 0f;
            wallEjectAnim.Play(m_animator, FSAnimationLayer.Action);
            yield return Yields.WaitForFixedUpdate;
        }

        m_grindFollower.m_speed = 20f * -dirSign;
        State = GrindState.Default;
    }

    private void TryRailSwap()
    {
        if (State == GrindState.Default)
        {
            // Can we rail swap?
            if (m_input && m_input.GetButton(GameInput.Jump))
            {
                var dir = m_physics.MoveInput();

                if (m_feeler.TryGetRailSwap(dir, out var swapSpline, out var swapPoint))
                {
                    m_grindFollower.Init(swapSpline, m_physics, swapPoint);
                    m_initialSwapOffsetVector =
                        (GrindOrientation == OrientationState.Regular
                            ? m_physics.FootPosition
                            : m_physics.HeadPosition) - m_grindFollower.Position;
                    State = GrindState.Swap;
                    m_input.ConsumeInput(GameInput.Jump);
                }
            }
        }
    }
    
    private void TryChangeZipOrientation()
    {
        // Can't swap orientation while in the middle of some transition state
        if (State != GrindState.Default) return;

        if (GrindOrientation == OrientationState.Zipline && !IsZipGrindPositionClear())
        {
            GrindOrientation = OrientationState.Regular;
        }
        
        // If the current grind direction is very vertical, then we wanna make sure we're in Default orientation
        float grindAngle = Vector3.Angle(m_grindFollower.Direction, m_physics.UpDirection);
        if (grindAngle <= 30f)
            GrindOrientation = OrientationState.Regular; // Force
        else if (m_sinceOrientationChanged > 0.2f) // TODO: obviously magic numbers rn
        {
            // Input based, just flip-flop between em
            if (m_input.GetButton(GameInput.VertSkip))
            {
                // First check, can we even go 
                if (GrindOrientation == OrientationState.Regular && !IsZipGrindPositionClear())
                    return;
                
                GrindOrientation = GrindOrientation == OrientationState.Regular ? OrientationState.Zipline : OrientationState.Regular;
                m_input.ConsumeInput(GameInput.VertSkip);
            }
        }
    }

    private bool IsZipGrindPositionClear() // Checks if there are collisions
    {
        var queryPos = m_grindFollower.Position - m_grindFollower.Normal * m_physics.CapsuleHeight;
        return !m_physics.CheckCharacterCapsule(queryPos);
    }

    public void UpdateVelocity()
    {
        if (State == GrindState.Rebound) return;
        
        // Update grind speed w/ gravity
        {
            var gravityOnTangent = Vector3.Dot(m_physics.GravityDir * 15, m_grindFollower.Direction * m_grindFollower.DirectionSign);
            m_grindFollower.m_speed += gravityOnTangent * Time.deltaTime;
            m_grindFollower.m_speed = Mathf.Clamp(Mathf.Abs(m_grindFollower.m_speed), 12, 35) * Mathf.Sign(m_grindFollower.m_speed);
        }
        
        m_physics.Velocity = m_grindFollower.Velocity;
        m_grindFollower.UpdateFollower();
    }

    public void UpdateRotation()
    {
        if (State == GrindState.Rebound) return;

        m_physics.Rotation = Quaternion.Slerp(m_physics.Rotation, m_grindFollower.Rotation, Time.deltaTime * 15f);
    }
}