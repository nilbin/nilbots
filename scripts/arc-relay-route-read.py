#!/usr/bin/env python3
"""Read route productivity and progress from an Arc Relay raw replay.

This diagnostic separates three facts that previously got collapsed into
"the body had a move order":

* the selected executor channel actually reached movement;
* the mind submitted a movement command and the engine accepted it; and
* bodies made geographic progress, including the closest approach to the
  Well nearest each team's starting Reactor.

Use ``--reason-contains`` to isolate one authored intent (for example
``centre-push``). Counts are body-ticks, not match ticks: one selected command
for one live body is one observation.

Usage:
  scripts/arc-relay-route-read.py REPLAY [REPLAY ...]
      [--team N] [--reason-contains TEXT] [--json]
"""

from __future__ import annotations

import argparse
import gzip
import json
import re
from pathlib import Path


MOVEMENT_TARGET = re.compile(r"@-?\d+,-?\d+")


def read_replay(path: Path) -> dict:
    if path.suffix == ".gz":
        with gzip.open(path, "rt", encoding="utf-8") as handle:
            return json.load(handle)
    return json.loads(path.read_text(encoding="utf-8"))


def chebyshev(left: tuple[int, int], right: tuple[int, int]) -> int:
    return max(abs(left[0] - right[0]), abs(left[1] - right[1]))


def region_point(regions: dict[str, dict], region_id: str) -> tuple[int, int]:
    tiles = regions[region_id]["tiles"]
    if not tiles:
        raise ValueError(f"region '{region_id}' has no tiles")
    return tuple(tiles[0])


def nearest_starting_wells(replay: dict) -> dict[int, tuple[int, int]]:
    contract = replay["header"]["contract"]
    binding = contract.get("modeMapBinding") or {}
    well_ids = binding.get("orderedWellRegionIds") or []
    if not well_ids:
        return {}
    regions = {
        region["regionId"]: region
        for region in contract["map"].get("regions", [])
    }
    well_points = [region_point(regions, value) for value in well_ids]
    participants = {
        value["participantId"]: value["teamId"]
        for value in contract["topology"]["participants"]
    }
    reactor_role = binding.get("reactorRegionRoleId")
    result: dict[int, tuple[int, int]] = {}
    for assignment in contract.get("participantRegionAssignments", []):
        if assignment.get("regionRoleId") != reactor_role:
            continue
        reactor = region_point(regions, assignment["mapRegionId"])
        team = participants[assignment["participantId"]]
        result[team] = min(
            well_points,
            key=lambda point: (chebyshev(reactor, point), point[1], point[0]),
        )
    return result


def all_lives(replay: dict):
    initial = replay.get("initialFrame", {}).get("state", {})
    yield from initial.get("activeLives", []) or []
    for tick in replay.get("ticks", []):
        state = tick.get("postState") or {}
        yield from state.get("activeLives", []) or []


def is_movement(action_id: str) -> bool:
    return action_id.startswith("move") or action_id.startswith("strafe")


def percentage(value: int, total: int) -> float:
    return value * 100.0 / total if total else 0.0


def read(path: Path, team_filter: int | None, reason_filter: str | None) -> dict:
    replay = read_replay(path)
    wells = nearest_starting_wells(replay)
    teams = sorted({
        value["teamId"]
        for value in replay["header"]["contract"]["topology"]["participants"]
    })
    if team_filter is not None:
        teams = [team_filter]

    summaries = {
        team: {
            "selectedBodyTicks": 0,
            "movementChannelTicks": 0,
            "movementCommands": 0,
            "successfulMovementCommands": 0,
            "blockedMovementCommands": 0,
            "routeBypassUnreachable": 0,
            "routeBypassStuck": 0,
            "routeNoPath": 0,
            "routeArrived": 0,
            "routeBlocked": 0,
            "oscillationYield": 0,
            "deliveryTimeoutTicks": 0,
        }
        for team in teams
    }

    for tick in replay.get("ticks", []):
        for turn in tick.get("mindTurns", []) or []:
            team = turn["teamId"]
            if team not in summaries:
                continue
            resolutions = {
                (value["unitId"], value["lifeId"]): value
                for value in turn.get("resolutions", []) or []
            }
            for command in turn.get("commands", []) or []:
                reason = command.get("debugMessage") or ""
                if reason_filter and reason_filter not in reason:
                    continue
                summary = summaries[team]
                summary["selectedBodyTicks"] += 1
                if MOVEMENT_TARGET.search(reason):
                    summary["movementChannelTicks"] += 1
                if "route-bypass-unreachable" in reason:
                    summary["routeBypassUnreachable"] += 1
                if "route-bypass-stuck" in reason:
                    summary["routeBypassStuck"] += 1
                if "route-" in reason and "-no-path" in reason:
                    summary["routeNoPath"] += 1
                if "route-" in reason and "-arrived" in reason:
                    summary["routeArrived"] += 1
                if "route-" in reason and ":blocked" in reason:
                    summary["routeBlocked"] += 1
                if "oscillation-yield" in reason:
                    summary["oscillationYield"] += 1
                if "custody:delivery-timeout-" in reason:
                    summary["deliveryTimeoutTicks"] += 1

                if not is_movement(command["actionId"]):
                    continue
                summary["movementCommands"] += 1
                resolution = resolutions.get(
                    (command["unitId"], command["lifeId"]), {})
                outcome = (resolution.get("actionResolution") or {}).get(
                    "outcome")
                if outcome == "success":
                    summary["successfulMovementCommands"] += 1
                elif outcome == "blocked":
                    summary["blockedMovementCommands"] += 1

    closest: dict[int, int | None] = {team: None for team in teams}
    for life in all_lives(replay):
        team = life["actorId"]["teamId"]
        if team not in closest or team not in wells:
            continue
        position = (life["position"]["x"], life["position"]["y"])
        distance = chebyshev(position, wells[team])
        prior = closest[team]
        closest[team] = distance if prior is None else min(prior, distance)

    # Exact-life single-tile stalls. A death/respawn or observation gap ends a
    # run; stable unit ids retain their worst exact-life run for the side
    # summary. Tick difference matches replay vocabulary: t69 through t599 is
    # a 530-tick stall.
    runs: dict[tuple[int, int, int], dict] = {}
    longest_by_unit: dict[tuple[int, int], dict] = {}
    for tick in replay.get("ticks", []):
        tick_number = tick["tick"]
        lives = (tick.get("tickStart") or {}).get("state", {}).get(
            "activeLives", []) or []
        seen: set[tuple[int, int, int]] = set()
        for life in lives:
            actor = life["actorId"]
            team = actor["teamId"]
            if team not in summaries:
                continue
            key = (team, actor["unitId"], actor["lifeId"])
            seen.add(key)
            position = (life["position"]["x"], life["position"]["y"])
            prior = runs.get(key)
            if (prior is None or prior["position"] != position
                    or prior["lastTick"] != tick_number - 1):
                current = {
                    "position": position,
                    "startTick": tick_number,
                    "lastTick": tick_number,
                }
            else:
                current = prior
                current["lastTick"] = tick_number
            runs[key] = current
            duration = current["lastTick"] - current["startTick"]
            unit_key = (team, actor["unitId"])
            best = longest_by_unit.get(unit_key)
            if best is None or duration > best["ticks"]:
                longest_by_unit[unit_key] = {
                    "ticks": duration,
                    "position": list(position),
                    "lifeId": actor["lifeId"],
                }
        for key in list(runs):
            if key[0] in summaries and key not in seen:
                runs.pop(key)

    for team, summary in summaries.items():
        total = summary["selectedBodyTicks"]
        summary["movementChannelPercent"] = percentage(
            summary["movementChannelTicks"], total)
        summary["movementCommandPercent"] = percentage(
            summary["movementCommands"], total)
        summary["successfulMovementPercent"] = percentage(
            summary["successfulMovementCommands"], total)
        summary["nearestStartingWell"] = list(wells[team]) \
            if team in wells else None
        summary["closestWellDistance"] = closest[team]
        stalls = {
            unit: value
            for (stall_team, unit), value in longest_by_unit.items()
            if stall_team == team
        }
        if stalls:
            worst_unit, worst = max(
                stalls.items(),
                key=lambda value: (value[1]["ticks"], -value[0]),
            )
            summary["longestSingleTileStallTicks"] = worst["ticks"]
            summary["longestSingleTileStallUnit"] = worst_unit
            summary["longestSingleTileStallPosition"] = worst["position"]
            summary["bodiesOver100Stationary"] = sum(
                value["ticks"] > 100 for value in stalls.values())
        else:
            summary["longestSingleTileStallTicks"] = 0
            summary["longestSingleTileStallUnit"] = None
            summary["longestSingleTileStallPosition"] = None
            summary["bodiesOver100Stationary"] = 0

    return {
        "replay": str(path),
        "mapId": replay["header"]["contract"]["map"]["mapId"],
        "endTick": replay.get("ticks", [{}])[-1].get("tick", -1),
        "reasonContains": reason_filter,
        "teams": {str(team): summaries[team] for team in teams},
    }


def print_human(result: dict) -> None:
    selector = result["reasonContains"] or "all commands"
    print(
        f"{Path(result['replay']).parent.name or Path(result['replay']).name} "
        f"map={result['mapId']} end={result['endTick']} filter={selector!r}")
    for team, summary in result["teams"].items():
        total = summary["selectedBodyTicks"]
        well = summary["nearestStartingWell"]
        well_text = "n/a" if well is None else f"({well[0]},{well[1]})"
        print(
            f"  t{team} own-well {well_text} closest "
            f"{summary['closestWellDistance']} | selected {total} | "
            f"movement-channel {summary['movementChannelTicks']}/{total} "
            f"({summary['movementChannelPercent']:.1f}%) | commands "
            f"{summary['movementCommands']}/{total} "
            f"({summary['movementCommandPercent']:.1f}%) | success "
            f"{summary['successfulMovementCommands']}/{total} "
            f"({summary['successfulMovementPercent']:.1f}%)")
        print(
            "      route "
            f"bypass-unreachable={summary['routeBypassUnreachable']} "
            f"bypass-stuck={summary['routeBypassStuck']} "
            f"no-path={summary['routeNoPath']} "
            f"arrived={summary['routeArrived']} "
            f"blocked={summary['routeBlocked']} "
            f"oscillation-yield={summary['oscillationYield']}")
        unit = summary["longestSingleTileStallUnit"]
        unit_text = "n/a" if unit is None else f"u{unit}"
        print(
            "      stall "
            f"longest={summary['longestSingleTileStallTicks']}t "
            f"{unit_text}@{summary['longestSingleTileStallPosition']} "
            f"bodies>100t={summary['bodiesOver100Stationary']} "
            f"delivery-timeout={summary['deliveryTimeoutTicks']}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("replays", nargs="+", type=Path)
    parser.add_argument("--team", type=int)
    parser.add_argument("--reason-contains")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    results = [
        read(path, args.team, args.reason_contains)
        for path in args.replays
    ]
    if args.json:
        print(json.dumps(results, indent=2, sort_keys=True))
        return
    for index, result in enumerate(results):
        if index:
            print()
        print_human(result)


if __name__ == "__main__":
    main()
