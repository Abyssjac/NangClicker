using System;
using System.Collections.Generic;
using System.Reflection;
using JackyUtility;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NangClicker.HexWave.Editor
{
    /// <summary>Explicit setup for the approved map/player integration; never executes on import.</summary>
    public static class HexTerrainPlayerSetup
    {
        public const string PlayerFolder = "Assets/Prefabs/HexWave/Player";
        public const string PlayerPath = PlayerFolder + "/HexWizardPlayer.prefab";
        private const string InitPath = "Assets/A_Systems/AInitGameObject Variant.prefab";
        private const string SourcePlayer = "Packages/com.abyss.packages/_Others/MovementEssential/Prefabs/PlayerRootCC.prefab";

        [MenuItem("Tools/Hex Wave/Install Terrain And Player In Active Scene")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var map = Object.FindFirstObjectByType<HexWaveManager>();
            if (map == null) throw new InvalidOperationException("Active scene needs HexWaveManager.");
            CreateSurfaces();
            RegisterDatabaseAndManager();
            UpgradeCellPrefab(map);
            var sceneCells = map.CellsRoot.GetComponentsInChildren<HexCellView>(true);
            foreach (var cell in sceneCells)
            {
                HexTerrainAuthoring.RecordCell(cell, "Upgrade Hex Terrain");
                if (cell.TerrainSchemaVersion == 0)
                {
                    var key = cell.IsWall ? Key_HexTerrainSurfacePP.Rock : Key_HexTerrainSurfacePP.Grass;
                    var profile = HexTerrainAuthoring.Resolve(key); cell.SetSurface(key, profile.material);
                    if (cell.IsWall) cell.SetWallShape(cell.transform.localPosition.y, cell.ColumnDepth);
                }
                HexTerrainAuthoring.Apply(cell);
            }
            map.RebuildGrid();
            var playerPrefab = CreatePlayerVariant(map.Spacing);
            var motor = Object.FindFirstObjectByType<HexPlayerMotor>();
            if (motor == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, map.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(instance, "Place Hex Player"); motor = instance.GetComponent<HexPlayerMotor>();
            }
            var spawn = HexCoordinate.FromAxial(0, -1);
            if (!map.IsMovable(spawn))
            {
                int best = int.MaxValue;
                foreach (var cell in sceneCells)
                    if (map.IsMovable(cell.Coordinate) && cell.Coordinate.DistanceTo(HexCoordinate.Origin) < best)
                    { spawn = cell.Coordinate; best = spawn.DistanceTo(HexCoordinate.Origin); }
            }
            if (!map.TryGetCell(spawn, out var spawnTile)) throw new InvalidOperationException("No spawn floor exists.");
            var motorData = new SerializedObject(motor);
            motorData.FindProperty("terrain").objectReferenceValue = map;
            motorData.FindProperty("movementCamera").objectReferenceValue = Camera.main;
            var spawnData = motorData.FindProperty("spawnCell");
            spawnData.FindPropertyRelative("a").intValue = spawn.A; spawnData.FindPropertyRelative("b").intValue = spawn.B; spawnData.FindPropertyRelative("c").intValue = spawn.C;
            motorData.ApplyModifiedProperties();
            motor.transform.position = spawnTile.SurfacePoint + Vector3.up * 0.03f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(motor); PrefabUtility.RecordPrefabInstancePropertyModifications(motor.transform);
            ConfigureHorizontalFollowCamera(map,motor);
            var spell = map.GetComponent<SpellManager>();
            if (spell != null)
            {
                var data = new SerializedObject(spell); data.FindProperty("directionMode").intValue = (int)HexSpellDirectionMode.PlayerToTarget;
                data.ApplyModifiedProperties(); EditorUtility.SetDirty(spell);
            }
            foreach (var text in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (text.name == "Guide Text")
                {
                    Undo.RecordObject(text, "Update player controls");
                    text.text = "OBJECTIVE\n\nStop enemies from reaching the center. Moving hexes damage enemies. Base HP 0 means defeat.\n\nCONTROLS\n\nWASD - Move wizard\n1 / 2 / 3 - Select spell\nHold Left Mouse on a hex - Charge\nRelease when READY - Cast\nRelease early / RMB / Esc - Cancel\nCharging locks movement\nAuto Aim - Player hex toward target hex\nR - Rotate in Manual mode\nWheel - Camera zoom\nCamera follows player horizontally\nI - Toggle Guide\nAlt+R - Restart";
                    EditorUtility.SetDirty(text); PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                }
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene); EditorSceneManager.SaveScene(map.gameObject.scene);
            AssetDatabase.SaveAssets(); Physics.SyncTransforms();
            Debug.Log("[Hex Terrain] Installed shared surfaces, UV columns, registered player Variant and scene integration.");
        }

        [MenuItem("Tools/Hex Wave/Use Horizontal Player Follow Camera")]
        public static void InstallHorizontalFollowCamera()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var map=Object.FindFirstObjectByType<HexWaveManager>();
            var motor=Object.FindFirstObjectByType<HexPlayerMotor>();
            if(map==null || motor==null)throw new InvalidOperationException("Scene needs a hex map and player.");
            ConfigureHorizontalFollowCamera(map,motor);
            foreach(var text in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(text.name=="Guide Text")
                {
                    Undo.RecordObject(text,"Update follow camera guide");
                    text.text=text.text.Replace("MMB Drag / Wheel - Camera / Zoom","Wheel - Camera zoom\nCamera follows player horizontally");
                    EditorUtility.SetDirty(text);PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                }
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            EditorSceneManager.SaveScene(map.gameObject.scene);
        }

        private static void ConfigureHorizontalFollowCamera(HexWaveManager map,HexPlayerMotor motor)
        {
            var camera=Camera.main;
            if(camera==null)camera=map.GetComponentInChildren<Camera>(true);
            if(camera==null)throw new InvalidOperationException("Scene camera is missing.");
            var existing=camera.GetComponent<HexPlayerFollowCamera>();
            float near=12,far=55,zoom=3;
            var drag=camera.GetComponent<CameraDragController>();
            if(drag!=null)
            {
                var old=new SerializedObject(drag);
                near=old.FindProperty("minZoomDistance").floatValue;far=old.FindProperty("maxZoomDistance").floatValue;
                // The old authoring value may be a large free-map step; use a readable follow-camera wheel step.
                zoom=Mathf.Clamp(old.FindProperty("zoomSpeed").floatValue,0.1f,5);
                Undo.DestroyObjectImmediate(drag);
            }
            if(existing==null)
            {
                var old=camera.GetComponent<CameraBase>();
                if(old!=null)Undo.DestroyObjectImmediate(old);
                existing=Undo.AddComponent<HexPlayerFollowCamera>(camera.gameObject);
            }
            else
            {
                var current=new SerializedObject(existing);
                near=current.FindProperty("minDistance").floatValue;far=current.FindProperty("maxDistance").floatValue;
                zoom=current.FindProperty("zoomSpeed").floatValue;
            }
            Undo.RecordObject(existing,"Configure horizontal player camera");
            var data=new SerializedObject(existing);
            data.FindProperty("cameraMode").intValue=(int)CameraMode.FollowTarget;
            data.FindProperty("reliedCameraComponents").arraySize=0;
            data.FindProperty("target").objectReferenceValue=null;
            data.FindProperty("followSource").objectReferenceValue=motor.transform;
            data.FindProperty("followPlaneHeight").floatValue=map.CellsRoot.TransformPoint(Vector3.up*HexColumnMeshBuilder.TopDatum).y;
            data.FindProperty("followActivePlayer").boolValue=true;data.FindProperty("activateOnStart").boolValue=true;
            data.FindProperty("targetOffset").vector3Value=new Vector3(0,0.65f,0);
            data.FindProperty("depthFollowWeight").floatValue=1;
            data.FindProperty("yaw").floatValue=camera.transform.eulerAngles.y;
            data.FindProperty("pitch").floatValue=Mathf.Clamp(camera.transform.eulerAngles.x,10,89);
            float distance=Vector3.Distance(camera.transform.position,motor.transform.position+Vector3.up*0.65f);
            data.FindProperty("distance").floatValue=Mathf.Clamp(distance,near,far);
            data.FindProperty("minDistance").floatValue=near;data.FindProperty("maxDistance").floatValue=far;
            data.FindProperty("zoomSpeed").floatValue=zoom;data.FindProperty("enableZoom").boolValue=true;
            data.FindProperty("rotateWithTarget").boolValue=false;data.FindProperty("smoothTime").floatValue=0.12f;
            data.FindProperty("enableLookAhead").boolValue=false;data.FindProperty("clampPosition").boolValue=false;
            data.FindProperty("drawDebug").boolValue=false;data.ApplyModifiedProperties();
            EditorUtility.SetDirty(existing);PrefabUtility.RecordPrefabInstancePropertyModifications(existing);
        }
        private static void CreateSurfaces()
        {
            HexTerrainAuthoring.EnsureFolder(HexTerrainAuthoring.SurfaceFolder);
            const string materialFolder = "Assets/Prefabs/HexWave/Terrain/Materials";
            HexTerrainAuthoring.EnsureFolder(materialFolder);
            var shader = Shader.Find("NangClicker/Hex Terrain");
            if (shader == null) throw new InvalidOperationException("Terrain shader must compile before installing.");
            var top = new[] { new Color(0.36f,0.51f,0.27f),new Color(0.52f,0.37f,0.25f),new Color(0.49f,0.48f,0.53f),new Color(0.84f,0.91f,0.93f),new Color(0.12f,0.39f,0.54f) };
            var side = new[] { new Color(0.32f,0.30f,0.28f),new Color(0.32f,0.26f,0.22f),new Color(0.31f,0.30f,0.35f),new Color(0.38f,0.44f,0.50f),new Color(0.09f,0.23f,0.31f) };
            var soil = new[] { new Color(0.43f,0.29f,0.17f),new Color(0.46f,0.32f,0.21f),new Color(0.42f,0.40f,0.43f),new Color(0.69f,0.77f,0.80f),new Color(0.14f,0.35f,0.44f) };
            for (int i=0;i<5;i++)
            {
                var key=(Key_HexTerrainSurfacePP)(i+1);
                string materialPath=materialFolder+"/M_Terrain_"+key+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(material==null)
                {
                    material=new Material(shader){name="M_Terrain_"+key};
                    material.SetColor("_TopColor",top[i]);material.SetColor("_SideColor",side[i]);material.SetColor("_SoilColor",soil[i]);
                    material.SetFloat("_SoilThickness",i==2?0.04f:0.18f);
                    material.SetFloat("_Variation",i==4?0.18f:0.12f);material.SetFloat("_Water",i==4?1:0);
                    AssetDatabase.CreateAsset(material,materialPath);
                }
                string profilePath=HexTerrainAuthoring.SurfaceFolder+"/HexTerrainSurfacePP_"+key+".asset";
                if(AssetDatabase.LoadAssetAtPath<HexTerrainSurfaceProperty>(profilePath)==null)
                {
                    var profile=ScriptableObject.CreateInstance<HexTerrainSurfaceProperty>();
                    profile.displayName=key.ToString();profile.material=material;profile.editorColor=top[i];
                    var data=new SerializedObject(profile);data.FindProperty("enumKey").intValue=(int)key;data.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(profile,profilePath);
                }
            }
            var db=AssetDatabase.LoadAssetAtPath<HexTerrainSurfaceDatabase>(HexTerrainAuthoring.DatabasePath);
            if(db==null){db=ScriptableObject.CreateInstance<HexTerrainSurfaceDatabase>();AssetDatabase.CreateAsset(db,HexTerrainAuthoring.DatabasePath);}
            var dbData=new SerializedObject(db);dbData.FindProperty("scanFolder").stringValue=HexTerrainAuthoring.SurfaceFolder.Substring(7);dbData.ApplyModifiedPropertiesWithoutUndo();
            typeof(HexTerrainSurfaceDatabase).GetMethod("EditorCollectFromFolder",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(db,null);
            EditorUtility.SetDirty(db);
        }
        private static void RegisterDatabaseAndManager()
        {
            var database=AssetDatabase.LoadAssetAtPath<HexTerrainSurfaceDatabase>(HexTerrainAuthoring.DatabasePath);
            var root=PrefabUtility.LoadPrefabContents(InitPath);
            try
            {
                var registry=root.GetComponent<PropertyDatabaseManager>();
                if(registry==null)throw new InvalidOperationException("Initialization prefab lacks PropertyDatabaseManager.");
                var data=new SerializedObject(registry);var list=data.FindProperty("allDatabases");bool found=false;
                for(int i=list.arraySize-1;i>=0;i--)
                    if(list.GetArrayElementAtIndex(i).objectReferenceValue is HexTerrainSurfaceDatabase)
                    {
                        if(!found){list.GetArrayElementAtIndex(i).objectReferenceValue=database;found=true;}
                        else{list.GetArrayElementAtIndex(i).objectReferenceValue=null;list.DeleteArrayElementAtIndex(i);}
                    }
                if(!found){int i=list.arraySize;list.InsertArrayElementAtIndex(i);list.GetArrayElementAtIndex(i).objectReferenceValue=database;}
                data.ApplyModifiedPropertiesWithoutUndo();
                if(root.GetComponentInChildren<PlayerManager>(true)==null)root.AddComponent<PlayerManager>();
                PrefabUtility.SaveAsPrefabAsset(root,InitPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static void UpgradeCellPrefab(HexWaveManager map)
        {
            if(map.CellPrefab==null)throw new InvalidOperationException("Assign a cell prefab.");
            string asset=AssetDatabase.GetAssetPath(map.CellPrefab);
            var root=PrefabUtility.LoadPrefabContents(asset);
            try
            {
                var cell=root.GetComponent<HexCellView>();HexTerrainAuthoring.Apply(cell);
                PrefabUtility.SaveAsPrefabAsset(root,asset);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static GameObject CreatePlayerVariant(float spacing)
        {
            HexTerrainAuthoring.EnsureFolder(PlayerFolder);
            string spritePath=PlayerFolder+"/WizardPlaceholder.png";
            var importer=AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Wizard sprite has not imported.");
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
            // Fit the visible character, not the generator's transparent canvas, to the capsule/tile.
            var probe=new Texture2D(2,2);
            RectInt opaqueBounds;
            int sourceWidth,sourceHeight;
            try
            {
                probe.LoadImage(System.IO.File.ReadAllBytes(spritePath));
                sourceWidth=probe.width;sourceHeight=probe.height;
                int minX=sourceWidth,minY=sourceHeight,maxX=0,maxY=0;
                var pixels=probe.GetPixels32();
                for(int y=0;y<sourceHeight;y++)for(int x=0;x<sourceWidth;x++)
                    if(pixels[y*sourceWidth+x].a>=16)
                    {minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
                opaqueBounds=new RectInt(minX,minY,Mathf.Max(1,maxX-minX+1),Mathf.Max(1,maxY-minY+1));
            }
            finally{Object.DestroyImmediate(probe);}
            importer.spritePixelsPerUnit=Mathf.Max(opaqueBounds.width/(spacing*0.95f),opaqueBounds.height/1.35f);
            var textureData=new TextureImporterSettings();importer.ReadTextureSettings(textureData);
            textureData.spriteAlignment=(int)SpriteAlignment.Custom;
            textureData.spritePivot=new Vector2(opaqueBounds.center.x/sourceWidth,(float)opaqueBounds.yMin/sourceHeight);
            importer.SetTextureSettings(textureData);importer.SaveAndReimport();
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);if(existing!=null)return existing;
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            var shader=Shader.Find("NangClicker/Upright Player Sprite");
            if(shader==null)throw new InvalidOperationException("Player sprite shader has not imported.");
            string materialPath=PlayerFolder+"/M_WizardSprite.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(SourcePlayer);
            if(source==null)throw new InvalidOperationException("MovementEssential PlayerRootCC prefab is missing.");
            var root=(GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                root.name="HexWizardPlayer";
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                foreach(var collider in root.GetComponentsInChildren<Collider>(true))if(!(collider is CharacterController))collider.enabled=false;
                var generic=root.GetComponent<PlayerMovementCC>();generic.enabled=false;
                var cameraBinding=root.GetComponent<PlayerMovementCameraBinding>();if(cameraBinding!=null)cameraBinding.enabled=false;
                var control=root.GetComponent<PlayerControl>();var controlData=new SerializedObject(control);
                controlData.FindProperty("logActionsOnInit").boolValue=false;controlData.ApplyModifiedPropertiesWithoutUndo();
                var cc=root.GetComponent<CharacterController>();cc.height=1.35f;cc.radius=0.22f;cc.center=new Vector3(0,0.675f,0);
                cc.stepOffset=0.18f;cc.skinWidth=0.02f;cc.minMoveDistance=0;
                var agent=root.GetComponent<PlayerAgent>();if(agent==null)agent=root.AddComponent<PlayerAgent>();
                var agentData=new SerializedObject(agent);agentData.FindProperty("playerMovementCC").objectReferenceValue=generic;
                agentData.FindProperty("registerWithPlayerManager").boolValue=true;agentData.ApplyModifiedPropertiesWithoutUndo();
                if(root.GetComponent<HexPlayerMotor>()==null)root.AddComponent<HexPlayerMotor>();
                var facing=root.AddComponent<HexPlayerFacing>();
                var flip=new GameObject("FlipPivot");flip.transform.SetParent(root.transform,false);
                var visual=new GameObject("Wizard Sprite");visual.transform.SetParent(flip.transform,false);
                var wizardRenderer=visual.AddComponent<SpriteRenderer>();wizardRenderer.sprite=sprite;wizardRenderer.sharedMaterial=material;
                var facingData=new SerializedObject(facing);facingData.FindProperty("flipPivot").objectReferenceValue=flip.transform;facingData.ApplyModifiedPropertiesWithoutUndo();
                var result=PrefabUtility.SaveAsPrefabAsset(root,PlayerPath);
                return result;
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
