#!/usr/bin/env python3
"""Focused offline tests for the Meshy multiview runner."""

from __future__ import annotations

from contextlib import redirect_stdout
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch
import zlib


SCRIPT = Path(__file__).with_name("meshy_multi_image.py")
SPEC = importlib.util.spec_from_file_location("meshy_multi_image", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot import {SCRIPT}")
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


def png_chunk(kind: bytes, payload: bytes) -> bytes:
    return (
        struct.pack(">I", len(payload))
        + kind
        + payload
        + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)
    )


def write_png(path: Path, width: int = 2, height: int = 2) -> None:
    row = b"\x00" + b"\x33\x66\x99\xff" * width
    payload = row * height
    path.write_bytes(
        MODULE.PNG_SIGNATURE
        + png_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
        + png_chunk(b"IDAT", zlib.compress(payload))
        + png_chunk(b"IEND", b"")
    )


class MeshyMultiImageTests(unittest.TestCase):
    def make_package(
        self,
        root: Path,
        *,
        allowed: bool = True,
        blockers: list[str] | None = None,
    ) -> Path:
        package = (
            root
            / "art/class-models/provider-inputs/meshy/aegis-tortoise-mobile"
        )
        package.mkdir(parents=True)
        image = package / "01-top.png"
        write_png(image)
        digest = hashlib.sha256(image.read_bytes()).hexdigest()
        manifest = {
            "schemaVersion": 1,
            "assetId": "aegis-tortoise-mobile",
            "provider": "meshy",
            "taskKind": "image-to-3d-multiview",
            "status": "owner-approved-provider-ready",
            "sourceSheet": {
                "path": "01-top.png",
                "sha256": digest,
                "width": 2,
                "height": 2,
            },
            "topAuthority": {
                "path": "01-top.png",
                "sha256": digest,
                "kind": "canonical-2d",
            },
            "orientation": {
                "modelForward": "+X",
                "imageRight": "+X",
                "worldUp": "+Y",
            },
            "views": [
                {
                    "role": "top",
                    "path": "01-top.png",
                    "width": 2,
                    "height": 2,
                    "sha256": digest,
                }
            ],
            "blockers": blockers or [],
            "providerSubmission": {
                "allowed": allowed,
                "purpose": "Approved Bulwark mobile base-mesh proof",
                "expectedCredits": 30,
                "texturePrompt": "Matte graphite and bronze armor.",
            },
        }
        manifest_path = package / "manifest.json"
        manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
        return manifest_path

    def test_default_package_mode_is_offline_dry_run(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = self.make_package(root)
            output = io.StringIO()
            with (
                patch.object(MODULE, "repo_root_from_script", return_value=root),
                patch.object(
                    MODULE,
                    "read_keychain_token",
                    side_effect=AssertionError("credential read"),
                ),
                redirect_stdout(output),
            ):
                self.assertEqual(
                    MODULE.main(["--package", str(manifest)]),
                    0,
                )
            self.assertIn("OFFLINE DRY RUN", output.getvalue())
            self.assertIn("Submission gate: READY", output.getvalue())

    def test_blocked_package_cannot_pass_submission_gate(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = self.make_package(
                root,
                allowed=False,
                blockers=["top overlay needs owner approval"],
            )
            plan = MODULE.validate_manifest(manifest, root)
            with self.assertRaisesRegex(
                MODULE.RunnerError,
                "provider submission is blocked",
            ):
                MODULE.enforce_submission_gate(plan, 30)

    def test_credit_guard_is_exactly_thirty(self) -> None:
        MODULE.enforce_credit_guard(30)
        for value in (None, 29, 31, True):
            with self.subTest(value=value):
                with self.assertRaises(MODULE.RunnerError):
                    MODULE.enforce_credit_guard(value)

    def test_sanitizer_removes_secrets_data_uris_and_signed_queries(self) -> None:
        value = {
            "Authorization": "Bearer never-record-this",
            "task_error": {
                "message": (
                    "invalid input data:image/png;base64,never-record-this"
                )
            },
            "model_urls": {
                "glb": (
                    "https://assets.meshy.ai/task/model.glb"
                    "?Expires=1&Signature=secret"
                )
            },
        }
        sanitized = MODULE.sanitize_for_record(value)
        encoded = json.dumps(sanitized)
        self.assertNotIn("Authorization", sanitized)
        self.assertNotIn("never-record-this", encoded)
        self.assertEqual(
            sanitized["task_error"]["message"],
            "<redacted-data-uri>",
        )
        self.assertNotIn("Signature", encoded)
        self.assertEqual(
            sanitized["model_urls"]["glb"],
            "https://assets.meshy.ai/task/model.glb",
        )

    def test_download_plan_keeps_all_outputs_and_deduplicates_preview(self) -> None:
        task = {
            "model_urls": {
                "glb": "https://assets.meshy.ai/task/model.glb?signature=one"
            },
            "thumbnail_url": (
                "https://assets.meshy.ai/task/preview.png?signature=one"
            ),
            "thumbnail_urls": {
                "front": (
                    "https://assets.meshy.ai/task/preview.png?signature=two"
                ),
                "right": (
                    "https://assets.meshy.ai/task/preview-right.png?signature=one"
                ),
            },
            "texture_urls": [
                {
                    "base_color": (
                        "https://assets.meshy.ai/task/base.png?signature=one"
                    ),
                    "normal": (
                        "https://assets.meshy.ai/task/normal.png?signature=one"
                    ),
                }
            ],
        }
        downloads = MODULE.collect_downloads(task)
        self.assertEqual(
            [download.filename for download in downloads],
            [
                "raw-model.glb",
                "provider-preview.png",
                "provider-preview-right.png",
                "texture-0-base-color.png",
                "texture-0-normal.png",
            ],
        )


if __name__ == "__main__":
    unittest.main()
