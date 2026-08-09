#!/usr/bin/env node

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const playbookPath = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/wellwright-v1.json",
);
const source = JSON.parse(fs.readFileSync(playbookPath, "utf8"));

const clone = (value) => structuredClone(value);
const focus = (maximumAttackers) => ({ maximumAttackers });

const memory = clone(source.memory);
delete memory.formationStableTicks;

const custodyPolicies = clone(source.custodyPolicies).map((policy) => {
  delete policy.escortGroups;
  delete policy.escorts;
  return policy;
});

const haulFight = {
  targets: { prefer: ["carrier", "anchor", "weakest"] },
  chase: { leash: 1, persistTicks: 1 },
  defense: { radius: 3, return: true },
};
const aidFight = {
  targets: { prefer: ["carrier", "weakest"] },
  chase: { leash: 0, persistTicks: 1 },
  defense: { radius: 3, return: true },
};
const reserveFight = {
  targets: { prefer: ["carrier", "weakest"] },
  chase: { leash: 2, persistTicks: 1 },
  defense: { radius: 3, return: true },
};
const repair = {
  prefer: ["carrier", "weakest", "any"],
  maximumProvidersPerTarget: 1,
  minimumProviderSeparation: 2,
  reserveHealthPercent: 30,
  survivalFallback: "evade",
};

function wellModes(posture, sharedFocus, useHaulCohorts, canFetch) {
  const modes = [];
  if (canFetch) modes.push({ fetch: "farm-custody" });
  for (const well of ["north", "centre", "south"]) {
    const mode = {
      patrol: `${well}-well-loop`,
      while: `${well}-birth-soon`,
      until: `${well}-birth-far`,
      focus: sharedFocus,
      posture,
    };
    if (useHaulCohorts) mode.cohort = `${well}-race`;
    modes.push(mode);
  }
  modes.push({ patrol: "farm-loop", focus: sharedFocus, posture });
  return modes;
}

const migrated = {
  schema: source.schema,
  playbookId: source.playbookId,
  auditStatus: source.auditStatus,
  composition: source.composition,
  layout: source.layout,
  memory,
  roles: source.roles,
  custodyPolicies,
  doctrines: {
    roles: {
      hauler: {
        custody: "farm-custody",
        fight: haulFight,
        modes: wellModes("core", "haul", true, true),
        conceal: false,
      },
      runner: {
        custody: "farm-custody",
        fight: haulFight,
        modes: wellModes("core", "haul", true, true),
        conceal: false,
      },
      escort: {
        custody: "farm-custody",
        fight: haulFight,
        modes: wellModes("front", "haul", true, true),
        conceal: false,
      },
      eyes: {
        custody: "farm-custody",
        fight: aidFight,
        modes: wellModes("core", "aid", false, false),
        conceal: false,
      },
      medic: {
        custody: "farm-custody",
        fight: aidFight,
        repair,
        modes: wellModes("rear", "aid", false, false),
        conceal: false,
      },
      reserve: {
        custody: "farm-custody",
        fight: reserveFight,
        modes: wellModes("rear", focus(2), false, true),
        conceal: false,
      },
    },
    cohorts: {
      "north-race": { take: 3, select: "nearest" },
      "centre-race": { take: 3, select: "nearest" },
      "south-race": { take: 3, select: "nearest" },
    },
    focus: {
      haul: { maximumAttackers: 3 },
      aid: { maximumAttackers: 2 },
    },
  },
  conditions: {
    "north-birth-soon": {
      fact: "well-ticks-until-birth",
      operator: "at-most",
      value: 18,
      subject: "north",
    },
    "north-birth-far": {
      fact: "well-ticks-until-birth",
      operator: "at-least",
      value: 45,
      subject: "north",
    },
    "centre-birth-soon": {
      fact: "well-ticks-until-birth",
      operator: "at-most",
      value: 18,
      subject: "centre",
    },
    "centre-birth-far": {
      fact: "well-ticks-until-birth",
      operator: "at-least",
      value: 45,
      subject: "centre",
    },
    "south-birth-soon": {
      fact: "well-ticks-until-birth",
      operator: "at-most",
      value: 18,
      subject: "south",
    },
    "south-birth-far": {
      fact: "well-ticks-until-birth",
      operator: "at-least",
      value: 45,
      subject: "south",
    },
  },
};

const text = `${JSON.stringify(migrated, null, 1)}\n`;
fs.writeFileSync(playbookPath, text);
console.log(
  `wrote wellwright-v1 (${text.split("\n").length - 1} lines, `
    + `${crypto.createHash("sha256").update(text).digest("hex")})`,
);
