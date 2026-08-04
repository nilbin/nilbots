#!/usr/bin/env python3
"""Audit Home Siege causality and registered camping diagnostics."""

from __future__ import annotations

import argparse
import gzip
import json
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


def analyze(cell: Path, entrant_id: str) -> dict[str, Any]:
    record = read_json(cell / "match-record.json")
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

    phases_seen = [item["phase"] for item in phase_timeline]
    eligibility = scorecard["feltDegeneracy"]
    return {
        "cellId": record["matchId"],
        "seed": record["seed"],
        "subjectTeamId": subject_team,
        "winnerTeamId": record["result"]["winnerTeamId"],
        "completionReason": record["result"]["reason"],
        "endTick": record["result"]["endTick"],
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
        "schema": "arc-relay-home-siege-audit-v1",
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
