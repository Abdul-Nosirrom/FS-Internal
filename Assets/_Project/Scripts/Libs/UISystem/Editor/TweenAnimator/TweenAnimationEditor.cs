// ============================================================================
// TweenAnimator integration with the generic timeline editor.
// ============================================================================

using System;
using System.Linq;
using FS.Editor.Timeline;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.UI.Editor
{
    // ========================================================================
    // ITimelineElement adapter for TweenAnimationHolder
    // ========================================================================
    public class TweenTimelineElement : ITimelineElement
    {
        private readonly TweenAnimationHolder m_holder;
        private readonly float m_totalDuration;

        public TweenAnimationHolder Holder => m_holder;

        public TweenTimelineElement(TweenAnimationHolder holder, float totalDuration)
        {
            m_holder = holder;
            m_totalDuration = totalDuration;
        }

        // --- ITimelineElement ---
        public string Label => m_holder.Animation?.GetType().Name.Replace("Tween", "") ?? "None";
        public bool IsMarker => false;

        public float StartTime
        {
            get => m_holder.StartTime * m_totalDuration;
            set => m_holder.StartTime = Mathf.Clamp01(value / m_totalDuration);
        }

        public float EndTime
        {
            get => m_holder.EndTime * m_totalDuration;
            set => m_holder.EndTime = Mathf.Clamp01(value / m_totalDuration);
        }

        public bool IsActive => m_holder.Animation != null;
        public Texture2D Icon => null;
        public int StableId => m_holder.GetHashCode();

        public override bool Equals(object obj) =>
            obj is TweenTimelineElement other && ReferenceEquals(m_holder, other.m_holder);

        public override int GetHashCode() => m_holder?.GetHashCode() ?? 0;
    }

    // ========================================================================
    // Custom Editor
    // ========================================================================
    [CustomEditor(typeof(TweenAnimator), editorForChildClasses: true)]
    public class TweenAnimationEditor : OdinEditor
    {
        #region State

        private TweenAnimator m_target;
        private TimelineView m_view;
        private TimelineSelection m_selection;
        private TweenPreviewManager m_preview;
        private TweenTimelineElement[] m_elements;
        private PropertyTree m_selectedPropertyTree;

        private bool m_bIsPlaying;
        private bool m_bIsLooping;

        /// <summary>
        /// Whether the playhead is paused at a specific position (scrub result or explicit pause).
        /// Distinct from "not playing" — paused means we're in preview with a visible playhead.
        /// </summary>
        private bool m_bIsPaused;

        /// <summary>
        /// True when any preview-worthy interaction is happening (play, scrub, pause).
        /// Drives the playhead visibility and preview eye icon.
        /// </summary>
        private bool IsShowingPlayhead => m_bIsPlaying || m_bIsPaused;

        #endregion

        #region Lifecycle

        public override bool RequiresConstantRepaint() => true;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_target = (TweenAnimator)target;

            m_view = new TimelineView();
            m_selection = new TimelineSelection();
            m_preview = new TweenPreviewManager(m_target);

            m_view.IsSnapping = EditorPrefs.GetBool("FS.Timeline.Snap", true);
            m_view.FixedDuration = m_target.m_totalDuration;

            // Timeline events
            m_view.ElementSelected += OnElementSelected;
            m_view.ElementDrag += OnElementDrag;
            m_view.ElementDragEnd += OnElementDragEnd;

            m_view.TimeDrag += OnTimeDrag;
            m_view.TimeDragEnd += OnTimeDragEnd;
            m_view.PreviewDisabled += OnPreviewDisabled;

            m_view.AddClicked += OnAddAnimation;
            m_view.RemoveClicked += OnRemove;
            m_view.DuplicateClicked += OnDuplicate;

            m_view.PlayClicked += OnPlay;
            m_view.StopClicked += OnStop;
            m_view.LoopToggled += v => m_bIsLooping = v;
            m_view.SnapToggled += () => EditorPrefs.SetBool("FS.Timeline.Snap", m_view.IsSnapping);

            EditorApplication.update += Repaint;
            Undo.undoRedoEvent += OnUndoRedo;
        }

        protected override void OnDisable()
        {
            // Exit preview and restore state when inspector loses focus
            m_preview.Dispose();

            m_view.ElementSelected -= OnElementSelected;
            m_view.ElementDrag -= OnElementDrag;
            m_view.ElementDragEnd -= OnElementDragEnd;
            m_view.TimeDrag -= OnTimeDrag;
            m_view.TimeDragEnd -= OnTimeDragEnd;
            m_view.PreviewDisabled -= OnPreviewDisabled;
            m_view.AddClicked -= OnAddAnimation;
            m_view.RemoveClicked -= OnRemove;
            m_view.DuplicateClicked -= OnDuplicate;
            m_view.PlayClicked -= OnPlay;
            m_view.StopClicked -= OnStop;

            m_selectedPropertyTree?.Dispose();
            m_selectedPropertyTree = null;

            m_bIsPlaying = false;
            m_bIsPaused = false;

            EditorApplication.update -= Repaint;
            Undo.undoRedoEvent -= OnUndoRedo;
            base.OnDisable();
        }

        private void OnUndoRedo(in UndoRedoInfo undo)
        {
            m_selectedPropertyTree?.Dispose();
            m_selectedPropertyTree = null;
        }

        #endregion

        #region Inspector Drawing

        public override void OnInspectorGUI()
        {
            // Duration field
            var durationProp = serializedObject.FindProperty("m_totalDuration");
            serializedObject.Update();
            durationProp.floatValue = SirenixEditorFields.RangeFloatField(
                "Total Duration", durationProp.floatValue, 0.1f, 10f);

            if (serializedObject.ApplyModifiedProperties())
                m_target.UpdateSequence();

            m_view.FixedDuration = m_target.m_totalDuration;

            // Build element array from current data
            m_elements = m_target.m_tweenAnimations
                .Select(t => new TweenTimelineElement(t, m_target.m_totalDuration))
                .ToArray();

            m_selection.Validate(m_elements);

            // If playing and the sequence died (finished naturally), sync state
            if (m_bIsPlaying && !m_target.ActiveSequence.isAlive)
            {
                m_bIsPlaying = false;
                m_bIsPaused = false;
            }

            // Current playhead time
            float displayTime = 0f;
            if (m_target.ActiveSequence.isAlive)
                displayTime = m_target.ActiveSequence.elapsedTime;

            // Draw the timeline
            m_view.DrawTimeline(m_elements, m_selection.Selected,
                IsShowingPlayhead, displayTime, m_bIsLooping, m_bIsPaused);

            // Inspector for selected element
            if (m_selection.Selected is TweenTimelineElement selectedTween)
            {
                m_view.DrawInspector("Inspector", () =>
                {
                    if (m_selectedPropertyTree == null ||
                        !ReferenceEquals(m_selectedPropertyTree.WeakTargets[0], selectedTween.Holder))
                    {
                        m_selectedPropertyTree?.Dispose();
                        m_selectedPropertyTree = PropertyTree.Create(selectedTween.Holder);
                    }

                    m_selectedPropertyTree.Draw(false);
                });
            }

            GUILayout.Label($"Number Of Tracks: {m_target.m_tweenAnimations.Count}");
            if (GUILayout.Button("Reverse")) m_target.Reverse();
        }

        #endregion

        #region Selection

        private void OnElementSelected(ITimelineElement element)
        {
            m_selection.Set(element);
            GUIUtility.keyboardControl = 0;

            m_selectedPropertyTree?.Dispose();
            m_selectedPropertyTree = null;
        }

        #endregion

        #region Element Dragging

        private void OnElementDrag(float newStart, float newEnd, ElementDragMode mode)
        {
            if (m_selection.Selected is not TweenTimelineElement sel) return;

            Undo.RecordObject(m_target, "Drag Tween");

            switch (mode)
            {
                case ElementDragMode.Center:
                    float duration = newEnd - newStart;
                    newStart = Mathf.Clamp(newStart, 0, m_target.m_totalDuration - duration);
                    sel.StartTime = newStart;
                    sel.EndTime = newStart + duration;
                    break;

                case ElementDragMode.StartEdge:
                    newStart = Mathf.Clamp(newStart, 0, sel.EndTime - 0.01f);
                    sel.StartTime = newStart;
                    break;

                case ElementDragMode.EndEdge:
                    newEnd = Mathf.Clamp(newEnd, sel.StartTime + 0.01f, m_target.m_totalDuration);
                    sel.EndTime = newEnd;
                    break;
            }

            EditorUtility.SetDirty(m_target);

            // If we're in preview, rebuild so the user sees the timing change applied
            if (m_preview.IsInPreview)
            {
                m_preview.RebuildSequence();
            }
        }

        private void OnElementDragEnd()
        {
            Undo.FlushUndoRecordObjects();
        }

        #endregion

        #region Time Scrubbing

        private void OnTimeDrag(float time)
        {
            m_preview.ScrubTo(time);

            // Scrubbing always puts us in paused preview state
            m_bIsPlaying = false;
            m_bIsPaused = true;
        }

        private void OnTimeDragEnd(Event mouseEvent)
        {
            // Right/middle click: cancel preview entirely
            if (mouseEvent.button is 1 or 2)
            {
                OnPreviewDisabled();
                return;
            }

            // Left click release: stay paused at the scrub position
            m_bIsPaused = true;
        }

        #endregion

        #region Transport Controls

        private void OnPlay()
        {
            m_preview.Play();
            m_bIsPlaying = true;
            m_bIsPaused = false;
        }

        /// <summary>
        /// Stop: exit preview entirely and restore original state.
        /// </summary>
        private void OnStop()
        {
            if (m_bIsPaused && !m_bIsPlaying) // TEMP: Hack to get playback after scrubbing through
            {
                OnPlay();
                return;
            }
            m_preview.ExitPreview();
            m_bIsPlaying = false;
            m_bIsPaused = false;
        }

        /// <summary>
        /// Eye icon or explicit preview disable: exit and restore.
        /// </summary>
        private void OnPreviewDisabled()
        {
            m_preview.ExitPreview();
            m_bIsPlaying = false;
            m_bIsPaused = false;
        }

        #endregion

        #region Add / Remove / Duplicate

        private void OnAddAnimation()
        {
            Undo.RecordObject(m_target, "Add Tween Animation");
            m_target.m_tweenAnimations.Add(new TweenAnimationHolder
            {
                StartTime = 0f,
                EndTime = 0.2f
            });
            EditorUtility.SetDirty(m_target);
        }

        private void OnRemove()
        {
            if (m_selection.Selected is not TweenTimelineElement tweenElement) return;
            Undo.RecordObject(m_target, "Remove Tween Animation");
            m_target.m_tweenAnimations.Remove(tweenElement.Holder);
            m_selection.Clear();
            EditorUtility.SetDirty(m_target);
        }

        private void OnDuplicate()
        {
            if (m_selection.Selected is not TweenTimelineElement tweenElement) return;
            Undo.RecordObject(m_target, "Duplicate Tween Animation");
            m_target.m_tweenAnimations.Add(new TweenAnimationHolder
            {
                StartTime = tweenElement.Holder.StartTime,
                EndTime = tweenElement.Holder.EndTime,
            });
            EditorUtility.SetDirty(m_target);
        }

        #endregion
    }
}