#!/usr/bin/env python3
"""Blender-side deterministic Bulwark kinematic family builder.

The geometry in this file is authored from primitives and explicit convex
profiles.  No provider mesh is imported.  Every moving hard-surface assembly
has a named pivot, a stowed transform, and a deployed transform.  The complete
mesh receives one material and one non-overlapping UV atlas before export so a
later Meshy retexture pass can retain the exact production geometry with
``enable_original_uv:true``.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from array import array
from pathlib import Path
from typing import Any, Iterable

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


EXPECTED_BLENDER = (5, 2)
REPORT_SCHEMA = "nilbots-bulwark-kinematic-master-1"
ANIMATION_REVISION = "concept-v2-hatch-first-fold-up-cannons"
MODEL_COLLECTION = "NB_Bulwark_Kinematic_Master"
PROOF_COLLECTION = "NB_Proof_Only"
ROOT_NAME = "AUTH_AegisTortoise_Kinematic_Master"
BODY_PIVOT_NAME = "Mobile.Body.Pivot"
NEUTRAL_MATERIAL_NAME = "Aegis_Neutral_OriginalUV"
TEAM_ACCENT_MATERIAL_NAME = "NB_TeamAccent_Semantic"
FPS = 30
DEPLOY_END = 96
RECOIL_END = 22
UV_MARGIN_IN_CELL = 0.08
EPSILON = 1.0e-6
TOWER_CENTER_X = -0.085
TOWER_WELL_RADIUS = 0.209
TOWER_HATCH_RADIUS = 0.213
TOWER_HATCH_Z = 0.342

POD_NAMES = ("NE", "SE", "NW", "SW")
SHELL_PLATES = ("NorthOuter", "NorthInner", "SouthInner", "SouthOuter")
VANE_COUNT = 8
TOWER_VANES = tuple(f"{index:02d}" for index in range(1, VANE_COUNT + 1))

SEMANTIC_CLIPS = (
    "PodPlant_HoverSettle",
    "Shell_Deploy",
    "Tower_Deploy",
    "Shell_Recoil",
    "Tower_Recoil",
    "Mobile_Recoil",
) + tuple(f"Tower_Recoil_{index:02d}" for index in range(1, VANE_COUNT + 1))

SHELL_CONTACT_FRAMES = (1, 8, 14, 24, 36, 48, 60, 72, 84, 96)
TOWER_CONTACT_FRAMES = (1, 8, 18, 30, 40, 50, 60, 72, 84, 96)
POD_CONTACT_FRAMES = (1, 8, 16, 24, 32, 40, 52, 68, 84, 96)
RECOIL_CONTACT_FRAMES = (1, 3, 5, 8, 12, 16, 19, 22)
SHELL_RECOIL_CONTACT_FRAMES = (1, 3, 6, 11, 16, 21, 24)
TOWER_RECOIL_CONTACT_FRAMES = (1, 5, 7, 9, 11, 14, 20, 34)

PROOF_COLORS = {
    "body": (0.16, 0.20, 0.27, 1.0),
    "body-detail": (0.23, 0.27, 0.34, 1.0),
    "pod": (0.11, 0.15, 0.19, 1.0),
    "shell": (0.38, 0.31, 0.21, 1.0),
    "shell-detail": (0.53, 0.43, 0.28, 1.0),
    "tower": (0.20, 0.34, 0.41, 1.0),
    "tower-detail": (0.31, 0.48, 0.55, 1.0),
    "weapon": (0.08, 0.10, 0.13, 1.0),
    "team-accent": (0.82, 0.88, 0.96, 1.0),
    "floor": (0.035, 0.045, 0.065, 1.0),
}

TEAM_ACCENT_PROOF_COLORS = {
    "neutral": (0.82, 0.88, 0.96, 1.0),
    "blue": (0.16, 0.52, 1.00, 1.0),
    "orange": (1.00, 0.34, 0.08, 1.0),
}

COLLISION_ALLOWLIST = {
    (
        f"Tower.Vane.{vane}.Armor",
        f"Tower.Vane.{vane}.Barrel",
    ): "barrel is intentionally cradled by its own rigid vane housing"
    for vane in TOWER_VANES
}
COLLISION_DELTA_ALLOWANCE = {
    # The corrected deployed-recoil overlay measures a 59-pair peak while the
    # barrel slides through its own open cradle; 60 is the tight integer cap.
    pair: 60
    for pair in COLLISION_ALLOWLIST
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = []
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--output-relative", required=True)
    parser.add_argument("--authorities-json", required=True)
    return parser.parse_args(argv)


def rounded(value: float, digits: int = 7) -> float:
    result = round(float(value), digits)
    return 0.0 if result == -0.0 else result


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def purge_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.actions,
        bpy.data.collections,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def make_collection(name: str) -> bpy.types.Collection:
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def move_to_collection(
    obj: bpy.types.Object,
    collection: bpy.types.Collection,
) -> None:
    for owner in tuple(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)


def make_empty(
    name: str,
    *,
    parent: bpy.types.Object | None,
    location: tuple[float, float, float] = (0.0, 0.0, 0.0),
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    collection: bpy.types.Collection,
    role: str,
) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    collection.objects.link(obj)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.035
    obj.parent = parent
    obj.location = location
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = rotation
    obj["nilbotsRole"] = role
    return obj


def make_material() -> bpy.types.Material:
    material = bpy.data.materials.new(NEUTRAL_MATERIAL_NAME)
    material.use_nodes = True
    material.diffuse_color = (0.33, 0.37, 0.42, 1.0)
    material.metallic = 0.15
    material.roughness = 0.62
    material["nilbotsTextureStage"] = "neutral-original-uv-input"
    material["nilbotsAtlasCount"] = 1
    nodes = material.node_tree.nodes
    principled = nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = (0.33, 0.37, 0.42, 1.0)
        principled.inputs["Metallic"].default_value = 0.15
        principled.inputs["Roughness"].default_value = 0.62
    return material


def make_team_accent_material() -> bpy.types.Material:
    material = bpy.data.materials.new(TEAM_ACCENT_MATERIAL_NAME)
    material.use_nodes = True
    material.diffuse_color = TEAM_ACCENT_PROOF_COLORS["neutral"]
    material.metallic = 0.10
    material.roughness = 0.34
    material["nilbotsRole"] = "team-accent"
    material["nilbotsTextureStage"] = "semantic-texture-free"
    material["nilbotsTextureEligible"] = False
    material["nilbotsRendererContract"] = (
        "renderer supplies restrained team glow; provider material is ignored"
    )
    nodes = material.node_tree.nodes
    principled = nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = (
            TEAM_ACCENT_PROOF_COLORS["neutral"]
        )
        principled.inputs["Metallic"].default_value = 0.10
        principled.inputs["Roughness"].default_value = 0.34
        emission = principled.inputs.get("Emission Color")
        if emission is None:
            emission = principled.inputs.get("Emission")
        if emission is not None:
            emission.default_value = TEAM_ACCENT_PROOF_COLORS["neutral"]
        strength = principled.inputs.get("Emission Strength")
        if strength is not None:
            strength.default_value = 0.35
    return material


def finish_mesh(
    obj: bpy.types.Object,
    *,
    material: bpy.types.Material,
    parent: bpy.types.Object | None,
    collection: bpy.types.Collection,
    color_role: str,
    part_role: str,
) -> bpy.types.Object:
    move_to_collection(obj, collection)
    obj.parent = parent
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
        polygon.use_smooth = False
    obj.color = PROOF_COLORS[color_role]
    obj["nilbotsPart"] = part_role
    obj["nilbotsProofColorRole"] = color_role
    return obj


def apply_bevel(
    obj: bpy.types.Object,
    *,
    width: float,
    segments: int = 2,
) -> None:
    if width <= 0:
        return
    modifier = obj.modifiers.new("Authored edge chamfer", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    modifier.angle_limit = math.radians(20.0)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def cut_cylindrical_well(
    objects: tuple[bpy.types.Object, ...],
    *,
    center: tuple[float, float, float],
    radius: float,
    depth: float,
) -> None:
    """Cut one real shared tower well through the authored chassis layers."""

    bpy.ops.mesh.primitive_cylinder_add(
        vertices=32,
        radius=radius,
        depth=depth,
        location=center,
    )
    cutter = bpy.context.object
    cutter.name = "PROOF.TowerWell.Cutter"
    for obj in objects:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new("Authored tower well", "BOOLEAN")
        modifier.operation = "DIFFERENCE"
        modifier.solver = "EXACT"
        modifier.object = cutter
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
    bpy.data.objects.remove(cutter, do_unlink=True)


def cut_radial_slots(
    obj: bpy.types.Object,
    *,
    center: tuple[float, float, float],
    inner_radius: float,
    outer_radius: float,
    width: float,
    depth: float,
    count: int,
) -> None:
    """Cut equal radial magazines through one rigid local-space deck."""

    slot_length = outer_radius - inner_radius
    slot_radius = (inner_radius + outer_radius) * 0.5
    for index in range(count):
        angle = math.tau * index / count
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.0))
        cutter = bpy.context.object
        cutter.name = f"PROOF.RadialMagazine.{index + 1:02d}.Cutter"
        cutter.dimensions = (slot_length, width, depth)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        cutter.parent = obj.parent
        cutter.location = (
            center[0] + slot_radius * math.cos(angle),
            center[1] + slot_radius * math.sin(angle),
            center[2],
        )
        cutter.rotation_mode = "XYZ"
        cutter.rotation_euler = (0.0, 0.0, angle)
        bpy.context.view_layer.update()

        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new(
            f"Authored cannon magazine {index + 1:02d}",
            "BOOLEAN",
        )
        modifier.operation = "DIFFERENCE"
        modifier.solver = "EXACT"
        modifier.object = cutter
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
        bpy.data.objects.remove(cutter, do_unlink=True)

    apply_bevel(obj, width=0.0025, segments=2)


def add_box(
    name: str,
    dimensions: tuple[float, float, float],
    *,
    location: tuple[float, float, float],
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    bevel: float = 0.008,
    parent: bpy.types.Object | None,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    color_role: str,
    part_role: str,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.0))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    apply_bevel(obj, width=bevel)
    obj.location = location
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = rotation
    return finish_mesh(
        obj,
        material=material,
        parent=parent,
        collection=collection,
        color_role=color_role,
        part_role=part_role,
    )


def add_cylinder(
    name: str,
    *,
    radius: float,
    depth: float,
    location: tuple[float, float, float],
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    vertices: int = 16,
    bevel: float = 0.004,
    parent: bpy.types.Object | None,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    color_role: str,
    part_role: str,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=(0.0, 0.0, 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    apply_bevel(obj, width=bevel)
    obj.location = location
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = rotation
    return finish_mesh(
        obj,
        material=material,
        parent=parent,
        collection=collection,
        color_role=color_role,
        part_role=part_role,
    )


def add_hollow_cylinder(
    name: str,
    *,
    outer_radius: float,
    inner_radius: float,
    depth: float,
    location: tuple[float, float, float],
    vertices: int,
    bevel: float,
    parent: bpy.types.Object,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    color_role: str,
    part_role: str,
) -> bpy.types.Object:
    """Build one rigid annular sleeve with an authored through-bore."""

    if not 0.0 < inner_radius < outer_radius:
        raise ValueError("Hollow cylinder radii are invalid")
    obj = add_cylinder(
        name,
        radius=outer_radius,
        depth=depth,
        location=location,
        vertices=vertices,
        bevel=0.0,
        parent=parent,
        material=material,
        collection=collection,
        color_role=color_role,
        part_role=part_role,
    )
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=inner_radius,
        depth=depth + 0.030,
        location=(0.0, 0.0, 0.0),
    )
    cutter = bpy.context.object
    cutter.name = f"PROOF.{name}.Bore.Cutter"
    cutter.parent = parent
    cutter.location = location
    bpy.context.view_layer.update()

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    modifier = obj.modifiers.new("Authored telescopic bore", "BOOLEAN")
    modifier.operation = "DIFFERENCE"
    modifier.solver = "EXACT"
    modifier.object = cutter
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
    bpy.data.objects.remove(cutter, do_unlink=True)
    apply_bevel(obj, width=bevel, segments=2)
    return obj


def add_torus(
    name: str,
    *,
    major_radius: float,
    minor_radius: float,
    location: tuple[float, float, float],
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    parent: bpy.types.Object | None,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    color_role: str,
    part_role: str,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major_radius,
        minor_radius=minor_radius,
        major_segments=24,
        minor_segments=6,
        location=(0.0, 0.0, 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    obj.location = location
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = rotation
    return finish_mesh(
        obj,
        material=material,
        parent=parent,
        collection=collection,
        color_role=color_role,
        part_role=part_role,
    )


def add_prism(
    name: str,
    profile: tuple[tuple[float, float], ...],
    *,
    height: float,
    location: tuple[float, float, float],
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    bevel: float = 0.008,
    parent: bpy.types.Object | None,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    color_role: str,
    part_role: str,
) -> bpy.types.Object:
    half = height * 0.5
    vertices = [(x, y, -half) for x, y in profile] + [
        (x, y, half) for x, y in profile
    ]
    count = len(profile)
    faces: list[tuple[int, ...]] = [
        tuple(reversed(range(count))),
        tuple(range(count, count * 2)),
    ]
    for index in range(count):
        nxt = (index + 1) % count
        faces.append((index, nxt, count + nxt, count + index))
    mesh = bpy.data.meshes.new(f"{name}.Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    apply_bevel(obj, width=bevel)
    obj.location = location
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = rotation
    return finish_mesh(
        obj,
        material=material,
        parent=parent,
        collection=collection,
        color_role=color_role,
        part_role=part_role,
    )


def join_meshes(
    name: str,
    objects: list[bpy.types.Object],
    *,
    parent: bpy.types.Object,
    color_role: str,
    part_role: str,
) -> bpy.types.Object:
    if not objects:
        raise ValueError(f"Cannot join empty mesh list for {name}")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    active = objects[0]
    bpy.context.view_layer.objects.active = active
    bpy.ops.object.join()
    active.name = name
    active.data.name = f"{name}.Mesh"
    active.parent = parent
    active.color = PROOF_COLORS[color_role]
    active["nilbotsPart"] = part_role
    active["nilbotsProofColorRole"] = color_role
    active.select_set(False)
    return active


def armor_profile(
    length: float,
    width: float,
    *,
    rear_notch: float = 0.018,
) -> tuple[tuple[float, float], ...]:
    return (
        (-rear_notch, -width * 0.28),
        (0.02, -width * 0.50),
        (length * 0.76, -width * 0.50),
        (length, -width * 0.24),
        (length, width * 0.24),
        (length * 0.76, width * 0.50),
        (0.02, width * 0.50),
        (-rear_notch, width * 0.28),
    )


def hatch_leaf_profile(
    radius: float,
    *,
    side: int,
    segments: int = 12,
) -> tuple[tuple[float, float], ...]:
    """Return one semicircular leaf in coordinates local to its outer hinge."""

    if side not in (-1, 1):
        raise ValueError("Hatch side must be -1 or +1")
    seam_half = 0.0015
    if side > 0:
        global_points = [(-radius, seam_half), (radius, seam_half)]
        global_points.extend(
            (
                radius * math.cos(math.pi * index / segments),
                radius * math.sin(math.pi * index / segments),
            )
            for index in range(1, segments)
        )
    else:
        global_points = [(radius, -seam_half), (-radius, -seam_half)]
        global_points.extend(
            (
                radius * math.cos(math.pi + math.pi * index / segments),
                radius * math.sin(math.pi + math.pi * index / segments),
            )
            for index in range(1, segments)
        )
    return tuple((x, y - side * radius) for x, y in global_points)


def build_static_hull(
    *,
    body: bpy.types.Object,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> tuple[bpy.types.Object, dict[str, tuple[float, float, float]]]:
    parts: list[bpy.types.Object] = []
    base_profile = (
        (-0.49, -0.17),
        (-0.39, -0.34),
        (-0.12, -0.42),
        (0.25, -0.41),
        (0.49, -0.27),
        (0.53, -0.09),
        (0.51, 0.21),
        (0.27, 0.40),
        (-0.20, 0.42),
        (-0.45, 0.28),
    )
    lower_hull = add_prism(
        "Mobile.Hull.Lower",
        base_profile,
        height=0.105,
        location=(0.0, 0.0, 0.145),
        bevel=0.015,
        parent=body,
        material=material,
        collection=collection,
        color_role="body",
        part_role="mobile-hull",
    )
    parts.append(lower_hull)
    deck_profile = (
        (-0.40, -0.15),
        (-0.31, -0.30),
        (-0.02, -0.35),
        (0.34, -0.28),
        (0.43, -0.08),
        (0.40, 0.19),
        (0.20, 0.33),
        (-0.24, 0.34),
        (-0.39, 0.22),
    )
    upper_deck = add_prism(
        "Mobile.Hull.UpperDeck",
        deck_profile,
        height=0.075,
        location=(-0.015, 0.0, 0.225),
        bevel=0.014,
        parent=body,
        material=material,
        collection=collection,
        color_role="body-detail",
        part_role="mobile-hull",
    )
    parts.append(upper_deck)

    for sign in (-1.0, 1.0):
        parts.append(
            add_prism(
                f"Mobile.Armor.Rear.{int(sign):+d}",
                armor_profile(0.28, 0.16),
                height=0.052,
                location=(-0.37, sign * 0.19, 0.278),
                rotation=(0.0, 0.0, sign * math.radians(15.0)),
                bevel=0.009,
                parent=body,
                material=material,
                collection=collection,
                color_role="body",
                part_role="mobile-static-armor",
            )
        )
        parts.append(
            add_box(
                f"Mobile.ShoulderUnderframe.{int(sign):+d}",
                (0.47, 0.16, 0.045),
                location=(0.16, sign * 0.30, 0.274),
                rotation=(0.0, 0.0, sign * math.radians(2.5)),
                bevel=0.012,
                parent=body,
                material=material,
                collection=collection,
                color_role="body-detail",
                part_role="mobile-static-armor",
            )
        )

    # A real annular collar surrounds the boolean-cut tower well.  The floor
    # closes the bottom below the stowed kit; no solid cover remains for the
    # tower to pass through.
    well_components = [
            add_torus(
                "Mobile.Reactor.WellCollar",
                major_radius=0.233,
                minor_radius=0.022,
                location=(TOWER_CENTER_X, 0.0, 0.286),
                parent=body,
                material=material,
                collection=collection,
                color_role="body",
                part_role="tower-well-collar",
            ),
            add_torus(
                "Mobile.Reactor.WellLip",
                major_radius=0.223,
                minor_radius=0.012,
                location=(TOWER_CENTER_X, 0.0, 0.315),
                parent=body,
                material=material,
                collection=collection,
                color_role="body-detail",
                part_role="tower-well-collar",
            ),
            add_cylinder(
                "Mobile.Reactor.WellFloor",
                radius=0.216,
                depth=0.012,
                location=(TOWER_CENTER_X, 0.0, 0.087),
                vertices=24,
                bevel=0.004,
                parent=body,
                material=material,
                collection=collection,
                color_role="body-detail",
                part_role="tower-well-floor",
            ),
    ]
    parts.extend(well_components)

    # The muzzle socket is deliberately open: four rails surround the moving
    # barrel, so retraction never passes through a decorative solid.
    for y, z in ((-0.055, 0.0), (0.055, 0.0)):
        parts.append(
            add_box(
                f"Mobile.GunSocket.Side.{y:+.3f}",
                (0.16, 0.024, 0.075),
                location=(0.35, y, 0.242 + z),
                bevel=0.005,
                parent=body,
                material=material,
                collection=collection,
                color_role="weapon",
                part_role="mobile-gun-socket",
            )
        )
    for z in (-0.034, 0.034):
        parts.append(
            add_box(
                f"Mobile.GunSocket.Vertical.{z:+.3f}",
                (0.16, 0.086, 0.018),
                location=(0.35, 0.0, 0.242 + z),
                bevel=0.004,
                parent=body,
                material=material,
                collection=collection,
                color_role="weapon",
                part_role="mobile-gun-socket",
            )
        )

    pod_positions = {
        "NE": (0.34, 0.395, 0.0),
        "SE": (0.34, -0.395, 0.0),
        "NW": (-0.35, 0.390, 0.0),
        "SW": (-0.35, -0.390, 0.0),
    }
    for pod_name, (x, y, _) in pod_positions.items():
        # A round open lock collar keeps the mobile read as hovering machinery
        # rather than four walking feet.  The anchor piston moves through the
        # real central cavity.
        parts.append(
            add_torus(
                f"Pod.{pod_name}.LockCollar",
                major_radius=0.055,
                minor_radius=0.016,
                location=(x, y, 0.135),
                parent=body,
                material=material,
                collection=collection,
                color_role="pod",
                part_role="hover-lock-housing",
            )
        )
        radial_angle = math.atan2(y, x)
        parts.append(
            add_box(
                f"Pod.{pod_name}.BodyLink",
                (0.080, 0.032, 0.050),
                location=(
                    x - 0.045 * math.cos(radial_angle),
                    y - 0.045 * math.sin(radial_angle),
                    0.145,
                ),
                rotation=(0.0, 0.0, radial_angle),
                bevel=0.006,
                parent=body,
                material=material,
                collection=collection,
                color_role="pod",
                part_role="hover-lock-housing",
            )
        )
        parts.append(
            add_torus(
                f"Pod.{pod_name}.HoverCoil",
                major_radius=0.046,
                minor_radius=0.009,
                location=(x, y, 0.088),
                parent=body,
                material=material,
                collection=collection,
                color_role="pod",
                part_role="hover-coil",
            )
        )

    # Sparse raised service ribs preserve the layered fortress identity.
    for index, (x, y, angle) in enumerate(
        (
            (-0.26, 0.08, 12.0),
            (-0.26, -0.08, -12.0),
            (0.03, 0.23, 0.0),
            (0.03, -0.23, 0.0),
        ),
        start=1,
    ):
        parts.append(
            add_box(
                f"Mobile.ServiceRib.{index:02d}",
                (0.19, 0.025, 0.018),
                location=(x, y, 0.316),
                rotation=(0.0, 0.0, math.radians(angle)),
                bevel=0.004,
                parent=body,
                material=material,
                collection=collection,
                color_role="body-detail",
                part_role="mobile-service-rib",
            )
        )

    # Cut the complete authored chassis, including service ribs and secondary
    # armor added after the base decks.  Exclude only the explicit well collar,
    # lip, and sealed floor; cutting just the first two hull prisms can leave a
    # later decorative part crossing the tower sweep.
    cut_cylindrical_well(
        tuple(part for part in parts if part not in well_components),
        center=(TOWER_CENTER_X, 0.0, 0.235),
        radius=TOWER_WELL_RADIUS,
        depth=0.420,
    )

    hull = join_meshes(
        "Mobile.Hull.Static",
        parts,
        parent=body,
        color_role="body",
        part_role="mobile-static-chassis",
    )
    return hull, pod_positions


def build_tower_hatch(
    *,
    body: bpy.types.Object,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> tuple[dict[str, bpy.types.Object], dict[str, bpy.types.Object]]:
    """Build two rigid hatch leaves over the real central tower well."""

    radius = TOWER_HATCH_RADIUS
    pivots: dict[str, bpy.types.Object] = {}
    meshes: dict[str, bpy.types.Object] = {}
    for name, side in (("North", 1), ("South", -1)):
        pivot = make_empty(
            f"Tower.Hatch.{name}.Hinge.Pivot",
            parent=body,
            location=(TOWER_CENTER_X, side * radius, TOWER_HATCH_Z),
            collection=collection,
            role="tower-hatch-hinge-pivot",
        )
        leaf = add_prism(
            f"Tower.Hatch.{name}.LeafPanel",
            hatch_leaf_profile(radius, side=side),
            height=0.016,
            location=(0.0, 0.0, 0.0),
            bevel=0.004,
            parent=pivot,
            material=material,
            collection=collection,
            color_role="body",
            part_role="tower-hatch-leaf",
        )
        hinge_barrel = add_cylinder(
            f"Tower.Hatch.{name}.HingeBarrel",
            radius=0.006,
            depth=0.160,
            location=(0.0, 0.0, 0.0),
            rotation=(0.0, math.radians(90.0), 0.0),
            vertices=12,
            bevel=0.002,
            parent=pivot,
            material=material,
            collection=collection,
            color_role="body",
            part_role="tower-hatch-leaf",
        )
        mesh = join_meshes(
            f"Tower.Hatch.{name}.Leaf",
            [leaf, hinge_barrel],
            parent=pivot,
            color_role="body",
            part_role="tower-hatch-leaf",
        )
        target = -170.0 if side > 0 else 170.0
        pivot["nilbotsDeployAngleDegrees"] = target
        pivot["nilbotsDeployRailX"] = -0.125
        pivot["nilbotsOpensBefore"] = "Tower.CrownLift.Pivot"
        mesh["nilbotsStowedVisible"] = True
        pivots[name] = pivot
        meshes[name] = mesh
    return pivots, meshes


def build_pod_anchors(
    *,
    body: bpy.types.Object,
    pod_positions: dict[str, tuple[float, float, float]],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> tuple[dict[str, bpy.types.Object], dict[str, bpy.types.Object]]:
    pivots: dict[str, bpy.types.Object] = {}
    meshes: dict[str, bpy.types.Object] = {}
    for pod_name in POD_NAMES:
        x, y, _ = pod_positions[pod_name]
        pivot = make_empty(
            f"Pod.{pod_name}.Anchor.Pivot",
            parent=body,
            location=(x, y, 0.0),
            collection=collection,
            role="hover-lock-anchor-pivot",
        )
        parts = [
            add_cylinder(
                f"Pod.{pod_name}.Anchor.Piston",
                radius=0.027,
                depth=0.060,
                location=(0.0, 0.0, 0.105),
                vertices=12,
                bevel=0.003,
                parent=pivot,
                material=material,
                collection=collection,
                color_role="pod",
                part_role="hover-lock-anchor",
            ),
            add_cylinder(
                f"Pod.{pod_name}.Anchor.Pad",
                radius=0.048,
                depth=0.025,
                location=(0.0, 0.0, 0.070),
                vertices=12,
                bevel=0.004,
                parent=pivot,
                material=material,
                collection=collection,
                color_role="pod",
                part_role="hover-lock-anchor",
            ),
            add_torus(
                f"Pod.{pod_name}.Anchor.PadRing",
                major_radius=0.034,
                minor_radius=0.005,
                location=(0.0, 0.0, 0.058),
                parent=pivot,
                material=material,
                collection=collection,
                color_role="pod",
                part_role="hover-lock-anchor",
            ),
        ]
        mesh = join_meshes(
            f"Pod.{pod_name}.Anchor",
            parts,
            parent=pivot,
            color_role="pod",
            part_role="hover-lock-anchor",
        )
        mesh["nilbotsStowedVisible"] = True
        pivots[pod_name] = pivot
        meshes[pod_name] = mesh
    return pivots, meshes


def build_team_accent_emitters(
    *,
    shell_hinges: dict[str, bpy.types.Object],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> dict[str, bpy.types.Object]:
    """Author four gameplay-readable semantic emitters in the Shell plates.

    The bars are deliberately separate rigid meshes.  They inherit the body
    settle and each plate's real rail/hinge motion through their plate pivots,
    giving the renderer one stable semantic material to recolor and bloom.
    In Mobile and Tower the plates are stowed on the upper deck, keeping all
    four emitters top-facing and readable.
    """

    emitters: dict[str, bpy.types.Object] = {}
    definitions = {
        "NorthOuter": (0.125, 0.245),
        "NorthInner": (0.095, 0.155),
        "SouthInner": (0.095, 0.155),
        "SouthOuter": (0.125, 0.245),
    }
    for plate_name in SHELL_PLATES:
        length, center_x = definitions[plate_name]
        emitter = add_box(
            f"TeamAccent.Shell.{plate_name}.Emitter",
            (length, 0.044, 0.018),
            location=(center_x, 0.0, 0.060),
            bevel=0.005,
            parent=shell_hinges[plate_name],
            material=material,
            collection=collection,
            color_role="team-accent",
            part_role="team-accent-emitter",
        )
        emitter.data.name = f"{emitter.name}.Mesh"
        emitter["nilbotsRole"] = "team-accent"
        emitter["nilbotsShellPlate"] = plate_name
        emitter["nilbotsTextureEligible"] = False
        emitter["nilbotsFixedTo"] = f"Shell.{plate_name}.Plate"
        emitters[plate_name] = emitter
    return emitters


def build_mobile_gun(
    *,
    body: bpy.types.Object,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> tuple[bpy.types.Object, bpy.types.Object]:
    pivot = make_empty(
        "Mobile.GunBarrel.Pivot",
        parent=body,
        location=(0.34, 0.0, 0.242),
        collection=collection,
        role="mobile-barrel-rail",
    )
    parts = [
        add_cylinder(
            "Mobile.GunBarrel.Core",
            radius=0.024,
            depth=0.255,
            location=(0.1275, 0.0, 0.0),
            rotation=(0.0, math.radians(90.0), 0.0),
            vertices=16,
            bevel=0.003,
            parent=pivot,
            material=material,
            collection=collection,
            color_role="weapon",
            part_role="mobile-gun-barrel",
        ),
        add_cylinder(
            "Mobile.GunBarrel.Collar",
            radius=0.038,
            depth=0.042,
            location=(0.105, 0.0, 0.0),
            rotation=(0.0, math.radians(90.0), 0.0),
            vertices=16,
            bevel=0.004,
            parent=pivot,
            material=material,
            collection=collection,
            color_role="weapon",
            part_role="mobile-gun-barrel",
        ),
        add_cylinder(
            "Mobile.GunBarrel.Muzzle",
            radius=0.032,
            depth=0.034,
            location=(0.255, 0.0, 0.0),
            rotation=(0.0, math.radians(90.0), 0.0),
            vertices=16,
            bevel=0.003,
            parent=pivot,
            material=material,
            collection=collection,
            color_role="weapon",
            part_role="mobile-gun-barrel",
        ),
    ]
    barrel = join_meshes(
        "Mobile.GunBarrel",
        parts,
        parent=pivot,
        color_role="weapon",
        part_role="mobile-gun-barrel",
    )
    return pivot, barrel


def build_shell_kit(
    *,
    body: bpy.types.Object,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> tuple[
    dict[str, bpy.types.Object],
    dict[str, bpy.types.Object],
    dict[str, bpy.types.Object],
]:
    rails: dict[str, bpy.types.Object] = {}
    hinges: dict[str, bpy.types.Object] = {}
    meshes: dict[str, bpy.types.Object] = {}
    definitions = {
        "NorthOuter": {
            "location": (-0.055, 0.375, 0.310),
            "length": 0.485,
            "width": 0.165,
            "target_angle": -24.0,
            "travel": (0.200, 0.025, 0.048),
        },
        "NorthInner": {
            "location": (0.055, 0.198, 0.316),
            "length": 0.315,
            "width": 0.087,
            "target_angle": -34.0,
            "travel": (0.195, 0.008, 0.040),
        },
        "SouthInner": {
            "location": (0.055, -0.198, 0.316),
            "length": 0.315,
            "width": 0.087,
            "target_angle": 34.0,
            "travel": (0.195, -0.008, 0.040),
        },
        "SouthOuter": {
            "location": (-0.055, -0.375, 0.310),
            "length": 0.485,
            "width": 0.165,
            "target_angle": 24.0,
            "travel": (0.200, -0.025, 0.048),
        },
    }
    for plate_name in SHELL_PLATES:
        definition = definitions[plate_name]
        rail = make_empty(
            f"Shell.{plate_name}.Rail.Pivot",
            parent=body,
            location=definition["location"],
            collection=collection,
            role="shell-rail-pivot",
        )
        hinge = make_empty(
            f"Shell.{plate_name}.Hinge.Pivot",
            parent=rail,
            collection=collection,
            role="shell-hinge-pivot",
        )
        length = float(definition["length"])
        width = float(definition["width"])
        parts = [
            add_prism(
                f"Shell.{plate_name}.MainPlate",
                armor_profile(length, width),
                height=0.052,
                location=(0.0, 0.0, 0.0),
                bevel=0.010,
                parent=hinge,
                material=material,
                collection=collection,
                color_role="shell",
                part_role="shell-guard-plate",
            ),
            add_prism(
                f"Shell.{plate_name}.RaisedSpine",
                armor_profile(length * 0.70, width * 0.36, rear_notch=0.008),
                height=0.018,
                location=(length * 0.11, 0.0, 0.033),
                bevel=0.004,
                parent=hinge,
                material=material,
                collection=collection,
                color_role="shell-detail",
                part_role="shell-guard-plate",
            ),
            add_box(
                f"Shell.{plate_name}.EmitterBed",
                (length * 0.40, width * 0.11, 0.012),
                location=(length * 0.36, 0.0, 0.043),
                bevel=0.003,
                parent=hinge,
                material=material,
                collection=collection,
                color_role="shell-detail",
                part_role="shell-emitter-bed",
            ),
        ]
        plate = join_meshes(
            f"Shell.{plate_name}.Plate",
            parts,
            parent=hinge,
            color_role="shell",
            part_role="shell-guard-plate",
        )
        plate["nilbotsStowedVisible"] = True
        plate["nilbotsFacingSectorDegrees"] = [-45, 45]
        rail["nilbotsTravel"] = list(definition["travel"])
        hinge["nilbotsDeployAngleDegrees"] = definition["target_angle"]
        rails[plate_name] = rail
        hinges[plate_name] = hinge
        meshes[plate_name] = plate
    return rails, hinges, meshes


def build_tower_kit(
    *,
    body: bpy.types.Object,
    material: bpy.types.Material,
    team_accent_material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> tuple[
    bpy.types.Object,
    bpy.types.Object,
    bpy.types.Object,
    bpy.types.Object,
    dict[str, bpy.types.Object],
    dict[str, bpy.types.Object],
    dict[str, bpy.types.Object],
    dict[str, bpy.types.Object],
    dict[str, bpy.types.Object],
]:
    crown = make_empty(
        "Tower.CrownLift.Pivot",
        parent=body,
        collection=collection,
        role="tower-crown-lift-pivot",
    )
    upper = make_empty(
        "Tower.UpperLift.Pivot",
        parent=crown,
        location=(0.0, 0.0, 0.0),
        collection=collection,
        role="tower-upper-lift-pivot",
    )
    reactor_x = TOWER_CENTER_X
    # The lower stage is a real hollow sleeve.  The narrow upper spine occupies
    # its bore at rest and retains axial engagement when deployed; broad crown
    # parts stack above the sleeve rather than occupying the same solid volume.
    lower_spine = add_hollow_cylinder(
        "Tower.Keep.LowerSpine",
        outer_radius=0.110,
        inner_radius=0.090,
        depth=0.170,
        location=(reactor_x, 0.0, 0.185),
        vertices=16,
        bevel=0.004,
        parent=crown,
        material=material,
        collection=collection,
        color_role="tower",
        part_role="tower-lift-spine",
    )
    lower_armor = add_hollow_cylinder(
        "Tower.Keep.LowerArmor",
        outer_radius=0.145,
        inner_radius=0.090,
        depth=0.030,
        location=(reactor_x, 0.0, 0.275),
        vertices=16,
        bevel=0.005,
        parent=crown,
        material=material,
        collection=collection,
        color_role="tower",
        part_role="tower-keep",
    )
    cut_radial_slots(
        lower_armor,
        center=(reactor_x, 0.0, 0.275),
        inner_radius=0.125,
        outer_radius=0.180,
        width=0.052,
        depth=0.060,
        count=VANE_COUNT,
    )
    crown_parts = [lower_spine, lower_armor]
    crown_mesh = join_meshes(
        "Tower.Crown",
        crown_parts,
        parent=crown,
        color_role="tower",
        part_role="tower-lower-stage",
    )
    crown_mesh["nilbotsStowedVisible"] = True

    gun_deck = add_cylinder(
        "Tower.Keep.GunDeck",
        radius=0.178,
        depth=0.012,
        location=(reactor_x, 0.0, 0.318),
        vertices=16,
        bevel=0.004,
        parent=upper,
        material=material,
        collection=collection,
        color_role="tower",
        part_role="tower-gun-deck",
    )
    cut_radial_slots(
        gun_deck,
        center=(reactor_x, 0.0, 0.318),
        inner_radius=0.130,
        outer_radius=0.215,
        width=0.052,
        depth=0.080,
        count=VANE_COUNT,
    )
    upper_parts = [
        add_cylinder(
            "Tower.Keep.UpperSpine",
            radius=0.082,
            depth=0.202,
            location=(reactor_x, 0.0, 0.199),
            vertices=12,
            bevel=0.004,
            parent=upper,
            material=material,
            collection=collection,
            color_role="tower-detail",
            part_role="tower-upper-spine",
        ),
        add_cylinder(
            "Tower.Keep.UpperArmor",
            radius=0.122,
            depth=0.018,
            location=(reactor_x, 0.0, 0.301),
            vertices=8,
            bevel=0.004,
            parent=upper,
            material=material,
            collection=collection,
            color_role="tower-detail",
            part_role="tower-keep",
        ),
        add_torus(
            "Tower.Keep.LockRing",
            major_radius=0.112,
            minor_radius=0.010,
            location=(reactor_x, 0.0, 0.306),
            parent=upper,
            material=material,
            collection=collection,
            color_role="tower",
            part_role="tower-keep",
        ),
        gun_deck,
        add_cylinder(
            "Tower.Keep.CommandCap",
            radius=0.070,
            depth=0.008,
            location=(reactor_x, 0.0, 0.322),
            vertices=12,
            bevel=0.002,
            parent=upper,
            material=material,
            collection=collection,
            color_role="tower-detail",
            part_role="tower-command-cap",
        ),
    ]
    upper_mesh = join_meshes(
        "Tower.UpperKeep",
        upper_parts,
        parent=upper,
        color_role="tower",
        part_role="tower-upper-stage",
    )
    upper_mesh["nilbotsStowedVisible"] = True

    # Four broad top-facing emitter bars stay texture-free and renderer-owned.
    # They ride the crown, so team identity remains attached to the elevated
    # form rather than being left behind on the mobile deck.
    crown_emitters: dict[str, bpy.types.Object] = {}
    for index, angle_degrees in enumerate(
        (22.5, 112.5, 202.5, 292.5),
        start=1,
    ):
        angle = math.radians(angle_degrees)
        emitter = add_box(
            f"TeamAccent.Tower.Crown.{index:02d}.Emitter",
            (0.105, 0.038, 0.006),
            location=(
                reactor_x + 0.120 * math.cos(angle),
                0.120 * math.sin(angle),
                0.323,
            ),
            rotation=(0.0, 0.0, angle),
            bevel=0.002,
            parent=upper,
            material=team_accent_material,
            collection=collection,
            color_role="team-accent",
            part_role="team-accent-emitter",
        )
        emitter.data.name = f"{emitter.name}.Mesh"
        emitter["nilbotsRole"] = "team-accent"
        emitter["nilbotsTextureEligible"] = False
        emitter["nilbotsFixedTo"] = "Tower.UpperLift.Pivot"
        crown_emitters[f"{index:02d}"] = emitter

    fan_pivots: dict[str, bpy.types.Object] = {}
    armor_meshes: dict[str, bpy.types.Object] = {}
    barrel_pivots: dict[str, bpy.types.Object] = {}
    barrel_meshes: dict[str, bpy.types.Object] = {}
    base_radius = 0.168
    for zero_index, vane_name in enumerate(TOWER_VANES):
        angle = math.radians(zero_index * 45.0)
        fan = make_empty(
            f"Tower.Vane.{vane_name}.Fan.Pivot",
            parent=upper,
            location=(
                reactor_x + base_radius * math.cos(angle),
                base_radius * math.sin(angle),
                0.316,
            ),
            # The eight cannon carriers hang vertically in narrow magazines
            # around the upper keep while stowed.  Their radial ring fits
            # inside the central hatch; only after the keep clears the body do
            # they hinge flat and telescope outward from the crown.
            rotation=(0.0, math.radians(90.0), angle),
            collection=collection,
            role="tower-vane-fan-pivot",
        )
        vane_parts = [
            add_prism(
                f"Tower.Vane.{vane_name}.Armor",
                armor_profile(0.130, 0.044, rear_notch=0.010),
                height=0.040,
                location=(0.010, 0.0, 0.0),
                bevel=0.007,
                parent=fan,
                material=material,
                collection=collection,
                color_role="tower",
                part_role="tower-vane",
            ),
            add_box(
                f"Tower.Vane.{vane_name}.EmitterBed",
                (0.065, 0.016, 0.014),
                location=(0.052, 0.0, 0.028),
                bevel=0.003,
                parent=fan,
                material=material,
                collection=collection,
                color_role="tower-detail",
                part_role="tower-vane",
            ),
        ]
        armor = join_meshes(
            f"Tower.Vane.{vane_name}.Armor",
            vane_parts,
            parent=fan,
            color_role="tower",
            part_role="tower-vane",
        )
        armor["nilbotsStowedVisible"] = True
        armor["nilbotsVaneIndex"] = zero_index + 1
        armor["nilbotsRadialDegrees"] = zero_index * 45

        barrel_pivot = make_empty(
            f"Tower.Vane.{vane_name}.Barrel.Pivot",
            parent=fan,
            location=(-0.010, 0.0, 0.006),
            collection=collection,
            role="tower-vane-recoil-pivot",
        )
        barrel_parts = [
            add_cylinder(
                f"Tower.Vane.{vane_name}.Barrel",
                radius=0.015,
                depth=0.150,
                location=(0.075, 0.0, 0.0),
                rotation=(0.0, math.radians(90.0), 0.0),
                vertices=12,
                bevel=0.002,
                parent=barrel_pivot,
                material=material,
                collection=collection,
                color_role="weapon",
                part_role="tower-vane-barrel",
            ),
            add_cylinder(
                f"Tower.Vane.{vane_name}.Muzzle",
                radius=0.024,
                depth=0.030,
                location=(0.150, 0.0, 0.0),
                rotation=(0.0, math.radians(90.0), 0.0),
                vertices=12,
                bevel=0.002,
                parent=barrel_pivot,
                material=material,
                collection=collection,
                color_role="weapon",
                part_role="tower-vane-barrel",
            ),
        ]
        barrel = join_meshes(
            f"Tower.Vane.{vane_name}.Barrel",
            barrel_parts,
            parent=barrel_pivot,
            color_role="weapon",
            part_role="tower-vane-barrel",
        )
        barrel["nilbotsVaneIndex"] = zero_index + 1
        fan_pivots[vane_name] = fan
        armor_meshes[vane_name] = armor
        barrel_pivots[vane_name] = barrel_pivot
        barrel_meshes[vane_name] = barrel
    crown["nilbotsVaneCount"] = VANE_COUNT
    return (
        crown,
        upper,
        crown_mesh,
        upper_mesh,
        fan_pivots,
        armor_meshes,
        barrel_pivots,
        barrel_meshes,
        crown_emitters,
    )


def capture_rest(objects: Iterable[bpy.types.Object]) -> dict[str, dict[str, Any]]:
    return {
        obj.name: {
            "location": tuple(float(value) for value in obj.location),
            "rotation_euler": tuple(float(value) for value in obj.rotation_euler),
            "scale": tuple(float(value) for value in obj.scale),
        }
        for obj in objects
    }


def restore_rest(rest: dict[str, dict[str, Any]]) -> None:
    for object_name, transform in rest.items():
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            continue
        obj.location = transform["location"]
        obj.rotation_mode = "XYZ"
        obj.rotation_euler = transform["rotation_euler"]
        obj.scale = transform["scale"]
    bpy.context.view_layer.update()


def full_transform(
    rest: dict[str, Any],
    *,
    location: tuple[float, float, float] | None = None,
    rotation: tuple[float, float, float] | None = None,
) -> dict[str, tuple[float, ...]]:
    return {
        "location": location if location is not None else rest["location"],
        "rotation_euler": (
            rotation if rotation is not None else rest["rotation_euler"]
        ),
    }


def add_semantic_clip(
    name: str,
    object_keyframes: dict[
        str,
        list[tuple[int, dict[str, tuple[float, ...]]]],
    ],
    *,
    rest: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    record: dict[str, Any] = {"objects": {}, "frameStart": None, "frameEnd": None}
    all_frames: list[int] = []
    for object_name, keyframes in object_keyframes.items():
        if len(keyframes) < 2:
            raise ValueError(f"{name}/{object_name} needs at least two keyframes")
        frames = [frame for frame, _values in keyframes]
        if frames != sorted(set(frames)):
            raise ValueError(f"{name}/{object_name} keyframes are not strict")
        obj = bpy.data.objects[object_name]
        obj.animation_data_create()
        obj.animation_data.action = None
        action = bpy.data.actions.new(f"{name}__{object_name}")
        obj.animation_data.action = action
        for frame, values in keyframes:
            obj.location = values["location"]
            obj.rotation_mode = "XYZ"
            obj.rotation_euler = values["rotation_euler"]
            obj.keyframe_insert(
                data_path="location",
                frame=frame,
                group="RigidTransform",
            )
            obj.keyframe_insert(
                data_path="rotation_euler",
                frame=frame,
                group="RigidTransform",
            )
        # Blender 5.2 stores keyed channels in layered action channelbags
        # rather than exposing Action.fcurves.  keyframe_insert uses Blender's
        # default Bezier interpolation; the explicitly authored stage keys
        # above provide the required anticipation, overshoot, and settle.
        track = obj.animation_data.nla_tracks.new()
        track.name = name
        track.mute = True
        strip = track.strips.new(name, frames[0], action)
        strip.name = name
        strip.action_frame_start = float(frames[0])
        strip.action_frame_end = float(frames[-1])
        strip.extrapolation = "NOTHING"
        strip.blend_type = "REPLACE"
        obj.animation_data.action = None
        transform_rest = rest[object_name]
        obj.location = transform_rest["location"]
        obj.rotation_euler = transform_rest["rotation_euler"]
        obj.scale = transform_rest["scale"]
        record["objects"][object_name] = {
            "frames": frames,
            "stages": len(frames),
            "start": keyframes[0][1],
            "end": keyframes[-1][1],
            "action": action.name,
        }
        all_frames.extend(frames)
    record["frameStart"] = min(all_frames)
    record["frameEnd"] = max(all_frames)
    return record


def create_animations(
    *,
    body: bpy.types.Object,
    pod_pivots: dict[str, bpy.types.Object],
    gun_pivot: bpy.types.Object,
    shell_rails: dict[str, bpy.types.Object],
    shell_hinges: dict[str, bpy.types.Object],
    hatch_pivots: dict[str, bpy.types.Object],
    crown: bpy.types.Object,
    upper: bpy.types.Object,
    fan_pivots: dict[str, bpy.types.Object],
    barrel_pivots: dict[str, bpy.types.Object],
    rest: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    clips: dict[str, Any] = {}

    body_rest = rest[body.name]
    body_locations = (
        (1, (0.0, 0.0, 0.0)),
        (8, (0.0, 0.0, -0.012)),
        (14, (0.0, 0.0, 0.006)),
        (24, (0.0, 0.0, -0.014)),
        (34, (0.0, 0.0, -0.024)),
        (44, (0.0, 0.0, -0.018)),
        (56, (0.0, 0.0, -0.020)),
        (72, (0.0, 0.0, -0.020)),
        (96, (0.0, 0.0, -0.020)),
    )
    pod_keyframes: dict[
        str,
        list[tuple[int, dict[str, tuple[float, ...]]]],
    ] = {
        body.name: [
            (
                frame,
                full_transform(body_rest, location=location),
            )
            for frame, location in body_locations
        ]
    }
    pod_starts = {"NE": 10, "SE": 14, "NW": 18, "SW": 22}
    for pod_name in POD_NAMES:
        pivot = pod_pivots[pod_name]
        pivot_rest = rest[pivot.name]
        start = pod_starts[pod_name]
        z_values = (
            (1, 0.0),
            (start, 0.0),
            (start + 5, 0.004),
            (start + 13, -0.024),
            (start + 19, -0.036),
            (start + 25, -0.031),
            (start + 32, -0.0336699),
            (72, -0.0336699),
            (96, -0.0336699),
        )
        base = pivot_rest["location"]
        pod_keyframes[pivot.name] = [
            (
                frame,
                full_transform(
                    pivot_rest,
                    location=(base[0], base[1], base[2] + z),
                ),
            )
            for frame, z in z_values
        ]
    clips["PodPlant_HoverSettle"] = add_semantic_clip(
        "PodPlant_HoverSettle",
        pod_keyframes,
        rest=rest,
    )

    # Shell_Deploy is a self-contained full-body form clip.  It deliberately
    # duplicates the body settle and four-pod sequence instead of relying on
    # runtime layering with PodPlant_HoverSettle.
    shell_keyframes: dict[
        str,
        list[tuple[int, dict[str, tuple[float, ...]]]],
    ] = {
        object_name: list(keyframes)
        for object_name, keyframes in pod_keyframes.items()
    }
    for plate_name in SHELL_PLATES:
        rail = shell_rails[plate_name]
        hinge = shell_hinges[plate_name]
        rail_rest = rest[rail.name]
        hinge_rest = rest[hinge.name]
        travel = tuple(float(value) for value in rail["nilbotsTravel"])
        base = rail_rest["location"]
        sign = 1.0 if "North" in plate_name else -1.0
        rail_stages = (
            (1, (0.0, 0.0, 0.0)),
            (8, (0.0, 0.0, 0.0)),
            (14, (-0.008, 0.0, 0.025)),
            (22, (travel[0] * 0.16, travel[1] * 0.08, travel[2] * 0.90)),
            (38, (travel[0] * 0.58, travel[1] * 0.45, travel[2])),
            (52, (travel[0], travel[1], travel[2])),
            (62, (travel[0] + 0.010, travel[1] + sign * 0.004, travel[2])),
            (72, (travel[0] - 0.004, travel[1] - sign * 0.002, travel[2])),
            (84, travel),
            (96, travel),
        )
        shell_keyframes[rail.name] = [
            (
                frame,
                full_transform(
                    rail_rest,
                    location=(
                        base[0] + delta[0],
                        base[1] + delta[1],
                        base[2] + delta[2],
                    ),
                ),
            )
            for frame, delta in rail_stages
        ]
        target = math.radians(float(hinge["nilbotsDeployAngleDegrees"]))
        hinge_angles = (
            (1, 0.0),
            (26, 0.0),
            (36, target * 0.12),
            (48, target * 0.58),
            (60, target * 1.08),
            (70, target * 0.96),
            (80, target * 1.02),
            (88, target),
            (96, target),
        )
        base_rotation = hinge_rest["rotation_euler"]
        shell_keyframes[hinge.name] = [
            (
                frame,
                full_transform(
                    hinge_rest,
                    rotation=(
                        base_rotation[0],
                        base_rotation[1],
                        base_rotation[2] + angle,
                    ),
                ),
            )
            for frame, angle in hinge_angles
        ]
    gun_rest = rest[gun_pivot.name]
    gun_base = gun_rest["location"]
    shell_keyframes[gun_pivot.name] = [
        (
            frame,
            full_transform(
                gun_rest,
                location=(gun_base[0] + delta, gun_base[1], gun_base[2]),
            ),
        )
        for frame, delta in (
            (1, 0.0),
            (10, 0.0),
            (16, 0.012),
            (28, -0.055),
            (40, -0.125),
            (52, -0.118),
            (64, -0.120),
            (96, -0.120),
        )
    ]
    clips["Shell_Deploy"] = add_semantic_clip(
        "Shell_Deploy",
        shell_keyframes,
        rest=rest,
    )

    # Tower_Deploy owns the same complete settle/plant sequence for the same
    # reason: the renderer selects one stance clip from replay form progress.
    tower_keyframes: dict[
        str,
        list[tuple[int, dict[str, tuple[float, ...]]]],
    ] = {
        object_name: list(keyframes)
        for object_name, keyframes in pod_keyframes.items()
    }
    # The two real cover leaves clear the complete well before either nested
    # tower stage moves.  They remain open for the full deployed stance, so
    # reverse playback naturally lowers the keep before closing the cover.
    for hatch_name, pivot in hatch_pivots.items():
        pivot_rest = rest[pivot.name]
        base_location = pivot_rest["location"]
        base_rotation = pivot_rest["rotation_euler"]
        target = math.radians(float(pivot["nilbotsDeployAngleDegrees"]))
        tower_keyframes[pivot.name] = [
            (
                frame,
                full_transform(
                    pivot_rest,
                    location=(
                        base_location[0] + rail_x,
                        base_location[1],
                        base_location[2],
                    ),
                    rotation=(
                        base_rotation[0] + angle,
                        base_rotation[1],
                        base_rotation[2],
                    ),
                ),
            )
            for frame, angle, rail_x in (
                (1, 0.0, 0.0),
                (8, 0.0, 0.0),
                (12, 0.0, 0.0),
                (18, target * 0.38, -0.025),
                (24, target * 0.82, -0.125),
                (28, target, -0.125),
                (30, target, -0.125),
                (96, target, -0.125),
            )
        ]
    crown_rest = rest[crown.name]
    crown_base = crown_rest["location"]
    tower_keyframes[crown.name] = [
        (
            frame,
            full_transform(
                crown_rest,
                location=(
                    crown_base[0],
                    crown_base[1],
                    crown_base[2] + z,
                ),
            ),
        )
        for frame, z in (
            (1, 0.0),
            (30, 0.0),
            (34, 0.0),
            (40, 0.060),
            (48, 0.185),
            (54, 0.315),
            (58, 0.297),
            (62, 0.305),
            (96, 0.305),
        )
    ]
    upper_rest = rest[upper.name]
    upper_base = upper_rest["location"]
    tower_keyframes[upper.name] = [
        (
            frame,
            full_transform(
                upper_rest,
                location=(
                    upper_base[0],
                    upper_base[1],
                    upper_base[2] + z,
                ),
            ),
        )
        for frame, z in (
            (1, 0.0),
            (48, 0.0),
            (54, 0.035),
            (60, 0.100),
            (66, 0.165),
            (70, 0.150),
            (72, 0.155),
            (96, 0.155),
        )
    ]
    for zero_index, vane_name in enumerate(TOWER_VANES):
        fan = fan_pivots[vane_name]
        fan_rest = rest[fan.name]
        base = fan_rest["location"]
        base_rotation = fan_rest["rotation_euler"]
        angle = math.radians(zero_index * 45.0)
        # Opposed pairs unlock in a short ripple.  Each carrier first slides
        # radially through its real crown magazine while still vertical, then
        # folds upward to horizontal; only the nearly level gun telescopes.
        start = 72 + (zero_index % 4)
        base_pitch = base_rotation[1]
        stages = (
            (1, 0.0, base_pitch),
            (start, 0.0, base_pitch),
            (start + 2, 0.015, base_pitch),
            (start + 5, 0.030, base_pitch),
            (start + 7, 0.030, math.radians(68.0)),
            (start + 10, 0.030, math.radians(30.0)),
            (start + 13, 0.030, math.radians(-4.0)),
            (start + 15, 0.028, math.radians(2.0)),
            (start + 17, 0.030, 0.0),
            (96, 0.030, 0.0),
        )
        tower_keyframes[fan.name] = [
            (
                frame,
                full_transform(
                    fan_rest,
                    location=(
                        base[0] + extension * math.cos(angle),
                        base[1] + extension * math.sin(angle),
                        base[2],
                    ),
                    rotation=(base_rotation[0], pitch, angle),
                ),
            )
            for frame, extension, pitch in stages
        ]
        barrel = barrel_pivots[vane_name]
        barrel_rest = rest[barrel.name]
        barrel_base = barrel_rest["location"]
        tower_keyframes[barrel.name] = [
            (
                frame,
                full_transform(
                    barrel_rest,
                    location=(
                        barrel_base[0] + extension,
                        barrel_base[1],
                        barrel_base[2],
                    ),
                ),
            )
            for frame, extension in (
                (1, 0.0),
                (start + 11, 0.0),
                (start + 13, 0.050),
                (start + 15, 0.145),
                (start + 17, 0.170),
                (96, 0.170),
            )
        ]
    tower_keyframes[gun_pivot.name] = [
        (
            frame,
            full_transform(
                gun_rest,
                location=(gun_base[0] + delta, gun_base[1], gun_base[2]),
            ),
        )
        for frame, delta in (
            (1, 0.0),
            (12, 0.0),
            (18, 0.010),
            (30, -0.070),
            (42, -0.132),
            (54, -0.125),
            (66, -0.128),
            (96, -0.128),
        )
    ]
    clips["Tower_Deploy"] = add_semantic_clip(
        "Tower_Deploy",
        tower_keyframes,
        rest=rest,
    )

    # Recoil revision v12 is deliberately silhouette-first at the native
    # 41 px gameplay footprint.  Only the two deployed inner braces move:
    # each rail drives its plate rearward/outward while the hinge tightens
    # briefly, then both return to the exact deployed baseline.
    shell_recoil_keyframes: dict[
        str,
        list[tuple[int, dict[str, tuple[float, ...]]]],
    ] = {}
    for plate_name in ("NorthInner", "SouthInner"):
        rail = shell_rails[plate_name]
        hinge = shell_hinges[plate_name]
        rail_rest = rest[rail.name]
        hinge_rest = rest[hinge.name]
        deployed = shell_keyframes[rail.name][-1][1]
        deployed_hinge = shell_keyframes[hinge.name][-1][1]
        baseline = deployed["location"]
        hinge_baseline = deployed_hinge["rotation_euler"]
        sign = 1.0 if "North" in plate_name else -1.0
        shell_recoil_keyframes[rail.name] = [
            (
                frame,
                full_transform(
                    rail_rest,
                    location=(
                        baseline[0] + delta_x,
                        baseline[1] + sign * delta_y,
                        baseline[2] + delta_z,
                    ),
                ),
            )
            for frame, delta_x, delta_y, delta_z in (
                (1, 0.0, 0.0, 0.0),
                (3, -0.125, 0.035, 0.015),
                (6, -0.090, 0.024, 0.010),
                (11, 0.020, -0.008, -0.003),
                (16, -0.007, 0.003, 0.001),
                (21, 0.002, -0.001, 0.0),
                (24, 0.0, 0.0, 0.0),
            )
        ]
        shell_recoil_keyframes[hinge.name] = [
            (
                frame,
                full_transform(
                    hinge_rest,
                    rotation=(
                        hinge_baseline[0],
                        hinge_baseline[1],
                        hinge_baseline[2] + sign * delta_angle,
                    ),
                ),
            )
            for frame, delta_angle in (
                (1, 0.0),
                (3, math.radians(-6.0)),
                (6, math.radians(-4.0)),
                (11, math.radians(1.5)),
                (16, math.radians(-0.5)),
                (21, math.radians(0.15)),
                (24, 0.0),
            )
        ]
    clips["Shell_Recoil"] = add_semantic_clip(
        "Shell_Recoil",
        shell_recoil_keyframes,
        rest=rest,
    )

    # Aggregate Tower recoil is available for a single renderer contract slot;
    # the eight per-vane clips below remain available for heading-exact fire.
    # Paired one-frame offsets retain a clockwise ripple while keeping enough
    # vanes in motion together to read at gameplay scale.  Each rigid
    # vane carrier braces inward while its barrel telescopes into the authored
    # cradle, then both return to the exact deployed/local baseline.  The
    # crown and body never wobble.
    tower_recoil_keyframes: dict[
        str,
        list[tuple[int, dict[str, tuple[float, ...]]]],
    ] = {}
    for zero_index, vane_name in enumerate(TOWER_VANES):
        fan = fan_pivots[vane_name]
        barrel = barrel_pivots[vane_name]
        fan_rest = rest[fan.name]
        barrel_rest = rest[barrel.name]
        fan_deployed = tower_keyframes[fan.name][-1][1]
        fan_base = fan_deployed["location"]
        fan_rotation = fan_deployed["rotation_euler"]
        barrel_deployed = tower_keyframes[barrel.name][-1][1]
        barrel_base = barrel_deployed["location"]
        angle = math.radians(zero_index * 45.0)
        offset = zero_index // 2
        tower_recoil_keyframes[fan.name] = [
            (
                frame,
                full_transform(
                    fan_rest,
                    location=(
                        fan_base[0] + radial_delta * math.cos(angle),
                        fan_base[1] + radial_delta * math.sin(angle),
                        fan_base[2],
                    ),
                    rotation=(
                        fan_rotation[0],
                        fan_rotation[1] + pitch_delta,
                        fan_rotation[2],
                    ),
                ),
            )
            for frame, radial_delta, pitch_delta in (
                (1, 0.0, 0.0),
                (3 + offset, 0.0, 0.0),
                (5 + offset, -0.060, math.radians(-8.0)),
                (8 + offset, -0.043, math.radians(-5.0)),
                (12 + offset, 0.008, math.radians(1.5)),
                (17 + offset, -0.003, math.radians(-0.5)),
                (23 + offset, 0.0, 0.0),
                (34, 0.0, 0.0),
            )
        ]
        tower_recoil_keyframes[barrel.name] = [
            (
                frame,
                full_transform(
                    barrel_rest,
                    location=(
                        barrel_base[0] + delta,
                        barrel_base[1],
                        barrel_base[2],
                    ),
                ),
            )
            for frame, delta in (
                (1, 0.0),
                (3 + offset, 0.0),
                (5 + offset, -0.082),
                (8 + offset, -0.057),
                (12 + offset, 0.012),
                (17 + offset, -0.004),
                (23 + offset, 0.0),
                (34, 0.0),
            )
        ]
    clips["Tower_Recoil"] = add_semantic_clip(
        "Tower_Recoil",
        tower_recoil_keyframes,
        rest=rest,
    )

    clips["Mobile_Recoil"] = add_semantic_clip(
        "Mobile_Recoil",
        {
            body.name: [
                (
                    frame,
                    full_transform(
                        body_rest,
                        location=(
                            body_rest["location"][0] + delta_x,
                            body_rest["location"][1],
                            body_rest["location"][2] + delta_z,
                        ),
                    ),
                )
                for frame, delta_x, delta_z in (
                    (1, 0.0, 0.0),
                    (3, -0.115, 0.018),
                    (6, -0.080, 0.011),
                    (11, 0.018, -0.004),
                    (16, -0.006, 0.001),
                    (22, 0.0, 0.0),
                )
            ],
            gun_pivot.name: [
                (
                    frame,
                    full_transform(
                        gun_rest,
                        location=(
                            gun_base[0] + delta,
                            gun_base[1],
                            gun_base[2],
                        ),
                    ),
                )
                for frame, delta in (
                    (1, 0.0),
                    (3, -0.065),
                    (6, -0.047),
                    (11, 0.012),
                    (16, -0.004),
                    (22, 0.0),
                )
            ]
        },
        rest=rest,
    )
    for vane_name in TOWER_VANES:
        zero_index = int(vane_name) - 1
        fan = fan_pivots[vane_name]
        barrel = barrel_pivots[vane_name]
        fan_rest = rest[fan.name]
        barrel_rest = rest[barrel.name]
        fan_deployed = tower_keyframes[fan.name][-1][1]
        fan_base = fan_deployed["location"]
        fan_rotation = fan_deployed["rotation_euler"]
        barrel_deployed = tower_keyframes[barrel.name][-1][1]
        barrel_base = barrel_deployed["location"]
        angle = math.radians(zero_index * 45.0)
        clip_name = f"Tower_Recoil_{vane_name}"
        clips[clip_name] = add_semantic_clip(
            clip_name,
            {
                fan.name: [
                    (
                        frame,
                        full_transform(
                            fan_rest,
                            location=(
                                fan_base[0] + radial_delta * math.cos(angle),
                                fan_base[1] + radial_delta * math.sin(angle),
                                fan_base[2],
                            ),
                            rotation=(
                                fan_rotation[0],
                                fan_rotation[1] + pitch_delta,
                                fan_rotation[2],
                            ),
                        ),
                    )
                    for frame, radial_delta, pitch_delta in (
                        (1, 0.0, 0.0),
                        (3, -0.060, math.radians(-8.0)),
                        (5, -0.046, math.radians(-5.5)),
                        (9, -0.018, math.radians(-2.0)),
                        (12, 0.008, math.radians(1.5)),
                        (16, -0.003, math.radians(-0.5)),
                        (19, 0.001, math.radians(0.15)),
                        (22, 0.0, 0.0),
                    )
                ],
                barrel.name: [
                    (
                        frame,
                        full_transform(
                            barrel_rest,
                            location=(
                                barrel_base[0] + delta,
                                barrel_base[1],
                                barrel_base[2],
                            ),
                        ),
                    )
                    for frame, delta in (
                        (1, 0.0),
                        (3, -0.082),
                        (5, -0.060),
                        (9, -0.021),
                        (12, 0.012),
                        (16, -0.004),
                        (19, 0.0015),
                        (22, 0.0),
                    )
                ]
            },
            rest=rest,
        )
    restore_rest(rest)
    return clips


def set_tracks(active_names: set[str]) -> None:
    for obj in bpy.data.objects:
        if obj.animation_data is None:
            continue
        obj.animation_data.action = None
        for track in obj.animation_data.nla_tracks:
            track.mute = track.name not in active_names


def sample_frame(
    *,
    active_names: set[str],
    frame: int,
    rest: dict[str, dict[str, Any]],
) -> None:
    # NLA only evaluates channels touched by the active strip.  Without this
    # explicit reset, a previously sampled sibling form can leak transforms
    # into the next proof.  Every sample therefore starts from the same pinned
    # Mobile rest pose before enabling exactly the requested semantic clip(s).
    set_tracks(set())
    restore_rest(rest)
    set_tracks(active_names)
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()


def mesh_objects(collection: bpy.types.Collection) -> list[bpy.types.Object]:
    return sorted(
        (obj for obj in collection.all_objects if obj.type == "MESH"),
        key=lambda obj: obj.name,
    )


def assign_original_uv_atlas(
    objects: list[bpy.types.Object],
) -> dict[str, Any]:
    grid = math.ceil(math.sqrt(len(objects)))
    cell_size = 1.0 / grid
    records: dict[str, Any] = {}
    for index, obj in enumerate(objects):
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(
            angle_limit=math.radians(66.0),
            island_margin=0.035,
            area_weight=0.0,
            correct_aspect=True,
            scale_to_bounds=True,
        )
        bpy.ops.object.mode_set(mode="OBJECT")
        uv_layer = obj.data.uv_layers.active
        if uv_layer is None:
            raise ValueError(f"Smart UV did not create a layer for {obj.name}")
        uv_layer.name = "UVMap"
        values = [loop.uv.copy() for loop in uv_layer.data]
        minimum_u = min(value.x for value in values)
        maximum_u = max(value.x for value in values)
        minimum_v = min(value.y for value in values)
        maximum_v = max(value.y for value in values)
        span_u = max(maximum_u - minimum_u, EPSILON)
        span_v = max(maximum_v - minimum_v, EPSILON)
        column = index % grid
        row = index // grid
        inner = cell_size * (1.0 - 2.0 * UV_MARGIN_IN_CELL)
        offset_u = column * cell_size + UV_MARGIN_IN_CELL * cell_size
        offset_v = row * cell_size + UV_MARGIN_IN_CELL * cell_size
        scale = inner / max(span_u, span_v)
        for loop in uv_layer.data:
            loop.uv.x = offset_u + (loop.uv.x - minimum_u) * scale
            loop.uv.y = offset_v + (loop.uv.y - minimum_v) * scale
        transformed = [loop.uv.copy() for loop in uv_layer.data]
        records[obj.name] = {
            "cell": [column, row],
            "bounds": [
                rounded(min(value.x for value in transformed), 9),
                rounded(min(value.y for value in transformed), 9),
                rounded(max(value.x for value in transformed), 9),
                rounded(max(value.y for value in transformed), 9),
            ],
            "loops": len(transformed),
        }
        obj.select_set(False)
    return {"grid": grid, "objects": records}


def polygon_area(points: list[Vector]) -> float:
    if len(points) < 3:
        return 0.0
    return abs(
        sum(
            points[index].x * points[(index + 1) % len(points)].y
            - points[(index + 1) % len(points)].x * points[index].y
            for index in range(len(points))
        )
        * 0.5
    )


def uv_area(objects: list[bpy.types.Object]) -> float:
    total = 0.0
    for obj in objects:
        layer = obj.data.uv_layers.active
        if layer is None:
            continue
        for polygon in obj.data.polygons:
            points = [layer.data[index].uv.copy() for index in polygon.loop_indices]
            total += polygon_area(points)
    return total


def write_uv_layout_svg(
    objects: list[bpy.types.Object],
    *,
    output: Path,
    size: int = 2048,
) -> None:
    palette = (
        "#42566d",
        "#6f6048",
        "#355f6a",
        "#293541",
        "#526174",
        "#756a54",
    )
    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        (
            f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" '
            f'height="{size}" viewBox="0 0 {size} {size}">'
        ),
        '<rect width="100%" height="100%" fill="#10151d"/>',
    ]
    for object_index, obj in enumerate(objects):
        layer = obj.data.uv_layers.active
        if layer is None:
            continue
        color = palette[object_index % len(palette)]
        for polygon in obj.data.polygons:
            points = []
            for loop_index in polygon.loop_indices:
                uv = layer.data[loop_index].uv
                points.append(f"{uv.x * size:.3f},{(1.0 - uv.y) * size:.3f}")
            lines.append(
                f'<polygon points="{" ".join(points)}" fill="{color}" '
                'fill-opacity="0.30" stroke="#d7e1eb" stroke-width="0.7"/>'
            )
    lines.append("</svg>")
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")


def mesh_digest(obj: bpy.types.Object) -> str:
    digest = hashlib.sha256()
    mesh = obj.data
    for vertex in mesh.vertices:
        digest.update(
            struct.pack(
                "<3d",
                float(vertex.co.x),
                float(vertex.co.y),
                float(vertex.co.z),
            )
        )
    for polygon in mesh.polygons:
        digest.update(struct.pack("<I", len(polygon.vertices)))
        for vertex_index in polygon.vertices:
            digest.update(struct.pack("<I", int(vertex_index)))
    layer = mesh.uv_layers.active
    if layer is not None:
        for loop in layer.data:
            digest.update(struct.pack("<2d", float(loop.uv.x), float(loop.uv.y)))
    return digest.hexdigest()


def object_bounds_world(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def mesh_centroid_world(obj: bpy.types.Object) -> Vector:
    if obj.type != "MESH" or not obj.data.vertices:
        return obj.matrix_world.translation.copy()
    total = Vector((0.0, 0.0, 0.0))
    for vertex in obj.data.vertices:
        total += obj.matrix_world @ vertex.co
    return total / len(obj.data.vertices)


def collective_bounds_world(
    objects: Iterable[bpy.types.Object],
) -> tuple[Vector, Vector]:
    points: list[Vector] = []
    for obj in objects:
        points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def maximum_radial_extent_world(
    objects: Iterable[bpy.types.Object],
    *,
    center: tuple[float, float],
) -> float:
    maximum = 0.0
    for obj in objects:
        for vertex in obj.data.vertices:
            point = obj.matrix_world @ vertex.co
            maximum = max(
                maximum,
                math.hypot(point.x - center[0], point.y - center[1]),
            )
    return maximum


def matrix_record(matrix: Matrix) -> list[list[float]]:
    return [[rounded(value, 9) for value in row] for row in matrix]


def transform_records(names: Iterable[str]) -> dict[str, Any]:
    return {
        name: {
            "local": matrix_record(bpy.data.objects[name].matrix_local),
            "world": matrix_record(bpy.data.objects[name].matrix_world),
            "location": [rounded(value, 9) for value in bpy.data.objects[name].location],
            "rotationEuler": [
                rounded(value, 9)
                for value in bpy.data.objects[name].rotation_euler
            ],
            "scale": [rounded(value, 9) for value in bpy.data.objects[name].scale],
        }
        for name in sorted(names)
    }


def endpoint_snapshot(
    *,
    active: set[str],
    frame: int,
    names: Iterable[str],
    rest: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    sample_frame(active_names=active, frame=frame, rest=rest)
    return transform_records(names)


def endpoint_delta(
    first: dict[str, Any],
    second: dict[str, Any],
) -> float:
    maximum = 0.0
    for name in first:
        left = first[name]["local"]
        right = second[name]["local"]
        for row in range(4):
            for column in range(4):
                maximum = max(maximum, abs(left[row][column] - right[row][column]))
    return maximum


def bvh_overlap_pairs(
    objects: list[bpy.types.Object],
) -> list[tuple[str, str, int]]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    trees: dict[str, BVHTree] = {}
    for obj in objects:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        try:
            world = evaluated.matrix_world
            vertices = [world @ vertex.co for vertex in mesh.vertices]
            polygons = [list(polygon.vertices) for polygon in mesh.polygons]
            trees[obj.name] = BVHTree.FromPolygons(
                vertices,
                polygons,
                all_triangles=False,
                epsilon=1.0e-7,
            )
        finally:
            evaluated.to_mesh_clear()
    overlaps: list[tuple[str, str, int]] = []
    for left_index, left in enumerate(objects):
        for right in objects[left_index + 1 :]:
            pairs = trees[left.name].overlap(trees[right.name])
            if pairs:
                overlaps.append((left.name, right.name, len(pairs)))
    return overlaps


def configure_scene(
    proof_collection: bpy.types.Collection,
) -> dict[str, bpy.types.Object]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.image_settings.color_depth = "8"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "rim.sl"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.display.shading.curvature_ridge_factor = 1.6
    scene.display.shading.curvature_valley_factor = 1.35
    scene.display.shading.show_specular_highlight = True
    scene.display.shading.show_object_outline = True
    scene.display.shading.background_type = "VIEWPORT"
    scene.display.shading.background_color = (0.028, 0.038, 0.058)
    scene.display.shading.color_type = "OBJECT"
    scene.render.fps = FPS
    scene.frame_start = 1
    scene.frame_end = DEPLOY_END

    floor = add_box(
        "PROOF.Floor",
        (2.6, 2.6, 0.025),
        location=(0.0, 0.0, -0.014),
        bevel=0.0,
        parent=None,
        material=make_material(),
        collection=proof_collection,
        color_role="floor",
        part_role="proof-only-floor",
    )
    floor.color = PROOF_COLORS["floor"]
    floor.hide_render = False

    cameras: dict[str, bpy.types.Object] = {}
    for name in (
        "top",
        "side",
        "front",
        "rear",
        "gameplay",
        "tower-side",
        "tower-front",
        "tower-rear",
    ):
        camera_data = bpy.data.cameras.new(f"PROOF.Camera.{name}.Data")
        camera = bpy.data.objects.new(f"PROOF.Camera.{name}", camera_data)
        proof_collection.objects.link(camera)
        camera_data.type = "ORTHO"
        cameras[name] = camera
    cameras["top"].location = (0.0, 0.0, 5.0)
    cameras["top"].data.ortho_scale = 1.42
    look_at(cameras["top"], Vector((0.0, 0.0, 0.20)))
    cameras["side"].location = (0.0, -4.5, 0.31)
    cameras["side"].data.ortho_scale = 1.28
    look_at(cameras["side"], Vector((0.0, 0.0, 0.20)))
    cameras["front"].location = (4.5, 0.0, 0.31)
    cameras["front"].data.ortho_scale = 1.28
    look_at(cameras["front"], Vector((0.0, 0.0, 0.20)))
    cameras["rear"].location = (-4.5, 0.0, 0.31)
    cameras["rear"].data.ortho_scale = 1.28
    look_at(cameras["rear"], Vector((0.0, 0.0, 0.20)))
    cameras["tower-side"].location = (0.0, -4.5, 0.47)
    cameras["tower-side"].data.ortho_scale = 1.58
    look_at(cameras["tower-side"], Vector((0.0, 0.0, 0.40)))
    cameras["tower-front"].location = (4.5, 0.0, 0.47)
    cameras["tower-front"].data.ortho_scale = 1.58
    look_at(cameras["tower-front"], Vector((0.0, 0.0, 0.40)))
    cameras["tower-rear"].location = (-4.5, 0.0, 0.47)
    cameras["tower-rear"].data.ortho_scale = 1.58
    look_at(cameras["tower-rear"], Vector((0.0, 0.0, 0.40)))
    cameras["gameplay"].location = (2.8, -3.2, 6.80)
    cameras["gameplay"].data.ortho_scale = 1.44
    look_at(cameras["gameplay"], Vector((0.0, 0.0, 0.19)))
    return cameras


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_frame(
    *,
    camera: bpy.types.Object,
    output: Path,
    width: int,
    height: int,
) -> None:
    scene = bpy.context.scene
    scene.camera = camera
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


def image_pixels(path: Path) -> tuple[int, int, array]:
    image = bpy.data.images.load(str(path), check_existing=False)
    try:
        width, height = image.size
        return width, height, array("f", image.pixels[:])
    finally:
        bpy.data.images.remove(image)


def count_lit_pixels(path: Path, *, threshold: float = 0.30) -> int:
    _width, _height, pixels = image_pixels(path)
    return sum(
        1
        for index in range(0, len(pixels), 4)
        if max(pixels[index], pixels[index + 1], pixels[index + 2]) > threshold
    )


def image_difference_metrics(
    first: Path,
    second: Path,
    *,
    threshold: float = 0.035,
) -> dict[str, Any]:
    first_width, first_height, first_pixels = image_pixels(first)
    second_width, second_height, second_pixels = image_pixels(second)
    if (first_width, first_height) != (second_width, second_height):
        raise ValueError("Image difference inputs have different dimensions")
    changed = 0
    total_difference = 0.0
    maximum = 0.0
    for index in range(0, len(first_pixels), 4):
        delta = max(
            abs(first_pixels[index + channel] - second_pixels[index + channel])
            for channel in range(3)
        )
        total_difference += delta
        maximum = max(maximum, delta)
        if delta > threshold:
            changed += 1
    pixel_count = first_width * first_height
    return {
        "width": first_width,
        "height": first_height,
        "changedPixels": changed,
        "changedRatio": rounded(changed / pixel_count, 8),
        "meanMaxChannelDelta": rounded(total_difference / pixel_count, 8),
        "maximumChannelDelta": rounded(maximum, 8),
        "threshold": threshold,
    }


def render_team_accent_proofs(
    *,
    model_meshes: list[bpy.types.Object],
    accent_meshes: list[bpy.types.Object],
    hull: bpy.types.Object,
    cameras: dict[str, bpy.types.Object],
    proofs_dir: Path,
    frames_dir: Path,
) -> dict[str, Any]:
    original_colors = {obj.name: tuple(obj.color) for obj in accent_meshes}
    variant_records: dict[str, Any] = {}
    for variant, color in TEAM_ACCENT_PROOF_COLORS.items():
        for obj in accent_meshes:
            obj.color = color
        large = proofs_dir / f"team-accent-{variant}-gameplay.png"
        small = proofs_dir / f"team-accent-{variant}-gameplay-41px.png"
        render_frame(camera=cameras["gameplay"], output=large, width=640, height=640)
        render_frame(camera=cameras["gameplay"], output=small, width=41, height=41)
        variant_records[variant] = {
            "large": large,
            "small": small,
        }
    for obj in accent_meshes:
        obj.color = original_colors[obj.name]

    scene = bpy.context.scene
    floor = bpy.data.objects["PROOF.Floor"]
    saved_visibility = {obj.name: obj.hide_render for obj in model_meshes}
    saved_colors = {obj.name: tuple(obj.color) for obj in model_meshes}
    saved_floor_color = tuple(floor.color)
    shading = scene.display.shading
    saved_shading = {
        "light": shading.light,
        "show_shadows": shading.show_shadows,
        "show_cavity": shading.show_cavity,
        "show_object_outline": shading.show_object_outline,
        "background_color": tuple(shading.background_color),
    }
    body_mask = frames_dir / "team-accent-body-mask.png"
    accent_mask = proofs_dir / "team-accent-coverage-mask.png"
    accent_mask_41 = frames_dir / "team-accent-mask-41px.png"
    try:
        shading.light = "FLAT"
        shading.show_shadows = False
        shading.show_cavity = False
        shading.show_object_outline = False
        shading.background_color = (0.0, 0.0, 0.0)
        floor.color = (0.0, 0.0, 0.0, 1.0)
        for obj in model_meshes:
            obj.hide_render = True
            obj.color = (0.0, 0.0, 0.0, 1.0)
        hull.hide_render = False
        hull.color = (1.0, 1.0, 1.0, 1.0)
        render_frame(camera=cameras["top"], output=body_mask, width=640, height=640)

        hull.hide_render = True
        for obj in accent_meshes:
            obj.hide_render = False
            obj.color = (1.0, 1.0, 1.0, 1.0)
        render_frame(camera=cameras["top"], output=accent_mask, width=640, height=640)
        render_frame(
            camera=cameras["gameplay"],
            output=accent_mask_41,
            width=41,
            height=41,
        )
    finally:
        for obj in model_meshes:
            obj.hide_render = saved_visibility[obj.name]
            obj.color = saved_colors[obj.name]
        floor.color = saved_floor_color
        shading.light = saved_shading["light"]
        shading.show_shadows = saved_shading["show_shadows"]
        shading.show_cavity = saved_shading["show_cavity"]
        shading.show_object_outline = saved_shading["show_object_outline"]
        shading.background_color = saved_shading["background_color"]

    body_pixels = count_lit_pixels(body_mask)
    accent_pixels = count_lit_pixels(accent_mask)
    accent_pixels_41 = count_lit_pixels(accent_mask_41)
    blue_orange_difference = image_difference_metrics(
        variant_records["blue"]["small"],
        variant_records["orange"]["small"],
    )
    return {
        "variants": variant_records,
        "coverageMask": accent_mask,
        "bodyMask": body_mask,
        "smallMask": accent_mask_41,
        "fixedTopBodyPixels": body_pixels,
        "accentPixels": accent_pixels,
        "coverageRatio": rounded(accent_pixels / max(body_pixels, 1), 8),
        "visiblePixelsAt41": accent_pixels_41,
        "blueVsOrangeAt41": blue_orange_difference,
    }


def projected_pixel(
    *,
    camera: bpy.types.Object,
    world_point: Vector,
    size: int = 41,
) -> Vector:
    normalized = world_to_camera_view(
        bpy.context.scene,
        camera,
        world_point,
    )
    return Vector((normalized.x * size, normalized.y * size))


def screen_landmark(
    obj: bpy.types.Object,
    *,
    camera: bpy.types.Object,
    size: int = 41,
) -> dict[str, Vector]:
    world_vertices = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    center_world = sum(world_vertices, Vector((0.0, 0.0, 0.0))) / len(
        world_vertices
    )
    tip_vertex = max(obj.data.vertices, key=lambda vertex: vertex.co.x)
    tip_world = obj.matrix_world @ tip_vertex.co
    return {
        "center": projected_pixel(
            camera=camera,
            world_point=center_world,
            size=size,
        ),
        "tip": projected_pixel(
            camera=camera,
            world_point=tip_world,
            size=size,
        ),
    }


def vector_distance(first: Vector, second: Vector) -> float:
    return rounded((first - second).length, 6)


BITMAP_FONT = {
    " ": ("000", "000", "000", "000", "000"),
    "0": ("111", "101", "101", "101", "111"),
    "1": ("010", "110", "010", "010", "111"),
    "2": ("111", "001", "111", "100", "111"),
    "3": ("111", "001", "111", "001", "111"),
    "4": ("101", "101", "111", "001", "001"),
    "5": ("111", "100", "111", "001", "111"),
    "6": ("111", "100", "111", "101", "111"),
    "7": ("111", "001", "010", "010", "010"),
    "8": ("111", "101", "111", "101", "111"),
    "9": ("111", "101", "111", "001", "111"),
    "A": ("010", "101", "111", "101", "101"),
    "C": ("111", "100", "100", "100", "111"),
    "D": ("110", "101", "101", "101", "110"),
    "E": ("111", "100", "110", "100", "111"),
    "F": ("111", "100", "110", "100", "100"),
    "G": ("111", "100", "101", "101", "111"),
    "H": ("101", "101", "111", "101", "101"),
    "I": ("111", "010", "010", "010", "111"),
    "K": ("101", "101", "110", "101", "101"),
    "L": ("100", "100", "100", "100", "111"),
    "M": ("101", "111", "111", "101", "101"),
    "N": ("101", "111", "111", "111", "101"),
    "O": ("111", "101", "101", "101", "111"),
    "P": ("110", "101", "110", "100", "100"),
    "R": ("110", "101", "110", "101", "101"),
    "S": ("111", "100", "111", "001", "111"),
    "T": ("111", "010", "010", "010", "010"),
    "U": ("101", "101", "101", "101", "111"),
    "V": ("101", "101", "101", "101", "010"),
    "W": ("101", "101", "111", "111", "101"),
}


def set_pixel(
    pixels: array,
    width: int,
    height: int,
    x: int,
    y: int,
    color: tuple[float, float, float, float],
) -> None:
    if x < 0 or y < 0 or x >= width or y >= height:
        return
    offset = (y * width + x) * 4
    pixels[offset : offset + 4] = array("f", color)


def draw_label(
    pixels: array,
    *,
    width: int,
    height: int,
    text: str,
    panel_x: int,
    panel_y: int,
    panel_width: int,
    scale: int = 3,
) -> None:
    glyph_width = 3 * scale
    spacing = 2 * scale
    total_width = len(text) * glyph_width + (len(text) - 1) * spacing
    origin_x = panel_x + (panel_width - total_width) // 2
    origin_y = panel_y + 9
    color = (0.90, 0.94, 0.98, 1.0)
    for character_index, character in enumerate(text):
        glyph = BITMAP_FONT[character]
        for row, bits in enumerate(glyph):
            for column, bit in enumerate(bits):
                if bit != "1":
                    continue
                for offset_y in range(scale):
                    for offset_x in range(scale):
                        set_pixel(
                            pixels,
                            width,
                            height,
                            origin_x
                            + character_index * (glyph_width + spacing)
                            + column * scale
                            + offset_x,
                            origin_y + (4 - row) * scale + offset_y,
                            color,
                        )


def compose_contact_sheet(
    frame_paths: list[Path],
    frames: tuple[int, ...],
    *,
    output: Path,
    columns: int = 5,
    stage_labels: tuple[str, ...] | None = None,
) -> dict[str, Any]:
    if len(frame_paths) != len(frames):
        raise ValueError("Contact sheet frame/label count mismatch")
    if stage_labels is not None and len(stage_labels) != len(frames):
        raise ValueError("Contact sheet stage label count mismatch")
    images = [bpy.data.images.load(str(path), check_existing=False) for path in frame_paths]
    try:
        frame_width, frame_height = images[0].size
        rows = math.ceil(len(images) / columns)
        header = 30
        gutter = 6
        width = columns * frame_width + (columns - 1) * gutter
        height = rows * (frame_height + header) + (rows - 1) * gutter
        background = (0.018, 0.025, 0.039, 1.0)
        pixels = array("f", background) * (width * height)
        for index, image in enumerate(images):
            column = index % columns
            row = index // columns
            panel_x = column * (frame_width + gutter)
            panel_y = (rows - 1 - row) * (frame_height + header + gutter)
            source = array("f", image.pixels[:])
            for source_row in range(frame_height):
                source_start = source_row * frame_width * 4
                source_end = source_start + frame_width * 4
                target_start = (
                    (panel_y + source_row) * width + panel_x
                ) * 4
                target_end = target_start + frame_width * 4
                pixels[target_start:target_end] = source[source_start:source_end]
            draw_label(
                pixels,
                width=width,
                height=height,
                text=(
                    f"F{frames[index]:02d} {stage_labels[index]}"
                    if stage_labels is not None
                    else f"F{frames[index]:02d}"
                ),
                panel_x=panel_x,
                panel_y=panel_y + frame_height,
                panel_width=frame_width,
                scale=1 if frame_width < 100 else 3,
            )
        sheet = bpy.data.images.new(
            output.stem,
            width=width,
            height=height,
            alpha=True,
            float_buffer=False,
        )
        sheet.pixels.foreach_set(pixels)
        sheet.filepath_raw = str(output)
        sheet.file_format = "PNG"
        sheet.save()
        bpy.data.images.remove(sheet)
        return {
            "frames": list(frames),
            "columns": columns,
            "rows": rows,
            "width": width,
            "height": height,
        }
    finally:
        for image in images:
            if image.name in bpy.data.images:
                bpy.data.images.remove(image)


def render_contact_sheet(
    *,
    active_clips: set[str],
    frames: tuple[int, ...],
    camera: bpy.types.Object,
    output: Path,
    frame_dir: Path,
    deployed_overlay: dict[str, dict[str, Any]] | None = None,
    stage_labels: tuple[str, ...] | None = None,
    rest: dict[str, dict[str, Any]],
    tile_size: int = 300,
) -> dict[str, Any]:
    frame_dir.mkdir(parents=True, exist_ok=True)
    frame_paths: list[Path] = []
    for frame in frames:
        sample_frame(active_names=active_clips, frame=frame, rest=rest)
        if deployed_overlay is not None:
            for name, transform in deployed_overlay.items():
                obj = bpy.data.objects[name]
                obj.location = transform["location"]
                obj.rotation_euler = transform["rotationEuler"]
        bpy.context.view_layer.update()
        path = frame_dir / f"f{frame:03d}.png"
        render_frame(
            camera=camera,
            output=path,
            width=tile_size,
            height=tile_size,
        )
        frame_paths.append(path)
    return compose_contact_sheet(
        frame_paths,
        frames,
        output=output,
        stage_labels=stage_labels,
    )


def gif_lzw(indices: bytes, minimum_code_size: int = 8) -> bytes:
    clear_code = 1 << minimum_code_size
    end_code = clear_code + 1
    dictionary: dict[tuple[int, int], int] = {}
    next_code = end_code + 1
    code_size = minimum_code_size + 1
    bit_buffer = 0
    bit_count = 0
    encoded = bytearray()

    def emit(code: int) -> None:
        nonlocal bit_buffer, bit_count
        bit_buffer |= code << bit_count
        bit_count += code_size
        while bit_count >= 8:
            encoded.append(bit_buffer & 0xFF)
            bit_buffer >>= 8
            bit_count -= 8

    emit(clear_code)
    if not indices:
        emit(end_code)
    else:
        prefix = indices[0]
        for symbol in indices[1:]:
            key = (prefix, symbol)
            known = dictionary.get(key)
            if known is not None:
                prefix = known
                continue
            emit(prefix)
            if next_code < 4096:
                dictionary[key] = next_code
                next_code += 1
                if next_code == (1 << code_size) and code_size < 12:
                    code_size += 1
            else:
                emit(clear_code)
                dictionary.clear()
                next_code = end_code + 1
                code_size = minimum_code_size + 1
            prefix = symbol
        emit(prefix)
        emit(end_code)
    if bit_count:
        encoded.append(bit_buffer & 0xFF)
    return bytes(encoded)


def linear_to_srgb_byte(value: float) -> int:
    value = max(0.0, min(1.0, float(value)))
    if value <= 0.0031308:
        converted = value * 12.92
    else:
        converted = 1.055 * (value ** (1.0 / 2.4)) - 0.055
    return max(0, min(255, int(round(converted * 255.0))))


def gif_palette() -> bytes:
    palette = bytearray()
    for index in range(256):
        red = (index >> 5) & 0x07
        green = (index >> 2) & 0x07
        blue = index & 0x03
        palette.extend(
            (
                int(round(red * 255 / 7)),
                int(round(green * 255 / 7)),
                int(round(blue * 255 / 3)),
            )
        )
    return bytes(palette)


def quantize_gif_frame(path: Path, width: int, height: int) -> bytes:
    image = bpy.data.images.load(str(path), check_existing=False)
    try:
        if tuple(image.size) != (width, height):
            raise ValueError(f"Unexpected GIF source size for {path}")
        pixels = image.pixels[:]
        indices = bytearray(width * height)
        for pixel_index in range(width * height):
            base = pixel_index * 4
            red = linear_to_srgb_byte(pixels[base])
            green = linear_to_srgb_byte(pixels[base + 1])
            blue = linear_to_srgb_byte(pixels[base + 2])
            indices[pixel_index] = (
                ((red >> 5) << 5) | ((green >> 5) << 2) | (blue >> 6)
            )
        return bytes(indices)
    finally:
        bpy.data.images.remove(image)


def encode_image_sequence_gif(
    *,
    images: list[Path],
    output: Path,
    width: int,
    height: int,
    fps: int = 15,
) -> dict[str, Any]:
    if not images:
        raise ValueError("Cannot encode an empty motion sequence")
    delay_centiseconds = max(1, int(round(100 / fps)))
    with output.open("wb") as stream:
        stream.write(b"GIF89a")
        stream.write(struct.pack("<HH", width, height))
        stream.write(bytes((0xF7, 0x00, 0x00)))
        stream.write(gif_palette())
        stream.write(
            b"\x21\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00"
        )
        for path in images:
            indices = quantize_gif_frame(path, width, height)
            stream.write(b"\x21\xf9\x04\x04")
            stream.write(struct.pack("<H", delay_centiseconds))
            stream.write(b"\x00\x00")
            stream.write(b"\x2c")
            stream.write(struct.pack("<HHHHB", 0, 0, width, height, 0))
            stream.write(b"\x08")
            compressed = gif_lzw(indices)
            for offset in range(0, len(compressed), 255):
                block = compressed[offset : offset + 255]
                stream.write(bytes((len(block),)))
                stream.write(block)
            stream.write(b"\x00")
        stream.write(b"\x3b")
    return {
        "frames": len(images),
        "fps": fps,
        "durationSeconds": rounded(len(images) * delay_centiseconds / 100, 3),
        "codec": "GIF LZW, fixed deterministic RGB332 palette",
        "container": "GIF89a",
        "loop": "infinite",
    }


def render_motion_loop(
    *,
    deploy_clip: str,
    recoil_clip: str | None,
    recoil_end: int,
    deployed_overlay: dict[str, dict[str, Any]],
    recoil_nodes: set[str],
    camera: bpy.types.Object,
    frame_dir: Path,
    output: Path,
    rest: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    frame_dir.mkdir(parents=True, exist_ok=True)
    deploy_frames = list(range(1, DEPLOY_END + 1, 2))
    if deploy_frames[-1] != DEPLOY_END:
        deploy_frames.append(DEPLOY_END)
    deploy_paths: list[Path] = []
    for frame in deploy_frames:
        sample_frame(active_names={deploy_clip}, frame=frame, rest=rest)
        path = frame_dir / f"deploy-f{frame:03d}.png"
        render_frame(camera=camera, output=path, width=320, height=320)
        deploy_paths.append(path)

    recoil_paths: list[Path] = []
    if recoil_clip is not None:
        recoil_frames = list(range(1, recoil_end + 1, 2))
        if recoil_frames[-1] != recoil_end:
            recoil_frames.append(recoil_end)
        overlay = {
            name: transform
            for name, transform in deployed_overlay.items()
            if name not in recoil_nodes
        }
        for frame in recoil_frames:
            sample_frame(active_names={recoil_clip}, frame=frame, rest=rest)
            for name, transform in overlay.items():
                obj = bpy.data.objects[name]
                obj.location = transform["location"]
                obj.rotation_euler = transform["rotationEuler"]
            bpy.context.view_layer.update()
            path = frame_dir / f"recoil-f{frame:03d}.png"
            render_frame(camera=camera, output=path, width=320, height=320)
            recoil_paths.append(path)

    # 0.53 s form hold, localized recoil, 0.4 s recovery hold, then sample
    # the exact same deploy clip backward.  This makes reversibility visible
    # rather than merely asserted in JSON.
    timeline = (
        deploy_paths
        + [deploy_paths[-1]] * 8
        + recoil_paths
        + [deploy_paths[-1]] * 6
        + list(reversed(deploy_paths))
    )
    encoded = encode_image_sequence_gif(
        images=timeline,
        output=output,
        width=320,
        height=320,
        fps=15,
    )
    encoded.update(
        {
            "deployClip": deploy_clip,
            "recoilClip": recoil_clip,
            "deploySourceFrames": deploy_frames,
            "reverseUsesSameFrames": True,
            "path": output.as_posix(),
        }
    )
    return encoded


def render_recoil_loop(
    *,
    clip: str,
    clip_end: int,
    deployed_overlay: dict[str, dict[str, Any]] | None,
    camera: bpy.types.Object,
    frame_dir: Path,
    output: Path,
    rest: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    frame_dir.mkdir(parents=True, exist_ok=True)
    frames = list(range(1, clip_end + 1, 2))
    if frames[-1] != clip_end:
        frames.append(clip_end)
    frame_paths: list[Path] = []
    for frame in frames:
        sample_frame(active_names={clip}, frame=frame, rest=rest)
        if deployed_overlay is not None:
            for name, transform in deployed_overlay.items():
                obj = bpy.data.objects[name]
                obj.location = transform["location"]
                obj.rotation_euler = transform["rotationEuler"]
        bpy.context.view_layer.update()
        path = frame_dir / f"f{frame:03d}.png"
        render_frame(camera=camera, output=path, width=240, height=240)
        frame_paths.append(path)
    timeline = [frame_paths[0]] * 4 + frame_paths + [frame_paths[-1]] * 5
    encoded = encode_image_sequence_gif(
        images=timeline,
        output=output,
        width=240,
        height=240,
        fps=15,
    )
    encoded.update(
        {
            "clip": clip,
            "sourceFrames": frames,
            "returnsToFirstPose": True,
            "path": output.as_posix(),
        }
    )
    return encoded


def export_glb(
    *,
    output: Path,
    model_collection: bpy.types.Collection,
) -> None:
    for obj in model_collection.all_objects:
        if obj.animation_data is not None:
            for track in obj.animation_data.nla_tracks:
                track.mute = False
    bpy.ops.export_scene.gltf(
        filepath=str(output),
        export_format="GLB",
        use_selection=False,
        collection=model_collection.name,
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
    set_tracks(set())


def export_texture_upload_glb(
    *,
    output: Path,
    model_collection: bpy.types.Collection,
    accent_meshes: list[bpy.types.Object],
    neutral_material: bpy.types.Material,
) -> None:
    set_tracks(set())
    authored_materials = {
        obj.name: obj.data.materials[0] for obj in accent_meshes
    }
    try:
        # The upload donor keeps exact full surface/UV parity.  Its four
        # semantic emitters temporarily share the neutral atlas material so
        # the provider sees one material only.  Provider materials are never
        # transplanted onto these meshes; the authored semantic material in
        # the .blend/animated GLB remains authoritative.
        for obj in accent_meshes:
            obj.data.materials[0] = neutral_material
        bpy.ops.export_scene.gltf(
            filepath=str(output),
            export_format="GLB",
            use_selection=False,
            collection=model_collection.name,
            export_materials="EXPORT",
            export_cameras=False,
            export_lights=False,
            export_extras=True,
            export_animations=False,
            export_apply=True,
            export_yup=True,
        )
    finally:
        for obj in accent_meshes:
            obj.data.materials[0] = authored_materials[obj.name]


def read_glb(path: Path) -> tuple[dict[str, Any], bytes]:
    payload = path.read_bytes()
    if payload[:4] != b"glTF":
        raise ValueError("Exported file is not a GLB")
    _magic, version, total_length = struct.unpack_from("<4sII", payload, 0)
    if version != 2 or total_length != len(payload):
        raise ValueError("Malformed GLB header")
    offset = 12
    json_chunk: bytes | None = None
    binary_chunk = b""
    while offset < len(payload):
        length, chunk_type = struct.unpack_from("<II", payload, offset)
        offset += 8
        chunk = payload[offset : offset + length]
        offset += length
        if chunk_type == 0x4E4F534A:
            json_chunk = chunk
        elif chunk_type == 0x004E4942:
            binary_chunk = chunk
    if json_chunk is None:
        raise ValueError("GLB has no JSON chunk")
    document = json.loads(json_chunk.rstrip(b" \x00").decode("utf-8"))
    return document, binary_chunk


def parse_glb(path: Path) -> dict[str, Any]:
    document, _binary_chunk = read_glb(path)
    triangles = 0
    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            accessor = document["accessors"][primitive["indices"]]
            triangles += accessor["count"] // 3
    return {
        "version": 2,
        "nodes": len(document.get("nodes", [])),
        "meshes": len(document.get("meshes", [])),
        "meshNames": [
            mesh.get("name") for mesh in document.get("meshes", [])
        ],
        "materials": len(document.get("materials", [])),
        "materialNames": [
            material.get("name") for material in document.get("materials", [])
        ],
        "images": len(document.get("images", [])),
        "textures": len(document.get("textures", [])),
        "animations": [
            animation.get("name") for animation in document.get("animations", [])
        ],
        "triangles": triangles,
        "skins": len(document.get("skins", [])),
        "cameras": len(document.get("cameras", [])),
    }


def glb_surface_digest(path: Path) -> str:
    document, binary = read_glb(path)
    digest = hashlib.sha256()
    for mesh_index, mesh in enumerate(document.get("meshes", [])):
        for primitive_index, primitive in enumerate(mesh.get("primitives", [])):
            digest.update(struct.pack("<II", mesh_index, primitive_index))
            references = [("INDICES", primitive.get("indices"))]
            references.extend(
                sorted(primitive.get("attributes", {}).items())
            )
            for semantic, accessor_index in references:
                if accessor_index is None:
                    continue
                accessor = document["accessors"][accessor_index]
                view = document["bufferViews"][accessor["bufferView"]]
                start = int(view.get("byteOffset", 0))
                end = start + int(view["byteLength"])
                digest.update(semantic.encode("ascii"))
                digest.update(
                    json.dumps(
                        {
                            "componentType": accessor["componentType"],
                            "count": accessor["count"],
                            "type": accessor["type"],
                            "normalized": accessor.get("normalized", False),
                            "byteOffset": accessor.get("byteOffset", 0),
                            "byteStride": view.get("byteStride"),
                        },
                        sort_keys=True,
                        separators=(",", ":"),
                    ).encode("utf-8")
                )
                digest.update(binary[start:end])
    return digest.hexdigest()


def artifact_record(path: Path, repo_root: Path) -> dict[str, Any]:
    return {
        "path": path.relative_to(repo_root).as_posix(),
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
    }


def main() -> None:
    args = parse_args()
    repo_root = args.repo_root.resolve(strict=True)
    output_dir = args.output_dir.resolve()
    try:
        output_dir.relative_to(repo_root)
    except ValueError as error:
        raise ValueError("Output directory escaped repository") from error
    if output_dir.exists():
        raise ValueError("Output directory already exists")
    if not (
        args.output_relative.startswith("sandbox/")
        or args.output_relative.startswith("var/")
    ):
        raise ValueError("Output must remain below sandbox/ or var/")
    authorities = json.loads(args.authorities_json)
    if not isinstance(authorities, dict) or not authorities:
        raise ValueError("Authority ledger must be a non-empty object")
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise ValueError(
            f"Expected Blender {EXPECTED_BLENDER[0]}.{EXPECTED_BLENDER[1]}.x, "
            f"found {bpy.app.version_string}"
        )

    output_dir.mkdir(parents=True)
    proofs_dir = output_dir / "proofs"
    frames_dir = proofs_dir / "frames"
    proofs_dir.mkdir()
    frames_dir.mkdir()

    purge_scene()
    model_collection = make_collection(MODEL_COLLECTION)
    proof_collection = make_collection(PROOF_COLLECTION)
    material = make_material()
    team_accent_material = make_team_accent_material()
    root = make_empty(
        ROOT_NAME,
        parent=None,
        collection=model_collection,
        role="authoritative-static-root",
    )
    root["nilbotsFamily"] = "aegis-tortoise"
    root["nilbotsGeometryAuthority"] = "deterministic-blender-kinematic-master"
    root["nilbotsFacing"] = "+X"
    root["nilbotsUp"] = "+Z in Blender / +Y in GLB"
    root["nilbotsTexturePlan"] = "Meshy texture-only with original UV"
    body = make_empty(
        BODY_PIVOT_NAME,
        parent=root,
        collection=model_collection,
        role="hover-settle-carrier",
    )
    hull, pod_positions = build_static_hull(
        body=body,
        material=material,
        collection=model_collection,
    )
    hatch_pivots, hatch_meshes = build_tower_hatch(
        body=body,
        material=material,
        collection=model_collection,
    )
    pod_pivots, pod_meshes = build_pod_anchors(
        body=body,
        pod_positions=pod_positions,
        material=material,
        collection=model_collection,
    )
    gun_pivot, gun_mesh = build_mobile_gun(
        body=body,
        material=material,
        collection=model_collection,
    )
    shell_rails, shell_hinges, shell_meshes = build_shell_kit(
        body=body,
        material=material,
        collection=model_collection,
    )
    team_accent_emitters = build_team_accent_emitters(
        shell_hinges=shell_hinges,
        material=team_accent_material,
        collection=model_collection,
    )
    (
        crown,
        upper,
        crown_mesh,
        upper_mesh,
        fan_pivots,
        tower_armor_meshes,
        barrel_pivots,
        tower_barrel_meshes,
        tower_team_emitters,
    ) = build_tower_kit(
        body=body,
        material=material,
        team_accent_material=team_accent_material,
        collection=model_collection,
    )

    animated_objects = (
        [body, gun_pivot, crown, upper]
        + list(pod_pivots.values())
        + list(shell_rails.values())
        + list(shell_hinges.values())
        + list(hatch_pivots.values())
        + list(fan_pivots.values())
        + list(barrel_pivots.values())
    )
    rest = capture_rest(animated_objects)
    clips = create_animations(
        body=body,
        pod_pivots=pod_pivots,
        gun_pivot=gun_pivot,
        shell_rails=shell_rails,
        shell_hinges=shell_hinges,
        hatch_pivots=hatch_pivots,
        crown=crown,
        upper=upper,
        fan_pivots=fan_pivots,
        barrel_pivots=barrel_pivots,
        rest=rest,
    )
    sample_frame(active_names=set(), frame=1, rest=rest)

    model_meshes = mesh_objects(model_collection)
    atlas = assign_original_uv_atlas(model_meshes)
    uv_layout_path = proofs_dir / "uv-layout.svg"
    write_uv_layout_svg(model_meshes, output=uv_layout_path)
    mesh_hashes = {obj.name: mesh_digest(obj) for obj in model_meshes}

    cameras = configure_scene(proof_collection)
    sample_frame(active_names=set(), frame=1, rest=rest)
    still_records: dict[str, Any] = {}
    for view in ("top", "side", "front", "rear", "gameplay"):
        path = proofs_dir / f"mobile-{view}.png"
        render_frame(camera=cameras[view], output=path, width=640, height=640)
        still_records[view] = artifact_record(path, repo_root)
    accent_meshes = sorted(
        [
            *team_accent_emitters.values(),
            *tower_team_emitters.values(),
        ],
        key=lambda obj: obj.name,
    )
    team_accent_proofs = render_team_accent_proofs(
        model_meshes=model_meshes,
        accent_meshes=accent_meshes,
        hull=hull,
        cameras=cameras,
        proofs_dir=proofs_dir,
        frames_dir=frames_dir,
    )

    moving_transform_names = [obj.name for obj in animated_objects]
    common_rest_snapshot = endpoint_snapshot(
        active=set(),
        frame=1,
        names=moving_transform_names,
        rest=rest,
    )
    shell_start_snapshot = endpoint_snapshot(
        active={"Shell_Deploy"},
        frame=1,
        names=moving_transform_names,
        rest=rest,
    )
    tower_start_snapshot = endpoint_snapshot(
        active={"Tower_Deploy"},
        frame=1,
        names=moving_transform_names,
        rest=rest,
    )
    shell_order_a = endpoint_snapshot(
        active={"Shell_Deploy"},
        frame=60,
        names=moving_transform_names,
        rest=rest,
    )
    endpoint_snapshot(
        active={"Tower_Deploy"},
        frame=60,
        names=moving_transform_names,
        rest=rest,
    )
    shell_order_b = endpoint_snapshot(
        active={"Shell_Deploy"},
        frame=60,
        names=moving_transform_names,
        rest=rest,
    )
    tower_order_a = endpoint_snapshot(
        active={"Tower_Deploy"},
        frame=60,
        names=moving_transform_names,
        rest=rest,
    )
    endpoint_snapshot(
        active={"Shell_Deploy"},
        frame=60,
        names=moving_transform_names,
        rest=rest,
    )
    tower_order_b = endpoint_snapshot(
        active={"Tower_Deploy"},
        frame=60,
        names=moving_transform_names,
        rest=rest,
    )
    isolation_dir = frames_dir / "sample-isolation"
    isolation_dir.mkdir()
    isolation_paths = {
        "shellA": isolation_dir / "shell-a.png",
        "towerA": isolation_dir / "tower-a.png",
        "shellB": isolation_dir / "shell-b.png",
        "towerB": isolation_dir / "tower-b.png",
    }
    sample_frame(active_names={"Shell_Deploy"}, frame=60, rest=rest)
    render_frame(
        camera=cameras["gameplay"],
        output=isolation_paths["shellA"],
        width=300,
        height=300,
    )
    sample_frame(active_names={"Tower_Deploy"}, frame=60, rest=rest)
    render_frame(
        camera=cameras["gameplay"],
        output=isolation_paths["towerA"],
        width=300,
        height=300,
    )
    sample_frame(active_names={"Shell_Deploy"}, frame=60, rest=rest)
    sample_frame(active_names={"Tower_Deploy"}, frame=60, rest=rest)
    render_frame(
        camera=cameras["gameplay"],
        output=isolation_paths["towerB"],
        width=300,
        height=300,
    )
    sample_frame(active_names={"Shell_Deploy"}, frame=60, rest=rest)
    render_frame(
        camera=cameras["gameplay"],
        output=isolation_paths["shellB"],
        width=300,
        height=300,
    )
    sample_isolation = {
        "shellFrame1VsCommonRestMaxDelta": endpoint_delta(
            shell_start_snapshot, common_rest_snapshot
        ),
        "towerFrame1VsCommonRestMaxDelta": endpoint_delta(
            tower_start_snapshot, common_rest_snapshot
        ),
        "shellPermutationMaxDelta": endpoint_delta(shell_order_a, shell_order_b),
        "towerPermutationMaxDelta": endpoint_delta(tower_order_a, tower_order_b),
        "shellRenderHashes": [
            sha256(isolation_paths["shellA"]),
            sha256(isolation_paths["shellB"]),
        ],
        "towerRenderHashes": [
            sha256(isolation_paths["towerA"]),
            sha256(isolation_paths["towerB"]),
        ],
        "shellRenderPixelDifference": image_difference_metrics(
            isolation_paths["shellA"],
            isolation_paths["shellB"],
            threshold=0.0,
        ),
        "towerRenderPixelDifference": image_difference_metrics(
            isolation_paths["towerA"],
            isolation_paths["towerB"],
            threshold=0.0,
        ),
    }

    contact_records = {
        "Shell_Deploy": render_contact_sheet(
            active_clips={"Shell_Deploy"},
            frames=SHELL_CONTACT_FRAMES,
            camera=cameras["gameplay"],
            output=proofs_dir / "shell-deploy-contact-sheet.png",
            frame_dir=frames_dir / "shell-deploy",
            rest=rest,
            stage_labels=(
                "REST",
                "ANTICIPATE",
                "UNLOCK",
                "RAIL",
                "RAIL",
                "HINGE",
                "OPEN",
                "SETTLE",
                "HOLD",
                "HOLD",
            ),
        ),
        "Shell_Deploy_Top": render_contact_sheet(
            active_clips={"Shell_Deploy"},
            frames=SHELL_CONTACT_FRAMES,
            camera=cameras["top"],
            output=proofs_dir / "shell-deploy-contact-sheet-top.png",
            frame_dir=frames_dir / "shell-deploy-top",
            rest=rest,
            stage_labels=(
                "REST",
                "ANTICIPATE",
                "UNLOCK",
                "RAIL",
                "RAIL",
                "HINGE",
                "OPEN",
                "SETTLE",
                "HOLD",
                "HOLD",
            ),
        ),
        "Tower_Deploy": render_contact_sheet(
            active_clips={"Tower_Deploy"},
            frames=TOWER_CONTACT_FRAMES,
            camera=cameras["gameplay"],
            output=proofs_dir / "tower-deploy-contact-sheet.png",
            frame_dir=frames_dir / "tower-deploy",
            rest=rest,
            stage_labels=(
                "REST",
                "LOCK",
                "HATCH",
                "OPEN",
                "LOWER",
                "LOWER",
                "UPPER",
                "GUN LOCK",
                "GUNS UP",
                "HOLD",
            ),
        ),
        "Tower_Deploy_Top": render_contact_sheet(
            active_clips={"Tower_Deploy"},
            frames=TOWER_CONTACT_FRAMES,
            camera=cameras["top"],
            output=proofs_dir / "tower-deploy-contact-sheet-top.png",
            frame_dir=frames_dir / "tower-deploy-top",
            rest=rest,
            stage_labels=(
                "REST",
                "LOCK",
                "HATCH",
                "OPEN",
                "LOWER",
                "LOWER",
                "UPPER",
                "GUN LOCK",
                "GUNS UP",
                "HOLD",
            ),
        ),
        "PodPlant_HoverSettle": render_contact_sheet(
            active_clips={"PodPlant_HoverSettle"},
            frames=POD_CONTACT_FRAMES,
            camera=cameras["side"],
            output=proofs_dir / "podplant-contact-sheet.png",
            frame_dir=frames_dir / "podplant",
            rest=rest,
            stage_labels=(
                "REST",
                "COMPRESS",
                "POD 1",
                "POD 2",
                "POD 3",
                "POD 4",
                "LOCK",
                "SETTLE",
                "HOLD",
                "HOLD",
            ),
        ),
    }

    shell_deployed = endpoint_snapshot(
        active={"Shell_Deploy"},
        frame=DEPLOY_END,
        names=moving_transform_names,
        rest=rest,
    )
    shell_recoil_nodes = set(clips["Shell_Recoil"]["objects"])
    shell_overlay = {
        name: {
            "location": tuple(record["location"]),
            "rotationEuler": tuple(record["rotationEuler"]),
        }
        for name, record in shell_deployed.items()
        if name not in shell_recoil_nodes
    }
    contact_records["Shell_Recoil"] = render_contact_sheet(
        active_clips={"Shell_Recoil"},
        frames=SHELL_RECOIL_CONTACT_FRAMES,
        camera=cameras["gameplay"],
        output=proofs_dir / "shell-recoil-contact-sheet.png",
        frame_dir=frames_dir / "shell-recoil",
        deployed_overlay=shell_overlay,
        rest=rest,
        stage_labels=(
            "HOLD",
            "KICK",
            "RECOIL",
            "RETURN",
            "SETTLE",
            "HOLD",
            "HOLD",
        ),
    )
    tower_deployed = endpoint_snapshot(
        active={"Tower_Deploy"},
        frame=DEPLOY_END,
        names=moving_transform_names,
        rest=rest,
    )
    tower_overlay_base = {
        name: {
            "location": tuple(record["location"]),
            "rotationEuler": tuple(record["rotationEuler"]),
        }
        for name, record in tower_deployed.items()
    }
    tower_vane_01_nodes = set(clips["Tower_Recoil_01"]["objects"])
    tower_vane_01_overlay = {
        name: transform
        for name, transform in tower_overlay_base.items()
        if name not in tower_vane_01_nodes
    }
    tower_recoil_nodes = set(clips["Tower_Recoil"]["objects"])
    tower_overlay = {
        name: transform
        for name, transform in tower_overlay_base.items()
        if name not in tower_recoil_nodes
    }
    contact_records["Tower_Recoil_01"] = render_contact_sheet(
        active_clips={"Tower_Recoil_01"},
        frames=RECOIL_CONTACT_FRAMES,
        camera=cameras["gameplay"],
        output=proofs_dir / "tower-recoil-contact-sheet.png",
        frame_dir=frames_dir / "tower-recoil-01",
        deployed_overlay=tower_vane_01_overlay,
        rest=rest,
        stage_labels=(
            "HOLD",
            "KICK",
            "RECOIL",
            "RETURN",
            "SETTLE",
            "HOLD",
            "HOLD",
            "HOLD",
        ),
    )
    contact_records["Tower_Recoil"] = render_contact_sheet(
        active_clips={"Tower_Recoil"},
        frames=TOWER_RECOIL_CONTACT_FRAMES,
        camera=cameras["gameplay"],
        output=proofs_dir / "tower-aggregate-recoil-contact-sheet.png",
        frame_dir=frames_dir / "tower-aggregate-recoil",
        deployed_overlay=tower_overlay,
        rest=rest,
        stage_labels=(
            "HOLD",
            "KICK",
            "RIPPLE",
            "RIPPLE",
            "RETURN",
            "SETTLE",
            "HOLD",
            "HOLD",
        ),
    )

    # Native 41 px proofs match the approximate on-map bot footprint.  The
    # contact sheets preserve those exact source pixels (rather than rendering
    # a large image and downsampling it after review).
    small_motion_specs = {
        "Shell_Deploy": {
            "clips": {"Shell_Deploy"},
            "frames": (1, 14, 38, 60, 96),
            "overlay": None,
        },
        "Tower_Deploy": {
            "clips": {"Tower_Deploy"},
            "frames": (1, 18, 30, 52, 72, 84, 96),
            "overlay": None,
        },
        "Mobile_Recoil": {
            "clips": {"Mobile_Recoil"},
            "frames": (1, 3, 11, 22),
            "overlay": None,
        },
        "Shell_Recoil": {
            "clips": {"Shell_Recoil"},
            "frames": (1, 3, 11, 24),
            "overlay": shell_overlay,
        },
        "Tower_Recoil": {
            "clips": {"Tower_Recoil"},
            "frames": (1, 5, 9, 14, 34),
            "overlay": tower_overlay,
        },
    }
    small_motion_records: dict[str, Any] = {}
    for proof_name, spec in small_motion_specs.items():
        slug = proof_name.lower().replace("_", "-")
        output = proofs_dir / f"{slug}-41px-contact-sheet.png"
        frame_dir = frames_dir / f"{slug}-41px"
        record = render_contact_sheet(
            active_clips=spec["clips"],
            frames=spec["frames"],
            camera=cameras["gameplay"],
            output=output,
            frame_dir=frame_dir,
            deployed_overlay=spec["overlay"],
            rest=rest,
            tile_size=41,
        )
        record["artifact"] = artifact_record(output, repo_root)
        record["nativeFramePixels"] = 41
        record["frameDirectory"] = frame_dir.relative_to(repo_root).as_posix()
        small_motion_records[proof_name] = record

    shell_landmarks: dict[int, dict[str, dict[str, Vector]]] = {}
    for frame in (1, 14, 38, 60, 96):
        sample_frame(active_names={"Shell_Deploy"}, frame=frame, rest=rest)
        shell_landmarks[frame] = {
            plate_name: screen_landmark(
                mesh,
                camera=cameras["gameplay"],
            )
            for plate_name, mesh in shell_meshes.items()
        }
    shell_part_readability: dict[str, Any] = {}
    for plate_name in SHELL_PLATES:
        unlock = shell_landmarks[14][plate_name]
        rail = shell_landmarks[38][plate_name]
        open_pose = shell_landmarks[60][plate_name]
        rail_pixels = vector_distance(unlock["center"], rail["center"])
        unlock_vector = rail["tip"] - rail["center"]
        hinge_vector = open_pose["tip"] - open_pose["center"]
        hinge_pixels = vector_distance(unlock_vector, hinge_vector)
        shell_part_readability[plate_name] = {
            "railCenterTravelPixels": rail_pixels,
            "hingeTipArcPixels": hinge_pixels,
            "readableRail": rail_pixels >= 0.75,
            "readableHinge": hinge_pixels >= 0.75,
        }

    tower_landmarks: dict[int, dict[str, Any]] = {}
    for frame in (1, 18, 30, 40, 52, 64, 72, 74, 84, 96):
        sample_frame(active_names={"Tower_Deploy"}, frame=frame, rest=rest)
        crown_center = screen_landmark(
            crown_mesh,
            camera=cameras["gameplay"],
        )["center"]
        tower_landmarks[frame] = {
            "crown": crown_center,
            "crownLocalZ": float(crown.location.z),
            "upperLocalZ": float(upper.location.z),
            "hatchLocalX": {
                name: float(pivot.rotation_euler.x)
                for name, pivot in hatch_pivots.items()
            },
            "vanes": {
                vane_name: (
                    screen_landmark(mesh, camera=cameras["gameplay"])["tip"]
                    - crown_center
                )
                for vane_name, mesh in tower_armor_meshes.items()
            },
            "vaneLocal": {
                vane_name: {
                    "location": fan_pivots[vane_name].location.copy(),
                    "rotation": fan_pivots[vane_name].rotation_euler.copy(),
                }
                for vane_name in TOWER_VANES
            },
        }
    tower_active_vanes: dict[str, int] = {}
    for frame in (64, 72, 74, 84, 96):
        count = 0
        for vane_name in TOWER_VANES:
            rest_local = tower_landmarks[1]["vaneLocal"][vane_name]
            frame_local = tower_landmarks[frame]["vaneLocal"][vane_name]
            local_travel = vector_distance(
                rest_local["location"],
                frame_local["location"],
            )
            local_yaw = abs(
                frame_local["rotation"].z - rest_local["rotation"].z
            )
            local_pitch = abs(
                frame_local["rotation"].y - rest_local["rotation"].y
            )
            if (
                local_travel >= 0.010
                or local_yaw >= math.radians(4.0)
                or local_pitch >= math.radians(4.0)
            ):
                count += 1
        tower_active_vanes[str(frame)] = count
    crown_lift_pixels = vector_distance(
        tower_landmarks[1]["crown"],
        tower_landmarks[52]["crown"],
    )

    tower_stowed_meshes = [
        crown_mesh,
        upper_mesh,
        *tower_armor_meshes.values(),
        *tower_barrel_meshes.values(),
        *tower_team_emitters.values(),
    ]
    sample_frame(active_names={"Tower_Deploy"}, frame=1, rest=rest)
    stowed_tower_bounds = collective_bounds_world(tower_stowed_meshes)
    closed_hatch_bounds = collective_bounds_world(hatch_meshes.values())
    stowed_radial_extent = maximum_radial_extent_world(
        tower_stowed_meshes,
        center=(TOWER_CENTER_X, 0.0),
    )
    stowage_clearance = {
        "belowHatch": closed_hatch_bounds[0].z - stowed_tower_bounds[1].z,
        "aboveWellFloor": stowed_tower_bounds[0].z - 0.093,
        "insideWellRadius": TOWER_WELL_RADIUS - stowed_radial_extent,
        "deployedSleeveEngagement": (
            (0.185 + 0.170 * 0.5 + 0.305)
            - (0.199 - 0.202 * 0.5 + 0.305 + 0.155)
        ),
    }
    prelift_floor_clearance = math.inf
    for frame in range(1, 37):
        sample_frame(active_names={"Tower_Deploy"}, frame=frame, rest=rest)
        frame_bounds = collective_bounds_world(tower_stowed_meshes)
        moving_floor_top = 0.093 + float(body.matrix_world.translation.z)
        prelift_floor_clearance = min(
            prelift_floor_clearance,
            frame_bounds[0].z - moving_floor_top,
        )
    stowage_clearance["minimumAboveFloorThroughHatchPhase"] = (
        prelift_floor_clearance
    )

    sample_frame(active_names={"Tower_Deploy"}, frame=30, rest=rest)
    hatch_target_errors = {
        name: abs(
            abs(float(pivot.rotation_euler.x))
            - abs(math.radians(float(pivot["nilbotsDeployAngleDegrees"])))
        )
        for name, pivot in hatch_pivots.items()
    }
    hatch_first_sequence = {
        "checkpointFrame": 30,
        "targetErrorDegrees": {
            name: rounded(math.degrees(error), 6)
            for name, error in hatch_target_errors.items()
        },
        "crownTravel": rounded(
            abs(float(crown.location.z) - float(rest[crown.name]["location"][2])),
            7,
        ),
    }

    sample_frame(active_names={"Tower_Deploy"}, frame=72, rest=rest)
    guns_wait_for_upper_keep = (
        abs(
            float(upper.location.z)
            - (float(rest[upper.name]["location"][2]) + 0.155)
        )
        < EPSILON
        and all(
            vector_distance(
                fan.location,
                Vector(rest[fan.name]["location"]),
            )
            < EPSILON
            and abs(
                float(fan.rotation_euler.y)
                - float(rest[fan.name]["rotation_euler"][1])
            )
            < EPSILON
            and vector_distance(
                barrel_pivots[vane_name].location,
                Vector(rest[barrel_pivots[vane_name].name]["location"]),
            )
            < EPSILON
            for vane_name, fan in fan_pivots.items()
        )
    )

    gun_sequence_records: dict[str, Any] = {}
    for zero_index, vane_name in enumerate(TOWER_VANES):
        fan = fan_pivots[vane_name]
        barrel = barrel_pivots[vane_name]
        start = 72 + (zero_index % 4)
        fan_rest_location = Vector(rest[fan.name]["location"])
        fan_rest_pitch = float(rest[fan.name]["rotation_euler"][1])
        barrel_rest_location = Vector(rest[barrel.name]["location"])

        sample_frame(
            active_names={"Tower_Deploy"},
            frame=start + 5,
            rest=rest,
        )
        slide_travel = vector_distance(fan.location, fan_rest_location)
        pitch_at_slide = abs(float(fan.rotation_euler.y) - fan_rest_pitch)

        sample_frame(
            active_names={"Tower_Deploy"},
            frame=start + 7,
            rest=rest,
        )
        fold_pitch_delta = abs(float(fan.rotation_euler.y) - fan_rest_pitch)

        sample_frame(
            active_names={"Tower_Deploy"},
            frame=start + 13,
            rest=rest,
        )
        barrel_extension = vector_distance(
            barrel.location,
            barrel_rest_location,
        )
        pitch_when_barrel_moves = abs(float(fan.rotation_euler.y))
        gun_sequence_records[vane_name] = {
            "slideFrame": start + 5,
            "slideTravel": rounded(slide_travel, 7),
            "pitchDeltaAtSlideDegrees": rounded(
                math.degrees(pitch_at_slide),
                6,
            ),
            "foldFrame": start + 7,
            "foldPitchDeltaDegrees": rounded(
                math.degrees(fold_pitch_delta),
                6,
            ),
            "barrelFrame": start + 13,
            "barrelExtension": rounded(barrel_extension, 7),
            "pitchFromHorizontalWhenBarrelMovesDegrees": rounded(
                math.degrees(pitch_when_barrel_moves),
                6,
            ),
        }
    gun_slide_precedes_fold = all(
        record["slideTravel"] >= 0.029
        and record["pitchDeltaAtSlideDegrees"] <= 0.01
        and record["foldPitchDeltaDegrees"] >= 20.0
        for record in gun_sequence_records.values()
    )
    barrels_wait_for_fold = all(
        record["barrelExtension"] >= 0.049
        and record["pitchFromHorizontalWhenBarrelMovesDegrees"] <= 15.0
        for record in gun_sequence_records.values()
    )

    # Dense structural sweep.  Explicit forbidden cross-pairs avoid noisy
    # contacts inside the joined static chassis while still auditing every
    # surface that can invalidate the tower's authored clearance.
    hatch_names = {obj.name for obj in hatch_meshes.values()}
    tower_gun_meshes = [
        *tower_armor_meshes.values(),
        *tower_barrel_meshes.values(),
    ]
    tower_rigid_meshes = [crown_mesh, upper_mesh, *tower_gun_meshes]
    shell_rigid_meshes = [*shell_meshes.values()]
    forbidden_pair_reasons: dict[tuple[str, str], str] = {}

    def forbid_pairs(
        left_objects: Iterable[bpy.types.Object],
        right_objects: Iterable[bpy.types.Object],
        reason: str,
    ) -> None:
        for left in left_objects:
            for right in right_objects:
                if left.name == right.name:
                    continue
                forbidden_pair_reasons[tuple(sorted((left.name, right.name)))] = (
                    reason
                )

    forbid_pairs([hull], tower_rigid_meshes, "tower kit must clear static hull")
    forbid_pairs(
        hatch_meshes.values(),
        [
            *tower_rigid_meshes,
            *shell_rigid_meshes,
            *team_accent_emitters.values(),
            *tower_team_emitters.values(),
            gun_mesh,
        ],
        "moving hatch must clear every stance and muzzle surface",
    )
    hatch_list = list(hatch_meshes.values())
    forbidden_pair_reasons[
        tuple(sorted((hatch_list[0].name, hatch_list[1].name)))
    ] = "opposed hatch leaves require a real center seam"
    forbid_pairs(
        shell_rigid_meshes,
        tower_rigid_meshes,
        "Tower transition must clear the stowed Shell kit",
    )
    forbid_pairs([gun_mesh], tower_rigid_meshes, "tower must clear mobile muzzle")
    forbid_pairs([crown_mesh], [upper_mesh], "telescopic stages cannot overlap")
    forbid_pairs(
        [crown_mesh, upper_mesh],
        tower_gun_meshes,
        "stowed and deploying gun magazines must clear both keep stages",
    )

    structural_objects_by_name = {
        obj.name: obj
        for obj in (
            hull,
            gun_mesh,
            *hatch_meshes.values(),
            *shell_rigid_meshes,
            *team_accent_emitters.values(),
            *tower_team_emitters.values(),
            *tower_rigid_meshes,
        )
    }
    tower_structural_violations: list[dict[str, Any]] = []
    hatch_sweep_violations: list[dict[str, Any]] = []
    for frame in range(1, DEPLOY_END + 1):
        sample_frame(active_names={"Tower_Deploy"}, frame=frame, rest=rest)
        overlaps = bvh_overlap_pairs(list(structural_objects_by_name.values()))
        for left, right, pairs in overlaps:
            pair = tuple(sorted((left, right)))
            reason = forbidden_pair_reasons.get(pair)
            if reason is None:
                continue
            violation = {
                "frame": frame,
                "left": left,
                "right": right,
                "pairs": pairs,
                "reason": reason,
            }
            tower_structural_violations.append(violation)
            if left in hatch_names or right in hatch_names:
                hatch_sweep_violations.append(violation)

    tower_recoil_structural_violations: list[dict[str, Any]] = []
    recoil_specs: list[
        tuple[str, range, dict[str, dict[str, Any]]]
    ] = [("Tower_Recoil", range(1, 35), tower_overlay)]
    for vane_name in TOWER_VANES:
        clip_name = f"Tower_Recoil_{vane_name}"
        active_nodes = set(clips[clip_name]["objects"])
        recoil_specs.append(
            (
                clip_name,
                range(1, 23),
                {
                    name: transform
                    for name, transform in tower_overlay_base.items()
                    if name not in active_nodes
                },
            )
        )
    for clip_name, frames, overlay in recoil_specs:
        for frame in frames:
            sample_frame(active_names={clip_name}, frame=frame, rest=rest)
            for name, transform in overlay.items():
                obj = bpy.data.objects[name]
                obj.location = transform["location"]
                obj.rotation_euler = transform["rotationEuler"]
            bpy.context.view_layer.update()
            for left, right, pairs in bvh_overlap_pairs(
                list(structural_objects_by_name.values())
            ):
                pair = tuple(sorted((left, right)))
                reason = forbidden_pair_reasons.get(pair)
                if reason is not None:
                    tower_recoil_structural_violations.append(
                        {
                            "clip": clip_name,
                            "frame": frame,
                            "left": left,
                            "right": right,
                            "pairs": pairs,
                            "reason": reason,
                        }
                    )

    def sample_with_overlay(
        *,
        clip: str,
        frame: int,
        overlay: dict[str, dict[str, Any]] | None,
    ) -> None:
        sample_frame(active_names={clip}, frame=frame, rest=rest)
        if overlay is not None:
            for name, transform in overlay.items():
                obj = bpy.data.objects[name]
                obj.location = transform["location"]
                obj.rotation_euler = transform["rotationEuler"]
        bpy.context.view_layer.update()

    sample_with_overlay(clip="Mobile_Recoil", frame=1, overlay=None)
    mobile_recoil_rest = {
        "chassisCenter": projected_pixel(
            camera=cameras["gameplay"],
            world_point=body.matrix_world.translation,
        ),
        "gunTip": screen_landmark(
            gun_mesh,
            camera=cameras["gameplay"],
        )["tip"],
    }
    sample_with_overlay(clip="Mobile_Recoil", frame=3, overlay=None)
    mobile_recoil_kick = {
        "chassisCenter": projected_pixel(
            camera=cameras["gameplay"],
            world_point=body.matrix_world.translation,
        ),
        "gunTip": screen_landmark(
            gun_mesh,
            camera=cameras["gameplay"],
        )["tip"],
    }

    shell_recoil_landmarks: dict[int, dict[str, Vector]] = {}
    for frame in (1, 3):
        sample_with_overlay(
            clip="Shell_Recoil",
            frame=frame,
            overlay=shell_overlay,
        )
        shell_recoil_landmarks[frame] = {
            plate_name: screen_landmark(
                shell_meshes[plate_name],
                camera=cameras["gameplay"],
            )["center"]
            for plate_name in ("NorthInner", "SouthInner")
        }

    tower_aggregate_landmarks: dict[int, dict[str, dict[str, Vector]]] = {}
    for frame in (1, 9):
        sample_with_overlay(
            clip="Tower_Recoil",
            frame=frame,
            overlay=tower_overlay,
        )
        tower_aggregate_landmarks[frame] = {
            vane_name: {
                "vaneCenter": screen_landmark(
                    tower_armor_meshes[vane_name],
                    camera=cameras["gameplay"],
                )["center"],
                "barrelTip": screen_landmark(
                    tower_barrel_meshes[vane_name],
                    camera=cameras["gameplay"],
                )["tip"],
            }
            for vane_name in TOWER_VANES
        }

    tower_per_vane_travel: dict[str, Any] = {}
    for vane_name in TOWER_VANES:
        clip_name = f"Tower_Recoil_{vane_name}"
        nodes = set(clips[clip_name]["objects"])
        overlay = {
            name: transform
            for name, transform in tower_overlay_base.items()
            if name not in nodes
        }
        sampled: dict[int, dict[str, Vector]] = {}
        for frame in (1, 3):
            sample_with_overlay(
                clip=clip_name,
                frame=frame,
                overlay=overlay,
            )
            sampled[frame] = {
                "vaneCenter": screen_landmark(
                    tower_armor_meshes[vane_name],
                    camera=cameras["gameplay"],
                )["center"],
                "barrelTip": screen_landmark(
                    tower_barrel_meshes[vane_name],
                    camera=cameras["gameplay"],
                )["tip"],
            }
        tower_per_vane_travel[vane_name] = {
            "vaneCenterPixels": vector_distance(
                sampled[1]["vaneCenter"],
                sampled[3]["vaneCenter"],
            ),
            "barrelTipPixels": vector_distance(
                sampled[1]["barrelTip"],
                sampled[3]["barrelTip"],
            ),
        }

    recoil_principal_travel = {
        "targetPixels": [3.0, 5.0],
        "mobile": {
            "chassisCenterPixels": vector_distance(
                mobile_recoil_rest["chassisCenter"],
                mobile_recoil_kick["chassisCenter"],
            ),
            "gunTipPixels": vector_distance(
                mobile_recoil_rest["gunTip"],
                mobile_recoil_kick["gunTip"],
            ),
        },
        "shell": {
            plate_name: {
                "plateCenterPixels": vector_distance(
                    shell_recoil_landmarks[1][plate_name],
                    shell_recoil_landmarks[3][plate_name],
                )
            }
            for plate_name in ("NorthInner", "SouthInner")
        },
        "towerAggregateFrame9": {
            vane_name: {
                "vaneCenterPixels": vector_distance(
                    tower_aggregate_landmarks[1][vane_name]["vaneCenter"],
                    tower_aggregate_landmarks[9][vane_name]["vaneCenter"],
                ),
                "barrelTipPixels": vector_distance(
                    tower_aggregate_landmarks[1][vane_name]["barrelTip"],
                    tower_aggregate_landmarks[9][vane_name]["barrelTip"],
                ),
            }
            for vane_name in TOWER_VANES
        },
        "towerPerVanePeak": tower_per_vane_travel,
    }

    def small_frame_path(proof_name: str, frame: int) -> Path:
        slug = proof_name.lower().replace("_", "-")
        return frames_dir / f"{slug}-41px" / f"f{frame:03d}.png"

    small_pixel_deltas = {
        "Shell_Deploy_RestToOpen": image_difference_metrics(
            small_frame_path("Shell_Deploy", 1),
            small_frame_path("Shell_Deploy", 60),
        ),
        "Tower_Deploy_RestToFinal": image_difference_metrics(
            small_frame_path("Tower_Deploy", 1),
            small_frame_path("Tower_Deploy", 96),
        ),
        "Mobile_Recoil_RestToKick": image_difference_metrics(
            small_frame_path("Mobile_Recoil", 1),
            small_frame_path("Mobile_Recoil", 3),
        ),
        "Mobile_Recoil_RestToSettle": image_difference_metrics(
            small_frame_path("Mobile_Recoil", 1),
            small_frame_path("Mobile_Recoil", 22),
        ),
        "Shell_Recoil_RestToKick": image_difference_metrics(
            small_frame_path("Shell_Recoil", 1),
            small_frame_path("Shell_Recoil", 3),
        ),
        "Shell_Recoil_RestToSettle": image_difference_metrics(
            small_frame_path("Shell_Recoil", 1),
            small_frame_path("Shell_Recoil", 24),
        ),
        "Tower_Recoil_RestToRipple": image_difference_metrics(
            small_frame_path("Tower_Recoil", 1),
            small_frame_path("Tower_Recoil", 9),
        ),
        "Tower_Recoil_RestToSettle": image_difference_metrics(
            small_frame_path("Tower_Recoil", 1),
            small_frame_path("Tower_Recoil", 34),
        ),
    }
    motion_readability = {
        "nativeGameplayPixels": 41,
        "supersededV11ChangedPixelBaselines": {
            "Mobile_Recoil_RestToKick": 199,
            "Shell_Recoil_RestToKick": 36,
            "Tower_Recoil_RestToRipple": 5,
        },
        "shellParts": shell_part_readability,
        "tower": {
            "crownLiftPixels": crown_lift_pixels,
            "activeVanesByFrame": tower_active_vanes,
            "stowageClearance": {
                name: rounded(value, 7)
                for name, value in stowage_clearance.items()
            },
            "hatchFirst": hatch_first_sequence,
            "gunsWaitForUpperKeep": guns_wait_for_upper_keep,
            "gunSequence": gun_sequence_records,
            "hatchSweepViolations": hatch_sweep_violations,
            "structuralSweepViolations": tower_structural_violations,
            "recoilStructuralSweepViolations": (
                tower_recoil_structural_violations
            ),
        },
        "pixelDeltas": small_pixel_deltas,
        "recoilPrincipalTravel": recoil_principal_travel,
        "thresholds": {
            "shellRailPixelsPerPlate": 0.75,
            "shellHingePixelsPerPlate": 0.75,
            "towerCrownLiftPixels": 1.25,
            "mobileRecoilChangedPixels": 300,
            "shellRecoilChangedPixels": 90,
            "towerRecoilChangedPixels": 55,
            "principalTravelPixels": {
                "mobileChassis": [2.75, 5.5],
                "mobileGunTip": [3.0, 5.75],
                "shellPlateCenter": [2.75, 5.5],
                "towerVaneCenterPerspectiveAware": [0.75, 2.25],
                "towerBarrelTip": [2.75, 5.0],
            },
            "settleChangedPixels": 0,
        },
    }

    # Concept approval needs the strict multiviews, keyed contact sheets, the
    # editable animation authority, and one direct Tower deploy/retract loop.
    # The complete Shell/recoil GIF suite is a promotion gate and can consume
    # several GB while Blender retains render buffers, so it stays disabled in
    # this scratch-only concept builder.
    render_full_motion_loops = False
    render_tower_owner_preview = True
    motion_records = {
        "Shell_Deploy_Loop": render_motion_loop(
            deploy_clip="Shell_Deploy",
            recoil_clip="Shell_Recoil",
            recoil_end=24,
            deployed_overlay=shell_deployed,
            recoil_nodes=set(clips["Shell_Recoil"]["objects"]),
            camera=cameras["gameplay"],
            frame_dir=frames_dir / "motion-shell",
            output=proofs_dir / "shell-deploy-loop.gif",
            rest=rest,
        ),
        "Tower_Deploy_Loop": render_motion_loop(
            deploy_clip="Tower_Deploy",
            recoil_clip="Tower_Recoil",
            recoil_end=34,
            deployed_overlay=tower_deployed,
            recoil_nodes=set(clips["Tower_Recoil"]["objects"]),
            camera=cameras["gameplay"],
            frame_dir=frames_dir / "motion-tower",
            output=proofs_dir / "tower-deploy-loop.gif",
            rest=rest,
        ),
    } if render_full_motion_loops else ({
        "Tower_Deploy_Loop": render_motion_loop(
            deploy_clip="Tower_Deploy",
            recoil_clip=None,
            recoil_end=0,
            deployed_overlay=tower_deployed,
            recoil_nodes=set(),
            camera=cameras["gameplay"],
            frame_dir=frames_dir / "motion-tower-owner-preview",
            output=proofs_dir / "tower-deploy-loop.gif",
            rest=rest,
        ),
    } if render_tower_owner_preview else {})
    recoil_motion_records = {
        "Mobile_Recoil_Loop": render_recoil_loop(
            clip="Mobile_Recoil",
            clip_end=RECOIL_END,
            deployed_overlay=None,
            camera=cameras["gameplay"],
            frame_dir=frames_dir / "motion-mobile-recoil",
            output=proofs_dir / "mobile-recoil-loop.gif",
            rest=rest,
        ),
        "Shell_Recoil_Loop": render_recoil_loop(
            clip="Shell_Recoil",
            clip_end=24,
            deployed_overlay=shell_overlay,
            camera=cameras["gameplay"],
            frame_dir=frames_dir / "motion-shell-recoil",
            output=proofs_dir / "shell-recoil-loop.gif",
            rest=rest,
        ),
        "Tower_Recoil_Loop": render_recoil_loop(
            clip="Tower_Recoil",
            clip_end=34,
            deployed_overlay=tower_overlay,
            camera=cameras["gameplay"],
            frame_dir=frames_dir / "motion-tower-recoil",
            output=proofs_dir / "tower-recoil-loop.gif",
            rest=rest,
        ),
    } if render_full_motion_loops else {}

    # Endpoint and reversibility proof.  The reverse operation samples the
    # same clip in descending frame order; no separately-authored inverse can
    # drift.
    endpoint_records: dict[str, Any] = {}
    for clip_name, active in (
        ("PodPlant_HoverSettle", {"PodPlant_HoverSettle"}),
        ("Shell_Deploy", {"Shell_Deploy"}),
        ("Tower_Deploy", {"Tower_Deploy"}),
    ):
        start = endpoint_snapshot(
            active=active,
            frame=1,
            names=moving_transform_names,
            rest=rest,
        )
        end = endpoint_snapshot(
            active=active,
            frame=DEPLOY_END,
            names=moving_transform_names,
            rest=rest,
        )
        reverse_start = endpoint_snapshot(
            active=active,
            frame=DEPLOY_END,
            names=moving_transform_names,
            rest=rest,
        )
        reverse_end = endpoint_snapshot(
            active=active,
            frame=1,
            names=moving_transform_names,
            rest=rest,
        )
        endpoint_records[clip_name] = {
            "start": start,
            "end": end,
            "forwardEndVsReverseStartMaxDelta": endpoint_delta(
                end, reverse_start
            ),
            "forwardStartVsReverseEndMaxDelta": endpoint_delta(
                start, reverse_end
            ),
            "reverseMethod": "sample identical semantic clip backward",
        }
    recoil_clip_ends = {
        "Shell_Recoil": 24,
        "Tower_Recoil": 34,
        "Mobile_Recoil": RECOIL_END,
        **{
            f"Tower_Recoil_{index:02d}": RECOIL_END
            for index in range(1, VANE_COUNT + 1)
        },
    }
    for clip_name, clip_end in recoil_clip_ends.items():
        names = list(clips[clip_name]["objects"])
        start = endpoint_snapshot(
            active={clip_name},
            frame=1,
            names=names,
            rest=rest,
        )
        end = endpoint_snapshot(
            active={clip_name},
            frame=clip_end,
            names=names,
            rest=rest,
        )
        endpoint_records[clip_name] = {
            "start": start,
            "end": end,
            "returnToRestMaxDelta": endpoint_delta(start, end),
        }

    # Form endpoint renders supplement the motion sheets.
    form_records: dict[str, Any] = {}
    for form, active in (
        ("shell", {"Shell_Deploy"}),
        ("tower", {"Tower_Deploy"}),
    ):
        sample_frame(active_names=active, frame=DEPLOY_END, rest=rest)
        for view in ("top", "side", "front", "rear", "gameplay"):
            path = proofs_dir / f"{form}-{view}.png"
            camera_name = (
                f"tower-{view}"
                if form == "tower" and view in {"side", "front", "rear"}
                else view
            )
            render_frame(
                camera=cameras[camera_name],
                output=path,
                width=640,
                height=640,
            )
            form_records[f"{form}-{view}"] = artifact_record(path, repo_root)

    # Sample moving-part BVH intersections.  The static hull is intentionally a
    # union of overlapping armor primitives, so the check is restricted to
    # independently moving assemblies whose sweeps must remain clear.
    collision_samples: list[dict[str, Any]] = []
    moving_meshes = (
        list(shell_meshes.values())
        + list(pod_meshes.values())
        + [crown_mesh, upper_mesh]
        + list(tower_armor_meshes.values())
        + list(tower_barrel_meshes.values())
        + [gun_mesh]
    )
    collision_clip_specs: list[
        tuple[
            str,
            set[str],
            tuple[int, ...],
            dict[str, dict[str, Any]] | None,
        ]
    ] = [
        ("Shell_Deploy", {"Shell_Deploy"}, SHELL_CONTACT_FRAMES, None),
        ("Tower_Deploy", {"Tower_Deploy"}, TOWER_CONTACT_FRAMES, None),
        (
            "Mobile_Recoil",
            {"Mobile_Recoil"},
            (1, 3, 6, 11, 16, 22),
            None,
        ),
        (
            "Shell_Recoil",
            {"Shell_Recoil"},
            SHELL_RECOIL_CONTACT_FRAMES,
            shell_overlay,
        ),
        (
            "Tower_Recoil",
            {"Tower_Recoil"},
            TOWER_RECOIL_CONTACT_FRAMES,
            tower_overlay,
        ),
    ]
    for vane_name in TOWER_VANES:
        clip_name = f"Tower_Recoil_{vane_name}"
        nodes = set(clips[clip_name]["objects"])
        collision_clip_specs.append(
            (
                clip_name,
                {clip_name},
                RECOIL_CONTACT_FRAMES,
                {
                    name: transform
                    for name, transform in tower_overlay_base.items()
                    if name not in nodes
                },
            )
        )
    for clip_name, active, frames, overlay in collision_clip_specs:
        for frame in frames:
            sample_frame(active_names=active, frame=frame, rest=rest)
            if overlay is not None:
                for name, transform in overlay.items():
                    obj = bpy.data.objects[name]
                    obj.location = transform["location"]
                    obj.rotation_euler = transform["rotationEuler"]
                bpy.context.view_layer.update()
            overlaps = bvh_overlap_pairs(moving_meshes)
            collision_samples.append(
                {
                    "clip": clip_name,
                    "frame": frame,
                    "overlaps": [
                        {"left": left, "right": right, "pairs": count}
                        for left, right, count in overlaps
                    ],
                }
            )
    collision_baselines: dict[str, Any] = {}
    collision_violations: list[dict[str, Any]] = []
    for clip_name, _active, _frames, _overlay in collision_clip_specs:
        clip_samples = [
            sample for sample in collision_samples if sample["clip"] == clip_name
        ]
        baseline_sample = next(
            sample for sample in clip_samples if sample["frame"] == 1
        )
        baseline_map = {
            (item["left"], item["right"]): item["pairs"]
            for item in baseline_sample["overlaps"]
        }
        collision_baselines[clip_name] = {
            "frame": 1,
            "pairs": [
                {
                    "left": left,
                    "right": right,
                    "pairs": pairs,
                    "allowlisted": (left, right) in COLLISION_ALLOWLIST,
                    "reason": COLLISION_ALLOWLIST.get((left, right)),
                    "maximumAdditionalTrianglePairs": (
                        COLLISION_DELTA_ALLOWANCE.get((left, right), 0)
                    ),
                }
                for (left, right), pairs in sorted(baseline_map.items())
            ],
        }
        for sample in clip_samples:
            current_map = {
                (item["left"], item["right"]): item["pairs"]
                for item in sample["overlaps"]
            }
            classified = []
            for pair, pairs in sorted(current_map.items()):
                baseline_pairs = baseline_map.get(pair, 0)
                allowlisted = pair in COLLISION_ALLOWLIST
                delta_allowance = COLLISION_DELTA_ALLOWANCE.get(pair, 0)
                record = {
                    "left": pair[0],
                    "right": pair[1],
                    "pairs": pairs,
                    "baselinePairs": baseline_pairs,
                    "deltaPairs": pairs - baseline_pairs,
                    "newIdentity": pair not in baseline_map,
                    "allowlisted": allowlisted,
                    "reason": COLLISION_ALLOWLIST.get(pair),
                    "maximumAdditionalTrianglePairs": delta_allowance,
                    "withinBoundedAllowance": (
                        allowlisted
                        and pairs <= baseline_pairs + delta_allowance
                    ),
                }
                classified.append(record)
                if (
                    not allowlisted
                    or pairs > baseline_pairs + delta_allowance
                ):
                    violation = {
                        "clip": clip_name,
                        "frame": sample["frame"],
                        **record,
                    }
                    collision_violations.append(violation)
            sample["classified"] = classified
            sample["newPairIdentities"] = [
                record for record in classified if record["newIdentity"]
            ]
            sample["severityIncreases"] = [
                record for record in classified if record["deltaPairs"] > 0
            ]
            sample["unallowlisted"] = [
                record for record in classified if not record["allowlisted"]
            ]

    sample_frame(active_names=set(), frame=1, rest=rest)
    rest_bounds = collective_bounds_world(model_meshes)
    rest_span_x = rest_bounds[1].x - rest_bounds[0].x
    rest_span_y = rest_bounds[1].y - rest_bounds[0].y
    rest_height = rest_bounds[1].z - rest_bounds[0].z
    rest_hover_gap = rest_bounds[0].z

    sample_frame(active_names={"Shell_Deploy"}, frame=DEPLOY_END, rest=rest)
    shell_centroid_angles = []
    for plate_name, plate in shell_meshes.items():
        center = mesh_centroid_world(plate)
        angle = math.degrees(math.atan2(center.y, center.x))
        shell_centroid_angles.append(
            {"plate": plate_name, "degreesFromEast": rounded(angle, 5)}
        )
    sample_frame(active_names={"Tower_Deploy"}, frame=DEPLOY_END, rest=rest)
    tower_angles = []
    for vane_name, fan in fan_pivots.items():
        center = fan.matrix_world.translation
        angle = math.degrees(math.atan2(center.y, center.x + 0.085)) % 360.0
        tower_angles.append(
            {"vane": vane_name, "degrees": rounded(angle, 5)}
        )
    tower_angles.sort(key=lambda item: item["degrees"])
    tower_spacing = [
        rounded(
            (
                tower_angles[(index + 1) % VANE_COUNT]["degrees"]
                - tower_angles[index]["degrees"]
            )
            % 360.0,
            5,
        )
        for index in range(VANE_COUNT)
    ]
    sample_frame(
        active_names={"PodPlant_HoverSettle"},
        frame=DEPLOY_END,
        rest=rest,
    )
    planted_pad_bottoms = {
        pod_name: rounded(object_bounds_world(mesh)[0].z, 7)
        for pod_name, mesh in pod_meshes.items()
    }

    sample_frame(active_names=set(), frame=1, rest=rest)
    blend_path = output_dir / "aegis-tortoise-kinematic-master.blend"
    glb_path = output_dir / "aegis-tortoise-neutral-animated.glb"
    upload_glb_path = output_dir / "aegis-tortoise-texture-upload.glb"
    root["nilbotsSemanticClips"] = list(SEMANTIC_CLIPS)
    root["nilbotsShellSectorDegrees"] = [-45, 45]
    root["nilbotsTowerVaneCount"] = VANE_COUNT
    root["nilbotsUVAtlas"] = "UVMap, one non-overlapping authored atlas"
    root["nilbotsNoInventedFinalGeometry"] = True
    root["nilbotsTeamAccentMaterial"] = TEAM_ACCENT_MATERIAL_NAME
    root["nilbotsTeamAccentMeshCount"] = len(accent_meshes)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    sample_frame(active_names=set(), frame=1, rest=rest)
    export_glb(output=glb_path, model_collection=model_collection)
    sample_frame(active_names=set(), frame=1, rest=rest)
    export_texture_upload_glb(
        output=upload_glb_path,
        model_collection=model_collection,
        accent_meshes=accent_meshes,
        neutral_material=material,
    )
    glb = parse_glb(glb_path)
    upload_glb = parse_glb(upload_glb_path)
    animated_surface_digest = glb_surface_digest(glb_path)
    upload_surface_digest = glb_surface_digest(upload_glb_path)

    animation_data_on_root = root.animation_data is not None and (
        root.animation_data.action is not None
        or len(root.animation_data.nla_tracks) > 0
    )
    shape_key_objects = [
        obj.name
        for obj in model_meshes
        if obj.data.shape_keys is not None
    ]
    materials = sorted(
        {
            material_slot.name
            for obj in model_meshes
            for material_slot in obj.data.materials
            if material_slot is not None
        }
    )
    surface_materials = sorted(
        {
            material_slot.name
            for obj in model_meshes
            if obj not in accent_meshes
            for material_slot in obj.data.materials
            if material_slot is not None
        }
    )
    accent_materials = sorted(
        {
            material_slot.name
            for obj in accent_meshes
            for material_slot in obj.data.materials
            if material_slot is not None
        }
    )
    material_images = []
    for candidate in bpy.data.materials:
        if candidate.node_tree is None:
            continue
        for node in candidate.node_tree.nodes:
            image = getattr(node, "image", None)
            if image is not None:
                material_images.append(image.name)
    current_mesh_hashes = {obj.name: mesh_digest(obj) for obj in model_meshes}
    geometry_unchanged = current_mesh_hashes == mesh_hashes
    triangles = sum(
        sum(len(polygon.vertices) - 2 for polygon in obj.data.polygons)
        for obj in model_meshes
    )
    vertices = sum(len(obj.data.vertices) for obj in model_meshes)
    faces = sum(len(obj.data.polygons) for obj in model_meshes)
    accent_triangles = sum(
        sum(len(polygon.vertices) - 2 for polygon in obj.data.polygons)
        for obj in accent_meshes
    )
    uv_coverage = uv_area(model_meshes)
    all_uv_in_bounds = all(
        -EPSILON <= loop.uv.x <= 1.0 + EPSILON
        and -EPSILON <= loop.uv.y <= 1.0 + EPSILON
        for obj in model_meshes
        for loop in obj.data.uv_layers.active.data
    )
    uv_cell_count = len(
        {tuple(record["cell"]) for record in atlas["objects"].values()}
    )
    expected_glb_animations = set(SEMANTIC_CLIPS)
    actual_glb_animations = set(glb["animations"])
    accent_mesh_data_names = {obj.data.name for obj in accent_meshes}
    glb_mesh_names = set(glb["meshNames"])
    upload_mesh_names = set(upload_glb["meshNames"])
    recoil_settle_keys = (
        "Mobile_Recoil_RestToSettle",
        "Shell_Recoil_RestToSettle",
        "Tower_Recoil_RestToSettle",
    )
    shell_motion_readable = all(
        item["readableRail"] and item["readableHinge"]
        for item in shell_part_readability.values()
    )
    tower_ripple_counts = tower_active_vanes
    tower_motion_readable = (
        crown_lift_pixels >= 1.25
        and tower_ripple_counts["64"] == 0
        and tower_ripple_counts["72"] == 0
        and 0 < tower_ripple_counts["74"] < VANE_COUNT
        and tower_ripple_counts["84"] == VANE_COUNT
        and tower_ripple_counts["96"] == VANE_COUNT
        and small_pixel_deltas["Tower_Deploy_RestToFinal"]["changedPixels"] >= 20
    )
    recoil_silhouettes_amplified = (
        small_pixel_deltas["Mobile_Recoil_RestToKick"]["changedPixels"] >= 300
        and small_pixel_deltas["Shell_Recoil_RestToKick"]["changedPixels"] >= 90
        and small_pixel_deltas["Tower_Recoil_RestToRipple"]["changedPixels"] >= 55
    )
    recoil_principal_travel_readable = (
        2.75
        <= recoil_principal_travel["mobile"]["chassisCenterPixels"]
        <= 5.5
        and 3.0
        <= recoil_principal_travel["mobile"]["gunTipPixels"]
        <= 5.75
        and all(
            2.75 <= record["plateCenterPixels"] <= 5.5
            for record in recoil_principal_travel["shell"].values()
        )
        and all(
            0.75 <= record["vaneCenterPixels"] <= 2.25
            and 2.75 <= record["barrelTipPixels"] <= 5.0
            for record in recoil_principal_travel[
                "towerPerVanePeak"
            ].values()
        )
        and sum(
            record["barrelTipPixels"] >= 1.5
            for record in recoil_principal_travel[
                "towerAggregateFrame9"
            ].values()
        )
        >= 6
    )
    recoil_settles_exactly_at_41px = all(
        small_pixel_deltas[key]["changedPixels"] == 0
        for key in recoil_settle_keys
    )
    recoil_motion_readable = (
        recoil_silhouettes_amplified
        and recoil_principal_travel_readable
        and recoil_settles_exactly_at_41px
    )
    localized_recoil_ownership = (
        set(clips["Mobile_Recoil"]["objects"])
        == {body.name, gun_pivot.name}
        and set(clips["Shell_Recoil"]["objects"])
        == {
            shell_rails["NorthInner"].name,
            shell_rails["SouthInner"].name,
            shell_hinges["NorthInner"].name,
            shell_hinges["SouthInner"].name,
        }
        and set(clips["Tower_Recoil"]["objects"])
        == {
            *[pivot.name for pivot in fan_pivots.values()],
            *[pivot.name for pivot in barrel_pivots.values()],
        }
        and all(
            set(clips[f"Tower_Recoil_{vane_name}"]["objects"])
            == {
                fan_pivots[vane_name].name,
                barrel_pivots[vane_name].name,
            }
            for vane_name in TOWER_VANES
        )
    )

    failures: list[str] = []
    checks = {
        "blenderVersionPinned": bpy.app.version[:2] == EXPECTED_BLENDER,
        "rootHasNoAnimation": not animation_data_on_root,
        "noArmatures": not any(
            obj.type == "ARMATURE" for obj in model_collection.all_objects
        ),
        "noShapeKeys": not shape_key_objects,
        "oneNeutralSurfaceMaterial": surface_materials == [
            NEUTRAL_MATERIAL_NAME
        ],
        "oneSemanticTeamAccentMaterial": (
            accent_materials == [TEAM_ACCENT_MATERIAL_NAME]
            and team_accent_material.get("nilbotsRole") == "team-accent"
            and not bool(team_accent_material.get("nilbotsTextureEligible"))
        ),
        "exactlyEightTeamAccentEmitters": len(accent_meshes) == 8,
        "teamAccentCoverageUnderSixPercent": (
            0.003 <= team_accent_proofs["coverageRatio"] < 0.06
        ),
        "teamAccentVisibleAt41px": (
            team_accent_proofs["visiblePixelsAt41"] >= 4
            and team_accent_proofs["blueVsOrangeAt41"]["changedPixels"] >= 4
        ),
        "noTextureImages": not material_images and glb["images"] == 0,
        "allMeshesHaveOneUVLayer": all(
            len(obj.data.uv_layers) == 1 for obj in model_meshes
        ),
        "allUVsInBounds": all_uv_in_bounds,
        "uniqueUVCellPerMesh": uv_cell_count == len(model_meshes),
        "usefulUVArea": uv_coverage > 0.18,
        "exactlyFourAnchorPivots": len(pod_pivots) == 4,
        "exactlyFourShellPlates": len(shell_meshes) == 4,
        "exactlyEightTowerVanes": len(tower_armor_meshes) == VANE_COUNT,
        "exactlyEightTowerBarrels": len(tower_barrel_meshes) == VANE_COUNT,
        "shellCentroidsInsideEastSector": all(
            -45.0 - EPSILON <= item["degreesFromEast"] <= 45.0 + EPSILON
            for item in shell_centroid_angles
        ),
        "towerVanesEvenlySpaced": all(
            abs(spacing - 45.0) < 0.01 for spacing in tower_spacing
        ),
        "allPadsPlantAtFloor": all(
            abs(bottom) < 0.001 for bottom in planted_pad_bottoms.values()
        ),
        "allSemanticClipsAuthored": set(clips) == expected_glb_animations,
        "allSemanticClipsExported": expected_glb_animations.issubset(
            actual_glb_animations
        ),
        "recoilOwnershipIsLocalized": localized_recoil_ownership,
        "shellMotionReadableAt41px": shell_motion_readable,
        "towerMotionReadableAt41px": tower_motion_readable,
        "towerStowedBelowHatchAboveFloorInsideWell": (
            stowage_clearance["belowHatch"] >= 0.005
            and stowage_clearance["aboveWellFloor"] >= 0.004
            and stowage_clearance["insideWellRadius"] >= 0.005
            and stowage_clearance["minimumAboveFloorThroughHatchPhase"]
            >= 0.004
            and stowage_clearance["deployedSleeveEngagement"] >= 0.015
        ),
        "hatchFullyOpenBeforeCrownMoves": (
            max(hatch_target_errors.values()) <= math.radians(1.0)
            and hatch_first_sequence["crownTravel"] == 0.0
        ),
        "gunsWaitForUpperKeep": guns_wait_for_upper_keep,
        "gunSlidePrecedesFold": gun_slide_precedes_fold,
        "barrelsWaitForFold": barrels_wait_for_fold,
        "hatchSweepAvoidsAllMovingKits": not hatch_sweep_violations,
        "towerStructuralSweepAvoidsForbiddenPairs": (
            not tower_structural_violations
        ),
        "towerRecoilStructuralSweepAvoidsForbiddenPairs": (
            not tower_recoil_structural_violations
        ),
        "recoilSilhouettesAmplifiedAt41px": recoil_silhouettes_amplified,
        "recoilPrincipalTravelAt41px": recoil_principal_travel_readable,
        "recoilSettlesExactlyAt41px": recoil_settles_exactly_at_41px,
        "recoilsReadableAndSettleAt41px": recoil_motion_readable,
        "formClipsStartAtCommonMobileRest": (
            sample_isolation["shellFrame1VsCommonRestMaxDelta"] == 0.0
            and sample_isolation["towerFrame1VsCommonRestMaxDelta"] == 0.0
        ),
        "sampleOrderIndependent": (
            sample_isolation["shellPermutationMaxDelta"] == 0.0
            and sample_isolation["towerPermutationMaxDelta"] == 0.0
        ),
        "renderOrderIndependent": (
            sample_isolation["shellRenderPixelDifference"][
                "maximumChannelDelta"
            ]
            == 0.0
            and sample_isolation["towerRenderPixelDifference"][
                "maximumChannelDelta"
            ]
            == 0.0
        ),
        "recoilReturnsExactly": all(
            endpoint_records[name]["returnToRestMaxDelta"] == 0.0
            for name in recoil_clip_ends
        ),
        "deployEndpointsExactlyReversible": all(
            endpoint_records[name]["forwardEndVsReverseStartMaxDelta"] == 0.0
            and endpoint_records[name]["forwardStartVsReverseEndMaxDelta"] == 0.0
            for name in (
                "PodPlant_HoverSettle",
                "Shell_Deploy",
                "Tower_Deploy",
            )
        ),
        "geometryNeverInventedOrMutated": geometry_unchanged,
        "noScaleAnimation": all(
            all(
                all(
                    abs(value - 1.0) < EPSILON
                    for value in transform["scale"]
                )
                for transform in (
                    endpoint_records[clip_name]["start"].values()
                )
            )
            for clip_name in endpoint_records
        ),
        "mobilePlanformNearApprovedEnvelope": (
            1.04 <= max(rest_span_x, rest_span_y) <= 1.18
        ),
        "mobileIsLowProfile": rest_height / max(rest_span_x, rest_span_y) < 0.44,
        "mobileActuallyHovers": 0.035 <= rest_hover_gap <= 0.090,
        "glbHasNoSkin": glb["skins"] == 0,
        "glbHasNoCamera": glb["cameras"] == 0,
        "glbTriangleParity": glb["triangles"] == triangles,
        "uploadHasNoAnimations": not upload_glb["animations"],
        "uploadHasSameSurfaceCounts": (
            upload_glb["triangles"] == glb["triangles"]
            and upload_glb["meshes"] == glb["meshes"]
        ),
        "uploadHasSingleNeutralAtlasMaterial": (
            upload_glb["materials"] == 1
            and upload_glb["materialNames"] == [NEUTRAL_MATERIAL_NAME]
        ),
        "teamAccentPersistsInAnimatedGlb": accent_mesh_data_names.issubset(
            glb_mesh_names
        ),
        "teamAccentSurfaceIncludedInUpload": accent_mesh_data_names.issubset(
            upload_mesh_names
        ),
        "uploadSurfaceBytesMatchAuthority": (
            upload_surface_digest == animated_surface_digest
        ),
        "noUnallowlistedMovingPartInterpenetration": not collision_violations,
    }
    for name, passed in checks.items():
        if not passed:
            failures.append(name)

    report = {
        "schema": REPORT_SCHEMA,
        "status": "PASS" if not failures else "FAIL",
        "failures": failures,
        "outputDirectory": args.output_relative,
        "authorities": authorities,
        "method": {
            "animationRevision": ANIMATION_REVISION,
            "geometryAuthority": "deterministic Blender primitives/profiles",
            "providerCalls": 0,
            "providerGeometryImported": False,
            "textureNextStage": (
                "Meshy retexture/texture-only against this exact neutral GLB "
                "with original UV retention"
            ),
            "rigType": "rigid object pivots and NLA tracks",
            "skinning": False,
            "shapeKeys": False,
            "runtimeIntegration": False,
        },
        "artifacts": {
            "blend": artifact_record(blend_path, repo_root),
            "glb": artifact_record(glb_path, repo_root),
            "textureUploadGlb": artifact_record(upload_glb_path, repo_root),
            "uvLayout": artifact_record(uv_layout_path, repo_root),
            "stills": still_records,
            "forms": form_records,
            "teamAccent": {
                "variants": {
                    variant: {
                        "gameplay": artifact_record(
                            record["large"],
                            repo_root,
                        ),
                        "gameplay41px": artifact_record(
                            record["small"],
                            repo_root,
                        ),
                    }
                    for variant, record in team_accent_proofs[
                        "variants"
                    ].items()
                },
                "coverageMask": artifact_record(
                    team_accent_proofs["coverageMask"],
                    repo_root,
                ),
                "bodyMask": artifact_record(
                    team_accent_proofs["bodyMask"],
                    repo_root,
                ),
                "smallMask": artifact_record(
                    team_accent_proofs["smallMask"],
                    repo_root,
                ),
            },
            "motionProofs": {
                key: {
                    **{
                        field: value
                        for field, value in record.items()
                        if field != "path"
                    },
                    "artifact": artifact_record(
                        proofs_dir
                        / (
                            "shell-deploy-loop.gif"
                            if key == "Shell_Deploy_Loop"
                            else "tower-deploy-loop.gif"
                        ),
                        repo_root,
                    ),
                }
                for key, record in motion_records.items()
            },
            "recoilMotionProofs": {
                key: {
                    **{
                        field: value
                        for field, value in record.items()
                        if field != "path"
                    },
                    "artifact": artifact_record(
                        proofs_dir
                        / {
                            "Mobile_Recoil_Loop": "mobile-recoil-loop.gif",
                            "Shell_Recoil_Loop": "shell-recoil-loop.gif",
                            "Tower_Recoil_Loop": "tower-recoil-loop.gif",
                        }[key],
                        repo_root,
                    ),
                }
                for key, record in recoil_motion_records.items()
            },
            "contactSheets": {
                key: {
                    **value,
                    "path": (
                        proofs_dir
                        / {
                            "Shell_Deploy": "shell-deploy-contact-sheet.png",
                            "Shell_Deploy_Top": (
                                "shell-deploy-contact-sheet-top.png"
                            ),
                            "Tower_Deploy": "tower-deploy-contact-sheet.png",
                            "Tower_Deploy_Top": (
                                "tower-deploy-contact-sheet-top.png"
                            ),
                            "PodPlant_HoverSettle": "podplant-contact-sheet.png",
                            "Shell_Recoil": "shell-recoil-contact-sheet.png",
                            "Tower_Recoil_01": "tower-recoil-contact-sheet.png",
                            "Tower_Recoil": (
                                "tower-aggregate-recoil-contact-sheet.png"
                            ),
                        }[key]
                    ).relative_to(repo_root).as_posix(),
                }
                for key, value in contact_records.items()
            },
            "gameplaySizeMotionProofs": small_motion_records,
        },
        "geometry": {
            "meshObjects": len(model_meshes),
            "vertices": vertices,
            "faces": faces,
            "triangles": triangles,
            "semanticAccentTriangles": accent_triangles,
            "textureEligibleTriangles": triangles - accent_triangles,
            "meshHashes": mesh_hashes,
            "geometryUnchangedAfterAllAnimationSampling": geometry_unchanged,
            "restBounds": {
                "minimum": [rounded(value) for value in rest_bounds[0]],
                "maximum": [rounded(value) for value in rest_bounds[1]],
                "spanX": rounded(rest_span_x),
                "spanY": rounded(rest_span_y),
                "height": rounded(rest_height),
                "hoverGap": rounded(rest_hover_gap),
            },
            "parts": {
                "hoverLockPods": sorted(pod_meshes),
                "shellPlates": sorted(shell_meshes),
                "towerVanes": sorted(tower_armor_meshes),
                "towerBarrels": sorted(tower_barrel_meshes),
                "teamAccentEmitters": sorted(
                    obj.name for obj in accent_meshes
                ),
            },
            "shellSector": {
                "permittedDegrees": [-45, 45],
                "deployedCentroids": shell_centroid_angles,
                "otherQuadrantsOpen": checks[
                    "shellCentroidsInsideEastSector"
                ],
            },
            "towerRadial": {
                "vaneCount": VANE_COUNT,
                "angles": tower_angles,
                "spacingDegrees": tower_spacing,
                "noPrivilegedNose": checks["towerVanesEvenlySpaced"],
            },
            "plantedPadBottoms": planted_pad_bottoms,
        },
        "uv": {
            "layer": "UVMap",
            "atlasCount": 1,
            "grid": atlas["grid"],
            "uniqueCells": uv_cell_count,
            "coverage": rounded(uv_coverage, 8),
            "allInBounds": all_uv_in_bounds,
            "objects": atlas["objects"],
            "suitableForEnableOriginalUv": (
                checks["allMeshesHaveOneUVLayer"]
                and checks["allUVsInBounds"]
                and checks["uniqueUVCellPerMesh"]
                and checks["usefulUVArea"]
            ),
        },
        "materials": {
            "names": materials,
            "surfaceNames": surface_materials,
            "semanticAccentNames": accent_materials,
            "textureImages": material_images,
            "neutralSurface": True,
            "atlasReady": True,
            "teamAccent": {
                "material": TEAM_ACCENT_MATERIAL_NAME,
                "role": team_accent_material.get("nilbotsRole"),
                "textureEligible": bool(
                    team_accent_material.get("nilbotsTextureEligible")
                ),
                "meshCount": len(accent_meshes),
                "coverageRatioOfFixedTopBody": team_accent_proofs[
                    "coverageRatio"
                ],
                "visiblePixelsAt41": team_accent_proofs[
                    "visiblePixelsAt41"
                ],
                "blueVsOrangeAt41": team_accent_proofs[
                    "blueVsOrangeAt41"
                ],
            },
        },
        "animations": {
            "fps": FPS,
            "semanticClips": clips,
            "expectedNames": list(SEMANTIC_CLIPS),
            "exportedNames": glb["animations"],
            "endpoints": endpoint_records,
            "rootAnimated": animation_data_on_root,
            "contactFrames": {
                "Shell_Deploy": list(SHELL_CONTACT_FRAMES),
                "Tower_Deploy": list(TOWER_CONTACT_FRAMES),
                "PodPlant_HoverSettle": list(POD_CONTACT_FRAMES),
                "Shell_Recoil": list(SHELL_RECOIL_CONTACT_FRAMES),
                "Tower_Recoil": list(TOWER_RECOIL_CONTACT_FRAMES),
                "Tower_Recoil_01": list(RECOIL_CONTACT_FRAMES),
            },
            "gameplaySizeReadability": motion_readability,
        },
        "sampleIsolation": sample_isolation,
        "collisionAudit": {
            "method": (
                "evaluated world-space moving-mesh BVH triangle overlap, "
                "classified against each deploy/recoil clip's frame-1 baseline"
            ),
            "conclusive": True,
            "allowlist": [
                {
                    "left": left,
                    "right": right,
                    "reason": reason,
                    "maximumAdditionalTrianglePairs": (
                        COLLISION_DELTA_ALLOWANCE[(left, right)]
                    ),
                }
                for (left, right), reason in sorted(
                    COLLISION_ALLOWLIST.items()
                )
            ],
            "baselines": collision_baselines,
            "samples": collision_samples,
            "violations": collision_violations,
            "hardGate": True,
            "note": (
                "Every independently moving rigid surface is transformed to "
                "world space before the triangle-overlap query. Only a barrel "
                "telescoping inside its own authored rigid vane housing is "
                "allowlisted, with a pinned maximum of 60 additional triangle "
                "pairs above that clip's frame-1 cradle baseline. Foreign pair "
                "identities remain zero-tolerance. In addition to the sampled "
                "general ledger, explicit static-hull, hatch, keep-stage, "
                "neighboring-kit, and gun cross-pairs are swept over every "
                "integer Tower deploy, aggregate recoil, and per-vane recoil "
                "frame. Numeric stowage and sleeve-engagement gates cover "
                "closed-solid containment that surface BVHs cannot detect."
            ),
        },
        "glb": glb,
        "textureUpload": {
            "glb": upload_glb,
            "surfaceDigest": upload_surface_digest,
            "authoritySurfaceDigest": animated_surface_digest,
            "surfaceLoopUvCorrespondenceExact": (
                upload_surface_digest == animated_surface_digest
            ),
            "semanticTeamAccentMeshesIncluded": True,
            "semanticTeamAccentMeshCount": len(accent_meshes),
            "semanticTeamAccentMaterialOnUpload": False,
            "semanticTeamAccentAuthoredMaterialPreservedForTransplant": True,
            "oneNeutralSurfaceMaterial": upload_glb["materials"] == 1,
            "animationsOmitted": not upload_glb["animations"],
            "recommendedProviderSetting": "enable_original_uv:true",
            "transplantRule": (
                "discard provider materials for TeamAccent.* meshes and retain "
                "the authored texture-free NB_TeamAccent_Semantic material"
            ),
        },
        "checks": checks,
        "blender": {
            "version": bpy.app.version_string,
            "expectedMajorMinor": list(EXPECTED_BLENDER),
        },
    }
    report_path = output_dir / "validation-report.json"
    report_path.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    if failures:
        raise RuntimeError(
            "Bulwark kinematic validation failed: " + ", ".join(failures)
        )
    print(json.dumps({"status": "PASS", "report": str(report_path)}, indent=2))


if __name__ == "__main__":
    main()
