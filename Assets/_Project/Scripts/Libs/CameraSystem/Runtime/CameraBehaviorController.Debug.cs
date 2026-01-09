using System.Collections.Generic;
using Drawing;
using FS.RuntimeDebug;
using UnityEngine;

namespace FS.CameraSystem
{
    public abstract partial class CameraBehaviorController : IDebugProvider, IDrawGizmos
    {
        public CameraBehaviorController()
        {
#if UNITY_EDITOR
            DrawingManager.Register(this);
#endif            
        }
        
        public string DebugName => "Camera Behavior Controller";

        private CameraBehavior debug_inspectedBehavior;
        private Dictionary<CameraBehavior, bool> debug_behaviorFoldouts = new();
        
        public void OnDebugGUI()
        {
            // Draw framing shit
            {
                GL.PushMatrix();
                DebugGUI.debug_lineMaterial.SetPass(0);
                GL.LoadOrtho();
                
                GL.Color(Color.black);
                
                GL.Begin(GL.LINES);

                GL.Vertex3(0, 0.5f, 0);
                GL.Vertex3(1, 0.5f, 0);

                GL.Vertex3(0.5f, 0f, 0);
                GL.Vertex3(0.5f, 1f, 0);
                
                GL.End();
                
                GL.PopMatrix();
            }
            
            GUILayout.BeginVertical(GUI.skin.box);

            var label = new GUIStyle(GUI.skin.label);
            label.fontSize *= 2;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            GUILayout.Label("Camera State", label);

            label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.MiddleLeft;
            GUILayout.Label($"Distance: {m_cameraVector.Distance:F1}", label);
            GUILayout.Label($"FOV: {m_cameraVector.FOV}", label);
            
            GUILayout.BeginHorizontal();
            
            GUILayout.Label("Rotation: ");
            
            DebugGUI.ProgressBar($"Pitch [{m_cameraVector.Rotation.pitch:F0}]", m_cameraVector.Rotation.pitch, -180, 180, Color.darkRed, Color.gray1);
            DebugGUI.ProgressBar($"Yaw [{m_cameraVector.Rotation.yaw:F0}]", m_cameraVector.Rotation.yaw, -180, 180, Color.forestGreen, Color.gray1);
            
            GUILayout.EndHorizontal();
            
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();
            
            GUILayout.BeginVertical(GUI.skin.box);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            GUILayout.Label("Behaviors", label);
            
            
            // Invoke behaviors to draw their own custom GUI elements
            foreach (var camBehavior in m_cameraBehaviors)
            {
                if (camBehavior == null) continue;
                        
                DebugGUI.ProgressBar("", camBehavior.GetBlendAlpha());
                if (GUI.Button(GUILayoutUtility.GetLastRect(), $"[{camBehavior.Priority}] {camBehavior.Name}"))
                {
                    debug_inspectedBehavior = camBehavior;
                }
            }
            
            GUILayout.EndVertical();
            
            // Draw behavior
            if (debug_inspectedBehavior != null)
            {
                debug_inspectedBehavior.OnCameraGUI(this);
            }
            
            GUILayout.EndHorizontal();
        }

        public void DrawGizmos()
        {
            if (Application.isPlaying) return;
            
            // TODO: Kinda shitty, cuz DebugManager isn't available in editor obv
            using var thicknessScope = Draw.ingame.WithLineWidth(2f);

            {
                var matrix = Matrix4x4.TRS(PlayerPivot, m_basis, Vector3.one);
                using var drawTransform = Draw.ingame.WithMatrix(matrix);

                
                Draw.ingame.Circle(Vector3.zero, Vector3.up, m_cameraVector.Distance);

                var topRadius = m_cameraVector.Distance * Mathf.Cos(Mathf.Deg2Rad * m_cameraVector.PitchLimits.y);
                var topHeight = m_cameraVector.Distance * Mathf.Sin(Mathf.Deg2Rad * m_cameraVector.PitchLimits.y);

                Draw.ingame.Circle(topHeight * Vector3.up, Vector3.up, topRadius);

                var botRadius = m_cameraVector.Distance * Mathf.Cos(Mathf.Deg2Rad * m_cameraVector.PitchLimits.x);
                var botHeight = m_cameraVector.Distance * Mathf.Sin(Mathf.Deg2Rad * m_cameraVector.PitchLimits.x);
                
                Draw.ingame.Circle(botHeight * Vector3.up, Vector3.up, botRadius);
            }
            //if (!Application.isPlaying) OnDebugDraw();
        }
        
        public void OnDebugDraw() // TODO: Pass IsSelected here?
        {
            // Draw behavior gizmos
            debug_inspectedBehavior?.OnDrawCameraGizmos(this);

            {
                m_cameraObstruction.DrawObstructionGizmos();
            }
        }
    }
}