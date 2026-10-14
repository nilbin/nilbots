#!/usr/bin/env python3
"""Build a deterministic, renderer-friendly derivative of a proven PBR skin.

This is an offline post-process, not a new texture-generation step.  It grades
the archived standalone PNG maps, then asks pinned Blender 5.2 to substitute
only those maps in the already-validated animated authority.  The source donor
transplant is immutable and all geometry, UV, hierarchy, pivot, semantic team
material, and animation invariants are checked against it.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from lossless_pbr_png import (  # noqa: E402
    encode_rgb8_png,
    inspect_png,
    sha256_bytes,
)


REPORT_SCHEMA = "nilbots-runtime-pbr-grade-1"
PROFILE = "aegis-foundry-runtime-v1"
EXPECTED_BLENDER = (5, 2)
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
PINNED_BLENDER_RELATIVE_CANDIDATES = (
    Path("var/toolchains/Blender-5.2.0.app/Contents/MacOS/Blender"),
    Path("var/toolchains/blender-5.2.0/blender"),
)

# All arithmetic is integer-only so the same source bytes always produce the
# same result.  The curve lifts graphite shadows while compressing white source
# islands into a darker warm-ash band.  Strong Foundry orange is soft-masked
# back to its original samples to avoid hard boundaries at antialiased edges.
LUMA_CURVE = (
    (0, 18),
    (8, 34),
    (16, 46),
    (32, 62),
    (48, 75),
    (64, 86),
    (96, 101),
    (128, 112),
    (160, 121),
    (192, 129),
    (224, 136),
    (255, 142),
)
LUMA_WEIGHTS_256 = (54, 183, 19)
ASH_OFFSETS = (2, 1, -3)
CHROMA_DENOMINATORS = (5, 4, 7)
WARM_RED_MINIMUM = 64
WARM_RED_GREEN_MARGIN = 8
WARM_GREEN_BLUE_MARGIN = 4
WARM_BLEND_MULTIPLIERS = (4, 8, 8)
ROUGHNESS_FLOOR = 140
ROUGHNESS_AFFINE_OFFSET = 116
ROUGHNESS_DETAIL_NUMERATOR = 9
ROUGHNESS_DETAIL_DENOMINATOR = 16
METALLIC_CAP = 102


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
    common = Path(completed.stdout.strip())
    common = (repo_root / common).resolve() if not common.is_absolute() else common.resolve()
    if common.name != ".git":
        raise ValueError(f"Expected git common directory ending in .git: {common}")
    return common.parent


def resolve_blender(repo_root: Path) -> Path:
    shared = shared_worktree_root(repo_root)
    candidates = [
        (shared / relative).resolve()
        for relative in PINNED_BLENDER_RELATIVE_CANDIDATES
    ]
    blender = next(
        (
            path
            for path in candidates
            if path.is_file() and os.access(path, os.X_OK)
        ),
        None,
    )
    if blender is None:
        raise ValueError(
            "Pinned Blender 5.2 was not found: "
            + ", ".join(str(path) for path in candidates)
        )
    return blender


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


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", required=True, type=Path)
    parser.add_argument("--expected-source-report-sha256", required=True)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--profile", choices=[PROFILE], default=PROFILE)
    args = parser.parse_args(argv)
    if not SHA256_RE.fullmatch(args.expected_source_report_sha256):
        parser.error("--expected-source-report-sha256 must be lowercase SHA-256")
    return args


def clamp8(value: int) -> int:
    return max(0, min(255, value))


def rounded_signed_division(numerator: int, denominator: int) -> int:
    if numerator >= 0:
        return (numerator + denominator // 2) // denominator
    return -((-numerator + denominator // 2) // denominator)


def interpolate_curve(value: int) -> int:
    for index in range(len(LUMA_CURVE) - 1):
        left_x, left_y = LUMA_CURVE[index]
        right_x, right_y = LUMA_CURVE[index + 1]
        if value <= right_x:
            span = right_x - left_x
            offset = value - left_x
            return left_y + (offset * (right_y - left_y) + span // 2) // span
    return LUMA_CURVE[-1][1]


def luma(red: int, green: int, blue: int) -> int:
    wr, wg, wb = LUMA_WEIGHTS_256
    return (red * wr + green * wg + blue * wb + 128) // 256


def warm_blend_weight(red: int, green: int, blue: int) -> int:
    red_gate = max(0, red - WARM_RED_MINIMUM) * WARM_BLEND_MULTIPLIERS[0]
    rg_gate = max(0, red - green - WARM_RED_GREEN_MARGIN) * WARM_BLEND_MULTIPLIERS[1]
    gb_gate = max(0, green - blue - WARM_GREEN_BLUE_MARGIN) * WARM_BLEND_MULTIPLIERS[2]
    return min(255, red_gate, rg_gate, gb_gate)


def histogram_quantiles(histogram: list[int]) -> dict[str, int]:
    total = sum(histogram)
    if total <= 0:
        raise ValueError("Cannot summarize an empty histogram")
    targets = {
        "minimum": 0,
        "p01": total // 100,
        "p10": total // 10,
        "median": total // 2,
        "p90": total * 9 // 10,
        "p99": total * 99 // 100,
        "maximum": total - 1,
    }
    result: dict[str, int] = {}
    running = 0
    for sample, count in enumerate(histogram):
        running += count
        for name, target in targets.items():
            if name not in result and running > target:
                result[name] = sample
    return result


def grade_base_color(payload: bytes) -> tuple[bytes, dict[str, Any]]:
    source = inspect_png(payload, label="Source base color", include_samples=True)
    if source["colorType"] != 2 or source["channels"] != 3:
        raise ValueError("Source base color must be RGB8")
    samples: bytes = source.pop("samples")
    output = bytearray(len(samples))
    input_luma = [0] * 256
    output_luma = [0] * 256
    warm_pixels = 0
    strong_warm_pixels = 0
    source_white_pixels = 0
    source_near_black_pixels = 0
    source_near_black_output_luma_sum = 0
    purple_excess_before = 0
    purple_excess_after = 0
    non_warm_pixels = 0

    for offset in range(0, len(samples), 3):
        red, green, blue = samples[offset : offset + 3]
        source_y = luma(red, green, blue)
        graded_y = interpolate_curve(source_y)
        graded = [
            clamp8(
                graded_y
                + ASH_OFFSETS[channel]
                + rounded_signed_division(
                    (red, green, blue)[channel] - source_y,
                    CHROMA_DENOMINATORS[channel],
                )
            )
            for channel in range(3)
        ]
        weight = warm_blend_weight(red, green, blue)
        if weight:
            warm_pixels += 1
        if weight == 255:
            strong_warm_pixels += 1
        inverse = 255 - weight
        final = [
            clamp8((graded[channel] * inverse + original * weight + 127) // 255)
            for channel, original in enumerate((red, green, blue))
        ]
        output[offset : offset + 3] = bytes(final)
        final_y = luma(*final)
        input_luma[source_y] += 1
        output_luma[final_y] += 1
        if max(red, green, blue) - min(red, green, blue) <= 32 and source_y >= 192:
            source_white_pixels += 1
        if source_y <= 48:
            source_near_black_pixels += 1
            source_near_black_output_luma_sum += final_y
        if weight < 128:
            non_warm_pixels += 1
            purple_excess_before += max(0, (red + blue + 1) // 2 - green)
            purple_excess_after += max(0, (final[0] + final[2] + 1) // 2 - final[1])

    encoded = encode_rgb8_png(
        width=source["width"],
        height=source["height"],
        samples=bytes(output),
    )
    result = inspect_png(encoded, label="Graded base color")
    pixel_count = source["width"] * source["height"]
    return encoded, {
        "algorithm": "integer luma compression + warm-ash tint + soft Foundry-orange preservation",
        "input": source,
        "output": result,
        "parameters": {
            "lumaWeightsOver256": list(LUMA_WEIGHTS_256),
            "lumaCurve": [list(point) for point in LUMA_CURVE],
            "ashOffsetsRgb8": list(ASH_OFFSETS),
            "residualChromaDenominatorsRgb": list(CHROMA_DENOMINATORS),
            "warmMask": {
                "redMinimumExclusive": WARM_RED_MINIMUM,
                "redGreenMarginExclusive": WARM_RED_GREEN_MARGIN,
                "greenBlueMarginExclusive": WARM_GREEN_BLUE_MARGIN,
                "gateMultipliers": list(WARM_BLEND_MULTIPLIERS),
                "softBlendScale": 255,
            },
        },
        "evidence": {
            "pixels": pixel_count,
            "warmBlendPixels": warm_pixels,
            "strongWarmPreservedPixels": strong_warm_pixels,
            "sourceNeutralWhitePixels": source_white_pixels,
            "sourceNearBlackPixels": source_near_black_pixels,
            "sourceNearBlackMeanOutputLuma": round(
                source_near_black_output_luma_sum / max(1, source_near_black_pixels),
                4,
            ),
            "nonWarmPurpleExcessMeanBefore": round(
                purple_excess_before / max(1, non_warm_pixels), 4
            ),
            "nonWarmPurpleExcessMeanAfter": round(
                purple_excess_after / max(1, non_warm_pixels), 4
            ),
            "inputLumaQuantiles": histogram_quantiles(input_luma),
            "outputLumaQuantiles": histogram_quantiles(output_luma),
            "outputBodyValueBands": {
                "recess0to63": sum(output_luma[:64]),
                "graphite64to103": sum(output_luma[64:104]),
                "ash104to159": sum(output_luma[104:160]),
                "foundryOrHighlight160to255": sum(output_luma[160:]),
            },
        },
    }


def grade_metallic_roughness(payload: bytes) -> tuple[bytes, dict[str, Any]]:
    source = inspect_png(
        payload,
        label="Source metallic/roughness",
        include_samples=True,
    )
    if source["colorType"] != 2 or source["channels"] != 3:
        raise ValueError("Source metallic/roughness must be RGB8")
    samples: bytes = source.pop("samples")
    output = bytearray(len(samples))
    roughness_changed = 0
    metallic_changed = 0
    roughness_min = 255
    roughness_max = 0
    metallic_min = 255
    metallic_max = 0
    for offset in range(0, len(samples), 3):
        red, roughness, metallic = samples[offset : offset + 3]
        graded_roughness = max(
            ROUGHNESS_FLOOR,
            ROUGHNESS_AFFINE_OFFSET
            + (roughness * ROUGHNESS_DETAIL_NUMERATOR + ROUGHNESS_DETAIL_DENOMINATOR // 2)
            // ROUGHNESS_DETAIL_DENOMINATOR,
        )
        graded_roughness = clamp8(graded_roughness)
        graded_metallic = min(metallic, METALLIC_CAP)
        output[offset : offset + 3] = bytes((red, graded_roughness, graded_metallic))
        roughness_changed += graded_roughness != roughness
        metallic_changed += graded_metallic != metallic
        roughness_min = min(roughness_min, graded_roughness)
        roughness_max = max(roughness_max, graded_roughness)
        metallic_min = min(metallic_min, graded_metallic)
        metallic_max = max(metallic_max, graded_metallic)
    encoded = encode_rgb8_png(
        width=source["width"],
        height=source["height"],
        samples=bytes(output),
    )
    result = inspect_png(encoded, label="Graded metallic/roughness")
    return encoded, {
        "algorithm": "preserve R; G=max(floor, offset+round(G*numerator/denominator)); B=min(B, cap)",
        "input": source,
        "output": result,
        "parameters": {
            "roughnessFloorRgb8": ROUGHNESS_FLOOR,
            "roughnessFloorNormalized": round(ROUGHNESS_FLOOR / 255.0, 8),
            "roughnessAffineOffsetRgb8": ROUGHNESS_AFFINE_OFFSET,
            "roughnessDetailNumerator": ROUGHNESS_DETAIL_NUMERATOR,
            "roughnessDetailDenominator": ROUGHNESS_DETAIL_DENOMINATOR,
            "metallicCapRgb8": METALLIC_CAP,
            "metallicCapNormalized": round(METALLIC_CAP / 255.0, 8),
        },
        "evidence": {
            "pixels": source["width"] * source["height"],
            "roughnessChangedPixels": roughness_changed,
            "metallicChangedPixels": metallic_changed,
            "outputRoughnessMinimum": roughness_min,
            "outputRoughnessMaximum": roughness_max,
            "outputMetallicMinimum": metallic_min,
            "outputMetallicMaximum": metallic_max,
        },
    }


def zero_emissive(payload: bytes) -> tuple[bytes, dict[str, Any]]:
    source = inspect_png(payload, label="Source body emissive", include_samples=True)
    if source["colorType"] != 2 or source["channels"] != 3:
        raise ValueError("Source body emissive must be RGB8")
    samples: bytes = source.pop("samples")
    nonzero_samples = sum(value != 0 for value in samples)
    output_samples = bytes(len(samples))
    encoded = encode_rgb8_png(
        width=source["width"],
        height=source["height"],
        samples=output_samples,
    )
    result = inspect_png(encoded, label="Zero body emissive")
    return encoded, {
        "algorithm": "all RGB8 body-emissive samples set to zero",
        "input": source,
        "output": result,
        "parameters": {"outputRgb8": [0, 0, 0]},
        "evidence": {
            "inputNonzeroChannelSamples": nonzero_samples,
            "outputNonzeroChannelSamples": 0,
        },
    }


def source_texture_records(report: dict[str, Any]) -> dict[str, dict[str, Any]]:
    textures = report.get("pbr", {}).get("textures")
    if not isinstance(textures, list):
        raise ValueError("Source report has no PBR texture ledger")
    records = {
        item["role"]: item
        for item in textures
        if isinstance(item, dict) and isinstance(item.get("role"), str)
    }
    expected = {"baseColor", "metallicRoughness", "normal", "emissive"}
    if set(records) != expected:
        raise ValueError(f"Source PBR roles differ: {sorted(records)}")
    return records


def write_texture(
    *,
    texture_dir: Path,
    role: str,
    payload: bytes,
) -> dict[str, Any]:
    digest = sha256_bytes(payload)
    path = texture_dir / f"{role}-{digest[:16]}.png"
    path.write_bytes(payload)
    inspected = inspect_png(payload, label=f"Output {role}")
    return {
        "role": role,
        "path": path,
        "sha256": digest,
        "bytes": len(payload),
        "width": inspected["width"],
        "height": inspected["height"],
        "mimeType": inspected["mimeType"],
        "pixelSamplesSha256": inspected["pixelSamplesSha256"],
    }


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    repo_root = repository_root()
    source_dir, source_relative = resolve_repo_path(
        args.source_dir,
        repo_root=repo_root,
        option="--source-dir",
        strict=True,
    )
    output_dir, output_relative = resolve_repo_path(
        args.output_dir,
        repo_root=repo_root,
        option="--output-dir",
        strict=False,
    )
    if source_relative.parts[0] != "sandbox" or not source_dir.is_dir():
        raise ValueError("--source-dir must be an existing sandbox directory")
    if output_relative.parts[0] not in {"sandbox", "var"}:
        raise ValueError("--output-dir must stay below sandbox/ or var/")
    if output_dir.exists():
        raise ValueError(f"Refusing to overwrite output directory: {output_relative}")

    source_report_path = source_dir / "report.json"
    if sha256(source_report_path) != args.expected_source_report_sha256:
        raise ValueError("Source report SHA-256 does not match the required pin")
    source_report = json.loads(source_report_path.read_text(encoding="utf-8"))
    if source_report.get("status") != "PASS":
        raise ValueError("Source transplant report is not PASS")
    source_textures = source_texture_records(source_report)
    for role, record in source_textures.items():
        path, _relative = resolve_repo_path(
            Path(record["path"]),
            repo_root=repo_root,
            option=f"source {role}",
            strict=True,
        )
        if sha256(path) != record["sha256"]:
            raise ValueError(f"Source {role} hash drifted")
        record["resolvedPath"] = path

    source_artifacts = {
        item["kind"]: item
        for item in source_report.get("artifacts", [])
        if isinstance(item, dict) and item.get("kind") in {"blend", "animated-glb"}
    }
    if set(source_artifacts) != {"blend", "animated-glb"}:
        raise ValueError("Source report lacks blend/animated-glb artifact pins")
    for record in source_artifacts.values():
        path, _relative = resolve_repo_path(
            Path(record["path"]),
            repo_root=repo_root,
            option=f"source {record['kind']}",
            strict=True,
        )
        if sha256(path) != record["sha256"]:
            raise ValueError(f"Source {record['kind']} hash drifted")
        record["resolvedPath"] = path

    output_dir.mkdir(parents=True)
    try:
        texture_dir = output_dir / "textures"
        texture_dir.mkdir()
        base_payload, base_grade = grade_base_color(
            source_textures["baseColor"]["resolvedPath"].read_bytes()
        )
        mr_payload, mr_grade = grade_metallic_roughness(
            source_textures["metallicRoughness"]["resolvedPath"].read_bytes()
        )
        emission_payload, emission_grade = zero_emissive(
            source_textures["emissive"]["resolvedPath"].read_bytes()
        )
        normal_payload = source_textures["normal"]["resolvedPath"].read_bytes()
        textures = {
            "baseColor": write_texture(
                texture_dir=texture_dir, role="baseColor", payload=base_payload
            ),
            "metallicRoughness": write_texture(
                texture_dir=texture_dir, role="metallicRoughness", payload=mr_payload
            ),
            "normal": write_texture(
                texture_dir=texture_dir, role="normal", payload=normal_payload
            ),
            "emissive": write_texture(
                texture_dir=texture_dir, role="emissive", payload=emission_payload
            ),
        }
        payload_set = {
            role: record["sha256"] for role, record in sorted(textures.items())
        }
        manifest = {
            "schema": REPORT_SCHEMA,
            "profile": PROFILE,
            "source": {
                "directory": source_relative.as_posix(),
                "report": {
                    "path": source_report_path.relative_to(repo_root).as_posix(),
                    "sha256": args.expected_source_report_sha256,
                },
                "blend": {
                    "path": source_artifacts["blend"]["path"],
                    "sha256": source_artifacts["blend"]["sha256"],
                },
                "animatedGlb": {
                    "path": source_artifacts["animated-glb"]["path"],
                    "sha256": source_artifacts["animated-glb"]["sha256"],
                },
                "textures": {
                    role: {
                        "path": record["path"],
                        "sha256": record["sha256"],
                    }
                    for role, record in sorted(source_textures.items())
                },
            },
            "outputDirectory": output_relative.as_posix(),
            "textureGrade": {
                "baseColor": base_grade,
                "metallicRoughness": mr_grade,
                "normal": {
                    "algorithm": "byte-for-byte copy",
                    "sourceSha256": source_textures["normal"]["sha256"],
                    "outputSha256": textures["normal"]["sha256"],
                },
                "emissive": emission_grade,
            },
            "outputTextures": {
                role: {
                    **{key: value for key, value in record.items() if key != "path"},
                    "path": record["path"].relative_to(repo_root).as_posix(),
                }
                for role, record in sorted(textures.items())
            },
            "payloadSetSha256": stable_hash(payload_set),
            "policy": {
                "bodyEmission": "zero texture; semantic renderer-owned team material unchanged",
                "teamColorOwnership": "renderer",
                "geometryAuthority": "source donor-transplant blend only",
                "providerCalls": 0,
                "networkCalls": 0,
            },
        }
        manifest_path = output_dir / "grade-manifest.json"
        manifest_path.write_text(
            json.dumps(manifest, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )

        blender = resolve_blender(repo_root)
        blender_script = SCRIPT_DIR / "grade_runtime_pbr_blender.py"
        completed = subprocess.run(
            [
                str(blender),
                "--background",
                "--factory-startup",
                "--python",
                str(blender_script),
                "--",
                "--source-blend",
                source_artifacts["blend"]["path"],
                "--expected-source-blend-sha256",
                source_artifacts["blend"]["sha256"],
                "--source-glb",
                source_artifacts["animated-glb"]["path"],
                "--expected-source-glb-sha256",
                source_artifacts["animated-glb"]["sha256"],
                "--source-report",
                source_report_path.relative_to(repo_root).as_posix(),
                "--expected-source-report-sha256",
                args.expected_source_report_sha256,
                "--grade-manifest",
                manifest_path.relative_to(repo_root).as_posix(),
                "--expected-grade-manifest-sha256",
                sha256(manifest_path),
                "--output-dir",
                output_relative.as_posix(),
            ],
            cwd=repo_root,
            check=False,
            text=True,
            capture_output=True,
        )
        if completed.returncode != 0:
            sys.stderr.write(completed.stdout)
            sys.stderr.write(completed.stderr)
            raise RuntimeError(f"Pinned Blender grade failed ({completed.returncode})")
        sys.stdout.write(completed.stdout)
        report_path = output_dir / "report.json"
        report = json.loads(report_path.read_text(encoding="utf-8"))
        print(
            json.dumps(
                {
                    "status": report["status"],
                    "profile": PROFILE,
                    "report": report_path.relative_to(repo_root).as_posix(),
                    "blendSha256": report["artifacts"][0]["sha256"],
                    "animatedGlbSha256": report["artifacts"][1]["sha256"],
                },
                sort_keys=True,
            )
        )
    except Exception:
        # The target did not exist before this invocation, so cleaning this
        # script-owned partial output cannot erase user data.
        shutil.rmtree(output_dir)
        raise
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
