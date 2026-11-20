using JackyUtility;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NangClicker.HexWave
{
    [DefaultExecutionOrder(-50), DisallowMultipleComponent, RequireComponent(typeof(HexWaveManager))]
    public sealed class SpellManager : MonoBehaviour
    {
        [SerializeField] private HexWaveManager waveManager;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private HexSpellPreviewRenderer preview;
        [SerializeField] private Key_HexSpellPP[] slots = { Key_HexSpellPP.Line, Key_HexSpellPP.ChargedTriangle, Key_HexSpellPP.ResonanceCage };
        [SerializeField] private int selectedSlot;
        [Range(0, 5), SerializeField] private int direction;
        [SerializeField] private float strengthMultiplier = 1;
        private HexSpellDatabase database;
        private HexSpellWave activeCast;
        private HexSpellProperty chargingSpell;
        private float chargingMultiplier;
        public int SelectedSlot => selectedSlot;
        public int Direction => direction;
        public Key_HexSpellPP[] Slots => slots;
        public HexWaveManager WaveManager => waveManager;
        public HexSpellProperty SelectedSpell => GetSpell(selectedSlot);
        public HexSpellWave ActiveCast => activeCast;
        public bool IsCharging => activeCast != null;
        public bool IsReady => activeCast != null && activeCast.Phase == HexCastPhase.Ready;
        public float ChargeProgress => activeCast?.ChargeProgress ?? 0;
        public float ChargeElapsed => activeCast?.ChargeElapsed ?? 0;
        public float ChargeDuration => activeCast?.ChargeDuration ?? 0;
        private void Awake()
        {
            if (waveManager == null) waveManager = GetComponent<HexWaveManager>();
            if (inputCamera == null) inputCamera = Camera.main;
            if (preview == null) preview = GetComponent<HexSpellPreviewRenderer>();
        }
        public HexSpellProperty GetSpell(int slot)
        {
            if (database == null && PropertyDatabaseManager.Instance != null)
                database = PropertyDatabaseManager.Instance.GetDatabase<HexSpellDatabase>();
            return database != null && slot >= 0 && slot < slots.Length ? database.GetByEnum(slots[slot]) : null;
        }
        public void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= slots.Length || slot == selectedSlot) return;
            CancelCurrent(); selectedSlot = slot;
        }
        public void RotateDirection()
        {
            if (IsCharging) return; // The committed aim cannot rotate during a held cast.
            direction = (direction + 1) % 6;
        }
        public bool BeginAt(HexCoordinate origin)
        {
            if (activeCast != null || waveManager == null) return false;
            chargingSpell = SelectedSpell; chargingMultiplier = strengthMultiplier;
            activeCast = waveManager.BeginSpell(chargingSpell, origin, direction, chargingMultiplier);
            return activeCast != null;
        }
        public bool ReleaseCurrent()
        {
            if (activeCast == null) return false;
            bool result = false;
            if (IsReady) result = waveManager.ReleaseSpell(activeCast);
            else waveManager.CancelSpell(activeCast);
            activeCast = null; chargingSpell = null; if (preview != null) preview.Hide();
            return result;
        }
        public void CancelCurrent()
        {
            if (activeCast != null && waveManager != null) waveManager.CancelSpell(activeCast);
            activeCast = null; chargingSpell = null; if (preview != null) preview.Hide();
        }
        private void Update()
        {
            if (activeCast != null && activeCast.Phase != HexCastPhase.Charging && activeCast.Phase != HexCastPhase.Ready)
            { activeCast = null; chargingSpell = null; }
            bool locked = Time.timeScale <= 0 || inputCamera == null || !inputCamera.isActiveAndEnabled ||
                inputCamera.TryGetComponent<CameraBase>(out var cameraBase) && cameraBase.IsInputLocked || EditingText();
            if (locked) { CancelCurrent(); return; }
            for (int i = 0; i < Mathf.Min(slots.Length, 9); i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i))) SelectSlot(i);
            if (Input.GetKeyDown(KeyCode.R) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt) &&
                !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) RotateDirection();
            if (activeCast != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { CancelCurrent(); return; }
                if (preview != null && chargingSpell != null)
                    preview.Show(waveManager, chargingSpell, activeCast.Origin, activeCast.Direction, chargingMultiplier);
                if (Input.GetMouseButtonUp(0)) ReleaseCurrent();
                else if (!Input.GetMouseButton(0)) CancelCurrent();
                return;
            }
            var spell = SelectedSpell;
            if (spell == null || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject() ||
                HexSpellTestUI.IsPointerOverPanel(Input.mousePosition) || !inputCamera.pixelRect.Contains(Input.mousePosition))
            { if (preview != null) preview.Hide(); return; }
            var ray = inputCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit)) { if (preview != null) preview.Hide(); return; }
            var cell = hit.collider.GetComponentInParent<HexCellView>();
            if (cell == null || !waveManager.TryGetCell(cell.Coordinate, out var owned) || cell != owned)
            { if (preview != null) preview.Hide(); return; }
            if (preview != null) preview.Show(waveManager, spell, cell.Coordinate, direction, strengthMultiplier);
            if (Input.GetMouseButtonDown(0)) BeginAt(cell.Coordinate);
        }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelCurrent(); }
        private void OnDisable() => CancelCurrent();
        private static bool EditingText()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && (selected.GetComponent<UnityEngine.UI.InputField>() != null || selected.GetComponent<TMPro.TMP_InputField>() != null);
        }
    }
}
