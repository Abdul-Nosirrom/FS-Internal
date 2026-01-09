using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.Animation.Editor
{
    // TODO: Composite editor, could be improved, but does the job for now
    public class AnimationEditor : OdinMenuEditorWindow
    {
        [MenuItem("Free Skies/Animation/Animation Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<AnimationEditor>();
            window.titleContent = new UnityEngine.GUIContent("Animation Editor");
            window.Show();
        }
        
        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree();
            tree.AddAllAssetsAtPath("Animations", "Assets", typeof(FSAnimation), true);
            
            tree.Selection.SelectionChanged += SelectionOnSelectionChanged;
            
            return tree;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EditorApplication.update += Repaint;
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            EditorApplication.update -= Repaint;
            if (m_animEditor != null)
            {
                DestroyImmediate(m_animEditor);
                m_animEditor = null;
            }
        }

        private void SelectionOnSelectionChanged(SelectionChangedType obj)
        {
            if (m_animEditor)
            {
                DestroyImmediate(m_animEditor);
                m_animEditor = null;
            }
            
            var selected = MenuTree.Selection.SelectedValue as FSAnimation;
            m_animEditor = UnityEditor.Editor.CreateEditor(selected) as FSAnimationEditor;
            //m_animEditor = CreateInstance<FSAnimationEditor>();
        }

        private FSAnimationEditor m_animEditor;

        protected override void OnEndDrawEditors()
        {
            // Draw the preview of the selected animation
            if (m_animEditor != null)
            {
                //EditorGUILayout.BeginVertical();
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal(GUIStyles.GUIStyles.HelpBox);
                GUILayout.FlexibleSpace();
                m_animEditor.OnPreviewSettings();
                GUILayout.EndHorizontal();
                var rect = GUILayoutUtility.GetRect(240, 1080, 240, 1080);
                m_animEditor.OnInteractivePreviewGUI(rect, SirenixGUIStyles.BoxContainer);
                //EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.LabelField("Select an animation to edit.");
            }
        }
    }
}