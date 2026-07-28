#!/usr/bin/env python3
"""Tests for generic Frontline replay-v3 descriptive evaluation."""

from __future__ import annotations

import copy
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "scripts" / "labs-replay-eval.py"
FIXTURE = (
    ROOT
    / "tests"
    / "BotArena.Engine.Tests"
    / "Fixtures"
    / "generic-frontline-replay-v3.json"
)
SPEC = importlib.util.spec_from_file_location("labs_replay_eval", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Could not load {SCRIPT}")
EVALUATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(EVALUATOR)


class LabsReplayEvalTests(unittest.TestCase):
    def setUp(self) -> None:
        self.document = json.loads(FIXTURE.read_text(encoding="utf-8"))

    def test_frontline_v3_fixture_emits_identity_and_dynamics(self) -> None:
        row = EVALUATOR.analyze_replay(
            self.document,
            source="fixture",
            group="baseline",
        )

        self.assertEqual(3, self.document["header"]["replayVersion"])
        self.assertEqual(1, row["duration"]["ticks"])
        self.assertEqual(0, row["result"]["winnerTeamId"])
        self.assertEqual(
            "generic-frontline-replay-v3-arena",
            row["identity"]["mapId"],
        )
        self.assertEqual({"wait": 2}, row["actions"]["submitted"])
        self.assertEqual(0, row["safety"]["runtimeFaultEvents"])
        self.assertEqual(0, row["mechanics"]["anchor"]["completions"])
        self.assertEqual(1, row["objective"]["soleControlTicks"])
        self.assertEqual(0, row["activity"]["longestNoInteractionRunTicks"])

        summary = EVALUATOR.summarize_group("baseline", [row])
        self.assertEqual(1, summary["matches"])
        self.assertEqual({"team-0": 1}, summary["outcomes"])
        self.assertEqual(["in-process"], summary["cohort"]["runtimeClasses"])

    def test_partial_and_non_frontline_replays_are_rejected(self) -> None:
        partial = copy.deepcopy(self.document)
        partial["partial"] = True
        with self.assertRaisesRegex(ValueError, "partial must be false"):
            EVALUATOR.analyze_replay(partial)

        deathmatch = copy.deepcopy(self.document)
        deathmatch["header"]["contract"]["rules"]["gameMode"]["kind"] = (
            "deathmatch"
        )
        with self.assertRaisesRegex(ValueError, "must be frontline"):
            EVALUATOR.analyze_replay(deathmatch)

    def test_group_rejects_rules_and_runtime_class_mixture(self) -> None:
        first = EVALUATOR.analyze_replay(self.document)
        changed_rules = copy.deepcopy(first)
        changed_rules["identity"]["rulesFingerprint"] = "different"
        with self.assertRaisesRegex(ValueError, "mixes rules fingerprints"):
            EVALUATOR.summarize_group(
                "mixed-rules",
                [first, changed_rules],
            )

        changed_runtime = copy.deepcopy(first)
        changed_runtime["identity"]["participants"][0]["runtimeKind"] = (
            "wasm-generic-actor"
        )
        changed_runtime["identity"]["participants"][1]["runtimeKind"] = (
            "wasm-generic-actor"
        )
        with self.assertRaisesRegex(ValueError, "mixes runtime classes"):
            EVALUATOR.summarize_group(
                "mixed-runtime",
                [first, changed_runtime],
            )

    def test_faulted_turn_without_submitted_decision_is_counted_safely(
        self,
    ) -> None:
        faulted = copy.deepcopy(self.document)
        turn = faulted["ticks"][0]["actorTurns"][0]
        turn["submittedDecision"] = None
        turn["actionResolution"]["submittedAction"] = None
        turn["actionResolution"]["outcome"] = "faulted"
        turn["actionResolution"]["runtimeFault"] = {
            "participantId": 10,
            "actorId": {
                "teamId": 0,
                "unitId": 0,
                "lifeId": 0,
            },
            "stage": "tick-execution",
            "faultCode": "tick-execution-failed",
            "cumulativeFaultCount": "1",
            "disqualificationTriggered": True,
        }

        row = EVALUATOR.analyze_replay(faulted)

        self.assertEqual({"wait": 1}, row["actions"]["submitted"])
        self.assertEqual(1, row["actions"]["successful"]["wait"])

        invalid = copy.deepcopy(self.document)
        invalid["ticks"][0]["actorTurns"][0]["submittedDecision"] = None
        with self.assertRaisesRegex(
            ValueError,
            "may be null only for a faulted turn",
        ):
            EVALUATOR.analyze_replay(invalid)

    def test_json_report_uses_group_relative_source_paths(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            replay = root / "matches" / "one" / "attempt-01" / "replay.json"
            replay.parent.mkdir(parents=True)
            replay.write_text(
                json.dumps(self.document),
                encoding="utf-8",
            )
            report_path = root / "report.json"

            completed = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    "--group",
                    f"baseline={root / 'matches'}",
                    "--json",
                    str(report_path),
                ],
                cwd=ROOT,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(0, completed.returncode, completed.stderr)
            report = json.loads(report_path.read_text(encoding="utf-8"))
            self.assertEqual("replay.json", report["matches"][0]["source"])
            self.assertFalse(
                Path(report["matches"][0]["source"]).is_absolute()
            )


if __name__ == "__main__":
    unittest.main()
