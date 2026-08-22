#!/usr/bin/env python3
"""Regression tests for the Arc Relay map analyzer."""

from __future__ import annotations

import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest


SCRIPT_DIRECTORY = Path(__file__).resolve().parent
REPOSITORY_ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(SCRIPT_DIRECTORY))

import map_analysis  # noqa: E402


EXAMPLE = (
    REPOSITORY_ROOT
    / ".claude/skills/analyze-arc-relay-map/references"
    / "tapline-equal-return-candidate.json"
)


class GridTests(unittest.TestCase):
    def test_corner_strict_rule_blocks_a_false_diagonal(self) -> None:
        grid = map_analysis.Grid([list(".#."), list("..."), list("...")])

        self.assertFalse(grid.step_ok((0, 0), (1, 1)))
        path = grid.shortest_path((0, 0), (1, 1))
        self.assertIsNotNone(path)
        self.assertEqual(len(path) - 1, 2)

    def test_authored_route_rejects_an_illegal_step(self) -> None:
        spec = {
            "map": {"rows": [".#", "#."]},
            "landmarks": [
                {"id": "start", "position": [0, 0]},
                {"id": "end", "position": [1, 1]},
            ],
            "validateCutVertices": False,
            "routes": [
                {
                    "id": "illegal",
                    "from": "start",
                    "to": "end",
                    "mode": "authored",
                    "path": [[0, 0], [1, 1]],
                }
            ],
        }

        result = map_analysis.analyze_spec(spec, Path("test.json"))

        self.assertFalse(result["valid"])
        self.assertTrue(any("illegal step" in error for error in result["errors"]))

    def test_custom_wall_symbol_is_preserved_for_the_atlas(self) -> None:
        spec = {
            "map": {"rows": ["xxx", "x.x", "xxx"], "wall": "x"},
            "validateCutVertices": False,
        }

        result = map_analysis.analyze_spec(spec, Path("test.json"))

        self.assertTrue(result["valid"], result["errors"])
        self.assertEqual(result["wall"], "x")
        self.assertIn("tile===data.wall", map_analysis.render_html(result))

    def test_corner_strict_vision_cannot_see_through_a_wall_corner(self) -> None:
        grid = map_analysis.Grid([list(".#."), list("#.."), list("...")])

        self.assertFalse(grid.has_line_of_sight((0, 0), (1, 1)))


class TaplineCandidateTests(unittest.TestCase):
    def setUp(self) -> None:
        self.spec = json.loads(EXAMPLE.read_text(encoding="utf-8"))

    def test_pins_landmarks_topology_and_every_route_family(self) -> None:
        result = map_analysis.analyze_spec(self.spec, EXAMPLE)

        self.assertTrue(result["valid"], result["errors"])
        self.assertEqual(
            {landmark["id"] for landmark in result["landmarks"]},
            {
                "own-reactor",
                "enemy-reactor",
                "own-well",
                "centre-well",
                "enemy-well",
                "north-healing",
                "south-healing",
            },
        )
        self.assertEqual(
            {route["id"]: route["steps"] for route in result["routes"]},
            {
                "own-direct": 12,
                "centre-north": 19,
                "centre-south": 19,
                "centre-middle-north": 28,
                "centre-middle-south": 44,
                "centre-full-north": 47,
                "centre-full-south": 65,
                "enemy-direct": 25,
                "enemy-via-centre-north": 26,
                "enemy-via-centre-south": 26,
                "enemy-middle-north": 35,
                "enemy-middle-south": 41,
                "enemy-full-north": 52,
                "enemy-full-south": 58,
            },
        )
        self.assertEqual(result["topology"]["connectedComponents"], [523])
        for x, y in ((10, 9), (11, 9), (19, 17), (20, 17)):
            self.assertEqual(result["rows"][y][x], "#")
        self.assertEqual(result["topology"]["falseCornerTouches"], [])
        self.assertEqual(result["topology"]["cutVertices"], [])
        self.assertEqual(result["topology"]["symmetryMismatches"], [])
        self.assertEqual(result["topology"]["landmarkSymmetryMismatches"], [])
        self.assertEqual(result["atlasRequirements"], {
            "distances": True,
            "combatRanges": True,
        })
        combat = result["combatReference"]
        self.assertIsNotNone(combat)
        self.assertEqual(combat["vision"]["range"], 5)
        self.assertEqual(
            {attack["id"]: attack["range"] for attack in combat["attacks"]},
            {"short-fast": 3, "medium-steady": 4, "long-slow": 6},
        )
        self.assertIn([15, 13], combat["vision"]["visibleTiles"])

    def test_required_own_well_cannot_be_silently_omitted(self) -> None:
        spec = copy.deepcopy(self.spec)
        spec["landmarks"] = [
            landmark
            for landmark in spec["landmarks"]
            if landmark["id"] != "own-well"
        ]
        spec["routes"] = []

        result = map_analysis.analyze_spec(spec, EXAMPLE)

        self.assertFalse(result["valid"])
        self.assertIn("expected 3 well landmarks, found 2", result["errors"])
        self.assertIn("missing required landmarks: own-well", result["errors"])

    def test_required_route_family_cannot_be_silently_omitted(self) -> None:
        spec = copy.deepcopy(self.spec)
        spec["routes"] = [
            route
            for route in spec["routes"]
            if route["id"] != "centre-full-south"
        ]

        result = map_analysis.analyze_spec(spec, EXAMPLE)

        self.assertFalse(result["valid"])
        self.assertIn(
            "missing required routes: centre-full-south",
            result["errors"],
        )

    def test_distance_atlas_rejects_an_unpinned_route(self) -> None:
        spec = copy.deepcopy(self.spec)
        del spec["routes"][0]["expectedSteps"]

        result = map_analysis.analyze_spec(spec, EXAMPLE)

        self.assertFalse(result["valid"])
        self.assertIn(
            "distance atlas routes must pin expectedSteps: own-direct",
            result["errors"],
        )

    def test_combat_atlas_requires_all_three_profiles(self) -> None:
        spec = copy.deepcopy(self.spec)
        spec["combatReference"]["attacks"].pop()

        result = map_analysis.analyze_spec(spec, EXAMPLE)

        self.assertFalse(result["valid"])
        self.assertIn(
            "combat range reference must declare all three attack profiles; found 2",
            result["errors"],
        )

    def test_html_names_all_wells_and_both_healing_tiles(self) -> None:
        result = map_analysis.analyze_spec(self.spec, EXAMPLE)
        document = map_analysis.render_html(result)

        for expected in (
            "Own Well (1)",
            "Centre Well (2)",
            "Enemy Well (4)",
            "North healing tile",
            "South healing tile",
        ):
            self.assertIn(expected, document)
        self.assertLess(
            document.index("data.landmarks.forEach"),
            document.index("drawReferenceHandle(scene,cell,combat);"),
        )
        for expected in (
            "Combat range reference",
            "Base vision",
            "data-vision-cone",
            "data-reference-handle",
            "Rotate reference",
            "Reset reference",
            "visibleTilesFor",
            "geometric base-vision cone",
            "Short-fast",
            "Medium-steady",
            "Long-slow",
            "Attack contours are distance envelopes",
        ):
            self.assertIn(expected, document)

    def test_tactical_sites_and_comparison_reach_json_and_html(self) -> None:
        spec = copy.deepcopy(self.spec)
        spec["tacticalSites"] = [
            {
                "id": "centre-plaza",
                "label": "Centre plaza",
                "note": "mouths and cover",
                "marks": [
                    {"kind": "mouth", "label": "W mouth", "tiles": [[14, 13]]},
                    {"kind": "post", "label": "guard", "tiles": [[16, 13]],
                     "facing": "east"},
                    {"kind": "seal", "tiles": [[15, 12]]},
                    {"kind": "lane", "label": "rail", "from": [16, 13],
                     "to": [20, 13]},
                    {"kind": "route", "label": "break",
                     "path": [[16, 13], [17, 13], [18, 13]]},
                    {"kind": "zone", "label": "floor", "tiles": [[17, 12]]},
                ],
            }
        ]
        spec["comparison"] = {
            "baselineLabel": "Tapline15 (live)",
            "candidateLabel": "Candidate",
            "rows": [
                {"metric": "Own Well return", "baseline": 12, "candidate": 8,
                 "note": "shorter"},
            ],
        }

        result = map_analysis.analyze_spec(spec, EXAMPLE)

        self.assertTrue(result["valid"], result["errors"])
        self.assertEqual(result["tacticalSites"][0]["id"], "centre-plaza")
        self.assertEqual(
            result["comparison"]["rows"][0]["baseline"], "12"
        )
        document = map_analysis.render_html(result)
        for expected in (
            "Centre plaza",
            "data-site-toggle",
            "Baseline comparison",
            "Tapline15 (live)",
            "Own Well return",
            "drawSites(scene,cell);",
        ):
            self.assertIn(expected, document)

    def test_tactical_site_rejects_a_walled_tile(self) -> None:
        spec = copy.deepcopy(self.spec)
        spec["tacticalSites"] = [
            {
                "id": "bad",
                "label": "Bad",
                "marks": [{"kind": "post", "tiles": [[0, 0]]}],
            }
        ]

        with self.assertRaises(map_analysis.SpecError):
            map_analysis.analyze_spec(spec, EXAMPLE)

    def test_tactical_site_rejects_a_bent_lane(self) -> None:
        spec = copy.deepcopy(self.spec)
        spec["tacticalSites"] = [
            {
                "id": "bad-lane",
                "label": "Bad lane",
                "marks": [{"kind": "lane", "from": [14, 13], "to": [16, 12]}],
            }
        ]

        with self.assertRaises(map_analysis.SpecError):
            map_analysis.analyze_spec(spec, EXAMPLE)

    def test_comparison_requires_both_sides_of_every_row(self) -> None:
        spec = copy.deepcopy(self.spec)
        spec["comparison"] = {
            "baselineLabel": "Baseline",
            "candidateLabel": "Candidate",
            "rows": [{"metric": "Own Well return", "baseline": 12}],
        }

        with self.assertRaises(map_analysis.SpecError):
            map_analysis.analyze_spec(spec, EXAMPLE)

    def test_cli_writes_json_and_self_contained_html(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            exit_code = map_analysis.main(
                [
                    str(EXAMPLE),
                    "--json-out",
                    str(output / "analysis.json"),
                    "--html-out",
                    str(output / "atlas.html"),
                ]
            )

            self.assertEqual(exit_code, 0)
            self.assertTrue(json.loads((output / "analysis.json").read_text())["valid"])
            self.assertIn("<svg id=\"map\"", (output / "atlas.html").read_text())


if __name__ == "__main__":
    unittest.main()
