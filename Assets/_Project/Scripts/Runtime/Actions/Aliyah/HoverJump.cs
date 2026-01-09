using System;
using FS.Animation;
using FS.GameplayActions;
using FS.Math;
using FS.Player;
using PrimeTween;
using TimeUtils;
using UnityEngine;

[Serializable]
public class HoverJump : GameplayAction, IActionPhysicsReciever
{
    public override ActionChannel Channels => ActionChannel.PhysicsVertical;
    
    [RuntimeData] private bool m_jumpReset = true;

    // These are multiples of gravity
    [Tooltip("How much vertical boost to give on hover jump? Final speed will be max(0, startVertical) + verticalBoost * (1-pctTimeSpentFalling)")]
    [SerializeField] private float m_verticalBoost = 6f;
    [SerializeField] private float m_hoverDuration = 0.75f;
    [SerializeField] private float m_minHoverTime = 0.25f;
    [SerializeField, Range(0, 1)] private float m_fallGraceDuration = 0.2f;
    
    [Header("Constraints")]
    [SerializeField] private float m_maxSpeedMultiplier = 1.5f; // Hard cap at 1.5x boost
    [SerializeField] private float m_softCapMultiplier = 1.2f; // Soft cap starts at 1.2x

    [Header("Visual")] [SerializeField] private AnimationCurve m_animSpeedCurve = AnimationCurve.Constant(0, 1, 1);

    /// <summary>
    /// Once input is released, we exit out if we've surpassed the min hover time
    /// </summary>
    // [SerializeField, Range(0, 2)] private float m_minHoverTime = 0.2f;
    // [SerializeField, Range(0, 2)] private float m_hoverUpTime = 1f;
    //
    // [RuntimeData] private TimeSince m_sinceStartedHoveredUp;
    
    [RuntimeData] private bool m_hasInputReleased;
    [RuntimeData] private float m_startVerticalSpeed;
    
    private IAnimation m_hoverJumpAnimation;

    

    public override void OnInitialize(GameObject owner)
    {
        m_physics.OnPhysicsStateChanged += TryResetJumpCounter;
        m_hoverJumpAnimation = m_animator.GetAnimationSet<ActionsAnimationSet>().WhipArmHoverJump;
    }

    private void TryResetJumpCounter(PhysicsState prevState , PhysicsState newState)
    {
        if (newState == PhysicsState.Air) m_jumpReset = true;
    }
    
    protected override bool StartCondition()
    {
        return m_input.GetButton(GameInput.Jump) && m_jumpReset && m_physics.State == PhysicsState.Air;
    }

    private float m_timeStoppedFalling = 0f;
    
    public override void OnStart()
    {
        m_hoverJumpAnimation.Play(m_animator);
        
        m_startVerticalSpeed = m_physics.VerticalSpeed;
        m_input.ConsumeInput(GameInput.Jump);
        m_hasInputReleased = false;
        m_jumpReset = false;
        m_timeStoppedFalling = 0f;
        
        // Slight speed boost in movement direction for momentum
        if (m_physics.LateralSpeed > 0.1f)
        {
            m_physics.LateralSpeed *= 1.15f; // 15% boost
        }
    }

    public override void OnEnd()
    {
        if (m_hoverJumpAnimation.TryGetState(m_animator, out var hoverJumpState))
        {
            Tween.Custom(hoverJumpState, hoverJumpState.Speed, 0, 0.2f, (state, v) => state.Speed = v, Ease.OutQuad);
        }
        m_hoverJumpAnimation.Stop(m_animator, 0.7f);
    }

    public void UpdateVelocity()
    {
        m_hasInputReleased |= m_input.GetButtonRelease(GameInput.Jump);

        if (m_hoverJumpAnimation.TryGetState(m_animator, out var hoverJumpState))
        {
            float speed = m_animSpeedCurve.Evaluate(m_timeSinceStarted / m_hoverDuration);
            hoverJumpState.Speed = speed;
        }
    
        if (m_timeSinceStarted >= m_hoverDuration)
        {
            EndAction();
            return;
        }
    
        if (m_physics.IsFalling)
        {
            // IMMEDIATE catch - kill fall speed faster
            float alpha = Mathf.Clamp01(m_timeSinceStarted / (m_fallGraceDuration * 0.5f)); // Faster catch
            alpha = Easing.Evaluate(alpha, Ease.OutCubic); // Sharper curve
            m_physics.VerticalSpeed = Mathf.Lerp(m_startVerticalSpeed, m_verticalBoost * 0.3f, alpha); // Pop up slightly
            m_timeStoppedFalling = m_timeSinceStarted;
        }
        else
        {
            float hoverTime = m_timeSinceStarted - m_timeStoppedFalling;
            float alpha = Mathf.Clamp01(hoverTime / m_hoverDuration);
        
            // Two-phase hover: quick rise -> plateau
            float targetSpeed;
            if (alpha < 0.4f) // Rising phase
            {
                float riseAlpha = alpha / 0.4f;
                riseAlpha = Easing.Evaluate(riseAlpha, Ease.OutQuad); // Fast start, ease out
                targetSpeed = Mathf.Lerp(m_verticalBoost * 0.3f, m_verticalBoost, riseAlpha);
            }
            else // Hover plateau
            {
                float plateauAlpha = (alpha - 0.4f) / 0.6f;
                plateauAlpha = Easing.Evaluate(plateauAlpha, Ease.InOutSine); // Gentle float
                targetSpeed = Mathf.Lerp(m_verticalBoost, m_verticalBoost * 0.2f, plateauAlpha);
            }
        
            float combinedSpeed = Mathf.Max(0, m_startVerticalSpeed) + targetSpeed;
            float softCap = m_verticalBoost * m_softCapMultiplier;
            float hardCap = m_verticalBoost * m_maxSpeedMultiplier;

            if (combinedSpeed > softCap)
            {
                float excess = combinedSpeed - softCap;
                float maxExcess = hardCap - softCap;
                combinedSpeed = softCap + maxExcess * (1 - Mathf.Exp(-2f * excess / maxExcess));
            }

            m_physics.VerticalSpeed = combinedSpeed;
            
            // Early exit with slight downward impulse for control
            if (m_hasInputReleased && hoverTime > m_minHoverTime)
            {
                m_physics.VerticalSpeed *= 0.6f; // Cut speed on release
                EndAction();
            }
        }
    }

    public void OnFoundGround()
    {
        EndAction();
    }
}