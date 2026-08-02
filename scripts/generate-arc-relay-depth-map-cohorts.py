#!/usr/bin/env python3
"""Derive map-native Arc Relay depth-study sheets from a frozen cohort.

Counterflow keeps authored coordinates and changes only map identity. The
larger Threefold arm stretches authored y coordinates around the three Well
anchors, preserving the evaluation sheet's intent instead of silently asking
the stock mind to play a taller map with baseline waypoints.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import subprocess
from typing import Any


REPO = Path(__file__).resolve().parent.parent
DEFAULT_SOURCE = (
    REPO / "arena-bots/arc-relay/flow-intent-v1-2026-08-02/cohort.json"
)
DEFAULT_OUTPUT = (
    REPO / "arena-bots/arc-relay/depth-map-v1-2026-08-02"
)
DEFAULT_CLI = REPO / "src/BotArena.Cli/bin/Debug/net10.0/botarena.dll"
PROFILES = {
    "counterflow": "depth-counterflow",
    "larger": "depth-larger",
}


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path}: JSON root must be an object")
    return value


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def encode(value: dict[str, Any]) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")


def resolve(base: Path, value: str) -> Path:
    path = Path(value)
    return path.resolve() if path.is_absolute() else (base / path).resolve()


def stretch_y(value: int) -> int:
    """Map Well anchors 4/11/18 to 4/14/24 with integer rounding."""
    if value <= 4:
        return value
    if value <= 11:
        return 4 + ((value - 4) * 10 + 3) // 7
    if value <= 18:
        return 14 + ((value - 11) * 10 + 3) // 7
    return value + 6


def transform_position(value: list[int], variant: str) -> list[int]:
    if len(value) != 2 or not all(isinstance(item, int) for item in value):
        raise ValueError(f"invalid authored position: {value!r}")
    if variant == "counterflow":
        return list(value)
    return [value[0], stretch_y(value[1])]


def transform_sheet(
    source: dict[str, Any],
    variant: str,
    map_id: str,
) -> dict[str, Any]:
    result = copy.deepcopy(source)
    result["mapId"] = map_id
    for slot in result["slots"]:
        for key in ("outboundPath", "returnPath"):
            slot[key] = [
                transform_position(value, variant) for value in slot[key]
            ]
    result["rallyLines"] = {
        name: [transform_position(value, variant) for value in values]
        for name, values in result["rallyLines"].items()
    }
    result["zones"] = {
        name: [
            rectangle[0],
            stretch_y(rectangle[1]) if variant == "larger" else rectangle[1],
            rectangle[2],
            stretch_y(rectangle[3]) if variant == "larger" else rectangle[3],
        ]
        for name, rectangle in result["zones"].items()
    }
    result["auditStatus"]["mapStudyAdaptation"] = {
        "sourceMapId": source["mapId"],
        "variant": variant,
        "coordinateTransform": (
            "three-Well anchored vertical stretch v1"
            if variant == "larger"
            else "identity coordinates v1"
        ),
    }
    return result


def contract(
    cli: Path,
    profile: str,
    sheet0: Path,
    sheet1: Path,
) -> dict[str, Any]:
    completed = subprocess.run(
        [
            "dotnet",
            str(cli),
            "experiment",
            "arc-relay",
            "--sheet0",
            str(sheet0),
            "--sheet1",
            str(sheet1),
            "--loop-profile",
            profile,
            "--print-contract",
        ],
        cwd=REPO,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError(completed.stderr or completed.stdout)
    value = json.loads(completed.stdout)
    if not isinstance(value, dict):
        raise ValueError("contract output was not an object")
    return value


def authored_positions(sheet: dict[str, Any]) -> list[tuple[str, list[int]]]:
    result: list[tuple[str, list[int]]] = []
    for slot in sheet["slots"]:
        for key in ("outboundPath", "returnPath"):
            for index, value in enumerate(slot[key]):
                result.append(
                    (f"slot-{slot['unitId']}.{key}[{index}]", value))
    for name, values in sheet["rallyLines"].items():
        for index, value in enumerate(values):
            result.append((f"rallyLines.{name}[{index}]", value))
    return result


def validate_sheet(sheet: dict[str, Any], contract_value: dict[str, Any]) -> None:
    map_contract = contract_value["map"]
    if sheet["mapId"] != map_contract["mapId"]:
        raise ValueError(
            f"sheet map {sheet['mapId']} != contract {map_contract['mapId']}"
        )
    rows = map_contract["tileRows"]
    width = len(rows[0])
    height = len(rows)
    for label, value in authored_positions(sheet):
        x, y = value
        if not (0 <= x < width and 0 <= y < height):
            raise ValueError(f"{sheet['sheetId']} {label} is out of bounds")
        # Evaluation sheets may deliberately name a blocked waypoint: the
        # stock executor paths toward the nearest reachable approach tile.
        # That behavior exists in the frozen source cohort, so bounds—not
        # walkability—are the compatibility contract here.
    for name, rectangle in sheet["zones"].items():
        min_x, min_y, max_x, max_y = rectangle
        if not (0 <= min_x <= max_x < width and 0 <= min_y <= max_y < height):
            raise ValueError(
                f"{sheet['sheetId']} zone {name} is out of bounds: {rectangle}"
            )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--cli", type=Path, default=DEFAULT_CLI)
    args = parser.parse_args()

    source_path = args.source.resolve()
    output = args.output.resolve()
    source = read_json(source_path)
    entrants = source["entrants"]
    if len(entrants) < 2:
        raise ValueError("source cohort needs at least two entrants")
    source_sheets = [
        resolve(source_path.parent, item["sheet"]) for item in entrants
    ]
    source_artifact = resolve(source_path.parent, entrants[0]["artifact"])
    if not all(path.is_file() for path in [*source_sheets, source_artifact]):
        raise FileNotFoundError("source cohort is incomplete")

    for variant, profile in PROFILES.items():
        resolved_contract = contract(
            args.cli.resolve(), profile, source_sheets[0], source_sheets[1])
        map_contract = resolved_contract["map"]
        variant_dir = output / variant
        sheet_dir = variant_dir / "sheets"
        transformed: list[tuple[dict[str, Any], bytes]] = []
        for item, sheet_path in zip(entrants, source_sheets):
            sheet = transform_sheet(
                read_json(sheet_path), variant, map_contract["mapId"])
            validate_sheet(sheet, resolved_contract)
            transformed.append((item, encode(sheet)))

        sheet_dir.mkdir(parents=True, exist_ok=True)
        cohort_entrants = []
        for (item, payload), sheet_path in zip(transformed, source_sheets):
            filename = sheet_path.name
            destination = sheet_dir / filename
            destination.write_bytes(payload)
            cohort_entrants.append({
                "artifact": os.path.relpath(source_artifact, variant_dir),
                "artifactSha256": sha256(source_artifact),
                "entrantId": item["entrantId"],
                "sheet": f"sheets/{filename}",
                "sheetSha256": sha256_bytes(payload),
            })
        cohort = {
            "cohortId": f"arc-relay-depth-map-{variant}-v1",
            "eligibilityBars": os.path.relpath(
                REPO / "balance/arc-relay-felt-degeneracy-bars-v3.json",
                variant_dir,
            ),
            "entrants": cohort_entrants,
            "mapStudy": {
                "candidateRole": "same-cohort geometry causality",
                "loopProfile": profile,
                "mapId": map_contract["mapId"],
                "mapFingerprint": map_contract["mapFingerprint"],
                "rulesetId": resolved_contract["rules"]["rulesetId"],
                "rulesFingerprint": resolved_contract["rules"][
                    "rulesFingerprint"
                ],
                "sourceCohort": os.path.relpath(source_path, variant_dir),
                "sourceCohortSha256": sha256(source_path),
            },
        }
        (variant_dir / "cohort.json").write_bytes(encode(cohort))
        print(
            f"{variant}: {len(cohort_entrants)} sheets, "
            f"map {map_contract['mapFingerprint']}"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
