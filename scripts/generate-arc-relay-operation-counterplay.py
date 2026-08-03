#!/usr/bin/env python3
"""Freeze the multi-seed Arc Relay operation counterplay discovery matrix.

This is evaluation infrastructure, not a player-facing sheet format.  The
matrix deliberately uses the ten already-proven operation sheets against the
real counterflow population.  Opponents are chosen before outcomes for their
authored counter thesis; three registered seeds alternate participant side so
the discovery read cannot quietly depend on one spawn assignment.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import subprocess
from typing import Any


REPO = Path(__file__).resolve().parent.parent
PROOF_ROOT = REPO / "arena-bots/arc-relay/intelligent-operation-proof-v1-2026-08-02"
PROOF_CATALOG = PROOF_ROOT / "catalog.json"
POPULATION_ROOT = REPO / "arena-bots/arc-relay/depth-map-v1-2026-08-02/counterflow"
POPULATION_COHORT = POPULATION_ROOT / "cohort.json"
OUTPUT = REPO / "arena-bots/arc-relay/operation-counterplay-v1-2026-08-03"
OPERATION_ARTIFACT = REPO / "arena-bots/arc-relay/stock-mind-v3/out/bot.wasm"
POPULATION_ARTIFACT = REPO / "arena-bots/arc-relay/flow-intent-v1-2026-08-02/out/bot.wasm"
BARS = REPO / "balance/arc-relay-felt-degeneracy-bars-v3.json"
CLI = REPO / "src/BotArena.Cli/bin/Debug/net10.0/botarena.dll"

ENGINE_VERSION = "1.0.5"
RULESET_ID = "arc-relay-h0-01"
RULES_FINGERPRINT = "f6d3ee9b1bb17d7bd8d0981941fd00a6a96f0e7ef834497d11924c06087174eb"
MAP_ID = "arc-relay-threefold-depth-counterflow-01"
MAP_FINGERPRINT = "5ca7d1a1826791d736465d352c1558793846fc2e3df343d730f1df4c79f47e0c"

SEEDS = ("32452843", "49979687", "86080201")

# The counter thesis is authored before outcome access.  Eight candidates per
# card keep the discovery matrix bounded while the union covers all 32 real
# population sheets.  A retained counter later needs causal replay evidence;
# its name alone never qualifies it.
COUNTERS: dict[str, list[tuple[str, str]]] = {
    "rear-hook": [
        ("counter-courier", "carrier protection and direct escort pressure"),
        ("smoke-convoy", "concealed convoy denies a clean exact-carrier hook"),
        ("fortress-counterattack", "prepared home defence punishes infiltrators"),
        ("interception", "active carrier hunters contest the rear strike group"),
        ("trap-punish", "route traps punish fixed ambush staging"),
        ("null-veil", "smoke and dampening shorten causal target access"),
        ("elastic-reserve", "reserve rotation answers a revealed rear commitment"),
        ("balanced", "general baseline control checks over-specialisation"),
    ],
    "lantern-sweep": [
        ("null-veil", "concealment and dampening oppose the probe"),
        ("beacon-hunt", "sensor contest hunts the exposed Lantern"),
        ("interception", "carrier pressure forces the route decision"),
        ("outer-pincers", "two-lane pressure attacks the chosen fork"),
        ("trap-punish", "fixed fork movement encounters prepared traps"),
        ("sensor-grid", "distributed vision contests information advantage"),
        ("smoke-and-mines", "concealment and denial complicate both branches"),
        ("control-grid", "persistent nodes constrain the return lanes"),
    ],
    "fork-shadow": [
        ("feint-switch", "route feints test locked-branch discipline"),
        ("relay-chain", "Core tosses can bypass a conventional cutoff"),
        ("counter-courier", "escort pressure protects the exact carrier"),
        ("courier-sprint", "swift route commitment tests cutoff timing"),
        ("smoke-convoy", "concealment tests last-seen pursuit"),
        ("repair-web", "sustain tests whether displacement converts"),
        ("displacement-control", "counter-displacement contests the hook pair"),
        ("rotating-bastions", "lane rotation tests a fixed north/south branch"),
    ],
    "birth-rotation": [
        ("three-well-race", "all-Well pressure contests the timed rotation"),
        ("split", "distributed theater control contests both rotators"),
        ("mine-crescent", "future objective lanes are trapped"),
        ("pod-lattice", "deployable control occupies the destination band"),
        ("mortar-wheel", "ranged denial attacks predictable timing"),
        ("centre-phalanx", "central mass tests whether rotation concedes home"),
        ("control-grid", "persistent control challenges arrival"),
        ("beacon-hunt", "scouting pressure detects early rotation"),
    ],
    "escort-counterpunch": [
        ("interception", "dedicated carrier pressure creates the decision"),
        ("home-counterpunch", "mirrored counterpressure tests route choice"),
        ("hook-burst", "forced displacement separates carrier and guard"),
        ("fireline-picks", "long-range focus punishes a held carrier"),
        ("rail-screen", "screened rails deny the direct return"),
        ("sustain-attrition", "sustained pressure tests bounded commitment"),
        ("repair-web", "opposing sustain tests conversion rather than survival"),
        ("convoy", "dense escorting tests whether the counterpunch finds leverage"),
    ],
    "smoke-breach": [
        ("sensor-grid", "distributed sensors contest smoke cover"),
        ("beacon-hunt", "active scouting searches the breach pair"),
        ("null-veil", "counter-concealment and dampening contest signatures"),
        ("mortar-wheel", "area denial punishes concentrated staging"),
        ("trap-punish", "fixed breach lane enters prepared traps"),
        ("centre-phalanx", "central mass contests the crossing"),
        ("rotating-bastions", "hard lane defence absorbs the breach"),
        ("fortress-counterattack", "fortified response punishes concentration"),
    ],
    "hardlight-gate": [
        ("breach-column", "concentrated breach pressure tests the gate"),
        ("displacement-control", "forced movement can separate gate actors"),
        ("hook-burst", "hook and burst pressure tests carrier clearance"),
        ("mortar-wheel", "lobbed denial reaches behind the gate"),
        ("outer-pincers", "split pressure attacks both gate edges"),
        ("smoke-and-mines", "concealment and traps contest the safe line"),
        ("counter-courier", "carrier hunters force the protection mission"),
        ("fireline-picks", "ranged focus tests hardlight timing"),
    ],
    "relay-catch": [
        ("interception", "carrier hunters can punish the toss setup"),
        ("counter-courier", "escort denial attacks sender and receiver"),
        ("hook-burst", "forced movement can break the catch lane"),
        ("displacement-control", "receiver displacement contests ownership"),
        ("relay-chain", "an opposing toss network contests tempo"),
        ("courier-sprint", "direct speed tests whether setup is worthwhile"),
        ("smoke-convoy", "concealment complicates interception and pursuit"),
        ("trap-punish", "predictable catch position is denied"),
    ],
    "decoy-switch": [
        ("sensor-grid", "distributed sensing should expose the decoy"),
        ("beacon-hunt", "active scouts test false-pressure credibility"),
        ("three-well-race", "broad pressure punishes conceded centre"),
        ("split", "balanced theater coverage resists the switch"),
        ("elastic-reserve", "mobile reserves answer the locked pincer"),
        ("feint-switch", "a competing feint tests branch discipline"),
        ("outer-pincers", "opposite multi-lane pressure attacks staging"),
        ("control-grid", "persistent control makes the real lane costly"),
    ],
    "emergency-exchange": [
        ("fireline-picks", "focus fire creates and then pursues the wounded carrier"),
        ("rail-screen", "long-range denial contests the exchange route"),
        ("mortar-wheel", "area pressure attacks the rendezvous"),
        ("interception", "carrier hunters force the emergency"),
        ("home-counterpunch", "return-lane pressure tests the rescue"),
        ("sustain-attrition", "continued damage tests whether exchange stabilises"),
        ("null-veil", "concealment tests causal rendezvous information"),
        ("repair-web", "sustain alternative checks exchange opportunity cost"),
    ],
}


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path}: expected object")
    return value


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def repo_path(path: Path) -> str:
    return str(path.resolve().relative_to(REPO))


def resolve_contract(sheet0: Path, sheet1: Path) -> tuple[str, str]:
    """Resolve composition-sensitive fingerprints without playing a match."""
    if not CLI.is_file():
        raise FileNotFoundError(
            f"missing {CLI}; build BotArena.sln in Debug before generating"
        )
    completed = subprocess.run(
        [
            "dotnet",
            str(CLI),
            "experiment",
            "arc-relay",
            "--sheet0",
            str(sheet0),
            "--sheet1",
            str(sheet1),
            "--loop-profile",
            "depth-counterflow",
            "--print-contract",
        ],
        cwd=REPO,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError(
            f"contract resolution failed for {sheet0.name} vs {sheet1.name}: "
            f"{completed.stderr.strip()}"
        )
    contract = json.loads(completed.stdout)
    if contract["rules"]["rulesetId"] != RULESET_ID:
        raise ValueError("contract resolver returned the wrong ruleset")
    if contract["rules"]["rulesFingerprint"] != RULES_FINGERPRINT:
        raise ValueError("contract resolver returned a moved rules fingerprint")
    if contract["map"]["mapId"] != MAP_ID:
        raise ValueError("contract resolver returned the wrong map")
    if contract["map"]["mapFingerprint"] != MAP_FINGERPRINT:
        raise ValueError("contract resolver returned a moved map fingerprint")
    return (
        contract["topology"]["topologyFingerprint"],
        contract["matchContractFingerprint"],
    )


def main() -> int:
    proof = read_json(PROOF_CATALOG)
    population = read_json(POPULATION_COHORT)
    population_by_id = {
        entrant["entrantId"]: entrant for entrant in population["entrants"]
    }
    proof_by_id = {card["id"]: card for card in proof["cards"]}
    if set(COUNTERS) != set(proof_by_id):
        raise ValueError("counter registration must cover the exact proof catalog")
    registered_population = {
        opponent for rows in COUNTERS.values() for opponent, _ in rows
    }
    if registered_population != set(population_by_id):
        missing = sorted(set(population_by_id) - registered_population)
        extra = sorted(registered_population - set(population_by_id))
        raise ValueError(f"counter registration population mismatch: missing={missing}, extra={extra}")

    entrants: dict[str, dict[str, str]] = {}
    for operation_id, card in proof_by_id.items():
        sheet = PROOF_ROOT / card["sheet"]
        entrants[f"op-{operation_id}"] = {
            "artifact": repo_path(OPERATION_ARTIFACT),
            "artifactSha256": sha256(OPERATION_ARTIFACT),
            "sheet": repo_path(sheet),
            "sheetSha256": sha256(sheet),
        }
    for opponent_id, source in population_by_id.items():
        sheet = POPULATION_ROOT / source["sheet"]
        entrants[f"pop-{opponent_id}"] = {
            "artifact": repo_path(POPULATION_ARTIFACT),
            "artifactSha256": sha256(POPULATION_ARTIFACT),
            "sheet": repo_path(sheet),
            "sheetSha256": sha256(sheet),
        }

    contract_cache: dict[tuple[str, str], tuple[str, str]] = {}
    cells: list[dict[str, Any]] = []
    for operation_index, (operation_id, candidates) in enumerate(COUNTERS.items()):
        for opponent_index, (opponent_id, thesis) in enumerate(candidates):
            for seed_index, seed in enumerate(SEEDS):
                operation_team = (operation_index + opponent_index + seed_index) % 2
                operation = f"op-{operation_id}"
                opponent = f"pop-{opponent_id}"
                team0 = operation if operation_team == 0 else opponent
                team1 = opponent if operation_team == 0 else operation
                cache_key = (team0, team1)
                if cache_key not in contract_cache:
                    contract_cache[cache_key] = resolve_contract(
                        REPO / entrants[team0]["sheet"],
                        REPO / entrants[team1]["sheet"],
                    )
                topology_fingerprint, match_contract_fingerprint = (
                    contract_cache[cache_key]
                )
                cells.append({
                    "cellId": (
                        f"{operation_id}--{opponent_id}--s{seed}--"
                        f"op{operation_team}"
                    ),
                    "seed": seed,
                    "team0": team0,
                    "team1": team1,
                    "operationId": operation_id,
                    "operationTeamId": operation_team,
                    "opponentId": opponent_id,
                    "counterThesis": thesis,
                    "topologyFingerprint": topology_fingerprint,
                    "matchContractFingerprint": match_contract_fingerprint,
                })

    manifest = {
        "schema": "arc-relay-sweep-plan-v1",
        "sweepId": "arc-relay-operation-counterplay-discovery-v1",
        "cohortId": "arc-relay-operation-counterplay-v1",
        "purpose": (
            "preregistered discovery only: find causal success, counter, and "
            "casualty-recovery candidates before freezing retained evidence"
        ),
        "runtime": "wasm",
        "loopProfile": "depth-counterflow",
        "engineVersion": ENGINE_VERSION,
        "rulesetId": RULESET_ID,
        "rulesFingerprint": RULES_FINGERPRINT,
        "mapId": MAP_ID,
        "mapFingerprint": MAP_FINGERPRINT,
        "eligibilityBars": repo_path(BARS),
        "eligibilityBarsSha256": sha256(BARS),
        "registration": {
            "status": "frozen-before-discovery-outcomes",
            "seeds": list(SEEDS),
            "operationCount": len(COUNTERS),
            "opponentsPerOperation": 8,
            "realPopulationCoverage": len(registered_population),
            "cells": len(cells),
            "participantAssignment": "alternating by frozen operation/opponent/seed index",
            "contractResolution": (
                "composition-sensitive fingerprints resolved without match "
                "execution via experiment arc-relay --print-contract"
            ),
            "counterQualification": (
                "a name or loss is insufficient; retained counter evidence must "
                "show a complete committed abort/adaptation with causal hostile "
                "interaction, bounded recovery, and baseline release"
            ),
        },
        "entrants": entrants,
        "cells": cells,
    }
    catalog = {
        "schema": "arc-relay-operation-counterplay-catalog-v1",
        "proofCatalog": repo_path(PROOF_CATALOG),
        "populationCohort": repo_path(POPULATION_COHORT),
        "discoveryManifest": "discovery-manifest.json",
        "seeds": list(SEEDS),
        "operations": [
            {
                **proof_by_id[operation_id],
                "counterCandidates": [
                    {"entrantId": entrant_id, "thesis": thesis}
                    for entrant_id, thesis in candidates
                ],
            }
            for operation_id, candidates in COUNTERS.items()
        ],
    }
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, value in (("discovery-manifest.json", manifest), ("catalog.json", catalog)):
        (OUTPUT / name).write_text(
            json.dumps(value, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
    print(f"wrote {len(cells)} cells covering {len(registered_population)} population sheets")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
