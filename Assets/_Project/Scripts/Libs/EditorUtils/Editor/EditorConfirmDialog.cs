using System;
using UnityEditor;
using UnityEngine;

namespace FS.Editor
{
    /// <summary>
    /// Generic modal confirmation popup window
    /// </summary>
    public class EditorConfirmDialog : EditorWindow
    {
        public delegate void OnConfirmationSelectedDelegate(bool result);
        private OnConfirmationSelectedDelegate m_callback;
        
        private bool bFirstPass = true;
        private const string k_textFieldName = "EntryField";
        
        private string m_toolTip = null;

        private bool bConfirmationResult = false;

        public static void Show(string windowName, OnConfirmationSelectedDelegate callback, string tooltip = null)
        {
            EditorConfirmDialog dialog = CreateInstance<EditorConfirmDialog>();
            dialog.titleContent = new GUIContent(windowName);

            dialog.m_toolTip = tooltip ?? "Are you sure you want to do this?";
            
            dialog.maxSize = new Vector2(320, 120);
            dialog.minSize = new Vector2(320, 120);
            
            dialog.m_callback = callback;

            // For layout reasons we need this in a delay call (otherwise mismatch begin/end from parent window)
            EditorApplication.delayCall += dialog.ShowModal;
        }
        
        public static bool Show(string windowName, string tooltip = null)
        {
            EditorConfirmDialog dialog = CreateInstance<EditorConfirmDialog>();
            dialog.titleContent = new GUIContent(windowName);

            dialog.m_toolTip = tooltip ?? "Are you sure you want to do this?";

            dialog.maxSize = new Vector2(320, 120);
            dialog.minSize = new Vector2(320, 120);
            
            dialog.ShowModal();
            
            return dialog.bConfirmationResult;
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

                GUILayout.Label(m_toolTip, GUIStyles.GUIStyles.HelpBox, GUILayout.Width(310));


                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.HorizontalScope(GUIStyles.GUIStyles.HelpBox))
                {
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("Ok", GUILayout.Width(100)))
                    {
                        Accepted();
                    }

                    GUI.enabled = true;

                    if (GUILayout.Button("Cancel", GUILayout.Width(100)))
                    {
                        Cancel();
                    }
                    
                    GUILayout.FlexibleSpace();

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


        private bool bCloseRequested = false;
        private void Accepted()
        {
            bConfirmationResult = true;
            bCloseRequested = true;
            
            m_callback?.Invoke(bConfirmationResult);
        }

        private void Cancel()
        {
            bConfirmationResult = false;
            bCloseRequested = true;
            
            m_callback?.Invoke(bConfirmationResult);
        }
    }
}