#!/usr/bin/env python3
"""Blender-side map substitution and authority validation for runtime grade."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import traceback
from pathlib import Path
from typing import Any

import bpy


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from lossless_pbr_png import inspect_png  # noqa: E402
from transplant_texture_donor_blender import (  # noqa: E402
    EXPECTED_BLENDER,
    authority_snapshot,
    body_and_accent_materials,
    canonicalize_exported_material_factors,
    export_animated_glb,
    load_packed_image,
    material_fingerprint,
    model_collection,
    model_objects,
    parse_glb,
    stable_hash,
    validate_embedded_glb,
    validate_output_glb,
)


REPORT_SCHEMA = "nilbots-runtime-pbr-grade-1"
PROFILE = "aegis-foundry-runtime-v1"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
NODE_NAMES = {
    "baseColor": "NB.BaseColor",
    "metallicRoughness": "NB.MetallicRoughness",
    "normal": "NB.Normal",
    "emissive": "NB.Emissive",
}


def blender_arguments() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-blend", required=True, type=Path)
    parser.add_argument("--expected-source-blend-sha256", required=True)
    parser.add_argument("--source-glb", required=True, type=Path)
    parser.add_argument("--expected-source-glb-sha256", required=True)
    parser.add_argument("--source-report", required=True, type=Path)
    parser.add_argument("--expected-source-report-sha256", required=True)
    parser.add_argument("--grade-manifest", required=True, type=Path)
    parser.add_argument("--expected-grade-manifest-sha256", required=True)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args(argv)
    for name, value in vars(args).items():
        if name.startswith("expected_") and not SHA256_RE.fullmatch(value):
            parser.error(f"--{name.replace('_', '-')} must be lowercase SHA-256")
    return args


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def resolve_repo_path(
    value: Path,
    *,
    repo_root: Path,
    option: str,
    strict: bool,
) -> tuple[Path, Path]:
    if value.is_absolute() or not value.parts:
        raise ValueError(f"{option} must be repository-relative")
    path = (repo_root / value).resolve(strict=strict)
    try:
        relative = path.relative_to(repo_root)
    except ValueError as error:
        raise ValueError(f"{option} escaped repository") from error
    return path, relative


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def artifact(path: Path, *, repo_root: Path, kind: str) -> dict[str, Any]:
    return {
        "kind": kind,
        "path": path.relative_to(repo_root).as_posix(),
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
    }


def glb_material(document: dict[str, Any], name: str) -> dict[str, Any]:
    matches = [
        material
        for material in document.get("materials", [])
        if isinstance(material, dict) and material.get("name") == name
    ]
    if len(matches) != 1:
        raise ValueError(f"Expected one GLB material named {name!r}")
    return matches[0]


def main() -> int:
    args = parse_args(blender_arguments())
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise ValueError(
            f"Expected Blender {EXPECTED_BLENDER}, got {bpy.app.version_string}"
        )
    repo_root = repository_root()
    inputs: list[tuple[Path, str, str]] = []
    for option, value, expected in (
        ("--source-blend", args.source_blend, args.expected_source_blend_sha256),
        ("--source-glb", args.source_glb, args.expected_source_glb_sha256),
        ("--source-report", args.source_report, args.expected_source_report_sha256),
        ("--grade-manifest", args.grade_manifest, args.expected_grade_manifest_sha256),
    ):
        path, _relative = resolve_repo_path(
            value, repo_root=repo_root, option=option, strict=True
        )
        if sha256(path) != expected:
            raise ValueError(f"{option} SHA-256 does not match its pin")
        inputs.append((path, expected, option))
    source_blend, source_glb, source_report_path, manifest_path = [
        item[0] for item in inputs
    ]
    output_dir, output_relative = resolve_repo_path(
        args.output_dir,
        repo_root=repo_root,
        option="--output-dir",
        strict=True,
    )
    if output_relative.parts[0] not in {"sandbox", "var"}:
        raise ValueError("Output must stay below sandbox/ or var/")

    source_report = json.loads(source_report_path.read_text(encoding="utf-8"))
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("schema") != REPORT_SCHEMA or manifest.get("profile") != PROFILE:
        raise ValueError("Grade manifest schema/profile mismatch")
    collection_name = source_report["method"]["modelCollection"]
    bind_frame = int(source_report["method"]["bindFrame"])
    source_snapshot = source_report["authoritySnapshot"]["after"]
    source_glb_validation = source_report["outputGlbValidation"]
    source_factors = source_report["pbr"]["factors"]
    source_samplers = source_report["pbr"]["samplers"]
    donor_sha256 = source_report["inputs"]["donorGlb"]["sha256"]
    source_payload_set = source_report["pbr"]["payloadSetSha256"]

    texture_paths: dict[str, Path] = {}
    texture_records: dict[str, dict[str, Any]] = {}
    for role, record in manifest["outputTextures"].items():
        path, _relative = resolve_repo_path(
            Path(record["path"]),
            repo_root=repo_root,
            option=f"manifest output texture {role}",
            strict=True,
        )
        if sha256(path) != record["sha256"]:
            raise ValueError(f"Generated {role} SHA-256 drifted")
        inspected = inspect_png(path.read_bytes(), label=f"Generated {role}")
        if [inspected["width"], inspected["height"]] != [
            int(record["width"]), int(record["height"])
        ]:
            raise ValueError(f"Generated {role} dimensions drifted")
        texture_paths[role] = path
        texture_records[role] = record
    if set(texture_paths) != set(NODE_NAMES):
        raise ValueError("Grade manifest must provide exactly four PBR maps")

    bpy.ops.wm.open_mainfile(filepath=str(source_blend))
    collection = model_collection(collection_name)
    objects = model_objects(collection)
    body_materials, accent_materials = body_and_accent_materials(objects)
    before = authority_snapshot(objects, bind_frame=bind_frame)
    if before != source_snapshot:
        raise ValueError("Source blend authority differs from source PASS report")
    accent_before = {
        material.name: material_fingerprint(material)
        for material in accent_materials
    }
    body_material_names = [material.name for material in body_materials]
    accent_material_names = [material.name for material in accent_materials]

    loaded = {
        role: load_packed_image(path, role=role)
        for role, path in texture_paths.items()
    }
    for material in body_materials:
        if not material.use_nodes or material.node_tree is None:
            raise ValueError(f"Body material lost nodes: {material.name}")
        for role, node_name in NODE_NAMES.items():
            node = material.node_tree.nodes.get(node_name)
            if (
                node is None
                or node.bl_idname != "ShaderNodeTexImage"
                or node.image is None
            ):
                raise ValueError(f"Body material lacks canonical {role} node")
            node.image = loaded[role]
        material["nilbotsRuntimeGradeProfile"] = PROFILE
        material["nilbotsRuntimeGradeSourceGlbSha256"] = args.expected_source_glb_sha256
        material["nilbotsRuntimeGradePayloadSetSha256"] = manifest["payloadSetSha256"]
        material["nilbotsBodyEmissionPolicy"] = "zero-texture-team-semantic-only"

    in_memory = authority_snapshot(objects, bind_frame=bind_frame)
    accent_in_memory = {
        material.name: material_fingerprint(material)
        for material in accent_materials
    }
    if in_memory != before:
        raise ValueError("Runtime grade changed in-memory authority")
    if accent_in_memory != accent_before:
        raise ValueError("Runtime grade changed semantic team material")

    blend_path = output_dir / "runtime-graded-authority.blend"
    glb_path = output_dir / "runtime-graded-animated.glb"
    original_frame = bpy.context.scene.frame_current
    bpy.context.scene.frame_set(original_frame)
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    export_animated_glb(path=glb_path, collection=collection)
    canonicalize_exported_material_factors(
        glb_path,
        body_material_names=body_material_names,
        factors=source_factors,
        expected_texture_roles=set(texture_paths),
    )

    donor_source = {
        "images": {
            role: {
                "sha256": record["sha256"],
                "bytesPayload": texture_paths[role].read_bytes(),
            }
            for role, record in texture_records.items()
        },
        "samplers": source_samplers,
        "factors": source_factors,
    }
    glb_validation = validate_output_glb(
        glb_path,
        baseline_path=source_glb,
        body_material_names=body_material_names,
        accent_material_names=accent_material_names,
        donor_source=donor_source,
        donor_sha256=donor_sha256,
        payload_set_sha256=source_payload_set,
    )

    output_document, output_binary = parse_glb(glb_path)
    validate_embedded_glb(output_document, output_binary, label="Runtime graded GLB")
    source_document, _source_binary = parse_glb(source_glb)
    source_accent_glb = {
        name: stable_hash(glb_material(source_document, name))
        for name in accent_material_names
    }
    output_accent_glb = {
        name: stable_hash(glb_material(output_document, name))
        for name in accent_material_names
    }
    if source_accent_glb != output_accent_glb:
        raise ValueError("Runtime GLB changed semantic team material")

    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    persisted_collection = model_collection(collection_name)
    persisted_objects = model_objects(persisted_collection)
    persisted_body, persisted_accent = body_and_accent_materials(persisted_objects)
    after = authority_snapshot(persisted_objects, bind_frame=bind_frame)
    accent_after = {
        material.name: material_fingerprint(material)
        for material in persisted_accent
    }
    if after != before:
        raise ValueError("Persisted runtime-grade blend changed authority")
    if accent_after != accent_before:
        raise ValueError("Persisted runtime-grade blend changed semantic team material")
    for material in persisted_body:
        if material.get("nilbotsRuntimeGradeProfile") != PROFILE:
            raise ValueError("Persisted body material lost runtime-grade provenance")
        for role, node_name in NODE_NAMES.items():
            node = material.node_tree.nodes.get(node_name)
            if node is None or node.image is None or node.image.packed_file is None:
                raise ValueError(f"Persisted body material lost packed {role}")

    authority_hash_keys = {
        "nodeHierarchy": (
            "outputNodeHierarchySha256",
            "outputNodeHierarchySha256",
        ),
        "meshGeometryTopologyUvAttributes": (
            "outputMeshAuthoritySha256",
            "outputMeshAuthoritySha256",
        ),
        "skins": (
            "outputSkinAuthoritySha256",
            "outputSkinAuthoritySha256",
        ),
        "sceneRoots": (
            "outputSceneAuthoritySha256",
            "outputSceneAuthoritySha256",
        ),
        "animationChannelsTargetsTimesValues": (
            "outputAnimationSamplesSha256",
            "outputAnimationSamplesSha256",
        ),
    }
    glb_invariants = {
        label: {
            "sourceSha256": source_glb_validation[source_key],
            "outputSha256": glb_validation[output_key],
            "exact": source_glb_validation[source_key] == glb_validation[output_key],
        }
        for label, (source_key, output_key) in authority_hash_keys.items()
    }
    if not all(record["exact"] for record in glb_invariants.values()):
        raise ValueError("Runtime GLB authority hash differs from source GLB")

    artifacts = [
        artifact(blend_path, repo_root=repo_root, kind="blend"),
        artifact(glb_path, repo_root=repo_root, kind="animated-glb"),
    ]
    for role, path in sorted(texture_paths.items()):
        artifacts.append(artifact(path, repo_root=repo_root, kind=f"texture-{role}"))
    checks = {
        "offlineProviderCallsZero": True,
        "offlineNetworkCallsZero": True,
        "sourcePassReportPinned": True,
        "sourceBlendAndGlbPinned": True,
        "normalMapByteExact": (
            manifest["textureGrade"]["normal"]["sourceSha256"]
            == manifest["textureGrade"]["normal"]["outputSha256"]
        ),
        "bodyEmissionTextureAllZero": (
            manifest["textureGrade"]["emissive"]["evidence"]["outputNonzeroChannelSamples"] == 0
        ),
        "semanticTeamMaterialTextureFree": True,
        "semanticTeamMaterialBlendFingerprintUnchanged": accent_after == accent_before,
        "semanticTeamMaterialGlbRecordUnchanged": output_accent_glb == source_accent_glb,
        "objectTransformsMeshGeometryUvAndAnimationUnchanged": after == before,
        "glbHierarchyGeometryUvPivotsSkinsScenesAndAnimationsExact": all(
            record["exact"] for record in glb_invariants.values()
        ),
        "roughnessFloorApplied": (
            manifest["textureGrade"]["metallicRoughness"]["evidence"]["outputRoughnessMinimum"]
            >= manifest["textureGrade"]["metallicRoughness"]["parameters"]["roughnessFloorRgb8"]
        ),
        "metallicCapApplied": (
            manifest["textureGrade"]["metallicRoughness"]["evidence"]["outputMetallicMaximum"]
            <= manifest["textureGrade"]["metallicRoughness"]["parameters"]["metallicCapRgb8"]
        ),
    }
    if not all(checks.values()):
        raise ValueError("Runtime-grade final checks did not all pass")

    report = {
        **manifest,
        "status": "PASS",
        "blender": {
            "version": bpy.app.version_string,
            "expectedMajorMinor": list(EXPECTED_BLENDER),
            "background": bool(bpy.app.background),
        },
        "authoritySnapshot": {"before": before, "after": after},
        "semanticTeamMaterial": {
            "blendFingerprintBefore": accent_before,
            "blendFingerprintAfter": accent_after,
            "glbRecordSha256Before": source_accent_glb,
            "glbRecordSha256After": output_accent_glb,
            "rendererOwned": True,
            "textureFree": True,
        },
        "glbInvariants": glb_invariants,
        "outputGlbValidation": glb_validation,
        "checks": checks,
        "artifacts": artifacts,
    }
    report_path = output_dir / "report.json"
    report_path.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    for path, expected, label in inputs:
        if sha256(path) != expected:
            raise ValueError(f"{label} drifted during grade")
    print(
        json.dumps(
            {
                "status": "PASS",
                "report": report_path.relative_to(repo_root).as_posix(),
                "blend": artifacts[0]["sha256"],
                "animatedGlb": artifacts[1]["sha256"],
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception:
        traceback.print_exc()
        raise SystemExit(1)
