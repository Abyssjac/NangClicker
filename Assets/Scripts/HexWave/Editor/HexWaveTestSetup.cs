using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NangClicker.HexWave.Editor
{
    public static class HexWaveTestSetup
    {
        private const string PrefabFolder = "Assets/Prefabs/HexWave";
        private const string MeshPath = PrefabFolder + "/HexColumnMesh.asset";
        private const string MaterialPath = PrefabFolder + "/HexColumnMaterial.mat";
        private const string PrefabPath = PrefabFolder + "/HexCell.prefab";
        private const string EnemyStackPrefabPath = PrefabFolder + "/HexEnemyStack.prefab";
        private const string TestRootName = "Hex Wave Test";

        [MenuItem("Tools/Hex Wave/Create Test Setup In Active Scene")]
        public static void CreateTestSetup()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "HexWave");

            Mesh mesh = GetOrCreateMesh();
            Material material = GetOrCreateMaterial();
            HexCellView prefab = GetOrCreatePrefab(mesh, material);
            HexEnemyStackView enemyStackPrefab = GetOrCreateEnemyStackPrefab();

            GameObject existingRoot = GameObject.Find(TestRootName);
            if (existingRoot != null)
            {
                Selection.activeGameObject = existingRoot;
                Debug.Log($"[{nameof(HexWaveTestSetup)}] '{TestRootName}' already exists. Existing setup was left unchanged.", existingRoot);
                return;
            }

            GameObject root = new GameObject(TestRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Hex Wave Test Setup");

            HexWaveManager manager = root.AddComponent<HexWaveManager>();
            HexWaveRenderer waveRenderer = root.AddComponent<HexWaveRenderer>();
            HexWavePointerInput pointerInput = root.AddComponent<HexWavePointerInput>();
            HexEnemyManager enemyManager = root.AddComponent<HexEnemyManager>();
            HexEnemyVisualManager enemyVisualManager = root.AddComponent<HexEnemyVisualManager>();

            GameObject cellsObject = new GameObject("Cells");
            Undo.RegisterCreatedObjectUndo(cellsObject, "Create Hex Wave Cells Root");
            cellsObject.transform.SetParent(root.transform, false);

            const float spacing = 1.1f;
            manager.ConfigureAuthoring(prefab, cellsObject.transform, spacing);
            CreateHexagonGrid(manager, prefab, 5);
            manager.RebuildGrid();
            waveRenderer.Configure(manager, 1.25f);
            HexWaveEditorVisuals.RefreshBoundaryPreview(manager);

            Camera camera = GetOrCreateCamera(root.transform);
            GetOrCreateLight(root.transform);
            pointerInput.Configure(manager, camera, manager.DefaultImpulse, true);
            enemyManager.Configure(manager);
            enemyVisualManager.Configure(enemyManager, manager, camera, enemyStackPrefab);

            Selection.activeGameObject = root;
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[{nameof(HexWaveTestSetup)}] Created a radius-5 Hex Wave test grid. " +
                "Press Space or click a movable cell in Play Mode to add an impulse.",
                root);
        }

        private static void CreateHexagonGrid(HexWaveManager manager, HexCellView prefab, int radius)
        {
            for (int a = -radius; a <= radius; a++)
            {
                int minB = Mathf.Max(-radius, -a - radius);
                int maxB = Mathf.Min(radius, -a + radius);

                for (int b = minB; b <= maxB; b++)
                {
                    HexCoordinate coordinate = HexCoordinate.FromAxial(a, b);
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                        prefab.gameObject,
                        manager.CellsRoot);

                    instance.name = $"Hex {coordinate.A}_{coordinate.B}_{coordinate.C}";
                    HexCellView view = instance.GetComponent<HexCellView>();
                    view.SetCoordinate(coordinate);
                    view.transform.localPosition = coordinate.ToLocalPosition(manager.Spacing);
                    view.transform.localRotation = Quaternion.identity;
                    view.CaptureBaseLocalPosition();
                    EditorUtility.SetDirty(view);
                }
            }
        }

        private static HexCellView GetOrCreatePrefab(Mesh mesh, Material material)
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existingPrefab != null)
                return existingPrefab.GetComponent<HexCellView>();

            GameObject temporary = new GameObject("HexCell");
            MeshFilter meshFilter = temporary.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = temporary.AddComponent<MeshRenderer>();
            MeshCollider meshCollider = temporary.AddComponent<MeshCollider>();
            temporary.AddComponent<HexCellView>();

            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            meshCollider.sharedMesh = mesh;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, PrefabPath);
            Object.DestroyImmediate(temporary);
            return prefab.GetComponent<HexCellView>();
        }

        private static HexEnemyStackView GetOrCreateEnemyStackPrefab()
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyStackPrefabPath);
            if (existingPrefab != null)
                return existingPrefab.GetComponent<HexEnemyStackView>();

            GameObject temporary = new GameObject("HexEnemyStack");
            temporary.AddComponent<HexEnemyStackView>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, EnemyStackPrefabPath);
            Object.DestroyImmediate(temporary);
            return prefab.GetComponent<HexEnemyStackView>();
        }

        private static Mesh GetOrCreateMesh()
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null)
                return existing;

            const float radius = 0.6f;
            const float height = 1.5f;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, height, 0f));
            for (int i = 0; i < 6; i++)
                vertices.Add(RingPoint(i, radius, height));

            int bottomCenter = vertices.Count;
            vertices.Add(Vector3.zero);
            for (int i = 0; i < 6; i++)
                vertices.Add(RingPoint(i, radius, 0f));

            for (int i = 0; i < 6; i++)
            {
                int current = i + 1;
                int next = (i + 1) % 6 + 1;
                triangles.Add(topCenter);
                triangles.Add(next);
                triangles.Add(current);

                int bottomCurrent = bottomCenter + current;
                int bottomNext = bottomCenter + next;
                triangles.Add(bottomCenter);
                triangles.Add(bottomCurrent);
                triangles.Add(bottomNext);
            }

            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;
                int sideStart = vertices.Count;
                vertices.Add(RingPoint(i, radius, 0f));
                vertices.Add(RingPoint(next, radius, 0f));
                vertices.Add(RingPoint(next, radius, height));
                vertices.Add(RingPoint(i, radius, height));

                triangles.Add(sideStart);
                triangles.Add(sideStart + 2);
                triangles.Add(sideStart + 1);
                triangles.Add(sideStart);
                triangles.Add(sideStart + 3);
                triangles.Add(sideStart + 2);
            }

            Mesh mesh = new Mesh
            {
                name = "HexColumnMesh"
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return mesh;
        }

        private static Vector3 RingPoint(int index, float radius, float y)
        {
            float angle = index * 60f * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }

        private static Material GetOrCreateMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader)
            {
                name = "HexColumnMaterial"
            };

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", new Color(0.12f, 0.85f, 0.28f));
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", new Color(0.12f, 0.85f, 0.28f));
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.25f);

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static Camera GetOrCreateCamera(Transform parent)
        {
            Camera camera = Object.FindFirstObjectByType<Camera>();
            if (camera != null)
                return camera;

            GameObject cameraObject = new GameObject("Hex Wave Test Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create Hex Wave Test Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 12f, -12f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.5f, 0f));
            camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 50f;
            return camera;
        }

        private static void GetOrCreateLight(Transform parent)
        {
            Light existing = Object.FindFirstObjectByType<Light>();
            if (existing != null)
                return;

            GameObject lightObject = new GameObject("Hex Wave Test Light");
            Undo.RegisterCreatedObjectUndo(lightObject, "Create Hex Wave Test Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = Path.Combine(parent, child).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(fullPath))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
