from __future__ import annotations

"""Render fixed Aegis Tortoise skin comparisons in pinned Blender 5.2.

This script intentionally runs *inside* Blender.  It opens each frozen
``textured-authority.blend`` in turn, renders the exact authored meshes and
PBR node graphs in Eevee, and changes only the semantic team-accent material
between neutral, blue, and orange variants.  The comparison floor is placed
below the authored zero plane so Mobile, Shell, and Tower read as low-hover bots.

Example:

    blender --background --factory-startup \
      --python scripts/class-models/render_aegis_tortoise_skin_comparison_blender.py \
      -- \
      --output-dir sandbox/aegis-tortoise-skin-comparison-v1 \
      --skin foundry=sandbox/aegis-tortoise-foundry-ga002-transplant-v1/textured-authority.blend \
      --skin polar=sandbox/aegis-tortoise-polar-ga002-transplant-v1/textured-authority.blend
"""

import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import sys
import traceback
from typing import Any
import zlib

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector


EXPECTED_BLENDER = (5, 2)
MODEL_COLLECTION = "NB_Bulwark_Kinematic_Master"
TEAM_ACCENT_ROLE = "team-accent"
SHELL_CLIP = "Shell_Deploy"
TOWER_CLIP = "Tower_Deploy"
MOBILE_FRAME = 1
EXPECTED_TOWER_FRAME = 96
RENDER_SIZE = 512
CAMERA_LOCATION = Vector((2.8, -3.2, 6.8))
CAMERA_TARGET = Vector((0.0, 0.0, 0.25))
CAMERA_ORTHO_SCALE = 1.72
GROUND_TOP_Z = -0.055
CONTACT_TILE_PIXELS = 384

SKIN_LABELS = {
    "foundry": "FOUNDRY BASTION",
    "polar": "POLAR TUNGSTEN",
    "verdant": "VERDANT RAMPART",
    "violet": "VIOLET CITADEL",
}

TEAM_VARIANTS: tuple[tuple[str, tuple[float, float, float, float], float], ...] = (
    ("neutral", (0.52, 0.62, 0.74, 1.0), 1.65),
    ("blue", (0.025, 0.24, 1.0, 1.0), 4.25),
    ("orange", (1.0, 0.19, 0.018, 1.0), 4.25),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument(
        "--skin",
        action="append",
        required=True,
        metavar="NAME=TEXTURED_AUTHORITY.BLEND",
    )
    parser.add_argument("--resolution", type=int, default=RENDER_SIZE)
    parser.add_argument("--body-metallic-factor", type=float)
    parser.add_argument("--exposure", type=float, default=0.35)
    parser.add_argument("--light-energy-scale", type=float, default=1.0)
    parser.add_argument("--render-samples", type=int, default=16)
    parser.add_argument("--supersample", type=int, choices=(1, 2), default=1)
    parser.add_argument("--team-emission-strength", type=float, default=4.25)
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    args = parser.parse_args(argv)
    if args.resolution < 256 or args.resolution > 1024:
        parser.error("--resolution must be in [256, 1024]")
    if args.body_metallic_factor is not None and not (
        0.0 <= args.body_metallic_factor <= 1.0
    ):
        parser.error("--body-metallic-factor must be in [0, 1]")
    if not -3.0 <= args.exposure <= 3.0:
        parser.error("--exposure must be in [-3, 3]")
    if not 0.1 <= args.light_energy_scale <= 2.0:
        parser.error("--light-energy-scale must be in [0.1, 2]")
    if not 1 <= args.render_samples <= 64:
        parser.error("--render-samples must be in [1, 64]")
    if not 1.0 <= args.team_emission_strength <= 12.0:
        parser.error("--team-emission-strength must be in [1, 12]")
    parsed: list[tuple[str, Path]] = []
    seen: set[str] = set()
    for raw in args.skin:
        name, separator, path = raw.partition("=")
        name = name.strip().lower()
        if not separator or not name or not path:
            parser.error(f"invalid --skin value: {raw!r}")
        if name in seen:
            parser.error(f"duplicate skin name: {name}")
        if not all(character.isalnum() or character in "-_" for character in name):
            parser.error(f"unsafe skin name: {name!r}")
        seen.add(name)
        parsed.append((name, Path(path).resolve()))
    args.skins = parsed
    return args


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


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
        raise ValueError(f"expected PNG: {path}")
    chunks: list[tuple[bytes, bytes]] = []
    offset = len(signature)
    while offset < len(source):
        length = struct.unpack(">I", source[offset : offset + 4])[0]
        kind = source[offset + 4 : offset + 8]
        payload = source[offset + 8 : offset + 8 + length]
        chunks.append((kind, payload))
        offset += 12 + length
        if kind == b"IEND":
            break
    ihdr = next((payload for kind, payload in chunks if kind == b"IHDR"), None)
    if ihdr is None:
        raise ValueError(f"PNG has no IHDR: {path}")
    width, height, depth, color_type, compression, filtering, interlace = struct.unpack(
        ">IIBBBBB", ihdr
    )
    if (depth, color_type, compression, filtering, interlace) != (8, 6, 0, 0, 0):
        raise ValueError(f"expected non-interlaced RGBA8 PNG: {path}")
    encoded = zlib.decompress(
        b"".join(payload for kind, payload in chunks if kind == b"IDAT")
    )
    stride = width * 4
    rows: list[bytearray] = []
    cursor = 0
    previous = bytearray(stride)
    for _ in range(height):
        filter_type = encoded[cursor]
        cursor += 1
        filtered = encoded[cursor : cursor + stride]
        cursor += stride
        row = bytearray(stride)
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
                raise ValueError(f"unsupported PNG filter in {path}")
            row[index] = (value + predictor) & 0xFF
        rows.append(row)
        previous = row
    return width, height, rows


def write_rgba_png(
    path: Path, *, width: int, height: int, rows: list[bytearray]
) -> None:
    if len(rows) != height or any(len(row) != width * 4 for row in rows):
        raise ValueError(f"invalid RGBA rows for {path}")
    signature = b"\x89PNG\r\n\x1a\n"
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    raw = b"".join(b"\x00" + bytes(row) for row in rows)
    path.write_bytes(
        signature
        + png_chunk(b"IHDR", ihdr)
        + png_chunk(b"IDAT", zlib.compress(raw, level=9))
        + png_chunk(b"IEND", b"")
    )


def box_downsample_rgba(
    rows: list[bytearray],
    *,
    width: int,
    height: int,
    target: int,
) -> list[bytearray]:
    output = [bytearray(target * 4) for _ in range(target)]
    for out_y in range(target):
        y0 = out_y * height / target
        y1 = (out_y + 1) * height / target
        for out_x in range(target):
            x0 = out_x * width / target
            x1 = (out_x + 1) * width / target
            sums = [0.0, 0.0, 0.0, 0.0]
            weight_sum = 0.0
            for source_y in range(math.floor(y0), math.ceil(y1)):
                weight_y = max(0.0, min(y1, source_y + 1) - max(y0, source_y))
                for source_x in range(math.floor(x0), math.ceil(x1)):
                    weight_x = max(0.0, min(x1, source_x + 1) - max(x0, source_x))
                    weight = weight_x * weight_y
                    index = source_x * 4
                    for channel in range(4):
                        sums[channel] += rows[source_y][index + channel] * weight
                    weight_sum += weight
            target_index = out_x * 4
            for channel in range(4):
                output[out_y][target_index + channel] = max(
                    0, min(255, int(round(sums[channel] / weight_sum)))
                )
    return output


def write_gameplay_downsample(source: Path, output: Path, *, target: int = 41) -> None:
    width, height, rows = decode_rgba_png(source)
    downsampled = box_downsample_rgba(
        rows, width=width, height=height, target=target
    )
    output.parent.mkdir(parents=True, exist_ok=True)
    write_rgba_png(output, width=target, height=target, rows=downsampled)


def canonicalize_render_png(path: Path) -> None:
    """Strip Blender PNG metadata and use one deterministic filter/deflate path."""
    width, height, rows = decode_rgba_png(path)
    write_rgba_png(path, width=width, height=height, rows=rows)


def image_difference(path_a: Path, path_b: Path, *, threshold: int = 12) -> dict[str, Any]:
    width_a, height_a, rows_a = decode_rgba_png(path_a)
    width_b, height_b, rows_b = decode_rgba_png(path_b)
    if (width_a, height_a) != (width_b, height_b):
        raise ValueError("image-difference dimensions differ")
    changed = 0
    maximum = 0
    total = 0
    for row_a, row_b in zip(rows_a, rows_b, strict=True):
        for index in range(0, len(row_a), 4):
            delta = max(
                abs(row_a[index + channel] - row_b[index + channel])
                for channel in range(3)
            )
            maximum = max(maximum, delta)
            total += delta
            if delta > threshold:
                changed += 1
    pixels = width_a * height_a
    return {
        "width": width_a,
        "height": height_a,
        "threshold8Bit": threshold,
        "changedPixels": changed,
        "changedRatio": rounded(changed / pixels),
        "meanMaximumChannelDelta8Bit": rounded(total / pixels),
        "maximumChannelDelta8Bit": maximum,
    }


def rounded(value: float, digits: int = 7) -> float:
    return round(float(value), digits)


def serialized_value(value: Any) -> Any:
    if isinstance(value, (bool, int, str)) or value is None:
        return value
    if isinstance(value, float):
        return rounded(value, 9)
    try:
        return [serialized_value(item) for item in value]
    except TypeError:
        return repr(value)


def material_fingerprint(materials: list[bpy.types.Material]) -> str:
    records: list[dict[str, Any]] = []
    for material in sorted(materials, key=lambda item: item.name):
        record: dict[str, Any] = {
            "name": material.name,
            "diffuseColor": serialized_value(material.diffuse_color),
            "metallic": rounded(material.metallic, 9),
            "roughness": rounded(material.roughness, 9),
            "useNodes": bool(material.use_nodes),
            "custom": {
                key: serialized_value(material[key])
                for key in sorted(material.keys())
                if key != "_RNA_UI"
            },
            "nodes": [],
            "links": [],
        }
        if material.use_nodes and material.node_tree is not None:
            for node in sorted(material.node_tree.nodes, key=lambda item: item.name):
                node_record: dict[str, Any] = {
                    "name": node.name,
                    "type": node.bl_idname,
                    "mute": bool(node.mute),
                    "inputs": {},
                }
                for socket in node.inputs:
                    if not hasattr(socket, "default_value"):
                        continue
                    node_record["inputs"][socket.name] = serialized_value(
                        socket.default_value
                    )
                image = getattr(node, "image", None)
                if image is not None:
                    node_record["image"] = image.name
                    node_record["imageSource"] = image.source
                record["nodes"].append(node_record)
            record["links"] = sorted(
                (
                    {
                    "fromNode": link.from_node.name,
                    "fromSocket": link.from_socket.name,
                    "toNode": link.to_node.name,
                    "toSocket": link.to_socket.name,
                    }
                    for link in material.node_tree.links
                ),
                key=lambda item: (
                    item["fromNode"],
                    item["fromSocket"],
                    item["toNode"],
                    item["toSocket"],
                ),
            )
        records.append(record)
    payload = json.dumps(records, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def material_graph_topology_fingerprint(
    materials: list[bpy.types.Material],
) -> str:
    """Compare reusable shader wiring while intentionally ignoring skin maps."""
    records: list[dict[str, Any]] = []
    for material in sorted(materials, key=lambda item: item.name):
        if not material.use_nodes or material.node_tree is None:
            records.append({"material": material.name, "useNodes": False})
            continue
        records.append(
            {
                "material": material.name,
                "nodes": sorted(
                    (node.name, node.bl_idname) for node in material.node_tree.nodes
                ),
                "links": sorted(
                    (
                        link.from_node.name,
                        link.from_socket.name,
                        link.to_node.name,
                        link.to_socket.name,
                    )
                    for link in material.node_tree.links
                ),
            }
        )
    payload = json.dumps(records, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def model_collection() -> bpy.types.Collection:
    collection = bpy.data.collections.get(MODEL_COLLECTION)
    if collection is None:
        raise ValueError(f"missing model collection {MODEL_COLLECTION!r}")
    return collection


def model_meshes(collection: bpy.types.Collection) -> list[bpy.types.Object]:
    return sorted(
        (obj for obj in collection.all_objects if obj.type == "MESH"),
        key=lambda item: item.name,
    )


def team_material() -> bpy.types.Material:
    matches = [
        material
        for material in bpy.data.materials
        if material.get("nilbotsRole") == TEAM_ACCENT_ROLE
    ]
    if len(matches) != 1:
        raise ValueError(
            f"expected one semantic team-accent material, found {len(matches)}"
        )
    material = matches[0]
    if not material.use_nodes or material.node_tree is None:
        raise ValueError("semantic team-accent material is not node based")
    return material


def validate_team_meshes(
    meshes: list[bpy.types.Object], material: bpy.types.Material
) -> list[bpy.types.Object]:
    accents = [
        obj for obj in meshes if obj.get("nilbotsRole") == TEAM_ACCENT_ROLE
    ]
    if len(accents) != 8:
        raise ValueError(f"expected 8 semantic accent meshes, found {len(accents)}")
    for obj in accents:
        if list(obj.data.materials) != [material]:
            raise ValueError(f"{obj.name} does not exclusively use {material.name}")
    return accents


def body_pbr_record(
    meshes: list[bpy.types.Object], accent_material: bpy.types.Material
) -> tuple[list[bpy.types.Material], dict[str, Any]]:
    used = {
        material
        for obj in meshes
        for material in obj.data.materials
        if material is not None and material != accent_material
    }
    body_materials = sorted(used, key=lambda item: item.name)
    textured: list[dict[str, Any]] = []
    for material in body_materials:
        if not material.use_nodes or material.node_tree is None:
            continue
        image_nodes = sorted(
            (
                node
                for node in material.node_tree.nodes
                if node.type == "TEX_IMAGE" and node.image is not None
            ),
            key=lambda item: item.name,
        )
        if not image_nodes:
            continue
        outputs = [node for node in material.node_tree.nodes if node.type == "OUTPUT_MATERIAL"]
        surface_linked = any(
            output.inputs.get("Surface") is not None
            and output.inputs["Surface"].is_linked
            for output in outputs
        )
        nodes: list[dict[str, Any]] = []
        for node in image_nodes:
            image = node.image
            packed_bytes = None
            if image.packed_file is not None:
                packed_bytes = len(image.packed_file.data)
            nodes.append(
                {
                    "node": node.name,
                    "image": image.name,
                    "colorSpace": image.colorspace_settings.name,
                    "packedBytes": packed_bytes,
                    "linked": any(link.from_node == node for link in material.node_tree.links),
                }
            )
        textured.append(
            {
                "material": material.name,
                "surfaceLinked": surface_linked,
                "imageNodes": nodes,
            }
        )
    visible = bool(textured) and all(
        record["surfaceLinked"]
        and len(record["imageNodes"]) >= 3
        and all(node["linked"] for node in record["imageNodes"])
        for record in textured
    )
    if not visible:
        raise ValueError("body PBR image-node graph is not fully connected")
    return body_materials, {
        "renderEngine": "BLENDER_EEVEE",
        "materials": textured,
        "textureNodesVisible": True,
    }


def apply_body_metallic_derivative(
    body_materials: list[bpy.types.Material],
    factor: float | None,
) -> list[dict[str, Any]]:
    """Apply an explicit in-memory runtime derivative; never save the source."""
    if factor is None:
        return []
    records: list[dict[str, Any]] = []
    for material in body_materials:
        if not material.use_nodes or material.node_tree is None:
            continue
        node = material.node_tree.nodes.get("NB.MetallicFactor")
        if node is None:
            continue
        if node.type != "MATH" or node.operation != "MULTIPLY":
            raise ValueError(
                f"{material.name}/NB.MetallicFactor is not the pinned multiplier"
            )
        multiplier = node.inputs[1]
        if multiplier.is_linked:
            raise ValueError(
                f"{material.name}/NB.MetallicFactor multiplier is unexpectedly linked"
            )
        original = float(multiplier.default_value)
        multiplier.default_value = factor
        records.append(
            {
                "material": material.name,
                "node": node.name,
                "socket": multiplier.name,
                "sourceValue": rounded(original),
                "runtimeDerivativeValue": rounded(factor),
            }
        )
    if not records:
        raise ValueError("runtime metallic derivative found no NB.MetallicFactor node")
    return records


def capture_rest_transforms(
    collection: bpy.types.Collection,
) -> dict[str, Any]:
    set_active_tracks(set())
    bpy.context.scene.frame_set(MOBILE_FRAME)
    bpy.context.view_layer.update()
    return {
        obj.name: obj.matrix_basis.copy()
        for obj in collection.all_objects
    }


def restore_rest_transforms(rest: dict[str, Any]) -> None:
    for name, matrix in rest.items():
        obj = bpy.data.objects.get(name)
        if obj is None:
            raise ValueError(f"rest object disappeared: {name}")
        obj.matrix_basis = matrix.copy()


def set_active_tracks(active_names: set[str]) -> None:
    for obj in bpy.data.objects:
        animation = obj.animation_data
        if animation is None:
            continue
        animation.action = None
        for track in animation.nla_tracks:
            track.mute = track.name not in active_names


def semantic_clip_range(clip_name: str) -> tuple[int, int]:
    starts: list[float] = []
    ends: list[float] = []
    for obj in bpy.data.objects:
        animation = obj.animation_data
        if animation is None:
            continue
        for track in animation.nla_tracks:
            if track.name != clip_name:
                continue
            for strip in track.strips:
                starts.append(strip.frame_start)
                ends.append(strip.frame_end)
    if not starts:
        raise ValueError(f"no {clip_name} NLA strips found")
    return int(round(min(starts))), int(round(max(ends)))


def sample_form(
    *, form: str, frame: int, rest: dict[str, Any]
) -> None:
    active = {
        "mobile": set(),
        "shell": {SHELL_CLIP},
        "tower": {TOWER_CLIP},
    }[form]
    set_active_tracks(set())
    restore_rest_transforms(rest)
    set_active_tracks(active)
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()


def set_principled_input(
    material: bpy.types.Material,
    names: tuple[str, ...],
    value: Any,
) -> None:
    principled = next(
        (node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"),
        None,
    )
    if principled is None:
        raise ValueError(f"{material.name} has no Principled BSDF")
    socket = next((principled.inputs.get(name) for name in names if principled.inputs.get(name)), None)
    if socket is None:
        raise ValueError(f"{material.name} lacks inputs {names!r}")
    socket.default_value = value


def set_team_variant(
    material: bpy.types.Material,
    *,
    color: tuple[float, float, float, float],
    emission_strength: float,
) -> None:
    material.diffuse_color = color
    set_principled_input(material, ("Base Color",), color)
    set_principled_input(material, ("Emission Color", "Emission"), color)
    set_principled_input(material, ("Emission Strength",), emission_strength)
    set_principled_input(material, ("Metallic",), 0.08)
    set_principled_input(material, ("Roughness",), 0.30)


def make_material(
    name: str,
    color: tuple[float, float, float, float],
    *,
    metallic: float = 0.0,
    roughness: float = 0.8,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    material.diffuse_color = color
    return material


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def configure_compositor_glow(scene: bpy.types.Scene) -> None:
    tree = bpy.data.node_groups.new("COMPARE.Compositor", "CompositorNodeTree")
    scene.compositing_node_group = tree
    layers = tree.nodes.new("CompositorNodeRLayers")
    glare = tree.nodes.new("CompositorNodeGlare")
    # Blender 5.2 moved Glare controls from node properties to sockets.
    glare.inputs["Type"].default_value = "Fog Glow"
    glare.inputs["Quality"].default_value = "High"
    glare.inputs["Threshold"].default_value = 1.0
    glare.inputs["Strength"].default_value = 0.30
    glare.inputs["Size"].default_value = 0.48
    tree.interface.new_socket(
        name="Image", in_out="OUTPUT", socket_type="NodeSocketColor"
    )
    composite = tree.nodes.new("NodeGroupOutput")
    tree.links.new(layers.outputs["Image"], glare.inputs["Image"])
    tree.links.new(glare.outputs["Image"], composite.inputs["Image"])


def add_soft_hover_shadow(ground_color: tuple[float, float, float, float]) -> None:
    material = bpy.data.materials.new("COMPARE.SoftHoverShadow.Material")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    texcoord = nodes.new("ShaderNodeTexCoord")
    subtract = nodes.new("ShaderNodeVectorMath")
    subtract.operation = "SUBTRACT"
    subtract.inputs[1].default_value = (0.5, 0.5, 0.0)
    length = nodes.new("ShaderNodeVectorMath")
    length.operation = "LENGTH"
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "EASE"
    ramp.color_ramp.elements.remove(ramp.color_ramp.elements[1])
    inner = ramp.color_ramp.elements[0]
    inner.position = 0.0
    inner.color = (0.025, 0.032, 0.035, 1.0)
    middle = ramp.color_ramp.elements.new(0.42)
    middle.color = (0.07, 0.085, 0.09, 1.0)
    outer = ramp.color_ramp.elements.new(0.70)
    outer.color = ground_color
    principled.inputs["Roughness"].default_value = 0.92
    principled.inputs["Metallic"].default_value = 0.02
    links.new(texcoord.outputs["Generated"], subtract.inputs[0])
    links.new(subtract.outputs["Vector"], length.inputs[0])
    links.new(length.outputs["Value"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], principled.inputs["Base Color"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0.0, 0.0, GROUND_TOP_Z + 0.0008))
    plane = bpy.context.object
    plane.name = "COMPARE.SoftHoverShadow"
    plane.dimensions = (1.46, 1.18, 1.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    plane.data.materials.append(material)


def configure_render_scene(
    resolution: int,
    *,
    exposure: float,
    light_energy_scale: float,
    render_samples: int,
) -> tuple[bpy.types.Object, float]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = resolution
    scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 15
    scene.render.film_transparent = False
    scene.render.use_file_extension = True
    scene.render.dither_intensity = 0.0
    scene.eevee.taa_render_samples = render_samples
    scene.eevee.taa_samples = render_samples
    scene.eevee.shadow_ray_count = min(4, render_samples)
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = exposure
    scene.view_settings.gamma = 1.0

    for obj in bpy.data.objects:
        if obj.type in {"LIGHT", "CAMERA"} or obj.name == "PROOF.Floor":
            obj.hide_render = True

    world = bpy.data.worlds.new("COMPARE.World")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.055, 0.080, 0.100, 1.0)
    background.inputs["Strength"].default_value = 0.72
    scene.world = world

    ground_color = (0.20, 0.25, 0.26, 1.0)
    floor_material = make_material(
        "COMPARE.MapGround.Material",
        ground_color,
        metallic=0.05,
        roughness=0.86,
    )
    floor_center_z = GROUND_TOP_Z - 0.025
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, floor_center_z))
    floor = bpy.context.object
    floor.name = "COMPARE.MapGround"
    floor.dimensions = (4.0, 4.0, 0.05)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    floor.data.materials.append(floor_material)

    add_soft_hover_shadow(ground_color)

    ring_material = make_material(
        "COMPARE.CaptureRing.Material",
        (0.38, 0.45, 0.46, 1.0),
        metallic=0.08,
        roughness=0.75,
    )
    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.755,
        minor_radius=0.010,
        major_segments=128,
        minor_segments=8,
        location=(0.0, 0.0, GROUND_TOP_Z + 0.003),
    )
    ring = bpy.context.object
    ring.name = "COMPARE.NeutralCaptureRing"
    ring.data.materials.append(ring_material)

    light_specs = (
        ("Key", (-1.8, -2.0, 3.4), 560.0, 3.2, (0.93, 0.97, 1.0)),
        ("Fill", (2.3, -0.4, 2.1), 300.0, 2.8, (0.48, 0.62, 0.82)),
        ("WarmRim", (-1.1, 2.1, 2.4), 310.0, 2.0, (0.94, 0.69, 0.40)),
        ("TopSoft", (0.0, 0.2, 4.8), 390.0, 3.8, (0.86, 0.91, 1.0)),
    )
    for name, location, energy, size, color in light_specs:
        data = bpy.data.lights.new(f"COMPARE.{name}.Data", "AREA")
        data.energy = energy * light_energy_scale
        data.shape = "DISK"
        data.size = size
        data.color = color
        # At one sample, temporally jittered area shadows visibly speckle; the
        # authored radial hover shadow remains as the deterministic fallback.
        data.use_shadow = render_samples > 1
        light = bpy.data.objects.new(f"COMPARE.{name}", data)
        scene.collection.objects.link(light)
        light.location = location
        look_at(light, Vector((0.0, 0.0, 0.22)))

    camera_data = bpy.data.cameras.new("COMPARE.GameplayCamera.Data")
    camera = bpy.data.objects.new("COMPARE.GameplayCamera", camera_data)
    scene.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = CAMERA_ORTHO_SCALE
    camera.location = CAMERA_LOCATION
    look_at(camera, CAMERA_TARGET)
    scene.camera = camera
    configure_compositor_glow(scene)
    bpy.context.view_layer.update()
    return camera, GROUND_TOP_Z


def evaluated_bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return minimum, maximum


def camera_bounds(
    scene: bpy.types.Scene,
    camera: bpy.types.Object,
    meshes: list[bpy.types.Object],
) -> dict[str, float]:
    points = [
        world_to_camera_view(scene, camera, obj.matrix_world @ Vector(corner))
        for obj in meshes
        for corner in obj.bound_box
    ]
    return {
        "minX": rounded(min(point.x for point in points)),
        "minY": rounded(min(point.y for point in points)),
        "maxX": rounded(max(point.x for point in points)),
        "maxY": rounded(max(point.y for point in points)),
    }


def render_skin(
    *,
    skin_name: str,
    blend_path: Path,
    output_dir: Path,
    resolution: int,
    body_metallic_factor: float | None,
    exposure: float,
    light_energy_scale: float,
    render_samples: int,
    supersample: int,
    team_emission_strength: float,
) -> dict[str, Any]:
    if not blend_path.is_file():
        raise ValueError(f"missing textured authority: {blend_path}")
    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise ValueError(f"expected Blender 5.2.x, got {bpy.app.version_string}")

    collection = model_collection()
    meshes = model_meshes(collection)
    accent_material = team_material()
    accents = validate_team_meshes(meshes, accent_material)
    body_materials, pbr = body_pbr_record(meshes, accent_material)
    metallic_derivative = apply_body_metallic_derivative(
        body_materials, body_metallic_factor
    )
    body_fingerprint_before = material_fingerprint(body_materials)
    clip_ranges = {
        clip: semantic_clip_range(clip) for clip in (SHELL_CLIP, TOWER_CLIP)
    }
    for clip, clip_range in clip_ranges.items():
        if clip_range != (MOBILE_FRAME, EXPECTED_TOWER_FRAME):
            raise ValueError(
                f"{clip} range changed: {clip_range}, "
                f"expected {(MOBILE_FRAME, EXPECTED_TOWER_FRAME)}"
            )
    rest = capture_rest_transforms(collection)
    camera, floor_top = configure_render_scene(
        resolution * supersample,
        exposure=exposure,
        light_energy_scale=light_energy_scale,
        render_samples=render_samples,
    )
    skin_dir = output_dir / "renders" / skin_name
    skin_dir.mkdir(parents=True, exist_ok=False)

    images: list[dict[str, Any]] = []
    form_records: dict[str, Any] = {}
    for form, frame in (
        ("mobile", MOBILE_FRAME),
        ("shell", clip_ranges[SHELL_CLIP][1]),
        ("tower", clip_ranges[TOWER_CLIP][1]),
    ):
        sample_form(form=form, frame=frame, rest=rest)
        minimum, maximum = evaluated_bounds(meshes)
        hover_gap = minimum.z - floor_top
        if not 0.035 <= hover_gap <= 0.15:
            raise ValueError(f"{form} hover gap {hover_gap:.6f} is not visibly low-hover")
        view_bounds = camera_bounds(bpy.context.scene, camera, meshes)
        if min(view_bounds["minX"], view_bounds["minY"]) < 0.02 or max(
            view_bounds["maxX"], view_bounds["maxY"]
        ) > 0.98:
            raise ValueError(f"{form} clips fixed gameplay framing: {view_bounds}")
        form_records[form] = {
            "frame": frame,
            "activeClip": {
                "mobile": None,
                "shell": SHELL_CLIP,
                "tower": TOWER_CLIP,
            }[form],
            "worldBounds": {
                "minimum": [rounded(value) for value in minimum],
                "maximum": [rounded(value) for value in maximum],
            },
            "cameraBounds": view_bounds,
            "groundTopZ": floor_top,
            "minimumHoverGap": rounded(hover_gap),
        }
        for variant_name, color, strength in TEAM_VARIANTS:
            effective_strength = (
                strength
                if variant_name == "neutral"
                else team_emission_strength
            )
            before = material_fingerprint(body_materials)
            set_team_variant(
                accent_material,
                color=color,
                emission_strength=effective_strength,
            )
            bpy.context.view_layer.update()
            after = material_fingerprint(body_materials)
            if before != after or after != body_fingerprint_before:
                raise ValueError("body PBR material changed during team variant render")
            path = skin_dir / f"{skin_name}-{form}-{variant_name}.png"
            work_path = (
                path
                if supersample == 1
                else skin_dir / f".{skin_name}-{form}-{variant_name}-work.png"
            )
            bpy.context.scene.render.filepath = str(work_path)
            bpy.ops.render.render(write_still=True)
            canonicalize_render_png(work_path)
            if supersample > 1:
                write_gameplay_downsample(work_path, path, target=resolution)
                work_path.unlink()
            image_record = {
                "skin": skin_name,
                "form": form,
                "frame": frame,
                "teamVariant": variant_name,
                "path": str(path.relative_to(output_dir)),
                "bytes": path.stat().st_size,
                "sha256": sha256_file(path),
                "renderMethod": (
                    f"{supersample}x offline supersample box-filtered to {resolution}px"
                    if supersample > 1
                    else f"native {resolution}px"
                ),
            }
            small = skin_dir / "gameplay-41px" / (
                f"{skin_name}-{form}-{variant_name}-41px.png"
            )
            write_gameplay_downsample(path, small)
            image_record["gameplay41"] = {
                "path": str(small.relative_to(output_dir)),
                "bytes": small.stat().st_size,
                "sha256": sha256_file(small),
                "method": (
                    f"deterministic box downsample from {resolution}px proof"
                ),
            }
            images.append(image_record)

    body_fingerprint_after = material_fingerprint(body_materials)
    if body_fingerprint_after != body_fingerprint_before:
        raise ValueError("body PBR material fingerprint changed across comparison")
    locomotion_names = sorted(
        obj.name
        for obj in collection.all_objects
        if any(token in obj.name.lower() for token in ("foot", "feet", "leg", "walk"))
    )
    if locomotion_names:
        raise ValueError(f"unexpected walking/foot cues in levitating authority: {locomotion_names}")
    return {
        "skin": skin_name,
        "label": SKIN_LABELS.get(skin_name, skin_name.upper()),
        "input": {
            "path": str(blend_path),
            "bytes": blend_path.stat().st_size,
            "sha256": sha256_file(blend_path),
        },
        "semanticClipRanges": {
            clip: list(clip_range) for clip, clip_range in clip_ranges.items()
        },
        "meshCount": len(meshes),
        "accentMeshCount": len(accents),
        "bodyPbr": pbr,
        "runtimeDerivative": {
            "kind": "body-metallic-factor"
            if metallic_derivative
            else "none-donor-exact",
            "sourceBlendSaved": False,
            "edits": metallic_derivative,
        },
        "bodyMaterialGraphTopologyFingerprint": material_graph_topology_fingerprint(
            body_materials
        ),
        "bodyMaterialFingerprintBefore": body_fingerprint_before,
        "bodyMaterialFingerprintAfter": body_fingerprint_after,
        "forms": form_records,
        "images": images,
        "levitationContract": {
            "locomotion": "low-hover",
            "walkOrFootCueNames": locomotion_names,
            "softHoverShadow": "COMPARE.SoftHoverShadow",
        },
    }


def emission_material(
    name: str,
    *,
    image: bpy.types.Image | None = None,
    color: tuple[float, float, float, float] = (1.0, 1.0, 1.0, 1.0),
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = color
    emission.inputs["Strength"].default_value = 1.0
    if image is not None:
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = image
        texture.interpolation = "Linear"
        links.new(texture.outputs["Color"], emission.inputs["Color"])
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return material


def add_contact_text(
    body: str,
    *,
    location: tuple[float, float, float],
    size: float,
    align_x: str,
    material: bpy.types.Material,
) -> None:
    curve = bpy.data.curves.new(f"CONTACT.Text.{body}.Data", "FONT")
    curve.body = body
    curve.align_x = align_x
    curve.align_y = "CENTER"
    curve.size = size
    curve.space_line = 0.82
    obj = bpy.data.objects.new(f"CONTACT.Text.{body}", curve)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    obj.data.materials.append(material)


def render_contact_sheet(
    *,
    output_dir: Path,
    skin_records: list[dict[str, Any]],
    profile_label: str,
) -> dict[str, Any]:
    for obj in bpy.data.objects:
        obj.hide_render = True
    scene = bpy.context.scene
    scene.compositing_node_group = None
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 1
    scene.eevee.taa_samples = 1
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 15
    scene.render.dither_intensity = 0.0

    row_keys = [
        (form, variant)
        for form in ("mobile", "shell", "tower")
        for variant, _color, _strength in TEAM_VARIANTS
    ]
    columns = len(skin_records)
    rows = len(row_keys)
    tile_px = CONTACT_TILE_PIXELS
    label_px = 208
    top_px = 132
    gap_px = 16
    edge_px = 16
    width_px = label_px + columns * tile_px + (columns - 1) * gap_px + edge_px
    height_px = top_px + rows * tile_px + (rows - 1) * gap_px + edge_px
    pixels_per_unit = tile_px
    width_world = width_px / pixels_per_unit
    height_world = height_px / pixels_per_unit
    label_world = label_px / pixels_per_unit
    top_world = top_px / pixels_per_unit
    gap_world = gap_px / pixels_per_unit

    world = bpy.data.worlds.new("CONTACT.World")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.018, 0.026, 0.035, 1.0)
    background.inputs["Strength"].default_value = 1.0
    scene.world = world

    text_material = emission_material(
        "CONTACT.Text.Material", color=(0.80, 0.86, 0.90, 1.0)
    )
    add_contact_text(
        profile_label,
        location=(0.0, height_world / 2 - 0.055, 0.08),
        size=0.052,
        align_x="CENTER",
        material=text_material,
    )
    for column, record in enumerate(skin_records):
        x = -width_world / 2 + label_world + 0.5 + column * (1.0 + gap_world)
        y = height_world / 2 - top_world * 0.72
        add_contact_text(
            record["label"],
            location=(x, y, 0.08),
            size=0.078,
            align_x="CENTER",
            material=text_material,
        )

    for row, (form, variant) in enumerate(row_keys):
        y = height_world / 2 - top_world - 0.5 - row * (1.0 + gap_world)
        add_contact_text(
            f"{form.upper()}\n{variant.upper()}",
            location=(-width_world / 2 + label_world - 0.055, y, 0.08),
            size=0.066,
            align_x="RIGHT",
            material=text_material,
        )
        for column, record in enumerate(skin_records):
            x = -width_world / 2 + label_world + 0.5 + column * (1.0 + gap_world)
            image_record = next(
                item
                for item in record["images"]
                if item["form"] == form and item["teamVariant"] == variant
            )
            image_path = output_dir / image_record["path"]
            image = bpy.data.images.load(str(image_path), check_existing=False)
            material = emission_material(
                f"CONTACT.{record['skin']}.{form}.{variant}.Material",
                image=image,
            )
            bpy.ops.mesh.primitive_plane_add(size=1.0, location=(x, y, 0.0))
            plane = bpy.context.object
            plane.name = f"CONTACT.{record['skin']}.{form}.{variant}"
            plane.data.materials.append(material)

    camera_data = bpy.data.cameras.new("CONTACT.Camera.Data")
    camera = bpy.data.objects.new("CONTACT.Camera", camera_data)
    scene.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = height_world
    camera.location = (0.0, 0.0, 10.0)
    camera.rotation_euler = (0.0, 0.0, 0.0)
    scene.camera = camera
    scene.render.resolution_x = width_px
    scene.render.resolution_y = height_px
    scene.render.resolution_percentage = 100
    path = output_dir / "aegis-tortoise-ga002-skin-comparison.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    canonicalize_render_png(path)
    return {
        "path": str(path.relative_to(output_dir)),
        "bytes": path.stat().st_size,
        "sha256": sha256_file(path),
        "width": width_px,
        "height": height_px,
        "columns": [record["skin"] for record in skin_records],
        "rows": [f"{form}-{variant}" for form, variant in row_keys],
    }


def foundry_orange_collision_evidence(
    *, output_dir: Path, skin_records: list[dict[str, Any]]
) -> dict[str, Any]:
    foundry = next(
        (record for record in skin_records if record["skin"] == "foundry"), None
    )
    if foundry is None:
        return {"evaluated": False, "reason": "foundry skin not included"}

    def gameplay_path(form: str, variant: str) -> Path:
        image = next(
            item
            for item in foundry["images"]
            if item["form"] == form and item["teamVariant"] == variant
        )
        return output_dir / image["gameplay41"]["path"]

    forms: dict[str, Any] = {}
    for form in ("mobile", "shell", "tower"):
        neutral = gameplay_path(form, "neutral")
        blue = gameplay_path(form, "blue")
        orange = gameplay_path(form, "orange")
        forms[form] = {
            "neutralVsOrangeTeam": image_difference(neutral, orange),
            "blueVsOrangeTeam": image_difference(blue, orange),
        }
    passed = all(
        evidence[comparison]["changedPixels"] >= 6
        for evidence in forms.values()
        for comparison in ("neutralVsOrangeTeam", "blueVsOrangeTeam")
    )
    return {
        "evaluated": True,
        "passed": passed,
        "method": (
            "41px semantic-variant difference; body PBR fingerprint is invariant, "
            "so changed pixels are restricted to the renderer-owned emitter shapes "
            "and their restrained glow while fixed Foundry hull orange remains fixed"
        ),
        "minimumChangedPixels": 6,
        "forms": forms,
    }


def main() -> None:
    args = parse_args()
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise ValueError(f"expected Blender 5.2.x, got {bpy.app.version_string}")
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=False)
    records = [
        render_skin(
            skin_name=name,
            blend_path=path,
            output_dir=output_dir,
            resolution=args.resolution,
            body_metallic_factor=args.body_metallic_factor,
            exposure=args.exposure,
            light_energy_scale=args.light_energy_scale,
            render_samples=args.render_samples,
            supersample=args.supersample,
            team_emission_strength=args.team_emission_strength,
        )
        for name, path in args.skins
    ]
    profile = (
        "runtime-calibrated"
        if args.body_metallic_factor is not None
        else "donor-exact"
    )
    profile_label = (
        f"CALIBRATED · M {args.body_metallic_factor:.2f} · "
        f"EXP {args.exposure:+.2f} · L {args.light_energy_scale:.2f}X · "
        f"TEAM {args.team_emission_strength:.2f} · {args.supersample}X SS"
        if args.body_metallic_factor is not None
        else "DONOR-EXACT PBR · OFFLINE EEVEE LIGHT RIG"
    )
    contact_sheet = render_contact_sheet(
        output_dir=output_dir,
        skin_records=records,
        profile_label=profile_label,
    )
    collision_evidence = foundry_orange_collision_evidence(
        output_dir=output_dir, skin_records=records
    )
    body_graph_fingerprints = {
        record["bodyMaterialGraphTopologyFingerprint"] for record in records
    }
    manifest = {
        "schema": "nilbots-aegis-tortoise-skin-comparison-1",
        "status": "complete",
        "blender": {
            "version": bpy.app.version_string,
            "expectedMajorMinor": list(EXPECTED_BLENDER),
            "matchesExpectedMajorMinor": bpy.app.version[:2] == EXPECTED_BLENDER,
        },
        "geometryAuthorityId": "aegis-tortoise-ga-002",
        "renderProfile": {
            "name": profile,
            "label": profile_label,
            "sourceBlendSaved": False,
            "bodyMetallicFactor": args.body_metallic_factor,
            "exposure": args.exposure,
            "lightEnergyScale": args.light_energy_scale,
            "teamEmissionStrength": args.team_emission_strength,
            "renderSamples": args.render_samples,
            "supersample": args.supersample,
            "note": (
                "Separate runtime visibility derivative; not donor-exact."
                if args.body_metallic_factor is not None
                else "Body PBR factors are donor-exact."
            ),
        },
        "render": {
            "engine": "BLENDER_EEVEE",
            "eeveeRenderSamples": args.render_samples,
            "determinism": (
                "fixed camera/settings; rendered PNG metadata canonicalized; "
                "41px proofs use deterministic box downsample"
            ),
            "resolution": [args.resolution, args.resolution],
            "workResolution": [
                args.resolution * args.supersample,
                args.resolution * args.supersample,
            ],
            "camera": {
                "projection": "orthographic",
                "location": [rounded(value) for value in CAMERA_LOCATION],
                "target": [rounded(value) for value in CAMERA_TARGET],
                "orthoScale": CAMERA_ORTHO_SCALE,
                "framing": "fixed honest top-down-ish gameplay view",
            },
            "world": "mid-bright non-black map proof",
            "groundTopZ": GROUND_TOP_Z,
            "teamGlow": "restrained fixed compositor fog glow",
            "teamVariants": [
                {
                    "name": name,
                    "linearRgba": [rounded(value) for value in color],
                    "emissionStrength": (
                        strength
                        if name == "neutral"
                        else args.team_emission_strength
                    ),
                }
                for name, color, strength in TEAM_VARIANTS
            ],
        },
        "forms": {
            "mobile": {"frame": MOBILE_FRAME, "activeClip": None},
            "shell": {
                "frame": EXPECTED_TOWER_FRAME,
                "activeClip": SHELL_CLIP,
                "mobilize": "reverse Shell_Deploy",
            },
            "tower": {
                "frame": EXPECTED_TOWER_FRAME,
                "activeClip": TOWER_CLIP,
                "note": "Authority endpoint verified as 96; frame 80 is mid-deploy.",
            },
        },
        "skins": records,
        "contactSheet": contact_sheet,
        "foundryOrangeTeamCollision": collision_evidence,
        "checks": {
            "pinnedBlender52": bpy.app.version[:2] == EXPECTED_BLENDER,
            "eeveePbrTextureNodesVisible": all(
                record["bodyPbr"]["textureNodesVisible"] for record in records
            ),
            "towerEndpointVerifiedAt96": all(
                record["semanticClipRanges"][TOWER_CLIP] == [1, 96]
                for record in records
            ),
            "shellEndpointVerifiedAt96": all(
                record["semanticClipRanges"][SHELL_CLIP] == [1, 96]
                for record in records
            ),
            "bodyPbrMaterialsUnchangedAcrossTeamVariants": all(
                record["bodyMaterialFingerprintBefore"]
                == record["bodyMaterialFingerprintAfter"]
                for record in records
            ),
            "allSkinsShareBodyMaterialGraphTopology": len(body_graph_fingerprints) == 1,
            "teamVariantsChangeOnlySemanticAccentMaterial": True,
            "semanticAccentMeshCountEightPerSkin": all(
                record["accentMeshCount"] == 8 for record in records
            ),
            "allFormsRemainLowHover": all(
                0.035 <= form["minimumHoverGap"] <= 0.15
                for record in records
                for form in record["forms"].values()
            ),
            "noFeetLegsOrWalkCues": all(
                not record["levitationContract"]["walkOrFootCueNames"]
                for record in records
            ),
            "allImagesRendered": all(
                len(record["images"]) == len(TEAM_VARIANTS) * 3
                for record in records
            ),
            "allGameplay41ProofsRendered": all(
                "gameplay41" in image
                for record in records
                for image in record["images"]
            ),
            "foundryOrangeTeamDistinctAt41px": (
                collision_evidence.get("passed", True)
            ),
        },
    }
    manifest_path = output_dir / "manifest.json"
    manifest_path.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps({"manifest": str(manifest_path), "checks": manifest["checks"]}))


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        # Blender 5.2 otherwise exits zero after an uncaught --python error.
        raise SystemExit(1)
