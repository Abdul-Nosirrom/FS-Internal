using System;
using System.Collections.Generic;
using System.Linq;
using FS.Animation;
using FS.Editor;
using FS.Editor.Timeline;
using Rewired.Data.Mapping;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using FS.Player;
using TMPro;
using UnityEngine.Profiling;
using UnityEngine.TextCore.Text;

namespace FS.UI.Editor
{
    [CustomEditor(typeof(ControllerGlyphs))]
    public class ControllerGlyphsEditor : OdinEditor
    {
        private ControllerGlyphs m_target;

        private SerializedProperty m_tmpSpriteAsset;
        private SerializedProperty m_controllerType;
        private SerializedProperty m_glyphArray;
        private SerializedProperty m_hardwareFallback;
        private SerializedProperty m_hardwareAssignments;

        private Vector2 m_glyphScrollPos;

        private bool m_showOnlyAssigned = true;
        private bool m_hardwareAssignmentsFoldout = true;

        //private static HardwareJoystickMap[] s_hardwareIdentifiers;
        private static string[] s_hardwareIdentifierNames;
        
        protected override void OnEnable()
        {
            base.OnEnable();

            m_tmpSpriteAsset = serializedObject.FindProperty("TextMeshSpriteAsset");
            m_controllerType = serializedObject.FindProperty("ControllerType");
            m_glyphArray = serializedObject.FindProperty("GlyphKeys");
            m_hardwareFallback = serializedObject.FindProperty("IsJoystickFallback");
            m_hardwareAssignments = serializedObject.FindProperty("m_hardwareSupported");
            
            if (s_hardwareIdentifierNames == null)
                InitializeHardwareIdentifiers();

            EditorApplication.update += Repaint;
        }
        
        protected override void OnDisable()
        {
            base.OnDisable();
            EditorApplication.update -= Repaint;
        }
        
        private static void InitializeHardwareIdentifiers()
        {
            // Get all assets of type HardwareJoystickMap
            //var joystickMaps = new List<HardwareJoystickMap>();
            var joystickNames = new List<string>();
            var guids = AssetDatabase.FindAssets("t:HardwareJoystickMap");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<HardwareJoystickMap>(path);
                if (asset != null)
                {
                    //joystickMaps.Add(asset);
                    joystickNames.Add(asset.ControllerName);
                }
            }

            //s_hardwareIdentifiers = joystickMaps.ToArray();
            s_hardwareIdentifierNames = joystickNames.ToArray();
        }

        public override void OnInspectorGUI()
        {
            FS.Editor.FreeSkiesEditor.ToggleButton("Show Only Assigned", ref m_showOnlyAssigned);
            // Enum to change controller type, give a confirm dialog if changing as it can cause data loss
            serializedObject.Update();

            EditorGUILayout.Space();
            if ((ControllerType)m_controllerType.enumValueIndex == ControllerType.Gamepad) 
                HardwareAssignmentGUI();
            EditorGUILayout.Space();

            var newControllerType = (ControllerType)EditorGUILayout.EnumPopup("Controller Type", (ControllerType)m_controllerType.enumValueIndex);
            if (newControllerType != (ControllerType)m_controllerType.enumValueIndex)
            {
                if (EditorUtility.DisplayDialog("Change Controller Type", "Changing the controller type will clear the current glyph mappings. Are you sure you want to continue?", "Yes", "No"))
                {
                    m_controllerType.enumValueIndex = (int)newControllerType;
                    m_glyphArray.ClearArray();
                    serializedObject.ApplyModifiedProperties();
                }
            }

            EditorGUI.BeginChangeCheck();
            m_glyphScrollPos = EditorGUILayout.BeginScrollView(m_glyphScrollPos, GUIStyles.GUIStyles.HelpBox);
            DrawGlyphList();
            EditorGUILayout.EndScrollView();
            if (EditorGUI.EndChangeCheck()) serializedObject.ApplyModifiedProperties();
        }
        
        private void HardwareAssignmentGUI()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginVertical(GUIStyles.GUIStyles.HelpBox);

            EditorGUI.indentLevel++;
            m_hardwareAssignmentsFoldout = SirenixEditorGUI.Foldout(m_hardwareAssignmentsFoldout, "Hardware Assignments");
            if (m_hardwareAssignmentsFoldout)
            {
                var toggleVal = m_hardwareFallback.boolValue;
                FreeSkiesEditor.ToggleButton("Hardware Fallback", ref toggleVal);
                m_hardwareFallback.boolValue = toggleVal;

                for (int h = 0; h < m_hardwareAssignments.arraySize; h++)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    // Dropdown of all hardware identifiers
                    var element = m_hardwareAssignments.GetArrayElementAtIndex(h);
                    FreeSkiesEditor.DropDown(element.stringValue, element.stringValue, s_hardwareIdentifierNames, selection =>
                    {
                        var hwd = selection.FirstOrDefault();
                        if (hwd != null)
                        {
                            element.stringValue = hwd;
                            serializedObject.ApplyModifiedProperties();
                        }
                    });
                    
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        m_hardwareAssignments.DeleteArrayElementAtIndex(h);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Add Hardware Identifier", GUILayout.ExpandWidth(true)))
                {
                    m_hardwareAssignments.arraySize++;
                }
            }

            EditorGUI.indentLevel--;
            
            EditorGUILayout.EndVertical();
            if (EditorGUI.EndChangeCheck()) serializedObject.ApplyModifiedProperties();
        }

        private void DrawGlyphList()
        {
            // Text mesh pro sprite asset dropdown field
            m_tmpSpriteAsset.objectReferenceValue = (TMP_SpriteAsset)SirenixEditorFields.UnityObjectField(m_tmpSpriteAsset.objectReferenceValue, typeof(TMP_SpriteAsset), false);
            
            int zebraStriping = 0;
            int numElems = m_glyphArray.arraySize;
            for (int i = 0; i < numElems; i++)
            {
                float gScale = 0.1f;
                var color1 = new Color(gScale, gScale, gScale, 0.2f);
                var color2 = new Color(gScale, gScale, gScale, 0.4f);
                var zebraColor = zebraStriping % 2 == 0 ? color1 : color2;
                zebraStriping++;
                
                
                var element = m_glyphArray.GetArrayElementAtIndex(i);
                var keyID = element.FindPropertyRelative("m_physicalInputID");
                var glyphObj = element.FindPropertyRelative("m_glyph");
                
                if (m_showOnlyAssigned && glyphObj.objectReferenceValue == null) continue;
                
                var backgroundRect = EditorGUILayout.BeginHorizontal(GUI.skin.box);
                EditorGUI.DrawRect(backgroundRect, zebraColor);

                glyphObj.objectReferenceValue = SirenixEditorFields.UnityPreviewObjectField(keyID.stringValue, glyphObj.objectReferenceValue,
                    typeof(Sprite), false, 45f);

                EditorGUILayout.EndHorizontal();
            }
        }
    }
}