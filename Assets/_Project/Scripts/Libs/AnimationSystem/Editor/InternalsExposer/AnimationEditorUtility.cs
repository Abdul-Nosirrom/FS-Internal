using Animancer;
using UnityEditor;
using UnityEngine;

namespace FS.Animation.Editor
{
    public static class AnimationEditorUtility
    {
        /// <summary>
        /// Instantiate a GameObject prefab for previewing animations in the editor. Based off of the internal EditorUtility.InstantiateForAnimatorPreview method
        /// </summary>
        public static AnimancerComponent InstantiatePreview(GameObject prefab)
        {
            // TODO: If we ever have any dynamics, might be good to keep those components if they're previewable
            GameObject animPreview = EditorUtility.InstantiateForAnimatorPreview(prefab);
            animPreview.GetComponent<Animator>().enabled = true;
            if (animPreview.TryGetComponent<AnimancerComponent>(out var animancer))
            {
                // If the prefab already has an AnimancerComponent, just return it
                return animancer;
            }
            return animPreview.AddComponent<AnimancerComponent>();
        }

        public static GUIStyle toolbarLabel => EditorStyles.toolbarLabel;
    }
}