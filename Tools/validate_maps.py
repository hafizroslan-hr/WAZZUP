#!/usr/bin/env python3
"""Validates Wardline ASCII map files: shape, required markers, connectivity and timing balance.
Usage: python3 Tools/validate_maps.py [map files...]"""
import sys, glob, os
from collections import deque

WALK = set(".ABRW123gprl")
BLOCK = set("#chwo")
ALL = WALK | BLOCK

def load(path):
    head, grid, body = {}, [], False
    for line in open(path, encoding="utf-8").read().splitlines():
        if not body:
            if line.strip() == "---": body = True
            elif ":" in line:
                k, v = line.split(":", 1); head[k.strip()] = v.strip()
        elif line.strip():
            grid.append(line.rstrip())
    return head, grid

def bfs(grid, starts):
    H, W = len(grid), len(grid[0])
    d = {s: 0 for s in starts}; q = deque(starts)
    while q:
        x, z = q.popleft()
        for dx, dz in ((1,0),(-1,0),(0,1),(0,-1)):
            nx, nz = x+dx, z+dz
            if 0 <= nz < H and 0 <= nx < W and grid[nz][nx] in WALK and (nx, nz) not in d:
                d[(nx, nz)] = d[(x, z)] + 1; q.append((nx, nz))
    return d

def cells(grid, ch):
    return [(x, z) for z, row in enumerate(grid) for x, c in enumerate(row) if c == ch]

def check(path):
    head, grid = load(path); errs = []
    name = head.get("name", os.path.basename(path))
    widths = {len(r) for r in grid}
    if len(widths) != 1: errs.append(f"row widths differ: {sorted(widths)}")
    W = max(widths); grid = [r.ljust(W, "#") for r in grid]
    bad = {c for r in grid for c in r if c not in ALL}
    if bad: errs.append(f"unknown chars {bad}")
    for z, r in enumerate(grid):
        if r[0] != "#" or r[-1] != "#": errs.append(f"row {z} not closed by walls")
    if set(grid[0]) != {"#"} or set(grid[-1]) != {"#"}: errs.append("top/bottom not closed")
    for ch in "ABRW":
        if len(cells(grid, ch)) < 4: errs.append(f"marker {ch} has < 4 cells")
    for ch in "123":
        if not cells(grid, ch): errs.append(f"lane waypoint {ch} missing")
    if len(cells(grid, "g")) < 2: errs.append("needs >= 2 guard spots")
    walk = [(x, z) for z, r in enumerate(grid) for x, c in enumerate(r) if c in WALK]
    dr, dw = bfs(grid, cells(grid, "R")), bfs(grid, cells(grid, "W"))
    unreach = [p for p in walk if p not in dr]
    if unreach: errs.append(f"{len(unreach)} walkable cells unreachable, e.g. {unreach[:5]}")
    for x, z in cells(grid, "r"):
        if not any(grid[z+dz][x+dx] == "p" for dx, dz in ((1,0),(-1,0),(0,1),(0,-1))):
            errs.append(f"ramp at {(x,z)} has no adjacent platform")
    timing = {}
    for site in "AB":
        sc = cells(grid, site)
        if sc and all(c in dr for c in sc):
            timing[site] = (min(dr[c] for c in sc), min(dw[c] for c in sc))
    for site, (r, w) in timing.items():
        if not (w < r): errs.append(f"site {site}: wardens ({w}) should arrive before raiders ({r})")
        if r > 40: errs.append(f"site {site}: raider route too long ({r} cells)")
        if w < 4: errs.append(f"site {site}: wardens spawn too close ({w} cells)")
    lanes = {ch: dr.get(cells(grid, ch)[0]) for ch in "123" if cells(grid, ch)}
    print(f"{name}: {W}x{len(grid)}, walkable={len(walk)}, site timing (raider,warden cells)={timing}, lane dist={lanes}")
    for e in errs: print("   ERROR:", e)
    return not errs

files = sys.argv[1:] or sorted(glob.glob(os.path.join(os.path.dirname(__file__), "..", "Assets/Wardline/Resources/Maps/*.txt")))
ok = all([check(f) for f in files])
print("ALL MAPS VALID" if ok else "MAP VALIDATION FAILED"); sys.exit(0 if ok else 1)
