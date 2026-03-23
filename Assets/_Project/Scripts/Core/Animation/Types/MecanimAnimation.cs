using Animancer;
using FS.Math;
using UnityEngine;

namespace FS.Animation
{
    [CreateAssetMenu(menuName = "FS/Animation/Generic/Mecanim", fileName = "New Mecanim Animation")]
    public class MecanimAnimationAsset : FSAnimation, IMecanimGenericAnimation
    {
        private static class Parameters
        {
            public const string ForwardLeans = "ForwardLean";
            public const string SideLeans = "SideLean";
            public const string Speed = "Speed";
            public const string VerticalSpeed = "VerticalSpeed";
            public const string LateralSpeed = "LateralSpeed";
            public const string IsGrounded = "IsGrounded";
        }

        [SerializeField, Tooltip("Destroys the state before playing to ensure Mecanim controller starts from Entry. Disable for performance if state reuse is desired.")]
        private bool EnsureResetOnPlay = false;
        public MecanimAnimation m_animationController;

        public override ITransition GetTransition() => m_animationController.GetTransition();

        public override AnimancerState Play(AnimancerComponent animator, float fadeDuration = -1)
        {
            // If a state exists, destroy it first WARNING: THIS IS TEMPORARY FIX FOR ANIMATION CONTROLLERS NOT RESETTING
            if (EnsureResetOnPlay && animator.States.TryGet(this, out var state))
                state.Destroy();
            return m_animationController.Play(animator, Layer, fadeDuration);
        }

        // these are manually bound
        public override AnimationEventHolder GetEventsFor(int childIdx) => null;
        public override bool HasValidClips() => ((IAnimation)m_animationController).IsValid;

        public void UpdateSpeedBlending(AnimancerState state, PhysicsController physics)
        {
            UpdateAnimationParameters((ControllerState)state, physics);
        }

        public void UpdateAirIdleFallSpeed(AnimancerState state, PhysicsController physics)
        {
            UpdateAnimationParameters((ControllerState)state, physics);
        }

        public void UpdatePhysics(AnimancerState state, PhysicsController physics)
        {
            UpdateAnimationParameters((ControllerState)state, physics);
        }

        private void UpdateAnimationParameters(ControllerState state, PhysicsController physics)
        {
            bool skipInterp = state.NormalizedTime <= 0f;
            for (int p = 0; p < state.GetParameterCount(); p++)
            {
                var param = state.GetParameter(p);
                switch (param.name)
                {
                    case Parameters.ForwardLeans:
                        var forwardLeanInitProp = state.GetFloat(param.nameHash);
                        float forwardLean = AnimationParameterCalculator.CalculateForwardLeans(physics);
                        if (!skipInterp) forwardLean = Mathf.Lerp(forwardLeanInitProp, forwardLean, 10f * Time.deltaTime);
                        state.SetFloat(param.nameHash, forwardLean);
                        break;
                    case Parameters.SideLeans:
                        float sideLean = AnimationParameterCalculator.CalculateSideLeans(physics);
                        var sideLeanInitProp = state.GetFloat(param.nameHash);
                        if (!skipInterp) sideLean = Mathf.Lerp(sideLeanInitProp, sideLean, 4f * Time.deltaTime);
                        state.SetFloat(param.nameHash, sideLean);
                        break;
                    case Parameters.Speed:
                        float speed = AnimationParameterCalculator.CalculateSpeed(physics);
                        state.SetFloat(param.nameHash, speed);
                        break;
                    case Parameters.LateralSpeed:
                        float lateralSpeed = AnimationParameterCalculator.CalculateLateralSpeed(physics);
                        state.SetFloat(param.nameHash, lateralSpeed);
                        break;
                    case Parameters.VerticalSpeed:
                        float verticalSpeed = AnimationParameterCalculator.CalculateVerticalSpeed(physics);
                        var verticalSpeedInitProp = state.GetFloat(param.nameHash);
                        if (!skipInterp) verticalSpeed = Mathf.Lerp(verticalSpeedInitProp, verticalSpeed, 5f * Time.deltaTime);
                        state.SetFloat(param.nameHash, verticalSpeed);
                        break;
                    case Parameters.IsGrounded:
                        state.SetBool(param.nameHash, physics.IsGrounded);
                        break;
                }
            }
        }

    }
}