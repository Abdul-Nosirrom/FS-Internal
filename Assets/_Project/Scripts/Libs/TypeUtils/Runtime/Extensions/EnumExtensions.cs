using System;

namespace FS.Extensions
{
    public static class EnumExtensions
    {
        /// <summary>
        /// Checks if enum [Value] has any of the flags in [flags] set
        /// </summary>
        public static bool HasAnyFlag<T>(this T value, T flags) where T : Enum
        {
            int valueInt = (int)(object)value;
            int flagsInt = (int)(object)flags;
            return (valueInt & flagsInt) != 0;
        }

        /// <summary>
        /// Returns only the flags from 'source' that are also present in 'mask'
        /// </summary>
        public static T KeepOnly<T>(this T source, T mask) where T : Enum => (T)(object)((int)(object)source & (int)(object)mask);
        
        /// <summary>
        /// Removes the flags from 'toRemove' out of 'source'
        /// </summary>
        public static T Remove<T>(this T source, T toRemove) where T : Enum => (T)(object)((int)(object)source & ~(int)(object)toRemove);
    }
}