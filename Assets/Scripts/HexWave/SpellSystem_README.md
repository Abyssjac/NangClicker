# Hex Spell system

## Play and configure

Open Assets/Scenes/S_MainGame.unity or Assets/Scenes/S_HexTerrainDemo.unity. Slots are SpellManager.slots and use the centrally registered HexSpellDatabase. Select with the slot's number (main row or keypad), then hold LMB on a valid floor hex. PlayerToTarget aims automatically; ManualRotation uses R in 60-degree steps. The anchor, direction, strength and event data are captured on mouse down.

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

## Terrain and registered player

S_MainGame now contains HexWizardPlayer, a true Variant of MovementEssential PlayerRootCC.
AInitGameObject Variant hosts the existing PlayerManager and registers HexTerrainSurfaceDB_main with PropertyDatabaseManager.
PlayerAgent still owns registration and its owner-based movement locks. HexPlayerMotor reads PlayerControl and moves the same CharacterController.
The package's generic PlayerMovementCC and camera binding are disabled in this Variant so they cannot spawn a second camera or rotate the entire actor; their sources are unchanged. PlayerAgent still routes InputEnabled through the existing movement component.

WASD is relative to the existing scene camera's horizontal axes. No jump/sprint is enabled.
HexPlayerFollowCamera extends the package's PlayerCameraFollow and registers as CameraMode.FollowTarget with the existing AllCameraManager. Its startup coroutine waits for camera registration before requesting this mode; it does not add another camera manager or change the shared manager's default for other scenes.
The camera follows a transient proxy at (player.x, followPlaneHeight, player.z), default plane Y=1.5. Player lifting/falling never changes that target's Y. The inherited fixed-angle smoothing, scroll zoom and input locks remain active. Zoom can change camera altitude; player wave displacement cannot. Sprite flipping never rotates the camera.
The two scenes remove CameraDragController from this camera to prevent competing movement. Its script remains available for other free cameras.
Tools > Hex Wave > Use Horizontal Player Follow Camera upgrades only the active scene's camera/guide. The full player installer also uses this setup. FollowSource is the fallback scene player reference; Follow Active Player prefers PlayerManager.ActivePlayer at runtime.
HexPlayerFacing smoothly rotates only FlipPivot around local Y (0/180 degrees, default 0.15s); the sprite is upright in a fixed world plane with no billboard behavior.
The visible sprite is fitted to the 1.35-unit capsule height and at most 0.95 tile spacing in width, using alpha bounds only to configure import scale/pivot; the generated PNG is unchanged.
Supporting grid motion is published before player movement, synchronized to physics, and carried vertically even while input is locked.
The controller ignores only its supporting collider during that support's carry, preserving other collision pairs.
A floor gap can cause a fall; falling below the configurable local Y threshold respawns at spawnCell. Authored Wall cells are non-walkable even at floor height.

SpellManager.directionMode selects PlayerToTarget (scene default) or ManualRotation.
Mouse picking still supplies the spell anchor. PlayerToTarget uses player-cell center toward target-cell center, quantized to the nearest of six spell axes; ties choose the lower index and same-cell casts reuse the last direction.
ManualRotation retains R. Mouse-down snapshots origin/direction and acquires the existing PlayerAgent lock; cancel/ready release releases only the spell manager's lock owner.
Existing skill slots and spell SO layouts are preserved.

## Map authoring and terrain surfaces

Open Tools > Hex Wave > Grid Painter (window title Hex Map Editor).
Structure layer: Select / Floor / Wall / Erase / Height; radius is in hex steps. Wall response selects the existing shared Fixed, Absorb or Amplify profile. No Free wall was added.
Wall Top offset H is relative to the original floor top (local 1.5). Column depth D extends downward:
top = 1.5 + H, bottom = 1.5 + H - D.
Increasing D does not raise the top; increasing H moves both top/bottom together. A Floor always restores the original top and depth.
Height brush adds Height Delta to Top offset on existing authored Wall cells only. Positive raises and negative lowers. Floors (including automatically fixed outer floor cells) and empty coordinates are skipped. Each cell is changed at most once per mouse-down/up stroke; another stroke adds again. Column depth, mesh, material and wall response are preserved, and no height clamp is imposed. The Scene brush and selected-cell panel show the predicted top offset.
Height-only strokes do not rebuild wave topology; they update the authored base transform/collision pose and support Undo. Top offset does not become wave displacement or damage.
The map still treats missing neighbors as automatic fixed wave boundaries. Explicit wall painting separately chooses shared response behavior.
Surface / Material layer paints Grass / Dirt / Rock / Snow / Water on existing cells without changing role, geometry or navigation. Water is a visual profile only.
The 2D canvas and Scene View brush edit the same scene objects. Wheel zooms, MMB pans, Frame Map fits the map and Reset View restores default framing.
A stroke is one Undo group and rebuilds topology only once when geometry/roles changed; surface-only strokes do not rebuild topology.
Wall function uses an independent outline/glyph: Fixed gray outline, Absorb cyan minus, Amplify purple plus. Terrain material remains visible. Fixed markers create no glyph or contact-edge children.
Save Scene persists authoring changes. Painting is disabled during Play Mode.

HexTerrainSurfaceProperty/HexTerrainSurfaceDatabase follow the project's enum/string PropertyDatabase family.
Shared profiles: Assets/A_Systems/HexTerrainSystem/AllSOs/HexTerrainSurfaceSOs.
Materials: Assets/Prefabs/HexWave/Terrain/Materials.
Persistent meshes: Assets/Prefabs/HexWave/Terrain/Meshes.
HexTerrainAuthoring caches one mesh asset per radius/depth rounded to 0.001; top planar UVs, independent side vertices/hard normals, and side V measured in world-length units preserve texture density as walls deepen.
Collider and visible mesh share the same geometry. Meshes are not regenerated during waves; runtime motion translates the cell root.
The URP Hex Terrain shader provides faceted top color variation, a soil band, rock sides and a water-style surface.
HexWaveRenderer no longer applies wave height gradients or legacy wall material tints. It retains the old serialized settings as hidden data.

Tools > Hex Wave > Install Terrain And Player In Active Scene is explicit and never runs on import.
It creates only missing surface assets/Variant, registers one central DB/player manager, upgrades cell meshes and connects the scene actor.
Existing authored surface profiles and player Variant settings are preserved on subsequent runs.
No package, AbyssToolKit source or project setting is changed.

## Player / terrain verification

Tools > Hex Wave > Verify Terrain And Player Assets checks 155 conditions, including shared surfaces/shaders, true Variant/CC setup, mesh winding/UV density, six-direction quantization, brush rebuild batching, positive/negative height strokes, once-per-stroke behavior, floor/empty-cell exclusion, Undo, surface isolation and actor-transparent terrain picking in an isolated physics scene.
HexTerrainPlayerVerification.RunPlay() must run in Play Mode; it checks registration, real CC movement, existing lock ownership, carry during controlled lift and return, cast direction/commit, and falling respawn.
The existing spell simulation verification remains available. Test fixtures are temporary and do not replace the scene map or authored spells.
HexTerrainPlayerVerification.RunFollowPlay() checks 9 conditions in Play Mode: FollowTarget activation, a single active registered camera, no competing drag, fixed Y during player height changes, both horizontal axes, camera offset and angle preservation.

## Valley demo scene

Assets/Scenes/S_HexTerrainDemo.unity is a copy of the current main scene with an authored replacement map. Main-scene terrain is preserved, including pre-existing unsaved authoring changes saved before the camera upgrade.
The demo has 541 cells: 258 walls and 283 connected, walkable floor cells. Wall top offsets range from -0.5 to 4.5. All floors remain at the original baseline.
It uses the existing Grass/Dirt/Rock/Snow/Water profiles: a central clearing, branching dirt trails and crossings, a western river, a southeastern basin, stepped rock ridges and snow caps. The water cells are lowered, non-walkable walls; they have no additional water physics.
Two Amplify and two Absorb posts demonstrate the existing shared boundary profiles. Other walls use Fixed. No spell or shared response SO was changed.
The copied enemy manager is enabled in this scene only: 3 enemies per 3-second step, spawn distances 8 through 12. The map has 148 spawn candidates, and every walkable cell is connected to 000.
Generated columns reuse persistent mesh assets at depths 2.5, 3.5, 4, 4.5, 5.5, 6 and 6.5 (plus the existing depths). No level-generation script is added to Assets; the result is an ordinary editable scene.
Open the scene and press Play directly in the Editor. Build Settings and the package-owned MySceneManager scene registry are unchanged. The latter reports an unregistered-scene warning and skips automatic save loading for the demo; this does not prevent its player, camera, waves or enemies from running.

## Placeholder art provenance

Asset: Assets/Prefabs/HexWave/Player/WizardPlaceholder.png.
Created with the built-in imagegen tool in transparent-background mode; copied into the project without raster edits.
Prompt:

> Use case: stylized-concept. Asset type: single Unity 2D character sprite. Generate one full-body cute cartoon wizard, standing upright on a horizontal ground, neutral idle pose, facing slightly to the viewer's right so left-right flipping reads clearly. Oversized pointed blue-violet hat, simple teal-blue robe, small belt, readable boots and friendly face; compact chibi proportions, clean low-detail painted shapes and restrained dark outlines, suitable at one hex tile wide in a low-poly fantasy strategy game. Orthographic front-three-quarter character view, NOT an overhead view. Entire hat, hands, and boots visible, feet on the same baseline, centered character with generous transparent margin. Single isolated character, genuinely transparent background. No ground plane, no floor shadow, no scenery, no text, no border, no multiple views, no sprite sheet, no motion blur.
