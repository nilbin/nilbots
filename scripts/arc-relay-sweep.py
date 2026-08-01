#!/usr/bin/env python3
"""Run deterministic Arc Relay cells concurrently with restart-safe evidence.

The harness treats a sweep attempt as the unit of provenance. An interrupted
attempt may resume only while its manifest, runner, and built CLI surface are
byte-identical. A failed or changed attempt must be relaunched whole; verified
cells from different execution surfaces are never spliced into one result.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import gzip
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import threading
import time
from typing import Any


REPO = Path(__file__).resolve().parent.parent
DEFAULT_CLI = REPO / "src/BotArena.Cli/bin/Debug/net10.0/botarena.dll"
MATCH_RUNNER = REPO / "scripts/arc-relay-match.py"
BROADCAST_RUNNER = REPO / "scripts/arc-relay-broadcast.py"
SCORECARD_RUNNER = REPO / "scripts/arc-relay-scorecard.py"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def sha256_values(values: list[tuple[str, str]]) -> str:
    encoded = json.dumps(
        values,
        ensure_ascii=False,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def read_json(path: Path) -> dict[str, Any]:
    with path.open("rb") as source:
        prefix = source.read(2)
    opener = gzip.open if prefix == b"\x1f\x8b" else open
    with opener(path, "rt", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: JSON root must be an object")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    encoded = json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(encoded, encoding="utf-8")
    temporary.replace(path)


def resolve_repo_path(value: str) -> Path:
    path = Path(value)
    return path.resolve() if path.is_absolute() else (REPO / path).resolve()


def execution_surface(cli: Path) -> dict[str, Any]:
    if not cli.is_file():
        raise FileNotFoundError(cli)
    files = [MATCH_RUNNER, BROADCAST_RUNNER, SCORECARD_RUNNER, Path(__file__)]
    files.extend(sorted(cli.parent.glob("BotArena*.dll")))
    if cli not in files:
        files.append(cli)
    identities = sorted(
        (str(path.resolve().relative_to(REPO)), sha256(path))
        for path in set(files)
        if path.is_file()
    )
    return {
        "files": [{"path": path, "sha256": digest} for path, digest in identities],
        "sha256": sha256_values(identities),
    }


def validate_manifest(path: Path) -> dict[str, Any]:
    manifest = read_json(path)
    if manifest.get("schema") not in (
        "arc-relay-golden-sweep-v1",
        "arc-relay-sweep-plan-v1",
    ):
        raise ValueError("unsupported Arc Relay sweep manifest schema")
    entrants = manifest.get("entrants")
    cells = manifest.get("cells")
    if not isinstance(entrants, dict) or len(entrants) < 2:
        raise ValueError("sweep needs at least two entrants")
    if not isinstance(cells, list) or not cells:
        raise ValueError("sweep needs at least one cell")
    if "goldenId" in manifest and len(cells) < 6:
        raise ValueError("a golden sweep must freeze at least six cells")
    for entrant_id, entrant in entrants.items():
        artifact = resolve_repo_path(entrant["artifact"])
        sheet = resolve_repo_path(entrant["sheet"])
        if not artifact.is_file() or not sheet.is_file():
            raise FileNotFoundError(f"{entrant_id}: missing artifact or sheet")
        for file_path, field in (
            (artifact, "artifactSha256"),
            (sheet, "sheetSha256"),
        ):
            actual = sha256(file_path)
            if actual != entrant[field]:
                raise ValueError(
                    f"{entrant_id} {field}: declared {entrant[field]}, actual {actual}"
                )
    ids: set[str] = set()
    for cell in cells:
        cell_id = cell.get("cellId")
        if not isinstance(cell_id, str) or not cell_id or cell_id in ids:
            raise ValueError("cellId values must be non-empty and unique")
        ids.add(cell_id)
        for side in ("team0", "team1"):
            if cell.get(side) not in entrants:
                raise ValueError(f"{cell_id}: unknown {side} entrant")
        expected = cell.get("canonicalReplaySha256")
        if expected is not None and (
            not isinstance(expected, str) or len(expected) != 64
        ):
            raise ValueError(f"{cell_id}: invalid canonical replay hash")
    return manifest


class ProcessGroup:
    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._active: set[subprocess.Popen[str]] = set()

    def run(
        self,
        command: list[str],
        log_path: Path,
        cancelled: threading.Event,
    ) -> int:
        log_path.parent.mkdir(parents=True, exist_ok=True)
        with log_path.open("w", encoding="utf-8") as log:
            process = subprocess.Popen(
                command,
                cwd=REPO,
                text=True,
                stdout=log,
                stderr=subprocess.STDOUT,
                start_new_session=True,
            )
            with self._lock:
                self._active.add(process)
            try:
                while process.poll() is None:
                    if cancelled.wait(0.1):
                        self._terminate(process)
                        break
                return process.wait()
            finally:
                with self._lock:
                    self._active.discard(process)

    def stop_all(self) -> None:
        with self._lock:
            active = list(self._active)
        for process in active:
            self._terminate(process)

    @staticmethod
    def _terminate(process: subprocess.Popen[str]) -> None:
        if process.poll() is not None:
            return
        try:
            os.killpg(process.pid, signal.SIGTERM)
            process.wait(timeout=5)
        except (ProcessLookupError, subprocess.TimeoutExpired):
            if process.poll() is None:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass


def run_identity(
    manifest_path: Path,
    manifest: dict[str, Any],
    cli: Path,
) -> dict[str, Any]:
    surface = execution_surface(cli)
    return {
        "schema": "arc-relay-sweep-attempt-v1",
        "manifest": str(manifest_path.resolve()),
        "manifestSha256": sha256(manifest_path),
        "sweepId": manifest.get("goldenId", manifest.get("sweepId")),
        "runtime": manifest.get("runtime", "wasm"),
        "cli": str(cli.resolve()),
        "executionSurfaceSha256": surface["sha256"],
        "executionSurface": surface["files"],
        "cellCount": len(manifest["cells"]),
    }


def next_attempt(output: Path) -> Path:
    number = len(list(output.glob("attempt-*"))) + 1
    return output / f"attempt-{number:02d}"


def exact_identity(left: dict[str, Any], right: dict[str, Any]) -> bool:
    keys = (
        "manifestSha256",
        "sweepId",
        "runtime",
        "cli",
        "executionSurfaceSha256",
        "cellCount",
    )
    return all(left.get(key) == right.get(key) for key in keys)


def select_attempt(
    output: Path,
    identity: dict[str, Any],
    *,
    resume: bool,
    relaunch: bool,
) -> Path:
    attempts = sorted(output.glob("attempt-*")) if output.exists() else []
    if resume and relaunch:
        raise ValueError("use either --resume or --relaunch")
    if not attempts:
        if resume or relaunch:
            raise ValueError("no prior sweep attempt exists")
        output.mkdir(parents=True, exist_ok=True)
        attempt = next_attempt(output)
        attempt.mkdir()
        write_json(attempt / "RUN.json", identity)
        return attempt
    latest = attempts[-1]
    if relaunch:
        attempt = next_attempt(output)
        attempt.mkdir()
        write_json(attempt / "RUN.json", identity)
        return attempt
    if not resume:
        raise ValueError(f"{output} already contains a sweep; use --resume or --relaunch")
    if (latest / "COMPLETE.json").exists():
        raise ValueError("latest attempt is complete; use --relaunch for a new run")
    if (latest / "FAILED.json").exists():
        raise ValueError("failed attempts cannot resume; use --relaunch")
    frozen = read_json(latest / "RUN.json")
    if not exact_identity(frozen, identity):
        raise ValueError(
            "manifest, runner, or built CLI changed; kill-fix requires --relaunch"
        )
    return latest


def command_for_cell(
    cli: Path,
    manifest: dict[str, Any],
    cell: dict[str, Any],
    directory: Path,
) -> list[str]:
    entrants = manifest["entrants"]
    team0 = entrants[cell["team0"]]
    team1 = entrants[cell["team1"]]
    command = [
        sys.executable,
        str(MATCH_RUNNER),
        "--cli",
        str(cli.resolve()),
        "run",
        "--artifact0",
        str(resolve_repo_path(team0["artifact"])),
        "--artifact1",
        str(resolve_repo_path(team1["artifact"])),
        "--sheet0",
        str(resolve_repo_path(team0["sheet"])),
        "--sheet1",
        str(resolve_repo_path(team1["sheet"])),
        "--seed",
        str(cell["seed"]),
        "--output",
        str(directory.resolve()),
        "--cohort-id",
        manifest["cohortId"],
        "--match-id",
        cell["cellId"],
        "--entrant0-id",
        cell["team0"],
        "--entrant1-id",
        cell["team1"],
        "--runtime",
        manifest.get("runtime", "wasm"),
        "--loop-profile",
        manifest.get("loopProfile", "h0"),
    ]
    return command


def accepted_attempt(cell_dir: Path) -> tuple[Path, dict[str, Any]] | None:
    for attempt in sorted(cell_dir.glob("attempt-*"), reverse=True):
        result_path = attempt / "cell-result.json"
        if not result_path.is_file():
            continue
        result = read_json(result_path)
        paths = {
            "replay": attempt / "replay.json.gz",
            "record": attempt / "match-record.json",
            "broadcast": attempt / "broadcast.json.gz",
            "scorecard": attempt / "scorecard.json",
        }
        if not all(path.is_file() for path in paths.values()):
            continue
        if any(sha256(paths[name]) != result[f"{name}FileSha256"] for name in paths):
            continue
        if result.get("verified") is True:
            return attempt, result
    return None


def validate_record(
    manifest: dict[str, Any],
    cell: dict[str, Any],
    record: dict[str, Any],
) -> None:
    exact = (
        ("matchId", record.get("matchId"), cell["cellId"]),
        ("seed", record.get("seed"), str(cell["seed"])),
        (
            "loopProfile",
            record.get("loopProfile", "h0"),
            manifest.get("loopProfile", "h0"),
        ),
        ("engineVersion", record.get("engineVersion"), manifest["engineVersion"]),
        ("rulesetId", record.get("rulesetId"), manifest["rulesetId"]),
        (
            "rulesFingerprint",
            record.get("rulesFingerprint"),
            manifest["rulesFingerprint"],
        ),
        ("mapId", record.get("mapId"), manifest["mapId"]),
        (
            "mapFingerprint",
            record.get("mapFingerprint"),
            manifest["mapFingerprint"],
        ),
        (
            "topologyFingerprint",
            record.get("topologyFingerprint"),
            cell["topologyFingerprint"],
        ),
        (
            "matchContractFingerprint",
            record.get("matchContractFingerprint"),
            cell["matchContractFingerprint"],
        ),
    )
    for label, actual, expected in exact:
        if actual != expected:
            raise ValueError(f"{cell['cellId']} {label}: expected {expected}, got {actual}")
    entrants = manifest["entrants"]
    for index, entrant_id in enumerate((cell["team0"], cell["team1"])):
        participant = record["participants"][index]
        entrant = entrants[entrant_id]
        if participant["entrantId"] != entrant_id:
            raise ValueError(f"{cell['cellId']}: participant order changed")
        if participant["artifactHash"] != entrant["artifactSha256"]:
            raise ValueError(f"{cell['cellId']}: algorithm/artifact hash changed")
        if participant["sheetHash"] != entrant["sheetSha256"]:
            raise ValueError(f"{cell['cellId']}: sheet hash changed")
    expected_hash = cell.get("canonicalReplaySha256")
    actual_hash = record["canonicalReplay"]["hash"]
    if expected_hash is not None and actual_hash != expected_hash:
        raise ValueError(
            f"{cell['cellId']}: canonical hash moved: {expected_hash} -> {actual_hash}"
        )


def execute_cell(
    process_group: ProcessGroup,
    cancelled: threading.Event,
    cli: Path,
    manifest: dict[str, Any],
    sweep_attempt: Path,
    cell: dict[str, Any],
    *,
    resume: bool,
) -> dict[str, Any]:
    cell_dir = sweep_attempt / "cells" / cell["cellId"]
    if resume:
        accepted = accepted_attempt(cell_dir)
        if accepted is not None:
            attempt, prior = accepted
            verify_count = int(prior.get("verificationRuns", 1)) + 1
            code = process_group.run(
                ["dotnet", str(cli.resolve()), "verify", str(attempt / "replay.json.gz")],
                attempt / f"resume-verify-{verify_count:02d}.log",
                cancelled,
            )
            if code == 0 and not cancelled.is_set():
                prior["verificationRuns"] = verify_count
                write_json(attempt / "cell-result.json", prior)
                return {"cell": cell, "attempt": attempt, "result": prior, "resumed": True}
    if cancelled.is_set():
        return {"cell": cell, "status": "cancelled"}
    attempt_number = len(list(cell_dir.glob("attempt-*"))) + 1
    attempt = cell_dir / f"attempt-{attempt_number:02d}"
    attempt.mkdir(parents=True)
    command = command_for_cell(cli, manifest, cell, attempt)
    write_json(attempt / "command.json", {"command": command})
    started = time.perf_counter()
    code = process_group.run(command, attempt / "runner.log", cancelled)
    if code != 0 or cancelled.is_set():
        raise RuntimeError(f"{cell['cellId']}: runner exited {code}")
    replay = attempt / "replay.json.gz"
    record_path = attempt / "match-record.json"
    broadcast = attempt / "broadcast.json.gz"
    if not all(path.is_file() for path in (replay, record_path, broadcast)):
        raise RuntimeError(f"{cell['cellId']}: runner did not produce all files")
    verify_code = process_group.run(
        ["dotnet", str(cli.resolve()), "verify", str(replay)],
        attempt / "verify.log",
        cancelled,
    )
    if verify_code != 0 or cancelled.is_set():
        raise RuntimeError(f"{cell['cellId']}: replay verification exited {verify_code}")
    record = read_json(record_path)
    validate_record(manifest, cell, record)
    scorecard_path = attempt / "scorecard.json"
    scorecard_code = process_group.run(
        [
            sys.executable,
            str(SCORECARD_RUNNER),
            str(broadcast),
            "--record",
            str(record_path),
            "--output",
            str(scorecard_path),
        ],
        attempt / "scorecard.log",
        cancelled,
    )
    if scorecard_code != 0 or cancelled.is_set():
        raise RuntimeError(
            f"{cell['cellId']}: scorecard exited {scorecard_code}")
    scorecard = read_json(scorecard_path)
    eligibility = scorecard["feltDegeneracy"]
    team_eligibility = eligibility["cohortEligibilityByTeam"]
    result = {
        "schema": "arc-relay-sweep-cell-result-v1",
        "cellId": cell["cellId"],
        "runtime": manifest.get("runtime", "wasm"),
        "verified": True,
        "verificationRuns": 1,
        "expectedCanonicalReplaySha256": cell.get("canonicalReplaySha256"),
        "actualCanonicalReplaySha256": record["canonicalReplay"]["hash"],
        "replayFileSha256": sha256(replay),
        "recordFileSha256": sha256(record_path),
        "broadcastFileSha256": sha256(broadcast),
        "scorecardFileSha256": sha256(scorecard_path),
        "recordBytes": record_path.stat().st_size,
        "broadcastGzipBytes": broadcast.stat().st_size,
        "cohortEligibilityByTeam": team_eligibility,
        "matchEligibleForCohortRead": eligibility[
            "matchEligibleForCohortRead"],
        "elapsedSeconds": round(time.perf_counter() - started, 6),
    }
    write_json(attempt / "cell-result.json", result)
    return {"cell": cell, "attempt": attempt, "result": result, "resumed": False}


def execute_sweep(
    manifest: dict[str, Any],
    attempt: Path,
    cli: Path,
    *,
    jobs: int,
    resume: bool,
    keep_canonical: bool,
) -> dict[str, Any]:
    cancelled = threading.Event()
    process_group = ProcessGroup()
    rows: list[dict[str, Any]] = []
    failures: list[str] = []
    started = time.perf_counter()

    def worker(cell: dict[str, Any]) -> dict[str, Any]:
        try:
            return execute_cell(
                process_group,
                cancelled,
                cli,
                manifest,
                attempt,
                cell,
                resume=resume,
            )
        except Exception as error:  # noqa: BLE001 - failure must stop the sweep
            cancelled.set()
            process_group.stop_all()
            raise RuntimeError(str(error)) from error

    with concurrent.futures.ThreadPoolExecutor(max_workers=jobs) as pool:
        futures = [pool.submit(worker, cell) for cell in manifest["cells"]]
        for future in futures:
            try:
                row = future.result()
                if row.get("status") != "cancelled":
                    rows.append(row)
            except Exception as error:  # noqa: BLE001 - recorded below
                failures.append(str(error))
    rows.sort(key=lambda item: item["cell"]["cellId"])
    if failures:
        failed = {
            "schema": "arc-relay-sweep-failure-v1",
            "runtime": manifest.get("runtime", "wasm"),
            "verifiedCellsBeforeFailure": len(rows),
            "failures": failures,
            "elapsedSeconds": round(time.perf_counter() - started, 6),
        }
        write_json(attempt / "FAILED.json", failed)
        raise RuntimeError("; ".join(failures))

    canonical_count = sum(
        int((row["attempt"] / "replay.json.gz").is_file()) for row in rows
    )
    record_count = sum(
        int((row["attempt"] / "match-record.json").is_file()) for row in rows
    )
    broadcast_count = sum(
        int((row["attempt"] / "broadcast.json.gz").is_file()) for row in rows
    )
    scorecard_count = sum(
        int((row["attempt"] / "scorecard.json").is_file()) for row in rows
    )
    if (canonical_count, record_count, broadcast_count, scorecard_count) != (
        len(manifest["cells"]),
        len(manifest["cells"]),
        len(manifest["cells"]),
        len(manifest["cells"]),
    ):
        raise RuntimeError("sweep completion file counts do not match the plan")
    results = {
        "schema": "arc-relay-sweep-results-v1",
        "sweepId": manifest.get("goldenId", manifest.get("sweepId")),
        "runtime": manifest.get("runtime", "wasm"),
        "plannedCells": len(manifest["cells"]),
        "verifiedCanonicalReplays": canonical_count,
        "matchRecordFiles": record_count,
        "broadcastFiles": broadcast_count,
        "scorecardFiles": scorecard_count,
        "eligibleCells": sum(
            int(row["result"]["matchEligibleForCohortRead"])
            for row in rows
        ),
        "allCellsEligibleForCohortRead": all(
            row["result"]["matchEligibleForCohortRead"] for row in rows
        ),
        "allExpectedCanonicalHashesMatched": all(
            row["result"]["expectedCanonicalReplaySha256"]
            == row["result"]["actualCanonicalReplaySha256"]
            for row in rows
            if row["result"]["expectedCanonicalReplaySha256"] is not None
        ),
        "elapsedSeconds": round(time.perf_counter() - started, 6),
        "jobs": jobs,
        "cells": [
            {
                **row["result"],
                "attempt": str(row["attempt"].relative_to(attempt)),
                "resumed": row["resumed"],
            }
            for row in rows
        ],
    }
    write_json(attempt / "results.json", results)
    if not keep_canonical:
        for row in rows:
            (row["attempt"] / "replay.json.gz").unlink()
        results["canonicalReplayFilesAfterPrune"] = 0
        write_json(attempt / "results.json", results)
    else:
        results["canonicalReplayFilesAfterPrune"] = canonical_count
        write_json(attempt / "results.json", results)
    write_json(attempt / "COMPLETE.json", results)
    return results


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--cli", type=Path, default=DEFAULT_CLI)
    parser.add_argument("--jobs", type=int, default=max(1, min(4, os.cpu_count() or 1)))
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--relaunch", action="store_true")
    parser.add_argument("--keep-canonical", action="store_true")
    args = parser.parse_args(argv)
    if args.jobs < 1:
        parser.error("--jobs must be at least 1")
    manifest_path = args.manifest.resolve()
    manifest = validate_manifest(manifest_path)
    identity = run_identity(manifest_path, manifest, args.cli.resolve())
    attempt = select_attempt(
        args.output.resolve(),
        identity,
        resume=args.resume,
        relaunch=args.relaunch,
    )
    results = execute_sweep(
        manifest,
        attempt,
        args.cli.resolve(),
        jobs=args.jobs,
        resume=args.resume,
        keep_canonical=args.keep_canonical,
    )
    write_json(
        args.output.resolve() / "LATEST.json",
        {
            "schema": "arc-relay-sweep-latest-v1",
            "attempt": attempt.name,
            "resultsSha256": sha256(attempt / "results.json"),
            "runtime": results["runtime"],
            "verifiedCells": results["verifiedCanonicalReplays"],
        },
    )
    print(
        f"{results['verifiedCanonicalReplays']}/{results['plannedCells']} "
        f"canonical replays verified on {results['runtime']} in "
        f"{results['elapsedSeconds']:.3f}s ({args.jobs} jobs)",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, json.JSONDecodeError, RuntimeError) as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(2)
