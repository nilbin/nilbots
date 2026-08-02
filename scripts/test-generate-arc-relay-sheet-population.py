#!/usr/bin/env python3
"""Definition checks for the 32-sheet Arc Relay evaluation population."""

from __future__ import annotations

from collections import Counter
import importlib.util
import json
from pathlib import Path
import unittest


SCRIPT = Path(__file__).with_name("generate-arc-relay-sheet-population.py")
SPEC = importlib.util.spec_from_file_location("arc_sheet_population", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
POPULATION = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(POPULATION)


class SheetPopulationTests(unittest.TestCase):
    def test_expansion_has_twenty_unique_theses(self) -> None:
        ids = [entry["entrantId"] for entry in POPULATION.NEW_DOCTRINES]
        theses = [entry["thesis"] for entry in POPULATION.NEW_DOCTRINES]
        self.assertEqual(20, len(ids))
        self.assertEqual(20, len(set(ids)))
        self.assertEqual(20, len(set(theses)))

    def test_every_new_sheet_obeys_slot_and_copy_limits(self) -> None:
        for doctrine in POPULATION.NEW_DOCTRINES:
            with self.subTest(doctrine=doctrine["entrantId"]):
                self.assertEqual(8, len(doctrine["composition"]))
                self.assertLessEqual(
                    max(Counter(doctrine["composition"]).values()), 2
                )
                self.assertEqual(8, len(doctrine["slots"]))
                self.assertTrue(all(0 <= slot[2] < 8 for slot in doctrine["slots"]))

    def test_checked_population_passes_static_distinctness(self) -> None:
        value = json.loads(POPULATION.DISTINCTNESS.read_text(encoding="utf-8"))
        self.assertEqual(32, value["populationSize"])
        self.assertEqual(0, value["exactGameplayDuplicates"])
        self.assertTrue(all(
            row["passes"] for row in value["newSheetClosestPairs"]
        ))

    def test_cohort_is_derived_from_current_artifact_and_sheets(self) -> None:
        sheets = {
            path.stem: json.loads(path.read_text(encoding="utf-8"))
            for path in POPULATION.PACK.glob("*.json")
        }
        current = json.loads(POPULATION.COHORT.read_text(encoding="utf-8"))
        self.assertEqual(32, len(sheets))
        self.assertEqual(POPULATION.cohort(sheets), current)


if __name__ == "__main__":
    unittest.main()
