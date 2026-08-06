#!/usr/bin/env python3
"""Per-unit doctrine-adherence report for Arc Relay tactical-playbook cells.

The felt-degeneracy bars answer "is this match watchable"; this answers the
iteration question underneath: did each unit spend its time doing what the
sheet intended, and where did the time go instead? It reads the CANONICAL
replay (run directories keep one beside run.json; broadcast projections drop
the debug text this needs) and cross-references three per-tick streams the
mind already emits:

- task leases per unit, parsed from the debug line's ``tasks=...`` trace
  (``c=<unit>:<assignment>`` claim lists);
- per-unit positions, from the team observation bodies;
- Arc facts (banks, drops, pickups, destructions, well births).

The headline instrument is PARKED-WHILE-TASKED: a unit that holds a task
lease yet stays within a small radius for a long stretch. Owner replay
review keeps finding exactly this shape by eye (the parked ghost at top
centre, the statue livelock, the spawn dancers); this makes the same
observation mechanically, per unit, with the task that held the unit named.

Usage:
  python3 scripts/arc-relay-doctrine-report.py RUN_DIR [RUN_DIR ...]
      [--team 0|1|both] [--dwell-radius 2] [--dwell-ticks 40] [--json OUT]

Each RUN_DIR is an ``experiment arc-relay`` output directory containing
replay.json.gz and run.json. Reports go to stdout; --json adds a combined
machine-readable dump for downstream tooling.
"""

import argparse
import gzip
import json
import re
import sys
from collections import Counter, defaultdict

TASK_TRACE = re.compile(r"tasks=([^;]*(?:;[^;=]+=[^;]*)*)")
TASK_ENTRY = re.compile(
    r"(?P<task>[a-z0-9-]+)=(?P<phase>\w+)\[(?P<reason>[^|\]]*)"
    r"\|armed=\d\|c=(?P<claims>[^\]]*)\]")


def load_cell(run_dir):
    with open(f"{run_dir}/run.json", encoding="utf-8") as f:
        run = json.load(f)
    with gzip.open(f"{run_dir}/replay.json.gz") as f:
        replay = json.load(f)
    return run, replay


def team_sheets(run):
    names = {}
    for participant in run.get("Participants", []):
        team = participant["TeamId"]
        path = participant.get("SheetPath")
        names[team] = (path.rsplit("/", 1)[-1].removesuffix(".json")
                       if path else participant.get("Name", f"team{team}"))
    return names


def parse_tick(tick, teams):
    """Yield (team, unit->position, unit->task, task->(phase, reason))."""
    for turn in tick.get("mindTurns", []):
        team = turn["teamId"]
        if team not in teams:
            continue
        positions = {}
        for body in turn.get("observation", {}).get("bodies", []):
            actor = body["actorId"]
            if actor["teamId"] == team:
                positions[actor["unitId"]] = (
                    body["position"]["x"], body["position"]["y"])
        leases = {}
        states = {}
        debug = None
        for body in turn.get("observation", {}).get("bodies", []):
            message = body.get("debugMessage")
            if message and "tasks=" in message:
                debug = message
                break
        if debug is None:
            debug = json.dumps(turn)
        for entry in TASK_ENTRY.finditer(debug):
            task = entry.group("task")
            states[task] = (entry.group("phase"), entry.group("reason"))
            claims = entry.group("claims")
            if claims and claims != "-":
                for claim in claims.split(","):
                    unit, _, assignment = claim.partition(":")
                    try:
                        leases[int(unit)] = (task, assignment)
                    except ValueError:
                        pass
        yield team, positions, leases, states


def arc_facts(tick):
    for event in tick.get("events", []):
        fact = event.get("payload", {}).get("fact")
        if fact:
            yield fact
        elif event.get("kind") == "destruction":
            yield {"kind": "destruction", **event["payload"]}


def fact_team_unit(fact):
    actor = (fact.get("carrierActorId") or fact.get("actorId")
             or fact.get("sourceActorId") or {})
    return actor.get("teamId", fact.get("teamId")), actor.get("unitId")


def analyse(run, replay, teams, dwell_radius, dwell_ticks):
    sheets = team_sheets(run)
    report = {}
    for team in teams:
        report[team] = {
            "sheet": sheets.get(team, f"team{team}"),
            "units": defaultdict(lambda: {
                "leaseTicks": Counter(),
                "unleasedTicks": 0,
                "distinctTiles": set(),
                "kills": 0,
                "deaths": 0,
                "banks": 0,
                "pickups": 0,
                "dropsForced": 0,
                "parked": [],
            }),
            "banksTimeline": [],
            "taskReasons": defaultdict(Counter),
        }

    # Dwell is keyed by POSITION ONLY. Task-lease churn must not reset the
    # streak: a unit that stands still while tasks cycle over it (trigger,
    # fail, re-trigger) is still parked — that churn is how the first version
    # of this detector was blinded to a camping ghost the owner then caught
    # on replay. The tasks held during the streak are reported as a set.
    dwell = {}  # (team, unit) -> [startTick, anchor, lastTick, set(tasks)]
    carriers = {}  # (team, unit) -> carrying since pickup

    def close_dwell(key, tick):
        state = dwell.pop(key, None)
        if state is None:
            return
        start, anchor, last, tasks = state
        if last - start >= dwell_ticks:
            team, unit = key
            report[team]["units"][unit]["parked"].append({
                "from": start, "to": last, "around": anchor,
                "task": "+".join(sorted(tasks)) if tasks else "unleased"})

    for tick in replay["ticks"]:
        now = tick["tick"]
        for team, positions, leases, states in parse_tick(tick, teams):
            for task, (phase, reason) in states.items():
                report[team]["taskReasons"][task][f"{phase}:{reason}"] += 1
            for unit, position in positions.items():
                entry = report[team]["units"][unit]
                entry["distinctTiles"].add(position)
                lease = leases.get(unit)
                if lease:
                    entry["leaseTicks"][lease[0]] += 1
                else:
                    entry["unleasedTicks"] += 1
                key = (team, unit)
                state = dwell.get(key)
                task_name = lease[0] if lease else None
                if state is None:
                    dwell[key] = [
                        now, position, now,
                        {task_name} if task_name else set()]
                else:
                    anchor = state[1]
                    if (max(abs(position[0] - anchor[0]),
                            abs(position[1] - anchor[1])) > dwell_radius):
                        close_dwell(key, now)
                        dwell[key] = [
                            now, position, now,
                            {task_name} if task_name else set()]
                    else:
                        state[2] = now
                        if task_name:
                            state[3].add(task_name)
        for fact in arc_facts(tick):
            kind = fact.get("kind")
            team, unit = fact_team_unit(fact)
            if team not in report:
                if kind == "destruction":
                    source = fact.get("sourceActorId") or {}
                    src_team = source.get("teamId")
                    if src_team in report:
                        report[src_team]["units"][source["unitId"]]["kills"] += 1
                        victim = fact.get("actorId", {})
                        victim_key = (victim.get("teamId"), victim.get("unitId"))
                        if victim_key in carriers:
                            report[src_team]["units"][
                                source["unitId"]]["dropsForced"] += 1
                continue
            entry = report[team]["units"].get(unit)
            if kind == "core-banked":
                if entry:
                    entry["banks"] += 1
                report[team]["banksTimeline"].append(now)
                carriers.pop((team, unit), None)
            elif kind == "core-picked-up":
                if entry:
                    entry["pickups"] += 1
                carriers[(team, unit)] = now
            elif kind == "core-dropped":
                carriers.pop((team, unit), None)
            elif kind == "destruction":
                if entry:
                    entry["deaths"] += 1
                carriers.pop((team, unit), None)
                source = fact.get("sourceActorId") or {}
                src_team = source.get("teamId")
                if src_team in report and src_team != team:
                    report[src_team]["units"][source["unitId"]]["kills"] += 1

    for key in list(dwell):
        close_dwell(key, dwell[key][2])
    return report


def print_report(cell, run, report):
    result = run.get("Result", {})
    print(f"=== {cell}: {result.get('Reason', '?')} "
          f"t{result.get('EndTick', '?')}, winner team "
          f"{result.get('WinnerTeamId', '?')}")
    for team, data in sorted(report.items()):
        banks = data["banksTimeline"]
        print(f"-- team {team} ({data['sheet']}): {len(banks)} banks"
              + (f" (last t{banks[-1]})" if banks else ""))
        for unit in sorted(data["units"]):
            entry = data["units"][unit]
            leases = ", ".join(
                f"{task}:{ticks}" for task, ticks
                in entry["leaseTicks"].most_common())
            print(f"   unit {unit}: tiles={len(entry['distinctTiles'])}"
                  f" kills={entry['kills']} deaths={entry['deaths']}"
                  f" banks={entry['banks']} pickups={entry['pickups']}"
                  f" dropsForced={entry['dropsForced']}"
                  f" unleased={entry['unleasedTicks']}"
                  + (f" | leases {leases}" if leases else ""))
        anomalies = [
            (unit, parked)
            for unit in sorted(data["units"])
            for parked in data["units"][unit]["parked"]]
        for unit, parked in anomalies:
            print(f"   PARKED unit {unit}: t{parked['from']}-{parked['to']}"
                  f" ({parked['to'] - parked['from']} ticks) around"
                  f" {parked['around']} while {parked['task']}")
        if not anomalies:
            print("   no parked-while-tasked stretches")


def to_json(report):
    out = {}
    for team, data in report.items():
        out[str(team)] = {
            "sheet": data["sheet"],
            "banksTimeline": data["banksTimeline"],
            "units": {
                str(unit): {
                    "leaseTicks": dict(entry["leaseTicks"]),
                    "unleasedTicks": entry["unleasedTicks"],
                    "distinctTiles": len(entry["distinctTiles"]),
                    "kills": entry["kills"],
                    "deaths": entry["deaths"],
                    "banks": entry["banks"],
                    "pickups": entry["pickups"],
                    "dropsForced": entry["dropsForced"],
                    "parked": entry["parked"],
                }
                for unit, entry in data["units"].items()
            },
            "taskReasons": {
                task: dict(counter)
                for task, counter in data["taskReasons"].items()
            },
        }
    return out


def main():
    parser = argparse.ArgumentParser(
        description=__doc__.splitlines()[0])
    parser.add_argument("cells", nargs="+", help="experiment run directories")
    parser.add_argument("--team", default="both", choices=["0", "1", "both"])
    parser.add_argument("--dwell-radius", type=int, default=2)
    parser.add_argument("--dwell-ticks", type=int, default=40)
    parser.add_argument("--json", help="write combined JSON report here")
    args = parser.parse_args()

    teams = [0, 1] if args.team == "both" else [int(args.team)]
    combined = {}
    for cell in args.cells:
        try:
            run, replay = load_cell(cell)
        except OSError as error:
            print(f"=== {cell}: unreadable ({error})", file=sys.stderr)
            continue
        report = analyse(
            run, replay, teams, args.dwell_radius, args.dwell_ticks)
        print_report(cell, run, report)
        combined[cell] = to_json(report)
    if args.json:
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump(combined, f, indent=1)
        print(f"json report: {args.json}")


if __name__ == "__main__":
    main()
