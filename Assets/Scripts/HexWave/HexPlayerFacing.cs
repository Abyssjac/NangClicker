using UnityEngine;

namespace NangClicker.HexWave
{
    /// <summary>Fixed upright sprite orientation. Only the local visual pivot rotates; never billboards.</summary>
    [DisallowMultipleComponent]
    public sealed class HexPlayerFacing : MonoBehaviour
    {
        [SerializeField] private Transform flipPivot;
        [Min(0.01f), SerializeField] private float turnDuration = 0.15f;
        private PlayerControl input;
        private HexPlayerMotor motor;
        private float startAngle, angle, targetAngle, elapsed;
        private void Awake() { input = GetComponent<PlayerControl>(); motor = GetComponent<HexPlayerMotor>(); }
        public void SetHorizontalFacing(float horizontal)
        {
            if (Mathf.Abs(horizontal) < 0.1f) return;
            float target = horizontal < 0 ? 180 : 0;
            if (target == targetAngle) return;
            startAngle = angle; targetAngle = target; elapsed = 0;
        }
        private void LateUpdate()
        {
            if (input != null && (motor == null || motor.InputAllowed)) SetHorizontalFacing(input.MoveInput().x);
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / turnDuration));
            angle = Mathf.Lerp(startAngle, targetAngle, u);
            if (flipPivot != null) flipPivot.localRotation = Quaternion.Euler(0, angle, 0);
        }
    }
}
