using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonaLisaGame
{
    /// <summary>
    /// Owns all live photo-point timers and circle-range tests. Photo points intentionally have no colliders.
    /// </summary>
    public sealed class PhotoPointManager : MonoBehaviour
    {
        [Serializable]
        public readonly struct DebugSnapshot
        {
            public DebugSnapshot(int activePointCount, int pooledPointCount, int successCount, int failureCount)
            {
                ActivePointCount = activePointCount;
                PooledPointCount = pooledPointCount;
                SuccessCount = successCount;
                FailureCount = failureCount;
            }

            public int ActivePointCount { get; }
            public int PooledPointCount { get; }
            public int SuccessCount { get; }
            public int FailureCount { get; }
        }

        private sealed class ActivePhotoPoint
        {
            public PhotoPointView View;
            public float PatienceRemaining;
            public float FocusElapsed;
            public PhotoPointState State;
            public Action<PhotoPointResult> ResolvedCallback;
        }

        [Header("References")]
        [SerializeField] private GazeFocusController gazeFocus;
        [SerializeField] private PhotoPointView photoPointPrefab;
        [SerializeField] private Transform photoPointRoot;

        [Header("Photo Point Rules")]
        [SerializeField, Min(0.01f)] private float hitRadius = 0.72f;
        [SerializeField, Min(0.01f)] private float patienceDuration = 6f;
        [SerializeField, Min(0.01f)] private float requiredFocusDuration = 1.1f;

        private readonly List<ActivePhotoPoint> activePhotoPoints = new();
        private readonly Queue<PhotoPointView> inactivePhotoPointPool = new();

        public int SuccessCount { get; private set; }
        public int FailureCount { get; private set; }
        public int ActivePointCount => activePhotoPoints.Count;
        public event Action<int, int> CountsChanged;

        private void LateUpdate()
        {
            if (gazeFocus == null)
                return;

            Vector2 focusPosition = gazeFocus.CurrentWorldPosition;
            float hitRadiusSquared = hitRadius * hitRadius;

            for (int i = activePhotoPoints.Count - 1; i >= 0; i--)
            {
                ActivePhotoPoint point = activePhotoPoints[i];
                if (point.View == null)
                {
                    activePhotoPoints.RemoveAt(i);
                    continue;
                }

                Vector2 pointPosition = point.View.transform.position;
                bool isFocused = (focusPosition - pointPosition).sqrMagnitude <= hitRadiusSquared;
                point.State = isFocused ? PhotoPointState.Focused : PhotoPointState.Unfocused;
                point.FocusElapsed = isFocused ? point.FocusElapsed + Time.deltaTime : 0f;

                float focus01 = point.FocusElapsed / requiredFocusDuration;
                point.View.SetState(point.State);
                point.View.SetFocus01(focus01);

                // Success is intentionally checked before patience loss, so it wins a same-frame tie.
                if (point.FocusElapsed >= requiredFocusDuration)
                {
                    ResolvePointAt(i, PhotoPointResult.Succeeded);
                    continue;
                }

                point.PatienceRemaining -= Time.deltaTime;
                point.View.SetPatience01(point.PatienceRemaining / patienceDuration);
                if (point.PatienceRemaining <= 0f)
                    ResolvePointAt(i, PhotoPointResult.Failed);
            }
        }

        /// <summary>Creates an independent point at an exact world-space position.</summary>
        public PhotoPointView SpawnAtPosition(Vector2 position) => SpawnAtPosition(position, null);

        /// <summary>Creates a point and invokes the supplied callback exactly once when it resolves or is cancelled.</summary>
        public PhotoPointView SpawnAtPosition(Vector2 position, Action<PhotoPointResult> resolvedCallback)
        {
            if (photoPointPrefab == null)
            {
                Debug.LogWarning($"[{nameof(PhotoPointManager)}] Photo Point Prefab is not assigned.", this);
                return null;
            }

            PhotoPointView view = TakePhotoPointFromPoolOrCreate();
            if (view == null)
                return null;

            view.transform.SetParent(photoPointRoot != null ? photoPointRoot : transform, true);
            view.transform.position = new Vector3(position.x, position.y, view.transform.position.z);
            view.gameObject.SetActive(true);
            view.Setup(hitRadius, 1f, 0f, PhotoPointState.Unfocused);

            activePhotoPoints.Add(new ActivePhotoPoint
            {
                View = view,
                PatienceRemaining = patienceDuration,
                FocusElapsed = 0f,
                State = PhotoPointState.Unfocused,
                ResolvedCallback = resolvedCallback,
            });
            return view;
        }

        public bool CancelPhotoPoint(PhotoPointView view)
        {
            if (view == null)
                return false;

            for (int i = activePhotoPoints.Count - 1; i >= 0; i--)
            {
                if (activePhotoPoints[i].View != view)
                    continue;

                ResolvePointAt(i, PhotoPointResult.Cancelled);
                return true;
            }

            return false;
        }

        public void SpawnDebugPoints(int count)
        {
            if (gazeFocus == null)
                return;

            int requestedCount = Mathf.Max(0, count);
            GazeArea area = gazeFocus.Area;
            for (int i = 0; i < requestedCount; i++)
                SpawnAtPosition(area.GetRandomPoint(hitRadius));
        }

        /// <summary>Cancels points without changing either counter. Waiting tourists receive a cancellation callback and leave.</summary>
        public void ClearAllPhotoPoints()
        {
            for (int i = activePhotoPoints.Count - 1; i >= 0; i--)
                ResolvePointAt(i, PhotoPointResult.Cancelled);
        }

        public void ResetCounts()
        {
            SuccessCount = 0;
            FailureCount = 0;
            CountsChanged?.Invoke(SuccessCount, FailureCount);
        }

        public DebugSnapshot GetDebugSnapshot() => new(
            activePhotoPoints.Count,
            inactivePhotoPointPool.Count,
            SuccessCount,
            FailureCount);

        public void ConfigurePrototype(
            GazeFocusController focus,
            PhotoPointView prefab,
            Transform root,
            float configuredHitRadius,
            float configuredPatienceDuration,
            float configuredFocusDuration)
        {
            gazeFocus = focus;
            photoPointPrefab = prefab;
            photoPointRoot = root;
            hitRadius = Mathf.Max(0.01f, configuredHitRadius);
            patienceDuration = Mathf.Max(0.01f, configuredPatienceDuration);
            requiredFocusDuration = Mathf.Max(0.01f, configuredFocusDuration);
        }

        private PhotoPointView TakePhotoPointFromPoolOrCreate()
        {
            while (inactivePhotoPointPool.Count > 0)
            {
                PhotoPointView pooled = inactivePhotoPointPool.Dequeue();
                if (pooled != null)
                    return pooled;
            }

            PhotoPointView created = Instantiate(photoPointPrefab, photoPointRoot != null ? photoPointRoot : transform);
            created.name = "PhotoPoint";
            return created;
        }

        private void ResolvePointAt(int index, PhotoPointResult result)
        {
            ActivePhotoPoint point = activePhotoPoints[index];
            activePhotoPoints.RemoveAt(index);

            if (result == PhotoPointResult.Succeeded)
                SuccessCount++;
            else if (result == PhotoPointResult.Failed)
                FailureCount++;

            if (result != PhotoPointResult.Cancelled)
                CountsChanged?.Invoke(SuccessCount, FailureCount);

            point.ResolvedCallback?.Invoke(result);
            ReturnPhotoPointToPool(point.View);
        }

        private void ReturnPhotoPointToPool(PhotoPointView view)
        {
            if (view == null)
                return;

            view.gameObject.SetActive(false);
            inactivePhotoPointPool.Enqueue(view);
        }
    }
}
