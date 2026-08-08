#!/usr/bin/env python3
"""Read execution-shape distance without exposing match outcomes.

The scorecards already exist after a sweep, but this reader deliberately omits
winners, scores, deliveries, Pulses, durations, and completion reasons. It is
used to decide whether newly authored evaluation sheets are behaviorally
distinct before opening the balance table.
"""

from __future__ import annotations

import argparse
from collections import defaultdict
import json
import math
from pathlib import Path
from statistics import mean
from typing import Any


MINIMUM_EXECUTION_DISTANCE = 0.12


def team_value(container: dict[str, Any], team: str, default: Any = 0) -> Any:
    return container.get(team, container.get(str(int(team)), default))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("attempt", type=Path)
    parser.add_argument("--new-ids", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    parser.add_argument(
        "--minimum-distance", type=float, default=MINIMUM_EXECUTION_DISTANCE
    )
    args = parser.parse_args()
    requested = json.loads(args.new_ids.read_text(encoding="utf-8"))
    new_ids = set(requested["newEntrants"])
    cards = sorted(args.attempt.resolve().glob("cells/**/scorecard.json"))
    if not cards:
        raise FileNotFoundError("completed sweep contains no scorecards")

    totals: dict[str, dict[str, Any]] = defaultdict(lambda: {
        "games": 0,
        "allocationEntropy": [],
        "escortDensity": [],
        "transitions": 0,
        "theaterBodyTicks": defaultdict(int),
        "roleTicks": defaultdict(int),
        "signatureAttempts": defaultdict(int),
        "quietTicks": 0,
        "highWaitTicks": 0,
        "noTheaterPresenceTicks": 0,
        "handoffs": 0,
        "carrierChanges": 0,
        "carrierClassTicks": defaultdict(int),
    })
    for path in cards:
        card = json.loads(path.read_text(encoding="utf-8"))
        for team, entrant in card["identity"]["entrantsByTeam"].items():
            entry = totals[entrant]
            entry["games"] += 1
            field = team_value(card["fieldShape"]["theatersByTeam"], team, {})
            entry["allocationEntropy"].append(
                field["normalizedAllocationEntropy"]
            )
            entry["transitions"] += field["crossTheaterTransitions"]
            for theater, ticks in field["bodyTicks"].items():
                entry["theaterBodyTicks"][theater] += ticks
            escort = team_value(
                card["fieldShape"]["convoyEscortCountWithinTwoTiles"],
                team,
                {},
            )
            entry["escortDensity"].append(escort["mean"])
            roles = team_value(
                card["fieldShape"]["publishedRoleTicksByTeamAndRole"],
                team,
                {},
            )
            for role, ticks in roles.items():
                entry["roleTicks"][role] += ticks
            signatures = team_value(
                card["signatures"]["byTeamAndSignature"], team, {}
            )
            for signature, values in signatures.items():
                entry["signatureAttempts"][signature] += values["attempts"]
            passive = card["feltDegeneracy"]["sustainedPassivity"]
            entry["quietTicks"] += team_value(
                passive["quietTicksByTeam"], team
            )
            entry["highWaitTicks"] += team_value(
                passive["highWaitTicksByTeam"], team
            )
            entry["noTheaterPresenceTicks"] += team_value(
                passive["noTheaterPresenceTicksByTeam"], team
            )
            entry["handoffs"] += team_value(
                card["possession"]["handoffsByTeam"], team
            )
            entry["carrierChanges"] += team_value(
                card["possession"]["carrierChangesByTeam"], team
            )
            for klass, ticks in team_value(
                card["possession"]["ticksByTeamAndClass"], team, {}
            ).items():
                entry["carrierClassTicks"][klass] += ticks

    all_signatures = sorted({
        signature
        for entry in totals.values()
        for signature in entry["signatureAttempts"]
        if signature != "forced-displacement"
    })
    all_roles = sorted({
        role for entry in totals.values() for role in entry["roleTicks"]
    })
    all_classes = sorted({
        klass
        for entry in totals.values()
        for klass in entry["carrierClassTicks"]
    })
    vectors: dict[str, dict[str, float]] = {}
    summaries: dict[str, dict[str, Any]] = {}
    for entrant, entry in sorted(totals.items()):
        games = entry["games"]
        theater_total = sum(entry["theaterBodyTicks"].values()) or 1
        role_total = sum(entry["roleTicks"].values()) or 1
        signature_total = sum(entry["signatureAttempts"].values()) or 1
        carrier_total = sum(entry["carrierClassTicks"].values()) or 1
        vector = {
            "allocationEntropy": mean(entry["allocationEntropy"]),
            "escortDensity": mean(entry["escortDensity"]),
            "transitionsPerGame": entry["transitions"] / games,
            "quietTicksPerGame": entry["quietTicks"] / games,
            "highWaitTicksPerGame": entry["highWaitTicks"] / games,
            "noTheaterPresenceTicksPerGame": (
                entry["noTheaterPresenceTicks"] / games
            ),
            "handoffsPerGame": entry["handoffs"] / games,
            "carrierChangesPerGame": entry["carrierChanges"] / games,
            **{
                f"theater:{theater}":
                    entry["theaterBodyTicks"].get(theater, 0) / theater_total
                for theater in ("north", "centre", "south")
            },
            **{
                f"role:{role}": entry["roleTicks"].get(role, 0) / role_total
                for role in all_roles
            },
            **{
                f"signature:{signature}":
                    entry["signatureAttempts"].get(signature, 0)
                    / signature_total
                for signature in all_signatures
            },
            **{
                f"carrierClass:{klass}":
                    entry["carrierClassTicks"].get(klass, 0) / carrier_total
                for klass in all_classes
            },
        }
        vectors[entrant] = vector
        summaries[entrant] = {
            "games": games,
            "allocationEntropy": vector["allocationEntropy"],
            "escortDensity": vector["escortDensity"],
            "transitionsPerGame": vector["transitionsPerGame"],
            "handoffsPerGame": vector["handoffsPerGame"],
            "carrierChangesPerGame": vector["carrierChangesPerGame"],
            "theaterShares": {
                theater: vector[f"theater:{theater}"]
                for theater in ("north", "centre", "south")
            },
            "roleShares": {
                role: vector[f"role:{role}"] for role in all_roles
            },
            "signatureAttemptShares": {
                signature: vector[f"signature:{signature}"]
                for signature in all_signatures
                if vector[f"signature:{signature}"] > 0
            },
        }
    missing = sorted(new_ids - set(vectors))
    if missing:
        raise ValueError(f"new entrants missing from execution screen: {missing}")

    dimensions = sorted(next(iter(vectors.values())))
    ranges = {
        dimension: (
            min(vector[dimension] for vector in vectors.values()),
            max(vector[dimension] for vector in vectors.values()),
        )
        for dimension in dimensions
    }
    normalized = {
        entrant: {
            dimension: (
                0.0 if low == high
                else (vector[dimension] - low) / (high - low)
            )
            for dimension, (low, high) in ranges.items()
        }
        for entrant, vector in vectors.items()
    }
    closest_rows = []
    for entrant in sorted(new_ids):
        vector = normalized[entrant]
        distance, closest = min(
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
        closest_rows.append({
            "entrantId": entrant,
            "closestEntrantId": closest,
            "normalizedRmsDistance": distance,
            "passes": distance >= args.minimum_distance,
        })

    result = {
        "schema": "arc-relay-sheet-execution-distinctness-v1",
        "outcomeBlind": True,
        "omitted": [
            "winner", "score", "deliveries", "pulses", "duration",
            "completionReason",
        ],
        "scorecardCount": len(cards),
        "populationSize": len(vectors),
        "minimumNormalizedRmsDistance": args.minimum_distance,
        "allNewSheetsPass": all(row["passes"] for row in closest_rows),
        "newSheetClosestPairs": closest_rows,
        "executionSummaries": summaries,
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
