using JackyUtility;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ScoreModifierDB_",
    menuName = "AllPropertyDatabases/ScoreModifierDatabase")]
public class ScoreModifierDatabase
    : EnumStringKeyedDatabase<ScoreModifierProperty, Key_ScoreModifierPP>
{
#if UNITY_EDITOR
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
#endif
}
