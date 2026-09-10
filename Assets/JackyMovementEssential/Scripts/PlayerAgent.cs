using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAgent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovementCC playerMovementCC;

    [Header("Debug")]
    [SerializeField] private bool logMovementLockChanges;

    private readonly HashSet<MonoBehaviour> movementLockOwners = new HashSet<MonoBehaviour>();
    private bool hasStarted;
    private bool isRegistered;

    public bool IsMovementLocked => movementLockOwners.Count > 0;
    public int MovementLockOwnerCount => movementLockOwners.Count;
    public PlayerMovementCC PlayerMovementCC => playerMovementCC;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();

        if (playerMovementCC == null)
            Debug.LogError($"[{nameof(PlayerAgent)}] Player '{name}' requires a {nameof(PlayerMovementCC)} reference.", this);
    }

    private void Start()
    {
        hasStarted = true;
        RegisterWithManager();
    }

    private void OnEnable()
    {
        if (hasStarted)
            RegisterWithManager();
    }

    private void OnDisable()
    {
        ClearMovementLocks();
        UnregisterFromManager();
    }

    private void Update()
    {
        if (PruneDestroyedMovementLockOwners())
            RefreshMovementInputState();
    }

    public bool AcquireMovementLock(MonoBehaviour owner)
    {
        if (owner == null)
        {
            Debug.LogWarning($"[{nameof(PlayerAgent)}] Cannot acquire a movement lock with a null owner.", this);
            return false;
        }

        PruneDestroyedMovementLockOwners();
        bool added = movementLockOwners.Add(owner);
        if (added)
        {
            RefreshMovementInputState();
            LogMovementLockChange($"Movement locked by {DescribeOwner(owner)}.");
        }

        return added;
    }

    public bool ReleaseMovementLock(MonoBehaviour owner)
    {
        if (owner == null)
        {
            PruneDestroyedMovementLockOwners();
            RefreshMovementInputState();
            return false;
        }

        bool removed = movementLockOwners.Remove(owner);
        if (removed)
        {
            RefreshMovementInputState();
            LogMovementLockChange($"Movement lock released by {DescribeOwner(owner)}.");
        }

        return removed;
    }

    public bool HasMovementLockOwner(MonoBehaviour owner)
    {
        if (owner == null)
            return false;

        PruneDestroyedMovementLockOwnersAndRefresh();
        return movementLockOwners.Contains(owner);
    }

    public MonoBehaviour[] GetMovementLockOwners()
    {
        PruneDestroyedMovementLockOwnersAndRefresh();
        MonoBehaviour[] owners = new MonoBehaviour[movementLockOwners.Count];
        movementLockOwners.CopyTo(owners);
        return owners;
    }

    private void RegisterWithManager()
    {
        if (isRegistered)
            return;

        if (PlayerManager.Instance == null)
        {
            Debug.LogWarning($"[{nameof(PlayerAgent)}] No {nameof(PlayerManager)} instance found. Player '{name}' was not registered.", this);
            return;
        }

        isRegistered = PlayerManager.Instance.RegisterPlayer(this);
    }

    private void UnregisterFromManager()
    {
        if (!isRegistered)
            return;

        if (PlayerManager.Instance != null)
            PlayerManager.Instance.UnregisterPlayer(this);

        isRegistered = false;
    }

    private void ClearMovementLocks()
    {
        if (movementLockOwners.Count == 0)
            return;

        movementLockOwners.Clear();
        RefreshMovementInputState();
    }

    private bool PruneDestroyedMovementLockOwners()
    {
        return movementLockOwners.RemoveWhere(owner => owner == null) > 0;
    }

    private void PruneDestroyedMovementLockOwnersAndRefresh()
    {
        if (PruneDestroyedMovementLockOwners())
            RefreshMovementInputState();
    }

    private void RefreshMovementInputState()
    {
        ResolveReferences();
        if (playerMovementCC != null)
            playerMovementCC.InputEnabled = !IsMovementLocked;
    }

    private void ResolveReferences()
    {
        if (playerMovementCC == null)
            playerMovementCC = GetComponent<PlayerMovementCC>();
    }

    private void LogMovementLockChange(string message)
    {
        if (logMovementLockChanges)
            Debug.Log($"[{nameof(PlayerAgent)}] {message} Active owners: {MovementLockOwnerCount}.", this);
    }

    private static string DescribeOwner(MonoBehaviour owner)
    {
        return $"{owner.GetType().Name} on {owner.gameObject.name}";
    }
}
