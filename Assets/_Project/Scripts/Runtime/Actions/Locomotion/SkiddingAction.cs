using System.Collections;
using Animancer;
using FS.Animation;
using FS.GameplayActions;
using FS.TagSystem;
using UnityEngine;

public class SkiddingAction : GameplayAction, IActionPhysicsReciever
{
    public override ActionChannel Channels => ActionChannel.Physics | ActionChannel.Animation | ActionChannel.Rotation;
    
    private const float k_minInputAngleForSkidding = 30f;
    //private const float k_minSpeedForSkidding = 4f;
    //private const float k_minSpeedForSkidLaunch = 10f;

    [SerializeField] private AnimationCurve m_skidDecelerationMultiplierCurve = AnimationCurve.Constant(0, 1, 1);
    [Range(5, 50), SerializeField] private float m_skidDeceleration = 20f;
    [Range(1, 10), SerializeField] private float k_minSpeedForSkidLaunch = 8f;
    [Range(1, 10), SerializeField] private float k_minSpeedForSkidding = 6f;

    [RuntimeData] private bool m_isAllowedToBoostOut;
    [RuntimeData] private float m_startSpeed;

    [RuntimeData] private Vector3 m_boostDir;

    private MecanimAnimationAsset m_skidAnim;

    private enum SkidState
    {
        Decelerating,
        TurningAround,
        BoostingOut
    }

    private SkidState m_currentSkidState = SkidState.Decelerating;
    private SkidState CurrentSkidState
    {
        get => m_currentSkidState;
        set
        {
            if (m_currentSkidState != value && value == SkidState.TurningAround)
            {
                if (m_skidAnim?.GetState(m_animator) is ControllerState animState) 
                    animState.SetTrigger("SkidTurnBoost");
            }
            m_currentSkidState = value;
        }
    }
    
    public override void OnInitialize(GameObject owner)
    {
        if (m_animator)
            m_skidAnim = m_animator.GetAnimationSet<ActionsAnimationSet>().Skid;
    }
    
    private bool HasSkidMoveInput
    {
        get
        {
            var currentInputDir = m_physics.MoveInput();
            if (currentInputDir.sqrMagnitude <= 0.1f) return false; // If no input, don't boost out
            float velInputAngle =
                Vector3.Angle(-m_physics.transform.forward, currentInputDir); // Compare input directions
            if (velInputAngle > k_minInputAngleForSkidding) return false;
            return true;
        }
    }

    protected override bool StartCondition()
    {
        if (m_physics.State != PhysicsState.Ground) return false;

        var speed = m_physics.Velocity.magnitude;
        if (speed < k_minSpeedForSkidding) return false;

        return HasSkidMoveInput;
    }
    
    public override void OnStart()
    {
        m_startSpeed = m_physics.Velocity.magnitude;
        m_isAllowedToBoostOut = m_startSpeed >= k_minSpeedForSkidLaunch;

        CurrentSkidState = SkidState.Decelerating;
        m_skidAnim.Play(m_animator).OnFlag(m_animator, Tag.Animation.SkidBoost, OnSkidBoostOut);
        
        m_boostDir = -m_physics.transform.forward;
    }

    public override void OnEnd()
    {
        if (CurrentSkidState == SkidState.Decelerating)
            // Explicitly fade out animation layer because its in a looping state
            m_skidAnim.Stop(m_animator);
    }
    
    private void OnSkidBoostOut()
    {
        // Skid boost time
        CurrentSkidState = SkidState.BoostingOut;
        m_physics.Velocity = (m_boostDir) * Mathf.Max(m_startSpeed, 20f);
        EndAction();
    }

    public void UpdateVelocity()
    {
        if (!m_physics.IsGrounded) 
        {
            EndAction(); // If we lost ground, end skid
            return;
        }
        
        if (CurrentSkidState == SkidState.Decelerating)
        {
            // To boost out, we gotta keep holding the input to imply we wanna turn and move
            m_isAllowedToBoostOut = m_isAllowedToBoostOut && HasSkidMoveInput;

            float decelerationT = Mathf.Clamp01(m_physics.Speed / m_physics.LateralPhysicsParams.m_maxSpeed);
            float decelMultiplier = m_skidDecelerationMultiplierCurve.Evaluate(decelerationT);
            //m_physics.Speed = Mathf.Max(0, Mathf.Lerp(m_startSpeed, 0f, Easing.Evaluate(m_timeSinceStarted / k_skidDuration, Ease.OutQuad)));
            m_physics.Speed = Mathf.Max(m_physics.Speed - decelMultiplier * m_skidDeceleration * Time.deltaTime, 0f);
            if (m_physics.Speed <= 0f)
            {
                if (m_isAllowedToBoostOut) CurrentSkidState = SkidState.TurningAround;
                else EndAction();
            }
        }
    }

    public void UpdateRotation()
    {
        if (CurrentSkidState == SkidState.TurningAround)
            m_physics.Rotation *= m_physics.RootMotionDeltaRotation;
    }
}