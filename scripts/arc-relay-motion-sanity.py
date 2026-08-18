#!/usr/bin/env python3
"""Fail a raw Arc Relay replay on replay-visible movement defects.

This is deliberately not a balance score. It covers executor seams that a
gallery must never hide behind aggregate movement rates: an own-squad closed
movement cycle, a recovery order that makes no geographic/health progress,
and a planner move trace that silently degrades into an actual Wait command.
It also rejects a body spending a sustained move episode orbiting a tiny patch
without reaching the named destination; rotating in place is the one-tile case.
A stationary episode whose every stalled tick names an explicit executor yield
(planner wait/turn, a blocked or denied destination, right-of-way, or a follow
target) is reported as a held yield rather than an orbit while it stays under a
hard ceiling: posts, escorts, and converge queues legitimately stand in place
with intent, and the ceiling keeps a queue that never clears failing loudly.
Enemy occupancy pauses the recovery clock; contesting a scarce healer is play,
not a planner fault. Loaded returns are checked here because aggregate Core
score bars cannot distinguish a carrier waiting for right-of-way from one that
is actively commanded into a backward loop.
"""

from __future__ import annotations

import argparse
import collections
import gzip
import json
import re
import sys
from pathlib import Path


MOVE_GOAL = re.compile(r":march@(-?\d+),(-?\d+)")
AVOID_DENIED = re.compile(r":avoid-denied-(-?\d+)-(-?\d+)@")
TRAFFIC_RIGHT_OF_WAY = re.compile(
    r":traffic-right-of-way-(-?\d+)-(-?\d+)")
RECOVERY_NO_PROGRESS_BAR = 18
MOVEMENT_CYCLE_BAR = 3
RECOVERY_CONTEST_DISTANCE = 1
# Traffic debt escalates a continuously blocked body after 12 ticks. Give that
# bounded recovery a full window to resolve before calling the episode an
# orbit; the owner-caught failures ran for 100+ ticks and remain far over this
# bar.
LOCAL_MOVE_ORBIT_BAR = 18
# A stationary episode is a held yield, not an orbit, while the body stays
# within this many tiles, every stalled tick names an explicit executor yield,
# and the stall stays under the ceiling. Two tiles covers one polite side-step;
# 45 ticks is far past the 12-tick traffic-debt escalation, so a queue that
# genuinely never clears still fails.
LOCAL_MOVE_YIELD_TILE_SPAN = 2
LOCAL_MOVE_YIELD_CEILING = 45
LOCAL_MOVE_YIELD_MARKERS = (
    ":epibt-wait:",
    ":epibt-turn:",
    ":move-blocked:",
    ":avoid-denied-",
    ":traffic-right-of-way-",
    ":traffic-detour-",
    ":planner-movement-lease@",
    ":planner-oscillation-yield@",
    ":move:body:",
    # A loaded carrier queueing at its own reactor. The stock sheets' spelling
    # of the same wait ("return-to-reactor") is carved out of orbit tracking
    # entirely; the materialized spelling stays under the yield ceiling.
    ":carry-home",
)
CARRIER_ROUTE_REGRESSION_BAR = 3
CARRIER_PAIR_REVERSAL_BAR = 2
HEADINGS = {
    0: (0, -1),
    1: (1, -1),
    2: (1, 0),
    3: (1, 1),
    4: (0, 1),
    5: (-1, 1),
    6: (-1, 0),
    7: (-1, -1),
    "north": (0, -1),
    "north-east": (1, -1),
    "east": (1, 0),
    "south-east": (1, 1),
    "south": (0, 1),
    "south-west": (-1, 1),
    "west": (-1, 0),
    "north-west": (-1, -1),
}


def read(path: Path) -> dict:
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8") as source:
        return json.load(source)


def chebyshev(left: tuple[int, int], right: tuple[int, int]) -> int:
    return max(abs(left[0] - right[0]), abs(left[1] - right[1]))


def point(value: dict) -> tuple[int, int]:
    return value["x"], value["y"]


def movement_cycle(events: list[dict]) -> list[list[int]]:
    blocked = [event["payload"] for event in events
               if event.get("kind") == "movement-blocked"]
    by_team: dict[int, list[dict]] = collections.defaultdict(list)
    for event in blocked:
        by_team[event["actorId"]["teamId"]].append(event)
    cycles: list[list[int]] = []
    for members in by_team.values():
        occupied = {point(event["from"]): event["actorId"]["unitId"]
                    for event in members}
        edge = {}
        for event in members:
            target = occupied.get(point(event["attemptedTo"]))
            if target is not None:
                edge[event["actorId"]["unitId"]] = target
        for start in sorted(edge):
            order: list[int] = []
            indexes: dict[int, int] = {}
            current = start
            while current in edge:
                if current in indexes:
                    cycle = order[indexes[current]:]
                    canonical = min(tuple(cycle[index:] + cycle[:index])
                                    for index in range(len(cycle)))
                    if len(canonical) >= MOVEMENT_CYCLE_BAR \
                            and list(canonical) not in cycles:
                        cycles.append(list(canonical))
                    break
                indexes[current] = len(order)
                order.append(current)
                current = edge[current]
    return cycles


def static_distance_field(replay: dict, team: int,
                          goal: tuple[int, int]) -> dict[tuple[int, int], int]:
    """Corner-strict terrain distance with the team's return pads blocked."""
    contract = replay["header"]["contract"]
    arena = contract["map"]
    rows = arena["tileRows"]
    width = arena["width"]
    height = arena["height"]
    assigned = {
        value.get("assignedRespawnSpawnId")
        for value in contract.get("lifecycleAssignments", [])
        if value["teamId"] == team
        and value.get("assignedRespawnSpawnId") is not None
    }
    blocked = {
        tuple(anchor["position"])
        for anchor in arena.get("spawnAnchors", [])
        if anchor["spawnId"] in assigned
    }
    blocked.discard(goal)

    def open_tile(tile: tuple[int, int]) -> bool:
        x, y = tile
        return 0 <= x < width and 0 <= y < height \
            and rows[y][x] != "#" and tile not in blocked

    distances = {goal: 0}
    queue = collections.deque([goal])
    while queue:
        current = queue.popleft()
        for dx, dy in HEADINGS.values():
            candidate = current[0] + dx, current[1] + dy
            if candidate in distances or not open_tile(candidate):
                continue
            if dx != 0 and dy != 0 \
                    and (not open_tile((current[0] + dx, current[1]))
                         or not open_tile((current[0], current[1] + dy))):
                continue
            distances[candidate] = distances[current] + 1
            queue.append(candidate)
    return distances


def analyze(path: Path) -> dict:
    replay = read(path)
    recovery: dict[tuple[int, int, int], dict] = {}
    worst_recovery: dict[tuple[int, int, int], dict] = {}
    cycle_ticks: list[dict] = []
    false_move_traces: list[dict] = []
    contradictory_traffic_traces: list[dict] = []
    move_windows: dict[tuple[int, int, int], dict] = {}
    local_move_orbits: list[dict] = []
    held_yields: list[dict] = []
    reported_held_episodes: set[
        tuple[int, int, int, tuple[int, int]]] = set()
    carrier_route_trips: list[dict] = []
    carrier_regression_debt: dict[tuple[int, int, int], int] = {}
    tripped_carrier_routes: set[tuple[int, int, int]] = set()
    tripped_move_episodes: set[tuple[int, int, int, tuple[int, int]]] = set()
    move_fields: dict[tuple[int, tuple[int, int]],
                      dict[tuple[int, int], int]] = {}
    carrier_fields: dict[tuple[int, tuple[int, int]],
                         dict[tuple[int, int], int]] = {}
    carrier_motion: dict[tuple[int, int, int], dict] = {}

    for tick in replay.get("ticks", []):
        cycles = movement_cycle(tick.get("events", []))
        if cycles:
            cycle_ticks.append({"tick": tick["tick"], "cycles": cycles})
        for turn in tick.get("mindTurns", []):
            team = turn["teamId"]
            bodies = {(body["actorId"]["unitId"],
                       body["actorId"]["lifeId"]): body
                      for body in turn["observation"].get("bodies", [])}
            enemies = [point(enemy["position"])
                       for enemy in turn["observation"].get("enemies", [])]
            seen: set[tuple[int, int, int]] = set()
            seen_movers: set[tuple[int, int, int]] = set()
            seen_carriers: set[tuple[int, int, int]] = set()
            for command in turn.get("commands", []):
                body_key = (command["unitId"], command["lifeId"])
                body = bodies.get(body_key)
                reason = command.get("debugMessage") or ""
                # Eligibility explains rejected higher Instructions after the
                # active order path. It must never make a diagnostic treat a
                # rejected recovery as the action being executed.
                active_order = reason.split(":eligibility[", 1)[0]
                move_match = MOVE_GOAL.search(reason)
                actor_key = (team, *body_key)
                denied_match = AVOID_DENIED.search(reason)
                right_of_way_match = TRAFFIC_RIGHT_OF_WAY.search(reason)
                if denied_match is not None and right_of_way_match is not None \
                        and denied_match.groups() == right_of_way_match.groups():
                    contradictory_traffic_traces.append({
                        "tick": tick["tick"],
                        "teamId": team,
                        "unitId": command["unitId"],
                        "lifeId": command["lifeId"],
                        "tile": [int(denied_match.group(1)),
                                 int(denied_match.group(2))],
                        "reason": reason,
                    })
                if body is not None and "return-to-reactor" in reason:
                    seen_carriers.add(actor_key)
                    carrier_problem = None
                    position = point(body["position"])
                    motion = carrier_motion.get(actor_key)
                    if motion is None:
                        motion = {
                            "prior": None,
                            "last": position,
                            "reversals": 0,
                        }
                    elif position != motion["last"]:
                        adjacent = chebyshev(position, motion["last"]) == 1
                        reversal = adjacent \
                            and motion["prior"] is not None \
                            and position == motion["prior"]
                        motion["reversals"] = motion["reversals"] + 1 \
                            if reversal else 0
                        motion["prior"] = motion["last"] if adjacent else None
                        motion["last"] = position
                    carrier_motion[actor_key] = motion
                    if motion["reversals"] >= CARRIER_PAIR_REVERSAL_BAR:
                        carrier_problem = (
                            "return repeatedly reversed across one tile pair")
                    operation = re.search(r":op-([^:]+)", reason)
                    if command.get("actionId") == "rotate" \
                            and operation is not None \
                            and "F" not in operation.group(1):
                        carrier_problem = "rotation prepared no movement"
                    if command.get("actionId") in {
                            "move-eight-way", "strafe-eight-way"}:
                        argument = next((value for value in
                                         command.get("arguments", [])
                                         if value.get("kind")
                                         == "projectile-heading"), None)
                        heading = None if argument is None else HEADINGS.get(
                            argument.get("value"))
                        if heading is None:
                            carrier_problem = "movement has no known heading"
                        elif move_match is not None:
                            goal = (int(move_match.group(1)),
                                    int(move_match.group(2)))
                            destination = (position[0] + heading[0],
                                           position[1] + heading[1])
                            key = team, goal
                            field = carrier_fields.setdefault(
                                key, static_distance_field(replay, team, goal))
                            before = field.get(position)
                            after = field.get(destination)
                            if before is None or after is None:
                                carrier_problem = (
                                    "movement left the static return graph")
                            else:
                                debt = max(
                                    0,
                                    carrier_regression_debt.get(actor_key, 0)
                                    + after - before)
                                carrier_regression_debt[actor_key] = debt
                                if debt >= CARRIER_ROUTE_REGRESSION_BAR:
                                    carrier_problem = (
                                        "commanded return accumulated "
                                        f"{debt} tiles of backward progress")
                    if carrier_problem is not None \
                            and actor_key not in tripped_carrier_routes:
                        carrier_route_trips.append({
                            "tick": tick["tick"],
                            "teamId": team,
                            "unitId": command["unitId"],
                            "lifeId": command["lifeId"],
                            "problem": carrier_problem,
                            "reason": reason,
                        })
                        tripped_carrier_routes.add(actor_key)
                if body is not None and move_match is not None \
                        and ":at-target:" not in reason \
                        and ":at-contribution:" not in reason \
                        and "return-to-reactor" not in reason \
                        and "recover-health" not in active_order:
                    seen_movers.add(actor_key)
                    goal = (int(move_match.group(1)), int(move_match.group(2)))
                    position = point(body["position"])
                    field_key = team, goal
                    field = move_fields.setdefault(
                        field_key, static_distance_field(replay, team, goal))
                    distance = field.get(
                        position, 100_000 + chebyshev(position, goal))
                    prior_move = move_windows.get(actor_key)
                    new_episode = prior_move is None \
                        or prior_move["goal"] != goal \
                        or prior_move["lastTick"] + 1 != tick["tick"]
                    if new_episode:
                        state = {
                            "goal": goal,
                            "lastTick": tick["tick"],
                            "seenPositions": {position},
                            "repeatPositions": {position},
                            "bestDistance": distance,
                            "repeatTicks": 0,
                            "unreasonedRepeatTicks": 0,
                        }
                    else:
                        state = prior_move
                        novel = position not in state["seenPositions"]
                        closer = distance < state["bestDistance"]
                        state["seenPositions"].add(position)
                        state["bestDistance"] = min(
                            state["bestDistance"], distance)
                        if novel or closer:
                            state["repeatTicks"] = 0
                            state["repeatPositions"] = {position}
                            state["unreasonedRepeatTicks"] = 0
                        else:
                            state["repeatTicks"] += 1
                            state["repeatPositions"].add(position)
                            if not any(marker in reason for marker
                                       in LOCAL_MOVE_YIELD_MARKERS):
                                state["unreasonedRepeatTicks"] += 1
                        state["lastTick"] = tick["tick"]
                    move_windows[actor_key] = state
                    episode = (*actor_key, goal)
                    if state["repeatTicks"] >= LOCAL_MOVE_ORBIT_BAR \
                            and episode not in tripped_move_episodes:
                        window = {
                            "fromTick": tick["tick"]
                                - state["repeatTicks"] + 1,
                            "toTick": tick["tick"],
                            "teamId": team,
                            "unitId": command["unitId"],
                            "lifeId": command["lifeId"],
                            "goal": goal,
                            "distinctTiles": len(state["repeatPositions"]),
                            "reason": reason,
                        }
                        held = state["unreasonedRepeatTicks"] == 0 \
                            and len(state["repeatPositions"]) \
                                <= LOCAL_MOVE_YIELD_TILE_SPAN \
                            and state["repeatTicks"] < LOCAL_MOVE_YIELD_CEILING
                        if held:
                            if episode not in reported_held_episodes:
                                held_yields.append(window)
                                reported_held_episodes.add(episode)
                        else:
                            local_move_orbits.append(window)
                            tripped_move_episodes.add(episode)
                # Suppression governors deliberately turn a proposed move
                # into a Wait and name why (lease/oscillation). The broken
                # seam this catches retained the generic move-blocked suffix:
                # the planner chose movement, the action selector chose a
                # chassis-forbidden verb, and submission silently fell back.
                if command.get("actionId") == "wait" \
                        and ":epibt-move:" in reason \
                        and ":move-blocked:" in reason:
                    false_move_traces.append({
                        "tick": tick["tick"],
                        "teamId": team,
                        "unitId": command["unitId"],
                        "lifeId": command["lifeId"],
                        "reason": reason,
                    })
                goal_match = MOVE_GOAL.search(reason)
                if body is None or goal_match is None \
                        or "recover-health" not in active_order:
                    continue
                key = (team, *body_key)
                seen.add(key)
                goal = (int(goal_match.group(1)), int(goal_match.group(2)))
                distance = chebyshev(point(body["position"]), goal)
                staged = ":at-contribution:heal:" in reason
                contested = any(chebyshev(enemy, goal)
                                <= RECOVERY_CONTEST_DISTANCE
                                for enemy in enemies)
                prior = recovery.get(key)
                if (prior is None or prior["goal"] != goal
                        or body["health"] > prior["health"]
                        or distance < prior["bestDistance"]):
                    state = {
                        "teamId": team,
                        "unitId": body_key[0],
                        "lifeId": body_key[1],
                        "goal": goal,
                        "health": body["health"],
                        "bestDistance": distance,
                        "fromTick": tick["tick"],
                        "uncontestedTicks": 0,
                        "contestedTicks": 0,
                    }
                else:
                    state = prior
                    state["health"] = body["health"]
                    if contested or staged:
                        state["contestedTicks"] += 1
                    else:
                        state["uncontestedTicks"] += 1
                recovery[key] = state
                best = worst_recovery.get(key)
                if best is None or state["uncontestedTicks"] > best[
                        "uncontestedTicks"]:
                    worst_recovery[key] = dict(state)
            for key in list(recovery):
                if key[0] == team and key not in seen:
                    recovery.pop(key)
            for key in list(move_windows):
                if key[0] == team and key not in seen_movers:
                    move_windows.pop(key)
            for key in list(carrier_regression_debt):
                if key[0] == team and key not in seen_carriers:
                    carrier_regression_debt.pop(key)
            for key in list(carrier_motion):
                if key[0] == team and key not in seen_carriers:
                    carrier_motion.pop(key)

    trips = [state for state in worst_recovery.values()
             if state["uncontestedTicks"] >= RECOVERY_NO_PROGRESS_BAR]
    return {
        "replay": str(path),
        "endTick": replay.get("result", {}).get("endTick"),
        "completionReason": replay.get("result", {}).get(
            "completionReason"),
        "closedAlliedMovementCycles": cycle_ticks,
        "recoveryNoProgressBar": RECOVERY_NO_PROGRESS_BAR,
        "recoveryNoProgressTrips": sorted(
            trips,
            key=lambda value: (-value["uncontestedTicks"],
                               value["teamId"], value["unitId"])),
        "falseMoveTracesOnWait": false_move_traces,
        "contradictoryTrafficTraces": contradictory_traffic_traces,
        "localMoveOrbitBar": LOCAL_MOVE_ORBIT_BAR,
        "localMoveOrbits": local_move_orbits,
        "localMoveYieldCeiling": LOCAL_MOVE_YIELD_CEILING,
        "heldYields": held_yields,
        "carrierRouteTrips": carrier_route_trips,
        "mechanicallyClean": not cycle_ticks and not trips
            and not false_move_traces and not contradictory_traffic_traces
            and not local_move_orbits
            and not carrier_route_trips,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("replay", type=Path)
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--fail", action="store_true")
    args = parser.parse_args()
    result = analyze(args.replay)
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print(
            f"{args.replay.name}: end {result['endTick']} "
            f"({result['completionReason']}); "
            f"closed cycles {len(result['closedAlliedMovementCycles'])}; "
            f"recovery no-progress "
            f"{len(result['recoveryNoProgressTrips'])}; "
            f"false move traces "
            f"{len(result['falseMoveTracesOnWait'])}; "
            f"contradictory traffic traces "
            f"{len(result['contradictoryTrafficTraces'])}; "
            f"local move orbits {len(result['localMoveOrbits'])}; "
            f"held yields {len(result['heldYields'])}; "
            f"carrier route trips {len(result['carrierRouteTrips'])}; "
            f"{'CLEAN' if result['mechanicallyClean'] else 'FAIL'}"
        )
        for trip in result["recoveryNoProgressTrips"]:
            print(
                "  recovery "
                f"t{trip['teamId']} u{trip['unitId']} l{trip['lifeId']}: "
                f"{trip['uncontestedTicks']} uncontested ticks without "
                f"health/distance progress toward {tuple(trip['goal'])}"
            )
        for trip in result["closedAlliedMovementCycles"][:8]:
            print(f"  t{trip['tick']}: closed allied cycles "
                  f"{trip['cycles']}")
        for trip in result["falseMoveTracesOnWait"][:8]:
            print(
                f"  t{trip['tick']} t{trip['teamId']} u{trip['unitId']} "
                "planned movement but submitted wait: "
                f"{trip['reason']}"
            )
        for trip in result["contradictoryTrafficTraces"][:8]:
            print(
                f"  t{trip['tick']} t{trip['teamId']} u{trip['unitId']} "
                f"l{trip['lifeId']} both avoids and claims right-of-way "
                f"at {tuple(trip['tile'])}: {trip['reason']}"
            )
        for trip in result["localMoveOrbits"][:8]:
            print(
                f"  t{trip['fromTick']}-t{trip['toTick']} "
                f"t{trip['teamId']} u{trip['unitId']} l{trip['lifeId']} "
                f"remained in {trip['distinctTiles']} tiles while moving "
                f"toward {tuple(trip['goal'])}: {trip['reason']}"
            )
        for trip in result["heldYields"][:8]:
            print(
                f"  held yield t{trip['fromTick']}-t{trip['toTick']} "
                f"t{trip['teamId']} u{trip['unitId']} l{trip['lifeId']} "
                f"stood within {trip['distinctTiles']} tiles toward "
                f"{tuple(trip['goal'])}: {trip['reason']}"
            )
        for trip in result["carrierRouteTrips"][:8]:
            print(
                f"  t{trip['tick']} t{trip['teamId']} u{trip['unitId']} "
                f"l{trip['lifeId']} carrier route: {trip['problem']}: "
                f"{trip['reason']}"
            )
    return 1 if args.fail and not result["mechanicallyClean"] else 0


if __name__ == "__main__":
    sys.exit(main())
