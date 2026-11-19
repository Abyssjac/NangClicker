using JackyUtility;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NangClicker.HexWave
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HexWaveManager))]
    public sealed class SpellManager : MonoBehaviour
    {
        [SerializeField] private HexWaveManager waveManager;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private HexSpellPreviewRenderer preview;
        [SerializeField] private Key_HexSpellPP[] slots = { Key_HexSpellPP.Line, Key_HexSpellPP.Triangle };
        [SerializeField] private int selectedSlot;
        [Range(0, 5)] [SerializeField] private int direction;
        [SerializeField] private float strengthMultiplier = 1f;
        private HexSpellDatabase database;
        private bool warned;
        private bool databaseResolved;
        public int SelectedSlot => selectedSlot;
        public int Direction => direction;
        public Key_HexSpellPP[] Slots => slots;
        public HexWaveManager WaveManager => waveManager;
        public HexSpellProperty SelectedSpell => GetSpell(selectedSlot);

        private void Awake()
        {
            if (waveManager == null) waveManager = GetComponent<HexWaveManager>();
            if (inputCamera == null) inputCamera = Camera.main;
            if (preview == null) preview = GetComponent<HexSpellPreviewRenderer>();
            var oldInput = GetComponent<HexWavePointerInput>();
            if (oldInput != null) oldInput.enabled = false;
        }

        public HexSpellProperty GetSpell(int slot)
        {
            if (!databaseResolved && PropertyDatabaseManager.Instance != null)
            {
                database = PropertyDatabaseManager.Instance.GetDatabase<HexSpellDatabase>();
                databaseResolved = true;
            }
            return database != null && slot >= 0 && slot < slots.Length ? database.GetByEnum(slots[slot]) : null;
        }

        public void SelectSlot(int slot) { if (slot >= 0 && slot < slots.Length) selectedSlot = slot; }
        public void RotateDirection() { direction = (direction + 1) % 6; }
        public bool CastAt(HexCoordinate origin) => waveManager != null &&
            waveManager.CastSpell(SelectedSpell, origin, direction, strengthMultiplier);

        private void Update()
        {
            var spell = SelectedSpell;
            if (spell == null && !warned && PropertyDatabaseManager.Instance != null)
            { Debug.LogError("[SpellManager] Selected spell is missing from the registered HexSpellDatabase.", this); warned = true; }
            if (Time.timeScale <= 0f || spell == null || inputCamera == null || !inputCamera.isActiveAndEnabled ||
                (inputCamera.TryGetComponent<CameraBase>(out var cameraBase) && cameraBase.IsInputLocked) ||
                (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                HexSpellTestUI.IsPointerOverPanel(Input.mousePosition) ||
                !inputCamera.pixelRect.Contains(Input.mousePosition))
            { if (preview != null) preview.Hide(); return; }

            Ray ray = inputCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit))
            { if (preview != null) preview.Hide(); return; }
            var cell = hit.collider.GetComponentInParent<HexCellView>();
            if (cell == null ||
                !waveManager.TryGetCell(cell.Coordinate, out var ownedCell) || ownedCell != cell)
            { if (preview != null) preview.Hide(); return; }
            if (preview != null) preview.Show(waveManager, spell, cell.Coordinate, direction, strengthMultiplier);
            if (Input.GetMouseButtonDown(0)) CastAt(cell.Coordinate);
        }

        private void OnDisable() { if (preview != null) preview.Hide(); }
    }
}
