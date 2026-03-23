using System;
using System.Collections.Generic;
using System.Linq;
using Datamodel;
using Datamodel.Vmap;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace FS.VmapImport
{
    /// <summary>
    /// Unity ScriptedImporter for Valve .vmap files (Source 2 map format).
    ///
    /// Importing a .vmap produces a complete GameObject hierarchy with:
    ///   - Mesh geometry (with per-mesh static flags, colliders, surface tags)
    ///   - Entities dispatched through handler system (lights, triggers, text, cables, etc.)
    ///   - Instances: shared templates built once, stamped via Instantiate
    ///   - Material resolution with inspector remap support
    ///   - Topology preservation for runtime edge tools (PreservedMesh)
    ///
    /// Import pipeline phases:
    ///   Phase 0: Parse vmap, build element lookup, collect instance targets
    ///   Phase 1a: Build instance templates (reuses the full pipeline so templates
    ///             support any node type — meshes, entities, nested groups, etc.)
    ///   Phase 1b: Walk world hierarchy, dispatch nodes by type + classname
    ///   Phase 1c: Cleanup instance templates
    ///   Phase 2: Resolve cross-entity references (I/O wiring, target lookups)
    ///   Phase 3: Sync discovered materials to inspector remap list
    /// </summary>
    [ScriptedImporter(1, "vmap")]
    public class VmapImporter : ScriptedImporter
    {
        [SerializeField] private VmapImportSettings m_settings = new();

        /// <summary> Access to settings for the custom inspector. </summary>
        public VmapImportSettings Settings => m_settings;

        public override void OnImportAsset(AssetImportContext unityCtx)
        {
            // ============================================================
            //  Phase 0: Parse and prepare
            // ============================================================
            Datamodel.Datamodel dm;
            try
            {
                dm = Datamodel.Datamodel.Load(unityCtx.assetPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[VmapImporter] Failed to load '{unityCtx.assetPath}': {e.Message}");
                unityCtx.SetMainObject(new GameObject("IMPORT_FAILED"));
                return;
            }

            var ctx = new VmapImportContext(dm, unityCtx, m_settings);

            ctx.Materials = new MaterialResolver(m_settings.m_materialRemaps, ctx);

            BuildElementLookup(dm, ctx);

            if (dm.Root is not CMapRootElement root)
            {
                Debug.LogError("[VmapImporter] Root is not CMapRootElement. Is this a valid vmap?");
                unityCtx.SetMainObject(new GameObject("INVALID_VMAP"));
                return;
            }

            CMapWorld world = root.World;
            if (world == null)
            {
                Debug.LogError("[VmapImporter] No CMapWorld found in vmap.");
                unityCtx.SetMainObject(new GameObject("NO_WORLD"));
                return;
            }

            var rootGO = new GameObject(System.IO.Path.GetFileNameWithoutExtension(unityCtx.assetPath));
            var processor = new VmapNodeProcessor();

            // Scan the world tree for CMapInstance nodes and record which
            // groups they reference. These groups are templates that live
            // outside the world hierarchy and must be built explicitly.
            CollectInstanceTargets(world, ctx);

            // ============================================================
            //  Phase 1a: Build instance templates
            // ============================================================
            BuildInstanceTemplates(processor, ctx);

            // ============================================================
            //  Phase 1b: Walk world hierarchy and dispatch
            // ============================================================
            WalkWorldChildren(world, rootGO.transform, processor, ctx);

            // ============================================================
            //  Phase 1c: Cleanup instance templates
            // ============================================================
            // Templates have been stamped by every instance that references
            // them. The template GOs themselves are not part of the final
            // hierarchy — destroy them so they don't appear in the import.
            foreach (var templateGO in ctx.InstanceTemplateGOs.Values)
                DestroyImmediate(templateGO);

            // ============================================================
            //  Phase 2: Resolve cross-entity references
            // ============================================================
            processor.ResolveAllReferences(ctx);

            // ============================================================
            //  Phase 3: Sync discovered materials to remap list
            // ============================================================
            SyncMaterialRemaps(ctx);

            unityCtx.AddObjectToAsset("root", rootGO);
            unityCtx.SetMainObject(rootGO);

            int meshCount = ctx.MeshContentCache.Count;
            int entityCount = ctx.TargetnameRegistry.Count;
            int templateCount = ctx.InstanceTemplateGOs.Count;
            Debug.Log($"[VmapImporter] Import complete: {meshCount} unique meshes, {entityCount} named entities, "
                    + $"{templateCount} instance templates, {ctx.DiscoveredMaterials.Count} materials discovered.");
        }

        // ==================================================================
        //  Phase 0: Element Lookup & Instance Target Collection
        // ==================================================================

        /// <summary>
        /// Indexes all elements in the Datamodel by GUID for instance target resolution.
        /// </summary>
        private void BuildElementLookup(Datamodel.Datamodel dm, VmapImportContext ctx)
        {
            foreach (Element elem in dm.AllElements)
            {
                if (elem.ID != Guid.Empty)
                    ctx.ElementLookup[elem.ID] = elem;
            }
        }

        /// <summary>
        /// Recursively scans the world hierarchy for CMapInstance nodes and records
        /// the GUIDs of their target elements. These targets are template groups that
        /// must be built before the world walk so instances can Instantiate them.
        /// </summary>
        private void CollectInstanceTargets(MapNode parent, VmapImportContext ctx)
        {
            foreach (Element child in parent.Children)
            {
                if (child is CMapInstance instance && instance.Target != null)
                    ctx.InstanceTargetIDs.Add(instance.Target.ID);

                if (child is MapNode mapNode)
                    CollectInstanceTargets(mapNode, ctx);
            }
        }

        // ==================================================================
        //  Phase 1a: Instance Template Building
        // ==================================================================

        /// <summary>
        /// Builds a complete GameObject for each unique instance template.
        ///
        /// Template groups live outside CMapWorld as top-level elements. They are
        /// processed through the same WalkWorldChildren pipeline as everything else,
        /// so templates naturally support any content, meshes, entities, nested groups.
        ///
        /// After building, direct children are localized relative to the group origin.
        /// Source 2 stores all origins in world-space; localizing means instances can
        /// be placed at their own origin and children end up in the right spot.
        /// </summary>
        private void BuildInstanceTemplates(VmapNodeProcessor processor, VmapImportContext ctx)
        {
            // When building up a template to later be 'stamped' by the instance handler, don't register it (so it doesn't get 'picked up', we're gonna delete it after settig up instances)
            ctx.SuppressRegistration = true;
            
            foreach (var targetId in ctx.InstanceTargetIDs)
            {
                if (!ctx.ElementLookup.TryGetValue(targetId, out var element)) continue;
                if (element is not MapNode targetNode) continue;

                var templateGO = new GameObject($"[Template] {targetId}");
                WalkWorldChildren(targetNode, templateGO.transform, processor, ctx);

                // Localize direct children: subtract the group's world-space origin
                // so that children are positioned relative to the template root.
                // Nested children (e.g. meshes under entities) are already relative
                // to their parent and don't need adjustment.
                Vector3 groupOrigin = ctx.ConvertPosition(targetNode.Origin);
                foreach (Transform child in templateGO.transform)
                    child.localPosition -= groupOrigin;

                ctx.InstanceTemplateGOs[targetId] = templateGO;
            }
            
            // Reset it
            ctx.SuppressRegistration = false;
        }

        // ==================================================================
        //  Phase 1b: Hierarchy Walk
        // ==================================================================

        /// <summary>
        /// Walks the children of a MapNode, dispatching each child based on its type.
        ///
        /// Dispatch logic:
        ///   - CMapMesh → visual geometry (world brushes)
        ///   - CMapGroup / CMapWorldLayer / CMapPrefab → structural containers, recurse
        ///   - BaseEntity (CMapEntity, CMapCable, CMapInstance) → handler dispatch
        ///
        /// Structural containers do not apply their own transforms. Source 2 stores
        /// all origins in world-space, groups are purely organizational, not
        /// coordinate-space parents.
        ///
        /// Instance template groups (identified by InstanceTargetIDs) are skipped
        /// during the world walk. They are built separately in Phase 1a and stamped
        /// by instances during this walk via the InstanceHandler.
        /// </summary>
        private void WalkWorldChildren(MapNode parent, Transform parentTransform, VmapNodeProcessor processor, VmapImportContext ctx)
        {
            foreach (Element child in parent.Children)
            {
                try
                {
                    switch (child)
                    {
                        // ---- World geometry ----
                        case CMapMesh meshNode:
                            VmapMeshBuilder.BuildMeshGameObject(meshNode, parentTransform, ctx);
                            break;

                        // ---- Structural containers (recurse, no transform applied) ----
                        case CMapWorldLayer layer:
                            var layerGO = new GameObject(ctx.GetNodeDisplayName(layer, layer.WorldLayerName));
                            layerGO.transform.SetParent(parentTransform, false);
                            WalkWorldChildren(layer, layerGO.transform, processor, ctx);
                            break;

                        case CMapGroup group:
                            if (ctx.InstanceTargetIDs.Contains(group.ID))
                                break;

                            var groupGO = new GameObject(ctx.GetNodeDisplayName(group, "group"));
                            groupGO.transform.SetParent(parentTransform, false);
                            WalkWorldChildren(group, groupGO.transform, processor, ctx);
                            break;

                        case CMapPrefab prefab:
                            var prefabGO = new GameObject(ctx.GetNodeDisplayName(prefab, "prefab"));
                            prefabGO.transform.SetParent(parentTransform, false);
                            WalkWorldChildren(prefab, prefabGO.transform, processor, ctx);
                            break;

                        // ---- Entities → handler dispatch ----
                        case BaseEntity entity:
                            if (!ctx.Settings.m_importEntities && child is not CMapInstance) break;

                            var entityGO = processor.ProcessNode(entity, ctx);
                            if (entityGO != null)
                                entityGO.transform.SetParent(parentTransform, false);
                            break;

                        default:
                            break;
                    }
                }
                catch (Exception e)
                {
                    int nodeID = (child is MapNode mn) ? mn.NodeID : -1;
                    Debug.LogWarning($"[VmapImporter] Error processing node {nodeID} ({child.GetType().Name}): {e.Message}\n{e.StackTrace}");
                }
            }
        }

        // ==================================================================
        //  Phase 3: Material Remap Sync
        // ==================================================================

        /// <summary>
        /// Merges newly discovered material paths into the serialized remap list.
        /// Preserves existing user assignments while adding entries for new paths.
        /// </summary>
        private void SyncMaterialRemaps(VmapImportContext ctx)
        {
            var existing = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            foreach (var remap in m_settings.m_materialRemaps)
            {
                if (!string.IsNullOrEmpty(remap.sourcePath))
                    existing[remap.sourcePath] = remap.material;
            }

            foreach (string path in ctx.DiscoveredMaterials)
            {
                if (!existing.ContainsKey(path))
                    existing[path] = null;
            }

            m_settings.m_materialRemaps = existing
                .OrderBy(kv => kv.Key)
                .Select(kv => new MaterialRemap { sourcePath = kv.Key, material = kv.Value })
                .ToArray();
        }
    }
}
