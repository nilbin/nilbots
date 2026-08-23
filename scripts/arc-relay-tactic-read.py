#!/usr/bin/env python3
"""Project an Arc Relay replay into a compact, LLM-readable tactical trace.

The canonical replay remains the source of truth.  This read-only consumer
joins its authoritative state/events with the tactic/phase attribution already
published by the tactical mind.  With ``--check`` it also compares those
observations with one player-owned ``arc-relay-tactics-v2`` document.

The output is deliberately tiered:

  scripts/arc-relay-tactic-read.py REPLAY.json[.gz]
  scripts/arc-relay-tactic-read.py REPLAY.json.gz --spans
  scripts/arc-relay-tactic-read.py REPLAY.json.gz --unit 2 --window 20:60
  scripts/arc-relay-tactic-read.py REPLAY.json.gz --check TACTICS.json
  scripts/arc-relay-tactic-read.py REPLAY.json.gz --spans --frames --json

``--strict`` changes only the exit status: ordinary conformance deviations are
findings and exit zero.  The JSON and text renderers consume the same internal
model.  No timestamps or input paths enter that model, so identical replay and
tactics bytes produce byte-identical output.
"""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import re
import sys
from collections import Counter, defaultdict, deque
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence


SCHEMA = "arc-relay-tactic-read-v1"
PROGRAM_PREFIX = re.compile(
    r"orders:(idle|[a-z0-9-]+/[a-z0-9-]+)(?:!([^ ]+))? "
)
PATH_PATTERN = re.compile(
    r"([a-z0-9-]+) > ([a-z0-9-]+)(?: > ([a-z0-9-]+))?"
    r"(?: > ([a-z0-9-]+))?"
)
TRAIN_TARGET = re.compile(r"(?:^|[:\s])workshop:([a-z][a-z0-9-]*)")
TRAIN_ON_TILE = re.compile(r"at-target:workshop:([a-z][a-z0-9-]*)")
TACTIC_ORDINAL = re.compile(r"^t(\d+)$")
PHASED_LEAF = re.compile(r"^t\d+-p(\d+)-g(\d+)-")
INVARIANT_LEAF = re.compile(r"^t\d+-g(\d+)-")
LEGACY_LEAF = re.compile(r"^t\d+-g(\d+)-(s|a)-")
CONDITION_CASE = re.compile(r"\[c(\d+)\]")
FIGHT_TARGET = re.compile(
    r"(?:^|:)(?:fight|close|cadence|hold-track|hold-carrier-close|"
    r"hold-counter-range-close):(\d+):(\d+):(\d+)(?:[:;]|$)"
)
TARGET_POINT = re.compile(r"(?:march|face)@(-?\d+),(-?\d+)")
GOAL_POINT = re.compile(r"(?:^|:)goal-(-?\d+)-(-?\d+)(?:[:]|$)")

MOVEMENT_ACTIONS = {"move-eight-way", "strafe-eight-way"}
ATTACK_ACTIONS = {"shoot-direction", "attack"}
INTERRUPTION_PHRASES = {
    "left-tile": "stepped off the training tile",
    "moved": "moved",
    "damaged": "took damage",
    "acted-in-combat": "fought",
    "acquired-core": "picked up a Core",
    "died": "was destroyed",
    "contested": "enemies entered the contest room",
}


def read_json(path: Path) -> dict[str, Any]:
    if path.suffix == ".gz":
        with gzip.open(path, "rt", encoding="utf-8") as handle:
            return json.load(handle)
    return json.loads(path.read_text(encoding="utf-8"))


def actor_key(actor: dict[str, Any] | None) -> tuple[int, int, int] | None:
    if not actor:
        return None
    return actor["teamId"], actor["unitId"], actor["lifeId"]


def unit_key(actor: dict[str, Any] | None) -> tuple[int, int] | None:
    key = actor_key(actor)
    return None if key is None else key[:2]


def point(value: dict[str, Any] | Sequence[int] | None) -> tuple[int, int] | None:
    if value is None:
        return None
    if isinstance(value, dict):
        return int(value["x"]), int(value["y"])
    return int(value[0]), int(value[1])


def chebyshev(left: tuple[int, int], right: tuple[int, int]) -> int:
    return max(abs(left[0] - right[0]), abs(left[1] - right[1]))


def stable_json(value: Any) -> str:
    return json.dumps(
        value,
        ensure_ascii=True,
        separators=(",", ":"),
        sort_keys=True,
    )


def parse_window(value: str) -> tuple[int, int]:
    match = re.fullmatch(r"(\d+):(\d+)", value)
    if not match:
        raise argparse.ArgumentTypeError("window must be inclusive A:B ticks")
    start, end = int(match.group(1)), int(match.group(2))
    if end < start:
        raise argparse.ArgumentTypeError("window end must be >= its start")
    return start, end


@dataclass(frozen=True)
class Attribution:
    """Python twin of web/src/site/tactics/practice.ts attribution parsing."""

    role_id: str
    program: str | None
    marker: str | None
    tactic_ordinal: int | None
    phase_ordinal: int | None
    group_ordinal: int | None
    kind: str
    leaf_id: str
    path: str


def parse_attribution(debug_message: str) -> Attribution | None:
    """Follow practice.ts exactly; do not infer a second prefix grammar."""
    at = debug_message.find("orders:")
    if at < 0:
        return None
    rest = debug_message[at + len("orders:"):]
    marker: str | None = None
    program_name: str | None = None
    program = PROGRAM_PREFIX.search(debug_message[at:])
    if program:
        program_name = program.group(1)
        marker = program.group(2)
        rest = debug_message[at + len(program.group(0)):]
    match = PATH_PATTERN.search(rest)
    if not match:
        return None
    role_id, root, second, third = match.groups()
    leaf_id = third or second or root
    ordinal = TACTIC_ORDINAL.fullmatch(root)
    if not ordinal:
        return Attribution(
            role_id, program_name, marker, None, None, None, "baseline",
            leaf_id, " > ".join(value for value in match.groups() if value),
        )
    tactic_ordinal = int(ordinal.group(1))
    phase_ordinal: int | None = None
    group_ordinal: int | None = None
    kind = "invariant"
    phased = PHASED_LEAF.match(leaf_id)
    invariant = INVARIANT_LEAF.match(leaf_id)
    legacy = LEGACY_LEAF.match(leaf_id)
    if phased:
        phase_ordinal = int(phased.group(1))
        group_ordinal = int(phased.group(2))
        kind = "phase"
    elif legacy:
        group_ordinal = int(legacy.group(1))
        phase_ordinal = 1 if legacy.group(2) == "s" else 2
        kind = "phase"
    elif second == f"{root}-go":
        phase_ordinal = 2
        kind = "phase"
    elif second == f"{root}-s":
        phase_ordinal = 1
        kind = "phase"
    elif invariant:
        group_ordinal = int(invariant.group(1))
    return Attribution(
        role_id,
        program_name,
        marker,
        tactic_ordinal,
        phase_ordinal,
        group_ordinal,
        kind,
        leaf_id,
        " > ".join(value for value in match.groups() if value),
    )


def parse_training_intent(debug_message: str) -> tuple[str, bool] | None:
    standing = TRAIN_ON_TILE.search(debug_message)
    if standing:
        return standing.group(1), True
    walking = TRAIN_TARGET.search(debug_message)
    return (walking.group(1), False) if walking else None


def parse_fight_target(debug_message: str) -> tuple[int, int, int] | None:
    match = FIGHT_TARGET.search(debug_message)
    return tuple(map(int, match.groups())) if match else None


def compact_reason_state(reason: str) -> str:
    """Stable executor vocabulary only; dynamic EPIBT details stay drill-down."""
    ordered = (
        "route-bypass-unreachable",
        "route-bypass-stuck",
        "no-path",
        "move-blocked",
        "at-target",
        "use-unavailable",
        "no-actionable-target",
        "no-eligible-order",
        "cadence",
        "close",
        "fight",
    )
    return next((value for value in ordered if value in reason), "active")


def event_payload(event: dict[str, Any]) -> dict[str, Any]:
    payload = event.get("payload") or {}
    return payload.get("fact") if event.get("kind") == "arc-relay" else payload


class ReplayIndex:
    """One lossless, indexed read of the canonical generic-mind replay."""

    def __init__(self, replay: dict[str, Any]):
        if replay.get("partial"):
            raise ValueError("arc-relay-tactic-read requires a complete replay")
        if not replay.get("result"):
            raise ValueError("arc-relay-tactic-read requires a completed replay")
        self.replay = replay
        self.header = replay["header"]
        self.contract = self.header["contract"]
        self.map = self.contract["map"]
        self.width = int(self.map["width"])
        self.height = int(self.map["height"])
        self.rows = list(self.map["tileRows"])
        self.ticks = list(replay.get("ticks") or [])
        self.end_tick = int(replay["result"].get(
            "endTick", self.ticks[-1]["tick"] if self.ticks else -1
        ))
        provenance = self.header.get("provenance") or {}
        self.participants = {
            int(value["teamId"]): value
            for value in provenance.get("participants") or []
        }
        self.teams = sorted({
            int(value["teamId"])
            for value in self.contract["topology"]["participants"]
        })
        self.initial_state = replay["initialFrame"]["state"]
        self.events_by_tick: dict[int, list[dict[str, Any]]] = defaultdict(list)
        self.commands: dict[tuple[int, int, int], dict[str, Any]] = {}
        self.resolutions: dict[tuple[int, int, int], dict[str, Any]] = {}
        self.turns: dict[tuple[int, int], dict[str, Any]] = {}
        self.pre_lives: dict[int, dict[tuple[int, int], dict[str, Any]]] = {}
        self.post_lives: dict[int, dict[tuple[int, int], dict[str, Any]]] = {}
        self.pre_mode: dict[int, dict[str, Any]] = {}
        self.post_mode: dict[int, dict[str, Any]] = {}
        self._index()
        self.landmarks = self._landmarks()
        self.named_zones: list[dict[str, Any]] = []
        self.seat_normalize = False

    def _index(self) -> None:
        seen_events: set[str] = set()
        for tick in self.ticks:
            number = int(tick["tick"])
            start = tick["tickStart"]["state"]
            post = tick["postState"]
            self.pre_lives[number] = {
                (value["actorId"]["teamId"], value["actorId"]["unitId"]): value
                for value in start.get("activeLives") or []
            }
            self.post_lives[number] = {
                (value["actorId"]["teamId"], value["actorId"]["unitId"]): value
                for value in post.get("activeLives") or []
            }
            self.pre_mode[number] = start.get("mode") or {}
            self.post_mode[number] = post.get("mode") or {}
            event_sources = [
                tick.get("tickStart", {}).get("events") or [],
                tick.get("events") or [],
            ]
            for source in event_sources:
                for event in source:
                    handle = event.get("eventHandle") or stable_json(event)
                    if handle in seen_events:
                        continue
                    seen_events.add(handle)
                    self.events_by_tick[number].append(event)
            self.events_by_tick[number].sort(key=lambda value: (
                int(value.get("globalOrdinal", 0)),
                int(value.get("sourceOrdinal", 0)),
                value.get("kind", ""),
            ))
            for turn in tick.get("mindTurns") or []:
                team = int(turn["teamId"])
                self.turns[number, team] = turn
                resolution_by_actor = {
                    (int(value["unitId"]), int(value["lifeId"])): value
                    for value in turn.get("resolutions") or []
                }
                for command in turn.get("commands") or []:
                    unit = int(command["unitId"])
                    life = int(command["lifeId"])
                    self.commands[number, team, unit] = command
                    resolution = resolution_by_actor.get((unit, life))
                    if resolution:
                        self.resolutions[number, team, unit] = resolution

    def _landmarks(self) -> list[dict[str, Any]]:
        mode = self.initial_state.get("mode") or {}
        values: list[dict[str, Any]] = []
        for well in mode.get("wells") or []:
            values.append({
                "id": f"well-{well['wellId']}",
                "label": f"{well['wellId']} Well",
                "kind": "well",
                "position": point(well["position"]),
            })
        for reactor in mode.get("reactors") or []:
            team = int(reactor["teamId"])
            values.append({
                "id": f"reactor-team-{team}",
                "label": f"team {team} Reactor",
                "kind": "reactor",
                "teamId": team,
                "position": point(reactor["position"]),
            })
        for workshop in mode.get("workshops") or []:
            values.append({
                "id": f"workshop-{workshop['workshopId']}",
                "label": f"{workshop['workshopId']} Workshop",
                "kind": "workshop",
                "position": point(workshop["trainingTile"]),
            })
        for region in self.map.get("regions") or []:
            region_id = region["regionId"]
            if not region_id.startswith("heal-"):
                continue
            tiles = region.get("tiles") or []
            if tiles:
                values.append({
                    "id": region_id,
                    "label": region_id.replace("-", " "),
                    "kind": "heal",
                    "position": point(tiles[0]),
                })
        return sorted(values, key=lambda value: value["id"])

    def rotate(self, value: tuple[int, int]) -> tuple[int, int]:
        return self.width - 1 - value[0], self.height - 1 - value[1]

    def seat_point(self, value: tuple[int, int], team: int) -> tuple[int, int]:
        return value if team == self.teams[0] else self.rotate(value)

    def bind_authored(self, value: Sequence[int], team: int) -> tuple[int, int]:
        global_point = (int(value[0]), int(value[1]))
        return global_point if team == self.teams[0] else self.rotate(global_point)

    def relation(self, value: tuple[int, int], team: int | None) -> str | None:
        for zone in self.named_zones:
            left, top, right, bottom = zone["rect"]
            if left <= value[0] <= right and top <= value[1] <= bottom:
                return f"in zone {zone['name']}"
        candidates = list(self.landmarks)
        if not candidates:
            return None
        landmark = min(candidates, key=lambda item: (
            chebyshev(value, item["position"]), item["id"]
        ))
        distance = chebyshev(value, landmark["position"])
        label = landmark["label"]
        if landmark.get("kind") == "reactor" and landmark.get("teamId") == team:
            label = "own Reactor"
        if distance == 0:
            return f"at {label}"
        if distance <= 2:
            return f"near {label}"
        return None

    def position_model(
        self,
        value: tuple[int, int] | None,
        team: int | None,
    ) -> dict[str, Any] | None:
        if value is None:
            return None
        model: dict[str, Any] = {}
        if team is None:
            seats = {
                str(candidate): list(self.seat_point(value, candidate))
                for candidate in self.teams
            }
            if self.seat_normalize:
                model["seatByTeam"] = seats
                model["global"] = list(value)
            else:
                model["global"] = list(value)
                model["seatByTeam"] = seats
        else:
            seat = list(self.seat_point(value, team))
            if self.seat_normalize:
                model["seat"] = seat
                model["global"] = list(value)
            else:
                model["global"] = list(value)
                model["seat"] = seat
        relation = self.relation(value, team)
        if relation:
            model["relation"] = relation
        return model

    def active_life(
        self,
        tick: int,
        team: int,
        unit: int,
        *,
        post: bool = False,
    ) -> dict[str, Any] | None:
        source = self.post_lives if post else self.pre_lives
        return source.get(tick, {}).get((team, unit))

    def carrier_core(
        self,
        tick: int,
        actor: tuple[int, int, int] | None,
        *,
        post: bool = False,
    ) -> dict[str, Any] | None:
        if actor is None:
            return None
        mode = (self.post_mode if post else self.pre_mode).get(tick, {})
        for core in mode.get("visibleCores") or []:
            if actor_key(core.get("carrierActorId")) == actor:
                return core
        return None

    def workshop_state(self, tick: int, workshop: str) -> dict[str, Any] | None:
        for value in self.pre_mode.get(tick, {}).get("workshops") or []:
            if value["workshopId"] == workshop:
                return value
        return None

    def actor_events(self, tick: int, actor: tuple[int, int, int]) -> list[dict[str, Any]]:
        result = []
        for event in self.events_by_tick.get(tick, []):
            payload = event_payload(event)
            actors = [
                payload.get(name)
                for name in (
                    "actorId", "sourceActorId", "targetActorId",
                    "carrierActorId", "traineeActorId", "targetActorId",
                )
            ]
            if any(actor_key(value) == actor for value in actors if value):
                result.append(event)
        return result

    def is_walkable(self, value: tuple[int, int]) -> bool:
        x, y = value
        return (
            0 <= x < self.width
            and 0 <= y < self.height
            and self.rows[y][x] != "#"
        )

    def terrain_distance(
        self,
        start: tuple[int, int],
        goal: tuple[int, int],
    ) -> int | None:
        if not self.is_walkable(start) or not self.is_walkable(goal):
            return None
        queue = deque([(start, 0)])
        seen = {start}
        while queue:
            current, distance = queue.popleft()
            if current == goal:
                return distance
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    if dx == dy == 0:
                        continue
                    candidate = current[0] + dx, current[1] + dy
                    if candidate in seen or not self.is_walkable(candidate):
                        continue
                    if dx and dy:
                        if not self.is_walkable((current[0] + dx, current[1])):
                            continue
                        if not self.is_walkable((current[0], current[1] + dy)):
                            continue
                    seen.add(candidate)
                    queue.append((candidate, distance + 1))
        return None

    def resolve_workshop_selector(self, selector: str, team: int) -> set[str]:
        """Resolve authored Workshop vocabulary exactly as the playbook mind.

        north/south are global room ids; own/enemy are relative to the team's
        own Well by static walking distance; either includes every room.
        """
        mode = self.initial_state.get("mode") or {}
        workshops = list(mode.get("workshops") or [])
        if selector == "either":
            return {str(value["workshopId"]) for value in workshops}
        if selector in {"north", "south"}:
            return {selector}
        if selector not in {"own", "enemy"} or not workshops:
            return set()

        reactors = [
            value for value in mode.get("reactors") or []
            if int(value["teamId"]) == team
        ]
        wells = list(mode.get("wells") or [])
        if not reactors or not wells:
            return set()

        def distance(start: tuple[int, int], end: tuple[int, int]) -> int:
            value = self.terrain_distance(start, end)
            return sys.maxsize if value is None else value

        reactor = point(reactors[0]["position"])
        own_well = min(wells, key=lambda value: (
            distance(reactor, point(value["position"])),
            str(value["wellId"]),
        ))
        own_well_position = point(own_well["position"])
        ordered = sorted(workshops, key=lambda value: (
            distance(own_well_position, point(value["trainingTile"])),
            str(value["workshopId"]),
        ))
        chosen = ordered[0] if selector == "own" else ordered[-1]
        return {str(chosen["workshopId"])}


def core_label(core_id: dict[str, Any] | None) -> str | None:
    if not core_id:
        return None
    return f"{core_id['sourceWellId']}:{core_id['sourceOrdinal']}"


def action_outcome(resolution: dict[str, Any] | None) -> str:
    if not resolution:
        return "missing"
    return (resolution.get("actionResolution") or {}).get("outcome") or "missing"


def accepted_action(resolution: dict[str, Any] | None, field: str) -> str | None:
    if not resolution:
        return None
    action = (resolution.get("actionResolution") or {}).get(field)
    return None if not action else action.get("actionId")


def event_summary(event: dict[str, Any]) -> str:
    kind = event.get("kind", "?")
    payload = event_payload(event)
    if kind == "damage":
        source = unit_key(payload.get("sourceActorId"))
        target = unit_key(payload.get("targetActorId"))
        return f"damage t{source[0]}u{source[1]}→t{target[0]}u{target[1]} {payload.get('amount')}"
    if kind == "destruction":
        victim = unit_key(payload.get("actorId"))
        source = unit_key(payload.get("sourceActorId"))
        by = "environment" if source is None else f"t{source[0]}u{source[1]}"
        return f"destroyed t{victim[0]}u{victim[1]} by {by}"
    if kind == "movement-blocked":
        target = point(payload.get("attemptedTo"))
        return f"movement blocked toward {target}"
    if kind == "life-spawned":
        value = unit_key(payload.get("actorId"))
        return f"respawned t{value[0]}u{value[1]}"
    if kind == "arc-relay":
        fact = payload
        fact_kind = fact.get("kind", "arc-relay")
        if fact_kind == "core-banked":
            return f"banked {core_label(fact.get('coreId'))} +{fact.get('chargePips')}"
        if fact_kind.startswith("workshop-"):
            return fact_kind
        return fact_kind
    return kind


def simplified_path(points: Iterable[tuple[int, int]], limit: int = 12) -> list[tuple[int, int]]:
    unique: list[tuple[int, int]] = []
    for value in points:
        if not unique or unique[-1] != value:
            unique.append(value)
    if len(unique) <= 2:
        return unique
    turns = [unique[0]]
    previous_direction: tuple[int, int] | None = None
    for left, right in zip(unique, unique[1:]):
        direction = (
            (right[0] > left[0]) - (right[0] < left[0]),
            (right[1] > left[1]) - (right[1] < left[1]),
        )
        if previous_direction is not None and direction != previous_direction:
            turns.append(left)
        previous_direction = direction
    turns.append(unique[-1])
    if len(turns) <= limit:
        return turns
    indices = [round(index * (len(turns) - 1) / (limit - 1)) for index in range(limit)]
    result: list[tuple[int, int]] = []
    for index in indices:
        if not result or result[-1] != turns[index]:
            result.append(turns[index])
    return result


def class_id(life: dict[str, Any] | None) -> str | None:
    if not life:
        return None
    form = life.get("formId") or ""
    return form.removeprefix("arc-body-")


class TacticProjection:
    """Build the single analysis model consumed by every renderer."""

    def __init__(
        self,
        replay: dict[str, Any],
        tactics: dict[str, Any] | None,
        *,
        seat_normalize: bool,
    ):
        self.index = ReplayIndex(replay)
        self.index.seat_normalize = seat_normalize
        self.tactics = tactics
        self.seat_normalize = seat_normalize
        self.tactics_team = self._resolve_tactics_team(tactics)
        self._install_named_zones()
        self.program_by_tick = self._program_timeline()
        self.workshops = self._workshop_lifecycle()
        self.records = self._tick_records()
        self.transitions = self._transitions()
        self.banks = self._banks()
        self.deaths = self._deaths()
        self.engagement_starts = self._engagement_starts()

    def _install_named_zones(self) -> None:
        if self.tactics is None or self.tactics_team is None:
            return
        found: set[tuple[str, tuple[int, int, int, int]]] = set()

        def walk(value: Any) -> None:
            if isinstance(value, dict):
                if isinstance(value.get("name"), str) and len(value.get("rect") or []) == 4:
                    rect = tuple(int(item) for item in value["rect"])
                    found.add((value["name"], rect))
                for child in value.values():
                    walk(child)
            elif isinstance(value, list):
                for child in value:
                    walk(child)

        walk(self.tactics)
        zones = []
        for name, rect in sorted(found):
            first = self.index.bind_authored(rect[:2], self.tactics_team)
            second = self.index.bind_authored(rect[2:], self.tactics_team)
            zones.append({
                "name": name,
                "rect": [
                    min(first[0], second[0]), min(first[1], second[1]),
                    max(first[0], second[0]), max(first[1], second[1]),
                ],
            })
        self.index.named_zones = zones

    def _resolve_tactics_team(self, tactics: dict[str, Any] | None) -> int | None:
        if tactics is None:
            return None
        if tactics.get("schema") != "arc-relay-tactics-v2":
            raise ValueError("--check requires schema arc-relay-tactics-v2")
        sheet = tactics.get("sheetId")
        matches = [
            team for team, participant in self.index.participants.items()
            if participant.get("name") == sheet
        ]
        if len(matches) != 1:
            known = ", ".join(
                f"team {team}={value.get('name')}"
                for team, value in sorted(self.index.participants.items())
            )
            raise ValueError(
                f"tactics sheetId '{sheet}' does not uniquely match replay participants ({known})"
            )
        return matches[0]

    def _program_timeline(self) -> dict[tuple[int, int], dict[str, Any]]:
        result: dict[tuple[int, int], dict[str, Any]] = {}
        prior = {team: "idle" for team in self.index.teams}
        for tick in range(self.index.end_tick + 1):
            for team in self.index.teams:
                parsed = []
                markers: set[str] = set()
                turn = self.index.turns.get((tick, team), {})
                for command in turn.get("commands") or []:
                    attribution = parse_attribution(command.get("debugMessage") or "")
                    if attribution is None:
                        continue
                    if attribution.program:
                        parsed.append(attribution.program)
                    if attribution.marker:
                        markers.add(attribution.marker)
                if parsed:
                    # Custody-latched carriers may report an older phase.  The
                    # squad prefix remains the majority; canonical lexical
                    # order breaks a genuinely tied diagnostic deterministically.
                    counts = Counter(parsed)
                    program = sorted(counts, key=lambda value: (-counts[value], value))[0]
                    prior[team] = program
                else:
                    program = prior[team]
                result[tick, team] = {
                    "program": program,
                    "markers": sorted(markers),
                }
        return result

    def _workshop_lifecycle(self) -> dict[str, Any]:
        events: list[dict[str, Any]] = []
        sessions: list[dict[str, Any]] = []
        ready_ticks: dict[str, list[int]] = defaultdict(list)
        active: dict[str, dict[str, Any]] = {}
        for tick in range(self.index.end_tick + 1):
            for event in self.index.events_by_tick.get(tick, []):
                if event.get("kind") != "arc-relay":
                    continue
                fact = event_payload(event)
                kind = fact.get("kind", "")
                if not kind.startswith("workshop-"):
                    continue
                item = {"tick": tick, **fact}
                if "trainingTile" in item:
                    team = item.get("teamId")
                    item["position"] = self.index.position_model(
                        point(item.pop("trainingTile")), team
                    )
                events.append(item)
                workshop = fact["workshopId"]
                if kind == "workshop-ready":
                    ready_ticks[workshop].append(tick)
                elif kind == "workshop-training-started":
                    active[workshop] = {
                        "workshopId": workshop,
                        "teamId": fact["teamId"],
                        "startTick": tick,
                        "trainee": fact.get("traineeActorId"),
                    }
                elif kind in {
                    "workshop-training-completed",
                    "workshop-training-interrupted",
                }:
                    session = active.pop(workshop, {
                        "workshopId": workshop,
                        "teamId": fact.get("teamId"),
                        "startTick": None,
                    })
                    session.update({
                        "endTick": tick,
                        "outcome": "complete" if kind.endswith("completed") else "interrupted",
                    })
                    if "reason" in fact:
                        session["reason"] = fact["reason"]
                        session["progressLost"] = fact.get("progressLost", 0)
                    if "xpAward" in fact:
                        session["xpAward"] = fact["xpAward"]
                        session["trainee"] = fact.get("traineeActorId")
                    ready_again = fact.get("readyAgainTick")
                    if ready_again is not None:
                        session["readyAgainTick"] = ready_again
                        session["dormancyWindow"] = (
                            [tick + 1, ready_again - 1]
                            if ready_again > tick + 1 else None
                        )
                    sessions.append(session)
        for workshop, session in sorted(active.items()):
            sessions.append({
                **session,
                "endTick": self.index.end_tick,
                "outcome": "ongoing-at-match-end",
            })
        sessions.sort(key=lambda value: (
            value.get("startTick") if value.get("startTick") is not None else -1,
            value["workshopId"], value.get("teamId", -1),
        ))
        return {
            "events": events,
            "sessions": sessions,
            "readyTicks": dict(sorted(ready_ticks.items())),
        }

    def _activity(
        self,
        tick: int,
        team: int,
        unit: int,
        life: dict[str, Any] | None,
        command: dict[str, Any] | None,
        attribution: Attribution | None,
        reason: str,
        events: list[dict[str, Any]],
    ) -> tuple[str, str, tuple[int, int, int] | None, dict[str, Any] | None]:
        if life is None:
            return "dead", "not-active", None, None
        actor = actor_key(life.get("actorId"))
        post_life = self.index.active_life(tick, team, unit, post=True)
        post_actor = actor_key((post_life or {}).get("actorId"))
        carrier = self.index.carrier_core(tick, actor) or self.index.carrier_core(
            tick, post_actor, post=True
        )
        banked = next((
            event_payload(event)
            for event in events
            if event.get("kind") == "arc-relay"
            and event_payload(event).get("kind") == "core-banked"
            and actor_key(event_payload(event).get("carrierActorId")) == actor
        ), None)
        if banked:
            return "bank", "core-banked", None, banked
        training = parse_training_intent(reason)
        if carrier:
            return "carry", attribution.leaf_id if attribution else "carry", None, carrier
        if training:
            workshop, on_tile = training
            state = self.index.workshop_state(tick, workshop) or {}
            status = "on-tile" if on_tile else "walking"
            if on_tile and state.get("phase") not in {None, "training"}:
                status = f"stalled-{state.get('phase')}"
            return "train", f"{attribution.leaf_id if attribution else 'train'}:{status}", None, {
                "workshopId": workshop,
                "onTile": on_tile,
                "phase": state.get("phase"),
                "progressTicks": state.get("progressTicks"),
                "requiredTicks": state.get("requiredTicks"),
            }
        opponent = parse_fight_target(reason)
        leaf = attribution.leaf_id if attribution else ""
        combat_leaf = any(value in leaf for value in (
            "fight", "carrier-defence", "hunt-carrier"
        ))
        if opponent or combat_leaf or (command and command.get("actionId") in ATTACK_ACTIONS):
            key = leaf or "engage"
            if opponent:
                key += f":t{opponent[0]}u{opponent[1]}l{opponent[2]}"
            return "engage", key, opponent, None
        if "move:route:" in reason:
            goal_match = GOAL_POINT.search(reason)
            goal = tuple(map(int, goal_match.groups())) if goal_match else None
            goal_key = f":goal-{goal[0]}-{goal[1]}" if goal else ""
            return (
                "patrol",
                f"{leaf}{goal_key}:{compact_reason_state(reason)}",
                None,
                {"goal": goal},
            )
        if (
            point(life.get("position")) is not None
            and point((post_life or {}).get("position")) is not None
            and point(life.get("position")) != point(post_life.get("position"))
        ):
            return "move", f"{leaf or 'motion'}:{compact_reason_state(reason)}", None, None
        if "at-target:" in reason and any(
            value in reason for value in ("anchor:", "zone:", "point:")
        ):
            return "hold-post", f"{leaf}:{compact_reason_state(reason)}", None, None
        action = command.get("actionId") if command else "wait"
        movement_leaf = any(value in leaf for value in (
            "move", "bank", "grab", "hunt-core", "heal", "reclaim"
        ))
        if action in MOVEMENT_ACTIONS or movement_leaf:
            return "move", f"{leaf or 'move'}:{compact_reason_state(reason)}", None, None
        return "idle", f"{leaf or 'none'}:{compact_reason_state(reason)}", None, None

    def _tick_records(self) -> dict[tuple[int, int], list[dict[str, Any]]]:
        result: dict[tuple[int, int], list[dict[str, Any]]] = {}
        for team in self.index.teams:
            for unit in range(8):
                rows: list[dict[str, Any]] = []
                for tick in range(self.index.end_tick + 1):
                    life = self.index.active_life(tick, team, unit)
                    post = self.index.active_life(tick, team, unit, post=True)
                    command = self.index.commands.get((tick, team, unit))
                    resolution = self.index.resolutions.get((tick, team, unit))
                    reason = (command or {}).get("debugMessage") or ""
                    attribution = parse_attribution(reason)
                    actor = actor_key((life or {}).get("actorId"))
                    events = self.index.actor_events(tick, actor) if actor else []
                    kind, reason_key, opponent, detail = self._activity(
                        tick, team, unit, life, command, attribution, reason, events
                    )
                    pre_position = point((life or {}).get("position"))
                    post_position = point((post or {}).get("position"))
                    rows.append({
                        "tick": tick,
                        "teamId": team,
                        "unitId": unit,
                        "lifeId": (life or {}).get("actorId", {}).get("lifeId"),
                        "classId": class_id(life or post),
                        "roleId": (command or {}).get("roleTag")
                            or (attribution.role_id if attribution else None),
                        "position": pre_position,
                        "postPosition": post_position,
                        "facing": (life or {}).get("facing"),
                        "health": (life or {}).get("health"),
                        "postHealth": (post or {}).get("health"),
                        "activity": kind,
                        "reasonKey": reason_key,
                        "reason": reason,
                        "attribution": attribution,
                        "opponent": opponent,
                        "detail": detail,
                        "commandAction": (command or {}).get("actionId"),
                        "commandOutcome": (command or {}).get("outcome"),
                        "resolutionOutcome": action_outcome(resolution),
                        "acceptedAction": accepted_action(resolution, "acceptedAction"),
                        "validatedAction": accepted_action(resolution, "validatedAction"),
                        "events": events,
                        "program": self.program_by_tick[tick, team]["program"],
                    })
                result[team, unit] = rows
        return result

    def _span_key(self, row: dict[str, Any]) -> tuple[Any, ...]:
        attribution: Attribution | None = row["attribution"]
        return (
            row["lifeId"],
            row["activity"],
            row["reasonKey"],
            attribution.tactic_ordinal if attribution else None,
            attribution.phase_ordinal if attribution else None,
            attribution.group_ordinal if attribution else None,
            row["program"],
        )

    def _freeze_span(self, rows: list[dict[str, Any]]) -> dict[str, Any]:
        first, last = rows[0], rows[-1]
        team = first["teamId"]
        attribution: Attribution | None = next(
            (value["attribution"] for value in rows if value["attribution"]), None
        )
        positions = [
            value for row in rows
            for value in (row["position"], row["postPosition"])
            if value is not None
        ]
        route = simplified_path(positions)
        occupancy: list[dict[str, Any]] = []
        occupancy_start = 0
        for index in range(1, len(rows) + 1):
            prior = rows[occupancy_start]["postPosition"]
            if index < len(rows) and rows[index]["postPosition"] == prior:
                continue
            occupancy.append({
                "startTick": rows[occupancy_start]["tick"],
                "endTick": rows[index - 1]["tick"],
                "position": self.index.position_model(prior, team),
            })
            occupancy_start = index
        action_counts = Counter(
            row["commandAction"] or "none" for row in rows
        )
        outcome_counts = Counter(row["resolutionOutcome"] for row in rows)
        damage_dealt = damage_received = 0
        kills: set[tuple[int, int, int]] = set()
        destroyed = False
        event_lines: list[str] = []
        for row in rows:
            for event in row["events"]:
                payload = event_payload(event)
                kind = event.get("kind")
                actor = (team, first["unitId"], row["lifeId"])
                if kind == "damage":
                    if actor_key(payload.get("sourceActorId")) == actor:
                        damage_dealt += int(payload.get("amount", 0))
                    if actor_key(payload.get("targetActorId")) == actor:
                        damage_received += int(payload.get("amount", 0))
                elif kind == "destruction":
                    if actor_key(payload.get("sourceActorId")) == actor:
                        target = actor_key(payload.get("actorId"))
                        if target:
                            kills.add(target)
                    if actor_key(payload.get("actorId")) == actor:
                        destroyed = True
                line = event_summary(event)
                if line not in event_lines:
                    event_lines.append(line)
        opponents = sorted({row["opponent"] for row in rows if row["opponent"]})
        model: dict[str, Any] = {
            "teamId": team,
            "unitId": first["unitId"],
            "lifeId": first["lifeId"],
            "classId": next((row["classId"] for row in rows if row["classId"]), None),
            "roleId": next((row["roleId"] for row in rows if row["roleId"]), None),
            "startTick": first["tick"],
            "endTick": last["tick"],
            "ticks": len(rows),
            "kind": first["activity"],
            "reason": first["reasonKey"],
            "program": first["program"],
            "start": self.index.position_model(first["position"], team),
            "end": self.index.position_model(
                last["postPosition"] or last["position"], team
            ),
            "path": [self.index.position_model(value, team) for value in route],
            # Exact post-state tile RLE.  This keeps the compact span useful
            # for body-tick/zone proofs without duplicating the per-tick tier.
            "occupancy": occupancy,
            "movementSteps": sum(
                row["position"] is not None
                and row["postPosition"] is not None
                and row["position"] != row["postPosition"]
                for row in rows
            ),
            "actions": dict(sorted(action_counts.items())),
            "resolutions": dict(sorted(outcome_counts.items())),
        }
        if attribution:
            model["attribution"] = {
                "roleId": attribution.role_id,
                "tacticOrdinal": attribution.tactic_ordinal,
                "phaseOrdinal": attribution.phase_ordinal,
                "groupOrdinal": attribution.group_ordinal,
                "kind": attribution.kind,
                "leafId": attribution.leaf_id,
                "path": attribution.path,
            }
        if opponents:
            model["opponents"] = [
                {"teamId": value[0], "unitId": value[1], "lifeId": value[2]}
                for value in opponents
            ]
        if damage_dealt or damage_received:
            model["combat"] = {
                "damageDealt": damage_dealt,
                "damageReceived": damage_received,
                "kills": len(kills),
                "outcome": "lost" if destroyed else "won" if kills else "disengaged",
            }
        details = [row["detail"] for row in rows if row["detail"]]
        if first["activity"] == "train" and details:
            progress = [
                value.get("progressTicks") for value in details
                if value.get("progressTicks") is not None
            ]
            model["training"] = {
                "workshopId": details[0]["workshopId"],
                "onTile": any(value.get("onTile") for value in details),
                "progressStart": progress[0] if progress else None,
                "progressMaximum": max(progress) if progress else None,
                "progressEnd": progress[-1] if progress else None,
                "requiredTicks": details[-1].get("requiredTicks"),
            }
            actor = (team, first["unitId"], first["lifeId"])
            sessions = [
                value for value in self.workshops["sessions"]
                if value["workshopId"] == details[0]["workshopId"]
                and value.get("teamId") == team
                and (
                    value.get("trainee") is None
                    or actor_key(value.get("trainee")) == actor
                )
                and value.get("startTick") is not None
                and value["startTick"] <= last["tick"]
                and value["endTick"] >= first["tick"]
            ]
            if sessions:
                model["training"]["sessions"] = sessions
        if first["activity"] == "patrol" and details:
            goal = next((value.get("goal") for value in details if value.get("goal")), None)
            if goal is not None:
                actual_start = first["position"]
                actual_end = last["postPosition"] or last["position"]
                shortest = (
                    self.index.terrain_distance(actual_start, actual_end)
                    if actual_start is not None and actual_end is not None else None
                )
                arrived = actual_end is not None and chebyshev(actual_end, goal) <= 1
                model["patrol"] = {
                    "goal": self.index.position_model(goal, team),
                    "arrived": arrived,
                    "movementSteps": model["movementSteps"],
                    "shortestSteps": shortest,
                    "legDeviation": (
                        None if shortest is None
                        else model["movementSteps"] - shortest
                    ),
                }
        if first["activity"] in {"carry", "bank"} and details:
            detail = details[-1]
            core = detail.get("coreId") if isinstance(detail, dict) else None
            model["coreId"] = core_label(core)
            if first["activity"] == "bank":
                model["chargePips"] = detail.get("chargePips")
        if event_lines:
            model["events"] = event_lines
        return model

    def _spans(self) -> list[dict[str, Any]]:
        result: list[dict[str, Any]] = []
        for team, unit in sorted(self.records):
            rows = self.records[team, unit]
            start = 0
            for index in range(1, len(rows) + 1):
                if index < len(rows) and self._span_key(rows[index]) == self._span_key(rows[start]):
                    continue
                result.append(self._freeze_span(rows[start:index]))
                start = index
        patrol_state: dict[tuple[Any, ...], dict[str, Any]] = {}
        for span in result:
            patrol = span.get("patrol")
            if not patrol or not patrol.get("arrived"):
                continue
            attribution = span.get("attribution") or {}
            key = (
                span["teamId"], span["unitId"], span["lifeId"], span["program"],
                attribution.get("groupOrdinal"),
            )
            goal = tuple(patrol["goal"]["global"])
            state = patrol_state.setdefault(key, {
                "first": goal, "seen": set(), "laps": 0, "arrivals": 0,
            })
            state["arrivals"] += 1
            if (
                goal == state["first"]
                and state["arrivals"] > 1
                and len(state["seen"]) > 1
            ):
                state["laps"] += 1
                state["seen"] = set()
                patrol["lapCompleted"] = state["laps"]
            state["seen"].add(goal)
            patrol["lapsSoFar"] = state["laps"]
        return result

    def _tactic_names(
        self,
        team: int,
        program: str | None,
    ) -> tuple[str | None, str | None]:
        if self.tactics is None or team != self.tactics_team or not program or program == "idle":
            return None, None
        match = re.fullmatch(r"t(\d+)/p(\d+)", program)
        if not match:
            return None, None
        tactic_index, phase_index = int(match.group(1)) - 1, int(match.group(2)) - 1
        try:
            tactic = self.tactics["tactics"][tactic_index]
            phase = tactic["phases"][phase_index]
            return tactic["name"], phase["name"]
        except (IndexError, KeyError):
            return None, None

    def _transition_condition(
        self,
        team: int,
        prior_program: str,
        marker: str,
    ) -> str | None:
        if self.tactics is None or team != self.tactics_team:
            match = CONDITION_CASE.search(marker)
            return f"condition rule c{match.group(1)}" if match else None
        program = re.fullmatch(r"t(\d+)/p(\d+)", prior_program)
        case = CONDITION_CASE.search(marker)
        if not program or not case:
            return None
        try:
            tactic = self.tactics["tactics"][int(program.group(1)) - 1]
            if marker.startswith("abort-"):
                value = tactic["abort"]["cases"][int(case.group(1)) - 1]
            else:
                phase = tactic["phases"][int(program.group(2)) - 1]
                value = phase["continue"][int(case.group(1)) - 1]
            return describe_case(value)
        except (IndexError, KeyError, TypeError):
            return f"condition rule c{case.group(1)}"

    def _transitions(self) -> list[dict[str, Any]]:
        values: list[dict[str, Any]] = []
        for team in self.index.teams:
            prior = "idle"
            for tick in range(self.index.end_tick + 1):
                state = self.program_by_tick[tick, team]
                current = state["program"]
                markers = [
                    item
                    for marker in state["markers"]
                    for item in marker.split("+")
                ]
                if current == prior and not markers:
                    continue
                tactic_name, phase_name = self._tactic_names(team, current)
                transition: dict[str, Any] = {
                    "tick": tick,
                    "teamId": team,
                    "from": prior,
                    "to": current,
                    "markers": markers,
                }
                if tactic_name:
                    transition["tacticName"] = tactic_name
                if phase_name:
                    transition["phaseName"] = phase_name
                conditions = [
                    condition
                    for marker in markers
                    if (condition := self._transition_condition(team, prior, marker))
                ]
                if conditions:
                    transition["conditions"] = conditions
                values.append(transition)
                prior = current
        return sorted(values, key=lambda value: (value["tick"], value["teamId"]))

    def _banks(self) -> list[dict[str, Any]]:
        values = []
        for tick in range(self.index.end_tick + 1):
            for event in self.index.events_by_tick.get(tick, []):
                if event.get("kind") != "arc-relay":
                    continue
                fact = event_payload(event)
                if fact.get("kind") != "core-banked":
                    continue
                actor = fact["carrierActorId"]
                values.append({
                    "tick": tick,
                    "teamId": fact["teamId"],
                    "unitId": actor["unitId"],
                    "lifeId": actor["lifeId"],
                    "coreId": core_label(fact["coreId"]),
                    "chargePips": fact["chargePips"],
                    "position": self.index.position_model(
                        point(fact["position"]), fact["teamId"]
                    ),
                })
        return values

    def _deaths(self) -> list[dict[str, Any]]:
        values = []
        for tick in range(self.index.end_tick + 1):
            for event in self.index.events_by_tick.get(tick, []):
                if event.get("kind") != "destruction":
                    continue
                payload = event_payload(event)
                actor = payload["actorId"]
                source = payload.get("sourceActorId")
                values.append({
                    "tick": tick,
                    "teamId": actor["teamId"],
                    "unitId": actor["unitId"],
                    "lifeId": actor["lifeId"],
                    "by": None if source is None else {
                        "teamId": source["teamId"],
                        "unitId": source["unitId"],
                        "lifeId": source["lifeId"],
                    },
                    "position": self.index.position_model(
                        point(payload["position"]), actor["teamId"]
                    ),
                })
        return values

    def _engagement_starts(self) -> list[dict[str, Any]]:
        prior: set[tuple[int, int, int, int]] = set()
        values = []
        for tick in range(self.index.end_tick + 1):
            active: set[tuple[int, int, int, int]] = set()
            for (team, unit), rows in self.records.items():
                row = rows[tick]
                if row["activity"] != "engage":
                    continue
                opponent = row["opponent"]
                if opponent:
                    active.add((team, unit, opponent[0], opponent[1]))
            added = sorted(active - prior)
            if added:
                values.append({
                    "tick": tick,
                    "pairs": [
                        {
                            "fighter": {"teamId": value[0], "unitId": value[1]},
                            "opponent": {"teamId": value[2], "unitId": value[3]},
                        }
                        for value in added
                    ],
                })
            prior = active
        return values

    def _match_model(self) -> dict[str, Any]:
        rules = self.index.contract["rules"]
        result = self.index.replay["result"]
        standings = result.get("standings") or {}
        teams = []
        for team in sorted(standings.get("teams") or [], key=lambda value: value["teamId"]):
            teams.append({
                "teamId": team["teamId"],
                "rank": team.get("rank"),
                "outcome": team.get("outcome"),
                "scores": {
                    score["channel"]: score["value"]
                    for score in team.get("scores") or []
                },
            })
        return {
            "mapId": self.index.map["mapId"],
            "mapSize": [self.index.width, self.index.height],
            "rulesetId": rules["rulesetId"],
            "seed": self.index.header["seed"],
            "replayHash": self.index.replay.get("replayHash"),
            "sheets": {
                str(team): self.index.participants.get(team, {}).get("name", "unknown")
                for team in self.index.teams
            },
            "endTick": result.get("endTick"),
            "completionReason": result.get("completionReason"),
            "winnerTeamId": standings.get("winnerTeamId"),
            "teams": teams,
        }

    def build(
        self,
        *,
        include_spans: bool,
        unit: int | None,
        team: int | None,
        window: tuple[int, int] | None,
        include_frames: bool,
        include_conformance: bool,
    ) -> dict[str, Any]:
        model: dict[str, Any] = {
            "legend": legend_model(),
            "schema": SCHEMA,
            "coordinateOrder": (
                "seat-then-global" if self.seat_normalize else "global-then-seat"
            ),
            "match": self._match_model(),
            "phaseTransitions": self.transitions,
            "workshops": self.workshops,
            "coreBanks": self.banks,
            "deaths": self.deaths,
        }
        if include_spans:
            model["unitSpans"] = self._spans()
            model["engagementStarts"] = self.engagement_starts
            model["squadPhases"] = [
                {
                    "teamId": candidate_team,
                    **segment,
                    "tacticName": self._tactic_names(
                        candidate_team, segment["program"]
                    )[0],
                    "phaseName": self._tactic_names(
                        candidate_team, segment["program"]
                    )[1],
                }
                for candidate_team in self.index.teams
                for segment in self._phase_segments(candidate_team)
            ]
        if unit is not None:
            model["drillDown"] = self._drill_down(unit, team, window)
        if include_frames:
            model["frames"] = self._frames()
        if include_conformance:
            model["conformance"] = self._conformance()
        digest_value = hashlib.sha256(stable_json(model).encode("utf-8")).hexdigest()
        model["projectionHash"] = digest_value
        return model

    def _drill_down(
        self,
        unit: int,
        team: int | None,
        window: tuple[int, int] | None,
    ) -> list[dict[str, Any]]:
        start, end = window or (0, self.index.end_tick)
        start = max(0, start)
        end = min(self.index.end_tick, end)
        teams = self.index.teams if team is None else [team]
        values = []
        for candidate_team in teams:
            for row in self.records[candidate_team, unit][start:end + 1]:
                attribution: Attribution | None = row["attribution"]
                item: dict[str, Any] = {
                    "tick": row["tick"],
                    "teamId": candidate_team,
                    "unitId": unit,
                    "lifeId": row["lifeId"],
                    "position": self.index.position_model(row["position"], candidate_team),
                    "postPosition": self.index.position_model(
                        row["postPosition"], candidate_team
                    ),
                    "facing": row["facing"],
                    "health": row["health"],
                    "postHealth": row["postHealth"],
                    "activity": row["activity"],
                    "program": row["program"],
                    "command": row["commandAction"],
                    "commandAdmission": row["commandOutcome"],
                    "acceptedAction": row["acceptedAction"],
                    "validatedAction": row["validatedAction"],
                    "resolution": row["resolutionOutcome"],
                    "decisionReason": row["reason"],
                    "events": [event_summary(event) for event in row["events"]],
                }
                if attribution:
                    item["attribution"] = {
                        "roleId": attribution.role_id,
                        "program": attribution.program,
                        "marker": attribution.marker,
                        "tacticOrdinal": attribution.tactic_ordinal,
                        "phaseOrdinal": attribution.phase_ordinal,
                        "groupOrdinal": attribution.group_ordinal,
                        "leafId": attribution.leaf_id,
                    }
                values.append(item)
        return values

    def _frames(self) -> list[dict[str, Any]]:
        labels: dict[int, list[str]] = defaultdict(list)
        for transition in self.transitions:
            markers = "+".join(transition["markers"]) or (
                f"{transition['from']}→{transition['to']}"
            )
            labels[transition["tick"]].append(
                f"team {transition['teamId']} phase {markers}"
            )
        for start in self.engagement_starts:
            pairs = ",".join(
                f"t{pair['fighter']['teamId']}u{pair['fighter']['unitId']}→"
                f"t{pair['opponent']['teamId']}u{pair['opponent']['unitId']}"
                for pair in start["pairs"]
            )
            labels[start["tick"]].append(f"engagement {pairs}")
        frames = []
        for tick in sorted(labels):
            grid = [list(row) for row in self.index.rows]
            glyphs = {
                "well-north": "N", "well-centre": "C", "well-south": "S",
                "workshop-north": "^", "workshop-south": "v",
                "heal-north": "+", "heal-south": "+",
                "reactor-team-0": "L", "reactor-team-1": "R",
            }
            for landmark in self.index.landmarks:
                glyph = glyphs.get(landmark["id"])
                if glyph:
                    x, y = landmark["position"]
                    grid[y][x] = glyph
            mode = self.index.pre_mode.get(tick, {})
            for core in mode.get("visibleCores") or []:
                if core.get("disposition") != "loose":
                    continue
                core_position = point(core.get("position"))
                if core_position:
                    grid[core_position[1]][core_position[0]] = "o"
            actors = []
            for (team, unit), life in sorted(self.index.pre_lives.get(tick, {}).items()):
                actor_position = point(life["position"])
                glyph = str(unit) if team == self.index.teams[0] else chr(ord("a") + unit)
                grid[actor_position[1]][actor_position[0]] = glyph
                carrier = self.index.carrier_core(tick, actor_key(life.get("actorId")))
                actors.append({
                    "glyph": glyph,
                    "teamId": team,
                    "unitId": unit,
                    "lifeId": life["actorId"]["lifeId"],
                    "classId": class_id(life),
                    "position": self.index.position_model(actor_position, team),
                    "facing": life["facing"],
                    "health": life["health"],
                    "coreId": core_label((carrier or {}).get("coreId")),
                })
            frames.append({
                "tick": tick,
                "reasons": sorted(labels[tick]),
                "grid": ["".join(row) for row in grid],
                "actors": actors,
            })
        return frames

    def _phase_segments(self, team: int) -> list[dict[str, Any]]:
        values = []
        start = 0
        program = self.program_by_tick[0, team]["program"]
        for tick in range(1, self.index.end_tick + 2):
            current = (
                self.program_by_tick[tick, team]["program"]
                if tick <= self.index.end_tick else None
            )
            if current == program:
                continue
            values.append({"program": program, "startTick": start, "endTick": tick - 1})
            start, program = tick, current
        return values

    def _effective_job(
        self,
        tactic: dict[str, Any],
        phase_index: int,
        group_id: str,
    ) -> dict[str, Any] | None:
        for candidate in range(phase_index, -1, -1):
            job = tactic["phases"][candidate]["jobs"][group_id]
            if job.get("kind") != "none":
                return job
        return None

    def _longest_park(
        self,
        rows: list[dict[str, Any]],
    ) -> tuple[tuple[int, int] | None, int | None, int | None]:
        best: tuple[int, tuple[int, int], int, int] | None = None
        run_position: tuple[int, int] | None = None
        run_start = 0
        for index, row in enumerate(rows + [{"position": None, "tick": rows[-1]["tick"] + 1}]):
            position = row["position"]
            if position == run_position:
                continue
            if run_position is not None:
                end = rows[index - 1]["tick"]
                length = end - run_start + 1
                candidate = (length, run_position, run_start, end)
                if best is None or (candidate[0], -candidate[2], candidate[1]) > (
                    best[0], -best[2], best[1]
                ):
                    best = candidate
            run_position = position
            run_start = row["tick"]
        if best is None:
            return None, None, None
        return best[1], best[2], best[3]

    def _route_evidence(self, rows: list[dict[str, Any]]) -> str:
        vocabulary = (
            "route-bypass-unreachable", "route-bypass-stuck", "no-path",
            "move-blocked", "at-target", "active",
        )
        for token in vocabulary:
            matches = [row for row in rows if token == "active" or token in row["reason"]]
            if matches:
                target = TARGET_POINT.search(matches[-1]["reason"])
                suffix = f" toward {target.group(1)},{target.group(2)}" if target else ""
                return token + suffix
        return "no decision reason"

    def _ground_distance(
        self,
        position: tuple[int, int],
        job: dict[str, Any],
        team: int,
        slot: int,
    ) -> int | None:
        kind = job.get("kind")
        if kind == "posts":
            posts = job.get("posts") or []
            post = next((value for value in posts if value.get("slot") == slot), None)
            if post is None:
                return None
            return chebyshev(position, self.index.bind_authored(post["at"], team))
        if kind == "converge":
            return chebyshev(position, self.index.bind_authored(job["point"], team))
        if kind == "area":
            x1, y1, x2, y2 = job["rect"]
            corners = [
                self.index.bind_authored((x1, y1), team),
                self.index.bind_authored((x2, y2), team),
            ]
            left, right = sorted((corners[0][0], corners[1][0]))
            top, bottom = sorted((corners[0][1], corners[1][1]))
            dx = max(left - position[0], 0, position[0] - right)
            dy = max(top - position[1], 0, position[1] - bottom)
            return max(dx, dy)
        return None

    def _post_findings(
        self,
        tactic: dict[str, Any],
        phase: dict[str, Any],
        phase_index: int,
        group: dict[str, Any],
        job: dict[str, Any],
        segment: dict[str, Any],
    ) -> list[dict[str, Any]]:
        team = self.tactics_team
        assert team is not None
        posts = job.get("posts") or []
        by_slot = {
            int(post.get("slot", group["slots"][index])): post
            for index, post in enumerate(posts)
        }
        findings = []
        readiness_release = any(
            (rule.get("required") or {}).get("groupsSet") is not None
            for rule in phase.get("continue") or []
        )
        for slot in group["slots"]:
            post = by_slot.get(slot)
            if post is None:
                findings.append(finding(
                    "deviation",
                    f"posts {tactic['name']} / {phase['name']} / {group['name']} / u{slot}",
                    segment,
                    "no authored post is assigned to this group member",
                ))
                continue
            target = self.index.bind_authored(post["at"], team)
            rows = self.records[team, slot][segment["startTick"]:segment["endTick"] + 1]
            live = [row for row in rows if row["position"] is not None]
            exact = [row for row in live if row["position"] == target]
            ready = [row for row in live if chebyshev(row["position"], target) <= 1]
            boundary = None
            if readiness_release:
                next_tick = segment["endTick"] + 1
                track = self.records[team, slot]
                if next_tick < len(track) and track[next_tick]["position"] is not None:
                    boundary = track[next_tick]
            boundary_ready = (
                boundary is not None
                and chebyshev(boundary["position"], target) <= 1
            )
            longest_exact = 0
            longest_ready = 0
            exact_run = 0
            ready_run = 0
            for row in live:
                exact_run = exact_run + 1 if row["position"] == target else 0
                ready_run = (
                    ready_run + 1
                    if chebyshev(row["position"], target) <= 1
                    else 0
                )
                longest_exact = max(longest_exact, exact_run)
                longest_ready = max(longest_ready, ready_run)
            parked, parked_start, parked_end = (
                self._longest_park(live) if live else (None, None, None)
            )
            valid = self.index.is_walkable(target)
            # Runtime formation readiness deliberately accepts Chebyshev-one
            # around an exact post. Requiring the literal tile here made the
            # checker contradict a legal groupsSet release, especially when
            # adjacent bodies occupied the final tiles. Entering the authored
            # readiness range is the observable promise; exact occupancy is
            # still reported as the stronger placement detail.
            status = "pass" if valid and (ready or boundary_ready) else "deviation"
            target_text = self._position_text(self.index.position_model(target, team))
            if not valid:
                summary = f"authored post {target_text} is not walkable map terrain"
            elif exact:
                summary = (
                    f"arrived t{exact[0]['tick']}; exact {len(exact)}/{len(live)} body-ticks; "
                    f"longest hold {longest_exact}; "
                    f"readiness-range {len(ready)}/{len(live)} "
                    f"(longest {longest_ready})"
                )
            elif ready:
                summary = (
                    f"entered readiness range of {target_text} t{ready[0]['tick']}; "
                    f"exact 0/{len(live)} body-ticks; "
                    f"readiness-range {len(ready)}/{len(live)} "
                    f"(longest {longest_ready})"
                )
            elif boundary_ready:
                summary = (
                    f"became ready within one tile of {target_text} at the "
                    f"phase-release boundary t{boundary['tick']}; exact "
                    f"0/{len(live)} phase body-ticks"
                )
            else:
                parked_text = self._position_text(
                    self.index.position_model(parked, team)
                ) if parked else "inactive"
                summary = (
                    f"never reached {target_text}; readiness-range {len(ready)}/{len(live)}; "
                    f"parked {parked_text} t{parked_start}–t{parked_end}; "
                    f"route: {self._route_evidence(live)}"
                )
            findings.append(finding(
                status,
                f"posts {tactic['name']} / {phase['name']} / {group['name']} / u{slot}",
                segment,
                summary,
                {
                    "target": self.index.position_model(target, team),
                    "walkable": valid,
                    "arrivalTick": (
                        ready[0]["tick"] if ready
                        else boundary["tick"] if boundary_ready
                        else None
                    ),
                    "exactArrivalTick": exact[0]["tick"] if exact else None,
                    "exactBodyTicks": len(exact),
                    "longestExactHoldTicks": longest_exact,
                    "readinessBodyTicks": len(ready),
                    "longestReadinessHoldTicks": longest_ready,
                    "readyAtReleaseBoundary": boundary_ready,
                    "liveBodyTicks": len(live),
                    "parked": self.index.position_model(parked, team),
                    "parkedWindow": [parked_start, parked_end],
                },
            ))
        return findings

    def _patrol_metrics(
        self,
        rows: list[dict[str, Any]],
        route: list[tuple[int, int]],
    ) -> dict[str, Any]:
        if not route:
            return {"laps": 0, "legs": [], "nextWaypoint": None}
        team = rows[0]["teamId"]
        attempts: list[tuple[tuple[int, int], list[dict[str, Any]]]] = []
        active_goal: tuple[int, int] | None = None
        active_rows: list[dict[str, Any]] = []
        for row in rows:
            match = GOAL_POINT.search(row["reason"])
            goal = tuple(map(int, match.groups())) if match else None
            if goal is None:
                if active_rows:
                    attempts.append((active_goal, active_rows))
                    active_goal, active_rows = None, []
                continue
            if active_rows and goal != active_goal:
                attempts.append((active_goal, active_rows))
                active_rows = []
            active_goal = goal
            active_rows.append(row)
        if active_rows:
            attempts.append((active_goal, active_rows))

        laps = 0
        legs: list[dict[str, Any]] = []
        final_goal = route[-1]
        open_goal = route[-1]
        closed = len(route) > 1 and route[0] == route[-1]
        prior_goal: tuple[int, int] | None = None
        for goal, attempt in attempts:
            live = [row for row in attempt if row["position"] is not None]
            if not live:
                continue
            start_position = live[0]["position"]
            end_position = live[-1]["postPosition"] or live[-1]["position"]
            movement_steps = sum(
                row["position"] is not None
                and row["postPosition"] is not None
                and row["position"] != row["postPosition"]
                for row in live
            )
            shortest = self.index.terrain_distance(start_position, end_position)
            arrived = chebyshev(end_position, goal) <= 1
            legs.append({
                "from": self.index.position_model(prior_goal, team),
                "to": self.index.position_model(goal, team),
                "reachedFrom": self.index.position_model(start_position, team),
                "reachedTo": self.index.position_model(end_position, team),
                "startTick": live[0]["tick"],
                "endTick": live[-1]["tick"],
                "arrived": arrived,
                "movementSteps": movement_steps,
                "shortestSteps": shortest,
                "deviation": None if shortest is None else movement_steps - shortest,
            })
            if arrived and goal == (final_goal if closed else open_goal):
                laps += 1
            if arrived:
                prior_goal = goal
        next_goal = attempts[-1][0] if attempts else route[0]
        return {
            "laps": laps,
            "legs": legs,
            "nextWaypoint": self.index.position_model(next_goal, team),
        }

    def _patrol_findings(
        self,
        tactic: dict[str, Any],
        phase: dict[str, Any],
        group: dict[str, Any],
        job: dict[str, Any],
        segment: dict[str, Any],
    ) -> list[dict[str, Any]]:
        team = self.tactics_team
        assert team is not None
        route = [self.index.bind_authored(value, team) for value in job.get("path") or []]
        values = []
        for slot in group["slots"]:
            rows = self.records[team, slot][segment["startTick"]:segment["endTick"] + 1]
            metrics = self._patrol_metrics(rows, route)
            deviations = [
                value["deviation"] for value in metrics["legs"]
                if value["deviation"] is not None
            ]
            nonzero = [
                f"L{index}:{value['deviation']:+d}"
                for index, value in enumerate(metrics["legs"], 1)
                if value["deviation"] not in {None, 0}
            ]
            arrived = sum(value["arrived"] for value in metrics["legs"])
            status = "pass" if metrics["laps"] > 0 else "deviation"
            summary = (
                f"{metrics['laps']} laps; {len(metrics['legs'])} leg attempts "
                f"({arrived} arrived); per-leg movement deviations "
                f"{', '.join(nonzero) if nonzero else 'all zero'}; "
                f"{deviations.count(0)} zero"
            )
            values.append(finding(
                status,
                f"patrol {tactic['name']} / {phase['name']} / {group['name']} / u{slot}",
                segment,
                summary,
                metrics,
            ))
        return values

    def _train_findings(
        self,
        tactic: dict[str, Any],
        phase: dict[str, Any],
        group: dict[str, Any],
        job: dict[str, Any],
        segment: dict[str, Any],
    ) -> list[dict[str, Any]]:
        team = self.tactics_team
        assert team is not None
        workshop = job["workshop"]
        workshop_ids = self.index.resolve_workshop_selector(workshop, team)
        sessions = [
            value for value in self.workshops["sessions"]
            if value.get("teamId") == team
            and value["workshopId"] in workshop_ids
            and value.get("startTick") is not None
            and value["startTick"] <= segment["endTick"]
            and value["endTick"] >= segment["startTick"]
        ]
        interrupted = [value for value in sessions if value["outcome"] == "interrupted"]
        complete = [value for value in sessions if value["outcome"] == "complete"]
        phase_exit_interruptions = [
            value for value in interrupted
            if segment["endTick"] <= value["endTick"] <= segment["endTick"] + 1
        ]
        unexpected_interruptions = [
            value for value in interrupted
            if value not in phase_exit_interruptions
        ]
        cadence = []
        for session in sessions:
            ready_ticks = self.workshops["readyTicks"].get(
                session["workshopId"], []
            )
            eligible = [
                tick for tick in ready_ticks
                if tick <= session["startTick"]
            ]
            ready_tick = eligible[-1] if eligible else None
            cadence.append({
                "readyTick": ready_tick,
                "startTick": session["startTick"],
                "delayTicks": (
                    None if ready_tick is None
                    else session["startTick"] - ready_tick
                ),
            })
        first_opportunities = []
        for workshop_id in workshop_ids:
            ready_ticks = self.workshops["readyTicks"].get(workshop_id, [])
            candidates = [
                max(segment["startTick"], tick + 1)
                for tick in ready_ticks
                if tick <= segment["endTick"]
            ]
            if candidates:
                first_opportunities.append(min(candidates))
        first_opportunity = min(first_opportunities) if first_opportunities else None
        if not sessions:
            if first_opportunity is None or first_opportunity > segment["endTick"]:
                status = "note"
                future_ready = sorted(
                    tick
                    for workshop_id in workshop_ids
                    for tick in self.workshops["readyTicks"].get(workshop_id, [])
                    if tick >= segment["startTick"]
                )
                if future_ready:
                    summary = (
                        "phase ended before the first Workshop channel opportunity "
                        f"(ready t{future_ready[0]}; earliest channel t{future_ready[0] + 1})"
                    )
                else:
                    summary = (
                        "no Workshop channel session began; no public readiness "
                        "event made one possible in this phase window"
                    )
            else:
                status = "deviation"
                summary = (
                    "no Workshop channel session began despite an observable "
                    f"opportunity from t{first_opportunity}"
                )
        else:
            status = "deviation" if unexpected_interruptions else "pass"
            parts = [
                f"{value['startTick']}→{value['endTick']} {value['outcome']}"
                + (
                    f"({value.get('reason')}, lost {value.get('progressLost')})"
                    if value["outcome"] == "interrupted" else ""
                )
                + (
                    "[phase-exit]"
                    if value in phase_exit_interruptions else ""
                )
                + (
                    f"; ready t{value['readyAgainTick']}"
                    if value.get("readyAgainTick") is not None else ""
                )
                for value in sessions
            ]
            delays = [
                value["delayTicks"] for value in cadence
                if value["delayTicks"] is not None
            ]
            summary = (
                f"{len(complete)} complete, {len(interrupted)} interrupted: "
                + "; ".join(parts)
                + f"; ready-to-start delays {delays or 'unknown'}"
            )
        return [finding(
            status,
            f"train {tactic['name']} / {phase['name']} / {group['name']} / u{group['slots'][0]}",
            segment,
            summary,
            {
                "workshopId": workshop,
                "resolvedWorkshopIds": sorted(workshop_ids),
                "sessions": sessions,
                "phaseExitInterruptions": phase_exit_interruptions,
                "cadence": cadence,
                "firstChannelOpportunityTick": first_opportunity,
            },
        )]

    def _posture_finding(
        self,
        tactic: dict[str, Any],
        phase: dict[str, Any],
        phase_index: int,
        group: dict[str, Any],
        job: dict[str, Any],
        segment: dict[str, Any],
        group_ordinal: int,
    ) -> dict[str, Any]:
        team = self.tactics_team
        assert team is not None
        engage_rows = []
        starts = []
        start_contexts = []
        max_excursion = 0
        for slot in group["slots"]:
            rows = self.records[team, slot][segment["startTick"]:segment["endTick"] + 1]
            in_episode = False
            anchor: tuple[int, int] | None = None
            prior_row: dict[str, Any] | None = None
            for row in rows:
                attribution: Attribution | None = row["attribution"]
                owned = (
                    row["activity"] == "engage"
                    and attribution is not None
                    and attribution.tactic_ordinal is not None
                    and attribution.phase_ordinal == phase_index + 1
                    and attribution.group_ordinal == group_ordinal
                )
                if owned:
                    engage_rows.append(row)
                    if not in_episode:
                        starts.append(row)
                        start_contexts.append((row, prior_row))
                        anchor = row["position"]
                    if anchor is not None and row["position"] is not None:
                        max_excursion = max(max_excursion, chebyshev(anchor, row["position"]))
                in_episode = owned
                if not owned:
                    anchor = None
                prior_row = row
        posture = job.get("posture", "observe")
        status = "pass"
        if posture == "observe":
            at_ground = [
                row for row in engage_rows
                if row["position"] is not None
                and (self._ground_distance(row["position"], job, team, row["unitId"]) or 0) == 0
            ]
            if at_ground:
                status = "deviation"
            summary = (
                f"observe: {len(engage_rows)} phase-owned engage ticks "
                f"({len(at_ground)} at destination)"
            )
        elif posture == "hold":
            starts_with_distance = [
                (row, prior, distance)
                for row, prior in start_contexts
                if row["position"] is not None
                for distance in [self._ground_distance(
                    row["position"], job, team, row["unitId"]
                )]
                if distance is not None
            ]
            start_distances = [distance for _, _, distance in starts_with_distance]
            leash_edge_starts = [distance for distance in start_distances if distance == 2]
            signature_excursion_starts = [
                distance
                for _, prior, distance in starts_with_distance
                if distance > 2 and prior is not None and any(
                    token in prior["reason"]
                    for token in ("approach-deployment", ":use:")
                )
            ]
            bad_starts = [
                distance
                for _, prior, distance in starts_with_distance
                if distance > 2 and not (
                    prior is not None and any(
                        token in prior["reason"]
                        for token in ("approach-deployment", ":use:")
                    )
                )
            ]
            if bad_starts or max_excursion > 5:
                status = "deviation"
            elif leash_edge_starts or signature_excursion_starts:
                status = "note"
            summary = (
                f"hold: {len(engage_rows)} engage ticks, {len(starts)} episodes, "
                f"{len(leash_edge_starts)} starts at the defensive leash edge, "
                f"{len(signature_excursion_starts)} after an authored signature excursion, "
                f"{len(bad_starts)} beyond it, max fight excursion {max_excursion}"
            )
        elif posture == "wait":
            if engage_rows:
                status = "deviation"
            summary = f"wait: {len(engage_rows)} phase-owned engage ticks"
        else:
            summary = (
                f"{posture}: {len(engage_rows)} phase-owned engage ticks "
                f"in {len(starts)} episodes"
            )
        return finding(
            status,
            f"posture {tactic['name']} / {phase['name']} / {group['name']}",
            segment,
            summary,
            {
                "engageTicks": len(engage_rows),
                "episodes": len(starts),
                "maxExcursion": max_excursion,
                "holdStartDistances": start_distances if posture == "hold" else [],
                "holdSignatureExcursionStarts": (
                    signature_excursion_starts if posture == "hold" else []
                ),
            },
        )

    def _condition_finding(
        self,
        tactic: dict[str, Any],
        phase: dict[str, Any],
        segment: dict[str, Any],
    ) -> dict[str, Any] | None:
        rules = phase.get("continue") or []
        if not rules:
            return None
        transition = next((
            value for value in self.transitions
            if value["teamId"] == self.tactics_team
            and value["from"] == segment["program"]
            and segment["endTick"] <= value["tick"] <= segment["endTick"] + 1
            and any(CONDITION_CASE.search(marker) for marker in value["markers"])
        ), None)
        if transition:
            marker = next(
                marker for marker in transition["markers"] if CONDITION_CASE.search(marker)
            )
            index = int(CONDITION_CASE.search(marker).group(1))
            case = rules[index - 1] if 0 < index <= len(rules) else None
            return finding(
                "pass",
                f"continue {tactic['name']} / {phase['name']}",
                segment,
                f"rule c{index} fired t{transition['tick']}: "
                f"{describe_case(case) if case else 'unknown rule'}",
                {"tick": transition["tick"], "ruleIndex": index, "case": case},
            )
        return finding(
            "note",
            f"continue {tactic['name']} / {phase['name']}",
            segment,
            "no authored continuation fired in this observed phase window",
        )

    def _abort_findings(self) -> list[dict[str, Any]]:
        assert self.tactics is not None and self.tactics_team is not None
        values = []
        for transition in self.transitions:
            if transition["teamId"] != self.tactics_team:
                continue
            program = re.fullmatch(r"t(\d+)/p(\d+)", transition["from"] or "")
            if not program:
                continue
            tactic_index = int(program.group(1)) - 1
            if not 0 <= tactic_index < len(self.tactics["tactics"]):
                continue
            tactic = self.tactics["tactics"][tactic_index]
            for marker in transition["markers"]:
                case_match = CONDITION_CASE.search(marker)
                if not marker.startswith("abort-") or not case_match:
                    continue
                case_index = int(case_match.group(1))
                cases = (tactic.get("abort") or {}).get("cases") or []
                case = cases[case_index - 1] if 0 < case_index <= len(cases) else None
                values.append(finding(
                    "pass",
                    f"abort {tactic['name']}",
                    {
                        "startTick": transition["tick"],
                        "endTick": transition["tick"],
                    },
                    f"rule c{case_index} fired t{transition['tick']}: "
                    f"{describe_case(case) if case else 'unknown rule'}",
                    {
                        "tick": transition["tick"],
                        "ruleIndex": case_index,
                        "case": case,
                    },
                ))
        return values

    def _custody_findings(self) -> list[dict[str, Any]]:
        team = self.tactics_team
        assert team is not None and self.tactics is not None
        values = []
        pickups: dict[str, dict[str, Any]] = {}
        for tick in range(self.index.end_tick + 1):
            for event in self.index.events_by_tick.get(tick, []):
                if event.get("kind") != "arc-relay":
                    continue
                fact = event_payload(event)
                kind = fact.get("kind")
                if kind == "core-picked-up":
                    actor = fact["carrierActorId"]
                    if actor["teamId"] != team:
                        continue
                    core = core_label(fact["coreId"])
                    custody = pickups.get(core)
                    if custody is None:
                        program = self.program_by_tick[tick, team]["program"]
                        match = re.fullmatch(r"t(\d+)/p(\d+)", program)
                        group = None
                        tactic = phase = None
                        if match:
                            tactic = self.tactics["tactics"][int(match.group(1)) - 1]
                            phase = tactic["phases"][int(match.group(2)) - 1]
                            group = next((
                                value for value in tactic["groups"]
                                if actor["unitId"] in value["slots"]
                            ), None)
                        custody = {
                            "tick": tick,
                            "actor": actor,
                            "program": program,
                            "group": group,
                            "tactic": tactic,
                            "phase": phase,
                        }
                        pickups[core] = custody
                        pickup_text = f"acquired {core} under {program}"
                    else:
                        program = custody["program"]
                        group = custody["group"]
                        pickup_text = (
                            f"reacquired {core}; custody remains bound to {program}"
                        )
                    status = "deviation" if group and group.get("coreRule") == "ignore" else "pass"
                    summary = (
                        f"u{actor['unitId']} {pickup_text}; "
                        f"group {group['name'] if group else 'unknown'} coreRule="
                        f"{group.get('coreRule') if group else 'unknown'}"
                    )
                    values.append(finding(
                        status,
                        f"core pickup {core}",
                        {"startTick": tick, "endTick": tick},
                        summary,
                    ))
                elif kind == "core-banked" and fact.get("teamId") == team:
                    actor = fact["carrierActorId"]
                    core = core_label(fact["coreId"])
                    pickup = pickups.get(core)
                    tactic = (pickup or {}).get("tactic")
                    phase = (pickup or {}).get("phase")
                    plan = None
                    if tactic:
                        plan = (phase.get("custody") or {}).get("plan") if phase else None
                        plan = plan or tactic.get("delivery", {}).get("plan")
                    values.append(finding(
                        "pass",
                        f"core bank {core}",
                        {
                            "startTick": (pickup or {}).get("tick", tick),
                            "endTick": tick,
                        },
                        f"banked t{tick} by u{actor['unitId']}; "
                        f"pickup-phase delivery={plan or 'unknown'}",
                        {
                            "carrier": actor,
                            "pickupProgram": (pickup or {}).get("program"),
                            "deliveryPlan": plan,
                            "chargePips": fact.get("chargePips"),
                        },
                    ))
        return values

    def _conformance(self) -> dict[str, Any]:
        if self.tactics is None or self.tactics_team is None:
            raise ValueError("conformance requested without --check")
        findings: list[dict[str, Any]] = []
        declared_map = self.tactics.get("mapId")
        actual_map = self.index.map["mapId"]
        declared_size = [
            self.tactics.get("mapWidth"), self.tactics.get("mapHeight")
        ]
        actual_size = [self.index.width, self.index.height]
        findings.append(finding(
            (
                "pass"
                if declared_map == actual_map and declared_size == actual_size
                else "deviation"
            ),
            "map binding",
            {"startTick": 0, "endTick": self.index.end_tick},
            f"tactics {declared_map} {declared_size[0]}x{declared_size[1]}; "
            f"replay {actual_map} {actual_size[0]}x{actual_size[1]}",
        ))
        for segment in self._phase_segments(self.tactics_team):
            match = re.fullmatch(r"t(\d+)/p(\d+)", segment["program"] or "")
            if not match:
                continue
            tactic_index, phase_index = int(match.group(1)) - 1, int(match.group(2)) - 1
            if tactic_index >= len(self.tactics["tactics"]):
                findings.append(finding(
                    "deviation", "program attribution", segment,
                    f"replay names {segment['program']} but the tactics document "
                    "has no such tactic",
                ))
                continue
            tactic = self.tactics["tactics"][tactic_index]
            if phase_index >= len(tactic["phases"]):
                findings.append(finding(
                    "deviation", "program attribution", segment,
                    f"replay names {segment['program']} but {tactic['name']} has no such phase",
                ))
                continue
            phase = tactic["phases"][phase_index]
            for group_ordinal, group in enumerate(tactic["groups"], 1):
                job = self._effective_job(tactic, phase_index, group["id"])
                if job is None:
                    findings.append(finding(
                        "note",
                        f"ground {tactic['name']} / {phase['name']} / {group['name']}",
                        segment,
                        "no effective geometric job in this phase",
                    ))
                    continue
                kind = job.get("kind")
                if kind == "posts":
                    findings.extend(self._post_findings(
                        tactic, phase, phase_index, group, job, segment
                    ))
                elif kind == "patrol":
                    findings.extend(self._patrol_findings(
                        tactic, phase, group, job, segment
                    ))
                elif kind == "train":
                    findings.extend(self._train_findings(
                        tactic, phase, group, job, segment
                    ))
                else:
                    findings.append(finding(
                        "note",
                        f"job {kind} {tactic['name']} / {phase['name']} / {group['name']}",
                        segment,
                        "ground activity is represented in unit spans; no "
                        "stricter arrival promise is inferred",
                    ))
                findings.append(self._posture_finding(
                    tactic, phase, phase_index, group, job, segment, group_ordinal
                ))
            condition = self._condition_finding(tactic, phase, segment)
            if condition:
                findings.append(condition)
        findings.extend(self._abort_findings())
        findings.extend(self._custody_findings())
        findings.sort(key=lambda value: (
            value["evidenceWindow"][0],
            value["promise"],
            value["status"],
        ))
        return {
            "sheetId": self.tactics["sheetId"],
            "teamId": self.tactics_team,
            "summary": {
                "pass": sum(value["status"] == "pass" for value in findings),
                "deviation": sum(value["status"] == "deviation" for value in findings),
                "note": sum(value["status"] == "note" for value in findings),
            },
            "findings": findings,
        }

    def _position_text(self, model: dict[str, Any] | None) -> str:
        return format_position(model, self.seat_normalize)


def legend_model() -> dict[str, str]:
    return {
        "ticks": "tA-B is an inclusive tick span; body-ticks count one living unit for one tick",
        "units": "tNuM means team N stable unit M; lifeId distinguishes respawns",
        "programs": (
            "tN/pM means authored tactic N phase M; cK is the authored "
            "condition-row index that fired"
        ),
        "coordinates": (
            "g(x,y) is global map space; s(x,y) rotates east 180 degrees "
            "into its authored seat"
        ),
        "spanKinds": "move, hold-post, patrol, train, engage, carry, bank, dead, idle",
        "conformance": (
            "PASS matches an observable promise; DEVIATION names contrary "
            "evidence; NOTE means no stronger replay claim was derivable"
        ),
        "evidence": (
            "events/markers are authoritative; spans, engagements, laps and "
            "conformance are deterministic derivations"
        ),
        "glyphs": (
            "ASCII: 0-7 team 0; a-h team 1; # wall; N/C/S Wells; "
            "^/v Workshops; L/R Reactors; + heal; o loose Core"
        ),
    }


def describe_value(value: Any) -> str:
    if value is True:
        return "true"
    if value is False:
        return "false"
    if isinstance(value, list):
        return "[" + ",".join(describe_value(item) for item in value) + "]"
    if isinstance(value, dict):
        return "{" + ",".join(
            f"{key}={describe_value(value[key])}" for key in sorted(value)
        ) + "}"
    return str(value)


def describe_case(value: dict[str, Any] | None) -> str:
    if not value:
        return "empty condition"
    parts = []
    required = value.get("required") or {}
    triggers = value.get("triggers") or {}
    unless = value.get("unless") or {}
    if required:
        parts.append("required " + " AND ".join(
            f"{key}{describe_value(required[key])}" for key in sorted(required)
        ))
    if triggers:
        parts.append("trigger " + " OR ".join(
            f"{key}{describe_value(triggers[key])}" for key in sorted(triggers)
        ))
    if unless:
        parts.append("unless " + " OR ".join(
            f"{key}{describe_value(unless[key])}" for key in sorted(unless)
        ))
    return "; ".join(parts) or "empty condition"


def finding(
    status: str,
    promise: str,
    window: dict[str, Any],
    summary: str,
    details: dict[str, Any] | None = None,
) -> dict[str, Any]:
    value = {
        "status": status,
        "promise": promise,
        "evidenceWindow": [window["startTick"], window["endTick"]],
        "summary": summary,
    }
    if details:
        value["details"] = details
    return value


def format_position(model: dict[str, Any] | None, seat_first: bool) -> str:
    if model is None:
        return "off-map"
    global_point = model["global"]
    relation = f" {model['relation']}" if model.get("relation") else ""
    if "seat" in model:
        seat = model["seat"]
        if seat_first:
            return f"s({seat[0]},{seat[1]})/g({global_point[0]},{global_point[1]}){relation}"
        return f"g({global_point[0]},{global_point[1]})/s({seat[0]},{seat[1]}){relation}"
    seats = model.get("seatByTeam") or {}
    seat_text = "/".join(
        f"s{team}({value[0]},{value[1]})" for team, value in sorted(seats.items())
    )
    return f"g({global_point[0]},{global_point[1]})/{seat_text}{relation}"


def render_legend(model: dict[str, Any]) -> list[str]:
    legend = model["legend"]
    return [
        f"LEGEND {model['schema']}: {legend['ticks']}; {legend['units']}.",
        f"  Programs: {legend['programs']}. Coordinates: {legend['coordinates']}.",
        f"  Spans: {legend['spanKinds']}. Evidence: {legend['evidence']}.",
        f"  Check: {legend['conformance']}.",
    ]


def render_digest(model: dict[str, Any], seat_first: bool) -> list[str]:
    match = model["match"]
    lines = render_legend(model)
    lines.append(
        f"MATCH map={match['mapId']} {match['mapSize'][0]}x{match['mapSize'][1]} "
        f"profile={match['rulesetId']} seed={match['seed']} "
        f"replay={str(match.get('replayHash') or 'none')[:12]}"
    )
    sheets = " | ".join(
        f"team {team}={sheet}" for team, sheet in sorted(match["sheets"].items())
    )
    lines.append(f"SHEETS {sheets}")
    scores = []
    for team in match["teams"]:
        score = ",".join(f"{key}={value}" for key, value in sorted(team["scores"].items()))
        scores.append(f"t{team['teamId']} {team['outcome']}({score})")
    lines.append(
        f"RESULT {match['completionReason']} t{match['endTick']} winner={match['winnerTeamId']} | "
        + " | ".join(scores)
    )
    lines.append("PHASES")
    if model["phaseTransitions"]:
        for value in model["phaseTransitions"]:
            names = ""
            if value.get("tacticName"):
                names = f" {value['tacticName']} / {value.get('phaseName', '?')}"
            markers = "+".join(value["markers"]) or "state-change"
            condition = (
                " <= " + " | ".join(value["conditions"])
                if value.get("conditions") else ""
            )
            lines.append(
                f"  t{value['tick']} team {value['teamId']} {value['from']}->{value['to']} "
                f"[{markers}]{names}{condition}"
            )
    else:
        lines.append("  none published")
    lines.append("WORKSHOPS (authoritative events)")
    ready = model["workshops"]["readyTicks"]
    if ready:
        lines.append("  ready " + "; ".join(
            f"{workshop}:" + ",".join(f"t{tick}" for tick in ticks)
            for workshop, ticks in sorted(ready.items())
        ))
    sessions = model["workshops"]["sessions"]
    if not sessions:
        lines.append("  no channel sessions")
    for value in sessions:
        span = f"t{value.get('startTick')}–t{value['endTick']}"
        outcome = value["outcome"]
        detail = ""
        if outcome == "interrupted":
            phrase = INTERRUPTION_PHRASES.get(
                value.get("reason"), value.get("reason")
            )
            detail = f" reason={phrase} progressLost={value.get('progressLost')}"
        if outcome == "complete":
            detail = f" XP={value.get('xpAward')} ready=t{value.get('readyAgainTick')}"
            if value.get("dormancyWindow"):
                a, b = value["dormancyWindow"]
                detail += f" dormancy=t{a}–t{b}"
            else:
                detail += " dormancy=none"
        lines.append(
            f"  {value['workshopId']} team {value.get('teamId')} {span} {outcome}{detail}"
        )
    lines.append("BANKS")
    if not model["coreBanks"]:
        lines.append("  none")
    for value in model["coreBanks"]:
        lines.append(
            f"  t{value['tick']} team {value['teamId']} u{value['unitId']} "
            f"{value['coreId']} +{value['chargePips']} at "
            f"{format_position(value['position'], seat_first)}"
        )
    deaths = defaultdict(list)
    for value in model["deaths"]:
        by = value["by"]
        by_text = "environment" if by is None else f"t{by['teamId']}u{by['unitId']}"
        deaths[value["teamId"]].append(
            f"u{value['unitId']}l{value['lifeId']}@t{value['tick']} by {by_text} "
            f"{format_position(value['position'], seat_first)}"
        )
    lines.append("DEATHS")
    if not deaths:
        lines.append("  none")
    for team, values in sorted(deaths.items()):
        lines.append(f"  team {team}: " + "; ".join(values))
    return lines


def render_spans(model: dict[str, Any], seat_first: bool) -> list[str]:
    lines = ["", "SQUAD PHASE SPANS"]
    for phase in model.get("squadPhases") or []:
        names = ""
        if phase.get("tacticName"):
            names = f" {phase['tacticName']} / {phase.get('phaseName', '?')}"
        lines.append(
            f"  team {phase['teamId']} t{phase['startTick']}–t{phase['endTick']} "
            f"{phase['program']}{names}"
        )
    lines.append("")
    lines.append("SPANS (deterministic derived activity)")
    by_unit: dict[tuple[int, int], list[dict[str, Any]]] = defaultdict(list)
    for span in model.get("unitSpans") or []:
        by_unit[span["teamId"], span["unitId"]].append(span)
    for (team, unit), spans in sorted(by_unit.items()):
        first = next((span for span in spans if span.get("classId")), spans[0])
        lines.append(
            f"t{team}u{unit} ({first.get('classId') or 'unknown'}, "
            f"{first.get('roleId') or 'no-role'})"
        )
        reason_ids = {
            reason: f"r{index}"
            for index, reason in enumerate(
                dict.fromkeys(span["reason"] for span in spans), start=1
            )
        }
        lines.append("  reasons " + "; ".join(
            f"{reason_id}={reason}"
            for reason, reason_id in reason_ids.items()
        ))
        for span in spans:
            start, end = span["startTick"], span["endTick"]
            tick_text = f"t{start}" if start == end else f"t{start}–t{end}"
            start_position = format_position(span["start"], seat_first)
            end_position = format_position(span["end"], seat_first)
            detail = (
                f"at {start_position}"
                if span["start"] == span["end"]
                else f"{start_position}->{end_position}"
            )
            path = span.get("path") or []
            if len(path) > 2:
                detail += " via " + ",".join(
                    format_position(value, seat_first).split(" ", 1)[0]
                    for value in path[1:-1]
                )
            if span.get("training"):
                training = span["training"]
                detail += (
                    f" {training['workshopId']} progress "
                    f"{training.get('progressStart')}/{training.get('progressMaximum')}/"
                    f"{training.get('requiredTicks')}"
                )
                terminals = [
                    value for value in training.get("sessions") or []
                    if span["startTick"] <= value["endTick"] <= span["endTick"]
                ]
                for session in terminals:
                    detail += f" -> {session['outcome']}"
                    if session.get("reason"):
                        detail += (
                            f"({session['reason']},lost={session.get('progressLost', 0)})"
                        )
            if span.get("patrol"):
                patrol = span["patrol"]
                detail += (
                    f" goal {format_position(patrol['goal'], seat_first)}"
                    f" arrived={str(patrol['arrived']).lower()}"
                    f" leg-dev={patrol.get('legDeviation')}"
                )
                if patrol.get("lapCompleted"):
                    detail += f" lap={patrol['lapCompleted']}"
            if span.get("combat"):
                combat = span["combat"]
                detail += (
                    f" {combat['outcome']} dmg {combat['damageDealt']}/"
                    f"{combat['damageReceived']} kills {combat['kills']}"
                )
            if span.get("coreId"):
                detail += f" Core {span['coreId']}"
            lines.append(
                f"  {tick_text:<11} {span['kind']:<9} {detail} "
                f"[{reason_ids[span['reason']]}]"
            )
    return lines


def render_drill_down(model: dict[str, Any], seat_first: bool) -> list[str]:
    lines = ["", "DRILL-DOWN (authoritative tick data + parsed attribution)"]
    for row in model.get("drillDown") or []:
        movement = (
            f"{format_position(row['position'], seat_first)}->"
            f"{format_position(row['postPosition'], seat_first)}"
        )
        action = (
            f"{row['command'] or 'none'} admission={row['commandAdmission']} "
            f"accepted={row['acceptedAction']} validated={row['validatedAction']} "
            f"resolution={row['resolution']}"
        )
        events = "; ".join(row["events"]) or "none"
        lines.append(
            f"t{row['tick']} t{row['teamId']}u{row['unitId']}l{row['lifeId']} "
            f"{row['activity']} {movement} hp={row['health']}->{row['postHealth']} "
            f"face={row['facing']} program={row['program']}"
        )
        lines.append(f"    action {action}")
        lines.append(f"    events {events}")
        lines.append(f"    reason {row['decisionReason'] or '(none)'}")
    return lines


def render_frames(model: dict[str, Any], seat_first: bool) -> list[str]:
    lines = ["", "ASCII KEYFRAMES", "  " + model["legend"]["glyphs"]]
    width = model["match"]["mapSize"][0]
    header_tens = "".join(str((index // 10) % 10) if index >= 10 else " " for index in range(width))
    header_ones = "".join(str(index % 10) for index in range(width))
    for frame in model.get("frames") or []:
        lines.append("")
        lines.append(f"t{frame['tick']} — " + "; ".join(frame["reasons"]))
        lines.append(f"    {header_tens}")
        lines.append(f"    {header_ones}")
        for y, row in enumerate(frame["grid"]):
            lines.append(f"{y:02d}  {row}")
        for actor in frame["actors"]:
            carry = f" carry={actor['coreId']}" if actor.get("coreId") else ""
            lines.append(
                f"  {actor['glyph']} t{actor['teamId']}u{actor['unitId']}l{actor['lifeId']} "
                f"{actor['classId']} {format_position(actor['position'], seat_first)} "
                f"face={actor['facing']} hp={actor['health']}{carry}"
            )
    return lines


def render_conformance(model: dict[str, Any]) -> list[str]:
    value = model["conformance"]
    summary = value["summary"]
    lines = [
        "",
        f"CONFORMANCE sheet={value['sheetId']} team={value['teamId']} "
        f"pass={summary['pass']} deviation={summary['deviation']} note={summary['note']}",
    ]
    glyph = {"pass": "PASS", "deviation": "DEVIATION", "note": "NOTE"}
    for item in value["findings"]:
        start, end = item["evidenceWindow"]
        lines.append(
            f"  {glyph[item['status']]:9} t{start}–t{end} {item['promise']}: {item['summary']}"
        )
    return lines


def render_text(model: dict[str, Any], *, seat_first: bool) -> str:
    lines = render_digest(model, seat_first)
    if "unitSpans" in model:
        lines.extend(render_spans(model, seat_first))
    if "drillDown" in model:
        lines.extend(render_drill_down(model, seat_first))
    if "frames" in model:
        lines.extend(render_frames(model, seat_first))
    if "conformance" in model:
        lines.extend(render_conformance(model))
    lines.append("")
    lines.append(f"PROJECTION sha256={model['projectionHash']}")
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("replay", type=Path)
    parser.add_argument("--spans", action="store_true", help="include per-unit span narrative")
    parser.add_argument("--unit", type=int, choices=range(8), help="stable unit slot to inspect")
    parser.add_argument("--team", type=int, help="optional team filter for --unit")
    parser.add_argument("--window", type=parse_window, help="inclusive A:B tick slice for --unit")
    parser.add_argument("--check", type=Path, metavar="TACTICS.json")
    parser.add_argument(
        "--json", action="store_true",
        help="render the same model as JSON; implies --spans",
    )
    parser.add_argument(
        "--seat-normalize", action="store_true",
        help="print seat-relative coordinates first",
    )
    parser.add_argument(
        "--frames", action="store_true",
        help="include ASCII phase/contact keyframes",
    )
    parser.add_argument(
        "--strict", action="store_true",
        help="exit 1 when conformance has deviations",
    )
    args = parser.parse_args()
    if args.window and args.unit is None:
        parser.error("--window requires --unit")
    if args.team is not None and args.unit is None:
        parser.error("--team requires --unit")
    if args.strict and args.check is None:
        parser.error("--strict requires --check")
    try:
        replay = read_json(args.replay)
        tactics = read_json(args.check) if args.check else None
        projection = TacticProjection(
            replay,
            tactics,
            seat_normalize=args.seat_normalize,
        )
        model = projection.build(
            include_spans=args.spans or args.json,
            unit=args.unit,
            team=args.team,
            window=args.window,
            include_frames=args.frames,
            include_conformance=args.check is not None,
        )
    except (KeyError, TypeError, ValueError, OSError, json.JSONDecodeError) as error:
        print(f"arc-relay-tactic-read: {error}", file=sys.stderr)
        return 2
    if args.json:
        print(json.dumps(model, ensure_ascii=True, indent=2, sort_keys=False))
    else:
        sys.stdout.write(render_text(model, seat_first=args.seat_normalize))
    if args.strict and model["conformance"]["summary"]["deviation"]:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
