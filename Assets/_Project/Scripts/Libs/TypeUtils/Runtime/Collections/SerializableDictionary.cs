using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS.Collections
{
    [Serializable]
    public class SerializableDictionary<K, V> : Dictionary<K, V>, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector] List<K> m_keys = new();
        [SerializeField, HideInInspector] List<V> m_values = new();
        
        public void OnBeforeSerialize()
        {
            m_keys.Clear();
            m_values.Clear();
            
            foreach (var kvp in this)
            {
                m_keys.Add(kvp.Key);
                m_values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();
            for (int i = 0; i < m_keys.Count && i < m_values.Count; i++)
            {
                this[m_keys[i]] = m_values[i];
            }
        }
    }
    
    [Serializable]
    public class SerializableListDictionary<K, V> : Dictionary<K, List<V>>, ISerializationCallbackReceiver
    {
        [Serializable]
        private class ListEntires
        {
            private ListEntires(List<V> values)
            {
                m_values = values;
            }
            
            // Implicit constructor to generate from List<V> values
            public static implicit operator ListEntires(List<V> values) => new(values);
            
            public List<V> m_values;
        }
        
        [SerializeField, HideInInspector] List<K> m_keys = new();
        [SerializeField, HideInInspector] List<ListEntires> m_values = new();
        
        public void OnBeforeSerialize()
        {
            m_keys.Clear();
            m_values.Clear();
            
            foreach (var kvp in this)
            {
                m_keys.Add(kvp.Key);
                m_values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();
            for (int i = 0; i < m_keys.Count && i < m_values.Count; i++)
            {
                this[m_keys[i]] = m_values[i].m_values;
            }
        }
    }
}