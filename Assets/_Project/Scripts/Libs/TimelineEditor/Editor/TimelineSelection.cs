using System.Linq;

namespace FS.Editor.Timeline
{
    /// <summary>
    /// Manages the currently selected timeline element.
    /// Uses Equals() for validation, so elements must implement Equals/GetHashCode properly.
    /// </summary>
    public class TimelineSelection
    {
        private ITimelineElement m_selected;

        public ITimelineElement Selected => m_selected;

        /// <summary>
        /// Validates that the current selection still exists in the provided element array.
        /// Call each frame before drawing to handle elements being added/removed.
        /// </summary>
        public void Validate(ITimelineElement[] elements)
        {
            if (m_selected != null && !elements.Contains(m_selected))
                Clear();
        }

        public void Set(ITimelineElement element) => m_selected = element;
        public void Clear() => Set(null);
    }
}
