#!/usr/bin/env python3
"""Guard one approved Meshy T2 Smart Topology experiment.

The default invocation is a completely offline dry run. A paid task is only
reachable with an explicit named trial, ``--submit``, and the exact
``--max-credits 15`` ceiling. The runner accepts two separately pinned
owner-approved PNG trials, one fixed request, and the committed experiment
ledger. Provider credentials and input data URIs only exist in process memory
and are never printed or written to disk.
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
TASK_COLLECTION_PATH = "/openapi/v1/image-to-3d"
KEYCHAIN_SERVICE = "nilbots.meshy.api"
EXPECTED_CREDITS = 15
EXPERIMENT_MAX_CALLS = 3
EXPERIMENT_MAX_CREDITS = 45
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
TERMINAL_STATUSES = frozenset({"SUCCEEDED", "FAILED", "CANCELED"})
ACTIVE_STATUSES = frozenset({"PENDING", "IN_PROGRESS"})
KNOWN_STATUSES = TERMINAL_STATUSES | ACTIVE_STATUSES
SAFE_TASK_ID_PATTERN = re.compile(r"^[A-Za-z0-9._-]+$")
SECRET_KEY_PARTS = (
    "authorization",
    "api_key",
    "apikey",
    "access_token",
    "refresh_token",
    "credential",
    "password",
    "secret",
    "cookie",
)

APPROVED_ASSET_ID = "aegis-tortoise-turret"
APPROVED_INPUT_WIDTH = 1254
APPROVED_INPUT_HEIGHT = 1254
TRIAL_SPECS: dict[str, dict[str, Any]] = {
    "oblique": {
        "candidate": "B1",
        "role": "front-three-quarter",
        "path": Path(
            "art/class-models/provider-inputs/meshy/"
            "aegis-tortoise-turret/clean-v1/03-front-three-quarter.png"
        ),
        "sha256": (
            "404b12a82bf21a0b22f492bac2190ec4b5562cf1ae18a8baf23d4b204a029bb4"
        ),
    },
    "top": {
        "candidate": "B2",
        "role": "top",
        "path": Path(
            "art/class-models/provider-inputs/meshy/"
            "aegis-tortoise-turret/clean-v1/01-top.png"
        ),
        "sha256": (
            "c9ca639173a8744ff1c0475da451eca29ef1ea8828fd1399289fb0facd577ca2"
        ),
    },
}

# Compatibility aliases for the immutable B1 request record and callers that
# imported the original one-trial runner constants.
APPROVED_INPUT_RELATIVE = TRIAL_SPECS["oblique"]["path"]
APPROVED_INPUT_SHA256 = TRIAL_SPECS["oblique"]["sha256"]
LEDGER_RELATIVE = Path(
    "art/class-models/topology-experiments/experiment-ledger.json"
)
RUNS_RELATIVE = Path("art/class-models/topology-experiments/runs/meshy")
RAW_ROOT_RELATIVE = Path("var/class-models/topology-experiments/meshy")

TEXTURE_PROMPT = (
    "Restrained stylized PBR whole radial emplacement: matte graphite-navy "
    "hard-surface armor, weathered bronze cardinal plates, crisp panel seams, "
    "dark gunmetal locks and restrained wear. Preserve exactly four equal "
    "bounded cyan cardinal inlays; keep the cyan reactor as separate class "
    "energy. No extra cyan, glow spill, text, logos, labels, decals, baked "
    "shadows, floor lighting, added markings, or glossy toy plastic."
)

# Meshy T2 does not support image_enhancement or remove_lighting. They are
# deliberately absent, not set to false.
FIXED_SETTINGS: dict[str, Any] = {
    "model_type": "smart-topology",
    "ai_model": "meshy-t2",
    "target_polycount": 15000,
    "should_texture": True,
    "enable_pbr": True,
    "texture_resolution": "4k",
    "texture_prompt": TEXTURE_PROMPT,
    "target_formats": ["glb"],
}

OFFICIAL_DOCS = {
    "imageTo3dApi": "https://docs.meshy.ai/en/api/image-to-3d",
    "balanceApi": "https://docs.meshy.ai/en/api/balance",
    "pricing": "https://docs.meshy.ai/en/api/pricing",
    "checkedOn": "2026-07-30",
}


class RunnerError(RuntimeError):
    """A safe, user-facing runner failure."""


@dataclass(frozen=True)
class ApprovedInput:
    trial: str
    candidate: str
    role: str
    repo_root: Path
    path: Path
    recorded_path: str
    byte_count: int
    sha256: str
    width: int
    height: int


@dataclass(frozen=True)
class ArtifactSpec:
    url: str
    destination: Path
    expected_kind: str
    storage: str


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Validate the pinned Turret Smart Topology experiment offline, "
            "or explicitly submit/resume its paid Meshy task."
        )
    )
    parser.add_argument(
        "--trial",
        choices=tuple(TRIAL_SPECS),
        help="required for a new dry run/submission: oblique (B1) or top (B2)",
    )
    parser.add_argument(
        "--input",
        type=Path,
        help=(
            "optional explicit input; it must resolve to the pinned "
            "owner-approved PNG for --trial"
        ),
    )
    parser.add_argument(
        "--resume-run",
        type=Path,
        help="poll/download an existing recorded topology-experiment run",
    )
    parser.add_argument(
        "--submit",
        action="store_true",
        help="make the paid API request; omitted means offline dry run",
    )
    parser.add_argument(
        "--max-credits",
        type=int,
        help="required with --submit and must be exactly 15",
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

    if args.resume_run is not None:
        if args.trial is not None:
            parser.error("--trial cannot be combined with --resume-run")
        if args.input is not None:
            parser.error("--input cannot be combined with --resume-run")
        if args.submit:
            parser.error("--submit cannot be combined with --resume-run")
        if args.max_credits is not None:
            parser.error("--max-credits cannot be combined with --resume-run")
    elif args.trial is None:
        parser.error("--trial oblique|top is required for a new run")
    elif args.submit:
        if args.max_credits is None:
            parser.error("--submit requires --max-credits 15")
    elif args.max_credits is not None:
        parser.error("--max-credits is only valid with --submit")

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


def png_dimensions(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != PNG_SIGNATURE:
        raise RunnerError(f"approved input is not a PNG: {path}")
    if header[12:16] != b"IHDR":
        raise RunnerError(f"approved input has no leading PNG IHDR: {path}")
    width, height = struct.unpack(">II", header[16:24])
    if width <= 0 or height <= 0:
        raise RunnerError(f"approved input has invalid PNG dimensions: {path}")
    return width, height


def validate_approved_input(
    trial: str,
    supplied_path: Path | None,
    repo_root: Path,
) -> ApprovedInput:
    spec = TRIAL_SPECS.get(trial)
    if spec is None:
        raise RunnerError(f"unknown topology trial: {trial!r}")
    repo_root = repo_root.resolve()
    expected_relative = spec["path"]
    expected_hash = spec["sha256"]
    expected = (repo_root / expected_relative).resolve()
    candidate = (
        expected
        if supplied_path is None
        else (
            supplied_path.resolve()
            if supplied_path.is_absolute()
            else (repo_root / supplied_path).resolve()
        )
    )
    if candidate != expected:
        raise RunnerError(
            f"only the owner-approved Turret {trial} input is allowed: "
            f"{expected_relative.as_posix()}"
        )
    if not candidate.is_file():
        raise RunnerError(f"approved input does not exist: {candidate}")
    if candidate.suffix != ".png":
        raise RunnerError("approved input path must end in lowercase .png")
    width, height = png_dimensions(candidate)
    if (width, height) != (APPROVED_INPUT_WIDTH, APPROVED_INPUT_HEIGHT):
        raise RunnerError(
            "approved input dimensions drifted: expected "
            f"{APPROVED_INPUT_WIDTH}x{APPROVED_INPUT_HEIGHT}, "
            f"found {width}x{height}"
        )
    digest = sha256_file(candidate)
    if digest != expected_hash:
        raise RunnerError(
            f"approved {trial} input hash drifted; no provider task may be "
            "submitted"
        )
    return ApprovedInput(
        trial=trial,
        candidate=spec["candidate"],
        role=spec["role"],
        repo_root=repo_root,
        path=candidate,
        recorded_path=expected_relative.as_posix(),
        byte_count=candidate.stat().st_size,
        sha256=digest,
        width=width,
        height=height,
    )


def load_json_object(path: Path, label: str) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise RunnerError(f"cannot read {label}: {path}") from error
    if not isinstance(value, dict):
        raise RunnerError(f"{label} must be a JSON object: {path}")
    return value


def require_number(value: object, label: str) -> int | float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise RunnerError(f"{label} must be numeric")
    if value < 0:
        raise RunnerError(f"{label} must not be negative")
    return value


def validate_numeric_ledger_fields(call: dict[str, Any], index: int) -> None:
    for field in (
        "balanceBefore",
        "balanceAfter",
        "expectedCredits",
        "maxCredits",
        "consumedCredits",
    ):
        if field in call:
            require_number(call[field], f"calls[{index}].{field}")


def trial_from_input_record(value: object) -> str:
    if not isinstance(value, dict):
        raise RunnerError("trial input record must be an object")
    path = value.get("file", value.get("path"))
    digest = value.get("sha256")
    matches = [
        name
        for name, spec in TRIAL_SPECS.items()
        if path == spec["path"].as_posix() and digest == spec["sha256"]
    ]
    if len(matches) != 1:
        raise RunnerError("record does not pin one recognized approved trial input")
    return matches[0]


def call_trial(call: dict[str, Any]) -> str:
    inferred = trial_from_input_record(call.get("input"))
    recorded = call.get("trial")
    if recorded is not None and recorded != inferred:
        raise RunnerError("recorded call trial conflicts with its pinned input")
    expected_candidate = TRIAL_SPECS[inferred]["candidate"]
    accepted_candidates = (
        {expected_candidate, "B"} if inferred == "oblique" else {expected_candidate}
    )
    if call.get("candidate") not in accepted_candidates:
        raise RunnerError("recorded call candidate conflicts with its trial")
    return inferred


def load_experiment_ledger(
    repo_root: Path,
) -> tuple[Path, dict[str, Any]]:
    ledger_path = (repo_root / LEDGER_RELATIVE).resolve()
    ledger = load_json_object(ledger_path, "topology experiment ledger")
    if ledger.get("schemaVersion") != 1:
        raise RunnerError("experiment ledger schemaVersion must be 1")
    if ledger.get("experimentId") != "meshy-smart-topology-turret-v1":
        raise RunnerError("experiment ledger ID does not match this runner")
    if ledger.get("provider") != "Meshy":
        raise RunnerError("experiment ledger provider must be Meshy")

    approved = ledger.get("approvedInput")
    if not isinstance(approved, dict):
        raise RunnerError("experiment ledger approvedInput must be an object")
    if approved.get("assetId") != APPROVED_ASSET_ID:
        raise RunnerError("experiment ledger approved asset drifted")
    if approved.get("path") != APPROVED_INPUT_RELATIVE.as_posix():
        raise RunnerError("experiment ledger approved input path drifted")
    if approved.get("sha256") != APPROVED_INPUT_SHA256:
        raise RunnerError("experiment ledger approved input hash drifted")
    if approved.get("width") != APPROVED_INPUT_WIDTH:
        raise RunnerError("experiment ledger approved input width drifted")
    if approved.get("height") != APPROVED_INPUT_HEIGHT:
        raise RunnerError("experiment ledger approved input height drifted")

    trials = ledger.get("trials")
    if not isinstance(trials, dict) or set(trials) != set(TRIAL_SPECS):
        raise RunnerError("experiment ledger must define exactly oblique and top trials")
    for name, spec in TRIAL_SPECS.items():
        trial = trials.get(name)
        if not isinstance(trial, dict):
            raise RunnerError(f"experiment ledger trial {name} must be an object")
        expected = {
            "candidate": spec["candidate"],
            "assetId": APPROVED_ASSET_ID,
            "role": spec["role"],
            "path": spec["path"].as_posix(),
            "sha256": spec["sha256"],
            "width": APPROVED_INPUT_WIDTH,
            "height": APPROVED_INPUT_HEIGHT,
            "mediaType": "image/png",
            "ownerApproved": True,
        }
        for key, expected_value in expected.items():
            if trial.get(key) != expected_value:
                raise RunnerError(
                    f"experiment ledger trial {name}.{key} drifted"
                )
        if not isinstance(trial.get("status"), str) or not trial["status"]:
            raise RunnerError(f"experiment ledger trial {name}.status is missing")

    request = ledger.get("request")
    if not isinstance(request, dict):
        raise RunnerError("experiment ledger request must be an object")
    if request != FIXED_SETTINGS:
        raise RunnerError("experiment ledger request drifted from the fixed T2 request")
    if "image_enhancement" in request or "remove_lighting" in request:
        raise RunnerError("unsupported T2 request fields are present in the ledger")

    budget = ledger.get("budget")
    if not isinstance(budget, dict):
        raise RunnerError("experiment ledger budget must be an object")
    expected_budget = {
        "maxCalls": EXPERIMENT_MAX_CALLS,
        "maxCredits": EXPERIMENT_MAX_CREDITS,
    }
    for key, expected in expected_budget.items():
        if budget.get(key) != expected:
            raise RunnerError(f"experiment ledger budget.{key} must be {expected}")
    for key in (
        "callsSubmitted",
        "creditsCommitted",
        "creditsConsumed",
    ):
        require_number(budget.get(key), f"budget.{key}")

    first_gate = ledger.get("firstGate")
    if not isinstance(first_gate, dict):
        raise RunnerError("experiment ledger firstGate must be an object")
    if first_gate.get("assetId") != APPROVED_ASSET_ID:
        raise RunnerError("first gate must target the approved Turret")
    if first_gate.get("candidate") != "B1":
        raise RunnerError("first gate must target candidate B1")
    if first_gate.get("maxCalls") != 1:
        raise RunnerError("first gate must allow exactly one call")
    if first_gate.get("maxCredits") != EXPECTED_CREDITS:
        raise RunnerError("first gate must pin exactly 15 credits")
    verdict = first_gate.get("reviewVerdict")
    if verdict is not None and (
        not isinstance(verdict, str) or not verdict.strip()
    ):
        raise RunnerError("firstGate.reviewVerdict must be null or non-empty text")

    calls = ledger.get("calls")
    if not isinstance(calls, list):
        raise RunnerError("experiment ledger calls must be an array")
    for index, call in enumerate(calls):
        if not isinstance(call, dict):
            raise RunnerError(f"calls[{index}] must be an object")
        validate_numeric_ledger_fields(call, index)
        call_trial(call)
    if budget["callsSubmitted"] != len(calls):
        raise RunnerError("budget.callsSubmitted must equal the recorded call count")
    committed = sum(call.get("maxCredits", 0) for call in calls)
    consumed = sum(call.get("consumedCredits", 0) for call in calls)
    if budget["creditsCommitted"] != committed:
        raise RunnerError("budget.creditsCommitted does not match calls")
    if budget["creditsConsumed"] != consumed:
        raise RunnerError("budget.creditsConsumed does not match calls")
    return ledger_path, ledger


def enforce_credit_guard(max_credits: object) -> None:
    if isinstance(max_credits, bool) or max_credits != EXPECTED_CREDITS:
        raise RunnerError(
            "--max-credits must be exactly 15; the runner does not permit "
            "a higher or lower per-task ceiling"
        )


def enforce_experiment_gate(
    ledger: dict[str, Any],
    trial: str,
    max_credits: object,
) -> None:
    enforce_credit_guard(max_credits)
    if trial not in TRIAL_SPECS:
        raise RunnerError(f"unknown topology trial: {trial!r}")
    calls = ledger["calls"]
    budget = ledger["budget"]
    first_gate = ledger["firstGate"]
    matching_calls = [call for call in calls if call_trial(call) == trial]
    if matching_calls:
        raise RunnerError(
            f"trial {trial} already has a recorded paid call; duplicates are blocked"
        )
    if len(calls) >= EXPERIMENT_MAX_CALLS:
        raise RunnerError("the three-call topology experiment cap is exhausted")
    if budget["creditsCommitted"] + EXPECTED_CREDITS > EXPERIMENT_MAX_CREDITS:
        raise RunnerError("the 45-credit topology experiment cap would be exceeded")
    if trial == "oblique":
        if calls:
            raise RunnerError("B1 oblique must be the first paid view trial")
        return
    oblique_calls = [call for call in calls if call_trial(call) == "oblique"]
    if (
        len(oblique_calls) != 1
        or oblique_calls[0].get("status") != "SUCCEEDED"
    ):
        raise RunnerError("B2 top requires one successful recorded B1 oblique call")
    if first_gate.get("expansionAuthorized") is not True:
        raise RunnerError(
            "B2 top remains gated; review B1 and set "
            "firstGate.expansionAuthorized true"
        )
    verdict = first_gate.get("reviewVerdict")
    if not isinstance(verdict, str) or not verdict.strip():
        raise RunnerError(
            "B2 top requires a non-empty firstGate.reviewVerdict for B1"
        )


def print_plan(
    approved_input: ApprovedInput,
    ledger: dict[str, Any],
) -> None:
    print("OFFLINE DRY RUN - no credential read and no network request")
    print(f"Object: {APPROVED_ASSET_ID}")
    print(f"Trial: {approved_input.trial} ({approved_input.candidate})")
    print(f"Input: {approved_input.recorded_path}")
    print(
        "Input validation: "
        f"{approved_input.width}x{approved_input.height} PNG, "
        f"{approved_input.sha256}"
    )
    print("Pinned Meshy request:")
    for key, value in FIXED_SETTINGS.items():
        display = (
            f"<sha256:{json_sha256(value)}>"
            if key == "texture_prompt"
            else json.dumps(value)
        )
        print(f"  {key}: {display}")
    print("  image_url: <in-memory approved PNG data URI; never recorded>")
    print("Unsupported T2 fields omitted: image_enhancement, remove_lighting")
    print(
        "Experiment budget: "
        f"{ledger['budget']['callsSubmitted']}/{EXPERIMENT_MAX_CALLS} calls, "
        f"{ledger['budget']['creditsCommitted']}/{EXPERIMENT_MAX_CREDITS} "
        "credits committed"
    )
    try:
        enforce_experiment_gate(
            ledger,
            approved_input.trial,
            EXPECTED_CREDITS,
        )
    except RunnerError as error:
        print(f"Submission gate: BLOCKED ({error})")
    else:
        print(
            "Submission gate: READY "
            "(still requires --submit --max-credits 15)"
        )


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
            "User-Agent": "nilbots-meshy-topology-experiment/1",
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
        return require_number(response.get("balance"), "Meshy balance")

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


def build_request_payload(approved_input: ApprovedInput) -> dict[str, Any]:
    payload = dict(FIXED_SETTINGS)
    encoded = base64.b64encode(approved_input.path.read_bytes()).decode("ascii")
    payload["image_url"] = f"data:image/png;base64,{encoded}"
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


def input_record(approved_input: ApprovedInput) -> dict[str, Any]:
    return {
        "role": approved_input.role,
        "file": approved_input.recorded_path,
        "bytes": approved_input.byte_count,
        "sha256": approved_input.sha256,
        "width": approved_input.width,
        "height": approved_input.height,
        "mediaType": "image/png",
    }


def initial_request_record(
    approved_input: ApprovedInput,
    *,
    task_id: str,
    submitted_at: str,
    balance_before: int | float,
    raw_directory: Path,
    ledger_sha256: str,
) -> dict[str, Any]:
    return {
        "schemaVersion": 1,
        "runnerId": "nilbots-meshy-smart-topology-v1",
        "provider": "Meshy",
        "endpoint": "POST /openapi/v1/image-to-3d",
        "taskId": task_id,
        "assetId": APPROVED_ASSET_ID,
        "trial": approved_input.trial,
        "candidate": approved_input.candidate,
        "purpose": (
            f"Run controlled {approved_input.candidate} input-view trial "
            f"{approved_input.trial} to test whether Meshy T2 Smart Topology "
            "preserves the approved Turret identity and produces useful native "
            "parts, segmentation, and topology without changing request "
            "settings or texture prompt."
        ),
        "ownerApprovedInput": True,
        "submittedAt": submitted_at,
        "balanceBefore": balance_before,
        "expectedCredits": EXPECTED_CREDITS,
        "maxCredits": EXPECTED_CREDITS,
        "inputs": [input_record(approved_input)],
        "settings": dict(FIXED_SETTINGS),
        "unsupportedT2FieldsOmitted": [
            "image_enhancement",
            "remove_lighting",
        ],
        "sourceExperimentLedger": {
            "file": LEDGER_RELATIVE.as_posix(),
            "sha256BeforeSubmission": ledger_sha256,
        },
        "rawArtifactDirectory": recorded_repo_path(
            raw_directory,
            approved_input.repo_root,
        ),
        "credentialStore": {
            "platform": "macOS Keychain",
            "service": KEYCHAIN_SERVICE,
            "account": "current-login-account",
            "valueRecorded": False,
        },
        "officialDocs": OFFICIAL_DOCS,
        "assetRetentionNote": (
            "The raw GLB is ignored under var and is only a provider base-mesh "
            "experiment. Preserve it in durable artifact storage before "
            "deleting the worktree."
        ),
    }


def append_ledger_call(
    ledger: dict[str, Any],
    *,
    approved_input: ApprovedInput,
    task_id: str,
    run_directory: Path,
    raw_directory: Path,
    repo_root: Path,
    submitted_at: str,
    balance_before: int | float,
) -> None:
    calls = ledger["calls"]
    calls.append(
        {
            "callIndex": len(calls) + 1,
            "candidate": approved_input.candidate,
            "trial": approved_input.trial,
            "assetId": APPROVED_ASSET_ID,
            "taskId": task_id,
            "status": "PENDING",
            "submittedAt": submitted_at,
            "balanceBefore": balance_before,
            "expectedCredits": EXPECTED_CREDITS,
            "maxCredits": EXPECTED_CREDITS,
            "consumedCredits": 0,
            "input": {
                "path": approved_input.recorded_path,
                "sha256": approved_input.sha256,
            },
            "runRecord": recorded_repo_path(run_directory, repo_root),
            "rawArtifactDirectory": recorded_repo_path(raw_directory, repo_root),
        }
    )
    ledger["budget"]["callsSubmitted"] = len(calls)
    ledger["budget"]["creditsCommitted"] = sum(
        call["maxCredits"] for call in calls
    )
    ledger["budget"]["creditsConsumed"] = sum(
        call["consumedCredits"] for call in calls
    )
    ledger["trials"][approved_input.trial]["status"] = "running"
    if approved_input.trial == "oblique":
        ledger["firstGate"]["status"] = "running"
        ledger["status"] = "gate-one-running"
    else:
        ledger["status"] = "view-trial-top-running"
    ledger["updatedAt"] = utc_now()


def update_ledger_call(
    ledger: dict[str, Any],
    *,
    task_id: str,
    status: str,
    balance_after: int | float,
    consumed: int | float,
) -> None:
    matches = [call for call in ledger["calls"] if call.get("taskId") == task_id]
    if len(matches) != 1:
        raise RunnerError("experiment ledger has no unique record for this task")
    call = matches[0]
    call["status"] = status
    call["finishedAt"] = utc_now()
    call["balanceAfter"] = balance_after
    call["consumedCredits"] = consumed
    trial = call_trial(call)
    ledger["budget"]["creditsConsumed"] = sum(
        existing["consumedCredits"] for existing in ledger["calls"]
    )
    if trial == "oblique":
        verdict = ledger["firstGate"].get("reviewVerdict")
        if status == "SUCCEEDED" and isinstance(verdict, str) and verdict.strip():
            ledger["trials"][trial]["status"] = "visually-liked-retained"
            ledger["firstGate"]["status"] = "reviewed-b2-gated"
            if not any(
                call_trial(existing) == "top"
                for existing in ledger["calls"]
            ):
                ledger["status"] = "b1-retained-b2-gated"
        else:
            ledger["trials"][trial]["status"] = (
                "awaiting-review"
                if status == "SUCCEEDED"
                else "provider-task-failed"
            )
            ledger["firstGate"]["status"] = (
                "awaiting-review"
                if status == "SUCCEEDED"
                else "provider-task-failed"
            )
            ledger["status"] = "gate-one-awaiting-review"
    else:
        ledger["trials"][trial]["status"] = (
            "awaiting-review" if status == "SUCCEEDED" else "provider-task-failed"
        )
        ledger["status"] = "view-trial-top-awaiting-review"
    ledger["updatedAt"] = utc_now()


def consumed_credits(task: dict[str, Any]) -> int | float | None:
    value = task.get("consumed_credits")
    if value is None:
        return None
    return require_number(value, "Meshy task consumed_credits")


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
                request_record["maxCreditViolation"] = True
                credit_violation = True
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
        raise RunnerError("Meshy returned an empty artifact URL")
    parsed = urlsplit(url)
    host = (parsed.hostname or "").lower()
    if (
        parsed.username is not None
        or parsed.password is not None
        or parsed.scheme != "https"
        or not (
        host == "meshy.ai" or host.endswith(".meshy.ai")
        )
    ):
        raise RunnerError("Meshy returned an unexpected artifact URL origin")
    return url


def safe_label(value: object) -> str:
    label = re.sub(r"[^a-z0-9]+", "-", str(value).lower()).strip("-")
    return label or "output"


def collect_artifact_specs(
    task: dict[str, Any],
    *,
    run_directory: Path,
    raw_directory: Path,
) -> list[ArtifactSpec]:
    specs: list[ArtifactSpec] = []
    model_urls = task.get("model_urls", {})
    if model_urls is not None and not isinstance(model_urls, dict):
        raise RunnerError("Meshy model_urls was not an object")
    glb_url = (model_urls or {}).get("glb") or task.get("model_url")
    if not glb_url:
        raise RunnerError("succeeded Meshy task did not expose a GLB")
    specs.append(
        ArtifactSpec(
            url=validate_asset_url(glb_url),
            destination=raw_directory / "raw-model.glb",
            expected_kind="glb",
            storage="ignored-raw",
        )
    )

    thumbnail = task.get("thumbnail_url")
    if thumbnail:
        specs.append(
            ArtifactSpec(
                url=validate_asset_url(thumbnail),
                destination=run_directory / "provider-preview.png",
                expected_kind="png",
                storage="tracked-preview",
            )
        )
    alpha_thumbnail = task.get("alpha_thumbnail_url")
    if alpha_thumbnail:
        specs.append(
            ArtifactSpec(
                url=validate_asset_url(alpha_thumbnail),
                destination=run_directory / "provider-preview-alpha.png",
                expected_kind="png",
                storage="tracked-preview",
            )
        )
    thumbnails = task.get("thumbnail_urls", {})
    if thumbnails is not None and not isinstance(thumbnails, dict):
        raise RunnerError("Meshy thumbnail_urls was not an object")
    for view, value in (thumbnails or {}).items():
        if value:
            specs.append(
                ArtifactSpec(
                    url=validate_asset_url(value),
                    destination=(
                        run_directory
                        / f"provider-preview-{safe_label(view)}.png"
                    ),
                    expected_kind="png",
                    storage="tracked-preview",
                )
            )

    texture_sets = task.get("texture_urls", [])
    if texture_sets is not None and not isinstance(texture_sets, list):
        raise RunnerError("Meshy texture_urls was not an array")
    for index, texture_set in enumerate(texture_sets or []):
        if not isinstance(texture_set, dict):
            raise RunnerError("Meshy texture_urls entry was not an object")
        for map_name, value in texture_set.items():
            if value:
                specs.append(
                    ArtifactSpec(
                        url=validate_asset_url(value),
                        destination=(
                            raw_directory
                            / f"texture-{index}-{safe_label(map_name)}.png"
                        ),
                        expected_kind="png",
                        storage="ignored-pbr-map",
                    )
                )

    deduplicated: list[ArtifactSpec] = []
    seen_urls: set[str] = set()
    seen_destinations: set[Path] = set()
    for spec in specs:
        unsigned = strip_signed_url(spec.url)
        if unsigned in seen_urls:
            continue
        if spec.destination in seen_destinations:
            raise RunnerError(
                f"duplicate provider artifact destination: {spec.destination.name}"
            )
        seen_urls.add(unsigned)
        seen_destinations.add(spec.destination)
        deduplicated.append(spec)
    return deduplicated


def validate_artifact(path: Path, expected_kind: str) -> None:
    with path.open("rb") as stream:
        header = stream.read(12)
    if expected_kind == "glb" and (len(header) < 12 or header[:4] != b"glTF"):
        raise RunnerError(f"downloaded file is not a GLB: {path}")
    if expected_kind == "png" and header[:8] != PNG_SIGNATURE:
        raise RunnerError(f"downloaded file is not a PNG: {path}")
    if path.stat().st_size <= 0:
        raise RunnerError(f"downloaded artifact is empty: {path}")


def download_artifact(
    spec: ArtifactSpec,
    *,
    opener: Callable[..., Any] = urlopen,
) -> dict[str, Any]:
    destination = spec.destination
    if destination.exists():
        validate_artifact(destination, spec.expected_kind)
        return {
            "bytes": destination.stat().st_size,
            "sha256": sha256_file(destination),
            "contentType": None,
            "reusedExisting": True,
            "storage": spec.storage,
        }
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_name(f".{destination.name}.download")
    if temporary.exists():
        temporary.unlink()
    digest = hashlib.sha256()
    byte_count = 0
    content_type: str | None = None
    try:
        request = Request(
            spec.url,
            headers={"User-Agent": "nilbots-meshy-topology-experiment/1"},
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
        validate_artifact(temporary, spec.expected_kind)
        os.replace(temporary, destination)
    except Exception:
        if temporary.exists():
            temporary.unlink()
        raise
    return {
        "bytes": byte_count,
        "sha256": digest.hexdigest(),
        "contentType": content_type,
        "reusedExisting": False,
        "storage": spec.storage,
    }


def write_run_markdown(
    run_directory: Path,
    request_record: dict[str, Any],
) -> None:
    inputs = request_record.get("inputs", [])
    input_file = (
        inputs[0].get("file", "unknown")
        if isinstance(inputs, list) and inputs and isinstance(inputs[0], dict)
        else "unknown"
    )
    trial = request_record.get("trial")
    if trial is None and isinstance(inputs, list) and len(inputs) == 1:
        trial = trial_from_input_record(inputs[0])
    candidate = request_record.get(
        "candidate",
        TRIAL_SPECS.get(str(trial), {}).get("candidate", "B"),
    )
    artifacts = request_record.get("artifacts", [])
    raw_glb = next(
        (
            artifact
            for artifact in artifacts
            if artifact.get("storage") == "ignored-raw"
        ),
        {},
    )
    lines = [
        f"# Meshy Smart Topology run - {APPROVED_ASSET_ID}",
        "",
        f"- Task: `{request_record['taskId']}`",
        f"- Trial: `{trial}` (`{candidate}`)",
        f"- Status: {str(request_record.get('status', 'unknown')).lower()}",
        (
            f"- Credits: {request_record.get('consumedCredits')} "
            f"(`{request_record.get('balanceBefore')}` before, "
            f"`{request_record.get('balanceAfter')}` after)"
        ),
        f"- Input: `{input_file}`",
        f"- Raw GLB: `{raw_glb.get('file', 'not downloaded')}`",
        f"- Raw GLB SHA-256: `{raw_glb.get('sha256', 'not available')}`",
        "",
        "The raw provider GLB and separately exposed PBR maps are ignored "
        "under `var/`; provider previews are retained here for review. "
        "Sanitized provenance records hashes for every preserved output. No "
        "credential value, Authorization header, input data URI, or signed URL "
        "query is stored.",
        "",
        "This result is an experimental base mesh, not an approved runtime "
        "asset. It still requires the Blender, runtime, review, and validation "
        "gates in `../../PIPELINES.md`.",
        "",
    ]
    atomic_write_text(run_directory / "RUN.md", "\n".join(lines))


def finish_run(
    *,
    client: MeshyClient,
    task: dict[str, Any],
    run_directory: Path,
    raw_directory: Path,
    request_record: dict[str, Any],
    ledger_path: Path,
    ledger: dict[str, Any],
    credit_violation: bool,
) -> int:
    status = task_status(task)
    atomic_write_json(
        run_directory / "task.sanitized.json",
        sanitize_for_record(task),
    )
    artifacts: list[dict[str, Any]] = []
    download_failures: list[dict[str, str]] = []
    if status == "SUCCEEDED":
        specs = collect_artifact_specs(
            task,
            run_directory=run_directory,
            raw_directory=raw_directory,
        )
        for spec in specs:
            try:
                artifact = download_artifact(spec)
                artifact["file"] = recorded_repo_path(
                    spec.destination,
                    repo_root_from_script(),
                )
                artifacts.append(artifact)
                print(
                    f"Downloaded {artifact['file']} "
                    f"({artifact['bytes']} bytes, {artifact['sha256']})"
                )
            except Exception as error:
                download_failures.append(
                    {
                        "file": recorded_repo_path(
                            spec.destination,
                            repo_root_from_script(),
                        ),
                        "error": type(error).__name__,
                    }
                )
                print(
                    f"Download failed for {spec.destination.name}: "
                    f"{type(error).__name__}",
                    file=sys.stderr,
                )
    balance_after = client.balance()
    reported = consumed_credits(task)
    observed = (
        reported
        if reported is not None
        else max(0, request_record["balanceBefore"] - balance_after)
    )
    require_number(balance_after, "balanceAfter")
    require_number(observed, "consumedCredits")
    if observed > EXPECTED_CREDITS:
        credit_violation = True
    request_record.update(
        {
            "finishedAt": utc_now(),
            "status": status,
            "balanceAfter": balance_after,
            "consumedCredits": observed,
            "consumedCreditsSource": (
                "task" if reported is not None else "balance-delta"
            ),
            "artifacts": artifacts,
        }
    )
    if download_failures:
        request_record["downloadFailures"] = download_failures
    if credit_violation:
        request_record["maxCreditViolation"] = True
    atomic_write_json(run_directory / "request-record.json", request_record)
    update_ledger_call(
        ledger,
        task_id=request_record["taskId"],
        status=status,
        balance_after=balance_after,
        consumed=observed,
    )
    atomic_write_json(ledger_path, ledger)
    write_run_markdown(run_directory, request_record)

    if status != "SUCCEEDED":
        raise RunnerError(f"Meshy task ended with status {status}")
    if download_failures:
        raise RunnerError(
            "one or more expiring provider outputs failed to download; resume "
            "the recorded task immediately"
        )
    if credit_violation:
        raise RunnerError(
            "Meshy reported consumption above the 15-credit ceiling; the raw "
            "result was preserved but no further experiment call is allowed"
        )
    return 0


def submit_experiment(
    approved_input: ApprovedInput,
    *,
    ledger_path: Path,
    ledger: dict[str, Any],
    max_credits: int,
    interval_seconds: float,
    timeout_seconds: float,
) -> int:
    enforce_experiment_gate(ledger, approved_input.trial, max_credits)
    repo_root = approved_input.repo_root
    committed_root = repo_root / RUNS_RELATIVE
    raw_root = repo_root / RAW_ROOT_RELATIVE
    preflight_output_directory(committed_root)
    preflight_output_directory(raw_root)

    # This is the first point at which a credential is read.
    token = read_keychain_token()
    client = MeshyClient(token)
    balance_before = client.balance()
    if balance_before < EXPECTED_CREDITS:
        raise RunnerError(
            f"Meshy balance {balance_before} is below the 15-credit task cost"
        )

    submitted_at = utc_now()
    payload = build_request_payload(approved_input)
    task_id = client.create_task(payload)
    del payload

    task_directory = safe_task_directory_name(task_id)
    run_directory = committed_root / task_directory
    raw_directory = raw_root / task_directory
    if run_directory.exists() or raw_directory.exists():
        raise RunnerError(
            "provider task directory already exists; refusing to overwrite "
            f"task {task_id}"
        )
    run_directory.mkdir(parents=True)
    raw_directory.mkdir(parents=True)
    request_record = initial_request_record(
        approved_input,
        task_id=task_id,
        submitted_at=submitted_at,
        balance_before=balance_before,
        raw_directory=raw_directory,
        ledger_sha256=sha256_file(ledger_path),
    )
    record_path = run_directory / "request-record.json"
    atomic_write_json(record_path, request_record)
    append_ledger_call(
        ledger,
        approved_input=approved_input,
        task_id=task_id,
        run_directory=run_directory,
        raw_directory=raw_directory,
        repo_root=repo_root,
        submitted_at=submitted_at,
        balance_before=balance_before,
    )
    atomic_write_json(ledger_path, ledger)
    print(f"Created Meshy task {task_id}; sanitized provenance recorded")

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
        raw_directory=raw_directory,
        request_record=request_record,
        ledger_path=ledger_path,
        ledger=ledger,
        credit_violation=credit_violation,
    )


def resolve_recorded_raw_directory(
    value: object,
    repo_root: Path,
) -> Path:
    if not isinstance(value, str) or not value:
        raise RunnerError("request record has no raw artifact directory")
    supplied = Path(value)
    if supplied.is_absolute():
        raise RunnerError("raw artifact directory must be repository-relative")
    resolved = (repo_root / supplied).resolve()
    expected_root = (repo_root / RAW_ROOT_RELATIVE).resolve()
    if not is_relative_to(resolved, expected_root):
        raise RunnerError("raw artifact directory escapes the ignored var root")
    return resolved


def validate_resume_record(
    record: dict[str, Any],
    repo_root: Path,
) -> tuple[str, Path, str]:
    if record.get("schemaVersion") != 1:
        raise RunnerError("resume supports only request-record schema 1")
    if record.get("runnerId") != "nilbots-meshy-smart-topology-v1":
        raise RunnerError("request record does not belong to this runner")
    if record.get("provider") != "Meshy":
        raise RunnerError("request record provider is not Meshy")
    if record.get("endpoint") != "POST /openapi/v1/image-to-3d":
        raise RunnerError("request record endpoint does not match Image-to-3D")
    if record.get("maxCredits") != EXPECTED_CREDITS:
        raise RunnerError("request record does not pin the 15-credit ceiling")
    inputs = record.get("inputs")
    if not isinstance(inputs, list) or len(inputs) != 1:
        raise RunnerError("request record does not pin one approved input")
    trial = trial_from_input_record(inputs[0])
    recorded_trial = record.get("trial")
    if recorded_trial is not None and recorded_trial != trial:
        raise RunnerError("request record trial conflicts with its pinned input")
    recorded_candidate = record.get("candidate")
    expected_candidate = TRIAL_SPECS[trial]["candidate"]
    if recorded_candidate is not None and recorded_candidate != expected_candidate:
        raise RunnerError("request record candidate conflicts with its trial")
    if record.get("settings") != FIXED_SETTINGS:
        raise RunnerError("request record does not pin the fixed T2 settings")
    task_id = record.get("taskId")
    if not isinstance(task_id, str) or not task_id:
        raise RunnerError("request record has no task ID")
    raw_directory = resolve_recorded_raw_directory(
        record.get("rawArtifactDirectory"),
        repo_root,
    )
    return task_id, raw_directory, trial


def resume_run(
    run_directory: Path,
    *,
    repo_root: Path,
    interval_seconds: float,
    timeout_seconds: float,
) -> int:
    repo_root = repo_root.resolve()
    run_directory = run_directory.resolve()
    expected_root = (repo_root / RUNS_RELATIVE).resolve()
    if not is_relative_to(run_directory, expected_root):
        raise RunnerError(
            "resume directory must be under "
            f"{RUNS_RELATIVE.as_posix()}"
    )
    record_path = run_directory / "request-record.json"
    record = load_json_object(record_path, "Smart Topology request record")
    task_id, raw_directory, trial = validate_resume_record(record, repo_root)
    raw_directory.mkdir(parents=True, exist_ok=True)
    ledger_path, ledger = load_experiment_ledger(repo_root)
    matching_calls = [
        call for call in ledger["calls"] if call.get("taskId") == task_id
    ]
    if len(matching_calls) != 1:
        raise RunnerError("experiment ledger does not uniquely record this task")
    if call_trial(matching_calls[0]) != trial:
        raise RunnerError("experiment ledger trial does not match the run record")

    token = read_keychain_token()
    client = MeshyClient(token)
    task, credit_violation = poll_task(
        client,
        task_id,
        interval_seconds=interval_seconds,
        timeout_seconds=timeout_seconds,
        record_path=record_path,
        request_record=record,
    )
    return finish_run(
        client=client,
        task=task,
        run_directory=run_directory,
        raw_directory=raw_directory,
        request_record=record,
        ledger_path=ledger_path,
        ledger=ledger,
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
    approved_input = validate_approved_input(args.trial, args.input, repo_root)
    ledger_path, ledger = load_experiment_ledger(repo_root)
    if not args.submit:
        print_plan(approved_input, ledger)
        return 0
    return submit_experiment(
        approved_input,
        ledger_path=ledger_path,
        ledger=ledger,
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
