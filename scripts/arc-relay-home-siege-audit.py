#!/usr/bin/env python3
"""Audit Home Siege causality and registered camping diagnostics."""

from __future__ import annotations

import argparse
import gzip
import json
import re
from pathlib import Path
from typing import Any


def read_json(path: Path) -> dict[str, Any]:
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: expected an object")
    return value


def distance(first: tuple[int, int], second: tuple[int, int]) -> int:
    return max(abs(first[0] - second[0]), abs(first[1] - second[1]))


def phase_from_role(role: Any) -> str | None:
    if not isinstance(role, str):
        return None
    phase = role.split("-", 1)[0]
    return phase if phase in {"assault", "occupy", "regroup", "breach"} else None


def task_claims(debug: str, task_id: str) -> dict[int, str]:
    match = re.search(
        rf"(?:^|;){re.escape(task_id)}=active\[[^\]]*\|c=([^\]]+)\]",
        debug,
    )
    if match is None or match.group(1) == "-":
        return {}
    claims: dict[int, str] = {}
    for claim in match.group(1).split(","):
        unit, separator, role = claim.partition(":")
        if separator and unit.isdigit():
            claims[int(unit)] = role
    return claims


def debug_int(debug: str, field: str) -> int | None:
    match = re.search(rf"(?:^|; ){re.escape(field)}=(\d+)", debug)
    return int(match.group(1)) if match is not None else None


def returning_units(debug: str) -> set[int]:
    match = re.search(r"(?:^|; )returning=([^;]*)", debug)
    if match is None or not match.group(1):
        return set()
    return {
        int(value) for value in match.group(1).split(",") if value.isdigit()
    }


def participant_region_tiles(
    contract: dict[str, Any], participant_id: int, role_id: str,
) -> set[tuple[int, int]]:
    assignment = next(
        item for item in contract["participantRegionAssignments"]
        if int(item["participantId"]) == participant_id
        and item["regionRoleId"] == role_id
    )
    region = next(
        item for item in contract["map"]["regions"]
        if item["regionId"] == assignment["mapRegionId"]
    )
    return {(int(tile[0]), int(tile[1])) for tile in region["tiles"]}


def longest_true_run(values: list[bool]) -> int:
    longest = 0
    current = 0
    for value in values:
        current = current + 1 if value else 0
        longest = max(longest, current)
    return longest


def analyze(cell: Path, entrant_id: str) -> dict[str, Any]:
    record = read_json(cell / "match-record.json")
    replay = read_json(cell / "replay.json.gz")
    broadcast = read_json(cell / "broadcast.json.gz")
    scorecard = read_json(cell / "scorecard.json")
    subject = next(
        item for item in record["participants"]
        if item["entrantId"] == entrant_id
    )
    subject_team = int(subject["teamId"])
    opponent_team = 1 - subject_team
    reactors = {}
    for region in broadcast["header"]["contract"]["map"]["regions"]:
        if region["regionId"] == "reactor-west":
            reactors[0] = tuple(region["tiles"][0])
        elif region["regionId"] == "reactor-east":
            reactors[1] = tuple(region["tiles"][0])
    enemy_reactor = reactors[opponent_team]

    contract = replay["header"]["contract"]
    home_role = contract["modeMapBinding"]["homePadRegionRoleId"]
    own_home = participant_region_tiles(
        contract, int(subject["participantId"]), home_role
    )
    enemy_participant = next(
        item for item in record["participants"]
        if int(item["teamId"]) == opponent_team
    )
    enemy_home = participant_region_tiles(
        contract, int(enemy_participant["participantId"]), home_role
    )

    camp_active: dict[int, bool] = {}
    final_third_body_ticks = 0
    camp_body_ticks = 0
    for world in broadcast["worlds"]:
        tick = int(world[0])
        active = [life for life in world[4] if int(life[0]) == subject_team]
        near = sum(
            distance((int(life[6]), int(life[7])), enemy_reactor) <= 6
            for life in active
        )
        camp_active[tick] = near >= 5
        camp_body_ticks += near
        final_third_body_ticks += sum(
            int(life[6]) >= 20 if subject_team == 0 else int(life[6]) <= 10
            for life in active
        )

    events = [event for tick_events in broadcast["events"] for event in tick_events]
    deaths: list[dict[str, Any]] = []
    kill_distance = {"within2": 0, "within6": 0, "outside6": 0}
    drop_distance = {"within2": 0, "within6": 0, "outside6": 0}
    opponent_drop_distance = {"within2": 0, "within6": 0, "outside6": 0}
    subject_banks = 0
    subject_banks_during_camp = 0
    counter_banks_during_camp = 0
    subject_pickups = 0
    scorer_pickups = 0
    secured_pickups = 0
    secured_scorer_pickups = 0
    secured_banks = 0
    secured_scorer_banks = 0
    last_drop_team_by_core: dict[tuple[str, int], int] = {}
    subject_pickup_by_core: dict[tuple[str, int], tuple[bool, bool]] = {}
    for event in events:
        payload = event.get("payload", {})
        tick = int(event.get("tick", 0))
        if event.get("kind") == "destruction":
            actor = payload.get("actorId", {})
            if int(actor.get("teamId", -1)) == subject_team:
                deaths.append({"tick": tick, "actorId": actor})
            if int(payload.get("sourceTeamId", -1)) == subject_team:
                position = payload.get("position", {})
                bucket_distance(
                    kill_distance,
                    distance(
                        (int(position["x"]), int(position["y"])),
                        enemy_reactor,
                    ),
                )
            continue
        fact = payload.get("fact", {})
        kind = fact.get("kind")
        core = fact.get("coreId", {})
        core_key = (
            str(core.get("sourceWellId", "")),
            int(core.get("sourceOrdinal", -1)),
        )
        if kind == "core-dropped":
            source = fact.get("sourceActorId", {})
            last_drop_team_by_core[core_key] = int(source.get("teamId", -1))
            subject_pickup_by_core.pop(core_key, None)
            if int(source.get("teamId", -1)) == subject_team:
                position = fact["position"]
                bucket_distance(
                    drop_distance,
                    distance(
                        (int(position["x"]), int(position["y"])),
                        enemy_reactor,
                    ),
                )
            elif int(source.get("teamId", -1)) == opponent_team:
                position = fact["position"]
                bucket_distance(
                    opponent_drop_distance,
                    distance(
                        (int(position["x"]), int(position["y"])),
                        enemy_reactor,
                    ),
                )
        elif kind == "core-banked":
            team = int(fact["teamId"])
            if team == subject_team:
                subject_banks += 1
                subject_banks_during_camp += int(camp_active.get(tick, False))
                secured, scorer = subject_pickup_by_core.get(
                    core_key, (False, False)
                )
                secured_banks += int(secured)
                secured_scorer_banks += int(secured and scorer)
            elif camp_active.get(tick, False):
                counter_banks_during_camp += 1
            subject_pickup_by_core.pop(core_key, None)
            last_drop_team_by_core.pop(core_key, None)
        elif kind == "core-picked-up":
            carrier = fact.get("carrierActorId", {})
            if int(carrier.get("teamId", -1)) == subject_team:
                subject_pickups += 1
                role = role_at(broadcast["turns"], tick, carrier)
                scorer = isinstance(role, str) and "scorer" in role
                secured = last_drop_team_by_core.get(core_key) == opponent_team
                scorer_pickups += int(scorer)
                secured_pickups += int(secured)
                secured_scorer_pickups += int(secured and scorer)
                subject_pickup_by_core[core_key] = (secured, scorer)
            last_drop_team_by_core.pop(core_key, None)

    phase_timeline: list[dict[str, Any]] = []
    last_phase: str | None = None
    repair_commands = 0
    repair_medics: set[tuple[int, int]] = set()
    repair_targets: set[tuple[int, int]] = set()
    decisions_by_unit: dict[int, list[tuple[int, int, str | None]]] = {}
    for turn_index, decisions in enumerate(broadcast["turns"]):
        tick = turn_index
        phases: list[str] = []
        for decision in decisions:
            actor = decision[0]
            if int(actor[0]) != subject_team:
                continue
            role = decision[2]
            phase = phase_from_role(role)
            if phase is not None:
                phases.append(phase)
            decisions_by_unit.setdefault(int(actor[1]), []).append(
                (tick, int(actor[2]), role)
            )
            accepted = decision[5]
            if accepted[0] == "repair-beam":
                repair_commands += 1
                repair_medics.add((int(actor[1]), int(actor[2])))
                for argument in accepted[2]:
                    if argument.get("kind") == "unit-target":
                        target = argument["value"]
                        repair_targets.add(
                            (int(target["unitId"]), int(target.get("lifeId", -1)))
                        )
        phase = (
            max(sorted(set(phases)), key=phases.count)
            if phases else last_phase
        )
        if phase is not None and phase != last_phase:
            phase_timeline.append({"tick": tick, "phase": phase})
            last_phase = phase

    respawns_after_death = 0
    respawns_rejoined_strategy = 0
    for death in deaths:
        actor = death["actorId"]
        later = [
            decision for decision in decisions_by_unit.get(int(actor["unitId"]), [])
            if decision[0] > death["tick"] and decision[1] > int(actor["lifeId"])
        ]
        if later:
            respawns_after_death += 1
            respawns_rejoined_strategy += int(any(
                phase_from_role(decision[2]) is not None for decision in later
            ))

    exact_allocation_ticks = 0
    carrier_retarget_ticks = 0
    carrier_release_ticks = 0
    cutoff_active_ticks = 0
    remembered_cutoff_ticks = 0
    cutoff_action_ticks = 0
    emergency_commands = 0
    protected_pad_emergency_commands = 0
    reachable_emergency_commands = 0
    finished_trip_own_home_hold_ticks = 0
    returning_replacement_ticks = 0
    returning_replacement_move_ticks = 0
    replacement_own_home_wait: dict[tuple[int, int], list[bool]] = {}
    task_trace_examples: dict[str, dict[str, Any]] = {}
    subject_participant_id = int(subject["participantId"])
    for tick in replay["ticks"]:
        turn = next(
            item for item in tick["mindTurns"]
            if int(item["participantId"]) == subject_participant_id
        )
        debug = str(turn.get("debugMessage") or "")
        bodies = {
            int(body["actorId"]["unitId"]): body
            for body in turn["observation"]["bodies"]
            if int(body["actorId"]["teamId"]) == subject_team
        }
        commands = {
            int(command["unitId"]): command for command in turn["commands"]
        }
        deny = task_claims(debug, "deny-visible-carrier")
        harvest = task_claims(debug, "harvest-core-window")
        task_units = set(deny) | set(harvest)
        if (
            debug_int(debug, "live") == 8
            and list(deny.values()).count("interceptor") == 1
            and list(harvest.values()).count("courier") == 1
            and list(harvest.values()).count("escort") == 1
            and len(task_units) == 3
        ):
            exact_allocation_ticks += 1
            task_trace_examples.setdefault(
                "fivePlusOnePlusOnePlusOne",
                {"tick": tick["tick"], "debug": debug},
            )
        if "armed-carrier-retargeted" in debug:
            carrier_retarget_ticks += 1
            task_trace_examples.setdefault(
                "carrierRetarget", {"tick": tick["tick"], "debug": debug}
            )
        if "armed-carrier-released" in debug:
            carrier_release_ticks += 1
            task_trace_examples.setdefault(
                "carrierRelease", {"tick": tick["tick"], "debug": debug}
            )

        visible_cores = turn["observation"]["mode"].get("visibleCores", [])
        enemy_carrier_visible = any(
            core.get("disposition") == "carried"
            and int((core.get("carrierActorId") or {}).get("teamId", -1))
                == opponent_team
            for core in visible_cores
        )
        friendly_carrier_visible = any(
            core.get("disposition") == "carried"
            and int((core.get("carrierActorId") or {}).get("teamId", -1))
                == subject_team
            for core in visible_cores
        )
        if deny:
            cutoff_active_ticks += 1
            remembered_cutoff_ticks += int(not enemy_carrier_visible)
            cutoff_action_ticks += int(any(
                commands.get(unit_id, {}).get("actionId") != "wait"
                for unit_id in deny
            ))
            task_trace_examples.setdefault(
                "boundedCutoff", {"tick": tick["tick"], "debug": debug}
            )
        if harvest and not friendly_carrier_visible:
            finished_trip_own_home_hold_ticks += sum(
                int(
                    unit_id in bodies
                    and (
                        int(bodies[unit_id]["position"]["x"]),
                        int(bodies[unit_id]["position"]["y"]),
                    ) in own_home
                )
                for unit_id in harvest
            )

        loose_positions = {
            (int(core["position"]["x"]), int(core["position"]["y"]))
            for core in visible_cores
            if core.get("disposition") == "loose" and core.get("position")
        }
        for command in turn["commands"]:
            command_debug = str(command.get("debugMessage") or "")
            if "custody:emergency-pickup" not in command_debug:
                continue
            emergency_commands += 1
            if any(position in enemy_home for position in loose_positions):
                protected_pad_emergency_commands += 1
            else:
                reachable_emergency_commands += 1
            task_trace_examples.setdefault(
                "emergencyCore",
                {
                    "tick": tick["tick"],
                    "command": command,
                    "visibleLooseCorePositions": sorted(loose_positions),
                },
            )

        returning = returning_units(debug)
        for unit_id in returning:
            body = bodies.get(unit_id)
            if body is None or int(body.get("lifeStartedTick", 0)) == 0:
                continue
            actor = body["actorId"]
            life = (unit_id, int(actor["lifeId"]))
            command = commands.get(unit_id, {})
            is_wait = command.get("actionId") == "wait"
            position = (int(body["position"]["x"]), int(body["position"]["y"]))
            returning_replacement_ticks += 1
            returning_replacement_move_ticks += int(not is_wait)
            replacement_own_home_wait.setdefault(life, []).append(
                is_wait and position in own_home
            )
            task_trace_examples.setdefault(
                "replacementCatchUp",
                {
                    "tick": tick["tick"],
                    "actorId": actor,
                    "position": body["position"],
                    "actionId": command.get("actionId"),
                },
            )

    phases_seen = [item["phase"] for item in phase_timeline]
    eligibility = scorecard["feltDegeneracy"]
    deliveries = scorecard["scoring"]["deliveriesByTeam"]
    final_reactors = {
        int(reactor["teamId"]): int(reactor["integritySegments"])
        for reactor in broadcast["worlds"][-1][7]["reactors"]
    }
    return {
        "cellId": record["matchId"],
        "seed": record["seed"],
        "subjectTeamId": subject_team,
        "winnerTeamId": record["result"]["winnerTeamId"],
        "completionReason": record["result"]["reason"],
        "endTick": record["result"]["endTick"],
        "deliveries": {
            "subject": int(deliveries[str(subject_team)]),
            "opponent": int(deliveries[str(opponent_team)]),
        },
        "finalIntegrity": {
            "subject": final_reactors[subject_team],
            "opponent": final_reactors[opponent_team],
        },
        "canonicalReplaySha256": record["canonicalReplay"]["hash"],
        "phaseTimeline": phase_timeline,
        "recoveryCycleObserved": contains_subsequence(
            phases_seen, ["occupy", "regroup", "breach"]
        ),
        "casualtyRespawn": {
            "subjectDeaths": len(deaths),
            "deathsWithLaterRespawn": respawns_after_death,
            "respawnsRejoiningStrategy": respawns_rejoined_strategy,
        },
        "strictCorrections": {
            "fivePlusOnePlusOnePlusOneTicks": exact_allocation_ticks,
            "carrierLifecycle": {
                "retargetTicks": carrier_retarget_ticks,
                "releaseTicks": carrier_release_ticks,
                "finishedTripOwnHomeHoldTicks":
                    finished_trip_own_home_hold_ticks,
            },
            "boundedCutoff": {
                "activeTicks": cutoff_active_ticks,
                "lastSeenOnlyTicks": remembered_cutoff_ticks,
                "actionTicks": cutoff_action_ticks,
            },
            "emergencyCore": {
                "commands": emergency_commands,
                "reachableCommands": reachable_emergency_commands,
                "protectedEnemyPadCommands": protected_pad_emergency_commands,
            },
            "replacementCatchUp": {
                "returningReplacementTicks": returning_replacement_ticks,
                "movementActionTicks": returning_replacement_move_ticks,
                "maximumConsecutiveOwnHomeWaitTicks": max(
                    (
                        longest_true_run(values)
                        for values in replacement_own_home_wait.values()
                    ),
                    default=0,
                ),
            },
            "traceExamples": task_trace_examples,
        },
        "repair": {
            "acceptedRepairCommands": repair_commands,
            "distinctMedicLives": len(repair_medics),
            "distinctTargetSlots": len({item[0] for item in repair_targets}),
        },
        "coreConversion": {
            "subjectPickups": subject_pickups,
            "declaredScorerPickups": scorer_pickups,
            "opportunisticPickups": subject_pickups - scorer_pickups,
            "securedCorePickups": secured_pickups,
            "securedCoreScorerPickups": secured_scorer_pickups,
            "subjectBanks": subject_banks,
            "securedCoreBanks": secured_banks,
            "securedCoreScorerBanks": secured_scorer_banks,
            "subjectBanksDuringFiveBodyCamp": subject_banks_during_camp,
            "counterBanksDuringFiveBodyCamp": counter_banks_during_camp,
        },
        "campingDiagnostics": {
            "fiveBodyCampTicks": sum(camp_active.values()),
            "enemyFinalThirdBodyTicks": final_third_body_ticks,
            "enemyReactorRadiusSixBodyTicks": camp_body_ticks,
            "killsByDistanceFromEnemyReactor": kill_distance,
            "subjectCoreDropsByDistanceFromEnemyReactor": drop_distance,
            "opponentCoreDropsByDistanceFromEnemyReactor":
                opponent_drop_distance,
            "campToDeliveryShare": (
                subject_banks_during_camp / subject_banks
                if subject_banks else 0.0
            ),
        },
        "eligibility": {
            "matchEligibleForCohortRead": eligibility[
                "matchEligibleForCohortRead"
            ],
            "subjectFormationFreezeTripped": eligibility["formationFreeze"]
                ["barTrippedByTeam"][str(subject_team)],
            "subjectSustainedPassivityTripped": eligibility[
                "sustainedPassivity"
            ]["barTrippedByTeam"][str(subject_team)],
            "subjectHandoffPingPongTripped": eligibility["handoffPingPong"]
                ["barTrippedByTeam"][str(subject_team)],
            "subjectStuckCarrierTripped": eligibility["stuckCarrier"]
                ["barTrippedByTeam"][str(subject_team)],
            "subjectHomeCarrierNonProgressTripped": eligibility[
                "homeCarrierNonProgress"
            ]["barTrippedByTeam"][str(subject_team)],
            "subjectPickupDropCycleTripped": eligibility["pickupDropCycle"]
                ["barTrippedByTeam"][str(subject_team)],
        },
    }


def role_at(turns: list[Any], tick: int, actor: dict[str, Any]) -> str | None:
    index = min(max(tick, 0), len(turns) - 1)
    for decision in turns[index]:
        identity = decision[0]
        if (
            int(identity[0]) == int(actor["teamId"])
            and int(identity[1]) == int(actor["unitId"])
            and int(identity[2]) == int(actor["lifeId"])
        ):
            return decision[2]
    return None


def bucket_distance(buckets: dict[str, int], value: int) -> None:
    if value <= 2:
        buckets["within2"] += 1
    elif value <= 6:
        buckets["within6"] += 1
    else:
        buckets["outside6"] += 1


def contains_subsequence(values: list[str], wanted: list[str]) -> bool:
    index = 0
    for value in values:
        if value == wanted[index]:
            index += 1
            if index == len(wanted):
                return True
    return False


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sweep-attempt", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--entrant-id", default="home-siege")
    args = parser.parse_args()
    cell_attempts = sorted(
        args.sweep_attempt.glob("cells/*/attempt-*/match-record.json")
    )
    if not cell_attempts:
        raise FileNotFoundError("no final sweep cells found")
    cells = [analyze(path.parent, args.entrant_id) for path in cell_attempts]
    result = {
        "schema": "arc-relay-home-siege-audit-v2",
        "sourceSweep": str(args.sweep_attempt),
        "cells": cells,
        "summary": {
            "cells": len(cells),
            "homeSiegeWins": sum(
                cell["winnerTeamId"] == cell["subjectTeamId"] for cell in cells
            ),
            "allEligible": all(
                cell["eligibility"]["matchEligibleForCohortRead"]
                for cell in cells
            ),
            "cellsWithRecoveryCycle": sum(
                cell["recoveryCycleObserved"] for cell in cells
            ),
            "subjectBanks": sum(
                cell["coreConversion"]["subjectBanks"] for cell in cells
            ),
            "securedCoreBanks": sum(
                cell["coreConversion"]["securedCoreBanks"] for cell in cells
            ),
            "securedCoreScorerBanks": sum(
                cell["coreConversion"]["securedCoreScorerBanks"]
                for cell in cells
            ),
            "counterBanksDuringCamp": sum(
                cell["coreConversion"]["counterBanksDuringFiveBodyCamp"]
                for cell in cells
            ),
            "enemyFinalThirdBodyTicks": sum(
                cell["campingDiagnostics"]["enemyFinalThirdBodyTicks"]
                for cell in cells
            ),
            "acceptedRepairCommands": sum(
                cell["repair"]["acceptedRepairCommands"] for cell in cells
            ),
            "deathsWithLaterRespawn": sum(
                cell["casualtyRespawn"]["deathsWithLaterRespawn"]
                for cell in cells
            ),
            "respawnsRejoiningStrategy": sum(
                cell["casualtyRespawn"]["respawnsRejoiningStrategy"]
                for cell in cells
            ),
            "fivePlusOnePlusOnePlusOneTicks": sum(
                cell["strictCorrections"]["fivePlusOnePlusOnePlusOneTicks"]
                for cell in cells
            ),
            "carrierRetargetTicks": sum(
                cell["strictCorrections"]["carrierLifecycle"]["retargetTicks"]
                for cell in cells
            ),
            "carrierReleaseTicks": sum(
                cell["strictCorrections"]["carrierLifecycle"]["releaseTicks"]
                for cell in cells
            ),
            "finishedTripOwnHomeHoldTicks": sum(
                cell["strictCorrections"]["carrierLifecycle"]
                    ["finishedTripOwnHomeHoldTicks"]
                for cell in cells
            ),
            "emergencyCoreCommands": sum(
                cell["strictCorrections"]["emergencyCore"]["commands"]
                for cell in cells
            ),
            "protectedEnemyPadEmergencyCommands": sum(
                cell["strictCorrections"]["emergencyCore"]
                    ["protectedEnemyPadCommands"]
                for cell in cells
            ),
            "boundedCutoffTicks": sum(
                cell["strictCorrections"]["boundedCutoff"]["activeTicks"]
                for cell in cells
            ),
            "lastSeenCutoffTicks": sum(
                cell["strictCorrections"]["boundedCutoff"]["lastSeenOnlyTicks"]
                for cell in cells
            ),
            "maximumConsecutiveReplacementOwnHomeWaitTicks": max(
                cell["strictCorrections"]["replacementCatchUp"]
                    ["maximumConsecutiveOwnHomeWaitTicks"]
                for cell in cells
            ),
        },
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(result, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(result["summary"], sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
