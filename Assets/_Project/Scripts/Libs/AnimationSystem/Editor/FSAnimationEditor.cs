using System.Collections.Generic;
using System.Linq;
using Animancer;
using FS.Animation.Editor.Internals;
using FS.Audio;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.Animation.Editor
{
    [CustomEditor(typeof(FSAnimation), true)]
    public class FSAnimationEditor : OdinEditor
    {
        protected AnimationPreviewRender m_previewRender;
        protected GameObject m_previewInstance;
        protected AnimancerComponent m_animator;

        protected AnimancerState m_animationState;
        
        [SerializeField] private GameObject m_previewPrefab;
        
        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_previewRender != null) m_previewRender.Dispose();
            m_previewRender = new AnimationPreviewRender();

            m_previewRender.AcceptDragObject += OnPreviewPrefabDrag;
            
            // TODO: Might be possible to disable doing repaints as TimeControl can update it when playing. Notice when disabeld and the animation is playing its fine. Just when its paused it breaks prolly because BTS updates are happening by animator
            EditorApplication.update += Repaint;

            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;

//#if FMOD_ENABLED
            AudioManager.LoadEditorPreviewBanks();
//#endif
        }

        // Edge case where assembly reload interrupts OnDisable cleanup, esp for the preview scene. This can cause
        // the scene to leak and not be properly disposed of.
        private void BeforeAssemblyReload()
        {
            PerformCleanup();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            PerformCleanup();
            
            AudioManager.UnloadEditorPreviewBanks(); // For preview audio
        }

        private void PerformCleanup()
        {
            DestroyAnimState();
            
            if (m_previewInstance)
            {
                DestroyImmediate(m_previewInstance);
                m_previewInstance = null;
            }

            if (m_previewRender != null)
            {
                m_previewRender.AcceptDragObject -= OnPreviewPrefabDrag;
                m_previewRender?.Dispose();
                m_previewRender = null;
            }

            EditorApplication.update -= Repaint;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
        }

        private void DestroyAnimState()
        {
            if (m_animationState != null && !m_animationState.Graph.PlayableGraph.IsNullOrDestroyed())// && !m_animationState.Graph.PlayableGraph.IsNullOrDestroyed())
            {
                m_animationState.Destroy();
            }
            m_animationState = null;
        }

        private bool OnPreviewPrefabDrag(Object arg)
        {
            if (arg is GameObject go)
            {
                if (go.GetComponent<Animator>())
                {
                    m_previewPrefab = go;
                    InitializePreviewModel();
                    return true;
                }
            }
            return false;
        }
        
        private bool m_animationResetRequested = true;
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck() && m_animationState != null)
            {
                // reinitialize the preview model if any clip related settings change
                m_animationResetRequested = true;
            }
        }

        private void InitializePreviewModel()
        {
            if (!m_previewPrefab) return;
            
            if (m_previewInstance)
            {
                m_previewRender.UnregisterPreviewableObject(m_previewInstance);
                DestroyImmediate(m_previewInstance);
            }
            
            m_animator = AnimationEditorUtility.InstantiatePreview(m_previewPrefab);
            m_previewInstance = m_animator.gameObject;
            m_previewRender.RegisterPreviewableObject(m_previewInstance);
            m_previewRender.PreviewScene.AddSingleGO(m_previewInstance);
            if (m_animator && m_animator.Animator)
            {
                m_animator.Animator.applyRootMotion = false;
            }
        }

        public override bool HasPreviewGUI() => true;

        private bool m_enableRootMotion = false;
        public override void OnPreviewSettings()
        {
            m_previewRender.DoPreviewSettings();
            if (GUILayout.Toggle(m_enableRootMotion, AdvancedPreviewRenderUtility.Styles.avatarIcon, AdvancedPreviewRenderUtility.Styles.preButton))
            {
                m_enableRootMotion = !m_enableRootMotion;
                if (m_animator)
                {
                    m_animator.Animator.applyRootMotion = m_enableRootMotion;
                    m_previewInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
            }
        }

        private List<AnimationClip> m_clips = new();
        private Rect m_previewRectOnRepaint; // During layout phase, we wanna keep the rect passed to the FSAnimation controls the same as on repaint phase to avoid weirdness with the preview rect changing size.
        public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
        {
            var animation = target as FSAnimation;

            if (animation.HasValidClips())
            {
                if (m_previewPrefab == null)
                {
                    animation.GetAnimationClips(m_clips);
                    if (m_clips == null || m_clips.Count == 0) return;
                    m_previewPrefab = m_clips.FirstOrDefault()?.RetrieveBestFittingPreviewGameObject();
                    InitializePreviewModel();
                    //return;
                }

                m_animationState = m_animator ? m_animator.Layers[(int)animation.Layer].CurrentState : null;
                if (m_animationResetRequested)
                {
                    PlayAnimation(m_animator, animation);
                    m_animationResetRequested = false;
                }
                m_previewRender.SyncAnimancerStateWithTimeControl(m_animationState);
            }

            var finalPreviewRect = m_previewRender.DoRenderPreview(r, background, false);
            
            if (Event.current.type == EventType.Repaint)
                m_previewRectOnRepaint = finalPreviewRect;

            if (m_animationState != null)
                animation.EditorPreviewParameterControl(m_animator, m_previewRectOnRepaint, serializedObject, m_animationState);

            m_previewRender.DoPreviewControlsAndGUI(finalPreviewRect);
        }

        protected void PlayAnimation(AnimancerComponent animator, FSAnimation animation)
        {
            if (!animator || !animation) return;

            DestroyAnimState();
            
            m_animationState = animation.Play(animator);
            
            //if (wasNull && m_animationState != null)
            {
                m_previewRender.m_timeControl.playing = true;
            }
        }
    }
}