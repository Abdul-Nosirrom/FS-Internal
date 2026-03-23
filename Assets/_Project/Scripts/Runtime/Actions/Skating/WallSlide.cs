using Animancer;
using FS.Animation;
using TimeUtils;
using FS.GameplayActions;
using FS.Math;
using FS.RuntimeDebug;
using UnityEngine;

public class WallSlideConstraint : ActionConstraintBase
{
    // Presumably could put this in lEdgeClimb to make sure we dsont attempt it at a wall we're sliding on
    public override bool EvaluateConstraint(GameplayAction action) => action is not LedgeClimbAction;
}

public class WallSlide : GameplayAction, IActionUpdateReciever, IActionPhysicsReciever, IDebugProvider
{
    public enum State
    {
        Slide, Jump
    }
    
    public override ActionChannel Channels => ActionChannel.Physics;

    [SerializeField] private float m_minSpeed = 8f;
    [SerializeField] private float m_gravityDampenDuration = 1.25f;
    [SerializeField] private Vector2 m_wallJumpForce = new Vector2(10f, 5f);
    
    [RuntimeData] public State m_state = State.Slide; // Temp public given that theres dependencies and i dont feel like changing it rn
    [RuntimeData] private TimeSince m_timeSinceWallJumped;
    [RuntimeData] private RaycastHit m_wallInfo;

    /// <summary> Cached position we jumped from to evaluate whether re-entry into wall-slide on same wall is possible </summary>
    [RuntimeData] private Vector3 m_posOnJump;

    public override ActionConstraintBase DefaultConstraint => m_constraint;
    private readonly WallSlideConstraint m_constraint = ActionConstraintBase.Create<WallSlideConstraint>("Wall Slide Constraint");

    public State WallState
    {
        get => m_state;
        set
        {
            if (!m_wallSlideAnim.IsActive(m_animator)) m_wallSlideAnim?.Play(m_animator);
            
            AnimWallSlideState?.SetFloat(k_animWallSlideDirection, -Mathf.Sign(m_physics.transform.right.Dot(WallNormal)));
            if (value == m_state) return;
            m_state = value;
            if (m_state == State.Slide) 
                AnimWallSlideState?.CrossFade(k_animWallEnterState, m_timeSinceStarted <= 0 ? 0 : 0.25f);
            else
            {
                AnimWallSlideState?.CrossFade(k_animWallJumpState, 0.1f);
                m_posOnJump = m_physics.transform.position;
            }
        }
    }

    #region Animation Properties

    public static readonly int k_animWallSlideDirection = Animator.StringToHash("WallSlide Direction");
    public static readonly int k_animWallEnterState = Animator.StringToHash("Enter");
    public static readonly int k_animWallJumpState = Animator.StringToHash("Jump");

    private IAnimation m_wallSlideAnim;
    public ControllerState AnimWallSlideState => (ControllerState)m_wallSlideAnim?.GetState(m_animator, false);//{ get; private set; }

    public override void OnInitialize(GameObject owner)
    {
        m_wallSlideAnim = m_animator.GetAnimationSet<ActionsAnimationSet>().WallSlide;
    }

    #endregion
    
    protected override bool StartCondition()
    {
        return m_physics.State == PhysicsState.Air && m_timeSinceEnded > 0.2f && DoWallFeeler(); // arbitrary cooldown
    }
    
    public bool DoWallFeeler()
    {
        // Wanna trace radially along the semi-circle on our forward plane (we trace further than our valid dist but then do a dist check)
        float wallTraceDist = 4f;
        float validWallDist = 2f;
        float minEntryAngle = 25f; // The minimum angle between our -vel and the wall normal, below this we consider we went head-first into the wall
        float sweepRadius = m_physics.CapsuleRadius / 1.1f; // shrink it a bit

        bool shouldDoFullSweep = !IsActive || WallState == State.Jump;
        Vector3 traceDir = Vector3.zero;
        var startPos = m_physics.CenterPosition;
        var rightDir = !shouldDoFullSweep ? -m_wallInfo.normal : m_physics.transform.right;

        RaycastHit wallHit;
        
        // Right to left trace rotation sliced
        int numTraces = !shouldDoFullSweep ? 0 : 4;
        bool anyHit = false;
        for (int i = 0; i <= numTraces; i++)
        {
            float t = shouldDoFullSweep ? (float)i / (float)numTraces : 0f;
            Vector3 dir = Quaternion.AngleAxis(180f * t, m_physics.transform.up) * rightDir;
            if (Physics.SphereCast(startPos, sweepRadius, dir, out wallHit, wallTraceDist, 1 << PhysicsLayers.WallSlide))
            {
                if (wallHit.distance > validWallDist) continue;
                
                // If we're in wall-jump, and we'll be hitting the same wall (roughly same normal and same wall obj)
                // don't allow reentry if we're above the point we jumped from to avoid 'wall-climbing'
                if (IsActive && WallState == State.Jump)
                {
                    float verticalPosOnJump = m_posOnJump.Dot(m_physics.UpDirection);
                    float currentVerticalPos = m_physics.transform.position.Dot(m_physics.UpDirection);
                    float heightDelta = currentVerticalPos - verticalPosOnJump;
                    bool isMaybeSameWall = (wallHit.collider == m_wallInfo.collider) &&
                                           (wallHit.normal.Dot(m_wallInfo.normal) > 0.5f); // same collider, roughly same normal
                    bool isValidHeightToReenter = heightDelta <= 1f;
                    
                    if (isMaybeSameWall && !isValidHeightToReenter) continue;
                }
                
                //var angle = Vector3.Angle(wallHit.normal, -m_physics.LateralVelocity.normalized);
                //if (angle < minEntryAngle) continue;
                
                traceDir = dir;
                m_wallInfo = wallHit;
                anyHit = true;
                break;
            }
        }
        if (!anyHit) return false;
        
        // Evaluate the resulting m_wallInfo, should be properly tagged
        //var angle = Vector3.Angle(m_wallInfo.normal, m_physics.Velocity);
        //if (angle >= 90f || angle < 20f) return false;
        //if (angle < minWallEnterAngle) return false; // we're almost barely just facing forward towards the wall, not enough to initiate a slide
        
        // Ensure wall normal is not walkable
        if (m_physics.IsHitStableGround(m_wallInfo)) return false;
        
        // Ensure planar speed against the wall isn't zero
        //var lateralVel = m_physics.Velocity.ProjectOnPlane(m_wallInfo.normal).ProjectOnPlane(m_physics.GravityDir);
        //if (lateralVel.sqrMagnitude < 1f) return false; // we're barely moving along the wall, not enough to initiate a slide'

        
        return true;
    }

    private float DistanceToWall
    {
        get
        {
            var wallPointAlongNormal = m_wallInfo.point.ProjectOnto(WallNormal);
            var physPosAlongNormal = m_physics.transform.position.ProjectOnto(WallNormal);
            return ((wallPointAlongNormal - physPosAlongNormal).Dot(WallNormal)) + 1.5f * m_physics.CapsuleRadius;
        }
    }
    
    public Vector3 WallNormal => m_wallInfo.normal.normalized;
    private TimeSince m_gravityPhaseTimer;
    private bool m_wallJumpInputReleased;

    public override void OnStart()
    {
        m_gravityPhaseTimer = 0;
        WallState = State.Slide;
        m_wallSlideAnim.Play(m_animator);
        //m_physics.Velocity = m_physics.transform.forward.ProjectOnPlane(Vector3.up).ProjectOnPlane(WallNormal).normalized * Mathf.Max(12, m_physics.Velocity.magnitude); // Remove any velocity into the wall
    }

    public void OnUpdate()
    {
        m_wallSlideAnim?.Play(m_animator);
        if (MaybeWallJump())
        {
            WallState = State.Jump;
            m_timeSinceWallJumped = 0;
            m_physics.AddVelocity(WallNormal * m_wallJumpForce.x + m_physics.UpDirection * m_wallJumpForce.y - m_physics.Velocity.ProjectOnto(m_physics.UpDirection));
        }
    }

    public void UpdateVelocity()
    {
        if (m_physics.IsGrounded)
            EndAction();
        else if (WallState == State.Jump)
        {
            JumpPhysics();
        }
        else
        {
            if (!DoWallFeeler()) EndAction();

            SlidePhysics();
            
            // Constrain position
            var targetPos = Vector3.Lerp(m_physics.transform.position, m_physics.transform.position + WallNormal * DistanceToWall, 3f * Time.deltaTime);
            m_physics.SetPosition(targetPos);
        }
    }

    private bool MaybeWallJump()
    {
        if (WallState == State.Jump) return false;
        
        if (m_input.GetButton(GameInput.Jump))
        {
            //m_wallJumpInputReleased = m_input.GetButtonRelease(GameInput.Jump);
            m_wallJumpInputReleased = false;
            m_input.ConsumeInput(GameInput.Jump);
            return true;
        }

        return false;
    }

    private void JumpPhysics()
    {
        // Raycast along vel direction
        m_wallJumpInputReleased |= m_input.GetButtonRelease(GameInput.Jump);
        
        var lateralParams = m_physics.LateralPhysicsParams;
        lateralParams.m_acceleration *= m_timeSinceWallJumped < 0.2f ? 0.1f : 1f;
        lateralParams.m_friction *= m_timeSinceWallJumped < 0.05f ? 0.1f : 1f;
        lateralParams.m_airDrag *= m_wallJumpInputReleased ? 0 : 0.5f;
        lateralParams.m_airDeceleration *= 0.1f;
        
        var verticalParams = m_physics.VerticalPhysicsParams;
        verticalParams.m_upGravity *= m_wallJumpInputReleased ? 1f : 0.5f;
        
        m_physics.LateralPhysics(lateralParams);
        m_physics.VerticalPhysics(verticalParams);

        // Don't let us push into the wall while we're going up, to prevent wall climbing (no need anymore w/ our height reentry check
        // if (!m_physics.IsFalling)
        // {
        //     var velAlongWall = m_physics.Velocity.Dot(WallNormal);
        //
        //     if (velAlongWall < 0)
        //     {
        //         m_physics.Velocity = m_physics.Velocity.WithAxis(WallNormal, 0);
        //     }
        // }

        float jumpAlpha = m_timeSinceWallJumped;//Easing.Evaluate(m_timeSinceWallJumped / 1f, Ease.OutCirc);
        //float acceleration = Mathf.Lerp(5f, 0f, jumpAlpha);
        //m_physics.SetPosition(m_physics.transform.position + WallNormal * acceleration * deltaTime);
        
        if (jumpAlpha >= 1)// || (DoWallFeeler() && jumpAlpha > 0.7f)) 
            EndAction();

        if (jumpAlpha > 0.2f && DoWallFeeler())
        {
            WallState = State.Slide;
            m_gravityPhaseTimer = 0;
        }
    }
    
    private void SlidePhysics()
    {
        var velOnWall = m_physics.Velocity.ProjectOnPlane(WallNormal);//currentVelocity.ProjectOnPlane(m_wallInfo.normal);
        
        var verticalVel = velOnWall.ProjectOnto(m_physics.GravityDir);
        var lateralVel = velOnWall.ProjectOnPlane(m_physics.GravityDir).normalized * m_physics.LateralVelocity.magnitude;

        {
            float wallSlideSpeed = lateralVel.magnitude;
            if (wallSlideSpeed < m_minSpeed)
                wallSlideSpeed = Mathf.Lerp(wallSlideSpeed, m_minSpeed, 10f * Time.deltaTime);
            lateralVel = lateralVel.normalized * wallSlideSpeed;
        }
        {
            if (m_gravityPhaseTimer < m_gravityDampenDuration)
                verticalVel = Vector3.Lerp(verticalVel, Vector3.zero, 4f * Time.deltaTime);
            else
            {
                float t = Mathf.Clamp01((m_gravityPhaseTimer - m_gravityDampenDuration)/2f);
                float gravScale = Mathf.Lerp(8f, 35f, t);
                verticalVel += m_physics.GravityDir * gravScale * Time.deltaTime;
            }
        }
        m_physics.Velocity = lateralVel + verticalVel;
        //currentVelocity.y = Mathf.Lerp(currentVelocity.y, 0, 0.5f * deltaTime);
        //currentVelocity = currentVelocity.normalized * m_minSpeed;
    }

    #region IDebugProvider

    public string DebugName => "WallSlide";

    public void OnDebugGUI()
    {
        GUILayout.Label($"State: {WallState}");
        GUILayout.Label($"Wall Normal: {m_wallInfo.normal}");
        
        if (WallState == State.Slide)
        {
            GUILayout.Label($"Time Sliding: {(float)m_timeSinceStarted:F2}s");
            GUILayout.Label($"Gravity Phase: {(m_timeSinceStarted < m_gravityDampenDuration ? "Dampened" : "Ramping")}");
        }
        else
        {
            GUILayout.Label($"Time Since Jump: {(float)m_timeSinceWallJumped:F2}s");
            GUILayout.Label($"Can Reattach: {m_timeSinceWallJumped < 0.2f}");
            GUILayout.Label($"Jump Released: {m_wallJumpInputReleased}");
        }
    }

    public void OnDebugDraw()
    {
        var pos = m_physics.transform.position;
        
        // Wall normal
        Drawing.Draw.ingame.Arrow(pos, pos + m_wallInfo.normal * 2f, Color.cyan);
    }

    #endregion
}