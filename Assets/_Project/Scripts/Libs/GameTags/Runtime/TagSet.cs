using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS.TagSystem
{
    /// <summary>
    /// Serializable collection of <see cref="Tag"/>s with set-based query operations.
    /// Backed by a HashSet for O(1) lookups, with a serialized list for Unity persistence.
    /// Used for tagging entities, defining requirements, and checking conditions.
    /// 
    /// <code>
    /// // Add/remove tags
    /// container.Add(Tag.Status.Grounded);
    /// container.Remove(Tag.Status.Airborne);
    /// 
    /// // Exact check
    /// if (container.Has(Tag.Status.Stunned)) { ... }
    /// 
    /// // Hierarchical check — "has any status tag?"
    /// if (container.HasAny(Tag.Status)) { ... }
    /// 
    /// // Multi-tag checks (via ITagSource extensions)
    /// if (container.HasAny(Tag.Status.Grounded, Tag.Status.Airborne)) { ... }
    /// if (container.HasAll(Tag.Status.Grounded, Tag.Combat.HitFrame)) { ... }
    /// 
    /// // Container vs container
    /// if (container.HasAll(requiredTags)) { ... }
    /// </code>
    /// </summary>
    [Serializable]
    public class TagSet : ITagSource, ISerializationCallbackReceiver
    {
        [SerializeField] private List<Tag> m_serializedTags = new();
        private HashSet<Tag> m_tags = new();

        public event Action<Tag> OnTagAdded;
        public event Action<Tag> OnTagRemoved;
        public event Action<Tag, int> OnTagCountChanged;

        public int Count => m_tags.Count;
        
        /// <summary>Ordered read-only view of all tags in this set.</summary>
        public IReadOnlyList<Tag> Tags => m_serializedTags;

        // public static implicit operator TagSet(Tag tag)
        // {
        //     TagSet set = new();
        //     set.Add(tag);
        //     return set;
        // }

        public Tag this[int idx] => Tags[idx];

        public int this[Tag tag]
        {
            get => Has(tag) ? 1 : 0;
            set
            {
                if (value > 0) Add(tag);
                else Remove(tag);
            }
        }

        #region Single Tag Operations

        /// <summary>Adds a tag if not already present.</summary>
        public bool Add(Tag tag)
        {
            bool result = m_tags.Add(tag);
            if (result)
            {
                m_serializedTags.Add(tag);
                OnTagAdded?.Invoke(tag);
                OnTagCountChanged?.Invoke(tag, 1);
            }
            return result;
        }

        /// <summary>Removes a tag from the set.</summary>
        public bool Remove(Tag tag)
        {
            bool result = m_tags.Remove(tag);
            if (result)
            {
                m_serializedTags.Remove(tag);
                OnTagRemoved?.Invoke(tag);
                OnTagCountChanged?.Invoke(tag, 0);
            }
            return result;
        }
        public bool RemoveAll(Tag tag) => Remove(tag);

        /// <summary>Returns true if the exact tag is present.</summary>
        public bool Has(Tag tag) => m_tags.Contains(tag);

        #endregion

        /// <summary>Removes all tags from the set.</summary>
        public void Clear()
        {
            m_tags.Clear();
            m_serializedTags.Clear();
        }
        
        #region Serialization
        
        public void OnBeforeSerialize()
        {
            Debug.Assert(m_tags.Count == m_serializedTags.Count, 
                "[TagSet] Tag/list desync detected");
        }

        public void OnAfterDeserialize()
        {
            m_tags = new HashSet<Tag>(m_serializedTags);
        }
        
        #endregion
    }
}
