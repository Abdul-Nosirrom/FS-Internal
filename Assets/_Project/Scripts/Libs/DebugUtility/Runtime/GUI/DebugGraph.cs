using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS.RuntimeDebug
{
    /// <summary>
    /// A flexible, feature-rich graph plotter for Unity's IMGUI debug tools.
    /// Supports multiple data series, auto-scaling, grid lines, and value indicators.
    /// </summary>
    public class DebugGraph
    {
        public string Title { get; set; }
        public int Capacity { get; set; }
        public float MinY { get; set; }
        public float MaxY { get; set; }
        public bool AutoScale { get; set; }
        public bool Symmetric { get; set; }
        public bool ShowGrid { get; set; }
        public bool ShowLegend { get; set; }
        public bool ShowMinMax { get; set; }
        public bool ShowCurrentValue { get; set; }
        public int GridLinesHorizontal { get; set; }
        public int GridLinesVertical { get; set; }
        public Color BackgroundColor { get; set; }
        public Color GridColor { get; set; }
        public Color AxisColor { get; set; }
        public Color TextColor { get; set; }
        
        private readonly List<DataSeries> _series = new List<DataSeries>();
        private static Material _lineMaterial;
        
        public class DataSeries
        {
            public string Name { get; set; }
            public Color Color { get; set; }
            public float LineWidth { get; set; }
            public bool Visible { get; set; }
            
            internal readonly RingBuffer<float> Values;
            internal float CurrentMin;
            internal float CurrentMax;
            internal float CurrentAvg;
            
            public DataSeries(string name, Color color, int capacity, float lineWidth = 2f)
            {
                Name = name;
                Color = color;
                LineWidth = lineWidth;
                Visible = true;
                Values = new RingBuffer<float>(capacity);
                CurrentMin = float.MaxValue;
                CurrentMax = float.MinValue;
            }
            
            public void Push(float value)
            {
                Values.Push(value);
                UpdateStats();
            }
            
            public void Clear()
            {
                Values.Clear();
                CurrentMin = float.MaxValue;
                CurrentMax = float.MinValue;
                CurrentAvg = 0;
            }
            
            private void UpdateStats()
            {
                if (Values.Count == 0) return;
                
                CurrentMin = float.MaxValue;
                CurrentMax = float.MinValue;
                float sum = 0;
                
                foreach (var val in Values)
                {
                    CurrentMin = Mathf.Min(CurrentMin, val);
                    CurrentMax = Mathf.Max(CurrentMax, val);
                    sum += val;
                }
                
                CurrentAvg = sum / Values.Count;
            }
        }
        
        /// <summary>
        /// Efficient ring buffer for storing graph data
        /// </summary>
        internal class RingBuffer<T> : IEnumerable<T>
        {
            private readonly T[] _buffer;
            private int _head;
            private int _count;
            
            public int Count => _count;
            public int Capacity => _buffer.Length;
            
            public RingBuffer(int capacity)
            {
                _buffer = new T[capacity];
            }
            
            public void Push(T item)
            {
                _buffer[_head] = item;
                _head = (_head + 1) % _buffer.Length;
                _count = Mathf.Min(_count + 1, _buffer.Length);
            }
            
            public void Clear()
            {
                _head = 0;
                _count = 0;
            }
            
            public T this[int index]
            {
                get
                {
                    if (index < 0 || index >= _count)
                        throw new IndexOutOfRangeException();
                    int actualIndex = (_head - _count + index + _buffer.Length) % _buffer.Length;
                    return _buffer[actualIndex];
                }
            }
            
            public IEnumerator<T> GetEnumerator()
            {
                for (int i = 0; i < _count; i++)
                    yield return this[i];
            }
            
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            
            public T[] ToArray()
            {
                var arr = new T[_count];
                for (int i = 0; i < _count; i++)
                    arr[i] = this[i];
                return arr;
            }
        }
        
        public DebugGraph(string title = "Graph", int capacity = 256)
        {
            Title = title;
            Capacity = capacity;
            MinY = 0;
            MaxY = 10;
            AutoScale = true;
            Symmetric = false;
            ShowGrid = true;
            ShowLegend = true;
            ShowMinMax = true;
            ShowCurrentValue = true;
            GridLinesHorizontal = 4;
            GridLinesVertical = 8;
            BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            GridColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            AxisColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            TextColor = Color.white;
        }
        
        public DataSeries AddSeries(string name, Color color, float lineWidth = 2f)
        {
            var series = new DataSeries(name, color, Capacity, lineWidth);
            _series.Add(series);
            return series;
        }
        
        public DataSeries GetSeries(string name)
        {
            return _series.Find(s => s.Name == name);
        }
        
        public DataSeries GetSeries(int index)
        {
            return index >= 0 && index < _series.Count ? _series[index] : null;
        }
        
        public void RemoveSeries(string name)
        {
            _series.RemoveAll(s => s.Name == name);
        }
        
        public void ClearAll()
        {
            foreach (var series in _series)
                series.Clear();
        }
        
        /// <summary>
        /// Draw the graph. Call this from OnGUI.
        /// </summary>
        public void Draw(Rect? rect = null, float aspectRatio = 0.5f)
        {
            // Get rect
            Rect graphRect;
            if (rect.HasValue)
            {
                graphRect = rect.Value;
            }
            else
            {
                graphRect = GUILayoutUtility.GetAspectRect(aspectRatio);
            }
            
            // Draw toggle controls for series visibility
            if (ShowLegend && _series.Count > 0)
            {
                DrawLegend();
            }
            
            if (Event.current.type != EventType.Repaint)
                return;
            
            // Calculate Y range
            float minY = MinY;
            float maxY = MaxY;
            
            if (AutoScale)
            {
                minY = float.MaxValue;
                maxY = float.MinValue;
                
                foreach (var series in _series)
                {
                    if (!series.Visible || series.Values.Count == 0) continue;
                    minY = Mathf.Min(minY, series.CurrentMin);
                    maxY = Mathf.Max(maxY, series.CurrentMax);
                }
                
                if (minY == float.MaxValue)
                {
                    minY = MinY;
                    maxY = MaxY;
                }
                
                // Add padding
                float range = maxY - minY;
                if (range < 0.001f) range = 1f;
                minY -= range * 0.1f;
                maxY += range * 0.1f;
                
                if (Symmetric)
                {
                    float absMax = Mathf.Max(Mathf.Abs(minY), Mathf.Abs(maxY));
                    minY = -absMax;
                    maxY = absMax;
                }
            }
            
            // Convert to screen coordinates
            var screenRect = GUIToNDC(GUIUtility.GUIToScreenRect(new Rect(
                graphRect.x, 
                graphRect.y + graphRect.height, 
                graphRect.width, 
                graphRect.height
            )));
            
            EnsureMaterial();
            
            GL.PushMatrix();
            _lineMaterial.SetPass(0);
            GL.LoadOrtho();
            
            // Draw background
            DrawBackground(screenRect);
            
            // Draw grid
            if (ShowGrid)
            {
                DrawGrid(screenRect, minY, maxY);
            }
            
            // Draw zero line if in range
            if (minY < 0 && maxY > 0)
            {
                DrawZeroLine(screenRect, minY, maxY);
            }
            
            // Draw frame
            DrawFrame(screenRect);
            
            // Draw each series
            foreach (var series in _series)
            {
                if (series.Visible && series.Values.Count > 1)
                {
                    DrawSeries(screenRect, series, minY, maxY);
                }
            }
            
            GL.PopMatrix();
            
            // Draw axis labels using GUI
            DrawAxisLabels(graphRect, minY, maxY);
            
            // Draw current values
            if (ShowCurrentValue)
            {
                DrawCurrentValues(graphRect);
            }
        }
        
        private void DrawLegend()
        {
            GUILayout.BeginHorizontal();
            
            var originalColor = GUI.color;
            foreach (var series in _series)
            {
                GUI.color = series.Color;
                series.Visible = GUILayout.Toggle(series.Visible, series.Name, GUILayout.Width(80));
            }
            GUI.color = originalColor;
            
            GUILayout.EndHorizontal();
        }
        
        private void DrawBackground(Rect rect)
        {
            GL.Begin(GL.QUADS);
            GL.Color(BackgroundColor);
            GL.Vertex3(rect.xMin, rect.yMin, 0);
            GL.Vertex3(rect.xMax, rect.yMin, 0);
            GL.Vertex3(rect.xMax, rect.yMax, 0);
            GL.Vertex3(rect.xMin, rect.yMax, 0);
            GL.End();
        }
        
        private void DrawGrid(Rect rect, float minY, float maxY)
        {
            GL.Begin(GL.LINES);
            GL.Color(GridColor);
            
            // Horizontal grid lines
            for (int i = 0; i <= GridLinesHorizontal; i++)
            {
                float t = i / (float)GridLinesHorizontal;
                float y = Mathf.Lerp(rect.yMin, rect.yMax, t);
                GL.Vertex3(rect.xMin, y, 0);
                GL.Vertex3(rect.xMax, y, 0);
            }
            
            // Vertical grid lines
            for (int i = 0; i <= GridLinesVertical; i++)
            {
                float t = i / (float)GridLinesVertical;
                float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                GL.Vertex3(x, rect.yMin, 0);
                GL.Vertex3(x, rect.yMax, 0);
            }
            
            GL.End();
        }
        
        private void DrawZeroLine(Rect rect, float minY, float maxY)
        {
            float zeroT = Mathf.InverseLerp(minY, maxY, 0);
            float zeroY = Mathf.Lerp(rect.yMin, rect.yMax, zeroT);
            
            GL.Begin(GL.LINES);
            GL.Color(AxisColor);
            GL.Vertex3(rect.xMin, zeroY, 0);
            GL.Vertex3(rect.xMax, zeroY, 0);
            GL.End();
        }
        
        private void DrawFrame(Rect rect)
        {
            GL.Begin(GL.LINE_STRIP);
            GL.Color(AxisColor);
            GL.Vertex3(rect.xMin, rect.yMin, 0);
            GL.Vertex3(rect.xMax, rect.yMin, 0);
            GL.Vertex3(rect.xMax, rect.yMax, 0);
            GL.Vertex3(rect.xMin, rect.yMax, 0);
            GL.Vertex3(rect.xMin, rect.yMin, 0);
            GL.End();
        }
        
        private void DrawSeries(Rect rect, DataSeries series, float minY, float maxY)
        {
            GL.Begin(GL.LINE_STRIP);
            GL.Color(series.Color);
            
            int count = series.Values.Count;
            for (int i = 0; i < count; i++)
            {
                float value = series.Values[i];
                float xT = i / (float)(count - 1);
                float yT = Mathf.InverseLerp(minY, maxY, value);
                
                float x = Mathf.Lerp(rect.xMin, rect.xMax, xT);
                float y = Mathf.Lerp(rect.yMin, rect.yMax, yT);
                
                GL.Vertex3(x, y, 0);
            }
            
            GL.End();
        }
        
        private void DrawAxisLabels(Rect graphRect, float minY, float maxY)
        {
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft
            };
            labelStyle.normal.textColor = TextColor;
            
            // Y-axis labels
            float yLabelWidth = 45;
            
            // Max value
            GUI.Label(
                new Rect(graphRect.xMax + 5, graphRect.y - 8, yLabelWidth, 16),
                maxY.ToString("F1"),
                labelStyle
            );
            
            // Min value
            GUI.Label(
                new Rect(graphRect.xMax + 5, graphRect.yMax - 8, yLabelWidth, 16),
                minY.ToString("F1"),
                labelStyle
            );
            
            // Zero line label if visible
            if (minY < 0 && maxY > 0)
            {
                float zeroT = Mathf.InverseLerp(maxY, minY, 0);
                float zeroY = Mathf.Lerp(graphRect.y, graphRect.yMax, zeroT);
                GUI.Label(
                    new Rect(graphRect.xMax + 5, zeroY - 8, yLabelWidth, 16),
                    "0",
                    labelStyle
                );
            }
            
            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = TextColor;
            
            GUI.Label(
                new Rect(graphRect.x, graphRect.y - 18, graphRect.width, 16),
                Title,
                titleStyle
            );
        }
        
        private void DrawCurrentValues(Rect graphRect)
        {
            var valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleRight
            };
            
            float yOffset = 0;
            foreach (var series in _series)
            {
                if (!series.Visible || series.Values.Count == 0) continue;
                
                valueStyle.normal.textColor = series.Color;
                
                float currentValue = series.Values[series.Values.Count - 1];
                string text = ShowMinMax
                    ? $"{series.Name}: {currentValue:F1} (min:{series.CurrentMin:F1} max:{series.CurrentMax:F1})"
                    : $"{series.Name}: {currentValue:F1}";
                
                GUI.Label(
                    new Rect(graphRect.x, graphRect.yMax + 2 + yOffset, graphRect.width, 14),
                    text,
                    valueStyle
                );
                yOffset += 14;
            }
        }
        
        private static Rect GUIToNDC(Rect rect)
        {
            float x = rect.x / Screen.width;
            float y = 1 - rect.y / Screen.height;
            float width = rect.width / Screen.width;
            float height = rect.height / Screen.height;
            return new Rect(x, y, width, height);
        }
        
        private static void EnsureMaterial()
        {
            if (_lineMaterial != null) return;
            
            _lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
        }
    }
    
    /// <summary>
    /// Quick static helper for one-off graphs (backwards compatible with original API)
    /// </summary>
    public static partial class DebugGUI
    {
        private static readonly Dictionary<string, DebugGraph> _quickGraphs = new Dictionary<string, DebugGraph>();
        
        /// <summary>
        /// Quick plot for simple use cases. For more control, use DebugGraph class directly.
        /// </summary>
        public static void QuickPlot(string id, float value, Rect? rect = null, Color? color = null, 
            float maxY = 10, bool symmetric = false, int capacity = 256)
        {
            if (!_quickGraphs.TryGetValue(id, out var graph))
            {
                graph = new DebugGraph(id, capacity)
                {
                    ShowLegend = false,
                    ShowMinMax = false,
                    AutoScale = false,
                    MaxY = maxY,
                    MinY = symmetric ? -maxY : 0,
                    Symmetric = symmetric
                };
                graph.AddSeries("value", color ?? Color.green);
                _quickGraphs[id] = graph;
            }
            
            graph.GetSeries(0).Push(value);
            graph.Draw(rect);
        }
        
        /// <summary>
        /// Clear a quick plot's data
        /// </summary>
        public static void ClearQuickPlot(string id)
        {
            if (_quickGraphs.TryGetValue(id, out var graph))
            {
                graph.ClearAll();
            }
        }
    }
}