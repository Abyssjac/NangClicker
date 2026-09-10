using System;
using System.Collections.Generic;
using UnityEngine;
using JackyUtility;

[DefaultExecutionOrder(-1000)]
public class AllCameraManager : MonoBehaviour
{
    public static AllCameraManager Instance { get; private set; }

    [SerializeField] private CameraMode defaultCameraMode = CameraMode.Empty;
    private List<CameraBase> currentCameras = new List<CameraBase>();
    private CameraMode currentCameraMode;
    private CameraMode cameraModeBeforeOverride = CameraMode.Empty;
    private bool hasCameraOverride;

    private List<CameraBase> allRegisteredCameras = new List<CameraBase>();

    public Action<CameraMode> OnCameraModeSwitched;
    public CameraMode CurrentCameraMode => currentCameraMode;
    public bool HasCameraOverride => hasCameraOverride;

    private void RegisterDebugCommands()
    {
        if (DebugConsoleManager.Instance == null) return;

        // Usage: cam <mode>   e.g. "cam FollowTarget"
        DebugConsoleManager.Instance.RegisterCommand(new DebugCommand(
            "cam",
            "Switch camera mode. Usage: cam <CameraMode>. Requests are blocked while a camera override is active.",
            args =>
            {
                if (args.Length == 0)
                {
                    Debug.LogWarning("cam command requires a mode argument. Current mode: " + currentCameraMode);
                    return;
                }

                if (Enum.TryParse(args[0], true, out CameraMode mode))
                {
                    if (SwitchCameraMode(mode))
                        Debug.Log($"Camera switched to {mode}");
                }
                else
                {
                    Debug.LogWarning($"Unknown CameraMode: {args[0]}. Available: {string.Join(", ", Enum.GetNames(typeof(CameraMode)))}");
                }
            }
        ));
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (defaultCameraMode == CameraMode.Empty) { Debug.LogError("Remember To Set the DefaultCameraMode, Current is Empty"); }
    }

    private void Start()
    {
        RegisterDebugCommands();
    }

    public void RegisterCamera(CameraBase tarCameraBase)
    {
        if (tarCameraBase == null) { Debug.LogError($"[{nameof(AllCameraManager)}] Trying to register a NULL camera.", this); return; }
        if (allRegisteredCameras.Contains(tarCameraBase)) { Debug.LogError("Trying to Register a CameraBase that has already been registered, Please Check"); return; }
        allRegisteredCameras.Add(tarCameraBase);

        if (currentCameraMode == CameraMode.Empty && tarCameraBase.CameraMode == defaultCameraMode)
        {
            SwitchCameraMode(defaultCameraMode);
        }
        else if (currentCameraMode != CameraMode.Empty)
        {
            ApplyCameraMode(currentCameraMode, null, false);
        }

        Debug.Log($"Camera '{tarCameraBase.name}' registered to AllCameraManager. Total registered cameras: {allRegisteredCameras.Count}");
    }

    public void UnRegisterCamera(CameraBase tarCameraBase)
    {
        if (tarCameraBase == null) { Debug.LogError("Trying to UnRegister a Null CameraBase, Please Check"); return; }
        if (!allRegisteredCameras.Contains(tarCameraBase)) { Debug.LogError("Trying to UnRegister a CameraBase that has not been registered, Please Check"); return; }
        allRegisteredCameras.Remove(tarCameraBase);

        tarCameraBase.DeactivateCamera();
        currentCameras.Remove(tarCameraBase);

        if (currentCameraMode != CameraMode.Empty && FindCamerasByMode(currentCameraMode).Count > 0)
        {
            ApplyCameraMode(currentCameraMode, null, false);
        }
        else if (defaultCameraMode != CameraMode.Empty && FindCamerasByMode(defaultCameraMode).Count > 0)
        {
            ApplyCameraMode(defaultCameraMode, null, false);
        }
        else
        {
            currentCameras.Clear();
            currentCameraMode = CameraMode.Empty;
        }

        Debug.Log($"Camera '{tarCameraBase.name}' unregistered from AllCameraManager. Total registered cameras: {allRegisteredCameras.Count}");
    }

    public bool SwitchCameraMode(CameraMode tarMode, params CameraActivationPolicy[] excludedPolicies)
    {
        if (hasCameraOverride)
        {
            Debug.LogWarning(
                $"[{nameof(AllCameraManager)}] Cannot switch to '{tarMode}' while a camera override is active. Current override mode: {currentCameraMode}.",
                this);
            return false;
        }

        return ApplyCameraMode(tarMode, excludedPolicies);
    }

    private bool ApplyCameraMode(
        CameraMode tarMode,
        CameraActivationPolicy[] excludedPolicies,
        bool notifyModeSwitched = true)
    {
        CleanupNullCameras();

        if (tarMode == CameraMode.Empty)
        {
            Debug.LogError($"[{nameof(AllCameraManager)}] Cannot switch to CameraMode.Empty.", this);
            return false;
        }
        List<CameraBase> targetCameras = FindCamerasByMode(tarMode);
        if (targetCameras == null || targetCameras.Count == 0)
        {
            Debug.LogError($"[{nameof(AllCameraManager)}] No registered camera found for mode '{tarMode}'.", this);
            return false;
        }

        List<CameraBase> newlyActiveCameras = new List<CameraBase>();
        for (int i = 0; i < allRegisteredCameras.Count; i++)
        {
            CameraBase curCam = allRegisteredCameras[i];
            if (curCam == null)
                continue;

            bool isTargetModeCamera = targetCameras.Contains(curCam);
            bool shouldRemainActive = !isTargetModeCamera && ShouldRemainActive(curCam, excludedPolicies);

            if (isTargetModeCamera || shouldRemainActive)
            {
                curCam.ActivateCamera();
                newlyActiveCameras.Add(curCam);
            }
            else
            {
                curCam.DeactivateCamera();
            }
        }

        currentCameras = newlyActiveCameras;
        currentCameraMode = tarMode;

        if (notifyModeSwitched)
            OnCameraModeSwitched?.Invoke(currentCameraMode);

        return true;
    }

    private static bool ShouldRemainActive(CameraBase camera, CameraActivationPolicy[] excludedPolicies)
    {
        if (camera.ActivationPolicy == CameraActivationPolicy.ModeBound)
            return false;

        return !IsPolicyExcluded(camera.ActivationPolicy, excludedPolicies);
    }

    private static bool IsPolicyExcluded(CameraActivationPolicy policy, CameraActivationPolicy[] excludedPolicies)
    {
        if (excludedPolicies == null)
            return false;

        for (int i = 0; i < excludedPolicies.Length; i++)
        {
            if (excludedPolicies[i] == policy)
                return true;
        }

        return false;
    }

    public bool BeginCameraOverride(CameraMode overrideMode)
    {
        if (hasCameraOverride)
        {
            Debug.LogWarning($"[{nameof(AllCameraManager)}] Camera override is already active. Current mode: {currentCameraMode}.", this);
            return false;
        }

        if (overrideMode == CameraMode.Empty)
        {
            Debug.LogError($"[{nameof(AllCameraManager)}] Cannot begin camera override with CameraMode.Empty.", this);
            return false;
        }

        CameraMode previousMode = currentCameraMode != CameraMode.Empty ? currentCameraMode : defaultCameraMode;
        CameraMode modeToRestore = previousMode != overrideMode ? previousMode : defaultCameraMode;

        cameraModeBeforeOverride = modeToRestore;
        hasCameraOverride = true;

        if (!ApplyCameraMode(
                overrideMode,
                new[] { CameraActivationPolicy.Supplemental }))
        {
            hasCameraOverride = false;
            cameraModeBeforeOverride = CameraMode.Empty;
            return false;
        }

        return true;
    }

    public bool EndCameraOverride()
    {
        if (!hasCameraOverride)
        {
            Debug.LogWarning($"[{nameof(AllCameraManager)}] Tried to end camera override, but no override is active.", this);
            return false;
        }

        CameraMode restoreMode = cameraModeBeforeOverride != CameraMode.Empty ? cameraModeBeforeOverride : defaultCameraMode;
        hasCameraOverride = false;
        cameraModeBeforeOverride = CameraMode.Empty;

        return ApplyCameraMode(restoreMode, null);
    }

    public List<CameraBase> FindCamerasActivated()
    {
        return new List<CameraBase>(currentCameras);
    }

    public List<CameraBase> FindCamerasByMode(CameraMode mode)
    {
        List<CameraBase> camerasWithMode = new List<CameraBase>();
        for (int i = 0; i < allRegisteredCameras.Count; i++)
        {
            CameraBase cam = allRegisteredCameras[i];
            if (cam == null)
                continue;
            if (cam.CameraMode == mode)
                camerasWithMode.Add(cam);
        }

        return camerasWithMode;
    }

    private void DeactivateAllRegisteredCameras()
    {
        for (int i = 0; i < allRegisteredCameras.Count; i++)
        {
            CameraBase cam = allRegisteredCameras[i];
            if (cam == null)
                continue;

            cam.DeactivateCamera();
        }
    }

    private void CleanupNullCameras()
    {
        for (int i = allRegisteredCameras.Count - 1; i >= 0; i--)
        {
            if (allRegisteredCameras[i] == null)
                allRegisteredCameras.RemoveAt(i);
        }
    }
}

public enum CameraMode
{ 
    Empty = 0,
    BaseTest = 1,
    FollowTarget = 2,
    FreePerspective = 3,
    FirstPerson = 4,
    ThirdPerson = 5,
    Cutscene = 6,
    Game_PlayerView = 20,
    Game_VoyageView = 21,
}
