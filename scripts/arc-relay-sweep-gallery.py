#!/usr/bin/env python3
"""Materialize a frozen, eligibility-clean Arc Relay sweep as a blind gallery input."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path}: JSON root must be an object")
    return value


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("attempt", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    attempt = args.attempt.resolve()
    run = read_json(attempt / "RUN.json")
    results = read_json(attempt / "results.json")
    manifest_path = Path(run["manifest"])
    manifest = read_json(manifest_path)
    review = manifest.get("outcomeBlindReview")
    if not isinstance(review, dict) or not review.get("preparedBeforeOutcomes"):
        raise ValueError("sweep did not freeze an outcome-blind review before running")
    if review.get("containsOutcomesScoresOrDurations") is not False:
        raise ValueError("blind review declaration is missing its outcome-free boundary")
    if not results.get("allCellsEligibleForCohortRead"):
        raise ValueError("refusing gallery: at least one cell failed cohort eligibility")

    cells = {cell["cellId"]: cell for cell in manifest["cells"]}
    completed = {cell["cellId"]: cell for cell in results["cells"]}
    replays: list[dict[str, Any]] = []
    for frozen in review["cells"]:
        cell_id = frozen["cellId"]
        cell = cells[cell_id]
        result = completed.get(cell_id)
        if result is None or not result.get("matchEligibleForCohortRead"):
            raise ValueError(f"refusing gallery: {cell_id} is incomplete or ineligible")
        source = attempt / result["attempt"] / "broadcast.json.gz"
        if not source.is_file():
            raise FileNotFoundError(source)
        if source.stat().st_size > 300 * 1024:
            raise ValueError(f"{cell_id}: broadcast exceeds 300 KiB")
        replays.append({
            "reviewId": frozen["reviewId"],
            "matchId": cell_id,
            "rules": f"{cell['team0']} vs {cell['team1']}",
            "map": f"{manifest['loopProfile']} assignment {cell_id.rsplit('--a', 1)[-1]}",
            "source": str(source.resolve()),
        })

    gallery = {
        "schema": "arc-relay-sweep-outcome-blind-gallery-input-v1",
        "sweepId": manifest["sweepId"],
        "runtime": manifest["runtime"],
        "loopProfile": manifest["loopProfile"],
        "frozenPlan": str(manifest_path.resolve()),
        "preparedBeforeOutcomes": True,
        "containsOutcomesScoresOrDurations": False,
        "allCellsPassedCohortEligibility": True,
        "replays": replays,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(gallery, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(f"wrote {len(replays)} eligible blind reviews to {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
