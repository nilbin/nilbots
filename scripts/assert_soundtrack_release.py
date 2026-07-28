#!/usr/bin/env python3
"""Fail production release while any publicly shipped soundtrack is unapproved."""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any
from urllib.parse import unquote


class ReleaseApprovalError(Exception):
    """A soundtrack catalog is unsafe to publish."""


def load_json_object(path: Path, label: str) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ReleaseApprovalError(f"{label} could not be read: {error}") from error
    if not isinstance(value, dict):
        raise ReleaseApprovalError(f"{label} must be a JSON object")
    return value


def resolve_manifest(catalog_path: Path, raw_path: object) -> Path:
    return resolve_relative_path(
        catalog_path.parent,
        raw_path,
        "catalog manifest",
    )


def resolve_relative_path(root_path: Path, raw_path: object, label: str) -> Path:
    if not isinstance(raw_path, str) or not raw_path:
        raise ReleaseApprovalError(f"{label} path must be a non-empty string")
    if (
        raw_path.startswith("/")
        or "\\" in raw_path
        or ":" in raw_path
        or "?" in raw_path
        or "#" in raw_path
    ):
        raise ReleaseApprovalError(
            f"{label} path is not a safe relative path: {raw_path!r}"
        )
    try:
        parts = [unquote(part) for part in raw_path.split("/")]
    except (UnicodeError, ValueError) as error:
        raise ReleaseApprovalError(
            f"{label} path is malformed: {raw_path!r}"
        ) from error
    if any(part in ("", ".", "..") for part in parts):
        raise ReleaseApprovalError(
            f"{label} path is not a safe relative path: {raw_path!r}"
        )

    root = root_path.resolve()
    candidate = (root / raw_path).resolve()
    try:
        candidate.relative_to(root)
    except ValueError as error:
        raise ReleaseApprovalError(
            f"{label} escapes its root: {raw_path!r}"
        ) from error
    return candidate


def release_blockers(
    catalog_path: Path,
    *,
    require_audition: bool = True,
) -> list[str]:
    catalog = load_json_object(catalog_path, "soundtrack catalog")
    if catalog.get("schemaVersion") != 1:
        raise ReleaseApprovalError("soundtrack catalog schemaVersion must be 1")
    tracks = catalog.get("tracks")
    if not isinstance(tracks, list):
        raise ReleaseApprovalError("soundtrack catalog tracks must be an array")

    blockers: list[str] = []
    seen_ids: set[str] = set()
    manifest_paths: set[Path] = set()
    declared_media: set[Path] = set()
    for index, track_value in enumerate(tracks):
        if not isinstance(track_value, dict):
            raise ReleaseApprovalError(f"catalog track {index} must be an object")
        track_id = track_value.get("id")
        if not isinstance(track_id, str) or not track_id:
            raise ReleaseApprovalError(
                f"catalog track {index} id must be a non-empty string"
            )
        if track_id in seen_ids:
            raise ReleaseApprovalError(f"duplicate catalog track id {track_id!r}")
        seen_ids.add(track_id)

        manifest_path = resolve_manifest(catalog_path, track_value.get("manifest"))
        manifest_paths.add(manifest_path)
        manifest = load_json_object(manifest_path, f"{track_id} manifest")
        if manifest.get("schemaVersion") != 1:
            raise ReleaseApprovalError(
                f"{track_id} manifest schemaVersion must be 1"
            )
        if manifest.get("id") != track_id:
            raise ReleaseApprovalError(
                f"{track_id} manifest id does not match its catalog entry"
            )
        blockers.extend(
            manifest_release_blockers(
                manifest,
                track_id,
                require_audition=require_audition,
            )
        )
        declared_media.update(manifest_media_paths(manifest_path, manifest, track_id))

    # Public directories can retain older content-addressed versions even after
    # the mutable catalog advances. They remain directly fetchable, so approval
    # must cover every shipped manifest rather than only the current entries.
    root = catalog_path.parent.resolve()
    all_manifest_paths = {
        path.resolve()
        for path in root.rglob("manifest.json")
        if path.is_file()
    }
    for manifest_path in sorted(all_manifest_paths - manifest_paths):
        relative = manifest_path.relative_to(root).as_posix()
        manifest = load_json_object(manifest_path, f"{relative} manifest")
        if manifest.get("schemaVersion") != 1:
            raise ReleaseApprovalError(
                f"{relative} manifest schemaVersion must be 1"
            )
        manifest_id = manifest.get("id")
        if not isinstance(manifest_id, str) or not manifest_id:
            raise ReleaseApprovalError(f"{relative} manifest id is malformed")
        blockers.extend(
            manifest_release_blockers(
                manifest,
                f"{manifest_id} ({relative})",
                require_audition=require_audition,
            )
        )
        declared_media.update(
            manifest_media_paths(
                manifest_path,
                manifest,
                f"{manifest_id} ({relative})",
            )
        )

    for media_path in sorted(
        path.resolve()
        for path in root.rglob("*")
        if path.is_file() and path.suffix.lower() in (".m4a", ".ogg")
    ):
        if media_path not in declared_media:
            relative = media_path.relative_to(root).as_posix()
            blockers.append(f"{relative}: encoded media is not declared by a manifest")
    return blockers


def manifest_release_blockers(
    manifest: dict[str, Any],
    label: str,
    *,
    require_audition: bool = True,
) -> list[str]:
    blockers: list[str] = []
    provenance = manifest.get("provenance")
    if not isinstance(provenance, dict):
        raise ReleaseApprovalError(f"{label} provenance must be an object")
    if provenance.get("rightsStatus") != "rights-cleared":
        blockers.append(f"{label}: source rights are not cleared")
    if provenance.get("shipApproval") != "approved":
        blockers.append(f"{label}: ship approval is not approved")

    sections = manifest.get("sections")
    if not isinstance(sections, list):
        raise ReleaseApprovalError(f"{label} sections must be an array")
    unauditioned = 0
    for section in sections:
        if not isinstance(section, dict):
            raise ReleaseApprovalError(f"{label} contains a malformed section")
        loop = section.get("loop")
        if loop is None:
            continue
        if not isinstance(loop, dict):
            raise ReleaseApprovalError(f"{label} contains a malformed loop review")
        if (
            loop.get("approvalStatus") != "auditioned"
            or loop.get("auditionRequired") is not False
        ):
            unauditioned += 1
    if unauditioned and require_audition:
        blockers.append(
            f"{label}: {unauditioned} loop(s) still require human audition"
        )
    return blockers


def manifest_media_paths(
    manifest_path: Path,
    manifest: dict[str, Any],
    label: str,
) -> set[Path]:
    assets = manifest.get("assets")
    if not isinstance(assets, dict):
        raise ReleaseApprovalError(f"{label} assets must be an object")
    media: set[Path] = set()
    for asset_path in assets:
        resolved = resolve_relative_path(
            manifest_path.parent,
            asset_path,
            f"{label} asset",
        )
        if resolved.suffix.lower() in (".m4a", ".ogg"):
            media.add(resolved)
    return media


def assert_soundtracks_shippable(
    catalog_path: Path,
    *,
    require_audition: bool = True,
) -> None:
    blockers = release_blockers(
        catalog_path,
        require_audition=require_audition,
    )
    if blockers:
        detail = "\n".join(f"- {blocker}" for blocker in blockers)
        guidance = (
            "Keep these assets local, or record cleared rights, approved shipment, "
            "and completed loop auditions before publishing."
            if require_audition
            else
            "Pilot publication still requires cleared rights, explicit ship approval, "
            "valid manifests, and fully declared media."
        )
        raise ReleaseApprovalError(
            "soundtrack release approval failed:\n"
            f"{detail}\n"
            f"{guidance}"
        )


def main(argv: list[str]) -> int:
    pilot = len(argv) == 3 and argv[1] == "--pilot"
    if not (len(argv) == 2 or pilot):
        print(
            "usage: python3 scripts/assert_soundtrack_release.py [--pilot] "
            "web/public/soundtracks/index.json",
            file=sys.stderr,
        )
        return 2
    catalog_path = Path(argv[2] if pilot else argv[1])
    try:
        assert_soundtracks_shippable(
            catalog_path,
            require_audition=not pilot,
        )
    except ReleaseApprovalError as error:
        print(error, file=sys.stderr)
        return 1
    if pilot:
        audition_warnings = [
            blocker
            for blocker in release_blockers(catalog_path)
            if "still require human audition" in blocker
        ]
        if audition_warnings:
            detail = "\n".join(f"- {warning}" for warning in audition_warnings)
            print(
                "Pilot soundtrack warning:\n"
                f"{detail}\n"
                "Complete human loop auditions before a production-tier release.",
                file=sys.stderr,
            )
        print(
            "Pilot soundtrack gate passed: rights, ship approval, manifests, "
            "and declared media are valid."
        )
        return 0
    print("All public soundtracks are rights-cleared, auditioned, and approved.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
