using System.Collections.Generic;
using System.Linq;
using FS.Editor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.GameplayActions.Editor
{
    // TODO: Would be nice to show active constraints running 
    public class ActionControllerDebugWindow : EditorWindow
    {
        [MenuItem("Free Skies/Action Controller Runtime Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<ActionControllerDebugWindow>();
            window.titleContent = new UnityEngine.GUIContent("Action Controller Debugger");
            window.Show();
        }
        
        private OdinMenuTree m_gameObjectTree;
        private PropertyTree m_actionPropertyTree;

        private List<ActionController> m_actionControllers;
        private ActionController m_actionController;
        private PlayerStateSelector m_playerState;
        private GameplayAction m_action;
        
        Dictionary<GameplayAction, bool> m_debugOnStart = new();
        Dictionary<GameplayAction, bool> m_debugOnEnd = new();

        
        private GUIStyle m_categoryHeaderStyle => GUIStyles.GUIStyles.WhiteLargeCenterLabel;
        private GUIStyle m_sectionHeaderStyle => GUIStyles.GUIStyles.LargeBoldLabel;
        
        private void OnEnable()
        {
            EditorApplication.update += ConditionalRepaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= ConditionalRepaint;
            m_actionPropertyTree?.Dispose();
            m_actionPropertyTree = null;
            m_action = null;
            m_actionController = null;
            m_gameObjectTree = null;
            m_actionControllers = null;
        }

        private float m_lastRepaintTime;
        private void ConditionalRepaint()
        {
            // Only repaint at most 10 times per second (every 0.1s)
            if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup - m_lastRepaintTime > 0.1f)
            {
                m_lastRepaintTime = (float)EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void InitializeGameObjectMenu()
        {
            m_gameObjectTree = new OdinMenuTree();
            m_gameObjectTree.Selection.SupportsMultiSelect = false;
            m_gameObjectTree.Config.DrawSearchToolbar = true;
            
            foreach (var controller in m_actionControllers)
            {
                m_gameObjectTree.Add(controller.gameObject.name, controller);
                
                // Debug menu cache
                foreach (var action in controller.IterateAllActions<GameplayAction>())
                {
                    m_debugOnStart.TryAdd(action, false);
                    m_debugOnEnd.TryAdd(action, false);
                }
            }
            
            m_gameObjectTree.Selection.SelectionChanged += x =>
            {
                m_actionController = null;
                
                m_actionController = m_gameObjectTree.Selection.SelectedValue as ActionController;
                if (m_actionController == null) return;
                m_playerState = m_actionController.gameObject.GetComponent<PlayerStateSelector>();
            };
        }

        private bool InitializeControllers()
        {
            var actionControllers = GameActionTicker.Instance.Editor_Controllers;
            if (!actionControllers.Equals(m_actionControllers))
            {
                m_actionControllers = actionControllers;
                InitializeGameObjectMenu();
            }
            if (m_actionControllers.Count == 0)
            {
                GUILayout.Label("No Action Controllers found");
                return false;
            }

            if (m_actionController == null || !m_actionControllers.Contains(m_actionController))
            {
                m_actionController = m_actionControllers.FirstOrDefault();
                FreeSkiesEditor.SetMenuTreeSelection(m_gameObjectTree, m_actionController);
            }
            
            return m_actionController != null;
        }
        
        private GUILayoutOption[] LayoutWidth(float percent)
        {
            float width = position.width * percent;
            return new GUILayoutOption[] { GUILayout.MinWidth(width), GUILayout.Width(width), GUILayout.MaxWidth(width), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true) };
        }
        
        private void OnGUI()
        {
            using (var mainVerticalLayout = new EditorGUILayout.VerticalScope())
            {
                var logoRect = FreeSkiesEditor.DrawLogo(200f, SirenixGUIStyles.BoxContainer);
                
                if (!Application.isPlaying)
                {
                    GUILayout.Label("Game is not running");
                    return;
                }

                if (!InitializeControllers()) return;

                using (var horizontalDebugSections = new EditorGUILayout.HorizontalScope())
                {
                    DrawGameObjectSelector();

                    DrawActionDebugList();

                    DrawSelectedActionInfo();
                }
            }
        }

        private Vector2 m_goSelectorScrollPos;
        private void DrawGameObjectSelector()
        {
            using var goSelectionVertical = new EditorGUILayout.ScrollViewScope(m_goSelectorScrollPos, GUIStyles.GUIStyles.HelpBox, LayoutWidth(0.2f));
            m_goSelectorScrollPos = goSelectionVertical.scrollPosition;
            
            GUILayout.Label("Active GameObjects", m_categoryHeaderStyle);
            SirenixEditorGUI.HorizontalLineSeparator();
            
            m_gameObjectTree?.DrawMenuTree();
        }

        private bool m_debugFoldout = false;
        private bool m_constraintFoldout = false;
        private void DrawActionDebugList()
        {
            using var mainVertical = new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox, LayoutWidth(0.38f));
            
            if (m_actionController == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("No Action Controller selected", GUIStyles.GUIStyles.WhiteLargeCenterLabel);
                GUILayout.FlexibleSpace();
                return;
            }
            
            var activeActions = m_actionController.ActiveActions;

            m_debugFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(m_debugFoldout, "Debug break");
            if (m_debugFoldout)
            {
                using (var debugListVertical = new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox))
                {
                    //GUILayout.Label("Debug Break", m_sectionHeaderStyle);
                    //SirenixEditorGUI.HorizontalLineSeparator();

                    foreach (var action in m_actionController.IterateAllActions<GameplayAction>())
                    {
                        using (var debugListEntry = new EditorGUILayout.HorizontalScope(GUIStyles.GUIStyles.HelpBox))
                        {
                            GUILayout.Label(action.actionName);
                            GUILayout.FlexibleSpace();

                            var ogColor = GUI.backgroundColor;
                            GUI.backgroundColor = m_debugOnStart[action]
                                ? FreeSkiesEditor.ToggleOnColor
                                : FreeSkiesEditor.ToggleOffColor;
                            if (GUILayout.Button("On Start"))
                            {
                                m_debugOnStart[action] = !m_debugOnStart[action];
                                if (m_debugOnStart[action])
                                    action.OnActionStarted += DebugBreakOnActionStart;
                                else
                                    action.OnActionStarted -= DebugBreakOnActionStart;
                            }

                            GUI.backgroundColor = m_debugOnEnd[action]
                                ? FreeSkiesEditor.ToggleOnColor
                                : FreeSkiesEditor.ToggleOffColor;
                            if (GUILayout.Button("On End"))
                            {
                                m_debugOnEnd[action] = !m_debugOnEnd[action];
                                if (m_debugOnEnd[action])
                                    action.OnActionEnded += DebugBreakOnActionEnd;
                                else
                                    action.OnActionEnded -= DebugBreakOnActionEnd;
                            }

                            GUI.backgroundColor = ogColor;
                        }
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            m_constraintFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(m_constraintFoldout, "Constraints");
            if (m_constraintFoldout)
            {
                using var constraintsVertical = new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox);
                foreach (var constraint in m_actionController.ConstraintHandler.Editor_Constraints)
                {
                    bool enableState = constraint.IsEnabled;
                    if (FreeSkiesEditor.ToggleButton(constraint.Name, ref enableState))
                    {
                        if (enableState) constraint.EnableConstraint();
                        else constraint.DisableConstraint();
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            using (var channelsVertical = new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox))
            {
                GUILayout.Label("Action Channels", m_sectionHeaderStyle);
                SirenixEditorGUI.HorizontalLineSeparator();

                foreach (var channel in ActionChannelUtils.IterateChannels())
                {
                    using var channelEntryHorizontal = new EditorGUILayout.HorizontalScope();
                    
                    bool isOccupied = activeActions.ContainsAnyChannel(channel);
                    //var channelColor = isOccupied ? FreeSkiesEditor.ToggleOnColor : FreeSkiesEditor.ToggleOffColor;

                    string entryText = isOccupied ? $"{channel} - {activeActions[channel]}" : channel.ToString();
                    
                    GUI.enabled = isOccupied;
                    GUILayout.Toggle(isOccupied, entryText);
                    GUI.enabled = true;
                }
            }
            
            using (var activeActionsVertical = new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox))
            {
                GUILayout.Label("Active Actions", m_sectionHeaderStyle);
                if (activeActions.Actions.Any()) SirenixEditorGUI.HorizontalLineSeparator();

                DrawActiveActionsList();
            }

            using (var availableActionsVertical = new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox))
            {
                GUILayout.Label("Available Actions", m_sectionHeaderStyle);
                SirenixEditorGUI.HorizontalLineSeparator();

                if (m_playerState != null)
                {
                    DrawPlayerAvailableActionsList();
                }
            }
        }

        private void DrawSelectedActionInfo()
        {
            using var mainVertical = new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox, LayoutWidth(0.4f));
            
            if (m_action == null || m_actionController == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("No Action Selected", GUIStyles.GUIStyles.WhiteLargeCenterLabel);
                GUILayout.FlexibleSpace();
                return;
            }
            
            EditorGUILayout.LabelField("Selected Action:", m_action.actionName, m_sectionHeaderStyle);

            using (var actionInfoVertical = new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox))
            {
                GUILayout.Label("Selected Action Action Info");

                var ogColor = GUI.color;
                GUI.color = m_action.IsActive ? FreeSkiesEditor.ToggleOnColor : FreeSkiesEditor.ToggleOffColor;
                EditorGUILayout.LabelField("Active:", m_action.IsActive.ToString());
                GUI.color = ogColor;
                
                EditorGUILayout.LabelField("Channels:", m_action.Channels.ToString());
                EditorGUILayout.LabelField("Time Active:", m_action.IsActive ? m_action.m_timeSinceStarted.ToString() : "0");
                EditorGUILayout.LabelField("Owner:", m_actionController.gameObject.name);
                EditorGUILayout.LabelField("Parent Action", m_action.m_parentAction == null ? "Root" : m_action.m_parentAction.actionName);

                using (var buttonsHoriz = new EditorGUILayout.HorizontalScope())
                {
                    if (!m_action.IsActive && GUILayout.Button("Start Action"))
                    {
                        m_action.TryStartAction();
                    }
                    if (m_action.IsActive && GUILayout.Button("End Action"))
                    {
                        m_action.TryEndAction();
                    }
                }
            }
            
            using (var actionPropertiesVertical = new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox))
            {
                GUILayout.Label("Action Properties");
                if (GUILayout.Button("Copy Changes To Asset")) // TODO: Anyway to save this from playmode?
                {
                }
                m_actionPropertyTree?.Draw(false);
            }
        }
        
        private void DrawActiveActionsList()
        {
            void DrawActionCard(GameplayAction action)
            {
                // Name (Channels: X | Y | Z)
                string mainLabel = $"{action.actionName} (Channels: ";
                foreach (var channel in ActionChannelUtils.IterateChannels(action.Channels))
                {
                    mainLabel += $"{channel} |";
                }
                mainLabel = mainLabel.TrimEnd('|');
                mainLabel += ")";
                
                // Structure label (Root Action / Followup From: parent)
                string structureLabel = $"Active for: {action.m_timeSinceStarted.ToString()}s";

                var ogBGColor = GUI.backgroundColor;
                if (action == m_action)
                {
                    GUI.backgroundColor = FreeSkiesEditor.ToggleOnColor;
                }
                
                if (GUILayout.Button(mainLabel + "\n" + structureLabel))
                {
                    SelectDebugAction(action);
                }
                
                GUI.backgroundColor = ogBGColor;
            }
            
            foreach (var activeAction in m_actionController.ActiveActions.Actions)
            {
                DrawActionCard(activeAction);
            }
        }

        private void DrawPlayerAvailableActionsList()
        {
            void DrawActionCard(GameplayAction action, GameplayAction parent)
            {
                // Name (Channels: X | Y | Z)
                string mainLabel = $"{action.actionName} (Channels: ";
                foreach (var channel in ActionChannelUtils.IterateChannels(action.Channels))
                {
                    mainLabel += $"{channel} |";
                }
                mainLabel = mainLabel.TrimEnd('|');
                mainLabel += ")";
                
                // Structure label (Root Action / Followup From: parent)
                string structureLabel = parent == null ? "Root Action" : $"Followup From: {parent.actionName}";

                var ogBGColor = GUI.backgroundColor;
                if (action == m_action)
                {
                    GUI.backgroundColor = FreeSkiesEditor.ToggleOnColor;
                }
                
                if (GUILayout.Button(mainLabel + "\n" + structureLabel))
                {
                    SelectDebugAction(action);
                }
                
                GUI.backgroundColor = ogBGColor;
            }
            
            // Foreach followup in active actions, try activating
            foreach (var activeAction in m_actionController.ActiveActions.Actions.ToHashSet())
            {
                foreach (var followUp in m_playerState.m_actionGraph.NodeEdges(activeAction))
                {
                    DrawActionCard(followUp, activeAction);
                }
            }
            
            // Foreach root action, try activating
            foreach (var rootAction in m_playerState.m_actionGraph.RootNodes)
            {
                if (rootAction.IsActive && !rootAction.IsRetriggerable) continue;
                DrawActionCard(rootAction, null);
            }
        }

        private void SelectDebugAction(GameplayAction action)
        {
            m_action = action;
            m_actionPropertyTree?.Dispose();
            if (action != null)
                m_actionPropertyTree = PropertyTree.Create(action);
        }

        // Can store a dict <action, bool> debugOnEnd & debugOnStart and have these as toggles instead of one-time buttons
        private void DebugBreakOnActionStart(GameplayAction action)
        {
            //action.OnActionStarted -= DebugBreakOnActionStart;
            EditorApplication.isPaused = true;
        }
        private void DebugBreakOnActionEnd(GameplayAction action)
        {
            //action.OnActionEnded -= DebugBreakOnActionEnd;
            EditorApplication.isPaused = true;
        }
    }
}