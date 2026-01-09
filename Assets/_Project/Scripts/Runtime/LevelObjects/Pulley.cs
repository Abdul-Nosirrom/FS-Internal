using System;
using Drawing;
using FS.Attributes;
using FS.GameplayActions;
using FS.Math;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

// NOTE: We have to put a rigidbody on the gameobject of this comp, so we can put the trigger on pulley_end and still have its trigger events forwarded to the parent
[LevelDesignCategory("Level Objects/Pulleys")]
public class Pulley : LevelObjectBase
{
    public const float k_pulleySpeed = 15;
    public const float k_endLaunchHeight = 4;
    public const float k_minPulleyY = -0.5f;

    [SerializeField] public bool m_startDisabled = false;
    [SerializeField, Range(1, 50)] public float m_pulleyHeight = 10;
    [Required, SerializeField] public Transform m_pulleyEnd;

    // Pulley is limited to 1-interactor, so we keep track of them
    private Context m_activeContext;
    private Tween m_activeTween;
    private Tween m_activePhysicsTween;
    private bool m_isPulleyActive = false;
    
#if UNITY_EDITOR
    public void OnValidate()
    {
        if (m_pulleyEnd)
        {
            m_pulleyEnd.localPosition = new Vector3(0, -m_pulleyHeight, 0);
            m_pulleyEnd.localRotation = Quaternion.identity;
            m_pulleyEnd.localScale = Vector3.one;
        }
    }
#endif

    private void Awake()
    {
        if (m_startDisabled)
        {
            m_isPulleyActive = false;
            m_pulleyEnd.transform.localPosition = new Vector3(0, k_minPulleyY, 0);
        }
        else m_isPulleyActive = true;
    }

    public void ActivatePulley()
    {
        if (m_isPulleyActive) return;
        
        Tween.LocalPositionY(m_pulleyEnd, m_pulleyEnd.localPosition.y, -m_pulleyHeight,
                Mathf.Abs(m_pulleyEnd.localPosition.y + m_pulleyHeight) / k_pulleySpeed, Ease.InQuad)
            .OnComplete(() => m_isPulleyActive = true);
    }

    protected override void OnPhysicsActorEnter(Context context)
    {
        if (!m_isPulleyActive) return;
        if (m_activeContext != null) return; // Already have an active context, can't allow 2
        
        m_activeContext = context;
        
        // Pulley could be tweening back down, cancel that and we start from its current position
        if (m_activeTween.isAlive)
            m_activeTween.Stop();
        
        // Maybe a good way with this is to *tween* the pulley, and "attach" the physics actor to it?
        context.physics.HeadPosition = m_pulleyEnd.position;
        context.physics.UnGround();
        
        // TODO: This doesnt work, also we need better handling of action constraints, esp since a lot of these things might share constraints (block physics actions or block all actions)
        context.physics.AttachmentParent = m_pulleyEnd;
        
        // Cancel actions
        if (context.actionController)
            context.actionController.DisableActions();

        BeginInteraction(context);

        context.physics.Velocity = Vector3.zero; // Zero out velocity to avoid weirdness
        
        float tweenDuration = Mathf.Abs(m_pulleyEnd.localPosition.y + k_minPulleyY) / k_pulleySpeed;
        m_activeTween = Tween.LocalPositionY(m_pulleyEnd, m_pulleyEnd.localPosition.y, k_minPulleyY, tweenDuration, Ease.InQuad).OnComplete(() => EndInteraction(context));
        m_activePhysicsTween = PhysicsTweens.HeadPosition(context.physics, transform.TransformPoint(new Vector3(0, k_minPulleyY, 0)), tweenDuration, Ease.InQuad);
    }

    protected override void OnInteractionCleanup(Context context)
    {
        m_activeTween.Stop();
        m_activePhysicsTween.Stop();

        PulleyCompleted();
    }

    private void PulleyCompleted()
    {
        if (m_activeContext.actionController) m_activeContext.actionController.EnableActions();
        m_activeContext.physics.AttachmentParent = null; // Lmao this doesnt work i never set it up woooooooooops
        ProjectileMotion.LaunchPhysicsControllerToHeight(m_activeContext.physics, k_endLaunchHeight + m_activeContext.physics.HeadPositionOffset.magnitude, out _, out _);
        
        // Dont reset context instantly, basically block usage of this for the next lil-bit
        Tween.Delay(0.1f, () => m_activeContext = null);
        
        // Tween back down
        float tweenDuration = Mathf.Abs(m_pulleyHeight - m_pulleyEnd.localPosition.y) / k_pulleySpeed;
        m_activeTween = Tween.LocalPositionY(m_pulleyEnd, -m_pulleyHeight, tweenDuration, Ease.InQuad);
    }
    
#if UNITY_EDITOR
    public override void DrawGizmos()
    {
        using var thickness = Draw.WithLineWidth(3);
        Vector3 launchPos = transform.position + transform.up * k_endLaunchHeight;
        Draw.DashedLine(transform.position, launchPos, 0.25f, 0.1f);
        Draw.WireSphere(launchPos, 0.5f, Color.forestGreen);
        Draw.Line(transform.position, transform.position - transform.up * m_pulleyHeight, Color.crimson);

        if (!m_pulleyEnd)
        {
            Draw.Label2D(transform.position + Vector3.up, "ERROR! No Pulley End!", 24f, LabelAlignment.Center, Color.red);
        }
    }
#endif    
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(Pulley))]
public class PulleyEditor : UnityEditor.Editor
{
    private void OnSceneGUI()
    {
        Pulley pulley = (Pulley)target;

        if (!pulley.m_pulleyEnd) return;
        
        var handleRot = Quaternion.LookRotation(-pulley.transform.up, pulley.transform.forward);
        var newHeight = FS.MeshProcessing.Editor.HandlesUtility.LinearScaleHandle(pulley.transform.position, handleRot, pulley.m_pulleyHeight);
        if (!Mathf.Approximately(newHeight, pulley.m_pulleyHeight))
        {
            UnityEditor.Undo.RecordObject(pulley, "Change Pulley Height");
            pulley.m_pulleyHeight = newHeight;
            pulley.OnValidate();
            UnityEditor.EditorUtility.SetDirty(pulley);
        }
    }
}
#endif