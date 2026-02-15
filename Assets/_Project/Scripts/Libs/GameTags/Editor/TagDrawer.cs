using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.TagSystem.Editor
{
    /// <summary>
    /// Odin property drawer for <see cref="Tag"/> fields.
    /// Displays the current tag value as a dropdown button that opens a hierarchical tag selector.
    /// </summary>
    public class TagDrawer : OdinValueDrawer<Tag>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var tag = ValueEntry.SmartValue;
            var rect = EditorGUILayout.GetControlRect();

            if (label != null)
            {
                rect = EditorGUI.PrefixLabel(rect, label);
            }

            string displayName = tag.IsValid ? tag.Value : "(None)";

            // Color the button based on validity
            bool bIsValid = !tag.IsValid || TagExists(tag.Value);
            var ogColor = GUI.backgroundColor;
            if (!bIsValid)
            {
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            }

            if (EditorGUI.DropdownButton(rect, new GUIContent(displayName), FocusType.Keyboard))
            {
                ShowSelector(rect);
            }

            GUI.backgroundColor = ogColor;

            // Show warning if the tag isn't found in any definition file
            if (tag.IsValid && !bIsValid)
            {
                SirenixEditorGUI.WarningMessageBox($"Tag \"{tag.Value}\" not found in any .gameplayTags definition file.");
            }
        }

        private void ShowSelector(Rect buttonRect)
        {
            var filter = ValueEntry.Property.GetAttribute<TagFilterAttribute>()?.FilterTags;
            var selector = new TagSelector(filterOptions: filter);
            selector.SetSelection(ValueEntry.SmartValue.Value);

            selector.OnTagSelected += selection =>
            {
                ValueEntry.SmartValue = selection;
            };

            selector.ShowInPopup(buttonRect, new Vector2(buttonRect.width, 300));
        }

        private static bool TagExists(string path)
        {
            return TagTreeProvider.TryGetNode(path, out _);
        }
    }
}
