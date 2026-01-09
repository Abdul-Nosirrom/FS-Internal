using System;
using FS.MeshProcessing;
using UnityEditor;
using UnityEngine;

namespace FS.Editor
{
    public class HammerMeshScaleAdjuster : AssetPostprocessor
    {
        public override int GetPostprocessOrder() => Int32.MinValue;

        private float m_inputScaleFactor;
        private void OnPreprocessModel()
        {
            if (!assetPath.Contains("hmr")) return; // Hammer file processing only
            var modelImporter = (ModelImporter)assetImporter;
            if (modelImporter == null) return;
            
            m_inputScaleFactor = modelImporter.globalScale;
            modelImporter.globalScale *= 1 / 64f; // 1 Hammer 64x64 grid == 1x1 m unity
        }

        private void OnPostprocessModel(GameObject g)
        {
            var modelImporter = (ModelImporter)assetImporter;
            if (modelImporter != null && assetPath.Contains("hmr"))
                modelImporter.globalScale = m_inputScaleFactor; // reset it
        }
    }
    /// <summary>
    /// Simple helper to post process skate element imports.
    /// </summary>
    public class SkateElementImportProcessor : AssetPostprocessor
    {
        public override int GetPostprocessOrder() => Int32.MaxValue;
        
        private void OnPreprocessModel()
        {
            var modelImporter = (ModelImporter)assetImporter;
            
            if (modelImporter == null) return;
            
            if (!assetPath.Contains("hmr")) return; // Hammer file processing only
            
            // Set appropraite scale for hammer
            //modelImporter.globalScale = 1/64f; // 1 Hammer 64x64 grid == 1x1 m unity TODO: Check perservetopologyimporter, it applies it internally
            modelImporter.useFileScale = false; // This is the 'conver units' option
            modelImporter.addCollider = true; // Add a mesh collider by default
            
            // Disable unnecessary import options
            modelImporter.importBlendShapes = false;
            modelImporter.importVisibility = false;
            modelImporter.importCameras = false;
            modelImporter.importLights = false;
        }

        private void OnPostprocessModel(GameObject g)
        {
            foreach (var child in g.transform)
            {
                var childGO = ((Transform)child).gameObject;
                
                var name = childGO.name.ToLower();

                childGO.isStatic = !name.Contains("dynamic");
                
                // Remove collision if specified in name
                if (name.Contains("nocol") && childGO.TryGetComponent<MeshCollider>(out var collider))
                    UnityEngine.Object.DestroyImmediate(collider);
                
                var topologyPreserver = childGO.GetComponent<MeshTopologyPreserver>();
                if (topologyPreserver)
                {
                    var edgeSelector = childGO.GetComponent<MeshEdgeSelector>();
                    if (edgeSelector == null)
                        edgeSelector = childGO.AddComponent<MeshEdgeSelector>();
                }

                if (name.Contains("vert")) childGO.layer = PhysicsLayers.Vert;
                if (name.Contains("rail") || name.Contains("grind")) childGO.layer = PhysicsLayers.RailGrind;
                if (name.Contains("wallslide")) childGO.layer = PhysicsLayers.WallSlide;
            }
        }
    }
}