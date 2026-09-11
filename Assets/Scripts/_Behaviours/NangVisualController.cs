using UnityEngine;

/// <summary>
/// Gives a Nang visual a centre-pivoted, spring-damped press response.
/// This is visual-only: it reads successful press events from <see cref="NangBehaviour"/>
/// and never changes score or production data.
/// </summary>
[DisallowMultipleComponent]
public class NangVisualController : MonoBehaviour
{
    private const float SettleTiltThreshold = 0.001f;
    private const float SettleVelocityThreshold = 0.001f;

    [Header("References")]
    [Tooltip("The input source that reports successful Nang presses.")]
    [SerializeField] private NangBehaviour nangBehaviour;

    [Tooltip("The visual root that pivots around the geometric centre of the Nang. Its local Y axis must be normal to the Nang surface.")]
    [SerializeField] private Transform visualPivot;

    [Header("Press Response")]
    [Tooltip("A constant downward force is used for every press. Distance from the pivot naturally changes the torque.")]
    [SerializeField, Min(0f)] private float pressForce = 1f;

    [Tooltip("Converts the force lever arm into angular velocity. Increase this to make every press feel lighter and more reactive.")]
    [SerializeField, Min(0f)] private float inverseMomentOfInertia = 120f;

    [Tooltip("Hard limit that prevents rapid presses from flipping the Nang.")]
    [SerializeField, Range(0f, 45f)] private float maximumTiltDegrees = 11f;

    [Header("Spring")]
    [Tooltip("Strength pulling the Nang back to its captured resting rotation.")]
    [SerializeField, Min(0f)] private float springStrength = 70f;

    [Tooltip("Resistance against angular velocity. Lower values allow more rebound.")]
    [SerializeField, Min(0f)] private float damping = 12f;

    private Quaternion restingLocalRotation;
    private Vector2 tiltDegrees;
    private Vector2 angularVelocityDegrees;

    public Transform VisualPivot => visualPivot != null ? visualPivot : transform;
    public Vector3 SurfaceNormal => VisualPivot.up;
    public Vector3 PressIntoSurfaceDirection => -SurfaceNormal;

    private void Reset()
    {
        CacheDefaultReferences();
    }

    private void Awake()
    {
        CacheDefaultReferences();
        CaptureRestingRotation();
    }

    private void OnEnable()
    {
        if (nangBehaviour != null)
            nangBehaviour.OnNangPressed += HandleNangPressed;
    }

    private void OnDisable()
    {
        if (nangBehaviour != null)
            nangBehaviour.OnNangPressed -= HandleNangPressed;
    }

    private void OnValidate()
    {
        CacheDefaultReferences();

        if (!Application.isPlaying)
            CaptureRestingRotation();
    }

    private void LateUpdate()
    {
        if (visualPivot == null)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        Vector2 angularAcceleration = (-springStrength * tiltDegrees) - (damping * angularVelocityDegrees);
        angularVelocityDegrees += angularAcceleration * deltaTime;
        tiltDegrees += angularVelocityDegrees * deltaTime;

        ConstrainTilt();

        if (tiltDegrees.sqrMagnitude < SettleTiltThreshold * SettleTiltThreshold &&
            angularVelocityDegrees.sqrMagnitude < SettleVelocityThreshold * SettleVelocityThreshold)
        {
            tiltDegrees = Vector2.zero;
            angularVelocityDegrees = Vector2.zero;
        }

        visualPivot.localRotation = restingLocalRotation * Quaternion.Euler(tiltDegrees.x, 0f, tiltDegrees.y);
    }

    [ContextMenu("Capture Current Rotation As Resting Pose")]
    public void CaptureRestingRotation()
    {
        if (visualPivot != null)
            restingLocalRotation = visualPivot.localRotation;
    }

    private void HandleNangPressed(NangPressInfo pressInfo)
    {
        if (visualPivot == null || pressForce <= 0f || inverseMomentOfInertia <= 0f)
            return;

        Transform pivot = VisualPivot;

        // The pivot's local Y axis is the outward Nang surface normal. Projecting onto its XZ
        // plane gives the centre-to-press lever arm, independent of the Collider's current tilt.
        Vector3 localPressPoint = pivot.InverseTransformPoint(pressInfo.WorldPoint);
        Vector3 leverArm = new Vector3(localPressPoint.x, 0f, localPressPoint.z);

        // A manual press always goes into the Nang, opposite to its outward surface normal.
        // Convert both the lever arm and force into the pivot's local space before computing
        // torque, so model/world rotations cannot invert the visual response.
        Vector3 inwardForceWorld = PressIntoSurfaceDirection * pressForce;
        Vector3 inwardForceLocal = pivot.InverseTransformDirection(inwardForceWorld);
        Vector3 torque = Vector3.Cross(leverArm, inwardForceLocal);

        // X and Z torque are the only axes that tilt a flat Nang. The same force is used for
        // all clicks: a press further from the centre has a longer lever arm and thus more torque.
        angularVelocityDegrees += new Vector2(torque.x, torque.z) * inverseMomentOfInertia;
    }

    private void ConstrainTilt()
    {
        float maximumTilt = Mathf.Max(0f, maximumTiltDegrees);
        float tiltMagnitude = tiltDegrees.magnitude;

        if (tiltMagnitude <= maximumTilt)
            return;

        Vector2 boundaryDirection = tiltDegrees / tiltMagnitude;
        tiltDegrees = boundaryDirection * maximumTilt;

        // Remove only the velocity that would continue pushing past the boundary. Tangential
        // velocity is retained so a fast hit still feels like it skims along the limit.
        float outwardVelocity = Vector2.Dot(angularVelocityDegrees, boundaryDirection);
        if (outwardVelocity > 0f)
            angularVelocityDegrees -= boundaryDirection * outwardVelocity;
    }

    private void CacheDefaultReferences()
    {
        if (nangBehaviour == null)
            nangBehaviour = GetComponent<NangBehaviour>();

        if (visualPivot == null)
            visualPivot = transform;
    }
}
