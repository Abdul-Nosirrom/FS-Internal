using FS.TagSystem;
using UnityEngine;

namespace FS.GameplayActions
{
    /// <summary>
    /// Extension methods that map gameplay action activation semantics onto the tag system.
    /// 
    /// <para>
    /// The underlying model is inverted: tag presence means "used/consumed," tag absence means "available."
    /// These extensions provide intention-revealing names so callsites read naturally:
    /// </para>
    /// 
    /// <code>
    /// // Check if action is available
    /// if (tags.HasActivation(BlastJump.ActivationTag)) { ... }
    /// 
    /// // Consume on use
    /// tags.ConsumeActivation(BlastJump.ActivationTag);
    /// 
    /// // Reset from a level object or state change
    /// tags.ResetActivation(BlastJump.ActivationTag);
    /// 
    /// // Reset all double jumps at once
    /// tags.ResetActivations(Tag.Action.Activation);
    /// </code>
    /// </summary>
    public static class GameplayActionTagExtensions
    {
        #region Has Activation (Single)

        /// <summary>
        /// Returns true if the activation tag has NOT been consumed — i.e. the action is available.
        /// </summary>
        public static bool HasActivation<T>(this T tags, Tag activationTag) where T : ITagSource
            => !tags.Has(activationTag);

        /// <summary>
        /// Returns true if the activation tag has NOT been consumed — i.e. the action is available.
        /// GameObject convenience overload.
        /// </summary>
        public static bool HasActivation(this GameObject go, Tag activationTag)
            => go.GetTags() is { } tags && tags.HasActivation(activationTag);
        
        /// <summary>
        /// Returns true if the activation tag has NOT been consumed — i.e. the action is available.
        /// MonoBehaviour convenience overload.
        /// </summary>
        public static bool HasActivation(this MonoBehaviour mb, Tag activationTag)
            => mb.gameObject.GetTags() is { } tags && tags.HasActivation(activationTag);

        #endregion

        #region Has Activation (Counted)

        /// <summary>
        /// Returns true if the activation tag has been consumed fewer than maxUses times.
        /// Used for multi-charge actions like wall kicks.
        /// </summary>
        public static bool HasActivation<T>(this T tags, Tag activationTag, int maxUses) where T : ITagSource
            => tags[activationTag] < maxUses;

        /// <summary>
        /// Returns true if the activation tag has been consumed fewer than maxUses times.
        /// GameObject convenience overload.
        /// </summary>
        public static bool HasActivation(this GameObject go, Tag activationTag, int maxUses)
            => go.GetTags() is { } tags && tags.HasActivation(activationTag, maxUses);
        
        /// <summary>
        /// Returns true if the activation tag has been consumed fewer than maxUses times.
        /// MonoBehaviour convenience overload.
        /// </summary>
        public static bool HasActivation(this MonoBehaviour mb, Tag activationTag, int maxUses)
            => mb.gameObject.GetTags() is { } tags && tags.HasActivation(activationTag, maxUses);

        #endregion

        #region Consume Activation

        /// <summary>
        /// Marks the activation tag as consumed (increments usage count).
        /// For binary actions this makes the tag present (unavailable).
        /// For counted actions this increments toward the max.
        /// </summary>
        public static bool ConsumeActivation<T>(this T tags, Tag activationTag) where T : ITagSource
            => tags.Add(activationTag);

        /// <summary>
        /// Marks the activation tag as consumed.
        /// GameObject convenience overload.
        /// </summary>
        public static bool ConsumeActivation(this GameObject go, Tag activationTag)
            => go.GetTags()?.ConsumeActivation(activationTag) ?? false;

        /// <summary>
        /// Marks the activation tag as consumed.
        /// MonoBehaviour convenience overload.
        /// </summary>
        public static bool ConsumeActivation(this MonoBehaviour mb, Tag activationTag)
            => mb.gameObject.GetTags()?.ConsumeActivation(activationTag) ?? false;
        
        #endregion

        #region Reset Activation (Single Tag)

        /// <summary>
        /// Resets a specific activation tag, clearing all usage counts.
        /// The action becomes fully available again.
        /// </summary>
        public static bool ResetActivation<T>(this T tags, Tag activationTag) where T : ITagSource
            => tags.RemoveAll(activationTag);

        /// <summary>
        /// Resets a specific activation tag.
        /// GameObject convenience overload.
        /// </summary>
        public static bool ResetActivation(this GameObject go, Tag activationTag)
            => go.GetTags()?.ResetActivation(activationTag) ?? false;
        
        /// <summary>
        /// Resets a specific activation tag.
        /// MonoBehaviour convenience overload.
        /// </summary>
        public static bool ResetActivation(this MonoBehaviour mb, Tag activationTag)
            => mb.gameObject.GetTags()?.ResetActivation(activationTag) ?? false;

        #endregion

        #region Reset Activation (Multiple Exact Tags)

        /// <summary>Resets multiple specific activation tags. Returns number of tags reset.</summary>
        public static int ResetActivation<T>(this T tags, Tag a, Tag b) where T : ITagSource
            => (tags.RemoveAll(a) ? 1 : 0) + (tags.RemoveAll(b) ? 1 : 0);

        /// <summary>Resets multiple specific activation tags. Returns number of tags reset.</summary>
        public static int ResetActivation<T>(this T tags, Tag a, Tag b, Tag c) where T : ITagSource
            => (tags.RemoveAll(a) ? 1 : 0) + (tags.RemoveAll(b) ? 1 : 0) + (tags.RemoveAll(c) ? 1 : 0);

        /// <summary>Resets multiple specific activation tags. Returns number of tags reset.</summary>
        public static int ResetActivation<T>(this T tags, Tag a, Tag b, Tag c, Tag d) where T : ITagSource
            => (tags.RemoveAll(a) ? 1 : 0) + (tags.RemoveAll(b) ? 1 : 0) 
             + (tags.RemoveAll(c) ? 1 : 0) + (tags.RemoveAll(d) ? 1 : 0);

        /// <summary>Resets multiple specific activation tags. Returns number of tags reset.</summary>
        public static int ResetActivation<T>(this T tags, params Tag[] activationTags) where T : ITagSource
        {
            int count = 0;
            foreach (var tag in activationTags) { if (tags.RemoveAll(tag)) count++; }
            return count;
        }

        #endregion

        #region Reset Activations (Hierarchy Match)

        /// <summary>
        /// Resets all activation tags under a parent hierarchy.
        /// e.g. ResetActivations(Tag.Action.Activation) resets all double jumps regardless of trinket.
        /// Returns number of tags reset.
        /// </summary>
        public static int ResetActivationsUnder<T>(this T tags, Tag parentTag) where T : ITagSource
            => tags.RemoveMatching(parentTag);

        /// <summary>
        /// Resets all activation tags under a parent hierarchy.
        /// GameObject convenience overload.
        /// </summary>
        public static int ResetActivationsUnder(this GameObject go, Tag parentTag)
            => go.GetTags()?.RemoveMatching(parentTag) ?? 0;
        
        /// <summary>
        /// Resets all activation tags under a parent hierarchy.
        /// Monobehavior convenience overload.
        /// </summary>
        public static int ResetActivationsUnder(this MonoBehaviour mb, Tag parentTag)
            => mb.gameObject.GetTags()?.RemoveMatching(parentTag) ?? 0;

        #endregion

        #region Exhaust Activation

        /// <summary>
        /// Forcefully exhausts an activation tag by setting its count to a specific value.
        /// Used when an action wants to prevent further use without a full reset,
        /// e.g. setting wall kick count to max on a whiffed dash.
        /// </summary>
        public static void ExhaustActivation<T>(this T tags, Tag activationTag, int count) where T : ITagSource
            => tags[activationTag] = count;

        /// <summary>
        /// Forcefully exhausts an activation tag.
        /// GameObject convenience overload.
        /// </summary>
        public static void ExhaustActivation(this GameObject go, Tag activationTag, int count)
            => go.GetTags()?.ExhaustActivation(activationTag, count);
        
        /// <summary>
        /// Forcefully exhausts an activation tag.
        /// MonoBehavior convenience overload.
        /// </summary>
        public static void ExhaustActivation(this MonoBehaviour mb, Tag activationTag, int count)
            => mb.gameObject.GetTags()?.ExhaustActivation(activationTag, count);

        #endregion
    }
}