#!/usr/bin/env python3
"""Offline unit tests for the Bulwark avatar-derivation wrapper."""

from __future__ import annotations

import hashlib
import importlib.util
import tempfile
import unittest
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).with_name("derive_bulwark_avatar.py")
SPEC = importlib.util.spec_from_file_location("derive_bulwark_avatar", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("Could not load derive_bulwark_avatar.py")
DERIVER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(DERIVER)


class BulwarkAvatarDerivationWrapperTests(unittest.TestCase):
    def setUp(self) -> None:
        self.repo = DERIVER.repository_root()
        (self.repo / "var").mkdir(exist_ok=True)

    def test_sha256_argument_requires_exact_lowercase_hex(self) -> None:
        valid = "a" * 64
        self.assertEqual(DERIVER.sha256_argument(valid), valid)
        with self.assertRaises(Exception):
            DERIVER.sha256_argument("abc")
        with self.assertRaises(Exception):
            DERIVER.sha256_argument("g" * 64)

    def test_paths_are_repository_relative_and_output_roots_are_narrow(
        self,
    ) -> None:
        with self.assertRaisesRegex(ValueError, "repository-relative"):
            DERIVER.resolve_repo_path(
                Path("/tmp/source.blend"),
                repo_root=self.repo,
                option="--input-blend",
                strict=False,
            )
        with self.assertRaisesRegex(ValueError, "inside the repository"):
            DERIVER.resolve_repo_path(
                Path("../source.blend"),
                repo_root=self.repo,
                option="--input-blend",
                strict=False,
            )
        with self.assertRaisesRegex(ValueError, "must be below"):
            DERIVER.resolve_output(
                Path("art/not-an-avatar-proof"),
                repo_root=self.repo,
            )
        _resolved, relative = DERIVER.resolve_output(
            Path(
                "art/class-models/masters/aegis-tortoise-kinematic/"
                "proofs/test-avatar-output-that-does-not-exist"
            ),
            repo_root=self.repo,
        )
        self.assertEqual(
            relative,
            Path(
                "art/class-models/masters/aegis-tortoise-kinematic/"
                "proofs/test-avatar-output-that-does-not-exist"
            ),
        )

    def test_source_hash_mismatch_fails_before_blender_or_output_creation(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(
            prefix="bulwark-avatar-hash-test-",
            dir=self.repo / "var",
        ) as temporary:
            root = Path(temporary)
            source = root / "source.blend"
            source.write_bytes(b"not-a-real-blend")
            output = root / "fresh-output"
            with mock.patch.object(DERIVER, "resolve_blender") as resolver:
                with self.assertRaisesRegex(
                    ValueError,
                    "Source hash mismatch before Blender launch",
                ):
                    DERIVER.main(
                        [
                            "--input-blend",
                            source.relative_to(self.repo).as_posix(),
                            "--expected-source-sha256",
                            "0" * 64,
                            "--output-dir",
                            output.relative_to(self.repo).as_posix(),
                        ]
                    )
            resolver.assert_not_called()
            self.assertFalse(output.exists())

    def test_existing_output_fails_before_blender_invocation(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="bulwark-avatar-overwrite-test-",
            dir=self.repo / "var",
        ) as temporary:
            root = Path(temporary)
            source = root / "source.blend"
            source.write_bytes(b"not-a-real-blend")
            output = root / "existing-output"
            output.mkdir()
            source_sha = hashlib.sha256(source.read_bytes()).hexdigest()
            with mock.patch.object(DERIVER, "resolve_blender") as resolver:
                with self.assertRaisesRegex(ValueError, "refusing overwrite"):
                    DERIVER.main(
                        [
                            "--input-blend",
                            source.relative_to(self.repo).as_posix(),
                            "--expected-source-sha256",
                            source_sha,
                            "--output-dir",
                            output.relative_to(self.repo).as_posix(),
                        ]
                    )
            resolver.assert_not_called()

    def test_report_artifacts_are_exactly_two_and_hash_pinned(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="bulwark-avatar-report-test-",
            dir=self.repo / "var",
        ) as temporary:
            output = Path(temporary)
            avatar = output / "avatar.png"
            mask = output / "mask.png"
            avatar.write_bytes(b"avatar")
            mask.write_bytes(b"mask")
            relative_output = output.relative_to(self.repo)
            source_relative = Path("sandbox/source.blend")
            source_sha = "1" * 64
            report = {
                "source": {
                    "path": source_relative.as_posix(),
                    "sha256": source_sha,
                },
                "outputs": {
                    "avatar": {
                        "path": avatar.relative_to(self.repo).as_posix(),
                        "sha256": hashlib.sha256(b"avatar").hexdigest(),
                    },
                    "teamAccentMask": {
                        "path": mask.relative_to(self.repo).as_posix(),
                        "sha256": hashlib.sha256(b"mask").hexdigest(),
                    },
                },
            }
            DERIVER.verify_artifacts(
                report,
                repo_root=self.repo,
                expected_source_relative=source_relative,
                expected_source_sha256=source_sha,
                expected_output_relative=relative_output,
            )
            report["outputs"]["avatar"]["sha256"] = "0" * 64
            with self.assertRaisesRegex(ValueError, "hash drifted"):
                DERIVER.verify_artifacts(
                    report,
                    repo_root=self.repo,
                    expected_source_relative=source_relative,
                    expected_source_sha256=source_sha,
                    expected_output_relative=relative_output,
                )


if __name__ == "__main__":
    unittest.main()
