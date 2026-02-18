using Drawing;
using FS.Animation;
using FS.Attributes;
using FS.GameplayActions;
using FS.Math;
using FS.TagSystem;
using Sirenix.OdinInspector;
using TimeUtils;
using UnityEngine;

[LevelDesignCategory("Level Objects/Springs")]
public class LaunchSpring : LevelObjectBase
{
    [SerializeField] public float LaunchHeight;
    [SerializeField] public bool ShouldOverrideGravity = false;
    [SerializeField, Range(1, 100), ShowIf(nameof(ShouldOverrideGravity))] public float Gravity = 35;
    
    public static AnimationReference s_frontTuckAnim = AnimationReference.Get<ActionsAnimationSet>("FrontFlip");
    
    protected override void OnPhysicsActorEnter(Context context)
    {
        context.physics.Position = transform.position;
        var gravity = ShouldOverrideGravity ? Gravity : Mathf.Abs(context.physics.VerticalPhysicsParams.m_upGravity);
        ProjectileMotion.LaunchPhysicsControllerToHeight(context.physics, LaunchHeight, gravity, out _, out _);

        // Reset style jumps
        context.physics.ResetActivationsUnder(Tag.Action.Activation.StyleJumps);
        
        // Play front tuck animation if available
        s_frontTuckAnim.Play(context.animator);
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
[UnityEditor.CustomEditor(typeof(LaunchSpring))]
public class LaunchSpringEditor : UnityEditor.Editor
{
    private void OnSceneGUI()
    {
        LaunchSpring launchSpring = (LaunchSpring)target;
        
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