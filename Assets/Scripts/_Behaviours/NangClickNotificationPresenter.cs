using System.Globalization;
using UnityEngine;

/// <summary>
/// Presentation-only bridge from successful Nang presses to the reusable World Float notification channel.
/// </summary>
[DisallowMultipleComponent]
public class NangClickNotificationPresenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NangBehaviour nangBehaviour;
    [SerializeField] private NangVisualController nangVisualController;
    [SerializeField] private Camera presentationCamera;
    [SerializeField] private Sprite nangIcon;

    [Header("World Float")]
    [SerializeField] private Key_WorldFloatProfilePP profileKey = Key_WorldFloatProfilePP.NangClickGain;

    private void Reset()
    {
        CacheDefaultReferences();
    }

    private void Awake()
    {
        CacheDefaultReferences();
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
    }

    private void HandleNangPressed(NangPressInfo pressInfo)
    {
        NotificationManager manager = NotificationManager.Instance;
        if (manager == null || profileKey == Key_WorldFloatProfilePP.None)
            return;

        Camera camera = presentationCamera != null
            ? presentationCamera
            : nangBehaviour != null ? nangBehaviour.InteractionCamera : null;

        var request = new WorldFloatRequest(
            pressInfo.WorldPoint,
            "+" + FormatAmount(pressInfo.EarnedMoney),
            nangIcon,
            camera,
            ResolveSurfaceNormal(camera, pressInfo.WorldNormal));

        manager.ShowWorldFloat(profileKey, request);
    }

    private void CacheDefaultReferences()
    {
        if (nangBehaviour == null)
            nangBehaviour = GetComponent<NangBehaviour>();

        if (nangVisualController == null)
            nangVisualController = GetComponent<NangVisualController>();

        if (presentationCamera == null && nangBehaviour != null)
            presentationCamera = nangBehaviour.InteractionCamera;
    }

    private Vector3 ResolveSurfaceNormal(Camera camera, Vector3 fallbackNormal)
    {
        if (nangVisualController != null)
            return nangVisualController.SurfaceNormal;

        if (camera != null)
            return -camera.transform.forward;

        return fallbackNormal;
    }

    private static string FormatAmount(double amount)
    {
        string format = amount >= 100d ? "0" : "0.##";
        return amount.ToString(format, CultureInfo.InvariantCulture);
    }
}
