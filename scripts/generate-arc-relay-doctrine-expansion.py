#!/usr/bin/env python3
"""Generate the seven-sheet Arc Relay doctrine expansion and cohort.

The existing five balance-audit-v1 sheets stay byte-frozen. This generator
adds seven evaluation-only doctrines to the v2 archive and validates the whole
twelve-sheet pack against ``generate-arc-relay-sheet.py``. It does not define
the future player-facing sheet format.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path


REPO = Path(__file__).resolve().parent.parent
PACK = REPO / "arena-bots/arc-relay/balance-audit-v2-2026-08-01/sheets"
ARCHIVE = PACK.parent
ARTIFACT = ARCHIVE / "out/bot.wasm"
COHORT = ARCHIVE / "cohort.json"
VALIDATOR = REPO / "scripts/generate-arc-relay-sheet.py"

ENTRANTS = [
    ("balanced", "balanced.json"),
    ("control-grid", "control-grid.json"),
    ("convoy", "convoy.json"),
    ("interception", "interception.json"),
    ("split", "split.json"),
    ("relay-chain", "relay-chain.json"),
    ("fortress-counterattack", "fortress-counterattack.json"),
    ("trap-punish", "trap-punish.json"),
    ("fireline-picks", "fireline-picks.json"),
    ("displacement-control", "displacement-control.json"),
    ("sustain-attrition", "sustain-attrition.json"),
    ("feint-switch", "feint-switch.json"),
]

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

DOCTRINES = [
    {
        "file": "relay-chain.json",
        "sheetId": "depth-relay-chain-static-v1",
        "composition": [
            "relay", "relay", "switchback", "palisade",
            "kestrel", "towline", "veil", "patchbay",
        ],
        "slots": [
            ("north", "carrier", 2, "north-fast"),
            ("south", "carrier", 3, "south-fast"),
            ("north", "screen", 0, "north-safe"),
            ("south", "screen", 1, "south-safe"),
            ("centre", "carrier", 6, "centre-fast"),
            ("centre", "intercept", 4, "centre-fast"),
            ("centre", "screen", 4, "centre-safe"),
            ("centre", "reserve", 4, "centre-safe"),
        ],
        "allocation": "2 north / 4 centre / 2 south",
        "plan": "stage receivers ahead of swift carriers and move Cores by Arc Toss or emergency Exchange",
        "counter": "occupy catch tiles, pressure receivers, or Null the public transfer tell",
        "failure": "rushed throws create neutral loose Cores and fragile receivers can be isolated",
        "policy": (2, True, 8, 2, True, True, True),
    },
    {
        "file": "fortress-counterattack.json",
        "sheetId": "depth-fortress-counterattack-static-v1",
        "composition": [
            "palisade", "palisade", "mason", "relay",
            "nest", "patchbay", "longshot", "relay",
        ],
        "slots": [
            ("north", "screen", 3, "north-safe"),
            ("south", "screen", 7, "south-safe"),
            ("centre", "screen", 5, "centre-safe"),
            ("north", "carrier", 0, "north-fast"),
            ("centre", "intercept", 5, "centre-fast"),
            ("centre", "carrier", 2, "centre-safe"),
            ("centre", "intercept", 5, "centre-safe"),
            ("south", "carrier", 1, "south-fast"),
        ],
        "allocation": "2 north / 4 centre / 2 south",
        "plan": "bend homeward routes with cover and sentries, then counterattack behind a durable screen",
        "counter": "spread to the weak theater, use Mortar over blocks, or Hush the structure stack",
        "failure": "slow bodies can concede outer births and overbuilding one route leaves another open",
        "policy": (3, True, 14, 1, True, True, True),
    },
    {
        "file": "trap-punish.json",
        "sheetId": "depth-trap-punish-static-v1",
        "composition": [
            "minesmith", "minesmith", "veil", "mortar",
            "mortar", "sunder", "kestrel", "relay",
        ],
        "slots": [
            ("north", "screen", 6, "north-safe"),
            ("south", "screen", 7, "south-safe"),
            ("centre", "screen", 7, "centre-safe"),
            ("north", "intercept", 6, "north-fast"),
            ("south", "intercept", 7, "south-fast"),
            ("centre", "intercept", 7, "centre-fast"),
            ("north", "carrier", 0, "north-fast"),
            ("south", "carrier", 1, "south-fast"),
        ],
        "allocation": "3 north / 2 centre / 3 south",
        "plan": "seed predictable exits, obscure the setup, then punish committed carriers with delayed area fire",
        "counter": "reveal with Lantern, clear constructs, bait cooldowns, or rotate through centre",
        "failure": "misread routes strand traps and low-hull fire support folds under direct pressure",
        "policy": (1, True, 10, 2, True, True, True),
    },
    {
        "file": "fireline-picks.json",
        "sheetId": "depth-fireline-picks-static-v1",
        "composition": [
            "longshot", "longshot", "lantern", "lantern",
            "sunder", "mortar", "relay", "kestrel",
        ],
        "slots": [
            ("north", "intercept", 6, "north-safe"),
            ("south", "intercept", 7, "south-safe"),
            ("north", "carrier", 2, "north-safe"),
            ("south", "screen", 7, "south-safe"),
            ("north", "screen", 2, "north-safe"),
            ("centre", "screen", 6, "centre-safe"),
            ("centre", "carrier", 4, "centre-fast"),
            ("south", "carrier", 1, "south-fast"),
        ],
        "allocation": "3 north / 2 centre / 3 south",
        "plan": "hold public firing corridors, reveal obscured routes, and focus exposed carriers",
        "counter": "smoke or block rail lines, dash onto the backline, or attack the uncovered theater",
        "failure": "fixed firing lanes are publicly telegraphed and the backline is fragile when flanked",
        "policy": (1, True, 11, 2, True, True, True),
    },
    {
        "file": "displacement-control.json",
        "sheetId": "depth-displacement-control-static-v1",
        "composition": [
            "towline", "towline", "repulsor", "repulsor",
            "switchback", "sunder", "relay", "kestrel",
        ],
        "slots": [
            ("north", "intercept", 6, "north-fast"),
            ("south", "intercept", 7, "south-fast"),
            ("centre", "intercept", 6, "centre-fast"),
            ("centre", "screen", 6, "centre-safe"),
            ("north", "screen", 7, "north-safe"),
            ("south", "intercept", 7, "south-safe"),
            ("centre", "carrier", 3, "centre-fast"),
            ("north", "carrier", 4, "north-fast"),
        ],
        "allocation": "3 north / 3 centre / 2 south",
        "plan": "attack carrier geometry with pulls, bursts, swaps, and coordinated focus rather than raw damage",
        "counter": "spread escorts, block straight hooks, preserve distance, or punish the committed Repulsors",
        "failure": "displacement bodies must enter danger and can pull targets toward safety when angles are wrong",
        "policy": (2, False, 8, 2, True, True, True),
    },
    {
        "file": "sustain-attrition.json",
        "sheetId": "depth-sustain-attrition-static-v1",
        "composition": [
            "patchbay", "patchbay", "palisade", "palisade",
            "hush", "nest", "relay", "towline",
        ],
        "slots": [
            ("north", "carrier", 2, "north-safe"),
            ("south", "screen", 7, "south-safe"),
            ("north", "screen", 6, "north-safe"),
            ("south", "screen", 7, "south-safe"),
            ("centre", "intercept", 6, "centre-fast"),
            ("centre", "reserve", 6, "centre-safe"),
            ("centre", "carrier", 4, "centre-safe"),
            ("south", "carrier", 3, "south-safe"),
        ],
        "allocation": "3 north / 3 centre / 2 south",
        "plan": "keep three theater carriers alive through layered repair, projectile cover, and local counter-tech",
        "counter": "break repair sight, split theaters, focus Patchbay, or force simultaneous threats",
        "failure": "tight protection can surrender map width and burst focus can outrun channelled repair",
        "policy": (3, True, 14, 1, True, True, True),
    },
    {
        "file": "feint-switch.json",
        "sheetId": "depth-feint-switch-static-v1",
        "composition": [
            "kestrel", "kestrel", "relay", "relay",
            "switchback", "veil", "lantern", "hush",
        ],
        "slots": [
            ("north", "carrier", 4, "north-fast"),
            ("north", "intercept", 0, "north-fast"),
            ("north", "carrier", 5, "north-fast"),
            ("south", "carrier", 6, "south-fast"),
            ("centre", "screen", 0, "centre-fast"),
            ("centre", "screen", 2, "centre-safe"),
            ("south", "reserve", 3, "south-fast"),
            ("south", "intercept", 3, "south-safe"),
        ],
        "allocation": "opening 5 north / 1 centre / 2 south, then scheduled north-south inversion",
        "plan": "show an outer-theater overload, then rotate swift carriers and screens to the opposite birth",
        "counter": "hold central information, refuse the first feint, or punish the public rotation window",
        "failure": "a mistimed switch abandons live Cores and can turn rotation into empty travel",
        "policy": (1, False, 7, 2, True, True, True),
    },
]


def load_validator():
    spec = importlib.util.spec_from_file_location("arc_sheet", VALIDATOR)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot import {VALIDATOR}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def slot(unit_id: int, values: tuple[str, str, int, str]) -> dict:
    theater, role, partner, path_id = values
    path = PATHS[path_id]
    return {
        "unitId": unit_id,
        "theater": theater,
        "role": role,
        "partnerUnitId": partner,
        "outboundPath": path["outbound"],
        "returnPath": path["return"],
    }


def sheet(base: dict, doctrine: dict) -> dict:
    handoff, assigned, failure_ticks, follow, focus, carrier_focus, fallback = (
        doctrine["policy"]
    )
    value = json.loads(json.dumps(base))
    value["sheetId"] = doctrine["sheetId"]
    value["composition"] = doctrine["composition"]
    value["slots"] = [
        slot(index, definition)
        for index, definition in enumerate(doctrine["slots"])
    ]
    value["gambits"] = []
    value["policies"] = {
        "carrier": {
            "handoffHealthAtOrBelow": handoff,
            "preferAssignedTheater": assigned,
            "routeFailureTicks": failure_ticks,
        },
        "escort": {
            "followDistance": follow,
            "focusEnemyCarrier": focus,
        },
        "interception": {
            "focusEnemyCarrier": carrier_focus,
            "looseCoreFallback": fallback,
        },
    }
    value["auditDimensions"] = {
        "adaptationStyle": "static doctrine with deterministic reactive verbs",
        "allocation": doctrine["allocation"],
        "policyStyle": doctrine["plan"],
        "visibleCounter": doctrine["counter"],
        "failureMode": doctrine["failure"],
    }
    value["auditStatus"] = {
        "playerFacingProductSchema": False,
        "provisionalEvaluationOnly": True,
        "purpose": "twelve-doctrine depth and counterplay screen",
    }
    return value


def encoded(value: dict) -> bytes:
    return (json.dumps(
        value,
        ensure_ascii=False,
        indent=2,
        sort_keys=True,
    ) + "\n").encode("utf-8")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def cohort() -> dict:
    if not ARTIFACT.is_file():
        raise FileNotFoundError(
            f"build the shared WASM artifact before generating {COHORT}"
        )
    artifact_hash = sha256(ARTIFACT)
    return {
        "schema": "arc-relay-native-eligible-population-v1",
        "cohortId": "arc-relay-doctrine-expansion-v1",
        "eligibilityBars": (
            "../../../balance/arc-relay-felt-degeneracy-bars-v2.json"
        ),
        "excludedBeforeOutcomeReads": [],
        "provenance": {
            "claimScope": (
                "same-engine doctrine depth and gross balance screen; not "
                "independent-lineage or human-fun authority"
            ),
            "sharedExecutionEngine": "out/bot.wasm",
            "sharedExecutionEngineSha256": artifact_hash,
            "authoringBoundary": (
                "evaluation-grade ARS1 sheets, not player-facing product schema"
            ),
            "rulesChange": "none",
        },
        "entrants": [
            {
                "entrantId": entrant_id,
                "artifact": "out/bot.wasm",
                "artifactSha256": artifact_hash,
                "sheet": f"sheets/{filename}",
                "sheetSha256": sha256(PACK / filename),
            }
            for entrant_id, filename in ENTRANTS
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="fail if generated sheets or cohort differ instead of writing",
    )
    args = parser.parse_args()
    validator = load_validator()
    base = json.loads((PACK / "balanced.json").read_text(encoding="utf-8"))
    expected: dict[Path, bytes] = {}
    for doctrine in DOCTRINES:
        value = sheet(base, doctrine)
        validator.validate(value)
        expected[PACK / doctrine["file"]] = encoded(value)

    changed: list[Path] = []
    for path, content in expected.items():
        if not path.is_file() or path.read_bytes() != content:
            changed.append(path)
            if not args.check:
                path.parent.mkdir(parents=True, exist_ok=True)
                temporary = path.with_suffix(path.suffix + ".tmp")
                temporary.write_bytes(content)
                temporary.replace(path)
    if args.check and changed:
        raise SystemExit("stale doctrine sheets: " + ", ".join(map(str, changed)))

    for path in sorted(PACK.glob("*.json")):
        validator.validate(json.loads(path.read_text(encoding="utf-8")))
    if len(list(PACK.glob("*.json"))) != 12:
        raise SystemExit("the doctrine pack must contain exactly twelve sheets")

    cohort_content = encoded(cohort())
    if not COHORT.is_file() or COHORT.read_bytes() != cohort_content:
        if args.check:
            raise SystemExit(f"stale doctrine cohort: {COHORT}")
        temporary = COHORT.with_suffix(COHORT.suffix + ".tmp")
        temporary.write_bytes(cohort_content)
        temporary.replace(COHORT)
    verb = "checked" if args.check else "generated"
    print(f"{verb} 7 expansion sheets and cohort; validated 12-sheet pack")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
