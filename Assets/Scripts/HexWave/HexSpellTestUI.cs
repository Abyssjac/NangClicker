using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NangClicker.HexWave
{
    /// <summary>Flat skill-bar presentation. Equipment and selection belong to SpellManager.</summary>
    [DisallowMultipleComponent]
    public sealed class HexSpellTestUI : MonoBehaviour
    {
        [SerializeField] private SpellManager manager;
        private RectTransform panel;
        private TMP_Text heading;
        private Image[] frames, icons, accents;
        private Image chargeFill;
        private bool wasReady;
        private float readyAt;
        private Button[] buttons;
        private static HexSpellTestUI active;
        public static bool IsPointerOverPanel(Vector2 screenPoint) => active != null && active.panel != null &&
            RectTransformUtility.RectangleContainsScreenPoint(active.panel, screenPoint);
        private void Start()
        {
            active = this;
            if (manager == null) manager = GetComponent<SpellManager>();
            if (manager == null) return;
            var root = new GameObject("Spell Bar Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; root.GetComponent<Canvas>().sortingOrder = 90;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            int count = manager.Slots.Length;
            panel = MakeRect("Spell Slots", root.transform, new Vector2(0.5f, 0), new Vector2(Mathf.Max(420, count * 134 + 36), 178), new Vector2(0, 16));
            panel.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.045f, 0.06f, 0.9f);
            frames = new Image[count]; icons = new Image[count]; accents = new Image[count]; buttons = new Button[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var slot = MakeRect("Slot " + (i + 1), panel, new Vector2(0.5f, 0), new Vector2(116, 116), new Vector2((i - (count - 1) * 0.5f) * 134, 45));
                frames[i] = slot.gameObject.AddComponent<Image>();
                buttons[i] = slot.gameObject.AddComponent<Button>(); buttons[i].targetGraphic = frames[i];
                buttons[i].transition = Selectable.Transition.None; buttons[i].onClick.AddListener(() => manager.SelectSlot(index));
                var inside = MakeRect("Face", slot, new Vector2(0.5f, 0), new Vector2(106, 106), new Vector2(0, 5));
                inside.gameObject.AddComponent<Image>().color = new Color(0.065f, 0.085f, 0.10f);
                var icon = MakeRect("Icon", slot, new Vector2(0.5f, 0), new Vector2(80, 80), new Vector2(0, 24));
                icons[i] = icon.gameObject.AddComponent<Image>(); icons[i].preserveAspect = true; icons[i].raycastTarget = false;
                var number = MakeRect("Hotkey", slot, new Vector2(0, 0), new Vector2(28, 26), new Vector2(22, 10));
                var text = number.gameObject.AddComponent<TextMeshProUGUI>(); Style(text, 21); text.text = (i + 1).ToString();
                var accent = MakeRect("Selected Mark", slot, new Vector2(0.5f, 0), new Vector2(46, 5), new Vector2(0, 7));
                accents[i] = accent.gameObject.AddComponent<Image>(); accents[i].raycastTarget = false;
            }
            var status = MakeRect("Heading", panel, new Vector2(0.5f, 0), new Vector2(panel.sizeDelta.x - 20, 30), new Vector2(0, 6));
            heading = status.gameObject.AddComponent<TextMeshProUGUI>(); Style(heading, 20);
            var track = MakeRect("Charge Track", panel, new Vector2(0.5f, 0), new Vector2(panel.sizeDelta.x - 24, 5), new Vector2(0, 38));
            track.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.18f, 0.2f);
            var fill = MakeRect("Charge Progress", track, new Vector2(0, 0), track.sizeDelta, Vector2.zero);
            fill.pivot = Vector2.zero;
            chargeFill = fill.gameObject.AddComponent<Image>(); chargeFill.raycastTarget = false;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Spell UI EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }
        }
        private void Update()
        {
            if (frames == null || manager == null) return;
            if (manager.IsReady && !wasReady) readyAt = Time.unscaledTime;
            wasReady = manager.IsReady;
            for (int i = 0; i < frames.Length; i++)
            {
                var spell = manager.GetSpell(i); bool selected = i == manager.SelectedSlot;
                Color tint = selected ? new Color(0.4f, 0.95f, 0.82f) : new Color(0.29f, 0.34f, 0.37f);
                if (selected && manager.IsCharging)
                    tint = Color.Lerp(new Color(0.3f, 0.5f, 0.65f), new Color(0.55f, 1, 0.8f), manager.ChargeProgress);
                if (selected && manager.IsReady)
                    tint = Color.Lerp(tint, Color.white, Mathf.Clamp01(1 - (Time.unscaledTime - readyAt) / 0.35f));
                frames[i].color = spell == null ? new Color(0.12f, 0.14f, 0.16f) : tint;
                icons[i].sprite = spell != null ? spell.icon : null; icons[i].enabled = icons[i].sprite != null;
                icons[i].color = selected ? Color.white : new Color(0.6f, 0.66f, 0.69f);
                accents[i].color = selected ? tint : Color.clear;
                buttons[i].interactable = spell != null && Time.timeScale > 0;
            }
            chargeFill.rectTransform.localScale = new Vector3(manager.ChargeProgress, 1, 1);
            chargeFill.color = manager.IsReady ? new Color(0.55f, 1, 0.8f) : new Color(0.3f, 0.7f, 1);
            heading.text = manager.IsReady ? "READY - RELEASE MOUSE TO CAST" :
                manager.IsCharging ? $"CHARGING {manager.ChargeElapsed:0.0} / {manager.ChargeDuration:0.0}s" :
                $"1/2/3 Select  |  R Rotate {manager.Direction + 1}/6  |  Hold LMB";
        }
        private void OnDestroy() { if (active == this) active = null; }
        private static RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 position)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static void Style(TMP_Text text, float size)
        { text.font = TMP_Settings.defaultFontAsset; text.fontSize = size; text.alignment = TextAlignmentOptions.Center; text.color = Color.white; text.raycastTarget = false; }
    }
}
