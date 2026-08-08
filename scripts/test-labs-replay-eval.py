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


OBJECTIVE_TILES = [
    {(2, 2)},
    {(3, 2)},
    {(4, 2)},
    {(5, 2)},
    {(6, 2)},
]
FORM_WEIGHTS = {"mobile": 1, "turret": 0}
ADVANCE_DELTAS = {0: 1, 1: -1}


def _entry(
    tick: int,
    position_index: int,
    *,
    progress: int = 0,
    bodies: tuple = (),
    damage: bool = False,
    claiming: int | None = None,
    resumes: int = 0,
) -> dict:
    return {
        "tick": tick,
        "positionIndex": position_index,
        "claimingTeamId": claiming,
        "captureProgress": progress,
        "controlResumesAtTick": resumes,
        "damage": damage,
        "bodies": tuple(bodies),
    }


def _body(
    team: int,
    unit: int,
    life: int,
    x: int,
    y: int,
    *,
    form: str = "mobile",
    facing: str = "east",
    health: int = 1,
) -> tuple:
    return (team, unit, life, form, x, y, facing, health)


def _dynamics(
    trace: list[dict],
    *,
    completion_reason: str = "max-ticks",
    winner_team_id: int | None = None,
    final_signed_score: int = 0,
) -> dict:
    return EVALUATOR._dynamics_metrics(
        trace,
        objective_tiles=OBJECTIVE_TILES,
        form_weights=FORM_WEIGHTS,
        advance_deltas=ADVANCE_DELTAS,
        centre_index=2,
        capture_threshold=3,
        floor_tiles=35,
        completion_reason=completion_reason,
        winner_team_id=winner_team_id,
        final_signed_score=final_signed_score,
    )


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


class PendulumDynamicsTests(unittest.TestCase):
    """Pin the metric family of DESIGN-FORENSICS-DYNAMICS-2026-07-29.md."""

    def test_breach_is_counted_as_the_final_uncensored_advance(self) -> None:
        trace = [
            _entry(0, 2),
            _entry(1, 3),
            _entry(2, 4),
        ]
        row = _dynamics(
            trace,
            completion_reason="base-breach",
            winner_team_id=0,
        )
        frontline = row["frontline"]

        # Two observed advances plus the breach the replay stops short of.
        self.assertEqual(
            {"2|1": 1, "3|1": 1, "4|1": 1},
            frontline["transitions"],
        )
        self.assertTrue(frontline["breachCountedAsAdvance"])
        self.assertEqual(2, frontline["advances"])
        self.assertEqual(0, frontline["reversals"])
        self.assertEqual(2, frontline["netDisplacement"])
        self.assertEqual(2, frontline["finalDisplacement"])
        self.assertEqual(1.0, frontline["displacementEfficiency"])

    def test_reversals_and_tug_efficiency_use_signed_displacement(
        self,
    ) -> None:
        row = _dynamics([_entry(index, value) for index, value in
                         enumerate([2, 3, 2, 1, 2])])
        frontline = row["frontline"]

        self.assertEqual(4, frontline["advances"])
        self.assertEqual(2, frontline["reversals"])
        self.assertAlmostEqual(2 / 3, frontline["reversalRate"])
        self.assertEqual(0, frontline["netDisplacement"])
        self.assertEqual(0.0, frontline["displacementEfficiency"])
        self.assertFalse(frontline["breachCountedAsAdvance"])

    def test_leader_extends_pools_only_off_centre_transitions(self) -> None:
        rows = [
            {
                "source": "one",
                "result": {"completionReason": "max-ticks", "draw": False},
                "duration": {"ticks": 5},
                "dynamics": _dynamics(
                    [
                        _entry(index, value)
                        for index, value in enumerate([2, 3, 4, 3, 2])
                    ]
                ),
            }
        ]
        summary = EVALUATOR.summarize_dynamics("cell", rows)
        pendulum = summary["pendulum"]

        # 2->3 leaves the centre and is excluded; 3->4 extends, 4->3 and
        # 3->2 revert, so the leading side pushed further once in three.
        self.assertEqual(3, pendulum["leaderExtendsTransitions"])
        self.assertAlmostEqual(1 / 3, pendulum["leaderExtendsProbability"])
        self.assertEqual(
            [0, 1, 2],
            [row["displacement"] for row in pendulum["byDisplacement"]],
        )

    def test_transit_is_bucketed_by_the_spawning_team_lead(self) -> None:
        # Team 0 leads at index 3; team 1 trails there. Both reinforce.
        trace = [
            _entry(0, 3, bodies=[_body(0, 0, 0, 1, 1)]),
            _entry(1, 3, bodies=[_body(0, 0, 0, 1, 1), _body(1, 0, 1, 8, 1)]),
            _entry(2, 3, bodies=[_body(0, 0, 1, 1, 1), _body(1, 0, 1, 5, 2)]),
            _entry(3, 3, bodies=[_body(0, 0, 1, 5, 2), _body(1, 0, 1, 5, 2)]),
        ]
        row = _dynamics(trace)
        transit = row["reinforcementTransit"]

        # Team 1's life spawned at tick 1 and stood on the objective at 2;
        # team 0's spawned at tick 2 and arrived at 3.
        self.assertEqual(
            {"-1": {"1": 1}, "1": {"1": 1}},
            transit["byLeadTickHistogram"],
        )
        self.assertEqual(2, transit["reinforcementLives"])
        self.assertEqual(2, transit["arrivedLives"])

    def test_sole_presence_splits_productive_from_wasted_ticks(self) -> None:
        centre = [_body(0, 0, 0, 4, 2)]
        east = [_body(0, 0, 0, 5, 2)]
        contested = [_body(0, 0, 0, 5, 2), _body(1, 0, 0, 5, 2)]
        trace = [
            # A two-tick sole run on position 2 that captures at tick 2.
            _entry(0, 2, bodies=centre, progress=1, claiming=0),
            _entry(1, 2, bodies=centre, progress=2, claiming=0),
            _entry(2, 3, bodies=[], progress=0),
            # A two-tick sole run on position 3 broken by contest: wasted.
            _entry(3, 3, bodies=east, progress=1, claiming=0),
            _entry(4, 3, bodies=east, progress=2, claiming=0),
            _entry(5, 3, bodies=contested, progress=1, claiming=0),
        ]
        sole = _dynamics(trace)["solePresence"]

        self.assertEqual({"contested": 1, "empty": 1, "sole": 4},
                         sole["controlMixTicks"])
        self.assertEqual(2, sole["productiveTicks"])
        self.assertEqual(2, sole["wastedTicks"])
        self.assertEqual(0.5, sole["wastedShare"])
        self.assertEqual({"2": 2}, sole["runLengthHistogram"])
        self.assertEqual(1, sole["captures"])
        self.assertEqual({"contested": 1, "empty": 1}, sole["terminations"])
        # Progress destroyed by decay, never the reset an advance performs.
        self.assertEqual(3, sole["progressGained"])
        self.assertEqual(1, sole["progressLost"])
        self.assertAlmostEqual(1 / 3, sole["decayDestroyedShare"])
        self.assertEqual(0, sole["runsReachingThreshold"])

    def test_objective_weight_zero_forms_neither_claim_nor_contest(
        self,
    ) -> None:
        turret = [_body(0, 0, 0, 4, 2, form="turret")]
        row = _dynamics([_entry(0, 2, bodies=turret)])

        self.assertEqual({"empty": 1}, row["solePresence"]["controlMixTicks"])

    def test_paused_ticks_are_not_presence(self) -> None:
        objective = [_body(0, 0, 0, 4, 2)]
        row = _dynamics(
            [_entry(0, 2, bodies=objective, resumes=2)],
        )

        self.assertEqual({"paused": 1}, row["solePresence"]["controlMixTicks"])

    def test_frozen_scoreboard_and_exact_limit_cycles(self) -> None:
        held = [_body(0, 0, 0, 4, 2)]
        moved = [_body(0, 0, 0, 4, 3)]
        # Position and progress never change; the bodies oscillate on a
        # two-tick period, so both detectors fire on the same trace.
        trace = [
            _entry(index, 2, bodies=held if index % 2 == 0 else moved)
            for index in range(9)
        ]
        staleness = _dynamics(trace)["staleness"]

        self.assertEqual(8, staleness["longestFrozenScoreboardTicks"])
        self.assertEqual(7, staleness["longestLimitCycleTicks"])
        self.assertEqual(2, staleness["limitCyclePeriod"])
        self.assertEqual(7, staleness["terminalLimitCycleTicks"])
        self.assertFalse(staleness["frozenScoreboard"])
        self.assertFalse(staleness["limitCycle"])

    def test_close_contact_without_damage_uses_chebyshev_distance(
        self,
    ) -> None:
        trace = [
            # Chebyshev 3 (diagonal), damage-free: a stare.
            _entry(0, 2, bodies=[_body(0, 0, 0, 1, 1), _body(1, 0, 0, 4, 4)]),
            # Same separation, but damage lands.
            _entry(
                1,
                2,
                bodies=[_body(0, 0, 0, 1, 1), _body(1, 0, 0, 4, 4)],
                damage=True,
            ),
            # Chebyshev 4: out of contact even though Manhattan is 4 too.
            _entry(2, 2, bodies=[_body(0, 0, 0, 1, 1), _body(1, 0, 0, 5, 1)]),
            # A lone team cannot be in contact with anyone.
            _entry(3, 2, bodies=[_body(0, 0, 0, 1, 1)]),
        ]
        close = _dynamics(trace)["closeContact"]

        self.assertEqual("chebyshev", close["distanceMetric"])
        self.assertEqual(2, close["ticks"])
        self.assertEqual(1, close["noDamageTicks"])
        self.assertEqual(0.5, close["noDamageShare"])
        self.assertEqual(0, close["sustainedStareTicks"])

    def test_positional_entropy_and_slot_trace_repetition(self) -> None:
        trace = [
            _entry(
                index,
                2,
                bodies=[_body(0, 0, 0, 4 if index % 2 == 0 else 5, 2)],
            )
            for index in range(40)
        ]
        positional = _dynamics(trace)["positional"]

        self.assertEqual(40, positional["bodyTicks"])
        self.assertEqual(2, positional["distinctTiles"])
        self.assertEqual(35, positional["floorTiles"])
        self.assertAlmostEqual(2 / 35, positional["coverage"])
        self.assertAlmostEqual(1.0, positional["entropyBits"])
        self.assertAlmostEqual(2.0, positional["effectiveTiles"])
        self.assertEqual(1, len(positional["traces"]))
        slot = positional["traces"][0]
        self.assertEqual(2, slot["argmaxLag"])
        self.assertAlmostEqual(1.0, slot["argmaxAutocorrelation"])
        self.assertEqual(0.0, slot["dwellFraction"])
        self.assertAlmostEqual(0.05, slot["tileRatio"])

    def test_slot_traces_shorter_than_the_floor_are_dropped(self) -> None:
        trace = [
            _entry(index, 2, bodies=[_body(0, 0, 0, 4, 2)])
            for index in range(39)
        ]

        self.assertEqual([], _dynamics(trace)["positional"]["traces"])

    def test_signed_territorial_score_follows_the_advance_direction(
        self,
    ) -> None:
        trace = [
            _entry(0, 3, progress=2, claiming=0),
            _entry(1, 1, progress=2, claiming=1),
        ]
        score = _dynamics(trace, final_signed_score=-5)["score"]

        # threshold 3: +1 position and +2 claim, then -1 position and -2.
        self.assertEqual(
            {"100": -5, "200": -5, "300": -5, "400": -5},
            score["signedTerritorialProbes"],
        )
        self.assertEqual(1, score["leadChanges"])
        self.assertEqual(5, score["maxAbsoluteTerritorial"])

    def test_gates_transcribe_the_pre_registered_thresholds(self) -> None:
        rows = [
            {
                "source": "one",
                "result": {"completionReason": "max-ticks", "draw": True},
                "duration": {"ticks": 3},
                "dynamics": _dynamics(
                    [_entry(index, value) for index, value in
                     enumerate([2, 3, 4])],
                ),
            }
        ]
        gates = EVALUATOR.summarize_dynamics("cell", rows)["gates"]

        self.assertEqual(
            [
                "N1-lower-capture-threshold",
                "S1-territory-ratchet",
                "S2-forward-spawn",
                "S3-contest-costs",
                "S4-overtime-escalation",
                "S5-map-geometry",
            ],
            sorted(gates),
        )
        criteria = {
            item["metric"]: item
            for item in gates["S1-territory-ratchet"]["criteria"]
        }
        self.assertEqual(0.50, criteria["pendulum.leaderExtendsProbability"][
            "threshold"
        ])
        self.assertEqual(1.0, criteria["pendulum.capShare"]["observed"])
        self.assertFalse(criteria["pendulum.capShare"]["pass"])
        self.assertFalse(gates["S1-territory-ratchet"]["pass"])
        # A draw-free ratchet is required, and this cell is all draws.
        self.assertTrue(criteria["pendulum.drawRate"]["operationalized"])
        # Without any transit sample the gate reports "no verdict".
        self.assertIsNone(gates["S2-forward-spawn"]["pass"])

    def test_dynamics_are_opt_in_and_reach_the_report(self) -> None:
        document = json.loads(FIXTURE.read_text(encoding="utf-8"))
        self.assertNotIn("dynamics", EVALUATOR.analyze_replay(document))

        row = EVALUATOR.analyze_replay(document, dynamics=True)
        dynamics = row["dynamics"]
        self.assertEqual(3, dynamics["contract"]["captureThreshold"])
        self.assertEqual(2, dynamics["contract"]["centreIndex"])
        self.assertEqual(0, dynamics["contract"]["publicAdvanceTeamId"])
        self.assertEqual(35, dynamics["contract"]["floorTiles"])
        self.assertEqual(
            1,
            dynamics["solePresence"]["controlMixTicks"]["sole"],
        )

        summary = EVALUATOR.summarize_group("baseline", [row])
        self.assertIn("dynamics", summary)
        self.assertEqual(1, summary["dynamics"]["matches"])

    def test_verify_against_reports_deviations_and_fails_the_run(
        self,
    ) -> None:
        document = json.loads(FIXTURE.read_text(encoding="utf-8"))
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            replay = root / "matches" / "one" / "attempt-01" / "replay.json"
            replay.parent.mkdir(parents=True)
            replay.write_text(json.dumps(document), encoding="utf-8")
            baseline = root / "baseline.json"
            baseline.write_text(
                json.dumps(
                    {
                        "baseline": "unit-test",
                        "expectations": [
                            {
                                "path": "solePresence.contestedShare",
                                "expected": 0.0,
                                "tolerance": 0.0,
                            },
                            {
                                "path": "pendulum.capShare",
                                "expected": 0.0,
                                "tolerance": 0.1,
                            },
                        ],
                    }
                ),
                encoding="utf-8",
            )

            completed = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    "--group",
                    f"baseline={root / 'matches'}",
                    "--verify-against",
                    str(baseline),
                ],
                cwd=ROOT,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(3, completed.returncode, completed.stderr)
            self.assertIn("1/2 within tolerance", completed.stdout)

    def test_checked_in_baseline_paths_resolve_against_a_report(self) -> None:
        baseline = json.loads(
            (
                ROOT
                / "balance"
                / "frontline-pendulum-dynamics-baseline-v1.json"
            ).read_text(encoding="utf-8")
        )
        document = json.loads(FIXTURE.read_text(encoding="utf-8"))
        row = EVALUATOR.analyze_replay(document, dynamics=True)
        report = {
            "groups": [EVALUATOR.summarize_group("baseline", [row])],
            "dynamics": EVALUATOR.summarize_dynamics("pooled", [row]),
        }

        results = EVALUATOR.verify_dynamics(report, baseline)

        self.assertEqual(len(baseline["expectations"]), len(results))
        self.assertEqual(
            [],
            [result for result in results if result.get("error")],
        )


if __name__ == "__main__":
    unittest.main()
