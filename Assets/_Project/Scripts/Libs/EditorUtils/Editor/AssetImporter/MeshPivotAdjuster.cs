using System;
using FS.Editor.Internals;
using UnityEditor;
using UnityEngine;

namespace FS.Editor
{
    public class MeshPivotAdjusterAssetPostProcessor : AssetPostprocessor
    {
        private void OnPostprocessModel(GameObject g)
        {
            if (!ModelImporterUserData.HasSettings<MeshPivotAdjusterEditorHook.MeshPivotData>(assetImporter))
                return;
            
            var meshFilter = g.GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = g.GetComponentInChildren<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) return;
            
            var modelImporter = (ModelImporter) assetImporter;
            if (modelImporter == null) return;
            var settings = ModelImporterUserData.GetSettings<MeshPivotAdjusterEditorHook.MeshPivotData>(modelImporter);

            foreach (var mesh in g.GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.sharedMesh == null) continue;
                ApplyPivotAdjustment(mesh.sharedMesh, settings);
            }
        }
        
        private void ApplyPivotAdjustment(Mesh mesh, MeshPivotAdjusterEditorHook.MeshPivotData settings)
        {
            if (settings == null) return;
            
            var positionOffset = settings.positionOffset;
            var rotationOffset = settings.rotationOffset;
            Debug.LogError($"Applying Position Offset: {positionOffset} | Rotation Offset: {rotationOffset}");
            var rotationQuat = Quaternion.Euler(rotationOffset);
            var TRS = Matrix4x4.TRS(positionOffset, rotationQuat, Vector3.one);
            
            // Transform vertices
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = TRS.MultiplyPoint3x4(vertices[i]);
            }
            mesh.vertices = vertices;
            
            // Rotate normals
            if (mesh.normals.Length > 0)
            {
                var normals = mesh.normals;
                for (int i = 0; i < normals.Length; i++)
                {
                    normals[i] = rotationQuat * normals[i];
                }
                mesh.normals = normals;
            }
            
            // Rotate tangents
            if (mesh.tangents.Length > 0)
            {
                var tangents = mesh.tangents;
                for (int i = 0; i < tangents.Length; i++)
                {
                    Vector3 tangentVec = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                    tangentVec = rotationQuat * tangentVec;
                    tangents[i] = new Vector4(tangentVec.x, tangentVec.y, tangentVec.z, tangents[i].w);
                }
                mesh.tangents = tangents;
            }
            
            mesh.RecalculateBounds();
        }
    }

    public class MeshPivotAdjusterEditorHook : AssetImporterEditorHook
    {
        public override string Name => "Mesh Pivot Adjuster";
        
        private Vector3 m_setPivotOffset;
        private Vector3 m_setRotationOffset;
        
        [Serializable]
        public class MeshPivotData
        {
            public Vector3 positionOffset;
            public Vector3 rotationOffset;
        }

        public override void OnEnable()
        {
            var settings = ModelImporterUserData.GetSettings<MeshPivotData>(m_importer);
            m_setPivotOffset = settings.positionOffset;
            m_setRotationOffset = settings.rotationOffset;
        }

        public override void OnGUI()
        {
            GUILayout.Space(10f);
            GUILayout.Label("Mesh Pivot Adjuster", EditorStyles.whiteBoldLabel);
            
            m_setPivotOffset = EditorGUILayout.Vector3Field("Position Offset", m_setPivotOffset);
            m_setRotationOffset = EditorGUILayout.Vector3Field("Rotation Offset", m_setRotationOffset);
        }

        public override bool HasModified()
        {
            var settings = ModelImporterUserData.GetSettings<MeshPivotData>(m_importer);
            bool hasChanged = m_setPivotOffset != settings.positionOffset || m_setRotationOffset != settings.rotationOffset;
            return hasChanged;
        }

        public override void ApplySettings()
        {
            var settings = new MeshPivotData
            {
                positionOffset = m_setPivotOffset,
                rotationOffset = m_setRotationOffset
            };
            ModelImporterUserData.SetSettings(m_importer, settings);
        }
    }
}