#!/usr/bin/env python3
"""Focused checks for the Arc Relay operation counterplay trace reader."""

from __future__ import annotations

import importlib.util
from pathlib import Path
import unittest


SCRIPT = Path(__file__).with_name("arc-relay-operation-counterplay.py")
SPEC = importlib.util.spec_from_file_location("operation_counterplay", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
READ = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(READ)


def command(unit: int, life: int, role: str, action: str = "wait") -> dict:
    return {
        "unitId": unit,
        "lifeId": life,
        "roleTag": role,
        "actionId": action,
    }


def turn(tick: int, state: str, commands: list[dict]) -> dict:
    return {
        "tick": tick,
        "teamId": 0,
        "debugMessage": f"audit; demo={state}",
        "commands": commands,
    }


def replay(turns: list[dict], events: dict[int, list[dict]] | None = None) -> dict:
    by_tick = {item["tick"]: item for item in turns}
    end = max(by_tick)
    return {
        "ticks": [
            {
                "tick": tick,
                "mindTurns": [by_tick[tick]],
                "events": (events or {}).get(tick, []),
            }
            for tick in range(1, end + 1)
        ],
        "result": {"endTick": end, "eligibleTeamIds": [0, 1]},
    }


class OperationCounterplayTests(unittest.TestCase):
    def test_success_requires_action_release_and_baseline(self) -> None:
        data = replay([
            turn(1, "prepare[evidence-and-actors|c=4:a,5:b|e=always:t]", [
                command(4, 0, "g-d-p-a"), command(5, 0, "g-d-p-b")]),
            turn(2, "commit/main[branch-main|c=4:x,5:y|e=always:t]", [
                command(4, 0, "g-d-c-x", "tractor-hook"),
                command(5, 0, "g-d-c-y")]),
            turn(3, "recover/main[mission-success|c=4:r,5:r|e=goal:t]", [
                command(4, 0, "g-d-r-r"), command(5, 0, "g-d-r-r")]),
            turn(4, "dormant[recovery-complete|c=-|e=always:t]", [
                command(4, 0, "base-a"), command(5, 0, "base-b")]),
        ])
        activations = READ.inspect_activations(
            {"id": "demo", "requiredActionIds": ["tractor-hook"]},
            data,
            0,
            True,
        )
        self.assertEqual(1, len(activations))
        self.assertTrue(activations[0]["qualifies"]["success"])
        self.assertTrue(activations[0]["baselineRelease"])

    def test_hostile_casualty_abort_recovers_and_respawns_to_baseline(self) -> None:
        destruction = {
            "kind": "destruction",
            "payload": {
                "actorId": {"teamId": 0, "unitId": 4, "lifeId": 0},
                "sourceTeamId": 1,
                "sourceActorId": {"teamId": 1, "unitId": 2, "lifeId": 0},
            },
        }
        data = replay([
            turn(1, "prepare[evidence-and-actors|c=4:a,5:b|e=always:t]", [
                command(4, 0, "g-d-p-a"), command(5, 0, "g-d-p-b")]),
            turn(2, "commit/main[branch-main|c=4:x,5:y|e=always:t]", [
                command(4, 0, "g-d-c-x"), command(5, 0, "g-d-c-y")]),
            turn(3, "recover/main[commit-participant-minimum|c=5:r|e=always:f]", [
                command(5, 0, "g-d-r-r")]),
            turn(4, "dormant[recovery-complete|c=-|e=always:t]", [
                command(5, 0, "base-survivor")]),
            turn(5, "dormant[cooldown-until-20|c=-|e=always:t]", [
                command(4, 1, "base-respawn"), command(5, 0, "base-survivor")]),
        ], {3: [destruction]})
        activation = READ.inspect_activations(
            {"id": "demo", "requiredActionIds": []},
            data,
            0,
            True,
        )[0]
        self.assertTrue(activation["qualifies"]["counter"])
        self.assertTrue(activation["qualifies"]["casualtyRecovery"])
        self.assertTrue(activation["casualtyRespawnBaseline"])

    def test_abort_without_hostile_contact_is_not_a_counter(self) -> None:
        data = replay([
            turn(1, "prepare[evidence-and-actors|c=4:a|e=always:t]", [
                command(4, 0, "g-d-p-a")]),
            turn(2, "commit/main[branch-main|c=4:x|e=always:t]", [
                command(4, 0, "g-d-c-x")]),
            turn(3, "recover/main[mission-deadline|c=4:r|e=always:f]", [
                command(4, 0, "g-d-r-r")]),
            turn(4, "dormant[recovery-complete|c=-|e=always:t]", [
                command(4, 0, "base-a")]),
        ])
        activation = READ.inspect_activations(
            {"id": "demo", "requiredActionIds": []},
            data,
            0,
            True,
        )[0]
        self.assertFalse(activation["qualifies"]["counter"])

    def test_hostile_preparation_casualty_is_a_distinct_denial_counter(self) -> None:
        destruction = {
            "kind": "destruction",
            "payload": {
                "actorId": {"teamId": 0, "unitId": 4, "lifeId": 0},
                "sourceTeamId": 1,
                "sourceActorId": {"teamId": 1, "unitId": 2, "lifeId": 0},
            },
        }
        data = replay([
            turn(1, "prepare[evidence-and-actors|c=4:a,5:b|e=always:t]", [
                command(4, 0, "g-d-p-a"), command(5, 0, "g-d-p-b")]),
            turn(2, "recover[prepare-participant-minimum|c=5:r|e=always:f]", [
                command(5, 0, "g-d-r-r")]),
            turn(3, "dormant[recovery-complete|c=-|e=always:t]", [
                command(5, 0, "base-survivor")]),
            turn(4, "dormant[cooldown-until-20|c=-|e=always:t]", [
                command(4, 1, "base-respawn"), command(5, 0, "base-survivor")]),
        ], {2: [destruction]})
        activation = READ.inspect_activations(
            {"id": "demo", "requiredActionIds": []},
            data,
            0,
            True,
        )[0]
        self.assertTrue(activation["qualifies"]["preparationDenial"])
        self.assertFalse(activation["qualifies"]["committedCounter"])
        self.assertTrue(activation["qualifies"]["counter"])

    def test_match_end_before_recovery_deadline_is_not_stranding(self) -> None:
        data = replay([
            turn(1, "prepare[evidence-and-actors|c=4:a|e=always:t]", [
                command(4, 0, "g-d-p-a")]),
            turn(2, "commit/main[branch-main|c=4:x|e=always:t]", [
                command(4, 0, "g-d-c-x")]),
            turn(3, "recover/main[mission-success|c=4:r|e=goal:t]", [
                command(4, 0, "g-d-r-r")]),
        ])
        activation = READ.inspect_activations(
            {
                "id": "demo",
                "requiredActionIds": [],
                "recoveryDeadlineTicks": 12,
            },
            data,
            0,
            True,
        )[0]
        self.assertTrue(activation["releasePreemptedByMatchEnd"])
        self.assertFalse(activation["stranded"])


if __name__ == "__main__":
    unittest.main()
