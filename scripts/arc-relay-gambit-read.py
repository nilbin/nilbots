#!/usr/bin/env python3
"""Measure how evaluation gambits changed roles and actions in a depth audit."""

from __future__ import annotations

import argparse
import collections
import gzip
import json
from pathlib import Path
from typing import Any


def read_json(path: Path) -> dict[str, Any]:
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: expected object")
    return value


def intervals(ticks: list[int]) -> list[list[int]]:
    if not ticks:
        return []
    result: list[list[int]] = []
    start = previous = ticks[0]
    for tick in ticks[1:]:
        if tick != previous + 1:
            result.append([start, previous])
            start = tick
        previous = tick
    result.append([start, previous])
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("audit", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    root = args.audit.resolve()
    plan = read_json(root / "audit-plan.json")
    legacy_results = root / "results.json"
    sweep_results = root / "sweep/attempt-01/results.json"
    if legacy_results.is_file():
        results = read_json(legacy_results)
        matches = [
            (item["header"], root / item["broadcast"])
            for item in results["matches"]
        ]
    elif sweep_results.is_file():
        results = read_json(sweep_results)
        headers = {item["matchId"]: item for item in plan["matches"]}
        attempt = sweep_results.parent
        matches = [
            (
                headers[item["cellId"]],
                attempt / item["attempt"] / "broadcast.json.gz",
            )
            for item in results["cells"]
        ]
    else:
        raise FileNotFoundError("no legacy or sweep results found")
    variants = {item["variantId"]: item for item in plan["variants"]}
    base_roles: dict[str, dict[int, str]] = {}
    for variant_id, variant in variants.items():
        sheet = read_json(root / variant["sheet"])
        base_roles[variant_id] = {
            int(slot["unitId"]): str(slot["role"]) for slot in sheet["slots"]
        }

    by_variant: dict[str, dict[str, Any]] = {}
    for variant_id, variant in variants.items():
        if variant["adaptationStyle"] != "gambit":
            continue
        by_variant[variant_id] = {
            "matches": 0,
            "liveBodyTicks": 0,
            "overriddenBodyTicks": 0,
            "waitBodyTicks": 0,
            "overriddenWaitBodyTicks": 0,
            "fullSquadSingleRoleTicks": 0,
            "overrideRoleBodyTicks": collections.Counter(),
            "overrideWindowCount": 0,
            "overrideWindowTicks": 0,
            "maxOverrideWindowTicks": 0,
        }

    for header, broadcast_path in matches:
        broadcast = read_json(broadcast_path)
        for team_id in (0, 1):
            variant_id = header[f"team{team_id}VariantId"]
            if variant_id not in by_variant:
                continue
            aggregate = by_variant[variant_id]
            aggregate["matches"] += 1
            changed_ticks: list[int] = []
            for tick, turns in enumerate(broadcast["turns"]):
                team_turns = [turn for turn in turns if int(turn[1]) == team_id]
                if not team_turns:
                    continue
                roles: list[str] = []
                changed = False
                for turn in team_turns:
                    unit_id = int(turn[0][1])
                    role = turn[2] or base_roles[variant_id][unit_id]
                    action = turn[5][0]
                    overridden = role != base_roles[variant_id][unit_id]
                    roles.append(role)
                    aggregate["liveBodyTicks"] += 1
                    if action == "wait":
                        aggregate["waitBodyTicks"] += 1
                    if overridden:
                        changed = True
                        aggregate["overriddenBodyTicks"] += 1
                        aggregate["overrideRoleBodyTicks"][role] += 1
                        if action == "wait":
                            aggregate["overriddenWaitBodyTicks"] += 1
                if changed:
                    changed_ticks.append(tick)
                if len(team_turns) >= 2 and len(set(roles)) == 1:
                    aggregate["fullSquadSingleRoleTicks"] += 1
            windows = intervals(changed_ticks)
            aggregate["overrideWindowCount"] += len(windows)
            for start, end in windows:
                duration = end - start + 1
                aggregate["overrideWindowTicks"] += duration
                aggregate["maxOverrideWindowTicks"] = max(
                    aggregate["maxOverrideWindowTicks"], duration
                )

    normalized: dict[str, Any] = {}
    totals = collections.Counter()
    for variant_id, value in by_variant.items():
        live = value["liveBodyTicks"]
        overridden = value["overriddenBodyTicks"]
        normalized[variant_id] = {
            **value,
            "overrideRoleBodyTicks": dict(value["overrideRoleBodyTicks"]),
            "overriddenBodyTickRate": overridden / live if live else 0,
            "waitRate": value["waitBodyTicks"] / live if live else 0,
            "overriddenWaitRate": (
                value["overriddenWaitBodyTicks"] / overridden
                if overridden else 0
            ),
        }
        for key in (
            "matches", "liveBodyTicks", "overriddenBodyTicks", "waitBodyTicks",
            "overriddenWaitBodyTicks", "fullSquadSingleRoleTicks",
            "overrideWindowCount", "overrideWindowTicks",
        ):
            totals[key] += value[key]
        totals["maxOverrideWindowTicks"] = max(
            totals["maxOverrideWindowTicks"], value["maxOverrideWindowTicks"]
        )

    output = {
        "schema": "arc-relay-gambit-execution-read-v1",
        "sourceAudit": str(root),
        "aggregate": {
            **dict(totals),
            "overriddenBodyTickRate": (
                totals["overriddenBodyTicks"] / totals["liveBodyTicks"]
                if totals["liveBodyTicks"] else 0
            ),
            "overriddenWaitRate": (
                totals["overriddenWaitBodyTicks"]
                / totals["overriddenBodyTicks"]
                if totals["overriddenBodyTicks"] else 0
            ),
        },
        "byVariant": normalized,
    }
    rendered = json.dumps(output, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered, encoding="utf-8")
        print(args.output)
    else:
        print(rendered, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
