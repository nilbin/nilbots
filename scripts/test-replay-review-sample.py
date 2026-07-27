#!/usr/bin/env python3
"""Version-dispatch tests for replay-review-sample.py."""

from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "scripts" / "replay-review-sample.py"
V1 = ROOT / "web" / "tests" / "fixtures" / "golden-replay.json"
V2 = ROOT / "web" / "tests" / "fixtures" / "frontline-replay-v2.json"
SPEC = importlib.util.spec_from_file_location("replay_review_sample", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Could not load {SCRIPT}")
SAMPLER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SAMPLER)


class ReplayReviewSampleTests(unittest.TestCase):
    def test_v1_and_v2_headers_normalize_without_reading_outcomes(self) -> None:
        v1 = SAMPLER.candidate(V1, 20260727)
        v2 = SAMPLER.candidate(V2, 20260727)

        self.assertEqual(1, v1["replayVersion"])
        self.assertEqual("arena-01", v1["map"])
        self.assertEqual(2, v2["replayVersion"])
        self.assertEqual("frontline-test-anchor", v2["map"])
        for candidate in (v1, v2):
            self.assertNotIn("winner", candidate)
            self.assertNotIn("reason", candidate)
            self.assertNotIn("duration", candidate)
            self.assertEqual(64, len(candidate["order"]))

    def test_selection_remains_deterministic_across_versions(self) -> None:
        candidates = [
            SAMPLER.candidate(V2, 17),
            SAMPLER.candidate(V1, 17),
        ]

        first = SAMPLER.select(list(candidates), 2)
        second = SAMPLER.select(list(reversed(candidates)), 2)

        self.assertEqual(
            [item["source"] for item in first],
            [item["source"] for item in second],
        )

    def test_shared_artifact_doctrines_and_duplicate_headers_are_stable(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            first_path = root / "a" / "replay.json"
            second_path = root / "b" / "replay.json"
            third_path = root / "c" / "replay.json"
            first_path.parent.mkdir()
            second_path.parent.mkdir()
            third_path.parent.mkdir()

            original = json.loads(V2.read_text(encoding="utf-8"))
            changed = json.loads(V2.read_text(encoding="utf-8"))
            changed["header"]["participants"][0]["name"] = (
                "Different Doctrine"
            )
            first_path.write_text(json.dumps(original), encoding="utf-8")
            second_path.write_text(json.dumps(original), encoding="utf-8")
            third_path.write_text(json.dumps(changed), encoding="utf-8")

            candidates = [
                SAMPLER.candidate(first_path, 23),
                SAMPLER.candidate(second_path, 23),
                SAMPLER.candidate(third_path, 23),
            ]

        self.assertEqual(candidates[0]["order"], candidates[1]["order"])
        self.assertNotEqual(candidates[0]["order"], candidates[2]["order"])
        forward = SAMPLER.select(list(candidates), 3)
        reverse = SAMPLER.select(list(reversed(candidates)), 3)
        self.assertEqual(
            [item["source"] for item in forward],
            [item["source"] for item in reverse],
        )


if __name__ == "__main__":
    unittest.main()
