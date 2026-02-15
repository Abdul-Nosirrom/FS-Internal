using FS.Animation;
using FS.Animation.Editor;
using UnityEngine;

namespace FS.AnimationEvents
{
    public class MovementCurveEvent : IAnimationEvent
    {
        public string Name => "Movement Curve";
        public bool IsRangedEvent => true;
        
        public void Start(GameObject context)
        {
            
        }

        public void End(GameObject context)
        {
            
        }
        
#if UNITY_EDITOR
        public void Start_Editor(GameObject context, AnimationPreviewRender previewRender)
        {
            
        }

        public void End_Editor(GameObject context, AnimationPreviewRender previewRender)
        {
            
        }
#endif        
    }
}