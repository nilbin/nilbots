#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const playbooksRoot = path.join(
  repoRoot,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks",
);
const checkOnly = process.argv.includes("--check");
const fixedChannels = [
  "custody-emergency",
  "self-preservation",
  "repair",
  "signature",
  "focus-fire",
  "movement",
  "facing",
  "hold",
];

function detectIndent(source) {
  const match = source.match(/\n( +)"/);
  return match ? match[1].length : 2;
}

function encodeJson(value, indent) {
  return `${JSON.stringify(value, null, indent)}\n`;
}

function sameArray(left, right) {
  return left.length === right.length
    && left.every((value, index) => value === right[index]);
}

function doctrineForRole(doctrines, roleId) {
  return Object.entries(doctrines).find(([, value]) => value.role === roleId);
}

function uniqueDoctrineId(doctrines, roleId) {
  const base = `${roleId}-custody`;
  if (!(base in doctrines)) return base;
  for (let ordinal = 2; ; ordinal += 1) {
    const candidate = `${base}-${ordinal}`;
    if (!(candidate in doctrines)) return candidate;
  }
}

function migrate(source, fileName) {
  // Named byte-frozen evidence control. Its old source dialect remains
  // executable through the compiler's exact compatibility seam.
  if (source.playbookId === "home-siege-v2") return source;

  if (source.arbitration) {
    if (source.arbitration.mode !== "first-legal"
      || !sameArray(source.arbitration.channels ?? [], fixedChannels)) {
      throw new Error(
        `${fileName}: arbitration does not match the fixed executor order`,
      );
    }
    delete source.arbitration;
  }

  const policies = source.custodyPolicies ?? [];
  const policiesByRole = new Map();
  let hasLegacyRoster = false;
  for (const policy of policies) {
    if (!("authorizedCarrierRoles" in policy)) continue;
    hasLegacyRoster = true;
    for (const roleId of policy.authorizedCarrierRoles) {
      const rolePolicies = policiesByRole.get(roleId) ?? [];
      rolePolicies.push(policy.custodyId);
      policiesByRole.set(roleId, rolePolicies);
    }
  }

  if (hasLegacyRoster) {
    source.doctrines ??= {};
    for (const role of source.roles ?? []) {
      const custodyIds = policiesByRole.get(role.roleId) ?? [];
      if (custodyIds.length === 0) continue;
      let entry = doctrineForRole(source.doctrines, role.roleId);
      if (!entry) {
        const id = uniqueDoctrineId(source.doctrines, role.roleId);
        source.doctrines[id] = {
          role: role.roleId,
          custody: custodyIds[0],
          modes: [{ squad: true }],
        };
        entry = [id, source.doctrines[id]];
      }
      const doctrine = entry[1];
      const existing = new Set(
        (doctrine.modes ?? [])
          .filter((mode) => "fetch" in mode)
          .map((mode) => mode.fetch),
      );
      const additions = custodyIds
        .filter((custodyId) => !existing.has(custodyId))
        .map((custodyId) => ({ fetch: custodyId }));
      // Inserting at the top leaves every existing mode on the same numeric
      // rung: list length and each old index increase together. It therefore
      // makes the formerly unconditional pickup override explicit without
      // moving recover/muster/patrol relative to one another.
      doctrine.modes = [...additions, ...(doctrine.modes ?? [])];
      if (doctrine.modes.length > 8) {
        throw new Error(
          `${fileName}: ${role.roleId} needs more than eight doctrine modes`,
        );
      }
    }
    for (const policy of policies) delete policy.authorizedCarrierRoles;
  }

  return source;
}

const writes = [];
for (const fileName of fs
  .readdirSync(playbooksRoot)
  .filter((entry) => entry.endsWith(".json"))
  .sort()) {
  const target = path.join(playbooksRoot, fileName);
  const original = fs.readFileSync(target, "utf8");
  const migrated = migrate(JSON.parse(original), fileName);
  writes.push([
    target,
    migrated.playbookId === "home-siege-v2"
      ? original
      : encodeJson(migrated, detectIndent(original)),
  ]);
}

let changed = 0;
for (const [target, expected] of writes) {
  const actual = fs.readFileSync(target, "utf8");
  if (actual === expected) continue;
  changed += 1;
  if (!checkOnly) fs.writeFileSync(target, expected);
}

if (checkOnly && changed > 0) {
  console.error(`${changed} sheet DSL source(s) need phase-2 migration`);
  process.exitCode = 1;
} else {
  console.log(
    checkOnly
      ? "phase-2 sheet DSL sources are current"
      : `migrated ${changed} sheet DSL source(s)`,
  );
}
