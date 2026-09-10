/// <summary>
/// Determines how a camera participates when <see cref="AllCameraManager"/> switches modes.
/// </summary>
public enum CameraActivationPolicy
{
    /// <summary>
    /// The camera is enabled only when its <see cref="CameraBase.CameraMode"/> is selected.
    /// </summary>
    ModeBound = 0,

    /// <summary>
    /// The camera remains enabled during ordinary mode switches unless the caller excludes this policy.
    /// </summary>
    Supplemental = 1,
}
