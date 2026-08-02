#!/usr/bin/env python3
"""Generate paired evaluation sheets for the dynamic-strategy study.

The sources are frozen Counterflow evaluation sheets. Each static/dynamic pair
keeps composition, opening allocation, routes, and baseline policies identical;
only the ordered gambit list differs. The output remains provisional and is not
the product sheet schema.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


REPO = Path(__file__).resolve().parent.parent
SOURCE = (
    REPO
    / "arena-bots/arc-relay/depth-map-v1-2026-08-02/counterflow/sheets"
)
DEFAULT_OUTPUT = (
    REPO / "arena-bots/arc-relay/dynamic-strategy-v3-2026-08-02"
)


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path}: root must be an object")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def position(
    kind: str,
    target: str = "",
    *,
    offset: tuple[int, int] = (0, 0),
    arrival: str = "hold",
    fallback_zone: str = "",
) -> dict[str, Any]:
    return {
        "arrival": arrival,
        "fallbackZone": fallback_zone,
        "kind": kind,
        "offset": list(offset),
        "target": target,
    }


def clause(
    fact: str,
    operator: str = "at-least",
    value: int = 1,
    zone: str = "",
) -> dict[str, Any]:
    return {
        "fact": fact,
        "operator": operator,
        "value": value,
        "zone": zone,
    }


def overlay(
    *,
    role: str,
    position_intent: dict[str, Any],
    formation: list[list[int]],
    engagement: str,
    signature: str,
    policies: dict[str, Any] | None = None,
    applies_while_carrying: bool = False,
) -> dict[str, Any]:
    return {
        "appliesWhileCarrying": applies_while_carrying,
        "engagementIntent": engagement,
        "formationOffsets": formation,
        "policies": policies or {},
        "position": position_intent,
        "roleOverride": role,
        "signatureIntent": signature,
    }


def gambit(
    *,
    priority: int,
    plan_id: str,
    activation: str,
    minimum: int,
    maximum: int,
    cooldown: int,
    unit_ids: list[int],
    roles: list[str],
    enter: list[dict[str, Any]],
    exit_any: list[dict[str, Any]],
    plan_overlay: dict[str, Any],
) -> dict[str, Any]:
    return {
        "activation": activation,
        "cooldownTicks": cooldown,
        "enterAll": enter,
        "exitAny": exit_any,
        "id": plan_id,
        "maximumTicks": maximum,
        "minimumTicks": minimum,
        "overlay": plan_overlay,
        "priority": priority,
        "scope": {"roles": roles, "unitIds": unit_ids},
    }


def base_sheet(source_name: str, family: str, dynamic: bool) -> dict[str, Any]:
    sheet = read_json(SOURCE / f"{source_name}.json")
    sheet["schema"] = "arc-relay-evaluation-sheet-v1"
    sheet["sheetId"] = f"dynamic-strategy-{family}-{'dynamic' if dynamic else 'static'}-v1"
    sheet["paths"] = {}
    sheet["gambits"] = []
    sheet["dynamicStrategyAudit"] = {
        "family": family,
        "pairedControl": not dynamic,
        "playerFacingProductSchema": False,
        "provisionalEvaluationOnly": True,
        "sourceSheet": source_name,
    }
    for slot in sheet["slots"]:
        slot["defaultIntent"] = {
            "engagementIntent": "normal",
            "position": position("base-assignment", arrival="base-assignment"),
            "signatureIntent": "normal",
        }
    return sheet


def rear_ambush(dynamic: bool) -> dict[str, Any]:
    sheet = base_sheet("outer-pincers", "rear-ambush", dynamic)
    sheet["zones"].update({
        "enemy-rear-staging": [23, 6, 24, 16],
        "enemy-return-corridor": [17, 3, 25, 19],
    })
    sheet["paths"].update({
        "rear-infiltration-north": [
            [5, 8], [8, 6], [13, 6], [17, 6], [21, 7], [24, 8]
        ],
        "rear-infiltration-south": [
            [5, 14], [8, 16], [13, 16], [17, 16], [21, 15], [24, 14]
        ],
    })
    for unit_id, path_id in ((4, "rear-infiltration-north"),
                             (5, "rear-infiltration-south")):
        slot = next(value for value in sheet["slots"] if value["unitId"] == unit_id)
        slot["defaultIntent"] = {
            "engagementIntent": "hold-fire",
            "position": position(
                "path",
                path_id,
                arrival="zone",
                fallback_zone="enemy-rear-staging",
            ),
            "signatureIntent": "conserve",
        }
    if dynamic:
        sheet["gambits"] = [
            gambit(
                priority=10,
                plan_id="rear-collapse",
                activation="while-true",
                minimum=16,
                maximum=48,
                cooldown=54,
                unit_ids=[4, 5, 7],
                roles=[],
                enter=[clause(
                    "enemy-carriers-in-zone",
                    zone="enemy-return-corridor",
                ), clause(
                    "own-bodies-in-zone",
                    value=2,
                    zone="enemy-rear-staging",
                ), clause(
                    "own-min-zone-tenure",
                    value=6,
                    zone="enemy-rear-staging",
                )],
                exit_any=[clause("enemy-carried-cores", "equals", 0)],
                plan_overlay=overlay(
                    role="intercept",
                    position_intent=position(
                        "anchor-offset",
                        "nearest-enemy-carrier",
                        offset=(2, 0),
                        fallback_zone="enemy-rear-staging",
                    ),
                    formation=[[0, -1], [0, 1], [-2, 0]],
                    engagement="carrier-only",
                    signature="aggressive",
                    policies={
                        "interception": {
                            "focusEnemyCarrier": True,
                            "looseCoreFallback": False,
                        }
                    },
                ),
            ),
            gambit(
                priority=20,
                plan_id="rear-recovery",
                activation="while-true",
                minimum=8,
                maximum=24,
                cooldown=36,
                unit_ids=[4, 5],
                roles=[],
                enter=[clause(
                    "loose-cores-in-zone",
                    "at-least",
                    1,
                    "enemy-return-corridor",
                )],
                exit_any=[clause("visible-loose-cores", "equals", 0)],
                plan_overlay=overlay(
                    role="carrier",
                    position_intent=position(
                        "anchor-offset",
                        "nearest-loose-core",
                        fallback_zone="enemy-rear-staging",
                    ),
                    formation=[[0, 0], [-1, 0]],
                    engagement="aggressive",
                    signature="aggressive",
                ),
            ),
        ]
    return sheet


def well_rotation(dynamic: bool) -> dict[str, Any]:
    sheet = base_sheet("three-well-race", "well-rotation", dynamic)
    sheet["zones"]["rotation-spine"] = [9, 3, 14, 19]
    if dynamic:
        sheet["gambits"] = [gambit(
            priority=10,
            plan_id="prebirth-rotation",
            activation="while-true",
            minimum=12,
            maximum=24,
            cooldown=60,
            unit_ids=[4, 5],
            roles=[],
            enter=[clause("ticks-until-next-well", "at-most", 12)],
            exit_any=[clause("visible-loose-cores", "at-least", 1)],
            plan_overlay=overlay(
                role="carrier",
                position_intent=position(
                    "anchor-offset",
                    "next-well",
                    offset=(-2, 0),
                    fallback_zone="rotation-spine",
                ),
                formation=[[0, -1], [0, 1]],
                engagement="normal",
                signature="aggressive",
                policies={"carrier": {"preferAssignedTheater": False}},
            ),
        )]
    return sheet


def feint_pincer(dynamic: bool) -> dict[str, Any]:
    sheet = base_sheet("feint-switch", "feint-pincer", dynamic)
    sheet["zones"].update({
        "centre-bait": [10, 7, 20, 15],
        "far-flank": [17, 16, 24, 20],
    })
    sheet["paths"]["south-pincer-release"] = [
        [7, 17], [12, 20], [18, 20], [22, 17], [20, 14]
    ]
    if dynamic:
        sheet["gambits"] = [gambit(
            priority=10,
            plan_id="south-pincer-release",
            activation="rising-edge",
            minimum=20,
            maximum=60,
            cooldown=64,
            unit_ids=[6, 7],
            roles=[],
            enter=[clause("enemy-bodies-in-zone", "at-least", 3, "centre-bait")],
            exit_any=[clause("enemy-bodies-in-zone", "at-most", 1, "centre-bait")],
            plan_overlay=overlay(
                role="intercept",
                position_intent=position(
                    "path",
                    "south-pincer-release",
                    arrival="base-assignment",
                    fallback_zone="far-flank",
                ),
                formation=[[0, 0], [0, 1]],
                engagement="aggressive",
                signature="aggressive",
                policies={
                    "interception": {
                        "focusEnemyCarrier": True,
                        "looseCoreFallback": True,
                    }
                },
            ),
        )]
    return sheet


def escort_counterpunch(dynamic: bool) -> dict[str, Any]:
    sheet = base_sheet("home-counterpunch", "escort-counterpunch", dynamic)
    sheet["zones"].update({
        "own-deep-half": [1, 3, 13, 19],
        "home-backstop": [4, 7, 8, 15],
    })
    if dynamic:
        sheet["gambits"] = [
            gambit(
                priority=10,
                plan_id="deep-cutoff",
                activation="while-true",
                minimum=16,
                maximum=44,
                    cooldown=60,
                unit_ids=[0, 1, 4, 5],
                roles=[],
                enter=[clause(
                    "enemy-carriers-in-zone", "at-least", 1, "own-deep-half"
                )],
                exit_any=[clause("enemy-carried-cores", "equals", 0)],
                plan_overlay=overlay(
                    role="intercept",
                    position_intent=position(
                        "anchor-offset",
                        "nearest-enemy-carrier",
                        offset=(2, 0),
                        fallback_zone="home-backstop",
                    ),
                    formation=[[0, -2], [0, 2], [1, -1], [1, 1]],
                    engagement="carrier-only",
                    signature="aggressive",
                    policies={
                        "interception": {
                            "focusEnemyCarrier": True,
                            "looseCoreFallback": False,
                        }
                    },
                ),
            ),
            gambit(
                priority=20,
                plan_id="escort-column",
                activation="while-true",
                minimum=18,
                maximum=50,
                cooldown=64,
                unit_ids=[0, 1],
                roles=[],
                enter=[clause("own-carried-cores", "at-least", 1)],
                exit_any=[clause("own-carried-cores", "equals", 0)],
                plan_overlay=overlay(
                    role="screen",
                    position_intent=position(
                        "anchor-offset",
                        "nearest-own-carrier",
                        offset=(-1, 0),
                        fallback_zone="home-backstop",
                    ),
                    formation=[[0, -1], [0, 1]],
                    engagement="normal",
                    signature="defensive",
                    policies={
                        "escort": {
                            "focusEnemyCarrier": True,
                            "followDistance": 2,
                        }
                    },
                ),
            ),
        ]
    return sheet


FAMILIES = {
    "rear-ambush": rear_ambush,
    "well-rotation": well_rotation,
    "feint-pincer": feint_pincer,
    "escort-counterpunch": escort_counterpunch,
}


def generate(output: Path) -> None:
    sheets = output / "sheets"
    entrants: list[dict[str, Any]] = []
    artifact = output.parent / "stock-mind-v1/out/bot.wasm"
    for family, build in FAMILIES.items():
        for kind, dynamic in (("static", False), ("dynamic", True)):
            entrant_id = f"{family}-{kind}"
            path = sheets / f"{entrant_id}.json"
            write_json(path, build(dynamic))
            item: dict[str, Any] = {
                "artifact": "../stock-mind-v1/out/bot.wasm",
                "entrantId": entrant_id,
                "family": family,
                "sheet": f"sheets/{entrant_id}.json",
                "strategyKind": kind,
            }
            if artifact.is_file():
                item["artifactSha256"] = sha256(artifact)
            item["sheetSha256"] = sha256(path)
            entrants.append(item)
    write_json(output / "cohort.json", {
        "cohortId": "arc-relay-dynamic-strategy-v3",
        "eligibilityBars": "../../../balance/arc-relay-felt-degeneracy-bars-v3.json",
        "entrants": entrants,
        "mapProfile": "depth-counterflow",
        "provisionalEvaluationOnly": True,
        "schema": "arc-relay-dynamic-strategy-cohort-v1",
        "study": "../../../balance/arc-relay-dynamic-strategy-v1.json",
    })


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    generate(args.output.resolve())
    print(f"generated {len(FAMILIES) * 2} paired strategy sheets in {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
