#!/usr/bin/env python3
"""Measure combat activity and repetition from canonical nilbots replays.

Outcome tables answer who won. This script answers whether the games moved,
changed, and created visible pressure. It is intentionally cohort-aware:
repeat --group with the same label to merge blocks from one native generation.

Example:
  python3 scripts/replay-dynamics-eval.py \
    --group previous-v7=/tmp/run/block1/cone-active-bolt2-arcs \
    --group previous-v7=/tmp/run/block2/cone-active-bolt2-arcs \
    --group current-v8=/tmp/new/block1/cone-occupancy-bolt2-arcs

Metric definitions live in docs/EVALUATION-METHODOLOGY.md. No composite
"fun score" is emitted: the dimensions stay visible and replay review remains
part of the decision.
"""

import argparse
import collections
import json
import math
import pathlib
import statistics


ACTION_FAMILIES = {
    "MoveForward": "Move",
    "StrafeLeft": "Move",
    "StrafeRight": "Move",
    "TurnLeft": "Turn",
    "TurnRight": "Turn",
    "Shoot": "Shoot",
    "Wait": "Wait",
}
WORLD_EVENT_TYPES = {"Move", "Turn", "Shot", "Damage", "Destroyed"}


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


def percentile(values, fraction):
    if not values:
        return 0
    ordered = sorted(values)
    return ordered[max(0, math.ceil(len(ordered) * fraction) - 1)]


def normalized_entropy(counts):
    total = sum(counts.values())
    populated = sum(1 for count in counts.values() if count)
    if total == 0 or populated <= 1:
        return 0.0
    entropy = -sum(
        (count / total) * math.log2(count / total)
        for count in counts.values()
        if count
    )
    return entropy / math.log2(len(set(ACTION_FAMILIES.values())))


def projectile_signature(projectile):
    return (
        projectile.get("x"),
        projectile.get("y"),
        projectile.get("ownerSlot"),
        projectile.get("heading") or projectile.get("direction"),
        projectile.get("ticksUntilAdvance"),
        projectile.get("remainingTiles"),
    )


def frame_signature(tick):
    bot_state = tuple(
        (
            bot.get("slot"),
            bot.get("x"),
            bot.get("y"),
            bot.get("facing"),
            bot.get("health"),
            bot.get("status"),
        )
        for bot in sorted(tick.get("state", []), key=lambda item: item["slot"])
    )
    actions = tuple(
        (
            bot.get("slot"),
            ACTION_FAMILIES.get(bot.get("validatedAction"), bot.get("validatedAction")),
        )
        for bot in sorted(tick.get("bots", []), key=lambda item: item["slot"])
    )
    projectiles = tuple(
        sorted(projectile_signature(projectile) for projectile in tick.get("projectiles", []))
    )
    return bot_state, actions, projectiles, tick.get("controlPressure")


def initial_state(header):
    return {
        participant["slot"]: (
            participant["spawnX"],
            participant["spawnY"],
            participant["spawnFacing"],
            None,
            "Active",
        )
        for participant in header["participants"]
    }


def current_state(tick):
    return {
        bot["slot"]: (
            bot["x"],
            bot["y"],
            bot["facing"],
            bot["health"],
            bot["status"],
        )
        for bot in tick.get("state", [])
    }


def analyze(document):
    header = document["header"]
    result = document["result"]
    ticks = document["ticks"]
    metrics = collections.Counter()
    action_counts = collections.defaultdict(collections.Counter)
    action_runs = collections.defaultdict(list)
    current_runs = {}
    previous_actions = {}
    previous_state = initial_state(header)
    previous_pressure = 0
    recent_frames = collections.deque(maxlen=20)
    zone = {tuple(tile) for tile in header.get("zoneTiles") or []}
    previous_occupants = {
        slot
        for slot, (x, y, _, _, status) in previous_state.items()
        if status == "Active" and (x, y) in zone
    }
    damage_ticks = set()
    stagnant_run = 0
    repeat_run = 0
    max_stagnant_run = 0
    max_repeat_run = 0

    for tick in ticks:
        metrics["ticks"] += 1
        events = tick.get("events", [])
        event_types = {event.get("type") for event in events}
        damage = sum(1 for event in events if event.get("type") == "Damage")
        shots = sum(1 for event in events if event.get("type") == "Shot")
        moves = sum(1 for event in events if event.get("type") == "Move")
        turns = sum(1 for event in events if event.get("type") == "Turn")
        metrics["damage"] += damage
        metrics["shots"] += shots
        metrics["moves"] += moves
        metrics["turns"] += turns
        if damage:
            damage_ticks.add(tick["tick"])

        state = current_state(tick)
        pressure = tick.get("controlPressure")
        pressure_changed = (
            pressure is not None
            and pressure != previous_pressure
        )
        projectile_activity = bool(
            tick.get("projectiles") or tick.get("projectileTraversals")
        )
        world_activity = bool(event_types & WORLD_EVENT_TYPES)
        if world_activity or projectile_activity or pressure_changed:
            metrics["active_world_ticks"] += 1
        if (
            not world_activity
            and not projectile_activity
            and not pressure_changed
            and state == previous_state
        ):
            metrics["stagnant_ticks"] += 1
            stagnant_run += 1
            max_stagnant_run = max(max_stagnant_run, stagnant_run)
        else:
            stagnant_run = 0

        signature = frame_signature(tick)
        if recent_frames:
            metrics["repeat_opportunities"] += 1
            if signature in recent_frames:
                metrics["recent_repeat_ticks"] += 1
                repeat_run += 1
                max_repeat_run = max(max_repeat_run, repeat_run)
            else:
                repeat_run = 0
        recent_frames.append(signature)

        for bot in tick.get("bots", []):
            slot = bot["slot"]
            action = ACTION_FAMILIES.get(
                bot.get("validatedAction"),
                bot.get("validatedAction", "Unknown"),
            )
            action_counts[slot][action] += 1
            metrics["decisions"] += 1
            metrics["wait_decisions"] += int(action == "Wait")
            if slot in previous_actions:
                metrics["switch_opportunities"] += 1
                metrics["action_switches"] += int(previous_actions[slot] != action)
            previous_actions[slot] = action

            prior_action, run_length = current_runs.get(slot, (None, 0))
            if prior_action == action:
                current_runs[slot] = (action, run_length + 1)
            else:
                if run_length:
                    action_runs[slot].append(run_length)
                current_runs[slot] = (action, 1)

        occupants = {
            slot
            for slot, (x, y, _, _, status) in state.items()
            if status == "Active" and (x, y) in zone
        }
        if len(occupants) > 1:
            metrics["contested_zone_ticks"] += 1
        if len(previous_occupants) > 1 and len(occupants) == 1:
            metrics["contest_to_sole"] += 1
            evicted = previous_occupants - occupants
            damaged = {
                event.get("targetSlot")
                for event in events
                if event.get("type") in {"Damage", "Destroyed"}
            }
            metrics["damage_evictions"] += int(bool(evicted & damaged))
        previous_occupants = occupants
        previous_state = state
        previous_pressure = pressure if pressure is not None else previous_pressure

    for slot, (_, run_length) in current_runs.items():
        if run_length:
            action_runs[slot].append(run_length)

    bot_entropies = [normalized_entropy(counts) for counts in action_counts.values()]
    all_runs = [length for runs in action_runs.values() for length in runs]
    long_repeat_decisions = sum(
        length for length in all_runs if length >= 4
    )

    metrics["games"] = 1
    metrics["draws"] = int(result.get("winnerSlot") is None)
    metrics["eliminations"] = int(result.get("reason") == "Elimination")
    metrics["max_ticks"] = int(result.get("reason") == "MaxTicks")
    metrics["faults"] = sum(bot.get("faults", 0) for bot in result.get("bots", []))
    metrics["fault_games"] = int(metrics["faults"] > 0)
    metrics["non_wasm_games"] = int(
        any(
            participant.get("runtimeKind") != "wasm"
            for participant in header.get("participants", [])
        )
    )
    metrics["damage_games"] = int(metrics["damage"] > 0)
    metrics["reciprocal_damage_games"] = int(
        len(result.get("bots", [])) >= 2
        and all(bot.get("damageDealt", 0) > 0 for bot in result["bots"])
    )
    metrics["multi_damage_tick_games"] = int(len(damage_ticks) >= 2)
    metrics["damage_ticks"] = len(damage_ticks)
    metrics["long_repeat_decisions"] = long_repeat_decisions
    metrics["stalled_games"] = int(max_stagnant_run >= 20)
    metrics["looped_games"] = int(max_repeat_run >= 20)

    winner = None
    participant_names = {
        participant["slot"]: participant["name"]
        for participant in header["participants"]
    }
    if result.get("winnerSlot") is not None:
        winner = participant_names[result["winnerSlot"]]
    return {
        "metrics": metrics,
        "end_tick": result.get("endTick", max(0, len(ticks) - 1)),
        "entropies": bot_entropies,
        "max_runs": [max(runs, default=0) for runs in action_runs.values()],
        "winner": winner,
        "records": [
            (participant_names[bot["slot"]], bot["outcome"])
            for bot in result.get("bots", [])
        ],
        "reason": result.get("reason"),
        "rules": header.get("gameRulesVersion"),
    }


def ratio(numerator, denominator):
    return 100.0 * numerator / denominator if denominator else 0.0


def aggregate(paths):
    totals = collections.Counter()
    ticks = []
    entropies = []
    max_runs = []
    winners = collections.Counter()
    records = collections.defaultdict(collections.Counter)
    reasons = collections.Counter()
    rules = collections.Counter()
    for path in replay_files(paths):
        analyzed = analyze(json.loads(path.read_text()))
        totals.update(analyzed["metrics"])
        ticks.append(analyzed["end_tick"])
        entropies.extend(analyzed["entropies"])
        max_runs.extend(analyzed["max_runs"])
        if analyzed["winner"] is not None:
            winners[analyzed["winner"]] += 1
        for name, outcome in analyzed["records"]:
            records[name][outcome] += 1
        reasons[analyzed["reason"]] += 1
        rules[analyzed["rules"]] += 1
    return totals, ticks, entropies, max_runs, winners, records, reasons, rules


def print_group(label, paths):
    totals, ticks, entropies, max_runs, winners, records, reasons, rules = aggregate(paths)
    games = totals["games"]
    decisions = totals["decisions"]
    decided = games - totals["draws"]
    print(label)
    print(
        "  outcomes "
        f"games={games} draws={totals['draws']}({ratio(totals['draws'], games):.1f}%) "
        f"eliminations={totals['eliminations']}({ratio(totals['eliminations'], games):.1f}%) "
        f"maxTicks={totals['max_ticks']} medianEndTick={statistics.median(ticks) if ticks else 0:g} "
        f"averageEndTick={statistics.mean(ticks) if ticks else 0:.1f} "
        f"p90EndTick={percentile(ticks, 0.9)} faults={totals['faults']} "
        f"nonWasmGames={totals['non_wasm_games']}"
    )
    print(
        "  combat "
        f"damageGames={totals['damage_games']}({ratio(totals['damage_games'], games):.1f}%) "
        f"damagePerGame={totals['damage'] / games if games else 0:.2f} "
        f"damagePer100Ticks={ratio(totals['damage'], totals['ticks']):.2f} "
        f"reciprocal={totals['reciprocal_damage_games']}({ratio(totals['reciprocal_damage_games'], games):.1f}%) "
        f"multiDamageTicks={totals['multi_damage_tick_games']}({ratio(totals['multi_damage_tick_games'], games):.1f}%) "
        f"shots={totals['shots']} hitPerShot={ratio(totals['damage'], totals['shots']):.1f}%"
    )
    print(
        "  motion "
        f"activeWorldTicks={ratio(totals['active_world_ticks'], totals['ticks']):.1f}% "
        f"stagnantTicks={ratio(totals['stagnant_ticks'], totals['ticks']):.1f}% "
        f"recentRepeatTicks={ratio(totals['recent_repeat_ticks'], totals['repeat_opportunities']):.1f}% "
        f"stalledGames={totals['stalled_games']}({ratio(totals['stalled_games'], games):.1f}%) "
        f"loopedGames={totals['looped_games']}({ratio(totals['looped_games'], games):.1f}%) "
        f"movesPer100Ticks={ratio(totals['moves'], totals['ticks']):.1f} "
        f"turnsPer100Ticks={ratio(totals['turns'], totals['ticks']):.1f}"
    )
    print(
        "  decisions "
        f"wait={ratio(totals['wait_decisions'], decisions):.1f}% "
        f"switches={ratio(totals['action_switches'], totals['switch_opportunities']):.1f}% "
        f"longRuns={ratio(totals['long_repeat_decisions'], decisions):.1f}% "
        f"medianActionEntropy={statistics.median(entropies) if entropies else 0:.3f} "
        f"medianMaxRun={statistics.median(max_runs) if max_runs else 0:g}"
    )
    print(
        "  territory "
        f"contestedTicks={totals['contested_zone_ticks']} "
        f"contestToSole={totals['contest_to_sole']} "
        f"damageEvictions={totals['damage_evictions']}"
    )
    top_winner = winners.most_common(1)
    top_text = (
        f"{top_winner[0][0]}:{top_winner[0][1]}({ratio(top_winner[0][1], decided):.1f}%)"
        if top_winner else "none"
    )
    print(
        f"  diversity winners={len(winners)} top={top_text} "
        f"reasons={dict(sorted(reasons.items()))}"
    )
    print(
        "  records "
        + " ".join(
            f"{name}={record['Win']}-{record['Loss']}-{record['Draw']}"
            for name, record in sorted(
                records.items(),
                key=lambda item: (-item[1]["Win"], item[1]["Loss"], item[0]),
            )
        )
    )
    print(f"  rules={dict(sorted(rules.items()))}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--group",
        action="append",
        required=True,
        metavar="LABEL=PATH",
        help="replay file/directory; repeat a label to merge tournament blocks",
    )
    args = parser.parse_args()
    groups = collections.OrderedDict()
    for value in args.group:
        if "=" not in value:
            parser.error(f"--group expects LABEL=PATH, got {value!r}")
        label, path = value.split("=", 1)
        if not label or not path:
            parser.error(f"--group expects non-empty LABEL=PATH, got {value!r}")
        groups.setdefault(label, []).append(path)
    for label, paths in groups.items():
        print_group(label, paths)


if __name__ == "__main__":
    main()
