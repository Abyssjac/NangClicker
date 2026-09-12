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
/// Creates or repairs TasteSystem assets and scene bindings through Unity's AssetDatabase and
/// serialized APIs. It never hand-edits Unity YAML; the explicit menu is also safe to rerun.
/// </summary>
internal static class TasteSystemSetup
{
    private const string SystemRoot = "Assets/A_Systems/TasteSystem";
    private const string MaterialFolder = SystemRoot + "/Materials";
    private const string PrefabFolder = SystemRoot + "/Prefabs";
    private const string CounterPrefabPath = PrefabFolder + "/TasteSpiceCounter.prefab";
    private const string ModifierPath = "Assets/A_Systems/ScoreSystem/AllSOs/ScoreModifierSOs/Main/ScoreModifierPP_TasteIncomeMultiplier.asset";
    private const string ModifierDatabasePath = "Assets/A_Systems/ScoreSystem/AllSOs/ScoreModifierSOs/ScoreModifierDB_main.asset";
    private const string InitPrefabPath = "Assets/A_Systems/AInitGameObject Variant.prefab";
    private const string MainScenePath = "Assets/Scenes/S_MainGame.unity";

    [MenuItem("Tools/Nang Clicker/Setup Taste System")]
    private static void SetupFromMenu()
    {
        EnsureFolder(SystemRoot);
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);

        ScoreModifierProperty modifier = EnsureTasteIncomeMultiplier();
        RegisterAndCollectScoreModifierDatabase();
        GameObject counterPrefab = EnsureCounterPrefab();
        InstallSceneReferences(counterPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[{nameof(TasteSystemSetup)}] Installed {modifier.name}, TasteSpiceCounter prefab, and S_MainGame taste references.");
    }

    private static ScoreModifierProperty EnsureTasteIncomeMultiplier()
    {
        ScoreModifierProperty modifier = AssetDatabase.LoadAssetAtPath<ScoreModifierProperty>(ModifierPath);
        if (modifier == null)
        {
            modifier = ScriptableObject.CreateInstance<ScoreModifierProperty>();
            AssetDatabase.CreateAsset(modifier, ModifierPath);
        }

        SerializedObject serializedModifier = new(modifier);
        serializedModifier.FindProperty("enumKey").intValue = (int)Key_ScoreModifierPP.TasteIncomeMultiplier;
        serializedModifier.FindProperty("stringKey").stringValue = string.Empty;
        serializedModifier.FindProperty("modifierType").enumValueIndex = (int)ScoreModifierType.IncomeMultiplier;
        serializedModifier.FindProperty("amount").doubleValue = 5d;
        serializedModifier.FindProperty("defaultDuration").floatValue = 0f;
        serializedModifier.FindProperty("upgradePrice").doubleValue = 0d;
        serializedModifier.FindProperty("displayName").stringValue = "Taste";
        serializedModifier.FindProperty("icon").objectReferenceValue = null;
        serializedModifier.FindProperty("description").stringValue = "Runtime-only x5 Income Multiplier while the selected spices exactly match customer preference.";
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

    private static GameObject EnsureCounterPrefab()
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CounterPrefabPath);
        if (existingPrefab != null)
            return existingPrefab;

        Material sesameMaterial = EnsureMaterial("MAT_TasteSesame", new Color(0.9f, 0.75f, 0.36f, 1f));
        Material cheeseMaterial = EnsureMaterial("MAT_TasteCheese", new Color(1f, 0.76f, 0.1f, 1f));
        Material chiliMaterial = EnsureMaterial("MAT_TasteChiliPowder", new Color(0.83f, 0.08f, 0.03f, 1f));
        Material cuminMaterial = EnsureMaterial("MAT_TasteCumin", new Color(0.28f, 0.17f, 0.05f, 1f));

        GameObject root = new("TasteSpiceCounter");
        try
        {
            CreateCounterSpice("SesameSpice", root.transform, SpiceType.Sesame, new Vector3(-0.3f, 1.25f, -0.1f), sesameMaterial);
            CreateCounterSpice("CheeseSpice", root.transform, SpiceType.Cheese, new Vector3(-0.1f, 1.25f, -0.1f), cheeseMaterial);
            CreateCounterSpice("ChiliPowderSpice", root.transform, SpiceType.ChiliPowder, new Vector3(0.1f, 1.25f, -0.1f), chiliMaterial);
            CreateCounterSpice("CuminSpice", root.transform, SpiceType.Cumin, new Vector3(0.3f, 1.25f, -0.1f), cuminMaterial);

            return PrefabUtility.SaveAsPrefabAsset(root, CounterPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateCounterSpice(
        string name,
        Transform parent,
        SpiceType spice,
        Vector3 localPosition,
        Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = new Vector3(0.12f, 0.24f, 0.42f);
        cube.GetComponent<Renderer>().sharedMaterial = material;

        TasteSpiceInteractable interactable = cube.AddComponent<TasteSpiceInteractable>();
        SerializedObject serializedInteractable = new(interactable);
        serializedInteractable.FindProperty("spiceType").enumValueIndex = (int)spice;
        serializedInteractable.FindProperty("hitCollider").objectReferenceValue = cube.GetComponent<Collider>();
        serializedInteractable.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void InstallSceneReferences(GameObject counterPrefab)
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

            TasteManager tasteManager = scoreManager.GetComponent<TasteManager>();
            if (tasteManager == null)
                tasteManager = scoreManager.gameObject.AddComponent<TasteManager>();

            Camera rightPanelCamera = FindInScene<Camera>(scene, camera => camera.name == "RightPanelCamera");
            if (rightPanelCamera == null)
                throw new InvalidOperationException("S_MainGame has no RightPanelCamera.");

            Transform counter = FindTransformInScene(scene, "Counter");
            if (counter == null)
                throw new InvalidOperationException("S_MainGame has no Counter.");

            Transform counterInstanceTransform = counter.Find("TasteSpiceCounter");
            GameObject counterInstance = counterInstanceTransform != null
                ? counterInstanceTransform.gameObject
                : (GameObject)PrefabUtility.InstantiatePrefab(counterPrefab, counter);
            counterInstance.name = "TasteSpiceCounter";
            counterInstance.transform.localPosition = Vector3.zero;
            counterInstance.transform.localRotation = Quaternion.identity;
            counterInstance.transform.localScale = Vector3.one;

            TasteSpiceInteractable[] interactables = counterInstance.GetComponentsInChildren<TasteSpiceInteractable>(true);
            foreach (TasteSpiceInteractable interactable in interactables)
            {
                SerializedObject serializedInteractable = new(interactable);
                serializedInteractable.FindProperty("tasteManager").objectReferenceValue = tasteManager;
                serializedInteractable.FindProperty("interactionCamera").objectReferenceValue = rightPanelCamera;
                serializedInteractable.ApplyModifiedPropertiesWithoutUndo();
            }

            Transform nangPreview = FindTransformInScene(scene, "NangPreview");
            if (nangPreview == null)
                throw new InvalidOperationException("S_MainGame has no NangPreview.");

            Material cheeseMaterial = EnsureMaterial("MAT_TasteCheese", new Color(1f, 0.76f, 0.1f, 1f));
            Material chiliMaterial = EnsureMaterial("MAT_TasteChiliPowder", new Color(0.83f, 0.08f, 0.03f, 1f));
            Material cuminMaterial = EnsureMaterial("MAT_TasteCumin", new Color(0.28f, 0.17f, 0.05f, 1f));

            Transform sesameRoot = nangPreview.Find("SesameRoot");
            if (sesameRoot == null)
                throw new InvalidOperationException("NangPreview has no existing SesameRoot.");

            // Topping visuals must never become the first raycast hit. NangBehaviour owns the
            // one interactive Collider on NangPreview, while Counter keeps the spice buttons.
            RemoveToppingColliders(sesameRoot);

            Transform cheeseRoot = EnsureToppingRoot(nangPreview, "CheeseRoot", cheeseMaterial, PrimitiveType.Cube,
                new[] { new Vector3(-0.25f, 0.99f, 0.12f), new Vector3(0.14f, 0.99f, -0.2f), new Vector3(0.3f, 0.99f, 0.22f) },
                new Vector3(0.13f, 0.07f, 0.13f));
            Transform chiliRoot = EnsureToppingRoot(nangPreview, "ChiliPowderRoot", chiliMaterial, PrimitiveType.Cylinder,
                new[] { new Vector3(-0.22f, 0.99f, -0.16f), new Vector3(0.02f, 0.99f, 0.22f), new Vector3(0.27f, 0.99f, -0.08f) },
                new Vector3(0.1f, 0.025f, 0.1f));
            Transform cuminRoot = EnsureToppingRoot(nangPreview, "CuminRoot", cuminMaterial, PrimitiveType.Cube,
                new[] { new Vector3(-0.3f, 0.99f, -0.1f), new Vector3(0.0f, 0.99f, -0.23f), new Vector3(0.24f, 0.99f, 0.18f) },
                new Vector3(0.06f, 0.04f, 0.16f));

            sesameRoot.gameObject.SetActive(false);
            cheeseRoot.gameObject.SetActive(false);
            chiliRoot.gameObject.SetActive(false);
            cuminRoot.gameObject.SetActive(false);

            TasteVisualController visualController = nangPreview.GetComponent<TasteVisualController>();
            if (visualController == null)
                visualController = nangPreview.gameObject.AddComponent<TasteVisualController>();

            SerializedObject serializedVisualController = new(visualController);
            serializedVisualController.FindProperty("tasteManager").objectReferenceValue = tasteManager;
            serializedVisualController.FindProperty("sesameRoot").objectReferenceValue = sesameRoot.gameObject;
            serializedVisualController.FindProperty("cheeseRoot").objectReferenceValue = cheeseRoot.gameObject;
            serializedVisualController.FindProperty("chiliPowderRoot").objectReferenceValue = chiliRoot.gameObject;
            serializedVisualController.FindProperty("cuminRoot").objectReferenceValue = cuminRoot.gameObject;
            serializedVisualController.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (openedHere && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Transform EnsureToppingRoot(
        Transform nangPreview,
        string rootName,
        Material material,
        PrimitiveType primitiveType,
        IReadOnlyList<Vector3> positions,
        Vector3 pieceScale)
    {
        Transform existingRoot = nangPreview.Find(rootName);
        if (existingRoot != null)
        {
            RemoveToppingColliders(existingRoot);
            return existingRoot;
        }

        GameObject root = new(rootName);
        root.transform.SetParent(nangPreview, false);
        for (int index = 0; index < positions.Count; index++)
        {
            GameObject piece = GameObject.CreatePrimitive(primitiveType);
            piece.name = rootName + "Piece" + (index + 1);
            piece.transform.SetParent(root.transform, false);
            piece.transform.localPosition = positions[index];
            piece.transform.localScale = pieceScale;
            piece.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(piece.GetComponent<Collider>());
        }

        return root.transform;
    }

    private static void RemoveToppingColliders(Transform toppingRoot)
    {
        foreach (Collider collider in toppingRoot.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);
    }

    private static Material EnsureMaterial(string materialName, Color color)
    {
        string path = MaterialFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("No supported Lit shader was found for TasteSystem materials.");

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
