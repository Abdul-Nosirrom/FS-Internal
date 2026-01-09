using Animancer;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using Sirenix.Utilities.Editor;
#endif

namespace FS.Animation
{
    [CreateAssetMenu(menuName = "FS/Animation/Idle/Aliyah Style Idles", fileName = "New Idle Animation")]
    public class AliyahIdleAnimation : FSAnimation, IIdleAnimation
    {
        public WeaponExtensions.Type ActiveStyle
        {
            get => m_stylesController ? m_stylesController.Equipped : m_stylePreview;
            set => m_stylePreview = value;
        }
        
        public IdleAnimation Idle_SpringLegs;
        public IdleAnimation Idle_FingerGuns;
        public IdleAnimation Idle_BoxingGloves;
        public IdleAnimation Idle_WhipArms;

        // TODO: Temp, only aliyah uses this asset so its fine
        private WeaponExtensions.Type m_stylePreview;
        private WeaponExtensions m_stylesController;
        public void SetStylesController(WeaponExtensions styles) => m_stylesController = styles;

        public override ITransition GetTransition() => ActiveIdle.GetTransition();

        public override AnimationEventHolder GetEventsFor(int childIdx) => ActiveIdle.GetEventsFor(childIdx);

        public override bool HasValidClips() => (Idle_SpringLegs && Idle_SpringLegs.HasValidClips()) &&
                                                (Idle_FingerGuns && Idle_FingerGuns.HasValidClips()) &&
                                                (Idle_BoxingGloves && Idle_BoxingGloves.HasValidClips()) &&
                                                (Idle_WhipArms && Idle_WhipArms.HasValidClips());

        public IdleAnimation ActiveIdle
        {
            get
            {
                switch (ActiveStyle)
                {
                    case WeaponExtensions.Type.SpringLegs: return Idle_SpringLegs;
                    case WeaponExtensions.Type.FingerGuns: return Idle_FingerGuns;
                    case WeaponExtensions.Type.BoxingGloves: return Idle_BoxingGloves;
                    case WeaponExtensions.Type.WhipArms: return Idle_WhipArms;
                }

                return null;
            }
        }

#if UNITY_EDITOR
        public override void EditorPreviewParameterControl(AnimancerComponent animator, Rect previewRect, SerializedObject target,
            AnimancerState activeState)
        {
            var previewControlRect = new Rect(
                previewRect.x + 10,
                previewRect.yMax * 0.9f,
                300,
                15
            );
            
            EditorGUI.BeginChangeCheck();
            ActiveStyle = (WeaponExtensions.Type) SirenixEditorFields.EnumDropdown(previewControlRect, ActiveStyle);
            if (EditorGUI.EndChangeCheck())
                Play(animator); // Replay
        }
#endif        
    }
}