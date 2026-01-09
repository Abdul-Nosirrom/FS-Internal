using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FS.Editor.Internals
{
    /// <summary>
    /// Helper class to manage multiple independent settings in ModelImporter.userData
    /// </summary>
    public static class ModelImporterUserData
    {
        [System.Serializable]
        private class UserDataContainer
        {
            public List<Entry> entries = new List<Entry>();
            
            [System.Serializable]
            public class Entry
            {
                public string key;
                public string jsonData;
            }
        }
        
        public static T GetSettings<T>(AssetImporter importer) where T : new()
        {
            string key = typeof(T).FullName;
            
            if (string.IsNullOrEmpty(importer.userData))
                return new T();
            
            try
            {
                var container = JsonUtility.FromJson<UserDataContainer>(importer.userData);
                if (container == null || container.entries == null)
                    return new T();
                
                var entry = container.entries.Find(e => e.key == key);
                if (entry == null)
                    return new T();
                
                return JsonUtility.FromJson<T>(entry.jsonData);
            }
            catch
            {
                return new T();
            }
        }
        
        public static void SetSettings<T>(AssetImporter importer, T settings)
        {
            string key = typeof(T).FullName;
            UserDataContainer container;
            
            if (string.IsNullOrEmpty(importer.userData))
            {
                container = new UserDataContainer();
            }
            else
            {
                try
                {
                    container = JsonUtility.FromJson<UserDataContainer>(importer.userData);
                    if (container == null)
                        container = new UserDataContainer();
                }
                catch
                {
                    container = new UserDataContainer();
                }
            }
            
            if (container.entries == null)
                container.entries = new List<UserDataContainer.Entry>();
            
            var entry = container.entries.Find(e => e.key == key);
            var jsonData = JsonUtility.ToJson(settings);
            
            if (entry == null)
            {
                container.entries.Add(new UserDataContainer.Entry
                {
                    key = key,
                    jsonData = jsonData
                });
            }
            else
            {
                entry.jsonData = jsonData;
            }
            
            importer.userData = JsonUtility.ToJson(container);
        }
        
        public static bool HasSettings<T>(AssetImporter importer)
        {
            string key = typeof(T).FullName;
            
            if (string.IsNullOrEmpty(importer.userData))
                return false;
            
            try
            {
                var container = JsonUtility.FromJson<UserDataContainer>(importer.userData);
                return container?.entries?.Exists(e => e.key == key) ?? false;
            }
            catch
            {
                return false;
            }
        }
        
        public static void RemoveSettings<T>(AssetImporter importer)
        {
            string key = typeof(T).FullName;
            
            if (string.IsNullOrEmpty(importer.userData))
                return;
            
            try
            {
                var container = JsonUtility.FromJson<UserDataContainer>(importer.userData);
                if (container?.entries != null)
                {
                    container.entries.RemoveAll(e => e.key == key);
                    importer.userData = JsonUtility.ToJson(container);
                }
            }
            catch
            {
                // Ignore
            }
        }
    }
}