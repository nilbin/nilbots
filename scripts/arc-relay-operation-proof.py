#!/usr/bin/env python3
"""Inspect authoritative Arc Relay replays for complete operation success.

An operation qualifies only when one activation enters preparation, commits a
fixed branch, records ``mission-success``, completes physical recovery, and
emits baseline role tags for its surviving participants after release.
Signature-specific cards may additionally require named actions during that
same activation.
"""

from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import gzip
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
from typing import Any


REPO = Path(__file__).resolve().parent.parent
DEFAULT_CLI = REPO / "src/BotArena.Cli/bin/Debug/net10.0/botarena.dll"


STATE_PATTERN = r"(?:^|;)\s*{operation}=(dormant|prepare|commit|recover)(?:/([^\[]+))?\[([^\]]+)\]"


def read_json(path: Path) -> dict[str, Any]:
    with path.open("rb") as source:
        compressed = source.read(2) == b"\x1f\x8b"
    opener = gzip.open if compressed else open
    with opener(path, "rt", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: expected a JSON object")
    return value


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def team_turns(replay: dict[str, Any], team_id: int) -> list[dict[str, Any]]:
    return [
        turn
        for frame in replay["ticks"]
        for turn in frame["mindTurns"]
        if turn["teamId"] == team_id
    ]


def parse_state(
    operation_id: str, turn: dict[str, Any]
) -> dict[str, Any] | None:
    debug = turn.get("debugMessage") or ""
    match = re.search(
        STATE_PATTERN.format(operation=re.escape(operation_id)), debug
    )
    if match is None:
        return None
    payload = match.group(3)
    reason = payload.split("|", 1)[0]
    claims_match = re.search(r"(?:^|\|)c=([^|]+)", payload)
    claims = []
    if claims_match is not None and claims_match.group(1) != "-":
        for raw in claims_match.group(1).split(","):
            unit, task_id = raw.split(":", 1)
            claims.append({"unitId": int(unit), "taskId": task_id})
    return {
        "tick": turn["tick"],
        "state": match.group(1),
        "branch": match.group(2),
        "reason": reason,
        "claims": claims,
    }


def inspect_card(
    card: dict[str, Any], replay_path: Path, team_id: int
) -> dict[str, Any]:
    replay = read_json(replay_path)
    turns = team_turns(replay, team_id)
    states = [
        state
        for turn in turns
        if (state := parse_state(card["id"], turn)) is not None
    ]
    successes = [
        state
        for state in states
        if state["state"] == "recover" and state["reason"] == "mission-success"
    ]
    attempts = sum(
        state["state"] == "prepare" and state["reason"] == "evidence-and-actors"
        for state in states
    )
    commits = sum(
        state["state"] == "commit" and state["reason"].startswith("branch-")
        for state in states
    )
    if not successes:
        terminal_reasons = sorted(
            {
                state["reason"]
                for state in states
                if state["state"] in {"recover", "dormant"}
                and state["reason"] not in {
                    "recovering-success",
                    "recovering-abort",
                    "cooldown",
                    "trigger-false",
                    "trigger-unknown",
                }
            }
        )
        return {
            "id": card["id"],
            "passed": False,
            "attempts": attempts,
            "commits": commits,
            "failure": "no mission-success transition",
            "terminalReasons": terminal_reasons,
        }

    qualification_failures: list[dict[str, Any]] = []
    selected: dict[str, Any] | None = None
    for success in successes:
        failures: list[str] = []
        commit = next(
            (
                state
                for state in reversed(states)
                if state["tick"] <= success["tick"]
                and state["state"] == "commit"
                and state["reason"].startswith("branch-")
            ),
            None,
        )
        prepare = next(
            (
                state
                for state in reversed(states)
                if commit is not None
                and state["tick"] <= commit["tick"]
                and state["state"] == "prepare"
                and state["reason"] == "evidence-and-actors"
            ),
            None,
        )
        release = next(
            (
                state
                for state in states
                if state["tick"] >= success["tick"]
                and state["state"] == "dormant"
                and state["reason"].startswith("recovery-")
            ),
            None,
        )
        if prepare is None or commit is None or release is None:
            qualification_failures.append(
                {
                    "successTick": success["tick"],
                    "reasons": ["missing prepare, commit, or release transition"],
                }
            )
            continue

        active_turns = [
            turn
            for turn in turns
            if prepare["tick"] <= turn["tick"] <= success["tick"]
        ]
        action_ticks: dict[str, list[int]] = {}
        for required in card.get("requiredActionIds", []):
            action_ticks[required] = sorted(
                {
                    turn["tick"]
                    for turn in active_turns
                    for command in turn.get("commands", [])
                    if command.get("actionId") == required
                }
            )
            if not action_ticks[required]:
                failures.append(
                    f"successful activation omitted required action {required}"
                )

        participant_ids = sorted(
            {claim["unitId"] for claim in commit["claims"]}
        )
        release_turn = next(
            (turn for turn in turns if turn["tick"] == release["tick"]), None
        )
        baseline_tags = {
            command["unitId"]: command.get("roleTag") or ""
            for command in (release_turn or {}).get("commands", [])
            if command["unitId"] in participant_ids
        }
        if not baseline_tags:
            failures.append("no committed survivor emitted a post-release command")
        if any(tag.startswith("g-") for tag in baseline_tags.values()):
            failures.append("a released survivor retained an operation role tag")
        if team_id not in replay["result"]["eligibleTeamIds"]:
            failures.append("proof team was not runtime-eligible")

        if not failures:
            selected = {
                "prepareTick": prepare["tick"],
                "commitTick": commit["tick"],
                "branch": commit["branch"],
                "successTick": success["tick"],
                "releaseTick": release["tick"],
                "participants": participant_ids,
                "requiredActionTicks": action_ticks,
                "baselineRoleTags": baseline_tags,
            }
            break
        qualification_failures.append(
            {"successTick": success["tick"], "reasons": failures}
        )

    if selected is None:
        return {
            "id": card["id"],
            "passed": False,
            "attempts": attempts,
            "commits": commits,
            "successTransitions": len(successes),
            "failure": "no complete qualifying activation",
            "qualificationFailures": qualification_failures,
        }
    return {
        "id": card["id"],
        "passed": True,
        "attempts": attempts,
        "commits": commits,
        "successTransitions": len(successes),
        "proof": selected,
        "replayHash": replay["replayHash"],
        "result": replay["result"],
    }


def inspect(args: argparse.Namespace) -> int:
    catalog = read_json(args.catalog)
    results = []
    for card in catalog["cards"]:
        replay = args.replays / card["id"] / "replay.json.gz"
        if not replay.is_file():
            results.append(
                {
                    "id": card["id"],
                    "passed": False,
                    "failure": f"missing replay: {replay}",
                }
            )
            continue
        results.append(inspect_card(card, replay, args.team_id))
    receipt = {
        "schema": "arc-relay-intelligent-operation-proof-v1",
        "catalog": str(args.catalog),
        "teamId": args.team_id,
        "passed": all(result["passed"] for result in results),
        "passedCount": sum(result["passed"] for result in results),
        "requiredCount": len(results),
        "operations": results,
    }
    encoded = json.dumps(receipt, indent=2, sort_keys=True) + "\n"
    if args.json is not None:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(encoded, encoding="utf-8")
    print(encoded, end="")
    return 0 if receipt["passed"] else 1


def inspect_replay(args: argparse.Namespace) -> int:
    catalog = read_json(args.catalog)
    card = next(
        (
            candidate
            for candidate in catalog["cards"]
            if candidate["id"] == args.operation_id
        ),
        None,
    )
    if card is None:
        raise ValueError(f"unknown operation: {args.operation_id}")
    result = inspect_card(card, args.replay, args.team_id)
    encoded = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if args.json is not None:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(encoded, encoding="utf-8")
    print(encoded, end="")
    return 0 if result["passed"] else 1


def run_command(command: list[str], log_path: Path) -> None:
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


def run_proof_cell(
    card: dict[str, Any],
    catalog: dict[str, Any],
    args: argparse.Namespace,
) -> dict[str, Any]:
    catalog_root = args.catalog.resolve().parent
    output = args.output.resolve() / card["id"]
    output.mkdir(parents=True, exist_ok=True)
    replay = output / "replay.json.gz"
    seed = args.seed or str(card.get("proofSeed", catalog["proofSeed"]))
    sheet = catalog_root / card["sheet"]
    baseline = catalog_root / card.get(
        "opponentSheet", catalog["baselineSheet"]
    )
    opponent_artifact = (
        catalog_root / card["opponentArtifact"]
        if "opponentArtifact" in card
        else args.artifact
    )
    loop_profile = args.loop_profile or catalog["mapProfile"]
    expected_inputs = {
        "seed": seed,
        "runtime": args.runtime,
        "mapProfile": loop_profile,
        "teamId": args.team_id,
        "artifactHash": sha256(args.artifact),
        "opponentArtifactHash": sha256(opponent_artifact),
        "sheetHash": sha256(sheet),
        "baselineHash": sha256(baseline),
        "catalogHash": sha256(args.catalog),
        "cliHash": sha256(args.cli),
        "proofHarnessHash": sha256(Path(__file__)),
        "matchHarnessHash": sha256(REPO / "scripts/arc-relay-match.py"),
    }
    resume_receipt = output / "cell-inputs.json"
    can_resume = (
        args.resume
        and replay.is_file()
        and (output / "match-record.json").is_file()
        and (output / "broadcast.json.gz").is_file()
        and resume_receipt.is_file()
        and read_json(resume_receipt) == expected_inputs
    )
    if not can_resume:
        run_command(
            [
                sys.executable,
                str(REPO / "scripts/arc-relay-match.py"),
                "run",
                "--artifact0",
                str(args.artifact.resolve()),
                "--artifact1",
                str(opponent_artifact.resolve()),
                "--sheet0",
                str(sheet),
                "--sheet1",
                str(baseline),
                "--seed",
                seed,
                "--output",
                str(output),
                "--cohort-id",
                catalog["cohortId"],
                "--match-id",
                f"{catalog['cohortId']}-{card['id']}-s{seed}",
                "--entrant0-id",
                card["id"],
                "--entrant1-id",
                card.get("opponentId", "baseline"),
                "--runtime",
                args.runtime,
                "--loop-profile",
                loop_profile,
            ],
            output / "harness-run.log",
        )
        resume_receipt.write_text(
            json.dumps(expected_inputs, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
    run_command(
        ["dotnet", str(args.cli.resolve()), "verify", str(replay)],
        output / "verify.log",
    )
    result = inspect_card(card, replay, args.team_id)
    match_record = read_json(output / "match-record.json")
    result["seed"] = seed
    result["runtime"] = args.runtime
    result["resumed"] = can_resume
    result["sheetHash"] = match_record["participants"][0]["sheetHash"]
    result["baselineSheetHash"] = match_record["participants"][1]["sheetHash"]
    result["opponentArtifactHash"] = match_record["participants"][1][
        "artifactHash"
    ]
    result["matchRecord"] = f"{card['id']}/match-record.json"
    result["matchRecordBytes"] = (output / "match-record.json").stat().st_size
    result["broadcastBytes"] = match_record["broadcast"]["gzipBytes"]
    return result


def prove(args: argparse.Namespace) -> int:
    catalog = read_json(args.catalog)
    if args.runtime != catalog["runtimeForEvidence"]:
        raise ValueError(
            "proof runtime must match catalog runtimeForEvidence: "
            + catalog["runtimeForEvidence"]
        )
    cards = catalog["cards"]
    if args.operation_id:
        requested = set(args.operation_id)
        cards = [card for card in cards if card["id"] in requested]
        missing = requested - {card["id"] for card in cards}
        if missing:
            raise ValueError(
                "unknown operation ids: " + ", ".join(sorted(missing))
            )
    results: list[dict[str, Any]] = []
    with ThreadPoolExecutor(max_workers=args.workers) as executor:
        futures = {
            executor.submit(run_proof_cell, card, catalog, args): card["id"]
            for card in cards
        }
        for future in as_completed(futures):
            operation_id = futures[future]
            try:
                results.append(future.result())
            except Exception as error:  # surfaced in the durable receipt
                results.append(
                    {"id": operation_id, "passed": False, "failure": str(error)}
                )
    order = {card["id"]: index for index, card in enumerate(catalog["cards"])}
    results.sort(key=lambda result: order[result["id"]])
    receipt = {
        "schema": "arc-relay-intelligent-operation-live-proof-v1",
        "catalog": str(args.catalog),
        "artifact": str(args.artifact),
        "artifactHash": sha256(args.artifact),
        "teamId": args.team_id,
        "runtime": args.runtime,
        "passed": all(result["passed"] for result in results),
        "passedCount": sum(result["passed"] for result in results),
        "requiredCount": len(results),
        "operations": results,
    }
    encoded = json.dumps(receipt, indent=2, sort_keys=True) + "\n"
    proof_path = args.output / "proof.json"
    proof_path.write_text(encoded, encoding="utf-8")
    print(encoded, end="")
    return 0 if receipt["passed"] else 1


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    inspect_parser = subparsers.add_parser("inspect")
    inspect_parser.add_argument("--catalog", type=Path, required=True)
    inspect_parser.add_argument("--replays", type=Path, required=True)
    inspect_parser.add_argument("--team-id", type=int, default=0)
    inspect_parser.add_argument("--json", type=Path)
    inspect_parser.set_defaults(handler=inspect)
    replay_parser = subparsers.add_parser("inspect-replay")
    replay_parser.add_argument("--catalog", type=Path, required=True)
    replay_parser.add_argument("--operation-id", required=True)
    replay_parser.add_argument("--replay", type=Path, required=True)
    replay_parser.add_argument("--team-id", type=int, default=0)
    replay_parser.add_argument("--json", type=Path)
    replay_parser.set_defaults(handler=inspect_replay)
    prove_parser = subparsers.add_parser("prove")
    prove_parser.add_argument("--catalog", type=Path, required=True)
    prove_parser.add_argument("--artifact", type=Path, required=True)
    prove_parser.add_argument("--output", type=Path, required=True)
    prove_parser.add_argument("--cli", type=Path, default=DEFAULT_CLI)
    prove_parser.add_argument("--seed")
    prove_parser.add_argument(
        "--operation-id",
        action="append",
        help="run only this operation (repeatable)",
    )
    prove_parser.add_argument(
        "--loop-profile",
        help="override the catalog map/rules profile for a versioned candidate run",
    )
    prove_parser.add_argument("--team-id", type=int, default=0)
    prove_parser.add_argument("--runtime", choices=("wasm",), default="wasm")
    prove_parser.add_argument("--workers", type=int, default=4)
    prove_parser.add_argument("--resume", action="store_true")
    prove_parser.set_defaults(handler=prove)
    args = parser.parse_args()
    return args.handler(args)


if __name__ == "__main__":
    raise SystemExit(main())
