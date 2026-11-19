# Hex Spell system

## Quick start

`Assets/Scenes/S_MainGame.unity` is already wired. Enter Play Mode, select a bottom spell slot,
press **Rotate 60°** to choose one of six headings, and left-click a movable floor hex.
The old `HexWavePointerInput` component is retained but disabled to avoid double attacks.
The test UI and EventSystem (when absent) are built at runtime, so no additional Canvas setup is needed.

Two reference-inspired samples live in
`Assets/A_Systems/HexSpellSystem/AllSOs/HexSpellSOs/`:

| Asset | Direct impulses | Allowed cells | Anchor |
| --- | --- | --- | --- |
| `HexSpellPP_Line.asset` | 4 | 16 | Leftmost red cell |
| `HexSpellPP_Triangle.asset` | 3 | 12 | Lowest red cell |

All sample impulses initially fire at 0 seconds with +8 impulse per cell. The region is the
union of each red cell and its immediate neighbors, giving a closed one-cell surrounding band.
The blue outlines are snapped to hex edges rather than the hand-drawn curves in the reference.

## Data and registration

`Key_HexSpellPP` is a persistent identifier enum: append new values rather than reordering them.
`HexSpellProperty` inherits `EnumStringKeyedProperty<Key_HexSpellPP>`.
`HexSpellDatabase` inherits `EnumStringKeyedDatabase<HexSpellProperty, Key_HexSpellPP>`.

The runtime lookup is:

`PropertyDatabaseManager.Instance.GetDatabase<HexSpellDatabase>().GetByEnum(key)`

`HexSpellDB_main.asset` has its family-specific scanFolder configured and its entries collected.
It is registered once on `Assets/A_Systems/AInitGameObject Variant.prefab`.
SpellManager only serializes slot enum keys, not a database reference. The editor may select a
Property asset directly for editing. No AbyssToolKit files were changed.

New assets are available through **Create > AllProperties > HexSpellProperty** and
**Create > AllPropertyDatabases > HexSpellDatabase**. When adding a new skill:

1. Append its enum key; create and configure its Property in the family folder.
2. Give it a unique non-None enum key; leave stringKey empty.
3. Run **Collect Entries From Folder** on the database.
4. Add its enum key to SpellManager's Slots array.

The explicit **Tools > Hex Wave > Install Spell Test In Active Scene** command can wire a
different existing HexWave map. It creates missing samples/materials, collects and registers
the database, attaches test components, and saves the scene. It does not overwrite existing
sample spell configurations. It is not an automatic initializer or map generator.

## Configuration

- `region`: unique cube-coordinate offsets relative to the anchor; must include `(0,0,0)`.
- `impulses`: offset, time in seconds since cast, signed impulse. Multiple events at one offset
  are supported. Every event offset must belong to the region.
- `strength`: asset-wide multiplier; final impulse is event impulse × asset strength × cast multiplier.
- `edgeMode`: Absorb (default), Free, or Fixed. This applies to the spell mask perimeter only.
- `edgeDamping` / `absorptionWidth`: extra damping near mask edges in Absorb mode. Absorption
  reduces reflection, but does not promise mathematically reflection-free boundaries.

Positive impulse adds upward velocity; negative impulse adds downward velocity. These are
impulses, not target heights: `deltaVelocity = impulse / mass`. Height and velocity can cancel
or add to contributions already present. Heights have no hard clamp.

Direction zero points along the existing upper-right neighbor `(1,0,-1)` (30° above map +X).
Each direction step rotates cube coordinates by 60°, using `(a,b,c) -> (-c,-a,-b)`.
Six steps return exactly to the original pattern, with no world-coordinate rounding.

## Casting, walls and simulation

The clicked anchor must exist and must not be fixed. Each authored event then checks its own
target cell independently. Wall/missing targets are skipped and their impulse is not redistributed.
There is no line-of-sight or origin-to-event reachability test: an authored direct source on the
far side of a wall is allowed. Ordinary wave transmission cannot go through that wall.

`HexCellView.isWall` is the authored map flag. `HexWaveManager` combines it with the existing
automatic boundary rule (any missing neighbor means a fixed boundary). These flags feed both
enemy navigation and wave simulation. The Grid Painter has **Wall / Floor** brushes; **Floor**
clears the authored flag but does not override automatic outer boundary fixation.
Painting topology/walls rebuilds the grid and resets active waves. If editing the flag directly
in the Inspector, run **Rebuild Grid Topology** afterward.

Spell masks do not modify these global map flags. Each accepted cast creates a compact,
isolated `HexSpellWave` containing only valid cells inside its rotated region. Neighbor springs
couple those cells. A scene wall is a zero-height fixed support; an open spell edge has no
outside coupling; a fixed spell edge has a zero-height support. Absorb mode adds edge damping.
An available route around a wall inside the region lets the simulated wave travel around it.

Events at time zero are submitted immediately; delayed events fire at their configured time,
splitting a simulation interval at event timestamps. Direction, origin, events, strength and
physics parameters are captured when casting, so later selection/asset changes do not alter it.
The physics parameters are sampled from the map Manager; editor preview uses the selected
map's same settings, or documented defaults if no source is assigned.

`PendingCastCount` becomes zero as soon as all accepted events have fired. The residual wave
state stays alive independently until all its cells meet the configured height/velocity sleep
thresholds for the required steps. There is no persistent Spell GameObject per cast.

HexWaveManager adds the contributions into its authoritative height/velocity snapshots. Existing
rendering interpolation and enemy absolute-height damage read those combined snapshots.
Independent waves may cancel visually without destroying their own residual simulation state.
Reset/rebuild clears both legacy and spell waves. Mechanical-energy diagnostics include all
independent wave layers, not just the legacy simulation.

The solver uses a shared previous-state snapshot and semi-implicit Euler integration. It chooses
additional substeps from mass, stiffness and damping to keep ordinary parameter changes stable.
No maximum simultaneous-cast limit or cooldown is imposed by this test controller.

## Preview

Game hover preview:

- Blue outline/fill: allowed footprint (gray where the anchor/target is invalid).
- Red: positive direct impulses; purple: negative direct impulses.
- White SphereField marker and arrow: casting anchor and heading.
- Gray cross: invalid target; no collider is created for range markers.
- Labels show total signed impulse at that offset and its earliest event time. The Spell Editor
  shows every separate event, including repeated hits at the same cell.

Markers follow the displayed column tops without changing the terrain's material/color state.
Hovering the UI, leaving the map, input locking, or game failure hides the preview and blocks casts.
`M_SpellAnchor` is a project-owned copy of the package's white SphereField material. Overlay
and editor preview shaders are project-owned; the package shader/material is not modified.

Open **Tools > Hex Wave > Spell Editor**, or use **Open Spell Editor / Live Preview** in a
Property Inspector. The top view edits region cells and impulse events; the lower view renders
actual column movement in an isolated PreviewRenderUtility scene.

- `Select`: inspect all events at a cell; adjust their times and impulses or delete them.
- `Region + / Region −`: paint allowed cells; erasing a region cell also removes its events.
- `Impulse +`: add an event using Paint impulse / Paint time (and include the cell in the region).
- `Test Wall`: toggle a local wall; these walls are never saved to the game map or spell asset.
- `Grid k × k`: axial parallelogram test map; the outer perimeter remains fixed, matching game rules.
- Play/Pause, Replay, Step 0.02s, Add Cast, Loop, playback speed and six headings are supported.
- `Save Asset`: save edits; painting/event edits support Undo/Redo. Configuration edits reset preview.

## Verification

Run **Tools > Hex Wave > Verify Spell Simulation** outside Play Mode. The checks create only
an isolated preview scene and temporary in-memory data. They cover database lookup, six-way
rotation, stable finite motion, decay/recycling, strict mask isolation, wall blocking, wall detours,
independent direct sources, event timing, frozen cast data, addition/cancellation, and map walls.

Also verify in S_MainGame: slot buttons, rotation, hover preview, casting near walls/map edges,
repeated casts, UI click blocking, and enemy damage. Screenshots used during development are
kept under `Library/HexSpellVerification`, not imported as project assets.
