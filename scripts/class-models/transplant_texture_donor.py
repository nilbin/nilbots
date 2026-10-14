#!/usr/bin/env python3
"""Apply a proven texture donor to an untouched animated Blender authority.

This launcher is deliberately offline. It pins every input by SHA-256, accepts
repository-relative paths only, refuses to overwrite output, resolves the
shared Blender 5.2 toolchain, and delegates scene work to the adjacent Blender
script. Provider geometry, hierarchy, pivots, and animation are never copied.
The original v2 path consumes embedded donor images. The opt-in v3 path uses
five archived standalone provider PNGs without changing v2 report semantics.
The opt-in v4 path additionally reconstructs and validates a pinned world-baked
coverage-pose upload while still exporting the original animated hierarchy.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
from pathlib import Path
from typing import Any


EXPECTED_BLENDER = (5, 2)
REPORT_SCHEMA = "nilbots-texture-donor-transplant-2"
LOSSLESS_PBR_REPORT_SCHEMA = "nilbots-texture-donor-transplant-3"
COVERAGE_POSE_REPORT_SCHEMA = "nilbots-texture-donor-transplant-4"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COLLECTION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_. -]{0,127}$")
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
PINNED_BLENDER_RELATIVE_CANDIDATES = (
    Path("var/toolchains/Blender-5.2.0.app/Contents/MacOS/Blender"),
    Path("var/toolchains/blender-5.2.0/blender"),
)


def sha256_argument(value: str) -> str:
    normalized = value.strip().lower()
    if not SHA256_RE.fullmatch(normalized):
        raise argparse.ArgumentTypeError(
            "expected a lowercase 64-character SHA-256"
        )
    return normalized


def collection_argument(value: str) -> str:
    if not COLLECTION_RE.fullmatch(value):
        raise argparse.ArgumentTypeError(
            "collection must be a simple Blender collection name"
        )
    return value


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Transplant only PBR maps and material factors from a proven "
            "Retexture donor into a copy of an authored animated .blend. "
            "Optionally substitute the archived standalone lossless PNG "
            "payload, and optionally validate a pinned world-baked coverage "
            "upload. No provider or network calls are made."
        )
    )
    parser.add_argument(
        "--authored-blend",
        required=True,
        type=Path,
        help="Repository-relative untouched animated Blender authority.",
    )
    parser.add_argument(
        "--expected-authored-sha256",
        required=True,
        type=sha256_argument,
    )
    parser.add_argument(
        "--uploaded-source-glb",
        required=True,
        type=Path,
        help="Exact repository-relative GLB uploaded for Retexture.",
    )
    parser.add_argument(
        "--expected-uploaded-source-sha256",
        required=True,
        type=sha256_argument,
    )
    parser.add_argument(
        "--donor-glb",
        required=True,
        type=Path,
        help="Repository-relative Retexture donor GLB.",
    )
    parser.add_argument(
        "--expected-donor-sha256",
        required=True,
        type=sha256_argument,
    )
    parser.add_argument(
        "--model-collection",
        required=True,
        type=collection_argument,
        help="Authored collection containing only the exported model family.",
    )
    parser.add_argument(
        "--bind-frame",
        type=int,
        default=1,
        help="Authored mobile/bind frame used for surface correspondence.",
    )
    parser.add_argument(
        "--output-dir",
        required=True,
        type=Path,
        help=(
            "New repository-relative directory below sandbox/ or var/. "
            "Existing output is never overwritten."
        ),
    )
    pbr = parser.add_argument_group(
        "optional archived standalone lossless PBR payload"
    )
    for (
        _role,
        path_destination,
        hash_destination,
        path_option,
        hash_option,
        _report_key,
    ) in STANDALONE_PBR_OPTIONS:
        pbr.add_argument(
            path_option,
            dest=path_destination,
            type=Path,
            help=(
                "Repository-relative archived provider PNG. Supplying any "
                "standalone PBR option requires all five maps and hashes."
            ),
        )
        pbr.add_argument(
            hash_option,
            dest=hash_destination,
            type=sha256_argument,
        )
    coverage = parser.add_argument_group(
        "optional pinned world-baked coverage-pose upload"
    )
    coverage.add_argument(
        "--coverage-validation-report",
        type=Path,
        help=(
            "Repository-relative PASS report pinning the authored authority, "
            "coverage upload, active clips/frame, and world-bake policy."
        ),
    )
    coverage.add_argument(
        "--expected-coverage-validation-report-sha256",
        type=sha256_argument,
    )
    coverage.add_argument(
        "--coverage-pose-clip",
        action="append",
        type=collection_argument,
        help="Semantic NLA clip to evaluate; repeat once per active clip.",
    )
    coverage.add_argument("--coverage-pose-frame", type=int)
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


def resolve_blender(repo_root: Path) -> Path:
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


def require_relative(path: Path, option: str) -> None:
    if path.is_absolute():
        raise ValueError(f"{option} must be repository-relative")
    if not path.parts:
        raise ValueError(f"{option} cannot be empty")


def resolve_repo_path(
    path: Path,
    *,
    repo_root: Path,
    option: str,
    strict: bool,
) -> tuple[Path, Path]:
    require_relative(path, option)
    resolved = (repo_root / path).resolve(strict=strict)
    try:
        relative = resolved.relative_to(repo_root)
    except ValueError as error:
        raise ValueError(f"{option} must stay inside the repository") from error
    return resolved, relative


def resolve_source(
    path: Path,
    *,
    repo_root: Path,
    option: str,
    extension: str,
) -> tuple[Path, Path]:
    resolved, relative = resolve_repo_path(
        path,
        repo_root=repo_root,
        option=option,
        strict=True,
    )
    if not resolved.is_file() or resolved.suffix.lower() != extension:
        raise ValueError(
            f"{option} must name an existing {extension} file: {relative}"
        )
    return resolved, relative


def resolve_output(
    path: Path,
    *,
    repo_root: Path,
) -> tuple[Path, Path]:
    resolved, relative = resolve_repo_path(
        path,
        repo_root=repo_root,
        option="--output-dir",
        strict=False,
    )
    if not relative.parts or relative.parts[0] not in {"sandbox", "var"}:
        raise ValueError("--output-dir must be below repository sandbox/ or var/")
    if resolved.exists():
        raise ValueError(
            f"Output directory already exists; refusing overwrite: {relative}"
        )
    return resolved, relative


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def verify_hash(path: Path, expected: str, label: str) -> None:
    observed = sha256(path)
    if observed != expected:
        raise ValueError(
            f"{label} hash mismatch before Blender launch: "
            f"expected {expected}, observed {observed}"
        )


def blender_command(
    *,
    blender: Path,
    authored_relative: Path,
    authored_sha256: str,
    uploaded_relative: Path,
    uploaded_sha256: str,
    donor_relative: Path,
    donor_sha256: str,
    model_collection: str,
    bind_frame: int,
    output_relative: Path,
    standalone_pbr: dict[str, dict[str, Any]] | None = None,
    coverage_pose: dict[str, Any] | None = None,
) -> list[str]:
    command = [
        str(blender),
        "--background",
        "--factory-startup",
        "--python",
        "scripts/class-models/transplant_texture_donor_blender.py",
        "--",
        "--authored-blend",
        authored_relative.as_posix(),
        "--expected-authored-sha256",
        authored_sha256,
        "--uploaded-source-glb",
        uploaded_relative.as_posix(),
        "--expected-uploaded-source-sha256",
        uploaded_sha256,
        "--donor-glb",
        donor_relative.as_posix(),
        "--expected-donor-sha256",
        donor_sha256,
        "--model-collection",
        model_collection,
        "--bind-frame",
        str(bind_frame),
        "--output-dir",
        output_relative.as_posix(),
    ]
    if standalone_pbr is not None:
        for (
            role,
            _path_destination,
            _hash_destination,
            path_option,
            hash_option,
            _report_key,
        ) in STANDALONE_PBR_OPTIONS:
            record = standalone_pbr[role]
            command.extend(
                [
                    path_option,
                    record["relative"].as_posix(),
                    hash_option,
                    record["sha256"],
                ]
            )
    if coverage_pose is not None:
        command.extend(
            [
                "--coverage-validation-report",
                coverage_pose["relative"].as_posix(),
                "--expected-coverage-validation-report-sha256",
                coverage_pose["sha256"],
                "--coverage-pose-frame",
                str(coverage_pose["frame"]),
            ]
        )
        for clip in coverage_pose["clips"]:
            command.extend(["--coverage-pose-clip", clip])
    return command


def load_report(
    path: Path,
    *,
    expected_schema: str = REPORT_SCHEMA,
) -> dict[str, Any]:
    report = json.loads(path.read_text(encoding="utf-8"))
    if report.get("schema") != expected_schema:
        raise ValueError(
            f"Unexpected texture transplant report schema: {report.get('schema')!r}"
        )
    if report.get("status") != "PASS":
        raise ValueError("Texture transplant report did not pass")
    return report


def resolve_standalone_pbr(
    args: argparse.Namespace,
    *,
    repo_root: Path,
) -> dict[str, dict[str, Any]] | None:
    supplied = []
    for (
        _role,
        path_destination,
        hash_destination,
        path_option,
        hash_option,
        _report_key,
    ) in STANDALONE_PBR_OPTIONS:
        path_value = getattr(args, path_destination)
        hash_value = getattr(args, hash_destination)
        supplied.append((path_value is not None, path_option))
        supplied.append((hash_value is not None, hash_option))
    if not any(present for present, _option in supplied):
        return None
    missing = [option for present, option in supplied if not present]
    if missing:
        raise ValueError(
            "Standalone PBR mode requires all five PNG paths and hashes; "
            "missing: " + ", ".join(missing)
        )

    resolved: dict[str, dict[str, Any]] = {}
    seen_paths: set[Path] = set()
    for (
        role,
        path_destination,
        hash_destination,
        path_option,
        _hash_option,
        report_key,
    ) in STANDALONE_PBR_OPTIONS:
        path, relative = resolve_source(
            getattr(args, path_destination),
            repo_root=repo_root,
            option=path_option,
            extension=".png",
        )
        if path in seen_paths:
            raise ValueError(
                "Standalone PBR roles must reference five distinct PNG files"
            )
        seen_paths.add(path)
        resolved[role] = {
            "path": path,
            "relative": relative,
            "sha256": getattr(args, hash_destination),
            "reportKey": report_key,
        }
    return resolved


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
    if not clips or len(set(clips)) != len(clips):
        raise ValueError("Coverage-pose clips must be non-empty and unique")
    if args.coverage_pose_frame < 0:
        raise ValueError("--coverage-pose-frame must be non-negative")
    path, relative = resolve_source(
        args.coverage_validation_report,
        repo_root=repo_root,
        option="--coverage-validation-report",
        extension=".json",
    )
    return {
        "path": path,
        "relative": relative,
        "sha256": args.expected_coverage_validation_report_sha256,
        "clips": clips,
        "frame": args.coverage_pose_frame,
        "reportKey": "coverageValidationReport",
    }


def contains_absolute_path(value: Any) -> bool:
    if isinstance(value, dict):
        return any(
            contains_absolute_path(key) or contains_absolute_path(item)
            for key, item in value.items()
        )
    if isinstance(value, list):
        return any(contains_absolute_path(item) for item in value)
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


def verify_report(
    report: dict[str, Any],
    *,
    repo_root: Path,
    output_relative: Path,
    inputs: dict[str, tuple[Path, str]],
) -> None:
    if contains_absolute_path(report):
        raise ValueError("Sanitized report contains an absolute path")
    if report.get("outputDirectory") != output_relative.as_posix():
        raise ValueError("Report output directory does not match request")
    reported_inputs = report.get("inputs")
    if not isinstance(reported_inputs, dict):
        raise ValueError("Report omitted its input ledger")
    for key, (relative, expected_hash) in inputs.items():
        record = reported_inputs.get(key, {})
        if record.get("path") != relative.as_posix():
            raise ValueError(f"Report {key} path does not match request")
        if record.get("sha256") != expected_hash:
            raise ValueError(f"Report {key} hash does not match request")

    checks = report.get("checks")
    if not isinstance(checks, dict) or not checks:
        raise ValueError("Report contains no validation checks")
    if any(value is not True for value in checks.values()):
        raise ValueError("Report contains a failed validation check")

    artifacts = report.get("artifacts")
    if not isinstance(artifacts, list) or not artifacts:
        raise ValueError("Report contains no artifacts")
    output_root = (repo_root / output_relative).resolve(strict=True)
    if not output_root.is_dir():
        raise ValueError("Report output directory is not a directory")
    output_prefix = output_relative.as_posix() + "/"
    seen: set[str] = set()
    for artifact in artifacts:
        if not isinstance(artifact, dict):
            raise ValueError("Report contains a malformed artifact")
        path_text = artifact.get("path")
        expected_hash = artifact.get("sha256")
        if (
            not isinstance(path_text, str)
            or not path_text.startswith(output_prefix)
            or path_text in seen
        ):
            raise ValueError("Report contains an invalid artifact path")
        if not isinstance(expected_hash, str) or not SHA256_RE.fullmatch(
            expected_hash
        ):
            raise ValueError(f"Artifact has no valid SHA-256: {path_text!r}")
        seen.add(path_text)
        path, _ = resolve_repo_path(
            Path(path_text),
            repo_root=repo_root,
            option=f"artifact {path_text!r}",
            strict=True,
        )
        try:
            path.relative_to(output_root)
        except ValueError as error:
            raise ValueError(
                f"Artifact escaped the output directory: {path_text}"
            ) from error
        if not path.is_file() or sha256(path) != expected_hash:
            raise ValueError(f"Artifact hash mismatch: {path_text}")

    required = {
        (output_relative / "textured-authority.blend").as_posix(),
        (output_relative / "textured-animated.glb").as_posix(),
    }
    if not required.issubset(seen):
        raise ValueError(
            "Report omitted required outputs: "
            + ", ".join(sorted(required - seen))
        )


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    if args.bind_frame < 0:
        raise ValueError("--bind-frame must be non-negative")
    repo_root = repository_root()
    authored, authored_relative = resolve_source(
        args.authored_blend,
        repo_root=repo_root,
        option="--authored-blend",
        extension=".blend",
    )
    uploaded, uploaded_relative = resolve_source(
        args.uploaded_source_glb,
        repo_root=repo_root,
        option="--uploaded-source-glb",
        extension=".glb",
    )
    donor, donor_relative = resolve_source(
        args.donor_glb,
        repo_root=repo_root,
        option="--donor-glb",
        extension=".glb",
    )
    standalone_pbr = resolve_standalone_pbr(args, repo_root=repo_root)
    coverage_pose = resolve_coverage_pose(args, repo_root=repo_root)
    output, output_relative = resolve_output(
        args.output_dir,
        repo_root=repo_root,
    )
    del output

    verify_hash(
        authored,
        args.expected_authored_sha256,
        "Authored .blend",
    )
    verify_hash(
        uploaded,
        args.expected_uploaded_source_sha256,
        "Uploaded source GLB",
    )
    verify_hash(donor, args.expected_donor_sha256, "Donor GLB")
    if standalone_pbr is not None:
        for role, record in standalone_pbr.items():
            verify_hash(
                record["path"],
                record["sha256"],
                f"Standalone {role} PNG",
            )
    if coverage_pose is not None:
        verify_hash(
            coverage_pose["path"],
            coverage_pose["sha256"],
            "Coverage validation report",
        )

    blender = resolve_blender(repo_root)
    completed = subprocess.run(
        blender_command(
            blender=blender,
            authored_relative=authored_relative,
            authored_sha256=args.expected_authored_sha256,
            uploaded_relative=uploaded_relative,
            uploaded_sha256=args.expected_uploaded_source_sha256,
            donor_relative=donor_relative,
            donor_sha256=args.expected_donor_sha256,
            model_collection=args.model_collection,
            bind_frame=args.bind_frame,
            output_relative=output_relative,
            standalone_pbr=standalone_pbr,
            coverage_pose=coverage_pose,
        ),
        cwd=repo_root,
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError(
            "Pinned Blender texture transplant failed with exit code "
            f"{completed.returncode}. Partial output, if any, was retained "
            "for diagnosis and will not be overwritten."
        )

    report_path = repo_root / output_relative / "report.json"
    if not report_path.is_file():
        raise ValueError("Blender completed without writing report.json")
    expected_schema = (
        COVERAGE_POSE_REPORT_SCHEMA
        if coverage_pose is not None
        else (
            LOSSLESS_PBR_REPORT_SCHEMA
            if standalone_pbr is not None
            else REPORT_SCHEMA
        )
    )
    report = load_report(report_path, expected_schema=expected_schema)
    report_inputs = {
        "authoredBlend": (
            authored_relative,
            args.expected_authored_sha256,
        ),
        "uploadedSourceGlb": (
            uploaded_relative,
            args.expected_uploaded_source_sha256,
        ),
        "donorGlb": (donor_relative, args.expected_donor_sha256),
    }
    if standalone_pbr is not None:
        report_inputs.update(
            {
                record["reportKey"]: (
                    record["relative"],
                    record["sha256"],
                )
                for record in standalone_pbr.values()
            }
        )
    if coverage_pose is not None:
        report_inputs[coverage_pose["reportKey"]] = (
            coverage_pose["relative"],
            coverage_pose["sha256"],
        )
    verify_report(
        report,
        repo_root=repo_root,
        output_relative=output_relative,
        inputs=report_inputs,
    )
    print(
        json.dumps(
            {
                "status": "PASS",
                "report": (output_relative / "report.json").as_posix(),
                "blend": (
                    output_relative / "textured-authority.blend"
                ).as_posix(),
                "glb": (
                    output_relative / "textured-animated.glb"
                ).as_posix(),
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (
        OSError,
        ValueError,
        RuntimeError,
        subprocess.SubprocessError,
    ) as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(1)
