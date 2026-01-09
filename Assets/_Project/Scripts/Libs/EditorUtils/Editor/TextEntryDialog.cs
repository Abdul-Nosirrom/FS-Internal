using System;
using UnityEditor;
using UnityEngine;

namespace FS.Editor
{
    /// <summary>
    /// Generic text entry modal popup window
    /// </summary>
    public class TextEntryDialog : EditorWindow
    {
        public delegate void OnTextEntryCompleteDelegate(string result);
        private OnTextEntryCompleteDelegate m_callback;
        
        private bool bFirstPass = true;
        private const string k_textFieldName = "EntryField";
        
        private string m_entry = null;
        private string m_entryFieldResult = null;
        private string[] m_invalidEntries = null;

        public static void Show(string windowName, string entry, OnTextEntryCompleteDelegate callback, string[] invalidEntries = null)
        {
            TextEntryDialog dialog = CreateInstance<TextEntryDialog>();
            dialog.titleContent = new GUIContent(windowName);

            dialog.m_invalidEntries = invalidEntries;
            
            dialog.m_entry = entry;
            dialog.m_entryFieldResult = entry;

            dialog.maxSize = new Vector2(320, 120);
            dialog.minSize = new Vector2(320, 120);
            
            dialog.m_callback = callback;

            // For layout reasons we need this in a delay call (otherwise mismatch begin/end from parent window)
            EditorApplication.delayCall += dialog.ShowModal;
        }
        
        public static string Show(string windowName, string entry, string[] invalidEntries = null)
        {
            TextEntryDialog dialog = CreateInstance<TextEntryDialog>();
            dialog.titleContent = new GUIContent(windowName);

            dialog.m_invalidEntries = invalidEntries;
            
            dialog.m_entry = entry;
            dialog.m_entryFieldResult = entry;

            dialog.maxSize = new Vector2(320, 120);
            dialog.minSize = new Vector2(320, 120);
            
            dialog.ShowModal();
            
            return dialog.m_entryFieldResult;
        }
        
        private void OnEnable()
        {
            GUI.FocusControl(k_textFieldName);
        }

        private void OnGUI()
        {
            if (bCloseRequested)
            {
                Close();
                return;
            }
            HandleKeyboardInteraction();
            
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Space(20f);

                GUI.SetNextControlName(k_textFieldName);

                m_entryFieldResult = EditorGUILayout.TextField(m_entryFieldResult, GUILayout.Width(310));

                if (!IsAcceptableEntry())
                    GUILayout.Label("Not acceptable entry", GUIStyles.GUIStyles.ErrorLabel);

                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.HorizontalScope(GUIStyles.GUIStyles.HelpBox))
                {
                    GUILayout.FlexibleSpace();

                    if (!IsAcceptableEntry()) GUI.enabled = false;

                    if (GUILayout.Button("OK", GUILayout.Width(100)))
                    {
                        Accepted();
                    }

                    GUI.enabled = true;

                    if (GUILayout.Button("Cancel", GUILayout.Width(100)))
                    {
                        Cancel();
                    }
                }

                GUILayout.Space(20f);
            }

            if (bFirstPass) GUI.FocusControl(k_textFieldName);
            bFirstPass = false;
        }

        private void HandleKeyboardInteraction()
        {
            if (Event.current == null || !Event.current.isKey) return;

            switch (Event.current.keyCode)
            {
                case KeyCode.KeypadEnter:
                case KeyCode.Return:
                    Accepted();
                    break;
                case KeyCode.Escape:
                    Cancel();
                    break;
            }
        }

        private bool IsAcceptableEntry()
        {
            // Check if its null or empty
            if (string.IsNullOrEmpty(m_entryFieldResult)) return false;
            
            // Check if any of the invalid entries match (compare all in lower case?)
            if (m_invalidEntries == null) return true;
            
            foreach (var invalidEntry in m_invalidEntries)
            {
                if (m_entryFieldResult.ToLower().Equals(invalidEntry.ToLower()))
                    return false;
            }

            return true;
        }

        private bool bCloseRequested = false;
        private void Accepted()
        {
            if (!string.IsNullOrEmpty(m_entryFieldResult))
            {
                m_callback?.Invoke(m_entryFieldResult);
                bCloseRequested = true;
            }
        }

        private void Cancel()
        {
            m_entryFieldResult = m_entry;
            m_callback?.Invoke(m_entryFieldResult);
            bCloseRequested = true;
        }
    }
}