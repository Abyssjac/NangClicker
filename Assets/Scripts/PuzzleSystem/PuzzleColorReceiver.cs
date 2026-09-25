using UnityEngine;

/// <summary>
/// Bridges one color channel from a <see cref="PuzzleColorActivationManager"/> to a concrete
/// <see cref="BasePuzzleTarget"/>. It contributes itself as one regular target source, so color
/// activation coexists with the target's other direct sources.
/// </summary>
[DisallowMultipleComponent]
public sealed class PuzzleColorReceiver : MonoBehaviour
{
    [SerializeField] private PuzzleColorType colorType = PuzzleColorType.None;
    [SerializeField] private PuzzleColorActivationManager activationManager;
    [SerializeField] private BasePuzzleTarget target;

    private bool hasStarted;
    private bool isSubscribed;
    private bool reportedMissingConfiguration;

    public PuzzleColorType ColorType => colorType;
    public PuzzleColorActivationManager ActivationManager => activationManager;
    public BasePuzzleTarget Target => target;

    private void Awake()
    {
        ResolveTargetIfMissing();
    }

    private void OnEnable()
    {
        if (hasStarted)
            SubscribeAndSynchronize();
    }

    private void Start()
    {
        hasStarted = true;
        SubscribeAndSynchronize();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ReleaseTarget();
    }

    private void OnValidate()
    {
        ResolveTargetIfMissing();
    }

    private void SubscribeAndSynchronize()
    {
        if (colorType == PuzzleColorType.None)
        {
            ReleaseTarget();
            return;
        }

        if (activationManager == null || target == null)
        {
            ReportMissingConfigurationOnce();
            ReleaseTarget();
            return;
        }

        if (!isSubscribed)
        {
            activationManager.OnColorStateChanged += HandleColorStateChanged;
            isSubscribed = true;
        }

        target.SetTrigger(this, activationManager.IsColorActive(colorType));
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || activationManager == null)
            return;

        activationManager.OnColorStateChanged -= HandleColorStateChanged;
        isSubscribed = false;
    }

    private void HandleColorStateChanged(PuzzleColorType changedColorType, bool isActive)
    {
        if (changedColorType == colorType && target != null)
            target.SetTrigger(this, isActive);
    }

    private void ReleaseTarget()
    {
        if (target != null)
            target.SetTrigger(this, false);
    }

    private void ResolveTargetIfMissing()
    {
        if (target == null)
            target = GetComponent<BasePuzzleTarget>();
    }

    private void ReportMissingConfigurationOnce()
    {
        if (reportedMissingConfiguration || !Application.isPlaying)
            return;

        reportedMissingConfiguration = true;
        Debug.LogWarning($"[{nameof(PuzzleColorReceiver)}] '{name}' requires both an activation manager and a target.", this);
    }
}
