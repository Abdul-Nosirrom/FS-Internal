#if UNITY_EDITOR 
using UnityEditor;
#endif
using UnityEngine;

namespace FS.Extensions
{
    public static class ScriptableObjectExtensions
    {
        public static bool IsRuntimeInstance(this ScriptableObject so)
        {
#if UNITY_EDITOR
            return !EditorUtility.IsPersistent(so);
#else 
            return so.GetInstanceID() < 0;
#endif
        }
    }
}