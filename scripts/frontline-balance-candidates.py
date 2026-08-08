#!/usr/bin/env python3
"""Generate authoritative Frontline duel candidate contracts into a Lab spec."""

from __future__ import annotations

import argparse
import json
import subprocess
from pathlib import Path
from typing import Any


MAP_VALUES = {
    "current": "current",
    "thin-fronts": "thin-fronts",
    "outer-shoulder-bypass": "outer-shoulder-bypass",
}


def _contract(
    cli: Path,
    map_topology: str,
    companion_policy: str | None,
    class_pair: str | None,
) -> dict[str, Any]:
    command = [
        str(cli),
        "experiment",
        "frontline-labs",
        "--print-candidate-contract",
        "--duel-map",
        MAP_VALUES[map_topology],
    ]
    if class_pair is not None:
        if companion_policy is not None:
            raise ValueError(
                "class-pair candidates declare fabrication economics through "
                "the class, not a companion-policy factor"
            )
        command.extend(["--classes", class_pair])
    elif companion_policy == "automatic-activation":
        command.append("--auto-companions")
    elif companion_policy != "manual-fabrication":
        raise ValueError(
            f"unsupported companion policy {companion_policy!r}"
        )
    completed = subprocess.run(
        command,
        check=False,
        capture_output=True,
        text=True,
    )
    if completed.returncode != 0:
        raise ValueError(
            "candidate contract command failed:\n"
            + completed.stderr.strip()
        )
    value = json.loads(completed.stdout)
    if not isinstance(value, dict):
        raise ValueError("candidate contract output must be a JSON object")
    return value


def generate(spec_path: Path, cli: Path) -> tuple[dict[str, Any], bool]:
    document = json.loads(spec_path.read_text(encoding="utf-8"))
    if not isinstance(document, dict):
        raise ValueError("Balance Lab spec must be a JSON object")
    candidates = document.get("candidates")
    if not isinstance(candidates, list) or not candidates:
        raise ValueError("Balance Lab spec candidates must be non-empty")
    changed = False
    for candidate in candidates:
        if not isinstance(candidate, dict):
            raise ValueError("every candidate must be an object")
        factors = candidate.get("factors")
        if not isinstance(factors, dict):
            raise ValueError("candidate factors must be an object")
        map_topology = factors.get("map-topology")
        companion_policy = factors.get("companion-policy")
        class_pair = factors.get("class-pair")
        if map_topology not in MAP_VALUES:
            raise ValueError(
                f"unsupported map topology {map_topology!r}"
            )
        generated = _contract(
            cli,
            map_topology,
            companion_policy,
            class_pair,
        )
        if candidate.get("contract") != generated:
            candidate["contract"] = generated
            changed = True
    return document, changed


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--spec", required=True, type=Path)
    parser.add_argument("--cli", required=True, type=Path)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args(argv)
    spec_path = args.spec.resolve()
    cli = args.cli.resolve()
    if not cli.is_file():
        parser.error(f"--cli does not exist: {cli}")
    document, changed = generate(spec_path, cli)
    if args.write:
        if changed:
            spec_path.write_text(
                json.dumps(document, indent=2, ensure_ascii=False) + "\n",
                encoding="utf-8",
            )
        print("updated" if changed else "already current")
        return 0
    if changed:
        print(
            "candidate contracts have drifted; rerun with --write",
        )
        return 1
    print("candidate contracts are current")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
