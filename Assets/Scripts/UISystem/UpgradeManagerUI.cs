using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Listens to UpgradeManager snapshots and maps them onto fixed child SlotId components.
/// It never determines unlocks, prices, levels, or purchase validity.
/// </summary>
public class UpgradeManagerUI : MonoBehaviour
{
    [SerializeField] private UpgradeDetailUI detailUI;

    private readonly Dictionary<UpgradeSlotId, UpgradeSlotBehaviour> slotsById = new();
    private readonly Dictionary<UpgradeSlotId, UpgradeSlotSnapshot> snapshotsBySlot = new();
    private readonly HashSet<UpgradeSlotId> missingSlotWarnings = new();

    private UpgradeManager upgradeManager;
    private UpgradeSlotId selectedSlotId;

    private void Awake()
    {
        UpgradeSlotBehaviour[] slots = GetComponentsInChildren<UpgradeSlotBehaviour>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            UpgradeSlotBehaviour slot = slots[i];
            if (slot.SlotId == UpgradeSlotId.None)
            {
                Debug.LogError($"[{nameof(UpgradeManagerUI)}] Slot '{slot.name}' has no SlotId.", slot);
                continue;
            }

            if (slotsById.ContainsKey(slot.SlotId))
            {
                Debug.LogError($"[{nameof(UpgradeManagerUI)}] Duplicate SlotId '{slot.SlotId}' on '{slot.name}'.", slot);
                continue;
            }

            slotsById.Add(slot.SlotId, slot);

            slot.SetSelectionCallback(SelectSlot);
            slot.Clear();
        }

        if (detailUI != null)
        {
            detailUI.OnPurchaseRequested += PurchaseSelected;
            detailUI.Clear();
        }
    }

    private void OnEnable()
    {
        ResolveManagerAndRefresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromManager();
    }

    private void OnDestroy()
    {
        if (detailUI != null)
            detailUI.OnPurchaseRequested -= PurchaseSelected;
    }

    private void Update()
    {
        if (upgradeManager == null)
            ResolveManagerAndRefresh();
    }

    private void ResolveManagerAndRefresh()
    {
        UpgradeManager candidate = UpgradeManager.Instance;
        if (candidate == null || candidate == upgradeManager)
            return;

        UnsubscribeFromManager();
        upgradeManager = candidate;
        upgradeManager.OnSnapshotsChanged += ApplySnapshots;
        ApplySnapshots(upgradeManager.GetSnapshots());
    }

    private void UnsubscribeFromManager()
    {
        if (upgradeManager != null)
            upgradeManager.OnSnapshotsChanged -= ApplySnapshots;

        upgradeManager = null;
    }

    private void ApplySnapshots(IReadOnlyList<UpgradeSlotSnapshot> snapshots)
    {
        snapshotsBySlot.Clear();
        foreach (KeyValuePair<UpgradeSlotId, UpgradeSlotBehaviour> pair in slotsById)
            pair.Value.Clear();

        for (int i = 0; i < snapshots.Count; i++)
        {
            UpgradeSlotSnapshot snapshot = snapshots[i];
            snapshotsBySlot[snapshot.SlotId] = snapshot;

            if (slotsById.TryGetValue(snapshot.SlotId, out UpgradeSlotBehaviour slot))
            {
                slot.Bind(snapshot);
            }
            else if (missingSlotWarnings.Add(snapshot.SlotId))
            {
                Debug.LogWarning(
                    $"[{nameof(UpgradeManagerUI)}] No {nameof(UpgradeSlotBehaviour)} is configured for SlotId '{snapshot.SlotId}'.",
                    this);
            }
        }

        RefreshSelectedDetail();
    }

    private void SelectSlot(UpgradeSlotId slotId)
    {
        if (!snapshotsBySlot.ContainsKey(slotId))
            return;

        selectedSlotId = slotId;
        RefreshSelectedDetail();
    }

    private void RefreshSelectedDetail()
    {
        if (detailUI == null)
            return;

        if (selectedSlotId != UpgradeSlotId.None
            && snapshotsBySlot.TryGetValue(selectedSlotId, out UpgradeSlotSnapshot snapshot))
        {
            detailUI.Bind(snapshot);
        }
        else
        {
            detailUI.Clear();
        }
    }

    private void PurchaseSelected()
    {
        if (upgradeManager == null
            || selectedSlotId == UpgradeSlotId.None
            || !snapshotsBySlot.TryGetValue(selectedSlotId, out UpgradeSlotSnapshot snapshot))
        {
            return;
        }

        upgradeManager.TryPurchase(snapshot.ProfileId);
    }
}
