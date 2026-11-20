using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace NangClicker.HexWave.Editor
{
    /// <summary>Explicit editor asset lookup and persistent mesh cache. Geometry is never rebuilt per wave frame.</summary>
    public static class HexTerrainAuthoring
    {
        public const string SurfaceFolder = "Assets/A_Systems/HexTerrainSystem/AllSOs/HexTerrainSurfaceSOs";
        public const string DatabasePath = SurfaceFolder + "/HexTerrainSurfaceDB_main.asset";
        public const string MeshFolder = "Assets/Prefabs/HexWave/Terrain/Meshes";
        private static HexTerrainSurfaceDatabase database;
        public static HexTerrainSurfaceProperty Resolve(Key_HexTerrainSurfacePP key)
        {
            if (database == null) database = AssetDatabase.LoadAssetAtPath<HexTerrainSurfaceDatabase>(DatabasePath);
            return database != null ? database.GetByEnum(key) : null;
        }
        public static void EnsureFolder(string path)
        {
            string current = "Assets"; var parts = path.Split('/');
            for (int i = 1; i < parts.Length; i++)
            { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; }
        }
        public static Mesh GetMesh(float radius, float depth)
        {
            EnsureFolder(MeshFolder);
            radius = Mathf.Round(radius * 1000) / 1000; depth = Mathf.Round(depth * 1000) / 1000;
            string path = MeshFolder + "/" + HexColumnMeshBuilder.Key(radius, depth) + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = HexColumnMeshBuilder.Build(radius, depth); AssetDatabase.CreateAsset(mesh, path); }
            return mesh;
        }
        public static void Apply(HexCellView cell, bool surface = true)
        {
            cell.ApplyAuthoringGeometry(GetMesh(cell.ColumnRadius, cell.ColumnDepth));
            cell.MarkTerrainAuthored();
            if (surface)
            {
                var profile = Resolve(cell.Surface);
                if (profile != null) cell.SetSurface(profile.EnumKey, profile.material);
            }
            foreach (var component in cell.GetComponents<Component>())
                if (component != null)
                {
                    EditorUtility.SetDirty(component);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
        }
        public static void RecordCell(HexCellView cell, string name)
        {
            Undo.RecordObjects(cell.GetComponents<Component>(), name);
        }
    }

    [CustomEditor(typeof(HexCellView))]
    public sealed class HexCellViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck(); DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            var cell = (HexCellView)target;
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if ((changed || GUILayout.Button("Apply Geometry / Surface")) && !Application.isPlaying)
                {
                    HexTerrainAuthoring.RecordCell(cell, "Apply Hex Geometry");
                    HexTerrainAuthoring.Apply(cell);
                    var map = cell.GetComponentInParent<HexWaveManager>();
                    if (map != null && changed) map.RebuildGrid();
                    if (cell.gameObject.scene.IsValid())
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cell.gameObject.scene);
                }
            EditorGUILayout.LabelField("Surface world Y", cell.SurfacePoint.y.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }
}
