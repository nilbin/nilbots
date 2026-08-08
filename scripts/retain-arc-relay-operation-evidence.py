#!/usr/bin/env python3
"""Retain a compact, reproducible ten-operation confirmation and gallery input."""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
from typing import Any


REPO = Path(__file__).resolve().parent.parent
RECORD_LIMIT = 4 * 1024
BROADCAST_LIMIT = 300 * 1024
GALLERY_LIMIT = 8 * 1024 * 1024
TEAM_LABELS = ("Blue", "Orange")


def read_json(path: Path) -> dict[str, Any]:
    with path.open("rb") as source:
        compressed = source.read(2) == b"\x1f\x8b"
    opener = gzip.open if compressed else open
    with opener(path, "rt", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: expected object")
    return value


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, value: Any, *, compact: bool = False) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    encoded = (
        json.dumps(value, separators=(",", ":"), ensure_ascii=False)
        if compact
        else json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False)
    )
    path.write_text(encoded + "\n", encoding="utf-8")


def title(value: str) -> str:
    return " ".join(part.capitalize() for part in value.split("-"))


def qualifying(
    cell: dict[str, Any], key: str, *, prefer_committed: bool = False
) -> dict[str, Any]:
    rows = [
        activation for activation in cell["activations"]
        if activation["qualifies"].get(key) is True
    ]
    if not rows:
        raise ValueError(f"{cell['cellId']}: no qualifying {key} activation")
    rows.sort(key=lambda value: (
        0 if prefer_committed
        and value["qualifies"].get("committedCounter") else 1,
        value["terminalTick"] or 1_000_000,
        value["prepareTick"],
    ))
    return rows[0]


def all_three(cell: dict[str, Any]) -> bool:
    return all(
        any(activation["qualifies"].get(key) for activation in cell["activations"])
        for key in ("success", "counter", "casualtyRecovery")
    )


def selected_cells(read: dict[str, Any]) -> list[dict[str, Any]]:
    selected: list[dict[str, Any]] = []
    for operation in read["operations"]:
        candidates = [
            cell for cell in read["cells"]
            if cell["operationId"] == operation["id"] and all_three(cell)
        ]
        if not candidates:
            raise ValueError(f"{operation['id']}: no single all-three review cell")
        candidates.sort(key=lambda cell: (
            0 if any(
                activation["qualifies"].get("committedCounter")
                for activation in cell["activations"]
            ) else 1,
            cell["strandedActivations"],
            cell["matchEndPreemptedReleases"],
            cell["cellId"],
        ))
        selected.append(candidates[0])
    return selected


def activation_evidence(activation: dict[str, Any]) -> dict[str, Any]:
    return {
        key: activation[key]
        for key in (
            "prepareTick",
            "commitTick",
            "branch",
            "terminalTick",
            "terminalReason",
            "releaseTick",
            "recoveryTicks",
            "recoveryDeadlineTicks",
            "participants",
            "participantLives",
            "requiredActionTicks",
            "hostileImpacts",
            "casualties",
            "postReleaseCommands",
            "boundedRelease",
            "baselineRelease",
            "casualtyRespawnBaseline",
            "qualifies",
        )
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--read", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    read = read_json(args.read)
    manifest = read_json(args.manifest)
    if not read.get("allRequirementsMet"):
        raise ValueError("refusing retention: not all operation requirements passed")
    if read.get("eligibleCells") != read.get("readCells"):
        raise ValueError("refusing retention: confirmation contains ineligible cells")
    if sha256(args.manifest) != read["manifestSha256"]:
        raise ValueError("analysis and manifest hashes disagree")
    taxonomy = REPO / manifest["counterTaxonomy"]
    if sha256(taxonomy) != manifest["counterTaxonomySha256"]:
        raise ValueError("counter taxonomy hash moved")

    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    sweep_attempt = Path(read["sweepAttempt"])
    registered = {cell["cellId"]: cell for cell in manifest["cells"]}
    retained: list[dict[str, Any]] = []
    sample_entries: list[dict[str, Any]] = []
    broadcast_total = 0

    for cell in selected_cells(read):
        cell_id = cell["cellId"]
        source_attempts = sorted(
            (sweep_attempt / "cells" / cell_id).glob("attempt-*")
        )
        source = next(
            value for value in reversed(source_attempts)
            if (value / "cell-result.json").is_file()
        )
        result = read_json(source / "cell-result.json")
        record = read_json(source / "match-record.json")
        operation_id = cell["operationId"]
        destination = output / operation_id
        destination.mkdir(exist_ok=True)

        broadcast_source = source / "broadcast.json.gz"
        scorecard_source = source / "scorecard.json"
        broadcast_target = destination / "broadcast.json.gz"
        scorecard_target = destination / "scorecard.json"
        shutil.copyfile(broadcast_source, broadcast_target)
        shutil.copyfile(scorecard_source, scorecard_target)
        if sha256(broadcast_target) != result["broadcastFileSha256"]:
            raise ValueError(f"{cell_id}: copied broadcast bytes moved")
        if sha256(scorecard_target) != result["scorecardFileSha256"]:
            raise ValueError(f"{cell_id}: copied scorecard bytes moved")

        registration = registered[cell_id]
        for index, entrant_id in enumerate(
            (registration["team0"], registration["team1"])
        ):
            entrant = manifest["entrants"][entrant_id]
            record["participants"][index]["artifactPath"] = os.path.relpath(
                REPO / entrant["artifact"], destination
            )
            record["participants"][index]["sheetPath"] = os.path.relpath(
                REPO / entrant["sheet"], destination
            )
        record["broadcast"]["file"] = "broadcast.json.gz"
        record_target = destination / "match-record.json"
        write_json(record_target, record, compact=True)

        if record_target.stat().st_size > RECORD_LIMIT:
            raise ValueError(f"{cell_id}: retained record exceeds {RECORD_LIMIT} B")
        if broadcast_target.stat().st_size > BROADCAST_LIMIT:
            raise ValueError(
                f"{cell_id}: retained broadcast exceeds {BROADCAST_LIMIT} B"
            )
        broadcast_total += broadcast_target.stat().st_size

        success = qualifying(cell, "success")
        counter = qualifying(cell, "counter", prefer_committed=True)
        casualty = qualifying(cell, "casualtyRecovery")
        counter_kind = (
            "committed counter"
            if counter["qualifies"]["committedCounter"]
            else "preparation denial"
        )
        operation_team = int(cell["operationTeamId"])
        winner = record["result"]["winnerTeamId"]
        winner_label = "Draw" if winner is None else f"{TEAM_LABELS[int(winner)]} wins"
        result_label = (
            f"{winner_label} by {record['result']['reason'].replace('-', ' ')} "
            f"at t{record['result']['endTick']}"
        )
        first_casualty = casualty["casualties"][0]
        casualty_unit = str(first_casualty["unitId"])
        respawn = casualty["postReleaseCommands"][casualty_unit]
        subtitle = (
            f"{TEAM_LABELS[operation_team]} operation vs "
            f"{title(cell['opponentId'])} · {result_label} · "
            f"success t{success['terminalTick']}→release t{success['releaseTick']}; "
            f"{counter_kind} t{counter['terminalTick']}→release "
            f"t{counter['releaseTick']}; casualty t{first_casualty['tick']}→"
            f"baseline life {respawn['lifeId']} t{respawn['tick']}"
        )

        source_value = str(broadcast_target.relative_to(REPO))
        sample_entries.append({
            "source": source_value,
            "rules": record["rulesetId"],
            "map": record["mapId"],
            "matchSeed": record["seed"],
            "participants": [
                title(registration["team0"]),
                title(registration["team1"]),
            ],
            "operationId": operation_id,
            "cardTitle": (
                f"{title(operation_id)} — "
                f"{TEAM_LABELS[operation_team]} operation"
            ),
            "cardSubtitle": subtitle,
        })
        retained.append({
            "operationId": operation_id,
            "cellId": cell_id,
            "seed": cell["seed"],
            "operationTeamId": operation_team,
            "opponentId": cell["opponentId"],
            "counterThesis": cell["counterThesis"],
            "result": record["result"],
            "canonicalReplayHash": cell["canonicalReplayHash"],
            "sourceReplayFileSha256": cell["replayFileSha256"],
            "record": {
                "file": str(record_target.relative_to(REPO)),
                "bytes": record_target.stat().st_size,
                "sha256": sha256(record_target),
            },
            "broadcast": {
                "file": source_value,
                "gzipBytes": broadcast_target.stat().st_size,
                "sha256": sha256(broadcast_target),
            },
            "scorecard": {
                "file": str(scorecard_target.relative_to(REPO)),
                "bytes": scorecard_target.stat().st_size,
                "sha256": sha256(scorecard_target),
            },
            "success": activation_evidence(success),
            "counter": {
                "kind": counter_kind,
                **activation_evidence(counter),
            },
            "casualtyRecovery": activation_evidence(casualty),
            "feltDegeneracy": cell["feltDegeneracy"],
            "strandedActivations": cell["strandedActivations"],
            "matchEndPreemptedReleases": cell["matchEndPreemptedReleases"],
            "activeAtMatchEnd": cell["activeAtMatchEnd"],
        })

    if broadcast_total > GALLERY_LIMIT:
        raise ValueError(f"retained gallery exceeds {GALLERY_LIMIT} B")
    sample_entries.sort(key=lambda entry: hashlib.sha256(
        entry["source"].encode("utf-8")
    ).hexdigest())
    cards = [
        {
            "id": f"sample-{index:02}",
            "title": entry["cardTitle"],
            "subtitle": entry["cardSubtitle"],
        }
        for index, entry in enumerate(sample_entries, start=1)
    ]
    write_json(output / "gallery-sample.json", {
        "sampleVersion": 2,
        "selection": (
            "outcome-visible deterministic one-all-three-cell-per-operation"
        ),
        "outcomeBlind": False,
        "identitiesBlind": False,
        "populationSize": len(read["cells"]),
        "replays": [
            {key: value for key, value in entry.items()
             if key not in ("cardTitle", "cardSubtitle")}
            for entry in sample_entries
        ],
    })
    write_json(output / "gallery-cards.json", {
        "title": "Arc Relay operation reliability — labelled 3D review",
        "intro": (
            "Outcome-visible review. Every card names the operation side, "
            "opponent, final result, and exact success, counter, casualty, "
            "release, and baseline-return ticks."
        ),
        "cards": cards,
    })
    evidence = {
        "schema": "arc-relay-operation-retained-evidence-v1",
        "manifest": str(args.manifest.resolve()),
        "manifestSha256": sha256(args.manifest),
        "analysis": str(args.read.resolve()),
        "analysisSha256": sha256(args.read),
        "counterTaxonomy": str(taxonomy.relative_to(REPO)),
        "counterTaxonomySha256": sha256(taxonomy),
        "runtime": read["runtime"],
        "confirmationCells": read["readCells"],
        "eligibleConfirmationCells": read["eligibleCells"],
        "allRequirementsMet": read["allRequirementsMet"],
        "allCommittedOnlyRequirementsMet": read[
            "allCommittedOnlyRequirementsMet"
        ],
        "retainedMatches": retained,
        "budgets": {
            "recordBytesPerMatch": RECORD_LIMIT,
            "broadcastGzipBytesPerMatch": BROADCAST_LIMIT,
            "galleryBroadcastGzipBytes": GALLERY_LIMIT,
            "actualGalleryBroadcastGzipBytes": broadcast_total,
        },
    }
    write_json(output / "retained-evidence.json", evidence)
    print(
        f"retained {len(retained)} operation matches; "
        f"gallery broadcasts {broadcast_total} B"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
