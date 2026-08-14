#!/usr/bin/env python3
"""Reduce a registered Arc Relay opening matrix into causal strategy evidence.

The read deliberately keeps results and mechanisms together. A record without
movement, Core, theater, participation, and stall context is not allowed to
masquerade as an explanation of why an opening won.
"""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import gzip
import json
from pathlib import Path
import statistics
from typing import Any, Iterable


REPO = Path(__file__).resolve().parent.parent


def read_json(path: Path) -> dict[str, Any]:
    with path.open("rb") as source:
        compressed = source.read(2) == b"\x1f\x8b"
    opener = gzip.open if compressed else open
    with opener(path, "rt", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: JSON root must be an object")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def resolve_attempt(path: Path) -> Path:
    path = path.resolve()
    if (path / "COMPLETE.json").is_file():
        return path
    latest = path / "LATEST.json"
    if latest.is_file():
        attempt = path / read_json(latest)["attempt"]
        if (attempt / "COMPLETE.json").is_file():
            return attempt
    complete = sorted(
        candidate.parent for candidate in path.glob("attempt-*/COMPLETE.json"))
    if complete:
        return complete[-1]
    raise ValueError(f"{path}: no complete sweep attempt")


def accepted_cell(attempt: Path, cell_id: str) -> tuple[Path, dict[str, Any]]:
    cell_root = attempt / "cells" / cell_id
    for candidate in sorted(cell_root.glob("attempt-*"), reverse=True):
        result_path = candidate / "cell-result.json"
        replay_path = candidate / "replay.json.gz"
        if not result_path.is_file() or not replay_path.is_file():
            continue
        result = read_json(result_path)
        if result.get("verified") is True:
            return candidate, result
    raise ValueError(f"{cell_id}: no accepted cell attempt")


def actor(value: Any) -> tuple[int, int, int] | None:
    if not isinstance(value, dict):
        return None
    keys = ("teamId", "unitId", "lifeId")
    if not all(isinstance(value.get(key), int) for key in keys):
        return None
    return value["teamId"], value["unitId"], value["lifeId"]


def team_unit(value: Any) -> tuple[int, int] | None:
    parsed = actor(value)
    return None if parsed is None else parsed[:2]


def first_tile(regions: list[dict[str, Any]], region_id: str) -> tuple[int, int]:
    region = next(
        (item for item in regions if item.get("regionId") == region_id), None)
    if region is None or not region.get("tiles"):
        raise ValueError(f"map has no {region_id} region")
    x, y = region["tiles"][0]
    return int(x), int(y)


def nearest_theater(
    position: tuple[int, int],
    wells: dict[str, tuple[int, int]],
) -> str:
    x, y = position
    return min(
        wells,
        key=lambda name: (
            abs(x - wells[name][0]) + abs(y - wells[name][1]),
            name,
        ),
    )


def attack_progress(
    team: int,
    position: tuple[int, int],
    reactors: dict[int, tuple[int, int]],
) -> float:
    own = reactors[team]
    enemy = reactors[1 - team]
    dx = enemy[0] - own[0]
    dy = enemy[1] - own[1]
    denominator = dx * dx + dy * dy
    if denominator == 0:
        return 0.0
    px = position[0] - own[0]
    py = position[1] - own[1]
    return (px * dx + py * dy) / denominator


def blank_team() -> dict[str, Any]:
    return {
        "firstAttackTick": None,
        "firstPickupTick": None,
        "firstBankTick": None,
        "attacks": 0,
        "movements": 0,
        "banks": 0,
        "pulses": 0,
        "carrierInterceptions": 0,
        "kills": 0,
        "deaths": 0,
        "attackBodies": set(),
        "coreBodies": set(),
        "signatureBodies": set(),
        "movementBodies": set(),
        "openingTheaterBodyTicks": Counter(),
        "openingOpponentHalfBodyTicks": 0,
        "openingObservedBodyTicks": 0,
        "stationaryTransitions": 0,
        "observedTransitions": 0,
        "longestStationaryRun": 0,
    }


def mark_first(team: dict[str, Any], field: str, tick: int) -> None:
    if team[field] is None:
        team[field] = tick


def replay_metrics(replay: dict[str, Any], opening_ticks: int) -> dict[str, Any]:
    teams = {0: blank_team(), 1: blank_team()}
    regions = replay["header"]["contract"]["map"]["regions"]
    wells = {
        "north": first_tile(regions, "well-north"),
        "centre": first_tile(regions, "well-centre"),
        "south": first_tile(regions, "well-south"),
    }
    reactors = {
        0: first_tile(regions, "reactor-west"),
        1: first_tile(regions, "reactor-east"),
    }
    prior: dict[tuple[int, int, int], tuple[int, tuple[int, int], int]] = {}

    for frame in replay.get("ticks", []):
        tick = int(frame["tick"])
        observed: dict[tuple[int, int, int], tuple[int, int]] = {}
        for turn in frame.get("mindTurns", []):
            for body in turn.get("observation", {}).get("bodies", []):
                key = actor(body.get("actorId"))
                position_value = body.get("position")
                if key is None or not isinstance(position_value, dict):
                    continue
                position = (int(position_value["x"]), int(position_value["y"]))
                observed[key] = position
        for key, position in observed.items():
            team_id = key[0]
            team = teams[team_id]
            previous = prior.get(key)
            run = 0
            if previous is not None and previous[0] == tick - 1:
                team["observedTransitions"] += 1
                if previous[1] == position:
                    run = previous[2] + 1
                    team["stationaryTransitions"] += 1
                    team["longestStationaryRun"] = max(
                        team["longestStationaryRun"], run)
            prior[key] = (tick, position, run)
            if tick < opening_ticks:
                team["openingObservedBodyTicks"] += 1
                team["openingTheaterBodyTicks"][
                    nearest_theater(position, wells)
                ] += 1
                if attack_progress(team_id, position, reactors) > 0.5:
                    team["openingOpponentHalfBodyTicks"] += 1

        destructions: dict[tuple[int, int, int], int] = {}
        facts: list[dict[str, Any]] = []
        for event in frame.get("events", []):
            kind = event.get("kind")
            payload = event.get("payload", {})
            if kind == "movement":
                identity = team_unit(payload.get("actorId"))
                if identity is not None:
                    teams[identity[0]]["movements"] += 1
                    teams[identity[0]]["movementBodies"].add(identity[1])
            elif kind == "attack":
                identity = team_unit(payload.get("actorId"))
                if identity is not None:
                    team = teams[identity[0]]
                    team["attacks"] += 1
                    team["attackBodies"].add(identity[1])
                    mark_first(team, "firstAttackTick", tick)
            elif kind == "destruction":
                victim = actor(payload.get("actorId"))
                source_team = payload.get("sourceTeamId")
                if victim is not None:
                    teams[victim[0]]["deaths"] += 1
                    if isinstance(source_team, int):
                        destructions[victim] = source_team
                        teams[source_team]["kills"] += 1
            elif kind == "arc-relay":
                fact = payload.get("fact")
                if isinstance(fact, dict):
                    facts.append(fact)

        for fact in facts:
            kind = fact.get("kind")
            if kind == "core-picked-up":
                identity = team_unit(fact.get("carrierActorId"))
                if identity is not None:
                    teams[identity[0]]["coreBodies"].add(identity[1])
                    mark_first(teams[identity[0]], "firstPickupTick", tick)
            elif kind == "core-handed-off":
                for field in ("sourceActorId", "targetActorId"):
                    identity = team_unit(fact.get(field))
                    if identity is not None:
                        teams[identity[0]]["coreBodies"].add(identity[1])
            elif kind == "core-dropped":
                source = actor(fact.get("sourceActorId"))
                if source is not None:
                    teams[source[0]]["coreBodies"].add(source[1])
                    if fact.get("dropKind") == "destruction":
                        killer = destructions.get(source)
                        if killer is not None and killer != source[0]:
                            teams[killer]["carrierInterceptions"] += 1
            elif kind == "core-banked":
                team_id = fact.get("teamId")
                identity = team_unit(fact.get("carrierActorId"))
                if isinstance(team_id, int):
                    teams[team_id]["banks"] += 1
                    mark_first(teams[team_id], "firstBankTick", tick)
                if identity is not None:
                    teams[identity[0]]["coreBodies"].add(identity[1])
            elif kind == "pulse":
                team_id = fact.get("teamId")
                if isinstance(team_id, int):
                    teams[team_id]["pulses"] += 1
            elif (kind == "signature-changed"
                  and fact.get("reason") == "started"):
                identity = team_unit(fact.get("ownerActorId"))
                if identity is not None:
                    teams[identity[0]]["signatureBodies"].add(identity[1])

    serialized: dict[str, Any] = {}
    for team_id, team in teams.items():
        value_bodies = (
            team["attackBodies"]
            | team["coreBodies"]
            | team["signatureBodies"]
        )
        opening_total = team["openingObservedBodyTicks"]
        theater_shares = {
            name: round(team["openingTheaterBodyTicks"][name] / opening_total, 6)
            if opening_total else 0.0
            for name in ("north", "centre", "south")
        }
        transitions = team["observedTransitions"]
        serialized[str(team_id)] = {
            key: value
            for key, value in team.items()
            if not isinstance(value, (set, Counter))
            and key not in ("openingObservedBodyTicks",)
        }
        serialized[str(team_id)].update({
            "movementBodies": len(team["movementBodies"]),
            "attackBodies": len(team["attackBodies"]),
            "coreBodies": len(team["coreBodies"]),
            "signatureBodies": len(team["signatureBodies"]),
            "valueBodies": len(value_bodies),
            "openingTheaterShares": theater_shares,
            "openingOpponentHalfShare": round(
                team["openingOpponentHalfBodyTicks"] / opening_total, 6)
                if opening_total else 0.0,
            "stationaryShare": round(
                team["stationaryTransitions"] / transitions, 6)
                if transitions else 0.0,
        })
    result = replay["result"]
    winner = result.get("standings", {}).get("winnerTeamId")
    return {
        "winnerTeamId": winner,
        "completionReason": result.get("completionReason"),
        "endTick": result.get("endTick"),
        "teams": serialized,
    }


def mean(values: Iterable[float]) -> float | None:
    numbers = list(values)
    return round(statistics.fmean(numbers), 6) if numbers else None


def median(values: Iterable[float | int | None]) -> float | None:
    numbers = [float(value) for value in values if value is not None]
    return round(float(statistics.median(numbers)), 6) if numbers else None


def point_for(winner: str | None, entrant: str) -> float:
    if winner is None:
        return 0.5
    return 1.0 if winner == entrant else 0.0


def summarize(
    plan: dict[str, Any],
    attempt: Path,
    *,
    opening_ticks: int,
) -> dict[str, Any]:
    cells: list[dict[str, Any]] = []
    for cell in plan["cells"]:
        directory, cell_result = accepted_cell(attempt, cell["cellId"])
        metrics = replay_metrics(read_json(directory / "replay.json.gz"), opening_ticks)
        team0 = cell["team0"]
        team1 = cell["team1"]
        winner_team = metrics["winnerTeamId"]
        winner = team0 if winner_team == 0 else team1 if winner_team == 1 else None
        cells.append({
            "cellId": cell["cellId"],
            "seed": str(cell["seed"]),
            "team0": team0,
            "team1": team1,
            "winner": winner,
            "completionReason": metrics["completionReason"],
            "endTick": metrics["endTick"],
            "eligible": cell_result["matchEligibleForCohortRead"],
            "teams": metrics["teams"],
        })

    entrant_ids = sorted(plan["entrants"])
    pairings: dict[str, Any] = {}
    pair_rates: dict[str, dict[str, float]] = defaultdict(dict)
    for left_index, left in enumerate(entrant_ids):
        for right in entrant_ids[left_index + 1:]:
            relevant = [
                cell for cell in cells
                if {cell["team0"], cell["team1"]} == {left, right}
            ]
            # Sparse registered probes intentionally omit irrelevant pairs.
            # Absence is not a 0-0 matchup and must not be represented as one
            # (or divided by zero); only materialized cells enter pair reads.
            if not relevant:
                continue
            records: dict[str, Any] = {}
            for entrant in (left, right):
                wins = sum(cell["winner"] == entrant for cell in relevant)
                draws = sum(cell["winner"] is None for cell in relevant)
                losses = len(relevant) - wins - draws
                point_rate = (wins + 0.5 * draws) / len(relevant)
                records[entrant] = {
                    "wins": wins,
                    "draws": draws,
                    "losses": losses,
                    "pointRate": round(point_rate, 6),
                }
                pair_rates[entrant][right if entrant == left else left] = point_rate
            pairings[f"{left}--{right}"] = {
                "games": len(relevant),
                "records": records,
                "meanEndTick": mean(cell["endTick"] for cell in relevant),
                "horizonGames": sum(
                    cell["completionReason"] == "max-ticks" for cell in relevant),
                "eligibleGames": sum(cell["eligible"] for cell in relevant),
            }

    evaluation = plan.get("evaluation", {})
    dominant_at = evaluation.get("dominanceAlarm", {}).get(
        "pairPointRateAtLeastAgainstEveryOpponent", 0.7)
    dominated_at = evaluation.get("dominatedAlarm", {}).get(
        "pairPointRateAtMostAgainstEveryOpponent", 0.3)
    horizon_at = evaluation.get("horizonAlert", {}).get("shareAtLeast", 0.25)
    side_gap_at = evaluation.get("sideAlert", {}).get(
        "absolutePointRateGapAtLeast", 0.15)

    entrants: dict[str, Any] = {}
    alarms: list[dict[str, Any]] = []
    for entrant in entrant_ids:
        appearances: list[tuple[dict[str, Any], int]] = []
        for cell in cells:
            if cell["team0"] == entrant:
                appearances.append((cell, 0))
            elif cell["team1"] == entrant:
                appearances.append((cell, 1))
        wins = sum(cell["winner"] == entrant for cell, _ in appearances)
        draws = sum(cell["winner"] is None for cell, _ in appearances)
        losses = len(appearances) - wins - draws
        point_rate = (wins + 0.5 * draws) / len(appearances)
        side_rates: dict[str, float] = {}
        for side in (0, 1):
            side_cells = [(cell, team) for cell, team in appearances if team == side]
            points = sum(point_for(cell["winner"], entrant) for cell, _ in side_cells)
            side_rates[str(side)] = points / len(side_cells)
        side_gap = abs(side_rates["0"] - side_rates["1"])
        horizon_share = sum(
            cell["completionReason"] == "max-ticks" for cell, _ in appearances
        ) / len(appearances)
        per_team = [cell["teams"][str(team)] for cell, team in appearances]
        theater_names = ("north", "centre", "south")
        summary = {
            "label": plan["entrants"][entrant].get("label", entrant),
            "intent": plan["entrants"][entrant].get("intent"),
            "games": len(appearances),
            "wins": wins,
            "draws": draws,
            "losses": losses,
            "pointRate": round(point_rate, 6),
            "pairPointRates": {
                opponent: round(rate, 6)
                for opponent, rate in sorted(pair_rates[entrant].items())
            },
            "sidePointRates": {
                side: round(rate, 6) for side, rate in side_rates.items()
            },
            "sidePointRateGap": round(side_gap, 6),
            "horizonShare": round(horizon_share, 6),
            "meanEndTick": mean(cell["endTick"] for cell, _ in appearances),
            "medianFirstAttackTick": median(
                team["firstAttackTick"] for team in per_team),
            "medianFirstPickupTick": median(
                team["firstPickupTick"] for team in per_team),
            "medianFirstBankTick": median(
                team["firstBankTick"] for team in per_team),
            "meanBanks": mean(team["banks"] for team in per_team),
            "meanCarrierInterceptions": mean(
                team["carrierInterceptions"] for team in per_team),
            "minimumMovementBodies": min(
                team["movementBodies"] for team in per_team),
            "minimumValueBodies": min(team["valueBodies"] for team in per_team),
            "meanValueBodies": mean(team["valueBodies"] for team in per_team),
            "meanStationaryShare": mean(
                team["stationaryShare"] for team in per_team),
            "worstStationaryRun": max(
                team["longestStationaryRun"] for team in per_team),
            "openingTheaterShares": {
                theater: mean(
                    team["openingTheaterShares"][theater] for team in per_team)
                for theater in theater_names
            },
            "openingOpponentHalfShare": mean(
                team["openingOpponentHalfShare"] for team in per_team),
            "eligibleGames": sum(cell["eligible"] for cell, _ in appearances),
        }
        entrants[entrant] = summary
        rates = list(pair_rates[entrant].values())
        if rates and all(rate >= dominant_at for rate in rates):
            alarms.append({
                "kind": "cross-matchup-dominance",
                "entrant": entrant,
                "pairPointRates": summary["pairPointRates"],
            })
        if rates and all(rate <= dominated_at for rate in rates):
            alarms.append({
                "kind": "cross-matchup-dominated",
                "entrant": entrant,
                "pairPointRates": summary["pairPointRates"],
            })
        if horizon_share >= horizon_at:
            alarms.append({
                "kind": "horizon-share",
                "entrant": entrant,
                "share": round(horizon_share, 6),
            })
        if side_gap >= side_gap_at:
            alarms.append({
                "kind": "side-gap",
                "entrant": entrant,
                "gap": round(side_gap, 6),
            })

    return {
        "schema": "arc-relay-opening-matrix-read-v1",
        "sweepId": plan["sweepId"],
        "runtime": plan["runtime"],
        "loopProfile": plan["loopProfile"],
        "openingWindowTicks": opening_ticks,
        "cellCount": len(cells),
        "eligibleCellCount": sum(cell["eligible"] for cell in cells),
        "allCellsEligible": all(cell["eligible"] for cell in cells),
        "evaluation": evaluation,
        "entrants": entrants,
        "pairings": pairings,
        "alarms": alarms,
        "cells": cells,
        "interpretationLimit": (
            "Registered deterministic design screen; outcomes and mechanism "
            "metrics do not establish human fun or population balance."
        ),
    }


def print_summary(read: dict[str, Any]) -> None:
    print(
        f"{read['sweepId']}: {read['eligibleCellCount']}/"
        f"{read['cellCount']} eligible cells")
    for entrant_id, entrant in read["entrants"].items():
        print(
            f"{entrant_id:20} {entrant['wins']}-{entrant['draws']}-"
            f"{entrant['losses']} points={entrant['pointRate']:.3f} "
            f"first-bank={entrant['medianFirstBankTick']} "
            f"value-bodies={entrant['meanValueBodies']:.2f} "
            f"worst-stall={entrant['worstStationaryRun']}")
    if read["alarms"]:
        print("alarms:")
        for alarm in read["alarms"]:
            print("  " + json.dumps(alarm, sort_keys=True))
    else:
        print("alarms: none")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sweep", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--opening-ticks", type=int, default=120)
    args = parser.parse_args()
    if args.opening_ticks < 1:
        parser.error("--opening-ticks must be positive")
    attempt = resolve_attempt(args.sweep)
    run = read_json(attempt / "RUN.json")
    plan = read_json(Path(run["manifest"]))
    reduced = summarize(plan, attempt, opening_ticks=args.opening_ticks)
    if args.output is not None:
        write_json(args.output.resolve(), reduced)
    print_summary(reduced)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
