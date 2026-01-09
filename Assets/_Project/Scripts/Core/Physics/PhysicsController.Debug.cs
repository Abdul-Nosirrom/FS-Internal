using System.Collections.Generic;
using Drawing;
using FS.Math;
using FS.RuntimeDebug;
using Lightbug.CharacterControllerPro.Core;
using TimeUtils;
using Unity.Mathematics;
using UnityEngine;

public partial class PhysicsController : IDebugProvider
{

    private float debug_maxApex;
    private float debug_airDistance;
    private Vector3 debug_prevPos;

    private bool debug_modifiersExpanded = false;

    private TimeSince debug_timeSinceSpeedHistoryUpdated;
    private Queue<float> debug_lateralSpeedHistory = new Queue<float>(128);
    private Queue<float> debug_verticalSpeedHistory = new Queue<float>(128);
    private Queue<float> debug_totalSpeedHistory = new Queue<float>(128);

    private bool debug_graphLateralSpeed = false;
    private bool debug_graphVerticalSpeed = false;
    private bool debug_graphTotalSpeed = false;
    
    private bool debug_drawModeSettings = true;
    private bool debug_levelObjectState = true;
    private bool debug_drawCapsule = true;
    private bool debug_drawGrounding = true;
    private bool debug_drawVelocity = true;
    private bool debug_drawTrajectory = false;
    private bool debug_drawOrientation = false;

    private bool debug_pauseOnStateChange = false;
    private PhysicsState debug_pauseState = PhysicsState.Air;
    private bool debug_hasPausedThisStateChange = false;

    private bool debug_motorInfoExpanded = false;

    private DebugGraph debug_speedGraph = new DebugGraph("Speed History", capacity: 512)
    {
        AutoScale = true,
        Symmetric = true,           // Since vertical velocity can be negative
        ShowGrid = true,
        ShowLegend = true,
        ShowMinMax = true,
        ShowCurrentValue = true,
        GridLinesHorizontal = 4,
        GridLinesVertical = 8,
        BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f)
    };
    
    public string DebugName => "PhysicsController";
    public void OnDebugGUI()
    {
        Vector3 inputDir = MoveInput();
        
        var labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontStyle = FontStyle.Bold;

        var ogStyle = GUI.skin.label;
        GUI.skin.label = labelStyle;
        
        var ogColor = GUI.backgroundColor;
        
        debug_motorInfoExpanded = DebugGUI.BeginFoldout("Motor Info", ref debug_motorInfoExpanded, GUI.skin.box);
        if (debug_motorInfoExpanded)
        {
            GUILayout.Label(m_motor.GetCharacterInfo());
            DebugGUI.EndFoldout();
        }
        
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label( $"Physics State: {State}", GUI.skin.button);
        {
            GUILayout.BeginHorizontal();
            debug_pauseOnStateChange = GUILayout.Toggle(debug_pauseOnStateChange, "Paused on State Change");
            debug_pauseState = (PhysicsState)GUILayout.SelectionGrid((int)debug_pauseState, System.Enum.GetNames(typeof(PhysicsState)), 3);
            
            if (debug_pauseOnStateChange && State == debug_pauseState && !debug_hasPausedThisStateChange)
            {
                DebugManager.Instance.DebugPause = true;
                debug_hasPausedThisStateChange = true;
            }
            
            // Reset pause flag when we leave the state
            if (State != debug_pauseState)
            {
                debug_hasPausedThisStateChange = false;
            }
            
            GUILayout.EndHorizontal();
        }
        GUILayout.Label( $"Planar Speed: {LateralSpeed:F1}");
        GUILayout.Label( $"Vertical Speed: {VerticalSpeed:F1}");
        GUILayout.Label( $"Total Speed: {Speed:F1}");
        GUILayout.Label( $"Input Vector: {inputDir}");
        
        // Speed percent
        DebugGUI.ProgressBar("Speed Percent", LateralSpeed / LateralPhysicsParams.m_topSpeed, 0, 1);
        
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label( $"Grounding Status: {IsGrounded}");
        if (!IsGrounded)
        {
            GUILayout.Label($"Time Since Lost Ground: {m_timeSinceLostGround}");
        }
        else
        {
            GUILayout.Label($"Time Since Grounded: {m_timeSinceFoundGround}");
        }
        GUILayout.Label( $"Angle: {Vector3.Angle(Ground.Normal, -GravityDir):F1}");
        GUILayout.Label( $"Denivelation: {Ground.EdgeAngle}");
        GUILayout.EndVertical();

        
        GUI.skin.label = labelStyle;
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label( $"Max Apex Height: {debug_maxApex:F1}");
        GUILayout.Label( $"Air Distance: {debug_airDistance:F1}");
        GUILayout.EndVertical();
        
        // // Graph option toggles
        // {
        //     var ogTextColor = GUI.color;
        //     GUILayout.BeginHorizontal();
        //     GUI.color = Color.green;
        //     debug_graphLateralSpeed = GUILayout.Toggle(debug_graphLateralSpeed, "XZ");
        //     GUI.color = Color.deepSkyBlue;
        //     debug_graphVerticalSpeed = GUILayout.Toggle(debug_graphVerticalSpeed, "Y");
        //     GUI.color = Color.red;
        //     debug_graphTotalSpeed = GUILayout.Toggle(debug_graphTotalSpeed, "Total");
        //     GUILayout.EndHorizontal();
        //     GUI.color = ogTextColor;
        // }
        //
        debug_graphLateralSpeed = DebugGUI.BeginFoldout("Speed Graph", ref debug_graphLateralSpeed);
        if (debug_graphLateralSpeed)
        {
            var graphRect = GUILayoutUtility.GetAspectRect(1);
            graphRect.xMax -= 20; // Padding for legend
            graphRect.yMax -= 20; // Padding for min/max labels
            // Add series
            if (debug_speedGraph.GetSeries("Lateral Speed") == null)
                debug_speedGraph.AddSeries("Lateral Speed", Color.green);
            if (debug_speedGraph.GetSeries("Vertical Speed") == null)
                debug_speedGraph.AddSeries("Vertical Speed", Color.deepSkyBlue);
            if (debug_speedGraph.GetSeries("Total Speed") == null)
                debug_speedGraph.AddSeries("Total Speed", Color.red);

            if (debug_graphLateralSpeed && Event.current.type == EventType.Repaint)
            {
                debug_speedGraph.GetSeries(0).Push(LateralSpeed);
                debug_speedGraph.GetSeries(1).Push(VerticalSpeed);
                debug_speedGraph.GetSeries(2).Push(Speed);
            }
            
            debug_speedGraph.Draw(graphRect);
            
            DebugGUI.EndFoldout();
        }
        //if (debug_graphLateralSpeed) DebugGUI.Plot(debug_lateralSpeedHistory.ToArray(), graphRect, maxY:25);
        //if (debug_graphVerticalSpeed) DebugGUI.Plot(debug_verticalSpeedHistory.ToArray(), graphRect, maxY:25, color:Color.deepSkyBlue);
        //if (debug_graphTotalSpeed) DebugGUI.Plot(debug_totalSpeedHistory.ToArray(), graphRect, maxY:25, color:Color.red);

        GUI.backgroundColor = Color.gray6;
        if (DebugGUI.BeginFoldout("Physics Modifiers", ref debug_modifiersExpanded))
        {
            GUI.skin.label = GUI.skin.button;
            GUI.backgroundColor = m_lateralPhysicsModifications.HasAny ? Color.green : Color.red;
            GUILayout.Label($"Lateral Physics: {m_lateralPhysicsModifications}");
            GUI.backgroundColor = m_verticalPhysicsModifications.HasAny ? Color.green : Color.red;
            GUILayout.Label($"Vertical Physics: {m_verticalPhysicsModifications}");
            GUI.backgroundColor = m_rotationPhysicsModifications.HasAny ? Color.green : Color.red;
            GUILayout.Label($"Rotation Physics: {m_rotationPhysicsModifications}");
            
            DebugGUI.EndFoldout();
        }

        GUI.backgroundColor = Color.crimson;
        if (DebugGUI.BeginFoldout("Level Object State", ref debug_levelObjectState))
        {
            GUILayout.Label($"Has Level Object? {HasLevelObject}");
            if (HasLevelObject)
                GUILayout.Label($"[{ActiveLevelObject.name}]");
            DebugGUI.EndFoldout();
        }

        GUI.backgroundColor = Color.goldenRod;
        if (DebugGUI.BeginFoldout("Draw Modes", ref debug_drawModeSettings))
        {
            debug_drawCapsule = GUILayout.Toggle(debug_drawCapsule, "Draw Capsule");
            debug_drawGrounding = GUILayout.Toggle(debug_drawGrounding, "Draw Grounding");
            debug_drawTrajectory = GUILayout.Toggle(debug_drawTrajectory, "Draw Trajectory");
            debug_drawOrientation = GUILayout.Toggle(debug_drawOrientation, "Draw Orientation");
            DebugGUI.EndFoldout();
        }
        
        
        if (!IsGrounded)
        {
            // Try getting apex
            var vertDelta = (transform.position - LastGround.Point).Dot(-GravityDir);
            debug_maxApex = Mathf.Max(debug_maxApex, vertDelta);
            debug_airDistance = (transform.position - LastGround.Point).ProjectOnPlane(-GravityDir).magnitude;
        }
        else
        {
            debug_maxApex = 0;
        }
        
        GUI.skin.label = ogStyle;
        GUI.backgroundColor = ogColor;

        if (debug_timeSinceSpeedHistoryUpdated > 0.05f)
        {
            // Speed history
            if (debug_lateralSpeedHistory.Count >= 128)
            {
                debug_lateralSpeedHistory.Dequeue();
                debug_verticalSpeedHistory.Dequeue();
                debug_totalSpeedHistory.Dequeue();
            }

            debug_lateralSpeedHistory.Enqueue(LateralSpeed);
            debug_verticalSpeedHistory.Enqueue(Velocity.Dot(UpDirection));
            debug_totalSpeedHistory.Enqueue(m_motor.Velocity.magnitude);
            
            debug_timeSinceSpeedHistoryUpdated = 0;
        }
        //debug_speedHistory.Dequeue();
    }

    // issue where we essentially are getting multiple frames w/ "groundThisFrame", not sure why yet but this is a bandaid
    // for the debug drawing purposes
    private bool debug_groundingReset = false;
    private Vector3 debug_orientationCache = Vector3.zero;
    
    public void OnDebugDraw()
    {
        // Draw capsule
        if (debug_drawCapsule)
        {
            Draw.ingame.WireCapsule(transform.position, transform.up, m_motor.BodySize.y, m_motor.BodySize.x/2f);
        }
        
        // && !m_actionController.GetActionSet<LocomotionActionSet>().Jump.IsActive)
        {
            // Draw up-vector at the point
            using var thickness = Draw.ingame.WithLineWidth(2f);
            using var duration = Draw.ingame.WithDuration(5f);
            if (debug_drawTrajectory)
                Draw.ingame.Line(debug_prevPos, transform.position, State == PhysicsState.Air ? Color.forestGreen :  Color.crimson);
            if (debug_drawOrientation && UpDirection != debug_orientationCache)
            {
                Draw.ingame.Arrow(FootPosition, FootPosition + UpDirection, State == PhysicsState.Air ? Color.red : Color.yellow);
            }

            debug_orientationCache = UpDirection;
        }
        
        debug_prevPos = transform.position;

        if (debug_drawVelocity)
        {
            using var thickness = Draw.ingame.WithLineWidth(2f);
            var arrowPos = transform.position + transform.up * CapsuleHeight;
            Draw.ingame.Arrow(arrowPos, arrowPos + Acceleration/15f, Color.red);
            Draw.ingame.Arrow(arrowPos, arrowPos + m_motor.Velocity.normalized, Color.blue);
            Draw.ingame.Arrow(arrowPos, arrowPos + MoveInput().normalized, Color.yellow);
        }

        if (IsGrounded && debug_drawGrounding)
        {
            using var thickness = Draw.ingame.WithLineWidth(4f);
            var arrowPos = transform.position + transform.up * CapsuleHeight;
            Draw.ingame.Arrow(arrowPos, arrowPos+ m_motor.GroundStableNormal, Color.green);
            var innerHitDirection =
                Vector3.ProjectOnPlane(m_motor.GroundStableNormal, transform.up).normalized;
            //Draw.ingame.Arrow(arrowPos, arrowPos+m_motor.GroundContactNormal + innerHitDirection * 0.01f, Color.blue);
            Draw.ingame.Arrow(arrowPos, arrowPos+m_motor.GroundContactNormal - innerHitDirection * 0.01f, Color.red);
        }

        debug_groundingReset |= (!LostGroundThisFrame && !FoundGroundThisFrame);
        
        if (debug_drawGrounding && debug_groundingReset && (LostGroundThisFrame || FoundGroundThisFrame))
        {
            debug_groundingReset = false;
            var groundInfo = LostGroundThisFrame ? LastGround : Ground;
            
            using var groundDebugDuration = Draw.ingame.WithDuration(10f);
            using var thickness = Draw.ingame.WithLineWidth(2f);
            
            Draw.ingame.Label2D(groundInfo.Point + groundInfo.Normal, $"Vel: {Velocity:F1}\n Speed: {Velocity.magnitude:F1}\n Angle: {m_motor.GroundSlopeAngle}");
            Draw.ingame.WireSphere(groundInfo.Point, 0.25f, LostGroundThisFrame ? Color.red : Color.green);
            Draw.ingame.Arrow(groundInfo.Point, groundInfo.Point + groundInfo.Normal, LostGroundThisFrame ? Color.green : Color.red);

            Draw.ingame.WireCapsule(FootPosition, transform.up, CapsuleHeight, CapsuleRadius, Color.coral);
        }
    }
}