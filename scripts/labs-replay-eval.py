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


REPORT_SCHEMA_VERSION = 2
METRIC_DEFINITIONS_VERSION = "generic-frontline-replay-v3-5"
PRESENTATION_TICKS_PER_SECOND = 5
STALL_TICKS = 20
RECENT_FRAME_WINDOW = 20
HEADING_SECTORS = {
    "north": 0,
    "north-east": 1,
    "east": 2,
    "south-east": 3,
    "south": 4,
    "south-west": 5,
    "west": 6,
    "north-west": 7,
}


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


def _actor_key(
    value: dict[str, Any],
    label: str,
) -> tuple[int, int, int]:
    return (
        _integer(value.get("teamId"), f"{label}.teamId"),
        _integer(value.get("unitId"), f"{label}.unitId"),
        _integer(value.get("lifeId"), f"{label}.lifeId"),
    )


def _position(
    value: Any,
    label: str,
) -> tuple[int, int]:
    position = _object(value, label)
    return (
        _integer(position.get("x"), f"{label}.x"),
        _integer(position.get("y"), f"{label}.y"),
    )


def _distance_to_region(
    position: tuple[int, int],
    tiles: set[tuple[int, int]],
) -> int | None:
    if not tiles:
        return None
    return min(
        max(abs(position[0] - x), abs(position[1] - y))
        for x, y in tiles
    )


def _phase_label(tick: int, unlock_ticks: list[int]) -> str:
    completed = sum(tick >= unlock_tick for unlock_tick in unlock_ticks)
    if completed == 0:
        return "before-first-unlock"
    return f"after-unlock-{completed}"


def _ray_sector(
    source: tuple[int, int],
    target: tuple[int, int],
) -> tuple[int, int] | None:
    dx = target[0] - source[0]
    dy = target[1] - source[1]
    distance = max(abs(dx), abs(dy))
    if distance == 0:
        return None
    if dx != 0 and dy != 0 and abs(dx) != abs(dy):
        return None
    step = (dx // distance, dy // distance)
    sector = {
        (0, -1): 0,
        (1, -1): 1,
        (1, 0): 2,
        (1, 1): 3,
        (0, 1): 4,
        (-1, 1): 5,
        (-1, 0): 6,
        (-1, -1): 7,
    }.get(step)
    return None if sector is None else (sector, distance)


def _sector_delta(left: int, right: int) -> int:
    raw = abs(left - right) % 8
    return min(raw, 8 - raw)


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
    form_weights: dict[str, int],
) -> tuple[
    dict[str, set[str]],
    dict[str, str],
    dict[tuple[str, str, str], str],
    dict[tuple[str, str], set[str]],
    dict[str, set[str]],
    dict[str, set[str]],
    set[str],
]:
    actions: dict[str, set[str]] = {
        "fabrication": set(),
        "split": set(),
        "anchor": set(),
    }
    transition_mechanics: dict[str, str] = {}
    route_mechanics: dict[tuple[str, str, str], str] = {}
    source_action_mechanics: dict[tuple[str, str], set[str]] = defaultdict(set)
    action_mechanics: dict[str, set[str]] = defaultdict(set)
    target_forms: dict[str, set[str]] = {
        "fabrication": set(),
        "split": set(),
        "anchor": set(),
    }
    fortified_forms: set[str] = set()

    for key, fixed_name in (
        ("fabricationTransitions", "fabrication"),
        ("replicationTransitions", "split"),
        ("sameLifeTransitions", None),
    ):
        for raw in _array(rules.get(key), f"rules.{key}"):
            transition = _object(raw, f"rules.{key}[]")
            action_id = str(transition.get("actionId"))
            transition_id = str(transition.get("transitionId"))
            target = transition.get(
                "targetFormId",
                transition.get("outputFormId"),
            )
            source_forms = transition.get("sourceFormIds")
            if source_forms is None:
                raw_source = transition.get("sourceFormId")
                source_forms = [] if raw_source is None else [raw_source]
            source_form_ids = {
                str(value)
                for value in _array(
                    source_forms,
                    f"rules.{key}[].sourceFormIds",
                )
            }

            public_name = fixed_name
            if public_name is None:
                target_form_id = None if target is None else str(target)
                source_weights = {
                    form_weights.get(source_form_id)
                    for source_form_id in source_form_ids
                }
                target_weight = (
                    None
                    if target_form_id is None
                    else form_weights.get(target_form_id)
                )
                if (
                    source_weights
                    and None not in source_weights
                    and target_weight is not None
                    and all(weight > 0 for weight in source_weights)
                    and target_weight == 0
                ):
                    public_name = "anchor"
                    fortified_forms.add(target_form_id)
                elif (
                    source_weights
                    and None not in source_weights
                    and target_weight is not None
                    and all(weight == 0 for weight in source_weights)
                    and target_weight > 0
                ):
                    public_name = "mobilize"
                else:
                    public_name = action_id

            actions.setdefault(public_name, set()).add(action_id)
            transition_mechanics[transition_id] = public_name
            action_mechanics[action_id].add(public_name)
            for source_form_id in source_form_ids:
                source_action_mechanics[
                    (source_form_id, action_id)
                ].add(public_name)
                if target is not None:
                    route_mechanics[
                        (source_form_id, action_id, str(target))
                    ] = public_name
            if target is not None:
                target_forms.setdefault(public_name, set()).add(str(target))
    return (
        actions,
        transition_mechanics,
        route_mechanics,
        source_action_mechanics,
        action_mechanics,
        target_forms,
        fortified_forms,
    )


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
    forms = [
        _object(raw, "rules form")
        for raw in _array(rules.get("forms"), "rules.forms")
    ]
    form_weights = {
        str(form.get("id")): _integer(
            form.get("objectiveWeight"),
            "form.objectiveWeight",
        )
        for form in forms
    }
    form_attack_profile = {
        str(form.get("id")): (
            None
            if form.get("attackProfileId") is None
            else str(form.get("attackProfileId"))
        )
        for form in forms
    }
    attack_profiles = {
        str(profile.get("id")): profile
        for profile in (
            _object(raw, "attack profile")
            for raw in _array(
                rules.get("attackProfiles"),
                "rules.attackProfiles",
            )
        )
    }
    (
        mechanic_actions,
        transition_mechanics,
        route_mechanics,
        source_action_mechanics,
        action_mechanics,
        target_forms,
        fortified_forms,
    ) = _transition_catalog(rules, form_weights)

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

    initial_frame = _object(document.get("initialFrame"), "initialFrame")
    initial_state = _object(initial_frame.get("state"), "initialFrame.state")
    slot_participant: dict[tuple[int, int], int] = {}
    slot_counts: Counter[int] = Counter()
    for raw in _array(topology.get("unitSlots"), "topology.unitSlots"):
        slot = _object(raw, "topology unit slot")
        slot_key = (
            _integer(slot.get("teamId"), "unit slot teamId"),
            _integer(slot.get("unitId"), "unit slot unitId"),
        )
        participant_id = _integer(
            slot.get("controllerParticipantId"),
            "unit slot controllerParticipantId",
        )
        slot_participant[slot_key] = participant_id
        slot_counts[participant_id] += 1

    unlock_ticks = sorted(
        {
            _integer(assignment.get("unlockTick"), "unlockTick")
            for raw in _array(
                contract.get("lifecycleAssignments"),
                "contract.lifecycleAssignments",
            )
            for assignment in [
                _object(raw, "lifecycle assignment")
            ]
            if assignment.get("unlockTick") is not None
        }
    )
    phase_boundaries = [0, *unlock_ticks]

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
    first_combat_tick: int | None = None
    stagnant_flags: list[bool] = []
    no_interaction_flags: list[bool] = []
    no_combat_event_flags: list[bool] = []
    repeated_flags: list[bool] = []
    both_turret_no_progress_flags: list[bool] = []
    frame_window: list[str] = []
    form_actor_ticks: Counter[str] = Counter()
    peak_bodies_by_team = {team_id: 0 for team_id in topology_team_ids}
    objective_contested_ticks = 0
    objective_sole_ticks = 0
    objective_empty_ticks = 0
    objective_equal_contest_ticks = 0
    objective_surplus_contest_ticks = 0
    global_weight_surplus_ticks = 0
    global_surplus_realized_on_objective_ticks = 0
    objective_evictions = 0
    previous_contested = False
    pushes = 0
    push_directions: list[int] = []
    score_lead_changes = 0
    previous_leader: int | None = None
    participant_stats: dict[int, Counter[str]] = {
        participant_id: Counter()
        for participant_id in participant_team
    }
    ready_episode_start: dict[tuple[int, int], int] = {}
    ready_latencies: dict[int, list[int]] = defaultdict(list)
    actor_participant: dict[tuple[int, int, int], int] = {}
    projectile_participant: dict[str, int] = {}
    phase_body_ticks: dict[int, Counter[str]] = defaultdict(Counter)
    phase_ticks: Counter[str] = Counter()

    for raw in _array(
        initial_state.get("activeLives"),
        "initialFrame.state.activeLives",
    ):
        life = _object(raw, "initial active life")
        actor_participant[
            _actor_key(
                _object(life.get("actorId"), "initial life.actorId"),
                "initial life.actorId",
            )
        ] = _integer(
            life.get("participantId"),
            "initial life participantId",
        )

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

        start_lives = [
            _object(raw, "tick-start active life")
            for raw in _array(
                start_state.get("activeLives"),
                "tickStart.state.activeLives",
            )
        ]
        for life in start_lives:
            actor_participant[
                _actor_key(
                    _object(life.get("actorId"), "life.actorId"),
                    "life.actorId",
                )
            ] = _integer(
                life.get("participantId"),
                "active life participantId",
            )

        phase = _phase_label(tick_number, unlock_ticks)
        phase_ticks[phase] += 1
        active_slots_by_participant: Counter[int] = Counter()
        eligible_slots_by_participant: Counter[int] = Counter()
        ready_slots_by_participant: Counter[int] = Counter()
        for raw_slot in _array(start_state.get("slots"), "state.slots"):
            slot = _object(raw_slot, "state slot")
            slot_key = (
                _integer(slot.get("teamId"), "state slot teamId"),
                _integer(slot.get("unitId"), "state slot unitId"),
            )
            participant_id = _integer(
                slot.get("participantId"),
                "state slot participantId",
            )
            if slot_participant.get(slot_key) != participant_id:
                raise ValueError(
                    f"slot {slot_key} changes participant ownership"
                )
            state = _object(slot.get("state"), "state slot.state")
            kind = str(state.get("kind"))
            locked = (
                kind == "availability-pending"
                and state.get("reason") == "initial-unlock"
            )
            eligible = not locked and kind != "permanently-dormant"
            active_slots_by_participant[participant_id] += int(
                kind == "active"
            )
            eligible_slots_by_participant[participant_id] += int(eligible)
            ready_slots_by_participant[participant_id] += int(
                kind == "ready"
            )

            if kind == "ready":
                ready_episode_start.setdefault(slot_key, tick_number)
            elif kind == "active" and slot_key in ready_episode_start:
                ready_latencies[participant_id].append(
                    tick_number - ready_episode_start.pop(slot_key)
                )

        for participant_id in participant_team:
            active = active_slots_by_participant[participant_id]
            eligible = eligible_slots_by_participant[participant_id]
            ready = ready_slots_by_participant[participant_id]
            stats = participant_stats[participant_id]
            stats["bodyTicks"] += active
            stats["eligibleSlotTicks"] += eligible
            stats["readySlotTicks"] += ready
            stats["fullEligiblePopulationTicks"] += int(
                eligible > 0 and active == eligible
            )
            stats["peakBodies"] = max(stats["peakBodies"], active)
            phase_body_ticks[participant_id][phase] += active

        start_mode = _object(start_state.get("mode"), "tickStart.mode")
        start_index = _integer(
            start_mode.get("activePositionIndex"),
            "tickStart activePositionIndex",
        )
        start_objective_tiles = (
            region_tiles.get(objective_region_ids[start_index], set())
            if 0 <= start_index < len(objective_region_ids)
            else set()
        )

        meaningful = False
        combat_event = False
        for raw_event in tick_events:
            event = _object(raw_event, "event")
            kind = str(event.get("kind"))
            event_counts[kind] += 1
            combat_event = combat_event or kind in {"attack", "damage"}
            payload = _object(event.get("payload"), "event.payload")
            if kind == "life-spawned":
                actor_participant[
                    _actor_key(
                        _object(
                            payload.get("actorId"),
                            "life-spawned.actorId",
                        ),
                        "life-spawned.actorId",
                    )
                ] = _integer(
                    payload.get("participantId"),
                    "life-spawned participantId",
                )
            elif kind == "attack":
                source_key = _actor_key(
                    _object(payload.get("actorId"), "attack.actorId"),
                    "attack.actorId",
                )
                participant_id = actor_participant.get(source_key)
                if participant_id is not None:
                    participant_stats[participant_id]["attacksLaunched"] += 1
                    projectile_participant[str(payload.get("projectileId"))] = (
                        participant_id
                    )
            elif kind == "damage":
                projectile_id = str(payload.get("projectileId"))
                participant_id = projectile_participant.get(projectile_id)
                if participant_id is None:
                    source_actor = payload.get("sourceActorId")
                    if source_actor is not None:
                        participant_id = actor_participant.get(
                            _actor_key(
                                _object(
                                    source_actor,
                                    "damage.sourceActorId",
                                ),
                                "damage.sourceActorId",
                            )
                        )
                if participant_id is not None:
                    participant_stats[participant_id]["damageEvents"] += 1
                    participant_stats[participant_id]["damageAmount"] += (
                        _integer(payload.get("amount"), "damage.amount")
                    )
            elif kind == "movement":
                source_key = _actor_key(
                    _object(payload.get("actorId"), "movement.actorId"),
                    "movement.actorId",
                )
                participant_id = actor_participant.get(source_key)
                if participant_id is not None:
                    before = _distance_to_region(
                        _position(payload.get("from"), "movement.from"),
                        start_objective_tiles,
                    )
                    after = _distance_to_region(
                        _position(payload.get("to"), "movement.to"),
                        start_objective_tiles,
                    )
                    if before is not None and after is not None:
                        direction = (
                            "towardObjectiveMoves"
                            if after < before
                            else "awayFromObjectiveMoves"
                            if after > before
                            else "lateralObjectiveMoves"
                        )
                        participant_stats[participant_id][direction] += 1
            if kind == "damage":
                damage_events.append(payload)
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
            transition_id = str(payload.get("transitionId"))
            mechanic = transition_mechanics.get(transition_id)
            if mechanic is not None:
                if kind in {
                    "lifecycle-completed",
                    "form-transition-completed",
                }:
                    completion_counts[mechanic] += 1
                if kind in {
                    "lifecycle-cancelled",
                    "form-transition-cancelled",
                }:
                    cancellation_counts[mechanic] += 1

        start_form_by_actor = {
            (
                _integer(actor_id.get("teamId"), "life teamId"),
                _integer(actor_id.get("unitId"), "life unitId"),
                _integer(actor_id.get("lifeId"), "life lifeId"),
            ): str(life.get("formId"))
            for life in (
                _object(raw, "tick-start active life")
                for raw in _array(
                    start_state.get("activeLives"),
                    "tickStart.state.activeLives",
                )
            )
            for actor_id in [_object(life.get("actorId"), "life.actorId")]
        }

        for raw_turn in _array(tick.get("actorTurns"), "tick.actorTurns"):
            turn = _object(raw_turn, "actor turn")
            actor = _object(turn.get("actorId"), "actor turn actorId")
            actor_key = _actor_key(actor, "actor")
            participant_id = _integer(
                turn.get("participantId"),
                "actor turn participantId",
            )
            actor_participant[actor_key] = participant_id
            stats = participant_stats[participant_id]
            stats["turns"] += 1
            resolution = _object(
                turn.get("actionResolution"),
                "actionResolution",
            )
            successful = resolution.get("outcome") == "success"
            stats["successfulTurns"] += int(successful)
            stats["nonSuccessfulTurns"] += int(not successful)

            observation = _object(
                turn.get("observation"),
                "actor turn observation",
            )
            enemies = [
                _object(raw, "observed enemy")
                for raw in _array(
                    observation.get("enemies"),
                    "observation.enemies",
                )
            ]
            enemies_visible = bool(enemies)
            legalities = [
                _object(raw, "action legality")
                for raw in _array(
                    observation.get("actionLegalities"),
                    "observation.actionLegalities",
                )
            ]
            attack_available = any(
                legality.get("available") is True
                and action_kinds.get(str(legality.get("actionId")))
                    == "attack"
                for legality in legalities
            )
            movement_available = any(
                legality.get("available") is True
                and action_kinds.get(str(legality.get("actionId")))
                    == "movement"
                for legality in legalities
            )
            visible_projectiles = [
                _object(raw, "visible projectile")
                for raw in _array(
                    observation.get("visibleProjectiles"),
                    "observation.visibleProjectiles",
                )
            ]
            hostile_projectiles = [
                projectile
                for projectile in visible_projectiles
                if _integer(
                    projectile.get("ownerTeamId"),
                    "visible projectile ownerTeamId",
                )
                != actor_key[0]
            ]
            hostile_projectile_visible = bool(hostile_projectiles)

            self_state = _object(
                observation.get("self"),
                "observation.self",
            )
            self_position = _position(
                self_state.get("position"),
                "observation.self.position",
            )
            attack_profile_id = form_attack_profile.get(
                str(self_state.get("formId"))
            )
            attack_profile = (
                None
                if attack_profile_id is None
                else attack_profiles.get(attack_profile_id)
            )
            direct_attack_opportunity = False
            if attack_available and attack_profile is not None:
                projectile = _object(
                    attack_profile.get("projectile"),
                    "attack profile.projectile",
                )
                maximum_range = _integer(
                    projectile.get("maxTravelTiles"),
                    "projectile.maxTravelTiles",
                )
                omnidirectional = (
                    attack_profile.get("omnidirectionalAim") is True
                )
                shot_program = _object(
                    attack_profile.get("shotProgram"),
                    "attack profile.shotProgram",
                )
                initial_aim = (
                    max(
                        abs(
                            _integer(
                                shot_program.get("minInitialAimSteps"),
                                "shotProgram.minInitialAimSteps",
                            )
                        ),
                        abs(
                            _integer(
                                shot_program.get("maxInitialAimSteps"),
                                "shotProgram.maxInitialAimSteps",
                            )
                        ),
                    )
                    if shot_program.get("enabled") is True
                    else 0
                )
                facing_sector = HEADING_SECTORS.get(
                    str(self_state.get("facing"))
                )
                for enemy in enemies:
                    ray = _ray_sector(
                        self_position,
                        _position(
                            enemy.get("position"),
                            "enemy.position",
                        ),
                    )
                    if (
                        ray is not None
                        and ray[1] <= maximum_range
                        and (
                            omnidirectional
                            or facing_sector is not None
                            and _sector_delta(ray[0], facing_sector)
                                <= initial_aim
                        )
                    ):
                        direct_attack_opportunity = True
                        break

            imminent_projectile_threat_count = 0
            for projectile in hostile_projectiles:
                ray = _ray_sector(
                    _position(
                        projectile.get("position"),
                        "visible projectile.position",
                    ),
                    self_position,
                )
                heading_sector = HEADING_SECTORS.get(
                    str(projectile.get("heading"))
                )
                if (
                    ray is not None
                    and heading_sector == ray[0]
                    and ray[1]
                        <= min(
                            _integer(
                                projectile.get("tilesPerAdvance"),
                                "projectile.tilesPerAdvance",
                            ),
                            _integer(
                                projectile.get("remainingTiles"),
                                "projectile.remainingTiles",
                            ),
                        )
                ):
                    imminent_projectile_threat_count += 1
            imminent_projectile_threat = (
                imminent_projectile_threat_count > 0
            )
            on_active_objective = (
                self_position in start_objective_tiles
            )
            stats["enemyVisibleTurns"] += int(enemies_visible)
            stats["enemyVisibleAttackAvailableTurns"] += int(
                enemies_visible and attack_available
            )
            stats["hostileProjectileVisibleTurns"] += int(
                hostile_projectile_visible
            )
            stats["hostileProjectileMovementAvailableTurns"] += int(
                hostile_projectile_visible and movement_available
            )
            stats["directAttackOpportunityTurns"] += int(
                direct_attack_opportunity
            )
            stats["imminentProjectileThreatTurns"] += int(
                imminent_projectile_threat
            )
            stats["imminentThreatMovementAvailableTurns"] += int(
                imminent_projectile_threat and movement_available
            )
            stats["multiImminentProjectileThreatTurns"] += int(
                imminent_projectile_threat_count > 1
            )
            stats["imminentThreatDirectAttackOpportunityTurns"] += int(
                imminent_projectile_threat and direct_attack_opportunity
            )
            stats["imminentThreatOnObjectiveTurns"] += int(
                imminent_projectile_threat and on_active_objective
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
            stats[f"{family}Decisions"] += 1
            stats["attackOpportunityUses"] += int(
                enemies_visible
                and attack_available
                and family == "attack"
            )
            stats["directAttackOpportunityUses"] += int(
                direct_attack_opportunity and family == "attack"
            )
            stats["projectileMovementResponses"] += int(
                hostile_projectile_visible
                and movement_available
                and family == "movement"
            )
            stats["imminentThreatMovementResponses"] += int(
                imminent_projectile_threat
                and movement_available
                and family == "movement"
            )
            stats[
                "imminentThreatMovementInsteadOfDirectAttackResponses"
            ] += int(
                imminent_projectile_threat
                and direct_attack_opportunity
                and family == "movement"
            )
            stats["imminentThreatOnObjectiveMovementResponses"] += int(
                imminent_projectile_threat
                and on_active_objective
                and family == "movement"
            )
            stats["imminentThreatOnObjectiveHoldResponses"] += int(
                imminent_projectile_threat
                and on_active_objective
                and family != "movement"
            )
            submitted_actions[action_id] += 1
            submitted_families[family] += 1
            actions_by_actor[actor_key].append(family)
            if successful:
                successful_actions[action_id] += 1
            source_form_id = start_form_by_actor.get(actor_key, "")
            target_form_id = next(
                (
                    str(argument.get("formId"))
                    for raw_argument in _array(
                        decision.get("arguments"),
                        "submittedDecision.arguments",
                    )
                    for argument in [
                        _object(raw_argument, "decision argument")
                    ]
                    if argument.get("kind") == "form-target"
                ),
                None,
            )
            mechanic = (
                route_mechanics.get(
                    (source_form_id, action_id, target_form_id)
                )
                if target_form_id is not None
                else None
            )
            if mechanic is None:
                candidates = source_action_mechanics.get(
                    (source_form_id, action_id),
                    set(),
                )
                if len(candidates) == 1:
                    mechanic = next(iter(candidates))
            if mechanic is None:
                candidates = action_mechanics.get(action_id, set())
                if len(candidates) == 1:
                    mechanic = next(iter(candidates))
            if mechanic is not None:
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
        objective_weight_by_team: Counter[int] = Counter()
        global_weight_by_team: Counter[int] = Counter()
        for life in active_lives:
            weight = form_weights.get(str(life.get("formId")), 0)
            if weight <= 0:
                continue
            team_id = _integer(
                _object(life.get("actorId"), "life.actorId").get("teamId"),
                "life teamId",
            )
            global_weight_by_team[team_id] += weight
            position = _object(life.get("position"), "life.position")
            tile = (
                _integer(position.get("x"), "life.position.x"),
                _integer(position.get("y"), "life.position.y"),
            )
            if tile in start_objective_tiles:
                objective_weight_by_team[team_id] += weight
        occupying_teams = set(objective_weight_by_team)
        contested = len(occupying_teams) > 1
        sole = len(occupying_teams) == 1
        objective_contested_ticks += int(contested)
        objective_sole_ticks += int(sole)
        objective_empty_ticks += int(not occupying_teams)
        if contested:
            objective_weights = [
                objective_weight_by_team[team_id]
                for team_id in topology_team_ids
            ]
            if len(set(objective_weights)) == 1:
                objective_equal_contest_ticks += 1
            else:
                objective_surplus_contest_ticks += 1
        global_weights = [
            global_weight_by_team[team_id]
            for team_id in topology_team_ids
        ]
        if len(set(global_weights)) > 1:
            global_weight_surplus_ticks += 1
            advantaged_team = topology_team_ids[
                global_weights.index(max(global_weights))
            ]
            if objective_weight_by_team[advantaged_team] > max(
                objective_weight_by_team[team_id]
                for team_id in topology_team_ids
                if team_id != advantaged_team
            ):
                global_surplus_realized_on_objective_ticks += 1
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
        no_combat_event_flags.append(not combat_event)
        if meaningful and first_meaningful_tick is None:
            first_meaningful_tick = tick_number
        if combat_event and first_combat_tick is None:
            first_combat_tick = tick_number

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

        turret_teams = {
            _integer(
                _object(life.get("actorId"), "life.actorId").get("teamId"),
                "life teamId",
            )
            for life in active_lives
            if str(life.get("formId")) in fortified_forms
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
    no_combat_event_runs = _run_lengths(no_combat_event_flags)
    turret_deadlock_runs = _run_lengths(both_turret_no_progress_flags)
    push_reversals = sum(
        left != right
        for left, right in zip(push_directions, push_directions[1:])
    )
    mechanic_rows = {}
    for public_name in mechanic_actions:
        actor_ticks = sum(
            form_actor_ticks[form_id]
            for form_id in target_forms.get(public_name, set())
        )
        mechanic_rows[public_name] = {
            "attempts": mechanic_attempts[public_name],
            "successfulActions": mechanic_successes[public_name],
            "completions": completion_counts[public_name],
            "cancellations": cancellation_counts[public_name],
            "targetFormActorTicks": actor_ticks,
            "targetFormIds": sorted(
                target_forms.get(public_name, set())
            ),
        }

    for slot_key, ready_at_tick in ready_episode_start.items():
        participant_id = slot_participant[slot_key]
        participant_stats[participant_id]["terminalReadyEpisodes"] += 1
        participant_stats[participant_id]["terminalReadySlotTicks"] += (
            duration_ticks - ready_at_tick
        )

    provenance_by_id = {
        participant["participantId"]: participant
        for participant in provenance
    }
    participant_rows = []
    for participant_id in sorted(participant_team):
        stats = participant_stats[participant_id]
        latencies = ready_latencies[participant_id]
        participant = provenance_by_id[participant_id]
        phase_rows = {
            phase: {
                "ticks": phase_ticks[phase],
                "bodyTicks": phase_body_ticks[participant_id][phase],
                "averageBodies": _fraction(
                    phase_body_ticks[participant_id][phase],
                    phase_ticks[phase],
                ),
            }
            for phase in sorted(
                phase_ticks,
                key=lambda label: (
                    0
                    if label == "before-first-unlock"
                    else int(label.rsplit("-", 1)[-1])
                ),
            )
        }
        participant_rows.append(
            {
                "participantId": participant_id,
                "teamId": participant_team[participant_id],
                "name": participant.get("name"),
                "artifactHash": participant.get("artifactHash"),
                "turns": stats["turns"],
                "successfulTurns": stats["successfulTurns"],
                "nonSuccessfulTurns": stats["nonSuccessfulTurns"],
                "nonSuccessfulTurnShare": _fraction(
                    stats["nonSuccessfulTurns"],
                    stats["turns"],
                ),
                "population": {
                    "stableSlots": slot_counts[participant_id],
                    "bodyTicks": stats["bodyTicks"],
                    "averageBodies": _fraction(
                        stats["bodyTicks"],
                        duration_ticks,
                    ),
                    "peakBodies": stats["peakBodies"],
                    "eligibleSlotTicks": stats["eligibleSlotTicks"],
                    "activeEligibleSlotShare": _fraction(
                        stats["bodyTicks"],
                        stats["eligibleSlotTicks"],
                    ),
                    "fullEligiblePopulationTicks": stats[
                        "fullEligiblePopulationTicks"
                    ],
                    "readySlotTicks": stats["readySlotTicks"],
                    "readyEligibleSlotShare": _fraction(
                        stats["readySlotTicks"],
                        stats["eligibleSlotTicks"],
                    ),
                    "readyToActiveLatencyTicks": latencies,
                    "readyToActiveMedianTicks": (
                        statistics.median(latencies)
                        if latencies
                        else None
                    ),
                    "readyToActiveP90Ticks": _nearest_rank(
                        latencies,
                        0.90,
                    ),
                    "terminalReadyEpisodes": stats[
                        "terminalReadyEpisodes"
                    ],
                    "terminalReadySlotTicks": stats[
                        "terminalReadySlotTicks"
                    ],
                    "phases": phase_rows,
                },
                "combatPolicy": {
                    "attackDecisions": stats["attackDecisions"],
                    "attacksLaunched": stats["attacksLaunched"],
                    "damageEvents": stats["damageEvents"],
                    "damageAmount": stats["damageAmount"],
                    "launchedAttackDamageConversion": _fraction(
                        stats["damageEvents"],
                        stats["attacksLaunched"],
                    ),
                    "enemyVisibleTurns": stats["enemyVisibleTurns"],
                    "enemyVisibleAttackAvailableTurns": stats[
                        "enemyVisibleAttackAvailableTurns"
                    ],
                    "attackOpportunityUses": stats[
                        "attackOpportunityUses"
                    ],
                    "attackOpportunityUseShare": _fraction(
                        stats["attackOpportunityUses"],
                        stats["enemyVisibleAttackAvailableTurns"],
                    ),
                    "directAttackOpportunityTurns": stats[
                        "directAttackOpportunityTurns"
                    ],
                    "directAttackOpportunityUses": stats[
                        "directAttackOpportunityUses"
                    ],
                    "directAttackOpportunityUseShare": _fraction(
                        stats["directAttackOpportunityUses"],
                        stats["directAttackOpportunityTurns"],
                    ),
                    "hostileProjectileVisibleTurns": stats[
                        "hostileProjectileVisibleTurns"
                    ],
                    "hostileProjectileMovementAvailableTurns": stats[
                        "hostileProjectileMovementAvailableTurns"
                    ],
                    "projectileMovementResponses": stats[
                        "projectileMovementResponses"
                    ],
                    "projectileMovementResponseShare": _fraction(
                        stats["projectileMovementResponses"],
                        stats[
                            "hostileProjectileMovementAvailableTurns"
                        ],
                    ),
                    "imminentProjectileThreatTurns": stats[
                        "imminentProjectileThreatTurns"
                    ],
                    "imminentThreatMovementAvailableTurns": stats[
                        "imminentThreatMovementAvailableTurns"
                    ],
                    "imminentThreatMovementResponses": stats[
                        "imminentThreatMovementResponses"
                    ],
                    "imminentThreatMovementResponseShare": _fraction(
                        stats["imminentThreatMovementResponses"],
                        stats[
                            "imminentThreatMovementAvailableTurns"
                        ],
                    ),
                    "multiImminentProjectileThreatTurns": stats[
                        "multiImminentProjectileThreatTurns"
                    ],
                    "imminentThreatDirectAttackOpportunityTurns": stats[
                        "imminentThreatDirectAttackOpportunityTurns"
                    ],
                    "imminentThreatMovementInsteadOfDirectAttackResponses":
                        stats[
                            "imminentThreatMovementInsteadOfDirectAttackResponses"
                        ],
                    "imminentThreatOnObjectiveTurns": stats[
                        "imminentThreatOnObjectiveTurns"
                    ],
                    "imminentThreatOnObjectiveMovementResponses": stats[
                        "imminentThreatOnObjectiveMovementResponses"
                    ],
                    "imminentThreatOnObjectiveHoldResponses": stats[
                        "imminentThreatOnObjectiveHoldResponses"
                    ],
                },
                "objectiveMovement": {
                    "toward": stats["towardObjectiveMoves"],
                    "lateral": stats["lateralObjectiveMoves"],
                    "away": stats["awayFromObjectiveMoves"],
                },
            }
        )

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
            "completionPhase": _phase_label(end_tick, unlock_ticks),
            "endTick": end_tick,
            "winnerTeamId": winner_team_id,
            "draw": winner_team_id is None,
            "teams": team_standings,
        },
        "duration": {
            "ticks": duration_ticks,
            "seconds": duration_ticks / PRESENTATION_TICKS_PER_SECOND,
        },
        "phaseBoundaries": {
            "unlockTicks": unlock_ticks,
            "labels": [
                _phase_label(boundary, unlock_ticks)
                for boundary in phase_boundaries
            ],
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
        "participants": participant_rows,
        "combat": {
            "attacks": event_counts["attack"],
            "attacksPer100Ticks": _fraction(
                event_counts["attack"] * 100,
                duration_ticks,
            ),
            "damageEvents": len(damage_events),
            "damageAmount": sum(
                _integer(payload.get("amount"), "damage.amount")
                for payload in damage_events
            ),
            "damagePer100Ticks": _fraction(
                sum(
                    _integer(payload.get("amount"), "damage.amount")
                    for payload in damage_events
                )
                * 100,
                duration_ticks,
            ),
            "damageTicks": len(damage_ticks),
            "destructions": event_counts["destruction"],
            "damagingTeams": sorted(damage_by_source_team),
            "reciprocalDamage": len(damage_by_source_team) >= 2,
        },
        "objective": {
            "contestedTicks": objective_contested_ticks,
            "soleControlTicks": objective_sole_ticks,
            "emptyTicks": objective_empty_ticks,
            "equalWeightContestedTicks": objective_equal_contest_ticks,
            "surplusWeightContestedTicks": objective_surplus_contest_ticks,
            "globalWeightSurplusTicks": global_weight_surplus_ticks,
            "globalSurplusRealizedOnObjectiveTicks":
                global_surplus_realized_on_objective_ticks,
            "contestedToSoleTransitions": objective_evictions,
            "pushes": pushes,
            "pushDirectionReversals": push_reversals,
            "scoreLeadChanges": score_lead_changes,
        },
        "activity": {
            "firstMeaningfulInteractionTick": first_meaningful_tick,
            "firstCombatEventTick": first_combat_tick,
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
            "longestCombatEventFreeRunTicks": max(
                no_combat_event_runs,
                default=0,
            ),
            "longestBothTurretNoProgressRunTicks": max(
                turret_deadlock_runs,
                default=0,
            ),
        },
    }


def _summarize_entrants(
    rows: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    buckets: dict[tuple[Any, Any], dict[str, Any]] = {}
    for row in rows:
        for participant in row["participants"]:
            key = (
                participant["artifactHash"],
                participant["name"],
            )
            bucket = buckets.setdefault(
                key,
                {
                    "name": participant["name"],
                    "artifactHash": participant["artifactHash"],
                    "appearances": 0,
                    "matchTicks": 0,
                    "turns": 0,
                    "successfulTurns": 0,
                    "nonSuccessfulTurns": 0,
                    "population": Counter(),
                    "latencies": [],
                    "combat": Counter(),
                    "movement": Counter(),
                    "phases": defaultdict(Counter),
                },
            )
            bucket["appearances"] += 1
            bucket["matchTicks"] += row["duration"]["ticks"]
            bucket["turns"] += participant["turns"]
            bucket["successfulTurns"] += participant["successfulTurns"]
            bucket["nonSuccessfulTurns"] += participant[
                "nonSuccessfulTurns"
            ]
            population = participant["population"]
            for field in (
                "bodyTicks",
                "eligibleSlotTicks",
                "fullEligiblePopulationTicks",
                "readySlotTicks",
                "terminalReadyEpisodes",
                "terminalReadySlotTicks",
            ):
                bucket["population"][field] += population[field]
            bucket["population"]["peakBodies"] = max(
                bucket["population"]["peakBodies"],
                population["peakBodies"],
            )
            bucket["population"]["stableSlots"] = max(
                bucket["population"]["stableSlots"],
                population["stableSlots"],
            )
            bucket["latencies"].extend(
                population["readyToActiveLatencyTicks"]
            )
            for phase, values in population["phases"].items():
                bucket["phases"][phase]["ticks"] += values["ticks"]
                bucket["phases"][phase]["bodyTicks"] += values[
                    "bodyTicks"
                ]
            combat = participant["combatPolicy"]
            for field in (
                "attackDecisions",
                "attacksLaunched",
                "damageEvents",
                "damageAmount",
                "enemyVisibleTurns",
                "enemyVisibleAttackAvailableTurns",
                "attackOpportunityUses",
                "directAttackOpportunityTurns",
                "directAttackOpportunityUses",
                "hostileProjectileVisibleTurns",
                "hostileProjectileMovementAvailableTurns",
                "projectileMovementResponses",
                "imminentProjectileThreatTurns",
                "imminentThreatMovementAvailableTurns",
                "imminentThreatMovementResponses",
                "multiImminentProjectileThreatTurns",
                "imminentThreatDirectAttackOpportunityTurns",
                "imminentThreatMovementInsteadOfDirectAttackResponses",
                "imminentThreatOnObjectiveTurns",
                "imminentThreatOnObjectiveMovementResponses",
                "imminentThreatOnObjectiveHoldResponses",
            ):
                bucket["combat"][field] += combat[field]
            for field, value in participant["objectiveMovement"].items():
                bucket["movement"][field] += value

    entrants = []
    for bucket in sorted(
        buckets.values(),
        key=lambda value: (
            str(value["name"]),
            str(value["artifactHash"]),
        ),
    ):
        population = bucket["population"]
        combat = bucket["combat"]
        latencies = bucket["latencies"]
        entrants.append(
            {
                "name": bucket["name"],
                "artifactHash": bucket["artifactHash"],
                "appearances": bucket["appearances"],
                "turns": bucket["turns"],
                "nonSuccessfulTurns": bucket["nonSuccessfulTurns"],
                "nonSuccessfulTurnShare": _fraction(
                    bucket["nonSuccessfulTurns"],
                    bucket["turns"],
                ),
                "population": {
                    "stableSlots": population["stableSlots"],
                    "bodyTicks": population["bodyTicks"],
                    "averageBodies": _fraction(
                        population["bodyTicks"],
                        bucket["matchTicks"],
                    ),
                    "peakBodies": population["peakBodies"],
                    "eligibleSlotTicks": population[
                        "eligibleSlotTicks"
                    ],
                    "activeEligibleSlotShare": _fraction(
                        population["bodyTicks"],
                        population["eligibleSlotTicks"],
                    ),
                    "fullEligiblePopulationTicks": population[
                        "fullEligiblePopulationTicks"
                    ],
                    "readySlotTicks": population["readySlotTicks"],
                    "readyEligibleSlotShare": _fraction(
                        population["readySlotTicks"],
                        population["eligibleSlotTicks"],
                    ),
                    "readyToActiveEpisodes": len(latencies),
                    "readyToActiveMedianTicks": (
                        statistics.median(latencies)
                        if latencies
                        else None
                    ),
                    "readyToActiveP90Ticks": _nearest_rank(
                        latencies,
                        0.90,
                    ),
                    "terminalReadyEpisodes": population[
                        "terminalReadyEpisodes"
                    ],
                    "terminalReadySlotTicks": population[
                        "terminalReadySlotTicks"
                    ],
                    "phases": {
                        phase: {
                            "ticks": values["ticks"],
                            "bodyTicks": values["bodyTicks"],
                            "averageBodies": _fraction(
                                values["bodyTicks"],
                                values["ticks"],
                            ),
                        }
                        for phase, values in sorted(
                            bucket["phases"].items()
                        )
                    },
                },
                "combatPolicy": {
                    **dict(combat),
                    "launchedAttackDamageConversion": _fraction(
                        combat["damageEvents"],
                        combat["attacksLaunched"],
                    ),
                    "attackOpportunityUseShare": _fraction(
                        combat["attackOpportunityUses"],
                        combat["enemyVisibleAttackAvailableTurns"],
                    ),
                    "directAttackOpportunityUseShare": _fraction(
                        combat["directAttackOpportunityUses"],
                        combat["directAttackOpportunityTurns"],
                    ),
                    "projectileMovementResponseShare": _fraction(
                        combat["projectileMovementResponses"],
                        combat[
                            "hostileProjectileMovementAvailableTurns"
                        ],
                    ),
                    "imminentThreatMovementResponseShare": _fraction(
                        combat["imminentThreatMovementResponses"],
                        combat[
                            "imminentThreatMovementAvailableTurns"
                        ],
                    ),
                },
                "objectiveMovement": dict(bucket["movement"]),
            }
        )
    return entrants


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
    completion_phases = Counter(
        str(row["result"]["completionPhase"]) for row in rows
    )
    mechanics = {}
    mechanic_names = sorted(
        {
            mechanic
            for row in rows
            for mechanic in row["mechanics"]
        },
        key=lambda value: (
            ["fabrication", "split", "anchor", "mobilize"].index(value)
            if value in {"fabrication", "split", "anchor", "mobilize"}
            else 4,
            value,
        ),
    )
    for mechanic in mechanic_names:
        mechanics[mechanic] = {
            key: sum(
                row["mechanics"].get(mechanic, {}).get(key, 0)
                for row in rows
            )
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
    first_combat_events = [
        row["activity"]["firstCombatEventTick"]
        for row in rows
        if row["activity"]["firstCombatEventTick"] is not None
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
        "completionPhases": dict(sorted(completion_phases.items())),
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
        "entrants": _summarize_entrants(rows),
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
            "attacksPer100Ticks": _fraction(
                sum(row["combat"]["attacks"] for row in rows) * 100,
                total_ticks,
            ),
            "damageAmount": sum(
                row["combat"]["damageAmount"] for row in rows
            ),
            "damagePer100Ticks": _fraction(
                sum(row["combat"]["damageAmount"] for row in rows) * 100,
                total_ticks,
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
                "emptyTicks",
                "equalWeightContestedTicks",
                "surplusWeightContestedTicks",
                "globalWeightSurplusTicks",
                "globalSurplusRealizedOnObjectiveTicks",
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
            "medianFirstCombatEventTick": (
                statistics.median(first_combat_events)
                if first_combat_events
                else None
            ),
            "gamesWithoutCombatEvents": (
                len(rows) - len(first_combat_events)
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
            "maxCombatEventFreeRunTicks": max(
                row["activity"]["longestCombatEventFreeRunTicks"]
                for row in rows
            ),
            "gamesWithCombatEventFreeRunAtLeast75": sum(
                row["activity"]["longestCombatEventFreeRunTicks"] >= 75
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
        objective = group["objective"]
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
            f"attacks/100t={combat['attacksPer100Ticks']:.1f} "
            f"damage/100t={combat['damagePer100Ticks']:.1f} "
            f"damage={combat['damageAmount']} "
            f"destructions={combat['destructions']}"
        )
        print(
            "  mechanics "
            + " ".join(
                f"{mechanic}={values['completions']}"
                for mechanic, values in mechanics.items()
            )
        )
        print(
            "  objective "
            f"phases={group['completionPhases']} "
            f"empty={objective['emptyTicks']} "
            f"sole={objective['soleControlTicks']} "
            f"equal-contest={objective['equalWeightContestedTicks']} "
            f"surplus-contest={objective['surplusWeightContestedTicks']} "
            f"global-surplus-realized="
            f"{objective['globalSurplusRealizedOnObjectiveTicks']}/"
            f"{objective['globalWeightSurplusTicks']}"
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
        for entrant in group["entrants"]:
            policy = entrant["combatPolicy"]
            population = entrant["population"]
            print(
                f"  entrant   {entrant['name']} "
                f"bodies={population['averageBodies']:.2f} "
                f"eligible-active="
                f"{population['activeEligibleSlotShare']:.1%} "
                f"ready={population['readyEligibleSlotShare']:.1%} "
                f"direct-shot-use="
                f"{policy['directAttackOpportunityUseShare']:.1%} "
                f"imminent-threat-move="
                f"{policy['imminentThreatMovementResponseShare']:.1%}"
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
