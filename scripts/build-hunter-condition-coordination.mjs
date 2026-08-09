#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const target = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/hunter-v1.json",
);
const source = JSON.parse(fs.readFileSync(target, "utf8"));
if (!source.conditions || !source.doctrines?.roles) {
  throw new Error("expected the condition-coordinated Hunter source");
}

const oldLayout = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/layouts/warren6-hunter-v1.json",
);
const newLayout = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/layouts/warren6-hunter-condition-v2.json",
);
const layout = JSON.parse(fs.readFileSync(oldLayout, "utf8"));
layout.layoutId = "warren6-hunter-condition-v2";
for (const binding of layout.bindings) {
  binding.routeAliases["shadow-hunt-loop"] = "shadow-hunt-loop-reverse";
}
const north = layout.routes.find((route) => route.routeId === "shadow-north-long");
const south = layout.routes.find((route) => route.routeId === "shadow-south-long");
if (!north || !south) throw new Error("expected both long shadow routes");
layout.routes = layout.routes.filter((route) =>
  route.routeId !== "shadow-hunt-loop" && route.routeId !== "shadow-hunt-loop-reverse"
);
const connectorDown = [[8, 8], [8, 13], [8, 18], [8, 25]];
const connectorUp = [[8, 18], [8, 13], [8, 8], [8, 1]];
const huntWaypoints = [
  ...north.waypoints,
  ...connectorDown,
  ...south.waypoints.slice(1),
  ...connectorUp,
];
layout.routes.push({
  routeId: "shadow-hunt-loop",
  corridorWidth: 2,
  waypoints: huntWaypoints,
});
layout.routes.push({
  routeId: "shadow-hunt-loop-reverse",
  corridorWidth: 2,
  waypoints: [...huntWaypoints].reverse(),
});
const layoutText = `${JSON.stringify(layout, null, 1)}\n`;
fs.writeFileSync(newLayout, layoutText);
const layoutSha256 = crypto.createHash("sha256").update(layoutText).digest("hex");

if (source.doctrines?.cohorts?.["well-surge"]?.take === 3
    && source.doctrines?.roles?.eyes?.modes?.length === 4
    && source.layout?.path === "../layouts/warren6-hunter-condition-v2.json"
    && source.layout?.sha256 === layoutSha256) {
  console.log("hunter-v1 already uses narrow condition coordination");
  process.exit(0);
}

const role = Object.fromEntries(source.roles.map((entry) => [entry.roleId, entry]));
const compactRoles = source.roles.map(({ tags: _tags, formationPosture: _posture, ...entry }) => entry);
const custodyPolicies = source.custodyPolicies.map(({ escorts: _escorts, escortGroups: _groups, ...policy }) => policy);

const standardFight = {
  targets: { prefer: ["carrier", "weakest"] },
  chase: { leash: 2 },
  defense: { radius: 4, return: true },
};
const homeFight = {
  targets: { prefer: ["carrier", "anchor", "weakest"] },
  chase: { leash: 1 },
  defense: { radius: 3, return: true },
};
const assassinFight = {
  targets: { prefer: ["carrier", "weakest"], lone: 4 },
  chase: { leash: 6, persistTicks: 3 },
  defense: { radius: 2, return: true },
};

const raceModes = [
  {
    patrol: "centre-well-loop",
    while: "centre-birth-soon",
    until: "centre-birth-far",
    cohort: "centre-runners",
    focus: "well",
  },
  {
    patrol: "north-well-loop",
    while: "north-birth-soon",
    until: "north-birth-far",
    cohort: "north-runners",
    focus: "well",
  },
  {
    patrol: "south-well-loop",
    while: "south-birth-soon",
    until: "south-birth-far",
    cohort: "south-runners",
    focus: "well",
  },
];
const homeMode = (posture) => ({
  patrol: { zone: "own-home" },
  while: "gate-threat or home-threat",
  until: "approach-clear and home-clear",
  cohort: "home-guard",
  focus: "home",
  posture,
  fight: homeFight,
});
const surgeMode = (posture) => ({
  patrol: { zone: "well-line" },
  while: "window-open",
  until: "window-closed",
  cohort: "well-surge",
  focus: "well",
  posture,
});
const races = (posture) => raceModes.map((mode) => ({ ...mode, posture }));
const floor = (target, posture) => ({ patrol: target, focus: "patrol", posture });

const roleDoctrines = {
  hunter: {
    custody: "well-custody",
    fight: source.doctrines.roles.hunter.fight,
    modes: [
      { recover: "auto", focus: "patrol" },
      {
        assault: "strike-line",
        while: "shadow-op",
        until: "shadow-cooled",
        focus: "assassin",
        posture: "front",
        fight: { collect: "first" },
      },
      {
        patrol: "stage-loop",
        while: "ghost-hurt",
        until: "ghost-whole",
        focus: "patrol",
        posture: "rear",
      },
      {
        ...floor("shadow-hunt-loop", "front"),
        fight: { collect: "first" },
      },
    ],
    conceal: false,
    scavenge: source.doctrines.roles.hunter.scavenge,
  },
  medic: {
    custody: "well-custody",
    fight: standardFight,
    repair: source.doctrines.roles.medic.repair,
    modes: [
      { recover: "auto", focus: "patrol" },
      {
        follow: "hunter",
        posture: "trail",
        while: "shadow-op",
        until: "shadow-cooled",
        focus: "assassin",
      },
      homeMode("rear"),
      ...races("rear"),
      floor("stage-loop", "rear"),
    ],
    conceal: false,
  },
  hauler: {
    custody: "well-custody",
    fight: standardFight,
    modes: [
      { fetch: "well-custody" },
      {
        assault: "strike-line",
        while: "self-veteran",
        until: "self-green",
        focus: "assassin",
        posture: "front",
        fight: assassinFight,
      },
      ...races("core"),
      surgeMode("core"),
      floor("stage-loop", "core"),
    ],
    conceal: false,
  },
  carrier: {
    custody: "well-custody",
    fight: standardFight,
    modes: [
      { fetch: "well-custody" },
      ...races("core"),
      surgeMode("core"),
      floor("stage-loop", "core"),
    ],
    conceal: false,
  },
  lancer: {
    custody: "well-custody",
    fight: standardFight,
    modes: [
      { fetch: "well-custody" },
      {
        follow: "hunter",
        posture: "screen",
        while: "shadow-op",
        until: "shadow-cooled",
        focus: "assassin",
      },
      homeMode("front"),
      floor("stage-loop", "front"),
    ],
    conceal: false,
  },
  eyes: {
    custody: "well-custody",
    fight: standardFight,
    modes: [
      { fetch: "well-custody" },
      homeMode("core"),
      surgeMode("front"),
      floor("stage-loop", "front"),
    ],
    conceal: false,
  },
  reserve: {
    custody: "well-custody",
    fight: standardFight,
    modes: [
      { fetch: "well-custody" },
      floor({ zone: "own-approach" }, "rear"),
    ],
    conceal: false,
  },
};

for (const roleId of Object.keys(roleDoctrines)) {
  if (!role[roleId]) throw new Error(`unknown role ${roleId}`);
}

const conditions = {
  "north-birth-soon": {
    fact: "well-ticks-until-birth", operator: "at-most", value: 28, subject: "north",
  },
  "north-birth-far": {
    fact: "well-ticks-until-birth", operator: "at-least", value: 45, subject: "north",
  },
  "centre-birth-soon": {
    fact: "well-ticks-until-birth", operator: "at-most", value: 28, subject: "centre",
  },
  "centre-birth-far": {
    fact: "well-ticks-until-birth", operator: "at-least", value: 45, subject: "centre",
  },
  "south-birth-soon": {
    fact: "well-ticks-until-birth", operator: "at-most", value: 28, subject: "south",
  },
  "south-birth-far": {
    fact: "well-ticks-until-birth", operator: "at-least", value: 45, subject: "south",
  },
  "window-open": {
    fact: "known-enemies-unavailable", operator: "at-least", value: 2,
  },
  "window-closed": {
    fact: "known-enemies-unavailable", operator: "at-most", value: 1,
  },
  "shadow-op": {
    fact: "max-level", operator: "at-least", value: 3, subject: "hunter",
  },
  "shadow-cooled": {
    fact: "max-level", operator: "at-most", value: 2, subject: "hunter",
  },
  "self-veteran": {
    fact: "self-level", operator: "at-least", value: 3,
  },
  "self-green": {
    fact: "self-level", operator: "at-most", value: 2,
  },
  "gate-threat": {
    fact: "remembered-enemies-in-zone", operator: "at-least", value: 1,
    zone: "own-approach", freshnessTicks: 21,
  },
  "home-threat": {
    fact: "remembered-enemies-in-zone", operator: "at-least", value: 1,
    zone: "own-home", freshnessTicks: 21,
  },
  "approach-clear": {
    fact: "remembered-enemies-in-zone", operator: "at-most", value: 0,
    zone: "own-approach", freshnessTicks: 21,
  },
  "home-clear": {
    fact: "remembered-enemies-in-zone", operator: "at-most", value: 0,
    zone: "own-home", freshnessTicks: 21,
  },
  "ghost-hurt": { fact: "self-health", operator: "at-most", value: 2 },
  "ghost-whole": { fact: "self-health", operator: "at-least", value: 3 },
};

const migrated = {
  schema: source.schema,
  playbookId: source.playbookId,
  auditStatus: source.auditStatus,
  composition: source.composition,
  layout: {
    path: "../layouts/warren6-hunter-condition-v2.json",
    sha256: layoutSha256,
  },
  memory: source.memory,
  roles: compactRoles,
  custodyPolicies,
  doctrines: {
    roles: roleDoctrines,
    cohorts: {
      "centre-runners": { take: 3, select: "nearest" },
      "north-runners": { take: 3, select: "nearest" },
      "south-runners": { take: 3, select: "nearest" },
      "home-guard": { take: 3, select: "nearest" },
      "well-surge": { take: 3, select: "nearest" },
    },
    focus: {
      assassin: { maximumAttackers: 1 },
      home: { maximumAttackers: 3 },
      patrol: { maximumAttackers: 2 },
      well: { maximumAttackers: 3 },
    },
  },
  conditions,
};

fs.writeFileSync(target, `${JSON.stringify(migrated, null, 1)}\n`);
console.log(`rebuilt ${path.relative(root, target)} with condition coordination`);
