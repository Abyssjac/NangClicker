using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[DisallowMultipleComponent]
public class PlayerMovementRB : MonoBehaviour
{
    // ----------------------------
    // References
    // ----------------------------
    private Rigidbody rb;
    private CapsuleCollider capsule;
    private PlayerControl controls;

    // ---------------------------
    // Camera Settings (for camera-relative movement)
    // ---------------------------
    [SerializeField] private Camera playerCameraPrefab;
    [SerializeField] private Camera playerCameraInstance;

    // ----------------------------
    // Movement Settings
    // ----------------------------
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("转向平滑度（越大越快）")]
    [SerializeField] private float rotateSpeed = 15f;

    [Tooltip("是否允许相机相对移动（2.5D 常见）。不开则世界坐标 WASD。")]
    [SerializeField] private bool useCameraRelativeMove = true;
    [SerializeField] private Transform moveReference;

    // ----------------------------
    // Ground
    // ----------------------------
    [Header("Ground")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckExtra = 0.15f;

    // ----------------------------
    // Dash Settings
    // ----------------------------
    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 1f;

    [Tooltip("Dash 方向锁定方式：true=锁定当前面朝方向；false=锁定当前输入方向")]
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
    private Vector3 moveDirWorld;
    private Vector3 planarVelocity;
    private bool isGrounded;

    // Dash runtime
    private bool isDashing;
    private float dashTimer;
    private float lastDashTime;
    private Vector3 dashDirWorld;

    // Debug cache
    private bool prevGrounded;
    private bool prevDashing;

    // Camera runtime
    private Camera spawnedCamera;
    public Camera LocalCamera => spawnedCamera;

    // ----------------------------
    // Lifecycle
    // ----------------------------

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        controls = GetComponent<PlayerControl>();
        spawnedCamera = playerCameraInstance != null ? playerCameraInstance : null;

        // Configure Rigidbody for character movement
        rb.useGravity = true;            // 使用 Rigidbody 自带重力
        rb.freezeRotation = true;        // 不让物理引擎转
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void Start()
    {
        EnsureLocalCamera();
        SceneManager.sceneLoaded += OnSceneLoadedEnsureCamera;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedEnsureCamera;

        if (spawnedCamera != null)
            Destroy(spawnedCamera.gameObject);

        spawnedCamera = null;
    }

    // ----------------------------
    // Update: Input + Dash timer + Rotation
    // ----------------------------

    private void Update()
    {
        ReadInput();
        UpdateDashState();
        HandleRotation();
        DebugStateChanges();
    }

    // ----------------------------
    // FixedUpdate: Physics movement
    // ----------------------------

    private void FixedUpdate()
    {
        isGrounded = CheckGrounded();

        // 计算水平速度
        Vector3 desiredPlanarVel = GetDesiredPlanarVelocity();
        planarVelocity = desiredPlanarVel;

        // Dash 覆盖水平速度
        if (isDashing)
            planarVelocity = dashDirWorld * dashSpeed;

        // 保留 Rigidbody 当前的竖直速度，交给物理系统处理重力
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 finalVelocity = new Vector3(planarVelocity.x, currentVelocity.y, planarVelocity.z);
        rb.linearVelocity = finalVelocity;
    }

    // ----------------------------
    // Camera
    // ----------------------------

    private void OnSceneLoadedEnsureCamera(Scene scene, LoadSceneMode mode)
    {
        EnsureLocalCamera();
    }

    private void EnsureLocalCamera()
    {
        if (playerCameraPrefab == null)
        {
            Debug.LogError("[PlayerMovementRB] playerCameraPrefab is NULL.", this);
            return;
        }

        if (spawnedCamera != null)
        {
            var cameraBase = spawnedCamera.GetComponent<CameraBase>();
            if (cameraBase != null)
                cameraBase.SetTarget(transform);

            if (useCameraRelativeMove && moveReference == null)
                moveReference = spawnedCamera.transform;

            return;
        }

        spawnedCamera = Instantiate(playerCameraPrefab);
        var camBase = spawnedCamera.GetComponent<CameraBase>();
        if (camBase != null)
        {
            camBase.SetTarget(transform);
        }
        else
        {
            Debug.LogWarning("[PlayerMovementRB] Spawned camera prefab missing CameraBase.", spawnedCamera);
        }

        if (useCameraRelativeMove && moveReference == null)
        {
            Debug.LogWarning("[PlayerMovementRB] useCameraRelativeMove is true but moveReference is not set. Auto-setting to player camera.", this);
            moveReference = spawnedCamera.transform;
        }
    }

    // ----------------------------
    // Debug GUI
    // ----------------------------

    private void OnGUI()
    {
        if (!debugOverlay) return;

        GUILayout.BeginArea(new Rect(10, 10, 460, 220), GUI.skin.box);
        GUILayout.Label("[PlayerMovementRB]");
        GUILayout.Label($"Grounded: {isGrounded} | Dashing: {isDashing}");
        GUILayout.Label($"MoveDir: {moveDirWorld} (mag={moveDirWorld.magnitude:0.00})");
        GUILayout.Label($"PlanarVel: {planarVelocity} (mag={planarVelocity.magnitude:0.00})");
        GUILayout.Label($"VerticalVel: {(rb != null ? rb.linearVelocity.y : 0f):0.00}");
        GUILayout.Label($"RB Velocity: {(rb != null ? rb.linearVelocity.ToString() : "N/A")}");
        float cdLeft = Mathf.Max(0f, dashCooldown - (Time.time - lastDashTime));
        GUILayout.Label($"DashTimer: {dashTimer:0.00} | DashCD left: {cdLeft:0.00}");
        GUILayout.EndArea();
    }

    // ----------------------------
    // Input
    // ----------------------------

    private void ReadInput()
    {
        Vector2 input = controls.MoveInput();
        Vector3 raw = new Vector3(input.x, 0f, input.y);
        if (raw.sqrMagnitude > 1f) raw.Normalize();

        if (useCameraRelativeMove && moveReference != null)
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
        if (isDashing) return Vector3.zero;
        return moveDirWorld * moveSpeed;
    }

    // ----------------------------
    // Rotation
    // ----------------------------

    private void HandleRotation()
    {
        if (isDashing) return;
        if (moveDirWorld.sqrMagnitude < 0.01f) return;

        Quaternion target = Quaternion.LookRotation(moveDirWorld, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * rotateSpeed);
    }

    // ----------------------------
    // Ground
    // ----------------------------

    private bool CheckGrounded()
    {
        // 使用 CapsuleCollider 的几何参数进行 SphereCast 检测地面
        float radius = capsule.radius;
        Vector3 worldCenter = transform.TransformPoint(capsule.center);
        float halfHeight = Mathf.Max(0f, capsule.height * 0.5f - radius);
        Vector3 sphereOrigin = worldCenter + Vector3.down * halfHeight;

        float castDist = groundCheckExtra;

        bool hit = Physics.SphereCast(
            sphereOrigin, radius * 0.9f, Vector3.down, out RaycastHit _,
            castDist, groundMask, QueryTriggerInteraction.Ignore);

        if (debugDrawRays)
        {
            // 中心射线 + 4 个边缘射线用于 debug 可视化
            Vector3 foot = worldCenter + Vector3.down * halfHeight;
            float rayLen = radius + groundCheckExtra;
            Debug.DrawRay(foot, Vector3.down * rayLen, hit ? Color.green : Color.red);

            float r = radius * 0.9f;
            Vector3 right = transform.right * r;
            Vector3 forward = transform.forward * r;
            Debug.DrawRay(foot + right, Vector3.down * rayLen, hit ? Color.green : Color.red);
            Debug.DrawRay(foot - right, Vector3.down * rayLen, hit ? Color.green : Color.red);
            Debug.DrawRay(foot + forward, Vector3.down * rayLen, hit ? Color.green : Color.red);
            Debug.DrawRay(foot - forward, Vector3.down * rayLen, hit ? Color.green : Color.red);
        }

        return hit;
    }

    // ----------------------------
    // Dash
    // ----------------------------

    private void UpdateDashState()
    {
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
                isDashing = false;
        }

        if (controls.DashTriggered())
            TryStartDash();
    }

    private void TryStartDash()
    {
        if (isDashing) return;
        if ((Time.time - lastDashTime) < dashCooldown) return;

        if (moveDirWorld.sqrMagnitude < 0.01f && !dashLockToFacing) return;

        isDashing = true;
        dashTimer = dashDuration;
        lastDashTime = Time.time;

        dashDirWorld = dashLockToFacing ? transform.forward : moveDirWorld.normalized;
    }

    // ----------------------------
    // Debug
    // ----------------------------

    private void DebugStateChanges()
    {
        if (!debugLogStateChanges) return;

        if (prevGrounded != isGrounded)
        {
            Debug.Log($"[PlayerMovementRB] Grounded changed: {prevGrounded} -> {isGrounded}", this);
            prevGrounded = isGrounded;
        }

        if (prevDashing != isDashing)
        {
            Debug.Log($"[PlayerMovementRB] Dashing changed: {prevDashing} -> {isDashing}", this);
            prevDashing = isDashing;
        }
    }

    // ----------------------------
    // Utility / Teleport
    // ----------------------------

    public void TeleportToPosition(Vector3 position)
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.position = position;

        planarVelocity = Vector3.zero;
        isDashing = false;
    }

    public void TeleportToPosition(Vector3 position, Quaternion rotation)
    {
        TeleportToPosition(position);
        transform.rotation = rotation;
    }
}