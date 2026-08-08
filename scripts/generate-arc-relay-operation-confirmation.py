#!/usr/bin/env python3
"""Freeze the held-out Arc Relay operation counterplay confirmation.

The discovery pass preregistered committed counters only and found them for
eight of ten operations.  Replay inspection showed that the other two were
reliably denied during claimed preparation by hostile destruction.  This
generator records that distinction before any held-out seed is executed and
selects two real-population opponents per operation for a bounded confirmation.
"""

from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
from typing import Any


REPO = Path(__file__).resolve().parent.parent
DISCOVERY_GENERATOR = Path(__file__).with_name(
    "generate-arc-relay-operation-counterplay.py"
)
SPEC = importlib.util.spec_from_file_location(
    "operation_counterplay_generator", DISCOVERY_GENERATOR
)
assert SPEC is not None and SPEC.loader is not None
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)

OUTPUT = REPO / "arena-bots/arc-relay/operation-counterplay-v1-2026-08-03"
DISCOVERY_MANIFEST = OUTPUT / "discovery-manifest.json"
TAXONOMY = OUTPUT / "counter-taxonomy-v2.json"
CONFIRMATION_MANIFEST = OUTPUT / "confirmation-manifest.json"
HELD_OUT_SEEDS = ("67867967", "982451653")

# Outcome-informed selection is allowed here because outcomes are not reused:
# these opponents had clear causal interactions in discovery, then face two
# seeds that were not part of discovery or the original ten-operation proof.
SELECTED_COUNTERS: dict[str, tuple[str, str]] = {
    "rear-hook": ("balanced", "fortress-counterattack"),
    "lantern-sweep": ("beacon-hunt", "sensor-grid"),
    "fork-shadow": ("smoke-convoy", "counter-courier"),
    "birth-rotation": ("beacon-hunt", "centre-phalanx"),
    "escort-counterpunch": ("fireline-picks", "home-counterpunch"),
    "smoke-breach": ("null-veil", "rotating-bastions"),
    "hardlight-gate": ("breach-column", "counter-courier"),
    "relay-catch": ("trap-punish", "hook-burst"),
    "decoy-switch": ("beacon-hunt", "elastic-reserve"),
    "emergency-exchange": ("interception", "mortar-wheel"),
}


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path}: expected object")
    return value


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def main() -> int:
    discovery = read_json(DISCOVERY_MANIFEST)
    if set(SELECTED_COUNTERS) != set(BASE.COUNTERS):
        raise ValueError("confirmation selection must cover all ten operations")
    for operation_id, selected in SELECTED_COUNTERS.items():
        registered = {value for value, _ in BASE.COUNTERS[operation_id]}
        if len(set(selected)) != 2 or not set(selected) <= registered:
            raise ValueError(f"invalid confirmation selection for {operation_id}")

    taxonomy = {
        "schema": "arc-relay-operation-counter-taxonomy-v2",
        "status": "frozen-after-discovery-before-held-out-confirmation",
        "discoveryCommittedOnlyResult": {
            "operationsMeetingSuccessCounterCasualty": 8,
            "operationsTotal": 10,
            "missingCommittedCounter": ["hardlight-gate", "lantern-sweep"],
        },
        "counterQualifiesWhen": {
            "committedCounter": [
                "the operation reached its commitment lock",
                "it terminated for a non-success reason",
                "a claimed life took direct hostile damage or destruction before termination",
                "all surviving claims released within the card recovery deadline",
                "released survivors returned to non-operation baseline behavior",
                "the match passed runtime and frozen felt-degeneracy eligibility",
            ],
            "preparationDenial": [
                "the operation had claimed its preparation actors but had not committed",
                "a claimed life was directly destroyed by the opponent",
                "the operation explicitly terminated as prepare-abort or prepare-participant-minimum",
                "all surviving claims released within the card recovery deadline",
                "released survivors returned to non-operation baseline behavior",
                "the match passed runtime and frozen felt-degeneracy eligibility",
            ],
        },
        "neverQualifiesByItself": [
            "entrant or doctrine name",
            "winning the match",
            "operation non-activation",
            "a condition becoming false before actors were claimed",
            "damage without a causally claimed casualty during preparation",
            "deadline expiry without causal hostile interaction",
            "a terminal operation that does not release to baseline in time",
        ],
        "rationale": (
            "Counterplay includes disrupting an observed setup before its lock; "
            "forcing premature commitment merely to satisfy a committed-only "
            "metric would make the operation less intelligent. The categories "
            "remain separate in every result."
        ),
    }
    write_json(TAXONOMY, taxonomy)

    entrants: dict[str, dict[str, str]] = {}
    cells: list[dict[str, Any]] = []
    for operation_index, (operation_id, opponents) in enumerate(
        SELECTED_COUNTERS.items()
    ):
        operation_entrant = f"op-{operation_id}"
        entrants[operation_entrant] = discovery["entrants"][operation_entrant]
        theses = dict(BASE.COUNTERS[operation_id])
        for opponent_index, opponent_id in enumerate(opponents):
            opponent_entrant = f"pop-{opponent_id}"
            entrants[opponent_entrant] = discovery["entrants"][opponent_entrant]
            for seed_index, seed in enumerate(HELD_OUT_SEEDS):
                operation_team = (
                    operation_index + opponent_index + seed_index
                ) % 2
                team0 = (
                    operation_entrant if operation_team == 0 else opponent_entrant
                )
                team1 = (
                    opponent_entrant if operation_team == 0 else operation_entrant
                )
                topology, match_contract = BASE.resolve_contract(
                    REPO / entrants[team0]["sheet"],
                    REPO / entrants[team1]["sheet"],
                )
                cells.append({
                    "cellId": (
                        f"confirm--{operation_id}--{opponent_id}--s{seed}--"
                        f"op{operation_team}"
                    ),
                    "seed": seed,
                    "team0": team0,
                    "team1": team1,
                    "operationId": operation_id,
                    "operationTeamId": operation_team,
                    "opponentId": opponent_id,
                    "counterThesis": theses[opponent_id],
                    "topologyFingerprint": topology,
                    "matchContractFingerprint": match_contract,
                })

    manifest = {
        "schema": "arc-relay-sweep-plan-v1",
        "sweepId": "arc-relay-operation-counterplay-confirmation-v1",
        "cohortId": "arc-relay-operation-counterplay-confirmation-v1",
        "purpose": (
            "held-out confirmation of success, explicit counter categories, "
            "casualty recovery, bounded release, baseline return, and bars"
        ),
        "runtime": discovery["runtime"],
        "loopProfile": discovery["loopProfile"],
        "engineVersion": discovery["engineVersion"],
        "rulesetId": discovery["rulesetId"],
        "rulesFingerprint": discovery["rulesFingerprint"],
        "mapId": discovery["mapId"],
        "mapFingerprint": discovery["mapFingerprint"],
        "eligibilityBars": discovery["eligibilityBars"],
        "eligibilityBarsSha256": discovery["eligibilityBarsSha256"],
        "counterTaxonomy": str(TAXONOMY.relative_to(REPO)),
        "counterTaxonomySha256": sha256(TAXONOMY),
        "registration": {
            "status": "frozen-before-held-out-outcomes",
            "discoveryWasOutcomeInformed": True,
            "heldOutSeeds": list(HELD_OUT_SEEDS),
            "operations": len(SELECTED_COUNTERS),
            "opponentsPerOperation": 2,
            "cells": len(cells),
            "participantAssignment": (
                "alternating by frozen operation/opponent/seed index"
            ),
            "selectionBasis": (
                "two real-population opponents with causal discovery contact; "
                "all retained claims come only from these held-out cells"
            ),
        },
        "entrants": entrants,
        "cells": cells,
    }
    write_json(CONFIRMATION_MANIFEST, manifest)
    print(
        f"wrote {len(cells)} held-out cells for "
        f"{len(SELECTED_COUNTERS)} operations"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
