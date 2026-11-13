# Map format

Maps live in `Assets/Wardline/Resources/Maps/*.txt`. Each file has a short header, a `---` line, and an ASCII
grid. Each character is one **3 m × 3 m** cell. Row 0 is north.

```
name: Desert Strike
theme: desert            # desert | urban | arctic | lab | tropical
description: One-line description shown on the map select screen.
---
####################
#..AAAA..WWWW..BBB.#
...
```

| Char | Meaning | Walkable |
|---|---|---|
| `#` | Wall (about 5 m tall). Horizontal runs merge into single boxes | no |
| `.` | Floor | yes |
| `A` / `B` | Charge site floor. The site's extent is the bounding box of these cells | yes |
| `R` / `W` | Raider / Warden spawn and buy zone | yes |
| `1` `2` `3` | Lane waypoints. Bots work out which lane feeds site A, which feeds B, and which is mid | yes |
| `g` | Guard spot: holding positions for defenders | yes |
| `p` | Raised platform (1.6 m), used for elevated positions | yes (top) |
| `r` | Ramp up to an adjacent `p` | yes |
| `l` | Ceiling lamp (point light) | yes |
| `c` | Crate, about 1 m: crouch cover that you can jump onto | no |
| `h` | Tall crate stack, about 2.3 m: blocks sight lines | no |
| `w` | Low wall, about 1.1 m | no |
| `o` | Pillar, or a tree on the `tropical` theme | no |

## Rules (checked by `Tools/validate_maps.py`)

- The border is fully enclosed and all rows are the same width.
- Each site and spawn has at least 4 cells. Lanes `1`, `2` and `3` exist, and there are at least 2 guard spots.
- Every walkable cell is reachable from the Raider spawn (no dead pockets).
- Every ramp touches a platform.
- Wardens reach each site before Raiders, and the Raider route to a site is at most 40 cells (about 120 m).

```bash
python3 Tools/validate_maps.py            # all maps
python3 Tools/validate_maps.py my_map.txt # one file
```

## Adding a map

1. Create `Assets/Wardline/Resources/Maps/my_map.txt` and validate it.
2. That's all: `MapLibrary` loads every text asset in `Resources/Maps`. Built-in maps appear first, in their fixed
   order, and new maps follow. The preview image, NavMesh and lighting are generated automatically.
