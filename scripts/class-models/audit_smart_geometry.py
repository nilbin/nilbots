#!/usr/bin/env python3
"""Run the deterministic offline Blender audit for one generated GLB.

The wrapper resolves the project's pinned Blender 5.2 toolchain, invokes the
Blender-side inspector without network access, and projects its detailed audit
into a compact geometry report. It never calls Meshy or any other provider and
never modifies the input model.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import subprocess
import sys
from pathlib import Path
from typing import Any


REPORT_SCHEMA = "nilbots-smart-geometry-report-1"
TOOL_VERSION = "1.0.0"
EXPECTED_AUDIT_SCHEMA = "nilbots-smart-geometry-audit-2"
EXPECTED_BLENDER = (5, 2)
PINNED_BLENDER_RELATIVE_CANDIDATES = (
    Path("var/toolchains/Blender-5.2.0.app/Contents/MacOS/Blender"),
    Path("var/toolchains/blender-5.2.0/blender"),
)


def positive_float(value: str) -> float:
    result = float(value)
    if not math.isfinite(result) or result <= 0.0:
        raise argparse.ArgumentTypeError("value must be finite and greater than zero")
    return result


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Audit one local GLB with pinned Blender 5.2. Emits objective "
            "geometry, fixed-view, top-silhouette, and game-scale evidence; "
            "never performs provider or network calls."
        )
    )
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument(
        "--audit-dir",
        type=Path,
        required=True,
        help=(
            "New output directory below a sandbox/ or var/ path component; "
            "one component beneath that root must contain 'audit'."
        ),
    )
    parser.add_argument(
        "--canonical-top",
        type=Path,
        help=(
            "Optional transparent RGBA PNG top-view authority. The audit "
            "reports all four quarter-turn comparisons and selects none."
        ),
    )
    parser.add_argument(
        "--authored-span-tiles",
        type=positive_float,
        default=1.0,
        help=(
            "Intended authored maximum planform span in gameplay tiles; "
            "default: 1.0. This records scale evidence and does not edit GLB."
        ),
    )
    parser.add_argument(
        "--runtime-scale",
        type=positive_float,
        default=1.0,
        help=(
            "Additional renderer/look multiplier; default: 1.0. This records "
            "the composed live footprint and does not edit GLB."
        ),
    )
    parser.add_argument(
        "--pixels-per-tile",
        type=positive_float,
        default=64.0,
        help=(
            "Gameplay-review sampling density used only for nominal pixel "
            "footprint evidence; default: 64."
        ),
    )
    parser.add_argument(
        "--blender",
        type=Path,
        help=(
            "Explicit absolute Blender executable. It must still report "
            "version 5.2.x. By default the repository's pinned toolchain is "
            "resolved through the shared Git directory."
        ),
    )
    parser.add_argument(
        "--rigid-rotation-x-degrees",
        type=int,
        choices=(-90, 0, 90, 180),
        default=0,
        help=(
            "Recorded rigid quarter-turn around Blender import-space X before "
            "measurement/rendering; default: 0. The source GLB is unchanged."
        ),
    )
    parser.add_argument(
        "--rigid-rotation-z-degrees",
        type=int,
        choices=(-90, 0, 90, 180),
        default=0,
        help=(
            "Recorded rigid quarter-turn around Blender import-space Z after "
            "the X turn; default: 0. Use only after inspecting facing."
        ),
    )
    return parser.parse_args(argv)


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def shared_worktree_root(repo_root: Path) -> Path:
    completed = subprocess.run(
        ["git", "rev-parse", "--git-common-dir"],
        cwd=repo_root,
        check=True,
        capture_output=True,
        text=True,
    )
    common_dir = Path(completed.stdout.strip())
    if not common_dir.is_absolute():
        common_dir = (repo_root / common_dir).resolve()
    else:
        common_dir = common_dir.resolve()
    if common_dir.name != ".git":
        raise ValueError(
            "Expected git common directory to end in .git; "
            f"got {common_dir}"
        )
    return common_dir.parent


def resolve_blender(explicit: Path | None, repo_root: Path) -> Path:
    if explicit is not None:
        if not explicit.is_absolute():
            raise ValueError("--blender must be an absolute path")
        candidates = (explicit.resolve(),)
    else:
        shared_root = shared_worktree_root(repo_root)
        candidates = tuple(
            (shared_root / relative).resolve()
            for relative in PINNED_BLENDER_RELATIVE_CANDIDATES
        )
    blender = next(
        (
            candidate
            for candidate in candidates
            if candidate.is_file() and os.access(candidate, os.X_OK)
        ),
        None,
    )
    if blender is None:
        rendered = ", ".join(str(candidate) for candidate in candidates)
        raise ValueError(
            "Pinned Blender 5.2 executable was not found or executable. "
            f"Checked: {rendered}"
        )
    return blender


def resolve_input(path: Path, *, repo_root: Path, suffix: str) -> Path:
    resolved = path if path.is_absolute() else repo_root / path
    resolved = resolved.resolve(strict=True)
    if not resolved.is_file() or resolved.suffix.lower() != suffix:
        raise ValueError(f"Expected an existing {suffix} file: {resolved}")
    return resolved


def resolve_new_audit_dir(path: Path, *, repo_root: Path) -> Path:
    resolved = path if path.is_absolute() else repo_root / path
    resolved = resolved.resolve(strict=False)
    if resolved.exists():
        raise ValueError(
            f"Audit output already exists; outputs are never overwritten: {resolved}"
        )
    return resolved


def sum_mesh_count(audit: dict[str, Any], key: str) -> int:
    return sum(
        int(mesh.get("counts", {}).get(key, 0))
        for mesh in audit["importedScene"]["meshes"]
    )


def compact_components(audit: dict[str, Any]) -> dict[str, Any]:
    components = []
    for mesh in audit["importedScene"]["meshes"]:
        mesh_name = mesh["name"]
        for component in mesh["components"]:
            bounds = component["boundsWorldDcc"]
            components.append(
                {
                    "mesh": mesh_name,
                    "componentIndex": component["componentIndex"],
                    "triangles": component["triangleCount"],
                    "vertices": component["vertexCount"],
                    "surfaceAreaWorld": component["surfaceArea"],
                    "boundsCenterWorldDcc": bounds["center"],
                    "boundsSpanWorldDcc": bounds["span"],
                    "boundaryEdges": component["boundaryEdges"],
                    "nonManifoldEdges": component["nonManifoldEdges"],
                }
            )
    components.sort(
        key=lambda component: (
            -component["triangles"],
            -component["vertices"],
            component["mesh"],
            component["componentIndex"],
        )
    )
    total_triangles = sum(component["triangles"] for component in components)
    triangle_shares = [
        component["triangles"] / total_triangles if total_triangles else 0.0
        for component in components
    ]
    return {
        "count": len(components),
        "largestTriangleShare": round(triangle_shares[0], 9)
        if triangle_shares
        else None,
        "componentsAtLeastOnePercentOfTriangles": sum(
            share >= 0.01 for share in triangle_shares
        ),
        "topEightByTriangles": components[:8],
        "note": (
            "Connected components are geometric observations, not semantic "
            "parts. Inspect boundaries before assigning pivots or hierarchy."
        ),
    }


def compact_renders(audit: dict[str, Any]) -> dict[str, Any]:
    render_evidence = audit["renderEvidence"]
    views = {
        name: {
            "file": view["file"],
            "sha256": view["sha256"],
            "bytes": view["bytes"],
            "cameraType": view["cameraType"],
            "cameraLocation": view["cameraLocation"],
            "cameraTarget": view["cameraTarget"],
            "orthoScale": view["orthoScale"],
            "fieldOfViewDegrees": view["fieldOfViewDegrees"],
        }
        for name, view in sorted(render_evidence["views"].items())
    }
    silhouette = render_evidence["topSilhouette"]
    return {
        "rig": render_evidence["rig"],
        "views": views,
        "topSilhouette": {
            "file": silhouette["file"],
            "sha256": silhouette["sha256"],
            "bytes": silhouette["bytes"],
            "orthoScale": silhouette["orthoScale"],
        },
    }


def scaled_vector(values: list[float], factor: float) -> list[float]:
    return [round(float(value) * factor, 9) for value in values]


def game_scale_inputs(
    audit: dict[str, Any],
    *,
    authored_span_tiles: float,
    runtime_scale: float,
    pixels_per_tile: float,
) -> dict[str, Any]:
    normalization = audit["renderNormalization"]["normalizedBoundsWorldDcc"]
    composed_scale = authored_span_tiles * runtime_scale
    return {
        "authoredMaximumPlanformSpanTiles": authored_span_tiles,
        "runtimeScale": runtime_scale,
        "liveMaximumPlanformSpanTiles": round(composed_scale, 9),
        "nominalPixelsPerTile": pixels_per_tile,
        "nominalLiveMaximumPlanformSpanPixels": round(
            composed_scale * pixels_per_tile,
            6,
        ),
        "liveBoundsBlenderTiles": {
            "minimum": scaled_vector(normalization["minimum"], composed_scale),
            "maximum": scaled_vector(normalization["maximum"], composed_scale),
            "span": scaled_vector(normalization["span"], composed_scale),
        },
        "coordinateNote": (
            "Blender import space is +Z up. The audit normalizes the maximum "
            "horizontal X/Y span to one before multiplying authored span and "
            "runtime scale. This is footprint evidence only; it does not "
            "prove semantic +X facing or wall clearance at every yaw."
        ),
    }


def build_compact_report(
    audit: dict[str, Any],
    *,
    authored_span_tiles: float,
    runtime_scale: float,
    pixels_per_tile: float,
) -> dict[str, Any]:
    if audit.get("schema") != EXPECTED_AUDIT_SCHEMA:
        raise ValueError(
            "Unexpected Blender audit schema: "
            f"{audit.get('schema')!r}; expected {EXPECTED_AUDIT_SCHEMA!r}"
        )
    blender = audit["blender"]
    if blender.get("expectedMajorMinor") != list(EXPECTED_BLENDER):
        raise ValueError("Blender audit does not declare the pinned 5.2 version")
    if not blender.get("matchesExpectedMajorMinor"):
        raise ValueError(
            f"Blender audit ran under an unpinned version: {blender.get('version')}"
        )

    counts = audit["importedScene"]["counts"]
    return {
        "schema": REPORT_SCHEMA,
        "toolVersion": TOOL_VERSION,
        "auditSchema": audit["schema"],
        "auditToolVersion": audit["toolVersion"],
        "input": audit["input"],
        "blender": blender,
        "sourceDocument": {
            "assetVersion": audit["sourceDocument"]["assetVersion"],
            "generator": audit["sourceDocument"]["generator"],
            "counts": audit["sourceDocument"]["counts"],
            "extensionsUsed": audit["sourceDocument"]["extensionsUsed"],
            "extensionsRequired": audit["sourceDocument"]["extensionsRequired"],
        },
        "geometry": {
            "meshObjects": counts["meshObjects"],
            "vertices": sum_mesh_count(audit, "vertices"),
            "faces": sum_mesh_count(audit, "faces"),
            "triangles": sum_mesh_count(audit, "triangles"),
            "materialSlots": sum_mesh_count(audit, "materialSlots"),
            "materials": counts["materials"],
            "images": counts["images"],
            "rawIndexedConnectedComponents": counts[
                "rawIndexedConnectedComponents"
            ],
            "weldedConnectedComponents": counts["weldedConnectedComponents"],
            "sourceBoundsWorldDcc": audit["globalGeometry"]["boundsWorldDcc"],
            "sourcePlanformSpan": audit["globalGeometry"]["planformSpan"],
            "sourceHeightToPlanform": audit["globalGeometry"][
                "heightToPlanform"
            ],
            "sourceFloorOffset": audit["globalGeometry"]["floorOffset"],
            "orientation": audit["globalGeometry"]["orientation"],
            "orientationAdjustment": audit["orientationAdjustment"],
            "renderNormalization": audit["renderNormalization"],
            "components": compact_components(audit),
        },
        "renders": compact_renders(audit),
        "acceptanceInputs": {
            "topSilhouette": audit["topSilhouetteComparison"],
            "gameScale": game_scale_inputs(
                audit,
                authored_span_tiles=authored_span_tiles,
                runtime_scale=runtime_scale,
                pixels_per_tile=pixels_per_tile,
            ),
            "passFail": None,
            "note": (
                "No acceptance verdict is computed. Review semantic facing, "
                "named identity anchors, forbidden cues, side/depth class, "
                "part boundaries, and the actual gameplay replay separately."
            ),
        },
    }


def write_json_atomic(path: Path, value: dict[str, Any]) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(
        json.dumps(value, indent=2, sort_keys=True, ensure_ascii=True) + "\n",
        encoding="utf-8",
    )
    temporary.replace(path)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    repo_root = repository_root()
    input_path = resolve_input(args.input, repo_root=repo_root, suffix=".glb")
    audit_dir = resolve_new_audit_dir(args.audit_dir, repo_root=repo_root)
    canonical_top = (
        resolve_input(args.canonical_top, repo_root=repo_root, suffix=".png")
        if args.canonical_top is not None
        else None
    )
    blender = resolve_blender(args.blender, repo_root)
    blender_script = (
        repo_root / "scripts/class-models/audit_smart_geometry_blender.py"
    ).resolve(strict=True)

    command = [
        str(blender),
        "--background",
        "--factory-startup",
        "--python",
        str(blender_script),
        "--",
        "--input",
        str(input_path),
        "--audit-dir",
        str(audit_dir),
    ]
    if canonical_top is not None:
        command.extend(("--canonical-top", str(canonical_top)))
    if args.rigid_rotation_x_degrees:
        command.extend(
            (
                "--rigid-rotation-x-degrees",
                str(args.rigid_rotation_x_degrees),
            )
        )
    if args.rigid_rotation_z_degrees:
        command.extend(
            (
                "--rigid-rotation-z-degrees",
                str(args.rigid_rotation_z_degrees),
            )
        )
    completed = subprocess.run(command, cwd=repo_root)
    if completed.returncode != 0:
        raise RuntimeError(
            "Blender audit failed with exit code "
            f"{completed.returncode}; no provider call was made"
        )

    detailed_path = audit_dir / "audit.json"
    audit = json.loads(detailed_path.read_text(encoding="utf-8"))
    compact = build_compact_report(
        audit,
        authored_span_tiles=args.authored_span_tiles,
        runtime_scale=args.runtime_scale,
        pixels_per_tile=args.pixels_per_tile,
    )
    compact_path = audit_dir / "geometry-report.json"
    write_json_atomic(compact_path, compact)
    print(
        json.dumps(
            {
                "auditDir": str(audit_dir),
                "detailedAudit": str(detailed_path),
                "geometryReport": str(compact_path),
                "inputSha256": compact["input"]["sha256"],
                "triangles": compact["geometry"]["triangles"],
                "weldedConnectedComponents": compact["geometry"][
                    "weldedConnectedComponents"
                ],
                "fixedViews": sorted(compact["renders"]["views"]),
                "topSilhouetteCompared": (
                    compact["acceptanceInputs"]["topSilhouette"] is not None
                ),
                "providerCalls": 0,
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        print(f"Smart geometry audit failed: {error}", file=sys.stderr)
        raise SystemExit(2)
