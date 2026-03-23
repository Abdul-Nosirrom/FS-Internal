using UnityEngine;
using UnityEditor;
using System.IO;
using Datamodel;
using Datamodel.Vmap;

/// <summary>
/// Quick test to verify Datamodel.NET can read .vmap files in Unity
/// using strongly-typed vmap classes.
/// </summary>
public static class VmapReadTest
{
    [MenuItem("Tools/Test Vmap Read")]
    static void TestVmapRead()
    {
        var path = EditorUtility.OpenFilePanel("Select a .vmap file", "", "vmap");
        if (string.IsNullOrEmpty(path)) return;

        Debug.Log($"<b>Loading:</b> {path}");

        using var stream = File.OpenRead(path);
        var dm = Datamodel.Datamodel.Load(stream, Datamodel.Codecs.DeferredMode.Disabled);

        Debug.Log($"Format: {dm.Format} v{dm.FormatVersion}");

        // === ROOT ===
        var root = dm.Root as CMapRootElement;
        if (root != null)
            Debug.Log($"<b>Root:</b> isPrefab={root.IsPrefab} editorVersion={root.EditorVersion} gridSpacing={root.GridSpacing}");
        else
            Debug.Log($"Root class: {dm.Root.ClassName} (not typed as CMapRootElement, falling back to untyped)");

        // === WORLD ===
        CMapWorld world = root?.World ?? dm.Root["world"] as CMapWorld;
        if (world == null)
        {
            Debug.LogWarning("Could not find world element!");
            return;
        }

        Debug.Log($"<b>World:</b> classname={world.EntityProperties["classname"]} mapUsageType={world.MapUsageType}");

        foreach (var key in world.EntityProperties.Keys)
            Debug.Log($"  worldspawn: {key} = {world.EntityProperties[key]}");

        // === VISIBILITY ===
        var visibility = root?.Visibility;
        if (visibility != null)
            Debug.Log($"<b>Visibility:</b> {visibility.HiddenFlags.Count} hidden flags");

        // === WALK CHILDREN ===
        Debug.Log($"<b>World has {world.Children.Count} children</b>");

        int meshCount = 0, entityCount = 0, groupCount = 0, layerCount = 0, prefabCount = 0, otherCount = 0;

        foreach (Element child in world.Children)
        {
            if (child is CMapMesh mesh)
            {
                meshCount++;
                LogMesh(mesh);
            }
            else if (child is CMapEntity entity)
            {
                entityCount++;
                LogEntity(entity, 0);
            }
            else if (child is CMapWorldLayer layer)
            {
                layerCount++;
                Debug.Log($"<b>[Layer]</b> {layer.WorldLayerName} (nodeID={layer.NodeID})");
                WalkChildren(layer, 1);
            }
            else if (child is CMapGroup group)
            {
                groupCount++;
                Debug.Log($"[Group] nodeID={group.NodeID}");
                WalkChildren(group, 1);
            }
            else if (child is CMapPrefab prefab)
            {
                prefabCount++;
                Debug.Log($"[Prefab] targetMap={prefab.TargetMapPath} targetName={prefab.TargetName}");
            }
            else
            {
                otherCount++;
                Debug.Log($"[Other] {child.ClassName}");
            }
        }

        Debug.Log($"<b>Summary:</b> {meshCount} meshes, {entityCount} entities, {groupCount} groups, {layerCount} layers, {prefabCount} prefabs, {otherCount} other");
        Debug.Log("=== Vmap read test complete ===");
    }

    static void WalkChildren(MapNode node, int depth)
    {
        var indent = new string(' ', depth * 2);
        int meshCount = 0, entityCount = 0, otherCount = 0;

        foreach (Element child in node.Children)
        {
            if (child is CMapMesh mesh)
            {
                meshCount++;
                if (meshCount <= 3) LogMesh(mesh, depth);
            }
            else if (child is CMapEntity entity)
            {
                entityCount++;
                LogEntity(entity, depth);
            }
            else if (child is CMapWorldLayer layer)
            {
                Debug.Log($"{indent}<b>[Layer]</b> {layer.WorldLayerName}");
                WalkChildren(layer, depth + 1);
            }
            else if (child is CMapGroup group)
            {
                Debug.Log($"{indent}[Group] nodeID={group.NodeID}");
                WalkChildren(group, depth + 1);
            }
            else if (child is CMapPrefab prefab)
            {
                Debug.Log($"{indent}[Prefab] {prefab.TargetMapPath}");
            }
            else
            {
                otherCount++;
            }
        }

        if (meshCount > 3)
            Debug.Log($"{indent}  ...and {meshCount - 3} more meshes");
        if (meshCount > 0 || entityCount > 0 || otherCount > 0)
            Debug.Log($"{indent}Subtotal: {meshCount} meshes, {entityCount} entities, {otherCount} other");
    }

    static void LogEntity(CMapEntity entity, int depth)
    {
        var indent = new string(' ', depth * 2);
        var classname = entity.EntityProperties.ContainsKey("classname") ? (string)entity.EntityProperties["classname"] : "???";
        var targetname = entity.EntityProperties.ContainsKey("targetname") ? (string)entity.EntityProperties["targetname"] : "";

        Debug.Log($"{indent}<b>[Entity]</b> {classname} targetname=\"{targetname}\" origin={entity.Origin}");

        foreach (var key in entity.EntityProperties.Keys)
        {
            if ((string)key == "classname" || (string)key == "targetname") continue;
            Debug.Log($"{indent}  {key} = {entity.EntityProperties[key]}");
        }

        // I/O connections
        foreach (Element connElem in entity.ConnectionsData)
        {
            if (connElem is DmeConnectionData conn)
                Debug.Log($"{indent}  IO: {conn.OutputName} -> {conn.TargetName}.{conn.InputName} (delay={conn.Delay}, fire={conn.TimesToFire})");
        }

        // Brush entity children
        if (entity.Children.Count > 0)
        {
            Debug.Log($"{indent}  Has {entity.Children.Count} children (brush entity)");
            WalkChildren(entity, depth + 1);
        }
    }

    static void LogMesh(CMapMesh mesh, int depth = 0)
    {
        var indent = new string(' ', depth * 2);
        var polyMesh = mesh.MeshData;

        Debug.Log($"{indent}[Mesh] faces={polyMesh.FaceEdgeIndices.Count} materials={polyMesh.Materials.Count} " +
                  $"smoothing={mesh.SmoothingAngle} physics={mesh.PhysicsType} " +
                  $"tint=({mesh.TintColor.R},{mesh.TintColor.G},{mesh.TintColor.B},{mesh.TintColor.A}) " +
                  $"renderAmt={mesh.RenderAmount} shadows={!mesh.DisableShadows}");

        foreach (var mat in polyMesh.Materials)
            Debug.Log($"{indent}  material: {mat}");

        LogDataStreams(polyMesh.VertexData, "VertexData", indent);
        LogDataStreams(polyMesh.FaceVertexData, "FaceVertexData", indent);
        LogDataStreams(polyMesh.EdgeData, "EdgeData", indent);
        LogDataStreams(polyMesh.FaceData, "FaceData", indent);
    }

    static void LogDataStreams(CDmePolygonMeshDataArray dataArray, string label, string indent)
    {
        if (dataArray.Streams.Count == 0) return;

        Debug.Log($"{indent}  {label} (size={dataArray.Size}):");
        foreach (Element streamElem in dataArray.Streams)
        {
            if (streamElem is CDmePolygonMeshDataStream stream)
                Debug.Log($"{indent}    stream: {stream.StandardAttributeName} semantic={stream.SemanticName}:{stream.SemanticIndex}");
            else
                Debug.Log($"{indent}    stream (untyped): {streamElem.ClassName}");
        }
    }
}