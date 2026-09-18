using System;
using UnityEngine;

namespace MonaLisaGame
{
    /// <summary>
    /// An axis-aligned world-space area in which the gaze focus may exist.
    /// The two serialized corners are normalized by every operation, so their Inspector order is safe.
    /// </summary>
    [Serializable]
    public struct GazeArea
    {
        [Tooltip("World-space lower-left corner of the playable gaze area.")]
        public Vector2 bottomLeft;

        [Tooltip("World-space upper-right corner of the playable gaze area.")]
        public Vector2 topRight;

        public float MinX => Mathf.Min(bottomLeft.x, topRight.x);
        public float MaxX => Mathf.Max(bottomLeft.x, topRight.x);
        public float MinY => Mathf.Min(bottomLeft.y, topRight.y);
        public float MaxY => Mathf.Max(bottomLeft.y, topRight.y);

        public Vector2 Center => new((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);
        public Vector2 Size => new(MaxX - MinX, MaxY - MinY);

        public GazeArea(Vector2 lowerLeft, Vector2 upperRight)
        {
            bottomLeft = lowerLeft;
            topRight = upperRight;
        }

        public bool Contains(Vector2 point) =>
            point.x >= MinX && point.x <= MaxX && point.y >= MinY && point.y <= MaxY;

        public Vector2 Clamp(Vector2 point) => new(
            Mathf.Clamp(point.x, MinX, MaxX),
            Mathf.Clamp(point.y, MinY, MaxY));

        /// <summary>
        /// Returns a random valid point. Padding is clamped so a malformed area remains safe.
        /// </summary>
        public Vector2 GetRandomPoint(float padding = 0f)
        {
            float safePaddingX = Mathf.Clamp(Mathf.Max(0f, padding), 0f, (MaxX - MinX) * 0.5f);
            float safePaddingY = Mathf.Clamp(Mathf.Max(0f, padding), 0f, (MaxY - MinY) * 0.5f);
            float minX = MinX + safePaddingX;
            float maxX = MaxX - safePaddingX;
            float minY = MinY + safePaddingY;
            float maxY = MaxY - safePaddingY;
            return new Vector2(
                UnityEngine.Random.Range(minX, maxX),
                UnityEngine.Random.Range(minY, maxY));
        }
    }
}
