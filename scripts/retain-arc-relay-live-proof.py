#!/usr/bin/env python3
"""Retain compact, gallery-ready evidence from eligible live operation proofs."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
from typing import Any


REPO = Path(__file__).resolve().parent.parent
EXPECTED_BARS_SCHEMA = "arc-relay-felt-degeneracy-bars-v4"
MATCH_BROADCAST_LIMIT = 300 * 1024
GALLERY_BROADCAST_LIMIT = 8 * 1024 * 1024


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path}: expected object")
    return value


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def relative(path: Path) -> str:
    return str(path.resolve().relative_to(REPO))


def compact_operation(
    operation: dict[str, Any], proof_root: Path
) -> dict[str, Any]:
    if not operation.get("passed"):
        raise ValueError(f"{operation['id']}: operation proof failed")
    if operation.get("feltDegeneracyBarsSchema") != EXPECTED_BARS_SCHEMA:
        raise ValueError(
            f"{operation['id']}: proof did not use {EXPECTED_BARS_SCHEMA}"
        )
    if not operation.get("matchEligibleForCohortRead"):
        raise ValueError(f"{operation['id']}: whole match is not eligible")
    eligibility = operation.get("cohortEligibilityByTeam", {})
    if eligibility != {"0": True, "1": True}:
        raise ValueError(
            f"{operation['id']}: both teams must pass eligibility: {eligibility}"
        )
    proof = operation["proof"]
    record = read_json(proof_root / operation["matchRecord"])
    result = record["result"]
    return {
        "id": operation["id"],
        "seed": operation["seed"],
        "replayHash": operation["replayHash"],
        "opponentArtifactHash": operation["opponentArtifactHash"],
        "sheetHash": operation["sheetHash"],
        "opponentSheetHash": operation["baselineSheetHash"],
        "branch": proof["branch"],
        "prepareTick": proof["prepareTick"],
        "commitTick": proof["commitTick"],
        "successTick": proof["successTick"],
        "releaseTick": proof["releaseTick"],
        "requiredActions": proof["requiredActionTicks"],
        "baselineRoleTagsAfterRelease": proof["baselineRoleTags"],
        "result": {
            "winnerTeamId": result["winnerTeamId"],
            "reason": result["reason"],
            "endTick": result["endTick"],
        },
        "feltDegeneracyBarsSchema": operation["feltDegeneracyBarsSchema"],
        "cohortEligibilityByTeam": eligibility,
        "matchEligibleForCohortRead": True,
        "broadcastBytes": operation["broadcastBytes"],
        "scorecardHash": operation["scorecardHash"],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", required=True, type=Path)
    parser.add_argument("--artifact", required=True, type=Path)
    parser.add_argument("--proof", required=True, action="append", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    artifact_hash = sha256(args.artifact)
    campaigns: list[dict[str, Any]] = []
    review_operations: list[dict[str, Any]] | None = None
    review_root: Path | None = None
    expected_ids: list[str] | None = None

    for proof_path in args.proof:
        receipt = read_json(proof_path)
        if receipt.get("artifactHash") != artifact_hash:
            raise ValueError(f"{proof_path}: artifact hash mismatch")
        if not receipt.get("passed"):
            raise ValueError(f"{proof_path}: campaign did not pass")
        operations = [
            compact_operation(value, proof_path.resolve().parent)
            for value in receipt["operations"]
        ]
        ids = [value["id"] for value in operations]
        if expected_ids is None:
            expected_ids = ids
            review_operations = operations
            review_root = proof_path.resolve().parent
        elif ids != expected_ids:
            raise ValueError(f"{proof_path}: operation population changed")
        campaigns.append({
            "proofSha256": sha256(proof_path),
            "passed": True,
            "passedCount": len(operations),
            "requiredCount": len(operations),
            "seeds": sorted({value["seed"] for value in operations}),
            "operations": operations,
        })

    assert review_operations is not None and review_root is not None
    output = args.output.resolve()
    replay_output = output / "replays"
    replay_output.mkdir(parents=True, exist_ok=True)
    gallery_bytes = 0
    for operation in review_operations:
        source = review_root / operation["id"] / "broadcast.json.gz"
        target = replay_output / f"{operation['id']}.broadcast.json.gz"
        shutil.copyfile(source, target)
        size = target.stat().st_size
        if size != operation["broadcastBytes"]:
            raise ValueError(f"{operation['id']}: broadcast size moved")
        if size > MATCH_BROADCAST_LIMIT:
            raise ValueError(f"{operation['id']}: broadcast exceeds match budget")
        gallery_bytes += size
        operation["broadcast"] = {
            "file": relative(target),
            "gzipBytes": size,
            "sha256": sha256(target),
        }
    if gallery_bytes > GALLERY_BROADCAST_LIMIT:
        raise ValueError("retained broadcasts exceed gallery budget")

    summary = {
        "schema": "arc-relay-intelligent-operation-live-proof-summary-v2",
        "catalog": relative(args.catalog),
        "catalogSha256": sha256(args.catalog),
        "artifact": relative(args.artifact),
        "artifactSha256": artifact_hash,
        "runtime": "wasm",
        "feltDegeneracyBarsSchema": EXPECTED_BARS_SCHEMA,
        "passed": True,
        "passedCount": sum(value["passedCount"] for value in campaigns),
        "requiredCount": sum(value["requiredCount"] for value in campaigns),
        "campaigns": campaigns,
        "reviewOperations": review_operations,
        "budgets": {
            "broadcastGzipBytesPerMatch": MATCH_BROADCAST_LIMIT,
            "galleryBroadcastGzipBytes": GALLERY_BROADCAST_LIMIT,
            "actualGalleryBroadcastGzipBytes": gallery_bytes,
        },
    }
    output.mkdir(parents=True, exist_ok=True)
    (output / "live-proof-summary.json").write_text(
        json.dumps(summary, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(
        f"retained {len(review_operations)} gallery matches and "
        f"{len(campaigns)} passing campaigns ({gallery_bytes} B)"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
