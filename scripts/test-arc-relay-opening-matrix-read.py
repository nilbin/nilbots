#!/usr/bin/env python3
"""Unit checks for the Arc Relay opening-matrix mechanism reducer."""

from __future__ import annotations

import importlib.util
from pathlib import Path
from unittest import mock
import unittest


SCRIPT = Path(__file__).with_name("arc-relay-opening-matrix-read.py")
SPEC = importlib.util.spec_from_file_location("arc_relay_opening_read", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
READ = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(READ)


def actor(team: int, unit: int, life: int = 0) -> dict[str, int]:
    return {"teamId": team, "unitId": unit, "lifeId": life}


class GeometryReadTests(unittest.TestCase):
    def test_attack_progress_is_team_relative(self) -> None:
        reactors = {0: (0, 0), 1: (10, 0)}
        self.assertEqual(0.75, READ.attack_progress(0, (7.5, 0), reactors))
        self.assertEqual(0.25, READ.attack_progress(1, (7.5, 0), reactors))

    def test_nearest_theater_has_stable_name_tie_break(self) -> None:
        wells = {"north": (0, 0), "centre": (2, 0), "south": (4, 0)}
        self.assertEqual("centre", READ.nearest_theater((1, 0), wells))


class ReplayMechanismTests(unittest.TestCase):
    def test_events_become_first_beats_participation_and_interception(self) -> None:
        regions = [
            {"regionId": "well-north", "tiles": [[2, 0]]},
            {"regionId": "well-centre", "tiles": [[5, 0]]},
            {"regionId": "well-south", "tiles": [[8, 0]]},
            {"regionId": "reactor-west", "tiles": [[0, 0]]},
            {"regionId": "reactor-east", "tiles": [[10, 0]]},
        ]
        body0 = {
            "actorId": actor(0, 0),
            "position": {"x": 2, "y": 0},
        }
        body1 = {
            "actorId": actor(1, 1),
            "position": {"x": 8, "y": 0},
        }
        replay = {
            "header": {"contract": {"map": {"regions": regions}}},
            "ticks": [
                {
                    "tick": 0,
                    "mindTurns": [
                        {"observation": {"bodies": [body0]}},
                        {"observation": {"bodies": [body1]}},
                    ],
                    "events": [
                        {
                            "kind": "attack",
                            "payload": {"actorId": actor(0, 0)},
                        },
                        {
                            "kind": "movement",
                            "payload": {"actorId": actor(0, 0)},
                        },
                        {
                            "kind": "arc-relay",
                            "payload": {"fact": {
                                "kind": "signature-changed",
                                "reason": "started",
                                "ownerActorId": actor(1, 1),
                            }},
                        },
                    ],
                },
                {
                    "tick": 1,
                    "mindTurns": [
                        {"observation": {"bodies": [body0]}},
                        {"observation": {"bodies": [body1]}},
                    ],
                    "events": [
                        {
                            "kind": "destruction",
                            "payload": {
                                "actorId": actor(1, 1),
                                "sourceTeamId": 0,
                            },
                        },
                        {
                            "kind": "arc-relay",
                            "payload": {"fact": {
                                "kind": "core-dropped",
                                "dropKind": "destruction",
                                "sourceActorId": actor(1, 1),
                            }},
                        },
                        {
                            "kind": "arc-relay",
                            "payload": {"fact": {
                                "kind": "core-picked-up",
                                "carrierActorId": actor(0, 0),
                            }},
                        },
                        {
                            "kind": "arc-relay",
                            "payload": {"fact": {
                                "kind": "core-banked",
                                "teamId": 0,
                                "carrierActorId": actor(0, 0),
                            }},
                        },
                    ],
                },
            ],
            "result": {
                "completionReason": "reactor-destroyed",
                "endTick": 1,
                "standings": {"winnerTeamId": 0},
            },
        }
        reduced = READ.replay_metrics(replay, opening_ticks=2)
        west = reduced["teams"]["0"]
        east = reduced["teams"]["1"]
        self.assertEqual(0, west["firstAttackTick"])
        self.assertEqual(1, west["firstPickupTick"])
        self.assertEqual(1, west["firstBankTick"])
        self.assertEqual(1, west["carrierInterceptions"])
        self.assertEqual(1, west["valueBodies"])
        self.assertEqual(1, east["signatureBodies"])
        self.assertEqual(1, west["longestStationaryRun"])


class SparseMatrixTests(unittest.TestCase):
    def test_omitted_pairs_are_absent_not_zero_game_records(self) -> None:
        entrants = {
            entrant: {"label": entrant, "intent": entrant}
            for entrant in ("pincer", "direct", "distributed", "split")
        }
        cells = []
        for opponent in ("direct", "distributed", "split"):
            cells.extend([
                {
                    "cellId": f"pincer--{opponent}--a0",
                    "seed": "1",
                    "team0": "pincer",
                    "team1": opponent,
                },
                {
                    "cellId": f"pincer--{opponent}--a1",
                    "seed": "1",
                    "team0": opponent,
                    "team1": "pincer",
                },
            ])
        team = {
            "firstAttackTick": 1,
            "firstPickupTick": 2,
            "firstBankTick": 3,
            "banks": 1,
            "carrierInterceptions": 0,
            "movementBodies": 8,
            "valueBodies": 8,
            "stationaryShare": 0.1,
            "longestStationaryRun": 2,
            "openingTheaterShares": {
                "north": 0.3,
                "centre": 0.4,
                "south": 0.3,
            },
            "openingOpponentHalfShare": 0.5,
        }
        metrics = {
            "winnerTeamId": 0,
            "completionReason": "reactor-destroyed",
            "endTick": 10,
            "teams": {"0": team, "1": team},
        }
        plan = {
            "sweepId": "sparse",
            "runtime": "in-process",
            "loopProfile": "tapline-3",
            "entrants": entrants,
            "cells": cells,
        }

        with mock.patch.object(
                READ,
                "accepted_cell",
                return_value=(Path("."), {
                    "matchEligibleForCohortRead": True,
                })), mock.patch.object(
                    READ, "read_json", return_value={}), mock.patch.object(
                        READ, "replay_metrics", return_value=metrics):
            reduced = READ.summarize(plan, Path("."), opening_ticks=120)

        self.assertEqual(
            {
                "direct--pincer",
                "distributed--pincer",
                "pincer--split",
            },
            set(reduced["pairings"]),
        )


if __name__ == "__main__":
    unittest.main()
