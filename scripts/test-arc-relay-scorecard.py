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


if __name__ == "__main__":
    unittest.main()
