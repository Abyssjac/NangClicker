using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NangClicker.HexWave
{
    [DisallowMultipleComponent]
    public sealed class HexSpellTestUI : MonoBehaviour
    {
        [SerializeField] private SpellManager manager;
        private RectTransform panel;
        private TMP_Text status;
        private TMP_Text[] captions;
        private Image[] backgrounds;
        private static HexSpellTestUI active;
        public static bool IsPointerOverPanel(Vector2 screenPoint) => active != null && active.panel != null &&
            RectTransformUtility.RectangleContainsScreenPoint(active.panel, screenPoint);

        private void Start()
        {
            active = this;
            if (manager == null) manager = GetComponent<SpellManager>();
            if (manager == null) return;
            var canvasObject = new GameObject("Spell Test Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            panel = MakeRect("Spell Slots", canvasObject.transform, new Vector2(0.5f, 0f), new Vector2(900, 136), new Vector2(0, 18));
            panel.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.04f, 0.07f, 0.94f);
            captions = new TMP_Text[manager.Slots.Length]; backgrounds = new Image[captions.Length];
            float width = Mathf.Min(230f, 600f / Mathf.Max(1, captions.Length));
            for (int i = 0; i < captions.Length; i++)
            {
                int index = i;
                Button button = MakeButton("Spell " + i, panel, new Vector2(-310 + i * width, 25), new Vector2(width - 12, 66), out captions[i]);
                backgrounds[i] = button.image;
                button.onClick.AddListener(() => manager.SelectSlot(index));
            }
            Button rotate = MakeButton("Rotate 60", panel, new Vector2(315, 25), new Vector2(230, 66), out var rotateText);
            rotateText.text = "Rotate 60°";
            rotate.onClick.AddListener(manager.RotateDirection);
            var statusRect = MakeRect("Status", panel, new Vector2(0.5f, 0f), new Vector2(860, 32), new Vector2(0, 8));
            status = statusRect.gameObject.AddComponent<TextMeshProUGUI>();
            Style(status, 20); status.raycastTarget = false;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Spell UI EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }
        }

        private void Update()
        {
            if (manager == null || captions == null) return;
            for (int i = 0; i < captions.Length; i++)
            {
                var spell = manager.GetSpell(i);
                captions[i].text = spell != null ? spell.displayName : manager.Slots[i].ToString();
                backgrounds[i].color = i == manager.SelectedSlot ? new Color(0.12f, 0.42f, 0.65f) : new Color(0.12f, 0.17f, 0.23f);
            }
            var forward = HexCoordinate.GetNeighborDirection(1).Rotate(manager.Direction);
            status.text = $"Direction {manager.Direction + 1}/6  {forward}     Click a floor hex to cast     Pending casts: {manager.WaveManager.PendingCastCount}  Waves: {manager.WaveManager.ActiveWaveCount}";
        }

        private void OnDestroy() { if (active == this) active = null; }

        private static RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 position)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0f); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static Button MakeButton(string name, Transform parent, Vector2 position, Vector2 size, out TMP_Text text)
        {
            var rect = MakeRect(name, parent, new Vector2(0.5f, 0.5f), size, position - new Vector2(0, 32));
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.12f, 0.17f, 0.23f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = MakeRect("Label", rect, new Vector2(0.5f, 0f), size, Vector2.zero);
            text = label.gameObject.AddComponent<TextMeshProUGUI>(); Style(text, 25); text.raycastTarget = false; return button;
        }
        private static void Style(TMP_Text text, float size)
        { text.font = TMP_Settings.defaultFontAsset; text.fontSize = size; text.alignment = TextAlignmentOptions.Center; text.color = Color.white; }
    }
}
