using JackyUtility;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ScoreModifierDatabase_",
    menuName = "Nang Clicker/Score System/Score Modifier Database")]
public class ScoreModifierDatabase
    : EnumStringKeyedDatabase<ScoreModifierProperty, ScoreModifierPropertyId>
{
#if UNITY_EDITOR
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
#endif
}
