from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from scripts.assert_soundtrack_release import (
    ReleaseApprovalError,
    assert_soundtracks_shippable,
)


class AssertSoundtrackReleaseTests(unittest.TestCase):
    def test_pending_rights_and_audition_block_release(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            catalog = write_pack(
                Path(directory),
                rights_status="user-supplied-unverified",
                ship_approval="pending",
                loop_approval="analysis-reviewed",
            )

            with self.assertRaisesRegex(
                ReleaseApprovalError,
                "source rights are not cleared",
            ) as raised:
                assert_soundtracks_shippable(catalog)

            self.assertIn("ship approval is not approved", str(raised.exception))
            self.assertIn("still require human audition", str(raised.exception))

    def test_cleared_approved_and_auditioned_pack_can_ship(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            catalog = write_pack(
                Path(directory),
                rights_status="rights-cleared",
                ship_approval="approved",
                loop_approval="auditioned",
            )

            assert_soundtracks_shippable(catalog)

    def test_catalog_manifest_may_not_escape_soundtrack_root(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            catalog = root / "index.json"
            write_json(
                catalog,
                {
                    "schemaVersion": 1,
                    "defaultId": "candidate",
                    "tracks": [
                        {
                            "id": "candidate",
                            "title": "Candidate",
                            "manifest": "../manifest.json",
                        }
                    ],
                },
            )

            with self.assertRaisesRegex(
                ReleaseApprovalError,
                "safe relative path",
            ):
                assert_soundtracks_shippable(catalog)

    def test_unreferenced_public_pack_must_also_be_approved(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            catalog = write_pack(
                root,
                rights_status="rights-cleared",
                ship_approval="approved",
                loop_approval="auditioned",
            )
            old_version = root / "candidate" / "v1-deadbeefdeadbeef"
            old_version.mkdir(parents=True)
            write_json(
                old_version / "manifest.json",
                {
                    "schemaVersion": 1,
                    "id": "candidate",
                    "provenance": {
                        "rightsStatus": "user-supplied-unverified",
                        "shipApproval": "pending",
                    },
                    "sections": [],
                    "assets": {},
                },
            )

            with self.assertRaisesRegex(
                ReleaseApprovalError,
                "v1-deadbeefdeadbeef/manifest.json",
            ):
                assert_soundtracks_shippable(catalog)

    def test_encoded_media_must_be_declared_by_a_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            catalog = write_pack(
                root,
                rights_status="rights-cleared",
                ship_approval="approved",
                loop_approval="auditioned",
            )
            rogue = root / "candidate" / "v1-aabbccddeeff0011" / "rogue.m4a"
            rogue.write_bytes(b"not deployable")

            with self.assertRaisesRegex(
                ReleaseApprovalError,
                "encoded media is not declared",
            ):
                assert_soundtracks_shippable(catalog)


def write_pack(
    root: Path,
    *,
    rights_status: str,
    ship_approval: str,
    loop_approval: str,
) -> Path:
    version = root / "candidate" / "v1-aabbccddeeff0011"
    version.mkdir(parents=True)
    manifest = version / "manifest.json"
    write_json(
        manifest,
        {
            "schemaVersion": 1,
            "id": "candidate",
            "provenance": {
                "rightsStatus": rights_status,
                "shipApproval": ship_approval,
            },
            "sections": [
                {
                    "id": "loop",
                    "loop": {
                        "approvalStatus": loop_approval,
                        "auditionRequired": loop_approval != "auditioned",
                    },
                },
                {"id": "finite"},
            ],
            "assets": {},
        },
    )
    catalog = root / "index.json"
    write_json(
        catalog,
        {
            "schemaVersion": 1,
            "defaultId": "candidate",
            "tracks": [
                {
                    "id": "candidate",
                    "title": "Candidate",
                    "manifest": manifest.relative_to(root).as_posix(),
                }
            ],
        },
    )
    return catalog


def write_json(path: Path, value: object) -> None:
    path.write_text(json.dumps(value), encoding="utf-8")


if __name__ == "__main__":
    unittest.main()
