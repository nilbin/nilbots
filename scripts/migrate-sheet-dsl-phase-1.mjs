#!/usr/bin/env node

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const packageRoot = path.join(
  repoRoot,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03",
);
const playbooksRoot = path.join(packageRoot, "playbooks");
const libraryPath = path.join(packageRoot, "library/standard-v1.json");
const checkOnly = process.argv.includes("--check");

function detectIndent(source) {
  const match = source.match(/\n( +)"/);
  return match ? match[1].length : 2;
}

function encodeJson(value, indent) {
  return `${JSON.stringify(value, null, indent)}\n`;
}

function sha256(bytes) {
  return crypto.createHash("sha256").update(bytes).digest("hex");
}

function conditionText(conditionSets, id, context) {
  if (id === "") return null;
  const alternatives = conditionSets[id];
  if (!Array.isArray(alternatives) || alternatives.length === 0) {
    throw new Error(`${context}: unknown or empty condition set '${id}'`);
  }
  return alternatives
    .map((all) => {
      if (!Array.isArray(all) || all.length === 0) {
        throw new Error(`${context}: condition set '${id}' has an empty arm`);
      }
      return all.join(" and ");
    })
    .join(" or ");
}

function inlineReference(
  owner,
  legacyName,
  authoredName,
  conditionSets,
  context,
) {
  if (!(legacyName in owner)) return;
  const text = conditionText(conditionSets, owner[legacyName], context);
  delete owner[legacyName];
  if (text !== null) owner[authoredName] = text;
}

function migratePlaybook(source, fileName, librarySha256) {
  // This named evidence control is byte-frozen by
  // evidence/home-siege-v2-reference-freeze.json. It remains executable as
  // history and is the only old-grammar compatibility exception.
  if (source.playbookId === "home-siege-v2") return source;

  const conditionSets = source.authoring?.conditionSets;
  if (!conditionSets || typeof conditionSets !== "object") {
    const serialized = JSON.stringify(source);
    if (/ConditionSetId|conditionSetId/.test(serialized)) {
      throw new Error(
        `${fileName}: has legacy condition references but no condition catalog`,
      );
    }
    if (source.authoring?.library) {
      source.authoring.library.sha256 = librarySha256;
    }
    return source;
  }

  for (const doctrine of Object.values(source.doctrines ?? {})) {
    if ("collect" in doctrine) {
      if ("scavenge" in doctrine) {
        throw new Error(`${fileName}: doctrine has collect and scavenge`);
      }
      doctrine.scavenge = doctrine.collect;
      delete doctrine.collect;
    }
  }

  for (const policy of source.custodyPolicies ?? []) {
    inlineReference(
      policy,
      "safeConversionConditionSetId",
      "safeConversion",
      conditionSets,
      `${fileName}: custody ${policy.custodyId}`,
    );
    inlineReference(
      policy,
      "emergencyRecoveryConditionSetId",
      "emergencyRecovery",
      conditionSets,
      `${fileName}: custody ${policy.custodyId}`,
    );
    if (policy.baitDrop) {
      inlineReference(
        policy.baitDrop,
        "reclaimConditionSetId",
        "reclaim",
        conditionSets,
        `${fileName}: custody ${policy.custodyId} baitDrop`,
      );
    }
  }

  for (const group of source.groups ?? []) {
    for (const state of group.localStateMachine?.states ?? []) {
      for (const transition of state.transitions ?? []) {
        inlineReference(
          transition,
          "conditionSetId",
          "when",
          conditionSets,
          `${fileName}: group ${group.groupId}`,
        );
      }
    }
  }

  for (const engagement of source.engagements ?? []) {
    if (engagement.holdFire) {
      inlineReference(
        engagement.holdFire,
        "releaseConditionSetId",
        "release",
        conditionSets,
        `${fileName}: engagement ${engagement.engagementId}`,
      );
    }
  }

  for (const task of source.coordination?.tasks ?? []) {
    const context = `${fileName}: task ${task.taskId}`;
    inlineReference(
      task,
      "whenConditionSetId",
      "when",
      conditionSets,
      context,
    );
    inlineReference(
      task,
      "completeConditionSetId",
      "complete",
      conditionSets,
      context,
    );
    inlineReference(
      task,
      "failConditionSetId",
      "fail",
      conditionSets,
      context,
    );
    inlineReference(
      task,
      "completionArmConditionSetId",
      "completionArm",
      conditionSets,
      context,
    );
    if (task.reintegration) {
      inlineReference(
        task.reintegration,
        "completeConditionSetId",
        "complete",
        conditionSets,
        `${context} reintegration`,
      );
    }
  }

  for (const phase of source.coordination?.phases ?? []) {
    for (const transition of phase.transitions ?? []) {
      inlineReference(
        transition,
        "conditionSetId",
        "when",
        conditionSets,
        `${fileName}: phase ${phase.phaseId}`,
      );
    }
  }

  delete source.authoring.conditionSets;
  if (source.authoring.library) {
    source.authoring.library.sha256 = librarySha256;
  }
  return source;
}

const writes = [];
const originalLibrary = fs.readFileSync(libraryPath, "utf8");
const library = JSON.parse(originalLibrary);
delete library.conditionSets;
const libraryBytes = encodeJson(library, detectIndent(originalLibrary));
const librarySha256 = sha256(libraryBytes);
writes.push([libraryPath, libraryBytes]);

for (const fileName of fs
  .readdirSync(playbooksRoot)
  .filter((entry) => entry.endsWith(".json"))
  .sort()) {
  const playbookPath = path.join(playbooksRoot, fileName);
  const original = fs.readFileSync(playbookPath, "utf8");
  const parsed = JSON.parse(original);
  const migrated = migratePlaybook(parsed, fileName, librarySha256);
  writes.push([
    playbookPath,
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
  console.error(`${changed} sheet DSL source(s) need phase-1 migration`);
  process.exitCode = 1;
} else {
  console.log(
    checkOnly
      ? "phase-1 sheet DSL sources are current"
      : `migrated ${changed} sheet DSL source(s)`,
  );
}
