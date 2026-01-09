using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEditor.VersionControl;
using UnityEngine;

namespace FS.Editor
{
    public static class VersionControlToolbar
    {
        public static string ProviderName => Provider.GetActivePlugin()?.name ?? "None";
        public static void TryConnect() => Provider.UpdateSettings();
        public static bool IsConnected => Provider.onlineState == OnlineState.Online;
        public static bool IsConnecting => Provider.onlineState == OnlineState.Updating;

        private static GUIContent GetStatusIcon()
        {
            if (IsConnected)
                return EditorGUIUtility.IconContent("P4_CheckOutRemote@2x");
            if (IsConnecting)
                return EditorGUIUtility.IconContent("P4_Updating@2x");
            return EditorGUIUtility.IconContent("P4_DeletedLocal@2x");
        }

        [MainToolbarElement("FreeSkies/VersionControlStatus", defaultDockPosition = MainToolbarDockPosition.Right)]
        private static MainToolbarElement VersionControlStatusElement() =>
            MainToolbarExtender.CreateIMGUIToolbar(OnToolbarGUI);
        
        private static void OnToolbarGUI()
        {
            GUILayout.FlexibleSpace();
            
            var buttonRect = GUILayoutUtility.GetRect(MainToolbarExtender.TOOLBAR_HEIGHT, MainToolbarExtender.TOOLBAR_HEIGHT);
            
            var mousePos = Event.current.mousePosition;
            if (buttonRect.Contains(mousePos))
            {
                SirenixEditorGUI.DrawRoundRect(buttonRect, Color.gray2, 4);
            }

            buttonRect.y -= 4;
            
            if (SirenixEditorGUI.IconButton(buttonRect, GetStatusIcon().image, $"Version Control State: {Provider.onlineState.ToString()}"))
            {
                if (!IsConnected) TryConnect();
                else SettingsService.OpenProjectSettings("Project/VersionControl");
            }
        }
    }
}