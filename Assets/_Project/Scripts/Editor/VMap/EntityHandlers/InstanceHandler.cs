using System;
using System.Collections.Generic;
using Datamodel;
using Datamodel.Vmap;
using UnityEngine;

namespace FS.VmapImport
{
    /// <summary>
    /// Handles <see cref="CMapInstance"/> nodes — instanced references to a shared
    /// <see cref="CMapGroup"/> template.
    ///
    /// Instance templates are built during Phase 1a of the import pipeline using
    /// the same WalkWorldChildren path as all other nodes. This means templates
    /// naturally support any content — meshes, entities, nested groups — without
    /// the instance handler needing to know about specific node types.
    ///
    /// This handler simply looks up the pre-built template GameObject and clones it
    /// via Instantiate. Mesh, material, topology, and other assets are shared across
    /// all instances automatically since Instantiate preserves asset references.
    ///
    /// The instance's own transform is applied by ProcessNode after this handler
    /// returns, placing the cloned hierarchy at the correct world position.
    /// </summary>
    public class InstanceHandler : INodeHandler
    {
        public string[] SupportedClassnames => Array.Empty<string>();
        public Type[] SupportedTypes => new[] { typeof(CMapInstance) };

        public GameObject Process(
            MapNode node,
            Dictionary<string, string> properties,
            List<CMapMesh> childMeshes,
            VmapImportContext ctx)
        {
            if (node is not CMapInstance instance)
            {
                Debug.LogWarning("[VmapImporter] InstanceHandler received non-CMapInstance node, skipping.");
                return null;
            }

            Element targetElement = instance.Target;
            if (targetElement == null)
            {
                Debug.LogWarning("[VmapImporter] CMapInstance target is null, skipping.");
                return null;
            }

            if (!ctx.InstanceTemplateGOs.TryGetValue(targetElement.ID, out var templateGO))
            {
                Debug.LogWarning($"[VmapImporter] No template found for instance target {targetElement.ID}, skipping.");
                return null;
            }

            var instanceGO = GameObject.Instantiate(templateGO);
            instanceGO.name = ctx.GetNodeDisplayName(instance, "instance");

            return instanceGO;
        }

        public void ResolveReferences(VmapImportContext ctx) { }
    }
}
