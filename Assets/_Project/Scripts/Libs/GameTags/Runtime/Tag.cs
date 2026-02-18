using System;
using UnityEngine;

namespace FS.TagSystem
{
    /// <summary>
    /// Lightweight tag identifier using interned strings for fast equality comparison.
    /// Supports hierarchical matching via dot-separated paths (e.g. "Animation.Skid.Start").
    /// 
    /// <para>Generated tag constants use implicit conversion from branch types,
    /// so both exact and hierarchical usage is natural:</para>
    /// <code>
    /// // Exact match
    /// if (tag == Tag.Animation.Skid.Start) { ... }
    /// 
    /// // Hierarchical — "any skid tag?"
    /// if (tag.MatchesTag(Tag.Animation.Skid)) { ... }
    /// </code>
    /// </summary>
    [Serializable]
    public partial struct Tag : ITagSource, IEquatable<Tag>, ISerializationCallbackReceiver
    {
        [SerializeField] private string m_value;

        public string Value => m_value ?? string.Empty;
        public bool IsValid => !string.IsNullOrEmpty(m_value);

        public Tag(string value) // DO NOT USE EXCEPT INTERNALLY! USE GENERATED HARD TYPES INSTEAD
        {
            m_value = value != null ? string.Intern(value) : null;
        }
        
        /// <summary>Implicit conversion to string, returns the tag's full path value.</summary>
        public static implicit operator string(Tag tag) => tag.Value;

        #region ITagSource

        public int Count => 1;
        
        public Tag this[int idx] => idx != 0 ? throw new ArgumentOutOfRangeException(nameof(idx)) : this;

        public int this[Tag tag]
        {
            get => this.Equals(tag) ? 1 : 0;
            set => throw new NotSupportedException("ITagSource Count Setter Not Available On Individual Tag Structs");
        }

        public bool Has(Tag tag) => this.Equals(tag);

        public bool Add(Tag tag) => throw new NotSupportedException("ITagSource Implementation Not Fully Available On 'Tag' Due To Boxing Copies");
        public bool Remove(Tag tag) => throw new NotSupportedException("ITagSource Implementation Not Fully Available On 'Tag' Due To Boxing Copies");
        public bool RemoveAll(Tag tag) => Remove(tag);
        
        // WARNING: Tag is a struct so gets copied over, binding to these isn't really a guarantee of things working as expected should not use, here for interface implementation
        public event Action<Tag> OnTagAdded
        {
            add => throw new NotSupportedException("ITagSource Events Unsupported on 'Tag' struct");
            remove => throw new NotSupportedException("ITagSource Events Unsupported on 'Tag' struct");
        }

        public event Action<Tag> OnTagRemoved
        {
            add => throw new NotSupportedException("ITagSource Events Unsupported on 'Tag' struct");
            remove => throw new NotSupportedException("ITagSource Events Unsupported on 'Tag' struct");
        }

        public event Action<Tag, int> OnTagCountChanged
        {
            add => throw new NotSupportedException("ITagSource Events Unsupported on 'Tag' struct");
            remove => throw new NotSupportedException("ITagSource Events Unsupported on 'Tag' struct");
        }

        #endregion

        #region Hierarchical Matching

        /// <summary>
        /// Returns true if this tag contains the given tag as a dot-boundary segment.
        /// Checks if the given tag appears as an ancestor, descendant, or equal segment.
        /// <code>
        /// "Animation.Skid.End".MatchesTag("Skid")          == true
        /// "Animation.Skid.End".MatchesTag("Animation")      == true
        /// "Animation.SkidExtra".MatchesTag("Skid")           == false  (no dot boundary)
        /// </code>
        /// </summary>
        public bool MatchesHierarchy(Tag other)
        {
            if (!other.IsValid) return false;

            var val = Value;
            var pval = other.Value;

            int index = val.IndexOf(pval, StringComparison.Ordinal);
            while (index >= 0)
            {
                bool leftOk = index == 0 || val[index - 1] == '.';
                int end = index + pval.Length;
                bool rightOk = end == val.Length || val[end] == '.';

                if (leftOk && rightOk) return true;

                index = val.IndexOf(pval, index + 1, StringComparison.Ordinal);
            }

            return false;
        }

        /// <summary>
        /// Returns the parent path. "Animation.Skid.Start" → "Animation.Skid".
        /// Returns <see cref="None"/> if there is no parent.
        /// </summary>
        public Tag Parent
        {
            get
            {
                int lastDot = Value.LastIndexOf('.');
                return lastDot > 0 ? new Tag(Value[..lastDot]) : None;
            }
        }

        /// <summary>
        /// Returns just the leaf segment. "Animation.Skid.Start" → "Start".
        /// </summary>
        public string ShortName
        {
            get
            {
                int lastDot = Value.LastIndexOf('.');
                return lastDot >= 0 ? Value[(lastDot + 1)..] : Value;
            }
        }

        #endregion

        #region Equality

        public bool Equals(Tag other) =>
            ReferenceEquals(m_value, other.m_value) || (m_value != null && other.m_value != null && string.Equals(m_value, other.m_value, StringComparison.Ordinal));
        
        public override bool Equals(object obj) => obj is Tag other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;

        public static bool operator ==(Tag a, Tag b) => a.Equals(b);
        public static bool operator !=(Tag a, Tag b) => !a.Equals(b);

        #endregion

        public static readonly Tag None = default;
        
        #region Serialization
        
        public void OnBeforeSerialize() {}

        public void OnAfterDeserialize()
        {    
            // Re-intern after Unity deserializes so ReferenceEquals works
            if (m_value != null)
            {
                m_value = string.Intern(m_value);
            }
        }
        
        #endregion
        
        /// <summary>
        /// Utility function to test string tags
        /// </summary>
        public static bool MatchesSegment(string fullPath, string segment)
        {
            int index = fullPath.IndexOf(segment, StringComparison.Ordinal);
            while (index >= 0)
            {
                bool leftOk  = index == 0 || fullPath[index - 1] == '.';
                int end      = index + segment.Length;
                bool rightOk = end == fullPath.Length || fullPath[end] == '.';

                if (leftOk && rightOk) return true;
                index = fullPath.IndexOf(segment, index + 1, StringComparison.Ordinal);
            }
            return false;
        }
    }

    /// <summary>
    /// Attribute to filter which tag branches are shown in the tag selector dropdown.
    /// Accepts branch type paths to restrict selection to specific hierarchies.
    /// <code>
    /// [TagFilter(typeof(Tag.Animation_))]
    /// [SerializeField] private TagSet m_animationTags;
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TagFilterAttribute : Attribute
    {
        public readonly string[] FilterTags;
        public TagFilterAttribute(params string[] tags)
        {
            FilterTags = tags;
        }
    }
}
