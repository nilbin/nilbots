#!/usr/bin/env python3
"""Blender-side Bulwark Mobile runtime tier packager.

Invoke only through ``package_bulwark_runtime.py``. The script opens one
hash-pinned local art master, evaluates its frame-1 bind pose, removes authored
clips from exported derivatives, floor-aligns the model, resizes only embedded
body maps, and exports fixed 2K/1K/512 GLBs. It also emits fixed neutral/blue/
orange gameplay and top views at 512, 140, and 41 pixels.

The script never imports provider or networking libraries and never writes
outside its newly-created sandbox/ or var/ output directory.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import struct
import sys
import traceback
import zlib
from pathlib import Path
from typing import Any, Iterable

import bpy
from mathutils import Matrix, Vector


SCHEMA = "nilbots-bulwark-runtime-package-1"
TOOL_VERSION = "1.0.0"
EXPECTED_BLENDER = (5, 2)
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
BODY_GROUPS = (
    "ChassisCarrier",
    "ShieldTrim_North",
    "ShieldLobe_South",
    "GunBarrels",
    "CrownReactor",
    "Pod_NorthOuter",
    "Pod_NorthInner",
    "Pod_SouthInner",
    "Pod_SouthOuter",
)
TIERS = (
    {
        "id": "source-2k",
        "mapSize": 2048,
        "imageFormat": "AUTO",
        "jpegQuality": 100,
        "purpose": "source-quality reference",
    },
    {
        "id": "runtime-1k",
        "mapSize": 1024,
        "imageFormat": "JPEG",
        "jpegQuality": 92,
        "purpose": "candidate on-demand runtime tier",
    },
    {
        "id": "comparison-512",
        "mapSize": 512,
        "imageFormat": "JPEG",
        "jpegQuality": 90,
        "purpose": "lean gameplay comparison tier",
    },
)
TEAMS = (
    ("neutral", "#d9e3ea", 0.12),
    ("blue", "#4da3ff", 0.72),
    ("orange", "#ff7a32", 0.72),
)
VIEWS = ("gameplay", "top")
FULL_RENDER_SIZE = 512
COMPARISON_SIZES = (140, 41)
GLB_JSON_CHUNK = 0x4E4F534A
GLB_BIN_CHUNK = 0x004E4942
GL_TRIANGLES = 4


def blender_arguments() -> list[str]:
    argv = sys.argv
    return argv[argv.index("--") + 1 :] if "--" in argv else []


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
            "Package an accented Bulwark Mobile art master into deterministic "
            "offline GLB tiers and fixed gameplay-scale evidence."
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


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def resolve_repo_relative(
    value: Path,
    *,
    repo_root: Path,
    option: str,
    strict: bool,
) -> tuple[Path, Path]:
    if value.is_absolute() or not value.parts:
        raise ValueError(f"{option} must be a non-empty repository-relative path")
    resolved = (repo_root / value).resolve(strict=strict)
    try:
        relative = resolved.relative_to(repo_root)
    except ValueError as error:
        raise ValueError(f"{option} must stay inside the repository") from error
    return resolved, relative


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def rounded(value: float, digits: int = 9) -> float:
    result = round(float(value), digits)
    return 0.0 if result == -0.0 else result


def vector_json(value: Iterable[float]) -> list[float]:
    return [rounded(component) for component in value]


def hex_linear_rgba(value: str) -> tuple[float, float, float, float]:
    channels = [int(value[index : index + 2], 16) / 255.0 for index in (1, 3, 5)]
    linear = [
        channel / 12.92
        if channel <= 0.04045
        else ((channel + 0.055) / 1.055) ** 2.4
        for channel in channels
    ]
    return (*linear, 1.0)


def triangle_count(mesh: bpy.types.Mesh) -> int:
    return sum(max(0, len(polygon.vertices) - 2) for polygon in mesh.polygons)


def descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    result: list[bpy.types.Object] = []
    stack = [root]
    while stack:
        current = stack.pop()
        result.append(current)
        stack.extend(sorted(current.children, key=lambda item: item.name, reverse=True))
    return result


def world_bounds(
    objects: Iterable[bpy.types.Object],
) -> tuple[Vector, Vector]:
    points = [
        obj.matrix_world @ vertex.co
        for obj in objects
        if obj.type == "MESH"
        for vertex in obj.data.vertices
    ]
    if not points:
        raise ValueError("Model contains no mesh vertices")
    minimum = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return minimum, maximum


def image_nodes_upstream(
    socket: bpy.types.NodeSocket,
    *,
    visited: set[int] | None = None,
) -> set[bpy.types.Image]:
    visited = set() if visited is None else visited
    images: set[bpy.types.Image] = set()
    for link in socket.links:
        node = link.from_node
        pointer = node.as_pointer()
        if pointer in visited:
            continue
        visited.add(pointer)
        if node.type == "TEX_IMAGE" and node.image is not None:
            images.add(node.image)
        for input_socket in node.inputs:
            if input_socket.is_linked:
                images.update(image_nodes_upstream(input_socket, visited=visited))
    return images


def principled(material: bpy.types.Material) -> bpy.types.Node:
    if not material.use_nodes or material.node_tree is None:
        raise ValueError(f"Material {material.name!r} has no node graph")
    nodes = [
        node
        for node in material.node_tree.nodes
        if node.type == "BSDF_PRINCIPLED"
    ]
    if len(nodes) != 1:
        raise ValueError(
            f"Material {material.name!r} must have exactly one Principled BSDF"
        )
    return nodes[0]


def material_pbr_images(
    material: bpy.types.Material,
) -> dict[str, set[bpy.types.Image]]:
    shader = principled(material)

    def images_for(*socket_names: str) -> set[bpy.types.Image]:
        found: set[bpy.types.Image] = set()
        for socket_name in socket_names:
            socket = shader.inputs.get(socket_name)
            if socket is not None:
                found.update(image_nodes_upstream(socket))
        return found

    return {
        "baseColor": images_for("Base Color"),
        "metallicRoughness": images_for("Metallic", "Roughness"),
        "normal": images_for("Normal"),
        "emissive": images_for("Emission Color", "Emission"),
    }


def material_has_image_nodes(material: bpy.types.Material) -> bool:
    return bool(
        material.use_nodes
        and material.node_tree is not None
        and any(node.type == "TEX_IMAGE" for node in material.node_tree.nodes)
    )


def snapshot_bind_pose(objects: list[bpy.types.Object]) -> None:
    scene = bpy.context.scene
    scene.frame_set(1)
    bpy.context.view_layer.update()
    local_matrices = {obj.name: obj.matrix_local.copy() for obj in objects}
    for obj in objects:
        if obj.animation_data is not None:
            obj.animation_data_clear()
    for obj in objects:
        obj.matrix_local = local_matrices[obj.name]
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    scene.frame_set(1)
    bpy.context.view_layer.update()


def inspect_and_prepare_model() -> dict[str, Any]:
    root = bpy.data.objects.get("BulwarkMobile_Root")
    if root is None or root.type != "EMPTY":
        raise ValueError("Art master has no BulwarkMobile_Root empty")
    if root.get("facing_axis_blender") != "+X":
        raise ValueError("Art master does not pin +X as its Blender facing axis")
    if root.get("up_axis_blender") != "+Z":
        raise ValueError("Art master does not pin +Z as its Blender up axis")

    model_objects = descendants(root)
    snapshot_bind_pose(model_objects)

    model_names = {obj.name for obj in model_objects}
    for obj in list(bpy.data.objects):
        if obj.name not in model_names:
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.update()

    group_objects: dict[str, bpy.types.Object] = {}
    for group in BODY_GROUPS:
        candidates = [
            obj
            for obj in model_objects
            if obj.type == "MESH" and obj.get("semantic_group") == group
        ]
        if len(candidates) != 1:
            raise ValueError(
                f"Expected exactly one mesh for semantic group {group!r}; "
                f"found {len(candidates)}"
            )
        group_objects[group] = candidates[0]

    body_meshes = set(group_objects.values())
    body_materials = {
        slot.material
        for obj in body_meshes
        for slot in obj.material_slots
        if slot.material is not None
    }
    if not body_materials:
        raise ValueError("The nine body groups have no materials")
    body_pbr: dict[str, dict[str, set[bpy.types.Image]]] = {}
    for material in sorted(body_materials, key=lambda item: item.name):
        role_images = material_pbr_images(material)
        missing = [role for role, images in role_images.items() if not images]
        if missing:
            raise ValueError(
                f"Body material {material.name!r} is missing PBR image roles: "
                + ", ".join(missing)
            )
        body_pbr[material.name] = role_images

    accent_materials = {
        slot.material
        for obj in model_objects
        if obj.type == "MESH"
        for slot in obj.material_slots
        if slot.material is not None
        and slot.material.get("nilbotsRole") == "team-accent"
    }
    if len(accent_materials) != 1:
        raise ValueError(
            "Expected exactly one source material with "
            "nilbotsRole='team-accent'"
        )
    accent_material = next(iter(accent_materials))
    if material_has_image_nodes(accent_material):
        raise ValueError("The source team-accent material must be texture-free")
    accent_objects = [
        obj
        for obj in model_objects
        if obj.type == "MESH"
        and any(
            slot.material == accent_material for slot in obj.material_slots
        )
    ]
    if not accent_objects:
        raise ValueError("The source team-accent material has no mesh surfaces")
    if any(obj in body_meshes for obj in accent_objects):
        raise ValueError(
            "Team accent must use separate surfaces, not a body-group material slot"
        )

    body_images = {
        image
        for roles in body_pbr.values()
        for images in roles.values()
        for image in images
    }
    if len(body_images) != 4:
        raise ValueError(
            "Bulwark runtime packaging expects one shared four-map PBR atlas; "
            f"found {len(body_images)} images"
        )
    if any(tuple(image.size) != (2048, 2048) for image in body_images):
        raise ValueError("The accented art master must retain four 2048² body maps")

    before_minimum, before_maximum = world_bounds(model_objects)
    root.matrix_world.translation.z -= before_minimum.z
    bpy.context.view_layer.update()
    after_minimum, after_maximum = world_bounds(model_objects)
    if abs(after_minimum.z) > 1e-6:
        raise ValueError("Could not floor-align the bind-pose art master")
    center = (after_minimum + after_maximum) * 0.5
    if abs(center.x) > 0.025 or abs(center.y) > 0.025:
        raise ValueError(
            "Bind-pose planform is not centered closely enough for runtime export"
        )

    gun = group_objects["GunBarrels"]
    gun_points = [gun.matrix_world @ vertex.co for vertex in gun.data.vertices]
    gun_center_x = sum(point.x for point in gun_points) / len(gun_points)
    if gun_center_x <= 0.12:
        raise ValueError(
            "GunBarrels do not verify the art master's declared +X facing"
        )

    export_collection = bpy.data.collections.new("Bulwark_Runtime_Export")
    bpy.context.scene.collection.children.link(export_collection)
    for obj in model_objects:
        export_collection.objects.link(obj)

    groups = {
        group: {
            "object": group_objects[group].name,
            "triangles": triangle_count(group_objects[group].data),
            "vertices": len(group_objects[group].data.vertices),
            "materials": sorted(
                {
                    slot.material.name
                    for slot in group_objects[group].material_slots
                    if slot.material is not None
                }
            ),
        }
        for group in BODY_GROUPS
    }
    return {
        "root": root,
        "objects": model_objects,
        "exportCollection": export_collection,
        "groups": groups,
        "bodyMaterials": sorted(body_materials, key=lambda item: item.name),
        "bodyPbr": body_pbr,
        "bodyImages": sorted(body_images, key=lambda item: item.name),
        "accentMaterial": accent_material,
        "accentObjects": sorted(accent_objects, key=lambda item: item.name),
        "bodyTriangles": sum(record["triangles"] for record in groups.values()),
        "accentTriangles": sum(
            triangle_count(obj.data) for obj in accent_objects
        ),
        "boundsBlenderBindPose": {
            "minimum": vector_json(after_minimum),
            "maximum": vector_json(after_maximum),
            "center": vector_json(center),
            "span": vector_json(after_maximum - after_minimum),
        },
        "floorAlignmentTranslation": rounded(-before_minimum.z),
        "facingEvidence": {
            "group": "GunBarrels",
            "centerXBlender": rounded(gun_center_x),
            "declaredFacing": "+X",
        },
    }


def resize_body_maps(images: list[bpy.types.Image], target_size: int) -> None:
    for image in images:
        if tuple(image.size) != (target_size, target_size):
            image.scale(target_size, target_size)
            image.update()
        if tuple(image.size) != (target_size, target_size):
            raise ValueError(
                f"Failed to resize body image {image.name!r} to {target_size}²"
            )


def set_team_material(
    material: bpy.types.Material,
    color: str,
    *,
    emission_strength: float,
) -> None:
    rgba = hex_linear_rgba(color)
    shader = principled(material)
    shader.inputs["Base Color"].default_value = rgba
    shader.inputs["Metallic"].default_value = 0.16
    shader.inputs["Roughness"].default_value = 0.34
    emission_socket = shader.inputs.get("Emission Color") or shader.inputs.get(
        "Emission"
    )
    if emission_socket is None:
        raise ValueError("Team accent Principled material has no emission color")
    emission_socket.default_value = rgba
    strength_socket = shader.inputs.get("Emission Strength")
    if strength_socket is not None:
        strength_socket.default_value = emission_strength
    material.diffuse_color = rgba


def simple_material(
    name: str,
    color: str,
    *,
    metallic: float,
    roughness: float,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = principled(material)
    rgba = hex_linear_rgba(color)
    shader.inputs["Base Color"].default_value = rgba
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    material.diffuse_color = rgba
    return material


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def configure_render_rig() -> tuple[bpy.types.Object, bpy.types.Object]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 15
    scene.render.film_transparent = False
    scene.render.use_file_extension = True
    scene.eevee.taa_render_samples = 1
    scene.eevee.taa_samples = 1
    scene.eevee.shadow_ray_count = 1
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.8

    world = bpy.data.worlds.get("Bulwark_Runtime_World")
    if world is None:
        world = bpy.data.worlds.new("Bulwark_Runtime_World")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = hex_linear_rgba("#0b1020")
    background.inputs["Strength"].default_value = 0.44
    scene.world = world

    floor_material = simple_material(
        "Bulwark_Runtime_FloorMaterial",
        "#252c36",
        metallic=0.4,
        roughness=0.74,
    )
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, -0.012))
    floor = bpy.context.object
    floor.name = "Bulwark_Runtime_Floor"
    floor.dimensions = (3.2, 3.2, 0.018)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    floor.data.materials.append(floor_material)

    lights = (
        ("Key", (-1.6, -1.8, 2.9), 320, 3.0, (0.90, 0.95, 1.0)),
        ("Fill", (1.8, 0.8, 1.1), 135, 2.2, (0.34, 0.48, 0.72)),
        ("Rim", (-0.8, 1.8, 1.8), 190, 1.5, (0.86, 0.64, 0.36)),
        ("SideFill", (0, -2, 1.25), 125, 2.0, (0.72, 0.82, 1.0)),
    )
    for name, location, energy, size, color in lights:
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.name = f"Bulwark_Runtime_{name}"
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.data.color = color
        look_at(light, Vector((0, 0, 0.13)))

    camera_data = bpy.data.cameras.new("Bulwark_Runtime_Camera")
    camera = bpy.data.objects.new("Bulwark_Runtime_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    return floor, camera


def png_chunk(chunk_type: bytes, payload: bytes) -> bytes:
    return (
        struct.pack(">I", len(payload))
        + chunk_type
        + payload
        + struct.pack(">I", zlib.crc32(chunk_type + payload) & 0xFFFFFFFF)
    )


def paeth_predictor(left: int, above: int, upper_left: int) -> int:
    estimate = left + above - upper_left
    left_distance = abs(estimate - left)
    above_distance = abs(estimate - above)
    upper_left_distance = abs(estimate - upper_left)
    if left_distance <= above_distance and left_distance <= upper_left_distance:
        return left
    if above_distance <= upper_left_distance:
        return above
    return upper_left


def decode_rgba_png(path: Path) -> tuple[int, int, list[bytearray]]:
    source = path.read_bytes()
    signature = b"\x89PNG\r\n\x1a\n"
    if not source.startswith(signature):
        raise ValueError(f"Expected a PNG file: {path.name}")
    chunks: list[tuple[bytes, bytes]] = []
    offset = len(signature)
    while offset < len(source):
        length = struct.unpack(">I", source[offset : offset + 4])[0]
        chunk_type = source[offset + 4 : offset + 8]
        payload = source[offset + 8 : offset + 8 + length]
        chunks.append((chunk_type, payload))
        offset += 12 + length
        if chunk_type == b"IEND":
            break
    ihdr = next((payload for kind, payload in chunks if kind == b"IHDR"), None)
    if ihdr is None:
        raise ValueError(f"PNG has no IHDR: {path.name}")
    width, height, depth, color_type, compression, filtering, interlace = (
        struct.unpack(">IIBBBBB", ihdr)
    )
    if (depth, color_type, compression, filtering, interlace) != (8, 6, 0, 0, 0):
        raise ValueError(f"Expected non-interlaced RGBA8 PNG: {path.name}")
    encoded = zlib.decompress(
        b"".join(payload for kind, payload in chunks if kind == b"IDAT")
    )
    row_bytes = width * 4
    rows: list[bytearray] = []
    cursor = 0
    previous = bytearray(row_bytes)
    for _ in range(height):
        filter_type = encoded[cursor]
        cursor += 1
        filtered = encoded[cursor : cursor + row_bytes]
        cursor += row_bytes
        row = bytearray(row_bytes)
        for index, value in enumerate(filtered):
            left = row[index - 4] if index >= 4 else 0
            above = previous[index]
            upper_left = previous[index - 4] if index >= 4 else 0
            if filter_type == 0:
                predictor = 0
            elif filter_type == 1:
                predictor = left
            elif filter_type == 2:
                predictor = above
            elif filter_type == 3:
                predictor = (left + above) // 2
            elif filter_type == 4:
                predictor = paeth_predictor(left, above, upper_left)
            else:
                raise ValueError(f"Unsupported PNG filter in {path.name}")
            row[index] = (value + predictor) & 0xFF
        rows.append(row)
        previous = row
    return width, height, rows


def write_rgba_png(path: Path, width: int, height: int, rows: list[bytearray]) -> None:
    if len(rows) != height or any(len(row) != width * 4 for row in rows):
        raise ValueError(f"Invalid RGBA row dimensions for {path.name}")
    signature = b"\x89PNG\r\n\x1a\n"
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    raw = b"".join(b"\x00" + bytes(row) for row in rows)
    output = bytearray(signature)
    output.extend(png_chunk(b"IHDR", ihdr))
    output.extend(png_chunk(b"IDAT", zlib.compress(raw, level=9)))
    output.extend(png_chunk(b"IEND", b""))
    path.write_bytes(output)


def canonicalize_png(path: Path) -> None:
    width, height, rows = decode_rgba_png(path)
    for row in rows:
        for index in range(0, len(row), 4):
            for channel in range(3):
                value = row[index + channel]
                row[index + channel] = value & (0xF0 if value < 80 else 0xFC)
    write_rgba_png(path, width, height, rows)


def render_view(
    *,
    path: Path,
    size: int,
    view: str,
    floor: bpy.types.Object,
    camera: bpy.types.Object,
) -> dict[str, Any]:
    scene = bpy.context.scene
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    floor.hide_render = view == "top"
    if view == "top":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.24
        camera.location = (0.0, 0.0, 3.0)
        target = Vector((0.0, 0.0, 0.0))
    elif view == "gameplay":
        camera.data.type = "PERSP"
        camera.data.angle = math.radians(42.0)
        distance = 1.88
        pitch = math.radians(58.0)
        camera.location = (
            -0.08,
            math.cos(pitch) * distance,
            math.sin(pitch) * distance,
        )
        target = Vector((0.02, 0.0, 0.13))
    else:
        raise ValueError(f"Unknown render view: {view}")
    look_at(camera, target)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    canonicalize_png(path)
    return {
        "path": path,
        "size": [size, size],
        "view": view,
        "cameraType": camera.data.type,
        "cameraLocationBlender": vector_json(camera.location),
        "cameraTargetBlender": vector_json(target),
        "orthoScale": rounded(camera.data.ortho_scale)
        if camera.data.type == "ORTHO"
        else None,
        "fieldOfViewDegrees": rounded(math.degrees(camera.data.angle))
        if camera.data.type == "PERSP"
        else None,
    }


def read_glb(path: Path) -> tuple[dict[str, Any], bytes]:
    payload = path.read_bytes()
    if len(payload) < 20:
        raise ValueError(f"GLB is too short: {path.name}")
    magic, version, declared_length = struct.unpack_from("<4sII", payload, 0)
    if magic != b"glTF" or version != 2 or declared_length != len(payload):
        raise ValueError(f"Invalid GLB v2 header: {path.name}")
    document: dict[str, Any] | None = None
    binary = b""
    offset = 12
    while offset < len(payload):
        chunk_length, chunk_type = struct.unpack_from("<II", payload, offset)
        chunk = payload[offset + 8 : offset + 8 + chunk_length]
        if len(chunk) != chunk_length:
            raise ValueError(f"Truncated GLB chunk: {path.name}")
        if chunk_type == GLB_JSON_CHUNK:
            document = json.loads(chunk.rstrip(b" \t\r\n\0").decode("utf-8"))
        elif chunk_type == GLB_BIN_CHUNK:
            binary = chunk
        offset += 8 + chunk_length
    if document is None:
        raise ValueError(f"GLB has no JSON chunk: {path.name}")
    return document, binary


def write_glb(path: Path, document: dict[str, Any], binary: bytes) -> None:
    json_payload = json.dumps(
        document,
        ensure_ascii=True,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    json_payload += b" " * ((-len(json_payload)) % 4)
    binary_payload = binary + b"\0" * ((-len(binary)) % 4)
    chunks = (
        (GLB_JSON_CHUNK, json_payload),
        (GLB_BIN_CHUNK, binary_payload),
    )
    total_length = 12 + sum(8 + len(payload) for _kind, payload in chunks)
    output = bytearray(struct.pack("<4sII", b"glTF", 2, total_length))
    for chunk_type, payload in chunks:
        output.extend(struct.pack("<II", len(payload), chunk_type))
        output.extend(payload)
    temporary = path.with_suffix(".glb.rewriting")
    temporary.write_bytes(output)
    temporary.replace(path)


def inject_team_accent_extras(path: Path) -> dict[str, Any]:
    """Restore role extras Blender cannot carry at primitive granularity."""
    document, binary = read_glb(path)
    materials = document.get("materials", [])
    accent_indices = [
        index
        for index, material in enumerate(materials)
        if material.get("extras", {}).get("nilbotsRole") == "team-accent"
    ]
    if len(accent_indices) != 1:
        raise ValueError(
            "Exported GLB must have one team-accent material before role injection"
        )
    accent_index = accent_indices[0]
    accent_mesh_indices: set[int] = set()
    primitive_count = 0
    for mesh_index, mesh in enumerate(document.get("meshes", [])):
        matched = [
            primitive
            for primitive in mesh.get("primitives", [])
            if primitive.get("material") == accent_index
        ]
        if not matched:
            continue
        if len(matched) != len(mesh.get("primitives", [])):
            raise ValueError("Accent and body primitives may not share one GLB mesh")
        mesh.setdefault("extras", {})["nilbotsRole"] = "team-accent"
        for primitive in matched:
            primitive.setdefault("extras", {})["nilbotsRole"] = "team-accent"
            primitive_count += 1
        accent_mesh_indices.add(mesh_index)
    node_count = 0
    for node in document.get("nodes", []):
        if node.get("mesh") in accent_mesh_indices:
            node.setdefault("extras", {})["nilbotsRole"] = "team-accent"
            node_count += 1
    if primitive_count == 0 or node_count == 0:
        raise ValueError("Exported team-accent material has no role surfaces")
    write_glb(path, document, binary)
    return {
        "materialCount": 1,
        "meshCount": len(accent_mesh_indices),
        "primitiveCount": primitive_count,
        "nodeCount": node_count,
        "binPayloadPreservedSha256": hashlib.sha256(binary).hexdigest(),
    }


def image_dimensions(payload: bytes, mime_type: str | None) -> tuple[int, int]:
    if payload.startswith(b"\x89PNG\r\n\x1a\n"):
        return struct.unpack(">II", payload[16:24])
    if payload.startswith(b"\xff\xd8"):
        offset = 2
        while offset + 4 <= len(payload):
            if payload[offset] != 0xFF:
                offset += 1
                continue
            marker = payload[offset + 1]
            offset += 2
            if marker in (0xD8, 0xD9) or 0xD0 <= marker <= 0xD7:
                continue
            if offset + 2 > len(payload):
                break
            length = struct.unpack(">H", payload[offset : offset + 2])[0]
            if marker in {
                0xC0,
                0xC1,
                0xC2,
                0xC3,
                0xC5,
                0xC6,
                0xC7,
                0xC9,
                0xCA,
                0xCB,
                0xCD,
                0xCE,
                0xCF,
            }:
                height, width = struct.unpack(
                    ">HH", payload[offset + 3 : offset + 7]
                )
                return width, height
            offset += length
    raise ValueError(f"Unsupported embedded image payload ({mime_type!r})")


def embedded_image(
    document: dict[str, Any],
    binary: bytes,
    image_index: int,
) -> tuple[dict[str, Any], bytes]:
    image = document["images"][image_index]
    if "bufferView" not in image:
        raise ValueError("Runtime GLB images must be embedded buffer views")
    view = document["bufferViews"][image["bufferView"]]
    start = int(view.get("byteOffset", 0))
    end = start + int(view["byteLength"])
    payload = binary[start:end]
    if len(payload) != int(view["byteLength"]):
        raise ValueError("Embedded GLB image buffer view is truncated")
    return image, payload


def texture_image_record(
    document: dict[str, Any],
    binary: bytes,
    texture_info: dict[str, Any] | None,
) -> dict[str, Any] | None:
    if texture_info is None:
        return None
    texture_index = int(texture_info["index"])
    image_index = int(document["textures"][texture_index]["source"])
    image, payload = embedded_image(document, binary, image_index)
    width, height = image_dimensions(payload, image.get("mimeType"))
    return {
        "textureIndex": texture_index,
        "imageIndex": image_index,
        "imageName": image.get("name"),
        "mimeType": image.get("mimeType"),
        "dimensions": [width, height],
        "bytes": len(payload),
        "sha256": hashlib.sha256(payload).hexdigest(),
    }


def material_record(
    document: dict[str, Any],
    binary: bytes,
    index: int,
) -> dict[str, Any]:
    material = document["materials"][index]
    pbr = material.get("pbrMetallicRoughness", {})
    roles = {
        "baseColor": texture_image_record(
            document, binary, pbr.get("baseColorTexture")
        ),
        "metallicRoughness": texture_image_record(
            document, binary, pbr.get("metallicRoughnessTexture")
        ),
        "normal": texture_image_record(
            document, binary, material.get("normalTexture")
        ),
        "emissive": texture_image_record(
            document, binary, material.get("emissiveTexture")
        ),
        "occlusion": texture_image_record(
            document, binary, material.get("occlusionTexture")
        ),
    }
    return {
        "index": index,
        "name": material.get("name"),
        "nilbotsRole": material.get("extras", {}).get("nilbotsRole"),
        "doubleSided": bool(material.get("doubleSided", False)),
        "textureRoles": roles,
        "textureFree": not any(record is not None for record in roles.values()),
    }


def primitive_triangles(
    document: dict[str, Any],
    primitive: dict[str, Any],
) -> int:
    if int(primitive.get("mode", GL_TRIANGLES)) != GL_TRIANGLES:
        raise ValueError("Runtime package only supports triangle primitives")
    if "indices" in primitive:
        count = int(document["accessors"][primitive["indices"]]["count"])
    else:
        count = int(
            document["accessors"][primitive["attributes"]["POSITION"]]["count"]
        )
    if count % 3:
        raise ValueError("Triangle primitive count is not divisible by three")
    return count // 3


def matrix_multiply(a: list[list[float]], b: list[list[float]]) -> list[list[float]]:
    return [
        [sum(a[row][k] * b[k][column] for k in range(4)) for column in range(4)]
        for row in range(4)
    ]


def identity_matrix() -> list[list[float]]:
    return [
        [1.0, 0.0, 0.0, 0.0],
        [0.0, 1.0, 0.0, 0.0],
        [0.0, 0.0, 1.0, 0.0],
        [0.0, 0.0, 0.0, 1.0],
    ]


def node_matrix(node: dict[str, Any]) -> list[list[float]]:
    if "matrix" in node:
        values = [float(value) for value in node["matrix"]]
        return [[values[column * 4 + row] for column in range(4)] for row in range(4)]
    translation = [float(value) for value in node.get("translation", [0, 0, 0])]
    scale = [float(value) for value in node.get("scale", [1, 1, 1])]
    x, y, z, w = [
        float(value) for value in node.get("rotation", [0, 0, 0, 1])
    ]
    rotation = [
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w), 0],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w), 0],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y), 0],
        [0, 0, 0, 1],
    ]
    scaling = [
        [scale[0], 0, 0, 0],
        [0, scale[1], 0, 0],
        [0, 0, scale[2], 0],
        [0, 0, 0, 1],
    ]
    transform = matrix_multiply(rotation, scaling)
    for axis in range(3):
        transform[axis][3] = translation[axis]
    return transform


def world_matrices(document: dict[str, Any]) -> dict[int, list[list[float]]]:
    nodes = document.get("nodes", [])
    parents: dict[int, int] = {}
    for parent_index, node in enumerate(nodes):
        for child in node.get("children", []):
            parents[int(child)] = parent_index
    cache: dict[int, list[list[float]]] = {}

    def resolve(index: int) -> list[list[float]]:
        if index in cache:
            return cache[index]
        local = node_matrix(nodes[index])
        parent = parents.get(index)
        cache[index] = (
            matrix_multiply(resolve(parent), local)
            if parent is not None
            else local
        )
        return cache[index]

    for index in range(len(nodes)):
        resolve(index)
    return cache


def accessor_vec3(
    document: dict[str, Any],
    binary: bytes,
    accessor_index: int,
) -> Iterable[tuple[float, float, float]]:
    accessor = document["accessors"][accessor_index]
    if accessor.get("type") != "VEC3" or int(accessor["componentType"]) != 5126:
        raise ValueError("Expected float32 VEC3 POSITION accessor")
    if "sparse" in accessor:
        raise ValueError("Sparse POSITION accessors are not supported")
    view = document["bufferViews"][accessor["bufferView"]]
    base = int(view.get("byteOffset", 0)) + int(accessor.get("byteOffset", 0))
    stride = int(view.get("byteStride", 12))
    for index in range(int(accessor["count"])):
        yield struct.unpack_from("<fff", binary, base + index * stride)


def transform_point(
    matrix: list[list[float]],
    point: tuple[float, float, float],
) -> tuple[float, float, float]:
    x, y, z = point
    return tuple(
        matrix[row][0] * x
        + matrix[row][1] * y
        + matrix[row][2] * z
        + matrix[row][3]
        for row in range(3)
    )


def inspect_glb(
    path: Path,
    *,
    source_model: dict[str, Any],
    expected_map_size: int,
) -> tuple[dict[str, Any], dict[str, bool]]:
    document, binary = read_glb(path)
    materials = [
        material_record(document, binary, index)
        for index in range(len(document.get("materials", [])))
    ]
    accent_indices = [
        record["index"]
        for record in materials
        if record["nilbotsRole"] == "team-accent"
    ]
    matrices = world_matrices(document)
    minimum = [math.inf, math.inf, math.inf]
    maximum = [-math.inf, -math.inf, -math.inf]
    group_triangles = {group: 0 for group in BODY_GROUPS}
    accent_triangles = 0
    primitive_count = 0
    body_material_indices: set[int] = set()
    accent_nodes: list[str] = []
    accent_primitive_extras = 0
    accent_mesh_extras = 0

    for node_index, node in enumerate(document.get("nodes", [])):
        mesh_index = node.get("mesh")
        if mesh_index is None:
            continue
        mesh = document["meshes"][mesh_index]
        node_group = node.get("extras", {}).get("semantic_group")
        if mesh.get("extras", {}).get("nilbotsRole") == "team-accent":
            accent_mesh_extras += 1
        node_has_accent = False
        for primitive in mesh.get("primitives", []):
            primitive_count += 1
            triangles = primitive_triangles(document, primitive)
            material_index = primitive.get("material")
            if node_group in group_triangles:
                group_triangles[node_group] += triangles
                if material_index is None:
                    raise ValueError(f"Body group {node_group!r} has no material")
                body_material_indices.add(int(material_index))
            if material_index in accent_indices:
                accent_triangles += triangles
                node_has_accent = True
                if (
                    primitive.get("extras", {}).get("nilbotsRole")
                    == "team-accent"
                ):
                    accent_primitive_extras += 1
            position_accessor = primitive.get("attributes", {}).get("POSITION")
            if position_accessor is None:
                raise ValueError("Mesh primitive has no POSITION accessor")
            for position in accessor_vec3(document, binary, position_accessor):
                world = transform_point(matrices[node_index], position)
                for axis in range(3):
                    minimum[axis] = min(minimum[axis], world[axis])
                    maximum[axis] = max(maximum[axis], world[axis])
        if node_has_accent:
            accent_nodes.append(node.get("name", f"node-{node_index}"))

    body_material_records = [
        materials[index] for index in sorted(body_material_indices)
    ]
    required_roles = ("baseColor", "metallicRoughness", "normal", "emissive")
    body_pbr_complete = all(
        all(material["textureRoles"][role] is not None for role in required_roles)
        for material in body_material_records
    )
    body_maps_correct_size = all(
        material["textureRoles"][role]["dimensions"]
        == [expected_map_size, expected_map_size]
        for material in body_material_records
        for role in required_roles
    )
    body_geometry_preserved = all(
        group_triangles[group]
        == source_model["groups"][group]["triangles"]
        for group in BODY_GROUPS
    )
    span = [maximum[axis] - minimum[axis] for axis in range(3)]
    center = [(minimum[axis] + maximum[axis]) * 0.5 for axis in range(3)]
    gun_node_indices = [
        index
        for index, node in enumerate(document.get("nodes", []))
        if node.get("extras", {}).get("semantic_group") == "GunBarrels"
        and "mesh" in node
    ]
    gun_facing = False
    if len(gun_node_indices) == 1:
        gun_node = document["nodes"][gun_node_indices[0]]
        gun_points = [
            transform_point(matrices[gun_node_indices[0]], position)
            for primitive in document["meshes"][gun_node["mesh"]]["primitives"]
            for position in accessor_vec3(
                document, binary, primitive["attributes"]["POSITION"]
            )
        ]
        gun_facing = (
            sum(point[0] for point in gun_points) / len(gun_points) > 0.12
        )

    has_lights = (
        "KHR_lights_punctual" in document.get("extensionsUsed", [])
        or "KHR_lights_punctual" in document.get("extensions", {})
        or any(
            "KHR_lights_punctual" in node.get("extensions", {})
            for node in document.get("nodes", [])
        )
    )
    checks = {
        "validGlbV2": True,
        "allNineBodyGroupsPresent": set(group_triangles) == set(BODY_GROUPS)
        and all(group_triangles.values()),
        "bodyGeometryPreservedPerGroup": body_geometry_preserved,
        "accentGeometryPreserved": accent_triangles
        == source_model["accentTriangles"],
        "exactlyOneTeamAccentMaterial": len(accent_indices) == 1,
        "teamAccentMaterialTextureFree": len(accent_indices) == 1
        and materials[accent_indices[0]]["textureFree"],
        "teamAccentSurfacesPresent": bool(accent_nodes),
        "teamAccentSurfaceExtrasPreserved": bool(accent_nodes)
        and accent_mesh_extras == len(accent_nodes)
        and accent_primitive_extras == len(accent_nodes),
        "allBodyPbrRolesPresent": body_pbr_complete,
        "allBodyMapsMatchTierDimensions": body_maps_correct_size,
        "noAnimations": not document.get("animations"),
        "noCameras": not document.get("cameras"),
        "noLights": not has_lights,
        "floorAtGlbYZero": abs(minimum[1]) <= 1e-5,
        "planformCentered": abs(center[0]) <= 0.025 and abs(center[2]) <= 0.025,
        "declaredPlusXFacingVerifiedByGunBarrels": gun_facing,
        "glbUsesPlusYUpExportContract": abs(minimum[1]) <= 1e-5
        and span[1] < span[0]
        and span[1] < span[2],
    }
    report = {
        "path": path,
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
        "generator": document.get("asset", {}).get("generator"),
        "nodes": len(document.get("nodes", [])),
        "meshes": len(document.get("meshes", [])),
        "primitives": primitive_count,
        "materials": materials,
        "textures": len(document.get("textures", [])),
        "images": len(document.get("images", [])),
        "animations": len(document.get("animations", [])),
        "cameras": len(document.get("cameras", [])),
        "triangles": sum(
            primitive_triangles(document, primitive)
            for mesh in document.get("meshes", [])
            for primitive in mesh.get("primitives", [])
        ),
        "bodyTriangles": sum(group_triangles.values()),
        "accentTriangles": accent_triangles,
        "groupTriangles": group_triangles,
        "accentNodes": sorted(accent_nodes),
        "accentMeshExtras": accent_mesh_extras,
        "accentPrimitiveExtras": accent_primitive_extras,
        "boundsGlbPlusYUp": {
            "minimum": [rounded(value) for value in minimum],
            "maximum": [rounded(value) for value in maximum],
            "center": [rounded(value) for value in center],
            "span": [rounded(value) for value in span],
        },
    }
    return report, checks


def artifact_record(path: Path, *, repo_root: Path, kind: str) -> dict[str, Any]:
    return {
        "path": path.relative_to(repo_root).as_posix(),
        "kind": kind,
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
    }


def compose_contact_sheet(
    *,
    paths: list[list[Path]],
    output: Path,
) -> dict[str, Any]:
    decoded = [[decode_rgba_png(path) for path in row] for row in paths]
    widths = {item[0] for row in decoded for item in row}
    heights = {item[1] for row in decoded for item in row}
    if len(widths) != 1 or len(heights) != 1:
        raise ValueError("Contact-sheet source dimensions do not match")
    cell_width = next(iter(widths))
    cell_height = next(iter(heights))
    rows: list[bytearray] = []
    for decoded_row in decoded:
        for y in range(cell_height):
            rows.append(
                bytearray().join(item[2][y] for item in decoded_row)
            )
    width = cell_width * len(decoded[0])
    height = cell_height * len(decoded)
    write_rgba_png(output, width, height, rows)
    return {
        "path": output,
        "dimensions": [width, height],
        "cellDimensions": [cell_width, cell_height],
        "columnOrder": [tier["id"] for tier in TIERS],
        "rowOrder": [team for team, _color, _strength in TEAMS],
    }


def export_tier(
    *,
    input_path: Path,
    input_sha256: str,
    tier: dict[str, Any],
    output_dir: Path,
    repo_root: Path,
) -> tuple[dict[str, Any], list[dict[str, Any]], dict[tuple[str, str, int], Path]]:
    if sha256(input_path) != input_sha256:
        raise ValueError("Art-master hash drifted between tier exports")
    bpy.ops.wm.open_mainfile(filepath=str(input_path))
    source_model = inspect_and_prepare_model()
    resize_body_maps(source_model["bodyImages"], int(tier["mapSize"]))

    tier_dir = output_dir / tier["id"]
    render_dir = tier_dir / "renders"
    comparison_dir = tier_dir / "gameplay-scale"
    tier_dir.mkdir()
    render_dir.mkdir()
    comparison_dir.mkdir()
    glb_path = tier_dir / "model.glb"

    set_team_material(
        source_model["accentMaterial"],
        "#d9e3ea",
        emission_strength=0.12,
    )
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=False,
        collection=source_model["exportCollection"].name,
        export_materials="EXPORT",
        export_image_format=tier["imageFormat"],
        export_jpeg_quality=int(tier["jpegQuality"]),
        export_cameras=False,
        export_lights=False,
        export_extras=True,
        export_animations=False,
        export_normals=True,
        export_tangents=True,
        export_apply=True,
        export_yup=True,
    )
    accent_extras = inject_team_accent_extras(glb_path)
    glb, checks = inspect_glb(
        glb_path,
        source_model=source_model,
        expected_map_size=int(tier["mapSize"]),
    )
    glb["path"] = glb_path.relative_to(repo_root).as_posix()
    glb["teamAccentExtrasInjection"] = accent_extras

    floor, camera = configure_render_rig()
    render_records: dict[str, Any] = {}
    small_paths: dict[tuple[str, str, int], Path] = {}
    artifacts = [artifact_record(glb_path, repo_root=repo_root, kind="glb")]
    for team, color, emission_strength in TEAMS:
        set_team_material(
            source_model["accentMaterial"],
            color,
            emission_strength=emission_strength,
        )
        for view in VIEWS:
            full_path = render_dir / f"{team}-{view}.png"
            record = render_view(
                path=full_path,
                size=FULL_RENDER_SIZE,
                view=view,
                floor=floor,
                camera=camera,
            )
            record["path"] = full_path.relative_to(repo_root).as_posix()
            render_records[f"{team}-{view}-{FULL_RENDER_SIZE}"] = record
            artifacts.append(
                artifact_record(full_path, repo_root=repo_root, kind="fixed-render")
            )
            for size in COMPARISON_SIZES:
                small_path = comparison_dir / f"{team}-{view}-{size}.png"
                small_record = render_view(
                    path=small_path,
                    size=size,
                    view=view,
                    floor=floor,
                    camera=camera,
                )
                small_record["path"] = small_path.relative_to(repo_root).as_posix()
                render_records[f"{team}-{view}-{size}"] = small_record
                small_paths[(team, view, size)] = small_path
                artifacts.append(
                    artifact_record(
                        small_path,
                        repo_root=repo_root,
                        kind="gameplay-scale-render",
                    )
                )

    return (
        {
            "purpose": tier["purpose"],
            "mapSize": tier["mapSize"],
            "imageEncoding": {
                "format": tier["imageFormat"],
                "jpegQuality": tier["jpegQuality"],
            },
            "sourceGeometry": {
                "groups": source_model["groups"],
                "bodyTriangles": source_model["bodyTriangles"],
                "accentTriangles": source_model["accentTriangles"],
                "accentObjects": [
                    obj.name for obj in source_model["accentObjects"]
                ],
                "boundsBlenderBindPose": source_model[
                    "boundsBlenderBindPose"
                ],
                "floorAlignmentTranslation": source_model[
                    "floorAlignmentTranslation"
                ],
                "facingEvidence": source_model["facingEvidence"],
            },
            "glb": glb,
            "renders": render_records,
            "checks": checks,
            "passed": all(checks.values()),
        },
        artifacts,
        small_paths,
    )


def markdown_report(report: dict[str, Any]) -> str:
    lines = [
        "# Bulwark Mobile runtime package",
        "",
        f"- Source: `{report['source']['path']}`",
        f"- Source SHA-256: `{report['source']['sha256']}`",
        f"- Blender: {report['blender']['version']}",
        f"- Overall gate: **{'PASS' if report['passed'] else 'FAIL'}**",
        "",
        "| Tier | GLB bytes | Triangles | Maps | Gate |",
        "| --- | ---: | ---: | --- | --- |",
    ]
    for tier_id in ("source-2k", "runtime-1k", "comparison-512"):
        tier = report["tiers"][tier_id]
        lines.append(
            f"| {tier_id} | {tier['glb']['bytes']:,} | "
            f"{tier['glb']['triangles']:,} | {tier['mapSize']}² | "
            f"{'PASS' if tier['passed'] else 'FAIL'} |"
        )
    lines.extend(
        [
            "",
            "All three derivatives contain the same nine semantic body groups "
            "and the same separate team-accent surfaces. The exporter evaluates "
            "the frame-1 bind pose, floor-aligns it, omits cameras/lights, and "
            "strips all authored clips because the current renderer does not "
            "sample asset animations.",
            "",
            "Comparison sheets use columns `source-2k`, `runtime-1k`, "
            "`comparison-512` and rows `neutral`, `blue`, `orange`. Team color "
            "is applied only to the texture-free semantic accent material.",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> int:
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise ValueError(
            f"Expected Blender {EXPECTED_BLENDER[0]}.{EXPECTED_BLENDER[1]}.x, "
            f"got {bpy.app.version_string}"
        )
    args = parse_args(blender_arguments())
    repo_root = repository_root()
    input_path, input_relative = resolve_repo_relative(
        args.input_blend,
        repo_root=repo_root,
        option="--input-blend",
        strict=True,
    )
    output_dir, output_relative = resolve_repo_relative(
        args.output_dir,
        repo_root=repo_root,
        option="--output-dir",
        strict=False,
    )
    if input_path.suffix.lower() != ".blend" or not input_path.is_file():
        raise ValueError("--input-blend must name an existing .blend file")
    if (
        not output_relative.parts
        or output_relative.parts[0] not in {"sandbox", "var"}
    ):
        raise ValueError("--output-dir must be below sandbox/ or var/")
    if output_dir.exists():
        raise ValueError("--output-dir already exists; refusing overwrite")
    observed_sha256 = sha256(input_path)
    if observed_sha256 != args.expected_source_sha256:
        raise ValueError(
            "Source hash mismatch inside Blender: "
            f"expected {args.expected_source_sha256}, observed {observed_sha256}"
        )
    output_dir.mkdir(parents=True)

    tiers: dict[str, Any] = {}
    artifacts: list[dict[str, Any]] = []
    small_renders: dict[str, dict[tuple[str, str, int], Path]] = {}
    for tier in TIERS:
        tier_report, tier_artifacts, paths = export_tier(
            input_path=input_path,
            input_sha256=args.expected_source_sha256,
            tier=tier,
            output_dir=output_dir,
            repo_root=repo_root,
        )
        tiers[tier["id"]] = tier_report
        artifacts.extend(tier_artifacts)
        small_renders[tier["id"]] = paths

    comparison_dir = output_dir / "comparisons"
    comparison_dir.mkdir()
    comparison_records: dict[str, Any] = {}
    for view in VIEWS:
        for size in COMPARISON_SIZES:
            output = comparison_dir / f"{view}-{size}.png"
            sheet = compose_contact_sheet(
                paths=[
                    [
                        small_renders[tier["id"]][(team, view, size)]
                        for tier in TIERS
                    ]
                    for team, _color, _strength in TEAMS
                ],
                output=output,
            )
            sheet["path"] = output.relative_to(repo_root).as_posix()
            comparison_records[f"{view}-{size}"] = sheet
            artifacts.append(
                artifact_record(output, repo_root=repo_root, kind="comparison-sheet")
            )

    cross_tier_checks = {
        "allTierGatesPass": all(tier["passed"] for tier in tiers.values()),
        "identicalBodyTrianglesAcrossTiers": len(
            {tier["glb"]["bodyTriangles"] for tier in tiers.values()}
        )
        == 1,
        "identicalAccentTrianglesAcrossTiers": len(
            {tier["glb"]["accentTriangles"] for tier in tiers.values()}
        )
        == 1,
        "identicalGroupTrianglesAcrossTiers": len(
            {
                json.dumps(tier["glb"]["groupTriangles"], sort_keys=True)
                for tier in tiers.values()
            }
        )
        == 1,
        "identicalNodesMeshesPrimitivesMaterialsAcrossTiers": len(
            {
                (
                    tier["glb"]["nodes"],
                    tier["glb"]["meshes"],
                    tier["glb"]["primitives"],
                    len(tier["glb"]["materials"]),
                )
                for tier in tiers.values()
            }
        )
        == 1,
        "allRequestedComparisonSheetsWritten": len(comparison_records)
        == len(VIEWS) * len(COMPARISON_SIZES),
    }
    report: dict[str, Any] = {
        "schema": SCHEMA,
        "toolVersion": TOOL_VERSION,
        "passed": all(cross_tier_checks.values()),
        "source": {
            "path": input_relative.as_posix(),
            "bytes": input_path.stat().st_size,
            "sha256": args.expected_source_sha256,
            "visualAuthority": "editable accented Bulwark Mobile art master",
        },
        "blender": {
            "version": bpy.app.version_string,
            "expectedMajorMinor": list(EXPECTED_BLENDER),
            "background": bpy.app.background,
        },
        "method": {
            "offline": True,
            "providerCalls": 0,
            "networkCalls": 0,
            "bindPoseFrame": 1,
            "glbFacing": "+X",
            "glbUp": "+Y",
            "glbFloor": "Y=0",
            "animations": "stripped from all runtime comparison derivatives",
            "cameras": "render-only; never exported",
            "lights": "render-only; never exported",
            "topCamera": "strict orthographic",
            "gameplayCamera": "42-degree perspective at 58 degrees above ground",
            "teamColorOwnership": (
                "neutral texture-free material with "
                "extras.nilbotsRole='team-accent'; renderer owns tint/glow"
            ),
            "mapTiering": [
                {
                    "id": tier["id"],
                    "dimensions": [tier["mapSize"], tier["mapSize"]],
                    "imageFormat": tier["imageFormat"],
                    "jpegQuality": tier["jpegQuality"],
                }
                for tier in TIERS
            ],
        },
        "tiers": tiers,
        "comparisons": comparison_records,
        "checks": cross_tier_checks,
        "artifacts": artifacts,
    }
    markdown_path = output_dir / "report.md"
    markdown_path.write_text(markdown_report(report), encoding="utf-8")
    artifacts.append(
        artifact_record(markdown_path, repo_root=repo_root, kind="report")
    )
    report_path = output_dir / "report.json"
    report_path.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(
        json.dumps(
            {
                "passed": report["passed"],
                "report": report_path.relative_to(repo_root).as_posix(),
                "sourceSha256": args.expected_source_sha256,
                "tiers": {
                    tier_id: {
                        "bytes": tier["glb"]["bytes"],
                        "sha256": tier["glb"]["sha256"],
                        "triangles": tier["glb"]["triangles"],
                    }
                    for tier_id, tier in tiers.items()
                },
            },
            sort_keys=True,
        )
    )
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    try:
        result = main()
    except SystemExit as error:
        result = int(error.code or 0)
    except Exception as error:
        print(f"Bulwark runtime package failed: {error}", file=sys.stderr)
        traceback.print_exc()
        sys.stdout.flush()
        sys.stderr.flush()
        os._exit(2)
    if result:
        sys.stdout.flush()
        sys.stderr.flush()
        os._exit(result)
