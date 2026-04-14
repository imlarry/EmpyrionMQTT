# EDNAmap -- Floor Map Skill

Top-down floor plan visualizer for Empyrion structures. The player stands inside a
structure, presses Refresh, and sees a 2D map of the horizontal slice at their level.

---

## What it does

1. Queries the player's world position and which structure they are inside
2. Converts the world position to structure-local block coordinates to determine the Y level
3. Scans two horizontal slices of the structure in parallel:
   - **Y** (wall level) -- blocks at eye level
   - **Y-1** (floor level) -- blocks underfoot
4. Classifies each (X,Z) cell:
   - **Wall** (dark gray) -- block at eye level only
   - **Floor** (light gray) -- block underfoot only, open at eye level
   - **WallFloor** (mid gray) -- block at both levels
   - **Empty** (black) -- no block at either level
5. Renders each cell as a 16x16 pixel top-down geometric silhouette using the block's
   shape ID and rotation
6. Overlays a cyan dot at the player's (X,Z) position
7. Displays the result in a scrollable WPF window

---

## MQTT Request Sequence

All requests target `Client/Q/...` (SinglePlayer). Multiplayer would need
`DedicatedServer` as the source -- a known limitation shared with ThreatTracker.

```
Step 1  ->  Client/Q/V2.Player/*/seq
            payload: {"Properties":["Position","CurrentStructureId"]}
        <-  Client/R/V2.Player/*/seq
            response: {"Position":{"X":f,"Y":f,"Z":f}, "CurrentStructureId":n|null}

Step 2  ->  Client/Q/V2.Structure.GlobalToStructPos/*/seq
            payload: {"EntityId":n, "Pos":{"X":f,"Y":f,"Z":f}}
        <-  Client/R/V2.Structure.GlobalToStructPos/*/seq
            response: {"StructPos":{"X":i,"Y":i,"Z":i}}

Step 3a ->  Client/Q/V2.Structure.ScanFloor/*/seq   (Y -- wall level)
Step 3b ->  Client/Q/V2.Structure.ScanFloor/*/seq   (Y-1 -- floor level)
        <-  Client/R/V2.Structure.ScanFloor/*/seq
            response: {"EntityId":n,"Y":i,"MinPos":{...},"MaxPos":{...},
                       "Blocks":[{"X":i,"Z":i,"Type":i,"Shape":i,"Rotation":i},...]}
```

Steps 3a and 3b are sent in parallel; each is awaited independently via a
`TaskCompletionSource` keyed by sequence number.

---

## Block Classification

```
wallSet  = {(X,Z)} from scan at Y
floorSet = {(X,Z)} from scan at Y-1

per (X,Z) cell:
  in both        -> WallFloor  #686868
  wall only      -> Wall       #383838
  floor only     -> Floor      #B0B0B0
  neither        -> Empty      (not drawn)
  player pos     -> cyan dot   #00CCCC (overlay on whatever cell)
```

---

## Shape Geometry (Path A)

Each block is rendered as a top-down silhouette using the `Shape` and `Rotation` values
returned by `ScanFloor` (via `IBlock.Get(type, shape, rotation, active)`).

Shapes are defined in `ShapeGeometry.cs` as normalized `Point[]` polygons (0..1 space),
scaled to the 16x16 cell at render time. Rotation maps `(rotation % 4) * 90` degrees.

### Known shape IDs

| Shape ID | Description | Top-down footprint |
|---|---|---|
| 0 | Full cube | Square (full cell) |
| 1 | Full cube variant | Square (full cell) |
| 2 | Slope / ramp | Right triangle |

Shape IDs are undocumented by Eleon. Add new entries to `ShapeGeometry._shapes` as
they are identified from live scan data. Unknown IDs fall back to the full square.

### Shape upgrade path (Path B)

When game texture sprites are available, replace `DrawGeometry` in
`FloorMapViewModel.Render` with `DrawImage(atlas[blockType], cellRect)`. The
classification color background and player dot overlay remain unchanged.

---

## Files

| File | Purpose |
|---|---|
| `ESB/TopicHandlers/V2/Structure.cs` | `ScanFloor` -- emits `Shape` and `Rotation` per block |
| `EDNAClient/Skills/FloorMap/ShapeGeometry.cs` | Shape ID -> top-down polygon catalog |
| `EDNAClient/Skills/FloorMap/FloorMapViewModel.cs` | Block classification + RenderTargetBitmap rendering |
| `EDNAClient/Skills/FloorMap/FloorMapWindow.xaml` | ScrollViewer + Image + Refresh button |
| `EDNAClient/Skills/FloorMap/FloorMapWindow.xaml.cs` | Code-behind; wires Refresh click |
| `EDNAClient/Skills/FloorMap/FloorMapper.cs` | MQTT request sequence; TCS-based request/response |
| `EDNAClient/Skills/FloorMap/FloorMapSkill.cs` | IEdnaSkill wrapper |
| `EDNAClient/App.xaml.cs` | Registers FloorMapSkill |

---

## Tile Size

16x16 pixels per block. A 60x60 block structure renders at 960x960 pixels, navigated
via the ScrollViewer. `NearestNeighbor` scaling ensures crisp edges if the window is
resized or zoomed in a future update.

---

## Known Limitations

- **SinglePlayer only**: ESB routing is hardcoded to `"Client"`. Multiplayer requires
  `EdnaContext.AuthoritativeSource` to be passed through to `FloorMapper`.
- **Shape IDs are empirical**: Eleon does not document shape ID values. The catalog
  in `ShapeGeometry.cs` must be extended from live scan data.
- **On-demand only**: The map is not updated automatically when the player moves to a
  different floor -- press Refresh to rescan.
- **No zoom / scale control**: The bitmap is rendered at 1:1 (16px per block). A zoom
  slider could be added by wrapping the ScrollViewer content in a `ScaleTransform`.
