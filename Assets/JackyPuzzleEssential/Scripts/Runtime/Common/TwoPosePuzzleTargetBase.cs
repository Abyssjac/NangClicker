using UnityEngine;

/// <summary>
/// A target that smoothly travels between its authored initial pose and a target pose marker.
/// The moving root and marker must be siblings (or otherwise share the same parent) so their
/// local poses use the same coordinate space.
/// </summary>
public abstract class TwoPosePuzzleTargetBase : BasePuzzleTarget
{
    [Header("Pose References")]
    [SerializeField] private Transform movingRoot;
    [SerializeField] private Transform targetPose;

    [Header("Position Smoothing")]
    [Tooltip("Higher values approach the target more quickly. This is smoothing responsiveness, not metres per second.")]
    [SerializeField, Min(0.01f)] private float positionLerpSharpness = 8f;
    [SerializeField, Min(0.0001f)] private float positionSnapDistance = 0.01f;

    [Header("Rotation Smoothing")]
    [Tooltip("Higher values approach the target rotation more quickly.")]
    [SerializeField, Min(0.01f)] private float rotationLerpSharpness = 8f;
    [SerializeField, Min(0.01f)] private float rotationSnapAngle = 0.25f;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private bool hasInitialPose;

    public Transform MovingRoot => movingRoot;
    public Transform TargetPose => targetPose;
    public bool HasValidPoseConfiguration => movingRoot != null
        && targetPose != null
        && movingRoot.parent == targetPose.parent;
    public Quaternion PreviewTargetRotation => ShouldMoveRotation && targetPose != null
        ? targetPose.rotation
        : movingRoot != null ? movingRoot.rotation : Quaternion.identity;

    protected virtual bool ShouldMoveRotation => true;

    protected override void Awake()
    {
        base.Awake();
        CacheInitialPose();
        ApplyInitialStateImmediately();
    }

    private void Update()
    {
        if (!hasInitialPose || !HasValidPoseConfiguration)
            return;

        Vector3 targetPosition = IsTriggered ? targetPose.localPosition : initialLocalPosition;
        MovePositionTowards(targetPosition);

        if (ShouldMoveRotation)
        {
            Quaternion targetRotation = IsTriggered ? targetPose.localRotation : initialLocalRotation;
            MoveRotationTowards(targetRotation);
        }
    }

    protected override void ApplyTriggeredState(bool isTriggered)
    {
        // Update performs the smooth travel. This method deliberately has no instantaneous motion.
    }

    protected override void ApplyInitialStateImmediately()
    {
        if (!hasInitialPose || movingRoot == null)
            return;

        movingRoot.localPosition = initialLocalPosition;
        movingRoot.localRotation = initialLocalRotation;
    }

    private void CacheInitialPose()
    {
        if (movingRoot == null)
            return;

        initialLocalPosition = movingRoot.localPosition;
        initialLocalRotation = movingRoot.localRotation;
        hasInitialPose = true;
    }

    private void MovePositionTowards(Vector3 targetPosition)
    {
        float snapDistanceSquared = positionSnapDistance * positionSnapDistance;
        if ((movingRoot.localPosition - targetPosition).sqrMagnitude <= snapDistanceSquared)
        {
            movingRoot.localPosition = targetPosition;
            return;
        }

        float interpolation = 1f - Mathf.Exp(-positionLerpSharpness * Time.deltaTime);
        movingRoot.localPosition = Vector3.Lerp(movingRoot.localPosition, targetPosition, interpolation);
    }

    private void MoveRotationTowards(Quaternion targetRotation)
    {
        if (Quaternion.Angle(movingRoot.localRotation, targetRotation) <= rotationSnapAngle)
        {
            movingRoot.localRotation = targetRotation;
            return;
        }

        float interpolation = 1f - Mathf.Exp(-rotationLerpSharpness * Time.deltaTime);
        movingRoot.localRotation = Quaternion.Slerp(movingRoot.localRotation, targetRotation, interpolation);
    }

    private void OnValidate()
    {
        positionLerpSharpness = Mathf.Max(0.01f, positionLerpSharpness);
        positionSnapDistance = Mathf.Max(0.0001f, positionSnapDistance);
        rotationLerpSharpness = Mathf.Max(0.01f, rotationLerpSharpness);
        rotationSnapAngle = Mathf.Max(0.01f, rotationSnapAngle);
    }
}
