#!/usr/bin/env python3
"""Author + verify the Drawbench base layout.

Same contract as crucible-base-v1 and deliberately the SAME id set, so a
sheet moves between the two arenas by editing its layout pin and nothing
else. What changes is where each name points: Drawbench is horizontal, and
each side's owned Well sits on the height its spawn did not.

Authored in the WEST frame; the east side gets it from the binding's
rotate-180, which is the runtime's own canonical transform.
"""
import hashlib
import json
import pathlib
import sys
from collections import deque

ROWS = None


def load_rows():
    global ROWS
    here = pathlib.Path(__file__).resolve().parent
    src = (here / "arc-relay-drawbench-build.py").read_text()
    ns = {}
    exec(compile(src.replace("if __name__ == '__main__':", "if False:"),
                 "drawbench", "exec"), ns)
    ROWS = ns["build"]()
    return ns


NS = load_rows()
W, H = NS["W"], NS["H"]
floor = lambda p: 0 <= p[0] < W and 0 <= p[1] < H and ROWS[p[1]][p[0]] == '.'
rot = lambda p: (W - 1 - p[0], H - 1 - p[1])
rotrect = lambda r: (W - 1 - r[2], H - 1 - r[3], W - 1 - r[0], H - 1 - r[1])


def path(a, b):
    q, seen = deque([a]), {a: None}
    while q:
        p = q.popleft()
        if p == b:
            out = []
            while p:
                out.append(p)
                p = seen[p]
            return out[::-1]
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                n = (p[0] + dx, p[1] + dy)
                if floor(n) and n not in seen:
                    seen[n] = p
                    q.append(n)
    raise SystemExit(f"no path {a} -> {b}")


def thin(pts):
    out = [pts[0]]
    for i in range(1, len(pts) - 1):
        if (pts[i][0] - pts[i-1][0], pts[i][1] - pts[i-1][1]) != \
           (pts[i+1][0] - pts[i][0], pts[i+1][1] - pts[i][1]):
            out.append(pts[i])
    out.append(pts[-1])
    return out


def leg(*stops):
    pts = [stops[0]]
    for a, b in zip(stops, stops[1:]):
        pts += path(a, b)[1:]
    return thin(pts)


# --- primary vocabulary, authored west -----------------------------------
OWN_ZONES = {
    "own-pad":       (1, 4, 4, 7),
    "own-delivery":  (6, 5, 8, 7),
    "own-home":      (1, 3, 9, 9),
    "own-well":      (7, 15, 11, 19),    # low: the height you did NOT spawn on
    "own-approach":  (5, 8, 13, 14),
    "own-heal":      (10, 17, 14, 21),
    "own-wing":      (1, 12, 12, 21),
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

# the corpus's shared names, pointed at the Drawbench landmark each denotes
COMPAT_ZONES = {
    "well-line":         (9, 3, 21, 19),
    "north-farm":        rotrect(OWN_ZONES["own-well"]),   # east's Well, high
    "south-farm":        OWN_ZONES["own-well"],            # west's Well, low
    "centre-farm":       SELF_ZONES["centre-well"],
    "enemy-backfield":   rotrect(OWN_ZONES["own-home"]),
    "north-perch":       (16, 1, 20, 5),
    "south-perch":       (10, 17, 14, 21),
    "own-choke":         (4, 9, 8, 13),
    "enemy-choke":       rotrect((4, 9, 8, 13)),
    "forward-rally":     OWN_ZONES["own-approach"],
    "own-gate":          (8, 4, 10, 8),
    "enemy-gate":        rotrect((8, 4, 10, 8)),
    "kill-pocket":       (11, 9, 14, 13),
    "siege-leash":       (18, 10, 26, 18),
    "outer-gate":        (20, 12, 25, 17),
    "bank-seal":         (5, 4, 9, 8),
    "enemy-bank-danger": rotrect((5, 4, 9, 8)),
}
zones.update(COMPAT_ZONES)

OWN_ANCHORS = {
    "own-reactor":       (2, 6),
    "own-delivery-post": (9, 7),
    "own-well-post":     (8, 16),
    "own-heal-post":     (12, 18),
    "own-lane-gate":     (5, 11),
    "centre-post-near":  (14, 11),
}


def mirror_id(name):
    if name.startswith("own-"):
        return "enemy-" + name[len("own-"):]
    if name.endswith("-near"):
        return name[: -len("-near")] + "-far"
    raise SystemExit(f"anchor '{name}' has no mirror naming rule")


anchors = {}
for k, v in OWN_ANCHORS.items():
    anchors[k] = v
    anchors[mirror_id(k)] = rot(v)
anchors["centre-well-post"] = (15, 11)
anchors.update({
    "home-gate":          (9, 7),
    "own-bank":           (7, 6),
    "enemy-perimeter":    (21, 15),
    "rally-centre":       (14, 11),
    "well-line-centre":   (15, 11),
    "perch-north":        (18, 4),
    "perch-south":        (12, 18),
    "sanitation-release": (6, 10),
})

routes = {
    "own-well-run":    (leg((7, 6), (7, 12), (9, 17)), 2),
    "centre-push":     (leg((7, 6), (11, 8), (15, 11)), 2),
    "own-wing-run":    (leg((7, 6), (4, 13), (9, 17)), 2),
    "raid-run":        (leg((7, 6), (15, 11), (21, 5)), 1),
    "own-lane-patrol": (leg((9, 7), (13, 10), (9, 14), (5, 11), (9, 7)), 2),
    "centre-hold":     (leg((14, 10), (16, 10), (16, 12), (14, 12), (14, 10)), 1),
    "own-home-patrol": (leg((5, 5), (9, 7), (7, 9), (3, 8), (5, 5)), 2),
    "score-return":    (leg((15, 11), (11, 8), (7, 6)), 2),
    "strike-line":     (leg((5, 11), (10, 11), (14, 11), (15, 11)), 1),
    "shadow-hunt-loop": (leg((9, 7), (13, 10), (11, 14), (9, 17),
                             (6, 13), (9, 7)), 2),
    "shadow-north-long": (leg((9, 7), (14, 4), (19, 4), (18, 3),
                              (16, 7), (11, 6), (9, 7)), 2),
    "shadow-south-long": (leg((6, 10), (4, 13), (8, 17), (12, 19),
                              (13, 15), (9, 11), (6, 10)), 2),
    "outer-rush":           (leg((7, 6), (9, 17), (18, 18), (23, 16)), 2),
    "outer-rush-opposite":  (leg((7, 6), (12, 4), (21, 6), (23, 16)), 2),
    "short-breach":         (leg((7, 6), (15, 11), (23, 16)), 1),
    "short-breach-opposite": (leg((7, 6), (14, 10), (16, 12), (23, 16)), 1),
}
for alias, target in (("north-well-loop", "raid-run"),
                      ("south-well-loop", "own-well-run"),
                      ("centre-well-loop", "centre-push"),
                      ("farm-loop", "own-lane-patrol"),
                      ("north-out", "raid-run"),
                      ("south-out", "own-well-run"),
                      ("centre-probe", "centre-push"),
                      ("stage-loop", "own-home-patrol")):
    routes[alias] = routes[target]

layout = {
    "schema": "arc-relay-tactical-layout-v1",
    "layoutId": "drawbench-base-v1",
    "mapId": "arc-relay-drawbench-01",
    "bindings": [
        {"matchContractFingerprint": "any-composition",
         "ownReactorSide": "west", "transform": "identity", "routeAliases": {}},
        {"matchContractFingerprint": "any-composition",
         "ownReactorSide": "east", "transform": "rotate-180", "routeAliases": {}},
    ],
    "zones": [{"zoneId": k, "rect": list(v)} for k, v in sorted(zones.items())],
    "routes": [{"routeId": k, "waypoints": [list(p) for p in v[0]],
                "corridorWidth": v[1]} for k, v in sorted(routes.items())],
    "anchors": [{"anchorId": k, "position": list(v)}
                for k, v in sorted(anchors.items())],
}

errs = []
for z in layout["zones"]:
    x0, y0, x1, y1 = z["rect"]
    if not (0 <= x0 <= x1 < W and 0 <= y0 <= y1 < H):
        errs.append(f"zone {z['zoneId']} out of bounds")
    if not any(floor((x, y)) for x in range(x0, x1 + 1)
               for y in range(y0, y1 + 1)):
        errs.append(f"zone {z['zoneId']} contains no floor")
for a in layout["anchors"]:
    if not floor(tuple(a["position"])):
        errs.append(f"anchor {a['anchorId']} is not floor")
for r in layout["routes"]:
    for p in r["waypoints"]:
        if not floor(tuple(p)):
            errs.append(f"route {r['routeId']} waypoint {p} is not floor")
for k, v in OWN_ZONES.items():
    if tuple(rotrect(v)) != tuple(zones["enemy-" + k[4:]]):
        errs.append(f"{k} is not the rotation of its enemy- pair")
for k, v in SELF_ZONES.items():
    if tuple(rotrect(v)) != tuple(v):
        errs.append(f"{k} is not self-symmetric")
for k, v in OWN_ANCHORS.items():
    if rot(v) != anchors[mirror_id(k)]:
        errs.append(f"anchor {k} is not the rotation of its pair")
if not 1 <= len(layout["zones"]) <= 64: errs.append("zone count")
if not 1 <= len(layout["routes"]) <= 64: errs.append("route count")
if not 1 <= len(layout["anchors"]) <= 64: errs.append("anchor count")

blob = json.dumps(layout, indent=2) + "\n"
digest = hashlib.sha256(blob.encode()).hexdigest()
print(f"zones {len(layout['zones'])}  routes {len(layout['routes'])}  "
      f"anchors {len(layout['anchors'])}")
print("errors:", errs or "none")
print("sha256:", digest)
if errs:
    raise SystemExit(1)
target = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else (
    pathlib.Path(__file__).resolve().parent.parent
    / "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03"
    / "layouts/drawbench-base-v1.json")
target.write_text(blob)
print(f"wrote {target}")
print(f'\n  "layout": {{ "path": "../layouts/drawbench-base-v1.json",')
print(f'               "sha256": "{digest}" }}')
