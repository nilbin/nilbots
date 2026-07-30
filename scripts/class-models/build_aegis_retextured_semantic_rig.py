#!/usr/bin/env python3
"""Build the textured Aegis Mobile semantic rig from one archived Meshy GLB.

Run this script through the repository's pinned Blender 5.2 executable:

    Blender --background --factory-startup --python-exit-code 1 \
      --python scripts/class-models/build_aegis_retextured_semantic_rig.py -- \
      --input-glb <raw-retextured-model.glb> \
      --source-task-id <Meshy retexture task id> \
      --expected-input-task-id <accepted geometry task id> \
      --expected-source-sha256 <sha256> \
      --output-dir sandbox/<new-directory>

The source GLB is never modified. The script verifies its local Meshy
provenance, applies the pinned rigid orientation and one recorded uniform
normalization/floor transform, partitions complete source components into the
proven nine Aegis Mobile groups, and authors two rigid NLA clips. Mesh data is
copied and unwanted component vertices are deleted through Blender's mesh
operator so provider UVs, face material assignments, custom loop data, PBR
material nodes, and source images survive. No mesh is reconstructed with
``from_pydata`` and no provider or network operation is available here.
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
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable

import bpy
import bmesh
from mathutils import Matrix, Vector


SCHEMA = "nilbots-aegis-retextured-semantic-rig-1"
TOOL_VERSION = "1.0.0"
EXPECTED_BLENDER = (5, 2)
EXPECTED_WELDED_COMPONENTS = 122
PROVEN_UNTEXTURED_RAW_INDEXED_COMPONENTS = 142
PROVEN_UNTEXTURED_SOURCE_TRIANGLES = 12_984
EXPECTED_ORIENTATION = Matrix(
    (
        (-1.0, 0.0, 0.0, 0.0),
        (0.0, 0.0, -1.0, 0.0),
        (0.0, -1.0, 0.0, 0.0),
        (0.0, 0.0, 0.0, 1.0),
    )
)
GROUP_ORDER = (
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
POD_GROUPS = (
    "Pod_NorthOuter",
    "Pod_NorthInner",
    "Pod_SouthInner",
    "Pod_SouthOuter",
)
SOURCE_POLYGON_ATTRIBUTE = "source_polygon_id"
SOURCE_VERTEX_ATTRIBUTE = "source_vertex_id"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def blender_arguments() -> list[str]:
    return sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []


def finite_positive(value: str) -> float:
    result = float(value)
    if not math.isfinite(result) or result <= 0.0:
        raise argparse.ArgumentTypeError("value must be finite and greater than zero")
    return result


def finite_nonnegative(value: str) -> float:
    result = float(value)
    if not math.isfinite(result) or result < 0.0:
        raise argparse.ArgumentTypeError("value must be finite and nonnegative")
    return result


def sha256_argument(value: str) -> str:
    normalized = value.strip().lower()
    if not SHA256_RE.fullmatch(normalized):
        raise argparse.ArgumentTypeError("expected a lowercase 64-character SHA-256")
    return normalized


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Build a deterministic nine-group textured Aegis Mobile rig from "
            "an already-archived local Meshy retexture result. No provider or "
            "network calls are performed."
        )
    )
    parser.add_argument("--input-glb", type=Path, required=True)
    parser.add_argument(
        "--source-record",
        type=Path,
        help=(
            "Archived request-record.json. Defaults to the input GLB sibling "
            "with that name."
        ),
    )
    parser.add_argument("--source-task-id", required=True)
    parser.add_argument("--expected-input-task-id", required=True)
    parser.add_argument(
        "--expected-source-sha256",
        type=sha256_argument,
        required=True,
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        required=True,
        help=(
            "New directory below this repository's ignored sandbox/ or var/ "
            "tree. Existing directories are never overwritten."
        ),
    )
    parser.add_argument(
        "--target-planform-span",
        type=finite_positive,
        default=1.0,
        help="Uniformly normalize max(X span, Y span) to this value; default 1.",
    )
    parser.add_argument(
        "--hover-height",
        type=finite_nonnegative,
        default=0.12,
        help="Mobile rest hover height in normalized Blender units; default .12.",
    )
    parser.add_argument(
        "--planted-hover-height",
        type=finite_nonnegative,
        default=0.04,
        help="Settled chassis hover height during pod plant; default .04.",
    )
    parser.add_argument(
        "--recoil-distance",
        type=finite_positive,
        default=0.035,
        help="Gun recoil distance opposite +X facing; default .035.",
    )
    args = parser.parse_args(argv)
    if args.planted_hover_height > args.hover_height:
        parser.error("--planted-hover-height cannot exceed --hover-height")
    return args


def resolve_existing_file(path: Path, repo: Path, suffix: str) -> Path:
    resolved = path if path.is_absolute() else repo / path
    resolved = resolved.resolve(strict=True)
    if not resolved.is_file() or resolved.suffix.lower() != suffix:
        raise ValueError(f"Expected an existing {suffix} file: {resolved}")
    return resolved


def resolve_output_directory(path: Path, repo: Path) -> Path:
    resolved = path if path.is_absolute() else repo / path
    resolved = resolved.resolve(strict=False)
    try:
        relative = resolved.relative_to(repo)
    except ValueError as error:
        raise ValueError("--output-dir must stay inside the repository") from error
    if not relative.parts or relative.parts[0] not in {"sandbox", "var"}:
        raise ValueError("--output-dir must be below repository sandbox/ or var/")
    if resolved.exists():
        raise ValueError(f"Output directory already exists: {resolved}")
    return resolved


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def canonical_json_hash(value: Any) -> str:
    encoded = json.dumps(
        value,
        ensure_ascii=True,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def rounded(value: float, places: int = 9) -> float:
    result = round(float(value), places)
    return 0.0 if result == -0.0 else result


def vector_json(vector: Vector) -> list[float]:
    return [rounded(value) for value in vector]


def matrix_json(matrix: Matrix) -> list[list[float]]:
    return [[rounded(value) for value in row] for row in matrix]


def bounds(points: Iterable[Vector]) -> tuple[Vector, Vector]:
    values = list(points)
    if not values:
        zero = Vector((0.0, 0.0, 0.0))
        return zero.copy(), zero.copy()
    return (
        Vector(tuple(min(point[axis] for point in values) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in values) for axis in range(3))),
    )


def bounds_record(points: Iterable[Vector]) -> dict[str, list[float]]:
    minimum, maximum = bounds(points)
    return {
        "minimum": vector_json(minimum),
        "maximum": vector_json(maximum),
        "center": vector_json((minimum + maximum) * 0.5),
        "span": vector_json(maximum - minimum),
    }


def validate_provenance(
    *,
    input_glb: Path,
    record_path: Path,
    source_task_id: str,
    expected_input_task_id: str,
    expected_sha256: str,
) -> dict[str, Any]:
    actual_sha256 = sha256(input_glb)
    if actual_sha256 != expected_sha256:
        raise ValueError(
            "Source hash mismatch: "
            f"expected {expected_sha256}, observed {actual_sha256}"
        )
    record = json.loads(record_path.read_text(encoding="utf-8"))
    if record.get("taskId") != source_task_id:
        raise ValueError(
            "Source task mismatch: "
            f"expected {source_task_id!r}, record has {record.get('taskId')!r}"
        )
    if record.get("inputTaskId") != expected_input_task_id:
        raise ValueError(
            "Input geometry task mismatch: "
            f"expected {expected_input_task_id!r}, "
            f"record has {record.get('inputTaskId')!r}"
        )
    if record.get("status") != "SUCCEEDED":
        raise ValueError(
            "Archived Meshy request did not succeed: "
            f"status={record.get('status')!r}"
        )
    artifacts = record.get("artifacts")
    if not isinstance(artifacts, list):
        raise ValueError("Source record has no artifacts list")
    matching = [
        artifact
        for artifact in artifacts
        if artifact.get("file") == input_glb.name
        or artifact.get("file") == "raw-retextured-model.glb"
    ]
    if len(matching) != 1:
        raise ValueError(
            "Source record must name exactly one raw-retextured-model.glb artifact"
        )
    artifact_sha256 = matching[0].get("sha256")
    if artifact_sha256 != expected_sha256:
        raise ValueError(
            "Source record artifact hash mismatch: "
            f"expected {expected_sha256}, record has {artifact_sha256!r}"
        )
    return {
        "provider": record.get("provider"),
        "endpoint": record.get("endpoint"),
        "taskId": source_task_id,
        "inputTaskId": expected_input_task_id,
        "requestFingerprint": record.get("requestFingerprint"),
        "status": record.get("status"),
        "state": record.get("state"),
        "consumedCredits": record.get("consumedCredits"),
        "sourceSha256": actual_sha256,
    }


def parse_glb(path: Path) -> tuple[dict[str, Any], bytes]:
    with path.open("rb") as stream:
        header = stream.read(12)
        if len(header) != 12:
            raise ValueError(f"Truncated GLB header: {path}")
        magic, version, length = struct.unpack("<4sII", header)
        if magic != b"glTF" or version != 2 or length != path.stat().st_size:
            raise ValueError(f"Invalid GLB 2.0 header: {path}")
        document = None
        binary = b""
        while stream.tell() < length:
            chunk_header = stream.read(8)
            if len(chunk_header) != 8:
                raise ValueError(f"Truncated GLB chunk header: {path}")
            chunk_length, chunk_type = struct.unpack("<II", chunk_header)
            payload = stream.read(chunk_length)
            if len(payload) != chunk_length:
                raise ValueError(f"Truncated GLB chunk: {path}")
            if chunk_type == 0x4E4F534A:
                document = json.loads(payload.decode("utf-8").rstrip("\x00 "))
            elif chunk_type == 0x004E4942:
                binary = payload
        if document is None:
            raise ValueError(f"GLB contains no JSON chunk: {path}")
        return document, binary


def glb_image_records(document: dict[str, Any], binary: bytes) -> list[dict[str, Any]]:
    buffer_views = document.get("bufferViews", [])
    records = []
    for index, image in enumerate(document.get("images", [])):
        record = {
            "index": index,
            "name": image.get("name"),
            "mimeType": image.get("mimeType"),
            "uri": image.get("uri"),
            "bytes": None,
            "sha256": None,
        }
        if "bufferView" in image:
            view = buffer_views[int(image["bufferView"])]
            if int(view.get("buffer", 0)) != 0:
                raise ValueError("Only the GLB binary buffer is supported")
            offset = int(view.get("byteOffset", 0))
            length = int(view["byteLength"])
            payload = binary[offset : offset + length]
            if len(payload) != length:
                raise ValueError("GLB image buffer view exceeds binary chunk")
            record["bytes"] = length
            record["sha256"] = hashlib.sha256(payload).hexdigest()
        records.append(record)
    return records


def glb_material_records(document: dict[str, Any]) -> list[dict[str, Any]]:
    records = []
    for index, material in enumerate(document.get("materials", [])):
        pbr = material.get("pbrMetallicRoughness", {})
        roles = {
            "baseColor": "baseColorTexture" in pbr,
            "metallicRoughness": "metallicRoughnessTexture" in pbr,
            "normal": "normalTexture" in material,
            "emissive": "emissiveTexture" in material,
            "occlusion": "occlusionTexture" in material,
        }
        records.append(
            {
                "index": index,
                "name": material.get("name"),
                "doubleSided": material.get("doubleSided", False),
                "textureRoles": roles,
                "textureRoleCount": sum(roles.values()),
            }
        )
    return records


def glb_triangle_count(document: dict[str, Any]) -> int:
    accessors = document.get("accessors", [])
    total = 0
    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            mode = int(primitive.get("mode", 4))
            accessor_index = primitive.get("indices")
            if accessor_index is None:
                position_index = primitive.get("attributes", {}).get("POSITION")
                if position_index is None:
                    continue
                count = int(accessors[int(position_index)]["count"])
            else:
                count = int(accessors[int(accessor_index)]["count"])
            if mode == 4:
                total += count // 3
            elif mode in (5, 6):
                total += max(0, count - 2)
            else:
                raise ValueError(f"Unsupported non-triangle GLB primitive mode {mode}")
    return total


def glb_summary(path: Path) -> dict[str, Any]:
    document, binary = parse_glb(path)
    animation_names = [
        animation.get("name", "") for animation in document.get("animations", [])
    ]
    return {
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
        "generator": document.get("asset", {}).get("generator"),
        "nodes": len(document.get("nodes", [])),
        "meshes": len(document.get("meshes", [])),
        "primitives": sum(
            len(mesh.get("primitives", [])) for mesh in document.get("meshes", [])
        ),
        "triangles": glb_triangle_count(document),
        "materials": glb_material_records(document),
        "textures": len(document.get("textures", [])),
        "images": glb_image_records(document, binary),
        "animations": [
            {
                "name": animation.get("name", ""),
                "channels": len(animation.get("channels", [])),
                "samplers": len(animation.get("samplers", [])),
            }
            for animation in document.get("animations", [])
        ],
        "animationNames": animation_names,
        "nodeExtras": {
            node.get("name", f"node-{index}"): node.get("extras")
            for index, node in enumerate(document.get("nodes", []))
            if node.get("extras") is not None
        },
    }


def clear_scene() -> None:
    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
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
    imported.sort(key=lambda obj: (obj.name, obj.type))
    if not any(obj.type == "MESH" for obj in imported):
        raise ValueError("Imported GLB contains no mesh object")
    bpy.context.view_layer.update()
    return imported


def make_collection(name: str) -> bpy.types.Collection:
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def add_empty(
    name: str,
    collection: bpy.types.Collection,
    *,
    parent: bpy.types.Object | None = None,
    location: Iterable[float] = (0.0, 0.0, 0.0),
    display: str = "PLAIN_AXES",
    size: float = 0.03,
) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    collection.objects.link(obj)
    obj.empty_display_type = display
    obj.empty_display_size = size
    obj.rotation_mode = "XYZ"
    if parent is not None:
        obj.parent = parent
    obj.location = tuple(location)
    return obj


class UnionFind:
    def __init__(self, size: int):
        self.parent = list(range(size))
        self.rank = [0] * size

    def find(self, item: int) -> int:
        while self.parent[item] != item:
            self.parent[item] = self.parent[self.parent[item]]
            item = self.parent[item]
        return item

    def union(self, left: int, right: int) -> None:
        left_root = self.find(left)
        right_root = self.find(right)
        if left_root == right_root:
            return
        if self.rank[left_root] < self.rank[right_root]:
            left_root, right_root = right_root, left_root
        self.parent[right_root] = left_root
        if self.rank[left_root] == self.rank[right_root]:
            self.rank[left_root] += 1


def quantized_key(point: Vector, epsilon: float = 1e-6) -> tuple[int, int, int]:
    return tuple(round(value / epsilon) for value in point)


def welded_components(
    mesh: bpy.types.Mesh,
    world_points: list[Vector],
    *,
    weld_epsilon: float = 1e-6,
) -> tuple[list[dict[str, Any]], dict[int, int], int]:
    indexed = UnionFind(len(mesh.vertices))
    welded = UnionFind(len(mesh.vertices))
    for polygon in mesh.polygons:
        vertices = list(polygon.vertices)
        for left, right in zip(vertices, vertices[1:] + vertices[:1]):
            indexed.union(left, right)
            welded.union(left, right)

    buckets: dict[tuple[int, int, int], list[int]] = defaultdict(list)
    for index, point in enumerate(world_points):
        buckets[quantized_key(point, weld_epsilon)].append(index)
    for indices in buckets.values():
        if len(indices) < 2:
            continue
        anchor = indices[0]
        coincident = [
            index
            for index in indices
            if (world_points[index] - world_points[anchor]).length <= weld_epsilon
        ]
        for index in coincident[1:]:
            welded.union(anchor, index)

    vertices_by_root: dict[int, list[int]] = defaultdict(list)
    polygons_by_root: dict[int, list[int]] = defaultdict(list)
    for vertex_index in range(len(mesh.vertices)):
        vertices_by_root[welded.find(vertex_index)].append(vertex_index)
    for polygon in mesh.polygons:
        polygons_by_root[welded.find(polygon.vertices[0])].append(polygon.index)

    sorted_roots = sorted(
        vertices_by_root,
        key=lambda root: (
            -len(polygons_by_root.get(root, [])),
            -len(vertices_by_root[root]),
            root,
        ),
    )
    components = []
    component_by_polygon: dict[int, int] = {}
    for component_index, root in enumerate(sorted_roots):
        vertex_indices = vertices_by_root[root]
        polygon_indices = polygons_by_root.get(root, [])
        component_points = [world_points[index] for index in vertex_indices]
        record = {
            "componentIndex": component_index,
            "vertexIndices": vertex_indices,
            "polygonIndices": polygon_indices,
            "vertexCount": len(vertex_indices),
            "triangleCount": sum(
                len(mesh.polygons[index].vertices) - 2 for index in polygon_indices
            ),
            "bounds": bounds_record(component_points),
        }
        components.append(record)
        for polygon_index in polygon_indices:
            component_by_polygon[polygon_index] = component_index

    raw_count = len({indexed.find(index) for index in range(len(mesh.vertices))})
    return components, component_by_polygon, raw_count


def classify_component(component: dict[str, Any]) -> tuple[str, str]:
    index = component["componentIndex"]
    center_x, center_y, _center_z = component["bounds"]["center"]
    _span_x, span_y, _span_z = component["bounds"]["span"]

    if index == 0:
        return "ChassisCarrier", "explicit invariant: dominant C-shaped carrier"
    if center_x >= 0.14 and abs(center_y) <= 0.13 and span_y <= 0.20:
        return "GunBarrels", "centroid in +X bore corridor; narrow Y span"
    if center_y >= 0.29 and center_x >= -0.16:
        return (
            "ShieldTrim_North",
            "north detached trim envelope; full north armor remains on carrier",
        )
    if center_y <= -0.285 and center_x >= -0.16:
        return "ShieldLobe_South", "south armor lobe envelope"
    if -0.29 <= center_x <= 0.08 and abs(center_y) <= 0.22:
        return "CrownReactor", "central crown/reactor envelope"
    if center_x <= -0.30:
        if center_y >= 0.25:
            return "Pod_NorthOuter", "rear perimeter north-outer band"
        if center_y >= 0.0:
            return "Pod_NorthInner", "rear perimeter north-inner band"
        if center_y >= -0.25:
            return "Pod_SouthInner", "rear perimeter south-inner band"
        return "Pod_SouthOuter", "rear perimeter south-outer band"
    return "ChassisCarrier", "fallback: load-bearing carrier or connector"


def add_source_identity_attributes(mesh: bpy.types.Mesh) -> None:
    if mesh.attributes.get(SOURCE_POLYGON_ATTRIBUTE) is not None:
        mesh.attributes.remove(mesh.attributes[SOURCE_POLYGON_ATTRIBUTE])
    if mesh.attributes.get(SOURCE_VERTEX_ATTRIBUTE) is not None:
        mesh.attributes.remove(mesh.attributes[SOURCE_VERTEX_ATTRIBUTE])
    polygon_attribute = mesh.attributes.new(
        SOURCE_POLYGON_ATTRIBUTE,
        type="INT",
        domain="FACE",
    )
    vertex_attribute = mesh.attributes.new(
        SOURCE_VERTEX_ATTRIBUTE,
        type="INT",
        domain="POINT",
    )
    for polygon in mesh.polygons:
        polygon_attribute.data[polygon.index].value = polygon.index
    for vertex in mesh.vertices:
        vertex_attribute.data[vertex.index].value = vertex.index


def triangle_count(mesh: bpy.types.Mesh) -> int:
    return sum(len(polygon.vertices) - 2 for polygon in mesh.polygons)


def material_names(mesh: bpy.types.Mesh) -> list[str | None]:
    return [material.name if material is not None else None for material in mesh.materials]


def face_payload_records(mesh: bpy.types.Mesh) -> list[dict[str, Any]]:
    polygon_attribute = mesh.attributes.get(SOURCE_POLYGON_ATTRIBUTE)
    vertex_attribute = mesh.attributes.get(SOURCE_VERTEX_ATTRIBUTE)
    if polygon_attribute is None or vertex_attribute is None:
        raise ValueError("Source identity attributes were not preserved")
    uv_layers = list(mesh.uv_layers)
    records = []
    for polygon in mesh.polygons:
        source_polygon = polygon_attribute.data[polygon.index].value
        corners = []
        for loop_index in polygon.loop_indices:
            loop = mesh.loops[loop_index]
            source_vertex = vertex_attribute.data[loop.vertex_index].value
            uv = {
                layer.name: [
                    rounded(layer.data[loop_index].uv.x, 8),
                    rounded(layer.data[loop_index].uv.y, 8),
                ]
                for layer in uv_layers
            }
            corners.append({"sourceVertex": source_vertex, "uv": uv})
        corners.sort(key=lambda item: item["sourceVertex"])
        records.append(
            {
                "sourcePolygon": source_polygon,
                "sourceVertices": sorted(
                    vertex_attribute.data[index].value for index in polygon.vertices
                ),
                "materialIndex": polygon.material_index,
                "smooth": bool(polygon.use_smooth),
                "corners": corners,
            }
        )
    records.sort(key=lambda item: item["sourcePolygon"])
    return records


def source_vertex_ids(mesh: bpy.types.Mesh) -> list[int]:
    attribute = mesh.attributes.get(SOURCE_VERTEX_ATTRIBUTE)
    if attribute is None:
        raise ValueError("Source vertex identity attribute was not preserved")
    return sorted(attribute.data[vertex.index].value for vertex in mesh.vertices)


def delete_unwanted_vertices(
    obj: bpy.types.Object,
    keep_vertex_indices: set[int],
) -> None:
    mesh = obj.data
    editable = bmesh.new()
    editable.from_mesh(mesh)
    editable.verts.ensure_lookup_table()
    editable.verts.index_update()
    unwanted = [
        vertex
        for vertex in editable.verts
        if vertex.index not in keep_vertex_indices
    ]
    bmesh.ops.delete(editable, geom=unwanted, context="VERTS")
    editable.to_mesh(mesh)
    editable.free()
    obj.data.update()


def transform_mesh_to_pivot(
    mesh: bpy.types.Mesh,
    source_world_matrix: Matrix,
    pivot_location: Vector,
) -> None:
    mesh.transform(source_world_matrix)
    mesh.transform(Matrix.Translation(-pivot_location))
    mesh.update()


def pivot_for_group(group_name: str, group_points: list[Vector]) -> tuple[Vector, str]:
    minimum, maximum = bounds(group_points)
    center = (minimum + maximum) * 0.5
    if group_name == "ChassisCarrier":
        return Vector((0.0, 0.0, 0.0)), "carrier origin"
    if group_name.startswith("Shield"):
        return (
            Vector((minimum.x, center.y, center.z)),
            "rear/inner hinge edge",
        )
    if group_name == "GunBarrels":
        return Vector((minimum.x, center.y, center.z)), "barrel rear rail"
    if group_name == "CrownReactor":
        return center, "reactor center"
    return (
        Vector((center.x, center.y, minimum.z)),
        "pod footprint center at component minimum Z",
    )


def create_clip(
    name: str,
    object_keyframes: dict[str, list[tuple[int, dict[str, tuple[float, ...]]]]],
) -> None:
    for object_name, keyframes in object_keyframes.items():
        obj = bpy.data.objects[object_name]
        obj.animation_data_create()
        obj.animation_data.action = None
        for track in obj.animation_data.nla_tracks:
            track.mute = True
        action = bpy.data.actions.new(f"{name}__{object_name}")
        obj.animation_data.action = action
        for frame, values in keyframes:
            if "location" in values:
                obj.location = values["location"]
                obj.keyframe_insert(
                    data_path="location",
                    frame=frame,
                    group="RigidTransform",
                )
            if "rotation_euler" in values:
                obj.rotation_euler = values["rotation_euler"]
                obj.keyframe_insert(
                    data_path="rotation_euler",
                    frame=frame,
                    group="RigidTransform",
                )
        track = obj.animation_data.nla_tracks.new()
        track.name = name
        strip = track.strips.new(action.name, int(keyframes[0][0]), action)
        strip.name = name
        strip.action_frame_start = float(keyframes[0][0])
        strip.action_frame_end = float(keyframes[-1][0])
        obj.animation_data.action = None


def set_tracks(active_name: str | None) -> None:
    for obj in bpy.data.objects:
        if obj.animation_data is None:
            continue
        obj.animation_data.action = None
        for track in obj.animation_data.nla_tracks:
            track.mute = active_name is None or track.name != active_name


def material_images(
    materials: Iterable[bpy.types.Material | None],
) -> set[bpy.types.Image]:
    images: set[bpy.types.Image] = set()
    for material in materials:
        if material is None or material.node_tree is None:
            continue
        for node in material.node_tree.nodes:
            image = getattr(node, "image", None)
            if image is not None:
                images.add(image)
    return images


def nla_track_records(objects: Iterable[bpy.types.Object]) -> dict[str, list[dict[str, Any]]]:
    records = {}
    for obj in objects:
        if obj.animation_data is None:
            continue
        tracks = []
        for track in obj.animation_data.nla_tracks:
            tracks.append(
                {
                    "name": track.name,
                    "muted": bool(track.mute),
                    "strips": [
                        {
                            "name": strip.name,
                            "frameStart": rounded(strip.frame_start),
                            "frameEnd": rounded(strip.frame_end),
                            "actionFrameStart": rounded(strip.action_frame_start),
                            "actionFrameEnd": rounded(strip.action_frame_end),
                            "action": strip.action.name if strip.action else None,
                        }
                        for strip in track.strips
                    ],
                }
            )
        records[obj.name] = tracks
    return records


def has_exact_nla_strip(
    records: dict[str, list[dict[str, Any]]],
    object_name: str,
    track_name: str,
    frame_start: int,
    frame_end: int,
) -> bool:
    matching_tracks = [
        track
        for track in records.get(object_name, [])
        if track["name"] == track_name
    ]
    if len(matching_tracks) != 1:
        return False
    strips = matching_tracks[0]["strips"]
    return (
        len(strips) == 1
        and strips[0]["name"] == track_name
        and strips[0]["frameStart"] == float(frame_start)
        and strips[0]["frameEnd"] == float(frame_end)
        and strips[0]["actionFrameStart"] == float(frame_start)
        and strips[0]["actionFrameEnd"] == float(frame_end)
    )


BITMAP_FONT_5X7 = {
    " ": (
        "00000",
        "00000",
        "00000",
        "00000",
        "00000",
        "00000",
        "00000",
    ),
    "0": (
        "01110",
        "10001",
        "10011",
        "10101",
        "11001",
        "10001",
        "01110",
    ),
    "1": (
        "00100",
        "01100",
        "00100",
        "00100",
        "00100",
        "00100",
        "01110",
    ),
    "2": (
        "01110",
        "10001",
        "00001",
        "00010",
        "00100",
        "01000",
        "11111",
    ),
    "3": (
        "11110",
        "00001",
        "00001",
        "01110",
        "00001",
        "00001",
        "11110",
    ),
    "6": (
        "00110",
        "01000",
        "10000",
        "11110",
        "10001",
        "10001",
        "01110",
    ),
    "7": (
        "11111",
        "00001",
        "00010",
        "00100",
        "01000",
        "01000",
        "01000",
    ),
    "A": (
        "01110",
        "10001",
        "10001",
        "11111",
        "10001",
        "10001",
        "10001",
    ),
    "C": (
        "01111",
        "10000",
        "10000",
        "10000",
        "10000",
        "10000",
        "01111",
    ),
    "D": (
        "11110",
        "10001",
        "10001",
        "10001",
        "10001",
        "10001",
        "11110",
    ),
    "E": (
        "11111",
        "10000",
        "10000",
        "11110",
        "10000",
        "10000",
        "11111",
    ),
    "F": (
        "11111",
        "10000",
        "10000",
        "11110",
        "10000",
        "10000",
        "10000",
    ),
    "I": (
        "11111",
        "00100",
        "00100",
        "00100",
        "00100",
        "00100",
        "11111",
    ),
    "L": (
        "10000",
        "10000",
        "10000",
        "10000",
        "10000",
        "10000",
        "11111",
    ),
    "N": (
        "10001",
        "11001",
        "11001",
        "10101",
        "10011",
        "10011",
        "10001",
    ),
    "O": (
        "01110",
        "10001",
        "10001",
        "10001",
        "10001",
        "10001",
        "01110",
    ),
    "P": (
        "11110",
        "10001",
        "10001",
        "11110",
        "10000",
        "10000",
        "10000",
    ),
    "R": (
        "11110",
        "10001",
        "10001",
        "11110",
        "10100",
        "10010",
        "10001",
    ),
    "S": (
        "01111",
        "10000",
        "10000",
        "01110",
        "00001",
        "00001",
        "11110",
    ),
    "T": (
        "11111",
        "00100",
        "00100",
        "00100",
        "00100",
        "00100",
        "00100",
    ),
}


def set_rgba_pixel(
    pixels: list[float],
    width: int,
    height: int,
    x: int,
    y: int,
    color: tuple[float, float, float, float],
) -> None:
    if x < 0 or y < 0 or x >= width or y >= height:
        return
    offset = (y * width + x) * 4
    pixels[offset : offset + 4] = color


def draw_bitmap_label(
    pixels: list[float],
    *,
    width: int,
    height: int,
    text: str,
    panel_x: int,
    panel_width: int,
    top_margin: int,
    scale: int,
    color: tuple[float, float, float, float],
) -> None:
    glyph_width = 5 * scale
    spacing = scale
    text_width = len(text) * glyph_width + max(0, len(text) - 1) * spacing
    origin_x = panel_x + max(0, (panel_width - text_width) // 2)
    for character_index, character in enumerate(text):
        glyph = BITMAP_FONT_5X7.get(character)
        if glyph is None:
            raise ValueError(f"Contact-sheet font has no glyph for {character!r}")
        glyph_x = origin_x + character_index * (glyph_width + spacing)
        for row, bits in enumerate(glyph):
            for column, bit in enumerate(bits):
                if bit != "1":
                    continue
                pixel_x = glyph_x + column * scale
                # Labels are addressed from the image top; Blender pixels are
                # bottom-up.
                pixel_y_top = top_margin + row * scale
                pixel_y = height - 1 - pixel_y_top
                for offset_y in range(scale):
                    for offset_x in range(scale):
                        set_rgba_pixel(
                            pixels,
                            width,
                            height,
                            pixel_x + offset_x,
                            pixel_y - offset_y,
                            color,
                        )


def make_labeled_contact_sheet(
    *,
    frame_paths: list[Path],
    labels: list[str],
    output_path: Path,
    audit: Any,
) -> dict[str, Any]:
    if len(frame_paths) != len(labels) or not frame_paths:
        raise ValueError("Contact sheet requires one label per frame")
    loaded = [
        bpy.data.images.load(str(path), check_existing=False) for path in frame_paths
    ]
    try:
        frame_width, frame_height = loaded[0].size
        if any(tuple(image.size) != (frame_width, frame_height) for image in loaded):
            raise ValueError("Contact sheet frame dimensions differ")
        header_height = 40
        gutter = 6
        width = frame_width * len(loaded) + gutter * (len(loaded) - 1)
        height = frame_height + header_height
        background = audit.hex_linear_rgba("#0b1020")
        text_color = audit.hex_linear_rgba("#f4f7fb")
        gutter_color = audit.hex_linear_rgba("#202938")
        pixels = list(background) * (width * height)

        for frame_index, image in enumerate(loaded):
            panel_x = frame_index * (frame_width + gutter)
            source_pixels = list(image.pixels[:])
            for row in range(frame_height):
                source_start = row * frame_width * 4
                source_end = source_start + frame_width * 4
                target_start = (row * width + panel_x) * 4
                target_end = target_start + frame_width * 4
                pixels[target_start:target_end] = source_pixels[
                    source_start:source_end
                ]
            draw_bitmap_label(
                pixels,
                width=width,
                height=height,
                text=labels[frame_index],
                panel_x=panel_x,
                panel_width=frame_width,
                top_margin=8,
                scale=3,
                color=text_color,
            )
            if frame_index < len(loaded) - 1:
                gutter_start = panel_x + frame_width
                for x in range(gutter_start, gutter_start + gutter):
                    for y in range(height):
                        set_rgba_pixel(
                            pixels,
                            width,
                            height,
                            x,
                            y,
                            gutter_color,
                        )

        sheet = bpy.data.images.new(
            output_path.stem,
            width=width,
            height=height,
            alpha=True,
            float_buffer=False,
        )
        try:
            sheet.pixels.foreach_set(pixels)
            sheet.filepath_raw = str(output_path)
            sheet.file_format = "PNG"
            sheet.save()
        finally:
            bpy.data.images.remove(sheet)
    finally:
        for image in loaded:
            bpy.data.images.remove(image)

    audit.canonicalize_png(output_path)
    return {
        "file": output_path.name,
        "relativeFile": output_path.name,
        "bytes": output_path.stat().st_size,
        "sha256": sha256(output_path),
        "width": width,
        "height": height,
        "labels": labels,
        "format": "lossless labeled PNG frame strip",
    }


def location_snapshot(object_names: Iterable[str]) -> dict[str, list[float]]:
    return {
        object_name: vector_json(bpy.data.objects[object_name].location)
        for object_name in object_names
    }


def max_snapshot_delta(
    left: dict[str, list[float]],
    right: dict[str, list[float]],
) -> float:
    if set(left) != set(right):
        return math.inf
    return max(
        (
            abs(left[name][axis] - right[name][axis])
            for name in left
            for axis in range(3)
        ),
        default=0.0,
    )


def render_motion_evidence(
    *,
    clip_name: str,
    object_names: list[str],
    stages: list[tuple[int, str, str]],
    expected_start: dict[str, list[float]],
    expected_middle: dict[str, list[float]],
    output_root: Path,
    floor: bpy.types.Object,
    camera: bpy.types.Object,
    audit: Any,
) -> dict[str, Any]:
    clip_slug = re.sub(r"[^a-z0-9]+", "-", clip_name.lower()).strip("-")
    frame_dir = output_root / "motion" / clip_slug
    frame_dir.mkdir(parents=True)
    set_tracks(clip_name)
    frames = []
    for stage_index, (frame, stage_name, label) in enumerate(stages):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        frame_path = frame_dir / f"frame-{frame:04d}-{stage_name}.png"
        render_record = audit.render_view(
            frame_path,
            floor,
            camera,
            "gameplay58",
        )
        render_record["relativeFile"] = str(frame_path.relative_to(output_root))
        render_record["frame"] = frame
        render_record["stage"] = stage_name
        render_record["label"] = label
        render_record["evaluatedLocalLocations"] = location_snapshot(object_names)
        render_record["stageIndex"] = stage_index
        frames.append(render_record)

    contact_path = output_root / f"{clip_slug}-gameplay-58-contact-sheet.png"
    contact = make_labeled_contact_sheet(
        frame_paths=[
            output_root / record["relativeFile"] for record in frames
        ],
        labels=[record["label"] for record in frames],
        output_path=contact_path,
        audit=audit,
    )
    start_snapshot = frames[0]["evaluatedLocalLocations"]
    middle_snapshot = frames[1]["evaluatedLocalLocations"]
    end_snapshot = frames[-1]["evaluatedLocalLocations"]
    return {
        "clip": clip_name,
        "evaluationSource": (
            "actual authored NLA curves; only this same-named track was unmuted "
            "and Blender scene.frame_set evaluated each pinned frame"
        ),
        "camera": "fixed gameplay58",
        "frameRange": [stages[0][0], stages[-1][0]],
        "stages": [
            {"frame": frame, "stage": stage, "label": label}
            for frame, stage, label in stages
        ],
        "frames": frames,
        "contactSheet": contact,
        "startMatchesAuthoredRest": max_snapshot_delta(
            start_snapshot,
            expected_start,
        )
        <= 1e-9,
        "middleMatchesAuthoredTarget": max_snapshot_delta(
            middle_snapshot,
            expected_middle,
        )
        <= 1e-9,
        "returnsExactlyToStart": max_snapshot_delta(
            start_snapshot,
            end_snapshot,
        )
        <= 1e-9,
        "restFrameRenderHashesMatch": frames[0]["sha256"]
        == frames[-1]["sha256"],
        "returnToStartMaxAbsDelta": max_snapshot_delta(
            start_snapshot,
            end_snapshot,
        ),
        "formatChoice": (
            "Lossless labeled PNG frame strip plus individual canonical PNG "
            "frames; GIF omitted because the pinned Blender toolchain has no "
            "GIF encoder and lossless audit frames are deterministic."
        ),
    }


def output_markdown(validation: dict[str, Any]) -> str:
    check_lines = "\n".join(
        f"- {'PASS' if passed else 'FAIL'} — `{name}`"
        for name, passed in validation["checks"].items()
    )
    group_lines = "\n".join(
        f"- `{name}`: {record['componentCount']} components, "
        f"{record['faces']:,} faces / {record['triangles']:,} triangles"
        for name, record in validation["groups"].items()
    )
    limitation_lines = "\n".join(
        f"- {limitation}" for limitation in validation["limitations"]
    )
    motion_lines = "\n".join(
        f"- `{name}`: frames {record['frameRange'][0]}–"
        f"{record['frameRange'][1]}, "
        f"start-rest={'PASS' if record['startMatchesAuthoredRest'] else 'FAIL'}, "
        f"target={'PASS' if record['middleMatchesAuthoredTarget'] else 'FAIL'}, "
        f"return-to-rest={'PASS' if record['returnsExactlyToStart'] else 'FAIL'}, "
        f"rest-render-hash="
        f"{'PASS' if record['restFrameRenderHashesMatch'] else 'FAIL'}, "
        f"`{record['contactSheet']['relativeFile']}`"
        for name, record in validation["motionEvidence"].items()
    )
    return f"""# Aegis retextured semantic rig validation

Verdict: **{'PASS' if validation['passed'] else 'FAIL'}**

Source task: `{validation['source']['taskId']}`  
Source SHA-256: `{validation['source']['sha256']}`

## Checks

{check_lines}

## Semantic groups

{group_lines}

## Motion evidence

{motion_lines}

## Limitations

{limitation_lines}
"""


def main() -> int:
    args = parse_args(blender_arguments())
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise ValueError(
            f"Expected Blender {EXPECTED_BLENDER[0]}.{EXPECTED_BLENDER[1]}.x, "
            f"found {bpy.app.version_string}"
        )

    repo = repository_root()
    input_glb = resolve_existing_file(args.input_glb, repo, ".glb")
    source_record = resolve_existing_file(
        args.source_record or input_glb.with_name("request-record.json"),
        repo,
        ".json",
    )
    output_dir = resolve_output_directory(args.output_dir, repo)
    renders_dir = output_dir / "renders"
    provenance = validate_provenance(
        input_glb=input_glb,
        record_path=source_record,
        source_task_id=args.source_task_id,
        expected_input_task_id=args.expected_input_task_id,
        expected_sha256=args.expected_source_sha256,
    )
    source_glb = glb_summary(input_glb)
    output_dir.mkdir(parents=True)
    renders_dir.mkdir()

    scripts_dir = repo / "scripts/class-models"
    sys.path.insert(0, str(scripts_dir))
    import audit_smart_geometry_blender as audit

    clear_scene()
    imported = import_glb(input_glb)
    source_mesh_objects = [obj for obj in imported if obj.type == "MESH"]
    if len(source_mesh_objects) != 1:
        raise ValueError(
            f"Expected exactly one source mesh object, found {len(source_mesh_objects)}"
        )
    source_obj = source_mesh_objects[0]
    source_mesh = source_obj.data
    if len(source_mesh.uv_layers) < 1:
        raise ValueError("Source mesh has no UV layer")
    if len(source_mesh.materials) < 1:
        raise ValueError("Source mesh has no material")

    imported_set = set(imported)
    imported_roots = [obj for obj in imported if obj.parent not in imported_set]
    original_root_matrices = {obj.name: obj.matrix_world.copy() for obj in imported_roots}
    original_source_world = source_obj.matrix_world.copy()
    oriented_source_world = EXPECTED_ORIENTATION @ original_source_world
    oriented_points = [
        oriented_source_world @ vertex.co for vertex in source_mesh.vertices
    ]
    oriented_minimum, oriented_maximum = bounds(oriented_points)
    oriented_span = oriented_maximum - oriented_minimum
    planform_span = max(oriented_span.x, oriented_span.y)
    if planform_span <= 0.0:
        raise ValueError("Source has a degenerate planform")
    uniform_scale = args.target_planform_span / planform_span
    oriented_center = (oriented_minimum + oriented_maximum) * 0.5
    normalization_translation = Vector(
        (
            -oriented_center.x * uniform_scale,
            -oriented_center.y * uniform_scale,
            -oriented_minimum.z * uniform_scale,
        )
    )
    normalization = (
        Matrix.Translation(normalization_translation)
        @ Matrix.Scale(uniform_scale, 4)
    )
    for root in imported_roots:
        root.matrix_world = (
            normalization @ EXPECTED_ORIENTATION @ original_root_matrices[root.name]
        )
    bpy.context.view_layer.update()
    normalized_points = [
        source_obj.matrix_world @ vertex.co for vertex in source_mesh.vertices
    ]
    normalized_minimum, normalized_maximum = bounds(normalized_points)
    normalized_span = normalized_maximum - normalized_minimum

    # Component classification intentionally uses the exact proven,
    # orientation-corrected provider scale. Normalization is presentation-only.
    components, component_by_polygon, raw_component_count = welded_components(
        source_mesh,
        oriented_points,
    )
    if len(components) != EXPECTED_WELDED_COMPONENTS:
        raise ValueError(
            f"Expected {EXPECTED_WELDED_COMPONENTS} welded components, "
            f"found {len(components)}"
        )
    source_triangles = triangle_count(source_mesh)

    group_components: dict[str, list[int]] = {name: [] for name in GROUP_ORDER}
    classification_records = {}
    for component in components:
        group_name, reason = classify_component(component)
        group_components[group_name].append(component["componentIndex"])
        classification_records[str(component["componentIndex"])] = {
            "group": group_name,
            "rule": reason,
        }
    if any(not values for values in group_components.values()):
        raise ValueError(
            "Semantic classification left a required group empty: "
            + repr({name: len(values) for name, values in group_components.items()})
        )

    add_source_identity_attributes(source_mesh)
    source_face_records = face_payload_records(source_mesh)
    source_face_payload_hash = canonical_json_hash(source_face_records)
    source_vertex_identity = source_vertex_ids(source_mesh)
    source_material_names = material_names(source_mesh)
    source_uv_layers = [layer.name for layer in source_mesh.uv_layers]
    source_images = material_images(source_mesh.materials)

    component_lookup = {
        component["componentIndex"]: component for component in components
    }
    model_collection = make_collection("Aegis_Retextured_SemanticRig")
    model_root = add_empty(
        "BulwarkMobile_Root",
        model_collection,
        display="CIRCLE",
        size=0.08,
    )
    model_root["nilbots_schema"] = SCHEMA
    model_root["source_task_id"] = args.source_task_id
    model_root["source_input_task_id"] = args.expected_input_task_id
    model_root["source_sha256"] = args.expected_source_sha256
    model_root["facing_axis_blender"] = "+X"
    model_root["up_axis_blender"] = "+Z"
    model_root["orientation_correction"] = "Rz(180) @ Rx(-90)"
    model_root["uniform_normalization_scale"] = uniform_scale
    model_root["target_planform_span"] = args.target_planform_span
    model_root["source_faces_preserved"] = len(source_mesh.polygons)
    model_root["source_triangles_preserved"] = source_triangles
    model_root["semantic_group_count"] = len(GROUP_ORDER)
    model_root["semantic_clips"] = ["PodPlant_HoverSettle", "GunRecoil"]

    hover_root = add_empty(
        "HoverSettle_Pivot",
        model_collection,
        parent=model_root,
        location=(0.0, 0.0, args.hover_height),
        display="ARROWS",
        size=0.06,
    )
    hover_root["semantic_role"] = "hover-settle"
    hover_root["rest_height"] = args.hover_height
    hover_root["planted_height"] = args.planted_hover_height

    group_pivots: dict[str, bpy.types.Object] = {}
    group_mesh_objects: dict[str, bpy.types.Object] = {}
    group_records: dict[str, dict[str, Any]] = {}
    for group_name in GROUP_ORDER:
        component_indices = group_components[group_name]
        group_vertex_indices = {
            vertex_index
            for component_index in component_indices
            for vertex_index in component_lookup[component_index]["vertexIndices"]
        }
        group_points = [
            normalized_points[vertex_index]
            for vertex_index in sorted(group_vertex_indices)
        ]
        pivot_location, pivot_rationale = pivot_for_group(group_name, group_points)
        pivot = add_empty(
            f"{group_name}_Pivot",
            model_collection,
            parent=hover_root,
            location=pivot_location,
            display="ARROWS",
            size=0.035,
        )
        pivot["semantic_group"] = group_name
        pivot["source_component_ids"] = component_indices
        pivot["pivot_rationale"] = pivot_rationale

        group_mesh = source_mesh.copy()
        group_mesh.name = f"{group_name}_Mesh"
        group_obj = bpy.data.objects.new(f"{group_name}_Geometry", group_mesh)
        model_collection.objects.link(group_obj)
        delete_unwanted_vertices(group_obj, group_vertex_indices)
        transform_mesh_to_pivot(
            group_mesh,
            source_obj.matrix_world,
            pivot_location,
        )
        group_obj.parent = pivot
        group_obj.location = (0.0, 0.0, 0.0)
        group_obj.rotation_euler = (0.0, 0.0, 0.0)
        group_obj.scale = (1.0, 1.0, 1.0)
        group_obj["semantic_group"] = group_name
        group_obj["source_component_ids"] = component_indices
        group_obj["source_face_count"] = len(group_mesh.polygons)
        group_obj["source_triangle_count"] = triangle_count(group_mesh)
        group_obj["source_uv_layers"] = [layer.name for layer in group_mesh.uv_layers]
        group_obj["provider_material_preserved"] = True

        group_pivots[group_name] = pivot
        group_mesh_objects[group_name] = group_obj
        group_records[group_name] = {
            "componentCount": len(component_indices),
            "componentIndices": component_indices,
            "vertices": len(group_mesh.vertices),
            "faces": len(group_mesh.polygons),
            "triangles": triangle_count(group_mesh),
            "loops": len(group_mesh.loops),
            "boundsNormalizedFloorAligned": bounds_record(group_points),
            "pivotNormalizedFloorAligned": vector_json(pivot_location),
            "pivotRationale": pivot_rationale,
            "materials": material_names(group_mesh),
            "uvLayers": [layer.name for layer in group_mesh.uv_layers],
        }

    # Source objects are no longer needed. Each source vertex and face now
    # appears in exactly one semantic mesh; materials/images remain referenced.
    for obj in imported:
        bpy.data.objects.remove(obj, do_unlink=True)

    rest_hover = tuple(hover_root.location)
    planted_hover = (0.0, 0.0, args.planted_hover_height)
    plant_keyframes: dict[str, list[tuple[int, dict[str, tuple[float, ...]]]]] = {
        hover_root.name: [
            (1, {"location": rest_hover}),
            (22, {"location": planted_hover}),
            (38, {"location": planted_hover}),
            (60, {"location": rest_hover}),
        ]
    }
    for group_name in POD_GROUPS:
        pivot = group_pivots[group_name]
        rest = tuple(pivot.location)
        planted = (rest[0], rest[1], -args.planted_hover_height)
        plant_keyframes[pivot.name] = [
            (1, {"location": rest}),
            (22, {"location": planted}),
            (38, {"location": planted}),
            (60, {"location": rest}),
        ]
    create_clip("PodPlant_HoverSettle", plant_keyframes)
    pod_expected_start = {
        object_name: vector_json(Vector(keyframes[0][1]["location"]))
        for object_name, keyframes in plant_keyframes.items()
    }
    pod_expected_planted = {
        object_name: vector_json(Vector(keyframes[1][1]["location"]))
        for object_name, keyframes in plant_keyframes.items()
    }

    gun_pivot = group_pivots["GunBarrels"]
    gun_rest = tuple(gun_pivot.location)
    gun_recoil = (
        gun_rest[0] - args.recoil_distance,
        gun_rest[1],
        gun_rest[2],
    )
    create_clip(
        "GunRecoil",
        {
            gun_pivot.name: [
                (1, {"location": gun_rest}),
                (4, {"location": gun_rest}),
                (7, {"location": gun_recoil}),
                (16, {"location": gun_rest}),
            ]
        },
    )
    gun_expected_start = {gun_pivot.name: vector_json(Vector(gun_rest))}
    gun_expected_recoil = {gun_pivot.name: vector_json(Vector(gun_recoil))}
    set_tracks(None)
    hover_root.location = rest_hover
    gun_pivot.location = gun_rest
    for group_name in POD_GROUPS:
        group_pivots[group_name].location = tuple(
            group_records[group_name]["pivotNormalizedFloorAligned"]
        )
    bpy.context.view_layer.update()

    floor, camera = audit.configure_neutral_rig(list(model_collection.all_objects))
    render_records = {}
    for filename, view in (
        ("fixed-top.png", "top"),
        ("gameplay-58.png", "gameplay58"),
        ("fixed-side.png", "side"),
    ):
        render_records[view] = audit.render_view(
            renders_dir / filename,
            floor,
            camera,
            view,
        )

    motion_evidence = {
        "PodPlant_HoverSettle": render_motion_evidence(
            clip_name="PodPlant_HoverSettle",
            object_names=[hover_root.name]
            + [group_pivots[name].name for name in POD_GROUPS],
            stages=[
                (1, "rest", "REST F01"),
                (22, "planted", "PLANTED F22"),
                (60, "rest-return", "REST F60"),
            ],
            expected_start=pod_expected_start,
            expected_middle=pod_expected_planted,
            output_root=output_dir,
            floor=floor,
            camera=camera,
            audit=audit,
        ),
        "GunRecoil": render_motion_evidence(
            clip_name="GunRecoil",
            object_names=[gun_pivot.name],
            stages=[
                (1, "rest", "REST F01"),
                (7, "recoil", "RECOIL F07"),
                (16, "rest-return", "REST F16"),
            ],
            expected_start=gun_expected_start,
            expected_middle=gun_expected_recoil,
            output_root=output_dir,
            floor=floor,
            camera=camera,
            audit=audit,
        ),
    }
    set_tracks(None)
    bpy.context.scene.frame_set(1)
    hover_root.location = rest_hover
    gun_pivot.location = gun_rest
    for group_name in POD_GROUPS:
        group_pivots[group_name].location = tuple(
            group_records[group_name]["pivotNormalizedFloorAligned"]
        )
    bpy.context.view_layer.update()

    glb_path = output_dir / "aegis-retextured-semantic-rig.glb"
    blend_path = output_dir / "aegis-retextured-semantic-rig.blend"
    for obj in bpy.data.objects:
        if obj.animation_data is not None:
            for track in obj.animation_data.nla_tracks:
                track.mute = False
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=False,
        collection=model_collection.name,
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
    set_tracks(None)
    hover_root.location = rest_hover
    gun_pivot.location = gun_rest
    for group_name in POD_GROUPS:
        group_pivots[group_name].location = tuple(
            group_records[group_name]["pivotNormalizedFloorAligned"]
        )
    bpy.context.view_layer.update()

    bpy.ops.file.pack_all()
    preview_text = bpy.data.texts.new("AEGIS_SEMANTIC_RIG_README")
    preview_text.write(
        "Source hash/task are pinned on BulwarkMobile_Root.\\n"
        "Geometry uses exact Rz(180) @ Rx(-90), one uniform normalization, "
        "and floor alignment.\\n"
        "All provider faces, UVs, source material assignments, and PBR images "
        "are retained across nine semantic rigid groups.\\n"
        "Unmute one same-named NLA track across animated pivots to preview "
        "PodPlant_HoverSettle or GunRecoil.\\n"
    )
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    group_face_records = [
        record
        for group_name in GROUP_ORDER
        for record in face_payload_records(group_mesh_objects[group_name].data)
    ]
    group_face_records.sort(key=lambda item: item["sourcePolygon"])
    group_vertex_identity = [
        source_id
        for group_name in GROUP_ORDER
        for source_id in source_vertex_ids(group_mesh_objects[group_name].data)
    ]
    group_polygon_ids = [record["sourcePolygon"] for record in group_face_records]
    regrouped_face_payload_hash = canonical_json_hash(group_face_records)
    regrouped_triangles = sum(
        triangle_count(obj.data) for obj in group_mesh_objects.values()
    )
    regrouped_faces = sum(
        len(obj.data.polygons) for obj in group_mesh_objects.values()
    )
    regrouped_loops = sum(
        len(obj.data.loops) for obj in group_mesh_objects.values()
    )
    regrouped_vertices = sum(
        len(obj.data.vertices) for obj in group_mesh_objects.values()
    )
    output_glb = glb_summary(glb_path)
    blend_nla = nla_track_records(model_collection.all_objects)
    output_animation_channels = {
        animation["name"]: animation["channels"]
        for animation in output_glb["animations"]
    }

    source_image_hashes = Counter(
        record["sha256"]
        for record in source_glb["images"]
        if record["sha256"] is not None
    )
    output_image_hashes = Counter(
        record["sha256"]
        for record in output_glb["images"]
        if record["sha256"] is not None
    )
    source_texture_roles = [
        material["textureRoles"] for material in source_glb["materials"]
    ]
    output_texture_roles = [
        material["textureRoles"] for material in output_glb["materials"]
    ]
    packed_source_images = {
        image.name: image.packed_file is not None for image in source_images
    }
    assigned_components = [
        component
        for group_name in GROUP_ORDER
        for component in group_components[group_name]
    ]
    checks = {
        "sourceHashMatchesExpected": sha256(input_glb)
        == args.expected_source_sha256,
        "sourceTaskMatchesArchivedRecord": provenance["taskId"]
        == args.source_task_id,
        "inputGeometryTaskMatchesArchivedRecord": provenance["inputTaskId"]
        == args.expected_input_task_id,
        "exactRigidCorrectionRz180AtRxMinus90": matrix_json(
            EXPECTED_ORIENTATION
        )
        == [
            [-1.0, 0.0, 0.0, 0.0],
            [0.0, 0.0, -1.0, 0.0],
            [0.0, -1.0, 0.0, 0.0],
            [0.0, 0.0, 0.0, 1.0],
        ],
        "orientationDeterminantOne": abs(
            EXPECTED_ORIENTATION.to_3x3().determinant() - 1.0
        )
        < 1e-9,
        "oneUniformNormalization": abs(
            normalized_span.x / oriented_span.x - uniform_scale
        )
        < 1e-6
        and abs(normalized_span.y / oriented_span.y - uniform_scale) < 1e-6
        and abs(normalized_span.z / oriented_span.z - uniform_scale) < 1e-6,
        "normalizedPlanformSpanMatchesTarget": abs(
            max(normalized_span.x, normalized_span.y)
            - args.target_planform_span
        )
        < 1e-6,
        "normalizedPlanformCentered": abs(
            (normalized_minimum.x + normalized_maximum.x) * 0.5
        )
        < 1e-6
        and abs((normalized_minimum.y + normalized_maximum.y) * 0.5) < 1e-6,
        "normalizedFloorAtZero": abs(normalized_minimum.z) < 1e-6,
        "exactly122WeldedComponents": len(components)
        == EXPECTED_WELDED_COMPONENTS,
        "rawIndexedComponentsDoNotUndercutWeldedTopology": raw_component_count
        >= EXPECTED_WELDED_COMPONENTS,
        "allComponentsAssignedExactlyOnce": sorted(assigned_components)
        == list(range(EXPECTED_WELDED_COMPONENTS))
        and len(assigned_components) == len(set(assigned_components)),
        "allNineSemanticGroupsPresent": list(group_records) == list(GROUP_ORDER)
        and all(record["componentCount"] > 0 for record in group_records.values()),
        "allSourceFacesPreservedExactlyOnce": regrouped_faces
        == len(source_mesh.polygons)
        and sorted(group_polygon_ids) == list(range(len(source_mesh.polygons)))
        and len(group_polygon_ids) == len(set(group_polygon_ids)),
        "allBlenderImportedTrianglesPreserved": source_triangles
        == regrouped_triangles
        == output_glb["triangles"],
        "allSourceVerticesPartitionedExactlyOnce": regrouped_vertices
        == len(source_vertex_identity)
        and sorted(group_vertex_identity) == source_vertex_identity
        and len(group_vertex_identity) == len(set(group_vertex_identity)),
        "allSourceLoopsPreserved": regrouped_loops == len(source_mesh.loops),
        "uvAndFaceMaterialPayloadPreserved": source_face_payload_hash
        == regrouped_face_payload_hash,
        "uvLayerNamesPreserved": all(
            record["uvLayers"] == source_uv_layers
            for record in group_records.values()
        ),
        "providerMaterialSlotsPreserved": all(
            record["materials"] == source_material_names
            for record in group_records.values()
        ),
        "sourcePbrTextureRolesPreserved": output_texture_roles
        == source_texture_roles,
        "sourceTexturePayloadsPreservedInGlb": output_image_hashes
        == source_image_hashes,
        "sourceImagesPackedInEditableBlend": bool(packed_source_images)
        and all(packed_source_images.values()),
        "semanticGlbHasNineMeshes": output_glb["meshes"] == len(GROUP_ORDER),
        "blendNlaStripsHaveExpectedOwnersAndRanges": has_exact_nla_strip(
            blend_nla,
            hover_root.name,
            "PodPlant_HoverSettle",
            1,
            60,
        )
        and all(
            has_exact_nla_strip(
                blend_nla,
                group_pivots[name].name,
                "PodPlant_HoverSettle",
                1,
                60,
            )
            for name in POD_GROUPS
        )
        and has_exact_nla_strip(
            blend_nla,
            gun_pivot.name,
            "GunRecoil",
            1,
            16,
        ),
        "bothNamedNlaClipsInGlb": set(output_glb["animationNames"])
        == {
            "PodPlant_HoverSettle",
            "GunRecoil",
        }
        and output_animation_channels
        == {"PodPlant_HoverSettle": 5, "GunRecoil": 1},
        "motionEvidenceUsesActualAuthoredNlaCurves": all(
            record["evaluationSource"].startswith("actual authored NLA curves")
            for record in motion_evidence.values()
        )
        and motion_evidence["PodPlant_HoverSettle"]["frameRange"] == [1, 60]
        and motion_evidence["GunRecoil"]["frameRange"] == [1, 16],
        "motionEvidenceMatchesAuthoredRestAndTargets": all(
            record["startMatchesAuthoredRest"]
            and record["middleMatchesAuthoredTarget"]
            for record in motion_evidence.values()
        ),
        "motionEvidenceReturnsExactlyToRest": all(
            record["returnsExactlyToStart"]
            and record["restFrameRenderHashesMatch"]
            and record["returnToStartMaxAbsDelta"] <= 1e-9
            for record in motion_evidence.values()
        ),
        "motionContactSheetsAndFramesWritten": all(
            (output_dir / record["contactSheet"]["relativeFile"]).is_file()
            and all(
                (output_dir / frame["relativeFile"]).is_file()
                for frame in record["frames"]
            )
            for record in motion_evidence.values()
        ),
        "rootProvenanceExtrasExported": output_glb["nodeExtras"]
        .get(model_root.name, {})
        .get("source_task_id")
        == args.source_task_id
        and output_glb["nodeExtras"]
        .get(model_root.name, {})
        .get("source_input_task_id")
        == args.expected_input_task_id
        and output_glb["nodeExtras"].get(model_root.name, {}).get("source_sha256")
        == args.expected_source_sha256,
        "semanticExtrasExported": all(
            f"{name}_Pivot" in output_glb["nodeExtras"]
            and output_glb["nodeExtras"][f"{name}_Pivot"].get("semantic_group")
            == name
            and f"{name}_Geometry" in output_glb["nodeExtras"]
            and output_glb["nodeExtras"][f"{name}_Geometry"].get(
                "semantic_group"
            )
            == name
            for name in GROUP_ORDER
        ),
        "fixedTopGameplaySideRendersWritten": set(render_records)
        == {"top", "gameplay58", "side"}
        and all((renders_dir / record["file"]).is_file() for record in render_records.values()),
        "blendAndGlbWritten": blend_path.is_file() and glb_path.is_file(),
    }

    limitations = [
        (
            "The nine groups reproduce the proven deterministic centroid/bounds "
            "classification. Several tiny trim islands near group envelopes "
            "remain semantic judgments rather than provider-authored labels."
        ),
        (
            f"The archived retexture has {raw_component_count} indexed islands "
            f"rather than the untextured proof's "
            f"{PROVEN_UNTEXTURED_RAW_INDEXED_COMPONENTS}; UV/material seam "
            "re-indexing is expected. Coincident-position welding still "
            f"reproduces exactly {EXPECTED_WELDED_COMPONENTS} source components."
        ),
        (
            f"The archived retexture contains {source_glb['triangles']} "
            "indexed triangles, compared with the earlier untextured proof's "
            f"{PROVEN_UNTEXTURED_SOURCE_TRIANGLES}. Blender imports "
            f"{source_triangles} of the retexture triangles and the semantic "
            "export retains every one of those imported faces/triangles."
        ),
        (
            "This is a textured Mobile-base proof. The dominant carrier still "
            "owns the visible north armor surface, so Shell and Tower remain "
            "separate future add-on kits rather than body-lobe morphs."
        ),
        (
            "PodPlant_HoverSettle proves four independent rigid pod clusters "
            "and reversible ground contact; it does not add telescoping piston "
            "geometry or authorize autonomous playback."
        ),
        (
            "GunRecoil is a rigid rail translation into the existing carrier "
            "socket and still needs a final close-range clearance review."
        ),
        (
            "No semantic team-accent material is authored in this stage. The "
            "provider PBR material and all source texture payloads are retained "
            "without tint ownership changes."
        ),
    ]
    validation = {
        "schema": SCHEMA,
        "toolVersion": TOOL_VERSION,
        "passed": all(checks.values()),
        "blender": {
            "version": bpy.app.version_string,
            "expectedMajorMinor": list(EXPECTED_BLENDER),
            "background": bpy.app.background,
        },
        "source": {
            **provenance,
            "path": str(input_glb.relative_to(repo)),
            "recordPath": str(source_record.relative_to(repo)),
            "bytes": input_glb.stat().st_size,
            "sha256": args.expected_source_sha256,
            "vertices": len(source_mesh.vertices),
            "faces": len(source_mesh.polygons),
            "triangles": source_triangles,
            "sourceGlbIndexedTriangles": source_glb["triangles"],
            "blenderImportTriangleDelta": source_glb["triangles"]
            - source_triangles,
            "loops": len(source_mesh.loops),
            "uvLayers": source_uv_layers,
            "materials": source_material_names,
            "glb": source_glb,
        },
        "transform": {
            "orientationOperation": "M = Rz(180) @ Rx(-90)",
            "orientationMatrix": matrix_json(EXPECTED_ORIENTATION),
            "orientationDeterminant3x3": rounded(
                EXPECTED_ORIENTATION.to_3x3().determinant()
            ),
            "normalizationOperation": (
                "one uniform max-planform-span scale, XY centering, and Z-floor "
                "translation after rigid orientation"
            ),
            "normalizationMatrix": matrix_json(normalization),
            "uniformScale": rounded(uniform_scale),
            "translationAfterScale": vector_json(normalization_translation),
            "targetPlanformSpan": args.target_planform_span,
            "orientedBounds": bounds_record(oriented_points),
            "normalizedFloorAlignedBounds": bounds_record(normalized_points),
            "deformation": False,
        },
        "classification": {
            "method": "priority-ordered deterministic welded-component bounds rules",
            "rawIndexedComponents": raw_component_count,
            "weldedComponents": len(components),
            "componentAssignments": classification_records,
            "prunedComponents": [],
            "prunedFaces": 0,
            "prunedTriangles": 0,
        },
        "groups": group_records,
        "motionEvidence": motion_evidence,
        "preservation": {
            "sourceFacePayloadSha256": source_face_payload_hash,
            "regroupedFacePayloadSha256": regrouped_face_payload_hash,
            "regroupedVertices": regrouped_vertices,
            "regroupedFaces": regrouped_faces,
            "regroupedTriangles": regrouped_triangles,
            "regroupedLoops": regrouped_loops,
            "packedSourceImages": packed_source_images,
            "sourceTexturePayloadHashes": dict(source_image_hashes),
            "outputTexturePayloadHashes": dict(output_image_hashes),
            "sourcePbrTextureRoles": source_texture_roles,
            "outputPbrTextureRoles": output_texture_roles,
        },
        "clips": {
            "blendNla": blend_nla,
            "PodPlant_HoverSettle": {
                "frames": [1, 60],
                "fps": 30,
                "movingNodes": [hover_root.name]
                + [group_pivots[name].name for name in POD_GROUPS],
                "rendererOwnership": "must be sampled from replay transition truth",
            },
            "GunRecoil": {
                "frames": [1, 16],
                "fps": 30,
                "movingNodes": [gun_pivot.name],
                "translationBlender": [-args.recoil_distance, 0.0, 0.0],
                "rendererOwnership": "must be sampled from an authoritative shot",
            },
        },
        "outputs": {
            "blend": {
                "file": blend_path.name,
                "bytes": blend_path.stat().st_size,
                "sha256": sha256(blend_path),
            },
            "glb": {
                "file": glb_path.name,
                **output_glb,
            },
            "renders": render_records,
        },
        "checks": checks,
        "limitations": limitations,
    }
    validation_path = output_dir / "validation.json"
    validation_md_path = output_dir / "validation.md"
    validation_path.write_text(
        json.dumps(validation, indent=2) + "\n",
        encoding="utf-8",
    )
    validation_md_path.write_text(output_markdown(validation), encoding="utf-8")

    print(
        json.dumps(
            {
                "passed": validation["passed"],
                "outputDirectory": str(output_dir),
                "sourceTaskId": args.source_task_id,
                "sourceSha256": args.expected_source_sha256,
                "groups": {
                    name: {
                        "components": record["componentCount"],
                        "faces": record["faces"],
                        "triangles": record["triangles"],
                    }
                    for name, record in group_records.items()
                },
                "animations": output_glb["animationNames"],
                "checks": checks,
            },
            indent=2,
        )
    )
    return 0 if validation["passed"] else 1


if __name__ == "__main__":
    try:
        result = main()
    except SystemExit as error:
        result = int(error.code or 0)
    except Exception as error:
        print(f"ERROR: {error}", file=sys.stderr)
        traceback.print_exc()
        sys.stdout.flush()
        sys.stderr.flush()
        os._exit(1)
    if result:
        sys.stdout.flush()
        sys.stderr.flush()
        os._exit(result)
