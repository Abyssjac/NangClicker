using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    private readonly List<PlayerAgent> registeredPlayers = new List<PlayerAgent>();
    private PlayerAgent activePlayer;

    public PlayerAgent ActivePlayer => activePlayer;
    public bool HasActivePlayer => activePlayer != null;
    public int RegisteredPlayerCount => registeredPlayers.Count;

    public event Action<PlayerAgent> OnPlayerRegistered;
    public event Action<PlayerAgent> OnPlayerUnregistered;
    public event Action<PlayerAgent, PlayerAgent> OnActivePlayerChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool RegisterPlayer(PlayerAgent player)
    {
        if (player == null)
        {
            Debug.LogWarning($"[{nameof(PlayerManager)}] Cannot register a null player.", this);
            return false;
        }

        if (registeredPlayers.Contains(player))
            return false;

        registeredPlayers.Add(player);
        OnPlayerRegistered?.Invoke(player);

        if (activePlayer == null)
            SetActivePlayer(player);

        Debug.Log($"[{nameof(PlayerManager)}] Player {player.gameObject.name} Registered to the PlayerManager; Current Active Player is {activePlayer.gameObject.name}");
        return true;
    }

    public bool UnregisterPlayer(PlayerAgent player)
    {
        if (player == null)
            return false;

        if (!registeredPlayers.Remove(player))
            return false;

        if (activePlayer == player)
            SetActivePlayer(null);

        OnPlayerUnregistered?.Invoke(player);

        Debug.Log($"[{nameof(PlayerManager)}] Player {player.gameObject.name} Unregistered from the PlayerManager; Current Active Player is {activePlayer.gameObject.name}");
        return true;
    }

    public bool SetActivePlayer(PlayerAgent player)
    {
        if (player != null && !registeredPlayers.Contains(player))
        {
            Debug.LogWarning($"[{nameof(PlayerManager)}] Player '{player.name}' must be registered before becoming active.", this);
            return false;
        }

        if (activePlayer == player)
            return true;

        PlayerAgent previousPlayer = activePlayer;
        activePlayer = player;
        OnActivePlayerChanged?.Invoke(previousPlayer, activePlayer);
        return true;
    }

    public bool TryGetActivePlayer(out PlayerAgent player)
    {
        player = activePlayer;
        return player != null;
    }

    public PlayerAgent[] GetRegisteredPlayers()
    {
        return registeredPlayers.ToArray();
    }

    public bool AcquireActivePlayerMovementLock(MonoBehaviour owner)
    {
        if (!TryGetActivePlayer(out PlayerAgent player))
        {
            Debug.LogWarning($"[{nameof(PlayerManager)}] Cannot acquire a movement lock because there is no active player.", this);
            return false;
        }

        return player.AcquireMovementLock(owner);
    }

    public bool ReleaseActivePlayerMovementLock(MonoBehaviour owner)
    {
        if (!TryGetActivePlayer(out PlayerAgent player))
        {
            Debug.LogWarning($"[{nameof(PlayerManager)}] Cannot release a movement lock because there is no active player.", this);
            return false;
        }

        return player.ReleaseMovementLock(owner);
    }
}
