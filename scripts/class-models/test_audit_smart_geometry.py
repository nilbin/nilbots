#!/usr/bin/env python3

from __future__ import annotations

import argparse
import unittest

import audit_smart_geometry as subject


def fixture_audit() -> dict:
    view = {
        "file": "top.png",
        "sha256": "a" * 64,
        "bytes": 123,
        "cameraType": "ORTHO",
        "cameraLocation": [0.0, 0.0, 3.0],
        "cameraTarget": [0.0, 0.0, 0.0],
        "orthoScale": 1.24,
        "fieldOfViewDegrees": None,
    }
    bounds = {
        "minimum": [-0.5, -0.3, 0.0],
        "maximum": [0.5, 0.3, 0.2],
        "span": [1.0, 0.6, 0.2],
        "center": [0.0, 0.0, 0.1],
        "aabbVolume": 0.12,
    }
    return {
        "schema": subject.EXPECTED_AUDIT_SCHEMA,
        "toolVersion": "2.0.0",
        "input": {
            "fileName": "candidate.glb",
            "sha256": "b" * 64,
            "bytes": 456,
        },
        "blender": {
            "version": "5.2.0",
            "expectedMajorMinor": [5, 2],
            "matchesExpectedMajorMinor": True,
            "background": True,
        },
        "sourceDocument": {
            "assetVersion": "2.0",
            "generator": "fixture",
            "counts": {
                "scenes": 1,
                "nodes": 1,
                "meshes": 1,
                "primitives": 1,
                "materials": 0,
                "images": 0,
                "textures": 0,
                "samplers": 0,
                "animations": 0,
                "skins": 0,
                "cameras": 0,
                "accessors": 3,
                "bufferViews": 3,
                "buffers": 1,
            },
            "extensionsUsed": [],
            "extensionsRequired": [],
        },
        "importedScene": {
            "counts": {
                "objects": 1,
                "meshObjects": 1,
                "armatureObjects": 0,
                "cameraObjects": 0,
                "lightObjects": 0,
                "emptyObjects": 0,
                "materials": 0,
                "images": 0,
                "actions": 0,
                "weldedConnectedComponents": 2,
                "rawIndexedConnectedComponents": 3,
            },
            "meshes": [
                {
                    "name": "mesh",
                    "counts": {
                        "vertices": 14,
                        "edges": 20,
                        "faces": 8,
                        "triangles": 12,
                        "materialSlots": 0,
                        "uvLayers": 0,
                        "colorAttributes": 0,
                        "shapeKeys": 0,
                        "vertexGroups": 0,
                        "armatureModifiers": 0,
                    },
                    "components": [
                        {
                            "componentIndex": 0,
                            "vertexCount": 10,
                            "weldedVertexCount": 10,
                            "faceCount": 6,
                            "triangleCount": 10,
                            "surfaceArea": 1.0,
                            "boundsWorldDcc": bounds,
                            "boundaryEdges": 0,
                            "manifoldEdges": 15,
                            "nonManifoldEdges": 0,
                        },
                        {
                            "componentIndex": 1,
                            "vertexCount": 4,
                            "weldedVertexCount": 4,
                            "faceCount": 2,
                            "triangleCount": 2,
                            "surfaceArea": 0.1,
                            "boundsWorldDcc": bounds,
                            "boundaryEdges": 4,
                            "manifoldEdges": 1,
                            "nonManifoldEdges": 0,
                        },
                    ],
                }
            ],
        },
        "globalGeometry": {
            "boundsWorldDcc": bounds,
            "planformSpan": 1.0,
            "heightToPlanform": 0.2,
            "floorOffset": 0.0,
            "orientation": {
                "sourceGlTfUp": "+Y",
                "blenderImportUp": "+Z",
                "expectedNilbotsFacingInBlender": "+X",
                "expectedNilbotsUpInBlender": "+Z",
                "semanticFacingInferred": False,
            },
        },
        "orientationAdjustment": {
            "operation": "fixture",
            "xDegrees": 0,
            "zDegrees": 0,
            "matrix": [
                [1.0, 0.0, 0.0, 0.0],
                [0.0, 1.0, 0.0, 0.0],
                [0.0, 0.0, 1.0, 0.0],
                [0.0, 0.0, 0.0, 1.0],
            ],
            "determinant3x3": 1.0,
            "rootObjectsTransformed": ["mesh"],
            "sourceGlbModified": False,
            "deformation": False,
        },
        "renderNormalization": {
            "operation": "fixture",
            "uniformScale": 1.0,
            "targetPlanformSpan": 1.0,
            "sourceBoundsWorldDcc": bounds,
            "normalizedBoundsWorldDcc": bounds,
            "rootObjectsTransformed": ["mesh"],
            "perModelCameraFraming": False,
        },
        "renderEvidence": {
            "rig": {
                "id": "fixture",
                "renderEngine": "BLENDER_EEVEE",
                "resolution": [768, 768],
                "fixedCameraFraming": True,
            },
            "views": {"top": view},
            "topSilhouette": {
                "file": "top-silhouette.png",
                "sha256": "c" * 64,
                "bytes": 99,
                "orthoScale": 1.24,
            },
        },
        "topSilhouetteComparison": {
            "rotations": [
                {
                    "candidateClockwiseYawDegrees": 0,
                    "intersectionOverUnion": 0.9,
                }
            ],
            "acceptanceThreshold": None,
            "semanticFacingSelected": False,
        },
    }


class AuditSmartGeometryTests(unittest.TestCase):
    def test_positive_float_rejects_nonpositive_and_nonfinite_values(self) -> None:
        for value in ("0", "-1", "nan", "inf"):
            with self.subTest(value=value):
                with self.assertRaises(argparse.ArgumentTypeError):
                    subject.positive_float(value)

    def test_compact_report_preserves_objective_inputs_without_verdict(self) -> None:
        report = subject.build_compact_report(
            fixture_audit(),
            authored_span_tiles=1.12,
            runtime_scale=1.062,
            pixels_per_tile=64.0,
        )

        self.assertEqual(report["schema"], subject.REPORT_SCHEMA)
        self.assertEqual(report["geometry"]["triangles"], 12)
        self.assertEqual(report["geometry"]["weldedConnectedComponents"], 2)
        self.assertEqual(
            report["geometry"]["orientationAdjustment"]["xDegrees"],
            0,
        )
        self.assertEqual(
            report["geometry"]["components"]["largestTriangleShare"],
            round(10 / 12, 9),
        )
        self.assertEqual(
            report["acceptanceInputs"]["gameScale"][
                "liveMaximumPlanformSpanTiles"
            ],
            1.18944,
        )
        self.assertEqual(report["acceptanceInputs"]["passFail"], None)
        self.assertEqual(
            report["acceptanceInputs"]["topSilhouette"]["acceptanceThreshold"],
            None,
        )
        self.assertFalse(
            report["acceptanceInputs"]["topSilhouette"][
                "semanticFacingSelected"
            ]
        )

    def test_compact_report_rejects_unpinned_blender(self) -> None:
        audit = fixture_audit()
        audit["blender"]["matchesExpectedMajorMinor"] = False
        audit["blender"]["version"] = "5.3.0"

        with self.assertRaisesRegex(ValueError, "unpinned version"):
            subject.build_compact_report(
                audit,
                authored_span_tiles=1.0,
                runtime_scale=1.0,
                pixels_per_tile=64.0,
            )


if __name__ == "__main__":
    unittest.main()
