using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MonaLisaGame.Editor
{
    /// <summary>
    /// Creates the independently playable prototype scene and its two presentation prefabs.
    /// It intentionally leaves the pre-existing scene singleton untouched.
    /// </summary>
    public static class MonaLisaSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/S_MonaLisa.unity";
        private const string SystemRootName = "MonaLisaGame";
        private const string AssetRoot = "Assets/M_Systems";
        private const string MaterialRoot = AssetRoot + "/Materials";
        private const string PrefabRoot = AssetRoot + "/Prefabs";
        private const string PrototypeAssetRoot = AssetRoot + "/PrototypeAssets";
        private const string PhotoPointPrefabPath = PrefabRoot + "/PhotoPoint.prefab";
        private const string TouristPrefabPath = PrefabRoot + "/Tourist.prefab";
        private const string CircleSpritePath = PrototypeAssetRoot + "/PrototypeCircle.asset";
        private const string SquareSpritePath = PrototypeAssetRoot + "/PrototypeSquare.asset";
        private const string PupilMaterialPath = MaterialRoot + "/Mat_MonaLisaPupilHighlight.mat";
        private const string PupilShaderName = "MonaLisa/Pupil Highlight";

        [MenuItem("Mona Lisa/Build or Update Prototype Scene")]
        public static void BuildOrUpdatePrototypeScene()
        {
            EnsureFolder(AssetRoot);
            EnsureFolder(MaterialRoot);
            EnsureFolder(PrefabRoot);
            EnsureFolder(PrototypeAssetRoot);

            Sprite circleSprite = GetOrCreateSprite(CircleSpritePath, true);
            Sprite squareSprite = GetOrCreateSprite(SquareSpritePath, false);
            Material pupilMaterial = GetOrCreatePupilMaterial();
            PhotoPointView photoPointPrefab = BuildPhotoPointPrefab(circleSprite, squareSprite);
            TouristBehaviour touristPrefab = BuildTouristPrefab(circleSprite, squareSprite);

            Scene scene = OpenOrCreateScene();
            GameObject oldRoot = GameObject.Find(SystemRootName);
            if (oldRoot != null)
                Object.DestroyImmediate(oldRoot);

            GameObject gameRoot = new(SystemRootName);
            BuildCamera(gameRoot.transform);

            GazeArea gazeArea = new(new Vector2(-7f, -5.3f), new Vector2(7f, 0.6f));
            BuildWorldPresentation(gameRoot.transform, squareSprite, circleSprite, pupilMaterial, gazeArea,
                out GazeFocusController gazeFocus,
                out PhotoPointManager photoPointManager,
                out Transform photoPointRoot,
                out Transform touristRoot,
                out Transform leftOffscreenPoint,
                out Transform rightOffscreenPoint);

            photoPointManager.ConfigurePrototype(
                gazeFocus,
                photoPointPrefab,
                photoPointRoot,
                configuredHitRadius: 0.72f,
                configuredPatienceDuration: 6f,
                configuredFocusDuration: 1.1f);

            TouristManager touristManager = gameRoot.AddComponent<TouristManager>();
            touristManager.ConfigurePrototype(
                touristPrefab,
                touristRoot,
                gazeFocus,
                photoPointManager,
                leftOffscreenPoint,
                rightOffscreenPoint);

            BuildCanvas(gameRoot.transform, photoPointManager);
            EnsureEventSystem();
            AddSceneToBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = gameRoot;
            Debug.Log("[MonaLisaSceneBuilder] Prototype scene, prefabs, and build-settings entry are ready.");
        }

        private static Scene OpenOrCreateScene()
        {
            if (File.Exists(ScenePath))
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Scene createdScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(createdScene, ScenePath);
            return createdScene;
        }

        private static PhotoPointView BuildPhotoPointPrefab(Sprite circleSprite, Sprite squareSprite)
        {
            GameObject root = new("PhotoPoint");
            PhotoPointView view = root.AddComponent<PhotoPointView>();

            SpriteRenderer range = CreateSpriteChild(
                root.transform,
                "Range",
                circleSprite,
                new Color(1f, 0.77f, 0.18f, 0.28f),
                Vector3.zero,
                Vector3.one,
                12);

            CreateSpriteChild(root.transform, "PatienceBackground", squareSprite, new Color(0.08f, 0.08f, 0.1f, 0.9f), new Vector3(0f, 0.94f, 0f), new Vector3(1.2f, 0.1f, 1f), 13);
            SpriteRenderer patienceFill = CreateSpriteChild(root.transform, "PatienceFill", squareSprite, new Color(0.94f, 0.25f, 0.24f, 1f), new Vector3(0f, 0.94f, -0.01f), new Vector3(1.2f, 0.1f, 1f), 14);
            CreateSpriteChild(root.transform, "FocusBackground", squareSprite, new Color(0.08f, 0.08f, 0.1f, 0.9f), new Vector3(0f, 1.14f, 0f), new Vector3(1.2f, 0.1f, 1f), 13);
            SpriteRenderer focusFill = CreateSpriteChild(root.transform, "FocusFill", squareSprite, new Color(0.16f, 0.9f, 0.52f, 1f), new Vector3(-0.6f, 1.14f, -0.01f), Vector3.zero, 14);

            view.ConfigurePrototype(range, patienceFill, focusFill, 1.2f, 0.1f);
            GameObject prefabObject = PrefabUtility.SaveAsPrefabAsset(root, PhotoPointPrefabPath);
            Object.DestroyImmediate(root);
            return prefabObject.GetComponent<PhotoPointView>();
        }

        private static TouristBehaviour BuildTouristPrefab(Sprite circleSprite, Sprite squareSprite)
        {
            GameObject root = new("Tourist");
            SpriteRenderer body = root.AddComponent<SpriteRenderer>();
            body.sprite = squareSprite;
            body.color = new Color(0.32f, 0.6f, 0.89f, 1f);
            body.sortingOrder = 8;
            body.transform.localScale = new Vector3(0.58f, 1.18f, 1f);

            SpriteRenderer head = CreateSpriteChild(root.transform, "Head", circleSprite, new Color(0.98f, 0.76f, 0.57f, 1f), new Vector3(0f, 0.75f, 0f), new Vector3(0.55f, 0.55f, 1f), 9);
            head.sortingOrder = 9;
            Transform photoPivot = new GameObject("PhotoPivot").transform;
            photoPivot.SetParent(root.transform, false);
            photoPivot.localPosition = new Vector3(0f, -0.66f, 0f);

            TouristBehaviour tourist = root.AddComponent<TouristBehaviour>();
            tourist.ConfigurePrototype(photoPivot);
            GameObject prefabObject = PrefabUtility.SaveAsPrefabAsset(root, TouristPrefabPath);
            Object.DestroyImmediate(root);
            return prefabObject.GetComponent<TouristBehaviour>();
        }

        private static void BuildCamera(Transform parent)
        {
            GameObject cameraObject = new("MonaLisaCamera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.depth = 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.105f, 0.12f, 0.14f, 1f);

            GameObject lightObject = new("MonaLisaDirectionalLight");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.35f;
        }

        private static void BuildWorldPresentation(
            Transform parent,
            Sprite squareSprite,
            Sprite circleSprite,
            Material pupilMaterial,
            GazeArea gazeArea,
            out GazeFocusController gazeFocus,
            out PhotoPointManager photoPointManager,
            out Transform photoPointRoot,
            out Transform touristRoot,
            out Transform leftOffscreenPoint,
            out Transform rightOffscreenPoint)
        {
            CreateSpriteChild(parent, "GameAreaBackdrop", squareSprite, new Color(0.16f, 0.19f, 0.21f, 1f), new Vector3(0f, -2.35f, 0f), new Vector3(14.6f, 6.1f, 1f), 0);
            CreateSpriteChild(parent, "GameAreaInner", squareSprite, new Color(0.22f, 0.25f, 0.26f, 1f), new Vector3(0f, -2.35f, -0.01f), new Vector3(14.15f, 5.65f, 1f), 1);

            Transform portraitRoot = new GameObject("MonaLisaPortrait_Placeholder").transform;
            portraitRoot.SetParent(parent, false);
            portraitRoot.localPosition = new Vector3(0f, 3.25f, 0f);
            CreateSpriteChild(portraitRoot, "Frame", squareSprite, new Color(0.72f, 0.49f, 0.2f, 1f), Vector3.zero, new Vector3(4.45f, 3.85f, 1f), 2);
            CreateSpriteChild(portraitRoot, "ArtworkPlaceholder", squareSprite, new Color(0.32f, 0.42f, 0.32f, 1f), new Vector3(0f, 0f, -0.01f), new Vector3(4.05f, 3.45f, 1f), 3);
            CreateSpriteChild(portraitRoot, "FacePlaceholder", circleSprite, new Color(0.76f, 0.62f, 0.43f, 1f), new Vector3(0f, 0.18f, -0.02f), new Vector3(1.85f, 2.3f, 1f), 4);

            SpriteRenderer leftPupilRenderer = CreateSpriteChild(portraitRoot, "LeftPupil", circleSprite, Color.white, new Vector3(-0.48f, 0.42f, -0.04f), new Vector3(0.26f, 0.26f, 1f), 6);
            SpriteRenderer rightPupilRenderer = CreateSpriteChild(portraitRoot, "RightPupil", circleSprite, Color.white, new Vector3(0.48f, 0.42f, -0.04f), new Vector3(0.26f, 0.26f, 1f), 6);
            leftPupilRenderer.sharedMaterial = pupilMaterial;
            rightPupilRenderer.sharedMaterial = pupilMaterial;
            Transform leftPupil = leftPupilRenderer.transform;
            Transform rightPupil = rightPupilRenderer.transform;

            GameObject focusObject = new("GazeFocus");
            focusObject.transform.SetParent(parent, false);
            focusObject.transform.position = new Vector3(0f, -2.35f, -0.2f);
            SpriteRenderer focusRenderer = focusObject.AddComponent<SpriteRenderer>();
            focusRenderer.sprite = circleSprite;
            focusRenderer.color = new Color(1f, 0.76f, 0.12f, 0.96f);
            focusRenderer.sortingOrder = 20;
            focusObject.transform.localScale = new Vector3(0.32f, 0.32f, 1f);
            WASDGazeInputProvider keyboardInput = focusObject.AddComponent<WASDGazeInputProvider>();
            gazeFocus = focusObject.AddComponent<GazeFocusController>();
            gazeFocus.ConfigurePrototype(keyboardInput, gazeArea, 6f);

            MonaLisaEyeController eyeController = portraitRoot.gameObject.AddComponent<MonaLisaEyeController>();
            eyeController.ConfigurePrototype(gazeFocus, portraitRoot, leftPupil, rightPupil, 0.11f);

            photoPointRoot = new GameObject("PhotoPoints").transform;
            photoPointRoot.SetParent(parent, false);
            touristRoot = new GameObject("Tourists").transform;
            touristRoot.SetParent(parent, false);

            GameObject managerObject = new("PhotoPointManager");
            managerObject.transform.SetParent(parent, false);
            photoPointManager = managerObject.AddComponent<PhotoPointManager>();

            leftOffscreenPoint = new GameObject("LeftOffscreenPoint").transform;
            leftOffscreenPoint.SetParent(parent, false);
            leftOffscreenPoint.localPosition = new Vector3(-8.6f, 0f, 0f);

            rightOffscreenPoint = new GameObject("RightOffscreenPoint").transform;
            rightOffscreenPoint.SetParent(parent, false);
            rightOffscreenPoint.localPosition = new Vector3(8.6f, 0f, 0f);
        }

        private static void BuildCanvas(Transform parent, PhotoPointManager photoPointManager)
        {
            GameObject canvasObject = new("MonaLisaCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024f, 768f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform visualBand = CreateUiRect(canvasObject.transform, "VisualBand", new Vector2(0.2f, 0.54f), new Vector2(0.8f, 0.605f));
            Image bandImage = visualBand.gameObject.AddComponent<Image>();
            bandImage.color = new Color(0.03f, 0.035f, 0.045f, 0.9f);
            TextMeshProUGUI visualLabel = CreateUiText(visualBand, "VisualLabel", "VISUAL", TextAlignmentOptions.Center, new Color(0.95f, 0.82f, 0.48f, 1f));
            Stretch(visualLabel.rectTransform);

            TextMeshProUGUI successText = CreateUiText(
                canvasObject.transform,
                "SuccessText",
                "SUCCESS  0",
                TextAlignmentOptions.Left,
                new Color(0.3f, 1f, 0.62f, 1f));
            SetAnchoredRect(successText.rectTransform, new Vector2(0.035f, 0.91f), new Vector2(250f, 44f));

            TextMeshProUGUI failureText = CreateUiText(
                canvasObject.transform,
                "FailureText",
                "FAIL  0",
                TextAlignmentOptions.Right,
                new Color(1f, 0.37f, 0.34f, 1f));
            SetAnchoredRect(failureText.rectTransform, new Vector2(0.965f, 0.91f), new Vector2(250f, 44f));

            PhotoResultCounterUI counterUi = canvasObject.AddComponent<PhotoResultCounterUI>();
            counterUi.ConfigurePrototype(photoPointManager, successText, failureText);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            GameObject eventSystem = new("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.name = "MonaLisaEventSystem";
        }

        private static SpriteRenderer CreateSpriteChild(
            Transform parent,
            string objectName,
            Sprite sprite,
            Color color,
            Vector3 localPosition,
            Vector3 localScale,
            int sortingOrder)
        {
            GameObject child = new(objectName);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = "FrotProps";
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static Material GetOrCreatePupilMaterial()
        {
            Shader pupilShader = Shader.Find(PupilShaderName);
            if (pupilShader == null)
            {
                Debug.LogError($"[{nameof(MonaLisaSceneBuilder)}] Could not find shader '{PupilShaderName}'.", null);
                return null;
            }

            Material pupilMaterial = AssetDatabase.LoadAssetAtPath<Material>(PupilMaterialPath);
            if (pupilMaterial == null)
            {
                pupilMaterial = new Material(pupilShader);
                AssetDatabase.CreateAsset(pupilMaterial, PupilMaterialPath);
            }
            else if (pupilMaterial.shader != pupilShader)
            {
                pupilMaterial.shader = pupilShader;
            }

            pupilMaterial.SetColor("_CoreColor", new Color(0.012f, 0.018f, 0.055f, 1f));
            pupilMaterial.SetColor("_IrisColor", new Color(0.04f, 0.28f, 0.54f, 1f));
            pupilMaterial.SetColor("_RimColor", new Color(0.02f, 1.15f, 1.6f, 1f));
            pupilMaterial.SetColor("_GlintColor", new Color(0.82f, 0.98f, 1f, 1f));
            pupilMaterial.SetFloat("_PulseSpeed", 3.2f);
            pupilMaterial.SetFloat("_PulseStrength", 0.26f);
            EditorUtility.SetDirty(pupilMaterial);
            return pupilMaterial;
        }

        private static RectTransform CreateUiRect(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject child = new(objectName, typeof(RectTransform));
            RectTransform rectTransform = child.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return rectTransform;
        }

        private static TextMeshProUGUI CreateUiText(
            Transform parent,
            string objectName,
            string content,
            TextAlignmentOptions alignment,
            Color color)
        {
            GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.alignment = alignment;
            text.color = color;
            text.fontSize = 26f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;
            return text;
        }

        private static void SetAnchoredRect(RectTransform rectTransform, Vector2 anchor, Vector2 size)
        {
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = new Vector2(anchor.x < 0.5f ? 0f : 1f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = size;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static Sprite GetOrCreateSprite(string path, bool isCircle)
        {
            Object[] existingAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < existingAssets.Length; i++)
            {
                if (existingAssets[i] is Sprite existingSprite)
                    return existingSprite;
            }

            const int textureSize = 64;
            Texture2D texture = new(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = Path.GetFileNameWithoutExtension(path) + "Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color[] pixels = new Color[textureSize * textureSize];
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float alpha = 1f;
                    if (isCircle)
                    {
                        Vector2 fromCenter = new((x + 0.5f) / textureSize - 0.5f, (y + 0.5f) / textureSize - 0.5f);
                        alpha = Mathf.Clamp01((0.5f - fromCenter.magnitude) * textureSize * 0.5f);
                    }

                    pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, path);
            Sprite createdSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
            createdSprite.name = Path.GetFileNameWithoutExtension(path) + "Sprite";
            AssetDatabase.AddObjectToAsset(createdSprite, texture);
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssets();
            return createdSprite;
        }

        private static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != ScenePath)
                    continue;

                scenes[i] = new EditorBuildSettingsScene(ScenePath, true);
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
