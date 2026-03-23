using System;
using System.Collections.Generic;
using System.Linq;
using Datamodel;
using Datamodel.Vmap;
using UnityEngine;

namespace FS.VmapImport
{
    // ======================================================================
    //  HANDLER INTERFACE
    // ======================================================================

    /// <summary>
    /// Interface for handling vmap nodes during import.
    ///
    /// Dispatch works in two stages:
    ///   1. Type-based: if the node's C# type matches (e.g. <see cref="CMapCable"/>,
    ///      <see cref="CMapInstance"/>), the corresponding handler is used.
    ///      This handles format-level types that are structurally distinct.
    ///   2. Classname-based: if no type match, the node's entity classname property
    ///      is checked (e.g. "trigger_multiple", "light_omni2", "fs_launch_spring").
    ///      This handles game-specific entities that share the <see cref="CMapEntity"/> type.
    ///
    /// Import runs in two passes:
    ///   Pass 1 (Process): Create GameObjects, add components, parse properties.
    ///   Pass 2 (ResolveReferences): Wire up cross-entity references (I/O connections,
    ///           target_destination lookups, path chains) now that all GOs exist.
    /// </summary>
    public interface INodeHandler
    {
        /// <summary>
        /// Entity classnames this handler responds to (e.g. "trigger_multiple", "light_omni2").
        /// Return empty for type-only handlers (CMapCable, CMapInstance).
        /// </summary>
        string[] SupportedClassnames => Array.Empty<string>();

        /// <summary>
        /// Typed node classes this handler responds to (e.g. typeof(CMapCable)).
        /// Checked BEFORE classname - use this for format-level types.
        /// Return empty for classname-only handlers.
        /// </summary>
        Type[] SupportedTypes => Array.Empty<Type>();

        /// <summary>
        /// Pass 1: Create and configure a GameObject for this node.
        ///
        /// <paramref name="childMeshes"/> contains any CMapMesh children of this entity.
        /// The handler decides what to do with them:
        ///   - Trigger: build as trigger colliders (no renderer)
        ///   - Skate surface: build as visual mesh with surface component
        ///   - Spring: discard (prefab swap replaces the proxy geometry)
        ///   - Default: visual mesh with optional collider
        ///
        /// Return null to let the processor create a default empty GO.
        /// </summary>
        GameObject Process(
            MapNode node,
            Dictionary<string, string> properties,
            List<CMapMesh> childMeshes,
            VmapImportContext ctx);

        /// <summary>
        /// Pass 2: Wire up references that depend on other entities existing.
        /// Called once after ALL entities have been created. Default does nothing.
        /// </summary>
        void ResolveReferences(VmapImportContext ctx) { }
    }

    // ======================================================================
    //  PROCESSOR (dispatch + registry)
    // ======================================================================

    /// <summary>
    /// Orchestrates node import with unified type + classname dispatch.
    ///
    /// Maintains two handler registries:
    ///   - <see cref="m_typeHandlers"/>: keyed by C# Type, checked first
    ///   - <see cref="m_classnameHandlers"/>: keyed by entity classname string, checked second
    ///
    /// All BaseEntity subclasses (CMapEntity, CMapCable, CMapInstance) flow through here.
    /// </summary>
    public class VmapNodeProcessor
    {
        private readonly Dictionary<Type, INodeHandler> m_typeHandlers = new();
        private readonly Dictionary<string, INodeHandler> m_classnameHandlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<INodeHandler> m_allHandlers = new();

        public VmapNodeProcessor()
        {
            // ---- Type-based handlers (format-level types) ----
            RegisterHandler(new CurveHandler());
            RegisterHandler(new InstanceHandler());

            // ---- Classname-based handlers (game/engine entities) ----
            RegisterHandler(new TriggerHandler());
            RegisterHandler(new LightHandler());
            RegisterHandler(new WorldTextHandler());
            RegisterHandler(new ExampleSpringHandler());
        }

        /// <summary>
        /// Registers a handler. Type registrations are checked before classname registrations.
        /// </summary>
        public void RegisterHandler(INodeHandler handler)
        {
            m_allHandlers.Add(handler);

            foreach (var type in handler.SupportedTypes)
            {
                if (m_typeHandlers.ContainsKey(type))
                    Debug.LogWarning($"[VmapImporter] Duplicate type handler for {type.Name}, overwriting.");
                m_typeHandlers[type] = handler;
            }

            foreach (string classname in handler.SupportedClassnames)
            {
                if (m_classnameHandlers.ContainsKey(classname))
                    Debug.LogWarning($"[VmapImporter] Duplicate classname handler for '{classname}', overwriting.");
                m_classnameHandlers[classname] = handler;
            }
        }

        /// <summary> Retrieves a registered handler by its C# type (for direct access). </summary>
        public T GetHandler<T>() where T : class, INodeHandler
        {
            return m_allHandlers.OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Pass 1: Process a BaseEntity node.
        ///
        /// 1. Extracts entity properties and child meshes
        /// 2. Finds a handler (type-first, then classname)
        /// 3. Dispatches to the handler or creates a default GO
        /// 4. Applies transform and registers for cross-referencing
        /// </summary>
        public GameObject ProcessNode(BaseEntity entity, VmapImportContext ctx)
        {
            var properties = ExtractEntityProperties(entity);
            var childMeshes = ExtractChildMeshes(entity);
            string classname = properties.GetValueOrDefault("classname", "");
            string targetname = properties.GetValueOrDefault("targetname", "");

            // Find handler: type-based first, then classname-based
            INodeHandler handler = FindHandler(entity, classname);

            GameObject go = null;
            if (handler != null)
                go = handler.Process(entity, properties, childMeshes, ctx);

            // Fallback: named empty GO for entities without a handler (or handler returned null)
            if (go == null)
            {
                go = new GameObject(ctx.GetEntityDisplayName(entity, properties));

                // Default behavior: build child meshes as visual geometry with colliders.
                // This is what happens for unhandled brush entities - their meshes just render normally.
                BuildDefaultChildMeshes(childMeshes, go.transform, ctx);
            }

            ApplyMapNodeTransform(entity, go, ctx);
            ctx.RegisterTargetname(targetname, go);
            ctx.RegisterNode(entity.NodeID, go);

            return go;
        }

        /// <summary> Pass 2: Let every handler resolve cross-entity references. </summary>
        public void ResolveAllReferences(VmapImportContext ctx)
        {
            foreach (var handler in m_allHandlers)
                handler.ResolveReferences(ctx);
        }

        // ------------------------------------------------------------------
        //  Dispatch
        // ------------------------------------------------------------------

        /// <summary>
        /// Finds the appropriate handler for a node.
        /// Checks type registry first (CMapCable, CMapInstance), then classname registry.
        /// </summary>
        private INodeHandler FindHandler(MapNode node, string classname)
        {
            // 1. Type-based (format-level types like CMapCable, CMapInstance)
            if (m_typeHandlers.TryGetValue(node.GetType(), out var typeHandler))
                return typeHandler;

            // 2. Classname-based (game entities like trigger_multiple, light_omni2)
            if (!string.IsNullOrEmpty(classname) && m_classnameHandlers.TryGetValue(classname, out var classHandler))
                return classHandler;

            return null;
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Flattens a BaseEntity's EditGameClassProps into a string→string dictionary.
        /// All vmap entity properties are stored as strings regardless of actual type
        /// (e.g. "launch_height" → "256", "enabled" → "1").
        /// </summary>
        public static Dictionary<string, string> ExtractEntityProperties(BaseEntity entity)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var props = entity.EntityProperties;
            if (props == null) return result;

            foreach (var attr in props)
            {
                if (attr.Key == "id") continue; // Skip the internal Datamodel element ID
                try { result[attr.Key] = attr.Value?.ToString() ?? ""; }
                catch { /* skip unreadable attributes */ }
            }
            return result;
        }

        /// <summary>
        /// Collects all direct CMapMesh children from a node's Children array.
        /// These are the "tied to entity" meshes that define the entity's geometry.
        /// </summary>
        public static List<CMapMesh> ExtractChildMeshes(MapNode node)
        {
            var meshes = new List<CMapMesh>();
            foreach (Element child in node.Children)
            {
                if (child is CMapMesh mesh)
                    meshes.Add(mesh);
            }
            return meshes;
        }

        /// <summary> Applies Origin/Angles/Scales from any MapNode to a GameObject. </summary>
        public static void ApplyMapNodeTransform(MapNode node, GameObject go, VmapImportContext ctx)
        {
            go.transform.localPosition = ctx.ConvertPosition(node.Origin);
            go.transform.localRotation = ctx.ConvertAngles(node.Angles);
            go.transform.localScale = VmapImportContext.ToUnity(node.Scales);
        }

        /// <summary>
        /// Default child mesh handling: builds each CMapMesh as a visual GO with
        /// MeshFilter, MeshRenderer, and optional MeshCollider.
        /// Used for unhandled brush entities.
        /// </summary>
        public static void BuildDefaultChildMeshes(List<CMapMesh> meshes, Transform parent, VmapImportContext ctx)
        {
            foreach (var childMesh in meshes)
                VmapMeshBuilder.BuildMeshGameObject(childMesh, parent, ctx);
        }

        /// <summary>
        /// Extracts I/O connection data from a BaseEntity.
        ///
        /// Each connection represents a wire in Hammer's I/O system:
        ///   "When this entity fires [outputName], call [inputName] on entity named [targetName]"
        ///
        /// Consumed in ResolveReferences to wire up UnityEvents or similar runtime systems.
        /// </summary>
        public static List<ConnectionData> ExtractConnections(BaseEntity entity)
        {
            var connections = new List<ConnectionData>();
            foreach (Element connElem in entity.ConnectionsData)
            {
                if (connElem is DmeConnectionData conn)
                {
                    connections.Add(new ConnectionData
                    {
                        outputName = conn.OutputName ?? "",
                        targetName = conn.TargetName ?? "",
                        inputName = conn.InputName ?? "",
                        overrideParam = conn.OverrideParam ?? "",
                        delay = conn.Delay,
                        timesToFire = conn.TimesToFire
                    });
                }
            }
            return connections;
        }
    }

    // ======================================================================
    //  SUPPORTING TYPES
    // ======================================================================

    /// <summary>
    /// Parsed representation of a single Hammer I/O connection (DmeConnectionData).
    ///
    /// In Hammer, level designers wire entity outputs to other entity inputs visually:
    ///   e.g. trigger_zone.OnPlayerEnter → score_display.Enable (delay 0.5)
    ///
    /// Used during pass 2 (ResolveReferences) to look up target GameObjects by name
    /// and wire up runtime event systems.
    /// </summary>
    public struct ConnectionData
    {
        /// <summary> The output signal this entity fires (e.g. "OnPlayerEnter", "OnLaunched"). </summary>
        public string outputName;

        /// <summary> The targetname of the receiving entity (looked up via ctx.FindByTargetname). </summary>
        public string targetName;

        /// <summary> The input/function to call on the target (e.g. "Enable", "Open", "SetSpeed"). </summary>
        public string inputName;

        /// <summary> Optional parameter passed to the input function (e.g. "2.5" for SetSpeed). </summary>
        public string overrideParam;

        /// <summary> Seconds to wait after the output fires before calling the input. </summary>
        public float delay;

        /// <summary> How many times this connection fires. -1 = infinite, 1 = once, etc. </summary>
        public int timesToFire;
    }

    /// <summary>
    /// Extension methods for entity property dictionaries.
    ///
    /// All vmap entity properties are stored as strings (e.g. "launch_height" "string" "256").
    /// These parse the string values into typed C# values with safe fallback defaults.
    ///
    /// Usage in handlers:
    /// <code>
    ///   float height = properties.GetFloat("launch_height", 256f);
    ///   bool enabled = properties.GetBool("start_enabled", true);
    ///   Color color = properties.GetColor("color", Color.white);
    /// </code>
    /// </summary>
    public static class EntityPropertyExtensions
    {
        public static float GetFloat(this Dictionary<string, string> props, string key, float defaultValue = 0f)
        {
            if (!props.TryGetValue(key, out var value)) return defaultValue;
            return float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float result) ? result : defaultValue;
        }

        public static int GetInt(this Dictionary<string, string> props, string key, int defaultValue = 0)
        {
            if (!props.TryGetValue(key, out var value)) return defaultValue;
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        public static bool GetBool(this Dictionary<string, string> props, string key, bool defaultValue = false)
        {
            if (!props.TryGetValue(key, out var value)) return defaultValue;
            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses a space-separated color string like "255 255 255" or "255 255 255 255"
        /// into a Unity Color (normalized 0-1 range). Supports RGB and RGBA formats.
        /// </summary>
        public static UnityEngine.Color GetColor(this Dictionary<string, string> props, string key, UnityEngine.Color defaultValue = default)
        {
            if (!props.TryGetValue(key, out var value) || string.IsNullOrEmpty(value)) return defaultValue;
            var parts = value.Split(' ');
            if (parts.Length < 3) return defaultValue;

            float.TryParse(parts[0], out float r);
            float.TryParse(parts[1], out float g);
            float.TryParse(parts[2], out float b);
            float a = 255f;
            if (parts.Length >= 4) float.TryParse(parts[3], out a);

            return new UnityEngine.Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }
    }
}
