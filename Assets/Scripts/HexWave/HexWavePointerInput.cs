using UnityEngine;

namespace NangClicker.HexWave
{
    [DisallowMultipleComponent]
    public sealed class HexWavePointerInput : MonoBehaviour
    {
        [SerializeField] private HexWaveManager manager;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private LayerMask raycastLayers = ~0;
        [SerializeField] private float impulse = 8f;
        [SerializeField] private bool autoImpulseOnStart;
        [SerializeField] private HexCoordinate autoImpulseCoordinate;

        private void Reset()
        {
            manager = GetComponent<HexWaveManager>();
        }

        private void Start()
        {
            if (manager == null)
                manager = GetComponent<HexWaveManager>();
            if (inputCamera == null)
                inputCamera = Camera.main;

            if (autoImpulseOnStart && manager != null)
                manager.EnqueueImpulse(autoImpulseCoordinate, impulse);
        }

        private void Update()
        {
            if (manager == null)
                return;

            if (Input.GetKeyDown(KeyCode.Space))
                manager.EnqueueImpulse(HexCoordinate.Origin, impulse);

            if (!Input.GetMouseButtonDown(0) || inputCamera == null)
                return;

            Ray ray = inputCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, raycastLayers))
                return;

            HexCellView cell = hit.collider.GetComponentInParent<HexCellView>();
            if (cell != null)
                manager.EnqueueImpulse(cell.Coordinate, impulse);
        }

        public void Configure(HexWaveManager targetManager, Camera targetCamera, float impulseStrength, bool impulseOnStart)
        {
            manager = targetManager;
            inputCamera = targetCamera;
            impulse = impulseStrength;
            autoImpulseOnStart = impulseOnStart;
            autoImpulseCoordinate = HexCoordinate.Origin;
        }
    }
}
