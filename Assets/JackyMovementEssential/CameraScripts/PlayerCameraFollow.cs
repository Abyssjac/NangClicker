using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCameraFollow : CameraBase
{
    [Tooltip("Offset from target pivot to the point the camera follows.")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Angle (2.5D)")]
    [Tooltip("Yaw angle around the Y axis.")]
    [Range(0f, 360f)]
    [SerializeField] private float yaw = 45f;

    [Tooltip("Top-down pitch angle.")]
    [Range(10f, 89f)]
    [SerializeField] private float pitch = 60f;

    [Header("Distance / Zoom")]
    [SerializeField] private float distance = 10f;
    [SerializeField] private bool enableZoom = true;
    [SerializeField] private float zoomSpeed = 2.5f;
    [SerializeField] private float minDistance = 6f;
    [SerializeField] private float maxDistance = 18f;

    [Header("Follow Smoothing")]
    [SerializeField] private bool rotateWithTarget = false;
    [SerializeField] private float smoothTime = 0.10f;
    [SerializeField] private float rotationLerp = 12f;

    [Header("Look Ahead (Optional)")]
    [SerializeField] private bool enableLookAhead = false;
    [SerializeField] private float lookAheadDistance = 1.5f;
    [SerializeField] private float lookAheadSmoothTime = 0.12f;
    [SerializeField] private bool estimateVelocityFromTargetDelta = true;

    [Header("Clamp / Bounds (Optional)")]
    [SerializeField] private bool clampPosition = false;
    [SerializeField] private Vector2 minXZ = new Vector2(-50, -50);
    [SerializeField] private Vector2 maxXZ = new Vector2(50, 50);

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;

    private Vector3 posVel;
    private Vector3 lastTargetPos;
    private Vector3 lookAheadCurrent;
    private Vector3 lookAheadVel;

    [ContextMenu("Reset Parameters")]
    private void Reset()
    {
        yaw = 45f;
        pitch = 60f;
        distance = 10f;
        smoothTime = 0.10f;
        rotationLerp = 12f;
    }

    public override void ActivateCamera()
    {
        base.ActivateCamera();
        SnapToTargetImmediate();
    }

    public override void DeactivateCamera()
    {
        base.DeactivateCamera();
        posVel = Vector3.zero;
        lookAheadVel = Vector3.zero;
    }

    public override void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;

        if (enableZoom)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                distance -= scroll * zoomSpeed;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }
        }

        Vector3 focus = target.position + targetOffset;

        if (enableLookAhead)
        {
            Vector3 vel = Vector3.zero;

            if (estimateVelocityFromTargetDelta)
            {
                vel = (target.position - lastTargetPos) / Mathf.Max(dt, 0.0001f);
                lastTargetPos = target.position;
            }

            Vector3 planarVel = new Vector3(vel.x, 0f, vel.z);
            Vector3 dir = planarVel.sqrMagnitude > 0.01f ? planarVel.normalized : Vector3.zero;
            Vector3 targetLookAhead = dir * lookAheadDistance;

            lookAheadCurrent = Vector3.SmoothDamp(
                lookAheadCurrent,
                targetLookAhead,
                ref lookAheadVel,
                lookAheadSmoothTime
            );

            focus += lookAheadCurrent;
        }

        Quaternion rigRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPos = focus - (rigRot * Vector3.forward) * distance;
        Vector3 smoothedPos = Vector3.SmoothDamp(transform.position, desiredPos, ref posVel, smoothTime);

        if (clampPosition)
        {
            smoothedPos.x = Mathf.Clamp(smoothedPos.x, minXZ.x, maxXZ.x);
            smoothedPos.z = Mathf.Clamp(smoothedPos.z, minXZ.y, maxXZ.y);
        }

        transform.position = smoothedPos;

        Quaternion desiredRot = rotateWithTarget
            ? Quaternion.LookRotation(focus - transform.position, Vector3.up)
            : Quaternion.Euler(pitch, yaw, 0f);

        float t = 1f - Mathf.Exp(-rotationLerp * dt);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, t);

        if (drawDebug)
        {
            Debug.DrawLine(transform.position, focus, Color.cyan);
            Debug.DrawRay(focus, Vector3.up * 0.5f, Color.yellow);
        }
    }

    public override void SetTarget(Transform newTarget)
    {
        base.SetTarget(newTarget);

        if (target != null)
            lastTargetPos = target.position;

        lookAheadCurrent = Vector3.zero;
        lookAheadVel = Vector3.zero;
        posVel = Vector3.zero;

        if (CachedCamera != null && CachedCamera.enabled)
            SnapToTargetImmediate();
    }

    private void SnapToTargetImmediate()
    {
        if (target == null)
            return;

        Vector3 focus = target.position + targetOffset;
        Quaternion rigRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPos = focus - (rigRot * Vector3.forward) * distance;

        transform.position = desiredPos;
        transform.rotation = rotateWithTarget
            ? Quaternion.LookRotation(focus - transform.position, Vector3.up)
            : Quaternion.Euler(pitch, yaw, 0f);

        lastTargetPos = target.position;
        lookAheadCurrent = Vector3.zero;
        lookAheadVel = Vector3.zero;
        posVel = Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug || target == null) return;

        Vector3 focus = target.position + targetOffset;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(focus, 0.25f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, focus);
    }
}
