#!/usr/bin/env python3
"""Summarize doctrine shape from an eligibility-clean Arc Relay sweep."""

from __future__ import annotations

import argparse
from collections import defaultdict
import json
import math
from pathlib import Path
from statistics import mean
from typing import Any


def team_value(container: dict[str, Any], team: str, default: Any = 0) -> Any:
    return container.get(team, container.get(str(int(team)), default))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("attempt", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    scorecards = sorted(args.attempt.resolve().glob("cells/**/scorecard.json"))
    if not scorecards:
        raise FileNotFoundError("completed sweep contains no scorecards")

    totals: dict[str, dict[str, Any]] = defaultdict(lambda: {
        "games": 0,
        "wins": 0,
        "draws": 0,
        "losses": 0,
        "deliveries": 0,
        "pulses": 0,
        "steals": 0,
        "possessionTicks": 0,
        "crossTheaterTransitions": 0,
        "allocationEntropy": [],
        "escortDensity": [],
        "quietTicks": 0,
        "highWaitTicks": 0,
        "signatureAttempts": defaultdict(int),
    })
    for path in scorecards:
        card = json.loads(path.read_text(encoding="utf-8"))
        winner = card["outcome"]["winnerTeamId"]
        for team, entrant in card["identity"]["entrantsByTeam"].items():
            entry = totals[entrant]
            entry["games"] += 1
            if winner is None:
                entry["draws"] += 1
            elif int(team) == winner:
                entry["wins"] += 1
            else:
                entry["losses"] += 1
            entry["deliveries"] += team_value(
                card["scoring"]["deliveriesByTeam"], team)
            entry["pulses"] += team_value(
                card["scoring"]["pulsesByTeam"], team)
            entry["steals"] += team_value(
                card["possession"]["stealsByTeam"], team)
            entry["possessionTicks"] += team_value(
                card["possession"]["ticksByTeam"], team)
            field = team_value(card["fieldShape"]["theatersByTeam"], team, {})
            entry["crossTheaterTransitions"] += field[
                "crossTheaterTransitions"]
            entry["allocationEntropy"].append(
                field["normalizedAllocationEntropy"])
            escort = team_value(
                card["fieldShape"]["convoyEscortCountWithinTwoTiles"],
                team,
                {},
            )
            entry["escortDensity"].append(escort["mean"])
            passivity = card["feltDegeneracy"]["sustainedPassivity"]
            entry["quietTicks"] += team_value(
                passivity["quietTicksByTeam"], team)
            entry["highWaitTicks"] += team_value(
                passivity["highWaitTicksByTeam"], team)
            signatures = team_value(
                card["signatures"]["byTeamAndSignature"], team, {})
            for signature, values in signatures.items():
                entry["signatureAttempts"][signature] += values["attempts"]

    summaries: dict[str, dict[str, Any]] = {}
    all_signatures = sorted({
        signature
        for entry in totals.values()
        for signature in entry["signatureAttempts"]
        if signature != "forced-displacement"
    })
    shape_vectors: dict[str, dict[str, float]] = {}
    for entrant, entry in sorted(totals.items()):
        games = entry["games"]
        attempts = sum(
            entry["signatureAttempts"].get(signature, 0)
            for signature in all_signatures
        )
        summaries[entrant] = {
            "games": games,
            "record": {
                key: entry[key]
                for key in ("wins", "draws", "losses")
            },
            "perGame": {
                "deliveries": entry["deliveries"] / games,
                "pulses": entry["pulses"] / games,
                "steals": entry["steals"] / games,
                "possessionTicks": entry["possessionTicks"] / games,
                "crossTheaterTransitions": (
                    entry["crossTheaterTransitions"] / games
                ),
                "quietTicks": entry["quietTicks"] / games,
                "highWaitTicks": entry["highWaitTicks"] / games,
            },
            "meanAllocationEntropy": mean(entry["allocationEntropy"]),
            "meanEscortDensity": mean(entry["escortDensity"]),
            "signatureAttempts": dict(sorted(
                entry["signatureAttempts"].items()
            )),
        }
        shape_vectors[entrant] = {
            "deliveries": entry["deliveries"] / games,
            "pulses": entry["pulses"] / games,
            "steals": entry["steals"] / games,
            "possession": entry["possessionTicks"] / games,
            "transitions": entry["crossTheaterTransitions"] / games,
            "entropy": mean(entry["allocationEntropy"]),
            "escort": mean(entry["escortDensity"]),
            **{
                f"signature:{signature}": (
                    entry["signatureAttempts"].get(signature, 0) / attempts
                    if attempts else 0.0
                )
                for signature in all_signatures
            },
        }

    dimensions = sorted(next(iter(shape_vectors.values())))
    ranges = {
        dimension: (
            min(vector[dimension] for vector in shape_vectors.values()),
            max(vector[dimension] for vector in shape_vectors.values()),
        )
        for dimension in dimensions
    }
    normalized = {
        entrant: {
            dimension: (
                0.0 if ranges[dimension][0] == ranges[dimension][1]
                else (vector[dimension] - ranges[dimension][0])
                    / (ranges[dimension][1] - ranges[dimension][0])
            )
            for dimension in dimensions
        }
        for entrant, vector in shape_vectors.items()
    }
    for entrant, vector in normalized.items():
        closest = min(
            (
                math.sqrt(sum(
                    (vector[dimension] - other[dimension]) ** 2
                    for dimension in dimensions
                ) / len(dimensions)),
                other_entrant,
            )
            for other_entrant, other in normalized.items()
            if other_entrant != entrant
        )
        summaries[entrant]["closestExecutionShape"] = {
            "entrantId": closest[1],
            "normalizedRmsDistance": closest[0],
            "authority": "diagnostic only; no registered exclusion threshold",
        }

    result = {
        "schema": "arc-relay-doctrine-read-v1",
        "runtimeAuthority": "scorecards from exact-verified sweep broadcasts",
        "scorecardCount": len(scorecards),
        "doctrines": summaries,
    }
    content = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(content, encoding="utf-8")
        print(args.output)
    else:
        print(content, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
