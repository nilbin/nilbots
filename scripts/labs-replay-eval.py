#!/usr/bin/env python3
"""Describe complete generic Frontline replay-v3 tournament evidence.

The report intentionally has no composite "fun" score and makes no balance
verdict. It exposes outcomes, action/form use, combat, territorial movement,
faults, inactivity, and turret deadlocks for a frozen cohort. Causal claims
still require paired rules arms; product claims still require independently
authored doctrines and outcome-blind replay review.
"""

from __future__ import annotations

import argparse
import copy
import json
import math
import os
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable


REPORT_SCHEMA_VERSION = 1
METRIC_DEFINITIONS_VERSION = "generic-frontline-replay-v3-1"
PRESENTATION_TICKS_PER_SECOND = 5
STALL_TICKS = 20
RECENT_FRAME_WINDOW = 20


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
    return ordered[max(0, math.ceil(percentile * len(ordered)) - 1)]


def _fraction(numerator: int, denominator: int) -> float:
    return numerator / denominator if denominator else 0.0


def _score_values(state: dict[str, Any]) -> dict[tuple[int, str], int]:
    scoreboard = _object(state.get("scoreboard"), "state.scoreboard")
    values: dict[tuple[int, str], int] = {}
    for raw_team in _array(scoreboard.get("teams"), "scoreboard.teams"):
        team = _object(raw_team, "scoreboard team")
        team_id = _integer(team.get("teamId"), "scoreboard teamId")
        for raw_score in _array(team.get("scores"), "team.scores"):
            score = _object(raw_score, "team score")
            values[(team_id, str(score.get("channel")))] = int(
                str(score.get("value"))
            )
    return values


def _state_signature(state: dict[str, Any]) -> str:
    """Return the viewer-relevant tactical frame, excluding clocks/history."""

    lives = []
    for raw in _array(state.get("activeLives"), "state.activeLives"):
        life = _object(raw, "active life")
        actor = _object(life.get("actorId"), "active life actorId")
        position = _object(life.get("position"), "active life position")
        lives.append(
            (
                actor.get("teamId"),
                actor.get("unitId"),
                actor.get("lifeId"),
                life.get("formId"),
                position.get("x"),
                position.get("y"),
                life.get("facing"),
                life.get("health"),
                life.get("cooldown"),
                life.get("energy"),
                life.get("pendingSameLifeTransition"),
            )
        )
    projectiles = []
    for raw in _array(state.get("projectiles"), "state.projectiles"):
        projectile = _object(raw, "projectile")
        normalized = copy.deepcopy(projectile)
        projectiles.append(normalized)
    frame = {
        "participants": state.get("participants"),
        "slots": state.get("slots"),
        "activeLives": sorted(lives),
        "pendingReplications": state.get("pendingReplications"),
        "projectiles": projectiles,
        "scoreboard": state.get("scoreboard"),
        "mode": state.get("mode"),
    }
    return json.dumps(frame, sort_keys=True, separators=(",", ":"))


def _action_run_share(
    actions_by_actor: dict[tuple[int, int, int], list[str]],
) -> float:
    total = sum(len(actions) for actions in actions_by_actor.values())
    in_long_runs = 0
    for actions in actions_by_actor.values():
        start = 0
        while start < len(actions):
            end = start + 1
            while end < len(actions) and actions[end] == actions[start]:
                end += 1
            if end - start >= 4:
                in_long_runs += end - start
            start = end
    return _fraction(in_long_runs, total)


def _normalized_entropy(counts: Counter[str]) -> float:
    total = sum(counts.values())
    nonzero = [count for count in counts.values() if count]
    if total == 0 or len(nonzero) <= 1:
        return 0.0
    entropy = -sum(
        (count / total) * math.log(count / total)
        for count in nonzero
    )
    return entropy / math.log(len(nonzero))


def _transition_catalog(
    rules: dict[str, Any],
) -> tuple[dict[str, set[str]], dict[str, set[str]], dict[str, set[str]]]:
    attempts: dict[str, set[str]] = {
        "fabrication": set(),
        "replication": set(),
        "sameLife": set(),
    }
    transitions: dict[str, set[str]] = {
        "fabrication": set(),
        "replication": set(),
        "sameLife": set(),
    }
    target_forms: dict[str, set[str]] = {
        "fabrication": set(),
        "replication": set(),
        "sameLife": set(),
    }
    for key, family in (
        ("fabricationTransitions", "fabrication"),
        ("replicationTransitions", "replication"),
        ("sameLifeTransitions", "sameLife"),
    ):
        for raw in _array(rules.get(key), f"rules.{key}"):
            transition = _object(raw, f"rules.{key}[]")
            attempts[family].add(str(transition.get("actionId")))
            transitions[family].add(str(transition.get("transitionId")))
            target = transition.get(
                "targetFormId",
                transition.get("outputFormId"),
            )
            if target is not None:
                target_forms[family].add(str(target))
    return attempts, transitions, target_forms


def _region_tiles(
    map_contract: dict[str, Any],
) -> dict[str, set[tuple[int, int]]]:
    regions: dict[str, set[tuple[int, int]]] = {}
    for raw in _array(map_contract.get("regions"), "map.regions"):
        region = _object(raw, "map region")
        tiles = set()
        for raw_tile in _array(region.get("tiles"), "region.tiles"):
            tile = _array(raw_tile, "region tile")
            if len(tile) != 2:
                raise ValueError("region tile must contain x and y")
            tiles.add(
                (
                    _integer(tile[0], "region tile x"),
                    _integer(tile[1], "region tile y"),
                )
            )
        regions[str(region.get("regionId"))] = tiles
    return regions


def analyze_replay(
    document: dict[str, Any],
    *,
    source: str = "",
    group: str = "",
) -> dict[str, Any]:
    """Validate and derive one generic Frontline per-match metric row."""

    header = _object(document.get("header"), "header")
    if header.get("replayVersion") != 3:
        raise ValueError("header.replayVersion must be 3")
    if document.get("partial") is not False:
        raise ValueError("partial must be false")
    replay_hash = document.get("replayHash")
    if not isinstance(replay_hash, str) or len(replay_hash) != 64:
        raise ValueError("replayHash must be a 64-character string")

    result = _object(document.get("result"), "result")
    ticks = _array(document.get("ticks"), "ticks")
    raw_end_tick = result.get("endTick")
    if raw_end_tick is None:
        if ticks:
            raise ValueError("a null result.endTick requires zero ticks")
        end_tick = None
        duration_ticks = 0
    else:
        end_tick = _integer(raw_end_tick, "result.endTick")
        if end_tick < 0:
            raise ValueError("result.endTick must be non-negative")
        actual_ticks = [
            _integer(_object(tick, "tick").get("tick"), "tick.tick")
            for tick in ticks
        ]
        expected_ticks = list(range(end_tick + 1))
        if actual_ticks != expected_ticks:
            raise ValueError(
                "ticks must be contiguous 0..result.endTick "
                f"(got {actual_ticks[:3]}...{actual_ticks[-3:]})"
            )
        duration_ticks = end_tick + 1

    contract = _object(header.get("contract"), "header.contract")
    rules = _object(contract.get("rules"), "contract.rules")
    game_mode = _object(rules.get("gameMode"), "rules.gameMode")
    if game_mode.get("kind") != "frontline":
        raise ValueError("rules.gameMode.kind must be frontline")
    map_contract = _object(contract.get("map"), "contract.map")
    topology = _object(contract.get("topology"), "contract.topology")
    mode_binding = _object(
        contract.get("modeMapBinding"),
        "contract.modeMapBinding",
    )
    if mode_binding.get("kind") != "frontline":
        raise ValueError("contract.modeMapBinding.kind must be frontline")

    action_kinds = {}
    for raw in _array(rules.get("actions"), "rules.actions"):
        action = _object(raw, "rules action")
        action_kinds[str(action.get("id"))] = str(action.get("kind"))
    attempts, transition_ids, target_forms = _transition_catalog(rules)
    form_weights = {
        str(form.get("id")): _integer(
            form.get("objectiveWeight"),
            "form.objectiveWeight",
        )
        for form in (
            _object(raw, "rules form")
            for raw in _array(rules.get("forms"), "rules.forms")
        )
    }

    provenance = []
    participant_team: dict[int, int] = {}
    header_provenance = _object(
        header.get("provenance"),
        "header.provenance",
    )
    for raw in sorted(
        _array(
            header_provenance.get("participants"),
            "header.provenance.participants",
        ),
        key=lambda value: _object(value, "participant").get(
            "participantId",
            -1,
        ),
    ):
        participant = _object(raw, "participant")
        participant_id = _integer(
            participant.get("participantId"),
            "participant.participantId",
        )
        team_id = _integer(
            participant.get("teamId"),
            "participant.teamId",
        )
        participant_team[participant_id] = team_id
        provenance.append(
            {
                "participantId": participant_id,
                "teamId": team_id,
                "name": participant.get("name"),
                "runtimeKind": participant.get("runtimeKind"),
                "artifactHash": participant.get("artifactHash"),
            }
        )

    topology_team_ids = sorted(
        _integer(
            _object(raw, "topology team").get("teamId"),
            "topology teamId",
        )
        for raw in _array(topology.get("teams"), "topology.teams")
    )
    if not topology_team_ids:
        raise ValueError("topology.teams cannot be empty")

    submitted_actions: Counter[str] = Counter()
    successful_actions: Counter[str] = Counter()
    submitted_families: Counter[str] = Counter()
    actions_by_actor: dict[tuple[int, int, int], list[str]] = defaultdict(list)
    mechanic_attempts = Counter()
    mechanic_successes = Counter()
    all_events = list(
        _array(
            _object(document.get("initialFrame"), "initialFrame").get(
                "events"
            ),
            "initialFrame.events",
        )
    )
    damage_events: list[dict[str, Any]] = []
    event_counts: Counter[str] = Counter()
    completion_counts = Counter()
    cancellation_counts = Counter()
    first_meaningful_tick: int | None = None
    stagnant_flags: list[bool] = []
    no_interaction_flags: list[bool] = []
    repeated_flags: list[bool] = []
    both_turret_no_progress_flags: list[bool] = []
    frame_window: list[str] = []
    form_actor_ticks: Counter[str] = Counter()
    peak_bodies_by_team = {team_id: 0 for team_id in topology_team_ids}
    objective_contested_ticks = 0
    objective_sole_ticks = 0
    objective_evictions = 0
    previous_contested = False
    pushes = 0
    push_directions: list[int] = []
    score_lead_changes = 0
    previous_leader: int | None = None

    region_tiles = _region_tiles(map_contract)
    objective_region_ids = [
        str(value)
        for value in _array(
            mode_binding.get("orderedObjectiveRegionIds"),
            "modeMapBinding.orderedObjectiveRegionIds",
        )
    ]

    for raw_tick in ticks:
        tick = _object(raw_tick, "tick")
        tick_number = _integer(tick.get("tick"), "tick.tick")
        tick_start = _object(tick.get("tickStart"), "tick.tickStart")
        start_state = _object(tick_start.get("state"), "tickStart.state")
        post_state = _object(tick.get("postState"), "tick.postState")
        tick_events = [
            *_array(tick_start.get("events"), "tickStart.events"),
            *_array(tick.get("events"), "tick.events"),
        ]
        all_events.extend(tick_events)
        traversals = _array(tick.get("traversals"), "tick.traversals")

        meaningful = False
        for raw_event in tick_events:
            event = _object(raw_event, "event")
            kind = str(event.get("kind"))
            event_counts[kind] += 1
            if kind == "damage":
                damage_events.append(_object(event.get("payload"), "damage"))
            if kind in {
                "movement",
                "attack",
                "damage",
                "destruction",
                "life-spawned",
                "life-retired",
                "lifecycle-queued",
                "lifecycle-completed",
                "form-transition-started",
                "form-transition-completed",
                "form-transition-cancelled",
                "score-changed",
                "mode-changed",
            }:
                meaningful = True
            payload = _object(event.get("payload"), "event.payload")
            transition_id = payload.get("transitionId")
            for family in ("fabrication", "replication", "sameLife"):
                if transition_id in transition_ids[family]:
                    if kind in {
                        "lifecycle-completed",
                        "form-transition-completed",
                    }:
                        completion_counts[family] += 1
                    if kind in {
                        "lifecycle-cancelled",
                        "form-transition-cancelled",
                    }:
                        cancellation_counts[family] += 1

        for raw_turn in _array(tick.get("actorTurns"), "tick.actorTurns"):
            turn = _object(raw_turn, "actor turn")
            actor = _object(turn.get("actorId"), "actor turn actorId")
            actor_key = (
                _integer(actor.get("teamId"), "actor teamId"),
                _integer(actor.get("unitId"), "actor unitId"),
                _integer(actor.get("lifeId"), "actor lifeId"),
            )
            resolution = _object(
                turn.get("actionResolution"),
                "actionResolution",
            )
            raw_decision = turn.get("submittedDecision")
            if raw_decision is None:
                if resolution.get("outcome") != "faulted":
                    raise ValueError(
                        "submittedDecision may be null only for a faulted turn"
                    )
                continue
            decision = _object(raw_decision, "submittedDecision")
            action_id = str(decision.get("actionId"))
            family = action_kinds.get(action_id, "unknown")
            submitted_actions[action_id] += 1
            submitted_families[family] += 1
            actions_by_actor[actor_key].append(family)
            if resolution.get("outcome") == "success":
                successful_actions[action_id] += 1
            for mechanic in ("fabrication", "replication", "sameLife"):
                if action_id in attempts[mechanic]:
                    mechanic_attempts[mechanic] += 1
                    if resolution.get("outcome") == "success":
                        mechanic_successes[mechanic] += 1

        active_lives = [
            _object(raw, "active life")
            for raw in _array(post_state.get("activeLives"), "activeLives")
        ]
        team_bodies = Counter(
            _integer(
                _object(life.get("actorId"), "life.actorId").get("teamId"),
                "life teamId",
            )
            for life in active_lives
        )
        for team_id in topology_team_ids:
            peak_bodies_by_team[team_id] = max(
                peak_bodies_by_team[team_id],
                team_bodies[team_id],
            )
        for life in active_lives:
            form_actor_ticks[str(life.get("formId"))] += 1

        mode = _object(post_state.get("mode"), "postState.mode")
        position_index = _integer(
            mode.get("activePositionIndex"),
            "mode.activePositionIndex",
        )
        objective_tiles = (
            region_tiles.get(objective_region_ids[position_index], set())
            if 0 <= position_index < len(objective_region_ids)
            else set()
        )
        occupying_teams = set()
        for life in active_lives:
            if form_weights.get(str(life.get("formId")), 0) <= 0:
                continue
            position = _object(life.get("position"), "life.position")
            tile = (
                _integer(position.get("x"), "life.position.x"),
                _integer(position.get("y"), "life.position.y"),
            )
            if tile in objective_tiles:
                occupying_teams.add(
                    _integer(
                        _object(
                            life.get("actorId"),
                            "life.actorId",
                        ).get("teamId"),
                        "life teamId",
                    )
                )
        contested = len(occupying_teams) > 1
        sole = len(occupying_teams) == 1
        objective_contested_ticks += int(contested)
        objective_sole_ticks += int(sole)
        if previous_contested and sole:
            objective_evictions += 1
        previous_contested = contested

        start_mode = _object(start_state.get("mode"), "tickStart.mode")
        start_index = _integer(
            start_mode.get("activePositionIndex"),
            "tickStart activePositionIndex",
        )
        if position_index != start_index:
            pushes += abs(position_index - start_index)
            push_directions.append(
                1 if position_index > start_index else -1
            )

        scores = _score_values(post_state)
        progress = {
            team_id: scores.get((team_id, "territorial-progress"), 0)
            for team_id in topology_team_ids
        }
        high = max(progress.values())
        leaders = [
            team_id for team_id, value in progress.items() if value == high
        ]
        leader = leaders[0] if len(leaders) == 1 else None
        if (
            previous_leader is not None
            and leader is not None
            and leader != previous_leader
        ):
            score_lead_changes += 1
        if leader is not None:
            previous_leader = leader

        start_signature = _state_signature(start_state)
        post_signature = _state_signature(post_state)
        stagnant = (
            start_signature == post_signature
            and not tick_events
            and not traversals
        )
        stagnant_flags.append(stagnant)
        no_interaction = not meaningful and not traversals
        no_interaction_flags.append(no_interaction)
        if meaningful and first_meaningful_tick is None:
            first_meaningful_tick = tick_number

        action_frame = tuple(
            sorted(
                (
                    (
                        _object(turn, "actor turn")
                        .get("submittedDecision")
                        or {}
                    ).get("actionId", "<fault>")
                    for turn in _array(
                        tick.get("actorTurns"),
                        "tick.actorTurns",
                    )
                )
            )
        )
        frame = post_signature + "|" + json.dumps(action_frame)
        repeated_flags.append(frame in frame_window)
        frame_window.append(frame)
        if len(frame_window) > RECENT_FRAME_WINDOW:
            frame_window.pop(0)

        turret_forms = target_forms["sameLife"]
        turret_teams = {
            _integer(
                _object(life.get("actorId"), "life.actorId").get("teamId"),
                "life teamId",
            )
            for life in active_lives
            if str(life.get("formId")) in turret_forms
        }
        made_progress = _score_values(start_state) != scores
        both_turret_no_progress_flags.append(
            len(turret_teams) >= 2 and not made_progress
        )

    for raw_event in all_events[: len(
        _array(
            _object(document.get("initialFrame"), "initialFrame").get(
                "events"
            ),
            "initialFrame.events",
        )
    )]:
        event_counts[str(_object(raw_event, "event").get("kind"))] += 1

    standings = _object(result.get("standings"), "result.standings")
    winner_team_id = standings.get("winnerTeamId")
    if winner_team_id is not None:
        winner_team_id = _integer(
            winner_team_id,
            "standings.winnerTeamId",
        )
    team_standings = []
    for raw in _array(standings.get("teams"), "standings.teams"):
        standing = _object(raw, "team standing")
        team_standings.append(
            {
                "teamId": standing.get("teamId"),
                "rank": standing.get("rank"),
                "outcome": standing.get("outcome"),
                "scores": standing.get("scores"),
            }
        )

    damage_by_source_team = Counter(
        _integer(payload.get("sourceTeamId"), "damage.sourceTeamId")
        for payload in damage_events
    )
    damage_ticks = {
        _integer(
            _object(event, "event").get("tick"),
            "damage event tick",
        )
        for event in all_events
        if _object(event, "event").get("kind") == "damage"
    }
    runtime_fault_events = event_counts["runtime-fault"]
    disqualification_events = event_counts["participant-disqualified"]
    initial_state = _object(
        _object(document.get("initialFrame"), "initialFrame").get("state"),
        "initialFrame.state",
    )
    final_state = (
        _object(ticks[-1], "tick").get("postState")
        if ticks
        else initial_state
    )
    final_state = _object(final_state, "final state")
    cumulative_faults = sum(
        int(str(_object(raw, "participant state").get("runtimeFaultCount")))
        for raw in _array(
            final_state.get("participants"),
            "finalState.participants",
        )
    )

    stagnant_runs = _run_lengths(stagnant_flags)
    repeated_runs = _run_lengths(repeated_flags)
    no_interaction_runs = _run_lengths(no_interaction_flags)
    turret_deadlock_runs = _run_lengths(both_turret_no_progress_flags)
    push_reversals = sum(
        left != right
        for left, right in zip(push_directions, push_directions[1:])
    )
    mechanic_rows = {}
    for family, public_name in (
        ("fabrication", "fabrication"),
        ("replication", "split"),
        ("sameLife", "anchor"),
    ):
        actor_ticks = sum(
            form_actor_ticks[form_id]
            for form_id in target_forms[family]
        )
        mechanic_rows[public_name] = {
            "attempts": mechanic_attempts[family],
            "successfulActions": mechanic_successes[family],
            "completions": completion_counts[family],
            "cancellations": cancellation_counts[family],
            "targetFormActorTicks": actor_ticks,
            "targetFormIds": sorted(target_forms[family]),
        }

    return {
        "source": source,
        "group": group,
        "identity": {
            "replayHash": replay_hash,
            "seed": header.get("seed"),
            "rulesVersion": header.get("gameRulesVersion"),
            "rulesFingerprint": rules.get("rulesFingerprint"),
            "mapId": map_contract.get("mapId"),
            "mapVersion": map_contract.get("mapVersion"),
            "mapFingerprint": map_contract.get("mapFingerprint"),
            "matchContractFingerprint": contract.get(
                "matchContractFingerprint"
            ),
            "contractProfileId": _object(
                header.get("runtime"),
                "header.runtime",
            ).get("contractProfileId"),
            "participants": provenance,
        },
        "result": {
            "completionReason": result.get("completionReason"),
            "endTick": end_tick,
            "winnerTeamId": winner_team_id,
            "draw": winner_team_id is None,
            "teams": team_standings,
        },
        "duration": {
            "ticks": duration_ticks,
            "seconds": duration_ticks / PRESENTATION_TICKS_PER_SECOND,
        },
        "safety": {
            "runtimeFaultEvents": runtime_fault_events,
            "cumulativeRuntimeFaults": cumulative_faults,
            "participantDisqualifications": disqualification_events,
        },
        "actions": {
            "submitted": dict(sorted(submitted_actions.items())),
            "successful": dict(sorted(successful_actions.items())),
            "families": dict(sorted(submitted_families.items())),
            "normalizedFamilyEntropy": _normalized_entropy(
                submitted_families
            ),
            "longRunDecisionShare": _action_run_share(actions_by_actor),
        },
        "mechanics": mechanic_rows,
        "forms": {
            "actorTicks": dict(sorted(form_actor_ticks.items())),
            "peakBodiesByTeam": {
                str(team_id): peak_bodies_by_team[team_id]
                for team_id in topology_team_ids
            },
        },
        "combat": {
            "attacks": event_counts["attack"],
            "damageEvents": len(damage_events),
            "damageAmount": sum(
                _integer(payload.get("amount"), "damage.amount")
                for payload in damage_events
            ),
            "damageTicks": len(damage_ticks),
            "destructions": event_counts["destruction"],
            "damagingTeams": sorted(damage_by_source_team),
            "reciprocalDamage": len(damage_by_source_team) >= 2,
        },
        "objective": {
            "contestedTicks": objective_contested_ticks,
            "soleControlTicks": objective_sole_ticks,
            "contestedToSoleTransitions": objective_evictions,
            "pushes": pushes,
            "pushDirectionReversals": push_reversals,
            "scoreLeadChanges": score_lead_changes,
        },
        "activity": {
            "firstMeaningfulInteractionTick": first_meaningful_tick,
            "activeTicks": duration_ticks - sum(stagnant_flags),
            "stagnantTicks": sum(stagnant_flags),
            "longestStagnantRunTicks": max(stagnant_runs, default=0),
            "recentRepeatTicks": sum(repeated_flags),
            "longestRecentRepeatRunTicks": max(
                repeated_runs,
                default=0,
            ),
            "stalled": max(stagnant_runs, default=0) >= STALL_TICKS,
            "looped": max(repeated_runs, default=0) >= STALL_TICKS,
            "longestNoInteractionRunTicks": max(
                no_interaction_runs,
                default=0,
            ),
            "longestBothTurretNoProgressRunTicks": max(
                turret_deadlock_runs,
                default=0,
            ),
        },
    }


def summarize_group(name: str, rows: list[dict[str, Any]]) -> dict[str, Any]:
    if not rows:
        raise ValueError(f"group '{name}' contains no replays")
    rules_fingerprints = {
        row["identity"]["rulesFingerprint"] for row in rows
    }
    if len(rules_fingerprints) != 1:
        raise ValueError(
            f"group '{name}' mixes rules fingerprints: "
            f"{sorted(rules_fingerprints)}"
        )
    runtime_classes = {
        (
            "wasm"
            if "wasm" in str(participant["runtimeKind"]).lower()
            else "in-process"
        )
        for row in rows
        for participant in row["identity"]["participants"]
    }
    if len(runtime_classes) != 1:
        raise ValueError(
            f"group '{name}' mixes runtime classes: "
            f"{sorted(runtime_classes)}"
        )

    durations = [row["duration"]["ticks"] for row in rows]
    total_ticks = sum(durations)
    outcomes = Counter(
        "draw"
        if row["result"]["draw"]
        else f"team-{row['result']['winnerTeamId']}"
        for row in rows
    )
    reasons = Counter(
        str(row["result"]["completionReason"]) for row in rows
    )
    mechanics = {}
    for family in ("fabrication", "split", "anchor"):
        mechanics[family] = {
            key: sum(row["mechanics"][family][key] for row in rows)
            for key in (
                "attempts",
                "successfulActions",
                "completions",
                "cancellations",
                "targetFormActorTicks",
            )
        }

    first_interactions = [
        row["activity"]["firstMeaningfulInteractionTick"]
        for row in rows
        if row["activity"]["firstMeaningfulInteractionTick"] is not None
    ]
    active_ticks = sum(row["activity"]["activeTicks"] for row in rows)
    stagnant_ticks = sum(
        row["activity"]["stagnantTicks"] for row in rows
    )
    return {
        "name": name,
        "matches": len(rows),
        "cohort": {
            "rulesVersion": sorted(
                {row["identity"]["rulesVersion"] for row in rows}
            ),
            "rulesFingerprint": next(iter(rules_fingerprints)),
            "mapIds": sorted({row["identity"]["mapId"] for row in rows}),
            "contractProfiles": sorted(
                {row["identity"]["contractProfileId"] for row in rows}
            ),
            "runtimeClasses": sorted(runtime_classes),
            "artifactHashes": sorted(
                {
                    participant["artifactHash"]
                    for row in rows
                    for participant in row["identity"]["participants"]
                }
            ),
        },
        "outcomes": dict(sorted(outcomes.items())),
        "completionReasons": dict(sorted(reasons.items())),
        "drawRate": _fraction(outcomes["draw"], len(rows)),
        "duration": {
            "medianTicks": statistics.median(durations),
            "p10Ticks": _nearest_rank(durations, 0.10),
            "p90Ticks": _nearest_rank(durations, 0.90),
            "maxTicks": max(durations),
            "medianSeconds": statistics.median(durations)
            / PRESENTATION_TICKS_PER_SECOND,
            "p90Seconds": (
                _nearest_rank(durations, 0.90)
                / PRESENTATION_TICKS_PER_SECOND
            ),
        },
        "safety": {
            "runtimeFaultEvents": sum(
                row["safety"]["runtimeFaultEvents"] for row in rows
            ),
            "cumulativeRuntimeFaults": sum(
                row["safety"]["cumulativeRuntimeFaults"] for row in rows
            ),
            "participantDisqualifications": sum(
                row["safety"]["participantDisqualifications"]
                for row in rows
            ),
        },
        "combat": {
            "gamesWithDamage": sum(
                row["combat"]["damageEvents"] > 0 for row in rows
            ),
            "reciprocalDamageGames": sum(
                row["combat"]["reciprocalDamage"] for row in rows
            ),
            "multiDamageTickGames": sum(
                row["combat"]["damageTicks"] >= 2 for row in rows
            ),
            "attacks": sum(row["combat"]["attacks"] for row in rows),
            "damageAmount": sum(
                row["combat"]["damageAmount"] for row in rows
            ),
            "destructions": sum(
                row["combat"]["destructions"] for row in rows
            ),
        },
        "mechanics": mechanics,
        "objective": {
            key: sum(row["objective"][key] for row in rows)
            for key in (
                "contestedTicks",
                "soleControlTicks",
                "contestedToSoleTransitions",
                "pushes",
                "pushDirectionReversals",
                "scoreLeadChanges",
            )
        },
        "activity": {
            "activeTicks": active_ticks,
            "activeShare": _fraction(active_ticks, total_ticks),
            "stagnantTicks": stagnant_ticks,
            "stagnantShare": _fraction(stagnant_ticks, total_ticks),
            "stalledGames": sum(
                row["activity"]["stalled"] for row in rows
            ),
            "loopedGames": sum(
                row["activity"]["looped"] for row in rows
            ),
            "medianFirstMeaningfulInteractionTick": (
                statistics.median(first_interactions)
                if first_interactions
                else None
            ),
            "gamesFirstInteractionAfterTick120": sum(
                tick > 120 for tick in first_interactions
            )
            + (len(rows) - len(first_interactions)),
            "maxNoInteractionRunTicks": max(
                row["activity"]["longestNoInteractionRunTicks"]
                for row in rows
            ),
            "gamesWithNoInteractionRunAtLeast75": sum(
                row["activity"]["longestNoInteractionRunTicks"] >= 75
                for row in rows
            ),
            "maxBothTurretNoProgressRunTicks": max(
                row["activity"][
                    "longestBothTurretNoProgressRunTicks"
                ]
                for row in rows
            ),
            "gamesWithTurretDeadlockRunAtLeast60": sum(
                row["activity"][
                    "longestBothTurretNoProgressRunTicks"
                ]
                >= 60
                for row in rows
            ),
            "medianNormalizedActionFamilyEntropy": statistics.median(
                row["actions"]["normalizedFamilyEntropy"] for row in rows
            ),
            "medianLongRunDecisionShare": statistics.median(
                row["actions"]["longRunDecisionShare"] for row in rows
            ),
        },
    }


def _find_replays(path: Path) -> list[Path]:
    if path.is_file():
        return [path]
    return sorted(path.rglob("replay.json"))


def _parse_group(value: str) -> tuple[str, Path]:
    name, separator, raw_path = value.partition("=")
    if not separator or not name or not raw_path:
        raise argparse.ArgumentTypeError("expected NAME=PATH")
    return name, Path(raw_path)


def _print_report(groups: list[dict[str, Any]]) -> None:
    print(
        "Generic Frontline replay-v3 dynamics "
        "(descriptive only; no composite fun score)"
    )
    for group in groups:
        duration = group["duration"]
        safety = group["safety"]
        combat = group["combat"]
        activity = group["activity"]
        mechanics = group["mechanics"]
        print()
        print(
            f"{group['name']}: {group['matches']} matches  "
            f"outcomes={group['outcomes']}  "
            f"reasons={group['completionReasons']}"
        )
        print(
            "  duration  "
            f"median={duration['medianTicks']}t/"
            f"{duration['medianSeconds']:.1f}s "
            f"p90={duration['p90Ticks']}t/"
            f"{duration['p90Seconds']:.1f}s "
            f"max={duration['maxTicks']}t"
        )
        print(
            "  safety    "
            f"fault-events={safety['runtimeFaultEvents']} "
            f"cumulative-faults={safety['cumulativeRuntimeFaults']} "
            f"disqualifications={safety['participantDisqualifications']}"
        )
        print(
            "  combat    "
            f"damage-games={combat['gamesWithDamage']} "
            f"reciprocal={combat['reciprocalDamageGames']} "
            f"multi-tick={combat['multiDamageTickGames']} "
            f"damage={combat['damageAmount']} "
            f"destructions={combat['destructions']}"
        )
        print(
            "  mechanics "
            f"fabricate={mechanics['fabrication']['completions']} "
            f"split={mechanics['split']['completions']} "
            f"anchor={mechanics['anchor']['completions']}"
        )
        print(
            "  activity  "
            f"active={activity['activeShare']:.1%} "
            f"stalled={activity['stalledGames']} "
            f"looped={activity['loopedGames']} "
            f"max-no-interaction="
            f"{activity['maxNoInteractionRunTicks']}t "
            f"max-turret-deadlock="
            f"{activity['maxBothTurretNoProgressRunTicks']}t"
        )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
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
        help="Write the versioned report, including every per-match row.",
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
        source_root = Path(
            os.path.commonpath(
                [str(path.resolve().parent) for path in paths]
            )
        )
        group_rows = []
        for path in paths:
            try:
                document = _object(
                    json.loads(path.read_text(encoding="utf-8")),
                    str(path),
                )
                row = analyze_replay(
                    document,
                    source=path.resolve()
                    .relative_to(source_root)
                    .as_posix(),
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
            key=lambda row: (row["group"], row["source"]),
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
