# Hex Spell system

## Play and configure

Open Assets/Scenes/S_MainGame.unity. Slots are SpellManager.slots; the default three slots use the centrally registered HexSpellDatabase. Select with 1/2/3 (main row or keypad), rotate 60 degrees with R, then hold LMB on a valid floor hex. The anchor, direction, strength and event data are captured on mouse down.

Every skill charges, including impulse-only skills. Early release cancels; lifted contributions return smoothly without a free wave. At full charge the selected slot flashes and the bar says READY - RELEASE MOUSE TO CAST. Continue holding to keep lift targets stationary; release LMB to commit. Right-click, Escape, changing slot, losing application focus, disabling input or pausing cancels the held cast. R does not rotate a held cast. Alt+R is excluded from skill rotation.

Public authority: BeginSpell -> HexSpellWave; ReleaseSpell and CancelSpell. CastSpell is an automation convenience that performs a full charge and auto-releases; player input never uses it. Height/velocity snapshots are published by HexWaveManager. The renderer does not own simulation data.

## Events and authoritative lift

HexSpellProperty has strength, chargeDuration, wallDuration, region, impulses (the serialized event list) and walls.
An event has offset, mode, releaseDelay, signed impulse or signed targetHeight.

- Impulse: velocity increment = impulse * strength * castMultiplier / mass.
- LiftAndRelease: target = targetHeight * strength * castMultiplier.
- All lifts begin together during charging, using quintic smoothstep with zero endpoint velocity.
- All events use releaseDelay measured from the actual ready mouse release.
- A delayed lift holds its target until that delay expires, then preserves displacement and joins free spring motion with zero added impulse.
- Controlled lift and cancellation recovery contributions do not couple to neighbors. Other free contributions still evolve independently.
- Cancellation uses a 0.16-second Hermite recovery preserving its starting height and velocity.
- There is no Height From Strength curve, holdDuration, manual boundary-edge list or legacy ring impulse profile.

Physics per free contribution: m*a = -ko*h - (co + extraDamping)*v + sum(kn*(hj-h) + cn*(vj-v)).
All arrays advance from a common previous snapshot; the shared world substeps for stiffness/damping stability.
No hard height clamp. Sleep requires all heights and velocities of a contribution below their thresholds for sleepSteps consecutive calls; pending events and held casts prevent completion. Settings are captured on cast creation.

## Region perimeter and walls

Region is the set where this cast may have displacement. It includes anchor 000. Walls are separate cell coordinates outside the moving region and cannot overlap an event or anchor.
Perimeter EdgeMode is Absorb, Free or Fixed support. Fixed support means a zero-height external neighbor, not locking the inside perimeter row.

Physical scene walls override spell walls, which override the default perimeter response. Free still clips the cast to its region.
An Absorb wall remains fixed and adds damping inward. Width 2: contact layer gets full damping, next layer gets half, deeper cells get none. Overlapping absorption bands use the maximum extra damping, not a sum.

Spell walls appear on ready mouse release; wallDuration starts then. During charging/ready hold they are previews. They affect every free wave, including casts that predate the wall. No caster ownership test restricts response.
Topology updates on wall creation, expiry and effective response changes. New wall cells smoothly retire their existing contributions without transmitting that recovery. Expiry does not restore cached heights.
Wall duration is independent of wave sleep. Same-cell overlapping leases do not multiply gain; Fixed > Absorb > Amplify; same-priority ties use the latest creation. Every lease expires separately; the last lease removal frees the cell.
Temporary walls currently affect wave topology only, not the enemy BFS map.

## Shared response profiles

Assets/A_Systems/HexSpellSystem/AllSOs/HexBoundaryProfileSOs contains Fixed, Absorb and Amplify properties plus HexBoundaryProfileDB_main.
Lookup: PropertyDatabaseManager.GetDatabase<HexBoundaryProfileDatabase>().GetByEnum(key).
Skill wall entries store response enum keys. Profiles hold immutable response parameters; runtime contacts and expiry live in HexWaveWorld/HexBoundarySource.

Amplify has gain, triggerThreshold, triggerCooldown and rearmRatio. It has no EnergyBudget, MaxTriggers or fallback mode.
A contact triggers when abs(h) >= threshold, h*v > 0, its cooldown has expired and its gate is armed. It rearms after abs(h) drops below threshold * rearmRatio.
Walls amplify existing motion; they do not start a wave from zero.

All contacts inspect the same combined free-wave snapshot. Different adjacent wall cells add velocity increments:
vNew = vBefore * (1 + sum(gainWall - 1)).
Three Gain=1.2 walls give 1.6*vBefore. Each wall/contact pair is independent. Internal field count cannot duplicate a contact trigger.
The shared Amplify profile starts at gain 1.2; skill wall lifetime controls how long it can inject energy. Finite lifetime does not bound peak amplitude; adjust gain, lifetime and damping together.
Wall glyphs: Amplify purple +, Absorb cyan -, Fixed gray. Contact edges flash, and temporary-wall outlines fade near expiry.

## Editor

Tools > Hex Wave > Spell Editor.
Select a skill SO. Both 2D editing and 3D preview support wheel zoom/MMB pan and independent Reset View/Frame controls.
Brushes: Select, Region (toggle once per cell per stroke), Event (new default impulse then edit it), Spell Wall (saved in skill), Test Wall (preview environment only), Enemy (infinite-health training target).
The selected wall response chooses a shared profile; Generate wall ring paints outside the region. Painting a wall replaces conflicting region/events at that cell.
Sidebar sections separate identity, casting, perimeter response, selected events, walls/training, sources and cleanup.
Playback: Play/Pause, Replay, Step, Reset Simulation and Loop. Add Cast is removed.
Preview automatically releases at full charge; it does not emulate a mouse hold. Reset Simulation returns to stopped 0s and clears runtime motion, contacts, wall leases and damage statistics, preserving authored layout, test environment and views.

Physics Source supplies mass, ko/co/kn/cn, substeps and sleep settings. Damage Source independently supplies height deadzone, scale and hurt interval. Neither imports scene geometry.
Damage [D] toggles labels when the editor window is focused and not editing text. Damage = ceil(max(0, abs(height)-minimumHeight)*heightScale)*stackCount per hurt tick, including controlled lift. Training HP is infinite.

PreviewRenderUtility owns a separate scene/camera. Play Mode transitions pause/reset preview simulation and dispose rendering resources. Camera, meshes and material are checked independently and recreated when invalid. View state survives.
A render failure resets preview resources and displays a diagnostic instead of keeping invalid handles.

## Existing assets and migration

The nine authored skill layouts and enum identities remain. Existing direct heights are retained, including the user-edited ChargedTriangle targets.
Old event time becomes releaseDelay; old lift holdDuration is added to its event delay.
ResonanceCage's amplified perimeter becomes an explicit outer wall ring, lifetime 2.2s. Aftershock uses a temporary wall at local (4,0,-4), lifetime 1.6s. Both use the shared Amplify profile.
The scene height gradient and enemy manager enabled state are preserved.

Tools > Hex Wave > Upgrade Spell System In Active Scene is an explicit idempotent registration/setup utility. It creates missing standard profiles, collects databases, registers each family once on AInitGameObject Variant, attaches wall visuals and updates the guide. It never overwrites authored skill layouts.
No AbyssToolKit/package sources or build settings are modified.

## Verification

Tools > Hex Wave > Verify Spell Simulation runs isolated deterministic checks: all skill rotations, finite states and retirement, charge/hold/cancel, snapshot isolation, delayed execution, exact three-wall addition, aggregate cancellation, older-wave contacts, dynamic wall retirement/expiry, overlapping leases, absorption width, blocked events, height damage and editor canvas transforms.
Manual game check: select slot 2, cancel an early hold, then hold to READY and release. Select slot 3 to inspect temporary purple walls and their expiry. Reopen/use the editor across Play Mode transitions and confirm its columns remain visible.
