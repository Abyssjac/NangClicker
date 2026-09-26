using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NangClicker.HexWave
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class HexEnemyVisualManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexEnemyManager enemyManager;
        [SerializeField] private HexWaveManager waveManager;
        [SerializeField] private Camera billboardCamera;
        [SerializeField] private HexEnemyStackView stackViewPrefab;
        [SerializeField] private Material movePreviewMaterial;

        [Header("Stack Appearance")]
        [SerializeField] private Color enemyColor = new Color(0.92f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color textColor = Color.white;
        [Min(0f)] [SerializeField] private float columnTopLocalY = 1.5f;
        [Min(0.01f)] [SerializeField] private float sphereDiameter = 0.42f;
        [Min(0f)] [SerializeField] private float sphereGap = 0.04f;
        [Min(0f)] [SerializeField] private float textGap = 0.2f;

        [Header("Move Preview")]
        [SerializeField] private Color movePreviewColor = new Color(1f, 0.08f, 0.04f, 1f);
        [Range(0f, 1f)] [SerializeField] private float previewMinOpacity = 0.14f;
        [Range(0f, 1f)] [SerializeField] private float previewMaxOpacity = 0.42f;
        [Min(0f)] [SerializeField] private float previewVerticalOffset = 0.08f;
        [Min(0.01f)] [SerializeField] private float previewSphereScale = 0.92f;
        [Min(0.01f)] [SerializeField] private float arrowWidth = 0.18f;
        [Min(0f)] [SerializeField] private float arrowEndPadding = 0.28f;
        [Min(0f)] [SerializeField] private float arrowOpacityMultiplier = 1.45f;

        private readonly Dictionary<int, HexEnemyStackView> viewsByStackId =
            new Dictionary<int, HexEnemyStackView>();
        private readonly Dictionary<HexCoordinate, HexEnemyMovePreviewView> previewsByDestination =
            new Dictionary<HexCoordinate, HexEnemyMovePreviewView>();
        private readonly Dictionary<int, HexEnemyDirectionArrowView> arrowsByStackId =
            new Dictionary<int, HexEnemyDirectionArrowView>();
        private readonly Dictionary<HexCoordinate, int> plannedCountsByDestination =
            new Dictionary<HexCoordinate, int>();

        private readonly HashSet<int> activeStackIds = new HashSet<int>();
        private readonly HashSet<int> activeArrowIds = new HashSet<int>();
        private readonly HashSet<HexCoordinate> activePreviewDestinations = new HashSet<HexCoordinate>();
        private readonly List<int> removedStackIds = new List<int>();
        private readonly List<HexCoordinate> removedPreviewDestinations = new List<HexCoordinate>();

        private Material runtimePreviewMaterial;

        private void Reset()
        {
            enemyManager = GetComponent<HexEnemyManager>();
            waveManager = GetComponent<HexWaveManager>();
            billboardCamera = Camera.main;
        }

        private void Awake()
        {
            ResolveReferences();
            EnsurePreviewMaterial();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsurePreviewMaterial();
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

        private void OnDestroy()
        {
            if (runtimePreviewMaterial != null)
                Destroy(runtimePreviewMaterial);
        }

        private void OnValidate()
        {
            columnTopLocalY = Mathf.Max(0f, columnTopLocalY);
            sphereDiameter = Mathf.Max(0.01f, sphereDiameter);
            sphereGap = Mathf.Max(0f, sphereGap);
            textGap = Mathf.Max(0f, textGap);
            previewMaxOpacity = Mathf.Max(previewMinOpacity, previewMaxOpacity);
            previewVerticalOffset = Mathf.Max(0f, previewVerticalOffset);
            previewSphereScale = Mathf.Max(0.01f, previewSphereScale);
            arrowWidth = Mathf.Max(0.01f, arrowWidth);
            arrowEndPadding = Mathf.Max(0f, arrowEndPadding);
            arrowOpacityMultiplier = Mathf.Max(0f, arrowOpacityMultiplier);
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

            float progress = enemyManager != null ? enemyManager.EnemyStepProgress : 0f;
            float opacity = Mathf.Lerp(
                previewMinOpacity,
                previewMaxOpacity,
                Mathf.SmoothStep(0f, 1f, progress));

            foreach (HexEnemyMovePreviewView preview in previewsByDestination.Values)
            {
                if (preview == null)
                    continue;

                preview.SetOpacity(opacity);
                preview.FaceCamera(billboardCamera);
            }

            float arrowOpacity = Mathf.Clamp01(opacity * arrowOpacityMultiplier);
            foreach (HexEnemyDirectionArrowView arrow in arrowsByStackId.Values)
            {
                if (arrow == null)
                    continue;

                arrow.SetOpacity(arrowOpacity);
                arrow.UpdatePositions();
            }
        }

        public void Configure(
            HexEnemyManager targetEnemyManager,
            HexWaveManager targetWaveManager,
            Camera targetCamera,
            HexEnemyStackView targetPrefab = null,
            Material targetMovePreviewMaterial = null)
        {
            if (enemyManager != null)
                enemyManager.StateChanged -= Refresh;

            enemyManager = targetEnemyManager;
            waveManager = targetWaveManager;
            billboardCamera = targetCamera;
            stackViewPrefab = targetPrefab;
            movePreviewMaterial = targetMovePreviewMaterial;
            EnsurePreviewMaterial();

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

            Material previewMaterial = EnsurePreviewMaterial();
            activeStackIds.Clear();
            activeArrowIds.Clear();
            activePreviewDestinations.Clear();
            plannedCountsByDestination.Clear();

            for (int i = 0; i < snapshot.Count; i++)
            {
                HexEnemyStackSnapshot stack = snapshot[i];
                if (!waveManager.TryGetCell(stack.Coordinate, out HexCellView sourceCell) || sourceCell == null)
                    continue;

                activeStackIds.Add(stack.Id);
                if (!viewsByStackId.TryGetValue(stack.Id, out HexEnemyStackView view) || view == null)
                {
                    view = CreateStackView(stack.Id, sourceCell.transform);
                    viewsByStackId[stack.Id] = view;
                }
                else if (view.transform.parent != sourceCell.transform)
                {
                    view.transform.SetParent(sourceCell.transform, false);
                }

                view.transform.localPosition = Vector3.zero;
                view.transform.localRotation = Quaternion.identity;
                view.Refresh(stack.Count, stack.TotalHealth);

                if (!stack.HasPlannedMove ||
                    !waveManager.TryGetCell(stack.PlannedCoordinate, out HexCellView destinationCell) ||
                    destinationCell == null)
                {
                    continue;
                }

                activeArrowIds.Add(stack.Id);
                if (!arrowsByStackId.TryGetValue(stack.Id, out HexEnemyDirectionArrowView arrow) || arrow == null)
                {
                    arrow = CreateArrowView(stack.Id, sourceCell.transform, destinationCell.transform, previewMaterial);
                    arrowsByStackId[stack.Id] = arrow;
                }
                else
                {
                    arrow.RefreshAnchors(sourceCell.transform, destinationCell.transform);
                }

                if (plannedCountsByDestination.TryGetValue(stack.PlannedCoordinate, out int plannedCount))
                    plannedCountsByDestination[stack.PlannedCoordinate] = SaturatingAdd(plannedCount, stack.Count);
                else
                    plannedCountsByDestination.Add(stack.PlannedCoordinate, stack.Count);
            }

            RemoveMissingStackViews();
            RemoveMissingArrowViews();
            RefreshDestinationPreviews(previewMaterial);
        }

        private void RefreshDestinationPreviews(Material previewMaterial)
        {
            foreach (KeyValuePair<HexCoordinate, int> pair in plannedCountsByDestination)
            {
                if (!waveManager.TryGetCell(pair.Key, out HexCellView destinationCell) || destinationCell == null)
                    continue;

                activePreviewDestinations.Add(pair.Key);
                if (!previewsByDestination.TryGetValue(pair.Key, out HexEnemyMovePreviewView preview) || preview == null)
                {
                    preview = CreatePreviewView(pair.Key, destinationCell.transform, previewMaterial);
                    previewsByDestination[pair.Key] = preview;
                }
                else if (preview.transform.parent != destinationCell.transform)
                {
                    preview.transform.SetParent(destinationCell.transform, false);
                }

                preview.transform.localPosition = Vector3.zero;
                preview.transform.localRotation = Quaternion.identity;
                preview.Refresh(pair.Value, pair.Key == HexCoordinate.Origin);
            }

            removedPreviewDestinations.Clear();
            foreach (KeyValuePair<HexCoordinate, HexEnemyMovePreviewView> pair in previewsByDestination)
            {
                if (!activePreviewDestinations.Contains(pair.Key))
                    removedPreviewDestinations.Add(pair.Key);
            }

            for (int i = 0; i < removedPreviewDestinations.Count; i++)
            {
                HexCoordinate destination = removedPreviewDestinations[i];
                HexEnemyMovePreviewView preview = previewsByDestination[destination];
                previewsByDestination.Remove(destination);
                if (preview != null)
                    Destroy(preview.gameObject);
            }
        }

        private void RemoveMissingStackViews()
        {
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

        private void RemoveMissingArrowViews()
        {
            removedStackIds.Clear();
            foreach (KeyValuePair<int, HexEnemyDirectionArrowView> pair in arrowsByStackId)
            {
                if (!activeArrowIds.Contains(pair.Key))
                    removedStackIds.Add(pair.Key);
            }

            for (int i = 0; i < removedStackIds.Count; i++)
            {
                int stackId = removedStackIds[i];
                HexEnemyDirectionArrowView arrow = arrowsByStackId[stackId];
                arrowsByStackId.Remove(stackId);
                if (arrow != null)
                    Destroy(arrow.gameObject);
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

        private HexEnemyStackView CreateStackView(int stackId, Transform parent)
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

        private HexEnemyMovePreviewView CreatePreviewView(
            HexCoordinate destination,
            Transform parent,
            Material previewMaterial)
        {
            GameObject previewObject = new GameObject(
                $"Enemy Move Preview {destination.A}_{destination.B}_{destination.C}");
            previewObject.transform.SetParent(parent, false);
            HexEnemyMovePreviewView preview = previewObject.AddComponent<HexEnemyMovePreviewView>();
            preview.Initialize(
                previewMaterial,
                movePreviewColor,
                columnTopLocalY + previewVerticalOffset,
                sphereDiameter * previewSphereScale,
                sphereGap,
                textGap);
            return preview;
        }

        private HexEnemyDirectionArrowView CreateArrowView(
            int stackId,
            Transform source,
            Transform destination,
            Material previewMaterial)
        {
            GameObject arrowObject = new GameObject($"Enemy Move Arrow {stackId}");
            arrowObject.transform.SetParent(transform, false);
            HexEnemyDirectionArrowView arrow = arrowObject.AddComponent<HexEnemyDirectionArrowView>();
            arrow.Initialize(
                stackId,
                source,
                destination,
                previewMaterial,
                movePreviewColor,
                columnTopLocalY + previewVerticalOffset + 0.02f,
                arrowWidth,
                arrowEndPadding);
            return arrow;
        }

        private Material EnsurePreviewMaterial()
        {
            if (movePreviewMaterial != null)
                return movePreviewMaterial;
            if (runtimePreviewMaterial != null)
                return runtimePreviewMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError(
                    $"[{nameof(HexEnemyVisualManager)}] No compatible shader was found for enemy move previews.",
                    this);
                return null;
            }

            runtimePreviewMaterial = new Material(shader)
            {
                name = "Runtime Enemy Move Preview Material",
                hideFlags = HideFlags.DontSave
            };
            ConfigureTransparentMaterial(runtimePreviewMaterial);
            return runtimePreviewMaterial;
        }

        public static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
                return;

            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 3f);
                material.EnableKeyword("_ALPHABLEND_ON");
            }
            else
            {
                material.DisableKeyword("_ALPHABLEND_ON");
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
        }

        private static int SaturatingAdd(int left, int right)
        {
            long result = (long)left + right;
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }
    }
}
