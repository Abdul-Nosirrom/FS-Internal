using UnityEngine;

namespace FS.Math
{
    public static class TransformExtensions
    {
        public static Vector3 PositionWithLocalOffset(this Transform transform, Vector3 offset)
        {
            return transform.position + transform.TransformDirection(offset);
        }
    }
}