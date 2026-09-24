using UnityEditor;
using UnityEngine;

/// <summary>Editor-only Scene view previews for the puzzle target prefabs.</summary>
public static class PuzzleSceneGizmoDrawer
{
    private static readonly Color DoorGhostColor = new(0.16f, 0.85f, 1f, 0.25f);
    private static readonly Color TwoPoseColor = new(1f, 0.77f, 0.12f, 0.9f);
    private static readonly Color RailColor = new(0.42f, 0.96f, 0.55f, 0.95f);

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Active)]
    private static void DrawDoorPreview(PuzzleDoor door, GizmoType gizmoType)
    {
        if (!door.HasValidPoseConfiguration)
            return;

        Transform movingRoot = door.MovingRoot;
        Transform targetPose = door.TargetPose;
        if (movingRoot == null || targetPose == null)
            return;

        Matrix4x4 targetRootMatrix = Matrix4x4.TRS(
            targetPose.position,
            door.PreviewTargetRotation,
            movingRoot.lossyScale);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.color = DoorGhostColor;

        foreach (MeshFilter meshFilter in movingRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null)
                continue;

            Matrix4x4 meshRelativeToRoot = movingRoot.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
            Gizmos.matrix = targetRootMatrix * meshRelativeToRoot;
            Gizmos.DrawMesh(meshFilter.sharedMesh);
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    private static void DrawTwoPoseGuide(PuzzleMovingPlatform platform, GizmoType gizmoType)
    {
        if (!platform.HasValidPoseConfiguration)
            return;

        Transform movingRoot = platform.MovingRoot;
        Transform targetPose = platform.TargetPose;
        if (movingRoot == null || targetPose == null)
            return;

        Handles.color = TwoPoseColor;
        Handles.DrawDottedLine(movingRoot.position, targetPose.position, 4f);
        DrawPoseAxes(targetPose, HandleUtility.GetHandleSize(targetPose.position) * 0.45f);
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Active)]
    private static void DrawRailGuide(PingPongRailMovingPlatform platform, GizmoType gizmoType)
    {
        if (!platform.HasValidRailConfiguration)
            return;

        Transform startPose = platform.RailStartPose;
        Transform endPose = platform.RailEndPose;
        if (startPose == null || endPose == null)
            return;

        Handles.color = RailColor;
        Handles.DrawAAPolyLine(3f, startPose.position, endPose.position);

        float startSize = HandleUtility.GetHandleSize(startPose.position) * 0.12f;
        float endSize = HandleUtility.GetHandleSize(endPose.position) * 0.12f;
        Handles.SphereHandleCap(0, startPose.position, Quaternion.identity, startSize, EventType.Repaint);
        Handles.CubeHandleCap(0, endPose.position, Quaternion.identity, endSize, EventType.Repaint);

        Vector3 initialPosition = Vector3.Lerp(startPose.position, endPose.position, platform.PreviewProgress);
        Vector3 direction = (endPose.position - startPose.position).normalized * platform.PreviewDirection;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = platform.MovingRoot.forward * platform.PreviewDirection;

        Handles.ArrowHandleCap(
            0,
            initialPosition,
            Quaternion.LookRotation(direction, Vector3.up),
            HandleUtility.GetHandleSize(initialPosition) * 0.55f,
            EventType.Repaint);
    }

    private static void DrawPoseAxes(Transform pose, float size)
    {
        Handles.ArrowHandleCap(0, pose.position, pose.rotation * Quaternion.LookRotation(Vector3.right), size, EventType.Repaint);
        Handles.ArrowHandleCap(0, pose.position, pose.rotation * Quaternion.LookRotation(Vector3.up), size, EventType.Repaint);
        Handles.ArrowHandleCap(0, pose.position, pose.rotation, size, EventType.Repaint);
    }
}
