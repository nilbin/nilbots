#!/usr/bin/env python3
"""Attribute WHY each stuck carrier is stuck (the pocketed-carrier family).

The stuckCarrier bar says THAT a loaded carrier held one tile for 30+
consecutive ticks; this script says WHY, per tick, from the same broadcast
worlds: for every tick of every stuck run it looks at the carrier's
bankward exits (non-wall neighbours strictly closer to its own reactor by
Chebyshev) and classifies the tick:

  free            - at least one bankward exit was EMPTY; the pin is not
                    physical, the carrier (or its custody logic) chose to
                    stand
  friendly-blocked- every bankward exit was a wall or an own body, and no
                    enemy stood on any of them
  enemy-involved  - every bankward exit was blocked and at least one held
                    an enemy body

The run's verdict is the majority class, and the per-unit blocker table
names which own units spent how many ticks standing on bankward exits -
the direct input for an escort-yield or custody-internal fix (owner
finding 2026-08: "it's friendly bodies blocking it").

Usage: arc-relay-pocket-attribution.py BROADCAST.json.gz [more...]
"""

from __future__ import annotations

import collections
import gzip
import json
import sys
from pathlib import Path

STUCK_TICKS = 30


def read_json(path: Path) -> dict:
    if path.suffix == ".gz":
        with gzip.open(path, "rt", encoding="utf-8") as handle:
            return json.load(handle)
    return json.loads(path.read_text(encoding="utf-8"))


def position(value: dict | list) -> tuple[int, int]:
    if isinstance(value, dict):
        return (value["x"], value["y"])
    return (value[0], value[1])


def actor_key(value: dict | list | None) -> tuple[int, int, int] | None:
    if value is None:
        return None
    if isinstance(value, dict):
        return (value["teamId"], value["unitId"], value["lifeId"])
    return (value[0], value[1], value[2])


def chebyshev(a: tuple[int, int], b: tuple[int, int]) -> int:
    return max(abs(a[0] - b[0]), abs(a[1] - b[1]))


def active_lives(world: list) -> list[dict]:
    return [
        {
            "actor": (item[0], item[1], item[2]),
            "team": item[0],
            "unit": item[1],
            "position": (item[6], item[7]),
        }
        for item in world[4]
    ]


def mode(world: list) -> dict:
    value = world[7]
    if value.get("kind") != "arc-relay":
        raise ValueError("broadcast world is not Arc Relay")
    return value


def stuck_runs(broadcast: dict) -> list[dict]:
    """Same run rule as the scorecard's stuckCarrier bar."""
    active: dict[str, dict] = {}
    completed: list[dict] = []

    def finish(key: str, through_tick: int) -> None:
        state = active.pop(key, None)
        if state is None:
            return
        if state["ticks"] >= STUCK_TICKS:
            completed.append(
                {
                    "coreId": key,
                    "teamId": state["teamId"],
                    "carrier": state["carrier"],
                    "position": state["position"],
                    "fromTick": state["fromTick"],
                    "throughTick": through_tick,
                    "ticks": state["ticks"],
                }
            )

    for tick, world in enumerate(broadcast["worlds"]):
        present: set[str] = set()
        for core in mode(world)["visibleCores"]:
            carrier = actor_key(core.get("carrierActorId"))
            if core["disposition"] != "carried" or carrier is None:
                continue
            key = str(core["coreId"])
            present.add(key)
            current = {
                "teamId": carrier[0],
                "carrier": carrier,
                "position": position(core["position"]),
            }
            prior = active.get(key)
            if (
                prior is not None
                and prior["teamId"] == current["teamId"]
                and prior["carrier"] == current["carrier"]
                and prior["position"] == current["position"]
            ):
                prior["ticks"] += 1
                continue
            finish(key, tick - 1)
            active[key] = {**current, "fromTick": tick, "ticks": 1}
        for key in list(active):
            if key not in present:
                finish(key, tick - 1)
    for key in list(active):
        finish(key, len(broadcast["worlds"]) - 1)
    return completed


def attribute(broadcast: dict, run: dict) -> dict:
    contract_map = broadcast["header"]["contract"]["map"]
    rows = contract_map["tileRows"]
    width = contract_map["width"]
    height = contract_map["height"]

    def is_wall(tile: tuple[int, int]) -> bool:
        x, y = tile
        return x < 0 or y < 0 or x >= width or y >= height or rows[y][x] == "#"

    reactor = next(
        position(item["position"])
        for item in mode(broadcast["worlds"][run["fromTick"]])["reactors"]
        if item["teamId"] == run["teamId"]
    )
    spot = run["position"]
    neighbours = [
        (spot[0] + dx, spot[1] + dy)
        for dx in (-1, 0, 1)
        for dy in (-1, 0, 1)
        if (dx, dy) != (0, 0)
    ]
    bankward = [
        tile
        for tile in neighbours
        if not is_wall(tile)
        and chebyshev(tile, reactor) < chebyshev(spot, reactor)
    ]
    tick_classes = collections.Counter()
    blockers = collections.Counter()
    for tick in range(run["fromTick"], run["throughTick"] + 1):
        lives = active_lives(broadcast["worlds"][tick])
        occupied = {
            life["position"]: life
            for life in lives
            if life["actor"] != run["carrier"]
        }
        free = [tile for tile in bankward if tile not in occupied]
        if not bankward:
            tick_classes["walled"] += 1
            continue
        if free:
            tick_classes["free"] += 1
            continue
        holders = [occupied[tile] for tile in bankward]
        if any(life["team"] != run["teamId"] for life in holders):
            tick_classes["enemy-involved"] += 1
        else:
            tick_classes["friendly-blocked"] += 1
        for life in holders:
            if life["team"] == run["teamId"]:
                blockers[life["unit"]] += 1
    verdict = tick_classes.most_common(1)[0][0] if tick_classes else "empty"
    return {
        "coreId": run["coreId"],
        "teamId": run["teamId"],
        "carrier": list(run["carrier"]),
        "position": list(spot),
        "window": [run["fromTick"], run["throughTick"]],
        "ticks": run["ticks"],
        "bankwardExits": [list(tile) for tile in bankward],
        "tickClasses": dict(tick_classes),
        "verdict": verdict,
        "friendlyBlockersByUnit": {
            str(unit): count for unit, count in blockers.most_common()
        },
    }


def main() -> None:
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    for arg in sys.argv[1:]:
        path = Path(arg)
        broadcast = read_json(path)
        runs = stuck_runs(broadcast)
        report = [attribute(broadcast, run) for run in runs]
        print(
            json.dumps(
                {"broadcast": path.name, "stuckRuns": report},
                indent=1,
            )
        )


if __name__ == "__main__":
    main()
