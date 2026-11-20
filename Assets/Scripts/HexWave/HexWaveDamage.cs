using System;
using UnityEngine;

namespace NangClicker.HexWave
{
    [Serializable]
    public struct HexDamageSettings
    {
        [Min(0f)] public float minimumHeight;
        [Min(0f)] public float heightScale;
        [Min(0.01f)] public float interval;
        public static HexDamageSettings Default => new HexDamageSettings { minimumHeight = 0.02f, heightScale = 10, interval = 0.1f };
    }
    public static class HexWaveDamage
    {
        public static long Evaluate(float height, HexDamageSettings settings, int count = 1)
        {
            // Preserve the game's float arithmetic and round only after applying the height scale.
            double raw = Math.Ceiling((double)(Mathf.Max(0f, Mathf.Abs(height) - settings.minimumHeight) * settings.heightScale));
            double result = raw * Math.Max(0, count);
            return result >= long.MaxValue ? long.MaxValue : (long)Math.Max(0, result);
        }
        public static long Add(long total, long damage) => damage > long.MaxValue - total ? long.MaxValue : total + damage;
    }
}
