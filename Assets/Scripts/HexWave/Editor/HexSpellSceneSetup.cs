using System;
using System.Collections.Generic;
using System.Reflection;
using JackyUtility;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NangClicker.HexWave.Editor
{
    /// <summary>Explicit and idempotent setup; never runs on import or overwrites authored skill layouts.</summary>
    public static class HexSpellSceneSetup
    {
        public const string Folder = "Assets/A_Systems/HexSpellSystem/AllSOs/HexSpellSOs";
        public const string DatabasePath = Folder + "/HexSpellDB_main.asset";
        public const string ProfileFolder = "Assets/A_Systems/HexSpellSystem/AllSOs/HexBoundaryProfileSOs";
        public const string ProfileDatabasePath = ProfileFolder + "/HexBoundaryProfileDB_main.asset";
        private const string InitPath = "Assets/A_Systems/AInitGameObject Variant.prefab";
        private static readonly Dictionary<Key_HexBoundaryProfilePP, HexBoundaryProfileProperty> cache =
            new Dictionary<Key_HexBoundaryProfilePP, HexBoundaryProfileProperty>();
        public static HexBoundaryProfileProperty ResolveProfile(Key_HexBoundaryProfilePP key)
        {
            if (key == Key_HexBoundaryProfilePP.None) return null;
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var db = AssetDatabase.LoadAssetAtPath<HexBoundaryProfileDatabase>(ProfileDatabasePath);
            var result = db != null ? db.GetByEnum(key) : null;
            if (result != null) cache[key] = result;
            return result;
        }
        [MenuItem("Tools/Hex Wave/Upgrade Spell System In Active Scene")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            EnsureFolder(ProfileFolder);
            foreach (var key in new[] { Key_HexBoundaryProfilePP.Fixed, Key_HexBoundaryProfilePP.Absorb, Key_HexBoundaryProfilePP.Amplify })
            {
                string p = ProfileFolder + "/HexBoundaryProfilePP_" + key + ".asset";
                if (AssetDatabase.LoadAssetAtPath<HexBoundaryProfileProperty>(p) != null) continue;
                var profile = ScriptableObject.CreateInstance<HexBoundaryProfileProperty>();
                profile.mode = key == Key_HexBoundaryProfilePP.Amplify ? HexSpellEdgeMode.Amplify :
                    key == Key_HexBoundaryProfilePP.Absorb ? HexSpellEdgeMode.Absorb : HexSpellEdgeMode.Fixed;
                var data = new SerializedObject(profile); data.FindProperty("enumKey").intValue = (int)key;
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(profile, p);
            }
            var db = AssetDatabase.LoadAssetAtPath<HexBoundaryProfileDatabase>(ProfileDatabasePath);
            if (db == null) { db = ScriptableObject.CreateInstance<HexBoundaryProfileDatabase>(); AssetDatabase.CreateAsset(db, ProfileDatabasePath); }
            Collect(db, ProfileFolder);
            var spells = AssetDatabase.LoadAssetAtPath<HexSpellDatabase>(DatabasePath);
            if (spells == null) throw new InvalidOperationException("Existing spell database is missing.");
            Collect(spells, Folder);
            Register(db); Register(spells); cache.Clear();
            foreach (var spell in spells.Entries)
                if (spell != null && !spell.Validate(out string error)) throw new InvalidOperationException(spell.name + ": " + error);
            var map = UnityEngine.Object.FindFirstObjectByType<HexWaveManager>();
            if (map == null) throw new InvalidOperationException("Active scene needs HexWaveManager.");
            if (map.GetComponent<HexWallVisual>() == null) Undo.AddComponent<HexWallVisual>(map.gameObject);
            foreach (var text in UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (text.name == "Guide Text")
                {
                    Undo.RecordObject(text, "Update casting guide");
                    text.text = "OBJECTIVE\n\nStop enemies from reaching the center.\n\nMoving hexes damage enemies standing on them. Enemies reaching the center damage your Base. Base HP 0 means defeat.\n\nCONTROLS\n\n1 / 2 / 3 - Select spell\nR - Rotate 60 degrees\nHold Left Mouse - Charge at a floor hex\nRelease when READY - Cast\nRelease early / Right Mouse / Esc - Cancel\nMiddle Mouse Drag - Move Camera\nMouse Wheel - Zoom\nI - Toggle Guide\nAlt+R - Restart Game";
                    EditorUtility.SetDirty(text); PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                }
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            EditorSceneManager.SaveScene(map.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Hex Spell] Registered shared profiles and updated scene presentation. Authored skill layouts were preserved.");
        }
        private static void Collect(ScriptableObject db, string folder)
        {
            var data = new SerializedObject(db); data.FindProperty("scanFolder").stringValue = folder.Substring("Assets/".Length);
            data.ApplyModifiedPropertiesWithoutUndo();
            var method = db.GetType().GetMethod("EditorCollectFromFolder", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) throw new InvalidOperationException("Database collect method is missing.");
            method.Invoke(db, null); EditorUtility.SetDirty(db);
        }
        private static void Register(ScriptableObject database)
        {
            var root = PrefabUtility.LoadPrefabContents(InitPath);
            try
            {
                var registry = root.GetComponent<PropertyDatabaseManager>();
                if (registry == null) throw new InvalidOperationException("Initialization prefab has no property registry.");
                var data = new SerializedObject(registry); var list = data.FindProperty("allDatabases");
                bool found = false;
                for (int i = list.arraySize - 1; i >= 0; i--)
                {
                    var value = list.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (value == null || value.GetType() != database.GetType()) continue;
                    if (!found) { list.GetArrayElementAtIndex(i).objectReferenceValue = database; found = true; }
                    else { list.GetArrayElementAtIndex(i).objectReferenceValue = null; list.DeleteArrayElementAtIndex(i); }
                }
                if (!found) { int i = list.arraySize; list.InsertArrayElementAtIndex(i); list.GetArrayElementAtIndex(i).objectReferenceValue = database; }
                data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, InitPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void EnsureFolder(string folder)
        {
            string current = "Assets"; var parts = folder.Split('/');
            for (int i = 1; i < parts.Length; i++)
            { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; }
        }
    }
}
