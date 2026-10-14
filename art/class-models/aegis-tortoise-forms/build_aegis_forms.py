#!/usr/bin/env python3
"""Build the authored Aegis Tortoise Shell and whole Turret.

The provider GLBs are hashed as visual-reference provenance only. They are
never imported, queried for vertices, or used as topology. Every runtime mesh
is created below from the canonical SVG planforms and authored primitives.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
from pathlib import Path
from typing import Iterable, Sequence

import bmesh
import bpy
from mathutils import Matrix, Vector


TEAM_NEUTRAL = "#ffffff"
TEAM_BLUE = "#22d3ee"
TEAM_RED = "#ef4444"
BLENDER_VERSION = "5.2.0"

REFERENCE_MASTERS = {
    "aegis-tortoise-shell": {
        "taskId": "019fb22d-c313-76f8-8eca-91ccbb46afd0",
        "relative": (
            "aegis-tortoise-shell/"
            "019fb22d-c313-76f8-8eca-91ccbb46afd0/raw-model.glb"
        ),
        "sha256": "8a0a0129cc57afdf12a0c0e70159f2acd547b426501941330e4712d06b78c5ec",
        "bytes": 51_000_780,
    },
    "aegis-tortoise-turret": {
        "taskId": "019fb232-6e09-787f-85cc-dbaea29fb9b6",
        "relative": (
            "aegis-tortoise-turret/"
            "019fb232-6e09-787f-85cc-dbaea29fb9b6/raw-model.glb"
        ),
        "sha256": "a845f48ac9f0861c8ff2860de9c0dc79b0afa728794663f9d8ff70f7187f9cac",
        "bytes": 50_525_092,
    },
}

SHELL_SVG_OUTLINE = (
    (67, 176),
    (124, 111),
    (207, 94),
    (292, 126),
    (373, 94),
    (488, 202),
    (505, 256),
    (488, 310),
    (373, 418),
    (292, 386),
    (207, 418),
    (124, 401),
    (67, 336),
    (88, 285),
    (68, 256),
    (88, 227),
)

TURRET_SVG_OUTLINE = (
    (62, 193),
    (154, 154),
    (193, 62),
    (256, 43),
    (319, 62),
    (358, 154),
    (450, 193),
    (469, 256),
    (450, 319),
    (358, 358),
    (319, 450),
    (256, 469),
    (193, 450),
    (154, 358),
    (62, 319),
    (43, 256),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--artifacts-root", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--family-reference-glb", type=Path)
    return parser.parse_args(_script_args())


def _script_args() -> list[str]:
    import sys

    return sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=False) + "\n")


def verify_reference_masters(artifacts_root: Path) -> dict[str, object]:
    result: dict[str, object] = {}
    for asset_id, expected in REFERENCE_MASTERS.items():
        source = artifacts_root / str(expected["relative"])
        if not source.is_file():
            raise RuntimeError(f"Missing durable reference master: {source}")
        actual_hash = sha256(source)
        actual_bytes = source.stat().st_size
        if actual_hash != expected["sha256"] or actual_bytes != expected["bytes"]:
            raise RuntimeError(
                f"Reference master drift for {asset_id}: "
                f"{actual_hash}/{actual_bytes}"
            )
        result[asset_id] = {
            "taskId": expected["taskId"],
            "sha256": actual_hash,
            "bytes": actual_bytes,
            "usage": (
                "hash-and-human-visual-reference-only; provider geometry was "
                "never imported or sampled"
            ),
        }
    return result


def purge_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.armatures,
        bpy.data.actions,
    ):
        for block in list(collection):
            collection.remove(block)


def srgb(value: str, alpha: float = 1.0) -> tuple[float, float, float, float]:
    text = value.removeprefix("#")
    if len(text) != 6:
        raise ValueError(value)
    return (
        int(text[0:2], 16) / 255,
        int(text[2:4], 16) / 255,
        int(text[4:6], 16) / 255,
        alpha,
    )


def authored_surface_maps(
    label: str,
    path_prefix: Path,
    base_hex: str,
    *,
    wear_hex: str,
    wear_strength: float,
    seed: int,
    size: int,
    base_roughness: float,
    base_metallic: float,
) -> dict[str, bpy.types.Image]:
    """Create deterministic compact albedo, normal, and PBR control maps.

    The broad value grouping stays in the material. This image contributes
    low-amplitude grain, worn panel seams, chips, and fine scratches. The same
    authored height field drives normal and roughness variation, so surface
    breakup responds to arena lighting instead of reading as painted noise.
    """

    base = srgb(base_hex)
    wear = srgb(wear_hex)
    albedo_pixels: list[float] = [0.0] * (size * size * 4)
    control_pixels: list[float] = [0.0] * (size * size * 4)
    height: list[float] = [0.0] * (size * size)

    # A small periodic Voronoi layout gives each projected face irregular
    # armor cells instead of the toy-like square quilting an early pass used.
    # The cells and their wear factors are derived only from integer hashes.
    panel_cells: list[tuple[float, float, float]] = []
    panel_grid = 3
    for cell_y in range(panel_grid):
        for cell_x in range(panel_grid):
            cell_state = (
                (cell_x * 1_668_525)
                ^ (cell_y * 1_013_904_223)
                ^ (seed * 2_246_822_519)
            ) & 0xFFFFFFFF
            cell_state = (
                (cell_state ^ (cell_state >> 15)) * 2_246_822_519
            ) & 0xFFFFFFFF
            jitter_x = ((cell_state >> 8) & 255) / 255 - 0.5
            jitter_y = ((cell_state >> 16) & 255) / 255 - 0.5
            value = ((cell_state >> 24) & 255) / 255 - 0.5
            panel_cells.append(
                (
                    (cell_x + 0.5 + jitter_x * 0.52) / panel_grid,
                    (cell_y + 0.5 + jitter_y * 0.52) / panel_grid,
                    value,
                )
            )

    for y in range(size):
        for x in range(size):
            state = (
                (x * 374_761_393)
                ^ (y * 668_265_263)
                ^ (seed * 2_246_822_519)
            ) & 0xFFFFFFFF
            state = ((state ^ (state >> 13)) * 1_274_126_177) & 0xFFFFFFFF
            grain_unit = ((state >> 9) & 1023) / 1023 - 0.5
            u = (x + 0.5) / size
            v = (y + 0.5) / size
            nearest = (10.0, 0.0)
            second_distance = 10.0
            for cell_u, cell_v, cell_value in panel_cells:
                delta_u = abs(u - cell_u)
                delta_v = abs(v - cell_v)
                delta_u = min(delta_u, 1 - delta_u)
                delta_v = min(delta_v, 1 - delta_v)
                distance = delta_u * delta_u + delta_v * delta_v
                if distance < nearest[0]:
                    second_distance = nearest[0]
                    nearest = (distance, cell_value)
                elif distance < second_distance:
                    second_distance = distance
            edge_gap = math.sqrt(second_distance) - math.sqrt(nearest[0])
            seam = edge_gap < 0.0105
            bevel = 0.0105 <= edge_gap < 0.026
            scratch = (
                ((x * 7 + y * 3 + seed * 17) % 251 < 2)
                and edge_gap > 0.035
            )
            cross_scratch = (
                ((x * 5 - y * 11 + seed * 23) % 431 < 2)
                and edge_gap > 0.045
            )
            chip = edge_gap < 0.032 and ((state >> 19) & 2047) < 20
            macro = (
                math.sin((u * 4.7 + v * 1.3 + seed * 0.11) * math.tau)
                + math.sin((v * 3.1 - u * 0.9 + seed * 0.07) * math.tau)
            ) * 0.018
            multiplier = 1 + grain_unit * 0.10 + nearest[1] * 0.12 + macro
            if seam:
                multiplier *= 0.56
            elif bevel:
                multiplier *= 1.10
            if scratch or cross_scratch:
                multiplier *= 1.24
            if chip:
                multiplier *= 0.62

            # Mobile Aegis carries quiet worn metal at exposed seams rather than
            # one clean hue multiplied lighter and darker. Keep that language
            # restrained: the broad value remains the class paint, while bevels,
            # scratches and chips drift a little toward desaturated bare metal.
            wear_mix = 0.0
            if bevel:
                wear_mix += 0.045
            if scratch or cross_scratch:
                wear_mix += 0.18
            if chip:
                wear_mix += 0.12
            wear_mix = min(wear_mix * wear_strength, 0.24)

            offset = (y * size + x) * 4
            for channel in range(3):
                painted = base[channel] * multiplier
                albedo_pixels[offset + channel] = max(
                    0,
                    min(
                        painted * (1 - wear_mix)
                        + wear[channel] * wear_mix,
                        1,
                    ),
                )
            albedo_pixels[offset + 3] = 1

            relief = grain_unit * 0.085 + macro * 0.8
            if seam:
                relief -= 0.68
            elif bevel:
                relief += 0.18
            if scratch or cross_scratch:
                relief += 0.21
            if chip:
                relief -= 0.34
            height[y * size + x] = relief

            roughness = (
                base_roughness
                + grain_unit * 0.13
                + nearest[1] * 0.10
                - macro * 0.65
            )
            if seam or chip:
                roughness += 0.18
            if scratch or cross_scratch:
                roughness -= 0.13
            metallic = (
                base_metallic + grain_unit * 0.08 + nearest[1] * 0.13
            )
            if chip:
                metallic -= 0.16
            # glTF metallic-roughness convention: G=roughness, B=metallic.
            control_pixels[offset + 0] = 1
            control_pixels[offset + 1] = max(0.18, min(roughness, 0.95))
            control_pixels[offset + 2] = max(0, min(metallic, 1))
            control_pixels[offset + 3] = 1

    normal_pixels: list[float] = [0.0] * (size * size * 4)
    for y in range(size):
        previous_y = (y - 1) % size
        next_y = (y + 1) % size
        for x in range(size):
            previous_x = (x - 1) % size
            next_x = (x + 1) % size
            dx = (
                height[y * size + previous_x]
                - height[y * size + next_x]
            ) * 0.58
            dy = (
                height[previous_y * size + x]
                - height[next_y * size + x]
            ) * 0.58
            length = math.sqrt(dx * dx + dy * dy + 1)
            offset = (y * size + x) * 4
            normal_pixels[offset + 0] = dx / length * 0.5 + 0.5
            normal_pixels[offset + 1] = dy / length * 0.5 + 0.5
            normal_pixels[offset + 2] = 1 / length * 0.5 + 0.5
            normal_pixels[offset + 3] = 1

    def save_image(
        role: str,
        pixels: list[float],
        *,
        non_color: bool,
    ) -> bpy.types.Image:
        image = bpy.data.images.new(
            f"Aegis.{label}.{role}",
            width=size,
            height=size,
            alpha=True,
        )
        # Blender 5.2 clears generated-image pixels when its colour space is
        # changed after assignment. Set the data interpretation first.
        if non_color:
            image.colorspace_settings.name = "Non-Color"
        image.pixels.foreach_set(pixels)
        image.filepath_raw = str(
            path_prefix.with_name(f"{path_prefix.name}-{role.lower()}.png")
        )
        image.file_format = "PNG"
        image.filepath_raw and Path(image.filepath_raw).parent.mkdir(
            parents=True,
            exist_ok=True,
        )
        image.save()
        image.pack()
        return image

    return {
        "albedo": save_image("Albedo", albedo_pixels, non_color=False),
        "normal": save_image("Normal", normal_pixels, non_color=True),
        "metallicRoughness": save_image(
            "MetallicRoughness",
            control_pixels,
            non_color=True,
        ),
    }


def make_material(
    name: str,
    color: str,
    *,
    metallic: float,
    roughness: float,
    maps: dict[str, bpy.types.Image] | None = None,
    emission: str | None = None,
    emission_strength: float = 0,
    role: str | None = None,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = srgb(color)
    material.metallic = metallic
    material.roughness = roughness
    if role is not None:
        material["nilbotsRole"] = role

    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    principled.inputs["Base Color"].default_value = srgb(color)
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    if maps is not None:
        texture = nodes.new("ShaderNodeTexImage")
        texture.name = f"{name}.Albedo"
        texture.image = maps["albedo"]
        links.new(texture.outputs["Color"], principled.inputs["Base Color"])

        control = nodes.new("ShaderNodeTexImage")
        control.name = f"{name}.MetallicRoughness"
        control.image = maps["metallicRoughness"]
        control.image.colorspace_settings.name = "Non-Color"
        separate = nodes.new("ShaderNodeSeparateColor")
        separate.name = f"{name}.PBRChannels"
        links.new(control.outputs["Color"], separate.inputs["Color"])
        links.new(separate.outputs["Green"], principled.inputs["Roughness"])
        links.new(separate.outputs["Blue"], principled.inputs["Metallic"])

        normal_texture = nodes.new("ShaderNodeTexImage")
        normal_texture.name = f"{name}.Normal"
        normal_texture.image = maps["normal"]
        normal_texture.image.colorspace_settings.name = "Non-Color"
        normal = nodes.new("ShaderNodeNormalMap")
        normal.name = f"{name}.NormalBasis"
        normal.inputs["Strength"].default_value = 0.52
        links.new(normal_texture.outputs["Color"], normal.inputs["Color"])
        links.new(normal.outputs["Normal"], principled.inputs["Normal"])
    if emission is not None:
        principled.inputs["Emission Color"].default_value = srgb(emission)
        principled.inputs["Emission Strength"].default_value = emission_strength
    return material


def create_materials(texture_dir: Path) -> dict[str, bpy.types.Material]:
    navy_maps = authored_surface_maps(
        "Navy",
        texture_dir / "aegis-navy",
        "#2c3749",
        wear_hex="#66717f",
        wear_strength=0.78,
        seed=31,
        size=256,
        base_roughness=0.60,
        base_metallic=0.52,
    )
    bronze_maps = authored_surface_maps(
        "Bronze",
        texture_dir / "aegis-bronze",
        "#8f6b43",
        wear_hex="#c0a57d",
        wear_strength=0.82,
        seed=71,
        size=256,
        base_roughness=0.54,
        base_metallic=0.64,
    )
    graphite_maps = authored_surface_maps(
        "Graphite",
        texture_dir / "aegis-graphite",
        "#1b222d",
        wear_hex="#505966",
        wear_strength=0.46,
        seed=113,
        size=128,
        base_roughness=0.55,
        base_metallic=0.66,
    )
    return {
        "navy": make_material(
            "NB_NAVY_ARMOR",
            "#2c3749",
            metallic=0.52,
            roughness=0.60,
            maps=navy_maps,
        ),
        "bronze": make_material(
            "NB_WEATHERED_BRONZE",
            "#8f6b43",
            metallic=0.64,
            roughness=0.54,
            maps=bronze_maps,
        ),
        "graphite": make_material(
            "NB_GRAPHITE_MECHANICAL",
            "#1b222d",
            metallic=0.66,
            roughness=0.55,
            maps=graphite_maps,
        ),
        "dark": make_material(
            "NB_DARK_CONTACT",
            "#090d14",
            metallic=0.05,
            roughness=0.82,
        ),
        "team": make_material(
            "NB_TEAM_ACCENT",
            TEAM_NEUTRAL,
            metallic=0.18,
            roughness=0.36,
            emission=TEAM_NEUTRAL,
            emission_strength=0.72,
            role="team-accent",
        ),
        "energy": make_material(
            "NB_CLASS_ENERGY_CYAN",
            "#1f9bc1",
            metallic=0.12,
            roughness=0.32,
            emission="#12a8ce",
            emission_strength=0.42,
        ),
    }


def active_object(obj: bpy.types.Object) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)


def apply_bevel(obj: bpy.types.Object, width: float, segments: int = 3) -> None:
    if width <= 0:
        return
    modifier = obj.modifiers.new("Authored edge radius", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    active_object(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def add_uvs(mesh: bpy.types.Mesh) -> None:
    layer = mesh.uv_layers.new(name="UVMap")
    coordinates = [vertex.co for vertex in mesh.vertices]
    minimum = Vector(
        tuple(min(vertex[axis] for vertex in coordinates) for axis in range(3))
    )
    maximum = Vector(
        tuple(max(vertex[axis] for vertex in coordinates) for axis in range(3))
    )
    span = Vector(
        tuple(max(maximum[axis] - minimum[axis], 1e-9) for axis in range(3))
    )
    centre = (minimum + maximum) * 0.5
    scale = max(span)
    for polygon in mesh.polygons:
        normal = polygon.normal.normalized()
        # Construct a basis in the face plane rather than choosing two global
        # coordinate axes. Thin bevel triangles can be almost—but not exactly—
        # axis aligned; a dominant-axis switch on those faces may collapse one
        # UV dimension after glTF's coordinate conversion.
        reference = (
            Vector((1, 0, 0))
            if abs(normal.x) < 0.8
            else Vector((0, 1, 0))
        )
        u_axis = normal.cross(reference).normalized()
        v_axis = normal.cross(u_axis).normalized()
        for loop_index in polygon.loop_indices:
            vertex = (
                coordinates[mesh.loops[loop_index].vertex_index] - centre
            )
            layer.data[loop_index].uv = (
                vertex.dot(u_axis) / scale + 0.5,
                vertex.dot(v_axis) / scale + 0.5,
            )


def material_uses_normal_map(material: bpy.types.Material) -> bool:
    return bool(
        material.use_nodes
        and material.node_tree is not None
        and any(node.type == "NORMAL_MAP" for node in material.node_tree.nodes)
    )


def finalize_mesh_uvs(
    obj: bpy.types.Object,
    material: bpy.types.Material,
) -> None:
    """Author UVs on final post-bevel topology only where PBR needs them.

    Blender propagates pre-bevel UVs onto new bevel loops imperfectly, which
    can yield zero-length exported tangents. Reprojecting the final faces gives
    every normal-mapped triangle a valid basis. Flat materials do not need UVs;
    removing them also prevents unused tangent attributes on team/core/contact
    meshes.
    """

    while obj.data.uv_layers:
        obj.data.uv_layers.remove(obj.data.uv_layers[0])
    editable = bmesh.new()
    editable.from_mesh(obj.data)
    bmesh.ops.triangulate(
        editable,
        faces=list(editable.faces),
        quad_method="BEAUTY",
        ngon_method="BEAUTY",
    )
    editable.to_mesh(obj.data)
    editable.free()
    if material_uses_normal_map(material):
        add_uvs(obj.data)
    obj.data.validate(clean_customdata=False)
    obj.data.update()


def add_prism(
    name: str,
    points: Sequence[tuple[float, float]],
    bottom: float,
    top: float,
    material: bpy.types.Material,
    *,
    bevel: float = 0,
) -> bpy.types.Object:
    count = len(points)
    vertices = [(x, y, bottom) for x, y in points] + [
        (x, y, top) for x, y in points
    ]
    faces: list[tuple[int, ...]] = [
        tuple(reversed(range(count))),
        tuple(range(count, count * 2)),
    ]
    for index in range(count):
        following = (index + 1) % count
        faces.append((index, following, following + count, index + count))
    mesh = bpy.data.meshes.new(f"{name}.Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    apply_bevel(obj, bevel)
    finalize_mesh_uvs(obj, material)
    return obj


def add_box(
    name: str,
    dimensions: tuple[float, float, float],
    location: tuple[float, float, float],
    material: bpy.types.Material,
    *,
    bevel: float = 0,
    rotation_z: float = 0,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.dimensions = dimensions
    obj.rotation_euler.z = rotation_z
    active_object(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    apply_bevel(obj, bevel)
    finalize_mesh_uvs(obj, material)
    return obj


def add_cylinder(
    name: str,
    radius: float,
    depth: float,
    location: tuple[float, float, float],
    material: bpy.types.Material,
    *,
    axis: str = "Z",
    vertices: int = 16,
    bevel: float = 0,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
    )
    obj = bpy.context.active_object
    obj.name = name
    if axis == "X":
        obj.rotation_euler.y = math.pi / 2
    elif axis == "Y":
        obj.rotation_euler.x = math.pi / 2
    elif axis != "Z":
        raise ValueError(axis)
    active_object(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=False)
    obj.data.materials.append(material)
    apply_bevel(obj, bevel)
    finalize_mesh_uvs(obj, material)
    return obj


def add_torus(
    name: str,
    major_radius: float,
    minor_radius: float,
    height: float,
    material: bpy.types.Material,
    *,
    centre: tuple[float, float] = (0, 0),
    major_segments: int = 16,
    minor_segments: int = 6,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major_radius,
        minor_radius=minor_radius,
        major_segments=major_segments,
        minor_segments=minor_segments,
        location=(centre[0], centre[1], height),
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(material)
    finalize_mesh_uvs(obj, material)
    return obj


def mark_part(
    obj: bpy.types.Object,
    part: str,
    *,
    role: str | None = None,
) -> bpy.types.Object:
    obj["nilbotsPart"] = part
    if role is not None:
        obj["nilbotsRole"] = role
    return obj


def parent_to_bone(
    obj: bpy.types.Object,
    armature: bpy.types.Object,
    bone_name: str,
) -> None:
    world = obj.matrix_world.copy()
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = world
    obj["nilbotsParentPivot"] = bone_name


def svg_planform(
    source: Sequence[tuple[float, float]],
) -> tuple[tuple[float, float], ...]:
    minimum_x = min(point[0] for point in source)
    maximum_x = max(point[0] for point in source)
    minimum_y = min(point[1] for point in source)
    maximum_y = max(point[1] for point in source)
    centre_x = (minimum_x + maximum_x) / 2
    centre_y = (minimum_y + maximum_y) / 2
    scale = max(maximum_x - minimum_x, maximum_y - minimum_y)
    return tuple(
        ((x - centre_x) / scale, -(y - centre_y) / scale) for x, y in source
    )


def svg_subset(
    source: Sequence[tuple[float, float]],
    outer: Sequence[tuple[float, float]],
) -> tuple[tuple[float, float], ...]:
    minimum_x = min(point[0] for point in outer)
    maximum_x = max(point[0] for point in outer)
    minimum_y = min(point[1] for point in outer)
    maximum_y = max(point[1] for point in outer)
    centre_x = (minimum_x + maximum_x) / 2
    centre_y = (minimum_y + maximum_y) / 2
    scale = max(maximum_x - minimum_x, maximum_y - minimum_y)
    return tuple(
        ((x - centre_x) / scale, -(y - centre_y) / scale) for x, y in source
    )


def scaled_points(
    points: Sequence[tuple[float, float]],
    scale: float,
    *,
    centre: tuple[float, float] = (0, 0),
) -> tuple[tuple[float, float], ...]:
    return tuple(
        (
            centre[0] + (x - centre[0]) * scale,
            centre[1] + (y - centre[1]) * scale,
        )
        for x, y in points
    )


def create_armature(
    name: str,
    bones: Sequence[
        tuple[
            str,
            tuple[float, float, float],
            tuple[float, float, float],
            str | None,
        ]
    ],
) -> bpy.types.Object:
    data = bpy.data.armatures.new(name)
    armature = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(armature)
    active_object(armature)
    bpy.ops.object.mode_set(mode="EDIT")
    created: dict[str, bpy.types.EditBone] = {}
    for bone_name, head, tail, parent_name in bones:
        bone = data.edit_bones.new(bone_name)
        bone.head = head
        bone.tail = tail
        if parent_name is not None:
            bone.parent = created[parent_name]
        created[bone_name] = bone
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    armature["nilbotsFacing"] = "+X"
    armature["nilbotsUp"] = "+Y after glTF export"
    armature["nilbotsAnimationContract"] = (
        "in-place authored actions; a replay renderer must seek normalized clip time"
    )
    return armature


def shell_armature(core_x: float) -> bpy.types.Object:
    bones = (
        ("root", (0, 0, 0), (0, 0, 0.06), None),
        ("hub", (core_x, 0, 0.08), (core_x, 0, 0.22), "root"),
        ("guard", (core_x, 0, 0.10), (core_x + 0.16, 0, 0.10), "root"),
        (
            "support_north",
            (-0.36, 0.25, 0.02),
            (-0.36, 0.25, 0.12),
            "root",
        ),
        (
            "support_west",
            (-0.43, 0, 0.02),
            (-0.43, 0, 0.12),
            "root",
        ),
        (
            "support_south",
            (-0.36, -0.25, 0.02),
            (-0.36, -0.25, 0.12),
            "root",
        ),
    )
    return create_armature("Aegis.Shell.Rig", bones)


def turret_armature() -> bpy.types.Object:
    bones: list[
        tuple[
            str,
            tuple[float, float, float],
            tuple[float, float, float],
            str | None,
        ]
    ] = [
        ("root", (0, 0, 0), (0, 0, 0.06), None),
        ("hub", (0, 0, 0.07), (0, 0, 0.22), "root"),
    ]
    directions = {
        "east": (1.0, 0.0),
        "north": (0.0, 1.0),
        "west": (-1.0, 0.0),
        "south": (0.0, -1.0),
    }
    for direction, (x, y) in directions.items():
        bones.extend(
            [
                (
                    f"arm_{direction}",
                    (0, 0, 0.08),
                    (0, 0, 0.18),
                    "root",
                ),
                (
                    f"support_{direction}",
                    (x * 0.39, y * 0.39, 0.02),
                    (x * 0.39, y * 0.39, 0.10),
                    f"arm_{direction}",
                ),
                (
                    f"emitter_{direction}",
                    (x * 0.47, y * 0.47, 0.15),
                    (x * 0.50, y * 0.50, 0.15),
                    f"arm_{direction}",
                ),
            ]
        )
    return create_armature("Aegis.Turret.Rig", bones)


def shell_support(
    label: str,
    pivot: tuple[float, float],
    armature: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> list[bpy.types.Object]:
    x, y = pivot
    bone_name = f"support_{label}"
    foot = mark_part(
        add_box(
            f"Shell.Support.{label.title()}.Foot",
            (0.105, 0.105, 0.032),
            (x, y, 0.018),
            materials["dark"],
            bevel=0.010,
        ),
        f"planted-support-{label}",
    )
    lock = mark_part(
        add_cylinder(
            f"Shell.Support.{label.title()}.Lock",
            0.038,
            0.065,
            (x, y, 0.064),
            materials["bronze"],
            vertices=12,
            bevel=0.004,
        ),
        f"planted-support-{label}",
    )
    housing = mark_part(
        add_box(
            f"Shell.Support.{label.title()}.Housing",
            (0.095, 0.085, 0.072),
            (x, y, 0.115),
            materials["graphite"],
            bevel=0.012,
        ),
        f"planted-support-{label}",
    )
    cap = mark_part(
        add_cylinder(
            f"Shell.Support.{label.title()}.ArmorCap",
            0.048,
            0.030,
            (x, y, 0.158),
            materials["navy"],
            vertices=12,
            bevel=0.005,
        ),
        f"planted-support-{label}",
    )
    fork_a = mark_part(
        add_box(
            f"Shell.Support.{label.title()}.ForkA",
            (0.072, 0.016, 0.052),
            (x + 0.032, y + 0.030, 0.098),
            materials["bronze"],
            bevel=0.004,
        ),
        f"planted-support-{label}",
    )
    fork_b = mark_part(
        add_box(
            f"Shell.Support.{label.title()}.ForkB",
            (0.072, 0.016, 0.052),
            (x + 0.032, y - 0.030, 0.098),
            materials["bronze"],
            bevel=0.004,
        ),
        f"planted-support-{label}",
    )
    for obj in (foot, lock, housing, cap, fork_a, fork_b):
        parent_to_bone(obj, armature, bone_name)
    return [foot, lock, housing, cap, fork_a, fork_b]


def build_shell(
    materials: dict[str, bpy.types.Material],
) -> tuple[bpy.types.Object, dict[str, bpy.types.Action], dict[str, object]]:
    outer = svg_planform(SHELL_SVG_OUTLINE)
    core_x = (252 - (67 + 505) / 2) / (505 - 67)
    armature = shell_armature(core_x)
    authored: list[bpy.types.Object] = []

    base = mark_part(
        add_prism(
            "Shell.Root.Underbody",
            outer,
            0.045,
            0.107,
            materials["graphite"],
            bevel=0.010,
        ),
        "shell-underbody",
    )
    parent_to_bone(base, armature, "root")
    authored.append(base)

    lower = mark_part(
        add_prism(
            "Shell.Root.LowerArmor",
            scaled_points(outer, 0.93),
            0.098,
            0.150,
            materials["navy"],
            bevel=0.010,
        ),
        "shell-lower-armor",
    )
    parent_to_bone(lower, armature, "root")
    authored.append(lower)

    north_panel = svg_subset(
        (
            (82, 181),
            (131, 126),
            (203, 110),
            (287, 142),
            (336, 199),
            (285, 245),
            (192, 230),
            (126, 251),
            (78, 224),
        ),
        SHELL_SVG_OUTLINE,
    )
    south_panel = svg_subset(
        (
            (82, 331),
            (131, 386),
            (203, 402),
            (287, 370),
            (336, 313),
            (285, 267),
            (192, 282),
            (126, 261),
            (78, 288),
        ),
        SHELL_SVG_OUTLINE,
    )
    west_panel = svg_subset(
        (
            (101, 220),
            (192, 191),
            (293, 256),
            (192, 321),
            (101, 292),
            (78, 256),
        ),
        SHELL_SVG_OUTLINE,
    )
    for label, points in (
        ("North", north_panel),
        ("South", south_panel),
        ("West", west_panel),
    ):
        plate = mark_part(
            add_prism(
                f"Shell.Hub.OpenArmor.{label}",
                scaled_points(points, 0.95, centre=(core_x, 0)),
                0.105,
                0.184,
                materials["navy"],
                bevel=0.010,
            ),
            "open-quadrant-armor",
        )
        parent_to_bone(plate, armature, "hub")
        authored.append(plate)
        inset = mark_part(
            add_prism(
                f"Shell.Hub.OpenArmor.{label}.Inset",
                scaled_points(points, 0.83, centre=(core_x, 0)),
                0.178,
                0.207,
                materials["navy"],
                bevel=0.007,
            ),
            "open-quadrant-inset-armor",
        )
        parent_to_bone(inset, armature, "hub")
        authored.append(inset)

    guard_outer = svg_subset(
        (
            (256, 256),
            (376, 108),
            (472, 198),
            (501, 256),
            (472, 314),
            (376, 404),
        ),
        SHELL_SVG_OUTLINE,
    )
    guard_inner = svg_subset(
        (
            (292, 256),
            (386, 139),
            (450, 203),
            (478, 256),
            (450, 309),
            (386, 373),
        ),
        SHELL_SVG_OUTLINE,
    )
    guard_recess = svg_subset(
        (
            (319, 256),
            (397, 174),
            (438, 215),
            (459, 256),
            (438, 297),
            (397, 338),
        ),
        SHELL_SVG_OUTLINE,
    )
    for name, points, bottom, top, material, part in (
        (
            "Shell.Guard.BronzeFrame",
            guard_outer,
            0.105,
            0.242,
            materials["bronze"],
            "directional-guard-frame",
        ),
        (
            "Shell.Guard.NavyFace",
            guard_inner,
            0.225,
            0.275,
            materials["navy"],
            "directional-guard-face",
        ),
        (
            "Shell.Guard.Recess",
            guard_recess,
            0.270,
            0.288,
            materials["navy"],
            "directional-guard-recess",
        ),
    ):
        obj = mark_part(
            add_prism(name, points, bottom, top, material, bevel=0.008),
            part,
        )
        parent_to_bone(obj, armature, "guard")
        authored.append(obj)

    hub_parts = (
        mark_part(
            add_cylinder(
                "Shell.Hub.LowerRing",
                0.190,
                0.078,
                (core_x, 0, 0.150),
                materials["graphite"],
                vertices=16,
                bevel=0.006,
            ),
            "concentric-hub",
        ),
        mark_part(
            add_cylinder(
                "Shell.Hub.Armor",
                0.150,
                0.096,
                (core_x, 0, 0.205),
                materials["navy"],
                vertices=8,
                bevel=0.007,
            ),
            "concentric-hub",
        ),
        mark_part(
            add_torus(
                "Shell.Hub.BronzeRing",
                0.099,
                0.016,
                0.258,
                materials["bronze"],
                centre=(core_x, 0),
                major_segments=16,
            ),
            "concentric-hub",
        ),
        mark_part(
            add_torus(
                "Shell.Hub.GraphiteSeal",
                0.077,
                0.010,
                0.270,
                materials["graphite"],
                centre=(core_x, 0),
                major_segments=20,
                minor_segments=8,
            ),
            "concentric-hub",
        ),
        mark_part(
            add_cylinder(
                "Shell.Hub.CoreSocket",
                0.060,
                0.036,
                (core_x, 0, 0.280),
                materials["graphite"],
                vertices=16,
                bevel=0.004,
            ),
            "class-energy-core",
        ),
        mark_part(
            add_torus(
                "Shell.Hub.CoreBronzeBezel",
                0.046,
                0.007,
                0.299,
                materials["bronze"],
                centre=(core_x, 0),
                major_segments=16,
                minor_segments=6,
            ),
            "class-energy-core",
        ),
        mark_part(
            add_cylinder(
                "Shell.Hub.ClassEnergy",
                0.035,
                0.016,
                (core_x, 0, 0.304),
                materials["energy"],
                vertices=16,
                bevel=0.003,
            ),
            "class-energy-core",
        ),
        mark_part(
            add_torus(
                "Shell.Hub.CoreInnerSeal",
                0.022,
                0.004,
                0.312,
                materials["graphite"],
                centre=(core_x, 0),
                major_segments=16,
                minor_segments=6,
            ),
            "class-energy-core",
        ),
        mark_part(
            add_cylinder(
                "Shell.Hub.ClassEnergy.Inner",
                0.015,
                0.006,
                (core_x, 0, 0.314),
                materials["energy"],
                vertices=16,
                bevel=0.002,
            ),
            "class-energy-core",
        ),
    )
    for obj in hub_parts:
        parent_to_bone(obj, armature, "hub")
        authored.append(obj)

    for label, location, rotation in (
        ("North", (core_x - 0.040, 0.132, 0.270), math.radians(17)),
        ("West", (core_x - 0.138, 0, 0.270), 0.0),
        ("South", (core_x - 0.040, -0.132, 0.270), -math.radians(17)),
    ):
        key = mark_part(
            add_box(
                f"Shell.Hub.BronzeKey.{label}",
                (0.052, 0.030, 0.018),
                location,
                materials["bronze"],
                bevel=0.005,
                rotation_z=rotation,
            ),
            "concentric-hub-segment",
        )
        parent_to_bone(key, armature, "hub")
        authored.append(key)

    # Sparse physical breakup on the largest rear plates. These sit wholly
    # inside the canonical planform and share the hub bone, so they add the
    # mobile family's panel/fastener language without changing silhouette or
    # animation topology.
    for label, location, rotation in (
        ("North", (-0.285, 0.225, 0.213), math.radians(-12)),
        ("South", (-0.285, -0.225, 0.213), math.radians(12)),
    ):
        band = mark_part(
            add_box(
                f"Shell.Hub.RearBand.{label}",
                (0.165, 0.016, 0.010),
                location,
                materials["graphite"],
                bevel=0.003,
                rotation_z=rotation,
            ),
            "rear-secondary-graphite-band",
        )
        parent_to_bone(band, armature, "hub")
        authored.append(band)
        for index, offset in enumerate((-0.064, 0.064), start=1):
            fastener = mark_part(
                add_cylinder(
                    f"Shell.Hub.RearBand.{label}.Fastener.{index:02d}",
                    0.006,
                    0.007,
                    (
                        location[0] + math.cos(rotation) * offset,
                        location[1] + math.sin(rotation) * offset,
                        0.221,
                    ),
                    materials["bronze"],
                    vertices=10,
                    bevel=0.002,
                ),
                "rear-secondary-fastener",
            )
            parent_to_bone(fastener, armature, "hub")
            authored.append(fastener)

    support_pivots = {
        "north": (-0.365, 0.245),
        "west": (-0.430, 0.0),
        "south": (-0.365, -0.245),
    }
    for label, pivot in support_pivots.items():
        authored.extend(shell_support(label, pivot, armature, materials))
        pipe = mark_part(
            add_cylinder(
                f"Shell.Support.{label.title()}.Piston",
                0.018,
                0.165 if label == "west" else 0.135,
                (
                    pivot[0] + (0.070 if label == "west" else 0.045),
                    pivot[1],
                    0.105,
                ),
                materials["bronze"],
                axis="X",
                vertices=10,
                bevel=0.003,
            ),
            f"planted-support-{label}",
        )
        parent_to_bone(pipe, armature, f"support_{label}")
        authored.append(pipe)

    team_parts = (
        (
            "Shell.TeamInlay.GuardNorth",
            (0.145, 0.017, 0.009),
            (0.165, 0.222, 0.294),
            math.radians(51),
            "guard",
        ),
        (
            "Shell.TeamInlay.GuardSouth",
            (0.145, 0.017, 0.009),
            (0.165, -0.222, 0.294),
            -math.radians(51),
            "guard",
        ),
        (
            "Shell.TeamInlay.WestCentre",
            (0.055, 0.030, 0.009),
            (-0.425, 0, 0.164),
            0,
            "support_west",
        ),
    )
    team_nodes: list[str] = []
    for name, dimensions, location, rotation, bone in team_parts:
        channel = mark_part(
            add_box(
                name.replace("TeamInlay", "TeamChannel"),
                (dimensions[0] + 0.024, dimensions[1] + 0.014, 0.007),
                (location[0], location[1], location[2] - 0.0045),
                materials["graphite"],
                bevel=0.004,
                rotation_z=rotation,
            ),
            "bounded-team-channel",
        )
        parent_to_bone(channel, armature, bone)
        authored.append(channel)
        inlay = mark_part(
            add_box(
                name,
                dimensions,
                location,
                materials["team"],
                bevel=0.005,
                rotation_z=rotation,
            ),
            "semantic-team-inlay",
            role="team-accent",
        )
        parent_to_bone(inlay, armature, bone)
        authored.append(inlay)
        team_nodes.append(name)

    actions = make_shell_actions(armature)
    return armature, actions, {
        "assetId": "aegis-tortoise-shell",
        "authoredObjects": authored,
        "teamNodes": team_nodes,
        "planformAuthority": "exact SVG outer path",
        "targetHeight": 0.31,
    }


def radial_copy(
    source: bpy.types.Object,
    name: str,
    angle: float,
) -> bpy.types.Object:
    duplicate = source.copy()
    duplicate.data = source.data
    duplicate.name = name
    bpy.context.collection.objects.link(duplicate)
    duplicate.matrix_world = Matrix.Rotation(angle, 4, "Z") @ source.matrix_world
    return duplicate


def build_turret(
    materials: dict[str, bpy.types.Material],
) -> tuple[bpy.types.Object, dict[str, bpy.types.Action], dict[str, object]]:
    armature = turret_armature()
    authored: list[bpy.types.Object] = []
    team_nodes: list[str] = []
    direction_angles = {
        "east": 0.0,
        "north": math.pi / 2,
        "west": math.pi,
        "south": -math.pi / 2,
    }

    # One exact East sector of the SVG star. Four rotations reconstruct the
    # complete canonical outer path without a provider-derived vertex.
    arm_sector = svg_subset(
        (
            (256, 256),
            (358, 154),
            (450, 193),
            (469, 256),
            (450, 319),
            (358, 358),
        ),
        TURRET_SVG_OUTLINE,
    )
    canonical: list[tuple[bpy.types.Object, str]] = []
    canonical.append(
        (
            mark_part(
                add_prism(
                    "Turret.Arm.East.OuterBase",
                    arm_sector,
                    0.045,
                    0.105,
                    materials["graphite"],
                    bevel=0.009,
                ),
                "canonical-cardinal-arm",
            ),
            "arm",
        )
    )
    canonical.append(
        (
            mark_part(
                add_prism(
                    "Turret.Arm.East.InnerArmor",
                    scaled_points(arm_sector, 0.72),
                    0.145,
                    0.178,
                    materials["graphite"],
                    bevel=0.007,
                ),
                "canonical-cardinal-arm-detail",
            ),
            "arm",
        )
    )
    canonical.append(
        (
            mark_part(
                add_prism(
                    "Turret.Arm.East.NavyShoulder",
                    scaled_points(arm_sector, 0.91),
                    0.095,
                    0.150,
                    materials["navy"],
                    bevel=0.009,
                ),
                "canonical-cardinal-arm",
            ),
            "arm",
        )
    )

    pod_points = (
        (0.145, -0.105),
        (0.360, -0.115),
        (0.465, -0.072),
        (0.490, 0),
        (0.465, 0.072),
        (0.360, 0.115),
        (0.145, 0.105),
        (0.105, 0),
    )
    bronze_points = (
        (0.190, -0.070),
        (0.365, -0.074),
        (0.435, -0.045),
        (0.452, 0),
        (0.435, 0.045),
        (0.365, 0.074),
        (0.190, 0.070),
        (0.160, 0),
    )
    canonical.extend(
        [
            (
                mark_part(
                    add_prism(
                        "Turret.Arm.East.Pod",
                        pod_points,
                        0.105,
                        0.185,
                        materials["navy"],
                        bevel=0.010,
                    ),
                    "canonical-cardinal-arm",
                ),
                "arm",
            ),
            (
                mark_part(
                    add_prism(
                        "Turret.Arm.East.BronzeFace",
                        bronze_points,
                        0.177,
                        0.216,
                        materials["bronze"],
                        bevel=0.007,
                    ),
                    "canonical-cardinal-arm",
                ),
                "arm",
            ),
            (
                mark_part(
                    add_box(
                        "Turret.Arm.East.TeamChannel",
                        (0.179, 0.032, 0.007),
                        (0.315, 0, 0.2175),
                        materials["graphite"],
                        bevel=0.004,
                    ),
                    "bounded-team-channel",
                ),
                "arm",
            ),
            (
                mark_part(
                    add_box(
                        "Turret.Arm.East.TeamInlay",
                        (0.155, 0.018, 0.009),
                        (0.315, 0, 0.222),
                        materials["team"],
                        bevel=0.004,
                    ),
                    "semantic-team-inlay",
                    role="team-accent",
                ),
                "arm",
            ),
            (
                mark_part(
                    add_box(
                        "Turret.Arm.East.Underbeam",
                        (0.285, 0.070, 0.046),
                        (0.285, 0, 0.090),
                        materials["graphite"],
                        bevel=0.010,
                    ),
                    "under-arm-mechanics",
                ),
                "arm",
            ),
            (
                mark_part(
                    add_cylinder(
                        "Turret.Arm.East.PistonNorth",
                        0.016,
                        0.225,
                        (0.280, 0.052, 0.094),
                        materials["bronze"],
                        axis="X",
                        vertices=10,
                        bevel=0.003,
                    ),
                    "under-arm-mechanics",
                ),
                "arm",
            ),
            (
                mark_part(
                    add_cylinder(
                        "Turret.Arm.East.PistonSouth",
                        0.016,
                        0.225,
                        (0.280, -0.052, 0.094),
                        materials["bronze"],
                        axis="X",
                        vertices=10,
                        bevel=0.003,
                    ),
                    "under-arm-mechanics",
                ),
                "arm",
            ),
            (
                mark_part(
                    add_box(
                        "Turret.Arm.East.PodRailNorth",
                        (0.172, 0.007, 0.006),
                        (0.318, 0.058, 0.218),
                        materials["graphite"],
                        bevel=0.002,
                    ),
                    "canonical-cardinal-arm-detail",
                ),
                "arm",
            ),
            (
                mark_part(
                    add_box(
                        "Turret.Arm.East.PodRailSouth",
                        (0.172, 0.007, 0.006),
                        (0.318, -0.058, 0.218),
                        materials["graphite"],
                        bevel=0.002,
                    ),
                    "canonical-cardinal-arm-detail",
                ),
                "arm",
            ),
            (
                mark_part(
                    add_cylinder(
                        "Turret.Arm.East.HingeNorth",
                        0.016,
                        0.030,
                        (0.160, 0.100, 0.160),
                        materials["graphite"],
                        axis="Y",
                        vertices=12,
                        bevel=0.003,
                    ),
                    "canonical-cardinal-arm-detail",
                ),
                "arm",
            ),
            (
                mark_part(
                    add_cylinder(
                        "Turret.Arm.East.HingeSouth",
                        0.016,
                        0.030,
                        (0.160, -0.100, 0.160),
                        materials["graphite"],
                        axis="Y",
                        vertices=12,
                        bevel=0.003,
                    ),
                    "canonical-cardinal-arm-detail",
                ),
                "arm",
            ),
            (
                mark_part(
                    add_box(
                        "Turret.Arm.East.OuterLock",
                        (0.060, 0.095, 0.052),
                        (0.448, 0, 0.121),
                        materials["graphite"],
                        bevel=0.009,
                    ),
                    "canonical-cardinal-arm-detail",
                ),
                "arm",
            ),
        ]
    )

    # A pair of secondary graphite straps and exposed bolts break the broad arm
    # roots at game size. They are authored once on East and remain exact
    # shared-mesh 90-degree instances with every other radial component.
    for label, y, angle in (
        ("North", 0.104, math.radians(34)),
        ("South", -0.104, math.radians(-34)),
    ):
        canonical.extend(
            [
                (
                    mark_part(
                        add_box(
                            f"Turret.Arm.East.RootBand{label}",
                            (0.118, 0.015, 0.010),
                            (0.172, y, 0.184),
                            materials["graphite"],
                            bevel=0.003,
                            rotation_z=angle,
                        ),
                        "canonical-root-graphite-band",
                    ),
                    "arm",
                ),
                (
                    mark_part(
                        add_cylinder(
                            f"Turret.Arm.East.RootFastener{label}",
                            0.006,
                            0.007,
                            (0.205, y, 0.192),
                            materials["bronze"],
                            vertices=10,
                            bevel=0.002,
                        ),
                        "canonical-root-fastener",
                    ),
                    "arm",
                ),
            ]
        )

    for index, (x, y) in enumerate(
        (
            (0.205, 0.050),
            (0.205, -0.050),
            (0.415, 0.036),
            (0.415, -0.036),
        ),
        start=1,
    ):
        canonical.append(
            (
                mark_part(
                    add_cylinder(
                        f"Turret.Arm.East.Fastener.{index:02d}",
                        0.005,
                        0.006,
                        (x, y, 0.219),
                        materials["graphite"],
                        vertices=10,
                        bevel=0.002,
                    ),
                    "canonical-cardinal-arm-detail",
                ),
                "arm",
            )
        )

    # Two equal halves on every sector meet the adjacent sector at the
    # diagonals, yielding exactly four shoulder webs under exact 90° repeats.
    web_north = (
        (0.075, 0.070),
        (0.235, 0.225),
        (0.300, 0.175),
        (0.135, 0.050),
    )
    web_south = tuple((x, -y) for x, y in reversed(web_north))
    canonical.extend(
        [
            (
                mark_part(
                    add_prism(
                        "Turret.Arm.East.WebNorth",
                        web_north,
                        0.122,
                        0.168,
                        materials["navy"],
                        bevel=0.006,
                    ),
                    "diagonal-shoulder-web-half",
                ),
                "arm",
            ),
            (
                mark_part(
                    add_prism(
                        "Turret.Arm.East.WebSouth",
                        web_south,
                        0.122,
                        0.168,
                        materials["navy"],
                        bevel=0.006,
                    ),
                    "diagonal-shoulder-web-half",
                ),
                "arm",
            ),
            (
                mark_part(
                    add_box(
                        "Turret.Support.East.Foot",
                        (0.112, 0.112, 0.032),
                        (0.395, 0, 0.018),
                        materials["dark"],
                        bevel=0.010,
                    ),
                    "planted-support",
                ),
                "support",
            ),
            (
                mark_part(
                    add_cylinder(
                        "Turret.Support.East.Lock",
                        0.040,
                        0.072,
                        (0.395, 0, 0.068),
                        materials["bronze"],
                        vertices=12,
                        bevel=0.004,
                    ),
                    "planted-support",
                ),
                "support",
            ),
            (
                mark_part(
                    add_box(
                        "Turret.Support.East.Housing",
                        (0.088, 0.082, 0.060),
                        (0.395, 0, 0.115),
                        materials["graphite"],
                        bevel=0.010,
                    ),
                    "planted-support",
                ),
                "support",
            ),
            (
                mark_part(
                    add_box(
                        "Turret.Support.East.ForkNorth",
                        (0.090, 0.018, 0.054),
                        (0.372, 0.043, 0.085),
                        materials["bronze"],
                        bevel=0.004,
                    ),
                    "planted-support-detail",
                ),
                "support",
            ),
            (
                mark_part(
                    add_box(
                        "Turret.Support.East.ForkSouth",
                        (0.090, 0.018, 0.054),
                        (0.372, -0.043, 0.085),
                        materials["bronze"],
                        bevel=0.004,
                    ),
                    "planted-support-detail",
                ),
                "support",
            ),
        ]
    )

    radial_groups: dict[str, list[str]] = {}
    for source, kind in canonical:
        base = source.name.replace(".East.", ".{direction}.")
        radial_groups[source.name] = []
        for direction, angle in direction_angles.items():
            label = direction.title()
            if direction == "east":
                obj = source
            else:
                obj = radial_copy(source, base.format(direction=label), angle)
            radial_groups[source.name].append(obj.name)
            bone = (
                f"support_{direction}" if kind == "support" else f"arm_{direction}"
            )
            parent_to_bone(obj, armature, bone)
            authored.append(obj)
            if obj.get("nilbotsRole") == "team-accent":
                team_nodes.append(obj.name)

    hub_parts = (
        mark_part(
            add_cylinder(
                "Turret.Hub.Lower",
                0.185,
                0.085,
                (0, 0, 0.088),
                materials["graphite"],
                vertices=16,
                bevel=0.007,
            ),
            "singular-hub",
        ),
        mark_part(
            add_cylinder(
                "Turret.Hub.Armor",
                0.145,
                0.096,
                (0, 0, 0.160),
                materials["navy"],
                vertices=8,
                bevel=0.007,
            ),
            "singular-hub",
        ),
        mark_part(
            add_torus(
                "Turret.Hub.BronzeRing",
                0.090,
                0.016,
                0.214,
                materials["bronze"],
                major_segments=16,
            ),
            "singular-hub",
        ),
        mark_part(
            add_torus(
                "Turret.Hub.GraphiteSeal",
                0.072,
                0.010,
                0.226,
                materials["graphite"],
                major_segments=20,
                minor_segments=8,
            ),
            "singular-hub",
        ),
        mark_part(
            add_cylinder(
                "Turret.Hub.CoreSocket",
                0.057,
                0.032,
                (0, 0, 0.232),
                materials["graphite"],
                vertices=16,
                bevel=0.004,
            ),
            "class-energy-core",
        ),
        mark_part(
            add_torus(
                "Turret.Hub.CoreBronzeBezel",
                0.044,
                0.007,
                0.249,
                materials["bronze"],
                major_segments=16,
                minor_segments=6,
            ),
            "class-energy-core",
        ),
        mark_part(
            add_cylinder(
                "Turret.Hub.ClassEnergy",
                0.033,
                0.015,
                (0, 0, 0.254),
                materials["energy"],
                vertices=16,
                bevel=0.003,
            ),
            "class-energy-core",
        ),
        mark_part(
            add_torus(
                "Turret.Hub.CoreInnerSeal",
                0.021,
                0.004,
                0.262,
                materials["graphite"],
                major_segments=16,
                minor_segments=6,
            ),
            "class-energy-core",
        ),
        mark_part(
            add_cylinder(
                "Turret.Hub.ClassEnergy.Inner",
                0.014,
                0.006,
                (0, 0, 0.264),
                materials["energy"],
                vertices=16,
                bevel=0.002,
            ),
            "class-energy-core",
        ),
    )
    for obj in hub_parts:
        parent_to_bone(obj, armature, "hub")
        authored.append(obj)

    for index, angle in enumerate(
        (0.0, math.pi / 2, math.pi, -math.pi / 2),
        start=1,
    ):
        bolt = mark_part(
            add_cylinder(
                f"Turret.Hub.Fastener.{index:02d}",
                0.006,
                0.008,
                (
                    math.cos(angle) * 0.120,
                    math.sin(angle) * 0.120,
                    0.211,
                ),
                materials["graphite"],
                vertices=10,
                bevel=0.002,
            ),
            "family-detail-fastener",
        )
        parent_to_bone(bolt, armature, "hub")
        authored.append(bolt)

    actions = make_turret_actions(armature)
    shared_groups = {
        source_name: {
            "nodes": names,
            "sharedMesh": len(
                {
                    bpy.data.objects[name].data.as_pointer()
                    for name in names
                    if bpy.data.objects[name].type == "MESH"
                }
            )
            == 1,
        }
        for source_name, names in radial_groups.items()
    }
    if not all(group["sharedMesh"] for group in shared_groups.values()):
        raise RuntimeError("Turret radial copies do not share canonical mesh data")
    return armature, actions, {
        "assetId": "aegis-tortoise-turret",
        "authoredObjects": authored,
        "teamNodes": team_nodes,
        "planformAuthority": "four exact 90-degree SVG sectors",
        "targetHeight": 0.26,
        "symmetry": {
            "anglesDegrees": [0, 90, 180, 270],
            "canonicalMeshGroups": shared_groups,
        },
    }


def reset_pose(armature: bpy.types.Object) -> None:
    for bone in armature.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.location = (0, 0, 0)
        bone.rotation_euler = (0, 0, 0)
        bone.scale = (1, 1, 1)


def key_all_bones(armature: bpy.types.Object, frame: int) -> None:
    for bone in armature.pose.bones:
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_euler", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def make_shell_actions(
    armature: bpy.types.Object,
) -> dict[str, bpy.types.Action]:
    armature.animation_data_create()
    actions: dict[str, bpy.types.Action] = {}

    deploy = bpy.data.actions.new("AegisShell_Deploy")
    deploy.use_fake_user = True
    armature.animation_data.action = deploy
    reset_pose(armature)
    guard = armature.pose.bones["guard"]
    guard.scale = (0.70, 0.70, 0.12)
    guard.location.z = -0.040
    for name in ("support_north", "support_west", "support_south"):
        armature.pose.bones[name].location.z = 0.042
    key_all_bones(armature, 1)
    guard.scale = (0.91, 0.91, 0.66)
    guard.location.z = -0.012
    for name in ("support_north", "support_west", "support_south"):
        armature.pose.bones[name].location.z = 0.014
    key_all_bones(armature, 13)
    reset_pose(armature)
    key_all_bones(armature, 25)
    actions["deploy"] = deploy

    idle = bpy.data.actions.new("AegisShell_Idle")
    idle.use_fake_user = True
    armature.animation_data.action = idle
    reset_pose(armature)
    key_all_bones(armature, 1)
    armature.pose.bones["hub"].location.z = 0.002
    key_all_bones(armature, 13)
    reset_pose(armature)
    key_all_bones(armature, 25)
    armature.pose.bones["hub"].location.z = -0.0015
    key_all_bones(armature, 37)
    reset_pose(armature)
    key_all_bones(armature, 49)
    actions["idle"] = idle

    armature.animation_data.action = idle
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 49
    bpy.context.scene.frame_set(1)
    return actions


def make_turret_actions(
    armature: bpy.types.Object,
) -> dict[str, bpy.types.Action]:
    armature.animation_data_create()
    actions: dict[str, bpy.types.Action] = {}

    deploy = bpy.data.actions.new("AegisTurret_Deploy")
    deploy.use_fake_user = True
    armature.animation_data.action = deploy
    reset_pose(armature)
    folded = {
        "east": 0,
        "north": -math.pi / 2,
        "west": -math.pi,
        "south": math.pi / 2,
    }
    for direction, angle in folded.items():
        armature.pose.bones[f"arm_{direction}"].rotation_euler.z = angle
        armature.pose.bones[f"support_{direction}"].location.z = 0.045
    key_all_bones(armature, 1)
    for direction, angle in folded.items():
        armature.pose.bones[f"arm_{direction}"].rotation_euler.z = angle * 0.42
        armature.pose.bones[f"support_{direction}"].location.z = 0.016
    key_all_bones(armature, 13)
    reset_pose(armature)
    key_all_bones(armature, 25)
    actions["deploy"] = deploy

    idle = bpy.data.actions.new("AegisTurret_Idle")
    idle.use_fake_user = True
    armature.animation_data.action = idle
    reset_pose(armature)
    key_all_bones(armature, 1)
    armature.pose.bones["hub"].location.z = 0.0018
    key_all_bones(armature, 13)
    reset_pose(armature)
    key_all_bones(armature, 25)
    armature.pose.bones["hub"].location.z = -0.0012
    key_all_bones(armature, 37)
    reset_pose(armature)
    key_all_bones(armature, 49)
    actions["idle"] = idle

    armature.animation_data.action = idle
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 49
    bpy.context.scene.frame_set(1)
    return actions


def model_objects(armature: bpy.types.Object) -> list[bpy.types.Object]:
    return [armature] + [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and not obj.name.startswith("REVIEW.")
    ]


def select_model(armature: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in model_objects(armature):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature


def export_glb(path: Path, armature: bpy.types.Object) -> None:
    seen_meshes: set[int] = set()
    for obj in model_objects(armature):
        if obj.type != "MESH" or obj.data.as_pointer() in seen_meshes:
            continue
        seen_meshes.add(obj.data.as_pointer())
        editable = bmesh.new()
        editable.from_mesh(obj.data)
        bmesh.ops.triangulate(
            editable,
            faces=list(editable.faces),
            quad_method="BEAUTY",
            ngon_method="BEAUTY",
        )
        editable.to_mesh(obj.data)
        editable.free()
        obj.data.update()
    select_model(armature)
    path.parent.mkdir(parents=True, exist_ok=True)
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
        export_tangents=True,
        export_image_format="AUTO",
        export_image_quality=90,
    )
    canonicalize_glb_json(path)


def canonicalize_glb_json(path: Path) -> None:
    """Remove exporter ordering noise while preserving binary geometry bytes."""

    data = bytearray(path.read_bytes())
    if data[:4] != b"glTF" or struct.unpack_from("<I", data, 4)[0] != 2:
        raise RuntimeError(f"Not a GLB 2.0 file: {path}")
    json_length, json_type = struct.unpack_from("<II", data, 12)
    if json_type != 0x4E4F534A:
        raise RuntimeError("Expected GLB JSON chunk")
    start = 20
    end = start + json_length
    document = json.loads(bytes(data[start:end]).decode("utf-8"))
    asset = document.setdefault("asset", {})
    asset["generator"] = f"Nilbots deterministic Aegis forms / Blender {BLENDER_VERSION}"
    encoded = json.dumps(document, separators=(",", ":"), ensure_ascii=True).encode()
    padded = encoded + b" " * ((4 - len(encoded) % 4) % 4)
    bin_length, bin_type = struct.unpack_from("<II", data, end)
    if bin_type != 0x004E4942:
        raise RuntimeError("Expected GLB BIN chunk")
    binary = bytes(data[end + 8 : end + 8 + bin_length])
    output = bytearray()
    output.extend(b"glTF")
    output.extend(struct.pack("<I", 2))
    total = 12 + 8 + len(padded) + 8 + len(binary)
    output.extend(struct.pack("<I", total))
    output.extend(struct.pack("<II", len(padded), 0x4E4F534A))
    output.extend(padded)
    output.extend(struct.pack("<II", len(binary), 0x004E4942))
    output.extend(binary)
    path.write_bytes(output)


def collective_bounds(
    objects: Iterable[bpy.types.Object],
) -> tuple[Vector, Vector]:
    points: list[Vector] = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        for corner in obj.bound_box:
            points.append(obj.matrix_world @ Vector(corner))
    minimum = Vector(
        (
            min(point.x for point in points),
            min(point.y for point in points),
            min(point.z for point in points),
        )
    )
    maximum = Vector(
        (
            max(point.x for point in points),
            max(point.y for point in points),
            max(point.z for point in points),
        )
    )
    return minimum, maximum


def triangle_count(objects: Iterable[bpy.types.Object]) -> tuple[int, int]:
    vertices = 0
    triangles = 0
    seen_meshes: set[int] = set()
    for obj in objects:
        if obj.type != "MESH":
            continue
        pointer = obj.data.as_pointer()
        if pointer in seen_meshes:
            continue
        seen_meshes.add(pointer)
        obj.data.calc_loop_triangles()
        vertices += len(obj.data.vertices)
        triangles += len(obj.data.loop_triangles)
    return vertices, triangles


def set_team_material(material: bpy.types.Material, color: str) -> None:
    material.diffuse_color = srgb(color)
    principled = next(
        node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"
    )
    principled.inputs["Base Color"].default_value = srgb(color)
    principled.inputs["Emission Color"].default_value = srgb(color)


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def configure_review_scene() -> tuple[bpy.types.Object, bpy.types.Object]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
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

    lights = (
        ("REVIEW.Key", (-1.6, -1.8, 2.9), 320, 3.0, (0.90, 0.95, 1.0), (0, 0, 0.12)),
        ("REVIEW.Fill", (1.8, 0.8, 1.1), 135, 2.2, (0.34, 0.48, 0.72), (0, 0, 0.12)),
        ("REVIEW.Rim", (-0.8, 1.8, 1.8), 190, 1.5, (0.86, 0.64, 0.36), (0, 0, 0.15)),
        ("REVIEW.SideFill", (0, -2, 1.25), 125, 2.0, (0.72, 0.82, 1.0), (0, 0, 0.11)),
    )
    for name, location, energy, size, color, target in lights:
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.active_object
        light.name = name
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.data.color = color
        look_at(light, Vector(target))

    camera_data = bpy.data.cameras.new("REVIEW.Camera")
    camera = bpy.data.objects.new("REVIEW.Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    return floor, camera


def render_still(
    path: Path,
    camera: bpy.types.Object,
    floor: bpy.types.Object,
    view: str,
) -> None:
    scene = bpy.context.scene
    background = scene.world.node_tree.nodes.get("Background")
    path.parent.mkdir(parents=True, exist_ok=True)
    floor.hide_render = view not in {"gameplay"}
    scene.render.film_transparent = view != "gameplay"

    if view == "top":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.22
        camera.location = (0, 0, 3)
        look_at(camera, Vector((0, 0, 0.14)))
    elif view == "side":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.22
        camera.location = (0, -3, 0.155)
        look_at(camera, Vector((0, 0, 0.155)))
    elif view == "front":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.22
        camera.location = (3, 0, 0.155)
        look_at(camera, Vector((0, 0, 0.155)))
    elif view == "rear":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.22
        camera.location = (-3, 0, 0.155)
        look_at(camera, Vector((0, 0, 0.155)))
    elif view == "iso":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.34
        camera.location = (1.75, -2.05, 1.45)
        look_at(camera, Vector((0, 0, 0.13)))
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
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def render_review(
    output: Path,
    armature: bpy.types.Object,
    actions: dict[str, bpy.types.Action],
    materials: dict[str, bpy.types.Material],
    floor: bpy.types.Object,
    camera: bpy.types.Object,
) -> None:
    armature.animation_data.action = actions["idle"]
    bpy.context.scene.frame_set(1)
    set_team_material(materials["team"], TEAM_BLUE)
    for view in ("top", "side", "front", "rear", "iso"):
        render_still(output / f"{view}.png", camera, floor, view)
    render_still(output / "gameplay-blue.png", camera, floor, "gameplay")
    set_team_material(materials["team"], TEAM_RED)
    render_still(output / "gameplay-red.png", camera, floor, "gameplay")

    set_team_material(materials["team"], TEAM_BLUE)
    armature.animation_data.action = actions["deploy"]
    for frame in (1, 9, 17, 25):
        bpy.context.scene.frame_set(frame)
        render_still(
            output / f"deploy-frame-{frame:02d}.png",
            camera,
            floor,
            "gameplay",
        )
    armature.animation_data.action = actions["idle"]
    bpy.context.scene.frame_set(1)
    set_team_material(materials["team"], TEAM_NEUTRAL)


def render_family_reference(
    output_dir: Path,
    reference_glb: Path,
) -> dict[str, object]:
    """Render the approved mobile Aegis through the exact form-review rig.

    The mobile candidate was authored at a different absolute scale. This
    comparison applies one uniform review-only fit to a one-tile planform; it
    never alters the source or treats the mobile topology as form topology.
    """

    purge_scene()
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(reference_glb))
    imported = [
        obj for obj in bpy.context.scene.objects if obj not in before
    ]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"No mesh in family reference: {reference_glb}")

    armatures = [obj for obj in imported if obj.type == "ARMATURE"]
    idle = bpy.data.actions.get("Aegis_Idle")
    if armatures and idle is not None:
        armature = armatures[0]
        if armature.animation_data is None:
            armature.animation_data_create()
        armature.animation_data.action = idle
        bpy.context.scene.frame_set(1)
        bpy.context.view_layer.update()

    minimum, maximum = collective_bounds(meshes)
    span = max(maximum.x - minimum.x, maximum.y - minimum.y)
    if span <= 0:
        raise RuntimeError("Family reference has no planform span")
    review_scale = 1.0 / span
    centre = (minimum + maximum) * 0.5
    root = bpy.data.objects.new("REVIEW.FamilyReferenceRoot", None)
    bpy.context.scene.collection.objects.link(root)
    imported_set = set(imported)
    for obj in imported:
        if obj.parent not in imported_set:
            world = obj.matrix_world.copy()
            obj.parent = root
            obj.matrix_world = world
    root.scale = (review_scale,) * 3
    root.location = (
        -centre.x * review_scale,
        -centre.y * review_scale,
        -minimum.z * review_scale,
    )
    bpy.context.view_layer.update()

    team_materials = [
        material
        for material in bpy.data.materials
        if material.get("nilbotsRole") == "team-accent"
    ]
    if len(team_materials) != 1:
        raise RuntimeError(
            "Family reference must expose exactly one team-accent material"
        )

    floor, camera = configure_review_scene()
    review_dir = output_dir / "review" / "family-reference"
    team = team_materials[0]
    set_team_material(team, TEAM_BLUE)
    for view in ("top", "side", "iso"):
        render_still(review_dir / f"aegis-tortoise-mobile-{view}.png", camera, floor, view)
    render_still(
        review_dir / "aegis-tortoise-mobile-gameplay-blue.png",
        camera,
        floor,
        "gameplay",
    )
    set_team_material(team, TEAM_RED)
    render_still(
        review_dir / "aegis-tortoise-mobile-gameplay-red.png",
        camera,
        floor,
        "gameplay",
    )
    set_team_material(team, TEAM_NEUTRAL)
    return {
        "id": "aegis-tortoise",
        "sourceSha256": sha256(reference_glb),
        "sourceBytes": reference_glb.stat().st_size,
        "sourceCommit": "4ecfcb4",
        "usage": (
            "material-family reference only; rendered with one uniform "
            "one-tile review fit under the exact 58-degree form-review rig"
        ),
        "sourcePlanformSpan": round(span, 6),
        "reviewUniformScale": round(review_scale, 6),
        "teamAccentMaterialCount": len(team_materials),
    }


def asset_report(
    repo_root: Path,
    asset_id: str,
    armature: bpy.types.Object,
    actions: dict[str, bpy.types.Action],
    metadata: dict[str, object],
    glb: Path,
    blend: Path,
    svg: Path,
) -> dict[str, object]:
    objects = model_objects(armature)
    minimum, maximum = collective_bounds(objects)
    vertices, triangles = triangle_count(objects)
    planform_span = max(maximum.x - minimum.x, maximum.y - minimum.y)
    total_height = maximum.z - minimum.z
    return {
        "assetId": asset_id,
        "sourceAuthority": {
            "svg": str(svg.relative_to(repo_root)),
            "sha256": sha256(svg),
            "planform": metadata["planformAuthority"],
        },
        "providerReference": REFERENCE_MASTERS[asset_id],
        "authoredConstruction": {
            "topology": "procedural authored meshes; no provider topology",
            "objects": len(objects) - 1,
            "uniqueVertices": vertices,
            "uniqueTriangles": triangles,
            "boundsBlenderZUp": {
                "minimum": [round(value, 6) for value in minimum],
                "maximum": [round(value, 6) for value in maximum],
            },
            "boundsRuntimeYUp": {
                "minimum": [
                    round(minimum.x, 6),
                    round(minimum.z, 6),
                    round(-maximum.y, 6),
                ],
                "maximum": [
                    round(maximum.x, 6),
                    round(maximum.z, 6),
                    round(-minimum.y, 6),
                ],
            },
            "planformSpan": round(planform_span, 6),
            "height": round(total_height, 6),
            "heightToPlanform": round(total_height / planform_span, 6),
            "targetHeight": metadata["targetHeight"],
        },
        "hierarchy": {
            "armature": armature.name,
            "bones": [bone.name for bone in armature.data.bones],
            "teamNodes": metadata["teamNodes"],
        },
        "animations": [
            {
                "key": key,
                "name": action.name,
                "frames": [
                    int(action.frame_range[0]),
                    int(action.frame_range[1]),
                ],
            }
            for key, action in actions.items()
        ],
        "materials": [
            {
                "name": material.name,
                **(
                    {"nilbotsRole": material["nilbotsRole"]}
                    if "nilbotsRole" in material
                    else {}
                ),
                "images": sorted(
                    {
                        node.image.name
                        for node in material.node_tree.nodes
                        if node.type == "TEX_IMAGE" and node.image is not None
                    }
                ),
            }
            for material in bpy.data.materials
            if material.users > 0 and not material.name.startswith("REVIEW_")
        ],
        **({"symmetry": metadata["symmetry"]} if "symmetry" in metadata else {}),
        "artifacts": {
            "glb": {
                "path": str(glb.relative_to(repo_root)),
                "bytes": glb.stat().st_size,
                "sha256": sha256(glb),
            },
            "blend": {
                "path": str(blend.relative_to(repo_root)),
                "bytes": blend.stat().st_size,
                "sha256": sha256(blend),
            },
        },
    }


def build_one(
    repo_root: Path,
    output_dir: Path,
    asset_id: str,
) -> dict[str, object]:
    purge_scene()
    texture_dir = output_dir / "source" / "textures"
    materials = create_materials(texture_dir)
    if asset_id == "aegis-tortoise-shell":
        armature, actions, metadata = build_shell(materials)
    elif asset_id == "aegis-tortoise-turret":
        armature, actions, metadata = build_turret(materials)
    else:
        raise ValueError(asset_id)

    floor, camera = configure_review_scene()
    set_team_material(materials["team"], TEAM_NEUTRAL)
    armature.animation_data.action = actions["idle"]
    bpy.context.scene.frame_set(1)

    blend = output_dir / "source" / f"{asset_id}-editable.blend"
    blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), check_existing=False)

    glb = output_dir / "candidate" / f"{asset_id}.glb"
    export_glb(glb, armature)
    render_review(
        output_dir / "review" / asset_id,
        armature,
        actions,
        materials,
        floor,
        camera,
    )
    svg = repo_root / "web/src/assets/class-looks" / asset_id / "sprite.svg"
    report = asset_report(
        repo_root,
        asset_id,
        armature,
        actions,
        metadata,
        glb,
        blend,
        svg,
    )
    write_json(output_dir / "reports" / f"{asset_id}-build.json", report)
    return report


def main() -> None:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    artifacts_root = args.artifacts_root.resolve()
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    reference_report = verify_reference_masters(artifacts_root)
    assets = [
        build_one(repo_root, output_dir, asset_id)
        for asset_id in ("aegis-tortoise-shell", "aegis-tortoise-turret")
    ]
    family_reference = None
    if args.family_reference_glb is not None:
        reference_glb = args.family_reference_glb.resolve()
        if not reference_glb.is_file():
            raise RuntimeError(
                f"Missing family reference GLB: {reference_glb}"
            )
        family_reference = render_family_reference(output_dir, reference_glb)
    write_json(
        output_dir / "build-report.json",
        {
            "schemaVersion": 1,
            "generator": str(Path(__file__).resolve().relative_to(repo_root)),
            "generatorSha256": sha256(Path(__file__).resolve()),
            "blender": {
                "requiredVersion": BLENDER_VERSION,
                "actualVersion": bpy.app.version_string,
            },
            "providerReferences": reference_report,
            "sourcePolicy": (
                "provider masters are hashed visual/material references only; "
                "all checked-in topology is authored procedurally"
            ),
            **(
                {"familyReference": family_reference}
                if family_reference is not None
                else {}
            ),
            "assets": assets,
        },
    )
    print(
        json.dumps(
            {
                report["assetId"]: {
                    "triangles": report["authoredConstruction"]["uniqueTriangles"],
                    "bytes": report["artifacts"]["glb"]["bytes"],
                    "planform": report["authoredConstruction"]["planformSpan"],
                    "height": report["authoredConstruction"]["height"],
                }
                for report in assets
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
