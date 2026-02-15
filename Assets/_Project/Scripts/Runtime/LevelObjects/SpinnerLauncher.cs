using System;
using System.Collections;
using Drawing;
using FS.Attributes;
using FS.CameraSystem;
using FS.GameplayActions;
using FS.Math;
using FS.Utility;
using PrimeTween;
using Sirenix.OdinInspector;
using TimeUtils;
using UnityEngine;

/// <summary>
/// Attach player to an end-point and rotate them around a center.
/// - Rotation Speed dependent on entry speed, within a min-max range
/// - Launch velocity dependent on point in arc it was done, and rotation speed
/// </summary>
[LevelDesignCategory("Level Objects/Spinner Launcher")]
public class SpinnerLauncher : LevelObjectBase
{
    public const float k_minSpinnerSpeed = 180f;
    public const float k_maxSpinnerSpeed = 720f;
    
    [SerializeField, Range(1, 50)] public float m_spinnerLength = 10;
    [Required, SerializeField] public Transform m_spinnerEnd;

    // 1-interactor limit
    private Context m_activeContext;

    private float m_spinnerSpeed;
    private Vector3 m_spinAxis;
    
    private Tween m_resetTween;
    private Quaternion m_startRotation;
    
#if UNITY_EDITOR
    public void OnValidate()
    {
        if (m_spinnerEnd)
        {
            m_spinnerEnd.localPosition = new Vector3(0, -m_spinnerLength, 0);
            m_spinnerEnd.localRotation = Quaternion.identity;
            m_spinnerEnd.localScale = Vector3.one;
        }
    }
#endif

    private void Awake()
    {
        m_startRotation = transform.rotation;
    }

    private class SpinnerCamera : CameraFX // lowk would be great to add blending to these, just need quick adjustments to camera state shit
    {
        public override Mode FXMode => Mode.OrbitVector;
    
        public float m_fadeDuration = 0.5f;
        private float m_distance;
    
        private bool m_stopRequest;
        protected override void OnStartFX()
        {
            m_stopRequest = false;
        }
    
        private TimeSince m_sinceStopRequest;
        public void BeginStop()
        {
            m_stopRequest = true;
            m_sinceStopRequest = 0;
        }
    
        public override void OnUpdateFX()
        {
            if (m_stopRequest)
            {
                float alpha = Mathf.Clamp01(m_sinceStopRequest / m_fadeDuration);
                m_distance = Mathf.Lerp(m_distance, m_cameraVector.Distance, alpha);
                if (alpha >= 1) StopFX();
            }
            else
            {
                float alpha = Mathf.Clamp01(TimeSinceStarted / m_fadeDuration);
                m_distance = Mathf.Lerp(m_cameraVector.Distance, 8f, alpha);
            }
            m_cameraVector.Distance = m_distance;
        }
    }
    
    // private class SpinnerCamera : CameraFX // lowk would be great to add blending to these, just need quick adjustments to camera state shit
    // {
    //     public override Mode FXMode => Mode.CameraState;
    //
    //     public float m_fadeDuration = 1.5f;
    //     public Vector3 m_spinAxis;
    //     public Vector3 m_spinOrigin;
    //     private float m_distance;
    //
    //     public void StartSpinnerCamera(CameraController cameraController, Vector3 spinOrigin, Vector3 spinAxis)
    //     {
    //         cameraController.AddCameraFX(this);
    //         m_spinAxis = spinAxis;
    //         spinOrigin = m_spinOrigin;
    //     }
    //
    //     private bool m_stopRequest;
    //     protected override void OnStartFX()
    //     {
    //         m_stopRequest = false;
    //     }
    //
    //     private TimeSince m_sinceStopRequest;
    //     public void BeginStop()
    //     {
    //         m_stopRequest = true;
    //         m_sinceStopRequest = 0;
    //     }
    //
    //     public override void OnUpdateFX()
    //     {
    //         if (m_stopRequest)
    //         {
    //             float alpha = Mathf.Clamp01(m_sinceStopRequest / m_fadeDuration);
    //             m_distance = Mathf.Lerp(m_distance, m_cameraVector.Distance, alpha);
    //             if (alpha >= 1) StopFX();
    //         }
    //         else
    //         {
    //             float alpha = Mathf.Clamp01(TimeSinceStarted / m_fadeDuration);
    //             m_distance = Mathf.Lerp(m_cameraVector.Distance, 12f, alpha);
    //         }
    //         m_cameraVector.Distance = m_distance;
    //     }
    // }

    private SpinnerCamera m_cameraFX = new SpinnerCamera();

    protected override void OnPhysicsActorEnter(Context context)
    {
        if (m_activeContext != null) return; // Already have an active context, can't allow 2
        
        m_activeContext = context;
        
        // Pulley could be tweening back down, cancel that and we start from its current position
        if (m_resetTween.isAlive)
            m_resetTween.Stop();
        
        // Maybe a good way with this is to *tween* the pulley, and "attach" the physics actor to it?
        context.physics.HeadPosition = m_spinnerEnd.position;
        context.physics.UnGround();
        
        // TODO: This doesnt work, also we need better handling of action constraints, esp since a lot of these things might share constraints (block physics actions or block all actions)
        context.physics.AttachmentParent = m_spinnerEnd;
        
        // Cancel actions
        if (context.actionController)
            context.actionController.DisableActions();

        BeginInteraction(context);
        
        // Get rotation speed from angular velocity relation omega = v / r
        m_spinAxis = context.transform.right.ProjectOnPlane(context.physics.GravityDir);
        m_spinnerSpeed = Mathf.Clamp(Mathf.Rad2Deg * context.physics.Velocity.ProjectOnPlane(m_spinAxis).magnitude / m_spinnerLength, k_minSpinnerSpeed, k_maxSpinnerSpeed);
        
        // Figure out sign based on angle against forward (later we want complete degrees of freedom, prolly using the players right as axis of rotation is best, or vel x grav)
        //m_spinnerSpeed *= -Mathf.Sign(context.physics.Velocity.Dot(transform.forward));

        context.cameraController.AddCameraFX(m_cameraFX);

        context.physics.Velocity = Vector3.zero; // Zero out velocity to avoid weirdness
        
        StartInteractionCoroutine(context, DoSpinnerLoop());
    }

    private IEnumerator DoSpinnerLoop()
    {
        var prevpos = m_activeContext.physics.Position;
        while (!m_activeContext.PlayerInput.GetButton(GameInput.Jump))
        {
            transform.Rotate(-m_spinAxis, m_spinnerSpeed * Time.deltaTime, Space.World);
            prevpos = m_activeContext.physics.Position;
            m_activeContext.physics.HeadPosition = m_spinnerEnd.position;
            m_activeContext.physics.Rotation = Quaternion.LookRotation(-transform.up.Cross(m_spinAxis), transform.up);
            yield return Yields.WaitForFixedUpdate;
        }

        m_activeContext.PlayerInput.ConsumeInput(GameInput.Jump);
        
        // Launch in linear vel dir
        var dir = (m_activeContext.physics.Position - prevpos).normalized;
        float speed = Mathf.Deg2Rad * m_spinnerSpeed * m_spinnerLength;
        m_activeContext.physics.Velocity = dir * speed;
        
        // Complete
        EndInteraction(m_activeContext);
    }

    protected override void OnInteractionCleanup(Context context)
    {
        if (m_activeContext.actionController) m_activeContext.actionController.EnableActions();
        m_activeContext.physics.AttachmentParent = null; // Lmao this doesnt work i never set it up woooooooooops

        // Dont reset context instantly, basically block usage of this for the next lil-bit
        Tween.Delay(0.1f, () => m_activeContext = null);
        
        // Tween back to default rotation
        m_resetTween = Tween.RotationAtSpeed(transform.root, m_startRotation, m_spinnerSpeed, Ease.OutQuad);
        
        // Detach camera
        m_cameraFX.BeginStop();
    }
    
#if UNITY_EDITOR
    public override void DrawGizmos()
    {
        using var thickness = Draw.WithLineWidth(3);
        Draw.Line(transform.position, transform.position - transform.up * m_spinnerLength, Color.crimson);

        if (!m_spinnerEnd)
        {
            Draw.Label2D(transform.position + Vector3.up, "ERROR! No Pulley End!", 24f, LabelAlignment.Center, Color.red);
        }
    }
#endif    
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(SpinnerLauncher))]
public class SpinnerLauncherEditor : UnityEditor.Editor
{
    private void OnSceneGUI()
    {
        SpinnerLauncher spinner = (SpinnerLauncher)target;

        if (!spinner.m_spinnerEnd) return;
        
        var handleRot = Quaternion.LookRotation(-spinner.transform.up, spinner.transform.forward);
        var newHeight = FS.MeshProcessing.Editor.HandlesUtility.LinearScaleHandle(spinner.transform.position, handleRot, spinner.m_spinnerLength);
        if (!Mathf.Approximately(newHeight, spinner.m_spinnerLength))
        {
            UnityEditor.Undo.RecordObject(spinner, "Change Spinner Length!");
            spinner.m_spinnerLength = newHeight;
            spinner.OnValidate();
            UnityEditor.EditorUtility.SetDirty(spinner);
        }
    }
}
#endif