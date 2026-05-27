# Tomography: multi-cell window rotation diagnostic

## Status

Open. Diagnostic phase not yet executed. Pickup point for a fresh session.

## Context

Tomography Sharp now resolves Window/Door/Walkway blocks via `ResolveClassStamp`
in [TomographyScanner.cs](../../EDNAClient/Skills/Tomography/TomographyScanner.cs),
which reads each block's `Model` field from BlocksConfig and returns the last
path segment as the stamp name (e.g. block 770 -> `Window_v1x1Prefab`).

Prefab stamps are baked from `Content/Bundles/models` by
[Tools/ShapeBaker](../../Tools/ShapeBaker/Program.cs), filtered by
[BlocksConfigPrefabReader](../../Tools/ShapeBaker/BlocksConfigPrefabReader.cs)
to single-cell (`SizeInBlocks` absent or `"1,1,1"`) Window / Door / Walkway /
Hangar prefabs. Multi-cell block types (Window_v1x2 = 796, Window_v2x2 = 798,
Hangar* and Shutter* doors) substitute the 1x1 sibling's prefab via
`FindOneCellSiblingName` (regex replace of the size token, e.g. `1x2 -> 1x1`)
and rely on cross-cell face culling in `EmitStampVoxelCubes` to render the
cluster as one continuous mesh.

Build pipeline is automatic: a `ProjectReference` from EDNA to ShapeBaker
triggers a build-time bake whenever ShapeBaker code changes. See
`Tools/ShapeBaker/ShapeBaker.csproj` (`RunShapeBaker` target) and
`EDNAClient/EDNA.csproj` (ProjectReference with `ReferenceOutputAssembly=false`).

## Problem

Real-world building (greenhouse roof made of Window_v1x1 + Window_v1x2 +
Window_v2x2 in mixed sizes) renders with each cell of a multi-cell plate at a
different orientation: some panes sit at +Z of their cell, others at -Z, some
appear to be on +Y. From a distance the multi-cell plates look fragmented
instead of forming one continuous flat sheet.

The 1x1 cells in the same plate render correctly. Curved corner windows
(Window_c1x1 etc.) also render correctly. The bug is specific to multi-cell
flat variants.

## Hypothesis

For a multi-cell block instance, each occupied cell's `(Shape, Rotation)`
returned by `IBlock.Get` is **not identical**. The per-cell values encode
the cell's position within the cluster (e.g. for a 2x2: bottom-left, bottom-
right, top-left, top-right each get a distinct (Shape, Rotation) tuple) so
the game's renderer can pick the correct slice of the authored multi-cell
mesh.

Our code stamps the 1x1 sibling prefab at each cell with that cell's own
Rotation, producing four different orientations across a 2x2 cluster --
visibly broken in exactly the way observed.

The same encoding almost certainly applies to multi-cell doors (HangarDoors
5x3..14x7, ShutterDoors 2x2..5x5).

## Diagnostic plan

### Test playfield

Place each block type in isolation, well-separated from neighbours, at
known coordinates:

1. `Window_v1x1` (Type 770) -- control, single cell.
2. `Window_v1x2` (Type 796) -- 2 cells stacked Y.
3. `Window_v2x2` (Type 798) -- 4 cells in a 2x2 layout.

For each, place one copy at default rotation, then a second copy rotated
to face a different direction. Six instances total.

### Logging hook

Add a one-shot diagnostic in `TomographyScanner.BuildVoxelCubes` that, on
each scan, logs every Window-category cell's `(world X, world Y, world Z,
Type, Shape, Rotation, block-name)`. Guarded by a bool constant so it's
trivially togglable.

Sketch:

```csharp
private const bool LogMultiCellDebug = true;

// inside the kept-rows scan loop in BuildVoxelCubes:
if (LogMultiCellDebug)
{
    var cat = BlockClassifier.Classify(b.Type);
    if (cat == BlockCategory.Window || cat == BlockCategory.Door)
    {
        var name = BlocksConfig.GetById(b.Type)?.Name ?? "";
        EdnaLogger.Log(
            "[MultiCellDbg] cell=(" + b.X + "," + b.Y + "," + b.Z +
            ") type=" + b.Type + " shape=" + b.Shape +
            " rot=" + b.Rotation + " name=" + name);
    }
}
```

### What to read off the log

For each test placement:

- **Window_v1x1**: one row. Record (Shape, Rotation) as the single-cell baseline.
- **Window_v1x2**: two rows at adjacent Y. Compare (Shape, Rotation). Do they match each other? Do they match the equivalent 1x1?
- **Window_v2x2**: four rows in a 2x2 layout. Compare all four. Hypothesis predicts they are distinct.

Then for the rotated copies, repeat and compare to the un-rotated copies.
We should be able to infer:

- Whether `Shape` varies per cell.
- Whether `Rotation` varies per cell.
- The variance pattern (axis-aligned permutation? cluster-relative offset?
  `facing*4 + spin` where spin encodes corner position?).
- How variance changes with the block's overall orientation.

## Fix once the rule is known

Two edits expected in `TomographyScanner.BuildVoxelCubes`:

1. **Cluster detection (pass 0)**: scan kept rows once before the existing
   pass 1 to flood-fill contiguous regions of same-Type cells where the
   Type belongs to a multi-cell block. For each cluster, choose a canonical
   "anchor" cell (likely the min-X-then-Y-then-Z corner) and read its
   (Shape, Rotation) as the cluster's orientation.
2. **Rotation override**: in pass 1, when a cell is part of a multi-cell
   cluster, substitute the cluster's anchor (Shape, Rotation) for the cell's
   own values before resolving the stamp and rotating it. All cells of the
   cluster then render the same prefab at the same orientation; cross-cell
   face culling (which gates on same-(Type, Shape, Rotation)) hides the
   inner seams automatically.

Multi-cell type detection requires `SizeInBlocks` from BlocksConfig.
EDNA's `BlocksConfig.cs` currently parses Name/Ref/Shape/Model/Material/
Category/Class/ChildShapes/ChildBlocks; add SizeInBlocks the same way --
one new string property on `BlockDef` and one switch case in `ApplyKv`.
Ref-inheritance resolution should inherit SizeInBlocks like other fields.

Helper: a multi-cell block is one where `SizeInBlocks` is set and not
`"1,1,1"`. Anything else (single-cell, default, or missing) is 1x1x1.

## Out of scope (intentional)

- Reading `ParentBlock` from `IBlock` in ESB.Structure.GetAllBlocks. That
  is the architecturally clean fix and is described in
  `~/.claude/plans/there-are-three-classes-sorted-liskov.md` under "Out of
  scope: cost of true multi-cell rendering". We stay with the client-side
  flood-fill heuristic.
- Baking multi-cell prefabs natively. Stays out; cross-cell tiling of 1x1
  prefabs is the strategy.

## Files touched in the session that produced this issue

Listed for context when picking up cold:

- `Tools/ShapeBaker/BlocksConfigPrefabReader.cs` (new)
- `Tools/ShapeBaker/Program.cs` (models-bundle pass; +0.5 Y offset for
  prefab pivot; per-bundle position offset arg on `ReadAndDecode`)
- `Tools/ShapeBaker/BundleReader.cs` (accepts `SkinnedMeshRenderer` so
  animated doors decode)
- `Tools/ShapeBaker/ShapeBaker.csproj` (`RunShapeBaker` auto-bake target,
  incremental on `$(TargetPath)` vs `shapes.bake` mtime, guarded by
  `Exists(...shapes bundle...)`)
- `EDNAClient/EDNA.csproj` (ProjectReference to ShapeBaker with
  `ReferenceOutputAssembly=false` so the bake runs but the .NET 8 console
  outputs do not pollute EDNA's bin)
- `EDNAClient/Skills/Tomography/TomographyScanner.cs` -- `ResolveClassStamp`
  reads BlocksConfig Model; per-class buckets (Window / Door / Walkway);
  cross-cell culling already shipped earlier in the same session
- `EDNAClient/Skills/Tomography/TomographyDocument.cs` -- DoorIndices /
  WalkwayIndices / DoorMesh / WalkwayMesh buckets
- `EDNAClient/Skills/Tomography/TomographyPanel.xaml.cs` -- door (dark
  slate) and walkway (amber-brown) materials
- `Tools/ShapeBaker/find-dupes.ps1` and `list-stamps.ps1` (diagnostic
  utilities)

## Suggested next-session pickup order

1. Read this doc plus `TomographyScanner.cs` `ResolveClassStamp` and
   `BuildVoxelCubes`. Confirm understanding of current rendering pipeline.
2. Add the `LogMultiCellDebug` diagnostic in `BuildVoxelCubes` (sketch
   above). Ship as a single edit.
3. Ask the user to build the test playfield and run a Sharp scan.
4. Read the log output. Infer the per-cell encoding rule.
5. Add SizeInBlocks parsing to `EDNAClient/Core/BlocksConfig.cs`.
6. Implement cluster detection + rotation override in `BuildVoxelCubes`.
7. Re-test on the original greenhouse roof to confirm the fragmented
   multi-cell plates now read as continuous sheets.
8. Remove (or leave disabled) the `LogMultiCellDebug` flag.
