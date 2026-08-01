#!/usr/bin/env python3
"""Prepare and run the provisional Arc Relay commander depth audit.

This is evaluation infrastructure, not the player-facing sheet format. It
crosses four coverage families with static and ordered-gambit variants while
holding the frozen stock-mind source byte-identical. The stock algorithm and
stable data linker build once; every separately hashed sheet is supplied as
participant-local deterministic data by the Arc Relay runner.
"""

from __future__ import annotations

import argparse
import collections
import gzip
import hashlib
import importlib.util
import itertools
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time


REPO = Path(__file__).resolve().parent.parent
STOCK = REPO / "arena-bots/arc-relay/stock-mind-v0"
GENERATOR = REPO / "scripts/generate-arc-relay-sheet.py"
MATCH_RUNNER = REPO / "scripts/arc-relay-match.py"
SCORECARD = REPO / "scripts/arc-relay-scorecard.py"
DEFAULT_CLI = REPO / "src/BotArena.Cli/bin/Debug/net10.0/botarena.dll"
STOCK_SOURCE_SHA256 = "c8182e133a202733ef7c6b43367097eb118d2295a91dcdbf592e6fe13ff48f79"
STOCK_LINKER = STOCK / "StockSheet.cs"
SEED = "130363"

RECORD_LIMIT = 4 * 1024
BROADCAST_LIMIT = 300 * 1024
TOTAL_LIMIT = 304 * 1024


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


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


def load_scorecard_module():
    spec = importlib.util.spec_from_file_location("arc_relay_scorecard", SCORECARD)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot import {SCORECARD}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def run_process(command: list[str]) -> str:
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
            + completed.stdout[-5000:]
        )
    return completed.stdout


def contract(cli: Path, sheet0: Path, sheet1: Path) -> dict:
    return json.loads(run_process([
        "dotnet", str(cli.resolve()), "experiment", "arc-relay",
        "--sheet0", str(sheet0.resolve()),
        "--sheet1", str(sheet1.resolve()),
        "--loop-profile", "h0", "--print-contract",
    ]))


PATHS = {
    "north-fast": {
        "outbound": [[4, 6], [8, 6], [13, 6], [15, 4]],
        "return": [[13, 6], [9, 6], [5, 6], [2, 11]],
    },
    "north-safe": {
        "outbound": [[4, 9], [8, 9], [13, 8], [15, 4]],
        "return": [[13, 6], [9, 9], [5, 9], [3, 10], [2, 11]],
    },
    "centre-fast": {
        "outbound": [[4, 9], [8, 9], [13, 9], [15, 11]],
        "return": [[13, 9], [9, 9], [5, 9], [2, 11]],
    },
    "centre-safe": {
        "outbound": [[4, 13], [8, 13], [13, 13], [15, 11]],
        "return": [[13, 13], [9, 13], [5, 13], [3, 12], [2, 11]],
    },
    "south-fast": {
        "outbound": [[4, 16], [8, 16], [13, 16], [15, 18]],
        "return": [[13, 16], [9, 16], [5, 16], [2, 11]],
    },
    "south-safe": {
        "outbound": [[4, 13], [8, 13], [13, 14], [15, 18]],
        "return": [[13, 16], [9, 13], [5, 13], [3, 12], [2, 11]],
    },
}


FAMILIES = [
    {
        "familyId": "balanced",
        "composition": [
            "kestrel", "towline", "relay", "palisade",
            "switchback", "hush", "lantern", "patchbay",
        ],
        "slots": [
            ("north", "carrier", 1, "north-safe"),
            ("north", "screen", 0, "north-safe"),
            ("centre", "carrier", 3, "centre-safe"),
            ("centre", "screen", 2, "centre-fast"),
            ("south", "carrier", 5, "south-safe"),
            ("south", "intercept", 4, "south-fast"),
            ("centre", "intercept", 2, "centre-fast"),
            ("centre", "reserve", 3, "centre-safe"),
        ],
        "dimensions": {
            "allocation": "2 north / 4 centre / 2 south",
            "returnStyle": "mixed-safe",
            "handoffShape": "paired by theater",
            "policyStyle": "assigned-theater balanced",
        },
        "policies": {
            "carrier": {
                "handoffHealthAtOrBelow": 2,
                "preferAssignedTheater": True,
                "routeFailureTicks": 12,
            },
            "escort": {"followDistance": 1, "focusEnemyCarrier": True},
            "interception": {"focusEnemyCarrier": True, "looseCoreFallback": True},
        },
    },
    {
        "familyId": "split-fast",
        "composition": [
            "kestrel", "kestrel", "relay", "relay",
            "lantern", "towline", "hush", "patchbay",
        ],
        "slots": [
            ("north", "carrier", 1, "north-fast"),
            ("north", "screen", 0, "north-fast"),
            ("centre", "carrier", 6, "centre-fast"),
            ("south", "carrier", 5, "south-fast"),
            ("north", "intercept", 0, "north-fast"),
            ("south", "screen", 3, "south-fast"),
            ("centre", "intercept", 2, "centre-fast"),
            ("south", "reserve", 3, "south-fast"),
        ],
        "dimensions": {
            "allocation": "3 north / 2 centre / 3 south",
            "returnStyle": "fast-direct",
            "handoffShape": "short paired catches",
            "policyStyle": "low-health handoff and early route release",
        },
        "policies": {
            "carrier": {
                "handoffHealthAtOrBelow": 1,
                "preferAssignedTheater": True,
                "routeFailureTicks": 8,
            },
            "escort": {"followDistance": 2, "focusEnemyCarrier": True},
            "interception": {"focusEnemyCarrier": True, "looseCoreFallback": True},
        },
    },
    {
        "familyId": "convoy-safe",
        "composition": [
            "relay", "relay", "palisade", "palisade",
            "patchbay", "hush", "towline", "lantern",
        ],
        "slots": [
            ("centre", "carrier", 1, "centre-safe"),
            ("centre", "carrier", 0, "centre-safe"),
            ("centre", "screen", 0, "centre-safe"),
            ("centre", "screen", 1, "centre-fast"),
            ("centre", "screen", 0, "centre-safe"),
            ("centre", "intercept", 1, "centre-fast"),
            ("north", "reserve", 0, "north-safe"),
            ("south", "reserve", 1, "south-safe"),
        ],
        "dimensions": {
            "allocation": "1 north / 6 centre / 1 south",
            "returnStyle": "safe-covered",
            "handoffShape": "central relay chain",
            "policyStyle": "high-health handoff and tight escort",
        },
        "policies": {
            "carrier": {
                "handoffHealthAtOrBelow": 3,
                "preferAssignedTheater": False,
                "routeFailureTicks": 18,
            },
            "escort": {"followDistance": 1, "focusEnemyCarrier": True},
            "interception": {"focusEnemyCarrier": False, "looseCoreFallback": True},
        },
    },
    {
        "familyId": "intercept-switch",
        "composition": [
            "towline", "towline", "sunder", "sunder",
            "longshot", "repulsor", "lantern", "relay",
        ],
        "slots": [
            ("north", "intercept", 4, "north-safe"),
            ("south", "intercept", 5, "south-safe"),
            ("centre", "intercept", 3, "centre-fast"),
            ("centre", "screen", 7, "centre-safe"),
            ("north", "screen", 0, "north-fast"),
            ("south", "screen", 1, "south-fast"),
            ("centre", "reserve", 2, "centre-safe"),
            ("centre", "carrier", 3, "centre-fast"),
        ],
        "dimensions": {
            "allocation": "2 north / 4 centre / 2 south",
            "returnStyle": "safe outbound with route switching",
            "handoffShape": "central recovery pair",
            "policyStyle": "open-theater interception",
        },
        "policies": {
            "carrier": {
                "handoffHealthAtOrBelow": 2,
                "preferAssignedTheater": False,
                "routeFailureTicks": 10,
            },
            "escort": {"followDistance": 2, "focusEnemyCarrier": True},
            "interception": {"focusEnemyCarrier": True, "looseCoreFallback": True},
        },
    },
]


GAMBITS = [
    {
        "priority": 10,
        "id": "pulse-backstop",
        "trigger": "after-enemy-pulse",
        "durationTicks": 24,
        "cooldownTicks": 60,
        "scopeRoles": ["reserve"],
        "roleOverride": "intercept",
        "rallyLine": "home",
    },
    {
        "priority": 20,
        "id": "two-core-collapse",
        "trigger": "double-enemy-possession",
        "durationTicks": 16,
        "cooldownTicks": 32,
        "scopeRoles": ["screen", "reserve"],
        "roleOverride": "intercept",
        "rallyLine": "middle",
    },
    {
        "priority": 30,
        "id": "pulse-release",
        "trigger": "after-own-pulse",
        "durationTicks": 18,
        "cooldownTicks": 45,
        "scopeRoles": ["reserve"],
        "roleOverride": "carrier",
        "rallyLine": "forward",
    },
]


def sheet(family: dict, style: str) -> dict:
    slots = []
    for unit_id, (theater_id, role, partner, path_id) in enumerate(family["slots"]):
        route = PATHS[path_id]
        slots.append(
            {
                "unitId": unit_id,
                "theater": theater_id,
                "role": role,
                "partnerUnitId": partner,
                "outboundPath": route["outbound"],
                "returnPath": route["return"],
            }
        )
    return {
        "schema": "arc-relay-evaluation-sheet-v0",
        "sheetId": f"depth-{family['familyId']}-{style}-v1",
        "mapId": "arc-relay-threefold-01",
        "auditStatus": {
            "provisionalEvaluationOnly": True,
            "playerFacingProductSchema": False,
            "purpose": "Gate 3 coverage and reproducibility",
        },
        "auditDimensions": {**family["dimensions"], "adaptationStyle": style},
        "composition": family["composition"],
        "slots": slots,
        "zones": {
            "north": [11, 1, 19, 8],
            "centre": [10, 7, 20, 15],
            "south": [11, 14, 19, 21],
            "intercept": [9, 2, 21, 20],
        },
        "rallyLines": {
            "home": [[4, 9], [4, 11], [4, 13]],
            "middle": [[9, 6], [9, 9], [9, 13], [9, 16]],
            "forward": [[13, 6], [13, 9], [13, 13], [13, 16]],
        },
        "policies": family["policies"],
        "gambits": GAMBITS if style == "gambit" else [],
    }


def build_project_file(destination: Path) -> None:
    original = (STOCK / "ArcRelayStockMind.csproj").read_text(encoding="utf-8")
    sdk = REPO / "src/BotArena.Sdk/BotArena.Sdk.csproj"
    relative_sdk = os.path.relpath(sdk, destination)
    original = original.replace(
        "../../../src/BotArena.Sdk/BotArena.Sdk.csproj",
        relative_sdk,
    )
    (destination / "ArcRelayStockMind.csproj").write_text(original, encoding="utf-8")


def prepare(args: argparse.Namespace) -> int:
    output = args.output.resolve()
    if output.exists() and any(output.iterdir()):
        raise RuntimeError(f"refusing non-empty audit output: {output}")
    if sha256(STOCK / "ArcRelayStockMind.cs") != STOCK_SOURCE_SHA256:
        raise RuntimeError("frozen stock-mind source hash changed")
    output.mkdir(parents=True, exist_ok=True)

    algorithm_dir = output / "stock-algorithm"
    algorithm_dir.mkdir()
    shutil.copy2(STOCK / "ArcRelayStockMind.cs", algorithm_dir)
    shutil.copy2(STOCK_LINKER, algorithm_dir)
    shutil.copy2(STOCK / "botarena.json", algorithm_dir)
    build_project_file(algorithm_dir)
    build_started = time.perf_counter()
    run_process(
        [
            "dotnet",
            str(args.cli.resolve()),
            "build",
            str(algorithm_dir),
            "--no-cache",
        ]
    )
    build_seconds = time.perf_counter() - build_started
    artifact = algorithm_dir / "out" / "bot.wasm"
    if not artifact.is_file():
        raise RuntimeError(f"build did not produce {artifact}")
    algorithm = {
        "schema": "arc-relay-stock-algorithm-manifest-v1",
        "buildCount": 1,
        "stockMindSourceSha256": STOCK_SOURCE_SHA256,
        "dataLinkerSha256": sha256(STOCK_LINKER),
        "artifactSha256": sha256(artifact),
        "artifactBytes": artifact.stat().st_size,
        "buildSeconds": round(build_seconds, 6),
        "sheetDelivery": "participant-local deterministic WASI data",
    }
    write_json(algorithm_dir / "manifest.json", algorithm)

    variants = []
    for family in FAMILIES:
        for style in ("static", "gambit"):
            variant_id = f"{family['familyId']}--{style}"
            destination = output / "variants" / variant_id
            destination.mkdir(parents=True)
            sheet_path = destination / "sheet.json"
            write_json(sheet_path, sheet(family, style))
            run_process(
                [
                    sys.executable,
                    str(GENERATOR),
                    str(sheet_path),
                    "--validate-only",
                ]
            )
            manifest = {
                "schema": "arc-relay-depth-variant-manifest-v1",
                "variantId": variant_id,
                "familyId": family["familyId"],
                "adaptationStyle": style,
                "evaluationSchemaProvisional": True,
                "productSheetSchema": False,
                "stockMindSourceSha256": STOCK_SOURCE_SHA256,
                "sheetSha256": sha256(sheet_path),
                "artifactSha256": algorithm["artifactSha256"],
                "artifactBytes": algorithm["artifactBytes"],
                "buildPolicy": "one shared frozen stock-algorithm build",
                "sheetDelivery": algorithm["sheetDelivery"],
                "dimensions": sheet(family, style)["auditDimensions"],
            }
            write_json(destination / "manifest.json", manifest)
            variants.append(
                {
                    **manifest,
                    "artifact": str(artifact.relative_to(output)),
                    "sheet": str(sheet_path.relative_to(output)),
                    "manifest": str((destination / "manifest.json").relative_to(output)),
                }
            )
            print(f"prepared {variant_id}: {manifest['artifactSha256']}", flush=True)

    matches = []
    for first, second in itertools.combinations(variants, 2):
        if (args.within_family_only
                and (first["familyId"] != second["familyId"]
                     or first["adaptationStyle"]
                     == second["adaptationStyle"])):
            continue
        pair_id = "--vs--".join((first["variantId"], second["variantId"]))
        for assignment, (team0, team1) in enumerate(((first, second), (second, first))):
            matches.append(
                {
                    "matchId": f"{pair_id}--s{SEED}--a{assignment}",
                    "pairId": pair_id,
                    "seed": SEED,
                    "assignment": assignment,
                    "team0VariantId": team0["variantId"],
                    "team1VariantId": team1["variantId"],
                }
            )
    plan = {
        "schema": "arc-relay-depth-audit-plan-v1",
        "status": "provisional-evaluation-not-product-schema",
        "purpose": "coverage and reproducibility for Gate 3",
        "playerFacingSheetDesignDeferredUntilAfterGate3": True,
        "previewPlaygroundDeferred": True,
        "buildSpeedOptimizationInScope": True,
        "stockMindSourceSha256": STOCK_SOURCE_SHA256,
        "stockAlgorithm": algorithm,
        "seed": SEED,
        "variantCount": len(variants),
        "plannedMatchCount": len(matches),
        "variants": variants,
        "matches": matches,
    }
    plan_path = output / "audit-plan.json"
    write_json(plan_path, plan)
    freeze = {
        "schema": "arc-relay-depth-audit-freeze-v1",
        "outcomesExistedAtFreeze": False,
        "auditPlan": plan_path.name,
        "auditPlanSha256": sha256(plan_path),
        "stockMindSourceSha256": STOCK_SOURCE_SHA256,
        "seed": SEED,
        "variantCount": len(variants),
        "plannedMatchCount": len(matches),
    }
    write_json(output / "FROZEN.json", freeze)

    entrants = {
        item["variantId"]: {
            "artifact": os.path.relpath(output / item["artifact"], REPO),
            "artifactSha256": item["artifactSha256"],
            "sheet": os.path.relpath(output / item["sheet"], REPO),
            "sheetSha256": item["sheetSha256"],
        }
        for item in variants
    }
    cells = []
    common = None
    for match in matches:
        team0 = variants[[item["variantId"] for item in variants].index(
            match["team0VariantId"])]
        team1 = variants[[item["variantId"] for item in variants].index(
            match["team1VariantId"])]
        resolved = contract(
            args.cli,
            output / team0["sheet"],
            output / team1["sheet"],
        )
        identity = {
            "rulesetId": resolved["rules"]["rulesetId"],
            "rulesFingerprint": resolved["rules"]["rulesFingerprint"],
            "mapId": resolved["map"]["mapId"],
            "mapFingerprint": resolved["map"]["mapFingerprint"],
        }
        if common is None:
            common = identity
        elif common != identity:
            raise RuntimeError("depth sweep rules/map identity changed")
        cells.append({
            "cellId": match["matchId"],
            "seed": match["seed"],
            "team0": match["team0VariantId"],
            "team1": match["team1VariantId"],
            "topologyFingerprint": resolved["topology"]["topologyFingerprint"],
            "matchContractFingerprint": resolved["matchContractFingerprint"],
        })
    assert common is not None
    sweep = {
        "schema": "arc-relay-sweep-plan-v1",
        "sweepId": "gate3-2-gambit-grammar-within-family-v2",
        "preparedBeforeOutcomes": True,
        "cohortId": "arc-relay-depth-gambit-grammar-v2",
        "runtime": "wasm",
        "loopProfile": "h0",
        "engineVersion": "1.0.5",
        **common,
        "entrants": entrants,
        "cells": cells,
    }
    write_json(output / "sweep-plan.json", sweep)
    freeze["sweepPlan"] = "sweep-plan.json"
    freeze["sweepPlanSha256"] = sha256(output / "sweep-plan.json")
    write_json(output / "FROZEN.json", freeze)
    print(
        f"froze provisional depth audit: {len(variants)} variants, "
        f"{len(matches)} mirrored matches",
        flush=True,
    )
    return 0


def outcome_signature(card: dict, match: dict) -> str:
    winner_team = card["outcome"]["winnerTeamId"]
    winner = "draw" if winner_team is None else match[f"team{winner_team}VariantId"]
    scores = card["outcome"]["scoresByTeam"]
    return "|".join(
        (
            winner,
            card["outcome"]["completionReason"],
            str(scores["0"].get("pulses", 0)),
            str(scores["0"].get("reactor-charge", 0)),
            str(scores["1"].get("pulses", 0)),
            str(scores["1"].get("reactor-charge", 0)),
        )
    )


def summarize_match(card: dict) -> dict:
    return {
        "outcome": card["outcome"],
        "canonicalReplayHash": card["source"]["canonicalReplayHash"],
        "births": card["coreCadence"]["actualBirths"],
        "possessionTicksByTeam": card["possession"]["ticksByTeam"],
        "stealsByTeam": card["possession"]["stealsByTeam"],
        "handoffsByTeam": card["possession"]["handoffsByTeam"],
        "deliveriesByTeam": card["scoring"]["deliveriesByTeam"],
        "pulsesByTeam": card["scoring"]["pulsesByTeam"],
        "firstPulseTeamId": card["scoring"]["firstPulseTeamId"],
        "firstPulseConverted": card["scoring"]["firstPulseConvertedToMatchWinner"],
        "pulseLeadChanges": card["scoring"]["pulseLeadChanges"],
        "behindToAheadPulseReversals": card["scoring"]["behindToAheadPulseReversals"],
        "homeCampBodyTicksByTeam": card["fieldShape"]["homeCampBodyTicksByTeam"],
        "theatersByTeam": card["fieldShape"]["theatersByTeam"],
        "signatureUseByTeam": card["signatures"]["byTeamAndSignature"],
    }


def payoff(plan: dict, matches: list[dict]) -> dict:
    variants = [item["variantId"] for item in plan["variants"]]
    totals = {
        variant: {"wins": 0, "draws": 0, "losses": 0, "points": 0}
        for variant in variants
    }
    cells: dict[tuple[str, str], dict] = {}
    style_cross = {"gambit": {"wins": 0, "draws": 0, "losses": 0}}
    same_family_cross = {family["familyId"]: {"wins": 0, "draws": 0, "losses": 0} for family in FAMILIES}
    variant_info = {item["variantId"]: item for item in plan["variants"]}
    for row in variants:
        for column in variants:
            if row == column:
                continue
            relevant = [
                item for item in matches
                if {item["header"]["team0VariantId"], item["header"]["team1VariantId"]}
                == {row, column}
            ]
            cell = {"wins": 0, "draws": 0, "losses": 0, "points": 0}
            signatures = set()
            hashes = set()
            for item in relevant:
                winner_team = item["scorecard"]["outcome"]["winnerTeamId"]
                winner = (
                    None
                    if winner_team is None
                    else item["header"][f"team{winner_team}VariantId"]
                )
                if winner is None:
                    cell["draws"] += 1
                    cell["points"] += 1
                elif winner == row:
                    cell["wins"] += 1
                    cell["points"] += 3
                else:
                    cell["losses"] += 1
                signatures.add(outcome_signature(item["scorecard"], item["header"]))
                hashes.add(item["scorecard"]["canonicalReplayHash"])
            cell["distinctOutcomeSignatures"] = len(signatures)
            cell["distinctCanonicalReplayHashes"] = len(hashes)
            cells[(row, column)] = cell

    for item in matches:
        header = item["header"]
        card = item["scorecard"]
        team0 = header["team0VariantId"]
        team1 = header["team1VariantId"]
        winner_team = card["outcome"]["winnerTeamId"]
        if winner_team is None:
            for variant in (team0, team1):
                totals[variant]["draws"] += 1
                totals[variant]["points"] += 1
        else:
            winner = header[f"team{winner_team}VariantId"]
            loser = team1 if winner == team0 else team0
            totals[winner]["wins"] += 1
            totals[winner]["points"] += 3
            totals[loser]["losses"] += 1

        left = variant_info[team0]
        right = variant_info[team1]
        if left["adaptationStyle"] != right["adaptationStyle"]:
            gambit_variant = team0 if left["adaptationStyle"] == "gambit" else team1
            if winner_team is None:
                style_cross["gambit"]["draws"] += 1
            elif header[f"team{winner_team}VariantId"] == gambit_variant:
                style_cross["gambit"]["wins"] += 1
            else:
                style_cross["gambit"]["losses"] += 1
            if left["familyId"] == right["familyId"]:
                family_result = same_family_cross[left["familyId"]]
                if winner_team is None:
                    family_result["draws"] += 1
                elif header[f"team{winner_team}VariantId"] == gambit_variant:
                    family_result["wins"] += 1
                else:
                    family_result["losses"] += 1

    matrix = {
        row: {column: cells[(row, column)] for column in variants if column != row}
        for row in variants
    }
    return {
        "totalsByVariant": totals,
        "payoffMatrix": matrix,
        "gambitVersusStaticAllFamilies": style_cross["gambit"],
        "gambitVersusStaticWithinFamily": same_family_cross,
        "distinctCanonicalReplayHashes": len(
            {item["scorecard"]["canonicalReplayHash"] for item in matches}
        ),
        "distinctOutcomeSignatures": len(
            {outcome_signature(item["scorecard"], item["header"]) for item in matches}
        ),
    }


def run_audit(args: argparse.Namespace) -> int:
    output = args.output.resolve()
    freeze_path = output / "FROZEN.json"
    freeze = read_json(freeze_path)
    plan_path = output / freeze["auditPlan"]
    if sha256(plan_path) != freeze["auditPlanSha256"]:
        raise RuntimeError("depth-audit plan changed after freeze")
    if (output / "results.json").exists():
        raise RuntimeError("results already exist; refusing to reopen audit outcomes")
    plan = read_json(plan_path)
    variants = {item["variantId"]: item for item in plan["variants"]}
    scorecard_module = load_scorecard_module()
    results = []
    verified = 0
    for index, match in enumerate(plan["matches"], start=1):
        match_dir = output / "matches" / match["matchId"]
        if match_dir.exists() and any(match_dir.iterdir()):
            raise RuntimeError(f"non-empty planned match directory: {match_dir}")
        team0 = variants[match["team0VariantId"]]
        team1 = variants[match["team1VariantId"]]
        print(f"[{index}/{len(plan['matches'])}] {match['matchId']}", flush=True)
        run_process(
            [
                sys.executable,
                str(MATCH_RUNNER),
                "--cli",
                str(args.cli.resolve()),
                "run",
                "--artifact0",
                str(output / team0["artifact"]),
                "--artifact1",
                str(output / team1["artifact"]),
                "--sheet0",
                str(output / team0["sheet"]),
                "--sheet1",
                str(output / team1["sheet"]),
                "--seed",
                match["seed"],
                "--output",
                str(match_dir),
                "--cohort-id",
                "arc-relay-depth-audit-v1-2026-08-01",
                "--match-id",
                match["matchId"],
                "--entrant0-id",
                team0["variantId"],
                "--entrant1-id",
                team1["variantId"],
            ]
        )
        canonical = match_dir / "replay.json.gz"
        run_process(["dotnet", str(args.cli.resolve()), "verify", str(canonical)])
        verified += 1
        record_path = match_dir / "match-record.json"
        broadcast_path = match_dir / "broadcast.json.gz"
        record = read_json(record_path)
        full_card = scorecard_module.measure(
            read_json(broadcast_path), record, broadcast_path
        )
        durable = record_path.stat().st_size + broadcast_path.stat().st_size
        if record_path.stat().st_size > RECORD_LIMIT:
            raise RuntimeError("depth match record exceeded 4 KiB")
        if broadcast_path.stat().st_size > BROADCAST_LIMIT:
            raise RuntimeError("depth broadcast exceeded 300 KiB")
        if durable > TOTAL_LIMIT:
            raise RuntimeError("depth durable match exceeded 304 KiB")
        results.append(
            {
                "header": match,
                "record": str(record_path.relative_to(output)),
                "broadcast": str(broadcast_path.relative_to(output)),
                "recordBytes": record_path.stat().st_size,
                "broadcastGzipBytes": broadcast_path.stat().st_size,
                "durableBytes": durable,
                "scorecard": summarize_match(full_card),
            }
        )
        for name in ("replay.json.gz", "run.json", "match.log", "broadcast.log"):
            path = match_dir / name
            if path.exists():
                path.unlink()
        retained = sorted(path.name for path in match_dir.iterdir() if path.is_file())
        if retained != ["broadcast.json.gz", "match-record.json"]:
            raise RuntimeError(f"unexpected durable files in {match_dir}: {retained}")

    records = list((output / "matches").glob("*/match-record.json"))
    broadcasts = list((output / "matches").glob("*/broadcast.json.gz"))
    canonicals = list((output / "matches").glob("*/replay.json.gz"))
    if len(records) != len(plan["matches"]) or len(broadcasts) != len(plan["matches"]):
        raise RuntimeError("depth completion must be counted from durable files")
    if canonicals:
        raise RuntimeError("canonical replay survived depth pruning")
    result = {
        "schema": "arc-relay-depth-audit-results-v1",
        "status": "provisional-evaluation-not-product-schema",
        "stockMindSourceSha256": STOCK_SOURCE_SHA256,
        "planSha256": sha256(plan_path),
        "completionCountedFromFiles": {
            "matchRecords": len(records),
            "broadcasts": len(broadcasts),
            "canonicalReplaysVerifiedBeforePrune": verified,
            "canonicalReplaysAfterPrune": len(canonicals),
        },
        "sizeBudget": {
            "maxRecordBytes": max(item["recordBytes"] for item in results),
            "maxBroadcastGzipBytes": max(item["broadcastGzipBytes"] for item in results),
            "maxDurableBytes": max(item["durableBytes"] for item in results),
            "hardRecordBytes": RECORD_LIMIT,
            "hardBroadcastBytes": BROADCAST_LIMIT,
            "hardDurableBytes": TOTAL_LIMIT,
        },
        "read": payoff(plan, results),
        "matches": results,
    }
    write_json(output / "results.json", result)
    print(
        f"complete: {len(records)} depth records + {len(broadcasts)} broadcasts; "
        f"{verified} canonical replays verified and 0 retained",
        flush=True,
    )
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    freeze = sub.add_parser("prepare")
    freeze.add_argument("--output", required=True, type=Path)
    freeze.add_argument("--cli", type=Path, default=DEFAULT_CLI)
    freeze.add_argument("--within-family-only", action="store_true")
    freeze.set_defaults(handler=prepare)
    run = sub.add_parser("run")
    run.add_argument("--output", required=True, type=Path)
    run.add_argument("--cli", type=Path, default=DEFAULT_CLI)
    run.set_defaults(handler=run_audit)
    args = parser.parse_args()
    return args.handler(args)


if __name__ == "__main__":
    raise SystemExit(main())
