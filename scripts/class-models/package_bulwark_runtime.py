#!/usr/bin/env python3
"""Build deterministic offline runtime tiers from a Bulwark Mobile art master.

The public CLI accepts repository-relative paths only, verifies the exact
source ``.blend`` hash before Blender starts, resolves the repository's pinned
Blender 5.2 toolchain through the shared Git directory, and delegates all
asset work to the adjacent Blender-side script. It performs no provider or
network calls and never modifies the source art master.
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


EXPECTED_REPORT_SCHEMA = "nilbots-bulwark-runtime-package-1"
EXPECTED_BLENDER = (5, 2)
PINNED_BLENDER_RELATIVE_CANDIDATES = (
    Path("var/toolchains/Blender-5.2.0.app/Contents/MacOS/Blender"),
    Path("var/toolchains/blender-5.2.0/blender"),
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
            "Package one accented Bulwark Mobile .blend into fixed 2K, 1K, "
            "and 512 GLB tiers plus gameplay-scale comparison evidence. "
            "All paths are repository-relative; no provider or network calls "
            "are made."
        )
    )
    parser.add_argument(
        "--input-blend",
        type=Path,
        required=True,
        help="Repository-relative accented Bulwark Mobile .blend source.",
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
            "New repository-relative directory below sandbox/ or var/. "
            "Existing output is never overwritten."
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


def resolve_output(path: Path, *, repo_root: Path) -> tuple[Path, Path]:
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


def load_report(path: Path) -> dict[str, Any]:
    report = json.loads(path.read_text(encoding="utf-8"))
    if report.get("schema") != EXPECTED_REPORT_SCHEMA:
        raise ValueError(
            "Unexpected runtime package report schema: "
            f"{report.get('schema')!r}"
        )
    if report.get("passed") is not True:
        raise ValueError("Blender runtime package report did not pass")
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

    artifacts = report.get("artifacts")
    if not isinstance(artifacts, list) or not artifacts:
        raise ValueError("Report contains no artifact records")
    output_prefix = expected_output_relative.as_posix() + "/"
    seen_paths: set[str] = set()
    for record in artifacts:
        relative_text = record.get("path")
        expected_hash = record.get("sha256")
        if not isinstance(relative_text, str) or not relative_text.startswith(
            output_prefix
        ):
            raise ValueError("Reported artifact escaped the requested directory")
        if relative_text in seen_paths:
            raise ValueError(f"Duplicate artifact record: {relative_text}")
        seen_paths.add(relative_text)
        if not isinstance(expected_hash, str) or not SHA256_RE.fullmatch(
            expected_hash
        ):
            raise ValueError(f"Artifact has no valid SHA-256: {relative_text}")
        artifact, _ = resolve_repo_path(
            Path(relative_text),
            repo_root=repo_root,
            option=f"report artifact {relative_text!r}",
            strict=True,
        )
        if not artifact.is_file():
            raise ValueError(f"Reported artifact is not a file: {relative_text}")
        if sha256(artifact) != expected_hash:
            raise ValueError(f"Artifact hash drifted after Blender: {relative_text}")

    required = {
        (expected_output_relative / tier / "model.glb").as_posix()
        for tier in ("source-2k", "runtime-1k", "comparison-512")
    }
    if not required.issubset(seen_paths):
        missing = ", ".join(sorted(required - seen_paths))
        raise ValueError(f"Report omitted required GLB tier(s): {missing}")


def blender_command(
    *,
    blender: Path,
    source_relative: Path,
    source_sha256: str,
    output_relative: Path,
) -> list[str]:
    return [
        str(blender),
        "--background",
        "--factory-startup",
        "--python",
        "scripts/class-models/package_bulwark_runtime_blender.py",
        "--",
        "--input-blend",
        source_relative.as_posix(),
        "--expected-source-sha256",
        source_sha256,
        "--output-dir",
        output_relative.as_posix(),
    ]


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

    blender = resolve_blender(repo_root)
    completed = subprocess.run(
        blender_command(
            blender=blender,
            source_relative=source_relative,
            source_sha256=args.expected_source_sha256,
            output_relative=output_relative,
        ),
        cwd=repo_root,
        check=False,
        text=True,
    )
    if completed.returncode != 0:
        raise RuntimeError(
            "Pinned Blender runtime packager failed with exit code "
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
                "tiers": {
                    tier: report["tiers"][tier]["glb"]["path"]
                    for tier in ("source-2k", "runtime-1k", "comparison-512")
                },
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
