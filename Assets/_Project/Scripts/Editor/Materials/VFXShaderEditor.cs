using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FS.Editor
{
    // TODO: Tooltip decorator doesnt work here for some reason
    public class VFXShaderEditor : ShaderGUI
    {
        private const string k_tabGroupDecorator = "SectionHeader";

        private struct ShaderTab
        {
            public string Name { get; private set; }
            public List<MaterialProperty> Properties { get; private set; }
            public int PropertyCount => Properties.Count;
            
            public void AddProperty(MaterialProperty prop) => Properties.Add(prop);
            public static ShaderTab Create(string name) => new ShaderTab {Name = name, Properties = new List<MaterialProperty>()};
            public static ShaderTab Create(string name, params MaterialProperty[] properties) => new ShaderTab {Name = name, Properties = new List<MaterialProperty>(properties)};
        }

        private GUITabGroup m_tabGroupDrawer;
        private List<GUITabPage> m_tabPages = new List<GUITabPage>();
        
        // to handle refreshing the initialization only when the shader changes
        private Shader m_cachedShader;
        List<ShaderTab> m_shaderTabs = new List<ShaderTab>();

        private bool m_showAsTabs = true;
        
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            FreeSkiesEditor.ToggleButton("Show as Tabs", ref m_showAsTabs);
            if (!m_showAsTabs)
            {
                base.OnGUI(materialEditor, properties);
                return;
            }
            
            Material mat = (Material)materialEditor.target;
            Shader shader = mat.shader;

            materialEditor.SetDefaultGUIWidths();
            EditorGUIUtility.labelWidth -= 32; // Make fields wider

            // Initialization
            if (shader != m_cachedShader)
            {
                Debug.Log($"[VFX Editor] Tab Group Being Regenerated");
                m_cachedShader = shader;
                // Search for tab-groups as designated by [SectionHeader()] decorators
                m_shaderTabs = new List<ShaderTab> { ShaderTab.Create("General") };
                for (int p = 0; p < properties.Length; p++)
                {
                    if (IsStartOfTabGroup(shader, p, out var tabName))
                    {
                        m_shaderTabs.Add(ShaderTab.Create(tabName, properties[p]));
                    }
                    else m_shaderTabs[^1].AddProperty(properties[p]);
                }
                
                // remove first tab if it has no properties
                if (m_shaderTabs[0].PropertyCount == 0) m_shaderTabs.RemoveAt(0);

                CreateTabGroups();
            }

            if (m_shaderTabs.Count == 1)
            {
                base.OnGUI(materialEditor, properties);
                return;
            } 

            // Draw
            {
                m_tabGroupDrawer.BeginGroup();
                for (int t = 0; t < m_tabPages.Count; t++)
                {
                    var tab = m_tabPages[t];
                    if (tab.BeginPage())
                    {
                        var shaderTab = m_shaderTabs[t];
                        foreach (var prop in shaderTab.Properties)
                        {
                            if ((prop.propertyFlags & ShaderPropertyFlags.HideInInspector) == 0)
                                materialEditor.ShaderProperty(prop, prop.displayName);
                        }
                    }
                    tab.EndPage();
                }
                m_tabGroupDrawer.EndGroup();
            }
            
            // Draw some default settings for queue range and stuff
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            if (SupportedRenderingFeatures.active.editableMaterialRenderQueue)
                materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();
            materialEditor.DoubleSidedGIField();
        }

        private void CreateTabGroups()
        {
            m_tabGroupDrawer = SirenixEditorGUI.CreateAnimatedTabGroup(m_cachedShader);
            m_tabGroupDrawer.AnimationSpeed = 100;
            m_tabPages.Clear();

            foreach (var tab in m_shaderTabs)
            {
                m_tabPages.Add(m_tabGroupDrawer.RegisterTab(tab.Name));
            }
        }

        private bool IsStartOfTabGroup(Shader shader, int propIdx, out string tabName)
        {
            tabName = null;
            foreach (var attr in shader.GetPropertyAttributes(propIdx))
            {
                if (attr.Contains(k_tabGroupDecorator))
                {
                    // Fetch the tab name in format k_tabGroupDecorator(NAME)
                    tabName = attr.Substring(k_tabGroupDecorator.Length + 1, attr.Length - k_tabGroupDecorator.Length - 2);
                    return true;
                }
            }

            return false;
        }
    }
}