#!/usr/bin/env python3
"""Unit checks for the Arc Relay sweep attempt state machine."""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


SCRIPT = Path(__file__).with_name("arc-relay-sweep.py")
SPEC = importlib.util.spec_from_file_location("arc_relay_sweep", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
SWEEP = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SWEEP)


def identity(surface: str = "a") -> dict[str, object]:
    return {
        "manifestSha256": "m",
        "sweepId": "s",
        "runtime": "wasm",
        "cli": "/tmp/botarena.dll",
        "executionSurfaceSha256": surface,
        "cellCount": 6,
    }


class AttemptSelectionTests(unittest.TestCase):
    def test_new_then_exact_incomplete_resume_reuses_attempt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "sweep"
            created = SWEEP.select_attempt(
                output, identity(), resume=False, relaunch=False)
            resumed = SWEEP.select_attempt(
                output, identity(), resume=True, relaunch=False)
            self.assertEqual(created, resumed)
            self.assertEqual("attempt-01", resumed.name)

    def test_changed_execution_surface_requires_relaunch(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "sweep"
            SWEEP.select_attempt(
                output, identity("a"), resume=False, relaunch=False)
            with self.assertRaisesRegex(ValueError, "kill-fix requires"):
                SWEEP.select_attempt(
                    output, identity("b"), resume=True, relaunch=False)
            relaunched = SWEEP.select_attempt(
                output, identity("b"), resume=False, relaunch=True)
            self.assertEqual("attempt-02", relaunched.name)

    def test_failed_attempt_cannot_resume_or_splice(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "sweep"
            attempt = SWEEP.select_attempt(
                output, identity(), resume=False, relaunch=False)
            (attempt / "FAILED.json").write_text(
                json.dumps({"failures": ["boom"]}), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "cannot resume"):
                SWEEP.select_attempt(
                    output, identity(), resume=True, relaunch=False)
            relaunched = SWEEP.select_attempt(
                output, identity(), resume=False, relaunch=True)
            self.assertEqual("attempt-02", relaunched.name)

    def test_completed_attempt_requires_new_whole_attempt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "sweep"
            attempt = SWEEP.select_attempt(
                output, identity(), resume=False, relaunch=False)
            (attempt / "COMPLETE.json").write_text("{}", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "complete"):
                SWEEP.select_attempt(
                    output, identity(), resume=True, relaunch=False)


if __name__ == "__main__":
    unittest.main()
