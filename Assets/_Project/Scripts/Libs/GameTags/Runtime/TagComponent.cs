using System.Runtime.CompilerServices;
using UnityEngine;

namespace FS.TagSystem
{
    public interface ITagProvider
    {
        public TagSet Tags { get; }
    }
    
    /// <summary>
    /// Attach to a GameObject to give it a set of tags.
    /// Replaces Unity's single-string tag system with hierarchical multi-tag support.
    /// 
    /// <code>
    /// // Query via component
    /// var tags = GetComponent&lt;TagComponent&gt;();
    /// if (tags.Has(Tag.Entity.Player)) { ... }
    /// 
    /// // Query via extension (no caching needed)
    /// if (other.gameObject.HasTag(Tag.Entity.Enemy)) { ... }
    /// if (other.gameObject.HasAnyTag(Tag.Entity.Enemy)) { ... }
    /// </code>
    /// </summary>
    public class TagComponent : MonoBehaviour, ITagProvider
    {
        [SerializeField] private TagSet m_tags = new();

        /// <summary>The tag set on this GameObject.</summary>
        public TagSet Tags => m_tags;

        /// <summary>Returns true if this GameObject has the exact tag.</summary>
        public bool Has(Tag tagVal) => m_tags.Has(tagVal);

        /// <summary>Returns true if this GameObject has any tag under the given hierarchy.</summary>
        public bool HasAny(Tag parent) => m_tags.HasAny(parent);

        /// <summary>Evaluates a tag query against this GameObject's tags.</summary>
        public bool Matches(TagQuery query) => query.Matches(m_tags);
    }

    /// <summary>
    /// Extension methods for querying tags on any GameObject without caching the component reference.
    /// </summary>
    public static class TagExtensions
    {
        private static readonly ConditionalWeakTable<GameObject, ITagProvider> s_cache = new();

        public static ITagProvider GetTagProvider(this GameObject go)
        {
            if (!s_cache.TryGetValue(go, out var provider))
            {
                provider = go.GetComponentInChildren<ITagProvider>() ?? go.GetComponentInParent<ITagProvider>();
                s_cache.Add(go, provider);
            }
            return provider;
        }
        
        /// <summary>Returns true if the GameObject has the exact tag.</summary>
        public static bool HasTag(this GameObject go, Tag tag)
        {
            var comp = go.GetTagProvider();
            return comp != null && comp.Tags.Has(tag);
        }

        /// <summary>Returns true if the GameObject has any tag under the given hierarchy.</summary>
        public static bool HasAnyTag(this GameObject go, Tag parent)
        {
            var comp = go.GetTagProvider();
            return comp != null && comp.Tags.HasAny(parent);
        }

        /// <summary>Returns the tag set, or null if no <see cref="TagComponent"/> is present.</summary>
        public static TagSet GetTags(this GameObject go)
        {
            var comp = go.GetTagProvider();
            return comp?.Tags;
        }

        /// <summary>Evaluates a tag query against the GameObject's tags. Returns false if no component present.</summary>
        public static bool MatchesTagQuery(this GameObject go, TagQuery query)
        {
            var comp = go.GetTagProvider();
            return comp != null && comp.Tags.Matches(query);
        }
    }
}
