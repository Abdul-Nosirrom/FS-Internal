using System;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using FS.UtilityEditor;
using UnityEditor;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.Validation;
using Sirenix.Utilities.Editor;
#endif

namespace FS.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class ExperimentalAttribute : Attribute
    {}
    
#if UNITY_EDITOR
    
    public class ExperimentalAttributeWarning<T> : AttributeValidator<ExperimentalAttribute, T> where T : MonoBehaviour
    {
        protected override void Validate(ValidationResult result)
        {
            result.ResultType = ValidationResultType.Warning;
            result.Message = "This is an experimental feature and may change or be removed in future updates.";
        }
    }
    
    [InitializeOnLoad]
    public static class ExperimentalHeaderDrawer
    {
        static ExperimentalHeaderDrawer()
        {
            ComponentTitlebarGUI.OnTitlebarGUI += DrawExperimentalWarning;
        }

        private static void DrawExperimentalWarning(Rect rect, Object obj)
        {
            if (obj is MonoBehaviour behaviour)
            {
                if (behaviour.GetType().GetCustomAttribute<ExperimentalAttribute>(true) != null)
                {
                    EditorGUILayout.HelpBox(
                        "EXPERIMENTAL: This component may change or be removed in future updates. Changes may break existing functionality and require reconfiguration.",
                        MessageType.Warning
                    );
                }
            }
        }
    }
    
#endif   
}