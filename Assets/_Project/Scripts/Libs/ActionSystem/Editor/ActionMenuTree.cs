using System;
using System.Collections.Generic;
using FS.Editor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;

namespace FS.GameplayActions.Editor
{
    /// <summary>
    /// OdinMenuTree for the Action System, splitting categories into action channels and supports
    /// different coloring of the nodes
    /// </summary>
    public class ActionMenuTree
    {
        private GameplayAction[] m_actions;
        private OdinMenuTree m_menuTree;
        
        private Dictionary<ActionChannel, List<GameplayAction>> m_actionsByChannel;
        private List<GameplayAction> m_actionsWithNoChannel;

        public ActionMenuTree(GameplayAction[] actionsLookup, 
            Action<OdinMenuTree, ActionChannel, GameplayAction> onAddItem, Action<GameplayAction> onSelection)
        {
            m_actions = actionsLookup;
            InitData();
            
            m_menuTree = new OdinMenuTree();
            m_menuTree.Config.DrawSearchToolbar = true;
            m_menuTree.Selection.SupportsMultiSelect = false;

            m_menuTree.Config.SearchFunction = (menuItem) =>
            {
                // Get the gameplay action associated with it, return true for the search
                // if it matches the action itself or the name of one of its channels
                string[] path = menuItem.GetFullPath().Split("/");

                foreach (var item in path)
                {
                    if (item.ToLower().Contains(m_menuTree.Config.SearchTerm.ToLower()))
                        return true;
                }

                return false;
            };

            // Add each channel as a category
            foreach (var channel in ActionChannelUtils.IterateChannels())
            {
                // Skip if no actions in this channel
                if (m_actionsByChannel[channel].Count == 0) continue;

                foreach (var actionKey in m_actionsByChannel[channel])
                {
                    onAddItem(m_menuTree, channel, actionKey);
                }
            }
            
            // Add actions with no channel under their own category
            if (m_actionsWithNoChannel.Count > 0)
            {
                foreach (var actionKey in m_actionsWithNoChannel)
                {
                    m_menuTree.Add($"No Channel/{actionKey.actionName}", actionKey, EditorIcons.AlertTriangle.Raw);
                }
            }
            
            // Setup selection change handler
            m_menuTree.Selection.SelectionChanged += (selections) =>
            {
                onSelection(m_menuTree.Selection.SelectedValue as GameplayAction);
            };
        }
        
        public void SetSelection(GameplayAction selection) => FreeSkiesEditor.SetMenuTreeSelection(m_menuTree, selection);
        public void DrawMenuTree() => m_menuTree?.DrawMenuTree();

        private void InitData()
        {
            m_actionsByChannel = new Dictionary<ActionChannel, List<GameplayAction>>();
            m_actionsWithNoChannel = new List<GameplayAction>();
            
            foreach (ActionChannel channel in ActionChannelUtils.IterateChannels())
            {
                m_actionsByChannel[channel] = new();
            }

            foreach (var action in m_actions)
            {
                // Check for actions with no channels
                if (action.Channels == 0)
                {
                    m_actionsWithNoChannel.Add(action);
                    continue;
                }
                
                // Add to relevant channel lists
                foreach (ActionChannel channel in ActionChannelUtils.IterateChannels(action.Channels))
                {
                    if (!m_actionsByChannel.ContainsKey(channel))
                    {
                        m_actionsByChannel[channel] = new List<GameplayAction>();
                    }
                    m_actionsByChannel[channel].Add(action);
                }
            }
        }
    }
}