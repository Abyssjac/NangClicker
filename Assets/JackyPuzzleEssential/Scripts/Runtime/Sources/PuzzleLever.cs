using TMPro;
using UnityEngine;

/// <summary>
/// An interactable puzzle source with a one-shot or toggle activation mode.
/// Player interaction code should call <see cref="TryInteract"/> when the player uses the lever.
/// </summary>
[DisallowMultipleComponent]
public sealed class PuzzleLever : PuzzleTriggerSourceBase
{
    public enum LeverMode
    {
        OneShot,
        Toggle,
    }

    private const float MinimumRotationSharpness = 0.01f;
    private const float MinimumSnapAngle = 0.01f;

    [Header("Lever Behaviour")]
    [SerializeField] private LeverMode leverMode;

    [Header("Lever Visual")]
    [SerializeField] private Transform leverPivot;
    [Tooltip("The pivot's authored local rotation is the untriggered pose.")]
    [SerializeField] private Vector3 triggeredLocalEulerAngles = new(0f, 0f, -45f);
    [SerializeField, Min(MinimumRotationSharpness)] private float rotationLerpSharpness = 10f;
    [SerializeField, Min(MinimumSnapAngle)] private float rotationSnapAngle = 0.25f;

    [Header("World Label")]
    [SerializeField] private TextMeshPro worldLabel;
    [SerializeField] private bool faceMainCamera = true;

    private Quaternion untriggeredLocalRotation;
    private bool hasInitialRotation;
    private bool hasBeenUsed;

    public LeverMode Mode => leverMode;
    public bool HasBeenUsed => hasBeenUsed;
    public bool CanInteract => isActiveAndEnabled && (leverMode == LeverMode.Toggle || !hasBeenUsed);

    protected override void Awake()
    {
        base.Awake();
        hasBeenUsed = leverMode == LeverMode.OneShot && IsSourceTriggered;
        CacheInitialRotation();
        ApplyLeverRotationImmediately(IsSourceTriggered);
        RefreshWorldLabel();
    }

    private void Update()
    {
        MoveLeverTowardsCurrentState();
    }

    private void LateUpdate()
    {
        if (!faceMainCamera || worldLabel == null || Camera.main == null)
            return;

        Transform cameraTransform = Camera.main.transform;
        worldLabel.transform.rotation = Quaternion.LookRotation(cameraTransform.forward, cameraTransform.up);
    }

    /// <summary>
    /// Invoked by a player interaction system. Returns false when a one-shot lever was already used.
    /// </summary>
    public bool TryInteract()
    {
        if (!CanInteract)
            return false;

        if (leverMode == LeverMode.OneShot)
        {
            hasBeenUsed = true;
            SetSourceTriggered(true);
        }
        else
        {
            SetSourceTriggered(!IsSourceTriggered);
        }

        RefreshWorldLabel();
        return true;
    }

    public override void ResetPuzzle()
    {
        hasBeenUsed = false;
        base.ResetPuzzle();
        ApplyLeverRotationImmediately(false);
        RefreshWorldLabel();
    }

    protected override void OnSourceTriggeredStateChanged(bool triggered)
    {
        if (leverMode == LeverMode.OneShot && triggered)
            hasBeenUsed = true;

        RefreshWorldLabel();
    }

    private void CacheInitialRotation()
    {
        if (leverPivot == null)
            return;

        untriggeredLocalRotation = leverPivot.localRotation;
        hasInitialRotation = true;
    }

    private void MoveLeverTowardsCurrentState()
    {
        if (!hasInitialRotation || leverPivot == null)
            return;

        Quaternion targetRotation = IsSourceTriggered
            ? Quaternion.Euler(triggeredLocalEulerAngles)
            : untriggeredLocalRotation;

        if (Quaternion.Angle(leverPivot.localRotation, targetRotation) <= rotationSnapAngle)
        {
            leverPivot.localRotation = targetRotation;
            return;
        }

        float interpolation = 1f - Mathf.Exp(-rotationLerpSharpness * Time.deltaTime);
        leverPivot.localRotation = Quaternion.Slerp(leverPivot.localRotation, targetRotation, interpolation);
    }

    private void ApplyLeverRotationImmediately(bool triggered)
    {
        if (!hasInitialRotation || leverPivot == null)
            return;

        leverPivot.localRotation = triggered
            ? Quaternion.Euler(triggeredLocalEulerAngles)
            : untriggeredLocalRotation;
    }

    private void RefreshWorldLabel()
    {
        if (worldLabel == null)
            return;

        if (leverMode == LeverMode.OneShot)
        {
            worldLabel.text = hasBeenUsed
                ? "One-Shot Lever\nUsed"
                : "One-Shot Lever";
            return;
        }

        worldLabel.text = IsSourceTriggered
            ? "Toggle Lever\nON"
            : "Toggle Lever\nOFF";
    }

    private void OnValidate()
    {
        rotationLerpSharpness = Mathf.Max(MinimumRotationSharpness, rotationLerpSharpness);
        rotationSnapAngle = Mathf.Max(MinimumSnapAngle, rotationSnapAngle);

        if (!Application.isPlaying)
            RefreshWorldLabel();
    }
}
