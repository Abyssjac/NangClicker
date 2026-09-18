using UnityEngine;

namespace MonaLisaGame
{
    /// <summary>Temporary keyboard implementation of <see cref="IGazeInputProvider"/>.</summary>
    public sealed class WASDGazeInputProvider : MonoBehaviour, IGazeInputProvider
    {
        public Vector2 GetCurrentInput()
        {
            Vector2 input = Vector2.zero;

            if (Input.GetKey(KeyCode.A))
                input.x -= 1f;
            if (Input.GetKey(KeyCode.D))
                input.x += 1f;
            if (Input.GetKey(KeyCode.S))
                input.y -= 1f;
            if (Input.GetKey(KeyCode.W))
                input.y += 1f;

            return Vector2.ClampMagnitude(input, 1f);
        }
    }
}
