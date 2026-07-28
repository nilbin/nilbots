#!/usr/bin/env python3
"""Version-dispatch tests for replay-review-sample.py."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "scripts" / "replay-review-sample.py"
V1 = ROOT / "web" / "tests" / "fixtures" / "golden-replay.json"
V2 = ROOT / "web" / "tests" / "fixtures" / "frontline-replay-v2.json"
V3 = (
    ROOT
    / "tests"
    / "BotArena.Engine.Tests"
    / "Fixtures"
    / "generic-frontline-replay-v3.json"
)
SPEC = importlib.util.spec_from_file_location("replay_review_sample", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Could not load {SCRIPT}")
SAMPLER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SAMPLER)


class ReplayReviewSampleTests(unittest.TestCase):
    def test_v1_v2_and_v3_headers_normalize_without_reading_outcomes(
        self,
    ) -> None:
        v1 = SAMPLER.candidate(V1, 20260727)
        v2 = SAMPLER.candidate(V2, 20260727)
        v3 = SAMPLER.candidate(V3, 20260727)

        self.assertEqual(1, v1["replayVersion"])
        self.assertEqual("arena-01", v1["map"])
        self.assertEqual(2, v2["replayVersion"])
        self.assertEqual("frontline-test-anchor", v2["map"])
        self.assertEqual(3, v3["replayVersion"])
        self.assertEqual("generic-frontline-replay-v3-arena", v3["map"])
        for candidate in (v1, v2, v3):
            self.assertNotIn("winner", candidate)
            self.assertNotIn("reason", candidate)
            self.assertNotIn("duration", candidate)
            self.assertEqual(64, len(candidate["order"]))

    def test_selection_remains_deterministic_across_versions(self) -> None:
        candidates = [
            SAMPLER.candidate(V3, 17),
            SAMPLER.candidate(V2, 17),
            SAMPLER.candidate(V1, 17),
        ]

        first = SAMPLER.select(list(candidates), 3)
        second = SAMPLER.select(list(reversed(candidates)), 3)

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

    def test_identity_blind_manifest_hides_v3_bot_names(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source" / "replay.json"
            source.parent.mkdir()
            source.write_bytes(V3.read_bytes())
            (source.parent / "viewer.html").write_text(
                "standalone viewer must not be copied",
                encoding="utf-8",
            )
            source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
            output = root / "sample.json"
            package = root / "blind"
            subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    str(source),
                    "--count",
                    "1",
                    "--blind-identities",
                    "--copy-selected",
                    str(package),
                    "--output",
                    str(output),
                ],
                check=True,
                capture_output=True,
                text=True,
            )
            manifest = json.loads(output.read_text(encoding="utf-8"))
            review_index = json.loads(
                (package / "replays.json").read_text(encoding="utf-8")
            )
            copied_replay = package / "replays" / "sample-01.json"
            copied_hash = hashlib.sha256(copied_replay.read_bytes()).hexdigest()
            viewer_was_copied = (package / "viewer.html").exists()

        self.assertTrue(manifest["identitiesBlind"])
        self.assertEqual(
            ["Entrant A", "Entrant B"],
            manifest["replays"][0]["participants"],
        )
        self.assertNotIn("participant-10", json.dumps(manifest))
        self.assertNotIn(
            "generic-frontline-replay-v3.json",
            manifest["replays"][0]["source"],
        )
        self.assertEqual(source_hash, copied_hash)
        self.assertEqual(
            ["Entrant A", "Entrant B"],
            review_index[0]["bots"],
        )
        self.assertEqual(
            "replays/sample-01.json",
            review_index[0]["url"],
        )
        self.assertNotIn("participant-10", json.dumps(review_index))
        self.assertFalse(viewer_was_copied)
        self.assertEqual(
            [
                {"participantIndex": 0, "label": "Entrant A"},
                {"participantIndex": 1, "label": "Entrant B"},
            ],
            review_index[0]["identityAliases"],
        )


if __name__ == "__main__":
    unittest.main()
