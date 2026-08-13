#!/usr/bin/env python3
"""Retain compact, hash-linked real-replay proof for the Arc presentation package.

Canonical replays stay in scratch because they contain private bot observations
and are tens of megabytes. This tool validates them, retains their compact
spectator broadcasts, and writes the exact public lifecycle coordinates used by
the visual review. Output is deterministic for identical inputs.
"""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
from typing import Any


REPOSITORY = Path(__file__).resolve().parent.parent
PRESENTATION_ID = "arc-relay-signatures-v1"
BROADCAST_LIMIT = 300 * 1024
SIGNATURE_IDS = (
    "arc-toss",
    "exchange",
    "falling-star",
    "hardlight-block",
    "kinetic-burst",
    "null-field",
    "prism-wall",
    "rail-line",
    "repair-beam",
    "sentinel-seed",
    "smoke-canister",
    "survey-flare",
    "target-paint",
    "tractor-hook",
    "trip-node",
    "vector-dash",
)
TERMINAL_REASON_RANK = {
    "completed": 8,
    "landed": 7,
    "expired": 6,
    "consumed": 5,
    "channel-ended": 4,
    "replaced": 3,
    "lock-lost": 2,
    "owner-destroyed": 1,
}
MEANINGFUL_FACTS = {
    "body-relocated",
    "signature-damage",
    "signature-repair",
}
OUTCOME_PAINTER_SIGNATURES = {
    "exchange",
    "falling-star",
    "kinetic-burst",
    "rail-line",
    "vector-dash",
}


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


def relative(path: Path) -> str:
    resolved = path.resolve()
    try:
        return str(resolved.relative_to(REPOSITORY))
    except ValueError:
        return str(resolved)


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )


def arc_fact(event: dict[str, Any]) -> dict[str, Any] | None:
    if event.get("kind") != "arc-relay":
        return None
    fact = event.get("payload", {}).get("fact")
    return fact if isinstance(fact, dict) else None


def load_match(match_id: str, directory: Path) -> dict[str, Any]:
    replay_path = directory / "replay.json.gz"
    run_path = directory / "run.json"
    broadcast_path = directory / "broadcast.json.gz"
    for path in (replay_path, run_path, broadcast_path):
        if not path.is_file():
            raise FileNotFoundError(path)

    replay = read_json(replay_path)
    run = read_json(run_path)
    broadcast = read_json(broadcast_path)
    header = replay.get("header", {})
    mode = header.get("contract", {}).get("rules", {}).get("gameMode", {})
    presentation = header.get("presentation", {})
    if (
        header.get("replayVersion") != 3
        or mode.get("kind") != "arc-relay"
        or mode.get("signatureGrammarVersion") != 3
        or presentation.get("signaturePresentationId") != PRESENTATION_ID
        or replay.get("partial") is not False
        or replay.get("result") is None
    ):
        raise ValueError(f"{match_id}: not one complete grammar-3 Arc replay")
    if replay.get("replayHash") != run.get("Replay", {}).get("Hash"):
        raise ValueError(f"{match_id}: run and canonical replay hashes differ")
    if (
        broadcast.get("broadcastVersion") != 1
        or broadcast.get("canonicalReplayHash") != replay.get("replayHash")
        or len(broadcast.get("worlds", [])) != len(replay.get("ticks", []))
    ):
        raise ValueError(f"{match_id}: compact broadcast identity moved")
    runtime_kinds = [
        participant.get("RuntimeKind")
        for participant in run.get("Participants", [])
    ]
    if runtime_kinds != ["wasm", "wasm"]:
        raise ValueError(f"{match_id}: proof must cross two WASM runtimes")
    if broadcast_path.stat().st_size > BROADCAST_LIMIT:
        raise ValueError(f"{match_id}: compact broadcast exceeds byte ceiling")

    operations: dict[str, dict[str, Any]] = {}
    level_ups: list[dict[str, Any]] = []
    for tick in replay["ticks"]:
        tick_number = int(tick["tick"])
        mode_state = tick["postState"].get("mode", {})
        for state in mode_state.get("visibleSignatures", []):
            operation = operations.setdefault(
                state["operationId"],
                {
                    "signatureId": state["signatureId"],
                    "states": [],
                    "facts": [],
                },
            )
            operation["states"].append({
                "tick": tick_number,
                "phase": state["phase"],
                "remainingCapacity": state["remainingCapacity"],
                "suppressed": state["suppressed"],
            })
        for event in [*tick["tickStart"]["events"], *tick["events"]]:
            fact = arc_fact(event)
            if fact is None:
                continue
            if fact.get("kind") == "leveled-up":
                level_ups.append({
                    "tick": tick_number,
                    "actorId": fact["actorId"],
                    "level": fact["level"],
                    "position": fact["position"],
                })
            operation_id = fact.get("operationId")
            signature_id = fact.get("signatureId")
            if not operation_id or signature_id not in SIGNATURE_IDS:
                continue
            operation = operations.setdefault(
                operation_id,
                {"signatureId": signature_id, "states": [], "facts": []},
            )
            operation["facts"].append({
                "tick": tick_number,
                **{
                    key: fact[key]
                    for key in ("kind", "phase", "reason")
                    if key in fact
                },
            })

    forms = presentation.get("forms", [])
    projectile_ids = sorted(
        value["projectileLookId"] for value in forms
        if isinstance(value.get("projectileLookId"), str)
    )
    expected_projectiles = sorted(
        f"arc-shot-{class_id}"
        for class_id in (
            "hush", "kestrel", "lantern", "longshot", "mason",
            "minesmith", "mortar", "nest", "palisade", "patchbay",
            "relay", "repulsor", "sunder", "switchback", "towline", "veil",
        )
    )
    if projectile_ids != expected_projectiles:
        raise ValueError(f"{match_id}: replay lacks the full projectile package")

    return {
        "id": match_id,
        "directory": directory,
        "replayPath": replay_path,
        "broadcastPath": broadcast_path,
        "replay": replay,
        "run": run,
        "broadcast": broadcast,
        "operations": operations,
        "levelUps": level_ups,
        "projectileIds": projectile_ids,
    }


def operation_score(operation: dict[str, Any]) -> tuple[int, ...]:
    states = operation["states"]
    facts = operation["facts"]
    phases = {state["phase"] for state in states}
    capacity_values = {state["remainingCapacity"] for state in states}
    meaningful = sum(fact.get("kind") in MEANINGFUL_FACTS for fact in facts)
    terminal = max(
        (
            TERMINAL_REASON_RANK.get(fact.get("reason", ""), 0)
            for fact in facts
            if fact.get("kind") == "signature-changed"
            and fact.get("phase") is None
        ),
        default=0,
    )
    unsuppressed = sum(not state["suppressed"] for state in states)
    successful_terminal = any(
        fact.get("kind") == "signature-changed"
        and fact.get("phase") is None
        and fact.get("reason") in {"completed", "landed", "launched"}
        for fact in facts
    )
    return (
        len(phases),
        meaningful > 0,
        len(capacity_values) > 1,
        successful_terminal,
        terminal,
        min(len(states), 30),
        meaningful,
        unsuppressed,
    )


def lifecycle_record(match: dict[str, Any], operation_id: str) -> dict[str, Any]:
    operation = match["operations"][operation_id]
    states = operation["states"]
    facts = operation["facts"]
    if not states:
        raise ValueError(f"{operation_id}: lifecycle has facts but no public state")
    phases: list[dict[str, Any]] = []
    for phase in ("tell", "in-flight", "active", "channel"):
        matching = [state for state in states if state["phase"] == phase]
        if matching:
            phases.append({
                "phase": phase,
                "firstTick": matching[0]["tick"],
                "lastTick": matching[-1]["tick"],
            })
    terminal_facts = [
        fact for fact in facts
        if fact.get("kind") == "signature-changed"
        and fact.get("phase") is None
    ]
    start_tick = states[0]["tick"]
    action_phase = next(
        (phase for phase in phases if phase["firstTick"] > start_tick),
        None,
    )
    resolve_tick = terminal_facts[-1]["tick"] if terminal_facts else states[-1]["tick"]
    if action_phase is not None:
        action_time = action_phase["firstTick"] + 0.45
    elif len(states) > 1:
        action_time = states[-1]["tick"] + 0.45
    elif operation["signatureId"] in OUTCOME_PAINTER_SIGNATURES and terminal_facts:
        action_time = resolve_tick + 0.35
    else:
        action_time = start_tick + 0.75
    return {
        "signatureId": operation["signatureId"],
        "matchId": match["id"],
        "operationId": operation_id,
        "phases": phases,
        "facts": facts,
        "capacityValues": sorted({
            state["remainingCapacity"] for state in states
        }),
        "suppressedObserved": any(state["suppressed"] for state in states),
        "captureTimes": {
            "start": start_tick + 0.35,
            "action": action_time,
            "resolve": resolve_tick + (
                0.75
                if operation["signatureId"] in OUTCOME_PAINTER_SIGNATURES
                else 0.35
            ),
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--match",
        action="append",
        required=True,
        metavar="ID=DIRECTORY",
        help="Match ID and directory containing replay/run/broadcast files",
    )
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--source-revision")
    args = parser.parse_args()

    matches = []
    for value in args.match:
        if "=" not in value:
            raise ValueError("--match must be ID=DIRECTORY")
        match_id, raw_directory = value.split("=", 1)
        if not match_id or not raw_directory:
            raise ValueError("--match must be ID=DIRECTORY")
        matches.append(load_match(match_id, Path(raw_directory).resolve()))

    candidates: dict[str, list[tuple[dict[str, Any], str]]] = {
        signature_id: [] for signature_id in SIGNATURE_IDS
    }
    for match in matches:
        for operation_id, operation in match["operations"].items():
            signature_id = operation["signatureId"]
            if signature_id in candidates and operation["states"]:
                candidates[signature_id].append((match, operation_id))
    missing = [key for key, values in candidates.items() if not values]
    if missing:
        raise ValueError(f"real replay union lacks signatures: {', '.join(missing)}")

    lifecycle = []
    for signature_id in SIGNATURE_IDS:
        match, operation_id = max(
            candidates[signature_id],
            key=lambda value: operation_score(value[0]["operations"][value[1]]),
        )
        lifecycle.append(lifecycle_record(match, operation_id))

    output = args.output.resolve()
    replay_output = output / "replays"
    replay_output.mkdir(parents=True, exist_ok=True)
    match_records = []
    gallery_entries = []
    for match in matches:
        target = replay_output / f"{match['id']}.broadcast.json.gz"
        shutil.copyfile(match["broadcastPath"], target)
        if sha256(target) != sha256(match["broadcastPath"]):
            raise ValueError(f"{match['id']}: retained broadcast bytes moved")
        replay = match["replay"]
        run = match["run"]
        observed = sorted({
            operation["signatureId"]
            for operation in match["operations"].values()
            if operation["states"]
        })
        participants = [
            {
                "teamId": value["TeamId"],
                "name": value["Name"],
                "runtimeKind": value["RuntimeKind"],
                "artifactHash": value["ArtifactHash"],
                "sheet": relative(Path(value["SheetPath"])),
                "sheetHash": value["SheetHash"],
                "classes": value["Classes"],
            }
            for value in run["Participants"]
        ]
        match_records.append({
            "id": match["id"],
            "seed": run["Seed"],
            "rulesetId": run["RulesetId"],
            "rulesFingerprint": run["RulesFingerprint"],
            "mapId": run["MapId"],
            "mapFingerprint": run["MapFingerprint"],
            "matchContractFingerprint": run["MatchContractFingerprint"],
            "result": run["Result"],
            "participants": participants,
            "canonicalReplay": {
                "hash": replay["replayHash"],
                "ticks": len(replay["ticks"]),
                "sourceGzipBytes": match["replayPath"].stat().st_size,
                "sourceGzipSha256": sha256(match["replayPath"]),
            },
            "broadcast": {
                "file": relative(target),
                "gzipBytes": target.stat().st_size,
                "sha256": sha256(target),
            },
            "observedSignatureIds": observed,
            "levelUps": match["levelUps"],
        })
        gallery_entries.append({
            "id": match["id"],
            "broadcastOnly": relative(target),
            "displayBots": [
                Path(value["sheet"]).stem for value in participants
            ],
        })

    revision = args.source_revision or subprocess.check_output(
        ["git", "rev-parse", "HEAD"], cwd=REPOSITORY, text=True
    ).strip()
    manifest = {
        "schema": "arc-relay-presentation-runtime-proof-v1",
        "sourceRevision": revision,
        "map": "arc-relay-tapline-01",
        "loopProfile": "tapline-2",
        "runtime": "wasm",
        "signatureGrammarVersion": 3,
        "signaturePresentationId": PRESENTATION_ID,
        "passed": True,
        "coverage": {
            "signatureIds": list(SIGNATURE_IDS),
            "signatureCount": len(SIGNATURE_IDS),
            "projectileLookIds": matches[0]["projectileIds"],
            "projectileLookCount": len(matches[0]["projectileIds"]),
            "levelUpFactCount": sum(len(match["levelUps"]) for match in matches),
        },
        "matches": match_records,
        "lifecycleEvidence": lifecycle,
        "budgets": {
            "broadcastGzipBytesPerMatch": BROADCAST_LIMIT,
            "actualBroadcastGzipBytes": sum(
                value["broadcast"]["gzipBytes"] for value in match_records
            ),
        },
    }
    write_json(output / "proof-manifest.json", manifest)
    write_json(output / "gallery-sources.json", {
        "schemaVersion": 1,
        "outputDirectory": "web/dist-review",
        "archiveDirectory": "web/dist-review/replay-archive",
        "entries": gallery_entries,
    })
    print(
        f"retained {len(matches)} WASM Tapline matches with "
        f"{len(SIGNATURE_IDS)}/{len(SIGNATURE_IDS)} signature coverage"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
