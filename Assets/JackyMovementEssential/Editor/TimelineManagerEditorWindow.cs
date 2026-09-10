#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

public class TimelineManagerEditorWindow : EditorWindow
{
    private PlayableAsset selectedTimeline;

    [MenuItem("Jacky Tools/Timeline Manager")]
    public static void ShowWindow()
    {
        GetWindow<TimelineManagerEditorWindow>("Timeline Manager").Show();
    }

    private void OnGUI()
    {
        TimelineManager manager = TimelineManager.Instance;

        EditorGUILayout.LabelField("Timeline Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Instance", manager, typeof(TimelineManager), true);
            EditorGUILayout.Toggle("Is Playing", manager != null && manager.IsPlayingTimeline);
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to play timelines through TimelineManager.", MessageType.Info);
        }

        if (manager == null)
        {
            EditorGUILayout.HelpBox("No TimelineManager instance found in the active scene.", MessageType.Warning);
        }

        EditorGUILayout.Space(8f);
        selectedTimeline = (PlayableAsset)EditorGUILayout.ObjectField(
            "Playable Asset",
            selectedTimeline,
            typeof(PlayableAsset),
            false);

        using (new EditorGUI.DisabledScope(!Application.isPlaying || manager == null || selectedTimeline == null))
        {
            if (GUILayout.Button("Play Selected Timeline"))
            {
                manager.PlayTimeline(selectedTimeline);
            }
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying || manager == null || !manager.IsPlayingTimeline))
        {
            if (GUILayout.Button("Stop Timeline"))
            {
                manager.StopTimeline();
            }
        }

        EditorGUILayout.Space(8f);
        DrawCameraManagerStatus();
    }

    private static void DrawCameraManagerStatus()
    {
        AllCameraManager cameraManager = AllCameraManager.Instance;

        EditorGUILayout.LabelField("Camera Manager", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Instance", cameraManager, typeof(AllCameraManager), true);
            EditorGUILayout.EnumPopup("Current Mode", cameraManager != null ? cameraManager.CurrentCameraMode : CameraMode.Empty);
            EditorGUILayout.Toggle("Has Override", cameraManager != null && cameraManager.HasCameraOverride);
        }

        if (cameraManager == null)
        {
            EditorGUILayout.HelpBox("No AllCameraManager instance found in the active scene.", MessageType.Warning);
        }
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }
}
#endif
