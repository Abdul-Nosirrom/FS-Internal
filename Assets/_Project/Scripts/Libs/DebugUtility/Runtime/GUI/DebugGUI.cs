using System.Diagnostics;
using UnityEngine;

namespace FS.RuntimeDebug
{
    public static partial class DebugGUI
    {
        public static bool BeginFoldout(string label, ref bool isExpanded, GUIStyle style = null)
        {
            GUIStyle foldoutButton = new GUIStyle(GUI.skin.button);
            foldoutButton.fontStyle = FontStyle.Bold;
            foldoutButton.alignment = TextAnchor.MiddleLeft;

            if (isExpanded)
                GUILayout.BeginVertical(style ?? GUI.skin.box);

            if (GUILayout.Button($"{(isExpanded ? "▼" : "►")} - {label}", foldoutButton))
            {
                isExpanded = !isExpanded;
            }
            
            return isExpanded;
        }
        
        public static void EndFoldout()
        {
            GUILayout.EndVertical();
        }

        public static void ProgressBar(string label, float value, float min = 0, float max = 1, Color? fillColor = null,
            Color? backgroundColor = null, GUIStyle labelStyle = null)
        {
            labelStyle ??= new GUIStyle(GUI.skin.label);
            fillColor ??= Color.forestGreen;
            backgroundColor ??= Color.darkRed;
            
            var ogColor = GUI.backgroundColor;
            
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontStyle = FontStyle.Bold;
            
            GUILayout.Label(label);
            var rect = GUILayoutUtility.GetLastRect();
            Rect progressBarRect = rect;//GUILayoutUtility.GetRect(rect.width, height);
            GUI.color = backgroundColor.Value;
            GUI.DrawTexture(progressBarRect, Texture2D.whiteTexture);
            GUI.color = fillColor.Value;
            float normalizedValue = Mathf.Clamp01(Mathf.InverseLerp(min, max, value));
            Rect fillRect = new Rect(progressBarRect.x, progressBarRect.y, progressBarRect.width * normalizedValue, progressBarRect.height);
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            
            GUI.color = ogColor; // Reset color after drawing
            
            GUI.Label(rect, label, labelStyle);
            
        }

        private static Material debug_lineMaterial_internal;
        public static Material debug_lineMaterial
        {
            get
            {
                if (!debug_lineMaterial_internal)
                {
                    debug_lineMaterial_internal = new Material(Shader.Find("Hidden/Internal-Colored"));
                    debug_lineMaterial_internal.hideFlags = HideFlags.HideAndDontSave;
                    debug_lineMaterial_internal.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    debug_lineMaterial_internal.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    debug_lineMaterial_internal.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    debug_lineMaterial_internal.SetInt("_ZWrite", 0);
                }
                return debug_lineMaterial_internal;
            }
        }

        public static void Plot(float[] values, Rect? rect = null, Color? color = null, float maxY = 10, bool symmetric = true)
        {
            color ??= Color.green;
            var graphRect = rect ?? GUILayoutUtility.GetAspectRect(1);

            var adjustedNDCRect = graphRect; // drawing in NDC, y is flipped
            adjustedNDCRect.y += graphRect.height;
            
            var screenGraphRect = GUIToNDC(GUIUtility.GUIToScreenRect(adjustedNDCRect));

            if (Event.current.type != EventType.Repaint) return;
            
            maxY = Mathf.Max(maxY, Mathf.Abs(Mathf.Max(values)));
            
            GL.PushMatrix();
            debug_lineMaterial.SetPass(0);
            GL.LoadOrtho();
            
            // Graph Frame
            {
                GL.Color(Color.gray8);
                
                GL.Begin(GL.LINE_STRIP);
                GL.Vertex3(screenGraphRect.xMin, screenGraphRect.yMin, 0);
                GL.Vertex3(screenGraphRect.xMax, screenGraphRect.yMin, 0);
                GL.Vertex3(screenGraphRect.xMax, screenGraphRect.yMax, 0);
                GL.Vertex3(screenGraphRect.xMin, screenGraphRect.yMax, 0);
                GL.Vertex3(screenGraphRect.xMin, screenGraphRect.yMin, 0);
                GL.End();

                if (symmetric)
                {
                    GL.Begin(GL.LINES);

                    //GL.Vertex3((screenGraphRect.xMax + screenGraphRect.xMin)/2, screenGraphRect.yMin, 0);
                    //GL.Vertex3((screenGraphRect.xMax + screenGraphRect.xMin)/2, screenGraphRect.yMax, 0);

                    GL.Vertex3(screenGraphRect.xMin, (screenGraphRect.yMax + screenGraphRect.yMin) / 2, 0);
                    GL.Vertex3(screenGraphRect.xMax, (screenGraphRect.yMax + screenGraphRect.yMin) / 2, 0);

                    GL.End();
                }
            }
            
            
            GL.Begin(GL.LINE_STRIP);
            
            GL.Color(color.Value);
            

            int idx = 0;
            foreach (var val in values)
            {
                Vector3 graphPos = new Vector3(idx, val, 0);
                // Get the graph position in terms of the rect
                graphPos.x = Mathf.Lerp(screenGraphRect.xMin, screenGraphRect.xMax, idx / (float)(values.Length - 1));
                
                float yT = symmetric ? (val / maxY + 1) / 2f : val / maxY;
                graphPos.y = Mathf.Lerp(screenGraphRect.yMin, screenGraphRect.yMax, yT); // Assuming values are normalized between -1 and 1

                GL.Vertex(graphPos);
                idx++;
            }

            GL.End();
            GL.PopMatrix();
        }


        public static Rect GUIToNDC(Rect rect)
        {
            // Convert GUI coordinates to Normalized Device Coordinates (NDC)
            float x = rect.x / Screen.width;
            float y = 1 - rect.y / Screen.height;
            float width = rect.width / Screen.width;
            float height = rect.height / Screen.height;

            return new Rect(x, y, width, height);
        }
    }
}