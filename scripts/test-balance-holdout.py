#!/usr/bin/env python3
"""Tests for the Balance Lab holdout commitment protocol."""

from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "scripts" / "balance-holdout.py"
SPEC = importlib.util.spec_from_file_location("balance_holdout", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"could not load {SCRIPT}")
HOLDOUT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(HOLDOUT)


class BalanceHoldoutTests(unittest.TestCase):
    def test_create_verify_consume_and_refuse_reuse(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            private = root / "private" / "reveal.json"
            commitment = root / "public" / "commitment.json"
            created = HOLDOUT.create(
                "frontline-pilot",
                4,
                private,
                commitment,
                {1, 2, 3},
            )

            self.assertEqual(4, created["seedCount"])
            _, reveal = HOLDOUT.verify(commitment, private)
            self.assertEqual(4, len(reveal["seeds"]))
            self.assertTrue({1, 2, 3}.isdisjoint(reveal["seeds"]))

            consumed = HOLDOUT.consume(
                commitment,
                private,
                root / "consumed",
            )
            self.assertTrue(consumed.is_file())
            with self.assertRaises(FileExistsError):
                HOLDOUT.consume(
                    commitment,
                    private,
                    root / "consumed",
                )

    def test_tampered_reveal_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            private = root / "reveal.json"
            commitment = root / "commitment.json"
            HOLDOUT.create(
                "frontline-pilot",
                2,
                private,
                commitment,
                set(),
            )
            reveal = json.loads(private.read_text(encoding="utf-8"))
            reveal["seeds"][0] += 1
            private.write_text(json.dumps(reveal), encoding="utf-8")

            with self.assertRaises(ValueError):
                HOLDOUT.verify(commitment, private)


if __name__ == "__main__":
    unittest.main()
