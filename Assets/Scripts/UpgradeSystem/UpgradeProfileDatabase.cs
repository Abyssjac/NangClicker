using JackyUtility;
using UnityEngine;

[CreateAssetMenu(
    fileName = "UpgradeProfileDB_",
    menuName = "AllPropertyDatabases/UpgradeProfileDatabase")]
public class UpgradeProfileDatabase
    : EnumStringKeyedDatabase<UpgradeProfileProperty, Key_UpgradeProfilePP>
{
#if UNITY_EDITOR
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
#endif
}
