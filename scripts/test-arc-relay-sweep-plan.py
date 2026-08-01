#!/usr/bin/env python3
"""Unit checks for sparse Arc Relay sweep-plan selection."""

from __future__ import annotations

import importlib.util
from pathlib import Path
import unittest


SCRIPT = Path(__file__).with_name("arc-relay-sweep-plan.py")
SPEC = importlib.util.spec_from_file_location("arc_relay_sweep_plan", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
PLAN = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PLAN)


class PairSelectionTests(unittest.TestCase):
    def test_pair_selects_both_assignments(self) -> None:
        selected = PLAN.selected_pair_assignments(
            ["balanced,relay"], [], {"balanced", "relay"})
        self.assertEqual({0, 1}, selected[frozenset(("balanced", "relay"))])

    def test_pair_assignment_can_freeze_one_side(self) -> None:
        selected = PLAN.selected_pair_assignments(
            [], ["balanced,relay,1"], {"balanced", "relay"})
        self.assertEqual({1}, selected[frozenset(("balanced", "relay"))])

    def test_pair_modes_are_mutually_exclusive(self) -> None:
        with self.assertRaisesRegex(ValueError, "not both"):
            PLAN.selected_pair_assignments(
                ["balanced,relay"],
                ["balanced,relay,0"],
                {"balanced", "relay"},
            )

    def test_unknown_and_duplicate_assignments_fail(self) -> None:
        with self.assertRaisesRegex(ValueError, "unknown"):
            PLAN.selected_pair_assignments(
                [], ["balanced,missing,0"], {"balanced", "relay"})
        with self.assertRaisesRegex(ValueError, "duplicate"):
            PLAN.selected_pair_assignments(
                [],
                ["balanced,relay,0", "relay,balanced,0"],
                {"balanced", "relay"},
            )


if __name__ == "__main__":
    unittest.main()
