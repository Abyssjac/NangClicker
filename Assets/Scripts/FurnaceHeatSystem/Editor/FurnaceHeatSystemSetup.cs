#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using JackyUtility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates or repairs the furnace heat content installation without hand-editing Unity YAML.
/// The explicit menu command remains available when a deliberate rebuild is needed.
/// </summary>
internal static class FurnaceHeatSystemSetup
{
    private const string SystemRoot = "Assets/A_Systems/FurnaceHeatSystem";
    private const string MaterialFolder = SystemRoot + "/Materials";
    private const string PrefabFolder = SystemRoot + "/Prefabs";
    private const string PrefabPath = PrefabFolder + "/FurnaceHeatPanel.prefab";
    private const string ModifierPath = "Assets/A_Systems/ScoreSystem/AllSOs/ScoreModifierSOs/Main/ScoreModifierPP_FurnaceHeatIncomeMultiplier.asset";
    private const string ModifierDatabasePath = "Assets/A_Systems/ScoreSystem/AllSOs/ScoreModifierSOs/ScoreModifierDB_main.asset";
    private const string InitPrefabPath = "Assets/A_Systems/AInitGameObject Variant.prefab";
    private const string MainScenePath = "Assets/Scenes/S_MainGame.unity";

    [MenuItem("Tools/Nang Clicker/Setup Furnace Heat")]
    private static void SetupFromMenu()
    {
        Setup(logSuccess: true);
    }

    private static void Setup(bool logSuccess)
    {
        EnsureFolder(SystemRoot);
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);

        ScoreModifierProperty heatModifier = EnsureHeatModifier();
        RegisterAndCollectScoreModifierDatabase();
        GameObject panelPrefab = EnsurePanelPrefab();
        InstallSceneReferences(panelPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (logSuccess)
            Debug.Log($"[{nameof(FurnaceHeatSystemSetup)}] Installed {heatModifier.name}, FurnaceHeatPanel prefab, and S_MainGame scene references.");
    }

    private static ScoreModifierProperty EnsureHeatModifier()
    {
        ScoreModifierProperty modifier = AssetDatabase.LoadAssetAtPath<ScoreModifierProperty>(ModifierPath);
        if (modifier == null)
        {
            modifier = ScriptableObject.CreateInstance<ScoreModifierProperty>();
            AssetDatabase.CreateAsset(modifier, ModifierPath);
        }

        SerializedObject serializedModifier = new(modifier);
        serializedModifier.FindProperty("enumKey").intValue = (int)Key_ScoreModifierPP.FurnaceHeatIncomeMultiplier;
        serializedModifier.FindProperty("stringKey").stringValue = string.Empty;
        serializedModifier.FindProperty("modifierType").enumValueIndex = (int)ScoreModifierType.IncomeMultiplier;
        serializedModifier.FindProperty("amount").doubleValue = 5d;
        serializedModifier.FindProperty("defaultDuration").floatValue = 0f;
        serializedModifier.FindProperty("upgradePrice").doubleValue = 0d;
        serializedModifier.FindProperty("displayName").stringValue = "Furnace Heat";
        serializedModifier.FindProperty("icon").objectReferenceValue = null;
        serializedModifier.FindProperty("description").stringValue = "Runtime-only income multiplier while the furnace heat pointer is in its profitable range.";
        serializedModifier.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(modifier);
        return modifier;
    }

    private static void RegisterAndCollectScoreModifierDatabase()
    {
        ScoreModifierDatabase database = AssetDatabase.LoadAssetAtPath<ScoreModifierDatabase>(ModifierDatabasePath);
        if (database == null)
            throw new InvalidOperationException($"Could not load ScoreModifier database at '{ModifierDatabasePath}'.");

        List<ScoreModifierProperty> entries = AssetDatabase
            .FindAssets("t:ScoreModifierProperty", new[] { "Assets/A_Systems/ScoreSystem/AllSOs/ScoreModifierSOs" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ScoreModifierProperty>)
            .Where(property => property != null)
            .OrderBy(property => (int)property.EnumKey)
            .ToList();

        SerializedObject serializedDatabase = new(database);
        SerializedProperty serializedEntries = serializedDatabase.FindProperty("entries");
        serializedEntries.arraySize = entries.Count;
        for (int index = 0; index < entries.Count; index++)
            serializedEntries.GetArrayElementAtIndex(index).objectReferenceValue = entries[index];

        serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(database);

        GameObject initRoot = PrefabUtility.LoadPrefabContents(InitPrefabPath);
        try
        {
            PropertyDatabaseManager databaseManager = initRoot.GetComponentInChildren<PropertyDatabaseManager>(true);
            if (databaseManager == null)
                throw new InvalidOperationException("AInitGameObject Variant has no PropertyDatabaseManager.");

            SerializedObject serializedManager = new(databaseManager);
            SerializedProperty allDatabases = serializedManager.FindProperty("allDatabases");
            bool alreadyRegistered = false;
            for (int index = 0; index < allDatabases.arraySize; index++)
            {
                if (allDatabases.GetArrayElementAtIndex(index).objectReferenceValue == database)
                {
                    alreadyRegistered = true;
                    break;
                }
            }

            if (!alreadyRegistered)
            {
                int nextIndex = allDatabases.arraySize;
                allDatabases.InsertArrayElementAtIndex(nextIndex);
                allDatabases.GetArrayElementAtIndex(nextIndex).objectReferenceValue = database;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(initRoot, InitPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(initRoot);
        }
    }

    private static GameObject EnsurePanelPrefab()
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab != null)
            return existingPrefab;

        Material backdropMaterial = EnsureMaterial("MAT_FurnaceHeatBackdrop", new Color(0.14f, 0.045f, 0.018f, 1f));
        Material trackMaterial = EnsureMaterial("MAT_FurnaceHeatTrack", new Color(0.12f, 0.07f, 0.025f, 1f));
        Material profitableMaterial = EnsureMaterial("MAT_FurnaceHeatProfitableRange", new Color(0.95f, 0.34f, 0.045f, 1f));
        Material pointerMaterial = EnsureMaterial("MAT_FurnaceHeatPointer", new Color(0.65f, 0.25f, 0.08f, 1f));
        Material buttonMaterial = EnsureMaterial("MAT_FurnaceHeatManualSetButton", new Color(0.32f, 0.12f, 0.05f, 1f));

        GameObject root = new("FurnaceHeatPanel");
        try
        {
            GameObject backdrop = CreateCube("Backdrop", root.transform, new Vector3(0f, 0f, 0f), new Vector3(5.4f, 1.7f, 0.15f), backdropMaterial, false);
            GameObject track = CreateCube("HeatBarTrack", root.transform, new Vector3(0f, 0.28f, -0.16f), new Vector3(4.5f, 0.2f, 0.1f), trackMaterial, false);
            GameObject range = CreateCube("ProfitableRange", root.transform, new Vector3(1.9f, 0.28f, -0.26f), new Vector3(0.45f, 0.3f, 0.08f), profitableMaterial, false);
            GameObject pointer = CreateCube("HeatPointer", root.transform, new Vector3(-2.1f, 0.28f, -0.36f), new Vector3(0.12f, 0.68f, 0.12f), pointerMaterial, false);
            GameObject button = CreateCube("ManualSetButton", root.transform, new Vector3(0f, -0.55f, -0.28f), new Vector3(2.4f, 0.45f, 0.18f), buttonMaterial, true);

            Transform left = CreateMarker("TrackLeft", root.transform, new Vector3(-2.1f, 0.28f, -0.36f));
            Transform right = CreateMarker("TrackRight", root.transform, new Vector3(2.1f, 0.28f, -0.36f));

            FurnaceHeatView view = root.AddComponent<FurnaceHeatView>();
            FurnaceHeatInteractable interactable = button.AddComponent<FurnaceHeatInteractable>();

            SerializedObject serializedView = new(view);
            serializedView.FindProperty("trackLeft").objectReferenceValue = left;
            serializedView.FindProperty("trackRight").objectReferenceValue = right;
            serializedView.FindProperty("heatPointer").objectReferenceValue = pointer.transform;
            serializedView.FindProperty("profitableRangeVisual").objectReferenceValue = range.transform;
            serializedView.FindProperty("heatPointerRenderer").objectReferenceValue = pointer.GetComponent<Renderer>();
            serializedView.FindProperty("manualSetButtonRenderer").objectReferenceValue = button.GetComponent<Renderer>();
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return savedPrefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void InstallSceneReferences(GameObject panelPrefab)
    {
        Scene scene = SceneManager.GetSceneByPath(MainScenePath);
        bool openedHere = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        try
        {
            ScoreManager scoreManager = FindInScene<ScoreManager>(scene);
            if (scoreManager == null)
                throw new InvalidOperationException("S_MainGame has no ScoreManager.");

            FurnaceHeatManager heatManager = scoreManager.GetComponent<FurnaceHeatManager>();
            if (heatManager == null)
                heatManager = scoreManager.gameObject.AddComponent<FurnaceHeatManager>();

            Camera rightPanelCamera = FindInScene<Camera>(scene, camera => camera.name == "RightPanelCamera");
            if (rightPanelCamera == null)
                throw new InvalidOperationException("S_MainGame has no RightPanelCamera.");

            Transform panelWorld = FindTransformInScene(scene, "PanelWorld");
            if (panelWorld == null)
                throw new InvalidOperationException("S_MainGame has no PanelWorld root.");

            Transform existingPanel = panelWorld.Find("FurnaceHeatPanel");
            GameObject panelInstance = existingPanel != null
                ? existingPanel.gameObject
                : (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, panelWorld);

            panelInstance.name = "FurnaceHeatPanel";
            panelInstance.transform.localPosition = new Vector3(0f, -2.2f, 20f);
            panelInstance.transform.localRotation = Quaternion.identity;
            panelInstance.transform.localScale = Vector3.one;

            FurnaceHeatInteractable interactable = panelInstance.GetComponentInChildren<FurnaceHeatInteractable>(true);
            if (interactable == null)
                throw new InvalidOperationException("FurnaceHeatPanel prefab has no FurnaceHeatInteractable.");

            SerializedObject serializedInteractable = new(interactable);
            serializedInteractable.FindProperty("furnaceHeatManager").objectReferenceValue = heatManager;
            serializedInteractable.FindProperty("interactionCamera").objectReferenceValue = rightPanelCamera;
            serializedInteractable.ApplyModifiedPropertiesWithoutUndo();

            FurnaceHeatView view = panelInstance.GetComponentInChildren<FurnaceHeatView>(true);
            if (view != null)
            {
                SerializedObject serializedView = new(view);
                serializedView.FindProperty("furnaceHeatManager").objectReferenceValue = heatManager;
                serializedView.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (openedHere && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Material EnsureMaterial(string materialName, Color color)
    {
        string path = MaterialFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("No supported Lit shader was found for FurnaceHeat materials.");

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateCube(
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material,
        bool keepCollider)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;
        cube.GetComponent<Renderer>().sharedMaterial = material;

        if (!keepCollider)
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());

        return cube;
    }

    private static Transform CreateMarker(string name, Transform parent, Vector3 localPosition)
    {
        GameObject marker = new(name);
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        return marker.transform;
    }

    private static T FindInScene<T>(Scene scene, Func<T, bool> predicate = null) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                if (predicate == null || predicate(component))
                    return component;
            }
        }

        return null;
    }

    private static Transform FindTransformInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                    return transform;
            }
        }

        return null;
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string parentPath = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        string folderName = System.IO.Path.GetFileName(assetPath);
        if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(folderName))
            throw new InvalidOperationException($"Invalid asset folder path '{assetPath}'.");

        EnsureFolder(parentPath);
        AssetDatabase.CreateFolder(parentPath, folderName);
    }
}
#endif
