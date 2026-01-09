using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using UnityEngine;
#endif

namespace FS.GameplayActions
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class RuntimeDataAttribute : Attribute
    {}
}

#if UNITY_EDITOR
namespace FS.GameplayActions.Editor
{
    public class RuntimeAttributeProcessor<T> : OdinAttributeProcessor<T> where T : MonoBehaviour
    {
        public override bool CanProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member)
        {
            var runtimeDataAttr = member.GetCustomAttribute<RuntimeDataAttribute>();
            return runtimeDataAttr != null;
        }

        public override void ProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member, List<Attribute> attributes)
        {
            if (!Application.isPlaying)
            {
                attributes.Add(new HideInInspector());
                return;
            }
            
            // Add foldout group and readonly
            attributes.Add(new FoldoutGroupAttribute("Runtime Data") { Expanded = false });
            attributes.Add(new ShowInInspectorAttribute());
            attributes.Add(new ReadOnlyAttribute());
        }
    }
}
#endif