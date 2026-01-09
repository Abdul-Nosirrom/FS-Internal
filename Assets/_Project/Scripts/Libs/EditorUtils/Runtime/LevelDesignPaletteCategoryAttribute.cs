using System;

namespace FS.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class LevelDesignCategoryAttribute : Attribute
    {
        public string CategoryName { get; private set; }
        
        public LevelDesignCategoryAttribute(string categoryPath)
        {
            CategoryName = categoryPath;
        }
    }
}