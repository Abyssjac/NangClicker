using UnityEngine;

/// <summary>
/// A puzzle object that derives its active state from one or more trigger sources.
/// </summary>
public interface IPuzzleTarget : IPuzzleResettable
{
    bool IsTriggered { get; }

    /// <summary>
    /// Adds or removes a source's contribution. A target is triggered while it has at least one source.
    /// </summary>
    void SetTrigger(Object source, bool isTriggered);
}
