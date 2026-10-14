#!/usr/bin/env python3
"""Blender-side texture-only donor transplant.

The provider GLB is inspected in disposable factory scenes and parsed as a
container for PBR images/factors/samplers. An opt-in v3 path replaces its image
payload with five pinned standalone provider PNGs and losslessly composes glTF
metallic/roughness channels. The authored .blend is opened only after uploaded-
source/donor geometry+UV correspondence passes. Opening the .blend purges every
imported provider datablock before materials are rebuilt with deterministic
local nodes. An opt-in v4 path reconstructs a report-pinned coverage pose from
temporary world-baked body copies for upload validation, then discards them and
exports the untouched animated hierarchy.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import struct
import sys
import traceback
from collections import defaultdict
from itertools import permutations, product
from pathlib import Path
from typing import Any, Iterable

import bpy
import numpy as np
from mathutils import Vector

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from lossless_pbr_png import (  # noqa: E402
    compose_metallic_roughness_png,
    inspect_png,
    sha256_bytes as png_sha256_bytes,
)


EXPECTED_BLENDER = (5, 2)
REPORT_SCHEMA = "nilbots-texture-donor-transplant-2"
LOSSLESS_PBR_REPORT_SCHEMA = "nilbots-texture-donor-transplant-3"
COVERAGE_POSE_REPORT_SCHEMA = "nilbots-texture-donor-transplant-4"
SIMPLE_NAME_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_. -]{0,127}$")
SURFACE_QUANTUM = 1.0e-6
NORMALIZED_SURFACE_QUANTUM = 1.0e-5
SIMILARITY_MAX_NORMALIZED_RESIDUAL = 1.0e-4
SIMILARITY_RMS_NORMALIZED_RESIDUAL = 5.0e-6
SIMILARITY_P99_NORMALIZED_RESIDUAL = 2.0e-6
UV_EPSILON = 1.0e-9
CORRESPONDENCE_UV_AREA_EPSILON = 1.0e-14
MAX_SOURCE_ONLY_TRIANGLES = 512
MAX_SOURCE_ONLY_UV_AREA_RATIO = 1.0e-4
MAX_SOURCE_ONLY_NORMALIZED_SPATIAL_AREA = 2.0e-4
MAX_SOURCE_DEGENERATE_UV_TRIANGLES = 256
ATLAS_MICRO_UV_TRIANGLE_AREA = 1.0e-9
MAX_ATLAS_MICRO_UV_TRIANGLES = 512
MAX_ATLAS_MICRO_UV_AREA_RATIO = 1.0e-5
MAX_ATLAS_MICRO_NORMALIZED_SPATIAL_AREA = 1.0e-3
MAX_ATLAS_MICRO_OVERLAP_TRIANGLE_AREA = 1.0e-7
MAX_ATLAS_MICRO_OVERLAP_PAIRS = 2048
MAX_ATLAS_MICRO_OVERLAP_UV_AREA_RATIO = 1.0e-4
MAX_ATLAS_MICRO_OVERLAP_NORMALIZED_SPATIAL_AREA = 1.0e-3
TEAM_ROLE = "team-accent"
COVERAGE_TRANSFORM_POLICY = (
    "Bake each evaluated coverage-pose world transform into a temporary "
    "body-only mesh copy; export thirty identity-root named meshes with no "
    "parent pivots"
)
GLTF_REPEAT = 10497
GLTF_LINEAR = 9729
GLTF_LINEAR_MIPMAP_LINEAR = 9987
SUPPORTED_IMAGES = {
    "image/png": (".png", b"\x89PNG\r\n\x1a\n"),
    "image/jpeg": (".jpg", b"\xff\xd8"),
}
STANDALONE_PBR_OPTIONS = (
    (
        "baseColor",
        "pbr_base_color_png",
        "expected_pbr_base_color_sha256",
        "--pbr-base-color-png",
        "--expected-pbr-base-color-sha256",
        "pbrBaseColorPng",
    ),
    (
        "metallic",
        "pbr_metallic_png",
        "expected_pbr_metallic_sha256",
        "--pbr-metallic-png",
        "--expected-pbr-metallic-sha256",
        "pbrMetallicPng",
    ),
    (
        "roughness",
        "pbr_roughness_png",
        "expected_pbr_roughness_sha256",
        "--pbr-roughness-png",
        "--expected-pbr-roughness-sha256",
        "pbrRoughnessPng",
    ),
    (
        "normal",
        "pbr_normal_png",
        "expected_pbr_normal_sha256",
        "--pbr-normal-png",
        "--expected-pbr-normal-sha256",
        "pbrNormalPng",
    ),
    (
        "emissive",
        "pbr_emissive_png",
        "expected_pbr_emissive_sha256",
        "--pbr-emissive-png",
        "--expected-pbr-emissive-sha256",
        "pbrEmissivePng",
    ),
)


def blender_arguments() -> list[str]:
    argv = sys.argv
    if "--" not in argv:
        return []
    return argv[argv.index("--") + 1 :]


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--authored-blend", required=True, type=Path)
    parser.add_argument("--expected-authored-sha256", required=True)
    parser.add_argument("--uploaded-source-glb", required=True, type=Path)
    parser.add_argument("--expected-uploaded-source-sha256", required=True)
    parser.add_argument("--donor-glb", required=True, type=Path)
    parser.add_argument("--expected-donor-sha256", required=True)
    parser.add_argument("--model-collection", required=True)
    parser.add_argument("--bind-frame", required=True, type=int)
    parser.add_argument("--output-dir", required=True, type=Path)
    for (
        _role,
        path_destination,
        hash_destination,
        path_option,
        hash_option,
        _report_key,
    ) in STANDALONE_PBR_OPTIONS:
        parser.add_argument(path_option, dest=path_destination, type=Path)
        parser.add_argument(hash_option, dest=hash_destination)
    parser.add_argument("--coverage-validation-report", type=Path)
    parser.add_argument("--expected-coverage-validation-report-sha256")
    parser.add_argument("--coverage-pose-clip", action="append")
    parser.add_argument("--coverage-pose-frame", type=int)
    return parser.parse_args(argv)


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def resolve_repo_path(
    value: Path,
    *,
    repo_root: Path,
    option: str,
    strict: bool,
) -> tuple[Path, Path]:
    if value.is_absolute() or not value.parts:
        raise ValueError(f"{option} must be repository-relative")
    path = (repo_root / value).resolve(strict=strict)
    try:
        relative = path.relative_to(repo_root)
    except ValueError as error:
        raise ValueError(f"{option} escaped the repository") from error
    return path, relative


def resolve_standalone_pbr(
    args: argparse.Namespace,
    *,
    repo_root: Path,
) -> dict[str, dict[str, Any]] | None:
    supplied: list[tuple[bool, str]] = []
    for (
        _role,
        path_destination,
        hash_destination,
        path_option,
        hash_option,
        _report_key,
    ) in STANDALONE_PBR_OPTIONS:
        supplied.append((getattr(args, path_destination) is not None, path_option))
        supplied.append((getattr(args, hash_destination) is not None, hash_option))
    if not any(present for present, _option in supplied):
        return None
    missing = [option for present, option in supplied if not present]
    if missing:
        raise ValueError(
            "Standalone PBR mode requires all five PNG paths and hashes; "
            "missing: " + ", ".join(missing)
        )

    result: dict[str, dict[str, Any]] = {}
    seen: set[Path] = set()
    for (
        role,
        path_destination,
        hash_destination,
        path_option,
        _hash_option,
        report_key,
    ) in STANDALONE_PBR_OPTIONS:
        path, relative = resolve_repo_path(
            getattr(args, path_destination),
            repo_root=repo_root,
            option=path_option,
            strict=True,
        )
        if not path.is_file() or path.suffix.lower() != ".png":
            raise ValueError(f"{path_option} must name an existing .png file")
        if path in seen:
            raise ValueError(
                "Standalone PBR roles must reference five distinct PNG files"
            )
        seen.add(path)
        expected = getattr(args, hash_destination)
        if not isinstance(expected, str) or not re.fullmatch(
            r"[0-9a-f]{64}", expected
        ):
            raise ValueError(f"{path_option} has no valid pinned SHA-256")
        result[role] = {
            "path": path,
            "relative": relative,
            "sha256": expected,
            "reportKey": report_key,
        }
    return result


def resolve_coverage_pose(
    args: argparse.Namespace,
    *,
    repo_root: Path,
) -> dict[str, Any] | None:
    supplied = {
        "--coverage-validation-report": args.coverage_validation_report,
        "--expected-coverage-validation-report-sha256": (
            args.expected_coverage_validation_report_sha256
        ),
        "--coverage-pose-clip": args.coverage_pose_clip,
        "--coverage-pose-frame": args.coverage_pose_frame,
    }
    if all(value is None for value in supplied.values()):
        return None
    missing = [option for option, value in supplied.items() if value is None]
    if missing:
        raise ValueError(
            "Coverage-pose mode requires its report, hash, clip(s), and frame; "
            "missing: " + ", ".join(missing)
        )
    clips = tuple(args.coverage_pose_clip)
    if (
        not clips
        or len(set(clips)) != len(clips)
        or any(not SIMPLE_NAME_RE.fullmatch(clip) for clip in clips)
    ):
        raise ValueError("Coverage-pose clips must be simple, non-empty, unique names")
    if args.coverage_pose_frame < 0:
        raise ValueError("--coverage-pose-frame must be non-negative")
    path, relative = resolve_repo_path(
        args.coverage_validation_report,
        repo_root=repo_root,
        option="--coverage-validation-report",
        strict=True,
    )
    if not path.is_file() or path.suffix.lower() != ".json":
        raise ValueError(
            "--coverage-validation-report must name an existing .json file"
        )
    expected = args.expected_coverage_validation_report_sha256
    if not isinstance(expected, str) or not re.fullmatch(r"[0-9a-f]{64}", expected):
        raise ValueError("Coverage validation report has no valid pinned SHA-256")
    return {
        "path": path,
        "relative": relative,
        "sha256": expected,
        "clips": clips,
        "frame": args.coverage_pose_frame,
        "reportKey": "coverageValidationReport",
    }


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def stable_json(payload: Any) -> str:
    return json.dumps(
        payload,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
        allow_nan=False,
    )


def stable_hash(payload: Any) -> str:
    return sha256_bytes(stable_json(payload).encode("utf-8"))


def finite_number(value: Any, label: str) -> float:
    if (
        isinstance(value, bool)
        or not isinstance(value, (int, float))
        or not math.isfinite(float(value))
    ):
        raise ValueError(f"{label} must be finite")
    return float(value)


def finite_vector(
    value: Any,
    *,
    length: int,
    label: str,
    default: tuple[float, ...],
) -> list[float]:
    if value is None:
        return list(default)
    if not isinstance(value, list) or len(value) != length:
        raise ValueError(f"{label} must contain {length} numbers")
    return [
        finite_number(component, f"{label}[{index}]")
        for index, component in enumerate(value)
    ]


def parse_glb(path: Path) -> tuple[dict[str, Any], bytes]:
    payload = path.read_bytes()
    if len(payload) < 20 or payload[:4] != b"glTF":
        raise ValueError(f"Not a GLB: {path.name}")
    _magic, version, total_length = struct.unpack_from("<4sII", payload, 0)
    if version != 2 or total_length != len(payload):
        raise ValueError(f"Malformed GLB header: {path.name}")
    offset = 12
    json_chunk: bytes | None = None
    binary_chunk: bytes | None = None
    chunk_types: list[int] = []
    while offset < len(payload):
        if offset + 8 > len(payload):
            raise ValueError(f"Truncated GLB chunk header: {path.name}")
        length, chunk_type = struct.unpack_from("<II", payload, offset)
        offset += 8
        if offset + length > len(payload):
            raise ValueError(f"Truncated GLB chunk: {path.name}")
        chunk = payload[offset : offset + length]
        offset += length
        if length % 4 != 0:
            raise ValueError(f"Unaligned GLB chunk: {path.name}")
        chunk_types.append(chunk_type)
        if chunk_type == 0x4E4F534A:
            if json_chunk is not None:
                raise ValueError(f"Multiple GLB JSON chunks: {path.name}")
            json_chunk = chunk
        elif chunk_type == 0x004E4942:
            if binary_chunk is not None:
                raise ValueError(f"Multiple GLB BIN chunks: {path.name}")
            binary_chunk = chunk
    if json_chunk is None:
        raise ValueError(f"GLB has no JSON chunk: {path.name}")
    if not chunk_types or chunk_types[0] != 0x4E4F534A:
        raise ValueError(f"GLB JSON chunk is not first: {path.name}")
    document = json.loads(json_chunk.rstrip(b" \x00").decode("utf-8"))
    if not isinstance(document, dict):
        raise ValueError(f"GLB JSON root is not an object: {path.name}")
    asset = document.get("asset")
    if not isinstance(asset, dict) or asset.get("version") != "2.0":
        raise ValueError(f"GLB asset version is not 2.0: {path.name}")
    return document, binary_chunk or b""


def write_glb(
    path: Path,
    *,
    document: dict[str, Any],
    binary_chunk: bytes,
) -> None:
    json_payload = json.dumps(
        document,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
        allow_nan=False,
    ).encode("utf-8")
    json_payload += b" " * ((-len(json_payload)) % 4)
    chunks = [
        struct.pack("<II", len(json_payload), 0x4E4F534A),
        json_payload,
    ]
    if binary_chunk:
        if len(binary_chunk) % 4 != 0:
            raise ValueError("Cannot write an unaligned GLB BIN chunk")
        chunks.extend(
            [
                struct.pack("<II", len(binary_chunk), 0x004E4942),
                binary_chunk,
            ]
        )
    body = b"".join(chunks)
    path.write_bytes(
        struct.pack("<4sII", b"glTF", 2, 12 + len(body)) + body
    )


def validate_embedded_glb(
    document: dict[str, Any],
    binary_chunk: bytes,
    *,
    label: str,
) -> None:
    buffers = document.get("buffers", [])
    if not isinstance(buffers, list) or len(buffers) != 1:
        raise ValueError(f"{label} must contain one embedded GLB buffer")
    buffer = buffers[0]
    if not isinstance(buffer, dict) or "uri" in buffer:
        raise ValueError(f"{label} buffer must be embedded in the GLB BIN chunk")
    byte_length = buffer.get("byteLength")
    if (
        not isinstance(byte_length, int)
        or byte_length < 0
        or byte_length > len(binary_chunk)
    ):
        raise ValueError(f"{label} embedded buffer length is invalid")

    views = document.get("bufferViews", [])
    if not isinstance(views, list):
        raise ValueError(f"{label} bufferViews must be an array")
    for index, view in enumerate(views):
        if not isinstance(view, dict) or view.get("buffer", 0) != 0:
            raise ValueError(f"{label} bufferView {index} is invalid")
        start = view.get("byteOffset", 0)
        length = view.get("byteLength")
        if (
            not isinstance(start, int)
            or not isinstance(length, int)
            or start < 0
            or length < 0
            or start + length > byte_length
        ):
            raise ValueError(f"{label} bufferView {index} exceeds the BIN chunk")

    images = document.get("images", [])
    if not isinstance(images, list):
        raise ValueError(f"{label} images must be an array")
    for index, image in enumerate(images):
        if (
            not isinstance(image, dict)
            or "uri" in image
            or not isinstance(image.get("bufferView"), int)
        ):
            raise ValueError(
                f"{label} image {index} must be embedded with a bufferView"
            )
        view_index = image["bufferView"]
        if view_index < 0 or view_index >= len(views):
            raise ValueError(f"{label} image {index} has an invalid bufferView")


def validate_extension_declarations(
    document: dict[str, Any],
    *,
    label: str,
    allowed: set[str],
) -> None:
    used = document.get("extensionsUsed", [])
    required = document.get("extensionsRequired", [])
    if (
        not isinstance(used, list)
        or not isinstance(required, list)
        or any(not isinstance(item, str) or not item for item in used)
        or any(not isinstance(item, str) or not item for item in required)
        or len(set(used)) != len(used)
        or len(set(required)) != len(required)
        or not set(required).issubset(used)
    ):
        raise ValueError(f"{label} extension declarations are malformed")
    unsupported = (set(used) | set(required)) - allowed
    if unsupported:
        raise ValueError(
            f"{label} declares unsupported extensions: "
            + ", ".join(sorted(unsupported))
        )
    root_extensions = document.get("extensions", {})
    if (
        not isinstance(root_extensions, dict)
        or set(root_extensions) - allowed
    ):
        raise ValueError(f"{label} root extensions are unsupported")
    declared = set(used)
    for material in document.get("materials", []):
        if not isinstance(material, dict):
            continue
        extensions = material.get("extensions", {})
        if not isinstance(extensions, dict):
            raise ValueError(f"{label} material extensions are malformed")
        if not set(extensions).issubset(declared):
            raise ValueError(
                f"{label} material uses an undeclared extension"
            )


def raw_glb_surface_digest(
    document: dict[str, Any],
    binary_chunk: bytes,
) -> str:
    digest = hashlib.sha256()
    meshes = document.get("meshes", [])
    accessors = document.get("accessors", [])
    views = document.get("bufferViews", [])
    if (
        not isinstance(meshes, list)
        or not isinstance(accessors, list)
        or not isinstance(views, list)
    ):
        raise ValueError("GLB surface digest inputs are malformed")
    for mesh_index, mesh in enumerate(meshes):
        if not isinstance(mesh, dict):
            raise ValueError("GLB surface digest mesh is malformed")
        primitives = mesh.get("primitives", [])
        if not isinstance(primitives, list):
            raise ValueError("GLB surface digest primitives are malformed")
        for primitive_index, primitive in enumerate(primitives):
            if not isinstance(primitive, dict):
                raise ValueError("GLB surface digest primitive is malformed")
            digest.update(struct.pack("<II", mesh_index, primitive_index))
            references = [("INDICES", primitive.get("indices"))]
            attributes = primitive.get("attributes", {})
            if not isinstance(attributes, dict):
                raise ValueError("GLB surface digest attributes are malformed")
            references.extend(sorted(attributes.items()))
            for semantic, accessor_index in references:
                if accessor_index is None:
                    continue
                if (
                    not isinstance(semantic, str)
                    or not isinstance(accessor_index, int)
                    or accessor_index < 0
                    or accessor_index >= len(accessors)
                    or not isinstance(accessors[accessor_index], dict)
                ):
                    raise ValueError(
                        "GLB surface digest accessor reference is invalid"
                    )
                accessor = accessors[accessor_index]
                view_index = accessor.get("bufferView")
                if (
                    not isinstance(view_index, int)
                    or view_index < 0
                    or view_index >= len(views)
                    or not isinstance(views[view_index], dict)
                ):
                    raise ValueError(
                        "GLB surface digest bufferView is invalid"
                    )
                view = views[view_index]
                start = view.get("byteOffset", 0)
                length = view.get("byteLength")
                if (
                    not isinstance(start, int)
                    or not isinstance(length, int)
                    or start < 0
                    or length < 0
                    or start + length > len(binary_chunk)
                ):
                    raise ValueError(
                        "GLB surface digest bufferView exceeds BIN"
                    )
                digest.update(semantic.encode("ascii"))
                digest.update(
                    stable_json(
                        {
                            "componentType": accessor.get("componentType"),
                            "count": accessor.get("count"),
                            "type": accessor.get("type"),
                            "normalized": accessor.get(
                                "normalized",
                                False,
                            ),
                            "byteOffset": accessor.get("byteOffset", 0),
                            "byteStride": view.get("byteStride"),
                        }
                    ).encode("utf-8")
                )
                digest.update(binary_chunk[start : start + length])
    return digest.hexdigest()


def authority_validation_ledger(
    *,
    authored: Path,
    authored_relative: Path,
    authored_sha256: str,
    uploaded: Path,
    uploaded_relative: Path,
    uploaded_sha256: str,
    repo_root: Path,
) -> tuple[dict[str, Any], Path | None, str | None]:
    candidates = (
        authored.parent / "validation-report.json",
        authored.parent / "validation" / "validation-report.json",
    )
    existing = [candidate for candidate in candidates if candidate.is_file()]
    if not existing:
        return {"present": False}, None, None
    if len(existing) != 1:
        raise ValueError("Authored authority has ambiguous validation ledgers")
    ledger_path = existing[0].resolve(strict=True)
    try:
        ledger_relative = ledger_path.relative_to(repo_root)
    except ValueError as error:
        raise ValueError(
            "Authored authority validation ledger escaped the repository"
        ) from error
    ledger = json.loads(ledger_path.read_text(encoding="utf-8"))
    if (
        not isinstance(ledger, dict)
        or ledger.get("schema")
        != "nilbots-bulwark-kinematic-master-1"
        or ledger.get("status") != "PASS"
    ):
        raise ValueError("Authored authority validation ledger did not pass")
    artifacts = ledger.get("artifacts", {})
    texture_upload = ledger.get("textureUpload", {})
    if not isinstance(artifacts, dict) or not isinstance(texture_upload, dict):
        raise ValueError("Authored authority validation ledger is malformed")
    blend_artifact = artifacts.get("blend", {})
    upload_artifact = artifacts.get("textureUploadGlb", {})
    if (
        not isinstance(blend_artifact, dict)
        or not isinstance(upload_artifact, dict)
        or blend_artifact.get("sha256") != authored_sha256
        or upload_artifact.get("sha256") != uploaded_sha256
        or blend_artifact.get("bytes") != authored.stat().st_size
        or upload_artifact.get("bytes") != uploaded.stat().st_size
    ):
        raise ValueError(
            "Authored authority ledger does not pin the requested blend/upload"
        )
    surface_digest = texture_upload.get("surfaceDigest")
    if (
        not isinstance(surface_digest, str)
        or len(surface_digest) != 64
        or texture_upload.get("authoritySurfaceDigest") != surface_digest
        or texture_upload.get("surfaceLoopUvCorrespondenceExact") is not True
    ):
        raise ValueError(
            "Authored authority ledger does not prove exact upload surface"
        )
    uploaded_document, uploaded_binary = parse_glb(uploaded)
    observed_digest = raw_glb_surface_digest(
        uploaded_document,
        uploaded_binary,
    )
    if observed_digest != surface_digest:
        raise ValueError(
            "Uploaded source raw surface digest differs from authority ledger"
        )
    return (
        {
            "present": True,
            "path": ledger_relative.as_posix(),
            "sha256": sha256(ledger_path),
            "schema": ledger["schema"],
            "status": ledger["status"],
            "authoredBlend": {
                "path": authored_relative.as_posix(),
                "sha256": authored_sha256,
            },
            "uploadedSourceGlb": {
                "path": uploaded_relative.as_posix(),
                "sha256": uploaded_sha256,
            },
            "surfaceDigest": surface_digest,
            "surfaceLoopUvCorrespondenceExact": True,
        },
        ledger_path,
        sha256(ledger_path),
    )


def coverage_validation_ledger(
    *,
    coverage_pose: dict[str, Any],
    authored: Path,
    authored_relative: Path,
    authored_sha256: str,
    uploaded: Path,
    uploaded_relative: Path,
    uploaded_sha256: str,
) -> dict[str, Any]:
    path = coverage_pose["path"]
    observed = sha256(path)
    if observed != coverage_pose["sha256"]:
        raise ValueError(
            "Coverage validation report hash drifted before validation: "
            f"{observed} != {coverage_pose['sha256']}"
        )
    ledger = json.loads(path.read_text(encoding="utf-8"))
    if (
        not isinstance(ledger, dict)
        or ledger.get("schema") != "nilbots-aegis-uv-authority-candidate-2"
        or ledger.get("status") != "PASS"
    ):
        raise ValueError("Coverage validation report is not the expected PASS schema")
    outputs = ledger.get("outputs")
    coverage = ledger.get("coverageUpload")
    preservation = ledger.get("authorityPreservation")
    if not all(isinstance(value, dict) for value in (outputs, coverage, preservation)):
        raise ValueError("Coverage validation report is malformed")
    editable = outputs.get("editableCandidate")
    upload = outputs.get("bodyOnlyCoverageUpload")
    if (
        not isinstance(editable, dict)
        or editable.get("sha256") != authored_sha256
        or editable.get("bytes") != authored.stat().st_size
    ):
        raise ValueError("Coverage report does not pin the requested authored blend")
    if (
        not isinstance(upload, dict)
        or upload.get("sha256") != uploaded_sha256
        or upload.get("bytes") != uploaded.stat().st_size
    ):
        raise ValueError("Coverage report does not pin the requested upload GLB")
    pose = coverage.get("pose")
    validation = coverage.get("validation")
    if not isinstance(pose, dict) or not isinstance(validation, dict):
        raise ValueError("Coverage report omitted pose or validation evidence")
    if (
        pose.get("activeClips") != list(coverage_pose["clips"])
        or pose.get("frame") != coverage_pose["frame"]
        or pose.get("transformPolicy") != COVERAGE_TRANSFORM_POLICY
    ):
        raise ValueError("Coverage report pose does not match the explicit request")
    validation_checks = validation.get("checks")
    if (
        not isinstance(validation_checks, dict)
        or not validation_checks
        or any(value is not True for value in validation_checks.values())
    ):
        raise ValueError("Coverage report contains a failed upload validation check")
    in_memory = preservation.get("inMemoryChecks")
    persisted = preservation.get("persistedChecks")
    animated = preservation.get("animatedGlb")
    low_hover = preservation.get("lowHoverSemantics")
    if (
        not isinstance(in_memory, dict)
        or not in_memory
        or any(value is not True for value in in_memory.values())
        or not isinstance(persisted, dict)
        or not persisted
        or any(value is not True for value in persisted.values())
        or not isinstance(animated, dict)
        or not isinstance(animated.get("checks"), dict)
        or not animated["checks"]
        or any(value is not True for value in animated["checks"].values())
        or not isinstance(low_hover, dict)
        or low_hover.get("preserved") is not True
    ):
        raise ValueError("Coverage report does not preserve animated authority")
    object_names = validation.get("objectNames")
    mesh_names = validation.get("meshNames")
    topology = validation.get("topology")
    world_bounds = validation.get("worldBounds")
    distinct = validation.get("distinctRepeatedPartCenters")
    distinct_groups = distinct.get("groups") if isinstance(distinct, dict) else None
    if (
        not isinstance(object_names, list)
        or not object_names
        or any(not isinstance(name, str) or not name for name in object_names)
        or len(set(object_names)) != len(object_names)
        or not isinstance(mesh_names, list)
        or not mesh_names
        or any(not isinstance(name, str) or not name for name in mesh_names)
        or len(set(mesh_names)) != len(mesh_names)
        or not isinstance(topology, dict)
        or topology.get("expectedTriangles") != topology.get("importedTriangles")
        or topology.get("expectedTriangles", 0) <= 0
        or validation.get("meshes") != len(mesh_names)
        or len(object_names) != len(mesh_names)
        or not isinstance(world_bounds, dict)
        or world_bounds.get("maximumAbsoluteDelta", math.inf)
        > world_bounds.get("tolerance", -math.inf)
        or not isinstance(distinct, dict)
        or not isinstance(distinct_groups, dict)
        or not distinct_groups
        or any(
            not isinstance(group, dict)
            or group.get("complete") is not True
            or group.get("distinct") is not True
            for group in distinct_groups.values()
        )
    ):
        raise ValueError("Coverage report names/topology/world evidence is invalid")
    return {
        "present": True,
        "path": coverage_pose["relative"].as_posix(),
        "sha256": coverage_pose["sha256"],
        "schema": ledger["schema"],
        "status": ledger["status"],
        "authoredBlend": {
            "path": authored_relative.as_posix(),
            "sha256": authored_sha256,
            "bytes": authored.stat().st_size,
        },
        "uploadedSourceGlb": {
            "path": uploaded_relative.as_posix(),
            "sha256": uploaded_sha256,
            "bytes": uploaded.stat().st_size,
        },
        "pose": {
            "activeClips": list(coverage_pose["clips"]),
            "frame": coverage_pose["frame"],
            "transformPolicy": COVERAGE_TRANSFORM_POLICY,
        },
        "expected": {
            "objectNames": object_names,
            "meshNames": mesh_names,
            "meshes": validation["meshes"],
            "triangles": topology["expectedTriangles"],
        },
        "uploadValidationChecksSha256": stable_hash(validation_checks),
        "authorityPreservationChecksSha256": stable_hash(
            {
                "inMemory": in_memory,
                "persisted": persisted,
                "animatedGlb": animated["checks"],
                "lowHover": low_hover["preserved"],
            }
        ),
        "worldBounds": {
            "maximumAbsoluteDelta": world_bounds["maximumAbsoluteDelta"],
            "tolerance": world_bounds["tolerance"],
        },
        "distinctRepeatedPartCentersSha256": stable_hash(distinct),
    }


def material_texture_info(
    material: dict[str, Any],
    role: str,
) -> dict[str, Any] | None:
    pbr = material.get("pbrMetallicRoughness", {})
    if not isinstance(pbr, dict):
        raise ValueError("Donor pbrMetallicRoughness must be an object")
    paths = {
        "baseColor": pbr.get("baseColorTexture"),
        "metallicRoughness": pbr.get("metallicRoughnessTexture"),
        "normal": material.get("normalTexture"),
        "emissive": material.get("emissiveTexture"),
        "occlusion": material.get("occlusionTexture"),
    }
    value = paths[role]
    if value is None:
        return None
    if not isinstance(value, dict):
        raise ValueError(f"Donor {role} texture info must be an object")
    if value.get("texCoord", 0) != 0:
        raise ValueError(f"Donor {role} texture must use TEXCOORD_0")
    extensions = value.get("extensions", {})
    if not isinstance(extensions, dict) or extensions:
        raise ValueError(f"Donor {role} texture transforms are unsupported")
    return value


def image_payload(
    *,
    document: dict[str, Any],
    binary_chunk: bytes,
    image_index: int,
) -> tuple[bytes, str]:
    images = document.get("images", [])
    if (
        not isinstance(images, list)
        or image_index < 0
        or image_index >= len(images)
        or not isinstance(images[image_index], dict)
    ):
        raise ValueError("Donor texture references an invalid image")
    image = images[image_index]
    mime = image.get("mimeType")
    buffer_view_index = image.get("bufferView")
    payload: bytes
    views = document.get("bufferViews", [])
    if (
        "uri" in image
        or not isinstance(buffer_view_index, int)
        or not isinstance(views, list)
        or buffer_view_index < 0
        or buffer_view_index >= len(views)
        or not isinstance(views[buffer_view_index], dict)
    ):
        raise ValueError("Donor images must be embedded in the GLB BIN chunk")
    view = views[buffer_view_index]
    if view.get("buffer", 0) != 0:
        raise ValueError("Donor image must use the GLB BIN buffer")
    start = int(view.get("byteOffset", 0))
    length = int(view.get("byteLength", -1))
    if start < 0 or length <= 0 or start + length > len(binary_chunk):
        raise ValueError("Donor image bufferView is out of bounds")
    payload = binary_chunk[start : start + length]

    detected: str | None = None
    for candidate, (_extension, magic) in SUPPORTED_IMAGES.items():
        if payload.startswith(magic):
            detected = candidate
            break
    if detected is None:
        raise ValueError("Donor image is not PNG, JPEG, or WebP")
    if mime is not None and mime != detected:
        raise ValueError("Donor image MIME does not match its bytes")
    return payload, detected


def canonical_sampler(
    document: dict[str, Any],
    texture: dict[str, Any],
    *,
    role: str,
) -> dict[str, int]:
    texture_extensions = texture.get("extensions", {})
    if not isinstance(texture_extensions, dict) or texture_extensions:
        raise ValueError(f"Donor {role} texture extensions are unsupported")
    sampler_index = texture.get("sampler")
    if sampler_index is None:
        sampler: dict[str, Any] = {}
    else:
        samplers = document.get("samplers", [])
        if (
            not isinstance(sampler_index, int)
            or not isinstance(samplers, list)
            or sampler_index < 0
            or sampler_index >= len(samplers)
            or not isinstance(samplers[sampler_index], dict)
        ):
            raise ValueError(f"Donor {role} texture has an invalid sampler")
        sampler = samplers[sampler_index]
    sampler_extensions = sampler.get("extensions", {})
    if not isinstance(sampler_extensions, dict) or sampler_extensions:
        raise ValueError(f"Donor {role} sampler extensions are unsupported")
    normalized = {
        "magFilter": sampler.get("magFilter", GLTF_LINEAR),
        "minFilter": sampler.get("minFilter", GLTF_LINEAR_MIPMAP_LINEAR),
        "wrapS": sampler.get("wrapS", GLTF_REPEAT),
        "wrapT": sampler.get("wrapT", GLTF_REPEAT),
    }
    expected = {
        "magFilter": GLTF_LINEAR,
        "minFilter": GLTF_LINEAR_MIPMAP_LINEAR,
        "wrapS": GLTF_REPEAT,
        "wrapT": GLTF_REPEAT,
    }
    if normalized != expected:
        raise ValueError(
            f"Donor {role} sampler is not Blender 5.2 canonical "
            "linear-mipmap/repeat"
        )
    return normalized


def donor_material_source(
    path: Path,
) -> dict[str, Any]:
    document, binary_chunk = parse_glb(path)
    validate_embedded_glb(
        document,
        binary_chunk,
        label="Donor GLB",
    )
    validate_extension_declarations(
        document,
        label="Donor GLB",
        allowed={"KHR_materials_emissive_strength"},
    )
    used_materials: set[int] = set()
    meshes = document.get("meshes", [])
    if not isinstance(meshes, list) or not meshes:
        raise ValueError("Donor GLB contains no meshes")
    for mesh in meshes:
        if not isinstance(mesh, dict):
            raise ValueError("Donor mesh record is invalid")
        primitives = mesh.get("primitives", [])
        if not isinstance(primitives, list) or not primitives:
            raise ValueError("Donor mesh contains no primitives")
        for primitive in primitives:
            if not isinstance(primitive, dict):
                raise ValueError("Donor primitive is invalid")
            material_index = primitive.get("material")
            if not isinstance(material_index, int):
                raise ValueError("Every donor primitive must use one material")
            used_materials.add(material_index)
    if len(used_materials) != 1:
        raise ValueError(
            "Donor must use exactly one shared-atlas PBR material"
        )
    material_index = next(iter(used_materials))
    materials = document.get("materials", [])
    if (
        not isinstance(materials, list)
        or material_index < 0
        or material_index >= len(materials)
        or not isinstance(materials[material_index], dict)
    ):
        raise ValueError("Donor references an invalid material")
    material = materials[material_index]
    pbr = material.get("pbrMetallicRoughness", {})
    if not isinstance(pbr, dict):
        raise ValueError("Donor pbrMetallicRoughness is invalid")
    if material.get("alphaMode", "OPAQUE") != "OPAQUE":
        raise ValueError("Nilbots texture donors must be opaque")

    textures = document.get("textures", [])
    if not isinstance(textures, list):
        raise ValueError("Donor textures must be an array")
    image_records: dict[str, dict[str, Any]] = {}
    sampler_records: dict[str, dict[str, int]] = {}
    for role in (
        "baseColor",
        "metallicRoughness",
        "normal",
        "emissive",
        "occlusion",
    ):
        info = material_texture_info(material, role)
        if info is None:
            continue
        texture_index = info.get("index")
        if (
            not isinstance(texture_index, int)
            or texture_index < 0
            or texture_index >= len(textures)
            or not isinstance(textures[texture_index], dict)
        ):
            raise ValueError(f"Donor {role} references an invalid texture")
        texture = textures[texture_index]
        sampler_records[role] = canonical_sampler(
            document,
            texture,
            role=role,
        )
        source_index = texture.get("source")
        if not isinstance(source_index, int):
            raise ValueError(f"Donor {role} texture has no image source")
        payload, mime = image_payload(
            document=document,
            binary_chunk=binary_chunk,
            image_index=source_index,
        )
        image_records[role] = {
            "bytesPayload": payload,
            "bytes": len(payload),
            "mimeType": mime,
            "sha256": sha256_bytes(payload),
        }
    required = {"baseColor", "metallicRoughness", "normal"}
    if not required.issubset(image_records):
        raise ValueError(
            "Donor must provide base-color, metallic/roughness, and normal maps"
        )

    emissive_extension = material.get("extensions", {})
    if not isinstance(emissive_extension, dict):
        raise ValueError("Donor material extensions must be an object")
    unsupported_extensions = set(emissive_extension) - {
        "KHR_materials_emissive_strength"
    }
    if unsupported_extensions:
        raise ValueError(
            "Donor material extensions are unsupported: "
            + ", ".join(sorted(unsupported_extensions))
        )
    emissive_strength = 1.0
    strength_extension = emissive_extension.get(
        "KHR_materials_emissive_strength"
    )
    if strength_extension is not None:
        if not isinstance(strength_extension, dict):
            raise ValueError("Donor emissive-strength extension is invalid")
        emissive_strength = finite_number(
            strength_extension.get("emissiveStrength", 1.0),
            "emissiveStrength",
        )
    normal_info = material_texture_info(material, "normal")
    occlusion_info = material_texture_info(material, "occlusion")
    double_sided = material.get("doubleSided", False)
    if not isinstance(double_sided, bool):
        raise ValueError("Donor doubleSided must be a boolean")
    result = {
        "images": image_records,
        "samplers": sampler_records,
        "factors": {
            "baseColor": finite_vector(
                pbr.get("baseColorFactor"),
                length=4,
                label="baseColorFactor",
                default=(1.0, 1.0, 1.0, 1.0),
            ),
            "metallic": finite_number(
                pbr.get("metallicFactor", 1.0),
                "metallicFactor",
            ),
            "roughness": finite_number(
                pbr.get("roughnessFactor", 1.0),
                "roughnessFactor",
            ),
            "emissive": finite_vector(
                material.get("emissiveFactor"),
                length=3,
                label="emissiveFactor",
                default=(0.0, 0.0, 0.0),
            ),
            "emissiveStrength": emissive_strength,
            "normalScale": finite_number(
                normal_info.get("scale", 1.0) if normal_info else 1.0,
                "normalScale",
            ),
            "occlusionStrength": finite_number(
                (
                    occlusion_info.get("strength", 1.0)
                    if occlusion_info
                    else 1.0
                ),
                "occlusionStrength",
            ),
            "doubleSided": double_sided,
        },
    }
    factors = result["factors"]
    bounded = [
        *factors["baseColor"],
        factors["metallic"],
        factors["roughness"],
        factors["occlusionStrength"],
    ]
    if any(value < 0.0 or value > 1.0 for value in bounded):
        raise ValueError("Donor bounded PBR factors must stay within 0..1")
    if (
        any(value < 0.0 or value > 1.0 for value in factors["emissive"])
        or factors["emissiveStrength"] < 0.0
        or factors["normalScale"] < 0.0
    ):
        raise ValueError("Donor emissive/normal factors are outside glTF bounds")
    return result


def standalone_lossless_pbr_source(
    *,
    standalone_pbr: dict[str, dict[str, Any]],
    donor_source: dict[str, Any],
) -> dict[str, Any]:
    """Replace only donor image bytes with pinned standalone provider PNGs."""
    payloads: dict[str, bytes] = {}
    source_records: dict[str, dict[str, Any]] = {}
    for role in ("baseColor", "metallic", "roughness", "normal", "emissive"):
        record = standalone_pbr[role]
        payload = record["path"].read_bytes()
        observed = png_sha256_bytes(payload)
        if observed != record["sha256"]:
            raise ValueError(
                f"Standalone {role} PNG hash drifted before composition: "
                f"{observed} != {record['sha256']}"
            )
        payloads[role] = payload
        source_records[role] = inspect_png(
            payload,
            label=f"Standalone {role} PNG",
        )

    base_size = (
        source_records["baseColor"]["width"],
        source_records["baseColor"]["height"],
    )
    normal_size = (
        source_records["normal"]["width"],
        source_records["normal"]["height"],
    )
    if base_size != normal_size:
        raise ValueError(
            "Standalone base-color and normal PNG dimensions do not match: "
            f"{base_size} != {normal_size}"
        )

    metallic_roughness, composition = compose_metallic_roughness_png(
        metallic_payload=payloads["metallic"],
        roughness_payload=payloads["roughness"],
    )
    standalone_output_roles = (
        "baseColor",
        "metallicRoughness",
        "normal",
        "emissive",
    )
    fallback_roles = tuple(
        role for role in ("occlusion",) if role in donor_source["images"]
    )
    output_roles = standalone_output_roles + fallback_roles
    missing_samplers = set(output_roles) - set(donor_source["samplers"])
    if missing_samplers:
        raise ValueError(
            "Donor GLB does not define sampler authority for standalone role(s): "
            + ", ".join(sorted(missing_samplers))
        )

    image_payloads = {
        "baseColor": payloads["baseColor"],
        "metallicRoughness": metallic_roughness,
        "normal": payloads["normal"],
        "emissive": payloads["emissive"],
    }
    images: dict[str, dict[str, Any]] = {
        role: {
            "bytesPayload": payload,
            "bytes": len(payload),
            "mimeType": "image/png",
            "sha256": png_sha256_bytes(payload),
        }
        for role, payload in image_payloads.items()
    }
    for role in fallback_roles:
        images[role] = donor_source["images"][role]
    colorspaces = {
        "baseColor": "sRGB",
        "metallic": "Non-Color",
        "roughness": "Non-Color",
        "metallicRoughness": "Non-Color",
        "normal": "Non-Color",
        "emissive": "sRGB",
    }
    for role in fallback_roles:
        colorspaces[role] = "Non-Color"
    payload_set_sha256 = stable_hash(
        {
            "sourcePngSha256": {
                role: source_records[role]["sha256"]
                for role in sorted(source_records)
            },
            "outputTextureSha256": {
                role: images[role]["sha256"] for role in sorted(images)
            },
            "colorspaces": colorspaces,
            "metallicRoughnessCompositionSchema": composition["schema"],
        }
    )
    return {
        "images": images,
        "samplers": {
            role: donor_source["samplers"][role] for role in output_roles
        },
        "factors": donor_source["factors"],
        "payloadMode": "standalone-lossless-png",
        "payloadSetSha256": payload_set_sha256,
        "standaloneSources": source_records,
        "composition": composition,
        "colorspaces": colorspaces,
        "resolutionContract": {
            "baseColorMatchesNormal": True,
            "metallicMatchesRoughness": True,
            "emissiveMayUseIndependentResolution": True,
        },
        "standaloneOutputRoles": list(standalone_output_roles),
        "embeddedDonorFallbackRoles": list(fallback_roles),
    }


def reset_factory_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_glb_meshes(path: Path) -> list[bpy.types.Object]:
    reset_factory_scene()
    bpy.ops.import_scene.gltf(filepath=str(path))
    scene = bpy.context.scene
    scene.frame_set(0)
    for obj in scene.objects:
        animation_data = obj.animation_data
        if animation_data is None:
            continue
        animation_data.action = None
        for track in animation_data.nla_tracks:
            track.mute = True
    bpy.context.view_layer.update()
    meshes = sorted(
        (obj for obj in scene.objects if obj.type == "MESH"),
        key=lambda item: item.name,
    )
    if not meshes:
        raise ValueError(f"GLB contains no mesh objects: {path.name}")
    return meshes


def quantized(
    value: float,
    *,
    quantum: float = SURFACE_QUANTUM,
) -> int:
    if not math.isfinite(float(value)):
        raise ValueError("Surface contains a non-finite coordinate")
    return int(round(float(value) / quantum))


def capture_surface(objects: Iterable[bpy.types.Object]) -> dict[str, Any]:
    raw_triangles: list[
        tuple[
            tuple[
                tuple[float, float, float],
                tuple[float, float],
            ],
            ...,
        ]
    ] = []
    raw_triangle_objects: list[str] = []
    bounds_min = [math.inf, math.inf, math.inf]
    bounds_max = [-math.inf, -math.inf, -math.inf]
    vertex_count = 0
    for obj in objects:
        if obj.type != "MESH":
            continue
        if obj.modifiers:
            raise ValueError(
                f"Model mesh has unapplied modifiers: {obj.name}"
            )
        mesh = obj.data
        if len(mesh.uv_layers) != 1 or mesh.uv_layers.active is None:
            raise ValueError(
                f"Every model mesh must have exactly one active UV layer: {obj.name}"
            )
        mesh.calc_loop_triangles()
        uv_layer = mesh.uv_layers.active
        vertex_count += len(mesh.vertices)
        for triangle in mesh.loop_triangles:
            corners = []
            for loop_index in triangle.loops:
                vertex_index = mesh.loops[loop_index].vertex_index
                point = obj.matrix_world @ mesh.vertices[vertex_index].co
                uv = uv_layer.data[loop_index].uv
                for axis in range(3):
                    bounds_min[axis] = min(bounds_min[axis], float(point[axis]))
                    bounds_max[axis] = max(bounds_max[axis], float(point[axis]))
                corners.append(
                    (
                        (
                            float(point.x),
                            float(point.y),
                            float(point.z),
                        ),
                        (float(uv.x), float(uv.y)),
                    )
                )
            raw_triangles.append(tuple(corners))
            raw_triangle_objects.append(obj.name)
    if not raw_triangles:
        raise ValueError("Model surface contains no triangles")
    center = [
        (bounds_min[axis] + bounds_max[axis]) * 0.5
        for axis in range(3)
    ]
    maximum_span = max(
        bounds_max[axis] - bounds_min[axis]
        for axis in range(3)
    )
    if not math.isfinite(maximum_span) or maximum_span <= 0.0:
        raise ValueError("Model surface has invalid bounds")

    position_triangles: list[tuple[tuple[int, int, int], ...]] = []
    position_uv_triangles: list[
        tuple[tuple[int, int, int, int, int], ...]
    ] = []
    normalized_position_triangles: list[
        tuple[tuple[int, int, int], ...]
    ] = []
    normalized_position_uv_triangles: list[
        tuple[tuple[int, int, int, int, int], ...]
    ] = []
    uv_triangles: list[tuple[tuple[int, int], ...]] = []
    for triangle in raw_triangles:
        positions = []
        position_uvs = []
        normalized_positions = []
        normalized_position_uvs = []
        uvs = []
        for point, uv in triangle:
            position = tuple(quantized(value) for value in point)
            normalized = tuple(
                quantized(
                    (point[axis] - center[axis]) * 2.0 / maximum_span,
                    quantum=NORMALIZED_SURFACE_QUANTUM,
                )
                for axis in range(3)
            )
            uv_value = tuple(quantized(value) for value in uv)
            positions.append(position)
            position_uvs.append((*position, *uv_value))
            normalized_positions.append(normalized)
            normalized_position_uvs.append((*normalized, *uv_value))
            uvs.append(uv_value)
        position_triangles.append(tuple(sorted(positions)))
        position_uv_triangles.append(tuple(sorted(position_uvs)))
        normalized_position_triangles.append(
            tuple(sorted(normalized_positions))
        )
        normalized_position_uv_triangles.append(
            tuple(sorted(normalized_position_uvs))
        )
        uv_triangles.append(tuple(sorted(uvs)))
    position_triangles.sort()
    position_uv_triangles.sort()
    normalized_position_triangles.sort()
    normalized_position_uv_triangles.sort()
    uv_triangles.sort()
    return {
        "signature": {
            "triangles": len(position_triangles),
            "vertices": vertex_count,
            "positionSurfaceSha256": stable_hash(position_triangles),
            "positionUvSurfaceSha256": stable_hash(position_uv_triangles),
            "normalizedPositionSurfaceSha256": stable_hash(
                normalized_position_triangles
            ),
            "normalizedPositionUvSurfaceSha256": stable_hash(
                normalized_position_uv_triangles
            ),
            "uvSurfaceSha256": stable_hash(uv_triangles),
            "bounds": {
                "minimum": [quantized(value) for value in bounds_min],
                "maximum": [quantized(value) for value in bounds_max],
                "quantum": SURFACE_QUANTUM,
            },
            "normalization": {
                "center": center,
                "maximumSpan": maximum_span,
                "quantum": NORMALIZED_SURFACE_QUANTUM,
            },
        },
        "triangles": raw_triangles,
        "triangleObjects": raw_triangle_objects,
    }


def surface_signature(objects: Iterable[bpy.types.Object]) -> dict[str, Any]:
    return capture_surface(objects)["signature"]


def canonical_oriented_uv_triangle(
    triangle: tuple[
        tuple[tuple[float, float, float], tuple[float, float]],
        ...,
    ],
) -> tuple[
    tuple[tuple[int, int], ...],
    tuple[tuple[float, float, float], ...],
    int,
]:
    if len(triangle) != 3:
        raise ValueError("Surface evidence contains a non-triangle")
    corners = [
        (
            tuple(quantized(value) for value in uv),
            point,
            uv,
        )
        for point, uv in triangle
    ]
    if len({corner[0] for corner in corners}) != 3:
        raise ValueError(
            "Surface UV triangle has an ambiguous or degenerate corner"
        )
    signed_twice_area = (
        (corners[1][2][0] - corners[0][2][0])
        * (corners[2][2][1] - corners[0][2][1])
        - (corners[1][2][1] - corners[0][2][1])
        * (corners[2][2][0] - corners[0][2][0])
    )
    if abs(signed_twice_area) <= CORRESPONDENCE_UV_AREA_EPSILON:
        raise ValueError("Surface UV triangle is degenerate")
    winding = 1 if signed_twice_area > 0.0 else -1
    rotations = [
        corners[index:] + corners[:index]
        for index in range(3)
    ]
    canonical = min(
        rotations,
        key=lambda value: tuple(corner[0] for corner in value),
    )
    return (
        tuple(corner[0] for corner in canonical),
        tuple(corner[1] for corner in canonical),
        winding,
    )


def uv_triangle_area(
    triangle: tuple[
        tuple[tuple[float, float, float], tuple[float, float]],
        ...,
    ],
) -> float:
    uvs = [corner[1] for corner in triangle]
    return abs(
        (uvs[1][0] - uvs[0][0]) * (uvs[2][1] - uvs[0][1])
        - (uvs[1][1] - uvs[0][1]) * (uvs[2][0] - uvs[0][0])
    ) * 0.5


def spatial_triangle_area(
    triangle: tuple[
        tuple[tuple[float, float, float], tuple[float, float]],
        ...,
    ],
) -> float:
    points = [np.asarray(corner[0], dtype=np.float64) for corner in triangle]
    return float(
        np.linalg.norm(
            np.cross(points[1] - points[0], points[2] - points[0])
        )
        * 0.5
    )


def oriented_uv_surface_evidence(
    capture: dict[str, Any],
    *,
    label: str,
    allow_source_degenerate: bool,
) -> tuple[
    dict[tuple[tuple[int, int], ...], dict[str, Any]],
    dict[str, Any],
]:
    records: dict[tuple[tuple[int, int], ...], dict[str, Any]] = {}
    discarded = []
    triangle_objects = capture.get("triangleObjects")
    if not isinstance(triangle_objects, list) or len(triangle_objects) != len(
        capture["triangles"]
    ):
        raise ValueError(f"{label} surface evidence lost triangle ownership")
    for triangle, object_name in zip(
        capture["triangles"], triangle_objects, strict=True
    ):
        try:
            key, positions, winding = canonical_oriented_uv_triangle(triangle)
        except ValueError as error:
            message = str(error)
            if not allow_source_degenerate or message not in {
                "Surface UV triangle has an ambiguous or degenerate corner",
                "Surface UV triangle is degenerate",
            }:
                raise
            discarded.append(
                {
                    "reason": message,
                    "uvArea": uv_triangle_area(triangle),
                    "spatialArea": spatial_triangle_area(triangle),
                    "objectName": object_name,
                }
            )
            continue
        if key in records:
            raise ValueError(
                f"{label} contains an ambiguous duplicate oriented UV triangle"
            )
        records[key] = {
            "positions": positions,
            "winding": winding,
            "uvArea": uv_triangle_area(triangle),
            "spatialArea": spatial_triangle_area(triangle),
            "objectName": object_name,
        }
    return records, {
        "count": len(discarded),
        "uvArea": sum(record["uvArea"] for record in discarded),
        "spatialArea": sum(
            record["spatialArea"] for record in discarded
        ),
        "reasons": dict(
            sorted(
                {
                    reason: sum(
                        1 for record in discarded if record["reason"] == reason
                    )
                    for reason in {record["reason"] for record in discarded}
                }.items()
            )
        ),
        "objects": dict(
            sorted(
                {
                    object_name: sum(
                        1
                        for record in discarded
                        if record["objectName"] == object_name
                    )
                    for object_name in {
                        record["objectName"] for record in discarded
                    }
                }.items()
            )
        ),
        "objectMetrics": {
            object_name: {
                "triangles": sum(
                    1
                    for record in discarded
                    if record["objectName"] == object_name
                ),
                "uvArea": sum(
                    record["uvArea"]
                    for record in discarded
                    if record["objectName"] == object_name
                ),
                "spatialArea": sum(
                    record["spatialArea"]
                    for record in discarded
                    if record["objectName"] == object_name
                ),
            }
            for object_name in sorted(
                {record["objectName"] for record in discarded}
            )
        },
    }


def orthogonal_fit(
    source: np.ndarray,
    donor: np.ndarray,
    rotation: np.ndarray,
) -> dict[str, Any]:
    rotated = source @ rotation.T
    rotated_center = rotated.mean(axis=0)
    donor_center = donor.mean(axis=0)
    centered_rotated = rotated - rotated_center
    centered_donor = donor - donor_center
    denominator = float(np.sum(centered_rotated * centered_rotated))
    if denominator <= 0.0:
        raise ValueError("Surface correspondence has zero spatial variance")
    scale = float(
        np.sum(centered_rotated * centered_donor) / denominator
    )
    if not math.isfinite(scale) or scale <= 0.0:
        raise ValueError("Surface correspondence requires a negative scale")
    translation = donor_center - scale * rotated_center
    predicted = scale * rotated + translation
    residuals = np.linalg.norm(predicted - donor, axis=1)
    return {
        "rotationArray": rotation,
        "scale": scale,
        "translationArray": translation,
        "predicted": predicted,
        "residuals": residuals,
        "determinant": float(np.linalg.det(rotation)),
    }


def general_similarity_fit(
    source: np.ndarray,
    donor: np.ndarray,
    *,
    allow_reflection: bool,
) -> dict[str, Any]:
    centered_source = source - source.mean(axis=0)
    centered_donor = donor - donor.mean(axis=0)
    covariance = centered_source.T @ centered_donor
    left, _singular, right_transpose = np.linalg.svd(covariance)
    rotation = right_transpose.T @ left.T
    if not allow_reflection and np.linalg.det(rotation) < 0.0:
        right_transpose = right_transpose.copy()
        right_transpose[-1, :] *= -1.0
        rotation = right_transpose.T @ left.T
    return orthogonal_fit(source, donor, rotation)


def axis_similarity_fits(
    source: np.ndarray,
    donor: np.ndarray,
) -> tuple[dict[str, Any], dict[str, Any]]:
    fits = []
    for permutation in permutations(range(3)):
        for signs in product((-1.0, 1.0), repeat=3):
            rotation = np.zeros((3, 3), dtype=np.float64)
            for target_axis, source_axis in enumerate(permutation):
                rotation[target_axis, source_axis] = signs[target_axis]
            try:
                fit = orthogonal_fit(source, donor, rotation)
            except ValueError:
                continue
            fit["permutation"] = list(permutation)
            fit["signs"] = [int(value) for value in signs]
            fits.append(fit)
    if not fits or not any(fit["determinant"] > 0.0 for fit in fits):
        raise ValueError("No positive-scale signed-axis surface fit exists")
    best_any = min(
        fits,
        key=lambda fit: float(np.sqrt(np.mean(fit["residuals"] ** 2))),
    )
    best_proper = min(
        (fit for fit in fits if fit["determinant"] > 0.0),
        key=lambda fit: float(np.sqrt(np.mean(fit["residuals"] ** 2))),
    )
    return best_any, best_proper


def fit_report(
    fit: dict[str, Any],
    *,
    span: float,
) -> dict[str, Any]:
    normalized = fit["residuals"] / span
    return {
        "scale": fit["scale"],
        "translation": [
            float(value) for value in fit["translationArray"]
        ],
        "rotation": [
            [float(value) for value in row]
            for row in fit["rotationArray"]
        ],
        "determinant": fit["determinant"],
        "reflection": fit["determinant"] < 0.0,
        "residualNormalizedToDonorSpan": {
            "maximum": float(np.max(normalized)),
            "rms": float(np.sqrt(np.mean(normalized * normalized))),
            "mean": float(np.mean(normalized)),
            "p95": float(np.quantile(normalized, 0.95)),
            "p99": float(np.quantile(normalized, 0.99)),
        },
    }


def validate_provider_surface_correspondence(
    source: dict[str, Any],
    donor: dict[str, Any],
) -> dict[str, Any]:
    source_signature = source["signature"]
    donor_signature = donor["signature"]
    if donor_signature["triangles"] > source_signature["triangles"]:
        raise ValueError(
            "Donor surface introduced triangles beyond the uploaded source"
        )
    if donor_signature["vertices"] > source_signature["vertices"]:
        raise ValueError(
            "Donor surface introduced vertices beyond weld-only changes"
        )
    source_map, source_degenerate = oriented_uv_surface_evidence(
        source,
        label="Uploaded source",
        allow_source_degenerate=True,
    )
    donor_map, donor_degenerate = oriented_uv_surface_evidence(
        donor,
        label="Donor",
        allow_source_degenerate=False,
    )
    donor_only_keys = set(donor_map) - set(source_map)
    if donor_only_keys:
        raise ValueError(
            "Donor introduced oriented UV triangles outside the uploaded source"
        )
    source_only_keys = set(source_map) - set(donor_map)
    ordered_keys = sorted(donor_map)
    if not ordered_keys:
        raise ValueError("Donor surface contains no pairable UV triangles")

    source_only_triangle_count = (
        len(source_only_keys) + source_degenerate["count"]
    )
    expected_source_only = (
        source_signature["triangles"] - donor_signature["triangles"]
    )
    if source_only_triangle_count != expected_source_only:
        raise ValueError(
            "Donor triangle deficit is not fully explained by source-only UV "
            "triangles"
        )
    source_uv_area = (
        sum(record["uvArea"] for record in source_map.values())
        + source_degenerate["uvArea"]
    )
    if not math.isfinite(source_uv_area) or source_uv_area <= 0.0:
        raise ValueError("Uploaded source has invalid total UV area")
    source_only_uv_area = (
        sum(source_map[key]["uvArea"] for key in source_only_keys)
        + source_degenerate["uvArea"]
    )
    source_only_spatial_area = (
        sum(source_map[key]["spatialArea"] for key in source_only_keys)
        + source_degenerate["spatialArea"]
    )
    source_only_by_object: dict[str, dict[str, float | int]] = defaultdict(
        lambda: {"triangles": 0, "uvArea": 0.0, "spatialArea": 0.0}
    )
    for key in source_only_keys:
        record = source_map[key]
        metrics = source_only_by_object[record["objectName"]]
        metrics["triangles"] += 1
        metrics["uvArea"] += record["uvArea"]
        metrics["spatialArea"] += record["spatialArea"]
    for object_name, record in source_degenerate["objectMetrics"].items():
        metrics = source_only_by_object[object_name]
        metrics["triangles"] += record["triangles"]
        metrics["uvArea"] += record["uvArea"]
        metrics["spatialArea"] += record["spatialArea"]
    source_span = float(source_signature["normalization"]["maximumSpan"])
    if not math.isfinite(source_span) or source_span <= 0.0:
        raise ValueError("Uploaded source span is invalid")
    source_only = {
        "triangles": source_only_triangle_count,
        "orientedUvTriangles": len(source_only_keys),
        "degenerateUvTriangles": source_degenerate["count"],
        "triangleRatio": (
            source_only_triangle_count / source_signature["triangles"]
        ),
        "uvArea": source_only_uv_area,
        "uvAreaRatio": source_only_uv_area / source_uv_area,
        "spatialArea": source_only_spatial_area,
        "spatialAreaNormalizedToSourceSpanSquared": (
            source_only_spatial_area / (source_span * source_span)
        ),
        "orientedUvTriangleSha256": stable_hash(
            sorted(source_only_keys)
        ),
        "degenerateUvEvidence": source_degenerate,
        "bySourceObject": {
            object_name: {
                "triangles": int(metrics["triangles"]),
                "uvArea": float(metrics["uvArea"]),
                "spatialArea": float(metrics["spatialArea"]),
            }
            for object_name, metrics in sorted(source_only_by_object.items())
        },
    }
    source_only["withinBounds"] = bool(
        source_only["triangles"] <= MAX_SOURCE_ONLY_TRIANGLES
        and source_only["uvAreaRatio"] <= MAX_SOURCE_ONLY_UV_AREA_RATIO
        and source_only[
            "spatialAreaNormalizedToSourceSpanSquared"
        ]
        <= MAX_SOURCE_ONLY_NORMALIZED_SPATIAL_AREA
        and source_only["degenerateUvTriangles"]
        <= MAX_SOURCE_DEGENERATE_UV_TRIANGLES
    )
    if not source_only["withinBounds"]:
        raise ValueError(
            "Donor source-only micro-surface exceeds the bounded allowance: "
            + json.dumps(source_only, sort_keys=True)
        )

    source_points = []
    donor_points = []
    uv_winding_mismatches = 0
    for key in ordered_keys:
        source_record = source_map[key]
        donor_record = donor_map[key]
        if source_record["winding"] != donor_record["winding"]:
            uv_winding_mismatches += 1
        source_points.extend(source_record["positions"])
        donor_points.extend(donor_record["positions"])
    if uv_winding_mismatches:
        raise ValueError(
            "Donor UV triangle winding differs from uploaded source"
        )
    source_array = np.asarray(source_points, dtype=np.float64)
    donor_array = np.asarray(donor_points, dtype=np.float64)
    if source_array.shape != donor_array.shape or source_array.shape[1:] != (3,):
        raise ValueError("Surface correspondence point arrays are malformed")

    donor_span = float(donor_signature["normalization"]["maximumSpan"])
    if not math.isfinite(donor_span) or donor_span <= 0.0:
        raise ValueError("Donor surface span is invalid")
    unrestricted = general_similarity_fit(
        source_array,
        donor_array,
        allow_reflection=True,
    )
    proper = general_similarity_fit(
        source_array,
        donor_array,
        allow_reflection=False,
    )
    best_axis_any, best_axis_proper = axis_similarity_fits(
        source_array,
        donor_array,
    )
    proper_report = fit_report(proper, span=donor_span)
    residual = proper_report["residualNormalizedToDonorSpan"]
    normalized_corner_residuals = proper["residuals"] / donor_span
    outlier_triangle_indices = {
        index // 3
        for index, value in enumerate(normalized_corner_residuals)
        if value > SIMILARITY_MAX_NORMALIZED_RESIDUAL
    }
    outlier_uv_area = sum(
        source_map[ordered_keys[index]]["uvArea"]
        for index in outlier_triangle_indices
    )
    paired_uv_area = sum(
        source_map[key]["uvArea"] for key in ordered_keys
    )
    outlier_spatial_area = sum(
        source_map[ordered_keys[index]]["spatialArea"]
        for index in outlier_triangle_indices
    )
    paired_spatial_area = sum(
        source_map[key]["spatialArea"] for key in ordered_keys
    )
    if (
        proper_report["reflection"]
        or residual["maximum"] > SIMILARITY_MAX_NORMALIZED_RESIDUAL
        or residual["rms"] > SIMILARITY_RMS_NORMALIZED_RESIDUAL
        or residual["p99"] > SIMILARITY_P99_NORMALIZED_RESIDUAL
    ):
        raise ValueError(
            "Donor surface is not one tight orientation-preserving global "
            "similarity transform of the uploaded source: "
            f"maximum normalized residual={residual['maximum']:.9g}, "
            f"rms={residual['rms']:.9g}, "
            f"p95={residual['p95']:.9g}, p99={residual['p99']:.9g}, "
            f"outlier triangles={len(outlier_triangle_indices)}, "
            f"outlier UV area ratio="
            f"{outlier_uv_area / paired_uv_area:.9g}, "
            f"outlier spatial area ratio="
            f"{outlier_spatial_area / paired_spatial_area:.9g}, "
            f"outlier spatial area/span^2="
            f"{outlier_spatial_area / (source_span * source_span):.9g}, "
            f"thresholds=({SIMILARITY_MAX_NORMALIZED_RESIDUAL:.9g}, "
            f"{SIMILARITY_RMS_NORMALIZED_RESIDUAL:.9g}, "
            f"{SIMILARITY_P99_NORMALIZED_RESIDUAL:.9g})"
        )

    predicted = proper["predicted"]
    geometry_winding_mismatches = 0
    minimum_normal_dot = 1.0
    for index in range(0, len(predicted), 3):
        source_a, source_b, source_c = predicted[index : index + 3]
        donor_a, donor_b, donor_c = donor_array[index : index + 3]
        source_normal = np.cross(source_b - source_a, source_c - source_a)
        donor_normal = np.cross(donor_b - donor_a, donor_c - donor_a)
        source_length = float(np.linalg.norm(source_normal))
        donor_length = float(np.linalg.norm(donor_normal))
        if source_length <= 1.0e-15 or donor_length <= 1.0e-15:
            raise ValueError(
                "Surface correspondence contains a degenerate 3D triangle"
            )
        normal_dot = float(
            np.dot(source_normal, donor_normal)
            / (source_length * donor_length)
        )
        minimum_normal_dot = min(minimum_normal_dot, normal_dot)
        if normal_dot <= 0.0:
            geometry_winding_mismatches += 1
    if geometry_winding_mismatches:
        raise ValueError(
            "Donor geometric triangle winding differs from uploaded source"
        )

    source_edges = sorted(
        tuple(sorted((key[index], key[(index + 1) % 3])))
        for key in ordered_keys
        for index in range(3)
    )
    unrestricted_report = fit_report(unrestricted, span=donor_span)
    best_axis_any_report = fit_report(best_axis_any, span=donor_span)
    best_axis_any_report.update(
        {
            "permutation": best_axis_any["permutation"],
            "signs": best_axis_any["signs"],
        }
    )
    best_axis_proper_report = fit_report(
        best_axis_proper,
        span=donor_span,
    )
    best_axis_proper_report.update(
        {
            "permutation": best_axis_proper["permutation"],
            "signs": best_axis_proper["signs"],
        }
    )
    return {
        "triangles": len(ordered_keys),
        "sourceTriangles": source_signature["triangles"],
        "donorTriangles": donor_signature["triangles"],
        "pairedCorners": len(source_points),
        "ambiguousOrDegenerateUvTriangles": donor_degenerate["count"],
        "orientedUvTriangleSha256": stable_hash(ordered_keys),
        "sourceOrientedUvTriangleSha256": stable_hash(sorted(source_map)),
        "orientedUvEdgeSha256": stable_hash(source_edges),
        "sourceOnly": source_only,
        "uvWindingParity": 1,
        "uvWindingMismatches": 0,
        "geometryWindingParity": 1,
        "geometryWindingMismatches": 0,
        "minimumGeometryNormalDot": minimum_normal_dot,
        "sourceVertices": source_signature["vertices"],
        "donorVertices": donor_signature["vertices"],
        "providerWeldedVertexDelta": (
            source_signature["vertices"] - donor_signature["vertices"]
        ),
        "acceptedTransform": proper_report,
        "unrestrictedTransform": unrestricted_report,
        "bestSignedAxisTransform": best_axis_any_report,
        "bestOrientationPreservingAxisTransform": (
            best_axis_proper_report
        ),
        "reflectionRejected": True,
        "thresholds": {
            "maximumNormalizedResidual": (
                SIMILARITY_MAX_NORMALIZED_RESIDUAL
            ),
            "rmsNormalizedResidual": (
                SIMILARITY_RMS_NORMALIZED_RESIDUAL
            ),
            "p99NormalizedResidual": (
                SIMILARITY_P99_NORMALIZED_RESIDUAL
            ),
            "maximumSourceOnlyTriangles": MAX_SOURCE_ONLY_TRIANGLES,
            "maximumSourceOnlyUvAreaRatio": (
                MAX_SOURCE_ONLY_UV_AREA_RATIO
            ),
            "maximumSourceOnlySpatialAreaNormalizedToSourceSpanSquared": (
                MAX_SOURCE_ONLY_NORMALIZED_SPATIAL_AREA
            ),
            "maximumSourceDegenerateUvTriangles": (
                MAX_SOURCE_DEGENERATE_UV_TRIANGLES
            ),
        },
    }


def material_role(material: bpy.types.Material | None) -> str | None:
    if material is None:
        return None
    direct = material.get("nilbotsRole")
    if isinstance(direct, str):
        return direct
    extras = material.get("extras")
    if extras is not None and hasattr(extras, "get"):
        nested = extras.get("nilbotsRole")
        if isinstance(nested, str):
            return nested
    return None


def model_collection(name: str) -> bpy.types.Collection:
    collection = bpy.data.collections.get(name)
    if collection is None:
        raise ValueError(f"Authored model collection is missing: {name}")
    return collection


def model_objects(collection: bpy.types.Collection) -> list[bpy.types.Object]:
    objects = sorted(collection.all_objects, key=lambda item: item.name)
    if not objects:
        raise ValueError("Authored model collection is empty")
    names = [obj.name for obj in objects]
    if len(names) != len(set(names)):
        raise ValueError("Authored model collection has duplicate object names")
    return objects


def body_and_accent_materials(
    objects: Iterable[bpy.types.Object],
) -> tuple[list[bpy.types.Material], list[bpy.types.Material]]:
    body: set[bpy.types.Material] = set()
    accent: set[bpy.types.Material] = set()
    for obj in objects:
        if obj.type != "MESH":
            continue
        if len(obj.data.materials) == 0:
            raise ValueError(f"Mesh has no material slots: {obj.name}")
        for polygon in obj.data.polygons:
            if polygon.material_index >= len(obj.data.materials):
                raise ValueError(f"Mesh has an invalid material index: {obj.name}")
            material = obj.data.materials[polygon.material_index]
            if material is None:
                raise ValueError(f"Mesh uses an empty material slot: {obj.name}")
            if material_role(material) == TEAM_ROLE:
                accent.add(material)
            else:
                body.add(material)
    if not body:
        raise ValueError("Authored collection has no body material")
    if not accent:
        raise ValueError(
            "Authored collection has no semantic texture-free team-accent material"
        )
    for material in accent:
        if material.use_nodes and material.node_tree is not None:
            if any(
                node.bl_idname == "ShaderNodeTexImage"
                and node.image is not None
                for node in material.node_tree.nodes
            ):
                raise ValueError(
                    f"Team-accent material is not texture-free: {material.name}"
                )
    return (
        sorted(body, key=lambda item: item.name),
        sorted(accent, key=lambda item: item.name),
    )


def validate_material_scope(
    materials: Iterable[bpy.types.Material],
    *,
    scoped_objects: Iterable[bpy.types.Object],
) -> dict[str, list[str]]:
    scoped = set(scoped_objects)
    records: dict[str, list[str]] = {}
    for material in materials:
        consumers = sorted(
            obj.name
            for obj in bpy.data.objects
            if obj.type == "MESH"
            and any(slot == material for slot in obj.data.materials)
        )
        outside = [
            obj.name
            for obj in bpy.data.objects
            if obj.type == "MESH"
            and obj not in scoped
            and any(slot == material for slot in obj.data.materials)
        ]
        if outside:
            raise ValueError(
                f"Body material {material.name!r} is shared outside the "
                f"model collection by: {', '.join(sorted(outside))}"
            )
        records[material.name] = consumers
    return records


def triangle_area_2d(triangle: tuple[tuple[float, float], ...]) -> float:
    (ax, ay), (bx, by), (cx, cy) = triangle
    return abs((bx - ax) * (cy - ay) - (by - ay) * (cx - ax)) * 0.5


def strict_triangle_overlap(
    left: tuple[tuple[float, float], ...],
    right: tuple[tuple[float, float], ...],
) -> bool:
    for triangle in (left, right):
        for index in range(3):
            start = triangle[index]
            end = triangle[(index + 1) % 3]
            axis = (-(end[1] - start[1]), end[0] - start[0])
            length = math.hypot(axis[0], axis[1])
            if length <= UV_EPSILON:
                continue
            axis = (axis[0] / length, axis[1] / length)
            left_projection = [
                point[0] * axis[0] + point[1] * axis[1] for point in left
            ]
            right_projection = [
                point[0] * axis[0] + point[1] * axis[1] for point in right
            ]
            overlap = min(max(left_projection), max(right_projection)) - max(
                min(left_projection),
                min(right_projection),
            )
            if overlap <= UV_EPSILON:
                return False
    return True


def validate_shared_uv_atlas(
    objects: Iterable[bpy.types.Object],
) -> dict[str, Any]:
    objects = list(objects)
    triangles: list[
        tuple[str, int, tuple[tuple[float, float], ...], float]
    ] = []
    micro_uv_triangles: list[dict[str, Any]] = []
    layer_names: set[str] = set()
    bounds_min = [math.inf, math.inf, math.inf]
    bounds_max = [-math.inf, -math.inf, -math.inf]
    for obj in objects:
        if obj.type != "MESH":
            continue
        for vertex in obj.data.vertices:
            point = obj.matrix_world @ vertex.co
            for axis in range(3):
                bounds_min[axis] = min(bounds_min[axis], float(point[axis]))
                bounds_max[axis] = max(bounds_max[axis], float(point[axis]))
    for obj in objects:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        if len(mesh.uv_layers) != 1 or mesh.uv_layers.active is None:
            raise ValueError(
                f"Every model mesh must have one UV atlas layer: {obj.name}"
            )
        layer = mesh.uv_layers.active
        layer_names.add(layer.name)
        mesh.calc_loop_triangles()
        for loop_triangle in mesh.loop_triangles:
            polygon = mesh.polygons[loop_triangle.polygon_index]
            material = mesh.materials[polygon.material_index]
            if material_role(material) == TEAM_ROLE:
                continue
            triangle = tuple(
                (
                    float(layer.data[loop_index].uv.x),
                    float(layer.data[loop_index].uv.y),
                )
                for loop_index in loop_triangle.loops
            )
            if any(
                coordinate < -UV_EPSILON or coordinate > 1.0 + UV_EPSILON
                for point in triangle
                for coordinate in point
            ):
                raise ValueError(
                    f"Body UV escaped the shared 0..1 atlas: {obj.name}"
                )
            uv_area = triangle_area_2d(triangle)
            points = tuple(
                obj.matrix_world @ mesh.vertices[vertex_index].co
                for vertex_index in loop_triangle.vertices
            )
            spatial_area = float(
                (
                    (points[1] - points[0]).cross(points[2] - points[0])
                ).length
                * 0.5
            )
            if uv_area <= ATLAS_MICRO_UV_TRIANGLE_AREA:
                micro_uv_triangles.append(
                    {
                        "object": obj.name,
                        "polygon": int(loop_triangle.polygon_index),
                        "uvArea": uv_area,
                        "spatialArea": spatial_area,
                    }
                )
                continue
            triangles.append(
                (
                    obj.name,
                    int(loop_triangle.polygon_index),
                    triangle,
                    spatial_area,
                )
            )
    if len(layer_names) != 1:
        raise ValueError("Model meshes do not share one UV layer name")
    if not triangles:
        raise ValueError("Shared body UV atlas contains no triangles")

    maximum_span = max(
        bounds_max[axis] - bounds_min[axis] for axis in range(3)
    )
    if not math.isfinite(maximum_span) or maximum_span <= 0.0:
        raise ValueError("Model surface has invalid bounds")
    micro_uv_spatial_area = sum(
        record["spatialArea"] for record in micro_uv_triangles
    )
    micro_uv_area = sum(
        record["uvArea"] for record in micro_uv_triangles
    )
    regular_uv_area = sum(triangle_area_2d(record[2]) for record in triangles)
    total_uv_area = regular_uv_area + micro_uv_area
    micro_uv_evidence = {
        "triangles": len(micro_uv_triangles),
        "uvArea": micro_uv_area,
        "uvAreaRatio": micro_uv_area / total_uv_area,
        "spatialArea": micro_uv_spatial_area,
        "spatialAreaNormalizedToModelSpanSquared": (
            micro_uv_spatial_area / (maximum_span * maximum_span)
        ),
        "triangleAreaThreshold": ATLAS_MICRO_UV_TRIANGLE_AREA,
        "maximumTriangles": MAX_ATLAS_MICRO_UV_TRIANGLES,
        "maximumUvAreaRatio": MAX_ATLAS_MICRO_UV_AREA_RATIO,
        "maximumSpatialAreaNormalizedToModelSpanSquared": (
            MAX_ATLAS_MICRO_NORMALIZED_SPATIAL_AREA
        ),
    }
    micro_uv_evidence["withinBounds"] = bool(
        micro_uv_evidence["triangles"] <= MAX_ATLAS_MICRO_UV_TRIANGLES
        and micro_uv_evidence["uvAreaRatio"]
        <= MAX_ATLAS_MICRO_UV_AREA_RATIO
        and micro_uv_evidence[
            "spatialAreaNormalizedToModelSpanSquared"
        ]
        <= MAX_ATLAS_MICRO_NORMALIZED_SPATIAL_AREA
    )
    if not micro_uv_evidence["withinBounds"]:
        raise ValueError(
            "Body micro-UV surface exceeds the bounded allowance: "
            f"triangles={micro_uv_evidence['triangles']}, "
            f"UV area ratio={micro_uv_evidence['uvAreaRatio']:.9g}, "
            "spatial area/span^2="
            f"{micro_uv_evidence['spatialAreaNormalizedToModelSpanSquared']:.9g}"
        )

    grid_size = max(8, min(128, math.ceil(math.sqrt(len(triangles)))))
    buckets: dict[tuple[int, int], list[int]] = defaultdict(list)
    tested_pairs: set[tuple[int, int]] = set()
    micro_overlap_pairs: list[dict[str, Any]] = []
    for index, (_name, _polygon, triangle, _spatial_area) in enumerate(
        triangles
    ):
        minimum_u = min(point[0] for point in triangle)
        maximum_u = max(point[0] for point in triangle)
        minimum_v = min(point[1] for point in triangle)
        maximum_v = max(point[1] for point in triangle)
        first_x = max(0, min(grid_size - 1, int(minimum_u * grid_size)))
        last_x = max(
            0,
            min(
                grid_size - 1,
                int(max(0.0, maximum_u - UV_EPSILON) * grid_size),
            ),
        )
        first_y = max(0, min(grid_size - 1, int(minimum_v * grid_size)))
        last_y = max(
            0,
            min(
                grid_size - 1,
                int(max(0.0, maximum_v - UV_EPSILON) * grid_size),
            ),
        )
        for x in range(first_x, last_x + 1):
            for y in range(first_y, last_y + 1):
                bucket = buckets[(x, y)]
                for other_index in bucket:
                    pair = (other_index, index)
                    if pair in tested_pairs:
                        continue
                    tested_pairs.add(pair)
                    if strict_triangle_overlap(
                        triangles[other_index][2],
                        triangle,
                    ):
                        left = triangles[other_index]
                        right = triangles[index]
                        left_area = triangle_area_2d(left[2])
                        right_area = triangle_area_2d(right[2])
                        if (
                            max(left_area, right_area)
                            <= MAX_ATLAS_MICRO_OVERLAP_TRIANGLE_AREA
                        ):
                            micro_overlap_pairs.append(
                                {
                                    "leftObject": left[0],
                                    "leftPolygon": left[1],
                                    "leftUvArea": left_area,
                                    "rightObject": right[0],
                                    "rightPolygon": right[1],
                                    "rightUvArea": right_area,
                                    "leftTriangleIndex": other_index,
                                    "rightTriangleIndex": index,
                                    "boundedUvArea": min(
                                        left_area,
                                        right_area,
                                    ),
                                }
                            )
                            continue
                        raise ValueError(
                            "Shared UV atlas overlaps between "
                            f"{left[0]} polygon {left[1]} and "
                            f"{right[0]} polygon {right[1]} "
                            f"(UV areas {triangle_area_2d(left[2]):.9g}, "
                            f"{triangle_area_2d(right[2]):.9g})"
                        )
                bucket.append(index)
    micro_overlap_uv_area = sum(
        pair["boundedUvArea"] for pair in micro_overlap_pairs
    )
    micro_overlap_triangle_indices = {
        triangle_index
        for pair in micro_overlap_pairs
        for triangle_index in (
            pair["leftTriangleIndex"],
            pair["rightTriangleIndex"],
        )
    }
    micro_overlap_spatial_area = sum(
        triangles[index][3] for index in micro_overlap_triangle_indices
    )
    micro_overlap_evidence = {
        "pairs": len(micro_overlap_pairs),
        "uniqueTriangles": len(micro_overlap_triangle_indices),
        "boundedUvArea": micro_overlap_uv_area,
        "boundedUvAreaRatio": micro_overlap_uv_area / total_uv_area,
        "uniqueTriangleSpatialArea": micro_overlap_spatial_area,
        "uniqueTriangleSpatialAreaNormalizedToModelSpanSquared": (
            micro_overlap_spatial_area / (maximum_span * maximum_span)
        ),
        "maximumTriangleArea": MAX_ATLAS_MICRO_OVERLAP_TRIANGLE_AREA,
        "maximumPairs": MAX_ATLAS_MICRO_OVERLAP_PAIRS,
        "maximumBoundedUvAreaRatio": (
            MAX_ATLAS_MICRO_OVERLAP_UV_AREA_RATIO
        ),
        "maximumUniqueTriangleSpatialAreaNormalizedToModelSpanSquared": (
            MAX_ATLAS_MICRO_OVERLAP_NORMALIZED_SPATIAL_AREA
        ),
    }
    micro_overlap_evidence["withinBounds"] = bool(
        micro_overlap_evidence["pairs"] <= MAX_ATLAS_MICRO_OVERLAP_PAIRS
        and micro_overlap_evidence["boundedUvAreaRatio"]
        <= MAX_ATLAS_MICRO_OVERLAP_UV_AREA_RATIO
        and micro_overlap_evidence[
            "uniqueTriangleSpatialAreaNormalizedToModelSpanSquared"
        ]
        <= MAX_ATLAS_MICRO_OVERLAP_NORMALIZED_SPATIAL_AREA
    )
    if not micro_overlap_evidence["withinBounds"]:
        raise ValueError(
            "Body micro-UV overlap exceeds the bounded allowance: "
            f"pairs={micro_overlap_evidence['pairs']}, "
            "bounded UV area ratio="
            f"{micro_overlap_evidence['boundedUvAreaRatio']:.9g}, "
            "unique triangle spatial area/span^2="
            f"{micro_overlap_evidence['uniqueTriangleSpatialAreaNormalizedToModelSpanSquared']:.9g}"
        )
    return {
        "layer": next(iter(layer_names)),
        "bodyTriangles": len(triangles),
        "ignoredMicroUvSurface": micro_uv_evidence,
        "ignoredMicroOverlap": micro_overlap_evidence,
        "overlapPairs": 0,
        "bounds": [0.0, 0.0, 1.0, 1.0],
    }


def matrix_values(matrix: Any) -> list[list[float]]:
    return [[float(value) for value in row] for row in matrix]


def mesh_geometry_digest(mesh: bpy.types.Mesh) -> str:
    digest = hashlib.sha256()
    digest.update(mesh.name.encode("utf-8"))
    for vertex in mesh.vertices:
        digest.update(
            struct.pack(
                "<3d",
                float(vertex.co.x),
                float(vertex.co.y),
                float(vertex.co.z),
            )
        )
    for edge in mesh.edges:
        digest.update(struct.pack("<2I", *[int(value) for value in edge.vertices]))
    for loop in mesh.loops:
        digest.update(
            struct.pack(
                "<2I",
                int(loop.vertex_index),
                int(loop.edge_index),
            )
        )
    for polygon in mesh.polygons:
        digest.update(
            struct.pack(
                "<4I?",
                int(polygon.loop_start),
                int(polygon.loop_total),
                int(polygon.material_index),
                len(polygon.vertices),
                bool(polygon.use_smooth),
            )
        )
    return digest.hexdigest()


def mesh_uv_digest(mesh: bpy.types.Mesh) -> str:
    digest = hashlib.sha256()
    for layer in mesh.uv_layers:
        digest.update(layer.name.encode("utf-8"))
        digest.update(struct.pack("<?", layer == mesh.uv_layers.active))
        for loop in layer.data:
            digest.update(
                struct.pack("<2d", float(loop.uv.x), float(loop.uv.y))
            )
    return digest.hexdigest()


def fcurves_for_action(action: bpy.types.Action) -> list[Any]:
    curves: list[Any] = []
    seen: set[int] = set()

    def add(items: Iterable[Any]) -> None:
        for curve in items:
            pointer = int(curve.as_pointer())
            if pointer not in seen:
                seen.add(pointer)
                curves.append(curve)

    add(getattr(action, "fcurves", ()))
    for layer in getattr(action, "layers", ()):
        for strip in getattr(layer, "strips", ()):
            for channelbag in getattr(strip, "channelbags", ()):
                add(getattr(channelbag, "fcurves", ()))
    return sorted(
        curves,
        key=lambda curve: (curve.data_path, int(curve.array_index)),
    )


def animation_modifier_record(modifier: Any) -> dict[str, Any]:
    values: dict[str, Any] = {}
    for definition in modifier.bl_rna.properties:
        name = definition.identifier
        if name in {"rna_type", "name", "type", "active", "is_valid"}:
            continue
        if definition.type not in {"BOOLEAN", "INT", "FLOAT", "STRING", "ENUM"}:
            continue
        value = getattr(modifier, name)
        if isinstance(value, set):
            values[name] = sorted(value)
        elif bool(getattr(definition, "is_array", False)):
            values[name] = [float(item) for item in value]
        elif isinstance(value, (bool, int, float, str)):
            values[name] = value
    return {
        "type": modifier.type,
        "values": values,
    }


def fcurve_record(curve: Any) -> dict[str, Any]:
    points = []
    for keyframe in curve.keyframe_points:
        points.append(
            {
                "co": [float(keyframe.co.x), float(keyframe.co.y)],
                "interpolation": keyframe.interpolation,
                "type": keyframe.type,
                "easing": keyframe.easing,
                "back": float(keyframe.back),
                "amplitude": float(keyframe.amplitude),
                "period": float(keyframe.period),
                "handleLeft": [
                    float(keyframe.handle_left.x),
                    float(keyframe.handle_left.y),
                ],
                "handleRight": [
                    float(keyframe.handle_right.x),
                    float(keyframe.handle_right.y),
                ],
                "handleLeftType": keyframe.handle_left_type,
                "handleRightType": keyframe.handle_right_type,
            }
        )
    return {
        "dataPath": curve.data_path,
        "arrayIndex": int(curve.array_index),
        "group": curve.group.name if curve.group else None,
        "extrapolation": curve.extrapolation,
        "autoSmoothing": curve.auto_smoothing,
        "mute": bool(curve.mute),
        "lock": bool(curve.lock),
        "colorMode": curve.color_mode,
        "color": [float(value) for value in curve.color],
        "keyframes": points,
        "sampledPoints": [
            [float(point.co.x), float(point.co.y)]
            for point in curve.sampled_points
        ],
        "modifiers": [
            animation_modifier_record(modifier)
            for modifier in curve.modifiers
        ],
    }


def action_records() -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    actions: list[dict[str, Any]] = []
    endpoints: list[dict[str, Any]] = []
    for action in sorted(bpy.data.actions, key=lambda item: item.name):
        curve_records = [
            fcurve_record(curve)
            for curve in fcurves_for_action(action)
        ]
        endpoint_records: list[dict[str, Any]] = []
        for curve in curve_records:
            points = curve["keyframes"]
            endpoint_records.append(
                {
                    "dataPath": curve["dataPath"],
                    "arrayIndex": curve["arrayIndex"],
                    "first": points[0]["co"] if points else None,
                    "last": points[-1]["co"] if points else None,
                }
            )
        layers = []
        for layer in action.layers:
            strips = []
            for strip in layer.strips:
                channelbags = []
                for channelbag in strip.channelbags:
                    slot = channelbag.slot
                    channelbags.append(
                        {
                            "slot": slot.identifier if slot else None,
                            "slotHandle": int(channelbag.slot_handle),
                            "curves": [
                                fcurve_record(curve)
                                for curve in sorted(
                                    channelbag.fcurves,
                                    key=lambda item: (
                                        item.data_path,
                                        int(item.array_index),
                                    ),
                                )
                            ],
                        }
                    )
                strips.append(
                    {
                        "type": strip.type,
                        "channelbags": channelbags,
                    }
                )
            layers.append({"name": layer.name, "strips": strips})
        actions.append(
            {
                "name": action.name,
                "frameRange": [
                    float(action.frame_range[0]),
                    float(action.frame_range[1]),
                ],
                "manualFrameRange": [
                    float(action.frame_start),
                    float(action.frame_end),
                ],
                "useFrameRange": bool(action.use_frame_range),
                "useCyclic": bool(action.use_cyclic),
                "slots": [
                    {
                        "identifier": slot.identifier,
                        "displayName": slot.name_display,
                        "targetIdType": slot.target_id_type,
                        "handle": int(slot.handle),
                    }
                    for slot in action.slots
                ],
                "layers": layers,
                "poseMarkers": [
                    {
                        "name": marker.name,
                        "frame": int(marker.frame),
                    }
                    for marker in action.pose_markers
                ],
                "curves": curve_records,
            }
        )
        endpoints.append(
            {"name": action.name, "curves": endpoint_records}
        )
    return actions, endpoints


def nla_strip_record(strip: Any) -> dict[str, Any]:
    action_slot = getattr(strip, "action_slot", None)
    return {
        "name": strip.name,
        "type": strip.type,
        "action": strip.action.name if strip.action else None,
        "actionSlot": (
            action_slot.identifier if action_slot is not None else None
        ),
        "actionSlotHandle": int(getattr(strip, "action_slot_handle", 0)),
        "frameStart": float(strip.frame_start),
        "frameEnd": float(strip.frame_end),
        "actionFrameStart": float(strip.action_frame_start),
        "actionFrameEnd": float(strip.action_frame_end),
        "blendType": strip.blend_type,
        "extrapolation": strip.extrapolation,
        "blendIn": float(strip.blend_in),
        "blendOut": float(strip.blend_out),
        "useAutoBlend": bool(strip.use_auto_blend),
        "repeat": float(strip.repeat),
        "scale": float(strip.scale),
        "influence": float(strip.influence),
        "stripTime": float(strip.strip_time),
        "useAnimatedInfluence": bool(strip.use_animated_influence),
        "useAnimatedTime": bool(strip.use_animated_time),
        "useAnimatedTimeCyclic": bool(strip.use_animated_time_cyclic),
        "mute": bool(strip.mute),
        "useReverse": bool(strip.use_reverse),
        "useSyncLength": bool(strip.use_sync_length),
        "fcurves": [
            fcurve_record(curve)
            for curve in sorted(
                strip.fcurves,
                key=lambda item: (item.data_path, int(item.array_index)),
            )
        ],
        "modifiers": [
            animation_modifier_record(modifier)
            for modifier in strip.modifiers
        ],
        "strips": [
            nla_strip_record(child)
            for child in strip.strips
        ],
    }


def animation_bindings(objects: Iterable[bpy.types.Object]) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    for obj in objects:
        data = obj.animation_data
        if data is None:
            continue
        tracks = []
        for track in data.nla_tracks:
            tracks.append(
                {
                    "name": track.name,
                    "mute": bool(track.mute),
                    "isSolo": bool(track.is_solo),
                    "lock": bool(track.lock),
                    "strips": [nla_strip_record(strip) for strip in track.strips],
                }
            )
        action_slot = getattr(data, "action_slot", None)
        records.append(
            {
                "object": obj.name,
                "action": data.action.name if data.action else None,
                "actionSlot": (
                    action_slot.identifier if action_slot is not None else None
                ),
                "actionSlotHandle": int(
                    getattr(data, "action_slot_handle", 0)
                ),
                "actionExtrapolation": data.action_extrapolation,
                "actionBlendType": data.action_blend_type,
                "actionInfluence": float(data.action_influence),
                "useNla": bool(data.use_nla),
                "drivers": [
                    fcurve_record(curve)
                    for curve in sorted(
                        data.drivers,
                        key=lambda item: (
                            item.data_path,
                            int(item.array_index),
                        ),
                    )
                ],
                "tracks": tracks,
            }
        )
    return records


def constraint_record(constraint: Any) -> dict[str, Any]:
    values: dict[str, Any] = {}
    for definition in constraint.bl_rna.properties:
        name = definition.identifier
        if name in {"rna_type", "name", "type", "active", "is_valid"}:
            continue
        value = getattr(constraint, name)
        if definition.type == "POINTER":
            if value is not None and hasattr(value, "name"):
                values[name] = value.name
            continue
        if definition.type not in {"BOOLEAN", "INT", "FLOAT", "STRING", "ENUM"}:
            continue
        if isinstance(value, set):
            values[name] = sorted(value)
        elif bool(getattr(definition, "is_array", False)):
            values[name] = [float(item) for item in value]
        elif isinstance(value, (bool, int, float, str)):
            values[name] = value
    return {
        "name": constraint.name,
        "type": constraint.type,
        "values": values,
    }


def authority_snapshot(
    objects: list[bpy.types.Object],
    *,
    bind_frame: int,
) -> dict[str, Any]:
    scene = bpy.context.scene
    original_frame = scene.frame_current
    scene.frame_set(bind_frame)
    bpy.context.view_layer.update()
    transforms = [
        {
            "name": obj.name,
            "type": obj.type,
            "parent": obj.parent.name if obj.parent else None,
            "parentType": obj.parent_type,
            "parentBone": obj.parent_bone,
            "matrixLocal": matrix_values(obj.matrix_local),
            "matrixBasis": matrix_values(obj.matrix_basis),
            "matrixParentInverse": matrix_values(obj.matrix_parent_inverse),
            "location": [float(value) for value in obj.location],
            "deltaLocation": [
                float(value) for value in obj.delta_location
            ],
            "rotationMode": obj.rotation_mode,
            "rotationEuler": [float(value) for value in obj.rotation_euler],
            "rotationQuaternion": [
                float(value) for value in obj.rotation_quaternion
            ],
            "rotationAxisAngle": [
                float(value) for value in obj.rotation_axis_angle
            ],
            "deltaRotationEuler": [
                float(value) for value in obj.delta_rotation_euler
            ],
            "deltaRotationQuaternion": [
                float(value) for value in obj.delta_rotation_quaternion
            ],
            "scale": [float(value) for value in obj.scale],
            "deltaScale": [float(value) for value in obj.delta_scale],
            "constraints": [
                constraint_record(constraint)
                for constraint in obj.constraints
            ],
            "customProperties": {
                key: property_value(obj[key])
                for key in sorted(obj.keys())
                if key != "_RNA_UI"
            },
        }
        for obj in objects
    ]
    scene.frame_set(original_frame)
    bpy.context.view_layer.update()

    meshes = [
        {
            "object": obj.name,
            "mesh": obj.data.name,
            "geometrySha256": mesh_geometry_digest(obj.data),
            "uvSha256": mesh_uv_digest(obj.data),
        }
        for obj in objects
        if obj.type == "MESH"
    ]
    actions, endpoints = action_records()
    bindings = animation_bindings(objects)
    nla_track_names = sorted(
        {
            track["name"]
            for binding in bindings
            for track in binding["tracks"]
            if track["name"]
        }
    )
    return {
        "objectNames": [obj.name for obj in objects],
        "objectTransformsSha256": stable_hash(transforms),
        "meshGeometrySha256": stable_hash(meshes),
        "meshUvSha256": stable_hash(
            [
                {
                    "object": item["object"],
                    "mesh": item["mesh"],
                    "uvSha256": item["uvSha256"],
                }
                for item in meshes
            ]
        ),
        "actionNames": [action["name"] for action in actions],
        "nlaTrackNames": nla_track_names,
        "actionsSha256": stable_hash(actions),
        "actionEndpointsSha256": stable_hash(endpoints),
        "animationBindingsSha256": stable_hash(bindings),
        "objects": len(objects),
        "meshes": len(meshes),
        "actions": len(actions),
        "keyframes": sum(
            len(curve["keyframes"])
            for action in actions
            for curve in action["curves"]
        ),
    }


def property_value(value: Any) -> Any:
    if isinstance(value, (str, bool, int, float)) or value is None:
        return value
    if hasattr(value, "to_list"):
        return [property_value(item) for item in value.to_list()]
    if hasattr(value, "keys"):
        return {
            str(key): property_value(value[key])
            for key in sorted(value.keys())
        }
    if isinstance(value, Iterable):
        return [property_value(item) for item in value]
    return str(value)


def socket_default(socket: Any) -> Any:
    if not hasattr(socket, "default_value"):
        return None
    return property_value(socket.default_value)


def writable_scalar_properties(
    value: Any,
    *,
    excluded: set[str],
) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for definition in value.bl_rna.properties:
        name = definition.identifier
        if (
            name in excluded
            or definition.is_readonly
            or definition.type
            not in {"BOOLEAN", "INT", "FLOAT", "STRING", "ENUM"}
        ):
            continue
        item = getattr(value, name)
        if isinstance(item, set):
            result[name] = sorted(item)
        elif bool(getattr(definition, "is_array", False)):
            result[name] = [property_value(component) for component in item]
        else:
            result[name] = property_value(item)
    return result


def material_fingerprint(material: bpy.types.Material) -> str:
    nodes: list[dict[str, Any]] = []
    links: list[dict[str, Any]] = []
    if material.use_nodes and material.node_tree is not None:
        for node in sorted(
            material.node_tree.nodes,
            key=lambda item: (item.name, item.bl_idname),
        ):
            nodes.append(
                {
                    "name": node.name,
                    "type": node.bl_idname,
                    "label": node.label,
                    "location": [
                        float(node.location.x),
                        float(node.location.y),
                    ],
                    "properties": writable_scalar_properties(
                        node,
                        excluded={
                            "rna_type",
                            "name",
                            "label",
                            "select",
                            "show_options",
                            "show_preview",
                            "show_texture",
                        },
                    ),
                    "inputs": [
                        {
                            "name": socket.name,
                            "identifier": socket.identifier,
                            "enabled": bool(socket.enabled),
                            "hide": bool(socket.hide),
                            "default": socket_default(socket),
                        }
                        for socket in node.inputs
                    ],
                    "outputs": [
                        {
                            "name": socket.name,
                            "identifier": socket.identifier,
                            "enabled": bool(socket.enabled),
                            "hide": bool(socket.hide),
                            "default": socket_default(socket),
                        }
                        for socket in node.outputs
                    ],
                    "nodeTree": (
                        node.node_tree.name
                        if node.bl_idname == "ShaderNodeGroup"
                        and node.node_tree is not None
                        else None
                    ),
                    "image": (
                        {
                            "name": node.image.name,
                            "packed": node.image.packed_file is not None,
                        }
                        if node.bl_idname == "ShaderNodeTexImage"
                        and node.image is not None
                        else None
                    ),
                }
            )
        for link in material.node_tree.links:
            links.append(
                {
                    "fromNode": link.from_node.name,
                    "fromSocket": link.from_socket.name,
                    "toNode": link.to_node.name,
                    "toSocket": link.to_socket.name,
                }
            )
    custom = {
        key: property_value(material[key])
        for key in sorted(material.keys())
        if key != "_RNA_UI"
    }
    return stable_hash(
        {
            "name": material.name,
            "useNodes": bool(material.use_nodes),
            "diffuseColor": [float(value) for value in material.diffuse_color],
            "metallic": float(material.metallic),
            "roughness": float(material.roughness),
            "properties": writable_scalar_properties(
                material,
                excluded={
                    "rna_type",
                    "name",
                    "use_nodes",
                    "diffuse_color",
                    "metallic",
                    "roughness",
                    "tag",
                    "use_fake_user",
                    "use_extra_user",
                },
            ),
            "custom": custom,
            "nodes": nodes,
            "links": sorted(
                links,
                key=lambda item: (
                    item["fromNode"],
                    item["fromSocket"],
                    item["toNode"],
                    item["toSocket"],
                ),
            ),
        }
    )


def write_texture_sources(
    source: dict[str, Any],
    *,
    texture_dir: Path,
    repo_root: Path,
) -> tuple[dict[str, Path], list[dict[str, Any]]]:
    texture_dir.mkdir()
    paths: dict[str, Path] = {}
    records: list[dict[str, Any]] = []
    written_hashes: dict[str, Path] = {}
    for role in (
        "baseColor",
        "metallicRoughness",
        "normal",
        "emissive",
        "occlusion",
    ):
        image = source["images"].get(role)
        if image is None:
            continue
        digest = image["sha256"]
        extension = SUPPORTED_IMAGES[image["mimeType"]][0]
        existing = written_hashes.get(digest)
        if existing is None:
            path = texture_dir / f"{role}-{digest[:16]}{extension}"
            path.write_bytes(image["bytesPayload"])
            written_hashes[digest] = path
        else:
            path = existing
        paths[role] = path
        records.append(
            {
                "role": role,
                "path": path.relative_to(repo_root).as_posix(),
                "bytes": path.stat().st_size,
                "sha256": digest,
                "mimeType": image["mimeType"],
            }
        )
    return paths, records


def principled_socket(
    node: bpy.types.Node,
    *names: str,
) -> bpy.types.NodeSocket:
    for name in names:
        socket = node.inputs.get(name)
        if socket is not None:
            return socket
    raise ValueError(f"Principled BSDF is missing socket(s): {names}")


def enabled_socket(
    sockets: Iterable[bpy.types.NodeSocket],
    name: str,
) -> bpy.types.NodeSocket:
    matches = [
        socket
        for socket in sockets
        if socket.name == name and bool(getattr(socket, "enabled", True))
    ]
    if len(matches) != 1:
        raise ValueError(f"Expected one enabled node socket named {name!r}")
    return matches[0]


def canonical_color_multiply(
    tree: bpy.types.NodeTree,
    *,
    name: str,
    label: str,
    source: bpy.types.NodeSocket,
    factor: tuple[float, float, float, float],
    location: tuple[float, float],
) -> bpy.types.NodeSocket:
    mix = tree.nodes.new("ShaderNodeMix")
    mix.name = name
    mix.label = label
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.clamp_factor = True
    mix.clamp_result = False
    mix.location = location
    enabled_socket(mix.inputs, "Factor").default_value = 1.0
    enabled_socket(mix.inputs, "B").default_value = factor
    tree.links.new(source, enabled_socket(mix.inputs, "A"))
    return enabled_socket(mix.outputs, "Result")


def gltf_material_output_group() -> bpy.types.NodeTree:
    group = bpy.data.node_groups.get("glTF Material Output")
    if group is None:
        group = bpy.data.node_groups.new(
            "glTF Material Output",
            "ShaderNodeTree",
        )
        group.interface.new_socket(
            name="Occlusion",
            in_out="INPUT",
            socket_type="NodeSocketFloat",
        )
    if group.bl_idname != "ShaderNodeTree":
        raise ValueError("glTF Material Output is not a shader node group")
    inputs = [
        item
        for item in group.interface.items_tree
        if item.item_type == "SOCKET"
        and item.in_out == "INPUT"
        and item.name == "Occlusion"
    ]
    if len(inputs) != 1:
        raise ValueError(
            "glTF Material Output must expose one Occlusion input"
        )
    return group


def connect_occlusion_for_gltf(
    tree: bpy.types.NodeTree,
    *,
    source: bpy.types.NodeSocket,
    strength: float,
) -> None:
    separate = tree.nodes.new("ShaderNodeSeparateColor")
    separate.name = "NB.SeparateOcclusion"
    separate.mode = "RGB"
    separate.location = (-620, -820)
    tree.links.new(source, separate.inputs["Color"])

    mix = tree.nodes.new("ShaderNodeMix")
    mix.name = "NB.OcclusionStrength"
    mix.data_type = "RGBA"
    mix.blend_type = "MIX"
    mix.clamp_factor = True
    mix.clamp_result = False
    mix.location = (-380, -820)
    enabled_socket(mix.inputs, "Factor").default_value = strength
    enabled_socket(mix.inputs, "A").default_value = (1.0, 1.0, 1.0, 1.0)
    tree.links.new(separate.outputs["Red"], enabled_socket(mix.inputs, "B"))

    output_group = tree.nodes.new("ShaderNodeGroup")
    output_group.name = "NB.glTFMaterialOutput"
    output_group.node_tree = gltf_material_output_group()
    output_group.location = (-100, -820)
    tree.links.new(
        enabled_socket(mix.outputs, "Result"),
        output_group.inputs["Occlusion"],
    )


def load_packed_image(
    path: Path,
    *,
    role: str,
) -> bpy.types.Image:
    image = bpy.data.images.load(str(path), check_existing=False)
    image.name = f"NB_Donor_{role}"
    image.colorspace_settings.name = (
        "sRGB" if role in {"baseColor", "emissive"} else "Non-Color"
    )
    image.pack()
    return image


def rebuild_body_material(
    material: bpy.types.Material,
    *,
    factors: dict[str, Any],
    images: dict[str, bpy.types.Image],
    donor_sha256: str,
    payload_set_sha256: str | None = None,
) -> None:
    if material_role(material) == TEAM_ROLE:
        raise ValueError("Refusing to rebuild a team-accent material")
    material.use_nodes = True
    tree = material.node_tree
    if tree is None:
        raise ValueError(f"Material has no node tree: {material.name}")
    tree.nodes.clear()

    output = tree.nodes.new("ShaderNodeOutputMaterial")
    output.name = "NB.Output"
    output.location = (900, 0)
    shader = tree.nodes.new("ShaderNodeBsdfPrincipled")
    shader.name = "NB.DonorPBR"
    shader.location = (620, 0)
    tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])

    base_texture = tree.nodes.new("ShaderNodeTexImage")
    base_texture.name = "NB.BaseColor"
    base_texture.label = "Donor Base Color"
    base_texture.image = images["baseColor"]
    base_texture.location = (-850, 280)
    tree.links.new(
        canonical_color_multiply(
            tree,
            name="NB.BaseColorFactor",
            label="Donor Base Color Factor",
            source=base_texture.outputs["Color"],
            factor=tuple(factors["baseColor"]),
            location=(-260, 280),
        ),
        principled_socket(shader, "Base Color"),
    )

    mr_texture = tree.nodes.new("ShaderNodeTexImage")
    mr_texture.name = "NB.MetallicRoughness"
    mr_texture.label = "Donor Metallic (B) / Roughness (G)"
    mr_texture.image = images["metallicRoughness"]
    mr_texture.location = (-850, -20)
    separate = tree.nodes.new("ShaderNodeSeparateColor")
    separate.name = "NB.SeparateMetallicRoughness"
    separate.mode = "RGB"
    separate.location = (-620, -20)
    tree.links.new(mr_texture.outputs["Color"], separate.inputs["Color"])
    roughness = tree.nodes.new("ShaderNodeMath")
    roughness.name = "NB.RoughnessFactor"
    roughness.operation = "MULTIPLY"
    roughness.inputs[1].default_value = factors["roughness"]
    roughness.location = (-260, -60)
    tree.links.new(separate.outputs["Green"], roughness.inputs[0])
    tree.links.new(
        roughness.outputs[0],
        principled_socket(shader, "Roughness"),
    )
    metallic = tree.nodes.new("ShaderNodeMath")
    metallic.name = "NB.MetallicFactor"
    metallic.operation = "MULTIPLY"
    metallic.inputs[1].default_value = factors["metallic"]
    metallic.location = (-260, 40)
    tree.links.new(separate.outputs["Blue"], metallic.inputs[0])
    tree.links.new(
        metallic.outputs[0],
        principled_socket(shader, "Metallic"),
    )

    normal_texture = tree.nodes.new("ShaderNodeTexImage")
    normal_texture.name = "NB.Normal"
    normal_texture.label = "Donor Tangent Normal"
    normal_texture.image = images["normal"]
    normal_texture.location = (-850, -340)
    normal_map = tree.nodes.new("ShaderNodeNormalMap")
    normal_map.name = "NB.NormalScale"
    normal_map.inputs["Strength"].default_value = factors["normalScale"]
    normal_map.location = (-260, -340)
    tree.links.new(normal_texture.outputs["Color"], normal_map.inputs["Color"])
    tree.links.new(
        normal_map.outputs["Normal"],
        principled_socket(shader, "Normal"),
    )

    emission_socket = principled_socket(shader, "Emission Color", "Emission")
    if "emissive" in images:
        emission_texture = tree.nodes.new("ShaderNodeTexImage")
        emission_texture.name = "NB.Emissive"
        emission_texture.label = "Donor Emissive"
        emission_texture.image = images["emissive"]
        emission_texture.location = (-850, -600)
        tree.links.new(
            canonical_color_multiply(
                tree,
                name="NB.EmissiveFactor",
                label="Donor Emissive Factor",
                source=emission_texture.outputs["Color"],
                factor=(
                    factors["emissive"][0],
                    factors["emissive"][1],
                    factors["emissive"][2],
                    1.0,
                ),
                location=(-260, -600),
            ),
            emission_socket,
        )
    else:
        emission_socket.default_value = (
            factors["emissive"][0],
            factors["emissive"][1],
            factors["emissive"][2],
            1.0,
        )
    principled_socket(
        shader,
        "Emission Strength",
    ).default_value = factors["emissiveStrength"]

    if "occlusion" in images:
        occlusion_texture = tree.nodes.new("ShaderNodeTexImage")
        occlusion_texture.name = "NB.Occlusion"
        occlusion_texture.label = (
            "Donor Occlusion"
        )
        occlusion_texture.image = images["occlusion"]
        occlusion_texture.location = (-850, -820)
        occlusion_texture["nilbotsPbrRole"] = "occlusion"
        occlusion_texture["strength"] = factors["occlusionStrength"]
        connect_occlusion_for_gltf(
            tree,
            source=occlusion_texture.outputs["Color"],
            strength=factors["occlusionStrength"],
        )

    material.diffuse_color = tuple(factors["baseColor"])
    material.metallic = factors["metallic"]
    material.roughness = factors["roughness"]
    material.use_backface_culling = not factors["doubleSided"]
    material["nilbotsTextureSource"] = "texture-only-donor"
    material["nilbotsTextureDonorSha256"] = donor_sha256
    if payload_set_sha256 is not None:
        material["nilbotsTexturePayloadMode"] = "standalone-lossless-png"
        material["nilbotsTexturePayloadSetSha256"] = payload_set_sha256
    material["nilbotsTeamColorOwnership"] = "renderer"


def validate_rebuilt_body_materials(
    materials: Iterable[bpy.types.Material],
    *,
    donor_sha256: str,
    expected_roles: set[str],
    payload_set_sha256: str | None = None,
) -> dict[str, list[str]]:
    node_names = {
        "baseColor": "NB.BaseColor",
        "metallicRoughness": "NB.MetallicRoughness",
        "normal": "NB.Normal",
        "emissive": "NB.Emissive",
        "occlusion": "NB.Occlusion",
    }
    records: dict[str, list[str]] = {}
    for material in materials:
        if material_role(material) == TEAM_ROLE:
            raise ValueError("Team-accent entered rebuilt body validation")
        if (
            material.get("nilbotsTextureSource") != "texture-only-donor"
            or material.get("nilbotsTextureDonorSha256") != donor_sha256
        ):
            raise ValueError(
                f"Body material lost donor provenance: {material.name}"
            )
        if payload_set_sha256 is not None and (
            material.get("nilbotsTexturePayloadMode")
            != "standalone-lossless-png"
            or material.get("nilbotsTexturePayloadSetSha256")
            != payload_set_sha256
        ):
            raise ValueError(
                "Body material lost standalone payload provenance: "
                f"{material.name}"
            )
        if not material.use_nodes or material.node_tree is None:
            raise ValueError(f"Body material lost its nodes: {material.name}")
        found: list[str] = []
        for role in sorted(expected_roles):
            node = material.node_tree.nodes.get(node_names[role])
            if (
                node is None
                or node.bl_idname != "ShaderNodeTexImage"
                or node.image is None
                or node.image.packed_file is None
            ):
                raise ValueError(
                    f"Body material lost packed {role} map: {material.name}"
                )
            found.append(role)
        records[material.name] = found
    return records


def export_animated_glb(
    *,
    path: Path,
    collection: bpy.types.Collection,
) -> None:
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=False,
        collection=collection.name,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_cameras=False,
        export_lights=False,
        export_extras=True,
        export_animations=True,
        export_animation_mode="NLA_TRACKS",
        export_merge_animation="NLA_TRACK",
        export_nla_strips=True,
        export_force_sampling=True,
        export_frame_range=False,
        export_normals=True,
        export_apply=True,
        export_yup=True,
    )


def canonicalize_exported_material_factors(
    path: Path,
    *,
    body_material_names: list[str],
    factors: dict[str, Any],
    expected_texture_roles: set[str],
) -> None:
    document, binary_chunk = parse_glb(path)
    validate_embedded_glb(
        document,
        binary_chunk,
        label="Fresh output GLB",
    )
    materials = document.get("materials", [])
    if not isinstance(materials, list):
        raise ValueError("Fresh output GLB materials are malformed")
    by_name = {
        material.get("name"): material
        for material in materials
        if isinstance(material, dict)
        and isinstance(material.get("name"), str)
    }
    if set(body_material_names) - set(by_name):
        raise ValueError(
            "Fresh output GLB omitted a body material before factor sealing"
        )
    for name in body_material_names:
        material = by_name[name]
        pbr = material.get("pbrMetallicRoughness")
        if not isinstance(pbr, dict):
            raise ValueError(
                f"Fresh output body material PBR is malformed: {name}"
            )
        pbr["baseColorFactor"] = list(factors["baseColor"])
        pbr["metallicFactor"] = factors["metallic"]
        pbr["roughnessFactor"] = factors["roughness"]
        material["emissiveFactor"] = list(factors["emissive"])
        normal = material.get("normalTexture")
        if not isinstance(normal, dict):
            raise ValueError(
                f"Fresh output body material lost its normal map: {name}"
            )
        normal["scale"] = factors["normalScale"]
        if (
            "occlusion" in expected_texture_roles
            and "occlusionTexture" not in material
        ):
            raise ValueError(
                f"Fresh output body material lost its occlusion map: {name}"
            )
        occlusion = material.get("occlusionTexture")
        if isinstance(occlusion, dict):
            occlusion["strength"] = factors["occlusionStrength"]
        material["doubleSided"] = factors["doubleSided"]
        extensions = material.get("extensions", {})
        if not isinstance(extensions, dict):
            raise ValueError(
                f"Fresh output body material extensions are malformed: {name}"
            )
        if math.isclose(
            factors["emissiveStrength"],
            1.0,
            rel_tol=0.0,
            abs_tol=0.0,
        ):
            extensions.pop("KHR_materials_emissive_strength", None)
        else:
            extensions["KHR_materials_emissive_strength"] = {
                "emissiveStrength": factors["emissiveStrength"],
            }
        if extensions:
            material["extensions"] = extensions
        else:
            material.pop("extensions", None)

    used_material_extensions = {
        extension
        for material in materials
        if isinstance(material, dict)
        for extension in material.get("extensions", {})
    }
    declarations = set(document.get("extensionsUsed", []))
    if "KHR_materials_emissive_strength" in used_material_extensions:
        declarations.add("KHR_materials_emissive_strength")
    else:
        declarations.discard("KHR_materials_emissive_strength")
    if declarations:
        document["extensionsUsed"] = sorted(declarations)
    else:
        document.pop("extensionsUsed", None)
    required = set(document.get("extensionsRequired", []))
    required.intersection_update(declarations)
    if required:
        document["extensionsRequired"] = sorted(required)
    else:
        document.pop("extensionsRequired", None)
    write_glb(
        path,
        document=document,
        binary_chunk=binary_chunk,
    )


GLTF_COMPONENTS = {
    5120: ("b", 1),
    5121: ("B", 1),
    5122: ("h", 2),
    5123: ("H", 2),
    5125: ("I", 4),
    5126: ("f", 4),
}
GLTF_TYPE_COMPONENTS = {
    "SCALAR": 1,
    "VEC2": 2,
    "VEC3": 3,
    "VEC4": 4,
    "MAT2": 4,
    "MAT3": 9,
    "MAT4": 16,
}


def accessor_layout(
    document: dict[str, Any],
    binary_chunk: bytes,
    accessor_index: int,
) -> tuple[dict[str, Any], str, int, int, int, int]:
    accessors = document.get("accessors", [])
    views = document.get("bufferViews", [])
    if (
        not isinstance(accessors, list)
        or accessor_index < 0
        or accessor_index >= len(accessors)
        or not isinstance(accessors[accessor_index], dict)
    ):
        raise ValueError("GLB references an invalid accessor")
    accessor = accessors[accessor_index]
    if "sparse" in accessor:
        raise ValueError("Sparse accessors are unsupported for authority proof")
    component = GLTF_COMPONENTS.get(accessor.get("componentType"))
    component_count = GLTF_TYPE_COMPONENTS.get(accessor.get("type"))
    view_index = accessor.get("bufferView")
    count = accessor.get("count")
    if (
        component is None
        or component_count is None
        or not isinstance(view_index, int)
        or not isinstance(count, int)
        or isinstance(count, bool)
        or count < 0
        or not isinstance(views, list)
        or view_index < 0
        or view_index >= len(views)
        or not isinstance(views[view_index], dict)
    ):
        raise ValueError("GLB accessor shape is invalid")
    view = views[view_index]
    view_start = view.get("byteOffset", 0)
    view_length = view.get("byteLength")
    accessor_offset = accessor.get("byteOffset", 0)
    item_size = component_count * component[1]
    stride = view.get("byteStride", item_size)
    if (
        not isinstance(view_start, int)
        or isinstance(view_start, bool)
        or not isinstance(view_length, int)
        or isinstance(view_length, bool)
        or not isinstance(accessor_offset, int)
        or isinstance(accessor_offset, bool)
        or not isinstance(stride, int)
        or isinstance(stride, bool)
        or view_start < 0
        or view_length < 0
        or accessor_offset < 0
        or stride < item_size
        or stride % component[1] != 0
    ):
        raise ValueError("GLB accessor byte layout is invalid")
    start = view_start + accessor_offset
    end = start if count == 0 else start + (count - 1) * stride + item_size
    if (
        start < view_start
        or end > view_start + view_length
        or end > len(binary_chunk)
    ):
        raise ValueError("GLB accessor exceeds its bufferView")
    return accessor, component[0], component_count, count, start, stride


def accessor_record(
    document: dict[str, Any],
    binary_chunk: bytes,
    accessor_index: int,
) -> dict[str, Any]:
    accessor, _format, component_count, count, start, stride = accessor_layout(
        document,
        binary_chunk,
        accessor_index,
    )
    component_size = GLTF_COMPONENTS[accessor["componentType"]][1]
    item_size = component_count * component_size
    element_bytes = b"".join(
        binary_chunk[start + index * stride : start + index * stride + item_size]
        for index in range(count)
    )
    return {
        "componentType": accessor["componentType"],
        "type": accessor["type"],
        "count": count,
        "normalized": bool(accessor.get("normalized", False)),
        "minimum": accessor.get("min"),
        "maximum": accessor.get("max"),
        "name": accessor.get("name"),
        "extras": accessor.get("extras"),
        "elementBytesSha256": sha256_bytes(element_bytes),
    }


def accessor_values(
    document: dict[str, Any],
    binary_chunk: bytes,
    accessor_index: int,
) -> list[float | list[float]]:
    accessor, component_format, component_count, count, start, stride = (
        accessor_layout(
            document,
            binary_chunk,
            accessor_index,
        )
    )
    if accessor.get("componentType") != 5126:
        raise ValueError("Animation accessors must contain 32-bit floats")
    if accessor.get("normalized", False):
        raise ValueError("Normalized or sparse animation accessors are unsupported")
    values: list[float | list[float]] = []
    for index in range(count):
        components = list(
            struct.unpack_from(
                f"<{component_count}{component_format}",
                binary_chunk,
                start + index * stride,
            )
        )
        if any(not math.isfinite(float(value)) for value in components):
            raise ValueError("Animation accessor contains a non-finite value")
        values.append(components[0] if component_count == 1 else components)
    return values


def glb_mesh_records(
    document: dict[str, Any],
    binary_chunk: bytes,
    *,
    include_material_bindings: bool = True,
) -> list[dict[str, Any]]:
    meshes = document.get("meshes", [])
    materials = document.get("materials", [])
    if not isinstance(meshes, list) or not isinstance(materials, list):
        raise ValueError("Output GLB meshes or materials are malformed")
    material_names = []
    for material in materials:
        if (
            not isinstance(material, dict)
            or not isinstance(material.get("name"), str)
            or not material["name"]
        ):
            raise ValueError("Output GLB material identity is invalid")
        material_names.append(material["name"])
    names = [
        mesh.get("name") if isinstance(mesh, dict) else None
        for mesh in meshes
    ]
    if (
        any(not isinstance(name, str) or not name for name in names)
        or len(set(names)) != len(names)
    ):
        raise ValueError("Output GLB meshes must have unique non-empty names")
    records = []
    for mesh_index, mesh in enumerate(meshes):
        primitives = mesh.get("primitives", [])
        if not isinstance(primitives, list) or not primitives:
            raise ValueError("Output GLB mesh has no primitives")
        primitive_records = []
        for primitive in primitives:
            if not isinstance(primitive, dict):
                raise ValueError("Output GLB primitive is malformed")
            extensions = primitive.get("extensions", {})
            if not isinstance(extensions, dict) or extensions:
                raise ValueError("Output GLB primitive extensions are unsupported")
            attributes = primitive.get("attributes", {})
            targets = primitive.get("targets", [])
            material_index = primitive.get("material")
            if (
                not isinstance(attributes, dict)
                or any(
                    not isinstance(name, str)
                    or not isinstance(accessor, int)
                    for name, accessor in attributes.items()
                )
                or not isinstance(targets, list)
                or not isinstance(material_index, int)
                or material_index < 0
                or material_index >= len(material_names)
            ):
                raise ValueError("Output GLB primitive authority is invalid")
            target_records = []
            for target in targets:
                if (
                    not isinstance(target, dict)
                    or any(
                        not isinstance(name, str)
                        or not isinstance(accessor, int)
                        for name, accessor in target.items()
                    )
                ):
                    raise ValueError("Output GLB morph target is malformed")
                target_records.append(
                    {
                        name: accessor_record(
                            document,
                            binary_chunk,
                            accessor,
                        )
                        for name, accessor in sorted(target.items())
                    }
                )
            indices = primitive.get("indices")
            if indices is not None and not isinstance(indices, int):
                raise ValueError("Output GLB primitive indices are malformed")
            primitive_records.append(
                {
                    "mode": primitive.get("mode", 4),
                    **(
                        {"material": material_names[material_index]}
                        if include_material_bindings
                        else {}
                    ),
                    "attributes": {
                        name: accessor_record(
                            document,
                            binary_chunk,
                            accessor,
                        )
                        for name, accessor in sorted(attributes.items())
                    },
                    "indices": (
                        accessor_record(document, binary_chunk, indices)
                        if indices is not None
                        else None
                    ),
                    "targets": target_records,
                    "extras": primitive.get("extras"),
                }
            )
        records.append(
            {
                "name": names[mesh_index],
                "weights": mesh.get("weights"),
                "extras": mesh.get("extras"),
                "primitives": primitive_records,
            }
        )
    return sorted(records, key=lambda record: record["name"])


def validate_uploaded_authority_baseline(
    uploaded_path: Path,
    baseline_path: Path,
    *,
    ledger: dict[str, Any],
) -> dict[str, Any]:
    uploaded, uploaded_binary = parse_glb(uploaded_path)
    validate_embedded_glb(
        uploaded,
        uploaded_binary,
        label="Uploaded source GLB",
    )
    baseline, baseline_binary = parse_glb(baseline_path)
    validate_embedded_glb(
        baseline,
        baseline_binary,
        label="Fresh authored baseline GLB",
    )
    uploaded_geometry = glb_mesh_records(
        uploaded,
        uploaded_binary,
        include_material_bindings=False,
    )
    baseline_geometry = glb_mesh_records(
        baseline,
        baseline_binary,
        include_material_bindings=False,
    )
    if uploaded_geometry != baseline_geometry:
        raise ValueError(
            "Fresh authored baseline GLB geometry/accessors differ from "
            "the pinned uploaded source GLB"
        )
    uploaded_digest = raw_glb_surface_digest(uploaded, uploaded_binary)
    baseline_digest = raw_glb_surface_digest(baseline, baseline_binary)
    if uploaded_digest != baseline_digest:
        raise ValueError(
            "Fresh authored baseline raw surface digest differs from upload"
        )
    if ledger.get("present") and (
        ledger.get("surfaceDigest") != uploaded_digest
    ):
        raise ValueError(
            "Fresh authored baseline surface differs from validation ledger"
        )
    geometry_hash = stable_hash(uploaded_geometry)
    return {
        "uploadedRawSurfaceDigest": uploaded_digest,
        "freshBaselineRawSurfaceDigest": baseline_digest,
        "uploadedGeometryAccessorsSha256": geometry_hash,
        "freshBaselineGeometryAccessorsSha256": stable_hash(
            baseline_geometry
        ),
        "exactIndexPositionNormalUvTangentAndMorphAccessors": True,
        "materialBindingsExcludedFromThisSeam": True,
    }


def export_coverage_pose_baseline(
    *,
    path: Path,
    objects: list[bpy.types.Object],
    coverage_pose: dict[str, Any],
    coverage_ledger: dict[str, Any],
) -> dict[str, Any]:
    """Rebuild the pinned body-only, identity-root, world-baked upload pose."""
    scene = bpy.context.scene
    expected = coverage_ledger["expected"]
    expected_objects = set(expected["objectNames"])
    expected_meshes = set(expected["meshNames"])
    by_name = {obj.name: obj for obj in objects}
    if set(by_name) & expected_objects != expected_objects:
        raise ValueError("Authored authority omitted a coverage-pose body object")
    body = sorted((by_name[name] for name in expected_objects), key=lambda obj: obj.name)
    if (
        len(body) != expected["meshes"]
        or any(obj.type != "MESH" for obj in body)
        or {obj.data.name for obj in body} != expected_meshes
        or len({obj.data.as_pointer() for obj in body}) != len(body)
        or any(
            any(material_role(material) == TEAM_ROLE for material in obj.data.materials)
            for obj in body
        )
    ):
        raise ValueError("Authored coverage-pose body mesh identity is invalid")

    requested_clips = set(coverage_pose["clips"])
    available_clips = {
        track.name
        for obj in objects
        if obj.animation_data is not None
        for track in obj.animation_data.nla_tracks
    }
    if not requested_clips.issubset(available_clips):
        raise ValueError(
            "Authored authority omitted coverage clip(s): "
            + ", ".join(sorted(requested_clips - available_clips))
        )
    animation_state = {
        obj.name: {
            "action": obj.animation_data.action,
            "tracks": [
                (track, bool(track.mute))
                for track in obj.animation_data.nla_tracks
            ],
        }
        for obj in objects
        if obj.animation_data is not None
    }
    original_frame = scene.frame_current
    temporary_collection: bpy.types.Collection | None = None
    baked_objects: list[bpy.types.Object] = []
    baked_meshes: list[bpy.types.Mesh] = []
    source_names: list[tuple[bpy.types.Object, str, bpy.types.Mesh, str]] = []
    try:
        for obj in objects:
            data = obj.animation_data
            if data is None:
                continue
            data.action = None
            for track in data.nla_tracks:
                track.mute = track.name not in requested_clips
        scene.frame_set(coverage_pose["frame"])
        bpy.context.view_layer.update()
        source_names = [
            (obj, obj.name, obj.data, obj.data.name)
            for obj in body
        ]
        temporary_collection = bpy.data.collections.new(
            "__NB_TRANSPLANT_COVERAGE_UPLOAD_WORLD_BAKED__"
        )
        scene.collection.children.link(temporary_collection)
        for index, (obj, _object_name, mesh, _mesh_name) in enumerate(source_names):
            obj.name = f"__NB_COVERAGE_SOURCE_OBJECT_{index:02d}__"
            mesh.name = f"__NB_COVERAGE_SOURCE_MESH_{index:02d}__"
        for obj, object_name, mesh, mesh_name in source_names:
            baked_mesh = mesh.copy()
            baked_mesh.name = mesh_name
            baked_mesh.transform(obj.matrix_world)
            baked_mesh.update()
            baked_obj = bpy.data.objects.new(object_name, baked_mesh)
            temporary_collection.objects.link(baked_obj)
            baked_objects.append(baked_obj)
            baked_meshes.append(baked_mesh)
        bpy.ops.object.select_all(action="DESELECT")
        for baked_obj in baked_objects:
            baked_obj.select_set(True)
        bpy.context.view_layer.objects.active = baked_objects[0]
        bpy.ops.export_scene.gltf(
            filepath=str(path),
            export_format="GLB",
            use_selection=True,
            export_materials="EXPORT",
            export_cameras=False,
            export_lights=False,
            export_extras=True,
            export_animations=False,
            export_apply=True,
            export_yup=True,
        )
    finally:
        bpy.ops.object.select_all(action="DESELECT")
        for baked_obj in baked_objects:
            if baked_obj.name in bpy.data.objects:
                bpy.data.objects.remove(baked_obj, do_unlink=True)
        for baked_mesh in baked_meshes:
            if baked_mesh.name in bpy.data.meshes and baked_mesh.users == 0:
                bpy.data.meshes.remove(baked_mesh)
        if (
            temporary_collection is not None
            and temporary_collection.name in bpy.data.collections
        ):
            bpy.data.collections.remove(temporary_collection)
        for obj, object_name, mesh, mesh_name in source_names:
            obj.name = object_name
            mesh.name = mesh_name
        for object_name, state in animation_state.items():
            obj = bpy.data.objects.get(object_name)
            if obj is None or obj.animation_data is None:
                continue
            obj.animation_data.action = state["action"]
            for track, mute in state["tracks"]:
                track.mute = mute
        scene.frame_set(original_frame)
        bpy.context.view_layer.update()
    if not path.is_file():
        raise ValueError("Coverage-pose baseline export produced no GLB")
    return {
        "path": path.name,
        "sha256": sha256(path),
        "bytes": path.stat().st_size,
        "activeClips": list(coverage_pose["clips"]),
        "frame": coverage_pose["frame"],
        "worldTransformsBakedIntoTemporaryCopies": True,
        "identityRootMeshes": len(baked_objects),
        "providerObjectsImportedIntoAuthorityScene": 0,
    }


def glb_triangle_counts(records: list[dict[str, Any]]) -> dict[str, int]:
    result: dict[str, int] = {}
    for mesh in records:
        triangles = 0
        for primitive in mesh["primitives"]:
            if primitive["mode"] != 4:
                raise ValueError("Coverage-pose upload contains a non-triangle primitive")
            index_record = primitive["indices"]
            count = (
                index_record["count"]
                if index_record is not None
                else primitive["attributes"]["POSITION"]["count"]
            )
            if count % 3 != 0:
                raise ValueError("Coverage-pose triangle accessor count is invalid")
            triangles += count // 3
        result[mesh["name"]] = triangles
    return result


def validate_uploaded_coverage_pose_baseline(
    uploaded_path: Path,
    baseline_path: Path,
    *,
    coverage_ledger: dict[str, Any],
) -> dict[str, Any]:
    uploaded, uploaded_binary = parse_glb(uploaded_path)
    baseline, baseline_binary = parse_glb(baseline_path)
    validate_embedded_glb(uploaded, uploaded_binary, label="Coverage upload GLB")
    validate_embedded_glb(
        baseline,
        baseline_binary,
        label="Fresh authored coverage-pose baseline GLB",
    )
    for label, document in (("upload", uploaded), ("baseline", baseline)):
        if document.get("animations") or document.get("skins"):
            raise ValueError(f"Coverage {label} must omit animations and skins")
        if document.get("cameras") or document.get("lights"):
            raise ValueError(f"Coverage {label} must omit cameras and lights")
        materials = document.get("materials")
        if not isinstance(materials, list) or len(materials) != 1:
            raise ValueError(f"Coverage {label} must use one neutral material")

    uploaded_geometry = glb_mesh_records(
        uploaded,
        uploaded_binary,
        include_material_bindings=False,
    )
    baseline_geometry = glb_mesh_records(
        baseline,
        baseline_binary,
        include_material_bindings=False,
    )
    if uploaded_geometry != baseline_geometry:
        raise ValueError(
            "Fresh authored coverage-pose geometry/accessors differ from the "
            "pinned uploaded source GLB"
        )
    uploaded_nodes = glb_node_records(uploaded)
    baseline_nodes = glb_node_records(baseline)
    if uploaded_nodes != baseline_nodes:
        raise ValueError(
            "Fresh authored coverage-pose identity roots differ from upload"
        )
    expected = coverage_ledger["expected"]
    if (
        sorted(record["name"] for record in uploaded_nodes)
        != sorted(expected["objectNames"])
        or sorted(record["name"] for record in uploaded_geometry)
        != sorted(expected["meshNames"])
        or any(
            record["parent"] is not None
            or record["children"]
            or record["mesh"] is None
            or record["matrix"] is not None
            or record["translation"] != [0.0, 0.0, 0.0]
            or record["rotation"] != [0.0, 0.0, 0.0, 1.0]
            or record["scale"] != [1.0, 1.0, 1.0]
            or record["skin"] is not None
            for record in uploaded_nodes
        )
    ):
        raise ValueError("Coverage upload is not the expected flat identity-root set")
    if any(
        "POSITION" not in primitive["attributes"]
        or "NORMAL" not in primitive["attributes"]
        or "TEXCOORD_0" not in primitive["attributes"]
        for mesh in uploaded_geometry
        for primitive in mesh["primitives"]
    ):
        raise ValueError("Coverage upload omitted position, normal, or UV authority")
    uploaded_triangles = glb_triangle_counts(uploaded_geometry)
    baseline_triangles = glb_triangle_counts(baseline_geometry)
    if (
        uploaded_triangles != baseline_triangles
        or sum(uploaded_triangles.values()) != expected["triangles"]
    ):
        raise ValueError("Coverage upload named triangle counts differ from authority")
    uploaded_digest = raw_glb_surface_digest(uploaded, uploaded_binary)
    baseline_digest = raw_glb_surface_digest(baseline, baseline_binary)
    if uploaded_digest != baseline_digest:
        raise ValueError("Fresh authored coverage-pose raw surface differs from upload")
    return {
        "mode": "pinned-world-baked-coverage-pose",
        "uploadedRawSurfaceDigest": uploaded_digest,
        "freshCoveragePoseRawSurfaceDigest": baseline_digest,
        "uploadedGeometryAccessorsSha256": stable_hash(uploaded_geometry),
        "freshCoveragePoseGeometryAccessorsSha256": stable_hash(
            baseline_geometry
        ),
        "uploadedIdentityRootNodesSha256": stable_hash(uploaded_nodes),
        "freshCoveragePoseIdentityRootNodesSha256": stable_hash(baseline_nodes),
        "namedTriangleCountsSha256": stable_hash(uploaded_triangles),
        "objects": len(uploaded_nodes),
        "meshes": len(uploaded_geometry),
        "triangles": sum(uploaded_triangles.values()),
        "exactIndexPositionNormalUvTangentAndMorphAccessors": True,
        "exactNamedFlatIdentityRoots": True,
        "materialBindingsExcludedFromThisSeam": True,
        "providerGeometryUsedAsOutput": False,
    }


def glb_skin_records(
    document: dict[str, Any],
    binary_chunk: bytes,
) -> list[dict[str, Any]]:
    nodes = document.get("nodes", [])
    skins = document.get("skins", [])
    if not isinstance(nodes, list) or not isinstance(skins, list):
        raise ValueError("Output GLB skins are malformed")

    def node_name(index: Any, label: str) -> str:
        if (
            not isinstance(index, int)
            or index < 0
            or index >= len(nodes)
            or not isinstance(nodes[index], dict)
            or not isinstance(nodes[index].get("name"), str)
        ):
            raise ValueError(f"Output GLB skin {label} is invalid")
        return nodes[index]["name"]

    records = []
    for index, skin in enumerate(skins):
        if not isinstance(skin, dict):
            raise ValueError("Output GLB skin record is malformed")
        joints = skin.get("joints", [])
        if not isinstance(joints, list) or not joints:
            raise ValueError("Output GLB skin has no joints")
        inverse_bind = skin.get("inverseBindMatrices")
        if inverse_bind is not None and not isinstance(inverse_bind, int):
            raise ValueError("Output GLB inverse bind accessor is malformed")
        skeleton = skin.get("skeleton")
        records.append(
            {
                "index": index,
                "name": skin.get("name"),
                "joints": [node_name(joint, "joint") for joint in joints],
                "skeleton": (
                    node_name(skeleton, "skeleton")
                    if skeleton is not None
                    else None
                ),
                "inverseBindMatrices": (
                    accessor_record(document, binary_chunk, inverse_bind)
                    if inverse_bind is not None
                    else None
                ),
                "extras": skin.get("extras"),
            }
        )
    return records


def glb_scene_records(document: dict[str, Any]) -> dict[str, Any]:
    nodes = document.get("nodes", [])
    scenes = document.get("scenes", [])
    if not isinstance(nodes, list) or not isinstance(scenes, list):
        raise ValueError("Output GLB scenes are malformed")
    node_names = [
        node.get("name") if isinstance(node, dict) else None
        for node in nodes
    ]
    records = []
    for scene in scenes:
        if not isinstance(scene, dict):
            raise ValueError("Output GLB scene record is malformed")
        roots = scene.get("nodes", [])
        if (
            not isinstance(roots, list)
            or any(
                not isinstance(index, int)
                or index < 0
                or index >= len(node_names)
                or not isinstance(node_names[index], str)
                for index in roots
            )
        ):
            raise ValueError("Output GLB scene roots are invalid")
        records.append(
            {
                "name": scene.get("name"),
                "nodes": [node_names[index] for index in roots],
                "extras": scene.get("extras"),
            }
        )
    default_scene = document.get("scene")
    if default_scene is not None and (
        not isinstance(default_scene, int)
        or default_scene < 0
        or default_scene >= len(records)
    ):
        raise ValueError("Output GLB default scene is invalid")
    return {"default": default_scene, "scenes": records}


def glb_node_records(document: dict[str, Any]) -> list[dict[str, Any]]:
    nodes = document.get("nodes", [])
    meshes = document.get("meshes", [])
    if not isinstance(nodes, list) or not isinstance(meshes, list):
        raise ValueError("Output GLB nodes or meshes are malformed")
    names = [
        node.get("name")
        for node in nodes
        if isinstance(node, dict)
    ]
    if (
        len(names) != len(nodes)
        or any(not isinstance(name, str) or not name for name in names)
        or len(set(names)) != len(names)
    ):
        raise ValueError("Output GLB nodes must have unique non-empty names")
    parent_by_child: dict[int, int] = {}
    for parent_index, node in enumerate(nodes):
        children = node.get("children", [])
        if not isinstance(children, list):
            raise ValueError("Output GLB node children are malformed")
        for child in children:
            if (
                not isinstance(child, int)
                or child < 0
                or child >= len(nodes)
                or child in parent_by_child
            ):
                raise ValueError("Output GLB node hierarchy is invalid")
            parent_by_child[child] = parent_index
    records = []
    for index, node in enumerate(nodes):
        mesh_index = node.get("mesh")
        if mesh_index is None:
            mesh_name = None
        elif (
            not isinstance(mesh_index, int)
            or mesh_index < 0
            or mesh_index >= len(meshes)
            or not isinstance(meshes[mesh_index], dict)
        ):
            raise ValueError("Output GLB node references an invalid mesh")
        else:
            mesh_name = meshes[mesh_index].get("name")
        records.append(
            {
                "name": names[index],
                "parent": (
                    names[parent_by_child[index]]
                    if index in parent_by_child
                    else None
                ),
                "children": sorted(names[child] for child in node.get("children", [])),
                "mesh": mesh_name,
                "matrix": node.get("matrix"),
                "translation": node.get("translation", [0.0, 0.0, 0.0]),
                "rotation": node.get("rotation", [0.0, 0.0, 0.0, 1.0]),
                "scale": node.get("scale", [1.0, 1.0, 1.0]),
                "weights": node.get("weights"),
                "skin": node.get("skin"),
                "extras": node.get("extras"),
            }
        )
    return sorted(records, key=lambda record: record["name"])


def glb_animation_records(
    document: dict[str, Any],
    binary_chunk: bytes,
) -> list[dict[str, Any]]:
    animations = document.get("animations", [])
    nodes = document.get("nodes", [])
    if not isinstance(animations, list) or not isinstance(nodes, list):
        raise ValueError("Output GLB animations are malformed")
    records: list[dict[str, Any]] = []
    seen_names: set[str] = set()
    for animation in animations:
        if not isinstance(animation, dict):
            raise ValueError("Output GLB animation record is malformed")
        name = animation.get("name")
        channels = animation.get("channels", [])
        samplers = animation.get("samplers", [])
        if (
            not isinstance(name, str)
            or not name
            or name in seen_names
            or not isinstance(channels, list)
            or not isinstance(samplers, list)
        ):
            raise ValueError("Output GLB animation identity is invalid")
        seen_names.add(name)
        channel_records = []
        for channel in channels:
            if not isinstance(channel, dict):
                raise ValueError("Output GLB animation channel is malformed")
            sampler_index = channel.get("sampler")
            target = channel.get("target")
            if (
                not isinstance(sampler_index, int)
                or sampler_index < 0
                or sampler_index >= len(samplers)
                or not isinstance(samplers[sampler_index], dict)
                or not isinstance(target, dict)
            ):
                raise ValueError("Output GLB animation channel is invalid")
            sampler = samplers[sampler_index]
            node_index = target.get("node")
            path = target.get("path")
            if (
                not isinstance(node_index, int)
                or node_index < 0
                or node_index >= len(nodes)
                or not isinstance(nodes[node_index], dict)
                or path not in {"translation", "rotation", "scale", "weights"}
                or not isinstance(sampler.get("input"), int)
                or not isinstance(sampler.get("output"), int)
            ):
                raise ValueError("Output GLB animation target is invalid")
            times = accessor_values(
                document,
                binary_chunk,
                sampler["input"],
            )
            values = accessor_values(
                document,
                binary_chunk,
                sampler["output"],
            )
            interpolation = sampler.get("interpolation", "LINEAR")
            value_multiplier = 3 if interpolation == "CUBICSPLINE" else 1
            if interpolation not in {"LINEAR", "STEP", "CUBICSPLINE"}:
                raise ValueError(
                    "Output GLB animation interpolation is invalid"
                )
            expected_values = len(times) * value_multiplier
            if path == "weights":
                value_count_valid = (
                    len(times) > 0
                    and len(values) >= expected_values
                    and len(values) % expected_values == 0
                )
            else:
                value_count_valid = len(values) == expected_values
            if (
                not value_count_valid
                or any(not isinstance(value, float) for value in times)
                or any(
                    times[index] >= times[index + 1]
                    for index in range(len(times) - 1)
                )
            ):
                raise ValueError("Output GLB animation samples are invalid")
            channel_records.append(
                {
                    "targetNode": nodes[node_index].get("name"),
                    "targetPath": path,
                    "interpolation": interpolation,
                    "times": times,
                    "values": values,
                }
            )
        records.append(
            {
                "name": name,
                "channels": sorted(
                    channel_records,
                    key=lambda record: (
                        record["targetNode"],
                        record["targetPath"],
                        stable_hash(record),
                    ),
                ),
            }
        )
    return sorted(records, key=lambda record: record["name"])


def material_factor_record(material: dict[str, Any]) -> dict[str, Any]:
    pbr = material.get("pbrMetallicRoughness", {})
    if not isinstance(pbr, dict):
        raise ValueError("Output GLB material PBR record is malformed")
    extensions = material.get("extensions", {})
    if not isinstance(extensions, dict):
        raise ValueError("Output GLB material extensions are malformed")
    unsupported = set(extensions) - {"KHR_materials_emissive_strength"}
    if unsupported:
        raise ValueError(
            "Unsupported output GLB material extensions: "
            + ", ".join(sorted(unsupported))
        )
    strength = extensions.get("KHR_materials_emissive_strength", {})
    if not isinstance(strength, dict):
        raise ValueError("Output GLB emissive-strength extension is malformed")
    normal = material.get("normalTexture")
    occlusion = material.get("occlusionTexture")
    double_sided = material.get("doubleSided", False)
    if not isinstance(double_sided, bool):
        raise ValueError("Output doubleSided must be a boolean")
    return {
        "baseColor": finite_vector(
            pbr.get("baseColorFactor"),
            length=4,
            label="output baseColorFactor",
            default=(1.0, 1.0, 1.0, 1.0),
        ),
        "metallic": finite_number(
            pbr.get("metallicFactor", 1.0),
            "output metallicFactor",
        ),
        "roughness": finite_number(
            pbr.get("roughnessFactor", 1.0),
            "output roughnessFactor",
        ),
        "emissive": finite_vector(
            material.get("emissiveFactor"),
            length=3,
            label="output emissiveFactor",
            default=(0.0, 0.0, 0.0),
        ),
        "emissiveStrength": finite_number(
            strength.get("emissiveStrength", 1.0),
            "output emissiveStrength",
        ),
        "normalScale": finite_number(
            normal.get("scale", 1.0) if isinstance(normal, dict) else 1.0,
            "output normalScale",
        ),
        "occlusionStrength": finite_number(
            (
                occlusion.get("strength", 1.0)
                if isinstance(occlusion, dict)
                else 1.0
            ),
            "output occlusionStrength",
        ),
        "doubleSided": double_sided,
    }


def factors_equal(left: dict[str, Any], right: dict[str, Any]) -> bool:
    def close(a: Any, b: Any) -> bool:
        if isinstance(a, list) and isinstance(b, list):
            return len(a) == len(b) and all(
                math.isclose(float(x), float(y), rel_tol=0.0, abs_tol=1.0e-6)
                for x, y in zip(a, b)
            )
        if isinstance(a, bool) or isinstance(b, bool):
            return a is b
        return math.isclose(float(a), float(b), rel_tol=0.0, abs_tol=1.0e-6)

    return set(left) == set(right) and all(
        close(left[key], right[key]) for key in left
    )


def output_texture_record(
    document: dict[str, Any],
    binary_chunk: bytes,
    material: dict[str, Any],
    *,
    role: str,
) -> dict[str, Any] | None:
    info = material_texture_info(material, role)
    if info is None:
        return None
    textures = document.get("textures", [])
    index = info.get("index")
    if (
        not isinstance(textures, list)
        or not isinstance(index, int)
        or index < 0
        or index >= len(textures)
        or not isinstance(textures[index], dict)
    ):
        raise ValueError(f"Output GLB {role} texture is invalid")
    texture = textures[index]
    sampler = canonical_sampler(document, texture, role=role)
    source = texture.get("source")
    if not isinstance(source, int):
        raise ValueError(f"Output GLB {role} texture has no image")
    payload, mime = image_payload(
        document=document,
        binary_chunk=binary_chunk,
        image_index=source,
    )
    return {
        "sha256": sha256_bytes(payload),
        "bytes": len(payload),
        "mimeType": mime,
        "sampler": sampler,
    }


def validate_output_materials(
    document: dict[str, Any],
    binary_chunk: bytes,
    *,
    baseline_document: dict[str, Any],
    body_material_names: list[str],
    accent_material_names: list[str],
    donor_source: dict[str, Any],
    donor_sha256: str,
    payload_set_sha256: str | None = None,
) -> dict[str, Any]:
    materials = document.get("materials", [])
    meshes = document.get("meshes", [])
    if not isinstance(materials, list) or not isinstance(meshes, list):
        raise ValueError("Output GLB materials or meshes are malformed")
    by_name: dict[str, tuple[int, dict[str, Any]]] = {}
    for index, material in enumerate(materials):
        if not isinstance(material, dict):
            raise ValueError("Output GLB material record is malformed")
        name = material.get("name")
        if not isinstance(name, str) or not name or name in by_name:
            raise ValueError("Output GLB material names are invalid")
        by_name[name] = (index, material)
    expected_names = set(body_material_names) | set(accent_material_names)
    if set(by_name) != expected_names:
        raise ValueError(
            "Output GLB material set differs from authored model materials"
        )
    if not set(body_material_names).issubset(by_name):
        raise ValueError("Output GLB omitted an authored body material")
    if not set(accent_material_names).issubset(by_name):
        raise ValueError("Output GLB omitted a semantic team-accent material")

    texture_roles = {
        "baseColor",
        "metallicRoughness",
        "normal",
        "emissive",
        "occlusion",
    }
    expected_roles = set(donor_source["images"])
    body_indices: set[int] = set()
    body_records: dict[str, Any] = {}
    for name in body_material_names:
        index, material = by_name[name]
        body_indices.add(index)
        if material.get("alphaMode", "OPAQUE") != "OPAQUE":
            raise ValueError(f"Output body material is not opaque: {name}")
        extras = material.get("extras", {})
        if (
            not isinstance(extras, dict)
            or extras.get("nilbotsTextureSource") != "texture-only-donor"
            or extras.get("nilbotsTextureDonorSha256") != donor_sha256
            or extras.get("nilbotsTeamColorOwnership") != "renderer"
        ):
            raise ValueError(f"Output body material lost provenance: {name}")
        if payload_set_sha256 is not None and (
            extras.get("nilbotsTexturePayloadMode")
            != "standalone-lossless-png"
            or extras.get("nilbotsTexturePayloadSetSha256")
            != payload_set_sha256
        ):
            raise ValueError(
                f"Output body material lost standalone payload provenance: {name}"
            )
        factors = material_factor_record(material)
        if not factors_equal(factors, donor_source["factors"]):
            raise ValueError(
                f"Output body material factors differ from donor: {name}"
            )
        role_records = {
            role: output_texture_record(
                document,
                binary_chunk,
                material,
                role=role,
            )
            for role in sorted(texture_roles)
        }
        found_roles = {
            role for role, record in role_records.items() if record is not None
        }
        if found_roles != expected_roles:
            raise ValueError(
                f"Output body material PBR roles differ from donor: {name}"
            )
        for role in found_roles:
            if (
                role_records[role]["sha256"]
                != donor_source["images"][role]["sha256"]
                or role_records[role]["sampler"]
                != donor_source["samplers"][role]
            ):
                raise ValueError(
                    f"Output body material {role} payload/sampler drifted: {name}"
                )
        body_records[name] = {
            "factors": factors,
            "textures": role_records,
        }

    baseline_materials = baseline_document.get("materials", [])
    if not isinstance(baseline_materials, list):
        raise ValueError("Baseline GLB materials are malformed")
    baseline_by_name: dict[str, dict[str, Any]] = {}
    for material in baseline_materials:
        if (
            not isinstance(material, dict)
            or not isinstance(material.get("name"), str)
            or material["name"] in baseline_by_name
        ):
            raise ValueError("Baseline GLB material identity is invalid")
        baseline_by_name[material["name"]] = material

    accent_indices: set[int] = set()
    accent_records: dict[str, dict[str, str]] = {}
    for name in accent_material_names:
        index, material = by_name[name]
        accent_indices.add(index)
        baseline_material = baseline_by_name.get(name)
        if baseline_material is None or material != baseline_material:
            raise ValueError(
                f"Output team-accent material changed from baseline: {name}"
            )
        extras = material.get("extras", {})
        if (
            not isinstance(extras, dict)
            or extras.get("nilbotsRole") != TEAM_ROLE
        ):
            raise ValueError(
                f"Output team-accent material lost semantic role: {name}"
            )
        if any(
            material_texture_info(material, role) is not None
            for role in texture_roles
        ):
            raise ValueError(
                f"Output team-accent material gained a texture: {name}"
            )
        accent_records[name] = {
            "baselineSha256": stable_hash(baseline_material),
            "outputSha256": stable_hash(material),
        }

    used_body: set[int] = set()
    used_accent: set[int] = set()
    body_primitives = 0
    explicit_tangent_primitives = 0
    derived_tangent_primitives = 0
    for mesh in meshes:
        if not isinstance(mesh, dict):
            raise ValueError("Output GLB mesh record is malformed")
        primitives = mesh.get("primitives", [])
        if not isinstance(primitives, list):
            raise ValueError("Output GLB mesh primitives are malformed")
        for primitive in primitives:
            if not isinstance(primitive, dict):
                raise ValueError("Output GLB primitive is malformed")
            material_index = primitive.get("material")
            attributes = primitive.get("attributes", {})
            if (
                not isinstance(material_index, int)
                or not isinstance(attributes, dict)
            ):
                raise ValueError("Output GLB primitive material is invalid")
            if material_index in body_indices:
                used_body.add(material_index)
                body_primitives += 1
                required = {"POSITION", "NORMAL", "TEXCOORD_0"}
                if not required.issubset(attributes):
                    raise ValueError(
                        "Normal-mapped body primitive lacks portable "
                        "position/normal/UV tangent-basis inputs"
                    )
                if "TANGENT" in attributes:
                    explicit_tangent_primitives += 1
                else:
                    derived_tangent_primitives += 1
            elif material_index in accent_indices:
                used_accent.add(material_index)
            else:
                raise ValueError(
                    "Output GLB primitive uses an unexpected material"
                )
    if used_body != body_indices or used_accent != accent_indices:
        raise ValueError("Output GLB contains an unused authored material")
    return {
        "bodyMaterials": body_records,
        "teamAccentMaterials": sorted(accent_material_names),
        "teamAccentMaterialProof": accent_records,
        "bodyPrimitiveMaterials": len(used_body),
        "teamAccentPrimitiveMaterials": len(used_accent),
        "normalMapTangentBasis": {
            "bodyPrimitives": body_primitives,
            "explicitTangentPrimitives": explicit_tangent_primitives,
            "mikkTspaceDerivedPrimitives": derived_tangent_primitives,
            "requiredInputsPresent": (
                body_primitives
                == explicit_tangent_primitives + derived_tangent_primitives
            ),
        },
    }


def validate_output_glb(
    path: Path,
    *,
    baseline_path: Path,
    body_material_names: list[str],
    accent_material_names: list[str],
    donor_source: dict[str, Any],
    donor_sha256: str,
    payload_set_sha256: str | None = None,
) -> dict[str, Any]:
    document, binary_chunk = parse_glb(path)
    validate_embedded_glb(
        document,
        binary_chunk,
        label="Output GLB",
    )
    validate_extension_declarations(
        document,
        label="Output GLB",
        allowed={"KHR_materials_emissive_strength"},
    )
    baseline, baseline_binary = parse_glb(baseline_path)
    validate_embedded_glb(
        baseline,
        baseline_binary,
        label="Baseline GLB",
    )
    validate_extension_declarations(
        baseline,
        label="Baseline GLB",
        allowed={"KHR_materials_emissive_strength"},
    )
    nodes = glb_node_records(document)
    baseline_nodes = glb_node_records(baseline)
    if nodes != baseline_nodes:
        raise ValueError("Output GLB node hierarchy, pivots, or transforms drifted")
    meshes = glb_mesh_records(document, binary_chunk)
    baseline_meshes = glb_mesh_records(baseline, baseline_binary)
    if meshes != baseline_meshes:
        raise ValueError(
            "Output GLB geometry, topology, UVs, vertex attributes, morph "
            "targets, or material bindings drifted"
        )
    skins = glb_skin_records(document, binary_chunk)
    baseline_skins = glb_skin_records(baseline, baseline_binary)
    if skins != baseline_skins:
        raise ValueError(
            "Output GLB skin joints or inverse-bind matrices drifted"
        )
    scenes = glb_scene_records(document)
    baseline_scenes = glb_scene_records(baseline)
    if scenes != baseline_scenes:
        raise ValueError("Output GLB scene roots drifted")
    animations = glb_animation_records(document, binary_chunk)
    baseline_animations = glb_animation_records(
        baseline,
        baseline_binary,
    )
    if animations != baseline_animations:
        raise ValueError(
            "Output GLB animation channels, targets, times, or values drifted"
        )
    materials = validate_output_materials(
        document,
        binary_chunk,
        baseline_document=baseline,
        body_material_names=body_material_names,
        accent_material_names=accent_material_names,
        donor_source=donor_source,
        donor_sha256=donor_sha256,
        payload_set_sha256=payload_set_sha256,
    )
    node_hash = stable_hash(nodes)
    mesh_hash = stable_hash(meshes)
    skin_hash = stable_hash(skins)
    scene_hash = stable_hash(scenes)
    animation_hash = stable_hash(animations)
    return {
        "baselineNodeHierarchySha256": stable_hash(baseline_nodes),
        "outputNodeHierarchySha256": node_hash,
        "baselineMeshAuthoritySha256": stable_hash(baseline_meshes),
        "outputMeshAuthoritySha256": mesh_hash,
        "baselineSkinAuthoritySha256": stable_hash(baseline_skins),
        "outputSkinAuthoritySha256": skin_hash,
        "baselineSceneAuthoritySha256": stable_hash(baseline_scenes),
        "outputSceneAuthoritySha256": scene_hash,
        "baselineAnimationSamplesSha256": stable_hash(
            baseline_animations
        ),
        "outputAnimationSamplesSha256": animation_hash,
        "matchesBaseline": {
            "nodeHierarchyAndPivots": nodes == baseline_nodes,
            "meshAccessorsAndMaterialBindings": meshes == baseline_meshes,
            "skinsAndInverseBindMatrices": skins == baseline_skins,
            "sceneRoots": scenes == baseline_scenes,
            "animationChannelsTargetsTimesAndValues": (
                animations == baseline_animations
            ),
        },
        "animations": [
            {
                "name": animation["name"],
                "channels": len(animation["channels"]),
                "targets": sorted(
                    {
                        (
                            channel["targetNode"],
                            channel["targetPath"],
                        )
                        for channel in animation["channels"]
                    }
                ),
                "samples": sum(
                    len(channel["times"])
                    for channel in animation["channels"]
                ),
                "channelProof": [
                    {
                        "targetNode": channel["targetNode"],
                        "targetPath": channel["targetPath"],
                        "interpolation": channel["interpolation"],
                        "keyCount": len(channel["times"]),
                        "timesSha256": stable_hash(channel["times"]),
                        "valuesSha256": stable_hash(channel["values"]),
                    }
                    for channel in animation["channels"]
                ],
            }
            for animation in animations
        ],
        "materials": materials,
    }


def artifact_record(
    path: Path,
    *,
    repo_root: Path,
    kind: str,
) -> dict[str, Any]:
    return {
        "kind": kind,
        "path": path.relative_to(repo_root).as_posix(),
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
    }


def contains_sensitive_report_value(value: Any) -> bool:
    if isinstance(value, dict):
        return any(
            contains_sensitive_report_value(key)
            or contains_sensitive_report_value(item)
            for key, item in value.items()
        )
    if isinstance(value, (list, tuple)):
        return any(contains_sensitive_report_value(item) for item in value)
    if not isinstance(value, str):
        return False
    stripped = value.strip()
    return (
        stripped.startswith(("/", "\\"))
        or bool(re.match(r"^[A-Za-z]:[\\/]", stripped))
        or bool(re.search(r"(?:^|[\s\"'(=:\[])[\\/](?![\\/])", stripped))
        or bool(re.search(r"(?i)(?:^|[\s\"'(=])(?:https?|file)://", stripped))
        or stripped.lower().startswith("data:")
    )


def validate_report_sanitized(report: dict[str, Any]) -> None:
    if contains_sensitive_report_value(report):
        raise ValueError(
            "Refusing to write a report containing an absolute path, URL, "
            "or data URI"
        )


def main() -> int:
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise ValueError(
            f"Expected Blender {EXPECTED_BLENDER[0]}.{EXPECTED_BLENDER[1]}.x, "
            f"got {bpy.app.version_string}"
        )
    args = parse_args(blender_arguments())
    repo_root = repository_root()
    authored, authored_relative = resolve_repo_path(
        args.authored_blend,
        repo_root=repo_root,
        option="--authored-blend",
        strict=True,
    )
    uploaded, uploaded_relative = resolve_repo_path(
        args.uploaded_source_glb,
        repo_root=repo_root,
        option="--uploaded-source-glb",
        strict=True,
    )
    donor, donor_relative = resolve_repo_path(
        args.donor_glb,
        repo_root=repo_root,
        option="--donor-glb",
        strict=True,
    )
    standalone_pbr = resolve_standalone_pbr(args, repo_root=repo_root)
    coverage_pose = resolve_coverage_pose(args, repo_root=repo_root)
    report_schema = (
        COVERAGE_POSE_REPORT_SCHEMA
        if coverage_pose is not None
        else (
            LOSSLESS_PBR_REPORT_SCHEMA
            if standalone_pbr is not None
            else REPORT_SCHEMA
        )
    )
    output_dir, output_relative = resolve_repo_path(
        args.output_dir,
        repo_root=repo_root,
        option="--output-dir",
        strict=False,
    )
    if output_dir.exists():
        raise ValueError("--output-dir already exists; refusing overwrite")
    if not output_relative.parts or output_relative.parts[0] not in {
        "sandbox",
        "var",
    }:
        raise ValueError("--output-dir must stay below sandbox/ or var/")
    expected_inputs = [
        (authored, args.expected_authored_sha256, "Authored .blend"),
        (
            uploaded,
            args.expected_uploaded_source_sha256,
            "Uploaded source GLB",
        ),
        (donor, args.expected_donor_sha256, "Donor GLB"),
    ]
    if standalone_pbr is not None:
        expected_inputs.extend(
            (
                record["path"],
                record["sha256"],
                f"Standalone {role} PNG",
            )
            for role, record in standalone_pbr.items()
        )
    if coverage_pose is not None:
        expected_inputs.append(
            (
                coverage_pose["path"],
                coverage_pose["sha256"],
                "Coverage validation report",
            )
        )
    for path, expected, label in expected_inputs:
        observed = sha256(path)
        if observed != expected:
            raise ValueError(
                f"{label} hash drifted before Blender processing: "
                f"{observed} != {expected}"
            )
    if coverage_pose is None:
        (
            authority_ledger,
            authority_ledger_path,
            authority_ledger_sha256,
        ) = authority_validation_ledger(
            authored=authored,
            authored_relative=authored_relative,
            authored_sha256=args.expected_authored_sha256,
            uploaded=uploaded,
            uploaded_relative=uploaded_relative,
            uploaded_sha256=args.expected_uploaded_source_sha256,
            repo_root=repo_root,
        )
        coverage_ledger = {"present": False}
    else:
        authority_ledger = {"present": False}
        authority_ledger_path = None
        authority_ledger_sha256 = None
        coverage_ledger = coverage_validation_ledger(
            coverage_pose=coverage_pose,
            authored=authored,
            authored_relative=authored_relative,
            authored_sha256=args.expected_authored_sha256,
            uploaded=uploaded,
            uploaded_relative=uploaded_relative,
            uploaded_sha256=args.expected_uploaded_source_sha256,
        )

    # Disposable provider/source scenes. Only canonical surface signatures and
    # byte-level PBR payloads survive this stage.
    uploaded_document, uploaded_binary = parse_glb(uploaded)
    validate_embedded_glb(
        uploaded_document,
        uploaded_binary,
        label="Uploaded source GLB",
    )
    donor_document, donor_binary = parse_glb(donor)
    validate_embedded_glb(
        donor_document,
        donor_binary,
        label="Donor GLB",
    )
    uploaded_capture = capture_surface(import_glb_meshes(uploaded))
    uploaded_surface = uploaded_capture["signature"]
    donor_capture = capture_surface(import_glb_meshes(donor))
    donor_surface = donor_capture["signature"]
    provider_surface_correspondence = (
        validate_provider_surface_correspondence(
            uploaded_capture,
            donor_capture,
        )
    )
    embedded_donor_source = donor_material_source(donor)
    donor_source = (
        standalone_lossless_pbr_source(
            standalone_pbr=standalone_pbr,
            donor_source=embedded_donor_source,
        )
        if standalone_pbr is not None
        else embedded_donor_source
    )
    payload_set_sha256 = donor_source.get("payloadSetSha256")

    # Opening the authority is the hard contamination boundary: every donor
    # object, mesh, node, pivot, action, and imported image is discarded.
    bpy.ops.wm.open_mainfile(filepath=str(authored))
    collection = model_collection(args.model_collection)
    objects = model_objects(collection)
    original_scene_frame = bpy.context.scene.frame_current
    bpy.context.scene.frame_set(args.bind_frame)
    bpy.context.view_layer.update()
    authored_surface = surface_signature(
        obj for obj in objects if obj.type == "MESH"
    )
    bpy.context.scene.frame_set(original_scene_frame)
    bpy.context.view_layer.update()
    uv_atlas = validate_shared_uv_atlas(objects)
    body_materials, accent_materials = body_and_accent_materials(objects)
    material_scope = validate_material_scope(
        body_materials,
        scoped_objects=objects,
    )
    body_material_names = [material.name for material in body_materials]
    accent_material_names = [material.name for material in accent_materials]
    accent_before = {
        material.name: material_fingerprint(material)
        for material in accent_materials
    }
    before = authority_snapshot(objects, bind_frame=args.bind_frame)
    if before["actions"] == 0 or before["keyframes"] == 0:
        raise ValueError("Authored Blender authority contains no keyed actions")
    if not before["nlaTrackNames"]:
        raise ValueError(
            "Authored Blender authority contains no semantic NLA tracks"
        )

    output_dir.mkdir(parents=True)
    baseline_path = output_dir / ".authority-baseline.glb"
    export_animated_glb(path=baseline_path, collection=collection)
    coverage_baseline_record: dict[str, Any] | None = None
    if coverage_pose is None:
        uploaded_authority_proof = validate_uploaded_authority_baseline(
            uploaded,
            baseline_path,
            ledger=authority_ledger,
        )
    else:
        coverage_baseline_path = output_dir / ".authority-coverage-baseline.glb"
        coverage_baseline_record = export_coverage_pose_baseline(
            path=coverage_baseline_path,
            objects=objects,
            coverage_pose=coverage_pose,
            coverage_ledger=coverage_ledger,
        )
        uploaded_authority_proof = validate_uploaded_coverage_pose_baseline(
            uploaded,
            coverage_baseline_path,
            coverage_ledger=coverage_ledger,
        )
        coverage_baseline_path.unlink()
    # The Blender exporter evaluates animation and may leave transient object
    # property values at its final sample. Reopen the pinned authority so the
    # material transplant starts from disk truth, not exporter evaluation state.
    bpy.ops.wm.open_mainfile(filepath=str(authored))
    collection = model_collection(args.model_collection)
    objects = model_objects(collection)
    body_materials, accent_materials = body_and_accent_materials(objects)
    restored = authority_snapshot(
        objects,
        bind_frame=args.bind_frame,
    )
    if restored != before:
        raise ValueError("Reopened authority differs after baseline GLB export")
    if {
        material.name: material_fingerprint(material)
        for material in accent_materials
    } != accent_before:
        raise ValueError("Reopened authority changed semantic team-accent")
    original_scene_frame = bpy.context.scene.frame_current
    texture_dir = output_dir / "textures"
    texture_paths, texture_records = write_texture_sources(
        donor_source,
        texture_dir=texture_dir,
        repo_root=repo_root,
    )
    loaded_images = {
        role: load_packed_image(path, role=role)
        for role, path in texture_paths.items()
    }
    dimensions = {
        role: [int(image.size[0]), int(image.size[1])]
        for role, image in loaded_images.items()
    }
    loaded_colorspaces = {
        role: image.colorspace_settings.name
        for role, image in loaded_images.items()
    }
    if any(width <= 0 or height <= 0 for width, height in dimensions.values()):
        raise ValueError("Donor texture has invalid dimensions")
    if (
        standalone_pbr is None
        and len({tuple(size) for size in dimensions.values()}) != 1
    ):
        raise ValueError("Donor PBR maps do not share one atlas resolution")
    if standalone_pbr is not None and (
        dimensions["baseColor"] != dimensions["normal"]
        or dimensions["metallicRoughness"]
        != [
            donor_source["composition"]["output"]["width"],
            donor_source["composition"]["output"]["height"],
        ]
    ):
        raise ValueError("Standalone PBR output dimensions drifted after load")

    for material in body_materials:
        rebuild_body_material(
            material,
            factors=donor_source["factors"],
            images=loaded_images,
            donor_sha256=args.expected_donor_sha256,
            payload_set_sha256=payload_set_sha256,
        )

    accent_after_in_memory = {
        material.name: material_fingerprint(material)
        for material in accent_materials
    }
    if accent_after_in_memory != accent_before:
        raise ValueError("Semantic team-accent material changed in memory")
    after_in_memory = authority_snapshot(objects, bind_frame=args.bind_frame)
    if after_in_memory != before:
        raise ValueError(
            "Authored transforms, meshes, UVs, or animation changed in memory"
        )

    bpy.context.scene.frame_set(original_scene_frame)
    bpy.context.view_layer.update()
    blend_path = output_dir / "textured-authority.blend"
    glb_path = output_dir / "textured-animated.glb"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    export_animated_glb(path=glb_path, collection=collection)
    canonicalize_exported_material_factors(
        glb_path,
        body_material_names=body_material_names,
        factors=donor_source["factors"],
        expected_texture_roles=set(donor_source["images"]),
    )
    output_glb_validation = validate_output_glb(
        glb_path,
        baseline_path=baseline_path,
        body_material_names=body_material_names,
        accent_material_names=accent_material_names,
        donor_source=donor_source,
        donor_sha256=args.expected_donor_sha256,
        payload_set_sha256=payload_set_sha256,
    )
    output_animation_names = sorted(
        animation["name"]
        for animation in output_glb_validation["animations"]
    )
    if output_animation_names != sorted(before["nlaTrackNames"]):
        raise ValueError(
            "Animated output GLB did not preserve semantic NLA clip names"
        )
    fresh_animated_baseline_surface = (
        surface_signature(import_glb_meshes(baseline_path))
        if coverage_pose is not None
        else None
    )
    baseline_path.unlink()

    output_surface = surface_signature(import_glb_meshes(glb_path))
    expected_output_surface = (
        fresh_animated_baseline_surface
        if coverage_pose is not None
        else uploaded_surface
    )
    if (
        output_surface["positionUvSurfaceSha256"]
        != expected_output_surface["positionUvSurfaceSha256"]
        or output_surface["triangles"] != expected_output_surface["triangles"]
    ):
        raise ValueError("Exported textured GLB geometry/UV drifted")

    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    persisted_collection = model_collection(args.model_collection)
    persisted_objects = model_objects(persisted_collection)
    persisted_body, persisted_accent = body_and_accent_materials(
        persisted_objects
    )
    persisted_pbr = validate_rebuilt_body_materials(
        persisted_body,
        donor_sha256=args.expected_donor_sha256,
        expected_roles=set(texture_paths),
        payload_set_sha256=payload_set_sha256,
    )
    after_persisted = authority_snapshot(
        persisted_objects,
        bind_frame=args.bind_frame,
    )
    if after_persisted != before:
        raise ValueError(
            "Persisted output changed authored transforms, mesh/UV, or animation"
        )
    accent_after_persisted = {
        material.name: material_fingerprint(material)
        for material in persisted_accent
    }
    if accent_after_persisted != accent_before:
        raise ValueError("Persisted output changed semantic team-accent")

    artifacts = [
        artifact_record(blend_path, repo_root=repo_root, kind="blend"),
        artifact_record(glb_path, repo_root=repo_root, kind="animated-glb"),
    ]
    seen_texture_paths: set[str] = set()
    for record in texture_records:
        if record["path"] in seen_texture_paths:
            continue
        seen_texture_paths.add(record["path"])
        artifacts.append(
            artifact_record(
                repo_root / record["path"],
                repo_root=repo_root,
                kind=f"texture-{record['role']}",
            )
        )
    checks = {
        "pinnedBlender52": bpy.app.version[:2] == EXPECTED_BLENDER,
        "providerDonorSurfaceSubsetCorrespondenceWithinBounds": (
            provider_surface_correspondence[
                "ambiguousOrDegenerateUvTriangles"
            ]
            == 0
            and provider_surface_correspondence["sourceOnly"][
                "withinBounds"
            ]
            and provider_surface_correspondence["uvWindingMismatches"] == 0
            and provider_surface_correspondence[
                "geometryWindingMismatches"
            ]
            == 0
            and provider_surface_correspondence["reflectionRejected"]
        ),
        "providerDonorExactOrientedUvTrianglePairing": (
            provider_surface_correspondence["triangles"]
            == donor_surface["triangles"]
            and provider_surface_correspondence["pairedCorners"]
            == donor_surface["triangles"] * 3
            and provider_surface_correspondence["sourceOnly"]["triangles"]
            == uploaded_surface["triangles"] - donor_surface["triangles"]
        ),
        "providerDonorSourceOnlyMicroSurfaceBounded": (
            provider_surface_correspondence["sourceOnly"]["withinBounds"]
        ),
        "providerDonorGlobalSimilarityWithinFixedResidual": (
            provider_surface_correspondence["acceptedTransform"][
                "residualNormalizedToDonorSpan"
            ]["maximum"]
            <= SIMILARITY_MAX_NORMALIZED_RESIDUAL
            and provider_surface_correspondence["acceptedTransform"][
                "residualNormalizedToDonorSpan"
            ]["rms"]
            <= SIMILARITY_RMS_NORMALIZED_RESIDUAL
            and provider_surface_correspondence["acceptedTransform"][
                "residualNormalizedToDonorSpan"
            ]["p99"]
            <= SIMILARITY_P99_NORMALIZED_RESIDUAL
        ),
        "providerDonorReflectionAndWindingRejected": (
            not provider_surface_correspondence["acceptedTransform"][
                "reflection"
            ]
            and provider_surface_correspondence["uvWindingParity"] == 1
            and provider_surface_correspondence[
                "geometryWindingParity"
            ]
            == 1
        ),
        "authoredBlendMatchesUploadedSourceSurfaceGeometryAndUv": (
            uploaded_authority_proof[
                "uploadedGeometryAccessorsSha256"
            ]
            == uploaded_authority_proof[
                (
                    "freshCoveragePoseGeometryAccessorsSha256"
                    if coverage_pose is not None
                    else "freshBaselineGeometryAccessorsSha256"
                )
            ]
            and uploaded_authority_proof["uploadedRawSurfaceDigest"]
            == uploaded_authority_proof[
                (
                    "freshCoveragePoseRawSurfaceDigest"
                    if coverage_pose is not None
                    else "freshBaselineRawSurfaceDigest"
                )
            ]
        ),
        "sharedBodyUvAtlasHasOnlyBoundedMicroExceptions": (
            uv_atlas["overlapPairs"] == 0
            and uv_atlas["ignoredMicroUvSurface"]["withinBounds"]
            and uv_atlas["ignoredMicroOverlap"]["withinBounds"]
        ),
        "providerGeometryNeverEnteredOutput": (
            after_persisted["objectNames"] == before["objectNames"]
        ),
        "objectTransformsUnchanged": (
            after_persisted["objectTransformsSha256"]
            == before["objectTransformsSha256"]
        ),
        "meshGeometryChecksumsUnchanged": (
            after_persisted["meshGeometrySha256"]
            == before["meshGeometrySha256"]
        ),
        "meshUvChecksumsUnchanged": (
            after_persisted["meshUvSha256"] == before["meshUvSha256"]
        ),
        "actionNamesAndKeyframesUnchanged": (
            after_persisted["actionsSha256"] == before["actionsSha256"]
        ),
        "actionEndpointsUnchanged": (
            after_persisted["actionEndpointsSha256"]
            == before["actionEndpointsSha256"]
        ),
        "animationBindingsUnchanged": (
            after_persisted["animationBindingsSha256"]
            == before["animationBindingsSha256"]
        ),
        "animatedGlbPreservesSemanticClipNames": (
            output_animation_names == sorted(before["nlaTrackNames"])
        ),
        "animatedGlbPreservesFullChannelsTargetsAndSamples": (
            output_glb_validation["matchesBaseline"][
                "animationChannelsTargetsTimesAndValues"
            ]
        ),
        "animatedGlbPreservesNodeHierarchyAndPivots": (
            output_glb_validation["matchesBaseline"][
                "nodeHierarchyAndPivots"
            ]
        ),
        "animatedGlbPreservesGeometryTopologyUvAndVertexAttributes": (
            output_glb_validation["matchesBaseline"][
                "meshAccessorsAndMaterialBindings"
            ]
        ),
        "animatedGlbPreservesSkinsAndInverseBindMatrices": (
            output_glb_validation["matchesBaseline"][
                "skinsAndInverseBindMatrices"
            ]
        ),
        "animatedGlbPreservesSceneRoots": (
            output_glb_validation["matchesBaseline"]["sceneRoots"]
        ),
        "animatedGlbPreservesPbrFactorsTexturesAndSamplers": (
            set(
                output_glb_validation["materials"]["bodyMaterials"]
            )
            == set(body_material_names)
        ),
        "animatedGlbProvidesPortableNormalMapTangentBasisInputs": (
            output_glb_validation["materials"]["normalMapTangentBasis"][
                "requiredInputsPresent"
            ]
        ),
        "animatedGlbPreservesTextureFreeSemanticTeamAccent": (
            output_glb_validation["materials"]["teamAccentMaterials"]
            == accent_material_names
        ),
        "teamAccentRemainsSemanticAndTextureFree": (
            accent_after_persisted == accent_before
        ),
        "outputAnimatedGlbMatchesAuthoredSurfaceGeometryAndUv": (
            output_surface["positionUvSurfaceSha256"]
            == uploaded_surface["positionUvSurfaceSha256"]
        ),
        "allPbrMapsShareAtlasResolution": (
            len({tuple(value) for value in dimensions.values()}) == 1
        ),
        "rebuiltPbrMapsRemainPackedAfterSave": (
            set(persisted_pbr) == set(body_material_names)
            and all(
                set(roles) == set(texture_paths)
                for roles in persisted_pbr.values()
            )
        ),
        "offlineProviderCallsZero": True,
        "offlineNetworkCallsZero": True,
    }
    if standalone_pbr is not None:
        checks.pop("allPbrMapsShareAtlasResolution")
        output_body_materials = output_glb_validation["materials"][
            "bodyMaterials"
        ]
        checks.update(
            {
                "standaloneBaseColorAndNormalDimensionsMatch": (
                    dimensions["baseColor"] == dimensions["normal"]
                ),
                "standaloneMetallicAndRoughnessDimensionsMatch": (
                    donor_source["standaloneSources"]["metallic"]["width"]
                    == donor_source["standaloneSources"]["roughness"]["width"]
                    and donor_source["standaloneSources"]["metallic"]["height"]
                    == donor_source["standaloneSources"]["roughness"]["height"]
                ),
                "standalonePngColorSpacesAreRoleCorrect": (
                    loaded_colorspaces
                    == {
                        role: donor_source["colorspaces"][role]
                        for role in donor_source["images"]
                    }
                ),
                "standalonePngPayloadsRemainLosslessInAnimatedGlb": all(
                    all(
                        body["textures"][role]["sha256"]
                        == donor_source["images"][role]["sha256"]
                        and body["textures"][role]["mimeType"]
                        == "image/png"
                        for role in donor_source["standaloneOutputRoles"]
                    )
                    for body in output_body_materials.values()
                ),
                "metallicRoughnessChannelsAreLosslesslyComposed": (
                    donor_source["composition"]["channelProof"][
                        "greenRoughnessSamplesSha256"
                    ]
                    == donor_source["composition"]["roughness"][
                        "scalarSamplesSha256"
                    ]
                    and donor_source["composition"]["channelProof"][
                        "blueMetallicSamplesSha256"
                    ]
                    == donor_source["composition"]["metallic"][
                        "scalarSamplesSha256"
                    ]
                    and donor_source["composition"]["output"]["sha256"]
                    == donor_source["images"]["metallicRoughness"]["sha256"]
                ),
                "standalonePngInputsRemainPinned": all(
                    sha256(record["path"]) == record["sha256"]
                    for record in standalone_pbr.values()
                ),
            }
        )
    if coverage_pose is not None:
        checks.pop("authoredBlendMatchesUploadedSourceSurfaceGeometryAndUv")
        checks.pop("outputAnimatedGlbMatchesAuthoredSurfaceGeometryAndUv")
        checks.update(
            {
                "coverageValidationReportPinsAuthorityUploadPoseAndChecks": (
                    coverage_ledger["present"]
                    and coverage_ledger["authoredBlend"]["sha256"]
                    == args.expected_authored_sha256
                    and coverage_ledger["uploadedSourceGlb"]["sha256"]
                    == args.expected_uploaded_source_sha256
                    and coverage_ledger["pose"]["activeClips"]
                    == list(coverage_pose["clips"])
                    and coverage_ledger["pose"]["frame"]
                    == coverage_pose["frame"]
                ),
                "authoredCoveragePoseExactlyReproducesUploadedGeometryUv": (
                    uploaded_authority_proof[
                        "uploadedGeometryAccessorsSha256"
                    ]
                    == uploaded_authority_proof[
                        "freshCoveragePoseGeometryAccessorsSha256"
                    ]
                    and uploaded_authority_proof[
                        "uploadedRawSurfaceDigest"
                    ]
                    == uploaded_authority_proof[
                        "freshCoveragePoseRawSurfaceDigest"
                    ]
                ),
                "authoredCoveragePoseExactlyReproducesNamedIdentityRoots": (
                    uploaded_authority_proof[
                        "uploadedIdentityRootNodesSha256"
                    ]
                    == uploaded_authority_proof[
                        "freshCoveragePoseIdentityRootNodesSha256"
                    ]
                    and uploaded_authority_proof["objects"]
                    == coverage_ledger["expected"]["meshes"]
                    and uploaded_authority_proof["triangles"]
                    == coverage_ledger["expected"]["triangles"]
                ),
                "coveragePoseUsesTemporaryWorldBakedBodyCopiesOnly": (
                    coverage_baseline_record is not None
                    and coverage_baseline_record[
                        "worldTransformsBakedIntoTemporaryCopies"
                    ]
                    and coverage_baseline_record["identityRootMeshes"]
                    == coverage_ledger["expected"]["meshes"]
                    and coverage_baseline_record[
                        "providerObjectsImportedIntoAuthorityScene"
                    ]
                    == 0
                    and not any(
                        name.startswith("__NB_TRANSPLANT_COVERAGE_")
                        for name in after_persisted["objectNames"]
                    )
                ),
                "outputAnimatedGlbMatchesFreshAnimatedAuthoritySurface": (
                    fresh_animated_baseline_surface is not None
                    and output_surface["positionUvSurfaceSha256"]
                    == fresh_animated_baseline_surface[
                        "positionUvSurfaceSha256"
                    ]
                    and output_surface["triangles"]
                    == fresh_animated_baseline_surface["triangles"]
                ),
            }
        )
    if any(value is not True for value in checks.values()):
        raise ValueError("Texture transplant validation did not pass")

    report_inputs = {
        "authoredBlend": {
            "path": authored_relative.as_posix(),
            "sha256": args.expected_authored_sha256,
        },
        "uploadedSourceGlb": {
            "path": uploaded_relative.as_posix(),
            "sha256": args.expected_uploaded_source_sha256,
        },
        "donorGlb": {
            "path": donor_relative.as_posix(),
            "sha256": args.expected_donor_sha256,
        },
    }
    if standalone_pbr is not None:
        report_inputs.update(
            {
                record["reportKey"]: {
                    "path": record["relative"].as_posix(),
                    "sha256": record["sha256"],
                }
                for record in standalone_pbr.values()
            }
        )
    if coverage_pose is not None:
        report_inputs[coverage_pose["reportKey"]] = {
            "path": coverage_pose["relative"].as_posix(),
            "sha256": coverage_pose["sha256"],
        }
    pbr_report = {
        "factors": donor_source["factors"],
        "samplers": donor_source["samplers"],
        "dimensions": dimensions,
        "textures": texture_records,
        "bodyMaterialsRebuilt": body_material_names,
        "persistedBodyMaterialMaps": persisted_pbr,
        "teamAccentMaterialsUntouched": sorted(accent_before),
        "materialConsumersInsideModelCollection": material_scope,
    }
    if standalone_pbr is not None:
        pbr_report.update(
            {
                "payloadMode": donor_source["payloadMode"],
                "payloadSetSha256": donor_source["payloadSetSha256"],
                "standaloneSources": donor_source["standaloneSources"],
                "metallicRoughnessComposition": donor_source["composition"],
                "colorspaces": donor_source["colorspaces"],
                "loadedColorspaces": loaded_colorspaces,
                "resolutionContract": donor_source["resolutionContract"],
                "standaloneOutputRoles": donor_source[
                    "standaloneOutputRoles"
                ],
                "embeddedDonorFallbackRoles": donor_source[
                    "embeddedDonorFallbackRoles"
                ],
            }
        )
    report = {
        "schema": report_schema,
        "status": "PASS",
        "outputDirectory": output_relative.as_posix(),
        "inputs": report_inputs,
        "blender": {
            "version": bpy.app.version_string,
            "expectedMajorMinor": list(EXPECTED_BLENDER),
            "background": bool(bpy.app.background),
        },
        "method": {
            "offline": True,
            "providerCalls": 0,
            "networkCalls": 0,
            "geometryAuthority": "authored-blend-only",
            "providerUse": (
                "donor GLB geometry/UV validation plus material factors and "
                "samplers; standalone lossless PNG image payloads only"
                if standalone_pbr is not None
                else "PBR image bytes and numeric material factors only"
            ),
            "providerObjectsImportedIntoAuthorityScene": 0,
            "providerActionsImportedIntoAuthorityScene": 0,
            "providerNodeGraphsImportedIntoAuthorityScene": 0,
            "surfaceQuantum": SURFACE_QUANTUM,
            "providerSimilarityResidualThresholds": {
                "maximumNormalized": (
                    SIMILARITY_MAX_NORMALIZED_RESIDUAL
                ),
                "rmsNormalized": (
                    SIMILARITY_RMS_NORMALIZED_RESIDUAL
                ),
                "p99Normalized": (
                    SIMILARITY_P99_NORMALIZED_RESIDUAL
                ),
            },
            "bindFrame": args.bind_frame,
            "modelCollection": args.model_collection,
        },
        "surfaceCorrespondence": {
            "uploadedSource": uploaded_surface,
            "donor": donor_surface,
            "uploadedSourceToDonor": provider_surface_correspondence,
            "authoredBlend": authored_surface,
            "authoredBlendFreshBaselineToUploadedSource": (
                uploaded_authority_proof
            ),
            "texturedAnimatedGlb": output_surface,
        },
        "authorityValidationLedger": authority_ledger,
        "uvAtlas": uv_atlas,
        "pbr": pbr_report,
        "authoritySnapshot": {
            "before": before,
            "after": after_persisted,
        },
        "outputGlbAnimationNames": output_animation_names,
        "outputGlbValidation": output_glb_validation,
        "checks": checks,
        "artifacts": artifacts,
    }
    if coverage_pose is not None:
        report["method"].update(
            {
                "uploadValidationMode": "pinned-world-baked-coverage-pose",
                "coveragePoseClips": list(coverage_pose["clips"]),
                "coveragePoseFrame": coverage_pose["frame"],
                "coveragePoseTransformPolicy": COVERAGE_TRANSFORM_POLICY,
                "coveragePoseTemporaryGeometryPersisted": False,
            }
        )
        report["surfaceCorrespondence"][
            "authoredBlendCoveragePoseToUploadedSource"
        ] = report["surfaceCorrespondence"].pop(
            "authoredBlendFreshBaselineToUploadedSource"
        )
        report["coverageValidationLedger"] = coverage_ledger
        report["coveragePoseReconstruction"] = coverage_baseline_record
    for path, expected, label in expected_inputs:
        observed = sha256(path)
        if observed != expected:
            raise ValueError(
                f"{label} hash drifted during Blender processing: "
                f"{observed} != {expected}"
            )
    if (
        authority_ledger_path is not None
        and sha256(authority_ledger_path) != authority_ledger_sha256
    ):
        raise ValueError(
            "Authored authority validation ledger drifted during processing"
        )
    validate_report_sanitized(report)
    report_path = output_dir / "report.json"
    report_path.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(
        stable_json(
            {
                "status": "PASS",
                "report": report_path.relative_to(repo_root).as_posix(),
                "surfaceSha256": authored_surface[
                    "positionUvSurfaceSha256"
                ],
                "actionNames": before["actionNames"],
            }
        )
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception:
        traceback.print_exc()
        raise SystemExit(1)
