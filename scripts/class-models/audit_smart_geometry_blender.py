#!/usr/bin/env python3
"""Audit one generated GLB and render fixed comparison views with Blender 5.2.

This is an offline inspection tool. It never edits the input model, calls a
provider, or writes outside the explicitly supplied audit directory.

Normally invoke this through ``audit_smart_geometry.py``, which resolves the
pinned Blender executable and writes the compact report after this detailed
audit. Direct Blender use remains available for debugging:

  Blender --background --factory-startup \
    --python scripts/class-models/audit_smart_geometry_blender.py -- \
    --input /absolute/path/to/model.glb \
    --audit-dir /absolute/path/to/var/class-models/audits/candidate-id \
    --canonical-top /absolute/path/to/transparent-top-authority.png

The audit directory must be new, absolute, below a ``sandbox`` or ``var`` path
component, and contain ``audit`` in one of the components below that scratch
root. Existing output is never overwritten.

The topology, hierarchy, normalization, neutral-rig, and PNG-canonicalization
core was promoted from the Meshy T2 audit proven in repository commit
``fd1be9b``. This version adds the missing strict axes and objective alpha-mask
comparison while retaining that tool's non-mutating measurements.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import struct
import sys
import unicodedata
import zlib
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable

import bpy
from mathutils import Matrix, Vector


AUDIT_SCHEMA = "nilbots-smart-geometry-audit-2"
TOOL_VERSION = "2.0.0"
EXPECTED_BLENDER = (5, 2)
WELD_EPSILON = 1.0e-6
RENDER_SIZE = 768
NORMALIZED_PLANFORM_SPAN = 1.0
SILHOUETTE_ALPHA_THRESHOLD = 1.0 / 255.0
SILHOUETTE_COMPARE_SIZE = 768
SILHOUETTE_TARGET_SPAN = 620
SAFE_SCRATCH_COMPONENTS = frozenset(("sandbox", "var"))
VIEW_FILENAMES = {
    "top": "top.png",
    "side": "side.png",
    "front": "front.png",
    "rear": "rear.png",
    "frontThreeQuarter": "front-three-quarter.png",
    "rearThreeQuarter": "rear-three-quarter.png",
    "gameplay58": "gameplay-58.png",
}


class UnionFind:
    def __init__(self, size: int) -> None:
        self.parent = list(range(size))
        self.rank = [0] * size

    def find(self, item: int) -> int:
        parent = self.parent[item]
        while parent != self.parent[parent]:
            self.parent[parent] = self.parent[self.parent[parent]]
            parent = self.parent[parent]
        while item != parent:
            next_item = self.parent[item]
            self.parent[item] = parent
            item = next_item
        return parent

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


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(
        description=(
            "Offline Blender 5.2 GLB topology audit with fixed, normalized "
            "comparison renders and an optional alpha-mask silhouette "
            "comparison. No provider or network calls are made."
        ),
        epilog=(
            "Method: exact GLB document counts are recorded before Blender "
            "import; imported geometry is measured in source world space; "
            "connectivity is reported both by index and after a 1e-6 "
            "coincident-position weld; then one uniform center/floor/"
            "planform normalization feeds seven fixed cameras. Names, pivots, "
            "and AABB overlap are observations only—semantic parts are never "
            "inferred. A supplied canonical top image must carry meaningful "
            "alpha and is compared at centered, equal maximum-planform span "
            "under every quarter-turn. No acceptance threshold or subjective "
            "pass/fail is invented. The input is never modified."
        ),
    )
    parser.add_argument(
        "--input",
        type=Path,
        required=True,
        help="Absolute path to the existing GLB to inspect (never modified).",
    )
    parser.add_argument(
        "--audit-dir",
        type=Path,
        required=True,
        help=(
            "New absolute output directory below sandbox/ or var/; an output "
            "component below that root must contain the word 'audit'."
        ),
    )
    parser.add_argument(
        "--canonical-top",
        type=Path,
        help=(
            "Optional absolute transparent PNG top-view authority. Its alpha "
            "mask is compared with the generated top silhouette without "
            "choosing a semantic facing or an acceptance threshold."
        ),
    )
    parser.add_argument(
        "--rigid-rotation-x-degrees",
        type=int,
        choices=(-90, 0, 90, 180),
        default=0,
        help=(
            "Optional recorded rigid quarter-turn around Blender import-space "
            "X before measurement/rendering. It never edits the source GLB."
        ),
    )
    parser.add_argument(
        "--rigid-rotation-z-degrees",
        type=int,
        choices=(-90, 0, 90, 180),
        default=0,
        help=(
            "Optional recorded rigid quarter-turn around Blender import-space "
            "Z after the X turn. It can normalize inspected +X facing."
        ),
    )
    return parser.parse_args(argv)


def sanitize_name(value: Any, *, fallback: str = "") -> str:
    if value is None:
        return fallback
    normalized = unicodedata.normalize("NFKC", str(value))
    printable = "".join(
        character
        for character in normalized
        if character in ("\t", " ")
        or not unicodedata.category(character).startswith("C")
    )
    return printable.strip()[:160] or fallback


def rounded(value: float) -> float:
    if not math.isfinite(value):
        raise ValueError("Audit encountered a non-finite numeric value")
    result = round(float(value), 9)
    return 0.0 if result == -0.0 else result


def vector_json(value: Iterable[float]) -> list[float]:
    return [rounded(component) for component in value]


def matrix_json(value: Matrix) -> list[list[float]]:
    return [vector_json(row) for row in value]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
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


def canonicalize_png(path: Path) -> None:
    """Emit metadata-free, fixed-filter PNG bytes with stable 6-bit RGB."""
    source = path.read_bytes()
    signature = b"\x89PNG\r\n\x1a\n"
    if not source.startswith(signature):
        raise ValueError(f"Blender did not emit a PNG: {path.name}")
    chunks: list[tuple[bytes, bytes]] = []
    offset = len(signature)
    while offset < len(source):
        if offset + 12 > len(source):
            raise ValueError(f"Truncated PNG chunk in {path.name}")
        chunk_length = struct.unpack(">I", source[offset : offset + 4])[0]
        chunk_end = offset + 12 + chunk_length
        if chunk_end > len(source):
            raise ValueError(f"Truncated PNG payload in {path.name}")
        chunk_type = source[offset + 4 : offset + 8]
        payload = source[offset + 8 : offset + 8 + chunk_length]
        chunks.append((chunk_type, payload))
        offset = chunk_end
        if chunk_type == b"IEND":
            break
    if offset != len(source):
        raise ValueError(f"Unexpected trailing bytes in {path.name}")

    ihdr = next((payload for kind, payload in chunks if kind == b"IHDR"), None)
    if ihdr is None or len(ihdr) != 13:
        raise ValueError(f"PNG has no valid IHDR: {path.name}")
    width, height, bit_depth, color_type, compression, filtering, interlace = (
        struct.unpack(">IIBBBBB", ihdr)
    )
    if (bit_depth, color_type, compression, filtering, interlace) != (
        8,
        6,
        0,
        0,
        0,
    ):
        raise ValueError(
            f"Expected non-interlaced 8-bit RGBA Blender PNG: {path.name}"
        )
    compressed = b"".join(payload for kind, payload in chunks if kind == b"IDAT")
    encoded = zlib.decompress(compressed)
    bytes_per_pixel = 4
    row_bytes = width * bytes_per_pixel
    expected_size = height * (row_bytes + 1)
    if len(encoded) != expected_size:
        raise ValueError(f"Unexpected PNG scanline size in {path.name}")

    reconstructed_rows: list[bytearray] = []
    cursor = 0
    previous = bytearray(row_bytes)
    for _row_index in range(height):
        filter_type = encoded[cursor]
        cursor += 1
        filtered = encoded[cursor : cursor + row_bytes]
        cursor += row_bytes
        row = bytearray(row_bytes)
        for index, value in enumerate(filtered):
            left = row[index - bytes_per_pixel] if index >= bytes_per_pixel else 0
            above = previous[index]
            upper_left = (
                previous[index - bytes_per_pixel]
                if index >= bytes_per_pixel
                else 0
            )
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
        decoded_row = row.copy()
        # Eevee may vary slightly across processes on a coplanar shadow edge.
        # Six-bit RGB removes ordinary low-order noise; dark values use four
        # bits because the same edge can cross more than one low-order bucket.
        # Alpha is untouched and the gameplay-scale material read is retained.
        for index in range(0, row_bytes, bytes_per_pixel):
            for channel in range(3):
                value = row[index + channel]
                row[index + channel] = value & (0xF0 if value < 80 else 0xFC)
        reconstructed_rows.append(row)
        previous = decoded_row

    raw = b"".join(b"\x00" + bytes(row) for row in reconstructed_rows)
    retained_types = {b"sRGB", b"gAMA", b"cHRM", b"oFFs", b"pHYs"}
    output = bytearray(signature)
    output.extend(png_chunk(b"IHDR", ihdr))
    for kind, payload in chunks:
        if kind in retained_types:
            output.extend(png_chunk(kind, payload))
    output.extend(png_chunk(b"IDAT", zlib.compress(raw, level=9)))
    output.extend(png_chunk(b"IEND", b""))
    temporary = path.with_suffix(path.suffix + ".sanitizing")
    temporary.write_bytes(output)
    temporary.replace(path)


def decode_rgba_png(path: Path) -> tuple[int, int, list[bytearray]]:
    """Decode the exact non-interlaced 8-bit RGBA PNG subset used by audits."""
    source = path.read_bytes()
    signature = b"\x89PNG\r\n\x1a\n"
    if not source.startswith(signature):
        raise ValueError(f"Expected a PNG file: {path.name}")
    chunks: list[tuple[bytes, bytes]] = []
    offset = len(signature)
    while offset < len(source):
        if offset + 12 > len(source):
            raise ValueError(f"Truncated PNG chunk in {path.name}")
        chunk_length = struct.unpack(">I", source[offset : offset + 4])[0]
        chunk_end = offset + 12 + chunk_length
        if chunk_end > len(source):
            raise ValueError(f"Truncated PNG payload in {path.name}")
        chunk_type = source[offset + 4 : offset + 8]
        payload = source[offset + 8 : offset + 8 + chunk_length]
        chunks.append((chunk_type, payload))
        offset = chunk_end
        if chunk_type == b"IEND":
            break
    if offset != len(source):
        raise ValueError(f"Unexpected trailing bytes in {path.name}")

    ihdr = next((payload for kind, payload in chunks if kind == b"IHDR"), None)
    if ihdr is None or len(ihdr) != 13:
        raise ValueError(f"PNG has no valid IHDR: {path.name}")
    width, height, bit_depth, color_type, compression, filtering, interlace = (
        struct.unpack(">IIBBBBB", ihdr)
    )
    if (bit_depth, color_type, compression, filtering, interlace) != (
        8,
        6,
        0,
        0,
        0,
    ):
        raise ValueError(
            "Silhouette inputs must be non-interlaced 8-bit RGBA PNGs: "
            f"{path.name}"
        )

    compressed = b"".join(payload for kind, payload in chunks if kind == b"IDAT")
    encoded = zlib.decompress(compressed)
    bytes_per_pixel = 4
    row_bytes = width * bytes_per_pixel
    if len(encoded) != height * (row_bytes + 1):
        raise ValueError(f"Unexpected PNG scanline size in {path.name}")

    rows: list[bytearray] = []
    cursor = 0
    previous = bytearray(row_bytes)
    for _row_index in range(height):
        filter_type = encoded[cursor]
        cursor += 1
        filtered = encoded[cursor : cursor + row_bytes]
        cursor += row_bytes
        row = bytearray(row_bytes)
        for index, value in enumerate(filtered):
            left = row[index - bytes_per_pixel] if index >= bytes_per_pixel else 0
            above = previous[index]
            upper_left = (
                previous[index - bytes_per_pixel]
                if index >= bytes_per_pixel
                else 0
            )
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


def alpha_mask(path: Path) -> tuple[int, int, set[tuple[int, int]]]:
    width, height, rows = decode_rgba_png(path)
    threshold = round(SILHOUETTE_ALPHA_THRESHOLD * 255.0)
    mask = {
        (x, y)
        for y, row in enumerate(rows)
        for x in range(width)
        if row[x * 4 + 3] > threshold
    }
    if not mask:
        raise ValueError(f"Silhouette alpha mask is empty: {path.name}")
    if len(mask) == width * height:
        raise ValueError(
            "Silhouette authority needs meaningful transparency; alpha is "
            f"opaque across the entire image: {path.name}"
        )
    return width, height, mask


def mask_bounds(mask: set[tuple[int, int]]) -> tuple[int, int, int, int]:
    xs = [point[0] for point in mask]
    ys = [point[1] for point in mask]
    return min(xs), min(ys), max(xs), max(ys)


def normalize_mask(
    mask: set[tuple[int, int]],
    *,
    canvas_size: int = SILHOUETTE_COMPARE_SIZE,
    target_span: int = SILHOUETTE_TARGET_SPAN,
) -> set[tuple[int, int]]:
    minimum_x, minimum_y, maximum_x, maximum_y = mask_bounds(mask)
    source_span = max(maximum_x - minimum_x + 1, maximum_y - minimum_y + 1)
    if source_span <= 0:
        raise ValueError("Cannot normalize a zero-span silhouette mask")
    scale = target_span / source_span
    source_center_x = (minimum_x + maximum_x) * 0.5
    source_center_y = (minimum_y + maximum_y) * 0.5
    target_center = (canvas_size - 1) * 0.5
    normalized: set[tuple[int, int]] = set()
    for x, y in mask:
        target_x = int(round((x - source_center_x) * scale + target_center))
        target_y = int(round((y - source_center_y) * scale + target_center))
        if 0 <= target_x < canvas_size and 0 <= target_y < canvas_size:
            normalized.add((target_x, target_y))
    return normalized


def rotate_mask_quarter_turn(
    mask: set[tuple[int, int]],
    quarter_turns_clockwise: int,
    *,
    canvas_size: int = SILHOUETTE_COMPARE_SIZE,
) -> set[tuple[int, int]]:
    result = mask
    for _turn in range(quarter_turns_clockwise % 4):
        result = {(canvas_size - 1 - y, x) for x, y in result}
    return result


def mask_centroid(mask: set[tuple[int, int]]) -> tuple[float, float]:
    return (
        sum(point[0] for point in mask) / len(mask),
        sum(point[1] for point in mask) / len(mask),
    )


def write_overlay_png(
    path: Path,
    canonical: set[tuple[int, int]],
    candidate: set[tuple[int, int]],
) -> None:
    rows: list[bytes] = []
    for y in range(SILHOUETTE_COMPARE_SIZE):
        row = bytearray()
        for x in range(SILHOUETTE_COMPARE_SIZE):
            point = (x, y)
            if point in canonical and point in candidate:
                row.extend((236, 240, 246, 255))
            elif point in canonical:
                row.extend((236, 72, 153, 255))
            elif point in candidate:
                row.extend((34, 211, 238, 255))
            else:
                row.extend((11, 16, 32, 255))
        rows.append(b"\x00" + bytes(row))
    ihdr = struct.pack(
        ">IIBBBBB",
        SILHOUETTE_COMPARE_SIZE,
        SILHOUETTE_COMPARE_SIZE,
        8,
        6,
        0,
        0,
        0,
    )
    payload = b"".join(rows)
    output = bytearray(b"\x89PNG\r\n\x1a\n")
    output.extend(png_chunk(b"IHDR", ihdr))
    output.extend(png_chunk(b"IDAT", zlib.compress(payload, level=9)))
    output.extend(png_chunk(b"IEND", b""))
    path.write_bytes(output)


def silhouette_metrics(
    canonical: set[tuple[int, int]],
    candidate: set[tuple[int, int]],
) -> dict[str, Any]:
    intersection = canonical & candidate
    union = canonical | candidate
    canonical_centroid = mask_centroid(canonical)
    candidate_centroid = mask_centroid(candidate)
    canonical_bounds = mask_bounds(canonical)
    candidate_bounds = mask_bounds(candidate)
    return {
        "intersectionOverUnion": rounded(len(intersection) / len(union)),
        "canonicalCoverage": rounded(len(intersection) / len(canonical)),
        "candidateInsideCanonical": rounded(len(intersection) / len(candidate)),
        "candidateAreaDelta": rounded(len(candidate) / len(canonical) - 1.0),
        "centroidDeltaPixels": [
            rounded(candidate_centroid[0] - canonical_centroid[0]),
            rounded(candidate_centroid[1] - canonical_centroid[1]),
        ],
        "canonicalPixelCount": len(canonical),
        "candidatePixelCount": len(candidate),
        "intersectionPixelCount": len(intersection),
        "unionPixelCount": len(union),
        "canonicalBounds": list(canonical_bounds),
        "candidateBounds": list(candidate_bounds),
    }


def compare_top_silhouettes(
    candidate_path: Path,
    canonical_path: Path,
    audit_dir: Path,
) -> dict[str, Any]:
    canonical_width, canonical_height, canonical_raw = alpha_mask(canonical_path)
    candidate_width, candidate_height, candidate_raw = alpha_mask(candidate_path)
    canonical = normalize_mask(canonical_raw)
    candidate = normalize_mask(candidate_raw)
    rotations = []
    for quarter_turn in range(4):
        rotated = rotate_mask_quarter_turn(candidate, quarter_turn)
        overlay_path = (
            audit_dir / f"top-silhouette-overlay-yaw-{quarter_turn * 90:03d}.png"
        )
        write_overlay_png(overlay_path, canonical, rotated)
        rotations.append(
            {
                "candidateClockwiseYawDegrees": quarter_turn * 90,
                **silhouette_metrics(canonical, rotated),
                "overlay": {
                    "file": overlay_path.name,
                    "bytes": overlay_path.stat().st_size,
                    "sha256": sha256(overlay_path),
                },
            }
        )
    return {
        "method": (
            "alpha greater than 1/255; independently center each occupied "
            "bounding box and uniformly scale its maximum span to 620 pixels "
            "on a 768-square canvas; nearest-pixel rasterization; compare all "
            "four candidate quarter-turns without selecting semantic facing"
        ),
        "canonical": {
            "fileName": sanitize_name(canonical_path.name),
            "sha256": sha256(canonical_path),
            "bytes": canonical_path.stat().st_size,
            "sourceDimensions": [canonical_width, canonical_height],
            "sourceOccupiedPixels": len(canonical_raw),
        },
        "candidate": {
            "fileName": candidate_path.name,
            "sha256": sha256(candidate_path),
            "bytes": candidate_path.stat().st_size,
            "sourceDimensions": [candidate_width, candidate_height],
            "sourceOccupiedPixels": len(candidate_raw),
        },
        "comparisonCanvas": [
            SILHOUETTE_COMPARE_SIZE,
            SILHOUETTE_COMPARE_SIZE,
        ],
        "normalizedMaximumSpanPixels": SILHOUETTE_TARGET_SPAN,
        "alphaThresholdGreaterThan": SILHOUETTE_ALPHA_THRESHOLD,
        "rotations": rotations,
        "acceptanceThreshold": None,
        "semanticFacingSelected": False,
        "note": (
            "These measurements are acceptance inputs only. Inspect the "
            "fixed views to identify the real +X-facing quarter-turn and "
            "evaluate named anchors independently."
        ),
    }


def validate_paths(
    input_path: Path,
    audit_dir: Path,
    canonical_top: Path | None,
) -> tuple[Path, Path, Path | None]:
    if not input_path.is_absolute():
        raise ValueError("--input must be an absolute path")
    input_path = input_path.resolve(strict=True)
    if not input_path.is_file() or input_path.suffix.lower() != ".glb":
        raise ValueError("--input must name an existing .glb file")

    if not audit_dir.is_absolute():
        raise ValueError("--audit-dir must be an absolute path")
    unresolved = audit_dir
    if unresolved.exists():
        raise ValueError(
            "--audit-dir must not already exist; outputs are never overwritten"
        )
    for ancestor in (unresolved, *unresolved.parents):
        if ancestor.exists() and ancestor.is_symlink():
            raise ValueError("--audit-dir may not traverse an existing symbolic link")

    audit_dir = unresolved.resolve(strict=False)
    parts = audit_dir.parts
    scratch_indices = [
        index
        for index, component in enumerate(parts)
        if component in SAFE_SCRATCH_COMPONENTS
    ]
    if not scratch_indices:
        raise ValueError("--audit-dir must be below a sandbox/ or var/ path component")
    scratch_index = scratch_indices[-1]
    output_parts = parts[scratch_index + 1 :]
    if not output_parts or not any("audit" in part.lower() for part in output_parts):
        raise ValueError(
            "--audit-dir must include an 'audit' path component below sandbox/ or var/"
        )
    if audit_dir == Path("/") or audit_dir == Path.home().resolve():
        raise ValueError("Refusing a broad audit output path")
    try:
        input_path.relative_to(audit_dir)
    except ValueError:
        pass
    else:
        raise ValueError("--input may not be inside --audit-dir")

    if canonical_top is not None:
        if not canonical_top.is_absolute():
            raise ValueError("--canonical-top must be an absolute path")
        canonical_top = canonical_top.resolve(strict=True)
        if not canonical_top.is_file() or canonical_top.suffix.lower() != ".png":
            raise ValueError("--canonical-top must name an existing PNG file")
        try:
            canonical_top.relative_to(audit_dir)
        except ValueError:
            pass
        else:
            raise ValueError("--canonical-top may not be inside --audit-dir")
    return input_path, audit_dir, canonical_top


def read_glb_document(path: Path) -> dict[str, Any]:
    with path.open("rb") as stream:
        header = stream.read(12)
        if len(header) != 12:
            raise ValueError("Input is too short to be a GLB")
        magic, version, declared_length = struct.unpack("<4sII", header)
        if magic != b"glTF" or version != 2:
            raise ValueError("Input is not a GLB 2.0 document")
        if declared_length != path.stat().st_size:
            raise ValueError("GLB header length does not match file size")
        while stream.tell() < declared_length:
            chunk_header = stream.read(8)
            if len(chunk_header) != 8:
                raise ValueError("Truncated GLB chunk header")
            chunk_length, chunk_type = struct.unpack("<II", chunk_header)
            payload = stream.read(chunk_length)
            if len(payload) != chunk_length:
                raise ValueError("Truncated GLB chunk")
            if chunk_type == 0x4E4F534A:
                document = json.loads(payload.rstrip(b" \t\r\n\x00").decode("utf-8"))
                if not isinstance(document, dict):
                    raise ValueError("GLB JSON root must be an object")
                return document
    raise ValueError("GLB contains no JSON chunk")


def source_document_summary(document: dict[str, Any]) -> dict[str, Any]:
    meshes = document.get("meshes", [])
    return {
        "assetVersion": sanitize_name(document.get("asset", {}).get("version")),
        "generator": sanitize_name(document.get("asset", {}).get("generator")),
        "counts": {
            "scenes": len(document.get("scenes", [])),
            "nodes": len(document.get("nodes", [])),
            "meshes": len(meshes),
            "primitives": sum(len(mesh.get("primitives", [])) for mesh in meshes),
            "materials": len(document.get("materials", [])),
            "images": len(document.get("images", [])),
            "textures": len(document.get("textures", [])),
            "samplers": len(document.get("samplers", [])),
            "animations": len(document.get("animations", [])),
            "skins": len(document.get("skins", [])),
            "cameras": len(document.get("cameras", [])),
            "accessors": len(document.get("accessors", [])),
            "bufferViews": len(document.get("bufferViews", [])),
            "buffers": len(document.get("buffers", [])),
        },
        "names": {
            "nodes": [
                sanitize_name(node.get("name"), fallback=f"node-{index}")
                for index, node in enumerate(document.get("nodes", []))
            ],
            "meshes": [
                sanitize_name(mesh.get("name"), fallback=f"mesh-{index}")
                for index, mesh in enumerate(meshes)
            ],
            "materials": [
                sanitize_name(material.get("name"), fallback=f"material-{index}")
                for index, material in enumerate(document.get("materials", []))
            ],
            "animations": [
                sanitize_name(animation.get("name"), fallback=f"animation-{index}")
                for index, animation in enumerate(document.get("animations", []))
            ],
            "skins": [
                sanitize_name(skin.get("name"), fallback=f"skin-{index}")
                for index, skin in enumerate(document.get("skins", []))
            ],
        },
        "extensionsUsed": sorted(
            sanitize_name(value) for value in document.get("extensionsUsed", [])
        ),
        "extensionsRequired": sorted(
            sanitize_name(value) for value in document.get("extensionsRequired", [])
        ),
    }


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
    imported.sort(key=lambda obj: (sanitize_name(obj.name), obj.type))
    if not any(obj.type == "MESH" for obj in imported):
        raise ValueError("Imported GLB contains no mesh object")
    bpy.context.view_layer.update()
    return imported


def apply_rigid_orientation(
    imported: list[bpy.types.Object],
    rotation_x_degrees: int,
    rotation_z_degrees: int,
) -> dict[str, Any]:
    rotation_x = {
        -90: Matrix(
            (
                (1.0, 0.0, 0.0, 0.0),
                (0.0, 0.0, 1.0, 0.0),
                (0.0, -1.0, 0.0, 0.0),
                (0.0, 0.0, 0.0, 1.0),
            )
        ),
        0: Matrix.Identity(4),
        90: Matrix(
            (
                (1.0, 0.0, 0.0, 0.0),
                (0.0, 0.0, -1.0, 0.0),
                (0.0, 1.0, 0.0, 0.0),
                (0.0, 0.0, 0.0, 1.0),
            )
        ),
        180: Matrix(
            (
                (1.0, 0.0, 0.0, 0.0),
                (0.0, -1.0, 0.0, 0.0),
                (0.0, 0.0, -1.0, 0.0),
                (0.0, 0.0, 0.0, 1.0),
            )
        ),
    }[rotation_x_degrees]
    rotation_z = {
        -90: Matrix(
            (
                (0.0, 1.0, 0.0, 0.0),
                (-1.0, 0.0, 0.0, 0.0),
                (0.0, 0.0, 1.0, 0.0),
                (0.0, 0.0, 0.0, 1.0),
            )
        ),
        0: Matrix.Identity(4),
        90: Matrix(
            (
                (0.0, -1.0, 0.0, 0.0),
                (1.0, 0.0, 0.0, 0.0),
                (0.0, 0.0, 1.0, 0.0),
                (0.0, 0.0, 0.0, 1.0),
            )
        ),
        180: Matrix(
            (
                (-1.0, 0.0, 0.0, 0.0),
                (0.0, -1.0, 0.0, 0.0),
                (0.0, 0.0, 1.0, 0.0),
                (0.0, 0.0, 0.0, 1.0),
            )
        ),
    }[rotation_z_degrees]
    rotation = rotation_z @ rotation_x
    imported_set = set(imported)
    roots = [obj for obj in imported if obj.parent not in imported_set]
    for root in roots:
        root.matrix_world = rotation @ root.matrix_world
    bpy.context.view_layer.update()
    return {
        "operation": (
            "premultiply every imported root world transform by exact rigid "
            "quarter-turn matrices: import-space X first, then world-space Z"
        ),
        "xDegrees": rotation_x_degrees,
        "zDegrees": rotation_z_degrees,
        "matrix": matrix_json(rotation),
        "determinant3x3": rounded(rotation.to_3x3().determinant()),
        "rootObjectsTransformed": sorted(sanitize_name(obj.name) for obj in roots),
        "sourceGlbModified": False,
        "deformation": False,
    }


def world_vertices(obj: bpy.types.Object) -> list[Vector]:
    return [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]


def bounds_for_points(points: Iterable[Vector]) -> tuple[Vector, Vector]:
    points = list(points)
    if not points:
        zero = Vector((0.0, 0.0, 0.0))
        return zero.copy(), zero.copy()
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def bounds_json(minimum: Vector, maximum: Vector) -> dict[str, Any]:
    span = maximum - minimum
    return {
        "minimum": vector_json(minimum),
        "maximum": vector_json(maximum),
        "span": vector_json(span),
        "center": vector_json((minimum + maximum) * 0.5),
        "aabbVolume": rounded(max(0.0, span.x * span.y * span.z)),
    }


def triangle_area(a: Vector, b: Vector, c: Vector) -> float:
    return ((b - a).cross(c - a)).length * 0.5


def quantized_key(point: Vector) -> tuple[int, int, int]:
    return tuple(int(round(float(component) / WELD_EPSILON)) for component in point)


def edge_counts(
    polygons: Iterable[bpy.types.MeshPolygon],
    find_vertex: Any,
) -> tuple[Counter[tuple[int, int]], int]:
    incidence: Counter[tuple[int, int]] = Counter()
    collapsed = 0
    for polygon in polygons:
        vertices = list(polygon.vertices)
        for index, left in enumerate(vertices):
            right = vertices[(index + 1) % len(vertices)]
            left_root = find_vertex(left)
            right_root = find_vertex(right)
            if left_root == right_root:
                collapsed += 1
                continue
            incidence[tuple(sorted((left_root, right_root)))] += 1
    return incidence, collapsed


def topology_counts(
    incidence: Counter[tuple[int, int]],
    *,
    mapped_explicit_edge_count: int,
) -> dict[str, int]:
    boundary = sum(count == 1 for count in incidence.values())
    manifold = sum(count == 2 for count in incidence.values())
    non_manifold = sum(count > 2 for count in incidence.values())
    return {
        "uniquePolygonEdges": len(incidence),
        "boundaryEdges": boundary,
        "manifoldEdges": manifold,
        "nonManifoldEdges": non_manifold,
        "wireOrUnreferencedEdges": max(
            0, mapped_explicit_edge_count - len(incidence)
        ),
    }


def mapped_explicit_edges(
    edges: Iterable[bpy.types.MeshEdge],
    find_vertex: Any,
) -> tuple[int, int]:
    mapped: set[tuple[int, int]] = set()
    collapsed = 0
    for edge in edges:
        left = find_vertex(edge.vertices[0])
        right = find_vertex(edge.vertices[1])
        if left == right:
            collapsed += 1
            continue
        mapped.add(tuple(sorted((left, right))))
    return len(mapped), collapsed


def component_audit(
    obj: bpy.types.Object,
    points: list[Vector],
) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    mesh = obj.data
    vertex_count = len(mesh.vertices)
    indexed_connectivity = UnionFind(vertex_count)
    coincident_vertices = UnionFind(vertex_count)
    welded_connectivity = UnionFind(vertex_count)
    for polygon in mesh.polygons:
        vertices = list(polygon.vertices)
        for left, right in zip(vertices, vertices[1:] + vertices[:1]):
            indexed_connectivity.union(left, right)
            welded_connectivity.union(left, right)

    buckets: dict[tuple[int, int, int], list[int]] = defaultdict(list)
    for index, point in enumerate(points):
        buckets[quantized_key(point)].append(index)
    welded_vertex_groups = 0
    welded_vertex_reductions = 0
    for indices in buckets.values():
        if len(indices) < 2:
            continue
        anchor = indices[0]
        coincident_indices = [
            index
            for index in indices
            if (points[index] - points[anchor]).length <= WELD_EPSILON
        ]
        if len(coincident_indices) > 1:
            welded_vertex_groups += 1
            welded_vertex_reductions += len(coincident_indices) - 1
            for index in coincident_indices[1:]:
                coincident_vertices.union(anchor, index)
                welded_connectivity.union(anchor, index)

    raw_incidence, raw_collapsed = edge_counts(mesh.polygons, lambda index: index)
    welded_incidence, welded_collapsed = edge_counts(
        mesh.polygons, coincident_vertices.find
    )
    raw_explicit_edges, raw_collapsed_explicit = mapped_explicit_edges(
        mesh.edges, lambda index: index
    )
    welded_explicit_edges, welded_collapsed_explicit = mapped_explicit_edges(
        mesh.edges, coincident_vertices.find
    )

    mesh.calc_loop_triangles()
    triangle_areas_by_polygon: dict[int, float] = defaultdict(float)
    for triangle in mesh.loop_triangles:
        a, b, c = (points[index] for index in triangle.vertices)
        triangle_areas_by_polygon[triangle.polygon_index] += triangle_area(a, b, c)

    vertices_by_component: dict[int, list[int]] = defaultdict(list)
    for index in range(vertex_count):
        vertices_by_component[welded_connectivity.find(index)].append(index)
    polygons_by_component: dict[int, list[int]] = defaultdict(list)
    for polygon in mesh.polygons:
        if polygon.vertices:
            polygons_by_component[
                welded_connectivity.find(polygon.vertices[0])
            ].append(polygon.index)

    edge_topology_by_component: dict[int, Counter[str]] = defaultdict(Counter)
    for (left, _right), incidence in welded_incidence.items():
        component = welded_connectivity.find(left)
        if incidence == 1:
            edge_topology_by_component[component]["boundaryEdges"] += 1
        elif incidence == 2:
            edge_topology_by_component[component]["manifoldEdges"] += 1
        else:
            edge_topology_by_component[component]["nonManifoldEdges"] += 1

    components: list[dict[str, Any]] = []
    sorted_components = sorted(
        vertices_by_component.items(),
        key=lambda item: (
            -len(polygons_by_component.get(item[0], [])),
            -len(item[1]),
            item[0],
        ),
    )
    for component_index, (root, vertex_indices) in enumerate(sorted_components):
        polygon_indices = polygons_by_component.get(root, [])
        minimum, maximum = bounds_for_points(points[index] for index in vertex_indices)
        topology = edge_topology_by_component[root]
        components.append(
            {
                "componentIndex": component_index,
                "vertexCount": len(vertex_indices),
                "weldedVertexCount": len(
                    {
                        coincident_vertices.find(index)
                        for index in vertex_indices
                    }
                ),
                "faceCount": len(polygon_indices),
                "triangleCount": sum(
                    len(mesh.polygons[index].loop_indices) - 2
                    for index in polygon_indices
                ),
                "surfaceArea": rounded(
                    sum(triangle_areas_by_polygon[index] for index in polygon_indices)
                ),
                "boundsWorldDcc": bounds_json(minimum, maximum),
                "boundaryEdges": topology["boundaryEdges"],
                "manifoldEdges": topology["manifoldEdges"],
                "nonManifoldEdges": topology["nonManifoldEdges"],
            }
        )

    raw_component_count = len(
        {indexed_connectivity.find(index) for index in range(vertex_count)}
    )
    welded_component_count = len(
        {welded_connectivity.find(index) for index in range(vertex_count)}
    )
    summary = {
        "weldEpsilonWorldUnits": WELD_EPSILON,
        "rawIndexedComponentCount": raw_component_count,
        "weldedComponentCount": welded_component_count,
        "coincidentVertexGroupCount": welded_vertex_groups,
        "weldedVertexReduction": welded_vertex_reductions,
        "rawEdgeTopology": {
            **topology_counts(
                raw_incidence, mapped_explicit_edge_count=raw_explicit_edges
            ),
            "collapsedPolygonEdges": raw_collapsed,
            "collapsedExplicitEdges": raw_collapsed_explicit,
        },
        "weldedEdgeTopology": {
            **topology_counts(
                welded_incidence,
                mapped_explicit_edge_count=welded_explicit_edges,
            ),
            "collapsedPolygonEdges": welded_collapsed,
            "collapsedExplicitEdges": welded_collapsed_explicit,
        },
    }
    return summary, components


def mesh_object_audit(obj: bpy.types.Object) -> dict[str, Any]:
    mesh = obj.data
    points = world_vertices(obj)
    minimum, maximum = bounds_for_points(points)
    topology, components = component_audit(obj, points)
    mesh.calc_loop_triangles()
    surface_area = 0.0
    for triangle in mesh.loop_triangles:
        a, b, c = (points[index] for index in triangle.vertices)
        surface_area += triangle_area(a, b, c)
    origin = obj.matrix_world.translation
    center = (minimum + maximum) * 0.5
    material_names = [
        sanitize_name(slot.material.name) if slot.material else ""
        for slot in obj.material_slots
    ]
    armature_modifiers = [
        modifier
        for modifier in obj.modifiers
        if modifier.type == "ARMATURE"
    ]
    return {
        "name": sanitize_name(obj.name),
        "dataName": sanitize_name(mesh.name),
        "counts": {
            "vertices": len(mesh.vertices),
            "edges": len(mesh.edges),
            "faces": len(mesh.polygons),
            "triangles": len(mesh.loop_triangles),
            "materialSlots": len(obj.material_slots),
            "uvLayers": len(mesh.uv_layers),
            "colorAttributes": len(mesh.color_attributes),
            "shapeKeys": (
                max(0, len(mesh.shape_keys.key_blocks) - 1)
                if mesh.shape_keys
                else 0
            ),
            "vertexGroups": len(obj.vertex_groups),
            "armatureModifiers": len(armature_modifiers),
        },
        "surfaceAreaWorld": rounded(surface_area),
        "boundsWorldDcc": bounds_json(minimum, maximum),
        "pivot": {
            "originWorld": vector_json(origin),
            "boundsCenterWorld": vector_json(center),
            "originToBoundsCenter": vector_json(center - origin),
            "originInsideAabb": all(
                minimum[axis] - WELD_EPSILON
                <= origin[axis]
                <= maximum[axis] + WELD_EPSILON
                for axis in range(3)
            ),
        },
        "materials": material_names,
        "armatures": [
            sanitize_name(modifier.object.name)
            for modifier in armature_modifiers
            if modifier.object
        ],
        "topology": topology,
        "components": components,
    }


def object_hierarchy_audit(obj: bpy.types.Object) -> dict[str, Any]:
    location, rotation, scale = obj.matrix_world.decompose()
    return {
        "name": sanitize_name(obj.name),
        "type": obj.type,
        "dataName": sanitize_name(obj.data.name) if obj.data else "",
        "parent": sanitize_name(obj.parent.name) if obj.parent else None,
        "children": sorted(sanitize_name(child.name) for child in obj.children),
        "worldTransform": {
            "matrix": matrix_json(obj.matrix_world),
            "location": vector_json(location),
            "rotationQuaternionWxyz": vector_json(
                (rotation.w, rotation.x, rotation.y, rotation.z)
            ),
            "scale": vector_json(scale),
        },
    }


def material_audits(
    imported: list[bpy.types.Object],
) -> list[dict[str, Any]]:
    used: dict[bpy.types.Material, set[str]] = defaultdict(set)
    for obj in imported:
        if obj.type != "MESH":
            continue
        for slot in obj.material_slots:
            if slot.material:
                used[slot.material].add(sanitize_name(obj.name))
    results = []
    for material in sorted(used, key=lambda item: sanitize_name(item.name)):
        role = material.get("nilbotsRole")
        results.append(
            {
                "name": sanitize_name(material.name),
                "usedBy": sorted(used[material]),
                "useNodes": bool(material.use_nodes),
                "nodeCount": len(material.node_tree.nodes)
                if material.use_nodes and material.node_tree
                else 0,
                "blendMethod": sanitize_name(
                    getattr(material, "surface_render_method", "")
                ),
                "nilbotsRole": sanitize_name(role) if role is not None else None,
            }
        )
    return results


def imported_images(
    imported: list[bpy.types.Object],
) -> set[bpy.types.Image]:
    images: set[bpy.types.Image] = set()
    for obj in imported:
        if obj.type != "MESH":
            continue
        for slot in obj.material_slots:
            material = slot.material
            if not material or not material.node_tree:
                continue
            for node in material.node_tree.nodes:
                image = getattr(node, "image", None)
                if image is not None:
                    images.add(image)
    return images


def image_audits(imported: list[bpy.types.Object]) -> list[dict[str, Any]]:
    results = []
    for image in sorted(
        imported_images(imported), key=lambda item: sanitize_name(item.name)
    ):
        results.append(
            {
                "name": sanitize_name(image.name),
                "width": int(image.size[0]),
                "height": int(image.size[1]),
                "channels": int(image.channels),
                "source": sanitize_name(image.source),
                "fileFormat": sanitize_name(image.file_format),
                "packed": image.packed_file is not None,
                "colorSpace": sanitize_name(image.colorspace_settings.name),
            }
        )
    return results


def action_audits() -> list[dict[str, Any]]:
    results = []
    for action in sorted(bpy.data.actions, key=lambda item: sanitize_name(item.name)):
        frame_range = getattr(action, "frame_range", (0.0, 0.0))
        fcurves = getattr(action, "fcurves", ())
        slots = getattr(action, "slots", ())
        layers = getattr(action, "layers", ())
        results.append(
            {
                "name": sanitize_name(action.name),
                "frameRange": vector_json(frame_range),
                "fCurveCount": len(fcurves),
                "slotCount": len(slots),
                "layerCount": len(layers),
            }
        )
    return results


def armature_audits(imported: list[bpy.types.Object]) -> list[dict[str, Any]]:
    results = []
    for obj in imported:
        if obj.type != "ARMATURE":
            continue
        bones = []
        for bone in obj.data.bones:
            bones.append(
                {
                    "name": sanitize_name(bone.name),
                    "parent": sanitize_name(bone.parent.name) if bone.parent else None,
                    "headLocal": vector_json(bone.head_local),
                    "tailLocal": vector_json(bone.tail_local),
                }
            )
        results.append(
            {
                "name": sanitize_name(obj.name),
                "boneCount": len(bones),
                "bones": bones,
            }
        )
    return sorted(results, key=lambda item: item["name"])


def pairwise_aabb_overlaps(
    mesh_audits: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    overlaps = []
    for left_index, left in enumerate(mesh_audits):
        left_bounds = left["boundsWorldDcc"]
        for right in mesh_audits[left_index + 1 :]:
            right_bounds = right["boundsWorldDcc"]
            minimum = [
                max(left_bounds["minimum"][axis], right_bounds["minimum"][axis])
                for axis in range(3)
            ]
            maximum = [
                min(left_bounds["maximum"][axis], right_bounds["maximum"][axis])
                for axis in range(3)
            ]
            span = [maximum[axis] - minimum[axis] for axis in range(3)]
            if all(value > WELD_EPSILON for value in span):
                overlaps.append(
                    {
                        "left": left["name"],
                        "right": right["name"],
                        "intersectionSpan": vector_json(span),
                        "intersectionAabbVolume": rounded(
                            span[0] * span[1] * span[2]
                        ),
                        "classification": "broad-phase-aabb-only",
                    }
                )
    return overlaps


def global_geometry_audit(meshes: list[bpy.types.Object]) -> dict[str, Any]:
    points = [point for obj in meshes for point in world_vertices(obj)]
    minimum, maximum = bounds_for_points(points)
    span = maximum - minimum
    planform_span = max(span.x, span.y)
    return {
        "boundsWorldDcc": bounds_json(minimum, maximum),
        "planformSpan": rounded(planform_span),
        "heightToPlanform": rounded(span.z / planform_span)
        if planform_span > 0.0
        else None,
        "floorOffset": rounded(minimum.z),
        "dominantPlanformAxis": "+/-X" if span.x >= span.y else "+/-Y",
        "orientation": {
            "sourceGlTfUp": "+Y",
            "blenderImportUp": "+Z",
            "expectedNilbotsFacingInBlender": "+X",
            "expectedNilbotsUpInBlender": "+Z",
            "semanticFacingInferred": False,
            "note": (
                "Geometry dimensions cannot prove semantic facing; compare "
                "fixed views with the approved source."
            ),
        },
    }


def normalize_for_render(
    imported: list[bpy.types.Object],
    meshes: list[bpy.types.Object],
) -> dict[str, Any]:
    before_minimum, before_maximum = bounds_for_points(
        point for obj in meshes for point in world_vertices(obj)
    )
    span = before_maximum - before_minimum
    planform_span = max(span.x, span.y)
    if planform_span <= 0.0:
        raise ValueError("Cannot normalize a model with zero planform span")
    scale = NORMALIZED_PLANFORM_SPAN / planform_span
    center = (before_minimum + before_maximum) * 0.5
    transform = (
        Matrix.Translation(
            Vector((-center.x * scale, -center.y * scale, -before_minimum.z * scale))
        )
        @ Matrix.Scale(scale, 4)
    )
    imported_set = set(imported)
    roots = [obj for obj in imported if obj.parent not in imported_set]
    for root in roots:
        root.matrix_world = transform @ root.matrix_world
    bpy.context.view_layer.update()
    after_minimum, after_maximum = bounds_for_points(
        point for obj in meshes for point in world_vertices(obj)
    )
    return {
        "operation": (
            "one uniform transform: center the source world AABB in the "
            "horizontal plane, put its minimum Z on the floor, and scale its "
            "maximum X/Y span to exactly one audit unit"
        ),
        "uniformScale": rounded(scale),
        "targetPlanformSpan": NORMALIZED_PLANFORM_SPAN,
        "sourceBoundsWorldDcc": bounds_json(before_minimum, before_maximum),
        "normalizedBoundsWorldDcc": bounds_json(after_minimum, after_maximum),
        "rootObjectsTransformed": sorted(sanitize_name(obj.name) for obj in roots),
        "perModelCameraFraming": False,
    }


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
        node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"
    )
    rgba = hex_linear_rgba(color)
    principled.inputs["Base Color"].default_value = rgba
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    material.diffuse_color = rgba
    return material


def configure_neutral_rig(
    imported: list[bpy.types.Object],
) -> tuple[bpy.types.Object, bpy.types.Object]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = RENDER_SIZE
    scene.render.resolution_y = RENDER_SIZE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 15
    scene.render.film_transparent = True
    scene.render.use_file_extension = True
    # Eevee's temporal accumulation starts from a process-randomized jitter.
    # One fixed render sample is less anti-aliased but byte-stable across
    # independent Blender processes, which matters more for an audit baseline.
    scene.eevee.taa_render_samples = 1
    scene.eevee.taa_samples = 1
    scene.eevee.shadow_ray_count = 1
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -1.0

    for obj in imported:
        if obj.type in ("LIGHT", "CAMERA"):
            obj.hide_render = True

    world = bpy.data.worlds.new("TOPOLOGY_AUDIT.World")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = hex_linear_rgba("#0b1020")
    background.inputs["Strength"].default_value = 0.45
    scene.world = world

    floor_material = simple_material(
        "TOPOLOGY_AUDIT.FloorMaterial",
        "#252c36",
        metallic=0.42,
        roughness=0.72,
    )
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, -0.012))
    floor = bpy.context.object
    floor.name = "TOPOLOGY_AUDIT.Floor"
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
        light.name = f"TOPOLOGY_AUDIT.{name}"
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.data.color = color
        look_at(light, Vector((0, 0, 0.13)))

    camera_data = bpy.data.cameras.new("TOPOLOGY_AUDIT.Camera")
    camera = bpy.data.objects.new("TOPOLOGY_AUDIT.Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    return floor, camera


def render_view(
    output_path: Path,
    floor: bpy.types.Object,
    camera: bpy.types.Object,
    view: str,
) -> dict[str, Any]:
    scene = bpy.context.scene
    floor.hide_render = view != "gameplay58"
    # Opaque neutral backgrounds avoid undefined RGB values under alpha zero,
    # making byte-for-byte reruns possible while preserving fixed silhouettes.
    scene.render.film_transparent = False
    target = Vector((0.0, 0.0, 0.15))
    if view == "top":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.24
        camera.location = (0.0, 0.0, 3.0)
        target = Vector((0.0, 0.0, 0.0))
    elif view == "side":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.24
        camera.location = (0.0, -3.0, 0.18)
        target = Vector((0.0, 0.0, 0.18))
    elif view == "front":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.24
        camera.location = (3.0, 0.0, 0.18)
        target = Vector((0.0, 0.0, 0.18))
    elif view == "rear":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.24
        camera.location = (-3.0, 0.0, 0.18)
        target = Vector((0.0, 0.0, 0.18))
    elif view == "frontThreeQuarter":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.34
        camera.location = (2.4, -2.4, 1.7)
    elif view == "rearThreeQuarter":
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.34
        # A fixed half-degree asymmetry avoids exact diagonal depth ties on
        # mirrored/coplanar generated parts while remaining a rear 3/4 view.
        camera.location = (-2.42, 2.38, 1.7)
    elif view == "gameplay58":
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
        raise ValueError(f"Unknown audit view: {view}")
    look_at(camera, target)
    scene.render.filepath = str(output_path)
    bpy.ops.render.render(write_still=True)
    canonicalize_png(output_path)
    return {
        "file": output_path.name,
        "sha256": sha256(output_path),
        "bytes": output_path.stat().st_size,
        "cameraType": camera.data.type,
        "cameraLocation": vector_json(camera.location),
        "cameraTarget": vector_json(target),
        "orthoScale": rounded(camera.data.ortho_scale)
        if camera.data.type == "ORTHO"
        else None,
        "fieldOfViewDegrees": rounded(math.degrees(camera.data.angle))
        if camera.data.type == "PERSP"
        else None,
        "transparentBackground": scene.render.film_transparent,
    }


def render_top_silhouette(
    output_path: Path,
    floor: bpy.types.Object,
    camera: bpy.types.Object,
) -> dict[str, Any]:
    scene = bpy.context.scene
    floor.hide_render = True
    scene.render.film_transparent = True
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.24
    camera.location = (0.0, 0.0, 3.0)
    target = Vector((0.0, 0.0, 0.0))
    look_at(camera, target)

    silhouette_material = simple_material(
        "TOPOLOGY_AUDIT.SilhouetteMaterial",
        "#ffffff",
        metallic=0.0,
        roughness=1.0,
    )
    view_layer = bpy.context.view_layer
    previous_override = view_layer.material_override
    view_layer.material_override = silhouette_material
    try:
        scene.render.filepath = str(output_path)
        bpy.ops.render.render(write_still=True)
    finally:
        view_layer.material_override = previous_override
    canonicalize_png(output_path)
    return {
        "file": output_path.name,
        "sha256": sha256(output_path),
        "bytes": output_path.stat().st_size,
        "cameraType": camera.data.type,
        "cameraLocation": vector_json(camera.location),
        "cameraTarget": vector_json(target),
        "orthoScale": rounded(camera.data.ortho_scale),
        "transparentBackground": True,
        "materialOverride": "opaque neutral silhouette",
    }


def render_audit_views(
    audit_dir: Path,
    imported: list[bpy.types.Object],
) -> dict[str, Any]:
    floor, camera = configure_neutral_rig(imported)
    renders = {}
    for view, filename in VIEW_FILENAMES.items():
        renders[view] = render_view(audit_dir / filename, floor, camera, view)
    silhouette_path = audit_dir / "top-silhouette.png"
    silhouette = render_top_silhouette(silhouette_path, floor, camera)
    return {
        "rig": {
            "id": "nilbots-neutral-topology-audit-v1",
            "renderEngine": "BLENDER_EEVEE",
            "eeveeRenderSamples": 1,
            "castShadows": True,
            "resolution": [RENDER_SIZE, RENDER_SIZE],
            "pngCanonicalization": (
                "strip nondeterministic metadata, decode all source filters, "
                "quantize ordinary RGB to six bits and dark RGB to four bits "
                "without changing alpha, and encode one fixed-filter zlib-9 "
                "IDAT"
            ),
            "colorManagement": {
                "viewTransform": "AgX",
                "look": "AgX - Medium High Contrast",
                "exposure": -1.0,
            },
            "gameplayPitchDegreesAboveGround": 58.0,
            "fixedCameraFraming": True,
        },
        "views": renders,
        "topSilhouette": silhouette,
    }


def build_audit(
    input_path: Path,
    audit_dir: Path,
    source_document: dict[str, Any],
    canonical_top: Path | None,
    rotation_x_degrees: int,
    rotation_z_degrees: int,
) -> dict[str, Any]:
    clear_scene()
    imported = import_glb(input_path)
    orientation_adjustment = apply_rigid_orientation(
        imported,
        rotation_x_degrees,
        rotation_z_degrees,
    )
    meshes = [obj for obj in imported if obj.type == "MESH"]
    mesh_audits = [mesh_object_audit(obj) for obj in meshes]
    object_audits = [object_hierarchy_audit(obj) for obj in imported]
    global_geometry = global_geometry_audit(meshes)
    total_components = sum(
        mesh["topology"]["weldedComponentCount"] for mesh in mesh_audits
    )
    total_raw_components = sum(
        mesh["topology"]["rawIndexedComponentCount"] for mesh in mesh_audits
    )
    imported_counts = Counter(obj.type for obj in imported)

    audit: dict[str, Any] = {
        "schema": AUDIT_SCHEMA,
        "toolVersion": TOOL_VERSION,
        "blender": {
            "version": bpy.app.version_string,
            "expectedMajorMinor": list(EXPECTED_BLENDER),
            "matchesExpectedMajorMinor": bpy.app.version[:2] == EXPECTED_BLENDER,
            "background": bpy.app.background,
        },
        "input": {
            "fileName": sanitize_name(input_path.name),
            "sha256": sha256(input_path),
            "bytes": input_path.stat().st_size,
        },
        "sourceDocument": source_document_summary(source_document),
        "importedScene": {
            "counts": {
                "objects": len(imported),
                "meshObjects": imported_counts["MESH"],
                "armatureObjects": imported_counts["ARMATURE"],
                "cameraObjects": imported_counts["CAMERA"],
                "lightObjects": imported_counts["LIGHT"],
                "emptyObjects": imported_counts["EMPTY"],
                "materials": len(material_audits(imported)),
                "images": len(imported_images(imported)),
                "actions": len(bpy.data.actions),
                "weldedConnectedComponents": total_components,
                "rawIndexedConnectedComponents": total_raw_components,
            },
            "objects": object_audits,
            "meshes": mesh_audits,
            "materials": material_audits(imported),
            "images": image_audits(imported),
            "actions": action_audits(),
            "armatures": armature_audits(imported),
            "separation": {
                "meshObjectNames": sorted(mesh["name"] for mesh in mesh_audits),
                "meshObjectCount": len(mesh_audits),
                "rawIndexedConnectedComponentCount": total_raw_components,
                "weldedConnectedComponentCount": total_components,
                "note": (
                    "Coincident vertices are welded only for connectivity "
                    "measurement; the imported model is not modified."
                ),
            },
            "overlap": {
                "method": "pairwise world-space mesh AABB broad phase",
                "limitations": (
                    "A positive result does not prove surface intersection; "
                    "a negative result does prove disjoint AABBs."
                ),
                "pairs": pairwise_aabb_overlaps(mesh_audits),
            },
        },
        "globalGeometry": global_geometry,
        "orientationAdjustment": orientation_adjustment,
    }
    audit["renderNormalization"] = normalize_for_render(imported, meshes)
    audit["renderEvidence"] = render_audit_views(audit_dir, imported)
    if canonical_top is not None:
        silhouette_file = audit["renderEvidence"]["topSilhouette"]["file"]
        audit["topSilhouetteComparison"] = compare_top_silhouettes(
            audit_dir / silhouette_file,
            canonical_top,
            audit_dir,
        )
    else:
        audit["topSilhouetteComparison"] = None
    return audit


def main() -> int:
    args = parse_args()
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise ValueError(
            "This audit is pinned to Blender "
            f"{EXPECTED_BLENDER[0]}.{EXPECTED_BLENDER[1]}.x"
        )
    input_path, audit_dir, canonical_top = validate_paths(
        args.input,
        args.audit_dir,
        args.canonical_top,
    )
    source_document = read_glb_document(input_path)
    if canonical_top is not None:
        # Reject an opaque, empty, or unsupported authority before creating
        # an output directory or spending local render time.
        alpha_mask(canonical_top)
    audit_dir.mkdir(parents=True, exist_ok=False)
    audit = build_audit(
        input_path,
        audit_dir,
        source_document,
        canonical_top,
        args.rigid_rotation_x_degrees,
        args.rigid_rotation_z_degrees,
    )
    audit_path = audit_dir / "audit.json"
    audit_path.write_text(
        json.dumps(audit, indent=2, sort_keys=True, ensure_ascii=True) + "\n",
        encoding="utf-8",
    )
    print(
        json.dumps(
            {
                "audit": audit_path.name,
                "auditDir": audit_dir.name,
                "inputSha256": audit["input"]["sha256"],
                "meshObjects": audit["importedScene"]["counts"]["meshObjects"],
                "weldedConnectedComponents": audit["importedScene"]["counts"][
                    "weldedConnectedComponents"
                ],
                "renders": sorted(
                    [*VIEW_FILENAMES.values(), "top-silhouette.png"]
                ),
                "silhouetteComparison": (
                    audit["topSilhouetteComparison"] is not None
                ),
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    try:
        main()
    except SystemExit as error:
        # Blender otherwise reports Python failures while still exiting zero.
        code = error.code if isinstance(error.code, int) else 1
        if code:
            sys.stderr.flush()
            os._exit(code)
    except Exception as error:
        print(
            f"Topology audit failed: {sanitize_name(error, fallback='unknown error')}",
            file=sys.stderr,
            flush=True,
        )
        os._exit(2)
