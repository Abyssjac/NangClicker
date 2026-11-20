using JackyUtility;
using UnityEngine;

namespace NangClicker.HexWave
{
    [CreateAssetMenu(fileName = "HexTerrainSurfaceDB_", menuName = "AllPropertyDatabases/HexTerrainSurfaceDatabase")]
    public sealed class HexTerrainSurfaceDatabase : EnumStringKeyedDatabase<HexTerrainSurfaceProperty, Key_HexTerrainSurfacePP> { }
}
