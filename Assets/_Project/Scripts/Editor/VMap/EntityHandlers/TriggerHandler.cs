using System;
using System.Collections.Generic;
using Datamodel.Vmap;
using UnityEngine;

namespace FS.VmapImport
{
    /// <summary>
    /// Handles trigger_multiple (and similar trigger brush entities).
    ///
    /// When a level designer ties meshes to a trigger_multiple entity in Hammer,
    /// those meshes define the trigger volume - they're invisible collision shapes.
    ///
    /// This handler builds each child mesh as a convex MeshCollider with isTrigger=true
    /// and no MeshRenderer. Multiple meshes under a single trigger all act as receivers
    /// for the same entity's events.
    /// </summary>
    public class TriggerHandler : INodeHandler
    {
        public string[] SupportedClassnames => new[] { "trigger_multiple", "trigger_once", "trigger_push" };
        public Type[] SupportedTypes => Array.Empty<Type>();

        public GameObject Process(
            MapNode node,
            Dictionary<string, string> properties,
            List<CMapMesh> childMeshes,
            VmapImportContext ctx)
        {
            var go = new GameObject(ctx.GetEntityDisplayName(node as BaseEntity, properties));

            // Build each child mesh as a trigger collider (no visual rendering)
            foreach (var childMesh in childMeshes)
                VmapMeshBuilder.BuildTriggerColliderGameObject(childMesh, go.transform, ctx);

            return go;
        }

        public void ResolveReferences(VmapImportContext ctx) { }
    }
}
