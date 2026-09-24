using UnityEngine;

/// <summary>
/// MonoBehaviour base for resettable puzzle participants. It also lets a scene manager discover
/// runtime-created instances without requiring an Inspector list of interface references.
/// </summary>
public abstract class PuzzleResettableBehaviour : MonoBehaviour, IPuzzleResettable
{
    /// <summary>
    /// Generic puzzle logic resets after sources but before targets. Targets override this with 100.
    /// </summary>
    public virtual int ResetPriority => 50;

    protected virtual void Awake()
    {
        PuzzleResetManager.Register(this);
    }

    protected virtual void OnDestroy()
    {
        PuzzleResetManager.Unregister(this);
    }

    public abstract void ResetPuzzle();
}
