using System;
using System.Collections.Generic;
using JackyUtility;
using UnityEngine;
using UnityEngine.Serialization;

namespace NangClicker.HexWave
{
    public enum Key_HexSpellPP { None = 0, Line = 1, Triangle = 2, ChargedTriangle = 3, StairHammer = 4,
            Spell_HighStrike = 10,
        Convergence = 5, ResonanceCage = 6, RingEcho = 7, SinkRebound = 8, Aftershock = 9 }
    public enum HexSpellEdgeMode { Absorb = 0, Free = 1, Fixed = 2, Amplify = 3 }
    public enum HexSpellEventMode { Impulse = 0, LiftAndRelease = 1 }

    [Serializable]
    public struct HexSpellImpulse
    {
        public HexCoordinate offset;
        [FormerlySerializedAs("time"), Min(0)] public float releaseDelay;
        public float impulse;
        public HexSpellEventMode mode;
        public float targetHeight;
        public HexSpellImpulse(HexCoordinate offset, float delay, float impulse)
        { this.offset = offset; releaseDelay = delay; this.impulse = impulse; mode = HexSpellEventMode.Impulse; targetHeight = 0; }
    }

    [Serializable]
    public struct HexSpellWall
    {
        public HexCoordinate offset;
        public Key_HexBoundaryProfilePP response;
    }

    [CreateAssetMenu(fileName = "HexSpellPP_", menuName = "AllProperties/HexSpellProperty")]
    public sealed class HexSpellProperty : EnumStringKeyedProperty<Key_HexSpellPP>
    {
        public string displayName = "New Spell";
        [TextArea] public string description;
        public Sprite icon;
        [Tooltip("Multiplies signed event impulses and target heights.")] public float strength = 1;
        [Min(0.01f)] public float chargeDuration = 0.6f;
        [Tooltip("Starts on mouse release, not mouse down. Walls affect every wave.")]
        [Min(0.01f)] public float wallDuration = 2.5f;
        [Tooltip("Perimeter response where there is no physical wall. Paint cells to add Amplify.")]
        public HexSpellEdgeMode edgeMode = HexSpellEdgeMode.Absorb;
        [Min(0)] public float edgeDamping = 5;
        [Min(1)] public int absorptionWidth = 2;
        public List<HexCoordinate> region = new List<HexCoordinate>();
        public List<HexSpellImpulse> impulses = new List<HexSpellImpulse>();
        public List<HexSpellWall> walls = new List<HexSpellWall>();
        public HexBoundarySettings Boundary => new HexBoundarySettings
        { mode = edgeMode, damping = edgeDamping, absorptionWidth = absorptionWidth };
        public float LiftHeight(HexSpellImpulse hit, float multiplier) => hit.targetHeight * strength * multiplier;

        public bool Validate(out string error)
        {
            error = null;
            if (EnumKey == Key_HexSpellPP.None) error = "Choose a non-None spell key.";
            else if (!Finite(strength) || !Finite(chargeDuration) || chargeDuration <= 0 ||
                !Finite(wallDuration) || wallDuration <= 0 || !Boundary.IsValid || edgeMode == HexSpellEdgeMode.Amplify)
                error = "Invalid cast/boundary settings. Use wall cells for Amplify.";
            else if (region == null || region.Count == 0) error = "Paint an allowed region.";
            else if (impulses == null || impulses.Count == 0) error = "Add at least one event.";
            if (error != null) return false;
            var mask = new HashSet<HexCoordinate>();
            foreach (var c in region) if (!c.IsValid || !mask.Add(c))
            { error = "Region coordinates must be valid and unique."; return false; }
            if (!mask.Contains(HexCoordinate.Origin)) { error = "Region must contain anchor 000."; return false; }
            var wallCells = new HashSet<HexCoordinate>();
            if (walls != null) foreach (var wall in walls)
                if (!wall.offset.IsValid || !wallCells.Add(wall.offset) || wall.offset == HexCoordinate.Origin ||
                    wall.response == Key_HexBoundaryProfilePP.None || mask.Contains(wall.offset))
                { error = "Walls must be unique, outside the moving region, and have a response key."; return false; }
            foreach (var hit in impulses)
                if (!hit.offset.IsValid || !mask.Contains(hit.offset) || !Finite(hit.releaseDelay) || hit.releaseDelay < 0 ||
                    !Finite(hit.impulse) || !Finite(hit.targetHeight) || !Enum.IsDefined(typeof(HexSpellEventMode), hit.mode))
                { error = "Events need a region coordinate, finite values and a non-negative release delay."; return false; }
            return true;
        }
        internal static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
