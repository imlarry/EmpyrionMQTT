# Tomography

The Tomography skill reconstructs a 3D model of an Empyrion structure from its
block data and renders it in an EDNA document tab. This document captures what
the skill does today and outlines exploration paths for where it can go.

A short reference of Tomography alongside other skills lives in
[SkillsOverview.md](SkillsOverview.md). This document goes deeper: design,
limitations, and forward-looking ideas.

---

## 1. Concept

In-universe, EDNA performs an AI-enhanced tomographic scan of a structure --
the same way medical imaging reconstructs internal volume from external
samples. The result is a navigable 3D model the player can fly through.

Out of universe the input is a block list returned by the game; the output is
whichever 3D interpretation of that data the user finds most useful.

The key reframe driving this document: **a scan produces a dataset; rendering
decisions choose which interpretation (view) to display.** A single scan
should support multiple views without rescanning -- a wall-only architectural
view, a systems schematic with weapons / power / O2 markers, a translucent
X-ray, a damage map, and so on.

---

## 2. Current implementation

Code layout under `EDNAClient/Skills/Tomography/`:

| File | Role |
|---|---|
| [TomographySkill.cs](../../EDNAClient/Skills/Tomography/TomographySkill.cs) | Lifecycle, nav-root context menu, document open/save/delete. |
| [TomographyScanner.cs](../../EDNAClient/Skills/Tomography/TomographyScanner.cs) | MQTT scan + per-mode geometry builders. Holds the preset definitions, the skipped-category set, and the calibrated 24-entry rotation table. |
| [TomographyDocument.cs](../../EDNAClient/Skills/Tomography/TomographyDocument.cs) | POCO with mesh arrays; rebuilt into a `MeshGeometry3D` on load. JSON-serialized to disk. |
| [TomographyPanel.xaml.cs](../../EDNAClient/Skills/Tomography/TomographyPanel.xaml.cs) | WPF `Viewport3D` view with free-fly camera. |

Supporting catalogs (shared with other skills) under `EDNAClient/Core/`:

| File | Role |
|---|---|
| [BlockClassifier.cs](../../EDNAClient/Core/BlockClassifier.cs) | Block Type -> `BlockCategory` table. |
| [BlocksConfig.cs](../../EDNAClient/Core/BlocksConfig.cs) | Parses `BlocksConfig.ecf` for `ChildShapes` lists; `ResolveShape(type, shapeIndex)` returns the canonical shape name. |
| [ShapeBake/ShapeStampCatalog.cs](../../EDNAClient/Core/ShapeBake/ShapeStampCatalog.cs) | Loads `shapes.bake` (produced by `Tools/ShapeBaker`) at startup; `GetStamp(name)` returns a sub-voxel occupancy stamp. |

### Scan pipeline

1. `Player/GetProperties` returns `CurrentStructure.EntityId` (or null if not
   inside a structure).
2. `Structure/GetAllBlocks {EntityId}` returns a tabular payload with columns
   `[X, Y, Z, Type, HitPoints, Active, Shape, Rotation]`.
3. The selected mode's builder iterates rows: filter via `BlockClassifier` +
   `SkippedCategories`, resolve each block's stamp via
   `BlocksConfig.ResolveShape` + `ShapeStampCatalog.GetStamp`, apply the
   per-block rotation matrix to the stamp's voxels, and emit geometry. Sharp
   emits per-voxel cubes with intra-stamp face culling; Blocky emits a single
   unit cube per block with neighbour culling.

Saved documents land at
`{saveGame}/Content/Mods/ESB/EDNA/skills/tomography/{ss}/{pf}/{entityId}.json`
and reappear in the nav tree on the next session.

### Category filtering

[BlockClassifier.cs](../../EDNAClient/Core/BlockClassifier.cs) is the single
source of truth for block-Type -> `BlockCategory` mapping. The scanner drops
any block whose category is in its `SkippedCategories` set -- `EmptySpace`
and `Truss` today -- before emitting geometry.

### Presets

Each preset is a context-menu entry on the Tomography nav root.

| Preset | Mode | Purpose |
|---|---|---|
| Sharp (default) | VoxelCubes | Per-block stamp + rotation as tiny cubes. The only mode that renders blocks at full shape fidelity. |
| Blocky | Blocky | One unit cube per kept block with neighbour face culling; ignores Shape. Coarse overview. |
| Shape Gallery | Gallery | Diagnostic: arrays every baked stamp in a grid with hover labels. Shows what's baked and what's missing. |
| Rotation Atlas | RotationAtlas | Diagnostic: 24 cells of an asymmetric marker stamp, one per rotation index, for calibrating the rotation table against Empyrion's IBlock.Rotation indexing. |

### Viewer

WPF `Viewport3D` with free-fly camera and frame-timer-driven held-key
movement:

| Input | Action |
|---|---|
| Left-drag | Mouselook (yaw + pitch). |
| Right- or middle-drag | Pan perpendicular to view. |
| Mouse wheel | Forward / back along view. |
| W A S D | Move along view forward / strafe. |
| Space / C | Move along world up / down. |
| Shift | 3x sprint while held. |
| R | Recenter on the structure. |

---

## 3. Known limitations

- **Axis-aligned bias.** Empyrion structures are box-grid; Gaussian smoothing
  + marching cubes produces pillowy artifacts that misrepresent walls and
  corners.
- **Single baked mesh.** The document holds one mesh derived at scan time;
  comparing visual interpretations requires a full rescan.
- **No category awareness in render.** The mesh is a single undifferentiated
  surface; weapons, power, fuel, life-support all read identically even
  though we know each block's category.
- **"Preset" naming oversells what the four entries do.** They only vary
  smoothing and iso, not interpretation -- so the name blocks the broader
  view concept.

---

## 4. Reframe: views over presets

A **scan** produces a dataset: the raw block table plus the per-Type max-HP
map.

A **view** is one interpretation of that dataset. Views vary on three
orthogonal axes:

| Axis | Choices |
|---|---|
| Filter | Which `BlockCategory` values participate (all hull / functional only / windows only / mixed). |
| Reconstruction | How filled cells become geometry (density + MC, blocky faces, blocky + chamfer, point cloud, markers). |
| Rendering | How geometry is drawn (solid, X-ray translucent, wireframe, category-colored, damage-mapped). |

Storage implication: the document should retain the raw block table alongside
any baked mesh, so views can re-derive their geometry without rescanning.

---

## 5. Exploration paths

### 5a. Game-faithful blocky render via Shape catalog (leading candidate)

Use Empyrion's own configuration as the catalog instead of extracting Unity
assets:

- `Content/Configuration/BlockShapesWindow.ecf` defines the building-palette
  shape families (FullCube, Slope, Corner, HalfBlock, ThinBlock, ...). The
  file is plain text and stable across patches because modders depend on it.
- Per-block `Shape` (0-N) and `Rotation` (0-23, 24-orientation) come from the
  game's `IBlock.Get(...)` -- the same fields we currently drop in
  `Structure/GetAllBlocks`. They need to come back.

**At EDNA startup:**

1. Resolve the Empyrion install path (one level up from EDNA's executable
   directory; see `ProvisioningDetector` for the path-discovery pattern
   already used elsewhere).
2. Parse `BlockShapesWindow.ecf` once into an in-memory shape catalog.
3. For each shape family build a canonical parametric `MeshGeometry3D` (cube,
   half-cube, slope, corner, thin wedge, etc.) and `Freeze()` it. About 20
   meshes total -- tiny memory footprint.

**During scan:**

- Restore `Shape` and `Rotation` to the `Structure/GetAllBlocks` payload
  (reverses an earlier simplification; the ESB-side change is two extra
  columns).
- For each row, build a 4x4 transform from Rotation and instance the matching
  parametric mesh at the block's XYZ.

**During render:**

- Mesh-merge per Shape x Rotation x Color to keep the WPF `Viewport3D`
  triangle count manageable (capital ships have 10K+ blocks).
- Color by Type via `BlockClassifier` for material / category cues.

**Strengths:**

- No Unity asset extraction; no EULA / licensing concerns.
- ecf is plain text -- robust against game patches that would break binary
  asset parsing.
- Walls / floors / ceilings render axis-aligned and crisp; no marching-cubes
  artifacts.
- Drops naturally into the systems-schematic and architecture views below.

### 5a-alt. Simple voxel mesh ("Minecraft, no shape info")

Cheaper fallback path if Shape data is not restored: treat every block as a
full cube and emit a face only where the neighbor is empty (standard voxel
meshing). Optional Laplacian smoothing or edge chamfer to soften the look.
Loses slope and half-block detail but is artifact-free and very fast --
useful as an early proof-of-concept before the ecf parser lands.

### 5b. Systems schematic view

Filter to non-hull (functional) categories from `BlockClassifier` and render
the hull as a faint wireframe or ghost shell. Each functional block becomes a
colored marker -- small sphere or stylized icon -- at its cell center.

Suggested initial palette:

| Category | Color |
|---|---|
| Weapon, ShieldGenerator | Red |
| PowerGenerator, Capacitor | Orange |
| FuelTank | Yellow |
| OxygenTank, Utility (life-support) | Cyan |
| Container | Green |
| Cockpit, Console, LcdScreen | White |
| Sensor, Antenna | Purple |
| Constructor, Core | Lime |
| WarpDrive, CpuExtender | Bright pink |

This is the highest-value view -- it actualizes the original "weapons as red
spheres, power as orange spheres" vision and exercises everything the
`BlockClassifier` already knows.

### 5c. Architecture view

Filter to hull-classified (default `Unknown`) categories only and feed them
through the blocky reconstructor. Walls, floors, ceilings read cleanly with
no functional clutter -- the structure's bones.

### 5d. Skin / openings view

Render the hull as a translucent ghost; render `Window`, `Door`, hangar
doors, and blast doors as bright solids with optional outline. Highlights
the surface and entry points of the structure.

### 5e. X-ray view

Translucent hull (alpha ~0.35) with solid markers and functional devices
visible through walls. Implemented as a material/style swap on whichever
view is active; no rescan needed.

### 5f. Density / damage view

Color the hull mesh by `HitPoints / max-HP` per block. Damaged regions read
darker / duller. Useful for after-action assessment.

---

## 6. Implementation roadmap

Ordered by value-to-effort. Each step is shippable on its own. Marching
cubes stays in place as the "fuzzy scan" view -- the new path lives
alongside, not in place of it.

1. **Refactor `TomographyDocument` to retain raw blocks** (small JSON; baked
   meshes stay as an optional cache).
2. **Restore `Shape` and `Rotation` columns** to `Structure/GetAllBlocks`
   (reverses the prior simplification; required for 5a).
3. **Resolve install-path and parse `BlockShapesWindow.ecf`** into an
   in-memory shape catalog at EDNA startup. Build the parametric
   `MeshGeometry3D` cache.
4. **Introduce a `ViewMode` concept** distinct from `TomographyPreset`.
   Presets remain reconstruction-tuning knobs for the fuzzy view; views are
   a new dimension on the panel.
5. **Game-faithful blocky reconstructor** (5a). Replaces or complements MC
   for the architecture view.
6. **Systems schematic view** (5b) using `BlockClassifier` markers. Highest-
   value visualization.
7. **Architecture view** (5c) layered on the blocky reconstructor.
8. **X-ray rendering style** (5e) as a material swap on any view.
9. **Skin / openings view** (5d).
10. **Density / damage coloring** (5f) -- last because it needs custom
    material handling.

---

## 7. Open design questions

- Persist the raw block table on disk, or rescan on demand? (Storage cost vs
  flexibility.)
- Sub-voxel resolution: stay 1 voxel/block, or oversample for chamfered /
  smoothed paths?
- Per-document persistent view selection, or always open in a configured
  default view?
- Do the four current reconstruction presets survive once views land, or
  collapse to a single sensible default?
- Should views be selectable from the nav-root context menu, the document
  tab, or both?
- Does the marching-cubes "fuzzy scan" pipeline stay long-term, or get
  retired once the blocky / ecf path proves itself?
- ecf parsing: hand-rolled mini-parser or pull in a YAML/INI-ish library?
  (ecf has a bespoke syntax with braces, indent, comments.)
- Beyond `BlockShapesWindow.ecf`, which other ecf files are worth ingesting?
  Candidates: the main blocks config (HP, mass, material), `Templates.ecf`
  (item recipes), faction / predator definitions.
