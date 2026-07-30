#!/usr/bin/env python3
"""Render archive-only Bulwark avatar art in pinned Blender 5.2.

Invoke through ``derive_bulwark_avatar.py``. The source textured authority is
opened read-only, sampled at its authored Mobile rest pose, and rendered from
one fixed near-top orthographic camera. The semantic mask is a separate
occlusion-correct pass: body surfaces write black, team-accent surfaces write
white, and only visible white coverage becomes mask alpha.
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
from pathlib import Path
from typing import Any, Iterable

import bpy
from mathutils import Matrix, Vector


SCHEMA = "nilbots-bulwark-avatar-derivation-report-1"
TOOL_VERSION = "1.0.0"
EXPECTED_BLENDER = (5, 2)
EXPECTED_SOURCE_BLEND_SHA256 = (
    "b6827956ff032dbf01564e26706f19400305b1245bedee4c65960bddc448df23"
)
EXPECTED_RUNTIME_GLB_NAME = "aegis-tortoise-textured-animated.glb"
EXPECTED_RUNTIME_GLB_SHA256 = (
    "a169822ef3c3b98ab854257e3c637e00f50900e53cfb3f51dfd8910d7ec4df8b"
)
ROOT_NAME = "AUTH_AegisTortoise_Kinematic_Master"
TEAM_ROLE = "team-accent"
EXPECTED_TEAM_OBJECTS = (
    "TeamAccent.Shell.NorthInner.Emitter",
    "TeamAccent.Shell.NorthOuter.Emitter",
    "TeamAccent.Shell.SouthInner.Emitter",
    "TeamAccent.Shell.SouthOuter.Emitter",
)
EXPECTED_MESH_COUNT = 31
EXPECTED_TEAM_MESH_COUNT = 4
EXPECTED_ACTION_CLIP_NAMES = {
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
}
SOURCE_FRAME = 1
WORK_SIZE = 1024
OUTPUT_SIZE = 512
CAMERA_ORTHO_SCALE = 1.53
CAMERA_HEIGHT = 3.0
CAMERA_DEPTH_TIE_BIAS = (0.0015, -0.0010)
MINIMUM_OCCUPANCY = 0.65
MAXIMUM_OCCUPANCY = 0.75
AVATAR_NAME = "bulwark-mobile-avatar-512.png"
MASK_NAME = "bulwark-mobile-team-accent-mask-512.png"
EXPECTED_AVATAR_SHA256 = (
    "ef4d734bdded4143311aedee470c886095b48aa9165892ba0dda541710d822f9"
)
EXPECTED_MASK_SHA256 = (
    "f0744106106fe30319020f23ff9c56d0158710f3a177b046fd6958a442b7ee21"
)
REPORT_NAME = "report.json"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")


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
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-blend", required=True, type=Path)
    parser.add_argument(
        "--expected-source-sha256",
        required=True,
        type=sha256_argument,
    )
    parser.add_argument("--output-dir", required=True, type=Path)
    return parser.parse_args(argv)


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def require_relative(path: Path, option: str) -> None:
    if path.is_absolute() or not path.parts:
        raise ValueError(f"{option} must be a non-empty repository-relative path")


def resolve_repo_path(
    path: Path,
    *,
    repo_root: Path,
    option: str,
    strict: bool,
) -> tuple[Path, Path]:
    require_relative(path, option)
    resolved = (repo_root / path).resolve(strict=strict)
    try:
        relative = resolved.relative_to(repo_root)
    except ValueError as error:
        raise ValueError(f"{option} must stay inside the repository") from error
    return resolved, relative


def resolve_input(path: Path, repo_root: Path) -> tuple[Path, Path]:
    resolved, relative = resolve_repo_path(
        path,
        repo_root=repo_root,
        option="--input-blend",
        strict=True,
    )
    if not resolved.is_file() or resolved.suffix.lower() != ".blend":
        raise ValueError(f"Expected an existing .blend file: {relative}")
    return resolved, relative


def archive_output_allowed(relative: Path) -> bool:
    prefix = Path("art/class-models/masters/aegis-tortoise-kinematic/proofs")
    try:
        remainder = relative.relative_to(prefix)
    except ValueError:
        return False
    return bool(remainder.parts)


def resolve_output(path: Path, repo_root: Path) -> tuple[Path, Path]:
    resolved, relative = resolve_repo_path(
        path,
        repo_root=repo_root,
        option="--output-dir",
        strict=False,
    )
    disposable = bool(relative.parts) and relative.parts[0] in {"sandbox", "var"}
    if not disposable and not archive_output_allowed(relative):
        raise ValueError("Output path is outside the approved archive/disposable roots")
    if resolved.exists():
        raise ValueError(f"Refusing to overwrite output directory: {relative}")
    return resolved, relative


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def rounded(value: float, digits: int = 8) -> float:
    result = round(float(value), digits)
    return 0.0 if result == -0.0 else result


def vector_json(value: Iterable[float]) -> list[float]:
    return [rounded(component) for component in value]


def matrix_json(value: Matrix) -> list[list[float]]:
    return [vector_json(row) for row in value]


def role_value(value: Any) -> str | None:
    role = value.get("nilbotsRole") if value is not None else None
    return role if isinstance(role, str) else None


def descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    result: list[bpy.types.Object] = []
    pending = [root]
    while pending:
        current = pending.pop()
        result.append(current)
        pending.extend(sorted(current.children, key=lambda child: child.name))
    return sorted(result, key=lambda obj: obj.name)


def action_clip_names() -> set[str]:
    names: set[str] = set()
    for obj in bpy.data.objects:
        animation = obj.animation_data
        if animation is None:
            continue
        for track in animation.nla_tracks:
            names.add(track.name)
    return names


def animation_rest_record(objects: list[bpy.types.Object]) -> dict[str, Any]:
    active_actions = sorted(
        obj.name
        for obj in objects
        if obj.animation_data is not None
        and obj.animation_data.action is not None
    )
    tracks = [
        (obj.name, track.name, bool(track.mute))
        for obj in objects
        if obj.animation_data is not None
        for track in obj.animation_data.nla_tracks
    ]
    return {
        "frame": bpy.context.scene.frame_current,
        "activeActionObjects": active_actions,
        "nlaTrackCount": len(tracks),
        "allNlaTracksMuted": all(muted for _obj, _track, muted in tracks),
        "clipNames": sorted({track for _obj, track, _muted in tracks}),
    }


def world_bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [
        obj.matrix_world @ Vector(corner)
        for obj in meshes
        for corner in obj.bound_box
    ]
    if not points:
        raise ValueError("Cannot derive bounds from an empty mesh list")
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return minimum, maximum


def hide_non_model_objects(model_objects: list[bpy.types.Object]) -> None:
    model_set = set(model_objects)
    for obj in bpy.data.objects:
        obj.hide_render = obj not in model_set


def hex_linear_rgba(value: str) -> tuple[float, float, float, float]:
    channels = [int(value[index : index + 2], 16) / 255.0 for index in (1, 3, 5)]
    linear = [
        channel / 12.92
        if channel <= 0.04045
        else ((channel + 0.055) / 1.055) ** 2.4
        for channel in channels
    ]
    return (*linear, 1.0)


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def orient_top_camera_without_roll(
    camera: bpy.types.Object,
    *,
    target: Vector,
) -> None:
    """Aim near top while pinning model +X/+Y to screen right/up.

    Blender's generic ``to_track_quat`` has an under-constrained roll near an
    exact top view. A tiny depth-tie bias can therefore rotate the image by an
    arbitrary large angle. Constructing the camera basis explicitly retains
    the intended East-facing 2D contract.
    """
    forward = (target - camera.location).normalized()
    camera_z = -forward
    world_x = Vector((1.0, 0.0, 0.0))
    camera_x = (world_x - camera_z * world_x.dot(camera_z)).normalized()
    camera_y = camera_z.cross(camera_x).normalized()
    rotation = Matrix(
        (
            (camera_x.x, camera_y.x, camera_z.x),
            (camera_x.y, camera_y.y, camera_z.y),
            (camera_x.z, camera_y.z, camera_z.z),
        )
    )
    camera.rotation_mode = "QUATERNION"
    camera.rotation_quaternion = rotation.to_quaternion()


def configure_render_rig(
    *,
    target: Vector,
) -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.render.resolution_x = WORK_SIZE
    scene.render.resolution_y = WORK_SIZE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 15
    scene.render.film_transparent = True
    scene.render.use_file_extension = True
    scene.render.dither_intensity = 0.0
    scene.cycles.device = "CPU"
    scene.cycles.samples = 16
    scene.cycles.use_denoising = False
    scene.cycles.use_adaptive_sampling = False
    scene.cycles.seed = 0
    scene.cycles.use_animated_seed = False
    scene.render.threads_mode = "FIXED"
    scene.render.threads = 1
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.35
    scene.view_settings.gamma = 1.0

    world = bpy.data.worlds.new("AVATAR_DERIVE.World")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = hex_linear_rgba("#151b25")
    background.inputs["Strength"].default_value = 0.48
    scene.world = world

    lights: list[bpy.types.Object] = []
    light_specs = (
        ("Key", (-1.7, -1.8, 3.0), 390.0, 3.1, (0.93, 0.96, 1.0)),
        ("Fill", (1.8, 0.8, 1.45), 175.0, 2.4, (0.38, 0.51, 0.72)),
        ("WarmRim", (-0.9, 1.8, 1.9), 205.0, 1.6, (0.86, 0.67, 0.40)),
        ("NoseFill", (2.2, -1.0, 1.35), 145.0, 2.0, (0.72, 0.82, 1.0)),
    )
    light_target = target + Vector((0.0, 0.0, 0.18))
    for name, location, energy, size, color in light_specs:
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.name = f"AVATAR_DERIVE.{name}"
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.data.color = color
        look_at(light, light_target)
        lights.append(light)

    camera_data = bpy.data.cameras.new("AVATAR_DERIVE.Camera")
    camera = bpy.data.objects.new("AVATAR_DERIVE.Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = CAMERA_ORTHO_SCALE
    camera.location = (
        target.x + CAMERA_DEPTH_TIE_BIAS[0],
        target.y + CAMERA_DEPTH_TIE_BIAS[1],
        CAMERA_HEIGHT,
    )
    orient_top_camera_without_roll(camera, target=target)
    bpy.context.view_layer.update()
    return camera, lights


def png_chunk(chunk_type: bytes, payload: bytes) -> bytes:
    return (
        struct.pack(">I", len(payload))
        + chunk_type
        + payload
        + struct.pack(">I", zlib.crc32(chunk_type + payload) & 0xFFFFFFFF)
    )


def write_rgba_png(
    path: Path,
    *,
    width: int,
    height: int,
    pixels: bytes | bytearray,
) -> None:
    expected = width * height * 4
    if len(pixels) != expected:
        raise ValueError(
            f"RGBA buffer has {len(pixels)} bytes; expected {expected}"
        )
    scanlines = bytearray()
    stride = width * 4
    for row in range(height):
        scanlines.append(0)
        start = row * stride
        scanlines.extend(pixels[start : start + stride])
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    payload = (
        b"\x89PNG\r\n\x1a\n"
        + png_chunk(b"IHDR", ihdr)
        + png_chunk(b"IDAT", zlib.compress(bytes(scanlines), 9))
        + png_chunk(b"IEND", b"")
    )
    path.write_bytes(payload)


def quantize_rgb(value: int) -> int:
    return value & (0xF0 if value < 80 else 0xFC)


def downsample_avatar(
    rows: list[bytearray],
    *,
    width: int,
    height: int,
) -> bytearray:
    if (width, height) != (WORK_SIZE, WORK_SIZE):
        raise ValueError("Avatar working render has an unexpected resolution")
    if WORK_SIZE != OUTPUT_SIZE * 2:
        raise ValueError("Avatar derivation expects an exact 2x supersample")
    output = bytearray(OUTPUT_SIZE * OUTPUT_SIZE * 4)
    for out_y in range(OUTPUT_SIZE):
        for out_x in range(OUTPUT_SIZE):
            samples = [
                rows[out_y * 2 + dy][(out_x * 2 + dx) * 4 : (out_x * 2 + dx) * 4 + 4]
                for dy in range(2)
                for dx in range(2)
            ]
            alpha_sum = sum(sample[3] for sample in samples)
            output_index = (out_y * OUTPUT_SIZE + out_x) * 4
            alpha = (alpha_sum + 2) // 4
            if alpha_sum > 0:
                for channel in range(3):
                    premultiplied = sum(
                        sample[channel] * sample[3] for sample in samples
                    )
                    straight = (premultiplied + alpha_sum // 2) // alpha_sum
                    output[output_index + channel] = quantize_rgb(straight)
            output[output_index + 3] = alpha
    return output


def downsample_semantic_mask(
    rows: list[bytearray],
    *,
    width: int,
    height: int,
) -> bytearray:
    if (width, height) != (WORK_SIZE, WORK_SIZE):
        raise ValueError("Semantic working render has an unexpected resolution")
    output = bytearray(OUTPUT_SIZE * OUTPUT_SIZE * 4)
    denominator = 255 * 4
    for out_y in range(OUTPUT_SIZE):
        for out_x in range(OUTPUT_SIZE):
            coverage_sum = 0
            for dy in range(2):
                row = rows[out_y * 2 + dy]
                for dx in range(2):
                    index = (out_x * 2 + dx) * 4
                    white = max(row[index], row[index + 1], row[index + 2])
                    # The mask pass disables output dithering, but retain a
                    # small explicit floor so a driver-level one-bit black
                    # variation can never turn the whole opaque body into a
                    # one-alpha semantic mask. Values above the floor are
                    # rescaled so antialiased role boundaries stay lossless.
                    semantic = (
                        0
                        if white <= 8
                        else ((white - 8) * 255 + 123) // 247
                    )
                    coverage_sum += semantic * row[index + 3]
            alpha = (coverage_sum + denominator // 2) // denominator
            output_index = (out_y * OUTPUT_SIZE + out_x) * 4
            if alpha > 0:
                output[output_index : output_index + 3] = b"\xff\xff\xff"
            output[output_index + 3] = alpha
    return output


def make_emission_material(
    name: str,
    color: tuple[float, float, float, float],
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = color
    emission.inputs["Strength"].default_value = 1.0
    material.node_tree.links.new(emission.outputs["Emission"], output.inputs["Surface"])
    material.diffuse_color = color
    return material


def replace_materials(
    meshes: list[bpy.types.Object],
    *,
    team_objects: set[bpy.types.Object],
    body_material: bpy.types.Material,
    team_material: bpy.types.Material,
) -> dict[bpy.types.Mesh, list[bpy.types.Material | None]]:
    if len({obj.data for obj in meshes}) != len(meshes):
        raise ValueError("Semantic mask requires one distinct mesh datablock per object")
    originals: dict[bpy.types.Mesh, list[bpy.types.Material | None]] = {}
    for obj in meshes:
        mesh = obj.data
        originals[mesh] = list(mesh.materials)
        mesh.materials.clear()
        mesh.materials.append(team_material if obj in team_objects else body_material)
        for polygon in mesh.polygons:
            polygon.material_index = 0
    return originals


def restore_materials(
    originals: dict[bpy.types.Mesh, list[bpy.types.Material | None]],
) -> None:
    for mesh, materials in originals.items():
        mesh.materials.clear()
        for material in materials:
            mesh.materials.append(material)


def render_avatar(
    *,
    output_dir: Path,
    camera: bpy.types.Object,
    audit: Any,
) -> Path:
    scene = bpy.context.scene
    work_path = output_dir / ".avatar-working-1024.png"
    final_path = output_dir / AVATAR_NAME
    scene.camera = camera
    scene.render.film_transparent = True
    scene.render.filepath = str(work_path)
    bpy.ops.render.render(write_still=True)
    audit.canonicalize_png(work_path)
    width, height, rows = audit.decode_rgba_png(work_path)
    pixels = downsample_avatar(rows, width=width, height=height)
    write_rgba_png(
        final_path,
        width=OUTPUT_SIZE,
        height=OUTPUT_SIZE,
        pixels=pixels,
    )
    work_path.unlink()
    return final_path


def render_semantic_mask(
    *,
    output_dir: Path,
    meshes: list[bpy.types.Object],
    team_meshes: list[bpy.types.Object],
    camera: bpy.types.Object,
    lights: list[bpy.types.Object],
    audit: Any,
) -> Path:
    scene = bpy.context.scene
    work_path = output_dir / ".team-mask-working-1024.png"
    final_path = output_dir / MASK_NAME
    body_material = make_emission_material(
        "AVATAR_DERIVE.MaskBody",
        (0.0, 0.0, 0.0, 1.0),
    )
    team_material = make_emission_material(
        "AVATAR_DERIVE.MaskTeamAccent",
        (1.0, 1.0, 1.0, 1.0),
    )
    originals = replace_materials(
        meshes,
        team_objects=set(team_meshes),
        body_material=body_material,
        team_material=team_material,
    )
    old_transform = scene.view_settings.view_transform
    old_look = scene.view_settings.look
    old_exposure = scene.view_settings.exposure
    old_gamma = scene.view_settings.gamma
    old_light_hides = {light: light.hide_render for light in lights}
    try:
        scene.view_settings.view_transform = "Standard"
        scene.view_settings.look = "None"
        scene.view_settings.exposure = 0.0
        scene.view_settings.gamma = 1.0
        for light in lights:
            light.hide_render = True
        scene.render.film_transparent = True
        scene.camera = camera
        scene.render.filepath = str(work_path)
        bpy.context.view_layer.update()
        bpy.ops.render.render(write_still=True)
        width, height, rows = audit.decode_rgba_png(work_path)
        pixels = downsample_semantic_mask(rows, width=width, height=height)
        write_rgba_png(
            final_path,
            width=OUTPUT_SIZE,
            height=OUTPUT_SIZE,
            pixels=pixels,
        )
    finally:
        restore_materials(originals)
        scene.view_settings.view_transform = old_transform
        scene.view_settings.look = old_look
        scene.view_settings.exposure = old_exposure
        scene.view_settings.gamma = old_gamma
        for light, hidden in old_light_hides.items():
            light.hide_render = hidden
        bpy.context.view_layer.update()
        if body_material.users == 0:
            bpy.data.materials.remove(body_material)
        if team_material.users == 0:
            bpy.data.materials.remove(team_material)
    work_path.unlink()
    return final_path


def alpha_record(path: Path, audit: Any) -> dict[str, Any]:
    width, height, rows = audit.decode_rgba_png(path)
    occupied = {
        (x, y)
        for y, row in enumerate(rows)
        for x in range(width)
        if row[x * 4 + 3] > 1
    }
    if not occupied:
        raise ValueError(f"Derived image has no occupied alpha: {path.name}")
    xs = [point[0] for point in occupied]
    ys = [point[1] for point in occupied]
    bounds = [min(xs), min(ys), max(xs), max(ys)]
    spans = [bounds[2] - bounds[0] + 1, bounds[3] - bounds[1] + 1]
    unique_alpha = sorted(
        {
            row[index]
            for row in rows
            for index in range(3, len(row), 4)
        }
    )
    transparent_rgb_zero = all(
        row[index] == row[index + 1] == row[index + 2] == 0
        for row in rows
        for index in range(0, len(row), 4)
        if row[index + 3] == 0
    )
    return {
        "resolution": [width, height],
        "occupiedPixelsAboveOne": len(occupied),
        "occupiedBoundsInclusive": bounds,
        "occupiedSpanPixels": spans,
        "maximumSpanFraction": rounded(max(spans) / width),
        "alphaLevelCount": len(unique_alpha),
        "minimumAlpha": min(unique_alpha),
        "maximumAlpha": max(unique_alpha),
        "transparentRgbIsZero": transparent_rgb_zero,
        "_occupied": occupied,
    }


def artifact_record(path: Path, repo_root: Path) -> dict[str, Any]:
    return {
        "path": path.relative_to(repo_root).as_posix(),
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
        "mediaType": "image/png",
    }


def packed_texture_records() -> list[dict[str, Any]]:
    records = []
    for image in sorted(bpy.data.images, key=lambda candidate: candidate.name):
        if image.source == "VIEWER":
            continue
        records.append(
            {
                "name": image.name,
                "size": list(image.size),
                "source": image.source,
                "packed": image.packed_file is not None,
            }
        )
    return records


def main() -> int:
    args = parse_args(blender_arguments())
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise ValueError(
            f"Expected Blender {EXPECTED_BLENDER[0]}.{EXPECTED_BLENDER[1]}.x, "
            f"found {bpy.app.version_string}"
        )

    repo_root = repository_root()
    input_blend, input_relative = resolve_input(args.input_blend, repo_root)
    output_dir, output_relative = resolve_output(args.output_dir, repo_root)
    source_hash_before_open = sha256(input_blend)
    if source_hash_before_open != args.expected_source_sha256:
        raise ValueError(
            "Source hash mismatch before opening Blender file: "
            f"expected {args.expected_source_sha256}, "
            f"observed {source_hash_before_open}"
        )
    if args.expected_source_sha256 != EXPECTED_SOURCE_BLEND_SHA256:
        raise ValueError(
            "This derivation is pinned to the accepted textured Bulwark "
            f"authority {EXPECTED_SOURCE_BLEND_SHA256}; received "
            f"{args.expected_source_sha256}"
        )
    runtime_glb = input_blend.with_name(EXPECTED_RUNTIME_GLB_NAME)
    if not runtime_glb.is_file():
        raise ValueError(f"Missing accepted sibling runtime GLB: {runtime_glb.name}")
    runtime_glb_hash = sha256(runtime_glb)
    if runtime_glb_hash != EXPECTED_RUNTIME_GLB_SHA256:
        raise ValueError(
            "Accepted sibling runtime GLB hash mismatch: "
            f"expected {EXPECTED_RUNTIME_GLB_SHA256}, "
            f"observed {runtime_glb_hash}"
        )

    output_dir.mkdir(parents=True)
    bpy.ops.wm.open_mainfile(filepath=str(input_blend))
    if sha256(input_blend) != args.expected_source_sha256:
        raise ValueError("Source blend hash changed while Blender opened it")

    root = bpy.data.objects.get(ROOT_NAME)
    if root is None or root.type != "EMPTY" or root.parent is not None:
        raise ValueError(f"Missing authoritative root empty {ROOT_NAME!r}")
    model_objects = descendants(root)
    meshes = [obj for obj in model_objects if obj.type == "MESH"]
    team_meshes = [
        obj
        for obj in meshes
        if role_value(obj) == TEAM_ROLE
        or any(role_value(material) == TEAM_ROLE for material in obj.data.materials)
    ]
    if tuple(sorted(obj.name for obj in team_meshes)) != tuple(
        sorted(EXPECTED_TEAM_OBJECTS)
    ):
        raise ValueError("Semantic team-accent object set drifted")
    if len(meshes) != EXPECTED_MESH_COUNT:
        raise ValueError(
            f"Expected {EXPECTED_MESH_COUNT} mesh objects, found {len(meshes)}"
        )
    if len(team_meshes) != EXPECTED_TEAM_MESH_COUNT:
        raise ValueError(
            f"Expected {EXPECTED_TEAM_MESH_COUNT} team meshes, "
            f"found {len(team_meshes)}"
        )
    if any(
        len(obj.data.materials) != 1
        or role_value(obj.data.materials[0]) != TEAM_ROLE
        for obj in team_meshes
    ):
        raise ValueError("Team-accent meshes do not use one semantic material")

    scene = bpy.context.scene
    scene.frame_set(SOURCE_FRAME)
    bpy.context.view_layer.update()
    animation = animation_rest_record(model_objects)
    clips = action_clip_names()
    if animation["activeActionObjects"]:
        raise ValueError("Source has active object actions instead of a rest pose")
    if not animation["allNlaTracksMuted"]:
        raise ValueError("Source has an unmuted NLA track at the rest pose")
    if clips != EXPECTED_ACTION_CLIP_NAMES:
        raise ValueError(
            "Source semantic clip set drifted: "
            f"expected {sorted(EXPECTED_ACTION_CLIP_NAMES)}, "
            f"observed {sorted(clips)}"
        )

    minimum, maximum = world_bounds(meshes)
    center = (minimum + maximum) * 0.5
    target = Vector((center.x, center.y, 0.0))
    hide_non_model_objects(model_objects)

    scripts_dir = repo_root / "scripts/class-models"
    sys.path.insert(0, str(scripts_dir))
    import audit_smart_geometry_blender as audit

    camera, lights = configure_render_rig(target=target)
    avatar_path = render_avatar(
        output_dir=output_dir,
        camera=camera,
        audit=audit,
    )
    mask_path = render_semantic_mask(
        output_dir=output_dir,
        meshes=meshes,
        team_meshes=team_meshes,
        camera=camera,
        lights=lights,
        audit=audit,
    )

    avatar_alpha = alpha_record(avatar_path, audit)
    mask_alpha = alpha_record(mask_path, audit)
    avatar_occupied = avatar_alpha.pop("_occupied")
    mask_occupied = mask_alpha.pop("_occupied")
    mask_outside_avatar = sorted(mask_occupied - avatar_occupied)
    accent_ratio = len(mask_occupied) / len(avatar_occupied)

    camera_right = camera.matrix_world.to_3x3() @ Vector((1.0, 0.0, 0.0))
    camera_up = camera.matrix_world.to_3x3() @ Vector((0.0, 1.0, 0.0))
    source_hash_after_render = sha256(input_blend)
    output_records = {
        "avatar": artifact_record(avatar_path, repo_root),
        "teamAccentMask": artifact_record(mask_path, repo_root),
    }
    checks = {
        "pinnedBlender52": bpy.app.version[:2] == EXPECTED_BLENDER,
        "sourceHashMatchesBeforeOpen": source_hash_before_open
        == args.expected_source_sha256,
        "acceptedSourceBlendHashMatches": args.expected_source_sha256
        == EXPECTED_SOURCE_BLEND_SHA256,
        "sourceHashUnchangedAfterRender": source_hash_after_render
        == args.expected_source_sha256,
        "acceptedSiblingRuntimeGlbHashMatches": runtime_glb_hash
        == EXPECTED_RUNTIME_GLB_SHA256,
        "authoritativeRootFound": root.name == ROOT_NAME,
        "exactMeshCount": len(meshes) == EXPECTED_MESH_COUNT,
        "exactSemanticTeamMeshSet": tuple(sorted(obj.name for obj in team_meshes))
        == tuple(sorted(EXPECTED_TEAM_OBJECTS)),
        "sourceAtAnimationRestPose": (
            not animation["activeActionObjects"]
            and animation["allNlaTracksMuted"]
            and animation["frame"] == SOURCE_FRAME
        ),
        "semanticClipSetPreservedInSource": clips == EXPECTED_ACTION_CLIP_NAMES,
        "fourPacked2kPbrImages": (
            len(packed_texture_records()) == 4
            and all(
                record["packed"] and record["size"] == [2048, 2048]
                for record in packed_texture_records()
            )
        ),
        "avatarIsTransparentRgba512": (
            avatar_alpha["resolution"] == [OUTPUT_SIZE, OUTPUT_SIZE]
            and avatar_alpha["minimumAlpha"] == 0
            and avatar_alpha["maximumAlpha"] == 255
            and avatar_alpha["transparentRgbIsZero"]
        ),
        "avatarUsesCanonicalSixtyFiveToSeventyFivePercentSpan": (
            MINIMUM_OCCUPANCY
            <= avatar_alpha["maximumSpanFraction"]
            <= MAXIMUM_OCCUPANCY
        ),
        "teamMaskIsTransparentLosslessRgba512": (
            mask_alpha["resolution"] == [OUTPUT_SIZE, OUTPUT_SIZE]
            and mask_alpha["minimumAlpha"] == 0
            and mask_alpha["maximumAlpha"] == 255
            and mask_alpha["transparentRgbIsZero"]
        ),
        "teamMaskIsNonemptyAndRestrained": 0.001 <= accent_ratio <= 0.05,
        "teamMaskNeverEscapesVisibleAvatar": not mask_outside_avatar,
        "cameraPresentsPositiveXAsScreenRight": camera_right.x > 0.999999,
        "cameraPresentsPositiveYAsScreenUp": camera_up.y > 0.999999,
        "cyclesCpuSingleThreadFixedSeed": (
            scene.render.engine == "CYCLES"
            and scene.cycles.device == "CPU"
            and scene.cycles.samples == 16
            and not scene.cycles.use_denoising
            and not scene.cycles.use_adaptive_sampling
            and scene.cycles.seed == 0
            and not scene.cycles.use_animated_seed
            and scene.render.threads_mode == "FIXED"
            and scene.render.threads == 1
        ),
        "avatarBytesMatchIndependentPinnedReruns": (
            output_records["avatar"]["sha256"] == EXPECTED_AVATAR_SHA256
        ),
        "teamMaskBytesMatchIndependentPinnedReruns": (
            output_records["teamAccentMask"]["sha256"]
            == EXPECTED_MASK_SHA256
        ),
        "exactlyTwoDerivedPngOutputs": set(output_records)
        == {"avatar", "teamAccentMask"},
    }

    bias_degrees = math.degrees(
        math.atan2(
            math.hypot(*CAMERA_DEPTH_TIE_BIAS),
            CAMERA_HEIGHT,
        )
    )
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
            "runtimeFilesModified": False,
            "purpose": (
                "archive-only 2D derivation; existing SVG/runtime contracts "
                "remain authoritative until the mask pair is intentionally wired"
            ),
            "outputOverwritePolicy": "new directory only",
        },
        "source": {
            "path": input_relative.as_posix(),
            "sha256": args.expected_source_sha256,
            "bytes": input_blend.stat().st_size,
            "rootObject": root.name,
            "rootWorldMatrix": matrix_json(root.matrix_world),
            "runtimeGlb": {
                "path": runtime_glb.relative_to(repo_root).as_posix(),
                "sha256": runtime_glb_hash,
                "bytes": runtime_glb.stat().st_size,
            },
            "meshObjectCount": len(meshes),
            "bodyMeshObjectCount": len(meshes) - len(team_meshes),
            "teamAccentMeshObjects": sorted(obj.name for obj in team_meshes),
            "packedPbrImages": packed_texture_records(),
            "animationRest": animation,
        },
        "pose": {
            "semanticForm": "Mobile",
            "sourceFrame": SOURCE_FRAME,
            "allNlaTracksMuted": animation["allNlaTracksMuted"],
            "activeActionObjects": animation["activeActionObjects"],
            "worldBounds": {
                "minimum": vector_json(minimum),
                "maximum": vector_json(maximum),
                "span": vector_json(maximum - minimum),
            },
        },
        "camera": {
            "projection": "ORTHO",
            "orthoScale": CAMERA_ORTHO_SCALE,
            "location": vector_json(camera.location),
            "target": vector_json(target),
            "depthTieBiasXY": list(CAMERA_DEPTH_TIE_BIAS),
            "depthTieBiasDegreesFromExactTop": rounded(bias_degrees),
            "modelFacingAxis": "+X",
            "facingLabel": "East",
            "screenRightAxis": vector_json(camera_right),
            "screenUpAxis": vector_json(camera_up),
            "floorOrShadowRendered": False,
        },
        "raster": {
            "renderEngine": "CYCLES",
            "device": "CPU",
            "samples": 16,
            "denoising": False,
            "adaptiveSampling": False,
            "seed": 0,
            "animatedSeed": False,
            "threads": 1,
            "workingResolution": [WORK_SIZE, WORK_SIZE],
            "outputResolution": [OUTPUT_SIZE, OUTPUT_SIZE],
            "supersampling": (
                "fixed 2x2 straight-alpha-aware box downsample; final avatar "
                "RGB is stabilized to 6-bit steps and 4-bit steps below 80"
            ),
            "avatar": avatar_alpha,
            "teamAccentMask": {
                **mask_alpha,
                "encoding": (
                    "lossless metadata-free RGBA PNG; white RGB only where "
                    "straight 8-bit semantic coverage alpha is nonzero"
                ),
                "visibleRolePixelRatioToAvatar": rounded(accent_ratio),
                "pixelsOutsideVisibleAvatar": len(mask_outside_avatar),
                "occlusionMethod": (
                    "all body geometry renders black and all semantic role "
                    "geometry renders white in one depth-tested emission pass"
                ),
            },
        },
        "outputs": output_records,
        "determinism": {
            "method": (
                "two independent fresh pinned Blender 5.2 processes using "
                "single-thread CPU Cycles, 16 samples, fixed seed zero, no "
                "adaptive sampling, and no denoiser produced byte-identical "
                "metadata-free PNG payloads before archive"
            ),
            "expectedAvatarSha256": EXPECTED_AVATAR_SHA256,
            "expectedTeamAccentMaskSha256": EXPECTED_MASK_SHA256,
            "enforcedOnEveryRun": True,
        },
        "checks": checks,
        "passed": all(checks.values()),
        "reproduction": {
            "command": (
                "python3 scripts/class-models/derive_bulwark_avatar.py "
                f"--input-blend {input_relative.as_posix()} "
                f"--expected-source-sha256 {args.expected_source_sha256} "
                "--output-dir sandbox/bulwark-avatar-derivation-<new>"
            ),
            "outputDirectory": output_relative.as_posix(),
        },
        "limitations": [
            (
                "This archive candidate is not wired into Canvas, mobile, CLI, "
                "site, store, or WebGL fallback rendering."
            ),
            (
                "The avatar carries the source authority's neutral semantic "
                "emitters. Runtime team colour belongs to the separate mask."
            ),
        ],
    }
    report_path = output_dir / REPORT_NAME
    report_path.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    if not report["passed"]:
        failed = [name for name, passed in checks.items() if not passed]
        raise ValueError(f"Avatar derivation checks failed: {failed}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
