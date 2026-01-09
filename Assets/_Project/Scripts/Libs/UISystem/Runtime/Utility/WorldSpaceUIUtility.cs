using UnityEngine;

namespace FS.UI
{
    public static class WorldSpaceUIUtility
    {
        /// <summary>
        /// Converts a world position to a local point on a ScreenSpace-Camera canvas.
        /// Returns false if the point is behind the camera.
        /// </summary>
        public static bool WorldToCanvasLocalPoint(
            Camera camera,
            RectTransform canvasRect,
            Vector3 worldPosition,
            out Vector2 localPoint)
        {
            Vector3 viewportPos = camera.WorldToViewportPoint(worldPosition);

            if (viewportPos.z <= 0f)
            {
                localPoint = default;
                return false;
            }

            Vector2 canvasSize = canvasRect.sizeDelta;

            localPoint = new Vector2(
                (viewportPos.x - 0.5f) * canvasSize.x,
                (viewportPos.y - 0.5f) * canvasSize.y
            );

            return true;
        }

        /// <summary>
        /// Returns camera-space depth (positive in front of camera).
        /// </summary>
        public static float CameraDepth(Camera camera, Vector3 worldPosition)
        {
            return camera.transform.InverseTransformPoint(worldPosition).z;
        }

        /// <summary>
        /// Perspective-correct UI scale relative to a reference depth.
        /// </summary>
        public static float PerspectiveScale(
            float depth,
            float referenceDepth,
            float referenceScale = 1f)
        {
            if (depth <= 0f)
                return 0f;

            return referenceScale * (referenceDepth / depth);
        }

        /// <summary>
        /// Pixel-perfect scale that matches a given world-space size.
        /// </summary>
        public static float WorldSizeToUIScale(
            Camera camera,
            RectTransform canvasRect,
            float worldSize,
            float distance)
        {
            float frustumHeight =
                2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            float pixelsPerUnit = canvasRect.rect.height / frustumHeight;

            return worldSize * pixelsPerUnit;
        }

        /// <summary>
        /// Optional pixel snapping to eliminate sub-pixel jitter.
        /// </summary>
        public static Vector2 PixelSnap(Vector2 value)
        {
            return new Vector2(
                Mathf.Round(value.x),
                Mathf.Round(value.y)
            );
        }
    }
}