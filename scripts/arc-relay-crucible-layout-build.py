#!/usr/bin/env python3
"""Author + verify the Crucible base layout.

Authoring rule mirrors the map's: everything is written in the WEST frame
(reactor at (2,2)). The east side gets it via the binding's rotate-180, and
the runtime maps world->canonical with (W-1-x, H-1-y) -- the same rot() the
map generator uses. So an own-/enemy- zone pair is correct exactly when the
two rects are each other's rotation.
"""
import json, hashlib, pathlib, sys
from collections import deque

ROWS = [
    "###############################",
    "#....#......#.........##......#",
    "#....#...#..#....#........##..#",
    "#.........#..##..#............#",
    "#.......#.#.............#....##",
    "###................###.......##",
    "#.......#...#..#.....#........#",
    "#.......#......#.....#...##...#",
    "#.#.##........#...#...........#",
    "#.....##...#....#...##.##.##..#",
    "#............#...#............#",
    "#....#.#....#.....#....#.#....#",
    "#............#...#............#",
    "#..##.##.##...#....#...##.....#",
    "#...........#...#........##.#.#",
    "#...##...#.....#......#.......#",
    "#........#.....#..#...#.......#",
    "##.......###................###",
    "##....#.............#.#.......#",
    "#.....#......#..##..#.........#",
    "#..##........#....#..#...#....#",
    "#......##.........#......#....#",
    "###############################",
]
W, H = len(ROWS[0]), len(ROWS)
floor = lambda p: 0 <= p[0] < W and 0 <= p[1] < H and ROWS[p[1]][p[0]] == '.'
rot = lambda p: (W - 1 - p[0], H - 1 - p[1])
rotrect = lambda r: (W-1-r[2], H-1-r[3], W-1-r[0], H-1-r[1])


def path(a, b):
    q, seen = deque([a]), {a: None}
    while q:
        p = q.popleft()
        if p == b:
            out = []
            while p: out.append(p); p = seen[p]
            return out[::-1]
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                n = (p[0]+dx, p[1]+dy)
                if floor(n) and n not in seen:
                    seen[n] = p; q.append(n)
    raise SystemExit(f"no path {a}->{b}")


def thin(pts):
    """Keep only the tiles where the step direction changes."""
    out = [pts[0]]
    for i in range(1, len(pts)-1):
        d0 = (pts[i][0]-pts[i-1][0], pts[i][1]-pts[i-1][1])
        d1 = (pts[i+1][0]-pts[i][0], pts[i+1][1]-pts[i][1])
        if d0 != d1: out.append(pts[i])
    out.append(pts[-1])
    return out


def leg(*stops):
    pts = [stops[0]]
    for a, b in zip(stops, stops[1:]):
        pts += path(a, b)[1:]
    return thin(pts)


# ---- zones: authored west, each own-/enemy- pair an exact rotation --------
OWN_ZONES = {
    "own-pad":       (1, 1, 4, 4),
    "own-delivery":  (5, 5, 7, 7),
    "own-home":      (1, 1, 8, 8),
    "own-well":      (8, 1, 11, 5),
    "own-approach":  (5, 3, 13, 10),
    "own-heal":      (9, 15, 11, 17),
    "own-wing":      (1, 12, 13, 21),
}
SELF_ZONES = {
    "centre-well": (13, 9, 17, 13),
    "midfield":    (10, 7, 20, 15),
}
zones = {}
for k, v in OWN_ZONES.items():
    zones[k] = v
    zones["enemy-" + k[4:]] = rotrect(v)
zones.update(SELF_ZONES)

# ---- anchors: same rule ---------------------------------------------------
OWN_ANCHORS = {
    "own-reactor":       (2, 2),
    "own-delivery-post": (8, 5),    # east mouth of the scoring shoulder
    "own-well-post":     (8, 3),    # the near Well's short mouth
    "own-heal-post":     (11, 16),  # cover beside the near heal
    "own-lane-gate":     (6, 11),   # single-tile passage on the middle row
    "centre-post-near":  (14, 10),  # the lip you take centre from
}
anchors = {}
for k, v in OWN_ANCHORS.items():
    anchors[k] = v
    anchors["enemy-" + k[4:] if k.startswith("own-") else k + "-far"] = rot(v)
anchors["centre-well-post"] = (15, 11)

# ---- routes: pathfound, then thinned to turn points ----------------------
routes = {
    "own-well-run":    (leg((6, 6), (8, 5), (9, 3)), 2),
    "centre-push":     (leg((6, 6), (14, 10), (15, 11)), 2),
    "own-wing-run":    (leg((6, 6), (6, 10), (10, 16)), 2),
    "raid-run":        (leg((6, 6), (10, 16), (21, 19)), 1),
    "own-lane-patrol": (leg((8, 5), (12, 8), (11, 12), (6, 11), (8, 5)), 2),
    "centre-hold":     (leg((14, 10), (16, 10), (16, 12), (14, 12), (14, 10)), 1),
    "own-home-patrol": (leg((5, 5), (8, 5), (7, 8), (3, 8), (5, 5)), 2),
}

layout = {
    "schema": "arc-relay-tactical-layout-v1",
    "layoutId": "crucible-base-v1",
    "mapId": "arc-relay-crucible-01",
    "bindings": [
        {"matchContractFingerprint": "any-composition",
         "ownReactorSide": "west", "transform": "identity",
         "routeAliases": {}},
        {"matchContractFingerprint": "any-composition",
         "ownReactorSide": "east", "transform": "rotate-180",
         "routeAliases": {}},
    ],
    "zones": [{"zoneId": k, "rect": list(v)} for k, v in sorted(zones.items())],
    "routes": [{"routeId": k, "waypoints": [list(p) for p in v[0]],
                "corridorWidth": v[1]} for k, v in sorted(routes.items())],
    "anchors": [{"anchorId": k, "position": list(v)}
                for k, v in sorted(anchors.items())],
}

# ---- verification --------------------------------------------------------
errs = []
for z in layout["zones"]:
    x0, y0, x1, y1 = z["rect"]
    if not (0 <= x0 <= x1 < W and 0 <= y0 <= y1 < H):
        errs.append(f"zone {z['zoneId']} out of bounds {z['rect']}")
    if not any(floor((x, y)) for x in range(x0, x1+1) for y in range(y0, y1+1)):
        errs.append(f"zone {z['zoneId']} contains no floor")
for a in layout["anchors"]:
    if not floor(tuple(a["position"])):
        errs.append(f"anchor {a['anchorId']} is not floor {a['position']}")
for r in layout["routes"]:
    for p in r["waypoints"]:
        if not floor(tuple(p)):
            errs.append(f"route {r['routeId']} waypoint {p} is not floor")
# chirality of the vocabulary
for k, v in zones.items():
    if k.startswith("own-"):
        e = "enemy-" + k[4:]
        if tuple(rotrect(v)) != tuple(zones[e]):
            errs.append(f"{k} / {e} are not rotations")
for k, v in anchors.items():
    if k.startswith("own-"):
        e = "enemy-" + k[4:]
        if rot(v) != anchors[e]:
            errs.append(f"anchor {k} / {e} are not rotations")
# bounds the compiler enforces
if not 1 <= len(layout["zones"]) <= 64: errs.append("zone count")
if not 1 <= len(layout["routes"]) <= 64: errs.append("route count")
if not 1 <= len(layout["anchors"]) <= 64: errs.append("anchor count")
for r in layout["routes"]:
    if not 1 <= len(r["waypoints"]) <= 128: errs.append(f"{r['routeId']} waypoints")
    if not 0 <= r["corridorWidth"] <= 8: errs.append(f"{r['routeId']} width")

blob = json.dumps(layout, indent=2) + "\n"
digest = hashlib.sha256(blob.encode()).hexdigest()
print(f"zones {len(layout['zones'])}  routes {len(layout['routes'])}  "
      f"anchors {len(layout['anchors'])}")
for r in layout["routes"]:
    print(f"  route {r['routeId']:16} {len(r['waypoints']):2} waypoints  "
          f"corridor {r['corridorWidth']}")
print("errors:", errs or "none")
print("sha256:", digest)
if errs:
    raise SystemExit(1)

target = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else (
    pathlib.Path(__file__).resolve().parent.parent
    / "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03"
    / "layouts/crucible-base-v1.json")
if target.read_text() != blob if target.exists() else True:
    target.write_text(blob)
    print(f"wrote {target}")
else:
    print(f"unchanged {target}")
print("\nAny sheet pinning this layout must carry:")
print(f'  "layout": {{ "path": "../layouts/crucible-base-v1.json",')
print(f'               "sha256": "{digest}" }}')
