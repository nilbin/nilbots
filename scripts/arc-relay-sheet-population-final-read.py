#!/usr/bin/env python3
"""Combine a frozen population screen with one registered entrant repair."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from statistics import mean, median
from typing import Any


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path}: JSON root must be an object")
    return value


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_rows(attempt: Path) -> tuple[dict[str, Any], dict[str, dict[str, Any]]]:
    run = read_json(attempt / "RUN.json")
    manifest = read_json(Path(run["manifest"]))
    results = read_json(attempt / "results.json")
    cells = {cell["cellId"]: cell for cell in manifest["cells"]}
    rows: dict[str, dict[str, Any]] = {}
    for result in results["cells"]:
        cell_id = result["cellId"]
        card = read_json(attempt / result["attempt"] / "scorecard.json")
        rows[cell_id] = {"cell": cells[cell_id], "scorecard": card}
    if len(rows) != len(cells):
        raise ValueError(f"{attempt}: incomplete result set")
    return {
        "sweepId": manifest["sweepId"],
        "runtime": manifest.get("runtime", "wasm"),
        "planSha256": sha256(Path(run["manifest"])),
        "resultsSha256": sha256(attempt / "results.json"),
        "cells": len(rows),
    }, rows


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-attempt", required=True, type=Path)
    parser.add_argument("--repair-attempt", required=True, type=Path)
    parser.add_argument("--replace-entrant", required=True)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    base_meta, base_rows = load_rows(args.base_attempt.resolve())
    repair_meta, repair_rows = load_rows(args.repair_attempt.resolve())
    expected = {
        cell_id for cell_id, row in base_rows.items()
        if args.replace_entrant in (
            row["cell"]["team0"], row["cell"]["team1"]
        )
    }
    if set(repair_rows) != expected:
        raise ValueError(
            "repair cells must exactly replace every base cell for entrant; "
            f"expected {sorted(expected)}, got {sorted(repair_rows)}"
        )
    rows = {**base_rows, **repair_rows}
    entrant_ids = sorted({
        entrant
        for row in rows.values()
        for entrant in (row["cell"]["team0"], row["cell"]["team1"])
    })

    ineligible: dict[str, list[dict[str, Any]]] = {}
    records = {
        entrant: {"wins": 0, "draws": 0, "losses": 0}
        for entrant in entrant_ids
    }
    pair_records: dict[tuple[str, str], dict[str, Any]] = {}
    first_pulses = 0
    first_pulse_wins = 0
    end_ticks: list[int] = []
    max_ticks = 0
    draws = 0
    for cell_id, row in sorted(rows.items()):
        cell = row["cell"]
        card = row["scorecard"]
        eligibility = card["feltDegeneracy"]["cohortEligibilityByTeam"]
        for team, entrant in (("0", cell["team0"]), ("1", cell["team1"])):
            if not eligibility[team]:
                ineligible.setdefault(entrant, []).append({
                    "cellId": cell_id,
                    "teamId": int(team),
                })
        scoring = card["scoring"]
        if scoring["firstPulseTeamId"] is not None:
            first_pulses += 1
            first_pulse_wins += int(bool(
                scoring["firstPulseConvertedToMatchWinner"]
            ))
        outcome = card["outcome"]
        end_ticks.append(outcome["endTick"])
        max_ticks += int(outcome["completionReason"] == "max-ticks")
        pair = tuple(sorted((cell["team0"], cell["team1"])))
        pair_row = pair_records.setdefault(pair, {
            "entrants": list(pair),
            "games": 0,
            "wins": {pair[0]: 0, pair[1]: 0},
            "draws": 0,
        })
        pair_row["games"] += 1
        winner_team = outcome["winnerTeamId"]
        if winner_team is None:
            draws += 1
            records[cell["team0"]]["draws"] += 1
            records[cell["team1"]]["draws"] += 1
            pair_row["draws"] += 1
        else:
            winner = cell[f"team{winner_team}"]
            loser = cell[f"team{1 - winner_team}"]
            records[winner]["wins"] += 1
            records[loser]["losses"] += 1
            pair_row["wins"][winner] += 1

    if ineligible:
        raise ValueError(f"final population includes ineligible entrants: {ineligible}")
    decided = len(rows) - draws
    leader_wins = max(record["wins"] for record in records.values())
    leaders = sorted(
        entrant for entrant, record in records.items()
        if record["wins"] == leader_wins
    )
    winning_sheets = sum(record["wins"] > 0 for record in records.values())
    zero_win = sorted(
        entrant for entrant, record in records.items()
        if record["wins"] == 0
    )
    mirrored_splits = []
    two_game_sweeps = []
    for pair in sorted(pair_records):
        pair_row = pair_records[pair]
        if pair_row["games"] != 2:
            raise ValueError(f"pair {pair} does not have two assignments")
        wins = pair_row["wins"]
        if pair_row["draws"] == 0 and sorted(wins.values()) == [1, 1]:
            mirrored_splits.append(list(pair))
        if pair_row["draws"] == 0 and 2 in wins.values():
            winner = next(entrant for entrant, count in wins.items() if count == 2)
            loser = pair[1] if winner == pair[0] else pair[0]
            two_game_sweeps.append({"winner": winner, "loser": loser})
    sweep_winners = {row["winner"] for row in two_game_sweeps}
    sweep_losers = {row["loser"] for row in two_game_sweeps}

    draw_rate = draws / len(rows)
    leader_share = leader_wins / decided if decided else None
    pulse_rate = first_pulse_wins / first_pulses if first_pulses else None
    guardrails = {
        "drawRateAtMost10Percent": draw_rate <= 0.10,
        "atLeast12WinningSheets": winning_sheets >= 12,
        "leaderShareOfDecidedWinsAtMost15Percent": (
            leader_share is not None and leader_share <= 0.15
        ),
        "noZeroWinSheet": not zero_win,
    }
    output = {
        "schema": "arc-relay-sheet-population-final-read-v1",
        "classification": (
            "provisional shared-mind evaluation-sheet screen; not product "
            "balance or human-fun authority"
        ),
        "sourceSweeps": {
            "base": base_meta,
            "registeredRepair": repair_meta,
        },
        "replacement": {
            "entrantId": args.replace_entrant,
            "supersededCells": sorted(expected),
        },
        "runtime": "wasm",
        "matches": len(rows),
        "entrants": len(entrant_ids),
        "eligibility": {
            "ineligibleEntrants": ineligible,
            "eligibleMatches": len(rows),
        },
        "balance": {
            "records": records,
            "draws": draws,
            "drawRate": draw_rate,
            "decidedMatches": decided,
            "winningSheets": winning_sheets,
            "zeroWinSheets": zero_win,
            "leaders": leaders,
            "leaderWins": leader_wins,
            "leaderShareOfDecidedWins": leader_share,
            "guardrails": guardrails,
            "allRegisteredGuardrailsPass": all(guardrails.values()),
        },
        "counterWeb": {
            "pairs": len(pair_records),
            "mirroredSplitPairs": len(mirrored_splits),
            "twoGameSweeps": len(two_game_sweeps),
            "entrantsWithSweepWin": len(sweep_winners),
            "entrantsWithSweepLoss": len(sweep_losers),
            "entrantsWithBothSweepWinAndLoss": len(
                sweep_winners & sweep_losers
            ),
            "mirroredSplits": mirrored_splits,
            "sweeps": two_game_sweeps,
        },
        "firstPulse": {
            "matchesWithPulse": first_pulses,
            "convertedToWinner": first_pulse_wins,
            "conversionRate": pulse_rate,
            "alertThreshold": 0.70,
            "alertTripped": pulse_rate is not None and pulse_rate > 0.70,
        },
        "pacing": {
            "endTickMin": min(end_ticks),
            "endTickMedian": median(end_ticks),
            "endTickMean": mean(end_ticks),
            "endTickMax": max(end_ticks),
            "maxTicksMatches": max_ticks,
            "maxTicksRate": max_ticks / len(rows),
        },
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(output, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
