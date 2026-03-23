using FS.GameplayActions;
using FS.Math;
using FS.Player;
using PrimeTween;
using TimeUtils;
using UnityEngine;

[PlayerOnly]
public class SpringStompBounce : GameplayAction, IActionPhysicsReciever
{
    public override ActionChannel Channels => ActionChannel.Physics;
    public override bool IsRetriggerable => false;

    [Range(2, 8)] public float m_bounceHeight = 5f;
    public Transform m_skin;
    
    private enum StompState { Falling, Bounce, StompLand }
    private StompState m_state = StompState.Falling;
    private TimeSince m_sinceBounced;

    private bool m_inputReleased;
    
    private Sequence m_squashTween;
    
    public float BounceSpeed => Mathf.Sqrt(2 * m_physics.VerticalPhysicsParams.m_upGravity * m_bounceHeight); 
    //private float m_bounceSpeed;
    
    public GameInput StompInput => GameInput.Dash;
    
    protected override bool StartCondition()
    {
        return m_input.GetButton(StompInput) && m_physics.State == PhysicsState.Air;
    }

    public override void OnStart()
    {
        if (m_squashTween.isAlive) m_squashTween.Stop();
        m_skin.localScale = Vector3.one;
        m_input.ConsumeInput(StompInput);
        m_inputReleased = m_input.GetButtonRelease(StompInput);
        
        m_state = StompState.Falling;

        m_physics.GetComponent<CameraController>().AddCameraFX<CameraHoldPosition>();
        
        m_physics.Velocity *= 0.7f; // Dampen current velocity
        m_physics.VerticalVelocity = m_physics.GravityDir * 40f;
    }

    public void OnFoundGround()
    {
        if (true || m_inputReleased)
        {
            // if (m_physics.Ground.CompareLayer(PhysicsLayers.Vert) && m_physics.Ground.GroundSlopeAngle > 30f)
            // {
            //     m_physics.Velocity = Vector3.ProjectOnPlane(m_physics.Velocity, m_physics.GroundNormal).normalized * m_bounceSpeed;
            //     EndAction();
            //     return;
            // }
            
            // Stomp squash - quick and snappy
            m_squashTween = Sequence.Create(Tween.Scale(m_skin, Vector3.one, new Vector3(1.4f, 0.2f, 1.4f), 0.08f, Ease.OutQuad)
                .OnComplete(() =>
                {
                    // Stretch during bounce - happens right when velocity is applied
                    m_squashTween = Tween.Scale(m_skin, new Vector3(1.4f, 0.2f, 1.4f), new Vector3(0.7f, 1.3f, 0.7f), 0.15f, Ease.OutBack)
                        .Chain(Tween.Scale(m_skin, Vector3.one, 0.2f, Ease.InOutQuad));

                    if (!IsActive) return; // In case action was ended during tween
                    
                    m_sinceBounced = 0f;
                    m_physics.UnGround();
                    m_physics.VerticalVelocity = m_physics.UpDirection * BounceSpeed;
                    m_state = StompState.Bounce;
        
                    EndAction();
                }));
        }
        else
        {
            m_physics.Velocity = Vector3.zero;
            m_state = StompState.StompLand;
            EndAction();
        }
    }

    public void UpdateVelocity()
    {
        m_inputReleased = m_inputReleased || m_input.GetButtonRelease(StompInput);

        if (m_state == StompState.Bounce)
        {
            if (m_sinceBounced > 0.1f) EndAction();
            var gravParams = m_physics.VerticalPhysicsParams;
            gravParams.SetGravity(6f * gravParams.m_upGravity);
            //m_physics.LateralPhysics(ref currentVelocity, deltaTime);
            m_physics.VerticalPhysics(gravParams);
        }
        else if (m_state == StompState.Falling)
        {
            if (m_physics.State == PhysicsState.Air)
            {
                //m_physics.LateralVelocity *= 0.97f; // TODO: The way we handle passing in currentVelocity makes it impossible to use the velocity properties on our actions
                //m_bounceSpeed = Mathf.Abs(m_physics.VerticalSpeed);
            }
            //m_physics.VerticalPhysics();
        }
    }
}