using System;
using Drawing;
using FS.Attributes;
using FS.GameplayActions;
using FS.Math;
using FS.MeshProcessing.Utility;
using UnityEngine;

[LevelDesignCategory("Level Objects/Wind Current")]
public class WindCurrent : LevelObjectBase
{
    [SerializeField] private Material m_windMaterial;
    [Tooltip("Transform containing wind visual effects (optional), if none, proc-gen will be used")]
    [SerializeField] private Transform m_windVisuals;
    
    //[Tooltip("Range around the equillibrium height where we oscillate. Equillibrium point becomes height - m_oscillationAmplitude\n " +
    //         "General Behavior is that we accelerate to equillibriumPoint-m_oscillationAmplitude, then within that region we swap physics to a spring-system")]
    //[SerializeField, Range(0f, 5f)] public float m_oscillationAmplitude = 1;

    [Tooltip("Push force for lateral motion when current is oriented horizontally. Is a multiple of players acceleration")] 
    [SerializeField, Range(0f, 10f)]
    private float m_lateralPushForce = 2;
    
    [SerializeField, Range(0.1f, 10)] public float m_radius = 1;
    [SerializeField, Range(0.1f, 20)] public float m_height = 4;

    private Mesh m_windCurrentMesh; // Collision cylindrical mesh

    private const float k_windGravity = 10f;
    private const float k_oscillationAmplitude = 1f;
    private const float k_dampingCoefficient = 0.5f;

    public bool IsHorizontal => Mathf.Abs(transform.up.Dot(Vector3.up)) < 0.1f;
    
    private void Awake() => GenerateMesh();
    
#if UNITY_EDITOR
    public void OnValidate() => GenerateMesh();
#endif

    private void GenerateMesh() // Generating cylindrical collision mesh
    {
        if (m_windCurrentMesh != null) DestroyImmediate(m_windCurrentMesh);
        
        m_windCurrentMesh = FS.MeshProcessing.MeshGenerator.GenerateCylinder(
            radius: m_radius,
            height: m_height,
            radialSegments: 16,
            createCaps: true,
            name: "Wind Current Mesh");

        if (m_windVisuals != null)
        {
            // Assume mesh is 1x1, so scale it XZ by radius, Y by height
            m_windVisuals.localPosition = Vector3.zero;
            m_windVisuals.localRotation = Quaternion.identity;
            m_windVisuals.localScale = new Vector3(m_radius * 2f, m_height, m_radius * 2f);
            var meshFilters = m_windVisuals.GetComponentsInChildren<MeshRenderer>();
            foreach (var mf in meshFilters)
            {
                mf.sharedMaterial = m_windMaterial;
            }
        }

        var windCollider = gameObject.GetOrAddComponent<MeshCollider>();
        windCollider.sharedMesh = m_windCurrentMesh;
        windCollider.convex = true;
        windCollider.isTrigger = true;
    }

    protected override void OnPhysicsActorEnter(Context context)
    {
        BeginInteraction(context);
        if (!IsHorizontal && context.actionController)
            context.actionController.ActiveActions.CancelActionsInChannels(ActionChannel.Physics);//context.actionController.DisableActionChannels(ActionChannel.Physics));
        context.physics.ModifyVerticalPhysics((controller) => controller.ActiveLevelObject != this, (ref VerticalPhysicsParams value) =>
            {
                value.SetNoControl();
                value.SetGravity(0f);
            }, $"Wind Current_{GetInstanceID()}");
    }

    protected override void OnPhysicsActorExit(Context context)
    {
        //if (!IsHorizontal && context.actionController) context.actionController.EnableActionChannels(ActionChannel.Physics);
        EndInteraction(context);
    }

    protected override void OnPhysicsActorStay(Context context)
    {
        // Enforce a min speed
        //if (context.physics.Speed < 0.25f) context.physics.Velocity = transform.up * 2;
        if (IsHorizontal) HorizontalPhysics(context);
        else VerticalPhysics(context);
    }

    private void CancelIncompatibleActions(Context context)
    {
        if (!context.actionController) return;
        
        if (context.actionController.GetActionSet<PrototypeActionSet>() is { } prototypeActionSet)
        {
            // You'll get stuck in the current if starting this action
            if (prototypeActionSet.SpringKick && prototypeActionSet.SpringKick.IsActive)
            {
                prototypeActionSet.SpringKick.TryEndAction();
            }
        }
    }
    
    private void HorizontalPhysics(Context context)
    {
        if (context.physics.Speed < 0.25f) context.physics.Velocity = transform.up * 2;

        float baseAccel = Mathf.Max(context.physics.LateralPhysicsParams.m_acceleration, context.physics.LateralPhysicsParams.m_deceleration);
        float prevSpeed = context.physics.Velocity.Dot(transform.up);
        context.physics.Velocity += transform.up * baseAccel * m_lateralPushForce * Time.deltaTime;
        float currentSpeed = context.physics.Velocity.Dot(transform.up);
        if (prevSpeed <= context.physics.LateralPhysicsParams.m_topSpeed &&
            currentSpeed > context.physics.LateralPhysicsParams.m_topSpeed)
        {
            context.physics.Velocity = context.physics.Velocity.WithAxis(transform.up, context.physics.LateralPhysicsParams.m_topSpeed);
        }
    }
    
    private float EquillibriumHeight => (transform.position + transform.up * (m_height - k_oscillationAmplitude)).Dot(transform.up);

    private void VerticalPhysics(Context context)
    {
        if (context.physics.IsGrounded) context.physics.UnGround();

        CancelIncompatibleActions(context);
        
        // If a physics vertical channel is running - temporarily disable wind current effect
        if (context.actionController &&
            context.actionController.ActiveActions.ContainsAnyChannel(ActionChannel.PhysicsVertical))
            return;
        
        float x0 = EquillibriumHeight;
        float x = context.physics.CenterPosition.Dot(transform.up);
        
        float k = k_windGravity;//Mathf.Abs(context.physics.GravityScale); // Spring constant
        
        float dampingMultiplier = k_dampingCoefficient;
        // If within oscillation region or above equillibrium, apply remove damping
        if (Mathf.Abs(x - x0) <= k_oscillationAmplitude)// || x > x0)
        {
            dampingMultiplier = 0f;
        }
        
        float c = 2 * Mathf.Sqrt(k) * dampingMultiplier; // Damping coefficient for critical damping
        float F_spring = -k * (x - x0);
        float F_damper = -c * context.physics.VerticalVelocity.Dot(transform.up);
        float F_total = F_spring + F_damper;
        context.physics.VerticalVelocity += transform.up * F_total * Time.fixedDeltaTime;
    }
    
#if UNITY_EDITOR
    public override void DrawGizmos()
    {
        var equillibriumWorldPos = (transform.position + transform.up * (m_height - k_oscillationAmplitude));
        var upperBound = equillibriumWorldPos + transform.up * k_oscillationAmplitude;
        var lowerBound = equillibriumWorldPos - transform.up * k_oscillationAmplitude;
        
        Draw.WireCylinder(lowerBound, upperBound, m_radius, Color.crimson);
        Draw.WireSphere(equillibriumWorldPos, 0.25f, Color.forestGreen);
        
        float maxOscillationSpeed = Mathf.Sqrt(k_windGravity) * k_oscillationAmplitude; 
        float oscillationPeriod = 2 * Mathf.PI / Mathf.Sqrt(k_windGravity);

        string label = IsHorizontal ? ("Horizontal") : ($"Vertical: Osc Speed Max {maxOscillationSpeed:F1}\n Period: {oscillationPeriod:F1}s");
        Draw.Label2D(equillibriumWorldPos, label, 24f, LabelAlignment.Center);
    }
#endif    
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(WindCurrent))]
public class WindCurrentEditor : UnityEditor.Editor
{
    private void OnSceneGUI()
    {
        var windCurrent = (WindCurrent)target;
        
        // Height & Radius handles
        var radHandleRot = Quaternion.LookRotation(windCurrent.transform.forward, windCurrent.transform.up);
        var heightHandleRot = Quaternion.LookRotation(windCurrent.transform.up, windCurrent.transform.forward);
        var newRadius = FS.MeshProcessing.Editor.HandlesUtility.LinearScaleHandle(windCurrent.transform.position, radHandleRot, windCurrent.m_radius);
        var newHeight = FS.MeshProcessing.Editor.HandlesUtility.LinearScaleHandle(windCurrent.transform.position, heightHandleRot, windCurrent.m_height);

        if (!Mathf.Approximately(newRadius, windCurrent.m_radius) ||
            !Mathf.Approximately(newHeight, windCurrent.m_height))
        {
            UnityEditor.Undo.RecordObject(windCurrent, "Change Wind Current Radius/Height");
            windCurrent.m_radius = newRadius;
            windCurrent.m_height = newHeight;
            windCurrent.OnValidate();
            UnityEditor.EditorUtility.SetDirty(windCurrent);
        }
    }
}
#endif