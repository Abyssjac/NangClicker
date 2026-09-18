using System.Collections.Generic;
using UnityEngine;

namespace MonaLisaGame
{
    /// <summary>Spawns tourists from alternating sides, then reuses them after they leave.</summary>
    public sealed class TouristManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TouristBehaviour touristPrefab;
        [SerializeField] private Transform touristRoot;
        [SerializeField] private GazeFocusController gazeFocus;
        [SerializeField] private PhotoPointManager photoPointManager;
        [SerializeField] private Transform leftOffscreenPoint;
        [SerializeField] private Transform rightOffscreenPoint;

        [Header("Spawn Rules")]
        [SerializeField, Min(1)] private int maxActiveTourists = 4;
        [SerializeField, Min(0f)] private float moveSpeed = 2.1f;
        [SerializeField] private Vector2 spawnIntervalRange = new(1.4f, 2.4f);
        [SerializeField, Min(0f)] private float initialSpawnDelay = 0.6f;
        [SerializeField, Min(0f)] private float waitAreaPadding = 0.65f;

        private readonly List<TouristBehaviour> activeTourists = new();
        private readonly Queue<TouristBehaviour> inactiveTouristPool = new();
        private float spawnTimer;

        private void Start()
        {
            spawnTimer = Mathf.Max(0f, initialSpawnDelay);
        }

        private void Update()
        {
            RemoveDestroyedReferences();
            if (activeTourists.Count >= maxActiveTourists || !HasRouteConfiguration())
                return;

            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f)
                return;

            SpawnTourist();
            spawnTimer = UnityEngine.Random.Range(
                Mathf.Min(spawnIntervalRange.x, spawnIntervalRange.y),
                Mathf.Max(spawnIntervalRange.x, spawnIntervalRange.y));
        }

        public void ConfigurePrototype(
            TouristBehaviour prefab,
            Transform root,
            GazeFocusController focus,
            PhotoPointManager pointManager,
            Transform leftPoint,
            Transform rightPoint)
        {
            touristPrefab = prefab;
            touristRoot = root;
            gazeFocus = focus;
            photoPointManager = pointManager;
            leftOffscreenPoint = leftPoint;
            rightOffscreenPoint = rightPoint;
        }

        private void SpawnTourist()
        {
            if (!TryBuildRoute(out Vector3 spawnPosition, out Vector3 waitPosition, out Vector3 exitPosition))
                return;

            TouristBehaviour tourist = TakeTouristFromPoolOrCreate();
            if (tourist == null)
                return;

            tourist.gameObject.SetActive(true);
            activeTourists.Add(tourist);
            tourist.BeginRoute(
                spawnPosition,
                waitPosition,
                exitPosition,
                moveSpeed,
                photoPointManager,
                ReturnTouristToPool);
        }

        private bool TryBuildRoute(out Vector3 spawnPosition, out Vector3 waitPosition, out Vector3 exitPosition)
        {
            spawnPosition = default;
            waitPosition = default;
            exitPosition = default;
            if (!HasRouteConfiguration())
                return false;

            GazeArea area = gazeFocus.Area;
            bool entersFromLeft = UnityEngine.Random.value < 0.5f;
            Transform entry = entersFromLeft ? leftOffscreenPoint : rightOffscreenPoint;
            Transform exit = entersFromLeft ? rightOffscreenPoint : leftOffscreenPoint;

            Vector2 waitPoint = area.GetRandomPoint(waitAreaPadding);
            float y = waitPoint.y;
            float x = waitPoint.x;

            spawnPosition = new Vector3(entry.position.x, y, entry.position.z);
            waitPosition = new Vector3(x, y, entry.position.z);
            exitPosition = new Vector3(exit.position.x, y, exit.position.z);
            return true;
        }

        private TouristBehaviour TakeTouristFromPoolOrCreate()
        {
            while (inactiveTouristPool.Count > 0)
            {
                TouristBehaviour pooled = inactiveTouristPool.Dequeue();
                if (pooled != null)
                    return pooled;
            }

            if (touristPrefab == null)
            {
                Debug.LogWarning($"[{nameof(TouristManager)}] Tourist Prefab is not assigned.", this);
                return null;
            }

            TouristBehaviour created = Instantiate(touristPrefab, touristRoot != null ? touristRoot : transform);
            created.name = "Tourist";
            return created;
        }

        private void ReturnTouristToPool(TouristBehaviour tourist)
        {
            if (tourist == null)
                return;

            activeTourists.Remove(tourist);
            tourist.gameObject.SetActive(false);
            inactiveTouristPool.Enqueue(tourist);
        }

        private bool HasRouteConfiguration() =>
            touristPrefab != null
            && gazeFocus != null
            && photoPointManager != null
            && leftOffscreenPoint != null
            && rightOffscreenPoint != null;

        private void RemoveDestroyedReferences()
        {
            for (int i = activeTourists.Count - 1; i >= 0; i--)
            {
                if (activeTourists[i] == null)
                    activeTourists.RemoveAt(i);
            }
        }
    }
}
