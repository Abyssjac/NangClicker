using UnityEngine;
using UnityEngine.EventSystems;

namespace NangClicker.HexWave
{
    /// <summary>Terrain-specific CC adapter for PlayerRootCC. Uses the existing PlayerControl and PlayerAgent locks.
    /// The package's generic camera-owning driver is disabled in the Variant, not modified.</summary>
    [DefaultExecutionOrder(-10), RequireComponent(typeof(CharacterController), typeof(PlayerAgent), typeof(PlayerControl))]
    public sealed class HexPlayerMotor : MonoBehaviour
    {
        [SerializeField] private HexWaveManager terrain;
        [SerializeField] private Camera movementCamera;
        [Min(0), SerializeField] private float walkSpeed = 3;
        [Min(0.01f), SerializeField] private float acceleration = 18;
        [Min(0.01f), SerializeField] private float deceleration = 24;
        [Min(0.01f), SerializeField] private float gravity = 20;
        [Min(0.01f), SerializeField] private float maxFallSpeed = 25;
        [Min(0.01f), SerializeField] private float groundProbe = 0.18f;
        [SerializeField] private float respawnBelowHeight = -20;
        [SerializeField] private HexCoordinate spawnCell;
        private CharacterController controller;
        private PlayerControl input;
        private PlayerAgent agent;
        private PlayerMovementCC movementLocks;
        private HexCellView support;
        private float supportTop, verticalVelocity;
        private Vector3 planarVelocity;
        private bool grounded;
        public bool IsGrounded => grounded;
        public HexCellView SupportCell => support;
        public bool InputAllowed => agent != null && !agent.IsMovementLocked &&
            (movementLocks == null || movementLocks.InputEnabled) && Time.timeScale > 0 && IsActivePlayer();
        private bool IsActivePlayer() => PlayerManager.Instance == null || PlayerManager.Instance.ActivePlayer == agent;
        private void Awake()
        {
            controller = GetComponent<CharacterController>(); input = GetComponent<PlayerControl>();
            agent = GetComponent<PlayerAgent>(); movementLocks = GetComponent<PlayerMovementCC>();
            if (terrain == null) terrain = FindFirstObjectByType<HexWaveManager>();
            if (movementCamera == null) movementCamera = Camera.main;
        }
        private void Start()
        {
            if (terrain != null) { if (!terrain.IsInitialized) terrain.RebuildGrid(); Respawn(); }
        }
        public bool TryGetCurrentCell(out HexCellView cell)
        { cell = null; return terrain != null && terrain.TryGetCellAtWorld(transform.position, out cell) && !cell.IsWall; }
        private bool EditingText()
        {
            var obj = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return obj != null && (obj.GetComponent<UnityEngine.UI.InputField>() != null || obj.GetComponent<TMPro.TMP_InputField>() != null);
        }
        private void Update()
        {
            if (Time.timeScale <= 0 || terrain == null || !terrain.IsInitialized) return;
            TickMovement(input != null && InputAllowed && !EditingText() ? input.MoveInput() : Vector2.zero, Time.deltaTime);
        }
        public void TickMovement(Vector2 axes, float dt)
        {
            if (controller == null || !controller.enabled || dt <= 0 || terrain == null) return;
            dt = Mathf.Min(dt, 0.1f);
            CarrySupport();
            if (!InputAllowed) axes = Vector2.zero;
            axes = Vector2.ClampMagnitude(axes, 1);
            Vector3 forward = Vector3.forward, right = Vector3.right;
            if (movementCamera != null)
            {
                forward = Vector3.ProjectOnPlane(movementCamera.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(movementCamera.transform.up, Vector3.up).normalized;
                right = Vector3.Cross(Vector3.up, forward).normalized;
            }
            var target = (right * axes.x + forward * axes.y) * walkSpeed;
            planarVelocity = InputAllowed ? Vector3.MoveTowards(planarVelocity, target,
                (target.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration) * dt) : Vector3.zero;
            // Wall roles remain non-walkable even when their authored top is flush with the floor.
            var destination = transform.position + planarVelocity * dt;
            if (terrain.TryGetCellAtWorld(destination, out var tile) && tile.IsWall) planarVelocity = Vector3.zero;
            verticalVelocity = grounded ? -2 : Mathf.Max(-maxFallSpeed, verticalVelocity - gravity * dt);
            var flags = controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0) verticalVelocity = 0;
            ProbeSupport();
            if (terrain.CellsRoot.InverseTransformPoint(transform.position).y < respawnBelowHeight) Respawn();
        }
        private void CarrySupport()
        {
            if (!grounded || support == null) return;
            if (!terrain.TryGetCellAtWorld(transform.position, out var under) || under != support) { grounded = false; support = null; return; }
            float top = support.SurfacePoint.y, delta = top - supportTop;
            if (Mathf.Abs(delta) > 0.000001f)
            {
                var collider = support.SurfaceCollider;
                bool ignored = collider != null && Physics.GetIgnoreCollision(controller, collider);
                try
                {
                    // Ignore only our supporting platform during its own kinematic carry; other walls still collide.
                    if (collider != null && !ignored) Physics.IgnoreCollision(controller, collider, true);
                    controller.Move(Vector3.up * delta);
                }
                finally { if (collider != null && !ignored) Physics.IgnoreCollision(controller, collider, false); }
            }
            supportTop = top;
        }
        private void ProbeSupport()
        {
            float foot = transform.position.y + controller.center.y - controller.height * 0.5f;
            var ray = new Ray(new Vector3(transform.position.x, foot + groundProbe, transform.position.z), Vector3.down);
            if (terrain.TryRaycastCell(ray, out var cell, out var hit, groundProbe * 2 + controller.skinWidth) &&
                !cell.IsWall && hit.normal.y > 0.65f && hit.point.y <= foot + groundProbe)
            {
                support = cell; supportTop = cell.SurfacePoint.y;
                grounded = foot - hit.point.y <= groundProbe;
                if (grounded) verticalVelocity = -2;
            }
            else { support = null; grounded = false; }
        }
        public void Respawn()
        {
            if (terrain == null || !terrain.TryGetCell(spawnCell, out var cell) || cell.IsWall) return;
            bool enabled = controller.enabled; controller.enabled = false;
            transform.position = cell.SurfacePoint + Vector3.up * 0.03f;
            controller.enabled = enabled; planarVelocity = Vector3.zero; verticalVelocity = -2;
            support = cell; supportTop = cell.SurfacePoint.y; grounded = true;
            Physics.SyncTransforms();
        }
    }
}
