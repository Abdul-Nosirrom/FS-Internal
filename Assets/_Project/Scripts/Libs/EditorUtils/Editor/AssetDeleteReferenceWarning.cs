using System;
using System.Linq;
using UnityEditor;

namespace FS.Editor
{
    public class AssetDeleteReferenceWarning : AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            if (assetPath.EndsWith(".meta")) return AssetDeleteResult.DidNotDelete; // Ignore meta files
            
            // Find all references to this asset, either within scenes, prefabs, or other assets
            string assetGUID = AssetDatabase.AssetPathToGUID(assetPath);
            string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();

            var referencingAssets = allAssetPaths
                .Where(path => path != assetPath)
                .Where(path =>
                {
                    string[] dependencies = AssetDatabase.GetDependencies(path, true);
                    return dependencies.Any(dep => AssetDatabase.AssetPathToGUID(dep) == assetGUID);
                }).ToList();
            
            
            if (referencingAssets.Count > 0)
            {
                string message = $"The asset '{assetPath}' is referenced by {referencingAssets.Count} other asset(s):\n\n";
                message += string.Join("\n", referencingAssets.Take(10));
            
                if (referencingAssets.Count > 10)
                    message += $"\n... and {referencingAssets.Count - 10} more";
            
                message += "\n\nAre you sure you want to delete it?";

                if (!EditorUtility.DisplayDialog("Delete Asset?", message, "Delete", "Cancel"))
                {
                    return AssetDeleteResult.FailedDelete;
                }
            }

            return AssetDeleteResult.DidNotDelete; // Let Unity handle the actual deletion
        }
    }
}