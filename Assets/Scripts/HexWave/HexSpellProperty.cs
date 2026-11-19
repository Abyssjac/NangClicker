using System;
using System.Collections.Generic;
using JackyUtility;
using UnityEngine;

namespace NangClicker.HexWave
{
    public enum Key_HexSpellPP { None = 0, Line = 1, Triangle = 2 }
    public enum HexSpellEdgeMode { Absorb = 0, Free = 1, Fixed = 2 }

    [Serializable]
    public struct HexSpellImpulse
    {
        public HexCoordinate offset;
        [Min(0f)] public float time;
        public float impulse;
        public HexSpellImpulse(HexCoordinate offset, float time, float impulse)
        { this.offset = offset; this.time = time; this.impulse = impulse; }
    }

    [CreateAssetMenu(fileName = "HexSpellPP_", menuName = "AllProperties/HexSpellProperty")]
    public sealed class HexSpellProperty : EnumStringKeyedProperty<Key_HexSpellPP>
    {
        public string displayName = "New Spell";
        [TextArea] public string description;
        public Sprite icon;
        public float strength = 1f;
        public HexSpellEdgeMode edgeMode = HexSpellEdgeMode.Absorb;
        [Min(0f)] public float edgeDamping = 5f;
        [Min(1)] public int absorptionWidth = 2;
        public List<HexCoordinate> region = new List<HexCoordinate>();
        public List<HexSpellImpulse> impulses = new List<HexSpellImpulse>();

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
                if (!hit.offset.IsValid || !unique.Contains(hit.offset) || !Finite(hit.time) || hit.time < 0f || !Finite(hit.impulse))
                { error = "Each impulse needs a valid region coordinate, finite strength and non-negative time."; return false; }
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
