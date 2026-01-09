using System;
using System.Collections.Generic;
using Drawing;
using FS.CameraSystem;
using FS.GameplayActions;
using FS.Math;
using FS.Player;
using FS.Rendering;
using Sirenix.OdinInspector;
using TimeUtils;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public struct JumpPhase
{
    [HorizontalGroup("A")]
    [BoxGroup("A/Gravity")]
    [Range(0, 100)]
    public float m_gravity;
    [BoxGroup("A/Gravity")]
    [Range(0, 100)]
    public float m_fallingGravityInputReleased;
    
    [HorizontalGroup("A")]
    [BoxGroup("A/Phase End")]
    [Range(0, 1)]
    public float m_phaseEndTime;

    [BoxGroup("A/Phase End")]
    public bool m_endPhaseWithVerticalSpeed;
    [BoxGroup("A/Phase End")]
    public float m_phaseEndVerticalSpeed;

    [BoxGroup("Input")]
    [Range(0,1)]
    [LabelWidth(250)]
    public float m_minTimeToRespondToButtonRelease;
    [BoxGroup("Input")]
    [LabelWidth(200)]
    public bool m_endPhaseOnButtonRelease;
}

[PlayerOnly]
public class JumpAction : GameplayAction, IActionPhysicsReciever, IActionInputEvaluator, IActionGizmoReciever
{
    public override ActionChannel Channels => ActionChannel.PhysicsVertical;
    
    public float m_jumpSpeed = 15;
    public float m_fullStickBoost = 4;
    
    [InfoBox("$JumpInfoBoxMsg")]
    public List<JumpPhase> m_jumpPhases = new();
    [RuntimeData] private int m_currentJumpPhase = 0;
    private string JumpInfoBoxMsg => $"Height ~= {EstJumpHeight} m\nTime ~= {EstJumpTimeToApex} s";
    [RuntimeData] public float EstJumpHeight => m_jumpPhases.Count > 0 ? (m_jumpSpeed * m_jumpSpeed) / (2 * m_jumpPhases[0].m_gravity) : -1;
    [RuntimeData] public float EstJumpTimeToApex => m_jumpPhases.Count > 0 ? m_jumpSpeed / m_jumpPhases[0].m_gravity : -1;

    [Header("Effects")] 
    public VFXController m_jumpVFX;
    
    private PlayerCameraSystem m_camera;

    [RuntimeData] private TimeSince m_timeSinceCurrentPhase;
    [RuntimeData] private TimeSince m_timeSinceInputReleased;
    [RuntimeData] private bool m_bInputReleased;
    
    public override void OnInitialize(GameObject owner)
    {
        // NOTE: Moved OnInitialize to be called during Start not awake to ensure all subsystems are initialized
        // OnAwake is when this is called, subsystems are not guaranteed to be initialized need to fix
        m_camera = PlayerManager.Instance.GetSubsystem<PlayerCameraSystem>(owner);
    }
    
    public override void OnStart()
    {
        m_input.ConsumeInput(GameInput.Jump);
        
        // Jump direction: Try always going straight up, unless the surface normal is 'upside down' if so jump away from it
        Vector3 jumpDir = m_physics.Ground.CompareLayer(PhysicsLayers.Vert) || (-m_physics.GravityDir).Dot(m_physics.UpDirection) > 0
            ? -m_physics.GravityDir : m_physics.UpDirection;
        m_physics.AddVelocity(jumpDir * (m_jumpSpeed + Mathf.Max(m_physics.Velocity.Dot(m_physics.GravityDir), 0)));
        m_physics.UnGround();

        m_timeSinceCurrentPhase = 0;
        m_currentJumpPhase = 0;

        Vector3 input = m_physics.MoveInput();
        if (input.sqrMagnitude > 0.7f && m_physics.Velocity.magnitude + m_fullStickBoost < 7)
        {
            m_physics.AddVelocity(input.normalized * m_fullStickBoost);
        }

        m_bInputReleased = false;

        m_phaseChangedThisFrame = false;
        prevPos = m_physics.transform.position;
        phaseChangePositions.Clear();

        m_vertEverActive = m_physics.IsInSkateAction;
        
        // Spawn jump fx at last ground position and rotation & attach trail to character
        if (m_jumpVFX != null)
        {
            var verticalScale = 1f;
            if (m_physics.Speed > m_physics.LateralPhysicsParams.m_maxSpeed) verticalScale = 1.5f;
            VFXParams spawnParams = new VFXParams()
            {
                // If we're on rail-grind or otherwise, last ground might be very old
                m_localPositionOffset = m_physics.State == PhysicsState.Air ? m_physics.LastStableGround.Point : m_physics.Position,
                // Rotate in velocity direction, such that up vector is velocity dir
                m_localRotationOffset = Quaternion.FromToRotation(Vector3.up, (m_physics.VelocityDirection + jumpDir)/2f),
                m_localScale = new Vector3(1, verticalScale, 1)
            };
            VFXManager.Instance.PlayVFX(m_jumpVFX, spawnParams);
        }
    }

    public bool InputEvaluate() => m_input.GetButton(GameInput.Jump);
    protected override bool StartCondition()
    {
        // TODO: Consolidate JumpAction into JumpBase as an abstract class so we can use its logic in other 'jump' related actions
        return m_physics.State != PhysicsState.Air || 
               (m_physics.State == PhysicsState.Air && m_physics.PrevState != PhysicsState.WallSlide && m_physics.m_timeSinceStateChange < 0.2);
    }

    private bool m_vertEverActive = false;
    public void UpdateVelocity()
    {
        // For some reason there's a bug w/ the jump where isActive is set to true even tho its ended????? Its not even in "ActiveActions" but IsActive flag is true!!!!
        if (m_physics.IsGrounded)
        {
            EndAction();
            return;
        }

        m_vertEverActive = m_vertEverActive || m_physics.IsInSkateAction;
        if (/*m_physics.Velocity.Dot(m_physics.GravityDir) > -1 &&*/ m_vertEverActive)
        {
            // cancel out jump if we are falling and in vert
            if (!m_physics.IsFalling) m_physics.VerticalSpeed *= 0.8f;
            EndAction();
            return;
        }
        // Update gravity
        //if (currentVelocity.y > 0)
        {
            if (m_input.GetButtonRelease(GameInput.Jump) && !m_bInputReleased)
            {
                m_bInputReleased = true;
                m_timeSinceInputReleased = 0;
            }
        }

        UpdatePhase();

        if (m_currentJumpPhase > m_jumpPhases.Count - 1)
        {
            EndAction();
            return;
        }
        
        JumpPhase phase = m_jumpPhases[m_currentJumpPhase];
        float gravity = m_bInputReleased && m_timeSinceInputReleased > phase.m_minTimeToRespondToButtonRelease 
            ? phase.m_fallingGravityInputReleased :  phase.m_gravity;
        
        var verticalParams = m_physics.VerticalPhysicsParams;
        verticalParams.m_upGravity = verticalParams.m_downGravity = gravity;

        m_physics.VerticalPhysics(verticalParams);
        
        //m_physics.Velocity += gravity * m_physics.GravityDir * Time.deltaTime;
        
        //m_physics.VerticalPhysics(ref currentVelocity, deltaTime);
        prevPos = m_physics.transform.position;
    }

    private Vector3 prevPos;
    private bool m_phaseChangedThisFrame = false;
    private List<Vector3> phaseChangePositions = new();
    private void UpdatePhase()
    {
        JumpPhase phase = m_jumpPhases[m_currentJumpPhase];

        if (m_bInputReleased && m_timeSinceInputReleased > phase.m_minTimeToRespondToButtonRelease)
        {
            if (phase.m_endPhaseOnButtonRelease)
            {
                m_currentJumpPhase++;
                m_timeSinceCurrentPhase = 0;
                phaseChangePositions.Add(m_physics.transform.position);
                return;
            }
        }
        
        if (phase.m_endPhaseWithVerticalSpeed)
        {
            float verticalSpeed = Vector3.Dot(m_physics.Velocity, -m_physics.GravityDir);
            if (verticalSpeed < phase.m_phaseEndVerticalSpeed)
            {
                m_currentJumpPhase++;
                m_timeSinceCurrentPhase = 0;
                phaseChangePositions.Add(m_physics.transform.position);
                return;
            }
        }
        else if (m_timeSinceCurrentPhase > phase.m_phaseEndTime)
        {
            m_currentJumpPhase++;
            m_timeSinceCurrentPhase = 0;
            phaseChangePositions.Add(m_physics.transform.position);
            return;
        }
    }


    public void OnDrawActionGizmos(bool isSelected)
    {
        using var thickness = Draw.ingame.WithLineWidth(2f);

        if (!IsActive)
        {
            if (m_timeSinceEnded < 10f)
            {
                foreach (var phasePos in phaseChangePositions)
                    Draw.ingame.WireSphere(phasePos, 0.3f);
            }
            return;
        }
        
        {
            Color labelColor = m_bInputReleased ? Color.red : Color.green;
            Draw.ingame.Label2D(m_physics.transform.position + Vector3.up,
                $"Released: {m_bInputReleased} | Since: {m_timeSinceInputReleased}", labelColor);
        }

        {
            using var drawDuration = Draw.ingame.WithDuration(10f);

            Random.InitState(m_currentJumpPhase);

            Draw.ingame.Line(prevPos, m_physics.transform.position, Random.ColorHSV());
        }
        
        foreach (var phasePos in phaseChangePositions)
            Draw.ingame.WireSphere(phasePos, 0.3f, Color.red);
    }

}