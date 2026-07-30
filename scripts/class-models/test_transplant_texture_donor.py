#!/usr/bin/env python3
"""Offline tests for the deterministic texture-donor transplant."""

from __future__ import annotations

import hashlib
import json
import math
import shutil
import struct
import subprocess
import sys
import unittest
import uuid
from pathlib import Path
from typing import Any


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parents[1]
sys.path.insert(0, str(SCRIPT_DIR))

import transplant_texture_donor as transplant  # noqa: E402


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def contains_absolute_path(value: Any) -> bool:
    if isinstance(value, dict):
        return any(
            contains_absolute_path(key) or contains_absolute_path(item)
            for key, item in value.items()
        )
    if isinstance(value, list):
        return any(contains_absolute_path(item) for item in value)
    return isinstance(value, str) and value.startswith("/")


def stable_hash(value: Any) -> str:
    payload = json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
        allow_nan=False,
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def parse_glb(path: Path) -> tuple[dict[str, Any], bytes]:
    payload = path.read_bytes()
    if len(payload) < 20 or payload[:4] != b"glTF":
        raise AssertionError(f"Not a GLB: {path}")
    _magic, version, total_length = struct.unpack_from("<4sII", payload)
    if version != 2 or total_length != len(payload):
        raise AssertionError(f"Malformed GLB: {path}")
    offset = 12
    document: dict[str, Any] | None = None
    binary = b""
    while offset < len(payload):
        length, chunk_type = struct.unpack_from("<II", payload, offset)
        offset += 8
        chunk = payload[offset : offset + length]
        offset += length
        if chunk_type == 0x4E4F534A:
            document = json.loads(chunk.rstrip(b" \x00").decode("utf-8"))
        elif chunk_type == 0x004E4942:
            binary = chunk
    if document is None:
        raise AssertionError(f"GLB has no JSON: {path}")
    return document, binary


def write_glb(
    path: Path,
    *,
    document: dict[str, Any],
    binary: bytes,
) -> None:
    encoded = json.dumps(
        document,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
        allow_nan=False,
    ).encode("utf-8")
    encoded += b" " * ((-len(encoded)) % 4)
    chunks = (
        struct.pack("<II", len(encoded), 0x4E4F534A)
        + encoded
        + struct.pack("<II", len(binary), 0x004E4942)
        + binary
    )
    path.write_bytes(
        struct.pack("<4sII", b"glTF", 2, 12 + len(chunks)) + chunks
    )


def mutated_glb(
    source: Path,
    destination: Path,
    mutation: Any,
) -> Path:
    document, binary = parse_glb(source)
    mutation(document)
    write_glb(destination, document=document, binary=binary)
    return destination


def material_texture_info(
    material: dict[str, Any],
    role: str,
) -> dict[str, Any] | None:
    pbr = material.get("pbrMetallicRoughness", {})
    return {
        "baseColor": pbr.get("baseColorTexture"),
        "metallicRoughness": pbr.get("metallicRoughnessTexture"),
        "normal": material.get("normalTexture"),
        "emissive": material.get("emissiveTexture"),
        "occlusion": material.get("occlusionTexture"),
    }[role]


def material_factors(material: dict[str, Any]) -> dict[str, Any]:
    pbr = material.get("pbrMetallicRoughness", {})
    normal = material.get("normalTexture", {})
    occlusion = material.get("occlusionTexture", {})
    strength = material.get("extensions", {}).get(
        "KHR_materials_emissive_strength",
        {},
    )
    return {
        "baseColor": pbr.get("baseColorFactor", [1.0, 1.0, 1.0, 1.0]),
        "metallic": pbr.get("metallicFactor", 1.0),
        "roughness": pbr.get("roughnessFactor", 1.0),
        "emissive": material.get("emissiveFactor", [0.0, 0.0, 0.0]),
        "emissiveStrength": strength.get("emissiveStrength", 1.0),
        "normalScale": normal.get("scale", 1.0),
        "occlusionStrength": occlusion.get("strength", 1.0),
        "doubleSided": material.get("doubleSided", False),
    }


def image_payload(
    document: dict[str, Any],
    binary: bytes,
    *,
    texture_info: dict[str, Any],
) -> bytes:
    texture = document["textures"][texture_info["index"]]
    image = document["images"][texture["source"]]
    view = document["bufferViews"][image["bufferView"]]
    start = view.get("byteOffset", 0)
    return binary[start : start + view["byteLength"]]


def animation_accessor_values(
    document: dict[str, Any],
    binary: bytes,
    accessor_index: int,
) -> list[float | list[float]]:
    accessor = document["accessors"][accessor_index]
    view = document["bufferViews"][accessor["bufferView"]]
    components = {
        "SCALAR": 1,
        "VEC2": 2,
        "VEC3": 3,
        "VEC4": 4,
    }[accessor["type"]]
    item_size = components * 4
    stride = view.get("byteStride", item_size)
    start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    result: list[float | list[float]] = []
    for index in range(accessor["count"]):
        values = list(
            struct.unpack_from(
                f"<{components}f",
                binary,
                start + index * stride,
            )
        )
        result.append(values[0] if components == 1 else values)
    return result


def animation_records(
    document: dict[str, Any],
    binary: bytes,
) -> list[dict[str, Any]]:
    nodes = document["nodes"]
    result = []
    for animation in document.get("animations", []):
        channels = []
        for channel in animation["channels"]:
            sampler = animation["samplers"][channel["sampler"]]
            target = channel["target"]
            channels.append(
                {
                    "targetNode": nodes[target["node"]]["name"],
                    "targetPath": target["path"],
                    "interpolation": sampler.get(
                        "interpolation",
                        "LINEAR",
                    ),
                    "times": animation_accessor_values(
                        document,
                        binary,
                        sampler["input"],
                    ),
                    "values": animation_accessor_values(
                        document,
                        binary,
                        sampler["output"],
                    ),
                }
            )
        result.append(
            {
                "name": animation["name"],
                "channels": sorted(
                    channels,
                    key=lambda item: (
                        item["targetNode"],
                        item["targetPath"],
                    ),
                ),
            }
        )
    return sorted(result, key=lambda item: item["name"])


class TextureDonorTransplantTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        try:
            cls.blender = transplant.resolve_blender(REPO_ROOT)
        except (OSError, ValueError, subprocess.SubprocessError) as error:
            raise unittest.SkipTest(str(error)) from error
        cls.scratch_relative = Path(
            "sandbox",
            f"texture-donor-test-{uuid.uuid4().hex}",
        )
        cls.scratch = REPO_ROOT / cls.scratch_relative
        command = [
            str(cls.blender),
            "--background",
            "--factory-startup",
            "--python",
            str(
                SCRIPT_DIR / "build_texture_donor_fixture_blender.py"
            ),
            "--",
            "--repo-root",
            str(REPO_ROOT),
            "--output-dir",
            str(cls.scratch),
        ]
        completed = subprocess.run(
            command,
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
        )
        fixture_manifest = cls.scratch / "fixture.json"
        if completed.returncode != 0 or not fixture_manifest.is_file():
            raise RuntimeError(
                "Fixture builder failed:\n"
                + completed.stdout[-4000:]
                + completed.stderr[-4000:]
            )
        cls.fixture = json.loads(
            fixture_manifest.read_text(encoding="utf-8")
        )

    @classmethod
    def tearDownClass(cls) -> None:
        scratch = getattr(cls, "scratch", None)
        if scratch is None or not scratch.exists():
            return
        relative = scratch.resolve().relative_to(REPO_ROOT)
        if (
            len(relative.parts) != 2
            or relative.parts[0] != "sandbox"
            or not relative.parts[1].startswith("texture-donor-test-")
        ):
            raise AssertionError(f"Refusing unsafe test cleanup: {relative}")
        shutil.rmtree(scratch)

    def run_transplant(
        self,
        *,
        donor_key: str = "donorGlb",
        donor_path: Path | None = None,
        authored_key: str = "authoredBlend",
        output_name: str,
    ) -> subprocess.CompletedProcess[str]:
        authored = REPO_ROOT / self.fixture[authored_key]
        uploaded = REPO_ROOT / self.fixture["uploadedSourceGlb"]
        donor = (
            donor_path.resolve(strict=True)
            if donor_path is not None
            else REPO_ROOT / self.fixture[donor_key]
        )
        donor_relative = donor.relative_to(REPO_ROOT).as_posix()
        output_relative = self.scratch_relative / output_name
        command = [
            sys.executable,
            str(SCRIPT_DIR / "transplant_texture_donor.py"),
            "--authored-blend",
            self.fixture[authored_key],
            "--expected-authored-sha256",
            sha256(authored),
            "--uploaded-source-glb",
            self.fixture["uploadedSourceGlb"],
            "--expected-uploaded-source-sha256",
            sha256(uploaded),
            "--donor-glb",
            donor_relative,
            "--expected-donor-sha256",
            sha256(donor),
            "--model-collection",
            self.fixture["modelCollection"],
            "--bind-frame",
            "1",
            "--output-dir",
            output_relative.as_posix(),
        ]
        return subprocess.run(
            command,
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
        )

    def assert_factors_close(
        self,
        observed: dict[str, Any],
        expected: dict[str, Any],
    ) -> None:
        self.assertEqual(set(observed), set(expected))
        for key, expected_value in expected.items():
            observed_value = observed[key]
            if isinstance(expected_value, list):
                self.assertEqual(len(observed_value), len(expected_value))
                for actual, wanted in zip(observed_value, expected_value):
                    self.assertAlmostEqual(actual, wanted, places=6)
            elif isinstance(expected_value, bool):
                self.assertIs(observed_value, expected_value)
            else:
                self.assertAlmostEqual(
                    observed_value,
                    expected_value,
                    places=6,
                )

    def test_rejects_absolute_and_overwriting_outputs(self) -> None:
        with self.assertRaisesRegex(ValueError, "repository-relative"):
            transplant.resolve_output(
                Path("/tmp/not-allowed"),
                repo_root=REPO_ROOT,
            )
        existing = self.scratch_relative / "existing"
        (REPO_ROOT / existing).mkdir()
        with self.assertRaisesRegex(ValueError, "refusing overwrite"):
            transplant.resolve_output(existing, repo_root=REPO_ROOT)

    def test_transplants_only_pbr_and_preserves_authority(self) -> None:
        authored = REPO_ROOT / self.fixture["authoredBlend"]
        authored_before = sha256(authored)
        completed = self.run_transplant(
            donor_key="donorGlb",
            output_name="success",
        )
        self.assertEqual(
            completed.returncode,
            0,
            completed.stdout[-4000:] + completed.stderr[-4000:],
        )
        self.assertEqual(sha256(authored), authored_before)

        output = self.scratch / "success"
        report = json.loads(
            (output / "report.json").read_text(encoding="utf-8")
        )
        self.assertEqual(
            report["schema"],
            transplant.REPORT_SCHEMA,
        )
        self.assertEqual(report["status"], "PASS")
        self.assertFalse(contains_absolute_path(report))
        self.assertFalse(transplant.contains_absolute_path(report))
        self.assertTrue(all(report["checks"].values()))
        self.assertEqual(
            report["method"]["geometryAuthority"],
            "authored-blend-only",
        )
        self.assertEqual(
            report["method"]["providerObjectsImportedIntoAuthorityScene"],
            0,
        )
        self.assertEqual(
            report["authoritySnapshot"]["before"],
            report["authoritySnapshot"]["after"],
        )
        self.assertGreater(
            report["authoritySnapshot"]["before"]["keyframes"],
            0,
        )
        self.assertIn(
            "Shell_Deploy__FixturePivot",
            report["authoritySnapshot"]["before"]["actionNames"],
        )
        self.assertEqual(
            report["pbr"]["teamAccentMaterialsUntouched"],
            ["AUTH_TeamAccent"],
        )
        self.assertEqual(
            set(report["pbr"]["dimensions"]),
            {
                "baseColor",
                "emissive",
                "metallicRoughness",
                "normal",
                "occlusion",
            },
        )
        self.assertIn(
            "Shell_Deploy",
            report["outputGlbAnimationNames"],
        )
        self.assertTrue((output / "textured-authority.blend").is_file())
        self.assertTrue((output / "textured-animated.glb").is_file())

        donor_document, donor_binary = parse_glb(
            REPO_ROOT / self.fixture["donorGlb"]
        )
        output_document, output_binary = parse_glb(
            output / "textured-animated.glb"
        )
        donor_material = donor_document["materials"][0]
        raw_donor_factors = material_factors(donor_material)
        self.assert_factors_close(
            raw_donor_factors,
            self.fixture["donorFactors"],
        )
        output_materials = {
            material["name"]: material
            for material in output_document["materials"]
        }
        self.assertEqual(
            set(output_materials),
            {"AUTH_Body", "AUTH_TeamAccent"},
        )
        body_material = output_materials["AUTH_Body"]
        self.assert_factors_close(
            material_factors(body_material),
            raw_donor_factors,
        )
        self.assertEqual(
            body_material["extras"]["nilbotsTextureDonorSha256"],
            sha256(REPO_ROOT / self.fixture["donorGlb"]),
        )
        roles = {
            "baseColor",
            "emissive",
            "metallicRoughness",
            "normal",
            "occlusion",
        }
        body_proof = report["outputGlbValidation"]["materials"][
            "bodyMaterials"
        ]["AUTH_Body"]["textures"]
        for role in roles:
            donor_info = material_texture_info(donor_material, role)
            output_info = material_texture_info(body_material, role)
            self.assertIsNotNone(donor_info)
            self.assertIsNotNone(output_info)
            donor_payload = image_payload(
                donor_document,
                donor_binary,
                texture_info=donor_info,
            )
            output_payload = image_payload(
                output_document,
                output_binary,
                texture_info=output_info,
            )
            self.assertEqual(output_payload, donor_payload)
            self.assertEqual(
                hashlib.sha256(output_payload).hexdigest(),
                body_proof[role]["sha256"],
            )
            self.assertEqual(
                body_proof[role]["sampler"],
                {
                    "magFilter": 9729,
                    "minFilter": 9987,
                    "wrapS": 10497,
                    "wrapT": 10497,
                },
            )

        accent_material = output_materials["AUTH_TeamAccent"]
        self.assertEqual(
            accent_material["extras"]["nilbotsRole"],
            "team-accent",
        )
        self.assertTrue(
            all(
                material_texture_info(accent_material, role) is None
                for role in roles
            )
        )
        accent_proof = report["outputGlbValidation"]["materials"][
            "teamAccentMaterialProof"
        ]["AUTH_TeamAccent"]
        self.assertEqual(
            accent_proof["baselineSha256"],
            accent_proof["outputSha256"],
        )

        animations = animation_records(output_document, output_binary)
        self.assertEqual([item["name"] for item in animations], ["Shell_Deploy"])
        self.assertEqual(
            report["outputGlbValidation"][
                "baselineAnimationSamplesSha256"
            ],
            report["outputGlbValidation"]["outputAnimationSamplesSha256"],
        )
        self.assertEqual(
            report["outputGlbValidation"]["outputAnimationSamplesSha256"],
            stable_hash(animations),
        )
        channels = animations[0]["channels"]
        self.assertEqual(
            {
                (channel["targetNode"], channel["targetPath"])
                for channel in channels
            },
            {
                ("AUTH_FixturePivot", "rotation"),
                ("AUTH_FixturePivot", "translation"),
            },
        )
        expected_times = [index / 24.0 for index in range(1, 11)]
        channel_proof = {
            (record["targetNode"], record["targetPath"]): record
            for record in report["outputGlbValidation"]["animations"][0][
                "channelProof"
            ]
        }
        for channel in channels:
            self.assertEqual(len(channel["times"]), 10)
            for observed, expected in zip(
                channel["times"],
                expected_times,
            ):
                self.assertAlmostEqual(observed, expected, places=6)
            self.assertNotEqual(
                stable_hash(channel["values"][:1]),
                stable_hash(channel["values"][-1:]),
            )
            proof = channel_proof[
                (channel["targetNode"], channel["targetPath"])
            ]
            self.assertEqual(proof["keyCount"], 10)
            self.assertEqual(
                proof["timesSha256"],
                stable_hash(channel["times"]),
            )
            self.assertEqual(
                proof["valuesSha256"],
                stable_hash(channel["values"]),
            )
        self.assertTrue(
            all(
                report["outputGlbValidation"]["matchesBaseline"].values()
            )
        )

        overwrite = self.run_transplant(
            donor_key="donorGlb",
            output_name="success",
        )
        self.assertNotEqual(overwrite.returncode, 0)
        self.assertIn("refusing overwrite", overwrite.stderr)

    def test_preserves_noncanonical_raw_factor_decomposition(self) -> None:
        donor = REPO_ROOT / self.fixture["donorGlb"]
        mutated = self.scratch / "noncanonical-factors.glb"
        expected = {
            "baseColor": [0.25, 0.5, 0.75, 0.6],
            "metallic": 0.17,
            "roughness": 0.83,
            "emissive": [0.2, 0.3, 0.4],
            "emissiveStrength": 2.5,
            "normalScale": 0.42,
            "occlusionStrength": 0.23,
            "doubleSided": False,
        }

        def change_factors(document: dict[str, Any]) -> None:
            material = document["materials"][0]
            pbr = material["pbrMetallicRoughness"]
            pbr["baseColorFactor"] = expected["baseColor"]
            pbr["metallicFactor"] = expected["metallic"]
            pbr["roughnessFactor"] = expected["roughness"]
            material["emissiveFactor"] = expected["emissive"]
            material["extensions"] = {
                "KHR_materials_emissive_strength": {
                    "emissiveStrength": expected["emissiveStrength"],
                }
            }
            material["normalTexture"]["scale"] = expected["normalScale"]
            material["occlusionTexture"]["strength"] = expected[
                "occlusionStrength"
            ]
            material["doubleSided"] = expected["doubleSided"]
            used = set(document.get("extensionsUsed", []))
            used.add("KHR_materials_emissive_strength")
            document["extensionsUsed"] = sorted(used)

        mutated_glb(donor, mutated, change_factors)
        completed = self.run_transplant(
            donor_path=mutated,
            output_name="noncanonical-factor-success",
        )
        self.assertEqual(
            completed.returncode,
            0,
            completed.stdout[-4000:] + completed.stderr[-4000:],
        )
        output = self.scratch / "noncanonical-factor-success"
        document, _binary = parse_glb(output / "textured-animated.glb")
        body = next(
            material
            for material in document["materials"]
            if material["name"] == "AUTH_Body"
        )
        self.assert_factors_close(material_factors(body), expected)
        report = json.loads(
            (output / "report.json").read_text(encoding="utf-8")
        )
        self.assert_factors_close(report["pbr"]["factors"], expected)
        self.assert_factors_close(
            report["outputGlbValidation"]["materials"]["bodyMaterials"][
                "AUTH_Body"
            ]["factors"],
            expected,
        )

    def test_accepts_one_global_similarity_and_rejects_reflection(
        self,
    ) -> None:
        completed = self.run_transplant(
            donor_key="similarityDonorGlb",
            output_name="similarity-success",
        )
        self.assertEqual(
            completed.returncode,
            0,
            completed.stdout[-4000:] + completed.stderr[-4000:],
        )
        report = json.loads(
            (
                self.scratch
                / "similarity-success"
                / "report.json"
            ).read_text(encoding="utf-8")
        )
        proof = report["surfaceCorrespondence"][
            "uploadedSourceToDonor"
        ]
        self.assertEqual(proof["triangles"], 2)
        self.assertEqual(proof["pairedCorners"], 6)
        self.assertEqual(proof["uvWindingMismatches"], 0)
        self.assertEqual(proof["geometryWindingMismatches"], 0)
        self.assertGreater(
            proof["acceptedTransform"]["determinant"],
            0.0,
        )
        self.assertFalse(
            proof["acceptedTransform"]["reflection"]
        )
        residual = proof["acceptedTransform"][
            "residualNormalizedToDonorSpan"
        ]
        self.assertLessEqual(
            residual["maximum"],
            proof["thresholds"]["maximumNormalizedResidual"],
        )
        self.assertLessEqual(
            residual["rms"],
            proof["thresholds"]["rmsNormalizedResidual"],
        )

        reflected = self.run_transplant(
            donor_key="reflectionDonorGlb",
            output_name="reflection-failure",
        )
        self.assertNotEqual(reflected.returncode, 0)
        self.assertRegex(
            reflected.stdout + reflected.stderr,
            (
                "oriented UV-triangle surface differs"
                "|not one tight orientation-preserving"
                "|triangle winding differs"
            ),
        )
        self.assertFalse(
            (self.scratch / "reflection-failure").exists()
        )

    def test_rejects_external_glb_dependencies_before_output(self) -> None:
        donor = REPO_ROOT / self.fixture["donorGlb"]

        def external_buffer(document: dict[str, Any]) -> None:
            document["buffers"][0]["uri"] = "external.bin"

        def external_image(document: dict[str, Any]) -> None:
            image = document["images"][0]
            image.pop("bufferView", None)
            image["uri"] = "fixture-base.png"

        for name, mutation in (
            ("external-buffer", external_buffer),
            ("external-image", external_image),
        ):
            with self.subTest(name=name):
                mutated = self.scratch / f"{name}.glb"
                mutated_glb(donor, mutated, mutation)
                completed = self.run_transplant(
                    donor_path=mutated,
                    output_name=f"{name}-failure",
                )
                self.assertNotEqual(completed.returncode, 0)
                self.assertIn(
                    "must be embedded",
                    completed.stdout + completed.stderr,
                )
                self.assertFalse(
                    (self.scratch / f"{name}-failure").exists()
                )

    def test_rejects_noncanonical_sampler_and_extensions(self) -> None:
        donor = REPO_ROOT / self.fixture["donorGlb"]

        def clamp_sampler(document: dict[str, Any]) -> None:
            pbr = document["materials"][0]["pbrMetallicRoughness"]
            texture = document["textures"][pbr["baseColorTexture"]["index"]]
            sampler_index = texture.get("sampler")
            if sampler_index is None:
                sampler_index = len(document.setdefault("samplers", []))
                document["samplers"].append({})
                texture["sampler"] = sampler_index
            document["samplers"][sampler_index]["wrapS"] = 33071

        def texture_transform(document: dict[str, Any]) -> None:
            pbr = document["materials"][0]["pbrMetallicRoughness"]
            pbr["baseColorTexture"]["extensions"] = {
                "KHR_texture_transform": {"offset": [0.1, 0.0]}
            }
            used = set(document.get("extensionsUsed", []))
            used.add("KHR_texture_transform")
            document["extensionsUsed"] = sorted(used)

        cases = (
            (
                "noncanonical-sampler",
                clamp_sampler,
                "not Blender 5.2 canonical",
            ),
            (
                "texture-extension",
                texture_transform,
                "unsupported extensions",
            ),
        )
        for name, mutation, expected_error in cases:
            with self.subTest(name=name):
                mutated = self.scratch / f"{name}.glb"
                mutated_glb(donor, mutated, mutation)
                completed = self.run_transplant(
                    donor_path=mutated,
                    output_name=f"{name}-failure",
                )
                self.assertNotEqual(completed.returncode, 0)
                self.assertIn(
                    expected_error,
                    completed.stdout + completed.stderr,
                )
                self.assertFalse(
                    (self.scratch / f"{name}-failure").exists()
                )

    def test_rejects_body_material_shared_outside_collection(self) -> None:
        completed = self.run_transplant(
            authored_key="sharedMaterialAuthoredBlend",
            output_name="shared-material-failure",
        )
        self.assertNotEqual(completed.returncode, 0)
        self.assertIn(
            "shared outside the model collection",
            completed.stdout + completed.stderr,
        )
        self.assertFalse(
            (self.scratch / "shared-material-failure").exists()
        )

    def test_report_verifier_rejects_traversal_and_sensitive_values(
        self,
    ) -> None:
        for value in (
            "/tmp/private",
            "diagnostic: /private/secret",
            r"C:\Users\secret",
            "https://example.invalid/private",
            "data:image/png;base64,AAAA",
        ):
            with self.subTest(value=value):
                self.assertTrue(transplant.contains_absolute_path(value))
        self.assertFalse(
            transplant.contains_absolute_path(
                "sandbox/example/textured-animated.glb"
            )
        )

        output_relative = self.scratch_relative / "report-verifier"
        output = REPO_ROOT / output_relative
        output.mkdir()
        blend = output / "textured-authority.blend"
        glb = output / "textured-animated.glb"
        escaped = self.scratch / "escaped.glb"
        blend.write_bytes(b"blend")
        glb.write_bytes(b"glb")
        escaped.write_bytes(b"escaped")
        escaped_text = (
            output_relative / ".." / escaped.name
        ).as_posix()
        report = {
            "schema": transplant.REPORT_SCHEMA,
            "status": "PASS",
            "outputDirectory": output_relative.as_posix(),
            "inputs": {},
            "checks": {"fixture": True},
            "artifacts": [
                {
                    "path": (
                        output_relative / "textured-authority.blend"
                    ).as_posix(),
                    "sha256": sha256(blend),
                },
                {
                    "path": (
                        output_relative / "textured-animated.glb"
                    ).as_posix(),
                    "sha256": sha256(glb),
                },
                {
                    "path": escaped_text,
                    "sha256": sha256(escaped),
                },
            ],
        }
        with self.assertRaisesRegex(ValueError, "escaped"):
            transplant.verify_report(
                report,
                repo_root=REPO_ROOT,
                output_relative=output_relative,
                inputs={},
            )

    def test_mismatched_donor_fails_before_output(self) -> None:
        completed = self.run_transplant(
            donor_key="badDonorGlb",
            output_name="bad",
        )
        self.assertNotEqual(completed.returncode, 0)
        self.assertRegex(
            completed.stdout + completed.stderr,
            (
                "not one tight orientation-preserving"
                "|oriented UV-triangle surface differs"
            ),
        )
        self.assertFalse((self.scratch / "bad").exists())

    def test_mismatched_donor_uv_fails_before_output(self) -> None:
        completed = self.run_transplant(
            donor_key="badUvDonorGlb",
            output_name="bad-uv",
        )
        self.assertNotEqual(completed.returncode, 0)
        self.assertIn(
            "oriented UV-triangle surface differs",
            completed.stdout + completed.stderr,
        )
        self.assertFalse((self.scratch / "bad-uv").exists())


if __name__ == "__main__":
    unittest.main()
