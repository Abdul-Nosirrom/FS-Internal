using FS.Editor;
using FS.UtilityEditor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.GameplayActions.Editor
{
    [CustomEditor(typeof(GameplayAction), true)]
    public class GameplayActionEditor : OdinEditor
    {
        [MenuItem("CONTEXT/GameplayAction/Rename Action", false, 1)]
        private static void RenameAction(MenuCommand command)
        {
            var action = command.context as GameplayAction;
            if (!action) return;
            
            var newName = TextEntryDialog.Show("Rename Action", "Enter new action name:");
            if (newName != action.actionName)
            {
                Undo.RecordObject(action, "Renaming Action");
                action.actionName = newName;
                EditorUtility.SetDirty(action);
            }
        }

        private SerializedProperty m_actionNameProp => serializedObject.FindProperty("actionName");

        protected override void OnEnable() => ComponentTitlebarGUI.OnTitlebarGUI += DrawActionName;
        protected override void OnDisable() => ComponentTitlebarGUI.OnTitlebarGUI -= DrawActionName;

        private void DrawActionName(Rect rect, Object obj)
        {
            if (obj is not GameplayAction action) return;
            
            // Figured out via color-picker
            Color headerColor = new Color(62, 62, 62, 255) / 255f;
            Color headerHighlightedColor = new Color(71, 71, 71, 255) / 255f;
            
            Rect textAreaRect = rect;
            textAreaRect.x += 60;
            textAreaRect.width -= 60;
            textAreaRect.y += 1;
            textAreaRect.height -= 2;
            
            if (rect.Contains(Event.current.mousePosition))
                SirenixEditorGUI.DrawSolidRect(textAreaRect, headerHighlightedColor);
            else
                SirenixEditorGUI.DrawSolidRect(textAreaRect, headerColor);
            

            var labelWidth = GUI.skin.label.CalcSize(new GUIContent(action.actionName)).x;
            textAreaRect.width = labelWidth;
            textAreaRect.y -= 2;
            EditorGUI.DropShadowLabel(textAreaRect, action.actionName);
            //EditorGUI.LabelField(textAreaRect, action.actionName, SirenixGUIStyles.BoldLabel);
        }

        private Rect m_titleRect =>
            new Rect(0, 0, EditorGUIUtility.currentViewWidth, EditorGUIUtility.singleLineHeight);
        
        public override void OnInspectorGUI()
        {
            var action = (GameplayAction)target;
            SirenixEditorGUI.Title($"{action.actionName}", "", TextAlignment.Center, true);

            var currentEvt = Event.current;
            
            if (currentEvt is { type : EventType.ContextClick } && m_titleRect.Contains(currentEvt.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Rename Action"), false, () =>
                {
                    var newName = TextEntryDialog.Show("Rename Action", "Enter new action name:");
                    if (newName != action.actionName)
                    {
                        serializedObject.Update();
                        m_actionNameProp.stringValue = newName;
                        serializedObject.ApplyModifiedProperties();
                    }
                });
                menu.AddItem(new GUIContent("Reset Name"), false, () =>
                {
                    serializedObject.Update();
                    m_actionNameProp.stringValue = $"{action.GetType().Name}";
                    serializedObject.ApplyModifiedProperties();
                });
                menu.ShowAsContext();
                currentEvt.Use();
            }
            
            base.OnInspectorGUI();
        }
    }
}