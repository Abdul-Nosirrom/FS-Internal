using System;
using System.Collections.Generic;
using Datamodel;
using Datamodel.Vmap;
using FluffyUnderware.Curvy;
using UnityEngine;

namespace FS.VmapImport
{
    /// <summary>
    /// Handles <see cref="CMapCable"/> nodes - native Hammer curves composed of
    /// <see cref="CMapPathNode"/> children.
    ///
    /// Dispatched by type (not classname) because CMapCable is a distinct binary type
    /// in the vmap format, structurally different from CMapEntity.
    ///
    /// CMapCable does NOT contain mesh data. The tube you see in Hammer is procedurally
    /// generated at display time from the spline + rendering parameters (radius, numSides,
    /// materialName, tessellationSpacing). For Unity, we import the spline as a CurvySpline
    /// and let Curvy's mesh generation handle tube rendering if needed.
    /// </summary>
    public class CurveHandler : INodeHandler
    {
        public string[] SupportedClassnames => Array.Empty<string>();
        public Type[] SupportedTypes => new[] { typeof(CMapCable) };

        public GameObject Process(
            MapNode node,
            Dictionary<string, string> properties,
            List<CMapMesh> childMeshes,
            VmapImportContext ctx)
        {
            if (!ctx.Settings.m_importCurves) return null;

            // CMapCable is a typed BaseEntity subclass - downcast for typed property access
            if (node is not CMapCable cable)
            {
                Debug.LogWarning("[VmapImporter] CurveHandler received non-CMapCable node, skipping.");
                return null;
            }

            // Collect control point positions from CMapPathNode children (ordered)
            var worldPositions = new List<Vector3>();
            foreach (Element child in cable.Children)
            {
                if (child is CMapPathNode pathNode)
                    worldPositions.Add(ctx.ConvertPosition(pathNode.Origin));
            }

            if (worldPositions.Count < 2)
            {
                Debug.LogWarning("[VmapImporter] CMapCable with fewer than 2 path nodes, skipping.");
                return null;
            }

            // Create CurvySpline
            var spline = CurvySpline.Create();
            spline.gameObject.name = ctx.GetEntityDisplayName(cable as BaseEntity, properties);

            // ClosedLoop: whether the spline forms a closed loop
            spline.Closed = cable.ClosedLoop;

            // InterpolationType: 0 = linear, 1 = Catmull-Rom (smooth), 2 = Bezier
            spline.Interpolation = cable.InterpolationType switch
            {
                0 => CurvyInterpolation.Linear,
                2 => CurvyInterpolation.Bezier,
                _ => CurvyInterpolation.CatmullRom
            };

            spline.Add(worldPositions.ToArray());
            spline.Refresh();

            return spline.gameObject;
        }

        public void ResolveReferences(VmapImportContext ctx) { }
    }
}
