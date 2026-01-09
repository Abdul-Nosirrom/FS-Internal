using FS.Animation.Editor;
using FS.CameraSystem.Editor;
using FS.GameplayActions.Editor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.Editor
{
    public class FreeSkiesToolsWindow : OdinEditorWindow
    {
        [MenuItem("Free Skies/All Tools", priority = 100)]
        private static void OpenWindow() => GetWindow<FreeSkiesToolsWindow>().Show();
        
        protected override void DrawEditors()
        {
            FreeSkiesEditor.DrawLogo(200f, GUIStyles.GUIStyles.HelpBox, new Color(0,0,0,0));
            base.DrawEditors();
        }

        [BoxGroup("Editor Tools")]
        [Button("Player Action Graph", ButtonSizes.Large)]
        private void OpenPlayerActionGraph() => PlayerStateEditorWindow.Open(null, null);

        [BoxGroup("Editor Tools")]
        [Button("Animation Editor", ButtonSizes.Large)]
        private void OpenAnimationEditor() => AnimationEditor.ShowWindow();
        
        [BoxGroup("Debuggers")]
        [HorizontalGroup("Debuggers/A"), Button("Action Controllers", ButtonSizes.Large)]
        private void OpenActionsDebugger() => ActionControllerDebugWindow.ShowWindow();

        [BoxGroup("Debuggers")]
        [HorizontalGroup("Debuggers/A"), Button("Camera Debugger", ButtonSizes.Large)]
        private void OpenCameraDebugger() => CameraBehaviorEditorWindow.ShowWindow();
        
        [BoxGroup("Debuggers")]
        [HorizontalGroup("Debuggers/A"), Button("Animation Debugger", ButtonSizes.Large)]
        private void OpenAnimationDebugger() => AnimationDebugger.ShowWindow();

    }
}