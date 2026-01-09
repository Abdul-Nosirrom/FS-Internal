using UnityEngine;

namespace FS.Math
{
    public static class VectorExtensions
    {
        public static Vector3 WithY(this Vector3 vector, float y)
        {
            vector.y = y;
            return vector;
        }
        
        public static Vector3 WithX(this Vector3 vector, float x)
        {
            vector.x = x;
            return vector;
        }
        
        public static Vector3 WithZ(this Vector3 vector, float z)
        {
            vector.z = z;
            return vector;
        }

        public static Vector3 WithAxis(this Vector3 vector, Vector3 axis, float newProjection)
        {
            return vector.ProjectOnPlane(axis) + newProjection * axis;
        }

        public static Vector3 MostAligned(this Vector3 vector, Vector3 a, Vector3 b)
        {
            var aDot = vector.Dot(a);
            var bDot = vector.Dot(b);
            return aDot > bDot ? a : b;
        }
        
        public static Vector3 ClampMagnitude(this ref Vector3 vector, float max)
        {
            if (vector.sqrMagnitude > max * max)
            {
                vector.Normalize();
                vector *= max;
            }
            return vector;
        }

        public static bool IsZero(this Vector3 vector) => vector.sqrMagnitude < Mathf.Epsilon;
        public static bool IsNearlyZero(this Vector3 vector, float epsilon = 0.01f) => vector.sqrMagnitude < epsilon * epsilon;

        public static float Angle(this Vector3 a, Vector3 b) => Vector3.Angle(a, b);
        public static float SignedAngle(this Vector3 a, Vector3 b) => Vector3.SignedAngle(a, b, a.Cross(b));
        
        public static float Dot(this Vector3 a, Vector3 b) => Vector3.Dot(a, b);
        public static Vector3 Cross(this Vector3 a, Vector3 b) => Vector3.Cross(a, b);
        
        public static Vector3 ProjectOnPlane(this Vector3 vector, Vector3 planeNormal) 
            => Vector3.ProjectOnPlane(vector, planeNormal);
        public static Vector3 ProjectOnto(this Vector3 vector, Vector3 onNormal)
            => Vector3.Project(vector, onNormal);

        /// <summary>
        /// Returns the direction adjusted to be tangent to a specified surface normal relatively to the character's up direction.
        /// Useful for reorienting a direction on a slope without any lateral deviation in trajectory
        /// </summary>
        public static Vector3 GetDirectionTangentToSurface(this Vector3 direction, Vector3 surfaceNormal)
        {
            Vector3 directionRight = Vector3.Cross(direction, surfaceNormal);
            if (directionRight.IsZero()) return Vector3.zero; // direction is parallel to upVector, cannot determine right vector
            return Vector3.Cross(surfaceNormal, directionRight).normalized;
        }
    }
}