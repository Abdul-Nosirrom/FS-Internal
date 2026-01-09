using System;
using System.Linq;
using FluffyUnderware.Curvy;
using FS.MeshProcessing;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using ISplineProvider = FS.MeshProcessing.ISplineProvider;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Tool script that automatically generates rail grind collision meshes needed for a spline.
/// These aren't saved in editor and the meshes get generated at load time. Could be improved with Jobs as the meshes are simple.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class RailGrindCollisionGenerator : MonoBehaviour, ISplineProvider, ISelfValidator
{
    [SerializeField, HideInInspector] private Mesh m_collisionMesh;
    [SerializeField, ReadOnly] private CurvySpline m_railSpline;
    
    public CurvySpline GetSpline() => m_railSpline;


#if UNITY_EDITOR
    [MenuItem("CONTEXT/GameObjectToolContext/Free Skies/Add Rail Grind", false, 0)] // Scene view context click
    [MenuItem("GameObject/Free Skies/Add Rail Grind", false, 0)] // Hierarchy context click
    private static void AddRailGrindCollision(MenuCommand menuCommand)
    {
        var go = menuCommand.context as GameObject;
        AddRailGrindCollision(go);
    }
  
    [MenuItem("CONTEXT/GameObjectToolContext/Free Skies/Add Rail Grind", true)]
    [MenuItem("GameObject/Free Skies/Add Rail Grind", true)]
    private static bool CanAddRailGrindCollision()
    {
        foreach (var selection in Selection.gameObjects)
        {
            if (!CanGameObjectHaveRailGrindCollision(selection)) return false;
        }
        return true;
    }
    
    // [MenuItem("Free Skies/Level/Generate Vert Collisions For Scene", false, 0)]
    // private static void GenerateRailGrindCollisionsForScene()
    // {
    //     if (!EditorUtility.DisplayDialog("Generate Vert Collisions For Scene",
    //             "This will scan the entire scene for vert objects and add vert collision generators to them. This may take a while depending on scene complexity. Continue?",
    //             "Yes", "No"))
    //         return;
    //
    //     int addedCount = 0;
    //     var allGOs = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToList();
    //     foreach (var go in allGOs)
    //     {
    //         string info = $"Processing {go.name}";
    //         if (CanGameObjectHaveRailGrindCollision(go))
    //         {
    //             AddRailGrindCollision(go);
    //             addedCount++;
    //         }
    //         else info = $"Skipping {go.name}, does not qualify";
    //
    //         if (EditorUtility.DisplayCancelableProgressBar("Adding Vert Collisions", info,
    //                 (float)addedCount / allGOs.Count))
    //             break;
    //     }
    //
    //     EditorUtility.DisplayDialog("Generate Vert Collisions For Scene",
    //         $"Added Vert Collision Generators to {addedCount} objects in the scene.",
    //         "OK");
    // }
    #endif
    
    private static bool CanGameObjectHaveRailGrindCollision(GameObject go)
    {
        if (go == null) return false;
        if (go.GetComponent<ISplineProvider>() == null) return false;
        if (go.GetComponentInChildren<RailGrindCollisionGenerator>() != null) return false; // Already has vert collision
        return true;
    }

    private static void AddRailGrindCollision(GameObject go)
    {
        #if UNITY_EDITOR
        Undo.RecordObject(go, "Add Vert Collision");
        #endif 
        GameObject railCollision = new($"{go.name}_VertCollision")
        {
            layer = PhysicsLayers.RailGrind
        };
        railCollision.transform.SetParent(go.transform, false);
        railCollision.AddComponent<RailGrindCollisionGenerator>();
        #if UNITY_EDITOR
        EditorUtility.SetDirty(go);
        #endif
    }

    private void Awake()
    {
        m_collisionMesh = new()
        {
            name = $"{gameObject.name}_CollisionMesh",
            hideFlags = HideFlags.HideAndDontSave,
        };

        gameObject.layer = PhysicsLayers.RailGrind;

        if (ValidateParent())
            GenerateCollisionMesh();
    }

    private void OnDestroy()
    {
        if (m_collisionMesh)
            DestroyImmediate(m_collisionMesh);
    }

    private bool ValidateParent()
    {
        var parent = transform.parent;
        if (parent == null) return false;
        if (!parent.TryGetComponent<ISplineProvider>(out var parentSpline)) return false;
        
        m_railSpline = parentSpline.GetSpline();
        return true;
    }
    
    private void GenerateCollisionMesh()
    {
// #if UNITY_EDITOR 
//         Undo.RecordObject(this, "Generate Collision Mesh For Vert");  
// #endif        
        var meshFilter = GetComponent<MeshFilter>();
        var meshCollider = GetComponent<MeshCollider>();
        
        if (!meshFilter || !meshCollider) return;
        
        var railEdgeCount = m_railSpline.ControlPointCount;
        var vertices = new Vector3[railEdgeCount * 4];
        var uvs = new Vector2[railEdgeCount * 4];
        var triangles = new int[railEdgeCount * 24];

        int segmentIndex = 0;
        float accumulatedDist = 0;
        Vector3 prevPos = Vector3.zero;
        foreach (var vertKnot in m_railSpline.ControlPointsList)
        {
            Quaternion rotation = vertKnot.GetOrientationFast(0f); // NOTE: need BakeRotation enabled on the segment otherwise this is wrong i believe
            
            Vector3 vertUp = rotation * Vector3.up;
            Vector3 vertForward = rotation * Vector3.forward;
            Vector3 vertRight = rotation * Vector3.right;
            
            Vector3 center = vertKnot.transform.position;
            if (segmentIndex == 0) prevPos = center;
            
            // 4 vertices
            var v1 = center - 0.5f * vertUp - vertRight * 0.5f;
            var v2 = center - 0.5f * vertUp + vertRight * 0.5f;
            var v3 = center + 0.5f * vertUp - vertRight * 0.5f;
            var v4 = center + 0.5f * vertUp + vertRight * 0.5f;
            
            vertices[segmentIndex * 4 + 0] = v1;
            vertices[segmentIndex * 4 + 1] = v2;
            vertices[segmentIndex * 4 + 2] = v3;
            vertices[segmentIndex * 4 + 3] = v4;
            
            // UVs
            accumulatedDist += (center - prevPos).magnitude;
            prevPos = center;

            uvs[segmentIndex * 4 + 0] = new Vector2(accumulatedDist, 0);
            uvs[segmentIndex * 4 + 1] = new Vector2(accumulatedDist, 0);
            uvs[segmentIndex * 4 + 2] = new Vector2(accumulatedDist, (v3 - v1).magnitude);
            uvs[segmentIndex * 4 + 3] = new Vector2(accumulatedDist, (v4 - v2).magnitude);
            
            // No need for triangle setup at last segment
            if (segmentIndex == railEdgeCount - 1) break;
            
            // Connectivity (8 triangles)
            // Left Face
            triangles[segmentIndex * 24 + 0] = segmentIndex * 4 + 0;
            triangles[segmentIndex * 24 + 1] = segmentIndex * 4 + (0 + 4);
            triangles[segmentIndex * 24 + 2] = segmentIndex * 4 + 2;
            
            triangles[segmentIndex * 24 + 3] = segmentIndex * 4 + 2;
            triangles[segmentIndex * 24 + 4] = segmentIndex * 4 + (0 + 4);
            triangles[segmentIndex * 24 + 5] = segmentIndex * 4 + (2 + 4);
            
            // Top Face
            triangles[segmentIndex * 24 + 6] = segmentIndex * 4 + 2;
            triangles[segmentIndex * 24 + 7] = segmentIndex * 4 + (2 + 4);
            triangles[segmentIndex * 24 + 8] = segmentIndex * 4 + (3 + 4);
            
            triangles[segmentIndex * 24 + 9] = segmentIndex * 4 + 3;
            triangles[segmentIndex * 24 + 10] = segmentIndex * 4 + 2;
            triangles[segmentIndex * 24 + 11] = segmentIndex * 4 + (3 + 4);
            
            // Right Face
            triangles[segmentIndex * 24 + 12] = segmentIndex * 4 + 1;
            triangles[segmentIndex * 24 + 13] = segmentIndex * 4 + (3 + 4);
            triangles[segmentIndex * 24 + 14] = segmentIndex * 4 + (1 + 4);
            
            triangles[segmentIndex * 24 + 15] = segmentIndex * 4 + 1;
            triangles[segmentIndex * 24 + 16] = segmentIndex * 4 + 3;
            triangles[segmentIndex * 24 + 17] = segmentIndex * 4 + (3 + 4);
            
            // Bottom Face
            triangles[segmentIndex * 24 + 18] = segmentIndex * 4 + 0;
            triangles[segmentIndex * 24 + 19] = segmentIndex * 4 + 1;
            triangles[segmentIndex * 24 + 20] = segmentIndex * 4 + (1 + 4);
            
            triangles[segmentIndex * 24 + 21] = segmentIndex * 4 + 0;
            triangles[segmentIndex * 24 + 22] = segmentIndex * 4 + (1 + 4);
            triangles[segmentIndex * 24 + 23] = segmentIndex * 4 + (0 + 4);
            
            segmentIndex++;
        }
        
        m_collisionMesh.indexFormat = triangles.Length > Int16.MaxValue 
            ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        
        m_collisionMesh.Clear();
        m_collisionMesh.vertices = vertices;
        m_collisionMesh.triangles = triangles;
        m_collisionMesh.uv = uvs;
        m_collisionMesh.RecalculateNormals();
        m_collisionMesh.RecalculateBounds();
        m_collisionMesh.Optimize();
        m_collisionMesh.name = $"{gameObject.name}_CollisionMesh";

        meshFilter.sharedMesh = m_collisionMesh;
        meshCollider.sharedMesh = m_collisionMesh;
        
        gameObject.isStatic = true;
        
// #if UNITY_EDITOR
//         EditorUtility.SetDirty(this);
// #endif
    }
    
#if UNITY_EDITOR     
    private static Material s_collisionMaterial;
    private void OnDrawGizmos()
    {
        if (s_collisionMaterial == null)
        {
            s_collisionMaterial = new Material(Shader.Find("Hidden/Editor/TriggerVisualizer"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var texture = Resources.Load<Texture2D>("T_Editor_TriggerViz");
            s_collisionMaterial.SetTexture("_MainTex", texture);

            AssemblyReloadEvents.beforeAssemblyReload += DestroyEditorMaterial;
        }
        
        if (m_collisionMesh == null) return;
        
        s_collisionMaterial.SetPass(0);
        Graphics.DrawMeshNow(m_collisionMesh, transform.localToWorldMatrix);;
    }

    private static void DestroyEditorMaterial()
    {
        if (s_collisionMaterial) DestroyImmediate(s_collisionMaterial);
        
        AssemblyReloadEvents.beforeAssemblyReload -= DestroyEditorMaterial;
    }
#endif
    
    public void Validate(SelfValidationResult result)
    {
        var parent = transform.parent;
        if (parent == null)
        {
            result.AddError($"No parent found on Railgrind Collision Generator, must have a parent to provide a Spline").WithFix(
                "Delete Useless Rail Grind",
                () =>
                {
                    DestroyImmediate(gameObject);
                });
            return;
        }

        if (parent.GetComponent<ISplineProvider>() == null)
        {
            result.AddError($"Possible Rail Grind Parent Has No Spline Provider, must have a parent spline").WithFix(
                "Delete Useless Rail Grind",
                () =>
                {
                    DestroyImmediate(gameObject);
                });
            return;
        }
    }
}