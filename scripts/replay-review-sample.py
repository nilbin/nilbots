#!/usr/bin/env python3
"""Select a deterministic, outcome-blind replay-review sample.

Selection reads only replay headers. It balances map coverage first and unseen
bot pairings second, then uses a seeded SHA-256 order. The output deliberately
omits winner, reason, damage, and duration so reviewers can watch before seeing
the outcome table.
"""

import argparse
import collections
import hashlib
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


def candidate(path, selection_seed):
    document = json.loads(path.read_text())
    header = document["header"]
    replay_version = header.get("replayVersion")
    if replay_version == 1:
        map_id = header["mapId"]
        participant_key = "slot"
    elif replay_version == 2:
        map_id = header["contract"]["map"]["mapId"]
        participant_key = "participantId"
    else:
        raise ValueError(
            f"{path}: unsupported replay version {replay_version!r}"
        )
    participants = sorted(
        header["participants"],
        key=lambda item: item[participant_key],
    )
    names = [participant["name"] for participant in participants]
    artifacts = [participant["artifactHash"] for participant in participants]
    identity = "|".join(
        [
            str(selection_seed),
            str(replay_version),
            header["gameRulesVersion"],
            map_id,
            str(header["seed"]),
            *(
                f"{name}\0{artifact}"
                for name, artifact in zip(names, artifacts)
            ),
        ]
    )
    return {
        "source": str(path.resolve()),
        "replayVersion": replay_version,
        "rules": header["gameRulesVersion"],
        "map": map_id,
        "matchSeed": header["seed"],
        "participants": names,
        "pair": tuple(sorted(zip(names, artifacts))),
        "order": hashlib.sha256(identity.encode()).hexdigest(),
    }


def select(candidates, count):
    by_map = collections.defaultdict(list)
    for item in candidates:
        by_map[item["map"]].append(item)
    for items in by_map.values():
        items.sort(key=lambda item: (item["order"], item["source"]))

    selected = []
    seen_pairs = set()
    maps = sorted(by_map)
    while len(selected) < count and any(by_map.values()):
        made_progress = False
        for map_id in maps:
            items = by_map[map_id]
            if not items or len(selected) >= count:
                continue
            unseen_index = next(
                (index for index, item in enumerate(items) if item["pair"] not in seen_pairs),
                0,
            )
            item = items.pop(unseen_index)
            selected.append(item)
            seen_pairs.add(item["pair"])
            made_progress = True
        if not made_progress:
            break
    return selected


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("roots", nargs="+", help="replay files or directories")
    parser.add_argument("--count", type=int, default=12)
    parser.add_argument("--seed", type=int, default=20260724)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    if args.count <= 0:
        parser.error("--count must be positive")

    candidates = [
        candidate(path, args.seed)
        for path in replay_files(args.roots)
    ]
    chosen = select(candidates, min(args.count, len(candidates)))
    manifest = {
        "sampleVersion": 2,
        "selection": (
            "versioned header-only; map-balanced; unseen-pair-first; "
            "seeded SHA-256"
        ),
        "selectionSeed": args.seed,
        "outcomeBlind": True,
        "populationSize": len(candidates),
        "replays": [
            {
                "id": f"sample-{index + 1:02}",
                "source": item["source"],
                "replayVersion": item["replayVersion"],
                "rules": item["rules"],
                "map": item["map"],
                "matchSeed": item["matchSeed"],
                "participants": item["participants"],
            }
            for index, item in enumerate(chosen)
        ],
    }
    rendered = json.dumps(manifest, indent=2) + "\n"
    if args.output:
        args.output.write_text(rendered)
        print(f"Wrote {len(chosen)} of {len(candidates)} replays to {args.output}")
    else:
        print(rendered, end="")


if __name__ == "__main__":
    main()
