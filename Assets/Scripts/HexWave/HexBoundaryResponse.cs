using System;
using System.Collections.Generic;
using UnityEngine;

namespace NangClicker.HexWave
{
    [Serializable]
    public struct HexAmplifySettings
    {
        [Min(1f)] public float gain;
        [Min(0.001f)] public float triggerThreshold;
        [Min(0.01f)] public float triggerCooldown;
        [Min(0.01f)] public float activeDuration;
        [Min(0f)] public float energyBudget;
        [Min(1)] public int maxTriggers;
        public HexSpellEdgeMode fallbackMode;
        public static HexAmplifySettings Default => new HexAmplifySettings
        { gain = 1.35f, triggerThreshold = 0.025f, triggerCooldown = 0.16f, activeDuration = 2.5f,
            energyBudget = 20f, maxTriggers = 24, fallbackMode = HexSpellEdgeMode.Absorb };
        public bool IsValid => Finite(gain) && gain >= 1f && Finite(triggerThreshold) && triggerThreshold > 0 &&
            Finite(triggerCooldown) && triggerCooldown > 0 && Finite(activeDuration) && activeDuration > 0 &&
            Finite(energyBudget) && energyBudget >= 0 && maxTriggers > 0 &&
            (fallbackMode == HexSpellEdgeMode.Fixed || fallbackMode == HexSpellEdgeMode.Absorb);
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }

    [Serializable]
    public struct HexBoundarySettings
    {
        public HexSpellEdgeMode mode;
        [Min(0f)] public float damping;
        [Min(1)] public int absorptionWidth;
        public HexAmplifySettings amplification;
        public static HexBoundarySettings Fixed => new HexBoundarySettings
        { mode = HexSpellEdgeMode.Fixed, damping = 5, absorptionWidth = 2, amplification = HexAmplifySettings.Default };
        public bool IsValid => Enum.IsDefined(typeof(HexSpellEdgeMode), mode) &&
            !float.IsNaN(damping) && !float.IsInfinity(damping) && damping >= 0 && absorptionWidth >= 1 &&
            (mode != HexSpellEdgeMode.Amplify || amplification.IsValid);
    }

    [Serializable]
    public struct HexSpellBoundaryEdge
    {
        public HexCoordinate offset;
        [Range(0, 5)] public int direction;
        public HexBoundarySettings response;
    }

    /// <summary>One owned energy source. Scene-wall instances are shared across casts; spell-edge
    /// instances are shared across the fields of that cast. Exhaustion never resets automatically.</summary>
    public sealed class HexBoundarySource
    {
        private struct Gate { public bool armed; public double nextTime; }
        private readonly Dictionary<HexCoordinate, Gate> gates = new Dictionary<HexCoordinate, Gate>();
        public HexBoundarySettings Settings { get; }
        public bool IsSceneWall { get; }
        public double StartTime { get; private set; }
        public float EnergySpent { get; private set; }
        public int TriggerCount { get; private set; }
        public HexBoundarySource(HexBoundarySettings settings, double startTime, bool isSceneWall = false)
        { Settings = settings; StartTime = startTime; IsSceneWall = isSceneWall; }
        public void Rearm(double time) { StartTime = time; EnergySpent = 0; TriggerCount = 0; gates.Clear(); }
        public HexSpellEdgeMode ModeAt(double time)
        {
            var a = Settings.amplification;
            if (Settings.mode == HexSpellEdgeMode.Amplify &&
                (time >= StartTime + a.activeDuration || EnergySpent >= a.energyBudget || TriggerCount >= a.maxTriggers))
                return a.fallbackMode;
            return Settings.mode;
        }
        public void TryBoost(HexCoordinate cell, float height, ref float velocity, float mass, double time)
        {
            if (ModeAt(time) != HexSpellEdgeMode.Amplify) return;
            var a = Settings.amplification;
            if (!gates.TryGetValue(cell, out var gate)) gate = new Gate { armed = true };
            if (Mathf.Abs(height) < a.triggerThreshold * 0.4f) gate.armed = true;
            if (gate.armed && time >= gate.nextTime && Mathf.Abs(height) >= a.triggerThreshold &&
                height * velocity > 0f && Mathf.Abs(velocity) > 0.00001f)
            {
                float requested = 0.5f * mass * velocity * velocity * (a.gain * a.gain - 1f);
                float supplied = Mathf.Min(requested, Mathf.Max(0, a.energyBudget - EnergySpent));
                if (supplied > 0)
                {
                    velocity = Mathf.Sign(velocity) * Mathf.Sqrt(velocity * velocity + 2f * supplied / mass);
                    EnergySpent += supplied; TriggerCount++;
                }
                gate.armed = false; gate.nextTime = time + a.triggerCooldown;
            }
            gates[cell] = gate;
        }
    }
}
