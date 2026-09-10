using UnityEngine;

[DisallowMultipleComponent]
public class CameraThirdPerson : CameraBase
{
    [Header("Target")]
    [Tooltip("Offset from target pivot to the point this camera looks at.")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);

    [Header("Orbit")]
    [SerializeField] private bool enableInputRotation = true;
    [SerializeField] private bool rotateOnlyWhenRightMouseHeld = false;
    [SerializeField] private float yaw = 0f;
    [SerializeField] private float pitch = 20f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;

    [Header("Sensitivity")]
    [SerializeField] private float sensitivityX = 3f;
    [SerializeField] private float sensitivityY = 2f;
    [SerializeField] private bool invertX = false;
    [SerializeField] private bool invertY = false;

    [Header("Distance / Zoom")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 8f;
    [SerializeField] private bool enableZoom = true;
    [SerializeField] private float zoomSpeed = 2f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.06f;
    [SerializeField] private float rotationLerp = 60f;
    [SerializeField] private float collisionDistanceLerp = 20f;

    [Header("Target Rotation")]
    [SerializeField] private bool rotateTargetWithYaw = false;
    [SerializeField] private bool alignYawToTargetOnActivate = true;

    [Header("Collision")]
    [SerializeField] private bool enableCollision = true;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float collisionBuffer = 0.15f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnActive = false;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;

    private float currentYaw;
    private float currentPitch;
    private float currentDistance;
    private Vector3 positionVelocity;

    protected override void Awake()
    {
        currentYaw = yaw;
        currentPitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);

        base.Awake();
    }

    public override void ActivateCamera()
    {
        base.ActivateCamera();

        if (lockCursorOnActive)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (alignYawToTargetOnActivate && target != null)
            currentYaw = target.eulerAngles.y;

        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        SnapToTargetImmediate();
    }

    public override void DeactivateCamera()
    {
        base.DeactivateCamera();

        if (lockCursorOnActive)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        positionVelocity = Vector3.zero;
    }

    public override void SetTarget(Transform newTarget)
    {
        base.SetTarget(newTarget);

        positionVelocity = Vector3.zero;

        if (alignYawToTargetOnActivate && target != null)
            currentYaw = target.eulerAngles.y;

        if (CachedCamera != null && CachedCamera.enabled)
            SnapToTargetImmediate();
    }

    public override void Update()
    {
        if (target == null) return;

        float dt = Time.deltaTime;
        ReadInput();

        Vector3 focus = target.position + targetOffset;
        Quaternion orbitRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        float correctedDistance = ResolveCollisionDistance(focus, orbitRotation);
        currentDistance = Mathf.Lerp(currentDistance, correctedDistance, 1f - Mathf.Exp(-collisionDistanceLerp * dt));

        Vector3 desiredPosition = focus - (orbitRotation * Vector3.forward) * currentDistance;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, positionSmoothTime);

        Quaternion desiredRotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationLerp * dt));

        if (rotateTargetWithYaw)
            target.rotation = Quaternion.Euler(0f, currentYaw, 0f);

        if (drawDebug)
        {
            Debug.DrawLine(focus, transform.position, Color.cyan);
            Debug.DrawRay(focus, Vector3.up * 0.5f, Color.yellow);
        }
    }

    private void ReadInput()
    {
        if (enableInputRotation && (!rotateOnlyWhenRightMouseHeld || Input.GetMouseButton(1)))
        {
            float mouseX = Input.GetAxis("Mouse X") * sensitivityX * (invertX ? -1f : 1f);
            float mouseY = Input.GetAxis("Mouse Y") * sensitivityY * (invertY ? -1f : 1f);

            currentYaw += mouseX;
            currentPitch -= mouseY;
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        }

        if (enableZoom)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                distance -= scroll * zoomSpeed;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }
        }
    }

    private float ResolveCollisionDistance(Vector3 focus, Quaternion orbitRotation)
    {
        float desiredDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        if (!enableCollision)
            return desiredDistance;

        Vector3 cameraDir = -(orbitRotation * Vector3.forward);
        if (Physics.SphereCast(
                focus,
                collisionRadius,
                cameraDir,
                out RaycastHit hit,
                desiredDistance,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            return Mathf.Clamp(hit.distance - collisionBuffer, minDistance, desiredDistance);
        }

        return desiredDistance;
    }

    private void SnapToTargetImmediate()
    {
        if (target == null)
            return;

        Vector3 focus = target.position + targetOffset;
        Quaternion orbitRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        currentDistance = ResolveCollisionDistance(focus, orbitRotation);

        transform.position = focus - (orbitRotation * Vector3.forward) * currentDistance;
        transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
        positionVelocity = Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug || target == null) return;

        Vector3 focus = target.position + targetOffset;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(focus, 0.25f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(focus, transform.position);
    }
}
