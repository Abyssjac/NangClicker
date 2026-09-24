/// <summary>
/// A puzzle participant whose authored state can be restored by <see cref="PuzzleResetManager"/>.
/// </summary>
public interface IPuzzleResettable
{
    /// <summary>
    /// Lower values reset first. Sources and puzzle logic should run before their targets.
    /// </summary>
    int ResetPriority { get; }

    /// <summary>Restores the participant to its configured initial state.</summary>
    void ResetPuzzle();
}
