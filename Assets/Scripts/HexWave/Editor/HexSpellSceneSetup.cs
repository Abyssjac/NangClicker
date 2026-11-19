using System.Collections.Generic;
using System.Reflection;
using JackyUtility;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NangClicker.HexWave.Editor
{
    /// <summary>Explicit, repeatable installation command. Never runs automatically or overwrites authored spells.</summary>
    public static class HexSpellSceneSetup
    {
        public const string Folder = "Assets/A_Systems/HexSpellSystem/AllSOs/HexSpellSOs";
        public const string DatabasePath = Folder + "/HexSpellDB_main.asset";
        private const string InitPath = "Assets/A_Systems/AInitGameObject Variant.prefab";

        [MenuItem("Tools/Hex Wave/Install Spell Test In Active Scene")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before installing.");
            var map = Object.FindFirstObjectByType<HexWaveManager>();
            if (map == null) throw new System.InvalidOperationException("The active scene needs a HexWaveManager.");
            EnsureFolder(Folder);
            CreateSample(Key_HexSpellPP.Line, "Line", "Line Strike", new[]
            { HexCoordinate.Origin, HexCoordinate.FromAxial(1, 0), HexCoordinate.FromAxial(2, 0), HexCoordinate.FromAxial(3, 0) });
            CreateSample(Key_HexSpellPP.Triangle, "Triangle", "Triangle Strike", new[]
            { HexCoordinate.Origin, HexCoordinate.FromAxial(-1, 1), HexCoordinate.FromAxial(0, 1) });
            var database = AssetDatabase.LoadAssetAtPath<HexSpellDatabase>(DatabasePath);
            if (database == null) { database = ScriptableObject.CreateInstance<HexSpellDatabase>(); AssetDatabase.CreateAsset(database, DatabasePath); }
            var dbData = new SerializedObject(database);
            dbData.FindProperty("scanFolder").stringValue = Folder.Substring("Assets/".Length);
            dbData.ApplyModifiedPropertiesWithoutUndo();
            typeof(HexSpellDatabase).GetMethod("EditorCollectFromFolder", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(database, null);
            RegisterDatabase(database);

            EnsureFolder("Assets/Prefabs/HexWave/SpellMaterials");
            const string overlayPath = "Assets/Prefabs/HexWave/SpellMaterials/M_SpellOverlay.mat";
            var overlay = AssetDatabase.LoadAssetAtPath<Material>(overlayPath);
            if (overlay == null)
            {
                overlay = new Material(Shader.Find("NangClicker/Hex Spell Overlay")) { name = "M_SpellOverlay" };
                overlay.SetColor("_BaseColor", Color.white);
                AssetDatabase.CreateAsset(overlay, overlayPath);
            }
            overlay.shader = Shader.Find("NangClicker/Hex Spell Overlay");
            EditorUtility.SetDirty(overlay);
            const string spherePath = "Assets/Prefabs/HexWave/SpellMaterials/M_SpellAnchor.mat";
            var sphere = AssetDatabase.LoadAssetAtPath<Material>(spherePath);
            if (sphere == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.abyss.packages/_AbyssVisualToolKit/AllMaterials/SphereFieldMaterials/M_SphereField_White.mat");
                sphere = new Material(source != null ? source : overlay) { name = "M_SpellAnchor" };
                AssetDatabase.CreateAsset(sphere, spherePath);
            }
            var preview = map.GetComponent<HexSpellPreviewRenderer>();
            if (preview == null) preview = Undo.AddComponent<HexSpellPreviewRenderer>(map.gameObject);
            var previewData = new SerializedObject(preview);
            previewData.FindProperty("overlayMaterial").objectReferenceValue = overlay;
            previewData.FindProperty("sphereFieldMaterial").objectReferenceValue = sphere;
            previewData.ApplyModifiedProperties();
            var manager = map.GetComponent<SpellManager>();
            if (manager == null) manager = Undo.AddComponent<SpellManager>(map.gameObject);
            var managerData = new SerializedObject(manager);
            managerData.FindProperty("waveManager").objectReferenceValue = map;
            managerData.FindProperty("preview").objectReferenceValue = preview;
            managerData.FindProperty("inputCamera").objectReferenceValue = Camera.main;
            managerData.ApplyModifiedProperties();
            if (map.GetComponent<HexSpellTestUI>() == null) Undo.AddComponent<HexSpellTestUI>(map.gameObject);
            var legacy = map.GetComponent<HexWavePointerInput>();
            if (legacy != null) { Undo.RecordObject(legacy, "Disable legacy attack input"); legacy.enabled = false; EditorUtility.SetDirty(legacy); }
            var guideTexts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var text in guideTexts)
                if (text.name == "Guide Text")
                {
                    Undo.RecordObject(text, "Update spell guide");
                    text.text = "OBJECTIVE\n\nStop enemies from reaching the center.\n\nSelect a spell below, rotate its direction, then click a floor hex to cast.\nMoving hexes damage enemies standing on them.\n\nEnemies reaching the center damage your Base.\nBase HP 0 means defeat.\n\nCONTROLS\n\nLeft Click — Cast selected spell\nMiddle Mouse Drag — Move Camera\nMouse Wheel — Zoom\nI — Toggle Guide\nAlt+R — Restart Game";
                    PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                    EditorUtility.SetDirty(text);
                }
            map.RebuildGrid();
            HexWaveEditorVisuals.RefreshBoundaryPreview(map);
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            EditorSceneManager.SaveScene(map.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Hex Spell] Installed two sample spells, collected/registered database and wired scene test controls.");
        }

        private static void CreateSample(Key_HexSpellPP key, string identifier, string name, HexCoordinate[] hits)
        {
            string path = Folder + "/HexSpellPP_" + identifier + ".asset";
            if (AssetDatabase.LoadAssetAtPath<HexSpellProperty>(path) != null) return;
            var spell = ScriptableObject.CreateInstance<HexSpellProperty>();
            spell.displayName = name;
            spell.description = key == Key_HexSpellPP.Line ? "Four direct impulses; anchor is the leftmost red hex." : "Three triangular impulses; anchor is the lowest red hex.";
            var region = new HashSet<HexCoordinate>();
            foreach (var hit in hits)
            {
                region.Add(hit);
                for (int d = 0; d < 6; d++) region.Add(hit.GetNeighbor(d));
                spell.impulses.Add(new HexSpellImpulse(hit, 0f, 8f));
            }
            spell.region = new List<HexCoordinate>(region); spell.region.Sort();
            AssetDatabase.CreateAsset(spell, path);
            var data = new SerializedObject(spell); data.FindProperty("enumKey").intValue = (int)key;
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(spell);
        }

        private static void RegisterDatabase(HexSpellDatabase database)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(InitPath);
            try
            {
                var registry = root.GetComponent<PropertyDatabaseManager>();
                if (registry == null) throw new System.InvalidOperationException("Initialization prefab has no PropertyDatabaseManager.");
                var data = new SerializedObject(registry); var list = data.FindProperty("allDatabases");
                for (int i = list.arraySize - 1; i >= 0; i--)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue is HexSpellDatabase)
                    { list.GetArrayElementAtIndex(i).objectReferenceValue = null; list.DeleteArrayElementAtIndex(i); }
                int index = list.arraySize; list.InsertArrayElementAtIndex(index);
                list.GetArrayElementAtIndex(index).objectReferenceValue = database;
                data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, InitPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; }
        }
    }
}
