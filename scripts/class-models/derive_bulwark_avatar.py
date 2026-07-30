#!/usr/bin/env python3
"""Derive archive-only 2D Bulwark art from the accepted textured authority.

The public wrapper verifies the exact source authority before Blender starts,
resolves the repository-pinned Blender 5.2 executable, and delegates rendering
to ``derive_bulwark_avatar_blender.py``. It never calls a provider, never
modifies the source, and refuses to overwrite an existing output directory.
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


EXPECTED_REPORT_SCHEMA = "nilbots-bulwark-avatar-derivation-report-1"
EXPECTED_BLENDER = (5, 2)
EXPECTED_SOURCE_BLEND_SHA256 = (
    "b6827956ff032dbf01564e26706f19400305b1245bedee4c65960bddc448df23"
)
PINNED_BLENDER_RELATIVE_CANDIDATES = (
    Path("var/toolchains/Blender-5.2.0.app/Contents/MacOS/Blender"),
    Path("var/toolchains/blender-5.2.0/blender"),
)
ARCHIVE_OUTPUT_PREFIX = Path(
    "art/class-models/masters/aegis-tortoise-kinematic/proofs"
)
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")


def sha256_argument(value: str) -> str:
    normalized = value.strip().lower()
    if not SHA256_RE.fullmatch(normalized):
        raise argparse.ArgumentTypeError(
            "expected a lowercase 64-character SHA-256"
        )
    return normalized


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Render one transparent East-facing Bulwark avatar and separate "
            "semantic team-accent mask from an exact local .blend authority. "
            "No provider or network calls are made."
        )
    )
    parser.add_argument(
        "--input-blend",
        type=Path,
        required=True,
        help="Repository-relative accepted textured Bulwark .blend authority.",
    )
    parser.add_argument(
        "--expected-source-sha256",
        type=sha256_argument,
        required=True,
        help="Exact SHA-256 of --input-blend.",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        required=True,
        help=(
            "New repository-relative directory below sandbox/, var/, or the "
            "Aegis Tortoise archive proofs directory."
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


def resolve_source(path: Path, *, repo_root: Path) -> tuple[Path, Path]:
    resolved, relative = resolve_repo_path(
        path,
        repo_root=repo_root,
        option="--input-blend",
        strict=True,
    )
    if not resolved.is_file() or resolved.suffix.lower() != ".blend":
        raise ValueError(f"Expected an existing .blend file: {relative}")
    return resolved, relative


def archive_output_allowed(relative: Path) -> bool:
    try:
        remainder = relative.relative_to(ARCHIVE_OUTPUT_PREFIX)
    except ValueError:
        return False
    return bool(remainder.parts)


def resolve_output(path: Path, *, repo_root: Path) -> tuple[Path, Path]:
    resolved, relative = resolve_repo_path(
        path,
        repo_root=repo_root,
        option="--output-dir",
        strict=False,
    )
    disposable = bool(relative.parts) and relative.parts[0] in {"sandbox", "var"}
    if not disposable and not archive_output_allowed(relative):
        raise ValueError(
            "--output-dir must be below sandbox/, var/, or "
            f"{ARCHIVE_OUTPUT_PREFIX.as_posix()}/"
        )
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


def load_report(path: Path) -> dict[str, Any]:
    report = json.loads(path.read_text(encoding="utf-8"))
    if report.get("schema") != EXPECTED_REPORT_SCHEMA:
        raise ValueError(
            "Unexpected avatar derivation report schema: "
            f"{report.get('schema')!r}"
        )
    if report.get("passed") is not True:
        raise ValueError("Blender avatar derivation report did not pass")
    return report


def verify_artifacts(
    report: dict[str, Any],
    *,
    repo_root: Path,
    expected_source_relative: Path,
    expected_source_sha256: str,
    expected_output_relative: Path,
) -> None:
    source = report.get("source", {})
    if source.get("path") != expected_source_relative.as_posix():
        raise ValueError("Report source path does not match requested source")
    if source.get("sha256") != expected_source_sha256:
        raise ValueError("Report source hash does not match requested source")

    outputs = report.get("outputs")
    if not isinstance(outputs, dict) or set(outputs) != {
        "avatar",
        "teamAccentMask",
    }:
        raise ValueError("Report must contain exactly avatar and mask outputs")
    output_prefix = expected_output_relative.as_posix() + "/"
    for label, record in outputs.items():
        relative_text = record.get("path")
        expected_hash = record.get("sha256")
        if not isinstance(relative_text, str) or not relative_text.startswith(
            output_prefix
        ):
            raise ValueError(f"Output {label!r} escaped the requested directory")
        if not isinstance(expected_hash, str) or not SHA256_RE.fullmatch(
            expected_hash
        ):
            raise ValueError(f"Output {label!r} has no valid SHA-256")
        artifact, _ = resolve_repo_path(
            Path(relative_text),
            repo_root=repo_root,
            option=f"report output {label!r}",
            strict=True,
        )
        if not artifact.is_file():
            raise ValueError(f"Output {label!r} is not a file")
        if sha256(artifact) != expected_hash:
            raise ValueError(f"Output {label!r} hash drifted after Blender")


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    repo_root = repository_root()
    source, source_relative = resolve_source(
        args.input_blend,
        repo_root=repo_root,
    )
    output, output_relative = resolve_output(
        args.output_dir,
        repo_root=repo_root,
    )

    observed_source_sha256 = sha256(source)
    if observed_source_sha256 != args.expected_source_sha256:
        raise ValueError(
            "Source hash mismatch before Blender launch: "
            f"expected {args.expected_source_sha256}, "
            f"observed {observed_source_sha256}"
        )
    if args.expected_source_sha256 != EXPECTED_SOURCE_BLEND_SHA256:
        raise ValueError(
            "This derivation is pinned to the accepted textured Bulwark "
            f"authority {EXPECTED_SOURCE_BLEND_SHA256}; received "
            f"{args.expected_source_sha256}"
        )

    blender = resolve_blender(repo_root)
    blender_script = Path(
        "scripts/class-models/derive_bulwark_avatar_blender.py"
    )
    completed = subprocess.run(
        [
            str(blender),
            "--background",
            "--factory-startup",
            "--python",
            blender_script.as_posix(),
            "--",
            "--input-blend",
            source_relative.as_posix(),
            "--expected-source-sha256",
            args.expected_source_sha256,
            "--output-dir",
            output_relative.as_posix(),
        ],
        cwd=repo_root,
        check=False,
        text=True,
    )
    if completed.returncode != 0:
        raise RuntimeError(
            "Pinned Blender avatar derivation failed with exit code "
            f"{completed.returncode}. Partial output, if any, was retained "
            "for diagnosis and will not be overwritten."
        )

    report_path = output / "report.json"
    if not report_path.is_file():
        raise ValueError("Blender completed without writing report.json")
    report = load_report(report_path)
    verify_artifacts(
        report,
        repo_root=repo_root,
        expected_source_relative=source_relative,
        expected_source_sha256=args.expected_source_sha256,
        expected_output_relative=output_relative,
    )
    print(
        json.dumps(
            {
                "passed": True,
                "report": (output_relative / "report.json").as_posix(),
                "avatar": report["outputs"]["avatar"]["path"],
                "teamAccentMask": report["outputs"]["teamAccentMask"]["path"],
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(1)
