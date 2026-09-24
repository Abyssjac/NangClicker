using UnityEngine;

/// <summary>
/// A triggered platform that travels linearly between two rail poses. Removing its final trigger
/// pauses it at the current progress; adding a trigger later resumes in the same direction.
/// </summary>
[DisallowMultipleComponent]
public sealed class PingPongRailMovingPlatform : BasePuzzleTarget
{
    private const float MinimumTravelDuration = 0.01f;

    [Header("Rail References")]
    [SerializeField] private Transform movingRoot;
    [SerializeField] private Transform railStartPose;
    [SerializeField] private Transform railEndPose;

    [Header("Initial Rail State")]
    [SerializeField, Range(0f, 1f)] private float initialProgress;
    [Tooltip("True means movement initially heads toward Rail End Pose; false heads toward Rail Start Pose.")]
    [SerializeField] private bool initiallyMovesTowardEnd = true;

    [Header("Movement")]
    [Tooltip("Seconds required to traverse from one rail endpoint to the other. Position and rotation both advance linearly over this time.")]
    [SerializeField, Min(MinimumTravelDuration)] private float secondsPerFullTraversal = 3f;

    private float progress;
    private int direction;
    private bool hasInitialized;

    public Transform MovingRoot => movingRoot;
    public Transform RailStartPose => railStartPose;
    public Transform RailEndPose => railEndPose;
    public float Progress => progress;
    public int Direction => direction;
    public float PreviewProgress => Application.isPlaying && hasInitialized ? progress : initialProgress;
    public int PreviewDirection => Application.isPlaying && hasInitialized
        ? direction
        : initiallyMovesTowardEnd ? 1 : -1;
    public bool HasValidRailConfiguration => movingRoot != null
        && railStartPose != null
        && railEndPose != null
        && movingRoot.parent == railStartPose.parent
        && movingRoot.parent == railEndPose.parent;

    protected override void Awake()
    {
        base.Awake();
        ResetRailState();
    }

    private void Update()
    {
        if (!IsTriggered || !hasInitialized || !HasValidRailConfiguration)
            return;

        AdvanceProgress(Time.deltaTime / secondsPerFullTraversal);
        ApplyProgress();
    }

    protected override void ApplyTriggeredState(bool isTriggered)
    {
        // The update loop moves only while triggered. Pausing preserves progress and direction.
    }

    protected override void ApplyInitialStateImmediately()
    {
        ResetRailState();
    }

    private void ResetRailState()
    {
        progress = Mathf.Clamp01(initialProgress);
        direction = initiallyMovesTowardEnd ? 1 : -1;
        hasInitialized = HasValidRailConfiguration;

        if (hasInitialized)
            ApplyProgress();
    }

    private void AdvanceProgress(float deltaProgress)
    {
        float remainingProgress = Mathf.Max(0f, deltaProgress);
        while (remainingProgress > 0f)
        {
            float distanceToEndpoint = direction > 0 ? 1f - progress : progress;
            if (remainingProgress <= distanceToEndpoint)
            {
                progress += direction * remainingProgress;
                break;
            }

            progress = direction > 0 ? 1f : 0f;
            remainingProgress -= distanceToEndpoint;
            direction *= -1;

            // When already sitting on an endpoint, flip once and use the remaining distance.
            if (distanceToEndpoint <= 0f && remainingProgress <= 0f)
                break;
        }

        progress = Mathf.Clamp01(progress);
    }

    private void ApplyProgress()
    {
        movingRoot.localPosition = Vector3.Lerp(railStartPose.localPosition, railEndPose.localPosition, progress);
        movingRoot.localRotation = Quaternion.Slerp(railStartPose.localRotation, railEndPose.localRotation, progress);
    }

    private void OnValidate()
    {
        initialProgress = Mathf.Clamp01(initialProgress);
        secondsPerFullTraversal = Mathf.Max(MinimumTravelDuration, secondsPerFullTraversal);
    }
}
