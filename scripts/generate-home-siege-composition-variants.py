#!/usr/bin/env python3
"""Generate disclosed data-only Home Siege composition screening sheets."""

from __future__ import annotations

import argparse
import copy
import json
from pathlib import Path


VARIANTS = {
    "bulwark": ["relay", "kestrel", "patchbay", "patchbay", "palisade", "palisade", "repulsor", "repulsor"],
    "paint-rail": ["relay", "kestrel", "patchbay", "patchbay", "sunder", "sunder", "longshot", "longshot"],
    "hook-paint": ["relay", "kestrel", "patchbay", "patchbay", "towline", "towline", "sunder", "sunder"],
    "hook-rail": ["relay", "kestrel", "patchbay", "patchbay", "towline", "towline", "longshot", "longshot"],
    "paint-mortar": ["relay", "kestrel", "patchbay", "patchbay", "sunder", "sunder", "mortar", "mortar"],
    "paint-nest": ["relay", "kestrel", "patchbay", "patchbay", "sunder", "sunder", "nest", "nest"],
    "paint-mines": ["relay", "kestrel", "patchbay", "patchbay", "sunder", "sunder", "minesmith", "minesmith"],
    "paint-hush": ["relay", "kestrel", "patchbay", "patchbay", "sunder", "sunder", "hush", "hush"],
    "paint-veil": ["relay", "kestrel", "patchbay", "patchbay", "sunder", "sunder", "veil", "veil"],
    "paint-mason": ["relay", "kestrel", "patchbay", "patchbay", "sunder", "sunder", "mason", "mason"],
    "paint-palisade": ["relay", "kestrel", "patchbay", "patchbay", "sunder", "sunder", "palisade", "palisade"],
    "paint-repulsor": ["relay", "kestrel", "patchbay", "patchbay", "sunder", "sunder", "repulsor", "repulsor"],
    "fast-paint": ["relay", "relay", "kestrel", "kestrel", "patchbay", "patchbay", "sunder", "sunder"],
    "kestrel-rail": ["kestrel", "kestrel", "patchbay", "patchbay", "sunder", "sunder", "longshot", "longshot"],
    "relay-rail": ["relay", "relay", "patchbay", "patchbay", "sunder", "sunder", "longshot", "longshot"],
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("template", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--batch-plans", type=Path)
    parser.add_argument("--baseline-sheet", type=Path)
    parser.add_argument("--seed", default="104729")
    parser.add_argument("--loop-profile", default="forward-combat")
    args = parser.parse_args()
    source = json.loads(args.template.read_text(encoding="utf-8"))
    args.output.mkdir(parents=True, exist_ok=True)
    for name, composition in VARIANTS.items():
        document = copy.deepcopy(source)
        document["sheetId"] = f"home-siege-{name}-screen-v1"
        document["composition"] = composition
        path = args.output / f"{name}.json"
        path.write_text(
            json.dumps(document, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
    timing_root = args.output.parent / "timing-screen"
    timing_root.mkdir(parents=True, exist_ok=True)
    for threshold in (30, 40, 45, 50):
        document = copy.deepcopy(source)
        document["sheetId"] = f"home-siege-paint-repulsor-hot-{threshold}-screen-v1"
        document["composition"] = VARIANTS["paint-repulsor"]
        for phase in document["standingStrategy"]["phases"]:
            for assignment in phase["assignments"]:
                for group in assignment.get("when", []):
                    for condition in group.get("all", []):
                        if condition.get("fact") == "ticks-without-objective-progress":
                            condition["value"] = threshold
        path = timing_root / f"hot-{threshold}.json"
        path.write_text(
            json.dumps(document, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
    lane_root = args.output.parent / "lane-screen"
    lane_root.mkdir(parents=True, exist_ok=True)
    for name, lane, perspective in (
        ("absolute-north", "north", None),
        ("absolute-south", "south", None),
        ("team-relative", "north", "team-relative"),
    ):
        document = copy.deepcopy(source)
        document["sheetId"] = f"home-siege-paint-repulsor-{name}-screen-v1"
        document["composition"] = VARIANTS["paint-repulsor"]
        parameters = document["standingStrategy"]["parameters"]
        parameters["lane"] = lane
        parameters["scoreWell"] = lane
        if perspective is None:
            parameters.pop("lanePerspective", None)
        else:
            parameters["lanePerspective"] = perspective
        path = lane_root / f"{name}.json"
        path.write_text(
            json.dumps(document, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
    print(f"generated {len(VARIANTS)} composition sheets in {args.output}")
    print(f"generated 4 timing sheets in {timing_root}")
    print(f"generated 3 lane sheets in {lane_root}")
    if args.batch_plans is not None:
        if args.baseline_sheet is None:
            parser.error("--batch-plans requires --baseline-sheet")
        args.batch_plans.mkdir(parents=True, exist_ok=True)
        standing_mind = "arena-bots/arc-relay/stock-mind-v5"
        baseline_mind = "arena-bots/arc-relay/stock-mind-v4"
        for assignment in (0, 1):
            cells = []
            for name in VARIANTS:
                subject = str(args.output / f"{name}.json")
                baseline = str(args.baseline_sheet)
                cells.append({
                    "cellId": f"{name}-a{assignment}",
                    "sheet0": subject if assignment == 0 else baseline,
                    "sheet1": baseline if assignment == 0 else subject,
                    "seed": args.seed,
                })
            document = {
                "schema": "arc-relay-screen-batch-v1",
                "bot": standing_mind if assignment == 0 else baseline_mind,
                "opponent": baseline_mind if assignment == 0 else standing_mind,
                "loopProfile": args.loop_profile,
                "cells": cells,
            }
            plan = args.batch_plans / f"composition-a{assignment}.json"
            plan.write_text(
                json.dumps(document, indent=2) + "\n", encoding="utf-8")
        print(f"generated paired composition plans in {args.batch_plans}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
