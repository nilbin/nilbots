#!/usr/bin/env python3
"""Unit checks for Arc Relay depth/map reduction helpers."""

from __future__ import annotations

import importlib.util
from pathlib import Path
import unittest


SCRIPT = Path(__file__).with_name("arc-relay-depth-map-read.py")
SPEC = importlib.util.spec_from_file_location("arc_relay_depth_map_read", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
READ = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(READ)


class DepthMapReadTests(unittest.TestCase):
    @staticmethod
    def broadcast(north: int, centre: int, south: int, height: int) -> dict:
        world = [None] * 8
        world[4] = [
            [0, 0, 1, 0, 0, "arc-body-kestrel", 5, north, "east", 3],
            [1, 0, 1, 1, 0, "arc-body-kestrel", 25, north, "west", 3],
        ]
        world[7] = {"kind": "arc-relay", "visibleCores": []}
        return {
            "header": {
                "contract": {
                    "map": {
                        "width": 31,
                        "height": height,
                        "tileRows": ["." * 31 for _ in range(height)],
                        "regions": [
                            {
                                "regionId": "well-north",
                                "tiles": [[15, north]],
                            },
                            {
                                "regionId": "well-centre",
                                "tiles": [[15, centre]],
                            },
                            {
                                "regionId": "well-south",
                                "tiles": [[15, south]],
                            },
                        ],
                    },
                },
            },
            "worlds": [world],
            "turns": [[
                [[0, 0, 1], None, None, None, None, ["move-eight-way"], None],
                [[1, 0, 1], None, None, None, None, ["move-eight-way"], None],
            ]],
        }

    def test_coarse_opening_is_team_normalized(self) -> None:
        broadcast = self.broadcast(4, 11, 18, 23)
        self.assertEqual(
            READ.coarse_opening_archetype(broadcast, 0, 1),
            READ.coarse_opening_archetype(broadcast, 1, 1),
        )

    def test_opening_bands_follow_well_spacing(self) -> None:
        baseline = READ.opening_bands(self.broadcast(4, 11, 18, 23))
        larger = READ.opening_bands(self.broadcast(4, 14, 24, 29))
        self.assertEqual((7, 15), (
            baseline["northMaximumY"], baseline["southMinimumY"]))
        self.assertEqual((9, 19), (
            larger["northMaximumY"], larger["southMinimumY"]))


if __name__ == "__main__":
    unittest.main()
