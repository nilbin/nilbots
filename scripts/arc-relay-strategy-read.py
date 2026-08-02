#!/usr/bin/env python3
"""Read coordinated Arc Relay strategy execution from durable broadcasts.

This diagnostic consumes only spectator-authoritative broadcasts, match
receipts, and the entrants' declared evaluation sheets. It never reads mind
observations or opponent-private state.
"""

from __future__ import annotations

import argparse
import collections
import gzip
import json
from pathlib import Path
from typing import Any, Iterable


REPO = Path(__file__).resolve().parent.parent
PASSIVE_ACTIONS = {"wait", "move-eight-way", "rotate"}


def read_json(path: Path) -> dict[str, Any]:
    with path.open("rb") as source:
        prefix = source.read(2)
    opener = gzip.open if prefix == b"\x1f\x8b" else open
    with opener(path, "rt", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: JSON root must be an object")
    return value


def resolve_path(receipt: Path, value: str) -> Path:
    candidate = Path(value)
    if candidate.is_absolute():
        return candidate
    repo_path = (REPO / candidate).resolve()
    return repo_path if repo_path.exists() else (receipt.parent / candidate).resolve()


def position(value: dict[str, int] | list[int]) -> tuple[int, int]:
    return (value["x"], value["y"]) if isinstance(value, dict) else (value[0], value[1])


def chebyshev(left: tuple[int, int], right: tuple[int, int]) -> int:
    return max(abs(left[0] - right[0]), abs(left[1] - right[1]))


def active_positions(world: list[Any]) -> dict[tuple[int, int, int], tuple[int, int]]:
    return {
        (item[0], item[1], item[2]): (item[6], item[7])
        for item in world[4]
    }


def mode(world: list[Any]) -> dict[str, Any]:
    value = world[7]
    if value.get("kind") != "arc-relay":
        raise ValueError("strategy read requires an Arc Relay broadcast")
    return value


def mirror(point: tuple[int, int], team: int, reactors: dict[int, tuple[int, int]], width: int) -> tuple[int, int]:
    opponent = next(value for key, value in reactors.items() if key != team)
    return (width - 1 - point[0], point[1]) if reactors[team][0] > opponent[0] else point


def zone_tiles(
    bounds: list[int],
    team: int,
    reactors: dict[int, tuple[int, int]],
    width: int,
) -> set[tuple[int, int]]:
    return {
        mirror((x, y), team, reactors, width)
        for x in range(bounds[0], bounds[2] + 1)
        for y in range(bounds[1], bounds[3] + 1)
    }


def participant_rows(receipt: dict[str, Any]) -> list[dict[str, Any]]:
    return receipt.get("participants", receipt.get("Participants", []))


def field(value: dict[str, Any], lower: str, upper: str) -> Any:
    return value[lower] if lower in value else value[upper]


def find_receipt(broadcast: Path) -> Path:
    for name in ("match-record.json", "run.json"):
        candidate = broadcast.parent / name
        if candidate.is_file():
            return candidate
    raise FileNotFoundError(f"no match receipt beside {broadcast}")


def plan_episodes(ticks: Iterable[int]) -> list[dict[str, int]]:
    episodes: list[list[int]] = []
    for tick in sorted(set(ticks)):
        if not episodes or tick != episodes[-1][-1] + 1:
            episodes.append([tick])
        else:
            episodes[-1].append(tick)
    return [
        {"startTick": values[0], "endTick": values[-1], "visibleTicks": len(values)}
        for values in episodes
    ]


def carrier_positions(world: list[Any], team: int) -> list[tuple[int, int]]:
    return [
        position(core["position"])
        for core in mode(world)["visibleCores"]
        if (core.get("carrierActorId") or {}).get("teamId") == team
    ]


def loose_positions(world: list[Any]) -> list[tuple[int, int]]:
    return [
        position(core["position"])
        for core in mode(world)["visibleCores"]
        if core["disposition"] == "loose"
    ]


def closest(values: list[tuple[int, int]], origin: tuple[int, int]) -> tuple[int, int] | None:
    return min(values, key=lambda value: (chebyshev(origin, value), value[1], value[0])) if values else None


def intended_goals(
    sheet: dict[str, Any],
    plan: dict[str, Any],
    team: int,
    unit: int,
    before: tuple[int, int],
    world: list[Any],
    reactors: dict[int, tuple[int, int]],
    width: int,
) -> set[tuple[int, int]]:
    intent = plan["overlay"].get("position")
    if not intent:
        return set()
    kind = intent["kind"]
    target = intent.get("target", "")
    goals: set[tuple[int, int]] = set()
    if kind == "zone":
        goals = zone_tiles(sheet["zones"][target], team, reactors, width)
    elif kind == "path":
        goals = {mirror(position(value), team, reactors, width) for value in sheet["paths"][target]}
        fallback = intent.get("fallbackZone")
        if fallback:
            goals.update(zone_tiles(sheet["zones"][fallback], team, reactors, width))
    elif kind == "anchor-offset":
        enemy = next(value for value in reactors if value != team)
        anchor = {
            "own-reactor": reactors[team],
            "enemy-reactor": reactors[enemy],
            "nearest-enemy-carrier": closest(carrier_positions(world, enemy), before),
            "nearest-own-carrier": closest(carrier_positions(world, team), before),
            "nearest-loose-core": closest(loose_positions(world), before),
            "next-well": position(min(
                (well for well in mode(world)["wells"] if well["nextScheduledBirthTick"] is not None),
                key=lambda well: (well["nextScheduledBirthTick"], well["wellId"]),
                default={"position": {"x": before[0], "y": before[1]}},
            )["position"]),
        }.get(target)
        if anchor is not None:
            offset = intent.get("offset", [0, 0])
            scope = sorted(plan["scope"].get("unitIds", []))
            index = scope.index(unit) if unit in scope else -1
            formations = plan["overlay"].get("formationOffsets", [])
            formation = formations[index] if 0 <= index < len(formations) else [0, 0]
            direction = -1 if reactors[team][0] > reactors[enemy][0] else 1
            goals.add((
                anchor[0] + direction * (offset[0] + formation[0]),
                anchor[1] + offset[1] + formation[1],
            ))
        fallback = intent.get("fallbackZone")
        if not goals and fallback:
            goals = zone_tiles(sheet["zones"][fallback], team, reactors, width)
    return goals


def rear_proof(
    broadcast: dict[str, Any],
    sheet: dict[str, Any],
    team: int,
    plan_ticks: list[int],
    reactors: dict[int, tuple[int, int]],
    width: int,
) -> dict[str, Any]:
    if not plan_ticks:
        return {"activated": False}
    first = min(plan_ticks)
    staging = zone_tiles(sheet["zones"]["enemy-rear-staging"], team, reactors, width)
    infiltrators = [4, 5]
    turns_by_tick = {
        tick: {
            (turn[0][0], turn[0][1], turn[0][2]): turn
            for turn in broadcast["turns"][tick]
        }
        for tick in range(len(broadcast["turns"]))
    }
    staged: dict[str, bool] = {}
    quiet: dict[str, bool] = {}
    for unit in infiltrators:
        positions_ok = True
        actions_ok = True
        # Broadcast role labels are the role visible at the start of the next
        # turn, while the accepted action is published on the execution tick.
        # Exclude the immediately preceding tick, which can already contain
        # the activation action under the prior visible label.
        for tick in range(max(0, first - 7), max(0, first - 1)):
            positions = active_positions(broadcast["worlds"][tick])
            actors = [actor for actor in positions if actor[0] == team and actor[1] == unit]
            if not actors or positions[actors[0]] not in staging:
                positions_ok = False
                continue
            turn = turns_by_tick[tick].get(actors[0])
            action = turn[5][0] if turn and turn[5] else "wait"
            actions_ok &= action in PASSIVE_ACTIONS
        staged[str(unit)] = positions_ok
        quiet[str(unit)] = actions_ok

    enemy = next(value for value in reactors if value != team)
    contact: dict[str, Any] | None = None
    movement = False
    fighting = False
    for tick in sorted(set(plan_ticks)):
        world = broadcast["worlds"][tick]
        positions = active_positions(world)
        enemies = carrier_positions(world, enemy)
        for turn in broadcast["turns"][tick]:
            actor = tuple(turn[0])
            if actor[0] != team or turn[2] != "rear-collapse":
                continue
            action = turn[5][0] if turn[5] else "wait"
            movement |= action == "move-eight-way"
            if action in PASSIVE_ACTIONS:
                continue
            fighting = True
            carrier = closest(enemies, positions.get(actor, reactors[enemy]))
            if contact is None and carrier is not None and actor in positions:
                homeward = positions[actor][0] > carrier[0] if reactors[enemy][0] > reactors[team][0] else positions[actor][0] < carrier[0]
                contact = {
                    "tick": tick,
                    "unitId": actor[1],
                    "action": action,
                    "ambusherPosition": list(positions[actor]),
                    "carrierPosition": list(carrier),
                    "enemyHomewardSide": homeward,
                }
    episodes = plan_episodes(plan_ticks)
    return {
        "activated": True,
        "firstActivationTick": first,
        "stagedSixTicksByUnit": staged,
        "quietSixTicksByUnit": quiet,
        "firstContact": contact,
        "movedAfterActivation": movement,
        "foughtAfterActivation": fighting,
        "visibleEpisodes": episodes,
        "observedExitBeforeMatchEnd": episodes[-1]["endTick"] < len(broadcast["turns"]) - 1,
    }


def read_match(broadcast_path: Path) -> dict[str, Any]:
    broadcast = read_json(broadcast_path)
    receipt_path = find_receipt(broadcast_path)
    receipt = read_json(receipt_path)
    contract = broadcast["header"]["contract"]
    width = len(contract["map"]["tileRows"][0])
    reactors = {
        value["teamId"]: position(value["position"])
        for value in mode(broadcast["worlds"][0])["reactors"]
    }
    participant_results: list[dict[str, Any]] = []
    for raw in participant_rows(receipt):
        team = int(field(raw, "teamId", "TeamId"))
        sheet_path = resolve_path(receipt_path, field(raw, "sheetPath", "SheetPath"))
        sheet = read_json(sheet_path)
        plans = {plan["id"]: plan for plan in sheet.get("gambits", [])}
        ticks_by_plan: dict[str, list[int]] = collections.defaultdict(list)
        actions_by_plan: dict[str, collections.Counter[str]] = collections.defaultdict(collections.Counter)
        adherence: dict[str, list[bool]] = collections.defaultdict(list)
        surrendered: collections.Counter[str] = collections.Counter()
        for tick, turns in enumerate(broadcast["turns"]):
            before_world = broadcast["initial"] if tick == 0 else broadcast["worlds"][tick - 1]
            after_world = broadcast["worlds"][tick]
            before_positions = active_positions(before_world)
            after_positions = active_positions(after_world)
            wells = [position(value["position"]) for value in mode(before_world)["wells"]]
            for turn in turns:
                actor = tuple(turn[0])
                plan_id = turn[2]
                if actor[0] != team or plan_id not in plans:
                    continue
                ticks_by_plan[plan_id].append(tick)
                action = turn[5][0] if turn[5] else "wait"
                actions_by_plan[plan_id][action] += 1
                before = before_positions.get(actor, after_positions.get(actor))
                after = after_positions.get(actor, before)
                if before is None or after is None:
                    continue
                goals = intended_goals(
                    sheet, plans[plan_id], team, actor[1], before,
                    before_world, reactors, width)
                if goals:
                    in_goal = after in goals
                    reduced = min(chebyshev(after, goal) for goal in goals) < min(
                        chebyshev(before, goal) for goal in goals)
                    adherence[plan_id].append(in_goal or reduced)
                surrendered[plan_id] += int(all(chebyshev(after, well) > 4 for well in wells))
        plan_results = []
        for plan_id, plan in plans.items():
            samples = adherence[plan_id]
            plan_results.append({
                "planId": plan_id,
                "priority": plan["priority"],
                "visibleEpisodes": plan_episodes(ticks_by_plan[plan_id]),
                "visibleScopedBodyTicks": len(ticks_by_plan[plan_id]),
                "authoredPositionAdherence": (
                    sum(samples) / len(samples) if samples else None),
                "adherenceSamples": len(samples),
                "objectivePresenceSurrenderedBodyTicks": surrendered[plan_id],
                "acceptedActions": dict(sorted(actions_by_plan[plan_id].items())),
            })
        participant_results.append({
            "teamId": team,
            "entrantId": raw.get("entrantId", Path(sheet_path).stem),
            "sheet": str(sheet_path.relative_to(REPO)),
            "family": sheet.get("dynamicStrategyAudit", {}).get("family"),
            "strategyKind": (
                "static" if sheet.get("dynamicStrategyAudit", {}).get("pairedControl")
                else "dynamic"),
            "plans": plan_results,
            "rearAmbushProof": rear_proof(
                broadcast, sheet, team, ticks_by_plan.get("rear-collapse", []),
                reactors, width) if "rear-collapse" in plans else None,
        })
    scorecard_path = broadcast_path.parent / "scorecard.json"
    eligible = None
    if scorecard_path.is_file():
        eligible = read_json(scorecard_path)["feltDegeneracy"]["matchEligibleForCohortRead"]
    result = receipt.get("result", receipt.get("Result", {}))
    return {
        "broadcast": str(broadcast_path),
        "matchId": receipt.get("matchId", broadcast_path.parent.name),
        "runtime": receipt.get("executionRuntime", "in-process"),
        "seed": field(receipt, "seed", "Seed"),
        "winnerTeamId": result.get("winnerTeamId", result.get("WinnerTeamId")),
        "completionReason": result.get("reason", result.get("Reason")),
        "cohortEligible": eligible,
        "participants": participant_results,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    source = args.input.resolve()
    broadcasts = [source] if source.is_file() else sorted(source.rglob("broadcast.json.gz"))
    matches = [read_match(path) for path in broadcasts]
    plan_rows = [
        plan
        for match in matches
        for participant in match["participants"]
        for plan in participant["plans"]
    ]
    output = {
        "schema": "arc-relay-strategy-read-v1",
        "authority": "durable spectator broadcast plus declared evaluation sheet",
        "matchCount": len(matches),
        "allCohortEligible": all(
            match["cohortEligible"] is not False for match in matches),
        "planActivationCounts": dict(sorted(collections.Counter(
            plan["planId"] for plan in plan_rows if plan["visibleScopedBodyTicks"]
        ).items())),
        "matches": matches,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(output, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
