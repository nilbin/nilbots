#!/usr/bin/env python3
"""Build and validate a non-destructive Aegis Tortoise UV-authority candidate.

This script opens the frozen GA-002 Blender scene, changes only the UV
coordinates of texture-eligible body meshes, and writes a candidate under
``sandbox/``.  It never overwrites the tracked authority.  The existing UV
islands are repacked jointly, so seams and GLTF vertex splitting stay stable,
while object allocation follows physical surface area plus an explicit
gameplay-visibility weight.

The static provider upload uses the composed Shell + Tower endpoint as a
coverage pose and omits the eight renderer-owned ``TeamAccent.*`` meshes.  The
animated candidate retains those exact meshes, their texture-free semantic
material, every hierarchy/pivot transform, and all fourteen clips.

Example:

    blender --background --factory-startup \
      --python scripts/class-models/build_aegis_uv_authority_v2_blender.py -- \
      --repo-root "$PWD" \
      --source-blend art/class-models/masters/aegis-tortoise-ga-002/aegis-tortoise-kinematic-master.blend \
      --source-animated-glb art/class-models/masters/aegis-tortoise-ga-002/aegis-tortoise-neutral-animated.glb \
      --output-dir sandbox/aegis-tortoise-uv-authority-v2
"""

from __future__ import annotations

import argparse
import hashlib
import html
import json
import math
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any, Iterable

import bpy


EXPECTED_BLENDER = (5, 2)
MODEL_COLLECTION = "NB_Bulwark_Kinematic_Master"
TEAM_PREFIX = "TeamAccent."
TEAM_MATERIAL = "NB_TeamAccent_Semantic"
BODY_MATERIAL = "Aegis_Neutral_OriginalUV"
UV_LAYER = "UVMap"
SEMANTIC_CLIPS = (
    "Mobile_Recoil",
    "PodPlant_HoverSettle",
    "Shell_Deploy",
    "Shell_Recoil",
    "Tower_Deploy",
    "Tower_Recoil",
    "Tower_Recoil_01",
    "Tower_Recoil_02",
    "Tower_Recoil_03",
    "Tower_Recoil_04",
    "Tower_Recoil_05",
    "Tower_Recoil_06",
    "Tower_Recoil_07",
    "Tower_Recoil_08",
)
TARGET_BODY_COVERAGE = 0.70
MINIMUM_PADDED_BODY_COVERAGE = 0.64
TARGET_HULL_COVERAGE = 0.40
TARGET_HULL_AREA_GAIN = 12.0
WORLD_BOUNDS_TOLERANCE = 2.0e-5
DISTINCT_CENTER_TOLERANCE = 1.0e-4
EPSILON = 1.0e-9


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--source-blend", required=True, type=Path)
    parser.add_argument("--source-animated-glb", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument(
        "--margin",
        type=float,
        default=0.0005,
        help="Blender FRACTION island margin in normalized atlas units",
    )
    parser.add_argument(
        "--minimum-body-coverage",
        type=float,
        default=MINIMUM_PADDED_BODY_COVERAGE,
        help=(
            "Minimum accepted body UV coverage after padding. Lower this only "
            "for a recorded padding/bleed tradeoff."
        ),
    )
    parser.add_argument(
        "--unwrap-angle",
        type=float,
        default=0.0,
        help=(
            "Optional Smart-project seam angle in degrees; zero preserves the "
            "approved GA-002 seam partition"
        ),
    )
    return parser.parse_args(argv)


def resolved(repo_root: Path, path: Path) -> Path:
    return path.resolve() if path.is_absolute() else (repo_root / path).resolve()


def relative(repo_root: Path, path: Path) -> str:
    return path.resolve().relative_to(repo_root.resolve()).as_posix()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def artifact(path: Path, repo_root: Path) -> dict[str, Any]:
    return {
        "path": relative(repo_root, path),
        "bytes": path.stat().st_size,
        "sha256": sha256_file(path),
    }


def material_role(material: bpy.types.Material | None) -> str | None:
    if material is None:
        return None
    value = material.get("nilbotsRole")
    return value if isinstance(value, str) else None


def collection_objects() -> tuple[
    bpy.types.Collection,
    list[bpy.types.Object],
    list[bpy.types.Object],
    list[bpy.types.Object],
]:
    collection = bpy.data.collections.get(MODEL_COLLECTION)
    if collection is None:
        raise ValueError(f"Missing authority collection: {MODEL_COLLECTION}")
    objects = sorted(collection.all_objects, key=lambda item: item.name)
    meshes = [obj for obj in objects if obj.type == "MESH"]
    accent = [obj for obj in meshes if obj.name.startswith(TEAM_PREFIX)]
    body = [obj for obj in meshes if not obj.name.startswith(TEAM_PREFIX)]
    if len(meshes) != 38 or len(body) != 30 or len(accent) != 8:
        raise ValueError(
            "Expected GA-002's 38 meshes (30 body + 8 semantic accents), got "
            f"{len(meshes)} ({len(body)} + {len(accent)})"
        )
    if any(
        len(obj.data.materials) == 0
        or obj.data.materials[0] is None
        or obj.data.materials[0].name != TEAM_MATERIAL
        or material_role(obj.data.materials[0]) != "team-accent"
        for obj in accent
    ):
        raise ValueError("A renderer-owned team-accent mesh/material drifted")
    return collection, objects, body, accent


def surface_area(obj: bpy.types.Object) -> float:
    # GA-002 uses unit object scale.  Keeping this local-space measure also
    # makes allocation independent of an animated endpoint transform.
    return sum(float(polygon.area) for polygon in obj.data.polygons)


def gameplay_weight(name: str) -> float:
    """Explicit, broad-form texel priorities for the pinned gameplay camera."""
    if name == "Mobile.Hull.Static":
        return 1.65
    if name == "Mobile.GunBarrel":
        return 1.40
    if name.startswith("Pod."):
        return 1.25
    if name.startswith("Shell."):
        return 1.55
    if name == "Tower.Crown":
        return 1.50
    if name == "Tower.UpperKeep":
        return 1.50
    if name.startswith("Tower.Hatch."):
        return 1.45
    if name.endswith(".Armor"):
        return 1.40
    if name.endswith(".Barrel"):
        return 1.15
    raise ValueError(f"No gameplay UV weight is defined for {name}")


def uv_triangle_area(
    first: Any,
    second: Any,
    third: Any,
) -> float:
    return abs(
        (float(second.x) - float(first.x))
        * (float(third.y) - float(first.y))
        - (float(second.y) - float(first.y))
        * (float(third.x) - float(first.x))
    ) * 0.5


def object_uv_area(obj: bpy.types.Object) -> float:
    mesh = obj.data
    layer = mesh.uv_layers.active
    if layer is None:
        raise ValueError(f"Mesh has no UV layer: {obj.name}")
    mesh.calc_loop_triangles()
    result = 0.0
    for triangle in mesh.loop_triangles:
        coordinates = [layer.data[index].uv for index in triangle.loops]
        result += uv_triangle_area(*coordinates)
    return result


def uv_bounds(obj: bpy.types.Object) -> list[float]:
    layer = obj.data.uv_layers.active
    if layer is None or not layer.data:
        raise ValueError(f"Mesh has no UV coordinates: {obj.name}")
    values = [item.uv for item in layer.data]
    return [
        min(float(value.x) for value in values),
        min(float(value.y) for value in values),
        max(float(value.x) for value in values),
        max(float(value.y) for value in values),
    ]


def atlas_metrics(objects: Iterable[bpy.types.Object]) -> dict[str, Any]:
    objects = list(objects)
    records: dict[str, Any] = {}
    total = 0.0
    for obj in objects:
        area = object_uv_area(obj)
        total += area
        records[obj.name] = {
            "uvArea": area,
            "bounds": uv_bounds(obj),
            "surfaceArea": surface_area(obj),
            "gameplayWeight": gameplay_weight(obj.name),
            "equivalentSquareSidePixelsAt2k": math.sqrt(area) * 2048.0,
            "equivalentSquareSidePixelsAt4k": math.sqrt(area) * 4096.0,
        }
    for record in records.values():
        record["shareOfBodyUvArea"] = record["uvArea"] / total
    return {"coverage": total, "objects": records}


def scale_uv_allocation(body: list[bpy.types.Object]) -> dict[str, Any]:
    targets = {
        obj.name: surface_area(obj) * gameplay_weight(obj.name) for obj in body
    }
    target_total = sum(targets.values())
    scale_records: dict[str, Any] = {}
    for obj in body:
        layer = obj.data.uv_layers.active
        if layer is None or layer.name != UV_LAYER:
            raise ValueError(f"Expected one active {UV_LAYER} layer on {obj.name}")
        if len(obj.data.uv_layers) != 1:
            raise ValueError(f"Expected exactly one UV layer on {obj.name}")
        current_area = object_uv_area(obj)
        desired_area = targets[obj.name] / target_total
        if current_area <= EPSILON:
            raise ValueError(f"Cannot scale zero-area UV allocation: {obj.name}")
        factor = math.sqrt(desired_area / current_area)
        for loop in layer.data:
            loop.uv.x *= factor
            loop.uv.y *= factor
        obj.data.update()
        scale_records[obj.name] = {
            "surfaceArea": surface_area(obj),
            "gameplayWeight": gameplay_weight(obj.name),
            "targetShare": desired_area,
            "prePackScale": factor,
        }
    return scale_records


def reunwrap_body(body: list[bpy.types.Object], *, angle_degrees: float) -> None:
    """Refresh only body UV seams; object meshes and every team UV stay exact."""
    if angle_degrees < 45.0 or angle_degrees > 89.0:
        raise ValueError("UV unwrap angle must be between 45 and 89 degrees")
    if bpy.context.object is not None and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for obj in body:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        result = bpy.ops.uv.smart_project(
            angle_limit=math.radians(angle_degrees),
            margin_method="SCALED",
            rotate_method="AXIS_ALIGNED_Y",
            island_margin=0.0,
            area_weight=0.0,
            correct_aspect=True,
            scale_to_bounds=False,
        )
        bpy.ops.object.mode_set(mode="OBJECT")
        if result != {"FINISHED"}:
            raise RuntimeError(f"Smart UV projection failed for {obj.name}")
        layer = obj.data.uv_layers.active
        if layer is None:
            raise ValueError(f"Smart UV projection omitted {obj.name}")
        layer.name = UV_LAYER
        obj.data.update()


def strict_overlap_polygons(
    transplant: Any,
    body: list[bpy.types.Object],
) -> list[tuple[str, int, str, int]]:
    """Return every regular-area overlap using the transplanter's exact SAT."""
    triangles: list[
        tuple[str, int, tuple[tuple[float, float], ...]]
    ] = []
    for obj in body:
        mesh = obj.data
        layer = mesh.uv_layers.active
        if layer is None:
            raise ValueError(f"Missing UV layer on {obj.name}")
        mesh.calc_loop_triangles()
        for triangle in mesh.loop_triangles:
            coordinates = tuple(
                (
                    float(layer.data[index].uv.x),
                    float(layer.data[index].uv.y),
                )
                for index in triangle.loops
            )
            if transplant.triangle_area_2d(coordinates) <= 1.0e-9:
                continue
            triangles.append(
                (obj.name, int(triangle.polygon_index), coordinates)
            )
    grid_size = max(8, min(128, math.ceil(math.sqrt(len(triangles)))))
    buckets: dict[tuple[int, int], list[int]] = defaultdict(list)
    tested: set[tuple[int, int]] = set()
    overlaps: set[tuple[str, int, str, int]] = set()
    for index, (name, polygon, triangle) in enumerate(triangles):
        minimum_u = min(point[0] for point in triangle)
        maximum_u = max(point[0] for point in triangle)
        minimum_v = min(point[1] for point in triangle)
        maximum_v = max(point[1] for point in triangle)
        first_x = max(0, min(grid_size - 1, int(minimum_u * grid_size)))
        last_x = max(
            0,
            min(
                grid_size - 1,
                int(max(0.0, maximum_u - 1.0e-10) * grid_size),
            ),
        )
        first_y = max(0, min(grid_size - 1, int(minimum_v * grid_size)))
        last_y = max(
            0,
            min(
                grid_size - 1,
                int(max(0.0, maximum_v - 1.0e-10) * grid_size),
            ),
        )
        for x in range(first_x, last_x + 1):
            for y in range(first_y, last_y + 1):
                bucket = buckets[(x, y)]
                for other_index in bucket:
                    pair = (other_index, index)
                    if pair in tested:
                        continue
                    tested.add(pair)
                    other_name, other_polygon, other_triangle = triangles[
                        other_index
                    ]
                    if transplant.strict_triangle_overlap(
                        other_triangle, triangle
                    ):
                        overlaps.add(
                            (other_name, other_polygon, name, polygon)
                        )
                bucket.append(index)
    return sorted(overlaps)


def detach_overlapping_polygons(
    body: list[bpy.types.Object],
    overlaps: list[tuple[str, int, str, int]],
) -> int:
    """Make implicated polygons independent islands before the next pack."""
    implicated = sorted(
        {
            (name, polygon)
            for left_name, left_polygon, right_name, right_polygon in overlaps
            for name, polygon in (
                (left_name, left_polygon),
                (right_name, right_polygon),
            )
        }
    )
    self_overlaps = {
        (left_name, left_polygon)
        for left_name, left_polygon, right_name, right_polygon in overlaps
        if left_name == right_name and left_polygon == right_polygon
    }
    by_name = {obj.name: obj for obj in body}
    for index, (name, polygon_index) in enumerate(implicated):
        obj = by_name[name]
        polygon = obj.data.polygons[polygon_index]
        layer = obj.data.uv_layers.active
        mesh = obj.data
        mesh.calc_loop_triangles()
        polygon_triangles = [
            triangle
            for triangle in mesh.loop_triangles
            if triangle.polygon_index == polygon_index
        ]
        old_area = sum(
            uv_triangle_area(
                *[layer.data[loop_index].uv for loop_index in triangle.loops]
            )
            for triangle in polygon_triangles
        )
        vertices = [mesh.vertices[vertex_index].co for vertex_index in polygon.vertices]
        origin = vertices[0]
        tangent = next(
            (
                (point - origin).normalized()
                for point in vertices[1:]
                if (point - origin).length > EPSILON
            ),
            None,
        )
        if tangent is None:
            raise ValueError(f"Cannot flatten zero-span polygon {name}:{polygon_index}")
        normal = polygon.normal.normalized()
        bitangent = normal.cross(tangent)
        if bitangent.length <= EPSILON:
            raise ValueError(f"Cannot flatten polygon {name}:{polygon_index}")
        bitangent.normalize()
        if (name, polygon_index) in self_overlaps:
            # A handful of microscopic bevel quads are non-planar bow-ties in
            # their 3D loop order.  Their GLTF triangle pair therefore cannot
            # share a planar projection without crossing.  Give only those
            # already-subpixel faces a convex loop-order chart; geometry and
            # triangulation remain untouched.
            projected = [
                (
                    math.cos(2.0 * math.pi * item / len(vertices)),
                    math.sin(2.0 * math.pi * item / len(vertices)),
                )
                for item in range(len(vertices))
            ]
        else:
            projected = [
                (
                    (point - origin).dot(tangent),
                    (point - origin).dot(bitangent),
                )
                for point in vertices
            ]
        for loop_index, coordinate in zip(polygon.loop_indices, projected):
            layer.data[loop_index].uv = coordinate
        mesh.update()
        mesh.calc_loop_triangles()
        new_area = sum(
            uv_triangle_area(
                *[layer.data[loop_index].uv for loop_index in triangle.loops]
            )
            for triangle in mesh.loop_triangles
            if triangle.polygon_index == polygon_index
        )
        if old_area > EPSILON and new_area > EPSILON:
            center_u = sum(value[0] for value in projected) / len(projected)
            center_v = sum(value[1] for value in projected) / len(projected)
            scale = math.sqrt(old_area / new_area)
            for loop_index in polygon.loop_indices:
                uv = layer.data[loop_index].uv
                uv.x = center_u + (uv.x - center_u) * scale
                uv.y = center_v + (uv.y - center_v) * scale
        offset = 4.0 + index * 0.01
        for loop_index in polygon.loop_indices:
            layer.data[loop_index].uv.x += offset
            layer.data[loop_index].uv.y += offset
        obj.data.update()
    return len(implicated)


def repair_and_pack_body(
    transplant: Any,
    body: list[bpy.types.Object],
    *,
    margin: float,
) -> dict[str, Any]:
    records: list[dict[str, int]] = []
    for iteration in range(4):
        jointly_pack_body(body, margin=margin)
        overlaps = strict_overlap_polygons(transplant, body)
        records.append(
            {
                "iteration": iteration + 1,
                "strictOverlapPairs": len(overlaps),
                "examples": overlaps[:5],
            }
        )
        if not overlaps:
            return {
                "iterations": records,
                "detachedPolygons": sum(
                    item.get("detachedPolygons", 0) for item in records
                ),
            }
        records[-1]["detachedPolygons"] = detach_overlapping_polygons(
            body, overlaps
        )
    raise ValueError(f"UV overlap repair did not converge: {records}")


def jointly_pack_body(body: list[bpy.types.Object], *, margin: float) -> None:
    if margin <= 0.0 or margin >= 0.02:
        raise ValueError("UV margin must be in the open interval (0, 0.02)")
    if bpy.context.object is not None and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in body:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = body[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    result = bpy.ops.uv.pack_islands(
        rotate=True,
        rotate_method="ANY",
        scale=True,
        merge_overlap=False,
        margin_method="FRACTION",
        margin=margin,
        pin=False,
        shape_method="CONCAVE",
    )
    bpy.ops.object.mode_set(mode="OBJECT")
    if result != {"FINISHED"}:
        raise RuntimeError(f"Blender UV packing failed: {sorted(result)}")
    for obj in body:
        obj.data.update()


def write_uv_svg(
    objects: list[bpy.types.Object],
    *,
    output: Path,
    size: int = 2048,
) -> None:
    palette = (
        "#ec6a3b",
        "#d4a34d",
        "#7c9e75",
        "#5c86a8",
        "#8b75aa",
        "#b7b9bc",
    )
    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        (
            f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" '
            f'height="{size}" viewBox="0 0 {size} {size}">'
        ),
        '<rect width="100%" height="100%" fill="#11151b"/>',
    ]
    for object_index, obj in enumerate(objects):
        layer = obj.data.uv_layers.active
        if layer is None:
            continue
        fill = palette[object_index % len(palette)]
        mesh = obj.data
        mesh.calc_loop_triangles()
        for triangle in mesh.loop_triangles:
            points = " ".join(
                (
                    f"{layer.data[index].uv.x * size:.3f},"
                    f"{(1.0 - layer.data[index].uv.y) * size:.3f}"
                )
                for index in triangle.loops
            )
            lines.append(
                f'<polygon points="{points}" fill="{fill}" fill-opacity="0.24" '
                'stroke="#dce6ef" stroke-opacity="0.30" stroke-width="0.35"/>'
            )
        bounds = uv_bounds(obj)
        label_x = (bounds[0] + bounds[2]) * 0.5 * size
        label_y = (1.0 - (bounds[1] + bounds[3]) * 0.5) * size
        lines.append(
            f'<text x="{label_x:.2f}" y="{label_y:.2f}" fill="#ffffff" '
            'fill-opacity="0.72" font-size="14" text-anchor="middle">'
            f'{html.escape(obj.name)}</text>'
        )
    lines.append("</svg>")
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")


def set_active_tracks(active: set[str]) -> None:
    for obj in bpy.data.objects:
        data = obj.animation_data
        if data is None:
            continue
        data.action = None
        for track in data.nla_tracks:
            track.mute = track.name not in active


def save_track_state(objects: Iterable[bpy.types.Object]) -> dict[str, Any]:
    return {
        obj.name: {
            "action": obj.animation_data.action if obj.animation_data else None,
            "tracks": (
                [(track, bool(track.mute)) for track in obj.animation_data.nla_tracks]
                if obj.animation_data
                else []
            ),
        }
        for obj in objects
    }


def restore_track_state(state: dict[str, Any]) -> None:
    for name, record in state.items():
        obj = bpy.data.objects.get(name)
        if obj is None or obj.animation_data is None:
            continue
        obj.animation_data.action = record["action"]
        for track, mute in record["tracks"]:
            track.mute = mute


def export_animated_glb(
    *, output: Path, collection: bpy.types.Collection
) -> None:
    # Keep GA-002's stored muted-track state.  Blender still exports the named
    # NLA strips in NLA_TRACKS mode, while its node records retain the authored
    # Mobile bind transforms.  Unmuting overlapping strips before export makes
    # their action-space frame-one zeroes replace those static pivots.
    bpy.ops.export_scene.gltf(
        filepath=str(output),
        export_format="GLB",
        use_selection=False,
        collection=collection.name,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
        export_extras=True,
        export_animations=True,
        export_animation_mode="NLA_TRACKS",
        export_merge_animation="NLA_TRACK",
        export_nla_strips=True,
        export_force_sampling=True,
        export_frame_range=False,
        export_apply=True,
        export_yup=True,
    )


def render_coverage_pose(output: Path, camera_name: str) -> None:
    scene = bpy.context.scene
    camera = bpy.data.objects.get(camera_name)
    if camera is None or camera.type != "CAMERA":
        raise ValueError(f"Missing coverage-proof camera: {camera_name}")
    scene.camera = camera
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


def object_world_metrics(obj: bpy.types.Object) -> dict[str, Any]:
    """Return transform-aware bounds and topology for one mesh object."""
    if obj.type != "MESH":
        raise ValueError(f"World metrics require a mesh object: {obj.name}")
    if not obj.data.vertices:
        raise ValueError(f"Mesh has no vertices: {obj.name}")
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    minimum = [min(float(point[axis]) for point in points) for axis in range(3)]
    maximum = [max(float(point[axis]) for point in points) for axis in range(3)]
    obj.data.calc_loop_triangles()
    return {
        "object": obj.name,
        "mesh": obj.data.name,
        "minimum": minimum,
        "maximum": maximum,
        "center": [
            (minimum[axis] + maximum[axis]) * 0.5 for axis in range(3)
        ],
        "triangles": len(obj.data.loop_triangles),
    }


def coverage_world_expectation(
    body: Iterable[bpy.types.Object],
) -> dict[str, Any]:
    records = {
        obj.name: object_world_metrics(obj)
        for obj in sorted(body, key=lambda item: item.name)
    }
    return {
        "objects": records,
        "objectNames": sorted(records),
        "meshNames": sorted(record["mesh"] for record in records.values()),
        "triangles": sum(record["triangles"] for record in records.values()),
    }


def export_coverage_upload(
    *,
    transplant: Any,
    output: Path,
    collection: bpy.types.Collection,
    objects: list[bpy.types.Object],
    accent: list[bpy.types.Object],
    gameplay_proof: Path,
    top_proof: Path,
) -> dict[str, Any]:
    scene = bpy.context.scene
    frame = scene.frame_current
    track_state = save_track_state(objects)
    render_state = {obj.name: bool(obj.hide_render) for obj in accent}
    original_camera = scene.camera
    try:
        set_active_tracks({"Shell_Deploy", "Tower_Deploy"})
        scene.frame_set(96)
        bpy.context.view_layer.update()
        for obj in accent:
            obj.hide_render = True
        render_coverage_pose(gameplay_proof, "PROOF.Camera.gameplay")
        render_coverage_pose(top_proof, "PROOF.Camera.top")
        body = [
            obj
            for obj in objects
            if obj.type == "MESH" and not obj.name.startswith(TEAM_PREFIX)
        ]
        if len(body) != 30:
            raise ValueError(f"Expected 30 coverage body meshes, found {len(body)}")
        if len({obj.name for obj in body}) != len(body):
            raise ValueError("Coverage body object names are not unique")
        if len({obj.data.name for obj in body}) != len(body):
            raise ValueError("Coverage body mesh names are not unique")
        if len({obj.data.as_pointer() for obj in body}) != len(body):
            raise ValueError("Coverage body objects unexpectedly share mesh data")

        expectation = coverage_world_expectation(body)
        temporary_collection = bpy.data.collections.new(
            "__NB_COVERAGE_UPLOAD_WORLD_BAKED__"
        )
        scene.collection.children.link(temporary_collection)
        source_names = [
            (obj, obj.name, obj.data, obj.data.name)
            for obj in sorted(body, key=lambda item: item.name)
        ]
        baked_objects: list[bpy.types.Object] = []
        baked_meshes: list[bpy.types.Mesh] = []
        try:
            # Free the exact authority names for the temporary export copies.
            # The source objects remain untouched apart from these transient
            # names and the persisted candidate was already saved above.
            for index, (obj, _object_name, mesh, _mesh_name) in enumerate(
                source_names
            ):
                obj.name = f"__NB_SOURCE_OBJECT_{index:02d}__"
                mesh.name = f"__NB_SOURCE_MESH_{index:02d}__"

            # Bake the evaluated coverage-pose world matrix into copied mesh
            # vertices.  Every exported object is therefore an identity root;
            # the provider cannot discard a parent pivot and stack repeated
            # pods, shell plates, or tower vanes at their local coordinates.
            for obj, object_name, mesh, mesh_name in source_names:
                baked_mesh = mesh.copy()
                baked_mesh.name = mesh_name
                baked_mesh.transform(obj.matrix_world)
                baked_mesh.update()
                baked_obj = bpy.data.objects.new(object_name, baked_mesh)
                temporary_collection.objects.link(baked_obj)
                baked_objects.append(baked_obj)
                baked_meshes.append(baked_mesh)

            bpy.ops.object.select_all(action="DESELECT")
            for baked_obj in baked_objects:
                baked_obj.select_set(True)
            bpy.context.view_layer.objects.active = baked_objects[0]
            bpy.ops.export_scene.gltf(
                filepath=str(output),
                export_format="GLB",
                use_selection=True,
                export_materials="EXPORT",
                export_cameras=False,
                export_lights=False,
                export_extras=True,
                export_animations=False,
                export_apply=True,
                export_yup=True,
            )
        finally:
            bpy.ops.object.select_all(action="DESELECT")
            for baked_obj in baked_objects:
                if baked_obj.name in bpy.data.objects:
                    bpy.data.objects.remove(baked_obj, do_unlink=True)
            for baked_mesh in baked_meshes:
                if baked_mesh.name in bpy.data.meshes and baked_mesh.users == 0:
                    bpy.data.meshes.remove(baked_mesh)
            if temporary_collection.name in bpy.data.collections:
                bpy.data.collections.remove(temporary_collection)
            for obj, object_name, mesh, mesh_name in source_names:
                obj.name = object_name
                mesh.name = mesh_name
        return expectation
    finally:
        for obj in accent:
            obj.hide_render = render_state[obj.name]
        restore_track_state(track_state)
        scene.camera = original_camera
        scene.frame_set(frame)
        bpy.context.view_layer.update()


def stripped_geometry_records(
    transplant: Any,
    document: dict[str, Any],
    binary: bytes,
) -> list[dict[str, Any]]:
    records = transplant.glb_mesh_records(document, binary)
    for mesh in records:
        for primitive in mesh["primitives"]:
            primitive["attributes"].pop("TEXCOORD_0", None)
    return records


def pin_animation_accessor_bytes(
    transplant: Any,
    *,
    source_path: Path,
    candidate_path: Path,
) -> dict[str, Any]:
    """Copy the pinned GA-002 sampled clip payload into the UV-only export."""
    source, source_binary = transplant.parse_glb(source_path)
    candidate, candidate_binary = transplant.parse_glb(candidate_path)
    source_nodes = source.get("nodes", [])
    candidate_nodes = candidate.get("nodes", [])

    def animation_map(document: dict[str, Any], nodes: list[Any]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for animation in document.get("animations", []):
            channels: dict[tuple[str, str], tuple[int, int]] = {}
            samplers = animation.get("samplers", [])
            for channel in animation.get("channels", []):
                target = channel["target"]
                node_name = nodes[target["node"]]["name"]
                sampler = samplers[channel["sampler"]]
                channels[(node_name, target["path"])] = (
                    sampler["input"],
                    sampler["output"],
                )
            result[animation["name"]] = channels
        return result

    source_map = animation_map(source, source_nodes)
    candidate_map = animation_map(candidate, candidate_nodes)
    if set(source_map) != set(candidate_map):
        raise ValueError("Candidate clip names differ before animation pinning")
    output = bytearray(candidate_binary)
    copied: set[tuple[int, int]] = set()

    def copy_accessor(source_index: int, candidate_index: int) -> None:
        key = (source_index, candidate_index)
        if key in copied:
            return
        source_layout = transplant.accessor_layout(
            source, source_binary, source_index
        )
        candidate_layout = transplant.accessor_layout(
            candidate, candidate_binary, candidate_index
        )
        source_accessor, _sf, source_components, source_count, source_start, source_stride = source_layout
        candidate_accessor, _cf, candidate_components, candidate_count, candidate_start, candidate_stride = candidate_layout
        structural_keys = ("componentType", "type", "count", "normalized")
        if (
            source_components != candidate_components
            or source_count != candidate_count
            or any(
                source_accessor.get(key_name, False)
                != candidate_accessor.get(key_name, False)
                for key_name in structural_keys
            )
        ):
            raise ValueError("Candidate animation accessor layout drifted")
        component_size = transplant.GLTF_COMPONENTS[
            source_accessor["componentType"]
        ][1]
        item_size = source_components * component_size
        for item in range(source_count):
            source_offset = source_start + item * source_stride
            candidate_offset = candidate_start + item * candidate_stride
            output[candidate_offset : candidate_offset + item_size] = (
                source_binary[source_offset : source_offset + item_size]
            )
        copied.add(key)

    for animation_name in sorted(source_map):
        if set(source_map[animation_name]) != set(candidate_map[animation_name]):
            raise ValueError(
                f"Candidate animation targets drifted: {animation_name}"
            )
        for target in sorted(source_map[animation_name]):
            source_input, source_output = source_map[animation_name][target]
            candidate_input, candidate_output = candidate_map[animation_name][target]
            copy_accessor(source_input, candidate_input)
            copy_accessor(source_output, candidate_output)
    transplant.write_glb(
        candidate_path,
        document=candidate,
        binary_chunk=bytes(output),
    )
    return {
        "sourceSha256": sha256_file(source_path),
        "accessorPairsCopied": len(copied),
        "reason": (
            "UV-only export resampling differs by sub-1e-7 float noise; copy "
            "the already-approved GA-002 clip accessor bytes verbatim"
        ),
    }


def glb_validation(
    transplant: Any,
    *,
    source_path: Path,
    candidate_path: Path,
) -> dict[str, Any]:
    source, source_binary = transplant.parse_glb(source_path)
    candidate, candidate_binary = transplant.parse_glb(candidate_path)
    source_nodes = transplant.glb_node_records(source)
    candidate_nodes = transplant.glb_node_records(candidate)
    source_geometry = stripped_geometry_records(transplant, source, source_binary)
    candidate_geometry = stripped_geometry_records(
        transplant, candidate, candidate_binary
    )
    source_animations = transplant.glb_animation_records(source, source_binary)
    candidate_animations = transplant.glb_animation_records(
        candidate, candidate_binary
    )
    checks = {
        "nodeHierarchyPivotsAndTransformsExact": source_nodes == candidate_nodes,
        "geometryTopologyNormalsAndBindingsExact": (
            source_geometry == candidate_geometry
        ),
        "animationSamplesExact": source_animations == candidate_animations,
        "semanticClipSetExact": (
            sorted(item["name"] for item in candidate_animations)
            == sorted(SEMANTIC_CLIPS)
        ),
    }
    if not all(checks.values()):
        raise ValueError(f"Candidate animated GLB drifted: {checks}")
    return {
        "checks": checks,
        "sourceNodeSha256": transplant.stable_hash(source_nodes),
        "candidateNodeSha256": transplant.stable_hash(candidate_nodes),
        "sourceGeometryWithoutUvSha256": transplant.stable_hash(source_geometry),
        "candidateGeometryWithoutUvSha256": transplant.stable_hash(
            candidate_geometry
        ),
        "sourceAnimationSha256": transplant.stable_hash(source_animations),
        "candidateAnimationSha256": transplant.stable_hash(candidate_animations),
        "clips": [
            {
                "name": animation["name"],
                "channels": len(animation["channels"]),
                "start": min(
                    min(channel["times"]) for channel in animation["channels"]
                ),
                "end": max(
                    max(channel["times"]) for channel in animation["channels"]
                ),
            }
            for animation in candidate_animations
        ],
    }


def upload_validation(
    transplant: Any,
    *,
    path: Path,
    expected: dict[str, Any],
) -> dict[str, Any]:
    document, binary = transplant.parse_glb(path)
    records = transplant.glb_mesh_records(document, binary)
    mesh_names = sorted(record["name"] for record in records)
    expected_mesh_names = expected["meshNames"]
    expected_object_names = expected["objectNames"]
    material_names = sorted(
        material.get("name") for material in document.get("materials", [])
    )
    nodes = document.get("nodes", [])
    node_names = sorted(
        node.get("name") for node in nodes if isinstance(node, dict)
    )
    flat_named_mesh_roots = (
        len(nodes) == len(expected_object_names)
        and node_names == expected_object_names
        and all(
            isinstance(node, dict)
            and isinstance(node.get("mesh"), int)
            and not node.get("children")
            for node in nodes
        )
    )

    # Import through Blender, then compare transform-aware bounds by preserved
    # object name.  This is deliberately tolerant of float32 GLB noise but not
    # of missing parent transforms, stacked repeated parts, or changed scale.
    imported = transplant.import_glb_meshes(path)
    imported_by_name = {obj.name: obj for obj in imported}
    imported_object_names = sorted(imported_by_name)
    imported_metrics = {
        name: object_world_metrics(obj)
        for name, obj in sorted(imported_by_name.items())
    }
    shared_names = sorted(set(expected_object_names) & set(imported_metrics))
    bounds_records: dict[str, Any] = {}
    maximum_bounds_delta = 0.0
    bounds_within_tolerance = (
        imported_object_names == expected_object_names
    )
    for name in shared_names:
        expected_record = expected["objects"][name]
        imported_record = imported_metrics[name]
        deltas: dict[str, list[float]] = {}
        object_maximum = 0.0
        for field in ("minimum", "maximum", "center"):
            deltas[field] = [
                abs(float(imported_record[field][axis]) - float(expected_record[field][axis]))
                for axis in range(3)
            ]
            object_maximum = max(object_maximum, *deltas[field])
        maximum_bounds_delta = max(maximum_bounds_delta, object_maximum)
        object_within_tolerance = object_maximum <= WORLD_BOUNDS_TOLERANCE
        bounds_within_tolerance = (
            bounds_within_tolerance and object_within_tolerance
        )
        bounds_records[name] = {
            "expected": {
                field: expected_record[field]
                for field in ("minimum", "maximum", "center")
            },
            "imported": {
                field: imported_record[field]
                for field in ("minimum", "maximum", "center")
            },
            "absoluteDelta": deltas,
            "maximumAbsoluteDelta": object_maximum,
            "withinTolerance": object_within_tolerance,
        }

    triangle_records = {
        name: {
            "expected": expected["objects"][name]["triangles"],
            "imported": imported_metrics[name]["triangles"],
            "exact": (
                expected["objects"][name]["triangles"]
                == imported_metrics[name]["triangles"]
            ),
        }
        for name in shared_names
    }
    imported_triangles = sum(
        record["triangles"] for record in imported_metrics.values()
    )
    triangles_exact = (
        imported_object_names == expected_object_names
        and imported_triangles == expected["triangles"]
        and all(record["exact"] for record in triangle_records.values())
    )

    group_members = {
        "pods": [
            name
            for name in expected_object_names
            if name.startswith("Pod.") and name.endswith(".Anchor")
        ],
        "shellPlates": [
            name
            for name in expected_object_names
            if name.startswith("Shell.") and name.endswith(".Plate")
        ],
        "towerVanes": [
            name
            for name in expected_object_names
            if name.startswith("Tower.Vane.") and name.endswith(".Armor")
        ],
    }
    expected_group_counts = {"pods": 4, "shellPlates": 4, "towerVanes": 8}

    def minimum_pair_distance(
        metrics: dict[str, dict[str, Any]], names: list[str]
    ) -> float:
        distances = []
        for index, left_name in enumerate(names):
            for right_name in names[index + 1 :]:
                left = metrics[left_name]["center"]
                right = metrics[right_name]["center"]
                distances.append(
                    math.sqrt(
                        sum(
                            (float(left[axis]) - float(right[axis])) ** 2
                            for axis in range(3)
                        )
                    )
                )
        return min(distances) if distances else 0.0

    distinct_groups: dict[str, Any] = {}
    repeated_centers_distinct = True
    for group_name, names in group_members.items():
        complete = (
            len(names) == expected_group_counts[group_name]
            and all(name in imported_metrics for name in names)
        )
        expected_minimum = (
            minimum_pair_distance(expected["objects"], names)
            if complete
            else 0.0
        )
        imported_minimum = (
            minimum_pair_distance(imported_metrics, names)
            if complete
            else 0.0
        )
        group_distinct = (
            complete
            and expected_minimum > DISTINCT_CENTER_TOLERANCE
            and imported_minimum > DISTINCT_CENTER_TOLERANCE
        )
        repeated_centers_distinct = (
            repeated_centers_distinct and group_distinct
        )
        distinct_groups[group_name] = {
            "members": names,
            "expectedCount": expected_group_counts[group_name],
            "complete": complete,
            "expectedMinimumPairDistance": expected_minimum,
            "importedMinimumPairDistance": imported_minimum,
            "distinct": group_distinct,
            "importedCenters": {
                name: imported_metrics[name]["center"]
                for name in names
                if name in imported_metrics
            },
        }

    checks = {
        "bodyMeshCountExact": len(records) == len(expected_object_names) == 30,
        "teamMeshesExcluded": not any(
            TEAM_PREFIX in name for name in mesh_names + imported_object_names
        ),
        "meshNamesExact": mesh_names == expected_mesh_names,
        "objectNamesExactAfterReimport": (
            imported_object_names == expected_object_names
        ),
        "flatNamedMeshRootsExact": flat_named_mesh_roots,
        "triangleCountsExactAfterReimport": triangles_exact,
        "worldBoundsWithinToleranceAfterReimport": bounds_within_tolerance,
        "repeatedPartCentersDistinctAfterReimport": repeated_centers_distinct,
        "animationOmitted": not document.get("animations"),
        "oneNeutralMaterial": material_names == [BODY_MATERIAL],
        "cameraAndLightsOmitted": (
            not document.get("cameras")
            and not any(
                "camera" in node for node in document.get("nodes", [])
            )
            and "KHR_lights_punctual" not in document.get("extensions", {})
        ),
    }
    if not all(checks.values()):
        raise ValueError(f"Coverage upload failed validation: {checks}")
    return {
        "checks": checks,
        "meshes": len(records),
        "meshNames": mesh_names,
        "objectNames": imported_object_names,
        "materials": material_names,
        "topology": {
            "expectedTriangles": expected["triangles"],
            "importedTriangles": imported_triangles,
            "objects": triangle_records,
        },
        "worldBounds": {
            "tolerance": WORLD_BOUNDS_TOLERANCE,
            "maximumAbsoluteDelta": maximum_bounds_delta,
            "objects": bounds_records,
        },
        "distinctRepeatedPartCenters": {
            "tolerance": DISTINCT_CENTER_TOLERANCE,
            "groups": distinct_groups,
        },
    }


def body_geometry_records(
    transplant: Any, objects: Iterable[bpy.types.Object]
) -> list[dict[str, str]]:
    return [
        {
            "object": obj.name,
            "mesh": obj.data.name,
            "geometrySha256": transplant.mesh_geometry_digest(obj.data),
        }
        for obj in objects
        if obj.type == "MESH"
    ]


def material_bindings(objects: Iterable[bpy.types.Object]) -> list[dict[str, Any]]:
    return [
        {
            "object": obj.name,
            "materials": [
                material.name if material is not None else None
                for material in obj.data.materials
            ],
            "polygonMaterialIndices": [
                int(polygon.material_index) for polygon in obj.data.polygons
            ],
        }
        for obj in objects
        if obj.type == "MESH"
    ]


def main() -> None:
    args = parse_args()
    if not 0.0 < args.minimum_body_coverage < 1.0:
        raise ValueError("Minimum body coverage must be between zero and one")
    repo_root = args.repo_root.resolve()
    source_blend = resolved(repo_root, args.source_blend)
    source_animated_glb = resolved(repo_root, args.source_animated_glb)
    output_dir = resolved(repo_root, args.output_dir)
    try:
        output_dir.relative_to(repo_root / "sandbox")
    except ValueError as error:
        raise ValueError("UV candidates must be written under repo sandbox/") from error
    if not source_blend.is_file() or not source_animated_glb.is_file():
        raise FileNotFoundError("Source GA-002 blend/animated GLB is missing")
    output_dir.mkdir(parents=True, exist_ok=True)

    scripts_dir = repo_root / "scripts" / "class-models"
    sys.path.insert(0, str(scripts_dir))
    import transplant_texture_donor_blender as transplant

    bpy.ops.wm.open_mainfile(filepath=str(source_blend))
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise ValueError(
            f"Expected Blender {EXPECTED_BLENDER}, got {bpy.app.version_string}"
        )
    collection, objects, body, accent = collection_objects()
    body.sort(key=lambda item: item.name)
    accent.sort(key=lambda item: item.name)

    source_snapshot = transplant.authority_snapshot(objects, bind_frame=1)
    source_geometry = body_geometry_records(transplant, objects)
    source_geometry_sha = transplant.stable_hash(source_geometry)
    source_bindings = material_bindings(objects)
    source_bindings_sha = transplant.stable_hash(source_bindings)
    source_team_uv = {
        obj.name: transplant.mesh_uv_digest(obj.data) for obj in accent
    }
    source_uv = atlas_metrics(body)

    if args.unwrap_angle:
        reunwrap_body(body, angle_degrees=args.unwrap_angle)
    allocation = scale_uv_allocation(body)
    repair = repair_and_pack_body(
        transplant, body, margin=args.margin
    )
    candidate_uv = atlas_metrics(body)
    diagnostic_bounds = [
        min(record["bounds"][0] for record in candidate_uv["objects"].values()),
        min(record["bounds"][1] for record in candidate_uv["objects"].values()),
        max(record["bounds"][2] for record in candidate_uv["objects"].values()),
        max(record["bounds"][3] for record in candidate_uv["objects"].values()),
    ]
    print(
        json.dumps(
            {
                "diagnosticBodyCoverage": candidate_uv["coverage"],
                "diagnosticHullUvArea": candidate_uv["objects"][
                    "Mobile.Hull.Static"
                ]["uvArea"],
                "diagnosticBounds": diagnostic_bounds,
            },
            sort_keys=True,
        )
    )
    strict_atlas = transplant.validate_shared_uv_atlas(body)

    hull_source = source_uv["objects"]["Mobile.Hull.Static"]
    hull_candidate = candidate_uv["objects"]["Mobile.Hull.Static"]
    hull_gain = hull_candidate["uvArea"] / hull_source["uvArea"]
    hull_linear_gain = math.sqrt(hull_gain)
    all_in_bounds = all(
        -1.0e-7 <= float(loop.uv.x) <= 1.0 + 1.0e-7
        and -1.0e-7 <= float(loop.uv.y) <= 1.0 + 1.0e-7
        for obj in body
        for loop in obj.data.uv_layers.active.data
    )
    uv_checks = {
        "bodyCoverageMeetsRecordedMinimumWithPadding": (
            candidate_uv["coverage"] >= args.minimum_body_coverage
        ),
        "hullCoverageAtLeast40Percent": (
            hull_candidate["uvArea"] >= TARGET_HULL_COVERAGE
        ),
        "hullAreaGainAtLeast12x": hull_gain >= TARGET_HULL_AREA_GAIN,
        "allBodyUvInBounds": all_in_bounds,
        "strictNonOverlappingAtlas": strict_atlas["overlapPairs"] == 0,
        "teamUvUntouched": all(
            transplant.mesh_uv_digest(obj.data) == source_team_uv[obj.name]
            for obj in accent
        ),
    }
    if not all(uv_checks.values()):
        raise ValueError(f"UV-authority targets failed: {uv_checks}")

    candidate_snapshot = transplant.authority_snapshot(objects, bind_frame=1)
    candidate_geometry = body_geometry_records(transplant, objects)
    candidate_bindings = material_bindings(objects)
    candidate_body_uv_sha = transplant.stable_hash(
        {
            obj.name: transplant.mesh_uv_digest(obj.data)
            for obj in body
        }
    )
    in_memory_checks = {
        "objectNamesExact": (
            candidate_snapshot["objectNames"] == source_snapshot["objectNames"]
        ),
        "hierarchyPivotsAndTransformsExact": (
            candidate_snapshot["objectTransformsSha256"]
            == source_snapshot["objectTransformsSha256"]
        ),
        "geometryTopologyExact": (
            transplant.stable_hash(candidate_geometry) == source_geometry_sha
        ),
        "materialBindingsExact": (
            transplant.stable_hash(candidate_bindings) == source_bindings_sha
        ),
        "actionNamesExact": (
            candidate_snapshot["actionNames"] == source_snapshot["actionNames"]
        ),
        "nlaTrackNamesExact": (
            candidate_snapshot["nlaTrackNames"]
            == source_snapshot["nlaTrackNames"]
            == sorted(SEMANTIC_CLIPS)
        ),
        "actionDataExact": (
            candidate_snapshot["actionsSha256"]
            == source_snapshot["actionsSha256"]
        ),
        "animationRangesAndEndpointsExact": (
            candidate_snapshot["actionEndpointsSha256"]
            == source_snapshot["actionEndpointsSha256"]
        ),
        "animationBindingsExact": (
            candidate_snapshot["animationBindingsSha256"]
            == source_snapshot["animationBindingsSha256"]
        ),
        "teamGeometryAndUvExact": all(
            transplant.mesh_uv_digest(obj.data) == source_team_uv[obj.name]
            for obj in accent
        ),
    }
    if not all(in_memory_checks.values()):
        raise ValueError(f"Authority changed beyond body UVs: {in_memory_checks}")

    blend_path = output_dir / "aegis-tortoise-uv-authority-v2.blend"
    animated_path = output_dir / "aegis-tortoise-neutral-animated-uv-v2.glb"
    upload_path = output_dir / "aegis-tortoise-texture-upload-coverage-uv-v2.glb"
    uv_svg_path = output_dir / "uv-layout-v2.svg"
    coverage_gameplay = output_dir / "coverage-pose-gameplay.png"
    coverage_top = output_dir / "coverage-pose-top.png"

    write_uv_svg(body, output=uv_svg_path)
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    export_animated_glb(output=animated_path, collection=collection)
    animation_pinning = pin_animation_accessor_bytes(
        transplant,
        source_path=source_animated_glb,
        candidate_path=animated_path,
    )
    upload_expectation = export_coverage_upload(
        transplant=transplant,
        output=upload_path,
        collection=collection,
        objects=objects,
        accent=accent,
        gameplay_proof=coverage_gameplay,
        top_proof=coverage_top,
    )

    glb_proof = glb_validation(
        transplant,
        source_path=source_animated_glb,
        candidate_path=animated_path,
    )
    upload_proof = upload_validation(
        transplant, path=upload_path, expected=upload_expectation
    )

    # Reopen the saved candidate to prove the UV edit and every invariant
    # survived serialization rather than only passing in transient memory.
    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    _collection, persisted_objects, persisted_body, persisted_accent = (
        collection_objects()
    )
    persisted_snapshot = transplant.authority_snapshot(
        persisted_objects, bind_frame=1
    )
    persisted_checks = {
        "hierarchyPivotsAndTransformsExact": (
            persisted_snapshot["objectTransformsSha256"]
            == source_snapshot["objectTransformsSha256"]
        ),
        "geometryTopologyExact": (
            transplant.stable_hash(
                body_geometry_records(transplant, persisted_objects)
            )
            == source_geometry_sha
        ),
        "actionDataExact": (
            persisted_snapshot["actionsSha256"]
            == source_snapshot["actionsSha256"]
        ),
        "animationRangesAndEndpointsExact": (
            persisted_snapshot["actionEndpointsSha256"]
            == source_snapshot["actionEndpointsSha256"]
        ),
        "animationBindingsExact": (
            persisted_snapshot["animationBindingsSha256"]
            == source_snapshot["animationBindingsSha256"]
        ),
        "teamGeometryAndUvExact": all(
            transplant.mesh_uv_digest(obj.data) == source_team_uv[obj.name]
            for obj in persisted_accent
        ),
        "candidateBodyUvExact": (
            transplant.stable_hash(
                {
                    obj.name: transplant.mesh_uv_digest(obj.data)
                    for obj in persisted_body
                }
            )
            == candidate_body_uv_sha
        ),
    }
    if not all(persisted_checks.values()):
        raise ValueError(f"Persisted candidate drifted: {persisted_checks}")

    # Convert floating point output to compact, review-friendly values only at
    # the report boundary; all validation above used full precision.
    for metrics in (source_uv, candidate_uv):
        metrics["coverage"] = round(metrics["coverage"], 9)
        for record in metrics["objects"].values():
            for key, value in list(record.items()):
                if isinstance(value, float):
                    record[key] = round(value, 9)
            record["bounds"] = [round(value, 9) for value in record["bounds"]]

    report = {
        "schema": "nilbots-aegis-uv-authority-candidate-2",
        "status": "PASS",
        "candidateOnly": True,
        "geometryAuthoritySource": "aegis-tortoise-ga-002",
        "candidateGeometryAuthorityId": "aegis-tortoise-ga-003-uv-candidate",
        "blender": {
            "version": bpy.app.version_string,
            "expectedMajorMinor": list(EXPECTED_BLENDER),
        },
        "source": {
            "blend": artifact(source_blend, repo_root),
            "animatedGlb": artifact(source_animated_glb, repo_root),
            "bodyUvCoverage": source_uv["coverage"],
            "bodyGeometryWithoutUvSha256": source_geometry_sha,
            "materialBindingsSha256": source_bindings_sha,
        },
        "uv": {
            "method": (
                "preserve existing UV seams; scale each body mesh by square root "
                "of local surface area times explicit gameplay visibility weight; "
                "jointly pack every body island with Blender CONCAVE packing"
            ),
            "packing": {
                "rotate": True,
                "rotateMethod": "ANY",
                "scale": True,
                "mergeOverlap": False,
                "marginMethod": "FRACTION",
                "margin": args.margin,
                "shapeMethod": "CONCAVE",
                "unwrapAngleDegrees": args.unwrap_angle,
            },
            "targets": {
                "desiredBodyCoverage": TARGET_BODY_COVERAGE,
                "minimumPaddedBodyCoverage": args.minimum_body_coverage,
                "minimumHullCoverage": TARGET_HULL_COVERAGE,
                "minimumHullAreaGain": TARGET_HULL_AREA_GAIN,
            },
            "checks": uv_checks,
            "strictAtlas": strict_atlas,
            "overlapRepair": repair,
            "source": source_uv,
            "candidate": candidate_uv,
            "allocation": allocation,
            "hull": {
                "sourceUvArea": hull_source["uvArea"],
                "candidateUvArea": hull_candidate["uvArea"],
                "areaGain": hull_gain,
                "linearTexelGain": hull_linear_gain,
                "sourceEquivalentSquareSideAt2k": (
                    hull_source["equivalentSquareSidePixelsAt2k"]
                ),
                "candidateEquivalentSquareSideAt2k": (
                    hull_candidate["equivalentSquareSidePixelsAt2k"]
                ),
            },
            "teamMeshes": {
                "count": len(persisted_accent),
                "atlasEligible": False,
                "uploadEligible": False,
                "uvAndGeometryPreservedInAnimatedAuthority": True,
                "names": [obj.name for obj in persisted_accent],
            },
        },
        "authorityPreservation": {
            "inMemoryChecks": in_memory_checks,
            "persistedChecks": persisted_checks,
            "animatedGlb": glb_proof,
            "animationAccessorPinning": animation_pinning,
            "lowHoverSemantics": {
                "preserved": True,
                "reason": (
                    "geometry, transforms, clips, and renderer-owned locomotion "
                    "metadata are unchanged; only body UV coordinates differ"
                ),
            },
        },
        "coverageUpload": {
            "pose": {
                "activeClips": ["Shell_Deploy", "Tower_Deploy"],
                "frame": 96,
                "purpose": (
                    "Expose Mobile hull, deployed Shell plates, hatches, keep, "
                    "crown, vanes, and barrels to one texture-only provider pass"
                ),
                "transformPolicy": (
                    "Bake each evaluated coverage-pose world transform into a "
                    "temporary body-only mesh copy; export thirty identity-root "
                    "named meshes with no parent pivots"
                ),
            },
            "validation": upload_proof,
        },
        "outputs": {
            "editableCandidate": artifact(blend_path, repo_root),
            "neutralAnimatedCandidate": artifact(animated_path, repo_root),
            "bodyOnlyCoverageUpload": artifact(upload_path, repo_root),
            "uvLayout": artifact(uv_svg_path, repo_root),
            "coverageGameplayProof": artifact(coverage_gameplay, repo_root),
            "coverageTopProof": artifact(coverage_top, repo_root),
        },
        "providerCalls": 0,
        "providerCredits": 0,
    }
    report_path = output_dir / "validation-report.json"
    report_path.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    print(
        json.dumps(
            {
                "status": "PASS",
                "report": str(report_path),
                "bodyCoverage": candidate_uv["coverage"],
                "hullAreaGain": round(hull_gain, 3),
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
