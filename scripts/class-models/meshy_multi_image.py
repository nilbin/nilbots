#!/usr/bin/env python3
"""Safely submit one approved Nilbots multiview package to Meshy.

The default mode is an offline dry run. The paid path is deliberately narrow:
one explicit provider-input manifest, one object, one Meshy 6 textured task,
and an exact 30-credit ceiling. Request data URIs and credentials exist only
in process memory and are never printed or written to disk.
"""

from __future__ import annotations

import argparse
import base64
from dataclasses import dataclass
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import struct
import subprocess
import sys
import tempfile
import time
from typing import Any, Callable
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urlsplit, urlunsplit
from urllib.request import Request, urlopen


API_ORIGIN = "https://api.meshy.ai"
BALANCE_PATH = "/openapi/v1/balance"
TASK_COLLECTION_PATH = "/openapi/v1/multi-image-to-3d"
KEYCHAIN_SERVICE = "nilbots.meshy.api"
EXPECTED_CREDITS = 30
MAX_TEXTURE_PROMPT_LENGTH = 600
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
TERMINAL_STATUSES = frozenset({"SUCCEEDED", "FAILED", "CANCELED"})
ACTIVE_STATUSES = frozenset({"PENDING", "IN_PROGRESS"})
KNOWN_STATUSES = TERMINAL_STATUSES | ACTIVE_STATUSES
ASSET_ID_PATTERN = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
SAFE_TASK_ID_PATTERN = re.compile(r"^[A-Za-z0-9._-]+$")
SECRET_KEY_PARTS = ("authorization", "api_key", "apikey", "token", "secret")
OFFICIAL_DOCS = {
    "multiImageApi": "https://docs.meshy.ai/en/api/multi-image-to-3d",
    "balanceApi": "https://docs.meshy.ai/en/api/balance",
    "pricing": "https://docs.meshy.ai/en/api/pricing",
    "terms": "https://www.meshy.ai/terms-of-use",
    "checkedOn": "2026-07-30",
}
FIXED_SETTINGS = {
    "ai_model": "meshy-6",
    "should_texture": True,
    "enable_pbr": True,
    "texture_resolution": "4k",
    "should_remesh": False,
    "pose_mode": "",
    "image_enhancement": False,
    "remove_lighting": True,
    "target_formats": ["glb"],
}


class RunnerError(RuntimeError):
    """A safe, user-facing runner failure."""


@dataclass(frozen=True)
class InputImage:
    role: str
    path: Path
    recorded_path: str
    byte_count: int
    sha256: str
    width: int
    height: int


@dataclass(frozen=True)
class PackagePlan:
    repo_root: Path
    manifest_path: Path
    manifest_sha256: str
    asset_id: str
    status: str
    purpose: str
    texture_prompt: str
    expected_credits: int | None
    provider_allowed: bool
    blockers: tuple[str, ...]
    images: tuple[InputImage, ...]

    @property
    def submission_issues(self) -> tuple[str, ...]:
        issues = list(self.blockers)
        if not self.provider_allowed:
            issues.append("providerSubmission.allowed is not true")
        if not self.purpose:
            issues.append("providerSubmission.purpose is missing")
        if not self.texture_prompt:
            issues.append("providerSubmission.texturePrompt is missing")
        if self.expected_credits != EXPECTED_CREDITS:
            issues.append(
                "providerSubmission.expectedCredits must be "
                f"{EXPECTED_CREDITS}"
            )
        return tuple(issues)


@dataclass(frozen=True)
class DownloadSpec:
    url: str
    filename: str
    expected_kind: str


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Validate one Nilbots Meshy multiview package offline by default, "
            "or explicitly submit/resume a paid task."
        )
    )
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument(
        "--package",
        type=Path,
        help="explicit art/class-models/provider-inputs/meshy/<asset>/manifest.json",
    )
    source.add_argument(
        "--resume-run",
        type=Path,
        help="explicit existing provider-run directory to poll/download",
    )
    parser.add_argument(
        "--submit",
        action="store_true",
        help="make the paid API request; omitted means offline dry run",
    )
    parser.add_argument(
        "--max-credits",
        type=int,
        help=f"required with --submit and must be exactly {EXPECTED_CREDITS}",
    )
    parser.add_argument(
        "--poll-interval-seconds",
        type=float,
        default=10.0,
        help="task polling interval (default: 10)",
    )
    parser.add_argument(
        "--poll-timeout-seconds",
        type=float,
        default=1800.0,
        help="maximum polling duration before a resumable exit (default: 1800)",
    )
    args = parser.parse_args(argv)
    if args.resume_run is not None and args.submit:
        parser.error("--submit cannot be combined with --resume-run")
    if args.resume_run is not None and args.max_credits is not None:
        parser.error("--max-credits is only valid with --submit")
    if args.package is not None and not args.submit and args.max_credits is not None:
        parser.error("--max-credits is only valid with --submit")
    if args.submit and args.max_credits is None:
        parser.error("--submit requires --max-credits 30")
    if args.poll_interval_seconds <= 0:
        parser.error("--poll-interval-seconds must be positive")
    if args.poll_timeout_seconds <= 0:
        parser.error("--poll-timeout-seconds must be positive")
    return args


def repo_root_from_script() -> Path:
    return Path(__file__).resolve().parents[2]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def json_sha256(value: Any) -> str:
    encoded = json.dumps(
        value,
        ensure_ascii=True,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def png_dimensions(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != PNG_SIGNATURE:
        raise RunnerError(f"{path} is not a PNG")
    if header[12:16] != b"IHDR":
        raise RunnerError(f"{path} has no leading PNG IHDR chunk")
    width, height = struct.unpack(">II", header[16:24])
    if width <= 0 or height <= 0:
        raise RunnerError(f"{path} has invalid PNG dimensions")
    return width, height


def is_relative_to(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def recorded_repo_path(path: Path, repo_root: Path) -> str:
    try:
        return path.relative_to(repo_root).as_posix()
    except ValueError as error:
        raise RunnerError(f"{path} must stay inside the repository") from error


def resolve_recorded_path(
    raw_path: object,
    *,
    manifest_path: Path,
    repo_root: Path,
    require_package_local: bool,
) -> Path:
    if not isinstance(raw_path, str) or not raw_path.strip():
        raise RunnerError("manifest path values must be non-empty strings")
    supplied = Path(raw_path)
    if supplied.is_absolute():
        candidate = supplied.resolve()
    else:
        package_candidate = (manifest_path.parent / supplied).resolve()
        repo_candidate = (repo_root / supplied).resolve()
        if package_candidate.exists():
            candidate = package_candidate
        else:
            candidate = repo_candidate
    if not candidate.is_file():
        raise RunnerError(f"manifest file does not exist: {raw_path}")
    if not is_relative_to(candidate, repo_root):
        raise RunnerError(f"manifest file escapes the repository: {raw_path}")
    if require_package_local and not is_relative_to(candidate, manifest_path.parent):
        raise RunnerError(f"provider view escapes its one-object package: {raw_path}")
    return candidate


def require_sha256(value: object, label: str) -> str:
    if (
        not isinstance(value, str)
        or len(value) != 64
        or any(character not in "0123456789abcdef" for character in value)
    ):
        raise RunnerError(f"{label} must be a lowercase SHA-256 hex digest")
    return value


def require_positive_int(value: object, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        raise RunnerError(f"{label} must be a positive integer")
    return value


def load_json_object(path: Path, label: str) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise RunnerError(f"cannot read {label}: {path}") from error
    if not isinstance(value, dict):
        raise RunnerError(f"{label} must be a JSON object: {path}")
    return value


def validate_reference(
    entry: object,
    *,
    label: str,
    manifest_path: Path,
    repo_root: Path,
) -> None:
    if not isinstance(entry, dict):
        raise RunnerError(f"{label} must be an object")
    path = resolve_recorded_path(
        entry.get("path"),
        manifest_path=manifest_path,
        repo_root=repo_root,
        require_package_local=False,
    )
    expected_hash = require_sha256(entry.get("sha256"), f"{label}.sha256")
    if sha256_file(path) != expected_hash:
        raise RunnerError(f"{label} hash does not match: {path}")
    if "width" in entry or "height" in entry:
        expected_width = require_positive_int(entry.get("width"), f"{label}.width")
        expected_height = require_positive_int(entry.get("height"), f"{label}.height")
        actual_width, actual_height = png_dimensions(path)
        if (actual_width, actual_height) != (expected_width, expected_height):
            raise RunnerError(
                f"{label} dimensions do not match: "
                f"recorded {expected_width}x{expected_height}, "
                f"actual {actual_width}x{actual_height}"
            )


def validate_manifest(manifest_path: Path, repo_root: Path) -> PackagePlan:
    manifest_path = manifest_path.resolve()
    repo_root = repo_root.resolve()
    expected_parent = repo_root / "art/class-models/provider-inputs/meshy"
    if not is_relative_to(manifest_path, expected_parent):
        raise RunnerError(
            "package manifest must be under "
            "art/class-models/provider-inputs/meshy/<asset-id>/"
        )
    if manifest_path.name != "manifest.json":
        raise RunnerError("the explicit package path must end in manifest.json")
    manifest = load_json_object(manifest_path, "provider-input manifest")
    if manifest.get("schemaVersion") != 1:
        raise RunnerError("provider-input manifest schemaVersion must be 1")
    if manifest.get("provider") != "meshy":
        raise RunnerError("provider-input manifest provider must be meshy")
    if manifest.get("taskKind") != "image-to-3d-multiview":
        raise RunnerError(
            "provider-input manifest taskKind must be image-to-3d-multiview"
        )

    asset_id = manifest.get("assetId")
    if not isinstance(asset_id, str) or not ASSET_ID_PATTERN.fullmatch(asset_id):
        raise RunnerError("assetId must be a lowercase kebab-case identifier")
    if manifest_path.parent.name != asset_id:
        raise RunnerError("assetId must match the package directory name")

    orientation = manifest.get("orientation")
    if orientation != {
        "modelForward": "+X",
        "imageRight": "+X",
        "worldUp": "+Y",
    }:
        raise RunnerError(
            "orientation must pin modelForward +X, imageRight +X, and worldUp +Y"
        )

    validate_reference(
        manifest.get("sourceSheet"),
        label="sourceSheet",
        manifest_path=manifest_path,
        repo_root=repo_root,
    )
    validate_reference(
        manifest.get("topAuthority"),
        label="topAuthority",
        manifest_path=manifest_path,
        repo_root=repo_root,
    )

    raw_views = manifest.get("views")
    if not isinstance(raw_views, list) or not 1 <= len(raw_views) <= 4:
        raise RunnerError("views must contain between 1 and 4 PNGs")
    images: list[InputImage] = []
    roles: set[str] = set()
    for index, raw_view in enumerate(raw_views):
        label = f"views[{index}]"
        if not isinstance(raw_view, dict):
            raise RunnerError(f"{label} must be an object")
        role = raw_view.get("role")
        if not isinstance(role, str) or not role:
            raise RunnerError(f"{label}.role must be a non-empty string")
        if role in roles:
            raise RunnerError(f"duplicate provider view role: {role}")
        roles.add(role)
        image_path = resolve_recorded_path(
            raw_view.get("path"),
            manifest_path=manifest_path,
            repo_root=repo_root,
            require_package_local=True,
        )
        if image_path.suffix.lower() != ".png":
            raise RunnerError(f"{label}.path must name a PNG: {image_path}")
        actual_width, actual_height = png_dimensions(image_path)
        expected_width = require_positive_int(raw_view.get("width"), f"{label}.width")
        expected_height = require_positive_int(
            raw_view.get("height"), f"{label}.height"
        )
        if (actual_width, actual_height) != (expected_width, expected_height):
            raise RunnerError(
                f"{label} dimensions do not match: recorded "
                f"{expected_width}x{expected_height}, actual "
                f"{actual_width}x{actual_height}"
            )
        actual_hash = sha256_file(image_path)
        expected_hash = require_sha256(raw_view.get("sha256"), f"{label}.sha256")
        if actual_hash != expected_hash:
            raise RunnerError(f"{label} hash does not match: {image_path}")
        images.append(
            InputImage(
                role=role,
                path=image_path,
                recorded_path=recorded_repo_path(image_path, repo_root),
                byte_count=image_path.stat().st_size,
                sha256=actual_hash,
                width=actual_width,
                height=actual_height,
            )
        )
    if "top" not in roles:
        raise RunnerError("one provider view must have role top")

    blockers_value = manifest.get("blockers", [])
    if not isinstance(blockers_value, list) or any(
        not isinstance(blocker, str) or not blocker.strip()
        for blocker in blockers_value
    ):
        raise RunnerError("blockers must be an array of non-empty strings")
    blockers = tuple(blocker.strip() for blocker in blockers_value)

    submission = manifest.get("providerSubmission", {})
    if not isinstance(submission, dict):
        raise RunnerError("providerSubmission must be an object")
    if "settings" in submission:
        raise RunnerError("provider settings are runner-owned; remove settings")
    texture_prompt_value = submission.get("texturePrompt", "")
    if not isinstance(texture_prompt_value, str):
        raise RunnerError("providerSubmission.texturePrompt must be a string")
    texture_prompt = texture_prompt_value.strip()
    if len(texture_prompt) > MAX_TEXTURE_PROMPT_LENGTH:
        raise RunnerError(
            "providerSubmission.texturePrompt exceeds Meshy's 600-character limit"
        )
    purpose_value = submission.get("purpose", "")
    if not isinstance(purpose_value, str):
        raise RunnerError("providerSubmission.purpose must be a string")
    expected_credits_value = submission.get("expectedCredits")
    if expected_credits_value is not None and (
        isinstance(expected_credits_value, bool)
        or not isinstance(expected_credits_value, int)
    ):
        raise RunnerError("providerSubmission.expectedCredits must be an integer")

    status_value = manifest.get("status", "")
    if not isinstance(status_value, str) or not status_value:
        raise RunnerError("status must be a non-empty string")

    return PackagePlan(
        repo_root=repo_root,
        manifest_path=manifest_path,
        manifest_sha256=sha256_file(manifest_path),
        asset_id=asset_id,
        status=status_value,
        purpose=purpose_value.strip(),
        texture_prompt=texture_prompt,
        expected_credits=expected_credits_value,
        provider_allowed=submission.get("allowed") is True,
        blockers=blockers,
        images=tuple(images),
    )


def enforce_credit_guard(max_credits: object) -> None:
    if isinstance(max_credits, bool) or max_credits != EXPECTED_CREDITS:
        raise RunnerError(
            f"--max-credits must be exactly {EXPECTED_CREDITS}; "
            "the runner does not permit a higher per-task ceiling"
        )


def enforce_submission_gate(plan: PackagePlan, max_credits: object) -> None:
    enforce_credit_guard(max_credits)
    if plan.submission_issues:
        joined = "; ".join(plan.submission_issues)
        raise RunnerError(f"provider submission is blocked: {joined}")


def print_plan(plan: PackagePlan) -> None:
    print("OFFLINE DRY RUN — no credential read and no network request")
    print(f"Package: {recorded_repo_path(plan.manifest_path, plan.repo_root)}")
    print(f"Object: {plan.asset_id}")
    print(f"Package status: {plan.status}")
    print(f"Views: {len(plan.images)}")
    for image in plan.images:
        print(
            f"  {image.role}: {image.recorded_path} "
            f"({image.width}x{image.height}, {image.sha256})"
        )
    print("Pinned request:")
    for key, value in FIXED_SETTINGS.items():
        print(f"  {key}: {json.dumps(value)}")
    print(f"  texture_prompt_sha256: {json_sha256(plan.texture_prompt)}")
    print(f"  expected_credits: {EXPECTED_CREDITS}")
    if plan.submission_issues:
        print("Submission gate: BLOCKED")
        for issue in plan.submission_issues:
            print(f"  - {issue}")
    else:
        print("Submission gate: READY (still requires --submit --max-credits 30)")


def read_keychain_token() -> str:
    security = Path("/usr/bin/security")
    if not security.is_file():
        raise RunnerError(
            "macOS /usr/bin/security is required to read the Meshy credential"
        )
    account = pwd.getpwuid(os.getuid()).pw_name
    result = subprocess.run(
        (
            str(security),
            "find-generic-password",
            "-s",
            KEYCHAIN_SERVICE,
            "-a",
            account,
            "-w",
        ),
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        text=True,
    )
    token = result.stdout.rstrip("\r\n")
    if result.returncode != 0 or not token:
        raise RunnerError(
            f"no Keychain credential found for service {KEYCHAIN_SERVICE}"
        )
    return token


def strip_signed_url(value: str) -> str:
    parsed = urlsplit(value)
    if parsed.scheme not in {"http", "https"} or not parsed.netloc:
        return value
    return urlunsplit((parsed.scheme, parsed.netloc, parsed.path, "", ""))


def is_secret_key(key: object) -> bool:
    normalized = str(key).lower().replace("-", "_")
    return any(part in normalized for part in SECRET_KEY_PARTS)


def sanitize_for_record(value: Any) -> Any:
    if isinstance(value, dict):
        return {
            key: sanitize_for_record(item)
            for key, item in value.items()
            if not is_secret_key(key)
        }
    if isinstance(value, list):
        return [sanitize_for_record(item) for item in value]
    if isinstance(value, str):
        if "data:" in value.lower():
            return "<redacted-data-uri>"
        return strip_signed_url(value)
    return value


def safe_json_body(value: object, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise RunnerError(f"Meshy returned a non-object {label}")
    return value


class MeshyClient:
    def __init__(
        self,
        token: str,
        *,
        opener: Callable[..., Any] = urlopen,
        sleeper: Callable[[float], None] = time.sleep,
    ) -> None:
        self._token = token
        self._opener = opener
        self._sleep = sleeper

    def request_json(
        self,
        method: str,
        path: str,
        payload: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        encoded = None
        headers = {
            "Accept": "application/json",
            "Authorization": f"Bearer {self._token}",
            "User-Agent": "nilbots-meshy-authoring/1",
        }
        if payload is not None:
            encoded = json.dumps(payload, separators=(",", ":")).encode("utf-8")
            headers["Content-Type"] = "application/json"
        attempts = 3 if method == "GET" else 1
        for attempt in range(attempts):
            request = Request(
                API_ORIGIN + path,
                data=encoded,
                headers=headers,
                method=method,
            )
            try:
                with self._opener(request, timeout=60) as response:
                    return safe_json_body(
                        json.load(response),
                        f"{method} {path} response",
                    )
            except HTTPError as error:
                retryable = error.code == 429 or 500 <= error.code <= 599
                if retryable and attempt + 1 < attempts:
                    self._sleep(2.0 * (attempt + 1))
                    continue
                raise RunnerError(
                    f"Meshy API returned HTTP {error.code} for {method} {path}"
                ) from None
            except (URLError, TimeoutError, OSError, json.JSONDecodeError):
                if attempt + 1 < attempts:
                    self._sleep(2.0 * (attempt + 1))
                    continue
                retry_note = (
                    " The POST was not retried because Meshy has no documented "
                    "idempotency key; inspect the provider task list before "
                    "attempting another paid submission."
                    if method == "POST"
                    else ""
                )
                raise RunnerError(
                    f"Meshy request failed for {method} {path}.{retry_note}"
                ) from None
        raise AssertionError("request loop exhausted")

    def balance(self) -> int | float:
        response = self.request_json("GET", BALANCE_PATH)
        balance = response.get("balance")
        if isinstance(balance, bool) or not isinstance(balance, (int, float)):
            raise RunnerError("Meshy balance response was not numeric")
        if balance < 0:
            raise RunnerError("Meshy balance response was negative")
        return balance

    def create_task(self, payload: dict[str, Any]) -> str:
        response = self.request_json("POST", TASK_COLLECTION_PATH, payload)
        task_id = response.get("result")
        if not isinstance(task_id, str) or not task_id:
            raise RunnerError("Meshy create response did not contain a task ID")
        return task_id

    def task(self, task_id: str) -> dict[str, Any]:
        encoded_id = quote(task_id, safe="")
        task = self.request_json("GET", f"{TASK_COLLECTION_PATH}/{encoded_id}")
        returned_id = task.get("id")
        if returned_id is not None and returned_id != task_id:
            raise RunnerError("Meshy task response ID did not match the request")
        return task


def build_request_payload(plan: PackagePlan) -> dict[str, Any]:
    payload = dict(FIXED_SETTINGS)
    payload["texture_prompt"] = plan.texture_prompt
    image_urls: list[str] = []
    for image in plan.images:
        encoded = base64.b64encode(image.path.read_bytes()).decode("ascii")
        image_urls.append(f"data:image/png;base64,{encoded}")
    payload["image_urls"] = image_urls
    return payload


def safe_task_directory_name(task_id: str) -> str:
    if (
        SAFE_TASK_ID_PATTERN.fullmatch(task_id)
        and task_id not in {".", ".."}
        and len(task_id) <= 180
    ):
        return task_id
    return "task-" + hashlib.sha256(task_id.encode("utf-8")).hexdigest()[:24]


def preflight_output_directory(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=".meshy-write-check.",
        dir=path,
    )
    os.close(descriptor)
    Path(temporary_name).unlink()


def atomic_write_text(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.",
        suffix=".tmp",
        dir=path.parent,
        text=True,
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            stream.write(content)
        os.replace(temporary_path, path)
    finally:
        if temporary_path.exists():
            temporary_path.unlink()


def atomic_write_json(path: Path, value: Any) -> None:
    atomic_write_text(
        path,
        json.dumps(value, indent=2, ensure_ascii=True, sort_keys=False) + "\n",
    )


def input_records(plan: PackagePlan) -> list[dict[str, Any]]:
    return [
        {
            "role": image.role,
            "file": image.recorded_path,
            "bytes": image.byte_count,
            "sha256": image.sha256,
            "width": image.width,
            "height": image.height,
        }
        for image in plan.images
    ]


def initial_request_record(
    plan: PackagePlan,
    task_id: str,
    submitted_at: str,
    balance_before: int | float,
) -> dict[str, Any]:
    settings = dict(FIXED_SETTINGS)
    settings["texture_prompt"] = plan.texture_prompt
    return {
        "schemaVersion": 1,
        "provider": "Meshy",
        "endpoint": "POST /openapi/v1/multi-image-to-3d",
        "taskId": task_id,
        "assetId": plan.asset_id,
        "purpose": plan.purpose,
        "ownerApproved": True,
        "submittedAt": submitted_at,
        "balanceBefore": balance_before,
        "expectedCredits": EXPECTED_CREDITS,
        "maxCredits": EXPECTED_CREDITS,
        "inputs": input_records(plan),
        "settings": settings,
        "sourcePackage": {
            "manifest": recorded_repo_path(plan.manifest_path, plan.repo_root),
            "sha256": plan.manifest_sha256,
            "status": plan.status,
        },
        "credentialStore": {
            "platform": "macOS Keychain",
            "service": KEYCHAIN_SERVICE,
            "account": "current-login-account",
            "valueRecorded": False,
        },
        "officialDocs": OFFICIAL_DOCS,
        "assetRetentionNote": (
            "Meshy output URLs expire; provider output is downloaded "
            "immediately and URLs are not storage."
        ),
    }


def consumed_credits(task: dict[str, Any]) -> int | float | None:
    value = task.get("consumed_credits")
    if value is None:
        return None
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise RunnerError("Meshy task consumed_credits was not numeric")
    if value < 0:
        raise RunnerError("Meshy task consumed_credits was negative")
    return value


def task_status(task: dict[str, Any]) -> str:
    status = task.get("status")
    if not isinstance(status, str) or status not in KNOWN_STATUSES:
        raise RunnerError(f"Meshy returned an unknown task status: {status!r}")
    return status


def poll_task(
    client: MeshyClient,
    task_id: str,
    *,
    interval_seconds: float,
    timeout_seconds: float,
    record_path: Path,
    request_record: dict[str, Any],
) -> tuple[dict[str, Any], bool]:
    deadline = time.monotonic() + timeout_seconds
    last_summary: tuple[str, object] | None = None
    credit_violation = False
    while True:
        task = client.task(task_id)
        status = task_status(task)
        credits = consumed_credits(task)
        progress = task.get("progress")
        summary = (status, progress)
        if summary != last_summary:
            print(f"Task {status.lower()}: progress={progress!r}")
            last_summary = summary
        request_record["lastStatus"] = status
        request_record["lastProgress"] = progress
        request_record["lastPolledAt"] = utc_now()
        if credits is not None:
            request_record["consumedCreditsObserved"] = credits
            if credits > EXPECTED_CREDITS:
                credit_violation = True
                request_record["maxCreditViolation"] = True
        atomic_write_json(record_path, request_record)
        if status in TERMINAL_STATUSES:
            return task, credit_violation
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            atomic_write_json(
                record_path.parent / "task.sanitized.json",
                sanitize_for_record(task),
            )
            raise RunnerError(
                "poll timeout; the paid task was recorded and can be resumed "
                f"with --resume-run {record_path.parent}"
            )
        time.sleep(min(interval_seconds, remaining))


def validate_asset_url(url: object) -> str:
    if not isinstance(url, str) or not url:
        raise RunnerError("Meshy returned an empty output URL")
    parsed = urlsplit(url)
    host = (parsed.hostname or "").lower()
    if parsed.scheme != "https" or not (
        host == "meshy.ai" or host.endswith(".meshy.ai")
    ):
        raise RunnerError("Meshy returned an unexpected output URL origin")
    return url


def safe_label(value: object) -> str:
    label = re.sub(r"[^a-z0-9]+", "-", str(value).lower()).strip("-")
    return label or "output"


def collect_downloads(task: dict[str, Any]) -> list[DownloadSpec]:
    specs: list[DownloadSpec] = []
    model_urls = task.get("model_urls", {})
    if model_urls is not None and not isinstance(model_urls, dict):
        raise RunnerError("Meshy model_urls was not an object")
    for format_name, value in (model_urls or {}).items():
        if not value:
            continue
        format_label = safe_label(format_name)
        filename = (
            "raw-model.glb"
            if format_name == "glb"
            else f"raw-model-{format_label}.{format_label}"
        )
        specs.append(
            DownloadSpec(
                validate_asset_url(value),
                filename,
                "glb" if format_name == "glb" else "binary",
            )
        )
    if not any(spec.filename == "raw-model.glb" for spec in specs):
        legacy_model = task.get("model_url")
        if legacy_model:
            specs.insert(
                0,
                DownloadSpec(
                    validate_asset_url(legacy_model),
                    "raw-model.glb",
                    "glb",
                ),
            )

    thumbnail = task.get("thumbnail_url")
    if thumbnail:
        specs.append(
            DownloadSpec(
                validate_asset_url(thumbnail),
                "provider-preview.png",
                "png",
            )
        )
    alpha_thumbnail = task.get("alpha_thumbnail_url")
    if alpha_thumbnail:
        specs.append(
            DownloadSpec(
                validate_asset_url(alpha_thumbnail),
                "provider-preview-alpha.png",
                "png",
            )
        )
    thumbnails = task.get("thumbnail_urls", {})
    if thumbnails is not None and not isinstance(thumbnails, dict):
        raise RunnerError("Meshy thumbnail_urls was not an object")
    for view, value in (thumbnails or {}).items():
        if value:
            specs.append(
                DownloadSpec(
                    validate_asset_url(value),
                    f"provider-preview-{safe_label(view)}.png",
                    "png",
                )
            )

    textures = task.get("texture_urls", [])
    if textures is not None and not isinstance(textures, list):
        raise RunnerError("Meshy texture_urls was not an array")
    for index, texture_set in enumerate(textures or []):
        if not isinstance(texture_set, dict):
            raise RunnerError("Meshy texture_urls entry was not an object")
        for map_name, value in texture_set.items():
            if value:
                specs.append(
                    DownloadSpec(
                        validate_asset_url(value),
                        f"texture-{index}-{safe_label(map_name)}.png",
                        "png",
                    )
                )

    deduplicated: list[DownloadSpec] = []
    seen_urls: set[str] = set()
    seen_filenames: set[str] = set()
    for spec in specs:
        unsigned = strip_signed_url(spec.url)
        if unsigned in seen_urls:
            continue
        if spec.filename in seen_filenames:
            raise RunnerError(f"duplicate provider output filename: {spec.filename}")
        seen_urls.add(unsigned)
        seen_filenames.add(spec.filename)
        deduplicated.append(spec)
    if not any(spec.filename == "raw-model.glb" for spec in deduplicated):
        raise RunnerError("succeeded Meshy task did not expose a GLB")
    return deduplicated


def download_file(
    spec: DownloadSpec,
    destination: Path,
    *,
    opener: Callable[..., Any] = urlopen,
) -> dict[str, Any]:
    if destination.exists():
        byte_count = destination.stat().st_size
        digest = sha256_file(destination)
        validate_download(destination, spec.expected_kind)
        return {
            "file": destination.name,
            "bytes": byte_count,
            "sha256": digest,
            "contentType": None,
            "reusedExisting": True,
        }

    temporary = destination.with_name(f".{destination.name}.download")
    if temporary.exists():
        temporary.unlink()
    digest = hashlib.sha256()
    byte_count = 0
    content_type: str | None = None
    try:
        request = Request(
            spec.url,
            headers={"User-Agent": "nilbots-meshy-authoring/1"},
            method="GET",
        )
        with opener(request, timeout=120) as response, temporary.open("xb") as output:
            content_type = response.headers.get_content_type()
            while True:
                chunk = response.read(1024 * 1024)
                if not chunk:
                    break
                output.write(chunk)
                digest.update(chunk)
                byte_count += len(chunk)
        validate_download(temporary, spec.expected_kind)
        os.replace(temporary, destination)
    except Exception:
        if temporary.exists():
            temporary.unlink()
        raise
    return {
        "file": destination.name,
        "bytes": byte_count,
        "sha256": digest.hexdigest(),
        "contentType": content_type,
        "reusedExisting": False,
    }


def validate_download(path: Path, expected_kind: str) -> None:
    with path.open("rb") as stream:
        header = stream.read(12)
    if expected_kind == "glb" and header[:4] != b"glTF":
        raise RunnerError(f"downloaded file is not a GLB: {path.name}")
    if expected_kind == "png" and header[:8] != PNG_SIGNATURE:
        raise RunnerError(f"downloaded file is not a PNG: {path.name}")
    if path.stat().st_size == 0:
        raise RunnerError(f"downloaded file is empty: {path.name}")


def download_outputs(
    task: dict[str, Any],
    run_directory: Path,
) -> tuple[list[dict[str, Any]], list[dict[str, str]]]:
    artifacts: list[dict[str, Any]] = []
    failures: list[dict[str, str]] = []
    for spec in collect_downloads(task):
        destination = run_directory / spec.filename
        try:
            artifact = download_file(spec, destination)
            artifacts.append(artifact)
            print(
                f"Downloaded {artifact['file']} "
                f"({artifact['bytes']} bytes, {artifact['sha256']})"
            )
        except Exception as error:
            failures.append(
                {
                    "file": spec.filename,
                    "error": type(error).__name__,
                }
            )
            print(
                f"Download failed for {spec.filename}: {type(error).__name__}",
                file=sys.stderr,
            )
    return artifacts, failures


def finish_run(
    *,
    client: MeshyClient,
    task: dict[str, Any],
    run_directory: Path,
    request_record: dict[str, Any],
    credit_violation: bool,
) -> int:
    status = task_status(task)
    artifacts: list[dict[str, Any]] = []
    failures: list[dict[str, str]] = []
    if status == "SUCCEEDED":
        artifacts, failures = download_outputs(task, run_directory)

    atomic_write_json(
        run_directory / "task.sanitized.json",
        sanitize_for_record(task),
    )
    balance_after = client.balance()
    credits = consumed_credits(task)
    request_record.update(
        {
            "finishedAt": utc_now(),
            "status": status,
            "balanceAfter": balance_after,
            "consumedCredits": credits,
            "artifacts": artifacts,
        }
    )
    if failures:
        request_record["downloadFailures"] = failures
    if credit_violation or (credits is not None and credits > EXPECTED_CREDITS):
        request_record["maxCreditViolation"] = True
        credit_violation = True
    atomic_write_json(run_directory / "request-record.json", request_record)
    write_run_markdown(run_directory, request_record)

    if status != "SUCCEEDED":
        raise RunnerError(f"Meshy task ended with status {status}")
    if failures:
        raise RunnerError(
            "one or more expiring outputs failed to download; resume the run "
            "immediately"
        )
    if credit_violation:
        raise RunnerError(
            "Meshy reported consumed_credits above the 30-credit ceiling; "
            "outputs were preserved but provider pricing must be investigated"
        )
    return 0


def write_run_markdown(
    run_directory: Path,
    request_record: dict[str, Any],
) -> None:
    lines = [
        f"# Meshy {request_record['assetId']} provider run",
        "",
        f"- Provider: Meshy",
        f"- Task: `{request_record['taskId']}`",
        f"- Status: {str(request_record.get('status', 'unknown')).lower()}",
        (
            f"- Credits: {request_record.get('consumedCredits')} "
            f"(`{request_record.get('balanceBefore')}` before, "
            f"`{request_record.get('balanceAfter')}` after)"
        ),
        f"- Source package: `{request_record['sourcePackage']['manifest']}`",
        "",
        "The credential came from macOS Keychain service "
        f"`{KEYCHAIN_SERVICE}`. No credential value, Authorization header, "
        "input data URI, or signed download query is stored here.",
        "",
        "Provider masters and lossless texture maps are ignored by ordinary "
        "Git. Preserve them in durable artifact storage before deleting the "
        "worktree; only a corrected, reviewed derivative may enter runtime.",
        "",
    ]
    atomic_write_text(run_directory / "RUN.md", "\n".join(lines))


def submit_package(
    plan: PackagePlan,
    *,
    max_credits: int,
    interval_seconds: float,
    timeout_seconds: float,
) -> int:
    enforce_submission_gate(plan, max_credits)
    asset_directory = (
        plan.repo_root
        / "art/class-models/provider-runs/meshy"
        / plan.asset_id
    )
    preflight_output_directory(asset_directory)
    token = read_keychain_token()
    client = MeshyClient(token)
    balance_before = client.balance()
    if balance_before < EXPECTED_CREDITS:
        raise RunnerError(
            f"Meshy balance {balance_before} is below the "
            f"{EXPECTED_CREDITS}-credit task cost"
        )
    submitted_at = utc_now()
    payload = build_request_payload(plan)
    task_id = client.create_task(payload)
    del payload
    run_directory = asset_directory / safe_task_directory_name(task_id)
    if run_directory.exists():
        raise RunnerError(f"provider run directory already exists: {run_directory}")
    run_directory.mkdir(parents=True)
    request_record = initial_request_record(
        plan,
        task_id,
        submitted_at,
        balance_before,
    )
    record_path = run_directory / "request-record.json"
    atomic_write_json(record_path, request_record)
    print(f"Created Meshy task {task_id}; provenance recorded at {run_directory}")
    task, credit_violation = poll_task(
        client,
        task_id,
        interval_seconds=interval_seconds,
        timeout_seconds=timeout_seconds,
        record_path=record_path,
        request_record=request_record,
    )
    return finish_run(
        client=client,
        task=task,
        run_directory=run_directory,
        request_record=request_record,
        credit_violation=credit_violation,
    )


def resume_run(
    run_directory: Path,
    *,
    repo_root: Path,
    interval_seconds: float,
    timeout_seconds: float,
) -> int:
    run_directory = run_directory.resolve()
    expected_root = repo_root / "art/class-models/provider-runs/meshy"
    if not is_relative_to(run_directory, expected_root):
        raise RunnerError(
            "resume directory must be under art/class-models/provider-runs/meshy"
        )
    record_path = run_directory / "request-record.json"
    request_record = load_json_object(record_path, "request record")
    if request_record.get("schemaVersion") != 1:
        raise RunnerError("resume supports only runner request-record schema 1")
    if request_record.get("provider") != "Meshy":
        raise RunnerError("request record provider is not Meshy")
    if request_record.get("maxCredits") != EXPECTED_CREDITS:
        raise RunnerError("request record does not pin the 30-credit ceiling")
    task_id = request_record.get("taskId")
    if not isinstance(task_id, str) or not task_id:
        raise RunnerError("request record has no task ID")
    token = read_keychain_token()
    client = MeshyClient(token)
    task, credit_violation = poll_task(
        client,
        task_id,
        interval_seconds=interval_seconds,
        timeout_seconds=timeout_seconds,
        record_path=record_path,
        request_record=request_record,
    )
    return finish_run(
        client=client,
        task=task,
        run_directory=run_directory,
        request_record=request_record,
        credit_violation=credit_violation,
    )


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    repo_root = repo_root_from_script()
    if args.resume_run is not None:
        return resume_run(
            args.resume_run,
            repo_root=repo_root,
            interval_seconds=args.poll_interval_seconds,
            timeout_seconds=args.poll_timeout_seconds,
        )
    plan = validate_manifest(args.package, repo_root)
    if not args.submit:
        print_plan(plan)
        return 0
    return submit_package(
        plan,
        max_credits=args.max_credits,
        interval_seconds=args.poll_interval_seconds,
        timeout_seconds=args.poll_timeout_seconds,
    )


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RunnerError as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(2) from None
