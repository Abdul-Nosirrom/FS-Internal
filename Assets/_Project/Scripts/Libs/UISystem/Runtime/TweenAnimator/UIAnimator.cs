using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FS.UI
{
    [AddComponentMenu("Free Skies/UI/UI Animator")]
    public class UIAnimator : TweenAnimator
    {
        
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private async static void TweenConfig()
        {
            await Awaitable.NextFrameAsync(); // Calls PrimeTweenManager.Instance and creates a GO, shouldn't be done first frame (monobehavior constructor)
            PrimeTween.PrimeTweenConfig.warnEndValueEqualsCurrent = false;
        }
#endif
        [RuntimeInitializeOnLoadMethod]
        private static void RuntimeTweenConfig()
        {
            PrimeTween.PrimeTweenConfig.warnEndValueEqualsCurrent = false;
            PrimeTween.PrimeTweenConfig.warnZeroDuration = false;
        }

        public async Awaitable PlayForwardAsync()
        {
            Play();
            await ActiveSequence;
        }

        public async Awaitable PlayReverseAsync()
        {
            Reverse();
            await ActiveSequence;
        }
    }
}