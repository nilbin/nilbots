#!/usr/bin/env python3
"""Measure, validate, and render deterministic Arc Relay grid routes."""

from __future__ import annotations

import argparse
from collections import deque
from dataclasses import dataclass
import html
import json
from pathlib import Path
import runpy
import sys
from typing import Any, Iterable


Point = tuple[int, int]


class SpecError(ValueError):
    """Raised when an analysis specification is invalid."""


def point(value: Any, field: str) -> Point:
    if (
        not isinstance(value, list)
        or len(value) != 2
        or isinstance(value[0], bool)
        or isinstance(value[1], bool)
        or not isinstance(value[0], int)
        or not isinstance(value[1], int)
    ):
        raise SpecError(f"{field} must be [x, y] integers")
    return value[0], value[1]


def points(values: Any, field: str) -> list[Point]:
    if not isinstance(values, list):
        raise SpecError(f"{field} must be an array")
    return [point(value, f"{field}[{index}]") for index, value in enumerate(values)]


@dataclass
class Grid:
    rows: list[list[str]]
    wall: str = "#"

    @property
    def width(self) -> int:
        return len(self.rows[0])

    @property
    def height(self) -> int:
        return len(self.rows)

    def in_bounds(self, position: Point) -> bool:
        x, y = position
        return 0 <= x < self.width and 0 <= y < self.height

    def is_open(self, position: Point) -> bool:
        x, y = position
        return self.in_bounds(position) and self.rows[y][x] != self.wall

    def step_ok(self, start: Point, end: Point) -> bool:
        dx, dy = end[0] - start[0], end[1] - start[1]
        if max(abs(dx), abs(dy)) != 1 or not self.is_open(end):
            return False
        if dx == 0 or dy == 0:
            return True
        return self.is_open((start[0] + dx, start[1])) and self.is_open(
            (start[0], start[1] + dy)
        )

    def neighbours(self, position: Point, blocked: set[Point] | None = None) -> Iterable[Point]:
        blocked = blocked or set()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                if not (dx or dy):
                    continue
                candidate = position[0] + dx, position[1] + dy
                if candidate not in blocked and self.step_ok(position, candidate):
                    yield candidate

    def shortest_path(
        self,
        start: Point,
        end: Point,
        blocked: set[Point] | None = None,
    ) -> list[Point] | None:
        blocked = set(blocked or set()) - {start, end}
        if not self.is_open(start) or not self.is_open(end):
            return None
        queue = deque([start])
        previous: dict[Point, Point | None] = {start: None}
        while queue:
            current = queue.popleft()
            if current == end:
                break
            for candidate in self.neighbours(current, blocked):
                if candidate not in previous:
                    previous[candidate] = current
                    queue.append(candidate)
        if end not in previous:
            return None
        result: list[Point] = []
        current: Point | None = end
        while current is not None:
            result.append(current)
            current = previous[current]
        return list(reversed(result))

    @staticmethod
    def supercover_line(start: Point, end: Point) -> list[Point]:
        """Match the engine's integer corner-strict supercover ray."""
        dx, dy = end[0] - start[0], end[1] - start[1]
        nx, ny = abs(dx), abs(dy)
        sx = (dx > 0) - (dx < 0)
        sy = (dy > 0) - (dy < 0)
        x, y = start
        result = [start]
        ix = iy = 0
        while ix < nx or iy < ny:
            cross_x = (1 + 2 * ix) * ny
            cross_y = (1 + 2 * iy) * nx
            if cross_x == cross_y:
                result.extend(((x + sx, y), (x, y + sy)))
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
            result.append((x, y))
        return result

    def has_line_of_sight(self, start: Point, end: Point) -> bool:
        return all(
            position in (start, end) or self.is_open(position)
            for position in self.supercover_line(start, end)
        )

    def open_tiles(self) -> set[Point]:
        return {
            (x, y)
            for y in range(self.height)
            for x in range(self.width)
            if self.is_open((x, y))
        }

    def components(self, excluded: Point | None = None) -> list[set[Point]]:
        remaining = self.open_tiles()
        if excluded is not None:
            remaining.discard(excluded)
        result: list[set[Point]] = []
        while remaining:
            start = min(remaining, key=lambda value: (value[1], value[0]))
            component = {start}
            queue = deque([start])
            remaining.remove(start)
            while queue:
                current = queue.popleft()
                for candidate in self.neighbours(current):
                    if candidate in remaining:
                        remaining.remove(candidate)
                        component.add(candidate)
                        queue.append(candidate)
            result.append(component)
        return result

    def false_corner_touches(self) -> list[tuple[Point, Point]]:
        floor = self.open_tiles()
        result: list[tuple[Point, Point]] = []
        for x, y in sorted(floor, key=lambda value: (value[1], value[0])):
            for dx, dy in ((1, 1), (1, -1)):
                diagonal = x + dx, y + dy
                if (
                    diagonal in floor
                    and (x + dx, y) not in floor
                    and (x, y + dy) not in floor
                ):
                    result.append(((x, y), diagonal))
        return result

    def meaningful_cut_vertices(self, stub_allowance: int) -> list[tuple[Point, int]]:
        result: list[tuple[Point, int]] = []
        for candidate in sorted(self.open_tiles(), key=lambda value: (value[1], value[0])):
            parts = self.components(candidate)
            if len(parts) > 1:
                severed = min(len(part) for part in parts)
                if severed > stub_allowance:
                    result.append((candidate, severed))
        return result


def resolve_path(candidate: str, spec_path: Path) -> Path:
    path = Path(candidate).expanduser()
    if path.is_absolute():
        return path
    from_working_directory = Path.cwd() / path
    if from_working_directory.exists():
        return from_working_directory
    return spec_path.parent / path


def load_rows(map_spec: dict[str, Any], spec_path: Path) -> list[str]:
    sources = sum(key in map_spec for key in ("rows", "rowsFile", "builder"))
    if sources != 1:
        raise SpecError("map must define exactly one of rows, rowsFile, or builder")
    if "rows" in map_spec:
        rows = map_spec["rows"]
    elif "rowsFile" in map_spec:
        rows_path = resolve_path(str(map_spec["rowsFile"]), spec_path)
        rows = rows_path.read_text(encoding="utf-8").splitlines()
    else:
        builder_path = resolve_path(str(map_spec["builder"]), spec_path)
        namespace = runpy.run_path(str(builder_path))
        function_name = str(map_spec.get("function", "build"))
        function = namespace.get(function_name)
        if not callable(function):
            raise SpecError(f"builder {builder_path} has no callable {function_name}()")
        rows = function()
    if not isinstance(rows, (list, tuple)) or not rows:
        raise SpecError("map rows must be a non-empty array")
    if not all(isinstance(row, str) and row for row in rows):
        raise SpecError("every map row must be a non-empty string")
    width = len(rows[0])
    if any(len(row) != width for row in rows):
        raise SpecError("map rows must all have equal width")
    return list(rows)


def build_grid(spec: dict[str, Any], spec_path: Path) -> tuple[Grid, list[Point], list[Point]]:
    map_spec = spec.get("map")
    if not isinstance(map_spec, dict):
        raise SpecError("map must be an object")
    wall = str(map_spec.get("wall", "#"))
    if len(wall) != 1:
        raise SpecError("map.wall must be one character")
    rows = [list(row) for row in load_rows(map_spec, spec_path)]
    grid = Grid(rows, wall)
    added = points(map_spec.get("addWalls", []), "map.addWalls")
    removed = points(map_spec.get("removeWalls", []), "map.removeWalls")
    if set(added) & set(removed):
        raise SpecError("the same tile cannot be added and removed as a wall")
    for position in added:
        if not grid.in_bounds(position):
            raise SpecError(f"added wall {position} is outside the map")
        grid.rows[position[1]][position[0]] = wall
    open_character = str(map_spec.get("open", "."))
    if len(open_character) != 1 or open_character == wall:
        raise SpecError("map.open must be one non-wall character")
    for position in removed:
        if not grid.in_bounds(position):
            raise SpecError(f"removed wall {position} is outside the map")
        grid.rows[position[1]][position[0]] = open_character
    return grid, added, removed


def parse_landmarks(
    spec: dict[str, Any], grid: Grid
) -> tuple[list[dict[str, Any]], dict[str, Point], list[str]]:
    raw_landmarks = spec.get("landmarks", [])
    if not isinstance(raw_landmarks, list):
        raise SpecError("landmarks must be an array")
    landmarks: list[dict[str, Any]] = []
    positions: dict[str, Point] = {}
    errors: list[str] = []
    for index, raw in enumerate(raw_landmarks):
        if not isinstance(raw, dict):
            raise SpecError(f"landmarks[{index}] must be an object")
        landmark_id = str(raw.get("id", "")).strip()
        if not landmark_id or landmark_id in positions:
            raise SpecError(f"landmarks[{index}].id must be non-empty and unique")
        position = point(raw.get("position"), f"landmarks[{index}].position")
        if not grid.in_bounds(position):
            raise SpecError(f"landmark {landmark_id} is outside the map")
        kind = str(raw.get("kind", "other")).strip() or "other"
        label = str(raw.get("label", landmark_id))
        protected = bool(raw.get("protected", True))
        if protected and not grid.is_open(position):
            errors.append(f"protected landmark {landmark_id} is walled at {position}")
        positions[landmark_id] = position
        landmarks.append(
            {
                "id": landmark_id,
                "position": list(position),
                "kind": kind,
                "label": label,
                "name": str(raw.get("name", landmark_id)),
                "team": raw.get("team"),
                "symmetryExempt": bool(raw.get("symmetryExempt", False)),
            }
        )
    requirements = spec.get("landmarkRequirements", {})
    if not isinstance(requirements, dict):
        raise SpecError("landmarkRequirements must be an object")
    for kind, expected in requirements.items():
        if isinstance(expected, bool) or not isinstance(expected, int) or expected < 0:
            raise SpecError(f"landmarkRequirements.{kind} must be a non-negative integer")
        actual = sum(landmark["kind"] == kind for landmark in landmarks)
        if actual != expected:
            errors.append(f"expected {expected} {kind} landmarks, found {actual}")
    required_ids = spec.get("requiredLandmarkIds", [])
    if not isinstance(required_ids, list) or not all(
        isinstance(value, str) and value for value in required_ids
    ):
        raise SpecError("requiredLandmarkIds must be an array of non-empty strings")
    missing_ids = sorted(set(required_ids) - set(positions))
    if missing_ids:
        errors.append(f"missing required landmarks: {', '.join(missing_ids)}")
    return landmarks, positions, errors


def resolve_endpoint(value: Any, field: str, landmarks: dict[str, Point]) -> Point:
    if isinstance(value, str):
        if value not in landmarks:
            raise SpecError(f"{field} names unknown landmark {value!r}")
        return landmarks[value]
    return point(value, field)


def join_paths(parts: list[list[Point]]) -> list[Point]:
    result: list[Point] = []
    for part in parts:
        result.extend(part if not result else part[1:])
    return result


def route_path(
    raw: dict[str, Any],
    index: int,
    grid: Grid,
    landmarks: dict[str, Point],
) -> list[Point]:
    start = resolve_endpoint(raw.get("from"), f"routes[{index}].from", landmarks)
    end = resolve_endpoint(raw.get("to"), f"routes[{index}].to", landmarks)
    mode = str(raw.get("mode", "shortest"))
    blocked = set(points(raw.get("avoid", []), f"routes[{index}].avoid"))
    if mode == "shortest":
        result = grid.shortest_path(start, end, blocked)
        if result is None:
            raise SpecError(f"routes[{index}] has no legal path")
    elif mode == "via":
        raw_via = raw.get("via")
        if not isinstance(raw_via, list) or not raw_via:
            raise SpecError(f"routes[{index}].via must be a non-empty array")
        via = [
            resolve_endpoint(value, f"routes[{index}].via[{via_index}]", landmarks)
            for via_index, value in enumerate(raw_via)
        ]
        route_points = [start, *via, end]
        parts: list[list[Point]] = []
        for segment_index, (segment_start, segment_end) in enumerate(
            zip(route_points, route_points[1:])
        ):
            part = grid.shortest_path(segment_start, segment_end, blocked)
            if part is None:
                raise SpecError(
                    f"routes[{index}] segment {segment_index} has no legal path"
                )
            parts.append(part)
        result = join_paths(parts)
    elif mode == "authored":
        result = points(raw.get("path"), f"routes[{index}].path")
        if not result or result[0] != start or result[-1] != end:
            raise SpecError(f"routes[{index}].path must begin at from and end at to")
    else:
        raise SpecError(f"routes[{index}].mode must be shortest, via, or authored")
    for step_index, position in enumerate(result):
        if not grid.is_open(position):
            raise SpecError(f"routes[{index}] uses blocked tile {position}")
        if step_index and not grid.step_ok(result[step_index - 1], position):
            raise SpecError(
                f"routes[{index}] has illegal step {result[step_index - 1]} -> {position}"
            )
    if not bool(raw.get("allowRepeats", False)) and len(set(result)) != len(result):
        raise SpecError(f"routes[{index}] repeats a tile; use an authored simple route")
    return result


def analyze_routes(
    spec: dict[str, Any], grid: Grid, landmarks: dict[str, Point]
) -> tuple[list[dict[str, Any]], list[str]]:
    raw_routes = spec.get("routes", [])
    if not isinstance(raw_routes, list):
        raise SpecError("routes must be an array")
    routes: list[dict[str, Any]] = []
    errors: list[str] = []
    ids: set[str] = set()
    for index, raw in enumerate(raw_routes):
        if not isinstance(raw, dict):
            raise SpecError(f"routes[{index}] must be an object")
        route_id = str(raw.get("id", "")).strip()
        if not route_id or route_id in ids:
            raise SpecError(f"routes[{index}].id must be non-empty and unique")
        ids.add(route_id)
        try:
            measured_path = route_path(raw, index, grid, landmarks)
            steps = len(measured_path) - 1
            expected = raw.get("expectedSteps")
            if expected is not None:
                if isinstance(expected, bool) or not isinstance(expected, int) or expected < 0:
                    raise SpecError(f"routes[{index}].expectedSteps must be non-negative")
                if expected != steps:
                    errors.append(f"route {route_id} expected {expected} steps, measured {steps}")
            label_at = raw.get("labelAt")
            if label_at is not None:
                label_position = point(label_at, f"routes[{index}].labelAt")
                if not grid.in_bounds(label_position):
                    raise SpecError(f"routes[{index}].labelAt is outside the map")
                label_at = list(label_position)
            shortest_path = grid.shortest_path(measured_path[0], measured_path[-1])
            if shortest_path is None:
                raise SpecError(f"routes[{index}] endpoints are disconnected")
            shortest_steps = len(shortest_path) - 1
            routes.append(
                {
                    "id": route_id,
                    "label": str(raw.get("label", route_id)),
                    "origin": str(raw.get("origin", "routes")),
                    "group": str(raw.get("group", raw.get("origin", "Routes"))),
                    "style": str(raw.get("style", "north")),
                    "mode": str(raw.get("mode", "shortest")),
                    "steps": steps,
                    "expectedSteps": expected,
                    "shortestSteps": shortest_steps,
                    "path": [list(position) for position in measured_path],
                    "labelAt": label_at,
                    "ratioToShortest": round(steps / shortest_steps, 4)
                    if shortest_steps
                    else 1.0,
                }
            )
        except SpecError as error:
            errors.append(f"route {route_id}: {error}")
    required_ids = spec.get("requiredRouteIds", [])
    if not isinstance(required_ids, list) or not all(
        isinstance(value, str) and value for value in required_ids
    ):
        raise SpecError("requiredRouteIds must be an array of non-empty strings")
    missing_ids = sorted(set(required_ids) - ids)
    if missing_ids:
        errors.append(f"missing required routes: {', '.join(missing_ids)}")
    return routes, errors


FACING_VECTORS: dict[str, Point] = {
    "north": (0, -1),
    "east": (1, 0),
    "south": (0, 1),
    "west": (-1, 0),
}


def in_facing_quadrant(origin: Point, target: Point, facing: str) -> bool:
    dx, dy = target[0] - origin[0], target[1] - origin[1]
    fx, fy = FACING_VECTORS[facing]
    forward = dx * fx + dy * fy
    lateral = abs(dx * fy) + abs(dy * fx)
    return forward >= 0 and lateral <= forward


def parse_combat_reference(
    spec: dict[str, Any],
    grid: Grid,
    landmarks: dict[str, Point],
) -> tuple[dict[str, Any] | None, list[str]]:
    raw = spec.get("combatReference")
    if raw is None:
        return None, []
    if not isinstance(raw, dict):
        raise SpecError("combatReference must be an object")
    origin = resolve_endpoint(raw.get("origin"), "combatReference.origin", landmarks)
    if not grid.is_open(origin):
        raise SpecError("combatReference.origin must be an open map tile")
    facing = str(raw.get("facing", "east")).lower()
    if facing not in FACING_VECTORS:
        raise SpecError("combatReference.facing must be north, east, south, or west")

    raw_vision = raw.get("vision")
    if not isinstance(raw_vision, dict):
        raise SpecError("combatReference.vision must be an object")
    vision_range = raw_vision.get("range")
    if (
        isinstance(vision_range, bool)
        or not isinstance(vision_range, int)
        or vision_range < 1
    ):
        raise SpecError("combatReference.vision.range must be a positive integer")
    shape = str(raw_vision.get("shape", "facing-quadrant"))
    if shape != "facing-quadrant":
        raise SpecError("combatReference.vision.shape must be facing-quadrant")
    line_of_sight = str(
        raw_vision.get("lineOfSight", "corner-strict-supercover")
    )
    if line_of_sight != "corner-strict-supercover":
        raise SpecError(
            "combatReference.vision.lineOfSight must be corner-strict-supercover"
        )
    point_blank_range = raw_vision.get("pointBlankRange", 1)
    if (
        isinstance(point_blank_range, bool)
        or not isinstance(point_blank_range, int)
        or point_blank_range < 0
        or point_blank_range > vision_range
    ):
        raise SpecError(
            "combatReference.vision.pointBlankRange must be between zero and vision range"
        )

    visible_tiles: list[list[int]] = []
    for y in range(grid.height):
        for x in range(grid.width):
            target = (x, y)
            distance = max(abs(x - origin[0]), abs(y - origin[1]))
            if distance > vision_range:
                continue
            if distance > point_blank_range and not in_facing_quadrant(
                origin, target, facing
            ):
                continue
            if grid.has_line_of_sight(origin, target):
                visible_tiles.append([x, y])

    raw_attacks = raw.get("attacks")
    if not isinstance(raw_attacks, list):
        raise SpecError("combatReference.attacks must be an array")
    attacks: list[dict[str, Any]] = []
    ids: set[str] = set()
    errors: list[str] = []
    for index, attack in enumerate(raw_attacks):
        if not isinstance(attack, dict):
            raise SpecError(f"combatReference.attacks[{index}] must be an object")
        attack_id = str(attack.get("id", "")).strip()
        if not attack_id or attack_id in ids:
            raise SpecError(
                f"combatReference.attacks[{index}].id must be non-empty and unique"
            )
        ids.add(attack_id)
        attack_range = attack.get("range")
        if (
            isinstance(attack_range, bool)
            or not isinstance(attack_range, int)
            or attack_range < 1
        ):
            raise SpecError(
                f"combatReference.attacks[{index}].range must be a positive integer"
            )
        classes = attack.get("classes", [])
        if not isinstance(classes, list) or not classes or not all(
            isinstance(value, str) and value.strip() for value in classes
        ):
            raise SpecError(
                f"combatReference.attacks[{index}].classes must be a non-empty array of names"
            )
        attacks.append(
            {
                "id": attack_id,
                "label": str(attack.get("label", attack_id)),
                "range": attack_range,
                "classes": classes,
                "style": str(attack.get("style", attack_id)),
            }
        )
    if len(attacks) != 3:
        errors.append(
            f"combat range reference must declare all three attack profiles; found {len(attacks)}"
        )
    expected_profiles = {"short-fast", "medium-steady", "long-slow"}
    if ids != expected_profiles:
        errors.append(
            "combat range reference must declare short-fast, medium-steady, and long-slow"
        )
    memberships = [
        class_name
        for attack in attacks
        for class_name in attack["classes"]
    ]
    duplicate_memberships = sorted(
        {name for name in memberships if memberships.count(name) > 1}
    )
    if duplicate_memberships:
        errors.append(
            "classes cannot belong to multiple attack profiles: "
            + ", ".join(duplicate_memberships)
        )

    return (
        {
            "origin": list(origin),
            "facing": facing,
            "source": str(raw.get("source", "")),
            "vision": {
                "label": str(raw_vision.get("label", "Base vision")),
                "range": vision_range,
                "shape": shape,
                "lineOfSight": line_of_sight,
                "pointBlankRange": point_blank_range,
                "visibleTiles": visible_tiles,
            },
            "attacks": sorted(attacks, key=lambda value: value["range"]),
        },
        errors,
    )


def validate_atlas_requirements(
    spec: dict[str, Any],
    routes: list[dict[str, Any]],
    combat_reference: dict[str, Any] | None,
) -> tuple[dict[str, bool], list[str]]:
    raw = spec.get("atlasRequirements", {})
    if not isinstance(raw, dict):
        raise SpecError("atlasRequirements must be an object")
    requirements: dict[str, bool] = {}
    for key in ("distances", "combatRanges"):
        value = raw.get(key, False)
        if not isinstance(value, bool):
            raise SpecError(f"atlasRequirements.{key} must be true or false")
        requirements[key] = value
    errors: list[str] = []
    if requirements["distances"]:
        required_ids = spec.get("requiredRouteIds", [])
        route_ids = {route["id"] for route in routes}
        if not routes:
            errors.append("distance atlas requires at least one measured route")
        if not required_ids:
            errors.append("distance atlas requires a non-empty requiredRouteIds inventory")
        elif set(required_ids) != route_ids:
            unrequired = sorted(route_ids - set(required_ids))
            if unrequired:
                errors.append(
                    "distance atlas has routes outside requiredRouteIds: "
                    + ", ".join(unrequired)
                )
        unpinned = sorted(
            route["id"] for route in routes if route["expectedSteps"] is None
        )
        if unpinned:
            errors.append(
                "distance atlas routes must pin expectedSteps: " + ", ".join(unpinned)
            )
    if requirements["combatRanges"] and combat_reference is None:
        errors.append("combat range atlas requires combatReference")
    return requirements, errors


MARK_KINDS = {"zone", "mouth", "post", "seal", "lane", "route"}


def parse_tactical_sites(
    spec: dict[str, Any], grid: Grid
) -> list[dict[str, Any]]:
    """Validate declared tactical-site overlays.

    Sites are data, not viewer code: mouths, posts, seal tiles, firing
    lanes, and named-play routes all arrive from the specification so the
    atlas cannot drift from the analysis.
    """
    raw_sites = spec.get("tacticalSites", [])
    if not isinstance(raw_sites, list):
        raise SpecError("tacticalSites must be an array")
    sites: list[dict[str, Any]] = []
    ids: set[str] = set()
    for index, raw in enumerate(raw_sites):
        if not isinstance(raw, dict):
            raise SpecError(f"tacticalSites[{index}] must be an object")
        site_id = str(raw.get("id", "")).strip()
        if not site_id or site_id in ids:
            raise SpecError(f"tacticalSites[{index}].id must be non-empty and unique")
        ids.add(site_id)
        raw_marks = raw.get("marks")
        if not isinstance(raw_marks, list) or not raw_marks:
            raise SpecError(f"tacticalSites[{index}].marks must be a non-empty array")
        marks: list[dict[str, Any]] = []
        for mark_index, mark in enumerate(raw_marks):
            field = f"tacticalSites[{index}].marks[{mark_index}]"
            if not isinstance(mark, dict):
                raise SpecError(f"{field} must be an object")
            kind = str(mark.get("kind", "")).strip()
            if kind not in MARK_KINDS:
                raise SpecError(
                    f"{field}.kind must be one of {', '.join(sorted(MARK_KINDS))}"
                )
            resolved: dict[str, Any] = {
                "kind": kind,
                "label": str(mark.get("label", "")),
            }
            if kind in {"zone", "mouth", "post", "seal"}:
                tiles = points(mark.get("tiles"), f"{field}.tiles")
                if not tiles:
                    raise SpecError(f"{field}.tiles must be non-empty")
                for tile in tiles:
                    if not grid.is_open(tile):
                        raise SpecError(f"{field} uses a blocked tile {tile}")
                resolved["tiles"] = [list(tile) for tile in tiles]
                if kind == "post":
                    facing = mark.get("facing")
                    if facing is not None:
                        facing = str(facing).lower()
                        if facing not in FACING_VECTORS:
                            raise SpecError(f"{field}.facing must be cardinal")
                        resolved["facing"] = facing
            elif kind == "lane":
                start = point(mark.get("from"), f"{field}.from")
                end = point(mark.get("to"), f"{field}.to")
                dx, dy = end[0] - start[0], end[1] - start[1]
                if not (dx == 0 or dy == 0 or abs(dx) == abs(dy)):
                    raise SpecError(f"{field} lane must be straight")
                for tile in (start, end):
                    if not grid.is_open(tile):
                        raise SpecError(f"{field} lane endpoint {tile} is blocked")
                resolved["from"] = list(start)
                resolved["to"] = list(end)
            else:  # route
                path = points(mark.get("path"), f"{field}.path")
                if len(path) < 2:
                    raise SpecError(f"{field}.path needs at least two tiles")
                for step_index, tile in enumerate(path):
                    if not grid.is_open(tile):
                        raise SpecError(f"{field}.path uses blocked tile {tile}")
                    if step_index and not grid.step_ok(path[step_index - 1], tile):
                        raise SpecError(
                            f"{field}.path has illegal step "
                            f"{path[step_index - 1]} -> {tile}"
                        )
                resolved["path"] = [list(tile) for tile in path]
            marks.append(resolved)
        sites.append(
            {
                "id": site_id,
                "label": str(raw.get("label", site_id)),
                "note": str(raw.get("note", "")),
                "marks": marks,
            }
        )
    return sites


def parse_comparison(spec: dict[str, Any]) -> dict[str, Any] | None:
    """Validate the optional baseline-versus-candidate table."""
    raw = spec.get("comparison")
    if raw is None:
        return None
    if not isinstance(raw, dict):
        raise SpecError("comparison must be an object")
    baseline_label = str(raw.get("baselineLabel", "")).strip()
    candidate_label = str(raw.get("candidateLabel", "")).strip()
    if not baseline_label or not candidate_label:
        raise SpecError("comparison needs baselineLabel and candidateLabel")
    raw_rows = raw.get("rows")
    if not isinstance(raw_rows, list) or not raw_rows:
        raise SpecError("comparison.rows must be a non-empty array")
    rows: list[dict[str, str]] = []
    for index, row in enumerate(raw_rows):
        if not isinstance(row, dict):
            raise SpecError(f"comparison.rows[{index}] must be an object")
        metric = str(row.get("metric", "")).strip()
        if not metric:
            raise SpecError(f"comparison.rows[{index}].metric must be non-empty")
        for key in ("baseline", "candidate"):
            if key not in row:
                raise SpecError(f"comparison.rows[{index}].{key} is required")
        rows.append(
            {
                "metric": metric,
                "baseline": str(row["baseline"]),
                "candidate": str(row["candidate"]),
                "note": str(row.get("note", "")),
            }
        )
    return {
        "baselineLabel": baseline_label,
        "candidateLabel": candidate_label,
        "rows": rows,
    }


def topology_analysis(
    spec: dict[str, Any],
    grid: Grid,
    landmarks: list[dict[str, Any]],
) -> tuple[dict[str, Any], list[str]]:
    errors: list[str] = []
    components = grid.components()
    if len(components) != 1:
        sizes = sorted((len(component) for component in components), reverse=True)
        errors.append(f"open terrain has {len(components)} components: {sizes}")
    false_corners = grid.false_corner_touches()
    if false_corners:
        errors.append(f"map has {len(false_corners)} false diagonal corner-touch passages")
    stub_allowance = spec.get("stubAllowance", 2)
    if isinstance(stub_allowance, bool) or not isinstance(stub_allowance, int) or stub_allowance < 0:
        raise SpecError("stubAllowance must be a non-negative integer")
    cut_vertices: list[tuple[Point, int]] = []
    if bool(spec.get("validateCutVertices", True)):
        cut_vertices = grid.meaningful_cut_vertices(stub_allowance)
        if cut_vertices:
            errors.append(f"map has {len(cut_vertices)} meaningful cut vertices")
    symmetry_mismatches: list[Point] = []
    landmark_symmetry: list[str] = []
    symmetry = spec.get("symmetry")
    if symmetry is not None and symmetry != "rotate-180":
        raise SpecError("symmetry must be rotate-180 when present")
    if symmetry == "rotate-180":
        for y in range(grid.height):
            for x in range(grid.width):
                rotated = grid.width - 1 - x, grid.height - 1 - y
                if grid.rows[y][x] != grid.rows[rotated[1]][rotated[0]]:
                    symmetry_mismatches.append((x, y))
        if symmetry_mismatches:
            errors.append(f"terrain has {len(symmetry_mismatches)} 180-degree symmetry mismatches")
        positions_by_kind: dict[str, set[Point]] = {}
        for landmark in landmarks:
            if landmark["symmetryExempt"]:
                continue
            positions_by_kind.setdefault(landmark["kind"], set()).add(
                tuple(landmark["position"])
            )
        for kind, kind_positions in positions_by_kind.items():
            for position in kind_positions:
                rotated = grid.width - 1 - position[0], grid.height - 1 - position[1]
                if rotated not in kind_positions:
                    landmark_symmetry.append(f"{kind} at {position} lacks mirror {rotated}")
        if landmark_symmetry:
            errors.append(f"landmarks have {len(landmark_symmetry)} symmetry mismatches")
    return (
        {
            "connectedComponents": [len(component) for component in components],
            "falseCornerTouches": [
                [list(first), list(second)] for first, second in false_corners
            ],
            "cutVertices": [
                {"position": list(position), "severedTiles": severed}
                for position, severed in cut_vertices
            ],
            "stubAllowance": stub_allowance,
            "symmetry": symmetry,
            "symmetryMismatches": [list(position) for position in symmetry_mismatches],
            "landmarkSymmetryMismatches": landmark_symmetry,
        },
        errors,
    )


def analyze_spec(spec: dict[str, Any], spec_path: Path) -> dict[str, Any]:
    grid, added, removed = build_grid(spec, spec_path)
    landmarks, landmark_positions, landmark_errors = parse_landmarks(spec, grid)
    topology, topology_errors = topology_analysis(spec, grid, landmarks)
    routes, route_errors = analyze_routes(spec, grid, landmark_positions)
    combat_reference, combat_errors = parse_combat_reference(
        spec, grid, landmark_positions
    )
    atlas_requirements, requirement_errors = validate_atlas_requirements(
        spec, routes, combat_reference
    )
    tactical_sites = parse_tactical_sites(spec, grid)
    comparison = parse_comparison(spec)
    errors = [
        *landmark_errors,
        *topology_errors,
        *route_errors,
        *combat_errors,
        *requirement_errors,
    ]
    return {
        "title": str(spec.get("title", "Arc Relay map route analysis")),
        "note": str(spec.get("note", "")),
        "valid": not errors,
        "errors": errors,
        "width": grid.width,
        "height": grid.height,
        "wall": grid.wall,
        "rows": ["".join(row) for row in grid.rows],
        "openTiles": len(grid.open_tiles()),
        "addedWalls": [list(position) for position in added],
        "removedWalls": [list(position) for position in removed],
        "landmarks": landmarks,
        "topology": topology,
        "routes": routes,
        "atlasRequirements": atlas_requirements,
        "combatReference": combat_reference,
        "tacticalSites": tactical_sites,
        "comparison": comparison,
    }


HTML_TEMPLATE = r'''<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>__TITLE__</title>
<style>
:root{color-scheme:light dark;--bg:light-dark(#fff,#181818);--fg:light-dark(#1a1c1f,#fff);--panel:color-mix(in srgb,var(--fg) 5%,var(--bg));--wall:color-mix(in srgb,var(--fg) 72%,transparent);--border:color-mix(in srgb,var(--fg) 14%,transparent);--north:light-dark(#339cff,#83c3ff);--south:light-dark(#e2852f,#f59a56);--direct:light-dark(#916fe1,#aa91ef);--alternate:light-dark(#2db9af,#5acbc2);--well:light-dark(#5dc977,#74d58b);--candidate:light-dark(#eb77b1,#f08fc0);--reactor:light-dark(#297fd1,#83c3ff);--heal:light-dark(#35b9b1,#5acbc2);--vision:light-dark(#00a9d6,#52d6f4);--short-fast:light-dark(#ee6b64,#ff8d87);--medium-steady:light-dark(#e4a22f,#f5bc55);--long-slow:light-dark(#9b6ee3,#b795f2);--mouth:light-dark(#2f9e44,#69cf7d);--seal:light-dark(#d6453d,#ff8d87);--post:light-dark(#c78116,#ecab3f);--lane:light-dark(#d6459e,#f08fc0);--zone:light-dark(#4263eb,#8aa2ff)}
*{box-sizing:border-box}body{margin:0;padding:16px;background:var(--bg);color:var(--fg);font:14px/1.45 system-ui,sans-serif}main{max-width:960px;margin:0 auto}h1{font-size:20px;font-weight:600;margin:0 0 8px}h2{font-size:16px;margin:18px 0 4px}.controls{display:flex;flex-wrap:wrap;gap:6px;margin-bottom:6px}button{border:1px solid var(--border);border-radius:8px;padding:5px 9px;background:transparent;color:var(--fg);font:inherit}button[aria-pressed="true"]{background:var(--fg);color:var(--bg)}#summary,.note{margin:4px 0 8px;color:color-mix(in srgb,var(--fg) 62%,transparent)}svg{display:block;width:100%;height:auto;touch-action:none}.legend{display:flex;flex-wrap:wrap;gap:8px 14px;margin:6px 0;font-size:12px}.key{display:inline-flex;align-items:center;gap:5px}.line{width:20px;border-top:3px solid var(--north)}.line.south{border-color:var(--south)}.line.direct{border-color:var(--direct)}.line.vision{border-color:var(--vision);border-top-style:dashed}.line.short-fast{border-color:var(--short-fast)}.line.medium-steady{border-color:var(--medium-steady)}.line.long-slow{border-color:var(--long-slow)}.box{width:12px;height:12px;background:var(--candidate)}.box.heal{background:var(--heal);text-align:center;line-height:12px}.table-wrap{max-width:100%;overflow-x:auto}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:6px 8px;border-bottom:1px solid var(--border);vertical-align:top}th.num,td.num{text-align:right;font-variant-numeric:tabular-nums}.errors{color:light-dark(#b33,#ff8b88)}#combat-reference[hidden],#reference-status[hidden]{display:none}[data-reference-handle]{cursor:grab}[data-reference-handle]:active{cursor:grabbing}@media(max-width:520px){body{padding:12px}th.ratio,td.ratio{display:none}}
</style>
</head>
<body>
<main>
<h1>__TITLE__</h1>
<div id="controls" class="controls" role="group" aria-label="Route group"></div>
<div id="site-controls" class="controls" role="group" aria-label="Tactical sites" hidden></div>
<p id="site-note" class="note" hidden></p>
<p id="summary"></p>
<p id="reference-status" class="note" aria-live="polite" hidden></p>
<svg id="map" role="img"></svg>
<div class="legend"><span class="key"><span class="line"></span>north</span><span class="key"><span class="line south"></span>south</span><span class="key"><span class="line direct"></span>direct</span><span class="key"><span class="box"></span>candidate wall</span><span class="key"><span class="box heal">+</span>healing</span><span>R reactor</span><span>1 / 2 / 4 Well value</span></div>
<p id="landmark-summary" class="note"></p>
<div class="table-wrap"><table><thead><tr><th>Origin</th><th>Route</th><th class="num">Steps</th><th class="num ratio">Versus shortest</th></tr></thead><tbody id="route-rows"></tbody></table></div>
<section id="combat-reference" hidden>
<h2>Combat range reference</h2>
<div id="combat-legend" class="legend"></div>
<div class="table-wrap"><table><thead><tr><th>Profile</th><th class="num">Range</th><th>Classes</th></tr></thead><tbody id="combat-rows"></tbody></table></div>
<p id="combat-note" class="note"></p>
</section>
<section id="comparison" hidden>
<h2>Baseline comparison</h2>
<div class="table-wrap"><table><thead><tr id="comparison-head"></tr></thead><tbody id="comparison-rows"></tbody></table></div>
</section>
<p id="errors" class="errors"></p>
<p class="note">Routes report terrain-only corner-strict distance in steps, not elapsed match time. Carrier throttling, bodies, combat, signatures, and hesitation can all add ticks. The cyan outline is the geometric base-vision cone; cyan tile shading is actual wall-occluded vision. Attack contours are distance envelopes, not claims that every enclosed tile is a legal shot.</p>
</main>
<script>
const requestedTheme=new URLSearchParams(location.search).get('theme');
if(requestedTheme==='light'||requestedTheme==='dark')document.documentElement.style.colorScheme=requestedTheme;
const data=__DATA__;
const ns='http://www.w3.org/2000/svg';
const svg=document.getElementById('map');
const groups=[...new Set(data.routes.map(route=>route.group))];
let active=groups[0]||'';
let showCombat=Boolean(data.combatReference);
const defaultReferenceOrigin=data.combatReference?[...data.combatReference.origin]:null;
const defaultReferenceFacing=data.combatReference?data.combatReference.facing:null;
let referenceOrigin=defaultReferenceOrigin?[...defaultReferenceOrigin]:null;
let referenceFacing=defaultReferenceFacing;
let draggingReference=false;
const colour={north:'var(--north)',south:'var(--south)',direct:'var(--direct)',alternate:'var(--alternate)'};
const attackColour={'short-fast':'var(--short-fast)','medium-steady':'var(--medium-steady)','long-slow':'var(--long-slow)'};
const make=(name,attrs={})=>{const node=document.createElementNS(ns,name);for(const [key,value] of Object.entries(attrs))node.setAttribute(key,String(value));return node};
const addText=(scene,x,y,cell,value,fill='var(--fg)',stroke=null)=>{const node=make('text',{x:(x+.5)*cell,y:(y+.5)*cell,'text-anchor':'middle','dominant-baseline':'central',fill,'font-size':Math.max(11,Math.min(13,cell*.7)),'font-weight':600});if(stroke){node.setAttribute('stroke',stroke);node.setAttribute('stroke-width','2.5');node.setAttribute('paint-order','stroke')}node.textContent=value;scene.appendChild(node)};
function drawLandmark(scene,landmark,cell){const [x,y]=landmark.position;const kind=landmark.kind;if(kind==='well'){scene.appendChild(make('circle',{cx:(x+.5)*cell,cy:(y+.5)*cell,r:cell*.38,fill:'var(--well)',stroke:'var(--bg)','stroke-width':Math.max(1.5,cell*.1)}))}else{const fill=kind==='reactor'?'var(--reactor)':kind==='healing'?'var(--heal)':'var(--alternate)';scene.appendChild(make('rect',{x:(x+.1)*cell,y:(y+.1)*cell,width:cell*.8,height:cell*.8,fill,stroke:'var(--bg)','stroke-width':Math.max(1.5,cell*.1)}))}addText(scene,x,y,cell,landmark.label,kind==='reactor'?'var(--bg)':'var(--fg)','var(--bg)')}
function visionConePoints(combat,cell){
  const [ox,oy]=combat.origin;
  const range=combat.vision.range;
  const centre=[(ox+.5)*cell,(oy+.5)*cell];
  const far={
    north:[[(ox-range+.5)*cell,(oy-range+.5)*cell],[(ox+range+.5)*cell,(oy-range+.5)*cell]],
    east:[[(ox+range+.5)*cell,(oy-range+.5)*cell],[(ox+range+.5)*cell,(oy+range+.5)*cell]],
    south:[[(ox-range+.5)*cell,(oy+range+.5)*cell],[(ox+range+.5)*cell,(oy+range+.5)*cell]],
    west:[[(ox-range+.5)*cell,(oy-range+.5)*cell],[(ox-range+.5)*cell,(oy+range+.5)*cell]]
  }[combat.facing];
  return [centre,...far].map(([x,y])=>`${x},${y}`).join(' ');
}
function supercoverLine(start,end){
  const dx=end[0]-start[0],dy=end[1]-start[1],nx=Math.abs(dx),ny=Math.abs(dy),sx=Math.sign(dx),sy=Math.sign(dy);
  let x=start[0],y=start[1],ix=0,iy=0;
  const result=[[x,y]];
  while(ix<nx||iy<ny){
    const crossX=(1+2*ix)*ny,crossY=(1+2*iy)*nx;
    if(crossX===crossY){result.push([x+sx,y],[x,y+sy]);x+=sx;y+=sy;ix++;iy++}
    else if(crossX<crossY){x+=sx;ix++}
    else{y+=sy;iy++}
    result.push([x,y]);
  }
  return result;
}
const isInBounds=([x,y])=>x>=0&&y>=0&&x<data.width&&y<data.height;
const isOpen=([x,y])=>isInBounds([x,y])&&data.rows[y][x]!==data.wall;
function inFacingQuadrant(origin,target,facing){
  const vectors={north:[0,-1],east:[1,0],south:[0,1],west:[-1,0]};
  const [fx,fy]=vectors[facing],dx=target[0]-origin[0],dy=target[1]-origin[1];
  const forward=dx*fx+dy*fy,lateral=Math.abs(dx*fy)+Math.abs(dy*fx);
  return forward>=0&&lateral<=forward;
}
function visibleTilesFor(combat){
  const result=[],origin=combat.origin,range=combat.vision.range,pointBlank=combat.vision.pointBlankRange;
  for(let y=0;y<data.height;y++)for(let x=0;x<data.width;x++){
    const target=[x,y],distance=Math.max(Math.abs(x-origin[0]),Math.abs(y-origin[1]));
    if(distance>range)continue;
    if(distance>pointBlank&&!inFacingQuadrant(origin,target,combat.facing))continue;
    const visible=supercoverLine(origin,target).every(([rx,ry])=>(rx===origin[0]&&ry===origin[1])||(rx===x&&ry===y)||isOpen([rx,ry]));
    if(visible)result.push(target);
  }
  return result;
}
function currentCombat(){
  if(!data.combatReference)return null;
  const combat={...data.combatReference,origin:[...referenceOrigin],facing:referenceFacing};
  combat.vision={...data.combatReference.vision,visibleTiles:[]};
  combat.vision.visibleTiles=visibleTilesFor(combat);
  return combat;
}
function drawCombat(scene,cell,combat){
  if(!combat||!showCombat)return;
  combat.vision.visibleTiles.forEach(([x,y])=>scene.appendChild(make('rect',{x:x*cell,y:y*cell,width:cell,height:cell,fill:'var(--vision)','fill-opacity':.16})));
  const [ox,oy]=combat.origin;
  [...combat.attacks].sort((a,b)=>b.range-a.range).forEach(attack=>{
    const stroke=attackColour[attack.style]||attackColour[attack.id]||'var(--fg)';
    scene.appendChild(make('rect',{x:(ox-attack.range)*cell,y:(oy-attack.range)*cell,width:(attack.range*2+1)*cell,height:(attack.range*2+1)*cell,fill:'none',stroke,'stroke-width':Math.max(1.5,cell*.09),'stroke-dasharray':`${Math.max(3,cell*.25)} ${Math.max(2,cell*.15)}`}));
  });
  scene.appendChild(make('polygon',{'data-vision-cone':'base',points:visionConePoints(combat,cell),fill:'none',stroke:'var(--vision)','stroke-width':Math.max(2,cell*.13),'stroke-linejoin':'round'}));
  scene.appendChild(make('circle',{cx:(ox+.5)*cell,cy:(oy+.5)*cell,r:cell*.3,fill:'var(--fg)',stroke:'var(--bg)','stroke-width':Math.max(1.5,cell*.09)}));
  const vectors={north:[0,-1],east:[1,0],south:[0,1],west:[-1,0]};
  const [dx,dy]=vectors[combat.facing];
  scene.appendChild(make('line',{x1:(ox+.5)*cell,y1:(oy+.5)*cell,x2:(ox+.5+dx*.75)*cell,y2:(oy+.5+dy*.75)*cell,stroke:'var(--vision)','stroke-width':Math.max(2,cell*.16),'stroke-linecap':'round'}));
}
const activeSites=new Set();
function drawSites(scene,cell){
  const markColour={zone:'var(--zone)',mouth:'var(--mouth)',post:'var(--post)',seal:'var(--seal)',lane:'var(--lane)',route:'var(--lane)'};
  for(const site of data.tacticalSites.filter(site=>activeSites.has(site.id))){
    for(const mark of site.marks){
      const stroke=markColour[mark.kind];
      if(mark.kind==='zone'){
        mark.tiles.forEach(([x,y])=>scene.appendChild(make('rect',{x:x*cell,y:y*cell,width:cell,height:cell,fill:stroke,'fill-opacity':.18})));
      }else if(mark.kind==='mouth'){
        mark.tiles.forEach(([x,y])=>scene.appendChild(make('rect',{x:x*cell+cell*.08,y:y*cell+cell*.08,width:cell*.84,height:cell*.84,fill:'none',stroke,'stroke-width':Math.max(2,cell*.14)})));
      }else if(mark.kind==='seal'){
        mark.tiles.forEach(([x,y])=>{
          scene.appendChild(make('line',{x1:x*cell+cell*.2,y1:y*cell+cell*.2,x2:x*cell+cell*.8,y2:y*cell+cell*.8,stroke,'stroke-width':Math.max(2,cell*.16),'stroke-linecap':'round'}));
          scene.appendChild(make('line',{x1:x*cell+cell*.8,y1:y*cell+cell*.2,x2:x*cell+cell*.2,y2:y*cell+cell*.8,stroke,'stroke-width':Math.max(2,cell*.16),'stroke-linecap':'round'}));
        });
      }else if(mark.kind==='post'){
        mark.tiles.forEach(([x,y])=>{
          scene.appendChild(make('circle',{cx:(x+.5)*cell,cy:(y+.5)*cell,r:cell*.3,fill:'none',stroke,'stroke-width':Math.max(2,cell*.14)}));
          if(mark.facing){const [dx,dy]={north:[0,-1],east:[1,0],south:[0,1],west:[-1,0]}[mark.facing];
            scene.appendChild(make('line',{x1:(x+.5)*cell,y1:(y+.5)*cell,x2:(x+.5+dx*.55)*cell,y2:(y+.5+dy*.55)*cell,stroke,'stroke-width':Math.max(2,cell*.14),'stroke-linecap':'round'}));}
        });
      }else if(mark.kind==='lane'){
        scene.appendChild(make('line',{x1:(mark.from[0]+.5)*cell,y1:(mark.from[1]+.5)*cell,x2:(mark.to[0]+.5)*cell,y2:(mark.to[1]+.5)*cell,stroke,'stroke-width':Math.max(2.5,cell*.16),'stroke-dasharray':`${Math.max(4,cell*.4)} ${Math.max(3,cell*.3)}`,'stroke-linecap':'round'}));
      }else{
        scene.appendChild(make('polyline',{points:mark.path.map(([x,y])=>`${(x+.5)*cell},${(y+.5)*cell}`).join(' '),fill:'none',stroke,'stroke-width':Math.max(2,cell*.14),'stroke-dasharray':`${Math.max(2,cell*.2)} ${Math.max(2,cell*.2)}`,'stroke-linejoin':'round','stroke-linecap':'round'}));
      }
    }
  }
}
function updateSiteNote(){
  const note=document.getElementById('site-note');
  const shown=data.tacticalSites.filter(site=>activeSites.has(site.id));
  if(!shown.length){note.hidden=true;return}
  note.hidden=false;
  note.textContent=shown.map(site=>`${site.label}${site.note?` — ${site.note}`:''}`).join(' · ');
}
function drawReferenceHandle(scene,cell,combat){
  if(!combat||!showCombat)return;
  const [ox,oy]=combat.origin;
  scene.appendChild(make('circle',{'data-reference-handle':'true',cx:(ox+.5)*cell,cy:(oy+.5)*cell,r:Math.max(14,cell*.8),fill:'transparent','pointer-events':'all'}));
}
function updateReferenceCopy(combat){
  const status=document.getElementById('reference-status');
  if(!combat){status.hidden=true;return}
  status.hidden=false;
  status.textContent=`Drag the reference marker · tile ${combat.origin.join(', ')} · facing ${combat.facing}`;
  document.getElementById('combat-note').textContent=`Reference origin ${combat.origin.join(', ')}, facing ${combat.facing}. The cyan outline is the full base, unupgraded cone; shaded tiles inside it have corner-strict line of sight after walls. Drag the marker or rotate it above; changes are viewer-only. ${combat.source}`;
}
function draw(){
  const measured=Math.max(300,Math.round(svg.getBoundingClientRect().width||480));
  const cell=measured/data.width;
  const height=cell*data.height;
  svg.setAttribute('viewBox',`0 0 ${measured} ${height}`);
  const visible=data.routes.filter(route=>route.group===active);
  const combat=currentCombat();
  const scene=make('g');
  scene.appendChild(make('rect',{x:0,y:0,width:measured,height,fill:'var(--panel)',stroke:'var(--border)','stroke-width':1}));
  data.rows.forEach((row,y)=>[...row].forEach((tile,x)=>{if(tile===data.wall)scene.appendChild(make('rect',{x:x*cell,y:y*cell,width:cell+.15,height:cell+.15,fill:'var(--wall)'}))}));
  drawCombat(scene,cell,combat);
  data.addedWalls.forEach(([x,y])=>scene.appendChild(make('rect',{x:x*cell,y:y*cell,width:cell+.15,height:cell+.15,fill:'var(--candidate)',stroke:'var(--bg)','stroke-width':Math.max(1,cell*.09)})));
  drawSites(scene,cell);
  visible.forEach(route=>{const stroke=colour[route.style]||colour.north;scene.appendChild(make('polyline',{points:route.path.map(([x,y])=>`${(x+.5)*cell},${(y+.5)*cell}`).join(' '),fill:'none',stroke,'stroke-width':Math.max(3,cell*.22),'stroke-linecap':'round','stroke-linejoin':'round'}));const label=route.labelAt||route.path[Math.floor((route.path.length-1)/2)];const w=Math.max(24,cell*1.55),h=Math.max(16,cell*.9);scene.appendChild(make('rect',{x:(label[0]+.5)*cell-w/2,y:(label[1]+.5)*cell-h/2,width:w,height:h,rx:h*.35,fill:'var(--bg)',stroke,'stroke-width':Math.max(1.5,cell*.08)}));addText(scene,label[0],label[1],cell,String(route.steps))});
  data.landmarks.forEach(item=>drawLandmark(scene,item,cell));
  drawReferenceHandle(scene,cell,combat);
  const title=make('title');title.textContent=`${data.title}: ${visible.map(route=>`${route.label} ${route.steps} steps`).join(', ')}`;
  const desc=make('desc');desc.textContent='Arc Relay terrain, complete declared landmarks, corner-strict route distances, and optional combat range reference.';
  svg.replaceChildren(title,desc,scene);svg.setAttribute('aria-label',title.textContent);
  document.getElementById('summary').textContent=visible.map(route=>`${route.label}: ${route.steps} steps`).join(' · ');
  updateReferenceCopy(combat);
  updateSiteNote();
}
const controls=document.getElementById('controls');groups.forEach((group,index)=>{const button=document.createElement('button');button.type='button';button.dataset.kind='route';button.textContent=group;button.setAttribute('aria-pressed',String(index===0));button.addEventListener('click',()=>{active=group;[...controls.querySelectorAll('[data-kind="route"]')].forEach(peer=>peer.setAttribute('aria-pressed',String(peer===button)));draw()});controls.appendChild(button)});
if(data.combatReference){
  const button=document.createElement('button');button.type='button';button.textContent='Range reference';button.setAttribute('aria-pressed','true');button.addEventListener('click',()=>{showCombat=!showCombat;button.setAttribute('aria-pressed',String(showCombat));draw()});controls.appendChild(button);
  const rotate=document.createElement('button');rotate.type='button';rotate.textContent='Rotate reference ↻';rotate.addEventListener('click',()=>{const facings=['north','east','south','west'];referenceFacing=facings[(facings.indexOf(referenceFacing)+1)%facings.length];draw()});controls.appendChild(rotate);
  const reset=document.createElement('button');reset.type='button';reset.textContent='Reset reference';reset.addEventListener('click',()=>{referenceOrigin=[...defaultReferenceOrigin];referenceFacing=defaultReferenceFacing;draw()});controls.appendChild(reset);
}
function pointerTile(event){
  const bounds=svg.getBoundingClientRect();
  return [Math.floor((event.clientX-bounds.left)*data.width/bounds.width),Math.floor((event.clientY-bounds.top)*data.height/bounds.height)];
}
function moveReference(event){const tile=pointerTile(event);if(isOpen(tile)&&(tile[0]!==referenceOrigin[0]||tile[1]!==referenceOrigin[1])){referenceOrigin=tile;draw()}}
svg.addEventListener('pointerdown',event=>{if(!showCombat||!event.target.closest('[data-reference-handle]'))return;draggingReference=true;svg.setPointerCapture(event.pointerId);event.preventDefault()});
svg.addEventListener('pointermove',event=>{if(draggingReference)moveReference(event)});
svg.addEventListener('pointerup',event=>{if(!draggingReference)return;moveReference(event);draggingReference=false;if(svg.hasPointerCapture(event.pointerId))svg.releasePointerCapture(event.pointerId)});
svg.addEventListener('pointercancel',()=>{draggingReference=false});
const tbody=document.getElementById('route-rows');data.routes.forEach(route=>{const tr=document.createElement('tr');for(const [value,className] of [[route.origin,''],[route.label,''],[route.steps,'num'],[`${route.ratioToShortest.toFixed(2)}×`,'num ratio']]){const td=document.createElement('td');td.textContent=value;td.className=className;tr.appendChild(td)}tbody.appendChild(tr)});
document.getElementById('errors').textContent=data.errors.length?`Validation: ${data.errors.join(' · ')}`:'';
document.getElementById('landmark-summary').textContent=`Landmarks: ${data.landmarks.map(item=>item.name).join(' · ')}`;
if(data.combatReference){
  const section=document.getElementById('combat-reference');section.hidden=false;
  const combat=data.combatReference;
  const legend=document.getElementById('combat-legend');
  const vision=document.createElement('span');vision.className='key';vision.innerHTML=`<span class="line vision"></span>${combat.vision.label} cone ${combat.vision.range}`;legend.appendChild(vision);
  const rows=document.getElementById('combat-rows');
  combat.attacks.forEach(attack=>{const key=document.createElement('span');key.className='key';key.innerHTML=`<span class="line ${attack.style}"></span>${attack.label} ${attack.range}`;legend.appendChild(key);const tr=document.createElement('tr');for(const [value,className] of [[attack.label,''],[attack.range,'num'],[attack.classes.join(', '),'']]){const td=document.createElement('td');td.textContent=value;td.className=className;tr.appendChild(td)}rows.appendChild(tr)});
}
if(data.tacticalSites.length){
  const siteControls=document.getElementById('site-controls');siteControls.hidden=false;
  data.tacticalSites.forEach(site=>{const button=document.createElement('button');button.type='button';button.setAttribute('data-site-toggle',site.id);button.textContent=site.label;button.setAttribute('aria-pressed','false');button.addEventListener('click',()=>{if(activeSites.has(site.id))activeSites.delete(site.id);else activeSites.add(site.id);button.setAttribute('aria-pressed',String(activeSites.has(site.id)));draw()});siteControls.appendChild(button)});
  const legend=document.querySelector('.legend');
  [['mouth','mouth'],['seal','seal tile'],['post','post'],['lane','firing lane'],['zone','zone']].forEach(([kind,label])=>{const key=document.createElement('span');key.className='key';const swatch=document.createElement('span');swatch.className='box';swatch.style.background=`var(--${kind})`;key.appendChild(swatch);key.appendChild(document.createTextNode(label));legend.appendChild(key)});
}
if(data.comparison){
  const section=document.getElementById('comparison');section.hidden=false;
  const head=document.getElementById('comparison-head');
  [['Measure',''],[data.comparison.baselineLabel,'num'],[data.comparison.candidateLabel,'num'],['Reading','']].forEach(([value,className])=>{const th=document.createElement('th');th.textContent=value;th.className=className;head.appendChild(th)});
  const rows=document.getElementById('comparison-rows');
  data.comparison.rows.forEach(row=>{const tr=document.createElement('tr');[[row.metric,''],[row.baseline,'num'],[row.candidate,'num'],[row.note,'']].forEach(([value,className])=>{const td=document.createElement('td');td.textContent=value;td.className=className;tr.appendChild(td)});rows.appendChild(tr)});
}
if(data.note){const note=document.createElement('p');note.className='note';note.textContent=data.note;document.querySelector('h1').after(note)}
draw();new ResizeObserver(draw).observe(svg);
</script>
</body>
</html>
'''


def render_html(result: dict[str, Any]) -> str:
    title = html.escape(result["title"])
    payload = json.dumps(result, ensure_ascii=False, separators=(",", ":")).replace(
        "</", "<\\/"
    )
    return HTML_TEMPLATE.replace("__TITLE__", title).replace("__DATA__", payload)


def write_text(path: str, content: str) -> None:
    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(content, encoding="utf-8")


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Measure and render corner-strict Arc Relay map routes."
    )
    parser.add_argument("spec", help="Analysis specification JSON")
    parser.add_argument("--json-out", help="Write the complete analysis JSON")
    parser.add_argument("--html-out", help="Write a self-contained route atlas")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    spec_path = Path(args.spec).resolve()
    try:
        spec = json.loads(spec_path.read_text(encoding="utf-8"))
        if not isinstance(spec, dict):
            raise SpecError("specification root must be an object")
        result = analyze_spec(spec, spec_path)
    except (OSError, json.JSONDecodeError, SpecError) as error:
        print(f"map analysis failed: {error}", file=sys.stderr)
        return 2
    output = json.dumps(result, indent=2) + "\n"
    if args.json_out:
        write_text(args.json_out, output)
    if args.html_out:
        write_text(args.html_out, render_html(result))
    if not args.json_out:
        print(output, end="")
    if result["errors"]:
        for error in result["errors"]:
            print(f"validation error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
