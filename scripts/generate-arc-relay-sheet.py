#!/usr/bin/env python3
"""Validate a provisional Arc Relay evaluation sheet and compile it to C#.

The stock mind engine stays source-identical across the depth grid. Only this
generated data file and the separately hashed JSON sheet vary. This audit
schema is not the future player-facing drawing/editing format.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

LAUNCH_CLASSES = {
    "kestrel", "palisade", "towline", "patchbay", "lantern", "mortar",
    "minesmith", "hush", "relay", "switchback", "longshot", "mason",
    "sunder", "repulsor", "veil", "nest",
}
EVALUATION_MAPS = {
    "arc-relay-threefold-01",
    "arc-relay-threefold-home-gates-wide-01",
}
ROLES = {"carrier", "screen", "intercept", "reserve"}
THEATERS = {"north", "centre", "south"}
TRIGGERS = {
    "after-enemy-pulse", "double-enemy-possession", "after-own-pulse",
    "wipe", "route-failure",
}


def fail(message: str) -> None:
    raise ValueError(message)


def position(value: object, label: str) -> tuple[int, int]:
    if not isinstance(value, list) or len(value) != 2:
        fail(f"{label} must be [x,y]")
    x, y = value
    if not isinstance(x, int) or not isinstance(y, int) or x < 0 or y < 0:
        fail(f"{label} coordinates must be non-negative integers")
    return x, y


def quote(value: str) -> str:
    return json.dumps(value, ensure_ascii=False)


def csharp_positions(values: list[object], label: str) -> str:
    points = [position(value, f"{label}[{index}]")
              for index, value in enumerate(values)]
    if not points:
        fail(f"{label} must not be empty")
    return "[" + ", ".join(f"new({x}, {y})" for x, y in points) + "]"


def validate(data: dict) -> None:
    if data.get("schema") != "arc-relay-evaluation-sheet-v0":
        fail("schema must be arc-relay-evaluation-sheet-v0")
    if data.get("mapId") not in EVALUATION_MAPS:
        fail("v0 sheets must target a registered Arc Relay evaluation map")
    composition = data.get("composition")
    if not isinstance(composition, list) or len(composition) != 8:
        fail("composition must contain exactly eight classes")
    if any(value not in LAUNCH_CLASSES for value in composition):
        fail("composition contains an unknown launch class")
    if any(composition.count(value) > 2 for value in composition):
        fail("composition exceeds the two-copy cap")
    slots = data.get("slots")
    if not isinstance(slots, list) or len(slots) != 8:
        fail("slots must contain exactly eight plans")
    if sorted(slot.get("unitId") for slot in slots) != list(range(8)):
        fail("slot unitId values must be exactly 0..7")
    for slot in slots:
        if slot.get("role") not in ROLES or slot.get("theater") not in THEATERS:
            fail(f"slot {slot.get('unitId')} has invalid role or theater")
        partner = slot.get("partnerUnitId")
        if not isinstance(partner, int) or partner not in range(8):
            fail(f"slot {slot.get('unitId')} has invalid partnerUnitId")
        csharp_positions(slot.get("outboundPath", []), "outboundPath")
        csharp_positions(slot.get("returnPath", []), "returnPath")
    zones = data.get("zones")
    if not isinstance(zones, dict) or set(zones) != THEATERS | {"intercept"}:
        fail("zones must define north, centre, south, and intercept")
    for name, rect in zones.items():
        if (not isinstance(rect, list) or len(rect) != 4
                or any(not isinstance(value, int) for value in rect)):
            fail(f"zone {name} must be [minX,minY,maxX,maxY]")
        if rect[0] > rect[2] or rect[1] > rect[3]:
            fail(f"zone {name} has inverted bounds")
    rally = data.get("rallyLines")
    if not isinstance(rally, dict) or not rally:
        fail("rallyLines must be a non-empty object")
    for name, values in rally.items():
        csharp_positions(values, f"rallyLines.{name}")
    policies = data.get("policies")
    if not isinstance(policies, dict):
        fail("policies must be an object")
    carrier = policies.get("carrier", {})
    escort = policies.get("escort", {})
    interception = policies.get("interception", {})
    for key in ("handoffHealthAtOrBelow", "routeFailureTicks"):
        if not isinstance(carrier.get(key), int) or carrier[key] < 0:
            fail(f"policies.carrier.{key} must be non-negative")
    if not isinstance(escort.get("followDistance"), int):
        fail("policies.escort.followDistance must be an integer")
    gambits = data.get("gambits")
    if not isinstance(gambits, list):
        fail("gambits must be an array")
    priorities = [entry.get("priority") for entry in gambits]
    if priorities != sorted(priorities) or len(priorities) != len(set(priorities)):
        fail("gambits must have unique ascending priorities")
    for entry in gambits:
        if entry.get("trigger") not in TRIGGERS:
            fail(f"unknown gambit trigger {entry.get('trigger')}")
        if entry.get("roleOverride") not in ROLES:
            fail(f"unknown gambit role {entry.get('roleOverride')}")
        scope_roles = entry.get("scopeRoles")
        if (not isinstance(scope_roles, list) or not scope_roles
                or any(role not in ROLES for role in scope_roles)
                or len(scope_roles) != len(set(scope_roles))):
            fail("gambit scopeRoles must be unique known roles")
        if entry.get("rallyLine") not in rally:
            fail(f"unknown gambit rally line {entry.get('rallyLine')}")
        if not isinstance(entry.get("durationTicks"), int) or entry["durationTicks"] <= 0:
            fail("gambit durationTicks must be positive")
        if not isinstance(entry.get("cooldownTicks"), int) or entry["cooldownTicks"] <= 0:
            fail("gambit cooldownTicks must be positive")


def generate(data: dict, digest: str) -> str:
    composition = ", ".join(quote(value) for value in data["composition"])
    slots = []
    for slot in sorted(data["slots"], key=lambda value: value["unitId"]):
        slots.append(
            "        new("
            f"{slot['unitId']}, {quote(slot['theater'])}, {quote(slot['role'])}, "
            f"{slot['partnerUnitId']}, "
            f"{csharp_positions(slot['outboundPath'], 'outboundPath')}, "
            f"{csharp_positions(slot['returnPath'], 'returnPath')})")
    zones = []
    for name in sorted(data["zones"]):
        min_x, min_y, max_x, max_y = data["zones"][name]
        zones.append(
            f"        [{quote(name)}] = new({min_x}, {min_y}, {max_x}, {max_y}),")
    rally = []
    for name in sorted(data["rallyLines"]):
        rally.append(
            f"        [{quote(name)}] = "
            f"{csharp_positions(data['rallyLines'][name], f'rallyLines.{name}')},")
    gambits = []
    for entry in data["gambits"]:
        gambits.append(
            "        new("
            f"{entry['priority']}, {quote(entry['id'])}, "
            f"{quote(entry['trigger'])}, {entry['durationTicks']}, "
            f"{entry['cooldownTicks']}, "
            "[" + ", ".join(quote(role) for role in entry['scopeRoles']) + "], "
            f"{quote(entry['roleOverride'])}, {quote(entry['rallyLine'])})")
    carrier = data["policies"]["carrier"]
    escort = data["policies"]["escort"]
    intercept = data["policies"]["interception"]
    boolean = lambda value: "true" if value else "false"
    slot_lines = ",\n".join(slots)
    zone_lines = "\n".join(zones)
    rally_lines = "\n".join(rally)
    gambit_lines = ",\n".join(gambits)
    gambit_block = f"{gambit_lines}," if gambit_lines else ""
    return f"""// <auto-generated />
// Source sheet SHA-256: {digest}
using BotArena.Sdk;

internal static class StockSheet
{{
    internal const string Schema = {quote(data['schema'])};
    internal const string SheetId = {quote(data['sheetId'])};
    internal const string MapId = {quote(data['mapId'])};
    internal const string SourceSha256 = {quote(digest)};

    internal static readonly string[] Composition = [{composition}];

    internal static readonly UnitPlan[] Units =
    [
{slot_lines},
    ];

    internal static readonly Dictionary<string, Zone> Zones =
        new(StringComparer.Ordinal)
    {{
{zone_lines}
    }};

    internal static readonly Dictionary<string, Position[]> RallyLines =
        new(StringComparer.Ordinal)
    {{
{rally_lines}
    }};

    internal static readonly CarrierPolicy Carrier = new(
        {carrier['handoffHealthAtOrBelow']},
        {boolean(carrier['preferAssignedTheater'])},
        {carrier['routeFailureTicks']});
    internal static readonly EscortPolicy Escort = new(
        {escort['followDistance']},
        {boolean(escort['focusEnemyCarrier'])});
    internal static readonly InterceptionPolicy Interception = new(
        {boolean(intercept['focusEnemyCarrier'])},
        {boolean(intercept['looseCoreFallback'])});

    internal static readonly GambitPlan[] Gambits =
    [
{gambit_block}
    ];
}}
"""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sheet", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--validate-only", action="store_true")
    args = parser.parse_args()
    if args.validate_only and args.output is not None:
        parser.error("--validate-only and --output cannot be combined")
    if not args.validate_only and args.output is None:
        parser.error("--output is required unless --validate-only is used")
    raw = args.sheet.read_bytes()
    data = json.loads(raw)
    if not isinstance(data, dict):
        fail("sheet root must be an object")
    validate(data)
    if args.validate_only:
        print(
            f"{args.sheet}: valid; sha256={hashlib.sha256(raw).hexdigest()}"
        )
        return 0
    output = generate(data, hashlib.sha256(raw).hexdigest())
    args.output.write_text(output, encoding="utf-8")
    print(f"{args.sheet}: valid; wrote {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
