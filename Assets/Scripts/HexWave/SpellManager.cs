using JackyUtility;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NangClicker.HexWave
{
    public enum HexSpellDirectionMode { ManualRotation = 0, PlayerToTarget = 1 }

    [DefaultExecutionOrder(-50), DisallowMultipleComponent, RequireComponent(typeof(HexWaveManager))]
    public sealed class SpellManager : MonoBehaviour
    {
        [SerializeField] private HexWaveManager waveManager;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private HexSpellPreviewRenderer preview;
        [SerializeField] private Key_HexSpellPP[] slots = { Key_HexSpellPP.Line, Key_HexSpellPP.ChargedTriangle, Key_HexSpellPP.ResonanceCage };
        [SerializeField] private int selectedSlot;
        [SerializeField] private HexSpellDirectionMode directionMode = HexSpellDirectionMode.PlayerToTarget;
        [SerializeField] private LayerMask terrainQueryMask = ~0;
        private PlayerAgent lockedPlayer;
        private int lastAutoDirection;
        [Range(0, 5), SerializeField] private int direction;
        [SerializeField] private float strengthMultiplier = 1;
        private HexSpellDatabase database;
        private HexSpellWave activeCast;
        private HexSpellProperty chargingSpell;
        private float chargingMultiplier;
        public int SelectedSlot => selectedSlot;
        public int Direction => activeCast != null ? activeCast.Direction :
            directionMode == HexSpellDirectionMode.ManualRotation ? direction : lastAutoDirection;
        public HexSpellDirectionMode DirectionMode => directionMode;
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
            if (IsCharging || directionMode != HexSpellDirectionMode.ManualRotation) return; // The committed aim cannot rotate during a held cast.
            direction = (direction + 1) % 6;
        }
        public static int QuantizeDirection(HexCoordinate player, HexCoordinate target, int fallback = 0)
        {
            Vector3 delta = target.ToLocalPosition(1) - player.ToLocalPosition(1);
            if (delta.sqrMagnitude < 0.000001f) return (fallback % 6 + 6) % 6;
            float best = float.NegativeInfinity; int result = 0;
            for (int i = 0; i < 6; i++)
            {
                var axis = HexCoordinate.GetNeighborDirection(1).Rotate(i).ToLocalPosition(1).normalized;
                float score = Vector3.Dot(delta.normalized, axis);
                if (score > best + 0.000001f) { best = score; result = i; }
            }
            return result;
        }
        private bool TryPlayer(out PlayerAgent player, out HexCellView cell)
        {
            player = PlayerManager.Instance != null ? PlayerManager.Instance.ActivePlayer : null;
            cell = null;
            var motor = player != null ? player.GetComponent<HexPlayerMotor>() : null;
            return motor != null && motor.TryGetCurrentCell(out cell);
        }
        private int AimDirection(HexCoordinate target)
        {
            if (directionMode == HexSpellDirectionMode.ManualRotation) return direction;
            if (TryPlayer(out _, out var playerCell)) lastAutoDirection = QuantizeDirection(playerCell.Coordinate, target, lastAutoDirection);
            return lastAutoDirection;
        }
        private void ReleaseMovementLock()
        {
            if (lockedPlayer != null) lockedPlayer.ReleaseMovementLock(this);
            lockedPlayer = null;
        }
        public bool BeginAt(HexCoordinate origin)
        {
            if (activeCast != null || waveManager == null) return false;
            if (!TryPlayer(out var player, out _)) return false;
            int castDirection = AimDirection(origin);
            chargingSpell = SelectedSpell; chargingMultiplier = strengthMultiplier;
            activeCast = waveManager.BeginSpell(chargingSpell, origin, castDirection, chargingMultiplier);
            if (activeCast != null) { lockedPlayer = player; player.AcquireMovementLock(this); }
            return activeCast != null;
        }
        public bool ReleaseCurrent()
        {
            if (activeCast == null) return false;
            bool result = false;
            if (IsReady) result = waveManager.ReleaseSpell(activeCast);
            else waveManager.CancelSpell(activeCast);
            ReleaseMovementLock(); activeCast = null; chargingSpell = null; if (preview != null) preview.Hide();
            return result;
        }
        public void CancelCurrent()
        {
            if (activeCast != null && waveManager != null) waveManager.CancelSpell(activeCast);
            ReleaseMovementLock(); activeCast = null; chargingSpell = null; if (preview != null) preview.Hide();
        }
        private void Update()
        {
            if (activeCast != null && activeCast.Phase != HexCastPhase.Charging && activeCast.Phase != HexCastPhase.Ready)
            { ReleaseMovementLock(); activeCast = null; chargingSpell = null; }
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
            if (!waveManager.TryRaycastCell(ray, out var cell, out _, 1000, terrainQueryMask.value))
            { if (preview != null) preview.Hide(); return; }
            if (cell == null || !waveManager.TryGetCell(cell.Coordinate, out var owned) || cell != owned)
            { if (preview != null) preview.Hide(); return; }
            if (preview != null) preview.Show(waveManager, spell, cell.Coordinate, AimDirection(cell.Coordinate), strengthMultiplier);
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
