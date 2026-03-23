using System;
using System.Collections.Generic;
using Datamodel;
using Datamodel.Vmap;
using TMPro;
using UnityEngine;
using Color = UnityEngine.Color;

namespace FS.VmapImport
{
    /// <summary>
    /// Converts point_worldtext entities into TextMeshPro world-space text objects.
    ///
    /// Useful for blockout labeling - designers can place text in Hammer to annotate
    /// areas ("BOSS ARENA", "SHORTCUT", "VERT SECTION") and it carries through to Unity.
    ///
    /// Source 2 properties mapped:
    ///   message            → TMP text content
    ///   color (255 255 255 255)  → text color (RGBA)
    ///   font_size (20)     → TMP font size
    ///   world_units_per_pixel (2) → text scale in world space
    ///   justify_horizontal (0=left, 1=center, 2=right)
    ///   justify_vertical   (0=top, 1=center, 2=bottom)
    /// </summary>
    public class WorldTextHandler : INodeHandler
    {
        public string[] SupportedClassnames => new[] { "point_worldtext" };
        public Type[] SupportedTypes => Array.Empty<Type>();

        public GameObject Process(
            MapNode node,
            Dictionary<string, string> properties,
            List<CMapMesh> childMeshes,
            VmapImportContext ctx)
        {
            var go = new GameObject(ctx.GetEntityDisplayName(node as BaseEntity, properties));

            // Add world-space TextMeshPro component
            var tmp = go.AddComponent<TextMeshPro>();
            
            // TMP Rotation offset by 90 degrees in Source rotation units, so offset it here for when its applied later
            node.Angles = new QAngle(node.Angles.Pitch, node.Angles.Yaw, node.Angles.Roll - 90);
            
            // Message text content
            tmp.text = properties.GetValueOrDefault("message", "");

            // Text color (RGBA, 0-255 space-separated)
            tmp.color = properties.GetColor("color", Color.white);

            // Font size - Source 2's font_size is in screen-space pixels.
            // Combined with world_units_per_pixel to determine world-space size.
            float fontSize = properties.GetFloat("font_size", 20f);// * (8f / 20f); TODO: Hardcoded scale factor, TBD
            float worldUnitsPerPixel = properties.GetFloat("world_units_per_pixel", 2f);

            // Scale the TMP font size and transform to approximate the Hammer appearance.
            // world_units_per_pixel * scale converts from Hammer world units to Unity meters.
            tmp.fontSize = fontSize;
            float textScale = worldUnitsPerPixel * ctx.Settings.m_scale;
            go.transform.localScale = new Vector3(textScale, textScale, textScale);

            // Horizontal alignment
            int hJustify = properties.GetInt("justify_horizontal", 1);
            tmp.alignment = hJustify switch
            {
                0 => TextAlignmentOptions.Left,
                2 => TextAlignmentOptions.Right,
                _ => TextAlignmentOptions.Center
            };

            // Vertical alignment
            int vJustify = properties.GetInt("justify_vertical", 0);
            tmp.verticalAlignment = vJustify switch
            {
                1 => VerticalAlignmentOptions.Middle,
                2 => VerticalAlignmentOptions.Bottom,
                _ => VerticalAlignmentOptions.Top
            };

            // Fullbright = no lighting effects on text
            // For blockout labeling, this is almost always what you want.

            return go;
        }

        public void ResolveReferences(VmapImportContext ctx) { }
    }
}
