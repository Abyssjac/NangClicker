using System;
using System.Collections.Generic;
using JackyUtility;
using UnityEngine;

namespace NangClicker.HexWave
{
    public enum Key_HexSpellPP { None = 0, Line = 1, Triangle = 2, ChargedTriangle = 3, StairHammer = 4,
        Convergence = 5, ResonanceCage = 6, RingEcho = 7, SinkRebound = 8, Aftershock = 9 }
    public enum HexSpellEdgeMode { Absorb = 0, Free = 1, Fixed = 2, Amplify = 3 }
    public enum HexSpellEventMode { Impulse = 0, LiftAndRelease = 1 }

    [Serializable]
    public struct HexSpellImpulse
    {
        public HexCoordinate offset;
        [Min(0f)] public float time;
        public float impulse;
        public HexSpellEventMode mode;
        [Tooltip("Signed target height for LiftAndRelease; this is an authoritative displacement, not a visual offset.")]
        public float targetHeight;
        public bool heightFromStrength;
        public HexSpellImpulse(HexCoordinate offset, float time, float impulse)
        { this.offset = offset; this.time = time; this.impulse = impulse; mode = HexSpellEventMode.Impulse; targetHeight = 0; heightFromStrength = false; }
    }

    [CreateAssetMenu(fileName = "HexSpellPP_", menuName = "AllProperties/HexSpellProperty")]
    public sealed class HexSpellProperty : EnumStringKeyedProperty<Key_HexSpellPP>
    {
        public string displayName = "New Spell";
        [TextArea] public string description;
        public Sprite icon;
        [Tooltip("Global signed multiplier. Scales impulse events and lift heights, not the region or event times.")]
        public float strength = 1f;
        public HexSpellEdgeMode edgeMode = HexSpellEdgeMode.Absorb;
        [Min(0f)] public float edgeDamping = 5f;
        [Min(1)] public int absorptionWidth = 2;
        public HexAmplifySettings amplification = HexAmplifySettings.Default;
        [Tooltip("Seconds to reach the exact lift target using a smooth curve with zero endpoint velocity.")]
        [Min(0.01f)] public float chargeDuration = 0.6f;
        [Min(0f)] public float holdDuration;
        [Tooltip("Only used by lift events with Height From Strength enabled. X = |event impulse|, Y = lift height.")]
        public AnimationCurve heightFromImpulse = AnimationCurve.Linear(0, 0, 10, 1);
        [Tooltip("Optional per-edge overrides. Directions are in the skill's unrotated local coordinates.")]
        public List<HexSpellBoundaryEdge> boundaryEdges = new List<HexSpellBoundaryEdge>();
        public List<HexCoordinate> region = new List<HexCoordinate>();
        public List<HexSpellImpulse> impulses = new List<HexSpellImpulse>();
        public HexBoundarySettings Boundary => new HexBoundarySettings
        { mode = edgeMode, damping = edgeDamping, absorptionWidth = absorptionWidth, amplification = amplification };
        public float LiftHeight(HexSpellImpulse hit, float multiplier) =>
            (hit.heightFromStrength ? Mathf.Sign(hit.impulse) * heightFromImpulse.Evaluate(Mathf.Abs(hit.impulse)) : hit.targetHeight) * strength * multiplier;

        public bool Validate(out string error)
        {
            error = null;
            if (EnumKey == Key_HexSpellPP.None) error = "Choose a non-None spell key.";
            else if (!Finite(strength) || !Finite(edgeDamping) || edgeDamping < 0f || absorptionWidth < 1)
                error = "Strength and edge settings must be finite and valid.";
            else if (region == null || region.Count == 0) error = "Paint an allowed region.";
            else if (impulses == null || impulses.Count == 0) error = "Add at least one impulse event.";
            if (error != null) return false;
            var unique = new HashSet<HexCoordinate>();
            foreach (var cell in region)
                if (!cell.IsValid || !unique.Add(cell))
                { error = "Region coordinates must be valid and unique."; return false; }
            if (!unique.Contains(HexCoordinate.Origin))
            { error = "The allowed region must include the anchor (0,0,0)."; return false; }
            foreach (var hit in impulses)
                if (!hit.offset.IsValid || !unique.Contains(hit.offset) || !Finite(hit.time) || hit.time < 0f || !Finite(hit.impulse) ||
                    !Enum.IsDefined(typeof(HexSpellEventMode), hit.mode) || !Finite(hit.targetHeight) ||
                    (hit.mode == HexSpellEventMode.LiftAndRelease && (!Finite(chargeDuration) || chargeDuration <= 0 ||
                    !Finite(holdDuration) || holdDuration < 0 || (hit.heightFromStrength && (heightFromImpulse == null || !Finite(LiftHeight(hit, 1)))))))
                { error = "Each impulse needs a valid region coordinate, finite strength and non-negative time."; return false; }
            if (!Boundary.IsValid) { error = "Invalid boundary response or amplification limits."; return false; }
            var edges = new HashSet<string>();
            foreach (var edge in boundaryEdges)
                if (!unique.Contains(edge.offset) || edge.direction < 0 || edge.direction > 5 ||
                    unique.Contains(edge.offset.GetNeighbor(edge.direction)) || !edge.response.IsValid ||
                    !edges.Add(edge.offset + ":" + edge.direction))
                { error = "Boundary overrides must be unique outward-facing edges of the allowed region."; return false; }
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
