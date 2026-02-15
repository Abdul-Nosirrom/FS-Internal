// ============================================================================
// Manages editor preview state for TweenAnimator: state capture/restore,
// lazy sequence creation, and scrubbing.
// ============================================================================

using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FS.UI.Editor
{
    /// <summary>
    /// Handles the preview lifecycle for the TweenAnimator editor.
    /// Any timeline interaction (scrub, play, drag) auto-enters preview mode which:
    ///   1. Snapshots all tween target objects via JSON serialization
    ///   2. Creates a paused PrimeTween sequence for scrubbing
    ///   3. Restores original state on exit by deserializing the snapshots
    ///
    /// State capture is decoupled from Unity's Undo system so that intentional user edits
    /// (element timing changes, add/remove) are preserved through the normal Undo stack.
    /// </summary>
    public class TweenPreviewManager
    {
        #region State

        private TweenAnimator m_target;
        private bool m_bInPreview;

        /// <summary>
        /// Serialized JSON snapshots of each target object, keyed by instance ID.
        /// Captured on preview enter, restored on preview exit.
        /// </summary>
        private Dictionary<int, ObjectSnapshot> m_snapshots = new();

        public bool IsInPreview => m_bInPreview;

        private struct ObjectSnapshot
        {
            public Object Target;
            public string Json;
        }

        #endregion

        #region Lifecycle

        public TweenPreviewManager(TweenAnimator target)
        {
            m_target = target;
        }

        /// <summary>
        /// Call on editor disable / cleanup. Exits preview if active.
        /// </summary>
        public void Dispose()
        {
            if (m_bInPreview)
                ExitPreview();
        }

        #endregion

        #region Preview Enter / Exit

        /// <summary>
        /// Enters preview mode if not already in it.
        /// Snapshots all tween target objects so we can restore them later.
        /// Creates a paused sequence ready for scrubbing.
        /// </summary>
        public void EnsurePreview()
        {
            if (m_bInPreview) return;
            m_bInPreview = true;

            // Snapshot all target objects for state restoration
            CaptureTargetStates();

            // Create a paused sequence so scrubbing works immediately
            EnsureSequenceAlive();
            m_target.ActiveSequence.isPaused = true;
        }

        /// <summary>
        /// Exits preview mode and restores all objects to their pre-preview state.
        /// Does NOT touch the Undo stack — user edits to timing data are preserved.
        /// </summary>
        public void ExitPreview()
        {
            if (!m_bInPreview) return;

            // Kill any active sequence first so PrimeTween doesn't hold references
            if (m_target.ActiveSequence.isAlive)
                m_target.ActiveSequence.Stop();

            // Restore all target objects from their snapshots
            RestoreTargetStates();

            m_bInPreview = false;
        }

        #endregion

        #region Sequence Management

        /// <summary>
        /// Ensures the PrimeTween sequence exists (creates if dead).
        /// Call before any scrub or play operation.
        /// </summary>
        public void EnsureSequenceAlive()
        {
            if (m_target.ActiveSequence.isAlive) return;

            m_target.Play();
            m_target.ActiveSequence.isPaused = true;
        }

        /// <summary>
        /// Rebuilds the sequence (e.g. after element drag changes timing).
        /// Preserves current elapsed time.
        /// </summary>
        public void RebuildSequence()
        {
            m_target.UpdateSequence();
        }

        /// <summary>
        /// Scrubs the preview to an absolute time. Auto-enters preview if needed.
        /// </summary>
        public void ScrubTo(float time)
        {
            EnsurePreview();
            EnsureSequenceAlive();
            m_target.ActiveSequence.isPaused = true;
            m_target.ActiveSequence.elapsedTime = Mathf.Clamp(time, 0f, m_target.m_totalDuration);
        }

        /// <summary>
        /// Starts playback from the current position. Auto-enters preview if needed.
        /// </summary>
        public void Play()
        {
            EnsurePreview();
            EnsureSequenceAlive();
            m_target.ActiveSequence.isPaused = false;
        }

        /// <summary>
        /// Pauses playback without exiting preview (keeps state for continued scrubbing).
        /// </summary>
        public void Pause()
        {
            if (!m_target.ActiveSequence.isAlive) return;
            m_target.ActiveSequence.isPaused = true;
        }

        #endregion

        #region State Capture / Restore

        /// <summary>
        /// Serializes all unique target objects referenced by the tweens.
        /// </summary>
        private void CaptureTargetStates()
        {
            m_snapshots.Clear();

            foreach (var holder in m_target.m_tweenAnimations)
            {
                if (holder.Animation == null) continue;

                var targetObj = GetTargetViaReflection(holder.Animation);
                if (targetObj == null) continue;

                // For Components, snapshot the component and its Transform
                // (position/rotation/scale changes always go through Transform)
                if (targetObj is Component comp)
                {
                    SnapshotObject(comp);

                    if (comp is not Transform)
                        SnapshotObject(comp.transform);
                }
                else
                {
                    SnapshotObject(targetObj);
                }
            }
        }

        private void SnapshotObject(Object obj)
        {
            if (obj == null) return;

            int id = obj.GetInstanceID();
            if (m_snapshots.ContainsKey(id)) return;

            m_snapshots[id] = new ObjectSnapshot
            {
                Target = obj,
                Json = EditorJsonUtility.ToJson(obj)
            };
        }

        /// <summary>
        /// Restores all captured objects from their JSON snapshots.
        /// </summary>
        private void RestoreTargetStates()
        {
            foreach (var snapshot in m_snapshots.Values)
            {
                if (snapshot.Target == null) continue;

                EditorJsonUtility.FromJsonOverwrite(snapshot.Json, snapshot.Target);
                EditorUtility.SetDirty(snapshot.Target);
            }

            m_snapshots.Clear();
            SceneView.RepaintAll();
        }

        #endregion

        #region Target Collection

        /// <summary>
        /// Finds the "Target" field on a TweenAnimation instance via reflection.
        /// Walks up the type hierarchy to handle both the generic base and manual declarations.
        /// </summary>
        private static Object GetTargetViaReflection(TweenAnimation animation)
        {
            var type = animation.GetType();

            while (type != null && type != typeof(object))
            {
                var field = type.GetField("Target",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                if (field != null)
                {
                    var value = field.GetValue(animation);
                    return value as Object;
                }

                type = type.BaseType;
            }

            return null;
        }

        #endregion
    }
}