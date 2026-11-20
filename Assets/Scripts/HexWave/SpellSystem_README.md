# Hex Spell system

## Run and equip

Open `Assets/Scenes/S_MainGame.unity` and enter Play Mode. The flat bottom bar defaults to
**Line Strike / Triad Fall / Resonance Cage**. Click a slot, press **1–3** on the main keyboard
or numeric keypad, and click a movable hex to cast. **R** rotates 60 degrees. Alt+R does not
also rotate. The skill bar has icons, hotkey badges and selected-state highlights; no description panel.

Equipment is `SpellManager.slots` (enum keys). The database may contain more than three spells.
`HexSpellTestUI` retains its existing serialized component identity but is presentation-only.
Its Canvas/EventSystem are generated at runtime. Legacy pointer attack input stays disabled.

The scene's original enemy-manager enabled/disabled setting is preserved. If it was disabled
before this change, enable it yourself when testing automatic enemy spawning. Editor training
targets do not depend on enabling or instantiating the real EnemyManager.

## Database and sample collection

Assets: `Assets/A_Systems/HexSpellSystem/AllSOs/HexSpellSOs/`

| Key | Display name | Purpose |
| --- | --- | --- |
| Line | Line Strike | Existing direct line impulses |
| Triangle | Triangle Strike | Existing user-authored sample; its region/events are preserved |
| ChargedTriangle | Triad Fall | Three isolated lifts; 0.65s charge + 0.12s hold, then elastic release |
| StairHammer | Cascade | Five staggered lifts along a line |
| Convergence | Cross Echo | Opposing sources with a delayed second axis |
| ResonanceCage | Resonance Cage | Radius-three mask, short amplified perimeter, then absorption |
| RingEcho | Ring Return | Outer impulse ring, inverted inner ring, delayed center |
| SinkRebound | Undertow | Negative lift targets release into an upward rebound |
| Aftershock | Threefold | Three beats and one explicitly amplified outward edge |

Existing enum values are persistent identifiers; append rather than reorder them.
Property/Database inherit the project's `EnumStringKeyedProperty` / `EnumStringKeyedDatabase`.
Runtime lookup remains `PropertyDatabaseManager -> HexSpellDatabase -> GetByEnum(key)`.
`HexSpellDB_main` has its family scanFolder collected and remains registered once on
`AInitGameObject Variant`. Managers do not serialize a database asset reference.

Create new assets using **Create > AllProperties > HexSpellProperty**. Add an enum value,
give the Property a unique non-None key, keep stringKey empty, collect database entries,
and equip its key in SpellManager. Icons/presentation assets may be referenced directly.

The explicit **Tools > Hex Wave > Install Spell Test In Active Scene** command creates missing
samples/icons, collects/registers the database, and wires an existing map. It preserves existing
spell configurations and custom slot assignments; the old two-slot Line/Triangle loadout is
upgraded to the new three-slot default. It is never run automatically.

## Event parameters

| Parameter | Meaning |
| --- | --- |
| region | Allowed cube-coordinate offsets, relative to anchor 000 |
| impulses | Event list; each entry can be Impulse or LiftAndRelease |
| offset | Target offset, required to belong to region |
| time | Event start time since cast; not necessarily its release time |
| impulse | Signed physical impulse for Impulse mode |
| targetHeight | Signed authoritative displacement for LiftAndRelease |
| heightFromStrength | Optional mapping instead of direct targetHeight |
| heightFromImpulse | Mapping curve X=abs(event impulse), Y=lift height; sign restored afterward |
| strength | Signed global multiplier; scales impulse and lift displacement |
| chargeDuration | Lift time; default uses a quintic smooth trajectory with zero endpoint velocity |
| holdDuration | Stationary hold after lifting, before release |
| edgeMode | Default outer-edge response: Absorb / Free / Fixed / Amplify |
| edgeDamping / absorptionWidth | Extra absorption damping and inward width in cells |
| amplification | Finite pump activation and budget settings |
| boundaryEdges | Optional per-outward-edge response overrides, in local skill coordinates |

Final impulse = event impulse × Property strength × cast multiplier.
Final lift height = direct targetHeight (or signed mapped height) × Property strength × cast multiplier.
The two modes have different physical meanings; they are not assumed to inject equal energy.

A lift event starting at 0.2s, with 0.6s charge and 0.1s hold, releases at 0.9s.
Direction zero follows the upper-right map neighbor (1,0,-1). Rotation uses exact cube-coordinate
permutations; six turns return to the same pattern without world-position rounding.

## Authoritative lift and release

A controlled lift contributes height and velocity directly to the Manager snapshot.
Its quintic trajectory reaches the exact target with zero velocity; it is not just a Transform animation.

During charge/hold, that contribution does not excite neighboring cells. Other casts continue
simulating normally, and enemies on the raised/depressed cell take damage from total absolute height.
At release, the current height becomes an initial displacement of a free wave field, with zero
endpoint velocity. No extra impulse is applied, even if the event's unused impulse field is nonzero.

Several lifts/events at one cell remain separate contributions; they do not overwrite another
skill's wave or total terrain height. Height/velocity snapshots, visual interpolation, damage and
energy diagnostics all read the authoritative sum. There is no hard height clamp.

The supplied work of controlled movement includes elastic coupling energy available at release;
energy diagnostics sum independent contribution energies, rather than pretending this is a
passive gravitational system. The restoring forces are spring forces, not a separate gravity term.

PendingCastCount includes waiting events and not-yet-released lifts. Once all events are released,
the cast is finished while its residual free fields remain alive until sleep thresholds are met.
A Spell GameObject is not kept alive per cast.

## Map walls versus spell edges

Map roles and motion control are independent:
- `HexCellView.isWall`: authored obstacle. Any missing neighbor still causes automatic fixed boundary.
- `useWallResponse / wallResponse`: optional scene-wall response; old walls default to Fixed.
- `HexMotionControl`: Free, Charging or Holding contribution state; a lift is not automatically an obstacle.
- Spell region: limits only that cast's wave contribution and does not change global walkability.

Grid Painter **Wall** has a response configuration; **Floor** clears the authored wall flag but
does not unfix automatic outer boundaries. Rebuilding topology resets waves and wall budgets.
Direct Inspector edits require a rebuild. Fixed walls stay dark, Absorb walls blue-gray,
and active Amplify walls amber; an expired amplifier displays its fallback response.

The clicked anchor must be movable. Missing/wall direct-event cells are skipped without
redistributing their strength. No origin-to-source reachability or line-of-sight check is imposed.
An independently authored event beyond a wall is allowed; actual propagation cannot cross walls,
but can travel around them inside its own region.

A real scene wall takes precedence over a spell-edge override. A spell cannot open a map wall.
Legacy debug impulses also use the shared wall-response solver.

## Finite amplification

Amplify is an active energy source, not negative damping or a per-frame velocity multiplier.
A boundary-adjacent cell triggers while moving away from zero past triggerThreshold.
It rearms only after falling below 40% of that threshold, and respects triggerCooldown.

The pump adds velocity in the current direction, aiming for the configured gain. Injected kinetic
energy is measured and deducted from energyBudget. It also respects activeDuration and maxTriggers.
On expiry/exhaustion it falls back to Fixed or Absorb without deleting the residual wave.

This is a controlled gameplay approximation of enhanced reflection, not an exact traveling-wave
collision detector. Gain is a local velocity gain per valid trigger, not a guaranteed final damage multiplier.

Each cast shares one default-edge budget across its free fields. A per-edge override owns its own
budget. A scene-wall source shares its activation, gates and remaining budget across all casts.
Its activation starts when a wave first resolves that wall source, not every render frame.
Scene walls do not recharge automatically; `HexWaveManager.RearmWall(coordinate)` explicitly rearms one.
Reset/rebuild also resets their budgets. New casts do not recharge an existing scene-wall source.

## Editor navigation and editing

Open **Tools > Hex Wave > Spell Editor**, or open from the Property Inspector.

Both views support wheel zoom and **middle mouse drag**. Each has separate:
- Reset View: default framing.
- Frame Spell: fit authored region/current motion/lift targets.
- Frame Map: fit the test map.

These do not restart simulation. Play/Pause and Replay are separate. Asset/brush changes may reset
simulation and statistics but preserve view pan/zoom. Zoom in the edit canvas is anchored under the pointer.

Brushes:
- Select: select an offset and inspect all events there.
- Region + / Region -: edit the allowed footprint. Erasing removes events and obsolete edge overrides.
- Event +: add an event using New event type/start/impulse/target settings.
- Wall: toggle a local test wall with the brush response. These do not modify the game map.
- Enemy: toggle a static infinite-health training target on a movable cell.

Use the sidebar's local edge selector and Set selected spell edge to override an outward-facing
edge. Use default removes that override. Blue=Absorb, white=Fixed, green=Free, orange=Amplify.
When painting expands a region, obsolete interior-edge overrides are removed.

Grid k × k is an axial parallelogram. Its outer perimeter is fixed, matching game rules.
The lower view uses PreviewRenderUtility and the same solver as the game. Purple columns are
currently controlled lifts; yellow spheres are training targets. D toggles damage labels when
the editor window is focused and a text field is not being edited.

## Training targets and settings sources

Targets have no finite HP pool or death behavior. Their stack count can be adjusted.
Counters include total damage, maximum hit, hurt count, peak height, average DPS and sample history.
Hiding labels does not stop damage collection. Replay/new Play resets statistics and budgets;
Add Cast preserves the current run. Loop continues the run until reset.

Physics Source / Use scene physics settings only reads:
mass, kOrigin, cOrigin, kNeighbor, cNeighbor, height/velocity sleep thresholds, quiet step count,
and minimum substeps. It does not import scene geometry, walls, or enemies.

Damage Source / Use scene damage settings separately reads:
minimumDamageHeight, heightDamageScale, enemyHurtStep. If absent, editable fallback settings are used.

Runtime and editor call the same `HexWaveDamage.Evaluate`:
`ceil(max(0, abs(height) - minimumHeight) * heightScale) * count`.
The float arithmetic/rounding order is preserved from the original game.
Training totals use saturating 64-bit accumulation; they do not fake infinity with int.MaxValue HP.
Sampling uses simulation time, so playback speed changes real-time viewing speed, not damage per run.

Parameters and events are captured on cast; changing a source affects future casts. Replay after
changing external scene parameters is recommended for a controlled comparison. Amplification and
interference can change timing and cumulative damage: a larger visual peak alone does not prove more damage.

## Verification and scope

**Tools > Hex Wave > Verify Spell Simulation** runs deterministic checks in an isolated preview scene:
all skill rotations, finite motion and retirement, mask isolation, wall blocking/detours, independent
direct sources, timed events, captured data, lifts and continuous release, budgets, damage,
canvas transforms, additive/canceling casts and fixed map boundaries.

Development screenshots are generated under `Library/HexSpellVerificationV2`, outside imported assets.
Project-owned placeholder icons under `Assets/Prefabs/HexWave/SpellIcons` are copies of package visuals;
their Sprite import settings do not modify AbyssToolKit. All implementation changes are under Assets.
