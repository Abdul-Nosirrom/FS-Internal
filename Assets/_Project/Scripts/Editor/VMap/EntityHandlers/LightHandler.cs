using System;
using System.Collections.Generic;
using Datamodel.Vmap;
using UnityEngine;

namespace FS.VmapImport
{
    /// <summary>
    /// Converts Source 2 light entities to Unity Light components.
    ///
    /// Supported types:
    ///   - light_omni2  → Unity Point Light (position + color + range + intensity)
    ///   - light_environment → Unity Directional Light (sun direction from entity angles,
    ///     sky color for ambient)
    ///
    /// Property mapping from Source 2 to Unity:
    ///   color (255 255 255)     → Light.color (normalized)
    ///   range (256.0)           → Light.range (Hammer units → meters via scale)
    ///   brightness_lumens (1000)→ Light.intensity (approximate conversion)
    ///   castshadows (0/1/2)     → Light.shadows
    ///   angles (QAngle)         → sun direction (for directional lights)
    /// </summary>
    public class LightHandler : INodeHandler
    {
        public string[] SupportedClassnames => new[] { "light_omni2", "light_environment" };
        public Type[] SupportedTypes => Array.Empty<Type>();

        public GameObject Process(
            MapNode node,
            Dictionary<string, string> properties,
            List<CMapMesh> childMeshes,
            VmapImportContext ctx)
        {
            string classname = properties.GetValueOrDefault("classname", "");
            var go = new GameObject(ctx.GetEntityDisplayName(node as BaseEntity, properties));

            switch (classname)
            {
                case "light_omni2":
                    SetupPointLight(go, properties, ctx);
                    break;
                case "light_environment":
                    SetupDirectionalLight(go, properties, ctx);
                    break;
            }

            return go;
        }

        public void ResolveReferences(VmapImportContext ctx) { }

        /// <summary>
        /// Sets up a Unity Point Light from light_omni2 properties.
        /// </summary>
        private void SetupPointLight(GameObject go, Dictionary<string, string> properties, VmapImportContext ctx)
        {
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;

            // Color - "255 255 255" space-separated RGB
            light.color = properties.GetColor("color", Color.white);

            // Range - Hammer units, convert to meters
            float range = properties.GetFloat("range", 256f);
            light.range = range * ctx.Settings.m_scale;

            // Intensity - Source 2 uses multiple brightness systems (candelas, nits, lumens).
            // We use lumens as the primary value and do a rough conversion to Unity intensity.
            // Unity's default pipeline uses a different light falloff model, so this is approximate.
            float lumens = properties.GetFloat("brightness_lumens", 1000f);
            light.intensity = lumens / 100f; // Rough mapping - tune as needed

            // Shadows - Source 2: 0=none, 1=baked, 2=dynamic
            int castShadows = properties.GetInt("castshadows", 2);
            light.shadows = castShadows >= 2 ? LightShadows.Soft
                          : castShadows == 1 ? LightShadows.Hard
                          : LightShadows.None;

            // Enabled
            light.enabled = properties.GetBool("enabled", true);
        }

        /// <summary>
        /// Sets up a Unity Directional Light from light_environment properties.
        /// The entity's rotation (angles) defines the sun direction.
        /// </summary>
        private void SetupDirectionalLight(GameObject go, Dictionary<string, string> properties, VmapImportContext ctx)
        {
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;

            // Sun color
            light.color = properties.GetColor("color", Color.white);

            // Sun brightness
            float brightness = properties.GetFloat("brightness", 1f);
            light.intensity = brightness;

            // Shadows
            int castShadows = properties.GetInt("castshadows", 1);
            light.shadows = castShadows >= 1 ? LightShadows.Soft : LightShadows.None;

            light.enabled = properties.GetBool("enabled", true);

            // Note: the entity's origin/angles (applied by the processor after this returns)
            // naturally orient the directional light to match Hammer's sun direction.
        }
    }
}
