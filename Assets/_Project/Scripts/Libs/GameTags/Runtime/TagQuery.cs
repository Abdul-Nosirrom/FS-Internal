using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS.TagSystem
{
    /// <summary>
    /// Interface for any object that can be queried for tag presence.
    /// Implemented by <see cref="TagSet"/> and <see cref="TagStack"/>.
    /// </summary>
    public interface ITagSource
    {
        /// <summary>
        /// Number of tags this ITagSource as
        /// </summary>
        public int Count { get; }
        
        /// <summary>
        /// Indexer into tags of this ITagSource (Tag implementation returns itself obv)
        /// </summary>
        public Tag this[int idx] { get; }
        
        /// <summary>Returns true if the exact tag is present.</summary>
        bool Has(Tag tag);
    }
    
    /// <summary>
    /// Extension methods for <see cref="ITagSource"/> providing shared query operations.
    /// Includes zero-allocation fixed overloads for common 2-4 tag checks,
    /// with a params fallback for larger sets.
    /// </summary>
    public static class TagSourceExtensions
    {
        public static bool HasNone<T>(this T source, ITagSource other) where T : ITagSource
        {
            for (int i = 0; i < other.Count; i++)
            {
                if (source.Has(other[i])) return false;
            }
            return true;
        }
        
        #region HasAny — Fixed Overloads (Zero Allocation)

        public static bool HasAny<T>(this T source, ITagSource other) where T : ITagSource
        {
            for (int i = 0; i < other.Count; i++)
            {
                if (source.Has(other[i])) return true;
            }
            return false;
        }
        
        /// <summary>Returns true if the source has any of the given tags.</summary>
        public static bool HasAny<T>(this T source, Tag a, Tag b) where T : ITagSource
            => source.Has(a) || source.Has(b);
        
        /// <summary>Returns true if the source has any of the given tags.</summary>
        public static bool HasAny<T>(this T source, Tag a, Tag b, Tag c) where T : ITagSource
            => source.Has(a) || source.Has(b) || source.Has(c);
        
        /// <summary>Returns true if the source has any of the given tags.</summary>
        public static bool HasAny<T>(this T source, Tag a, Tag b, Tag c, Tag d) where T : ITagSource
            => source.Has(a) || source.Has(b) || source.Has(c) || source.Has(d);
        
        /// <summary>Returns true if the source has any of the given tags. Allocates an array — prefer fixed overloads for 2-4 tags.</summary>
        public static bool HasAny<T>(this T source, params Tag[] tags) where T : ITagSource
        {
            foreach (var tag in tags)
            {
                if (source.Has(tag)) return true;
            }
            return false;
        }
        
        #endregion
        
        #region HasAll — Fixed Overloads (Zero Allocation)
        
        public static bool HasAll<T>(this T source, ITagSource other) where T : ITagSource
        {
            for (int i = 0; i < other.Count; i++)
            {
                if (!source.Has(other[i])) return false;
            }
            return true;
        }
        
        /// <summary>Returns true if the source has all of the given tags.</summary>
        public static bool HasAll<T>(this T source, Tag a, Tag b) where T : ITagSource
            => source.Has(a) && source.Has(b);
        
        /// <summary>Returns true if the source has all of the given tags.</summary>
        public static bool HasAll<T>(this T source, Tag a, Tag b, Tag c) where T : ITagSource
            => source.Has(a) && source.Has(b) && source.Has(c);
        
        /// <summary>Returns true if the source has all of the given tags.</summary>
        public static bool HasAll<T>(this T source, Tag a, Tag b, Tag c, Tag d) where T : ITagSource
            => source.Has(a) && source.Has(b) && source.Has(c) && source.Has(d);
        
        /// <summary>Returns true if the source has all of the given tags. Allocates an array — prefer fixed overloads for 2-4 tags.</summary>
        public static bool HasAll<T>(this T source, params Tag[] tags) where T : ITagSource
        {
            foreach (var tag in tags)
            {
                if (!source.Has(tag)) return false;
            }
            return true;
        }
        
        #endregion
        
        /// <summary>Evaluates a <see cref="TagQuery"/> against this tag source.</summary>
        public static bool Matches<T>(this T source, TagQuery query) where T : ITagSource => query.Matches(source);
    }
    
    /// <summary>
    /// Serializable query for evaluating an <see cref="ITagSource"/> against complex conditions.
    /// Supports require-all, require-any, and exclude rules.
    /// 
    /// <code>
    /// [SerializeField] private TagQuery m_abilityRequirements;
    /// 
    /// public bool CanExecute(TagSet ownerTags)
    /// {
    ///     return m_abilityRequirements.Matches(ownerTags);
    /// }
    /// </code>
    /// </summary>
    [Serializable]
    public class TagQuery
    {
        /// <summary>All of these tags must be present for the query to match.</summary>
        [SerializeField] private List<Tag> m_requireAll = new();

        /// <summary>At least one of these tags must be present (if any are specified).</summary>
        [SerializeField] private List<Tag> m_requireAny = new();

        /// <summary>None of these tags can be present for the query to match.</summary>
        [SerializeField] private List<Tag> m_exclude = new();

        public IReadOnlyList<Tag> RequireAll => m_requireAll;
        public IReadOnlyList<Tag> RequireAny => m_requireAny;
        public IReadOnlyList<Tag> Exclude => m_exclude;

        /// <summary>
        /// Returns true if the query has no conditions at all.
        /// An empty query matches everything.
        /// </summary>
        public bool IsEmpty => m_requireAll.Count == 0 && m_requireAny.Count == 0 && m_exclude.Count == 0;

        /// <summary>
        /// Evaluates this query against a tag source.
        /// Returns true if all conditions are satisfied:
        /// all require-all tags are present, at least one require-any tag is present (if any specified),
        /// and no excluded tags are present.
        /// </summary>
        public bool Matches(ITagSource source)
        {
            if (source == null) return IsEmpty;

            // All required tags must be present
            foreach (var tag in m_requireAll)
            {
                if (!source.Has(tag)) return false;
            }

            // At least one of the "any" tags must be present (skip if none specified)
            if (m_requireAny.Count > 0)
            {
                bool bAnyFound = false;
                foreach (var tag in m_requireAny)
                {
                    if (source.Has(tag))
                    {
                        bAnyFound = true;
                        break;
                    }
                }

                if (!bAnyFound) return false;
            }

            // None of the excluded tags can be present
            foreach (var tag in m_exclude)
            {
                if (source.Has(tag)) return false;
            }

            return true;
        }
    }
}
