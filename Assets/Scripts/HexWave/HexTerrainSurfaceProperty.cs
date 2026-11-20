using JackyUtility;
using UnityEngine;

namespace NangClicker.HexWave
{
    public enum Key_HexTerrainSurfacePP { None = 0, Grass = 1, Dirt = 2, Rock = 3, Snow = 4, Water = 5 }

    [CreateAssetMenu(fileName = "HexTerrainSurfacePP_", menuName = "AllProperties/HexTerrainSurfaceProperty")]
    public sealed class HexTerrainSurfaceProperty : EnumStringKeyedProperty<Key_HexTerrainSurfacePP>
    {
        public string displayName;
        public Material material;
        public Color editorColor = Color.green;
    }
}
