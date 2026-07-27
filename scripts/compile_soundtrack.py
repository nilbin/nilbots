#!/usr/bin/env python3
"""Compile aligned PCM WAV stems into a content-aware adaptive soundtrack.

The compiler deliberately uses only the Python standard library. ffmpeg is the
preferred AAC encoder; macOS' afconvert is a supported fallback. It never calls
ZipFile.extract/extractall: every archive member is validated before mapped
stems are copied to compiler-owned filenames in a temporary directory.
"""

from __future__ import annotations

import argparse
import array
import hashlib
import heapq
import json
import math
import os
import re
import shutil
import stat
import struct
import subprocess
import sys
import tempfile
import unicodedata
import uuid
import wave
import zipfile
from fractions import Fraction
from pathlib import Path, PurePosixPath
from typing import Any, Dict, Iterable, List, Optional, Sequence, Tuple


SCHEMA_VERSION = 1
PIPELINE_VERSION = 1
CLASSIFICATIONS = (
    "sparse",
    "tension",
    "pursuit",
    "combat",
    "climax",
    "resolve",
)
GAMEPLAY_CLASSIFICATIONS = CLASSIFICATIONS[:-1]
SECTION_ROLES = ("hold", "bridge", "stinger", "resolve")
DEFAULT_ADAPTIVE_LATENCY_BUDGET_BARS = {
    "gameplay": 2.0,
    "resolve": 1.0,
}
SLUG_RE = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
MAX_ARCHIVE_FILES = 64
MAX_MEMBER_BYTES = 1024 * 1024 * 1024
MAX_ARCHIVE_BYTES = 4 * 1024 * 1024 * 1024
MAX_COMPRESSION_RATIO = 1000.0
READ_CHUNK_BYTES = 1024 * 1024


class PipelineError(Exception):
    """An expected, user-actionable compiler failure."""


def add_warning(warnings: List[str], message: str) -> None:
    if message not in warnings:
        warnings.append(message)


def reject_duplicate_json_keys(pairs: Sequence[Tuple[str, Any]]) -> Dict[str, Any]:
    result: Dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise PipelineError("duplicate JSON key: {!r}".format(key))
        result[key] = value
    return result


def load_json(path: Path) -> Dict[str, Any]:
    try:
        with path.open("r", encoding="utf-8") as handle:
            value = json.load(handle, object_pairs_hook=reject_duplicate_json_keys)
    except OSError as error:
        raise PipelineError("cannot read {}: {}".format(path, error)) from error
    except json.JSONDecodeError as error:
        raise PipelineError(
            "{}:{}:{}: invalid JSON: {}".format(
                path, error.lineno, error.colno, error.msg
            )
        ) from error
    if not isinstance(value, dict):
        raise PipelineError("{}: top-level JSON value must be an object".format(path))
    return value


def canonical_json_bytes(value: Any) -> bytes:
    return (
        json.dumps(
            value,
            ensure_ascii=False,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        )
        + "\n"
    ).encode("utf-8")


def pretty_json_bytes(value: Any) -> bytes:
    return (
        json.dumps(
            value,
            ensure_ascii=False,
            allow_nan=False,
            sort_keys=False,
            indent=2,
        )
        + "\n"
    ).encode("utf-8")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(READ_CHUNK_BYTES)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def write_bytes_atomic(path: Path, contents: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(
        ".{}.{}.tmp".format(path.name, uuid.uuid4().hex)
    )
    try:
        with temporary.open("xb") as handle:
            handle.write(contents)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(str(temporary), str(path))
    finally:
        if temporary.exists():
            temporary.unlink()


def ensure_only_keys(value: Dict[str, Any], allowed: Iterable[str], context: str) -> None:
    unknown = sorted(set(value) - set(allowed))
    if unknown:
        raise PipelineError(
            "{} contains unknown field{}: {}".format(
                context, "" if len(unknown) == 1 else "s", ", ".join(unknown)
            )
        )


def require_string(value: Any, context: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise PipelineError("{} must be a non-empty string".format(context))
    return value


def require_slug(value: Any, context: str) -> str:
    result = require_string(value, context)
    if not SLUG_RE.fullmatch(result):
        raise PipelineError(
            "{} must use lowercase letters, digits, and single hyphens".format(context)
        )
    return result


def require_number(
    value: Any,
    context: str,
    minimum: Optional[float] = None,
    maximum: Optional[float] = None,
) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise PipelineError("{} must be a number".format(context))
    result = float(value)
    if not math.isfinite(result):
        raise PipelineError("{} must be finite".format(context))
    if minimum is not None and result < minimum:
        raise PipelineError("{} must be at least {}".format(context, minimum))
    if maximum is not None and result > maximum:
        raise PipelineError("{} must be at most {}".format(context, maximum))
    return result


def require_integer(
    value: Any,
    context: str,
    minimum: Optional[int] = None,
    maximum: Optional[int] = None,
) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        raise PipelineError("{} must be an integer".format(context))
    if minimum is not None and value < minimum:
        raise PipelineError("{} must be at least {}".format(context, minimum))
    if maximum is not None and value > maximum:
        raise PipelineError("{} must be at most {}".format(context, maximum))
    return value


def round_float(value: float, digits: int = 9) -> float:
    rounded = round(float(value), digits)
    return 0.0 if rounded == -0.0 else rounded


def dbfs(amplitude: float, full_scale: float) -> float:
    if amplitude <= 0.0:
        return -120.0
    return max(-120.0, 20.0 * math.log10(amplitude / full_scale))


def amplitude_from_db(db: float) -> float:
    if db <= -120.0:
        return 0.0
    return 10.0 ** (db / 20.0)


def percentile(values: Sequence[float], proportion: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    position = (len(ordered) - 1) * proportion
    lower = int(math.floor(position))
    upper = int(math.ceil(position))
    if lower == upper:
        return ordered[lower]
    fraction = position - lower
    return ordered[lower] * (1.0 - fraction) + ordered[upper] * fraction


def weighted_metric(windows: Sequence[Dict[str, Any]]) -> Dict[str, float]:
    sample_count = sum(int(item["sampleCount"]) for item in windows)
    frame_count = sum(int(item["frameCount"]) for item in windows)
    if sample_count <= 0:
        return {
            "rmsDbfs": -120.0,
            "peakDbfs": -120.0,
            "activity": 0.0,
            "transient": 0.0,
            "frameCount": 0.0,
        }
    sum_squares = sum(float(item["sumSquares"]) for item in windows)
    full_scale = float(windows[0]["fullScale"])
    rms = math.sqrt(sum_squares / sample_count)
    peak = max(float(item["peak"]) for item in windows)
    active_frames = sum(
        int(item["frameCount"]) for item in windows if bool(item["active"])
    )
    changes: List[float] = []
    previous: Optional[float] = None
    for item in windows:
        current = float(item["rmsDbfs"])
        if previous is not None:
            changes.append(max(0.0, current - previous))
        previous = current
    transient = (
        sum(min(change / 24.0, 1.0) for change in changes) / len(changes)
        if changes
        else 0.0
    )
    return {
        "rmsDbfs": round_float(dbfs(rms, full_scale), 3),
        "peakDbfs": round_float(dbfs(peak, full_scale), 3),
        "activity": round_float(active_frames / max(1, frame_count), 4),
        "transient": round_float(transient, 4),
        "frameCount": float(frame_count),
    }


def normalize_config(raw: Dict[str, Any], config_path: Path) -> Dict[str, Any]:
    ensure_only_keys(
        raw,
        {
            "schemaVersion",
            "id",
            "title",
            "default",
            "provenance",
            "sourceArchive",
            "bpm",
            "beatsPerBar",
            "segmentBars",
            "gridOriginFrame",
            "barFrames",
            "sourceEndFrame",
            "masterGainDb",
            "encoding",
            "analysis",
            "adaptiveLatencyBudgetBars",
            "adaptiveSeam",
            "stems",
            "retrospectiveCue",
            "straightThroughCue",
            "sections",
            "transitions",
            "entrySection",
        },
        str(config_path),
    )
    schema = require_integer(raw.get("schemaVersion"), "schemaVersion")
    if schema != SCHEMA_VERSION:
        raise PipelineError(
            "schemaVersion {} is unsupported; expected {}".format(
                schema, SCHEMA_VERSION
            )
        )
    track_id = require_slug(raw.get("id"), "id")
    title = require_string(raw.get("title"), "title")
    source_archive = require_string(raw.get("sourceArchive"), "sourceArchive")
    if "\x00" in source_archive:
        raise PipelineError("sourceArchive contains a NUL byte")
    bpm = require_number(raw.get("bpm"), "bpm", 1.0, 500.0)
    beats_per_bar = require_integer(
        raw.get("beatsPerBar"), "beatsPerBar", 1, 32
    )
    segment_bars = require_integer(
        raw.get("segmentBars", 4), "segmentBars", 1, 64
    )
    grid_origin_frame = require_integer(
        raw.get("gridOriginFrame"), "gridOriginFrame", 0
    )
    configured_bar_frames = require_integer(
        raw.get("barFrames"), "barFrames", 1
    )
    source_end_frame = require_integer(
        raw.get("sourceEndFrame"), "sourceEndFrame", 1
    )
    if source_end_frame <= grid_origin_frame:
        raise PipelineError("sourceEndFrame must be after gridOriginFrame")
    if (source_end_frame - grid_origin_frame) % configured_bar_frames != 0:
        raise PipelineError(
            "sourceEndFrame - gridOriginFrame must contain an integral number "
            "of configured bars"
        )
    master_gain = require_number(
        raw.get("masterGainDb", 0.0), "masterGainDb", -60.0, 12.0
    )
    default_track = raw.get("default", False)
    if not isinstance(default_track, bool):
        raise PipelineError("default must be true or false")
    provenance_raw = raw.get("provenance")
    if not isinstance(provenance_raw, dict):
        raise PipelineError("provenance must be an object")
    ensure_only_keys(
        provenance_raw,
        {"sourceTool", "rightsStatus", "shipApproval"},
        "provenance",
    )
    provenance = {
        "sourceTool": require_string(
            provenance_raw.get("sourceTool"), "provenance.sourceTool"
        ),
        "rightsStatus": require_string(
            provenance_raw.get("rightsStatus"), "provenance.rightsStatus"
        ),
        "shipApproval": require_string(
            provenance_raw.get("shipApproval"), "provenance.shipApproval"
        ),
    }
    if provenance["rightsStatus"] not in (
        "user-supplied-unverified",
        "rights-cleared",
    ):
        raise PipelineError(
            "provenance.rightsStatus must be user-supplied-unverified or "
            "rights-cleared"
        )
    if provenance["shipApproval"] not in ("pending", "approved"):
        raise PipelineError(
            "provenance.shipApproval must be pending or approved"
        )

    encoding_raw = raw.get("encoding", {})
    if not isinstance(encoding_raw, dict):
        raise PipelineError("encoding must be an object")
    ensure_only_keys(encoding_raw, {"bitrateKbps"}, "encoding")
    encoding = {
        "bitrateKbps": require_integer(
            encoding_raw.get("bitrateKbps", 128),
            "encoding.bitrateKbps",
            48,
            512,
        )
    }

    analysis_raw = raw.get("analysis", {})
    if not isinstance(analysis_raw, dict):
        raise PipelineError("analysis must be an object")
    ensure_only_keys(
        analysis_raw,
        {
            "windowMs",
            "activityDbfs",
            "silenceRmsDbfs",
            "silencePeakDbfs",
            "trimRmsDbfs",
            "trimPeakDbfs",
            "minimumBoundarySimilarity",
            "minimumTransitionSimilarity",
            "targetPeakDbfs",
        },
        "analysis",
    )
    analysis = {
        "windowMs": require_number(
            analysis_raw.get("windowMs", 50.0), "analysis.windowMs", 5.0, 500.0
        ),
        "activityDbfs": require_number(
            analysis_raw.get("activityDbfs", -54.0),
            "analysis.activityDbfs",
            -120.0,
            0.0,
        ),
        "silenceRmsDbfs": require_number(
            analysis_raw.get("silenceRmsDbfs", -72.0),
            "analysis.silenceRmsDbfs",
            -120.0,
            0.0,
        ),
        "silencePeakDbfs": require_number(
            analysis_raw.get("silencePeakDbfs", -48.0),
            "analysis.silencePeakDbfs",
            -120.0,
            0.0,
        ),
        "trimRmsDbfs": require_number(
            analysis_raw.get("trimRmsDbfs", -58.0),
            "analysis.trimRmsDbfs",
            -120.0,
            0.0,
        ),
        "trimPeakDbfs": require_number(
            analysis_raw.get("trimPeakDbfs", -42.0),
            "analysis.trimPeakDbfs",
            -120.0,
            0.0,
        ),
        "minimumBoundarySimilarity": require_number(
            analysis_raw.get("minimumBoundarySimilarity", 0.55),
            "analysis.minimumBoundarySimilarity",
            0.0,
            1.0,
        ),
        "minimumTransitionSimilarity": require_number(
            analysis_raw.get("minimumTransitionSimilarity", 0.3),
            "analysis.minimumTransitionSimilarity",
            0.0,
            1.0,
        ),
        "targetPeakDbfs": require_number(
            analysis_raw.get("targetPeakDbfs", -1.0),
            "analysis.targetPeakDbfs",
            -24.0,
            0.0,
        ),
    }
    if analysis["silenceRmsDbfs"] > analysis["activityDbfs"]:
        raise PipelineError(
            "analysis.silenceRmsDbfs must not exceed analysis.activityDbfs"
        )

    adaptive_latency_raw = raw.get("adaptiveLatencyBudgetBars", {})
    if not isinstance(adaptive_latency_raw, dict):
        raise PipelineError("adaptiveLatencyBudgetBars must be an object")
    ensure_only_keys(
        adaptive_latency_raw,
        {"gameplay", "resolve"},
        "adaptiveLatencyBudgetBars",
    )
    adaptive_latency_budget_bars = {
        "gameplay": require_number(
            adaptive_latency_raw.get(
                "gameplay",
                DEFAULT_ADAPTIVE_LATENCY_BUDGET_BARS["gameplay"],
            ),
            "adaptiveLatencyBudgetBars.gameplay",
            0.25,
            64.0,
        ),
        "resolve": require_number(
            adaptive_latency_raw.get(
                "resolve",
                DEFAULT_ADAPTIVE_LATENCY_BUDGET_BARS["resolve"],
            ),
            "adaptiveLatencyBudgetBars.resolve",
            0.25,
            64.0,
        ),
    }

    stems_raw = raw.get("stems")
    if not isinstance(stems_raw, list) or not stems_raw:
        raise PipelineError("stems must be a non-empty array")
    stems: List[Dict[str, Any]] = []
    stem_ids = set()
    stem_sources = set()
    for index, stem_raw in enumerate(stems_raw):
        context = "stems[{}]".format(index)
        if not isinstance(stem_raw, dict):
            raise PipelineError("{} must be an object".format(context))
        ensure_only_keys(
            stem_raw,
            {"id", "source", "label", "role", "gainDb", "response"},
            context,
        )
        stem_id = require_slug(stem_raw.get("id"), context + ".id")
        source = require_string(stem_raw.get("source"), context + ".source")
        if stem_id in stem_ids:
            raise PipelineError("duplicate stem id: {}".format(stem_id))
        if source in stem_sources:
            raise PipelineError("duplicate stem source mapping: {!r}".format(source))
        stem_ids.add(stem_id)
        stem_sources.add(source)
        response_raw = stem_raw.get("response")
        if not isinstance(response_raw, dict):
            raise PipelineError("{} must be an object".format(context + ".response"))
        ensure_only_keys(response_raw, {"minimum", "full"}, context + ".response")
        response_minimum = require_number(
            response_raw.get("minimum"), context + ".response.minimum", 0.0, 1.0
        )
        response_full = require_number(
            response_raw.get("full"), context + ".response.full", 0.0, 1.0
        )
        if response_minimum > response_full:
            raise PipelineError(
                "{}.response.minimum must not exceed .full".format(context)
            )
        stems.append(
            {
                "id": stem_id,
                "source": source,
                "label": require_string(stem_raw.get("label"), context + ".label"),
                "role": require_string(stem_raw.get("role"), context + ".role"),
                "gainDb": require_number(
                    stem_raw.get("gainDb", 0.0),
                    context + ".gainDb",
                    -60.0,
                    24.0,
                ),
                "response": {
                    "minimum": response_minimum,
                    "full": response_full,
                },
            }
        )

    adaptive_seam_raw = raw.get("adaptiveSeam")
    adaptive_seam: Optional[Dict[str, Any]] = None
    if adaptive_seam_raw is not None:
        if not isinstance(adaptive_seam_raw, dict):
            raise PipelineError("adaptiveSeam must be an object")
        ensure_only_keys(
            adaptive_seam_raw,
            {
                "strategy",
                "retreatBars",
                "overlapBars",
                "riseBars",
                "curve",
            },
            "adaptiveSeam",
        )
        strategy = require_string(
            adaptive_seam_raw.get("strategy"), "adaptiveSeam.strategy"
        )
        if strategy != "staged":
            raise PipelineError("adaptiveSeam.strategy must be staged")
        curve = require_string(
            adaptive_seam_raw.get("curve"), "adaptiveSeam.curve"
        )
        if curve != "linear":
            raise PipelineError("adaptiveSeam.curve must be linear")
        retreat_bars = require_number(
            adaptive_seam_raw.get("retreatBars"),
            "adaptiveSeam.retreatBars",
            0.25,
            64.0,
        )
        overlap_bars = require_number(
            adaptive_seam_raw.get("overlapBars"),
            "adaptiveSeam.overlapBars",
            0.0,
            64.0,
        )
        rise_bars = require_number(
            adaptive_seam_raw.get("riseBars"),
            "adaptiveSeam.riseBars",
            0.25,
            64.0,
        )
        if overlap_bars > retreat_bars or overlap_bars > rise_bars:
            raise PipelineError(
                "adaptiveSeam.overlapBars must not exceed retreatBars or "
                "riseBars"
            )
        adaptive_seam = {
            "strategy": strategy,
            "retreatBars": retreat_bars,
            "overlapBars": overlap_bars,
            "riseBars": rise_bars,
            "curve": curve,
        }

    retrospective_cue_raw = raw.get("retrospectiveCue")
    retrospective_cue: Optional[Dict[str, Any]] = None
    if retrospective_cue_raw is not None:
        if not isinstance(retrospective_cue_raw, dict):
            raise PipelineError("retrospectiveCue must be an object")
        ensure_only_keys(
            retrospective_cue_raw,
            {"id", "startBar", "barCount", "anchorBar", "stems"},
            "retrospectiveCue",
        )
        cue_id = require_slug(
            retrospective_cue_raw.get("id"), "retrospectiveCue.id"
        )
        total_source_bars = (
            source_end_frame - grid_origin_frame
        ) // configured_bar_frames
        start_bar = require_integer(
            retrospective_cue_raw.get("startBar"),
            "retrospectiveCue.startBar",
            0,
            total_source_bars - 1,
        )
        bar_count = require_integer(
            retrospective_cue_raw.get("barCount"),
            "retrospectiveCue.barCount",
            1,
            256,
        )
        if start_bar + bar_count > total_source_bars:
            raise PipelineError(
                "retrospectiveCue range ends at bar {}, after configured "
                "source end bar {}".format(
                    start_bar + bar_count, total_source_bars
                )
            )
        anchor_bar = require_integer(
            retrospective_cue_raw.get("anchorBar"),
            "retrospectiveCue.anchorBar",
            0,
            bar_count - 1,
        )
        cue_stems_raw = retrospective_cue_raw.get("stems")
        if not isinstance(cue_stems_raw, list) or not cue_stems_raw:
            raise PipelineError(
                "retrospectiveCue.stems must be a non-empty array"
            )
        cue_stems: List[str] = []
        for index, stem_id_raw in enumerate(cue_stems_raw):
            stem_id = require_slug(
                stem_id_raw,
                "retrospectiveCue.stems[{}]".format(index),
            )
            if stem_id not in stem_ids:
                raise PipelineError(
                    "retrospectiveCue.stems names unknown stem: {}".format(
                        stem_id
                    )
                )
            if stem_id in cue_stems:
                raise PipelineError(
                    "retrospectiveCue.stems contains duplicate stem: {}".format(
                        stem_id
                    )
                )
            cue_stems.append(stem_id)
        retrospective_cue = {
            "id": cue_id,
            "startBar": start_bar,
            "barCount": bar_count,
            "anchorBar": anchor_bar,
            "stems": cue_stems,
        }

    straight_through_cue_raw = raw.get("straightThroughCue")
    straight_through_cue: Optional[Dict[str, Any]] = None
    if straight_through_cue_raw is not None:
        if not isinstance(straight_through_cue_raw, dict):
            raise PipelineError("straightThroughCue must be an object")
        ensure_only_keys(
            straight_through_cue_raw,
            {"id", "startBar", "barCount", "stems"},
            "straightThroughCue",
        )
        cue_id = require_slug(
            straight_through_cue_raw.get("id"), "straightThroughCue.id"
        )
        total_source_bars = (
            source_end_frame - grid_origin_frame
        ) // configured_bar_frames
        start_bar = require_integer(
            straight_through_cue_raw.get("startBar"),
            "straightThroughCue.startBar",
            0,
            total_source_bars - 1,
        )
        bar_count = require_integer(
            straight_through_cue_raw.get("barCount"),
            "straightThroughCue.barCount",
            1,
            256,
        )
        if start_bar + bar_count > total_source_bars:
            raise PipelineError(
                "straightThroughCue range ends at bar {}, after configured "
                "source end bar {}".format(
                    start_bar + bar_count, total_source_bars
                )
            )
        cue_stems_raw = straight_through_cue_raw.get("stems")
        if not isinstance(cue_stems_raw, list) or not cue_stems_raw:
            raise PipelineError(
                "straightThroughCue.stems must be a non-empty array"
            )
        cue_stems: List[str] = []
        for index, stem_id_raw in enumerate(cue_stems_raw):
            stem_id = require_slug(
                stem_id_raw,
                "straightThroughCue.stems[{}]".format(index),
            )
            if stem_id not in stem_ids:
                raise PipelineError(
                    "straightThroughCue.stems names unknown stem: {}".format(
                        stem_id
                    )
                )
            if stem_id in cue_stems:
                raise PipelineError(
                    "straightThroughCue.stems contains duplicate stem: {}".format(
                        stem_id
                    )
                )
            cue_stems.append(stem_id)
        straight_through_cue = {
            "id": cue_id,
            "startBar": start_bar,
            "barCount": bar_count,
            "stems": cue_stems,
        }

    sections_raw = raw.get("sections", [])
    if not isinstance(sections_raw, list):
        raise PipelineError("sections must be an array")
    sections: List[Dict[str, Any]] = []
    section_ids = set()
    for index, section_raw in enumerate(sections_raw):
        context = "sections[{}]".format(index)
        if not isinstance(section_raw, dict):
            raise PipelineError("{} must be an object".format(context))
        ensure_only_keys(
            section_raw,
            {
                "id",
                "label",
                "startBar",
                "barCount",
                "classification",
                "role",
                "loop",
                "repeat",
                "cooldownSeconds",
                "stemGainsDb",
            },
            context,
        )
        section_id = require_slug(section_raw.get("id"), context + ".id")
        if section_id in section_ids:
            raise PipelineError("duplicate section id: {}".format(section_id))
        section_ids.add(section_id)
        start_bar = require_integer(
            section_raw.get("startBar"), context + ".startBar", 0
        )
        bar_count_raw = section_raw.get("barCount")
        if bar_count_raw == "to-end":
            bar_count: Any = "to-end"
        else:
            bar_count = require_integer(
                bar_count_raw, context + ".barCount", 1, 256
            )
        classification = require_string(
            section_raw.get("classification"), context + ".classification"
        )
        if classification not in CLASSIFICATIONS:
            raise PipelineError(
                "{}.classification must be one of {}".format(
                    context, "|".join(CLASSIFICATIONS)
                )
            )
        role = require_string(section_raw.get("role"), context + ".role")
        if role not in SECTION_ROLES:
            raise PipelineError(
                "{}.role must be one of {}".format(
                    context, "|".join(SECTION_ROLES)
                )
            )
        loop_raw = section_raw.get("loop")
        loop: Optional[Dict[str, Any]] = None
        if loop_raw is not None:
            if not isinstance(loop_raw, dict):
                raise PipelineError("{} must be an object".format(context + ".loop"))
            ensure_only_keys(
                loop_raw,
                {
                    "approval",
                    "strategy",
                    "crossfadeSeconds",
                    "allowLowSimilarity",
                },
                context + ".loop",
            )
            approval = require_string(
                loop_raw.get("approval"), context + ".loop.approval"
            )
            if approval not in ("analysis-reviewed", "auditioned"):
                raise PipelineError(
                    "{}.loop.approval must be analysis-reviewed or auditioned; "
                    "loopability cannot be inferred safely".format(context)
                )
            strategy = require_string(
                loop_raw.get("strategy", "equal-power"),
                context + ".loop.strategy",
            )
            if strategy != "rendered-head-crossfade":
                raise PipelineError(
                    "{}.loop.strategy must be rendered-head-crossfade; raw "
                    "source seams are never asserted to be seamless".format(context)
                )
            allow_low = loop_raw.get("allowLowSimilarity", False)
            if not isinstance(allow_low, bool):
                raise PipelineError(
                    "{}.loop.allowLowSimilarity must be true or false".format(
                        context
                    )
                )
            loop = {
                "approval": approval,
                "strategy": strategy,
                "crossfadeSeconds": require_number(
                    loop_raw.get("crossfadeSeconds", 0.25),
                    context + ".loop.crossfadeSeconds",
                    0.02,
                    4.0,
                ),
                "allowLowSimilarity": allow_low,
            }
        repeat_raw = section_raw.get("repeat")
        repeat: Optional[Dict[str, Any]] = None
        if repeat_raw is not None:
            if not isinstance(repeat_raw, dict):
                raise PipelineError(
                    "{} must be an object".format(context + ".repeat")
                )
            ensure_only_keys(
                repeat_raw,
                {"minimumBars"},
                context + ".repeat",
            )
            minimum_bars = require_integer(
                repeat_raw.get("minimumBars"),
                context + ".repeat.minimumBars",
                1,
                1024,
            )
            if bar_count == "to-end":
                raise PipelineError(
                    "{}.repeat requires an integral barCount".format(context)
                )
            if minimum_bars < bar_count or minimum_bars % bar_count != 0:
                raise PipelineError(
                    "{}.repeat.minimumBars must be a whole-section multiple "
                    "at least as long as barCount".format(context)
                )
            repeat = {"minimumBars": minimum_bars}
        cooldown_seconds_raw = section_raw.get("cooldownSeconds")
        cooldown_seconds: Optional[float] = None
        if cooldown_seconds_raw is not None:
            cooldown_seconds = require_number(
                cooldown_seconds_raw,
                context + ".cooldownSeconds",
                0.1,
                3600.0,
            )
        if role == "hold" and loop is None:
            raise PipelineError("{} role hold requires a reviewed loop".format(context))
        if role != "hold" and loop is not None:
            raise PipelineError(
                "{} role {} must be finite and cannot define loop".format(
                    context, role
                )
            )
        if (classification == "resolve") != (role == "resolve"):
            raise PipelineError(
                "{} must use role resolve exactly when classification is "
                "resolve".format(context)
            )
        if repeat is not None and role != "hold":
            raise PipelineError(
                "{}.repeat is only valid for role hold".format(context)
            )
        if cooldown_seconds is not None and role != "stinger":
            raise PipelineError(
                "{}.cooldownSeconds is only valid for role stinger".format(
                    context
                )
            )
        stem_gains_raw = section_raw.get("stemGainsDb")
        stem_gains: Optional[Dict[str, float]] = None
        if stem_gains_raw is not None:
            if not isinstance(stem_gains_raw, dict):
                raise PipelineError(
                    "{}.stemGainsDb must be an object".format(context)
                )
            unknown_stems = sorted(set(stem_gains_raw) - stem_ids)
            if unknown_stems:
                raise PipelineError(
                    "{}.stemGainsDb names unknown stems: {}".format(
                        context, ", ".join(unknown_stems)
                    )
                )
            stem_gains = {
                stem_id: require_number(
                    gain,
                    "{}.stemGainsDb.{}".format(context, stem_id),
                    -60.0,
                    24.0,
                )
                for stem_id, gain in stem_gains_raw.items()
            }
        section = {
            "id": section_id,
            "label": require_string(
                section_raw.get("label"), context + ".label"
            ),
            "startBar": start_bar,
            "barCount": bar_count,
            "classification": classification,
            "role": role,
            "loop": loop,
        }
        if repeat is not None:
            section["repeat"] = repeat
        if cooldown_seconds is not None:
            section["cooldownSeconds"] = cooldown_seconds
        if stem_gains is not None:
            section["stemGainsDb"] = stem_gains
        sections.append(section)

    transitions_raw = raw.get("transitions", [])
    if not isinstance(transitions_raw, list):
        raise PipelineError("transitions must be an array")
    transitions: List[Dict[str, Any]] = []
    transition_pairs = set()
    for index, transition_raw in enumerate(transitions_raw):
        context = "transitions[{}]".format(index)
        if not isinstance(transition_raw, dict):
            raise PipelineError("{} must be an object".format(context))
        ensure_only_keys(
            transition_raw,
            {
                "from",
                "to",
                "quantizeBars",
                "crossfadeBars",
                "weight",
                "timing",
                "allowLowSimilarity",
            },
            context,
        )
        source = require_slug(transition_raw.get("from"), context + ".from")
        target = require_slug(transition_raw.get("to"), context + ".to")
        if source not in section_ids or target not in section_ids:
            raise PipelineError(
                "{} references an unknown section ({} -> {})".format(
                    context, source, target
                )
            )
        pair = (source, target)
        if pair in transition_pairs:
            raise PipelineError(
                "duplicate transition: {} -> {}".format(source, target)
            )
        transition_pairs.add(pair)
        allow_low = transition_raw.get("allowLowSimilarity", False)
        if not isinstance(allow_low, bool):
            raise PipelineError(
                "{}.allowLowSimilarity must be true or false".format(context)
            )
        quantize_bars = require_integer(
            transition_raw.get("quantizeBars", segment_bars),
            context + ".quantizeBars",
            1,
            64,
        )
        crossfade_bars = require_number(
            transition_raw.get("crossfadeBars", 0.25),
            context + ".crossfadeBars",
            0.0,
            float(quantize_bars),
        )
        timing = require_string(
            transition_raw.get("timing"), context + ".timing"
        )
        if timing not in ("next-quantum", "section-end"):
            raise PipelineError(
                "{}.timing must be next-quantum or section-end".format(context)
            )
        transitions.append(
            {
                "from": source,
                "to": target,
                "quantizeBars": quantize_bars,
                "crossfadeBars": crossfade_bars,
                "weight": require_number(
                    transition_raw.get("weight", 1.0),
                    context + ".weight",
                    0.001,
                    1000.0,
                ),
                "timing": timing,
                "allowLowSimilarity": allow_low,
            }
        )

    entry_section_raw = raw.get("entrySection")
    entry_section: Optional[str] = None
    if entry_section_raw is not None:
        entry_section = require_slug(entry_section_raw, "entrySection")
        if entry_section not in section_ids:
            raise PipelineError(
                "entrySection references unknown section: {}".format(entry_section)
            )
    if sections and entry_section is None:
        raise PipelineError("entrySection is required when sections are configured")
    if entry_section is not None:
        entry = next(section for section in sections if section["id"] == entry_section)
        if entry["role"] != "hold":
            raise PipelineError("entrySection must reference a hold section")
    if transitions and not sections:
        raise PipelineError("transitions require configured sections")

    return {
        "schemaVersion": SCHEMA_VERSION,
        "id": track_id,
        "title": title,
        "default": default_track,
        "provenance": provenance,
        "sourceArchive": source_archive,
        "bpm": bpm,
        "beatsPerBar": beats_per_bar,
        "segmentBars": segment_bars,
        "gridOriginFrame": grid_origin_frame,
        "barFrames": configured_bar_frames,
        "sourceEndFrame": source_end_frame,
        "masterGainDb": master_gain,
        "encoding": encoding,
        "analysis": analysis,
        "adaptiveLatencyBudgetBars": adaptive_latency_budget_bars,
        "adaptiveSeam": adaptive_seam,
        "stems": stems,
        "retrospectiveCue": retrospective_cue,
        "straightThroughCue": straight_through_cue,
        "sections": sections,
        "transitions": transitions,
        "entrySection": entry_section,
    }


def normalized_member_name(info: zipfile.ZipInfo) -> str:
    original_name = getattr(info, "orig_filename", info.filename)
    if "\x00" in original_name:
        raise PipelineError("ZIP member name contains a NUL byte")
    name = info.filename
    if not name:
        raise PipelineError("ZIP contains an empty member name")
    if "\\" in name:
        raise PipelineError(
            "ZIP member {!r} uses backslashes; archive paths must be POSIX".format(
                name
            )
        )
    if name.startswith("/") or re.match(r"^[A-Za-z]:", name):
        raise PipelineError("ZIP member {!r} is an absolute path".format(name))
    path = PurePosixPath(name)
    if any(part in ("", ".", "..") for part in path.parts):
        raise PipelineError("ZIP member {!r} has an unsafe path".format(name))
    return unicodedata.normalize("NFC", name.rstrip("/")).casefold()


def inspect_archive(
    archive_path: Path, stems: Sequence[Dict[str, Any]]
) -> Tuple[zipfile.ZipFile, Dict[str, zipfile.ZipInfo], Dict[str, Any]]:
    try:
        archive = zipfile.ZipFile(str(archive_path), "r")
    except (OSError, zipfile.BadZipFile) as error:
        raise PipelineError(
            "cannot open source archive {}: {}".format(archive_path, error)
        ) from error
    try:
        infos = archive.infolist()
        if len(infos) > MAX_ARCHIVE_FILES:
            raise PipelineError(
                "ZIP has {} entries; safety limit is {}".format(
                    len(infos), MAX_ARCHIVE_FILES
                )
            )
        seen_names: Dict[str, str] = {}
        files: Dict[str, zipfile.ZipInfo] = {}
        total_bytes = 0
        for info in infos:
            normalized = normalized_member_name(info)
            if normalized in seen_names:
                raise PipelineError(
                    "ZIP has duplicate/colliding members {!r} and {!r}".format(
                        seen_names[normalized], info.filename
                    )
                )
            seen_names[normalized] = info.filename
            if info.flag_bits & 0x1:
                raise PipelineError(
                    "ZIP member {!r} is encrypted; encrypted sources are unsupported".format(
                        info.filename
                    )
                )
            unix_mode = (info.external_attr >> 16) & 0xFFFF
            file_type = stat.S_IFMT(unix_mode)
            if info.create_system == 3 and file_type:
                if stat.S_ISLNK(unix_mode):
                    raise PipelineError(
                        "ZIP member {!r} is a symlink".format(info.filename)
                    )
                if not (stat.S_ISREG(unix_mode) or stat.S_ISDIR(unix_mode)):
                    raise PipelineError(
                        "ZIP member {!r} is not a regular file".format(info.filename)
                    )
            if info.is_dir():
                continue
            if info.file_size > MAX_MEMBER_BYTES:
                raise PipelineError(
                    "ZIP member {!r} is {} bytes; safety limit is {}".format(
                        info.filename, info.file_size, MAX_MEMBER_BYTES
                    )
                )
            total_bytes += info.file_size
            if total_bytes > MAX_ARCHIVE_BYTES:
                raise PipelineError(
                    "ZIP expands beyond the {} byte safety limit".format(
                        MAX_ARCHIVE_BYTES
                    )
                )
            if info.file_size and info.compress_size <= 0:
                raise PipelineError(
                    "ZIP member {!r} has an invalid compressed size".format(
                        info.filename
                    )
                )
            ratio = info.file_size / max(1, info.compress_size)
            if ratio > MAX_COMPRESSION_RATIO:
                raise PipelineError(
                    "ZIP member {!r} compression ratio {:.1f}:1 exceeds "
                    "the {:.0f}:1 safety limit".format(
                        info.filename, ratio, MAX_COMPRESSION_RATIO
                    )
                )
            files[info.filename] = info
        configured_sources = {stem["source"] for stem in stems}
        archive_sources = set(files)
        missing = sorted(configured_sources - archive_sources)
        unexpected = sorted(archive_sources - configured_sources)
        if missing:
            raise PipelineError(
                "ZIP is missing configured stem mapping{}: {}".format(
                    "" if len(missing) == 1 else "s",
                    ", ".join(repr(item) for item in missing),
                )
            )
        if unexpected:
            raise PipelineError(
                "ZIP contains unmapped file{}: {}. Every file must have an "
                "explicit stem mapping.".format(
                    "" if len(unexpected) == 1 else "s",
                    ", ".join(repr(item) for item in unexpected),
                )
            )
        summary = {
            "memberCount": len(files),
            "uncompressedBytes": total_bytes,
            "compressedBytes": sum(info.compress_size for info in files.values()),
        }
        return archive, files, summary
    except Exception:
        archive.close()
        raise


def extract_stems_safely(
    archive_path: Path,
    stems: Sequence[Dict[str, Any]],
    destination: Path,
) -> Tuple[Dict[str, Path], Dict[str, Any]]:
    archive, files, summary = inspect_archive(archive_path, stems)
    extracted: Dict[str, Path] = {}
    try:
        for stem in stems:
            info = files[stem["source"]]
            output_path = destination / "{}.wav".format(stem["id"])
            bytes_written = 0
            try:
                with archive.open(info, "r") as source, output_path.open("xb") as target:
                    while True:
                        chunk = source.read(READ_CHUNK_BYTES)
                        if not chunk:
                            break
                        bytes_written += len(chunk)
                        if bytes_written > info.file_size:
                            raise PipelineError(
                                "ZIP member {!r} expanded past its declared size".format(
                                    info.filename
                                )
                            )
                        target.write(chunk)
            except (OSError, EOFError, zipfile.BadZipFile, RuntimeError) as error:
                raise PipelineError(
                    "failed reading ZIP member {!r}: {}".format(
                        info.filename, error
                    )
                ) from error
            if bytes_written != info.file_size:
                raise PipelineError(
                    "ZIP member {!r} produced {} bytes; expected {}".format(
                        info.filename, bytes_written, info.file_size
                    )
                )
            extracted[stem["id"]] = output_path
    finally:
        archive.close()
    return extracted, summary


def inspect_wav(path: Path, stem_id: str) -> Dict[str, Any]:
    try:
        with wave.open(str(path), "rb") as reader:
            params = reader.getparams()
            if params.comptype != "NONE":
                raise PipelineError(
                    "stem {} is compressed WAV ({}); aligned PCM WAV is required".format(
                        stem_id, params.comptype
                    )
                )
            if params.nchannels < 1 or params.nchannels > 8:
                raise PipelineError(
                    "stem {} has unsupported channel count {}".format(
                        stem_id, params.nchannels
                    )
                )
            if params.sampwidth not in (1, 2, 3, 4):
                raise PipelineError(
                    "stem {} has unsupported PCM width {} bytes".format(
                        stem_id, params.sampwidth
                    )
                )
            if params.framerate <= 0 or params.nframes <= 0:
                raise PipelineError(
                    "stem {} has invalid sample rate/frame count".format(stem_id)
                )
            expected_bytes = params.nframes * params.nchannels * params.sampwidth
            read_bytes = 0
            while True:
                data = reader.readframes(65536)
                if not data:
                    break
                read_bytes += len(data)
            if read_bytes != expected_bytes:
                raise PipelineError(
                    "stem {} PCM payload has {} bytes; expected {}".format(
                        stem_id, read_bytes, expected_bytes
                    )
                )
            return {
                "frames": params.nframes,
                "sampleRate": params.framerate,
                "channels": params.nchannels,
                "sampleWidthBytes": params.sampwidth,
            }
    except (OSError, EOFError, wave.Error) as error:
        raise PipelineError(
            "stem {} is not a readable PCM WAV: {}".format(stem_id, error)
        ) from error


def validate_wav_alignment(
    paths: Dict[str, Path], stems: Sequence[Dict[str, Any]]
) -> Dict[str, Any]:
    reference: Optional[Dict[str, Any]] = None
    reference_id: Optional[str] = None
    for stem in stems:
        stem_id = stem["id"]
        current = inspect_wav(paths[stem_id], stem_id)
        if reference is None:
            reference = current
            reference_id = stem_id
            continue
        differences = [
            key
            for key in ("frames", "sampleRate", "channels", "sampleWidthBytes")
            if current[key] != reference[key]
        ]
        if differences:
            detail = ", ".join(
                "{}={} ({}={})".format(
                    key, current[key], reference_id, reference[key]
                )
                for key in differences
            )
            raise PipelineError(
                "stem {} is not sample-aligned with {}: {}".format(
                    stem_id, reference_id, detail
                )
            )
    if reference is None:
        raise PipelineError("no WAV stems were extracted")
    return reference


def decode_pcm(data: bytes, sample_width: int) -> Sequence[int]:
    if sample_width == 1:
        return [value - 128 for value in data]
    if sample_width == 2:
        values = array.array("h")
        values.frombytes(data)
        if sys.byteorder != "little":
            values.byteswap()
        return values
    if sample_width == 4:
        values = array.array("i")
        values.frombytes(data)
        if sys.byteorder != "little":
            values.byteswap()
        return values
    # Standard wave PCM is little-endian. Sign-extend each 24-bit sample.
    values_24: List[int] = []
    for offset in range(0, len(data), 3):
        value = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16)
        if value & 0x800000:
            value -= 0x1000000
        values_24.append(value)
    return values_24


def encode_pcm(samples: Sequence[int], sample_width: int) -> bytes:
    if sample_width == 1:
        return bytes(max(0, min(255, int(value) + 128)) for value in samples)
    if sample_width == 2:
        values = array.array("h", (int(value) for value in samples))
        if sys.byteorder != "little":
            values.byteswap()
        return values.tobytes()
    if sample_width == 4:
        values = array.array("i", (int(value) for value in samples))
        if sys.byteorder != "little":
            values.byteswap()
        return values.tobytes()
    encoded = bytearray()
    for sample in samples:
        value = int(sample)
        if value < 0:
            value += 1 << 24
        encoded.extend(
            (value & 0xFF, (value >> 8) & 0xFF, (value >> 16) & 0xFF)
        )
    return bytes(encoded)


def sample_stats(
    samples: Sequence[float],
    frame_count: int,
    full_scale: float,
    activity_dbfs: float,
) -> Dict[str, Any]:
    sample_count = len(samples)
    if sample_count == 0:
        raise PipelineError("internal error: empty PCM analysis window")
    sum_squares = 0.0
    peak = 0.0
    for value in samples:
        magnitude = abs(float(value))
        if magnitude > peak:
            peak = magnitude
        sum_squares += float(value) * float(value)
    rms = math.sqrt(sum_squares / sample_count)
    rms_db = dbfs(rms, full_scale)
    return {
        "sumSquares": sum_squares,
        "sampleCount": sample_count,
        "frameCount": frame_count,
        "fullScale": full_scale,
        "peak": peak,
        "rmsDbfs": rms_db,
        "peakDbfs": dbfs(peak, full_scale),
        "active": rms_db >= activity_dbfs,
    }


def classify_energy(energy: float, final: bool = False) -> str:
    if final:
        return "resolve"
    if energy < 0.2:
        return "sparse"
    if energy < 0.4:
        return "tension"
    if energy < 0.62:
        return "pursuit"
    if energy < 0.84:
        return "combat"
    return "climax"


def analysis_windows_in_range(
    windows: Sequence[Dict[str, Any]], start_frame: int, end_frame: int
) -> List[Dict[str, Any]]:
    return [
        item
        for item in windows
        if int(item["startFrame"]) < end_frame
        and int(item["startFrame"]) + int(item["frameCount"]) > start_frame
    ]


def analyze_pack(
    paths: Dict[str, Path],
    config: Dict[str, Any],
    pcm: Dict[str, Any],
) -> Dict[str, Any]:
    sample_rate = int(pcm["sampleRate"])
    channels = int(pcm["channels"])
    sample_width = int(pcm["sampleWidthBytes"])
    total_frames = int(pcm["frames"])
    bpm_fraction = Fraction(str(config["bpm"]))
    bar_frames_fraction = (
        Fraction(sample_rate * 60 * config["beatsPerBar"], 1) / bpm_fraction
    )
    if bar_frames_fraction.denominator != 1:
        raise PipelineError(
            "bpm/beatsPerBar produce non-integral bar boundaries at {} Hz "
            "({} frames/bar); choose timing that lands on PCM frames".format(
                sample_rate, bar_frames_fraction
            )
        )
    bar_frames = int(bar_frames_fraction)
    if bar_frames != config["barFrames"]:
        raise PipelineError(
            "configured barFrames {} disagrees with BPM/sample-rate timing "
            "({} frames/bar)".format(config["barFrames"], bar_frames)
        )
    grid_origin = int(config["gridOriginFrame"])
    source_end = int(config["sourceEndFrame"])
    if grid_origin >= total_frames:
        raise PipelineError(
            "gridOriginFrame {} is outside the {}-frame source".format(
                grid_origin, total_frames
            )
        )
    if source_end > total_frames:
        raise PipelineError(
            "sourceEndFrame {} exceeds the {}-frame source".format(
                source_end, total_frames
            )
        )
    window_frames_fraction = Fraction(
        sample_rate * Fraction(str(config["analysis"]["windowMs"])), 1000
    )
    if window_frames_fraction.denominator != 1:
        raise PipelineError(
            "analysis.windowMs does not land on an integral PCM frame at {} Hz".format(
                sample_rate
            )
        )
    window_frames = int(window_frames_fraction)
    if bar_frames % window_frames != 0:
        raise PipelineError(
            "analysis.windowMs must divide one musical bar exactly "
            "({} frames/bar, {} frames/window)".format(bar_frames, window_frames)
        )
    full_scale = float(1 << (sample_width * 8 - 1))
    stem_windows: Dict[str, List[Dict[str, Any]]] = {
        stem["id"]: [] for stem in config["stems"]
    }
    mix_windows: List[Dict[str, Any]] = []
    readers: List[wave.Wave_read] = []
    try:
        readers = [
            wave.open(str(paths[stem["id"]]), "rb") for stem in config["stems"]
        ]
        gains = [
            10.0 ** (float(stem["gainDb"]) / 20.0) for stem in config["stems"]
        ]
        for reader in readers:
            reader.setpos(grid_origin)
        frame_position = grid_origin
        while frame_position < source_end:
            requested_frames = min(window_frames, source_end - frame_position)
            decoded: List[Sequence[int]] = []
            actual_frames: Optional[int] = None
            for stem, reader in zip(config["stems"], readers):
                data = reader.readframes(requested_frames)
                frame_bytes = channels * sample_width
                if len(data) % frame_bytes:
                    raise PipelineError(
                        "stem {} returned a partial PCM frame during analysis".format(
                            stem["id"]
                        )
                    )
                current_frames = len(data) // frame_bytes
                if actual_frames is None:
                    actual_frames = current_frames
                elif current_frames != actual_frames:
                    raise PipelineError(
                        "stems stopped at different frames during analysis"
                    )
                samples = decode_pcm(data, sample_width)
                metric = sample_stats(
                    samples,
                    current_frames,
                    full_scale,
                    config["analysis"]["activityDbfs"],
                )
                metric["startFrame"] = frame_position
                stem_windows[stem["id"]].append(metric)
                decoded.append(samples)
            if not actual_frames:
                raise PipelineError("stems ended before their declared frame count")
            mixed_samples = []
            for sample_values in zip(*decoded):
                mixed_samples.append(
                    sum(
                        float(value) * gain
                        for value, gain in zip(sample_values, gains)
                    )
                )
            mix_metric = sample_stats(
                mixed_samples,
                actual_frames,
                full_scale,
                config["analysis"]["activityDbfs"],
            )
            mix_metric["startFrame"] = frame_position
            mix_windows.append(mix_metric)
            frame_position += actual_frames
        if frame_position != source_end:
            raise PipelineError(
                "analyzed {} PCM frames; expected {}".format(
                    frame_position, source_end
                )
            )
    finally:
        for reader in readers:
            reader.close()

    active_mix_windows = [
        item
        for item in mix_windows
        if float(item["rmsDbfs"]) >= config["analysis"]["trimRmsDbfs"]
        or float(item["peakDbfs"]) >= config["analysis"]["trimPeakDbfs"]
    ]
    if not active_mix_windows:
        raise PipelineError("all mapped stems are below the trim silence thresholds")
    raw_start = int(active_mix_windows[0]["startFrame"])
    raw_end = int(active_mix_windows[-1]["startFrame"]) + int(
        active_mix_windows[-1]["frameCount"]
    )
    trim_start = grid_origin + (
        (raw_start - grid_origin) // bar_frames
    ) * bar_frames
    trim_end = min(
        source_end,
        grid_origin
        + int(math.ceil((raw_end - grid_origin) / bar_frames)) * bar_frames,
    )
    if trim_end <= trim_start:
        raise PipelineError("silence trimming removed the entire soundtrack")

    source_bar_count = (source_end - grid_origin) // bar_frames
    bar_count = int(source_bar_count)
    bars: List[Dict[str, Any]] = []
    raw_energy: List[float] = []
    for bar_index in range(bar_count):
        start = grid_origin + bar_index * bar_frames
        end = min(source_end, start + bar_frames)
        stem_metrics: Dict[str, Dict[str, float]] = {}
        audible_count = 0
        for stem in config["stems"]:
            windows = analysis_windows_in_range(stem_windows[stem["id"]], start, end)
            metric = weighted_metric(windows)
            public_metric = {
                "rmsDbfs": metric["rmsDbfs"],
                "peakDbfs": metric["peakDbfs"],
                "activity": metric["activity"],
                "transient": metric["transient"],
            }
            stem_metrics[stem["id"]] = public_metric
            if (
                metric["rmsDbfs"] > config["analysis"]["silenceRmsDbfs"]
                or metric["peakDbfs"] > config["analysis"]["silencePeakDbfs"]
            ):
                audible_count += 1
        mix_metric = weighted_metric(
            analysis_windows_in_range(mix_windows, start, end)
        )
        score = (
            mix_metric["rmsDbfs"]
            + 1.5 * audible_count
            + 3.0 * mix_metric["transient"]
        )
        raw_energy.append(score)
        bars.append(
            {
                "index": bar_index,
                "startSeconds": round_float(start / sample_rate),
                "durationSeconds": round_float((end - start) / sample_rate),
                "energy": 0.0,
                "classification": "sparse",
                "mix": {
                    "rmsDbfs": mix_metric["rmsDbfs"],
                    "peakDbfs": mix_metric["peakDbfs"],
                    "activity": mix_metric["activity"],
                    "transient": mix_metric["transient"],
                    "audibleStemCount": audible_count,
                },
                "stems": stem_metrics,
            }
        )
    playable_scores = [
        raw_energy[index]
        for index, bar in enumerate(bars)
        if grid_origin + int(bar["index"]) * bar_frames < trim_end
        and grid_origin + (int(bar["index"]) + 1) * bar_frames > trim_start
    ]
    low = percentile(playable_scores, 0.05)
    high = percentile(playable_scores, 0.95)
    span = max(0.001, high - low)
    for index, bar in enumerate(bars):
        energy = min(1.0, max(0.0, (raw_energy[index] - low) / span))
        bar["energy"] = round_float(energy, 4)
        bar["classification"] = classify_energy(energy)

    phrase_bars = int(config["segmentBars"])
    phrases: List[Dict[str, Any]] = []
    first_trim_bar = (trim_start - grid_origin) // bar_frames
    trimmed_bar_count = (trim_end - trim_start) / bar_frames
    final_bar_exclusive = (trim_end - grid_origin) / bar_frames
    phrase_start = first_trim_bar
    while phrase_start < final_bar_exclusive:
        phrase_end = min(final_bar_exclusive, phrase_start + phrase_bars)
        relevant = [
            bar
            for bar in bars
            if bar["index"] < phrase_end and bar["index"] + 1 > phrase_start
        ]
        energy = (
            sum(float(bar["energy"]) * float(bar["durationSeconds"]) for bar in relevant)
            / max(
                0.001,
                sum(float(bar["durationSeconds"]) for bar in relevant),
            )
        )
        final = phrase_end >= final_bar_exclusive
        phrases.append(
            {
                "startBar": phrase_start,
                "barCount": round_float(phrase_end - phrase_start, 6),
                "energy": round_float(energy, 4),
                "classification": classify_energy(energy, final=final),
            }
        )
        phrase_start += phrase_bars

    boundary_candidates: List[Dict[str, Any]] = []
    boundary = first_trim_bar
    while boundary <= final_bar_exclusive:
        if boundary in (first_trim_bar, final_bar_exclusive):
            contrast = 1.0
        else:
            previous_index = max(0, min(len(bars) - 1, int(boundary) - 1))
            next_index = max(0, min(len(bars) - 1, int(boundary)))
            contrast = abs(
                float(bars[previous_index]["energy"])
                - float(bars[next_index]["energy"])
            )
        phrase_aligned = (
            isinstance(boundary, int)
            and (boundary - first_trim_bar) % phrase_bars == 0
        )
        score = min(1.0, (0.65 if phrase_aligned else 0.25) + 0.35 * contrast)
        boundary_candidates.append(
            {
                "bar": round_float(float(boundary), 6),
                "phraseAligned": phrase_aligned,
                "energyContrast": round_float(contrast, 4),
                "score": round_float(score, 4),
                "safeCandidate": score >= 0.55,
            }
        )
        boundary += phrase_bars
        if boundary > final_bar_exclusive and boundary - phrase_bars < final_bar_exclusive:
            boundary = final_bar_exclusive

    suggested_sections: List[Dict[str, Any]] = []
    for index, phrase in enumerate(phrases):
        suggested_sections.append(
            {
                "id": "candidate-{:02d}".format(index + 1),
                "startBar": phrase["startBar"],
                "barCount": phrase["barCount"],
                "classification": phrase["classification"],
                "energy": phrase["energy"],
                "loopable": False,
                "note": "Review harmony and boundary audio before marking loopable.",
            }
        )

    global_stems: List[Dict[str, Any]] = []
    globally_silent = set()
    for stem in config["stems"]:
        metric = weighted_metric(
            analysis_windows_in_range(
                stem_windows[stem["id"]], trim_start, trim_end
            )
        )
        silent = (
            metric["rmsDbfs"] <= config["analysis"]["silenceRmsDbfs"]
            and metric["peakDbfs"] <= config["analysis"]["silencePeakDbfs"]
        )
        if silent:
            globally_silent.add(stem["id"])
        global_stems.append(
            {
                "id": stem["id"],
                "source": stem["source"],
                "rmsDbfs": metric["rmsDbfs"],
                "peakDbfs": metric["peakDbfs"],
                "activity": metric["activity"],
                "transient": metric["transient"],
                "analyticallySilent": silent,
            }
        )
    global_mix = weighted_metric(
        analysis_windows_in_range(mix_windows, trim_start, trim_end)
    )
    post_gain_peak = global_mix["peakDbfs"] + config["masterGainDb"]
    if post_gain_peak > config["analysis"]["targetPeakDbfs"] + 0.1:
        raise PipelineError(
            "pack mix peak is {:.2f} dBFS; masterGainDb {:.2f} yields {:.2f} "
            "dBFS, above target {:.2f}. Lower only the pack-level master gain "
            "to preserve stem balance.".format(
                global_mix["peakDbfs"],
                config["masterGainDb"],
                post_gain_peak,
                config["analysis"]["targetPeakDbfs"],
            )
        )

    return {
        "sampleRate": sample_rate,
        "channels": channels,
        "sampleWidthBytes": sample_width,
        "totalFrames": total_frames,
        "gridOriginFrame": grid_origin,
        "sourceEndFrame": source_end,
        "barFrames": bar_frames,
        "windowFrames": window_frames,
        "trimStartFrame": trim_start,
        "trimEndFrame": trim_end,
        "rawActiveStartFrame": raw_start,
        "rawActiveEndFrame": raw_end,
        "trimmedBarCount": trimmed_bar_count,
        "stemWindows": stem_windows,
        "mixWindows": mix_windows,
        "bars": bars,
        "phrases": phrases,
        "boundaryCandidates": boundary_candidates,
        "suggestedSections": suggested_sections,
        "globalStems": global_stems,
        "globallySilentStems": globally_silent,
        "globalMix": global_mix,
    }


def range_metric(
    windows: Sequence[Dict[str, Any]], start_frame: int, end_frame: int
) -> Dict[str, float]:
    return weighted_metric(
        analysis_windows_in_range(windows, start_frame, end_frame)
    )


def range_similarity(
    analysis: Dict[str, Any],
    included_stems: Sequence[str],
    source_range_start: int,
    source_range_end: int,
    target_range_start: int,
    target_range_end: int,
) -> float:
    similarities: List[Tuple[float, float]] = []
    for stem_id in included_stems:
        source_metric = range_metric(
            analysis["stemWindows"][stem_id],
            source_range_start,
            source_range_end,
        )
        target_metric = range_metric(
            analysis["stemWindows"][stem_id],
            target_range_start,
            target_range_end,
        )
        source_silent = source_metric["rmsDbfs"] <= -90.0
        target_silent = target_metric["rmsDbfs"] <= -90.0
        if source_silent and target_silent:
            similarity = 1.0
            weight = 0.01
        elif source_silent != target_silent:
            similarity = 0.1
            weight = max(
                amplitude_from_db(source_metric["rmsDbfs"]),
                amplitude_from_db(target_metric["rmsDbfs"]),
            )
        else:
            rms_delta = abs(
                source_metric["rmsDbfs"] - target_metric["rmsDbfs"]
            )
            peak_delta = abs(
                source_metric["peakDbfs"] - target_metric["peakDbfs"]
            )
            activity_delta = abs(
                source_metric["activity"] - target_metric["activity"]
            )
            similarity = (
                0.6 * math.exp(-rms_delta / 18.0)
                + 0.25 * math.exp(-peak_delta / 24.0)
                + 0.15 * (1.0 - activity_delta)
            )
            weight = max(
                0.01,
                amplitude_from_db(source_metric["rmsDbfs"])
                + amplitude_from_db(target_metric["rmsDbfs"]),
            )
        similarities.append((similarity, weight))
    if not similarities:
        return 0.0
    total_weight = sum(weight for _, weight in similarities)
    return round_float(
        sum(similarity * weight for similarity, weight in similarities)
        / total_weight,
        4,
    )


def edge_similarity(
    analysis: Dict[str, Any],
    included_stems: Sequence[str],
    source_start: int,
    source_end: int,
    target_start: int,
    target_end: int,
    crossfade_seconds: float,
) -> float:
    edge_frames = max(
        analysis["windowFrames"],
        int(round(crossfade_seconds * analysis["sampleRate"])),
    )
    return range_similarity(
        analysis,
        included_stems,
        max(source_start, source_end - edge_frames),
        source_end,
        target_start,
        min(target_end, target_start + edge_frames),
    )


def section_energy(
    analysis: Dict[str, Any], start_frame: int, end_frame: int
) -> float:
    weighted = 0.0
    seconds = 0.0
    for bar in analysis["bars"]:
        bar_start = int(round(float(bar["startSeconds"]) * analysis["sampleRate"]))
        bar_end = bar_start + int(
            round(float(bar["durationSeconds"]) * analysis["sampleRate"])
        )
        overlap = max(0, min(end_frame, bar_end) - max(start_frame, bar_start))
        if overlap:
            weighted += float(bar["energy"]) * overlap
            seconds += overlap
    return round_float(weighted / max(1.0, seconds), 4)


def prepare_retrospective_cue(
    config: Dict[str, Any],
    analysis: Dict[str, Any],
) -> Tuple[Optional[Dict[str, Any]], Optional[Dict[str, Any]]]:
    configured = config.get("retrospectiveCue")
    if configured is None:
        return None, None
    start_frame = (
        analysis["gridOriginFrame"]
        + int(configured["startBar"]) * analysis["barFrames"]
    )
    end_frame = start_frame + int(configured["barCount"]) * analysis["barFrames"]
    if start_frame < analysis["trimStartFrame"]:
        raise PipelineError(
            "retrospectiveCue starts before the detected playable range "
            "(bar {})".format(
                analysis["trimStartFrame"] / analysis["barFrames"]
            )
        )
    if end_frame > analysis["trimEndFrame"]:
        raise PipelineError(
            "retrospectiveCue ends at bar {}, after the detected playable end "
            "at bar {:.6f}".format(
                configured["startBar"] + configured["barCount"],
                (
                    analysis["trimEndFrame"] - analysis["gridOriginFrame"]
                )
                / analysis["barFrames"],
            )
        )
    duration_seconds = (end_frame - start_frame) / analysis["sampleRate"]
    output = {
        "id": configured["id"],
        "startBar": configured["startBar"],
        "barCount": configured["barCount"],
        "anchorBar": configured["anchorBar"],
        "durationSeconds": round_float(duration_seconds),
        "files": {},
    }
    internal = {
        "startFrame": start_frame,
        "endFrame": end_frame,
        "stems": list(configured["stems"]),
    }
    return output, internal


def prepare_straight_through_cue(
    config: Dict[str, Any],
    analysis: Dict[str, Any],
) -> Tuple[Optional[Dict[str, Any]], Optional[Dict[str, Any]]]:
    configured = config.get("straightThroughCue")
    if configured is None:
        return None, None
    start_frame = (
        analysis["gridOriginFrame"]
        + int(configured["startBar"]) * analysis["barFrames"]
    )
    end_frame = start_frame + int(configured["barCount"]) * analysis["barFrames"]
    if start_frame < analysis["trimStartFrame"]:
        raise PipelineError(
            "straightThroughCue starts before the detected playable range "
            "(bar {})".format(
                analysis["trimStartFrame"] / analysis["barFrames"]
            )
        )
    if end_frame > analysis["trimEndFrame"]:
        raise PipelineError(
            "straightThroughCue ends at bar {}, after the detected playable end "
            "at bar {:.6f}".format(
                configured["startBar"] + configured["barCount"],
                (
                    analysis["trimEndFrame"] - analysis["gridOriginFrame"]
                )
                / analysis["barFrames"],
            )
        )
    duration_seconds = (end_frame - start_frame) / analysis["sampleRate"]
    output = {
        "id": configured["id"],
        "startBar": configured["startBar"],
        "barCount": configured["barCount"],
        "durationSeconds": round_float(duration_seconds),
        "file": "",
    }
    internal = {
        "startFrame": start_frame,
        "endFrame": end_frame,
        "stems": list(configured["stems"]),
    }
    return output, internal


def validate_straight_through_cue_mix(
    paths: Dict[str, Path],
    config: Dict[str, Any],
    cue: Optional[Dict[str, Any]],
    internal: Optional[Dict[str, Any]],
) -> None:
    if cue is None or internal is None:
        return
    metrics = render_straight_through_mix(
        paths,
        config["stems"],
        internal["stems"],
        internal["startFrame"],
        internal["endFrame"],
        config["masterGainDb"],
    )
    internal["mixHeadroom"] = metrics
    target = config["analysis"]["targetPeakDbfs"]
    if metrics["postMasterPeakDbfs"] > target:
        raise PipelineError(
            "straightThroughCue {} mix peak is {:.2f} dBFS after the runtime "
            "pack master, above target {:.2f}; lower a selected stem gain or "
            "the pack master".format(
                cue["id"], metrics["postMasterPeakDbfs"], target
            )
        )


def prepare_sections(
    config: Dict[str, Any], analysis: Dict[str, Any], warnings: List[str]
) -> Tuple[List[Dict[str, Any]], Dict[str, Dict[str, Any]]]:
    if not config["sections"]:
        return [], {}
    bar_frames = analysis["barFrames"]
    total_frames = analysis["totalFrames"]
    selected: List[Dict[str, Any]] = []
    internal: Dict[str, Dict[str, Any]] = {}
    for section in config["sections"]:
        start_frame = (
            analysis["gridOriginFrame"] + int(section["startBar"]) * bar_frames
        )
        if section["barCount"] == "to-end":
            end_frame = analysis["trimEndFrame"]
        else:
            end_frame = start_frame + int(section["barCount"]) * bar_frames
        if start_frame < analysis["trimStartFrame"]:
            raise PipelineError(
                "section {} starts before the detected playable range (bar {})".format(
                    section["id"], analysis["trimStartFrame"] / bar_frames
                )
            )
        if end_frame > analysis["trimEndFrame"]:
            raise PipelineError(
                "section {} ends at bar {}, after the detected playable end "
                "at bar {:.6f}".format(
                    section["id"],
                    end_frame / bar_frames,
                    analysis["trimEndFrame"] / bar_frames,
                )
            )
        if end_frame <= start_frame:
            raise PipelineError("section {} has no audio frames".format(section["id"]))
        included: List[str] = []
        omitted: List[str] = []
        metrics: Dict[str, Dict[str, float]] = {}
        for stem in config["stems"]:
            metric = range_metric(
                analysis["stemWindows"][stem["id"]], start_frame, end_frame
            )
            metrics[stem["id"]] = metric
            silent = (
                metric["rmsDbfs"] <= config["analysis"]["silenceRmsDbfs"]
                and metric["peakDbfs"] <= config["analysis"]["silencePeakDbfs"]
            )
            if silent:
                omitted.append(stem["id"])
            else:
                included.append(stem["id"])
        if not included:
            raise PipelineError(
                "section {} is analytically silent across every stem".format(
                    section["id"]
                )
            )
        duration_seconds = (end_frame - start_frame) / analysis["sampleRate"]
        bar_count = (end_frame - start_frame) / bar_frames
        output: Dict[str, Any] = {
            "id": section["id"],
            "label": section["label"],
            "classification": section["classification"],
            "role": section["role"],
            "energy": section_energy(analysis, start_frame, end_frame),
            "startBar": section["startBar"],
            "startSeconds": round_float(
                (start_frame - analysis["gridOriginFrame"])
                / analysis["sampleRate"]
            ),
            "barCount": round_float(bar_count, 6),
            "durationSeconds": round_float(duration_seconds),
            "loopable": section["loop"] is not None,
            "files": {},
        }
        if "repeat" in section:
            output["repeat"] = section["repeat"]
        if "cooldownSeconds" in section:
            if section["cooldownSeconds"] < duration_seconds:
                raise PipelineError(
                    "section {} cooldownSeconds must be at least its {:.3f}s "
                    "cue duration".format(section["id"], duration_seconds)
                )
            output["cooldownSeconds"] = section["cooldownSeconds"]
        if "stemGainsDb" in section:
            output["stemGainsDb"] = section["stemGainsDb"]
        if section["loop"] is not None:
            if abs(bar_count - round(bar_count)) > 1e-9:
                raise PipelineError(
                    "loopable section {} must contain complete musical bars".format(
                        section["id"]
                    )
                )
            if section["loop"]["crossfadeSeconds"] * 2 >= duration_seconds:
                raise PipelineError(
                    "section {} loop crossfade is too long for the section".format(
                        section["id"]
                    )
                )
            continuation_frames = int(
                round(
                    section["loop"]["crossfadeSeconds"]
                    * analysis["sampleRate"]
                )
            )
            if end_frame + continuation_frames > analysis["sourceEndFrame"]:
                raise PipelineError(
                    "loopable section {} needs {} source-continuation frames "
                    "after its end; choose an earlier boundary or make it "
                    "non-looping".format(section["id"], continuation_frames)
                )
            similarity = edge_similarity(
                analysis,
                included,
                start_frame,
                end_frame,
                start_frame,
                end_frame,
                section["loop"]["crossfadeSeconds"],
            )
            if similarity < config["analysis"]["minimumBoundarySimilarity"]:
                add_warning(
                    warnings,
                    "section {} raw loop boundary similarity {:.3f} is below "
                    "{:.3f}; the compiler will render and validate an identical "
                    "continuation-to-head crossfade across all included stems".format(
                        section["id"],
                        similarity,
                        config["analysis"]["minimumBoundarySimilarity"],
                    )
                )
            output["loop"] = {
                "boundarySimilarity": 0.0,
                "sourceBoundarySimilarity": similarity,
                "crossfadeSeconds": section["loop"]["crossfadeSeconds"],
                "strategy": section["loop"]["strategy"],
                "rendered": True,
                "approvalStatus": section["loop"]["approval"],
                "auditionRequired": section["loop"]["approval"] != "auditioned",
            }
        selected.append(output)
        internal[section["id"]] = {
            "startFrame": start_frame,
            "endFrame": end_frame,
            "includedStems": included,
            "omittedStems": omitted,
            "stemMetrics": metrics,
            "sampleRate": analysis["sampleRate"],
            "config": section,
            "output": output,
        }
    return selected, internal


def adaptive_transition_wait_bars(
    source: Dict[str, Any], transition: Dict[str, Any]
) -> float:
    if transition["timing"] == "next-quantum":
        return float(transition["quantizeBars"])
    return float(source["barCount"])


def adaptive_route_destination_allowed(
    section: Dict[str, Any],
    origin_classification: str,
    target_classification: str,
) -> bool:
    if section["role"] == "stinger":
        return False
    if target_classification == "resolve":
        return section["role"] != "stinger"
    if section["role"] == "resolve":
        return False
    ranks = {
        classification: index
        for index, classification in enumerate(GAMEPLAY_CLASSIFICATIONS)
    }
    lower = min(ranks[origin_classification], ranks[target_classification])
    upper = max(ranks[origin_classification], ranks[target_classification])
    rank = ranks[section["classification"]]
    return lower <= rank <= upper


def lowest_latency_adaptive_route(
    source_id: str,
    target_classification: str,
    sections: Dict[str, Dict[str, Any]],
    transitions: Sequence[Dict[str, Any]],
) -> Dict[str, Any]:
    outputs = {
        section_id: internal["output"]
        for section_id, internal in sections.items()
    }
    source = outputs[source_id]
    if target_classification == "resolve":
        targets = {
            section_id
            for section_id, section in outputs.items()
            if section["role"] == "resolve"
        }
    else:
        targets = {
            section_id
            for section_id, section in outputs.items()
            if section["role"] == "hold"
            and section["classification"] == target_classification
        }
    adjacency: Dict[str, List[Dict[str, Any]]] = {
        section_id: [] for section_id in outputs
    }
    for transition in transitions:
        destination = outputs[transition["to"]]
        if adaptive_route_destination_allowed(
            destination,
            source["classification"],
            target_classification,
        ):
            adjacency[transition["from"]].append(transition)

    start_path = (source_id,)
    pending: List[Tuple[float, int, Tuple[str, ...], str]] = [
        (0.0, 0, start_path, source_id)
    ]
    best: Dict[str, Tuple[float, int, Tuple[str, ...]]] = {
        source_id: (0.0, 0, start_path)
    }
    while pending:
        cost, hops, path, current = heapq.heappop(pending)
        if best.get(current) != (cost, hops, path):
            continue
        if current in targets:
            return {
                "from": source_id,
                "toClassification": target_classification,
                "hops": hops,
                "worstCaseBars": round_float(cost, 6),
                "path": list(path),
            }
        for transition in sorted(
            adjacency[current],
            key=lambda item: (-item["weight"], item["to"]),
        ):
            destination_id = transition["to"]
            next_cost = cost + adaptive_transition_wait_bars(
                outputs[current], transition
            )
            next_path = path + (destination_id,)
            candidate = (next_cost, hops + 1, next_path)
            if candidate >= best.get(
                destination_id,
                (float("inf"), sys.maxsize, tuple()),
            ):
                continue
            best[destination_id] = candidate
            heapq.heappush(
                pending,
                (next_cost, hops + 1, next_path, destination_id),
            )
    raise PipelineError(
        "adaptive route from {} to {} is unreachable without using a "
        "stinger".format(source_id, target_classification)
    )


def validate_adaptive_routes(
    config: Dict[str, Any],
    sections: Dict[str, Dict[str, Any]],
    transitions: Sequence[Dict[str, Any]],
) -> Dict[str, Any]:
    outputs = {
        section_id: internal["output"]
        for section_id, internal in sections.items()
    }
    missing_holds = [
        classification
        for classification in GAMEPLAY_CLASSIFICATIONS
        if not any(
            section["role"] == "hold"
            and section["classification"] == classification
            for section in outputs.values()
        )
    ]
    if missing_holds:
        raise PipelineError(
            "adaptive graph has no hold for gameplay state{}: {}".format(
                "" if len(missing_holds) == 1 else "s",
                ", ".join(missing_holds),
            )
        )

    outgoing: Dict[str, List[Dict[str, Any]]] = {
        section_id: [] for section_id in outputs
    }
    for transition in transitions:
        outgoing[transition["from"]].append(transition)
    for section_id, section in outputs.items():
        if section["role"] == "resolve":
            continue
        same_state = [
            transition
            for transition in outgoing[section_id]
            if outputs[transition["to"]]["classification"]
            == section["classification"]
            and outputs[transition["to"]]["role"] != "stinger"
        ]
        if section["role"] == "hold":
            compatible = [
                transition
                for transition in same_state
                if transition["timing"] == "next-quantum"
            ]
            invalid = [
                transition
                for transition in same_state
                if transition["timing"] != "next-quantum"
            ]
        else:
            compatible = [
                transition
                for transition in same_state
                if transition["timing"] == "section-end"
            ]
            invalid = [
                transition
                for transition in same_state
                if transition["timing"] != "section-end"
            ]
        if invalid:
            raise PipelineError(
                "section {} has an incompatible same-state transition".format(
                    section_id
                )
            )
        # A reviewed hold can continue through its intrinsic rendered loop.
        # Finite cues exhaust their source buffer and therefore need an
        # ordinary, non-stinger successor in the same gameplay state.
        if section["role"] != "hold" and not compatible:
            raise PipelineError(
                "section {} has no executable non-stinger same-state "
                "continuation".format(section_id)
            )

    gameplay_routes: List[Dict[str, Any]] = []
    for section_id, section in outputs.items():
        if section["role"] != "hold":
            continue
        for target in GAMEPLAY_CLASSIFICATIONS:
            if target == section["classification"]:
                continue
            route = lowest_latency_adaptive_route(
                section_id, target, sections, transitions
            )
            if (
                route["worstCaseBars"]
                > config["adaptiveLatencyBudgetBars"]["gameplay"] + 1e-9
            ):
                raise PipelineError(
                    "adaptive route {} -> {} has worst-case latency {} bars "
                    "via {}, exceeding gameplay budget {} bars".format(
                        section_id,
                        target,
                        route["worstCaseBars"],
                        " -> ".join(route["path"]),
                        config["adaptiveLatencyBudgetBars"]["gameplay"],
                    )
                )
            gameplay_routes.append(route)

    resolve_routes: List[Dict[str, Any]] = []
    for section_id, section in outputs.items():
        if section["role"] == "resolve":
            continue
        route = lowest_latency_adaptive_route(
            section_id, "resolve", sections, transitions
        )
        if (
            route["worstCaseBars"]
            > config["adaptiveLatencyBudgetBars"]["resolve"] + 1e-9
        ):
            raise PipelineError(
                "adaptive route {} -> resolve has worst-case latency {} bars "
                "via {}, exceeding resolve budget {} bars".format(
                    section_id,
                    route["worstCaseBars"],
                    " -> ".join(route["path"]),
                    config["adaptiveLatencyBudgetBars"]["resolve"],
                )
            )
        resolve_routes.append(route)
    return {
        "budgetBars": config["adaptiveLatencyBudgetBars"],
        "gameplay": gameplay_routes,
        "resolve": resolve_routes,
    }


def prepare_transitions(
    config: Dict[str, Any],
    analysis: Dict[str, Any],
    sections: Dict[str, Dict[str, Any]],
    warnings: List[str],
    paths: Dict[str, Path],
) -> Tuple[List[Dict[str, Any]], Dict[str, Any]]:
    if not config["sections"]:
        return [], {
            "budgetBars": config["adaptiveLatencyBudgetBars"],
            "gameplay": [],
            "resolve": [],
        }
    if not config["transitions"]:
        raise PipelineError(
            "configured sections require an explicit directed transition graph"
        )
    output: List[Dict[str, Any]] = []
    transition_headroom: List[Dict[str, Any]] = []
    outgoing = {section_id: 0 for section_id in sections}
    adjacency = {section_id: [] for section_id in sections}
    for transition in config["transitions"]:
        source = sections[transition["from"]]
        target = sections[transition["to"]]
        common_stems = sorted(
            set(source["includedStems"]) | set(target["includedStems"])
        )
        crossfade_seconds = (
            transition["crossfadeBars"]
            * analysis["barFrames"]
            / analysis["sampleRate"]
        )
        comparison_seconds = max(
            crossfade_seconds,
            analysis["windowFrames"] / analysis["sampleRate"],
        )
        comparison_frames = int(
            round(comparison_seconds * analysis["sampleRate"])
        )
        compatibility_samples: List[float] = []
        if transition["timing"] == "next-quantum":
            quantum_frames = (
                transition["quantizeBars"] * analysis["barFrames"]
            )
            boundary = source["startFrame"] + quantum_frames
            while boundary <= source["endFrame"]:
                # A held loop wraps at its end. Runtime crossfades from the
                # post-boundary source audio, so the end boundary samples the
                # rendered loop's beginning rather than its preceding tail.
                if boundary == source["endFrame"] and source["output"]["loopable"]:
                    source_range_start = source["startFrame"]
                    source_range_end = min(
                        source["endFrame"],
                        source_range_start + comparison_frames,
                    )
                elif boundary == source["endFrame"]:
                    source_range_start = max(
                        source["startFrame"],
                        source["endFrame"] - comparison_frames,
                    )
                    source_range_end = source["endFrame"]
                else:
                    source_range_start = boundary
                    source_range_end = min(
                        source["endFrame"],
                        source_range_start + comparison_frames,
                    )
                compatibility_samples.append(
                    range_similarity(
                        analysis,
                        common_stems,
                        source_range_start,
                        source_range_end,
                        target["startFrame"],
                        min(
                            target["endFrame"],
                            target["startFrame"] + comparison_frames,
                        ),
                    )
                )
                boundary += quantum_frames
        else:
            compatibility_samples.append(
                edge_similarity(
                    analysis,
                    common_stems,
                    source["startFrame"],
                    source["endFrame"],
                    target["startFrame"],
                    target["endFrame"],
                    comparison_seconds,
                )
            )
        if not compatibility_samples:
            raise PipelineError(
                "transition {} -> {} has no eligible quantized cut point".format(
                    transition["from"], transition["to"]
                )
            )
        compatibility = min(compatibility_samples)
        minimum = config["analysis"]["minimumTransitionSimilarity"]
        if compatibility < minimum:
            message = (
                "transition {} -> {} compatibility {:.3f} is below "
                "configured minimum {:.3f}".format(
                    transition["from"],
                    transition["to"],
                    compatibility,
                    minimum,
                )
            )
            if transition["allowLowSimilarity"]:
                add_warning(
                    warnings,
                    message
                    + "; retained because allowLowSimilarity is explicitly configured"
                )
            else:
                raise PipelineError(
                    message
                    + "; change the graph/crossfade or explicitly review the exception"
                )
        headroom = validate_transition_mix_headroom(
            paths=paths,
            config=config,
            analysis=analysis,
            source=source,
            target=target,
            transition=transition,
        )
        transition_headroom.append(
            {
                "from": transition["from"],
                "to": transition["to"],
                **headroom,
            }
        )
        output.append(
            {
                "from": transition["from"],
                "to": transition["to"],
                "timing": transition["timing"],
                "quantizeBars": transition["quantizeBars"],
                "crossfadeBars": transition["crossfadeBars"],
                "weight": transition["weight"],
                "compatibility": compatibility,
                "compatibilityRange": {
                    "minimum": compatibility,
                    "maximum": max(compatibility_samples),
                    "cutPoints": len(compatibility_samples),
                },
            }
        )
        outgoing[transition["from"]] += 1
        adjacency[transition["from"]].append(transition["to"])
    analysis["transitionHeadroom"] = transition_headroom
    dead_ends = sorted(section_id for section_id, count in outgoing.items() if count == 0)
    if dead_ends:
        raise PipelineError(
            "transition graph has dead-end section{}: {}".format(
                "" if len(dead_ends) == 1 else "s", ", ".join(dead_ends)
            )
        )
    reached = set()
    pending = [config["entrySection"]]
    while pending:
        current = pending.pop()
        if current in reached:
            continue
        reached.add(current)
        pending.extend(adjacency[current])
    unreachable = sorted(set(sections) - reached)
    if unreachable:
        raise PipelineError(
            "transition graph cannot reach section{} from entrySection: {}".format(
                "" if len(unreachable) == 1 else "s", ", ".join(unreachable)
            )
        )
    routing = validate_adaptive_routes(config, sections, output)
    return output, routing


def encoder_version(executable: str, name: str) -> str:
    command = [executable, "-version"] if name == "ffmpeg" else [executable, "-h"]
    try:
        result = subprocess.run(
            command,
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            timeout=10,
        )
    except (OSError, subprocess.SubprocessError):
        return "unknown"
    lines = [line.strip() for line in result.stdout.splitlines() if line.strip()]
    if name == "afconvert":
        for index, line in enumerate(lines):
            if line.startswith("Version:"):
                return line.split(":", 1)[1].strip()
            if line == "Audio File Convert" and index + 1 < len(lines):
                return lines[index + 1].replace("Version:", "").strip()
    return lines[0][:160] if lines else "unknown"


def select_encoder(requested: str, bitrate_kbps: int) -> Dict[str, Any]:
    choices = [requested] if requested != "auto" else ["ffmpeg", "afconvert"]
    for name in choices:
        executable = shutil.which(name)
        if executable:
            return {
                "name": name,
                "executable": executable,
                "version": encoder_version(executable, name),
                "codec": "aac",
                "bitrateKbps": bitrate_kbps,
            }
    if requested == "auto":
        raise PipelineError(
            "no AAC encoder found: install ffmpeg, or run on macOS with afconvert"
        )
    raise PipelineError("{} was requested but is not on PATH".format(requested))


def crossfade_weights(curve: str, progress: float) -> Tuple[float, float]:
    if curve == "equal-power":
        return (
            math.cos(progress * math.pi / 2.0),
            math.sin(progress * math.pi / 2.0),
        )
    if curve == "linear":
        return (1.0 - progress, progress)
    raise PipelineError("unsupported head-crossfade curve: {}".format(curve))


def read_pcm_frames(
    reader: wave.Wave_read,
    start_frame: int,
    frame_count: int,
    stem_id: str,
) -> Sequence[int]:
    reader.setpos(start_frame)
    samples = decode_pcm(
        reader.readframes(frame_count),
        reader.getsampwidth(),
    )
    expected_samples = frame_count * reader.getnchannels()
    if len(samples) != expected_samples:
        raise PipelineError(
            "stem {} returned {} samples at frame {}; expected {}".format(
                stem_id,
                len(samples),
                start_frame,
                expected_samples,
            )
        )
    return samples


def rendered_stem_range(
    reader: wave.Wave_read,
    stem_id: str,
    internal: Dict[str, Any],
    offset_frames: int,
    frame_count: int,
) -> Sequence[float]:
    section_frames = internal["endFrame"] - internal["startFrame"]
    if (
        offset_frames < 0
        or frame_count < 0
        or offset_frames + frame_count > section_frames
    ):
        raise PipelineError(
            "internal error: requested frames outside section {}".format(
                internal["config"]["id"]
            )
        )
    if frame_count == 0:
        return []

    loop = internal["output"].get("loop")
    fade_frames = int(loop.get("crossfadeFrames", 0)) if loop else 0
    curve = str(loop.get("curve", "equal-power")) if loop else "equal-power"
    blended_frames = max(
        0,
        min(frame_count, fade_frames - offset_frames),
    )
    output: List[float] = []
    channels = reader.getnchannels()
    if blended_frames:
        head = read_pcm_frames(
            reader,
            internal["startFrame"] + offset_frames,
            blended_frames,
            stem_id,
        )
        continuation = read_pcm_frames(
            reader,
            internal["endFrame"] + offset_frames,
            blended_frames,
            stem_id,
        )
        for local_frame in range(blended_frames):
            rendered_frame = offset_frames + local_frame
            progress = rendered_frame / (fade_frames - 1)
            continuation_gain, head_gain = crossfade_weights(curve, progress)
            sample_start = local_frame * channels
            for channel in range(channels):
                sample_index = sample_start + channel
                output.append(
                    continuation[sample_index] * continuation_gain
                    + head[sample_index] * head_gain
                )

    raw_frames = frame_count - blended_frames
    if raw_frames:
        output.extend(
            float(value)
            for value in read_pcm_frames(
                reader,
                internal["startFrame"] + offset_frames + blended_frames,
                raw_frames,
                stem_id,
            )
        )
    return output


def rendered_section_mix_range(
    paths: Dict[str, Path],
    config: Dict[str, Any],
    internal: Dict[str, Any],
    offset_frames: int,
    frame_count: int,
) -> Tuple[Sequence[float], int, float]:
    included = set(internal["includedStems"])
    section_gains = internal["config"].get("stemGainsDb", {})
    mixed: Optional[List[float]] = None
    channels: Optional[int] = None
    full_scale: Optional[float] = None
    for stem in config["stems"]:
        stem_id = stem["id"]
        if stem_id not in included:
            continue
        try:
            with wave.open(str(paths[stem_id]), "rb") as reader:
                if channels is None:
                    channels = reader.getnchannels()
                    full_scale = float(1 << (reader.getsampwidth() * 8 - 1))
                samples = rendered_stem_range(
                    reader,
                    stem_id,
                    internal,
                    offset_frames,
                    frame_count,
                )
        except (OSError, EOFError, wave.Error) as error:
            raise PipelineError(
                "failed measuring rendered section {} stem {}: {}".format(
                    internal["config"]["id"], stem_id, error
                )
            ) from error
        if mixed is None:
            mixed = [0.0] * len(samples)
        elif len(samples) != len(mixed):
            raise PipelineError(
                "internal error: rendered stems lost alignment in section {}".format(
                    internal["config"]["id"]
                )
            )
        gain = 10.0 ** (
            (
                float(stem["gainDb"])
                + float(section_gains.get(stem_id, 0.0))
            )
            / 20.0
        )
        for index, value in enumerate(samples):
            mixed[index] += float(value) * gain
    if mixed is None or channels is None or full_scale is None:
        raise PipelineError(
            "cannot measure section {} with no included stems".format(
                internal["config"]["id"]
            )
        )
    return mixed, channels, full_scale


def measure_section_mix_peak(
    paths: Dict[str, Path],
    config: Dict[str, Any],
    internal: Dict[str, Any],
) -> Dict[str, float]:
    section_frames = internal["endFrame"] - internal["startFrame"]
    peak = 0.0
    full_scale: Optional[float] = None
    offset = 0
    while offset < section_frames:
        frame_count = min(65536, section_frames - offset)
        mixed, _, current_full_scale = rendered_section_mix_range(
            paths,
            config,
            internal,
            offset,
            frame_count,
        )
        full_scale = current_full_scale
        peak = max(peak, max(abs(value) for value in mixed))
        offset += frame_count
    if full_scale is None:
        raise PipelineError("internal error: section peak has no PCM scale")
    raw_peak_dbfs = dbfs(peak, full_scale)
    return {
        "rawPeakDbfs": round_float(raw_peak_dbfs, 3),
        "postMasterPeakDbfs": round_float(
            raw_peak_dbfs + config["masterGainDb"], 3
        ),
    }


def prepare_section_mix_headroom(
    paths: Dict[str, Path],
    config: Dict[str, Any],
    sections: Dict[str, Dict[str, Any]],
    warnings: List[str],
) -> None:
    target = config["analysis"]["targetPeakDbfs"]
    for internal in sections.values():
        section = internal["output"]
        if section["loopable"]:
            crossfade_curve, head_mix = choose_head_crossfade_curve(
                paths, config, internal
            )
            section["loop"]["curve"] = crossfade_curve
            section["loop"]["crossfadeFrames"] = head_mix[
                "crossfadeFrames"
            ]
            section["loop"]["packHeadPeakDbfs"] = head_mix[
                "headroom"
            ]["postMasterPeakDbfs"]
            section["loop"]["headroomTreatment"] = (
                "none"
                if crossfade_curve == "equal-power"
                else "linear-blend-fallback"
            )
            internal["crossfadeCurve"] = crossfade_curve
            if crossfade_curve == "linear":
                add_warning(
                    warnings,
                    "section {} uses a linear head crossfade because "
                    "the equal-power blend exceeded pack headroom".format(
                        section["id"]
                    ),
                )
        else:
            internal["crossfadeCurve"] = "equal-power"

        metrics = measure_section_mix_peak(paths, config, internal)
        internal["mixHeadroom"] = metrics
        if metrics["postMasterPeakDbfs"] > target:
            raise PipelineError(
                "section {} mix peak is {:.2f} dBFS after the pack master, "
                "above target {:.2f}; lower its stemGainsDb or the pack "
                "master".format(
                    section["id"],
                    metrics["postMasterPeakDbfs"],
                    target,
                )
            )


def transition_source_offsets(
    source: Dict[str, Any],
    transition: Dict[str, Any],
    crossfade_frames: int,
    bar_frames: int,
) -> List[int]:
    section_frames = source["endFrame"] - source["startFrame"]
    if transition["timing"] == "section-end":
        return [max(0, section_frames - crossfade_frames)]

    quantum_frames = int(transition["quantizeBars"]) * bar_frames
    offsets: List[int] = []
    boundary = quantum_frames
    while boundary <= section_frames:
        if boundary == section_frames:
            offset = (
                0
                if source["output"]["loopable"]
                else max(0, section_frames - crossfade_frames)
            )
        else:
            offset = min(
                boundary,
                max(0, section_frames - crossfade_frames),
            )
        if offset not in offsets:
            offsets.append(offset)
        boundary += quantum_frames
    return offsets


def validate_transition_mix_headroom(
    paths: Dict[str, Path],
    config: Dict[str, Any],
    analysis: Dict[str, Any],
    source: Dict[str, Any],
    target: Dict[str, Any],
    transition: Dict[str, Any],
) -> Dict[str, Any]:
    authored_frames = int(
        round(
            transition["crossfadeBars"]
            * analysis["barFrames"]
        )
    )
    source_frames = source["endFrame"] - source["startFrame"]
    target_frames = target["endFrame"] - target["startFrame"]
    crossfade_frames = min(
        authored_frames,
        source_frames // 2,
        target_frames // 2,
    )
    if crossfade_frames <= 0:
        return {
            "crossfadeFrames": 0,
            "cutPoints": 0,
            "rawPeakDbfs": None,
            "postMasterPeakDbfs": None,
        }
    if crossfade_frames < 2:
        raise PipelineError(
            "transition {} -> {} crossfade must contain at least two PCM "
            "frames".format(transition["from"], transition["to"])
        )
    offsets = transition_source_offsets(
        source,
        transition,
        crossfade_frames,
        analysis["barFrames"],
    )
    if not offsets:
        raise PipelineError(
            "transition {} -> {} has no eligible headroom cut point".format(
                transition["from"], transition["to"]
            )
        )

    target_mix, target_channels, full_scale = rendered_section_mix_range(
        paths,
        config,
        target,
        0,
        crossfade_frames,
    )
    peak = 0.0
    peak_offset = offsets[0]
    for offset in offsets:
        source_mix, source_channels, source_full_scale = (
            rendered_section_mix_range(
                paths,
                config,
                source,
                offset,
                crossfade_frames,
            )
        )
        if (
            source_channels != target_channels
            or source_full_scale != full_scale
            or len(source_mix) != len(target_mix)
        ):
            raise PipelineError(
                "internal error: transition mix formats lost alignment"
            )
        current_peak = 0.0
        for frame_index in range(crossfade_frames):
            progress = frame_index / (crossfade_frames - 1)
            source_gain, target_gain = crossfade_weights(
                "equal-power", progress
            )
            sample_start = frame_index * target_channels
            for channel in range(target_channels):
                sample_index = sample_start + channel
                mixed = (
                    source_mix[sample_index] * source_gain
                    + target_mix[sample_index] * target_gain
                )
                current_peak = max(current_peak, abs(mixed))
        if current_peak > peak:
            peak = current_peak
            peak_offset = offset

    raw_peak_dbfs = dbfs(peak, full_scale)
    post_master = raw_peak_dbfs + config["masterGainDb"]
    target_peak = config["analysis"]["targetPeakDbfs"]
    metrics = {
        "crossfadeFrames": crossfade_frames,
        "cutPoints": len(offsets),
        "peakSourceOffsetFrames": peak_offset,
        "rawPeakDbfs": round_float(raw_peak_dbfs, 3),
        "postMasterPeakDbfs": round_float(post_master, 3),
    }
    if post_master > target_peak:
        raise PipelineError(
            "transition {} -> {} equal-power overlap peaks at {:.2f} dBFS "
            "after the pack master, above target {:.2f}; lower section stem "
            "gains, crossfadeBars, or the pack master".format(
                transition["from"],
                transition["to"],
                post_master,
                target_peak,
            )
        )
    return metrics


def measure_head_crossfade_mix(
    paths: Dict[str, Path],
    stems: Sequence[Dict[str, Any]],
    included_stems: Sequence[str],
    section_stem_gains: Dict[str, float],
    start_frame: int,
    end_frame: int,
    crossfade_frames: int,
    master_gain_db: float,
    curve: str,
) -> Dict[str, float]:
    included = set(included_stems)
    sources = []
    sample_width: Optional[int] = None
    channels: Optional[int] = None
    full_scale: Optional[float] = None
    try:
        for stem in stems:
            if stem["id"] not in included:
                continue
            reader = wave.open(str(paths[stem["id"]]), "rb")
            sources.append((stem, reader))
            if sample_width is None:
                sample_width = reader.getsampwidth()
                channels = reader.getnchannels()
                full_scale = float(1 << (sample_width * 8 - 1))
            reader.setpos(start_frame)
            head = decode_pcm(reader.readframes(crossfade_frames), reader.getsampwidth())
            reader.setpos(end_frame)
            continuation = decode_pcm(
                reader.readframes(crossfade_frames), reader.getsampwidth()
            )
            expected_samples = crossfade_frames * reader.getnchannels()
            if len(head) != expected_samples or len(continuation) != expected_samples:
                raise PipelineError(
                    "could not measure complete loop head for {}".format(stem["id"])
                )
            gain_db = float(stem["gainDb"]) + float(
                section_stem_gains.get(stem["id"], 0.0)
            )
            sources[-1] = (
                stem,
                reader,
                head,
                continuation,
                10.0 ** (gain_db / 20.0),
            )
        if not sources or sample_width is None or channels is None or full_scale is None:
            raise PipelineError("cannot measure a loop with no included stems")
        pack_peak = 0.0
        individual_peak = 0.0
        for frame_index in range(crossfade_frames):
            progress = frame_index / (crossfade_frames - 1)
            continuation_gain, head_gain = crossfade_weights(curve, progress)
            sample_start = frame_index * channels
            for channel in range(channels):
                sample_index = sample_start + channel
                pack_value = 0.0
                for _, _, head, continuation, gain in sources:
                    blended = (
                        continuation[sample_index] * continuation_gain
                        + head[sample_index] * head_gain
                    )
                    individual_peak = max(individual_peak, abs(blended))
                    pack_value += blended * gain
                pack_peak = max(pack_peak, abs(pack_value))
        raw_peak_dbfs = dbfs(pack_peak, full_scale)
        return {
            "rawPackPeakDbfs": round_float(raw_peak_dbfs, 3),
            "postMasterPeakDbfs": round_float(raw_peak_dbfs + master_gain_db, 3),
            "individualPeakDbfs": round_float(
                dbfs(individual_peak, full_scale), 3
            ),
        }
    finally:
        for source in sources:
            source[1].close()


def choose_head_crossfade_curve(
    paths: Dict[str, Path],
    config: Dict[str, Any],
    internal: Dict[str, Any],
) -> Tuple[str, Dict[str, Any]]:
    loop = internal["config"]["loop"]
    if loop is None:
        raise PipelineError("internal error: curve requested for non-loop section")
    crossfade_frames = int(
        round(loop["crossfadeSeconds"] * internal["sampleRate"])
    )
    section_stem_gains = internal["config"].get("stemGainsDb", {})
    attempts = {}
    for curve in ("equal-power", "linear"):
        metrics = measure_head_crossfade_mix(
            paths,
            config["stems"],
            internal["includedStems"],
            section_stem_gains,
            internal["startFrame"],
            internal["endFrame"],
            crossfade_frames,
            config["masterGainDb"],
            curve,
        )
        attempts[curve] = metrics
        if (
            metrics["individualPeakDbfs"] <= 0.0
            and metrics["postMasterPeakDbfs"]
            <= config["analysis"]["targetPeakDbfs"]
        ):
            return curve, {
                "curve": curve,
                "crossfadeFrames": crossfade_frames,
                "headroom": metrics,
                "attempts": attempts,
            }
    raise PipelineError(
        "section {} rendered loop head exceeds {:.2f} dBFS after the "
        "pack master with both equal-power and linear curves; lower the "
        "pack master or review the section".format(
            internal["config"]["id"], config["analysis"]["targetPeakDbfs"]
        )
    )


def write_wav_section(
    source_path: Path,
    destination: Path,
    start_frame: int,
    end_frame: int,
    head_crossfade_seconds: float = 0.0,
    continuation_end_frame: Optional[int] = None,
    crossfade_curve: str = "equal-power",
) -> Dict[str, Any]:
    expected_frames = end_frame - start_frame
    verification: Dict[str, Any] = {
        "boundarySimilarity": 0.0,
        "continuationFrames": 0,
    }
    try:
        with wave.open(str(source_path), "rb") as source:
            params = source.getparams()
            fade_frames = int(round(head_crossfade_seconds * params.framerate))
            if fade_frames and fade_frames < 2:
                raise PipelineError(
                    "rendered head crossfade for {} must contain at least "
                    "two PCM frames".format(source_path.name)
                )
            if fade_frames and fade_frames >= expected_frames:
                raise PipelineError(
                    "rendered head crossfade is too long for {}".format(
                        source_path.name
                    )
                )
            continuation_limit = (
                source.getnframes()
                if continuation_end_frame is None
                else min(source.getnframes(), continuation_end_frame)
            )
            if fade_frames and end_frame + fade_frames > continuation_limit:
                raise PipelineError(
                    "loop section ending at frame {} needs {} continuation "
                    "frames, but the configured source ends at {}. Choose an "
                    "earlier section end or mark it non-looping.".format(
                        end_frame, fade_frames, continuation_limit
                    )
                )
            blended_head = b""
            blend_peak = 0
            if fade_frames:
                source.setpos(start_frame)
                original_head = decode_pcm(
                    source.readframes(fade_frames), params.sampwidth
                )
                source.setpos(end_frame)
                continuation = decode_pcm(
                    source.readframes(fade_frames), params.sampwidth
                )
                expected_samples = fade_frames * params.nchannels
                if (
                    len(original_head) != expected_samples
                    or len(continuation) != expected_samples
                ):
                    raise PipelineError(
                        "could not read complete head/continuation audio from {}".format(
                            source_path.name
                        )
                    )
                sample_minimum = -(1 << (params.sampwidth * 8 - 1))
                sample_maximum = (1 << (params.sampwidth * 8 - 1)) - 1
                blended_samples: List[int] = []
                for frame_index in range(fade_frames):
                    progress = frame_index / (fade_frames - 1)
                    continuation_gain, head_gain = crossfade_weights(
                        crossfade_curve, progress
                    )
                    sample_start = frame_index * params.nchannels
                    for channel in range(params.nchannels):
                        sample_index = sample_start + channel
                        value = int(
                            round(
                                continuation[sample_index] * continuation_gain
                                + original_head[sample_index] * head_gain
                            )
                        )
                        if value < sample_minimum or value > sample_maximum:
                            raise PipelineError(
                                "rendered head crossfade clips {} at frame {}; "
                                "adjust the reviewed section/crossfade".format(
                                    source_path.name, frame_index
                                )
                            )
                        blend_peak = max(blend_peak, abs(value))
                        blended_samples.append(value)
                blended_head = encode_pcm(blended_samples, params.sampwidth)
            with wave.open(str(destination), "wb") as target:
                target.setnchannels(params.nchannels)
                target.setsampwidth(params.sampwidth)
                target.setframerate(params.framerate)
                written = fade_frames if fade_frames else 0
                if fade_frames:
                    target.writeframesraw(blended_head)
                    source.setpos(start_frame + fade_frames)
                else:
                    source.setpos(start_frame)
                remaining = expected_frames - written
                while remaining:
                    data = source.readframes(min(remaining, 65536))
                    if not data:
                        break
                    frame_bytes = params.nchannels * params.sampwidth
                    frames = len(data) // frame_bytes
                    target.writeframesraw(data)
                    written += frames
                    remaining -= frames
                target.writeframes(b"")
        if written != expected_frames:
            raise PipelineError(
                "section extraction from {} wrote {} frames; expected {}".format(
                    source_path.name, written, expected_frames
                )
            )
        if fade_frames:
            with wave.open(str(destination), "rb") as rendered, wave.open(
                str(source_path), "rb"
            ) as original:
                first = decode_pcm(rendered.readframes(1), params.sampwidth)
                rendered.setpos(expected_frames - 1)
                last = decode_pcm(rendered.readframes(1), params.sampwidth)
                original.setpos(end_frame - 1)
                expected_last = decode_pcm(
                    original.readframes(1), params.sampwidth
                )
                original.setpos(end_frame)
                expected_first = decode_pcm(
                    original.readframes(1), params.sampwidth
                )
            if list(first) != list(expected_first) or list(last) != list(expected_last):
                raise PipelineError(
                    "rendered loop seam for {} is not the verified natural "
                    "source continuation".format(source_path.name)
                )
            full_scale = float(1 << (params.sampwidth * 8 - 1))
            seam_jump = max(
                abs(float(first[channel]) - float(last[channel]))
                for channel in range(params.nchannels)
            )
            verification = {
                "boundarySimilarity": 1.0,
                "continuationFrames": fade_frames,
                "seamJumpDbfs": round_float(dbfs(seam_jump, full_scale), 3),
                "blendPeakDbfs": round_float(dbfs(blend_peak, full_scale), 3),
            }
    except (OSError, wave.Error) as error:
        raise PipelineError(
            "failed writing temporary WAV section from {}: {}".format(
                source_path, error
            )
        ) from error
    return verification


def render_straight_through_mix(
    paths: Dict[str, Path],
    stems: Sequence[Dict[str, Any]],
    included_stems: Sequence[str],
    start_frame: int,
    end_frame: int,
    master_gain_db: float,
    destination: Optional[Path] = None,
) -> Dict[str, float]:
    included = set(included_stems)
    selected = [stem for stem in stems if stem["id"] in included]
    if len(selected) != len(included):
        raise PipelineError(
            "cannot render straight-through cue with unknown stems"
        )
    if not selected:
        raise PipelineError(
            "cannot render straight-through cue with no stems"
        )
    frame_count = end_frame - start_frame
    if frame_count <= 0:
        raise PipelineError("straight-through cue has no source frames")

    readers: List[Tuple[Dict[str, Any], wave.Wave_read, float]] = []
    target: Optional[wave.Wave_write] = None
    channels: Optional[int] = None
    sample_width: Optional[int] = None
    sample_rate: Optional[int] = None
    full_scale: Optional[float] = None
    peak = 0.0
    try:
        for stem in selected:
            reader = wave.open(str(paths[stem["id"]]), "rb")
            current = (
                reader.getnchannels(),
                reader.getsampwidth(),
                reader.getframerate(),
            )
            if channels is None:
                channels, sample_width, sample_rate = current
                full_scale = float(1 << (sample_width * 8 - 1))
            elif current != (channels, sample_width, sample_rate):
                reader.close()
                raise PipelineError(
                    "straight-through cue stems lost PCM alignment"
                )
            if end_frame > reader.getnframes():
                reader.close()
                raise PipelineError(
                    "straight-through cue exceeds stem {} source frames".format(
                        stem["id"]
                    )
                )
            reader.setpos(start_frame)
            readers.append(
                (
                    stem,
                    reader,
                    10.0 ** (float(stem["gainDb"]) / 20.0),
                )
            )
        if (
            channels is None
            or sample_width is None
            or sample_rate is None
            or full_scale is None
        ):
            raise PipelineError(
                "straight-through cue has no readable PCM stems"
            )
        sample_minimum = -(1 << (sample_width * 8 - 1))
        sample_maximum = (1 << (sample_width * 8 - 1)) - 1
        if destination is not None:
            destination.parent.mkdir(parents=True, exist_ok=True)
            target = wave.open(str(destination), "wb")
            target.setnchannels(channels)
            target.setsampwidth(sample_width)
            target.setframerate(sample_rate)

        remaining = frame_count
        while remaining:
            requested = min(65536, remaining)
            mixed = [0.0] * (requested * channels)
            for stem, reader, gain in readers:
                samples = decode_pcm(
                    reader.readframes(requested), sample_width
                )
                if len(samples) != len(mixed):
                    raise PipelineError(
                        "straight-through cue stem {} ended early".format(
                            stem["id"]
                        )
                    )
                for index, value in enumerate(samples):
                    mixed[index] += float(value) * gain
            rendered: List[int] = []
            for value in mixed:
                peak = max(peak, abs(value))
                sample = int(round(value))
                if sample < sample_minimum or sample > sample_maximum:
                    raise PipelineError(
                        "straight-through cue mix clips before the runtime "
                        "master gain"
                    )
                rendered.append(sample)
            if target is not None:
                target.writeframesraw(encode_pcm(rendered, sample_width))
            remaining -= requested
        if target is not None:
            target.writeframes(b"")
        raw_peak = dbfs(peak, full_scale)
        return {
            "rawPeakDbfs": round_float(raw_peak, 3),
            "postMasterPeakDbfs": round_float(
                raw_peak + master_gain_db, 3
            ),
        }
    except (OSError, EOFError, wave.Error) as error:
        raise PipelineError(
            "failed rendering straight-through cue mix: {}".format(error)
        ) from error
    finally:
        if target is not None:
            target.close()
        for _, reader, _ in readers:
            reader.close()


def encode_m4a(
    encoder: Dict[str, Any], input_wav: Path, output_m4a: Path
) -> None:
    output_m4a.parent.mkdir(parents=True, exist_ok=True)
    if encoder["name"] == "ffmpeg":
        command = [
            encoder["executable"],
            "-nostdin",
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-fflags",
            "+bitexact",
            "-i",
            str(input_wav),
            "-map_metadata",
            "-1",
            "-vn",
            "-c:a",
            "aac",
            "-b:a",
            "{}k".format(encoder["bitrateKbps"]),
            "-flags:a",
            "+bitexact",
            "-movflags",
            "+faststart",
            str(output_m4a),
        ]
    else:
        command = [
            encoder["executable"],
            str(input_wav),
            "-o",
            str(output_m4a),
            "-f",
            "m4af",
            "-d",
            "aac",
            "-b",
            str(encoder["bitrateKbps"] * 1000),
            "-q",
            "127",
            "-s",
            "0",
            "--no-filler",
        ]
    result = subprocess.run(
        command,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    if result.returncode != 0:
        detail = (result.stderr or result.stdout).strip()
        raise PipelineError(
            "{} failed encoding {}: {}".format(
                encoder["name"], input_wav.name, detail or "exit {}".format(result.returncode)
            )
        )
    if not output_m4a.is_file() or output_m4a.stat().st_size < 256:
        raise PipelineError(
            "{} did not produce a valid-sized M4A for {}".format(
                encoder["name"], input_wav.name
            )
        )
    canonicalize_mp4_timestamps(output_m4a)


def canonicalize_mp4_timestamps(path: Path) -> None:
    """Zero non-semantic MP4 creation/modification times for reproducible bytes."""

    data = bytearray(path.read_bytes())
    timestamp_boxes = {b"mvhd", b"tkhd", b"mdhd"}
    container_boxes = {
        b"moov",
        b"trak",
        b"mdia",
        b"minf",
        b"stbl",
        b"edts",
        b"dinf",
        b"udta",
        b"ilst",
    }
    patched = 0

    def walk(start: int, end: int) -> None:
        nonlocal patched
        position = start
        while position + 8 <= end:
            size = struct.unpack_from(">I", data, position)[0]
            box_type = bytes(data[position + 4 : position + 8])
            header_size = 8
            if size == 1:
                if position + 16 > end:
                    raise PipelineError("{} has a truncated large MP4 box".format(path))
                size = struct.unpack_from(">Q", data, position + 8)[0]
                header_size = 16
            elif size == 0:
                size = end - position
            if size < header_size or position + size > end:
                raise PipelineError(
                    "{} has an invalid MP4 box at byte {}".format(path, position)
                )
            payload_start = position + header_size
            box_end = position + size
            if box_type in timestamp_boxes:
                if payload_start + 4 > box_end:
                    raise PipelineError(
                        "{} has a truncated {} box".format(
                            path, box_type.decode("ascii")
                        )
                    )
                version = data[payload_start]
                timestamp_bytes = 16 if version == 1 else 8 if version == 0 else 0
                if not timestamp_bytes or payload_start + 4 + timestamp_bytes > box_end:
                    raise PipelineError(
                        "{} has an unsupported {} box version".format(
                            path, box_type.decode("ascii")
                        )
                    )
                data[
                    payload_start + 4 : payload_start + 4 + timestamp_bytes
                ] = b"\x00" * timestamp_bytes
                patched += 1
            elif box_type in container_boxes:
                walk(payload_start, box_end)
            elif box_type == b"meta":
                # FullBox version/flags precede meta's children.
                walk(payload_start + 4, box_end)
            position = box_end

    walk(0, len(data))
    if patched < 3:
        raise PipelineError(
            "{} has only {} recognized MP4 timestamp boxes; refusing an "
            "unverified canonicalization".format(path, patched)
        )
    with path.open("r+b") as handle:
        handle.write(data)
        handle.truncate()


def verify_m4a(
    path: Path,
    expected_duration: float,
    expected_sample_rate: int,
    expected_channels: int,
) -> Dict[str, Any]:
    with path.open("rb") as handle:
        header = handle.read(64)
    if len(header) < 12 or b"ftyp" not in header:
        raise PipelineError("{} is not an MPEG-4 audio container".format(path))
    ffprobe = shutil.which("ffprobe")
    if ffprobe:
        result = subprocess.run(
            [
                ffprobe,
                "-v",
                "error",
                "-select_streams",
                "a:0",
                "-show_entries",
                "stream=codec_name,sample_rate,channels:format=duration",
                "-of",
                "json",
                str(path),
            ],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        if result.returncode != 0:
            raise PipelineError(
                "ffprobe could not verify {}: {}".format(path, result.stderr.strip())
            )
        try:
            probe = json.loads(result.stdout)
            stream = probe["streams"][0]
            duration = float(probe["format"]["duration"])
            sample_rate = int(stream["sample_rate"])
            channels = int(stream["channels"])
            codec = stream["codec_name"]
        except (KeyError, IndexError, TypeError, ValueError, json.JSONDecodeError) as error:
            raise PipelineError(
                "ffprobe returned incomplete metadata for {}".format(path)
            ) from error
        if codec != "aac":
            raise PipelineError("{} codec is {}; expected AAC".format(path, codec))
        method = "ffprobe"
    else:
        afinfo = shutil.which("afinfo")
        if not afinfo:
            return {
                "method": "container-header",
                "durationSeconds": round_float(expected_duration),
                "sampleRate": expected_sample_rate,
                "channels": expected_channels,
                "codec": "aac",
            }
        result = subprocess.run(
            [afinfo, str(path)],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        if result.returncode != 0:
            raise PipelineError(
                "afinfo could not verify {}: {}".format(path, result.stderr.strip())
            )
        duration_match = re.search(
            r"estimated duration:\s*([0-9.]+) sec", result.stdout
        )
        format_match = re.search(
            r"Data format:\s+(\d+) ch,\s+(\d+) Hz,\s+aac", result.stdout
        )
        if not duration_match or not format_match:
            raise PipelineError(
                "afinfo returned incomplete AAC metadata for {}".format(path)
            )
        duration = float(duration_match.group(1))
        channels = int(format_match.group(1))
        sample_rate = int(format_match.group(2))
        codec = "aac"
        method = "afinfo"
    if abs(duration - expected_duration) > 0.075:
        raise PipelineError(
            "{} duration {:.6f}s differs from expected {:.6f}s".format(
                path, duration, expected_duration
            )
        )
    if sample_rate != expected_sample_rate or channels != expected_channels:
        raise PipelineError(
            "{} encoded as {} Hz/{} ch; expected {} Hz/{} ch".format(
                path,
                sample_rate,
                channels,
                expected_sample_rate,
                expected_channels,
            )
        )
    return {
        "method": method,
        "durationSeconds": round_float(duration),
        "sampleRate": sample_rate,
        "channels": channels,
        "codec": codec,
    }


def encode_continuous_cue_assets(
    cue: Dict[str, Any],
    internal: Dict[str, Any],
    directory: str,
    kind: str,
    paths: Dict[str, Path],
    staging: Path,
    chunk_root: Path,
    encoder: Dict[str, Any],
    pcm: Dict[str, Any],
    assets: Dict[str, Dict[str, Any]],
    output_records: List[Dict[str, Any]],
) -> int:
    for stem_id in internal["stems"]:
        relative_path = "{}/{}/{}.m4a".format(
            directory, cue["id"], stem_id
        )
        output_path = staging / relative_path
        chunk_wav = chunk_root / "{}-{}-{}-continuous.wav".format(
            kind, cue["id"], stem_id
        )
        write_wav_section(
            paths[stem_id],
            chunk_wav,
            internal["startFrame"],
            internal["endFrame"],
        )
        encode_m4a(encoder, chunk_wav, output_path)
        verification = verify_m4a(
            output_path,
            cue["durationSeconds"],
            pcm["sampleRate"],
            pcm["channels"],
        )
        digest = sha256_file(output_path)
        size = output_path.stat().st_size
        assets[relative_path] = {
            "sha256": digest,
            "bytes": size,
        }
        output_records.append(
            {
                "path": relative_path,
                "sha256": digest,
                "bytes": size,
                "kind": kind,
                "verification": verification,
            }
        )
        cue["files"][stem_id] = relative_path
        chunk_wav.unlink()
    return len(internal["stems"])


def encode_straight_through_cue_asset(
    cue: Dict[str, Any],
    internal: Dict[str, Any],
    paths: Dict[str, Path],
    config: Dict[str, Any],
    staging: Path,
    chunk_root: Path,
    encoder: Dict[str, Any],
    pcm: Dict[str, Any],
    assets: Dict[str, Dict[str, Any]],
    output_records: List[Dict[str, Any]],
) -> None:
    relative_path = "straight-through-cues/{}/mix.m4a".format(cue["id"])
    output_path = staging / relative_path
    chunk_wav = chunk_root / "{}-straight-through-mix.wav".format(
        cue["id"]
    )
    mix_headroom = render_straight_through_mix(
        paths,
        config["stems"],
        internal["stems"],
        internal["startFrame"],
        internal["endFrame"],
        config["masterGainDb"],
        destination=chunk_wav,
    )
    encode_m4a(encoder, chunk_wav, output_path)
    verification = verify_m4a(
        output_path,
        cue["durationSeconds"],
        pcm["sampleRate"],
        pcm["channels"],
    )
    digest = sha256_file(output_path)
    size = output_path.stat().st_size
    assets[relative_path] = {
        "sha256": digest,
        "bytes": size,
    }
    output_records.append(
        {
            "path": relative_path,
            "sha256": digest,
            "bytes": size,
            "kind": "straight-through-cue",
            "mixHeadroom": mix_headroom,
            "verification": verification,
        }
    )
    cue["file"] = relative_path
    chunk_wav.unlink()


def public_stem_descriptor(stem: Dict[str, Any]) -> Dict[str, Any]:
    return {
        "id": stem["id"],
        "label": stem["label"],
        "role": stem["role"],
        "gainDb": stem["gainDb"],
        "response": stem["response"],
    }


def public_optional_adaptive_metadata(
    config: Dict[str, Any],
    retrospective_cue: Optional[Dict[str, Any]],
    straight_through_cue: Optional[Dict[str, Any]] = None,
) -> Dict[str, Any]:
    output: Dict[str, Any] = {}
    if config.get("adaptiveSeam") is not None:
        output["adaptiveSeam"] = config["adaptiveSeam"]
    if retrospective_cue is not None:
        output["retrospectiveCue"] = retrospective_cue
    if straight_through_cue is not None:
        output["straightThroughCue"] = straight_through_cue
    return output


def build_analysis_report(
    config: Dict[str, Any],
    config_path: Path,
    archive_path: Path,
    archive_sha256: str,
    archive_summary: Dict[str, Any],
    pcm: Dict[str, Any],
    analysis: Dict[str, Any],
    sections: Sequence[Dict[str, Any]],
    section_internal: Dict[str, Dict[str, Any]],
    transitions: Sequence[Dict[str, Any]],
    adaptive_routing: Dict[str, Any],
    retrospective_cue: Optional[Dict[str, Any]],
    straight_through_cue: Optional[Dict[str, Any]],
    warnings: List[str],
    encoder: Optional[Dict[str, Any]] = None,
    outputs: Optional[Sequence[Dict[str, Any]]] = None,
) -> Dict[str, Any]:
    selected_sections = []
    for section in sections:
        internal = section_internal[section["id"]]
        selected_sections.append(
            {
                "id": section["id"],
                "label": section["label"],
                "classification": section["classification"],
                "role": section["role"],
                "energy": section["energy"],
                "startBar": section["startBar"],
                "barCount": section["barCount"],
                "durationSeconds": section["durationSeconds"],
                "loopable": section["loopable"],
                **(
                    {"repeat": section["repeat"]}
                    if "repeat" in section
                    else {}
                ),
                **(
                    {"cooldownSeconds": section["cooldownSeconds"]}
                    if "cooldownSeconds" in section
                    else {}
                ),
                **({"loop": section["loop"]} if "loop" in section else {}),
                "mixHeadroom": internal["mixHeadroom"],
                "includedStems": internal["includedStems"],
                "omittedSilentStems": internal["omittedStems"],
                "stemMetrics": {
                    stem_id: {
                        "rmsDbfs": metric["rmsDbfs"],
                        "peakDbfs": metric["peakDbfs"],
                        "activity": metric["activity"],
                        "transient": metric["transient"],
                    }
                    for stem_id, metric in internal["stemMetrics"].items()
                },
            }
        )
    report: Dict[str, Any] = {
        "schemaVersion": SCHEMA_VERSION,
        "pipelineVersion": PIPELINE_VERSION,
        "trackId": config["id"],
        "source": {
            "config": config_path.name,
            "archive": archive_path.name,
            "archiveSha256": archive_sha256,
            **archive_summary,
            "pcm": {
                **pcm,
                "durationSeconds": round_float(
                    pcm["frames"] / pcm["sampleRate"]
                ),
            },
        },
        "provenance": config["provenance"],
        "timing": {
            "bpm": config["bpm"],
            "beatsPerBar": config["beatsPerBar"],
            "phraseBars": config["segmentBars"],
            "barDurationSeconds": round_float(
                analysis["barFrames"] / analysis["sampleRate"]
            ),
            "totalBars": round_float(
                (analysis["sourceEndFrame"] - analysis["gridOriginFrame"])
                / analysis["barFrames"],
                6,
            ),
            "rawActiveStartSeconds": round_float(
                analysis["rawActiveStartFrame"] / analysis["sampleRate"]
            ),
            "rawActiveEndSeconds": round_float(
                analysis["rawActiveEndFrame"] / analysis["sampleRate"]
            ),
            "trimmedStartBar": round_float(
                (analysis["trimStartFrame"] - analysis["gridOriginFrame"])
                / analysis["barFrames"],
                6,
            ),
            "trimmedEndBar": round_float(
                (analysis["trimEndFrame"] - analysis["gridOriginFrame"])
                / analysis["barFrames"],
                6,
            ),
            "trimmedDurationSeconds": round_float(
                (analysis["trimEndFrame"] - analysis["trimStartFrame"])
                / analysis["sampleRate"]
            ),
        },
        "thresholds": config["analysis"],
        "mix": {
            "rmsDbfs": analysis["globalMix"]["rmsDbfs"],
            "peakDbfs": analysis["globalMix"]["peakDbfs"],
            "masterGainDb": config["masterGainDb"],
            "postMasterPeakDbfs": round_float(
                analysis["globalMix"]["peakDbfs"] + config["masterGainDb"], 3
            ),
            "targetPeakDbfs": config["analysis"]["targetPeakDbfs"],
            "treatment": "manifest pack-level gain only; encoded stem PCM balance unchanged",
        },
        "stems": analysis["globalStems"],
        "bars": analysis["bars"],
        "phrases": analysis["phrases"],
        "boundaryCandidates": analysis["boundaryCandidates"],
        "suggestedSections": analysis["suggestedSections"],
        "selectedSections": selected_sections,
        "transitions": list(transitions),
        "transitionHeadroom": analysis.get("transitionHeadroom", []),
        "adaptiveRouting": adaptive_routing,
        "warnings": list(warnings),
        **public_optional_adaptive_metadata(
            config, retrospective_cue, straight_through_cue
        ),
    }
    if encoder is not None:
        report["encoding"] = {
            key: encoder[key]
            for key in ("name", "version", "codec", "bitrateKbps")
        }
    if outputs is not None:
        report["outputs"] = list(outputs)
    return report


def update_catalog(
    output_root: Path,
    config: Dict[str, Any],
    manifest_relative_path: str,
) -> None:
    catalog_path = output_root / "index.json"
    if catalog_path.exists():
        existing = load_json(catalog_path)
        if existing.get("schemaVersion") != SCHEMA_VERSION:
            raise PipelineError(
                "{} has unsupported schemaVersion".format(catalog_path)
            )
        tracks_raw = existing.get("tracks")
        if not isinstance(tracks_raw, list):
            raise PipelineError("{} tracks must be an array".format(catalog_path))
        tracks = []
        for entry in tracks_raw:
            if not isinstance(entry, dict) or set(entry) != {
                "id",
                "title",
                "manifest",
            }:
                raise PipelineError(
                    "{} contains an invalid track entry".format(catalog_path)
                )
            if entry["id"] != config["id"]:
                tracks.append(entry)
        default_id = existing.get("defaultId")
    else:
        tracks = []
        default_id = None
    tracks.append(
        {
            "id": config["id"],
            "title": config["title"],
            "manifest": manifest_relative_path,
        }
    )
    tracks.sort(key=lambda entry: entry["id"])
    track_ids = {entry["id"] for entry in tracks}
    if config["default"] or default_id not in track_ids:
        default_id = config["id"] if config["default"] else tracks[0]["id"]
    catalog = {
        "schemaVersion": SCHEMA_VERSION,
        "defaultId": default_id,
        "tracks": tracks,
    }
    write_bytes_atomic(catalog_path, pretty_json_bytes(catalog))


def compile_one(
    config_path: Path,
    archive_override: Optional[Path],
    output_root: Path,
    mode: str,
    requested_encoder: str,
    analysis_out: Optional[Path],
) -> Optional[Path]:
    raw_config = load_json(config_path)
    config = normalize_config(raw_config, config_path)
    if archive_override is not None:
        archive_path = archive_override.resolve()
    else:
        archive_path = (config_path.parent / config["sourceArchive"]).resolve()
    if not archive_path.is_file():
        raise PipelineError(
            "source archive does not exist: {} (from {})".format(
                archive_path, config["sourceArchive"]
            )
        )
    archive_sha256 = sha256_file(archive_path)
    config_sha256 = hashlib.sha256(canonical_json_bytes(raw_config)).hexdigest()
    warnings: List[str] = []
    if (
        config["provenance"]["rightsStatus"] != "rights-cleared"
        or config["provenance"]["shipApproval"] != "approved"
    ):
        add_warning(
            warnings,
            "source rights are unverified and ship approval is pending; "
            "generated assets are for local/in-game audition only"
        )
    with tempfile.TemporaryDirectory(prefix="nilbots-soundtrack-source-") as source_dir:
        source_root = Path(source_dir)
        paths, archive_summary = extract_stems_safely(
            archive_path, config["stems"], source_root
        )
        pcm = validate_wav_alignment(paths, config["stems"])
        analysis = analyze_pack(paths, config, pcm)
        retrospective_cue, retrospective_cue_internal = (
            prepare_retrospective_cue(config, analysis)
        )
        straight_through_cue, straight_through_cue_internal = (
            prepare_straight_through_cue(config, analysis)
        )
        validate_straight_through_cue_mix(
            paths,
            config,
            straight_through_cue,
            straight_through_cue_internal,
        )
        sections, section_internal = prepare_sections(config, analysis, warnings)
        prepare_section_mix_headroom(
            paths, config, section_internal, warnings
        )
        transitions, adaptive_routing = prepare_transitions(
            config, analysis, section_internal, warnings, paths
        )
        if not sections and mode == "build":
            raise PipelineError(
                "{} has no reviewed sections. Run --validate-only with "
                "--analysis-out, review suggestedSections/boundaries, then pin "
                "sections and transitions in the config.".format(config_path)
            )
        encoder: Optional[Dict[str, Any]] = None
        if mode in ("dry-run", "build"):
            encoder = select_encoder(
                requested_encoder, config["encoding"]["bitrateKbps"]
            )
        if mode != "build":
            report = build_analysis_report(
                config,
                config_path,
                archive_path,
                archive_sha256,
                archive_summary,
                pcm,
                analysis,
                sections,
                section_internal,
                transitions,
                adaptive_routing,
                retrospective_cue,
                straight_through_cue,
                warnings,
                encoder=encoder,
            )
            if analysis_out is not None:
                write_bytes_atomic(analysis_out, pretty_json_bytes(report))
            print(
                "{}: validated {} stems, {:.3f}s, {} bars, {} selected sections, "
                "{} transitions{}".format(
                    config["id"],
                    len(config["stems"]),
                    pcm["frames"] / pcm["sampleRate"],
                    round_float(
                        (analysis["sourceEndFrame"] - analysis["gridOriginFrame"])
                        / analysis["barFrames"],
                        3,
                    ),
                    len(sections),
                    len(transitions),
                    " using {}".format(encoder["name"]) if encoder else "",
                )
            )
            for warning in warnings:
                print("warning: {}".format(warning), file=sys.stderr)
            return None

        assert encoder is not None
        track_root = output_root / config["id"]
        track_root.mkdir(parents=True, exist_ok=True)
        staging = Path(
            tempfile.mkdtemp(prefix=".staging-", dir=str(track_root))
        )
        moved = False
        try:
            output_records: List[Dict[str, Any]] = []
            assets: Dict[str, Dict[str, Any]] = {}
            with tempfile.TemporaryDirectory(
                prefix="nilbots-soundtrack-chunk-"
            ) as chunk_dir:
                chunk_root = Path(chunk_dir)
                for section in sections:
                    internal = section_internal[section["id"]]
                    rendered_loop_metrics: List[Dict[str, Any]] = []
                    crossfade_curve = internal["crossfadeCurve"]
                    for stem_id in internal["includedStems"]:
                        relative_path = "sections/{}/{}.m4a".format(
                            section["id"], stem_id
                        )
                        output_path = staging / relative_path
                        chunk_wav = chunk_root / "{}-{}.wav".format(
                            section["id"], stem_id
                        )
                        rendered_metrics = write_wav_section(
                            paths[stem_id],
                            chunk_wav,
                            internal["startFrame"],
                            internal["endFrame"],
                            section["loop"]["crossfadeSeconds"]
                            if section["loopable"]
                            else 0.0,
                            continuation_end_frame=analysis["sourceEndFrame"],
                            crossfade_curve=crossfade_curve,
                        )
                        if section["loopable"]:
                            rendered_loop_metrics.append(rendered_metrics)
                        encode_m4a(encoder, chunk_wav, output_path)
                        verification = verify_m4a(
                            output_path,
                            section["durationSeconds"],
                            pcm["sampleRate"],
                            pcm["channels"],
                        )
                        digest = sha256_file(output_path)
                        size = output_path.stat().st_size
                        assets[relative_path] = {
                            "sha256": digest,
                            "bytes": size,
                        }
                        output_records.append(
                            {
                                "path": relative_path,
                                "sha256": digest,
                                "bytes": size,
                                "verification": verification,
                            }
                        )
                        section["files"][stem_id] = relative_path
                        chunk_wav.unlink()
                    if section["loopable"]:
                        boundary_similarity = min(
                            metric["boundarySimilarity"]
                            for metric in rendered_loop_metrics
                        )
                        section["loop"]["boundarySimilarity"] = boundary_similarity
                        section["loop"]["continuationFrames"] = min(
                            metric["continuationFrames"]
                            for metric in rendered_loop_metrics
                        )
                        section["loop"]["seamJumpDbfs"] = max(
                            metric["seamJumpDbfs"]
                            for metric in rendered_loop_metrics
                        )
                        section["loop"]["blendPeakDbfs"] = max(
                            metric["blendPeakDbfs"]
                            for metric in rendered_loop_metrics
                        )
                        if (
                            boundary_similarity
                            < config["analysis"]["minimumBoundarySimilarity"]
                        ):
                            raise PipelineError(
                                "rendered loop {} boundary similarity {:.3f} is "
                                "below configured minimum {:.3f}".format(
                                    section["id"],
                                    boundary_similarity,
                                    config["analysis"][
                                        "minimumBoundarySimilarity"
                                    ],
                                )
                            )
                if (
                    retrospective_cue is not None
                    and retrospective_cue_internal is not None
                ):
                    encode_continuous_cue_assets(
                        retrospective_cue,
                        retrospective_cue_internal,
                        "retrospective-cues",
                        "retrospective-cue",
                        paths,
                        staging,
                        chunk_root,
                        encoder,
                        pcm,
                        assets,
                        output_records,
                    )
                if (
                    straight_through_cue is not None
                    and straight_through_cue_internal is not None
                ):
                    encode_straight_through_cue_asset(
                        straight_through_cue,
                        straight_through_cue_internal,
                        paths,
                        config,
                        staging,
                        chunk_root,
                        encoder,
                        pcm,
                        assets,
                        output_records,
                    )
            retrospective_cue_asset_count = (
                len(retrospective_cue["files"])
                if retrospective_cue is not None
                else 0
            )
            straight_through_cue_asset_count = (
                1
                if straight_through_cue is not None
                and straight_through_cue["file"]
                else 0
            )
            section_asset_count = (
                len(output_records)
                - retrospective_cue_asset_count
                - straight_through_cue_asset_count
            )
            report = build_analysis_report(
                config,
                config_path,
                archive_path,
                archive_sha256,
                archive_summary,
                pcm,
                analysis,
                sections,
                section_internal,
                transitions,
                adaptive_routing,
                retrospective_cue,
                straight_through_cue,
                warnings,
                encoder=encoder,
                outputs=output_records,
            )
            report_path = staging / "analysis.json"
            report_path.write_bytes(pretty_json_bytes(report))
            assets["analysis.json"] = {
                "sha256": sha256_file(report_path),
                "bytes": report_path.stat().st_size,
            }
            fingerprint_payload = {
                "pipelineVersion": PIPELINE_VERSION,
                "sourceSha256": archive_sha256,
                "configSha256": config_sha256,
                "encoder": {
                    key: encoder[key]
                    for key in ("name", "version", "codec", "bitrateKbps")
                },
                "assets": assets,
            }
            fingerprint = hashlib.sha256(
                canonical_json_bytes(fingerprint_payload)
            ).hexdigest()
            version = "v{}-{}".format(PIPELINE_VERSION, fingerprint[:16])
            manifest = {
                "schemaVersion": SCHEMA_VERSION,
                "id": config["id"],
                "title": config["title"],
                "provenance": config["provenance"],
                "bpm": config["bpm"],
                "beatsPerBar": config["beatsPerBar"],
                "sampleRate": pcm["sampleRate"],
                "gridOriginFrame": config["gridOriginFrame"],
                "barFrames": config["barFrames"],
                "sourceEndFrame": config["sourceEndFrame"],
                "segmentBars": config["segmentBars"],
                "durationSeconds": round_float(
                    (analysis["trimEndFrame"] - analysis["trimStartFrame"])
                    / analysis["sampleRate"]
                ),
                "masterGainDb": config["masterGainDb"],
                "adaptiveLatencyBudgetBars": config[
                    "adaptiveLatencyBudgetBars"
                ],
                **public_optional_adaptive_metadata(
                    config, retrospective_cue, straight_through_cue
                ),
                "stems": [
                    public_stem_descriptor(stem) for stem in config["stems"]
                ],
                "entrySection": config["entrySection"],
                "sections": sections,
                "transitions": transitions,
                "assets": assets,
                "build": {
                    "version": version,
                    "pipelineVersion": PIPELINE_VERSION,
                    "sourceSha256": archive_sha256,
                    "configSha256": config_sha256,
                    "encoder": {
                        key: encoder[key]
                        for key in ("name", "version", "codec", "bitrateKbps")
                    },
                    "analysis": "analysis.json",
                },
            }
            manifest_bytes = pretty_json_bytes(manifest)
            (staging / "manifest.json").write_bytes(manifest_bytes)
            final_directory = track_root / version
            if final_directory.exists():
                existing_manifest = final_directory / "manifest.json"
                if (
                    not existing_manifest.is_file()
                    or existing_manifest.read_bytes() != manifest_bytes
                ):
                    raise PipelineError(
                        "version collision at {}; refusing to overwrite".format(
                            final_directory
                        )
                    )
                shutil.rmtree(staging)
            else:
                os.replace(str(staging), str(final_directory))
                moved = True
            manifest_relative = "{}/{}/manifest.json".format(
                config["id"], version
            )
            update_catalog(output_root, config, manifest_relative)
            if analysis_out is not None:
                write_bytes_atomic(
                    analysis_out, (final_directory / "analysis.json").read_bytes()
                )
            print(
                "{}: built {} section assets, {} retrospective cue assets, and "
                "{} straight-through cue assets with {} into {} ({})".format(
                    config["id"],
                    section_asset_count,
                    retrospective_cue_asset_count,
                    straight_through_cue_asset_count,
                    encoder["name"],
                    final_directory,
                    version,
                )
            )
            for warning in warnings:
                print("warning: {}".format(warning), file=sys.stderr)
            return final_directory
        finally:
            if not moved and staging.exists():
                shutil.rmtree(staging)


def default_config_paths(repo_root: Path) -> List[Path]:
    return sorted((repo_root / "soundtracks").glob("*.json"))


def parse_args(argv: Optional[Sequence[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Securely analyze aligned WAV stems and compile an adaptive AAC "
            "section graph."
        )
    )
    parser.add_argument(
        "configs",
        nargs="*",
        type=Path,
        help="soundtrack config JSON (default: every soundtracks/*.json)",
    )
    parser.add_argument(
        "--archive",
        type=Path,
        help="override source archive (only valid with one config)",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        help="output root (default: web/public/soundtracks)",
    )
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument(
        "--validate-only",
        action="store_true",
        help="validate ZIP/PCM/config/graph and analyze without requiring an encoder",
    )
    mode.add_argument(
        "--dry-run",
        action="store_true",
        help="validate/analyze and select an encoder without writing soundtrack assets",
    )
    parser.add_argument(
        "--encoder",
        choices=("auto", "ffmpeg", "afconvert"),
        default="auto",
        help="AAC encoder preference (default: ffmpeg, then afconvert)",
    )
    parser.add_argument(
        "--analysis-out",
        type=Path,
        help="also atomically write the detailed analysis report here (one config only)",
    )
    return parser.parse_args(argv)


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = parse_args(argv)
    repo_root = Path(__file__).resolve().parent.parent
    configs = [path.resolve() for path in args.configs]
    if not configs:
        configs = default_config_paths(repo_root)
    if not configs:
        print("error: no soundtrack configs found", file=sys.stderr)
        return 2
    if args.archive is not None and len(configs) != 1:
        print("error: --archive requires exactly one config", file=sys.stderr)
        return 2
    if args.analysis_out is not None and len(configs) != 1:
        print("error: --analysis-out requires exactly one config", file=sys.stderr)
        return 2
    output_root = (
        args.output_dir.resolve()
        if args.output_dir is not None
        else repo_root / "web" / "public" / "soundtracks"
    )
    mode = "validate" if args.validate_only else "dry-run" if args.dry_run else "build"
    try:
        for config_path in configs:
            if not config_path.is_file():
                raise PipelineError(
                    "soundtrack config does not exist: {}".format(config_path)
                )
            compile_one(
                config_path=config_path,
                archive_override=args.archive,
                output_root=output_root,
                mode=mode,
                requested_encoder=args.encoder,
                analysis_out=args.analysis_out.resolve()
                if args.analysis_out is not None
                else None,
            )
    except PipelineError as error:
        print("error: {}".format(error), file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
