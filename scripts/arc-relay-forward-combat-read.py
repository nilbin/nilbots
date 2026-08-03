#!/usr/bin/env python3
"""Reduce the pre-registered forward-combat sweep under its dominance gates."""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import gzip
import hashlib
import json
from pathlib import Path
from typing import Any


REPO = Path(__file__).resolve().parent.parent


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path}: expected a JSON object")
    return value


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(REPO))
    except ValueError:
        return str(path)


def rate(numerator: int, denominator: int) -> float | None:
    return numerator / denominator if denominator else None


def team_bool(values: dict[str, Any], team_id: int) -> bool:
    return bool(values.get(str(team_id), values.get(team_id, False)))


def action_id(turn: list[Any]) -> str:
    validated = turn[5]
    return str(validated[0])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--attempt", required=True, type=Path)
    parser.add_argument("--registration", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    attempt = args.attempt.resolve()
    registration_path = args.registration.resolve()
    registration = read_json(registration_path)
    run_path = attempt / "RUN.json"
    results_path = attempt / "results.json"
    run = read_json(run_path)
    results = read_json(results_path)
    plan_path = Path(run["manifest"]).resolve()
    plan = read_json(plan_path)
    cells = {cell["cellId"]: cell for cell in plan["cells"]}
    result_rows = {row["cellId"]: row for row in results["cells"]}
    if set(result_rows) != set(cells):
        missing = sorted(set(cells) - set(result_rows))
        extra = sorted(set(result_rows) - set(cells))
        raise ValueError(f"incomplete sweep; missing={missing}, extra={extra}")
    expected_cells = int(registration["sampling"]["cells"])
    if len(cells) != expected_cells:
        raise ValueError(
            f"registration requires {expected_cells} cells; got {len(cells)}")
    if plan["loopProfile"] != registration["candidate"]["loopProfile"]:
        raise ValueError("plan loop profile does not match registration")
    if plan["runtime"] != registration["candidate"]["runtime"]:
        raise ValueError("plan runtime does not match registration")

    entrant_records: dict[str, Counter[str]] = defaultdict(Counter)
    class_records: dict[str, Counter[str]] = defaultdict(Counter)
    handling_exposure: dict[str, dict[str, Counter[str]]] = defaultdict(
        lambda: {"high": Counter(), "other": Counter()})
    class_turns: Counter[str] = Counter()
    class_combat: Counter[str] = Counter()
    handling_actions: dict[str, Counter[str]] = defaultdict(Counter)
    action_kinds: Counter[str] = Counter()
    canonical_hashes: set[str] = set()
    ineligible_sides: list[dict[str, Any]] = []
    runtime_faults = 0
    verified = 0
    draws = 0

    for cell_id, result in sorted(result_rows.items()):
        cell_dir = attempt / result["attempt"]
        record = read_json(cell_dir / "match-record.json")
        scorecard = read_json(cell_dir / "scorecard.json")
        with gzip.open(cell_dir / "broadcast.json.gz", "rt", encoding="utf-8") as stream:
            broadcast = json.load(stream)
        if result["verified"]:
            verified += 1
        canonical_hashes.add(record["canonicalReplay"]["hash"])
        if broadcast["canonicalReplayHash"] != record["canonicalReplay"]["hash"]:
            raise ValueError(f"{cell_id}: broadcast canonical hash mismatch")
        if broadcast["header"]["gameRulesVersion"] != plan["rulesetId"]:
            raise ValueError(f"{cell_id}: unexpected ruleset")
        outcome = scorecard["outcome"]
        winner = outcome["winnerTeamId"]
        if winner is None:
            draws += 1
        for runtime in outcome["runtimeByParticipant"].values():
            runtime_faults += int(runtime["runtimeFaultCount"])

        participants = {
            int(participant["teamId"]): participant
            for participant in record["participants"]
        }
        topology = broadcast["header"]["contract"]["topology"]
        class_by_slot = {
            (int(slot["teamId"]), int(slot["unitId"])): slot["classId"]
            for slot in topology["unitSlots"]
        }
        rules = broadcast["header"]["contract"]["rules"]
        form_handling = {
            form["id"]: form["movementProfileId"] for form in rules["forms"]
        }
        handling_by_class = {
            class_id: form_handling[f"arc-body-{class_id}"]
            for class_id in set(class_by_slot.values())
        }
        kind_by_action = {
            action["id"]: action["kind"] for action in rules["actions"]
        }

        for team_id, participant in sorted(participants.items()):
            entrant = participant["entrantId"]
            record_row = entrant_records[entrant]
            record_row["games"] += 1
            if winner is None:
                record_row["draws"] += 1
            elif winner == team_id:
                record_row["wins"] += 1
            else:
                record_row["losses"] += 1
            eligible = team_bool(
                scorecard["feltDegeneracy"]["cohortEligibilityByTeam"],
                team_id,
            )
            if not eligible:
                ineligible_sides.append({
                    "cellId": cell_id,
                    "teamId": team_id,
                    "entrantId": entrant,
                })

            composition = Counter(participant["classes"])
            for class_id, copies in sorted(composition.items()):
                row = class_records[class_id]
                row["teamMatchAppearances"] += 1
                row["fieldedCopies"] += copies
                if winner is None:
                    row["draws"] += 1
                elif winner == team_id:
                    row["wins"] += 1
                else:
                    row["losses"] += 1
            handling_counts = Counter(
                handling_by_class[class_id]
                for class_id in participant["classes"]
            )
            for handling in sorted(set(handling_by_class.values())):
                bucket = "high" if handling_counts[handling] >= 4 else "other"
                row = handling_exposure[handling][bucket]
                row["teamMatchAppearances"] += 1
                row["fieldedBodies"] += handling_counts[handling]
                if winner is None:
                    row["draws"] += 1
                elif winner == team_id:
                    row["wins"] += 1
                else:
                    row["losses"] += 1

        for tick_turns in broadcast["turns"]:
            for turn in tick_turns:
                team_id, unit_id, _life_id = turn[0]
                class_id = class_by_slot[(int(team_id), int(unit_id))]
                handling = handling_by_class[class_id]
                class_turns[class_id] += 1
                action = action_id(turn)
                kind = kind_by_action[action]
                action_kinds[kind] += 1
                handling_actions[handling][action] += 1
                if kind in ("attack", "signature") and turn[6] == "success":
                    class_combat[class_id] += 1

    decided = len(cells) - draws
    winning_sheets = sum(row["wins"] > 0 for row in entrant_records.values())
    zero_win_sheets = sorted(
        entrant for entrant, row in entrant_records.items() if row["wins"] == 0)
    leader_wins = max(row["wins"] for row in entrant_records.values())
    leaders = sorted(
        entrant for entrant, row in entrant_records.items()
        if row["wins"] == leader_wins)
    total_turns = sum(class_turns.values())
    total_combat = sum(class_combat.values())

    class_output: dict[str, Any] = {}
    class_gate_pass = True
    class_gate_limit = float(registration["hardGates"][
        "maximumClassFieldedTeamWinRateWithAtLeastTenAppearances"])
    combat_ratio_limit = float(registration["hardGates"][
        "maximumClassCombatShareToTurnShareRatio"])
    for class_id in sorted(class_records):
        row = class_records[class_id]
        appearances = row["teamMatchAppearances"]
        win_rate = rate(row["wins"], appearances)
        turn_share = rate(class_turns[class_id], total_turns) or 0.0
        combat_share = rate(class_combat[class_id], total_combat) or 0.0
        combat_ratio = combat_share / turn_share if turn_share else None
        win_gate = appearances < 10 or win_rate <= class_gate_limit
        ratio_gate = combat_ratio is None or combat_ratio <= combat_ratio_limit
        class_gate_pass = class_gate_pass and win_gate and ratio_gate
        class_output[class_id] = {
            **dict(row),
            "fieldedTeamWinRate": win_rate,
            "turns": class_turns[class_id],
            "turnShare": turn_share,
            "successfulCombatActions": class_combat[class_id],
            "combatShare": combat_share,
            "combatShareToTurnShareRatio": combat_ratio,
            "gates": {
                "fieldedTeamWinRate": win_gate,
                "combatShareToTurnShareRatio": ratio_gate,
            },
        }

    exposure_output: dict[str, Any] = {}
    max_exposure_delta = float(registration["hardGates"][
        "maximumHandlingHighExposureWinRateDelta"])
    exposure_gate_pass = True
    for handling, buckets in sorted(handling_exposure.items()):
        high = buckets["high"]
        other = buckets["other"]
        high_rate = rate(high["wins"], high["teamMatchAppearances"])
        other_rate = rate(other["wins"], other["teamMatchAppearances"])
        delta = (
            abs(high_rate - other_rate)
            if high_rate is not None and other_rate is not None
            else None
        )
        enough = high["teamMatchAppearances"] >= 10
        passed = enough and delta is not None and delta <= max_exposure_delta
        exposure_gate_pass = exposure_gate_pass and passed
        exposure_output[handling] = {
            "highExposureDefinition": "at least four of eight fielded bodies",
            "high": {**dict(high), "winRate": high_rate},
            "other": {**dict(other), "winRate": other_rate},
            "absoluteWinRateDelta": delta,
            "minimumHighExposureSampleMet": enough,
            "gatePassed": passed,
        }

    deliberate = handling_actions["deliberate"]
    deliberate_rotation_denominator = (
        deliberate["rotate"] + deliberate["move-eight-way"])
    deliberate_rotation_share = rate(
        deliberate["rotate"], deliberate_rotation_denominator)
    swift = handling_actions["swift"]
    swift_movement = swift["move-eight-way"] + swift["strafe-eight-way"]
    swift_strafe_share = rate(swift["strafe-eight-way"], swift_movement)
    swift_turn_share = rate(swift["move-eight-way"], swift_movement)

    hard = registration["hardGates"]
    gate_results = {
        "runtimeFaults": runtime_faults == int(hard["runtimeFaults"]),
        "canonicalReplayVerification": verified == expected_cells,
        "cohortEligibility": not ineligible_sides,
        "drawRate": rate(draws, len(cells)) <= float(hard["drawRateAtMost"]),
        "minimumWinningSheets": winning_sheets >= int(hard["minimumWinningSheets"]),
        "leaderShareOfDecidedWins": (
            rate(leader_wins, decided)
            <= float(hard["leaderShareOfDecidedWinsAtMost"])
        ),
        "noZeroWinSheet": not zero_win_sheets,
        "classDominance": class_gate_pass,
        "handlingHighExposure": exposure_gate_pass,
        "deliberateRotationShare": (
            deliberate_rotation_share is not None
            and deliberate_rotation_share
            <= float(hard["maximumDeliberateRotationShare"])
        ),
        "swiftStrafeShare": (
            swift_strafe_share is not None
            and swift_strafe_share
            <= float(hard["maximumSwiftStrafeShareOfSwiftMovement"])
        ),
        "swiftTurnWithMoveShare": (
            swift_turn_share is not None
            and swift_turn_share
            >= float(hard["minimumSwiftTurnWithMoveShareOfSwiftMovement"])
        ),
    }
    output = {
        "schema": "arc-relay-forward-combat-read-v1",
        "classification": (
            "representative shared-mind implementation and dominance gate; "
            "not a causal class-balance or human-fun claim"
        ),
        "authority": {
            "runtime": plan["runtime"],
            "loopProfile": plan["loopProfile"],
            "rulesetId": plan["rulesetId"],
            "registration": display_path(registration_path),
            "registrationSha256": sha256(registration_path),
            "plan": display_path(plan_path),
            "planSha256": sha256(plan_path),
            "resultsSha256": sha256(results_path),
            "canonicalReplayHashes": len(canonical_hashes),
        },
        "sample": {
            "matches": len(cells),
            "verified": verified,
            "eligibleMatches": len(cells) - len({
                row["cellId"] for row in ineligible_sides}),
            "ineligibleSides": ineligible_sides,
            "runtimeFaults": runtime_faults,
        },
        "sheetBalance": {
            "records": {
                entrant: dict(row)
                for entrant, row in sorted(entrant_records.items())
            },
            "draws": draws,
            "drawRate": rate(draws, len(cells)),
            "decidedMatches": decided,
            "winningSheets": winning_sheets,
            "zeroWinSheets": zero_win_sheets,
            "leaders": leaders,
            "leaderWins": leader_wins,
            "leaderShareOfDecidedWins": rate(leader_wins, decided),
        },
        "classRead": class_output,
        "handlingRead": {
            "highExposure": exposure_output,
            "successfulValidatedActions": {
                handling: dict(sorted(actions.items()))
                for handling, actions in sorted(handling_actions.items())
            },
            "deliberateRotationShare": deliberate_rotation_share,
            "swiftStrafeShareOfMovement": swift_strafe_share,
            "swiftTurnWithMoveShareOfMovement": swift_turn_share,
        },
        "actionKinds": dict(sorted(action_kinds.items())),
        "gates": gate_results,
        "allHardGatesPass": all(gate_results.values()),
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(output, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(args.output)
    print(json.dumps({
        "allHardGatesPass": output["allHardGatesPass"],
        "gates": gate_results,
    }, sort_keys=True))
    return 0 if output["allHardGatesPass"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
