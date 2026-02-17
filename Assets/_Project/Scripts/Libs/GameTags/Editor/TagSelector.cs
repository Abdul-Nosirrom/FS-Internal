using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace FS.TagSystem.Editor
{
    /// <summary>
    /// Hierarchical tag selector built on Odin's OdinSelector.
    /// Displays tags in a tree structure matching the .gameplayTags definition file hierarchy,
    /// with search support and folder icons for branches.
    /// </summary>
    public class TagSelector : GenericSelector<string>
    {
        public event Action<Tag> OnTagSelected;
        public event Action<Tag> OnTagDeselected;
        
        private readonly HashSet<string> m_selectedTags;
        private readonly HashSet<string> m_tagFilter;
        private readonly bool m_shouldShowTagToggles;
        
        /// <summary>
        /// Creates a new tag selector.
        /// </summary>
        /// <param name="currentSelections">Tags already part of selection.</param>
        /// <param name="filterOptions">Tag paths to filter visible options (only branches containing these paths are shown).</param>
        /// <param name="displayMultiToggle">Whether to show toggle checkboxes for multi-selection mode.</param>
        public TagSelector(IEnumerable<string> currentSelections = null, IEnumerable<string> filterOptions = null, bool displayMultiToggle = false)
            : base("", displayMultiToggle, null, Array.Empty<string>())
        {
            m_selectedTags = currentSelections != null ? new HashSet<string>(currentSelections) : new HashSet<string>();
            m_tagFilter = filterOptions != null ? new HashSet<string>(filterOptions) : new HashSet<string>();
            m_shouldShowTagToggles = displayMultiToggle;
            
            if (!m_shouldShowTagToggles)
            {
                EnableSingleClickToSelect();
                SelectionConfirmed += OnSelectionConfirmed;
                SelectionTree.Selection.SupportsMultiSelect = false;
            }
            else
            {
                SelectionTree.Selection.SupportsMultiSelect = true;
            }
        }

        public override bool IsValidSelection(IEnumerable<string> collection)
        {
            if (m_shouldShowTagToggles) return false;
            return base.IsValidSelection(collection);
        }

        private void OnSelectionConfirmed(IEnumerable<string> selection)
        {
            var selectionStr = selection.FirstOrDefault();
            if (selectionStr != null)
            {
                OnTagSelected?.Invoke(new Tag(selectionStr));
            }
        }

        protected override void BuildSelectionTree(OdinMenuTree tree)
        {
            tree.Config.DrawSearchToolbar = true;
            tree.Config.SearchToolbarHeight = 24;
            tree.DefaultMenuStyle.IconSize = 16;

            var rootNodes = TagTreeProvider.GetTree();
            AddNodes(tree, rootNodes);

            foreach (var menuItem in tree.EnumerateTree())
            {
                if (m_selectedTags.Contains(menuItem.Value))
                {
                    menuItem.Select(true);
                }
                menuItem.OnDrawItem += OnDrawTagItem;
            }
        }

        private void OnDrawTagItem(OdinMenuItem tagMenuItem)
        {
            if (!m_shouldShowTagToggles) return;
    
            const float TOGGLE_SIZE = 14f;
            var rect = tagMenuItem.LabelRect;
            rect.x -= TOGGLE_SIZE;
            rect.width = TOGGLE_SIZE;
    
            bool isTagItemSelected = m_selectedTags.Contains(tagMenuItem.Value);
            GUI.enabled = tagMenuItem.IsSelectable;
    
            bool wasSelected = isTagItemSelected;
            bool isSelected = GUI.Toggle(rect, wasSelected, GUIContent.none);
    
            if (isSelected != wasSelected)
            {
                if (isSelected)
                {
                    m_selectedTags.Add(tagMenuItem.Value as string);
                    tagMenuItem.Select(true);
                    OnTagSelected?.Invoke(new Tag(tagMenuItem.Value as string));
                }
                else
                {
                    m_selectedTags.Remove(tagMenuItem.Value as string);    
                    tagMenuItem.Deselect();
                    OnTagDeselected?.Invoke(new Tag(tagMenuItem.Value as string));
                }
        
                Event.current.Use();
            }
    
            GUI.enabled = true;
        }

        private void AddNodes(OdinMenuTree tree, List<TagTreeProvider.TagNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (!ShouldShowNode(node)) continue;

                if (node.IsLeaf)
                {
                    string menuPath = node.FullPath.Replace('.', '/');
                    
                    var item = new OdinMenuItem(tree, node.Name, node.FullPath);
                    if (!string.IsNullOrEmpty(node.Description))
                    {
                        item.SearchString = $"{node.FullPath} {node.Description}";
                    }
                    
                    tree.AddMenuItemAtPath(menuPath.Contains('/') 
                        ? menuPath[..menuPath.LastIndexOf('/')] 
                        : "", item);
                }
                else
                {
                    string menuPath = node.FullPath.Replace('.', '/');
                    tree.Add(menuPath, node.FullPath);
                    
                    AddNodes(tree, node.Children);
                }
            }
        }

        /// <summary>
        /// Checks against the filter tags, if the given tag node is part of the filter tag hierarchy,
        /// we signal to show it. Otherwise hide it.
        /// </summary>
        private bool ShouldShowNode(TagTreeProvider.TagNode node)
        {
            if (m_tagFilter.Count == 0) return true;

            foreach (var filterReq in m_tagFilter)
            {
                if (Tag.MatchesSegment(node.FullPath, filterReq)) return true;
            }
            
            return false;
        }
    }
}
