#!/usr/bin/env python3
"""Author restrained semantic team accents on a grouped Bulwark Mobile rig.

Invoke through ``add_bulwark_team_accents.py``. The script runs entirely in
the pinned Blender 5.2 process, accepts repository-relative paths only, and
performs no provider or network operation.

Four explicit, surface-fitted emitter-bar meshes are created:

* one long panel on each forward shield lobe;
* one compact panel on each rear/outer pod housing.

Each bar uses one center ray to fit a clean tangent-plane solid, is parented
directly to its semantic rigid pivot, and shares one texture-free, neutral
Principled material. The exported GLB receives
``nilbotsRole=team-accent`` material, mesh, and primitive extras. Blender's
exporter currently omits primitive extras, so those are injected into the
GLB JSON chunk without touching its BIN payload and then structurally
validated.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import struct
import sys
import zlib
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

import bpy
from mathutils import Matrix, Vector


SCHEMA = "nilbots-bulwark-team-accent-report-1"
TOOL_VERSION = "1.0.0"
EXPECTED_BLENDER = (5, 2)
ROLE = "team-accent"
ROOT_NAME = "BulwarkMobile_Root"
MATERIAL_NAME = "Nilbots Team Accent"
OUTPUT_BLEND_NAME = "bulwark-mobile-team-accent.blend"
OUTPUT_GLB_NAME = "bulwark-mobile-team-accent.glb"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
RENDER_SIZE = 768
ART_RENDER_SIZE = 512
ALPHA_THRESHOLD = 1.0 / 255.0
NEUTRAL_HEX = "#b8b8b8"
BLUE_HEX = "#38bdf8"
ORANGE_HEX = "#fb923c"
NEUTRAL_EMISSION_STRENGTH = 0.12
TEAM_EMISSION_STRENGTH = 0.72
PANEL_SURFACE_CLEARANCE = 0.0015
PANEL_THICKNESS = 0.006
TOP_CAMERA_DEPTH_TIE_BIAS = (0.0015, -0.0010)


@dataclass(frozen=True)
class PanelSpec:
    name: str
    primary_geometry: str
    parent_pivot: str
    fallback_geometry: tuple[str, ...]
    center_x_fraction: float
    center_y_fraction: float
    size_x_fraction: float
    size_y_fraction: float
    minimum_size_x: float
    minimum_size_y: float
    kind: str


PANEL_SPECS = (
    PanelSpec(
        name="TeamAccent_Shield_North",
        primary_geometry="ShieldTrim_North_Geometry",
        parent_pivot="ShieldTrim_North_Pivot",
        fallback_geometry=("ChassisCarrier_Geometry",),
        center_x_fraction=0.58,
        center_y_fraction=0.50,
        size_x_fraction=0.28,
        size_y_fraction=0.075,
        minimum_size_x=0.17,
        minimum_size_y=0.018,
        kind="forward-shield-long",
    ),
    PanelSpec(
        name="TeamAccent_Shield_South",
        primary_geometry="ShieldLobe_South_Geometry",
        parent_pivot="ShieldLobe_South_Pivot",
        fallback_geometry=("ChassisCarrier_Geometry",),
        center_x_fraction=0.58,
        center_y_fraction=0.50,
        size_x_fraction=0.28,
        size_y_fraction=0.075,
        minimum_size_x=0.17,
        minimum_size_y=0.018,
        kind="forward-shield-long",
    ),
    PanelSpec(
        name="TeamAccent_Pod_NorthOuter",
        primary_geometry="Pod_NorthOuter_Geometry",
        parent_pivot="Pod_NorthOuter_Pivot",
        fallback_geometry=(),
        center_x_fraction=0.50,
        center_y_fraction=0.50,
        size_x_fraction=0.28,
        size_y_fraction=0.075,
        minimum_size_x=0.052,
        minimum_size_y=0.016,
        kind="rear-outer-pod-compact",
    ),
    PanelSpec(
        name="TeamAccent_Pod_SouthOuter",
        primary_geometry="Pod_SouthOuter_Geometry",
        parent_pivot="Pod_SouthOuter_Pivot",
        fallback_geometry=(),
        center_x_fraction=0.50,
        center_y_fraction=0.50,
        size_x_fraction=0.28,
        size_y_fraction=0.075,
        minimum_size_x=0.052,
        minimum_size_y=0.016,
        kind="rear-outer-pod-compact",
    ),
)


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def blender_arguments() -> list[str]:
    return sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []


def sha256_argument(value: str) -> str:
    normalized = value.strip().lower()
    if not SHA256_RE.fullmatch(normalized):
        raise argparse.ArgumentTypeError(
            "expected a lowercase 64-character SHA-256"
        )
    return normalized


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Offline deterministic Blender postprocess for four Bulwark "
            "Mobile semantic team-accent panels."
        )
    )
    parser.add_argument("--input-blend", type=Path, required=True)
    parser.add_argument(
        "--expected-source-sha256",
        type=sha256_argument,
        required=True,
    )
    parser.add_argument("--output-dir", type=Path, required=True)
    return parser.parse_args(argv)


def resolve_repo_relative(
    path: Path,
    *,
    repo: Path,
    option: str,
    strict: bool,
) -> tuple[Path, Path]:
    if path.is_absolute():
        raise ValueError(f"{option} must be repository-relative")
    if not path.parts:
        raise ValueError(f"{option} cannot be empty")
    resolved = (repo / path).resolve(strict=strict)
    try:
        relative = resolved.relative_to(repo)
    except ValueError as error:
        raise ValueError(f"{option} must stay inside the repository") from error
    return resolved, relative


def resolve_input(path: Path, repo: Path) -> tuple[Path, Path]:
    resolved, relative = resolve_repo_relative(
        path,
        repo=repo,
        option="--input-blend",
        strict=True,
    )
    if not resolved.is_file() or resolved.suffix.lower() != ".blend":
        raise ValueError(f"Expected an existing .blend source: {relative}")
    return resolved, relative


def resolve_output(path: Path, repo: Path) -> tuple[Path, Path]:
    resolved, relative = resolve_repo_relative(
        path,
        repo=repo,
        option="--output-dir",
        strict=False,
    )
    if not relative.parts or relative.parts[0] not in {"sandbox", "var"}:
        raise ValueError("--output-dir must be below repository sandbox/ or var/")
    if resolved.exists():
        raise ValueError(f"Output directory already exists: {relative}")
    return resolved, relative


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def rounded(value: float, places: int = 9) -> float:
    if not math.isfinite(value):
        raise ValueError("Encountered a non-finite numeric value")
    result = round(float(value), places)
    return 0.0 if result == -0.0 else result


def vector_json(value: Iterable[float]) -> list[float]:
    return [rounded(component) for component in value]


def matrix_json(value: Matrix) -> list[list[float]]:
    return [vector_json(row) for row in value]


def bounds(points: Iterable[Vector]) -> tuple[Vector, Vector]:
    values = list(points)
    if not values:
        raise ValueError("Cannot measure empty geometry")
    return (
        Vector(tuple(min(point[axis] for point in values) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in values) for axis in range(3))),
    )


def hex_linear_rgba(value: str) -> tuple[float, float, float, float]:
    if not re.fullmatch(r"#[0-9a-fA-F]{6}", value):
        raise ValueError(f"Invalid six-digit hex colour: {value!r}")
    channels = [int(value[index : index + 2], 16) / 255.0 for index in (1, 3, 5)]
    linear = [
        channel / 12.92
        if channel <= 0.04045
        else ((channel + 0.055) / 1.055) ** 2.4
        for channel in channels
    ]
    return (*linear, 1.0)


def role_value(datablock: Any) -> Any:
    return datablock.get("nilbotsRole") if hasattr(datablock, "get") else None


def require_semantic_source() -> tuple[bpy.types.Object, dict[str, bpy.types.Object]]:
    root = bpy.data.objects.get(ROOT_NAME)
    if root is None or root.type != "EMPTY":
        raise ValueError(f"Source has no {ROOT_NAME!r} empty")
    if role_value(root) is not None:
        raise ValueError("Source root already carries a nilbotsRole")

    required: dict[str, bpy.types.Object] = {}
    for spec in PANEL_SPECS:
        geometry = bpy.data.objects.get(spec.primary_geometry)
        pivot = bpy.data.objects.get(spec.parent_pivot)
        if geometry is None or geometry.type != "MESH":
            raise ValueError(f"Missing mesh object {spec.primary_geometry!r}")
        if pivot is None or pivot.type != "EMPTY":
            raise ValueError(f"Missing pivot object {spec.parent_pivot!r}")
        if geometry.parent != pivot:
            raise ValueError(
                f"{geometry.name!r} is not parented to {pivot.name!r}"
            )
        required[geometry.name] = geometry
        required[pivot.name] = pivot
        for fallback_name in spec.fallback_geometry:
            fallback = bpy.data.objects.get(fallback_name)
            if fallback is None or fallback.type != "MESH":
                raise ValueError(f"Missing fallback mesh {fallback_name!r}")
            required[fallback.name] = fallback

    existing_roles = [
        f"object:{obj.name}"
        for obj in bpy.data.objects
        if role_value(obj) == ROLE
    ]
    existing_roles.extend(
        f"mesh:{mesh.name}"
        for mesh in bpy.data.meshes
        if role_value(mesh) == ROLE
    )
    existing_roles.extend(
        f"material:{material.name}"
        for material in bpy.data.materials
        if role_value(material) == ROLE
    )
    if existing_roles:
        raise ValueError(
            "Source already contains team-accent roles: "
            + ", ".join(sorted(existing_roles))
        )

    descendants = set(root.children_recursive)
    if any(obj not in descendants for obj in required.values()):
        raise ValueError("Required semantic objects are not descendants of the root")
    return root, required


def find_nla_tracks() -> dict[str, list[str]]:
    records: dict[str, list[str]] = {}
    for obj in bpy.data.objects:
        if obj.animation_data is None:
            continue
        for track in obj.animation_data.nla_tracks:
            records.setdefault(track.name, []).append(obj.name)
    for names in records.values():
        names.sort()
    required = {"PodPlant_HoverSettle", "GunRecoil"}
    if not required.issubset(records):
        raise ValueError(
            "Source is missing required NLA clips: "
            + repr(sorted(required - set(records)))
        )
    return dict(sorted(records.items()))


def set_nla_clip(active_name: str | None) -> None:
    for obj in bpy.data.objects:
        if obj.animation_data is None:
            continue
        obj.animation_data.action = None
        for track in obj.animation_data.nla_tracks:
            track.mute = active_name is None or track.name != active_name


def descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    values = [root, *root.children_recursive]
    values.sort(key=lambda obj: (obj.name, obj.type))
    return values


def remove_prior_audit_rig() -> None:
    for obj in list(bpy.data.objects):
        if obj.name.startswith("TOPOLOGY_AUDIT."):
            bpy.data.objects.remove(obj, do_unlink=True)
    for collection in (
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.worlds,
    ):
        for datablock in list(collection):
            if datablock.name.startswith("TOPOLOGY_AUDIT.") and datablock.users == 0:
                collection.remove(datablock)


def create_accent_material() -> bpy.types.Material:
    material = bpy.data.materials.new(MATERIAL_NAME)
    material.use_nodes = True
    if material.node_tree is None:
        raise ValueError("Could not create node tree for accent material")
    nodes = material.node_tree.nodes
    principled = next(
        (node for node in nodes if node.type == "BSDF_PRINCIPLED"),
        None,
    )
    if principled is None:
        raise ValueError("Accent material has no Principled BSDF")
    # The default output and one Principled node are retained; no texture node
    # is introduced. This keeps the GLB material texture-free and compatible
    # with the renderer's MeshStandardMaterial recolour path.
    for node in list(nodes):
        if node not in {principled} and node.type != "OUTPUT_MATERIAL":
            nodes.remove(node)
    material["nilbotsRole"] = ROLE
    material["nilbotsNeutral"] = True
    material["nilbotsTeamColourAtRuntime"] = True
    material["nilbotsSourceTextureFree"] = True
    principled.inputs["Metallic"].default_value = 0.22
    principled.inputs["Roughness"].default_value = 0.43
    set_accent_look(
        material,
        NEUTRAL_HEX,
        emission_strength=NEUTRAL_EMISSION_STRENGTH,
    )
    return material


def set_accent_look(
    material: bpy.types.Material,
    color_hex: str,
    *,
    emission_strength: float,
) -> None:
    if not material.use_nodes or material.node_tree is None:
        raise ValueError("Accent material is not node-based")
    principled = next(
        node
        for node in material.node_tree.nodes
        if node.type == "BSDF_PRINCIPLED"
    )
    rgba = hex_linear_rgba(color_hex)
    principled.inputs["Base Color"].default_value = rgba
    emission_input = principled.inputs.get("Emission Color")
    if emission_input is None:
        emission_input = principled.inputs.get("Emission")
    if emission_input is None:
        raise ValueError("Principled BSDF has no emission colour input")
    emission_input.default_value = rgba
    principled.inputs["Emission Strength"].default_value = emission_strength
    material.diffuse_color = rgba


def chamfered_rectangle(
    center_x: float,
    center_y: float,
    size_x: float,
    size_y: float,
) -> list[Vector]:
    half_x = size_x * 0.5
    half_y = size_y * 0.5
    chamfer = min(size_x, size_y) * 0.24
    return [
        Vector((center_x - half_x + chamfer, center_y - half_y, 0.0)),
        Vector((center_x + half_x - chamfer, center_y - half_y, 0.0)),
        Vector((center_x + half_x, center_y - half_y + chamfer, 0.0)),
        Vector((center_x + half_x, center_y + half_y - chamfer, 0.0)),
        Vector((center_x + half_x - chamfer, center_y + half_y, 0.0)),
        Vector((center_x - half_x + chamfer, center_y + half_y, 0.0)),
        Vector((center_x - half_x, center_y + half_y - chamfer, 0.0)),
        Vector((center_x - half_x, center_y - half_y + chamfer, 0.0)),
    ]


def raycast_surface(
    *,
    parent: bpy.types.Object,
    surfaces: list[bpy.types.Object],
    point_parent_xy: Vector,
    ray_start_parent_z: float,
    ray_distance: float,
) -> tuple[Vector, Vector, str, int, float]:
    origin_parent = Vector(
        (point_parent_xy.x, point_parent_xy.y, ray_start_parent_z)
    )
    origin_world = parent.matrix_world @ origin_parent
    direction_world = Vector((0.0, 0.0, -1.0))
    candidates: list[tuple[float, Vector, Vector, str, int]] = []
    for surface in surfaces:
        inverse = surface.matrix_world.inverted()
        origin_local = inverse @ origin_world
        direction_local_raw = inverse.to_3x3() @ direction_world
        local_scale = direction_local_raw.length
        if local_scale <= 0.0:
            continue
        direction_local = direction_local_raw.normalized()
        hit, location_local, normal_local, polygon_index = surface.ray_cast(
            origin_local,
            direction_local,
            distance=ray_distance * local_scale,
        )
        if not hit:
            continue
        location_world = surface.matrix_world @ location_local
        distance_world = (origin_world - location_world).dot(
            -direction_world
        )
        if distance_world < -1e-6 or distance_world > ray_distance + 1e-6:
            continue
        normal_world = (
            surface.matrix_world.inverted().transposed().to_3x3() @ normal_local
        ).normalized()
        if normal_world.z < 0.0:
            normal_world.negate()
        candidates.append(
            (
                distance_world,
                location_world,
                normal_world,
                surface.name,
                int(polygon_index),
            )
        )
    if not candidates:
        raise ValueError(
            "Panel projection ray missed all fitted surfaces at parent-local "
            f"XY {vector_json(point_parent_xy[:2])}"
        )
    candidates.sort(key=lambda item: (item[0], item[3], item[4]))
    distance_world, location_world, normal_world, surface_name, polygon_index = (
        candidates[0]
    )
    parent_inverse = parent.matrix_world.inverted()
    location_parent = parent_inverse @ location_world
    normal_parent = (
        parent.matrix_world.transposed().to_3x3() @ normal_world
    ).normalized()
    if normal_parent.z < 0.0:
        normal_parent.negate()
    return (
        location_parent,
        normal_parent,
        surface_name,
        polygon_index,
        distance_world,
    )


def create_panel(
    spec: PanelSpec,
    *,
    material: bpy.types.Material,
) -> tuple[bpy.types.Object, dict[str, Any]]:
    primary = bpy.data.objects[spec.primary_geometry]
    parent = bpy.data.objects[spec.parent_pivot]
    surfaces = [
        primary,
        *(bpy.data.objects[name] for name in spec.fallback_geometry),
    ]

    primary_points_parent = [
        primary.matrix_local @ vertex.co for vertex in primary.data.vertices
    ]
    minimum, maximum = bounds(primary_points_parent)
    span = maximum - minimum
    center_x = minimum.x + spec.center_x_fraction * span.x
    center_y = minimum.y + spec.center_y_fraction * span.y
    size_x = min(
        span.x * 0.72,
        max(spec.minimum_size_x, span.x * spec.size_x_fraction),
    )
    size_y = min(
        span.y * 0.72,
        max(spec.minimum_size_y, span.y * spec.size_y_fraction),
    )
    if size_x <= 0.0 or size_y <= 0.0:
        raise ValueError(f"Degenerate panel size for {spec.name}")

    ray_start = maximum.z + max(0.18, span.z * 2.0)
    ray_distance = max(0.50, span.z * 5.0)
    fit = raycast_surface(
        parent=parent,
        surfaces=surfaces,
        point_parent_xy=Vector((center_x, center_y, 0.0)),
        ray_start_parent_z=ray_start,
        ray_distance=ray_distance,
    )
    surface_location, surface_normal, hit_surface, hit_polygon, hit_distance = fit

    # One deterministic surface fit establishes a stable tangent plane. The
    # bar is authored explicitly in that plane; its vertices are never
    # independently projected onto disconnected facets. This avoids the
    # jagged floating patches produced by per-vertex ray projection while
    # retaining an objective attachment point and normal.
    desired_axis = Vector((1.0, 0.0, 0.0))
    axis_u = desired_axis - surface_normal * desired_axis.dot(surface_normal)
    if axis_u.length <= 1e-6:
        desired_axis = Vector((0.0, 1.0, 0.0))
        axis_u = desired_axis - surface_normal * desired_axis.dot(surface_normal)
    axis_u.normalize()
    if axis_u.x < 0.0:
        axis_u.negate()
    axis_v = surface_normal.cross(axis_u).normalized()
    outline_uv = chamfered_rectangle(0.0, 0.0, size_x, size_y)
    lower_center = (
        surface_location + surface_normal * PANEL_SURFACE_CLEARANCE
    )
    upper_center = lower_center + surface_normal * PANEL_THICKNESS
    bottom_points = [
        lower_center + axis_u * point.x + axis_v * point.y
        for point in outline_uv
    ]
    top_points = [
        upper_center + axis_u * point.x + axis_v * point.y
        for point in outline_uv
    ]

    outline_count = len(outline_uv)
    vertices = [*bottom_points, *top_points]
    faces: list[tuple[int, ...]] = [
        tuple(reversed(range(outline_count))),
        tuple(range(outline_count, outline_count * 2)),
    ]
    for index in range(outline_count):
        next_index = (index + 1) % outline_count
        faces.append(
            (
                index,
                next_index,
                outline_count + next_index,
                outline_count + index,
            )
        )

    mesh = bpy.data.meshes.new(f"{spec.name}_Mesh")
    mesh.from_pydata([tuple(point) for point in vertices], [], faces)
    mesh.update(calc_edges=True)
    mesh.materials.append(material)
    mesh["nilbotsRole"] = ROLE
    mesh["nilbotsPanelKind"] = spec.kind
    mesh["nilbotsSurfaceFitted"] = True
    mesh["nilbotsTextureFree"] = True

    panel = bpy.data.objects.new(spec.name, mesh)
    parent.users_collection[0].objects.link(panel)
    panel.parent = parent
    panel.location = (0.0, 0.0, 0.0)
    panel.rotation_euler = (0.0, 0.0, 0.0)
    panel.scale = (1.0, 1.0, 1.0)
    panel["nilbotsRole"] = ROLE
    panel["nilbotsPanelKind"] = spec.kind
    panel["nilbotsSurfaceFitTarget"] = hit_surface
    panel["nilbotsRigidParent"] = spec.parent_pivot

    bpy.context.view_layer.update()
    top_minimum, top_maximum = bounds(top_points)
    return panel, {
        "name": spec.name,
        "kind": spec.kind,
        "mesh": mesh.name,
        "parent": parent.name,
        "primaryProjectionGeometry": primary.name,
        "fallbackProjectionGeometry": list(spec.fallback_geometry),
        "dimensionsParentLocalXY": [rounded(size_x), rounded(size_y)],
        "surfaceClearance": PANEL_SURFACE_CLEARANCE,
        "solidThickness": PANEL_THICKNESS,
        "outlineVertices": outline_count,
        "faces": len(mesh.polygons),
        "triangles": sum(
            max(0, len(polygon.vertices) - 2) for polygon in mesh.polygons
        ),
        "projectionSamples": 1,
        "projectionHitCounts": {hit_surface: 1},
        "projectionPolygonIndices": [
            {"surface": hit_surface, "polygon": hit_polygon}
        ],
        "projectionRayDistances": [rounded(hit_distance)],
        "surfaceFitLocationParentLocal": vector_json(surface_location),
        "surfaceFitNormalParentLocal": vector_json(surface_normal),
        "barAxisParentLocal": vector_json(axis_u),
        "barCrossAxisParentLocal": vector_json(axis_v),
        "construction": (
            "one center ray fit plus explicit chamfered solid in the fitted "
            "tangent plane; no independently projected patch vertices"
        ),
        "topBoundsParentLocal": {
            "minimum": vector_json(top_minimum),
            "maximum": vector_json(top_maximum),
        },
        "localMatrix": matrix_json(panel.matrix_local),
        "materialSlots": [slot.name if slot is not None else None for slot in mesh.materials],
    }


def maximum_matrix_delta(left: Matrix, right: Matrix) -> float:
    return max(
        abs(float(left[row][column]) - float(right[row][column]))
        for row in range(4)
        for column in range(4)
    )


def validate_panel_motion(
    panels: list[bpy.types.Object],
) -> dict[str, Any]:
    scene = bpy.context.scene
    frames = (1, 22, 38, 60)
    set_nla_clip("PodPlant_HoverSettle")
    samples: dict[str, list[dict[str, Any]]] = {panel.name: [] for panel in panels}
    try:
        for frame in frames:
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            for panel in panels:
                parent_relative = panel.parent.matrix_world.inverted() @ panel.matrix_world
                samples[panel.name].append(
                    {
                        "frame": frame,
                        "worldTranslation": vector_json(
                            panel.matrix_world.translation
                        ),
                        "parentRelativeMatrix": matrix_json(parent_relative),
                        "_parentRelative": parent_relative.copy(),
                    }
                )
    finally:
        scene.frame_set(60)
        set_nla_clip(None)
        bpy.context.view_layer.update()

    records: dict[str, Any] = {}
    for panel in panels:
        panel_samples = samples[panel.name]
        baseline = panel_samples[0]["_parentRelative"]
        local_delta = max(
            maximum_matrix_delta(baseline, sample["_parentRelative"])
            for sample in panel_samples
        )
        world_positions = [
            Vector(sample["worldTranslation"]) for sample in panel_samples
        ]
        world_motion = max(
            (position - world_positions[0]).length
            for position in world_positions[1:]
        )
        clean_samples = []
        for sample in panel_samples:
            clean = dict(sample)
            clean.pop("_parentRelative")
            clean_samples.append(clean)
        records[panel.name] = {
            "parent": panel.parent.name,
            "frames": clean_samples,
            "maximumParentRelativeMatrixDelta": rounded(local_delta),
            "maximumWorldTranslationDelta": rounded(world_motion),
            "parentRigid": local_delta <= 1e-8,
            "movesWithClip": world_motion >= 0.02,
        }
    return {
        "clip": "PodPlant_HoverSettle",
        "sampleFrames": list(frames),
        "panels": records,
        "allParentRigid": all(
            record["parentRigid"] for record in records.values()
        ),
        "allMoveWithClip": all(
            record["movesWithClip"] for record in records.values()
        ),
    }


def configure_render_rig(
    model_objects: list[bpy.types.Object],
) -> tuple[bpy.types.Object, bpy.types.Object, Any]:
    scripts_dir = repository_root() / "scripts/class-models"
    sys.path.insert(0, str(scripts_dir))
    import audit_smart_geometry_blender as audit

    remove_prior_audit_rig()
    model_set = set(model_objects)
    for obj in bpy.data.objects:
        if obj not in model_set:
            obj.hide_render = True
    floor, camera = audit.configure_neutral_rig(model_objects)
    return floor, camera, audit


def render_variants(
    *,
    renders_dir: Path,
    material: bpy.types.Material,
    floor: bpy.types.Object,
    camera: bpy.types.Object,
    audit: Any,
) -> dict[str, Any]:
    records: dict[str, Any] = {}
    variants = (
        ("neutral", NEUTRAL_HEX, NEUTRAL_EMISSION_STRENGTH),
        ("blue", BLUE_HEX, TEAM_EMISSION_STRENGTH),
        ("orange", ORANGE_HEX, TEAM_EMISSION_STRENGTH),
    )
    try:
        for name, color, emission_strength in variants:
            set_accent_look(
                material,
                color,
                emission_strength=emission_strength,
            )
            for view, audit_view in (("top", "top"), ("gameplay", "gameplay58")):
                path = renders_dir / f"{name}-{view}.png"
                if view == "top":
                    record = render_near_top_proof(
                        path=path,
                        floor=floor,
                        camera=camera,
                        audit=audit,
                    )
                else:
                    record = audit.render_view(path, floor, camera, audit_view)
                record["accentHexSrgb"] = color
                record["accentEmissionStrength"] = emission_strength
                records[f"{name}-{view}"] = record
    finally:
        set_accent_look(
            material,
            NEUTRAL_HEX,
            emission_strength=NEUTRAL_EMISSION_STRENGTH,
        )
    return records


def render_near_top_proof(
    *,
    path: Path,
    floor: bpy.types.Object,
    camera: bpy.types.Object,
    audit: Any,
) -> dict[str, Any]:
    """Render a visually exact top view with a deterministic depth-tie bias."""
    scene = bpy.context.scene
    floor.hide_render = True
    scene.render.film_transparent = False
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.24
    camera.location = (
        TOP_CAMERA_DEPTH_TIE_BIAS[0],
        TOP_CAMERA_DEPTH_TIE_BIAS[1],
        3.0,
    )
    target = Vector((0.0, 0.0, 0.0))
    camera.rotation_euler = (target - camera.location).to_track_quat(
        "-Z",
        "Y",
    ).to_euler()
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    audit.canonicalize_png(path)
    bias_degrees = math.degrees(
        math.atan2(
            math.hypot(*TOP_CAMERA_DEPTH_TIE_BIAS),
            camera.location.z,
        )
    )
    return {
        "file": path.name,
        "sha256": sha256(path),
        "bytes": path.stat().st_size,
        "cameraType": camera.data.type,
        "cameraLocation": vector_json(camera.location),
        "cameraTarget": vector_json(target),
        "orthoScale": rounded(camera.data.ortho_scale),
        "fieldOfViewDegrees": None,
        "transparentBackground": False,
        "depthTieBiasDegreesFromExactTop": rounded(bias_degrees),
        "depthTieBiasPurpose": (
            "resolve source coplanar/collocated raster ties without a "
            "perceptible presentation-angle change"
        ),
    }


def set_top_camera(
    camera: bpy.types.Object,
    *,
    ortho_scale: float,
    depth_tie_bias: bool = False,
) -> None:
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = ortho_scale
    if depth_tie_bias:
        camera.location = (
            TOP_CAMERA_DEPTH_TIE_BIAS[0],
            TOP_CAMERA_DEPTH_TIE_BIAS[1],
            3.0,
        )
        camera.rotation_euler = (
            Vector((0.0, 0.0, 0.0)) - camera.location
        ).to_track_quat("-Z", "Y").to_euler()
    else:
        camera.location = (0.0, 0.0, 3.0)
        camera.rotation_euler = (0.0, 0.0, 0.0)


def render_mask_pixel_count(
    *,
    output_path: Path,
    visible: set[bpy.types.Object],
    all_model_meshes: list[bpy.types.Object],
    floor: bpy.types.Object,
    camera: bpy.types.Object,
    audit: Any,
) -> int:
    scene = bpy.context.scene
    old_resolution = (
        scene.render.resolution_x,
        scene.render.resolution_y,
        scene.render.resolution_percentage,
    )
    old_transparent = scene.render.film_transparent
    old_floor = floor.hide_render
    old_hides = {obj: obj.hide_render for obj in all_model_meshes}
    old_override = bpy.context.view_layer.material_override
    silhouette = audit.simple_material(
        "NILBOTS_ACCENT.CoverageSilhouette",
        "#ffffff",
        metallic=0.0,
        roughness=1.0,
    )
    try:
        scene.render.resolution_x = RENDER_SIZE
        scene.render.resolution_y = RENDER_SIZE
        scene.render.resolution_percentage = 100
        scene.render.film_transparent = True
        floor.hide_render = True
        set_top_camera(camera, ortho_scale=1.24)
        for obj in all_model_meshes:
            obj.hide_render = obj not in visible
        bpy.context.view_layer.material_override = silhouette
        bpy.context.view_layer.update()
        scene.render.filepath = str(output_path)
        bpy.ops.render.render(write_still=True)
        audit.canonicalize_png(output_path)
        width, height, occupied = audit.alpha_mask(output_path)
        if (width, height) != (RENDER_SIZE, RENDER_SIZE):
            raise ValueError("Coverage render has an unexpected resolution")
        return len(occupied)
    finally:
        bpy.context.view_layer.material_override = old_override
        for obj, hidden in old_hides.items():
            obj.hide_render = hidden
        floor.hide_render = old_floor
        scene.render.film_transparent = old_transparent
        (
            scene.render.resolution_x,
            scene.render.resolution_y,
            scene.render.resolution_percentage,
        ) = old_resolution
        if silhouette.users == 0:
            bpy.data.materials.remove(silhouette)


def projected_area_record(
    *,
    renders_dir: Path,
    body_meshes: list[bpy.types.Object],
    panels: list[bpy.types.Object],
    floor: bpy.types.Object,
    camera: bpy.types.Object,
    audit: Any,
) -> dict[str, Any]:
    all_meshes = [*body_meshes, *panels]
    body_mask_path = renders_dir / "coverage-body-mask.png"
    accent_mask_path = renders_dir / "coverage-accent-mask.png"
    body_pixels = render_mask_pixel_count(
        output_path=body_mask_path,
        visible=set(body_meshes),
        all_model_meshes=all_meshes,
        floor=floor,
        camera=camera,
        audit=audit,
    )
    accent_pixels = render_mask_pixel_count(
        output_path=accent_mask_path,
        visible=set(panels),
        all_model_meshes=all_meshes,
        floor=floor,
        camera=camera,
        audit=audit,
    )
    if body_pixels <= 0:
        raise ValueError("Body-only coverage mask is empty")
    role_total = body_pixels + accent_pixels
    body_share = body_pixels / role_total
    accent_to_body = accent_pixels / body_pixels
    return {
        "method": (
            "separate fixed orthographic +Z alpha masks at identical 768x768 "
            "framing; count alpha > 1/255; treat body and accent samples as "
            "separate projected role contributions"
        ),
        "resolution": [RENDER_SIZE, RENDER_SIZE],
        "cameraOrthoScale": 1.24,
        "alphaThreshold": ALPHA_THRESHOLD,
        "bodyPixels": body_pixels,
        "accentPixels": accent_pixels,
        "bodyRoleShare": rounded(body_share),
        "accentToBodyRatio": rounded(accent_to_body),
        "minimumBodyRoleShare": 0.90,
        "passed": body_share >= 0.90,
        "targetAccentToBodyRatioRange": [0.01, 0.03],
        "withinRestrainedVisualTarget": 0.01 <= accent_to_body <= 0.03,
        "bodyMask": {
            "file": body_mask_path.name,
            "bytes": body_mask_path.stat().st_size,
            "sha256": sha256(body_mask_path),
        },
        "accentMask": {
            "file": accent_mask_path.name,
            "bytes": accent_mask_path.stat().st_size,
            "sha256": sha256(accent_mask_path),
        },
    }


def png_chunk(chunk_type: bytes, payload: bytes) -> bytes:
    return (
        struct.pack(">I", len(payload))
        + chunk_type
        + payload
        + struct.pack(">I", zlib.crc32(chunk_type + payload) & 0xFFFFFFFF)
    )


def write_semantic_mask_png(
    path: Path,
    *,
    width: int,
    height: int,
    alpha_values: list[int],
) -> None:
    if len(alpha_values) != width * height:
        raise ValueError("Semantic mask alpha buffer has the wrong size")
    scanlines = bytearray()
    for row in range(height):
        scanlines.append(0)
        for column in range(width):
            alpha = alpha_values[row * width + column]
            if alpha > 0:
                scanlines.extend((255, 255, 255, alpha))
            else:
                scanlines.extend((0, 0, 0, 0))
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    payload = (
        b"\x89PNG\r\n\x1a\n"
        + png_chunk(b"IHDR", ihdr)
        + png_chunk(b"IDAT", zlib.compress(bytes(scanlines), 9))
        + png_chunk(b"IEND", b"")
    )
    path.write_bytes(payload)


def render_art_authority(
    *,
    output_dir: Path,
    material: bpy.types.Material,
    body_meshes: list[bpy.types.Object],
    panels: list[bpy.types.Object],
    floor: bpy.types.Object,
    camera: bpy.types.Object,
    audit: Any,
) -> dict[str, Any]:
    scene = bpy.context.scene
    all_meshes = [*body_meshes, *panels]
    old_resolution = (
        scene.render.resolution_x,
        scene.render.resolution_y,
        scene.render.resolution_percentage,
    )
    old_transparent = scene.render.film_transparent
    old_floor = floor.hide_render
    old_hides = {obj: obj.hide_render for obj in all_meshes}
    old_override = bpy.context.view_layer.material_override
    avatar_path = output_dir / "art-top-avatar.png"
    mask_path = output_dir / "art-top-team-mask.png"
    mask_material = audit.simple_material(
        "NILBOTS_ACCENT.SemanticMask",
        "#ffffff",
        metallic=0.0,
        roughness=1.0,
    )
    try:
        set_accent_look(
            material,
            NEUTRAL_HEX,
            emission_strength=NEUTRAL_EMISSION_STRENGTH,
        )
        scene.render.resolution_x = ART_RENDER_SIZE
        scene.render.resolution_y = ART_RENDER_SIZE
        scene.render.resolution_percentage = 100
        scene.render.film_transparent = True
        floor.hide_render = True
        set_top_camera(camera, ortho_scale=1.18, depth_tie_bias=True)
        for obj in all_meshes:
            obj.hide_render = False
        scene.render.filepath = str(avatar_path)
        bpy.ops.render.render(write_still=True)
        audit.canonicalize_png(avatar_path)

        for obj in all_meshes:
            obj.hide_render = obj not in set(panels)
        bpy.context.view_layer.material_override = mask_material
        bpy.context.view_layer.update()
        scene.render.filepath = str(mask_path)
        bpy.ops.render.render(write_still=True)
        width, height, rows = audit.decode_rgba_png(mask_path)
        alpha_values = [
            row[index]
            for row in rows
            for index in range(3, len(row), 4)
        ]
        write_semantic_mask_png(
            mask_path,
            width=width,
            height=height,
            alpha_values=alpha_values,
        )
    finally:
        bpy.context.view_layer.material_override = old_override
        for obj, hidden in old_hides.items():
            obj.hide_render = hidden
        floor.hide_render = old_floor
        scene.render.film_transparent = old_transparent
        (
            scene.render.resolution_x,
            scene.render.resolution_y,
            scene.render.resolution_percentage,
        ) = old_resolution
        set_accent_look(
            material,
            NEUTRAL_HEX,
            emission_strength=NEUTRAL_EMISSION_STRENGTH,
        )
        if mask_material.users == 0:
            bpy.data.materials.remove(mask_material)

    unique_alpha = sorted(set(alpha_values))
    return {
        "avatar": {
            "file": avatar_path.name,
            "sha256": sha256(avatar_path),
            "bytes": avatar_path.stat().st_size,
            "resolution": [ART_RENDER_SIZE, ART_RENDER_SIZE],
            "transparentBackground": True,
            "accentLook": "neutral",
            "cameraType": "ORTHO",
            "cameraLocation": vector_json(camera.location),
            "cameraOrthoScale": 1.18,
            "screenRightAxis": "+X within 0.04 degrees",
            "screenUpAxis": "+Y within 0.04 degrees",
            "modelFacingAxis": "+X",
            "facingLabel": "East",
            "depthTieBias": {
                "offsetXY": list(TOP_CAMERA_DEPTH_TIE_BIAS),
                "purpose": "deterministic source coplanar depth ordering",
            },
        },
        "teamMask": {
            "file": mask_path.name,
            "sha256": sha256(mask_path),
            "bytes": mask_path.stat().st_size,
            "resolution": [ART_RENDER_SIZE, ART_RENDER_SIZE],
            "transparentBackground": True,
            "encoding": (
                "lossless RGBA PNG; straight 8-bit coverage alpha; white RGB "
                "where alpha > 0 and zero RGB under alpha zero"
            ),
            "role": ROLE,
            "visibleObjects": sorted(panel.name for panel in panels),
            "hiddenBodyObjectCount": len(body_meshes),
            "alphaLevelCount": len(unique_alpha),
            "minimumAlpha": min(unique_alpha),
            "maximumAlpha": max(unique_alpha),
            "cameraType": "ORTHO",
            "cameraLocation": vector_json(camera.location),
            "cameraOrthoScale": 1.18,
            "screenRightAxis": "+X within 0.04 degrees",
            "screenUpAxis": "+Y within 0.04 degrees",
            "modelFacingAxis": "+X",
            "facingLabel": "East",
        },
    }


def parse_glb(path: Path) -> tuple[dict[str, Any], list[tuple[int, bytes]]]:
    data = path.read_bytes()
    if len(data) < 20:
        raise ValueError(f"Truncated GLB: {path.name}")
    magic, version, declared_length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2 or declared_length != len(data):
        raise ValueError(f"Invalid GLB 2.0 header: {path.name}")
    offset = 12
    chunks: list[tuple[int, bytes]] = []
    document: dict[str, Any] | None = None
    while offset < len(data):
        if offset + 8 > len(data):
            raise ValueError("Truncated GLB chunk header")
        chunk_length, chunk_type = struct.unpack_from("<II", data, offset)
        offset += 8
        if chunk_length % 4 != 0 or offset + chunk_length > len(data):
            raise ValueError("Invalid GLB chunk length or alignment")
        payload = data[offset : offset + chunk_length]
        offset += chunk_length
        chunks.append((chunk_type, payload))
        if chunk_type == 0x4E4F534A:
            if document is not None:
                raise ValueError("GLB contains multiple JSON chunks")
            document = json.loads(payload.decode("utf-8").rstrip("\x00 "))
    if offset != len(data) or document is None:
        raise ValueError("GLB chunks do not consume the declared file")
    if not chunks or chunks[0][0] != 0x4E4F534A:
        raise ValueError("GLB JSON chunk is not first")
    return document, chunks


def write_glb_document(
    path: Path,
    document: dict[str, Any],
    chunks: list[tuple[int, bytes]],
) -> None:
    json_payload = json.dumps(
        document,
        ensure_ascii=True,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    json_payload += b" " * ((-len(json_payload)) % 4)
    rewritten = [(0x4E4F534A, json_payload), *chunks[1:]]
    total_length = 12 + sum(8 + len(payload) for _kind, payload in rewritten)
    output = bytearray(struct.pack("<4sII", b"glTF", 2, total_length))
    for chunk_type, payload in rewritten:
        if len(payload) % 4 != 0:
            raise ValueError("Cannot write an unaligned GLB chunk")
        output.extend(struct.pack("<II", len(payload), chunk_type))
        output.extend(payload)
    temporary = path.with_name(path.name + ".role-patch.tmp")
    temporary.write_bytes(bytes(output))
    temporary.replace(path)


def patch_role_extras(
    path: Path,
    *,
    panel_mesh_names: set[str],
) -> dict[str, Any]:
    document, chunks = parse_glb(path)
    materials = document.get("materials", [])
    material_indices = [
        index
        for index, material in enumerate(materials)
        if material.get("name") == MATERIAL_NAME
    ]
    if len(material_indices) != 1:
        raise ValueError(
            f"Expected one {MATERIAL_NAME!r} material, found {material_indices}"
        )
    material_index = material_indices[0]
    materials[material_index].setdefault("extras", {})["nilbotsRole"] = ROLE

    matched_meshes = []
    matched_primitives = 0
    for mesh in document.get("meshes", []):
        if mesh.get("name") not in panel_mesh_names:
            continue
        matched_meshes.append(mesh.get("name"))
        mesh.setdefault("extras", {})["nilbotsRole"] = ROLE
        primitives = mesh.get("primitives", [])
        if len(primitives) != 1:
            raise ValueError(
                f"Accent mesh {mesh.get('name')!r} must have one primitive"
            )
        primitive = primitives[0]
        if primitive.get("material") != material_index:
            raise ValueError(
                f"Accent mesh {mesh.get('name')!r} uses the wrong material"
            )
        primitive.setdefault("extras", {})["nilbotsRole"] = ROLE
        matched_primitives += 1
    if set(matched_meshes) != panel_mesh_names:
        raise ValueError(
            "Exported accent meshes differ from authored meshes: "
            f"expected {sorted(panel_mesh_names)}, found {sorted(matched_meshes)}"
        )
    write_glb_document(path, document, chunks)
    return {
        "method": (
            "deterministic GLB JSON-chunk rewrite; BIN and all non-JSON "
            "chunks preserved byte-for-byte"
        ),
        "material": MATERIAL_NAME,
        "materialIndex": material_index,
        "meshes": sorted(matched_meshes),
        "primitiveCount": matched_primitives,
    }


def glb_binary(document: dict[str, Any], chunks: list[tuple[int, bytes]]) -> bytes:
    binary_chunks = [
        payload for chunk_type, payload in chunks if chunk_type == 0x004E4942
    ]
    if len(binary_chunks) > 1:
        raise ValueError("GLB contains multiple BIN chunks")
    return binary_chunks[0] if binary_chunks else b""


def glb_image_hashes(
    document: dict[str, Any],
    binary: bytes,
) -> list[str]:
    hashes = []
    buffer_views = document.get("bufferViews", [])
    for image in document.get("images", []):
        if "uri" in image:
            raise ValueError("Expected embedded GLB images, found an image URI")
        view_index = image.get("bufferView")
        if view_index is None:
            raise ValueError("GLB image has neither bufferView nor URI")
        view = buffer_views[int(view_index)]
        offset = int(view.get("byteOffset", 0))
        length = int(view["byteLength"])
        payload = binary[offset : offset + length]
        if len(payload) != length:
            raise ValueError("GLB image bufferView exceeds BIN chunk")
        hashes.append(hashlib.sha256(payload).hexdigest())
    return sorted(hashes)


def primitive_triangle_count(
    primitive: dict[str, Any],
    accessors: list[dict[str, Any]],
) -> int:
    accessor_index = primitive.get("indices")
    if accessor_index is None:
        accessor_index = primitive.get("attributes", {}).get("POSITION")
    if accessor_index is None:
        raise ValueError("Triangle primitive has no indices or POSITION")
    count = int(accessors[int(accessor_index)]["count"])
    mode = int(primitive.get("mode", 4))
    if mode == 4:
        if count % 3 != 0:
            raise ValueError("TRIANGLES primitive index count is not divisible by 3")
        return count // 3
    if mode in (5, 6):
        return max(0, count - 2)
    raise ValueError(f"Unsupported non-triangle primitive mode {mode}")


def animation_records(document: dict[str, Any]) -> list[dict[str, Any]]:
    return sorted(
        (
            {
                "name": animation.get("name", ""),
                "channels": len(animation.get("channels", [])),
                "samplers": len(animation.get("samplers", [])),
            }
            for animation in document.get("animations", [])
        ),
        key=lambda record: record["name"],
    )


def source_glb_record(path: Path, repo: Path) -> dict[str, Any]:
    document, chunks = parse_glb(path)
    binary = glb_binary(document, chunks)
    accessors = document.get("accessors", [])
    triangles = sum(
        primitive_triangle_count(primitive, accessors)
        for mesh in document.get("meshes", [])
        for primitive in mesh.get("primitives", [])
    )
    return {
        "path": path.relative_to(repo).as_posix(),
        "sha256": sha256(path),
        "bytes": path.stat().st_size,
        "triangles": triangles,
        "imageSha256": glb_image_hashes(document, binary),
        "animations": animation_records(document),
        "materials": len(document.get("materials", [])),
        "meshes": len(document.get("meshes", [])),
        "primitives": sum(
            len(mesh.get("primitives", []))
            for mesh in document.get("meshes", [])
        ),
    }


def validate_glb(
    path: Path,
    *,
    panel_object_names: set[str],
    panel_mesh_names: set[str],
    parent_by_panel: dict[str, str],
    source_glb: dict[str, Any],
) -> dict[str, Any]:
    document, chunks = parse_glb(path)
    binary = glb_binary(document, chunks)
    accessors = document.get("accessors", [])
    buffer_views = document.get("bufferViews", [])
    buffers = document.get("buffers", [])
    materials = document.get("materials", [])
    meshes = document.get("meshes", [])
    nodes = document.get("nodes", [])

    if len(buffers) > 1:
        raise ValueError("Only one embedded GLB buffer is supported")
    if buffers and int(buffers[0].get("byteLength", 0)) > len(binary):
        raise ValueError("GLB buffer byteLength exceeds BIN chunk")
    for index, view in enumerate(buffer_views):
        if int(view.get("buffer", 0)) != 0:
            raise ValueError(f"bufferView {index} references a nonzero buffer")
        offset = int(view.get("byteOffset", 0))
        length = int(view.get("byteLength", 0))
        if offset < 0 or length < 0 or offset + length > len(binary):
            raise ValueError(f"bufferView {index} exceeds the BIN chunk")
    for index, accessor in enumerate(accessors):
        if "bufferView" in accessor:
            view_index = int(accessor["bufferView"])
            if view_index < 0 or view_index >= len(buffer_views):
                raise ValueError(f"accessor {index} has invalid bufferView")

    role_materials = [
        (index, material)
        for index, material in enumerate(materials)
        if material.get("extras", {}).get("nilbotsRole") == ROLE
    ]
    if len(role_materials) != 1:
        raise ValueError(f"Expected one role material, found {len(role_materials)}")
    material_index, role_material = role_materials[0]
    if role_material.get("name") != MATERIAL_NAME:
        raise ValueError("Role material has an unexpected name")
    pbr = role_material.get("pbrMetallicRoughness", {})
    forbidden_texture_keys = {
        "baseColorTexture": pbr.get("baseColorTexture"),
        "metallicRoughnessTexture": pbr.get("metallicRoughnessTexture"),
        "normalTexture": role_material.get("normalTexture"),
        "occlusionTexture": role_material.get("occlusionTexture"),
        "emissiveTexture": role_material.get("emissiveTexture"),
    }
    if any(value is not None for value in forbidden_texture_keys.values()):
        raise ValueError("Role material unexpectedly references a texture")
    base_factor = pbr.get("baseColorFactor", [1.0, 1.0, 1.0, 1.0])
    emissive_factor = role_material.get("emissiveFactor", [0.0, 0.0, 0.0])
    if max(base_factor[:3]) - min(base_factor[:3]) > 1e-7:
        raise ValueError("Final role base colour is not neutral")
    if max(emissive_factor) - min(emissive_factor) > 1e-7:
        raise ValueError("Final role emissive colour is not neutral")

    role_meshes = [
        (index, mesh)
        for index, mesh in enumerate(meshes)
        if mesh.get("extras", {}).get("nilbotsRole") == ROLE
    ]
    if len(role_meshes) != 4:
        raise ValueError(f"Expected four role meshes, found {len(role_meshes)}")
    if {mesh.get("name") for _index, mesh in role_meshes} != panel_mesh_names:
        raise ValueError("Role mesh names differ from the authored panel meshes")

    role_primitives: list[tuple[int, int, dict[str, Any]]] = []
    all_triangles = 0
    accent_triangles = 0
    for mesh_index, mesh in enumerate(meshes):
        for primitive_index, primitive in enumerate(mesh.get("primitives", [])):
            if "material" in primitive:
                referenced_material = int(primitive["material"])
                if referenced_material < 0 or referenced_material >= len(materials):
                    raise ValueError("Primitive references an invalid material")
            triangle_count = primitive_triangle_count(primitive, accessors)
            all_triangles += triangle_count
            if primitive.get("extras", {}).get("nilbotsRole") == ROLE:
                role_primitives.append((mesh_index, primitive_index, primitive))
                accent_triangles += triangle_count
    if len(role_primitives) != 4:
        raise ValueError(
            f"Expected four role primitives, found {len(role_primitives)}"
        )
    if any(
        primitive.get("material") != material_index
        for _mesh, _primitive_index, primitive in role_primitives
    ):
        raise ValueError("A role primitive does not use the one role material")
    other_role_primitive_count = sum(
        primitive.get("extras", {}).get("nilbotsRole") == ROLE
        for mesh_index, mesh in enumerate(meshes)
        if mesh_index not in {index for index, _mesh in role_meshes}
        for primitive in mesh.get("primitives", [])
    )
    if other_role_primitive_count:
        raise ValueError("A non-role mesh contains a role primitive")

    name_to_node = {
        node.get("name"): index
        for index, node in enumerate(nodes)
        if isinstance(node.get("name"), str)
    }
    parent_index_by_child: dict[int, int] = {}
    for parent_index, node in enumerate(nodes):
        for child in node.get("children", []):
            child_index = int(child)
            if child_index in parent_index_by_child:
                raise ValueError("GLB node has more than one parent")
            parent_index_by_child[child_index] = parent_index
    exported_parent_by_panel = {}
    for panel_name in panel_object_names:
        node_index = name_to_node.get(panel_name)
        if node_index is None:
            raise ValueError(f"GLB has no panel node {panel_name!r}")
        parent_index = parent_index_by_child.get(node_index)
        if parent_index is None:
            raise ValueError(f"Panel node {panel_name!r} has no parent")
        parent_name = nodes[parent_index].get("name")
        exported_parent_by_panel[panel_name] = parent_name
        if parent_name != parent_by_panel[panel_name]:
            raise ValueError(
                f"Panel {panel_name!r} exported under {parent_name!r}, "
                f"expected {parent_by_panel[panel_name]!r}"
            )

    output_animations = animation_records(document)
    if output_animations != source_glb["animations"]:
        raise ValueError(
            "Animation names/channel counts changed: "
            f"source={source_glb['animations']}, output={output_animations}"
        )
    output_image_hashes = glb_image_hashes(document, binary)
    if output_image_hashes != source_glb["imageSha256"]:
        raise ValueError("Provider body texture payload hashes changed")
    body_triangles = all_triangles - accent_triangles
    if body_triangles != source_glb["triangles"]:
        raise ValueError(
            "Body triangle count changed: "
            f"source={source_glb['triangles']}, output body={body_triangles}"
        )

    return {
        "validator": (
            "internal deterministic GLB 2.0 structural/semantic validator; "
            "no Khronos validator binary is pinned in this repository"
        ),
        "header": {
            "version": 2,
            "bytes": path.stat().st_size,
            "chunkCount": len(chunks),
            "chunkAlignmentValid": True,
        },
        "counts": {
            "nodes": len(nodes),
            "meshes": len(meshes),
            "primitives": sum(
                len(mesh.get("primitives", [])) for mesh in meshes
            ),
            "materials": len(materials),
            "images": len(document.get("images", [])),
            "textures": len(document.get("textures", [])),
            "triangles": all_triangles,
            "bodyTriangles": body_triangles,
            "accentTriangles": accent_triangles,
        },
        "roles": {
            "materialCount": len(role_materials),
            "meshCount": len(role_meshes),
            "primitiveCount": len(role_primitives),
            "materialName": role_material.get("name"),
            "materialTextureReferences": forbidden_texture_keys,
            "baseColorFactor": [rounded(value) for value in base_factor],
            "emissiveFactor": [rounded(value) for value in emissive_factor],
            "meshNames": sorted(panel_mesh_names),
            "nodeParents": dict(sorted(exported_parent_by_panel.items())),
        },
        "bodyTextureImageSha256": output_image_hashes,
        "animations": output_animations,
        "passed": True,
    }


def artifact_record(path: Path, repo: Path) -> dict[str, Any]:
    return {
        "path": path.relative_to(repo).as_posix(),
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
    }


def export_model(
    *,
    root: bpy.types.Object,
    model_objects: list[bpy.types.Object],
    glb_path: Path,
) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in model_objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    for obj in bpy.data.objects:
        if obj.animation_data is not None:
            for track in obj.animation_data.nla_tracks:
                track.mute = False
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_jpeg_quality=100,
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


def main() -> int:
    args = parse_args(blender_arguments())
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise ValueError(
            f"Expected Blender {EXPECTED_BLENDER[0]}.{EXPECTED_BLENDER[1]}.x, "
            f"found {bpy.app.version_string}"
        )
    repo = repository_root()
    input_blend, input_relative = resolve_input(args.input_blend, repo)
    output_dir, output_relative = resolve_output(args.output_dir, repo)
    observed_source_hash = sha256(input_blend)
    if observed_source_hash != args.expected_source_sha256:
        raise ValueError(
            "Source hash mismatch before opening Blender file: "
            f"expected {args.expected_source_sha256}, "
            f"observed {observed_source_hash}"
        )
    input_glb = input_blend.with_suffix(".glb")
    if not input_glb.is_file():
        raise ValueError(
            "Grouped source blend must have a byte-matched sibling semantic GLB"
        )
    source_glb = source_glb_record(input_glb, repo)

    output_dir.mkdir(parents=True)
    renders_dir = output_dir / "renders"
    renders_dir.mkdir()

    bpy.ops.wm.open_mainfile(filepath=str(input_blend))
    # Opening the file must not be allowed to swap the source on disk.
    if sha256(input_blend) != args.expected_source_sha256:
        raise ValueError("Source blend hash changed while Blender opened it")
    root, _required = require_semantic_source()
    nla_tracks = find_nla_tracks()
    set_nla_clip(None)
    bpy.context.scene.frame_set(60)
    bpy.context.view_layer.update()

    material = create_accent_material()
    panels: list[bpy.types.Object] = []
    panel_records = []
    for spec in PANEL_SPECS:
        panel, record = create_panel(spec, material=material)
        panels.append(panel)
        panel_records.append(record)
    if len({panel.data for panel in panels}) != 4:
        raise ValueError("Accent panels do not own four distinct mesh datablocks")
    if any(len(panel.data.materials) != 1 for panel in panels):
        raise ValueError("Every accent panel must have exactly one material slot")

    motion = validate_panel_motion(panels)
    # Frame 60 is the authored rest pose in the source clip.
    bpy.context.scene.frame_set(60)
    set_nla_clip(None)
    bpy.context.view_layer.update()

    model_objects = descendants(root)
    body_meshes = [
        obj
        for obj in model_objects
        if obj.type == "MESH" and obj not in panels
    ]
    floor, camera, audit = configure_render_rig(model_objects)
    render_records = render_variants(
        renders_dir=renders_dir,
        material=material,
        floor=floor,
        camera=camera,
        audit=audit,
    )
    projected_area = projected_area_record(
        renders_dir=renders_dir,
        body_meshes=body_meshes,
        panels=panels,
        floor=floor,
        camera=camera,
        audit=audit,
    )
    art_authority = render_art_authority(
        output_dir=output_dir,
        material=material,
        body_meshes=body_meshes,
        panels=panels,
        floor=floor,
        camera=camera,
        audit=audit,
    )
    set_accent_look(
        material,
        NEUTRAL_HEX,
        emission_strength=NEUTRAL_EMISSION_STRENGTH,
    )

    glb_path = output_dir / OUTPUT_GLB_NAME
    blend_path = output_dir / OUTPUT_BLEND_NAME
    export_model(
        root=root,
        model_objects=model_objects,
        glb_path=glb_path,
    )
    set_nla_clip(None)
    bpy.context.scene.frame_set(60)
    bpy.context.view_layer.update()
    patch_record = patch_role_extras(
        glb_path,
        panel_mesh_names={panel.data.name for panel in panels},
    )
    glb_validation = validate_glb(
        glb_path,
        panel_object_names={panel.name for panel in panels},
        panel_mesh_names={panel.data.name for panel in panels},
        parent_by_panel={panel.name: panel.parent.name for panel in panels},
        source_glb=source_glb,
    )

    bpy.ops.file.pack_all()
    readme = bpy.data.texts.get("NILBOTS_TEAM_ACCENT_README")
    if readme is None:
        readme = bpy.data.texts.new("NILBOTS_TEAM_ACCENT_README")
    readme.clear()
    readme.write(
        "Four explicit projected team-accent panels share one neutral, "
        "texture-free Principled material.\\n"
        "Runtime recolours material/mesh/primitive role "
        "nilbotsRole=team-accent; no blue/orange is baked.\\n"
        "Panels are direct children of their shield/pod rigid pivots and "
        "carry no animation keys.\\n"
    )
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    outputs: dict[str, dict[str, Any]] = {
        "blend": artifact_record(blend_path, repo),
        "glb": artifact_record(glb_path, repo),
    }
    for key, record in render_records.items():
        outputs[f"render-{key}"] = artifact_record(
            renders_dir / record["file"],
            repo,
        )
    outputs["coverage-body-mask"] = artifact_record(
        renders_dir / projected_area["bodyMask"]["file"],
        repo,
    )
    outputs["coverage-accent-mask"] = artifact_record(
        renders_dir / projected_area["accentMask"]["file"],
        repo,
    )
    outputs["art-avatar"] = artifact_record(
        output_dir / art_authority["avatar"]["file"],
        repo,
    )
    outputs["art-team-mask"] = artifact_record(
        output_dir / art_authority["teamMask"]["file"],
        repo,
    )

    role_materials_in_blend = [
        candidate
        for candidate in bpy.data.materials
        if role_value(candidate) == ROLE
    ]
    role_meshes_in_blend = [
        mesh for mesh in bpy.data.meshes if role_value(mesh) == ROLE
    ]
    checks = {
        "sourceHashMatchesBeforeAndAfterOpen": sha256(input_blend)
        == args.expected_source_sha256,
        "sourceHasRequiredGroupedRig": root.name == ROOT_NAME,
        "sourceHasBothNamedNlaClips": {
            "PodPlant_HoverSettle",
            "GunRecoil",
        }.issubset(nla_tracks),
        "exactlyFourExplicitPanelObjects": len(panels) == 4,
        "exactlyFourDistinctPanelMeshes": len({panel.data for panel in panels})
        == 4,
        "exactlyOneRoleMaterialInBlend": len(role_materials_in_blend) == 1,
        "exactlyFourRoleMeshesInBlend": len(role_meshes_in_blend) == 4,
        "oneMaterialSlotPerPanel": all(
            len(panel.data.materials) == 1
            and panel.data.materials[0] == material
            for panel in panels
        ),
        "allProjectionSamplesHit": all(
            record["projectionSamples"]
            == sum(record["projectionHitCounts"].values())
            for record in panel_records
        ),
        "allPanelsRigidToRequiredParents": motion["allParentRigid"],
        "allPanelsMoveWithPodPlantClip": motion["allMoveWithClip"],
        "bodyAtLeastNinetyPercentProjectedRoleArea": projected_area["passed"],
        "accentWithinOneToThreePercentTopAreaTarget": projected_area[
            "withinRestrainedVisualTarget"
        ],
        "sixFixedNeutralBlueOrangeProofs": set(render_records)
        == {
            "neutral-top",
            "neutral-gameplay",
            "blue-top",
            "blue-gameplay",
            "orange-top",
            "orange-gameplay",
        },
        "transparentEastTopAvatarAndSemanticMaskWritten": (
            (output_dir / art_authority["avatar"]["file"]).is_file()
            and (output_dir / art_authority["teamMask"]["file"]).is_file()
        ),
        "finalMaterialRestoredToNeutral": (
            glb_validation["roles"]["baseColorFactor"][0]
            == glb_validation["roles"]["baseColorFactor"][1]
            == glb_validation["roles"]["baseColorFactor"][2]
            and glb_validation["roles"]["emissiveFactor"][0]
            == glb_validation["roles"]["emissiveFactor"][1]
            == glb_validation["roles"]["emissiveFactor"][2]
        ),
        "glbStructuralSemanticValidationPassed": glb_validation["passed"],
        "blendAndGlbWritten": blend_path.is_file() and glb_path.is_file(),
    }
    report = {
        "schema": SCHEMA,
        "toolVersion": TOOL_VERSION,
        "blender": {
            "version": bpy.app.version_string,
            "expectedMajorMinor": list(EXPECTED_BLENDER),
        },
        "operation": {
            "providerCalls": 0,
            "networkCalls": 0,
            "sourceModified": False,
            "inputPathsPolicy": "repository-relative only",
            "outputOverwritePolicy": "new sandbox/var directory only",
            "facingAxisBlender": "+X",
            "upAxisBlender": "+Z",
        },
        "source": {
            "path": input_relative.as_posix(),
            "sha256": args.expected_source_sha256,
            "bytes": input_blend.stat().st_size,
            "rootSourceTaskId": root.get("source_task_id"),
            "rootProviderSourceSha256": root.get("source_sha256"),
            "semanticGlb": source_glb,
            "nlaTracks": nla_tracks,
        },
        "material": {
            "name": material.name,
            "role": ROLE,
            "materialCountForRole": len(role_materials_in_blend),
            "textureFree": True,
            "shader": "Principled BSDF / glTF metallic-roughness",
            "neutralHexSrgb": NEUTRAL_HEX,
            "neutralEmissionStrength": NEUTRAL_EMISSION_STRENGTH,
            "proofTeamLooks": {
                "blueHexSrgb": BLUE_HEX,
                "orangeHexSrgb": ORANGE_HEX,
                "teamEmissionStrength": TEAM_EMISSION_STRENGTH,
            },
            "runtimeContract": (
                "renderer clones the neutral standard material, reads "
                "userData.nilbotsRole, and supplies team base/emissive colour"
            ),
        },
        "panels": panel_records,
        "motion": motion,
        "projectedArea": projected_area,
        "renders": render_records,
        "artAuthority": art_authority,
        "glbRoleExtrasPatch": patch_record,
        "glbValidation": glb_validation,
        "outputs": outputs,
        "checks": checks,
        "passed": all(checks.values()),
        "limitations": [
            (
                "This stage authors rigid accent panels only; runtime loading "
                "and the existing SVG authority are intentionally unchanged."
            ),
            (
                "The GLB gate is an internal structural/semantic validator "
                "because no pinned Khronos validator is present; it does not "
                "claim external conformance certification."
            ),
            (
                "Blue and orange are proof renders only. The shipped GLB "
                "contains neutral equal-channel factors and no team texture."
            ),
        ],
    }
    report_path = output_dir / "report.json"
    report_path.write_text(
        json.dumps(report, ensure_ascii=True, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(
        "NILBOTS_TEAM_ACCENT_RESULT="
        + json.dumps(
            {
                "passed": report["passed"],
                "report": (output_relative / "report.json").as_posix(),
                "glb": outputs["glb"]["path"],
                "blend": outputs["blend"]["path"],
            },
            sort_keys=True,
        )
    )
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"error: {error}", file=sys.stderr)
        raise
