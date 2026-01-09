using System;
using FS.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace FS.Player.Editor
{
    public class CameraPlayToolbar
    {
        // [MainToolbarElement("FreeSkies/CameraPlayButton", defaultDockIndex = -1, defaultDockPosition = MainToolbarDockPosition.Middle)]
        // private static MainToolbarElement OnCameraPlayButton()
        // {
        //     GUI.enabled = !Application.isPlaying;
        //     var icon = EditorGUIUtility.IconContent("d_VideoPlayer Icon");
        //     var element = new MainToolbarButton(icon, () =>
        //     {
        //         PlayerSpawnConfig.instance.m_spawnAtCamera = true;
        //         PlayerSpawnConfig.instance.m_position = SceneView.lastActiveSceneView.camera.transform.position;
        //         PlayerSpawnConfig.instance.m_rotation = SceneView.lastActiveSceneView.camera.transform.rotation;
        //         
        //         EditorApplication.EnterPlaymode();
        //     });
        //     return element;
        // }
        
        [MainToolbarElement("FreeSkies/CameraPlayButton", defaultDockIndex = -1, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement CreateToolbarElement()
        {
            var element = MainToolbarExtender.CreateIMGUIToolbar(OnCameraPlayButtonGUI);
            //imguiContainer.style.marginBottom = imguiContainer.style.marginLeft = imguiContainer.style.marginRight = imguiContainer.style.marginBottom = 0;
            return element;
        }
        private static void OnCameraPlayButtonGUI()
        {
            GUI.enabled = !Application.isPlaying;
            var icon = EditorGUIUtility.IconContent("d_VideoPlayer Icon");
            if (GUILayout.Button(icon, GUILayout.Width(48f), GUILayout.Height(MainToolbarExtender.TOOLBAR_HEIGHT)))
            {
                PlayerSpawnConfig.instance.m_spawnAtCamera = true;
                PlayerSpawnConfig.instance.m_position = SceneView.lastActiveSceneView.camera.transform.position;
                PlayerSpawnConfig.instance.m_rotation = SceneView.lastActiveSceneView.camera.transform.rotation;

                EditorApplication.EnterPlaymode();
            }
            GUI.enabled = true;
        }
    }
}