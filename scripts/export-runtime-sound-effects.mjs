#!/usr/bin/env node

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
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
const sourceManifestPath = path.join(sourceRoot, "manifest-v5.json");
const generationRecordPath = path.join(
  sourceRoot,
  "v5",
  "sfx-engine-generation.json",
);
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
const selectedPackId = "astral-relay";
const selectedAmbienceId = "arena-ambience";
const sourceCueIds = [
  "attack-windup",
  "projectile",
  "attack-fizzle",
  "attack-cancel",
  "impact",
  "destroyed",
  "level-up",
  "arc-birth",
  "arc-pickup",
  "arc-steal",
  "arc-drop",
  "arc-bank",
  "arc-pulse",
  "arc-handoff",
  "deflect",
  "arrival",
];
const cueIds = [
  "attack-windup",
  "projectile",
  "impact",
  "destroyed",
  "level-up",
  "arc-birth",
  "arc-pickup",
  "arc-drop",
  "arc-bank",
  "arc-pulse",
  "arc-handoff",
  "deflect",
  "arrival",
];
const mix = {
  roomSend: 0.06,
  cues: {
    "attack-windup": { gain: 0.39, voiceLimit: 3 },
    projectile: { gain: 0.52, voiceLimit: 3 },
    impact: { gain: 0.38, voiceLimit: 2 },
    destroyed: { gain: 0.44, voiceLimit: 1 },
    "level-up": { gain: 0.46, voiceLimit: 2 },
    "arc-birth": { gain: 0.4, voiceLimit: 2 },
    "arc-pickup": { gain: 0.3, voiceLimit: 2 },
    "arc-drop": { gain: 0.3, voiceLimit: 2 },
    "arc-bank": { gain: 0.5, voiceLimit: 1 },
    "arc-pulse": { gain: 0.54, voiceLimit: 1 },
    "arc-handoff": { gain: 0.38, voiceLimit: 2 },
    deflect: { gain: 0.34, voiceLimit: 2 },
    arrival: { gain: 0.38, voiceLimit: 3 },
  },
};
const ambience = {
  id: "original-dark-field",
  sourceCueId: selectedAmbienceId,
  file: "arena-ambience.m4a",
  loop: true,
  durationSeconds: 30,
  gain: 0.21,
  mapIds: ["arc-relay-tapline-01"],
};

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
  if (hadPreviousExport) await rename(destinationRoot, backupRoot);

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

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

/**
 * `afconvert` writes the current Mac epoch time into mvhd/tkhd/mdhd even when
 * the encoded AAC payload is byte-identical. Zero those standard creation and
 * modification fields so a hash identifies the audio/export recipe, not the
 * wall clock on the machine that ran it.
 */
function canonicalizeM4aTimestamps(bytes) {
  const normalized = Buffer.from(bytes);
  const timestampBoxes = new Set(["mvhd", "tkhd", "mdhd"]);
  const containers = new Set(["moov", "trak", "mdia"]);
  let canonicalized = 0;

  function walk(start, end) {
    let offset = start;
    while (offset + 8 <= end) {
      let size = normalized.readUInt32BE(offset);
      const type = normalized.toString("ascii", offset + 4, offset + 8);
      let headerSize = 8;
      if (size === 1) {
        if (offset + 16 > end)
          throw new Error(`Truncated extended M4A box '${type}'.`);
        size = Number(normalized.readBigUInt64BE(offset + 8));
        headerSize = 16;
      } else if (size === 0) {
        size = end - offset;
      }
      if (size < headerSize || offset + size > end) {
        throw new Error(`Invalid M4A box '${type}' while canonicalizing timestamps.`);
      }

      const body = offset + headerSize;
      if (timestampBoxes.has(type)) {
        const version = normalized[body];
        const fieldBytes = version === 1 ? 8 : version === 0 ? 4 : 0;
        if (fieldBytes === 0 || body + 4 + fieldBytes * 2 > offset + size) {
          throw new Error(`Unsupported M4A timestamp box '${type}'.`);
        }
        normalized.fill(0, body + 4, body + 4 + fieldBytes * 2);
        canonicalized += 1;
      }
      if (containers.has(type)) walk(body, offset + size);
      offset += size;
    }
  }

  walk(0, normalized.length);
  if (canonicalized !== 3) {
    throw new Error(
      `Expected mvhd/tkhd/mdhd timestamps in M4A export, found ${canonicalized}.`,
    );
  }
  return normalized;
}

function safeSourcePath(relativeFile) {
  const sourceFile = path.resolve(
    sourceRoot,
    relativeFile.replace(/^\.\//, ""),
  );
  if (!sourceFile.startsWith(`${sourceRoot}${path.sep}`)) {
    throw new Error(`Source cue escapes the audio root: ${relativeFile}`);
  }
  return sourceFile;
}

async function exportAsset({
  id,
  sourceFile,
  sourceHash,
  destination,
  bitrate,
}) {
  const sourceBytes = await readFile(sourceFile);
  if (sha256(sourceBytes) !== sourceHash) {
    throw new Error(`${id}: source WAV differs from its generation record.`);
  }
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
      String(bitrate),
      "-q",
      "127",
      "--no-filler",
    ],
    { stdio: "inherit" },
  );
  const outputBytes = canonicalizeM4aTimestamps(await readFile(destination));
  await writeFile(destination, outputBytes);
  if (
    outputBytes.length === 0 ||
    outputBytes.toString("ascii", 4, 8) !== "ftyp"
  ) {
    throw new Error(`Exported an invalid runtime cue: ${destination}`);
  }
  return {
    sourceSha256: sourceHash,
    outputSha256: sha256(outputBytes),
    bytes: outputBytes.length,
    bitrate,
  };
}

await recoverInterruptedExport();

if (process.platform !== "darwin") {
  throw new Error(
    "Sound-effect AAC export currently requires macOS afconvert. " +
      "The checked-in assets remain buildable on every platform.",
  );
}

const [sourceManifestBytes, generationRecordBytes] = await Promise.all([
  readFile(sourceManifestPath),
  readFile(generationRecordPath),
]);
const sourceManifest = JSON.parse(sourceManifestBytes);
const generationRecord = JSON.parse(generationRecordBytes);
const pack = sourceManifest.packs?.find(
  (candidate) => candidate.id === selectedPackId,
);
if (sourceManifest.version !== 5 || !pack) {
  throw new Error(`Missing selected V5 source pack: ${selectedPackId}`);
}
if (generationRecord.pack?.id !== selectedPackId) {
  throw new Error("The SFX Engine generation record names a different pack.");
}
if (
  JSON.stringify(pack.cues.map((cue) => cue.id)) !==
  JSON.stringify(sourceCueIds)
) {
  throw new Error("Astral Relay runtime cue coverage differs from the approved set.");
}
const sourceAmbience = sourceManifest.backgroundLoops?.find(
  (candidate) => candidate.id === selectedAmbienceId,
);
if (!sourceAmbience) {
  throw new Error(`Missing selected runtime ambience: ${selectedAmbienceId}`);
}

let totalBytes = 0;
let fileCount = 0;
const directory = path.join(stagingRoot, selectedPackId);
const cues = {};
const integrity = {};

try {
  await mkdir(directory, { recursive: true });
  for (const cueId of cueIds) {
    const sourceCue = pack.cues.find((candidate) => candidate.id === cueId);
    const generationCue = generationRecord.cues.find(
      (candidate) => candidate.id === cueId,
    );
    if (!sourceCue || !generationCue?.result?.outputSha256) {
      throw new Error(`${cueId}: source manifest and generation record disagree.`);
    }
    const filename = `${cueId}.m4a`;
    const result = await exportAsset({
      id: cueId,
      sourceFile: safeSourcePath(sourceCue.file),
      sourceHash: generationCue.result.outputSha256,
      destination: path.join(directory, filename),
      bitrate: 192_000,
    });
    cues[cueId] = filename;
    integrity[cueId] = result;
    totalBytes += result.bytes;
    fileCount += 1;
  }

  const generationAmbience = generationRecord.cues.find(
    (candidate) => candidate.id === selectedAmbienceId,
  );
  if (!generationAmbience?.result?.outputSha256) {
    throw new Error(`${selectedAmbienceId}: generation provenance is missing.`);
  }
  integrity[selectedAmbienceId] = await exportAsset({
    id: selectedAmbienceId,
    sourceFile: safeSourcePath(sourceAmbience.file),
    sourceHash: generationAmbience.result.outputSha256,
    destination: path.join(directory, ambience.file),
    bitrate: 160_000,
  });
  totalBytes += integrity[selectedAmbienceId].bytes;
  fileCount += 1;

  await writeFile(
    path.join(directory, "manifest.json"),
    `${JSON.stringify(
      {
        version: 2,
        id: pack.id,
        label: pack.label,
        approval: "approved",
        format: "aac-lc-m4a",
        sampleRate: 48_000,
        channels: 2,
        provenance: {
          provider: "sfx-engine",
          generatedBy: sourceManifest.generatedBy,
          exportedBy: "scripts/export-runtime-sound-effects.mjs",
          generationRecord:
            "art/audio/sound-lab/v5/sfx-engine-generation.json",
          generationRecordSha256: sha256(generationRecordBytes),
          deliveryEncoding: {
            encoder: "macos-afconvert",
            canonicalization:
              "zero-iso-bmff-creation-and-modification-times",
          },
          rightsStatus: "rights-cleared",
          license: {
            termsUrl: "https://sfxengine.com/terms-of-service",
            checkedAt: "2026-08-11",
            permittedUse: "personal-and-commercial-projects",
            standaloneRedistribution: "prohibited",
          },
          shipApproval: "approved",
          sourceComposition: sourceManifest.sourceComposition,
        },
        mix,
        behavior: {
          silentOutcomeCueIds: ["attack-fizzle", "attack-cancel"],
          cueAliases: { "arc-steal": "arc-pickup" },
        },
        cues,
        ambience,
        integrity,
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
  `Exported ${fileCount} approved Astral Relay runtime audio assets ` +
    `(${(totalBytes / 1_048_576).toFixed(2)} MiB) to ` +
    path.relative(repositoryRoot, destinationRoot),
);
