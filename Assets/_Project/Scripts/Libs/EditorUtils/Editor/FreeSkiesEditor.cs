using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FS.Editor
{
    public class DirtyIfChangedScope : IDisposable
    {
        private Object m_object;
        public DirtyIfChangedScope(Object unityObject, bool recordUndo = true)
        {
            m_object = unityObject;
            if (recordUndo && m_object)
                Undo.RecordObject(m_object, "Modify " + m_object.name);
            EditorGUI.BeginChangeCheck();
        }
        public void Dispose()
        {
            if (EditorGUI.EndChangeCheck() && m_object)
            {
                EditorUtility.SetDirty(m_object);
            }
        }
    }
    
    public static class FreeSkiesEditor
    {
        public static readonly Texture2D Logo = EditorGUIUtility.Load("Free Skies/logo.png") as Texture2D;

        public static readonly Color ToggleOnColor = new Color(0.4f, 0.8f, 0.4f, 0.5f);
        public static readonly Color ToggleOffColor = new Color(0.8f, 0.4f, 0.4f, 0.5f);
        
        public static Rect DrawLogo(float width = 200, GUIStyle style = null, Color? backgroundColor = null)
        {
            //GUILayout.FlexibleSpace();  // Add flexible space above
            style ??= GUIStyle.none;
            backgroundColor ??= new Color(0.3f, 0.1f, 0.3f, 0.5f);

            // Start a horizontal group to handle horizontal centering
            var logoRect = EditorGUILayout.BeginHorizontal(style);
            
            EditorGUI.DrawRect(logoRect, backgroundColor.Value);
            
            GUILayout.FlexibleSpace();  // Add flexible space to the left

            // Draw the texture with a fixed size while maintaining aspect ratio
            float height = width * (Logo.height / (float)Logo.width);
            GUILayout.Label(Logo, GUILayout.Width(width), GUILayout.Height(height));

            GUILayout.FlexibleSpace();  // Add flexible space to the right
            EditorGUILayout.EndHorizontal();

            //GUILayout.FlexibleSpace();  // Add flexible space below
            //GUILayout.FlexibleSpace();  // Add flexible space below
            return logoRect;
        }

        /// <summary>
        /// Iterates an OdinMenuTree of a given object type and force sets the selected menu item
        /// </summary>
        public static void SetMenuTreeSelection<T>(OdinMenuTree menuTree, T value)
        {
            // Select this action in the menu tree
            menuTree.EnumerateTree((menuItem) =>
            {
                if (menuItem.Value is not T item) return;
                if (item.Equals(value))
                {
                    menuItem.Select();
                }
            });
        }

        public static void DropDown<T>(string label, T currentSelection, T[] options,
            Action<IEnumerable<T>> onSelectionConfirmed, GUIStyle style = null)
        {
            GenericSelector<T> selector = new GenericSelector<T>(options);
            selector.SelectionConfirmed += onSelectionConfirmed;
            selector.SetSelection(currentSelection);

            OdinSelector<T>.DrawSelectorDropdown(new GUIContent(), label, (rect) =>
            {
                selector.ShowInPopup(rect);
                return selector;
            }, style);
        }
        
        public static void DropDown<T>(string label, T currentSelection, Func<T[]> options,
            Action<IEnumerable<T>> onSelectionConfirmed, GUIStyle style = null)
        {
            OdinSelector<T>.DrawSelectorDropdown(new GUIContent(), label, (rect) =>
            {
                GenericSelector<T> selector = new GenericSelector<T>(options());
                selector.SelectionConfirmed += onSelectionConfirmed;
                selector.SetSelection(currentSelection);
                selector.EnableSingleClickToSelect();
                selector.ShowInPopup(rect);
                return selector;
            }, style);
        }

        public static void ToggleLabel(string label, bool value, GUIStyle style = null)
        {
            GUI.backgroundColor = value ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.8f, 0.4f, 0.4f);
            GUILayout.Button(label, style ?? GUIStyles.GUIStyles.ButtonMid);
            GUI.backgroundColor = GUI.color;
        }
        
        public static bool ToggleButton(string label, ref bool value, GUIStyle style = null, params GUILayoutOption[] options)
        {
            GUI.backgroundColor = value ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.8f, 0.4f, 0.4f);
            var buttonVal = GUILayout.Button(label, style ?? GUIStyles.GUIStyles.ButtonMid, options);
            if (buttonVal) value = !value;
            GUI.backgroundColor = GUI.color;
            return buttonVal;
        }
    }
}