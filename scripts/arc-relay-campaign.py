#!/usr/bin/env python3
"""Freeze and run the Arc Relay Gate-3 native-doctrine campaign.

``prepare`` records the cohort, mirrored schedule, and outcome-blind review
sample before any aggregate result exists. ``run`` refuses a changed freeze,
executes every planned match, verifies the scratch canonical replay, derives a
scorecard from the durable broadcast, and prunes the canonical replay/logs.
"""

from __future__ import annotations

import argparse
import collections
import gzip
import hashlib
import importlib.util
import itertools
import json
import os
from pathlib import Path
import random
import subprocess
import sys


REPO = Path(__file__).resolve().parent.parent
DEFAULT_CLI = REPO / "src/BotArena.Cli/bin/Debug/net10.0/botarena.dll"
MATCH_RUNNER = REPO / "scripts/arc-relay-match.py"
SCORECARD_SCRIPT = REPO / "scripts/arc-relay-scorecard.py"
RECORD_LIMIT = 4 * 1024
BROADCAST_LIMIT = 300 * 1024
TOTAL_LIMIT = 304 * 1024


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def read_json(path: Path) -> dict:
    with path.open("rb") as source:
        prefix = source.read(2)
    opener = gzip.open if prefix == b"\x1f\x8b" else open
    with opener(path, "rt", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: JSON root must be an object")
    return value


def write_json(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    encoded = json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(encoded, encoding="utf-8")
    temporary.replace(path)


def resolve_asset(cohort_path: Path, value: str) -> Path:
    path = Path(value)
    return path.resolve() if path.is_absolute() else (cohort_path.parent / path).resolve()


def load_scorecard_module():
    spec = importlib.util.spec_from_file_location("arc_relay_scorecard", SCORECARD_SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot import {SCORECARD_SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def validate_cohort(cohort_path: Path) -> tuple[dict, list[dict]]:
    cohort = read_json(cohort_path)
    if cohort.get("schema") != "arc-relay-native-cohort-v1":
        raise ValueError("cohort schema must be arc-relay-native-cohort-v1")
    entrants = cohort.get("entrants")
    if not isinstance(entrants, list) or len(entrants) != 4:
        raise ValueError("native cohort must contain exactly four entrants")
    ids = [item.get("entrantId") for item in entrants]
    if any(not isinstance(value, str) or not value for value in ids):
        raise ValueError("every entrant needs entrantId")
    if len(set(ids)) != len(ids):
        raise ValueError("entrantId values must be unique")
    cells = [item.get("doctrineCell") for item in entrants]
    if len(set(cells)) != 4:
        raise ValueError("the four doctrineCell values must be distinct")
    normalized = []
    for item in entrants:
        artifact = resolve_asset(cohort_path, item["artifact"])
        sheet = resolve_asset(cohort_path, item["sheet"])
        source_values = item.get("sourceFiles")
        if source_values is None:
            source_values = [item["source"]]
        if not isinstance(source_values, list) or not source_values:
            raise ValueError(f"{item['entrantId']} needs sourceFiles")
        sources = [resolve_asset(cohort_path, value) for value in source_values]
        manifest = resolve_asset(cohort_path, item["manifest"])
        for path in (artifact, sheet, manifest, *sources):
            if not path.is_file():
                raise FileNotFoundError(path)
        source_files = [
            {
                "path": str(path),
                "sha256": sha256(path),
            }
            for path in sources
        ]
        source_bundle_sha = sha256_bytes(
            json.dumps(
                source_files,
                ensure_ascii=False,
                separators=(",", ":"),
                sort_keys=True,
            ).encode("utf-8")
        )
        actual = {
            "artifactSha256": sha256(artifact),
            "sheetSha256": sha256(sheet),
            "sourceBundleSha256": source_bundle_sha,
            "manifestSha256": sha256(manifest),
        }
        for field, digest in actual.items():
            declared = item.get(field)
            if declared is not None and declared != digest:
                raise ValueError(
                    f"{item['entrantId']} {field}: declared {declared}, actual {digest}"
                )
        normalized.append(
            {
                **item,
                **actual,
                "artifactPath": artifact,
                "sheetPath": sheet,
                "sourceFilesResolved": source_files,
                "manifestPath": manifest,
                "authorManifestSourceSha256": read_json(manifest).get("sourceSha256"),
            }
        )
    return cohort, normalized


def public_entrant(item: dict, cohort_path: Path) -> dict:
    base = cohort_path.parent.resolve()
    return {
        "entrantId": item["entrantId"],
        "doctrineCell": item["doctrineCell"],
        "artifact": os.path.relpath(item["artifactPath"], base),
        "artifactSha256": item["artifactSha256"],
        "sheet": os.path.relpath(item["sheetPath"], base),
        "sheetSha256": item["sheetSha256"],
        "sourceFiles": [
            {
                "path": os.path.relpath(Path(source["path"]), base),
                "sha256": source["sha256"],
            }
            for source in item["sourceFilesResolved"]
        ],
        "sourceBundleSha256": item["sourceBundleSha256"],
        "authorManifestSourceSha256": item["authorManifestSourceSha256"],
        "manifest": os.path.relpath(item["manifestPath"], base),
        "manifestSha256": item["manifestSha256"],
    }


def match_plan(cohort: dict, entrants: list[dict]) -> list[dict]:
    seeds = cohort.get("seeds", ["104729"])
    if not isinstance(seeds, list) or not seeds or any(not str(seed) for seed in seeds):
        raise ValueError("cohort seeds must be a non-empty list")
    matches = []
    for seed in map(str, seeds):
        for first, second in itertools.combinations(entrants, 2):
            pair_id = "--".join(sorted((first["entrantId"], second["entrantId"])))
            for assignment, (team0, team1) in enumerate(((first, second), (second, first))):
                matches.append(
                    {
                        "matchId": f"{pair_id}--s{seed}--a{assignment}",
                        "pairId": pair_id,
                        "seed": seed,
                        "assignment": assignment,
                        "team0EntrantId": team0["entrantId"],
                        "team1EntrantId": team1["entrantId"],
                    }
                )
    return matches


def prepare(args: argparse.Namespace) -> int:
    cohort, entrants = validate_cohort(args.cohort)
    output = args.output.resolve()
    if (output / "results.json").exists() or list(output.glob("matches/*/match-record.json")):
        raise RuntimeError("refusing to freeze a sample after match outcomes exist")
    matches = match_plan(cohort, entrants)
    plan = {
        "schema": "arc-relay-native-campaign-plan-v1",
        "cohortId": cohort["cohortId"],
        "preparedBeforeOutcomes": True,
        "commonRandomnessSeeds": [str(value) for value in cohort.get("seeds", ["104729"])],
        "entrants": [public_entrant(item, args.cohort) for item in entrants],
        "matches": matches,
    }
    plan_path = output / "campaign-plan.json"
    write_json(plan_path, plan)

    sample = [
        {
            "reviewId": f"blind-{index + 1:02d}",
            "matchId": item["matchId"],
            "pairId": item["pairId"],
            "assignment": item["assignment"],
            "seed": item["seed"],
            "team0EntrantId": item["team0EntrantId"],
            "team1EntrantId": item["team1EntrantId"],
        }
        for index, item in enumerate(matches)
    ]
    order_seed = int(cohort.get("reviewOrderSeed", 20260801))
    random.Random(order_seed).shuffle(sample)
    review = {
        "schema": "arc-relay-outcome-blind-sample-v1",
        "cohortId": cohort["cohortId"],
        "selectionBoundary": "headers, pairs, assignments, and frozen seeds only",
        "containsOutcomesScoresOrDurations": False,
        "pairAndAssignmentBalanced": True,
        "reviewOrderSeed": str(order_seed),
        "matches": sample,
    }
    review_path = output / "review-sample.json"
    write_json(review_path, review)
    freeze = {
        "schema": "arc-relay-native-campaign-freeze-v1",
        "cohortId": cohort["cohortId"],
        "cohortFile": str(args.cohort.resolve()),
        "cohortSha256": sha256(args.cohort),
        "planFile": plan_path.name,
        "planSha256": sha256(plan_path),
        "reviewSampleFile": review_path.name,
        "reviewSampleSha256": sha256(review_path),
        "outcomesExistedAtFreeze": False,
        "plannedMatchCount": len(matches),
        "canonicalReplayPolicy": "gzip scratch, verify, then delete",
        "durablePerMatchFiles": ["match-record.json", "broadcast.json.gz"],
    }
    freeze_path = output / "FROZEN.json"
    write_json(freeze_path, freeze)
    print(
        f"froze {len(matches)} mirrored matches and {len(sample)} blind reviews; "
        f"sample sha256={sha256(review_path)}",
        flush=True,
    )
    return 0


def run_process(command: list[str]) -> None:
    completed = subprocess.run(
        command,
        cwd=REPO,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError(
            f"command exited {completed.returncode}: {' '.join(command)}\n"
            + completed.stdout[-4000:]
        )


def score_outcome_signature(scorecard: dict, match: dict) -> str:
    winner_team = scorecard["outcome"]["winnerTeamId"]
    winner = (
        "draw"
        if winner_team is None
        else match[f"team{winner_team}EntrantId"]
    )
    scores = scorecard["outcome"]["scoresByTeam"]
    return "|".join(
        (
            winner,
            scorecard["outcome"]["completionReason"],
            str(scores["0"].get("pulses", 0)),
            str(scores["0"].get("reactor-charge", 0)),
            str(scores["1"].get("pulses", 0)),
            str(scores["1"].get("reactor-charge", 0)),
        )
    )


def aggregate_results(plan: dict, match_results: list[dict]) -> dict:
    entrants = [item["entrantId"] for item in plan["entrants"]]
    standings = {
        entrant: {"wins": 0, "draws": 0, "losses": 0, "points": 0}
        for entrant in entrants
    }
    assignment = collections.Counter()
    reasons = collections.Counter()
    outcome_signatures = collections.Counter()
    first_pulse_matches = 0
    first_pulse_converted = 0
    max_ticks = 0
    draws = 0
    for item in match_results:
        match = item["header"]
        card = item["scorecard"]
        outcome = card["outcome"]
        reasons[outcome["completionReason"]] += 1
        if outcome["completionReason"] == "max-ticks":
            max_ticks += 1
        winner_team = outcome["winnerTeamId"]
        team0 = match["team0EntrantId"]
        team1 = match["team1EntrantId"]
        if winner_team is None:
            draws += 1
            for entrant in (team0, team1):
                standings[entrant]["draws"] += 1
                standings[entrant]["points"] += 1
            assignment[("draw", "draw")] += 1
        else:
            winner = match[f"team{winner_team}EntrantId"]
            loser = team1 if winner == team0 else team0
            standings[winner]["wins"] += 1
            standings[winner]["points"] += 3
            standings[loser]["losses"] += 1
            assignment[(f"team{winner_team}", winner)] += 1
        scoring = card["scoring"]
        if scoring["firstPulseTeamId"] is not None:
            first_pulse_matches += 1
            if scoring["firstPulseConvertedToMatchWinner"]:
                first_pulse_converted += 1
        outcome_signatures[score_outcome_signature(card, match)] += 1

    leave_one_out = {}
    for excluded in entrants:
        retained = [
            item for item in match_results
            if excluded not in (
                item["header"]["team0EntrantId"],
                item["header"]["team1EntrantId"],
            )
        ]
        leave_one_out[excluded] = {
            "retainedMatchCount": len(retained),
            "distinctCanonicalReplayHashes": len(
                {item["scorecard"]["source"]["canonicalReplayHash"] for item in retained}
            ),
            "distinctOutcomeSignatures": len(
                {score_outcome_signature(item["scorecard"], item["header"]) for item in retained}
            ),
            "winsByEntrant": dict(
                collections.Counter(
                    item["header"][f"team{item['scorecard']['outcome']['winnerTeamId']}EntrantId"]
                    for item in retained
                    if item["scorecard"]["outcome"]["winnerTeamId"] is not None
                )
            ),
        }

    return {
        "standings": standings,
        "assignmentWins": {
            f"{side}:{entrant}": count
            for (side, entrant), count in sorted(assignment.items())
        },
        "completionReasons": dict(sorted(reasons.items())),
        "distinctCanonicalReplayHashes": len(
            {item["scorecard"]["source"]["canonicalReplayHash"] for item in match_results}
        ),
        "distinctOutcomeSignatures": len(outcome_signatures),
        "outcomeSignatureCounts": dict(sorted(outcome_signatures.items())),
        "riskAlerts": {
            "firstPulseMatches": first_pulse_matches,
            "firstPulseConverted": first_pulse_converted,
            "firstPulseConversionRate": (
                first_pulse_converted / first_pulse_matches if first_pulse_matches else None
            ),
            "firstPulseDiagnosticThreshold": 0.70,
            "maxTicksMatches": max_ticks,
            "maxTicksRate": max_ticks / len(match_results) if match_results else None,
            "maxTicksDiagnosticThreshold": 0.20,
            "drawMatches": draws,
            "drawRate": draws / len(match_results) if match_results else None,
            "drawDiagnosticThreshold": 0.10,
        },
        "leaveOneDoctrineOut": leave_one_out,
    }


def choose_highlights(match_results: list[dict], count: int = 4) -> list[dict]:
    ranked = []
    for item in match_results:
        card = item["scorecard"]
        scoring = card["scoring"]
        possession = card["possession"]
        deliveries = sum(scoring["deliveriesByTeam"].values())
        pulses = sum(scoring["pulsesByTeam"].values())
        steals = sum(possession["stealsByTeam"].values())
        handoffs = sum(possession["handoffsByTeam"].values())
        reversals = scoring["behindToAheadPulseReversals"]
        rank = (reversals, pulses, steals + handoffs, deliveries, item["header"]["matchId"])
        ranked.append((rank, item))
    ranked.sort(reverse=True, key=lambda pair: pair[0])
    selected = []
    seen_pairs = set()
    for rank, item in ranked:
        if item["header"]["pairId"] in seen_pairs:
            continue
        selected.append((rank, item))
        seen_pairs.add(item["header"]["pairId"])
        if len(selected) == count:
            break
    if len(selected) < count:
        for rank, item in ranked:
            if any(chosen["header"]["matchId"] == item["header"]["matchId"] for _, chosen in selected):
                continue
            selected.append((rank, item))
            if len(selected) == count:
                break
    return [
        {
            "matchId": item["header"]["matchId"],
            "pairId": item["header"]["pairId"],
            "selectionTuple": list(rank[:-1]),
            "selectionMethod": "reversals, Pulses, steals+handoffs, deliveries, then matchId",
        }
        for rank, item in selected
    ]


def run_campaign(args: argparse.Namespace) -> int:
    output = args.output.resolve()
    freeze_path = output / "FROZEN.json"
    freeze = read_json(freeze_path)
    plan_path = output / freeze["planFile"]
    review_path = output / freeze["reviewSampleFile"]
    if sha256(args.cohort) != freeze["cohortSha256"]:
        raise RuntimeError("cohort changed after freeze")
    if sha256(plan_path) != freeze["planSha256"]:
        raise RuntimeError("campaign plan changed after freeze")
    if sha256(review_path) != freeze["reviewSampleSha256"]:
        raise RuntimeError("outcome-blind sample changed after freeze")
    cohort, entrants = validate_cohort(args.cohort)
    plan = read_json(plan_path)
    entrant_map = {item["entrantId"]: item for item in entrants}
    scorecard_module = load_scorecard_module()
    match_results = []
    canonical_verified = 0

    if (output / "results.json").exists():
        raise RuntimeError("results.json already exists; refusing to reopen outcomes")
    for index, match in enumerate(plan["matches"], start=1):
        match_dir = output / "matches" / match["matchId"]
        if match_dir.exists() and any(match_dir.iterdir()):
            raise RuntimeError(f"non-empty planned match directory: {match_dir}")
        team0 = entrant_map[match["team0EntrantId"]]
        team1 = entrant_map[match["team1EntrantId"]]
        print(f"[{index}/{len(plan['matches'])}] {match['matchId']}", flush=True)
        run_process(
            [
                sys.executable,
                str(MATCH_RUNNER),
                "--cli",
                str(args.cli.resolve()),
                "run",
                "--artifact0",
                str(team0["artifactPath"]),
                "--artifact1",
                str(team1["artifactPath"]),
                "--sheet0",
                str(team0["sheetPath"]),
                "--sheet1",
                str(team1["sheetPath"]),
                "--seed",
                match["seed"],
                "--output",
                str(match_dir),
                "--cohort-id",
                cohort["cohortId"],
                "--match-id",
                match["matchId"],
                "--entrant0-id",
                team0["entrantId"],
                "--entrant1-id",
                team1["entrantId"],
            ]
        )
        canonical = match_dir / "replay.json.gz"
        run_process(["dotnet", str(args.cli.resolve()), "verify", str(canonical)])
        canonical_verified += 1
        record_path = match_dir / "match-record.json"
        broadcast_path = match_dir / "broadcast.json.gz"
        record = read_json(record_path)
        card = scorecard_module.measure(
            read_json(broadcast_path), record, broadcast_path
        )
        if card["source"]["canonicalReplayHash"] != record["canonicalReplay"]["hash"]:
            raise RuntimeError(f"canonical hash mismatch in {match['matchId']}")
        match_results.append(
            {
                "header": match,
                "record": {
                    "path": str(record_path.relative_to(output)),
                    "bytes": record_path.stat().st_size,
                    "sha256": sha256(record_path),
                },
                "broadcast": {
                    "path": str(broadcast_path.relative_to(output)),
                    "gzipBytes": broadcast_path.stat().st_size,
                    "sha256": sha256(broadcast_path),
                },
                "durableBytes": record_path.stat().st_size + broadcast_path.stat().st_size,
                "scorecard": card,
            }
        )
        for name in ("replay.json.gz", "run.json", "match.log", "broadcast.log"):
            path = match_dir / name
            if path.exists():
                path.unlink()
        retained = sorted(path.name for path in match_dir.iterdir() if path.is_file())
        if retained != ["broadcast.json.gz", "match-record.json"]:
            raise RuntimeError(f"unexpected durable files in {match_dir}: {retained}")

    records = sorted((output / "matches").glob("*/match-record.json"))
    broadcasts = sorted((output / "matches").glob("*/broadcast.json.gz"))
    canonicals = sorted((output / "matches").glob("*/replay.json.gz"))
    if len(records) != len(plan["matches"]) or len(broadcasts) != len(plan["matches"]):
        raise RuntimeError("campaign completion must be counted from durable files")
    if canonicals:
        raise RuntimeError("canonical replay survived durable pruning")
    for item in match_results:
        if item["record"]["bytes"] > RECORD_LIMIT:
            raise RuntimeError("match record exceeded 4 KiB")
        if item["broadcast"]["gzipBytes"] > BROADCAST_LIMIT:
            raise RuntimeError("broadcast exceeded 300 KiB")
        if item["durableBytes"] > TOTAL_LIMIT:
            raise RuntimeError("durable match exceeded 304 KiB")

    result = {
        "schema": "arc-relay-native-campaign-results-v1",
        "cohortId": cohort["cohortId"],
        "freezeSha256": sha256(freeze_path),
        "reviewSampleSha256": sha256(review_path),
        "outcomeBlindSampleWasFrozenBeforeRun": True,
        "completionCountedFromFiles": {
            "matchRecordFiles": len(records),
            "broadcastFiles": len(broadcasts),
            "canonicalReplayFilesAfterPrune": len(canonicals),
            "canonicalReplaysVerifiedBeforePrune": canonical_verified,
        },
        "sizeBudget": {
            "recordCeilingBytes": RECORD_LIMIT,
            "broadcastCeilingBytes": BROADCAST_LIMIT,
            "durableCeilingBytes": TOTAL_LIMIT,
            "recordBytes": {
                "min": min(item["record"]["bytes"] for item in match_results),
                "max": max(item["record"]["bytes"] for item in match_results),
            },
            "broadcastGzipBytes": {
                "min": min(item["broadcast"]["gzipBytes"] for item in match_results),
                "max": max(item["broadcast"]["gzipBytes"] for item in match_results),
            },
            "durableBytes": {
                "min": min(item["durableBytes"] for item in match_results),
                "max": max(item["durableBytes"] for item in match_results),
            },
        },
        "aggregate": aggregate_results(plan, match_results),
        "matches": match_results,
    }
    write_json(output / "results.json", result)
    result_by_id = {item["header"]["matchId"]: item for item in match_results}
    frozen_review = read_json(review_path)
    gallery_sample = {
        "schema": "arc-relay-outcome-blind-gallery-input-v1",
        "cohortId": cohort["cohortId"],
        "frozenReviewSampleSha256": sha256(review_path),
        "containsOutcomesScoresOrDurations": False,
        "replays": [
            {
                "reviewId": item["reviewId"],
                "matchId": item["matchId"],
                "pairId": item["pairId"],
                "assignment": item["assignment"],
                "rules": item["pairId"],
                "map": f"Threefold assignment {item['assignment']}",
                "source": str(
                    (output / result_by_id[item["matchId"]]["broadcast"]["path"])
                    .resolve()
                ),
            }
            for item in frozen_review["matches"]
        ],
    }
    write_json(output / "gallery-sample.json", gallery_sample)
    highlights = {
        "schema": "arc-relay-outcome-aware-highlights-v1",
        "cohortId": cohort["cohortId"],
        "separateFromOutcomeBlindSample": True,
        "matches": choose_highlights(match_results),
    }
    write_json(output / "highlights.json", highlights)
    gallery_highlights = {
        "schema": "arc-relay-outcome-aware-highlight-gallery-input-v1",
        "cohortId": cohort["cohortId"],
        "separateFromOutcomeBlindSample": True,
        "replays": [
            {
                "matchId": item["matchId"],
                "pairId": item["pairId"],
                "rules": item["pairId"],
                "map": "Threefold curated highlight",
                "source": str(
                    (output / result_by_id[item["matchId"]]["broadcast"]["path"])
                    .resolve()
                ),
            }
            for item in highlights["matches"]
        ],
    }
    write_json(output / "gallery-highlights.json", gallery_highlights)
    print(
        f"complete: {len(records)} records + {len(broadcasts)} broadcasts; "
        f"{canonical_verified} canonical replays verified and 0 retained",
        flush=True,
    )
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    freeze = sub.add_parser("prepare")
    freeze.add_argument("--cohort", required=True, type=Path)
    freeze.add_argument("--output", required=True, type=Path)
    freeze.set_defaults(handler=prepare)
    run = sub.add_parser("run")
    run.add_argument("--cohort", required=True, type=Path)
    run.add_argument("--output", required=True, type=Path)
    run.add_argument("--cli", type=Path, default=DEFAULT_CLI)
    run.set_defaults(handler=run_campaign)
    args = parser.parse_args()
    return args.handler(args)


if __name__ == "__main__":
    raise SystemExit(main())
