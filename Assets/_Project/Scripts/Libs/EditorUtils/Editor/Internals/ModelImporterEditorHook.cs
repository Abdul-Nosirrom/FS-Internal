using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace FS.Editor.Internals
{
    public abstract class AssetImporterEditorHook
    {
        public abstract string Name { get; }

        protected AssetImporter m_importer;
        public void SetImporter(AssetImporter importer) => m_importer = importer;
        
        
        // Helper methods for derived classes
        protected T GetSettings<T>(AssetImporter importer) where T : new()
        {
            return ModelImporterUserData.GetSettings<T>(importer);
        }
    
        protected void SetSettings<T>(AssetImporter importer, T settings)
        {
            ModelImporterUserData.SetSettings(importer, settings);
        }
    
        protected bool HasSettings<T>(AssetImporter importer)
        {
            return ModelImporterUserData.HasSettings<T>(importer);
        }
        
        public virtual void OnEnable() {}
        public virtual void OnDisable() {}
        
        public virtual void OnGUI() {}
        public virtual void ApplySettings() {}
        public virtual bool HasModified() => false;
    }

    internal class AssetImporterTabHook : BaseAssetImporterTabUI
    {
        public AssetImporterTabHook(AssetImporterEditor panelContainer) : base(panelContainer)
        {
        }
        
        private List<AssetImporterEditorHook> m_importerHooks = new List<AssetImporterEditorHook>();

        private AssetImporterEditorHook m_selectedHook;

        internal override void OnEnable()
        {
            // Get all types inheriting from AssetImporterFreeSkiesTab
            var importerHooks = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => !type.IsAbstract && typeof(AssetImporterEditorHook).IsAssignableFrom(type) &&
                               type != typeof(AssetImporterEditorHook));
            
            foreach (var hookType in importerHooks)
            {
                var hookInstance = (AssetImporterEditorHook) Activator.CreateInstance(hookType);
                hookInstance.SetImporter(panelContainer.target as AssetImporter);
                hookInstance.OnEnable();
                m_importerHooks.Add(hookInstance);
            }
            
            m_selectedHook = m_importerHooks.FirstOrDefault();
        }

        internal override void OnDisable()
        {
            base.OnDisable();
            foreach (var hook in m_importerHooks)
            {
                hook.OnDisable();
            }
        }

        public override void OnInspectorGUI()
        {
            // Dropdown selection for which hook to display
            string label = m_selectedHook != null ? m_selectedHook.Name : "Select Hook";
            if (EditorGUILayout.DropdownButton(new(label), FocusType.Keyboard))
            {
                Debug.LogError($"Number of hooks: {m_importerHooks.Count}");
                var menu = new GenericMenu();
                foreach (var hook in m_importerHooks)
                {
                    menu.AddItem(new GUIContent(hook.Name), hook == m_selectedHook, () =>
                    {
                        m_selectedHook = hook;
                    });
                }
                var rect = GUILayoutUtility.GetLastRect();
                menu.DropDown(rect);
            }
            
            if (m_selectedHook != null)
            {
                GUILayout.Space(10f);
                m_selectedHook.OnGUI();
            }
            else
            {
                EditorGUILayout.HelpBox("No hook selected.", MessageType.Info);
            }
        }

        internal override void PreApply()
        {
            foreach (var hook in m_importerHooks)
            {
                if (hook.HasModified())
                {
                    hook.ApplySettings();
                }
            }
            base.PreApply();
        }

        internal override bool HasModified() => m_importerHooks.Any(hook => hook != null && hook.HasModified());
    }
    
    [CanEditMultipleObjects]
    [CustomEditor(typeof (ModelImporter))]
    internal class ModelImporterEditorHook : ModelImporterEditor
    {

        private AssetImporterTabHook m_tabHook;
        
        public override void OnEnable()
        {
            m_tabHook = new AssetImporterTabHook(this);
            if (this.tabs == null)
            {
                this.tabs = new BaseAssetImporterTabUI[5]
                {
                    (BaseAssetImporterTabUI) new ModelImporterModelEditor((AssetImporterEditor) this),
                    (BaseAssetImporterTabUI) new ModelImporterRigEditor((AssetImporterEditor) this),
                    (BaseAssetImporterTabUI) new ModelImporterClipEditor((AssetImporterEditor) this),
                    (BaseAssetImporterTabUI) new ModelImporterMaterialEditor((AssetImporterEditor) this),
                    (BaseAssetImporterTabUI) m_tabHook
                };
                this.m_TabNames = new string[5]
                {
                    "Model",
                    "Rig",
                    "Animation",
                    "Materials",
                    "Custom"
                };
            }
            base.OnEnable();
        }

        public override bool HasModified() => m_tabHook.HasModified() || base.HasModified();
    }
}