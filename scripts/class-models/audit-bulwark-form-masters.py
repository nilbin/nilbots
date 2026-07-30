#!/usr/bin/env python3
"""Inspect and render the raw Meshy Bulwark form masters in Blender 5.2.

Run through the pinned portable Blender, not the host Python. The probe phase
tests the two source-up conventions observed in the Nilbots Meshy trials and
both longitudinal facings. The render phase requires the reviewed source axes.
Neither phase modifies or exports the durable provider master.
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
RENDER_SIZE = 1024
BACKGROUND_RGB = (0.184, 0.184, 0.184, 1.0)


@dataclass(frozen=True)
class FormMaster:
    asset_id: str
    task_id: str
    label: str
    runtime_scale: float
    directional: bool


MASTERS = (
    FormMaster(
        "aegis-tortoise-shell",
        "019fb22d-c313-76f8-8eca-91ccbb46afd0",
        "Aegis Tortoise Shell",
        1.20,
        True,
    ),
    FormMaster(
        "aegis-tortoise-turret",
        "019fb232-6e09-787f-85cc-dbaea29fb9b6",
        "Aegis Tortoise whole Turret",
        1.08,
        False,
    ),
)


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--artifacts-root", type=Path, required=True)
    parser.add_argument("--phase", choices=("probe", "render"), required=True)
    parser.add_argument(
        "--source-facing",
        action="append",
        default=[],
        metavar="ASSET=+x|-x",
    )
    parser.add_argument(
        "--source-up",
        action="append",
        default=[],
        metavar="ASSET=+y|+z",
    )
    return parser.parse_args(argv)


def keyed_values(
    entries: Iterable[str],
    *,
    allowed: set[str],
    option: str,
) -> dict[str, str]:
    result: dict[str, str] = {}
    for entry in entries:
        if "=" not in entry:
            raise ValueError(f"Invalid {option} value: {entry}")
        asset_id, value = entry.split("=", 1)
        if value not in allowed:
            raise ValueError(f"Invalid {option} value for {asset_id}: {value}")
        result[asset_id] = value
    return result


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def png_dimensions(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"Not a PNG: {path}")
    return struct.unpack(">II", header[16:24])


def glb_json(path: Path) -> dict[str, Any]:
    with path.open("rb") as stream:
        header = stream.read(12)
        magic, version, _length = struct.unpack("<4sII", header)
        if magic != b"glTF" or version != 2:
            raise ValueError(f"Unsupported GLB header: {path}")
        chunk_length, chunk_type = struct.unpack("<II", stream.read(8))
        if chunk_type != 0x4E4F534A:
            raise ValueError(f"First GLB chunk is not JSON: {path}")
        payload = stream.read(chunk_length)
    return json.loads(payload.rstrip(b" \t\r\n\x00"))


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for datablock in list(collection):
            if datablock.users == 0:
                collection.remove(datablock)


def import_master(path: Path) -> list[bpy.types.Object]:
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    if not meshes:
        raise ValueError(f"No mesh imported from {path}")
    return meshes


def mesh_triangles(obj: bpy.types.Object) -> np.ndarray:
    triangles: list[tuple[int, int, int]] = []
    for polygon in obj.data.polygons:
        vertices = tuple(polygon.vertices)
        if len(vertices) == 3:
            triangles.append(vertices)
        else:
            triangles.extend(
                (vertices[0], vertices[index], vertices[index + 1])
                for index in range(1, len(vertices) - 1)
            )
    return np.asarray(triangles, dtype=np.int64)


def mesh_positions(obj: bpy.types.Object) -> np.ndarray:
    matrix = np.asarray(obj.matrix_world, dtype=np.float64)
    local = np.asarray(
        [
            (vertex.co.x, vertex.co.y, vertex.co.z, 1.0)
            for vertex in obj.data.vertices
        ],
        dtype=np.float64,
    )
    return (local @ matrix.T)[:, :3]


def component_roots(
    vertex_count: int,
    triangles: np.ndarray,
) -> tuple[np.ndarray, np.ndarray]:
    if vertex_count == 0:
        return np.empty(0, dtype=np.int64), np.empty(0, dtype=np.int64)
    parent = np.arange(vertex_count, dtype=np.int64)
    rank = np.zeros(vertex_count, dtype=np.uint8)

    def find(value: int) -> int:
        root = value
        while parent[root] != root:
            root = int(parent[root])
        while parent[value] != value:
            next_value = int(parent[value])
            parent[value] = root
            value = next_value
        return root

    def union(left: int, right: int) -> None:
        left_root = find(left)
        right_root = find(right)
        if left_root == right_root:
            return
        if rank[left_root] < rank[right_root]:
            left_root, right_root = right_root, left_root
        parent[right_root] = left_root
        if rank[left_root] == rank[right_root]:
            rank[left_root] += 1

    used = np.unique(triangles.reshape(-1))
    for triangle in triangles:
        union(int(triangle[0]), int(triangle[1]))
        union(int(triangle[0]), int(triangle[2]))
    roots = np.full(vertex_count, -1, dtype=np.int64)
    for value in used:
        roots[int(value)] = find(int(value))
    return roots, used


def component_count(vertex_count: int, triangles: np.ndarray) -> int:
    roots, used = component_roots(vertex_count, triangles)
    return len({int(roots[int(value)]) for value in used})


def welded_topology(
    meshes: list[bpy.types.Object],
) -> tuple[dict[str, Any], dict[str, Any]]:
    indexed_components = 0
    positions_parts: list[np.ndarray] = []
    triangle_parts: list[np.ndarray] = []
    vertex_offsets: list[int] = []
    vertex_offset = 0
    triangle_count = 0

    for obj in meshes:
        positions = mesh_positions(obj)
        triangles = mesh_triangles(obj)
        indexed_components += component_count(len(positions), triangles)
        positions_parts.append(positions)
        triangle_parts.append(triangles + vertex_offset)
        vertex_offsets.append(vertex_offset)
        vertex_offset += len(positions)
        triangle_count += len(triangles)

    positions = np.concatenate(positions_parts, axis=0)
    triangles = np.concatenate(triangle_parts, axis=0)
    unique_positions, inverse = np.unique(
        positions,
        axis=0,
        return_inverse=True,
    )
    welded_triangles = inverse[triangles]
    nondegenerate = welded_triangles[
        (welded_triangles[:, 0] != welded_triangles[:, 1])
        & (welded_triangles[:, 1] != welded_triangles[:, 2])
        & (welded_triangles[:, 2] != welded_triangles[:, 0])
    ]
    welded_roots, welded_used = component_roots(
        len(unique_positions),
        nondegenerate,
    )
    component_ids = sorted(
        {int(welded_roots[int(value)]) for value in welded_used}
    )
    root_to_component = {
        root: component_index
        for component_index, root in enumerate(component_ids)
    }
    edges = np.concatenate(
        (
            nondegenerate[:, [0, 1]],
            nondegenerate[:, [1, 2]],
            nondegenerate[:, [2, 0]],
        ),
        axis=0,
    )
    edges.sort(axis=1)
    unique_edges, counts = np.unique(edges, axis=0, return_counts=True)

    global_span = np.ptp(unique_positions, axis=0)
    triangle_roots = welded_roots[nondegenerate[:, 0]]
    component_details = []
    for component_index, root in enumerate(component_ids):
        member_indices = np.flatnonzero(welded_roots == root)
        member_positions = unique_positions[member_indices]
        member_triangles = nondegenerate[triangle_roots == root]
        minimum = np.min(member_positions, axis=0)
        maximum = np.max(member_positions, axis=0)
        span = maximum - minimum
        triangle_positions = unique_positions[member_triangles]
        cross = np.cross(
            triangle_positions[:, 1] - triangle_positions[:, 0],
            triangle_positions[:, 2] - triangle_positions[:, 0],
        )
        component_edges = np.concatenate(
            (
                member_triangles[:, [0, 1]],
                member_triangles[:, [1, 2]],
                member_triangles[:, [2, 0]],
            ),
            axis=0,
        )
        component_edges.sort(axis=1)
        _component_edges, component_counts = np.unique(
            component_edges,
            axis=0,
            return_counts=True,
        )
        boundary_edges = int(np.count_nonzero(component_counts == 1))
        nonmanifold_edges = int(np.count_nonzero(component_counts > 2))
        component_details.append(
            {
                "component": component_index + 1,
                "weldedVertices": int(len(member_indices)),
                "triangles": int(len(member_triangles)),
                "bounds": {
                    "minimum": minimum.tolist(),
                    "maximum": maximum.tolist(),
                    "span": span.tolist(),
                },
                "centroid": np.mean(member_positions, axis=0).tolist(),
                "planformCoverage": {
                    "longitudinalX": (
                        float(span[0] / global_span[0])
                        if global_span[0]
                        else 0.0
                    ),
                    "lateralY": (
                        float(span[1] / global_span[1])
                        if global_span[1]
                        else 0.0
                    ),
                },
                "heightZ": float(span[2]),
                "surfaceArea": float(
                    np.linalg.norm(cross, axis=1).sum() * 0.5
                ),
                "boundaryEdges": boundary_edges,
                "nonManifoldEdges": nonmanifold_edges,
                "watertight": (
                    boundary_edges == 0 and nonmanifold_edges == 0
                ),
            }
        )

    boundary_edges = int(np.count_nonzero(counts == 1))
    nonmanifold_edges = int(np.count_nonzero(counts > 2))
    report = {
        "indexedSeamIslands": int(indexed_components),
        "weldedUniquePositions": int(len(unique_positions)),
        "weldedEdges": int(len(unique_edges)),
        "weldedComponents": int(len(component_ids)),
        "boundaryEdges": boundary_edges,
        "nonManifoldEdges": nonmanifold_edges,
        "degenerateTrianglesAfterWeld": int(
            triangle_count - len(nondegenerate)
        ),
        "weldedComponentDetails": component_details,
        "reportedAxes": {
            "longitudinal": "x",
            "lateral": "y",
            "height": "z",
        },
        "verdict": (
            "single-watertight-manifold-not-logical-parts"
            if len(component_ids) == 1
            and boundary_edges == 0
            and nonmanifold_edges == 0
            else "requires-component-by-component-review"
        ),
    }
    diagnostic = {
        "inverse": inverse,
        "weldedRoots": welded_roots,
        "rootToComponent": root_to_component,
        "vertexOffsets": vertex_offsets,
    }
    return report, diagnostic


def glb_geometry_report(document: dict[str, Any]) -> dict[str, Any]:
    accessors = document.get("accessors", [])
    primitives = [
        primitive
        for mesh in document.get("meshes", [])
        for primitive in mesh.get("primitives", [])
    ]

    def accessor_count(index: int | None) -> int:
        return 0 if index is None else int(accessors[index].get("count", 0))

    vertices = sum(
        accessor_count(primitive.get("attributes", {}).get("POSITION"))
        for primitive in primitives
    )
    triangles = 0
    for primitive in primitives:
        indices = primitive.get("indices")
        if indices is None:
            triangles += (
                accessor_count(primitive.get("attributes", {}).get("POSITION"))
                // 3
            )
        else:
            triangles += accessor_count(indices) // 3
    return {
        "meshes": len(document.get("meshes", [])),
        "primitives": len(primitives),
        "vertices": vertices,
        "triangles": triangles,
        "skins": len(document.get("skins", [])),
        "animations": len(document.get("animations", [])),
        "morphTargets": sum(
            len(primitive.get("targets", [])) for primitive in primitives
        ),
        "tangentAccessors": sum(
            1
            for primitive in primitives
            if "TANGENT" in primitive.get("attributes", {})
        ),
    }


def material_report(
    document: dict[str, Any],
    source_dir: Path,
) -> dict[str, Any]:
    materials = document.get("materials", [])
    semantic_roles = [
        material.get("extras", {}).get("nilbotsRole")
        for material in materials
        if material.get("extras", {}).get("nilbotsRole")
    ]
    slots: set[str] = set()
    for material in materials:
        pbr = material.get("pbrMetallicRoughness", {})
        if "baseColorTexture" in pbr:
            slots.add("baseColor")
        if "metallicRoughnessTexture" in pbr:
            slots.add("metallicRoughness")
        if "normalTexture" in material:
            slots.add("normal")
        if "emissiveTexture" in material:
            slots.add("emission")
        if "occlusionTexture" in material:
            slots.add("occlusion")

    lossless_maps = []
    for path in sorted(source_dir.glob("texture-*.png")):
        width, height = png_dimensions(path)
        lossless_maps.append(
            {
                "file": path.name,
                "bytes": path.stat().st_size,
                "sha256": sha256(path),
                "width": width,
                "height": height,
            }
        )
    return {
        "count": len(materials),
        "doubleSided": any(
            bool(material.get("doubleSided")) for material in materials
        ),
        "semanticRoles": semantic_roles,
        "hasTeamAccentMaterial": "team-accent" in semantic_roles,
        "embeddedTextures": sorted(slots),
        "losslessProviderMaps": lossless_maps,
    }


def scene_bounds(
    meshes: list[bpy.types.Object],
    transform: Matrix,
) -> tuple[Vector, Vector]:
    points = []
    for obj in meshes:
        object_matrix = transform @ obj.matrix_world
        points.extend(object_matrix @ vertex.co for vertex in obj.data.vertices)
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


def orientation_matrix(source_up: str, source_facing: str) -> Matrix:
    # Meshy trials have used both standard glTF +Y physical up and a +Z-up
    # object encoded inside glTF. Blender converts standard glTF +Y to DCC +Z.
    # When physical up is source +Z, Blender presents it on DCC +Y, so a fixed
    # +90-degree X rotation restores DCC +Z.
    up_rotation = (
        Matrix.Rotation(math.radians(90), 4, "X")
        if source_up == "+z"
        else Matrix.Identity(4)
    )
    yaw = (
        Matrix.Identity(4)
        if source_facing == "+x"
        else Matrix.Rotation(math.radians(180), 4, "Z")
    )
    return yaw @ up_rotation


def normalize(
    meshes: list[bpy.types.Object],
    source_up: str,
    source_facing: str,
) -> tuple[bpy.types.Object, dict[str, Any]]:
    root = bpy.data.objects.new("AUDIT_NORMALIZED_ROOT", None)
    bpy.context.scene.collection.objects.link(root)
    for obj in meshes:
        obj.parent = root

    rotation = orientation_matrix(source_up, source_facing)
    raw_minimum, raw_maximum = scene_bounds(meshes, rotation)
    span = raw_maximum - raw_minimum
    planform_span = max(span.x, span.y)
    if planform_span <= 0:
        raise ValueError("Master has no planform span")
    scale = 1.0 / planform_span
    center = (raw_minimum + raw_maximum) * 0.5
    translation = Matrix.Translation(
        Vector((-center.x * scale, -center.y * scale, -raw_minimum.z * scale))
    )
    root.matrix_world = (
        translation @ Matrix.Scale(scale, 4) @ rotation
    )
    bpy.context.view_layer.update()
    minimum, maximum = scene_bounds(meshes, Matrix.Identity(4))
    normalized_span = maximum - minimum
    return root, {
        "sourceFacing": source_facing,
        "sourceUp": source_up,
        "targetFacing": "+x",
        "targetUp": "+y",
        "dccReviewUp": "+z",
        "operation": (
            "fixed source-up correction; optional fixed 180-degree yaw; "
            "uniform scale and translation only"
        ),
        "uniformScale": scale,
        "normalizedBounds": {
            "minimum": list(minimum),
            "maximum": list(maximum),
            "span": list(normalized_span),
        },
        "authoredPlanformSpanTiles": 1.0,
    }


def direction_rotation(origin: Vector, target: Vector):
    return (target - origin).to_track_quat("-Z", "Y").to_euler()


def configure_scene() -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = RENDER_SIZE
    scene.render.resolution_y = RENDER_SIZE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 15
    scene.render.pixel_aspect_x = 1.0
    scene.render.pixel_aspect_y = 1.0
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0

    world = bpy.data.worlds.new("BULWARK_AUDIT_WORLD")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = BACKGROUND_RGB
    background.inputs["Strength"].default_value = 0.75

    lights = (
        ("Key", (2.7, -3.2, 4.8), 1050.0, 4.0),
        ("Fill", (-3.2, -1.0, 2.4), 500.0, 5.0),
        ("Rim", (0.8, 3.6, 3.2), 700.0, 3.0),
    )
    for name, location, energy, size in lights:
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        obj = bpy.data.objects.new(name, data)
        obj.location = location
        obj.rotation_euler = direction_rotation(
            Vector(location),
            Vector((0.0, 0.0, 0.15)),
        )
        bpy.context.scene.collection.objects.link(obj)


def camera_for(
    name: str,
    location: Vector,
    target: Vector,
    *,
    orthographic_scale: float | None,
) -> bpy.types.Object:
    data = bpy.data.cameras.new(f"{name}_CAMERA")
    camera = bpy.data.objects.new(f"{name}_CAMERA", data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = location
    camera.rotation_euler = direction_rotation(location, target)
    if orthographic_scale is None:
        data.type = "PERSP"
        data.lens = 50.0
        data.angle = math.radians(42)
    else:
        data.type = "ORTHO"
        data.ortho_scale = orthographic_scale
    bpy.context.scene.camera = camera
    return camera


def render(
    output: Path,
    location: tuple[float, float, float],
    target: tuple[float, float, float],
    *,
    orthographic_scale: float | None,
    transparent: bool = False,
    material_override: bpy.types.Material | None = None,
) -> None:
    scene = bpy.context.scene
    previous_transparent = scene.render.film_transparent
    previous_override = scene.view_layers[0].material_override
    scene.render.film_transparent = transparent
    scene.view_layers[0].material_override = material_override
    camera = camera_for(
        output.stem,
        Vector(location),
        Vector(target),
        orthographic_scale=orthographic_scale,
    )
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    scene.render.film_transparent = previous_transparent
    scene.view_layers[0].material_override = previous_override


def emission_material(
    name: str,
    color: tuple[float, float, float, float],
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
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return material


def render_component_diagnostic(
    meshes: list[bpy.types.Object],
    diagnostic: dict[str, Any],
    output: Path,
    center_z: float,
) -> None:
    palette = (
        (0.95, 0.24, 0.20, 1.0),
        (0.18, 0.78, 0.93, 1.0),
        (0.98, 0.72, 0.18, 1.0),
        (0.48, 0.88, 0.40, 1.0),
        (0.72, 0.46, 0.96, 1.0),
        (0.98, 0.44, 0.70, 1.0),
    )
    component_materials = [
        emission_material(f"AUDIT_COMPONENT_{index + 1}", color)
        for index, color in enumerate(palette)
    ]
    saved: list[tuple[bpy.types.Object, list[Any], list[int]]] = []
    inverse = diagnostic["inverse"]
    roots = diagnostic["weldedRoots"]
    root_to_component = diagnostic["rootToComponent"]
    for obj, offset in zip(meshes, diagnostic["vertexOffsets"], strict=True):
        original_materials = list(obj.data.materials)
        original_indices = [polygon.material_index for polygon in obj.data.polygons]
        saved.append((obj, original_materials, original_indices))
        obj.data.materials.clear()
        for material in component_materials:
            obj.data.materials.append(material)
        for polygon in obj.data.polygons:
            global_vertex = offset + int(polygon.vertices[0])
            welded_vertex = int(inverse[global_vertex])
            root = int(roots[welded_vertex])
            component = root_to_component[root]
            polygon.material_index = component % len(component_materials)

    render(
        output,
        (1.65, -1.75, 1.35 + center_z),
        (0.0, 0.0, center_z),
        orthographic_scale=1.34,
    )
    for obj, materials, indices in saved:
        obj.data.materials.clear()
        for material in materials:
            obj.data.materials.append(material)
        for polygon, material_index in zip(
            obj.data.polygons,
            indices,
            strict=True,
        ):
            polygon.material_index = material_index


def render_probe(
    meshes: list[bpy.types.Object],
    output_dir: Path,
    source_up: str,
    source_facing: str,
) -> dict[str, Any]:
    _root, normalization = normalize(meshes, source_up, source_facing)
    configure_scene()
    suffix = f"up-{source_up[1]}-facing-{'plus' if source_facing == '+x' else 'minus'}-x"
    render(
        output_dir / f"probe-{suffix}.png",
        (0.0, 0.0, 3.0),
        (0.0, 0.0, 0.15),
        orthographic_scale=1.26,
    )
    mask = emission_material("AUDIT_PROBE_MASK", (1.0, 1.0, 1.0, 1.0))
    render(
        output_dir / f"probe-{suffix}-mask.png",
        (0.0, 0.0, 3.0),
        (0.0, 0.0, 0.15),
        orthographic_scale=1.26,
        transparent=True,
        material_override=mask,
    )
    return normalization


def render_final(
    meshes: list[bpy.types.Object],
    output_dir: Path,
    normalization: dict[str, Any],
    topology_diagnostic: dict[str, Any],
) -> None:
    configure_scene()
    span = normalization["normalizedBounds"]["span"]
    center_z = span[2] * 0.5
    render(
        output_dir / "normalized-top.png",
        (0.0, 0.0, 3.0),
        (0.0, 0.0, center_z),
        orthographic_scale=1.26,
    )
    render(
        output_dir / "normalized-side.png",
        (0.0, -3.0, center_z),
        (0.0, 0.0, center_z),
        orthographic_scale=1.26,
    )
    render(
        output_dir / "normalized-iso.png",
        (1.65, -1.75, 1.35 + center_z),
        (0.0, 0.0, center_z),
        orthographic_scale=1.34,
    )
    pitch = math.radians(58)
    distance = 1.95
    render(
        output_dir / "gameplay-camera.png",
        (
            0.0,
            -math.cos(pitch) * distance,
            center_z + math.sin(pitch) * distance,
        ),
        (0.0, 0.0, center_z),
        orthographic_scale=None,
        transparent=True,
    )
    mask = emission_material("AUDIT_MASK", (1.0, 1.0, 1.0, 1.0))
    render(
        output_dir / "top-mask.png",
        (0.0, 0.0, 3.0),
        (0.0, 0.0, center_z),
        orthographic_scale=1.26,
        transparent=True,
        material_override=mask,
    )
    render(
        output_dir / "side-mask.png",
        (0.0, -3.0, center_z),
        (0.0, 0.0, center_z),
        orthographic_scale=1.26,
        transparent=True,
        material_override=mask,
    )
    render_component_diagnostic(
        meshes,
        topology_diagnostic,
        output_dir / "components-iso.png",
        center_z,
    )


def provenance_report(
    master: FormMaster,
    repo_root: Path,
    source_path: Path,
) -> dict[str, Any]:
    record_path = (
        repo_root
        / "art/class-models/provider-runs/meshy"
        / master.asset_id
        / master.task_id
        / "request-record.json"
    )
    record = json.loads(record_path.read_text(encoding="utf-8"))
    raw_artifact = next(
        artifact
        for artifact in record["artifacts"]
        if artifact["file"] == "raw-model.glb"
    )
    actual_hash = sha256(source_path)
    actual_bytes = source_path.stat().st_size
    if raw_artifact["sha256"] != actual_hash:
        raise ValueError(f"Durable master hash mismatch for {master.asset_id}")
    if raw_artifact["bytes"] != actual_bytes:
        raise ValueError(f"Durable master byte-size mismatch for {master.asset_id}")
    return {
        "requestRecord": str(record_path.relative_to(repo_root)),
        "durableMasterMatchesRecord": True,
        "providerPreviewUsedForVerdict": False,
        "paidCallsMade": 0,
    }


def base_report(
    master: FormMaster,
    repo_root: Path,
    source_path: Path,
    source_dir: Path,
) -> dict[str, Any]:
    document = glb_json(source_path)
    return {
        "schemaVersion": 1,
        "assetId": master.asset_id,
        "label": master.label,
        "taskId": master.task_id,
        "auditTool": {
            "name": "Blender",
            "version": BLENDER_VERSION,
            "script": "scripts/class-models/audit-bulwark-form-masters.py",
        },
        "source": {
            "storagePath": (
                f"var/model-artifacts/meshy/{master.asset_id}/"
                f"{master.task_id}/raw-model.glb"
            ),
            "bytes": source_path.stat().st_size,
            "sha256": sha256(source_path),
            "preservedUnmodified": True,
        },
        "provenance": provenance_report(
            master,
            repo_root,
            source_path,
        ),
        "geometry": glb_geometry_report(document),
        "materials": material_report(document, source_dir),
        "presentation": {
            "currentSpriteScale": master.runtime_scale,
            "directional": master.directional,
            "turretHasNoPrivilegedFacing": not master.directional,
        },
    }


def main() -> None:
    args = parse_args()
    args.repo_root = args.repo_root.resolve()
    args.artifacts_root = args.artifacts_root.resolve()
    source_facing = keyed_values(
        args.source_facing,
        allowed={"+x", "-x"},
        option="--source-facing",
    )
    source_up = keyed_values(
        args.source_up,
        allowed={"+y", "+z"},
        option="--source-up",
    )

    for master in MASTERS:
        source_dir = args.artifacts_root / master.asset_id / master.task_id
        source_path = source_dir / "raw-model.glb"
        if not source_path.is_file():
            raise FileNotFoundError(source_path)
        output_dir = (
            args.repo_root
            / "art/class-models/provider-runs/meshy"
            / master.asset_id
            / master.task_id
            / "audit"
        )
        output_dir.mkdir(parents=True, exist_ok=True)
        clear_scene()
        meshes = import_master(source_path)
        report = base_report(
            master,
            args.repo_root,
            source_path,
            source_dir,
        )

        if args.phase == "probe":
            report["normalizationProbes"] = {}
            hypotheses = (
                ("+z", "+x"),
                ("+z", "-x"),
                ("+y", "+x"),
                ("+y", "-x"),
            )
            for index, (up, facing) in enumerate(hypotheses):
                if index:
                    clear_scene()
                    meshes = import_master(source_path)
                key = f"up-{up}-facing-{facing}"
                report["normalizationProbes"][key] = render_probe(
                    meshes,
                    output_dir,
                    up,
                    facing,
                )
        else:
            up = source_up.get(master.asset_id)
            facing = source_facing.get(master.asset_id)
            if up is None or facing is None:
                raise ValueError(
                    f"Missing reviewed source axes for {master.asset_id}"
                )
            _root, normalization = normalize(meshes, up, facing)
            topology, diagnostic = welded_topology(meshes)
            report["normalization"] = normalization
            report["topology"] = topology
            render_final(
                meshes,
                output_dir,
                normalization,
                diagnostic,
            )

        encoded = json.dumps(report, indent=2) + "\n"
        (output_dir / f"{args.phase}-blender-report.json").write_text(
            encoded,
            encoding="utf-8",
        )
        if args.phase == "render":
            (output_dir.parent / "geometry-report.json").write_text(
                encoded,
                encoding="utf-8",
            )
        print(
            f"AUDIT {args.phase} {master.asset_id} "
            f"{report['source']['sha256']}"
        )


if __name__ == "__main__":
    main()
