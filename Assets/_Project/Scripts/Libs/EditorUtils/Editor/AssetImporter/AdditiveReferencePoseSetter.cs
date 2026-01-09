using System;
using FS.Editor.Internals;
using UnityEditor;
using UnityEngine;

namespace FS.Editor
{
    public class AdditiveReferencePoseSetter : AssetPostprocessor
    {
        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!ModelImporterUserData.HasSettings<AdditiveReferencePoseSetterEditorHook.Settings>(assetImporter))
                return;
            var settings = ModelImporterUserData.GetSettings<AdditiveReferencePoseSetterEditorHook.Settings>(assetImporter);
            var refClip = AssetDatabase.LoadAssetByGUID<AnimationClip>(new (settings.referencePoseClipGUID));
            AnimationUtility.SetAdditiveReferencePose(clip, refClip, 0f);
        }
    }

    public class AdditiveReferencePoseSetterEditorHook : AssetImporterEditorHook
    {
        public override string Name => "Additive Reference Pose";

        private AnimationClip m_refClip;
        
        [Serializable]
        public class Settings
        {
            public string referencePoseClipGUID = null;
        }

        public override void OnEnable()
        {
            var settings = GetSettings<Settings>(m_importer);
            if (string.IsNullOrEmpty(settings.referencePoseClipGUID))
                return;
            m_refClip = AssetDatabase.LoadAssetByGUID<AnimationClip>(new GUID(settings.referencePoseClipGUID));
        }

        public override void OnGUI()
        {
            // GUI code to set additive reference pose clip
            m_refClip = (AnimationClip) EditorGUILayout.ObjectField("Additive Reference Pose Clip", m_refClip, typeof(AnimationClip), false);
        }

        public override void ApplySettings()
        {
            var settings = new Settings
            {
                referencePoseClipGUID = m_refClip == null ? null : AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(m_refClip)).ToString()
            };
            SetSettings(m_importer, settings);
        }

        public override bool HasModified()
        {
            var settings = GetSettings<Settings>(m_importer);
            if (m_refClip == null && string.IsNullOrEmpty(settings.referencePoseClipGUID))
                return false;
            return ModelImporterUserData.GetSettings<Settings>(m_importer).referencePoseClipGUID != AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(m_refClip)).ToString();
        }
    }
}