#!/usr/bin/env python3
"""Read Arc Relay operation success, counterplay, and recovery from a WASM sweep.

The canonical replay is the authority for operation state and hostile contact;
the durable scorecard is the authority for the frozen felt-degeneracy bars.
An entrant name, match loss, or abort by itself is never counter evidence.
"""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
from pathlib import Path
import re
from typing import Any


REPO = Path(__file__).resolve().parent.parent
STATE_PATTERN = (
    r"(?:^|;)\s*{operation}="
    r"(dormant|prepare|commit|recover)(?:/([^\[]+))?\[([^\]]+)\]"
)
TERMINAL_REASONS = {
    "mission-success",
    "mission-abort",
    "commit-participant-minimum",
    "mission-deadline",
    "prepare-abort",
    "prepare-participant-minimum",
    "prepare-deadline",
}
MAX_RECOVERY_TICKS = 60


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


def resolve_repo_path(value: str) -> Path:
    path = Path(value)
    return path.resolve() if path.is_absolute() else (REPO / path).resolve()


def parse_state(operation_id: str, turn: dict[str, Any]) -> dict[str, Any] | None:
    match = re.search(
        STATE_PATTERN.format(operation=re.escape(operation_id)),
        turn.get("debugMessage") or "",
    )
    if match is None:
        return None
    payload = match.group(3)
    reason = payload.split("|", 1)[0]
    claims_match = re.search(r"(?:^|\|)c=([^|]+)", payload)
    claims: list[dict[str, Any]] = []
    if claims_match is not None and claims_match.group(1) != "-":
        for raw in claims_match.group(1).split(","):
            unit, task_id = raw.split(":", 1)
            claims.append({"unitId": int(unit), "taskId": task_id})
    evidence_match = re.search(r"(?:^|\|)e=([^|]+)", payload)
    evidence: dict[str, list[str]] = {}
    if evidence_match is not None:
        for raw in evidence_match.group(1).split(","):
            if ":" not in raw:
                continue
            fact, truth = raw.rsplit(":", 1)
            evidence.setdefault(fact, []).append(truth)
    return {
        "tick": int(turn["tick"]),
        "phase": match.group(1),
        "branch": match.group(2),
        "reason": reason,
        "claims": claims,
        "evidence": evidence,
        "raw": payload,
    }


def team_turns(replay: dict[str, Any], team_id: int) -> list[dict[str, Any]]:
    return [
        turn
        for tick in replay["ticks"]
        for turn in tick["mindTurns"]
        if int(turn["teamId"]) == team_id
    ]


def actor_tuple(value: dict[str, Any] | None) -> tuple[int, int, int] | None:
    if value is None:
        return None
    return (int(value["teamId"]), int(value["unitId"]), int(value["lifeId"]))


def participant_lives(
    turn: dict[str, Any], participant_ids: list[int]
) -> dict[int, int]:
    wanted = set(participant_ids)
    return {
        int(command["unitId"]): int(command["lifeId"])
        for command in turn.get("commands", [])
        if int(command["unitId"]) in wanted
    }


def hostile_contact(
    replay: dict[str, Any],
    team_id: int,
    lives: dict[int, int],
    first_tick: int,
    last_tick: int,
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    opponent = 1 - team_id
    impacts: list[dict[str, Any]] = []
    casualties: list[dict[str, Any]] = []
    claimed = {(team_id, unit_id, life_id) for unit_id, life_id in lives.items()}
    for frame in replay["ticks"]:
        tick = int(frame["tick"])
        if tick < first_tick or tick > last_tick:
            continue
        for event in frame.get("events", []):
            kind = event.get("kind")
            payload = event.get("payload") or {}
            target = None
            source_team = payload.get("sourceTeamId")
            if kind == "damage":
                target = actor_tuple(payload.get("targetActorId"))
            elif kind == "destruction":
                target = actor_tuple(payload.get("actorId"))
            if target not in claimed or source_team is None or int(source_team) != opponent:
                continue
            row = {
                "tick": tick,
                "kind": kind,
                "unitId": target[1],
                "lifeId": target[2],
                "sourceActor": payload.get("sourceActorId"),
            }
            impacts.append(row)
            if kind == "destruction":
                casualties.append(row)
    return impacts, casualties


def commands_after(
    turns: list[dict[str, Any]],
    tick: int,
    participant_ids: list[int],
) -> dict[int, dict[str, Any]]:
    wanted = set(participant_ids)
    result: dict[int, dict[str, Any]] = {}
    for turn in turns:
        if int(turn["tick"]) < tick:
            continue
        for command in turn.get("commands", []):
            unit_id = int(command["unitId"])
            if unit_id in wanted and unit_id not in result:
                result[unit_id] = {
                    "tick": int(turn["tick"]),
                    "lifeId": int(command["lifeId"]),
                    "roleTag": command.get("roleTag") or "",
                    "actionId": command.get("actionId"),
                }
    return result


def inspect_activations(
    operation: dict[str, Any],
    replay: dict[str, Any],
    team_id: int,
    match_eligible: bool,
) -> list[dict[str, Any]]:
    operation_id = operation["id"]
    recovery_deadline = int(
        operation.get("recoveryDeadlineTicks", MAX_RECOVERY_TICKS)
    )
    match_end_tick = int(replay["result"]["endTick"])
    turns = team_turns(replay, team_id)
    parsed = [
        (turn, state)
        for turn in turns
        if (state := parse_state(operation_id, turn)) is not None
    ]
    activations: list[dict[str, Any]] = []
    for start_index, (prepare_turn, prepare) in enumerate(parsed):
        if prepare["phase"] != "prepare" or prepare["reason"] != "evidence-and-actors":
            continue
        if any(
            candidate[1]["phase"] != "dormant"
            for candidate in parsed[max(0, start_index - 1):start_index]
        ):
            continue
        next_start = next(
            (
                index
                for index in range(start_index + 1, len(parsed))
                if parsed[index][1]["phase"] == "prepare"
                and parsed[index][1]["reason"] == "evidence-and-actors"
            ),
            len(parsed),
        )
        window = parsed[start_index:next_start]
        commit_pair = next(
            (
                pair for pair in window
                if pair[1]["phase"] == "commit"
                and pair[1]["reason"].startswith("branch-")
            ),
            None,
        )
        terminal_pair = next(
            (
                pair for pair in window
                if pair[1]["phase"] == "recover"
                and pair[1]["reason"] in TERMINAL_REASONS
            ),
            None,
        )
        release_pair = next(
            (
                pair for pair in window
                if terminal_pair is not None
                and pair[1]["tick"] >= terminal_pair[1]["tick"]
                and pair[1]["phase"] == "dormant"
                and pair[1]["reason"].startswith("recovery-")
            ),
            None,
        )
        claim_state = commit_pair[1] if commit_pair is not None else prepare
        participant_ids = sorted({
            int(claim["unitId"]) for claim in claim_state["claims"]
        })
        claim_turn = commit_pair[0] if commit_pair is not None else prepare_turn
        lives = participant_lives(claim_turn, participant_ids)
        terminal_tick = (
            terminal_pair[1]["tick"] if terminal_pair is not None
            else match_end_tick
        )
        release_tick = release_pair[1]["tick"] if release_pair is not None else None
        impacts, casualties = hostile_contact(
            replay,
            team_id,
            lives,
            prepare["tick"],
            terminal_tick,
        )
        post_release = (
            commands_after(turns, release_tick, participant_ids)
            if release_tick is not None else {}
        )
        survivor_ids = sorted(set(participant_ids) - {
            int(item["unitId"]) for item in casualties
        })
        survivor_baseline = all(
            unit_id in post_release
            and not post_release[unit_id]["roleTag"].startswith("g-")
            for unit_id in survivor_ids
        )
        casualty_respawn_baseline = all(
            unit_id in post_release
            and post_release[unit_id]["lifeId"] != lives.get(unit_id)
            and not post_release[unit_id]["roleTag"].startswith("g-")
            for unit_id in {int(item["unitId"]) for item in casualties}
        )
        bounded_release = (
            terminal_pair is not None
            and release_tick is not None
            and 0 <= release_tick - terminal_tick <= recovery_deadline
            and not release_pair[1]["claims"]
        )
        release_preempted_by_match_end = (
            terminal_pair is not None
            and release_tick is None
            and match_end_tick < terminal_tick + recovery_deadline
        )
        stranded = (
            terminal_pair is not None
            and release_tick is None
            and not release_preempted_by_match_end
        )
        baseline_release = bounded_release and survivor_baseline
        required_action_ticks = {
            action_id: sorted({
                int(turn["tick"])
                for turn, _ in window
                if int(turn["tick"]) <= terminal_tick
                for command in turn.get("commands", [])
                if command.get("actionId") == action_id
            })
            for action_id in operation.get("requiredActionIds", [])
        }
        required_actions_present = all(required_action_ticks.values())
        terminal_reason = terminal_pair[1]["reason"] if terminal_pair else None
        success = (
            commit_pair is not None
            and terminal_reason == "mission-success"
            and required_actions_present
            and baseline_release
            and match_eligible
        )
        committed_counter = (
            commit_pair is not None
            and terminal_reason is not None
            and terminal_reason != "mission-success"
            and bool(impacts)
            and baseline_release
            and match_eligible
        )
        # A prepared operation can be countered before its commitment lock:
        # killing a claimed setup body forces an explicit bounded abort. This
        # is distinct from a committed counter, and plain damage, a failed
        # trigger, or a deadline never qualifies as preparation denial.
        preparation_denial = (
            commit_pair is None
            and terminal_reason in {
                "prepare-abort",
                "prepare-participant-minimum",
            }
            and bool(casualties)
            and baseline_release
            and match_eligible
        )
        counter = committed_counter or preparation_denial
        casualty_recovery = (
            bool(casualties)
            and baseline_release
            and casualty_respawn_baseline
            and match_eligible
        )
        activations.append({
            "prepareTick": prepare["tick"],
            "commitTick": commit_pair[1]["tick"] if commit_pair else None,
            "branch": commit_pair[1]["branch"] if commit_pair else None,
            "terminalTick": terminal_tick if terminal_pair else None,
            "terminalReason": terminal_reason,
            "terminalEvidence": terminal_pair[1]["evidence"] if terminal_pair else {},
            "releaseTick": release_tick,
            "recoveryTicks": (
                release_tick - terminal_tick if release_tick is not None else None
            ),
            "recoveryDeadlineTicks": recovery_deadline,
            "matchEndTick": match_end_tick,
            "participants": participant_ids,
            "participantLives": {str(key): value for key, value in lives.items()},
            "requiredActionTicks": required_action_ticks,
            "hostileImpacts": impacts,
            "casualties": casualties,
            "postReleaseCommands": {
                str(key): value for key, value in post_release.items()
            },
            "boundedRelease": bounded_release,
            "releasePreemptedByMatchEnd": release_preempted_by_match_end,
            "stranded": stranded,
            "baselineRelease": baseline_release,
            "casualtyRespawnBaseline": casualty_respawn_baseline,
            "qualifies": {
                "success": success,
                "committedCounter": committed_counter,
                "preparationDenial": preparation_denial,
                "counter": counter,
                "casualtyRecovery": casualty_recovery,
            },
        })
    return activations


def latest_attempt(sweep_output: Path) -> Path:
    latest = read_json(sweep_output / "LATEST.json")
    attempt = sweep_output / latest["attempt"]
    if not (attempt / "COMPLETE.json").is_file():
        raise ValueError(f"sweep attempt is not complete: {attempt}")
    return attempt


def cell_attempt(sweep_attempt: Path, cell_id: str) -> Path:
    attempts = sorted((sweep_attempt / "cells" / cell_id).glob("attempt-*"))
    for attempt in reversed(attempts):
        if (attempt / "cell-result.json").is_file():
            return attempt
    raise FileNotFoundError(f"no accepted attempt for {cell_id}")


def analyze(args: argparse.Namespace) -> int:
    catalog = read_json(args.catalog)
    proof_path = resolve_repo_path(catalog["proofCatalog"])
    proof = read_json(proof_path)
    operation_by_id: dict[str, dict[str, Any]] = {}
    for card in proof["cards"]:
        sheet = read_json((proof_path.parent / card["sheet"]).resolve())
        plan = next(
            value for value in sheet["operations"]
            if value["id"] == card["id"]
        )
        operation_by_id[card["id"]] = {
            **card,
            "recoveryDeadlineTicks": plan["recovery"]["deadlineTicks"],
        }
    manifest = read_json(args.manifest)
    attempt = latest_attempt(args.sweep_output)
    cells: list[dict[str, Any]] = []
    for registered in manifest["cells"]:
        directory = cell_attempt(attempt, registered["cellId"])
        replay_path = directory / "replay.json.gz"
        scorecard_path = directory / "scorecard.json"
        replay = read_json(replay_path)
        scorecard = read_json(scorecard_path)
        team_id = int(registered["operationTeamId"])
        runtime_eligible = team_id in [
            int(value) for value in replay["result"]["eligibleTeamIds"]
        ]
        match_eligible = bool(
            scorecard["feltDegeneracy"]["matchEligibleForCohortRead"]
        ) and runtime_eligible
        activations = inspect_activations(
            operation_by_id[registered["operationId"]],
            replay,
            team_id,
            match_eligible,
        )
        cells.append({
            "cellId": registered["cellId"],
            "operationId": registered["operationId"],
            "operationTeamId": team_id,
            "opponentId": registered["opponentId"],
            "counterThesis": registered["counterThesis"],
            "seed": str(registered["seed"]),
            "canonicalReplayHash": replay["replayHash"],
            "replayFileSha256": sha256(replay_path),
            "scorecardFileSha256": sha256(scorecard_path),
            "runtimeEligible": runtime_eligible,
            "matchEligibleForCohortRead": match_eligible,
            "feltDegeneracy": scorecard["feltDegeneracy"],
            "activations": activations,
            "qualifyingSuccesses": sum(
                item["qualifies"]["success"] for item in activations
            ),
            "qualifyingCounters": sum(
                item["qualifies"]["counter"] for item in activations
            ),
            "qualifyingCommittedCounters": sum(
                item["qualifies"]["committedCounter"] for item in activations
            ),
            "qualifyingPreparationDenials": sum(
                item["qualifies"]["preparationDenial"] for item in activations
            ),
            "qualifyingCasualtyRecoveries": sum(
                item["qualifies"]["casualtyRecovery"] for item in activations
            ),
            "strandedActivations": sum(
                item["stranded"]
                for item in activations
            ),
            "matchEndPreemptedReleases": sum(
                item["releasePreemptedByMatchEnd"]
                for item in activations
            ),
            "activeAtMatchEnd": sum(
                item["terminalReason"] is None for item in activations
            ),
        })

    operations: list[dict[str, Any]] = []
    for operation in catalog["operations"]:
        rows = [cell for cell in cells if cell["operationId"] == operation["id"]]
        successes = [row["cellId"] for row in rows if row["qualifyingSuccesses"]]
        counters = [row["cellId"] for row in rows if row["qualifyingCounters"]]
        committed_counters = [
            row["cellId"] for row in rows
            if row["qualifyingCommittedCounters"]
        ]
        preparation_denials = [
            row["cellId"] for row in rows
            if row["qualifyingPreparationDenials"]
        ]
        casualties = [
            row["cellId"] for row in rows if row["qualifyingCasualtyRecoveries"]
        ]
        operations.append({
            "id": operation["id"],
            "cells": len(rows),
            "opponents": sorted({row["opponentId"] for row in rows}),
            "seeds": sorted({row["seed"] for row in rows}),
            "eligibleCells": sum(row["matchEligibleForCohortRead"] for row in rows),
            "activationCount": sum(len(row["activations"]) for row in rows),
            "successCells": successes,
            "counterCells": counters,
            "committedCounterCells": committed_counters,
            "preparationDenialCells": preparation_denials,
            "casualtyRecoveryCells": casualties,
            "strandedActivations": sum(row["strandedActivations"] for row in rows),
            "matchEndPreemptedReleases": sum(
                row["matchEndPreemptedReleases"] for row in rows
            ),
            "activeAtMatchEnd": sum(row["activeAtMatchEnd"] for row in rows),
            "requirementsMet": bool(successes and counters and casualties),
            "committedOnlyRequirementsMet": bool(
                successes and committed_counters and casualties
            ),
        })
    taxonomy_path = None
    taxonomy_sha256 = None
    if manifest.get("counterTaxonomy") is not None:
        taxonomy_path = resolve_repo_path(manifest["counterTaxonomy"])
        taxonomy_sha256 = sha256(taxonomy_path)
        if taxonomy_sha256 != manifest.get("counterTaxonomySha256"):
            raise ValueError("counter taxonomy moved after the sweep was frozen")
    result = {
        "schema": "arc-relay-operation-counterplay-read-v1",
        "catalog": str(args.catalog.resolve()),
        "catalogSha256": sha256(args.catalog),
        "manifest": str(args.manifest.resolve()),
        "manifestSha256": sha256(args.manifest),
        "sweepAttempt": str(attempt.resolve()),
        "runtime": manifest["runtime"],
        "counterTaxonomy": str(taxonomy_path) if taxonomy_path else None,
        "counterTaxonomySha256": taxonomy_sha256,
        "plannedCells": len(manifest["cells"]),
        "readCells": len(cells),
        "eligibleCells": sum(cell["matchEligibleForCohortRead"] for cell in cells),
        "populationOpponentsCovered": len({cell["opponentId"] for cell in cells}),
        "allRequirementsMet": all(row["requirementsMet"] for row in operations),
        "allCommittedOnlyRequirementsMet": all(
            row["committedOnlyRequirementsMet"] for row in operations
        ),
        "operations": operations,
        "cells": cells,
    }
    encoded = json.dumps(result, indent=2, sort_keys=True) + "\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(encoded, encoding="utf-8")
    print(
        f"{sum(row['requirementsMet'] for row in operations)}/"
        f"{len(operations)} operations have success+counter+casualty evidence; "
        f"{result['eligibleCells']}/{result['readCells']} cells eligible"
    )
    return 0 if result["allRequirementsMet"] else 1


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--sweep-output", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return analyze(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
