using System.Linq;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.TagSystem.Editor
{
    /// <summary>
    /// Odin property drawer for <see cref="TagSet"/> fields.
    /// Displays existing tags as removable pills/chips and provides an "Add Tag" button
    /// that opens the hierarchical tag selector, excluding already-added tags.
    /// </summary>
    public class TagSetDrawer : OdinValueDrawer<TagSet>
    {
        private static readonly Color k_tagPillColor = new(0.25f, 0.25f, 0.25f, 0.6f);
        private static readonly Color k_tagPillColorHover = new(0.35f, 0.35f, 0.35f, 0.6f);
        private static readonly Color k_addButtonColor = new(0.2f, 0.5f, 0.2f, 0.4f);
        
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var set = ValueEntry.SmartValue;
            
            // Header with label and count
            SirenixEditorGUI.BeginBox();
            {
                EditorGUILayout.BeginHorizontal();
                {
                    if (label != null)
                    {
                        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                    }
                    
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"{set.Count} tag(s)", SirenixGUIStyles.RightAlignedGreyMiniLabel, GUILayout.Width(60));
                }
                EditorGUILayout.EndHorizontal();

                // Draw existing tags as removable pills
                if (set.Count > 0)
                {
                    DrawTagPills(set);
                }

                // Add button
                DrawAddButton(set);
            }
            SirenixEditorGUI.EndBox();
        }

        private void DrawTagPills(TagSet set)
        {
            int indexToRemove = -1;
            
            // Flow layout — wrap tags horizontally
            EditorGUILayout.BeginHorizontal();
            float currentLineWidth = 0f;
            float maxWidth = EditorGUIUtility.currentViewWidth - 60f;

            for (int i = 0; i < set.Count; i++)
            {
                var tag = set.Tags[i];
                string displayText = tag.Value;
                
                // Calculate pill width
                var content = new GUIContent($"  {displayText}  ×");
                float pillWidth = EditorStyles.miniButton.CalcSize(content).x + 4f;
                
                // Wrap to next line if needed
                if (currentLineWidth + pillWidth > maxWidth && currentLineWidth > 0f)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    currentLineWidth = 0f;
                }

                // Draw the pill
                var ogColor = GUI.backgroundColor;
                GUI.backgroundColor = k_tagPillColor;
                
                if (GUILayout.Button(content, EditorStyles.miniButton, GUILayout.Height(20f)))
                {
                    indexToRemove = i;
                }
                
                GUI.backgroundColor = ogColor;
                currentLineWidth += pillWidth;
            }
            
            EditorGUILayout.EndHorizontal();

            // Process removal after iteration
            if (indexToRemove >= 0)
            {
                ValueEntry.Property.RecordForUndo("Remove Tag");
                set.Remove(set.Tags[indexToRemove]);
                ValueEntry.SmartValue = set;
            }
        }

        private void DrawAddButton(TagSet set)
        {
            var ogColor = GUI.backgroundColor;
            GUI.backgroundColor = k_addButtonColor;
            
            var rect = EditorGUILayout.GetControlRect(GUILayout.Height(20f));
            
            if (GUI.Button(rect, "+ Add Tag", EditorStyles.miniButton))
            {
                // Exclude tags already in the set
                var existing = set.Tags.Select(t => t.Value);
                var filtered = ValueEntry.Property.GetAttribute<TagFilterAttribute>()?.FilterTags;
                var selector = new TagSelector(currentSelections: existing, filterOptions: filtered, displayMultiToggle: true);
                
                selector.OnTagSelected += selection =>
                {
                    ValueEntry.Property.RecordForUndo("Add Tag");
                    set.Add(new Tag(selection));
                    ValueEntry.SmartValue = set;
                };
                
                selector.OnTagDeselected += selection =>
                {
                    ValueEntry.Property.RecordForUndo("Remove Tag");
                    set.Remove(new Tag(selection));
                    ValueEntry.SmartValue = set;
                };

                selector.ShowInPopup(rect, new Vector2(rect.width, 300));
            }
            
            GUI.backgroundColor = ogColor;
        }
    }
}
