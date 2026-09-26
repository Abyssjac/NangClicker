using System.Collections.Generic;
using UnityEngine;

namespace NangClicker.HexWave
{
    [DisallowMultipleComponent]
    public sealed class HexEnemyVisualManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexEnemyManager enemyManager;
        [SerializeField] private HexWaveManager waveManager;
        [SerializeField] private Camera billboardCamera;
        [SerializeField] private HexEnemyStackView stackViewPrefab;

        [Header("Stack Appearance")]
        [SerializeField] private Color enemyColor = new Color(0.92f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color textColor = Color.white;
        [Min(0f)] [SerializeField] private float columnTopLocalY = 1.5f;
        [Min(0.01f)] [SerializeField] private float sphereDiameter = 0.42f;
        [Min(0f)] [SerializeField] private float sphereGap = 0.04f;
        [Min(0f)] [SerializeField] private float textGap = 0.2f;

        private readonly Dictionary<int, HexEnemyStackView> viewsByStackId =
            new Dictionary<int, HexEnemyStackView>();
        private readonly HashSet<int> activeStackIds = new HashSet<int>();
        private readonly List<int> removedStackIds = new List<int>();

        private void Reset()
        {
            enemyManager = GetComponent<HexEnemyManager>();
            waveManager = GetComponent<HexWaveManager>();
            billboardCamera = Camera.main;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (enemyManager != null)
            {
                enemyManager.StateChanged += Refresh;
                Refresh(enemyManager.CurrentSnapshot);
            }
        }

        private void OnDisable()
        {
            if (enemyManager != null)
                enemyManager.StateChanged -= Refresh;
        }

        private void OnValidate()
        {
            columnTopLocalY = Mathf.Max(0f, columnTopLocalY);
            sphereDiameter = Mathf.Max(0.01f, sphereDiameter);
            sphereGap = Mathf.Max(0f, sphereGap);
            textGap = Mathf.Max(0f, textGap);
        }

        private void LateUpdate()
        {
            if (billboardCamera == null)
                billboardCamera = Camera.main;

            foreach (HexEnemyStackView view in viewsByStackId.Values)
            {
                if (view != null)
                    view.FaceCamera(billboardCamera);
            }
        }

        public void Configure(
            HexEnemyManager targetEnemyManager,
            HexWaveManager targetWaveManager,
            Camera targetCamera,
            HexEnemyStackView targetPrefab = null)
        {
            if (enemyManager != null)
                enemyManager.StateChanged -= Refresh;

            enemyManager = targetEnemyManager;
            waveManager = targetWaveManager;
            billboardCamera = targetCamera;
            stackViewPrefab = targetPrefab;

            if (isActiveAndEnabled && enemyManager != null)
            {
                enemyManager.StateChanged += Refresh;
                Refresh(enemyManager.CurrentSnapshot);
            }
        }

        public void Refresh(IReadOnlyList<HexEnemyStackSnapshot> snapshot)
        {
            if (waveManager == null)
                return;

            activeStackIds.Clear();
            for (int i = 0; i < snapshot.Count; i++)
            {
                HexEnemyStackSnapshot stack = snapshot[i];
                if (!waveManager.TryGetCell(stack.Coordinate, out HexCellView cell) || cell == null)
                    continue;

                activeStackIds.Add(stack.Id);
                if (!viewsByStackId.TryGetValue(stack.Id, out HexEnemyStackView view) || view == null)
                {
                    view = CreateView(stack.Id, cell.transform);
                    viewsByStackId[stack.Id] = view;
                }
                else if (view.transform.parent != cell.transform)
                {
                    view.transform.SetParent(cell.transform, false);
                }

                view.transform.localPosition = Vector3.zero;
                view.transform.localRotation = Quaternion.identity;
                view.Refresh(stack.Count, stack.TotalHealth);
            }

            removedStackIds.Clear();
            foreach (KeyValuePair<int, HexEnemyStackView> pair in viewsByStackId)
            {
                if (!activeStackIds.Contains(pair.Key))
                    removedStackIds.Add(pair.Key);
            }

            for (int i = 0; i < removedStackIds.Count; i++)
            {
                int stackId = removedStackIds[i];
                HexEnemyStackView view = viewsByStackId[stackId];
                viewsByStackId.Remove(stackId);
                if (view != null)
                    Destroy(view.gameObject);
            }
        }

        private void ResolveReferences()
        {
            if (enemyManager == null)
                enemyManager = GetComponent<HexEnemyManager>();
            if (waveManager == null)
                waveManager = GetComponent<HexWaveManager>();
            if (billboardCamera == null)
                billboardCamera = Camera.main;
        }

        private HexEnemyStackView CreateView(int stackId, Transform parent)
        {
            HexEnemyStackView view;
            if (stackViewPrefab != null)
            {
                view = Instantiate(stackViewPrefab, parent);
            }
            else
            {
                GameObject viewObject = new GameObject($"Enemy Stack {stackId}");
                viewObject.transform.SetParent(parent, false);
                view = viewObject.AddComponent<HexEnemyStackView>();
            }

            view.name = $"Enemy Stack {stackId}";
            view.Initialize(
                stackId,
                enemyColor,
                textColor,
                columnTopLocalY,
                sphereDiameter,
                sphereGap,
                textGap);
            return view;
        }
    }
}
