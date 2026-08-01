#!/usr/bin/env python3
"""Freeze a registered Arc Relay sweep plan before running outcomes."""

from __future__ import annotations

import argparse
import hashlib
import itertools
import json
import os
from pathlib import Path
import random
import subprocess
from typing import Any


REPO = Path(__file__).resolve().parent.parent
DEFAULT_CLI = REPO / "src/BotArena.Cli/bin/Debug/net10.0/botarena.dll"
BARS = REPO / "balance/arc-relay-felt-degeneracy-bars-v1.json"


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path}: JSON root must be an object")
    return value


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def resolve(base: Path, value: str) -> Path:
    path = Path(value)
    return path.resolve() if path.is_absolute() else (base / path).resolve()


def contract(
    cli: Path,
    profile: str,
    sheet0: Path,
    sheet1: Path,
) -> dict[str, Any]:
    completed = subprocess.run(
        [
            "dotnet",
            str(cli),
            "experiment",
            "arc-relay",
            "--sheet0",
            str(sheet0),
            "--sheet1",
            str(sheet1),
            "--loop-profile",
            profile,
            "--print-contract",
        ],
        cwd=REPO,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError(completed.stderr or completed.stdout)
    value = json.loads(completed.stdout)
    if not isinstance(value, dict):
        raise ValueError("contract output was not an object")
    return value


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cohort", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--sweep-id", required=True)
    parser.add_argument("--profile", required=True)
    parser.add_argument("--seeds", required=True)
    parser.add_argument("--include", action="append", default=[])
    parser.add_argument("--runtime", choices=("wasm", "in-process"), default="wasm")
    parser.add_argument(
        "--blind-review-order-seed",
        type=int,
        help=(
            "Freeze an outcome-blind review order into the plan before any "
            "cell runs. The review contains cell identities only."
        ),
    )
    parser.add_argument("--engine-version", default="1.0.5")
    parser.add_argument("--cli", type=Path, default=DEFAULT_CLI)
    args = parser.parse_args()

    cohort_path = args.cohort.resolve()
    cohort = read_json(cohort_path)
    raw_entrants = cohort.get("entrants")
    if not isinstance(raw_entrants, list) or len(raw_entrants) < 2:
        raise ValueError("cohort needs at least two entrants")
    include = set(args.include)
    entrants: dict[str, dict[str, Any]] = {}
    resolved: dict[str, dict[str, Path]] = {}
    for item in raw_entrants:
        entrant_id = item["entrantId"]
        if include and entrant_id not in include:
            continue
        artifact = resolve(cohort_path.parent, item["artifact"])
        sheet = resolve(cohort_path.parent, item["sheet"])
        if not artifact.is_file() or not sheet.is_file():
            raise FileNotFoundError(f"{entrant_id}: missing artifact or sheet")
        artifact_hash = sha256(artifact)
        sheet_hash = sha256(sheet)
        if item.get("artifactSha256") not in (None, artifact_hash):
            raise ValueError(f"{entrant_id}: artifact hash declaration moved")
        if item.get("sheetSha256") not in (None, sheet_hash):
            raise ValueError(f"{entrant_id}: sheet hash declaration moved")
        entrants[entrant_id] = {
            "artifact": os.path.relpath(artifact, REPO),
            "artifactSha256": artifact_hash,
            "sheet": os.path.relpath(sheet, REPO),
            "sheetSha256": sheet_hash,
        }
        resolved[entrant_id] = {"artifact": artifact, "sheet": sheet}
    if len(entrants) < 2:
        raise ValueError("selected sweep population needs at least two entrants")

    seeds = [value.strip() for value in args.seeds.split(",") if value.strip()]
    if not seeds:
        raise ValueError("--seeds needs at least one value")
    cells: list[dict[str, Any]] = []
    common: dict[str, str] | None = None
    for seed in seeds:
        for first, second in itertools.combinations(sorted(entrants), 2):
            pair_id = f"{first}--{second}"
            for assignment, (team0, team1) in enumerate(
                ((first, second), (second, first))
            ):
                resolved_contract = contract(
                    args.cli.resolve(),
                    args.profile,
                    resolved[team0]["sheet"],
                    resolved[team1]["sheet"],
                )
                identity = {
                    "rulesetId": resolved_contract["rules"]["rulesetId"],
                    "rulesFingerprint": resolved_contract["rules"][
                        "rulesFingerprint"],
                    "mapId": resolved_contract["map"]["mapId"],
                    "mapFingerprint": resolved_contract["map"]["mapFingerprint"],
                }
                if common is None:
                    common = identity
                elif common != identity:
                    raise ValueError("rules/map identity changed within one sweep")
                cells.append({
                    "cellId": f"{pair_id}--s{seed}--a{assignment}",
                    "seed": seed,
                    "team0": team0,
                    "team1": team1,
                    "topologyFingerprint": resolved_contract["topology"][
                        "topologyFingerprint"],
                    "matchContractFingerprint": resolved_contract[
                        "matchContractFingerprint"],
                })
    assert common is not None
    plan = {
        "schema": "arc-relay-sweep-plan-v1",
        "sweepId": args.sweep_id,
        "preparedBeforeOutcomes": True,
        "cohortId": cohort["cohortId"],
        "cohortFile": os.path.relpath(cohort_path, REPO),
        "cohortSha256": sha256(cohort_path),
        "runtime": args.runtime,
        "loopProfile": args.profile,
        "engineVersion": args.engine_version,
        **common,
        "eligibilityBars": os.path.relpath(BARS, REPO),
        "eligibilityBarsSha256": sha256(BARS),
        "entrants": entrants,
        "cells": cells,
    }
    if args.blind_review_order_seed is not None:
        review_cells = [cell["cellId"] for cell in cells]
        random.Random(args.blind_review_order_seed).shuffle(review_cells)
        plan["outcomeBlindReview"] = {
            "schema": "arc-relay-sweep-blind-review-v1",
            "preparedBeforeOutcomes": True,
            "containsOutcomesScoresOrDurations": False,
            "orderSeed": str(args.blind_review_order_seed),
            "cells": [
                {
                    "reviewId": f"blind-{index + 1:02d}",
                    "cellId": cell_id,
                }
                for index, cell_id in enumerate(review_cells)
            ],
        }
    output = args.output.resolve()
    if output.exists():
        raise FileExistsError(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        json.dumps(plan, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(
        f"froze {len(cells)} {args.profile} cells before outcomes: "
        f"{sha256(output)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
