"""Build deterministic normalized Lattice Loom review derivatives in Blender.

Run with the pinned portable Blender binary from the repository root:

  blender --background --factory-startup \
    --python scripts/class-models/build-lattice-loom.py -- \
    --source var/model-artifacts/meshy/lattice-loom/<task-id>/raw-model.glb \
    --output-dir sandbox/class-model-builds/lattice-loom

The provider GLB is an immutable, fused reconstruction master. This recipe
performs only the measured fixed facing correction, a small fork-local
extension, one uniform world normalization, deterministic decimation, PBR map
resizing, and plain glTF export. Semantic team-material extraction is a
separate offline step because the provider mesh has one fused material.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

import bpy
from mathutils import Matrix


SOURCE_SHA256 = "0cd9084e14355323b46194f84e9a406cb07fa19f69320c62dce263223dbd0b6f"
SOURCE_TRIANGLES = 967_724
TARGET_PLANFORM_SPAN = 1.08
RICH_TRIANGLES = 150_000
LEAN_TRIANGLES = 75_000
RICH_TEXTURE_SIZE = 2048
LEAN_TEXTURE_SIZE = 1024

# The approved concept audit found only the two east fork tips short. Grow the
# outer two prongs from their own roots; the central nose, rear half, height,
# width, and every other vertex remain untouched. This is intentionally not a
# whole-model anisotropic scale.
FORK_START_FRACTION = 0.48
FORK_SIDE_FRACTION = 0.12
FORK_LOCAL_EXTENSION = 0.12


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    return parser.parse_args(raw)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for item in list(collection):
            if item.users == 0:
                collection.remove(item)


def mesh_object() -> bpy.types.Object:
    meshes = [item for item in bpy.context.scene.objects if item.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one provider mesh, found {len(meshes)}.")
    return meshes[0]


def triangle_count(obj: bpy.types.Object) -> int:
    return sum(len(poly.loop_indices) - 2 for poly in obj.data.polygons)


def bounds(obj: bpy.types.Object) -> tuple[tuple[float, float, float], tuple[float, float, float]]:
    coordinates = [vertex.co for vertex in obj.data.vertices]
    return (
        tuple(min(co[index] for co in coordinates) for index in range(3)),
        tuple(max(co[index] for co in coordinates) for index in range(3)),
    )


def normalize_master(obj: bpy.types.Object) -> dict[str, object]:
    mesh = obj.data

    # The audited provider nose is -X. Blender has already converted glTF +Y up
    # to Blender +Z, so only the fixed yaw belongs here.
    mesh.transform(Matrix.Rotation(math.pi, 4, "Z"))
    mesh.update()

    before_min, before_max = bounds(obj)
    center_x = (before_min[0] + before_max[0]) / 2
    center_y = (before_min[1] + before_max[1]) / 2
    positive_half = before_max[0] - center_x
    side_half = (before_max[1] - before_min[1]) / 2
    fork_start = center_x + positive_half * FORK_START_FRACTION
    fork_side = side_half * FORK_SIDE_FRACTION
    edited_vertices = 0
    maximum_extension = 0.0
    for vertex in mesh.vertices:
        if vertex.co.x <= fork_start or abs(vertex.co.y - center_y) <= fork_side:
            continue
        extension = (vertex.co.x - fork_start) * FORK_LOCAL_EXTENSION
        vertex.co.x += extension
        maximum_extension = max(maximum_extension, extension)
        edited_vertices += 1
    mesh.update()

    corrected_min, corrected_max = bounds(obj)
    center_x = (corrected_min[0] + corrected_max[0]) / 2
    center_y = (corrected_min[1] + corrected_max[1]) / 2
    span = max(
        corrected_max[0] - corrected_min[0],
        corrected_max[1] - corrected_min[1],
    )
    uniform_scale = TARGET_PLANFORM_SPAN / span
    floor_z = corrected_min[2]
    for vertex in mesh.vertices:
        vertex.co.x = (vertex.co.x - center_x) * uniform_scale
        vertex.co.y = (vertex.co.y - center_y) * uniform_scale
        vertex.co.z = (vertex.co.z - floor_z) * uniform_scale
    mesh.update()

    obj.name = "LatticeLoom"
    mesh.name = "LatticeLoom"
    obj.location = (0, 0, 0)
    obj.rotation_euler = (0, 0, 0)
    obj.scale = (1, 1, 1)

    if len(obj.material_slots) != 1 or obj.material_slots[0].material is None:
        raise RuntimeError("Expected one fused provider PBR material.")
    material = obj.material_slots[0].material
    material.name = "Nilbots Hull Source"
    material.use_backface_culling = True

    normalized_min, normalized_max = bounds(obj)
    floor_error = abs(normalized_min[2])
    normalized_span = max(
        normalized_max[0] - normalized_min[0],
        normalized_max[1] - normalized_min[1],
    )
    if floor_error > 1e-6 or abs(normalized_span - TARGET_PLANFORM_SPAN) > 1e-6:
        raise RuntimeError(
            f"Normalization drifted: floor={normalized_min[2]}, span={normalized_span}."
        )

    return {
        "sourceFacing": "-X",
        "runtimeFacing": "+X",
        "blenderUp": "+Z",
        "runtimeUp": "+Y",
        "yawDegrees": 180,
        "forkCorrection": {
            "editedVertices": edited_vertices,
            "maximumSourceSpaceExtension": maximum_extension,
            "startFractionOfForwardHalf": FORK_START_FRACTION,
            "minimumSideFractionOfHalfWidth": FORK_SIDE_FRACTION,
            "localExtensionFactor": FORK_LOCAL_EXTENSION,
        },
        "uniformScale": uniform_scale,
        "boundsMin": normalized_min,
        "boundsMax": normalized_max,
        "maxPlanformSpan": normalized_span,
    }


def decimate_to(obj: bpy.types.Object, target_triangles: int) -> int:
    current = triangle_count(obj)
    if current <= target_triangles:
        return current
    modifier = obj.modifiers.new(name=f"Runtime {target_triangles} tris", type="DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = target_triangles / current
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.data.update()
    result = triangle_count(obj)
    if result > target_triangles + 8:
        raise RuntimeError(
            f"Decimation exceeded target: expected <= {target_triangles}, got {result}."
        )
    return result


def enforce_coordinate_contract(obj: bpy.types.Object) -> dict[str, tuple[float, float, float] | float]:
    """Remove the tiny rigid drift introduced by edge collapses."""
    minimum, maximum = bounds(obj)
    center_x = (minimum[0] + maximum[0]) / 2
    center_y = (minimum[1] + maximum[1]) / 2
    span = max(maximum[0] - minimum[0], maximum[1] - minimum[1])
    scale = TARGET_PLANFORM_SPAN / span
    for vertex in obj.data.vertices:
        vertex.co.x = (vertex.co.x - center_x) * scale
        vertex.co.y = (vertex.co.y - center_y) * scale
        vertex.co.z = (vertex.co.z - minimum[2]) * scale
    obj.data.update()
    corrected_minimum, corrected_maximum = bounds(obj)
    return {
        "uniformScale": scale,
        "boundsMin": corrected_minimum,
        "boundsMax": corrected_maximum,
    }


def resize_pbr_images(size: int) -> list[dict[str, object]]:
    result: list[dict[str, object]] = []
    for image in sorted(
        (item for item in bpy.data.images if item.type == "IMAGE" and item.size[0] > 0),
        key=lambda item: item.name,
    ):
        original = tuple(image.size)
        if original[0] != size or original[1] != size:
            image.scale(size, size)
        # All provider maps are opaque. Keep the export compact while retaining
        # the full base/emission/normal/metallic-roughness material graph.
        image.file_format = "JPEG"
        image.pack()
        result.append(
            {
                "name": image.name,
                "sourceSize": original,
                "runtimeSize": tuple(image.size),
            }
        )
    return result


def save_blend(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path), compress=True)


def export_glb(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        export_yup=True,
        export_apply=True,
        export_texcoords=True,
        export_normals=True,
        export_tangents=True,
        export_materials="EXPORT",
        export_image_format="JPEG",
        export_jpeg_quality=90,
        export_image_quality=90,
        export_keep_originals=False,
        export_cameras=False,
        export_lights=False,
        export_animations=False,
        export_skins=False,
        export_morph=False,
        export_extras=True,
    )


def repair_invalid_tangents(path: Path) -> dict[str, int]:
    """Replace exporter zero tangents at degenerate UV points deterministically.

    Blender emits a tangent accessor when requested, but a handful of vertices
    on this provider mesh have collapsed UV derivatives and receive (0, 0, 0).
    That is invalid glTF. Use each vertex normal to choose a stable
    perpendicular instead. Canonicalize every retained tangent through a
    two-decimal direction grid and renormalize to six decimals as well.
    Blender's basis solve otherwise varies by 0.0001 at a few seam vertices;
    the grid is sub-degree and below a 1K normal-map sample. Geometry, UVs,
    topology and textures stay untouched.
    """

    data = bytearray(path.read_bytes())
    if data[:4] != b"glTF" or struct.unpack_from("<I", data, 4)[0] != 2:
        raise RuntimeError(f"Expected a glTF 2.0 binary: {path}")
    json_length, json_type = struct.unpack_from("<II", data, 12)
    if json_type != 0x4E4F534A:
        raise RuntimeError(f"GLB JSON chunk is missing: {path}")
    document = json.loads(
        bytes(data[20 : 20 + json_length]).decode("utf-8").rstrip("\0 ")
    )
    binary_header = 20 + json_length
    binary_length, binary_type = struct.unpack_from("<II", data, binary_header)
    if binary_type != 0x004E4942:
        raise RuntimeError(f"GLB BIN chunk is missing: {path}")
    binary_offset = binary_header + 8
    if binary_offset + binary_length > len(data):
        raise RuntimeError(f"GLB BIN chunk overruns the file: {path}")

    pairs: dict[int, int] = {}
    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            attributes = primitive.get("attributes", {})
            tangent = attributes.get("TANGENT")
            normal = attributes.get("NORMAL")
            if tangent is None or normal is None:
                continue
            if tangent in pairs and pairs[tangent] != normal:
                raise RuntimeError("One tangent accessor is paired with multiple normals.")
            pairs[tangent] = normal
    if not pairs:
        raise RuntimeError(f"Export did not contain a tangent accessor: {path}")

    accessors = document["accessors"]
    views = document["bufferViews"]

    def vector(accessor_index: int, element: int, components: int) -> tuple[float, ...]:
        accessor = accessors[accessor_index]
        if accessor.get("componentType") != 5126 or accessor.get("sparse") is not None:
            raise RuntimeError("Expected a dense FLOAT tangent/normal accessor.")
        view = views[accessor["bufferView"]]
        if view.get("buffer", 0) != 0:
            raise RuntimeError("Expected tangent/normal data in GLB buffer zero.")
        stride = view.get("byteStride", components * 4)
        offset = (
            binary_offset
            + view.get("byteOffset", 0)
            + accessor.get("byteOffset", 0)
            + element * stride
        )
        return struct.unpack_from(f"<{components}f", data, offset)

    repaired = 0
    quantized = 0
    examined = 0

    def canonical_direction(x: float, y: float, z: float) -> tuple[float, float, float]:
        coarse = (round(x, 2), round(y, 2), round(z, 2))
        coarse_length = math.sqrt(sum(value * value for value in coarse))
        if coarse_length <= 1e-12:
            raise RuntimeError("Cannot canonicalize a zero tangent direction.")
        return tuple(round(value / coarse_length, 6) for value in coarse)

    for tangent_index, normal_index in sorted(pairs.items()):
        tangent_accessor = accessors[tangent_index]
        normal_accessor = accessors[normal_index]
        if tangent_accessor.get("type") != "VEC4" or normal_accessor.get("type") != "VEC3":
            raise RuntimeError("Expected VEC4 tangents paired with VEC3 normals.")
        if tangent_accessor["count"] != normal_accessor["count"]:
            raise RuntimeError("Tangent and normal accessor counts differ.")
        view = views[tangent_accessor["bufferView"]]
        stride = view.get("byteStride", 16)
        start = (
            binary_offset
            + view.get("byteOffset", 0)
            + tangent_accessor.get("byteOffset", 0)
        )
        for element in range(tangent_accessor["count"]):
            examined += 1
            x, y, z, handedness = vector(tangent_index, element, 4)
            length = math.sqrt(x * x + y * y + z * z)
            valid_handedness = abs(abs(handedness) - 1.0) <= 1e-5
            if math.isfinite(length) and length > 1e-6 and valid_handedness:
                tx, ty, tz = canonical_direction(x, y, z)
                replacement = (
                    tx,
                    ty,
                    tz,
                    handedness,
                )
            else:
                nx, ny, nz = vector(normal_index, element, 3)
                if not all(math.isfinite(value) for value in (nx, ny, nz)):
                    raise RuntimeError(f"Non-finite normal at vertex {element}.")
                # Cross the normal with whichever cardinal axis is least parallel.
                if abs(nx) <= abs(ny) and abs(nx) <= abs(nz):
                    tx, ty, tz = 0.0, nz, -ny
                elif abs(ny) <= abs(nz):
                    tx, ty, tz = -nz, 0.0, nx
                else:
                    tx, ty, tz = ny, -nx, 0.0
                tangent_length = math.sqrt(tx * tx + ty * ty + tz * tz)
                if tangent_length <= 1e-12:
                    raise RuntimeError(f"Cannot derive tangent at vertex {element}.")
                tx, ty, tz = canonical_direction(tx, ty, tz)
                replacement = (
                    tx,
                    ty,
                    tz,
                    handedness if valid_handedness else 1.0,
                )
                repaired += 1
            packed = struct.pack("<4f", *replacement)
            offset = start + element * stride
            if data[offset : offset + 16] != packed:
                data[offset : offset + 16] = packed
                quantized += 1

    path.write_bytes(data)
    return {
        "accessors": len(pairs),
        "verticesExamined": examined,
        "verticesRepaired": repaired,
        "verticesQuantized": quantized,
    }


def tier_record(path: Path, triangles: int, texture_size: int) -> dict[str, object]:
    obj = mesh_object()
    minimum, maximum = bounds(obj)
    return {
        "file": path.name,
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
        "triangles": triangles,
        "vertices": len(obj.data.vertices),
        "materials": len(obj.material_slots),
        "textureSize": texture_size,
        "blenderBoundsMin": minimum,
        "blenderBoundsMax": maximum,
    }


def main() -> None:
    args = parse_args()
    source = args.source.resolve()
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    if not source.is_file():
        raise RuntimeError(f"Source GLB does not exist: {source}")
    actual_hash = sha256(source)
    if actual_hash != SOURCE_SHA256:
        raise RuntimeError(
            f"Source SHA-256 drift: expected {SOURCE_SHA256}, got {actual_hash}."
        )

    reset_scene()
    bpy.ops.import_scene.gltf(filepath=str(source))
    obj = mesh_object()
    if triangle_count(obj) != SOURCE_TRIANGLES:
        raise RuntimeError(
            f"Source triangle drift: expected {SOURCE_TRIANGLES}, got {triangle_count(obj)}."
        )
    normalization = normalize_master(obj)

    rich_triangles = decimate_to(obj, RICH_TRIANGLES)
    rich_contract = enforce_coordinate_contract(obj)
    rich_images = resize_pbr_images(RICH_TEXTURE_SIZE)
    rich_blend = output_dir / "lattice-loom-rich.blend"
    rich_glb = output_dir / "lattice-loom-rich-source.glb"
    save_blend(rich_blend)
    export_glb(rich_glb)
    rich_tangents = repair_invalid_tangents(rich_glb)
    rich = tier_record(rich_glb, rich_triangles, RICH_TEXTURE_SIZE)
    rich["tangentRepair"] = rich_tangents
    rich["coordinateContract"] = rich_contract
    rich["editableSource"] = {
        "file": rich_blend.name,
        "bytes": rich_blend.stat().st_size,
        "sha256": sha256(rich_blend),
    }
    rich["images"] = rich_images

    lean_triangles = decimate_to(obj, LEAN_TRIANGLES)
    lean_contract = enforce_coordinate_contract(obj)
    lean_images = resize_pbr_images(LEAN_TEXTURE_SIZE)
    lean_blend = output_dir / "lattice-loom-lean.blend"
    lean_glb = output_dir / "lattice-loom-lean-source.glb"
    save_blend(lean_blend)
    export_glb(lean_glb)
    lean_tangents = repair_invalid_tangents(lean_glb)
    lean = tier_record(lean_glb, lean_triangles, LEAN_TEXTURE_SIZE)
    lean["tangentRepair"] = lean_tangents
    lean["coordinateContract"] = lean_contract
    lean["editableSource"] = {
        "file": lean_blend.name,
        "bytes": lean_blend.stat().st_size,
        "sha256": sha256(lean_blend),
    }
    lean["images"] = lean_images

    report = {
        "schemaVersion": 1,
        "source": {
            "file": str(source),
            "bytes": source.stat().st_size,
            "sha256": actual_hash,
            "triangles": SOURCE_TRIANGLES,
        },
        "normalization": normalization,
        "tiers": {"rich": rich, "lean": lean},
        "notes": [
            "One fused manifold remains one fused manifold.",
            "No global nonuniform scale, rig, skin, morph, camera, light, or animation is added.",
            "Semantic team extraction and hull cyan neutralization run after this Blender stage.",
        ],
    }
    report_path = output_dir / "build-report.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
