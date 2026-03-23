using System;
using System.Collections.Generic;
using Datamodel.Vmap;
using UnityEngine;

namespace FS.VmapImport
{
    /// <summary>
    /// EXAMPLE handler demonstrating the prefab-swap pattern for game-specific entities.
    ///
    /// When a level designer places an fs_launch_spring entity in Hammer, Unity should
    /// replace it with the appropriate spring prefab (which varies per level theme -
    /// jungle spring, desert spring, etc.). The prefab is selected in the importer inspector,
    /// similar to how material remaps work.
    ///
    /// This handler is deliberately minimal - it's a template showing how to:
    ///   1. Register a classname-based handler
    ///   2. Read entity properties (launch_height, launch_angle, etc.)
    ///   3. Instantiate a prefab (or create a placeholder if none assigned)
    ///   4. Discard child meshes (the Hammer proxy geometry is replaced by the prefab)
    ///
    /// To implement your own entity handler, copy this pattern and change:
    ///   - SupportedClassnames to match your entity classname(s)
    ///   - Process() to read your entity's specific properties
    ///   - Add any runtime components your entity needs
    ///
    /// NOTE: The prefab remap system is defined in <see cref="VmapImportSettings"/>
    /// as PrefabRemap[] so you can assign prefabs per-classname in the inspector.
    /// This handler reads from ctx.Settings to find the assigned prefab.
    /// </summary>
    public class ExampleSpringHandler : INodeHandler
    {
        public string[] SupportedClassnames => new[] { "fs_launch_spring" };
        public Type[] SupportedTypes => Array.Empty<Type>();

        public GameObject Process(
            MapNode node,
            Dictionary<string, string> properties,
            List<CMapMesh> childMeshes,
            VmapImportContext ctx)
        {
            string displayName = ctx.GetEntityDisplayName(node as BaseEntity, properties);

            // --- Prefab instantiation ---
            // Look up assigned prefab from settings. If none assigned, create a placeholder.
            // The prefab remap system (PrefabRemap[]) lets designers assign different prefabs
            // per level theme - e.g. jungle_spring for the jungle level import.
            GameObject go;
            string classname = properties.GetValueOrDefault("classname", "fs_launch_spring");
            GameObject prefab = FindPrefabForClassname(classname, ctx);

            if (prefab != null)
            {
                // Instantiate the assigned prefab - this replaces the Hammer proxy geometry entirely
                go = UnityEngine.Object.Instantiate(prefab);
                go.name = displayName;
            }
            else
            {
                // No prefab assigned yet - create a visible placeholder so the LD can still
                // see where springs are placed and assign prefabs later.
                go = CreatePlaceholder(displayName);
            }

            // --- Read entity properties ---
            // These values come from the FGD definition and are set by the LD in Hammer's
            // entity property panel. They control runtime behavior, not appearance.
            //
            // Example: You'd add a SpringLauncher MonoBehaviour and populate it here:
            //
            //   var launcher = go.AddComponent<SpringLauncher>();
            //   launcher.launchHeight = properties.GetFloat("launch_height", 256f) * ctx.Settings.m_scale;
            //   launcher.launchAngle = properties.GetFloat("launch_angle", 75f);
            //   launcher.chargeTime = properties.GetFloat("charge_time", 0.5f);
            //   launcher.startEnabled = properties.GetBool("StartDisabled") == false;

            // --- Child meshes ---
            // Deliberately ignored. The mesh tied to a spring in Hammer is a proxy/placeholder
            // that the prefab replaces entirely. If you wanted to keep it (e.g. for a brush
            // entity where the mesh IS the entity), you'd call:
            //   VmapNodeProcessor.BuildDefaultChildMeshes(childMeshes, go.transform, ctx);

            return go;
        }

        public void ResolveReferences(VmapImportContext ctx) { }

        /// <summary>
        /// Looks up the prefab assigned to a classname in the importer's prefab remap table.
        /// Returns null if no prefab is assigned (placeholder will be created instead).
        /// </summary>
        private GameObject FindPrefabForClassname(string classname, VmapImportContext ctx)
        {
            if (ctx.Settings.m_prefabRemaps == null) return null;

            foreach (var remap in ctx.Settings.m_prefabRemaps)
            {
                if (string.Equals(remap.classname, classname, StringComparison.OrdinalIgnoreCase))
                    return remap.prefab;
            }
            return null;
        }

        /// <summary>
        /// Creates a bright yellow sphere as a visual placeholder for unassigned prefabs.
        /// Makes it easy to spot in the scene view and assign the real prefab later.
        /// </summary>
        private GameObject CreatePlaceholder(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.localScale = Vector3.one * 0.5f;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(
                    Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = Color.yellow;
                renderer.sharedMaterial = mat;
            }
            return go;
        }
    }
}
