using UnityEngine;

namespace FS.Extensions
{
    public static class TransformExtensions
    {
        public static Transform FindChildRecursive(this Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform result = child.FindChildRecursive(childName);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}