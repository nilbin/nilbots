#!/usr/bin/env python3
"""Generate ten evaluation-grade Arc Relay operation proof sheets.

The player-facing sheet schema remains deliberately out of scope. These files
exercise the bounded v2 operation interpreter over one complete baseline and
keep the authored source compact enough to review.
"""

from __future__ import annotations

from copy import deepcopy
import json
from pathlib import Path


REPO = Path(__file__).resolve().parent.parent
SOURCE = (
    REPO
    / "arena-bots/arc-relay/intelligent-gambit-v1-2026-08-02/sheets"
    / "baseline-only.json"
)
OUTPUT = (
    REPO
    / "arena-bots/arc-relay/intelligent-operation-proof-v1-2026-08-02"
)


def condition(
    fact: str,
    operator: str = "at-least",
    value: int = 1,
    *,
    zone: str = "",
    subject: str = "",
    freshness_ticks: int = 0,
    class_ids: list[str] | None = None,
) -> dict:
    result: dict[str, object] = {
        "fact": fact,
        "operator": operator,
        "value": value,
        "zone": zone,
    }
    if subject:
        result["subject"] = subject
    if freshness_ticks:
        result["freshnessTicks"] = freshness_ticks
    if class_ids:
        result["classIds"] = class_ids
    return result


def position(
    kind: str,
    target: str,
    *,
    arrival: str = "hold",
    fallback: str = "",
) -> dict:
    return {
        "kind": kind,
        "target": target,
        "offset": [0, 0],
        "arrival": arrival,
        "fallbackZone": fallback,
    }


def task(
    task_id: str,
    candidates: list[int],
    target: dict,
    *,
    minimum: int = 1,
    resilience: str = "essential",
    permits_carrying: bool = False,
    requires_carrying: bool = False,
    role: str,
    engagement: str = "opportunistic",
    signature: str = "normal",
) -> dict:
    return {
        "id": task_id,
        "resilience": resilience,
        "minimum": minimum,
        "candidateUnitIds": candidates,
        "candidateRoles": [],
        "candidateClassIds": [],
        "permitsCarrying": permits_carrying,
        "requiresCarrying": requires_carrying,
        "position": target,
        "roleOverride": role,
        "engagementIntent": engagement,
        "signatureIntent": signature,
    }


def branch(
    branch_id: str,
    commit_all: list[dict],
    tasks: list[dict],
    success_any: list[dict],
    abort_any: list[dict],
    *,
    deadline_ticks: int,
) -> dict:
    return {
        "id": branch_id,
        "commitWhen": {"all": commit_all, "any": []},
        "tasks": tasks,
        "successAny": success_any,
        "abortAny": abort_any,
        "deadlineTicks": deadline_ticks,
    }


def recovery(candidates: list[int], *, zone: str = "home-gate") -> dict:
    extract = task(
        "extract",
        candidates,
        position("zone", zone),
        minimum=0,
        resilience="optional",
        permits_carrying=True,
        role="extract",
        engagement="break-contact",
        signature="conserve",
    )
    return {
        "deadlineTicks": 12,
        "completeAll": [
            condition(
                "task-participants-in-zone",
                zone=zone,
                subject="extract",
            )
        ],
        "onSuccess": [extract],
        "onAbort": [deepcopy(extract)],
    }


def operation(
    operation_id: str,
    prepare_all: list[dict],
    prepare_tasks: list[dict],
    branches: list[dict],
    *,
    prepare_deadline: int,
    prepare_abort: list[dict] | None = None,
) -> dict:
    participant_ids = sorted(
        {
            unit_id
            for item in prepare_tasks
            for unit_id in item["candidateUnitIds"]
        }
    )
    return {
        "priority": 10,
        "id": operation_id,
        "prepareDeadlineTicks": prepare_deadline,
        "cooldownTicks": 36,
        "prepare": {
            "when": {"all": prepare_all, "any": []},
            "abortAny": prepare_abort or [],
            "tasks": prepare_tasks,
        },
        "branches": branches,
        "recovery": recovery(participant_ids),
    }


def carrier_return_tasks(path: str, screen_ids: list[int]) -> list[dict]:
    return [
        task(
            "carrier",
            list(range(8)),
            position("path", path, arrival="zone", fallback="home-side"),
            permits_carrying=True,
            requires_carrying=True,
            role="return",
            signature="defensive",
        ),
        task(
            "screen",
            screen_ids,
            position(
                "anchor-offset",
                "nearest-own-carrier",
                fallback="home-gate",
            ),
            role="screen",
            engagement="defend-in-place",
            signature="defensive",
        ),
    ]


def cards() -> list[dict]:
    target_success = [condition("target-core-loose-or-ours", "equals")]
    lost_core = [condition("own-carried-cores", "equals", 0)]
    carrier_home = [condition("own-carriers-in-zone", zone="home-side")]

    rear_hook = operation(
        "rear-hook",
        [
            condition("ticks-until-next-well", "at-most", 20),
            condition("visible-enemies-in-zone", zone="forward-objectives"),
        ],
        [
            task(
                "north-hook",
                [4],
                position(
                    "path",
                    "hook-stage-north",
                    arrival="zone",
                    fallback="rear-pocket",
                ),
                role="ambush",
                engagement="conceal",
                signature="conserve",
            ),
            task(
                "south-hook",
                [5],
                position(
                    "path",
                    "hook-stage-south",
                    arrival="zone",
                    fallback="rear-pocket",
                ),
                role="ambush",
                engagement="conceal",
                signature="conserve",
            ),
        ],
        [
            branch(
                "carrier-strike",
                [
                    condition(
                        "task-participants-in-zone", value=2, zone="rear-pocket"
                    ),
                    condition(
                        "visible-enemy-carriers-in-zone",
                        zone="enemy-return-corridor",
                    ),
                ],
                [
                    task(
                        "hook-team",
                        [4, 5],
                        position(
                            "anchor-offset",
                            "nearest-enemy-carrier",
                            fallback="enemy-return-corridor",
                        ),
                        minimum=0,
                        resilience="optional",
                        role="strike",
                        engagement="carrier-focus",
                        signature="aggressive",
                    ),
                ],
                target_success,
                [condition("always", "equals", 0)],
                deadline_ticks=48,
            )
        ],
        prepare_deadline=100,
    )

    lantern_prepare = [
        task(
            "carrier",
            list(range(8)),
            position("zone", "safe-pre-fork"),
            permits_carrying=True,
            requires_carrying=True,
            role="probe-hold",
            engagement="defend-in-place",
            signature="defensive",
        ),
        task(
            "lantern",
            [2],
            position(
                "path",
                "lantern-probe",
                arrival="zone",
                fallback="primary-return",
            ),
            role="sweep",
            engagement="defend-in-place",
            signature="aggressive",
        ),
        task(
            "screen",
            [3, 7],
            position(
                "anchor-offset", "nearest-own-carrier", fallback="safe-pre-fork"
            ),
            resilience="replaceable",
            role="screen",
            engagement="defend-in-place",
            signature="defensive",
        ),
    ]
    lantern_sweep = operation(
        "lantern-sweep",
        [condition("own-carriers-in-zone", zone="forward-field")],
        lantern_prepare,
        [
            branch(
                "alternate-return",
                [
                    condition(
                        "task-participants-in-zone",
                        zone="safe-pre-fork",
                        subject="carrier",
                    ),
                    condition("visible-enemies-in-zone", zone="home-risk"),
                ],
                carrier_return_tasks("return-alternate", [3, 7]),
                carrier_home,
                lost_core,
                deadline_ticks=22,
            ),
            branch(
                "primary-return",
                [
                    condition(
                        "task-participants-in-zone",
                        zone="safe-pre-fork",
                        subject="carrier",
                    )
                ],
                carrier_return_tasks("return-primary", [3, 7]),
                carrier_home,
                lost_core,
                deadline_ticks=20,
            ),
        ],
        prepare_deadline=24,
        prepare_abort=lost_core,
    )

    fork_tasks = [
        task(
            "north-shadow",
            [4],
            position("path", "shadow-north"),
            role="shadow",
            engagement="conceal",
            signature="conserve",
        ),
        task(
            "south-shadow",
            [5],
            position("path", "shadow-south"),
            role="shadow",
            engagement="conceal",
            signature="conserve",
        ),
    ]

    def shadow_branch(branch_id: str, zone: str) -> dict:
        return branch(
            branch_id,
            [condition("visible-enemy-carriers-in-zone", zone=zone)],
            [
                task(
                    "cutoff-team",
                    [4, 5],
                    position(
                        "anchor-offset",
                        "nearest-enemy-carrier",
                        fallback=zone,
                    ),
                    minimum=0,
                    resilience="optional",
                    role="cutoff",
                    engagement="carrier-focus",
                    signature="aggressive",
                ),
            ],
            target_success
            + [condition("target-carrier-outside-zone", zone=zone)],
            [condition("always", "equals", 0)],
            deadline_ticks=48,
        )

    fork_shadow = operation(
        "fork-shadow",
        [
            condition(
                "visible-enemy-carriers-in-zone", zone="enemy-return-corridor"
            )
        ],
        fork_tasks,
        [
            shadow_branch("north-cutoff", "north-return"),
            shadow_branch("south-cutoff", "south-return"),
        ],
        prepare_deadline=48,
    )

    rotation_pool = task(
        "rotation-pool",
        [2, 6, 7],
        position("path", "rotation-stage", arrival="zone", fallback="home-gate"),
        minimum=2,
        resilience="replaceable",
        role="rotate",
        signature="conserve",
    )
    birth_rotation = operation(
        "birth-rotation",
        [condition("ticks-until-next-well", "at-most", 20)],
        [rotation_pool],
        [
            branch(
                "release-to-next-well",
                [
                    condition(
                        "task-participants-in-zone",
                        value=2,
                        zone="home-gate",
                        subject="rotation-pool",
                    )
                ],
                [
                    task(
                        "rotation",
                        [2, 6, 7],
                        position("anchor-offset", "next-well", fallback="centre"),
                        minimum=2,
                        role="rotate",
                        signature="aggressive",
                    )
                ],
                [
                    condition(
                        "task-participants-in-zone",
                        value=2,
                        zone="forward-objectives",
                        subject="rotation",
                    )
                ],
                [condition("visible-enemies-in-zone", value=3, zone="home-side")],
                deadline_ticks=30,
            )
        ],
        prepare_deadline=24,
    )

    escort_prepare = [
        task(
            "carrier",
            list(range(8)),
            position("zone", "safe-pre-fork"),
            permits_carrying=True,
            requires_carrying=True,
            role="counter-carrier",
            engagement="defend-in-place",
            signature="defensive",
        ),
        task(
            "guard",
            [3, 4],
            position(
                "anchor-offset", "nearest-own-carrier", fallback="safe-pre-fork"
            ),
            minimum=1,
            resilience="replaceable",
            role="counter-guard",
            engagement="defend-in-place",
            signature="aggressive",
        ),
    ]
    escort_counterpunch = operation(
        "escort-counterpunch",
        [
            condition("own-carriers-in-zone", zone="risk-fork"),
            condition("visible-enemies-in-zone", zone="risk-fork"),
        ],
        escort_prepare,
        [
            branch(
                "counter-route",
                [condition("visible-enemies-in-zone", value=2, zone="risk-fork")],
                carrier_return_tasks("return-alternate", [3, 4]),
                carrier_home,
                lost_core,
                deadline_ticks=24,
            ),
            branch(
                "direct-return",
                [
                    condition(
                        "task-participants-in-zone",
                        zone="safe-pre-fork",
                        subject="carrier",
                    )
                ],
                carrier_return_tasks("return-primary", [3, 4]),
                carrier_home,
                lost_core,
                deadline_ticks=22,
            ),
        ],
        prepare_deadline=24,
        prepare_abort=lost_core,
    )

    smoke_breach = operation(
        "smoke-breach",
        [condition("visible-enemies-in-zone", zone="centre")],
        [
            task(
                "veil",
                [6],
                position("path", "smoke-stage", arrival="zone", fallback="centre-stage"),
                role="smoke-lead",
                signature="aggressive",
            ),
            task(
                "breacher",
                [4, 7],
                position("path", "smoke-stage", arrival="zone", fallback="centre-stage"),
                resilience="replaceable",
                role="breacher",
                signature="aggressive",
            ),
        ],
        [
            branch(
                "cross-centre",
                [
                    condition(
                        "task-participants-in-zone", value=2, zone="centre-stage"
                    )
                ],
                [
                    task(
                        "breach",
                        [4, 6, 7],
                        position(
                            "path",
                            "smoke-breach-line",
                            arrival="zone",
                            fallback="centre-forward",
                        ),
                        minimum=2,
                        role="breach",
                        signature="aggressive",
                    )
                ],
                [
                    condition(
                        "task-participants-in-zone",
                        value=2,
                        zone="centre-forward",
                        subject="breach",
                    )
                ],
                [condition("visible-enemies-in-zone", value=3, zone="home-side")],
                deadline_ticks=34,
            )
        ],
        prepare_deadline=28,
    )

    hardlight_gate = operation(
        "hardlight-gate",
        [
            condition("own-carriers-in-zone", zone="risk-fork"),
            condition("visible-enemies-in-zone", zone="home-risk"),
        ],
        [
            task(
                "gate",
                [2, 3],
                position("path", "hardlight-gate-line", arrival="zone", fallback="home-gate"),
                minimum=2,
                role="gate",
                engagement="defend-in-place",
                signature="aggressive",
            )
        ],
        [
            branch(
                "hold-gate",
                [
                    condition(
                        "task-participants-in-zone",
                        value=2,
                        zone="home-gate",
                        subject="gate",
                    )
                ],
                [
                    task(
                        "gate",
                        [2, 3],
                        position("zone", "home-gate"),
                        minimum=2,
                        role="gate",
                        engagement="defend-in-place",
                        signature="aggressive",
                    )
                ],
                carrier_home,
                lost_core,
                deadline_ticks=28,
            )
        ],
        prepare_deadline=24,
        prepare_abort=lost_core,
    )

    relay_catch = operation(
        "relay-catch",
        [condition("own-carriers-in-zone", zone="forward-field")],
        [
            task(
                "relay-carrier",
                [1],
                position(
                    "anchor-offset",
                    "task:relay-carrier",
                    fallback="risk-fork",
                ),
                permits_carrying=True,
                requires_carrying=True,
                role="thrower",
                engagement="defend-in-place",
                signature="conserve",
            ),
            task(
                "receiver",
                [0],
                position(
                    "anchor-offset",
                    "task:relay-carrier",
                    fallback="safe-pre-fork",
                ),
                role="receiver",
                engagement="defend-in-place",
                signature="conserve",
            ),
        ],
        [
            branch(
                "throw-home",
                [condition("always")],
                [
                    task(
                        "relay-carrier",
                        [1],
                        position(
                            "anchor-offset",
                            "task:relay-carrier",
                            fallback="risk-fork",
                        ),
                        permits_carrying=True,
                        requires_carrying=True,
                        role="thrower",
                        signature="aggressive",
                    ),
                    task(
                        "receiver",
                        [0],
                        position(
                            "anchor-offset",
                            "task:relay-carrier",
                            fallback="safe-pre-fork",
                        ),
                        role="receiver",
                        engagement="defend-in-place",
                        signature="conserve",
                    ),
                ],
                [
                    condition(
                        "task-participants-carrying",
                        subject="receiver",
                    )
                ],
                [condition("always", "equals", 0)],
                deadline_ticks=24,
            )
        ],
        prepare_deadline=36,
        prepare_abort=lost_core,
    )

    decoy_switch = operation(
        "decoy-switch",
        [
            condition("ticks-until-next-well", "at-most", 20),
            condition("visible-enemies-in-zone", zone="north-forward"),
        ],
        [
            task(
                "decoy",
                [0],
                position("path", "feint-north", arrival="zone", fallback="north-stage"),
                role="decoy",
                engagement="conceal",
                signature="conserve",
            ),
            task(
                "hitters",
                [6, 7],
                position("path", "feint-south-stage", arrival="zone", fallback="south-stage"),
                minimum=2,
                role="pincer",
                signature="conserve",
            ),
        ],
        [
            branch(
                "south-pincer",
                [
                    condition(
                        "task-participants-in-zone",
                        zone="north-stage",
                        subject="decoy",
                    ),
                    condition(
                        "task-participants-in-zone",
                        value=2,
                        zone="south-stage",
                        subject="hitters",
                    ),
                    condition("visible-enemies-in-zone", zone="north-forward"),
                ],
                [
                    task(
                        "decoy",
                        [0],
                        position("zone", "north-forward"),
                        role="decoy",
                        engagement="defend-in-place",
                        signature="defensive",
                    ),
                    task(
                        "hitters",
                        [6, 7],
                        position("path", "feint-south-pincer", arrival="zone", fallback="south-forward"),
                        minimum=2,
                        role="pincer",
                        signature="aggressive",
                    ),
                ],
                [
                    condition(
                        "task-participants-in-zone",
                        value=2,
                        zone="south-forward",
                        subject="hitters",
                    )
                ],
                [condition("visible-enemies-in-zone", value=3, zone="home-side")],
                deadline_ticks=36,
            )
        ],
        prepare_deadline=44,
    )

    emergency_exchange = operation(
        "emergency-exchange",
        [
            condition("own-carrier-min-health", "at-most", 3),
            condition("own-carriers-in-zone", zone="risk-fork"),
        ],
        [
            task(
                "carrier",
                list(range(8)),
                position("path", "return-primary", arrival="zone", fallback="home-side"),
                permits_carrying=True,
                requires_carrying=True,
                role="wounded-carrier",
                engagement="defend-in-place",
                signature="defensive",
            ),
            task(
                "exchanger",
                [7],
                position("anchor-offset", "nearest-own-carrier", fallback="home-gate"),
                role="exchanger",
                engagement="defend-in-place",
                signature="aggressive",
            ),
        ],
        [
            branch(
                "exchange-and-return",
                [condition("own-carrier-min-health", "at-most", 3)],
                [
                    task(
                        "carrier",
                        list(range(8)),
                        position("path", "return-primary", arrival="zone", fallback="home-side"),
                        permits_carrying=True,
                        requires_carrying=True,
                        role="wounded-carrier",
                        engagement="opportunistic",
                        signature="defensive",
                    ),
                    task(
                        "exchanger",
                        [7],
                        position("anchor-offset", "nearest-own-carrier", fallback="home-gate"),
                        role="exchanger",
                        engagement="defend-in-place",
                        signature="aggressive",
                    ),
                ],
                [
                    condition(
                        "operation-action-used-and-own-carrier-in-zone",
                        zone="home-side",
                        subject="exchange",
                    )
                ],
                lost_core,
                deadline_ticks=26,
            )
        ],
        prepare_deadline=24,
        prepare_abort=lost_core,
    )

    return [
        {
            "id": "rear-hook",
            "slug": "rear-hook",
            "sheetStrategy": "rear-ambush",
            "mission": "Pre-position two Towlines and force one exact returning Core loose.",
            "distinctiveCost": "Both outer interceptors abandon ordinary Well pressure before a carrier is confirmed.",
            "requiredActionIds": ["tractor-hook"],
            "operation": rear_hook,
        },
        {
            "id": "lantern-sweep",
            "slug": "lantern-sweep",
            "sheetStrategy": "balanced",
            "mission": "Probe a risky fork, choose one route once, and bring the carrier home.",
            "distinctiveCost": "The carrier pauses while a Lantern and screen leave their baseline jobs.",
            "requiredActionIds": [],
            "operation": lantern_sweep,
        },
        {
            "id": "fork-shadow",
            "slug": "fork-shadow",
            "sheetStrategy": "rear-ambush",
            "mission": "React to a revealed return route and force the exact carrier off that lane.",
            "distinctiveCost": "The Towlines react late and must choose north or south from causal sight.",
            "requiredActionIds": ["tractor-hook"],
            "operation": fork_shadow,
        },
        {
            "id": "birth-rotation",
            "slug": "birth-rotation",
            "sheetStrategy": "well-rotation",
            "mission": "Move two of three reserves across theaters before the next public Well beat.",
            "distinctiveCost": "Two bodies give up current-theater pressure for a timed future position.",
            "requiredActionIds": [],
            "operation": birth_rotation,
        },
        {
            "id": "escort-counterpunch",
            "slug": "escort-counterpunch",
            "sheetStrategy": "escort-counterpunch",
            "mission": "Hold a pressured carrier, then choose a direct or counter route home.",
            "distinctiveCost": "A carrier and guard surrender delivery tempo to answer visible pressure.",
            "requiredActionIds": [],
            "operation": escort_counterpunch,
        },
        {
            "id": "smoke-breach",
            "slug": "smoke-breach",
            "sheetStrategy": "trap-punish",
            "mission": "Stage a Veil-led pair and cross the contested centre under signature cover.",
            "distinctiveCost": "The breach concentrates a Veil and one reserve into one exposed lane.",
            "requiredActionIds": ["smoke-canister"],
            "operation": smoke_breach,
        },
        {
            "id": "hardlight-gate",
            "slug": "hardlight-gate",
            "sheetStrategy": "fortress",
            "mission": "Build a two-body gate that keeps a pressured carrier's home line open.",
            "distinctiveCost": "A Mason and Lantern stop contesting objectives to protect one return.",
            "requiredActionIds": ["hardlight-block"],
            "operation": hardlight_gate,
        },
        {
            "id": "relay-catch",
            "slug": "relay-catch",
            "sheetStrategy": "relay-chain",
            "mission": "Set a receiver behind a Relay carrier and complete a bounded Core return.",
            "distinctiveCost": "The receiver holds a catch lane instead of fighting or collecting.",
            "requiredActionIds": ["arc-toss"],
            "operation": relay_catch,
        },
        {
            "id": "decoy-switch",
            "slug": "decoy-switch",
            "sheetStrategy": "feint",
            "mission": "Show one body north, then send a paired strike through the south lane.",
            "distinctiveCost": "The decoy is isolated and the opposite pair concedes centre coverage.",
            "requiredActionIds": [],
            "operation": decoy_switch,
        },
        {
            "id": "emergency-exchange",
            "slug": "emergency-exchange",
            "sheetStrategy": "intercept",
            "mission": "Bring a Switchback to a wounded carrier and complete the emergency return.",
            "distinctiveCost": "The Switchback leaves interception before the carrier is guaranteed to survive.",
            "requiredActionIds": ["exchange"],
            "operation": emergency_exchange,
        },
    ]


def common_sheet(source: dict) -> dict:
    sheet = deepcopy(source)
    sheet["sheetId"] = "intelligent-operation-proof-baseline-v1"
    sheet["composition"] = [
        "kestrel",
        "relay",
        "lantern",
        "palisade",
        "towline",
        "towline",
        "veil",
        "switchback",
    ]
    roles = [
        ("north", "carrier", 2),
        ("south", "carrier", 3),
        ("north", "screen", 0),
        ("south", "screen", 1),
        ("north", "intercept", 0),
        ("south", "intercept", 1),
        ("centre", "reserve", 7),
        ("centre", "reserve", 6),
    ]
    for slot, (theater, role, partner) in zip(sheet["slots"], roles):
        slot["theater"] = theater
        slot["role"] = role
        slot["partnerUnitId"] = partner
        slot["defaultIntent"] = {
            "position": position("base-assignment", "", arrival="base-assignment"),
            "engagementIntent": "normal",
            "signatureIntent": "normal",
        }
    sheet["zones"].update(
        {
            "home-side": [2, 1, 16, 21],
            "home-gate": [6, 7, 11, 15],
            "home-risk": [6, 4, 15, 18],
            "risk-fork": [9, 4, 15, 18],
            "safe-pre-fork": [8, 7, 12, 15],
            "rear-pocket": [13, 3, 19, 19],
            "enemy-return-corridor": [15, 2, 26, 20],
            "north-return": [15, 2, 26, 10],
            "south-return": [15, 12, 26, 20],
            "north-stage": [10, 2, 16, 9],
            "south-stage": [10, 13, 16, 20],
            "centre-stage": [9, 7, 15, 15],
            "north-forward": [14, 1, 22, 9],
            "south-forward": [14, 13, 22, 21],
            "centre-forward": [15, 7, 22, 15],
            "forward-field": [12, 1, 24, 21],
            "forward-objectives": [13, 1, 20, 21],
        }
    )
    sheet["paths"].update(
        {
            "hook-stage-north": [[5, 8], [9, 6], [13, 5], [16, 6]],
            "hook-stage-south": [[5, 14], [9, 16], [13, 17], [16, 16]],
            "shadow-north": [[8, 7], [12, 5], [16, 6]],
            "shadow-south": [[8, 15], [12, 17], [16, 16]],
            "lantern-probe": [[9, 10], [11, 8], [13, 7]],
            "rotation-stage": [[7, 11], [9, 10], [10, 12]],
            "smoke-stage": [[7, 11], [10, 11], [13, 11]],
            "smoke-breach-line": [[13, 11], [16, 11], [19, 11]],
            "hardlight-gate-line": [[6, 9], [8, 10], [10, 11]],
            "feint-north": [[7, 7], [10, 5], [14, 4]],
            "feint-south-stage": [[7, 15], [10, 17], [13, 18]],
            "feint-south-pincer": [[13, 18], [16, 18], [19, 17]],
        }
    )
    sheet["gambits"] = []
    sheet["operations"] = []
    sheet["auditStatus"] = {
        "playerFacingProductSchema": False,
        "provisionalEvaluationOnly": True,
        "purpose": "ten-operation live success proof",
    }
    sheet["dynamicStrategyAudit"] = {
        "family": "intelligent-operation-proof",
        "pairedControl": True,
        "playerFacingProductSchema": False,
        "provisionalEvaluationOnly": True,
    }
    return sheet


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def main() -> int:
    source = json.loads(SOURCE.read_text(encoding="utf-8"))
    base = common_sheet(source)
    write_json(OUTPUT / "sheets/baseline.json", base)

    catalog_cards = []
    for card in cards():
        sheet = deepcopy(base)
        sheet["sheetId"] = (
            f"intelligent-operation-proof-{card['sheetStrategy']}-"
            f"{card['slug']}-v1"
        )
        sheet["operations"] = [card["operation"]]
        if card["id"] == "relay-catch":
            sheet["composition"][0] = "relay"
            sheet["slots"][0]["partnerUnitId"] = 1
            sheet["slots"][0]["role"] = "screen"
            sheet["slots"][0]["theater"] = "south"
            sheet["slots"][1]["partnerUnitId"] = 0
            sheet["slots"][1]["role"] = "carrier"
            sheet["slots"][1]["theater"] = "south"
        if card["id"] == "hardlight-gate":
            sheet["composition"][3] = "mason"
        if card["id"] == "emergency-exchange":
            sheet["policies"]["carrier"]["handoffHealthAtOrBelow"] = 3
        filename = f"sheets/{card['slug']}.json"
        write_json(OUTPUT / filename, sheet)
        catalog_cards.append(
            {
                "id": card["id"],
                "sheet": filename,
                "mission": card["mission"],
                "distinctiveCost": card["distinctiveCost"],
                "requiredActionIds": card["requiredActionIds"],
            }
        )

    write_json(
        OUTPUT / "catalog.json",
        {
            "schema": "arc-relay-intelligent-operation-proof-catalog-v1",
            "cohortId": "intelligent-operation-proof-v1",
            "mapProfile": "depth-counterflow",
            "proofSeed": "86080201",
            "runtimeForEvidence": "wasm",
            "baselineSheet": "sheets/baseline.json",
            "acceptance": {
                "requiredStates": ["prepare", "commit", "recover", "dormant"],
                "requiredCommitExit": "mission-success",
                "requireRecoveryRelease": True,
                "requireNextTickBaseline": True,
            },
            "cards": catalog_cards,
        },
    )
    print(f"Generated baseline + {len(catalog_cards)} operation sheets in {OUTPUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
