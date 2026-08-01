#!/usr/bin/env python3
"""Reduce a completed Arc Relay sweep under frozen cohort-eligibility bars."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from statistics import mean, median
from typing import Any


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path}: JSON root must be an object")
    return value


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("attempt", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    attempt = args.attempt.resolve()
    run = read_json(attempt / "RUN.json")
    results = read_json(attempt / "results.json")
    manifest = read_json(Path(run["manifest"]))
    cells = {cell["cellId"]: cell for cell in manifest["cells"]}

    rows: list[dict[str, Any]] = []
    tripped_entrants: dict[str, list[dict[str, Any]]] = {}
    for result in results["cells"]:
        cell = cells[result["cellId"]]
        cell_dir = attempt / result["attempt"]
        card = read_json(cell_dir / "scorecard.json")
        eligibility = card["feltDegeneracy"]
        for team, entrant in (("0", cell["team0"]), ("1", cell["team1"])):
            if eligibility["cohortEligibilityByTeam"][team]:
                continue
            tripped_entrants.setdefault(entrant, []).append({
                "cellId": cell["cellId"],
                "teamId": int(team),
                "pingPongReversals": eligibility["handoffPingPong"][
                    "maxEpisodeReversalsByTeam"][team],
                "quietTicksInWindow": eligibility["sustainedPassivity"][
                    "maxQuietTicksInWindowByTeam"][team],
            })
        rows.append({"cell": cell, "scorecard": card, "result": result})

    retained = [
        row for row in rows
        if row["cell"]["team0"] not in tripped_entrants
        and row["cell"]["team1"] not in tripped_entrants
    ]
    first_pulse = [
        row for row in retained
        if row["scorecard"]["scoring"]["firstPulseTeamId"] is not None
    ]
    converted = sum(
        bool(row["scorecard"]["scoring"]["firstPulseConvertedToMatchWinner"])
        for row in first_pulse
    )
    end_ticks = [row["scorecard"]["outcome"]["endTick"] for row in retained]
    max_ticks = sum(
        row["scorecard"]["outcome"]["completionReason"] == "max-ticks"
        for row in retained
    )
    draws = sum(
        row["scorecard"]["outcome"]["winnerTeamId"] is None
        for row in retained
    )
    wins: dict[str, int] = {entrant: 0 for entrant in manifest["entrants"]}
    losses: dict[str, int] = {entrant: 0 for entrant in manifest["entrants"]}
    ties: dict[str, int] = {entrant: 0 for entrant in manifest["entrants"]}
    for row in retained:
        winner_team = row["scorecard"]["outcome"]["winnerTeamId"]
        if winner_team is None:
            ties[row["cell"]["team0"]] += 1
            ties[row["cell"]["team1"]] += 1
            continue
        winner = row["cell"][f"team{winner_team}"]
        loser = row["cell"][f"team{1 - winner_team}"]
        wins[winner] += 1
        losses[loser] += 1

    output = {
        "schema": "arc-relay-sweep-eligibility-read-v1",
        "sweepId": manifest.get("sweepId", manifest.get("goldenId")),
        "runtime": manifest.get("runtime", "wasm"),
        "loopProfile": manifest.get("loopProfile", "h0"),
        "plannedCells": len(rows),
        "ineligibleEntrants": tripped_entrants,
        "retainedEntrants": sorted(
            set(manifest["entrants"]) - set(tripped_entrants)),
        "retainedCells": len(retained),
        "excludedCells": len(rows) - len(retained),
        "firstPulse": {
            "matchesWithPulse": len(first_pulse),
            "convertedToWinner": converted,
            "conversionRate": converted / len(first_pulse) if first_pulse else None,
            "alertThreshold": 0.70,
            "alertTripped": (
                converted / len(first_pulse) >= 0.70 if first_pulse else None),
        },
        "pacing": {
            "endTickMin": min(end_ticks) if end_ticks else None,
            "endTickMedian": median(end_ticks) if end_ticks else None,
            "endTickMean": mean(end_ticks) if end_ticks else None,
            "endTickMax": max(end_ticks) if end_ticks else None,
            "maxTicksMatches": max_ticks,
            "maxTicksRate": max_ticks / len(retained) if retained else None,
            "drawMatches": draws,
            "drawRate": draws / len(retained) if retained else None,
        },
        "records": {
            entrant: {
                "wins": wins[entrant],
                "draws": ties[entrant],
                "losses": losses[entrant],
            }
            for entrant in sorted(manifest["entrants"])
        },
        "retainedCellIds": [row["cell"]["cellId"] for row in retained],
    }
    encoded = json.dumps(output, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
        print(args.output)
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
