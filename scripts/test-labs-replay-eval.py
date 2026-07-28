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

    def test_attack_trajectory_uses_committed_program(self) -> None:
        self.assertEqual(
            "straight",
            EVALUATOR._attack_trajectory({"arguments": []}),
        )
        self.assertEqual(
            "curved",
            EVALUATOR._attack_trajectory(
                {
                    "arguments": [
                        {
                            "kind": "shot-program",
                            "value": {"bendCount": 1},
                        }
                    ]
                }
            ),
        )

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
        self.assertIsNone(row["activity"]["firstCombatEventTick"])
        self.assertEqual(
            1,
            row["activity"]["longestCombatEventFreeRunTicks"],
        )
        self.assertEqual(2, len(row["participants"]))
        self.assertEqual(1, row["opening"]["ticks"])
        self.assertEqual(0, row["opening"]["damageAmount"])
        self.assertEqual(
            {"territorial-progress": 1},
            row["opening"]["boundaryScores"]["0"],
        )
        self.assertEqual(
            1.0,
            row["participants"][0]["population"]["averageBodies"],
        )
        self.assertEqual(
            1.0,
            row["participants"][0]["population"][
                "activeEligibleSlotShare"
            ],
        )

        summary = EVALUATOR.summarize_group("baseline", [row])
        self.assertEqual(1, summary["matches"])
        self.assertEqual({"team-0": 1}, summary["outcomes"])
        self.assertEqual(["in-process"], summary["cohort"]["runtimeClasses"])
        self.assertEqual(2, len(summary["entrants"]))
        self.assertEqual(1, len(summary["pairings"]))
        self.assertEqual(1, summary["pairings"][0]["matches"])
        self.assertEqual(1, summary["activity"]["gamesWithoutCombatEvents"])
        self.assertEqual(0.0, summary["combat"]["attacksPer100Ticks"])
        self.assertEqual(0, summary["opening"]["gamesWithDamage"])

    def test_ready_slots_are_population_debt_not_inactive_locked_slots(
        self,
    ) -> None:
        candidate = copy.deepcopy(self.document)
        topology = candidate["header"]["contract"]["topology"]
        topology["unitSlots"].append(
            {
                "teamId": 0,
                "unitId": 1,
                "controllerParticipantId": 10,
            }
        )
        candidate["header"]["contract"]["lifecycleAssignments"].append(
            {
                "teamId": 0,
                "unitId": 1,
                "lifecycleProfileId": "child-ready",
                "initialGeneration": None,
                "allowedFormIds": ["mobile"],
                "initialAvailability": "dormant-unlock-at-tick",
                "unlockTick": 0,
                "assignedRespawnSpawnId": None,
            }
        )
        candidate["ticks"][0]["tickStart"]["state"]["slots"].append(
            {
                "teamId": 0,
                "unitId": 1,
                "participantId": 10,
                "nextLifeId": 0,
                "state": {"kind": "ready"},
                "pendingParentActorId": None,
                "splitReservation": None,
            }
        )

        row = EVALUATOR.analyze_replay(candidate)
        participant = next(
            value
            for value in row["participants"]
            if value["participantId"] == 10
        )

        self.assertEqual(2, participant["population"]["eligibleSlotTicks"])
        self.assertEqual(1, participant["population"]["readySlotTicks"])
        self.assertEqual(
            0.5,
            participant["population"]["activeEligibleSlotShare"],
        )
        self.assertEqual(1, participant["population"]["terminalReadyEpisodes"])

    def test_opening_classifies_straight_and_curved_shot_decisions(
        self,
    ) -> None:
        candidate = copy.deepcopy(self.document)
        decision = candidate["ticks"][0]["actorTurns"][0][
            "submittedDecision"
        ]
        decision.update(
            {
                "actionId": "shoot",
                "actionCode": 4,
                "arguments": [
                    {
                        "kind": "shot-program",
                        "value": {
                            "initialAimOffset": 0,
                            "bendDirection": 1,
                            "bendAfterTiles": 2,
                            "bendEveryTiles": 1,
                            "bendCount": 1,
                        },
                    }
                ],
            }
        )

        row = EVALUATOR.analyze_replay(candidate)
        participant = next(
            value
            for value in row["participants"]
            if value["participantId"] == 10
        )

        self.assertEqual(1, participant["opening"]["attackDecisions"])
        self.assertEqual(
            1,
            participant["opening"]["curvedAttackDecisions"],
        )
        self.assertEqual(
            0,
            participant["opening"]["straightAttackDecisions"],
        )

    def test_direct_shot_and_imminent_projectile_metrics_are_narrow(
        self,
    ) -> None:
        candidate = copy.deepcopy(self.document)
        turn = candidate["ticks"][0]["actorTurns"][0]
        observation = turn["observation"]
        observation["enemies"] = [
            {
                "actorId": {
                    "teamId": 1,
                    "unitId": 0,
                    "lifeId": 0,
                },
                "formId": "mobile",
                "position": {"x": 6, "y": 2},
                "facing": "west",
                "health": 1,
                "pendingSameLifeTransition": None,
                "observedBy": [turn["actorId"]],
            }
        ]
        observation["visibleProjectiles"] = [
            {
                "projectileId": "threat",
                "ownerTeamId": 1,
                "ownerActorId": {
                    "teamId": 1,
                    "unitId": 0,
                    "lifeId": 0,
                },
                "position": {"x": 2, "y": 2},
                "heading": "east",
                "tilesPerAdvance": 2,
                "ticksUntilAdvance": 1,
                "remainingTiles": 7,
                "observedBy": [turn["actorId"]],
            }
        ]
        candidate["header"]["contract"]["rules"]["actions"].append(
            {
                "id": "move",
                "code": 1,
                "kind": "movement",
            }
        )
        observation["actionLegalities"].append(
            {
                "actionId": "move",
                "actionCode": 1,
                "allowedByForm": True,
                "available": True,
                "constraints": [],
            }
        )

        row = EVALUATOR.analyze_replay(candidate)
        participant = next(
            value
            for value in row["participants"]
            if value["participantId"] == 10
        )
        policy = participant["combatPolicy"]

        self.assertEqual(1, policy["directAttackOpportunityTurns"])
        self.assertEqual(0, policy["directAttackOpportunityUses"])
        self.assertEqual(1, policy["imminentProjectileThreatTurns"])
        self.assertEqual(0, policy["imminentThreatMovementResponses"])
        self.assertEqual(
            1,
            policy["imminentThreatDirectAttackOpportunityTurns"],
        )
        self.assertEqual(
            0,
            policy[
                "imminentThreatMovementInsteadOfDirectAttackResponses"
            ],
        )
        self.assertEqual(1, policy["imminentThreatOnObjectiveTurns"])
        self.assertEqual(
            1,
            policy["imminentThreatOnObjectiveHoldResponses"],
        )
        self.assertEqual(0, policy["multiImminentProjectileThreatTurns"])

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

    def test_same_life_transitions_are_classified_by_objective_role(
        self,
    ) -> None:
        candidate = copy.deepcopy(self.document)
        rules = candidate["header"]["contract"]["rules"]
        rules["forms"].append(
            {
                "id": "turret",
                "maxHealth": 5,
                "movementProfileId": "ground",
                "visionProfileId": "turret-vision",
                "attackProfileId": "turret-bolt",
                "objectiveWeight": 0,
                "allowedActionIds": ["mobilize", "wait"],
            }
        )
        rules["actions"].extend(
            [
                {
                    "id": "transform",
                    "code": 103,
                    "kind": "same-life-transition",
                },
                {
                    "id": "mobilize",
                    "code": 104,
                    "kind": "same-life-transition",
                },
            ]
        )
        rules["sameLifeTransitions"] = [
            {
                "transitionId": "anchor-mobile",
                "actionId": "transform",
                "sourceFormId": "mobile",
                "targetFormId": "turret",
            },
            {
                "transitionId": "mobilize-mobile",
                "actionId": "mobilize",
                "sourceFormId": "turret",
                "targetFormId": "mobile",
            },
        ]

        row = EVALUATOR.analyze_replay(candidate)

        self.assertEqual(["turret"], row["mechanics"]["anchor"]["targetFormIds"])
        self.assertEqual(
            ["mobile"],
            row["mechanics"]["mobilize"]["targetFormIds"],
        )
        summary = EVALUATOR.summarize_group("candidate", [row])
        self.assertEqual(0, summary["mechanics"]["mobilize"]["completions"])

        form_weights = {
            form["id"]: form["objectiveWeight"] for form in rules["forms"]
        }
        catalog = EVALUATOR._transition_catalog(rules, form_weights)
        self.assertEqual({"turret"}, catalog[6])

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
