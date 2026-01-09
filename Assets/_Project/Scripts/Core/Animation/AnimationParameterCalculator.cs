using FS.Math;
using UnityEngine;

namespace FS.Animation
{
    public static class AnimationParameterCalculator
    {
        public static float CalculateForwardLeans(PhysicsController physics) =>
            physics.Velocity.Dot(physics.transform.forward)/6f;//CalculateAxisLeans(physics, physics.transform.forward);

        public static float CalculateSideLeans(PhysicsController physics)
        {
            var forward = physics.transform.forward.ProjectOnPlane(physics.UpDirection).normalized;
            var right = Vector3.Cross(physics.UpDirection, forward).normalized;
            return CalculateAxisLeans(physics, right);
        }

        public static float CalculateSpeed(PhysicsController physics) => physics.Speed;
        public static float CalculateLateralSpeed(PhysicsController physics) => physics.LateralSpeed;
        public static float CalculateVerticalSpeed(PhysicsController physics) => physics.VerticalSpeed;

        public static float CalculateAxisLeans(PhysicsController physics, Vector3 axis)
        {
            var velocity = physics.LateralVelocity;
    
            // Need minimum speed to determine lean direction
            if (velocity.magnitude < 0.1f)
                return 0f;
    
            var forward = physics.transform.forward.ProjectOnPlane(physics.UpDirection).normalized;
            var right = Vector3.Cross(physics.UpDirection, forward).normalized;
            var velocityDir = velocity.normalized;
    
            // Lean is based on how much velocity deviates to the left/right
            // Dot with right vector gives us signed lean [-1 = left, 1 = right]
            float lean = Vector3.Dot(velocityDir, axis);
    
            // Optional: Scale by turn sharpness (angular velocity)
            // This makes tighter turns lean more dramatically
            float turnIntensity = Mathf.Abs(physics.AngularVelocity);// / maxTurnRate; // normalize to [0,1]
            lean *= Mathf.Clamp01(turnIntensity) * 2f;
    
            return Mathf.Clamp(lean, -1f, 1f);
        }
    }
}