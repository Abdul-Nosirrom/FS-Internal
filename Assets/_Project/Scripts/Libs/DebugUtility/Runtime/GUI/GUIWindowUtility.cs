using UnityEngine;

namespace FS.RuntimeDebug
{
    public static class GUIWindowUtility
    {
        public static Rect ResizeRect(Rect windowRect, float minWidth, float minHeight, ref bool isDragging)
        {
            Rect resizedRect = new Rect(windowRect);
            
            // Bottom right corner for resizing
            Rect resizeZone = new Rect(resizedRect.width - 20f, resizedRect.height - 20f, 20f, 20f);
            Event e = Event.current;

            var ogColor = GUI.color;
            GUI.color = Color.darkCyan;
            if (isDragging)
                GUI.color = Color.cyan;
            var style = new GUIStyle(GUI.skin.button);
            style.fontSize *= 2;
            GUI.Label(resizeZone, "◢", style);
            GUI.color = ogColor;

            if (resizeZone.Contains(e.mousePosition) && e.type == EventType.MouseDown)
            {
                isDragging = true;
                e.Use();
            }
            if (isDragging && e.type == EventType.MouseUp)
            {
                isDragging = false;
                e.Use();
            }
            if (isDragging && e.type == EventType.MouseDrag)
            {
                resizedRect.width = Mathf.Max(minWidth, resizedRect.width + e.delta.x);
                resizedRect.height = Mathf.Max(minHeight, resizedRect.height + e.delta.y);
                e.Use();
            }

            if (!isDragging)
            {
                GUI.DragWindow();
            }

            return resizedRect;
        }

        public static bool CloseWindowButton(Rect windowRect)
        {
            // Close Button
            var ogColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.red;
            bool shouldClose = GUI.Button(new Rect(windowRect.width - 20, 0, 16, 16), "X");
            GUI.backgroundColor = ogColor; // Reset color
            return shouldClose;
        }
    }
}