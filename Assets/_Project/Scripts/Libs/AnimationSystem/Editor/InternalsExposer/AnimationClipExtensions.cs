using UnityEditor;
using UnityEngine;

namespace FS.Animation.Editor.Internals
{
    public static class AnimationClipExtensions
    {
        public static GameObject RetrieveBestFittingPreviewGameObject(this AnimationClip clip)
        {
            if (clip == null) return null;

            var type = AvatarPreview.GetAnimationType(clip);
            var target =
                AvatarPreview.FindBestFittingRenderableGameObjectFromModelAsset(clip, type);

            if (target == null)
            {
                return type == ModelImporterAnimationType.Generic ? 
                    (GameObject) EditorGUIUtility.Load("Avatar/DefaultGeneric.fbx") : 
                    (GameObject) EditorGUIUtility.Load("Avatar/DefaultAvatar.fbx");
                
            }

            return target;
        }
    }
}