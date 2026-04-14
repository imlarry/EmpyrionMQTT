"""
floormap.py -- render a V2.Structure.ScanFloor response as ASCII art.

Usage:
    python floormap.py < floor.json
    mosquitto_sub ... | python floormap.py

Input: the JSON payload from Client/R/V2.Structure.ScanFloor/#
  {
    "EntityId": 1007,
    "Y": 178,
    "MinPos": {...},
    "MaxPos": {...},
    "Blocks": [{"X": -8, "Z": 3, "Type": 406}, ...]
  }

Block type legend (adjust as you discover more):
  #   406  -- hull plate (main floor/wall)
  +   416  -- corner or special plate
  D   407  -- door
  E   461  -- equipment / device
  ~   884  -- open floor grating (walkable surface)
  ?        -- any other non-zero type
  .        -- empty (not in payload)
"""

import json
import sys


LEGEND = {
    406:  "#",   # HullArmoredFullLarge (wall=# floor=_)
    407:  "-",   # HullArmoredThinLarge
    416:  "+",   # TrussCube
    461:  "S",   # StairsMS
    468:  "^",   # ElevatorMS
    691:  "\\",  # RailingSlopeLeft
    692:  "/",   # RailingSlopeRight
    884:  "~",   # WalkwayVertNew
    1189: "W",   # Window_L1x1Thick
    1738: "V",   # SVDecoVent01
    1787: "=",   # HullArmoredExtendedLarge
    1978: "\\",  # RailingSlopeMetalLeft
    1980: "/",   # RailingSlopeMetalRight
}


def render(data):
    blocks = data.get("Blocks", [])
    if not blocks:
        print(f"Floor Y={data.get('Y')} -- no blocks returned.")
        return

    entity_id = data.get("EntityId", "?")
    y         = data.get("Y", "?")

    min_x = min(b["X"] for b in blocks)
    max_x = max(b["X"] for b in blocks)
    min_z = min(b["Z"] for b in blocks)
    max_z = max(b["Z"] for b in blocks)

    width  = max_x - min_x + 1
    height = max_z - min_z + 1

    grid = [["." for _ in range(width)] for _ in range(height)]

    # Build occupied set for edge detection
    occupied = {(b["X"], b["Z"]) for b in blocks}

    for b in blocks:
        x = b["X"] - min_x
        z = b["Z"] - min_z
        ch = LEGEND.get(b["Type"], "?")
        # For solid hull blocks, distinguish wall (exposed edge) from interior floor
        if b["Type"] == 406:
            bx, bz = b["X"], b["Z"]
            exposed = any((bx+dx, bz+dz) not in occupied
                          for dx, dz in ((1,0),(-1,0),(0,1),(0,-1)))
            ch = "#" if exposed else "_"
        grid[z][x] = ch

    types_present = sorted(set(b["Type"] for b in blocks))

    print(f"Entity {entity_id}  Floor Y={y}  {width}x{height} cells  {len(blocks)} blocks")
    print(f"Types present: {types_present}")
    print()

    # Z axis label on the side, X axis along the top
    x_labels = [str(min_x + i) for i in range(0, width, 5)]
    print("    " + "     ".join(x_labels))

    for zi, row in enumerate(grid):
        z_label = str(min_z + zi)
        print(f"{z_label:>4} {' '.join(row)}")

    print()
    print("Legend:")
    shown = {}
    for t in types_present:
        ch = LEGEND.get(t, "?")
        if ch not in shown:
            shown[ch] = t
            print(f"  {ch}  Type {t}")


def main():
    raw = sys.stdin.read().strip()
    # mosquitto_sub -v prefixes lines with "topic payload" -- strip topic if present
    if raw.startswith("Client/"):
        raw = raw.split(" ", 1)[1]
    data = json.loads(raw)
    render(data)


if __name__ == "__main__":
    main()
