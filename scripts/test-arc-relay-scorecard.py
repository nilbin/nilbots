#!/usr/bin/env python3
"""Unit checks for the frozen Arc Relay felt-degeneracy bars."""

from __future__ import annotations

import importlib.util
from pathlib import Path
import unittest


SCRIPT = Path(__file__).with_name("arc-relay-scorecard.py")
SPEC = importlib.util.spec_from_file_location("arc_relay_scorecard", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
SCORE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SCORE)


class FeltDegeneracyTests(unittest.TestCase):
    @staticmethod
    def map_contract(height: int, north: int, centre: int, south: int) -> dict:
        return {
            "map": {
                "tileRows": ["." * 31 for _ in range(height)],
                "regions": [
                    {"regionId": "well-north", "tiles": [[15, north]]},
                    {"regionId": "well-centre", "tiles": [[15, centre]]},
                    {"regionId": "well-south", "tiles": [[15, south]]},
                    {
                        "regionId": "home-west",
                        "tiles": [[x, centre] for x in range(1, 4)],
                    },
                    {
                        "regionId": "home-east",
                        "tiles": [[x, centre] for x in range(27, 30)],
                    },
                ],
            },
        }

    def test_analysis_layout_reproduces_threefold_bands(self) -> None:
        layout = SCORE.map_analysis_layout(
            self.map_contract(23, north=4, centre=11, south=18))
        self.assertEqual(7, layout["theaterNorthMaximumY"])
        self.assertEqual(15, layout["theaterSouthMinimumY"])
        self.assertEqual(9, layout["westHomeCampMaximumX"])
        self.assertEqual(21, layout["eastHomeCampMinimumX"])

    def test_analysis_layout_scales_taller_threefold_theaters(self) -> None:
        layout = SCORE.map_analysis_layout(
            self.map_contract(29, north=4, centre=14, south=24))
        self.assertEqual(9, layout["theaterNorthMaximumY"])
        self.assertEqual(19, layout["theaterSouthMinimumY"])
        self.assertEqual("north", SCORE.theater((15, 9), layout))
        self.assertEqual("centre", SCORE.theater((15, 10), layout))
        self.assertEqual("south", SCORE.theater((15, 19), layout))

    def test_three_rapid_same_pair_reversals_trip_ping_pong_bar(self) -> None:
        a = (0, 0, 1)
        b = (0, 1, 1)
        output = SCORE.ping_pong_metrics(
            {"centre:1": [
                {"tick": 10, "epoch": 1, "source": a, "target": b},
                {"tick": 12, "epoch": 1, "source": b, "target": a},
                {"tick": 14, "epoch": 1, "source": a, "target": b},
                {"tick": 16, "epoch": 1, "source": b, "target": a},
            ]},
            [0, 1],
            maximum_gap_ticks=4,
        )
        self.assertTrue(output["barTrippedByTeam"]["0"])
        self.assertEqual(3, output["maxEpisodeReversalsByTeam"]["0"])
        self.assertFalse(output["barTrippedByTeam"]["1"])

    def test_two_reversals_remain_below_ping_pong_bar(self) -> None:
        a = (0, 0, 1)
        b = (0, 1, 1)
        output = SCORE.ping_pong_metrics(
            {"centre:1": [
                {"tick": 10, "epoch": 1, "source": a, "target": b},
                {"tick": 12, "epoch": 1, "source": b, "target": a},
                {"tick": 14, "epoch": 1, "source": a, "target": b},
            ]},
            [0],
            maximum_gap_ticks=4,
        )
        self.assertFalse(output["barTrippedByTeam"]["0"])

    def test_sixty_quiet_ticks_in_seventy_five_trip_passivity_bar(self) -> None:
        maximum, windows = SCORE.sustained_passivity_windows(
            [True] * 60 + [False] * 15)
        self.assertEqual(60, maximum)
        self.assertEqual(1, len(windows))

    def test_fifty_nine_quiet_ticks_do_not_trip_passivity_bar(self) -> None:
        maximum, windows = SCORE.sustained_passivity_windows(
            [True] * 59 + [False] * 16)
        self.assertEqual(59, maximum)
        self.assertEqual([], windows)

    def test_sixty_high_wait_ticks_trip_formation_freeze_bar(self) -> None:
        maximum, windows = SCORE.threshold_windows(
            [True] * 60 + [False] * 15,
            SCORE.FREEZE_WINDOW_TICKS,
            SCORE.FREEZE_MIN_HIGH_WAIT_TICKS,
        )
        self.assertEqual(60, maximum)
        self.assertEqual(1, len(windows))

    def test_thirty_tick_stationary_carrier_trips_bar(self) -> None:
        def world(x: int) -> list:
            value = [None] * 8
            value[7] = {
                "kind": "arc-relay",
                "visibleCores": [{
                    "coreId": {
                        "sourceWellId": "centre",
                        "sourceOrdinal": 1,
                    },
                    "disposition": "carried",
                    "carrierActorId": [0, 3, 1],
                    "position": [x, 11],
                }],
            }
            return value

        output = SCORE.stuck_carrier_metrics(
            {"worlds": [world(9)] * 30}, [0, 1])
        self.assertTrue(output["barTrippedByTeam"]["0"])
        self.assertEqual(30, output["maxConsecutiveTicksByTeam"]["0"])
        self.assertFalse(output["barTrippedByTeam"]["1"])

    def test_carrier_progress_resets_stationary_run(self) -> None:
        def world(x: int) -> list:
            value = [None] * 8
            value[7] = {
                "kind": "arc-relay",
                "visibleCores": [{
                    "coreId": {
                        "sourceWellId": "centre",
                        "sourceOrdinal": 1,
                    },
                    "disposition": "carried",
                    "carrierActorId": [0, 3, 1],
                    "position": [x, 11],
                }],
            }
            return value

        output = SCORE.stuck_carrier_metrics(
            {"worlds": [world(9)] * 29 + [world(8)] * 29}, [0])
        self.assertFalse(output["barTrippedByTeam"]["0"])
        self.assertEqual(29, output["maxConsecutiveTicksByTeam"]["0"])

    @staticmethod
    def progress_world(
        core_x: int,
        carrier_unit: int = 3,
        enemy_x: int | None = None,
    ) -> list:
        value = [None] * 8
        actors = [[
            0, carrier_unit, 1, 0, 0, "arc-body-relay",
            core_x, 1, "west", 4,
        ]]
        if enemy_x is not None:
            actors.append([
                1, 0, 1, 1, 0, "arc-body-relay",
                enemy_x, 1, "east", 4,
            ])
        value[4] = actors
        value[7] = {
            "kind": "arc-relay",
            "visibleCores": [{
                "coreId": {
                    "sourceWellId": "centre",
                    "sourceOrdinal": 1,
                },
                "disposition": "carried",
                "carrierActorId": [0, carrier_unit, 1],
                "position": [core_x, 1],
            }],
        }
        return value

    def test_home_progress_bar_survives_tile_oscillation_and_handoff(self) -> None:
        worlds = []
        for tick in range(30):
            worlds.append(self.progress_world(
                3 if tick % 2 == 0 else 4,
                carrier_unit=3 if tick < 15 else 4,
            ))
        output = SCORE.home_carrier_non_progress_metrics(
            {"worlds": worlds},
            [0, 1],
            [".......", ".......", "......."],
            {0: (1, 1), 1: (5, 1)},
        )
        self.assertTrue(output["barTrippedByTeam"]["0"])
        self.assertEqual(
            30,
            output["maxUncontestedTicksWithoutProgressByTeam"]["0"],
        )
        self.assertEqual(1, output["trippingRuns"][0]["carrierChanges"])
        self.assertEqual(2, output["trippingRuns"][0]["distinctPositions"])

    def test_home_progress_and_visible_contest_prevent_false_trip(self) -> None:
        progressing = [
            self.progress_world(5),
            *[self.progress_world(4) for _ in range(12)],
            *[self.progress_world(3) for _ in range(12)],
            *[self.progress_world(2) for _ in range(12)],
        ]
        progress_output = SCORE.home_carrier_non_progress_metrics(
            {"worlds": progressing},
            [0, 1],
            [".......", ".......", "......."],
            {0: (1, 1), 1: (5, 1)},
        )
        self.assertFalse(progress_output["barTrippedByTeam"]["0"])

        contested = [self.progress_world(3, enemy_x=4) for _ in range(45)]
        contest_output = SCORE.home_carrier_non_progress_metrics(
            {"worlds": contested},
            [0, 1],
            [".......", ".......", "......."],
            {0: (1, 1), 1: (5, 1)},
        )
        self.assertFalse(contest_output["barTrippedByTeam"]["0"])
        self.assertEqual(
            0,
            contest_output[
                "maxUncontestedTicksWithoutProgressByTeam"
            ]["0"],
        )


if __name__ == "__main__":
    unittest.main()
