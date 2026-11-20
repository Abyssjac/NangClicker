using JackyUtility;
using UnityEngine;

namespace NangClicker.HexWave
{
    public enum Key_HexBoundaryProfilePP { None = 0, Fixed = 1, Absorb = 2, Amplify = 3 }
    [CreateAssetMenu(fileName = "HexBoundaryProfilePP_", menuName = "AllProperties/HexBoundaryProfileProperty")]
    public sealed class HexBoundaryProfileProperty : EnumStringKeyedProperty<Key_HexBoundaryProfilePP>
    {
        public HexSpellEdgeMode mode = HexSpellEdgeMode.Fixed;
        [Min(0)] public float damping = 5;
        [Min(1)] public int absorptionWidth = 2;
        public HexAmplifySettings amplification = HexAmplifySettings.Default;
        public HexBoundarySettings Settings => new HexBoundarySettings
        { mode = mode, damping = damping, absorptionWidth = absorptionWidth, profile = EnumKey };
        public bool IsValid => Settings.IsValid && (mode != HexSpellEdgeMode.Amplify || amplification.IsValid);
    }
}
