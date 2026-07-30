#!/usr/bin/env python3
"""Build the authored Nilbots class projectile heads.

The canonical SVG paths below own every outer silhouette and aperture. Audited
provider GLBs are verified by hash, then used only as human form/material
reference: this script never imports or samples their geometry, UVs, or maps.
"""

from __future__ import annotations

import argparse
import bmesh
import bpy
import hashlib
import json
import math
import struct
import sys
from pathlib import Path
from typing import Iterable, Sequence

from mathutils import Vector
from mathutils.geometry import tessellate_polygon


BLENDER_VERSION = "5.2.0"
TEXTURE_SIZE = 128
REVIEW_SIZE = 640
TEAM_CYAN = "#22d3ee"
TEAM_RED = "#ef4444"

REFERENCE_MASTERS = {
    "trident-spark": {
        "taskId": "019fb203-d779-779d-8374-59deec6606d3",
        "relative": (
            "trident-spark/"
            "019fb203-d779-779d-8374-59deec6606d3/raw-model.glb"
        ),
        "bytes": 34_210_560,
        "sha256": (
            "b48adbf2931a101663918e207af12794f"
            "5ee3cc3a480bf35e98ede979be0736d"
        ),
        "auditCorrection": (
            "exact SVG wing/fork planform and authored 0.165-tile underside "
            "replace the raw master's 33.7% depth deficit"
        ),
    },
    "rebound-diamond": {
        "taskId": "019fb206-978f-7ff8-9f18-e2b725c02584",
        "relative": (
            "rebound-diamond/"
            "019fb206-978f-7ff8-9f18-e2b725c02584/raw-model.glb"
        ),
        "bytes": 37_580_164,
        "sha256": (
            "fad945e58c6010f72205787c1570c895"
            "395f4846a05e4984e232c06858f1964d"
        ),
        "auditCorrection": (
            "exact elongated-hex SVG aperture replaces the rounded raw "
            "tunnel while preserving the low armored depth class"
        ),
    },
    "lattice-rivet": {
        "taskId": "019fb209-ea51-76ae-94ed-c6b3c92275b3",
        "relative": (
            "lattice-rivet/"
            "019fb209-ea51-76ae-94ed-c6b3c92275b3/raw-model.glb"
        ),
        "bytes": 40_265_884,
        "sha256": (
            "f62409fd1d8bf3cafaaa92a29cd8e3eb"
            "9cfe072d51368c7ce1a63c253f030774"
        ),
        "auditCorrection": (
            "provider topology is discarded; the visible-top identity is "
            "rebuilt over one authored 0.160-tile underside with no duplicate"
        ),
    },
}

SVG_SHA256 = {
    "trident-spark": (
        "b44365017f788d9ba8ff207f04976ee2"
        "44f488493699d9214d2fd8a767b25f59"
    ),
    "rebound-diamond": (
        "7aa2a8621dbd75fa135135a2573b35ae"
        "bcc140d46afc35b8cf4ae16f35d34dd7"
    ),
    "lattice-rivet": (
        "e5f86e1d2c33816ab807132ced927487"
        "1854b2881ee2d47978d8ce980248a3ab"
    ),
}

# Exact outer and negative-space coordinates from the canonical 256-viewBox
# runtime SVGs. They are normalized uniformly per asset; no warp is permitted.
OUTLINES = {
    "trident-spark": (
        (43, 113),
        (119, 113),
        (167, 70),
        (194, 70),
        (176, 113),
        (198, 113),
        (231, 128),
        (198, 143),
        (176, 143),
        (194, 186),
        (167, 186),
        (119, 143),
        (43, 143),
        (70, 128),
    ),
    "rebound-diamond": (
        (42, 128),
        (88, 86),
        (150, 86),
        (214, 128),
        (150, 170),
        (88, 170),
    ),
    "lattice-rivet": (
        (45, 103),
        (98, 103),
        (127, 80),
        (170, 103),
        (190, 103),
        (225, 128),
        (190, 153),
        (170, 153),
        (127, 176),
        (98, 153),
        (45, 153),
        (73, 128),
    ),
}

APERTURES = {
    "trident-spark": (),
    "rebound-diamond": (
        (85, 128),
        (109, 146),
        (141, 146),
        (172, 128),
        (141, 110),
        (109, 110),
    ),
    "lattice-rivet": (
        (109, 128),
        (130, 143),
        (155, 128),
        (130, 113),
    ),
}

LOOK_SCALES = {
    "trident-spark": 0.58,
    "rebound-diamond": 0.56,
    "lattice-rivet": 0.54,
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--artifacts-root", required=True, type=Path)
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def verify_inputs(
    repo_root: Path,
    artifacts_root: Path,
) -> dict[str, object]:
    report: dict[str, object] = {}
    for asset_id, expected in REFERENCE_MASTERS.items():
        svg = (
            repo_root
            / "web/src/assets/class-projectile-looks"
            / asset_id
            / "sprite.svg"
        )
        if sha256(svg) != SVG_SHA256[asset_id]:
            raise RuntimeError(f"Canonical SVG drift for {asset_id}")

        master = artifacts_root / str(expected["relative"])
        if not master.is_file():
            raise RuntimeError(f"Missing durable provider reference: {master}")
        actual_hash = sha256(master)
        actual_bytes = master.stat().st_size
        if (
            actual_hash != expected["sha256"]
            or actual_bytes != expected["bytes"]
        ):
            raise RuntimeError(
                f"Provider reference drift for {asset_id}: "
                f"{actual_hash}/{actual_bytes}"
            )
        report[asset_id] = {
            "taskId": expected["taskId"],
            "sha256": actual_hash,
            "bytes": actual_bytes,
            "usage": (
                "verified human material/form reference only; never imported "
                "or sampled by the deterministic builder"
            ),
            "auditCorrection": expected["auditCorrection"],
        }
    return report


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
        bpy.data.worlds,
    ):
        for block in list(collection):
            collection.remove(block)


def srgb(value: str) -> tuple[float, float, float, float]:
    text = value.removeprefix("#")
    if len(text) != 6:
        raise ValueError(value)
    return (
        int(text[0:2], 16) / 255,
        int(text[2:4], 16) / 255,
        int(text[4:6], 16) / 255,
        1.0,
    )


def normalize_points(
    asset_id: str,
    points: Sequence[tuple[float, float]],
) -> tuple[tuple[float, float], ...]:
    authority = OUTLINES[asset_id]
    minimum_x = min(point[0] for point in authority)
    maximum_x = max(point[0] for point in authority)
    minimum_y = min(point[1] for point in authority)
    maximum_y = max(point[1] for point in authority)
    centre_x = (minimum_x + maximum_x) / 2
    centre_y = (minimum_y + maximum_y) / 2
    scale = max(maximum_x - minimum_x, maximum_y - minimum_y)
    return tuple(
        ((x - centre_x) / scale, -(y - centre_y) / scale)
        for x, y in points
    )


def scale_loop(
    points: Sequence[tuple[float, float]],
    factor: float,
    *,
    centre: tuple[float, float] = (0.0, 0.0),
) -> tuple[tuple[float, float], ...]:
    return tuple(
        (
            centre[0] + (x - centre[0]) * factor,
            centre[1] + (y - centre[1]) * factor,
        )
        for x, y in points
    )


def signed_area(points: Sequence[tuple[float, float]]) -> float:
    return 0.5 * sum(
        x * points[(index + 1) % len(points)][1]
        - points[(index + 1) % len(points)][0] * y
        for index, (x, y) in enumerate(points)
    )


def oriented_loop(
    points: Sequence[tuple[float, float]],
    *,
    clockwise: bool,
) -> tuple[tuple[float, float], ...]:
    result = tuple(points)
    is_clockwise = signed_area(result) < 0
    return tuple(reversed(result)) if is_clockwise != clockwise else result


def active_object(obj: bpy.types.Object) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)


def apply_bevel(
    obj: bpy.types.Object,
    width: float,
    *,
    segments: int = 2,
) -> None:
    if width <= 0:
        return
    modifier = obj.modifiers.new("Authored edge chamfer", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    modifier.affect = "EDGES"
    active_object(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def add_box_uvs(mesh: bpy.types.Mesh) -> None:
    layer = mesh.uv_layers.get("UVMap") or mesh.uv_layers.new(name="UVMap")
    coordinates = [vertex.co for vertex in mesh.vertices]
    minimum = Vector(
        tuple(min(value[axis] for value in coordinates) for axis in range(3))
    )
    maximum = Vector(
        tuple(max(value[axis] for value in coordinates) for axis in range(3))
    )
    span = Vector(
        tuple(max(maximum[axis] - minimum[axis], 1e-7) for axis in range(3))
    )
    for polygon in mesh.polygons:
        dominant = max(
            range(3),
            key=lambda axis: abs(polygon.normal[axis]),
        )
        axes = (0, 1) if dominant == 2 else ((1, 2) if dominant == 0 else (0, 2))
        for loop_index in polygon.loop_indices:
            vertex = coordinates[mesh.loops[loop_index].vertex_index]
            layer.data[loop_index].uv = (
                (vertex[axes[0]] - minimum[axes[0]]) / span[axes[0]],
                (vertex[axes[1]] - minimum[axes[1]]) / span[axes[1]],
            )


def add_prism(
    name: str,
    loops: Sequence[Sequence[tuple[float, float]]],
    bottom: float,
    top: float,
    material: bpy.types.Material,
    *,
    bevel: float = 0.0,
) -> bpy.types.Object:
    if not loops or len(loops[0]) < 3:
        raise ValueError(f"{name} requires an outer loop")
    prepared = [
        oriented_loop(loops[0], clockwise=False),
        *(
            oriented_loop(loop, clockwise=True)
            for loop in loops[1:]
            if len(loop) >= 3
        ),
    ]
    planar = [
        [Vector((x, y, 0.0)) for x, y in loop]
        for loop in prepared
    ]
    triangles = tessellate_polygon(planar)
    flat = [point for loop in prepared for point in loop]
    coordinate_index = {
        (round(x, 9), round(y, 9)): index
        for index, (x, y) in enumerate(flat)
    }
    count = len(flat)
    vertices = [(x, y, bottom) for x, y in flat] + [
        (x, y, top) for x, y in flat
    ]
    faces: list[tuple[int, ...]] = []
    for triangle in triangles:
        # Blender 5.2 returns flattened integer indices; older supported
        # Blender builds returned the input Vectors. Accept both without
        # changing authored vertex order.
        indices = (
            tuple(int(point) for point in triangle)
            if triangle and isinstance(triangle[0], int)
            else tuple(
                coordinate_index[(round(point.x, 9), round(point.y, 9))]
                for point in triangle
            )
        )
        faces.append(tuple(reversed(indices)))
        faces.append(tuple(index + count for index in indices))

    offset = 0
    for loop in prepared:
        loop_count = len(loop)
        for index in range(loop_count):
            following = (index + 1) % loop_count
            lower = offset + index
            lower_next = offset + following
            faces.append(
                (
                    lower,
                    lower_next,
                    lower_next + count,
                    lower + count,
                )
            )
        offset += loop_count

    mesh = bpy.data.meshes.new(f"{name}.Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj["nilbotsRole"] = "projectile-head"
    apply_bevel(obj, bevel)
    add_box_uvs(mesh)
    for polygon in mesh.polygons:
        polygon.use_smooth = False
    return obj


def add_box(
    name: str,
    dimensions: tuple[float, float, float],
    location: tuple[float, float, float],
    material: bpy.types.Material,
    *,
    rotation_z: float = 0.0,
    bevel: float = 0.0,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=location,
        rotation=(0.0, 0.0, rotation_z),
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.dimensions = dimensions
    active_object(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    obj["nilbotsRole"] = "projectile-head"
    apply_bevel(obj, bevel)
    add_box_uvs(obj.data)
    return obj


def add_bar(
    name: str,
    start: tuple[float, float],
    end: tuple[float, float],
    z: float,
    width: float,
    height: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    delta_x = end[0] - start[0]
    delta_y = end[1] - start[1]
    length = math.hypot(delta_x, delta_y)
    return add_box(
        name,
        (length, width, height),
        (
            (start[0] + end[0]) / 2,
            (start[1] + end[1]) / 2,
            z + height / 2,
        ),
        material,
        rotation_z=math.atan2(delta_y, delta_x),
        bevel=min(width, height) * 0.22,
    )


def add_cylinder(
    name: str,
    radius: float,
    depth: float,
    location: tuple[float, float, float],
    material: bpy.types.Material,
    *,
    vertices: int = 12,
    bevel: float = 0.0,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(material)
    obj["nilbotsRole"] = "projectile-head"
    apply_bevel(obj, bevel)
    add_box_uvs(obj.data)
    return obj


def deterministic_surface_maps(
    asset_id: str,
    output_dir: Path,
    seed: int,
) -> dict[str, bpy.types.Image]:
    output_dir.mkdir(parents=True, exist_ok=True)
    height: list[float] = [0.0] * (TEXTURE_SIZE * TEXTURE_SIZE)
    roughness: list[float] = [0.0] * (TEXTURE_SIZE * TEXTURE_SIZE)

    for y in range(TEXTURE_SIZE):
        for x in range(TEXTURE_SIZE):
            u = (x + 0.5) / TEXTURE_SIZE
            v = (y + 0.5) / TEXTURE_SIZE
            state = (
                (x * 374_761_393)
                ^ (y * 668_265_263)
                ^ (seed * 2_246_822_519)
            ) & 0xFFFFFFFF
            state = ((state ^ (state >> 13)) * 1_274_126_177) & 0xFFFFFFFF
            grain = ((state >> 9) & 1023) / 1023 - 0.5
            diagonal_a = abs(
                math.sin((u * 2.6 + v * 1.35 + seed * 0.031) * math.tau)
            )
            diagonal_b = abs(
                math.sin((u * 1.15 - v * 3.1 + seed * 0.019) * math.tau)
            )
            seam = diagonal_a < 0.035 or diagonal_b < 0.027
            scratch = (
                ((x * 11 + y * 5 + seed * 17) % 419 < 2)
                and not seam
            )
            broad = (
                math.sin((u * 2.1 + v * 0.7 + seed * 0.11) * math.tau)
                + math.sin((v * 1.8 - u * 0.55 + seed * 0.07) * math.tau)
            ) * 0.045
            relief = grain * 0.10 + broad
            if seam:
                relief -= 0.52
            if scratch:
                relief += 0.24
            index = y * TEXTURE_SIZE + x
            height[index] = relief
            roughness[index] = max(
                0.26,
                min(0.82, 0.51 + grain * 0.16 - broad * 0.8 + (0.16 if seam else 0)),
            )

    normal_pixels: list[float] = [0.0] * (
        TEXTURE_SIZE * TEXTURE_SIZE * 4
    )
    control_pixels: list[float] = [0.0] * (
        TEXTURE_SIZE * TEXTURE_SIZE * 4
    )
    for y in range(TEXTURE_SIZE):
        previous_y = (y - 1) % TEXTURE_SIZE
        next_y = (y + 1) % TEXTURE_SIZE
        for x in range(TEXTURE_SIZE):
            previous_x = (x - 1) % TEXTURE_SIZE
            next_x = (x + 1) % TEXTURE_SIZE
            dx = (
                height[y * TEXTURE_SIZE + previous_x]
                - height[y * TEXTURE_SIZE + next_x]
            ) * 0.48
            dy = (
                height[previous_y * TEXTURE_SIZE + x]
                - height[next_y * TEXTURE_SIZE + x]
            ) * 0.48
            length = math.sqrt(dx * dx + dy * dy + 1.0)
            index = y * TEXTURE_SIZE + x
            offset = index * 4
            normal_pixels[offset + 0] = dx / length * 0.5 + 0.5
            normal_pixels[offset + 1] = dy / length * 0.5 + 0.5
            normal_pixels[offset + 2] = 1.0 / length * 0.5 + 0.5
            normal_pixels[offset + 3] = 1.0
            control_pixels[offset + 0] = 1.0
            control_pixels[offset + 1] = roughness[index]
            control_pixels[offset + 2] = max(
                0.08,
                min(0.72, 0.46 + height[index] * 0.16),
            )
            control_pixels[offset + 3] = 1.0

    def save(
        label: str,
        pixels: list[float],
        *,
        non_color: bool,
    ) -> bpy.types.Image:
        image = bpy.data.images.new(
            f"{asset_id}.{label}",
            width=TEXTURE_SIZE,
            height=TEXTURE_SIZE,
            alpha=True,
        )
        if non_color:
            image.colorspace_settings.name = "Non-Color"
        image.pixels.foreach_set(pixels)
        image.filepath_raw = str(output_dir / f"{label}.png")
        image.file_format = "PNG"
        image.save()
        image.pack()
        return image

    maps: dict[str, bpy.types.Image] = {
        "normal": save("surface-normal", normal_pixels, non_color=True),
        "metallicRoughness": save(
            "surface-metallic-roughness",
            control_pixels,
            non_color=True,
        ),
    }
    values = {
        "dark": 0.27,
        "mid": 0.61,
        "core": 0.94,
    }
    for label, base in values.items():
        pixels: list[float] = [0.0] * (
            TEXTURE_SIZE * TEXTURE_SIZE * 4
        )
        for index, relief in enumerate(height):
            value = max(
                0.04,
                min(1.0, base * (1.0 + relief * 0.34)),
            )
            offset = index * 4
            pixels[offset : offset + 4] = [value, value, value, 1.0]
        maps[label] = save(
            f"surface-albedo-{label}",
            pixels,
            non_color=False,
        )
    return maps


def make_material(
    asset_id: str,
    label: str,
    albedo: bpy.types.Image,
    normal: bpy.types.Image,
    control: bpy.types.Image,
    *,
    metallic: float,
    roughness: float,
    emission_strength: float,
) -> bpy.types.Material:
    material = bpy.data.materials.new(
        f"NB_PROJECTILE_MASK_{asset_id.upper().replace('-', '_')}_{label.upper()}"
    )
    material.use_nodes = True
    material["nilbotsRole"] = "projectile-mask"
    material["nilbotsPalette"] = "team-neutral-grayscale"
    material.diffuse_color = (1.0, 1.0, 1.0, 1.0)
    material.metallic = metallic
    material.roughness = roughness

    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = next(
        node for node in nodes if node.type == "BSDF_PRINCIPLED"
    )
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Emission Color"].default_value = (
        1.0,
        1.0,
        1.0,
        1.0,
    )
    principled.inputs["Emission Strength"].default_value = emission_strength

    albedo_node = nodes.new("ShaderNodeTexImage")
    albedo_node.name = f"{material.name}.Albedo"
    albedo_node.label = "nilbots-albedo"
    albedo_node.image = albedo
    links.new(albedo_node.outputs["Color"], principled.inputs["Base Color"])
    # Three.js multiplies the renderer-owned team tint through the emissive
    # map. Reusing the grayscale albedo preserves dark/mid/core value breakup
    # instead of turning every material into one flat full-strength glow.
    links.new(
        albedo_node.outputs["Color"],
        principled.inputs["Emission Color"],
    )

    control_node = nodes.new("ShaderNodeTexImage")
    control_node.name = f"{material.name}.MetallicRoughness"
    control_node.image = control
    control_node.image.colorspace_settings.name = "Non-Color"
    separate = nodes.new("ShaderNodeSeparateColor")
    separate.name = f"{material.name}.PBRChannels"
    links.new(control_node.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], principled.inputs["Roughness"])
    links.new(separate.outputs["Blue"], principled.inputs["Metallic"])

    normal_texture = nodes.new("ShaderNodeTexImage")
    normal_texture.name = f"{material.name}.Normal"
    normal_texture.image = normal
    normal_texture.image.colorspace_settings.name = "Non-Color"
    normal_node = nodes.new("ShaderNodeNormalMap")
    normal_node.name = f"{material.name}.NormalBasis"
    normal_node.inputs["Strength"].default_value = 0.52
    links.new(normal_texture.outputs["Color"], normal_node.inputs["Color"])
    links.new(normal_node.outputs["Normal"], principled.inputs["Normal"])
    return material


def create_materials(
    asset_id: str,
    texture_dir: Path,
) -> dict[str, bpy.types.Material]:
    seed = {
        "trident-spark": 31,
        "rebound-diamond": 71,
        "lattice-rivet": 113,
    }[asset_id]
    maps = deterministic_surface_maps(asset_id, texture_dir, seed)
    return {
        "dark": make_material(
            asset_id,
            "dark",
            maps["dark"],
            maps["normal"],
            maps["metallicRoughness"],
            metallic=0.54,
            roughness=0.53,
            emission_strength=0.07,
        ),
        "mid": make_material(
            asset_id,
            "mid",
            maps["mid"],
            maps["normal"],
            maps["metallicRoughness"],
            metallic=0.66,
            roughness=0.43,
            emission_strength=0.15,
        ),
        "core": make_material(
            asset_id,
            "core",
            maps["core"],
            maps["normal"],
            maps["metallicRoughness"],
            metallic=0.14,
            roughness=0.29,
            emission_strength=0.82,
        ),
    }


def build_trident(
    materials: dict[str, bpy.types.Material],
) -> list[bpy.types.Object]:
    asset_id = "trident-spark"
    outer = normalize_points(asset_id, OUTLINES[asset_id])
    objects = [
        add_prism(
            "TridentSpark.Root.Hardpoint",
            [outer],
            0.0,
            0.074,
            materials["dark"],
            bevel=0.009,
        ),
        add_prism(
            "TridentSpark.Root.Armor",
            [scale_loop(outer, 0.955)],
            0.055,
            0.102,
            materials["dark"],
            bevel=0.008,
        ),
    ]
    for label, points in (
        (
            "NorthConductor",
            ((119, 113), (167, 70), (194, 70), (176, 113)),
        ),
        (
            "SouthConductor",
            ((119, 143), (176, 143), (194, 186), (167, 186)),
        ),
        (
            "NoseFace",
            ((167, 116), (198, 116), (225, 128), (198, 140), (167, 140)),
        ),
    ):
        objects.append(
            add_prism(
                f"TridentSpark.{label}",
                [normalize_points(asset_id, points)],
                0.084,
                0.145,
                materials["mid"],
                bevel=0.006,
            )
        )
    spine = normalize_points(
        asset_id,
        ((100, 128), (119, 119), (209, 128), (119, 137)),
    )
    objects.append(
        add_prism(
            "TridentSpark.EnergySpine",
            [spine],
            0.102,
            0.165,
            materials["core"],
            bevel=0.005,
        )
    )
    for y, label in ((0.055, "NorthRail"), (-0.055, "SouthRail")):
        objects.append(
            add_bar(
                f"TridentSpark.{label}",
                (-0.30, y),
                (0.24, y * 0.58),
                0.081,
                0.018,
                0.022,
                materials["mid"],
            )
        )
    return objects


def build_rebound(
    materials: dict[str, bpy.types.Material],
) -> list[bpy.types.Object]:
    asset_id = "rebound-diamond"
    outer = normalize_points(asset_id, OUTLINES[asset_id])
    aperture = normalize_points(asset_id, APERTURES[asset_id])
    objects = [
        add_prism(
            "ReboundDiamond.Root.OpenRing",
            [outer, aperture],
            0.0,
            0.088,
            materials["dark"],
            bevel=0.010,
        ),
        add_prism(
            "ReboundDiamond.ReflectorRing",
            [
                scale_loop(outer, 0.925),
                scale_loop(aperture, 1.115),
            ],
            0.064,
            0.126,
            materials["mid"],
            bevel=0.008,
        ),
        add_prism(
            "ReboundDiamond.InnerTunnelRim",
            [
                scale_loop(aperture, 1.27),
                aperture,
            ],
            0.092,
            0.145,
            materials["dark"],
            bevel=0.006,
        ),
    ]
    for label, points in (
        (
            "NorthEnergy",
            ((105, 99), (142, 99), (160, 110), (148, 107), (116, 107), (98, 104)),
        ),
        (
            "SouthEnergy",
            ((98, 152), (116, 149), (148, 149), (160, 146), (142, 157), (105, 157)),
        ),
        (
            "RearChevron",
            ((54, 128), (83, 104), (91, 114), (72, 128), (91, 142), (83, 152)),
        ),
    ):
        objects.append(
            add_prism(
                f"ReboundDiamond.{label}",
                [normalize_points(asset_id, points)],
                0.126,
                0.153 if label == "RearChevron" else 0.158,
                materials["core"] if label != "RearChevron" else materials["dark"],
                bevel=0.004,
            )
        )
    return objects


def build_lattice(
    materials: dict[str, bpy.types.Material],
) -> list[bpy.types.Object]:
    asset_id = "lattice-rivet"
    outer = normalize_points(asset_id, OUTLINES[asset_id])
    aperture = normalize_points(asset_id, APERTURES[asset_id])
    objects = [
        add_prism(
            "LatticeRivet.Root.SingleBody",
            [outer, aperture],
            0.0,
            0.084,
            materials["dark"],
            bevel=0.009,
        ),
        add_prism(
            "LatticeRivet.CeramicArmor",
            [
                scale_loop(outer, 0.925),
                scale_loop(aperture, 1.12),
            ],
            0.060,
            0.128,
            materials["mid"],
            bevel=0.008,
        ),
        add_prism(
            "LatticeRivet.DiamondCollar",
            [scale_loop(aperture, 1.31), aperture],
            0.098,
            0.160,
            materials["dark"],
            bevel=0.006,
        ),
        add_prism(
            "LatticeRivet.ForwardWedge",
            [
                normalize_points(
                    asset_id,
                    ((158, 106), (189, 106), (218, 128), (189, 150), (158, 150), (176, 128)),
                )
            ],
            0.111,
            0.158,
            materials["mid"],
            bevel=0.006,
        ),
        add_prism(
            "LatticeRivet.RearCore",
            [
                normalize_points(
                    asset_id,
                    ((64, 128), (80, 113), (101, 128), (80, 143)),
                )
            ],
            0.104,
            0.158,
            materials["dark"],
            bevel=0.005,
        ),
    ]

    north_rail = normalize_points(
        asset_id,
        ((48, 106), (96, 106), (117, 90), (126, 97), (94, 121), (65, 121)),
    )
    south_rail = normalize_points(
        asset_id,
        ((65, 135), (94, 135), (126, 159), (117, 166), (96, 150), (48, 150)),
    )
    for label, points in (("NorthRail", north_rail), ("SouthRail", south_rail)):
        objects.append(
            add_prism(
                f"LatticeRivet.{label}.Armor",
                [points],
                0.104,
                0.153,
                materials["mid"],
                bevel=0.006,
            )
        )

    # Coarse, real truss bars survive the 20 px review; they are deliberately
    # geometry rather than a painted fake opening.
    for side, y in (("North", 0.090), ("South", -0.090)):
        for index, x in enumerate((-0.39, -0.29, -0.19), start=1):
            objects.append(
                add_bar(
                    f"LatticeRivet.{side}Truss{index}",
                    (x - 0.045, y - 0.022),
                    (x + 0.045, y + 0.022),
                    0.086,
                    0.012,
                    0.025,
                    materials["dark"],
                )
            )
    for index, y in enumerate((-0.168, 0.168), start=1):
        objects.append(
            add_cylinder(
                f"LatticeRivet.Coil{index}",
                0.021,
                0.068,
                (-0.095, y, 0.105),
                materials["core"],
                vertices=12,
                bevel=0.003,
            )
        )
    return objects


def model_objects() -> list[bpy.types.Object]:
    return [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and not obj.name.startswith("REVIEW.")
    ]


def triangulate(objects: Iterable[bpy.types.Object]) -> None:
    seen: set[int] = set()
    for obj in objects:
        pointer = obj.data.as_pointer()
        if pointer in seen:
            continue
        seen.add(pointer)
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


def canonicalize_glb(path: Path) -> None:
    data = bytearray(path.read_bytes())
    if data[:4] != b"glTF" or struct.unpack_from("<I", data, 4)[0] != 2:
        raise RuntimeError(f"Not a GLB 2.0 file: {path}")
    json_length, json_type = struct.unpack_from("<II", data, 12)
    if json_type != 0x4E4F534A:
        raise RuntimeError("Expected GLB JSON chunk")
    start = 20
    end = start + json_length
    document = json.loads(bytes(data[start:end]).decode("utf-8"))
    document.setdefault("asset", {})["generator"] = (
        f"Nilbots deterministic class projectiles / Blender {BLENDER_VERSION}"
    )
    encoded = json.dumps(
        document,
        separators=(",", ":"),
        ensure_ascii=True,
    ).encode()
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


def export_glb(path: Path, objects: list[bpy.types.Object]) -> None:
    triangulate(objects)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=True,
        export_extras=True,
        export_cameras=False,
        export_lights=False,
        export_animations=False,
        export_tangents=True,
        export_image_format="AUTO",
        export_image_quality=90,
    )
    canonicalize_glb(path)


def object_bounds(
    objects: Iterable[bpy.types.Object],
) -> tuple[Vector, Vector]:
    points = [
        obj.matrix_world @ Vector(corner)
        for obj in objects
        for corner in obj.bound_box
    ]
    minimum = Vector(
        tuple(min(point[axis] for point in points) for axis in range(3))
    )
    maximum = Vector(
        tuple(max(point[axis] for point in points) for axis in range(3))
    )
    return minimum, maximum


def geometry_counts(
    objects: Iterable[bpy.types.Object],
) -> tuple[int, int]:
    unique: dict[int, bpy.types.Mesh] = {}
    for obj in objects:
        unique.setdefault(obj.data.as_pointer(), obj.data)
    vertices = sum(len(mesh.vertices) for mesh in unique.values())
    triangles = sum(
        max(0, len(polygon.vertices) - 2)
        for mesh in unique.values()
        for polygon in mesh.polygons
    )
    return vertices, triangles


def make_review_material(
    name: str,
    color: str,
    *,
    metallic: float,
    roughness: float,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = srgb(color)
    principled = next(
        node
        for node in material.node_tree.nodes
        if node.type == "BSDF_PRINCIPLED"
    )
    principled.inputs["Base Color"].default_value = srgb(color)
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    return material


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def configure_review_scene() -> tuple[bpy.types.Object, bpy.types.Object]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = REVIEW_SIZE
    scene.render.resolution_y = REVIEW_SIZE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 15
    scene.render.film_transparent = True
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.8

    world = bpy.data.worlds.new("REVIEW.World")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = srgb("#0b1020")
    background.inputs["Strength"].default_value = 0.38
    scene.world = world

    floor_material = make_review_material(
        "REVIEW.FloorMaterial",
        "#222a35",
        metallic=0.34,
        roughness=0.78,
    )
    floor = add_box(
        "REVIEW.Floor",
        (4.0, 4.0, 0.018),
        (0.0, 0.0, -0.012),
        floor_material,
        bevel=0.004,
    )
    floor["reviewOnly"] = True

    for name, location, energy, size, color in (
        ("REVIEW.Key", (-1.8, -2.1, 3.0), 390, 3.2, "#dbeafe"),
        ("REVIEW.Fill", (2.1, 0.4, 1.4), 170, 2.4, "#8aa4c8"),
        ("REVIEW.Rim", (-0.5, 2.0, 2.0), 235, 1.7, "#d0a46d"),
        ("REVIEW.Top", (0.1, 0.0, 3.4), 180, 2.0, "#ffffff"),
    ):
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.active_object
        light.name = name
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.data.color = srgb(color)[:3]
        look_at(light, Vector((0.0, 0.0, 0.08)))

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
    path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.resolution_x = REVIEW_SIZE
    scene.render.resolution_y = REVIEW_SIZE
    floor.hide_render = view not in {"gameplay", "gameplay-small"}
    scene.render.film_transparent = floor.hide_render
    target = Vector((0.0, 0.0, 0.08))

    if view == "top":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.20
        camera.location = (0.0, 0.0, 3.0)
    elif view == "side":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.20
        camera.location = (0.0, -3.0, 0.15)
    elif view == "front":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 0.72
        camera.location = (3.0, 0.0, 0.15)
    elif view == "rear":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 0.72
        camera.location = (-3.0, 0.0, 0.15)
    elif view == "iso":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.32
        camera.location = (1.75, -2.05, 1.45)
    elif view == "gameplay":
        camera.data.type = "PERSP"
        camera.data.angle = math.radians(42)
        distance = 1.90
        pitch = math.radians(58)
        camera.location = (
            -0.08,
            math.cos(pitch) * distance,
            math.sin(pitch) * distance,
        )
    elif view == "gameplay-small":
        scene.render.resolution_x = 128
        scene.render.resolution_y = 128
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 6.4
        pitch = math.radians(58)
        camera.location = (
            -0.08,
            math.cos(pitch) * 4.0,
            math.sin(pitch) * 4.0,
        )
    else:
        raise ValueError(view)
    look_at(camera, target)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def install_review_tint(
    materials: Iterable[bpy.types.Material],
    color: str,
) -> None:
    tint = srgb(color)
    for material in materials:
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        principled = next(
            node for node in nodes if node.type == "BSDF_PRINCIPLED"
        )
        albedo = next(
            node for node in nodes if node.label == "nilbots-albedo"
        )
        mix = nodes.get("REVIEW.TeamTint")
        if mix is None:
            mix = nodes.new("ShaderNodeMixRGB")
            mix.name = "REVIEW.TeamTint"
            mix.blend_type = "MULTIPLY"
            mix.inputs[0].default_value = 1.0
            links.new(albedo.outputs["Color"], mix.inputs[1])
            for link in list(principled.inputs["Base Color"].links):
                links.remove(link)
            links.new(mix.outputs["Color"], principled.inputs["Base Color"])
            for link in list(principled.inputs["Emission Color"].links):
                links.remove(link)
            links.new(
                mix.outputs["Color"],
                principled.inputs["Emission Color"],
            )
        mix.inputs[2].default_value = tint


def render_review(
    review_dir: Path,
    materials: dict[str, bpy.types.Material],
) -> None:
    floor, camera = configure_review_scene()
    for view in ("top", "side", "front", "rear", "iso", "gameplay"):
        render_still(
            review_dir / f"{view}-neutral.png",
            camera,
            floor,
            view,
        )
    install_review_tint(materials.values(), TEAM_CYAN)
    render_still(
        review_dir / "gameplay-cyan.png",
        camera,
        floor,
        "gameplay",
    )
    render_still(
        review_dir / "gameplay-20px-cyan.png",
        camera,
        floor,
        "gameplay-small",
    )
    install_review_tint(materials.values(), TEAM_RED)
    render_still(
        review_dir / "gameplay-red.png",
        camera,
        floor,
        "gameplay",
    )
    render_still(
        review_dir / "gameplay-20px-red.png",
        camera,
        floor,
        "gameplay-small",
    )


def build_asset(
    repo_root: Path,
    package_root: Path,
    asset_id: str,
) -> dict[str, object]:
    purge_scene()
    materials = create_materials(
        asset_id,
        package_root / "source/textures" / asset_id,
    )
    if asset_id == "trident-spark":
        objects = build_trident(materials)
        target_height = 0.165
    elif asset_id == "rebound-diamond":
        objects = build_rebound(materials)
        target_height = 0.158
    elif asset_id == "lattice-rivet":
        objects = build_lattice(materials)
        target_height = 0.160
    else:
        raise ValueError(asset_id)

    minimum, maximum = object_bounds(objects)
    span = maximum - minimum
    planform_span = max(span.x, span.y)
    if abs(planform_span - 1.0) > 0.012:
        raise RuntimeError(
            f"{asset_id} planform span drift: {planform_span:.6f}"
        )
    if minimum.z < -1e-6 or abs(maximum.z - target_height) > 0.015:
        raise RuntimeError(
            f"{asset_id} height/floor drift: {minimum.z:.6f}..{maximum.z:.6f}"
        )

    blend = package_root / "source" / f"{asset_id}-editable.blend"
    blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), check_existing=False)

    glb = (
        repo_root
        / "web/src/assets/class-projectile-looks"
        / asset_id
        / "model.glb"
    )
    export_glb(glb, objects)
    vertices, triangles = geometry_counts(objects)
    render_review(package_root / "review" / asset_id, materials)
    return {
        "assetId": asset_id,
        "canonicalSvg": {
            "path": (
                f"web/src/assets/class-projectile-looks/{asset_id}/sprite.svg"
            ),
            "sha256": SVG_SHA256[asset_id],
            "authority": "exact outer planform and aperture",
        },
        "authoredConstruction": {
            "sourceFacing": "+X",
            "sourceUp": "+Y after glTF export",
            "floor": "Y=0 after glTF export",
            "planformSpanTiles": round(planform_span, 6),
            "heightTiles": round(span.z, 6),
            "objects": len(objects),
            "uniqueVertices": vertices,
            "uniqueTriangles": triangles,
            "materials": len(materials),
            "textureMaps": {
                "dimensions": [TEXTURE_SIZE, TEXTURE_SIZE],
                "albedo": 3,
                "normal": 1,
                "metallicRoughness": 1,
                "palette": "strict grayscale; renderer supplies team tint",
            },
            "semanticRoles": sorted(
                {
                    material.get("nilbotsRole")
                    for material in materials.values()
                }
            ),
        },
        "runtime": {
            "lookScale": LOOK_SCALES[asset_id],
            "rendererProjectileFactor": 0.72,
            "effectivePlanformSpanTiles": round(
                LOOK_SCALES[asset_id] * 0.72 * planform_span,
                6,
            ),
            "motion": {
                "trident-spark": "axial-spin",
                "rebound-diamond": "flat-spin",
                "lattice-rivet": "tumble",
            }[asset_id],
            "exclusions": [
                "trail",
                "glow pool",
                "impact",
                "miss",
                "deflection",
                "hitbox",
                "range",
                "speed",
                "camera",
                "light",
                "animation",
            ],
        },
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
            "review": str(
                (package_root / "review" / asset_id).relative_to(repo_root)
            ),
        },
    }


def main() -> None:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    artifacts_root = args.artifacts_root.resolve()
    package_root = repo_root / "art/class-models/class-projectiles"
    references = verify_inputs(repo_root, artifacts_root)
    assets = [
        build_asset(repo_root, package_root, asset_id)
        for asset_id in (
            "trident-spark",
            "rebound-diamond",
            "lattice-rivet",
        )
    ]
    report = {
        "schemaVersion": 1,
        "generator": str(Path(__file__).resolve().relative_to(repo_root)),
        "generatorSha256": sha256(Path(__file__).resolve()),
        "blender": {
            "requiredVersion": BLENDER_VERSION,
            "actualVersion": bpy.app.version_string,
        },
        "paidProviderCallsMade": 0,
        "sourcePolicy": (
            "canonical SVG geometry plus authored Blender primitives; audited "
            "provider masters are verified human references only"
        ),
        "providerReferences": references,
        "reviewPolicy": (
            "fixed views are local gates; final approval requires combined "
            "same-frame native V4 Frontline replay evidence"
        ),
        "assets": assets,
    }
    write_json(package_root / "build-report.json", report)
    print(
        json.dumps(
            {
                asset["assetId"]: {
                    "bytes": asset["artifacts"]["glb"]["bytes"],
                    "sha256": asset["artifacts"]["glb"]["sha256"],
                    "triangles": asset["authoredConstruction"][
                        "uniqueTriangles"
                    ],
                }
                for asset in assets
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
