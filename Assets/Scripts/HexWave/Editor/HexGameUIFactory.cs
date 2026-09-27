using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NangClicker.HexWave.Editor
{
    public static class HexGameUIFactory
    {
        public const string PrefabPath = "Assets/Prefabs/HexWave/HexGameUI.prefab";

        private const string RootName = "Hex Game UI";

        private const string GuideCopy =
            "OBJECTIVE\n\n" +
            "Stop enemies from reaching the center.\n\n" +
            "Click a hex to send a wave through the grid.\n" +
            "Moving hexes damage enemies standing on them.\n\n" +
            "Enemies that reach the center damage your Base.\n" +
            "If Base HP reaches 0, you lose.\n\n" +
            "CONTROLS\n\n" +
            "Left Click — Attack a Hex\n" +
            "Middle Mouse Drag — Move Camera\n" +
            "Mouse Wheel — Zoom\n" +
            "I — Toggle Guide";

        [MenuItem("Tools/Hex Wave/Add Game UI To Active Scene")]
        public static void AddGameUIToActiveScene()
        {
            HexGameUIController existing = Object.FindFirstObjectByType<HexGameUIController>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log($"[{nameof(HexGameUIFactory)}] '{RootName}' already exists.", existing);
                return;
            }

            HexEnemyManager enemyManager = Object.FindFirstObjectByType<HexEnemyManager>();
            if (enemyManager == null)
            {
                Debug.LogError(
                    $"[{nameof(HexGameUIFactory)}] No {nameof(HexEnemyManager)} exists in the active scene.");
                return;
            }

            HexGameUIController instance = InstantiateGameUI(enemyManager.transform, enemyManager);
            Selection.activeGameObject = instance.gameObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        public static HexGameUIController GetOrCreatePrefab()
        {
            HexGameUIController existing = AssetDatabase.LoadAssetAtPath<HexGameUIController>(PrefabPath);
            if (existing != null)
                return existing;

            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "HexWave");

            GameObject temporary = CreateHierarchy();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, PrefabPath);
            Object.DestroyImmediate(temporary);
            AssetDatabase.SaveAssets();
            return prefab.GetComponent<HexGameUIController>();
        }

        public static HexGameUIController InstantiateGameUI(
            Transform parent,
            HexEnemyManager enemyManager)
        {
            HexGameUIController prefab = GetOrCreatePrefab();
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                prefab.gameObject,
                parent != null ? parent.gameObject.scene : SceneManager.GetActiveScene());

            if (parent != null)
                instance.transform.SetParent(parent, false);

            Undo.RegisterCreatedObjectUndo(instance, "Create Hex Game UI");
            HexGameUIController controller = instance.GetComponent<HexGameUIController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("enemyManager").objectReferenceValue = enemyManager;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject CreateHierarchy()
        {
            GameObject root = new GameObject(
                RootName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(HexGameUIController));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject guidePanel = CreatePanel(
                "Guide Panel",
                root.transform,
                new Color(0.025f, 0.035f, 0.05f, 0.82f));
            RectTransform guideRect = guidePanel.GetComponent<RectTransform>();
            guideRect.anchorMin = new Vector2(0f, 1f);
            guideRect.anchorMax = new Vector2(0f, 1f);
            guideRect.pivot = new Vector2(0f, 1f);
            guideRect.anchoredPosition = new Vector2(24f, -24f);
            guideRect.sizeDelta = new Vector2(620f, 500f);

            TextMeshProUGUI guideText = CreateText("Guide Text", guidePanel.transform, GuideCopy);
            Stretch(guideText.rectTransform, 26f);
            guideText.fontSize = 25f;
            guideText.alignment = TextAlignmentOptions.TopLeft;
            guideText.lineSpacing = 2f;

            TextMeshProUGUI healthText = CreateText("Base Health Text", root.transform, "BASE HP  20 / 20");
            RectTransform healthRect = healthText.rectTransform;
            healthRect.anchorMin = new Vector2(0.5f, 1f);
            healthRect.anchorMax = new Vector2(0.5f, 1f);
            healthRect.pivot = new Vector2(0.5f, 1f);
            healthRect.anchoredPosition = new Vector2(0f, -24f);
            healthRect.sizeDelta = new Vector2(600f, 72f);
            healthText.fontSize = 38f;
            healthText.fontStyle = FontStyles.Bold;
            healthText.alignment = TextAlignmentOptions.Center;

            GameObject failPanel = CreatePanel(
                "Fail Panel",
                root.transform,
                new Color(0.015f, 0.02f, 0.03f, 0.86f));
            Stretch(failPanel.GetComponent<RectTransform>(), 0f);

            TextMeshProUGUI failTitle = CreateText("Fail Title", failPanel.transform, "FAIL");
            RectTransform failTitleRect = failTitle.rectTransform;
            failTitleRect.anchorMin = new Vector2(0.5f, 0.5f);
            failTitleRect.anchorMax = new Vector2(0.5f, 0.5f);
            failTitleRect.pivot = new Vector2(0.5f, 0.5f);
            failTitleRect.anchoredPosition = new Vector2(0f, 90f);
            failTitleRect.sizeDelta = new Vector2(700f, 130f);
            failTitle.fontSize = 84f;
            failTitle.fontStyle = FontStyles.Bold;
            failTitle.color = new Color(1f, 0.16f, 0.12f, 1f);
            failTitle.alignment = TextAlignmentOptions.Center;

            TextMeshProUGUI failMessage = CreateText(
                "Fail Message",
                failPanel.transform,
                "The Base has fallen.\n\nAlt + R to restart");
            RectTransform failMessageRect = failMessage.rectTransform;
            failMessageRect.anchorMin = new Vector2(0.5f, 0.5f);
            failMessageRect.anchorMax = new Vector2(0.5f, 0.5f);
            failMessageRect.pivot = new Vector2(0.5f, 0.5f);
            failMessageRect.anchoredPosition = new Vector2(0f, -55f);
            failMessageRect.sizeDelta = new Vector2(900f, 180f);
            failMessage.fontSize = 34f;
            failMessage.alignment = TextAlignmentOptions.Center;

            failPanel.SetActive(false);
            HexGameUIController controller = root.GetComponent<HexGameUIController>();
            controller.Configure(null, guidePanel, healthText, failPanel);
            return root;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return panel;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string copy)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = copy;
            text.color = Color.white;
            text.raycastTarget = false;
            text.font = TMP_Settings.defaultFontAsset;
            return text;
        }

        private static void Stretch(RectTransform rectTransform, float inset)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(inset, inset);
            rectTransform.offsetMax = new Vector2(-inset, -inset);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = Path.Combine(parent, child).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(fullPath))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
