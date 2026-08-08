#!/usr/bin/env python3
"""Select a deterministic, outcome-blind replay-review sample.

Selection uses only replay-header fields. It balances map coverage first and
unseen bot pairings second, then uses a seeded SHA-256 order. The output
deliberately omits winner, reason, damage, and duration so reviewers can watch
before seeing the outcome table.
"""

import argparse
import collections
import gzip
import hashlib
import json
import pathlib


def replay_bytes(path):
    """Replay document bytes, decompressing a .gz transparently."""
    if path.suffix == ".gz":
        with gzip.open(path, "rb") as stream:
            return stream.read()
    return path.read_bytes()


def file_sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def replay_files(roots):
    # The experiment command writes replay.json.gz; other flows write
    # replay.json. Accept both, deduplicating on the uncompressed name so
    # a directory holding both counts once (the .json wins).
    seen = set()
    for root in roots:
        path = pathlib.Path(root)
        if path.is_file():
            candidates = [path]
        else:
            candidates = sorted(path.rglob("replay.json")) + sorted(
                path.rglob("replay.json.gz")
            )
        for candidate in candidates:
            resolved = candidate.resolve()
            key = (
                resolved.with_suffix("")
                if resolved.suffix == ".gz"
                else resolved
            )
            if key not in seen:
                seen.add(key)
                yield candidate


def candidate(path, selection_seed):
    document = json.loads(replay_bytes(path))
    header = document["header"]
    replay_version = header.get("replayVersion")
    if replay_version == 1:
        map_id = header["mapId"]
        participant_key = "slot"
    elif replay_version == 2:
        map_id = header["contract"]["map"]["mapId"]
        participant_key = "participantId"
    elif replay_version == 3:
        map_id = header["contract"]["map"]["mapId"]
        participant_key = "participantId"
    else:
        raise ValueError(
            f"{path}: unsupported replay version {replay_version!r}"
        )
    provenance = (
        header["provenance"]["participants"]
        if replay_version == 3
        else header["participants"]
    )
    participants = sorted(
        provenance,
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


def presentation_labels(item, blind):
    if not blind:
        return list(item["participants"])
    return [
        f"Entrant {chr(ord('A') + index)}"
        for index in range(len(item["participants"]))
    ]


def write_review_package(destination, chosen, blind):
    destination.mkdir(parents=True, exist_ok=False)
    replay_directory = destination / "replays"
    replay_directory.mkdir()
    index = []
    for sample_index, item in enumerate(chosen, start=1):
        sample_id = f"sample-{sample_index:02}"
        copied_replay = replay_directory / f"{sample_id}.json"
        source = pathlib.Path(item["source"])
        source_bytes = replay_bytes(source)
        source_hash = hashlib.sha256(source_bytes).hexdigest()
        copied_replay.write_bytes(source_bytes)
        copied_hash = file_sha256(copied_replay)
        if copied_hash != source_hash:
            raise ValueError(f"{source}: copied replay bytes changed")
        labels = presentation_labels(item, blind)
        item["reviewSource"] = f"replays/{sample_id}.json"
        item["presentationLabels"] = labels
        index.append(
            {
                "id": sample_id,
                "url": f"replays/{sample_id}.json",
                "map": item["map"],
                "bots": labels,
                # Outcome-blind packages intentionally do not inspect duration
                # or termination reason. The existing picker renders "?t".
                "ticks": "?",
                "reason": None,
                "identityAliases": [
                    {
                        "participantIndex": participant_index,
                        "label": label,
                    }
                    for participant_index, label in enumerate(labels)
                ],
            }
        )
    (destination / "replays.json").write_text(
        json.dumps(index, indent=2) + "\n",
        encoding="utf-8",
    )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("roots", nargs="+", help="replay files or directories")
    parser.add_argument("--count", type=int, default=12)
    parser.add_argument("--seed", type=int, default=20260724)
    parser.add_argument(
        "--blind-identities",
        action="store_true",
        help="Replace participant names in the reviewer manifest.",
    )
    parser.add_argument(
        "--copy-selected",
        type=pathlib.Path,
        help=(
            "Write a hosted-review package with neutral replay paths and "
            "replays.json; required with --blind-identities."
        ),
    )
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    if args.count <= 0:
        parser.error("--count must be positive")
    if args.blind_identities and args.copy_selected is None:
        parser.error(
            "--blind-identities requires --copy-selected to hide source paths"
        )

    candidates = [
        candidate(path, args.seed)
        for path in replay_files(args.roots)
    ]
    chosen = select(candidates, min(args.count, len(candidates)))
    if args.copy_selected is not None:
        write_review_package(
            args.copy_selected,
            chosen,
            args.blind_identities,
        )
    manifest = {
        "sampleVersion": 2,
        "selection": (
            "versioned header-fields-only; map-balanced; unseen-pair-first; "
            "seeded SHA-256"
        ),
        "selectionSeed": args.seed,
        "outcomeBlind": True,
        "identitiesBlind": args.blind_identities,
        "populationSize": len(candidates),
        "replays": [
            {
                "id": f"sample-{index + 1:02}",
                "source": item.get("reviewSource", item["source"]),
                "replayVersion": item["replayVersion"],
                "rules": item["rules"],
                "map": item["map"],
                "matchSeed": item["matchSeed"],
                "participants": item.get(
                    "presentationLabels",
                    presentation_labels(item, args.blind_identities),
                ),
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
