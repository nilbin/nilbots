#!/usr/bin/env python3
"""Fixture tests for scripts/frontline-replay-eval.py."""

from __future__ import annotations

import contextlib
import copy
import importlib.util
import io
import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "scripts" / "frontline-replay-eval.py"
FIXTURE = ROOT / "web" / "tests" / "fixtures" / "frontline-replay-v2.json"
SPEC = importlib.util.spec_from_file_location(
    "frontline_replay_eval",
    SCRIPT,
)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Could not load {SCRIPT}")
EVALUATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(EVALUATOR)


class FrontlineReplayEvalTests(unittest.TestCase):
    def setUp(self) -> None:
        self.document = json.loads(FIXTURE.read_text(encoding="utf-8"))

    def test_engine_fixture_pins_mechanic_metrics(self) -> None:
        row = EVALUATOR.analyze_replay(
            self.document,
            source="fixture",
            group="fixture",
        )

        self.assertEqual(12, row["durationTicks"])
        self.assertEqual(2.4, row["durationSeconds"])
        self.assertEqual("late", row["endingPhase"])
        self.assertEqual(2, row["fabricationQueues"])
        self.assertEqual(2, row["fabricatedBirths"])
        self.assertEqual(2, row["anchorCompletions"])
        self.assertEqual(4, row["turretActorTicks"])
        self.assertEqual(2, row["turretShots"])
        self.assertEqual(4, row["peakSimultaneousActiveBodies"])
        self.assertEqual(0, row["stagnantTicks"])
        self.assertEqual(0, row["teamActorlessTicks"])
        self.assertEqual(0.0, row["teamActorlessShare"])
        self.assertEqual(
            {
                "fabricate": 2,
                "move-forward": 9,
                "shoot-direction": 2,
                "transform": 2,
                "turn-left": 2,
                "turn-right": 2,
                "wait": 25,
            },
            row["validatedActions"],
        )
        self.assertEqual(
            [0, 0],
            [
                team["firstFabricationQueueLatencyTicks"]
                for team in row["teams"]
            ],
        )

    def test_partial_and_noncontiguous_documents_are_rejected(self) -> None:
        partial = copy.deepcopy(self.document)
        partial["partial"] = True
        partial["result"] = None
        with self.assertRaisesRegex(ValueError, "partial must be false"):
            EVALUATOR.analyze_replay(partial)

        gapped = copy.deepcopy(self.document)
        gapped["ticks"][3]["tick"] = 99
        with self.assertRaisesRegex(ValueError, "must be contiguous"):
            EVALUATOR.analyze_replay(gapped)

    def test_rebuild_latency_and_turret_projectile_attribution_are_exact(
        self,
    ) -> None:
        document = copy.deepcopy(self.document)
        events = [
            event
            for tick in document["ticks"]
            for event in tick["resolution"]["events"]
        ]
        initial_queue = next(
            event
            for event in events
            if event["type"] == "fabrication-queued"
        )
        rebuild_queue = copy.deepcopy(initial_queue)
        rebuild_queue["tick"] = document["result"]["endTick"]
        rebuild_queue["spawnReason"] = "rebuild"
        document["ticks"][-1]["resolution"]["events"].append(rebuild_queue)

        turret_shot = next(
            event
            for event in events
            if event["type"] == "shot"
            and event["actionId"] == "shoot-direction"
        )
        document["ticks"][-1]["resolution"]["events"].extend(
            [
                {
                    "type": "damage",
                    "tick": document["result"]["endTick"],
                    "amount": 2,
                    "sourceActorId": turret_shot["sourceActorId"],
                    "projectileId": turret_shot["projectileId"],
                },
                {
                    "type": "destroyed",
                    "tick": document["result"]["endTick"],
                    "sourceActorId": turret_shot["sourceActorId"],
                    "projectileId": turret_shot["projectileId"],
                },
            ]
        )

        row = EVALUATOR.analyze_replay(document)

        self.assertEqual(3, row["fabricationQueues"])
        self.assertEqual([0, 0], row["fabricationQueueLatencyTicks"])
        self.assertEqual(1, row["turretDamageEvents"])
        self.assertEqual(2, row["turretDamage"])
        self.assertEqual(1, row["turretKills"])

    def test_cli_writes_a_versioned_deterministic_report(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "report.json"
            with contextlib.redirect_stdout(io.StringIO()):
                exit_code = EVALUATOR.main(
                    [
                        "--group",
                        f"fixture={FIXTURE}",
                        "--json",
                        str(output),
                    ]
                )
            report = json.loads(output.read_text(encoding="utf-8"))

        self.assertEqual(0, exit_code)
        self.assertEqual(1, report["schemaVersion"])
        self.assertEqual(
            "frontline-replay-v2-1",
            report["metricDefinitionsVersion"],
        )
        self.assertIn("descriptive", report["evidenceClass"])
        self.assertNotIn("funScore", report)
        self.assertEqual(1, report["groups"][0]["matches"])
        self.assertEqual(
            "frontline-replication-test",
            report["groups"][0]["cohort"]["rulesVersion"],
        )
        self.assertEqual(
            2.4,
            report["groups"][0]["duration"]["medianSeconds"],
        )

    def test_group_summary_rejects_mixed_rules_fingerprints(self) -> None:
        first = EVALUATOR.analyze_replay(self.document)
        second = copy.deepcopy(first)
        second["rulesFingerprint"] = "different"

        with self.assertRaisesRegex(ValueError, "mixes rules cohorts"):
            EVALUATOR.summarize_group("mixed", [first, second])


if __name__ == "__main__":
    unittest.main()
