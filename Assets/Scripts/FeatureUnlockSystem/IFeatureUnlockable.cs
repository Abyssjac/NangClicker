/// <summary>
/// Implemented by authoritative gameplay managers that can be enabled by a permanent progression feature unlock.
/// Presentation roots are controlled separately by <see cref="FeatureUnlockManager"/>.
/// </summary>
public interface IFeatureUnlockable
{
    bool IsUnlocked { get; }
    void SetUnlocked(bool unlocked);
}
