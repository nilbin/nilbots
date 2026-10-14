#!/usr/bin/env python3
"""Evaluate a Meshy surface transfer onto an authored Aegis form.

This is an evaluation recipe, not an asset builder.  Start Blender with one
authored Shell or Turret source Blend already open, then run this script:

    Blender source/aegis-tortoise-shell-editable.blend --background \
      --python evaluate_provider_surface_transfer.py -- \
      --asset-id aegis-tortoise-shell \
      --artifacts-root /path/to/var/model-artifacts/meshy \
      --output-dir /path/to/sandbox/aegis-shell-surface-transfer

The open source scene is never saved.  The recipe snapshots only model meshes
parented to the expected authored rig, joins them into an evaluation-only
static target, builds a unique Smart UV atlas, and bakes the pinned provider
surface through an explicit expanded same-topology cage.  It writes PNGs and
one provenance report only beneath ``--output-dir``.  It never calls a
provider, mutates a candidate GLB, or replaces the authored source materials.
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
from typing import Iterable, Sequence

import bmesh
import bpy
from mathutils import Matrix, Vector


SCHEMA_VERSION = 1
REQUIRED_BLENDER = "5.2.0"
BAKE_SIZE = 512
RENDER_SIZE = 768
UV_LAYER_NAME = "ProviderSurfaceTransferUV"
EVALUATION_COLLECTION = "EVAL.ProviderSurfaceTransfer"

ASSETS: dict[str, dict[str, object]] = {
    "aegis-tortoise-shell": {
        "rig": "Aegis.Shell.Rig",
        "idleAction": "AegisShell_Idle",
        "providerYawDegrees": 180,
        "targetTriangles": 12_000,
        "provider": {
            "taskId": "019fb22d-c313-76f8-8eca-91ccbb46afd0",
            "relative": (
                "aegis-tortoise-shell/"
                "019fb22d-c313-76f8-8eca-91ccbb46afd0"
            ),
            "files": {
                "raw-model.glb": (
                    "8a0a0129cc57afdf12a0c0e70159f2acd547b426501941330e4712d06b78c5ec"
                ),
                "texture-0-base-color.png": (
                    "9a4749049079988989c2a1477584c05f8a4c5a5d7df7d53f468417eede9e8d75"
                ),
                "texture-0-emission.png": (
                    "836d9dc81598d8a73fd20647a43d30be5b9d18c6f3eed15353b506f883e82dd5"
                ),
                "texture-0-metallic.png": (
                    "de771b0ea97789f124a68bb9fa3bdfa646a161706ccb130da4c76d3fc808a1f8"
                ),
                "texture-0-normal.png": (
                    "6095d6dc215d0fc8586a3cb918761bb235c85d57f1f94213e642104a44c82239"
                ),
                "texture-0-roughness.png": (
                    "e5fc0e88a4d9649755db110e0dbf9ab43c8159ef6ee0fd5048389470c8f86b91"
                ),
            },
        },
    },
    "aegis-tortoise-turret": {
        "rig": "Aegis.Turret.Rig",
        "idleAction": "AegisTurret_Idle",
        "providerYawDegrees": 0,
        "targetTriangles": 9_000,
        "provider": {
            "taskId": "019fb232-6e09-787f-85cc-dbaea29fb9b6",
            "relative": (
                "aegis-tortoise-turret/"
                "019fb232-6e09-787f-85cc-dbaea29fb9b6"
            ),
            "files": {
                "raw-model.glb": (
                    "a845f48ac9f0861c8ff2860de9c0dc79b0afa728794663f9d8ff70f7187f9cac"
                ),
                "texture-0-base-color.png": (
                    "29418bafb2c80aad3e43b47c75e345ce7531e3f1df60eaa1292da198f38cf0ae"
                ),
                "texture-0-emission.png": (
                    "c74d8072aeecf211103c224efaaf9a5b35a404f2a4e846cded927231f6e18cba"
                ),
                "texture-0-metallic.png": (
                    "1e733e1785d67d9973ab97c74227b787987ff0491bb7c7e4e10c984d8d9939bf"
                ),
                "texture-0-normal.png": (
                    "00bd5edb7bba31ab06e078034f2423cb018f772a1ae837ba4c5523bd92746559"
                ),
                "texture-0-roughness.png": (
                    "ecd6ec7bc2f2d9fbae9bd3069c3ddccb1e578d1ab00932c6b6de46a288bf2b20"
                ),
            },
        },
    },
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(
        description="Evaluate pinned provider PBR transfer onto an open Aegis source Blend."
    )
    parser.add_argument("--asset-id", required=True, choices=sorted(ASSETS))
    parser.add_argument(
        "--artifacts-root",
        required=True,
        type=Path,
        help="Directory containing the durable Meshy artifact families.",
    )
    parser.add_argument(
        "--output-dir",
        required=True,
        type=Path,
        help="Evaluation-only output directory; source and candidate directories are refused.",
    )
    parser.add_argument(
        "--target-triangles",
        type=int,
        default=None,
        help="Optional low-target triangle ceiling; defaults per form.",
    )
    parser.add_argument(
        "--cage-distance",
        type=float,
        default=0.06,
        help="Outward normal expansion of the explicit same-topology cage, in tiles.",
    )
    parser.add_argument(
        "--max-ray-distance",
        type=float,
        default=0.24,
        help="Selected-to-active bake ray limit, in tiles.",
    )
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=False) + "\n")


def is_beneath(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
    except ValueError:
        return False
    return True


def verify_environment(
    asset_id: str,
    output_dir: Path,
) -> tuple[Path, dict[str, object]]:
    if bpy.app.version_string.split()[0] != REQUIRED_BLENDER:
        raise RuntimeError(
            f"Blender {REQUIRED_BLENDER} is required; found {bpy.app.version_string}."
        )

    if not bpy.data.filepath:
        raise RuntimeError(
            "Open an authored Aegis source .blend before running this evaluator."
        )
    source_blend = Path(bpy.data.filepath).resolve()
    if not source_blend.is_file() or source_blend.suffix.lower() != ".blend":
        raise RuntimeError(f"Open source Blend is not a readable file: {source_blend}")

    source_dir = source_blend.parent
    candidate_dir = source_dir.parent / "candidate"
    if is_beneath(output_dir, source_dir) or is_beneath(output_dir, candidate_dir):
        raise RuntimeError(
            "--output-dir must not be the authored source or candidate directory."
        )

    specification = ASSETS[asset_id]
    rig_name = str(specification["rig"])
    rig = bpy.data.objects.get(rig_name)
    if rig is None or rig.type != "ARMATURE":
        raise RuntimeError(
            f"Open Blend does not contain expected armature {rig_name!r}."
        )

    return source_blend, {
        "file": source_blend.name,
        "bytes": source_blend.stat().st_size,
        "sha256": sha256(source_blend),
        "armature": rig_name,
    }


def verify_provider(
    artifacts_root: Path,
    asset_id: str,
) -> tuple[Path, dict[str, object]]:
    provider = ASSETS[asset_id]["provider"]
    assert isinstance(provider, dict)
    relative = Path(str(provider["relative"]))
    provider_dir = artifacts_root / relative
    expected_files = provider["files"]
    assert isinstance(expected_files, dict)

    files: dict[str, object] = {}
    for name, expected_hash in expected_files.items():
        source = provider_dir / str(name)
        if not source.is_file():
            raise RuntimeError(f"Missing pinned provider artifact: {source}")
        actual_hash = sha256(source)
        if actual_hash != expected_hash:
            raise RuntimeError(
                f"Pinned provider hash mismatch for {name}: "
                f"expected {expected_hash}, found {actual_hash}."
            )
        files[str(name)] = {
            "bytes": source.stat().st_size,
            "sha256": actual_hash,
        }

    raw_glb = provider_dir / "raw-model.glb"
    return raw_glb, {
        "taskId": provider["taskId"],
        "relativeDirectory": relative.as_posix(),
        "files": files,
        "networkCalls": 0,
    }


def object_is_descendant_of(
    obj: bpy.types.Object,
    ancestor: bpy.types.Object,
) -> bool:
    parent = obj.parent
    while parent is not None:
        if parent == ancestor:
            return True
        parent = parent.parent
    return False


def source_model_meshes(rig: bpy.types.Object) -> list[bpy.types.Object]:
    meshes = [
        obj
        for obj in bpy.data.objects
        if obj.type == "MESH"
        and object_is_descendant_of(obj, rig)
        and not obj.name.startswith("REVIEW.")
        and not obj.name.startswith("EVAL.")
        and not bool(obj.get("reviewOnly"))
        and not any(collection.name.startswith("REVIEW") for collection in obj.users_collection)
    ]
    meshes.sort(key=lambda obj: obj.name)
    if not meshes:
        raise RuntimeError(f"No non-REVIEW model meshes found beneath {rig.name}.")
    return meshes


def ensure_evaluation_collection() -> bpy.types.Collection:
    collection = bpy.data.collections.get(EVALUATION_COLLECTION)
    if collection is None:
        collection = bpy.data.collections.new(EVALUATION_COLLECTION)
        bpy.context.scene.collection.children.link(collection)
    return collection


def set_idle_pose(rig: bpy.types.Object, action_name: str) -> None:
    action = bpy.data.actions.get(action_name)
    if action is None:
        raise RuntimeError(f"Open Blend is missing idle action {action_name!r}.")
    rig.animation_data_create()
    rig.animation_data.action = action
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()


def deselect_all() -> None:
    if bpy.context.object is not None and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")


def set_active(obj: bpy.types.Object) -> None:
    deselect_all()
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def triangle_count(mesh: bpy.types.Mesh) -> int:
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def mesh_bounds(mesh: bpy.types.Mesh) -> tuple[Vector, Vector]:
    if not mesh.vertices:
        raise RuntimeError(f"Mesh {mesh.name!r} has no vertices.")
    minimum = Vector(
        tuple(min(vertex.co[axis] for vertex in mesh.vertices) for axis in range(3))
    )
    maximum = Vector(
        tuple(max(vertex.co[axis] for vertex in mesh.vertices) for axis in range(3))
    )
    return minimum, maximum


def collective_world_bounds(
    objects: Iterable[bpy.types.Object],
) -> tuple[Vector, Vector]:
    points: list[Vector] = []
    for obj in objects:
        points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    if not points:
        raise RuntimeError("Cannot measure an empty object collection.")
    minimum = Vector(
        tuple(min(point[axis] for point in points) for axis in range(3))
    )
    maximum = Vector(
        tuple(max(point[axis] for point in points) for axis in range(3))
    )
    return minimum, maximum


def vector_values(value: Vector) -> list[float]:
    return [round(component, 9) for component in value]


def matrix_values(value: Matrix) -> list[list[float]]:
    return [
        [round(value[row][column], 9) for column in range(4)]
        for row in range(4)
    ]


def duplicate_static_target(
    rig: bpy.types.Object,
    source_meshes: Sequence[bpy.types.Object],
    collection: bpy.types.Collection,
) -> tuple[bpy.types.Object, dict[str, object]]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    duplicates: list[bpy.types.Object] = []
    for source in source_meshes:
        evaluated = source.evaluated_get(depsgraph)
        mesh = bpy.data.meshes.new_from_object(
            evaluated,
            preserve_all_data_layers=True,
            depsgraph=depsgraph,
        )
        mesh.name = f"EVAL.Static.{source.name}.Mesh"
        world = evaluated.matrix_world.copy()
        mesh.transform(world)
        if world.to_3x3().determinant() < 0:
            mesh.flip_normals()
        mesh.materials.clear()
        mesh.update()

        duplicate = bpy.data.objects.new(f"EVAL.Static.{source.name}", mesh)
        duplicate.matrix_world = Matrix.Identity(4)
        collection.objects.link(duplicate)
        duplicates.append(duplicate)

    deselect_all()
    for duplicate in duplicates:
        duplicate.select_set(True)
    target = duplicates[0]
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.join()
    target = bpy.context.active_object
    if target is None or target.type != "MESH":
        raise RuntimeError("Failed to join the authored evaluation target.")
    target.name = f"EVAL.{rig.name}.StaticLow"
    target.data.name = f"{target.name}.Mesh"
    target["evaluationOnly"] = True
    target["sourceRig"] = rig.name

    return target, {
        "sourceObjects": [source.name for source in source_meshes],
        "sourceObjectCount": len(source_meshes),
        "evaluatedVerticesBeforeLowTarget": len(target.data.vertices),
        "evaluatedTrianglesBeforeLowTarget": triangle_count(target.data),
    }


def normalize_target(target: bpy.types.Object) -> dict[str, object]:
    before_minimum, before_maximum = mesh_bounds(target.data)
    span_x = before_maximum.x - before_minimum.x
    span_y = before_maximum.y - before_minimum.y
    planform = max(span_x, span_y)
    if planform <= 1e-9:
        raise RuntimeError("Authored target has a degenerate planform.")
    scale = 1.0 / planform
    centre_x = (before_minimum.x + before_maximum.x) * 0.5
    centre_y = (before_minimum.y + before_maximum.y) * 0.5
    transform = Matrix.Translation(
        Vector((-centre_x * scale, -centre_y * scale, -before_minimum.z * scale))
    ) @ Matrix.Scale(scale, 4)
    target.data.transform(transform)
    target.data.update()
    after_minimum, after_maximum = mesh_bounds(target.data)
    return {
        "coordinateSystem": "Blender Z-up",
        "yawDegrees": 0,
        "uniformScale": scale,
        "matrix": matrix_values(transform),
        "boundsBefore": {
            "minimum": vector_values(before_minimum),
            "maximum": vector_values(before_maximum),
        },
        "boundsAfter": {
            "minimum": vector_values(after_minimum),
            "maximum": vector_values(after_maximum),
        },
        "rule": "uniform one-tile planform; XY centred; minimum Z floored",
    }


def apply_low_target(
    target: bpy.types.Object,
    triangle_ceiling: int,
) -> dict[str, object]:
    if triangle_ceiling < 1:
        raise RuntimeError("--target-triangles must be positive.")
    initial = triangle_count(target.data)
    if initial > triangle_ceiling:
        modifier = target.modifiers.new("EVAL.LowTarget", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = triangle_ceiling / initial
        if hasattr(modifier, "use_collapse_triangulate"):
            modifier.use_collapse_triangulate = True
        set_active(target)
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    triangulate = target.modifiers.new("EVAL.Triangulate", "TRIANGULATE")
    set_active(target)
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    target.data.validate(clean_customdata=False)
    target.data.update()
    final = triangle_count(target.data)
    return {
        "requestedTriangleCeiling": triangle_ceiling,
        "trianglesBefore": initial,
        "trianglesAfter": final,
        "verticesAfter": len(target.data.vertices),
        "method": "deterministic collapse when required, followed by explicit triangulation",
    }


def create_smart_uv_atlas(target: bpy.types.Object) -> dict[str, object]:
    while target.data.uv_layers:
        target.data.uv_layers.remove(target.data.uv_layers[0])
    target.data.uv_layers.new(name=UV_LAYER_NAME)

    set_active(target)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(
        angle_limit=math.radians(66),
        margin_method="FRACTION",
        rotate_method="AXIS_ALIGNED_Y",
        island_margin=0.018,
        area_weight=0.0,
        correct_aspect=True,
        scale_to_bounds=True,
    )
    bpy.ops.object.mode_set(mode="OBJECT")
    target.data.uv_layers.active.name = UV_LAYER_NAME
    target.data.update()
    return {
        "layer": UV_LAYER_NAME,
        "operator": "bpy.ops.uv.smart_project",
        "angleLimitDegrees": 66,
        "marginMethod": "FRACTION",
        "rotateMethod": "AXIS_ALIGNED_Y",
        "islandMargin": 0.018,
        "areaWeight": 0.0,
        "correctAspect": True,
        "scaleToBounds": True,
    }


def topology_hash(mesh: bpy.types.Mesh) -> str:
    digest = hashlib.sha256()
    for vertex in mesh.vertices:
        digest.update(
            struct.pack(
                "<3d",
                round(vertex.co.x, 9),
                round(vertex.co.y, 9),
                round(vertex.co.z, 9),
            )
        )
    for polygon in mesh.polygons:
        digest.update(struct.pack("<I", len(polygon.vertices)))
        for index in polygon.vertices:
            digest.update(struct.pack("<I", index))
    uv_layer = mesh.uv_layers.get(UV_LAYER_NAME)
    if uv_layer is not None:
        for datum in uv_layer.data:
            digest.update(
                struct.pack(
                    "<2d",
                    round(datum.uv.x, 9),
                    round(datum.uv.y, 9),
                )
            )
    return digest.hexdigest()


def create_explicit_cage(
    target: bpy.types.Object,
    collection: bpy.types.Collection,
    distance: float,
) -> tuple[bpy.types.Object, dict[str, object]]:
    if distance <= 0:
        raise RuntimeError("--cage-distance must be positive.")
    cage_mesh = target.data.copy()
    cage_mesh.name = f"{target.data.name}.ExplicitCage"
    source_vertex_count = len(target.data.vertices)
    source_polygon_count = len(target.data.polygons)

    bm = bmesh.new()
    bm.from_mesh(cage_mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    for vertex in bm.verts:
        normal = vertex.normal.normalized()
        vertex.co += normal * distance
    bm.to_mesh(cage_mesh)
    bm.free()
    cage_mesh.update()

    cage = bpy.data.objects.new("EVAL.ExplicitSameTopologyCage", cage_mesh)
    cage["evaluationOnly"] = True
    cage["cageDistance"] = distance
    cage.hide_render = True
    collection.objects.link(cage)
    if (
        len(cage_mesh.vertices) != source_vertex_count
        or len(cage_mesh.polygons) != source_polygon_count
    ):
        raise RuntimeError("Explicit cage no longer has target topology.")
    return cage, {
        "kind": "explicit expanded same-topology cage",
        "distance": distance,
        "vertices": len(cage_mesh.vertices),
        "polygons": len(cage_mesh.polygons),
        "targetVertices": source_vertex_count,
        "targetPolygons": source_polygon_count,
    }


def import_provider(raw_glb: Path) -> list[bpy.types.Object]:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(raw_glb))
    imported = set(bpy.data.objects) - before
    provider_meshes = sorted(
        (obj for obj in imported if obj.type == "MESH"),
        key=lambda obj: obj.name,
    )
    if not provider_meshes:
        raise RuntimeError(f"Pinned provider GLB contains no mesh: {raw_glb}")
    for obj in provider_meshes:
        obj.name = f"EVAL.Provider.{obj.name}"
        obj["evaluationOnly"] = True
        obj.hide_set(False)
        obj.hide_render = False
    bpy.context.view_layer.update()
    return provider_meshes


def normalize_provider(
    provider_meshes: Sequence[bpy.types.Object],
    yaw_degrees: float,
) -> dict[str, object]:
    yaw = Matrix.Rotation(math.radians(yaw_degrees), 4, "Z")
    for obj in provider_meshes:
        obj.matrix_world = yaw @ obj.matrix_world
    bpy.context.view_layer.update()

    rotated_minimum, rotated_maximum = collective_world_bounds(provider_meshes)
    planform = max(
        rotated_maximum.x - rotated_minimum.x,
        rotated_maximum.y - rotated_minimum.y,
    )
    if planform <= 1e-9:
        raise RuntimeError("Provider reference has a degenerate planform.")
    scale = 1.0 / planform
    centre_x = (rotated_minimum.x + rotated_maximum.x) * 0.5
    centre_y = (rotated_minimum.y + rotated_maximum.y) * 0.5
    fit = Matrix.Translation(
        Vector(
            (
                -centre_x * scale,
                -centre_y * scale,
                -rotated_minimum.z * scale,
            )
        )
    ) @ Matrix.Scale(scale, 4)
    for obj in provider_meshes:
        obj.matrix_world = fit @ obj.matrix_world
    bpy.context.view_layer.update()
    final_minimum, final_maximum = collective_world_bounds(provider_meshes)
    return {
        "coordinateSystem": "imported Blender Z-up",
        "authorityYawDegrees": yaw_degrees,
        "uniformScale": scale,
        "matrixAfterYaw": matrix_values(fit),
        "combinedAuthorityMatrix": matrix_values(fit @ yaw),
        "boundsAfterYawBeforeFit": {
            "minimum": vector_values(rotated_minimum),
            "maximum": vector_values(rotated_maximum),
        },
        "boundsAfter": {
            "minimum": vector_values(final_minimum),
            "maximum": vector_values(final_maximum),
        },
        "rule": "authority yaw, then uniform one-tile planform; XY centred; minimum Z floored",
    }


def ensure_material(material: bpy.types.Material | None, label: str) -> bpy.types.Material:
    if material is not None:
        duplicate = material.copy()
        duplicate.name = f"EVAL.ProviderBase.{label}.{material.name}"
        return duplicate
    duplicate = bpy.data.materials.new(f"EVAL.ProviderBase.{label}.Default")
    duplicate.use_nodes = True
    return duplicate


def duplicate_provider_materials(
    provider_meshes: Sequence[bpy.types.Object],
) -> dict[str, list[bpy.types.Material]]:
    by_pointer: dict[int, bpy.types.Material] = {}
    layouts: dict[str, list[bpy.types.Material]] = {}
    for obj in provider_meshes:
        materials: list[bpy.types.Material] = []
        if not obj.material_slots:
            obj.data.materials.append(None)
        for index, slot in enumerate(obj.material_slots):
            source = slot.material
            pointer = source.as_pointer() if source is not None else -(index + 1)
            material = by_pointer.get(pointer)
            if material is None:
                material = ensure_material(source, obj.name)
                by_pointer[pointer] = material
            slot.material = material
            materials.append(material)
        layouts[obj.name] = materials
    return layouts


def assign_material_layout(
    provider_meshes: Sequence[bpy.types.Object],
    layouts: dict[str, list[bpy.types.Material]],
) -> None:
    for obj in provider_meshes:
        materials = layouts[obj.name]
        if len(materials) != len(obj.material_slots):
            raise RuntimeError(f"Provider material layout changed on {obj.name}.")
        for slot, material in zip(obj.material_slots, materials, strict=True):
            slot.material = material


def route_material_to_emission(
    source: bpy.types.Material,
    channel: str | None,
) -> bpy.types.Material:
    material = source.copy()
    material.name = f"EVAL.{channel or 'Coverage'}.{source.name}"
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    output = next(
        (node for node in nodes if node.type == "OUTPUT_MATERIAL" and node.is_active_output),
        None,
    )
    if output is None:
        output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Strength"].default_value = 1.0

    if channel is None:
        emission.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    else:
        principled = next(
            (node for node in nodes if node.type == "BSDF_PRINCIPLED"),
            None,
        )
        if principled is None or channel not in principled.inputs:
            raise RuntimeError(
                f"Provider material {source.name!r} has no Principled {channel!r} input."
            )
        source_socket = principled.inputs[channel]
        if source_socket.is_linked:
            links.new(source_socket.links[0].from_socket, emission.inputs["Color"])
        else:
            value = float(source_socket.default_value)
            emission.inputs["Color"].default_value = (value, value, value, 1.0)

    for link in list(output.inputs["Surface"].links):
        links.remove(link)
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return material


def channel_material_layout(
    base_layouts: dict[str, list[bpy.types.Material]],
    channel: str | None,
) -> dict[str, list[bpy.types.Material]]:
    by_pointer: dict[int, bpy.types.Material] = {}
    result: dict[str, list[bpy.types.Material]] = {}
    for object_name, materials in base_layouts.items():
        routed: list[bpy.types.Material] = []
        for source in materials:
            pointer = source.as_pointer()
            material = by_pointer.get(pointer)
            if material is None:
                material = route_material_to_emission(source, channel)
                by_pointer[pointer] = material
            routed.append(material)
        result[object_name] = routed
    return result


def fill_image(
    image: bpy.types.Image,
    rgba: tuple[float, float, float, float],
) -> None:
    pixels = array("f", rgba) * (image.size[0] * image.size[1])
    image.pixels.foreach_set(pixels)
    image.update()


def create_bake_image(
    name: str,
    color_space: str,
    fill: tuple[float, float, float, float],
) -> bpy.types.Image:
    image = bpy.data.images.new(
        name,
        width=BAKE_SIZE,
        height=BAKE_SIZE,
        alpha=True,
        float_buffer=False,
        is_data=color_space == "Non-Color",
    )
    # Blender 5.2 clears generated pixel data when the colour space changes.
    # Set it before writing the deterministic sentinel/default pixels.
    image.colorspace_settings.name = color_space
    fill_image(image, fill)
    return image


def create_target_bake_material(
    images: dict[str, bpy.types.Image],
) -> tuple[bpy.types.Material, dict[str, bpy.types.ShaderNodeTexImage]]:
    material = bpy.data.materials.new("EVAL.ProviderSurfaceBakeTarget")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    material.node_tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])

    image_nodes: dict[str, bpy.types.ShaderNodeTexImage] = {}
    for index, (name, image) in enumerate(images.items()):
        node = nodes.new("ShaderNodeTexImage")
        node.name = f"EVAL.BakeDestination.{name}"
        node.label = name
        node.image = image
        node.location = (-650, 220 - index * 190)
        image_nodes[name] = node
    return material, image_nodes


def activate_bake_destination(
    material: bpy.types.Material,
    node: bpy.types.ShaderNodeTexImage,
) -> None:
    for candidate in material.node_tree.nodes:
        candidate.select = False
    node.select = True
    material.node_tree.nodes.active = node


def configure_bake(
    cage: bpy.types.Object,
    max_ray_distance: float,
) -> None:
    if max_ray_distance <= 0:
        raise RuntimeError("--max-ray-distance must be positive.")
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    bake = scene.render.bake
    bake.target = "IMAGE_TEXTURES"
    bake.use_selected_to_active = True
    bake.use_clear = False
    bake.use_cage = True
    bake.cage_object = cage
    bake.cage_extrusion = 0.0
    bake.max_ray_distance = max_ray_distance
    bake.margin = 12
    bake.margin_type = "EXTEND"
    bake.normal_space = "TANGENT"
    bake.normal_r = "POS_X"
    bake.normal_g = "POS_Y"
    bake.normal_b = "POS_Z"
    bake.view_from = "ABOVE_SURFACE"


def select_for_bake(
    provider_meshes: Sequence[bpy.types.Object],
    target: bpy.types.Object,
) -> None:
    deselect_all()
    for source in provider_meshes:
        source.hide_set(False)
        source.select_set(True)
    target.hide_set(False)
    target.select_set(True)
    bpy.context.view_layer.objects.active = target


def save_image(image: bpy.types.Image, path: Path) -> dict[str, object]:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.file_format = "PNG"
    image.filepath_raw = str(path)
    image.save()
    return {
        "file": path.name,
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
        "dimensions": [int(image.size[0]), int(image.size[1])],
        "colorSpace": image.colorspace_settings.name,
    }


def bake_maps(
    provider_meshes: Sequence[bpy.types.Object],
    target: bpy.types.Object,
    cage: bpy.types.Object,
    output_dir: Path,
    max_ray_distance: float,
) -> tuple[
    dict[str, bpy.types.Image],
    bpy.types.Material,
    dict[str, object],
]:
    images = {
        "basecolor": create_bake_image(
            "EVAL.BakedBaseColor",
            "sRGB",
            (0.0, 0.0, 0.0, 1.0),
        ),
        "normal": create_bake_image(
            "EVAL.BakedTangentNormal",
            "Non-Color",
            (0.5, 0.5, 1.0, 1.0),
        ),
        "roughness": create_bake_image(
            "EVAL.BakedRoughness",
            "Non-Color",
            (1.0, 1.0, 1.0, 1.0),
        ),
        "metallic": create_bake_image(
            "EVAL.BakedMetallic",
            "Non-Color",
            (0.0, 0.0, 0.0, 1.0),
        ),
        "coverage": create_bake_image(
            "EVAL.BakedCoverage",
            "Non-Color",
            (0.0, 0.0, 0.0, 1.0),
        ),
    }
    target_material, destinations = create_target_bake_material(images)
    target.data.materials.clear()
    target.data.materials.append(target_material)

    base_layouts = duplicate_provider_materials(provider_meshes)
    configure_bake(cage, max_ray_distance)
    outputs: dict[str, object] = {}

    assign_material_layout(provider_meshes, base_layouts)
    activate_bake_destination(target_material, destinations["basecolor"])
    select_for_bake(provider_meshes, target)
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"})
    outputs["basecolor"] = save_image(
        images["basecolor"], output_dir / "basecolor.png"
    )

    assign_material_layout(provider_meshes, base_layouts)
    activate_bake_destination(target_material, destinations["normal"])
    select_for_bake(provider_meshes, target)
    bpy.ops.object.bake(type="NORMAL")
    outputs["normal"] = save_image(images["normal"], output_dir / "normal.png")

    assign_material_layout(provider_meshes, base_layouts)
    activate_bake_destination(target_material, destinations["roughness"])
    select_for_bake(provider_meshes, target)
    bpy.ops.object.bake(type="ROUGHNESS")
    outputs["roughness"] = save_image(
        images["roughness"], output_dir / "roughness.png"
    )

    metallic_layouts = channel_material_layout(base_layouts, "Metallic")
    assign_material_layout(provider_meshes, metallic_layouts)
    activate_bake_destination(target_material, destinations["metallic"])
    select_for_bake(provider_meshes, target)
    bpy.ops.object.bake(type="EMIT")
    outputs["metallic"] = save_image(
        images["metallic"], output_dir / "metallic.png"
    )

    coverage_layouts = channel_material_layout(base_layouts, None)
    assign_material_layout(provider_meshes, coverage_layouts)
    activate_bake_destination(target_material, destinations["coverage"])
    select_for_bake(provider_meshes, target)
    bpy.ops.object.bake(type="EMIT")
    outputs["coverage"] = save_image(
        images["coverage"], output_dir / "coverage.png"
    )
    assign_material_layout(provider_meshes, base_layouts)

    return images, target_material, {
        "resolution": [BAKE_SIZE, BAKE_SIZE],
        "engine": "Cycles CPU",
        "samples": 1,
        "selectedToActive": True,
        "cageObject": cage.name,
        "cageExtrusionSetting": 0.0,
        "maxRayDistance": max_ray_distance,
        "marginPixels": 12,
        "normalSpace": "TANGENT",
        "outputs": outputs,
    }


def image_pixels(image: bpy.types.Image) -> array:
    values = array("f", [0.0]) * (image.size[0] * image.size[1] * 4)
    image.pixels.foreach_get(values)
    return values


def signed_area(
    a: tuple[float, float],
    b: tuple[float, float],
    c: tuple[float, float],
) -> float:
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (
        c[0] - a[0]
    )


def rasterize_uv_occupancy(mesh: bpy.types.Mesh, size: int) -> bytearray:
    layer = mesh.uv_layers.get(UV_LAYER_NAME)
    if layer is None:
        raise RuntimeError(f"Target is missing UV layer {UV_LAYER_NAME!r}.")
    occupied = bytearray(size * size)
    for polygon in mesh.polygons:
        if len(polygon.loop_indices) != 3:
            raise RuntimeError("UV occupancy requires the explicit triangulated target.")
        vertices = [
            (
                float(layer.data[loop_index].uv.x),
                float(layer.data[loop_index].uv.y),
            )
            for loop_index in polygon.loop_indices
        ]
        area = signed_area(vertices[0], vertices[1], vertices[2])
        if abs(area) <= 1e-12:
            continue
        minimum_x = max(0, math.floor(min(point[0] for point in vertices) * size))
        maximum_x = min(
            size - 1,
            math.ceil(max(point[0] for point in vertices) * size),
        )
        minimum_y = max(0, math.floor(min(point[1] for point in vertices) * size))
        maximum_y = min(
            size - 1,
            math.ceil(max(point[1] for point in vertices) * size),
        )
        for y in range(minimum_y, maximum_y + 1):
            sample_y = (y + 0.5) / size
            for x in range(minimum_x, maximum_x + 1):
                sample = ((x + 0.5) / size, sample_y)
                edge_0 = signed_area(vertices[0], vertices[1], sample)
                edge_1 = signed_area(vertices[1], vertices[2], sample)
                edge_2 = signed_area(vertices[2], vertices[0], sample)
                if (
                    (edge_0 >= -1e-10 and edge_1 >= -1e-10 and edge_2 >= -1e-10)
                    or (
                        edge_0 <= 1e-10
                        and edge_1 <= 1e-10
                        and edge_2 <= 1e-10
                    )
                ):
                    occupied[y * size + x] = 1
    return occupied


def surface_metrics(
    target: bpy.types.Object,
    images: dict[str, bpy.types.Image],
) -> dict[str, object]:
    occupancy = rasterize_uv_occupancy(target.data, BAKE_SIZE)
    base = image_pixels(images["basecolor"])
    coverage = image_pixels(images["coverage"])
    occupied_count = sum(occupancy)
    covered_count = 0
    uncovered_count = 0
    cyan_count = 0
    for pixel, is_occupied in enumerate(occupancy):
        if not is_occupied:
            continue
        offset = pixel * 4
        is_covered = max(
            coverage[offset],
            coverage[offset + 1],
            coverage[offset + 2],
        ) >= 0.5
        if not is_covered:
            uncovered_count += 1
            continue
        covered_count += 1
        red = base[offset]
        green = base[offset + 1]
        blue = base[offset + 2]
        # Explicit transfer-contamination gate: cyan/teal energy from the provider
        # should not silently become general-purpose armor colour.
        if (
            blue >= 0.35
            and green >= 0.25
            and blue >= red * 1.35
            and green >= red * 1.25
            and abs(blue - green) <= 0.45
        ):
            cyan_count += 1

    total = BAKE_SIZE * BAKE_SIZE
    return {
        "uvAtlas": {
            "totalPixels": total,
            "occupiedPixels": occupied_count,
            "occupiedPercent": (
                occupied_count / total * 100 if total else 0.0
            ),
        },
        "coverage": {
            "coveredOccupiedPixels": covered_count,
            "uncoveredOccupiedPixels": uncovered_count,
            "uncoveredPercentOfOccupied": (
                uncovered_count / occupied_count * 100 if occupied_count else 100.0
            ),
            "threshold": "coverage RGB max >= 0.5",
        },
        "cyanContamination": {
            "cyanCoveredPixels": cyan_count,
            "cyanPercentOfCovered": (
                cyan_count / covered_count * 100 if covered_count else 0.0
            ),
            "classification": (
                "B>=0.35, G>=0.25, B>=1.35R, G>=1.25R, |B-G|<=0.45 "
                "in baked image working values"
            ),
        },
    }


def srgb(hex_color: str) -> tuple[float, float, float, float]:
    value = hex_color.removeprefix("#")
    return tuple(int(value[index : index + 2], 16) / 255 for index in (0, 2, 4)) + (
        1.0,
    )


def simple_material(
    name: str,
    base: str,
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
    principled.inputs["Base Color"].default_value = srgb(base)
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    return material


def build_baked_material(
    material: bpy.types.Material,
    images: dict[str, bpy.types.Image],
) -> None:
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])

    base = nodes.new("ShaderNodeTexImage")
    base.image = images["basecolor"]
    base.label = "Baked base colour"
    links.new(base.outputs["Color"], principled.inputs["Base Color"])

    normal_texture = nodes.new("ShaderNodeTexImage")
    normal_texture.image = images["normal"]
    normal_texture.label = "Baked tangent normal"
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 1.0
    links.new(normal_texture.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])

    roughness = nodes.new("ShaderNodeTexImage")
    roughness.image = images["roughness"]
    roughness.label = "Baked roughness"
    links.new(roughness.outputs["Color"], principled.inputs["Roughness"])

    metallic = nodes.new("ShaderNodeTexImage")
    metallic.image = images["metallic"]
    metallic.label = "Baked metallic"
    links.new(metallic.outputs["Color"], principled.inputs["Metallic"])


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def link_only(
    obj: bpy.types.Object,
    collection: bpy.types.Collection,
) -> None:
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def add_review_floor(
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, -0.012))
    floor = bpy.context.active_object
    floor.name = "EVAL.REVIEW.Floor"
    floor.dimensions = (3.2, 3.2, 0.018)
    set_active(floor)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    floor.data.materials.append(
        simple_material("EVAL.REVIEW.FloorMaterial", "#252c36", 0.42, 0.72)
    )
    floor["evaluationOnly"] = True
    link_only(floor, collection)
    return floor


def configure_exact_mobile_family_review(
    collection: bpy.types.Collection,
) -> tuple[bpy.types.Object, list[bpy.types.Object], dict[str, object]]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = RENDER_SIZE
    scene.render.resolution_y = RENDER_SIZE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -1.15

    world = bpy.data.worlds.new("EVAL.AegisMobileFamilyWorld")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = srgb("#0b1020")
    background.inputs["Strength"].default_value = 0.45
    scene.world = world

    lights_spec = (
        ("Key", (-1.6, -1.8, 2.9), 320, 3.0, (0.90, 0.95, 1.0), (0, 0, 0.12)),
        ("Fill", (1.8, 0.8, 1.1), 135, 2.2, (0.34, 0.48, 0.72), (0, 0, 0.12)),
        ("Rim", (-0.8, 1.8, 1.8), 190, 1.5, (0.86, 0.64, 0.36), (0, 0, 0.15)),
        (
            "SideFill",
            (0, -2.0, 1.25),
            125,
            2.0,
            (0.72, 0.82, 1.0),
            (0, 0, 0.11),
        ),
    )
    created: list[bpy.types.Object] = []
    for name, location, energy, size, color, target in lights_spec:
        light_data = bpy.data.lights.new(f"EVAL.REVIEW.{name}", "AREA")
        light_data.energy = energy
        light_data.shape = "DISK"
        light_data.size = size
        light_data.color = color
        light = bpy.data.objects.new(f"EVAL.REVIEW.{name}", light_data)
        light.location = location
        look_at(light, Vector(target))
        collection.objects.link(light)
        created.append(light)

    camera_data = bpy.data.cameras.new("EVAL.REVIEW.Camera")
    camera_data.type = "PERSP"
    camera_data.angle = math.radians(42)
    camera = bpy.data.objects.new("EVAL.REVIEW.Camera", camera_data)
    distance = 1.88
    pitch = math.radians(58)
    camera.location = (
        -0.08,
        math.cos(pitch) * distance,
        math.sin(pitch) * distance,
    )
    look_at(camera, Vector((0.02, 0, 0.13)))
    collection.objects.link(camera)
    scene.camera = camera
    created.append(camera)

    return camera, created, {
        "renderer": "Blender EEVEE",
        "resolution": [RENDER_SIZE, RENDER_SIZE],
        "viewTransformLook": "AgX - Medium High Contrast",
        "exposure": -1.15,
        "world": {"color": "#0b1020", "strength": 0.45},
        "floor": {
            "color": "#252c36",
            "metallic": 0.42,
            "roughness": 0.72,
        },
        "camera": {
            "fovDegrees": 42,
            "distance": distance,
            "pitchDegrees": 58,
            "location": vector_values(camera.location),
            "target": [0.02, 0, 0.13],
        },
        "lights": [
            {
                "name": name,
                "location": list(location),
                "energy": energy,
                "size": size,
                "color": list(color),
                "target": list(target),
            }
            for name, location, energy, size, color, target in lights_spec
        ],
    }


def render_baked_target(
    target: bpy.types.Object,
    images: dict[str, bpy.types.Image],
    collection: bpy.types.Collection,
    output_dir: Path,
) -> tuple[dict[str, object], dict[str, object]]:
    target_material = target.data.materials[0]
    build_baked_material(target_material, images)
    floor = add_review_floor(collection)
    _camera, review_objects, review_spec = configure_exact_mobile_family_review(
        collection
    )

    visible = {target, floor, *review_objects}
    for obj in bpy.context.scene.objects:
        obj.hide_render = obj not in visible
    target.hide_set(False)
    target.hide_render = False
    floor.hide_render = False

    output = output_dir / "baked-58deg.png"
    bpy.context.scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    artifact = {
        "file": output.name,
        "bytes": output.stat().st_size,
        "sha256": sha256(output),
        "dimensions": [RENDER_SIZE, RENDER_SIZE],
    }
    return artifact, review_spec


def main() -> None:
    args = parse_args()
    asset_id = str(args.asset_id)
    specification = ASSETS[asset_id]
    output_dir = args.output_dir.resolve()
    source_blend, source_report = verify_environment(asset_id, output_dir)
    raw_glb, provider_report = verify_provider(
        args.artifacts_root.resolve(),
        asset_id,
    )
    output_dir.mkdir(parents=True, exist_ok=True)

    rig = bpy.data.objects[str(specification["rig"])]
    set_idle_pose(rig, str(specification["idleAction"]))
    source_meshes = source_model_meshes(rig)
    evaluation_collection = ensure_evaluation_collection()
    target, target_report = duplicate_static_target(
        rig,
        source_meshes,
        evaluation_collection,
    )
    target_normalization = normalize_target(target)
    triangle_ceiling = (
        int(args.target_triangles)
        if args.target_triangles is not None
        else int(specification["targetTriangles"])
    )
    low_report = apply_low_target(target, triangle_ceiling)
    uv_report = create_smart_uv_atlas(target)
    target_topology = topology_hash(target.data)
    cage, cage_report = create_explicit_cage(
        target,
        evaluation_collection,
        float(args.cage_distance),
    )

    provider_meshes = import_provider(raw_glb)
    provider_normalization = normalize_provider(
        provider_meshes,
        float(specification["providerYawDegrees"]),
    )
    images, _target_material, bake_report = bake_maps(
        provider_meshes,
        target,
        cage,
        output_dir,
        float(args.max_ray_distance),
    )
    metrics = surface_metrics(target, images)
    render_report, review_spec = render_baked_target(
        target,
        images,
        evaluation_collection,
        output_dir,
    )

    report = {
        "schemaVersion": SCHEMA_VERSION,
        "assetId": asset_id,
        "verdict": "evaluation-only-not-runtime",
        "evaluator": {
            "file": Path(__file__).name,
            "sha256": sha256(Path(__file__).resolve()),
            "blenderRequired": REQUIRED_BLENDER,
            "blenderActual": bpy.app.version_string,
            "paidCalls": 0,
            "sourceBlendSaved": False,
        },
        "source": source_report,
        "provider": provider_report,
        "authorityTransform": {
            "provider": provider_normalization,
            "authoredTarget": target_normalization,
        },
        "staticLowTarget": {
            **target_report,
            **low_report,
            "idleAction": specification["idleAction"],
            "idleFrame": 1,
            "topologyAndUvSha256": target_topology,
            "sourcePolicy": (
                "only non-REVIEW meshes parented beneath the expected authored rig "
                "were evaluated before provider import"
            ),
        },
        "uvAtlas": uv_report,
        "cage": cage_report,
        "bake": bake_report,
        "metrics": metrics,
        "review58Degrees": {
            "artifact": render_report,
            "rig": review_spec,
        },
        "outputs": {
            "directory": output_dir.name,
            "sourceOrCandidateModified": False,
        },
        "limitations": [
            (
                "Provider texture semantics are spatially tied to an inferred provider "
                "shape; this transfer is diagnostic evidence, not an accepted authored skin."
            ),
            (
                "Independent authority normalization establishes scale, centre, floor, "
                "and yaw but cannot establish semantic correspondence between unlike panels."
            ),
            (
                "The joined target intentionally discards rig and material partitions; "
                "no evaluation object is suitable for runtime installation."
            ),
            (
                "Coverage and cyan contamination are texture-space diagnostics; the "
                "retained 58-degree render remains the perceptual family gate."
            ),
        ],
    }
    write_json(output_dir / "surface-transfer-report.json", report)
    print(
        json.dumps(
            {
                "assetId": asset_id,
                "report": str(output_dir / "surface-transfer-report.json"),
                "uncoveredPercent": metrics["coverage"][
                    "uncoveredPercentOfOccupied"
                ],
                "cyanPercent": metrics["cyanContamination"][
                    "cyanPercentOfCovered"
                ],
                "renderSha256": render_report["sha256"],
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
