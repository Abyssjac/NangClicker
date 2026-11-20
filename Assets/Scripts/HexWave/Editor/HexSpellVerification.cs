using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NangClicker.HexWave.Editor
{
    public static class HexSpellVerification
    {
        [MenuItem("Tools/Hex Wave/Verify Spell Simulation")]
        public static void RunMenu() => Debug.Log(Run());
        private static readonly HexCoordinate Zero = HexCoordinate.Origin;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static HexWaveSettings Defaults => new HexWaveSettings
        { mass = 1, kOrigin = 20, cOrigin = 1.8f, kNeighbor = 35, cNeighbor = 0.6f,
            sleepHeight = 0.0005f, sleepVelocity = 0.0005f, sleepSteps = 12, minimumSubsteps = 1 };
        private static HexSpellProperty Spell()
        {
            var spell = ScriptableObject.CreateInstance<HexSpellProperty>();
            var data = new SerializedObject(spell); data.FindProperty("enumKey").intValue = 1; data.ApplyModifiedPropertiesWithoutUndo();
            spell.region.Add(Zero); spell.edgeMode = HexSpellEdgeMode.Free; spell.chargeDuration = 0.1f; spell.wallDuration = 0.5f;
            spell.impulses.Add(new HexSpellImpulse(Zero, 0, 8));
            return spell;
        }
        private static void Step(HexWaveWorld world, float duration)
        { for (int i = 0; i < Mathf.RoundToInt(duration / 0.01f); i++) world.Step(0.01f); }
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            int checks = 0;
            Action<bool, string> check = (ok, why) => { if (!ok) throw new InvalidOperationException(why); checks++; };
            var profile = HexSpellSceneSetup.ResolveProfile(Key_HexBoundaryProfilePP.Amplify);
            check(profile != null && profile.IsValid, "Shared Amplify profile must resolve.");
            var db = AssetDatabase.LoadAssetAtPath<HexSpellDatabase>(HexSpellSceneSetup.DatabasePath);
            check(db != null && db.Entries.Count >= 9, "Existing spell collection must remain registered.");
            var settings = Defaults;
            foreach (var sample in db.Entries)
            {
                check(sample.Validate(out _), sample.name + " must validate.");
                for (int direction = 0; direction < 6; direction++)
                {
                    var world = new HexWaveWorld(_ => true, null, HexSpellSceneSetup.ResolveProfile);
                    var cast = world.BeginCast(sample, Zero, direction, settings, 1, true);
                    check(cast != null, sample.name + " rotation must begin.");
                    var mask = new HashSet<HexCoordinate>(); foreach (var c in sample.region) mask.Add(c.Rotate(direction));
                    bool released = false;
                    for (int i = 0; i < 3000 && !world.IsIdle; i++)
                    {
                        world.Step(0.02f);
                        released |= cast.Phase == HexCastPhase.Released || cast.IsFinished;
                        if (i % 25 != 0) continue;
                        for (int j = 0; j < cast.CellCount; j++)
                        {
                            check(mask.Contains(cast.GetCoordinate(j)), "Simulation must remain in the authored mask.");
                            check(Finite(cast.GetHeight(j)) && Finite(cast.GetVelocity(j)), sample.name + " produced a nonfinite state.");
                        }
                    }
                    check(released && world.IsIdle, sample.name + " must release, expire walls and settle.");
                }
            }
            var spell = Spell();
            var pumpProfile = Object.Instantiate(profile);
            var pump = HexAmplifySettings.Default; pump.gain = 1.2f; pump.triggerThreshold = 0.001f; pumpProfile.amplification = pump;
            Func<Key_HexBoundaryProfilePP, HexBoundaryProfileProperty> resolve = key => key == Key_HexBoundaryProfilePP.Amplify ? pumpProfile : HexSpellSceneSetup.ResolveProfile(key);
            try
            {
                var neighbor = HexCoordinate.FromAxial(1, 0); spell.region.Add(neighbor);
                spell.impulses[0] = new HexSpellImpulse(Zero, 0.2f, 0) { mode = HexSpellEventMode.LiftAndRelease, targetHeight = 1 };
                var world = new HexWaveWorld(_ => true, null, resolve);
                var cast = world.BeginCast(spell, Zero, 0, settings);
                Step(world, 0.05f);
                check(world.Height(Zero) > 0 && world.Height(Zero) < 1, "Charge must change authoritative height.");
                check(world.Height(neighbor) == 0, "Controlled lift must not propagate.");
                float beforeCancel = world.Height(Zero); cast.Cancel();
                check(Mathf.Abs(world.Height(Zero) - beforeCancel) < 1e-6f, "Cancel must preserve continuity.");
                Step(world, 0.3f);
                check(world.IsIdle && world.Height(neighbor) == 0, "Cancel must recover without a released wave.");

                world = new HexWaveWorld(_ => true, null, resolve);
                cast = world.BeginCast(spell, Zero, 0, settings);
                spell.impulses[0] = new HexSpellImpulse(Zero, 0, 0) { mode = HexSpellEventMode.LiftAndRelease, targetHeight = 99 };
                Step(world, 0.5f);
                check(cast.Phase == HexCastPhase.Ready && Mathf.Abs(world.Height(Zero) - 1) < 1e-6f, "Full charge must hold and snapshot target data.");
                check(world.Height(neighbor) == 0, "Waiting for mouse release must stay isolated.");
                check(world.ReleaseCast(cast), "A ready cast must commit.");
                Step(world, 0.19f);
                check(Mathf.Abs(world.Height(Zero) - 1) < 1e-6f && world.Height(neighbor) == 0, "Release delay starts after commit.");
                Step(world, 0.03f);
                check(world.Velocity(Zero) < 0 && world.Height(neighbor) != 0, "Lift release uses elastic motion without a positive impulse.");

                spell.impulses[0] = new HexSpellImpulse(Zero, 0.05f, 8);
                world = new HexWaveWorld(_ => true, null, resolve);
                cast = world.BeginCast(spell, Zero, 0, settings);
                Step(world, 0.2f);
                check(cast.Phase == HexCastPhase.Ready && world.Height(Zero) == 0, "Impulse-only spells must also wait for release.");
                world.ReleaseCast(cast); Step(world, 0.04f);
                check(world.Height(Zero) == 0, "Impulse delay must be honored.");
                Step(world, 0.03f); check(world.Height(Zero) > 0, "Delayed impulse must execute.");

                // Exact additive calculation: three distinct walls, one shared before-boost velocity.
                spell.region = new List<HexCoordinate> { Zero }; spell.chargeDuration = 0.01f;
                spell.impulses[0] = new HexSpellImpulse(Zero, 0, 2);
                spell.walls = new List<HexSpellWall>();
                foreach (var c in new[] { HexCoordinate.FromAxial(0, -1), HexCoordinate.FromAxial(1, -1), HexCoordinate.FromAxial(1, 0) })
                    spell.walls.Add(new HexSpellWall { offset = c, response = Key_HexBoundaryProfilePP.Amplify });
                var lossless = settings; lossless.kOrigin = lossless.kNeighbor = lossless.cOrigin = lossless.cNeighbor = 0;
                world = new HexWaveWorld(_ => true, null, resolve);
                cast = world.BeginCast(spell, Zero, 0, lossless);
                Step(world, 0.02f);
                check(world.ActiveWallCount == 0, "Charging/ready walls must only be previews.");
                world.ReleaseCast(cast); world.Step(0.01f);
                check(Mathf.Abs(world.Velocity(Zero) - 3.2f) < 1e-5f, "Three 1.2 walls must yield 1 + 3*0.2, not a product.");
                float speed = world.Velocity(Zero); world.Step(0.01f);
                check(world.Velocity(Zero) == speed, "A single wave excursion cannot retrigger each frame.");

                // An unrelated wave receives the same shared wall response.
                var other = Spell();
                try
                {
                    other.chargeDuration = 0.01f; other.impulses[0] = new HexSpellImpulse(Zero, 0, 2);
                    var otherCast = world.BeginCast(other, Zero, 0, lossless); world.Step(0.01f);
                    check(otherCast != null && world.ReleaseCast(otherCast), "Another cast can use the enclosed floor.");
                    check(world.ActiveWallCount == 3, "Other casts do not duplicate existing wall cells.");
                    Step(world, 0.6f); check(world.ActiveWallCount == 0, "Temporary walls expire independently of surviving waves.");
                }
                finally { Object.DestroyImmediate(other); }

                // Same-cell overlapping leases do not add extra gain and survive until the last lease expires.
                world = new HexWaveWorld(_ => true, null, resolve);
                var first = world.BeginCast(spell, Zero, 0, lossless); Step(world, 0.01f); world.ReleaseCast(first);
                Step(world, 0.1f);
                var second = world.BeginCast(spell, Zero, 0, lossless); Step(world, 0.01f); world.ReleaseCast(second);
                check(world.ActiveWallCount == 3, "Overlapping leases remain three physical walls.");
                Step(world, 0.4f); check(world.ActiveWallCount == 3, "Expiring first lease must preserve second lease.");
                Step(world, 0.12f); check(world.ActiveWallCount == 0, "Last lease expiry removes the walls.");

                // Scene geometry overrides temporary profiles and invalid event placements reject the whole cast.
                var blocked = HexCoordinate.FromAxial(1, 0);
                world = new HexWaveWorld(c => c != blocked, _ => new HexBoundarySource(HexBoundarySettings.Fixed), resolve);
                check(world.BeginCast(spell, blocked, 0, settings) == null, "Blocked anchor is invalid.");
                spell.region.Add(blocked); spell.walls.Clear(); spell.impulses.Add(new HexSpellImpulse(blocked, 0, 8));
                check(world.BeginCast(spell, Zero, 0, settings) == null, "Any blocked event must reject the cast.");

                var floorSpell = Spell();
                var wallSpell = Spell();
                try
                {
                    floorSpell.chargeDuration = wallSpell.chargeDuration = 0.01f;
                    floorSpell.impulses[0] = new HexSpellImpulse(Zero, 0, 2);
                    wallSpell.impulses[0] = new HexSpellImpulse(Zero, 0, 0);
                    wallSpell.walls.Add(new HexSpellWall { offset = HexCoordinate.FromAxial(-2, 0), response = Key_HexBoundaryProfilePP.Amplify });
                    world = new HexWaveWorld(_ => true, null, resolve);
                    var older = world.BeginCast(floorSpell, Zero, 0, lossless);
                    world.Step(0.01f); world.ReleaseCast(older); world.Step(0.01f);
                    var creator = world.BeginCast(wallSpell, HexCoordinate.FromAxial(3, 0), 0, lossless);
                    world.Step(0.01f); world.ReleaseCast(creator); world.Step(0.01f);
                    check(Mathf.Abs(world.Velocity(Zero) - 2.4f) < 1e-5f, "A wall created later must amplify an already-running unrelated wave.");

                    // Two equal and opposite fields cancel before contact gating.
                    world = new HexWaveWorld(_ => true, null, resolve);
                    floorSpell.walls.Add(new HexSpellWall { offset = neighbor, response = Key_HexBoundaryProfilePP.Amplify });
                    var positive = world.BeginCast(floorSpell, Zero, 0, lossless);
                    var negative = world.BeginCast(floorSpell, Zero, 0, lossless, -1);
                    world.Step(0.01f); world.ReleaseCast(positive); world.ReleaseCast(negative); world.Step(0.01f);
                    check(world.Height(Zero) == 0 && world.Velocity(Zero) == 0 &&
                        double.IsNegativeInfinity(world.GetWallSource(neighbor).LastTriggerTime),
                        "Cancelled combined motion must not trigger a boost separately for each field.");

                    // A newly occupied wall cell retires old displacement without resurrecting it on expiry.
                    floorSpell.walls.Clear();
                    world = new HexWaveWorld(_ => true, null, resolve);
                    older = world.BeginCast(floorSpell, neighbor, 0, lossless);
                    world.Step(0.01f); world.ReleaseCast(older); world.Step(0.01f);
                    float beforeWall = world.Height(neighbor);
                    creator = world.BeginCast(wallSpell, HexCoordinate.FromAxial(3, 0), 0, lossless);
                    world.Step(0.01f); float beforeCommit = world.Height(neighbor); world.ReleaseCast(creator);
                    check(beforeWall > 0 && Mathf.Abs(world.Height(neighbor) - beforeCommit) < 1e-6f, "Wall creation must preserve the retiring height continuously.");
                    Step(world, 0.2f); check(world.Height(neighbor) == 0 && world.IsBlocked(neighbor), "New wall cell must smoothly reach zero.");
                    Step(world, 0.4f); check(!world.IsBlocked(neighbor) && world.Height(neighbor) == 0, "Expiry must not restore the retired displacement.");

                    // Absorption width=2 gives full damping at contact and half one layer inward.
                    var absorbSettings = HexBoundarySettings.Fixed;
                    absorbSettings.mode = HexSpellEdgeMode.Absorb; absorbSettings.damping = 5; absorbSettings.absorptionWidth = 2;
                    var absorb = new HexBoundarySource(absorbSettings);
                    var wallCell = HexCoordinate.FromAxial(2, 0);
                    floorSpell.region.Clear(); floorSpell.impulses.Clear();
                    for (int a = -1; a <= 1; a++)
                    {
                        var c = HexCoordinate.FromAxial(a, 0); floorSpell.region.Add(c);
                        floorSpell.impulses.Add(new HexSpellImpulse(c, 0, 1));
                    }
                    world = new HexWaveWorld(c => c != wallCell, _ => absorb, resolve);
                    var test = world.BeginCast(floorSpell, Zero, 0, lossless);
                    world.Step(0.01f); world.ReleaseCast(test); world.Step(0.001f);
                    check(Mathf.Abs(world.Velocity(neighbor) - 0.995f) < 1e-6f &&
                        Mathf.Abs(world.Velocity(Zero) - 0.9975f) < 1e-6f &&
                        Mathf.Abs(world.Velocity(HexCoordinate.FromAxial(-1, 0)) - 1) < 1e-6f,
                        "Absorption must use 5/2.5/0 damping across two inward layers.");
                }
                finally { Object.DestroyImmediate(floorSpell); Object.DestroyImmediate(wallSpell); }

                var canvas = new HexEditorCanvasView { pan = new Vector2(73, -41), zoom = 1.7f };
                var rect = new Rect(40, 80, 700, 300); var point = new Vector2(170, 190);
                Vector2 local = canvas.ToLocal(point, rect, 20); canvas.ZoomAt(point, rect, 20, -3);
                check(Vector2.Distance(local, canvas.ToLocal(point, rect, 20)) < 1e-5f, "Pointer-anchored editor zoom must remain correct.");
                check(HexWaveDamage.Evaluate(0.27f, HexDamageSettings.Default, 4) == 12, "Damage must retain height deadzone, ceiling and stack multiplier.");
            }
            finally { Object.DestroyImmediate(spell); Object.DestroyImmediate(pumpProfile); }
            return $"Hex Spell verification passed: {checks} checks; charge/hold/cancel, delayed release, rotations, masks, additive walls, shared contacts, expiry, damage and canvas transforms.";
        }
    }
}
