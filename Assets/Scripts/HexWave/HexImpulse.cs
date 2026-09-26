using System;
using UnityEngine;

namespace NangClicker.HexWave
{
    [Serializable]
    public struct HexImpulseRing
    {
        [Min(0)] [SerializeField] private int distance;
        [Min(0f)] [SerializeField] private float impulseWeight;

        public int Distance => distance;
        public float ImpulseWeight => impulseWeight;

        public HexImpulseRing(int distance, float impulseWeight)
        {
            this.distance = Mathf.Max(0, distance);
            this.impulseWeight = Mathf.Max(0f, impulseWeight);
        }
    }

    public readonly struct HexImpulseRequest
    {
        public HexCoordinate Center { get; }
        public float TotalImpulse { get; }

        public HexImpulseRequest(HexCoordinate center, float totalImpulse)
        {
            Center = center;
            TotalImpulse = totalImpulse;
        }
    }
}
