#!/usr/bin/env node

import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const SAMPLE_RATE = 44_100;
const ROOT = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
  'art',
  'audio',
  'sound-lab',
);

const cues = [
  {
    id: 'pulse-bolt',
    label: 'Pulse Bolt',
    category: 'PROJECTILE',
    description: 'The default shot: fast, readable, and light enough to repeat.',
    duration: 0.34,
  },
  {
    id: 'phase-needle',
    label: 'Phase Needle',
    category: 'PROJECTILE',
    description: 'A precise high-speed projectile with a surgical transient.',
    duration: 0.28,
  },
  {
    id: 'cinder-disc',
    label: 'Cinder Disc',
    category: 'PROJECTILE',
    description: 'A weightier launch with heat, grit, and a wider tail.',
    duration: 0.52,
  },
  {
    id: 'bot-hit',
    label: 'Bot Impact',
    category: 'COMBAT',
    description: 'Confirms damage without competing with the projectile identity.',
    duration: 0.34,
  },
  {
    id: 'wall-hit',
    label: 'Wall Impact',
    category: 'COMBAT',
    description: 'Duller and less rewarding than striking a bot.',
    duration: 0.42,
  },
  {
    id: 'bot-destroyed',
    label: 'Bot Destroyed',
    category: 'COMBAT',
    description: 'A compact mechanical collapse rather than a giant explosion.',
    duration: 1.08,
  },
  {
    id: 'zone-shift',
    label: 'Zone Secured',
    category: 'OBJECTIVE',
    description: 'A restrained upward cue for a meaningful control change.',
    duration: 1.14,
  },
  {
    id: 'countdown-start',
    label: 'Countdown / Start',
    category: 'MATCH',
    description: 'Three preparation ticks and a clean systems-live cue.',
    duration: 1.72,
  },
  {
    id: 'match-win',
    label: 'Match Won',
    category: 'MATCH',
    description: 'A short result stinger suitable for repeated ranked games.',
    duration: 1.62,
  },
  {
    id: 'entitlement-unlock',
    label: 'Reward Unlocked',
    category: 'REWARD',
    description: 'The richest cue, timed for the accomplishment toast.',
    duration: 2.42,
  },
];

const packs = [
  {
    id: 'vector-tactical',
    number: '01',
    label: 'Vector Tactical',
    kicker: 'CLEAN · DRY · COMPETITIVE',
    accent: '#64d8ff',
    description:
      'Precise transients, controlled tails, and strong material contrast. The safest fit for the current control-room art.',
    render: renderVector,
  },
  {
    id: 'foundry-signal',
    number: '02',
    label: 'Foundry Signal',
    kicker: 'MECHANICAL · WEIGHTED · INDUSTRIAL',
    accent: '#ffb45f',
    description:
      'Relays, resonant metal, pneumatic snaps, and heavier low mids. More physical and characterful without becoming noisy.',
    render: renderFoundry,
  },
  {
    id: 'neon-circuit',
    number: '03',
    label: 'Neon Circuit',
    kicker: 'SYNTHETIC · MUSICAL · EXPRESSIVE',
    accent: '#df87ff',
    description:
      'Glassy harmonics, compact digital motion, and a little more melody. The boldest direction and the most game-like.',
    render: renderNeon,
  },
];

function createBuffer(duration) {
  return new Float64Array(Math.ceil(duration * SAMPLE_RATE));
}

function seeded(seedText) {
  let state = 2_166_136_261;
  for (const character of seedText) {
    state ^= character.codePointAt(0);
    state = Math.imul(state, 16_777_619);
  }
  return () => {
    state = (Math.imul(state, 1_664_525) + 1_013_904_223) >>> 0;
    return state / 4_294_967_296;
  };
}

function wave(type, phase) {
  const sine = Math.sin(phase);
  switch (type) {
    case 'triangle':
      return (2 / Math.PI) * Math.asin(sine);
    case 'saw':
      return 2 * (phase / (Math.PI * 2) - Math.floor(phase / (Math.PI * 2) + 0.5));
    case 'square':
      return sine >= 0 ? 1 : -1;
    default:
      return sine;
  }
}

function envelope(t, duration, attack = 0.004, release = 0.08, decay = 0) {
  const attackGain = attack <= 0 ? 1 : Math.min(1, t / attack);
  const releaseGain =
    release <= 0 ? 1 : Math.min(1, Math.max(0, (duration - t) / release));
  const decayGain = decay <= 0 ? 1 : Math.exp((-decay * t) / duration);
  return attackGain * releaseGain * decayGain;
}

function addOsc(
  output,
  {
    start = 0,
    duration,
    from,
    to = from,
    amplitude = 0.2,
    type = 'sine',
    attack = 0.004,
    release = 0.08,
    decay = 0,
    vibratoRate = 0,
    vibratoDepth = 0,
    phaseOffset = 0,
  },
) {
  const first = Math.floor(start * SAMPLE_RATE);
  const count = Math.min(
    Math.ceil(duration * SAMPLE_RATE),
    Math.max(0, output.length - first),
  );
  let phase = phaseOffset;
  for (let index = 0; index < count; index++) {
    const t = index / SAMPLE_RATE;
    const progress = Math.min(1, t / Math.max(duration, 0.0001));
    const ratio = from > 0 && to > 0 ? to / from : 1;
    let frequency = from * ratio ** progress;
    if (vibratoRate > 0)
      frequency *= 1 + Math.sin(Math.PI * 2 * vibratoRate * t) * vibratoDepth;
    phase += (Math.PI * 2 * frequency) / SAMPLE_RATE;
    output[first + index] +=
      wave(type, phase) *
      amplitude *
      envelope(t, duration, attack, release, decay);
  }
}

function addFm(
  output,
  {
    start = 0,
    duration,
    carrier,
    carrierTo = carrier,
    modulator,
    index = 2,
    amplitude = 0.2,
    attack = 0.003,
    release = 0.08,
    decay = 0,
  },
) {
  const first = Math.floor(start * SAMPLE_RATE);
  const count = Math.min(
    Math.ceil(duration * SAMPLE_RATE),
    Math.max(0, output.length - first),
  );
  let carrierPhase = 0;
  let modulatorPhase = 0;
  for (let sample = 0; sample < count; sample++) {
    const t = sample / SAMPLE_RATE;
    const progress = Math.min(1, t / duration);
    const frequency = carrier * (carrierTo / carrier) ** progress;
    carrierPhase += (Math.PI * 2 * frequency) / SAMPLE_RATE;
    modulatorPhase += (Math.PI * 2 * modulator) / SAMPLE_RATE;
    output[first + sample] +=
      Math.sin(carrierPhase + Math.sin(modulatorPhase) * index) *
      amplitude *
      envelope(t, duration, attack, release, decay);
  }
}

function addNoise(
  output,
  random,
  {
    start = 0,
    duration,
    amplitude = 0.15,
    attack = 0,
    release = 0.08,
    decay = 2,
    lowpass = 12_000,
    highpass = 0,
    stepped = 1,
  },
) {
  const first = Math.floor(start * SAMPLE_RATE);
  const count = Math.min(
    Math.ceil(duration * SAMPLE_RATE),
    Math.max(0, output.length - first),
  );
  const lpCoefficient = 1 - Math.exp((-Math.PI * 2 * lowpass) / SAMPLE_RATE);
  const hpCoefficient =
    highpass > 0 ? 1 - Math.exp((-Math.PI * 2 * highpass) / SAMPLE_RATE) : 1;
  let low = 0;
  let highReference = 0;
  let held = 0;
  for (let sample = 0; sample < count; sample++) {
    if (sample % stepped === 0) held = random() * 2 - 1;
    low += (held - low) * lpCoefficient;
    highReference += (low - highReference) * hpCoefficient;
    const filtered = highpass > 0 ? low - highReference : low;
    const t = sample / SAMPLE_RATE;
    output[first + sample] +=
      filtered * amplitude * envelope(t, duration, attack, release, decay);
  }
}

function addBurst(output, random, start, amplitude, color = 'metal') {
  if (color === 'low') {
    addNoise(output, random, {
      start,
      duration: 0.18,
      amplitude,
      lowpass: 520,
      release: 0.12,
      decay: 4,
    });
    addOsc(output, {
      start,
      duration: 0.2,
      from: 115,
      to: 58,
      amplitude: amplitude * 0.7,
      release: 0.14,
      decay: 4,
    });
    return;
  }
  addNoise(output, random, {
    start,
    duration: 0.085,
    amplitude,
    lowpass: color === 'glass' ? 13_000 : 7_000,
    highpass: color === 'glass' ? 2_800 : 700,
    release: 0.06,
    decay: 6,
  });
  const frequency = color === 'glass' ? 2_900 : 760;
  addOsc(output, {
    start,
    duration: color === 'glass' ? 0.3 : 0.22,
    from: frequency,
    to: frequency * 0.86,
    amplitude: amplitude * 0.48,
    release: color === 'glass' ? 0.22 : 0.15,
    decay: 4,
  });
}

function addChord(output, notes, start, duration, amplitude, options = {}) {
  notes.forEach((frequency, index) =>
    addOsc(output, {
      start: start + (options.stagger ?? 0) * index,
      duration,
      from: frequency,
      amplitude: amplitude / Math.sqrt(notes.length),
      type: options.type ?? 'sine',
      attack: options.attack ?? 0.02,
      release: options.release ?? 0.3,
      decay: options.decay ?? 0.7,
      vibratoRate: options.vibratoRate ?? 0,
      vibratoDepth: options.vibratoDepth ?? 0,
    }),
  );
}

function addDelay(output, seconds, feedback = 0.2, mix = 0.3) {
  const delaySamples = Math.max(1, Math.round(seconds * SAMPLE_RATE));
  for (let sample = delaySamples; sample < output.length; sample++)
    output[sample] += output[sample - delaySamples] * feedback * mix;
}

function master(output, { target = 0.86, drive = 1.15, highpass = 22 } = {}) {
  const coefficient = 1 - Math.exp((-Math.PI * 2 * highpass) / SAMPLE_RATE);
  let reference = 0;
  let peak = 0;
  for (let sample = 0; sample < output.length; sample++) {
    reference += (output[sample] - reference) * coefficient;
    output[sample] = Math.tanh((output[sample] - reference) * drive);
    peak = Math.max(peak, Math.abs(output[sample]));
  }
  const scale = peak > 0 ? target / peak : 1;
  const tail = Math.min(output.length, Math.round(0.012 * SAMPLE_RATE));
  let squared = 0;
  peak = 0;
  for (let sample = 0; sample < output.length; sample++) {
    const fade =
      sample >= output.length - tail ? (output.length - sample - 1) / tail : 1;
    output[sample] *= scale * Math.max(0, fade);
    peak = Math.max(peak, Math.abs(output[sample]));
    squared += output[sample] ** 2;
  }
  return {
    peak,
    rms: Math.sqrt(squared / output.length),
  };
}

function renderVector(cue, output, random) {
  switch (cue.id) {
    case 'pulse-bolt':
      addNoise(output, random, {
        duration: 0.035,
        amplitude: 0.45,
        highpass: 2_600,
        decay: 7,
      });
      addOsc(output, {
        duration: 0.26,
        from: 1_250,
        to: 185,
        amplitude: 0.52,
        type: 'triangle',
        release: 0.12,
        decay: 2.8,
      });
      addOsc(output, {
        start: 0.015,
        duration: 0.18,
        from: 2_500,
        to: 620,
        amplitude: 0.16,
        release: 0.11,
        decay: 3,
      });
      break;
    case 'phase-needle':
      addNoise(output, random, {
        duration: 0.018,
        amplitude: 0.32,
        highpass: 6_000,
        decay: 9,
      });
      addOsc(output, {
        duration: 0.2,
        from: 4_800,
        to: 1_150,
        amplitude: 0.42,
        release: 0.13,
        decay: 2.2,
      });
      addOsc(output, {
        start: 0.016,
        duration: 0.18,
        from: 1_650,
        amplitude: 0.16,
        release: 0.13,
        decay: 4,
      });
      break;
    case 'cinder-disc':
      addBurst(output, random, 0, 0.44, 'low');
      addOsc(output, {
        duration: 0.38,
        from: 235,
        to: 78,
        amplitude: 0.44,
        type: 'saw',
        release: 0.2,
        decay: 2.2,
      });
      addNoise(output, random, {
        start: 0.055,
        duration: 0.34,
        amplitude: 0.17,
        highpass: 2_200,
        lowpass: 9_000,
        release: 0.19,
        decay: 1.5,
      });
      break;
    case 'bot-hit':
      addBurst(output, random, 0, 0.58, 'metal');
      addOsc(output, {
        duration: 0.24,
        from: 310,
        to: 230,
        amplitude: 0.29,
        release: 0.16,
        decay: 4,
      });
      break;
    case 'wall-hit':
      addBurst(output, random, 0, 0.64, 'low');
      addNoise(output, random, {
        duration: 0.24,
        amplitude: 0.26,
        lowpass: 1_400,
        release: 0.2,
        decay: 5,
      });
      break;
    case 'bot-destroyed':
      addBurst(output, random, 0, 0.56, 'low');
      addBurst(output, random, 0.115, 0.4, 'metal');
      addBurst(output, random, 0.24, 0.34, 'low');
      addOsc(output, {
        duration: 0.84,
        from: 380,
        to: 62,
        amplitude: 0.33,
        type: 'triangle',
        release: 0.3,
        decay: 1.8,
      });
      addNoise(output, random, {
        start: 0.18,
        duration: 0.65,
        amplitude: 0.15,
        highpass: 1_900,
        release: 0.3,
        decay: 2,
        stepped: 7,
      });
      break;
    case 'zone-shift':
      addChord(output, [293.66, 440, 587.33], 0, 0.95, 0.46, {
        stagger: 0.105,
        attack: 0.025,
        release: 0.44,
        decay: 0.45,
      });
      addOsc(output, {
        start: 0.29,
        duration: 0.7,
        from: 880,
        amplitude: 0.1,
        release: 0.42,
        decay: 1.4,
      });
      addDelay(output, 0.14, 0.26, 0.42);
      break;
    case 'countdown-start':
      for (const start of [0, 0.38, 0.76]) {
        addNoise(output, random, {
          start,
          duration: 0.025,
          amplitude: 0.34,
          highpass: 2_000,
          decay: 8,
        });
        addOsc(output, {
          start,
          duration: 0.15,
          from: 660,
          amplitude: 0.24,
          release: 0.1,
          decay: 4,
        });
      }
      addOsc(output, {
        start: 1.14,
        duration: 0.48,
        from: 220,
        to: 880,
        amplitude: 0.46,
        type: 'triangle',
        attack: 0.006,
        release: 0.24,
        decay: 0.7,
      });
      addNoise(output, random, {
        start: 1.14,
        duration: 0.09,
        amplitude: 0.28,
        highpass: 3_200,
        decay: 7,
      });
      break;
    case 'match-win':
      addChord(output, [293.66, 440, 587.33], 0, 1.25, 0.48, {
        stagger: 0.13,
        type: 'triangle',
        attack: 0.015,
        release: 0.52,
        decay: 0.65,
      });
      addOsc(output, {
        start: 0.42,
        duration: 0.95,
        from: 880,
        amplitude: 0.12,
        release: 0.5,
        decay: 1.2,
      });
      addDelay(output, 0.16, 0.2, 0.36);
      break;
    case 'entitlement-unlock':
      [440, 554.37, 659.25, 880].forEach((note, index) => {
        const start = index * 0.19;
        addOsc(output, {
          start,
          duration: 1.36,
          from: note,
          amplitude: 0.25,
          type: 'triangle',
          attack: 0.008,
          release: 0.76,
          decay: 1.6,
        });
        addOsc(output, {
          start: start + 0.015,
          duration: 1.05,
          from: note * 2,
          amplitude: 0.08,
          release: 0.66,
          decay: 2,
        });
      });
      addNoise(output, random, {
        start: 0.58,
        duration: 1.4,
        amplitude: 0.055,
        highpass: 7_500,
        release: 0.7,
        decay: 0.8,
        stepped: 5,
      });
      addDelay(output, 0.19, 0.34, 0.5);
      break;
  }
}

function renderFoundry(cue, output, random) {
  switch (cue.id) {
    case 'pulse-bolt':
      addBurst(output, random, 0, 0.48, 'metal');
      addOsc(output, {
        duration: 0.28,
        from: 510,
        to: 105,
        amplitude: 0.48,
        type: 'square',
        release: 0.13,
        decay: 3.4,
      });
      break;
    case 'phase-needle':
      addNoise(output, random, {
        duration: 0.024,
        amplitude: 0.54,
        highpass: 3_500,
        decay: 10,
      });
      addOsc(output, {
        duration: 0.2,
        from: 2_300,
        to: 720,
        amplitude: 0.32,
        type: 'saw',
        release: 0.12,
        decay: 3,
      });
      addBurst(output, random, 0.045, 0.22, 'metal');
      break;
    case 'cinder-disc':
      addBurst(output, random, 0, 0.62, 'low');
      addOsc(output, {
        start: 0.025,
        duration: 0.41,
        from: 175,
        to: 68,
        amplitude: 0.5,
        type: 'square',
        release: 0.21,
        decay: 2.6,
      });
      addNoise(output, random, {
        start: 0.08,
        duration: 0.35,
        amplitude: 0.18,
        highpass: 1_300,
        lowpass: 5_200,
        stepped: 4,
        release: 0.2,
        decay: 1.7,
      });
      break;
    case 'bot-hit':
      addBurst(output, random, 0, 0.72, 'metal');
      addOsc(output, {
        duration: 0.3,
        from: 640,
        to: 580,
        amplitude: 0.24,
        release: 0.23,
        decay: 3.7,
      });
      addOsc(output, {
        duration: 0.2,
        from: 125,
        to: 85,
        amplitude: 0.32,
        release: 0.12,
        decay: 4,
      });
      break;
    case 'wall-hit':
      addBurst(output, random, 0, 0.75, 'low');
      addNoise(output, random, {
        duration: 0.3,
        amplitude: 0.34,
        lowpass: 980,
        stepped: 3,
        release: 0.24,
        decay: 4.5,
      });
      addOsc(output, {
        duration: 0.34,
        from: 92,
        to: 58,
        amplitude: 0.34,
        release: 0.25,
        decay: 3,
      });
      break;
    case 'bot-destroyed':
      addBurst(output, random, 0, 0.7, 'metal');
      addBurst(output, random, 0.12, 0.68, 'low');
      addBurst(output, random, 0.31, 0.48, 'metal');
      addBurst(output, random, 0.49, 0.42, 'low');
      addOsc(output, {
        duration: 0.92,
        from: 245,
        to: 48,
        amplitude: 0.44,
        type: 'saw',
        release: 0.38,
        decay: 2.2,
      });
      addNoise(output, random, {
        start: 0.22,
        duration: 0.7,
        amplitude: 0.18,
        lowpass: 4_200,
        highpass: 400,
        stepped: 9,
        release: 0.36,
        decay: 1.8,
      });
      break;
    case 'zone-shift':
      addBurst(output, random, 0, 0.32, 'metal');
      addChord(output, [146.83, 220, 293.66], 0.05, 0.92, 0.53, {
        stagger: 0.13,
        type: 'triangle',
        attack: 0.012,
        release: 0.42,
        decay: 0.7,
      });
      addOsc(output, {
        start: 0.36,
        duration: 0.58,
        from: 590,
        to: 650,
        amplitude: 0.13,
        type: 'saw',
        release: 0.35,
        decay: 1,
      });
      break;
    case 'countdown-start':
      for (const [index, start] of [0, 0.38, 0.76].entries()) {
        addBurst(output, random, start, 0.42 + index * 0.04, 'metal');
        addOsc(output, {
          start,
          duration: 0.17,
          from: 190,
          amplitude: 0.24,
          release: 0.12,
          decay: 4,
        });
      }
      addBurst(output, random, 1.13, 0.56, 'low');
      addChord(output, [146.83, 220, 293.66], 1.13, 0.52, 0.55, {
        stagger: 0.025,
        type: 'saw',
        attack: 0.006,
        release: 0.26,
        decay: 1.8,
      });
      break;
    case 'match-win':
      addBurst(output, random, 0, 0.38, 'metal');
      addChord(output, [146.83, 220, 293.66, 369.99], 0.04, 1.34, 0.58, {
        stagger: 0.11,
        type: 'triangle',
        attack: 0.012,
        release: 0.62,
        decay: 0.75,
      });
      addDelay(output, 0.185, 0.18, 0.3);
      break;
    case 'entitlement-unlock':
      [220, 277.18, 329.63, 440].forEach((note, index) => {
        const start = index * 0.205;
        addBurst(output, random, start, 0.28, 'metal');
        addOsc(output, {
          start,
          duration: 1.45,
          from: note,
          amplitude: 0.3,
          type: 'triangle',
          attack: 0.006,
          release: 0.82,
          decay: 1.25,
        });
        addOsc(output, {
          start,
          duration: 1.25,
          from: note * 3.01,
          amplitude: 0.065,
          release: 0.72,
          decay: 1.8,
        });
      });
      addDelay(output, 0.22, 0.31, 0.43);
      break;
  }
}

function renderNeon(cue, output, random) {
  switch (cue.id) {
    case 'pulse-bolt':
      addFm(output, {
        duration: 0.29,
        carrier: 680,
        carrierTo: 145,
        modulator: 1_360,
        index: 3.4,
        amplitude: 0.52,
        release: 0.15,
        decay: 2.5,
      });
      addOsc(output, {
        duration: 0.2,
        from: 1_700,
        to: 430,
        amplitude: 0.14,
        release: 0.12,
        decay: 3,
      });
      break;
    case 'phase-needle':
      addFm(output, {
        duration: 0.22,
        carrier: 3_100,
        carrierTo: 920,
        modulator: 510,
        index: 5.5,
        amplitude: 0.46,
        release: 0.14,
        decay: 2.8,
      });
      addNoise(output, random, {
        duration: 0.025,
        amplitude: 0.22,
        highpass: 7_000,
        decay: 9,
      });
      break;
    case 'cinder-disc':
      addFm(output, {
        duration: 0.44,
        carrier: 260,
        carrierTo: 82,
        modulator: 72,
        index: 7,
        amplitude: 0.5,
        release: 0.23,
        decay: 2,
      });
      addNoise(output, random, {
        start: 0.08,
        duration: 0.31,
        amplitude: 0.12,
        highpass: 4_400,
        stepped: 8,
        release: 0.2,
        decay: 1.7,
      });
      break;
    case 'bot-hit':
      addFm(output, {
        duration: 0.28,
        carrier: 520,
        carrierTo: 240,
        modulator: 1_570,
        index: 4.2,
        amplitude: 0.5,
        release: 0.19,
        decay: 3.8,
      });
      addBurst(output, random, 0, 0.24, 'glass');
      break;
    case 'wall-hit':
      addFm(output, {
        duration: 0.34,
        carrier: 180,
        carrierTo: 72,
        modulator: 47,
        index: 3.5,
        amplitude: 0.5,
        release: 0.25,
        decay: 3.2,
      });
      addNoise(output, random, {
        duration: 0.16,
        amplitude: 0.2,
        lowpass: 1_500,
        release: 0.13,
        decay: 5,
      });
      break;
    case 'bot-destroyed':
      [0, 0.11, 0.22, 0.36].forEach((start, index) =>
        addFm(output, {
          start,
          duration: 0.46,
          carrier: 720 / (index + 1),
          carrierTo: 72,
          modulator: 92 + index * 37,
          index: 4 + index,
          amplitude: 0.35,
          release: 0.25,
          decay: 2.4,
        }),
      );
      addNoise(output, random, {
        start: 0.18,
        duration: 0.72,
        amplitude: 0.13,
        highpass: 3_400,
        stepped: 12,
        release: 0.36,
        decay: 1.6,
      });
      addDelay(output, 0.105, 0.22, 0.28);
      break;
    case 'zone-shift':
      [329.63, 493.88, 659.25].forEach((note, index) =>
        addFm(output, {
          start: index * 0.11,
          duration: 0.9,
          carrier: note,
          modulator: note * 2.01,
          index: 1.25,
          amplitude: 0.29,
          attack: 0.024,
          release: 0.48,
          decay: 0.75,
        }),
      );
      addDelay(output, 0.13, 0.34, 0.48);
      break;
    case 'countdown-start':
      [0, 0.38, 0.76].forEach((start, index) =>
        addFm(output, {
          start,
          duration: 0.18,
          carrier: 520 + index * 70,
          modulator: 1_040,
          index: 2.3,
          amplitude: 0.38,
          release: 0.11,
          decay: 4,
        }),
      );
      addFm(output, {
        start: 1.13,
        duration: 0.51,
        carrier: 220,
        carrierTo: 1_320,
        modulator: 110,
        index: 2.5,
        amplitude: 0.52,
        release: 0.25,
        decay: 0.8,
      });
      addDelay(output, 0.085, 0.25, 0.3);
      break;
    case 'match-win':
      [329.63, 415.3, 493.88, 659.25].forEach((note, index) =>
        addFm(output, {
          start: index * 0.12,
          duration: 1.22,
          carrier: note,
          modulator: note * 1.5,
          index: 1.6,
          amplitude: 0.25,
          attack: 0.012,
          release: 0.58,
          decay: 0.8,
        }),
      );
      addDelay(output, 0.145, 0.33, 0.47);
      break;
    case 'entitlement-unlock':
      [493.88, 622.25, 739.99, 987.77, 1_244.51].forEach((note, index) =>
        addFm(output, {
          start: index * 0.155,
          duration: 1.55,
          carrier: note,
          modulator: note * 2.005,
          index: 1.4,
          amplitude: 0.235,
          attack: 0.008,
          release: 0.88,
          decay: 1.3,
        }),
      );
      addNoise(output, random, {
        start: 0.45,
        duration: 1.55,
        amplitude: 0.05,
        highpass: 8_200,
        stepped: 9,
        release: 0.8,
        decay: 0.8,
      });
      addDelay(output, 0.16, 0.38, 0.56);
      break;
  }
}

function encodeWav(samples) {
  const dataBytes = samples.length * 2;
  const wav = Buffer.alloc(44 + dataBytes);
  wav.write('RIFF', 0);
  wav.writeUInt32LE(36 + dataBytes, 4);
  wav.write('WAVE', 8);
  wav.write('fmt ', 12);
  wav.writeUInt32LE(16, 16);
  wav.writeUInt16LE(1, 20);
  wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(SAMPLE_RATE, 24);
  wav.writeUInt32LE(SAMPLE_RATE * 2, 28);
  wav.writeUInt16LE(2, 32);
  wav.writeUInt16LE(16, 34);
  wav.write('data', 36);
  wav.writeUInt32LE(dataBytes, 40);
  for (let sample = 0; sample < samples.length; sample++) {
    const value = Math.max(-1, Math.min(1, samples[sample]));
    wav.writeInt16LE(Math.round(value * 32_767), 44 + sample * 2);
  }
  return wav;
}

async function main() {
  const manifest = {
    version: 1,
    generatedBy: 'scripts/generate-audio-candidates.mjs',
    sampleRate: SAMPLE_RATE,
    channels: 1,
    format: 'pcm-s16le-wav',
    packs: [],
  };

  for (const pack of packs) {
    const directory = path.join(ROOT, 'packs', pack.id);
    await mkdir(directory, { recursive: true });
    const renderedCues = [];
    for (const cue of cues) {
      const output = createBuffer(cue.duration);
      const random = seeded(`${pack.id}/${cue.id}/v1`);
      pack.render(cue, output, random);
      const measurements = master(output, {
        target: cue.id === 'entitlement-unlock' ? 0.82 : 0.86,
        drive: pack.id === 'foundry-signal' ? 1.3 : 1.15,
      });
      const filename = `${cue.id}.wav`;
      await writeFile(path.join(directory, filename), encodeWav(output));
      renderedCues.push({
        id: cue.id,
        label: cue.label,
        category: cue.category,
        description: cue.description,
        file: `./packs/${pack.id}/${filename}`,
        durationSeconds: Number((output.length / SAMPLE_RATE).toFixed(3)),
        peak: Number(measurements.peak.toFixed(4)),
        rms: Number(measurements.rms.toFixed(4)),
      });
    }
    manifest.packs.push({
      id: pack.id,
      number: pack.number,
      label: pack.label,
      kicker: pack.kicker,
      accent: pack.accent,
      description: pack.description,
      cues: renderedCues,
    });
  }

  const json = `${JSON.stringify(manifest, null, 2)}\n`;
  await writeFile(path.join(ROOT, 'manifest.json'), json);
  await writeFile(
    path.join(ROOT, 'manifest.js'),
    `window.SOUND_LAB_MANIFEST = ${JSON.stringify(manifest)};\n`,
  );
  const bytes = manifest.packs.reduce(
    (total, pack) =>
      total +
      pack.cues.reduce(
        (packTotal, cue) =>
          packTotal + Math.round(cue.durationSeconds * SAMPLE_RATE * 2 + 44),
        0,
      ),
    0,
  );
  console.log(
    `Generated ${manifest.packs.length * cues.length} cues ` +
      `(${(bytes / 1_048_576).toFixed(2)} MiB) in ${ROOT}`,
  );
}

await main();
