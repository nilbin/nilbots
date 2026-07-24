#!/usr/bin/env python3
"""Finite reachability lab for privately programmed projectile arcs.

This is deliberately engine-independent: it tests whether the proposed
information structure can create a prediction contest before the SDK, guest
adapter, replay format, or tracked WASM artifact changes.

The authoritative candidate is an eight-heading tile schedule:

* initial aim: forward-left / forward / forward-right;
* optional 45-degree bends in one direction;
* selectable straight run, bend cadence, and bend count (maximum 135 degrees);
* strict diagonal corners, range 8, speed 2;
* the first two tiles resolve on the firing tick after defender movement.

The shot plan is immutable and hidden. After each advance the defender learns
the exact path prefix that physically occurred, then chooses its next action.
The analysis is conservative: every completed segment is assumed visible even
when real cone vision or walls would hide it.
"""

from __future__ import annotations

import argparse
import collections
import dataclasses
import json
import math
import pathlib
from functools import lru_cache
from typing import Iterable, Iterator, Sequence


ROOT = pathlib.Path(__file__).resolve().parent.parent
Position = tuple[int, int]
Path = tuple[Position, ...]

# Clockwise, matching the game's sound-bearing octants.
HEADINGS: tuple[Position, ...] = (
    (0, -1),   # N
    (1, -1),   # NE
    (1, 0),    # E
    (1, 1),    # SE
    (0, 1),    # S
    (-1, 1),   # SW
    (-1, 0),   # W
    (-1, -1),  # NW
)
HEADING_NAMES = ("N", "NE", "E", "SE", "S", "SW", "W", "NW")
CARDINAL_HEADINGS = (0, 2, 4, 6)
ACTIONS = ("Wait", "TurnLeft", "TurnRight", "MoveForward")


@dataclasses.dataclass(frozen=True)
class ArcPlan:
    initial_offset: int
    bend_direction: int = 0
    bend_after: int = 0
    bend_every: int = 1
    bend_count: int = 0

    @property
    def label(self) -> str:
        aim = {-1: "FL", 0: "F", 1: "FR"}[self.initial_offset]
        if self.bend_count == 0:
            return aim
        bend = "L" if self.bend_direction < 0 else "R"
        return (
            f"{aim}-{bend}"
            f"@{self.bend_after}/e{self.bend_every}/x{self.bend_count}"
        )


@dataclasses.dataclass(frozen=True)
class ProgrammedPath:
    plan: ArcPlan
    tiles: Path


@dataclasses.dataclass(frozen=True)
class Arena:
    name: str
    rows: tuple[str, ...]
    zone: tuple[Position, ...] = ()

    @property
    def width(self) -> int:
        return len(self.rows[0])

    @property
    def height(self) -> int:
        return len(self.rows)

    def is_wall(self, position: Position) -> bool:
        x, y = position
        return (
            x < 0
            or y < 0
            or x >= self.width
            or y >= self.height
            or self.rows[y][x] == "#"
        )

    def floor(self) -> Iterator[Position]:
        for y, row in enumerate(self.rows):
            for x, tile in enumerate(row):
                if tile != "#":
                    yield (x, y)


@dataclasses.dataclass(frozen=True)
class LocalState:
    shooter: Position
    shooter_facing: int
    defender: Position
    defender_facing: int


@dataclasses.dataclass(frozen=True)
class StateResult:
    category: str
    path_count: int
    individually_safe: int
    universal_safe: bool
    stationary_threat: bool


def candidate_plans() -> list[ArcPlan]:
    plans: list[ArcPlan] = []
    for initial in (-1, 0, 1):
        plans.append(ArcPlan(initial))
        for bend_direction in (-1, 1):
            for bend_after in range(1, 5):
                for bend_every in range(1, 4):
                    for bend_count in range(1, 4):
                        plans.append(
                            ArcPlan(
                                initial,
                                bend_direction,
                                bend_after,
                                bend_every,
                                bend_count,
                            )
                        )
    return plans


def run_self_checks() -> None:
    arena = open_arena()
    straight = trace_plan(
        arena,
        (4, 6),
        2,
        ArcPlan(initial_offset=0),
    )
    assert straight == tuple((x, 6) for x in range(5, 13))
    assert len(candidate_plans()) == 219
    assert len(distinct_paths(arena, (4, 6), 2)) == 125

    corner = Arena(
        "strict-corner-check",
        (
            "#####",
            "##..#",
            "#...#",
            "#...#",
            "#####",
        ),
    )
    blocked = trace_plan(
        corner,
        (1, 2),
        2,
        ArcPlan(initial_offset=-1),
    )
    assert blocked == (), "diagonal launch clipped an orthogonal wall"


def step_is_blocked(
    arena: Arena,
    current: Position,
    heading: int,
) -> bool:
    dx, dy = HEADINGS[heading]
    target = (current[0] + dx, current[1] + dy)
    if arena.is_wall(target):
        return True
    if dx != 0 and dy != 0:
        # Strict corners: a center-to-center diagonal may not clip either
        # orthogonally adjacent wall.
        if arena.is_wall((current[0] + dx, current[1])):
            return True
        if arena.is_wall((current[0], current[1] + dy)):
            return True
    return False


def trace_plan(
    arena: Arena,
    origin: Position,
    facing: int,
    plan: ArcPlan,
    shot_range: int = 8,
) -> Path:
    position = origin
    heading = (facing + plan.initial_offset) % 8
    turns = 0
    path: list[Position] = []
    for tiles_moved in range(shot_range):
        should_bend = (
            turns < plan.bend_count
            and tiles_moved >= plan.bend_after
            and (tiles_moved - plan.bend_after) % plan.bend_every == 0
        )
        if should_bend:
            heading = (heading + plan.bend_direction) % 8
            turns += 1
        if step_is_blocked(arena, position, heading):
            break
        dx, dy = HEADINGS[heading]
        position = (position[0] + dx, position[1] + dy)
        path.append(position)
    return tuple(path)


def distinct_paths(
    arena: Arena,
    origin: Position,
    facing: int,
) -> tuple[ProgrammedPath, ...]:
    by_path: dict[Path, ArcPlan] = {}
    for plan in candidate_plans():
        path = trace_plan(arena, origin, facing, plan)
        if path:
            by_path.setdefault(path, plan)
    return tuple(
        ProgrammedPath(plan, path)
        for path, plan in sorted(
            by_path.items(),
            key=lambda item: (item[0], item[1].label),
        )
    )


class DefenderPolicyAnalyzer:
    """Checks robust defender policies as hidden path prefixes are revealed."""

    def __init__(
        self,
        arena: Arena,
        shooter: Position,
        launch_tiles: int = 2,
        speed: int = 2,
        shot_range: int = 8,
    ) -> None:
        self.arena = arena
        self.shooter = shooter
        self.launch_tiles = launch_tiles
        self.speed = speed
        self.max_phases = 1 + math.ceil(
            max(0, shot_range - launch_tiles) / speed
        )

    def phase_bounds(self, phase: int) -> tuple[int, int]:
        if phase == 0:
            return 0, self.launch_tiles
        start = self.launch_tiles + (phase - 1) * self.speed
        return start, start + self.speed

    def resulting_state(
        self,
        position: Position,
        facing: int,
        action: str,
    ) -> tuple[Position, int]:
        if action == "TurnLeft":
            return position, (facing - 2) % 8
        if action == "TurnRight":
            return position, (facing + 2) % 8
        if action == "MoveForward":
            dx, dy = HEADINGS[facing]
            target = (position[0] + dx, position[1] + dy)
            if not self.arena.is_wall(target) and target != self.shooter:
                return target, facing
        return position, facing

    def safe_policy(
        self,
        paths: Sequence[Path],
        position: Position,
        facing: int,
    ) -> bool:
        canonical = tuple(sorted(set(paths)))
        return self._safe_policy(canonical, position, facing, 0)

    def safe_initial_actions(
        self,
        paths: Sequence[Path],
        position: Position,
        facing: int,
    ) -> tuple[str, ...]:
        canonical = tuple(sorted(set(paths)))
        return tuple(
            action
            for action in ACTIONS
            if self._safe_after_action(canonical, position, facing, 0, action)
        )

    @lru_cache(maxsize=None)
    def _safe_policy(
        self,
        paths: tuple[Path, ...],
        position: Position,
        facing: int,
        phase: int,
    ) -> bool:
        if phase >= self.max_phases or not paths:
            return True
        return any(
            self._safe_after_action(
                paths,
                position,
                facing,
                phase,
                action,
            )
            for action in ACTIONS
        )

    def _safe_after_action(
        self,
        paths: tuple[Path, ...],
        position: Position,
        facing: int,
        phase: int,
        action: str,
    ) -> bool:
        moved, next_facing = self.resulting_state(position, facing, action)
        start, end = self.phase_bounds(phase)
        groups: dict[Path, list[Path]] = collections.defaultdict(list)

        for path in paths:
            if start >= len(path):
                continue

            # Existing projectiles occupy their current tile before advancing.
            if phase > 0 and moved == path[start - 1]:
                return False

            traversed = path[start : min(end, len(path))]
            if moved in traversed:
                return False

            # Reaching a wall/range endpoint despawns the projectile. Otherwise,
            # the physically revealed prefix determines the next information set.
            if end < len(path):
                groups[path[:end]].append(path)

        return all(
            self._safe_policy(
                tuple(sorted(group)),
                moved,
                next_facing,
                phase + 1,
            )
            for group in groups.values()
        )


def classify_state(
    arena: Arena,
    state: LocalState,
    programmed: Sequence[ProgrammedPath],
    launch_tiles: int,
) -> StateResult:
    paths = tuple(item.tiles for item in programmed)
    analyzer = DefenderPolicyAnalyzer(
        arena,
        state.shooter,
        launch_tiles=launch_tiles,
    )
    safe_count = sum(
        analyzer.safe_policy((path,), state.defender, state.defender_facing)
        for path in paths
    )
    universal = analyzer.safe_policy(paths, state.defender, state.defender_facing)
    stationary_threat = any(state.defender in path for path in paths)

    if not stationary_threat:
        category = "irrelevant"
    elif universal:
        category = "universal-defence"
    elif safe_count == len(paths):
        category = "prediction-contest"
    else:
        category = "forced-attack"
    return StateResult(
        category,
        len(paths),
        safe_count,
        universal,
        stationary_threat,
    )


def open_arena() -> Arena:
    width = 15
    height = 13
    rows = ["#" * width]
    rows.extend("#" + "." * (width - 2) + "#" for _ in range(height - 2))
    rows.append("#" * width)
    return Arena("open-floor", tuple(rows))


def load_ranked_maps() -> list[Arena]:
    arenas: list[Arena] = []
    for path in sorted((ROOT / "maps").glob("*.json")):
        data = json.loads(path.read_text())
        arenas.append(
            Arena(
                data["id"],
                tuple(data["tiles"]),
                tuple(tuple(tile) for tile in data.get("zone", [])),
            )
        )
    return arenas


def in_forward_cone(origin: Position, target: Position, facing: int) -> bool:
    dx = target[0] - origin[0]
    dy = target[1] - origin[1]
    fx, fy = HEADINGS[facing]
    forward = dx * fx + dy * fy
    lateral = abs(dx * fy) + abs(dy * fx)
    return forward >= 0 and lateral <= forward


def supercover_line(a: Position, b: Position) -> Iterator[Position]:
    dx = b[0] - a[0]
    dy = b[1] - a[1]
    nx = abs(dx)
    ny = abs(dy)
    sx = (dx > 0) - (dx < 0)
    sy = (dy > 0) - (dy < 0)
    x, y = a
    yield a
    ix = iy = 0
    while ix < nx or iy < ny:
        cross_x = (1 + 2 * ix) * ny
        cross_y = (1 + 2 * iy) * nx
        if cross_x == cross_y:
            yield (x + sx, y)
            yield (x, y + sy)
            x += sx
            y += sy
            ix += 1
            iy += 1
        elif cross_x < cross_y:
            x += sx
            ix += 1
        else:
            y += sy
            iy += 1
        yield (x, y)


def has_line_of_sight(arena: Arena, a: Position, b: Position) -> bool:
    for tile in supercover_line(a, b):
        if tile in (a, b):
            continue
        if arena.is_wall(tile):
            return False
    return True


def sampled(items: Sequence[LocalState], limit: int) -> list[LocalState]:
    if len(items) <= limit:
        return list(items)
    return [items[index * len(items) // limit] for index in range(limit)]


def open_states(arena: Arena) -> list[LocalState]:
    shooter = (4, 6)
    states: list[LocalState] = []
    for dx in range(2, 5):
        for dy in range(-dx, dx + 1):
            defender = (shooter[0] + dx, shooter[1] + dy)
            for facing in CARDINAL_HEADINGS:
                states.append(LocalState(shooter, 2, defender, facing))
    return states


def ranked_zone_states(arena: Arena, limit: int) -> list[LocalState]:
    states: list[LocalState] = []
    floors = list(arena.floor())
    for defender in arena.zone:
        for shooter in floors:
            distance = max(
                abs(defender[0] - shooter[0]),
                abs(defender[1] - shooter[1]),
            )
            if distance < 2 or distance > 4:
                continue
            for shooter_facing in CARDINAL_HEADINGS:
                if not in_forward_cone(shooter, defender, shooter_facing):
                    continue
                for defender_facing in CARDINAL_HEADINGS:
                    states.append(
                        LocalState(
                            shooter,
                            shooter_facing,
                            defender,
                            defender_facing,
                        )
                    )
    states.sort(
        key=lambda state: (
            state.defender,
            state.shooter,
            state.shooter_facing,
            state.defender_facing,
        )
    )
    return sampled(states, limit)


def analyze_states(
    arena: Arena,
    states: Sequence[LocalState],
    launch_tiles: int,
) -> tuple[collections.Counter[str], list[tuple[LocalState, StateResult, tuple[ProgrammedPath, ...]]]]:
    counts: collections.Counter[str] = collections.Counter()
    details = []
    catalogues: dict[tuple[Position, int], tuple[ProgrammedPath, ...]] = {}
    for state in states:
        key = (state.shooter, state.shooter_facing)
        programmed = catalogues.setdefault(
            key,
            distinct_paths(arena, state.shooter, state.shooter_facing),
        )
        result = classify_state(
            arena,
            state,
            programmed,
            launch_tiles,
        )
        counts[result.category] += 1
        details.append((state, result, programmed))
    return counts, details


def around_wall_examples(
    arena: Arena,
    limit: int = 3,
) -> list[tuple[Position, int, ProgrammedPath]]:
    examples: list[tuple[Position, int, ProgrammedPath]] = []
    for origin in arena.floor():
        for facing in CARDINAL_HEADINGS:
            for programmed in distinct_paths(arena, origin, facing):
                path = programmed.tiles
                if programmed.plan.bend_count == 0 or len(path) < 3:
                    continue
                endpoint = path[-1]
                if has_line_of_sight(arena, origin, endpoint):
                    continue
                examples.append((origin, facing, programmed))
                if len(examples) >= limit:
                    return examples
    return examples


def format_counts(counts: collections.Counter[str], total: int) -> str:
    categories = (
        "prediction-contest",
        "universal-defence",
        "forced-attack",
        "irrelevant",
    )
    return "  ".join(
        f"{category}={counts[category]}/{total} ({100 * counts[category] / total:.1f}%)"
        for category in categories
    )


def print_open_breakdown(
    details: Sequence[tuple[LocalState, StateResult, tuple[ProgrammedPath, ...]]],
) -> None:
    by_distance: dict[int, collections.Counter[str]] = collections.defaultdict(
        collections.Counter
    )
    by_facing: dict[str, collections.Counter[str]] = collections.defaultdict(
        collections.Counter
    )
    for state, result, _ in details:
        distance = max(
            abs(state.defender[0] - state.shooter[0]),
            abs(state.defender[1] - state.shooter[1]),
        )
        by_distance[distance][result.category] += 1
        by_facing[HEADING_NAMES[state.defender_facing]][result.category] += 1
    for distance, counts in sorted(by_distance.items()):
        print(
            f"  distance {distance}: "
            f"{format_counts(counts, sum(counts.values()))}"
        )
    for facing, counts in sorted(by_facing.items()):
        print(
            f"  defender facing {facing}: "
            f"{format_counts(counts, sum(counts.values()))}"
        )


def path_text(origin: Position, path: Path) -> str:
    points = (origin, *path)
    return "→".join(f"({x},{y})" for x, y in points)


def print_prediction_witness(
    arena: Arena,
    details: Sequence[tuple[LocalState, StateResult, tuple[ProgrammedPath, ...]]],
    launch_tiles: int,
) -> None:
    witness = next(
        (
            item
            for item in details
            if item[1].category == "prediction-contest"
        ),
        None,
    )
    if witness is None:
        print("  no prediction-contest witness")
        return

    state, _, programmed = witness
    analyzer = DefenderPolicyAnalyzer(
        arena,
        state.shooter,
        launch_tiles=launch_tiles,
    )
    rows = []
    for item in programmed:
        safe = analyzer.safe_initial_actions(
            (item.tiles,),
            state.defender,
            state.defender_facing,
        )
        if len(safe) == len(ACTIONS):
            continue
        rows.append((item, safe))

    # Greedily retain a compact collection whose safe-action intersection is empty.
    selected: list[tuple[ProgrammedPath, tuple[str, ...]]] = []
    intersection = set(ACTIONS)
    for item, safe in sorted(rows, key=lambda row: len(row[1])):
        narrowed = intersection.intersection(safe)
        if narrowed == intersection:
            continue
        selected.append((item, safe))
        intersection = narrowed
        if not intersection or len(selected) >= 5:
            break

    print(
        "  witness "
        f"shooter={state.shooter}/{HEADING_NAMES[state.shooter_facing]} "
        f"defender={state.defender}/{HEADING_NAMES[state.defender_facing]}"
    )
    print("  plan                     Wait  Left  Right  Move   path")
    for item, safe in selected:
        marks = ["safe" if action in safe else "HIT" for action in ACTIONS]
        print(
            f"  {item.plan.label:<24} "
            f"{marks[0]:>4}  {marks[1]:>4}  {marks[2]:>5}  {marks[3]:>4}   "
            f"{path_text(state.shooter, item.tiles)}"
        )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--zone-state-limit",
        type=int,
        default=96,
        help="deterministic sample count per ranked map",
    )
    parser.add_argument(
        "--launch-tiles",
        type=int,
        choices=(1, 2),
        default=1,
        help="tiles traversed immediately on the firing tick",
    )
    args = parser.parse_args()

    run_self_checks()
    plans = candidate_plans()
    print("=== PROGRAMMED ARC THEORY LAB ===")
    print(
        f"parameter plans={len(plans)}  range=8  speed=2  "
        f"immediate launch={args.launch_tiles}  max sweep=135°"
    )

    open_map = open_arena()
    open_catalogue = distinct_paths(open_map, (4, 6), 2)
    print(f"open-floor distinct paths={len(open_catalogue)}")

    states = open_states(open_map)
    open_counts, open_details = analyze_states(
        open_map,
        states,
        args.launch_tiles,
    )
    print(f"open distance 2-4: {format_counts(open_counts, len(states))}")
    print_open_breakdown(open_details)
    print_prediction_witness(
        open_map,
        open_details,
        args.launch_tiles,
    )

    print("\n=== RANKED ZONE SAMPLES ===")
    around_wall_total = 0
    prediction_maps = 0
    for arena in load_ranked_maps():
        zone_states = ranked_zone_states(arena, args.zone_state_limit)
        counts, _ = analyze_states(
            arena,
            zone_states,
            args.launch_tiles,
        )
        examples = around_wall_examples(arena, limit=1)
        around_wall_total += bool(examples)
        prediction_maps += counts["prediction-contest"] > 0
        print(
            f"{arena.name:<14} states={len(zone_states):>3}  "
            f"{format_counts(counts, len(zone_states))}  "
            f"around-wall={'yes' if examples else 'no'}"
        )
        if examples:
            origin, facing, programmed = examples[0]
            print(
                f"  corner witness {origin}/{HEADING_NAMES[facing]} "
                f"{programmed.plan.label}: {path_text(origin, programmed.tiles)}"
            )

    ranked_count = len(load_ranked_maps())
    print("\n=== STRUCTURAL GATE ===")
    checks = {
        "open prediction contest exists": open_counts["prediction-contest"] > 0,
        "prediction contest exists on every ranked map": prediction_maps == ranked_count,
        "around-wall path exists on every ranked map": around_wall_total == ranked_count,
        "open path catalogue <= 128": len(open_catalogue) <= 128,
        "open envelope not entirely universal defence":
            open_counts["universal-defence"] < len(states),
        "open envelope not entirely forced attack":
            open_counts["forced-attack"] < len(states),
    }
    for label, passed in checks.items():
        print(f"{'PASS' if passed else 'FAIL'}  {label}")
    return 0 if all(checks.values()) else 2


if __name__ == "__main__":
    raise SystemExit(main())
