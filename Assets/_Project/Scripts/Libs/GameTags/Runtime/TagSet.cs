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
        
        public int Count => m_tags.Count;
        
        /// <summary>Ordered read-only view of all tags in this set.</summary>
        public IReadOnlyList<Tag> Tags => m_serializedTags;

        public static implicit operator TagSet(Tag tag)
        {
            TagSet set = new();
            set.Add(tag);
            return set;
        }

        public Tag this[int idx] => Tags[idx];

        #region Single Tag Operations

        /// <summary>Adds a tag if not already present.</summary>
        public void Add(Tag tag)
        {
            if (m_tags.Add(tag)) m_serializedTags.Add(tag);
        }

        /// <summary>Removes a tag from the set.</summary>
        public void Remove(Tag tag)
        {
            if (m_tags.Remove(tag)) m_serializedTags.Remove(tag);
        }

        /// <summary>Returns true if the exact tag is present.</summary>
        public bool Has(Tag tag) => m_tags.Contains(tag);

        /// <summary>
        /// Returns true if any tag in this set matches the given tag hierarchy.
        /// e.g. if set has "Animation.Skid.Start", HasAny(Tag.Animation.Skid) returns true.
        /// </summary>
        public bool HasAny(Tag parent)
        {
            foreach (var tag in m_tags)
            {
                if (tag.MatchesTag(parent)) return true;
            }

            return false;
        }

        #endregion

        #region Set vs Set Operations

        /// <summary>Returns true if this set has ALL tags from the other set (exact match).</summary>
        public bool HasAll(TagSet other)
        {
            foreach (var tag in other.m_tags)
            {
                if (!Has(tag)) return false;
            }

            return true;
        }

        /// <summary>Returns true if this set shares ANY exact tag with the other.</summary>
        public bool HasAny(TagSet other)
        {
            foreach (var tag in other.m_tags)
            {
                if (Has(tag)) return true;
            }

            return false;
        }

        /// <summary>Returns true if this set has NONE of the tags from the other.</summary>
        public bool HasNone(TagSet other)
        {
            foreach (var tag in other.m_tags)
            {
                if (Has(tag)) return false;
            }

            return true;
        }

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
            m_serializedTags.Clear();
            m_serializedTags.AddRange(m_tags);
        }

        public void OnAfterDeserialize()
        {
            m_tags = new HashSet<Tag>(m_serializedTags);
        }
        
        #endregion
    }
}
