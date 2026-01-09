using Drawing;
using FS.CameraSystem;
using FS.GameplayActions;
using FS.Player;
using UnityEngine;


[PlayerOnly]
public class RailGrindBoostController : MonoBehaviour
{
    private PhysicsController m_physics;
    private RailGrindAction m_railGrind;
    private PlayerInputSystem m_input;
    private Camera m_playerCamera;

    private void Start()
    {
        m_physics = GetComponentInParent<PhysicsController>();
        m_railGrind = GetComponentInChildren<RailGrindAction>();
        m_input = this.GetPlayerSystem<PlayerInputSystem>();
        m_playerCamera = this.GetPlayerSystem<PlayerCameraSystem>()?.Camera;

        if (m_railGrind)
        {
            m_railGrind.OnActionStarted += InitBoostParams;
        }
    }

    [RuntimeData] private bool m_isBoosting;
    [RuntimeData] private float m_balanceAngle; // [-90f, 90f]
    [RuntimeData] private float m_balanceDrift => Mathf.Lerp(25f, 180f, Mathf.Abs(m_balanceAngle)/90f) * Mathf.Sign(m_balanceAngle);
    private float m_inputBalanceSpeed => Mathf.Max(Mathf.Abs(m_balanceDrift) * 2f, 50f);
    
    private void InitBoostParams(GameplayAction action)
    {
        m_isBoosting = false;
        m_balanceAngle = 0f;
    }

    private void LateUpdate()
    {
        if (!m_railGrind || !m_railGrind.IsActive) return;

        if (m_input.GetButton(GameInput.Dash))
        {
            m_isBoosting = !m_isBoosting;
            m_input.ConsumeInput(GameInput.Dash);
        }

        if (m_isBoosting)
        {
            UpdateRailBoosting();
        }
    }

    private void UpdateRailBoosting()
    {
        CalclateBalance();
        AccelerateBoostSpeed();
        DrawBalanceMeter();

        if (Mathf.Abs(m_balanceAngle) >= 90f) m_railGrind.TryEndAction();
    }

    private void CalclateBalance()
    {
        var inputDir = m_input.MoveVector.x;
        m_balanceAngle += inputDir * m_inputBalanceSpeed * Time.deltaTime;
        m_balanceAngle += m_balanceDrift * Time.deltaTime;
    }

    private void AccelerateBoostSpeed()
    {
        float boostAccel = 20f;
        m_railGrind.Speed += Mathf.Sign(m_railGrind.Speed) * boostAccel * Time.deltaTime;
    }

    private void DrawBalanceMeter()
    {
        using var thickness = Draw.ingame.WithLineWidth(4);
        
        Color col = Color.Lerp(Color.forestGreen, Color.crimson, Mathf.Abs(m_balanceAngle)/90f);
        
        var drawPos = m_physics.transform.position + m_physics.transform.up * m_physics.CapsuleHeight * 1.1f;
        
        var arcCenter = drawPos - m_physics.transform.up * 0.1f;
        var arcLeft = drawPos - m_playerCamera.transform.right;
        var arcRight = drawPos + m_playerCamera.transform.right;
        
        Draw.ingame.Arc(arcCenter, arcLeft, arcRight, col);
        
        
        var angleLerp = (m_balanceAngle + 90f) / 180f;
        angleLerp = Mathf.Clamp01(angleLerp);
        
        var vec1 = arcLeft - arcCenter;
        var vec2 = arcRight - arcCenter;
        var arcPoint = arcCenter + Vector3.Slerp(vec1, vec2, angleLerp);
        
        // Draw balance indicator

        Draw.ingame.SolidCircle(arcPoint, m_playerCamera.transform.forward, 0.1f, Color.orangeRed);
        Draw.ingame.Circle(arcPoint, m_playerCamera.transform.forward, 0.1f, Color.black);
    }
}