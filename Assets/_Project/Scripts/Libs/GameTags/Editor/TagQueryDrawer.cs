using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.TagSystem.Editor
{
    /// <summary>
    /// Odin property drawer for <see cref="TagQuery"/> fields.
    /// Displays three collapsible sections for Require All, Require Any, and Exclude conditions,
    /// each using the tag drawer for individual tag selection.
    /// </summary>
    public class TagQueryDrawer : OdinValueDrawer<TagQuery>
    {
        private static readonly Color k_requireAllColor = new(0.2f, 0.5f, 0.2f, 0.15f);
        private static readonly Color k_requireAnyColor = new(0.2f, 0.35f, 0.6f, 0.15f);
        private static readonly Color k_excludeColor = new(0.6f, 0.2f, 0.2f, 0.15f);

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (label != null)
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            }

            SirenixEditorGUI.BeginBox();
            {
                DrawSection("Require All", "All of these tags must be present",
                    Property.Children["m_requireAll"], k_requireAllColor);

                SirenixEditorGUI.HorizontalLineSeparator(1);

                DrawSection("Require Any", "At least one of these tags must be present",
                    Property.Children["m_requireAny"], k_requireAnyColor);

                SirenixEditorGUI.HorizontalLineSeparator(1);

                DrawSection("Exclude", "None of these tags can be present",
                    Property.Children["m_exclude"], k_excludeColor);
            }
            SirenixEditorGUI.EndBox();
        }

        private void DrawSection(string title, string tooltip, InspectorProperty listProperty, Color bgColor)
        {
            var rect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(rect, bgColor);
            
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField(new GUIContent(title, tooltip), SirenixGUIStyles.BoldLabel);
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();

            // Draw the list — Odin will use the TagDrawer for each Tag element
            listProperty.Draw(null);

            EditorGUILayout.EndVertical();
        }
    }
}
