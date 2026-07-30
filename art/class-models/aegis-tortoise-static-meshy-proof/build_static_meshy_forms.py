#!/usr/bin/env python3
"""Build a non-shipping static-form proof from the raw Meshy Aegis masters.

This intentionally keeps each provider master as one static whole-object
surface.  It performs only measured axis/scale/height normalization, direct
source decimation, texture downsampling/color grading, and an offline
material split for bounded team inlays.  It does not retopologize, rebuild,
segment for articulation, rig, animate, or install a runtime manifest.

Run with the pinned Blender 5.2 toolchain:

  Blender --background --factory-startup --python build_static_meshy_forms.py \
    -- --repo-root /absolute/path/to/worktree
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

import bpy
import numpy as np
from mathutils import Matrix, Vector


BLENDER_VERSION = "5.2.0"
TEAM_BLUE = "#22d3ee"
TEAM_RED = "#fb7185"
TEAM_NEUTRAL = "#ffffff"
RENDER_SIZE = 768


@dataclass(frozen=True)
class Tier:
    name: str
    target_triangles: int
    texture_size: int
    jpeg_quality: int


@dataclass(frozen=True)
class Form:
    asset_id: str
    task_id: str
    source_sha256: str
    source_facing: str
    height_factor: float
    approved_height_ratio: float
    tiers: tuple[Tier, ...]


FORMS = (
    Form(
        asset_id="aegis-tortoise-shell",
        task_id="019fb22d-c313-76f8-8eca-91ccbb46afd0",
        source_sha256=(
            "8a0a0129cc57afdf12a0c0e70159f2acd547b426501941330e4712d06b78c5ec"
        ),
        source_facing="-x",
        height_factor=0.30816831683168316 / 0.25552825552825553,
        approved_height_ratio=0.30816831683168316,
        tiers=(
            Tier("rich", 150_000, 2048, 92),
            Tier("lean", 75_000, 1024, 90),
        ),
    ),
    Form(
        asset_id="aegis-tortoise-turret",
        task_id="019fb232-6e09-787f-85cc-dbaea29fb9b6",
        source_sha256=(
            "a845f48ac9f0861c8ff2860de9c0dc79b0afa728794663f9d8ff70f7187f9cac"
        ),
        source_facing="+x",
        height_factor=0.26081582200247216 / 0.23832923832923833,
        approved_height_ratio=0.26081582200247216,
        tiers=(
            Tier("rich", 150_000, 2048, 92),
            Tier("lean", 75_000, 1024, 90),
        ),
    ),
)


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def repository_path(repo_root: Path, path: Path) -> str:
    return path.resolve().relative_to(repo_root).as_posix()


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.actions,
        bpy.data.worlds,
    ):
        for datablock in list(collection):
            if datablock.users == 0:
                collection.remove(datablock)


def import_glb(path: Path) -> list[bpy.types.Object]:
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    if not any(obj.type == "MESH" for obj in imported):
        raise RuntimeError(f"No mesh imported from {path}")
    return imported


def collective_bounds(
    meshes: Iterable[bpy.types.Object],
) -> tuple[Vector, Vector]:
    points: list[Vector] = []
    for obj in meshes:
        points.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
    return (
        Vector(
            (
                min(point.x for point in points),
                min(point.y for point in points),
                min(point.z for point in points),
            )
        ),
        Vector(
            (
                max(point.x for point in points),
                max(point.y for point in points),
                max(point.z for point in points),
            )
        ),
    )


def bake_world_transform(obj: bpy.types.Object, transform: Matrix) -> None:
    obj.matrix_world = transform @ obj.matrix_world
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def normalize_static_form(
    meshes: list[bpy.types.Object],
    form: Form,
) -> dict[str, Any]:
    yaw = (
        Matrix.Rotation(math.radians(180), 4, "Z")
        if form.source_facing == "-x"
        else Matrix.Identity(4)
    )
    rotated_points: list[Vector] = []
    for obj in meshes:
        matrix = yaw @ obj.matrix_world
        rotated_points.extend(matrix @ vertex.co for vertex in obj.data.vertices)
    minimum = Vector(
        tuple(min(point[axis] for point in rotated_points) for axis in range(3))
    )
    maximum = Vector(
        tuple(max(point[axis] for point in rotated_points) for axis in range(3))
    )
    span = maximum - minimum
    planform = max(span.x, span.y)
    scale = 1.0 / planform
    centre = (minimum + maximum) * 0.5
    transform = (
        Matrix.Translation(
            Vector((-centre.x * scale, -centre.y * scale, -minimum.z * scale))
        )
        @ Matrix.Scale(scale, 4)
        @ yaw
    )
    for obj in meshes:
        bake_world_transform(obj, transform)
        for vertex in obj.data.vertices:
            vertex.co.z *= form.height_factor
        obj.data.update()

    bpy.context.view_layer.update()
    normalized_minimum, normalized_maximum = collective_bounds(meshes)
    normalized_span = normalized_maximum - normalized_minimum
    return {
        "sourceFacing": form.source_facing,
        "sourceUp": "+y",
        "targetFacing": "+x",
        "targetUp": "+y",
        "dccUp": "+z",
        "uniformPlanformScale": scale,
        "heightOnlyFactor": form.height_factor,
        "operation": (
            "fixed inspected yaw; uniform one-tile planform scale; centre/floor "
            "translation; measured vertical-only side-profile correction"
        ),
        "boundsDcc": {
            "minimum": list(normalized_minimum),
            "maximum": list(normalized_maximum),
            "span": list(normalized_span),
        },
        "planformSpanTiles": max(normalized_span.x, normalized_span.y),
        "heightToPlanform": normalized_span.z
        / max(normalized_span.x, normalized_span.y),
        "approvedHeightToPlanform": form.approved_height_ratio,
    }


def triangle_count(meshes: Iterable[bpy.types.Object]) -> int:
    triangles = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    return triangles


def direct_decimate(
    meshes: list[bpy.types.Object],
    target_triangles: int,
) -> tuple[int, int]:
    before = triangle_count(meshes)
    if before <= target_triangles:
        return before, before
    ratio = target_triangles / before
    for obj in meshes:
        modifier = obj.modifiers.new("NB_DIRECT_SOURCE_DECIMATION", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = ratio
        modifier.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.data.update()
    after = triangle_count(meshes)
    return before, after


def image_pixels(image: bpy.types.Image) -> np.ndarray:
    result = np.empty(len(image.pixels), dtype=np.float32)
    image.pixels.foreach_get(result)
    return result.reshape((-1, 4))


def set_image_pixels(image: bpy.types.Image, pixels: np.ndarray) -> None:
    image.pixels.foreach_set(pixels.astype(np.float32, copy=False).reshape(-1))
    image.update()


def load_and_scale(
    source: Path,
    name: str,
    size: int,
    *,
    colorspace: str,
) -> bpy.types.Image:
    image = bpy.data.images.load(str(source), check_existing=False)
    image.name = name
    image.colorspace_settings.name = colorspace
    if image.size[0] != size or image.size[1] != size:
        image.scale(size, size)
    return image


def save_image(
    image: bpy.types.Image,
    path: Path,
    *,
    file_format: str,
    quality: int,
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.filepath_raw = str(path)
    image.file_format = file_format
    scene = bpy.context.scene
    scene.render.image_settings.quality = quality
    image.save()


def grade_base_color(image: bpy.types.Image) -> dict[str, float]:
    pixels = image_pixels(image)
    rgb = pixels[:, :3]
    cyan = (
        (rgb[:, 2] > 0.12)
        & (rgb[:, 1] > rgb[:, 0] * 1.32)
        & (rgb[:, 2] > rgb[:, 0] * 1.48)
    )
    bronze = (
        (~cyan)
        & (rgb[:, 0] > 0.045)
        & (rgb[:, 0] > rgb[:, 2] * 1.22)
        & (rgb[:, 1] > rgb[:, 2] * 1.05)
    )
    graphite = ~(cyan | bronze)

    # Keep Meshy's authored wear/microdetail while gently moving the palette
    # toward the approved Mobile's darker slate and weathered warm bronze.
    rgb[graphite] *= np.array((0.86, 0.90, 0.98), dtype=np.float32)
    rgb[bronze] *= np.array((0.90, 0.80, 0.70), dtype=np.float32)
    rgb[~cyan] = np.maximum(0.0, (rgb[~cyan] - 0.006) * 0.97)
    rgb[:] = np.clip(rgb, 0.0, 1.0)
    set_image_pixels(image, pixels)
    return {
        "cyanFraction": float(np.count_nonzero(cyan) / len(cyan)),
        "bronzeFraction": float(np.count_nonzero(bronze) / len(bronze)),
        "graphiteFraction": float(np.count_nonzero(graphite) / len(graphite)),
    }


def make_packed_metallic_roughness(
    metallic: bpy.types.Image,
    roughness: bpy.types.Image,
    name: str,
    size: int,
) -> tuple[bpy.types.Image, dict[str, float]]:
    metallic_pixels = image_pixels(metallic)
    roughness_pixels = image_pixels(roughness)
    metallic_value = np.clip(metallic_pixels[:, 0] * 0.82, 0.0, 0.92)
    roughness_value = np.clip(
        0.54 + roughness_pixels[:, 0] * 0.42,
        0.0,
        0.98,
    )
    packed = bpy.data.images.new(name, width=size, height=size, alpha=True)
    packed.colorspace_settings.name = "Non-Color"
    output = np.ones((size * size, 4), dtype=np.float32)
    output[:, 0] = 1.0
    output[:, 1] = roughness_value
    output[:, 2] = metallic_value
    set_image_pixels(packed, output)
    return packed, {
        "roughnessMean": float(np.mean(roughness_value)),
        "roughnessMinimum": float(np.min(roughness_value)),
        "roughnessMaximum": float(np.max(roughness_value)),
        "metallicMean": float(np.mean(metallic_value)),
        "metallicMaximum": float(np.max(metallic_value)),
    }


def make_textures(
    repo_root: Path,
    form: Form,
    tier: Tier,
    source_dir: Path,
    output_dir: Path,
) -> tuple[dict[str, bpy.types.Image], dict[str, Any]]:
    size = tier.texture_size
    base = load_and_scale(
        source_dir / "texture-0-base-color.png",
        f"{form.asset_id}-{tier.name}-base-color",
        size,
        colorspace="sRGB",
    )
    base_stats = grade_base_color(base)
    normal = load_and_scale(
        source_dir / "texture-0-normal.png",
        f"{form.asset_id}-{tier.name}-normal",
        size,
        colorspace="Non-Color",
    )
    emission = load_and_scale(
        source_dir / "texture-0-emission.png",
        f"{form.asset_id}-{tier.name}-emission",
        size,
        colorspace="sRGB",
    )
    metallic = load_and_scale(
        source_dir / "texture-0-metallic.png",
        f"{form.asset_id}-{tier.name}-metallic-source",
        size,
        colorspace="Non-Color",
    )
    roughness = load_and_scale(
        source_dir / "texture-0-roughness.png",
        f"{form.asset_id}-{tier.name}-roughness-source",
        size,
        colorspace="Non-Color",
    )
    packed, pbr_stats = make_packed_metallic_roughness(
        metallic,
        roughness,
        f"{form.asset_id}-{tier.name}-metallic-roughness",
        size,
    )

    paths = {
        "baseColor": output_dir / "base-color.jpg",
        "normal": output_dir / "normal.jpg",
        "metallicRoughness": output_dir / "metallic-roughness.jpg",
        "emission": output_dir / "emission.png",
    }
    save_image(
        base,
        paths["baseColor"],
        file_format="JPEG",
        quality=tier.jpeg_quality,
    )
    save_image(
        normal,
        paths["normal"],
        file_format="JPEG",
        quality=tier.jpeg_quality,
    )
    save_image(
        packed,
        paths["metallicRoughness"],
        file_format="JPEG",
        quality=tier.jpeg_quality,
    )
    save_image(
        emission,
        paths["emission"],
        file_format="PNG",
        quality=100,
    )

    # Reload the exact encoded files so the GLB embeds the reviewed bytes.
    for image in (base, normal, packed, emission, metallic, roughness):
        bpy.data.images.remove(image)
    exact = {
        key: bpy.data.images.load(str(path), check_existing=False)
        for key, path in paths.items()
    }
    exact["baseColor"].colorspace_settings.name = "sRGB"
    exact["normal"].colorspace_settings.name = "Non-Color"
    exact["metallicRoughness"].colorspace_settings.name = "Non-Color"
    exact["emission"].colorspace_settings.name = "sRGB"
    return exact, {
        "dimensions": [size, size],
        "jpegQuality": tier.jpeg_quality,
        "colorGrade": base_stats,
        "pbrGrade": pbr_stats,
        "files": {
            role: {
                "path": repository_path(repo_root, path),
                "bytes": path.stat().st_size,
                "sha256": sha256(path),
            }
            for role, path in paths.items()
        },
    }


def image_texture_node(
    nodes: bpy.types.Nodes,
    image: bpy.types.Image,
    name: str,
) -> bpy.types.Node:
    node = nodes.new("ShaderNodeTexImage")
    node.name = name
    node.label = name
    node.image = image
    node.interpolation = "Linear"
    return node


def make_hull_material(
    form: Form,
    tier: Tier,
    textures: dict[str, bpy.types.Image],
) -> bpy.types.Material:
    material = bpy.data.materials.new(f"NB_{form.asset_id}_{tier.name}_HULL")
    material.use_nodes = True
    material["nilbotsRole"] = "hull"
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    base = image_texture_node(nodes, textures["baseColor"], "Base Color")
    normal = image_texture_node(nodes, textures["normal"], "Normal")
    packed = image_texture_node(
        nodes,
        textures["metallicRoughness"],
        "Metallic Roughness",
    )
    emission = image_texture_node(nodes, textures["emission"], "Emission")
    separate = nodes.new("ShaderNodeSeparateColor")
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.82
    principled.inputs["Base Color"].default_value = (0.11, 0.15, 0.22, 1.0)
    principled.inputs["Metallic"].default_value = 0.35
    principled.inputs["Roughness"].default_value = 0.72
    principled.inputs["Emission Strength"].default_value = 1.0
    links.new(base.outputs["Color"], principled.inputs["Base Color"])
    links.new(packed.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], principled.inputs["Roughness"])
    links.new(separate.outputs["Blue"], principled.inputs["Metallic"])
    links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])
    links.new(emission.outputs["Color"], principled.inputs["Emission Color"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    material.diffuse_color = (0.11, 0.15, 0.22, 1.0)
    return material


def make_team_material(
    form: Form,
    tier: Tier,
    textures: dict[str, bpy.types.Image],
) -> bpy.types.Material:
    material = bpy.data.materials.new(f"NB_{form.asset_id}_{tier.name}_TEAM")
    material.use_nodes = True
    material["nilbotsRole"] = "team-accent"
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    normal = image_texture_node(nodes, textures["normal"], "Normal")
    packed = image_texture_node(
        nodes,
        textures["metallicRoughness"],
        "Metallic Roughness",
    )
    separate = nodes.new("ShaderNodeSeparateColor")
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.82
    principled.inputs["Base Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    principled.inputs["Emission Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    principled.inputs["Emission Strength"].default_value = 1.0
    links.new(packed.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], principled.inputs["Roughness"])
    links.new(separate.outputs["Blue"], principled.inputs["Metallic"])
    links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    material.diffuse_color = (1.0, 1.0, 1.0, 1.0)
    return material


TEAM_SAMPLES = (
    (1 / 3, 1 / 3, 1 / 3),
    (0.60, 0.20, 0.20),
    (0.20, 0.60, 0.20),
    (0.20, 0.20, 0.60),
    (0.45, 0.45, 0.10),
    (0.45, 0.10, 0.45),
    (0.10, 0.45, 0.45),
)


def sample_rgba(
    pixels: np.ndarray,
    width: int,
    height: int,
    u: float,
    v: float,
) -> np.ndarray:
    x = min(width - 1, max(0, int((u % 1.0) * width)))
    y = min(height - 1, max(0, int((v % 1.0) * height)))
    return pixels[y * width + x]


def cyan_sample(color: np.ndarray) -> bool:
    red, green, blue = color[:3]
    return (
        blue > 0.12
        and green > red * 1.32
        and blue > red * 1.48
        and (green + blue) > red * 2.9
    )


def polygon_is_bounded_team_region(
    form: Form,
    centre: Vector,
    normal: Vector,
) -> bool:
    if normal.z < 0.34:
        return False
    x = centre.x
    y = centre.y
    if form.asset_id.endswith("shell"):
        guard_bars = x > 0.10 and abs(y) > 0.105
        west_panel = x < -0.16 and abs(y) < 0.16
        return guard_bars or west_panel
    radius = math.hypot(x, y)
    axis_distance = min(abs(x), abs(y))
    return 0.20 < radius < 0.49 and axis_distance < 0.145


def assign_semantic_team_faces(
    meshes: list[bpy.types.Object],
    form: Form,
    base_image: bpy.types.Image,
    hull: bpy.types.Material,
    team: bpy.types.Material,
) -> dict[str, Any]:
    pixels = image_pixels(base_image)
    width, height = base_image.size
    selected = 0
    rejected_cyan = 0
    total = 0
    selected_area = 0.0
    total_area = 0.0
    regions: dict[str, int] = {}

    for obj in meshes:
        mesh = obj.data
        mesh.materials.clear()
        mesh.materials.append(hull)
        mesh.materials.append(team)
        uv_layer = mesh.uv_layers.active
        if uv_layer is None:
            raise RuntimeError(f"{obj.name} has no active UV layer")
        mesh.calc_loop_triangles()
        for polygon in mesh.polygons:
            total += 1
            total_area += polygon.area
            loops = [mesh.loops[index] for index in polygon.loop_indices]
            if len(loops) != 3:
                polygon.material_index = 0
                continue
            uvs = [uv_layer.data[loop.index].uv.copy() for loop in loops]
            votes = 0
            for left, middle, right in TEAM_SAMPLES:
                uv = uvs[0] * left + uvs[1] * middle + uvs[2] * right
                if cyan_sample(sample_rgba(pixels, width, height, uv.x, uv.y)):
                    votes += 1
            candidate = votes >= 5
            centre = polygon.center
            bounded = polygon_is_bounded_team_region(
                form,
                centre,
                polygon.normal,
            )
            if candidate and bounded:
                polygon.material_index = 1
                selected += 1
                selected_area += polygon.area
                if form.asset_id.endswith("shell"):
                    key = (
                        "guard-north"
                        if centre.x > 0.10 and centre.y > 0
                        else "guard-south"
                        if centre.x > 0.10
                        else "west-centre"
                    )
                else:
                    key = (
                        "east"
                        if abs(centre.x) >= abs(centre.y) and centre.x >= 0
                        else "west"
                        if abs(centre.x) >= abs(centre.y)
                        else "north"
                        if centre.y >= 0
                        else "south"
                    )
                regions[key] = regions.get(key, 0) + 1
            else:
                polygon.material_index = 0
                if candidate:
                    rejected_cyan += 1
        mesh.update()
        try:
            mesh.calc_tangents(uvmap=uv_layer.name)
        except RuntimeError as error:
            raise RuntimeError(f"Tangent generation failed for {obj.name}") from error

    if selected == 0:
        raise RuntimeError(f"No bounded team faces selected for {form.asset_id}")
    return {
        "method": (
            "seven interior UV samples, five-of-seven cyan vote, then explicit "
            "top-surface world-space region gate"
        ),
        "selectedFaces": selected,
        "rejectedCyanFaces": rejected_cyan,
        "totalFaces": total,
        "selectedSurfaceArea": selected_area,
        "totalSurfaceArea": total_area,
        "selectedAreaFraction": selected_area / total_area,
        "regions": regions,
    }


def select_only(objects: Iterable[bpy.types.Object]) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    objects = list(objects)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def export_glb(path: Path, meshes: list[bpy.types.Object], quality: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    select_only(meshes)
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_texcoords=True,
        export_normals=True,
        export_tangents=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_image_quality=quality,
        export_jpeg_quality=quality,
        export_extras=True,
        export_animations=False,
        export_skins=False,
        export_morph=False,
        export_cameras=False,
        export_lights=False,
        export_unused_images=False,
        export_unused_textures=False,
    )


def parse_glb(path: Path) -> dict[str, Any]:
    with path.open("rb") as stream:
        magic, version, length = struct.unpack("<4sII", stream.read(12))
        if magic != b"glTF" or version != 2 or length != path.stat().st_size:
            raise RuntimeError(f"Invalid GLB header: {path}")
        json_length, json_type = struct.unpack("<II", stream.read(8))
        if json_type != 0x4E4F534A:
            raise RuntimeError(f"First GLB chunk is not JSON: {path}")
        document = json.loads(stream.read(json_length).rstrip(b" \t\r\n\x00"))
    return document


def add_primitive_semantic_extras(path: Path) -> None:
    """Mirror the material role onto its primitive without touching payloads."""

    source = path.read_bytes()
    magic, version, _length = struct.unpack("<4sII", source[:12])
    if magic != b"glTF" or version != 2:
        raise RuntimeError(f"Invalid GLB header: {path}")
    offset = 12
    chunks: list[tuple[int, bytes]] = []
    while offset < len(source):
        chunk_length, chunk_type = struct.unpack("<II", source[offset : offset + 8])
        offset += 8
        chunks.append((chunk_type, source[offset : offset + chunk_length]))
        offset += chunk_length
    if not chunks or chunks[0][0] != 0x4E4F534A:
        raise RuntimeError(f"First GLB chunk is not JSON: {path}")
    document = json.loads(chunks[0][1].rstrip(b" \t\r\n\x00"))
    materials = document.get("materials", [])
    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            material_index = primitive.get("material")
            role = (
                materials[material_index].get("extras", {}).get("nilbotsRole")
                if material_index is not None
                else None
            )
            if role:
                primitive["extras"] = {
                    **primitive.get("extras", {}),
                    "nilbotsRole": role,
                }
    json_bytes = json.dumps(
        document,
        separators=(",", ":"),
        ensure_ascii=True,
    ).encode("utf-8")
    json_bytes += b" " * ((4 - len(json_bytes) % 4) % 4)
    output_chunks = [(0x4E4F534A, json_bytes), *chunks[1:]]
    total_length = 12 + sum(8 + len(payload) for _, payload in output_chunks)
    rebuilt = bytearray(struct.pack("<4sII", b"glTF", 2, total_length))
    for chunk_type, payload in output_chunks:
        rebuilt.extend(struct.pack("<II", len(payload), chunk_type))
        rebuilt.extend(payload)
    path.write_bytes(rebuilt)


def repair_degenerate_tangents(path: Path) -> int:
    """Repair the rare zero tangent created at a collapsed degenerate UV."""

    source = path.read_bytes()
    offset = 12
    chunks: list[tuple[int, bytes]] = []
    while offset < len(source):
        chunk_length, chunk_type = struct.unpack("<II", source[offset : offset + 8])
        offset += 8
        chunks.append((chunk_type, source[offset : offset + chunk_length]))
        offset += chunk_length
    document = json.loads(chunks[0][1].rstrip(b" \t\r\n\x00"))
    binary_index = next(
        index for index, (chunk_type, _payload) in enumerate(chunks)
        if chunk_type == 0x004E4942
    )
    binary = bytearray(chunks[binary_index][1])
    repaired = 0
    processed: set[int] = set()

    def accessor_offset(accessor: dict[str, Any], element: int) -> int:
        view = document["bufferViews"][accessor["bufferView"]]
        stride = view.get(
            "byteStride",
            16 if accessor["type"] == "VEC4" else 12,
        )
        return (
            view.get("byteOffset", 0)
            + accessor.get("byteOffset", 0)
            + element * stride
        )

    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            tangent_index = primitive.get("attributes", {}).get("TANGENT")
            normal_index = primitive.get("attributes", {}).get("NORMAL")
            if (
                tangent_index is None
                or normal_index is None
                or tangent_index in processed
            ):
                continue
            processed.add(tangent_index)
            tangent_accessor = document["accessors"][tangent_index]
            normal_accessor = document["accessors"][normal_index]
            if (
                tangent_accessor.get("componentType") != 5126
                or tangent_accessor.get("type") != "VEC4"
                or normal_accessor.get("componentType") != 5126
                or normal_accessor.get("type") != "VEC3"
                or tangent_accessor["count"] != normal_accessor["count"]
            ):
                raise RuntimeError(f"Unsupported tangent layout in {path}")
            for element in range(tangent_accessor["count"]):
                tangent_offset = accessor_offset(tangent_accessor, element)
                x, y, z, handedness = struct.unpack_from(
                    "<4f",
                    binary,
                    tangent_offset,
                )
                length = math.sqrt(x * x + y * y + z * z)
                if math.isfinite(length) and length > 0.999 and length < 1.001:
                    continue
                normal_offset = accessor_offset(normal_accessor, element)
                nx, ny, nz = struct.unpack_from("<3f", binary, normal_offset)
                normal_length = math.sqrt(nx * nx + ny * ny + nz * nz)
                if not math.isfinite(normal_length) or normal_length < 1e-8:
                    nx, ny, nz = (0.0, 1.0, 0.0)
                else:
                    nx /= normal_length
                    ny /= normal_length
                    nz /= normal_length
                if abs(ny) < 0.9:
                    tx, ty, tz = (-nz, 0.0, nx)
                else:
                    tx, ty, tz = (0.0, nz, -ny)
                tangent_length = math.sqrt(tx * tx + ty * ty + tz * tz)
                tx /= tangent_length
                ty /= tangent_length
                tz /= tangent_length
                if handedness not in (-1.0, 1.0):
                    handedness = 1.0
                struct.pack_into(
                    "<4f",
                    binary,
                    tangent_offset,
                    tx,
                    ty,
                    tz,
                    handedness,
                )
                repaired += 1
    chunks[binary_index] = (chunks[binary_index][0], bytes(binary))
    total_length = 12 + sum(8 + len(payload) for _, payload in chunks)
    rebuilt = bytearray(struct.pack("<4sII", b"glTF", 2, total_length))
    for chunk_type, payload in chunks:
        rebuilt.extend(struct.pack("<II", len(payload), chunk_type))
        rebuilt.extend(payload)
    path.write_bytes(rebuilt)
    return repaired


def glb_report(path: Path) -> dict[str, Any]:
    document = parse_glb(path)
    accessors = document.get("accessors", [])
    primitives = [
        primitive
        for mesh in document.get("meshes", [])
        for primitive in mesh.get("primitives", [])
    ]
    triangles = sum(
        accessors[primitive["indices"]]["count"] // 3
        if "indices" in primitive
        else accessors[primitive["attributes"]["POSITION"]]["count"] // 3
        for primitive in primitives
    )
    roles = [
        material.get("extras", {}).get("nilbotsRole")
        for material in document.get("materials", [])
    ]
    primitive_roles = [
        primitive.get("extras", {}).get("nilbotsRole")
        for primitive in primitives
    ]
    return {
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
        "meshes": len(document.get("meshes", [])),
        "primitives": len(primitives),
        "vertices": sum(
            accessors[primitive["attributes"]["POSITION"]]["count"]
            for primitive in primitives
        ),
        "triangles": triangles,
        "materials": len(document.get("materials", [])),
        "materialRoles": roles,
        "primitiveRoles": primitive_roles,
        "images": len(document.get("images", [])),
        "imageMimeTypes": [
            image.get("mimeType") for image in document.get("images", [])
        ],
        "animations": len(document.get("animations", [])),
        "skins": len(document.get("skins", [])),
        "cameras": len(document.get("cameras", [])),
        "lights": len(
            document.get("extensions", {})
            .get("KHR_lights_punctual", {})
            .get("lights", [])
        ),
        "tangentPrimitiveCount": sum(
            1 for primitive in primitives if "TANGENT" in primitive["attributes"]
        ),
    }


def hex_to_linear_rgba(value: str) -> tuple[float, float, float, float]:
    value = value.lstrip("#")
    channels = [int(value[index : index + 2], 16) / 255 for index in (0, 2, 4)]
    linear = [
        channel / 12.92
        if channel <= 0.04045
        else ((channel + 0.055) / 1.055) ** 2.4
        for channel in channels
    ]
    return (*linear, 1.0)


def set_team_color(material: bpy.types.Material, value: str) -> None:
    color = hex_to_linear_rgba(value)
    material.diffuse_color = color
    principled = next(
        node
        for node in material.node_tree.nodes
        if node.type == "BSDF_PRINCIPLED"
    )
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Emission Color"].default_value = color


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def simple_material(
    name: str,
    color: str,
    *,
    metallic: float,
    roughness: float,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    principled = next(
        node
        for node in material.node_tree.nodes
        if node.type == "BSDF_PRINCIPLED"
    )
    rgba = hex_to_linear_rgba(color)
    principled.inputs["Base Color"].default_value = rgba
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    material.diffuse_color = rgba
    return material


def add_box(
    name: str,
    dimensions: tuple[float, float, float],
    location: tuple[float, float, float],
    material: bpy.types.Material,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    return obj


def configure_review_scene() -> tuple[bpy.types.Object, bpy.types.Object]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = RENDER_SIZE
    scene.render.resolution_y = RENDER_SIZE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 15
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -1.15

    world = bpy.data.worlds.new("Aegis Static Proof World")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = hex_to_linear_rgba("#0b1020")
    background.inputs["Strength"].default_value = 0.45

    floor_material = simple_material(
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
    )

    lights = (
        (
            "REVIEW.Key",
            (-1.6, -1.8, 2.9),
            320,
            3.0,
            (0.90, 0.95, 1.0),
            (0, 0, 0.12),
        ),
        (
            "REVIEW.Fill",
            (1.8, 0.8, 1.1),
            135,
            2.2,
            (0.34, 0.48, 0.72),
            (0, 0, 0.12),
        ),
        (
            "REVIEW.Rim",
            (-0.8, 1.8, 1.8),
            190,
            1.5,
            (0.86, 0.64, 0.36),
            (0, 0, 0.15),
        ),
        (
            "REVIEW.SideFill",
            (0, -2, 1.25),
            125,
            2.0,
            (0.72, 0.82, 1.0),
            (0, 0, 0.11),
        ),
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
    bpy.context.scene.collection.objects.link(camera)
    scene.camera = camera
    return floor, camera


def mask_material() -> bpy.types.Material:
    material = bpy.data.materials.new("REVIEW_MASK")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (1, 1, 1, 1)
    emission.inputs["Strength"].default_value = 1.0
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return material


def render_view(
    path: Path,
    camera: bpy.types.Object,
    floor: bpy.types.Object,
    view: str,
    *,
    size: int = RENDER_SIZE,
    override: bpy.types.Material | None = None,
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.view_layers[0].material_override = override
    floor.hide_render = view != "gameplay"
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
    elif view == "gameplay":
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
    scene.view_layers[0].material_override = None


def render_candidate(
    output: Path,
    team: bpy.types.Material,
) -> None:
    floor, camera = configure_review_scene()
    set_team_color(team, TEAM_BLUE)
    render_view(output / "gameplay-blue.png", camera, floor, "gameplay")
    render_view(
        output / "gameplay-blue-192.png",
        camera,
        floor,
        "gameplay",
        size=192,
    )
    set_team_color(team, TEAM_RED)
    render_view(output / "gameplay-red.png", camera, floor, "gameplay")
    render_view(
        output / "gameplay-red-192.png",
        camera,
        floor,
        "gameplay",
        size=192,
    )
    set_team_color(team, TEAM_NEUTRAL)
    render_view(output / "top.png", camera, floor, "top")
    render_view(output / "side.png", camera, floor, "side")
    mask = mask_material()
    render_view(output / "top-mask.png", camera, floor, "top", override=mask)
    render_view(output / "side-mask.png", camera, floor, "side", override=mask)


def top_level_imported(imported: list[bpy.types.Object]) -> list[bpy.types.Object]:
    imported_set = set(imported)
    return [obj for obj in imported if obj.parent not in imported_set]


def render_mobile_reference(
    repo_root: Path,
    reference: Path,
    output: Path,
) -> dict[str, Any]:
    clear_scene()
    imported = import_glb(reference)
    meshes = [obj for obj in imported if obj.type == "MESH"]
    armatures = [obj for obj in imported if obj.type == "ARMATURE"]
    idle = bpy.data.actions.get("Aegis_Idle")
    if armatures and idle is not None:
        if armatures[0].animation_data is None:
            armatures[0].animation_data_create()
        armatures[0].animation_data.action = idle
        bpy.context.scene.frame_set(1)
        bpy.context.view_layer.update()

    minimum, maximum = collective_bounds(meshes)
    planform = max(maximum.x - minimum.x, maximum.y - minimum.y)
    scale = 1.0 / planform
    centre = (minimum + maximum) * 0.5
    root = bpy.data.objects.new("REVIEW.MobileOneTileRoot", None)
    bpy.context.scene.collection.objects.link(root)
    for obj in top_level_imported(imported):
        world = obj.matrix_world.copy()
        obj.parent = root
        obj.matrix_world = world
    root.scale = (scale, scale, scale)
    root.location = (
        -centre.x * scale,
        -centre.y * scale,
        -minimum.z * scale,
    )
    bpy.context.view_layer.update()

    teams = [
        material
        for material in bpy.data.materials
        if material.get("nilbotsRole") == "team-accent"
    ]
    if len(teams) != 1:
        raise RuntimeError(
            f"Mobile reference exposes {len(teams)} team materials, expected 1"
        )
    floor, camera = configure_review_scene()
    team = teams[0]
    set_team_color(team, TEAM_BLUE)
    render_view(output / "gameplay-blue.png", camera, floor, "gameplay")
    render_view(
        output / "gameplay-blue-192.png",
        camera,
        floor,
        "gameplay",
        size=192,
    )
    set_team_color(team, TEAM_RED)
    render_view(output / "gameplay-red.png", camera, floor, "gameplay")
    render_view(
        output / "gameplay-red-192.png",
        camera,
        floor,
        "gameplay",
        size=192,
    )
    return {
        "file": repository_path(repo_root, reference),
        "bytes": reference.stat().st_size,
        "sha256": sha256(reference),
        "sourcePlanformSpan": planform,
        "reviewUniformScale": scale,
        "teamAccentMaterialCount": 1,
    }


def build_form_tier(
    repo_root: Path,
    proof_root: Path,
    form: Form,
    tier: Tier,
) -> dict[str, Any]:
    clear_scene()
    source_dir = (
        repo_root
        / "var/model-artifacts/meshy"
        / form.asset_id
        / form.task_id
    )
    source = source_dir / "raw-model.glb"
    if sha256(source) != form.source_sha256:
        raise RuntimeError(f"Source hash mismatch for {form.asset_id}")
    imported = import_glb(source)
    meshes = [obj for obj in imported if obj.type == "MESH"]
    normalization = normalize_static_form(meshes, form)
    source_triangles, output_triangles = direct_decimate(
        meshes,
        tier.target_triangles,
    )
    tier_root = proof_root / "candidate" / form.asset_id / tier.name
    textures, texture_report = make_textures(
        repo_root,
        form,
        tier,
        source_dir,
        tier_root / "textures",
    )
    hull = make_hull_material(form, tier, textures)
    team = make_team_material(form, tier, textures)
    team_report = assign_semantic_team_faces(
        meshes,
        form,
        textures["baseColor"],
        hull,
        team,
    )

    glb = tier_root / f"{form.asset_id}-{tier.name}.glb"
    set_team_color(team, TEAM_NEUTRAL)
    export_glb(glb, meshes, tier.jpeg_quality)
    add_primitive_semantic_extras(glb)
    tangent_repairs = repair_degenerate_tangents(glb)
    model_report = glb_report(glb)
    model_report["repairedDegenerateTangents"] = tangent_repairs
    render_candidate(tier_root / "review", team)
    return {
        "assetId": form.asset_id,
        "tier": tier.name,
        "source": {
            "taskId": form.task_id,
            "file": repository_path(repo_root, source),
            "bytes": source.stat().st_size,
            "sha256": sha256(source),
        },
        "transformationClass": (
            "deterministic direct-source optimization; no visible-surface "
            "rebuild, retopology, articulation, rig, or animation"
        ),
        "normalization": normalization,
        "geometry": {
            "sourceTriangles": source_triangles,
            "targetTriangles": tier.target_triangles,
            "outputTrianglesDcc": output_triangles,
            "directDecimationRatio": output_triangles / source_triangles,
        },
        "textures": texture_report,
        "teamAccent": team_report,
        "model": {
            "file": repository_path(repo_root, glb),
            **model_report,
        },
    }


def main() -> None:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    proof_root = (
        repo_root / "art/class-models/aegis-tortoise-static-meshy-proof"
    )
    reports: list[dict[str, Any]] = []
    for form in FORMS:
        for tier in form.tiers:
            print(f"Building {form.asset_id} {tier.name}...", flush=True)
            reports.append(build_form_tier(repo_root, proof_root, form, tier))

    mobile = render_mobile_reference(
        repo_root,
        repo_root / "var/model-artifacts/reference/aegis-tortoise-mobile.glb",
        proof_root / "candidate/aegis-tortoise-mobile/reference/review",
    )
    report = {
        "schemaVersion": 1,
        "status": "non-shipping-review-proof",
        "blender": {
            "version": bpy.app.version_string,
            "expectedVersion": BLENDER_VERSION,
            "script": str(Path(__file__).relative_to(repo_root)),
        },
        "policy": {
            "providerCallsMade": 0,
            "rawMastersPreservedUnmodified": True,
            "visibleSurfaceRebuilt": False,
            "retopologyUsed": False,
            "rigsOrAnimationsAdded": False,
            "runtimeWiringAdded": False,
            "transformEffectOwner": "renderer-owned future energy dissolve/assembly",
        },
        "mobileReference": mobile,
        "candidates": reports,
    }
    report_path = proof_root / "reports/build-report.json"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(report, indent=2, sort_keys=True), flush=True)


if __name__ == "__main__":
    main()
