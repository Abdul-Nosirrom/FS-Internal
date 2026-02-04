#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using Animancer;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using FS.Animation.Editor.Internals;

namespace FS.Animation.Editor
{
    public class AnimationPreviewRender : AdvancedPreviewRenderUtility
    {
        public TimeControlAPI m_timeControl = new TimeControlAPI();
        
        private List<EditorPreviewSimulator> m_previewables = new();
        
        private const string k_speedPref = "FS.AnimationPreview.Speed";
        private const int fps = 60; // Assuming a fixed FPS for the preview

        public GameObject InstantiatePreviewable(GameObject prefab)
        {
            if (prefab == null) return null;
            var preview = GameObject.Instantiate(prefab);
            preview.transform.position = Vector3.zero;
            preview.transform.rotation = Quaternion.identity;
            preview.hideFlags = HideFlags.HideAndDontSave;
            preview.name = $"{prefab.name}_Preview";
            
            // Preview updater component
            var previewComponent = preview.AddComponent<EditorPreviewSimulator>();
            previewComponent.InvocationTime = m_timeControl.currentTime;
            previewComponent.PlaybackTime = m_timeControl.currentTime;

            // Add it to the preview render
            PreviewScene.AddSingleGO(preview);
            
            m_previewables.Add(previewComponent);
            return preview;
        }

        private AnimancerState m_cachedState = null;
        public void SyncAnimancerStateWithTimeControl(AnimancerState state)
        {
            if (state == null) return;

            // We cache our states in case our preview involves playing multiple states at once and swaps between them, so 
            // we try to do this to ensure accuracy of time display
            if (m_cachedState != state)
            {
                m_cachedState = state;
                m_timeControl.currentTime = state.Time;
            }
            
            if (Event.current.type == EventType.Repaint)
            {
                // Ensure TimeControl start/end times are valid
                m_timeControl.startTime = 0;
                m_timeControl.stopTime = state.Length;// / state.Speed;

                // Let it play nice w/ the playback speed
                // Because we've gotta manually update the time control, we're not taking into account the linear mixer individual clip speeds
                float effectiveSpeed = GetEffectiveAnimancerStateSpeed(state);
                var cachedPlayerSpeed = m_timeControl.playbackSpeed;
                m_timeControl.playbackSpeed = effectiveSpeed * m_timeControl.playbackSpeed;
                m_timeControl.Update();
                m_timeControl.playbackSpeed = cachedPlayerSpeed;

                if (!m_timeControl.currentTime.IsFinite())
                {
                    m_timeControl.currentTime = 0;
                    state.Time = 0;
                }
 
                UpdatePreviewables();

                // TODO: No support for playing backwards at the moment (negative speeds)
                float newTime = Mathf.Repeat(state.NormalizedTime + m_timeControl.deltaTime/state.Length, 1f);
                newTime = state.Time + m_timeControl.deltaTime;
                
                // NOTE: We want to update the time this way rather than set it to currentTime so loop counts properly function as they may be used in events
                if (m_timeControl.deltaTime < 0 || (newTime < state.Time)) // Scrubbing backwards, or looped around (moveTime would trigger all events backwards)
                    state.Time = newTime;
                else
                {
                    state.MoveTime(newTime, false);
                    // TODO: Below needs to be adjusted to get EndEvents to trigger
                }
                
                // Ensure we wrap around
                if (state.NormalizedTime is >= 1 or <= 0)
                    state.NormalizedTime = Mathf.Repeat(state.NormalizedTime, 1f);
                
                
                // Ensure times are synced
                m_timeControl.normalizedTime = state.NormalizedTime;
            }
        }

        private float GetEffectiveAnimancerStateSpeed(AnimancerState state)
        {
            if (state is ManualMixerState manualMixer)
                return GetHarmonicBlendedSpeed(manualMixer) * manualMixer.Speed;
            
            return state.Speed;
        }
        
        /// <summary>
        /// Get the effective speed of a manual mixer by blending the child speeds harmonically based on their weights.
        /// Meaning we blend the durations instead of speeds - ensuring all clips are at the same normalized time, this is how
        /// playback speed should be calculated for mixers.
        /// - Convert speeds to durations (reciprocal 1/speed)
        /// - Blend the durations: weighted average
        /// - Convert back to speed (reciprocal of blended duration 1/blendedDUration)
        /// </summary>
        private float GetHarmonicBlendedSpeed(ManualMixerState mixer)
        {
            float totalWeight = 0f;
            float weightedInverseSpeed = 0f;
    
            for (int i = 0; i < mixer.ChildCount; i++)
            {
                var child = mixer.GetChild(i);
                if (child != null && child.Speed > 0)
                {
                    float weight = child.EffectiveWeight;
                    totalWeight += weight;
                    // Blend the reciprocal (duration) instead of speed
                    weightedInverseSpeed += weight / child.Speed;
                }
            }
    
            if (totalWeight > 0 && weightedInverseSpeed > 0)
            {
                // Convert back from duration to speed
                return totalWeight / weightedInverseSpeed;
            }
    
            return 1f;
        }

        private void UpdatePreviewables()
        {
            for (int p = m_previewables.Count - 1; p >= 0; p--)
            {
                var previewable = m_previewables[p];
                if (previewable == null || previewable.gameObject == null)
                {
                    m_previewables.RemoveAt(p);
                    continue;
                }
                
                // Update the invocation time and playback time
                previewable.PlaybackTime = m_timeControl.currentTime;
            }
        }
        
        protected override void DoPreviewWindowControls(ref Rect previewRect)
        {
            const float k_timeControlRectHeight = 20f;
            const float k_sliderWidth = 150f;
            const float k_spacing = 4f;

            Rect rect = previewRect;
            Rect timeControlRect = rect;

            // background
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);

            timeControlRect.height = k_timeControlRectHeight;
            timeControlRect.xMax -= k_sliderWidth;

            Rect sliderControlRect = rect;
            sliderControlRect.height = k_timeControlRectHeight;
            sliderControlRect.yMin += 1;
            sliderControlRect.yMax -= 1;
            sliderControlRect.xMin = sliderControlRect.xMax - k_sliderWidth + k_spacing;

            m_timeControl.DoTimeControl(timeControlRect);
            Rect labelRect = new Rect(new Vector2(rect.x, rect.y), AnimationEditorUtility.toolbarLabel.CalcSize(EditorGUIUtility.TrTempContent("xxxxxx")));;
            labelRect.x = rect.xMax - labelRect.width;
            labelRect.yMin = rect.yMin;
            labelRect.yMax = rect.yMax;

            sliderControlRect.xMax = labelRect.xMin;

            EditorGUI.BeginChangeCheck();
            m_timeControl.playbackSpeed = PreviewSlider(sliderControlRect, m_timeControl.playbackSpeed, 0.03f);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetFloat(k_speedPref, m_timeControl.playbackSpeed);
            GUI.Label(labelRect, m_timeControl.playbackSpeed.ToString("f2", CultureInfo.InvariantCulture.NumberFormat) + "x", AnimationEditorUtility.toolbarLabel);
            
            // Adjust input rect for the next controls
            previewRect.y += k_timeControlRectHeight;
        }
        
        public static readonly Texture2D Logo = EditorGUIUtility.Load("Free Skies/logo.png") as Texture2D;
        
        protected override void DoGUIOnPreviewRect(Rect rect)
        {
            var logoRect = rect;
            logoRect.height = 64;
            GUI.Label(logoRect, Logo);
            
            // Show current time in seconds:frame and in percentage in the actual preview area
            rect.y = rect.yMax - 50;
            float time = m_timeControl.currentTime - m_timeControl.startTime;
            EditorGUI.DropShadowLabel(new Rect(rect.x, rect.y, rect.width, 20),
                string.Format("{0,2}:{1:00} ({2:000.0%}) Frame {3}", (int)time, Mathf.Repeat(Mathf.FloorToInt(time * fps), fps), m_timeControl.normalizedTime, Mathf.FloorToInt(m_timeControl.currentTime * fps))
            );

            //rect.height = 20;
            //GUI.Button(rect, "SOME BUTTON!");
        }

        float PreviewSlider(Rect rect, float val, float snapThreshold)
        {
            val = GUI.HorizontalSlider(rect, val, 0.1f, 2.0f, Styles.preSlider, Styles.preSliderThumb);//, GUILayout.MaxWidth(64));
            if (val > 0.25f - snapThreshold && val < 0.25f + snapThreshold)
                val = 0.25f;
            else if (val > 0.5f - snapThreshold && val < 0.5f + snapThreshold)
                val = 0.5f;
            else if (val > 0.75f - snapThreshold && val < 0.75f + snapThreshold)
                val = 0.75f;
            else if (val > 1.0f - snapThreshold && val < 1.0f + snapThreshold)
                val = 1.0f;
            else if (val > 1.25f - snapThreshold && val < 1.25f + snapThreshold)
                val = 1.25f;
            else if (val > 1.5f - snapThreshold && val < 1.5f + snapThreshold)
                val = 1.5f;
            else if (val > 1.75f - snapThreshold && val < 1.75f + snapThreshold)
                val = 1.75f;

            return val;
        }

        protected override void SetupBounds()
        {
            throw new System.NotImplementedException();
        }

        public event Func<Object, bool> AcceptDragObject;
        protected override bool ShouldAcceptDragObject(Object newPreviewObject)
        {
            return AcceptDragObject?.Invoke(newPreviewObject) ?? false;
        }
    }
}
#endif
