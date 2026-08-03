#!/usr/bin/env python3
"""Freeze the complete local Home Siege screening ledger into compact JSON."""

from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path
from typing import Any


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    attempts: list[dict[str, Any]] = []
    for path in sorted(args.root.rglob("screen.json")):
        document = json.loads(path.read_text(encoding="utf-8"))
        participants = document.get("Participants", [])
        subject = next(
            (
                value for value in participants
                if value.get("Name") == "ArcRelayStandingStrategyMindV5"
            ),
            None,
        )
        if subject is None:
            continue
        result = document["Result"]
        subject_team = int(subject["TeamId"])
        eligible = subject_team in result.get("EligibleTeamIds", [])
        winner = result.get("WinnerTeamId")
        outcome = (
            "fault"
            if not eligible
            else "draw"
            if winner is None
            else "win"
            if int(winner) == subject_team
            else "loss"
        )
        attempts.append({
            "attempt": str(path.parent.relative_to(args.root)),
            "runtime": document.get("Runtime"),
            "rulesetId": document.get("RulesetId"),
            "seed": document.get("Seed"),
            "subjectTeamId": subject_team,
            "sheetSha256": subject.get("SheetHash"),
            "outcome": outcome,
            "winnerTeamId": winner,
            "reason": result.get("Reason"),
            "endTick": result.get("EndTick"),
            "bothSidesEligible": len(result.get("EligibleTeamIds", [])) == 2,
        })
    counts = Counter(value["outcome"] for value in attempts)
    rules = Counter(value["rulesetId"] for value in attempts)
    output = {
        "schema": "arc-relay-home-siege-screen-ledger-v1",
        "claimBoundary": (
            "Discovery-only in-process screens. None is a balance claim; "
            "the separately frozen final cohort is the WASM evidence."
        ),
        "summary": {
            "attempts": len(attempts),
            "outcomes": dict(sorted(counts.items())),
            "rulesetCells": dict(sorted(rules.items())),
        },
        "attempts": attempts,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(output, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(output["summary"], sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
