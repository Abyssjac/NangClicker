using JackyUtility;
using UnityEngine;

namespace NangClicker.HexWave
{
    [CreateAssetMenu(fileName = "HexSpellDB_", menuName = "AllPropertyDatabases/HexSpellDatabase")]
    public sealed class HexSpellDatabase : EnumStringKeyedDatabase<HexSpellProperty, Key_HexSpellPP> { }
}
