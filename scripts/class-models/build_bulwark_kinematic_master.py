#!/usr/bin/env python3
"""Build the deterministic Bulwark Mobile/Shell/Tower kinematic master.

This launcher is intentionally provider-free.  It verifies the approved art
references, resolves the repository-pinned Blender 5.2 executable through the
shared worktree root, and delegates the actual scene construction to the
adjacent Blender-side script.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
import sys
from pathlib import Path


EXPECTED_BLENDER = (5, 2)
REPORT_SCHEMA = "nilbots-bulwark-kinematic-master-1"
PINNED_BLENDER_RELATIVE_CANDIDATES = (
    Path("var/toolchains/Blender-5.2.0.app/Contents/MacOS/Blender"),
    Path("var/toolchains/blender-5.2.0/blender"),
)
AUTHORITY_FILES = {
    Path(
        "art/class-models/concept-targets/"
        "aegis-tortoise-3d-concept-v2/approval-sheet.png"
    ): "d1acf3d6af2ed71c64146a843f7564c99ede0b9384b26c28515218f4f57c3be7",
    Path(
        "art/class-models/concept-targets/"
        "aegis-tortoise-shell-3d-concept-v1/approval-sheet.png"
    ): "2d77b5ae7e6a96714164fc4f417d4c0cc0b4a23f13851ce4dad34208fe19f867",
    Path(
        "art/class-models/concept-targets/"
        "aegis-tortoise-turret-3d-concept-v1/approval-sheet.png"
    ): "b3df8d605c734c7a078d63dd6d7b98848337c67755d4205b427ecd6c27cf4b0f",
    Path(
        "art/class-models/masters/"
        "aegis-tortoise-mobile/aegis-tortoise-mobile.blend"
    ): "be2ccebf9fc2fc5165e3a6297b5668eb2c1ee78b713e926e765ca9df9bc68027",
    Path(
        "art/class-models/masters/"
        "aegis-tortoise-mobile/proofs/neutral-top.png"
    ): "e85c841d21ac09bc4bb4c1f0f463328f3443c4dfaa285a339781f7e2fefd0e20",
}


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Build an untextured deterministic Bulwark family master with "
            "rigid Mobile/Shell/Tower mechanisms and review evidence."
        )
    )
    parser.add_argument(
        "--output-dir",
        required=True,
        type=Path,
        help=(
            "New repository-relative output directory below sandbox/ or var/. "
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


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def verify_authorities(repo_root: Path) -> dict[str, str]:
    verified: dict[str, str] = {}
    for relative, expected in AUTHORITY_FILES.items():
        source = (repo_root / relative).resolve()
        if not source.is_file():
            raise ValueError(f"Missing approved authority: {relative}")
        actual = sha256(source)
        if actual != expected:
            raise ValueError(
                f"Authority hash drift for {relative}: {actual} != {expected}"
            )
        verified[relative.as_posix()] = actual
    return verified


def resolve_output(repo_root: Path, value: Path) -> tuple[Path, Path]:
    if value.is_absolute():
        raise ValueError("--output-dir must be repository-relative")
    resolved = (repo_root / value).resolve()
    try:
        relative = resolved.relative_to(repo_root)
    except ValueError as error:
        raise ValueError("--output-dir must stay inside the repository") from error
    if not relative.parts or relative.parts[0] not in {"sandbox", "var"}:
        raise ValueError("--output-dir must be below repository sandbox/ or var/")
    if resolved.exists():
        raise ValueError(
            f"Output directory already exists; refusing overwrite: {relative}"
        )
    return resolved, relative


def validate_report(
    report_path: Path,
    *,
    output_relative: Path,
    authorities: dict[str, str],
) -> dict[str, object]:
    if not report_path.is_file():
        raise RuntimeError("Blender did not produce validation-report.json")
    report = json.loads(report_path.read_text(encoding="utf-8"))
    if report.get("schema") != REPORT_SCHEMA:
        raise RuntimeError(f"Unexpected report schema: {report.get('schema')!r}")
    if report.get("status") != "PASS":
        raise RuntimeError(
            "Kinematic build failed validation: "
            + json.dumps(report.get("failures", []), sort_keys=True)
        )
    if report.get("outputDirectory") != output_relative.as_posix():
        raise RuntimeError("Report output directory does not match requested path")
    if report.get("authorities") != authorities:
        raise RuntimeError("Report authority ledger does not match launcher ledger")
    expected_files = (
        "aegis-tortoise-kinematic-master.blend",
        "aegis-tortoise-neutral-animated.glb",
        "aegis-tortoise-texture-upload.glb",
        "proofs/mobile-top.png",
        "proofs/mobile-side.png",
        "proofs/mobile-gameplay.png",
        "proofs/team-accent-neutral-gameplay.png",
        "proofs/team-accent-blue-gameplay.png",
        "proofs/team-accent-orange-gameplay.png",
        "proofs/team-accent-coverage-mask.png",
        "proofs/shell-deploy-contact-sheet.png",
        "proofs/tower-deploy-contact-sheet.png",
        "proofs/podplant-contact-sheet.png",
        "proofs/tower-recoil-contact-sheet.png",
        "proofs/shell-deploy-41px-contact-sheet.png",
        "proofs/tower-deploy-41px-contact-sheet.png",
        "proofs/mobile-recoil-41px-contact-sheet.png",
        "proofs/shell-recoil-41px-contact-sheet.png",
        "proofs/tower-recoil-41px-contact-sheet.png",
        "proofs/mobile-recoil-loop.gif",
        "proofs/shell-recoil-loop.gif",
        "proofs/tower-recoil-loop.gif",
        "proofs/shell-deploy-loop.gif",
        "proofs/tower-deploy-loop.gif",
        "proofs/uv-layout.svg",
    )
    for relative in expected_files:
        if not (report_path.parent / relative).is_file():
            raise RuntimeError(f"Missing expected build artifact: {relative}")
    return report


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    repo_root = repository_root()
    output, output_relative = resolve_output(repo_root, args.output_dir)
    authorities = verify_authorities(repo_root)
    blender = resolve_blender(repo_root)
    blender_script = Path(__file__).with_name(
        "build_bulwark_kinematic_master_blender.py"
    )
    if not blender_script.is_file():
        raise ValueError(f"Missing Blender-side builder: {blender_script}")

    command = [
        str(blender),
        "--background",
        "--factory-startup",
        "--python",
        str(blender_script),
        "--",
        "--repo-root",
        str(repo_root),
        "--output-dir",
        str(output),
        "--output-relative",
        output_relative.as_posix(),
        "--authorities-json",
        json.dumps(authorities, sort_keys=True, separators=(",", ":")),
    ]
    completed = subprocess.run(command, cwd=repo_root)
    if completed.returncode != 0:
        return completed.returncode
    report = validate_report(
        output / "validation-report.json",
        output_relative=output_relative,
        authorities=authorities,
    )
    result = {
        "status": report["status"],
        "outputDirectory": report["outputDirectory"],
        "blend": report["artifacts"]["blend"],
        "glb": report["artifacts"]["glb"],
        "triangles": report["geometry"]["triangles"],
        "animations": sorted(report["animations"]["semanticClips"]),
        "uvCoverage": report["uv"]["coverage"],
    }
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(1)
