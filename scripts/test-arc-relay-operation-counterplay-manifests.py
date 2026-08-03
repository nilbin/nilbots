#!/usr/bin/env python3
"""Integrity checks for the retained Arc Relay operation counterplay pass."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import unittest


REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / "arena-bots/arc-relay/operation-counterplay-v1-2026-08-03"


def read(name: str) -> dict:
    return json.loads((ROOT / name).read_text(encoding="utf-8"))


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class OperationCounterplayManifestTests(unittest.TestCase):
    def test_discovery_is_multi_seed_and_covers_the_real_population(self) -> None:
        manifest = read("discovery-manifest.json")
        cells = manifest["cells"]
        self.assertEqual(240, len(cells))
        self.assertEqual(10, len({cell["operationId"] for cell in cells}))
        self.assertEqual(32, len({cell["opponentId"] for cell in cells}))
        self.assertEqual(3, len({str(cell["seed"]) for cell in cells}))
        self.assertGreater(
            len({cell["topologyFingerprint"] for cell in cells}),
            1,
            "ordered class compositions must not reuse one stale topology hash",
        )
        for operation_id in {cell["operationId"] for cell in cells}:
            rows = [cell for cell in cells if cell["operationId"] == operation_id]
            self.assertEqual(24, len(rows))
            self.assertEqual(8, len({cell["opponentId"] for cell in rows}))
            self.assertEqual({0, 1}, {cell["operationTeamId"] for cell in rows})

    def test_confirmation_is_held_out_and_freezes_the_amended_taxonomy(self) -> None:
        discovery = read("discovery-manifest.json")
        confirmation = read("confirmation-manifest.json")
        cells = confirmation["cells"]
        self.assertEqual(40, len(cells))
        self.assertTrue(
            {str(cell["seed"]) for cell in cells}.isdisjoint(
                {str(cell["seed"]) for cell in discovery["cells"]}
            )
        )
        for operation_id in {cell["operationId"] for cell in cells}:
            rows = [cell for cell in cells if cell["operationId"] == operation_id]
            self.assertEqual(4, len(rows))
            self.assertEqual(2, len({cell["opponentId"] for cell in rows}))
            self.assertEqual({0, 1}, {cell["operationTeamId"] for cell in rows})
        taxonomy = REPO / confirmation["counterTaxonomy"]
        self.assertEqual(
            confirmation["counterTaxonomySha256"],
            sha256(taxonomy),
        )

    def test_reads_preserve_the_strict_miss_and_pass_held_out_taxonomy(self) -> None:
        discovery = read("discovery-read.json")
        confirmation = read("confirmation-read.json")
        self.assertTrue(discovery["allRequirementsMet"])
        self.assertFalse(discovery["allCommittedOnlyRequirementsMet"])
        strict_missing = {
            row["id"] for row in discovery["operations"]
            if not row["committedOnlyRequirementsMet"]
        }
        self.assertEqual({"hardlight-gate", "lantern-sweep"}, strict_missing)
        self.assertTrue(confirmation["allRequirementsMet"])
        self.assertEqual(40, confirmation["eligibleCells"])
        self.assertEqual(40, confirmation["readCells"])
        self.assertEqual(
            0,
            sum(row["strandedActivations"] for row in confirmation["operations"]),
        )

    def test_retained_matches_are_complete_hashed_and_under_budget(self) -> None:
        evidence = read("retained/retained-evidence.json")
        matches = evidence["retainedMatches"]
        self.assertEqual(10, len(matches))
        self.assertEqual(10, len({row["operationId"] for row in matches}))
        self.assertTrue(evidence["allRequirementsMet"])
        for row in matches:
            self.assertTrue(row["success"]["qualifies"]["success"])
            self.assertTrue(row["counter"]["qualifies"]["counter"])
            self.assertTrue(
                row["casualtyRecovery"]["qualifies"]["casualtyRecovery"]
            )
            self.assertEqual(0, row["strandedActivations"])
            for kind in ("record", "broadcast", "scorecard"):
                path = REPO / row[kind]["file"]
                self.assertEqual(row[kind]["sha256"], sha256(path))
            self.assertLessEqual(
                row["record"]["bytes"],
                evidence["budgets"]["recordBytesPerMatch"],
            )
            self.assertLessEqual(
                row["broadcast"]["gzipBytes"],
                evidence["budgets"]["broadcastGzipBytesPerMatch"],
            )
        self.assertLessEqual(
            evidence["budgets"]["actualGalleryBroadcastGzipBytes"],
            evidence["budgets"]["galleryBroadcastGzipBytes"],
        )


if __name__ == "__main__":
    unittest.main()
