using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datamodel;
using Datamodel.Vmap;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace FS.VmapImport
{
    /// <summary>
    /// Shared state passed through all import phases.
    /// 
    /// Owns coordinate conversion, entity registries for cross-referencing,
    /// mesh deduplication, material resolution, and unique naming.
    /// Every handler receives this context to access shared services.
    /// </summary>
    public class VmapImportContext
    {
        public Datamodel.Datamodel Datamodel { get; }
        public AssetImportContext AssetContext { get; }
        public VmapImportSettings Settings { get; }

        // ------------------------------------------------------------------
        //  Element & Entity Registries
        // ------------------------------------------------------------------

        /// <summary> All elements indexed by GUID - used by InstanceHandler to resolve targets. </summary>
        public Dictionary<Guid, Element> ElementLookup { get; } = new();

        /// <summary> GameObjects keyed by Hammer targetname - used in pass 2 for I/O wiring. </summary>
        public Dictionary<string, GameObject> TargetnameRegistry { get; } = new();

        /// <summary> GameObjects keyed by Hammer nodeID - alternative cross-reference. </summary>
        public Dictionary<int, GameObject> NodeRegistry { get; } = new();

        
        // ------------------------------------------------------------------
        //  Instance Templates
        // ------------------------------------------------------------------
 
        /// <summary>
        /// When true, entity registration (targetname, nodeID) is suppressed.
        /// Set during instance template building, templates are blueprints,
        /// not live entities. Only stamped instances should be registered.
        /// </summary>
        public bool SuppressRegistration { get; set; }
        
        /// <summary>
        /// GUIDs of elements that are referenced as instance targets.
        /// Collected during Phase 0 by scanning CMapInstance nodes.
        /// Used to skip template groups during the world walk (they are
        /// built separately in Phase 1a) and to identify which elements
        /// need template GameObjects built for them.
        /// </summary>
        public HashSet<Guid> InstanceTargetIDs { get; } = new();
 
        /// <summary>
        /// Pre-built template GameObjects keyed by target element GUID.
        /// Built during Phase 1a via the same WalkWorldChildren pipeline.
        /// Instances clone these via Instantiate, sharing all assets.
        /// Destroyed during Phase 1c after all instances have been stamped.
        /// </summary>
        public Dictionary<Guid, GameObject> InstanceTemplateGOs { get; } = new();
        
        // ------------------------------------------------------------------
        //  Mesh Deduplication
        // ------------------------------------------------------------------

        /// <summary>
        /// Built mesh assets keyed by content hash.
        /// Two meshes with identical geometry (positions, normals, UVs, indices)
        /// will produce the same hash and share a single Mesh asset.
        /// This catches both instance reuse AND coincidental duplicates
        /// (e.g. two cubes created independently in Hammer).
        /// </summary>
        public Dictionary<int, Mesh> MeshContentCache { get; } = new();

        /// <summary>
        /// Material paths per cached mesh, so consumers can assign materials
        /// without re-extracting them from the CDmePolygonMesh.
        /// </summary>
        public Dictionary<int, List<string>> MeshMaterialCache { get; } = new();

        // ------------------------------------------------------------------
        //  Materials
        // ------------------------------------------------------------------

        /// <summary> Every unique .vmat path found during import. Synced to inspector remap UI at the end. </summary>
        public HashSet<string> DiscoveredMaterials { get; } = new();

        /// <summary> Material resolution logic. Initialized by the importer with the remap table. </summary>
        public MaterialResolver Materials { get; set; }

        // ------------------------------------------------------------------
        //  Naming
        // ------------------------------------------------------------------

        /// <summary> Tracks how many times each base name has been used, for generating unique suffixes. </summary>
        private readonly Dictionary<string, int> m_nameCounters = new(StringComparer.OrdinalIgnoreCase);

        /// <summary> Sub-asset counter for guaranteed unique asset identifiers. </summary>
        private int m_subAssetCounter;

        // ------------------------------------------------------------------
        //  Constructor
        // ------------------------------------------------------------------

        public VmapImportContext(Datamodel.Datamodel dm, AssetImportContext ctx, VmapImportSettings settings)
        {
            Datamodel = dm;
            AssetContext = ctx;
            Settings = settings;
        }

        // ------------------------------------------------------------------
        //  Entity Registration
        // ------------------------------------------------------------------

        public void RegisterTargetname(string targetname, GameObject go)
        {
            if (SuppressRegistration) return;
            if (!string.IsNullOrEmpty(targetname))
                TargetnameRegistry[targetname] = go;
        }

        public void RegisterNode(int nodeID, GameObject go)
        {
            if (SuppressRegistration) return;
            NodeRegistry[nodeID] = go;
        }

        /// <summary> Look up a previously registered entity by its Hammer targetname. </summary>
        public GameObject FindByTargetname(string targetname)
        {
            if (string.IsNullOrEmpty(targetname)) return null;
            TargetnameRegistry.TryGetValue(targetname, out var go);
            return go;
        }

        // ------------------------------------------------------------------
        //  Sub-Asset Management
        // ------------------------------------------------------------------

        /// <summary> Registers a Unity Object as a sub-asset with an auto-incremented unique identifier. </summary>
        public void AddSubAsset(string label, UnityEngine.Object obj)
        {
            string uniqueName = $"{label}_{m_subAssetCounter++}";
            obj.name = uniqueName;
            AssetContext.AddObjectToAsset(uniqueName, obj);
        }

        /// <summary> Registers a Unity Object as a sub-asset with an explicit name. </summary>
        public void AddSubAssetExplicit(string name, UnityEngine.Object obj)
        {
            obj.name = name;
            AssetContext.AddObjectToAsset($"asset_{m_subAssetCounter++}", obj);
        }

        // ------------------------------------------------------------------
        //  Naming Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Generates a unique name for a BaseEntity node.
        /// 
        /// Priority order:
        ///   1. targetname from entity properties (what the LD typed in Hammer)
        ///   2. Element.Name (Datamodel binary-level name, rarely populated)
        ///   3. classname from entity properties (e.g. "trigger_multiple")
        ///   4. C# type name (e.g. "CMapCable") as last resort
        ///
        /// All names get a unique suffix (_0, _1, etc.) to prevent duplicates.
        /// </summary>
        public string GetEntityDisplayName(BaseEntity entity, Dictionary<string, string> properties)
        {
            // 1. Targetname - the designer-assigned name
            if (properties.TryGetValue("targetname", out string targetname) && !string.IsNullOrEmpty(targetname))
                return MakeUnique(targetname);

            // 2. Element.Name - Datamodel binary-level name (rarely set, but cables may use it)
            if (!string.IsNullOrEmpty(entity.Name))
                return MakeUnique(entity.Name);

            // 3. Classname - the entity type (e.g. "trigger_multiple", "light_omni2")
            if (properties.TryGetValue("classname", out string classname) && !string.IsNullOrEmpty(classname))
                return MakeUnique(classname);

            // 4. C# type name as absolute fallback
            return MakeUnique(entity.GetType().Name);
        }

        /// <summary>
        /// Generates a unique name for a structural MapNode (groups, layers, meshes).
        /// Uses Element.Name with nodeID fallback.
        /// </summary>
        public string GetNodeDisplayName(MapNode node, string fallbackPrefix = "node")
        {
            if (!string.IsNullOrEmpty(node.Name))
                return MakeUnique(node.Name);

            return MakeUnique($"{fallbackPrefix}_{node.NodeID}");
        }

        /// <summary>
        /// Appends a numeric suffix to ensure no two GameObjects share the same name.
        /// First occurrence of "trigger_multiple" becomes "trigger_multiple_0", etc.
        /// </summary>
        private string MakeUnique(string baseName)
        {
            if (!m_nameCounters.TryGetValue(baseName, out int count))
                count = 0;
            m_nameCounters[baseName] = count + 1;
            return $"{baseName}_{count}";
        }

        // ------------------------------------------------------------------
        //  Coordinate Conversion
        //
        //  Source 2: right-handed (X-right, Y-forward, Z-up)
        //  Unity:    left-handed  (X-right, Y-up, Z-forward)
        //  Conversion: Unity = (Src.X, Src.Z, Src.Y) × scale
        // ------------------------------------------------------------------

        /// <summary> Converts a Source 2 position to Unity world space with scale. </summary>
        public Vector3 ConvertPosition(System.Numerics.Vector3 source)
        {
            return new Vector3(source.X, source.Z, source.Y) * Settings.m_scale;
        }

        /// <summary> Converts a Source 2 direction to Unity (no scale - used for normals). </summary>
        public Vector3 ConvertDirection(System.Numerics.Vector3 source)
        {
            return new Vector3(source.X, source.Z, source.Y);
        }

        /// <summary> Converts Source 2 QAngle (pitch, yaw, roll) to Unity Quaternion. </summary>
        public Quaternion ConvertAngles(System.Numerics.Vector3 qangle)
        {
            return Quaternion.Euler(-qangle.Z, -qangle.Y, -qangle.X);
        }

        /// <summary> Raw Vector3 conversion without axis swizzle (used for per-axis scales). </summary>
        public static Vector3 ToUnity(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
    }

    // ======================================================================
    //  MATERIAL RESOLVER
    // ======================================================================

    /// <summary>
    /// Resolves .vmat material paths to Unity Material assets.
    /// 
    /// Resolution order:
    ///   1. User remap - explicit drag-drop assignment in the inspector
    ///   2. Project search - finds a Unity Material with the matching filename
    ///   3. Placeholder - an orange material created as a sub-asset
    ///
    /// Stored on VmapImportContext so any handler can call ctx.Materials.Resolve(paths).
    /// </summary>
    public class MaterialResolver
    {
        private readonly Dictionary<string, Material> m_remapLookup;
        private readonly VmapImportContext m_ctx;
        private Material m_placeholder;

        public MaterialResolver(MaterialRemap[] remaps, VmapImportContext ctx)
        {
            m_ctx = ctx;
            m_remapLookup = new Dictionary<string, Material>();
            foreach (var remap in remaps)
            {
                if (remap.material != null)
                    m_remapLookup[remap.sourcePath] = remap.material;
            }
        }

        /// <summary>
        /// Resolves a list of .vmat paths to Unity Materials.
        /// Each path is tracked in DiscoveredMaterials for inspector sync.
        /// </summary>
        public Material[] Resolve(List<string> materialPaths)
        {
            if (materialPaths == null || materialPaths.Count == 0)
                return new[] { GetPlaceholder() };

            var materials = new Material[materialPaths.Count];
            for (int i = 0; i < materialPaths.Count; i++)
            {
                string path = materialPaths[i];
                m_ctx.DiscoveredMaterials.Add(path);

                // 1. User remap (explicit inspector assignment)
                if (m_remapLookup.TryGetValue(path, out var remapped))
                {
                    materials[i] = remapped;
                    continue;
                }

                // 2. Search project by filename
                materials[i] = SearchProjectMaterial(path);

                // 3. Placeholder fallback
                if (materials[i] == null)
                    materials[i] = GetPlaceholder();
            }
            return materials;
        }

        private Material SearchProjectMaterial(string vmatPath)
        {
            string matName = Path.GetFileNameWithoutExtension(vmatPath);
            if (string.IsNullOrEmpty(matName)) return null;

            var guids = AssetDatabase.FindAssets($"{matName} t:Material");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (candidate != null && candidate.name == matName)
                    return candidate;
            }
            return null;
        }

        private Material GetPlaceholder()
        {
            if (m_placeholder != null) return m_placeholder;

            m_placeholder = new Material(
                Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m_placeholder.color = new UnityEngine.Color(0.8f, 0.5f, 0.2f);
            m_placeholder.name = "VmapPlaceholder";
            m_ctx.AddSubAssetExplicit("mat_placeholder", m_placeholder);
            return m_placeholder;
        }
    }
}
