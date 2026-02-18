using System.Collections;
using System.Linq;
using Drawing;
using FluffyUnderware.Curvy;
using FS.Attributes;
using FS.CameraSystem;
using FS.CombatSystem;
using FS.GameplayActions;
using FS.Math;
using FS.TagSystem;
using FS.Utility;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[LevelDesignCategory("Level Objects/Pole Swing")]
public class PoleSwing : LevelObjectBase
{
    private VirtualCamera m_camera;
    [SerializeField, Range(1, 10)] public float m_length = 4f;
    [SerializeField, Required, ReadOnly] private CurvySpline m_spline;
    [SerializeField] private BoxCollider m_colliderTrigger;
    [SerializeField] private TargetingQuery m_homingTarget;
    private bool m_isOccupied = false;

    [SerializeField, HideInInspector] public Vector3 m_forwardJumpPositionOffset = Vector3.forward;
    [SerializeField, Range(15f, 80f)] public float m_forwardJumpAngle = 30f;

    [SerializeField, HideInInspector] public Vector3 m_backwardJumpPositionOffset = -Vector3.forward;
    [SerializeField, Range(15f, 80f)] public float m_backwardJumpAngle = 30f;
    
    private void Awake()
    {
        GameObject cameraObj = new GameObject("Pole Swing Camera");
        m_camera = cameraObj.AddComponent<VirtualCamera>();
        cameraObj.transform.SetParent(transform, false);
        cameraObj.transform.localPosition = Vector3.zero;
        cameraObj.transform.localRotation = Quaternion.identity;
        cameraObj.transform.localScale = Vector3.one;
    }
    
#if UNITY_EDITOR
    public void OnValidate()
    {
        if (Application.isPlaying) return; // Only do this in editor (and not in OnEnable, which is called on play mode start)

        m_spline = GetComponentInChildren<CurvySpline>();
        
        if (m_spline == null)
        {
            m_spline = CurvySpline.Create();
            m_spline.gameObject.name = "PoleSwingSpline";
            m_spline.transform.SetParent(transform, false);
        }
        
        if (m_spline.ControlPointCount != 3) m_spline.SetControlPointCount(3);

        m_spline.ControlPointsList[0].transform.localPosition = Vector3.zero;
        m_spline.ControlPointsList[1].transform.localPosition = Vector3.right * (m_length / 2f);
        m_spline.ControlPointsList[2].transform.localPosition = Vector3.right * m_length;
        
        // Ensure spline is set to non-editable
        m_spline.gameObject.hideFlags = HideFlags.NotEditable;
        foreach (var child in m_spline.ControlPointsList) child.gameObject.hideFlags = HideFlags.NotEditable;
        
        if (m_colliderTrigger)
        {
            m_colliderTrigger.size = new Vector3(m_length, 0.5f, 0.5f);
            m_colliderTrigger.center = new Vector3(m_length / 2f, 0, 0);
        }
        
        if (m_homingTarget)
            m_homingTarget.transform.localPosition = new Vector3(m_length / 2f, 0, 0);
    }
#endif

    protected override void OnPhysicsActorEnter(Context context)
    {
        if (m_isOccupied) return; // already occupied
        if (!context.IsPlayer) return;
        
        m_isOccupied = true;

        // Reset style jumps
        context.physics.ResetActivationsUnder(Tag.Action.Activation.StyleJumps);
        
        context.actionController.DisableActions();
        BeginInteraction(context);

        StartInteractionCoroutine(context, PoleSwingRoutine(context));
    }

    private float m_swingDirection;
    private float m_positionAlongPoleAxis;

    private void InitPoleSwingState(Context context)
    {
        // Figure out start direction (pole is along the 'right' vector)
        m_swingDirection = Mathf.Sign(context.transform.right.Dot(transform.right));
        m_positionAlongPoleAxis = Vector3.Dot(context.physics.HeadPosition - transform.position, transform.right);
        
        GetCameraPositionAndRotation(m_positionAlongPoleAxis, m_swingDirection, out var pos, out var rot);
        m_camera.m_cameraState.m_position = pos;
        m_camera.m_cameraState.m_rotation = rot;
        m_camera.m_cameraState.m_fov = 70f;
        m_camera.m_cameraState.m_nearClipPlane = 0.01f;
        m_camera.m_cameraState.m_farClipPlane = 1000f;
    }

    private void GetCameraPositionAndRotation(float posAlongPole, float swingDirection, out Vector3 pos, out Quaternion rot)
    {
        var worldPolePos = transform.position + transform.right * posAlongPole;
        var forwardDir = transform.forward * swingDirection;

        var lookDir = (forwardDir * 2f + transform.up * 0.5f).normalized;
        
        var lookAtPos = worldPolePos + lookDir * 2f;
        
        pos = worldPolePos - forwardDir * 4 + 3 * transform.right * m_swingDirection;
        
        lookDir = (lookAtPos - pos).normalized;
        rot = Quaternion.LookRotation(lookDir);//, transform.up);
    }

    private IEnumerator PoleSwingRoutine(Context context)
    {
        try
        {
            InitPoleSwingState(context);
            context.physics.VelocityCalculationsEnabled = false;
            context.physics.RotationCalculationsEnabled = false;
            var basisUp = transform.up;
            context.PlayerCamera.BlendToCamera(m_camera, CameraBlendParams.Create(0.35f, Ease.InOutQuad, true));
            float currentAngle = Vector3.Angle(context.transform.up, basisUp);
            
            while (true)
            {
                if (context.PlayerInput.GetButton(GameInput.Interact)) // exit input, just stop the interaction and let the player drop
                {
                    context.PlayerInput.ConsumeInput(GameInput.Interact);
                    break;
                }

                if (context.PlayerInput.GetButton(GameInput.Jump))
                {
                    context.PlayerInput.ConsumeInput(GameInput.Jump);
                    LaunchToNextPole(context);
                    break;
                }
                
                // Rotate around 
                // Angle: [0, 180], 0 = hanging down, 180 = upside down, determines speed (pendulum energy)
                float dotUp = Vector3.Dot(context.transform.up, basisUp);
                float normalizedAngle = Mathf.InverseLerp(-1, 1, dotUp);
                float angularSpeed = Mathf.Lerp(180f, 700f, Easing.Evaluate(normalizedAngle, Ease.OutQuad));
                currentAngle += angularSpeed * Time.deltaTime;
                
                // Determine player rotation
                Vector3 upDir = Quaternion.AngleAxis(-m_swingDirection * currentAngle, transform.right) * basisUp;
                Quaternion targetRot = Quaternion.LookRotation(Vector3.Cross(m_swingDirection * transform.right, upDir), upDir);
               
                var polePos = transform.position + transform.right * m_positionAlongPoleAxis - upDir * context.physics.CapsuleHeight;
                context.physics.Teleport(polePos, targetRot);

                yield return Yields.WaitForFixedUpdate;
            }
        }
        finally
        {
            context.PlayerCamera.PopVirtualCamera(m_camera, CameraBlendParams.Create(0.45f, Ease.InOutQuad, true));
            context.cameraController.m_cameraVector.Yaw = context.cameraController.AimYaw(transform.forward * m_swingDirection);
            EndInteraction(context);
            context.physics.VelocityCalculationsEnabled = true;
            context.physics.RotationCalculationsEnabled = true;
            context.actionController.EnableActions();
            m_isOccupied = false;
        }
    }

    private void LaunchToNextPole(Context context)
    {
        // Each pole has 2 possible connections of which only 1 is valid depending on the starting direction, if no connection just do a typical projectile launch, otherwise launch towards the next pole
        // then the next pole interaction will trigger. First there's a 'valid angle' region where we will actually connect, otherwise we just drop
        // Figure out launch position (forward or back)
        var launchOffset = m_swingDirection >= 0 ? m_forwardJumpPositionOffset : m_backwardJumpPositionOffset;
        var launchPos = context.transform.position + transform.rotation * (launchOffset);

        float validAngle = m_swingDirection >= 0 ? m_forwardJumpAngle : m_backwardJumpAngle;
        
        ProjectileMotion.LaunchPhysicsControllerWithLaunchAngle(context.physics, context.physics.Position, launchPos, 20f, validAngle, out _, out _);
        LaunchSpring.s_frontTuckAnim.Play(context.animator);
    }
    
#if UNITY_EDITOR
    private Vector3[] m_gizmoArcPoints = new Vector3[20];
    public override void DrawGizmos()
    {
        GetCameraPositionAndRotation(m_length/2f, 1f, out var pos, out var rot);
        Draw.SolidBox(pos, rot, Vector3.one * 0.5f, Color.dodgerBlue);
        Draw.Arrow(pos, pos + rot * Vector3.forward, Color.softRed);
        
        GetCameraPositionAndRotation(m_length/2f, -1f, out  pos, out  rot);
        Draw.SolidBox(pos, rot, Vector3.one * 0.5f, Color.dodgerBlue);
        Draw.Arrow(pos, pos + rot * Vector3.forward, Color.softRed);
        
        // Draw projectile trajectory arcs for both jump positions
        if (UnityEditor.Selection.Contains(gameObject))
        {
            var startPos = transform.position + transform.right * (m_length / 2f);
            var forwardPos = startPos +
                             transform.rotation * m_forwardJumpPositionOffset;
            var backwardPos = startPos +
                              transform.rotation * m_backwardJumpPositionOffset;
            
            var forwardVel = ProjectileMotion.CalculateLaunchVelocityWithRelativeAngle(startPos, forwardPos, 20f * Vector3.down, m_forwardJumpAngle, out var tf);
            ProjectileMotion.GetTrajectoryPoints(startPos, forwardVel, 20f * Vector3.down, tf, m_gizmoArcPoints);
            Draw.DashedPolyline(m_gizmoArcPoints.ToList(), 0.25f, 0.25f);
            Draw.WireSphere(forwardPos, 0.5f, Color.red);
            
            var backwardVel = ProjectileMotion.CalculateLaunchVelocityWithRelativeAngle(startPos, backwardPos, 20f * Vector3.down, m_backwardJumpAngle, out tf);
            ProjectileMotion.GetTrajectoryPoints(startPos, backwardVel, 20f * Vector3.down, tf, m_gizmoArcPoints);
            Draw.DashedPolyline(m_gizmoArcPoints.ToList(), 0.25f, 0.25f);
            Draw.WireSphere(backwardPos, 0.5f, Color.red);
        }
    }
#endif     
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(PoleSwing))]
public class PoleSwingEditor : UnityEditor.Editor
{
    private void OnSceneGUI()
    {
        PoleSwing poleSwing = (PoleSwing)target;

        var poleSwingCenter = poleSwing.transform.position + poleSwing.transform.right * (poleSwing.m_length / 2f);
        var forwardPos = poleSwingCenter +
                         poleSwing.transform.rotation * poleSwing.m_forwardJumpPositionOffset;
        var backwardPos = poleSwingCenter +
                          poleSwing.transform.rotation * poleSwing.m_backwardJumpPositionOffset;
        
        // Do jump angle handles
        float newForwardAngle = FS.MeshProcessing.Editor.HandlesUtility.ArcAngleHandle(poleSwingCenter,
            Quaternion.LookRotation(poleSwing.transform.forward, -poleSwing.transform.right), 2f, poleSwing.m_forwardJumpAngle, 80f);
        float newBackwardAngle = FS.MeshProcessing.Editor.HandlesUtility.ArcAngleHandle(poleSwingCenter,
            Quaternion.LookRotation(-poleSwing.transform.forward, poleSwing.transform.right), 2f, poleSwing.m_backwardJumpAngle, 80f);
        //if (newForwardAngle != poleSwing.m_forwardJumpAngle)
        {
            //UnityEditor.Undo.RecordObject(poleSwing, "Change Forward Jump Angle");
            poleSwing.m_forwardJumpAngle = newForwardAngle;
            //UnityEditor.EditorUtility.SetDirty(poleSwing);
        }
        //if (newBackwardAngle != poleSwing.m_backwardJumpAngle)
        {
            //UnityEditor.Undo.RecordObject(poleSwing, "Change Backward Jump Angle");
            poleSwing.m_backwardJumpAngle = newBackwardAngle;
            //UnityEditor.EditorUtility.SetDirty(poleSwing);
        }
        
        // Forward position handle
        var newForwardPos = UnityEditor.Handles.DoPositionHandle(forwardPos, poleSwing.transform.rotation);
        var newBackwardPos = UnityEditor.Handles.DoPositionHandle(backwardPos, poleSwing.transform.rotation);
        if (newForwardPos != forwardPos)
        {
            UnityEditor.Undo.RecordObject(poleSwing, "Move Forward Jump Position Offset");
            poleSwing.m_forwardJumpPositionOffset = Quaternion.Inverse(poleSwing.transform.rotation) * (newForwardPos - poleSwingCenter);
            poleSwing.m_forwardJumpPositionOffset.x = 0f;
            poleSwing.m_forwardJumpPositionOffset.z = Mathf.Max(poleSwing.m_forwardJumpPositionOffset.z, 1f);
            UnityEditor.EditorUtility.SetDirty(poleSwing);
        }

        if (newBackwardPos != backwardPos)
        {
            UnityEditor.Undo.RecordObject(poleSwing, "Move Backward Jump Position Offset");
            poleSwing.m_backwardJumpPositionOffset = Quaternion.Inverse(poleSwing.transform.rotation) * (newBackwardPos - poleSwingCenter);
            poleSwing.m_backwardJumpPositionOffset.x = 0f;
            poleSwing.m_backwardJumpPositionOffset.z = Mathf.Min(poleSwing.m_backwardJumpPositionOffset.z, -1f);
            UnityEditor.EditorUtility.SetDirty(poleSwing);
        }
    }
}
#endif