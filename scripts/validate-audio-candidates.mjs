#!/usr/bin/env node

import { access, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const labRoot = path.join(repoRoot, 'art', 'audio', 'sound-lab');
const manifestPath = path.join(labRoot, 'manifest.json');
const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));

assert(manifest.version === 1, 'manifest version must be 1');
assert(manifest.sampleRate === 44_100, 'candidate sample rate must be 44.1 kHz');
assert(manifest.channels === 1, 'candidate assets must be mono');
assert(Array.isArray(manifest.packs), 'manifest packs must be an array');
assert(manifest.packs.length === 3, 'sound lab must contain exactly three directions');

const referenceIds = manifest.packs[0].cues.map((cue) => cue.id);
assert(referenceIds.length === 10, 'each direction must contain ten review cues');
const files = new Set();

for (const pack of manifest.packs) {
  assert(/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(pack.id), `${pack.id}: invalid pack ID`);
  assert(/^#[0-9a-f]{6}$/i.test(pack.accent), `${pack.id}: invalid accent`);
  assert(
    JSON.stringify(pack.cues.map((cue) => cue.id)) === JSON.stringify(referenceIds),
    `${pack.id}: cue order or coverage differs from the reference pack`,
  );

  for (const cue of pack.cues) {
    const relative = cue.file.replace(/^\.\//, '');
    assert(!relative.includes('..'), `${cue.file}: unsafe path`);
    const absolute = path.resolve(labRoot, relative);
    assert(absolute.startsWith(`${labRoot}${path.sep}`), `${cue.file}: escapes sound lab`);
    await access(absolute);
    assert(!files.has(absolute), `${cue.file}: duplicate audio file`);
    files.add(absolute);

    const bytes = await readFile(absolute);
    const wav = parseWav(bytes, cue.file);
    assert(wav.sampleRate === manifest.sampleRate, `${cue.file}: wrong sample rate`);
    assert(wav.channels === manifest.channels, `${cue.file}: wrong channel count`);
    assert(wav.bitsPerSample === 16, `${cue.file}: must be signed 16-bit PCM`);
    assert(
      Math.abs(wav.durationSeconds - cue.durationSeconds) < 0.002,
      `${cue.file}: manifest duration differs from WAV`,
    );
    assert(wav.peak >= 0.7 && wav.peak <= 0.9, `${cue.file}: peak ${wav.peak} is out of range`);
    assert(wav.rms >= 0.018, `${cue.file}: cue is effectively silent`);
    assert(wav.rms <= 0.42, `${cue.file}: cue is excessively dense`);
    assert(wav.clippedSamples === 0, `${cue.file}: contains clipped samples`);
  }
}

assert(files.size === 30, `expected 30 unique WAV files, found ${files.size}`);
for (const filename of ['index.html', 'manifest.js', 'soundboard.css', 'soundboard.js'])
  await access(path.join(labRoot, filename));

console.log(
  `Validated ${manifest.packs.length} audio directions, ` +
    `${referenceIds.length} cues each, ${files.size} WAV files total.`,
);

function parseWav(bytes, filename) {
  assert(bytes.toString('ascii', 0, 4) === 'RIFF', `${filename}: missing RIFF header`);
  assert(bytes.toString('ascii', 8, 12) === 'WAVE', `${filename}: missing WAVE header`);
  assert(bytes.toString('ascii', 12, 16) === 'fmt ', `${filename}: missing fmt chunk`);
  assert(bytes.readUInt16LE(20) === 1, `${filename}: must use PCM encoding`);
  assert(bytes.toString('ascii', 36, 40) === 'data', `${filename}: missing data chunk`);
  const channels = bytes.readUInt16LE(22);
  const sampleRate = bytes.readUInt32LE(24);
  const bitsPerSample = bytes.readUInt16LE(34);
  const dataBytes = bytes.readUInt32LE(40);
  assert(dataBytes === bytes.length - 44, `${filename}: invalid data chunk length`);
  const sampleCount = dataBytes / 2;
  let peak = 0;
  let sumSquares = 0;
  let clippedSamples = 0;
  for (let offset = 44; offset < bytes.length; offset += 2) {
    const integer = bytes.readInt16LE(offset);
    const sample = integer / 32_767;
    peak = Math.max(peak, Math.abs(sample));
    sumSquares += sample * sample;
    if (integer === -32_768 || integer === 32_767) clippedSamples++;
  }
  return {
    channels,
    sampleRate,
    bitsPerSample,
    durationSeconds: sampleCount / channels / sampleRate,
    peak,
    rms: Math.sqrt(sumSquares / sampleCount),
    clippedSamples,
  };
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
