#!/usr/bin/env python3
"""Reduce an exact-verified Arc Relay sweep into design-depth evidence.

This is deliberately broader than the balance reducer. It describes whether
classes, signatures, theaters, openings, contests, and matchup relationships
actually produce different play. It does not infer human enjoyment and it does
not turn a shared stock mind into product-balance authority.
"""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict, deque
import gzip
import hashlib
import itertools
import json
import math
from pathlib import Path
from statistics import mean, median
from typing import Any


OPENING_PREFIX_TICKS = (25, 75, 125)
CLASS_SIGNATURE = {
    "kestrel": "vector-dash",
    "palisade": "prism-wall",
    "towline": "tractor-hook",
    "patchbay": "repair-beam",
    "lantern": "survey-flare",
    "mortar": "falling-star",
    "minesmith": "trip-node",
    "hush": "null-field",
    "relay": "arc-toss",
    "switchback": "exchange",
    "longshot": "rail-line",
    "mason": "hardlight-block",
    "sunder": "target-paint",
    "repulsor": "kinetic-burst",
    "veil": "smoke-canister",
    "nest": "sentinel-seed",
}


def read_json(path: Path) -> dict[str, Any]:
    with path.open("rb") as source:
        compressed = source.read(2) == b"\x1f\x8b"
    opener = gzip.open if compressed else open
    with opener(path, "rt", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: JSON root must be an object")
    return value


def team_value(container: dict[str, Any], team: int, default: Any = 0) -> Any:
    return container.get(str(team), container.get(team, default))


def ratio(numerator: int | float, denominator: int | float) -> float | None:
    return numerator / denominator if denominator else None


def percentile(values: list[int | float], fraction: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    index = math.ceil(fraction * len(ordered)) - 1
    return float(ordered[max(0, min(index, len(ordered) - 1))])


def series(values: list[int | float]) -> dict[str, int | float | None]:
    return {
        "count": len(values),
        "min": min(values) if values else None,
        "median": median(values) if values else None,
        "mean": mean(values) if values else None,
        "p10": percentile(values, 0.10),
        "p90": percentile(values, 0.90),
        "max": max(values) if values else None,
    }


def normalized_entropy(counter: Counter[str]) -> float:
    values = [value for value in counter.values() if value > 0]
    if not values:
        return 0.0
    if len(values) == 1:
        return 0.0
    total = sum(values)
    return -sum(
        (value / total) * math.log2(value / total)
        for value in values
    ) / math.log2(len(values))


def opening_fingerprint(
    broadcast: dict[str, Any],
    team: int,
    through_ticks: int,
) -> str:
    width = int(broadcast["header"]["contract"]["map"]["width"])
    frames: list[Any] = []
    for tick in range(min(through_ticks, len(broadcast["worlds"]))):
        world = broadcast["worlds"][tick]
        actors = []
        for actor in world[4]:
            if actor[0] != team:
                continue
            x = int(actor[6])
            actors.append([
                int(actor[1]),
                width - 1 - x if team == 1 else x,
                int(actor[7]),
                actor[5],
                int(actor[9]),
            ])
        actions = []
        for turn in broadcast["turns"][tick]:
            if turn[0][0] != team:
                continue
            resolved = turn[5]
            actions.append([int(turn[0][1]), resolved[0], turn[6]])
        frames.append([sorted(actors), sorted(actions)])
    encoded = json.dumps(
        frames, ensure_ascii=True, separators=(",", ":"), sort_keys=False,
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def map_metrics(broadcast: dict[str, Any]) -> dict[str, Any]:
    contract = broadcast["header"]["contract"]["map"]
    rows = contract["tileRows"]
    height = len(rows)
    width = len(rows[0])
    walls = sum(row.count("#") for row in rows)
    passable = width * height - walls
    regions = {
        region["regionId"]: [tuple(tile) for tile in region["tiles"]]
        for region in contract["regions"]
    }

    def shortest(starts: list[tuple[int, int]], target: tuple[int, int]) -> int | None:
        queue = deque((point, 0) for point in starts)
        seen = set(starts)
        while queue:
            (x, y), distance = queue.popleft()
            if (x, y) == target:
                return distance
            for dx, dy in itertools.product((-1, 0, 1), repeat=2):
                if dx == 0 and dy == 0:
                    continue
                nx, ny = x + dx, y + dy
                if not (0 <= nx < width and 0 <= ny < height):
                    continue
                if rows[ny][nx] == "#" or (nx, ny) in seen:
                    continue
                if dx and dy and (
                    rows[y][nx] == "#" or rows[ny][x] == "#"
                ):
                    continue
                seen.add((nx, ny))
                queue.append(((nx, ny), distance + 1))
        return None

    well_distances: dict[str, dict[str, int | None]] = {}
    for well in ("north", "centre", "south"):
        target = regions[f"well-{well}"][0]
        well_distances[well] = {
            "west": shortest(regions["reactor-west"], target),
            "east": shortest(regions["reactor-east"], target),
        }
    rotated_mismatches = sum(
        rows[y][x] != rows[height - 1 - y][width - 1 - x]
        for y in range(height)
        for x in range(width)
    )
    vertical_mismatches = sum(
        rows[y][x] != rows[y][width - 1 - x]
        for y in range(height)
        for x in range(width)
    )
    return {
        "mapId": contract["mapId"],
        "mapFingerprint": contract["mapFingerprint"],
        "width": width,
        "height": height,
        "tiles": width * height,
        "passableTiles": passable,
        "wallTiles": walls,
        "wallShare": walls / (width * height),
        "wellShortestEightWayTilesFromReactors": well_distances,
        "rotationalMismatchTiles": rotated_mismatches,
        "verticalMirrorMismatchTiles": vertical_mismatches,
    }


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

    matches = 0
    eligible_matches = 0
    draws = 0
    team_wins: Counter[int] = Counter()
    entrant_records: dict[str, Counter[str]] = defaultdict(Counter)
    end_ticks: list[int] = []
    pulse_lead_changes: list[int] = []
    behind_to_ahead: list[int] = []
    first_pulse_converted = 0
    matches_with_pulse = 0
    matches_with_steal = 0
    matches_with_handoff = 0
    matches_with_contested_pickup = 0
    steals = handoffs = contested_pickups = pickups = 0
    arc_toss_landings = forced_displacements = 0
    deliveries_by_source: Counter[str] = Counter()
    damage_by_theater: Counter[str] = Counter()
    theater_entropies: list[float] = []
    theater_transitions: list[int] = []
    route_stretch: list[float] = []
    delivery_age: list[int] = []
    class_data: dict[str, Counter[str]] = {
        class_id: Counter() for class_id in CLASS_SIGNATURE
    }
    signature_data: dict[str, Counter[str]] = {
        signature: Counter() for signature in CLASS_SIGNATURE.values()
    }
    opening_hashes: dict[int, dict[str, list[str]]] = {
        tick: defaultdict(list) for tick in OPENING_PREFIX_TICKS
    }
    pair_results: dict[tuple[str, str, str], list[dict[str, Any]]] = defaultdict(list)
    first_broadcast: dict[str, Any] | None = None

    for result in results["cells"]:
        matches += 1
        cell = cells[result["cellId"]]
        cell_dir = attempt / result["attempt"]
        card = read_json(cell_dir / "scorecard.json")
        if not card["feltDegeneracy"]["matchEligibleForCohortRead"]:
            continue
        eligible_matches += 1
        broadcast = read_json(cell_dir / "broadcast.json.gz")
        if first_broadcast is None:
            first_broadcast = broadcast
        winner_team = card["outcome"]["winnerTeamId"]
        end_ticks.append(card["outcome"]["endTick"])
        if winner_team is None:
            draws += 1
            for team in (0, 1):
                entrant_records[cell[f"team{team}"]]["draws"] += 1
        else:
            team_wins[winner_team] += 1
            entrant_records[cell[f"team{winner_team}"]]["wins"] += 1
            entrant_records[cell[f"team{1 - winner_team}"]]["losses"] += 1

        pair_key = (
            min(cell["team0"], cell["team1"]),
            max(cell["team0"], cell["team1"]),
            str(cell["seed"]),
        )
        pair_results[pair_key].append({
            "winnerTeam": winner_team,
            "winnerEntrant": (
                None if winner_team is None else cell[f"team{winner_team}"]
            ),
        })

        scoring = card["scoring"]
        pulses = scoring["pulseSequence"]
        if pulses:
            matches_with_pulse += 1
            first_pulse_converted += bool(
                scoring["firstPulseConvertedToMatchWinner"])
        pulse_lead_changes.append(scoring["pulseLeadChanges"])
        behind_to_ahead.append(scoring["behindToAheadPulseReversals"])
        for source_counts in scoring["deliveriesByTeamAndSource"].values():
            deliveries_by_source.update(source_counts)

        possession = card["possession"]
        match_steals = sum(possession["stealsByTeam"].values())
        match_handoffs = sum(possession["handoffsByTeam"].values())
        match_pickups = sum(
            sum(classes.values())
            for team in possession["pickupsByTeamSourceClass"].values()
            for classes in team.values()
        )
        match_contested = sum(
            values.get("contested", 0)
            for values in possession["pickupEnemyWithinTwoTilesProxy"].values()
        )
        steals += match_steals
        handoffs += match_handoffs
        pickups += match_pickups
        contested_pickups += match_contested
        matches_with_steal += match_steals > 0
        matches_with_handoff += match_handoffs > 0
        matches_with_contested_pickup += match_contested > 0
        arc_toss_landings += sum(possession["arcTossLandingsByTeam"].values())
        forced_displacements += sum(
            possession["forcedCarrierDisplacementsByCarrierTeam"].values())

        for values in card["fieldShape"]["damageBySourceTeamAndTheater"].values():
            damage_by_theater.update(values)
        for team in (0, 1):
            field = team_value(card["fieldShape"]["theatersByTeam"], team, {})
            theater_entropies.append(field["normalizedAllocationEntropy"])
            theater_transitions.append(field["crossTheaterTransitions"])
        for delivery in card["routes"]["deliveries"]:
            if delivery["routeStretch"] is not None:
                route_stretch.append(delivery["routeStretch"])
            delivery_age.append(delivery["ageTicks"])

        compositions = card["identity"]["compositionsByTeam"]
        signatures = card["signatures"]["byTeamAndSignature"]
        possession_by_class = possession["ticksByTeamAndClass"]
        for team in (0, 1):
            entrant = cell[f"team{team}"]
            composition = team_value(compositions, team, [])
            won = winner_team == team
            for class_id, body_count in Counter(composition).items():
                data = class_data[class_id]
                data["sideGames"] += 1
                data["bodyGames"] += body_count
                data["winningSideGames"] += won
                data["possessionTicks"] += team_value(
                    possession_by_class, team, {}).get(class_id, 0)
                signature = CLASS_SIGNATURE[class_id]
                measured = team_value(signatures, team, {}).get(signature, {})
                signature_entry = signature_data[signature]
                signature_entry["sideGames"] += 1
                signature_entry["bodyGames"] += body_count
                for source, target in (
                    ("attempts", "attempts"),
                    ("terminalTransitions", "terminalTransitions"),
                    ("counteredOrReplaced", "counteredOrReplaced"),
                    ("usefulEffectFacts", "effectFacts"),
                    ("stackedBodyTicks", "stackedBodyTicks"),
                ):
                    signature_entry[target] += measured.get(source, 0)
                signature_entry["sideGamesWithAttempt"] += (
                    measured.get("attempts", 0) > 0)
                signature_entry["sideGamesWithEffectFact"] += (
                    measured.get("usefulEffectFacts", 0) > 0)
            for prefix in OPENING_PREFIX_TICKS:
                opening_hashes[prefix][entrant].append(
                    opening_fingerprint(broadcast, team, prefix))

    if first_broadcast is None:
        raise ValueError("sweep has no eligible broadcasts")

    pair_counts: Counter[str] = Counter()
    dominance_edges: set[tuple[str, str]] = set()
    for (left, right, _seed), rows in pair_results.items():
        if len(rows) != 2:
            pair_counts["incomplete"] += 1
            continue
        winners = [row["winnerEntrant"] for row in rows]
        winner_teams = [row["winnerTeam"] for row in rows]
        if winners[0] is None or winners[1] is None:
            pair_counts["includesDraw"] += 1
        elif winners[0] == winners[1]:
            pair_counts["entrantSweep"] += 1
            loser = right if winners[0] == left else left
            dominance_edges.add((winners[0], loser))
        else:
            pair_counts["split"] += 1
            if winner_teams[0] == winner_teams[1] == 0:
                pair_counts["team0LockedSplit"] += 1
            elif winner_teams[0] == winner_teams[1] == 1:
                pair_counts["team1LockedSplit"] += 1
    entrants = sorted(manifest["entrants"])
    cycles = []
    for a, b, c in itertools.combinations(entrants, 3):
        if (
            ((a, b) in dominance_edges and (b, c) in dominance_edges
             and (c, a) in dominance_edges)
            or ((a, c) in dominance_edges and (c, b) in dominance_edges
                and (b, a) in dominance_edges)
        ):
            cycles.append([a, b, c])

    opening_output: dict[str, Any] = {}
    for prefix, by_entrant in opening_hashes.items():
        unique_counts = [len(set(values)) for values in by_entrant.values()]
        opening_output[str(prefix)] = {
            "entrantSides": sum(len(values) for values in by_entrant.values()),
            "distinctFingerprints": len({
                value for values in by_entrant.values() for value in values
            }),
            "uniqueFingerprintsPerEntrant": series(unique_counts),
            "entrantsWithOnlyOneFingerprint": sum(
                len(set(values)) == 1 for values in by_entrant.values()),
            "method": (
                "team-normalized authoritative body position, form, health, "
                "resolved action id, and result through the tick prefix"
            ),
        }

    source_total = sum(deliveries_by_source.values())
    damage_total = sum(damage_by_theater.values())
    class_output = {}
    for class_id, data in class_data.items():
        class_output[class_id] = {
            **dict(data),
            "signatureId": CLASS_SIGNATURE[class_id],
            "sideWinAssociation": ratio(
                data["winningSideGames"], data["sideGames"]),
            "possessionTicksPerBodyGame": ratio(
                data["possessionTicks"], data["bodyGames"]),
        }
    signature_output = {}
    for signature, data in signature_data.items():
        signature_output[signature] = {
            **dict(data),
            "attemptsPerBodyGame": ratio(data["attempts"], data["bodyGames"]),
            "effectFactsPerAttempt": ratio(data["effectFacts"], data["attempts"]),
            "counteredOrReplacedShare": ratio(
                data["counteredOrReplaced"], data["terminalTransitions"]),
            "sideGameAttemptCoverage": ratio(
                data["sideGamesWithAttempt"], data["sideGames"]),
            "sideGameEffectFactCoverage": ratio(
                data["sideGamesWithEffectFact"], data["sideGames"]),
        }

    output = {
        "schema": "arc-relay-depth-map-read-v1",
        "authority": (
            "exact-verified spectator broadcasts from a shared stock-mind "
            "evaluation corpus; diagnostic, not a human-fun claim"
        ),
        "sweepId": manifest.get("sweepId"),
        "runtime": results.get("runtime"),
        "loopProfile": manifest.get("loopProfile"),
        "map": map_metrics(first_broadcast),
        "population": {
            "plannedMatches": matches,
            "eligibleMatches": eligible_matches,
            "entrants": len(entrants),
            "records": {
                entrant: dict(entrant_records[entrant])
                for entrant in entrants
            },
        },
        "fairnessAndCounterWeb": {
            "team0Wins": team_wins[0],
            "team1Wins": team_wins[1],
            "draws": draws,
            "team0ShareOfDecidedWins": ratio(
                team_wins[0], team_wins[0] + team_wins[1]),
            "mirroredPairReads": dict(pair_counts),
            "dominanceEdges": len(dominance_edges),
            "directedThreeCycles": len(cycles),
            "cycleExamples": cycles[:12],
        },
        "pacingAndTension": {
            "endTicks": series(end_ticks),
            "matchesWithPulse": matches_with_pulse,
            "firstPulseConverted": first_pulse_converted,
            "firstPulseConversionRate": ratio(
                first_pulse_converted, matches_with_pulse),
            "pulseLeadChanges": series(pulse_lead_changes),
            "matchesWithPulseLeadChange": sum(
                value > 0 for value in pulse_lead_changes),
            "behindToAheadPulseReversals": series(behind_to_ahead),
            "matchesWithBehindToAheadPulseReversal": sum(
                value > 0 for value in behind_to_ahead),
        },
        "contestAndPossession": {
            "pickups": pickups,
            "contestedPickupProxy": contested_pickups,
            "contestedPickupShare": ratio(contested_pickups, pickups),
            "steals": steals,
            "handoffs": handoffs,
            "arcTossLandings": arc_toss_landings,
            "forcedCarrierDisplacements": forced_displacements,
            "matchesWithSteal": matches_with_steal,
            "matchesWithHandoff": matches_with_handoff,
            "matchesWithContestedPickup": matches_with_contested_pickup,
        },
        "mapFlow": {
            "deliveriesBySource": dict(sorted(deliveries_by_source.items())),
            "deliveryShareBySource": {
                key: value / source_total
                for key, value in sorted(deliveries_by_source.items())
            },
            "deliverySourceEntropy": normalized_entropy(deliveries_by_source),
            "damageByTheater": dict(sorted(damage_by_theater.items())),
            "damageShareByTheater": {
                key: value / damage_total
                for key, value in sorted(damage_by_theater.items())
            },
            "damageTheaterEntropy": normalized_entropy(damage_by_theater),
            "teamMatchAllocationEntropy": series(theater_entropies),
            "teamMatchCrossTheaterTransitions": series(theater_transitions),
            "deliveredRouteStretch": series(route_stretch),
            "deliveryAgeTicks": series(delivery_age),
        },
        "classes": class_output,
        "signatures": signature_output,
        "openings": opening_output,
        "limitations": [
            "sheet and class win associations are confounded by composition, plan, and opponent",
            "signature effect facts measure observable consequences, not strategic value",
            "opening diversity measures executed trajectories, not hidden intent",
            "shared stock-mind evidence cannot establish independent human strategy depth",
        ],
    }
    encoded = json.dumps(output, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
        print(args.output)
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
