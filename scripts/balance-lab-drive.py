#!/usr/bin/env python3
"""Run a reproducible Nilbots Balance Lab candidate matrix.

The orchestration core is mode-independent: a candidate is one immutable
mode + ruleset + map + match-format contract, and each candidate supplies an
opaque runner command. The first dynamics adapter understands generic
Frontline replay v3. Reports retain a vector of measurements and explicitly
mark unsupported layers instead of inventing one balance score.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import itertools
import json
import random
import shutil
import statistics
import subprocess
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parent.parent
COHORT_SCRIPT = ROOT / "scripts" / "labs-cohort-drive.py"
EVALUATOR_SCRIPT = ROOT / "scripts" / "labs-replay-eval.py"
REPORT_SCHEMA_VERSION = 3
TWO_TEAM_ZERO_SUM_PROFILE = "two-team-zero-sum-v1"
ANALYSIS_UNIT = "mirrored-entrant-pair-seed-v1"
EVIDENCE_LAYERS = {
    "contract-validity",
    "static-map-analysis",
    "exact-tactical-analysis",
    "qualification",
    "population-cross-play",
    "restricted-play",
    "adversarial-sentinels",
    "statistical-sufficiency",
    "holdout",
    "blind-replay-review",
    "author-dx",
}
PIPELINE_FILES = (
    Path("balance/balance-lab-spec.schema.json"),
    Path("scripts/balance-holdout.py"),
    Path("scripts/balance-lab-drive.py"),
    Path("scripts/frontline-balance-candidates.py"),
    Path("scripts/labs-cohort-drive.py"),
    Path("scripts/labs-replay-eval.py"),
)


def _load_module(name: str, path: Path) -> Any:
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"could not load {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


COHORT = _load_module("nilbots_labs_cohort_drive", COHORT_SCRIPT)
EVALUATOR = _load_module("nilbots_labs_replay_eval", EVALUATOR_SCRIPT)


def _slug(value: Any, label: str) -> str:
    if not isinstance(value, str) or COHORT._slug(value) != value:
        raise ValueError(f"{label} must be a lowercase kebab-case slug")
    return value


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _fingerprint(value: Any, label: str) -> str:
    if (
        not isinstance(value, str)
        or len(value) != 64
        or any(character not in "0123456789abcdef" for character in value)
    ):
        raise ValueError(f"{label} must be a lowercase SHA-256 fingerprint")
    return value


def _pipeline_identity() -> dict[str, str]:
    return {
        path.as_posix(): _sha256(ROOT / path)
        for path in PIPELINE_FILES
    }


def _non_negative_seeds(value: Any) -> list[int]:
    if (
        not isinstance(value, list)
        or not value
        or any(
            not isinstance(seed, int)
            or isinstance(seed, bool)
            or seed < 0
            for seed in value
        )
        or len(set(value)) != len(value)
    ):
        raise ValueError("pairedSeeds must be distinct non-negative integers")
    return value


def _normalize_holdout(
    value: Any,
    *,
    native_product_present: bool,
) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError("holdout must be an object")
    protocol = value.get("protocol")
    if protocol == "none":
        if set(value) != {"protocol", "reason"}:
            raise ValueError(
                "a holdout with protocol none requires exactly protocol "
                "and reason"
            )
        if (
            not isinstance(value.get("reason"), str)
            or not value["reason"].strip()
        ):
            raise ValueError("holdout.reason must be non-empty")
        if native_product_present:
            raise ValueError(
                "native-product evidence requires a committed holdout"
            )
        return value
    if protocol != "sha256-commit-reveal-v1":
        raise ValueError("holdout.protocol is unsupported")
    if set(value) != {
        "protocol",
        "commitmentSha256",
        "seedCount",
    }:
        raise ValueError(
            "a committed holdout requires exactly protocol, "
            "commitmentSha256, and seedCount"
        )
    _fingerprint(
        value.get("commitmentSha256"),
        "holdout.commitmentSha256",
    )
    seed_count = value.get("seedCount")
    if (
        not isinstance(seed_count, int)
        or isinstance(seed_count, bool)
        or seed_count <= 0
    ):
        raise ValueError("holdout.seedCount must be positive")
    return value


def _normalize_study_design(value: Any) -> dict[str, Any]:
    required = {
        "decisionProfileId",
        "analysisUnit",
        "confidenceLevel",
        "bootstrapResamples",
        "minimumMirroredUnitsPerCell",
        "minimumEntrantPairsPerCell",
        "minimumSeedsPerEntrantPair",
        "minimumIndependentLineagesPerPopulation",
        "minimumVotingLineagesPerPopulation",
        "minimumVotingTier",
        "multiplicityPolicy",
        "requiredEvidenceLayers",
    }
    if not isinstance(value, dict) or set(value) != required:
        raise ValueError(
            "studyDesign fields must be exactly "
            + ", ".join(sorted(required))
        )
    _slug(
        value.get("decisionProfileId"),
        "studyDesign.decisionProfileId",
    )
    if value.get("analysisUnit") != ANALYSIS_UNIT:
        raise ValueError(f"studyDesign.analysisUnit must be {ANALYSIS_UNIT}")
    confidence = value.get("confidenceLevel")
    if (
        not isinstance(confidence, (int, float))
        or isinstance(confidence, bool)
        or not 0.5 < float(confidence) < 1.0
    ):
        raise ValueError(
            "studyDesign.confidenceLevel must be between 0.5 and 1"
        )
    resamples = value.get("bootstrapResamples")
    if (
        not isinstance(resamples, int)
        or isinstance(resamples, bool)
        or not 100 <= resamples <= 100000
    ):
        raise ValueError(
            "studyDesign.bootstrapResamples must be between 100 and 100000"
        )
    for field in (
        "minimumMirroredUnitsPerCell",
        "minimumEntrantPairsPerCell",
        "minimumSeedsPerEntrantPair",
        "minimumIndependentLineagesPerPopulation",
        "minimumVotingLineagesPerPopulation",
    ):
        item = value.get(field)
        if (
            not isinstance(item, int)
            or isinstance(item, bool)
            or item <= 0
        ):
            raise ValueError(f"studyDesign.{field} must be positive")
    if value["minimumIndependentLineagesPerPopulation"] < 2:
        raise ValueError(
            "studyDesign.minimumIndependentLineagesPerPopulation "
            "must be at least 2"
        )
    if (
        value["minimumVotingLineagesPerPopulation"]
        < value["minimumIndependentLineagesPerPopulation"]
    ):
        raise ValueError(
            "studyDesign.minimumVotingLineagesPerPopulation cannot be "
            "below the diagnostic lineage floor"
        )
    if value.get("minimumVotingTier") not in {
        "T2",
        "T3",
        "T4",
        "T5",
        "T6",
        "T7",
        "T8",
    }:
        raise ValueError("studyDesign.minimumVotingTier is unsupported")
    if value.get("multiplicityPolicy") not in {
        "diagnostic-no-selection",
        "bonferroni-all-contrasts-v1",
    }:
        raise ValueError("studyDesign.multiplicityPolicy is unsupported")
    layers = value.get("requiredEvidenceLayers")
    if (
        not isinstance(layers, list)
        or not layers
        or len(set(layers)) != len(layers)
        or any(layer not in EVIDENCE_LAYERS for layer in layers)
    ):
        raise ValueError(
            "studyDesign.requiredEvidenceLayers must be unique known layers"
        )
    return value


def _normalize_toolchain(
    value: Any,
    study_blocks: Any,
) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError("toolchain must be an object")
    protocol = value.get("protocol")
    if protocol == "diagnostic-current-process":
        if set(value) != {"protocol", "reason"}:
            raise ValueError(
                "diagnostic toolchain requires exactly protocol and reason"
            )
        if (
            not isinstance(value.get("reason"), str)
            or not value["reason"].strip()
        ):
            raise ValueError("toolchain.reason must be non-empty")
        voting_roles = {"mechanic-causality", "native-product"}
        if any(
            isinstance(block, dict)
            and block.get("role") in voting_roles
            for block in study_blocks
        ):
            raise ValueError(
                "voting study blocks require a frozen build output"
            )
        return value
    if protocol != "frozen-build-output-v1":
        raise ValueError("toolchain.protocol is unsupported")
    if set(value) != {"protocol", "buildCommand", "entrypoint"}:
        raise ValueError(
            "frozen toolchain requires exactly protocol, buildCommand, "
            "and entrypoint"
        )
    build_command = value.get("buildCommand")
    if (
        not isinstance(build_command, str)
        or not build_command.strip()
        or "{out}" not in build_command
    ):
        raise ValueError(
            "toolchain.buildCommand must be non-empty and contain {out}"
        )
    entrypoint = value.get("entrypoint")
    if (
        not isinstance(entrypoint, str)
        or not entrypoint
        or Path(entrypoint).name != entrypoint
        or entrypoint in {".", ".."}
    ):
        raise ValueError("toolchain.entrypoint must be one file name")
    return value


def _normalize_ablation_registry(
    value: Any,
    manifest_root: Path,
) -> tuple[Path, dict[str, Any]]:
    if not isinstance(value, dict) or set(value) != {"path", "sha256"}:
        raise ValueError(
            "ablationRegistry requires exactly path and sha256"
        )
    digest = _fingerprint(
        value.get("sha256"),
        "ablationRegistry.sha256",
    )
    path = (manifest_root / str(value.get("path", ""))).resolve()
    allowed_root = (
        ROOT if manifest_root.is_relative_to(ROOT) else manifest_root
    )
    if (
        not path.is_relative_to(allowed_root)
        or not path.is_file()
        or _sha256(path) != digest
    ):
        raise ValueError(
            "ablationRegistry path must exist in the repository and match "
            "its SHA-256"
        )
    document = json.loads(path.read_text(encoding="utf-8"))
    if (
        not isinstance(document, dict)
        or set(document) != {"schemaVersion", "registryId", "items"}
        or document.get("schemaVersion") != 1
    ):
        raise ValueError("ablation registry must be a schema-1 object")
    _slug(document.get("registryId"), "ablation registry id")
    items = document.get("items")
    required_item_fields = {
        "id",
        "status",
        "currentInterpretation",
        "requiredIsolation",
        "requiredBefore",
    }
    if (
        not isinstance(items, list)
        or not items
        or any(
            not isinstance(item, dict)
            or set(item) != required_item_fields
            for item in items
        )
    ):
        raise ValueError("ablation registry items have invalid fields")
    ids = []
    for item in items:
        ids.append(_slug(item.get("id"), "ablation item id"))
        if item.get("status") not in {"open", "satisfied", "retired"}:
            raise ValueError("ablation item status is unsupported")
        for field in (
            "currentInterpretation",
            "requiredIsolation",
            "requiredBefore",
        ):
            if (
                not isinstance(item.get(field), str)
                or not item[field].strip()
            ):
                raise ValueError(
                    f"ablation item {item['id']}.{field} must be non-empty"
                )
    if len(set(ids)) != len(ids):
        raise ValueError("ablation registry item ids must be unique")
    return path, document


def _contract(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError(f"{label} must be an object")
    required = {
        "modeId",
        "rulesetId",
        "rulesFingerprint",
        "seedProfileId",
        "mapId",
        "mapVersion",
        "mapFingerprint",
        "formatId",
        "formatFingerprint",
        "topologyProfileId",
        "topologyFingerprint",
        "contractProfileId",
        "matchContractFingerprint",
    }
    if set(value) != required:
        raise ValueError(
            f"{label} fields must be exactly {', '.join(sorted(required))}"
        )
    for key in (
        "modeId",
        "rulesetId",
        "seedProfileId",
        "mapId",
        "formatId",
        "topologyProfileId",
        "contractProfileId",
    ):
        _slug(value[key], f"{label}.{key}")
    if (
        not isinstance(value["mapVersion"], int)
        or isinstance(value["mapVersion"], bool)
        or value["mapVersion"] <= 0
    ):
        raise ValueError(f"{label}.mapVersion must be positive")
    for key in (
        "rulesFingerprint",
        "mapFingerprint",
        "formatFingerprint",
        "topologyFingerprint",
        "matchContractFingerprint",
    ):
        _fingerprint(value[key], f"{label}.{key}")
    return value


def _validate_factors(
    raw_factors: Any,
    candidates: list[dict[str, Any]],
) -> dict[str, list[str]]:
    if not isinstance(raw_factors, dict) or not raw_factors:
        raise ValueError("factors must be a non-empty object")
    factors: dict[str, list[str]] = {}
    for raw_name, raw_values in raw_factors.items():
        name = _slug(raw_name, "factor name")
        if (
            not isinstance(raw_values, list)
            or not raw_values
            or len(set(raw_values)) != len(raw_values)
        ):
            raise ValueError(f"factor {name} must have unique values")
        factors[name] = [
            _slug(value, f"factor {name} value") for value in raw_values
        ]

    expected = {
        tuple(zip(factors, values))
        for values in itertools.product(
            *(factors[name] for name in factors)
        )
    }
    actual = set()
    for candidate in candidates:
        actual.add(
            tuple(
                (
                    name,
                    _slug(
                        candidate["factors"].get(name),
                        f"{candidate['id']} factor {name}",
                    ),
                )
                for name in factors
            )
        )
    if len(actual) != len(candidates) or actual != expected:
        raise ValueError(
            "candidates must cover every declared factor combination exactly once"
        )
    return factors


def _normalize_study_blocks(
    raw_blocks: Any,
    candidates: list[dict[str, Any]],
    populations: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    if not isinstance(raw_blocks, list) or not raw_blocks:
        raise ValueError("studyBlocks must be a non-empty array")
    candidates_by_id = {
        candidate["id"]: candidate for candidate in candidates
    }
    populations_by_id = {
        population["id"]: population for population in populations
    }
    required = {
        "id",
        "role",
        "hypothesis",
        "qualificationProfileId",
        "candidateIds",
        "populationIds",
        "includeSelfPlay",
        "commonRandomness",
    }
    known_roles = {
        "compatibility-sentinel",
        "mechanic-causality",
        "native-product",
        "infrastructure-smoke",
        "adversarial-sentinel",
    }
    normalized = []
    block_ids: set[str] = set()
    for raw in raw_blocks:
        if not isinstance(raw, dict) or set(raw) != required:
            raise ValueError(
                "study block fields must be exactly "
                + ", ".join(sorted(required))
            )
        block_id = _slug(raw.get("id"), "study block id")
        if block_id in block_ids:
            raise ValueError("study block ids must be unique")
        block_ids.add(block_id)
        role = raw.get("role")
        if role not in known_roles:
            raise ValueError(f"{block_id}: unknown study role")
        hypothesis = raw.get("hypothesis")
        if not isinstance(hypothesis, str) or not hypothesis.strip():
            raise ValueError(f"{block_id}: hypothesis must be non-empty")
        qualification_profile_id = _slug(
            raw.get("qualificationProfileId"),
            f"{block_id}.qualificationProfileId",
        )
        candidate_ids = raw.get("candidateIds")
        population_ids = raw.get("populationIds")
        include_self_play = raw.get("includeSelfPlay")
        if not isinstance(include_self_play, bool):
            raise ValueError(f"{block_id}.includeSelfPlay must be boolean")
        for label, identifiers, known in (
            ("candidateIds", candidate_ids, candidates_by_id),
            ("populationIds", population_ids, populations_by_id),
        ):
            if (
                not isinstance(identifiers, list)
                or not identifiers
                or len(set(identifiers)) != len(identifiers)
                or any(identifier not in known for identifier in identifiers)
            ):
                raise ValueError(
                    f"{block_id}.{label} must contain unique known ids"
                )
        mismatched_profiles = [
            population_id
            for population_id in population_ids
            if populations_by_id[population_id][
                "qualificationProfileId"
            ] != qualification_profile_id
        ]
        if mismatched_profiles:
            raise ValueError(
                f"{block_id}: populations do not match required "
                "qualificationProfileId: "
                + ", ".join(mismatched_profiles)
            )
        common = raw.get("commonRandomness")
        if not isinstance(common, dict):
            raise ValueError(f"{block_id}.commonRandomness must be an object")
        protocol = common.get("protocol")
        if protocol == "not-required":
            if set(common) != {"protocol"}:
                raise ValueError(
                    f"{block_id}: not-required common randomness has "
                    "no additional fields"
                )
            if role == "mechanic-causality":
                raise ValueError(
                    f"{block_id}: mechanic-causality requires a shared "
                    "seed profile"
                )
        elif protocol == "shared-seed-profile-v1":
            if set(common) != {"protocol", "seedProfileId"}:
                raise ValueError(
                    f"{block_id}: shared common randomness requires "
                    "protocol and seedProfileId"
                )
            seed_profile_id = _slug(
                common.get("seedProfileId"),
                f"{block_id}.commonRandomness.seedProfileId",
            )
            mismatched_candidates = [
                candidate_id
                for candidate_id in candidate_ids
                if candidates_by_id[candidate_id]["contract"][
                    "seedProfileId"
                ] != seed_profile_id
            ]
            if mismatched_candidates:
                raise ValueError(
                    f"{block_id}: candidates do not share the declared "
                    "seed profile: "
                    + ", ".join(mismatched_candidates)
                )
        else:
            raise ValueError(
                f"{block_id}.commonRandomness.protocol is unsupported"
            )
        if role == "mechanic-causality" and len(candidate_ids) < 2:
            raise ValueError(
                f"{block_id}: mechanic-causality requires at least "
                "two candidates"
            )
        normalized.append(
            {
                **raw,
                "id": block_id,
                "candidateIds": sorted(candidate_ids),
                "populationIds": sorted(population_ids),
            }
        )
    return sorted(normalized, key=lambda item: item["id"])


def _normalize_entrant(
    raw: Any,
    manifest_root: Path,
    population_id: str,
    qualification_profile_id: str,
    qualification_contract_fingerprint: str | None,
    balance_evidence_eligible: bool,
    tier: str,
    coordination_grade: str,
) -> dict[str, Any]:
    if not isinstance(raw, dict):
        raise ValueError(f"{population_id}: every entrant must be an object")
    required = {
        "id",
        "name",
        "authoringLineageId",
        "doctrineId",
        "authoringBudgetId",
        "authorPacketSha256",
        "root",
        "artifact",
        "artifactSha256",
        "sourceTreeSha256",
        "qualification",
    }
    if set(raw) != required:
        raise ValueError(
            f"{population_id}: entrant fields must be exactly "
            f"{', '.join(sorted(required))}"
        )
    entrant_id = _slug(raw.get("id"), f"{population_id} entrant id")
    if not isinstance(raw["name"], str) or not raw["name"].strip():
        raise ValueError(f"{population_id}/{entrant_id}: name is required")
    authoring_lineage_id = _slug(
        raw.get("authoringLineageId"),
        f"{population_id}/{entrant_id}.authoringLineageId",
    )
    doctrine_id = _slug(
        raw.get("doctrineId"),
        f"{population_id}/{entrant_id}.doctrineId",
    )
    authoring_budget_id = _slug(
        raw.get("authoringBudgetId"),
        f"{population_id}/{entrant_id}.authoringBudgetId",
    )
    author_packet_sha = raw.get("authorPacketSha256")
    if author_packet_sha is not None:
        _fingerprint(
            author_packet_sha,
            f"{population_id}/{entrant_id}.authorPacketSha256",
        )
    root = (manifest_root / str(raw.get("root", ""))).resolve()
    artifact = (manifest_root / str(raw.get("artifact", ""))).resolve()
    allowed_root = (
        ROOT
        if manifest_root.is_relative_to(ROOT)
        else manifest_root
    )
    if (
        not root.is_relative_to(allowed_root)
        or not artifact.is_relative_to(root)
        or not root.is_dir()
        or not artifact.is_file()
    ):
        raise ValueError(
            f"{population_id}/{entrant_id}: root and artifact must exist "
            "inside the spec directory"
        )
    artifact_sha = _sha256(artifact)
    if raw.get("artifactSha256") != artifact_sha:
        raise ValueError(
            f"{population_id}/{entrant_id}: artifactSha256 mismatch"
        )
    source_sha = COHORT._source_tree_sha256(root)
    if raw.get("sourceTreeSha256") != source_sha:
        raise ValueError(
            f"{population_id}/{entrant_id}: sourceTreeSha256 mismatch"
        )
    qualification = raw.get("qualification")
    qualification_fields = {
        "suiteId",
        "suiteVersion",
        "qualificationProfileId",
        "qualificationContractFingerprint",
        "evidence",
        "evidenceSha256",
        "tierAwarded",
        "coordinationGradeAwarded",
        "balanceEvidenceEligible",
    }
    if (
        not isinstance(qualification, dict)
        or set(qualification) != qualification_fields
    ):
        raise ValueError(
            f"{population_id}/{entrant_id}: qualification fields must be "
            f"exactly {', '.join(sorted(qualification_fields))}"
        )
    _slug(
        qualification.get("suiteId"),
        f"{population_id}/{entrant_id}.qualification.suiteId",
    )
    suite_version = qualification.get("suiteVersion")
    if (
        not isinstance(suite_version, int)
        or isinstance(suite_version, bool)
        or suite_version <= 0
    ):
        raise ValueError(
            f"{population_id}/{entrant_id}: suiteVersion must be positive"
        )
    if (
        qualification.get("qualificationProfileId")
            != qualification_profile_id
        or qualification.get("qualificationContractFingerprint")
            != qualification_contract_fingerprint
        or qualification.get("balanceEvidenceEligible")
            is not balance_evidence_eligible
    ):
        raise ValueError(
            f"{population_id}/{entrant_id}: qualification identity and "
            "eligibility must match the population"
        )
    evidence = qualification.get("evidence")
    evidence_sha = qualification.get("evidenceSha256")
    evidence_path: Path | None = None
    if evidence is None:
        if evidence_sha is not None:
            raise ValueError(
                f"{population_id}/{entrant_id}: evidenceSha256 requires "
                "an evidence path"
            )
    elif not isinstance(evidence, str) or not evidence.strip():
        raise ValueError(
            f"{population_id}/{entrant_id}: evidence must be a path or null"
        )
    else:
        _fingerprint(
            evidence_sha,
            f"{population_id}/{entrant_id}.qualification.evidenceSha256",
        )
        evidence_path = (manifest_root / evidence).resolve()
        if (
            not evidence_path.is_relative_to(allowed_root)
            or not evidence_path.is_file()
            or _sha256(evidence_path) != evidence_sha
        ):
            raise ValueError(
                f"{population_id}/{entrant_id}: qualification evidence "
                "must exist inside the repository and match evidenceSha256"
            )
    for field in ("tierAwarded", "coordinationGradeAwarded"):
        value = qualification.get(field)
        if value is not None and (
            not isinstance(value, str) or not value.strip()
        ):
            raise ValueError(
                f"{population_id}/{entrant_id}.qualification.{field} "
                "must be a non-empty string or null"
            )
    if balance_evidence_eligible and (
        qualification_contract_fingerprint is None
        or evidence_path is None
        or evidence_sha is None
        or qualification.get("tierAwarded") != tier
        or qualification.get("coordinationGradeAwarded")
            != coordination_grade
        or author_packet_sha is None
    ):
        raise ValueError(
            f"{population_id}/{entrant_id}: balance-eligible entrants must "
            "carry matching cumulative tier, coordination, "
            "qualification-contract, and evidence fingerprints"
        )
    if balance_evidence_eligible:
        evidence_report = json.loads(
            evidence_path.read_text(encoding="utf-8")
        )
        if not isinstance(evidence_report, dict):
            raise ValueError(
                f"{population_id}/{entrant_id}: qualification evidence "
                "must be a JSON object"
            )
        expected_report = {
            "suiteId": qualification["suiteId"],
            "suiteVersion": qualification["suiteVersion"],
            "qualificationProfileId": qualification_profile_id,
            "qualificationContractFingerprint":
                qualification_contract_fingerprint,
            "artifactHash": artifact_sha,
            "passed": True,
            "profileComplete": True,
            "tierAwarded": tier,
            "coordinationGradeAwarded": coordination_grade,
            "balanceEvidenceEligible": True,
        }
        mismatches = [
            field
            for field, expected in expected_report.items()
            if evidence_report.get(field) != expected
        ]
        if mismatches:
            raise ValueError(
                f"{population_id}/{entrant_id}: qualification evidence "
                "does not match entrant fields: "
                + ", ".join(mismatches)
            )
    return {
        **raw,
        "id": entrant_id,
        "authoringLineageId": authoring_lineage_id,
        "doctrineId": doctrine_id,
        "authoringBudgetId": authoring_budget_id,
        "authorPacketSha256": author_packet_sha,
        "rootPath": root,
        "artifactPath": artifact,
        "artifactSha256": artifact_sha,
        "sourceTreeSha256": source_sha,
        "qualificationEvidencePath": evidence_path,
    }


def load_spec(path: Path) -> dict[str, Any]:
    document = json.loads(path.read_text(encoding="utf-8"))
    required = {
        "schemaVersion",
        "experimentId",
        "status",
        "hypothesis",
        "studyBlocks",
        "ablationRegistry",
        "factors",
        "pairedSeeds",
        "holdout",
        "studyDesign",
        "toolchain",
        "verifyCommand",
        "evaluationProfileId",
        "candidates",
        "populations",
    }
    allowed = required | {"$schema", "dynamicsAdapter"}
    if not isinstance(document, dict):
        raise ValueError("Balance Lab spec must be a JSON object")
    if not required.issubset(document):
        raise ValueError(
            "Balance Lab spec is missing required fields: "
            + ", ".join(sorted(required.difference(document)))
        )
    if not set(document).issubset(allowed):
        raise ValueError(
            "Balance Lab spec has unsupported fields: "
            + ", ".join(sorted(set(document).difference(allowed)))
        )
    if document.get("schemaVersion") != 3:
        raise ValueError("Balance Lab schemaVersion must be 3")
    if document.get("evaluationProfileId") != TWO_TEAM_ZERO_SUM_PROFILE:
        raise ValueError(
            "evaluationProfileId must be two-team-zero-sum-v1 in slice 2"
        )
    document["experimentId"] = _slug(
        document.get("experimentId"),
        "experimentId",
    )
    if document.get("status") != "experimental":
        raise ValueError("status must be experimental")
    if (
        not isinstance(document.get("hypothesis"), str)
        or not document["hypothesis"].strip()
    ):
        raise ValueError("hypothesis must be a non-empty string")
    document["pairedSeeds"] = _non_negative_seeds(
        document.get("pairedSeeds")
    )
    (
        document["ablationRegistryPath"],
        document["ablationRegistryDocument"],
    ) = _normalize_ablation_registry(
        document.get("ablationRegistry"),
        path.parent.resolve(),
    )
    document["holdout"] = _normalize_holdout(
        document.get("holdout"),
        native_product_present=any(
            isinstance(block, dict)
            and block.get("role") == "native-product"
            for block in document.get("studyBlocks", [])
        ),
    )
    document["studyDesign"] = _normalize_study_design(
        document.get("studyDesign")
    )
    document["toolchain"] = _normalize_toolchain(
        document.get("toolchain"),
        document.get("studyBlocks"),
    )

    raw_candidates = document.get("candidates")
    if not isinstance(raw_candidates, list) or not raw_candidates:
        raise ValueError("candidates must be a non-empty array")
    candidates = []
    candidate_ids: set[str] = set()
    contract_fingerprints: set[str] = set()
    for raw in raw_candidates:
        if not isinstance(raw, dict):
            raise ValueError("every candidate must be an object")
        if set(raw) != {"id", "factors", "runnerCommand", "contract"}:
            raise ValueError(
                "candidate fields must be exactly contract, factors, id, "
                "and runnerCommand"
            )
        candidate_id = _slug(raw.get("id"), "candidate id")
        if candidate_id in candidate_ids:
            raise ValueError("candidate ids must be unique")
        candidate_ids.add(candidate_id)
        factors = raw.get("factors")
        if not isinstance(factors, dict):
            raise ValueError(f"{candidate_id}.factors must be an object")
        runner = raw.get("runnerCommand")
        if not isinstance(runner, str) or not runner.strip():
            raise ValueError(f"{candidate_id}.runnerCommand is required")
        missing_runner_fields = [
            field
            for field in ("bot", "opponent", "seed", "out")
            if f"{{{field}}}" not in runner
        ]
        if missing_runner_fields:
            raise ValueError(
                f"{candidate_id}.runnerCommand is missing placeholders: "
                + ", ".join(missing_runner_fields)
            )
        if (
            document["toolchain"]["protocol"]
                == "frozen-build-output-v1"
            and "{toolchain}" not in runner
        ):
            raise ValueError(
                f"{candidate_id}.runnerCommand must use the frozen "
                "{toolchain} placeholder"
            )
        candidate_contract = _contract(
            raw.get("contract"),
            f"{candidate_id}.contract",
        )
        fingerprint = candidate_contract["matchContractFingerprint"]
        if fingerprint in contract_fingerprints:
            raise ValueError(
                "candidate match-contract fingerprints must be unique"
            )
        contract_fingerprints.add(fingerprint)
        candidates.append(
            {
                **raw,
                "id": candidate_id,
                "factors": factors,
                "contract": candidate_contract,
            }
        )
    document["factors"] = _validate_factors(
        document.get("factors"),
        candidates,
    )
    factor_names = set(document["factors"])
    for candidate in candidates:
        if set(candidate["factors"]) != factor_names:
            raise ValueError(
                f"{candidate['id']} must bind every and only declared factor"
            )
    document["candidates"] = sorted(
        candidates,
        key=lambda item: item["id"],
    )

    raw_populations = document.get("populations")
    if not isinstance(raw_populations, list) or not raw_populations:
        raise ValueError("populations must be a non-empty array")
    manifest_root = path.parent.resolve()
    populations = []
    population_ids: set[str] = set()
    for raw in raw_populations:
        if not isinstance(raw, dict):
            raise ValueError("every population must be an object")
        if set(raw) != {
            "id",
            "tier",
            "coordinationGrade",
            "qualificationProfileId",
            "qualificationContractFingerprint",
            "balanceEvidenceEligible",
            "entrants",
        }:
            raise ValueError(
                "population fields must be exactly balanceEvidenceEligible, "
                "coordinationGrade, entrants, id, "
                "qualificationContractFingerprint, "
                "qualificationProfileId, and tier"
            )
        population_id = _slug(raw.get("id"), "population id")
        if population_id in population_ids:
            raise ValueError("population ids must be unique")
        population_ids.add(population_id)
        tier = raw.get("tier")
        coordination = raw.get("coordinationGrade")
        qualification_profile_id = _slug(
            raw.get("qualificationProfileId"),
            f"{population_id}.qualificationProfileId",
        )
        qualification_contract_fingerprint = raw.get(
            "qualificationContractFingerprint"
        )
        if qualification_contract_fingerprint is not None:
            _fingerprint(
                qualification_contract_fingerprint,
                f"{population_id}.qualificationContractFingerprint",
            )
        balance_evidence_eligible = raw.get("balanceEvidenceEligible")
        if not isinstance(balance_evidence_eligible, bool):
            raise ValueError(
                f"{population_id}: balanceEvidenceEligible must be boolean"
            )
        if (
            tier not in {
                "unqualified",
                "R0",
                "T1",
                "T2",
                "T3",
                "T4",
                "T5",
                "T6",
                "T7",
                "T8",
            }
            or coordination not in {
                "unqualified",
                "C0",
                "C1",
                "C2",
                "C3",
                "C4",
                "C5",
            }
        ):
            raise ValueError(
                f"{population_id}: tier or coordinationGrade is unsupported"
            )
        entrants = raw.get("entrants")
        if not isinstance(entrants, list) or len(entrants) < 2:
            raise ValueError(
                f"{population_id}: at least two entrants are required"
            )
        normalized = [
            _normalize_entrant(
                item,
                manifest_root,
                population_id,
                qualification_profile_id,
                qualification_contract_fingerprint,
                balance_evidence_eligible,
                tier,
                coordination,
            )
            for item in entrants
        ]
        if len({item["id"] for item in normalized}) != len(normalized):
            raise ValueError(f"{population_id}: entrant ids must be unique")
        if len(
            {item["artifactSha256"] for item in normalized}
        ) != len(normalized):
            raise ValueError(
                f"{population_id}: entrant artifacts must be distinct"
            )
        if len(
            {item["sourceTreeSha256"] for item in normalized}
        ) != len(normalized):
            raise ValueError(
                f"{population_id}: entrant source trees must be distinct"
            )
        independent_lineages = len(
            {item["authoringLineageId"] for item in normalized}
        )
        required_lineages = (
            document["studyDesign"][
                "minimumVotingLineagesPerPopulation"
            ]
            if balance_evidence_eligible
            else document["studyDesign"][
                "minimumIndependentLineagesPerPopulation"
            ]
        )
        if independent_lineages < required_lineages:
            raise ValueError(
                f"{population_id}: balance evidence requires at least "
                f"{required_lineages} independent authoring lineages"
            )
        tier_order = {
            f"T{index}": index for index in range(1, 9)
        }
        minimum_voting_tier = document["studyDesign"][
            "minimumVotingTier"
        ]
        if (
            balance_evidence_eligible
            and (
                tier not in tier_order
                or tier_order[tier] < tier_order[minimum_voting_tier]
            )
        ):
            raise ValueError(
                f"{population_id}: balance evidence requires "
                f"{minimum_voting_tier} or higher"
            )
        populations.append(
            {
                **raw,
                "id": population_id,
                "entrants": sorted(
                    normalized,
                    key=lambda item: item["id"],
                ),
            }
        )
    document["populations"] = sorted(
        populations,
        key=lambda item: item["id"],
    )
    document["studyBlocks"] = _normalize_study_blocks(
        document.get("studyBlocks"),
        document["candidates"],
        document["populations"],
    )

    verifier = document.get("verifyCommand")
    if not isinstance(verifier, str) or not verifier.strip():
        raise ValueError("verifyCommand is required")
    if "{replay}" not in verifier:
        raise ValueError("verifyCommand must include {replay}")
    if (
        document["toolchain"]["protocol"] == "frozen-build-output-v1"
        and "{toolchain}" not in verifier
    ):
        raise ValueError(
            "verifyCommand must use the frozen {toolchain} placeholder"
        )
    if document.get("dynamicsAdapter") not in {
        None,
        "generic-frontline-replay-v3",
    }:
        raise ValueError("dynamicsAdapter is unsupported")
    return document


def build_plan(
    spec: dict[str, Any],
) -> list[dict[str, Any]]:
    plan = []
    candidates = {
        candidate["id"]: candidate for candidate in spec["candidates"]
    }
    populations = {
        population["id"]: population for population in spec["populations"]
    }
    for block in spec["studyBlocks"]:
        for candidate_id in block["candidateIds"]:
            candidate = candidates[candidate_id]
            for population_id in block["populationIds"]:
                population = populations[population_id]
                matches = COHORT.build_plan(
                    population["entrants"],
                    spec["pairedSeeds"],
                    include_self_play=block["includeSelfPlay"],
                )
                for match in matches:
                    plan.append(
                        {
                            **match,
                            "studyBlockId": block["id"],
                            "studyRole": block["role"],
                            "candidateId": candidate["id"],
                            "populationId": population["id"],
                            "tier": population["tier"],
                            "coordinationGrade":
                                population["coordinationGrade"],
                            "qualificationProfileId":
                                population["qualificationProfileId"],
                            "balanceEvidenceEligible":
                                population["balanceEvidenceEligible"],
                            "evaluationProfileId":
                                spec["evaluationProfileId"],
                        }
                    )
    return plan


def _public_spec(spec: dict[str, Any]) -> dict[str, Any]:
    populations = []
    for population in spec["populations"]:
        populations.append(
            {
                **population,
                "entrants": [
                    {
                        key: value
                        for key, value in entrant.items()
                        if not key.endswith("Path")
                    }
                    for entrant in population["entrants"]
                ],
            }
        )
    return {
        key: value
        for key, value in {
            **spec,
            "populations": populations,
        }.items()
        if key not in {
            "ablationRegistryPath",
            "ablationRegistryDocument",
        }
    }


def _tree_identity(path: Path) -> dict[str, Any]:
    digest = hashlib.sha256()
    files = []
    for file_path in sorted(
        item for item in path.rglob("*") if item.is_file()
    ):
        relative = file_path.relative_to(path).as_posix()
        payload = file_path.read_bytes()
        executable = bool(file_path.stat().st_mode & 0o111)
        digest.update(relative.encode("utf-8"))
        digest.update(b"\0")
        digest.update(b"x" if executable else b"-")
        digest.update(b"\0")
        digest.update(str(len(payload)).encode("ascii"))
        digest.update(b"\0")
        digest.update(payload)
        digest.update(b"\0")
        files.append(relative)
    return {
        "treeSha256": f"sha256:{digest.hexdigest()}",
        "files": files,
    }


def _freeze_toolchain(
    spec: dict[str, Any],
    output: Path,
) -> dict[str, Any]:
    toolchain = spec["toolchain"]
    if toolchain["protocol"] == "diagnostic-current-process":
        return {
            "protocol": toolchain["protocol"],
            "reason": toolchain["reason"],
        }
    destination = output / "toolchain"
    destination.mkdir()
    before = COHORT._repository_source_identity(ROOT)
    command = COHORT._command(
        toolchain["buildCommand"],
        {"out": destination.resolve()},
    )
    with (output / "toolchain-build.stdout.log").open("w") as stdout, (
        output / "toolchain-build.stderr.log"
    ).open("w") as stderr:
        completed = subprocess.run(
            command,
            cwd=ROOT,
            stdout=stdout,
            stderr=stderr,
            check=False,
            text=True,
        )
    after = COHORT._repository_source_identity(ROOT)
    if before != after:
        raise ValueError("repository source changed during toolchain build")
    if completed.returncode != 0:
        raise ValueError(
            "frozen toolchain build failed; inspect "
            "toolchain-build.stderr.log"
        )
    entrypoint = destination / toolchain["entrypoint"]
    if not entrypoint.is_file() or not (entrypoint.stat().st_mode & 0o111):
        raise ValueError(
            "frozen toolchain entrypoint is missing or not executable"
        )
    return {
        "protocol": toolchain["protocol"],
        "entrypoint": toolchain["entrypoint"],
        **_tree_identity(destination),
    }


def _toolchain_values(
    spec: dict[str, Any],
    output: Path,
) -> dict[str, Path]:
    if spec["toolchain"]["protocol"] == "diagnostic-current-process":
        return {}
    return {
        "toolchain": (
            output / "toolchain" / spec["toolchain"]["entrypoint"]
        ).resolve(),
    }


def _assert_frozen_toolchain(
    spec: dict[str, Any],
    output: Path,
    expected: dict[str, Any],
) -> None:
    if spec["toolchain"]["protocol"] == "diagnostic-current-process":
        return
    actual = {
        "protocol": spec["toolchain"]["protocol"],
        "entrypoint": spec["toolchain"]["entrypoint"],
        **_tree_identity(output / "toolchain"),
    }
    if actual != expected:
        raise ValueError("frozen toolchain changed during the Balance Lab run")


def _freeze(
    spec_path: Path,
    spec: dict[str, Any],
    output: Path,
    plan: list[dict[str, Any]],
) -> dict[tuple[str, str], Path]:
    if output.exists():
        raise ValueError(f"output already exists: {output}")
    output.mkdir(parents=True)
    toolchain_identity = _freeze_toolchain(spec, output)
    frozen_ablation_registry = output / "ablation-registry.json"
    shutil.copy2(
        spec["ablationRegistryPath"],
        frozen_ablation_registry,
    )
    if _sha256(frozen_ablation_registry) != (
        spec["ablationRegistry"]["sha256"]
    ):
        raise ValueError("frozen ablation registry identity mismatch")
    artifacts: dict[tuple[str, str], Path] = {}
    for population in spec["populations"]:
        for entrant in population["entrants"]:
            destination = (
                output
                / "populations"
                / population["id"]
                / "entrants"
                / entrant["id"]
            )
            copied = COHORT._copy_entrant(entrant, destination)
            artifacts[(population["id"], entrant["id"])] = copied
            evidence_path = entrant["qualificationEvidencePath"]
            if evidence_path is not None:
                frozen_evidence = (
                    output
                    / "populations"
                    / population["id"]
                    / "qualification"
                    / f"{entrant['id']}.json"
                )
                frozen_evidence.parent.mkdir(
                    parents=True,
                    exist_ok=True,
                )
                shutil.copy2(evidence_path, frozen_evidence)
                if _sha256(frozen_evidence) != (
                    entrant["qualification"]["evidenceSha256"]
                ):
                    raise ValueError(
                        f"{population['id']}/{entrant['id']}: frozen "
                        "qualification evidence mismatch"
                    )
    run = {
        "schemaVersion": REPORT_SCHEMA_VERSION,
        "spec": _public_spec(spec),
        "sourceSpec": str(spec_path),
        "sourceSpecSha256": _sha256(spec_path),
        "repositorySource": COHORT._repository_source_identity(ROOT),
        "toolchainIdentity": toolchain_identity,
        "pipelineSha256": _pipeline_identity(),
        "plan": plan,
    }
    (output / "run.json").write_text(
        json.dumps(run, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    return artifacts


def _resume(
    spec_path: Path,
    spec: dict[str, Any],
    output: Path,
    plan: list[dict[str, Any]],
) -> dict[tuple[str, str], Path]:
    run_path = output / "run.json"
    if not run_path.is_file():
        raise ValueError(f"{output} is not a resumable Balance Lab run")
    run = json.loads(run_path.read_text(encoding="utf-8"))
    if (
        run.get("sourceSpecSha256") != _sha256(spec_path)
        or run.get("spec") != _public_spec(spec)
        or run.get("plan") != plan
        or run.get("pipelineSha256") != _pipeline_identity()
        or run.get("repositorySource")
            != COHORT._repository_source_identity(ROOT)
    ):
        raise ValueError("resume spec or plan does not match run.json")
    _assert_frozen_toolchain(
        spec,
        output,
        run.get("toolchainIdentity", {}),
    )
    frozen_ablation_registry = output / "ablation-registry.json"
    if (
        not frozen_ablation_registry.is_file()
        or _sha256(frozen_ablation_registry)
            != spec["ablationRegistry"]["sha256"]
    ):
        raise ValueError("frozen ablation registry changed")
    artifacts: dict[tuple[str, str], Path] = {}
    for population in spec["populations"]:
        for entrant in population["entrants"]:
            artifact = (
                output
                / "populations"
                / population["id"]
                / "entrants"
                / entrant["id"]
                / "bot.wasm"
            )
            if (
                not artifact.is_file()
                or _sha256(artifact) != entrant["artifactSha256"]
                or COHORT._source_tree_sha256(artifact.parent)
                    != entrant["sourceTreeSha256"]
            ):
                raise ValueError(
                    f"{population['id']}/{entrant['id']}: "
                    "frozen artifact or source changed"
                )
            if entrant["qualificationEvidencePath"] is not None:
                frozen_evidence = (
                    output
                    / "populations"
                    / population["id"]
                    / "qualification"
                    / f"{entrant['id']}.json"
                )
                if (
                    not frozen_evidence.is_file()
                    or _sha256(frozen_evidence)
                        != entrant["qualification"]["evidenceSha256"]
                ):
                    raise ValueError(
                        f"{population['id']}/{entrant['id']}: frozen "
                        "qualification evidence changed"
                    )
            artifacts[(population["id"], entrant["id"])] = artifact
    return artifacts


def _candidate_identity_issues(
    document: dict[str, Any],
    plan: dict[str, Any],
    candidate: dict[str, Any],
    population: dict[str, Any],
) -> list[str]:
    header = document.get("header", {})
    contract = header.get("contract", {})
    rules = contract.get("rules", {})
    map_contract = contract.get("map", {})
    format_contract = contract.get("format", {})
    capabilities = contract.get("capabilityVersions", {})
    expected = candidate["contract"]
    checks = (
        ("replayVersion", header.get("replayVersion"), 3),
        ("partial", document.get("partial"), False),
        ("seed", header.get("seed"), str(plan["seed"])),
        (
            "modeId",
            rules.get("gameMode", {}).get("modeId"),
            expected["modeId"],
        ),
        ("rulesetId", rules.get("rulesetId"), expected["rulesetId"]),
        (
            "rulesFingerprint",
            rules.get("rulesFingerprint"),
            expected["rulesFingerprint"],
        ),
        (
            "seedProfileId",
            rules.get("seedMechanics", {}).get("seedProfileId"),
            expected["seedProfileId"],
        ),
        ("mapId", map_contract.get("mapId"), expected["mapId"]),
        (
            "mapVersion",
            map_contract.get("mapVersion"),
            expected["mapVersion"],
        ),
        (
            "mapFingerprint",
            map_contract.get("mapFingerprint"),
            expected["mapFingerprint"],
        ),
        ("formatId", format_contract.get("formatId"), expected["formatId"]),
        (
            "formatFingerprint",
            format_contract.get("formatFingerprint"),
            expected["formatFingerprint"],
        ),
        (
            "topologyFingerprint",
            contract.get("topology", {}).get("topologyFingerprint"),
            expected["topologyFingerprint"],
        ),
        (
            "contractProfileId",
            capabilities.get("contractProfileId"),
            expected["contractProfileId"],
        ),
        (
            "matchContractFingerprint",
            contract.get("matchContractFingerprint"),
            expected["matchContractFingerprint"],
        ),
    )
    issues = [
        name for name, actual, expected_value in checks
        if actual != expected_value
    ]
    entrants = {
        entrant["id"]: entrant for entrant in population["entrants"]
    }
    provenance = {
        str(item.get("teamId")): item
        for item in header.get("provenance", {}).get("participants", [])
    }
    for team_id, entrant_id in plan["teamAssignments"].items():
        item = provenance.get(team_id, {})
        if item.get("artifactHash") != entrants[entrant_id]["artifactSha256"]:
            issues.append(f"team-{team_id}.artifactHash")
        if "wasm" not in str(item.get("runtimeKind", "")).lower():
            issues.append(f"team-{team_id}.runtimeKind")
    return issues


def _canonical_sha256(value: Any) -> str:
    payload = json.dumps(
        value,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def _without_seed_attestation(value: Any) -> Any:
    if isinstance(value, dict):
        return {
            key: _without_seed_attestation(item)
            for key, item in value.items()
            if key not in {
                "actorRandomSeed",
                "botRandomSeed",
                "debugMessage",
            }
        }
    if isinstance(value, list):
        return [_without_seed_attestation(item) for item in value]
    return value


def _trajectory_fingerprint(document: dict[str, Any]) -> str:
    """Hash public play while excluding the seed and bot debug messages."""

    ticks = []
    for tick in document.get("ticks", []):
        actor_turns = []
        for turn in tick.get("actorTurns", []):
            submitted = turn.get("submittedDecision")
            submitted_projection = None
            if isinstance(submitted, dict):
                submitted_projection = {
                    key: submitted.get(key)
                    for key in ("actionId", "actionCode", "arguments")
                }
            actor_turns.append(
                {
                    "participantId": turn.get("participantId"),
                    "actorId": turn.get("actorId"),
                    "submittedDecision": submitted_projection,
                    "actionResolution": turn.get("actionResolution"),
                }
            )
        ticks.append(
            {
                "tick": tick.get("tick"),
                "tickStart": tick.get("tickStart"),
                "actorTurns": actor_turns,
                "events": tick.get("events"),
                "traversals": tick.get("traversals"),
                "postState": tick.get("postState"),
            }
        )
    return _canonical_sha256(
        _without_seed_attestation(
        {
            "initialFrame": document.get("initialFrame"),
            "ticks": ticks,
            "result": document.get("result"),
        }
        )
    )


def _entrant_behavior(
    document: dict[str, Any],
    team_assignments: dict[str, str],
) -> dict[str, dict[str, Any]]:
    """Extract deterministic per-entrant action and positional signatures."""

    contract = document.get("header", {}).get("contract", {})
    action_kinds = {
        str(action.get("id")): str(action.get("kind"))
        for action in contract.get("rules", {}).get("actions", [])
    }
    regions = {
        str(region.get("regionId")): {
            (int(tile[0]), int(tile[1]))
            for tile in region.get("tiles", [])
            if isinstance(tile, list) and len(tile) == 2
        }
        for region in contract.get("map", {}).get("regions", [])
    }
    objective_ids = contract.get("modeMapBinding", {}).get(
        "orderedObjectiveRegionIds",
        [],
    )
    totals: dict[str, dict[str, Any]] = {
        entrant: {
            "turns": 0,
            "actionKindCounts": {},
            "formTurnCounts": {},
            "objectiveTurns": 0,
            "damageDealt": 0,
            "damageTaken": 0,
        }
        for entrant in team_assignments.values()
    }
    for tick in document.get("ticks", []):
        for turn in tick.get("actorTurns", []):
            actor_id = turn.get("actorId", {})
            entrant = team_assignments.get(str(actor_id.get("teamId")))
            if entrant not in totals:
                continue
            item = totals[entrant]
            item["turns"] += 1
            observation = turn.get("observation", {})
            self_state = observation.get("self", {})
            form_id = str(self_state.get("formId", "unknown"))
            item["formTurnCounts"][form_id] = (
                item["formTurnCounts"].get(form_id, 0) + 1
            )
            mode = observation.get("mode", {})
            active_index = mode.get("activePositionIndex")
            position = self_state.get("position", {})
            if (
                isinstance(active_index, int)
                and 0 <= active_index < len(objective_ids)
                and (
                    position.get("x"),
                    position.get("y"),
                ) in regions.get(str(objective_ids[active_index]), set())
            ):
                item["objectiveTurns"] += 1

            accepted = turn.get("actionResolution", {}).get(
                "acceptedAction"
            )
            if isinstance(accepted, dict):
                action_id = str(accepted.get("actionId"))
                kind = action_kinds.get(action_id, f"unknown:{action_id}")
                item["actionKindCounts"][kind] = (
                    item["actionKindCounts"].get(kind, 0) + 1
                )

        for event in tick.get("events", []):
            if event.get("kind") != "damage":
                continue
            payload = event.get("payload", {})
            amount = int(payload.get("amount", 0))
            source = team_assignments.get(
                str(payload.get("sourceTeamId"))
            )
            target = team_assignments.get(
                str(
                    payload.get("targetActorId", {}).get("teamId")
                )
            )
            if source in totals:
                totals[source]["damageDealt"] += amount
            if target in totals:
                totals[target]["damageTaken"] += amount
    return totals


def _result_row(
    execution: dict[str, Any],
    candidate: dict[str, Any],
    population: dict[str, Any],
    report_root: Path,
) -> dict[str, Any]:
    plan = execution["plan"]
    replay = execution["attempt"] / "replay.json"
    row = {
        "matchId": plan["id"],
        "studyBlockId": plan["studyBlockId"],
        "studyRole": plan["studyRole"],
        "candidateId": candidate["id"],
        "populationId": population["id"],
        "tier": population["tier"],
        "coordinationGrade": population["coordinationGrade"],
        "qualificationProfileId": population["qualificationProfileId"],
        "balanceEvidenceEligible": population["balanceEvidenceEligible"],
        "balanceVerdictEligibility": {
            "eligible": population["balanceEvidenceEligible"],
            "reason": (
                "population carries matching profile-scoped cumulative "
                "qualification evidence"
                if population["balanceEvidenceEligible"]
                else "diagnostic-only population; measurements cannot "
                "select or promote a candidate"
            ),
        },
        "seed": plan["seed"],
        "teamAssignments": plan["teamAssignments"],
        "status": execution["status"],
        "replay": replay.relative_to(report_root).as_posix()
            if replay.exists()
            else None,
    }
    if execution["status"] != "verified":
        return row
    document = json.loads(replay.read_text(encoding="utf-8"))
    issues = _candidate_identity_issues(
        document,
        plan,
        candidate,
        population,
    )
    final_state = (
        document.get("ticks", [])[-1].get("postState", {})
        if document.get("ticks")
        else document.get("initialFrame", {}).get("state", {})
    )
    unsafe = any(
        int(str(item.get("runtimeFaultCount", "0"))) != 0
        or item.get("disqualified") is True
        for item in final_state.get("participants", [])
    )
    if issues or unsafe:
        return {
            **row,
            "status": "invalid-replay",
            "identityIssues": issues,
            "runtimeFaultOrDisqualification": unsafe,
        }
    result = document.get("result", {})
    winner_team = result.get("standings", {}).get("winnerTeamId")
    winner = (
        plan["teamAssignments"].get(str(winner_team))
        if winner_team is not None
        else None
    )
    return {
        **row,
        "winner": winner,
        "winnerTeamId": winner_team,
        "draw": winner is None,
        "completionReason": result.get("completionReason"),
        "terminalReason":
            result.get("mode", {}).get("reason")
            or result.get("completionReason"),
        "endTick": result.get("endTick"),
        "durationTicks":
            result["endTick"] + 1
            if result.get("endTick") is not None
            else 0,
        "replayHash": document.get("replayHash"),
        "trajectoryFingerprint": _trajectory_fingerprint(document),
        "entrantBehavior": _entrant_behavior(
            document,
            plan["teamAssignments"],
        ),
    }


def _payoff_matrix(
    rows: list[dict[str, Any]],
    entrant_ids: list[str],
) -> dict[str, dict[str, float | None]]:
    totals: dict[tuple[str, str], list[float]] = defaultdict(list)
    for row in rows:
        if row["status"] != "verified":
            continue
        team0 = row["teamAssignments"]["0"]
        team1 = row["teamAssignments"]["1"]
        if row["draw"]:
            score0 = 0.0
        else:
            score0 = 1.0 if row["winner"] == team0 else -1.0
        totals[(team0, team1)].append(score0)
        totals[(team1, team0)].append(-score0)
    return {
        first: {
            second:
                statistics.fmean(totals[(first, second)])
                if totals[(first, second)]
                else None
            for second in entrant_ids
        }
        for first in entrant_ids
    }


def _normalized_distribution(
    counts: dict[str, int],
    total: int,
) -> dict[str, float]:
    if total <= 0:
        return {}
    return {
        key: value / total
        for key, value in counts.items()
    }


def _total_variation(
    first: dict[str, float],
    second: dict[str, float],
) -> float:
    keys = set(first) | set(second)
    return 0.5 * sum(
        abs(first.get(key, 0.0) - second.get(key, 0.0))
        for key in keys
    )


def _doctrine_redundancy(
    rows: list[dict[str, Any]],
    population: dict[str, Any],
    payoff_matrix: dict[str, dict[str, float | None]],
) -> dict[str, Any]:
    """Estimate effective doctrines without discarding any entrant.

    The v1 thresholds are intentionally global and diagnostic. Candidate
    promotion cannot depend on them until calibrated against known
    exact-boundary and deliberately redundant populations.
    """

    entrants = [item["id"] for item in population["entrants"]]
    doctrines = {
        item["id"]: item.get("doctrineId", item["id"])
        for item in population["entrants"]
    }
    totals: dict[str, dict[str, Any]] = {
        entrant: {
            "turns": 0,
            "actionKindCounts": Counter(),
            "formTurnCounts": Counter(),
            "objectiveTurns": 0,
            "damageDealt": 0,
            "damageTaken": 0,
        }
        for entrant in entrants
    }
    for row in rows:
        if row.get("status") != "verified":
            continue
        for entrant, signature in row.get(
            "entrantBehavior",
            {},
        ).items():
            if entrant not in totals:
                continue
            item = totals[entrant]
            item["turns"] += int(signature.get("turns", 0))
            item["actionKindCounts"].update(
                signature.get("actionKindCounts", {})
            )
            item["formTurnCounts"].update(
                signature.get("formTurnCounts", {})
            )
            item["objectiveTurns"] += int(
                signature.get("objectiveTurns", 0)
            )
            item["damageDealt"] += int(
                signature.get("damageDealt", 0)
            )
            item["damageTaken"] += int(
                signature.get("damageTaken", 0)
            )

    signatures = {}
    for entrant, item in totals.items():
        turns = item["turns"]
        signatures[entrant] = {
            "turns": turns,
            "actionKindDistribution": _normalized_distribution(
                dict(item["actionKindCounts"]),
                turns,
            ),
            "formDistribution": _normalized_distribution(
                dict(item["formTurnCounts"]),
                turns,
            ),
            "objectiveTurnShare":
                item["objectiveTurns"] / turns if turns else None,
            "damageDealtPer100Turns":
                item["damageDealt"] * 100 / turns if turns else None,
            "damageTakenPer100Turns":
                item["damageTaken"] * 100 / turns if turns else None,
        }

    thresholds = {
        "minimumCommonOpponents": 2,
        "maximumNormalizedPayoffRowDistance": 0.10,
        "maximumActionDistributionDistance": 0.10,
        "maximumFormDistributionDistance": 0.10,
        "maximumObjectiveTurnShareDistance": 0.10,
    }
    pairs = []
    redundant_edges: set[tuple[str, str]] = set()
    for first, second in itertools.combinations(entrants, 2):
        opponents = [
            opponent
            for opponent in entrants
            if opponent not in {first, second}
            and payoff_matrix[first].get(opponent) is not None
            and payoff_matrix[second].get(opponent) is not None
        ]
        payoff_distance = (
            statistics.fmean(
                abs(
                    float(payoff_matrix[first][opponent])
                    - float(payoff_matrix[second][opponent])
                )
                / 2.0
                for opponent in opponents
            )
            if opponents
            else None
        )
        first_signature = signatures[first]
        second_signature = signatures[second]
        action_distance = _total_variation(
            first_signature["actionKindDistribution"],
            second_signature["actionKindDistribution"],
        )
        form_distance = _total_variation(
            first_signature["formDistribution"],
            second_signature["formDistribution"],
        )
        first_objective = first_signature["objectiveTurnShare"]
        second_objective = second_signature["objectiveTurnShare"]
        objective_distance = (
            abs(first_objective - second_objective)
            if first_objective is not None
            and second_objective is not None
            else None
        )
        declared_same = doctrines[first] == doctrines[second]
        measured = (
            len(opponents) >= thresholds["minimumCommonOpponents"]
            and payoff_distance is not None
            and payoff_distance
                <= thresholds["maximumNormalizedPayoffRowDistance"]
            and action_distance
                <= thresholds["maximumActionDistributionDistance"]
            and form_distance
                <= thresholds["maximumFormDistributionDistance"]
            and objective_distance is not None
            and objective_distance
                <= thresholds["maximumObjectiveTurnShareDistance"]
        )
        redundant = declared_same or measured
        if redundant:
            redundant_edges.add((first, second))
        pairs.append(
            {
                "first": first,
                "second": second,
                "declaredSameDoctrine": declared_same,
                "commonOpponentCount": len(opponents),
                "normalizedPayoffRowDistance": payoff_distance,
                "actionDistributionDistance": action_distance,
                "formDistributionDistance": form_distance,
                "objectiveTurnShareDistance": objective_distance,
                "diagnosticallyRedundant": redundant,
                "basis":
                    "declared-doctrine"
                    if declared_same
                    else "measured-v1"
                    if measured
                    else "distinct-or-insufficient-evidence",
            }
        )

    parent = {entrant: entrant for entrant in entrants}

    def find(value: str) -> str:
        while parent[value] != value:
            parent[value] = parent[parent[value]]
            value = parent[value]
        return value

    def union(first: str, second: str) -> None:
        left = find(first)
        right = find(second)
        if left != right:
            parent[max(left, right)] = min(left, right)

    for first, second in sorted(redundant_edges):
        union(first, second)
    groups: dict[str, list[str]] = defaultdict(list)
    for entrant in entrants:
        groups[find(entrant)].append(entrant)
    components = [
        sorted(component)
        for component in groups.values()
    ]
    components.sort()
    return {
        "status": "diagnostic-versioned-thresholds",
        "profileId": "payoff-action-form-objective-redundancy-v1",
        "artifactCount": len(entrants),
        "declaredDoctrineCount": len(set(doctrines.values())),
        "effectiveDoctrineEstimate": len(components),
        "redundancyComponents": components,
        "thresholds": thresholds,
        "entrantSignatures": signatures,
        "pairwiseEvidence": pairs,
        "eligibilityUse": "diagnostic-only-until-calibrated",
    }


def _paired_assignment_sensitivity(rows: list[dict[str, Any]]) -> float | None:
    grouped: dict[tuple[str, str, int], list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        if row["status"] != "verified":
            continue
        pair = tuple(sorted(row["teamAssignments"].values()))
        grouped[(pair[0], pair[1], row["seed"])].append(row)
    comparable = [
        pair_rows
        for pair_rows in grouped.values()
        if len(pair_rows) == 2
        and all(not row["draw"] for row in pair_rows)
    ]
    if not comparable:
        return None
    sensitive = sum(
        1
        for pair_rows in comparable
        if pair_rows[0]["winnerTeamId"] == pair_rows[1]["winnerTeamId"]
    )
    return sensitive / len(comparable)


def _mirrored_units(
    rows: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    grouped: dict[
        tuple[str, str, int],
        list[dict[str, Any]],
    ] = defaultdict(list)
    for row in rows:
        if row.get("status") != "verified":
            continue
        assignments = row["teamAssignments"]
        pair = tuple(sorted(assignments.values()))
        if len(pair) != 2:
            continue
        grouped[(pair[0], pair[1], row["seed"])].append(row)

    units = []
    for (first, second, seed), pair_rows in sorted(grouped.items()):
        by_team_zero = {
            row["teamAssignments"]["0"]: row
            for row in pair_rows
        }
        if set(by_team_zero) != {first, second}:
            continue
        ordered = [by_team_zero[first], by_team_zero[second]]
        trajectory_signature = _canonical_sha256(
            [
                row.get(
                    "trajectoryFingerprint",
                    _canonical_sha256(
                        {
                            "seed": row["seed"],
                            "teamAssignments": row["teamAssignments"],
                            "winnerTeamId": row.get("winnerTeamId"),
                            "durationTicks": row.get("durationTicks"),
                        }
                    ),
                )
                for row in ordered
            ]
        )
        metrics: dict[str, float] = {}
        metric_names = {
            key
            for row in ordered
            for key in row.get("metricObservation", {})
        }
        for metric in sorted(metric_names):
            values = [
                row.get("metricObservation", {}).get(metric)
                for row in ordered
            ]
            if all(isinstance(value, (int, float)) for value in values):
                metrics[metric] = statistics.fmean(values)
        team_zero_payoffs = []
        for row in ordered:
            if row.get("draw"):
                team_zero_payoffs.append(0.0)
            elif row.get("winnerTeamId") == 0:
                team_zero_payoffs.append(1.0)
            else:
                team_zero_payoffs.append(-1.0)
        metrics["team0Payoff"] = statistics.fmean(team_zero_payoffs)
        units.append(
            {
                "pair": f"{first}:{second}",
                "seed": seed,
                "trajectorySignature": trajectory_signature,
                "metrics": metrics,
            }
        )
    return units


def _sampling_evidence(
    rows: list[dict[str, Any]],
    population: dict[str, Any],
    study_design: dict[str, Any],
) -> dict[str, Any]:
    valid = [row for row in rows if row.get("status") == "verified"]
    expected_units = len(valid) // 2
    units = _mirrored_units(valid)
    signatures_by_pair: dict[str, set[str]] = defaultdict(set)
    seeds_by_pair: dict[str, set[int]] = defaultdict(set)
    for unit in units:
        signatures_by_pair[unit["pair"]].add(
            unit["trajectorySignature"]
        )
        seeds_by_pair[unit["pair"]].add(unit["seed"])
    unique_by_pair = {
        pair: len(signatures)
        for pair, signatures in sorted(signatures_by_pair.items())
    }
    effective_units = sum(unique_by_pair.values())
    lineages = {
        entrant.get("authoringLineageId", entrant["id"])
        for entrant in population["entrants"]
    }
    required_units = study_design["minimumMirroredUnitsPerCell"]
    required_pairs = study_design["minimumEntrantPairsPerCell"]
    required_seeds = study_design["minimumSeedsPerEntrantPair"]
    required_lineages = study_design[
        (
            "minimumVotingLineagesPerPopulation"
            if population["balanceEvidenceEligible"]
            else "minimumIndependentLineagesPerPopulation"
        )
    ]
    complete_mirrors = len(units) == expected_units
    eligible = (
        complete_mirrors
        and len(units) >= required_units
        and len(signatures_by_pair) >= required_pairs
        and (
            min((len(seeds) for seeds in seeds_by_pair.values()), default=0)
            >= required_seeds
        )
        and len(lineages) >= required_lineages
    )
    return {
        "analysisUnit": study_design["analysisUnit"],
        "plannedMirroredUnits": expected_units,
        "completeMirroredUnits": len(units),
        "effectiveUniqueTrajectoryUnits": effective_units,
        "duplicateTrajectoryUnits": max(0, len(units) - effective_units),
        "selfPlayMatches": sum(
            len(set(row["teamAssignments"].values())) == 1
            for row in valid
        ),
        "uniqueTrajectoryUnitsByEntrantPair": unique_by_pair,
        "seedResponsiveEntrantPairs": sum(
            len(seeds_by_pair[pair]) > 1
            and len(signatures_by_pair[pair]) > 1
            for pair in signatures_by_pair
        ),
        "independentAuthoringLineages": len(lineages),
        "entrantPairs": len(signatures_by_pair),
        "minimumSeedsObservedPerEntrantPair": min(
            (len(seeds) for seeds in seeds_by_pair.values()),
            default=0,
        ),
        "requiredMirroredUnits": required_units,
        "requiredEntrantPairs": required_pairs,
        "requiredSeedsPerEntrantPair": required_seeds,
        "requiredIndependentLineages": required_lineages,
        "eligible": eligible,
        "reason": (
            "complete mirrors, entrant-pair/seed coverage, and independent "
            "authoring lineage requirements passed"
            if eligible
            else "insufficient complete mirrors, entrant-pair/seed "
            "coverage, or independent authoring lineages"
        ),
    }


def _bootstrap_mean_interval(
    values: list[float],
    *,
    confidence: float,
    resamples: int,
    identity: str,
) -> dict[str, Any]:
    if len(values) < 2:
        return {
            "status": "not-estimable",
            "effectiveUnits": len(values),
            "mean": statistics.fmean(values) if values else None,
            "confidenceLevel": confidence,
            "reason": "at least two effective units are required",
        }
    seed = int(hashlib.sha256(identity.encode("utf-8")).hexdigest()[:16], 16)
    generator = random.Random(seed)
    sample_count = len(values)
    means = sorted(
        statistics.fmean(
            values[generator.randrange(sample_count)]
            for _ in range(sample_count)
        )
        for _ in range(resamples)
    )
    tail = (1 - confidence) / 2

    def percentile(fraction: float) -> float:
        index = round(fraction * (len(means) - 1))
        return means[max(0, min(index, len(means) - 1))]

    return {
        "status": "estimated",
        "effectiveUnits": len(values),
        "mean": statistics.fmean(values),
        "confidenceLevel": confidence,
        "lower": percentile(tail),
        "upper": percentile(1 - tail),
        "method": "deterministic-cluster-percentile-bootstrap-v1",
        "resamples": resamples,
    }


def _leave_one_lineage_out(
    units: list[dict[str, Any]],
    population: dict[str, Any],
    metric: str,
) -> dict[str, Any]:
    lineage_by_entrant = {
        entrant["id"]: entrant.get(
            "authoringLineageId",
            entrant["id"],
        )
        for entrant in population["entrants"]
    }
    estimates = {}
    for lineage in sorted(set(lineage_by_entrant.values())):
        retained = []
        for unit in units:
            entrants = unit["pair"].split(":")
            if any(
                lineage_by_entrant.get(entrant) == lineage
                for entrant in entrants
            ):
                continue
            value = unit.get("metrics", {}).get(metric)
            if isinstance(value, (int, float)):
                retained.append(float(value))
        estimates[lineage] = (
            statistics.fmean(retained) if retained else None
        )
    estimable = [
        estimate for estimate in estimates.values()
        if estimate is not None
    ]
    return {
        "status": "estimated" if estimable else "not-estimable",
        "estimatesByOmittedLineage": estimates,
        "minimum": min(estimable) if estimable else None,
        "maximum": max(estimable) if estimable else None,
        "interpretation":
            "sensitivity of the frozen finite-population mean; not a "
            "population-generalization confidence interval",
    }


def _cell_report(
    candidate: dict[str, Any],
    population: dict[str, Any],
    rows: list[dict[str, Any]],
    dynamics: dict[str, Any] | None,
    study_design: dict[str, Any] | None = None,
    study_block: dict[str, Any] | None = None,
) -> dict[str, Any]:
    study_design = study_design or {
        "analysisUnit": ANALYSIS_UNIT,
        "confidenceLevel": 0.95,
        "bootstrapResamples": 1000,
        "minimumMirroredUnitsPerCell": 1,
        "minimumEntrantPairsPerCell": 1,
        "minimumSeedsPerEntrantPair": 1,
        "minimumIndependentLineagesPerPopulation": 2,
        "minimumVotingLineagesPerPopulation": 4,
        "minimumVotingTier": "T4",
        "multiplicityPolicy": "diagnostic-no-selection",
        "requiredEvidenceLayers": ["contract-validity"],
    }
    valid = [row for row in rows if row["status"] == "verified"]
    side_wins = Counter(
        str(row["winnerTeamId"])
        for row in valid
        if not row["draw"]
    )
    decisive = sum(side_wins.values())
    side_delta = (
        abs(side_wins["0"] - side_wins["1"]) / decisive
        if decisive
        else None
    )
    entrant_ids = [item["id"] for item in population["entrants"]]
    cell_evidence_eligible = (
        population["balanceEvidenceEligible"]
        and len(valid) == len(rows)
    )
    sampling = _sampling_evidence(
        rows,
        population,
        study_design,
    )
    cell_evidence_eligible = (
        cell_evidence_eligible
        and sampling["eligible"]
        and study_design["multiplicityPolicy"]
            != "diagnostic-no-selection"
        and study_block is not None
        and study_block["role"] in {
            "mechanic-causality",
            "native-product",
        }
    )
    mirrored_units = _mirrored_units(valid)
    side_payoffs = [
        float(unit["metrics"]["team0Payoff"])
        for unit in mirrored_units
    ]
    side_effect = _bootstrap_mean_interval(
        side_payoffs,
        confidence=study_design["confidenceLevel"],
        resamples=study_design["bootstrapResamples"],
        identity=(
            f"{candidate['id']}:{population['id']}:team0-side-effect"
        ),
    )
    side_effect["estimand"] = (
        "mean signed team-0 payoff across mirrored entrant-pair-by-seed "
        "blocks, conditional on this frozen population and seed block"
    )
    side_effect["leaveOneLineageOut"] = _leave_one_lineage_out(
        mirrored_units,
        population,
        "team0Payoff",
    )
    payoff_matrix = _payoff_matrix(valid, entrant_ids)
    doctrine_redundancy = _doctrine_redundancy(
        valid,
        population,
        payoff_matrix,
    )
    report = {
        "studyBlockId": (
            study_block["id"] if study_block is not None else "unspecified"
        ),
        "studyRole": (
            study_block["role"]
            if study_block is not None
            else "infrastructure-smoke"
        ),
        "candidateId": candidate["id"],
        "populationId": population["id"],
        "factors": candidate["factors"],
        "topologyProfileId":
            candidate["contract"]["topologyProfileId"],
        "tier": population["tier"],
        "coordinationGrade": population["coordinationGrade"],
        "populationEntrants": [
            {
                "id": entrant["id"],
                "authoringLineageId": entrant.get(
                    "authoringLineageId",
                    entrant["id"],
                ),
                "doctrineId": entrant.get(
                    "doctrineId",
                    entrant["id"],
                ),
            }
            for entrant in population["entrants"]
        ],
        "qualificationProfileId": population["qualificationProfileId"],
        "balanceEvidenceEligible": population["balanceEvidenceEligible"],
        "balanceVerdictEligibility": {
            "eligible": cell_evidence_eligible,
            "reason": (
                "all matches verified for a population carrying matching "
                "profile-scoped cumulative qualification evidence"
                if cell_evidence_eligible
                else "population is diagnostic-only or its match matrix is "
                "incomplete; measurements cannot select a candidate"
            ),
        },
        "plannedMatches": len(rows),
        "validMatches": len(valid),
        "payoffMatrix": payoff_matrix,
        "balanceVector": {
            "sideSpawnFairness": {
                "status": (
                    "measured-mirrored-block-estimand"
                    if mirrored_units
                    else "not-estimable-no-complete-mirrors"
                ),
                "mirroredTeam0Payoff": side_effect,
                "decisiveGames": decisive,
                "team0DecisiveWinShare":
                    side_wins["0"] / decisive if decisive else None,
                "decisiveWinDelta": side_delta,
                "assignmentSensitivePairShare":
                    _paired_assignment_sensitivity(valid),
            },
            "exploitability": {
                "status": "not-measured",
                "reason": "best-response search is not yet implemented",
            },
            "strategicDiversity": {
                "status": "diagnostic-redundancy-estimate",
                "equilibriumSupport": None,
                "doctrineRedundancy": doctrine_redundancy,
            },
            "skillGradient": {
                "status": "not-measured-within-one-tier-cell",
            },
            "phaseOccurrenceAndDuration": (
                dynamics.get("completionPhases")
                if dynamics is not None
                else None
            ),
            "matchDuration": (
                dynamics.get("duration")
                if dynamics is not None
                else {
                    "medianTicks": statistics.median(
                        row["durationTicks"] for row in valid
                    ) if valid else None,
                }
            ),
            "activityAndPressure": (
                {
                    "activity": dynamics["activity"],
                    "combat": dynamics["combat"],
                }
                if dynamics is not None
                else None
            ),
            "comebackAndCounterplay": (
                {
                    "scoreLeadChanges":
                        dynamics["objective"]["scoreLeadChanges"],
                    "pushDirectionReversals":
                        dynamics["objective"]["pushDirectionReversals"],
                    "contestedToSoleTransitions":
                        dynamics["objective"][
                            "contestedToSoleTransitions"
                        ],
                }
                if dynamics is not None
                else None
            ),
            "robustness": {
                "pairedSeedCount": len({row["seed"] for row in valid}),
                "samplingEvidence": sampling,
                "smallParameterPerturbations": "not-measured",
                "holdout": "not-run",
            },
        },
        "matches": rows,
    }
    if dynamics is not None:
        report["balanceVector"]["safety"] = dynamics["safety"]
        report["balanceVector"]["earlyWinVsOpeningSnowball"] = {
            "gamesEndingBeforeFirstUnlock":
                dynamics["opening"]["gamesEndingBeforeFirstUnlock"],
            "openingDamageAmount": dynamics["opening"]["damageAmount"],
            "openingPushes": dynamics["opening"]["pushes"],
        }
    return report


def _factor_contrasts(
    cells: list[dict[str, Any]],
    factors: dict[str, list[str]],
    study_design: dict[str, Any],
) -> list[dict[str, Any]]:
    comparison_specs = []
    by_block_population: dict[
        tuple[str, str],
        list[dict[str, Any]],
    ] = defaultdict(list)
    for cell in cells:
        if cell["studyRole"] not in {
            "mechanic-causality",
            "infrastructure-smoke",
        }:
            continue
        by_block_population[
            (cell["studyBlockId"], cell["populationId"])
        ].append(cell)
    for (
        study_block_id,
        population_id,
    ), population_cells in sorted(by_block_population.items()):
        factor_names = sorted(population_cells[0]["factors"])
        for factor_name in factor_names:
            other_names = [
                name for name in factor_names if name != factor_name
            ]
            grouped: dict[tuple[tuple[str, str], ...], list[dict[str, Any]]] = (
                defaultdict(list)
            )
            for cell in population_cells:
                key = tuple(
                    (name, cell["factors"][name])
                    for name in other_names
                )
                grouped[key].append(cell)
            for held, comparison in sorted(grouped.items()):
                if len(comparison) < 2:
                    continue
                comparison = sorted(
                    comparison,
                    key=lambda item: factors[factor_name].index(
                        item["factors"][factor_name]
                    ),
                )
                baseline = comparison[0]
                for candidate in comparison[1:]:
                    comparison_specs.append(
                        {
                            "studyBlockId": study_block_id,
                            "populationId": population_id,
                            "factor": factor_name,
                            "heldFactors": dict(held),
                            "baseline": baseline,
                            "candidate": candidate,
                        }
                    )

    family_size = max(1, len(comparison_specs))
    nominal_confidence = study_design["confidenceLevel"]
    confidence = (
        1 - (1 - nominal_confidence) / family_size
        if study_design["multiplicityPolicy"]
            == "bonferroni-all-contrasts-v1"
        else nominal_confidence
    )
    contrasts = []
    for comparison in comparison_specs:
        baseline = comparison["baseline"]
        candidate = comparison["candidate"]
        base_units = {
            (unit["pair"], unit["seed"]): unit
            for unit in _mirrored_units(baseline["matches"])
        }
        candidate_units = {
            (unit["pair"], unit["seed"]): unit
            for unit in _mirrored_units(candidate["matches"])
        }
        common_keys = sorted(set(base_units).intersection(candidate_units))
        effect_units = []
        for key in common_keys:
            before = base_units[key]
            after = candidate_units[key]
            metric_names = set(before["metrics"]).intersection(
                after["metrics"]
            )
            effect_units.append(
                {
                    "pair": before["pair"],
                    "seed": before["seed"],
                    "trajectorySignature": _canonical_sha256(
                        [
                            before["trajectorySignature"],
                            after["trajectorySignature"],
                        ]
                    ),
                    "metrics": {
                        metric: (
                            float(after["metrics"][metric])
                            - float(before["metrics"][metric])
                        )
                        for metric in sorted(metric_names)
                    },
                }
            )
        metric_evidence = {}
        for metric in (
            "durationTicks",
            "activeShare",
            "damagePer100Ticks",
            "stalled",
            "maxNoInteractionRunTicks",
            "team0Payoff",
        ):
            values = [
                unit["metrics"][metric]
                for unit in effect_units
                if metric in unit["metrics"]
            ]
            interval = _bootstrap_mean_interval(
                values,
                confidence=confidence,
                resamples=study_design["bootstrapResamples"],
                identity=(
                    f"{comparison['studyBlockId']}:"
                    f"{comparison['populationId']}:"
                    f"{comparison['factor']}:"
                    f"{baseline['candidateId']}:"
                    f"{candidate['candidateId']}:{metric}"
                ),
            )
            interval["estimand"] = (
                "paired mean delta across mirrored entrant-pair-by-seed "
                "blocks, conditional on the frozen population and seeds"
            )
            interval["leaveOneLineageOut"] = _leave_one_lineage_out(
                effect_units,
                {
                    "entrants": baseline["populationEntrants"],
                },
                metric,
            )
            metric_evidence[metric] = interval
        trajectory_effects_by_pair: dict[str, set[str]] = defaultdict(set)
        for unit in effect_units:
            trajectory_effects_by_pair[unit["pair"]].add(
                unit["trajectorySignature"]
            )
        contrasts.append(
            {
                "studyBlockId": comparison["studyBlockId"],
                "populationId": comparison["populationId"],
                "balanceEvidenceEligible": (
                    baseline["balanceVerdictEligibility"]["eligible"]
                    and candidate["balanceVerdictEligibility"]["eligible"]
                ),
                "factor": comparison["factor"],
                "heldFactors": comparison["heldFactors"],
                "from": baseline["factors"][comparison["factor"]],
                "to": candidate["factors"][comparison["factor"]],
                "pairedMirroredUnits": len(effect_units),
                "distinctTrajectoryEffects": sum(
                    len(signatures)
                    for signatures in trajectory_effects_by_pair.values()
                ),
                "metricEffects": metric_evidence,
                "multiplicity": {
                    "policy": study_design["multiplicityPolicy"],
                    "familySize": family_size,
                    "nominalConfidenceLevel": nominal_confidence,
                    "reportedConfidenceLevel": confidence,
                },
                "causalInterpretation": (
                    "eligible only when this study block pins the same "
                    "artifacts, assignments, numeric seeds, and verified "
                    "shared seed profile; intervals are conditional on the "
                    "frozen finite population"
                ),
            }
        )
    return contrasts


def _write_report(output: Path, report: dict[str, Any]) -> None:
    (output / "report.json").write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    lines = [
        f"# {report['experimentId']} Balance Lab report",
        "",
        (
            f"Decision profile: `{report['decisionProfileId']}`. "
            "No composite balance score is calculated. "
            f"Balance verdict eligible: "
            f"`{str(report['balanceVerdictEligible']).lower()}`. "
            f"Candidate promotion eligible: "
            f"`{str(report['candidatePromotionEligible']).lower()}`."
        ),
        "",
        "| Study | Candidate | Population | Valid | Effective doctrines | Side effect | Median ticks |",
        "| --- | --- | --- | ---: | ---: | ---: | ---: |",
    ]
    for cell in report["cells"]:
        vector = cell["balanceVector"]
        side_delta = vector["sideSpawnFairness"]["decisiveWinDelta"]
        side_delta_text = (
            f"{side_delta:.1%}"
            if side_delta is not None
            else "not estimable"
        )
        lines.append(
            f"| {cell['studyBlockId']} | {cell['candidateId']} | "
            f"{cell['populationId']} | "
            f"{cell['validMatches']}/{cell['plannedMatches']} | "
            f"{vector['strategicDiversity']['doctrineRedundancy']['effectiveDoctrineEstimate']}"
            f"/{vector['strategicDiversity']['doctrineRedundancy']['artifactCount']} | "
            f"{side_delta_text} | "
            f"{vector['matchDuration'].get('medianTicks')} |"
        )
    lines.extend(
        [
            "",
            "Exploitability, equilibrium support, automated best responses, "
            "restricted-play ablations, and human entertainment review remain "
            "explicitly unmeasured in the current slice.",
            "",
        ]
    )
    (output / "report.md").write_text(
        "\n".join(lines),
        encoding="utf-8",
    )


def run(
    spec_path: Path,
    output: Path,
    *,
    dry_run: bool,
    resume: bool,
) -> dict[str, Any]:
    spec = load_spec(spec_path)
    plan = build_plan(spec)
    artifacts = (
        _resume(spec_path, spec, output, plan)
        if resume
        else _freeze(spec_path, spec, output, plan)
    )
    frozen_run = json.loads(
        (output / "run.json").read_text(encoding="utf-8")
    )
    toolchain_values = _toolchain_values(spec, output)
    cells = []
    candidates = {
        candidate["id"]: candidate for candidate in spec["candidates"]
    }
    populations = {
        population["id"]: population for population in spec["populations"]
    }
    for block in spec["studyBlocks"]:
        for candidate_id in block["candidateIds"]:
            candidate = candidates[candidate_id]
            for population_id in block["populationIds"]:
                population = populations[population_id]
                cell_plan = [
                    item for item in plan
                    if item["studyBlockId"] == block["id"]
                    and item["candidateId"] == candidate["id"]
                    and item["populationId"] == population["id"]
                ]
                cell_artifacts = {
                    entrant["id"]: artifacts[
                        (population["id"], entrant["id"])
                    ]
                    for entrant in population["entrants"]
                }
                cell_root = (
                    output
                    / "studies"
                    / block["id"]
                    / "candidates"
                    / candidate["id"]
                    / "populations"
                    / population["id"]
                )
                _assert_frozen_toolchain(
                    spec,
                    output,
                    frozen_run["toolchainIdentity"],
                )
                executions = COHORT.execute_plan(
                    cell_plan,
                    cell_artifacts,
                    cell_root,
                    candidate["runnerCommand"],
                    spec["verifyCommand"],
                    ROOT,
                    dry_run=dry_run,
                    reverify_existing=resume,
                    extra_values=toolchain_values,
                )
                _assert_frozen_toolchain(
                    spec,
                    output,
                    frozen_run["toolchainIdentity"],
                )
                rows = [
                    _result_row(
                        execution,
                        candidate,
                        population,
                        output,
                    )
                    for execution in executions
                ]
                dynamics = None
                if (
                    not dry_run
                    and spec.get("dynamicsAdapter")
                        == "generic-frontline-replay-v3"
                    and all(row["status"] == "verified" for row in rows)
                ):
                    analyzed = []
                    for execution in executions:
                        replay = execution["attempt"] / "replay.json"
                        document = json.loads(
                            replay.read_text(encoding="utf-8")
                        )
                        analyzed.append(
                            EVALUATOR.analyze_replay(
                                document,
                                source=(
                                    replay.relative_to(output).as_posix()
                                ),
                                group=(
                                    f"{block['id']}--{candidate['id']}--"
                                    f"{population['id']}"
                                ),
                            ),
                        )
                    group_name = (
                        f"{block['id']}--{candidate['id']}--"
                        f"{population['id']}"
                    )
                    dynamics = EVALUATOR.summarize_group(
                        group_name,
                        analyzed,
                    )
                    for row, match_analysis in zip(
                        rows,
                        analyzed,
                    ):
                        row["metricObservation"] = {
                            "durationTicks": float(
                                match_analysis["duration"]["ticks"]
                            ),
                            "activeShare": (
                                match_analysis["activity"]["activeTicks"]
                                / match_analysis["duration"]["ticks"]
                                if match_analysis["duration"]["ticks"] > 0
                                else 0.0
                            ),
                            "damagePer100Ticks": float(
                                match_analysis["combat"][
                                    "damagePer100Ticks"
                                ]
                            ),
                            "stalled": (
                                1.0
                                if match_analysis["activity"]["stalled"]
                                else 0.0
                            ),
                            "maxNoInteractionRunTicks": float(
                                match_analysis["activity"][
                                    "longestNoInteractionRunTicks"
                                ]
                            ),
                        }
                    (cell_root / "dynamics.json").write_text(
                        json.dumps(
                            {
                                "schemaVersion":
                                    EVALUATOR.REPORT_SCHEMA_VERSION,
                                "metricDefinitionsVersion":
                                    EVALUATOR.METRIC_DEFINITIONS_VERSION,
                                "groups": [dynamics],
                                "matches": analyzed,
                            },
                            indent=2,
                            sort_keys=True,
                        ) + "\n",
                        encoding="utf-8",
                    )
                cells.append(
                    _cell_report(
                        candidate,
                        population,
                        rows,
                        dynamics,
                        spec["studyDesign"],
                        block,
                    )
                )
    valid_matches = sum(cell["validMatches"] for cell in cells)
    planned_matches = sum(cell["plannedMatches"] for cell in cells)
    report_status = (
        "planned"
        if dry_run
        else "complete"
        if valid_matches == planned_matches
        else "invalid"
    )
    voting_cells = [
        cell for cell in cells
        if cell["studyRole"] in {
            "mechanic-causality",
            "native-product",
        }
    ]
    contract_valid = report_status == "complete"
    qualification_passed = (
        bool(voting_cells)
        and all(cell["balanceEvidenceEligible"] for cell in voting_cells)
    )
    population_passed = (
        bool(voting_cells)
        and all(
            cell["balanceVector"]["robustness"]["samplingEvidence"][
                "eligible"
            ]
            for cell in voting_cells
        )
    )
    statistics_passed = (
        population_passed
        and spec["studyDesign"]["multiplicityPolicy"]
            != "diagnostic-no-selection"
    )
    evidence_layers = {
        layer: {
            "status": "not-measured",
            "reason": "no evidence artifact has been joined for this layer",
        }
        for layer in EVIDENCE_LAYERS
    }
    evidence_layers["contract-validity"] = {
        "status": "passed" if contract_valid else "failed",
        "reason": (
            "every planned replay verified against the frozen contract"
            if contract_valid
            else "the planned replay matrix is incomplete or invalid"
        ),
    }
    evidence_layers["qualification"] = {
        "status": "passed" if qualification_passed else "not-measured",
        "reason": (
            "all voting cells use matching balance-eligible qualification"
            if qualification_passed
            else "no complete voting block with qualified populations"
        ),
    }
    evidence_layers["population-cross-play"] = {
        "status": "passed" if population_passed else "not-measured",
        "reason": (
            "declared entrant-pair, seed, mirror, and lineage coverage passed"
            if population_passed
            else "no voting population passed its declared coverage contract"
        ),
    }
    evidence_layers["statistical-sufficiency"] = {
        "status": "passed" if statistics_passed else "not-measured",
        "reason": (
            "finite-matrix coverage passed under the preregistered "
            "multiplicity policy; inference remains conditional on the "
            "frozen population"
            if statistics_passed
            else "diagnostic-only policy or insufficient voting coverage"
        ),
    }
    evidence_layers["holdout"] = {
        "status": (
            "committed-not-run"
            if spec["holdout"]["protocol"]
                == "sha256-commit-reveal-v1"
            else "not-configured"
        ),
        "reason": (
            "a digest and seed count are committed, but no reveal has been "
            "consumed"
            if spec["holdout"]["protocol"]
                == "sha256-commit-reveal-v1"
            else spec["holdout"]["reason"]
        ),
    }
    balance_verdict_eligible = (
        contract_valid
        and qualification_passed
        and population_passed
        and statistics_passed
    )
    required_layers = spec["studyDesign"]["requiredEvidenceLayers"]
    candidate_promotion_eligible = (
        balance_verdict_eligible
        and all(
            evidence_layers[layer]["status"] == "passed"
            for layer in required_layers
        )
    )
    report = {
        "schemaVersion": REPORT_SCHEMA_VERSION,
        "experimentId": spec["experimentId"],
        "status": report_status,
        "hypothesis": spec["hypothesis"],
        "decisionProfileId":
            spec["studyDesign"]["decisionProfileId"],
        "studyBlocks": spec["studyBlocks"],
        "candidateDefinition":
            "mode + ruleset + map + match-format + resolved topology",
        "evaluationProfileId": spec["evaluationProfileId"],
        "pairedSeeds": spec["pairedSeeds"],
        "holdout": spec["holdout"],
        "ablationRegistry": {
            "registryId":
                spec["ablationRegistryDocument"]["registryId"],
            "sha256": spec["ablationRegistry"]["sha256"],
            "openItems": [
                item["id"]
                for item in spec["ablationRegistryDocument"]["items"]
                if item["status"] == "open"
            ],
            "items": spec["ablationRegistryDocument"]["items"],
        },
        "studyDesign": spec["studyDesign"],
        "cells": cells,
        "factorContrasts": _factor_contrasts(
            cells,
            spec["factors"],
            spec["studyDesign"],
        ),
        "evidenceLayers": evidence_layers,
        "requiredEvidenceLayers": required_layers,
        "balanceVerdictEligible": balance_verdict_eligible,
        "candidatePromotionEligible": candidate_promotion_eligible,
        "unmeasuredLayers": [
            layer
            for layer in required_layers
            if evidence_layers[layer]["status"] != "passed"
        ],
    }
    _write_report(output, report)
    return report


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--spec", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--resume", action="store_true")
    args = parser.parse_args(argv)
    if args.dry_run and args.resume:
        parser.error("--dry-run and --resume cannot be combined")
    report = run(
        args.spec.resolve(),
        args.output.resolve(),
        dry_run=args.dry_run,
        resume=args.resume,
    )
    valid = sum(cell["validMatches"] for cell in report["cells"])
    planned = sum(cell["plannedMatches"] for cell in report["cells"])
    print(
        f"{report['experimentId']}: {valid}/{planned} verified matches; "
        f"report {args.output.resolve() / 'report.json'}"
    )
    if args.dry_run:
        return 0
    return 0 if valid == planned else 2


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        print(f"balance lab failed: {error}", file=sys.stderr)
        raise SystemExit(2) from error
