using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level registry and reset entry point for all <see cref="PuzzleResettableBehaviour"/> instances.
/// Static scene objects are discovered once (including inactive objects); dynamically created objects register themselves.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class PuzzleResetManager : MonoBehaviour
{
    private static readonly HashSet<PuzzleResettableBehaviour> RegisteredResettables = new();
    private static readonly List<PuzzleResettableBehaviour> OrderedResettables = new();

    public static PuzzleResetManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"[{nameof(PuzzleResetManager)}] Only one manager is allowed per loaded scene set.", this);
            enabled = false;
            return;
        }

        Instance = this;
        DiscoverSceneResettables();
    }

    private void Start()
    {
        // Covers components whose Awake order placed them after this manager.
        DiscoverSceneResettables();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Resets sources/logic before targets according to <see cref="IPuzzleResettable.ResetPriority"/>.</summary>
    public void ResetAllPuzzles()
    {
        PruneDestroyedResettables();
        OrderedResettables.Clear();
        OrderedResettables.AddRange(RegisteredResettables);
        OrderedResettables.Sort(CompareResettables);

        foreach (PuzzleResettableBehaviour resettable in OrderedResettables)
            resettable.ResetPuzzle();
    }

    public static void Register(PuzzleResettableBehaviour resettable)
    {
        if (resettable != null)
            RegisteredResettables.Add(resettable);
    }

    public static void Unregister(PuzzleResettableBehaviour resettable)
    {
        if (resettable != null)
            RegisteredResettables.Remove(resettable);
    }

    private static int CompareResettables(PuzzleResettableBehaviour left, PuzzleResettableBehaviour right)
    {
        int priorityComparison = left.ResetPriority.CompareTo(right.ResetPriority);
        return priorityComparison != 0
            ? priorityComparison
            : left.GetInstanceID().CompareTo(right.GetInstanceID());
    }

    private void DiscoverSceneResettables()
    {
        PuzzleResettableBehaviour[] sceneResettables = FindObjectsByType<PuzzleResettableBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (PuzzleResettableBehaviour resettable in sceneResettables)
            Register(resettable);
    }

    private static void PruneDestroyedResettables()
    {
        RegisteredResettables.RemoveWhere(resettable => resettable == null);
    }

    [ContextMenu("Reset All Puzzles")]
    private void ResetAllPuzzlesFromContextMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{nameof(PuzzleResetManager)}] Enter Play Mode before resetting puzzle runtime state.", this);
            return;
        }

        ResetAllPuzzles();
    }
}
