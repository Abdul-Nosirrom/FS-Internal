using System;
using System.Collections.Generic;
using System.Linq;
using FS.Editor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace FS.GameplayActions.Editor
{
    [CustomEditor(typeof(PlayerStateSelector))]
    public class PlayerStateEditor : OdinEditor
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshActionGraph();
        }

        public override void OnInspectorGUI()
        {
            GUILayout.BeginVertical(GUIStyles.GUIStyles.HelpBox);
            {
                // Info about graph
                var playerState = (PlayerStateSelector)target;
                GUILayout.Label($"Nodes: {playerState.m_actionGraph.Nodes.Count}");
                GUILayout.Label($"Root Nodes: {playerState.m_actionGraph.RootNodes.ToHashSet().Count}");
                GUILayout.Label($"Edges: {playerState.m_actionGraph.Edges.Count}");
            }
            GUILayout.EndVertical();
            
            if (GUILayout.Button("Open Editor"))
            {
                RefreshActionGraph();
                PlayerStateEditorWindow.Open(serializedObject, (PlayerStateSelector)target);
            }

            if (GUILayout.Button("Refresh Actions Graph"))
            {
                RefreshActionGraph();
            }
            
            //base.OnInspectorGUI();
        }

        private void RefreshActionGraph()
        {
            var playerState = (PlayerStateSelector)target;
            var allActions = playerState.GetComponentsInChildren<GameplayAction>(true);

            bool anyAdditions = false;
            bool anyRemovals = false;
            
            // Ensure graph contains no null nodes in the event a component was deleted or something
            for (int n = playerState.m_actionGraph.Nodes.Count - 1; n >= 0; n--)
            {
                var node = playerState.m_actionGraph.Nodes[n];
                if (node.Node == null)
                {
                    anyRemovals = true;
                    playerState.m_actionGraph.RemoveNode(node.Node);
                    continue;
                }
                
                // Ensure followups are valid
                foreach (var followUp in node.Edges)
                {
                    if (!playerState.IsFollowupValid(node.Node, followUp, false))
                    {
                        anyRemovals = true;
                        playerState.m_actionGraph.RemoveEdge(node.Node, followUp);
                    }
                }
            }
            
            // Ensure everythings in the graph
            foreach (var action in allActions)
            {
                anyAdditions |= !playerState.m_actionGraph.HasNode(action); // means we're adding one
                playerState.m_actionGraph.AddNode(action);
            }

            anyAdditions |= playerState.m_actionGraph.ValidateRootNodes();
            
            // Sort root nodes based on hierarchy order
            anyAdditions |= playerState.m_actionGraph.SortRootNodes((a, b) =>
            {
                int aIdx = Array.IndexOf(allActions, a);
                int bIdx = Array.IndexOf(allActions, b);
                return aIdx.CompareTo(bIdx);
            });

            // We dont undo as this is just a refresh/validation step
            if (anyAdditions || anyRemovals)
                EditorUtility.SetDirty(playerState);
        }
    }

    public class PlayerStateEditorWindow : EditorWindow
    {
        public static void Open(SerializedObject serializedObject, PlayerStateSelector target)
        {
            PlayerStateEditorWindow window = GetWindow<PlayerStateEditorWindow>();
            window.InitializeEditorData(serializedObject, target);
        }

        // Target data
        private SerializedObject m_serializedObject;
        private PlayerStateSelector m_target;
        private GameplayAction[] m_actions;
        private GameplayActionSet[] m_actionSets;
        
        // Search bar for action selector
        private bool m_onlyShowRootActions = false;
        private bool m_showActionFollowups = true;
        
        // Editor
        private ActionMenuEditor m_actionsEditor;
        
        private void InitializeEditorData(SerializedObject serializedObject, PlayerStateSelector target)
        {
            m_serializedObject = serializedObject;
            m_target = target;

            SetupActionsEditor();
        }

        private void SetupActionsEditor()
        {
            m_actionsEditor?.Dispose(); // Dispose incase we're reinitializing

            if (m_target == null) return;
            
            m_actions = m_target.GetComponentsInChildren<GameplayAction>(true);
            m_actionSets = m_target.GetComponentsInChildren<GameplayActionSet>(true);
            
            m_actionsEditor = new ActionMenuEditor(m_actions, m_actionSets);
            m_actionsEditor.OnStartDrawSidebar += OnSidebarGUI;
            m_actionsEditor.OnDrawSidebarElement += OnSidebarElementGUI;
            m_actionsEditor.OnDrawSelectedAction += OnSelectedActionGUI;
            
            InitMenuTree();
        }
        
        private void InitMenuTree()
        {
            // Refresh the action editor side-bar TODO:
            m_actionsEditor.RefreshSidebar();
        }
        
        private void OnRefresh()
        {
            // Refresh editor as the change could involve adding / removing actions or action sets TODO
        }

        private void OnEnable()
        {
            Selection.selectionChanged += SelectionChanged;
            EditorApplication.hierarchyChanged += OnRefresh;
            EditorApplication.playModeStateChanged += RefreshPostPlaymode;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnRefresh;
            Selection.selectionChanged -= SelectionChanged;
            EditorApplication.playModeStateChanged -= RefreshPostPlaymode;
            
            m_actionsEditor?.Dispose();
            m_actionsEditor = null;
        }

        private bool m_repaintBound = false;
        private void RefreshPostPlaymode(PlayModeStateChange playmodeEvt)
        {
            SetupActionsEditor();

            if (playmodeEvt == PlayModeStateChange.EnteredPlayMode && !m_repaintBound)
            {
                m_repaintBound = true;
                EditorApplication.update += ConditionalRepaint;
            }
            else if (playmodeEvt == PlayModeStateChange.ExitingPlayMode && m_repaintBound)
            {
                m_repaintBound = false;
                EditorApplication.update -= ConditionalRepaint;
            }
        }
        
        private float m_lastRepaintTime;
        private void ConditionalRepaint()
        {
            // Only repaint at most 4 times per second (every 0.25s)
            if (Application.isPlaying && EditorApplication.timeSinceStartup - m_lastRepaintTime > 0.25f)
            {
                m_lastRepaintTime = (float)EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void SelectionChanged()
        {
            if (!Application.isPlaying) return;
            if (Selection.activeGameObject == null) return;

            if (m_target == null)
            {
                var newTarget = Selection.activeGameObject.GetComponentInChildren<PlayerStateSelector>();
                if (newTarget != null)
                {
                    m_target = newTarget;
                    SetupActionsEditor();
                }
            }
            
            // Dynamically swap out based on selection
            //var playerState = Selection.activeGameObject.GetComponentInChildren<PlayerStateSelector>();
            //if (!actionController) return;
        }
        
        private void OnGUI()
        {
            if (m_actionsEditor == null)
            {
                SetupActionsEditor();
                //return; NOTE: Dont return as maybe it gets excuted for Repaint but not Layout thus throwing a Getting Control 0 excepton
            }
            
            m_actionsEditor?.OnGUI();
        }

        private void OnSidebarGUI()
        {
            // Show only root button
            GUIStyle rootButtonStyle = GUIStyles.GUIStyles.LargeButton;
            rootButtonStyle.fontStyle = FontStyle.Bold;
            if (FreeSkiesEditor.ToggleButton("Root Actions Only", ref m_onlyShowRootActions, rootButtonStyle))
            {
                InitMenuTree();
            }
        }

        private void OnSidebarElementGUI(OdinMenuTree menuTree, ActionChannel channel, GameplayAction action)
        {
            if (action == null) return;
            
            bool isRoot = m_target.IsRootAction(action);

            if (!isRoot && m_onlyShowRootActions) return;

            // Create menu item with appropriate icon
            var Icon = isRoot ? EditorIcons.Checkmark.Raw : EditorIcons.FileCabinet.Raw;
            //Icon = isNull ? EditorIcons.AlertTriangle.Raw : Icon;
            var item = menuTree.Add($"{channel.ToString()}/{action.actionName}", action, Icon).Last();

            // if (item.Style.DefaultLabelStyle != null)
            //     item.Style.DefaultLabelStyle.fontStyle = FontStyle.Bold;
            //
            // // Set custom style
            item.OnDrawItem += (menuItem) =>
            {

                // Set color based on item type
                Color rectCol = Color.clear;
                if (isRoot)
                    rectCol = new Color(0.5f, 1f, 0.5f, 0.2f); // Green for root
                else //if (isNull)
                    rectCol = new Color(0.5f, 0.5f, 1f, 0.2f); // Blue for follow-up

                if (action.IsActive)
                    rectCol = new Color(1f, 0.5f, 0.5f, 0.2f); // Red for active

                EditorGUI.DrawRect(menuItem.Rect, rectCol);
            };
        }

        private void OnSelectedActionGUI(GameplayAction action)
        {
            if (action == null) return;
            DrawFollowUpsSection();
        }

        #region FOLLOWUPS

        private Vector2 m_followUpScrollPos;
        
        private void DrawFollowUpsSection()
        {
            using (new EditorGUILayout.VerticalScope(GUIStyles.GUIStyles.HelpBox))
            {
                // Show followups button
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Followups:", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    
                    GUIStyle showFollowupsStyle = GUIStyles.GUIStyles.LargeButton;
                    showFollowupsStyle.fontStyle = FontStyle.Bold;
                    Color ogcolor = GUI.color;
                    GUI.color = m_showActionFollowups ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.8f, 0.4f, 0.4f);
                    if (GUILayout.Button("Show", showFollowupsStyle))
                        m_showActionFollowups = !m_showActionFollowups;
                    GUI.color = ogcolor;

                    if (GUILayout.Button(EditorIcons.Plus.Raw, showFollowupsStyle))
                    {
                        // Show a popup to add a new followup
                        GenericMenu menu = new GenericMenu();
                        foreach (var action in m_actions)
                        {
                            if (!m_target.IsFollowupValid(m_actionsEditor.SelectedAction, action, true)) continue;
                            
                            menu.AddItem(new GUIContent(action.actionName), false, () =>
                            {
                                Undo.RecordObject(m_target, "Adding Followup");
                                
                                m_target.m_actionGraph.AddEdge(m_actionsEditor.SelectedAction, action);
                                
                                EditorUtility.SetDirty(m_target);
                                
                                InitMenuTree();
                            });
                        }
                        menu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
                    }
                }
                
                if (!m_showActionFollowups || !m_target.m_actionGraph.HasAnyEdges(m_actionsEditor.SelectedAction)) return;
                
                // Draw followup cardws
                m_followUpScrollPos =
                    EditorGUILayout.BeginScrollView(m_followUpScrollPos, false, false, GUILayout.Height(100f));

                EditorGUILayout.BeginHorizontal(GUIStyles.GUIStyles.HelpBox);

                var allFollowups = m_target.m_actionGraph.NodeEdges(m_actionsEditor.SelectedAction);
                for (int idx = 0; idx < allFollowups.Count; idx++)
                {
                    DrawFollowupCard(idx, allFollowups);
                }
                
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawFollowupCard(int followUpIdx, List<GameplayAction> allFollowups)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(140), GUILayout.Height(80));
            GameplayAction followUp = allFollowups[followUpIdx];
            
            // Card header
            var ogBGColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.5f, 0.5f, 1f, 0.5f);
            EditorGUILayout.BeginHorizontal(GUILayout.Height(24));
            
            //GUILayout.Label(followUp, EditorStyles.miniLabel);
            FreeSkiesEditor.DropDown(followUp.actionName, followUp, () =>
            {
                return m_actions.Where(followup => m_target.IsFollowupValid(m_actionsEditor.SelectedAction, followup, true)).ToArray();
            }, (selection) =>
            {
                Undo.RecordObject(m_target, "Changing Followups");

                var selectionList = selection as GameplayAction[] ?? selection.ToArray();
                
                m_target.m_actionGraph.RemoveEdge(m_actionsEditor.SelectedAction, followUp);
                m_target.m_actionGraph.AddEdge(m_actionsEditor.SelectedAction, selectionList.First());
                
                allFollowups.Insert(followUpIdx, selectionList.First());
                allFollowups.RemoveAt(allFollowups.Count - 1);
                
                EditorUtility.SetDirty(m_target);
                
                InitMenuTree();
            });
            
            EditorGUILayout.EndHorizontal();
    
            // Card content
            EditorGUILayout.BeginHorizontal();
            
            // Move left button
            GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            if (followUpIdx > 0)
            {
                if (GUILayout.Button(EditorIcons.ArrowLeft.Raw, GUILayout.Height(20), GUILayout.Width(20)))
                {
                    Undo.RecordObject(m_target, "Reordering Followups");
                
                    // "swap via deconstruction"
                    (allFollowups[followUpIdx-1], allFollowups[followUpIdx]) 
                        = (allFollowups[followUpIdx], allFollowups[followUpIdx-1]);

                    EditorUtility.SetDirty(m_target);
                }
            }

            // Action channels
            GUI.backgroundColor = ogBGColor;
            foreach (var channel in ActionChannelUtils.IterateChannels(followUp.Channels))
            {
                GUILayout.Label(channel.ToString(), EditorStyles.miniButton);
            }
            
            // Move right button
            GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            if (followUpIdx < allFollowups.Count - 1)
            {
                if (GUILayout.Button(EditorIcons.ArrowRight.Raw, GUILayout.Height(20), GUILayout.Width(20)))
                {
                    Undo.RecordObject(m_target, "Reordering Followups");
                
                    // "swap via deconstruction"
                    (allFollowups[followUpIdx], allFollowups[followUpIdx+1]) 
                        = (allFollowups[followUpIdx+1], allFollowups[followUpIdx]);

                    EditorUtility.SetDirty(m_target);
                }
            }
            
            EditorGUILayout.EndHorizontal();
    
            GUILayout.FlexibleSpace();
            
            // Edit button
            GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f, 0.5f);
            if (GUILayout.Button("Edit", GUILayout.Height(20)))
            {
                m_actionsEditor.SelectedAction = followUp;
            }
            
            // Delete followup button
            GUI.backgroundColor = new Color(1.0f, 0.5f, 0.5f, 0.5f);
            if (GUILayout.Button(EditorIcons.X.Raw, GUILayout.Height(10)))
            {
                Undo.RecordObject(m_target, "Removing Followup");
                
                m_target.m_actionGraph.RemoveEdge(m_actionsEditor.SelectedAction, followUp);
                
                EditorUtility.SetDirty(m_target);
                
                InitMenuTree();
            }
    
            GUI.backgroundColor = ogBGColor;
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }
        
        #endregion // Followups
    }
}