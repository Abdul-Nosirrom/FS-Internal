using System.Collections.Generic;
using FS.Editor.Timeline;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.UI.Editor
{
    public class TweenAnimationClip : TimelineTrack
    {
        public TweenAnimationHolder m_tweenAnimation;
        private PropertyTree m_propertyTree;
        
        public TweenAnimationClip(Timeline owner, TweenAnimationHolder anim) : base(owner)
        {
            m_tweenAnimation = anim;
            m_propertyTree = PropertyTree.Create(m_tweenAnimation);
            m_propertyTree.OnPropertyValueChanged += OnValueChanged;
        }

        private void OnValueChanged(InspectorProperty property, int selectionIndex)
        {
            IsDirty = true;
        }

        public override Rect ClipRect => RangedClipRect(m_tweenAnimation.StartTime, m_tweenAnimation.EndTime);
        
        public override void DrawClipTimelineTrack()
        {
            float start = m_tweenAnimation.StartTime;
            float end = m_tweenAnimation.EndTime;
            float center = (start + end) * 0.5f;

            if (DrawDefaultRangedClipSlot(ClipRect, ref start, ref center, ref end))
            {
                m_tweenAnimation.StartTime = start;
                m_tweenAnimation.EndTime = end;
                IsDirty = true;
            }
        }

        public override void DrawClipTrackContent()
        {
            var rect = ClipRect;
            rect.width = Mathf.Max(100, rect.width);
            EditorGUI.DropShadowLabel(rect, m_tweenAnimation.Animation?.GetType().Name.Replace("Tween", ""), SirenixGUIStyles.BoldLabelCentered);
        }

        public override void OnInspectorGUI()
        {
            m_propertyTree?.Draw(false);
        }

        public override void Dispose()
        {
            if (m_propertyTree != null) m_propertyTree.OnPropertyValueChanged -= OnValueChanged;
            m_propertyTree?.Dispose();
            m_propertyTree = null;
        }
    }
    
    [CustomEditor(typeof(TweenAnimator), editorForChildClasses: true)]
    public class TweenAnimationEditor : OdinEditor
    {
        private Timeline m_tweenTimeline;
        private TweenAnimator m_target;
        
        private SerializedProperty m_tweenAnimationsProp;
        private SerializedProperty m_durationProp;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_target = (TweenAnimator)target;
            
            m_tweenTimeline = new Timeline(null, OnContextClick, m_target.m_totalDuration);
            
            ValidateTimeline();

            m_tweenTimeline.OnTrackAdded += OnTrackAdded;
            m_tweenTimeline.OnTrackRemoved += OnTrackRemoved;

            m_tweenTimeline.OnPlay += OnPlay;
            m_tweenTimeline.OnPause += OnPause;
            m_tweenTimeline.OnComplete += OnStop;

            m_tweenTimeline.OnTimelineScrubbing += TimelineScrubbing;

            EditorApplication.update += Repaint;
            Undo.undoRedoEvent += OnUndoRedo;
        }

        private void OnUndoRedo(in UndoRedoInfo undo)
        {
            Debug.LogError($"Undo Performed {undo.undoName}");
            ValidateTimeline();
        }

        private void ValidateTimeline()
        {
            var data = new List<TimelineTrack>();
            // Reset the timeline to match the current state of the target's tween animations
            foreach (var tween in m_target.m_tweenAnimations)
            {
                data.Add(new TweenAnimationClip(m_tweenTimeline, tween));
            }

            m_tweenTimeline.SetData(data);
        }

        private void TimelineScrubbing(float time)
        {
            if (m_target.IsPlaying)
                m_target.ActiveSequence.elapsedTime = time;
        }

        protected override void OnDisable()
        {
            m_tweenTimeline?.Dispose();
            m_tweenTimeline = null;
            
            EditorApplication.update -= Repaint;
            Undo.undoRedoEvent -= OnUndoRedo;
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            m_durationProp ??= serializedObject.FindProperty("m_totalDuration");
            
            serializedObject.Update();
            m_durationProp.floatValue = SirenixEditorFields.RangeFloatField("Total Duration", m_durationProp.floatValue, 0.1f, 10f);
            if (serializedObject.ApplyModifiedProperties()) m_target.UpdateSequence();
            
            Undo.RecordObject(m_target, "Modify Tween Animation");
            
            m_tweenTimeline.Duration = m_target.m_totalDuration;
            Vector2 timelineSize = new Vector2(Screen.width, 300);
            m_tweenTimeline.DoGUI(timelineSize);


            if (m_tweenTimeline.IsDirty)
            {
                m_target.UpdateSequence();
                m_tweenTimeline.IsDirty = false;
                EditorUtility.SetDirty(m_target);
            }


            if (!m_target.IsPlaying) m_tweenTimeline.IsPlaying = false;
            else m_target.ActiveSequence.timeScale = m_tweenTimeline.PlaybackSpeed;
            
            if (!m_tweenTimeline.IsScrubbingThroughTimeline) m_tweenTimeline.CurrentTime = m_target.IsPlaying ? m_target.ElapsedTime : 0;
            else if (!m_target.ActiveSequence.isAlive)// Scrubbing through timeline
            {
                m_target.Play();
                m_target.ActiveSequence.isPaused = true;
            }
            
            GUILayout.Label($"Number Of Tracks: {m_target.m_tweenAnimations.Count}");
            
            if (GUILayout.Button("Reverse"))
                m_target.Reverse();
            
            //base.OnInspectorGUI();
        }
        
        private void OnPlay()
        {
            if (m_target.ActiveSequence.isAlive)
                m_target.ActiveSequence.isPaused = false;
            else
                m_target.Play();
        }

        private void OnPause()
        {
            if (m_target.ActiveSequence.isAlive)
                m_target.ActiveSequence.isPaused = true;
        }

        private void OnStop()
        {
            m_tweenTimeline.IsPlaying = false;
        }
        
        // Hmmm, no way to undo adding/removing tracks? Unless we can bind the Undo recording to the Timeline's OnTrackAdded/Removed events?
        private void OnTrackAdded(TimelineTrack obj)
        {
            var tweenClip = (TweenAnimationClip)obj;
            
            //serializedObject.Update();
            //m_tweenAnimationsProp.arraySize++;
            //m_tweenAnimationsProp.GetArrayElementAtIndex(m_tweenAnimationsProp.arraySize - 1).boxedValue = tweenClip?.m_tweenAnimation;
            //serializedObject.ApplyModifiedProperties();

            Undo.RecordObject(m_target, "Add Tween Animation");
            m_target.m_tweenAnimations.Add(tweenClip?.m_tweenAnimation);
            EditorUtility.SetDirty(m_target);

            m_tweenTimeline.IsDirty = false;
        }
        
        private void OnTrackRemoved(TimelineTrack obj)
        {
            var tweenClip = (TweenAnimationClip)obj;
            
            //serializedObject.Update();
            //var index = m_target.m_tweenAnimations.IndexOf(tweenClip?.m_tweenAnimation);
            //if (index >= 0 && index < m_tweenAnimationsProp.arraySize)
            //{
            //    m_tweenAnimationsProp.DeleteArrayElementAtIndex(index);
            //    serializedObject.ApplyModifiedProperties();
            //}
            
            Undo.RecordObject(m_target, "Remove Tween Animation");
            m_target.m_tweenAnimations.Remove(tweenClip.m_tweenAnimation);
            EditorUtility.SetDirty(m_target);
            
            m_tweenTimeline.IsDirty = false;
        }


        private void OnContextClick(Vector2 clickPos, Rect contentRect)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Tween"), false, () =>
            {
                float normalizedPos = (clickPos.x - contentRect.x) / contentRect.width;
                var newTween = new TweenAnimationHolder()
                {
                    StartTime = Mathf.Clamp01(normalizedPos - 0.1f),
                    EndTime = Mathf.Clamp01(normalizedPos + 0.1f),
                };
                var newClip = new TweenAnimationClip(m_tweenTimeline, newTween);
                m_tweenTimeline.AddClip(newClip);
            });
            menu.ShowAsContext();
            
        }
    }
}