#!/usr/bin/env python3
"""Definition checks for the Arc Relay doctrine expansion."""

from __future__ import annotations

from collections import Counter
import importlib.util
import json
from pathlib import Path
import unittest


SCRIPT = Path(__file__).with_name(
    "generate-arc-relay-doctrine-expansion.py")
SPEC = importlib.util.spec_from_file_location("arc_relay_doctrines", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
DOCTRINES = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(DOCTRINES)


class DoctrineDefinitionTests(unittest.TestCase):
    def test_pack_has_twelve_unique_entrants(self) -> None:
        ids = [entrant_id for entrant_id, _ in DOCTRINES.ENTRANTS]
        self.assertEqual(12, len(ids))
        self.assertEqual(12, len(set(ids)))
        self.assertEqual(7, len(DOCTRINES.DOCTRINES))

    def test_every_expansion_sheet_obeys_slot_limits(self) -> None:
        for doctrine in DOCTRINES.DOCTRINES:
            with self.subTest(doctrine=doctrine["sheetId"]):
                self.assertEqual(8, len(doctrine["composition"]))
                self.assertLessEqual(max(Counter(
                    doctrine["composition"]).values()), 2)
                self.assertEqual(8, len(doctrine["slots"]))
                self.assertTrue(all(
                    0 <= slot[2] < 8 for slot in doctrine["slots"]))

    def test_cohort_is_derived_from_current_artifact_and_sheets(self) -> None:
        current = json.loads(DOCTRINES.COHORT.read_text(encoding="utf-8"))
        self.assertEqual(DOCTRINES.cohort(), current)


if __name__ == "__main__":
    unittest.main()
