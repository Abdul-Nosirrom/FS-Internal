using TimeUtils;
using FS.GameplayActions;
using FS.Math;
using FS.RuntimeDebug;
using UnityEngine;

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
    
    [RuntimeData] public State m_state { get; private set; } = State.Slide;
    [RuntimeData] private TimeSince m_timeSinceWallJumped;
    [RuntimeData] private RaycastHit m_wallInfo;

    protected override bool StartCondition()
    {
        return m_physics.State == PhysicsState.Air && m_timeSinceEnded > 0.2f && DoWallFeeler(); // arbitrary cooldown
    }
    
    public bool DoWallFeeler()
    {
        float minTraceDist = 1f;
        float minWallEnterAngle = 20f;
        float sweepRadius = m_physics.CapsuleRadius / 1.1f; // shrink it a bit
        
        Vector3 traceDir = Vector3.zero;
        var startPos = m_physics.transform.position;
        var rightDir = m_physics.transform.right;
        var leftDir = -rightDir;

        RaycastHit wallHit;
        
        // Right to left trace rotation sliced
        int numTraces = 4;
        bool anyHit = false;
        for (int i = 0; i <= numTraces; i++)
        {
            float t = (float)i / (float)numTraces;
            Vector3 dir = Quaternion.AngleAxis(180f * t, m_physics.transform.up) * rightDir;
            if (Physics.SphereCast(startPos, sweepRadius, dir, out wallHit, minTraceDist, 1 << PhysicsLayers.WallSlide))
            {
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

        // Out head & Feet should hit w/ sphere casts
        //bool isHeadHitValid = Physics.SphereCast(m_physics.HeadPosition, sweepRadius, traceDir, out _, minTraceDist, 1 << PhysicsLayers.WallSlide);
        //if (!isHeadHitValid) return false;
        //bool isFeetHitValid = Physics.SphereCast(m_physics.FootPosition, sweepRadius, traceDir, out _, minTraceDist, 1 << PhysicsLayers.WallSlide);
        //if (!isFeetHitValid) return false;
        
        
        // Ensure planar speed against the wall isn't zero
        var lateralVel = m_physics.Velocity.ProjectOnPlane(m_wallInfo.normal).ProjectOnPlane(m_physics.GravityDir);
        if (lateralVel.sqrMagnitude < 1f) return false; // we're barely moving along the wall, not enough to initiate a slide'

        
        return true;
    }

    private float DistanceToWall
    {
        get
        {
            var wallPointAlongNormal = m_wallInfo.point.ProjectOnto(WallNormal);
            var physPosAlongNormal = m_physics.transform.position.ProjectOnto(WallNormal);
            return ((wallPointAlongNormal - physPosAlongNormal).Dot(WallNormal)) + 1.1f * m_physics.CapsuleRadius;
        }
    }
    
    public Vector3 WallNormal => m_wallInfo.normal.normalized;
    private TimeSince m_gravityPhaseTimer;
    private bool m_wallJumpInputReleased;

    public override void OnStart()
    {
        m_state = State.Slide;
        m_gravityPhaseTimer = 0;
        m_physics.Velocity = m_physics.Velocity.ProjectOnPlane(WallNormal).normalized * m_physics.Velocity.magnitude; // Remove any velocity into the wall
    }


    public void OnUpdate()
    {
        if (MaybeWallJump())
        {
            m_state = State.Jump;
            m_timeSinceWallJumped = 0;
            m_physics.AddVelocity(WallNormal * m_wallJumpForce.x + m_physics.UpDirection * m_wallJumpForce.y - m_physics.Velocity.ProjectOnto(m_physics.UpDirection));
        }
    }

    public void UpdateVelocity()
    {
        
        if (m_physics.IsGrounded)
            EndAction();
        else if (m_state == State.Jump)
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
        if (m_state == State.Jump) return false;
        
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
        verticalParams.m_upGravity *= m_wallJumpInputReleased ? 2f : 1f;
        
        m_physics.LateralPhysics(lateralParams);
        m_physics.VerticalPhysics(verticalParams);

        // Don't let us push into the wall while we're going up, to prevent wall climbing
        if (!m_physics.IsFalling)
        {
            var velAlongWall = m_physics.Velocity.Dot(WallNormal);

            if (velAlongWall < 0)
            {
                m_physics.Velocity = m_physics.Velocity.WithAxis(WallNormal, 0);
            }
        }

        float jumpAlpha = m_timeSinceWallJumped;//Easing.Evaluate(m_timeSinceWallJumped / 1f, Ease.OutCirc);
        //float acceleration = Mathf.Lerp(5f, 0f, jumpAlpha);
        //m_physics.SetPosition(m_physics.transform.position + WallNormal * acceleration * deltaTime);
        
        if (jumpAlpha >= 1)// || (DoWallFeeler() && jumpAlpha > 0.7f)) 
            EndAction();

        if (jumpAlpha > 0.2f && DoWallFeeler())
        {
            m_state = State.Slide;
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
        GUILayout.Label($"State: {m_state}");
        GUILayout.Label($"Wall Normal: {m_wallInfo.normal}");
        
        if (m_state == State.Slide)
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