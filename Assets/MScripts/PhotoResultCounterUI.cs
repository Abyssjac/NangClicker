using TMPro;
using UnityEngine;

namespace MonaLisaGame
{
    /// <summary>Canvas-only presentation of the PhotoPointManager's success and failure counters.</summary>
    public sealed class PhotoResultCounterUI : MonoBehaviour
    {
        [SerializeField] private PhotoPointManager photoPointManager;
        [SerializeField] private TextMeshProUGUI successText;
        [SerializeField] private TextMeshProUGUI failureText;
        [SerializeField] private string successFormat = "SUCCESS  {0}";
        [SerializeField] private string failureFormat = "FAIL  {0}";

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void Start()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            if (photoPointManager != null)
                photoPointManager.CountsChanged -= OnCountsChanged;
        }

        public void ConfigurePrototype(PhotoPointManager manager, TextMeshProUGUI success, TextMeshProUGUI failure)
        {
            if (photoPointManager != null)
                photoPointManager.CountsChanged -= OnCountsChanged;

            photoPointManager = manager;
            successText = success;
            failureText = failure;
            Subscribe();
            Refresh();
        }

        private void Subscribe()
        {
            if (photoPointManager == null)
                photoPointManager = FindFirstObjectByType<PhotoPointManager>();

            if (photoPointManager == null)
                return;

            photoPointManager.CountsChanged -= OnCountsChanged;
            photoPointManager.CountsChanged += OnCountsChanged;
        }

        private void OnCountsChanged(int successCount, int failureCount) => Refresh(successCount, failureCount);

        private void Refresh()
        {
            if (photoPointManager != null)
                Refresh(photoPointManager.SuccessCount, photoPointManager.FailureCount);
            else
                Refresh(0, 0);
        }

        private void Refresh(int successCount, int failureCount)
        {
            if (successText != null)
                successText.SetText(successFormat, successCount);
            if (failureText != null)
                failureText.SetText(failureFormat, failureCount);
        }
    }
}
