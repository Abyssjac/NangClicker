using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Presentation-only bridge from TasteManager state to the player's Nang toppings and the
/// Workshop world's Chef Special sign. TasteManager owns the sign root's unlock visibility;
/// this component owns only the preference icon appearance beneath it.
/// </summary>
[DisallowMultipleComponent]
public sealed class TasteVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TasteManager tasteManager;
    [SerializeField] private GameObject sesameRoot;
    [SerializeField] private GameObject cheeseRoot;
    [SerializeField] private GameObject chiliPowderRoot;
    [SerializeField] private GameObject cuminRoot;

    [Header("Chef Special Preference Icons")]
    [SerializeField] private GameObject sesamePreferenceIcon;
    [SerializeField] private GameObject cheesePreferenceIcon;
    [SerializeField] private GameObject chiliPowderPreferenceIcon;
    [SerializeField] private GameObject cuminPreferenceIcon;
    [SerializeField, Range(0f, 1f)] private float inactivePreferenceIconAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] private float inactivePreferenceIconGrayBlend = 1f;

    private bool isSubscribed;
    private readonly Dictionary<SpriteRenderer, Color> preferenceIconBaseColors = new();

    private void Reset()
    {
        TryResolveManager();
    }

    private void OnEnable()
    {
        TryResolveManager();
        CachePreferenceIconBaseColors();
        Subscribe();
        RefreshVisual();
    }

    private void Update()
    {
        if (tasteManager == null && TryResolveManager())
        {
            Subscribe();
            RefreshVisual();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            RefreshVisual();
    }

    private bool TryResolveManager()
    {
        if (tasteManager != null)
            return true;

        tasteManager = TasteManager.Instance;
        return tasteManager != null;
    }

    private void Subscribe()
    {
        if (isSubscribed || tasteManager == null)
            return;

        tasteManager.OnCurrentSpicesChanged += HandleCurrentSpicesChanged;
        tasteManager.OnPreferenceChanged += HandlePreferenceChanged;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (isSubscribed && tasteManager != null)
        {
            tasteManager.OnCurrentSpicesChanged -= HandleCurrentSpicesChanged;
            tasteManager.OnPreferenceChanged -= HandlePreferenceChanged;
        }

        isSubscribed = false;
    }

    private void HandleCurrentSpicesChanged(IReadOnlyList<SpiceType> spices)
    {
        RefreshVisual();
    }

    private void HandlePreferenceChanged(IReadOnlyList<SpiceType> preference)
    {
        RefreshPreferenceVisual();
    }

    private void RefreshVisual()
    {
        if (tasteManager == null)
            return;

        IReadOnlyList<SpiceType> currentSpices = tasteManager.CurrentSpices;
        SetRootActive(sesameRoot, currentSpices.Contains(SpiceType.Sesame));
        SetRootActive(cheeseRoot, currentSpices.Contains(SpiceType.Cheese));
        SetRootActive(chiliPowderRoot, currentSpices.Contains(SpiceType.ChiliPowder));
        SetRootActive(cuminRoot, currentSpices.Contains(SpiceType.Cumin));

        RefreshPreferenceVisual();
    }

    private void RefreshPreferenceVisual()
    {
        if (tasteManager == null)
            return;

        CachePreferenceIconBaseColors();
        IReadOnlyList<SpiceType> preference = tasteManager.CurrentPreference;
        SetPreferenceIconState(sesamePreferenceIcon, preference.Contains(SpiceType.Sesame));
        SetPreferenceIconState(cheesePreferenceIcon, preference.Contains(SpiceType.Cheese));
        SetPreferenceIconState(chiliPowderPreferenceIcon, preference.Contains(SpiceType.ChiliPowder));
        SetPreferenceIconState(cuminPreferenceIcon, preference.Contains(SpiceType.Cumin));
    }

    private void CachePreferenceIconBaseColors()
    {
        CacheRootRendererColors(sesamePreferenceIcon);
        CacheRootRendererColors(cheesePreferenceIcon);
        CacheRootRendererColors(chiliPowderPreferenceIcon);
        CacheRootRendererColors(cuminPreferenceIcon);
    }

    private void CacheRootRendererColors(GameObject iconRoot)
    {
        if (iconRoot == null)
            return;

        SpriteRenderer[] renderers = iconRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];
            if (renderer != null && !preferenceIconBaseColors.ContainsKey(renderer))
                preferenceIconBaseColors.Add(renderer, renderer.color);
        }
    }

    private void SetPreferenceIconState(GameObject iconRoot, bool isPreferred)
    {
        if (iconRoot == null)
            return;

        SpriteRenderer[] renderers = iconRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];
            if (renderer == null)
                continue;

            if (!preferenceIconBaseColors.TryGetValue(renderer, out Color baseColor))
            {
                baseColor = renderer.color;
                preferenceIconBaseColors.Add(renderer, baseColor);
            }

            Color targetColor = isPreferred
                ? baseColor
                : Color.Lerp(baseColor, Color.gray, inactivePreferenceIconGrayBlend);
            targetColor.a = isPreferred ? baseColor.a : baseColor.a * inactivePreferenceIconAlpha;
            renderer.color = targetColor;
        }
    }

    private static void SetRootActive(GameObject root, bool shouldBeActive)
    {
        if (root != null && root.activeSelf != shouldBeActive)
            root.SetActive(shouldBeActive);
    }
}
