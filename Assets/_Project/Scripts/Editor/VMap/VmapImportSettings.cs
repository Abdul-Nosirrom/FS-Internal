using System;
using Datamodel.Vmap;
using UnityEngine;

namespace FS.VmapImport
{
    /// <summary>
    /// Serialized mapping from a .vmat path found in the vmap to a Unity Material.
    /// Shown in the importer inspector so designers can drag-drop materials.
    /// </summary>
    [Serializable]
    public struct MaterialRemap
    {
        public string sourcePath;
        public Material material;
    }

    /// <summary>
    /// Serialized mapping from an entity classname to a Unity prefab.
    /// 
    /// Used for prefab-swap entities (springs, cannons, etc.) where the Hammer proxy
    /// geometry is replaced by a themed prefab in Unity. Different level themes can
    /// assign different prefabs to the same classname.
    ///
    /// Example: classname "fs_launch_spring" → jungle_spring_prefab for the jungle level.
    /// </summary>
    [Serializable]
    public struct PrefabRemap
    {
        [Tooltip("Entity classname from the FGD (e.g. fs_launch_spring, fs_cannon).")]
        public string classname;

        [Tooltip("Unity prefab to instantiate in place of this entity.")]
        public GameObject prefab;
    }

    /// <summary>
    /// Serializable import settings exposed in the ScriptedImporter inspector.
    /// Controls what gets imported and how geometry is processed.
    /// </summary>
    [Serializable]
    public class VmapImportSettings
    {
        [Header("Transform")]
        [Tooltip("Scale factor applied to all positions. 1/64 maps Hammer units (~1 inch) to Unity meters.")]
        public float m_scale = 1f / 64f;

        [Header("Mesh")]
        [Tooltip("Add MeshCollider components to imported meshes.")]
        public bool m_addColliders = true;

        [Tooltip("Build PreservedMesh assets for tagged surfaces (enables edge loop selection in runtime tools).")]
        public bool m_preserveTopology = true;

        [Tooltip("Mark meshes as static based on their Hammer BakeLighting property.")]
        public bool m_markStatic = true;

        [Header("Entities")]
        [Tooltip("Import entities and dispatch to registered handlers.")]
        public bool m_importEntities = true;

        [Tooltip("Import CMapCable curves as splines.")]
        public bool m_importCurves = true;

        [Header("Materials")]
        public MaterialRemap[] m_materialRemaps = Array.Empty<MaterialRemap>();

        [Header("Prefab Remaps")]
        [Tooltip("Map entity classnames to prefabs. Used by prefab-swap handlers (springs, cannons, etc.).")]
        public PrefabRemap[] m_prefabRemaps = Array.Empty<PrefabRemap>();
    }

    // ======================================================================
    //  MESH CLASSIFICATION
    // ======================================================================

    /// <summary>
    /// Centralized predicates for mesh classification decisions.
    ///
    /// All methods take the full typed <see cref="CMapMesh"/> so the implementation
    /// can read any combination of properties (name, physicsGroup, material, BakeLighting)
    /// without leaking what it checks into call-site signatures.
    ///
    /// To change classification logic later (e.g. switch from name-based to property-based),
    /// edit ONLY this class - no other code needs to change.
    /// </summary>
    public static class VmapClassification
    {
        /// <summary>
        /// Returns the surface tag for a mesh (e.g. "railgrind", "vert", "wallslide"),
        /// or null if it's a default surface with no special tag.
        /// </summary>
        public static string GetSurfaceTag(CMapMesh mesh)
        {
            string name = mesh.Name ?? "";
            string physicsGroup = mesh.PhysicsGroup ?? "";

            if (ContainsAny(name, physicsGroup, "railgrind")) return "railgrind";
            if (ContainsAny(name, physicsGroup, "vert")) return "vert";
            if (ContainsAny(name, physicsGroup, "wallslide")) return "wallslide";

            return null;
        }

        /// <summary>
        /// Whether this mesh's half-edge topology should be preserved as a PreservedMeshAsset.
        /// True for any tagged surface (edge loop selection needs topology) and meshes
        /// explicitly marked with "preserve" in their name.
        /// </summary>
        public static bool ShouldPreserveTopology(CMapMesh mesh)
        {
            if (GetSurfaceTag(mesh) != null) return true;

            string name = mesh.Name ?? "";
            return name.IndexOf("preserve", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Whether this mesh should be marked as static in Unity.
        /// Uses Hammer's BakeLighting property as a proxy - if Hammer bakes lighting for it,
        /// it's static geometry. Entity meshes (triggers, doors) typically have this off.
        /// </summary>
        public static bool ShouldBeStatic(CMapMesh mesh)
        {
            return mesh.BakeLighting;
        }

        private static bool ContainsAny(string a, string b, string keyword)
        {
            return a.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
                || b.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
