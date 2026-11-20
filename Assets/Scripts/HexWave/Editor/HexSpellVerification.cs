using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NangClicker.HexWave.Editor
{
    /// <summary>Deterministic domain checks in an isolated preview scene; never modifies the game map.</summary>
    public static class HexSpellVerification
    {
        [MenuItem("Tools/Hex Wave/Verify Spell Simulation")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run deterministic checks outside Play Mode.");
            int checks = 0;
            Action<bool, string> check = (condition, message) =>
            { if (!condition) throw new InvalidOperationException("Hex spell verification: " + message); checks++; };
            var db = AssetDatabase.LoadAssetAtPath<HexSpellDatabase>(HexSpellSceneSetup.DatabasePath);
            check(db != null && db.Entries.Count >= 9, "Database should contain the original and advanced samples.");
            var settings = new HexWaveSettings { mass = 1, kOrigin = 20, cOrigin = 1.8f, kNeighbor = 35, cNeighbor = 0.6f,
                sleepHeight = 0.0005f, sleepVelocity = 0.0005f, sleepSteps = 12, minimumSubsteps = 1 };
            var zero = HexCoordinate.Origin;
            foreach (var sample in db.Entries)
            {
                check(sample.Validate(out _), "Sample configuration is invalid.");
                check(db.GetByEnum(sample.EnumKey) == sample, "Enum lookup should resolve each sample.");
                foreach (var cell in sample.region)
                {
                    check(cell.Rotate(6) == cell && cell.Rotate(1).Rotate(-1) == cell, "Rotation must round-trip.");
                    check(cell.DistanceTo(zero) == cell.Rotate(1).DistanceTo(zero), "Rotation must preserve hex distance.");
                }
                for (int d = 0; d < 6; d++)
                {
                    var wave = new HexSpellWave(sample, zero, d, 1, settings, _ => true);
                    for (int step = 0; step < 100; step++) wave.Step(0.02f);
                    check(wave.GetHeight(HexCoordinate.FromAxial(20, 20)) == 0f, "No leakage outside the mask.");
                    for (int i = 0; i < wave.CellCount; i++)
                        check(!float.IsNaN(wave.GetHeight(i)) && !float.IsInfinity(wave.GetHeight(i)), "Simulation must stay finite.");
                    for (int step = 0; step < 2000 && !wave.IsFinished; step++) wave.Step(0.02f);
                    check(wave.IsFinished, "Default waves should decay and retire.");
                }
            }

            var spell = ScriptableObject.CreateInstance<HexSpellProperty>();
            var key = new SerializedObject(spell); key.FindProperty("enumKey").intValue = 1; key.ApplyModifiedPropertiesWithoutUndo();
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var wall = HexCoordinate.FromAxial(1, 0);
                var beyond = HexCoordinate.FromAxial(2, 0);
                spell.edgeMode = HexSpellEdgeMode.Free;
                spell.region = new List<HexCoordinate> { zero, wall, beyond };
                spell.impulses = new List<HexSpellImpulse> { new HexSpellImpulse(zero, 0, 8) };
                var blocked = new HexSpellWave(spell, zero, 0, 1, settings, c => c != wall);
                for (int i = 0; i < 50; i++) blocked.Step(0.02f);
                check(blocked.GetHeight(beyond) == 0f && blocked.GetHeight(wall) == 0f, "A wall must prevent transmission.");
                spell.region.Add(HexCoordinate.FromAxial(0, 1));
                spell.region.Add(HexCoordinate.FromAxial(1, 1));
                var around = new HexSpellWave(spell, zero, 0, 1, settings, c => c != wall);
                float reached = 0;
                for (int i = 0; i < 50; i++) { around.Step(0.02f); reached = Mathf.Max(reached, Mathf.Abs(around.GetHeight(beyond))); }
                check(reached > 0.001f, "Wave should be able to travel around a wall inside its mask.");
                spell.impulses = new List<HexSpellImpulse> { new HexSpellImpulse(wall, 0, 99), new HexSpellImpulse(beyond, 0, 8) };
                var independent = new HexSpellWave(spell, zero, 0, 1, settings, c => c != wall);
                float velocity = 0;
                for (int i = 0; i < independent.CellCount; i++) if (independent.GetCoordinate(i) == beyond) velocity = independent.GetVelocity(i);
                check(Mathf.Approximately(velocity, 8), "Wall hits are skipped, not redistributed; authored sources behind a wall remain valid.");
                spell.impulses = new List<HexSpellImpulse> { new HexSpellImpulse(zero, 0.1f, 8) };
                var delayed = new HexSpellWave(spell, zero, 0, 1, settings, _ => true);
                delayed.Step(0.05f); check(delayed.PendingEventCount == 1 && delayed.GetHeight(zero) == 0f, "Event must not fire early.");
                delayed.Step(0.05f); check(delayed.PendingEventCount == 0, "Event should fire at its scheduled timestamp.");

                var root = new GameObject("Isolated Spell Verification"); SceneManager.MoveGameObjectToScene(root, previewScene);
                var map = root.AddComponent<HexWaveManager>();
                for (int a = -5; a <= 5; a++) for (int b = -5; b <= 5; b++)
                {
                    var c = HexCoordinate.FromAxial(a, b); if (c.DistanceTo(zero) > 5) continue;
                    var obj = new GameObject(c.ToString()); obj.transform.SetParent(root.transform, false);
                    var view = obj.AddComponent<HexCellView>(); view.SetCoordinate(c);
                }
                map.RebuildGrid();
                spell.impulses = new List<HexSpellImpulse> { new HexSpellImpulse(zero, 0, 8) };
                check(map.CastSpell(spell, zero, 0), "First cast accepted.");
                check(map.PendingCastCount == 0 && map.ActiveWaveCount == 1, "Instant cast ends while its wave survives.");
                // Mutate the asset after submission: runtime must use captured coordinates, times and strengths.
                spell.impulses[0] = new HexSpellImpulse(zero, 100, -999);
                map.AdvanceSimulation(0.02f); map.TryGetCellIndex(zero, out int originIndex);
                float single = map.GetCurrentHeight(originIndex);
                check(single > 0, "Accepted cast data must be locked.");
                map.ResetSimulation(); spell.impulses[0] = new HexSpellImpulse(zero, 0, 8);
                map.CastSpell(spell, zero, 0); map.CastSpell(spell, zero, 0); map.AdvanceSimulation(0.02f);
                check(Mathf.Abs(map.GetCurrentHeight(originIndex) - 2 * single) < 0.00001f, "Two casts must add their contributions.");
                map.ResetSimulation(); map.CastSpell(spell, zero, 0); map.CastSpell(spell, zero, 0, -1); map.AdvanceSimulation(0.02f);
                check(Mathf.Abs(map.GetCurrentHeight(originIndex)) < 0.00001f, "Opposite contributions must cancel.");
                map.TryGetCell(wall, out var wallView); wallView.SetWall(true); map.RebuildGrid();
                map.TryGetCellIndex(wall, out int wallIndex);
                check(map.IsFixedBoundary(wallIndex) && !map.IsMovable(wall), "Authored walls must be shared map authority.");
                check(!map.CastSpell(spell, wall, 0), "A wall cannot be a casting anchor.");
                map.TryGetCellIndex(HexCoordinate.FromAxial(5, 0), out int edgeIndex);
                check(map.IsFixedBoundary(edgeIndex), "Automatic outer fixed boundary should be preserved.");

                // An authoritative lift neither leaks into neighbors nor injects another impulse on release.
                spell.edgeMode = HexSpellEdgeMode.Absorb; spell.chargeDuration = 0.4f; spell.holdDuration = 0.1f;
                spell.region = new List<HexCoordinate> { zero, wall, beyond };
                spell.impulses = new List<HexSpellImpulse> { new HexSpellImpulse(zero, 0, 99)
                    { mode = HexSpellEventMode.LiftAndRelease, targetHeight = 1 } };
                var lift = new HexSpellWave(spell, zero, 0, 1, settings, _ => true);
                lift.Step(0.2f);
                check(Mathf.Abs(lift.GetHeight(zero) - 0.5f) < 0.0001f, "Lift height must follow its authoritative smooth trajectory.");
                check(lift.GetHeight(wall) == 0 && lift.GetHeight(beyond) == 0, "Charging must not excite neighbors.");
                check(lift.PendingEventCount == 1 && lift.GetControl(zero) == HexMotionControl.Charging, "Cast remains pending until release.");
                lift.Step(0.2f);
                check(Mathf.Abs(lift.GetHeight(zero) - 1) < 0.0001f && lift.GetControl(zero) == HexMotionControl.Holding, "Lift reaches exact height and holds.");
                lift.Step(0.1f);
                float releaseVelocity = 0;
                for (int i = 0; i < lift.CellCount; i++) if (lift.GetCoordinate(i) == zero) releaseVelocity = lift.GetVelocity(i);
                check(lift.PendingEventCount == 0 && Mathf.Abs(releaseVelocity) < 0.0001f, "Release adds no impulse, even when the unused impulse field is 99.");
                check(Mathf.Abs(lift.GetHeight(zero) - 1) < 0.0001f, "Release preserves height without a discontinuity.");
                lift.Step(0.02f);
                check(lift.GetHeight(zero) < 1 && lift.GetHeight(wall) > 0, "Elastic fall should excite neighbors after release.");
                var dummy = new HexTrainingTarget { coordinate = zero, count = 2 };
                dummy.Sample(0.72f, HexDamageSettings.Default, 0.1);
                long expectedDamage = (long)Mathf.CeilToInt(Mathf.Max(0, Mathf.Abs(0.72f) - 0.02f) * 10f) * 2;
                check(dummy.totalDamage == expectedDamage && dummy.highestHit == expectedDamage, "Training and live damage must preserve float rounding and stack scaling.");
                dummy.ResetStats(); check(dummy.totalDamage == 0 && dummy.hurtCount == 0, "Replay clears damage stats.");
                check(HexWaveDamage.Add(long.MaxValue - 1, 9) == long.MaxValue, "Unlimited training totals must not overflow.");

                var response = HexBoundarySettings.Fixed; response.mode = HexSpellEdgeMode.Amplify;
                response.amplification = HexAmplifySettings.Default;
                var amplification = response.amplification; amplification.energyBudget = 0.5f; amplification.gain = 2;
                amplification.activeDuration = 0.5f; amplification.maxTriggers = 2; response.amplification = amplification;
                var pump = new HexBoundarySource(response, 0, true); float speed = 1;
                pump.TryBoost(zero, 0.1f, ref speed, 1, 0.1);
                check(pump.EnergySpent <= 0.50001f && Mathf.Abs(0.5f * (speed * speed - 1) - pump.EnergySpent) < 0.00001f,
                    "Amplifier must charge exactly its injected kinetic energy and respect budget.");
                check(pump.ModeAt(0.2) == HexSpellEdgeMode.Absorb, "Exhausted budget must fall back.");
                pump.Rearm(1); check(pump.EnergySpent == 0 && pump.ModeAt(1.1) == HexSpellEdgeMode.Amplify, "Explicit rearm starts a fresh finite activation.");
                check(pump.ModeAt(1.6) == HexSpellEdgeMode.Absorb, "Amplifier must expire by time even with unused energy.");
                var sharedPump = new HexBoundarySource(response, 0, true);
                spell.impulses = new List<HexSpellImpulse> { new HexSpellImpulse(zero, 0, 8) };
                var sharedA = new HexSpellWave(spell, zero, 0, 1, settings, c => c != wall, c => sharedPump);
                var sharedB = new HexSpellWave(spell, zero, 0, 1, settings, c => c != wall, c => sharedPump);
                for (int step = 0; step < 40; step++) { sharedA.Step(0.02f); sharedB.Step(0.02f); }
                check(sharedPump.EnergySpent > 0 && sharedPump.EnergySpent <= 0.50001f, "Overlapping waves must share a scene wall's energy budget.");

                wallView.SetWallResponse(response); map.RebuildGrid();
                check(map.GetWallResponseMode(wall) == HexSpellEdgeMode.Amplify, "Authored wall response must reach map authority.");
                var firstSource = map.GetWallSource(wall);
                check(ReferenceEquals(firstSource, map.GetWallSource(wall)), "Scene wall resolution must reuse its source.");
                map.RearmWall(wall); check(ReferenceEquals(firstSource, map.GetWallSource(wall)), "Rearm must update the existing source seen by active casts.");

                // Canvas zoom must preserve the pointer's map coordinate and remain invertible after pan.
                var canvasView = new HexEditorCanvasView { pan = new Vector2(73, -41), zoom = 1.7f };
                var rect = new Rect(40, 80, 700, 300); var point = new Vector2(170, 190);
                Vector2 local = canvasView.ToLocal(point, rect, 20); canvasView.ZoomAt(point, rect, 20, -3);
                check(Vector2.Distance(local, canvasView.ToLocal(point, rect, 20)) < 0.00001f, "Zoom must remain anchored under the pointer.");
                check(Vector2.Distance(point, canvasView.ToScreen(canvasView.ToLocal(point, rect, 20), rect, 20)) < 0.0001f, "Canvas hit testing must invert the rendered transform.");
            }
            finally { Object.DestroyImmediate(spell); EditorSceneManager.ClosePreviewScene(previewScene); }
            return $"Hex Spell verification passed: {checks} checks (database, rotations, masks, walls, lifts, continuous release, budgets, damage, canvas transforms, addition and cancellation).";
        }
    }
}
