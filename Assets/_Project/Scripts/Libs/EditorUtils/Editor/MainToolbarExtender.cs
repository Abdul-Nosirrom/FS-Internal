using System;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace FS.Editor
{
    /// <summary>
    /// Simple utility for MainToolbar extensions (6.3). Such as creating an ImGUI visual element container to use
    /// basic ImGUI approaches rather than being constrained by MainToolbarElement's UIElements-only approach.
    /// </summary>
    public static class MainToolbarExtender
    {
        public const int TOOLBAR_HEIGHT = 20;
        
        /// <summary>
        /// Creates a MainToolbarElement that contains an IMGUIContainer which calls the provided GUIFunc for its OnGUI.
        /// Out parameter imguiContainer provides access to the created IMGUIContainer for further customization if needed.
        ///
        /// Usage:
        /// <code>
        /// [MainToolbarElement("YourNamespace/YourElementName", defaultDockIndex = -1, defaultDockPosition = MainToolbarDockPosition.Middle)]
        /// private static MainToolbarElement YourElementName() => MainToolbarExtender.CreateIMGUIToolbar(YourGUIFunc, out var _);
        ///
        /// private static void YourGUIFunc()
        /// {
        ///     if (GUILayout.Button("Click Me")) Debug.Log("Button Clicked!");
        /// }
        /// </code>
        /// </summary>
        public static MainToolbarElement CreateIMGUIToolbar(Action GUIFunc)
        {
            var type = typeof(MainToolbarButton).Assembly.GetType("UnityEditor.Toolbars.MainToolbarCustom", true);
            var element = (MainToolbarElement)Activator.CreateInstance(type, (Func<VisualElement>) (() => new IMGUIContainer(GUIFunc) { style = { marginLeft = 5, marginRight = 5}}));
            return element;
        }
    }
}