#!/usr/bin/env python3
"""Bind the retained 32-sheet Counterflow corpus to the repaired strategy mind."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


REPO = Path(__file__).resolve().parent.parent
SOURCE = (
    REPO
    / "arena-bots/arc-relay/depth-map-v1-2026-08-02/counterflow/cohort.json"
)
OUTPUT = (
    REPO
    / "arena-bots/arc-relay/forward-combat-cohort-v14-2026-08-03/cohort.json"
)
ARTIFACT = (
    REPO / "arena-bots/arc-relay/stock-mind-v4/bot.wasm"
)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def document() -> dict:
    source = json.loads(SOURCE.read_text(encoding="utf-8"))
    root = OUTPUT.parent
    artifact = Path("../stock-mind-v4/bot.wasm")
    entrants = []
    for item in source["entrants"]:
        source_sheet = (SOURCE.parent / item["sheet"]).resolve()
        sheet = Path(
            "../depth-map-v1-2026-08-02/counterflow/sheets"
        ) / source_sheet.name
        entrants.append(
            {
                "artifact": str(artifact),
                "artifactSha256": sha256(ARTIFACT),
                "entrantId": item["entrantId"],
                "sheet": str(sheet),
                "sheetSha256": sha256((root / sheet).resolve()),
            }
        )
    return {
        "cohortId": "arc-relay-forward-combat-strategy-mind-v14",
        "eligibilityBars": "../../../balance/arc-relay-felt-degeneracy-bars-v3.json",
        "forwardCombatStudy": {
            "classification": "representative shared-strategy-mind implementation gate",
            "loopProfile": "forward-combat",
            "repairChain": "v1 replaced the legacy product stock mind; v3-v6 repaired facing-locked return-lane preparation and self-blocking cover; v7 added monotonic return-lane handoffs; v8 added route/aim commitment; v9 added bounded carrier preemption; v10 aligned static distance; v11 made recovery persistent; v12 exposed a greedy equal-distance orbit; v13 committed reservation-aware recovery; v14 preserves route rotations while allowing facing-locked aim preparation after movement and retains the two-seed campaign sample",
            "sourceCohort": str(SOURCE.relative_to(REPO)),
            "sourceCohortSha256": sha256(SOURCE),
            "stockArtifact": str(ARTIFACT.relative_to(REPO)),
            "stockArtifactSha256": sha256(ARTIFACT),
        },
        "entrants": entrants,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    encoded = json.dumps(
        document(), ensure_ascii=False, indent=2, sort_keys=True
    ) + "\n"
    if args.check:
        if not OUTPUT.is_file() or OUTPUT.read_text(encoding="utf-8") != encoded:
            raise SystemExit("forward-combat cohort is stale")
        print(f"verified {OUTPUT.relative_to(REPO)}")
        return 0
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(encoded, encoding="utf-8")
    print(f"wrote {OUTPUT.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
