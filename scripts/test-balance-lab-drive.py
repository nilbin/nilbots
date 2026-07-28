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
            "evaluationProfileId": "two-team-zero-sum-v1",
            "studyBlocks": [
                {
                    "id": "matrix",
                    "role": "infrastructure-smoke",
                    "candidateIds": ["a", "b", "c", "d"],
                    "populationIds": ["t-two"],
                    "includeSelfPlay": False,
                },
            ],
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
                    "qualificationProfileId": "test-profile-1",
                    "balanceEvidenceEligible": False,
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
                        "authoringLineageId": f"{entrant_id}-lineage",
                        "doctrineId": f"{entrant_id}-doctrine",
                        "authoringBudgetId": "test-budget",
                        "authorPacketSha256": None,
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
                            "suiteId": "test-suite",
                            "suiteVersion": 1,
                            "qualificationProfileId": "test-profile-1",
                            "qualificationContractFingerprint": None,
                            "evidence": None,
                            "evidenceSha256": None,
                            "tierAwarded": "T2",
                            "coordinationGradeAwarded": "C0",
                            "balanceEvidenceEligible": False,
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
            ablation = root / "ablation.json"
            ablation.write_text(
                json.dumps(
                    {
                        "schemaVersion": 1,
                        "registryId": "test-ablation-v1",
                        "items": [
                            {
                                "id": "test-debt",
                                "status": "open",
                                "currentInterpretation": "A bundle.",
                                "requiredIsolation": "Split the bundle.",
                                "requiredBefore": "pilot",
                            },
                        ],
                    }
                ),
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
                        "schemaVersion": 3,
                        "experimentId": "test-balance-lab",
                        "status": "experimental",
                        "hypothesis": "The matrix is reproducible.",
                        "ablationRegistry": {
                            "path": "ablation.json",
                            "sha256": DRIVER._sha256(ablation),
                        },
                        "studyBlocks": [
                            {
                                "id": "matrix-smoke",
                                "role": "infrastructure-smoke",
                                "hypothesis": "The fixture matrix runs.",
                                "qualificationProfileId":
                                    "test-profile-1",
                                "candidateIds": ["fixture-candidate"],
                                "populationIds": ["tier-two"],
                                "includeSelfPlay": False,
                                "commonRandomness": {
                                    "protocol": "not-required",
                                },
                            },
                        ],
                        "factors": {
                            "map-topology": ["fixture"],
                            "companion-policy": ["manual"],
                        },
                        "pairedSeeds": [7],
                        "holdout": {
                            "protocol": "none",
                            "reason": "test infrastructure smoke",
                        },
                        "studyDesign": {
                            "decisionProfileId": "test-pilot-v1",
                            "analysisUnit":
                                "mirrored-entrant-pair-seed-v1",
                            "confidenceLevel": 0.95,
                            "bootstrapResamples": 100,
                            "minimumMirroredUnitsPerCell": 1,
                            "minimumEntrantPairsPerCell": 1,
                            "minimumSeedsPerEntrantPair": 1,
                            "minimumIndependentLineagesPerPopulation": 2,
                            "minimumVotingLineagesPerPopulation": 4,
                            "minimumVotingTier": "T4",
                            "multiplicityPolicy":
                                "diagnostic-no-selection",
                            "requiredEvidenceLayers": [
                                "contract-validity",
                            ],
                        },
                        "toolchain": {
                            "protocol": "diagnostic-current-process",
                            "reason": "unit-test fixture",
                        },
                        "verifyCommand":
                            f"{DRIVER.sys.executable} {verify} "
                            "{replay}",
                        "evaluationProfileId":
                            "two-team-zero-sum-v1",
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
                                    "seedProfileId":
                                        rules["seedMechanics"][
                                            "seedProfileId"
                                        ],
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
                                    "topologyProfileId":
                                        "fixture-topology-v1",
                                    "topologyFingerprint":
                                        contract["topology"][
                                            "topologyFingerprint"
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
                                "qualificationProfileId":
                                    "test-profile-1",
                                "qualificationContractFingerprint": None,
                                "balanceEvidenceEligible": False,
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
            self.assertFalse(report["balanceVerdictEligible"])
            self.assertFalse(report["candidatePromotionEligible"])
            self.assertFalse(
                report["cells"][0]["balanceVerdictEligibility"]["eligible"]
            )
            self.assertEqual(2, report["cells"][0]["validMatches"])
            self.assertEqual(
                "not-measured",
                report["cells"][0]["balanceVector"][
                    "exploitability"
                ]["status"],
            )
            self.assertTrue(
                report["cells"][0]["matches"][0]["replay"].startswith(
                    "studies/matrix-smoke/candidates/fixture-candidate/"
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
                    / "studies"
                    / "matrix-smoke"
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
                        "schemaVersion": 3,
                        "experimentId": "invalid",
                        "status": "experimental",
                        "hypothesis": "invalid",
                        "studyBlocks": [],
                        "factors": {
                            "map": ["a", "b"],
                            "policy": ["manual", "automatic"],
                        },
                        "pairedSeeds": [1],
                        "holdout": {
                            "protocol": "none",
                            "reason": "invalid test",
                        },
                        "studyDesign": {
                            "decisionProfileId": "test-v1",
                            "analysisUnit":
                                "mirrored-entrant-pair-seed-v1",
                            "confidenceLevel": 0.95,
                            "bootstrapResamples": 100,
                            "minimumMirroredUnitsPerCell": 1,
                            "minimumEntrantPairsPerCell": 1,
                            "minimumSeedsPerEntrantPair": 1,
                            "minimumIndependentLineagesPerPopulation": 2,
                            "minimumVotingLineagesPerPopulation": 4,
                            "minimumVotingTier": "T4",
                            "multiplicityPolicy":
                                "diagnostic-no-selection",
                            "requiredEvidenceLayers": [
                                "contract-validity",
                            ],
                        },
                        "toolchain": {
                            "protocol": "diagnostic-current-process",
                            "reason": "invalid test",
                        },
                        "verifyCommand": "true {replay}",
                        "evaluationProfileId":
                            "two-team-zero-sum-v1",
                        "candidates": [],
                        "populations": [],
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaises(ValueError):
                DRIVER.load_spec(spec)

    def test_balance_eligible_entrant_requires_matching_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            entrant_root = root / "entrant"
            entrant_root.mkdir()
            (entrant_root / "Bot.cs").write_text(
                "// source\n",
                encoding="utf-8",
            )
            artifact = entrant_root / "bot.wasm"
            artifact.write_bytes(b"artifact")
            artifact_sha = hashlib.sha256(b"artifact").hexdigest()
            qualification_fingerprint = "a" * 64
            report_path = root / "qualification.json"
            report_path.write_text(
                json.dumps(
                    {
                        "suiteId": "test-suite",
                        "suiteVersion": 2,
                        "qualificationProfileId": "test-profile-1",
                        "qualificationContractFingerprint":
                            qualification_fingerprint,
                        "artifactHash": artifact_sha,
                        "passed": True,
                        "profileComplete": True,
                        "tierAwarded": "T2",
                        "coordinationGradeAwarded": "C0",
                        "balanceEvidenceEligible": True,
                    }
                ),
                encoding="utf-8",
            )
            evidence_sha = DRIVER._sha256(report_path)
            raw = {
                "id": "entrant",
                "name": "Entrant",
                "authoringLineageId": "entrant-lineage",
                "doctrineId": "entrant-doctrine",
                "authoringBudgetId": "test-budget",
                "authorPacketSha256": "b" * 64,
                "root": "entrant",
                "artifact": "entrant/bot.wasm",
                "artifactSha256": artifact_sha,
                "sourceTreeSha256": DRIVER.COHORT._source_tree_sha256(
                    entrant_root
                ),
                "qualification": {
                    "suiteId": "test-suite",
                    "suiteVersion": 2,
                    "qualificationProfileId": "test-profile-1",
                    "qualificationContractFingerprint":
                        qualification_fingerprint,
                    "evidence": "qualification.json",
                    "evidenceSha256": evidence_sha,
                    "tierAwarded": "T2",
                    "coordinationGradeAwarded": "C0",
                    "balanceEvidenceEligible": True,
                },
            }

            normalized = DRIVER._normalize_entrant(
                raw,
                root.resolve(),
                "population",
                "test-profile-1",
                qualification_fingerprint,
                True,
                "T2",
                "C0",
            )

            self.assertEqual(
                report_path.resolve(),
                normalized["qualificationEvidencePath"],
            )
            bad = json.loads(json.dumps(raw))
            bad["qualification"]["tierAwarded"] = "T3"
            with self.assertRaises(ValueError):
                DRIVER._normalize_entrant(
                    bad,
                    root.resolve(),
                    "population",
                    "test-profile-1",
                    qualification_fingerprint,
                    True,
                    "T2",
                    "C0",
                )

    def test_draw_only_cell_does_not_report_zero_side_bias(self) -> None:
        report = DRIVER._cell_report(
            {
                "id": "candidate",
                "factors": {"map": "one"},
                "contract": {
                    "topologyProfileId": "fixture-topology-v1",
                },
            },
            {
                "id": "population",
                "tier": "T2",
                "coordinationGrade": "C0",
                "qualificationProfileId": "test-profile-1",
                "balanceEvidenceEligible": False,
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
            "measured-mirrored-block-estimand",
            fairness["status"],
        )
        self.assertIsNone(fairness["decisiveWinDelta"])
        self.assertIsNone(fairness["assignmentSensitivePairShare"])

    def test_doctrine_redundancy_counts_behavior_not_artifacts(self) -> None:
        population = {
            "entrants": [
                {
                    "id": "alpha",
                    "doctrineId": "alpha-doctrine",
                },
                {
                    "id": "beta",
                    "doctrineId": "beta-doctrine",
                },
                {
                    "id": "gamma",
                    "doctrineId": "gamma-doctrine",
                },
                {
                    "id": "delta",
                    "doctrineId": "delta-doctrine",
                },
            ],
        }
        signatures = {
            "alpha": {
                "turns": 100,
                "actionKindCounts": {"movement": 50, "attack": 50},
                "formTurnCounts": {"prime-mobile": 100},
                "objectiveTurns": 40,
                "damageDealt": 10,
                "damageTaken": 8,
            },
            "beta": {
                "turns": 100,
                "actionKindCounts": {"movement": 48, "attack": 52},
                "formTurnCounts": {"prime-mobile": 100},
                "objectiveTurns": 42,
                "damageDealt": 11,
                "damageTaken": 8,
            },
            "gamma": {
                "turns": 100,
                "actionKindCounts": {"movement": 90, "attack": 10},
                "formTurnCounts": {"prime-mobile": 100},
                "objectiveTurns": 5,
                "damageDealt": 2,
                "damageTaken": 12,
            },
            "delta": {
                "turns": 100,
                "actionKindCounts": {"wait": 100},
                "formTurnCounts": {"turret": 100},
                "objectiveTurns": 100,
                "damageDealt": 20,
                "damageTaken": 2,
            },
        }
        payoff = {
            "alpha": {
                "alpha": None,
                "beta": 0.0,
                "gamma": 1.0,
                "delta": -1.0,
            },
            "beta": {
                "alpha": 0.0,
                "beta": None,
                "gamma": 1.0,
                "delta": -1.0,
            },
            "gamma": {
                "alpha": -1.0,
                "beta": -1.0,
                "gamma": None,
                "delta": 1.0,
            },
            "delta": {
                "alpha": 1.0,
                "beta": 1.0,
                "gamma": -1.0,
                "delta": None,
            },
        }

        result = DRIVER._doctrine_redundancy(
            [
                {
                    "status": "verified",
                    "entrantBehavior": signatures,
                },
            ],
            population,
            payoff,
        )

        self.assertEqual(4, result["artifactCount"])
        self.assertEqual(3, result["effectiveDoctrineEstimate"])
        self.assertIn(
            ["alpha", "beta"],
            result["redundancyComponents"],
        )
        alpha_beta = next(
            pair
            for pair in result["pairwiseEvidence"]
            if pair["first"] == "alpha" and pair["second"] == "beta"
        )
        self.assertTrue(alpha_beta["diagnosticallyRedundant"])
        self.assertEqual("measured-v1", alpha_beta["basis"])

    def test_trajectory_fingerprint_ignores_seed_attestation_not_actions(
        self,
    ) -> None:
        baseline = json.loads(FIXTURE.read_text(encoding="utf-8"))
        reseeded = json.loads(json.dumps(baseline))
        reseeded["header"]["seed"] = "999"
        reseeded["initialFrame"]["lifeStarts"][0][
            "actorRandomSeed"
        ] = "999"
        for tick in reseeded["ticks"]:
            for start in tick["tickStart"]["lifeStarts"]:
                start["actorRandomSeed"] = "999"

        self.assertEqual(
            DRIVER._trajectory_fingerprint(baseline),
            DRIVER._trajectory_fingerprint(reseeded),
        )

        changed_action = json.loads(json.dumps(reseeded))
        changed_action["ticks"][0]["actorTurns"][0][
            "submittedDecision"
        ]["actionId"] = "shoot"
        self.assertNotEqual(
            DRIVER._trajectory_fingerprint(baseline),
            DRIVER._trajectory_fingerprint(changed_action),
        )


if __name__ == "__main__":
    unittest.main()
