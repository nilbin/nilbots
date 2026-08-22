#!/usr/bin/env python3
"""Regenerate atlas.spec.json for the Broadloom candidate.

The spec is the analyzer's input contract. Everything tile-derived (room
zones, shortest-path overlays) is computed from build.py here so the atlas
cannot drift from the terrain. Run from this directory:

    python3 make-atlas-spec.py
    python3 ../../../.claude/skills/analyze-arc-relay-map/scripts/map_analysis.py \
        atlas.spec.json --json-out analysis.json --html-out atlas.html
"""
import importlib.util
import json
from collections import deque
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec_loader = importlib.util.spec_from_file_location('bb', HERE / 'build.py')
bb = importlib.util.module_from_spec(spec_loader)
spec_loader.loader.exec_module(bb)

rows = bb.build()
is_open = lambda p: rows[p[1]][p[0]] == '.'


def shortest(a, b):
    q, prev = deque([a]), {a: None}
    while q:
        c = q.popleft()
        if c == b:
            break
        for n in bb.neighbours(rows, c):
            if n not in prev:
                prev[n] = c
                q.append(n)
    path, c = [], b
    while c is not None:
        path.append(list(c))
        c = prev[c]
    return path[::-1]


def zone(x0, y0, x1, y1):
    return [[x, y] for y in range(y0, y1 + 1) for x in range(x0, x1 + 1)
            if is_open((x, y))]


R0, R1 = bb.REACTOR0, bb.rot(bb.REACTOR0)
WC, WN, WS = bb.WELL_CENTRE, bb.WORKSHOP_N, bb.WORKSHOP_S
OW, EW = bb.WELL_OWN0, bb.WELL_OWN1
HW, HE = bb.HEAL_W, bb.rot(bb.HEAL_W)

spec = {
    "title": "Broadloom 37x27 — first whole-map Arc Relay candidate",
    "note": (
        "UNIMPLEMENTED DESIGN CANDIDATE, west-team perspective — not the live "
        "map. Requires an explicit platform change: ArenaMap.MaxWidth 32 -> "
        "at least 37 (999 total tiles, below the 1,024-cell budget). Scoring "
        "is the exact reactor tile inside each protected pad. Workshops are "
        "proposed neutral veterancy sites; no workshop rules exist yet."
    ),
    "map": {"builder": "build.py", "function": "build"},
    "symmetry": "rotate-180",
    "stubAllowance": 2,
    "validateCutVertices": True,
    "atlasRequirements": {"distances": True, "combatRanges": True},
    "landmarkRequirements": {
        "reactor": 2, "well": 3, "healing": 2,
        "workshop": 2, "junction": 4, "spawn-exit": 2,
    },
    "combatReference": {
        "origin": "north-workshop",
        "facing": "south",
        "source": (
            "ArcRelayLoopProfile.Current (Tapline15) base profiles: vision 5 "
            "facing-quadrant with zero omnidirectional proximity, guns 3/4/6, "
            "Rail 12 separate. Signatures and veterancy excluded."
        ),
        "vision": {
            "label": "Base vision",
            "range": 5,
            "shape": "facing-quadrant",
            "lineOfSight": "corner-strict-supercover",
            "pointBlankRange": 0,
        },
        "attacks": [
            {"id": "short-fast", "label": "Short-fast", "style": "short-fast",
             "range": 3,
             "classes": ["Kestrel", "Palisade", "Patchbay", "Lantern",
                          "Minesmith", "Relay", "Mason", "Repulsor", "Veil"]},
            {"id": "medium-steady", "label": "Medium-steady",
             "style": "medium-steady", "range": 4,
             "classes": ["Towline", "Mortar", "Hush", "Switchback", "Sunder",
                          "Nest"]},
            {"id": "long-slow", "label": "Long-slow", "style": "long-slow",
             "range": 6, "classes": ["Longshot"]},
        ],
    },
    "landmarks": [
        {"id": "own-reactor", "kind": "reactor", "name": "Own reactor",
         "label": "R", "team": "west", "position": list(R0)},
        {"id": "enemy-reactor", "kind": "reactor", "name": "Enemy reactor",
         "label": "R", "team": "east", "position": list(R1)},
        {"id": "own-well", "kind": "well", "name": "Own Well (1)",
         "label": "1", "position": list(OW)},
        {"id": "centre-well", "kind": "well", "name": "Centre Well (2)",
         "label": "2", "position": list(WC)},
        {"id": "enemy-well", "kind": "well", "name": "Enemy Well (4)",
         "label": "4", "position": list(EW)},
        {"id": "own-heal", "kind": "healing", "name": "Own healing tile",
         "label": "+", "position": list(HW)},
        {"id": "enemy-heal", "kind": "healing", "name": "Enemy healing tile",
         "label": "+", "position": list(HE)},
        {"id": "north-workshop", "kind": "workshop",
         "name": "North workshop (neutral)", "label": "T",
         "position": list(WN)},
        {"id": "south-workshop", "kind": "workshop",
         "name": "South workshop (neutral)", "label": "T",
         "position": list(WS)},
        {"id": "nw-junction", "kind": "junction", "name": "NW junction",
         "label": "J", "position": [12, 8]},
        {"id": "ne-junction", "kind": "junction", "name": "NE junction",
         "label": "J", "position": [30, 8]},
        {"id": "sw-junction", "kind": "junction", "name": "SW junction",
         "label": "J", "position": [6, 18]},
        {"id": "se-junction", "kind": "junction", "name": "SE junction",
         "label": "J", "position": [24, 18]},
        {"id": "own-spawn-exit", "kind": "spawn-exit",
         "name": "Own pad north exit", "label": "»", "team": "west",
         "position": [4, 9]},
        {"id": "enemy-spawn-exit", "kind": "spawn-exit",
         "name": "Enemy pad south exit", "label": "»", "team": "east",
         "position": [32, 17]},
    ],
    "routes": [],
    "tacticalSites": [],
    "comparison": {
        "baselineLabel": "Tapline15 (live)",
        "candidateLabel": "Broadloom (candidate)",
        "rows": [
            {"metric": "Canvas and open floor",
             "baseline": "31x27, 517 open", "candidate": "37x27, 681 open",
             "note": "Needs the width-cap platform change; still under the "
                     "1,024-cell budget."},
            {"metric": "Own Well -> own reactor", "baseline": 12,
             "candidate": 8,
             "note": "Shorter, safer drip; income still gated by the 46-tick "
                     "recharge."},
            {"metric": "Centre Well -> own reactor", "baseline": 19,
             "candidate": 16, "note": "Comparable mid commitment."},
            {"metric": "Enemy Well -> own reactor", "baseline": 31,
             "candidate": 25,
             "note": "Raids commit through the enemy workshop theatre "
                     "instead of the centre line."},
            {"metric": "Enemy-Well return spread", "baseline": "31 / 33 / 41",
             "candidate": "25 / 28 / 37",
             "note": "Fast vs heal-detour vs shadow families."},
            {"metric": "Centre Well -> own Well", "baseline": 22,
             "candidate": 10,
             "note": "Defensive rotation exists; splitting is affordable."},
            {"metric": "Centre Well -> nearest heal", "baseline": 8,
             "candidate": 13,
             "note": "Healing decoupled from centre control."},
            {"metric": "Own reactor -> near heal", "baseline": 16,
             "candidate": 9,
             "note": "Heals move to the home quarters; contested healing "
                     "becomes a raid, not a centre reward."},
            {"metric": "Reactor -> reactor", "baseline": 38, "candidate": 32,
             "note": "Wider map, better connected spine."},
            {"metric": "Neutral workshops", "baseline": "none",
             "candidate": "2 on the centre column, 15/18 from the reactors",
             "note": "3-step supply-side bias per team, measured below."},
            {"metric": "Centre -> either workshop", "baseline": "n/a",
             "candidate": 11,
             "note": "A centre force cannot flash-rotate to workshop "
                     "defence."},
            {"metric": "Workshop -> adjacent owned Well", "baseline": "n/a",
             "candidate": 8,
             "note": "Training pressure and Well raids share a theatre."},
        ],
    },
}


def route(rid, label, group, style, frm, to, mode="shortest", via=None,
          label_at=None, steps=None, avoid=None):
    entry = {
        "id": rid, "label": label, "origin": group.split(" · ")[0],
        "group": group, "style": style, "from": frm, "to": to, "mode": mode,
    }
    if via:
        entry["via"] = via
    if avoid:
        entry["avoid"] = avoid
    if steps is not None:
        entry["expectedSteps"] = steps
    if label_at:
        entry["labelAt"] = label_at
    return entry


spec["routes"] = [
    # Own Well returns
    route("own-well-home-door", "Home door return", "Own Well · returns",
          "direct", "own-well", "own-reactor", steps=8, label_at=[7, 19]),
    route("own-well-north-mouth", "Return when the home door is cut",
          "Own Well · returns", "north", "own-well", "own-reactor",
          avoid=[[7, 17], [7, 18]], steps=13, label_at=[11, 15]),
    route("own-well-to-centre", "Rotate to Centre", "Own Well · returns",
          "alternate", "own-well", "centre-well", steps=10, label_at=[14, 16]),
    # Centre returns and pivots
    route("centre-direct", "Fast return", "Centre · returns", "direct",
          "centre-well", "own-reactor", steps=16, label_at=[10, 12]),
    route("centre-via-heal", "Return through own heal", "Centre · returns",
          "north", "centre-well", "own-reactor", mode="via",
          via=["own-heal"], steps=22, label_at=[8, 4]),
    route("centre-to-north-workshop", "Rotate to N workshop",
          "Centre · returns", "alternate", "centre-well", "north-workshop",
          steps=11, label_at=[15, 9]),
    # Raid returns (enemy Well, west perspective)
    route("raid-fast", "Fast return", "Raid · enemy Well", "direct",
          "enemy-well", "own-reactor", steps=25, label_at=[20, 11]),
    route("raid-via-heal", "Wounded return through own heal",
          "Raid · enemy Well", "alternate", "enemy-well", "own-reactor",
          mode="via", via=["own-heal"], steps=28, label_at=[9, 5]),
    route("raid-shadow-gallery", "Shadow return over the roof",
          "Raid · enemy Well", "north", "enemy-well", "own-reactor",
          mode="via", via=[[26, 1], [13, 1]], steps=37, label_at=[16, 1]),
    route("raid-well-to-centre", "Fall back to Centre", "Raid · enemy Well",
          "south", "enemy-well", "centre-well", steps=10, label_at=[23, 10]),
    # Workshop approaches (the measured neutrality question)
    route("own-to-north-workshop", "Own approach", "N workshop · approaches",
          "direct", "own-reactor", "north-workshop", steps=18, label_at=[9, 9]),
    route("enemy-to-north-workshop", "Enemy approach",
          "N workshop · approaches", "south", "enemy-reactor",
          "north-workshop", steps=15, label_at=[28, 6]),
    route("own-to-south-workshop", "Own approach", "S workshop · approaches",
          "direct", "own-reactor", "south-workshop", steps=15, label_at=[9, 17]),
    route("enemy-to-south-workshop", "Enemy approach",
          "S workshop · approaches", "south", "enemy-reactor",
          "south-workshop", steps=18, label_at=[28, 20]),
    # Workshop continuations
    route("train-to-raid", "Train, then raid the Well",
          "Workshops · continuations", "direct", "north-workshop",
          "enemy-well", steps=8, label_at=[22, 3]),
    route("train-to-recover", "Fall back to own heal",
          "Workshops · continuations", "north", "north-workshop", "own-heal",
          steps=12, label_at=[12, 4]),
    route("south-workshop-to-own-well", "Screen own Well",
          "Workshops · continuations", "alternate", "south-workshop",
          "own-well", steps=8, label_at=[14, 21]),
    route("workshop-to-workshop", "Cross-map development swing",
          "Workshops · continuations", "south", "north-workshop",
          "south-workshop", steps=19, label_at=[21, 13]),
    # Home, spine, flank
    route("own-heal-run", "Recover at home", "Home · spine and flanks",
          "direct", "own-reactor", "own-heal", steps=9, label_at=[5, 8]),
    route("spine", "Mid spine", "Home · spine and flanks", "south",
          "own-reactor", "enemy-reactor", steps=32, label_at=[24, 13]),
    route("deep-flank", "Committed roof flank", "Home · spine and flanks",
          "north", "own-spawn-exit", "enemy-reactor", mode="via",
          via=[[13, 1], [23, 1]], steps=39, label_at=[28, 2]),
    route("enemy-heal-raid", "Deep raid on the enemy heal",
          "Home · spine and flanks", "alternate", "own-reactor", "enemy-heal",
          steps=26, label_at=[24, 19]),
    route("junction-lateral", "Junction-to-junction lateral",
          "Home · spine and flanks", "direct", "nw-junction", "ne-junction",
          steps=19, label_at=[21, 7]),
]

spec["tacticalSites"] = [
    {
        "id": "north-workshop-site",
        "label": "N workshop",
        "note": (
            "Three wide mouths plus a sealable skylight and roof overwatch; "
            "one Mason can never seal it, two leave a +4 bounded detour."
        ),
        "marks": [
            {"kind": "zone", "label": "training room", "tiles": zone(15, 3, 21, 6)},
            {"kind": "mouth", "label": "W mouth", "tiles": [[14, 3], [14, 4]]},
            {"kind": "mouth", "label": "E mouth",
             "tiles": [[22, 4], [22, 5], [22, 6]]},
            {"kind": "mouth", "label": "S antechamber",
             "tiles": [[16, 7], [17, 7], [18, 7], [19, 7], [20, 7]]},
            {"kind": "seal", "label": "skylight (1-wide)", "tiles": [[18, 2]]},
            {"kind": "seal", "label": "fork slips (1-wide)",
             "tiles": [[16, 8], [20, 8]]},
            {"kind": "post", "label": "trainee", "tiles": [[18, 4]],
             "facing": "south"},
            {"kind": "post", "label": "W guard", "tiles": [[15, 3]],
             "facing": "west"},
            {"kind": "post", "label": "E guard", "tiles": [[21, 5]],
             "facing": "east"},
            {"kind": "post", "label": "roof overwatch", "tiles": [[18, 1]],
             "facing": "south"},
            {"kind": "lane", "label": "band interrupt rail",
             "from": [12, 4], "to": [19, 4]},
            {"kind": "lane", "label": "skylight rail", "from": [18, 1],
             "to": [18, 4]},
            {"kind": "lane", "label": "roof gallery", "from": [12, 1],
             "to": [24, 1]},
            {"kind": "route", "label": "train -> raid continuation",
             "path": shortest(WN, EW)},
        ],
    },
    {
        "id": "centre-plaza",
        "label": "Centre plaza",
        "note": (
            "Broken inner cross around the 2-Well; no straight rail onto the "
            "Well from any mouth; pincer approaches stay separate until y10."
        ),
        "marks": [
            {"kind": "mouth", "label": "W mouth",
             "tiles": [[14, 12], [14, 13], [14, 14]]},
            {"kind": "mouth", "label": "E mouth",
             "tiles": [[22, 12], [22, 13], [22, 14]]},
            {"kind": "post", "label": "cover posts",
             "tiles": [[17, 11], [19, 15]], "facing": "east"},
            {"kind": "lane", "label": "longest Well rail (5)",
             "from": [16, 13], "to": [20, 13]},
            {"kind": "route", "label": "pincer north arm",
             "path": shortest((12, 8), (17, 10))},
            {"kind": "route", "label": "pincer west arm",
             "path": shortest((12, 12), (16, 13))},
        ],
    },
    {
        "id": "east-well-room",
        "label": "Enemy Well room",
        "note": (
            "West's 4-value raid target: four doors on three families plus "
            "two 1-wide shadow seams; the defender cannot watch all of them "
            "from one facing."
        ),
        "marks": [
            {"kind": "zone", "label": "room", "tiles": zone(24, 5, 28, 9)},
            {"kind": "mouth", "label": "W mouth (raid)",
             "tiles": [[23, 6], [23, 7], [23, 8]]},
            {"kind": "mouth", "label": "S mouth (mid)",
             "tiles": [[24, 10], [25, 10]]},
            {"kind": "mouth", "label": "E mouth (home lane)",
             "tiles": [[29, 8], [29, 9]]},
            {"kind": "seal", "label": "N seams (1-wide)",
             "tiles": [[24, 4], [28, 4]]},
            {"kind": "post", "label": "guard", "tiles": [[27, 5]],
             "facing": "west"},
            {"kind": "post", "label": "mine turn", "tiles": [[25, 9]]},
            {"kind": "route", "label": "raid fast-return start",
             "path": shortest(EW, (22, 13))},
            {"kind": "route", "label": "raid shadow-return start",
             "path": shortest(EW, (23, 1))},
        ],
    },
    {
        "id": "west-well-room",
        "label": "Own Well room",
        "note": (
            "Mirror of the enemy room: the home door faces the pad lane, the "
            "east door faces the enemy raid theatre."
        ),
        "marks": [
            {"kind": "zone", "label": "room", "tiles": zone(8, 17, 12, 21)},
            {"kind": "mouth", "label": "E mouth (enemy raid)",
             "tiles": [[13, 18], [13, 19], [13, 20]]},
            {"kind": "mouth", "label": "N mouth (mid)",
             "tiles": [[11, 16], [12, 16]]},
            {"kind": "mouth", "label": "home door",
             "tiles": [[7, 17], [7, 18]]},
            {"kind": "seal", "label": "S seams (1-wide)",
             "tiles": [[8, 22], [12, 22]]},
            {"kind": "post", "label": "guard", "tiles": [[9, 21]],
             "facing": "east"},
            {"kind": "route", "label": "home convoy",
             "path": shortest(OW, R0)},
        ],
    },
    {
        "id": "west-heal-room",
        "label": "Own heal room",
        "note": (
            "Home-quarter recovery with three doors; one Null Field blankets "
            "the whole room, so heal denial is a real play."
        ),
        "marks": [
            {"kind": "zone", "label": "room", "tiles": zone(6, 5, 10, 7)},
            {"kind": "mouth", "label": "SW mouth", "tiles": [[5, 7]]},
            {"kind": "mouth", "label": "S mouth", "tiles": [[8, 8]]},
            {"kind": "mouth", "label": "E mouth", "tiles": [[11, 7]]},
            {"kind": "post", "label": "overwatch", "tiles": [[10, 5]],
             "facing": "west"},
        ],
    },
    {
        "id": "north-flank",
        "label": "North flank and galleries",
        "note": (
            "The committed flank: roof gallery, NE field and the pad-exit "
            "avenue; patrol posts, a mine pinch and the deep-raid crossing."
        ),
        "marks": [
            {"kind": "seal", "label": "loop crossing (1-wide)",
             "tiles": [[11, 2]]},
            {"kind": "post", "label": "patrol posts",
             "tiles": [[12, 2], [26, 1], [31, 2]]},
            {"kind": "lane", "label": "NE field lane", "from": [26, 1],
             "to": [33, 1]},
            {"kind": "route", "label": "committed flank entry",
             "path": shortest((4, 9), (12, 1))},
        ],
    },
    {
        "id": "homes",
        "label": "Home pads",
        "note": (
            "Spawn-protected pads with the exact scoring reactor inside; "
            "no pad tile can rail or even see any Well, heal or workshop."
        ),
        "marks": [
            {"kind": "zone", "label": "west pad", "tiles": zone(1, 10, 4, 16)},
            {"kind": "zone", "label": "east pad",
             "tiles": zone(32, 10, 35, 16)},
            {"kind": "mouth", "label": "west north exit",
             "tiles": [[3, 9], [4, 9]]},
            {"kind": "mouth", "label": "east south exit",
             "tiles": [[32, 17], [33, 17]]},
        ],
    },
]

required_routes = [entry["id"] for entry in spec["routes"]]
spec["requiredRouteIds"] = required_routes
spec["requiredLandmarkIds"] = [entry["id"] for entry in spec["landmarks"]]

out = HERE / "atlas.spec.json"
out.write_text(json.dumps(spec, indent=2) + "\n", encoding="utf-8")
print(f"wrote {out}")
