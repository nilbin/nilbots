#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const target = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/hunter-v1.json",
);
const source = JSON.parse(fs.readFileSync(target, "utf8"));

if (source.doctrines?.focus && !source.doctrines?.operations) {
  console.log("hunter-v1 already uses condition coordination");
  process.exit(0);
}
if (!source.conditions || !source.doctrines?.operations) {
  throw new Error("expected the Hunter Phase 3 gate source");
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
  },
  {
    patrol: "north-well-loop",
    while: "north-birth-soon",
    until: "north-birth-far",
  },
  {
    patrol: "south-well-loop",
    while: "south-birth-soon",
    until: "south-birth-far",
  },
];
const homeMode = {
  patrol: { zone: "own-home" },
  while: "gate-threat or home-threat",
  until: "approach-clear and home-clear",
  fight: homeFight,
};
const surgeMode = {
  patrol: { zone: "well-line" },
  while: "window-open",
  until: "window-closed",
};
const floor = (target) => ({ patrol: target });

const roleDoctrines = {
  hunter: {
    custody: "well-custody",
    fight: source.doctrines.roles.hunter.fight,
    modes: [
      { recover: "auto" },
      {
        assault: "strike-line",
        while: "shadow-op",
        until: "shadow-cooled",
        fight: { collect: "first" },
      },
      {
        patrol: "stage-loop",
        while: "ghost-hurt",
        until: "ghost-whole",
      },
      {
        patrol: "shadow-south-long",
        while: "south-prowl",
        until: "north-prowl",
      },
      {
        ...floor("shadow-north-long"),
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
      { recover: "auto" },
      {
        follow: "hunter",
        posture: "trail",
        while: "shadow-op",
        until: "shadow-cooled",
      },
      homeMode,
      ...raceModes,
      floor("stage-loop"),
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
        fight: assassinFight,
      },
      ...raceModes,
      surgeMode,
      floor("stage-loop"),
    ],
    conceal: false,
  },
  carrier: {
    custody: "well-custody",
    fight: standardFight,
    modes: [
      { fetch: "well-custody" },
      ...raceModes,
      surgeMode,
      floor("stage-loop"),
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
      },
      homeMode,
      floor("stage-loop"),
    ],
    conceal: false,
  },
  eyes: {
    custody: "well-custody",
    fight: standardFight,
    modes: [
      { fetch: "well-custody" },
      homeMode,
      surgeMode,
      floor("stage-loop"),
    ],
    conceal: false,
  },
  reserve: {
    custody: "well-custody",
    fight: standardFight,
    modes: [
      { fetch: "well-custody" },
      floor({ zone: "own-approach" }),
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
  "south-prowl": {
    fact: "tempo-active", operator: "equals", value: 1, subject: "prowl-south",
  },
  "north-prowl": {
    fact: "tempo-active", operator: "equals", value: 1, subject: "prowl",
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
  layout: source.layout,
  memory: source.memory,
  roles: compactRoles,
  custodyPolicies,
  doctrines: {
    roles: roleDoctrines,
    focus: { maximumAttackers: 2 },
    tempo: {
      cycle: [
        { modeId: "prowl", forTicks: 90 },
        { modeId: "prowl-south", forTicks: 90 },
      ],
    },
  },
  conditions,
};

fs.writeFileSync(target, `${JSON.stringify(migrated, null, 1)}\n`);
console.log(`rebuilt ${path.relative(root, target)} with condition coordination`);
