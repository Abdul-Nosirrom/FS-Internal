using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS.TagSystem
{
    /// <summary>
    /// A tag container where tags are reference-counted.
    /// Tags remain "present" as long as their count is greater than 0.
    /// Fires events on the 0→1 (tag added) and 1→0 (tag removed) transitions.
    ///
    /// <code>
    /// // Two abilities both grant speed boost
    /// stack.Add(Tag.Status.SpeedBoost);    // count: 1, fires OnTagAdded
    /// stack.Add(Tag.Status.SpeedBoost);    // count: 2, no event
    /// stack.Remove(Tag.Status.SpeedBoost); // count: 1, no event
    /// stack.Remove(Tag.Status.SpeedBoost); // count: 0, fires OnTagRemoved
    /// </code>
    /// </summary>
    [Serializable]
    public class TagStack : ITagSource
    {
        private Dictionary<Tag, int> m_tagCounts = new();
        private List<Tag> m_activeTags = new(); // Synced list for indexing of ITagSource

        /// <summary>Fires when a tag transitions from absent to present (0 → 1).</summary>
        public event Action<Tag> OnTagAdded;

        /// <summary>Fires when a tag transitions from present to absent (1 → 0).</summary>
        public event Action<Tag> OnTagRemoved;

        /// <summary>Fires on any count change. Provides the tag and its new count.</summary>
        public event Action<Tag, int> OnTagCountChanged;

        /// <summary>Enumerates all tags that currently have a non-zero count.</summary>
        public IEnumerable<Tag> Tags => m_tagCounts.Keys;
        
        /// <summary>
        /// Increments the reference count for a tag.
        /// Fires <see cref="OnTagAdded"/> if this is the first reference.
        /// </summary>
        public bool Add(Tag tag)
        {
            if (!tag.IsValid) return false;

            m_tagCounts.TryGetValue(tag, out int count);
            m_tagCounts[tag] = count + 1;

            OnTagCountChanged?.Invoke(tag, count + 1);

            if (count == 0)
            {
                // newly added tag, sync list
                m_activeTags.Add(tag);
                OnTagAdded?.Invoke(tag);
            }

            return true;
        }

        /// <summary>
        /// Decrements the reference count for a tag.
        /// Fires <see cref="OnTagRemoved"/> if the count reaches zero.
        /// </summary>
        public bool Remove(Tag tag)
        {
            if (!tag.IsValid) return false;
            if (!m_tagCounts.TryGetValue(tag, out int count) || count <= 0) return false;

            int newCount = count - 1;
            if (newCount <= 0)
            {
                m_activeTags.Remove(tag);
                m_tagCounts.Remove(tag);
                OnTagCountChanged?.Invoke(tag, 0);
                OnTagRemoved?.Invoke(tag);
            }
            else
            {
                m_tagCounts[tag] = newCount;
                OnTagCountChanged?.Invoke(tag, newCount);
            }

            return true;
        }

        /// <summary>Returns the current reference count for a tag.</summary>
        public int GetCount(Tag tag)
        {
            return m_tagCounts.GetValueOrDefault(tag, 0);
        }

        public void SetCount(Tag tag, int count)
        {
            if (!tag.IsValid) return;
    
            int prev = m_tagCounts.GetValueOrDefault(tag, 0);
            if (prev == count) return;
    
            if (count <= 0)
            {
                RemoveAll(tag);
                return;
            }
    
            m_tagCounts[tag] = count;
    
            // 0 → N transition
            if (prev == 0)
            {
                m_activeTags.Add(tag);
                OnTagCountChanged?.Invoke(tag, count);
                OnTagAdded?.Invoke(tag);
            }
            else
            {
                OnTagCountChanged?.Invoke(tag, count);
            }
        }
        
        #region ITagSource Implementation
        
        public int Count => m_tagCounts.Count;

        public Tag this[int idx] => m_activeTags[idx];

        public int this[Tag tag]
        {
            get => m_tagCounts.GetValueOrDefault(tag, 0);
            set => SetCount(tag, value);
        }
        
        /// <summary>Returns true if the tag has a count greater than 0.</summary>
        public bool Has(Tag tag)
        {
            return m_tagCounts.TryGetValue(tag, out int count) && count > 0;
        }
        
        #endregion

        /// <summary>
        /// Removes all references for a tag, regardless of count.
        /// Fires <see cref="OnTagRemoved"/> if the tag was present.
        /// </summary>
        public bool RemoveAll(Tag tag)
        {
            m_activeTags.Remove(tag);
            
            if (m_tagCounts.TryGetValue(tag, out int count) && count > 0)
            {
                m_tagCounts.Remove(tag);
                OnTagCountChanged?.Invoke(tag, 0);
                OnTagRemoved?.Invoke(tag);
                return true;
            }

            return false;
        }

        /// <summary>Clears all tags and fires <see cref="OnTagRemoved"/> for each.</summary>
        public void Clear()
        {
            m_activeTags.Clear();
            foreach (var kvp in m_tagCounts)
            {
                if (kvp.Value > 0)
                {
                    OnTagCountChanged?.Invoke(kvp.Key, 0);
                    OnTagRemoved?.Invoke(kvp.Key);
                }
            }
            m_tagCounts.Clear();
        }
    }
}
