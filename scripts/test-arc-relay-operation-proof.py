#!/usr/bin/env python3

from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


ROOT = Path(__file__).resolve().parent.parent
MODULE_PATH = ROOT / "scripts/arc-relay-operation-proof.py"
SPEC = importlib.util.spec_from_file_location("operation_proof", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
PROOF = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PROOF)


class ArcRelayOperationProofTests(unittest.TestCase):
    def test_complete_activation_with_action_and_baseline_release_passes(self) -> None:
        result = self.inspect(required_actions=["signature"])

        self.assertTrue(result["passed"])
        self.assertEqual(3, result["proof"]["successTick"])
        self.assertEqual([2], result["proof"]["requiredActionTicks"]["signature"])
        self.assertEqual({0: "a-baseline"}, result["proof"]["baselineRoleTags"])

    def test_success_without_required_action_does_not_qualify(self) -> None:
        result = self.inspect(required_actions=["missing"])

        self.assertFalse(result["passed"])
        self.assertEqual("no complete qualifying activation", result["failure"])
        self.assertEqual(
            ["successful activation omitted required action missing"],
            result["qualificationFailures"][0]["reasons"],
        )

    def inspect(self, required_actions: list[str]) -> dict:
        replay = {
            "replayHash": "proof-hash",
            "result": {"eligibleTeamIds": [0], "winnerTeamId": 0},
            "ticks": [
                self.turn(
                    1,
                    "test-op=prepare[evidence-and-actors|c=0:pair]",
                    [{"unitId": 0, "roleTag": "g-prepare"}],
                ),
                self.turn(
                    2,
                    "test-op=commit/go[branch-go|c=0:pair]",
                    [{"unitId": 0, "roleTag": "g-commit", "actionId": "signature"}],
                ),
                self.turn(
                    3,
                    "test-op=recover/go[mission-success|c=0:extract]",
                    [{"unitId": 0, "roleTag": "g-recover"}],
                ),
                self.turn(
                    4,
                    "test-op=dormant[recovery-complete|c=-]",
                    [{"unitId": 0, "roleTag": "a-baseline"}],
                ),
            ],
        }
        card = {"id": "test-op", "requiredActionIds": required_actions}
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "replay.json"
            path.write_text(json.dumps(replay), encoding="utf-8")
            return PROOF.inspect_card(card, path, team_id=0)

    @staticmethod
    def turn(tick: int, debug: str, commands: list[dict]) -> dict:
        return {
            "tick": tick,
            "mindTurns": [
                {
                    "tick": tick,
                    "teamId": 0,
                    "debugMessage": debug,
                    "commands": commands,
                }
            ],
        }


if __name__ == "__main__":
    unittest.main()
