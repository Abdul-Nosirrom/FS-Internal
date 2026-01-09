using System;
using System.Collections.Generic;
using Drawing;
using TimeUtils;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
using UnityEditor;
#endif

namespace FS.AI
{
    [Serializable]
    public struct PatrolPoint
    {
        public Vector3 m_position;
        public Quaternion m_lookAtRot;

        [Range(0, 1)] public float m_waitProbability;
        public float m_waitDuration;
    }

    public struct PatrolFollowState
    {
        public AINavigation m_navigation;
        public PatrolPath m_patrolPath;
        public int m_currentIdx;
        public int m_direction; // 1 for forward, -1 for backward (used in PingPong mode)
        public bool m_isWaiting;
        public TimeSince m_timeSinceReachedPoint;

        public bool HasValidPath => m_patrolPath != null;
        public PatrolPoint CurrentPatrolPoint => HasValidPath ? m_patrolPath.Waypoints[m_currentIdx] : default;
        
        public void Initialize(GameObject agent)
        {
            m_navigation = agent.GetComponent<AINavigation>();
            if (m_navigation == null)
                Debug.LogError($"[AI System] PatrolFollowState requires AINavigation component on {agent.name}");
        }
        
        public void UpdateTargetDestination()
        {
            if (m_navigation.reachedDestination && !m_isWaiting)
            {
                m_isWaiting = UnityEngine.Random.value < CurrentPatrolPoint.m_waitProbability;
                m_timeSinceReachedPoint = 0;
                if (!m_isWaiting) AdvancePath();
            }
            else if (m_isWaiting)
            {
                m_isWaiting = m_timeSinceReachedPoint < CurrentPatrolPoint.m_waitDuration;
                if (!m_isWaiting) AdvancePath();
            }

            if (!m_isWaiting) m_navigation.destination = CurrentPatrolPoint.m_position;
        }

        public void AdvancePath()
        {
            m_patrolPath.GetNextPathIndex(ref m_currentIdx, ref m_direction);
        }

        public void ReleasePath()
        {
            if (!HasValidPath) return;

            m_patrolPath.Release(ref this);
            m_patrolPath = null;
        }
    }

    public enum PatrolFollowMode
    {
        PingPong,
        Loop,
    }
    
    public class PatrolPath : MonoBehaviourGizmos
    {
        public bool IsClaimed { get; private set; }

        [SerializeField] private PatrolFollowMode FollowMode = PatrolFollowMode.Loop;
        [SerializeField] private List<PatrolPoint> m_waypoints = new();
        public List<PatrolPoint> Waypoints => m_waypoints;
        
        public bool ShouldLoopFollow => FollowMode == PatrolFollowMode.Loop;

        public Bounds PathBounds;

        private void OnEnable() => AIDirector.Instance.RegisterPatrolPath(this);
        private void OnDisable() => AIDirector.Instance?.UnregisterPatrolPath(this);
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateBounds();
        }

        public void UpdateBounds()
        {
            // Update path bounding box
            if (m_waypoints.Count == 0)
            {
                PathBounds = new Bounds(transform.position, Vector3.one);
                return;
            }
            var min = m_waypoints[0].m_position;
            var max = m_waypoints[0].m_position;
            foreach (var point in m_waypoints)
            {
                min = Vector3.Min(min, point.m_position);
                max = Vector3.Max(max, point.m_position);
            }
            PathBounds = new Bounds((min + max) / 2, max - min);
        }
#endif

        public bool ClaimPath(ref PatrolFollowState patrolState)
        {
            patrolState.m_patrolPath = this;
            patrolState.m_currentIdx = GetNearestPatrolPointIndex(patrolState.m_navigation.transform.position);
            patrolState.m_direction = 1;
            patrolState.m_isWaiting = false;
            patrolState.m_timeSinceReachedPoint = 0;
            return true; // TODO: Path claiming
        }

        public void Release(ref PatrolFollowState patrolState)
        {
            // TODO
        }

        public void GetNextPathIndex(ref int currentIdx, ref int direction)
        {
            if (FollowMode == PatrolFollowMode.Loop)
            {
                currentIdx = (currentIdx + 1) % m_waypoints.Count;
            }
            else if (FollowMode == PatrolFollowMode.PingPong)
            {
                currentIdx += direction;
                if (currentIdx >= m_waypoints.Count)
                {
                    currentIdx = m_waypoints.Count - 2;
                    direction = -1;
                }
                else if (currentIdx < 0)
                {
                    currentIdx = 1;
                    direction = 1;
                }
            }
        }
        
        public PatrolPoint GetNearstPatrolPoint(Vector3 position)
        {
            int nearestIndex = GetNearestPatrolPointIndex(position);
            if (nearestIndex == -1) throw new Exception("[AI System] No patrol points defined");
            return m_waypoints[nearestIndex];
        }
        public int GetNearestPatrolPointIndex(Vector3 position)
        {
            if (m_waypoints.Count == 0) return -1;
            
            int nearestIndex = 0;
            float nearestDistanceSqr = Vector3.SqrMagnitude(position - m_waypoints[0].m_position);
            
            for (int i = 1; i < m_waypoints.Count; i++)
            {
                float distanceSqr = Vector3.SqrMagnitude(position - m_waypoints[i].m_position);
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearestIndex = i;
                }
            }
            
            return nearestIndex;
        }

        public override void DrawGizmos()
        {
            //Draw.SolidBox(PathBounds, Color.cornflowerBlue);
            
            for (int p = 0; p < m_waypoints.Count; p++)
            {
                var currentPos = m_waypoints[p].m_position;
                if (p < m_waypoints.Count - 1 || ShouldLoopFollow)
                {
                    int nextPoint = p + 1;
                    if (ShouldLoopFollow) nextPoint %= m_waypoints.Count;
                    var nextPos = m_waypoints[nextPoint].m_position;
                    Draw.DashedLine(currentPos, nextPos, 0.5f, 0.2f);
                }
                
                // Foreach point, draw a line indicating the ground beneath it
                bool hasGround = Physics.Raycast(currentPos, Vector3.down, out var hit, 5f);
                
                if (hasGround)
                {
                    Draw.DashedLine(currentPos, hit.point, 0.2f, 0.05f);
                    Draw.Circle(hit.point, hit.normal, 0.2f, Color.forestGreen);
                }
                
                Draw.SphereOutline(currentPos, 0.3f, hasGround ? Color.forestGreen : Color.crimson);
            }
        }
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(PatrolPath))]
    public class PatrolPathEditor : UnityEditor.Editor
    {
        private int m_selectedPoint = -1;
        private bool m_hasDragAddedPoint = false;
        
        private void OnSceneGUI()
        {
            serializedObject.Update();
            var patrolPath = (PatrolPath)target;
            Undo.RecordObject(patrolPath, "Modify Patrol Path");
            
            if (patrolPath.Waypoints == null || patrolPath.Waypoints.Count == 0)
            {
                Handles.color = Color.red;
                Handles.Label(patrolPath.transform.position + Vector3.up, "No Waypoints Defined");

                {
                    {
                        Handles.BeginGUI();
                        GUIStyle labelStyle = new GUIStyle(GUI.skin.button)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = Mathf.RoundToInt(12/HandleUtility.GetHandleSize(patrolPath.transform.position)),
                            normal = { textColor = Color.white }
                        };
                        
                        if (GUI.Button(GetRectForPoint(patrolPath.transform.position + Vector3.up, new Vector2(120, 60)), "Add First Waypoint", labelStyle))
                        {
                            patrolPath.Waypoints.Add(new PatrolPoint()
                            {
                                m_position = patrolPath.transform.position + Vector3.forward,
                                m_lookAtRot = Quaternion.identity,
                                m_waitProbability = 0f,
                                m_waitDuration = 1f
                            });
                            
                            EditorUtility.SetDirty(patrolPath);
                        }
                        Handles.EndGUI();
                    }
                }
                return;
            }
            
            Event currentEvent = Event.current;

            // Reset drag added point flag if shift is released
            if (m_hasDragAddedPoint && currentEvent is { control: false })
                m_hasDragAddedPoint = false;

            for (int p = 0; p < patrolPath.Waypoints.Count; p++)
            {
                var points = patrolPath.Waypoints;
                
                // Transform gizmos
                var currentPos = points[p].m_position;
                var lookAtDir = points[p].m_lookAtRot;

                if (Handles.Button(currentPos, lookAtDir, 0.5f, 0.5f, Handles.SphereHandleCap))
                {
                    m_selectedPoint = p;
                }
                

                if (m_selectedPoint != p) continue;
                
                var newPos = Handles.PositionHandle(currentPos, lookAtDir);
                lookAtDir = Handles.RotationHandle(lookAtDir, currentPos);

                if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Delete)
                {
                    patrolPath.Waypoints.RemoveAt(p);
                    m_selectedPoint = -1;
                    EditorUtility.SetDirty(patrolPath);
                    currentEvent.Use();
                }
                if (newPos != currentPos && currentEvent is { control: true } && !m_hasDragAddedPoint)
                {
                    m_hasDragAddedPoint = true;
                    patrolPath.Waypoints.Insert(p, new PatrolPoint()
                    {
                        m_position = newPos,
                        m_lookAtRot = lookAtDir,
                        m_waitProbability = 0f,
                        m_waitDuration = 1f
                    });
                    EditorUtility.SetDirty(patrolPath);
                    
                    currentEvent.Use();
                }
                else
                {
                    points[p] = new PatrolPoint()
                    {
                        m_position = newPos,
                        m_lookAtRot = lookAtDir,
                        m_waitProbability = points[p].m_waitProbability,
                        m_waitDuration = points[p].m_waitDuration
                    };
                    
                    if (newPos != currentPos)
                        EditorUtility.SetDirty(patrolPath);
                }
                
                var settingsRect = GetRectForPoint(currentPos + Vector3.up * 2, new Vector2(300, 60));
                
                Handles.BeginGUI();
                
                SirenixEditorGUI.DrawSolidRect(settingsRect, Color.gray2);
                SirenixEditorGUI.DrawBorders(settingsRect, 2, Color.black);
                GUILayout.BeginArea(settingsRect);
                
                GUILayout.Label("Waypoint Settings", EditorStyles.boldLabel, GUILayout.Width(settingsRect.width * HandleUtility.GetHandleSize(currentPos + 2 * Vector3.up)));
                points[p] = new PatrolPoint()
                {
                    m_position = points[p].m_position,
                    m_lookAtRot = points[p].m_lookAtRot,
                    m_waitProbability = EditorGUILayout.Slider("Should Wait Probability", points[p].m_waitProbability, 0f, 1f, GUILayout.Width(settingsRect.width * HandleUtility.GetHandleSize(currentPos + 2 * Vector3.up))),
                    m_waitDuration = EditorGUILayout.Slider("Wait Duration", points[p].m_waitDuration, 0f, 5f, GUILayout.Width(settingsRect.width * HandleUtility.GetHandleSize(currentPos + 2 * Vector3.up)))
                };
                
                GUILayout.EndArea();
                
                Handles.EndGUI();
            }

            if (EditorUtility.IsDirty(patrolPath))
            {
                // trigger validate to update bounds
                patrolPath.UpdateBounds();
            }
        }

        private Rect GetRectForPoint(Vector3 pos, Vector2 size)
        {
            size /= HandleUtility.GetHandleSize(pos);
            var screenPos = HandleUtility.WorldToGUIPoint(pos);
            screenPos.x -= size.x / 2;
            
            return new Rect(screenPos, size);
        }
    }
#endif    
}