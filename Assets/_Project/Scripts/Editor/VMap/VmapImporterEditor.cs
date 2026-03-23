using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace FS.VmapImport.Editor
{
    /// <summary>
    /// Custom inspector for the VmapImporter ScriptedImporter.
    ///
    /// Shows the import settings (scale, colliders, static flags) and the material/prefab
    /// remap tables. Material remaps are auto-populated during import - any new .vmat paths
    /// found in the vmap get added as empty slots that designers can fill in.
    /// </summary>
    [CustomEditor(typeof(VmapImporter))]
    public class VmapImporterEditor : ScriptedImporterEditor
    {
        // Serialized property references for the settings fields
        private SerializedProperty m_settingsProp;

        public override void OnEnable()
        {
            base.OnEnable();
            m_settingsProp = serializedObject.FindProperty("m_settings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (m_settingsProp == null)
            {
                EditorGUILayout.HelpBox("Could not find settings property.", MessageType.Error);
                base.ApplyRevertGUI();
                return;
            }

            // ---- Transform ----
            EditorGUILayout.LabelField("Transform", EditorStyles.boldLabel);
            DrawProperty("m_scale", "Scale Factor");
            EditorGUILayout.Space();

            // ---- Mesh ----
            EditorGUILayout.LabelField("Mesh", EditorStyles.boldLabel);
            DrawProperty("m_addColliders", "Add Colliders");
            DrawProperty("m_preserveTopology", "Preserve Topology");
            DrawProperty("m_markStatic", "Mark Static (BakeLighting)");
            EditorGUILayout.Space();

            // ---- Entities ----
            EditorGUILayout.LabelField("Entities", EditorStyles.boldLabel);
            DrawProperty("m_importEntities", "Import Entities");
            DrawProperty("m_importCurves", "Import Curves");
            EditorGUILayout.Space();

            // ---- Material Remaps ----
            EditorGUILayout.LabelField("Material Remaps", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Material paths are discovered automatically during import. "
                + "Drag Unity Materials into the slots to override the placeholder.",
                MessageType.Info);

            var materialRemaps = m_settingsProp.FindPropertyRelative("m_materialRemaps");
            if (materialRemaps != null)
            {
                for (int i = 0; i < materialRemaps.arraySize; i++)
                {
                    var element = materialRemaps.GetArrayElementAtIndex(i);
                    var sourcePath = element.FindPropertyRelative("sourcePath");
                    var material = element.FindPropertyRelative("material");

                    EditorGUILayout.BeginHorizontal();

                    // Source path (read-only label, truncated to filename for readability)
                    string displayPath = sourcePath.stringValue;
                    if (displayPath.Length > 40)
                        displayPath = "..." + displayPath[^37..];
                    EditorGUILayout.LabelField(displayPath, GUILayout.MinWidth(200));

                    // Material assignment slot
                    EditorGUILayout.PropertyField(material, GUIContent.none, GUILayout.MinWidth(100));

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space();

            // ---- Prefab Remaps ----
            EditorGUILayout.LabelField("Prefab Remaps", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Map entity classnames to prefabs. Each level theme can assign different "
                + "prefabs (e.g. jungle_spring vs desert_spring) for the same entity type.",
                MessageType.Info);

            var prefabRemaps = m_settingsProp.FindPropertyRelative("m_prefabRemaps");
            if (prefabRemaps != null)
                EditorGUILayout.PropertyField(prefabRemaps, new GUIContent("Prefab Remaps"), true);

            serializedObject.ApplyModifiedProperties();
            base.ApplyRevertGUI();
        }

        /// <summary>
        /// Helper to draw a property field from the nested m_settings object.
        /// </summary>
        private void DrawProperty(string relativeName, string label)
        {
            var prop = m_settingsProp.FindPropertyRelative(relativeName);
            if (prop != null)
                EditorGUILayout.PropertyField(prop, new GUIContent(label));
        }
    }
}
