#!/usr/bin/env python3
"""Apply the inspected B2 quarter-turn without changing its authored design.

Run with the pinned Blender 5.2 toolchain:

  Blender --background --factory-startup \
    --python normalize_b2_orientation.py -- \
    --input /absolute/path/to/raw-model.glb \
    --output-dir /absolute/repo/var/class-models/topology-experiments/\
b2-orientation/build-a

The source is read-only. The output directory must be a new child of the
ignored B2 orientation root. The script writes ``corrected.glb`` and a
sanitized ``normalization-report.json``.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import struct
import sys
import unicodedata
from pathlib import Path
from typing import Any, Iterable

import bpy
from mathutils import Matrix, Vector


EXPECTED_BLENDER = (5, 2)
EXPECTED_SOURCE_SHA256 = (
    "6fd79e66b0211241c2ef1e1f6b048634d39074f81cd1e4b4a535e97629b7e05f"
)
ROTATION_AXIS = "+X"
ROTATION_DEGREES = -90
TARGET_PLANFORM_SPAN = 1.0
BOUNDS_EPSILON = 1.0e-6
OUTPUT_MODEL_NAME = "corrected.glb"
OUTPUT_REPORT_NAME = "normalization-report.json"


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(
        description=(
            "Offline Blender 5.2 B2 axis correction. Applies only the frozen "
            "-90 degree X quarter-turn plus uniform center/floor/scale."
        ),
        epilog=(
            "Inspection basis: the approved cross is visible from the frozen "
            "audit's -Y side camera while +Z top shows its edge. Therefore "
            "the authored outward top normal is -Y; a right-handed -90 "
            "degree rotation around +X maps that normal to +Z."
        ),
    )
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    return parser.parse_args(argv)


def sanitized(value: Any) -> str:
    normalized = unicodedata.normalize("NFKC", str(value))
    return "".join(
        character
        for character in normalized
        if character in ("\t", " ")
        or not unicodedata.category(character).startswith("C")
    )[:160]


def rounded(value: float) -> float:
    result = round(float(value), 9)
    return 0.0 if result == -0.0 else result


def vector_json(value: Iterable[float]) -> list[float]:
    return [rounded(component) for component in value]


def matrix_json(value: Matrix) -> list[list[float]]:
    return [vector_json(row) for row in value]


def inspected_quarter_turn_dcc() -> Matrix:
    """Exact right-handed -90 degree rotation around Blender +X."""
    return Matrix(
        (
            (1, 0, 0, 0),
            (0, 0, 1, 0),
            (0, -1, 0, 0),
            (0, 0, 0, 1),
        )
    )


def gltf_to_dcc_basis() -> Matrix:
    """Exact glTF +Y-up to Blender +Z-up basis used by the importer."""
    return Matrix(
        (
            (1, 0, 0, 0),
            (0, 0, -1, 0),
            (0, 1, 0, 0),
            (0, 0, 0, 1),
        )
    )


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def bytes_sha256(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def script_repo_root() -> Path:
    return Path(__file__).resolve().parents[4]


def validate_paths(
    source: Path,
    output_dir: Path,
) -> tuple[Path, Path]:
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise ValueError(
            "This normalization is pinned to Blender "
            f"{EXPECTED_BLENDER[0]}.{EXPECTED_BLENDER[1]}.x"
        )
    if not source.is_absolute() or not output_dir.is_absolute():
        raise ValueError("--input and --output-dir must be absolute paths")
    source = source.resolve(strict=True)
    output_dir = output_dir.resolve(strict=False)
    if not source.is_file() or source.suffix.lower() != ".glb":
        raise ValueError("--input must be an existing GLB")
    if sha256(source) != EXPECTED_SOURCE_SHA256:
        raise ValueError("Source SHA-256 does not match the frozen B2 master")
    if output_dir.exists():
        raise ValueError("--output-dir must not exist; outputs are never overwritten")

    repo_root = script_repo_root()
    output_root = (
        repo_root / "var/class-models/topology-experiments/b2-orientation"
    ).resolve(strict=False)
    if output_dir == output_root or output_root not in output_dir.parents:
        raise ValueError(
            "--output-dir must be a new child of the ignored B2 orientation root"
        )
    for ancestor in (output_dir, *output_dir.parents):
        if ancestor.exists() and ancestor.is_symlink():
            raise ValueError("--output-dir may not traverse an existing symlink")
    output_dir.mkdir(parents=True, exist_ok=False)
    return source, output_dir


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


def import_source(path: Path) -> tuple[list[bpy.types.Object], bpy.types.Object]:
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    if len(imported) != 1 or len(meshes) != 1:
        raise ValueError(
            "Frozen B2 contract requires exactly one imported mesh object"
        )
    if any(obj.type in {"CAMERA", "LIGHT", "ARMATURE"} for obj in imported):
        raise ValueError("Frozen B2 source unexpectedly contains rig or scene data")
    bpy.context.view_layer.update()
    return imported, meshes[0]


def world_points(mesh_object: bpy.types.Object) -> list[Vector]:
    matrix = mesh_object.matrix_world
    return [matrix @ vertex.co for vertex in mesh_object.data.vertices]


def bounds(points: Iterable[Vector]) -> tuple[Vector, Vector]:
    points = list(points)
    if not points:
        raise ValueError("Cannot normalize an empty mesh")
    return (
        Vector(
            tuple(min(point[axis] for point in points) for axis in range(3))
        ),
        Vector(
            tuple(max(point[axis] for point in points) for axis in range(3))
        ),
    )


def bounds_json(minimum: Vector, maximum: Vector) -> dict[str, Any]:
    span = maximum - minimum
    return {
        "minimum": vector_json(minimum),
        "maximum": vector_json(maximum),
        "span": vector_json(span),
        "center": vector_json((minimum + maximum) * 0.5),
    }


def update_float(digest: Any, value: float) -> None:
    digest.update(struct.pack("<d", float(value)))


def local_geometry_fingerprint(mesh_object: bpy.types.Object) -> str:
    mesh = mesh_object.data
    digest = hashlib.sha256()
    digest.update(
        struct.pack(
            "<III",
            len(mesh.vertices),
            len(mesh.edges),
            len(mesh.polygons),
        )
    )
    for vertex in mesh.vertices:
        for component in vertex.co:
            update_float(digest, component)
    for edge in mesh.edges:
        digest.update(struct.pack("<II", *edge.vertices))
    for polygon in mesh.polygons:
        digest.update(struct.pack("<I", len(polygon.vertices)))
        for index in polygon.vertices:
            digest.update(struct.pack("<I", index))
    return digest.hexdigest()


def uv_fingerprint(mesh_object: bpy.types.Object) -> str:
    digest = hashlib.sha256()
    layers = mesh_object.data.uv_layers
    digest.update(struct.pack("<I", len(layers)))
    for layer in layers:
        name = sanitized(layer.name).encode("utf-8")
        digest.update(struct.pack("<I", len(name)))
        digest.update(name)
        digest.update(struct.pack("<I", len(layer.data)))
        for item in layer.data:
            for component in item.uv:
                update_float(digest, component)
    return digest.hexdigest()


def material_summary(mesh_object: bpy.types.Object) -> list[dict[str, Any]]:
    summaries: list[dict[str, Any]] = []
    for slot_index, slot in enumerate(mesh_object.material_slots):
        material = slot.material
        if material is None:
            summaries.append({"slot": slot_index, "name": None})
            continue
        node_tree = material.node_tree
        texture_nodes = []
        if node_tree:
            for node in sorted(
                (
                    node
                    for node in node_tree.nodes
                    if node.type == "TEX_IMAGE" and node.image is not None
                ),
                key=lambda node: sanitized(node.name),
            ):
                image = node.image
                packed = bytes(image.packed_file.data) if image.packed_file else b""
                texture_nodes.append(
                    {
                        "node": sanitized(node.name),
                        "image": sanitized(image.name),
                        "size": [int(image.size[0]), int(image.size[1])],
                        "channels": int(image.channels),
                        "packedSha256": bytes_sha256(packed) if packed else None,
                        "colorSpace": sanitized(image.colorspace_settings.name),
                    }
                )
        summaries.append(
            {
                "slot": slot_index,
                "name": sanitized(material.name),
                "doubleSided": not bool(material.use_backface_culling),
                "nodeCount": len(node_tree.nodes) if node_tree else 0,
                "textureNodes": texture_nodes,
            }
        )
    return summaries


def mesh_metrics(mesh_object: bpy.types.Object) -> dict[str, Any]:
    mesh = mesh_object.data
    mesh.calc_loop_triangles()
    minimum, maximum = bounds(world_points(mesh_object))
    return {
        "objectName": sanitized(mesh_object.name),
        "meshName": sanitized(mesh.name),
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "faces": len(mesh.polygons),
        "triangles": len(mesh.loop_triangles),
        "uvLayers": len(mesh.uv_layers),
        "materialSlots": len(mesh_object.material_slots),
        "boundsWorldDcc": bounds_json(minimum, maximum),
        "localGeometryFingerprint": local_geometry_fingerprint(mesh_object),
        "uvFingerprint": uv_fingerprint(mesh_object),
        "materials": material_summary(mesh_object),
    }


def normalization_transform(
    mesh_object: bpy.types.Object,
) -> tuple[Matrix, dict[str, Any]]:
    source_minimum, source_maximum = bounds(world_points(mesh_object))
    rotation = inspected_quarter_turn_dcc()
    rotated_points = [
        rotation @ point for point in world_points(mesh_object)
    ]
    rotated_minimum, rotated_maximum = bounds(rotated_points)
    rotated_span = rotated_maximum - rotated_minimum
    planform_span = max(rotated_span.x, rotated_span.y)
    if planform_span <= 0:
        raise ValueError("B2 has zero planform span after quarter-turn")
    scale = TARGET_PLANFORM_SPAN / planform_span
    center = (rotated_minimum + rotated_maximum) * 0.5
    transform = (
        Matrix.Translation(
            Vector(
                (
                    -center.x * scale,
                    -center.y * scale,
                    -rotated_minimum.z * scale,
                )
            )
        )
        @ Matrix.Scale(scale, 4)
        @ rotation
    )
    expected_points = [
        transform @ point for point in world_points(mesh_object)
    ]
    expected_minimum, expected_maximum = bounds(expected_points)
    return transform, {
        "sourceBoundsWorldDcc": bounds_json(source_minimum, source_maximum),
        "afterQuarterTurnBeforePlacement": bounds_json(
            rotated_minimum, rotated_maximum
        ),
        "normalizedBoundsWorldDcc": bounds_json(
            expected_minimum, expected_maximum
        ),
        "rightHandedRotation": {
            "axis": ROTATION_AXIS,
            "degrees": ROTATION_DEGREES,
            "matrix": matrix_json(rotation),
            "sourceApprovedSurfaceNormal": [0, -1, 0],
            "targetApprovedSurfaceNormal": [0, 0, 1],
        },
        "uniformScale": rounded(scale),
        "targetPlanformSpan": TARGET_PLANFORM_SPAN,
        "horizontalCentering": "rotated X/Y AABB center to origin",
        "floorPlacement": "rotated minimum Z to zero",
        "combinedWorldMatrix": matrix_json(transform),
    }


def read_glb(
    path: Path,
) -> tuple[dict[str, Any], list[tuple[int, bytes]]]:
    with path.open("rb") as stream:
        magic, version, declared_length = struct.unpack("<4sII", stream.read(12))
        if magic != b"glTF" or version != 2 or declared_length != path.stat().st_size:
            raise ValueError("Input is not a valid GLB 2.0 file")
        chunks: list[tuple[int, bytes]] = []
        document: dict[str, Any] | None = None
        while stream.tell() < declared_length:
            chunk_length, chunk_type = struct.unpack("<II", stream.read(8))
            payload = stream.read(chunk_length)
            if len(payload) != chunk_length:
                raise ValueError("Truncated GLB chunk")
            if chunk_type == 0x4E4F534A:
                if document is not None:
                    raise ValueError("GLB has more than one JSON chunk")
                document = json.loads(
                    payload.rstrip(b" \t\r\n\x00").decode("utf-8")
                )
            else:
                chunks.append((chunk_type, payload))
    if document is None:
        raise ValueError("GLB has no JSON chunk")
    return document, chunks


def glb_document_counts(document: dict[str, Any]) -> dict[str, int]:
    meshes = document.get("meshes", [])
    return {
        "nodes": len(document.get("nodes", [])),
        "meshes": len(meshes),
        "primitives": sum(len(mesh.get("primitives", [])) for mesh in meshes),
        "materials": len(document.get("materials", [])),
        "images": len(document.get("images", [])),
        "textures": len(document.get("textures", [])),
        "animations": len(document.get("animations", [])),
        "skins": len(document.get("skins", [])),
        "cameras": len(document.get("cameras", [])),
    }


def gltf_column_major(matrix: Matrix) -> list[float]:
    return [
        rounded(matrix[row][column])
        for column in range(4)
        for row in range(4)
    ]


def is_identity_node(node: dict[str, Any]) -> bool:
    matrix = node.get("matrix")
    if matrix is not None:
        identity = gltf_column_major(Matrix.Identity(4))
        return len(matrix) == 16 and all(
            abs(float(left) - right) <= BOUNDS_EPSILON
            for left, right in zip(matrix, identity)
        )
    return (
        node.get("translation", [0, 0, 0]) == [0, 0, 0]
        and node.get("rotation", [0, 0, 0, 1]) == [0, 0, 0, 1]
        and node.get("scale", [1, 1, 1]) == [1, 1, 1]
    )


def write_lossless_transformed_glb(
    source: Path,
    output: Path,
    dcc_transform: Matrix,
) -> dict[str, Any]:
    document, non_json_chunks = read_glb(source)
    mesh_nodes = [
        (index, node)
        for index, node in enumerate(document.get("nodes", []))
        if "mesh" in node
    ]
    if len(mesh_nodes) != 1 or mesh_nodes[0][1].get("mesh") != 0:
        raise ValueError("Frozen B2 GLB must have one node referencing mesh zero")
    node_index, node = mesh_nodes[0]
    if not is_identity_node(node):
        raise ValueError("Frozen B2 mesh node is no longer identity-transformed")

    # Blender imports glTF +Y-up as DCC +Z-up with +Z glTF becoming -Y DCC.
    # Conjugating by that exact basis writes the inspected DCC correction back
    # into the provider document without touching a binary accessor or image.
    gltf_to_dcc = gltf_to_dcc_basis()
    dcc_to_gltf = gltf_to_dcc.inverted()
    gltf_transform = dcc_to_gltf @ dcc_transform @ gltf_to_dcc
    node.pop("translation", None)
    node.pop("rotation", None)
    node.pop("scale", None)
    node["matrix"] = gltf_column_major(gltf_transform)

    json_payload = json.dumps(
        document,
        ensure_ascii=True,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    json_payload += b" " * ((-len(json_payload)) % 4)
    chunks = [(0x4E4F534A, json_payload), *non_json_chunks]
    total_length = 12 + sum(8 + len(payload) for _kind, payload in chunks)
    with output.open("wb") as stream:
        stream.write(struct.pack("<4sII", b"glTF", 2, total_length))
        for chunk_type, payload in chunks:
            stream.write(struct.pack("<II", len(payload), chunk_type))
            stream.write(payload)

    output_document, output_non_json_chunks = read_glb(output)
    if output_document != document:
        raise ValueError("Written GLB JSON did not round-trip")
    if output_non_json_chunks != non_json_chunks:
        raise ValueError("Lossless GLB patch changed a binary chunk")
    return {
        "meshNodeIndex": node_index,
        "gltfYUpMatrixColumnMajor": node["matrix"],
        "gltfYUpMatrixRows": matrix_json(gltf_transform),
        "nonJsonChunkCount": len(non_json_chunks),
        "sourceNonJsonChunkSha256": [
            bytes_sha256(payload) for _kind, payload in non_json_chunks
        ],
        "outputNonJsonChunkSha256": [
            bytes_sha256(payload) for _kind, payload in output_non_json_chunks
        ],
        "binaryChunksByteIdentical": output_non_json_chunks == non_json_chunks,
    }


def assert_bounds(
    actual: dict[str, Any],
    expected: dict[str, Any],
) -> None:
    for field in ("minimum", "maximum", "span"):
        for actual_value, expected_value in zip(actual[field], expected[field]):
            if abs(actual_value - expected_value) > BOUNDS_EPSILON:
                raise ValueError(
                    f"Exported {field} differs from expected normalized bounds"
                )


def normalize(
    source: Path,
    output_dir: Path,
) -> dict[str, Any]:
    clear_scene()
    imported, mesh_object = import_source(source)
    source_metrics = mesh_metrics(mesh_object)
    transform, transform_report = normalization_transform(mesh_object)

    imported_set = set(imported)
    roots = [obj for obj in imported if obj.parent not in imported_set]
    for root in roots:
        root.matrix_world = transform @ root.matrix_world
    bpy.context.view_layer.update()

    transformed_metrics = mesh_metrics(mesh_object)
    for field in (
        "vertices",
        "edges",
        "faces",
        "triangles",
        "uvLayers",
        "materialSlots",
        "localGeometryFingerprint",
        "uvFingerprint",
        "materials",
    ):
        if transformed_metrics[field] != source_metrics[field]:
            raise ValueError(f"Axis correction changed preserved field: {field}")
    assert_bounds(
        transformed_metrics["boundsWorldDcc"],
        transform_report["normalizedBoundsWorldDcc"],
    )

    output_model = output_dir / OUTPUT_MODEL_NAME
    glb_patch = write_lossless_transformed_glb(
        source,
        output_model,
        transform,
    )
    output_hash = sha256(output_model)
    output_document, _output_chunks = read_glb(output_model)

    clear_scene()
    _output_imported, output_mesh = import_source(output_model)
    output_metrics = mesh_metrics(output_mesh)
    assert_bounds(
        output_metrics["boundsWorldDcc"],
        transform_report["normalizedBoundsWorldDcc"],
    )
    for field in (
        "vertices",
        "faces",
        "triangles",
        "uvLayers",
        "materialSlots",
    ):
        if output_metrics[field] != source_metrics[field]:
            raise ValueError(f"Export/reimport changed preserved field: {field}")

    return {
        "schema": "nilbots-b2-orientation-normalization-1",
        "blender": {
            "version": bpy.app.version_string,
            "pinnedMajorMinor": list(EXPECTED_BLENDER),
        },
        "input": {
            "fileName": source.name,
            "sha256": EXPECTED_SOURCE_SHA256,
            "bytes": source.stat().st_size,
            "unchangedAfterRun": sha256(source) == EXPECTED_SOURCE_SHA256,
        },
        "inspection": {
            "frozenAudit": "sandbox/topology-audits/t2-top-b2-audit",
            "approvedCrossVisibleIn": "side.png",
            "edgeProfileVisibleIn": "top.png",
            "sourceDccCrossPlane": "X/Z",
            "sourceDccThicknessAxis": "Y",
            "sourceApprovedSurfaceNormal": "-Y",
            "targetDccCrossPlane": "X/Y",
            "targetDccUp": "+Z",
            "operationClassification": "axis correction only; no design inference",
        },
        "sourceMetrics": source_metrics,
        "transform": transform_report,
        "preservation": {
            "worldTransformOnlyBeforeExport": True,
            "localGeometryFingerprintUnchanged": (
                transformed_metrics["localGeometryFingerprint"]
                == source_metrics["localGeometryFingerprint"]
            ),
            "uvFingerprintUnchanged": (
                transformed_metrics["uvFingerprint"]
                == source_metrics["uvFingerprint"]
            ),
            "materialSummaryUnchanged": (
                transformed_metrics["materials"] == source_metrics["materials"]
            ),
            "topologyCountsUnchanged": all(
                transformed_metrics[field] == source_metrics[field]
                for field in ("vertices", "edges", "faces", "triangles")
            ),
        },
        "output": {
            "fileName": OUTPUT_MODEL_NAME,
            "sha256": output_hash,
            "bytes": output_model.stat().st_size,
            "relativeStorageClass": (
                "var/class-models/topology-experiments/b2-orientation/"
                "<build>/corrected.glb"
            ),
            "glbDocumentCounts": glb_document_counts(output_document),
            "losslessNodeMatrixPatch": glb_patch,
            "reimportMetrics": output_metrics,
        },
        "export": {
            "format": "GLB",
            "method": (
                "Blender-inspected DCC transform conjugated into glTF +Y-up "
                "and written as the sole mesh node matrix"
            ),
            "jsonChangeScope": "one mesh-node matrix only",
            "binaryAccessorsAndEmbeddedImagesByteIdentical": glb_patch[
                "binaryChunksByteIdentical"
            ],
            "geometryRedesigned": False,
            "uvsModified": False,
            "materialsModified": False,
            "pbrTexturesModified": False,
        },
    }


def main() -> int:
    args = parse_args()
    source, output_dir = validate_paths(args.input, args.output_dir)
    report = normalize(source, output_dir)
    report_path = output_dir / OUTPUT_REPORT_NAME
    report_path.write_text(
        json.dumps(report, indent=2, sort_keys=True, ensure_ascii=True) + "\n",
        encoding="utf-8",
    )
    print(
        json.dumps(
            {
                "model": OUTPUT_MODEL_NAME,
                "modelSha256": report["output"]["sha256"],
                "report": OUTPUT_REPORT_NAME,
                "sourceUnchanged": report["input"]["unchangedAfterRun"],
                "transform": "-90 degrees around +X",
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    try:
        main()
    except SystemExit as error:
        code = error.code if isinstance(error.code, int) else 1
        if code:
            sys.stderr.flush()
            os._exit(code)
    except Exception as error:
        print(
            f"B2 orientation normalization failed: {sanitized(error)}",
            file=sys.stderr,
            flush=True,
        )
        os._exit(2)
