#!/usr/bin/env python3
"""Create, verify, and consume Nilbots Balance Lab holdout commitments."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import secrets
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parent.parent
PROTOCOL = "sha256-commit-reveal-v1"
SCHEMA_VERSION = 1


def _canonical_bytes(value: Any) -> bytes:
    return (
        json.dumps(
            value,
            ensure_ascii=False,
            separators=(",", ":"),
            sort_keys=True,
        )
        + "\n"
    ).encode("utf-8")


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _load_object(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path} must contain a JSON object")
    return value


def _validate_reveal(value: dict[str, Any]) -> dict[str, Any]:
    required = {
        "schemaVersion",
        "protocol",
        "experimentId",
        "nonce",
        "seeds",
    }
    if set(value) != required:
        raise ValueError(
            "holdout reveal fields must be exactly "
            + ", ".join(sorted(required))
        )
    if value.get("schemaVersion") != SCHEMA_VERSION:
        raise ValueError("holdout reveal schemaVersion must be 1")
    if value.get("protocol") != PROTOCOL:
        raise ValueError(f"holdout reveal protocol must be {PROTOCOL}")
    experiment_id = value.get("experimentId")
    if not isinstance(experiment_id, str) or not experiment_id:
        raise ValueError("holdout reveal experimentId must be non-empty")
    nonce = value.get("nonce")
    if (
        not isinstance(nonce, str)
        or len(nonce) < 64
        or any(character not in "0123456789abcdef" for character in nonce)
    ):
        raise ValueError(
            "holdout reveal nonce must contain at least 256 bits of "
            "lowercase hexadecimal entropy"
        )
    seeds = value.get("seeds")
    if (
        not isinstance(seeds, list)
        or not seeds
        or len(set(seeds)) != len(seeds)
        or any(
            not isinstance(seed, int)
            or isinstance(seed, bool)
            or seed < 0
            for seed in seeds
        )
    ):
        raise ValueError(
            "holdout reveal seeds must be distinct non-negative integers"
        )
    return value


def _validate_commitment(value: dict[str, Any]) -> dict[str, Any]:
    required = {
        "schemaVersion",
        "protocol",
        "experimentId",
        "seedCount",
        "commitmentSha256",
    }
    if set(value) != required:
        raise ValueError(
            "holdout commitment fields must be exactly "
            + ", ".join(sorted(required))
        )
    if value.get("schemaVersion") != SCHEMA_VERSION:
        raise ValueError("holdout commitment schemaVersion must be 1")
    if value.get("protocol") != PROTOCOL:
        raise ValueError(f"holdout commitment protocol must be {PROTOCOL}")
    digest = value.get("commitmentSha256")
    if (
        not isinstance(digest, str)
        or len(digest) != 64
        or any(character not in "0123456789abcdef" for character in digest)
    ):
        raise ValueError(
            "holdout commitment commitmentSha256 must be lowercase SHA-256"
        )
    seed_count = value.get("seedCount")
    if (
        not isinstance(seed_count, int)
        or isinstance(seed_count, bool)
        or seed_count <= 0
    ):
        raise ValueError("holdout commitment seedCount must be positive")
    return value


def commitment_for(reveal: dict[str, Any]) -> dict[str, Any]:
    reveal = _validate_reveal(reveal)
    return {
        "schemaVersion": SCHEMA_VERSION,
        "protocol": PROTOCOL,
        "experimentId": reveal["experimentId"],
        "seedCount": len(reveal["seeds"]),
        "commitmentSha256": _sha256_bytes(_canonical_bytes(reveal)),
    }


def verify(
    commitment_path: Path,
    reveal_path: Path,
) -> tuple[dict[str, Any], dict[str, Any]]:
    commitment = _validate_commitment(_load_object(commitment_path))
    reveal = _validate_reveal(_load_object(reveal_path))
    expected = commitment_for(reveal)
    if commitment != expected:
        raise ValueError(
            "holdout reveal does not match its experiment, count, or digest"
        )
    return commitment, reveal


def create(
    experiment_id: str,
    count: int,
    private_path: Path,
    commitment_path: Path,
    excluded: set[int],
) -> dict[str, Any]:
    if not experiment_id:
        raise ValueError("experiment id must be non-empty")
    if count <= 0:
        raise ValueError("count must be positive")
    if private_path.resolve().is_relative_to(ROOT):
        raise ValueError(
            "private holdout reveal must live outside the repository/"
            "authoring workspace"
        )
    if private_path.exists() or commitment_path.exists():
        raise ValueError("holdout output paths must not already exist")
    seeds: set[int] = set()
    while len(seeds) < count:
        candidate = secrets.randbits(63)
        if candidate not in excluded:
            seeds.add(candidate)
    reveal = {
        "schemaVersion": SCHEMA_VERSION,
        "protocol": PROTOCOL,
        "experimentId": experiment_id,
        "nonce": secrets.token_hex(32),
        "seeds": sorted(seeds),
    }
    commitment = commitment_for(reveal)
    private_path.parent.mkdir(parents=True, exist_ok=True)
    commitment_path.parent.mkdir(parents=True, exist_ok=True)
    descriptor = os.open(
        private_path,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL,
        0o600,
    )
    with os.fdopen(descriptor, "wb") as stream:
        stream.write(_canonical_bytes(reveal))
    commitment_path.write_bytes(_canonical_bytes(commitment))
    return commitment


def consume(
    commitment_path: Path,
    reveal_path: Path,
    consumption_directory: Path,
) -> Path:
    commitment, reveal = verify(commitment_path, reveal_path)
    consumption_directory.mkdir(parents=True, exist_ok=True)
    marker = (
        consumption_directory
        / f"{commitment['commitmentSha256']}.consumed.json"
    )
    record = {
        "schemaVersion": SCHEMA_VERSION,
        "protocol": PROTOCOL,
        "experimentId": commitment["experimentId"],
        "commitmentSha256": commitment["commitmentSha256"],
        "seedCount": commitment["seedCount"],
        "revealSha256": _sha256_bytes(_canonical_bytes(reveal)),
        "state": "consumed",
    }
    descriptor = os.open(
        marker,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL,
        0o600,
    )
    with os.fdopen(descriptor, "wb") as stream:
        stream.write(_canonical_bytes(record))
    return marker


def _parse_excluded(value: str) -> set[int]:
    if not value:
        return set()
    try:
        seeds = {int(item) for item in value.split(",")}
    except ValueError as error:
        raise ValueError("--exclude must be comma-separated integers") from error
    if any(seed < 0 for seed in seeds):
        raise ValueError("--exclude seeds must be non-negative")
    return seeds


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    create_parser = subparsers.add_parser("create")
    create_parser.add_argument("--experiment", required=True)
    create_parser.add_argument("--count", required=True, type=int)
    create_parser.add_argument("--private", required=True, type=Path)
    create_parser.add_argument("--commitment", required=True, type=Path)
    create_parser.add_argument("--exclude", default="")

    verify_parser = subparsers.add_parser("verify")
    verify_parser.add_argument("--commitment", required=True, type=Path)
    verify_parser.add_argument("--reveal", required=True, type=Path)

    consume_parser = subparsers.add_parser("consume")
    consume_parser.add_argument("--commitment", required=True, type=Path)
    consume_parser.add_argument("--reveal", required=True, type=Path)
    consume_parser.add_argument("--consumption-dir", required=True, type=Path)

    args = parser.parse_args(argv)
    if args.command == "create":
        commitment = create(
            args.experiment,
            args.count,
            args.private.resolve(),
            args.commitment.resolve(),
            _parse_excluded(args.exclude),
        )
        print(json.dumps(commitment, sort_keys=True))
        return 0
    if args.command == "verify":
        commitment, _ = verify(
            args.commitment.resolve(),
            args.reveal.resolve(),
        )
        print(
            f"{commitment['experimentId']}: "
            f"{commitment['seedCount']} holdout seeds verified"
        )
        return 0
    marker = consume(
        args.commitment.resolve(),
        args.reveal.resolve(),
        args.consumption_dir.resolve(),
    )
    print(f"holdout consumed: {marker}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
