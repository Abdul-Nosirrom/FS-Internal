using Drawing;
using FS.Attributes;
using FS.GameplayActions;
using FS.Math;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

[LevelDesignCategory("Level Objects/Springs")]
public class TrajectorySpring : LevelObjectBase
{
    [SerializeField, InfoBox("Optional transform to directly specify the target point")] 
    private Transform m_targetPoint;
    [SerializeField] public Vector2 m_localEndPoint = new Vector2(5, 0);
    [SerializeField, Range(0, 90)] public float m_launchAngle = 10f;
    [SerializeField, Range(1, 40)] private float m_gravity = 20;
    
    private class SpringActionConstraint : ActionConstraintBase
    {
        public override bool EvaluateConstraint(GameplayAction action) =>
            (action.Channels | ActionChannel.Physics) > 0 && action is not AcidDropAction;
    }

    public Vector3 EndPointWorld 
    {
        get => HasTransformAsEndPoint ? m_targetPoint.position : transform.TransformPoint(new Vector3(0, m_localEndPoint.y, m_localEndPoint.x));
        set => m_localEndPoint = new Vector2(transform.InverseTransformPoint(value).z, transform.InverseTransformPoint(value).y);
    }
    public bool HasTransformAsEndPoint => m_targetPoint != null;

    protected override void OnPhysicsActorEnter(Context context)
    {
        context.physics.Position = transform.position;
        ProjectileMotion.LaunchPhysicsControllerWithLaunchAngle(context.physics, transform.position, EndPointWorld, m_gravity, m_launchAngle, out var launchTime, out _);
        LaunchSpring.s_frontTuckAnim.Play(context.animator);

        if (context.actionController != null)
        {
            //context.actionController.DisableActionChannels(ActionChannel.Physics);
            if (!context.actionConstraints.TryGetValue(nameof(SpringActionConstraint), out var existingConstraint))
            {
                existingConstraint = new SpringActionConstraint();
                context.actionController.ConstraintHandler.AddConstraint(existingConstraint);
                context.actionConstraints[nameof(SpringActionConstraint)] = existingConstraint;
            }
            existingConstraint.EnableConstraint();
            Tween.Delay(launchTime, () => existingConstraint.DisableConstraint());
            //Tween.Delay(launchTime, () => context.actionController.EnableActionChannels(ActionChannel.Physics));
        }
    }

#if UNITY_EDITOR
    private Vector3[] debug_trajectoryPoints = new Vector3[16];
    public override void DrawGizmos()
    {
        ProjectileMotion.DrawProjectileGizmos(debug_trajectoryPoints, transform.position, EndPointWorld, Vector3.down * m_gravity, m_launchAngle);
    }
#endif    
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TrajectorySpring))]
public class TrajectorySpringEditor : UnityEditor.Editor
{
    private void OnSceneGUI()
    {
        TrajectorySpring launchSpring = (TrajectorySpring)target;

        if (!launchSpring.HasTransformAsEndPoint)
            ProjectileMotion.TrajectoryEndPointHandle(launchSpring, ref launchSpring.m_localEndPoint);

        ProjectileMotion.TrajectoryAngleHandle(launchSpring, launchSpring.transform.position,
            launchSpring.EndPointWorld, ref launchSpring.m_launchAngle);
    }
}
#endif