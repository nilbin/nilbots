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
REPORT_SCHEMA_VERSION = 2
TWO_TEAM_ZERO_SUM_PROFILE = "two-team-zero-sum-v1"
PIPELINE_FILES = (
    Path("balance/balance-lab-spec.schema.json"),
    Path("scripts/balance-lab-drive.py"),
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


def _contract(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError(f"{label} must be an object")
    required = {
        "modeId",
        "rulesetId",
        "rulesFingerprint",
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
        "evidenceClass",
        "hypothesis",
        "factors",
        "pairedSeeds",
        "holdoutSeeds",
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
    if document.get("schemaVersion") != 2:
        raise ValueError("Balance Lab schemaVersion must be 2")
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
    if document.get("evidenceClass") not in {
        "compatibility",
        "same-cohort-causality",
        "native-product",
        "infrastructure-smoke",
    }:
        raise ValueError("evidenceClass is unknown")
    if (
        not isinstance(document.get("hypothesis"), str)
        or not document["hypothesis"].strip()
    ):
        raise ValueError("hypothesis must be a non-empty string")
    document["pairedSeeds"] = _non_negative_seeds(
        document.get("pairedSeeds")
    )
    holdout = document["holdoutSeeds"]
    if not isinstance(holdout, list):
        raise ValueError("holdoutSeeds must be an array")
    if holdout:
        document["holdoutSeeds"] = _non_negative_seeds(holdout)
        if set(holdout).intersection(document["pairedSeeds"]):
            raise ValueError("holdoutSeeds must not overlap pairedSeeds")
    else:
        document["holdoutSeeds"] = []

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
            not isinstance(tier, str)
            or not tier.strip()
            or not isinstance(coordination, str)
            or not coordination.strip()
        ):
            raise ValueError(
                f"{population_id}: tier and coordinationGrade are required"
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

    verifier = document.get("verifyCommand")
    if not isinstance(verifier, str) or not verifier.strip():
        raise ValueError("verifyCommand is required")
    if "{replay}" not in verifier:
        raise ValueError("verifyCommand must include {replay}")
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
    for candidate in spec["candidates"]:
        for population in spec["populations"]:
            matches = COHORT.build_plan(
                population["entrants"],
                spec["pairedSeeds"],
            )
            for match in matches:
                plan.append(
                    {
                        **match,
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
    return {**spec, "populations": populations}


def _freeze(
    spec_path: Path,
    spec: dict[str, Any],
    output: Path,
    plan: list[dict[str, Any]],
) -> dict[tuple[str, str], Path]:
    if output.exists():
        raise ValueError(f"output already exists: {output}")
    output.mkdir(parents=True)
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
    ):
        raise ValueError("resume spec or plan does not match run.json")
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


def _cell_report(
    candidate: dict[str, Any],
    population: dict[str, Any],
    rows: list[dict[str, Any]],
    dynamics: dict[str, Any] | None,
) -> dict[str, Any]:
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
    report = {
        "candidateId": candidate["id"],
        "populationId": population["id"],
        "factors": candidate["factors"],
        "topologyProfileId":
            candidate["contract"]["topologyProfileId"],
        "tier": population["tier"],
        "coordinationGrade": population["coordinationGrade"],
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
        "payoffMatrix": _payoff_matrix(valid, entrant_ids),
        "balanceVector": {
            "sideSpawnFairness": {
                "status": (
                    "measured"
                    if decisive
                    else "not-estimable-no-decisive-games"
                ),
                "decisiveGames": decisive,
                "decisiveWinDelta": side_delta,
                "assignmentSensitivePairShare":
                    _paired_assignment_sensitivity(valid),
            },
            "exploitability": {
                "status": "not-measured",
                "reason": "best-response search is not yet implemented",
            },
            "strategicDiversity": {
                "status": "descriptive-payoff-matrix-only",
                "equilibriumSupport": None,
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
                "smallParameterPerturbations": "not-measured",
                "holdoutSeeds": "sealed-not-run",
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
) -> list[dict[str, Any]]:
    contrasts = []
    by_population: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for cell in cells:
        by_population[cell["populationId"]].append(cell)
    for population_id, population_cells in sorted(by_population.items()):
        factor_names = sorted(population_cells[0]["factors"])
        for factor_name in factor_names:
            other_names = [name for name in factor_names
                           if name != factor_name]
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
                    base_vector = baseline["balanceVector"]
                    candidate_vector = candidate["balanceVector"]
                    base_duration = base_vector["matchDuration"].get(
                        "medianTicks"
                    )
                    candidate_duration = candidate_vector[
                        "matchDuration"
                    ].get("medianTicks")
                    base_activity = base_vector["activityAndPressure"]
                    candidate_activity = candidate_vector[
                        "activityAndPressure"
                    ]

                    def delta(
                        section: str,
                        metric: str,
                    ) -> float | int | None:
                        if (
                            base_activity is None
                            or candidate_activity is None
                        ):
                            return None
                        before = base_activity[section].get(metric)
                        after = candidate_activity[section].get(metric)
                        if before is None or after is None:
                            return None
                        return after - before

                    contrasts.append(
                        {
                            "populationId": population_id,
                            "balanceEvidenceEligible":
                                baseline["balanceEvidenceEligible"],
                            "factor": factor_name,
                            "heldFactors": dict(held),
                            "from": baseline["factors"][factor_name],
                            "to": candidate["factors"][factor_name],
                            "medianDurationDeltaTicks": (
                                candidate_duration - base_duration
                                if candidate_duration is not None
                                and base_duration is not None
                                else None
                            ),
                            "activeShareDelta": delta(
                                "activity",
                                "activeShare",
                            ),
                            "damagePer100TicksDelta": delta(
                                "combat",
                                "damagePer100Ticks",
                            ),
                            "stalledGamesDelta": delta(
                                "activity",
                                "stalledGames",
                            ),
                            "maxNoInteractionRunTicksDelta": delta(
                                "activity",
                                "maxNoInteractionRunTicks",
                            ),
                            "causalInterpretation":
                                "same artifacts, pairings, assignments, "
                                "and paired seeds",
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
            f"Evidence class: `{report['evidenceClass']}`. "
            "No composite balance score is calculated. "
            f"Balance verdict eligible: "
            f"`{str(report['balanceVerdictEligible']).lower()}`. "
            f"Candidate promotion eligible: "
            f"`{str(report['candidatePromotionEligible']).lower()}`."
        ),
        "",
        "| Candidate | Population | Valid | Side delta | Median ticks |",
        "| --- | --- | ---: | ---: | ---: |",
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
            f"| {cell['candidateId']} | {cell['populationId']} | "
            f"{cell['validMatches']}/{cell['plannedMatches']} | "
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
    cells = []
    for candidate in spec["candidates"]:
        for population in spec["populations"]:
            cell_plan = [
                item for item in plan
                if item["candidateId"] == candidate["id"]
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
                / "candidates"
                / candidate["id"]
                / "populations"
                / population["id"]
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
                            source=replay.relative_to(output).as_posix(),
                            group=f"{candidate['id']}--{population['id']}",
                        )
                    )
                dynamics = EVALUATOR.summarize_group(
                    f"{candidate['id']}--{population['id']}",
                    analyzed,
                )
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
                _cell_report(candidate, population, rows, dynamics)
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
    unmeasured_layers = [
        "restricted-play capability ablations",
        "best-response exploitability",
        "equilibrium population estimation",
        "automated candidate search",
        "outcome-blind human replay review",
    ]
    balance_verdict_eligible = (
        report_status == "complete"
        and spec["evidenceClass"] in {
            "same-cohort-causality",
            "native-product",
        }
        and all(
            population["balanceEvidenceEligible"]
            for population in spec["populations"]
        )
    )
    report = {
        "schemaVersion": REPORT_SCHEMA_VERSION,
        "experimentId": spec["experimentId"],
        "status": report_status,
        "evidenceClass": spec["evidenceClass"],
        "hypothesis": spec["hypothesis"],
        "candidateDefinition":
            "mode + ruleset + map + match-format + resolved topology",
        "evaluationProfileId": spec["evaluationProfileId"],
        "pairedSeeds": spec["pairedSeeds"],
        "holdoutSeeds": spec["holdoutSeeds"],
        "cells": cells,
        "factorContrasts": _factor_contrasts(cells, spec["factors"]),
        "balanceVerdictEligible": balance_verdict_eligible,
        "candidatePromotionEligible":
            balance_verdict_eligible and not unmeasured_layers,
        "unmeasuredLayers": unmeasured_layers,
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
