# EDNA Tomography Rotation Calibration

## Context

EDNA's Tomography skill reconstructs an in-game structure by splatting each
block's baked voxel stamp into a density field and running marching cubes.
Each block carries an integer `Rotation` 0-23 from the game's
`IBlock.Get(...)` call. The scanner looks up a 3x3 transform from
`Rotations24[rotation]` ([TomographyScanner.cs:138-183](EDNAClient/Skills/Tomography/TomographyScanner.cs#L138-L183))
and rotates the stamp voxels before splatting.

The reconstructed surface did not match the in-game appearance for most
rotation indices. Root cause: the textbook BFS ordering in
`GenerateRotations24()` does not match Empyrion's opaque rotation-index
encoding. Empyrion's encoding is `rotation = facing*4 + spin` where:

- `facing` (0-5) is which world direction the block's local +Y axis points
  to after rotation: 0=+Y, 1=-Y, 2=+Z, 3=+X, 4=-Z, 5=-X.
- `spin` (0-3) is the intrinsic CW yaw count around the block's local +Y
  axis, with `rot(N+1) = rot(N) * R_y(+90 deg)` (right-multiplication).

This was determined by an in-game calibration session in May 2026 against a
core + Y/X/Z arms + verification block structure, with HUD-anchored world
axes and visual orientation confirmation per ramp.

## Calibration data

**11 of 24 directly verified** by placing ramps at known rotations and
reading back the rotation int + visual orientation:

| Index | Matrix | Notes |
|---|---|---|
| 0 | I | identity, anchor; wedge faces -Z (south) |
| 1 | R_y(+90 deg) | Y arm step 1, wedge faces -X |
| 2 | R_y(180 deg) | Y arm step 2, wedge faces +Z |
| 3 | R_y(+270 deg) | Y arm step 3, wedge faces +X |
| 4 | R_z(180 deg) | Z arm step 2 (facing 1 spin 0) |
| 6 | R_x(180 deg) | X arm step 2 (facing 1 spin 2) |
| 10 | R_x(+90 deg) | X arm step 1 (facing 2 spin 2), wedge tall wall +Y |
| 11 | R_x(+90 deg) * R_y(+90 deg) | verification block: tip X + yaw |
| 12 | R_z(-90 deg) | Z arm step 1 (facing 3 spin 0) |
| 16 | R_x(-90 deg) | X arm step 3 (facing 4 spin 0) |
| 20 | R_z(+90 deg) | Z arm step 3 (facing 5 spin 0) |

The intermediate "isolated block" sample (rotation 15 from a tip X + tip Z
sequence) was discarded -- the user moved to the other side of the
structure between rotations, so "CW from my perspective" wasn't a consistent
axis convention across the two presses.

**13 of 24 derived by hypothesis**, not directly verified: 5, 7, 8, 9, 13,
14, 15, 17, 18, 19, 21, 22, 23. These follow from the rule
`rot(N+1 in facing) = rot(N) * R_y(+90 deg)` applied to each facing's spin-0
anchor. Visual verification at runtime via the Sharp preset on a dense
test structure is the acceptance gate for these.

## Full 24-entry table (Empyrion index order)

Row-major 3x3, values in {-1, 0, 1}. **V** = directly verified.
**H** = hypothesis (rule-derived from a verified anchor).

```
rot  0: { 1, 0, 0,   0, 1, 0,   0, 0, 1 }   V  identity
rot  1: { 0, 0, 1,   0, 1, 0,  -1, 0, 0 }   V  R_y(+90)
rot  2: {-1, 0, 0,   0, 1, 0,   0, 0,-1 }   V  R_y(180)
rot  3: { 0, 0,-1,   0, 1, 0,   1, 0, 0 }   V  R_y(+270)
rot  4: {-1, 0, 0,   0,-1, 0,   0, 0, 1 }   V  R_z(180)        facing 1 spin 0
rot  5: { 0, 0,-1,   0,-1, 0,  -1, 0, 0 }   H
rot  6: { 1, 0, 0,   0,-1, 0,   0, 0,-1 }   V  R_x(180)        facing 1 spin 2
rot  7: { 0, 0, 1,   0,-1, 0,   1, 0, 0 }   H
rot  8: {-1, 0, 0,   0, 0, 1,   0, 1, 0 }   H                  facing 2 spin 0
rot  9: { 0, 0,-1,  -1, 0, 0,   0, 1, 0 }   H
rot 10: { 1, 0, 0,   0, 0,-1,   0, 1, 0 }   V  R_x(+90)        facing 2 spin 2
rot 11: { 0, 0, 1,   1, 0, 0,   0, 1, 0 }   V  facing 2 spin 3
rot 12: { 0, 1, 0,  -1, 0, 0,   0, 0, 1 }   V  R_z(-90)        facing 3 spin 0
rot 13: { 0, 1, 0,   0, 0,-1,  -1, 0, 0 }   H
rot 14: { 0, 1, 0,   1, 0, 0,   0, 0,-1 }   H
rot 15: { 0, 1, 0,   0, 0, 1,   1, 0, 0 }   H
rot 16: { 1, 0, 0,   0, 0, 1,   0,-1, 0 }   V  R_x(-90)        facing 4 spin 0
rot 17: { 0, 0, 1,  -1, 0, 0,   0,-1, 0 }   H
rot 18: {-1, 0, 0,   0, 0,-1,   0,-1, 0 }   H
rot 19: { 0, 0,-1,   1, 0, 0,   0,-1, 0 }   H
rot 20: { 0,-1, 0,   1, 0, 0,   0, 0, 1 }   V  R_z(+90)        facing 5 spin 0
rot 21: { 0,-1, 0,   0, 0, 1,  -1, 0, 0 }   H
rot 22: { 0,-1, 0,  -1, 0, 0,   0, 0,-1 }   H
rot 23: { 0,-1, 0,   0, 0,-1,   1, 0, 0 }   H
```

## Implementation

### Step 1: Replace rotation table in TomographyScanner

In [EDNAClient/Skills/Tomography/TomographyScanner.cs](EDNAClient/Skills/Tomography/TomographyScanner.cs):

- Replace `GenerateRotations24()` ([lines 150-183](EDNAClient/Skills/Tomography/TomographyScanner.cs#L150-L183))
  with a static literal initialization of the 24 calibrated matrices in
  Empyrion index order. Static init throws `InvalidOperationException` if
  the array length is not 24 or any entry is not a proper rotation, to
  match the existing style at [lines 179-181](EDNAClient/Skills/Tomography/TomographyScanner.cs#L179-L181).
- Add a comment block above the table noting: 11 entries (V) directly
  verified by in-game calibration May 2026; 13 entries (H) derived from
  the rule `rot(N+1 in facing) = rot(N) * R_y(+90 deg)` and pending visual
  verification via the Sharp preset on a dense test structure.
- Remove the `MatKey` and `Multiply3x3` helpers ([lines 185-199](EDNAClient/Skills/Tomography/TomographyScanner.cs#L185-L199))
  -- they were only used by the BFS generator.
- The usage sites at [lines 447-458](EDNAClient/Skills/Tomography/TomographyScanner.cs#L447-L458)
  (density splat) and `RotateStamp` (~line 655, VoxelCubes mode) stay
  unchanged -- they were already correct; only the table contents change.

### Step 2: Restore window rendering

Remove the temporary `BlockCategory.Window` entry from `SkippedCategories`
at [lines 121-125](EDNAClient/Skills/Tomography/TomographyScanner.cs#L121-L125).
The comment block in those lines explicitly identifies it as a debug
workaround tied to the rotation issue.

### Step 3: Update the RotationAtlas labels

The atlas at [TomographyScanner.cs:855-904](EDNAClient/Skills/Tomography/TomographyScanner.cs#L855-L904)
renders all 24 rotations in a grid. Update the cell label format from
`"Rot N  (marker +X=4, +Y=3, +Z=2)"` to also include the facing/spin
decomposition, e.g. `"Rot N  facing F spin S"`, so the atlas remains a
useful diagnostic if the encoding ever changes in a future Empyrion
version.

## Files to modify

- [EDNAClient/Skills/Tomography/TomographyScanner.cs](EDNAClient/Skills/Tomography/TomographyScanner.cs)
  -- rotation table, helpers cleanup, window category restore, atlas labels.

No changes needed elsewhere. The bake format ([Tools/ShapeBaker/](Tools/ShapeBaker/)),
runtime loader ([EDNAClient/Core/ShapeBake/](EDNAClient/Core/ShapeBake/)),
shape resolver ([EDNAClient/Core/BlocksConfig.cs](EDNAClient/Core/BlocksConfig.cs)),
and WPF mesh handedness flip in
[EDNAClient/Skills/Tomography/TomographyDocument.cs](EDNAClient/Skills/Tomography/TomographyDocument.cs)
are all correct as-is.

## Verification

1. User triggers `dotnet build`.
2. User runs EDNA in-game alongside Empyrion. Builds a dense test
   structure: a few dozen ramp blocks placed at every rotation index 0-23
   (cycling through tips and yaws to hit them all). Place at distinct
   structure-local positions so each rotation is individually identifiable
   in the scan.
3. User runs Tomography with the Sharp preset against this structure. The
   reconstructed surface should match the in-game view for every block --
   no orientation discrepancies, no Swiss-cheese holes, no mirrored ramps.
4. If any of the 13 hypothesis (H) entries renders incorrectly, the wrong
   matrix is identified by which rotation index appears wrong in the scan
   versus the in-game view. Correction: replace that entry in the table
   and rebuild. Mark the corrected entry V in the comment.
5. Once Sharp pass is clean, run the `RotationAtlas` preset. All 24 cells
   should display the asymmetric marker in 24 distinct orientations. With
   the updated labels (facing/spin), confirm the spatial layout matches
   the encoding.
6. After verification: remove the temporary `Window` skip per Step 2 of
   the implementation and re-scan a window-containing structure to
   confirm windows render correctly.
