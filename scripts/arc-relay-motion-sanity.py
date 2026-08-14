#!/usr/bin/env python3
"""Fail a raw Arc Relay replay on replay-visible movement defects.

This is deliberately not a balance score. It covers executor seams that a
gallery must never hide behind aggregate movement rates: an own-squad closed
movement cycle, a recovery order that makes no geographic/health progress,
and a planner move trace that silently degrades into an actual Wait command.
It also rejects a body spending a sustained move episode orbiting a tiny patch
without reaching the named destination; rotating in place is the one-tile case.
Enemy occupancy pauses the recovery clock; contesting a scarce healer is play,
not a planner fault. Core progress is enforced by the frozen scorecard bars.
"""

from __future__ import annotations

import argparse
import collections
import gzip
import json
import re
import sys
from pathlib import Path


RECOVER_GOAL = re.compile(r"recover-health.*?march@(-?\d+),(-?\d+)")
MOVE_GOAL = re.compile(r":march@(-?\d+),(-?\d+)")
RECOVERY_NO_PROGRESS_BAR = 18
MOVEMENT_CYCLE_BAR = 3
RECOVERY_CONTEST_DISTANCE = 1
# Traffic debt escalates a continuously blocked body after 12 ticks. Give that
# bounded recovery a full window to resolve before calling the episode an
# orbit; the owner-caught failures ran for 100+ ticks and remain far over this
# bar.
LOCAL_MOVE_ORBIT_BAR = 18


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


def analyze(path: Path) -> dict:
    replay = read(path)
    recovery: dict[tuple[int, int, int], dict] = {}
    worst_recovery: dict[tuple[int, int, int], dict] = {}
    cycle_ticks: list[dict] = []
    false_move_traces: list[dict] = []
    move_windows: dict[tuple[int, int, int], dict] = {}
    local_move_orbits: list[dict] = []
    tripped_move_episodes: set[tuple[int, int, int, tuple[int, int]]] = set()

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
            for command in turn.get("commands", []):
                body_key = (command["unitId"], command["lifeId"])
                body = bodies.get(body_key)
                reason = command.get("debugMessage") or ""
                move_match = MOVE_GOAL.search(reason)
                actor_key = (team, *body_key)
                if body is not None and move_match is not None \
                        and ":at-target:" not in reason \
                        and ":at-contribution:" not in reason \
                        and "return-to-reactor" not in reason \
                        and "recover-health" not in reason:
                    seen_movers.add(actor_key)
                    goal = (int(move_match.group(1)), int(move_match.group(2)))
                    position = point(body["position"])
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
                            "bestDistance": chebyshev(position, goal),
                            "repeatTicks": 0,
                        }
                    else:
                        state = prior_move
                        distance = chebyshev(position, goal)
                        novel = position not in state["seenPositions"]
                        closer = distance < state["bestDistance"]
                        state["seenPositions"].add(position)
                        state["bestDistance"] = min(
                            state["bestDistance"], distance)
                        if novel or closer:
                            state["repeatTicks"] = 0
                            state["repeatPositions"] = {position}
                        else:
                            state["repeatTicks"] += 1
                            state["repeatPositions"].add(position)
                        state["lastTick"] = tick["tick"]
                    move_windows[actor_key] = state
                    episode = (*actor_key, goal)
                    if state["repeatTicks"] >= LOCAL_MOVE_ORBIT_BAR \
                            and episode not in tripped_move_episodes:
                        local_move_orbits.append({
                            "fromTick": tick["tick"]
                                - state["repeatTicks"] + 1,
                            "toTick": tick["tick"],
                            "teamId": team,
                            "unitId": command["unitId"],
                            "lifeId": command["lifeId"],
                            "goal": goal,
                            "distinctTiles": len(state["repeatPositions"]),
                            "reason": reason,
                        })
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
                goal_match = RECOVER_GOAL.search(reason)
                if body is None or goal_match is None:
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
        "localMoveOrbitBar": LOCAL_MOVE_ORBIT_BAR,
        "localMoveOrbits": local_move_orbits,
        "mechanicallyClean": not cycle_ticks and not trips
            and not false_move_traces and not local_move_orbits,
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
            f"local move orbits {len(result['localMoveOrbits'])}; "
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
        for trip in result["localMoveOrbits"][:8]:
            print(
                f"  t{trip['fromTick']}-t{trip['toTick']} "
                f"t{trip['teamId']} u{trip['unitId']} l{trip['lifeId']} "
                f"remained in {trip['distinctTiles']} tiles while moving "
                f"toward {tuple(trip['goal'])}: {trip['reason']}"
            )
    return 1 if args.fail and not result["mechanicallyClean"] else 0


if __name__ == "__main__":
    sys.exit(main())
