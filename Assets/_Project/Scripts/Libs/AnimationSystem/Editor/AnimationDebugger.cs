using System;
using System.Collections.Generic;
using System.Linq;
using Animancer;
using FS.Editor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.Animation.Editor
{
    public class AnimationDebugger : EditorWindow
    {
        [MenuItem("Free Skies/Animation/Animation Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<AnimationDebugger>();
            window.titleContent = new GUIContent("Animation Debugger");
            window.Show();
        }

        private AnimationPreviewRender m_animationPreviewRender;
        
        private void OnEnable()
        {
            m_animationPreviewRender = new AnimationPreviewRender();

            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            if (m_animationPreviewRender != null)
            {
                m_animationPreviewRender.Dispose();
                m_animationPreviewRender = null;
            }
            
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            using (var mainVertical = new EditorGUILayout.VerticalScope())
            {
                var logoRect = FreeSkiesEditor.DrawLogo(200f, SirenixGUIStyles.BoxContainer);

                if (!Application.isPlaying)
                {
                    GUILayout.Label("Game is not running");
                    DrawAnimationPreview(position.height, position.width);
                    return;
                }

                if (m_animator == null && m_previewObjectInstance != null)
                {
                    DestroyImmediate(m_previewObjectInstance);
                    m_previewAnimator = null;
                }
                
                // Dropdown for animator selectors
                DoAnimatorSelector();

                // If the window is wider than it is taller, lets display them side by side
                if (position.width > position.height)
                {
                    using (var horizontalScope = new EditorGUILayout.HorizontalScope())
                    {
                        // Display preview window
                        DrawAnimationPreview(position.height, position.width * 0.5f);

                        // Display animator state & layer info
                        DisplayAnimatorInfo();
                    }
                }
                else
                {
                    // Display animator state & layer info
                    DisplayAnimatorInfo();

                    GUILayout.FlexibleSpace();
                    
                    // Display preview window
                    DrawAnimationPreview(position.width * 0.65f, position.width);
                }
            }
        }

        // Runtime animator
        private Animator m_animator;
        // Runtime FSAnimator
        private FSAnimator m_fsAnimator;

        private GameObject m_previewObjectInstance;
        private Animator m_previewAnimator;
        
        private void DoAnimatorSelector()
        {
            var rect = EditorGUILayout.GetControlRect(false, 32f);
            var label = m_animator == null ? "Select Animator" : m_animator.gameObject.name;
            OdinSelector<Animator>.DrawSelectorDropdown(rect, label, inputRect =>
            {
                var animators = FindObjectsByType<Animator>(FindObjectsSortMode.None);

                if (!m_animator) // initialize to default
                    AnimatorSelectionConfirmed(animators);
                

                var selector = new GenericSelector<Animator>("", false, animators.Select(x => new GenericSelectorItem<Animator>(x.gameObject.name, x)));
                selector.SelectionTree.Config.DrawScrollView = true;
                selector.EnableSingleClickToSelect();

                selector.SelectionConfirmed += AnimatorSelectionConfirmed;
                selector.SetSelection(m_animator);

                selector.ShowInPopup(inputRect);
                
                return selector;
            });
        }
        
        private void AnimatorSelectionConfirmed(IEnumerable<Animator> selectedAnimator)
        {
            if (selectedAnimator == null)
            {
                m_animator = null;
                m_fsAnimator = null;
                return;
            }
            
            m_animator = selectedAnimator.FirstOrDefault();
            if (m_animator == null) return;
            
            m_fsAnimator = m_animator.GetComponent<FSAnimator>();

            InitializePreviewGameObject();
        }

        private Vector2 m_animInfoScrollPos;
        private void DisplayAnimatorInfo()
        {
            if (m_fsAnimator == null)
            {
                GUILayout.Label("No FSAnimator selected.");
                return;
            }
            
            //using var infoScope = new EditorGUILayout.VerticalScope(SirenixGUIStyles.BoxContainer);
            using var infoScope =
                new EditorGUILayout.ScrollViewScope(m_animInfoScrollPos, SirenixGUIStyles.BoxContainer);

            foreach (var layer in m_fsAnimator.Layers)
            {
                using var layerScope = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);

                //using (var layerDisplay = new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.DropShadowLabel(EditorGUILayout.GetControlRect(), $"{layer.DebugName}: [Target Wieght: {layer.TargetWeight}]");
                    SirenixEditorFields.ProgressBarField(layer.Weight, 0f, 1f, ProgressBarConfig.Default);
                }

                //using (var fadeGroupScopeVertical = new EditorGUILayout.HorizontalScope())
                {
                    var fadeGroup = layer.FadeGroup;
                    SirenixEditorGUI.HorizontalLineSeparator(2);
                    var fadeDescr = fadeGroup.GetDescription();
                    float lineHeight = (1 + fadeDescr.Split('\n').Length) * 10f;
                    var fadeGroupRect = EditorGUILayout.GetControlRect(true, lineHeight);
                    SirenixEditorGUI.DrawRoundRect(fadeGroupRect, Color.gray1, 5f);
                    EditorGUI.DropShadowLabel(fadeGroupRect, $"[Fade Group] {fadeDescr}");
                }

                SirenixEditorGUI.HorizontalLineSeparator(2);
                
                using var stateColumns = new EditorGUILayout.HorizontalScope();
                var stateProgressBar = new ProgressBarConfig(24, Color.darkGreen, Color.darkRed, false, TextAlignment.Center);
                foreach (var activeState in layer.ActiveStates)
                {
                    if (activeState == null) continue;

                    using var stateBox = new EditorGUILayout.HorizontalScope();

                    using (var stateColumn = new EditorGUILayout.VerticalScope(GUILayout.Width(360f)))
                    {
                        EditorGUI.DropShadowLabel(EditorGUILayout.GetControlRect(), activeState.ToString());

                        EditorGUI.DropShadowLabel(EditorGUILayout.GetControlRect(), $"Time: {activeState.Time:F2}s");
                        EditorGUI.DropShadowLabel(EditorGUILayout.GetControlRect(),
                            $"Target Weight: {activeState.TargetWeight:F2}s");
                    }

                    using (var progressColumn = new EditorGUILayout.VerticalScope())
                    {
                        // Display the state progress bar
                        //var stateProgressBarRect = EditorGUILayout.GetControlRect(false, 24f);
                        //SirenixEditorFields.ProgressBarField(stateProgressBarRect, activeState.Weight, 0f, 1f, stateProgressBar);
                        EditorGUILayout.GetControlRect();
                        SirenixEditorFields.ProgressBarField(activeState.Weight, 0f, 1f, stateProgressBar);
                    }
                }

                if (layer.TargetWeight == 0) continue;
                
                m_animationPreviewRender.m_timeControl.currentTime = layer.CurrentState.Time;
                m_animationPreviewRender.m_timeControl.normalizedTime = layer.CurrentState.NormalizedTime;
                m_animationPreviewRender.m_timeControl.startTime = 0;
                m_animationPreviewRender.m_timeControl.stopTime = layer.CurrentState.Duration;

                m_animationPreviewRender.m_timeControl.loop = true;
                m_animationPreviewRender.m_timeControl.playing = true;
                m_animationPreviewRender.m_timeControl.Update();
            }
            
        }

        private (Transform source, Transform target)[] m_bonePairsToCopy;
        private void InitializePreviewGameObject()
        {
            if (m_previewObjectInstance)
                DestroyImmediate(m_previewObjectInstance);

            m_previewObjectInstance = AnimationEditorUtility.InstantiatePreview(m_animator.gameObject).gameObject;
            m_previewObjectInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            m_previewObjectInstance.transform.localScale = Vector3.one;

            if (m_previewObjectInstance.TryGetComponent(out Animator animator))
                m_previewAnimator = animator;
            if (m_previewObjectInstance.TryGetComponent(out FSAnimator fsAnimator))
                fsAnimator.enabled = false;

            m_animationPreviewRender.PreviewScene.AddSingleGO(m_previewObjectInstance);
            
            // initialize bone pairs for copying
            var sourceTransforms = m_animator.GetComponentsInChildren<Transform>();
            var targetLookup = m_previewObjectInstance.GetComponentsInChildren<Transform>()
                .ToDictionary(t => t.name, t => t);
    
            m_bonePairsToCopy = sourceTransforms
                .Where(source => targetLookup.ContainsKey(source.name))
                .Select(source => (source, targetLookup[source.name]))
                .ToArray();
        }
        
        private void DrawAnimationPreview(float height, float width)
        {
            if (m_previewAnimator != null && m_animator != null)
            {
                foreach (var (source, target) in m_bonePairsToCopy)
                {
                    target.localPosition = source.localPosition;
                    target.localRotation = source.localRotation;
                    target.localScale = source.localScale;
                }
            }

            // Display the preview render
            var previewRect = EditorGUILayout.GetControlRect(false, height, GUILayout.Width(width));
            m_animationPreviewRender.DoRenderPreview(previewRect, null);//, GUIStyles.GUIStyles.HelpBox);
        }
        
        
        private void CopyTransformHierarchy(Transform source, Transform target)
        {
            // Copy local transform data (this is key - LOCAL not world!)
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
    
            // Recursively copy all children
            int childCount = Mathf.Min(source.childCount, target.childCount);
            for (int i = 0; i < childCount; i++)
            {
                Transform sourceChild = source.GetChild(i);
                Transform targetChild = target.GetChild(i);
        
                // If names match, copy recursively
                if (sourceChild.name == targetChild.name)
                {
                    CopyTransformHierarchy(sourceChild, targetChild);
                }
                else
                {
                    // Try to find matching child by name
                    Transform matchingChild = target.Find(sourceChild.name);
                    if (matchingChild != null)
                    {
                        CopyTransformHierarchy(sourceChild, matchingChild);
                    }
                }
            }
        }
    }
}