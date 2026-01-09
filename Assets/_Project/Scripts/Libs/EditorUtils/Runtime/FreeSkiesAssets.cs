using System;
using UnityEditor;
using Object = UnityEngine.Object;

namespace FS.Assets
{
    #if UNITY_EDITOR
    public static class FreeSkiesAssets
    {
        /// <summary>
        /// Returns all sub-assets of a given unity asset object
        /// </summary>
        public static Object[] ListSubAssets(Object mainAsset, Type baseType = null)
        {
            // Get the path to the main asset
            string assetPath = AssetDatabase.GetAssetPath(mainAsset);
    
            // Load all assets at this path (main asset and all sub-assets)
            Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);

            // Filter out based on a base type
            if (baseType != null)
            {
                // Filter out any assets that are not of the specified type
                for (int i = subAssets.Length - 1; i >= 0; i--)
                {
                    if (!baseType.IsAssignableFrom(subAssets[i]?.GetType()))
                    {
                        ArrayUtility.RemoveAt(ref subAssets, i);
                    }
                }
            }

            return subAssets;
        }
    }
    #endif
}