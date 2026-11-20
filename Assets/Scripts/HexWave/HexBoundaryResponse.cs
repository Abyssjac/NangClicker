using System;
using System.Collections.Generic;
using UnityEngine;

namespace NangClicker.HexWave
{
    [Serializable]
    public struct HexAmplifySettings
    {
        [Min(1)] public float gain;
        [Min(0.001f)] public float triggerThreshold;
        [Min(0.01f)] public float triggerCooldown;
        [Range(0.01f, 0.99f)] public float rearmRatio;
        public static HexAmplifySettings Default => new HexAmplifySettings
        { gain = 1.2f, triggerThreshold = 0.025f, triggerCooldown = 0.16f, rearmRatio = 0.4f };
        public bool IsValid => HexSpellProperty.Finite(gain) && gain >= 1 && HexSpellProperty.Finite(triggerThreshold) &&
            triggerThreshold > 0 && HexSpellProperty.Finite(triggerCooldown) && triggerCooldown > 0 &&
            HexSpellProperty.Finite(rearmRatio) && rearmRatio > 0 && rearmRatio < 1;
    }

    [Serializable]
    public struct HexBoundarySettings
    {
        public HexSpellEdgeMode mode;
        [Min(0)] public float damping;
        [Min(1)] public int absorptionWidth;
        public Key_HexBoundaryProfilePP profile;
        public static HexBoundarySettings Fixed => new HexBoundarySettings
        { mode = HexSpellEdgeMode.Fixed, damping = 5, absorptionWidth = 2, profile = Key_HexBoundaryProfilePP.Fixed };
        public bool IsValid => Enum.IsDefined(typeof(HexSpellEdgeMode), mode) &&
            HexSpellProperty.Finite(damping) && damping >= 0 && absorptionWidth >= 1;
    }

    /// <summary>One wall cell. Gates belong to wall/contact pairs, not individual wave contributions.</summary>
    public sealed class HexBoundarySource
    {
        private struct Gate { public bool armed; public double nextTime, lastTrigger; }
        private readonly Dictionary<HexCoordinate, Gate> gates = new Dictionary<HexCoordinate, Gate>();
        private readonly HexAmplifySettings amplification;
        public HexBoundarySettings Settings { get; }
        public bool IsSceneWall { get; }
        public double LastTriggerTime { get; private set; } = double.NegativeInfinity;
        public HexCoordinate LastTriggerCell { get; private set; }
        public float ContactFlash(HexCoordinate cell, double time) => gates.TryGetValue(cell, out var gate)
            ? Mathf.Clamp01(1 - (float)(time - gate.lastTrigger) / 0.16f) : 0;
        public HexBoundarySource(HexBoundarySettings settings, bool isSceneWall = true, HexAmplifySettings? amplification = null)
        { Settings = settings; IsSceneWall = isSceneWall; this.amplification = amplification ?? HexAmplifySettings.Default; }
        public float ConsumeGain(HexCoordinate cell, float height, float velocity, double time)
        {
            if (Settings.mode != HexSpellEdgeMode.Amplify) return 0;
            if (!gates.TryGetValue(cell, out var gate)) gate = new Gate { armed = true, lastTrigger = double.NegativeInfinity };
            if (Mathf.Abs(height) < amplification.triggerThreshold * amplification.rearmRatio) gate.armed = true;
            float extra = 0;
            if (gate.armed && time >= gate.nextTime && Mathf.Abs(height) >= amplification.triggerThreshold &&
                height * velocity > 0 && Mathf.Abs(velocity) > 0.00001f)
            {
                extra = amplification.gain - 1;
                gate.armed = false; gate.nextTime = time + amplification.triggerCooldown;
                gate.lastTrigger = time;
                LastTriggerTime = time; LastTriggerCell = cell;
            }
            gates[cell] = gate;
            return extra;
        }
    }
}
