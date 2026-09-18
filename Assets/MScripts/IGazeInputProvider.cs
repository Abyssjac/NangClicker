using UnityEngine;

namespace MonaLisaGame
{
    /// <summary>Supplies a normalized world-movement direction for the current frame.</summary>
    public interface IGazeInputProvider
    {
        Vector2 GetCurrentInput();
    }
}
