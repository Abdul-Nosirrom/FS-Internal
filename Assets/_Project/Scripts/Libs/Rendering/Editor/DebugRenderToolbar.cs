using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace FS.Rendering.Editor
{
    [Overlay(typeof(SceneView), "Debug Render")]
    [Icon("d_Profiler.Rendering@2x")]
    public class DebugRenderOverlay : ToolbarOverlay
    {
        DebugRenderOverlay() : base(DebugTextureSelector.Id) {}

        [EditorToolbarElement(Id, typeof(SceneView))]
        class DebugTextureSelector : EditorToolbarDropdown
        {
            public const string Id = "Debug/Render/TextureSelector";
            
            public DebugTextureSelector() : base()
            {
                this.icon = EditorGUIUtility.IconContent("d_Profiler.Rendering@2x").image as Texture2D;
                this.text = DebugRenderBlit.DebugType.ToString();
                this.tooltip = "Debug Render Texture Viewer";
                RegisterCallback<ClickEvent>(ShowSelectionDropdown);
            }

            private void ShowSelectionDropdown(ClickEvent evt)
            {
                var genMenu = new GenericMenu();
                
                genMenu.AddItem(new GUIContent("None"),  false, SelectionConfirmed, DebugRenderBlit.Type.None);
                genMenu.AddItem(new GUIContent("Depth"),  false, SelectionConfirmed, DebugRenderBlit.Type.Depth);
                genMenu.AddItem(new GUIContent("Normals"),  false, SelectionConfirmed, DebugRenderBlit.Type.Normals);
                genMenu.AddItem(new GUIContent("Motion Vectors"),  false, SelectionConfirmed, DebugRenderBlit.Type.MotionVectors);
                genMenu.AddItem(new GUIContent("GameObject Tag"), false, SelectionConfirmed, DebugRenderBlit.Type.GameTag);
                genMenu.AddItem(new GUIContent("Physics Layer"), false, SelectionConfirmed, DebugRenderBlit.Type.PhysicsLayer);
                genMenu.AddItem(new GUIContent("Collision"), false, SelectionConfirmed, DebugRenderBlit.Type.Collision);
                genMenu.AddItem(new GUIContent("Unique Meshes"), false, SelectionConfirmed, DebugRenderBlit.Type.UniqueMeshes);


                genMenu.DropDown(worldBound);
            }

            private void SelectionConfirmed(object debugTypeObj)
            {
                var debugType = (DebugRenderBlit.Type)debugTypeObj;
                DebugRenderBlit.DebugType = debugType;
                this.text = debugType.ToString();
            }
        }
    }
}