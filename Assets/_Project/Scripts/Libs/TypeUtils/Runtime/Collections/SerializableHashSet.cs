using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS.Collections
{
    [Serializable]
    public class SerializableHashSet<T> : HashSet<T>, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector] List<T> m_elements = new();
        
        public void OnBeforeSerialize()
        {
            m_elements.Clear();
            
            foreach (var val in this)
            {
                m_elements.Add(val);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();
            foreach (var val in m_elements)
            {
                Add(val);
            }
        }
    }
}