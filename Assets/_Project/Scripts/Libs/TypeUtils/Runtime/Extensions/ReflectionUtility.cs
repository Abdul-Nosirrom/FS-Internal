using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace FS.Extensions
{
    public static class ReflectionUtility
    {
        public static IEnumerable<Type> GetAllTypesWithAttribute<TAttribute>() where TAttribute : Attribute
        {
        #if UNITY_EDITOR
            return UnityEditor.TypeCache.GetTypesWithAttribute<TAttribute>();
        #else
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(assembly => assembly.GetTypesWithAttribute<TAttribute>());
        #endif
        }
        
        public static IEnumerable<Type> GetAllDerivedTypes<T>()
        {
        #if UNITY_EDITOR
            return UnityEditor.TypeCache.GetTypesDerivedFrom<T>().Where(type => !type.IsAbstract && type != typeof(T));
        #else
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => !type.IsAbstract && typeof(T).IsAssignableFrom(type) && type != typeof(T));
        #endif
        }

        /// <summary>
        /// Iterates through all fields of a given object
        /// </summary>
        /// <param name="fieldType">Optional, specify fields of a given type only</param>
        public static IEnumerable<FieldInfo> IterateFields(object obj, [CanBeNull] Type fieldType = null)
        {
            if (obj == null) yield break;
            
            var fields = obj.GetType().GetFields();
            foreach (var field in fields)
            {
                if (fieldType != null && !fieldType.IsAssignableFrom(field.FieldType)) continue;
                yield return field;
            }
        }
    }
}