using JackyUtility;
using UnityEngine;

namespace NangClicker.HexWave
{
    [CreateAssetMenu(fileName = "HexBoundaryProfileDB_", menuName = "AllPropertyDatabases/HexBoundaryProfileDatabase")]
    public sealed class HexBoundaryProfileDatabase : EnumStringKeyedDatabase<HexBoundaryProfileProperty, Key_HexBoundaryProfilePP> { }
}
