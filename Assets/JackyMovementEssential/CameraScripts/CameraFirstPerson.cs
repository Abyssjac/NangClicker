using UnityEngine;

[DisallowMultipleComponent]
public class CameraFirstPerson : CameraBase
{
    [Tooltip("Local offset from the target pivot to the eye position.")]
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.65f, 0.1f);

    [Header("Mouse Sensitivity")]
    [SerializeField] private float sensitivityX = 2f;
    [SerializeField] private float sensitivityY = 2f;

    [Tooltip("Multiplier applied on top of base sensitivity for fine-tuning.")]
    [Range(0.1f, 5f)]
    [SerializeField] private float sensitivityMultiplier = 1f;

    [Header("Vertical Look Limits")]
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Smoothing")]
    [SerializeField] private bool enableSmoothing = true;

    [Tooltip("Smoothing time. Lower is snappier, higher is smoother.")]
    [Range(0.001f, 0.15f)]
    [SerializeField] private float smoothTime = 0.03f;

    [Header("Head Bob")]
    [SerializeField] private bool enableHeadBob = false;
    [SerializeField] private float bobFrequency = 8f;
    [SerializeField] private float bobAmplitudeY = 0.03f;
    [SerializeField] private float bobAmplitudeX = 0.015f;

    [Header("FOV")]
    [Range(50f, 120f)]
    [SerializeField] private float baseFOV = 75f;
    [SerializeField] private bool enableFOVKick = false;
    [SerializeField] private float fovKickSpeedThreshold = 6f;
    [SerializeField] private float fovKickAmount = 8f;
    [SerializeField] private float fovLerpSpeed = 6f;

    [Header("Cursor Lock")]
    [SerializeField] private bool lockCursor = true;

    [Header("Rotation Axis")]
    [SerializeField] private bool invertY = false;
    [SerializeField] private bool invertX = false;

    [Header("Target Body Rotation")]
    [SerializeField] private bool rotateTargetWithYaw = true;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = false;

    private float currentYaw;
    private float currentPitch;
    private float smoothYawVel;
    private float smoothPitchVel;
    private float smoothYaw;
    private float smoothPitch;
    private float bobTimer;
    private float currentFOV;
    private Vector3 lastTargetPos;

    public override void ActivateCamera()
    {
        base.ActivateCamera();

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        currentFOV = baseFOV;
        if (CachedCamera != null)
            CachedCamera.fieldOfView = baseFOV;

        SnapToTargetImmediate();
    }

    public override void DeactivateCamera()
    {
        base.DeactivateCamera();

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        smoothYawVel = 0f;
        smoothPitchVel = 0f;
        bobTimer = 0f;
    }

    public override void SetTarget(Transform newTarget)
    {
        base.SetTarget(newTarget);

        if (target != null)
            lastTargetPos = target.position;

        smoothYawVel = 0f;
        smoothPitchVel = 0f;
        bobTimer = 0f;

        if (CachedCamera != null && CachedCamera.enabled)
            SnapToTargetImmediate();
    }

    public override void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;

        float mouseX = Input.GetAxis("Mouse X") * sensitivityX * sensitivityMultiplier * (invertX ? -1f : 1f);
        float mouseY = Input.GetAxis("Mouse Y") * sensitivityY * sensitivityMultiplier * (invertY ? -1f : 1f);

        currentYaw += mouseX;
        currentPitch -= mouseY;
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        float yaw;
        float pitch;
        if (enableSmoothing)
        {
            smoothYaw = Mathf.SmoothDamp(smoothYaw, currentYaw, ref smoothYawVel, smoothTime);
            smoothPitch = Mathf.SmoothDamp(smoothPitch, currentPitch, ref smoothPitchVel, smoothTime);
            yaw = smoothYaw;
            pitch = smoothPitch;
        }
        else
        {
            yaw = currentYaw;
            pitch = currentPitch;
            smoothYaw = currentYaw;
            smoothPitch = currentPitch;
        }

        Vector3 velocity = (target.position - lastTargetPos) / Mathf.Max(dt, 0.0001f);
        Vector3 eyePos = target.position + target.TransformDirection(eyeOffset);

        if (enableHeadBob)
        {
            float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;

            if (horizontalSpeed > 0.2f)
            {
                bobTimer += dt * bobFrequency;
                float bobX = Mathf.Cos(bobTimer * Mathf.PI * 2f) * bobAmplitudeX;
                float bobY = Mathf.Sin(bobTimer * Mathf.PI * 2f) * bobAmplitudeY;
                eyePos += transform.right * bobX + transform.up * bobY;
            }
            else
            {
                bobTimer = 0f;
            }
        }

        transform.position = eyePos;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        if (rotateTargetWithYaw)
            target.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (enableFOVKick && CachedCamera != null)
        {
            float speed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            float targetFOV = speed > fovKickSpeedThreshold
                ? baseFOV + fovKickAmount
                : baseFOV;

            currentFOV = Mathf.Lerp(currentFOV, targetFOV, 1f - Mathf.Exp(-fovLerpSpeed * dt));
            CachedCamera.fieldOfView = currentFOV;
        }

        lastTargetPos = target.position;

        if (drawDebug)
        {
            Debug.DrawLine(target.position, transform.position, Color.green);
            Debug.DrawRay(transform.position, transform.forward * 2f, Color.red);
        }
    }

    private void SnapToTargetImmediate()
    {
        if (target == null) return;

        Vector3 eyePos = target.position + target.TransformDirection(eyeOffset);
        transform.position = eyePos;

        currentYaw = target.eulerAngles.y;
        currentPitch = 0f;
        smoothYaw = currentYaw;
        smoothPitch = currentPitch;
        smoothYawVel = 0f;
        smoothPitchVel = 0f;
        bobTimer = 0f;
        lastTargetPos = target.position;

        transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug || target == null) return;

        Vector3 eyePos = target.position + target.TransformDirection(eyeOffset);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(eyePos, 0.08f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(target.position, eyePos);
    }
}
