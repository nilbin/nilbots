#!/usr/bin/env node

/**
 * Deterministic, rights-cleared Arc Relay event cues for the approved Obsidian Foundry
 * runtime pack. Every sample is synthesized here; no recordings or provider assets enter
 * the build. Run from anywhere with `node scripts/generate-arc-relay-sfx.mjs`.
 */
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const SAMPLE_RATE = 48_000;
const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const OUTPUT = path.join(
  ROOT,
  'web/src/assets/audio/effects/obsidian-foundry',
);
const scratch = mkdtempSync(path.join(tmpdir(), 'nilbots-arc-sfx-'));

const definitions = [
  { id: 'arc-birth', duration: 0.82, render: renderBirth },
  { id: 'arc-steal', duration: 0.64, render: renderSteal },
  { id: 'arc-bank', duration: 0.96, render: renderBank },
  { id: 'arc-pulse', duration: 1.42, render: renderPulse },
];

try {
  for (const definition of definitions) {
    const signal = stereo(definition.duration);
    definition.render(signal, seeded(definition.id));
    normalize(signal, 0.9);
    const wav = path.join(scratch, `${definition.id}.wav`);
    const m4a = path.join(OUTPUT, `${definition.id}.m4a`);
    writeFileSync(wav, wavBytes(signal));
    const ffmpeg = spawnSync(
      'ffmpeg',
      [
        '-hide_banner',
        '-loglevel',
        'error',
        '-y',
        '-i',
        wav,
        '-c:a',
        'aac',
        '-b:a',
        '96k',
        '-ar',
        String(SAMPLE_RATE),
        '-ac',
        '2',
        m4a,
      ],
      { stdio: 'inherit' },
    );
    if (ffmpeg.status !== 0) {
      const afconvert = spawnSync(
        'afconvert',
        ['-f', 'm4af', '-d', 'aac', '-b', '96000', wav, m4a],
        { stdio: 'inherit' },
      );
      if (afconvert.status !== 0)
        throw new Error(`no AAC encoder succeeded for ${definition.id}`);
    }
  }
} finally {
  rmSync(scratch, { recursive: true, force: true });
}

function stereo(duration) {
  const length = Math.ceil(duration * SAMPLE_RATE);
  return { left: new Float64Array(length), right: new Float64Array(length) };
}

function seeded(label) {
  let state = 0x811c9dc5;
  for (const character of label) {
    state ^= character.codePointAt(0);
    state = Math.imul(state, 0x01000193);
  }
  return () => {
    state = (Math.imul(state, 1_664_525) + 1_013_904_223) >>> 0;
    return state / 0x1_0000_0000;
  };
}

function envelope(time, duration, attack = 0.006, release = 0.18) {
  const on = Math.min(1, time / Math.max(attack, 1 / SAMPLE_RATE));
  const off = Math.min(
    1,
    Math.max(0, duration - time) / Math.max(release, 1 / SAMPLE_RATE),
  );
  return Math.sin(on * Math.PI * 0.5) * Math.sin(off * Math.PI * 0.5);
}

function addTone(
  signal,
  {
    start = 0,
    duration,
    from,
    to = from,
    gain = 0.2,
    attack = 0.006,
    release = 0.18,
    pan = 0,
    panTo = pan,
    partials = [[1, 1]],
  },
) {
  const first = Math.floor(start * SAMPLE_RATE);
  const count = Math.min(
    Math.ceil(duration * SAMPLE_RATE),
    signal.left.length - first,
  );
  let phase = 0;
  for (let index = 0; index < count; index += 1) {
    const time = index / SAMPLE_RATE;
    const progress = time / Math.max(duration, 1 / SAMPLE_RATE);
    const frequency = from * (to / from) ** progress;
    phase += (Math.PI * 2 * frequency) / SAMPLE_RATE;
    let sample = 0;
    for (const [multiple, amount] of partials)
      sample += Math.sin(phase * multiple) * amount;
    const position = pan + (panTo - pan) * progress;
    const angle = ((position + 1) * Math.PI) / 4;
    const level = gain * envelope(time, duration, attack, release);
    signal.left[first + index] += sample * level * Math.cos(angle);
    signal.right[first + index] += sample * level * Math.sin(angle);
  }
}

function addNoise(
  signal,
  random,
  {
    start = 0,
    duration,
    gain = 0.12,
    attack = 0,
    release = 0.14,
    lowpass = 7_000,
    pan = 0,
    panTo = pan,
  },
) {
  const first = Math.floor(start * SAMPLE_RATE);
  const count = Math.min(
    Math.ceil(duration * SAMPLE_RATE),
    signal.left.length - first,
  );
  const coefficient = 1 - Math.exp((-Math.PI * 2 * lowpass) / SAMPLE_RATE);
  let filteredLeft = 0;
  let filteredRight = 0;
  for (let index = 0; index < count; index += 1) {
    filteredLeft += ((random() * 2 - 1) - filteredLeft) * coefficient;
    filteredRight += ((random() * 2 - 1) - filteredRight) * coefficient;
    const time = index / SAMPLE_RATE;
    const progress = time / Math.max(duration, 1 / SAMPLE_RATE);
    const position = pan + (panTo - pan) * progress;
    const angle = ((position + 1) * Math.PI) / 4;
    const level = gain * envelope(time, duration, attack, release);
    signal.left[first + index] += filteredLeft * level * Math.cos(angle);
    signal.right[first + index] += filteredRight * level * Math.sin(angle);
  }
}

function addTransient(signal, random, at, gain, body = 100) {
  addNoise(signal, random, {
    start: at,
    duration: 0.105,
    gain,
    release: 0.1,
    lowpass: 4_600,
  });
  addTone(signal, {
    start: at,
    duration: 0.24,
    from: body * 1.45,
    to: body * 0.68,
    gain: gain * 0.9,
    attack: 0,
    release: 0.22,
    partials: [
      [1, 1],
      [1.51, 0.34],
      [2.23, 0.16],
    ],
  });
}

function addEcho(signal, seconds, feedback) {
  const delay = Math.round(seconds * SAMPLE_RATE);
  for (let index = delay; index < signal.left.length; index += 1) {
    signal.left[index] += signal.right[index - delay] * feedback;
    signal.right[index] += signal.left[index - delay] * feedback;
  }
}

function renderBirth(signal, random) {
  addTone(signal, {
    duration: 0.52,
    from: 74,
    to: 186,
    gain: 0.3,
    release: 0.24,
    partials: [[1, 1], [2, 0.18]],
  });
  addNoise(signal, random, {
    duration: 0.42,
    gain: 0.13,
    attack: 0.05,
    release: 0.22,
    lowpass: 5_200,
    pan: -0.55,
    panTo: 0.55,
  });
  [392, 587.33, 880].forEach((frequency, index) =>
    addTone(signal, {
      start: 0.18 + index * 0.095,
      duration: 0.46,
      from: frequency,
      gain: 0.13,
      release: 0.34,
      pan: -0.5 + index * 0.5,
      partials: [[1, 1], [2.01, 0.2], [3.03, 0.08]],
    }),
  );
  addEcho(signal, 0.12, 0.18);
}

function renderSteal(signal, random) {
  addTransient(signal, random, 0, 0.4, 142);
  addTone(signal, {
    start: 0.012,
    duration: 0.43,
    from: 1_460,
    to: 240,
    gain: 0.29,
    release: 0.18,
    pan: 0.62,
    panTo: -0.62,
    partials: [[1, 1], [1.5, 0.28]],
  });
  for (const [start, frequency, pan] of [[0.12, 760, -0.48], [0.25, 610, 0.48]])
    addTone(signal, {
      start,
      duration: 0.27,
      from: frequency,
      gain: 0.19,
      release: 0.19,
      pan,
      partials: [[1, 1], [2.02, 0.16]],
    });
  addEcho(signal, 0.075, 0.16);
}

function renderBank(signal, random) {
  addTransient(signal, random, 0.04, 0.5, 82);
  [110, 164.81, 220].forEach((frequency, index) =>
    addTone(signal, {
      start: 0.09 + index * 0.035,
      duration: 0.72,
      from: frequency,
      to: frequency * 1.02,
      gain: 0.22,
      release: 0.38,
      pan: -0.38 + index * 0.38,
      partials: [[1, 1], [2, 0.15]],
    }),
  );
  addTone(signal, {
    start: 0.2,
    duration: 0.62,
    from: 520,
    to: 1_040,
    gain: 0.16,
    release: 0.34,
    pan: -0.3,
    panTo: 0.3,
    partials: [[1, 1], [2.01, 0.12]],
  });
  addEcho(signal, 0.14, 0.2);
}

function renderPulse(signal, random) {
  addTone(signal, {
    duration: 0.42,
    from: 52,
    to: 310,
    gain: 0.3,
    attack: 0.04,
    release: 0.04,
    pan: -0.45,
    panTo: 0.45,
    partials: [[1, 1], [2, 0.2]],
  });
  addNoise(signal, random, {
    duration: 0.42,
    gain: 0.14,
    attack: 0.08,
    release: 0.04,
    lowpass: 9_000,
    pan: -0.72,
    panTo: 0.72,
  });
  addTransient(signal, random, 0.38, 0.66, 74);
  addTone(signal, {
    start: 0.38,
    duration: 0.92,
    from: 112,
    to: 34,
    gain: 0.46,
    release: 0.46,
    partials: [[1, 1], [1.49, 0.31], [2.12, 0.13]],
  });
  addTone(signal, {
    start: 0.39,
    duration: 0.68,
    from: 2_800,
    to: 280,
    gain: 0.23,
    release: 0.34,
    pan: -0.75,
    panTo: 0.75,
    partials: [[1, 1], [2.02, 0.12]],
  });
  addEcho(signal, 0.17, 0.24);
}

function normalize(signal, peak) {
  let maximum = 0;
  for (let index = 0; index < signal.left.length; index += 1)
    maximum = Math.max(
      maximum,
      Math.abs(signal.left[index]),
      Math.abs(signal.right[index]),
    );
  const scale = maximum > 0 ? peak / maximum : 1;
  for (let index = 0; index < signal.left.length; index += 1) {
    signal.left[index] *= scale;
    signal.right[index] *= scale;
  }
}

function wavBytes(signal) {
  const dataBytes = signal.left.length * 4;
  const output = Buffer.alloc(44 + dataBytes);
  output.write('RIFF', 0);
  output.writeUInt32LE(36 + dataBytes, 4);
  output.write('WAVEfmt ', 8);
  output.writeUInt32LE(16, 16);
  output.writeUInt16LE(1, 20);
  output.writeUInt16LE(2, 22);
  output.writeUInt32LE(SAMPLE_RATE, 24);
  output.writeUInt32LE(SAMPLE_RATE * 4, 28);
  output.writeUInt16LE(4, 32);
  output.writeUInt16LE(16, 34);
  output.write('data', 36);
  output.writeUInt32LE(dataBytes, 40);
  for (let index = 0; index < signal.left.length; index += 1) {
    output.writeInt16LE(
      Math.round(Math.max(-1, Math.min(1, signal.left[index])) * 32_767),
      44 + index * 4,
    );
    output.writeInt16LE(
      Math.round(Math.max(-1, Math.min(1, signal.right[index])) * 32_767),
      46 + index * 4,
    );
  }
  return output;
}
