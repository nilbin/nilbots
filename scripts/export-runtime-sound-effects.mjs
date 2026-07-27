#!/usr/bin/env node

import { execFileSync } from "node:child_process";
import {
  access,
  mkdir,
  readFile,
  readdir,
  rename,
  rm,
  stat,
  writeFile,
} from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const sourceRoot = path.join(repositoryRoot, "art", "audio", "sound-lab");
const destinationRoot = path.join(
  repositoryRoot,
  "web",
  "src",
  "assets",
  "audio",
  "effects",
);
const exportSuffix = `${process.pid}-${Date.now()}`;
const stagingRoot = `${destinationRoot}.staging-${exportSuffix}`;
const backupRoot = `${destinationRoot}.backup-${exportSuffix}`;
const backupPrefix = `${path.basename(destinationRoot)}.backup-`;
const cueNames = new Map([
  ["projectile-showcase", "projectile"],
  ["armor-impact", "impact"],
  ["bot-destroyed", "destroyed"],
]);
const selectedPackId = "obsidian-foundry";

async function pathExists(target) {
  try {
    await access(target);
    return true;
  } catch (error) {
    if (error?.code === "ENOENT") return false;
    throw error;
  }
}

async function recoverInterruptedExport() {
  const destinationParent = path.dirname(destinationRoot);
  await mkdir(destinationParent, { recursive: true });
  const backupDirectories = (await readdir(destinationParent, {
    withFileTypes: true,
  }))
    .filter((entry) => entry.isDirectory() && entry.name.startsWith(backupPrefix))
    .map((entry) => path.join(destinationParent, entry.name));

  if (backupDirectories.length === 0) return;
  if (await pathExists(destinationRoot)) {
    for (const backupDirectory of backupDirectories) {
      await rm(backupDirectory, { recursive: true, force: true });
    }
    return;
  }
  if (backupDirectories.length > 1) {
    throw new Error(
      `Found multiple interrupted sound-effect backups while ${destinationRoot} ` +
        `is missing: ${backupDirectories.join(", ")}`,
    );
  }

  await rename(backupDirectories[0], destinationRoot);
  console.warn(
    `Recovered an interrupted sound-effects export at ${destinationRoot}.`,
  );
}

async function installExport() {
  const hadPreviousExport = await pathExists(destinationRoot);
  if (hadPreviousExport) {
    await rename(destinationRoot, backupRoot);
  }

  try {
    await rename(stagingRoot, destinationRoot);
  } catch (installError) {
    if (hadPreviousExport && !(await pathExists(destinationRoot))) {
      try {
        await rename(backupRoot, destinationRoot);
      } catch (rollbackError) {
        throw new AggregateError(
          [installError, rollbackError],
          `Could not install the sound effects export or restore ${destinationRoot}. ` +
            `The previous export remains at ${backupRoot}.`,
        );
      }
    }
    throw installError;
  }

  if (hadPreviousExport) {
    await rm(backupRoot, { recursive: true, force: true });
  }
}

await recoverInterruptedExport();

if (process.platform !== "darwin") {
  throw new Error(
    "Sound-effect AAC export currently requires macOS afconvert. " +
    "The checked-in assets remain buildable on every platform.",
  );
}
const sourceManifest = JSON.parse(
  await readFile(path.join(sourceRoot, "manifest-v2.json"), "utf8"),
);
if (!Array.isArray(sourceManifest.packs)) {
  throw new Error("Expected V2 source packs.");
}

const pack = sourceManifest.packs.find(
  (candidate) => candidate.id === selectedPackId,
);
if (!pack) throw new Error(`Missing selected source pack: ${selectedPackId}`);

let totalBytes = 0;
let fileCount = 0;
if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(pack.id)) {
  throw new Error(`Unsafe pack ID: ${pack.id}`);
}
const directory = path.join(stagingRoot, pack.id);

try {
  await mkdir(directory, { recursive: true });
  const cues = {};
  for (const sourceCue of pack.cues) {
    const runtimeCue = cueNames.get(sourceCue.id);
    if (!runtimeCue) continue;
    const sourceFile = path.resolve(
      sourceRoot,
      sourceCue.file.replace(/^\.\//, ""),
    );
    if (!sourceFile.startsWith(`${sourceRoot}${path.sep}`)) {
      throw new Error(`Source cue escapes the audio root: ${sourceCue.file}`);
    }
    const filename = `${runtimeCue}.m4a`;
    const destination = path.join(directory, filename);
    execFileSync(
      "afconvert",
      [
        sourceFile,
        "-o",
        destination,
        "-f",
        "m4af",
        "-d",
        "aac",
        "-b",
        "160000",
        "-q",
        "127",
        "--no-filler",
      ],
      { stdio: "inherit" },
    );
    const outputBytes = (await stat(destination)).size;
    if (outputBytes === 0) {
      throw new Error(`Exported an empty runtime cue: ${destination}`);
    }
    totalBytes += outputBytes;
    fileCount++;
    cues[runtimeCue] = filename;
  }
  if (Object.keys(cues).length !== cueNames.size) {
    throw new Error(
      `Expected ${cueNames.size} selected runtime cues, found ${Object.keys(cues).length}.`,
    );
  }
  await writeFile(
    path.join(directory, "manifest.json"),
    `${JSON.stringify(
      {
        version: 1,
        id: pack.id,
        label: pack.label,
        approval: "approved",
        format: "aac-lc-m4a",
        sampleRate: 48_000,
        channels: 2,
        provenance: {
          generatedBy: sourceManifest.generatedBy,
          rightsStatus: "rights-cleared",
          shipApproval: "approved",
        },
        cues,
      },
      null,
      2,
    )}\n`,
  );
  await installExport();
} finally {
  await rm(stagingRoot, { recursive: true, force: true });
}

console.log(
  `Exported ${fileCount} approved runtime sound effects ` +
    `(${(totalBytes / 1_048_576).toFixed(2)} MiB) to ` +
    path.relative(repositoryRoot, destinationRoot),
);
