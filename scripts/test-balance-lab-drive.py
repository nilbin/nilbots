#!/usr/bin/env python3
"""Tests for the mode-independent Nilbots Balance Lab driver."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "scripts" / "balance-lab-drive.py"
FIXTURE = (
    ROOT
    / "tests"
    / "BotArena.Engine.Tests"
    / "Fixtures"
    / "generic-frontline-replay-v3.json"
)
SPEC = importlib.util.spec_from_file_location("balance_lab_drive", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"could not load {SCRIPT}")
DRIVER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(DRIVER)


class BalanceLabDriveTests(unittest.TestCase):
    def test_factorial_plan_covers_candidates_populations_and_mirrors(
        self,
    ) -> None:
        spec = {
            "pairedSeeds": [7, 11],
            "candidates": [
                {"id": "a"},
                {"id": "b"},
                {"id": "c"},
                {"id": "d"},
            ],
            "populations": [
                {
                    "id": "t-two",
                    "tier": "T2",
                    "coordinationGrade": "C0",
                    "entrants": [
                        {"id": "one"},
                        {"id": "two"},
                        {"id": "three"},
                    ],
                },
            ],
        }

        plan = DRIVER.build_plan(spec)

        self.assertEqual(48, len(plan))
        self.assertEqual(
            {"a", "b", "c", "d"},
            {item["candidateId"] for item in plan},
        )
        self.assertIn(
            ("one", "two", 7),
            {
                (item["bot"], item["opponent"], item["seed"])
                for item in plan
            },
        )
        self.assertIn(
            ("two", "one", 7),
            {
                (item["bot"], item["opponent"], item["seed"])
                for item in plan
            },
        )

    def test_small_matrix_runs_verifies_and_writes_vector_report(self) -> None:
        fixture = json.loads(FIXTURE.read_text(encoding="utf-8"))
        contract = fixture["header"]["contract"]
        rules = contract["rules"]
        map_contract = contract["map"]
        format_contract = contract["format"]
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            entrants = []
            for entrant_id in ("alpha", "beta"):
                entrant_root = root / entrant_id
                entrant_root.mkdir()
                (entrant_root / "Bot.cs").write_text(
                    f"// {entrant_id}\n",
                    encoding="utf-8",
                )
                artifact = entrant_root / "bot.wasm"
                artifact.write_bytes(entrant_id.encode("utf-8"))
                entrants.append(
                    {
                        "id": entrant_id,
                        "name": entrant_id.title(),
                        "root": entrant_id,
                        "artifact": f"{entrant_id}/bot.wasm",
                        "artifactSha256": hashlib.sha256(
                            entrant_id.encode("utf-8")
                        ).hexdigest(),
                        "sourceTreeSha256":
                            DRIVER.COHORT._source_tree_sha256(
                                entrant_root
                            ),
                        "qualification": {
                            "suite": "test",
                            "tierAwarded": "T2",
                        },
                    }
                )

            helper = root / "fixture-runner.py"
            helper.write_text(
                """
import hashlib, json, pathlib, sys
fixture, bot, opponent, seed, output = sys.argv[1:]
document = json.loads(pathlib.Path(fixture).read_text())
document["header"]["seed"] = str(seed)
participants = document["header"]["provenance"]["participants"]
for item, artifact in zip(participants, (bot, opponent)):
    item["artifactHash"] = hashlib.sha256(
        pathlib.Path(artifact).read_bytes()
    ).hexdigest()
    item["runtimeKind"] = "wasm-generic-actor"
target = pathlib.Path(output)
target.mkdir(parents=True, exist_ok=True)
(target / "replay.json").write_text(json.dumps(document))
""".strip()
                + "\n",
                encoding="utf-8",
            )
            verify = root / "verify.py"
            verify.write_text(
                "raise SystemExit(0)\n",
                encoding="utf-8",
            )
            runner = (
                f"{DRIVER.sys.executable} {helper} {FIXTURE} "
                "{bot} {opponent} {seed} {out}"
            )
            spec_path = root / "spec.json"
            spec_path.write_text(
                json.dumps(
                    {
                        "schemaVersion": 1,
                        "experimentId": "test-balance-lab",
                        "status": "experimental",
                        "evidenceClass": "infrastructure-smoke",
                        "hypothesis": "The matrix is reproducible.",
                        "factors": {
                            "map-topology": ["fixture"],
                            "companion-policy": ["manual"],
                        },
                        "pairedSeeds": [7],
                        "holdoutSeeds": [13],
                        "verifyCommand":
                            f"{DRIVER.sys.executable} {verify} "
                            "{replay}",
                        "dynamicsAdapter":
                            "generic-frontline-replay-v3",
                        "candidates": [
                            {
                                "id": "fixture-candidate",
                                "factors": {
                                    "map-topology": "fixture",
                                    "companion-policy": "manual",
                                },
                                "runnerCommand": runner,
                                "contract": {
                                    "modeId":
                                        rules["gameMode"]["modeId"],
                                    "rulesetId": rules["rulesetId"],
                                    "rulesFingerprint":
                                        rules["rulesFingerprint"],
                                    "mapId": map_contract["mapId"],
                                    "mapVersion":
                                        map_contract["mapVersion"],
                                    "mapFingerprint":
                                        map_contract["mapFingerprint"],
                                    "formatId":
                                        format_contract["formatId"],
                                    "formatFingerprint":
                                        format_contract[
                                            "formatFingerprint"
                                        ],
                                    "contractProfileId":
                                        contract["capabilityVersions"][
                                            "contractProfileId"
                                        ],
                                    "matchContractFingerprint":
                                        contract[
                                            "matchContractFingerprint"
                                        ],
                                },
                            },
                        ],
                        "populations": [
                            {
                                "id": "tier-two",
                                "tier": "T2",
                                "coordinationGrade": "C0",
                                "entrants": entrants,
                            },
                        ],
                    }
                ),
                encoding="utf-8",
            )
            output = root / "evidence"

            report = DRIVER.run(
                spec_path,
                output,
                dry_run=False,
                resume=False,
            )

            self.assertEqual("complete", report["status"])
            self.assertEqual(2, report["cells"][0]["validMatches"])
            self.assertEqual(
                "not-measured",
                report["cells"][0]["balanceVector"][
                    "exploitability"
                ]["status"],
            )
            self.assertTrue(
                report["cells"][0]["matches"][0]["replay"].startswith(
                    "candidates/fixture-candidate/"
                )
            )
            self.assertTrue((output / "report.json").is_file())
            self.assertTrue((output / "report.md").is_file())
            frozen_run = json.loads(
                (output / "run.json").read_text(encoding="utf-8")
            )
            self.assertEqual(
                set(DRIVER.PIPELINE_FILES),
                {
                    Path(path)
                    for path in frozen_run["pipelineSha256"]
                },
            )
            self.assertTrue(
                (
                    output
                    / "candidates"
                    / "fixture-candidate"
                    / "populations"
                    / "tier-two"
                    / "dynamics.json"
                ).is_file()
            )

    def test_missing_factorial_cell_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            spec = root / "invalid.json"
            spec.write_text(
                json.dumps(
                    {
                        "schemaVersion": 1,
                        "experimentId": "invalid",
                        "status": "experimental",
                        "evidenceClass": "infrastructure-smoke",
                        "hypothesis": "invalid",
                        "factors": {
                            "map": ["a", "b"],
                            "policy": ["manual", "automatic"],
                        },
                        "pairedSeeds": [1],
                        "verifyCommand": "true {replay}",
                        "candidates": [],
                        "populations": [],
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaises(ValueError):
                DRIVER.load_spec(spec)

    def test_draw_only_cell_does_not_report_zero_side_bias(self) -> None:
        report = DRIVER._cell_report(
            {"id": "candidate", "factors": {"map": "one"}},
            {
                "id": "population",
                "tier": "T2",
                "coordinationGrade": "C0",
                "entrants": [{"id": "alpha"}, {"id": "beta"}],
            },
            [
                {
                    "status": "verified",
                    "seed": 7,
                    "teamAssignments": {"0": "alpha", "1": "beta"},
                    "winnerTeamId": None,
                    "winner": None,
                    "draw": True,
                    "durationTicks": 10,
                },
                {
                    "status": "verified",
                    "seed": 7,
                    "teamAssignments": {"0": "beta", "1": "alpha"},
                    "winnerTeamId": None,
                    "winner": None,
                    "draw": True,
                    "durationTicks": 10,
                },
            ],
            None,
        )

        fairness = report["balanceVector"]["sideSpawnFairness"]
        self.assertEqual(
            "not-estimable-no-decisive-games",
            fairness["status"],
        )
        self.assertIsNone(fairness["decisiveWinDelta"])
        self.assertIsNone(fairness["assignmentSensitivePairShare"])


if __name__ == "__main__":
    unittest.main()
