using UnityEngine;

namespace FS.Editor.Timeline
{
    /// <summary>
    /// Drag mode for timeline elements — determines which part the user is dragging.
    /// </summary>
    public enum ElementDragMode
    {
        None,
        Center,
        StartEdge,
        EndEdge
    }

    /// <summary>
    /// Mutable drag state tracked by TimelineView and written by TimelineGUI during interaction.
    /// </summary>
    public class ElementDragState
    {
        public bool IsDragging;
        public ElementDragMode Mode;
    }

    /// <summary>
    /// Generic interface for elements displayed on the timeline.
    /// Consumers implement this to plug any system into the timeline editor.
    ///
    /// IMPORTANT: Implementors MUST override Equals() and GetHashCode() so that two
    /// instances wrapping the same underlying data are considered equal. The timeline
    /// may recreate element arrays each frame, and selection/drag tracking relies on
    /// Equals() to correlate elements across frames.
    /// </summary>
    public interface ITimelineElement
    {
        /// <summary>Display label shown on the timeline bar or marker.</summary>
        string Label { get; }

        /// <summary>
        /// True = marker/callback (single time point, drawn as icon + label).
        /// False = ranged bar (drawn as a duration bar with resizable edges).
        /// </summary>
        bool IsMarker { get; }

        /// <summary>
        /// Start time in real seconds (absolute, not normalized).
        /// Settable — the timeline writes to this when the user drags.
        /// </summary>
        float StartTime { get; set; }

        /// <summary>
        /// End time in real seconds. Settable for edge-resize support.
        /// For markers, return StartTime (or any value — it's ignored).
        /// </summary>
        float EndTime { get; set; }

        /// <summary>Computed duration. Default: EndTime - StartTime.</summary>
        float Duration => EndTime - StartTime;

        /// <summary>Whether this element is enabled/active. Inactive = reduced alpha.</summary>
        bool IsActive { get; }

        /// <summary>Optional custom icon for marker elements. Null = default icon.</summary>
        Texture2D Icon { get; }

        /// <summary>
        /// Stable ID for deterministic per-element color assignment.
        /// Use GetInstanceID() for Components, or GetHashCode() for plain objects.
        /// </summary>
        int StableId { get; }
    }
}
