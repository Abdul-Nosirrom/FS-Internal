using System;
using UnityEngine;

namespace FS.Editor.Timeline
{
    /// <summary>
    /// Rect and Color extension methods used by the timeline GUI.
    /// Replaces DG.DemiEditor extension methods so there's no external dependency.
    /// </summary>
    public static class TimelineEditorExtensions
    {
        #region Color Extensions

        public static Color SetAlpha(this Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        #endregion

        #region Color Array Extensions

        /// <summary>
        /// Returns a random element from the array using Unity's Random (seed-dependent).
        /// Typically called after Random.InitState() for deterministic per-element colors.
        /// </summary>
        public static T GetRandom<T>(this T[] array)
        {
            return array[UnityEngine.Random.Range(0, array.Length)];
        }

        #endregion

        #region Rect Extensions

        public static Rect ShiftX(this Rect rect, float dx)
        {
            return new Rect(rect.x + dx, rect.y, rect.width, rect.height);
        }

        public static Rect ShiftY(this Rect rect, float dy)
        {
            return new Rect(rect.x, rect.y + dy, rect.width, rect.height);
        }

        public static Rect SetWidth(this Rect rect, float width)
        {
            return new Rect(rect.x, rect.y, width, rect.height);
        }

        public static Rect SetHeight(this Rect rect, float height)
        {
            return new Rect(rect.x, rect.y, rect.width, height);
        }

        /// <summary>
        /// Expands (positive) or contracts (negative) the rect on all sides by the given amount.
        /// </summary>
        public static Rect Expand(this Rect rect, float amount)
        {
            return new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2, rect.height + amount * 2);
        }

        /// <summary>
        /// Expands horizontally by x and vertically by y on each side.
        /// </summary>
        public static Rect Expand(this Rect rect, float x, float y)
        {
            return new Rect(rect.x - x, rect.y - y, rect.width + x * 2, rect.height + y * 2);
        }

        /// <summary>
        /// Shifts position by (x,y) and adjusts size by (w,h).
        /// </summary>
        public static Rect Shift(this Rect rect, float x, float y, float w, float h)
        {
            return new Rect(rect.x + x, rect.y + y, rect.width + w, rect.height + h);
        }

        /// <summary>
        /// Combines two rects into one that encompasses both (union).
        /// </summary>
        public static Rect Add(this Rect a, Rect b)
        {
            float xMin = Mathf.Min(a.xMin, b.xMin);
            float yMin = Mathf.Min(a.yMin, b.yMin);
            float xMax = Mathf.Max(a.xMax, b.xMax);
            float yMax = Mathf.Max(a.yMax, b.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        #endregion

        #region Array Extensions

        /// <summary>
        /// Returns the index of the first element matching the predicate, or -1 if not found.
        /// </summary>
        public static int FindIndex<T>(this T[] array, Predicate<T> match)
        {
            return Array.FindIndex(array, match);
        }

        #endregion
    }

    /// <summary>
    /// Disposable scope that temporarily overrides GUI.backgroundColor and GUI.contentColor.
    /// Replaces DG.DemiEditor.DeGUI.ColorScope.
    /// </summary>
    public struct GUIColorScope : IDisposable
    {
        private readonly Color m_prevBackground;
        private readonly Color m_prevContent;

        public GUIColorScope(Color? background = null, Color? content = null)
        {
            m_prevBackground = GUI.backgroundColor;
            m_prevContent = GUI.contentColor;
            if (background.HasValue) GUI.backgroundColor = background.Value;
            if (content.HasValue) GUI.contentColor = content.Value;
        }

        public void Dispose()
        {
            GUI.backgroundColor = m_prevBackground;
            GUI.contentColor = m_prevContent;
        }
    }
}
