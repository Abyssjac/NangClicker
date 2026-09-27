using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NangClicker.HexWave
{
    [DisallowMultipleComponent]
    public sealed class HexGameUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexEnemyManager enemyManager;
        [SerializeField] private GameObject guidePanel;
        [SerializeField] private TMP_Text baseHealthText;
        [SerializeField] private GameObject failPanel;

        [Header("Behavior")]
        [SerializeField] private bool showGuideOnStart = true;
        [SerializeField] private bool pauseOnFailure = true;

        private bool subscribed;
        private bool started;
        private bool failed;
        private bool restartRequested;
        private float timeScaleBeforeFailure = 1f;

        private void Awake()
        {
            ResolveEnemyManager();
            if (failPanel != null)
                failPanel.SetActive(false);
        }

        private void OnEnable()
        {
            ResolveEnemyManager();
            Subscribe();

            if (started)
                RefreshBaseHealth();
        }

        private void Start()
        {
            started = true;
            SetGuideVisible(showGuideOnStart);
            if (failPanel != null)
                failPanel.SetActive(false);
            RefreshBaseHealth();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (failed && pauseOnFailure && !restartRequested)
                Time.timeScale = timeScaleBeforeFailure;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
                ToggleGuide();

            if (!failed || !Input.GetKeyDown(KeyCode.R))
                return;

        }

        public void Configure(
            HexEnemyManager targetEnemyManager,
            GameObject targetGuidePanel,
            TMP_Text targetBaseHealthText,
            GameObject targetFailPanel)
        {
            Unsubscribe();
            enemyManager = targetEnemyManager;
            guidePanel = targetGuidePanel;
            baseHealthText = targetBaseHealthText;
            failPanel = targetFailPanel;
            Subscribe();

            if (failPanel != null)
                failPanel.SetActive(false);
            if (started)
                RefreshBaseHealth();
        }

        public void ToggleGuide()
        {
            if (guidePanel != null)
                guidePanel.SetActive(!guidePanel.activeSelf);
        }

        public void SetGuideVisible(bool visible)
        {
            if (guidePanel != null)
                guidePanel.SetActive(visible);
        }

        private void ResolveEnemyManager()
        {
            if (enemyManager == null)
                enemyManager = GetComponentInParent<HexEnemyManager>();
            if (enemyManager == null)
                enemyManager = FindFirstObjectByType<HexEnemyManager>();
        }

        private void Subscribe()
        {
            if (subscribed || enemyManager == null)
                return;

            enemyManager.TargetPointsChanged += OnTargetPointsChanged;
            enemyManager.TargetDepleted += OnTargetDepleted;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || enemyManager == null)
                return;

            enemyManager.TargetPointsChanged -= OnTargetPointsChanged;
            enemyManager.TargetDepleted -= OnTargetDepleted;
            subscribed = false;
        }

        private void OnTargetPointsChanged(int currentPoints, int maximumPoints)
        {
            SetBaseHealthText(currentPoints, maximumPoints);
        }

        private void RefreshBaseHealth()
        {
            if (enemyManager == null)
                return;

            SetBaseHealthText(
                enemyManager.TargetPointsRemaining,
                enemyManager.InitialTargetPoints);
        }

        private void SetBaseHealthText(int currentPoints, int maximumPoints)
        {
            if (baseHealthText != null)
                baseHealthText.text = $"BASE HP  {Mathf.Max(0, currentPoints)} / {Mathf.Max(1, maximumPoints)}";
        }

        private void OnTargetDepleted()
        {
            if (failed)
                return;

            failed = true;
            SetGuideVisible(false);
            if (failPanel != null)
                failPanel.SetActive(true);

            if (!pauseOnFailure)
                return;

            timeScaleBeforeFailure = Time.timeScale;
            Time.timeScale = 0f;
        }
    }
}
