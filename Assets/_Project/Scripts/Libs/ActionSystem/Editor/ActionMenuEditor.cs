using System;
using System.Collections.Generic;
using System.Reflection;
using FS.Editor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace FS.GameplayActions.Editor
{
    public class ActionMenuEditor : IDisposable
    {
        public event Action OnStartDrawSidebar;
        public event Action<OdinMenuTree, ActionChannel, GameplayAction> OnDrawSidebarElement;
        public event Action<GameplayAction> OnDrawSelectedAction;

        private GameplayAction[] m_actions;
        private GameplayActionSet[] m_actionSets;
        private Dictionary<GameplayAction, PropertyTree> m_actionPropertyTrees = new();
        private PropertyTree[] m_actionSetPropertyTrees;

        private ActionMenuTree m_actionsSidebar;
        
        private bool m_canDrawActionSets => m_actionSets != null && m_actionSets.Length > 0;

        public ActionMenuEditor(GameplayAction[] actions, GameplayActionSet[] actionSets)
        {
            m_actions = actions;
            m_actionSets = actionSets;
            InitializeActionsEditor();
        }

        private void InitializeActionsEditor()
        {
            // Init property tree
            m_actionPropertyTrees.Clear();
            foreach (var action in m_actions)
            {
                var serializedObject = new SerializedObject(action);
                var propertyTree = PropertyTree.Create(serializedObject);
                propertyTree.DrawMonoScriptObjectField = false;
                
                m_actionPropertyTrees[action] = propertyTree;
            }
            
            // Init action set property trees
            if (m_actionSets is { Length: > 0 })
            {
                m_actionSetPropertyTrees = new PropertyTree[m_actionSets.Length];

                int idx = 0;
                foreach (var actionSet in m_actionSets)
                {
                    var serializedObject = new SerializedObject(actionSet);
                    var propertyTree = PropertyTree.Create(serializedObject);
                    propertyTree.DrawMonoScriptObjectField = false;
                    m_actionSetPropertyTrees[idx] = propertyTree;
                    idx++;
                }
            }
            
            // Setup sidebar men
            m_actionsSidebar = new ActionMenuTree(m_actions, OnAddActionToMenuTree, OnActionSelected);
        }

        public void Dispose()
        {
            // Dispose all property trees
            foreach (var tree in m_actionPropertyTrees.Values)
            {
                tree?.Dispose();
            }

            if (m_actionSetPropertyTrees != null)
            {
                foreach (var tree in m_actionSetPropertyTrees)
                {
                    tree?.Dispose();
                }

                m_actionSetPropertyTrees = null;
            }
            
            m_actionPropertyTrees.Clear();
        }
        
        private void OnAddActionToMenuTree(OdinMenuTree menuTree, ActionChannel channel, GameplayAction action)
        {
            OnDrawSidebarElement?.Invoke(menuTree, channel, action);
        }
        
        private void OnActionSelected(GameplayAction action)
        {
            m_selectedAction = action;
        }

        public void RefreshSidebar()
        {
            m_actionsSidebar = new ActionMenuTree(m_actions, OnAddActionToMenuTree, OnActionSelected);
        }

        private bool m_isEditingActionSets = false;
        
        /// <summary>
        /// Returns true if there were any changes made to the actions
        /// </summary>
        public void OnGUI()
        {
            DrawToolbar();

            if (m_isEditingActionSets && m_canDrawActionSets)
                DrawActionSetEditorGUI();
            else
            {
                GUILayout.BeginHorizontal();
                DrawActionSelectionSidebar();
                DrawSelectedActionsEditor(); // NOTE: This is expensive, ~3.4
                GUILayout.EndHorizontal();
            }
        }

        private void DrawToolbar()
        {
            if (!m_canDrawActionSets) return;
            
            SirenixEditorGUI.BeginHorizontalToolbar(SirenixGUIStyles.BoxContainer, 24f);
            if (SirenixEditorGUI.ToolbarTab(!m_isEditingActionSets, "Actions"))
            {
                m_isEditingActionSets = false;
            }
            if (SirenixEditorGUI.ToolbarTab(m_isEditingActionSets, "Action Sets"))
            {
                m_isEditingActionSets = true;
            }
            SirenixEditorGUI.EndHorizontalToolbar();

        }
        
        private void DrawActionSetEditorGUI()
        {
            foreach (var actionSetProp in m_actionSetPropertyTrees)
            {
                EditorGUILayout.BeginVertical(GUIStyles.GUIStyles.HelpBox);

                var rect = EditorGUILayout.GetControlRect();
                EditorGUI.DropShadowLabel(rect, actionSetProp.TargetType.Name);

                SirenixEditorGUI.HorizontalLineSeparator(Color.rebeccaPurple, 2);
                
                actionSetProp.Draw();
                // TODO: Need editor for action sets to handle this then we can just use property trees
                
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawActionSelectionSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(220f));
            {
                OnStartDrawSidebar?.Invoke(); // For player state selector, the show root btton that then reinitializes the men tree
                
                Rect currentLayoutRect = GUIHelper.GetCurrentLayoutRect();
                EditorGUI.DrawRect(currentLayoutRect, SirenixGUIStyles.MenuBackgroundColor);
                m_actionsSidebar.DrawMenuTree();
            }
            EditorGUILayout.EndVertical();
        }

        private GameplayAction m_selectedAction;

        public GameplayAction SelectedAction
        {
            get => m_selectedAction;
            set
            {
                m_selectedAction = value;
                m_actionsSidebar.SetSelection(value);
            }
        }
        private Vector2 m_actionEditorScrollPos;
        
        private void DrawSelectedActionsEditor()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                using (var scrollPos = new EditorGUILayout.ScrollViewScope(m_actionEditorScrollPos, GUIStyles.GUIStyles.HelpBox))
                {
                    m_actionEditorScrollPos = scrollPos.scrollPosition;

                    if (!m_selectedAction)
                    {
                        EditorGUILayout.HelpBox("No Action Selected", MessageType.Info);
                        return;
                    }
                    
                    // Header Label TODO: Would be great to have set-information on here too, so we know if this action has been assigned to a set rather than relying on name only
                    GUILayout.BeginHorizontal();
                    {
                        GUIStyle headerStyle = new GUIStyle(GUIStyles.GUIStyles.WhiteLargeCenterLabel);
                        headerStyle.alignment = TextAnchor.MiddleCenter;
                        headerStyle.fontSize = 24;
                        headerStyle.fixedHeight = 26f;
                        EditorGUILayout.LabelField(m_selectedAction.actionName, headerStyle);

                        var labelRect = GUILayoutUtility.GetLastRect();
                        if (Event.current.type == EventType.ContextClick && labelRect.Contains(Event.current.mousePosition))
                        {
                            GenericMenu menu = new GenericMenu();
                            menu.AddItem(new GUIContent("Rename Action"), false, () =>
                            {
                                var newName = TextEntryDialog.Show("Rename Action", "Enter new action name:");
                                if (newName != m_selectedAction.actionName)
                                {
                                    Undo.RecordObject(m_selectedAction, "Rename Action");
                                    m_selectedAction.actionName = newName;
                                    EditorUtility.SetDirty(m_selectedAction);
                                }
                            });
                            menu.ShowAsContext();
                        }
                    }
                    GUILayout.EndHorizontal();
                    
                    GUILayout.Space(10f);
                    
                    SirenixEditorGUI.HorizontalLineSeparator(2);
                        
                    // Channels this action occupies, If no channels, show a warning
                    using (new GUILayout.HorizontalScope(GUIStyles.GUIStyles.HelpBox))
                    {
                        GUILayout.Label("Channels:", SirenixGUIStyles.BoldLabelCentered);
                            
                        // Show channels for this action
                        foreach (var channel in ActionChannelUtils.IterateChannels(m_selectedAction.Channels))
                        {
                            GUILayout.Label("• " + channel.ToString(), SirenixGUIStyles.Button);
                        }

                        GUILayout.FlexibleSpace();
                    }
                    
                    SirenixEditorGUI.HorizontalLineSeparator(2);

                    // Invoke event to draw custom stuff before drawing the inspector of an object
                    OnDrawSelectedAction?.Invoke(m_selectedAction);
                    
                    // Now we draw the inspector for this action
                    EditorGUILayout.Space();

                    if (!m_actionPropertyTrees.TryGetValue(m_selectedAction, out var propTree))
                    {
                        EditorGUILayout.HelpBox("ERROR! No Property Tree for Action", MessageType.Error);
                        return;
                    }

                    propTree.Draw();
                }
            }
        }
    }
}