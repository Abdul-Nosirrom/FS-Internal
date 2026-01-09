using System;
using FS.GameplayActions;
using UnityEngine;

public class SpinDashAction : GameplayAction, IActionPhysicsReciever
{
    public override ActionChannel Channels => ActionChannel.PhysicsLateral;

    private bool m_inputReleased;

    protected override bool StartCondition()
    {
        return m_physics.IsGrounded && m_input.GetButton(GameInput.Dash);
    }

    public override void OnStart()
    {
        m_input.ConsumeInput(GameInput.Dash);
        m_inputReleased = false;
    }

    public void UpdateVelocity()
    {
        if (!m_inputReleased)
        {
            m_inputReleased = m_input.GetButtonRelease(GameInput.Dash);
            if (m_inputReleased)
            {
                m_physics.Velocity += m_physics.transform.forward * 35 * Mathf.Clamp01(m_timeSinceStarted / 1f);
            }
        }

        if (m_inputReleased)
        {
            var moveInput = m_physics.MoveInput();
            if (moveInput.sqrMagnitude > 0)
            {
                m_physics.Velocity =
                    Vector3.Slerp(m_physics.Velocity.normalized, m_physics.MoveInput().normalized, 5f * Time.deltaTime) *
                    m_physics.Velocity.magnitude;
            }

            m_physics.SlopePhysics(m_physics.GroundNormal);
            
            if (m_physics.Velocity.sqrMagnitude <= 0) EndAction();
            if (m_input.GetButton(GameInput.Dash))
            {
                m_input.ConsumeInput(GameInput.Dash);
                EndAction();
            }
        }
        else
        {
            m_physics.Velocity = Vector3.Lerp(m_physics.Velocity, Vector3.zero, 2 * Time.deltaTime);        
        }
    }

    public void OnLostGround()
    {
        EndAction();
    }
}