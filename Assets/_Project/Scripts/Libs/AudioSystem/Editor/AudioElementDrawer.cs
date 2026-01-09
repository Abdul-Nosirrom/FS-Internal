using System.Collections.Generic;
using System.Linq;
using FS.Editor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.Audio.Editor
{
    public class AudioElementDrawer : OdinValueDrawer<AudioElement>
    {
        private bool m_parametersFoldout = true;
        private HashSet<int> m_markedForDeletion = new HashSet<int>();
        private List<string> m_missingParameters = new List<string>();
        
        protected override void DrawPropertyLayout(GUIContent label)
        {
            using var verticalBox = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);

            // Need a little margin on the left
            using var marginScope = new EditorGUILayout.HorizontalScope();
            GUILayout.Space(10f);
            
            using var backToVertical = new EditorGUILayout.VerticalScope();
            
            var value = ValueEntry.SmartValue;

            // Default drawer for AudioEvent property
            foreach (var child in Property.Children)
            {
                child.Label = label;
                child.Draw();
                break;
            }
            
            var eventRef = FMODUnity.EventManager.EventFromGUID(value.AudioEvent.Guid);
            if (eventRef == null)
            {
                SirenixEditorGUI.ErrorMessageBox("AudioEvent not found. Please ensure the event is valid and exists in the FMOD project.");
                return;
            }
            foreach (var param in eventRef.LocalParameters)
            {
                // If the parameter is not already in the list, add it to the missing parameters
                if (value.Parameters.Exists(p => param.Name.Equals(p.Name))) continue;
                if (!m_missingParameters.Contains(param.Name))
                {
                    m_missingParameters.Add(param.Name);
                }
            }
            
            // Draw parameter drop-down
            m_parametersFoldout = EditorGUILayout.Foldout(m_parametersFoldout, "Parameters");
            if (m_parametersFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Draw the existing parameters w/ an X button to remove them, then an Add button at the end to add new parameters
                m_markedForDeletion.Clear();
                {
                    for (int pIdx = 0; pIdx < value.Parameters.Count; pIdx++)
                    {
                        // First check if the parameter exists for our given audio event
                        if (eventRef.LocalParameters.All(p => p.Name != value.Parameters[pIdx].Name))
                        {
                            // If it doesn't, mark it for deletion
                            m_markedForDeletion.Add(pIdx);
                            continue;
                        }
                        DrawAudioParameter(pIdx);
                    }
                }
                for (int x = value.Parameters.Count - 1; x >= 0; x--)
                {
                    if (m_markedForDeletion.Contains(x)) value.Parameters.RemoveAt(x);
                }

                EditorGUILayout.Space(10f);
                
                SirenixEditorGUI.HorizontalLineSeparator();
                
                // Add Button
                FreeSkiesEditor.DropDown("Add Parameter", null, m_missingParameters.ToArray(), OnParameterAdded);
                
                EditorGUILayout.EndVertical();
            }
        }

        private void OnParameterAdded(IEnumerable<string> selection)
        {
            var selected = selection.FirstOrDefault();
            if (selected == null) return;
            
            {
                var eventRef = FMODUnity.EventManager.EventFromGUID(ValueEntry.SmartValue.AudioEvent.Guid);
                // get dfault value for the parameter
                var param = eventRef.LocalParameters.FirstOrDefault(p => p.Name.Equals(selected));
                float defaultValue = param == null ? 0 : param.Default;
                
                // Add the parameter to the list
                var newParam = new AudioParameter(selected, defaultValue); // add its default value
                ValueEntry.SmartValue.Parameters.Add(newParam);
                
                // Remove it from the missing parameters list
                //m_missingParameters.Remove(paramName);
            }
        }

        private void DrawAudioParameter(int paramIdx)
        {
            if (paramIdx != 0)
            {
                EditorGUILayout.Space(5f);
                SirenixEditorGUI.HorizontalLineSeparator();
                EditorGUILayout.Space(5f);
            }
            
            AudioParameter val = ValueEntry.SmartValue.Parameters[paramIdx];
            
            GUILayout.BeginHorizontal();
            
            GUILayout.Label(ValueEntry.SmartValue.Parameters[paramIdx].Name);
            GUILayout.FlexibleSpace();
            
            ValueEntry.SmartValue.Parameters[paramIdx] = new AudioParameter(val.Name, EditorGUILayout.FloatField(ValueEntry.SmartValue.Parameters[paramIdx].Value, GUILayout.MaxWidth(200)));

            SirenixEditorGUI.VerticalLineSeparator(2);

            var ogBGColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                m_markedForDeletion.Add(paramIdx);
            }
            GUI.backgroundColor = ogBGColor;
            
            GUILayout.EndHorizontal();
        }
    }
}