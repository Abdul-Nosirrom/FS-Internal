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
        public void Add(Tag tag)
        {
            if (!tag.IsValid) return;

            m_tagCounts.TryGetValue(tag, out int count);
            m_tagCounts[tag] = count + 1;

            OnTagCountChanged?.Invoke(tag, count + 1);

            if (count == 0)
            {
                OnTagAdded?.Invoke(tag);
            }
        }

        /// <summary>
        /// Decrements the reference count for a tag.
        /// Fires <see cref="OnTagRemoved"/> if the count reaches zero.
        /// </summary>
        public void Remove(Tag tag)
        {
            if (!tag.IsValid) return;
            if (!m_tagCounts.TryGetValue(tag, out int count) || count <= 0) return;

            int newCount = count - 1;
            if (newCount <= 0)
            {
                m_tagCounts.Remove(tag);
                OnTagCountChanged?.Invoke(tag, 0);
                OnTagRemoved?.Invoke(tag);
            }
            else
            {
                m_tagCounts[tag] = newCount;
                OnTagCountChanged?.Invoke(tag, newCount);
            }
        }

        /// <summary>Returns true if the tag has a count greater than 0.</summary>
        public bool Has(Tag tag)
        {
            return m_tagCounts.TryGetValue(tag, out int count) && count > 0;
        }

        /// <summary>Returns the current reference count for a tag.</summary>
        public int GetCount(Tag tag)
        {
            return m_tagCounts.GetValueOrDefault(tag, 0);
        }

        /// <summary>Returns true if any active tag matches the given tag hierarchy.</summary>
        public bool HasAny(Tag parent)
        {
            foreach (var kvp in m_tagCounts)
            {
                if (kvp.Value > 0 && kvp.Key.MatchesTag(parent)) return true;
            }
            return false;
        }

        /// <summary>
        /// Removes all references for a tag, regardless of count.
        /// Fires <see cref="OnTagRemoved"/> if the tag was present.
        /// </summary>
        public void RemoveAll(Tag tag)
        {
            if (m_tagCounts.TryGetValue(tag, out int count) && count > 0)
            {
                m_tagCounts.Remove(tag);
                OnTagCountChanged?.Invoke(tag, 0);
                OnTagRemoved?.Invoke(tag);
            }
        }

        /// <summary>Clears all tags and fires <see cref="OnTagRemoved"/> for each.</summary>
        public void Clear()
        {
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
