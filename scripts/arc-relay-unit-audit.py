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


def gun_range(replay: dict, form_id: str) -> int:
    rules = replay["header"]["contract"]["rules"]
    form = next(
        (f for f in rules["forms"] if f["id"] == form_id), None)
    if not form or not form.get("attackProfileId"):
        return 0
    attack = next(
        (a for a in rules["attackProfiles"]
         if a["id"] == form["attackProfileId"]), None)
    return attack["projectile"]["maxTravelTiles"] if attack else 0


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
                "edges": collections.Counter(),
                "halves": collections.Counter(),
                "form": None,
                "loneContacts": 0,
                "loneStruck": 0,
                "contactActive": False,
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
                if (command["actionId"].startswith("shoot")
                        and entry.get("contactActive")
                        and not entry.get("struckThisContact")):
                    entry["loneStruck"] += 1
                    entry["struckThisContact"] = True
                reason = (command.get("debugMessage") or "")[:64]
                if reason:
                    entry["reasons"][reason] += 1
        state = tick.get("tickStart", {}).get("state", {})
        lives = state.get("activeLives", []) or []
        width = replay["header"]["contract"]["map"]["width"]
        mid = width // 2
        for life in lives:
            actor = life["actorId"]
            if team_filter is not None and actor["teamId"] != team_filter:
                continue
            entry = unit(actor["teamId"], actor["unitId"])
            entry["form"] = life.get("formId") or entry["form"]
            spot = (life["position"]["x"], life["position"]["y"])
            if entry["positions"]:
                prev = entry["positions"][-1][1]
                if prev != spot:
                    entry["edges"][
                        (min(prev, spot), max(prev, spot),
                         prev < spot)
                    ] += 1
            # Territory: which half of the map (canonical: team 0 owns
            # low-x, team 1 owns high-x).
            own_low = actor["teamId"] == 0
            if spot[0] == mid:
                half = "mid"
            elif (spot[0] < mid) == own_low:
                half = "own"
            else:
                half = "enemy"
            entry["halves"][half] += 1
            entry["dwell"][spot] += 1
            entry["positions"].append((tick["tick"], spot))
            # Opportunity ledger: a LONE enemy inside this unit's gun
            # range (no other enemy within 4 of it) is a canonical kill
            # chance; count distinct contact episodes and how many drew a
            # strike while in contact.
            reach = gun_range(replay, entry["form"] or "")
            if reach > 0:
                enemies = [
                    (l["position"]["x"], l["position"]["y"])
                    for l in lives
                    if l["actorId"]["teamId"] != actor["teamId"]
                ]
                lone_in_reach = any(
                    max(abs(e[0] - spot[0]), abs(e[1] - spot[1])) <= reach
                    and not any(
                        o != e
                        and max(abs(o[0] - e[0]), abs(o[1] - e[1])) <= 4
                        for o in enemies
                    )
                    for e in enemies
                )
                if lone_in_reach and not entry["contactActive"]:
                    entry["loneContacts"] += 1
                    entry["contactActive"] = True
                    entry["struckThisContact"] = False
                elif not lone_in_reach:
                    entry["contactActive"] = False

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
        total_edges = sum(entry["edges"].values())
        # Pendulum score: how much of the walking re-traverses the same
        # tile edges in BOTH directions. A circuit walks each edge one
        # way (score near 0); a commuter walks its corridor out and back
        # (score near 1). This is the owner's "walks back and forth"
        # made measurable.
        both_ways = collections.Counter()
        for (a, b, forward), count in entry["edges"].items():
            both_ways[(a, b)] = both_ways.get((a, b), 0)
        pendulum = 0.0
        if total_edges:
            paired = 0
            undirected = collections.defaultdict(lambda: [0, 0])
            for (a, b, forward), count in entry["edges"].items():
                undirected[(a, b)][0 if forward else 1] += count
            for fwd, rev in undirected.values():
                paired += 2 * min(fwd, rev)
            pendulum = paired / total_edges
        halves_total = sum(entry["halves"].values()) or 1
        report[key] = {
            "pendulum": round(pendulum, 2),
            "territory": {
                half: round(entry["halves"][half] / halves_total, 2)
                for half in ("own", "mid", "enemy")
            },
            "loneContacts": entry["loneContacts"],
            "loneStruck": entry["loneStruck"],
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
            terr = entry["territory"]
            opp = (
                f" lone {entry['loneStruck']}/{entry['loneContacts']}"
                if entry["loneContacts"]
                else ""
            )
            print(
                f"  t{team} u{unit_id} [{roles}] "
                f"confined {entry['confinementTicks']:3d}t"
                f" @ {entry['confinementAnchor']}"
                f" | moves {moves:3d} shots {shots:3d}"
                f" | pend {entry['pendulum']:.2f}"
                f" own/mid/enemy {terr['own']:.2f}/{terr['mid']:.2f}"
                f"/{terr['enemy']:.2f}{opp}{flag}"
            )
            if args.verbose or flag:
                for reason, count in entry["topReasons"]:
                    print(f"        {count:4d}  {reason}")


if __name__ == "__main__":
    main()
