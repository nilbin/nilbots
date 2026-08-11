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
const selectedAmbienceId = "arena-ambience-ion-veil";
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
    "attack-windup": {
      gain: 0.24,
      voiceLimit: 1,
      panWidth: 0.28,
      roomSend: false,
    },
    projectile: {
      gain: 0.28,
      voiceLimit: 1,
      panWidth: 0.28,
      roomSend: false,
    },
    impact: { gain: 0.38, voiceLimit: 2, panWidth: 0.7, roomSend: true },
    destroyed: { gain: 0.44, voiceLimit: 1, panWidth: 0.7, roomSend: true },
    "level-up": { gain: 0.46, voiceLimit: 2, panWidth: 0.7, roomSend: true },
    "arc-birth": { gain: 0.4, voiceLimit: 2, panWidth: 0.7, roomSend: true },
    "arc-pickup": { gain: 0.3, voiceLimit: 2, panWidth: 0.7, roomSend: true },
    "arc-drop": { gain: 0.3, voiceLimit: 2, panWidth: 0.7, roomSend: true },
    "arc-bank": { gain: 0.5, voiceLimit: 1, panWidth: 0.7, roomSend: true },
    "arc-pulse": { gain: 0.54, voiceLimit: 1, panWidth: 0.7, roomSend: true },
    "arc-handoff": { gain: 0.38, voiceLimit: 2, panWidth: 0.7, roomSend: true },
    deflect: { gain: 0.34, voiceLimit: 2, panWidth: 0.7, roomSend: true },
    arrival: { gain: 0.38, voiceLimit: 3, panWidth: 0.7, roomSend: true },
  },
};
const combatDeliveryProcessing = {
  "attack-windup": {
    channelTransform: "side-to-dual-mono",
    durationSeconds: 0.24,
    lowPassHz: 3_200,
    fadeInSeconds: 0.03,
    fadeOutSeconds: 0.08,
    targetSamplePeak: 0.28,
  },
  projectile: {
    channelTransform: "mid-to-dual-mono",
    durationSeconds: 0.16,
    lowPassHz: 2_600,
    fadeInSeconds: 0.018,
    fadeOutSeconds: 0.07,
    targetSamplePeak: 0.28,
  },
};
const ambience = {
  id: "ion-veil",
  sourceCueId: selectedAmbienceId,
  file: "arena-ambience-ion-veil.m4a",
  loop: true,
  durationSeconds: 30,
  gain: 0.08,
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
  deliveryProcessing,
}) {
  const sourceBytes = await readFile(sourceFile);
  if (sha256(sourceBytes) !== sourceHash) {
    throw new Error(`${id}: source WAV differs from its generation record.`);
  }
  const temporaryWav = `${destination}.delivery.wav`;
  let encodingSource = sourceFile;
  let deliveryMetrics;
  if (deliveryProcessing) {
    const shaped = shapeCombatCue(sourceBytes, id, deliveryProcessing);
    await writeFile(temporaryWav, shaped.bytes);
    encodingSource = temporaryWav;
    deliveryMetrics = shaped.metrics;
    const effectivePeakDbfs =
      deliveryMetrics.samplePeakDbfs + 20 * Math.log10(mix.cues[id].gain);
    const effectiveRmsDbfs =
      deliveryMetrics.rmsDbfs + 20 * Math.log10(mix.cues[id].gain);
    if (effectivePeakDbfs > -20) {
      throw new Error(
        `${id}: ${effectivePeakDbfs.toFixed(2)} dBFS effective delivery peak ` +
          "exceeds the -20 dBFS combat-cue ceiling.",
      );
    }
    deliveryMetrics.effectiveSamplePeakDbfs = round(effectivePeakDbfs, 2);
    deliveryMetrics.effectiveRmsDbfs = round(effectiveRmsDbfs, 2);
  }
  try {
    execFileSync(
      "afconvert",
      [
        encodingSource,
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
  } finally {
    if (deliveryProcessing) {
      await rm(temporaryWav, { force: true });
    }
  }
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
    ...(deliveryMetrics ? { deliveryMetrics } : {}),
  };
}

function shapeCombatCue(sourceBytes, id, processing) {
  const wav = parsePcm16Wav(sourceBytes, id);
  if (wav.sampleRate !== 48_000 || wav.channels !== 2) {
    throw new Error(`${id}: combat delivery shaping requires 48 kHz stereo PCM.`);
  }
  const source = decodePcm16(sourceBytes, wav);
  const sourceInterchannelCorrelation = interchannelCorrelation(
    source,
    wav.channels,
  );
  const frameCount = Math.round(
    processing.durationSeconds * wav.sampleRate,
  );
  if (frameCount <= 0 || frameCount > source.length / wav.channels) {
    throw new Error(`${id}: combat delivery window exceeds the source cue.`);
  }
  const samples = new Float64Array(frameCount * wav.channels);
  for (let frame = 0; frame < frameCount; frame += 1) {
    const left = source[frame * 2];
    const right = source[frame * 2 + 1];
    const mono =
      processing.channelTransform === "side-to-dual-mono"
        ? (left - right) * 0.5
        : processing.channelTransform === "mid-to-dual-mono"
          ? (left + right) * 0.5
          : Number.NaN;
    if (!Number.isFinite(mono)) {
      throw new Error(`${id}: unknown combat channel transform.`);
    }
    samples[frame * 2] = mono;
    samples[frame * 2 + 1] = mono;
  }
  removeDc(samples, wav.channels);
  lowPassZeroPhase(
    samples,
    wav.channels,
    wav.sampleRate,
    processing.lowPassHz,
  );
  applyEndpointFades(
    samples,
    wav.channels,
    wav.sampleRate,
    processing.fadeInSeconds,
    processing.fadeOutSeconds,
  );
  removeDc(samples, wav.channels);
  normalizePeak(samples, processing.targetSamplePeak);
  const bytes = encodePcm16Wav(samples, wav.sampleRate, wav.channels);
  return {
    bytes,
    metrics: {
      ...analyzeDeliveryWav(bytes, id),
      sourceInterchannelCorrelation: round(
        sourceInterchannelCorrelation,
        4,
      ),
    },
  };
}

function parsePcm16Wav(bytes, label) {
  if (
    bytes.toString("ascii", 0, 4) !== "RIFF" ||
    bytes.toString("ascii", 8, 12) !== "WAVE"
  ) {
    throw new Error(`${label}: source is not RIFF/WAVE audio.`);
  }
  let offset = 12;
  let format;
  let dataOffset;
  let dataLength;
  while (offset + 8 <= bytes.length) {
    const chunkId = bytes.toString("ascii", offset, offset + 4);
    const length = bytes.readUInt32LE(offset + 4);
    const body = offset + 8;
    if (chunkId === "fmt ") {
      format = {
        audioFormat: bytes.readUInt16LE(body),
        channels: bytes.readUInt16LE(body + 2),
        sampleRate: bytes.readUInt32LE(body + 4),
        bitsPerSample: bytes.readUInt16LE(body + 14),
      };
    } else if (chunkId === "data") {
      dataOffset = body;
      dataLength = length;
    }
    offset = body + length + (length % 2);
  }
  if (
    !format ||
    dataOffset === undefined ||
    dataLength === undefined ||
    format.audioFormat !== 1 ||
    format.bitsPerSample !== 16 ||
    dataOffset + dataLength > bytes.length
  ) {
    throw new Error(`${label}: source must be valid signed 16-bit PCM.`);
  }
  return {
    ...format,
    dataOffset,
    sampleCount: dataLength / 2,
  };
}

function decodePcm16(bytes, wav) {
  const samples = new Float64Array(wav.sampleCount);
  for (let index = 0; index < samples.length; index += 1) {
    samples[index] =
      bytes.readInt16LE(wav.dataOffset + index * 2) / 32_768;
  }
  return samples;
}

function removeDc(samples, channels) {
  const frames = samples.length / channels;
  const means = new Float64Array(channels);
  for (let index = 0; index < samples.length; index += 1) {
    means[index % channels] += samples[index];
  }
  for (let channel = 0; channel < channels; channel += 1) {
    means[channel] /= frames;
  }
  for (let index = 0; index < samples.length; index += 1) {
    samples[index] -= means[index % channels];
  }
}

function lowPassZeroPhase(samples, channels, sampleRate, cutoffHz) {
  const frames = samples.length / channels;
  const alpha = 1 - Math.exp((-2 * Math.PI * cutoffHz) / sampleRate);
  for (let channel = 0; channel < channels; channel += 1) {
    let state = samples[channel];
    for (let frame = 1; frame < frames; frame += 1) {
      const index = frame * channels + channel;
      state += alpha * (samples[index] - state);
      samples[index] = state;
    }
    state = samples[(frames - 1) * channels + channel];
    for (let frame = frames - 2; frame >= 0; frame -= 1) {
      const index = frame * channels + channel;
      state += alpha * (samples[index] - state);
      samples[index] = state;
    }
  }
}

function applyEndpointFades(
  samples,
  channels,
  sampleRate,
  fadeInSeconds,
  fadeOutSeconds,
) {
  const frames = samples.length / channels;
  const fadeInFrames = Math.min(
    frames,
    Math.round(fadeInSeconds * sampleRate),
  );
  const fadeOutFrames = Math.min(
    frames,
    Math.round(fadeOutSeconds * sampleRate),
  );
  for (let frame = 0; frame < frames; frame += 1) {
    const fadeIn =
      fadeInFrames === 0 ? 1 : Math.min(1, frame / fadeInFrames);
    const remaining = frames - 1 - frame;
    const fadeOut =
      fadeOutFrames === 0 ? 1 : Math.min(1, remaining / fadeOutFrames);
    const gain = Math.sin(Math.min(fadeIn, fadeOut) * Math.PI * 0.5);
    for (let channel = 0; channel < channels; channel += 1) {
      samples[frame * channels + channel] *= gain;
    }
  }
}

function normalizePeak(samples, targetPeak) {
  let peak = 0;
  for (const sample of samples) peak = Math.max(peak, Math.abs(sample));
  if (peak <= 0.0001) {
    throw new Error("Combat delivery audio is effectively silent.");
  }
  const gain = targetPeak / peak;
  for (let index = 0; index < samples.length; index += 1) {
    samples[index] *= gain;
  }
}

function encodePcm16Wav(samples, sampleRate, channels) {
  const pcm = Buffer.alloc(samples.length * 2);
  for (let index = 0; index < samples.length; index += 1) {
    const sample = Math.max(-1, Math.min(1, samples[index]));
    pcm.writeInt16LE(Math.round(sample * 32_767), index * 2);
  }
  const header = Buffer.alloc(44);
  header.write("RIFF", 0, "ascii");
  header.writeUInt32LE(36 + pcm.length, 4);
  header.write("WAVE", 8, "ascii");
  header.write("fmt ", 12, "ascii");
  header.writeUInt32LE(16, 16);
  header.writeUInt16LE(1, 20);
  header.writeUInt16LE(channels, 22);
  header.writeUInt32LE(sampleRate, 24);
  header.writeUInt32LE(sampleRate * channels * 2, 28);
  header.writeUInt16LE(channels * 2, 32);
  header.writeUInt16LE(16, 34);
  header.write("data", 36, "ascii");
  header.writeUInt32LE(pcm.length, 40);
  return Buffer.concat([header, pcm]);
}

function analyzeDeliveryWav(bytes, label) {
  const wav = parsePcm16Wav(bytes, label);
  const frames = wav.sampleCount / wav.channels;
  let peak = 0;
  let leftSquares = 0;
  let rightSquares = 0;
  let cross = 0;
  for (let frame = 0; frame < frames; frame += 1) {
    const left =
      bytes.readInt16LE(wav.dataOffset + frame * wav.channels * 2) /
      32_768;
    const right =
      bytes.readInt16LE(
        wav.dataOffset + (frame * wav.channels + 1) * 2,
      ) / 32_768;
    peak = Math.max(peak, Math.abs(left), Math.abs(right));
    leftSquares += left * left;
    rightSquares += right * right;
    cross += left * right;
  }
  const correlation = cross / Math.sqrt(leftSquares * rightSquares);
  const rms = Math.sqrt((leftSquares + rightSquares) / (frames * 2));
  return {
    durationSeconds: round(frames / wav.sampleRate, 3),
    samplePeakDbfs: round(20 * Math.log10(peak), 2),
    rmsDbfs: round(20 * Math.log10(rms), 2),
    interchannelCorrelation: round(correlation, 4),
  };
}

function interchannelCorrelation(samples, channels) {
  if (channels !== 2) {
    throw new Error("Stereo correlation requires two channels.");
  }
  let leftSquares = 0;
  let rightSquares = 0;
  let cross = 0;
  for (let frame = 0; frame < samples.length / channels; frame += 1) {
    const left = samples[frame * channels];
    const right = samples[frame * channels + 1];
    leftSquares += left * left;
    rightSquares += right * right;
    cross += left * right;
  }
  return cross / Math.sqrt(leftSquares * rightSquares);
}

function round(value, decimals) {
  return Number(value.toFixed(decimals));
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
const runtimeIntegratedLufs =
  sourceAmbience.energy?.integratedLufs + 20 * Math.log10(ambience.gain);
const runtimeMaximumMomentaryLufs =
  sourceAmbience.energy?.maxMomentaryLufs + 20 * Math.log10(ambience.gain);
if (
  !Number.isFinite(runtimeIntegratedLufs) ||
  !Number.isFinite(runtimeMaximumMomentaryLufs) ||
  runtimeIntegratedLufs > -40 ||
  runtimeMaximumMomentaryLufs > -35.5
) {
  throw new Error(
    "The selected runtime ambience is not below the near-subliminal loudness ceilings.",
  );
}
if (
  JSON.stringify(sourceManifest.reviewScope?.selectedBackgroundLoop) !==
  JSON.stringify({
    cueId: ambience.sourceCueId,
    runtimeGain: ambience.gain,
    mapIds: ambience.mapIds,
    runtimeFile: path.join(
      "web/src/assets/audio/effects/astral-relay",
      ambience.file,
    ),
  })
) {
  throw new Error(
    "The runtime ambience export differs from the reviewed V5 selection.",
  );
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
      deliveryProcessing: combatDeliveryProcessing[cueId],
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
        version: 3,
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
          deliveryProcessing: {
            policy:
              "Short dual-mono low-pass combat markers with soft endpoints; " +
              "no synthesized layer and no provider request.",
            cues: combatDeliveryProcessing,
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
          tickMix: {
            coalesceSameCue: true,
            suppressWindupUnderForegroundEvents: true,
            suppressBeamUnderConcreteOutcomes: [
              "impact",
              "destroyed",
              "deflect",
            ],
          },
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
