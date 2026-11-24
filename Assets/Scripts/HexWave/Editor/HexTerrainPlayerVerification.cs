using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NangClicker.HexWave.Editor
{
    public static class HexTerrainPlayerVerification
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static void Invoke(object obj,string method,params object[] args) => obj.GetType().GetMethod(method,Private).Invoke(obj,args);
        private static void SetInt(Object target,string name,int value)
        { var data=new SerializedObject(target);data.FindProperty(name).intValue=value;data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetFloat(Object target,string name,float value)
        { var data=new SerializedObject(target);data.FindProperty(name).floatValue=value;data.ApplyModifiedPropertiesWithoutUndo(); }
        [MenuItem("Tools/Hex Wave/Verify Terrain And Player Assets")]
        public static void RunMenu() => Debug.Log(RunEditor());
        public static string RunEditor()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Run editor checks outside Play Mode.");
            int checks=0;
            Action<bool,string> check=(ok,why)=>{if(!ok)throw new InvalidOperationException(why);checks++;};
            var db=AssetDatabase.LoadAssetAtPath<HexTerrainSurfaceDatabase>(HexTerrainAuthoring.DatabasePath);
            check(db!=null&&db.Entries.Count==5,"Five terrain profiles must exist.");
            for(int i=1;i<=5;i++)
            {
                var profile=db.GetByEnum((Key_HexTerrainSurfacePP)i);
                check(profile!=null&&profile.material!=null,"Each profile must resolve a material.");
                check(profile.material.shader.name=="NangClicker/Hex Terrain"&&profile.material.shader.isSupported,"Terrain shader must be supported.");
            }
            foreach(var shaderName in new[]{"NangClicker/Hex Terrain","NangClicker/Upright Player Sprite"})
            {
                var shader=Shader.Find(shaderName);check(shader!=null,"Shader must exist.");
                foreach(var message in ShaderUtil.GetShaderMessages(shader))check(message.severity.ToString()!="Error",message.message);
            }
            var playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(HexTerrainPlayerSetup.PlayerPath);
            check(playerPrefab!=null&&PrefabUtility.GetPrefabAssetType(playerPrefab)==PrefabAssetType.Variant,"Player must be a Variant.");
            check(playerPrefab.GetComponent<CharacterController>()!=null&&playerPrefab.GetComponentsInChildren<Rigidbody>(true).Length==0,"Player uses CC, not Rigidbody.");
            check(!playerPrefab.GetComponent<PlayerMovementCC>().enabled&&playerPrefab.GetComponent<HexPlayerMotor>()!=null,"Only the terrain-specific CC driver owns movement/camera integration.");
            check(playerPrefab.GetComponent<PlayerAgent>()!=null&&playerPrefab.GetComponentInChildren<SpriteRenderer>()!=null,"Player must retain registration and sprite.");
            var meshes=new System.Collections.Generic.List<Mesh>();
            var preview=EditorSceneManager.NewPreviewScene();GameObject mapRoot=null;
            HexGridPainterWindow painter=null;
            try
            {
                foreach(float depth in new[]{0.5f,1.5f,3f,6f})
                {
                    var mesh=HexColumnMeshBuilder.Build(0.6f,depth);meshes.Add(mesh);
                    check(mesh.vertexCount==38&&mesh.uv.Length==38,"Every generated vertex needs UV.");
                    check(Mathf.Abs(mesh.bounds.max.y-1.5f)<1e-5f&&Mathf.Abs(mesh.bounds.min.y-(1.5f-depth))<1e-5f,"Depth must extend down, keeping the top fixed.");
                    check(Mathf.Abs(mesh.uv[14].y-depth)<1e-5f&&mesh.uv[17].y==0,"Side UV density must follow depth.");
                    var v=mesh.vertices;var n=mesh.normals;var indices=mesh.triangles;
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        var normal=Vector3.Cross(v[indices[i+1]]-v[indices[i]],v[indices[i+2]]-v[indices[i]]).normalized;
                        check(Vector3.Dot(normal,n[indices[i]])>0.99f,"Triangle winding and hard normals must agree.");
                    }
                }
                var zero=HexCoordinate.Origin;
                for(int i=0;i<6;i++)
                {
                    var d=HexCoordinate.GetNeighborDirection(1).Rotate(i);
                    var far=new HexCoordinate(d.A*4,d.B*4,d.C*4);
                    check(SpellManager.QuantizeDirection(zero,far)==i,"Six-direction center must match actual spell rotation.");
                    var offset=HexCoordinate.FromAxial(3,-2);
                    check(SpellManager.QuantizeDirection(offset,offset+far)==i,"Direction must be invariant under map translation.");
                }
                check(SpellManager.QuantizeDirection(zero,zero,4)==4,"Same-cell casts retain their fallback.");
                check(SpellManager.QuantizeDirection(zero,HexCoordinate.FromAxial(2,-1))==0,"Sector boundary tie must be deterministic.");
                mapRoot=new GameObject("Terrain verification map");SceneManager.MoveGameObjectToScene(mapRoot,preview);
                var map=mapRoot.AddComponent<HexWaveManager>();
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HexWave/HexCell.prefab").GetComponent<HexCellView>();
                map.ConfigureAuthoring(source,mapRoot.transform,1.1f);
                for(int a=-2;a<=2;a++)for(int b=-2;b<=2;b++)
                {
                    var c=HexCoordinate.FromAxial(a,b);if(c.DistanceTo(zero)>2)continue;
                    var obj=(GameObject)PrefabUtility.InstantiatePrefab(source.gameObject,mapRoot.transform);
                    var tile=obj.GetComponent<HexCellView>();tile.SetCoordinate(c);obj.transform.localPosition=c.ToLocalPosition(1.1f);tile.CaptureBaseLocalPosition();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tile);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
                }
                map.RebuildGrid();var selected=HexCoordinate.FromAxial(0,-1);
                painter=ScriptableObject.CreateInstance<HexGridPainterWindow>();
                var painterData=new SerializedObject(painter);painterData.FindProperty("manager").objectReferenceValue=map;painterData.ApplyModifiedPropertiesWithoutUndo();
                Invoke(painter,"RefreshCache");
                SetInt(painter,"layer",0);SetInt(painter,"structureBrush",2);SetInt(painter,"wallResponse",3);SetFloat(painter,"wallHeight",2);SetFloat(painter,"wallDepth",3);
                int topology=map.TopologyVersion;
                Invoke(painter,"BeginStroke");Invoke(painter,"Stamp",selected);Invoke(painter,"Stamp",selected);
                check(map.TopologyVersion==topology,"A stroke must defer topology rebuilding.");
                Invoke(painter,"EndStroke");Undo.FlushUndoRecordObjects();
                map.TryGetCell(selected,out var wall);
                check(map.TopologyVersion==topology+1&&wall.IsWall,"Structure stroke must rebuild once.");
                check(Mathf.Abs(wall.SurfacePoint.y-3.5f)<1e-5f&&Mathf.Abs(wall.GetComponent<MeshFilter>().sharedMesh.bounds.min.y+1.5f)<1e-5f,"Top height and depth must be independent.");
                check(wall.transform.localScale==Vector3.one,"Wall edits must never scale child objects.");
                var oldSurface=wall.Surface;
                SetInt(painter,"layer",1);SetInt(painter,"surface",(int)Key_HexTerrainSurfacePP.Water);
                topology=map.TopologyVersion;
                Invoke(painter,"BeginStroke");Invoke(painter,"Stamp",selected);Invoke(painter,"EndStroke");Undo.FlushUndoRecordObjects();
                check(map.TopologyVersion==topology&&wall.IsWall&&wall.WallTopHeight==2&&wall.ColumnDepth==3,"Material painting must preserve structure and topology.");
                check(wall.Surface==Key_HexTerrainSurfacePP.Water&&wall.TargetRenderer.sharedMaterial==db.GetByEnum(Key_HexTerrainSurfacePP.Water).material,"Painted material must be the shared profile asset.");
                Undo.PerformUndo();
                check(wall.Surface==oldSurface&&wall.IsWall,"Undo material stroke must preserve geometry.");
                SetInt(painter,"layer",0);SetInt(painter,"structureBrush",4);SetFloat(painter,"heightDelta",0.25f);
                topology=map.TopologyVersion;var originalMesh=wall.GetComponent<MeshFilter>().sharedMesh;
                var response=wall.WallResponse;int originalCount=map.CellCount;
                Invoke(painter,"BeginStroke");Invoke(painter,"Stamp",selected);Invoke(painter,"Stamp",selected);
                Invoke(painter,"Stamp",zero);Invoke(painter,"Stamp",HexCoordinate.FromAxial(99,0));
                Invoke(painter,"EndStroke");Undo.FlushUndoRecordObjects();
                check(Mathf.Abs(wall.WallTopHeight-2.25f)<1e-5f,"Height brush must apply once per cell per stroke.");
                check(map.TryGetCell(zero,out var floor)&&!floor.IsWall&&floor.SurfacePoint.y==1.5f&&map.CellCount==originalCount,"Height brush must skip floor and empty cells.");
                check(wall.ColumnDepth==3&&wall.Surface==oldSurface&&wall.WallResponse.Equals(response)&&wall.GetComponent<MeshFilter>().sharedMesh==originalMesh,"Height brush must preserve depth, surface, response and shared mesh.");
                check(map.TopologyVersion==topology,"Height-only strokes must preserve wave topology.");
                SetFloat(painter,"heightDelta",-0.5f);Invoke(painter,"BeginStroke");Invoke(painter,"Stamp",selected);Invoke(painter,"EndStroke");Undo.FlushUndoRecordObjects();
                check(Mathf.Abs(wall.WallTopHeight-1.75f)<1e-5f,"A later negative stroke must lower the wall.");
                Undo.PerformUndo();check(Mathf.Abs(wall.WallTopHeight-2.25f)<1e-5f,"Undo must restore one negative height stroke.");
                Undo.PerformUndo();check(wall.WallTopHeight==2,"Undo must restore one positive height stroke.");
                Undo.PerformUndo();
                check(!wall.IsWall&&Mathf.Abs(wall.SurfacePoint.y-1.5f)<1e-5f,"Undo structure stroke must restore the floor.");
                var blocker=new GameObject("Actor collider",typeof(CharacterController));SceneManager.MoveGameObjectToScene(blocker,preview);
                blocker.transform.position=new Vector3(0,2,0);Physics.SyncTransforms();
                check(map.TryRaycastCell(new Ray(new Vector3(0,8,0),Vector3.down),out var picked,out _)&&picked.Coordinate==zero,"An actor collider must not hide a terrain selection.");
                Object.DestroyImmediate(blocker);
            }
            finally
            {
                if(painter!=null)Object.DestroyImmediate(painter);
                if(mapRoot!=null)Object.DestroyImmediate(mapRoot);
                foreach(var mesh in meshes)Object.DestroyImmediate(mesh);
                EditorSceneManager.ClosePreviewScene(preview);
            }
            return "Terrain/player editor verification passed: "+checks+" assertions (profiles, shaders, Variant, UVs, normals, directions, brush batching, undo, material isolation and terrain picking).";
        }

        public static string RunPlay()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
            int checks=0;Action<bool,string> check=(ok,why)=>{if(!ok)throw new InvalidOperationException(why);checks++;};
            var motor=Object.FindFirstObjectByType<HexPlayerMotor>();var spell=Object.FindFirstObjectByType<SpellManager>();
            check(motor!=null&&spell!=null,"Scene player and spell manager must exist.");
            var map=spell.WaveManager;var renderer=map.GetComponent<HexWaveRenderer>();
            for(int i=1;i<=5;i++)
                check(map.GetSurfaceProfile((Key_HexTerrainSurfacePP)i)!=null,"Runtime surfaces must resolve through the central PropertyDatabaseManager.");
            var agent=motor.GetComponent<PlayerAgent>();var generic=motor.GetComponent<PlayerMovementCC>();
            check(PlayerManager.Instance!=null&&PlayerManager.Instance.ActivePlayer==agent&&PlayerManager.Instance.RegisteredPlayerCount==1,"PlayerAgent must register exactly once.");
            check(!generic.enabled&&generic.LocalCamera==null,"Generic driver must not own or duplicate the scene camera.");
            bool inputEnabled=spell.enabled,motorEnabled=motor.enabled,mapEnabled=map.enabled;
            int oldSlot=spell.SelectedSlot,oldMode=(int)spell.DirectionMode;
            var lift=ScriptableObject.CreateInstance<HexSpellProperty>();
            try
            {
                spell.enabled=false;motor.enabled=false;map.enabled=false;map.ResetSimulation();motor.Respawn();
                var start=motor.transform.position;
                for(int i=0;i<16;i++)motor.TickMovement(Vector2.right,0.02f);
                check(Vector2.Distance(new Vector2(start.x,start.z),new Vector2(motor.transform.position.x,motor.transform.position.z))>0.2f,"WASD drive must move the actual CC.");
                motor.Respawn();spell.SelectSlot(0);SetInt(spell,"directionMode",0);
                motor.TryGetCurrentCell(out var playerCell);
                var target=HexCoordinate.Origin;int nearest=int.MaxValue;bool foundTarget=false;
                for(int i=0;i<map.CellCount;i++)
                {
                    var candidate=map.GetCellView(i).Coordinate;
                    int autoDirection=SpellManager.QuantizeDirection(playerCell.Coordinate,candidate);
                    int distance=candidate.DistanceTo(HexCoordinate.Origin);
                    if(distance<nearest&&map.CanCastSpell(spell.SelectedSpell,candidate,0)&&map.CanCastSpell(spell.SelectedSpell,candidate,autoDirection))
                    {target=candidate;nearest=distance;foundTarget=true;}
                }
                if(!foundTarget)throw new InvalidOperationException("Map needs a floor footprint valid for manual and automatic casting.");
                check(spell.BeginAt(target),"A valid floor cast must begin.");
                check(agent.IsMovementLocked&&!generic.InputEnabled,"Charge must acquire the existing PlayerAgent movement lock.");
                start=motor.transform.position;motor.TickMovement(Vector2.right,0.1f);
                check(Vector2.Distance(new Vector2(start.x,start.z),new Vector2(motor.transform.position.x,motor.transform.position.z))<0.001f,"Charge lock must stop horizontal movement.");
                check(!spell.ReleaseCurrent()&&!agent.IsMovementLocked&&generic.InputEnabled,"Early mouse release must cancel and unlock.");
                agent.AcquireMovementLock(motor);
                check(spell.BeginAt(target),"Another owner lock must not prevent a valid spell request.");
                spell.CancelCurrent();
                check(agent.HasMovementLockOwner(motor)&&agent.MovementLockOwnerCount==1,"Spell cancellation must preserve another lock owner.");
                agent.ReleaseMovementLock(motor);
                map.ResetSimulation();motor.Respawn();motor.TryGetCurrentCell(out var support);
                var data=new SerializedObject(lift);data.FindProperty("enumKey").intValue=1;data.ApplyModifiedPropertiesWithoutUndo();
                lift.chargeDuration=0.2f;lift.region.Add(HexCoordinate.Origin);
                lift.impulses.Add(new HexSpellImpulse(HexCoordinate.Origin,0,0){mode=HexSpellEventMode.LiftAndRelease,targetHeight=1});
                var cast=map.World.BeginCast(lift,support.Coordinate,0,map.SimulationSettings);
                agent.AcquireMovementLock(motor);float originalY=motor.transform.position.y;
                for(int i=0;i<14;i++){map.AdvanceSimulation(0.02f);renderer.RefreshPhysicsPose();motor.TickMovement(Vector2.zero,0.02f);}
                float rise=motor.transform.position.y-originalY;
                check(cast.Phase==HexCastPhase.Ready&&rise>0.85f&&rise<1.15f,"A locked player must be carried by the lifting floor.");
                cast.Cancel();
                for(int i=0;i<18;i++){map.AdvanceSimulation(0.02f);renderer.RefreshPhysicsPose();motor.TickMovement(Vector2.zero,0.02f);}
                check(Mathf.Abs(motor.transform.position.y-originalY)<0.12f&&motor.IsGrounded,"Player must follow the descending floor and remain grounded.");
                agent.ReleaseMovementLock(motor);
                map.ResetSimulation();motor.Respawn();
                SetInt(spell,"directionMode",1);check(spell.BeginAt(target),"Auto direction must begin using player and target cells.");
                int locked=spell.ActiveCast.Direction;spell.RotateDirection();
                check(spell.ActiveCast.Direction==locked,"R must not alter a charged cast.");
                for(int i=0;i<100&&spell.ActiveCast.Phase!=HexCastPhase.Ready;i++)map.AdvanceSimulation(0.02f);
                check(spell.ReleaseCurrent()&&!agent.IsMovementLocked,"Ready release must commit and unlock.");
                motor.GetComponent<CharacterController>().enabled=false;
                motor.transform.position+=Vector3.down*50;
                motor.GetComponent<CharacterController>().enabled=true;
                motor.TickMovement(Vector2.zero,0.02f);
                check(motor.transform.position.y>0&&motor.IsGrounded,"Falling below the threshold must respawn.");
            }
            finally
            {
                spell.CancelCurrent();agent.ReleaseMovementLock(motor);map.ResetSimulation();renderer.RefreshPhysicsPose();motor.Respawn();
                spell.SelectSlot(oldSlot);SetInt(spell,"directionMode",oldMode);
                map.enabled=mapEnabled;motor.enabled=motorEnabled;spell.enabled=inputEnabled;Object.Destroy(lift);
            }
            return "Terrain/player Play Mode verification passed: "+checks+" assertions (registration, CC movement, existing locks, lift/return carry, direction, commit and respawn).";
        }

        public static string RunFollowPlay()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
            int checks=0;Action<bool,string> check=(ok,why)=>{if(!ok)throw new InvalidOperationException(why);checks++;};
            var motor=Object.FindFirstObjectByType<HexPlayerMotor>();
            var camera=Object.FindFirstObjectByType<HexPlayerFollowCamera>();
            check(motor!=null&&camera!=null,"Scene must contain the horizontal player follow camera.");
            check(camera.CameraMode==CameraMode.FollowTarget&&AllCameraManager.Instance.CurrentCameraMode==CameraMode.FollowTarget,"Existing camera manager must activate FollowTarget.");
            check(AllCameraManager.Instance.FindCamerasActivated().Count==1,"There must be one active registered camera.");
            check(camera.GetComponent<CameraDragController>()==null,"Free drag must not compete with following.");
            var original=motor.transform.position;bool motorEnabled=motor.enabled;
            var controller=motor.GetComponent<CharacterController>();bool controllerEnabled=controller.enabled;
            try
            {
                motor.enabled=false;controller.enabled=false;camera.ActivateCamera();
                Vector3 before=camera.transform.position;Quaternion rotation=camera.transform.rotation;
                motor.transform.position=original+Vector3.up*3;
                for(int i=0;i<30;i++)camera.LateUpdate();
                check(Vector3.Distance(before,camera.transform.position)<0.0001f,"Pure wave-height changes must not move the camera.");
                check(Mathf.Abs(camera.PlanarFollowPosition.y-camera.FollowPlaneHeight)<1e-5f,"Proxy target must retain its configured height.");
                var shift=new Vector3(3,0,-2);motor.transform.position=original+shift+Vector3.up*3;
                camera.LateUpdate();check(camera.PlanarFollowPosition.x==motor.transform.position.x&&camera.PlanarFollowPosition.z==motor.transform.position.z,"Proxy must follow both horizontal axes.");
                camera.ActivateCamera();
                check(Vector3.Distance(camera.transform.position,before+shift)<0.001f,"Horizontal translation must preserve the camera offset and altitude.");
                check(Quaternion.Angle(rotation,camera.transform.rotation)<0.001f,"Following must preserve the view angle.");
            }
            finally
            {
                motor.transform.position=original;controller.enabled=controllerEnabled;motor.enabled=motorEnabled;camera.ActivateCamera();Physics.SyncTransforms();
            }
            return "Horizontal follow Play Mode verification passed: "+checks+" assertions.";
        }
    }
}
