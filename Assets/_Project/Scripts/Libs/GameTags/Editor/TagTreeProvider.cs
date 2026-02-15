using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace FS.TagSystem.Editor
{
    /// <summary>
    /// Provides a shared hierarchical view of all tags defined in .gameplayTags files.
    /// Used by property drawers, selectors, and validators to present and validate tag choices.
    /// </summary>
    public static class TagTreeProvider
    {
        public class TagNode
        {
            /// <summary>Short name of this node, e.g. "Start"</summary>
            public string Name;

            /// <summary>Full dot-separated path, e.g. "Animation.Skid.Start"</summary>
            public string FullPath;

            /// <summary>Description from the definition file.</summary>
            public string Description;

            /// <summary>True if this node has no children (is a selectable leaf tag).</summary>
            public bool IsLeaf;

            public List<TagNode> Children = new();
        }

        private static List<TagNode> s_rootNodes;
        private static Dictionary<string, TagNode> s_pathLookup;
        private static bool s_dirty = true;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            s_dirty = true;
            EditorApplication.projectChanged += () => s_dirty = true;
        }

        /// <summary>
        /// Returns the hierarchical tag tree, rebuilding from definition files if stale.
        /// </summary>
        public static List<TagNode> GetTree()
        {
            if (s_rootNodes == null || s_dirty)
            {
                Rebuild();
                s_dirty = false;
            }

            return s_rootNodes;
        }

        /// <summary>
        /// Returns a flat list of all leaf tag paths. Useful for simple dropdowns and validation.
        /// </summary>
        public static List<string> GetAllLeafPaths()
        {
            var paths = new List<string>();
            CollectLeafPaths(GetTree(), paths);
            return paths;
        }

        /// <summary>
        /// Returns a flat list of all tag paths, including branch paths.
        /// Useful for selectors that also allow selecting parent/category tags.
        /// </summary>
        public static List<string> GetAllPaths()
        {
            var paths = new List<string>();
            CollectAllPaths(GetTree(), paths);
            return paths;
        }

        /// <summary>
        /// Tries to find a node by its full path.
        /// </summary>
        public static bool TryGetNode(string fullPath, out TagNode node)
        {
            GetTree(); // Ensure built
            return s_pathLookup.TryGetValue(fullPath, out node);
        }

        /// <summary>
        /// Force rebuild the tree from disk. Called automatically by the importer after codegen.
        /// </summary>
        public static void Rebuild()
        {
            var merged = MergeAllDefinitionFiles();
            s_rootNodes = new List<TagNode>();
            s_pathLookup = new Dictionary<string, TagNode>();
            
            if (merged != null)
            {
                BuildNodes(merged, "", s_rootNodes);
            }
        }

        /// <summary>
        /// Finds and merges all .gameplayTags JSON files in the project.
        /// </summary>
        public static JObject MergeAllDefinitionFiles()
        {
            var files = Directory.GetFiles("Assets", "*.gameTags", SearchOption.AllDirectories);
            if (files.Length == 0) return null;

            var merged = new JObject();
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                var partial = JObject.Parse(text);
                merged.Merge(partial, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Union
                });
            }

            return merged;
        }

        #region Tree Building

        private static void BuildNodes(JObject obj, string parentPath, List<TagNode> nodes)
        {
            foreach (var property in obj.Properties())
            {
                string key = property.Name;
                if (key.StartsWith("_")) continue;

                string fullPath = string.IsNullOrEmpty(parentPath) ? key : $"{parentPath}.{key}";

                if (property.Value is JObject childObj)
                {
                    string desc = childObj.TryGetValue("_desc", out var descToken) ? descToken.ToString() : null;

                    var node = new TagNode
                    {
                        Name = key,
                        FullPath = fullPath,
                        Description = desc,
                        IsLeaf = false
                    };

                    BuildNodes(childObj, fullPath, node.Children);
                    nodes.Add(node);
                    s_pathLookup[fullPath] = node;
                }
                else if (property.Value.Type == JTokenType.String)
                {
                    var node = new TagNode
                    {
                        Name = key,
                        FullPath = fullPath,
                        Description = property.Value.ToString(),
                        IsLeaf = true
                    };

                    nodes.Add(node);
                    s_pathLookup[fullPath] = node;
                }
            }
        }

        #endregion

        #region Path Collection

        private static void CollectLeafPaths(List<TagNode> nodes, List<string> paths)
        {
            foreach (var node in nodes)
            {
                if (node.IsLeaf)
                {
                    paths.Add(node.FullPath);
                }

                CollectLeafPaths(node.Children, paths);
            }
        }

        private static void CollectAllPaths(List<TagNode> nodes, List<string> paths)
        {
            foreach (var node in nodes)
            {
                paths.Add(node.FullPath);
                CollectAllPaths(node.Children, paths);
            }
        }

        #endregion
    }
}
