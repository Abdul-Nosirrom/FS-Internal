using System;
using System.Linq;
using UnityEngine;

namespace FS.Extensions
{
    public static class TypeExtensions
    {
        public static bool IsMonoBehavior(this Type type)
        {
            // NOTE: Reversing the order here gives different results, the below is correct
            return typeof(MonoBehaviour).IsAssignableFrom(type);
        }

        /// <summary>
        /// Determines whether the specified type is a leaf-most type.
        /// A type is considered leaf-most if it is sealed or if there are no
        /// non-abstract derived types assignable from it in the current application domain.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> to evaluate.</param>
        /// <returns>Returns true if the specified type is the leaf-most type; otherwise, false.</returns>
        public static bool IsLeafMost(this Type type)
        {
            // Obvious case of being the leaf-most (structs are sealed but not flagged as such, though we detect them w/ IsValueType)
            if (type.IsSealed || type.IsValueType) return true;

            // Interfaces are never leaf-most
            if (type.IsInterface) return false;
            
            // Check if there are any non-abstract derived types assignable from the specified type
            var subTypes = AppDomain.CurrentDomain
                                            .GetAssemblies()
                                            .SelectMany(assembly => assembly.GetTypes())
                                            .Where(derivedType => !derivedType.IsAbstract 
                                                                  && type.IsAssignableFrom(derivedType) 
                                                                  && derivedType != type);

            return !subTypes.Any();
        }
    }
}