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
            CreateAdvancedSamples();
            AssignPlaceholderIcons();
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
            var slots = managerData.FindProperty("slots");
            if (slots.arraySize == 2 && slots.GetArrayElementAtIndex(0).intValue == 1 && slots.GetArrayElementAtIndex(1).intValue == 2)
            {
                slots.arraySize = 3;
                slots.GetArrayElementAtIndex(0).intValue = (int)Key_HexSpellPP.Line;
                slots.GetArrayElementAtIndex(1).intValue = (int)Key_HexSpellPP.ChargedTriangle;
                slots.GetArrayElementAtIndex(2).intValue = (int)Key_HexSpellPP.ResonanceCage;
            }
            managerData.ApplyModifiedProperties();
            if (map.GetComponent<HexSpellTestUI>() == null) Undo.AddComponent<HexSpellTestUI>(map.gameObject);
            var legacy = map.GetComponent<HexWavePointerInput>();
            if (legacy != null) { Undo.RecordObject(legacy, "Disable legacy attack input"); legacy.enabled = false; EditorUtility.SetDirty(legacy); }
            var guideTexts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var text in guideTexts)
                if (text.name == "Guide Text")
                {
                    Undo.RecordObject(text, "Update spell guide");
                    text.text = "OBJECTIVE\n\nStop enemies from reaching the center.\n\nSelect a spell with 1 / 2 / 3. Press R to rotate, then click a floor hex to cast.\nMoving hexes damage enemies standing on them.\n\nEnemies reaching the center damage your Base.\nBase HP 0 means defeat.\n\nCONTROLS\n\nLeft Click — Cast selected spell\n1 / 2 / 3 — Select spell\nR — Rotate 60 degrees\nMiddle Mouse Drag — Move Camera\nMouse Wheel — Zoom\nI — Toggle Guide\nAlt+R — Restart Game";
                    PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                    text.enableAutoSizing = true; text.fontSizeMin = 20; text.fontSizeMax = 25;
                    if (text.transform.parent is RectTransform guidePanel)
                    {
                        guidePanel.sizeDelta = new Vector2(guidePanel.sizeDelta.x, 680);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(guidePanel);
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                    EditorUtility.SetDirty(text);
                }
            map.RebuildGrid();
            HexWaveEditorVisuals.RefreshBoundaryPreview(map);
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            EditorSceneManager.SaveScene(map.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Hex Spell] Installed spell collection, registered database and upgraded skill bar.");
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


        private static HexSpellImpulse Lift(HexCoordinate offset, float start, float height) =>
            new HexSpellImpulse(offset, start, 0) { mode = HexSpellEventMode.LiftAndRelease, targetHeight = height };

        private static List<HexCoordinate> Disk(int radius)
        {
            var result = new List<HexCoordinate>();
            for (int a = -radius; a <= radius; a++) for (int b = -radius; b <= radius; b++)
            { var cell = HexCoordinate.FromAxial(a, b); if (cell.DistanceTo(HexCoordinate.Origin) <= radius) result.Add(cell); }
            return result;
        }
        private static void SaveNew(Key_HexSpellPP key, string name, string description, List<HexSpellImpulse> events,
            int diskRadius = 0, System.Action<HexSpellProperty> customize = null)
        {
            string path = Folder + "/HexSpellPP_" + key + ".asset";
            if (AssetDatabase.LoadAssetAtPath<HexSpellProperty>(path) != null) return;
            var spell = ScriptableObject.CreateInstance<HexSpellProperty>();
            spell.displayName = name; spell.description = description; spell.impulses = events;
            if (diskRadius > 0) spell.region = Disk(diskRadius);
            else
            {
                var region = new HashSet<HexCoordinate>();
                foreach (var hit in events) { region.Add(hit.offset); for (int d = 0; d < 6; d++) region.Add(hit.offset.GetNeighbor(d)); }
                spell.region = new List<HexCoordinate>(region); spell.region.Sort();
            }
            customize?.Invoke(spell);
            AssetDatabase.CreateAsset(spell, path);
            var data = new SerializedObject(spell); data.FindProperty("enumKey").intValue = (int)key;
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(spell);
        }
        private static void CreateAdvancedSamples()
        {
            var zero = HexCoordinate.Origin;
            SaveNew(Key_HexSpellPP.ChargedTriangle, "Triad Fall", "Three authoritative lifts charge in isolation, hold, then release without added impulses.",
                new List<HexSpellImpulse> { Lift(zero, 0, 1), Lift(HexCoordinate.FromAxial(-1, 1), 0, 0.8f), Lift(HexCoordinate.FromAxial(0, 1), 0, 1.1f) },
                customize: s => { s.chargeDuration = 0.65f; s.holdDuration = 0.12f; });
            var stairs = new List<HexSpellImpulse>();
            for (int i = 0; i < 5; i++) stairs.Add(Lift(HexCoordinate.FromAxial(i, 0), i * 0.18f, 0.7f + 0.12f * i));
            SaveNew(Key_HexSpellPP.StairHammer, "Cascade", "A sequence of raised columns releases progressively along the line.", stairs,
                customize: s => { s.chargeDuration = 0.5f; s.holdDuration = 0.05f; });
            SaveNew(Key_HexSpellPP.Convergence, "Cross Echo", "Opposing pairs converge; a delayed second axis changes the interference at the center.",
                new List<HexSpellImpulse> {
                    new HexSpellImpulse(HexCoordinate.FromAxial(-2, 0), 0, 10), new HexSpellImpulse(HexCoordinate.FromAxial(2, 0), 0, 10),
                    new HexSpellImpulse(HexCoordinate.FromAxial(0, -2), 0.05f, 8), new HexSpellImpulse(HexCoordinate.FromAxial(0, 2), 0.05f, 8) }, 3);
            var cage = new List<HexSpellImpulse> { new HexSpellImpulse(zero, 0, 10) };
            for (int d = 0; d < 6; d++) cage.Add(new HexSpellImpulse(zero.GetNeighbor(d), 0.15f, 2));
            SaveNew(Key_HexSpellPP.ResonanceCage, "Resonance Cage", "A radius-three enclosure pumps at its edges for 2.2s with a shared 35-energy budget, then absorbs.", cage, 3,
                s => { s.edgeMode = HexSpellEdgeMode.Amplify; var a = HexAmplifySettings.Default; a.gain = 1.6f; a.activeDuration = 2.2f;
                    a.energyBudget = 35; a.maxTriggers = 24; a.triggerThreshold = 0.015f; s.amplification = a; });
            var ring = new List<HexSpellImpulse>();
            foreach (var cell in Disk(2))
            {
                int distance = cell.DistanceTo(zero);
                if (distance == 2) ring.Add(new HexSpellImpulse(cell, 0, 3));
                if (distance == 1) ring.Add(new HexSpellImpulse(cell, 0.25f, -2));
            }
            ring.Add(new HexSpellImpulse(zero, 0.6f, 6));
            SaveNew(Key_HexSpellPP.RingEcho, "Ring Return", "An outer pulse, an inverted inner ring, then a central echo.", ring, 3);
            var sink = new List<HexSpellImpulse> { Lift(zero, 0, -1.2f) };
            for (int d = 0; d < 6; d++) sink.Add(Lift(zero.GetNeighbor(d), 0, -0.4f));
            SaveNew(Key_HexSpellPP.SinkRebound, "Undertow", "An isolated depression stores elastic energy and rebounds upward on release.", sink, 2,
                s => { s.chargeDuration = 0.45f; s.holdDuration = 0.1f; });
            var echo = new List<HexSpellImpulse>();
            for (int i = 0; i < 3; i++) for (int beat = 0; beat < 3; beat++)
                echo.Add(new HexSpellImpulse(HexCoordinate.FromAxial(i, 0), beat * 0.38f, beat == 0 ? 7 : beat == 1 ? 4 : 3));
            SaveNew(Key_HexSpellPP.Aftershock, "Threefold", "Three timed beats with one outward edge providing a short, budgeted boost.", echo,
                customize: s => {
                    var response = HexBoundarySettings.Fixed; response.mode = HexSpellEdgeMode.Amplify;
                    var a = HexAmplifySettings.Default; a.gain = 1.25f; a.energyBudget = 6; a.activeDuration = 1.6f; a.maxTriggers = 8; response.amplification = a;
                    s.boundaryEdges.Add(new HexSpellBoundaryEdge { offset = HexCoordinate.FromAxial(3, 0), direction = 1, response = response });
                });
        }
        private static void AssignPlaceholderIcons()
        {
            const string destination = "Assets/Prefabs/HexWave/SpellIcons";
            const string source = "Packages/com.abyss.packages/_AbyssVisualToolKit/AllExtraIcons/Flat Icons [Free]/";
            EnsureFolder(destination);
            string[] names = { "Free Flat Arrow 1 E Icon.png", "Free Flat Arrow 1 S Icon.png", "Free Flat Star Filled Icon.png" };
            var icons = new Sprite[3];
            for (int i = 0; i < names.Length; i++)
            {
                string path = destination + "/SpellIcon_" + i + ".png";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null && !AssetDatabase.CopyAsset(source + names[i], path))
                    throw new System.InvalidOperationException("Could not copy placeholder icon: " + source + names[i]);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.maxTextureSize = 256; importer.SaveAndReimport();
                }
                icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:HexSpellProperty", new[] { Folder }))
            {
                var spell = AssetDatabase.LoadAssetAtPath<HexSpellProperty>(AssetDatabase.GUIDToAssetPath(guid));
                if (spell.icon != null) continue;
                spell.icon = icons[spell.EnumKey == Key_HexSpellPP.Line ? 0 : spell.EnumKey == Key_HexSpellPP.ChargedTriangle ? 1 : 2];
                EditorUtility.SetDirty(spell);
            }
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
