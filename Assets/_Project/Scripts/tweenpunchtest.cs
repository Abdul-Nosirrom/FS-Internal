using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _Project.Scripts
{
    public class tweenpunchtest : MonoBehaviour
    {
        public bool isScale;
        public Vector3 punchDir;
        public float punchStrength;
        public float punchDuration;
        public float punchFrequency;
        public bool enableFalloffEase;
        public Ease fallOffEase;
        
        [Button]
        private void PreviewTween()
        {
            var punchSettings = new ShakeSettings()
            {
                strength = transform.forward * punchStrength,
                duration = punchDuration,
                frequency = punchFrequency,
                enableFalloff = enableFalloffEase,
                falloffEase = fallOffEase
            };
            if (isScale) Tween.PunchScale(transform, punchSettings);
            else Tween.PunchLocalPosition(transform, punchSettings);
        }
    }
}