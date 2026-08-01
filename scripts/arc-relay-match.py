#!/usr/bin/env python3
"""Run or regenerate one frozen Arc Relay H0 evaluation match.

Per-game durable output is exactly ``match-record.json`` plus
``broadcast.json.gz``. The canonical replay is retained only in evaluation
evidence as ``replay.json.gz`` and is never copied into galleries. A record
references frozen cohort WASM/sheet assets by relative path, so regeneration
needs no prior replay and verifies both asset digests before simulation.
"""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

REPO = Path(__file__).resolve().parent.parent
DEFAULT_CLI = REPO / "src/BotArena.Cli/bin/Debug/net10.0/botarena.dll"
RECORD_LIMIT = 4 * 1024
BROADCAST_LIMIT = 300 * 1024
TOTAL_LIMIT = 304 * 1024


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def read_json(path: Path) -> dict:
    with path.open("rb") as source:
        prefix = source.read(2)
    opener = gzip.open if prefix == b"\x1f\x8b" else open
    with opener(path, "rt", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: JSON root must be an object")
    return value


def relative(path: Path, record_dir: Path) -> str:
    return os.path.relpath(path.resolve(), record_dir.resolve())


def run_process(command: list[str], log_path: Path) -> None:
    completed = subprocess.run(
        command,
        cwd=REPO,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    log_path.write_text(completed.stdout, encoding="utf-8")
    if completed.returncode != 0:
        raise RuntimeError(
            f"command exited {completed.returncode}; see {log_path}\n"
            + completed.stdout[-2000:]
        )


def execute(
    *,
    cli: Path,
    artifact0: Path,
    artifact1: Path,
    sheet0: Path,
    sheet1: Path,
    seed: str,
    output: Path,
    cohort_id: str,
    match_id: str,
    entrant0_id: str,
    entrant1_id: str,
    runtime: str = "wasm",
    loop_profile: str = "h0",
) -> dict:
    for path in (cli, sheet0, sheet1):
        if not path.is_file():
            raise FileNotFoundError(path)
    for path in (artifact0, artifact1):
        if not path.exists():
            raise FileNotFoundError(path)
    if runtime not in ("wasm", "in-process"):
        raise ValueError(f"unsupported runtime: {runtime}")
    output.mkdir(parents=True, exist_ok=True)
    run_process(
        [
            "dotnet",
            str(cli.resolve()),
            "experiment",
            "arc-relay",
            "--bot",
            str(artifact0.resolve()),
            "--opponent",
            str(artifact1.resolve()),
            "--sheet0",
            str(sheet0.resolve()),
            "--sheet1",
            str(sheet1.resolve()),
            "--seed",
            seed,
            "--runtime",
            runtime,
            "--loop-profile",
            loop_profile,
            "--out",
            str(output.resolve()),
        ],
        output / "match.log",
    )
    run_receipt = read_json(output / "run.json")
    canonical_path = output / "replay.json.gz"
    canonical = read_json(canonical_path)
    broadcast_path = output / "broadcast.json.gz"
    run_process(
        [
            sys.executable,
            str(REPO / "scripts/arc-relay-broadcast.py"),
            str(canonical_path),
            "--output",
            str(broadcast_path),
            "--max-gzip-bytes",
            str(BROADCAST_LIMIT),
        ],
        output / "broadcast.log",
    )

    participants = run_receipt["Participants"]
    inputs = [
        (entrant0_id, artifact0, sheet0),
        (entrant1_id, artifact1, sheet1),
    ]
    for participant, (_, artifact, sheet) in zip(participants, inputs):
        actual_artifact = sha256(artifact) if artifact.is_file() else None
        actual_sheet = sha256(sheet)
        if (actual_artifact is not None
                and participant["ArtifactHash"] != actual_artifact):
            raise RuntimeError(
                f"runtime artifact hash {participant['ArtifactHash']} does not "
                f"match {artifact}: {actual_artifact}"
            )
        if participant["SheetHash"] != actual_sheet:
            raise RuntimeError(
                f"runtime sheet hash {participant['SheetHash']} does not "
                f"match {sheet}: {actual_sheet}"
            )

    record_dir = output
    record = {
        "schemaVersion": 1,
        "cohortId": cohort_id,
        "matchId": match_id,
        "engineVersion": canonical["header"]["engineVersion"],
        "rulesetId": run_receipt["RulesetId"],
        "rulesFingerprint": run_receipt["RulesFingerprint"],
        "mapId": run_receipt["MapId"],
        "mapFingerprint": run_receipt["MapFingerprint"],
        "topologyFingerprint": run_receipt["TopologyFingerprint"],
        "matchContractFingerprint": run_receipt["MatchContractFingerprint"],
        "executionRuntime": runtime,
        "loopProfile": loop_profile,
        "runtime": canonical["header"]["runtime"],
        "seed": seed,
        "participants": [
            {
                "participantId": participant["ParticipantId"],
                "teamId": participant["TeamId"],
                "entrantId": entrant_id,
                "name": participant["Name"],
                "runtimeKind": participant["RuntimeKind"],
                "artifactHash": participant["ArtifactHash"],
                "artifactPath": relative(artifact, record_dir),
                "sheetHash": participant["SheetHash"],
                "sheetPath": relative(sheet, record_dir),
                "classes": participant["Classes"],
            }
            for participant, (entrant_id, artifact, sheet)
            in zip(participants, inputs)
        ],
        "result": {
            "winnerTeamId": run_receipt["Result"]["WinnerTeamId"],
            "reason": run_receipt["Result"]["Reason"],
            "endTick": run_receipt["Result"]["EndTick"],
            "eligibleTeamIds": run_receipt["Result"]["EligibleTeamIds"],
        },
        "canonicalReplay": {
            "formatVersion": run_receipt["Replay"]["FormatVersion"],
            "hash": run_receipt["Replay"]["Hash"],
            "gzipBytes": canonical_path.stat().st_size,
        },
        "broadcast": {
            "formatVersion": 1,
            "sha256": sha256(broadcast_path),
            "file": broadcast_path.name,
            "gzipBytes": broadcast_path.stat().st_size,
        },
    }
    encoded = json.dumps(record, separators=(",", ":"), ensure_ascii=False)
    encoded_bytes = encoded.encode("utf-8")
    if len(encoded_bytes) > RECORD_LIMIT:
        raise RuntimeError(
            f"match record exceeds ceiling: {len(encoded_bytes)} > "
            f"{RECORD_LIMIT} bytes"
        )
    durable = len(encoded_bytes) + broadcast_path.stat().st_size
    if durable > TOTAL_LIMIT:
        raise RuntimeError(
            f"durable per-game payload exceeds ceiling: {durable} > "
            f"{TOTAL_LIMIT} bytes"
        )
    (output / "match-record.json").write_text(encoded, encoding="utf-8")
    return record


def comparable(record: dict) -> dict:
    clone = json.loads(json.dumps(record))
    clone.pop("executionRuntime", None)
    clone.pop("loopProfile", None)
    for participant in clone["participants"]:
        participant.pop("artifactPath", None)
        participant.pop("sheetPath", None)
        participant.pop("runtimeKind", None)
    clone["broadcast"].pop("file", None)
    return clone


def run_command(args: argparse.Namespace) -> int:
    record = execute(
        cli=args.cli,
        artifact0=args.artifact0,
        artifact1=args.artifact1,
        sheet0=args.sheet0,
        sheet1=args.sheet1,
        seed=args.seed,
        output=args.output,
        cohort_id=args.cohort_id,
        match_id=args.match_id,
        entrant0_id=args.entrant0_id,
        entrant1_id=args.entrant1_id,
        runtime=args.runtime,
        loop_profile=args.loop_profile,
    )
    record_bytes = (args.output / "match-record.json").stat().st_size
    broadcast_bytes = record["broadcast"]["gzipBytes"]
    print(
        f"{record['matchId']}: replay {record['canonicalReplay']['hash']}, "
        f"record {record_bytes} B + broadcast {broadcast_bytes} B = "
        f"{record_bytes + broadcast_bytes} B durable"
    )
    return 0


def regenerate_command(args: argparse.Namespace) -> int:
    expected = read_json(args.record)
    base = args.record.resolve().parent
    participants = expected["participants"]
    for participant in participants:
        artifact = (base / participant["artifactPath"]).resolve()
        sheet = (base / participant["sheetPath"]).resolve()
        if sha256(artifact) != participant["artifactHash"]:
            raise RuntimeError(f"artifact digest mismatch: {artifact}")
        if sha256(sheet) != participant["sheetHash"]:
            raise RuntimeError(f"sheet digest mismatch: {sheet}")

    with tempfile.TemporaryDirectory(prefix="nilbots-arc-regenerate-") as tmp:
        actual = execute(
            cli=args.cli,
            artifact0=(base / participants[0]["artifactPath"]).resolve(),
            artifact1=(base / participants[1]["artifactPath"]).resolve(),
            sheet0=(base / participants[0]["sheetPath"]).resolve(),
            sheet1=(base / participants[1]["sheetPath"]).resolve(),
            seed=expected["seed"],
            output=Path(tmp),
            cohort_id=expected["cohortId"],
            match_id=expected["matchId"],
            entrant0_id=participants[0]["entrantId"],
            entrant1_id=participants[1]["entrantId"],
            runtime=expected.get("executionRuntime", "wasm"),
            loop_profile=expected.get("loopProfile", "h0"),
        )
        if comparable(actual) != comparable(expected):
            raise RuntimeError(
                "regeneration mismatch:\nexpected "
                + json.dumps(comparable(expected), indent=2)
                + "\nactual "
                + json.dumps(comparable(actual), indent=2)
            )
    print(
        f"OK: {expected['matchId']} regenerated canonical hash "
        f"{expected['canonicalReplay']['hash']} from match record."
    )
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cli", type=Path, default=DEFAULT_CLI)
    sub = parser.add_subparsers(dest="command", required=True)

    run = sub.add_parser("run")
    run.add_argument("--artifact0", type=Path, required=True)
    run.add_argument("--artifact1", type=Path, required=True)
    run.add_argument("--sheet0", type=Path, required=True)
    run.add_argument("--sheet1", type=Path, required=True)
    run.add_argument("--seed", required=True)
    run.add_argument("--output", type=Path, required=True)
    run.add_argument("--cohort-id", required=True)
    run.add_argument("--match-id", required=True)
    run.add_argument("--entrant0-id", required=True)
    run.add_argument("--entrant1-id", required=True)
    run.add_argument(
        "--runtime",
        choices=("wasm", "in-process"),
        default="wasm",
    )
    run.add_argument("--loop-profile", default="h0")
    run.set_defaults(handler=run_command)

    regenerate = sub.add_parser("regenerate")
    regenerate.add_argument("record", type=Path)
    regenerate.set_defaults(handler=regenerate_command)
    args = parser.parse_args()
    return args.handler(args)


if __name__ == "__main__":
    raise SystemExit(main())
