#!/usr/bin/env python3
"""Aggregate programmed-shot evidence from one or more replay directories.

This complements balance-eval.py's outcome table with mechanic-specific facts:
explicit programs, bends that physically manifest, attributed curved hits beyond
the launch tile, and active holders that vacate a tile an arc crosses that tick.
It reads authoritative replay fields only; it never tries to infer private plans
from a bot observation.
"""

import argparse
import collections
import json
import pathlib
import statistics


def replay_files(roots):
    seen = set()
    for root in roots:
        path = pathlib.Path(root)
        candidates = [path] if path.is_file() else path.rglob("replay.json")
        for candidate in candidates:
            resolved = candidate.resolve()
            if resolved not in seen:
                seen.add(resolved)
                yield candidate


def headings(origin, path):
    result = []
    previous = origin
    for point in path:
        result.append(
            (
                (point[0] > previous[0]) - (point[0] < previous[0]),
                (point[1] > previous[1]) - (point[1] < previous[1]),
            )
        )
        previous = point
    return result


def bends_by(origin, full_path, target):
    path_headings = headings(origin, full_path)
    first = path_headings[0] if path_headings else None
    for index, point in enumerate(full_path):
        if point == target:
            return any(heading != first for heading in path_headings[: index + 1])
    return False


def analyze_replay(document):
    header = document["header"]
    ticks = document["ticks"]
    zone = {tuple(tile) for tile in header.get("zoneTiles", [])}
    names = {participant["slot"]: participant["name"] for participant in header["participants"]}
    pre_positions = {
        participant["slot"]: (participant["spawnX"], participant["spawnY"])
        for participant in header["participants"]
    }
    metadata = {}
    previous_projectiles = []
    metrics = collections.Counter()
    games_with = set()

    for tick in ticks:
        bot_ticks = {bot["slot"]: bot for bot in tick["bots"]}
        state_positions = {state["slot"]: (state["x"], state["y"]) for state in tick["state"]}
        traversals = tick.get("projectileTraversals") or []

        for bot in bot_ticks.values():
            program = bot.get("shotProgram")
            if program is None:
                continue
            metrics["program_decisions"] += 1
            metrics["curved_decisions"] += int(program["bendCount"] > 0)

        for traversal in traversals:
            projectile_id = traversal["id"]
            if projectile_id in metadata:
                continue
            owner = traversal["ownerSlot"]
            program = bot_ticks.get(owner, {}).get("shotProgram")
            if program is None:
                continue
            full_path = [tuple(point) for point in traversal.get("programmedPath") or []]
            origin = (traversal["fromX"], traversal["fromY"])
            is_curved = program["bendCount"] > 0
            manifested = is_curved and len(set(headings(origin, full_path))) > 1
            metadata[projectile_id] = {
                "owner": owner,
                "origin": origin,
                "path": full_path,
                "curved": is_curved,
                "manifested": manifested,
            }
            metrics["programmed_launches"] += 1
            metrics["curved_launches"] += int(is_curved)
            metrics["manifested_curves"] += int(manifested)
            if program["initialAimOffset"] != 0:
                metrics["offset_aims"] += 1

        damage = [
            event
            for event in tick["events"]
            if event["type"] == "Damage"
        ]
        for event in damage:
            owner = event["slot"]
            target = event["targetSlot"]
            target_position = state_positions[target]
            candidates = []
            for traversal in traversals:
                if traversal["ownerSlot"] != owner:
                    continue
                if target_position in {
                    tuple(point) for point in traversal.get("path") or []
                }:
                    candidates.append(traversal["id"])
            for projectile in previous_projectiles:
                if (
                    projectile["ownerSlot"] == owner
                    and (projectile["x"], projectile["y"]) == target_position
                ):
                    candidates.append(projectile["id"])
            candidates = [candidate for candidate in candidates if candidate in metadata]
            if len(candidates) != 1:
                continue
            meta = metadata[candidates[0]]
            metrics["programmed_hits"] += 1
            if not meta["curved"]:
                continue
            metrics["curved_hits"] += 1
            if bends_by(meta["origin"], meta["path"], target_position):
                metrics["hits_after_bend"] += 1
            distance = max(
                abs(meta["origin"][0] - target_position[0]),
                abs(meta["origin"][1] - target_position[1]),
            )
            launch_tiles = header.get("programmedShotLimits", {}).get("launchTiles", 1)
            if distance > launch_tiles:
                metrics["ranged_curved_hits"] += 1
                games_with.add("ranged_curved_hit")

        damaged_pairs = {
            (event["slot"], event["targetSlot"]) for event in damage
        }
        for traversal in traversals:
            meta = metadata.get(traversal["id"])
            if meta is None or not meta["manifested"]:
                continue
            owner = traversal["ownerSlot"]
            crossed = {tuple(point) for point in traversal.get("path") or []}
            for target, bot in bot_ticks.items():
                if target == owner or pre_positions[target] not in crossed:
                    continue
                if pre_positions[target] not in zone:
                    continue
                if bot["validatedAction"] not in {
                    "MoveForward",
                    "TurnLeft",
                    "TurnRight",
                    "StrafeLeft",
                    "StrafeRight",
                }:
                    continue
                if (owner, target) in damaged_pairs:
                    continue
                metrics["vacated_arc_crossings"] += 1
                games_with.add("vacated_arc_crossing")

        pre_positions = state_positions
        previous_projectiles = tick.get("projectiles") or []

    metrics["games_with_ranged_curved_hit"] = int("ranged_curved_hit" in games_with)
    metrics["games_with_vacated_arc_crossing"] = int(
        "vacated_arc_crossing" in games_with
    )
    return metrics, names


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("roots", nargs="+", help="replay files or directories")
    args = parser.parse_args()

    by_rules = {}
    for path in replay_files(args.roots):
        document = json.loads(path.read_text())
        rules = document["header"]["gameRulesVersion"]
        bucket = by_rules.setdefault(
            rules,
            {
                "games": 0,
                "draws": 0,
                "eliminations": 0,
                "max_ticks": 0,
                "ticks": [],
                "records": collections.defaultdict(collections.Counter),
                "arc": collections.Counter(),
            },
        )
        result = document["result"]
        bucket["games"] += 1
        bucket["draws"] += int(result.get("winnerSlot") is None)
        bucket["eliminations"] += int(result["reason"] == "Elimination")
        bucket["max_ticks"] += int(result["reason"] == "MaxTicks")
        bucket["ticks"].append(result["endTick"])
        winner = result.get("winnerSlot")
        names = {
            participant["slot"]: participant["name"]
            for participant in document["header"]["participants"]
        }
        for slot, name in names.items():
            bucket["records"][name][
                "draws" if winner is None else "wins" if winner == slot else "losses"
            ] += 1
        arc_metrics, _ = analyze_replay(document)
        bucket["arc"].update(arc_metrics)

    for rules, bucket in sorted(by_rules.items()):
        ticks = bucket["ticks"]
        print(rules)
        print(
            "  games={games} draws={draws} eliminations={eliminations} "
            "median={median:.1f} average={average:.1f} maxTicks={max_ticks}".format(
                **bucket,
                median=statistics.median(ticks),
                average=statistics.fmean(ticks),
            )
        )
        records = sorted(
            bucket["records"].items(),
            key=lambda item: (-item[1]["wins"], item[1]["losses"], item[0]),
        )
        print(
            "  records "
            + "  ".join(
                f"{name} {record['wins']}-{record['losses']}-{record['draws']}"
                for name, record in records
            )
        )
        if bucket["arc"]["program_decisions"]:
            ordered = (
                "program_decisions",
                "curved_decisions",
                "programmed_launches",
                "curved_launches",
                "manifested_curves",
                "offset_aims",
                "programmed_hits",
                "curved_hits",
                "hits_after_bend",
                "ranged_curved_hits",
                "vacated_arc_crossings",
                "games_with_ranged_curved_hit",
                "games_with_vacated_arc_crossing",
            )
            print(
                "  arcs "
                + " ".join(f"{key}={bucket['arc'][key]}" for key in ordered)
            )


if __name__ == "__main__":
    main()
