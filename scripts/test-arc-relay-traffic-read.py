#!/usr/bin/env python3
"""Unit checks for successful-but-circular Arc Relay traffic reads."""

from __future__ import annotations

import importlib.util
from pathlib import Path
import unittest


SCRIPT = Path(__file__).with_name("arc-relay-traffic-read.py")
SPEC = importlib.util.spec_from_file_location("arc_relay_traffic_read", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
TRAFFIC = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(TRAFFIC)


class SquadLoopTests(unittest.TestCase):
    @staticmethod
    def frame(tick: int, positions, makeway=()):
        return {
            "tick": tick,
            "bodies": {
                (unit, 1): position
                for unit, position in enumerate(positions)
            },
            "makeway": set(makeway),
        }

    def test_three_body_passage_rotation_is_one_sustained_loop(self) -> None:
        a = [(8, 8), (7, 9), (7, 8)]
        b = [(8, 8), (7, 9), (8, 9)]
        c = [(8, 8), (8, 9), (7, 8)]
        frames = [
            self.frame(0, a),
            self.frame(1, b, [2]),
            self.frame(2, a, [2]),
            self.frame(3, c, [1]),
            self.frame(4, a, [1]),
            self.frame(5, b, [2]),
            self.frame(6, a, [2]),
        ]

        episodes = TRAFFIC.squad_loop_episodes(frames)

        self.assertEqual(1, len(episodes))
        self.assertEqual(2, episodes[0]["period"])
        self.assertEqual(0, episodes[0]["start"])
        self.assertEqual(6, episodes[0]["end"])

    def test_stationary_or_linear_squad_is_not_a_successful_circuit(self) -> None:
        stationary = [
            self.frame(tick, [(8, 8), (7, 9), (7, 8)])
            for tick in range(8)
        ]
        linear = [
            self.frame(tick, [
                (8 + tick, 8),
                (7 + tick, 9),
                (7 + tick, 8),
            ], [0])
            for tick in range(8)
        ]

        self.assertEqual([], TRAFFIC.squad_loop_episodes(stationary))
        self.assertEqual([], TRAFFIC.squad_loop_episodes(linear))


if __name__ == "__main__":
    unittest.main()
