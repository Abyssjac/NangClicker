using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared trigger-source bookkeeping for puzzle targets. Concrete targets only implement the
/// movement or presentation that corresponds to the effective triggered state.
/// </summary>
public abstract class BasePuzzleTarget : PuzzleResettableBehaviour, IPuzzleTarget
{
    private readonly HashSet<UnityEngine.Object> triggerSources = new();

    /// <summary>Raised only when the effective state changes.</summary>
    public event Action<bool> OnTriggeredStateChanged;

    public bool IsTriggered { get; private set; }
    public override int ResetPriority => 100;

    public void SetTrigger(UnityEngine.Object source, bool isTriggered)
    {
        if (source == null)
        {
            Debug.LogWarning($"[{nameof(BasePuzzleTarget)}] Ignored a trigger change without a valid source.", this);
            return;
        }

        PruneDestroyedSources();

        bool changed = isTriggered
            ? triggerSources.Add(source)
            : triggerSources.Remove(source);

        if (changed)
            UpdateEffectiveState();
    }

    public override void ResetPuzzle()
    {
        triggerSources.Clear();

        if (IsTriggered)
        {
            IsTriggered = false;
            ApplyTriggeredState(false);
            OnTriggeredStateChanged?.Invoke(false);
        }

        ApplyInitialStateImmediately();
    }

    protected abstract void ApplyTriggeredState(bool isTriggered);
    protected abstract void ApplyInitialStateImmediately();

    private void UpdateEffectiveState()
    {
        bool nextState = triggerSources.Count > 0;
        if (IsTriggered == nextState)
            return;

        IsTriggered = nextState;
        ApplyTriggeredState(nextState);
        OnTriggeredStateChanged?.Invoke(nextState);
    }

    private void PruneDestroyedSources()
    {
        triggerSources.RemoveWhere(source => source == null);
    }
}
