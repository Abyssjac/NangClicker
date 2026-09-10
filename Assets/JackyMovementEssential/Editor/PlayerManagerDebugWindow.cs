using System.Collections.Generic;
using JackyUtility;
using UnityEditor;
using UnityEngine;

public class PlayerManagerDebugWindow : DebugEditorWindow<PlayerManager>
{
    [MenuItem("Jacky Tools/Player Manager")]
    public static void ShowWindow() =>
        GetWindow<PlayerManagerDebugWindow>("Player Manager").Show();

    private void OnInspectorUpdate()
    {
        if (Application.isPlaying)
            Repaint();
    }

    protected override void DrawContent()
    {
        PlayerManager manager = PlayerManager.Instance != null ? PlayerManager.Instance : Target;
        if (manager == null)
        {
            EditorGUILayout.HelpBox("No PlayerManager instance found.", MessageType.Warning);
            return;
        }

        PlayerAgent[] registeredPlayers = manager.GetRegisteredPlayers();

        DrawManagerOverview(manager, registeredPlayers);
        DrawDebugMovementLockControls(manager, registeredPlayers);
        DrawRegisteredPlayers(manager, registeredPlayers);
        DrawActivePlayerCapabilities(manager);
    }

    private void DrawManagerOverview(PlayerManager manager, PlayerAgent[] registeredPlayers)
    {
        Header("Manager");

        EditorGUILayout.ObjectField("Instance", manager, typeof(PlayerManager), true);
        EditorGUILayout.ObjectField("Active Player", manager.ActivePlayer, typeof(PlayerAgent), true);
        Row("Registered Count", manager.RegisteredPlayerCount.ToString());
        Row("Play Mode", Application.isPlaying ? "Yes" : "No");

        if (manager.ActivePlayer == null)
        {
            EditorGUILayout.HelpBox(
                "No active player is registered. PlayerAgent registers itself after Start, so confirm that the PlayerManager exists before the player becomes active.",
                MessageType.Warning);
        }

        if (registeredPlayers.Length == 0)
        {
            EditorGUILayout.HelpBox("No players are currently registered.", MessageType.Info);
        }
    }

    private void DrawDebugMovementLockControls(PlayerManager manager, PlayerAgent[] registeredPlayers)
    {
        Header("Debug Movement Lock");

        EditorGUILayout.HelpBox(
            "These controls use PlayerManager itself as the lock owner. Release only removes locks owned by PlayerManager and never changes locks held by gameplay systems.",
            MessageType.Info);

        PlayerAgent activePlayer = manager.ActivePlayer;
        bool activePlayerAlreadyLockedByManager =
            activePlayer != null && activePlayer.HasMovementLockOwner(manager);

        using (new EditorGUI.DisabledScope(!Application.isPlaying || activePlayer == null || activePlayerAlreadyLockedByManager))
        {
            if (GUILayout.Button("Lock Active Player Movement"))
                activePlayer.AcquireMovementLock(manager);
        }

        List<PlayerAgent> managerLockedPlayers = GetPlayersLockedByManager(manager, registeredPlayers);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || managerLockedPlayers.Count == 0))
        {
            if (GUILayout.Button("Release PlayerManager Debug Locks"))
            {
                foreach (PlayerAgent player in managerLockedPlayers)
                    player.ReleaseMovementLock(manager);
            }
        }

        Row("PlayerManager Debug Locks", managerLockedPlayers.Count.ToString());
    }

    private void DrawRegisteredPlayers(PlayerManager manager, PlayerAgent[] registeredPlayers)
    {
        Header("Registered Players");

        if (registeredPlayers.Length == 0)
            return;

        for (int i = 0; i < registeredPlayers.Length; i++)
        {
            PlayerAgent player = registeredPlayers[i];
            if (player == null)
            {
                EditorGUILayout.LabelField($"{i}: <null>");
                continue;
            }

            bool isActive = player == manager.ActivePlayer;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"{i}: {player.name}",
                isActive ? EditorStyles.boldLabel : EditorStyles.label);

            if (GUILayout.Button("Select", GUILayout.Width(56)))
            {
                Selection.activeGameObject = player.gameObject;
                EditorGUIUtility.PingObject(player.gameObject);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Active", isActive ? "Yes" : "No");
            EditorGUILayout.LabelField("Movement", player.IsMovementLocked ? "Locked" : "Available");
            EditorGUILayout.LabelField("Movement Lock Owners", player.MovementLockOwnerCount.ToString());
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawActivePlayerCapabilities(PlayerManager manager)
    {
        Header("Active Player Capabilities");

        PlayerAgent player = manager.ActivePlayer;
        if (player == null)
        {
            EditorGUILayout.HelpBox("Select or register an active player to inspect its capabilities.", MessageType.Info);
            return;
        }

        EditorGUILayout.ObjectField("Player Agent", player, typeof(PlayerAgent), true);
        EditorGUILayout.ObjectField("Player Movement CC", player.PlayerMovementCC, typeof(PlayerMovementCC), true);

        bool inputEnabled = player.PlayerMovementCC != null && player.PlayerMovementCC.InputEnabled;
        Row("PlayerMovementCC Input Enabled", player.PlayerMovementCC == null ? "Missing" : inputEnabled ? "Yes" : "No");

        DrawMovementCapability(player);
    }

    private void DrawMovementCapability(PlayerAgent player)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("State", player.IsMovementLocked ? "Locked" : "Available");
        EditorGUILayout.LabelField("Lock Owner Count", player.MovementLockOwnerCount.ToString());

        MonoBehaviour[] owners = player.GetMovementLockOwners();
        if (owners.Length == 0)
        {
            EditorGUILayout.LabelField("Lock Owners", "None");
        }
        else
        {
            for (int i = 0; i < owners.Length; i++)
                DrawLockOwner(i, owners[i]);
        }

        if (player.PlayerMovementCC != null)
        {
            if (player.IsMovementLocked && player.PlayerMovementCC.InputEnabled)
            {
                EditorGUILayout.HelpBox(
                    "Movement has lock owners, but PlayerMovementCC.InputEnabled is true. The input state may have been changed by another script.",
                    MessageType.Warning);
            }
            else if (!player.IsMovementLocked && !player.PlayerMovementCC.InputEnabled)
            {
                EditorGUILayout.HelpBox(
                    "Movement has no PlayerAgent lock owners, but PlayerMovementCC.InputEnabled is false. Another script is controlling input directly.",
                    MessageType.Warning);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawLockOwner(int index, MonoBehaviour owner)
    {
        if (owner == null)
        {
            EditorGUILayout.LabelField($"{index}: <destroyed owner>");
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.ObjectField(owner, typeof(MonoBehaviour), true);

        if (GUILayout.Button("Select", GUILayout.Width(56)))
        {
            Selection.activeGameObject = owner.gameObject;
            EditorGUIUtility.PingObject(owner.gameObject);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField($"Owner {index}", $"{owner.GetType().Name} on {owner.gameObject.name}");
    }

    private static List<PlayerAgent> GetPlayersLockedByManager(PlayerManager manager, PlayerAgent[] registeredPlayers)
    {
        List<PlayerAgent> lockedPlayers = new List<PlayerAgent>();
        for (int i = 0; i < registeredPlayers.Length; i++)
        {
            PlayerAgent player = registeredPlayers[i];
            if (player != null && player.HasMovementLockOwner(manager))
                lockedPlayers.Add(player);
        }

        PlayerAgent[] scenePlayers = FindObjectsByType<PlayerAgent>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < scenePlayers.Length; i++)
        {
            PlayerAgent player = scenePlayers[i];
            if (player != null && !lockedPlayers.Contains(player) && player.HasMovementLockOwner(manager))
                lockedPlayers.Add(player);
        }

        return lockedPlayers;
    }
}
