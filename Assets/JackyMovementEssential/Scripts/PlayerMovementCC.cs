using System;
using UnityEngine;
using UnityEngine.SceneManagement;


// CharacterController-based player movement script. Supports top-down and first-person camera modes.
// Includes WASD movement, facing rotation, gravity, dash, and a fixed-height jump.
// Make sure to set groundMask so ground detection works correctly.
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class PlayerMovementCC : MonoBehaviour
{
    // ----------------------------
    // References
    // ----------------------------
    private CharacterController controller;
    private PlayerControl controls;

    // ---------------------------
    // Camera Settings (for camera-relative movement)
    // ---------------------------
    [SerializeField] private Camera playerCameraPrefab;   // Camera prefab to spawn; runtime direction uses moveReference
    [SerializeField] private Camera playerCameraInstance; // Optional pre-placed camera instance

    // ----------------------------
    // Movement Settings
    // ----------------------------
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("Rotation smoothing (higher = snappier)")]
    [SerializeField] private float rotateSpeed = 15f;

    [Tooltip("Use camera-relative movement (common for 2.5D). If off, WASD is world-space.")]
    [SerializeField] private bool useCameraRelativeMove = true;
    [SerializeField] private Transform moveReference;

    // ----------------------------
    // Ground / Gravity
    // ----------------------------
    [Header("Ground / Gravity")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float gravity = 20f;             // Positive value; applied downward in code
    [SerializeField] private float fallSpeedMax = 25f;
    [SerializeField] private float groundStickVelocity = 0f;  // Small downward speed while grounded (prevents slope bounce)
    [SerializeField] private float groundRayExtra = 0.15f;    // Extra ray length for ground check

    // ----------------------------
    // Jump Settings
    // ----------------------------
    [Header("Jump")]
    [Tooltip("Maximum jump height in world units. The initial jump speed is calculated from gravity.")]
    [Min(0.01f)]
    [SerializeField] private float jumpHeight = 1.5f;

    // ----------------------------
    // Dash Settings
    // ----------------------------
    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 1f;

    [Tooltip("Dash direction lock: true = lock to facing direction; false = lock to input direction")]
    [SerializeField] private bool dashLockToFacing = true;

    // ----------------------------
    // Debug
    // ----------------------------
    [Header("Debug")]
    [SerializeField] private bool debugOverlay = true;
    [SerializeField] private bool debugDrawRays = true;
    [SerializeField] private bool debugLogStateChanges = false;

    // ----------------------------
    // Runtime State
    // ----------------------------
    private Vector3 moveDirWorld;        // Input direction (world-space, y=0)
    private Vector3 planarVelocity;      // Horizontal velocity (x,z)
    private float verticalVelocity;      // Vertical velocity (y)
    private bool isGrounded;

    // Dash runtime
    private bool isDashing;
    private float dashTimer;
    private float lastDashTime;
    private Vector3 dashDirWorld;

    // Debug cache (only log on change)
    private bool prevGrounded;
    private bool prevDashing;

    // Camera runtime
    private Camera spawnedCamera;
    public Camera LocalCamera => spawnedCamera;

    // Camera mode runtime
    private bool isFirstPersonMode;
    private CameraMode currentCameraMode;

    /// <summary>
    /// When false, player input is ignored (movement, dash, rotation all stop).
    /// Set by camera mode switches or any external system that needs to freeze the player.
    /// </summary>
    public bool InputEnabled { get; set; } = true;

    // ----------------------------
    // Unity
    // ----------------------------

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        controls = GetComponent<PlayerControl>();
        spawnedCamera = playerCameraInstance != null ? playerCameraInstance : null;
    }

    private void Start()
    {
        EnsureLocalCamera();
        SceneManager.sceneLoaded += OnSceneLoadedEnsureCamera;

        if (AllCameraManager.Instance != null)
            AllCameraManager.Instance.OnCameraModeSwitched += OnCameraModeSwitched;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedEnsureCamera;

        if (AllCameraManager.Instance != null)
            AllCameraManager.Instance.OnCameraModeSwitched -= OnCameraModeSwitched;

        if (spawnedCamera != null)
            Destroy(spawnedCamera.gameObject);

        spawnedCamera = null;
    }

    private void OnCameraModeSwitched(CameraMode newMode)
    {
        currentCameraMode = newMode;
        isFirstPersonMode = (newMode == CameraMode.FirstPerson);

        // Freeze player input when using free camera
        //InputEnabled = (newMode != CameraMode.FreeCamera);

        if (debugLogStateChanges)
            Debug.Log($"[PlayerMovementCC] Camera mode switched to {newMode}, isFirstPersonMode={isFirstPersonMode}, InputEnabled={InputEnabled}", this);
    }

    private void Update()
    {
        if (!InputEnabled)
        {
            // Still apply gravity so the player doesn't float
            isGrounded = CheckGrounded();
            ApplyGravity(Time.deltaTime);
            controller.Move(Vector3.down * verticalVelocity * Time.deltaTime);
            moveDirWorld = Vector3.zero;
            planarVelocity = Vector3.zero;
            return;
        }

        ReadInput();
        isGrounded = CheckGrounded();
        UpdateDashState();
        TryStartJump();

        // Ground/Gravity in Update: keep consistent within the same tick (recommended for CharacterController)
        ApplyGravity(Time.deltaTime);

        // Calculate horizontal velocity
        Vector3 desiredPlanarVel = GetDesiredPlanarVelocity();
        planarVelocity = desiredPlanarVel;

        // Dash overrides horizontal velocity
        if (isDashing)
            planarVelocity = dashDirWorld * dashSpeed;

        // Compose final velocity and move
        Vector3 velocity = planarVelocity + Vector3.down * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        HandleRotation();

        DebugStateChanges();
    }

    private void OnSceneLoadedEnsureCamera(Scene scene, LoadSceneMode mode)
    {
        EnsureLocalCamera();
    }

    private void EnsureLocalCamera()
    {
        if (playerCameraPrefab == null)
        {
            Debug.LogError("[PlayerMovement] playerCameraPrefab is NULL.", this);
            return;
        }

        // If we already spawned one and it still exists, just re-bind target
        if (spawnedCamera != null)
        {
            var cameraBase = spawnedCamera.GetComponent<CameraBase>();
            if (cameraBase != null)
                cameraBase.SetTarget(transform);

            if (useCameraRelativeMove && moveReference == null)
                moveReference = spawnedCamera.transform;

            return;
        }

        // Spawn a new one
        spawnedCamera = Instantiate(playerCameraPrefab);
        var camBase = spawnedCamera.GetComponent<CameraBase>();
        if (camBase != null)
        {
            camBase.SetTarget(transform);
        }
        else
        {
            Debug.LogWarning("[PlayerMovement] Spawned camera prefab missing CameraBase.", spawnedCamera);
        }

        if (useCameraRelativeMove && moveReference == null)
        {
            Debug.LogWarning("[PlayerMovementCC] useCameraRelativeMove is true but moveReference is not set. Auto-setting to player camera.", this);
            moveReference = spawnedCamera.transform;
        }
    }

    private void OnGUI()
    {
        if (!debugOverlay) return;

        GUILayout.BeginArea(new Rect(10, 10, 460, 240), GUI.skin.box);
        GUILayout.Label($"[PlayerMovementCC]  CameraMode: {currentCameraMode}");
        GUILayout.Label($"Grounded: {isGrounded} | Dashing: {isDashing}");
        GUILayout.Label($"MoveDir: {moveDirWorld} (mag={moveDirWorld.magnitude:0.00})");
        GUILayout.Label($"PlanarVel: {planarVelocity} (mag={planarVelocity.magnitude:0.00})");
        GUILayout.Label($"VerticalVel: {verticalVelocity:0.00}");
        float cdLeft = Mathf.Max(0f, dashCooldown - (Time.time - lastDashTime));
        GUILayout.Label($"DashTimer: {dashTimer:0.00} | DashCD left: {cdLeft:0.00}");
        GUILayout.Label($"FirstPersonMode: {isFirstPersonMode}");
        GUILayout.EndArea();
    }

    // ----------------------------
    // Input
    // ----------------------------
    private void ReadInput()
    {
        Vector2 input = controls.MoveInput(); // WASD / stick
        Vector3 raw = new Vector3(input.x, 0f, input.y);
        if (raw.sqrMagnitude > 1f) raw.Normalize();

        if (isFirstPersonMode)
        {
            // First-person: input is relative to player body (yaw controlled by camera)
            Vector3 bodyF = transform.forward;
            Vector3 bodyR = transform.right;
            bodyF.y = 0f;
            bodyR.y = 0f;
            bodyF.Normalize();
            bodyR.Normalize();

            moveDirWorld = bodyR * raw.x + bodyF * raw.z;
        }
        else if (useCameraRelativeMove && moveReference != null)
        {
            Vector3 camF = moveReference.transform.forward;
            Vector3 camR = moveReference.transform.right;
            camF.y = 0f;
            camR.y = 0f;
            camF.Normalize();
            camR.Normalize();

            moveDirWorld = camR * raw.x + camF * raw.z;
        }
        else
        {
            moveDirWorld = raw;
        }
    }

    // ----------------------------
    // Movement
    // ----------------------------
    private Vector3 GetDesiredPlanarVelocity()
    {
        // During dash, ignore input (direction is locked)
        if (isDashing) return Vector3.zero;

        // Normal movement
        return moveDirWorld * moveSpeed;
    }

    // ----------------------------
    // Rotation
    // ----------------------------
    private void HandleRotation()
    {
        // First-person: camera owns rotation, movement must not interfere
        if (isFirstPersonMode) return;

        // During dash: skip rotation (modify here if you want turning while dashing)
        if (isDashing) return;

        if (moveDirWorld.sqrMagnitude < 0.01f) return;

        Quaternion target = Quaternion.LookRotation(moveDirWorld, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * rotateSpeed);
    }

    // ----------------------------
    // Ground / Gravity
    // ----------------------------
    private bool CheckGrounded()
    {
        // Use CharacterController geometry for stable ray origin
        // Calculate foot center
        Vector3 worldCenter = transform.TransformPoint(controller.center);
        float halfHeight = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
        Vector3 foot = worldCenter + Vector3.down * halfHeight;

        float rayLen = controller.radius + groundRayExtra;

        // Center ray
        if (RaycastDown(foot, rayLen)) return true;

        // 4 edge points (can be extended to 8 if needed)
        float r = controller.radius * 0.9f;
        Vector3 right = transform.right * r;
        Vector3 forward = transform.forward * r;

        if (RaycastDown(foot + right, rayLen)) return true;
        if (RaycastDown(foot - right, rayLen)) return true;
        if (RaycastDown(foot + forward, rayLen)) return true;
        if (RaycastDown(foot - forward, rayLen)) return true;

        return false;
    }

    private bool RaycastDown(Vector3 origin, float length)
    {
        bool hit = Physics.Raycast(origin, Vector3.down, out RaycastHit _, length, groundMask, QueryTriggerInteraction.Ignore);

        if (debugDrawRays)
        {
            Debug.DrawRay(origin, Vector3.down * length, hit ? Color.green : Color.red);
        }

        return hit;
    }

    private void ApplyGravity(float dt)
    {
        if (isGrounded)
        {
            // Grounded: apply small downward speed to prevent isGrounded jitter
            verticalVelocity = groundStickVelocity;
            return;
        }

        // Airborne: accelerate downward (verticalVelocity positive = downward speed)
        verticalVelocity += gravity * dt;
        if (verticalVelocity > fallSpeedMax) verticalVelocity = fallSpeedMax;
    }

    // ----------------------------
    // Jump
    // ----------------------------
    private void TryStartJump()
    {
        // Ground-only jump: pressing Jump in the air is intentionally ignored.
        if (controls == null || !isGrounded || !controls.JumpTriggered())
            return;

        // verticalVelocity uses a downward-positive convention because movement applies Vector3.down.
        verticalVelocity = -Mathf.Sqrt(2f * gravity * jumpHeight);
        isGrounded = false;
    }

    // ----------------------------
    // Dash
    // ----------------------------
    private void UpdateDashState()
    {
        // Update dash timer first
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
                isDashing = false;
        }

        // Then check for new dash trigger
        if (controls.DashTriggered())
            TryStartDash();
    }

    private void TryStartDash()
    {
        if (isDashing) return;
        if ((Time.time - lastDashTime) < dashCooldown) return;

        // No direction, no dash (remove this check to allow stationary dash)
        if (moveDirWorld.sqrMagnitude < 0.01f && !dashLockToFacing) return;

        isDashing = true;
        dashTimer = dashDuration;
        lastDashTime = Time.time;

        // Lock dash direction
        dashDirWorld = dashLockToFacing ? transform.forward : moveDirWorld.normalized;

        // Ground dash sticks to the floor. Air dash keeps the current rise/fall velocity.
        if (isGrounded)
            verticalVelocity = groundStickVelocity;
    }

    // ----------------------------
    // Debug
    // ----------------------------
    private void DebugStateChanges()
    {
        if (!debugLogStateChanges) return;

        if (prevGrounded != isGrounded)
        {
            Debug.Log($"[PlayerMovementCC] Grounded changed: {prevGrounded} -> {isGrounded}", this);
            prevGrounded = isGrounded;
        }

        if (prevDashing != isDashing)
        {
            Debug.Log($"[PlayerMovementCC] Dashing changed: {prevDashing} -> {isDashing}", this);
            prevDashing = isDashing;
        }
    }

    // ----------------------------
    // Utility
    // ----------------------------

    public void TeleportToPosition(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
    }

    public void TeleportToPosition(Vector3 position, Quaternion rotation)
    {
        TeleportToPosition(position);
        transform.rotation = rotation;
    }
}
