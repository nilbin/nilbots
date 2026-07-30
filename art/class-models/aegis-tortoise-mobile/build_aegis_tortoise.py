#!/usr/bin/env python3
"""Deterministically reconstruct and rig the Aegis Tortoise mobile chassis.

Run this script with Blender, not the system Python:

    Blender --background --factory-startup --python \
      art/class-models/aegis-tortoise-mobile/build_aegis_tortoise.py -- \
      --provider-dir /absolute/path/to/the/pinned/Meshy/task \
      --output-dir sandbox/aegis-tortoise-build

The provider mesh is retained only as an upper-armor master.  Its failed fused
lower slab is cut away and replaced with named rigid chassis, cannon, and leg
parts.  The recipe never globally or non-uniformly warps the provider armor.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path
from typing import Iterable

import bmesh
import bpy
from mathutils import Matrix, Vector


RAW_FILES = {
    "raw-model.glb": "dcb22bd3975bb767c5206a92918a59d6ef92cb338e7bad8463c2040edf274534",
    "texture-0-base-color.png": "797bc36310418edee66008a2cbaf14224186462087edc48bf6fb35a7b737aa33",
    "texture-0-emission.png": "72932ace044bf9ca70b401c66c5e51deb06dcea46a2e636f0434c32dd80c9b91",
    "texture-0-metallic.png": "328ed8ad97dbb40efb2cbcd83ebc51c818a6fadb8eae3e30c81991dbf55ae3c0",
    "texture-0-normal.png": "cdbcf51cde3264f85f0c4f5ec48eef6605541017440d8b4d2d103afbd58f86de",
    "texture-0-roughness.png": "e714ad9640e6ca6f6df0e43d12c6c808e74390c8ab4589a009fdb3a5273b93e5",
}

AUTHORITY_HASHES = {
    "rich2d": "22e41a0925f1b88497ff55265bebb249b21da004c9a2559a5f1d0f70db95fbd5",
    "runtimeSvg": "0e4e5c3c9ea91feec349f7516d6f1de09ac674aac7f2243d6af8845b92fc45f9",
    "cleanTop": "d806e4ba3517fb8e09ed02ca41636dbb04d31e92b92a5d792b648574768aec18",
    "cleanSide": "5b74bdd48413aedf63a6f7a37e3d33f8f29355c8985a18c23f437ccaa0ef008f",
    "cleanFrontThreeQuarter": "279ab86b4b370706628db23edde62b959b28e85a8a372dd5046307e6efb3687e",
    "cleanRearThreeQuarter": "8e2067220e89b98e526c0cc3ca3c129bcbdb6becb571ca22c20db121067aa54f",
}

AUTHORED_PLANFORM_SPAN = 1.12
TARGET_TOTAL_HEIGHT = AUTHORED_PLANFORM_SPAN / 3.345
ARMOR_TOP_Z = TARGET_TOTAL_HEIGHT
ARMOR_CUT_Z = 0.118
RICH_ARMOR_TRIANGLES = 220_000
LEAN_ARMOR_TRIANGLES = 92_000
TEAM_BLUE = "#22d3ee"
TEAM_RED = "#ef4444"

SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parents[2]
RICH_2D = REPO_ROOT / "art/class-look-concepts/bulwark/aegis-tortoise/concept.png"
RUNTIME_SVG = REPO_ROOT / "web/src/assets/class-looks/aegis-tortoise/sprite.svg"
APPROVED_REFERENCES = {
    "cleanTop": SCRIPT_DIR / "references/approved-clean-top.png",
    "cleanSide": SCRIPT_DIR / "references/approved-clean-side.png",
    "cleanFrontThreeQuarter": (
        SCRIPT_DIR / "references/approved-clean-front-three-quarter.png"
    ),
    "cleanRearThreeQuarter": (
        SCRIPT_DIR / "references/approved-clean-rear-three-quarter.png"
    ),
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = []
    parser = argparse.ArgumentParser()
    parser.add_argument("--provider-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def verify_inputs(provider_dir: Path) -> None:
    for filename, expected in RAW_FILES.items():
        path = provider_dir / filename
        if not path.is_file():
            raise RuntimeError(f"Missing pinned provider input: {path}")
        actual = sha256(path)
        if actual != expected:
            raise RuntimeError(
                f"Provider input hash drift for {filename}: {actual} != {expected}"
            )
    for path, key in ((RICH_2D, "rich2d"), (RUNTIME_SVG, "runtimeSvg")):
        actual = sha256(path)
        if actual != AUTHORITY_HASHES[key]:
            raise RuntimeError(
                f"Authority hash drift for {path.relative_to(REPO_ROOT)}: {actual}"
            )
    for key, path in APPROVED_REFERENCES.items():
        actual = sha256(path)
        if actual != AUTHORITY_HASHES[key]:
            raise RuntimeError(
                f"Approved reference hash drift for "
                f"{path.relative_to(REPO_ROOT)}: {actual}"
            )


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
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def mesh_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    return (
        Vector(tuple(min(point[i] for point in points) for i in range(3))),
        Vector(tuple(max(point[i] for point in points) for i in range(3))),
    )


def collective_bounds(objects: Iterable[bpy.types.Object]) -> tuple[Vector, Vector]:
    points: list[Vector] = []
    for obj in objects:
        if obj.type != "MESH" or obj.hide_render:
            continue
        points.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
    return (
        Vector(tuple(min(point[i] for point in points) for i in range(3))),
        Vector(tuple(max(point[i] for point in points) for i in range(3))),
    )


def import_and_normalize_armor(raw_glb: Path) -> bpy.types.Object:
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(raw_glb))
    imported = [
        obj
        for obj in bpy.context.scene.objects
        if obj not in before and obj.type == "MESH"
    ]
    if len(imported) != 1:
        raise RuntimeError(f"Expected one provider mesh, found {len(imported)}")
    armor = imported[0]
    armor.name = "Armor.ProviderMaster.Rich"
    armor.data.name = "Armor.ProviderMaster.Rich.Mesh"

    # Blender's glTF importer leaves this unusual provider result with physical
    # thickness on local Y.  Work directly on mesh data so object/Euler order
    # cannot silently change the conversion:
    # source-facing -X -> authored +X, provider physical +Z -> Blender +Z.
    armor.data.transform(Matrix.Rotation(math.radians(90), 4, "X"))
    armor.data.transform(Matrix.Rotation(math.radians(180), 4, "Z"))

    minimum, maximum = mesh_bounds(armor)
    planform_span = max(maximum.x - minimum.x, maximum.y - minimum.y)
    uniform_scale = AUTHORED_PLANFORM_SPAN / planform_span
    armor.data.transform(Matrix.Scale(uniform_scale, 4))

    minimum, maximum = mesh_bounds(armor)
    centre = Vector(((minimum.x + maximum.x) / 2, (minimum.y + maximum.y) / 2, 0))
    armor.data.transform(Matrix.Translation(-centre))

    # Preserve the complete upper armor without vertical scaling.  Move its
    # highest point to the approved side-profile roof, then physically cut the
    # provider's failed lower slab and collapsed pseudo-feet.
    _, maximum = mesh_bounds(armor)
    armor.data.transform(Matrix.Translation((0, 0, ARMOR_TOP_Z - maximum.z)))
    bisect_lower_slab(armor, ARMOR_CUT_Z)
    armor["nilbotsPart"] = "provider-upper-armor-master"
    armor["sourceSha256"] = RAW_FILES["raw-model.glb"]
    armor["normalization"] = (
        "mesh X+90deg, mesh Z+180deg, uniform planform 1.12, "
        f"roof Z={ARMOR_TOP_Z:.6f}, lower bisect Z={ARMOR_CUT_Z:.6f}"
    )
    return armor


def bisect_lower_slab(obj: bpy.types.Object, cut_z: float) -> None:
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    geometry = list(bm.verts) + list(bm.edges) + list(bm.faces)
    bmesh.ops.bisect_plane(
        bm,
        geom=geometry,
        dist=0.000001,
        plane_co=(0, 0, cut_z),
        plane_no=(0, 0, 1),
        clear_inner=True,
        clear_outer=False,
    )
    boundary = [
        edge
        for edge in bm.edges
        if edge.is_boundary
        and abs((edge.verts[0].co.z + edge.verts[1].co.z) / 2 - cut_z) < 0.0001
    ]
    if boundary:
        bmesh.ops.holes_fill(bm, edges=boundary)
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    minimum, maximum = mesh_bounds(obj)
    if minimum.z < cut_z - 0.0002 or maximum.z < ARMOR_TOP_Z - 0.0002:
        raise RuntimeError(
            f"Upper-armor bisect failed: z bounds {minimum.z:.6f}..{maximum.z:.6f}"
        )


def decimate_to(obj: bpy.types.Object, target_triangles: int) -> None:
    current = len(obj.data.polygons)
    if current <= target_triangles:
        return
    modifier = obj.modifiers.new(
        name=f"Deterministic decimation to {target_triangles}", type="DECIMATE"
    )
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = target_triangles / current
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def srgb(hex_color: str) -> tuple[float, float, float, float]:
    hex_color = hex_color.removeprefix("#")
    return tuple(int(hex_color[i : i + 2], 16) / 255 for i in (0, 2, 4)) + (1.0,)


def make_material(
    name: str,
    base: str,
    *,
    metallic: float,
    roughness: float,
    emission: str | None = None,
    emission_strength: float = 0.0,
    role: str | None = None,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = srgb(base)
    nodes = material.node_tree.nodes
    principled = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    principled.inputs["Base Color"].default_value = srgb(base)
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    if emission:
        principled.inputs["Emission Color"].default_value = srgb(emission)
        principled.inputs["Emission Strength"].default_value = emission_strength
    if role:
        material["nilbotsRole"] = role
    return material


def active_object(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_bevel(
    obj: bpy.types.Object, width: float, *, segments: int = 2
) -> bpy.types.Object:
    modifier = obj.modifiers.new(name="Authored edge bevel", type="BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    active_object(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def add_box(
    name: str,
    dimensions: tuple[float, float, float],
    location: tuple[float, float, float],
    material: bpy.types.Material,
    *,
    bevel: float,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = f"{name}.Mesh"
    obj.data.transform(Matrix.Diagonal((*dimensions, 1)))
    obj.data.materials.append(material)
    apply_bevel(obj, bevel)
    return obj


def add_cylinder(
    name: str,
    radius: float,
    depth: float,
    location: tuple[float, float, float],
    material: bpy.types.Material,
    *,
    axis: str,
    vertices: int = 16,
    bevel: float = 0.004,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth, location=location
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = f"{name}.Mesh"
    if axis == "X":
        obj.data.transform(Matrix.Rotation(math.radians(90), 4, "Y"))
    elif axis == "Y":
        obj.data.transform(Matrix.Rotation(math.radians(90), 4, "X"))
    elif axis != "Z":
        raise ValueError(axis)
    obj.data.materials.append(material)
    if bevel:
        apply_bevel(obj, bevel)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def add_inlay(
    name: str,
    dimensions: tuple[float, float, float],
    location: tuple[float, float, float],
    rotation_z: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    panel = add_box(
        name,
        dimensions,
        location,
        material,
        bevel=min(dimensions) * 0.26,
    )
    panel.rotation_euler.z = rotation_z
    panel["nilbotsPart"] = "semantic-team-inlay"
    panel["nilbotsRole"] = "team-accent"
    return panel


def create_armature() -> bpy.types.Object:
    armature_data = bpy.data.armatures.new("Aegis.Tortoise.Rig")
    armature = bpy.data.objects.new("Aegis.Tortoise.Rig", armature_data)
    bpy.context.collection.objects.link(armature)
    active_object(armature)
    bpy.ops.object.mode_set(mode="EDIT")

    root = armature_data.edit_bones.new("root")
    root.head = (0, 0, 0)
    root.tail = (0, 0, 0.08)

    body = armature_data.edit_bones.new("body")
    body.head = (0, 0, 0.12)
    body.tail = (0, 0, 0.24)
    body.parent = root

    cannon = armature_data.edit_bones.new("cannon")
    cannon.head = (0.30, 0, 0.205)
    cannon.tail = (0.49, 0, 0.205)
    cannon.parent = body

    leg_specs = leg_anchors()
    for leg_id, anchor in leg_specs.items():
        hip = armature_data.edit_bones.new(f"hip_{leg_id}")
        hip.head = anchor
        hip.tail = (anchor[0], anchor[1], anchor[2] - 0.055)
        hip.parent = body

        knee = armature_data.edit_bones.new(f"knee_{leg_id}")
        knee.head = hip.tail
        knee.tail = (anchor[0], anchor[1], anchor[2] - 0.108)
        knee.parent = hip
        knee.use_connect = True

        ankle = armature_data.edit_bones.new(f"ankle_{leg_id}")
        ankle.head = knee.tail
        ankle.tail = (anchor[0], anchor[1], 0.022)
        ankle.parent = knee
        ankle.use_connect = True

    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    armature["nilbotsFacing"] = "+X"
    armature["nilbotsUp"] = "+Y after glTF export"
    armature["nilbotsAnimationContract"] = (
        "looped in-place actions; replay renderer seeks normalized clip time"
    )
    return armature


def leg_anchors() -> dict[str, tuple[float, float, float]]:
    return {
        "fl": (0.355, 0.372, 0.147),
        "fr": (0.355, -0.372, 0.147),
        "rl": (-0.405, 0.455, 0.147),
        "rr": (-0.405, -0.455, 0.147),
    }


def parent_to_bone(
    obj: bpy.types.Object, armature: bpy.types.Object, bone_name: str
) -> None:
    world = obj.matrix_world.copy()
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = world
    obj["nilbotsParentPivot"] = bone_name


def upper_surface_z(
    armor: bpy.types.Object, x: float, y: float, fallback: float
) -> float:
    hit, location, _normal, _face = armor.ray_cast(
        Vector((x, y, 1.0)), Vector((0, 0, -1))
    )
    return location.z + 0.006 if hit else fallback


def build_authored_parts(
    armor: bpy.types.Object,
) -> tuple[bpy.types.Object, dict[str, bpy.types.Material], list[bpy.types.Object]]:
    materials = {
        "graphite": make_material(
            "NB_GRAPHITE_MECHANICAL",
            "#171d29",
            metallic=0.72,
            roughness=0.48,
        ),
        "navy": make_material(
            "NB_NAVY_ARMOR",
            "#202d4b",
            metallic=0.58,
            roughness=0.52,
        ),
        "bronze": make_material(
            "NB_WEATHERED_BRONZE",
            "#8f6b43",
            metallic=0.70,
            roughness=0.44,
        ),
        "rubber": make_material(
            "NB_FOOT_CONTACT",
            "#090d14",
            metallic=0.05,
            roughness=0.82,
        ),
        "team": make_material(
            "NB_TEAM_ACCENT",
            TEAM_BLUE,
            metallic=0.18,
            roughness=0.30,
            emission=TEAM_BLUE,
            emission_strength=1.2,
            role="team-accent",
        ),
        "energy": make_material(
            "NB_CLASS_ENERGY_CYAN",
            "#a5e8ff",
            metallic=0.05,
            roughness=0.24,
            emission="#38bdf8",
            emission_strength=2.0,
        ),
    }
    armature = create_armature()
    authored: list[bpy.types.Object] = []

    lower = add_cylinder(
        "LowerChassis.ArmoredRing",
        1,
        0.065,
        (0.0, 0.0, 0.130),
        materials["navy"],
        axis="Z",
        vertices=16,
        bevel=0.008,
    )
    lower.scale.x = 0.405
    lower.scale.y = 0.325
    active_object(lower)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    lower["nilbotsPart"] = "reconstructed-lower-chassis"
    authored.append(lower)

    belly = add_box(
        "LowerChassis.Belly",
        (0.53, 0.43, 0.055),
        (-0.06, 0, 0.105),
        materials["graphite"],
        bevel=0.018,
    )
    belly["nilbotsPart"] = "reconstructed-lower-chassis"
    authored.append(belly)

    side_skirt = add_box(
        "LowerChassis.SideSkirt",
        (0.72, 0.46, 0.058),
        (-0.015, 0, 0.083),
        materials["graphite"],
        bevel=0.014,
    )
    side_skirt["nilbotsPart"] = "reconstructed-lower-chassis"
    authored.append(side_skirt)

    # A clean, side-readable embedded cannon surrounds the provider's fused
    # approximation without extending beyond the approved C-shield planform.
    cannon_parts = [
        add_box(
            "Cannon.Housing",
            (0.205, 0.135, 0.070),
            (0.365, 0, 0.205),
            materials["graphite"],
            bevel=0.014,
        ),
        add_box(
            "Cannon.Rail.Top",
            (0.205, 0.032, 0.026),
            (0.400, 0.050, 0.205),
            materials["bronze"],
            bevel=0.006,
        ),
        add_box(
            "Cannon.Rail.Bottom",
            (0.205, 0.032, 0.026),
            (0.400, -0.050, 0.205),
            materials["bronze"],
            bevel=0.006,
        ),
        add_cylinder(
            "Cannon.Barrel",
            0.028,
            0.190,
            (0.450, 0, 0.205),
            materials["graphite"],
            axis="X",
            vertices=20,
            bevel=0.004,
        ),
        add_cylinder(
            "Cannon.Muzzle",
            0.036,
            0.043,
            (0.537, 0, 0.205),
            materials["bronze"],
            axis="X",
            vertices=20,
            bevel=0.004,
        ),
        add_box(
            "Cannon.ClassEnergy",
            (0.095, 0.014, 0.022),
            (0.410, -0.070, 0.205),
            materials["energy"],
            bevel=0.004,
        ),
    ]
    for part in cannon_parts:
        part["nilbotsPart"] = "embedded-mobile-cannon"
        parent_to_bone(part, armature, "cannon")
    authored.extend(cannon_parts)

    for leg_id, anchor in leg_anchors().items():
        y_sign = 1 if anchor[1] > 0 else -1
        is_rear = leg_id.startswith("r")
        guard = add_box(
            f"Leg.{leg_id.upper()}.HipGuard",
            (0.122 if is_rear else 0.105, 0.105, 0.060),
            (anchor[0], anchor[1], anchor[2] + 0.006),
            materials["navy"],
            bevel=0.014,
        )
        guard["nilbotsPart"] = f"leg-{leg_id}-hip-guard"
        parent_to_bone(guard, armature, "body")

        upper = add_cylinder(
            f"Leg.{leg_id.upper()}.UpperPiston",
            0.026,
            0.052,
            (anchor[0], anchor[1], anchor[2] - 0.028),
            materials["bronze"],
            axis="Z",
            vertices=12,
            bevel=0.004,
        )
        upper["nilbotsPart"] = f"leg-{leg_id}-upper"
        parent_to_bone(upper, armature, f"hip_{leg_id}")

        fork_a = add_cylinder(
            f"Leg.{leg_id.upper()}.UpperFork.Fore",
            0.013,
            0.052,
            (anchor[0] + 0.024, anchor[1], anchor[2] - 0.028),
            materials["graphite"],
            axis="Z",
            vertices=10,
            bevel=0.003,
        )
        fork_a["nilbotsPart"] = f"leg-{leg_id}-upper-fork"
        parent_to_bone(fork_a, armature, f"hip_{leg_id}")

        fork_b = add_cylinder(
            f"Leg.{leg_id.upper()}.UpperFork.Aft",
            0.013,
            0.052,
            (anchor[0] - 0.024, anchor[1], anchor[2] - 0.028),
            materials["graphite"],
            axis="Z",
            vertices=10,
            bevel=0.003,
        )
        fork_b["nilbotsPart"] = f"leg-{leg_id}-upper-fork"
        parent_to_bone(fork_b, armature, f"hip_{leg_id}")

        knee = add_cylinder(
            f"Leg.{leg_id.upper()}.Knee",
            0.038,
            0.055,
            (anchor[0], anchor[1] + y_sign * 0.002, anchor[2] - 0.055),
            materials["graphite"],
            axis="Y",
            vertices=14,
            bevel=0.004,
        )
        knee["nilbotsPart"] = f"leg-{leg_id}-knee-joint"
        parent_to_bone(knee, armature, f"knee_{leg_id}")

        lower_leg = add_box(
            f"Leg.{leg_id.upper()}.Lower",
            (0.052, 0.050, 0.060),
            (anchor[0], anchor[1], anchor[2] - 0.084),
            materials["graphite"],
            bevel=0.010,
        )
        lower_leg["nilbotsPart"] = f"leg-{leg_id}-lower"
        parent_to_bone(lower_leg, armature, f"knee_{leg_id}")

        ankle_joint = add_cylinder(
            f"Leg.{leg_id.upper()}.AnkleJoint",
            0.030,
            0.054,
            (anchor[0], anchor[1], 0.047),
            materials["bronze"],
            axis="Y",
            vertices=12,
            bevel=0.004,
        )
        ankle_joint["nilbotsPart"] = f"leg-{leg_id}-ankle-joint"
        parent_to_bone(ankle_joint, armature, f"ankle_{leg_id}")

        foot_dimensions = (
            (0.145, 0.142, 0.034)
            if is_rear
            else (0.120, 0.118, 0.032)
        )
        foot = add_box(
            f"Leg.{leg_id.upper()}.Foot",
            foot_dimensions,
            (anchor[0] + (-0.010 if is_rear else 0.012), anchor[1], 0.018),
            materials["rubber"],
            bevel=0.012,
        )
        foot["nilbotsPart"] = f"leg-{leg_id}-contact-foot"
        parent_to_bone(foot, armature, f"ankle_{leg_id}")

        toe = add_box(
            f"Leg.{leg_id.upper()}.ToeArmor",
            (foot_dimensions[0] * 0.76, foot_dimensions[1] * 0.82, 0.018),
            (
                anchor[0] + (0.006 if not is_rear else -0.018),
                anchor[1],
                0.039,
            ),
            materials["navy"],
            bevel=0.006,
        )
        toe["nilbotsPart"] = f"leg-{leg_id}-toe-armor"
        parent_to_bone(toe, armature, f"ankle_{leg_id}")

        authored.extend(
            [
                guard,
                upper,
                fork_a,
                fork_b,
                knee,
                lower_leg,
                ankle_joint,
                foot,
                toe,
            ]
        )

    # The SVG owns three semantic regions.  Put them on the provider armor's
    # measured upper surface so they remain visible and never compete with the
    # cyan reactor or two long forward class-energy slits.
    semantic_panels = (
        ("TeamInlay.RearLeft", -0.430, 0.448, -math.radians(52), (0.050, 0.016)),
        ("TeamInlay.RearRight", -0.430, -0.448, math.radians(52), (0.050, 0.016)),
        ("TeamInlay.WestCentre", -0.475, 0.0, 0.0, (0.052, 0.030)),
    )
    for name, x, y, rotation_z, dimensions in semantic_panels:
        inlay = add_inlay(
            name,
            (dimensions[0], dimensions[1], 0.010),
            (x, y, upper_surface_z(armor, x, y, 0.285)),
            rotation_z,
            materials["team"],
        )
        parent_to_bone(inlay, armature, "body")
        authored.append(inlay)

    parent_to_bone(armor, armature, "body")
    parent_to_bone(lower, armature, "body")
    parent_to_bone(belly, armature, "body")
    parent_to_bone(side_skirt, armature, "body")
    return armature, materials, authored


def reset_pose(armature: bpy.types.Object) -> None:
    for bone in armature.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = (0, 0, 0)
        bone.location = (0, 0, 0)
        bone.scale = (1, 1, 1)


def key_pose_bone(
    bone: bpy.types.PoseBone,
    frame: int,
    *,
    rotation_z: float | None = None,
    location_z: float | None = None,
) -> None:
    if rotation_z is not None:
        bone.rotation_euler.z = rotation_z
        bone.keyframe_insert("rotation_euler", frame=frame, group=bone.name)
    if location_z is not None:
        bone.location.z = location_z
        bone.keyframe_insert("location", frame=frame, group=bone.name)


def make_actions(armature: bpy.types.Object) -> dict[str, bpy.types.Action]:
    armature.animation_data_create()
    actions: dict[str, bpy.types.Action] = {}

    idle = bpy.data.actions.new("Aegis_Idle")
    idle.use_fake_user = True
    armature.animation_data.action = idle
    reset_pose(armature)
    body = armature.pose.bones["body"]
    for frame, offset in ((1, 0), (13, 0.0022), (25, 0), (37, -0.0015), (49, 0)):
        key_pose_bone(body, frame, location_z=offset)
    actions["idle"] = idle

    walk = bpy.data.actions.new("Aegis_Walk")
    walk.use_fake_user = True
    armature.animation_data.action = walk
    reset_pose(armature)
    sample_frames = tuple(range(1, 26, 3))
    for frame in sample_frames:
        cycle = (frame - 1) / 24
        body_drop = -0.0035 + 0.0025 * math.cos(cycle * math.tau * 2)
        key_pose_bone(body, frame, location_z=body_drop)
        for leg_id in leg_anchors():
            diagonal_offset = 0 if leg_id in ("fl", "rr") else math.pi
            phase = cycle * math.tau + diagonal_offset
            hip_angle = math.radians(13.0) * math.sin(phase)
            swing = max(0.0, math.sin(phase))
            knee_angle = math.radians(3.0 + 20.0 * swing)
            ankle_angle = -(hip_angle + knee_angle) * 0.82
            key_pose_bone(
                armature.pose.bones[f"hip_{leg_id}"],
                frame,
                rotation_z=hip_angle,
            )
            key_pose_bone(
                armature.pose.bones[f"knee_{leg_id}"],
                frame,
                rotation_z=knee_angle,
            )
            key_pose_bone(
                armature.pose.bones[f"ankle_{leg_id}"],
                frame,
                rotation_z=ankle_angle,
            )
    actions["walk"] = walk

    armature.animation_data.action = walk
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 25
    bpy.context.scene.frame_set(1)
    return actions


def configure_scene() -> tuple[bpy.types.Object, bpy.types.Object]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -1.15

    world = bpy.data.worlds.new("Aegis Review World")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = srgb("#0b1020")
    background.inputs["Strength"].default_value = 0.45

    floor_material = make_material(
        "REVIEW_FLOOR",
        "#252c36",
        metallic=0.42,
        roughness=0.72,
    )
    floor = add_box(
        "REVIEW.Floor",
        (3.2, 3.2, 0.018),
        (0, 0, -0.012),
        floor_material,
        bevel=0.006,
    )
    floor["reviewOnly"] = True

    bpy.ops.object.light_add(type="AREA", location=(-1.6, -1.8, 2.9))
    key = bpy.context.active_object
    key.name = "REVIEW.Key"
    key.data.energy = 320
    key.data.shape = "DISK"
    key.data.size = 3.0
    key.data.color = (0.90, 0.95, 1.0)
    look_at(key, Vector((0, 0, 0.12)))

    bpy.ops.object.light_add(type="AREA", location=(1.8, 0.8, 1.1))
    fill = bpy.context.active_object
    fill.name = "REVIEW.Fill"
    fill.data.energy = 135
    fill.data.size = 2.2
    fill.data.color = (0.34, 0.48, 0.72)
    look_at(fill, Vector((0, 0, 0.12)))

    bpy.ops.object.light_add(type="AREA", location=(-0.8, 1.8, 1.8))
    rim = bpy.context.active_object
    rim.name = "REVIEW.Rim"
    rim.data.energy = 190
    rim.data.size = 1.5
    rim.data.color = (0.86, 0.64, 0.36)
    look_at(rim, Vector((0, 0, 0.15)))

    bpy.ops.object.light_add(type="AREA", location=(0, -2.0, 1.25))
    side_fill = bpy.context.active_object
    side_fill.name = "REVIEW.SideFill"
    side_fill.data.energy = 125
    side_fill.data.size = 2.0
    side_fill.data.color = (0.72, 0.82, 1.0)
    look_at(side_fill, Vector((0, 0, 0.11)))

    camera_data = bpy.data.cameras.new("REVIEW.Camera")
    camera = bpy.data.objects.new("REVIEW.Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    return floor, camera


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def set_team_material(material: bpy.types.Material, color: str) -> None:
    material.diffuse_color = srgb(color)
    principled = next(
        node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"
    )
    principled.inputs["Base Color"].default_value = srgb(color)
    principled.inputs["Emission Color"].default_value = srgb(color)


def render_still(
    output: Path,
    camera: bpy.types.Object,
    floor: bpy.types.Object,
    *,
    view: str,
) -> None:
    scene = bpy.context.scene
    background = scene.world.node_tree.nodes.get("Background")
    output.parent.mkdir(parents=True, exist_ok=True)
    if view == "top":
        floor.hide_render = True
        scene.render.film_transparent = True
        background.inputs["Color"].default_value = srgb("#0b1020")
        background.inputs["Strength"].default_value = 0.45
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.34
        camera.location = (0, 0, 3)
        look_at(camera, Vector((0, 0, 0.15)))
    elif view == "side":
        floor.hide_render = True
        scene.render.film_transparent = False
        background.inputs["Color"].default_value = srgb("#626c78")
        background.inputs["Strength"].default_value = 0.34
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.34
        camera.location = (0, -3, 0.17)
        look_at(camera, Vector((0, 0, 0.17)))
    elif view == "gameplay":
        floor.hide_render = False
        scene.render.film_transparent = False
        background.inputs["Color"].default_value = srgb("#0b1020")
        background.inputs["Strength"].default_value = 0.45
        camera.data.type = "PERSP"
        camera.data.angle = math.radians(42)
        distance = 1.88
        pitch = math.radians(58)
        camera.location = (
            -0.08,
            math.cos(pitch) * distance,
            math.sin(pitch) * distance,
        )
        look_at(camera, Vector((0.02, 0, 0.13)))
    else:
        raise ValueError(view)
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


def resize_provider_images(tier: str) -> dict[str, tuple[int, int]]:
    material = bpy.data.materials.get("Material_0")
    if material is None or not material.use_nodes:
        raise RuntimeError("Provider PBR material Material_0 not found")
    targets = {
        "rich": {
            "base-color": 2048,
            "normal": 2048,
            "metallic-roughness": 1024,
            "emission": 1024,
        },
        "lean": {
            "base-color": 1024,
            "normal": 1024,
            "metallic-roughness": 512,
            "emission": 512,
        },
    }[tier]
    roles: dict[str, bpy.types.Image] = {}
    for node in material.node_tree.nodes:
        if node.type != "TEX_IMAGE" or node.image is None:
            continue
        image = node.image
        downstream = [
            (link.to_node.type, link.to_socket.name)
            for link in material.node_tree.links
            if link.from_node == node
        ]
        if ("BSDF_PRINCIPLED", "Base Color") in downstream:
            role = "base-color"
        elif ("BSDF_PRINCIPLED", "Emission Color") in downstream:
            role = "emission"
        elif any(node_type == "NORMAL_MAP" for node_type, _socket in downstream):
            role = "normal"
        elif any(
            node_type in {"SEPARATE_COLOR", "SEPRGB"}
            for node_type, _socket in downstream
        ):
            role = "metallic-roughness"
        else:
            raise RuntimeError(
                f"Could not assign provider texture role for {image.name}: "
                f"{downstream}"
            )
        if role in roles:
            raise RuntimeError(f"Duplicate provider texture role: {role}")
        roles[role] = image
        size = targets[role]
        if image.size[0] != size or image.size[1] != size:
            image.scale(size, size)
        image.pack()
    missing = sorted(set(targets) - set(roles))
    if missing:
        raise RuntimeError(f"Missing provider texture roles: {missing}")
    return {
        role: (int(image.size[0]), int(image.size[1]))
        for role, image in sorted(roles.items())
    }


def model_objects(armature: bpy.types.Object) -> list[bpy.types.Object]:
    result = [armature]
    result.extend(
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and not obj.name.startswith("REVIEW.")
    )
    return result


def strip_unused_authored_uvs(objects: Iterable[bpy.types.Object]) -> None:
    """Do not export UV attributes for flat-color procedural materials."""

    for obj in objects:
        if obj.type != "MESH":
            continue
        while obj.data.uv_layers:
            obj.data.uv_layers.remove(obj.data.uv_layers[0])


def select_model(armature: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in model_objects(armature):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature


def export_glb(path: Path, armature: bpy.types.Object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    select_model(armature)
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=True,
        export_extras=True,
        export_cameras=False,
        export_lights=False,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_extra_animations=True,
        export_merge_animation="ACTION",
        export_optimize_animation_size=False,
        export_image_format="AUTO",
        export_image_quality=90,
    )
    canonicalize_glb_uvs(path)


def canonicalize_glb_uvs(path: Path) -> None:
    """Remove one-ULP Blender UV noise without changing topology or framing.

    Blender can emit a platform-scheduling-dependent final bit in exported
    UVs. Quantizing only retained TEXCOORD_0 values to six decimal places
    makes the review GLBs bit-stable; at 2K this moves a sample by at most
    0.002 texture pixels.
    """

    data = bytearray(path.read_bytes())
    if data[:4] != b"glTF" or struct.unpack_from("<I", data, 4)[0] != 2:
        raise RuntimeError(f"Expected a GLB 2.0 export: {path}")
    json_length, json_type = struct.unpack_from("<II", data, 12)
    if json_type != 0x4E4F534A:
        raise RuntimeError(f"Expected a leading GLB JSON chunk: {path}")
    json_start = 20
    json_end = json_start + json_length
    gltf = json.loads(bytes(data[json_start:json_end]).decode("utf-8"))
    bin_length, bin_type = struct.unpack_from("<II", data, json_end)
    if bin_type != 0x004E4942:
        raise RuntimeError(f"Expected a trailing GLB BIN chunk: {path}")
    bin_start = json_end + 8
    if bin_start + bin_length != len(data):
        raise RuntimeError(f"Unexpected GLB chunk layout: {path}")

    uv_accessors = {
        primitive["attributes"]["TEXCOORD_0"]
        for mesh in gltf.get("meshes", [])
        for primitive in mesh.get("primitives", [])
        if "TEXCOORD_0" in primitive.get("attributes", {})
    }
    for accessor_index in sorted(uv_accessors):
        accessor = gltf["accessors"][accessor_index]
        if (
            accessor.get("componentType") != 5126
            or accessor.get("type") != "VEC2"
            or "sparse" in accessor
        ):
            raise RuntimeError(
                f"Unsupported TEXCOORD_0 accessor {accessor_index}: {accessor}"
            )
        view = gltf["bufferViews"][accessor["bufferView"]]
        stride = view.get("byteStride", 8)
        start = (
            bin_start
            + view.get("byteOffset", 0)
            + accessor.get("byteOffset", 0)
        )
        for element in range(accessor["count"]):
            for component in range(2):
                offset = start + element * stride + component * 4
                value = struct.unpack_from("<f", data, offset)[0]
                rounded = round(value, 6)
                if abs(rounded) < 0.0000005:
                    rounded = 0.0
                struct.pack_into("<f", data, offset, rounded)
    path.write_bytes(data)


def triangle_count(objects: Iterable[bpy.types.Object]) -> tuple[int, int]:
    vertices = 0
    triangles = 0
    for obj in objects:
        if obj.type != "MESH":
            continue
        obj.data.calc_loop_triangles()
        vertices += len(obj.data.vertices)
        triangles += len(obj.data.loop_triangles)
    return vertices, triangles


def tier_report(
    tier: str,
    armature: bpy.types.Object,
    glb: Path,
    texture_sizes: dict[str, tuple[int, int]],
) -> dict[str, object]:
    objects = model_objects(armature)
    minimum, maximum = collective_bounds(objects)
    vertices, triangles = triangle_count(objects)
    return {
        "tier": tier,
        "glb": {
            "path": glb.name,
            "bytes": glb.stat().st_size,
            "sha256": sha256(glb),
        },
        "geometry": {
            "objects": len(objects) - 1,
            "vertices": vertices,
            "triangles": triangles,
            "boundsBlenderZUp": {
                "min": [round(value, 6) for value in minimum],
                "max": [round(value, 6) for value in maximum],
            },
            "planformSpan": round(
                max(maximum.x - minimum.x, maximum.y - minimum.y), 6
            ),
            "totalHeight": round(maximum.z - minimum.z, 6),
            "sideAspect": round(
                (maximum.x - minimum.x) / (maximum.z - minimum.z), 6
            ),
        },
        "textures": texture_sizes,
        "materials": [
            {
                "name": material.name,
                **(
                    {"nilbotsRole": material["nilbotsRole"]}
                    if "nilbotsRole" in material
                    else {}
                ),
            }
            for material in bpy.data.materials
            if material.users > 0 and not material.name.startswith("REVIEW_")
        ],
    }


def render_tier_review(
    tier_dir: Path,
    armature: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
    actions: dict[str, bpy.types.Action],
    floor: bpy.types.Object,
    camera: bpy.types.Object,
    *,
    animation_frames: bool,
) -> None:
    armature.animation_data.action = actions["walk"]
    bpy.context.scene.frame_set(1)
    set_team_material(materials["team"], TEAM_BLUE)
    render_still(tier_dir / "top.png", camera, floor, view="top")
    render_still(tier_dir / "side.png", camera, floor, view="side")
    render_still(tier_dir / "gameplay-blue.png", camera, floor, view="gameplay")
    set_team_material(materials["team"], TEAM_RED)
    render_still(tier_dir / "gameplay-red.png", camera, floor, view="gameplay")
    if animation_frames:
        set_team_material(materials["team"], TEAM_BLUE)
        for frame in (1, 7, 13, 19):
            bpy.context.scene.frame_set(frame)
            render_still(
                tier_dir / f"walk-frame-{frame:02d}.png",
                camera,
                floor,
                view="gameplay",
            )
            render_still(
                tier_dir / f"walk-side-{frame:02d}.png",
                camera,
                floor,
                view="side",
            )
    bpy.context.scene.frame_set(1)
    set_team_material(materials["team"], TEAM_BLUE)


def main() -> None:
    args = parse_args()
    provider_dir = args.provider_dir.resolve()
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    verify_inputs(provider_dir)
    purge_scene()

    armor = import_and_normalize_armor(provider_dir / "raw-model.glb")
    decimate_to(armor, RICH_ARMOR_TRIANGLES)
    armature, materials, authored = build_authored_parts(armor)
    strip_unused_authored_uvs(authored)
    actions = make_actions(armature)
    floor, camera = configure_scene()

    report: dict[str, object] = {
        "schemaVersion": 1,
        "asset": "aegis-tortoise-mobile",
        "verdictAtBuildTime": "review-candidate-not-runtime",
        "source": {
            "providerTask": "019fb1d5-0c4f-7716-8b86-048ad5d8b066",
            "providerFiles": RAW_FILES,
            "authorityHashes": AUTHORITY_HASHES,
        },
        "recipe": {
            "authoredPlanformSpan": AUTHORED_PLANFORM_SPAN,
            "targetTotalHeight": TARGET_TOTAL_HEIGHT,
            "armorCutZ": ARMOR_CUT_Z,
            "richArmorTriangleTarget": RICH_ARMOR_TRIANGLES,
            "leanArmorTriangleTarget": LEAN_ARMOR_TRIANGLES,
            "nonUniformWarp": False,
            "locomotion": "heavy compact diagonal four-leg gait",
        },
        "rig": {
            "armature": armature.name,
            "bones": [bone.name for bone in armature.data.bones],
            "actions": {
                key: {
                    "name": action.name,
                    "frameRange": [
                        int(action.frame_range[0]),
                        int(action.frame_range[1]),
                    ],
                }
                for key, action in actions.items()
            },
        },
        "tiers": [],
    }

    rich_textures = resize_provider_images("rich")
    render_tier_review(
        output_dir / "review/rich",
        armature,
        materials,
        actions,
        floor,
        camera,
        animation_frames=True,
    )
    rich_glb = output_dir / "aegis-tortoise-rich.glb"
    export_glb(rich_glb, armature)
    bpy.context.scene.frame_set(1)
    set_team_material(materials["team"], TEAM_BLUE)
    select_model(armature)
    bpy.ops.file.pack_all()
    blend_path = output_dir / "aegis-tortoise-editable.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)
    report["tiers"].append(
        tier_report("rich", armature, rich_glb, rich_textures)
    )

    decimate_to(armor, LEAN_ARMOR_TRIANGLES)
    armor.name = "Armor.ProviderMaster.Lean"
    lean_textures = resize_provider_images("lean")
    render_tier_review(
        output_dir / "review/lean",
        armature,
        materials,
        actions,
        floor,
        camera,
        animation_frames=False,
    )
    lean_glb = output_dir / "aegis-tortoise-lean.glb"
    export_glb(lean_glb, armature)
    report["tiers"].append(
        tier_report("lean", armature, lean_glb, lean_textures)
    )

    report["editableBlend"] = {
        "path": blend_path.name,
        "bytes": blend_path.stat().st_size,
        "sha256": sha256(blend_path),
        "state": "rich tier with packed review textures and both actions",
    }
    report_path = output_dir / "build-report.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"AEGIS_BUILD_REPORT={report_path}")


if __name__ == "__main__":
    main()
