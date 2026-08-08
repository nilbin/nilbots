#!/usr/bin/env python3
"""Summarize inclusive sampled time from an evented Speedscope trace."""

from __future__ import annotations

import argparse
from collections import defaultdict
import json
from pathlib import Path
from typing import Any


def summarize(document: dict[str, Any]) -> tuple[dict[str, float], float, str]:
    frames = document["shared"]["frames"]
    totals: dict[str, float] = defaultdict(float)
    sampled = 0.0
    unit = "milliseconds"
    for profile in document["profiles"]:
        if profile.get("type") != "evented":
            continue
        unit = profile.get("unit", unit)
        stack: list[int] = []
        previous = float(profile.get("startValue", 0.0))
        for event in profile.get("events", []):
            current = float(event["at"])
            elapsed = current - previous
            if elapsed < 0:
                raise ValueError("Speedscope events are not monotonic")
            if stack and elapsed:
                sampled += elapsed
                for frame in stack:
                    totals[frames[frame]["name"]] += elapsed
            frame = int(event["frame"])
            if event["type"] == "O":
                stack.append(frame)
            elif event["type"] == "C":
                if not stack or stack[-1] != frame:
                    raise ValueError("Speedscope stack is unbalanced")
                stack.pop()
            else:
                raise ValueError(f"Unknown Speedscope event {event['type']}")
            previous = current
        if stack:
            raise ValueError("Speedscope profile ended with open frames")
    return totals, sampled, unit


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("trace", type=Path)
    parser.add_argument("--top", type=int, default=20)
    parser.add_argument("--contains", action="append", default=[])
    args = parser.parse_args()
    document = json.loads(args.trace.read_text(encoding="utf-8"))
    totals, sampled, unit = summarize(document)
    rows = sorted(totals.items(), key=lambda row: (-row[1], row[0]))
    if args.contains:
        rows = [
            row for row in rows
            if any(needle.casefold() in row[0].casefold()
                   for needle in args.contains)
        ]
    print(json.dumps({
        "schema": "speedscope-inclusive-summary-v1",
        "trace": str(args.trace.resolve()),
        "unit": unit,
        "sampledThreadTime": round(sampled, 6),
        "frames": [
            {
                "name": name,
                "inclusive": round(value, 6),
                "sampledThreadPercent": round(
                    100.0 * value / sampled if sampled else 0.0, 6),
            }
            for name, value in rows[:args.top]
        ],
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
