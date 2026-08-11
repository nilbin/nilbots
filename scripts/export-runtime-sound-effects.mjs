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
      gain: 0.18,
      voiceLimit: 1,
      panWidth: 0.2,
      roomSend: false,
    },
    projectile: {
      gain: 0.2,
      voiceLimit: 1,
      panWidth: 0.2,
      roomSend: false,
    },
    impact: { gain: 0.32, voiceLimit: 1, panWidth: 0.5, roomSend: false },
    destroyed: { gain: 0.32, voiceLimit: 1, panWidth: 0.45, roomSend: false },
    "level-up": { gain: 0.32, voiceLimit: 1, panWidth: 0.4, roomSend: false },
    "arc-birth": { gain: 0.4, voiceLimit: 2, panWidth: 0.7, roomSend: true },
    "arc-pickup": { gain: 0.3, voiceLimit: 2, panWidth: 0.7, roomSend: true },
    "arc-drop": { gain: 0.3, voiceLimit: 2, panWidth: 0.7, roomSend: true },
    "arc-bank": { gain: 0.44, voiceLimit: 1, panWidth: 0.55, roomSend: true },
    "arc-pulse": { gain: 0.32, voiceLimit: 1, panWidth: 0.55, roomSend: true },
    "arc-handoff": { gain: 0.3, voiceLimit: 1, panWidth: 0.55, roomSend: false },
    deflect: { gain: 0.34, voiceLimit: 2, panWidth: 0.7, roomSend: true },
    arrival: { gain: 0.28, voiceLimit: 1, panWidth: 0.45, roomSend: false },
  },
};
const deliveryProcessing = {
  "attack-windup": {
    channelTransform: "side-to-dual-mono",
    sourceStartSeconds: 0.1,
    playbackRate: 0.16,
    durationSeconds: 0.24,
    lowPassHz: 800,
    fadeInSeconds: 0.045,
    fadeOutSeconds: 0.09,
    targetSamplePeak: 0.24,
  },
  projectile: {
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0.005,
    playbackRate: 0.16,
    durationSeconds: 0.16,
    lowPassHz: 750,
    fadeInSeconds: 0.025,
    fadeOutSeconds: 0.08,
    targetSamplePeak: 0.24,
  },
  impact: {
    channelTransform: "preserve-stereo",
    sourceStartSeconds: 0,
    playbackRate: 1,
    durationSeconds: 1,
    lowPassHz: 3_600,
    fadeInSeconds: 0.02,
    fadeOutSeconds: 0.14,
    targetSamplePeak: 0.45,
  },
  destroyed: {
    channelTransform: "preserve-stereo",
    sourceStartSeconds: 0,
    playbackRate: 1,
    durationSeconds: 2,
    lowPassHz: 2_800,
    fadeInSeconds: 0.08,
    fadeOutSeconds: 0.45,
    targetSamplePeak: 0.5,
  },
  "level-up": {
    channelTransform: "preserve-stereo",
    sourceStartSeconds: 0,
    playbackRate: 1,
    durationSeconds: 1,
    lowPassHz: 3_500,
    fadeInSeconds: 0.12,
    fadeOutSeconds: 0.18,
    targetSamplePeak: 0.42,
  },
  "arc-bank": {
    channelTransform: "preserve-stereo",
    sourceStartSeconds: 0,
    playbackRate: 1,
    durationSeconds: 2,
    lowPassHz: 4_500,
    fadeInSeconds: 0.04,
    fadeOutSeconds: 0.5,
    targetSamplePeak: 0.55,
  },
  "arc-pulse": {
    channelTransform: "preserve-stereo",
    sourceStartSeconds: 0,
    playbackRate: 1,
    durationSeconds: 4,
    lowPassHz: 3_000,
    fadeInSeconds: 0.12,
    fadeOutSeconds: 0.4,
    targetSamplePeak: 0.55,
  },
  "arc-handoff": {
    channelTransform: "preserve-stereo",
    sourceStartSeconds: 0,
    playbackRate: 1,
    durationSeconds: 2,
    lowPassHz: 3_200,
    fadeInSeconds: 0.04,
    fadeOutSeconds: 0.5,
    targetSamplePeak: 0.5,
  },
  arrival: {
    channelTransform: "preserve-stereo",
    sourceStartSeconds: 0,
    playbackRate: 1,
    durationSeconds: 2,
    lowPassHz: 2_800,
    fadeInSeconds: 0.45,
    fadeOutSeconds: 0.08,
    targetSamplePeak: 0.5,
  },
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
  const decodedWav = `${destination}.decoded.wav`;
  let encodingSource = sourceFile;
  let sourceInterchannelCorrelation;
  if (deliveryProcessing) {
    const shaped = shapeDeliveryCue(sourceBytes, id, deliveryProcessing);
    await writeFile(temporaryWav, shaped.bytes);
    encodingSource = temporaryWav;
    sourceInterchannelCorrelation = shaped.sourceInterchannelCorrelation;
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
  let deliveryMetrics;
  try {
    execFileSync(
      "afconvert",
      [
        destination,
        decodedWav,
        "-f",
        "WAVE",
        "-d",
        "LEI16@48000",
        "-c",
        "2",
        "-r",
        "127",
      ],
      { stdio: "inherit" },
    );
    deliveryMetrics = analyzeDeliveryWav(await readFile(decodedWav), id);
  } finally {
    await rm(decodedWav, { force: true });
  }
  if (sourceInterchannelCorrelation !== undefined) {
    deliveryMetrics.sourceInterchannelCorrelation = round(
      sourceInterchannelCorrelation,
      4,
    );
  }
  const runtimeGainDb = 20 * Math.log10(mix.cues[id].gain);
  const effectivePeakDbfs = deliveryMetrics.samplePeakDbfs + runtimeGainDb;
  const effectiveRmsDbfs = deliveryMetrics.rmsDbfs + runtimeGainDb;
  const effectivePeakCeiling =
    id === "attack-windup" || id === "projectile" ? -20 : -9;
  if (effectivePeakDbfs > effectivePeakCeiling) {
    throw new Error(
      `${id}: ${effectivePeakDbfs.toFixed(2)} dBFS effective delivery peak ` +
        `exceeds its ${effectivePeakCeiling} dBFS ceiling.`,
    );
  }
  if (
    effectiveRmsDbfs > -25 ||
    deliveryMetrics.energyAbove4kPercent > 3 ||
    deliveryMetrics.energyAbove12kPercent > 0.05
  ) {
    throw new Error(
      `${id}: decoded runtime audio exceeds a comfort ceiling ` +
        `(${effectiveRmsDbfs.toFixed(2)} dBFS RMS, ` +
        `${deliveryMetrics.energyAbove4kPercent.toFixed(2)}% above 4 kHz, ` +
        `${deliveryMetrics.energyAbove12kPercent.toFixed(2)}% above 12 kHz).`,
    );
  }
  deliveryMetrics.effectiveSamplePeakDbfs = round(effectivePeakDbfs, 2);
  deliveryMetrics.effectiveRmsDbfs = round(effectiveRmsDbfs, 2);
  deliveryMetrics.effectiveMaximumWindowAbove4kRmsDbfs = round(
    deliveryMetrics.maximumWindowAbove4kRmsDbfs + runtimeGainDb,
    2,
  );
  if (
    deliveryMetrics.maximumWindowAbove4kPercent > 10 &&
    deliveryMetrics.effectiveMaximumWindowAbove4kRmsDbfs > -28
  ) {
    throw new Error(
      `${id}: a concentrated bright window remains audible above its ` +
        "-28 dBFS RMS ceiling.",
    );
  }
  return {
    sourceSha256: sourceHash,
    outputSha256: sha256(outputBytes),
    bytes: outputBytes.length,
    bitrate,
    deliveryMetrics,
  };
}

function shapeDeliveryCue(sourceBytes, id, processing) {
  const wav = parsePcm16Wav(sourceBytes, id);
  if (wav.sampleRate !== 48_000 || wav.channels !== 2) {
    throw new Error(`${id}: delivery shaping requires 48 kHz stereo PCM.`);
  }
  const source = decodePcm16(sourceBytes, wav);
  const sourceInterchannelCorrelation = interchannelCorrelation(
    source,
    wav.channels,
  );
  const frameCount = Math.round(
    processing.durationSeconds * wav.sampleRate,
  );
  const sourceStartFrame = processing.sourceStartSeconds * wav.sampleRate;
  const finalSourcePosition =
    sourceStartFrame + (frameCount - 1) * processing.playbackRate;
  const sourceFrameCount = source.length / wav.channels;
  if (
    frameCount <= 0 ||
    sourceStartFrame < 0 ||
    processing.playbackRate <= 0 ||
    finalSourcePosition > sourceFrameCount - 1
  ) {
    throw new Error(`${id}: delivery window exceeds the source cue.`);
  }
  const samples = new Float64Array(frameCount * wav.channels);
  for (let frame = 0; frame < frameCount; frame += 1) {
    const sourcePosition =
      sourceStartFrame + frame * processing.playbackRate;
    const sourceFrame = Math.floor(sourcePosition);
    const nextSourceFrame = Math.min(sourceFrame + 1, sourceFrameCount - 1);
    const fraction = sourcePosition - sourceFrame;
    const left =
      source[sourceFrame * 2] * (1 - fraction) +
      source[nextSourceFrame * 2] * fraction;
    const right =
      source[sourceFrame * 2 + 1] * (1 - fraction) +
      source[nextSourceFrame * 2 + 1] * fraction;
    if (processing.channelTransform === "preserve-stereo") {
      samples[frame * 2] = left;
      samples[frame * 2 + 1] = right;
      continue;
    }
    const mono =
      processing.channelTransform === "side-to-dual-mono"
        ? (left - right) * 0.5
        : processing.channelTransform === "mid-to-dual-mono"
          ? (left + right) * 0.5
          : Number.NaN;
    if (!Number.isFinite(mono)) {
      throw new Error(`${id}: unknown delivery channel transform.`);
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
    sourceInterchannelCorrelation,
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
        subFormat:
          length >= 40 ? bytes.readUInt32LE(body + 24) : undefined,
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
    !(
      format.audioFormat === 1 ||
      (format.audioFormat === 0xfffe && format.subFormat === 1)
    ) ||
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
  const mono = new Float64Array(frames);
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
    mono[frame] = (left + right) * 0.5;
  }
  const correlation = cross / Math.sqrt(leftSquares * rightSquares);
  const rms = Math.sqrt((leftSquares + rightSquares) / (frames * 2));
  return {
    durationSeconds: round(frames / wav.sampleRate, 3),
    samplePeakDbfs: round(20 * Math.log10(peak), 2),
    rmsDbfs: round(20 * Math.log10(rms), 2),
    dominantFrequencyHz: dominantFrequencyHz(mono, wav.sampleRate),
    interchannelCorrelation: round(correlation, 4),
    ...analyzeSpectrum(bytes, wav),
  };
}

function analyzeSpectrum(bytes, wav) {
  const frames = wav.sampleCount / wav.channels;
  const fftSize = 4_096;
  const hop = fftSize / 2;
  const starts =
    frames < fftSize
      ? [0]
      : Array.from(
          { length: Math.floor((frames - fftSize) / hop) + 1 },
          (_, index) => index * hop,
        );
  const spectrum = new Float64Array(fftSize / 2 + 1);
  let maximumWindowAbove4kPercent = 0;
  let maximumWindowAbove4kAtSeconds = 0;
  let maximumWindowAbove4kRmsDbfs = -Infinity;

  for (const start of starts) {
    const windowSpectrum = new Float64Array(fftSize / 2 + 1);
    let windowSquares = 0;
    for (let channel = 0; channel < wav.channels; channel += 1) {
      const real = new Float64Array(fftSize);
      const imaginary = new Float64Array(fftSize);
      for (let index = 0; index < fftSize; index += 1) {
        const frame = start + index;
        const sample =
          frame < frames
            ? bytes.readInt16LE(
                wav.dataOffset + (frame * wav.channels + channel) * 2,
              ) / 32_768
            : 0;
        windowSquares += sample * sample;
        const window =
          0.5 - 0.5 * Math.cos((2 * Math.PI * index) / (fftSize - 1));
        real[index] = sample * window;
      }
      fft(real, imaginary);
      for (let bin = 1; bin <= fftSize / 2; bin += 1) {
        windowSpectrum[bin] += real[bin] ** 2 + imaginary[bin] ** 2;
      }
    }

    let windowTotal = 0;
    let windowAbove4k = 0;
    for (let bin = 1; bin <= fftSize / 2; bin += 1) {
      const power = windowSpectrum[bin];
      spectrum[bin] += power;
      windowTotal += power;
      if ((bin * wav.sampleRate) / fftSize >= 4_000) {
        windowAbove4k += power;
      }
    }
    if (windowTotal > 0) {
      const percentage = (100 * windowAbove4k) / windowTotal;
      if (percentage > maximumWindowAbove4kPercent) {
        maximumWindowAbove4kPercent = percentage;
        maximumWindowAbove4kAtSeconds = start / wav.sampleRate;
        maximumWindowAbove4kRmsDbfs =
          20 *
          Math.log10(
            Math.sqrt(windowSquares / (fftSize * wav.channels)),
          );
      }
    }
  }

  let total = 0;
  let weightedFrequency = 0;
  let above4k = 0;
  let above8k = 0;
  let above12k = 0;
  for (let bin = 1; bin <= fftSize / 2; bin += 1) {
    const frequency = (bin * wav.sampleRate) / fftSize;
    const power = spectrum[bin];
    total += power;
    weightedFrequency += frequency * power;
    if (frequency >= 4_000) above4k += power;
    if (frequency >= 8_000) above8k += power;
    if (frequency >= 12_000) above12k += power;
  }
  if (total <= 0) throw new Error("Delivery spectrum is effectively silent.");
  if (!Number.isFinite(maximumWindowAbove4kRmsDbfs)) {
    maximumWindowAbove4kRmsDbfs = -120;
  }
  return {
    spectralCentroidHz: round(weightedFrequency / total, 0),
    energyAbove4kPercent: round((100 * above4k) / total, 2),
    energyAbove8kPercent: round((100 * above8k) / total, 2),
    energyAbove12kPercent: round((100 * above12k) / total, 2),
    maximumWindowAbove4kPercent: round(maximumWindowAbove4kPercent, 2),
    maximumWindowAbove4kAtSeconds: round(
      maximumWindowAbove4kAtSeconds,
      3,
    ),
    maximumWindowAbove4kRmsDbfs: round(maximumWindowAbove4kRmsDbfs, 2),
  };
}

function fft(real, imaginary) {
  const length = real.length;
  for (let index = 1, reversed = 0; index < length; index += 1) {
    let bit = length >> 1;
    for (; reversed & bit; bit >>= 1) reversed ^= bit;
    reversed ^= bit;
    if (index < reversed) {
      [real[index], real[reversed]] = [real[reversed], real[index]];
      [imaginary[index], imaginary[reversed]] = [
        imaginary[reversed],
        imaginary[index],
      ];
    }
  }
  for (let width = 2; width <= length; width <<= 1) {
    const angle = (-2 * Math.PI) / width;
    const rotationReal = Math.cos(angle);
    const rotationImaginary = Math.sin(angle);
    for (let start = 0; start < length; start += width) {
      let phaseReal = 1;
      let phaseImaginary = 0;
      for (let index = 0; index < width / 2; index += 1) {
        const even = start + index;
        const odd = even + width / 2;
        const oddReal =
          real[odd] * phaseReal - imaginary[odd] * phaseImaginary;
        const oddImaginary =
          real[odd] * phaseImaginary + imaginary[odd] * phaseReal;
        const evenReal = real[even];
        const evenImaginary = imaginary[even];
        real[even] = evenReal + oddReal;
        imaginary[even] = evenImaginary + oddImaginary;
        real[odd] = evenReal - oddReal;
        imaginary[odd] = evenImaginary - oddImaginary;
        const nextPhaseReal =
          phaseReal * rotationReal - phaseImaginary * rotationImaginary;
        phaseImaginary =
          phaseReal * rotationImaginary + phaseImaginary * rotationReal;
        phaseReal = nextPhaseReal;
      }
    }
  }
}

function dominantFrequencyHz(samples, sampleRate) {
  const frameCount = Math.min(samples.length, 16_384);
  const windowed = new Float64Array(frameCount);
  for (let frame = 0; frame < frameCount; frame += 1) {
    const window =
      frameCount === 1
        ? 1
        : 0.5 -
          0.5 * Math.cos((2 * Math.PI * frame) / (frameCount - 1));
    windowed[frame] = samples[frame] * window;
  }
  let bestFrequency = 0;
  let bestPower = -Infinity;
  for (let frequency = 80; frequency <= 2_000; frequency += 2) {
    const coefficient =
      2 * Math.cos((2 * Math.PI * frequency) / sampleRate);
    let previous = 0;
    let previousPrevious = 0;
    for (let frame = 0; frame < frameCount; frame += 1) {
      const current =
        windowed[frame] +
        coefficient * previous -
        previousPrevious;
      previousPrevious = previous;
      previous = current;
    }
    const power =
      previous * previous +
      previousPrevious * previousPrevious -
      coefficient * previous * previousPrevious;
    if (power > bestPower) {
      bestPower = power;
      bestFrequency = frequency;
    }
  }
  return bestFrequency;
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
if (sourceManifest.reviewScope?.selectedBackgroundLoop !== null) {
  throw new Error("The V5 sound lab still selects a runtime ambience loop.");
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
      deliveryProcessing: deliveryProcessing[cueId],
    });
    cues[cueId] = filename;
    integrity[cueId] = result;
    totalBytes += result.bytes;
    fileCount += 1;
  }

  await writeFile(
    path.join(directory, "manifest.json"),
    `${JSON.stringify(
      {
        version: 5,
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
              "Deterministic low-pass, endpoint, and peak treatment for frequent or " +
              "spectrally sharp cues; no synthesized layer and no provider request.",
            cues: deliveryProcessing,
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
