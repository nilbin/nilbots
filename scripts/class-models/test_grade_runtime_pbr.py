#!/usr/bin/env python3
"""Focused tests for the deterministic runtime PBR pixel grade."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from grade_runtime_pbr import (  # noqa: E402
    METALLIC_CAP,
    ROUGHNESS_FLOOR,
    grade_base_color,
    grade_metallic_roughness,
    zero_emissive,
)
from lossless_pbr_png import encode_rgb8_png, inspect_png  # noqa: E402


def rgb_png(samples: bytes, *, width: int) -> bytes:
    return encode_rgb8_png(width=width, height=1, samples=samples)


def decoded(payload: bytes) -> bytes:
    record = inspect_png(payload, label="test PNG", include_samples=True)
    return record["samples"]


class RuntimePbrGradeTests(unittest.TestCase):
    def test_base_grade_is_deterministic_and_preserves_strong_orange(self) -> None:
        source = rgb_png(
            bytes(
                (
                    8, 8, 8,       # near-black graphite
                    76, 51, 90,    # purple graphite
                    255, 255, 255, # white island
                    255, 112, 0,   # strong Foundry orange
                )
            ),
            width=4,
        )
        first, first_report = grade_base_color(source)
        second, second_report = grade_base_color(source)
        self.assertEqual(first, second)
        self.assertEqual(first_report, second_report)
        samples = decoded(first)
        near_black = samples[0:3]
        purple = samples[3:6]
        white = samples[6:9]
        orange = samples[9:12]
        self.assertGreater(sum(near_black), 3 * 8)
        self.assertLess(max(purple) - min(purple), 12)
        self.assertLess(max(white), 160)
        self.assertEqual(orange, bytes((255, 112, 0)))

    def test_metallic_roughness_grade_preserves_r_and_enforces_bounds(self) -> None:
        source_samples = bytes((255, 42, 155, 200, 160, 84))
        output, report = grade_metallic_roughness(
            rgb_png(source_samples, width=2)
        )
        samples = decoded(output)
        self.assertEqual(samples[0], 255)
        self.assertEqual(samples[3], 200)
        self.assertGreaterEqual(samples[1], ROUGHNESS_FLOOR)
        self.assertGreaterEqual(samples[4], ROUGHNESS_FLOOR)
        self.assertLessEqual(samples[2], METALLIC_CAP)
        self.assertLessEqual(samples[5], METALLIC_CAP)
        self.assertEqual(report["evidence"]["outputRoughnessMinimum"], samples[1])

    def test_body_emission_is_zeroed_without_dimension_change(self) -> None:
        source = rgb_png(bytes((1, 2, 3, 255, 64, 0)), width=2)
        output, report = zero_emissive(source)
        self.assertEqual(decoded(output), bytes(6))
        self.assertEqual(report["input"]["width"], report["output"]["width"])
        self.assertEqual(report["input"]["height"], report["output"]["height"])
        self.assertEqual(
            report["evidence"]["outputNonzeroChannelSamples"], 0
        )


if __name__ == "__main__":
    unittest.main()
