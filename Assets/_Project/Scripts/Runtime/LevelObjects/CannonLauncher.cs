using System;
using System.Collections;
using System.Linq;
using Drawing;
using FS.Attributes;
using FS.CameraSystem;
using FS.Math;
using FS.Rendering;
using FS.Utility;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
#endif

[LevelDesignCategory("Level Objects/Cannon")]
public class CannonLauncher : LevelObjectBase
{
    public Vector2 m_launchEndPoint = new Vector2(5, 0);
    public float m_launchAngle = 45f;
    
    public Vector3 LaunchEndPointWS => transform.TransformPoint(new Vector3(0, m_launchEndPoint.y, m_launchEndPoint.x));
    
    // TODO: make a custom editor for VFXController variables to display a dropdown (make it if it has an attribute)
    [SerializeField, VFXDropDown] private VFXController m_launchVFX;
    [SerializeField, VFXDropDown] private VFXController m_playerLaunchTrailVFX;
    
    [SerializeField] private VirtualCamera m_aimCamera;
    [SerializeField] private Transform m_aimYaw;
    [SerializeField] private Transform m_aimPitch;

    private bool m_isOccupied;
    private bool IsOccupied
    {
        get => m_isOccupied;
        set
        {
            m_isOccupied = value;
            m_trigger.enabled = !m_isOccupied;
        }
    }
    
    private Collider m_trigger;
    private Quaternion m_cachedPitch;
    private const float k_launchGravity = 25f;

    private void Awake()
    {
        m_cachedPitch = m_aimPitch.localRotation;

        foreach (var childCollider in GetComponentsInChildren<Collider>())
        {
            if (childCollider.isTrigger)
            {
                m_trigger = childCollider;
                break;
            }
        }
    }

    protected override void OnPhysicsActorEnter(Context context)
    {
        if (IsOccupied) return;
        if (!context.IsPlayer) return;
        
        BeginInteraction(context);
        StartInteractionCoroutine(context, DoCannonLaunch(context));
    }

    private IEnumerator DoCannonLaunch(Context context)
    {
        try
        {
            // Setup initial params
            IsOccupied = true;
            context.actionController.DisableActions();
            context.physics.VelocityCalculationsEnabled = false;
            context.physics.RotationCalculationsEnabled = false;
            
            context.meshVisibilityController.HideFullMesh(0.25f);
            
            context.PlayerCamera.BlendToCamera(m_aimCamera, CameraBlendParams.Create(0.35f, Ease.InOutQuad, true));
            
            // First settle the player properly into position & visually
            
            // Second, get the launch vector and aim the pitch towards it
            var launchVector = ProjectileMotion.CalculateLaunchVelocityWithRelativeAngle(context.physics.Position, LaunchEndPointWS, context.physics.GravityDir * k_launchGravity, m_launchAngle, out var launchTf);
            var launchRotDelta = Quaternion.FromToRotation(m_aimPitch.up, launchVector);
            var aimTween = Tween.Rotation(m_aimPitch, launchRotDelta * m_aimPitch.rotation, 0.75f, Ease.OutQuad);

            // {
            //     using var duration = Draw.ingame.WithDuration(10f);
            //     using var thickness = Draw.ingame.WithLineWidth(3);
            //     Draw.ingame.Arrow(m_aimYaw.position,
            //         m_aimYaw.position + aimRot * Vector3.forward * 4f,
            //         Color.red);
            //     Draw.ingame.Arrow(m_aimYaw.position,
            //         m_aimYaw.position + aimRot * Vector3.up * 4f,
            //         Color.green);
            // }

            while (aimTween.isAlive)
            {
                // Sync camera TODO: I realize our virtual camera shit is kinda dogshit, it has its own transform which makes it annoying
                m_aimCamera.m_cameraState.m_position = m_aimCamera.transform.position;
                m_aimCamera.m_cameraState.m_rotation = m_aimCamera.transform.rotation;
                
                context.physics.Position = m_trigger.transform.position - m_trigger.transform.up;
                context.physics.Rotation = transform.rotation;
                context.physics.Speed = 0;

                yield return Yields.WaitForNextFrame;
            }
            
            // Third, prep the launch 'punch' and launch the player/do FX
            var launchAnimSettings = new ShakeSettings()
            {
                strength = -m_aimPitch.forward,
                duration = 0.5f,
                frequency = 10f,
                enableFalloff = true,
                falloffEase = Ease.OutSine
            };
            var launchTween = Tween.PunchScale(m_aimPitch, launchAnimSettings);
                        
            // Play VFX
            if (m_launchVFX)
            {
                VFXManager.Instance.PlayVFX(m_launchVFX, new() { m_parent = m_aimPitch, m_localPositionOffset = Vector3.up * 2.6f, m_localRotationOffset = Quaternion.identity }); // TODO: Explicitly passing in rotation offset because spawnRot gets fucked for some reason
            }
            
            yield return Yields.WaitForSeconds(0.125f); // wait for half the punch to finish before launching
            
            // Apply camera shake & FOV Punch
            context.PlayerCamera.AddCameraShake(CameraShake.CreateShake(CameraShakeStrength.Heavy));
            launchAnimSettings.strength = Vector3.one * 35f; // 25 fov diff
            var fovPunch = Tween.PunchCustom(m_aimCamera, Vector3.one * context.cameraController.CameraState.m_fov,
                launchAnimSettings, (
                    (context1, vector3) =>
                    {
                        var newFOV = vector3.x;
                        context1.m_cameraState.m_fov = newFOV;
                    }));

            // Launch Player
            ProjectileMotion.LaunchPhysicsControllerWithLaunchAngle(context.physics, context.physics.Position, LaunchEndPointWS, k_launchGravity, m_launchAngle, out _, out _);
            
            // Show them again & make them follow their velocity direction
            context.meshVisibilityController.ShowFullMesh(0.25f);
            context.visualTransformInfluencer.ActiveMode = TransformInfluencer.Mode.VelocityFacing; // ensure we disable this mode after some time

            // Play a launch animation (spring kick)
            var dashAnimState = context.animator.GetAnimationSet<ActionsAnimationSet>().SpringLegsDashLoop.Play(context.animator);

            // Ensure we clean things up (NOTE: This is all hacky and easily prone to breaking)
            Tween.Delay(launchTf, () =>
            {
                context.visualTransformInfluencer.ActiveMode = TransformInfluencer.Mode.None;
                if (dashAnimState is { IsPlaying: true } && dashAnimState.Time > launchTf - 0.1f) // in case of animation cancelling 
                    LaunchSpring.s_frontTuckAnim.Play(context.animator); // TODO: tbh i can make all AnimationReferences static variables for easy access...
            });
                
            yield return Yields.WaitForSeconds(0.15f); // wait a bit before resetting the cannon
        }
        finally
        {
            context.actionController.EnableActions();
            context.meshVisibilityController.ShowFullMesh(0.25f);
            context.PlayerCamera.PopVirtualCamera(m_aimCamera, CameraBlendParams.Create(0.45f, Ease.InOutQuad, true));
            context.physics.VelocityCalculationsEnabled = true;
            context.physics.RotationCalculationsEnabled = true;
            EndInteraction(context);

            DoResettleTween();
        }
    }

    private void DoResettleTween()
    {
        // Can keep yaw same
        Tween.LocalRotation(m_aimPitch, m_cachedPitch, 0.5f, Ease.OutQuad)
            .OnComplete(() => IsOccupied = false);
    }
    
#if UNITY_EDITOR
    private Vector3[] debug_trajectoryPoints = new Vector3[16];
    public override void DrawGizmos()
    {
        ProjectileMotion.DrawProjectileGizmos(debug_trajectoryPoints, transform.position, LaunchEndPointWS, Vector3.down * k_launchGravity, m_launchAngle);
    }

    private void OnValidate()
    {
        m_launchEndPoint = new Vector2(Mathf.Max(m_launchEndPoint.x, 5), m_launchEndPoint.y);
    }
#endif    
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(CannonLauncher))]
public class CannonLauncherEditor : OdinEditor
{
    private void OnSceneGUI()
    {
        var cannon = (CannonLauncher)target;
        ProjectileMotion.TrajectoryAngleEndPointHandle(cannon, cannon.transform.position, ref cannon.m_launchEndPoint, ref cannon.m_launchAngle);
    }
}
#endif 