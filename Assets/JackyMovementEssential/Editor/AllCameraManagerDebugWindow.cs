using System;
using System.Collections.Generic;
using System.Reflection;
using JackyUtility;
using UnityEditor;
using UnityEngine;

public class AllCameraManagerDebugWindow : DebugEditorWindow<AllCameraManager>
{
    private const BindingFlags InstancePrivateFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly FieldInfo AllRegisteredCamerasField =
        typeof(AllCameraManager).GetField("allRegisteredCameras", InstancePrivateFlags);

    private static readonly FieldInfo CurrentCamerasField =
        typeof(AllCameraManager).GetField("currentCameras", InstancePrivateFlags);

    private static readonly FieldInfo CurrentCameraModeField =
        typeof(AllCameraManager).GetField("currentCameraMode", InstancePrivateFlags);

    private static readonly FieldInfo DefaultCameraModeField =
        typeof(AllCameraManager).GetField("defaultCameraMode", InstancePrivateFlags);

    private static readonly FieldInfo CameraModeBeforeOverrideField =
        typeof(AllCameraManager).GetField("cameraModeBeforeOverride", InstancePrivateFlags);

    private CameraMode _selectedMode = CameraMode.FollowTarget;

    [MenuItem("Jacky Tools/All Camera Manager")]
    public static void ShowWindow() =>
        GetWindow<AllCameraManagerDebugWindow>("All Camera Manager").Show();

    protected override void DrawContent()
    {
        AllCameraManager manager = AllCameraManager.Instance != null ? AllCameraManager.Instance : Target;
        if (manager == null)
        {
            EditorGUILayout.HelpBox("No AllCameraManager instance found.", MessageType.Warning);
            return;
        }

        List<CameraBase> registeredCameras = GetCameraList(AllRegisteredCamerasField, manager);
        List<CameraBase> currentCameras = GetCameraList(CurrentCamerasField, manager);
        List<CameraBase> publicActiveCameras = manager.FindCamerasActivated();

        DrawManagerOverview(manager, registeredCameras, currentCameras, publicActiveCameras);
        DrawSwitchControls(manager, registeredCameras);
        DrawCameraList("Registered Cameras", registeredCameras, currentCameras, true, manager);
        DrawUnregisteredSceneCameras(registeredCameras, currentCameras, manager);
    }

    private void DrawManagerOverview(
        AllCameraManager manager,
        List<CameraBase> registeredCameras,
        List<CameraBase> currentCameras,
        List<CameraBase> publicActiveCameras)
    {
        Header("Manager");

        EditorGUILayout.ObjectField("Instance", manager, typeof(AllCameraManager), true);
        Row("Default Mode", GetPrivateValue(DefaultCameraModeField, manager, CameraMode.Empty).ToString());
        Row("Current Mode", GetPrivateValue(CurrentCameraModeField, manager, CameraMode.Empty).ToString());
        Row("Has Camera Override", manager.HasCameraOverride ? "Yes" : "No");
        Row("Mode Before Override", GetPrivateValue(CameraModeBeforeOverrideField, manager, CameraMode.Empty).ToString());
        Row("Registered Count", registeredCameras.Count.ToString());
        Row("Current Count", currentCameras.Count.ToString());
        Row("FindCamerasActivated Count", publicActiveCameras.Count.ToString());

        using (new EditorGUI.DisabledScope(!manager.HasCameraOverride))
        {
            if (GUILayout.Button("End Camera Override"))
            {
                manager.EndCameraOverride();
            }
        }

        if (registeredCameras.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No camera is registered. CameraBase.Awake registers through AllCameraManager.Instance, so this usually means no camera has awakened successfully or the manager was missing when cameras awakened.",
                MessageType.Warning);
        }
    }

    private void DrawSwitchControls(AllCameraManager manager, List<CameraBase> registeredCameras)
    {
        Header("Switch Mode");

        EditorGUILayout.HelpBox(
            "Switch buttons call AllCameraManager.SwitchCameraMode(mode). Buttons are disabled when no registered camera uses that mode or a camera override is active.",
            MessageType.None);

        if (manager.HasCameraOverride)
        {
            EditorGUILayout.HelpBox(
                "A camera override is active. Manual camera switching is blocked until the override ends, then the recorded mode is restored.",
                MessageType.Info);
        }

        EditorGUILayout.BeginHorizontal();
        _selectedMode = (CameraMode)EditorGUILayout.EnumPopup("Selected Mode", _selectedMode);

        int selectedCount = CountMode(registeredCameras, _selectedMode);
        using (new EditorGUI.DisabledScope(manager.HasCameraOverride || _selectedMode == CameraMode.Empty || selectedCount == 0))
        {
            if (GUILayout.Button("Switch", GUILayout.Width(80)))
                manager.SwitchCameraMode(_selectedMode);
        }
        EditorGUILayout.EndHorizontal();

        if (_selectedMode != CameraMode.Empty && selectedCount == 0)
        {
            EditorGUILayout.HelpBox(
                $"No registered camera currently has mode {_selectedMode}. Calling SwitchCameraMode now would leave the manager with no active cameras.",
                MessageType.Warning);
        }

        foreach (CameraMode mode in Enum.GetValues(typeof(CameraMode)))
        {
            if (mode == CameraMode.Empty)
                continue;

            int registeredCount = CountMode(registeredCameras, mode);
            int activeCount = CountActiveMode(registeredCameras, mode);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"{mode}",
                $"registered: {registeredCount}, Camera.enabled: {activeCount}");

            using (new EditorGUI.DisabledScope(manager.HasCameraOverride || registeredCount == 0))
            {
                if (GUILayout.Button("Switch", GUILayout.Width(80)))
                    manager.SwitchCameraMode(mode);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawCameraList(
        string title,
        List<CameraBase> cameras,
        List<CameraBase> currentCameras,
        bool showEmptyWarning,
        AllCameraManager manager)
    {
        Header(title);

        if (cameras.Count == 0)
        {
            if (showEmptyWarning)
                EditorGUILayout.HelpBox("No cameras in this list.", MessageType.Info);
            return;
        }

        for (int i = 0; i < cameras.Count; i++)
        {
            CameraBase cameraBase = cameras[i];
            if (cameraBase == null)
            {
                EditorGUILayout.LabelField($"{i}: <null>");
                continue;
            }

            DrawCameraRow(i, cameraBase, currentCameras.Contains(cameraBase), manager);
        }
    }

    private void DrawCameraRow(int index, CameraBase cameraBase, bool managerActive, AllCameraManager manager)
    {
        Camera unityCamera = cameraBase.CachedCamera != null
            ? cameraBase.CachedCamera
            : cameraBase.GetComponent<Camera>();

        bool cameraEnabled = unityCamera != null && unityCamera.enabled;
        bool behaviourEnabled = cameraBase.enabled;
        bool hierarchyActive = cameraBase.gameObject.activeInHierarchy;
        Transform followTarget = GetCameraTarget(cameraBase);

        Color previousContent = GUI.contentColor;
        if (managerActive)
            GUI.contentColor = Color.green;
        else if (cameraEnabled)
            GUI.contentColor = Color.yellow;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(
            $"{index}: {cameraBase.name}",
            managerActive ? EditorStyles.boldLabel : EditorStyles.label);

        GUI.contentColor = previousContent;

        if (GUILayout.Button("Select", GUILayout.Width(56)))
        {
            Selection.activeGameObject = cameraBase.gameObject;
            EditorGUIUtility.PingObject(cameraBase.gameObject);
        }

        using (new EditorGUI.DisabledScope(manager.HasCameraOverride || cameraBase.CameraMode == CameraMode.Empty))
        {
            if (GUILayout.Button("Switch", GUILayout.Width(56)))
                manager.SwitchCameraMode(cameraBase.CameraMode);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Mode", cameraBase.CameraMode.ToString());
        EditorGUILayout.LabelField("Manager Active", managerActive ? "Yes" : "No");
        EditorGUILayout.LabelField("Camera.enabled", cameraEnabled ? "Yes" : "No");
        EditorGUILayout.LabelField("CameraBase.enabled", behaviourEnabled ? "Yes" : "No");
        EditorGUILayout.LabelField("GameObject Active", hierarchyActive ? "Yes" : "No");
        EditorGUILayout.ObjectField("Target", followTarget, typeof(Transform), true);
        EditorGUILayout.ObjectField("Camera Component", unityCamera, typeof(Camera), true);

        EditorGUILayout.EndVertical();
    }

    private void DrawUnregisteredSceneCameras(
        List<CameraBase> registeredCameras,
        List<CameraBase> currentCameras,
        AllCameraManager manager)
    {
        CameraBase[] sceneCameras = FindObjectsByType<CameraBase>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        List<CameraBase> unregisteredCameras = new List<CameraBase>();
        foreach (CameraBase cameraBase in sceneCameras)
        {
            if (cameraBase != null && !registeredCameras.Contains(cameraBase))
                unregisteredCameras.Add(cameraBase);
        }

        DrawCameraList("Scene Cameras Not Registered", unregisteredCameras, currentCameras, false, manager);

        if (unregisteredCameras.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "These CameraBase objects exist in the scene but are not inside AllCameraManager's registered list.",
                MessageType.Warning);
        }
    }

    private static List<CameraBase> GetCameraList(FieldInfo field, AllCameraManager manager)
    {
        if (field == null || manager == null)
            return new List<CameraBase>();

        return field.GetValue(manager) is List<CameraBase> cameras
            ? new List<CameraBase>(cameras)
            : new List<CameraBase>();
    }

    private static T GetPrivateValue<T>(FieldInfo field, AllCameraManager manager, T fallback)
    {
        if (field == null || manager == null)
            return fallback;

        object value = field.GetValue(manager);
        return value is T typedValue ? typedValue : fallback;
    }

    private static int CountMode(List<CameraBase> cameras, CameraMode mode)
    {
        int count = 0;
        for (int i = 0; i < cameras.Count; i++)
        {
            CameraBase cameraBase = cameras[i];
            if (cameraBase != null && cameraBase.CameraMode == mode)
                count++;
        }

        return count;
    }

    private static int CountActiveMode(List<CameraBase> cameras, CameraMode mode)
    {
        int count = 0;
        for (int i = 0; i < cameras.Count; i++)
        {
            CameraBase cameraBase = cameras[i];
            if (cameraBase == null || cameraBase.CameraMode != mode)
                continue;

            Camera unityCamera = cameraBase.CachedCamera != null
                ? cameraBase.CachedCamera
                : cameraBase.GetComponent<Camera>();

            if (unityCamera != null && unityCamera.enabled)
                count++;
        }

        return count;
    }

    private static Transform GetCameraTarget(CameraBase cameraBase)
    {
        return cameraBase != null ? cameraBase.Target : null;
    }
}
