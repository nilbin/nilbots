#!/usr/bin/env python3
"""Opening-tempo read for Arc Relay raw replays.

Born from the owner observation 2026-08-08: "the other mind seems to pass
through to center faster consistently so maybe it's a sheet issue". Both
participants run the SAME mind, so a consistent tempo gap is caused by the
SHEET (route geometry, task triggers, pace), not by the mind.

This answers "who reaches the middle first, and by how much" with numbers
instead of impressions. Per team it reports:

  cross1..cross4   tick at which the 1st/2nd/3rd/4th DISTINCT body first
                   reaches the mid column (the well line) or beyond
  own/mid/enemy    mean share of live-body-ticks in each territory band
                   over the opening window (default 100 ticks)
  depth            mean team-relative advance in tiles (0 = home edge,
                   mid column = width//2); higher is further forward
  core             tick of the team's first core pickup ("secured") and
                   first bank

Everything is measured team-relative, so w/e cells of a 180-symmetric map
are directly comparable and no side normalization is needed afterwards.

Usage:
  arc-relay-tempo-read.py REPLAY_OR_RUNDIR [more...] [--ticks 100]
                          [--orders] [--json]

Reads RAW replays (activeLives state + mindTurns command stream), not
broadcast projections. Labels each team from the sibling run.json sheet
path when one is present.
"""

from __future__ import annotations

import argparse
import collections
import gzip
import json
import statistics
from pathlib import Path


def resolve(path: Path) -> Path:
    if path.is_dir():
        return path / "replay.json.gz"
    return path


def read_json(path: Path) -> dict:
    if path.suffix == ".gz":
        with gzip.open(path, "rt", encoding="utf-8") as handle:
            return json.load(handle)
    return json.loads(path.read_text(encoding="utf-8"))


def sheet_labels(replay_path: Path) -> dict[int, str]:
    run = replay_path.parent / "run.json"
    if not run.exists():
        return {}
    try:
        document = json.loads(run.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}
    labels = {}
    for participant in document.get("Participants", []) or []:
        sheet = participant.get("SheetPath") or ""
        if sheet:
            labels[participant["TeamId"]] = Path(sheet).stem
    return labels


def crossed(team: int, x: int, mid: int) -> bool:
    """True once a body stands on the mid column or past it."""
    return x >= mid if team == 0 else x <= mid


def band(team: int, x: int, mid: int) -> str:
    if x == mid:
        return "mid"
    own_low = team == 0
    return "own" if (x < mid) == own_low else "enemy"


def depth(team: int, x: int, width: int) -> int:
    """Team-relative advance in tiles from that team's own board edge."""
    return x if team == 0 else (width - 1 - x)


def measure(replay: dict, window: int, want_orders: bool) -> dict:
    width = replay["header"]["contract"]["map"]["width"]
    mid = width // 2
    teams: dict[int, dict] = collections.defaultdict(
        lambda: {
            "crossings": [],
            "crossed_units": set(),
            "bands": collections.Counter(),
            "depths": [],
            "beyond_per_tick": [],
            "core_secured": None,
            "core_banked": None,
            "orders": collections.Counter(),
        }
    )

    for tick in replay["ticks"]:
        number = tick["tick"]
        lives = (
            tick.get("tickStart", {}).get("state", {}).get("activeLives", [])
            or []
        )
        beyond = collections.Counter()
        for life in lives:
            actor = life["actorId"]
            team = actor["teamId"]
            entry = teams[team]
            x = life["position"]["x"]
            if (
                crossed(team, x, mid)
                and actor["unitId"] not in entry["crossed_units"]
            ):
                entry["crossed_units"].add(actor["unitId"])
                entry["crossings"].append((number, actor["unitId"]))
            if number < window:
                entry["bands"][band(team, x, mid)] += 1
                entry["depths"].append(depth(team, x, width))
                if crossed(team, x, mid):
                    beyond[team] += 1
        if number < window:
            for team in teams:
                teams[team]["beyond_per_tick"].append(beyond.get(team, 0))

        for event in tick.get("events", []) or []:
            if event.get("kind") != "arc-relay":
                continue
            fact = event["payload"]["fact"]
            if fact["kind"] == "core-picked-up":
                team = fact["carrierActorId"]["teamId"]
                if teams[team]["core_secured"] is None:
                    teams[team]["core_secured"] = number
            elif fact["kind"] == "core-banked":
                team = fact["teamId"]
                if teams[team]["core_banked"] is None:
                    teams[team]["core_banked"] = number

        if want_orders and number < window:
            for turn in tick.get("mindTurns", []) or []:
                entry = teams[turn["teamId"]]
                for command in turn.get("commands", []) or []:
                    tag = command.get("roleTag") or "(none)"
                    entry["orders"][tag] += 1

    report = {}
    for team, entry in sorted(teams.items()):
        total = sum(entry["bands"].values()) or 1
        crossings = [tick for tick, _ in entry["crossings"]]
        report[team] = {
            "cross": crossings[:4] + [None] * max(0, 4 - len(crossings[:4])),
            "crossUnits": [unit for _, unit in entry["crossings"][:4]],
            "own": entry["bands"]["own"] / total,
            "mid": entry["bands"]["mid"] / total,
            "enemy": entry["bands"]["enemy"] / total,
            "depth": statistics.fmean(entry["depths"])
            if entry["depths"]
            else 0.0,
            "beyond": statistics.fmean(entry["beyond_per_tick"])
            if entry["beyond_per_tick"]
            else 0.0,
            "coreSecured": entry["core_secured"],
            "coreBanked": entry["core_banked"],
            "orders": entry["orders"].most_common(6),
        }
    return report


def cell_of(path: Path) -> str:
    return path.parent.name


def format_tick(value) -> str:
    return "--" if value is None else str(value)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Opening-tempo read for Arc Relay raw replays.")
    parser.add_argument("replays", nargs="+", type=Path)
    parser.add_argument("--ticks", type=int, default=100,
                        help="opening window for territory shares")
    parser.add_argument("--orders", action="store_true",
                        help="also print the top role tags in the window")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    rows = []
    for raw in args.replays:
        path = resolve(raw)
        replay = read_json(path)
        labels = sheet_labels(path)
        for team, entry in measure(replay, args.ticks, args.orders).items():
            rows.append({
                "cell": cell_of(path),
                "team": team,
                "sheet": labels.get(team, f"team-{team}"),
                **entry,
            })

    if args.json:
        print(json.dumps(rows, indent=1))
        return

    print(f"opening window: {args.ticks} ticks; "
          "cross = tick the Nth distinct body reaches the mid column")
    header = (f"{'cell':<22}{'sheet':<16}{'t':<3}"
              f"{'x1':>5}{'x2':>5}{'x3':>5}{'x4':>5}"
              f"{'own%':>7}{'mid%':>6}{'en%':>6}"
              f"{'depth':>7}{'past':>6}{'core':>6}{'bank':>6}")
    print(header)
    print("-" * len(header))
    for row in rows:
        crosses = "".join(f"{format_tick(v):>5}" for v in row["cross"])
        print(f"{row['cell']:<22}{row['sheet']:<16}{row['team']:<3}"
              f"{crosses}"
              f"{row['own'] * 100:>7.1f}{row['mid'] * 100:>6.1f}"
              f"{row['enemy'] * 100:>6.1f}"
              f"{row['depth']:>7.1f}{row['beyond']:>6.2f}"
              f"{format_tick(row['coreSecured']):>6}"
              f"{format_tick(row['coreBanked']):>6}")

    by_sheet: dict[str, list[dict]] = collections.defaultdict(list)
    for row in rows:
        by_sheet[row["sheet"]].append(row)
    if len(by_sheet) > 1 or len(rows) > 2:
        print()
        print("means by sheet (side-normalized: every metric is "
              "team-relative)")
        print(header.replace("cell", "sheet mean"))
        print("-" * len(header))
        for sheet, group in sorted(by_sheet.items()):
            def mean_of(key: str, index: int | None = None) -> float | None:
                values = [
                    (row[key][index] if index is not None else row[key])
                    for row in group
                    if (row[key][index] if index is not None else row[key])
                    is not None
                ]
                return statistics.fmean(values) if values else None

            crosses = "".join(
                f"{(f'{value:.0f}' if value is not None else '--'):>5}"
                for value in (mean_of("cross", i) for i in range(4)))
            core = mean_of("coreSecured")
            bank = mean_of("coreBanked")
            print(f"{sheet:<22}{'':<16}{'':<3}"
                  f"{crosses}"
                  f"{statistics.fmean(r['own'] for r in group) * 100:>7.1f}"
                  f"{statistics.fmean(r['mid'] for r in group) * 100:>6.1f}"
                  f"{statistics.fmean(r['enemy'] for r in group) * 100:>6.1f}"
                  f"{statistics.fmean(r['depth'] for r in group):>7.1f}"
                  f"{statistics.fmean(r['beyond'] for r in group):>6.2f}"
                  f"{(f'{core:.0f}' if core is not None else '--'):>6}"
                  f"{(f'{bank:.0f}' if bank is not None else '--'):>6}")

    if args.orders:
        print()
        for row in rows:
            tags = ", ".join(f"{tag} x{count}"
                             for tag, count in row["orders"])
            print(f"{row['cell']} team {row['team']} "
                  f"({row['sheet']}): {tags}")


if __name__ == "__main__":
    main()
