#!/usr/bin/env python3
"""Describe complete generic Frontline replay-v3 tournament evidence.

The report intentionally has no composite "fun" score and makes no balance
verdict. It exposes outcomes, action/form use, combat, territorial movement,
faults, inactivity, and turret deadlocks for a frozen cohort. Causal claims
still require paired rules arms; product claims still require independently
authored doctrines and outcome-blind replay review.

`--dynamics` adds the match-dynamics ("pendulum" / dullness) family defined by
`docs/DESIGN-FORENSICS-DYNAMICS-2026-07-29.md`, so the pre-registered S1-S5 /
N1-N5 pass-fail gates in that document can be evaluated mechanically instead of
by hand. Every definition below is that document's, reproduced exactly:

1. Leader extends - over every frontline position transition, the probability
   that the currently leading side advances further. A breach is counted as the
   final, uncensored advance in the winner's advance direction, so decisive
   matches are not censored out of the transition matrix. Pooled over all
   non-centre positions and broken out by signed displacement from centre.
2. Reinforcement transit gradient - ticks from a life's spawn until its first
   tick standing on the then-active objective, bucketed by the spawning team's
   territorial lead (position displacement in that team's advance direction).
   The spread between bucket medians is the rubber band (S2's gate).
3. Sole-presence efficiency - sole-presence ticks split into productive (the
   run closed a capture) and wasted, the share of earned capture progress
   destroyed by decay, and the sole-run length distribution against the
   capture threshold read from the replay's own contract.
4. Frozen scoreboard and limit cycles - the longest run with unchanged
   (frontline position, capture progress), and the longest exact repeat of the
   whole visible state (bodies, forms, facings, healths, position, progress) at
   any period up to 16 ticks.
5. Close-contact-no-damage - the share of ticks where opposing bodies stand
   within 3 tiles Chebyshev and no damage event occurs: the staring contest.
6. Reversal metrics - advances per match, reversal rate, tug efficiency
   (|net displacement| / advances) and the final-displacement distribution.
7. Positional entropy - distinct tiles over floor tiles, effective tiles
   (2^entropy of the body-tick tile distribution), dwell fraction and lag-k
   position autocorrelation with its argmax lag, per stable-slot trace.

The gate block transcribes the document's thresholds; two of them ("draws
~0", "reversal rate stays ~0.65") are prose in the source and are
operationalized here by explicitly named constants.
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
METRIC_DEFINITIONS_VERSION = "generic-frontline-replay-v3-7"
DYNAMICS_DEFINITIONS_VERSION = "pendulum-dynamics-1"
PRESENTATION_TICKS_PER_SECOND = 5
STALL_TICKS = 20
RECENT_FRAME_WINDOW = 20
WALL_TILE = "#"
# Match-dynamics parameters. These are the forensics document's, not tuning
# knobs: changing one invalidates comparison with its published baseline.
# Chebyshev matches the engine's eight-way movement. The forensics pipeline
# measured proximity with Manhattan distance; the damage-free share of
# close-contact ticks is the same to three decimals either way (0.828 vs
# 0.826), while the sustained-stare share is not (0.268 vs the document's
# 0.221), so only the former is a reconciliation target.
CLOSE_CONTACT_TILES = 3
CLOSE_CONTACT_DISTANCE = "chebyshev"
SUSTAINED_STARE_TICKS = 10
FROZEN_SCOREBOARD_TICKS = 50
LIMIT_CYCLE_TICKS = 50
LIMIT_CYCLE_MAX_PERIOD = 16
POSITION_TRACE_MIN_TICKS = 40
POSITION_AUTOCORRELATION_LAGS = (2, 41)
SCORE_SIGN_PROBE_TICKS = (100, 200, 300, 400)
# The document writes "draws 9.3%->~0" and "if reversal rate stays ~0.65";
# both gates need a number, so the prose is operationalized exactly here.
# "Stays ~0.65" is read as the per-match reversal-rate median remaining inside
# 0.65 +/- 0.05 (the pendulum corpus sits at 0.625), so N1 only passes when a
# numbers-only arm pulls that median below 0.60.
NEAR_ZERO_DRAW_RATE = 0.01
PENDULUM_REVERSAL_RATE = 0.65
PENDULUM_REVERSAL_TOLERANCE = 0.05
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


def _quantile(values: list[float], percentile: float) -> float | None:
    """Nearest-rank percentile over an unsorted float sample."""

    if not values:
        return None
    ordered = sorted(values)
    return ordered[max(0, math.ceil(percentile * len(ordered)) - 1)]


def _median(values: list[float]) -> float | None:
    return statistics.median(values) if values else None


def _mean(values: list[float]) -> float | None:
    return statistics.fmean(values) if values else None


def _histogram(counts: Counter[int]) -> dict[str, int]:
    """Serialize an integer-keyed histogram with deterministic key order."""

    return {str(key): counts[key] for key in sorted(counts)}


def _histogram_values(histogram: dict[str, int]) -> list[int]:
    return [
        int(key)
        for key, count in sorted(histogram.items(), key=lambda kv: int(kv[0]))
        for _ in range(count)
    ]


def _merge_histograms(
    histograms: Iterable[dict[str, int]],
) -> dict[str, int]:
    merged: Counter[int] = Counter()
    for histogram in histograms:
        for key, count in histogram.items():
            merged[int(key)] += count
    return _histogram(merged)


def _histogram_share_at_least(
    histogram: dict[str, int],
    threshold: int,
) -> float:
    total = sum(histogram.values())
    return _fraction(
        sum(
            count
            for key, count in histogram.items()
            if int(key) >= threshold
        ),
        total,
    )


def _entropy_bits(counts: Counter[tuple[int, int]]) -> float:
    total = sum(counts.values())
    if total == 0:
        return 0.0
    return -sum(
        (count / total) * math.log2(count / total)
        for count in counts.values()
        if count
    )


def _longest_repeat_run(
    signatures: list[Any],
    period: int,
) -> int:
    """Longest run of consecutive ticks that repeat the tick `period` back."""

    longest = 0
    run = 0
    for index in range(period, len(signatures)):
        if signatures[index] == signatures[index - period]:
            run += 1
            longest = max(longest, run)
        else:
            run = 0
    return longest


def _terminal_repeat_run(
    signatures: list[Any],
    period: int,
) -> int:
    """Length of the repeat run the replay ends inside, for this period."""

    length = 0
    index = len(signatures) - 1
    while index - period >= 0 and signatures[index] == signatures[
        index - period
    ]:
        length += 1
        index -= 1
    return length


def _resolve_metric_path(value: Any, path: str) -> Any:
    """Read a dotted path; numeric segments index into arrays."""

    current = value
    for segment in path.split("."):
        if isinstance(current, list):
            if not segment.lstrip("-").isdigit():
                raise ValueError(f"'{path}' indexes an array with '{segment}'")
            index = int(segment)
            if index >= len(current) or index < -len(current):
                raise ValueError(f"'{path}' is out of range at '{segment}'")
            current = current[index]
            continue
        if not isinstance(current, dict) or segment not in current:
            raise ValueError(f"'{path}' is missing at '{segment}'")
        current = current[segment]
    return current


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


def _attack_trajectory(action: Any) -> str:
    value = _object(action, "attack action")
    for raw_argument in _array(
        value.get("arguments"),
        "attack action.arguments",
    ):
        argument = _object(raw_argument, "attack action argument")
        if argument.get("kind") != "shot-program":
            continue
        program = _object(
            argument.get("value"),
            "attack shot-program.value",
        )
        return (
            "curved"
            if _integer(
                program.get("bendCount"),
                "attack shot-program.bendCount",
            )
            > 0
            else "straight"
        )
    return "straight"


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


def _body_row(life: dict[str, Any]) -> tuple[Any, ...]:
    """Reduce one active life to the visible state the dynamics pass reads."""

    actor = _object(life.get("actorId"), "active life actorId")
    position = _object(life.get("position"), "active life position")
    return (
        _integer(actor.get("teamId"), "active life teamId"),
        _integer(actor.get("unitId"), "active life unitId"),
        _integer(actor.get("lifeId"), "active life lifeId"),
        str(life.get("formId")),
        _integer(position.get("x"), "active life position.x"),
        _integer(position.get("y"), "active life position.y"),
        str(life.get("facing")),
        _integer(life.get("health"), "active life health"),
    )


def _advance_direction(delta: int) -> int:
    return 1 if delta > 0 else -1


def _control_class(
    entry: dict[str, Any],
    objective_tiles: list[set[tuple[int, int]]],
    form_weights: dict[str, int],
) -> str:
    """Objective control at one tick: paused / empty / contested / sole-N.

    Only forms with positive objective weight count, so an anchored turret
    standing on the objective neither claims nor contests it.
    """

    if entry["tick"] < entry["controlResumesAtTick"]:
        return "paused"
    index = entry["positionIndex"]
    tiles = (
        objective_tiles[index]
        if 0 <= index < len(objective_tiles)
        else set()
    )
    teams = {
        body[0]
        for body in entry["bodies"]
        if form_weights.get(body[3], 0) > 0 and (body[4], body[5]) in tiles
    }
    if not teams:
        return "empty"
    if len(teams) > 1:
        return "contested"
    return f"sole-{next(iter(teams))}"


def _position_trace_metrics(
    positions: list[tuple[int, int]],
) -> dict[str, Any]:
    """Repetition metrics for one stable slot's concatenated body trace."""

    ticks = len(positions)
    dwell = sum(
        1
        for index in range(1, ticks)
        if positions[index] == positions[index - 1]
    )
    autocorrelation: dict[int, float] = {}
    for lag in range(*POSITION_AUTOCORRELATION_LAGS):
        span = ticks - lag
        if span < 1:
            break
        autocorrelation[lag] = _fraction(
            sum(
                1
                for index in range(span)
                if positions[index] == positions[index + lag]
            ),
            span,
        )
    best_lag = None
    best_value = 0.0
    for lag in sorted(autocorrelation):
        if autocorrelation[lag] > best_value:
            best_lag = lag
            best_value = autocorrelation[lag]
    return {
        "ticks": ticks,
        "distinctTiles": len(set(positions)),
        "tileRatio": _fraction(len(set(positions)), ticks),
        "dwellFraction": _fraction(dwell, ticks - 1),
        "argmaxLag": best_lag,
        "argmaxAutocorrelation": best_value,
        "lag2Autocorrelation": autocorrelation.get(2, 0.0),
    }


def _dynamics_metrics(
    trace: list[dict[str, Any]],
    *,
    objective_tiles: list[set[tuple[int, int]]],
    form_weights: dict[str, int],
    advance_deltas: dict[int, int],
    centre_index: int,
    capture_threshold: int,
    floor_tiles: int,
    completion_reason: str,
    winner_team_id: int | None,
    final_signed_score: int,
) -> dict[str, Any]:
    """Derive one match's pendulum/dullness row from its per-tick trace."""

    ticks = len(trace)
    if ticks == 0:
        raise ValueError("dynamics require at least one tick")
    positive_teams = sorted(
        team_id for team_id, delta in advance_deltas.items() if delta > 0
    )
    if len(positive_teams) != 1:
        raise ValueError(
            "dynamics require exactly one team advancing toward a higher "
            "objective index"
        )
    positive_team = positive_teams[0]
    indexes = [entry["positionIndex"] for entry in trace]
    progress = [entry["captureProgress"] for entry in trace]

    # ---- 1/6 frontline transitions, advances and reversals ----------------
    transitions: Counter[tuple[int, int]] = Counter()
    directions: list[int] = []
    first_advance_tick: int | None = None
    for index in range(1, ticks):
        if indexes[index] == indexes[index - 1]:
            continue
        step = 1 if indexes[index] > indexes[index - 1] else -1
        transitions[(indexes[index - 1], step)] += 1
        directions.append(step)
        if first_advance_tick is None:
            first_advance_tick = trace[index]["tick"]
    breach = completion_reason != "max-ticks" and winner_team_id is not None
    if breach:
        transitions[
            (
                indexes[-1],
                _advance_direction(advance_deltas[int(winner_team_id)]),
            )
        ] += 1
    advances = len(directions)
    reversals = sum(
        1
        for index in range(1, advances)
        if directions[index] != directions[index - 1]
    )
    position_dwell: Counter[int] = Counter()
    run_start = 0
    for index in range(1, ticks):
        if indexes[index] != indexes[index - 1]:
            position_dwell[index - run_start] += 1
            run_start = index
    position_dwell[ticks - run_start] += 1
    net_displacement = indexes[-1] - indexes[0]

    # ---- 2 reinforcement transit ------------------------------------------
    first_seen: dict[tuple[int, int, int], int] = {}
    first_on_objective: dict[tuple[int, int, int], int] = {}
    occupancy: Counter[tuple[int, int]] = Counter()
    slot_positions: dict[tuple[int, int], list[tuple[int, int]]] = defaultdict(
        list
    )
    for index, entry in enumerate(trace):
        position_index = entry["positionIndex"]
        tiles = (
            objective_tiles[position_index]
            if 0 <= position_index < len(objective_tiles)
            else set()
        )
        for body in entry["bodies"]:
            key = (body[0], body[1], body[2])
            first_seen.setdefault(key, index)
            tile = (body[4], body[5])
            if key not in first_on_objective and tile in tiles:
                first_on_objective[key] = index
            occupancy[tile] += 1
            slot_positions[(body[0], body[1])].append(tile)
    transit_by_lead: dict[int, Counter[int]] = defaultdict(Counter)
    for key, spawn_index in first_seen.items():
        if spawn_index == 0:
            continue
        delta = advance_deltas.get(key[0])
        arrival_index = first_on_objective.get(key)
        if delta is None or arrival_index is None:
            continue
        lead = (
            indexes[spawn_index] - centre_index
        ) * _advance_direction(delta)
        transit_by_lead[lead][
            trace[arrival_index]["tick"] - trace[spawn_index]["tick"]
        ] += 1

    # ---- 3 sole-presence efficiency ---------------------------------------
    control = [
        _control_class(entry, objective_tiles, form_weights)
        for entry in trace
    ]
    control_mix = Counter(
        value if not value.startswith("sole-") else "sole"
        for value in control
    )
    sole_run_lengths: Counter[int] = Counter()
    completed_run_lengths: Counter[int] = Counter()
    terminations: Counter[str] = Counter()
    incomplete_terminations: Counter[str] = Counter()
    productive_ticks = 0
    wasted_ticks = 0
    index = 0
    while index < ticks:
        if not control[index].startswith("sole-"):
            index += 1
            continue
        end = index
        while end + 1 < ticks and control[end + 1] == control[index]:
            end += 1
        length = end - index + 1
        completed = end + 1 < ticks and indexes[end + 1] != indexes[end]
        termination = control[end + 1] if end + 1 < ticks else "match-end"
        terminations[
            "sole" if termination.startswith("sole-") else termination
        ] += 1
        sole_run_lengths[length] += 1
        if completed:
            completed_run_lengths[length] += 1
            productive_ticks += length
        else:
            wasted_ticks += length
            incomplete_terminations[
                "sole" if termination.startswith("sole-") else termination
            ] += 1
        index = end + 1
    progress_gained = 0
    progress_lost = 0
    for index in range(1, ticks):
        if indexes[index] != indexes[index - 1]:
            continue  # an advance resets progress by rule, not by decay
        change = progress[index] - progress[index - 1]
        if change > 0:
            progress_gained += change
        elif change < 0:
            progress_lost -= change

    # ---- 4 frozen scoreboard and exact limit cycles ------------------------
    frozen_run = 0
    longest_frozen = 0
    for index in range(1, ticks):
        if (indexes[index], progress[index]) == (
            indexes[index - 1],
            progress[index - 1],
        ):
            frozen_run += 1
            longest_frozen = max(longest_frozen, frozen_run)
        else:
            frozen_run = 0
    # The whole visible state: which stable slot stands where, facing which
    # way, at what health and in what form, plus the scoreboard. Life ids and
    # clocks are excluded, so a respawn into the same posture still repeats.
    signatures = [
        (
            tuple(
                sorted(
                    (
                        body[0],
                        body[1],
                        body[4],
                        body[5],
                        body[6],
                        body[7],
                        body[3],
                    )
                    for body in entry["bodies"]
                )
            ),
            entry["positionIndex"],
            entry["captureProgress"],
        )
        for entry in trace
    ]
    cycle_length = 0
    cycle_period = 0
    terminal_cycle_length = 0
    terminal_cycle_period = 0
    for period in range(1, LIMIT_CYCLE_MAX_PERIOD + 1):
        longest = _longest_repeat_run(signatures, period)
        if longest > cycle_length:
            cycle_length = longest
            cycle_period = period
        terminal = _terminal_repeat_run(signatures, period)
        if terminal > terminal_cycle_length:
            terminal_cycle_length = terminal
            terminal_cycle_period = period

    # ---- 5 close contact without damage ------------------------------------
    close_ticks = 0
    close_no_damage_ticks = 0
    stare_runs: Counter[int] = Counter()
    stare_run = 0
    for entry in trace:
        by_team: dict[int, list[tuple[int, int]]] = defaultdict(list)
        for body in entry["bodies"]:
            by_team[body[0]].append((body[4], body[5]))
        team_ids = sorted(by_team)
        separation: int | None = None
        for left in range(len(team_ids)):
            for right in range(left + 1, len(team_ids)):
                for source in by_team[team_ids[left]]:
                    for target in by_team[team_ids[right]]:
                        distance = max(
                            abs(source[0] - target[0]),
                            abs(source[1] - target[1]),
                        )
                        if separation is None or distance < separation:
                            separation = distance
        close = separation is not None and separation <= CLOSE_CONTACT_TILES
        close_ticks += int(close)
        if close and not entry["damage"]:
            close_no_damage_ticks += 1
            stare_run += 1
        else:
            if stare_run:
                stare_runs[stare_run] += 1
            stare_run = 0
    if stare_run:
        stare_runs[stare_run] += 1
    sustained_stare_ticks = sum(
        length * count
        for length, count in stare_runs.items()
        if length >= SUSTAINED_STARE_TICKS
    )

    # ---- 7 positional entropy ----------------------------------------------
    entropy = _entropy_bits(occupancy)
    traces = [
        _position_trace_metrics(positions)
        for _, positions in sorted(slot_positions.items())
        if len(positions) >= POSITION_TRACE_MIN_TICKS
    ]

    # ---- signed territorial score trajectory (S4's predictiveness) ---------
    signed_scores = []
    for entry in trace:
        claiming = entry["claimingTeamId"]
        claim = 0
        if claiming is not None and int(claiming) in advance_deltas:
            claim = entry["captureProgress"] * _advance_direction(
                advance_deltas[int(claiming)]
            )
        signed_scores.append(
            (entry["positionIndex"] - centre_index) * capture_threshold + claim
        )
    signs = [1 if value > 0 else -1 for value in signed_scores if value != 0]
    lead_changes = sum(
        1
        for index in range(1, len(signs))
        if signs[index] != signs[index - 1]
    )

    return {
        "definitionsVersion": DYNAMICS_DEFINITIONS_VERSION,
        "contract": {
            "captureThreshold": capture_threshold,
            "positionCount": len(objective_tiles),
            "centreIndex": centre_index,
            "advanceDeltas": {
                str(team_id): advance_deltas[team_id]
                for team_id in sorted(advance_deltas)
            },
            "publicAdvanceTeamId": positive_team,
            "floorTiles": floor_tiles,
        },
        "frontline": {
            "transitions": {
                f"{index}|{step}": count
                for (index, step), count in sorted(transitions.items())
            },
            "breachCountedAsAdvance": breach,
            "advances": advances,
            "reversals": reversals,
            "reversalRate": (
                reversals / (advances - 1) if advances >= 2 else None
            ),
            "netDisplacement": net_displacement,
            "finalDisplacement": indexes[-1] - centre_index,
            "displacementEfficiency": (
                abs(net_displacement) / advances if advances else None
            ),
            "firstAdvanceTick": first_advance_tick,
            "positionDwellHistogram": _histogram(position_dwell),
        },
        "reinforcementTransit": {
            "byLeadTickHistogram": {
                str(lead): _histogram(transit_by_lead[lead])
                for lead in sorted(transit_by_lead)
            },
            "arrivedLives": sum(
                sum(counts.values()) for counts in transit_by_lead.values()
            ),
            "reinforcementLives": sum(
                1 for index in first_seen.values() if index != 0
            ),
        },
        "solePresence": {
            "controlMixTicks": dict(sorted(control_mix.items())),
            "productiveTicks": productive_ticks,
            "wastedTicks": wasted_ticks,
            "wastedShare": _fraction(
                wasted_ticks,
                productive_ticks + wasted_ticks,
            ),
            "runLengthHistogram": _histogram(sole_run_lengths),
            "completedRunLengthHistogram": _histogram(completed_run_lengths),
            "runsReachingThreshold": sum(
                count
                for length, count in sole_run_lengths.items()
                if length >= capture_threshold
            ),
            "runs": sum(sole_run_lengths.values()),
            "terminations": dict(sorted(terminations.items())),
            "incompleteTerminations": dict(
                sorted(incomplete_terminations.items())
            ),
            "progressGained": progress_gained,
            "progressLost": progress_lost,
            "decayDestroyedShare": _fraction(
                progress_lost,
                progress_gained,
            ),
            "captures": sum(completed_run_lengths.values()),
        },
        "staleness": {
            "longestFrozenScoreboardTicks": longest_frozen,
            "frozenScoreboard": longest_frozen >= FROZEN_SCOREBOARD_TICKS,
            "longestLimitCycleTicks": cycle_length,
            "limitCyclePeriod": cycle_period,
            "limitCycle": cycle_length >= LIMIT_CYCLE_TICKS,
            "terminalLimitCycleTicks": terminal_cycle_length,
            "terminalLimitCyclePeriod": terminal_cycle_period,
            "terminalLimitCycle": (
                terminal_cycle_length >= LIMIT_CYCLE_TICKS
            ),
        },
        "closeContact": {
            "distanceMetric": CLOSE_CONTACT_DISTANCE,
            "radiusTiles": CLOSE_CONTACT_TILES,
            "ticks": close_ticks,
            "noDamageTicks": close_no_damage_ticks,
            "noDamageShare": _fraction(close_no_damage_ticks, close_ticks),
            "sustainedStareRunTicks": SUSTAINED_STARE_TICKS,
            "sustainedStareTicks": sustained_stare_ticks,
            "sustainedStareShare": _fraction(sustained_stare_ticks, ticks),
        },
        "positional": {
            "bodyTicks": sum(occupancy.values()),
            "distinctTiles": len(occupancy),
            "floorTiles": floor_tiles,
            "coverage": _fraction(len(occupancy), floor_tiles),
            "entropyBits": entropy,
            "effectiveTiles": 2 ** entropy if occupancy else 0.0,
            "traces": traces,
        },
        "score": {
            "signedTerritorialProbes": {
                str(probe): signed_scores[min(probe - 1, ticks - 1)]
                for probe in SCORE_SIGN_PROBE_TICKS
            },
            "finalSignedTerritorial": final_signed_score,
            "leadChanges": lead_changes,
            "levelTicks": sum(1 for value in signed_scores if value == 0),
            "maxAbsoluteTerritorial": max(
                abs(value) for value in signed_scores
            ),
        },
    }


def analyze_replay(
    document: dict[str, Any],
    *,
    source: str = "",
    group: str = "",
    dynamics: bool = False,
) -> dict[str, Any]:
    """Validate and derive one generic Frontline per-match metric row.

    `dynamics` additionally derives the pendulum/dullness family; the row is
    otherwise byte-identical to the descriptive report it has always emitted.
    """

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
    first_unlock_tick = (
        unlock_ticks[0] if unlock_ticks else duration_ticks
    )
    opening_ticks = min(duration_ticks, first_unlock_tick)

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
    opening_participant_stats: dict[int, Counter[str]] = {
        participant_id: Counter()
        for participant_id in participant_team
    }
    opening_objective = Counter()
    opening_boundary_scores = _score_values(initial_state)
    ready_episode_start: dict[tuple[int, int], int] = {}
    ready_latencies: dict[int, list[int]] = defaultdict(list)
    actor_participant: dict[tuple[int, int, int], int] = {}
    projectile_participant: dict[str, int] = {}
    projectile_trajectory: dict[str, str] = {}
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

    dynamics_trace: list[dict[str, Any]] = []
    objective_tiles: list[set[tuple[int, int]]] = []
    advance_deltas: dict[int, int] = {}
    capture_threshold = 0
    centre_index = 0
    floor_tiles = 0
    if dynamics:
        objective_tiles = [
            region_tiles.get(region_id, set())
            for region_id in objective_region_ids
        ]
        capture = _object(game_mode.get("capture"), "gameMode.capture")
        capture_threshold = _integer(
            capture.get("threshold"),
            "gameMode.capture.threshold",
        )
        # The public max-tick score is
        # (activePositionIndex - positionCount / 2) * captureThreshold + claim,
        # so the neutral position is positionCount // 2.
        centre_index = _integer(
            game_mode.get("frontlinePositionCount"),
            "gameMode.frontlinePositionCount",
        ) // 2
        for raw_advance in _array(
            mode_binding.get("teamAdvances"),
            "modeMapBinding.teamAdvances",
        ):
            advance = _object(raw_advance, "team advance")
            advance_deltas[
                _integer(advance.get("teamId"), "teamAdvance.teamId")
            ] = _integer(
                advance.get("objectiveIndexDelta"),
                "teamAdvance.objectiveIndexDelta",
            )
        floor_tiles = sum(
            1
            for raw_row in _array(map_contract.get("tileRows"), "map.tileRows")
            for tile in str(raw_row)
            if tile != WALL_TILE
        )

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
        in_opening = tick_number < first_unlock_tick
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
        damage_this_tick = False
        for raw_event in tick_events:
            event = _object(raw_event, "event")
            kind = str(event.get("kind"))
            event_counts[kind] += 1
            combat_event = combat_event or kind in {"attack", "damage"}
            damage_this_tick = damage_this_tick or kind == "damage"
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
                trajectory = _attack_trajectory(payload.get("action"))
                if participant_id is not None:
                    participant_stats[participant_id]["attacksLaunched"] += 1
                    participant_stats[participant_id][
                        f"{trajectory}AttacksLaunched"
                    ] += 1
                    if in_opening:
                        opening_participant_stats[participant_id][
                            "attacksLaunched"
                        ] += 1
                        opening_participant_stats[participant_id][
                            f"{trajectory}AttacksLaunched"
                        ] += 1
                    projectile_participant[str(payload.get("projectileId"))] = (
                        participant_id
                    )
                    projectile_trajectory[
                        str(payload.get("projectileId"))
                    ] = trajectory
            elif kind == "damage":
                projectile_id = str(payload.get("projectileId"))
                participant_id = projectile_participant.get(projectile_id)
                trajectory = projectile_trajectory.get(
                    projectile_id,
                    "unknown",
                )
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
                    participant_stats[participant_id][
                        f"{trajectory}DamageEvents"
                    ] += 1
                    participant_stats[participant_id][
                        f"{trajectory}DamageAmount"
                    ] += _integer(payload.get("amount"), "damage.amount")
                    if in_opening:
                        opening_participant_stats[participant_id][
                            "damageEvents"
                        ] += 1
                        opening_participant_stats[participant_id][
                            "damageAmount"
                        ] += _integer(
                            payload.get("amount"),
                            "damage.amount",
                        )
                        opening_participant_stats[participant_id][
                            f"{trajectory}DamageEvents"
                        ] += 1
                        opening_participant_stats[participant_id][
                            f"{trajectory}DamageAmount"
                        ] += _integer(
                            payload.get("amount"),
                            "damage.amount",
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
            hostile_trajectories = {
                projectile_trajectory.get(
                    str(projectile.get("projectileId")),
                    "unknown",
                )
                for projectile in hostile_projectiles
            }
            curved_threat_visible = "curved" in hostile_trajectories

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
            stats["curvedThreatVisibleTurns"] += int(
                curved_threat_visible
            )
            stats["curvedThreatMovementAvailableTurns"] += int(
                curved_threat_visible and movement_available
            )
            stats["curvedThreatOnObjectiveTurns"] += int(
                curved_threat_visible and on_active_objective
            )
            if in_opening:
                opening_stats = opening_participant_stats[participant_id]
                opening_stats["curvedThreatVisibleTurns"] += int(
                    curved_threat_visible
                )
                opening_stats[
                    "curvedThreatMovementAvailableTurns"
                ] += int(curved_threat_visible and movement_available)
                opening_stats["curvedThreatOnObjectiveTurns"] += int(
                    curved_threat_visible and on_active_objective
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
            if in_opening:
                opening_stats = opening_participant_stats[participant_id]
                opening_stats["turns"] += 1
                opening_stats[f"{family}Decisions"] += 1
                if family == "attack":
                    shot_program = next(
                        (
                            _object(
                                argument.get("value"),
                                "shot-program.value",
                            )
                            for raw_argument in _array(
                                decision.get("arguments"),
                                "submittedDecision.arguments",
                            )
                            for argument in [
                                _object(
                                    raw_argument,
                                    "decision argument",
                                )
                            ]
                            if argument.get("kind") == "shot-program"
                        ),
                        None,
                    )
                    bend_count = (
                        0
                        if shot_program is None
                        else _integer(
                            shot_program.get("bendCount"),
                            "shot-program.bendCount",
                        )
                    )
                    opening_stats[
                        "curvedAttackDecisions"
                        if bend_count > 0
                        else "straightAttackDecisions"
                    ] += 1
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
            stats["curvedThreatMovementResponses"] += int(
                curved_threat_visible
                and movement_available
                and family == "movement"
            )
            stats[
                "curvedThreatOnObjectiveMovementResponses"
            ] += int(
                curved_threat_visible
                and on_active_objective
                and family == "movement"
            )
            stats["curvedThreatOnObjectiveHoldResponses"] += int(
                curved_threat_visible
                and on_active_objective
                and family != "movement"
            )
            if in_opening:
                opening_stats["curvedThreatMovementResponses"] += int(
                    curved_threat_visible
                    and movement_available
                    and family == "movement"
                )
                opening_stats[
                    "curvedThreatOnObjectiveMovementResponses"
                ] += int(
                    curved_threat_visible
                    and on_active_objective
                    and family == "movement"
                )
                opening_stats[
                    "curvedThreatOnObjectiveHoldResponses"
                ] += int(
                    curved_threat_visible
                    and on_active_objective
                    and family != "movement"
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
        if dynamics:
            dynamics_trace.append(
                {
                    "tick": tick_number,
                    "positionIndex": position_index,
                    "claimingTeamId": mode.get("claimingTeamId"),
                    "captureProgress": _integer(
                        mode.get("captureProgress"),
                        "mode.captureProgress",
                    ),
                    "controlResumesAtTick": _integer(
                        mode.get("controlResumesAtTick") or 0,
                        "mode.controlResumesAtTick",
                    ),
                    "damage": damage_this_tick,
                    "bodies": tuple(
                        _body_row(life) for life in active_lives
                    ),
                }
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
        if in_opening:
            opening_objective["contestedTicks"] += int(contested)
            opening_objective["soleControlTicks"] += int(sole)
            opening_objective["emptyTicks"] += int(not occupying_teams)
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
            if in_opening:
                opening_objective["pushes"] += abs(
                    position_index - start_index
                )

        scores = _score_values(post_state)
        if in_opening:
            opening_boundary_scores = scores
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
        opening_stats = opening_participant_stats[participant_id]
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
                    "straightAttacksLaunched": stats[
                        "straightAttacksLaunched"
                    ],
                    "curvedAttacksLaunched": stats[
                        "curvedAttacksLaunched"
                    ],
                    "damageEvents": stats["damageEvents"],
                    "damageAmount": stats["damageAmount"],
                    "straightDamageEvents": stats[
                        "straightDamageEvents"
                    ],
                    "straightDamageAmount": stats[
                        "straightDamageAmount"
                    ],
                    "curvedDamageEvents": stats[
                        "curvedDamageEvents"
                    ],
                    "curvedDamageAmount": stats[
                        "curvedDamageAmount"
                    ],
                    "straightAttackDamageConversion": _fraction(
                        stats["straightDamageEvents"],
                        stats["straightAttacksLaunched"],
                    ),
                    "curvedAttackDamageConversion": _fraction(
                        stats["curvedDamageEvents"],
                        stats["curvedAttacksLaunched"],
                    ),
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
                    "curvedThreatVisibleTurns": stats[
                        "curvedThreatVisibleTurns"
                    ],
                    "curvedThreatMovementAvailableTurns": stats[
                        "curvedThreatMovementAvailableTurns"
                    ],
                    "curvedThreatMovementResponses": stats[
                        "curvedThreatMovementResponses"
                    ],
                    "curvedThreatMovementResponseShare": _fraction(
                        stats["curvedThreatMovementResponses"],
                        stats[
                            "curvedThreatMovementAvailableTurns"
                        ],
                    ),
                    "curvedThreatOnObjectiveTurns": stats[
                        "curvedThreatOnObjectiveTurns"
                    ],
                    "curvedThreatOnObjectiveMovementResponses": stats[
                        "curvedThreatOnObjectiveMovementResponses"
                    ],
                    "curvedThreatOnObjectiveHoldResponses": stats[
                        "curvedThreatOnObjectiveHoldResponses"
                    ],
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
                "opening": {
                    "turns": opening_stats["turns"],
                    "attackDecisions": opening_stats["attackDecisions"],
                    "straightAttackDecisions": opening_stats[
                        "straightAttackDecisions"
                    ],
                    "curvedAttackDecisions": opening_stats[
                        "curvedAttackDecisions"
                    ],
                    "attacksLaunched": opening_stats["attacksLaunched"],
                    "straightAttacksLaunched": opening_stats[
                        "straightAttacksLaunched"
                    ],
                    "curvedAttacksLaunched": opening_stats[
                        "curvedAttacksLaunched"
                    ],
                    "damageEvents": opening_stats["damageEvents"],
                    "damageAmount": opening_stats["damageAmount"],
                    "straightDamageEvents": opening_stats[
                        "straightDamageEvents"
                    ],
                    "straightDamageAmount": opening_stats[
                        "straightDamageAmount"
                    ],
                    "curvedDamageEvents": opening_stats[
                        "curvedDamageEvents"
                    ],
                    "curvedDamageAmount": opening_stats[
                        "curvedDamageAmount"
                    ],
                    "curvedThreatVisibleTurns": opening_stats[
                        "curvedThreatVisibleTurns"
                    ],
                    "curvedThreatMovementAvailableTurns": opening_stats[
                        "curvedThreatMovementAvailableTurns"
                    ],
                    "curvedThreatMovementResponses": opening_stats[
                        "curvedThreatMovementResponses"
                    ],
                    "curvedThreatOnObjectiveTurns": opening_stats[
                        "curvedThreatOnObjectiveTurns"
                    ],
                    "curvedThreatOnObjectiveMovementResponses":
                        opening_stats[
                            "curvedThreatOnObjectiveMovementResponses"
                        ],
                    "curvedThreatOnObjectiveHoldResponses": opening_stats[
                        "curvedThreatOnObjectiveHoldResponses"
                    ],
                    "movementDecisions": opening_stats[
                        "movementDecisions"
                    ],
                },
            }
        )

    dynamics_row = None
    if dynamics and dynamics_trace:
        positive_team_ids = sorted(
            team_id
            for team_id, delta in advance_deltas.items()
            if delta > 0
        )
        final_signed_score = 0
        for standing in team_standings:
            if not positive_team_ids:
                break
            if standing["teamId"] != positive_team_ids[0]:
                continue
            for raw_score in _array(
                standing["scores"] or [],
                "standing.scores",
            ):
                score = _object(raw_score, "standing score")
                if str(score.get("channel")) == "territorial-progress":
                    final_signed_score = int(str(score.get("value")))
        dynamics_row = _dynamics_metrics(
            dynamics_trace,
            objective_tiles=objective_tiles,
            form_weights=form_weights,
            advance_deltas=advance_deltas,
            centre_index=centre_index,
            capture_threshold=capture_threshold,
            floor_tiles=floor_tiles,
            completion_reason=str(result.get("completionReason")),
            winner_team_id=winner_team_id,
            final_signed_score=final_signed_score,
        )

    row = {
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
        "opening": {
            "endsBeforeTick": first_unlock_tick,
            "ticks": opening_ticks,
            "matchEndedBeforeFirstUnlock": (
                bool(unlock_ticks)
                and end_tick is not None
                and end_tick < first_unlock_tick
            ),
            "damageEvents": sum(
                stats["damageEvents"]
                for stats in opening_participant_stats.values()
            ),
            "damageAmount": sum(
                stats["damageAmount"]
                for stats in opening_participant_stats.values()
            ),
            "objective": {
                "contestedTicks": opening_objective["contestedTicks"],
                "soleControlTicks": opening_objective["soleControlTicks"],
                "emptyTicks": opening_objective["emptyTicks"],
                "pushes": opening_objective["pushes"],
            },
            "boundaryScores": {
                str(team_id): {
                    channel: value
                    for (score_team_id, channel), value
                    in sorted(opening_boundary_scores.items())
                    if score_team_id == team_id
                }
                for team_id in topology_team_ids
            },
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
    if dynamics_row is not None:
        row["dynamics"] = dynamics_row
    return row


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
                    "opening": Counter(),
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
                "straightAttacksLaunched",
                "curvedAttacksLaunched",
                "damageEvents",
                "damageAmount",
                "straightDamageEvents",
                "straightDamageAmount",
                "curvedDamageEvents",
                "curvedDamageAmount",
                "enemyVisibleTurns",
                "enemyVisibleAttackAvailableTurns",
                "attackOpportunityUses",
                "directAttackOpportunityTurns",
                "directAttackOpportunityUses",
                "hostileProjectileVisibleTurns",
                "hostileProjectileMovementAvailableTurns",
                "projectileMovementResponses",
                "curvedThreatVisibleTurns",
                "curvedThreatMovementAvailableTurns",
                "curvedThreatMovementResponses",
                "curvedThreatOnObjectiveTurns",
                "curvedThreatOnObjectiveMovementResponses",
                "curvedThreatOnObjectiveHoldResponses",
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
            for field, value in participant["opening"].items():
                bucket["opening"][field] += value

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
                    "straightAttackDamageConversion": _fraction(
                        combat["straightDamageEvents"],
                        combat["straightAttacksLaunched"],
                    ),
                    "curvedAttackDamageConversion": _fraction(
                        combat["curvedDamageEvents"],
                        combat["curvedAttacksLaunched"],
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
                    "curvedThreatMovementResponseShare": _fraction(
                        combat["curvedThreatMovementResponses"],
                        combat[
                            "curvedThreatMovementAvailableTurns"
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
                "opening": dict(bucket["opening"]),
            }
        )
    return entrants


def _summarize_pairings(
    rows: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    buckets: dict[tuple[str, str], dict[str, Any]] = {}
    for row in rows:
        participants = row["participants"]
        if len(participants) != 2:
            continue
        by_hash = {
            str(participant["artifactHash"]): participant
            for participant in participants
        }
        if len(by_hash) != 2:
            continue
        key = tuple(sorted(by_hash))
        bucket = buckets.setdefault(
            key,
            {
                "matches": 0,
                "entrants": {
                    artifact_hash: {
                        "name": by_hash[artifact_hash]["name"],
                        "artifactHash": artifact_hash,
                        "appearances": 0,
                        "wins": 0,
                        "opening": Counter(),
                        "boundaryTerritorialProgress": 0,
                    }
                    for artifact_hash in key
                },
            },
        )
        bucket["matches"] += 1
        winner_team_id = row["result"]["winnerTeamId"]
        for participant in participants:
            entrant = bucket["entrants"][
                str(participant["artifactHash"])
            ]
            entrant["appearances"] += 1
            entrant["wins"] += int(
                winner_team_id == participant["teamId"]
            )
            for field, value in participant["opening"].items():
                entrant["opening"][field] += value
            entrant["boundaryTerritorialProgress"] += row["opening"][
                "boundaryScores"
            ][str(participant["teamId"])].get(
                "territorial-progress",
                0,
            )

    result = []
    for key, bucket in sorted(buckets.items()):
        entrants = []
        for artifact_hash in key:
            entrant = bucket["entrants"][artifact_hash]
            opening = entrant["opening"]
            appearances = entrant["appearances"]
            entrants.append(
                {
                    "name": entrant["name"],
                    "artifactHash": artifact_hash,
                    "appearances": appearances,
                    "wins": entrant["wins"],
                    "winShare": _fraction(
                        entrant["wins"],
                        appearances,
                    ),
                    "openingDamagePerAppearance": _fraction(
                        opening["damageAmount"],
                        appearances,
                    ),
                    "openingCurvedAttackDamageConversion": _fraction(
                        opening["curvedDamageEvents"],
                        opening["curvedAttacksLaunched"],
                    ),
                    "openingStraightAttackDamageConversion": _fraction(
                        opening["straightDamageEvents"],
                        opening["straightAttacksLaunched"],
                    ),
                    "openingCurvedThreatMovementResponseShare": _fraction(
                        opening["curvedThreatMovementResponses"],
                        opening[
                            "curvedThreatMovementAvailableTurns"
                        ],
                    ),
                    "openingCurvedThreatOnObjectiveMoveShare": _fraction(
                        opening[
                            "curvedThreatOnObjectiveMovementResponses"
                        ],
                        opening["curvedThreatOnObjectiveTurns"],
                    ),
                    "averageBoundaryTerritorialProgress": _fraction(
                        entrant["boundaryTerritorialProgress"],
                        appearances,
                    ),
                }
            )
        result.append(
            {
                "matches": bucket["matches"],
                "entrants": sorted(
                    entrants,
                    key=lambda value: (
                        str(value["name"]),
                        value["artifactHash"],
                    ),
                ),
            }
        )
    return result


def _criterion(
    metric: str,
    comparison: str,
    threshold: float,
    observed: float | None,
    *,
    operationalized: bool = False,
    basis: str | None = None,
) -> dict[str, Any]:
    """One pre-registered pass/fail criterion with its observed value."""

    if observed is None:
        passed = None
    elif comparison == ">":
        passed = observed > threshold
    elif comparison == "<":
        passed = observed < threshold
    elif comparison == "<=":
        passed = observed <= threshold
    else:
        raise ValueError(f"unsupported comparison '{comparison}'")
    criterion = {
        "metric": metric,
        "comparison": comparison,
        "threshold": threshold,
        "observed": observed,
        "pass": passed,
    }
    if operationalized:
        criterion["operationalized"] = True
    if basis is not None:
        criterion["basis"] = basis
    return criterion


def _dynamics_gates(dynamics: dict[str, Any]) -> dict[str, Any]:
    """Evaluate the forensics document's pre-registered S1-S5 / N1 gates."""

    pendulum = dynamics["pendulum"]
    transit = dynamics["reinforcementTransit"]
    sole = dynamics["solePresence"]
    staleness = dynamics["staleness"]
    positional = dynamics["positional"]
    score = dynamics["score"]
    frozen_basis = (
        "capped-matches"
        if staleness["cappedMatches"]
        else "all-matches"
    )
    frozen_share = (
        staleness["cappedFrozenScoreboardShare"]
        if staleness["cappedMatches"]
        else staleness["frozenScoreboardShare"]
    )
    gates = {
        "S1-territory-ratchet": [
            _criterion(
                "pendulum.leaderExtendsProbability",
                ">",
                0.50,
                pendulum["leaderExtendsProbability"],
            ),
            _criterion(
                "pendulum.displacementEfficiencyMedian",
                ">",
                0.40,
                pendulum["displacementEfficiencyMedian"],
            ),
            _criterion(
                "pendulum.capShare",
                "<",
                0.35,
                pendulum["capShare"],
            ),
            _criterion(
                "pendulum.drawRate",
                "<",
                NEAR_ZERO_DRAW_RATE,
                pendulum["drawRate"],
                operationalized=True,
            ),
        ],
        "S2-forward-spawn": [
            _criterion(
                "reinforcementTransit.gradientSpreadTicks",
                "<=",
                3,
                transit["gradientSpreadTicks"],
            ),
        ],
        "S3-contest-costs": [
            _criterion(
                "solePresence.contestedShare",
                "<",
                0.20,
                sole["contestedShare"],
            ),
            _criterion(
                "solePresence.wastedShare",
                "<",
                0.25,
                sole["wastedShare"],
            ),
            _criterion(
                "staleness.frozenScoreboardShare",
                "<",
                0.15,
                frozen_share,
                basis=frozen_basis,
            ),
        ],
        "S4-overtime-escalation": [
            _criterion(
                "pendulum.capShare",
                "<",
                0.15,
                pendulum["capShare"],
            ),
            _criterion(
                "score.signPredictiveness.300",
                ">",
                0.80,
                score["signPredictiveness"].get("300"),
            ),
        ],
        "S5-map-geometry": [
            _criterion(
                "positional.effectiveTilesMedian",
                ">",
                60.0,
                positional["effectiveTilesMedian"],
            ),
        ],
        "N1-lower-capture-threshold": [
            _criterion(
                "pendulum.reversalRateMedian",
                "<",
                PENDULUM_REVERSAL_RATE - PENDULUM_REVERSAL_TOLERANCE,
                pendulum["reversalRateMedian"],
                operationalized=True,
            ),
        ],
    }
    return {
        gate: {
            "criteria": criteria,
            "pass": (
                None
                if any(item["pass"] is None for item in criteria)
                else all(item["pass"] for item in criteria)
            ),
        }
        for gate, criteria in sorted(gates.items())
    }


def summarize_dynamics(
    name: str,
    rows: list[dict[str, Any]],
) -> dict[str, Any]:
    """Aggregate the pendulum/dullness family over one replay set.

    Rows may span rules arms and maps: unlike `summarize_group` this block is
    a pooled description of match dynamics, not a cohort claim.
    """

    if not rows:
        raise ValueError(f"dynamics set '{name}' contains no replays")
    missing = [row["source"] for row in rows if "dynamics" not in row]
    if missing:
        raise ValueError(
            f"dynamics set '{name}' has rows without a dynamics block "
            "(analyzed without --dynamics, or a zero-tick replay): "
            f"{sorted(missing)[:3]}"
        )
    entries = [row["dynamics"] for row in rows]
    centre_indexes = {entry["contract"]["centreIndex"] for entry in entries}
    if len(centre_indexes) != 1:
        raise ValueError(
            f"dynamics set '{name}' mixes frontline centre indexes: "
            f"{sorted(centre_indexes)}"
        )
    centre_index = next(iter(centre_indexes))
    capped = [
        entry
        for row, entry in zip(rows, entries)
        if row["result"]["completionReason"] == "max-ticks"
    ]

    transitions: Counter[tuple[int, int]] = Counter()
    for entry in entries:
        for key, count in entry["frontline"]["transitions"].items():
            index, _, step = key.partition("|")
            transitions[(int(index), int(step))] += count
    by_displacement: dict[int, dict[str, int]] = {}
    for (index, step), count in transitions.items():
        bucket = by_displacement.setdefault(
            index - centre_index,
            {"advancesUp": 0, "advancesDown": 0},
        )
        bucket["advancesUp" if step > 0 else "advancesDown"] += count
    displacement_rows = []
    extends = 0
    reverts = 0
    for displacement in sorted(by_displacement):
        bucket = by_displacement[displacement]
        total = bucket["advancesUp"] + bucket["advancesDown"]
        leading = (
            None
            if displacement == 0
            else bucket["advancesUp"]
            if displacement > 0
            else bucket["advancesDown"]
        )
        if leading is not None:
            extends += leading
            reverts += total - leading
        displacement_rows.append(
            {
                "displacement": displacement,
                "positionIndex": displacement + centre_index,
                **bucket,
                "transitions": total,
                "leaderExtendsProbability": (
                    None if leading is None else _fraction(leading, total)
                ),
            }
        )

    advances = [entry["frontline"]["advances"] for entry in entries]
    reversal_rates = [
        entry["frontline"]["reversalRate"]
        for entry in entries
        if entry["frontline"]["reversalRate"] is not None
    ]
    efficiencies = [
        entry["frontline"]["displacementEfficiency"]
        for entry in entries
        if entry["frontline"]["displacementEfficiency"] is not None
    ]
    final_displacements: Counter[int] = Counter(
        entry["frontline"]["finalDisplacement"] for entry in entries
    )
    capped_final_displacements: Counter[int] = Counter(
        entry["frontline"]["finalDisplacement"] for entry in capped
    )

    transit_by_lead: dict[int, Counter[int]] = defaultdict(Counter)
    for entry in entries:
        for lead, histogram in entry["reinforcementTransit"][
            "byLeadTickHistogram"
        ].items():
            for key, count in histogram.items():
                transit_by_lead[int(lead)][int(key)] += count
    # Every lead the frontline can present is reported, sampled or not, so the
    # bucket key set stays stable across replay sets.
    position_counts = {
        entry["contract"]["positionCount"] for entry in entries
    }
    reachable_leads = sorted(
        {
            (index - centre_index) * sign
            for count in position_counts
            for index in range(count)
            for sign in (1, -1)
        }
        | set(transit_by_lead)
    )
    transit_rows = {}
    transit_medians = []
    for lead in reachable_leads:
        samples = _histogram_values(_histogram(transit_by_lead[lead]))
        median = _median(samples)
        if median is not None:
            transit_medians.append(median)
        transit_rows[str(lead)] = {
            "samples": len(samples),
            "medianTicks": median,
            "meanTicks": _mean(samples),
            "p75Ticks": _quantile(samples, 0.75),
        }

    control_mix: Counter[str] = Counter()
    terminations: Counter[str] = Counter()
    incomplete_terminations: Counter[str] = Counter()
    for entry in entries:
        control_mix.update(entry["solePresence"]["controlMixTicks"])
        terminations.update(entry["solePresence"]["terminations"])
        incomplete_terminations.update(
            entry["solePresence"]["incompleteTerminations"]
        )
    control_ticks = sum(control_mix.values())
    run_histogram = _merge_histograms(
        entry["solePresence"]["runLengthHistogram"] for entry in entries
    )
    run_samples = _histogram_values(run_histogram)
    productive_ticks = sum(
        entry["solePresence"]["productiveTicks"] for entry in entries
    )
    wasted_ticks = sum(
        entry["solePresence"]["wastedTicks"] for entry in entries
    )
    progress_gained = sum(
        entry["solePresence"]["progressGained"] for entry in entries
    )
    progress_lost = sum(
        entry["solePresence"]["progressLost"] for entry in entries
    )
    runs = sum(entry["solePresence"]["runs"] for entry in entries)
    runs_reaching = sum(
        entry["solePresence"]["runsReachingThreshold"] for entry in entries
    )

    frozen_ticks = [
        entry["staleness"]["longestFrozenScoreboardTicks"]
        for entry in entries
    ]
    cycle_ticks = [
        entry["staleness"]["longestLimitCycleTicks"] for entry in entries
    ]
    close_ticks = sum(entry["closeContact"]["ticks"] for entry in entries)
    close_no_damage = sum(
        entry["closeContact"]["noDamageTicks"] for entry in entries
    )
    sustained_stare = sum(
        entry["closeContact"]["sustainedStareTicks"] for entry in entries
    )
    total_ticks = sum(row["duration"]["ticks"] for row in rows)

    traces = [
        trace
        for entry in entries
        for trace in entry["positional"]["traces"]
    ]
    argmax_lags: Counter[int] = Counter(
        trace["argmaxLag"]
        for trace in traces
        if trace["argmaxLag"] is not None
    )

    predictiveness = {}
    for probe in SCORE_SIGN_PROBE_TICKS:
        agreeing = 0
        comparable = 0
        for entry in capped:
            observed = entry["score"]["signedTerritorialProbes"][str(probe)]
            final = entry["score"]["finalSignedTerritorial"]
            if observed == 0 or final == 0:
                continue
            comparable += 1
            agreeing += int((observed > 0) == (final > 0))
        predictiveness[str(probe)] = (
            _fraction(agreeing, comparable) if comparable else None
        )
        predictiveness[f"{probe}Matches"] = comparable

    summary = {
        "name": name,
        "definitionsVersion": DYNAMICS_DEFINITIONS_VERSION,
        "matches": len(rows),
        "cappedMatches": len(capped),
        "captureThresholds": sorted(
            {entry["contract"]["captureThreshold"] for entry in entries}
        ),
        "pendulum": {
            "leaderExtendsProbability": (
                _fraction(extends, extends + reverts)
                if extends + reverts
                else None
            ),
            "leaderExtendsTransitions": extends + reverts,
            "byDisplacement": displacement_rows,
            "advancesPerMatchMean": _mean(advances),
            "advancesPerMatchMedian": _median(advances),
            "reversalRateMean": _mean(reversal_rates),
            "reversalRateMedian": _median(reversal_rates),
            "reversalRateMatches": len(reversal_rates),
            "displacementEfficiencyMean": _mean(efficiencies),
            "displacementEfficiencyMedian": _median(efficiencies),
            "finalDisplacementHistogram": _histogram(final_displacements),
            "cappedFinalDisplacementHistogram": _histogram(
                capped_final_displacements
            ),
            "meanAbsoluteFinalDisplacement": _mean(
                [
                    abs(entry["frontline"]["finalDisplacement"])
                    for entry in entries
                ]
            ),
            "capShare": _fraction(len(capped), len(rows)),
            "drawRate": _fraction(
                sum(1 for row in rows if row["result"]["draw"]),
                len(rows),
            ),
            "breachMatches": sum(
                1
                for entry in entries
                if entry["frontline"]["breachCountedAsAdvance"]
            ),
        },
        "reinforcementTransit": {
            "byLead": transit_rows,
            "gradientSpreadTicks": (
                max(transit_medians) - min(transit_medians)
                if transit_medians
                else None
            ),
            "arrivedLives": sum(
                entry["reinforcementTransit"]["arrivedLives"]
                for entry in entries
            ),
            "reinforcementLives": sum(
                entry["reinforcementTransit"]["reinforcementLives"]
                for entry in entries
            ),
        },
        "solePresence": {
            "controlMixTicks": dict(sorted(control_mix.items())),
            "controlMixShare": {
                key: _fraction(value, control_ticks)
                for key, value in sorted(control_mix.items())
            },
            "contestedShare": _fraction(
                control_mix["contested"],
                control_ticks,
            ),
            "productiveTicks": productive_ticks,
            "wastedTicks": wasted_ticks,
            "wastedShare": _fraction(
                wasted_ticks,
                productive_ticks + wasted_ticks,
            ),
            "progressGained": progress_gained,
            "progressLost": progress_lost,
            "decayDestroyedShare": _fraction(progress_lost, progress_gained),
            "runs": runs,
            "runLengthMedian": _median(run_samples),
            "runLengthP90": _quantile(run_samples, 0.90),
            "runsReachingThreshold": runs_reaching,
            "runsReachingThresholdShare": _fraction(runs_reaching, runs),
            "terminationShare": {
                key: _fraction(value, sum(terminations.values()))
                for key, value in sorted(terminations.items())
            },
            "incompleteTerminationShare": {
                key: _fraction(
                    value,
                    sum(incomplete_terminations.values()),
                )
                for key, value in sorted(incomplete_terminations.items())
            },
            "capturesPerMatchMean": _mean(
                [entry["solePresence"]["captures"] for entry in entries]
            ),
        },
        "staleness": {
            "cappedMatches": len(capped),
            "frozenScoreboardShare": _fraction(
                sum(1 for entry in entries if entry["staleness"][
                    "frozenScoreboard"
                ]),
                len(entries),
            ),
            "cappedFrozenScoreboardShare": _fraction(
                sum(
                    1
                    for entry in capped
                    if entry["staleness"]["frozenScoreboard"]
                ),
                len(capped),
            ),
            "frozenScoreboardMedianTicks": _median(frozen_ticks),
            "frozenScoreboardP90Ticks": _quantile(frozen_ticks, 0.90),
            "limitCycleShare": _fraction(
                sum(
                    1
                    for entry in entries
                    if entry["staleness"]["limitCycle"]
                ),
                len(entries),
            ),
            "cappedLimitCycleShare": _fraction(
                sum(1 for entry in capped if entry["staleness"]["limitCycle"]),
                len(capped),
            ),
            "limitCycleMedianTicks": _median(cycle_ticks),
            "limitCycleP90Ticks": _quantile(cycle_ticks, 0.90),
            "terminalLimitCycleShare": _fraction(
                sum(
                    1
                    for entry in entries
                    if entry["staleness"]["terminalLimitCycle"]
                ),
                len(entries),
            ),
            "limitCyclePeriodHistogram": _histogram(
                Counter(
                    entry["staleness"]["limitCyclePeriod"]
                    for entry in entries
                    if entry["staleness"]["limitCycle"]
                )
            ),
        },
        "closeContact": {
            "distanceMetric": CLOSE_CONTACT_DISTANCE,
            "radiusTiles": CLOSE_CONTACT_TILES,
            "ticks": close_ticks,
            "closeShare": _fraction(close_ticks, total_ticks),
            "noDamageTicks": close_no_damage,
            "noDamageShare": _fraction(close_no_damage, close_ticks),
            "sustainedStareTicks": sustained_stare,
            "sustainedStareShare": _fraction(sustained_stare, total_ticks),
        },
        "positional": {
            "effectiveTilesMedian": _median(
                [entry["positional"]["effectiveTiles"] for entry in entries]
            ),
            "effectiveTilesP10": _quantile(
                [entry["positional"]["effectiveTiles"] for entry in entries],
                0.10,
            ),
            "effectiveTilesP90": _quantile(
                [entry["positional"]["effectiveTiles"] for entry in entries],
                0.90,
            ),
            "coverageMedian": _median(
                [entry["positional"]["coverage"] for entry in entries]
            ),
            "traces": len(traces),
            "tileRatioMedian": _median(
                [trace["tileRatio"] for trace in traces]
            ),
            "dwellFractionMedian": _median(
                [trace["dwellFraction"] for trace in traces]
            ),
            "argmaxAutocorrelationMedian": _median(
                [trace["argmaxAutocorrelation"] for trace in traces]
            ),
            "lag2AutocorrelationMedian": _median(
                [trace["lag2Autocorrelation"] for trace in traces]
            ),
            "argmaxLagHistogram": _histogram(argmax_lags),
            "argmaxLag2Share": _fraction(argmax_lags[2], len(traces)),
        },
        "score": {
            "signPredictiveness": predictiveness,
            "leadChangesMean": _mean(
                [entry["score"]["leadChanges"] for entry in entries]
            ),
            "leadChangesMedian": _median(
                [entry["score"]["leadChanges"] for entry in entries]
            ),
            "levelTickShareMedian": _median(
                [
                    _fraction(
                        entry["score"]["levelTicks"],
                        row["duration"]["ticks"],
                    )
                    for row, entry in zip(rows, entries)
                ]
            ),
        },
    }
    summary["gates"] = _dynamics_gates(summary)
    return summary


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
    summary = {
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
        "opening": {
            "gamesWithDamage": sum(
                row["opening"]["damageEvents"] > 0 for row in rows
            ),
            "gamesEndingBeforeFirstUnlock": sum(
                row["opening"]["matchEndedBeforeFirstUnlock"]
                for row in rows
            ),
            "damageAmount": sum(
                row["opening"]["damageAmount"] for row in rows
            ),
            "pushes": sum(
                row["opening"]["objective"]["pushes"] for row in rows
            ),
        },
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
        "pairings": _summarize_pairings(rows),
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
            "straightAttacks": sum(
                participant["combatPolicy"]["straightAttacksLaunched"]
                for row in rows
                for participant in row["participants"]
            ),
            "curvedAttacks": sum(
                participant["combatPolicy"]["curvedAttacksLaunched"]
                for row in rows
                for participant in row["participants"]
            ),
            "straightDamageAmount": sum(
                participant["combatPolicy"]["straightDamageAmount"]
                for row in rows
                for participant in row["participants"]
            ),
            "curvedDamageAmount": sum(
                participant["combatPolicy"]["curvedDamageAmount"]
                for row in rows
                for participant in row["participants"]
            ),
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
    if all("dynamics" in row for row in rows):
        summary["dynamics"] = summarize_dynamics(name, rows)
    return summary


def _find_replays(path: Path) -> list[Path]:
    if path.is_file():
        return [path]
    return sorted(path.rglob("replay.json"))


def _parse_group(value: str) -> tuple[str, Path]:
    name, separator, raw_path = value.partition("=")
    if not separator or not name or not raw_path:
        raise argparse.ArgumentTypeError("expected NAME=PATH")
    return name, Path(raw_path)


def _format_optional(value: float | None, template: str) -> str:
    return "n/a" if value is None else template.format(value)


def _print_dynamics(dynamics: dict[str, Any], indent: str = "  ") -> None:
    """Compact human view of the pendulum/dullness family and its gates."""

    pendulum = dynamics["pendulum"]
    transit = dynamics["reinforcementTransit"]
    sole = dynamics["solePresence"]
    staleness = dynamics["staleness"]
    close = dynamics["closeContact"]
    positional = dynamics["positional"]
    print(
        f"{indent}pendulum  leader-extends="
        + _format_optional(
            pendulum["leaderExtendsProbability"],
            "{:.3f}",
        )
        + f" (n={pendulum['leaderExtendsTransitions']}) "
        + "advances/match="
        + _format_optional(pendulum["advancesPerMatchMean"], "{:.1f}")
        + " reversal="
        + _format_optional(pendulum["reversalRateMean"], "{:.3f}")
        + " |net|/adv="
        + _format_optional(
            pendulum["displacementEfficiencyMedian"],
            "{:.3f}",
        )
        + f" cap={pendulum['capShare']:.1%}"
        + f" draws={pendulum['drawRate']:.1%}"
    )
    print(
        f"{indent}transit   "
        + " ".join(
            f"lead{int(lead):+d}="
            + _format_optional(values["medianTicks"], "{:.0f}")
            for lead, values in sorted(
                transit["byLead"].items(),
                key=lambda item: int(item[0]),
            )
        )
        + " spread="
        + _format_optional(transit["gradientSpreadTicks"], "{:.0f}")
        + "t"
    )
    print(
        f"{indent}sole      "
        f"wasted={sole['wastedShare']:.1%} "
        f"decay-destroyed={sole['decayDestroyedShare']:.1%} "
        f"runs>=threshold={sole['runsReachingThresholdShare']:.1%} "
        + "median-run="
        + _format_optional(sole["runLengthMedian"], "{:.0f}")
        + f" contested={sole['contestedShare']:.1%}"
    )
    print(
        f"{indent}stale     "
        f"frozen>=50={staleness['cappedFrozenScoreboardShare']:.1%} "
        f"cycle>=50={staleness['cappedLimitCycleShare']:.1%} "
        f"(capped n={staleness['cappedMatches']}) "
        f"close-no-damage={close['noDamageShare']:.1%} "
        f"stare>={SUSTAINED_STARE_TICKS}t="
        f"{close['sustainedStareShare']:.1%}"
    )
    print(
        f"{indent}space     eff-tiles="
        + _format_optional(positional["effectiveTilesMedian"], "{:.1f}")
        + " coverage="
        + _format_optional(positional["coverageMedian"], "{:.1%}")
        + " dwell="
        + _format_optional(positional["dwellFractionMedian"], "{:.3f}")
        + " pos-autocorr="
        + _format_optional(
            positional["argmaxAutocorrelationMedian"],
            "{:.3f}",
        )
        + f" lag2-argmax={positional['argmaxLag2Share']:.1%}"
    )
    print(
        f"{indent}gates     "
        + " ".join(
            f"{gate.split('-')[0]}="
            + (
                "n/a"
                if values["pass"] is None
                else "pass"
                if values["pass"]
                else "fail"
            )
            for gate, values in sorted(dynamics["gates"].items())
        )
    )


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
        opening = group["opening"]
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
            f"straight={combat['straightDamageAmount']}/"
            f"{combat['straightAttacks']} "
            f"curved={combat['curvedDamageAmount']}/"
            f"{combat['curvedAttacks']} "
            f"damage/100t={combat['damagePer100Ticks']:.1f} "
            f"damage={combat['damageAmount']} "
            f"destructions={combat['destructions']}"
        )
        print(
            "  opening   "
            f"damage-games={opening['gamesWithDamage']}/"
            f"{group['matches']} "
            f"damage={opening['damageAmount']} "
            f"pushes={opening['pushes']} "
            f"early-finishes={opening['gamesEndingBeforeFirstUnlock']}"
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
                f"{policy['imminentThreatMovementResponseShare']:.1%} "
                f"opening-curves="
                f"{entrant['opening'].get('curvedAttackDecisions', 0)} "
                f"opening-damage="
                f"{entrant['opening'].get('damageAmount', 0)}"
            )
        if "dynamics" in group:
            _print_dynamics(group["dynamics"])


def verify_dynamics(
    report: dict[str, Any],
    expectations: dict[str, Any],
) -> list[dict[str, Any]]:
    """Compare dynamics metrics against a pre-registered baseline file.

    Each expectation names a dotted `path` inside a dynamics block, its
    `expected` value and an absolute `tolerance`. `group` selects a named
    group's block; omitting it reads the report-level pooled block.
    """

    groups = {group["name"]: group for group in report.get("groups", [])}
    results = []
    for raw in _array(
        expectations.get("expectations"),
        "expectations",
    ):
        expectation = _object(raw, "expectation")
        group_name = expectation.get("group")
        if group_name is None:
            block = report.get("dynamics")
            scope = "pooled"
        else:
            group = groups.get(str(group_name))
            block = None if group is None else group.get("dynamics")
            scope = str(group_name)
        path = str(expectation.get("path"))
        expected = float(expectation.get("expected"))
        tolerance = float(expectation.get("tolerance"))
        if block is None:
            results.append(
                {
                    "scope": scope,
                    "path": path,
                    "expected": expected,
                    "tolerance": tolerance,
                    "observed": None,
                    "deviation": None,
                    "pass": False,
                    "error": f"no dynamics block for scope '{scope}'",
                }
            )
            continue
        try:
            observed = _resolve_metric_path(block, path)
        except ValueError as error:
            results.append(
                {
                    "scope": scope,
                    "path": path,
                    "expected": expected,
                    "tolerance": tolerance,
                    "observed": None,
                    "deviation": None,
                    "pass": False,
                    "error": str(error),
                }
            )
            continue
        deviation = (
            None if observed is None else abs(float(observed) - expected)
        )
        results.append(
            {
                "scope": scope,
                "path": path,
                "expected": expected,
                "tolerance": tolerance,
                "observed": observed,
                "deviation": deviation,
                "pass": deviation is not None and deviation <= tolerance,
            }
        )
    return results


def _print_verification(
    expectations: dict[str, Any],
    results: list[dict[str, Any]],
) -> None:
    print()
    print(
        "Dynamics verification against "
        f"{expectations.get('baseline', 'baseline')}"
    )
    for result in results:
        observed = (
            "n/a"
            if result["observed"] is None
            else f"{float(result['observed']):.4f}"
        )
        print(
            f"  [{'ok  ' if result['pass'] else 'FAIL'}] "
            f"{result['scope']}:{result['path']} "
            f"observed={observed} "
            f"expected={result['expected']:.4f}"
            f"+/-{result['tolerance']:.4f}"
            + (f"  {result['error']}" if result.get("error") else "")
        )
    failures = sum(1 for result in results if not result["pass"])
    print(
        f"  {len(results) - failures}/{len(results)} within tolerance"
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
    parser.add_argument(
        "--dynamics",
        action="store_true",
        help=(
            "Add the pendulum/dullness metric family and the pre-registered "
            "S1-S5 / N1 gate evaluation from "
            "docs/DESIGN-FORENSICS-DYNAMICS-2026-07-29.md."
        ),
    )
    parser.add_argument(
        "--verify-against",
        type=Path,
        metavar="BASELINE",
        help=(
            "Check the dynamics metrics against a baseline expectations "
            "file; implies --dynamics and exits non-zero on any deviation "
            "beyond its stated tolerance."
        ),
    )
    args = parser.parse_args(argv)
    dynamics = args.dynamics or args.verify_against is not None

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
                    dynamics=dynamics,
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
    if dynamics:
        report["dynamicsDefinitionsVersion"] = DYNAMICS_DEFINITIONS_VERSION
        report["dynamics"] = summarize_dynamics("pooled", rows)
    _print_report(summaries)
    if dynamics and len(summaries) > 1:
        print()
        print(f"pooled ({report['dynamics']['matches']} matches)")
        _print_dynamics(report["dynamics"])
    failures = 0
    if args.verify_against is not None:
        expectations = _object(
            json.loads(args.verify_against.read_text(encoding="utf-8")),
            str(args.verify_against),
        )
        results = verify_dynamics(report, expectations)
        report["verification"] = results
        _print_verification(expectations, results)
        failures = sum(1 for result in results if not result["pass"])
    if args.json is not None:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(
            json.dumps(report, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        print()
        print(f"JSON: {args.json.resolve()}")
    return 3 if failures else 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ValueError as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(2)
