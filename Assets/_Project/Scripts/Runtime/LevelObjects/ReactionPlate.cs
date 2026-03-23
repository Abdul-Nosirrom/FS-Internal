
using System.Collections;
using System.Collections.Generic;
using Drawing;
using FS.Attributes;
using FS.GameplayActions;
using FS.Player;
using FS.Utility;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif 

[Experimental]
[LevelDesignCategory("Level Objects/Reaction Plate")]
public class ReactionPlate : LevelObjectBase
{
    [SerializeField, Required, AssetsOnly] 
    private GameObject m_plateVisuals;
    
    // Query plates from children instead of storing a list
    public List<Transform> GetPlates()
    {
        List<Transform> plates = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            plates.Add(transform.GetChild(i));
        }
        return plates;
    }

    public int PlateCount => transform.childCount;

    private const float k_travelSpeed = 45f;
    private const float k_plateWaitTime = 0.5f;

    private HashSet<int> m_activeInteractors = new();

    private class DisableJumpConstraint : ActionConstraintBase
    {
        public override string Name => "Reaction Plate Disable Jump";
        public override bool EvaluateConstraint(GameplayAction action) => action is not JumpAction;
    }
    
    protected override void OnPhysicsActorEnter(Context context)
    {
        // Is there more than 1 plate?
        if (PlateCount < 2) return; // invalid
        if (!context.IsPlayer) return;
        
        // Are we standing on the first control plate
        var distToFirstPlate = Vector3.Distance(context.physics.Position, GetPlates()[0].position);
        if (distToFirstPlate > 4f) return; // magic number but reasonable distance to check if we're actually standing on the first plate
        
        if (!context.actionConstraints.TryGetValue("Reaction Plate Disable Jump", out var existingConstraint))
        {
            existingConstraint = ActionConstraintBase.Create<DisableJumpConstraint>(); // We disable because of input race condition atm...
            context.actionConstraints["Reaction Plate Disable Jump"] = existingConstraint;
            context.actionController.ConstraintHandler.AddConstraint(existingConstraint, false);
        }
        
        existingConstraint.EnableConstraint();
        
        BeginInteraction(context);
        StartInteractionCoroutine(context, JumpPlatesRoutine(context));
    }

    protected override void OnPhysicsActorExit(Context context)
    {
        if (!context.IsPlayer) return;
        
        if (context.actionConstraints.TryGetValue("Reaction Plate Disable Jump", out var existingConstraint))
        {
            existingConstraint.DisableConstraint();
        }
        
        // Just walked off the plate w/out triggering it
        if (!m_activeInteractors.Contains(context.transform.GetInstanceID()))
            EndInteraction(context);
    }

    private IEnumerator JumpPlatesRoutine(Context context)
    {
        try
        {
            yield return new WaitUntil(() => context.PlayerInput.GetButton(GameInput.Jump));

            m_activeInteractors.Add(context.transform.GetInstanceID());
            context.PlayerInput.ConsumeInput(GameInput.Jump);
            context.physics.VelocityCalculationsEnabled = false;
            context.physics.RotationCalculationsEnabled = false;
            context.actionController.DisableActions();

            context.physics.Velocity = Vector3.zero;

            int targetPlate = 1;
            var plates = GetPlates();

            while (targetPlate < PlateCount)
            {
                // travel loop
                float travelPct = 0f;
                
                Vector3 startPos = plates[targetPlate - 1].transform.position;
                Vector3 endPos = plates[targetPlate].transform.position;
                Quaternion startRot = plates[targetPlate - 1].transform.rotation;
                Quaternion endRot = plates[targetPlate].transform.rotation;
                
                // Offset by 0.25m because thats the mesh i made, TODO: Change later
                startPos += plates[targetPlate - 1].transform.up * 0.25f;
                endPos += plates[targetPlate].transform.up * 0.25f;

                Vector3 playerPos = context.physics.Position;
                Quaternion playerRot = context.physics.Rotation;
                while (travelPct < 1f)
                {
                    travelPct += (k_travelSpeed / Vector3.Distance(startPos, endPos)) * Time.deltaTime;
                    travelPct = Mathf.Clamp01(travelPct);

                    playerPos = Vector3.Lerp(startPos, endPos, travelPct);
                    playerRot = Quaternion.Slerp(startRot, endRot, travelPct);
                    
                    context.physics.Teleport(playerPos, playerRot);

                    yield return Yields.WaitForFixedUpdate;
                }
                
                targetPlate++;
                
                if (targetPlate >= PlateCount) break;

                // Reached new plate, wait for input for specified time or just exit out
                var wait = context.PlayerInput.WaitForPress(GameInput.Jump, timeout: k_plateWaitTime);
                while (wait.keepWaiting)
                {
                    context.physics.Teleport(playerPos, playerRot);
                    context.physics.Velocity = Vector3.zero;
                    yield return Yields.WaitForFixedUpdate;
                }
                
                // No press occured, we exit
                if (!wait.WasPressed) break;
            }
        }
        finally
        {
            EndInteraction(context);
            m_activeInteractors.Remove(context.transform.GetInstanceID());
            context.physics.VelocityCalculationsEnabled = true;
            context.physics.RotationCalculationsEnabled = true;
            context.actionController.EnableActions();
        }
    }
    
#if UNITY_EDITOR
    public override void DrawGizmos()
    {
        // Draw the connections
        var plates = GetPlates();
        for (int p = 0; p < plates.Count; p++)
        {
            var curPos = plates[p].transform.position;
            if (p < plates.Count - 1)
            {
                var nextPos = plates[p + 1].transform.position;
                Draw.DashedLine(curPos, nextPos, 0.25f, 0.1f);
            }
            
            Draw.Label2D(curPos, $"Jump Plate {p}", 18f, LabelAlignment.Center, Color.cornflowerBlue);
        }
    }
#endif    
}

#if UNITY_EDITOR
[CustomEditor(typeof(ReactionPlate))]
public class ReactionPlateEditor : Editor
{
    private ReactionPlate m_target;
    private SerializedProperty m_plateVisuals;

    private void OnEnable()
    {
        m_target = (ReactionPlate)target;
        m_plateVisuals = serializedObject.FindProperty("m_plateVisuals");
        
        // Subscribe to hierarchy changes
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
    }

    private void OnHierarchyChanged()
    {
        // Repaint when hierarchy changes so we see updates immediately
        if (m_target != null)
        {
            Repaint();
            SceneView.RepaintAll();
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(m_plateVisuals);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Plate Chain", EditorStyles.boldLabel);
        
        List<Transform> plates = m_target.GetPlates();
        
        if (plates.Count == 0)
        {
            EditorGUILayout.HelpBox("No plates in chain. Add plates as children.", MessageType.Info);
        }
        else
        {
            // Display current plate order
            for (int i = 0; i < plates.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.LabelField($"Plate {i}", GUILayout.Width(60));
                
                // Show the plate object (click to select)
                if (GUILayout.Button(plates[i].name, EditorStyles.objectField))
                {
                    Selection.activeGameObject = plates[i].gameObject;
                    SceneView.FrameLastActiveSceneView();
                }
                
                // Move up button
                GUI.enabled = i > 0;
                if (GUILayout.Button("↑", GUILayout.Width(25)))
                {
                    Undo.SetTransformParent(plates[i], m_target.transform, "Reorder Plate");
                    plates[i].SetSiblingIndex(i - 1);
                }
                
                // Move down button
                GUI.enabled = i < plates.Count - 1;
                if (GUILayout.Button("↓", GUILayout.Width(25)))
                {
                    Undo.SetTransformParent(plates[i], m_target.transform, "Reorder Plate");
                    plates[i].SetSiblingIndex(i + 1);
                }
                
                GUI.enabled = true;
                
                // Delete button
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    if (EditorUtility.DisplayDialog("Delete Plate", 
                        $"Delete plate '{plates[i].name}'?", "Delete", "Cancel"))
                    {
                        Undo.DestroyObjectImmediate(plates[i].gameObject);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        EditorGUILayout.Space();
        
        // Add plate button
        GUI.enabled = m_plateVisuals.objectReferenceValue != null;
        if (GUILayout.Button("Add New Plate (Shift+A)"))
        {
            AddNewPlate();
        }
        GUI.enabled = true;
        
        if (m_plateVisuals.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign Plate Visuals prefab before adding plates", MessageType.Info);
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Total Plates: {plates.Count}", EditorStyles.miniLabel);

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        // Keyboard shortcut for adding plates
        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.shift && e.keyCode == KeyCode.A)
        {
            AddNewPlate();
            e.Use();
        }

        List<Transform> plates = m_target.GetPlates();
        
        // Draw connection lines between plates
        Handles.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        for (int i = 0; i < plates.Count - 1; i++)
        {
            if (plates[i] != null && plates[i + 1] != null)
            {
                Vector3 start = plates[i].position;
                Vector3 end = plates[i + 1].position;
                
                Handles.DrawLine(start, end, 3f);
                
                // Draw arrow
                Vector3 direction = (end - start).normalized;
                Vector3 midPoint = (start + end) * 0.5f;
                
                float arrowSize = 0.5f;
                Quaternion arrowRotation = Quaternion.LookRotation(direction);
                Handles.ConeHandleCap(0, midPoint, arrowRotation, arrowSize, EventType.Repaint);
                
                // Draw travel distance
                float distance = Vector3.Distance(start, end);
                Handles.Label(midPoint + Vector3.up * 0.5f, 
                    $"{distance:F1}m", 
                    EditorStyles.whiteBoldLabel);
            }
        }

        // Draw plate numbers and highlight
        for (int i = 0; i < plates.Count; i++)
        {
            if (plates[i] == null) continue;
            
            Vector3 pos = plates[i].position;
            
            // Draw numbered label
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.white },
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            
            // Different color for start and end
            if (i == 0)
            {
                Handles.color = Color.green;
                Handles.Label(pos + Vector3.up * 2f, "START", labelStyle);
            }
            else if (i == plates.Count - 1)
            {
                Handles.color = Color.red;
                Handles.Label(pos + Vector3.up * 2f, "END", labelStyle);
            }
            else
            {
                Handles.color = Color.white;
                Handles.Label(pos + Vector3.up * 2f, $"#{i}", labelStyle);
            }
            
            // Draw wire disc under each plate
            Handles.color = new Color(0.3f, 0.8f, 1f, 0.3f);
            Handles.DrawWireDisc(pos, Vector3.up, 1f);
        }
    }

    private void AddNewPlate()
    {
        if (m_plateVisuals.objectReferenceValue == null) return;

        GameObject plateGO = Instantiate(m_plateVisuals.objectReferenceValue as GameObject);
        
        List<Transform> existingPlates = m_target.GetPlates();
        
        // Position based on last plate or parent
        if (existingPlates.Count > 0)
        {
            Transform lastPlate = existingPlates[existingPlates.Count - 1];
            plateGO.transform.position = lastPlate.position + lastPlate.forward * 10f;
            plateGO.transform.rotation = lastPlate.rotation;
        }
        else
        {
            plateGO.transform.position = m_target.transform.position;
            plateGO.transform.rotation = m_target.transform.rotation;
        }
        
        // Parent to the ReactionPlate
        Undo.SetTransformParent(plateGO.transform, m_target.transform, "Add Reaction Plate");
        plateGO.name = $"Plate_{existingPlates.Count}";
        
        Undo.RegisterCreatedObjectUndo(plateGO, "Add Reaction Plate");
        Selection.activeGameObject = plateGO;
        
        // Repaint to show the new plate immediately
        Repaint();
        SceneView.RepaintAll();
    }
}
#endif