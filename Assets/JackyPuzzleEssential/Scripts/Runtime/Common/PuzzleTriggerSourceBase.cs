using UnityEngine;

/// <summary>
/// Shared source lifecycle behaviour. Concrete levers, pressure plates, and puzzle controllers
/// inherit this class and call <see cref="SetSourceTriggered"/> when their own condition changes.
/// Disabling or destroying a source always removes its contribution from every target.
/// </summary>
public abstract class PuzzleTriggerSourceBase : PuzzleResettableBehaviour
{
    [SerializeField] private BasePuzzleTarget[] targets;
    [SerializeField] private bool startsTriggered;

    private bool isSourceTriggered;

    public override int ResetPriority => 0;
    public bool IsSourceTriggered => isSourceTriggered;

    protected override void Awake()
    {
        base.Awake();
        isSourceTriggered = startsTriggered;
    }

    protected virtual void OnEnable()
    {
        if (isSourceTriggered)
            BroadcastTriggerState(true);

        OnSourceTriggeredStateChanged(isSourceTriggered);
    }

    protected virtual void OnDisable()
    {
        // Preserve the logical state so a latched source can reassert itself on re-enable.
        // A pressure plate can override OnEnable and recalculate its physical overlap instead.
        BroadcastTriggerState(false);
    }

    protected override void OnDestroy()
    {
        BroadcastTriggerState(false);
        base.OnDestroy();
    }

    /// <summary>Updates this source's contribution to all configured targets.</summary>
    public void SetSourceTriggered(bool triggered)
    {
        if (isSourceTriggered == triggered)
            return;

        isSourceTriggered = triggered;
        BroadcastTriggerState(triggered);
        OnSourceTriggeredStateChanged(triggered);
    }

    public override void ResetPuzzle()
    {
        SetSourceTriggered(false);
    }

    private void BroadcastTriggerState(bool triggered)
    {
        if (targets == null)
            return;

        foreach (BasePuzzleTarget target in targets)
        {
            if (target != null)
                target.SetTrigger(this, triggered);
        }
    }

    /// <summary>
    /// Lets a concrete source synchronize its presentation with the same state sent to targets.
    /// </summary>
    protected virtual void OnSourceTriggeredStateChanged(bool triggered)
    {
    }
}
