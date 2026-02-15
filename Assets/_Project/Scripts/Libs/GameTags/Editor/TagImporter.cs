using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.VersionControl;
using UnityEngine;

namespace FS.TagSystem.Editor
{
    /// <summary>
    /// Custom importer for .gameTags files. On import, merges all .gameTags definition files
    /// in the project and regenerates a partial <see cref="Tag"/> struct with strongly-typed constants.
    /// 
    /// <para>Branch nodes are generated as nested classes with implicit conversion to <see cref="Tag"/>,
    /// allowing natural syntax like <c>tag.MatchesTag(Tag.Animation.Skid)</c>.</para>
    /// </summary>
    [ScriptedImporter(1, "gameTags")]
    public class TagImporter : ScriptedImporter
    {
        private const string k_outputPath = "Assets/_Project/Scripts/Generated/Runtime/TagDefinitions.generated.cs";
        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            // Import as a TextAsset so the file shows up in the project window
            var jsonText = File.ReadAllText(ctx.assetPath);
            var textAsset = new TextAsset(jsonText);
            ctx.AddObjectToAsset("main", textAsset);
            ctx.SetMainObject(textAsset);

            // Merge all definition files and regenerate
            var merged = TagTreeProvider.MergeAllDefinitionFiles();
            if (merged == null)
            {
                Debug.LogWarning("[TagSystem] No .gameTags files found. Skipping codegen.");
                return;
            }

            var code = GenerateCode(merged);

            var outputDir = Path.GetDirectoryName(k_outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Version control auto-checkout
            if (Provider.isActive && File.Exists(k_outputPath))
            {
                var task = Provider.Checkout(k_outputPath, CheckoutMode.Asset);
                task.Wait();
            }

            File.WriteAllText(k_outputPath, code);
            
            // Rebuild the tree provider cache
            TagTreeProvider.Rebuild();
            
            // Trigger recompile
            AssetDatabase.ImportAsset(k_outputPath);
        }

        #region Code Generation

        private string GenerateCode(JObject root)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// ============================================================================");
            sb.AppendLine("// AUTO-GENERATED FILE — Do not edit manually.");
            sb.AppendLine("// Generated from .gameTags definition files by TagImporter.");
            sb.AppendLine("// ============================================================================");
            sb.AppendLine();
            sb.AppendLine("namespace FS.TagSystem");
            sb.AppendLine("{");
            sb.AppendLine("    public partial struct Tag");
            sb.AppendLine("    {");

            WriteNode(sb, root, parentPath: "", indent: 2);

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private void WriteNode(StringBuilder sb, JObject node, string parentPath, int indent)
        {
            var pad = new string(' ', indent * 4);

            foreach (var property in node.Properties())
            {
                string key = property.Name;
                if (key.StartsWith("_")) continue;

                string fullPath = string.IsNullOrEmpty(parentPath) ? key : $"{parentPath}.{key}";

                if (property.Value is JObject childObj)
                {
                    string typeName = $"{key}_";

                    // Branch description
                    if (childObj.TryGetValue("_desc", out var branchDesc))
                    {
                        sb.AppendLine($"{pad}/// <summary>{EscapeXml(branchDesc.ToString())}</summary>");
                    }

                    sb.AppendLine($"{pad}public static readonly {typeName} {key} = new();");
                    sb.AppendLine();

                    // Class definition
                    if (childObj.TryGetValue("_desc", out var classDesc))
                    {
                        sb.AppendLine($"{pad}/// <summary>{EscapeXml(classDesc.ToString())}</summary>");
                    }

                    sb.AppendLine($"{pad}public class {typeName}");
                    sb.AppendLine($"{pad}{{");
                    sb.AppendLine($"{pad}    public const string Path = \"{fullPath}\";");
                    sb.AppendLine($"{pad}    public static implicit operator Tag({typeName} _) => new(\"{fullPath}\");");
                    sb.AppendLine();

                    WriteNode(sb, childObj, fullPath, indent + 1);

                    sb.AppendLine($"{pad}}}");
                    sb.AppendLine();
                }
                else if (property.Value.Type == JTokenType.String)
                {
                    // Leaf — generate a readonly Tag constant
                    string desc = property.Value.ToString();
                    sb.AppendLine($"{pad}/// <summary>{EscapeXml(desc)}</summary>");
                    sb.AppendLine($"{pad}public static readonly Tag {key} = new(\"{fullPath}\");");
                    sb.AppendLine($"{pad}public const string {key}_Path = \"{fullPath}\";");
                    sb.AppendLine();
                }
            }
        }

        private static string EscapeXml(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        #endregion
    }
}
