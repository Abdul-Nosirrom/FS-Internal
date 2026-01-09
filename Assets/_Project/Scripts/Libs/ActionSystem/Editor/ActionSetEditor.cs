using System.Linq;
using System.Reflection;
using FS.Editor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.GameplayActions.Editor
{
    public class GameplayActionSetDrawer<T> : OdinValueDrawer<T> where T : GameplayAction
    {
        private GameplayAction[] m_allActions;
        private GameplayActionSet m_actionSet;
    
        protected override void Initialize()
        {
            m_actionSet = this.Property.Parent.ValueEntry.WeakSmartValue as GameplayActionSet;
            if (!m_actionSet)
            {
                Debug.LogError($"Tried running Set Value Drawer on incorrect type {this.Property.Parent.ValueEntry.TypeOfValue}");
                return;
            }
        
            var actionController = m_actionSet.GetComponentInParent<ActionController>();
            if (actionController)
                m_allActions = actionController.GetComponentsInChildren<GameplayAction>();
            else
                m_allActions = m_actionSet.GetComponentsInChildren<GameplayAction>();
        }

        protected override bool CanDrawValueProperty(InspectorProperty property)
        {
            return (typeof(GameplayActionSet)).IsAssignableFrom(property.ParentType);
            return base.CanDrawValueProperty(property);
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (m_allActions == null || m_allActions.Length == 0)
            {
                SirenixEditorGUI.ErrorMessageBox("Failed to find actions.");
                return;
            }

            {
                var currentAction = ValueEntry.SmartValue;
                
                EditorGUILayout.BeginHorizontal();
                
                // Get prev rect
                var fieldRect = GUILayoutUtility.GetRect(90f, EditorGUIUtility.singleLineHeight);
                fieldRect.y += 1f;
                fieldRect.width = EditorGUIUtility.currentViewWidth;
                var col = currentAction ? new Color(0.1f, 0.6f, 0.1f, 0.3f) : new Color(0.6f, 0.1f, 0.1f, 0.3f);
                EditorGUI.DrawRect(fieldRect, col);

                fieldRect.width = 90f;
                EditorGUI.DropShadowLabel(fieldRect, $"{ValueEntry.Property.NiceName}", SirenixGUIStyles.BoldLabel);
                
                FreeSkiesEditor.DropDown(currentAction ? currentAction.actionName : "Unassigned", currentAction ? currentAction.actionName : null, () =>
                {
                    var compatibleActions = m_allActions.Where(a => a != currentAction && typeof(T).IsAssignableFrom(a.GetType())).Select(a => a.actionName).ToArray();
                    // obv need some additional checking as well to prevent duplicate or the same assignemtn
                    return compatibleActions;
                }, actions =>
                {
                    var selectedName = actions.FirstOrDefault();
                    var selection = m_allActions.FirstOrDefault(a => a != currentAction && a.actionName == selectedName && typeof(T).IsAssignableFrom(a.GetType()));
                    
                    ValueEntry.SmartValue = selection as T;
                });
                
                EditorGUILayout.EndHorizontal();
                
                SirenixEditorGUI.HorizontalLineSeparator();
            }
        }
    }
}