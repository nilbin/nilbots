#!/usr/bin/env python3
"""Freeze the registered 320-cell forward-combat sweep plan."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import subprocess
import sys


REPO = Path(__file__).resolve().parent.parent
REGISTRATION = REPO / "balance/arc-relay-forward-combat-v14.json"
COHORT = (
    REPO
    / "arena-bots/arc-relay/forward-combat-cohort-v14-2026-08-03/cohort.json"
)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    registration = json.loads(REGISTRATION.read_text(encoding="utf-8"))
    sampling = registration["sampling"]
    order = sampling["seededOrder"]
    pairs: set[tuple[str, str]] = set()
    for index, entrant in enumerate(order):
        for offset in sampling["offsets"]:
            opponent = order[(index + offset) % len(order)]
            pairs.add(tuple(sorted((entrant, opponent))))
    if len(pairs) != sampling["unorderedPairs"]:
        raise ValueError(
            f"registration expected {sampling['unorderedPairs']} pairs, "
            f"constructed {len(pairs)}"
        )
    command = [
        sys.executable,
        str(REPO / "scripts/arc-relay-sweep-plan.py"),
        "--cohort",
        str(COHORT),
        "--output",
        str(args.output),
        "--sweep-id",
        "arc-relay-forward-combat-strategy-mind-v14",
        "--profile",
        registration["candidate"]["loopProfile"],
        "--seeds",
        registration["candidate"]["seed"],
        "--runtime",
        registration["candidate"]["runtime"],
    ]
    for first, second in sorted(pairs):
        command.extend(["--pair", f"{first},{second}"])
    return subprocess.run(command, cwd=REPO, check=False).returncode


if __name__ == "__main__":
    raise SystemExit(main())
