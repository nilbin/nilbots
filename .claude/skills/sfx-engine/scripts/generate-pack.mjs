#!/usr/bin/env node

import { execFileSync, spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import {
  mkdtemp,
  mkdir,
  readFile,
  rename,
  rm,
  writeFile,
} from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import process from "node:process";

const DEFAULT_API_BASE = "https://api.sfxengine.com/v1";
const DEFAULT_KEYCHAIN_SERVICE = "com.nilbots.sfxengine.api-key";
const POLL_INTERVAL_MS = 3_000;
const POLL_TIMEOUT_MS = 15 * 60_000;

const options = parseArguments(process.argv.slice(2));
const configPath = path.resolve(options.config);
const configDirectory = path.dirname(configPath);
const config = JSON.parse(await readFile(configPath, "utf8"));
validateConfig(config);

const outputDirectory = resolveInside(
  configDirectory,
  config.outputDirectory,
  "outputDirectory",
);
await mkdir(outputDirectory, { recursive: true });

const apiKey = readApiKey(config.keychainService);
if (options.dryRun) {
  console.log(
    `Validated ${config.cues.length} cue request(s); no generation credits used.`,
  );
  process.exit(0);
}

for (const cue of config.cues) {
  const outputPath = resolveInside(outputDirectory, cue.file, `${cue.id}.file`);
  const forced = options.forceIds.has(cue.id);
  if (["generated", "derived"].includes(cue.result?.status) && !forced) {
    try {
      await readFile(outputPath);
      console.log(`Skipping ${cue.id}; completed output already exists.`);
      continue;
    } catch {
      throw new Error(
        `${cue.id}: provenance says generated but ${outputPath} is missing. ` +
          `Use --force ${cue.id} only if another generation credit is intended.`,
      );
    }
  }

  const previousAttempt = forced && cue.result
    ? snapshotAttempt(cue)
    : null;
  console.log(`Generating ${cue.id} (${cue.requestLengthSeconds}s)...`);
  const created = await requestJson(`${config.apiBase}/sound-effect`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      prompt: cue.prompt,
      length: cue.requestLengthSeconds,
      secondsToStart: cue.secondsToStart ?? 0,
      isPublic: false,
    }),
  });
  assertSoundEffect(created, `${cue.id}: create response`);
  if (previousAttempt) {
    cue.attempts ??= [];
    cue.attempts.push({
      ...previousAttempt,
      supersededAt: new Date().toISOString(),
      reason: cue.revisionReason ?? "Explicit forced regeneration",
    });
  }
  cue.result = {
    ...sanitizeResult(created),
    request: snapshotRequest(cue),
  };
  await saveConfig();

  const completed = await waitForSoundEffect(
    config.apiBase,
    apiKey,
    created.id,
    cue.id,
  );
  if (completed.status !== "generated") {
    cue.result = sanitizeResult(completed, cue.result);
    await saveConfig();
    throw new Error(`${cue.id}: generation failed.`);
  }
  if (typeof completed.url !== "string" || completed.url.length === 0) {
    throw new Error(`${cue.id}: generated response has no audio URL.`);
  }

  const processed = await downloadAndStandardize({
    url: completed.url,
    apiBase: config.apiBase,
    apiKey,
    outputPath,
    targetPeak: cue.targetPeak ?? 0.82,
    loopCrossfadeSeconds: cue.loopCrossfadeSeconds,
  });
  cue.result = {
    ...sanitizeResult(completed, cue.result),
    status: "generated",
    downloadedContentType: processed.downloadedContentType,
    sourceSha256: processed.sourceSha256,
    outputSha256: processed.outputSha256,
    outputBytes: processed.outputBytes,
    processing: processed.processing,
    metrics: processed.metrics,
  };
  await saveConfig();
  console.log(
    `Completed ${cue.id}: ${processed.metrics.durationSeconds.toFixed(2)}s, ` +
      `${processed.metrics.sampleRate} Hz stereo WAV.`,
  );
}

console.log(`Completed ${config.cues.length} SFX Engine cue(s).`);

async function saveConfig() {
  const temporaryPath = `${configPath}.tmp`;
  await writeFile(temporaryPath, `${JSON.stringify(config, null, 2)}\n`, {
    mode: 0o644,
  });
  await rename(temporaryPath, configPath);
}

async function waitForSoundEffect(apiBase, apiKey, id, cueId) {
  const startedAt = Date.now();
  let lastStatus = "";
  while (Date.now() - startedAt < POLL_TIMEOUT_MS) {
    const sound = await requestJson(
      `${apiBase}/sound-effect/${encodeURIComponent(id)}`,
      { headers: { Authorization: `Bearer ${apiKey}` } },
    );
    assertSoundEffect(sound, `${cueId}: poll response`);
    if (sound.status !== lastStatus) {
      console.log(`${cueId}: ${sound.status}.`);
      lastStatus = sound.status;
    }
    if (sound.status === "generated" || sound.status === "failed") return sound;
    await sleep(POLL_INTERVAL_MS);
  }
  throw new Error(`${cueId}: timed out waiting for SFX Engine.`);
}

async function downloadAndStandardize({
  url,
  apiBase,
  apiKey,
  outputPath,
  targetPeak,
  loopCrossfadeSeconds,
}) {
  const audioUrl = new URL(url);
  const apiOrigin = new URL(apiBase).origin;
  const headers = audioUrl.origin === apiOrigin
    ? { Authorization: `Bearer ${apiKey}` }
    : {};
  const response = await fetch(audioUrl, { headers });
  if (!response.ok) {
    throw new Error(`Audio download failed with HTTP ${response.status}.`);
  }
  const sourceBytes = Buffer.from(await response.arrayBuffer());
  if (sourceBytes.length === 0) throw new Error("Downloaded audio is empty.");

  const contentType = (response.headers.get("content-type") ?? "").split(";")[0];
  const extension = extensionFor(contentType, audioUrl.pathname);
  const temporaryDirectory = await mkdtemp(path.join(os.tmpdir(), "sfx-engine-"));
  try {
    const sourcePath = path.join(temporaryDirectory, `source${extension}`);
    const convertedPath = path.join(temporaryDirectory, "converted.wav");
    await writeFile(sourcePath, sourceBytes);
    const conversion = spawnSync(
      "afconvert",
      [
        sourcePath,
        convertedPath,
        "-f",
        "WAVE",
        "-d",
        "LEI16@48000",
        "-c",
        "2",
        "-r",
        "127",
      ],
      { encoding: "utf8" },
    );
    if (conversion.status !== 0) {
      throw new Error(
        `afconvert failed: ${(conversion.stderr || conversion.stdout).trim()}`,
      );
    }
    const converted = await readFile(convertedPath);
    const standardized = standardizeWav(converted, {
      targetPeak,
      loopCrossfadeSeconds,
    });
    await mkdir(path.dirname(outputPath), { recursive: true });
    await writeFile(outputPath, standardized.bytes);
    return {
      downloadedContentType: contentType || "application/octet-stream",
      sourceSha256: sha256(sourceBytes),
      outputSha256: sha256(standardized.bytes),
      outputBytes: standardized.bytes.length,
      processing: standardized.processing,
      metrics: standardized.metrics,
    };
  } finally {
    await rm(temporaryDirectory, { recursive: true, force: true });
  }
}

function standardizeWav(bytes, { targetPeak, loopCrossfadeSeconds }) {
  const wav = parsePcm16Wav(bytes);
  if (wav.sampleRate !== 48_000 || wav.channels !== 2) {
    throw new Error(
      `Expected afconvert to produce 48 kHz stereo; got ` +
        `${wav.sampleRate} Hz and ${wav.channels} channel(s).`,
    );
  }
  let samples = new Float64Array(wav.sampleCount);
  for (let index = 0; index < wav.sampleCount; index++) {
    const value = bytes.readInt16LE(wav.dataOffset + index * 2) / 32_768;
    samples[index] = value;
  }
  let frames = wav.sampleCount / wav.channels;
  removeDc(samples, frames, wav.channels);

  let processing;
  if (loopCrossfadeSeconds !== undefined) {
    const crossfadeFrames = Math.round(loopCrossfadeSeconds * wav.sampleRate);
    samples = circularCrossfade(samples, frames, wav.channels, crossfadeFrames);
    frames -= crossfadeFrames;
    processing = {
      endpointTreatment: "circular-equal-power-crossfade",
      loopCrossfadeSeconds,
    };
  } else {
    const fadeInFrames = Math.min(frames, Math.round(wav.sampleRate * 0.005));
    const fadeOutFrames = Math.min(frames, Math.round(wav.sampleRate * 0.04));
    for (let frame = 0; frame < frames; frame++) {
      const fadeIn = fadeInFrames === 0 ? 1 : Math.min(1, frame / fadeInFrames);
      const remaining = frames - 1 - frame;
      const fadeOut = fadeOutFrames === 0
        ? 1
        : Math.min(1, remaining / fadeOutFrames);
      const fade = Math.sin(Math.min(fadeIn, fadeOut) * Math.PI * 0.5);
      for (let channel = 0; channel < wav.channels; channel++) {
        samples[frame * wav.channels + channel] *= fade;
      }
    }
    processing = {
      endpointTreatment: "short-fades",
      fadeInSeconds: 0.005,
      fadeOutSeconds: 0.04,
    };
  }
  removeDc(samples, frames, wav.channels);

  let peak = 0;
  for (const sample of samples) {
    peak = Math.max(peak, Math.abs(sample));
  }
  if (peak < 0.0001) throw new Error("Converted audio is effectively silent.");
  const gain = targetPeak / peak;
  const pcm = Buffer.alloc(samples.length * 2);
  let sumSquares = 0;
  let stereoDifferenceSquares = 0;
  let outputPeak = 0;
  let outputSum = 0;
  for (let frame = 0; frame < frames; frame++) {
    const left = Math.max(-1, Math.min(1, samples[frame * 2] * gain));
    const right = Math.max(-1, Math.min(1, samples[frame * 2 + 1] * gain));
    const values = [left, right];
    stereoDifferenceSquares += (left - right) ** 2;
    for (let channel = 0; channel < 2; channel++) {
      const value = values[channel];
      pcm.writeInt16LE(Math.round(value * 32_767), (frame * 2 + channel) * 2);
      sumSquares += value * value;
      outputPeak = Math.max(outputPeak, Math.abs(value));
      outputSum += value;
    }
  }
  const output = writePcm16Wav(pcm, wav.sampleRate, wav.channels);
  return {
    bytes: output,
    processing,
    metrics: {
      durationSeconds: frames / wav.sampleRate,
      sampleRate: wav.sampleRate,
      channels: wav.channels,
      bitsPerSample: 16,
      peak: round(outputPeak, 4),
      rms: round(Math.sqrt(sumSquares / samples.length), 4),
      stereoDifferenceRms: round(
        Math.sqrt(stereoDifferenceSquares / frames),
        4,
      ),
      dcOffset: round(Math.abs(outputSum / samples.length), 6),
      clippedSamples: 0,
    },
  };
}

function removeDc(samples, frames, channels) {
  const means = new Float64Array(channels);
  for (let index = 0; index < samples.length; index++) {
    means[index % channels] += samples[index];
  }
  for (let channel = 0; channel < channels; channel++) {
    means[channel] /= frames;
  }
  for (let index = 0; index < samples.length; index++) {
    samples[index] -= means[index % channels];
  }
}

function circularCrossfade(samples, frames, channels, crossfadeFrames) {
  if (crossfadeFrames <= 0 || crossfadeFrames * 2 >= frames) {
    throw new Error("Loop crossfade must be positive and shorter than half the audio.");
  }
  const outputFrames = frames - crossfadeFrames;
  const output = new Float64Array(outputFrames * channels);
  for (let frame = 0; frame < crossfadeFrames; frame++) {
    const progress = crossfadeFrames === 1
      ? 1
      : frame / (crossfadeFrames - 1);
    const tailGain = Math.cos(progress * Math.PI * 0.5);
    const headGain = Math.sin(progress * Math.PI * 0.5);
    for (let channel = 0; channel < channels; channel++) {
      const tailIndex = (frames - crossfadeFrames + frame) * channels + channel;
      const headIndex = frame * channels + channel;
      output[frame * channels + channel] =
        samples[tailIndex] * tailGain + samples[headIndex] * headGain;
    }
  }
  for (let frame = crossfadeFrames; frame < outputFrames; frame++) {
    for (let channel = 0; channel < channels; channel++) {
      output[frame * channels + channel] = samples[frame * channels + channel];
    }
  }
  return output;
}

function parsePcm16Wav(bytes) {
  if (bytes.toString("ascii", 0, 4) !== "RIFF" ||
      bytes.toString("ascii", 8, 12) !== "WAVE") {
    throw new Error("Converted file is not RIFF/WAVE audio.");
  }
  let offset = 12;
  let format;
  let dataOffset;
  let dataLength;
  while (offset + 8 <= bytes.length) {
    const id = bytes.toString("ascii", offset, offset + 4);
    const length = bytes.readUInt32LE(offset + 4);
    const body = offset + 8;
    if (id === "fmt ") {
      format = {
        audioFormat: bytes.readUInt16LE(body),
        channels: bytes.readUInt16LE(body + 2),
        sampleRate: bytes.readUInt32LE(body + 4),
        bitsPerSample: bytes.readUInt16LE(body + 14),
      };
    } else if (id === "data") {
      dataOffset = body;
      dataLength = length;
    }
    offset = body + length + (length % 2);
  }
  if (!format || dataOffset === undefined || dataLength === undefined) {
    throw new Error("Converted WAV is missing fmt or data chunks.");
  }
  if (format.audioFormat !== 1 || format.bitsPerSample !== 16) {
    throw new Error("Converted WAV is not signed 16-bit PCM.");
  }
  if (dataOffset + dataLength > bytes.length || dataLength % 2 !== 0) {
    throw new Error("Converted WAV has an invalid data chunk.");
  }
  return {
    ...format,
    dataOffset,
    dataLength,
    sampleCount: dataLength / 2,
  };
}

function writePcm16Wav(pcm, sampleRate, channels) {
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

async function requestJson(url, init) {
  const response = await fetch(url, init);
  const text = await response.text();
  let body;
  try {
    body = JSON.parse(text);
  } catch {
    throw new Error(`SFX Engine returned non-JSON HTTP ${response.status}.`);
  }
  if (!response.ok) {
    const message = typeof body?.message === "string" ? `: ${body.message}` : "";
    throw new Error(`SFX Engine returned HTTP ${response.status}${message}`);
  }
  return body;
}

function sanitizeResult(sound, previous = {}) {
  return {
    ...previous,
    apiSoundEffectId: sound.id,
    status: sound.status,
    providerCreatedAt: sound.createdAt,
    providerLengthSeconds: sound.length,
    providerFileSize: sound.fileSize,
    recordedAt: new Date().toISOString(),
  };
}

function snapshotAttempt(cue) {
  const request = cue.result?.request ?? snapshotRequest(cue);
  return {
    ...request,
    result: cue.result,
  };
}

function snapshotRequest(cue) {
  return {
    prompt: cue.prompt,
    requestLengthSeconds: cue.requestLengthSeconds,
    secondsToStart: cue.secondsToStart ?? 0,
    targetPeak: cue.targetPeak ?? 0.82,
    ...(cue.loopCrossfadeSeconds === undefined
      ? {}
      : { loopCrossfadeSeconds: cue.loopCrossfadeSeconds }),
  };
}

function assertSoundEffect(value, label) {
  if (!value || typeof value !== "object" || typeof value.id !== "string" ||
      !["generated", "processing", "failed"].includes(value.status)) {
    throw new Error(`${label} does not match the saved OpenAPI contract.`);
  }
}

function readApiKey(keychainService = DEFAULT_KEYCHAIN_SERVICE) {
  const environmentKey = process.env.SFXENGINE_API_KEY?.trim();
  if (environmentKey) return environmentKey;
  if (process.platform !== "darwin") {
    throw new Error(
      "SFXENGINE_API_KEY is unset and macOS Keychain is unavailable.",
    );
  }
  const account = execFileSync("id", ["-un"], { encoding: "utf8" }).trim();
  const key = execFileSync(
    "security",
    [
      "find-generic-password",
      "-a",
      account,
      "-s",
      keychainService,
      "-w",
    ],
    { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] },
  ).trim();
  if (!key) throw new Error("The Keychain credential is empty.");
  return key;
}

function validateConfig(value) {
  if (!value || typeof value !== "object") throw new Error("Invalid config JSON.");
  value.apiBase ??= DEFAULT_API_BASE;
  if (value.apiBase !== DEFAULT_API_BASE) {
    throw new Error(`apiBase must be ${DEFAULT_API_BASE}.`);
  }
  if (typeof value.outputDirectory !== "string") {
    throw new Error("outputDirectory is required.");
  }
  if (!Array.isArray(value.cues) || value.cues.length === 0) {
    throw new Error("At least one cue is required.");
  }
  const ids = new Set();
  for (const cue of value.cues) {
    if (!cue || typeof cue !== "object" ||
        !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(cue.id ?? "")) {
      throw new Error("Every cue needs a kebab-case id.");
    }
    if (ids.has(cue.id)) throw new Error(`Duplicate cue id: ${cue.id}.`);
    ids.add(cue.id);
    if (typeof cue.prompt !== "string" || cue.prompt.length < 1 ||
        cue.prompt.length > 255) {
      throw new Error(`${cue.id}: prompt must contain 1-255 characters.`);
    }
    if (!Number.isInteger(cue.requestLengthSeconds) ||
        cue.requestLengthSeconds < 1 || cue.requestLengthSeconds > 47) {
      throw new Error(`${cue.id}: requestLengthSeconds must be an integer 1-47.`);
    }
    if (cue.secondsToStart !== undefined &&
        (!Number.isInteger(cue.secondsToStart) || cue.secondsToStart < 0 ||
         cue.secondsToStart > 47)) {
      throw new Error(`${cue.id}: secondsToStart must be an integer 0-47.`);
    }
    if (typeof cue.file !== "string" || path.extname(cue.file) !== ".wav") {
      throw new Error(`${cue.id}: file must be a relative .wav path.`);
    }
    const minimumPeak = cue.loopCrossfadeSeconds === undefined ? 0.7 : 0.3;
    if (cue.targetPeak !== undefined &&
        (typeof cue.targetPeak !== "number" || cue.targetPeak < minimumPeak ||
         cue.targetPeak > 0.9)) {
      throw new Error(
        `${cue.id}: targetPeak must be between ${minimumPeak} and 0.9.`,
      );
    }
    if (cue.loopCrossfadeSeconds !== undefined &&
        (typeof cue.loopCrossfadeSeconds !== "number" ||
         cue.loopCrossfadeSeconds <= 0 ||
         cue.loopCrossfadeSeconds * 2 >= cue.requestLengthSeconds)) {
      throw new Error(
        `${cue.id}: loopCrossfadeSeconds must be positive and shorter than ` +
          `half requestLengthSeconds.`,
      );
    }
  }
  for (const id of options.forceIds) {
    if (!ids.has(id)) throw new Error(`Unknown --force cue: ${id}.`);
  }
}

function resolveInside(parent, relativePath, label) {
  if (typeof relativePath !== "string" || path.isAbsolute(relativePath)) {
    throw new Error(`${label} must be a relative path.`);
  }
  const resolved = path.resolve(parent, relativePath);
  const relative = path.relative(parent, resolved);
  if (relative === "" || relative === ".." || relative.startsWith(`..${path.sep}`)) {
    throw new Error(`${label} escapes its owning directory.`);
  }
  return resolved;
}

function parseArguments(arguments_) {
  let config;
  let dryRun = false;
  const forceIds = new Set();
  for (let index = 0; index < arguments_.length; index++) {
    const argument = arguments_[index];
    if (argument === "--config") config = arguments_[++index];
    else if (argument === "--dry-run") dryRun = true;
    else if (argument === "--force") forceIds.add(arguments_[++index]);
    else throw new Error(`Unknown argument: ${argument}`);
  }
  if (!config) throw new Error("Usage: generate-pack.mjs --config <file> [--dry-run] [--force <cue-id>]");
  return { config, dryRun, forceIds };
}

function extensionFor(contentType, pathname) {
  const byType = {
    "audio/mpeg": ".mp3",
    "audio/mp3": ".mp3",
    "audio/wav": ".wav",
    "audio/x-wav": ".wav",
    "audio/flac": ".flac",
    "audio/mp4": ".m4a",
    "audio/x-m4a": ".m4a",
  }[contentType];
  if (byType) return byType;
  const extension = path.extname(pathname).toLowerCase();
  return /^\.[a-z0-9]{2,5}$/.test(extension) ? extension : ".audio";
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function round(value, decimals) {
  return Number(value.toFixed(decimals));
}

function sleep(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}
