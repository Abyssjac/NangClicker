using UnityEngine;

/// <summary>
/// A door that smoothly moves between its authored closed position and an open-position marker.
/// </summary>
[DisallowMultipleComponent]
public sealed class PuzzleDoor : TwoPosePuzzleTargetBase
{
    protected override bool ShouldMoveRotation => false;
}
