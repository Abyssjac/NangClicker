using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the active state of the color-puzzle channels in one level. A channel stays active while
/// at least one source remains registered for it, and channels are independent from one another.
/// </summary>
[DisallowMultipleComponent]
public sealed class PuzzleColorActivationManager : PuzzleResettableBehaviour
{
    private static readonly PuzzleColorType[] ManagedColors =
    {
        PuzzleColorType.PuzzleRed,
        PuzzleColorType.PuzzleBlue,
        PuzzleColorType.PuzzleGreen,
    };

    private readonly Dictionary<PuzzleColorType, HashSet<UnityEngine.Object>> activeSources = new();

    /// <summary>Raised only when a channel's effective active state changes.</summary>
    public event Action<PuzzleColorType, bool> OnColorStateChanged;

    /// <summary>Color state is reset after generic sources but before puzzle targets.</summary>
    public override int ResetPriority => 5;

    /// <summary>Returns whether the requested color channel currently has any live source.</summary>
    public bool IsColorActive(PuzzleColorType colorType)
    {
        if (!IsManagedColor(colorType))
            return false;

        PruneDestroyedSources(colorType);
        return TryGetSources(colorType, out HashSet<UnityEngine.Object> sources) && sources.Count > 0;
    }

    /// <summary>Returns the number of live sources currently maintaining a color channel.</summary>
    public int GetActiveSourceCount(PuzzleColorType colorType)
    {
        if (!IsManagedColor(colorType))
            return 0;

        PruneDestroyedSources(colorType);
        return TryGetSources(colorType, out HashSet<UnityEngine.Object> sources) ? sources.Count : 0;
    }

    /// <summary>
    /// Adds or removes a source for one color channel. Removing the final source turns that color
    /// off; sources registered to other colors remain unaffected.
    /// </summary>
    public void SetColorActive(UnityEngine.Object source, PuzzleColorType colorType, bool isActive)
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning($"[{nameof(PuzzleColorActivationManager)}] Ignored a color state change while the manager is disabled.", this);
            return;
        }

        if (!IsManagedColor(colorType))
        {
            if (colorType != PuzzleColorType.None)
                Debug.LogWarning($"[{nameof(PuzzleColorActivationManager)}] Unsupported color type: {colorType}.", this);
            return;
        }

        if (source == null)
        {
            Debug.LogWarning($"[{nameof(PuzzleColorActivationManager)}] Ignored a color state change without a valid source.", this);
            return;
        }

        PruneDestroyedSources(colorType);
        HashSet<UnityEngine.Object> sources = GetOrCreateSources(colorType);
        bool wasActive = sources.Count > 0;
        bool sourceSetChanged = isActive ? sources.Add(source) : sources.Remove(source);

        if (sourceSetChanged)
            PublishIfEffectiveStateChanged(colorType, wasActive);
    }

    /// <summary>
    /// Sets the manager's own test source. This is intended for the color-manager EditorWindow and
    /// is deliberately independent from future gameplay trigger sources.
    /// </summary>
    public void SetManualColorActive(PuzzleColorType colorType, bool isActive)
    {
        SetColorActive(this, colorType, isActive);
    }

    /// <summary>Clears every active color source and broadcasts any channels that became inactive.</summary>
    public void ClearAllColors()
    {
        foreach (PuzzleColorType colorType in ManagedColors)
        {
            if (!TryGetSources(colorType, out HashSet<UnityEngine.Object> sources) || sources.Count == 0)
                continue;

            sources.Clear();
            OnColorStateChanged?.Invoke(colorType, false);
        }
    }

    public override void ResetPuzzle()
    {
        ClearAllColors();
    }

    private void Update()
    {
        foreach (PuzzleColorType colorType in ManagedColors)
            PruneDestroyedSources(colorType);
    }

    private void OnDisable()
    {
        ClearAllColors();
    }

    private bool TryGetSources(PuzzleColorType colorType, out HashSet<UnityEngine.Object> sources)
    {
        return activeSources.TryGetValue(colorType, out sources);
    }

    private HashSet<UnityEngine.Object> GetOrCreateSources(PuzzleColorType colorType)
    {
        if (activeSources.TryGetValue(colorType, out HashSet<UnityEngine.Object> sources))
            return sources;

        sources = new HashSet<UnityEngine.Object>();
        activeSources.Add(colorType, sources);
        return sources;
    }

    private void PruneDestroyedSources(PuzzleColorType colorType)
    {
        if (!TryGetSources(colorType, out HashSet<UnityEngine.Object> sources) || sources.Count == 0)
            return;

        bool wasActive = true;
        sources.RemoveWhere(IsSourceUnavailable);
        PublishIfEffectiveStateChanged(colorType, wasActive);
    }

    private void PublishIfEffectiveStateChanged(PuzzleColorType colorType, bool previousState)
    {
        bool nextState = TryGetSources(colorType, out HashSet<UnityEngine.Object> sources) && sources.Count > 0;
        if (previousState != nextState)
            OnColorStateChanged?.Invoke(colorType, nextState);
    }

    private static bool IsManagedColor(PuzzleColorType colorType)
    {
        return colorType == PuzzleColorType.PuzzleRed
            || colorType == PuzzleColorType.PuzzleBlue
            || colorType == PuzzleColorType.PuzzleGreen;
    }

    private static bool IsSourceUnavailable(UnityEngine.Object source)
    {
        if (source == null)
            return true;

        if (source is Behaviour behaviour)
            return !behaviour.isActiveAndEnabled;

        if (source is Component component)
            return !component.gameObject.activeInHierarchy;

        if (source is GameObject gameObject)
            return !gameObject.activeInHierarchy;

        return false;
    }
}
