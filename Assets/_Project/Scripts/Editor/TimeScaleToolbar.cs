using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace FS.Editor
{
    public static class TimeScaleToolbar
    {
        private static float m_currentTimeScale = 1f;

        [MainToolbarElement("FreeSkies/TimeScaleSlider", defaultDockIndex = 5, defaultDockPosition = MainToolbarDockPosition.Right)]
        private static MainToolbarElement OnTimescaleSliderV2()
        {
            var icon = EditorGUIUtility.IconContent("d_UnityEditor.ProfilerWindow@2x");
            var element = new MainToolbarSlider(icon, Time.timeScale, 0, 1, f =>
            {
                m_currentTimeScale = f;
                Time.timeScale = m_currentTimeScale;
            });
            return element;
        }
    }
}