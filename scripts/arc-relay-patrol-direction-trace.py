#!/usr/bin/env python3
"""Which way round does the hunter actually walk its patrol loop?

Prints the deduped sequence of route waypoints the sheet's unit 0 (the
kestrel hunter, the whole shadow group) is nearest to, in MAP coordinates
with the side's binding applied, plus the own-frame advance (0 = own home
edge, 1 = enemy home edge) at each of them. A leg whose advance FALLS is a
leg walking home.

    scripts/arc-relay-patrol-direction-trace.py \
        <replay.json.gz> <team> <layout.json>

Team 0 binds as west, team 1 as east; the layout's binding for that side
(route alias plus transform) is applied, so the printed waypoints are the
ones the body actually walks.
"""

import gzip
import json
import sys


def rotate180(point, width, height):
    return [width - 1 - point[0], height - 1 - point[1]]


def main(path, team, layout_path):
    doc = json.load(gzip.open(path))
    layout = json.load(open(layout_path))
    contract = doc["header"]["contract"]
    width = contract["map"]["width"]
    height = contract["map"]["height"]
    routes = {route["routeId"]: route["waypoints"] for route in layout["routes"]}

    # Team 0 spawns west, team 1 east; the layout binds by that side.
    binding = next(
        value
        for value in layout["bindings"]
        if value["ownReactorSide"] == ("west" if team == 0 else "east")
    )
    routeId = binding["routeAliases"].get(
        "shadow-north-long", "shadow-north-long"
    )
    waypoints = routes[routeId]
    if binding["transform"] == "rotate-180":
        waypoints = [rotate180(point, width, height) for point in waypoints]
    # A loop's first and last waypoint are the same tile.
    if waypoints[0] == waypoints[-1]:
        waypoints = waypoints[:-1]

    def advance(position):
        """0 at own home edge, 1 at the enemy's."""
        return (position[0] if team == 0 else width - 1 - position[0]) / (
            width - 1
        )

    hunter = (team, 0)
    trail = []
    for tick in doc["ticks"]:
        for life in tick["postState"]["activeLives"]:
            actor = life["actorId"]
            if (actor["teamId"], actor["unitId"]) != hunter:
                continue
            here = (life["position"]["x"], life["position"]["y"])
            nearest = min(
                range(len(waypoints)),
                key=lambda index: max(
                    abs(waypoints[index][0] - here[0]),
                    abs(waypoints[index][1] - here[1]),
                ),
            )
            distance = max(
                abs(waypoints[nearest][0] - here[0]),
                abs(waypoints[nearest][1] - here[1]),
            )
            if distance > 3:
                continue  # off the route entirely (fighting, dead, home)
            if trail and trail[-1][1] == nearest:
                continue
            trail.append((tick["tick"], nearest, here))

    print(f"== {path.split('/')[-2]} team {team} route {routeId}")
    print(
        "   waypoints: "
        + " ".join(
            f"{index}:{point[0]},{point[1]}({advance(point):.2f})"
            for index, point in enumerate(waypoints)
        )
    )
    forward = backward = 0
    for previous, current in zip(trail, trail[1:]):
        step = (current[1] - previous[1]) % len(waypoints)
        if step == 0:
            continue
        # A single-waypoint step in authored order is "forward".
        if step <= len(waypoints) // 2:
            forward += 1
        else:
            backward += 1
    print(f"   authored-order steps {forward}, reversed-order steps {backward}")
    print("   trail (tick: waypoint@advance):")
    line = []
    for tick, index, here in trail:
        line.append(f"t{tick}:{index}@{advance(here):.2f}")
    for start in range(0, len(line), 10):
        print("     " + " ".join(line[start:start + 10]))


if __name__ == "__main__":
    main(sys.argv[1], int(sys.argv[2]), sys.argv[3])
