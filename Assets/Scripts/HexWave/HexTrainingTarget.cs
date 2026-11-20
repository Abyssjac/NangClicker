using System;
using System.Collections.Generic;
using UnityEngine;

namespace NangClicker.HexWave
{
    /// <summary>A damage probe, not a huge finite HP pool. No death or scene-side enemy behavior.</summary>
    [Serializable]
    public sealed class HexTrainingTarget
    {
        public HexCoordinate coordinate;
        [Min(1)] public int count = 1;
        [NonSerialized] public long totalDamage, highestHit;
        [NonSerialized] public int hurtCount;
        [NonSerialized] public float peakHeight;
        [NonSerialized] public readonly List<Vector2> damageHistory = new List<Vector2>();
        public void ResetStats() { totalDamage = highestHit = 0; hurtCount = 0; peakHeight = 0; damageHistory.Clear(); }
        public void Sample(float height, HexDamageSettings settings, double time)
        {
            peakHeight = Mathf.Max(peakHeight, Mathf.Abs(height));
            long damage = HexWaveDamage.Evaluate(height, settings, count);
            if (damage <= 0) return;
            totalDamage = HexWaveDamage.Add(totalDamage, damage); highestHit = Math.Max(highestHit, damage); hurtCount++;
            damageHistory.Add(new Vector2((float)time, damage));
        }
    }
}
