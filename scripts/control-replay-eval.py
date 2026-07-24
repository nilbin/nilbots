#!/usr/bin/env python3
"""Aggregate authoritative shared-control behavior from replay directories.

This reports what the engine actually scored rather than inferring intent from
bot debug text: zone occupancy/holding, pressure gain and decay, non-Wait
scoring, and contested-to-sole transitions. It complements balance-eval.py's
outcomes and arc-replay-eval.py's projectile evidence.
"""

import argparse
import collections
import json
import pathlib


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


def pressure_favors(slot, before, after):
    direction = 1 if slot == 0 else -1
    return (after - before) * direction > 0


def analyze(document):
    header = document["header"]
    if header.get("controlPressureLimit") is None:
        return None

    zone = {tuple(tile) for tile in header.get("zoneTiles") or []}
    participants = header["participants"]
    previous_state = {
        participant["slot"]: (
            participant["spawnX"],
            participant["spawnY"],
            True,
        )
        for participant in participants
    }
    previous_pressure = 0
    metrics = collections.Counter()
    action_gains = collections.Counter()

    for tick in document["ticks"]:
        state = {
            bot["slot"]: (bot["x"], bot["y"], bot["status"] == "Active")
            for bot in tick["state"]
        }
        previous_occupants = {
            slot
            for slot, (x, y, active) in previous_state.items()
            if active and (x, y) in zone
        }
        occupants = {
            slot
            for slot, (x, y, active) in state.items()
            if active and (x, y) in zone
        }
        pressure = tick.get("controlPressure") or 0
        bot_ticks = {bot["slot"]: bot for bot in tick["bots"]}

        if len(occupants) == 1:
            metrics["sole_occupancy_ticks"] += 1
            occupant = next(iter(occupants))
            if pressure_favors(occupant, previous_pressure, pressure):
                action = bot_ticks[occupant]["validatedAction"]
                metrics["sole_occupancy_gain_ticks"] += 1
                metrics["sole_occupancy_nonwait_gain_ticks"] += int(action != "Wait")
                action_gains[action] += 1
        elif len(occupants) > 1:
            metrics["contested_ticks"] += 1
            metrics["contested_decay_steps"] += int(
                abs(pressure) < abs(previous_pressure)
            )
        else:
            metrics["empty_ticks"] += 1
            metrics["empty_decay_steps"] += int(abs(pressure) < abs(previous_pressure))

        if len(previous_occupants) > 1 and len(occupants) == 1:
            occupant = next(iter(occupants))
            metrics["contest_to_sole_transitions"] += 1
            metrics["contest_to_sole_same_tick_gains"] += int(
                pressure_favors(occupant, previous_pressure, pressure)
            )
            damage_targets = {
                event.get("targetSlot")
                for event in tick["events"]
                if event["type"] in {"Damage", "Destroyed"}
            }
            metrics["contest_to_sole_with_damage"] += int(
                any(slot in damage_targets for slot in previous_occupants - occupants)
            )

        previous_state = state
        previous_pressure = pressure

    return metrics, action_gains


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("roots", nargs="+", help="replay files or directories")
    args = parser.parse_args()

    by_rules = {}
    for path in replay_files(args.roots):
        document = json.loads(path.read_text())
        analyzed = analyze(document)
        if analyzed is None:
            continue
        rules = document["header"]["gameRulesVersion"]
        bucket = by_rules.setdefault(
            rules,
            {
                "games": 0,
                "metrics": collections.Counter(),
                "actions": collections.Counter(),
            },
        )
        metrics, actions = analyzed
        bucket["games"] += 1
        bucket["metrics"].update(metrics)
        bucket["actions"].update(actions)

    ordered = (
        "sole_occupancy_ticks",
        "sole_occupancy_gain_ticks",
        "sole_occupancy_nonwait_gain_ticks",
        "contested_ticks",
        "contested_decay_steps",
        "empty_ticks",
        "empty_decay_steps",
        "contest_to_sole_transitions",
        "contest_to_sole_same_tick_gains",
        "contest_to_sole_with_damage",
    )
    for rules, bucket in sorted(by_rules.items()):
        print(f"{rules}")
        print(f"  games={bucket['games']}")
        print(
            "  control "
            + " ".join(f"{key}={bucket['metrics'][key]}" for key in ordered)
        )
        print(
            "  gain_actions "
            + " ".join(
                f"{action}={count}"
                for action, count in sorted(bucket["actions"].items())
            )
        )


if __name__ == "__main__":
    main()
