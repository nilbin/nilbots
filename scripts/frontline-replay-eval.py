#!/usr/bin/env python3
"""Descriptive replay-v2 metrics for the experimental Frontline game.

This analyzer deliberately does not emit a composite "fun" score or promotion
thresholds. It reports authoritative duration, progression, replication,
Anchor/turret, combat, and inactivity dimensions. Causal claims still require
paired frozen rules arms; product claims still require independently authored
native doctrines and outcome-blind replay review.
"""

from __future__ import annotations

import argparse
import copy
import json
import math
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable


REPORT_SCHEMA_VERSION = 1
METRIC_DEFINITIONS_VERSION = "frontline-replay-v2-1"
PRESENTATION_TICKS_PER_SECOND = 5


def _object(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError(f"{label} must be an object")
    return value


def _array(value: Any, label: str) -> list[Any]:
    if not isinstance(value, list):
        raise ValueError(f"{label} must be an array")
    return value


def _integer(value: Any, label: str) -> int:
    if not isinstance(value, int) or isinstance(value, bool):
        raise ValueError(f"{label} must be an integer")
    return value


def _actor_key(actor: dict[str, Any]) -> tuple[int, int, int]:
    return (
        _integer(actor.get("teamId"), "actor.teamId"),
        _integer(actor.get("unitId"), "actor.unitId"),
        _integer(actor.get("lifeId"), "actor.lifeId"),
    )


def _run_lengths(flags: Iterable[bool]) -> list[int]:
    runs: list[int] = []
    current = 0
    for flag in flags:
        if flag:
            current += 1
        elif current:
            runs.append(current)
            current = 0
    if current:
        runs.append(current)
    return runs


def _nearest_rank(values: list[int], percentile: float) -> int | None:
    if not values:
        return None
    ordered = sorted(values)
    index = max(0, math.ceil(percentile * len(ordered)) - 1)
    return ordered[index]


def _run_summary(runs: list[int]) -> dict[str, Any]:
    histogram = Counter(runs)
    return {
        "count": len(runs),
        "totalTicks": sum(runs),
        "medianTicks": statistics.median(runs) if runs else None,
        "p90Ticks": _nearest_rank(runs, 0.90),
        "maxTicks": max(runs, default=0),
        "histogram": {
            str(length): histogram[length]
            for length in sorted(histogram)
        },
    }


def _state_without_clock(state: dict[str, Any]) -> dict[str, Any]:
    normalized = copy.deepcopy(state)
    objective = _object(
        normalized.get("objective"),
        "worldState.objective",
    )
    objective.pop("nextTick", None)
    return normalized


def _phase_name(index: int, unlock_count: int) -> str:
    if unlock_count == 2:
        return ("early", "mid", "late")[index]
    return f"phase-{index}"


def analyze_replay(
    document: dict[str, Any],
    *,
    source: str = "",
    group: str = "",
) -> dict[str, Any]:
    """Validate and derive one authoritative per-match metric row."""

    header = _object(document.get("header"), "header")
    if header.get("replayVersion") != 2:
        raise ValueError("header.replayVersion must be 2")
    if document.get("partial") is not False:
        raise ValueError("partial must be false")
    result = _object(document.get("result"), "result")
    ticks = _array(document.get("ticks"), "ticks")
    end_tick = _integer(result.get("endTick"), "result.endTick")
    if end_tick < 0:
        raise ValueError("result.endTick must be non-negative")
    expected_ticks = list(range(end_tick + 1))
    actual_ticks = [
        _integer(_object(tick, "tick").get("tick"), "tick.tick")
        for tick in ticks
    ]
    if actual_ticks != expected_ticks:
        raise ValueError(
            "ticks must be contiguous 0..result.endTick "
            f"(got {actual_ticks[:3]}...{actual_ticks[-3:]})"
        )

    contract = _object(header.get("contract"), "header.contract")
    rules = _object(contract.get("rules"), "header.contract.rules")
    map_contract = _object(
        contract.get("map"),
        "header.contract.map",
    )
    topology = _object(
        contract.get("topology"),
        "header.contract.topology",
    )
    frontline = _object(
        rules.get("frontlineDefinition"),
        "rules.frontlineDefinition",
    )
    lifecycle = _object(
        frontline.get("lifecycle"),
        "frontlineDefinition.lifecycle",
    )
    capture = _object(
        frontline.get("capture"),
        "frontlineDefinition.capture",
    )
    victory = _object(
        frontline.get("victory"),
        "frontlineDefinition.victory",
    )
    fabrication = _object(
        frontline.get("fabrication"),
        "frontlineDefinition.fabrication",
    )
    anchor = _object(
        frontline.get("anchor"),
        "frontlineDefinition.anchor",
    )
    turret_fire = _object(
        frontline.get("turretFire"),
        "frontlineDefinition.turretFire",
    )

    unlock_ticks = [
        _integer(value, "fabricationUnlockTicks[]")
        for value in _array(
            lifecycle.get("fabricationUnlockTicks"),
            "fabricationUnlockTicks",
        )
    ]
    phase_index = sum(unlock <= end_tick for unlock in unlock_ticks)
    phase = _phase_name(phase_index, len(unlock_ticks))

    topology_teams = sorted(
        _integer(
            _object(team, "topology.teams[]").get("teamId"),
            "topology teamId",
        )
        for team in _array(topology.get("teams"), "topology.teams")
    )
    if not topology_teams:
        raise ValueError("topology.teams cannot be empty")

    participants = []
    for raw in sorted(
        _array(header.get("participants"), "header.participants"),
        key=lambda item: _object(
            item,
            "participant",
        ).get("participantId", -1),
    ):
        participant = _object(raw, "participant")
        participants.append(
            {
                "participantId": participant.get("participantId"),
                "teamId": participant.get("teamId"),
                "name": participant.get("name"),
                "runtimeKind": participant.get("runtimeKind"),
                "artifactHash": participant.get("artifactHash"),
            }
        )

    fabricator_unit = _integer(
        fabrication.get("fabricatorUnitId"),
        "fabrication.fabricatorUnitId",
    )
    action_fabricate = fabrication.get("actionId")
    action_anchor = anchor.get("actionId")
    action_turret = turret_fire.get("actionId")
    turret_form = turret_fire.get("formId")
    anchor_target_form = anchor.get("targetFormId")

    unit_slots_by_team: dict[int, list[int]] = defaultdict(list)
    for raw in _array(topology.get("unitSlots"), "topology.unitSlots"):
        slot = _object(raw, "topology.unitSlot")
        unit_slots_by_team[_integer(slot.get("teamId"), "slot.teamId")].append(
            _integer(slot.get("unitId"), "slot.unitId")
        )
    unlock_by_unit: dict[tuple[int, int], int] = {}
    for team_id, unit_ids in unit_slots_by_team.items():
        children = sorted(
            unit_id
            for unit_id in unit_ids
            if unit_id != fabricator_unit
        )
        if len(children) != len(unlock_ticks):
            raise ValueError(
                f"team {team_id} child slots do not match unlock count"
            )
        for unit_id, unlock_tick in zip(children, unlock_ticks):
            unlock_by_unit[(team_id, unit_id)] = unlock_tick

    per_team: dict[int, dict[str, Any]] = {
        team_id: {
            "teamId": team_id,
            "fabricationOpportunities": 0,
            "fabricationAttempts": 0,
            "fabricationQueues": 0,
            "fabricatedBirths": 0,
            "fabricationQueueLatencyTicks": [],
            "childActorTicks": 0,
            "peakActiveBodies": 0,
            "anchorAttempts": 0,
            "anchorStarts": 0,
            "anchorCompletions": 0,
            "anchorCancellations": 0,
            "turretActorTicks": 0,
            "turretShots": 0,
            "turretDamageEvents": 0,
            "turretDamage": 0,
            "turretKills": 0,
            "actorlessTicks": 0,
            "validatedActions": Counter(),
            "actionSwitches": 0,
        }
        for team_id in topology_teams
    }

    lifecycle_events: list[dict[str, Any]] = []
    resolution_events: list[dict[str, Any]] = []
    previous_action: dict[tuple[int, int, int], str] = {}
    actorless_flags: dict[int, list[bool]] = {
        team_id: [] for team_id in topology_teams
    }
    both_actorless_flags: list[bool] = []
    stagnant_flags: list[bool] = []

    for tick_raw in ticks:
        tick = _object(tick_raw, "tick")
        tick_start = _object(tick.get("tickStart"), "tick.tickStart")
        resolution = _object(tick.get("resolution"), "tick.resolution")
        start_state = _object(
            tick_start.get("state"),
            "tick.tickStart.state",
        )
        post_state = _object(tick.get("postState"), "tick.postState")
        tick_lifecycle = [
            _object(event, "lifecycleEvent")
            for event in _array(
                tick_start.get("lifecycleEvents"),
                "tickStart.lifecycleEvents",
            )
        ]
        tick_resolution = [
            _object(event, "resolutionEvent")
            for event in _array(
                resolution.get("events"),
                "resolution.events",
            )
        ]
        lifecycle_events.extend(tick_lifecycle)
        resolution_events.extend(tick_resolution)

        active_actors = _array(
            tick_start.get("activeActors"),
            "tickStart.activeActors",
        )
        both_actorless_flags.append(len(active_actors) == 0)

        active_by_team: dict[int, int] = {
            team_id: 0 for team_id in topology_teams
        }
        for raw_team in _array(
            start_state.get("teams"),
            "tickStart.state.teams",
        ):
            state_team = _object(raw_team, "state.team")
            team_id = _integer(state_team.get("teamId"), "state.teamId")
            active_by_team[team_id] = sum(
                _object(unit, "state.unit").get("activeLife") is not None
                for unit in _array(state_team.get("units"), "state.units")
            )
        for team_id in topology_teams:
            active = active_by_team.get(team_id, 0)
            actorless = active == 0
            actorless_flags[team_id].append(actorless)
            if actorless:
                per_team[team_id]["actorlessTicks"] += 1
            per_team[team_id]["peakActiveBodies"] = max(
                per_team[team_id]["peakActiveBodies"],
                active,
            )

        traversals = _array(
            resolution.get("projectileTraversals"),
            "resolution.projectileTraversals",
        )
        stagnant_flags.append(
            not tick_lifecycle
            and not traversals
            and _state_without_clock(start_state)
            == _state_without_clock(post_state)
        )

        for raw_turn in _array(tick.get("actors"), "tick.actors"):
            turn = _object(raw_turn, "actorTurn")
            actor_id = _object(turn.get("actorId"), "actorTurn.actorId")
            team_id, unit_id, _ = _actor_key(actor_id)
            observation = _object(
                turn.get("observation"),
                "actorTurn.observation",
            )
            resolution_row = _object(
                turn.get("actionResolution"),
                "actorTurn.actionResolution",
            )
            chosen = resolution_row.get("chosenActionId")
            validated = resolution_row.get("validatedActionId")
            if not isinstance(chosen, str) or not isinstance(validated, str):
                raise ValueError("action resolution IDs must be strings")

            if chosen == action_fabricate:
                per_team[team_id]["fabricationAttempts"] += 1
            if chosen == action_anchor:
                per_team[team_id]["anchorAttempts"] += 1
            per_team[team_id]["validatedActions"][validated] += 1

            identity = _actor_key(actor_id)
            if identity in previous_action and previous_action[identity] != validated:
                per_team[team_id]["actionSwitches"] += 1
            previous_action[identity] = validated

            if unit_id != fabricator_unit:
                per_team[team_id]["childActorTicks"] += 1
            self_observation = _object(
                observation.get("self"),
                "observation.self",
            )
            if self_observation.get("formId") == turret_form:
                per_team[team_id]["turretActorTicks"] += 1

            for raw_action in _array(
                observation.get("actions"),
                "observation.actions",
            ):
                observed_action = _object(raw_action, "observedAction")
                if (
                    observed_action.get("actionId") == action_fabricate
                    and observed_action.get("available") is True
                ):
                    per_team[team_id]["fabricationOpportunities"] += 1

    queue_events = [
        event
        for event in resolution_events
        if event.get("type") == "fabrication-queued"
    ]
    birth_events = [
        event
        for event in lifecycle_events
        if event.get("type") == "fabricated"
    ]
    for event in queue_events:
        team_id = _integer(event.get("teamId"), "fabrication teamId")
        unit_id = _integer(event.get("unitId"), "fabrication unitId")
        per_team[team_id]["fabricationQueues"] += 1
        unlock_tick = unlock_by_unit.get((team_id, unit_id))
        if (
            event.get("spawnReason") == "fabrication"
            and unlock_tick is not None
        ):
            per_team[team_id]["fabricationQueueLatencyTicks"].append(
                _integer(event.get("tick"), "fabrication tick")
                - unlock_tick
            )
    for event in birth_events:
        team_id = _integer(event.get("teamId"), "fabricated teamId")
        per_team[team_id]["fabricatedBirths"] += 1

    for event in resolution_events:
        event_type = event.get("type")
        team_id = event.get("teamId")
        if team_id not in per_team:
            continue
        if event_type == "form-transition-started":
            per_team[team_id]["anchorStarts"] += 1
        elif (
            event_type == "form-changed"
            and event.get("toFormId") == anchor_target_form
        ):
            per_team[team_id]["anchorCompletions"] += 1
        elif event_type == "form-transition-cancelled":
            per_team[team_id]["anchorCancellations"] += 1

    turret_projectiles: set[str] = set()
    for event in resolution_events:
        if event.get("type") != "shot" or event.get("actionId") != action_turret:
            continue
        source_actor = _object(
            event.get("sourceActorId"),
            "turret shot sourceActorId",
        )
        team_id = _integer(source_actor.get("teamId"), "turret teamId")
        projectile_id = event.get("projectileId")
        if not isinstance(projectile_id, str):
            raise ValueError("turret shot projectileId must be a string")
        turret_projectiles.add(projectile_id)
        per_team[team_id]["turretShots"] += 1

    shots = 0
    damage_events = 0
    damage_amount = 0
    damage_ticks: set[int] = set()
    damage_source_teams: set[int] = set()
    for event in resolution_events:
        event_type = event.get("type")
        if event_type == "shot":
            shots += 1
        elif event_type == "damage":
            damage_events += 1
            amount = _integer(event.get("amount"), "damage amount")
            damage_amount += amount
            damage_ticks.add(_integer(event.get("tick"), "damage tick"))
            source_actor = _object(
                event.get("sourceActorId"),
                "damage sourceActorId",
            )
            source_team = _integer(
                source_actor.get("teamId"),
                "damage source teamId",
            )
            damage_source_teams.add(source_team)
            if event.get("projectileId") in turret_projectiles:
                per_team[source_team]["turretDamageEvents"] += 1
                per_team[source_team]["turretDamage"] += amount
        elif (
            event_type == "destroyed"
            and event.get("projectileId") in turret_projectiles
        ):
            source_actor = _object(
                event.get("sourceActorId"),
                "destroyed sourceActorId",
            )
            source_team = _integer(
                source_actor.get("teamId"),
                "destroyed source teamId",
            )
            per_team[source_team]["turretKills"] += 1

    push_events = [
        event
        for event in resolution_events
        if event.get("type") == "frontline-position-advanced"
    ]
    push_deltas = [
        _integer(event.get("toPositionIndex"), "push.toPositionIndex")
        - _integer(event.get("fromPositionIndex"), "push.fromPositionIndex")
        for event in push_events
    ]
    push_reversals = sum(
        left * right < 0
        for left, right in zip(push_deltas, push_deltas[1:])
    )

    threshold = _integer(capture.get("threshold"), "capture.threshold")
    position_count = _integer(
        frontline.get("frontlinePositionCount"),
        "frontlinePositionCount",
    )
    centre = position_count // 2
    advance_by_team = {
        _integer(
            _object(raw, "victory.teamAdvances[]").get("teamId"),
            "advance.teamId",
        ): _integer(
            _object(raw, "victory.teamAdvances[]").get(
                "positionIndexDelta"
            ),
            "advance.positionIndexDelta",
        )
        for raw in _array(
            victory.get("teamAdvances"),
            "victory.teamAdvances",
        )
    }
    territorial_scores: list[int] = []
    for tick in ticks:
        objective = _object(
            _object(tick, "tick").get("postState"),
            "postState",
        )
        objective = _object(objective.get("objective"), "postState.objective")
        score = (
            _integer(
                objective.get("activePositionIndex"),
                "objective.activePositionIndex",
            )
            - centre
        ) * threshold
        claimant = objective.get("claimingTeamId")
        if claimant is not None:
            score += advance_by_team[_integer(claimant, "claimingTeamId")] * (
                _integer(
                    objective.get("captureProgress"),
                    "objective.captureProgress",
                )
            )
        territorial_scores.append(score)

    nonzero_signs = [
        1 if score > 0 else -1
        for score in territorial_scores
        if score != 0
    ]
    lead_changes = sum(
        left != right
        for left, right in zip(nonzero_signs, nonzero_signs[1:])
    )
    winner_team = result.get("winnerTeamId")
    maximum_winner_deficit = 0
    if winner_team is not None:
        winner = _integer(winner_team, "result.winnerTeamId")
        winner_advance = advance_by_team[winner]
        maximum_winner_deficit = max(
            [0]
            + [
                -winner_advance * score
                for score in territorial_scores
                if winner_advance * score < 0
            ]
        )

    for team_id in topology_teams:
        per_team[team_id]["actorlessRuns"] = _run_summary(
            _run_lengths(actorless_flags[team_id])
        )
        per_team[team_id]["actorlessShare"] = (
            per_team[team_id]["actorlessTicks"] / len(ticks)
        )
        latencies = per_team[team_id]["fabricationQueueLatencyTicks"]
        per_team[team_id]["firstFabricationQueueLatencyTicks"] = (
            min(latencies) if latencies else None
        )
        action_counts: Counter[str] = per_team[team_id]["validatedActions"]
        per_team[team_id]["validatedActions"] = dict(
            sorted(action_counts.items())
        )

    active_counts = [
        len(
            _array(
                _object(
                    _object(tick, "tick").get("tickStart"),
                    "tickStart",
                ).get("activeActors"),
                "activeActors",
            )
        )
        for tick in ticks
    ]
    validated_actions = Counter()
    action_switches = 0
    for team in per_team.values():
        validated_actions.update(team["validatedActions"])
        action_switches += team["actionSwitches"]

    both_actorless_runs = _run_summary(
        _run_lengths(both_actorless_flags)
    )
    stagnant_runs = _run_summary(_run_lengths(stagnant_flags))
    duration_ticks = len(ticks)
    team_rows = [per_team[team_id] for team_id in topology_teams]
    anchor_completions = sum(
        team["anchorCompletions"] for team in team_rows
    )
    turret_actor_ticks = sum(
        team["turretActorTicks"] for team in team_rows
    )
    turret_damage = sum(team["turretDamage"] for team in team_rows)
    turret_kills = sum(team["turretKills"] for team in team_rows)
    fabrication_latencies = [
        latency
        for team in team_rows
        for latency in team["fabricationQueueLatencyTicks"]
    ]
    team_actorless_ticks = sum(
        team["actorlessTicks"] for team in team_rows
    )
    team_actorless_runs = [
        run
        for flags in actorless_flags.values()
        for run in _run_lengths(flags)
    ]

    return {
        "group": group,
        "source": source,
        "rulesVersion": header.get("gameRulesVersion"),
        "mapId": map_contract.get("mapId"),
        "seed": header.get("seed"),
        "replayHash": document.get("replayHash"),
        "rulesFingerprint": rules.get("rulesFingerprint"),
        "mapFingerprint": map_contract.get("mapFingerprint"),
        "matchContractFingerprint": contract.get(
            "matchContractFingerprint"
        ),
        "participants": participants,
        "winnerTeamId": winner_team,
        "reason": result.get("reason"),
        "territorialScore": result.get("territorialScore"),
        "endTick": end_tick,
        "durationTicks": duration_ticks,
        "durationSeconds": (
            duration_ticks / PRESENTATION_TICKS_PER_SECOND
        ),
        "fabricationUnlockTicks": unlock_ticks,
        "endingPhaseIndex": phase_index,
        "endingPhase": phase,
        "pushes": len(push_events),
        "pushDirectionReversals": push_reversals,
        "territorialLeadChanges": lead_changes,
        "comebackWin": maximum_winner_deficit > 0,
        "fullPositionComeback": maximum_winner_deficit >= threshold,
        "maximumWinnerDeficitCapturePoints": maximum_winner_deficit,
        "maximumWinnerDeficitPositions": (
            maximum_winner_deficit / threshold
        ),
        "fabricationOpportunities": sum(
            team["fabricationOpportunities"] for team in team_rows
        ),
        "fabricationAttempts": sum(
            team["fabricationAttempts"] for team in team_rows
        ),
        "fabricationQueues": len(queue_events),
        "fabricatedBirths": len(birth_events),
        "teamsUsingFabrication": sum(
            team["fabricationQueues"] > 0 for team in team_rows
        ),
        "fabricationQueueLatencyTicks": fabrication_latencies,
        "firstFabricationQueueLatencyTicks": (
            min(fabrication_latencies)
            if fabrication_latencies
            else None
        ),
        "childActorTicks": sum(
            team["childActorTicks"] for team in team_rows
        ),
        "peakSimultaneousActiveBodies": max(
            active_counts,
            default=0,
        ),
        "anchorAttempts": sum(
            team["anchorAttempts"] for team in team_rows
        ),
        "anchorStarts": sum(team["anchorStarts"] for team in team_rows),
        "anchorCompletions": anchor_completions,
        "anchorCancellations": sum(
            team["anchorCancellations"] for team in team_rows
        ),
        "turretActorTicks": turret_actor_ticks,
        "turretShots": sum(team["turretShots"] for team in team_rows),
        "turretDamageEvents": sum(
            team["turretDamageEvents"] for team in team_rows
        ),
        "turretDamage": turret_damage,
        "turretKills": turret_kills,
        "turretDamagePer100ActorTicks": (
            100.0 * turret_damage / turret_actor_ticks
            if turret_actor_ticks
            else None
        ),
        "turretKillsPerCompletedAnchor": (
            turret_kills / anchor_completions
            if anchor_completions
            else None
        ),
        "shots": shots,
        "damageEvents": damage_events,
        "damageAmount": damage_amount,
        "damageTicks": len(damage_ticks),
        "reciprocalDamage": len(damage_source_teams) > 1,
        "multiDamageTickGame": len(damage_ticks) > 1,
        "validatedActions": dict(sorted(validated_actions.items())),
        "actionSwitches": action_switches,
        "teamActorlessTicks": team_actorless_ticks,
        "teamActorlessShare": (
            team_actorless_ticks / (duration_ticks * len(team_rows))
        ),
        "teamActorlessRuns": _run_summary(team_actorless_runs),
        "bothTeamsActorlessTicks": sum(both_actorless_flags),
        "bothTeamsActorlessRuns": both_actorless_runs,
        "stagnantTicks": sum(stagnant_flags),
        "stagnantRuns": stagnant_runs,
        "teams": team_rows,
    }


def _sum(rows: list[dict[str, Any]], key: str) -> int:
    return sum(int(row[key]) for row in rows)


def summarize_group(
    name: str,
    rows: list[dict[str, Any]],
) -> dict[str, Any]:
    durations = [int(row["durationTicks"]) for row in rows]
    rules_versions = sorted(
        {str(row["rulesVersion"]) for row in rows}
    )
    rules_fingerprints = sorted(
        {str(row["rulesFingerprint"]) for row in rows}
    )
    if len(rules_versions) != 1 or len(rules_fingerprints) != 1:
        raise ValueError(
            f"group '{name}' mixes rules cohorts: "
            f"versions={rules_versions}, "
            f"fingerprints={rules_fingerprints}"
        )
    map_ids = sorted({str(row["mapId"]) for row in rows})
    map_fingerprints = sorted(
        {str(row["mapFingerprint"]) for row in rows}
    )
    match_fingerprints = sorted(
        {str(row["matchContractFingerprint"]) for row in rows}
    )
    runtime_kinds = sorted(
        {
            str(participant["runtimeKind"])
            for row in rows
            for participant in row["participants"]
        }
    )
    participant_artifacts = sorted(
        {
            (
                str(participant["name"]),
                str(participant["runtimeKind"]),
                str(participant["artifactHash"]),
            )
            for row in rows
            for participant in row["participants"]
        }
    )
    total_ticks = sum(durations)
    actions = Counter()
    for row in rows:
        actions.update(row["validatedActions"])
    reasons = Counter(str(row["reason"]) for row in rows)
    endings = Counter(
        f"{row['endingPhase']}:{row['reason']}" for row in rows
    )
    winners = Counter(
        "draw"
        if row["winnerTeamId"] is None
        else f"team-{row['winnerTeamId']}"
        for row in rows
    )
    turret_actor_ticks = _sum(rows, "turretActorTicks")
    turret_damage = _sum(rows, "turretDamage")
    anchor_completions = _sum(rows, "anchorCompletions")
    turret_kills = _sum(rows, "turretKills")
    team_actor_denominator = sum(
        int(row["durationTicks"]) * len(row["teams"])
        for row in rows
    )
    fabrication_latencies = [
        latency
        for row in rows
        for latency in row["fabricationQueueLatencyTicks"]
    ]
    return {
        "name": name,
        "matches": len(rows),
        "cohort": {
            "rulesVersion": rules_versions[0],
            "rulesFingerprint": rules_fingerprints[0],
            "mapIds": map_ids,
            "mapFingerprints": map_fingerprints,
            "matchContractFingerprints": match_fingerprints,
            "runtimeKinds": runtime_kinds,
            "participantArtifacts": [
                {
                    "name": participant[0],
                    "runtimeKind": participant[1],
                    "artifactHash": participant[2],
                }
                for participant in participant_artifacts
            ],
        },
        "outcomes": dict(sorted(winners.items())),
        "reasons": dict(sorted(reasons.items())),
        "endingPhaseAndReason": dict(sorted(endings.items())),
        "duration": {
            "minTicks": min(durations),
            "medianTicks": statistics.median(durations),
            "p90Ticks": _nearest_rank(durations, 0.90),
            "maxTicks": max(durations),
            "minSeconds": (
                min(durations) / PRESENTATION_TICKS_PER_SECOND
            ),
            "medianSeconds": (
                statistics.median(durations)
                / PRESENTATION_TICKS_PER_SECOND
            ),
            "p90Seconds": (
                _nearest_rank(durations, 0.90)
                / PRESENTATION_TICKS_PER_SECOND
            ),
            "maxSeconds": (
                max(durations) / PRESENTATION_TICKS_PER_SECOND
            ),
        },
        "progression": {
            "pushes": _sum(rows, "pushes"),
            "pushDirectionReversals": _sum(
                rows,
                "pushDirectionReversals",
            ),
            "territorialLeadChanges": _sum(
                rows,
                "territorialLeadChanges",
            ),
            "comebackWins": sum(bool(row["comebackWin"]) for row in rows),
            "fullPositionComebackWins": sum(
                bool(row["fullPositionComeback"]) for row in rows
            ),
        },
        "fabrication": {
            "opportunities": _sum(rows, "fabricationOpportunities"),
            "attempts": _sum(rows, "fabricationAttempts"),
            "queues": _sum(rows, "fabricationQueues"),
            "births": _sum(rows, "fabricatedBirths"),
            "gamesUsingFabrication": sum(
                int(row["fabricationQueues"]) > 0 for row in rows
            ),
            "queueLatencyTicks": {
                "count": len(fabrication_latencies),
                "median": (
                    statistics.median(fabrication_latencies)
                    if fabrication_latencies
                    else None
                ),
                "p90": _nearest_rank(fabrication_latencies, 0.90),
                "max": max(fabrication_latencies, default=None),
            },
            "childActorTicks": _sum(rows, "childActorTicks"),
        },
        "anchorAndTurret": {
            "attempts": _sum(rows, "anchorAttempts"),
            "starts": _sum(rows, "anchorStarts"),
            "completions": anchor_completions,
            "cancellations": _sum(rows, "anchorCancellations"),
            "turretActorTicks": turret_actor_ticks,
            "turretShots": _sum(rows, "turretShots"),
            "turretDamage": turret_damage,
            "turretKills": turret_kills,
            "turretDamagePer100ActorTicks": (
                100.0 * turret_damage / turret_actor_ticks
                if turret_actor_ticks
                else None
            ),
            "turretKillsPerCompletedAnchor": (
                turret_kills / anchor_completions
                if anchor_completions
                else None
            ),
        },
        "combat": {
            "shots": _sum(rows, "shots"),
            "damageEvents": _sum(rows, "damageEvents"),
            "damageAmount": _sum(rows, "damageAmount"),
            "gamesWithDamage": sum(
                int(row["damageEvents"]) > 0 for row in rows
            ),
            "reciprocalDamageGames": sum(
                bool(row["reciprocalDamage"]) for row in rows
            ),
            "multiDamageTickGames": sum(
                bool(row["multiDamageTickGame"]) for row in rows
            ),
        },
        "activity": {
            "totalTicks": total_ticks,
            "teamActorlessTicks": _sum(rows, "teamActorlessTicks"),
            "teamActorlessShare": (
                _sum(rows, "teamActorlessTicks")
                / team_actor_denominator
            ),
            "bothTeamsActorlessTicks": _sum(
                rows,
                "bothTeamsActorlessTicks",
            ),
            "bothTeamsActorlessShare": (
                _sum(rows, "bothTeamsActorlessTicks") / total_ticks
            ),
            "stagnantTicks": _sum(rows, "stagnantTicks"),
            "stagnantShare": _sum(rows, "stagnantTicks") / total_ticks,
            "teamActorlessRuns": sum(
                int(row["teamActorlessRuns"]["count"])
                for row in rows
            ),
            "maxTeamActorlessRunTicks": max(
                (
                    int(row["teamActorlessRuns"]["maxTicks"])
                    for row in rows
                ),
                default=0,
            ),
        },
        "validatedActions": dict(sorted(actions.items())),
        "actionSwitches": _sum(rows, "actionSwitches"),
    }


def _find_replays(path: Path) -> list[Path]:
    if path.is_file():
        return [path]
    if not path.is_dir():
        raise ValueError(f"group path does not exist: {path}")
    return sorted(path.rglob("replay.json"))


def _parse_group(value: str) -> tuple[str, Path]:
    if "=" not in value:
        raise argparse.ArgumentTypeError(
            "--group must be NAME=FILE_OR_DIRECTORY"
        )
    name, raw_path = value.split("=", 1)
    if not name or not raw_path:
        raise argparse.ArgumentTypeError(
            "--group must be NAME=FILE_OR_DIRECTORY"
        )
    return name, Path(raw_path)


def _print_report(groups: list[dict[str, Any]]) -> None:
    print(
        "Frontline replay-v2 evaluation "
        "(descriptive only; no composite fun score)"
    )
    for group in groups:
        duration = group["duration"]
        combat = group["combat"]
        fabrication = group["fabrication"]
        turret = group["anchorAndTurret"]
        activity = group["activity"]
        progression = group["progression"]
        print()
        print(
            f"{group['name']}: {group['matches']} matches  "
            f"outcomes={group['outcomes']}  reasons={group['reasons']}"
        )
        cohort = group["cohort"]
        print(
            "  cohort    "
            f"rules={cohort['rulesVersion']} "
            f"maps={cohort['mapIds']} "
            f"runtimes={cohort['runtimeKinds']}"
        )
        print(
            "  duration  "
            f"median={duration['medianTicks']}t/"
            f"{duration['medianSeconds']:.1f}s "
            f"p90={duration['p90Ticks']}t/"
            f"{duration['p90Seconds']:.1f}s "
            f"range={duration['minTicks']}..{duration['maxTicks']}  "
            f"endings={group['endingPhaseAndReason']}"
        )
        print(
            "  combat    "
            f"shots={combat['shots']} damage={combat['damageAmount']} "
            f"damage-games={combat['gamesWithDamage']} "
            f"reciprocal={combat['reciprocalDamageGames']} "
            f"multi-tick={combat['multiDamageTickGames']}"
        )
        print(
            "  frontline "
            f"pushes={progression['pushes']} "
            f"reversals={progression['pushDirectionReversals']} "
            f"lead-changes={progression['territorialLeadChanges']} "
            f"comebacks={progression['comebackWins']}"
        )
        print(
            "  fabricate "
            f"opportunities={fabrication['opportunities']} "
            f"attempts={fabrication['attempts']} "
            f"queues={fabrication['queues']} "
            f"births={fabrication['births']} "
            f"games={fabrication['gamesUsingFabrication']}"
        )
        print(
            "  turrets    "
            f"anchors={turret['completions']} "
            f"actor-ticks={turret['turretActorTicks']} "
            f"shots={turret['turretShots']} "
            f"damage={turret['turretDamage']} "
            f"kills={turret['turretKills']}"
        )
        print(
            "  activity   "
            f"stagnant={activity['stagnantTicks']}/"
            f"{activity['totalTicks']} "
            f"({activity['stagnantShare']:.1%}) "
            f"team-actorless={activity['teamActorlessTicks']} "
            f"({activity['teamActorlessShare']:.1%}) "
            f"both-actorless={activity['bothTeamsActorlessTicks']} "
            f"({activity['bothTeamsActorlessShare']:.1%})"
        )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description=__doc__,
    )
    parser.add_argument(
        "--group",
        action="append",
        required=True,
        type=_parse_group,
        metavar="NAME=PATH",
        help="Replay file or directory; repeat to combine blocks by name.",
    )
    parser.add_argument(
        "--json",
        type=Path,
        help="Write the versioned report, including per-match rows.",
    )
    args = parser.parse_args(argv)

    paths_by_group: dict[str, list[Path]] = defaultdict(list)
    for name, path in args.group:
        paths_by_group[name].extend(_find_replays(path))

    rows: list[dict[str, Any]] = []
    summaries: list[dict[str, Any]] = []
    for name in sorted(paths_by_group):
        paths = list(dict.fromkeys(paths_by_group[name]))
        if not paths:
            raise ValueError(f"group '{name}' contains no replay.json files")
        group_rows: list[dict[str, Any]] = []
        for path in paths:
            try:
                document = _object(
                    json.loads(path.read_text(encoding="utf-8")),
                    str(path),
                )
                row = analyze_replay(
                    document,
                    source=str(path.resolve()),
                    group=name,
                )
            except (OSError, json.JSONDecodeError, ValueError) as error:
                raise ValueError(f"{path}: {error}") from error
            rows.append(row)
            group_rows.append(row)
        summaries.append(summarize_group(name, group_rows))

    report = {
        "schemaVersion": REPORT_SCHEMA_VERSION,
        "metricDefinitionsVersion": METRIC_DEFINITIONS_VERSION,
        "evidenceClass": (
            "descriptive-replay-dynamics; not causal or product verdict"
        ),
        "groups": summaries,
        "matches": sorted(
            rows,
            key=lambda row: (
                row["group"],
                row["source"],
            ),
        ),
    }
    _print_report(summaries)
    if args.json is not None:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(
            json.dumps(report, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        print()
        print(f"JSON: {args.json.resolve()}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ValueError as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(2)
