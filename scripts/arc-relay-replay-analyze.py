#!/usr/bin/env python3
"""Print an iteration-friendly summary of one Arc Relay replay.

Accepts a canonical replay (.json or .json.gz) from any runtime lane, or a
sweep cell directory containing match-record.json / scorecard.json. This is an
ANALYSIS tool for the discovery loop; it makes no evidence claim and does not
verify hashes — the sweep harness and `nilbots verify` own that.

Usage:
  python3 scripts/arc-relay-replay-analyze.py <replay.json[.gz] | cell-dir>
"""

import gzip
import json
import pathlib
import sys


def load(path: pathlib.Path):
    if path.is_dir():
        for name in ("replay.json.gz", "replay.json"):
            candidate = next(iter(sorted(path.rglob(name))), None)
            if candidate:
                return load(candidate), path
        return None, path
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt") as handle:
        return json.load(handle), path.parent


def summarize_scorecard(cell_dir: pathlib.Path):
    card = next(iter(sorted(cell_dir.rglob("scorecard.json"))), None)
    if not card:
        return
    data = json.loads(card.read_text())
    scoring = data.get("scoring", {})
    outcome = data.get("outcome", {})
    felt = data.get("feltDegeneracy", {})
    print("scorecard:")
    print(f"  deliveriesByTeam: {scoring.get('deliveriesByTeam')}")
    print(f"  completion: {outcome.get('completionReason')}"
          f" endTick={outcome.get('endTick')}")
    trips = {k: v for k, v in felt.items()
             if isinstance(v, dict) and v.get("tripped")}
    print(f"  felt-degeneracy trips: {sorted(trips) or 'none'}")


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    replay, cell_dir = load(pathlib.Path(sys.argv[1]))
    if replay is None:
        summarize_scorecard(cell_dir)
        return 0

    result = replay.get("result", {})
    standings = result.get("standings") or {}
    winner = result.get("winnerTeamId", standings.get("winnerTeamId"))
    print(f"result: {result.get('completionReason')}"
          f" winnerTeamId={winner}"
          f" endTick={result.get('endTick')}")

    kinds = {}
    timeline = []
    for tick in replay.get("ticks", []):
        for event in tick.get("events", []) or []:
            kind = event.get("kind", "?")
            kinds[kind] = kinds.get(kind, 0) + 1
            if kind == "score-changed":
                team = (event.get("payload") or {}).get("teamId")
                timeline.append((tick.get("tick"), team))
    print(f"event kinds: {dict(sorted(kinds.items(), key=lambda e: -e[1]))}")
    print("score timeline (tick, teamId): "
          f"{timeline if len(timeline) <= 24 else timeline[:24]}")

    turns = [t for tick in replay.get("ticks", [])
             for t in tick.get("mindTurns", []) or []]
    faults = [t for t in turns if t.get("runtimeFault")]
    print(f"mindTurns: {len(turns)} | runtime faults: {len(faults)}")
    if cell_dir:
        summarize_scorecard(cell_dir)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
