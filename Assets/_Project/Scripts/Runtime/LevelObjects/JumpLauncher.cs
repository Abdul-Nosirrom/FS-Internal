
using System;
using System.Collections;
using Drawing;
using FS.Attributes;
using FS.CameraSystem;
using FS.GameplayActions;
using FS.Math;
using Sirenix.OdinInspector;
using UnityEngine;

[Experimental]
[LevelDesignCategory("Level Objects/Jump Launcher")]
public class JumpLauncher : LevelObjectBase
{
    private class DisableJumpConstraint : ActionConstraintBase
    {
        public override string Name => "Disable Jump (JumpLauncher)";
        public override bool EvaluateConstraint(GameplayAction action) => action is not JumpAction;
    }
    
    [SerializeField] public float LaunchHeight = 15f;
    [SerializeField] public bool ShouldOverrideGravity = false;
    [SerializeField, Range(1, 100), ShowIf(nameof(ShouldOverrideGravity))] public float Gravity = 35;
    
    private enum Mode { Flat, Angled}
    [SerializeField] private Mode mode = Mode.Flat;
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        // Hacky way to show/hide the correct model based on mode, but this is a prototype
        transform.GetChild(0).gameObject.SetActive(false);
        transform.GetChild(1).gameObject.SetActive(false);

        if (mode == Mode.Flat)
            transform.GetChild(0).gameObject.SetActive(true);
        else 
            transform.GetChild(1).gameObject.SetActive(true);
    }
#endif     
    
    protected override void OnPhysicsActorEnter(Context context)
    {
        if (!context.IsPlayer) return;
        
        if (!context.actionConstraints.TryGetValue("Jump Launcher Disable Jump", out var existingConstraint))
        {
            existingConstraint = ActionConstraintBase.Create<DisableJumpConstraint>(); // We disable because of input race condition atm...
            context.actionConstraints["Jump Launcher Disable Jump"] = existingConstraint;
            context.actionController.ConstraintHandler.AddConstraint(existingConstraint, false);
        }
        
        existingConstraint.EnableConstraint();
        
        BeginInteraction(context);
        StartInteractionCoroutine(context, JumpLauncherRoutine(context));
    }
    
    protected override void OnPhysicsActorExit(Context context)
    {
        if (!context.IsPlayer) return;
        
        if (context.actionConstraints.TryGetValue("Jump Launcher Disable Jump", out var existingConstraint))
        {
            existingConstraint.DisableConstraint();
        }
        
        EndInteraction(context);
    }
    
    private IEnumerator JumpLauncherRoutine(Context context)
    {
        yield return new WaitUntil(() => context.PlayerInput.GetButton(GameInput.Jump));
        context.PlayerInput.ConsumeInput(GameInput.Jump);
        
        ProjectileMotion.LaunchPhysicsControllerToHeight(context.physics, LaunchHeight, ShouldOverrideGravity ? Gravity : context.physics.VerticalPhysicsParams.m_upGravity, out _, out _);
        
        // Camera shake
        context.PlayerCamera.AddCameraShake(CameraShake.CreateShake());
    }

    
#if UNITY_EDITOR
    public override void DrawGizmos()
    {
        using var thickness = Draw.WithLineWidth(3);
        Draw.DashedLine(transform.position, transform.position + Vector3.up * LaunchHeight, 0.25f, 0.1f, Color.whiteSmoke);
        Draw.WireSphere(transform.position + Vector3.up * LaunchHeight, 0.4f, ShouldOverrideGravity ? Color.crimson : Color.forestGreen);
        
        string labelText = $"Height: {LaunchHeight:F1}m";
        int offset = 1;
        if (ShouldOverrideGravity)
        {
            var launchSpeed = Mathf.Sqrt(2 * Gravity * LaunchHeight);
            float launchTime = Mathf.Sqrt(2 * LaunchHeight / Gravity);
            labelText += $"\nGravity: {Gravity:F1}m/s2\nLaunch Speed: {launchSpeed:F1}m/s\nAir Time: {launchTime:F2}s";
            offset = 2;
        }
        Draw.Label2D(transform.position + Vector3.up * (offset + LaunchHeight), labelText, 14f, LabelAlignment.Center);
    }
#endif
}


#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(JumpLauncher))]
public class JumpLauncherEditor : UnityEditor.Editor
{
    private void OnSceneGUI()
    {
        JumpLauncher launchSpring = (JumpLauncher)target;
        
        var handleRot = Quaternion.LookRotation(launchSpring.transform.up, launchSpring.transform.forward);
        var newHeight = FS.MeshProcessing.Editor.HandlesUtility.LinearScaleHandle(launchSpring.transform.position, handleRot, launchSpring.LaunchHeight);
        if (!Mathf.Approximately(newHeight, launchSpring.LaunchHeight))
        {
            UnityEditor.Undo.RecordObject(launchSpring, "Change Launch Height");
            launchSpring.LaunchHeight = newHeight;
            UnityEditor.EditorUtility.SetDirty(launchSpring);
        }
    }
}
#endif