# Vmap Importer — Architecture Guide

Unity ScriptedImporter for Valve .vmap files (Source 2 map format).
Converts Hammer maps into Unity GameObjects with geometry, entities, lighting, text labels, curves, and more.

## File Overview

```
VmapImporter/
├── VmapImporter.cs          — ScriptedImporter entry point, 4-phase pipeline
├── VmapImportContext.cs      — Shared state, coord conversion, naming, MaterialResolver
├── VmapImportSettings.cs     — Serialized settings, MaterialRemap, PrefabRemap, VmapClassification
├── VmapNodeProcessor.cs      — Unified dispatch (type + classname), INodeHandler interface
├── VmapMeshBuilder.cs        — Half-edge → Unity Mesh, content-hash dedup, PreservedMesh
├── VmapImporterEditor.cs     — Custom inspector (material remaps, prefab remaps, settings)
├── VmapCableTypes.cs         — CMapCable + CMapPathNode typed classes
├── VmapElementFactory.cs     — Datamodel class name → C# type registration
└── Handlers/
    ├── CurveHandler.cs       — CMapCable → CurvySpline (type-based dispatch)
    ├── InstanceHandler.cs    — CMapInstance → stamps shared geometry (type-based dispatch)
    ├── TriggerHandler.cs     — trigger_multiple → MeshCollider isTrigger (classname-based)
    ├── LightHandler.cs       — light_omni2 / light_environment → Unity Lights (classname-based)
    ├── WorldTextHandler.cs   — point_worldtext → TextMeshPro (classname-based)
    └── ExampleSpringHandler.cs — fs_launch_spring → prefab swap demo (classname-based)
```

## Import Pipeline Phases

### Phase 0 — Parse & Prepare
Load the .vmap via Datamodel.NET, build a GUID→Element lookup for instance resolution,
initialize the MaterialResolver with the user's remap table.

### Phase 1 — Walk & Dispatch
Recurse through CMapWorld's children. Each node type has a clear path:

| Node Type | Action |
|---|---|
| `CMapMesh` | Built directly as visual geometry (world brushes) |
| `CMapGroup` / `CMapWorldLayer` / `CMapPrefab` | Structural containers — create GO, recurse into children |
| `BaseEntity` subclasses | Sent through `VmapNodeProcessor` for handler dispatch |

The processor dispatches in two stages:
1. **Type-based**: `CMapCable` → `CurveHandler`, `CMapInstance` → `InstanceHandler`
2. **Classname-based**: `"trigger_multiple"` → `TriggerHandler`, `"light_omni2"` → `LightHandler`

If no handler matches, the entity gets a default GO with its child meshes built as visual geometry.

### Phase 2 — Resolve References
All handlers get a second pass to wire up cross-entity references (I/O connections,
target lookups, path chains) now that every GO exists.

### Phase 3 — Sync Materials
Merges discovered .vmat paths into the serialized remap list, preserving existing user assignments.

## How Entity-Mesh Parenting Works

In Hammer, "Tie Mesh to Entity" makes meshes children of an entity. What the child mesh
*means* depends entirely on the entity type:

| Entity Type | Child Mesh Behavior |
|---|---|
| `trigger_multiple` | Trigger collider (convex MeshCollider, isTrigger, no renderer) |
| `fs_skate_surface` | Visual mesh with surface component + tag |
| `fs_launch_spring` | Discarded (prefab replaces proxy geometry) |
| `func_door` | Visual mesh (door geometry) |
| Unhandled | Default: visual mesh + optional collider |

**Handlers own child mesh processing.** The processor extracts `List<CMapMesh> childMeshes`
and passes them to the handler, which decides what to do with them.

## How to Add a New Entity Handler

1. Create a class implementing `INodeHandler`
2. Return classnames from `SupportedClassnames` (or types from `SupportedTypes`)
3. Implement `Process()` — receives the node, its properties, and its child meshes
4. Register it in `VmapNodeProcessor`'s constructor

```csharp
public class MyEntityHandler : INodeHandler
{
    // What classnames this handler responds to
    public string[] SupportedClassnames => new[] { "fs_my_entity" };

    public GameObject Process(
        MapNode node,
        Dictionary<string, string> properties,
        List<CMapMesh> childMeshes,
        VmapImportContext ctx)
    {
        var go = new GameObject(ctx.GetEntityDisplayName(node as BaseEntity, properties));

        // Read properties (all strings, use extension methods for type conversion)
        float speed = properties.GetFloat("speed", 1.0f);
        bool enabled = properties.GetBool("StartDisabled") == false;
        Color color = properties.GetColor("color", Color.white);

        // Handle child meshes however your entity needs:
        // VmapNodeProcessor.BuildDefaultChildMeshes(childMeshes, go.transform, ctx);  // visual
        // VmapMeshBuilder.BuildTriggerColliderGameObject(mesh, go.transform, ctx);     // trigger

        return go;
    }
}
```

Then register in `VmapNodeProcessor` constructor:
```csharp
RegisterHandler(new MyEntityHandler());
```

## Mesh Deduplication

Content-hash based. Before building any mesh, a hash is computed from the CDmePolygonMesh's
structural arrays (vertex indices, edge connectivity, face structure, positions).
Identical geometry shares a single Mesh asset — catches both:
- **Instance reuse**: CMapInstance stamps of the same CMapGroup
- **Coincidental duplicates**: Two cubes created independently with the same dimensions

## Naming Strategy

Never empty. Priority:
1. `targetname` — designer-assigned name from entity properties
2. `Element.Name` — Datamodel binary-level name (rarely populated)
3. `classname` — entity type (e.g. "trigger_multiple")
4. C# type name — last resort (e.g. "CMapCable")

All names get a `_N` suffix for uniqueness.

## Coordinate Conversion

Source 2: right-handed (X-right, Y-forward, Z-up), units ≈ inches
Unity: left-handed (X-right, Y-up, Z-forward), units = meters

`Unity = (Src.X, Src.Z, Src.Y) × scale` where default scale is 1/64.

## Static Flags

Per-mesh, based on Hammer's `BakeLighting` property. If Hammer bakes lighting for a mesh,
it's static geometry. Entity meshes (triggers, doors) typically have BakeLighting off.

## Known Limitations

- **Concave N-gon triangulation**: Fan triangulation works for convex faces only
- **Light intensity**: Approximate conversion from Source 2 lumens to Unity intensity
- **Prefab swap**: Requires manual prefab assignment in inspector per import
- **I/O connections**: Data is extracted but runtime wiring not yet implemented
