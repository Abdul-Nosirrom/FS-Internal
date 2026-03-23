using PrimeTween;
using UnityEngine.Animations.Rigging;

namespace FS.Animation.Rigging
{
    public static class RigConstraintTweens
    {
        public static Tween Weight<T>(T rigConstraint, TweenSettings<float> settings) where T : class, IRigConstraint => Tween.Custom(rigConstraint, settings, SetWeight);
        public static Tween Weight<T>(T rigConstraint, float start, float end, float duration, Ease ease = Ease.Default, int cycles = 1,
            CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) where T : class, IRigConstraint
            => Weight(rigConstraint, new TweenSettings<float>(start, end, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
        public static Tween Weight<T>(T rigConstraint, float end, TweenSettings settings) where T : class, IRigConstraint => Weight(rigConstraint, new (rigConstraint.weight, end, settings));
        public static Tween Weight<T>(T rigConstraint, float end, float duration, Ease ease = Ease.Default, int cycles = 1,
            CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) where T : class, IRigConstraint
            => Weight(rigConstraint, new TweenSettings<float>(rigConstraint.weight, end, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));

        private static void SetWeight<T>(T rigConstraint, float newWeight) where T : class, IRigConstraint =>
            rigConstraint.weight = newWeight;
        
        public static Tween Weight(Rig rig, TweenSettings<float> settings)  => Tween.Custom(rig, settings, SetRigWeight);
        public static Tween Weight(Rig rig, float start, float end, float duration, Ease ease = Ease.Default, int cycles = 1,
            CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) 
            => Weight(rig, new TweenSettings<float>(start, end, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
        public static Tween Weight(Rig rig, float end, TweenSettings settings)  => Weight(rig, new (rig.weight, end, settings));
        public static Tween Weight(Rig rig, float end, float duration, Ease ease = Ease.Default, int cycles = 1,
            CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) 
            => Weight(rig, new TweenSettings<float>(rig.weight, end, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));

        private static void SetRigWeight(Rig rig, float newWeight)  => rig.weight = newWeight;
    }
}