#!/usr/bin/env python3
"""Per-unit behavior audit for Arc Relay raw replays.

Born from the camping-ghost miss (owner catch 2026-08): a battery scored
23/24 bars-clean while the hunter's ghost stood beside the enemy reactor
for a hundred ticks, shuffling one tile every thirty so the parked bar
never fired. Records and bars say whether a match LOOKS degenerate;
this audit says what each unit actually DID: where it dwelt, how long it
stayed confined to one pocket, what it commanded and why.

For every unit of every team: role tags seen, top dwell tiles, longest
confinement inside a radius-2 pocket, action counts, and the most common
command reasons. Units whose confinement exceeds --flag-ticks (default
60) are marked CONFINED - those are the replays to open in the viewer.

Usage: arc-relay-unit-audit.py REPLAY.json.gz [more...] [--flag-ticks N]
       [--team N] [--verbose]

Reads RAW replays (mindTurns command stream), not broadcast projections.
"""

from __future__ import annotations

import argparse
import collections
import gzip
import json
from pathlib import Path


def read_replay(path: Path) -> dict:
    if path.suffix == ".gz":
        with gzip.open(path, "rt", encoding="utf-8") as handle:
            return json.load(handle)
    return json.loads(path.read_text(encoding="utf-8"))


def audit(replay: dict, team_filter: int | None) -> dict:
    units: dict[tuple[int, int], dict] = {}

    def unit(team: int, unit_id: int) -> dict:
        return units.setdefault(
            (team, unit_id),
            {
                "roles": collections.Counter(),
                "dwell": collections.Counter(),
                "actions": collections.Counter(),
                "reasons": collections.Counter(),
                "positions": [],
            },
        )

    for tick in replay["ticks"]:
        for turn in tick.get("mindTurns", []) or []:
            team = turn["teamId"]
            if team_filter is not None and team != team_filter:
                continue
            for command in turn.get("commands", []) or []:
                entry = unit(team, command["unitId"])
                if command.get("roleTag"):
                    entry["roles"][command["roleTag"]] += 1
                entry["actions"][command["actionId"]] += 1
                reason = (command.get("debugMessage") or "")[:64]
                if reason:
                    entry["reasons"][reason] += 1
        state = tick.get("tickStart", {}).get("state", {})
        for life in state.get("activeLives", []) or []:
            actor = life["actorId"]
            if team_filter is not None and actor["teamId"] != team_filter:
                continue
            entry = unit(actor["teamId"], actor["unitId"])
            spot = (life["position"]["x"], life["position"]["y"])
            entry["dwell"][spot] += 1
            entry["positions"].append((tick["tick"], spot))

    report = {}
    for key, entry in sorted(units.items()):
        best = 0
        anchor = None
        current = None
        start = 0
        for tick_number, spot in entry["positions"]:
            if current is None or max(
                abs(spot[0] - current[0]), abs(spot[1] - current[1])
            ) > 2:
                current = spot
                start = tick_number
            if tick_number - start > best:
                best = tick_number - start
                anchor = current
        report[key] = {
            "roles": [role for role, _ in entry["roles"].most_common(3)],
            "topDwell": entry["dwell"].most_common(3),
            "confinementTicks": best,
            "confinementAnchor": anchor,
            "actions": dict(entry["actions"]),
            "topReasons": entry["reasons"].most_common(5),
        }
    return report


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("replays", nargs="+", type=Path)
    parser.add_argument("--flag-ticks", type=int, default=60)
    parser.add_argument("--team", type=int, default=None)
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()

    for path in args.replays:
        replay = read_replay(path)
        report = audit(replay, args.team)
        end_tick = (replay.get("result") or {}).get("endTick", "?")
        print(f"== {path.name} (end {end_tick})")
        for (team, unit_id), entry in report.items():
            flag = (
                "  CONFINED"
                if entry["confinementTicks"] >= args.flag_ticks
                else ""
            )
            roles = ",".join(entry["roles"]) or "-"
            shots = sum(
                count
                for action, count in entry["actions"].items()
                if action.startswith("shoot")
            )
            moves = sum(
                count
                for action, count in entry["actions"].items()
                if "move" in action or "strafe" in action
            )
            print(
                f"  t{team} u{unit_id} [{roles}] "
                f"confined {entry['confinementTicks']:3d}t"
                f" @ {entry['confinementAnchor']}"
                f" | moves {moves:3d} shots {shots:3d}{flag}"
            )
            if args.verbose or flag:
                for reason, count in entry["topReasons"]:
                    print(f"        {count:4d}  {reason}")


if __name__ == "__main__":
    main()
