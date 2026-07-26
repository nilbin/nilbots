#!/usr/bin/env node

import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const SAMPLE_RATE = 48_000;
const RENDER_RATE = SAMPLE_RATE * 2;
const ROOT = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
  "art",
  "audio",
  "sound-lab",
);

const cues = [
  {
    id: "projectile-showcase",
    label: "Signature Projectile",
    category: "PROJECTILE",
    description:
      "Launch transient, powered body, and spatial tail—the identity test.",
    duration: 0.64,
  },
  {
    id: "armor-impact",
    label: "Armor Impact",
    category: "COMBAT",
    description:
      "Readable damage with physical material, depth, and controlled debris.",
    duration: 0.82,
  },
  {
    id: "bot-destroyed",
    label: "Bot Destroyed",
    category: "COMBAT",
    description:
      "A layered systems failure and mechanical collapse, not one synth drop.",
    duration: 1.78,
  },
  {
    id: "entitlement-unlock",
    label: "Reward Unlocked",
    category: "REWARD",
    description:
      "The production-value test: anticipation, reveal, bloom, and a clean tail.",
    duration: 3.36,
  },
];

const packs = [
  {
    id: "aegis-systems",
    number: "A",
    label: "Aegis Systems",
    kicker: "PRECISE · PREMIUM · TACTICAL",
    accent: "#68e3ff",
    description:
      "High-end competitive sci-fi: controlled power, milled-alloy detail, and spacious tails that stay out of the fight.",
    render: renderAegis,
  },
  {
    id: "obsidian-foundry",
    number: "B",
    label: "Obsidian Foundry",
    kicker: "PHYSICAL · DENSE · CINEMATIC",
    accent: "#ffad5c",
    description:
      "Electromagnets, heavy mechanisms, resonant plates, and real-feeling mass. Darker without becoming muddy.",
    render: renderObsidian,
  },
  {
    id: "aurora-core",
    number: "C",
    label: "Aurora Core",
    kicker: "LUMINOUS · ENERGETIC · MODERN",
    accent: "#c49aff",
    description:
      "Polished energy, glass-metal harmonics, and confident musical color. Expressive, but no retro arcade vocabulary.",
    render: renderAurora,
  },
];

const fusion = {
  id: "nilbots-signature-unlock",
  number: "D",
  label: "Nilbots Signature",
  kicker: "AEGIS CLARITY · OBSIDIAN WEIGHT · AURORA LIFT",
  accent: "#7edcff",
  description:
    "A cohesive reward signature: precise confirmation, a physical earned-it moment, and a luminous reveal that opens into stereo.",
};

function createStereo(duration) {
  const length = Math.ceil(duration * RENDER_RATE);
  return {
    left: new Float64Array(length),
    right: new Float64Array(length),
    sampleRate: RENDER_RATE,
  };
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

function panGains(pan) {
  const angle = ((Math.max(-1, Math.min(1, pan)) + 1) * Math.PI) / 4;
  return [Math.cos(angle), Math.sin(angle)];
}

function envelope(t, duration, attack, release, decay) {
  const attackGain =
    attack <= 0 ? 1 : Math.sin(Math.min(1, t / attack) * Math.PI * 0.5);
  const releaseGain =
    release <= 0
      ? 1
      : Math.sin(
          Math.min(1, Math.max(0, (duration - t) / release)) * Math.PI * 0.5,
        );
  const decayGain = decay <= 0 ? 1 : Math.exp(-t / decay);
  return attackGain * releaseGain * decayGain;
}

function addTone(
  output,
  {
    start = 0,
    duration,
    from,
    to = from,
    amplitude = 0.2,
    attack = 0.002,
    release = 0.08,
    decay = 0,
    pan = 0,
    panTo = pan,
    partials = [[1, 1]],
    fmRatio = 0,
    fmIndex = 0,
    fmDecay = 0.1,
    vibratoRate = 0,
    vibratoDepth = 0,
  },
) {
  const first = Math.floor(start * RENDER_RATE);
  const count = Math.min(
    Math.ceil(duration * RENDER_RATE),
    Math.max(0, output.left.length - first),
  );
  let phase = 0;
  let modulatorPhase = 0;
  const ratio = from > 0 && to > 0 ? to / from : 1;
  for (let index = 0; index < count; index++) {
    const t = index / RENDER_RATE;
    const progress = Math.min(1, t / Math.max(duration, 0.0001));
    let frequency = from * ratio ** progress;
    if (vibratoRate > 0) {
      frequency *=
        1 + Math.sin(Math.PI * 2 * vibratoRate * t) * vibratoDepth;
    }
    phase += (Math.PI * 2 * frequency) / RENDER_RATE;
    modulatorPhase +=
      (Math.PI * 2 * frequency * Math.max(0, fmRatio)) / RENDER_RATE;
    const modulation =
      fmRatio > 0
        ? Math.sin(modulatorPhase) *
          fmIndex *
          (fmDecay > 0 ? Math.exp(-t / fmDecay) : 1)
        : 0;
    let value = 0;
    for (const [partial, gain] of partials) {
      value += Math.sin(phase * partial + modulation) * gain;
    }
    const [leftGain, rightGain] = panGains(
      pan + (panTo - pan) * progress,
    );
    const gain =
      amplitude * envelope(t, duration, attack, release, decay);
    output.left[first + index] += value * gain * leftGain;
    output.right[first + index] += value * gain * rightGain;
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
    decay = 0.12,
    lowpass = 18_000,
    highpass = 0,
    pan = 0,
    panTo = pan,
    stereo = 0.35,
    roughness = 1,
  },
) {
  const first = Math.floor(start * RENDER_RATE);
  const count = Math.min(
    Math.ceil(duration * RENDER_RATE),
    Math.max(0, output.left.length - first),
  );
  const lp = 1 - Math.exp((-Math.PI * 2 * lowpass) / RENDER_RATE);
  const hp =
    highpass > 0
      ? 1 - Math.exp((-Math.PI * 2 * highpass) / RENDER_RATE)
      : 1;
  let lowL = 0;
  let lowR = 0;
  let highReferenceL = 0;
  let highReferenceR = 0;
  let heldL = 0;
  let heldR = 0;
  const hold = Math.max(1, Math.round(roughness));
  for (let index = 0; index < count; index++) {
    if (index % hold === 0) {
      const shared = random() * 2 - 1;
      heldL = shared * (1 - stereo) + (random() * 2 - 1) * stereo;
      heldR = shared * (1 - stereo) + (random() * 2 - 1) * stereo;
    }
    lowL += (heldL - lowL) * lp;
    lowR += (heldR - lowR) * lp;
    highReferenceL += (lowL - highReferenceL) * hp;
    highReferenceR += (lowR - highReferenceR) * hp;
    const t = index / RENDER_RATE;
    const progress = Math.min(1, t / Math.max(duration, 0.0001));
    const [leftGain, rightGain] = panGains(
      pan + (panTo - pan) * progress,
    );
    const gain =
      amplitude * envelope(t, duration, attack, release, decay);
    output.left[first + index] +=
      (highpass > 0 ? lowL - highReferenceL : lowL) * gain * leftGain;
    output.right[first + index] +=
      (highpass > 0 ? lowR - highReferenceR : lowR) * gain * rightGain;
  }
}

function addModes(
  output,
  {
    start = 0,
    frequencies,
    amplitude,
    decay,
    spread = 0.5,
    detune = 0.003,
  },
) {
  frequencies.forEach((frequency, index) => {
    const side = index % 2 === 0 ? -1 : 1;
    addTone(output, {
      start,
      duration: Math.min(
        output.left.length / RENDER_RATE - start,
        decay * 5.5,
      ),
      from: frequency * (1 + side * detune),
      to: frequency * (1 - side * detune * 0.4),
      amplitude: amplitude / (1 + index * 0.42),
      attack: 0.0008,
      release: Math.min(0.15, decay),
      decay: decay / (1 + index * 0.13),
      pan: side * spread * Math.min(1, 0.38 + index * 0.11),
      partials: [
        [1, 1],
        [2.003, index < 2 ? 0.16 : 0.08],
      ],
    });
  });
}

function addTransient(output, random, start, character, amplitude = 0.5) {
  if (character === "alloy") {
    addNoise(output, random, {
      start,
      duration: 0.052,
      amplitude,
      release: 0.04,
      decay: 0.012,
      lowpass: 17_500,
      highpass: 1_500,
      stereo: 0.58,
    });
    addModes(output, {
      start,
      frequencies: [730, 1_187, 2_014, 3_331],
      amplitude: amplitude * 0.36,
      decay: 0.09,
      spread: 0.7,
    });
    return;
  }
  if (character === "heavy") {
    addNoise(output, random, {
      start,
      duration: 0.105,
      amplitude,
      release: 0.085,
      decay: 0.026,
      lowpass: 4_200,
      highpass: 65,
      stereo: 0.28,
      roughness: 1.7,
    });
    addTone(output, {
      start,
      duration: 0.24,
      from: 118,
      to: 52,
      amplitude: amplitude * 0.7,
      attack: 0,
      release: 0.16,
      decay: 0.055,
      partials: [
        [1, 1],
        [2.08, 0.18],
      ],
    });
    return;
  }
  addNoise(output, random, {
    start,
    duration: 0.075,
    amplitude,
    release: 0.06,
    decay: 0.018,
    lowpass: 20_000,
    highpass: 4_800,
    stereo: 0.82,
  });
  addModes(output, {
    start,
    frequencies: [2_350, 3_710, 5_940, 8_230],
    amplitude: amplitude * 0.23,
    decay: 0.13,
    spread: 0.9,
  });
}

function addDebris(
  output,
  random,
  {
    start,
    duration,
    count,
    low = 480,
    high = 5_600,
    amplitude = 0.12,
    metallic = true,
  },
) {
  for (let index = 0; index < count; index++) {
    const grainStart = start + random() ** 0.72 * duration;
    const frequency = low * (high / low) ** random();
    const grainDuration = 0.035 + random() * 0.14;
    const pan = random() * 1.8 - 0.9;
    if (metallic) {
      addTone(output, {
        start: grainStart,
        duration: grainDuration,
        from: frequency,
        to: frequency * (0.9 + random() * 0.13),
        amplitude: amplitude * (0.45 + random() * 0.7),
        attack: 0,
        release: grainDuration * 0.7,
        decay: grainDuration * 0.28,
        pan,
        partials: [
          [1, 1],
          [1.61, 0.28],
          [2.37, 0.12],
        ],
      });
    }
    addNoise(output, random, {
      start: grainStart,
      duration: Math.min(0.055, grainDuration),
      amplitude: amplitude * 0.5,
      release: 0.04,
      decay: 0.015,
      lowpass: Math.min(20_000, frequency * 3.5),
      highpass: Math.max(120, frequency * 0.4),
      pan,
      stereo: 0.6,
    });
  }
}

function addChime(
  output,
  {
    start,
    frequency,
    duration,
    amplitude,
    pan,
    color = "glass",
  },
) {
  const partials =
    color === "bronze"
      ? [
          [1, 1],
          [1.49, 0.28],
          [2.03, 0.19],
          [2.71, 0.1],
          [4.12, 0.05],
        ]
      : [
          [1, 1],
          [2.006, 0.24],
          [3.98, 0.11],
          [6.17, 0.055],
        ];
  addTone(output, {
    start,
    duration,
    from: frequency,
    to: frequency * 0.998,
    amplitude,
    attack: 0.003,
    release: Math.min(0.6, duration * 0.4),
    decay: duration * 0.33,
    pan,
    panTo: pan * -0.25,
    partials,
    fmRatio: color === "bronze" ? 1.49 : 2.01,
    fmIndex: color === "bronze" ? 0.18 : 0.09,
    fmDecay: 0.24,
  });
}

function addPad(
  output,
  notes,
  start,
  duration,
  amplitude,
  { shimmer = false } = {},
) {
  notes.forEach((note, index) => {
    const pan = notes.length === 1 ? 0 : -0.72 + (index * 1.44) / (notes.length - 1);
    addTone(output, {
      start,
      duration,
      from: note,
      amplitude: amplitude / Math.sqrt(notes.length),
      attack: 0.14,
      release: Math.min(1.1, duration * 0.45),
      decay: duration * 1.5,
      pan,
      panTo: -pan * 0.35,
      vibratoRate: 0.23 + index * 0.07,
      vibratoDepth: 0.0018,
      partials: shimmer
        ? [
            [1, 1],
            [2.002, 0.25],
            [3.004, 0.08],
          ]
        : [
            [1, 1],
            [2.001, 0.12],
          ],
    });
  });
}

function addPingPong(output, seconds, feedback, mix) {
  const delay = Math.max(1, Math.round(seconds * RENDER_RATE));
  const sourceLeft = output.left.slice();
  const sourceRight = output.right.slice();
  for (let index = delay; index < output.left.length; index++) {
    const leftEcho =
      sourceRight[index - delay] +
      (index >= delay * 2 ? output.left[index - delay * 2] * feedback : 0);
    const rightEcho =
      sourceLeft[index - delay] +
      (index >= delay * 2 ? output.right[index - delay * 2] * feedback : 0);
    output.left[index] += leftEcho * mix;
    output.right[index] += rightEcho * mix;
  }
}

function allpass(channel, delay, gain) {
  const result = new Float64Array(channel.length);
  for (let index = 0; index < channel.length; index++) {
    const delayedInput = index >= delay ? channel[index - delay] : 0;
    const delayedOutput = index >= delay ? result[index - delay] : 0;
    result[index] = -gain * channel[index] + delayedInput + gain * delayedOutput;
  }
  return result;
}

function reverbChannel(input, delays, feedback, damping, predelay) {
  const sum = new Float64Array(input.length);
  for (const delay of delays) {
    const comb = new Float64Array(input.length);
    let filtered = 0;
    for (let index = 0; index < input.length; index++) {
      const excitation = index >= predelay ? input[index - predelay] : 0;
      const delayed = index >= delay ? comb[index - delay] : 0;
      filtered += (delayed - filtered) * (1 - damping);
      comb[index] = excitation + filtered * feedback;
      sum[index] += comb[index] / delays.length;
    }
  }
  return allpass(
    allpass(sum, Math.round(0.0047 * RENDER_RATE), 0.61),
    Math.round(0.0019 * RENDER_RATE),
    0.53,
  );
}

function addReverb(
  output,
  { mix = 0.18, decay = 0.72, predelay = 0.012, width = 0.82 } = {},
) {
  const feedback = Math.max(0.25, Math.min(0.86, 0.38 + decay * 0.37));
  const leftWet = reverbChannel(
    output.left,
    [0.0277, 0.0311, 0.0367, 0.0411].map((value) =>
      Math.round(value * RENDER_RATE),
    ),
    feedback,
    0.43,
    Math.round(predelay * RENDER_RATE),
  );
  const rightWet = reverbChannel(
    output.right,
    [0.0293, 0.0337, 0.0389, 0.0437].map((value) =>
      Math.round(value * RENDER_RATE),
    ),
    feedback * 0.995,
    0.46,
    Math.round((predelay + 0.0017) * RENDER_RATE),
  );
  for (let index = 0; index < output.left.length; index++) {
    const left = leftWet[index] * width + rightWet[index] * (1 - width);
    const right = rightWet[index] * width + leftWet[index] * (1 - width);
    output.left[index] += left * mix;
    output.right[index] += right * mix;
  }
}

function biquad(channel, type, frequency, q = 0.707) {
  const omega = (Math.PI * 2 * frequency) / RENDER_RATE;
  const cosine = Math.cos(omega);
  const sine = Math.sin(omega);
  const alpha = sine / (2 * q);
  let b0;
  let b1;
  let b2;
  let a0 = 1 + alpha;
  const a1 = -2 * cosine;
  const a2 = 1 - alpha;
  if (type === "highpass") {
    b0 = (1 + cosine) / 2;
    b1 = -(1 + cosine);
    b2 = (1 + cosine) / 2;
  } else {
    b0 = (1 - cosine) / 2;
    b1 = 1 - cosine;
    b2 = (1 - cosine) / 2;
  }
  b0 /= a0;
  b1 /= a0;
  b2 /= a0;
  const normalizedA1 = a1 / a0;
  const normalizedA2 = a2 / a0;
  let x1 = 0;
  let x2 = 0;
  let y1 = 0;
  let y2 = 0;
  for (let index = 0; index < channel.length; index++) {
    const x = channel[index];
    const y =
      b0 * x +
      b1 * x1 +
      b2 * x2 -
      normalizedA1 * y1 -
      normalizedA2 * y2;
    channel[index] = y;
    x2 = x1;
    x1 = x;
    y2 = y1;
    y1 = y;
  }
}

function renderAegis(cue, output, random) {
  switch (cue.id) {
    case "projectile-showcase":
      addTransient(output, random, 0, "alloy", 0.62);
      addTone(output, {
        duration: 0.42,
        from: 2_850,
        to: 172,
        amplitude: 0.52,
        release: 0.17,
        decay: 0.12,
        pan: -0.08,
        panTo: 0.46,
        partials: [
          [1, 1],
          [2.01, 0.17],
          [3.97, 0.045],
        ],
        fmRatio: 0.51,
        fmIndex: 0.55,
        fmDecay: 0.07,
      });
      addTone(output, {
        start: 0.008,
        duration: 0.3,
        from: 188,
        to: 62,
        amplitude: 0.46,
        release: 0.2,
        decay: 0.075,
        pan: 0.05,
      });
      addNoise(output, random, {
        start: 0.018,
        duration: 0.44,
        amplitude: 0.12,
        attack: 0.006,
        release: 0.2,
        decay: 0.11,
        lowpass: 15_500,
        highpass: 3_800,
        pan: -0.5,
        panTo: 0.7,
        stereo: 0.78,
      });
      addPingPong(output, 0.067, 0.18, 0.11);
      addReverb(output, { mix: 0.095, decay: 0.46, predelay: 0.006 });
      break;
    case "armor-impact":
      addTransient(output, random, 0, "alloy", 0.82);
      addTransient(output, random, 0.006, "heavy", 0.46);
      addModes(output, {
        start: 0.003,
        frequencies: [218, 391, 677, 1_109, 1_817, 3_013],
        amplitude: 0.5,
        decay: 0.16,
        spread: 0.74,
      });
      addDebris(output, random, {
        start: 0.038,
        duration: 0.3,
        count: 13,
        low: 950,
        high: 7_800,
        amplitude: 0.07,
      });
      addReverb(output, { mix: 0.14, decay: 0.56, predelay: 0.009 });
      break;
    case "bot-destroyed":
      addTransient(output, random, 0, "heavy", 0.78);
      addTransient(output, random, 0.018, "alloy", 0.54);
      addTone(output, {
        duration: 1.18,
        from: 286,
        to: 38,
        amplitude: 0.5,
        attack: 0,
        release: 0.36,
        decay: 0.28,
        fmRatio: 1.43,
        fmIndex: 1.2,
        fmDecay: 0.21,
      });
      for (const [start, strength] of [
        [0.12, 0.48],
        [0.31, 0.4],
        [0.57, 0.28],
      ]) {
        addTransient(output, random, start, "alloy", strength);
      }
      addModes(output, {
        start: 0.09,
        frequencies: [154, 263, 446, 733, 1_193],
        amplitude: 0.34,
        decay: 0.38,
        spread: 0.8,
      });
      addDebris(output, random, {
        start: 0.08,
        duration: 1.03,
        count: 31,
        low: 380,
        high: 8_800,
        amplitude: 0.075,
      });
      addNoise(output, random, {
        start: 0.16,
        duration: 0.9,
        amplitude: 0.13,
        release: 0.38,
        decay: 0.28,
        lowpass: 7_200,
        highpass: 380,
        stereo: 0.74,
        roughness: 2.2,
      });
      addReverb(output, { mix: 0.17, decay: 0.78, predelay: 0.013 });
      break;
    case "entitlement-unlock": {
      addTransient(output, random, 0, "alloy", 0.22);
      addTone(output, {
        start: 0.02,
        duration: 0.72,
        from: 86,
        to: 172,
        amplitude: 0.3,
        attack: 0.025,
        release: 0.34,
        decay: 0.34,
      });
      const notes = [293.66, 369.99, 440, 659.25, 880];
      notes.forEach((note, index) =>
        addChime(output, {
          start: 0.2 + index * 0.16,
          frequency: note,
          duration: 2.35 - index * 0.09,
          amplitude: 0.27,
          pan: -0.7 + index * 0.35,
        }),
      );
      addPad(output, [146.83, 220, 293.66, 369.99], 0.43, 2.62, 0.34, {
        shimmer: true,
      });
      addDebris(output, random, {
        start: 0.48,
        duration: 1.48,
        count: 25,
        low: 4_200,
        high: 14_500,
        amplitude: 0.027,
      });
      addPingPong(output, 0.173, 0.36, 0.15);
      addReverb(output, {
        mix: 0.27,
        decay: 1.38,
        predelay: 0.026,
        width: 0.9,
      });
      break;
    }
  }
}

function renderObsidian(cue, output, random) {
  switch (cue.id) {
    case "projectile-showcase":
      addTransient(output, random, 0, "heavy", 0.68);
      addTransient(output, random, 0.014, "alloy", 0.39);
      addTone(output, {
        duration: 0.46,
        from: 740,
        to: 68,
        amplitude: 0.57,
        release: 0.22,
        decay: 0.13,
        pan: -0.22,
        panTo: 0.38,
        partials: [
          [1, 1],
          [1.51, 0.29],
          [2.27, 0.12],
        ],
        fmRatio: 0.25,
        fmIndex: 2.1,
        fmDecay: 0.11,
      });
      addNoise(output, random, {
        start: 0.035,
        duration: 0.42,
        amplitude: 0.18,
        attack: 0.004,
        release: 0.22,
        decay: 0.13,
        lowpass: 6_800,
        highpass: 420,
        pan: 0.5,
        panTo: -0.5,
        stereo: 0.5,
        roughness: 1.5,
      });
      addReverb(output, { mix: 0.1, decay: 0.42, predelay: 0.005 });
      break;
    case "armor-impact":
      addTransient(output, random, 0, "heavy", 0.92);
      addTransient(output, random, 0.009, "alloy", 0.48);
      addModes(output, {
        start: 0.004,
        frequencies: [92, 147, 239, 383, 617, 991, 1_591],
        amplitude: 0.58,
        decay: 0.25,
        spread: 0.62,
        detune: 0.006,
      });
      addNoise(output, random, {
        start: 0.015,
        duration: 0.39,
        amplitude: 0.22,
        release: 0.25,
        decay: 0.08,
        lowpass: 3_600,
        highpass: 110,
        stereo: 0.31,
        roughness: 2.4,
      });
      addDebris(output, random, {
        start: 0.05,
        duration: 0.38,
        count: 16,
        low: 380,
        high: 4_500,
        amplitude: 0.075,
      });
      addReverb(output, { mix: 0.16, decay: 0.68, predelay: 0.011 });
      break;
    case "bot-destroyed":
      addTransient(output, random, 0, "heavy", 0.88);
      addTone(output, {
        duration: 1.32,
        from: 214,
        to: 31,
        amplitude: 0.56,
        attack: 0,
        release: 0.4,
        decay: 0.31,
        fmRatio: 0.37,
        fmIndex: 3.6,
        fmDecay: 0.28,
        partials: [
          [1, 1],
          [1.47, 0.3],
          [2.11, 0.1],
        ],
      });
      for (const [start, character, strength] of [
        [0.08, "alloy", 0.53],
        [0.24, "heavy", 0.51],
        [0.43, "alloy", 0.41],
        [0.68, "heavy", 0.31],
      ]) {
        addTransient(output, random, start, character, strength);
      }
      addModes(output, {
        start: 0.11,
        frequencies: [83, 132, 219, 357, 571, 919],
        amplitude: 0.45,
        decay: 0.48,
        spread: 0.72,
      });
      addDebris(output, random, {
        start: 0.1,
        duration: 1.15,
        count: 38,
        low: 210,
        high: 5_400,
        amplitude: 0.085,
      });
      addNoise(output, random, {
        start: 0.13,
        duration: 1.08,
        amplitude: 0.18,
        release: 0.42,
        decay: 0.3,
        lowpass: 5_100,
        highpass: 75,
        stereo: 0.46,
        roughness: 3.2,
      });
      addReverb(output, {
        mix: 0.2,
        decay: 0.96,
        predelay: 0.015,
        width: 0.78,
      });
      break;
    case "entitlement-unlock": {
      addTransient(output, random, 0, "heavy", 0.33);
      addTransient(output, random, 0.12, "alloy", 0.31);
      addTone(output, {
        start: 0.05,
        duration: 0.9,
        from: 72,
        to: 144,
        amplitude: 0.34,
        attack: 0.04,
        release: 0.42,
        decay: 0.44,
        partials: [
          [1, 1],
          [1.5, 0.23],
          [2, 0.12],
        ],
      });
      const notes = [220, 277.18, 329.63, 440, 554.37];
      notes.forEach((note, index) =>
        addChime(output, {
          start: 0.26 + index * 0.18,
          frequency: note,
          duration: 2.45 - index * 0.08,
          amplitude: 0.3,
          pan: -0.68 + index * 0.34,
          color: "bronze",
        }),
      );
      addPad(output, [110, 164.81, 220, 277.18], 0.46, 2.65, 0.39);
      addDebris(output, random, {
        start: 0.3,
        duration: 1.35,
        count: 14,
        low: 1_200,
        high: 6_700,
        amplitude: 0.035,
      });
      addPingPong(output, 0.211, 0.29, 0.13);
      addReverb(output, {
        mix: 0.26,
        decay: 1.46,
        predelay: 0.032,
        width: 0.84,
      });
      break;
    }
  }
}

function renderAurora(cue, output, random) {
  switch (cue.id) {
    case "projectile-showcase":
      addTransient(output, random, 0, "glass", 0.54);
      addTone(output, {
        duration: 0.48,
        from: 3_900,
        to: 215,
        amplitude: 0.49,
        release: 0.2,
        decay: 0.15,
        pan: -0.66,
        panTo: 0.76,
        partials: [
          [1, 1],
          [2.005, 0.21],
          [3.01, 0.07],
        ],
        fmRatio: 2.013,
        fmIndex: 1.15,
        fmDecay: 0.13,
      });
      addTone(output, {
        start: 0.004,
        duration: 0.38,
        from: 330,
        to: 74,
        amplitude: 0.4,
        release: 0.23,
        decay: 0.1,
        pan: 0.18,
        partials: [
          [1, 1],
          [2.01, 0.15],
        ],
      });
      addNoise(output, random, {
        start: 0.012,
        duration: 0.48,
        amplitude: 0.13,
        attack: 0.005,
        release: 0.27,
        decay: 0.14,
        lowpass: 20_000,
        highpass: 6_500,
        pan: 0.62,
        panTo: -0.7,
        stereo: 0.9,
      });
      addPingPong(output, 0.052, 0.22, 0.14);
      addReverb(output, { mix: 0.16, decay: 0.62, predelay: 0.008 });
      break;
    case "armor-impact":
      addTransient(output, random, 0, "glass", 0.78);
      addTransient(output, random, 0.004, "alloy", 0.45);
      addTone(output, {
        duration: 0.48,
        from: 1_020,
        to: 182,
        amplitude: 0.43,
        release: 0.26,
        decay: 0.12,
        fmRatio: 1.618,
        fmIndex: 1.7,
        fmDecay: 0.14,
      });
      addModes(output, {
        start: 0.006,
        frequencies: [446, 713, 1_157, 1_871, 3_017, 4_883],
        amplitude: 0.42,
        decay: 0.24,
        spread: 0.9,
      });
      addDebris(output, random, {
        start: 0.04,
        duration: 0.42,
        count: 19,
        low: 1_900,
        high: 12_800,
        amplitude: 0.055,
      });
      addReverb(output, {
        mix: 0.2,
        decay: 0.83,
        predelay: 0.014,
        width: 0.92,
      });
      break;
    case "bot-destroyed":
      addTransient(output, random, 0, "glass", 0.72);
      addTransient(output, random, 0.008, "heavy", 0.53);
      addTone(output, {
        duration: 1.17,
        from: 520,
        to: 42,
        amplitude: 0.52,
        release: 0.42,
        decay: 0.3,
        fmRatio: 1.618,
        fmIndex: 3.8,
        fmDecay: 0.31,
        pan: -0.32,
        panTo: 0.4,
      });
      for (const [start, frequency, pan] of [
        [0.09, 1_760, -0.7],
        [0.18, 1_080, 0.65],
        [0.31, 690, -0.48],
        [0.51, 410, 0.42],
      ]) {
        addTone(output, {
          start,
          duration: 0.72,
          from: frequency,
          to: frequency * 0.21,
          amplitude: 0.25,
          release: 0.3,
          decay: 0.18,
          pan,
          fmRatio: 2.01,
          fmIndex: 1.8,
          fmDecay: 0.15,
        });
      }
      addDebris(output, random, {
        start: 0.07,
        duration: 1.1,
        count: 36,
        low: 900,
        high: 13_800,
        amplitude: 0.058,
      });
      addNoise(output, random, {
        start: 0.12,
        duration: 1.02,
        amplitude: 0.14,
        release: 0.43,
        decay: 0.31,
        lowpass: 14_000,
        highpass: 1_100,
        stereo: 0.9,
        roughness: 1.4,
      });
      addPingPong(output, 0.081, 0.24, 0.1);
      addReverb(output, {
        mix: 0.24,
        decay: 1.08,
        predelay: 0.018,
        width: 0.94,
      });
      break;
    case "entitlement-unlock": {
      addTone(output, {
        duration: 0.78,
        from: 110,
        to: 330,
        amplitude: 0.32,
        attack: 0.025,
        release: 0.36,
        decay: 0.4,
        fmRatio: 0.5,
        fmIndex: 0.8,
        fmDecay: 0.2,
      });
      addTransient(output, random, 0.18, "glass", 0.28);
      const notes = [329.63, 415.3, 493.88, 659.25, 987.77];
      notes.forEach((note, index) =>
        addChime(output, {
          start: 0.19 + index * 0.145,
          frequency: note,
          duration: 2.48 - index * 0.065,
          amplitude: 0.265,
          pan: -0.78 + index * 0.39,
        }),
      );
      addPad(output, [164.81, 246.94, 329.63, 415.3], 0.39, 2.72, 0.37, {
        shimmer: true,
      });
      addTone(output, {
        start: 0.83,
        duration: 1.82,
        from: 1_318.51,
        to: 1_325,
        amplitude: 0.1,
        attack: 0.04,
        release: 1.15,
        decay: 0.9,
        pan: -0.7,
        panTo: 0.7,
        partials: [
          [1, 1],
          [2.003, 0.18],
        ],
      });
      addDebris(output, random, {
        start: 0.42,
        duration: 1.65,
        count: 31,
        low: 5_500,
        high: 17_500,
        amplitude: 0.024,
      });
      addPingPong(output, 0.147, 0.37, 0.17);
      addReverb(output, {
        mix: 0.31,
        decay: 1.62,
        predelay: 0.028,
        width: 0.95,
      });
      break;
    }
  }
}

function renderSignatureUnlock(output, random) {
  // Aegis: a clean milled-alloy confirmation that reads immediately.
  addTransient(output, random, 0, "alloy", 0.38);
  addTone(output, {
    start: 0.008,
    duration: 0.48,
    from: 1_180,
    to: 590,
    amplitude: 0.17,
    attack: 0.001,
    release: 0.24,
    decay: 0.11,
    pan: -0.1,
    panTo: 0.1,
    partials: [
      [1, 1],
      [2.01, 0.14],
    ],
  });

  // Obsidian: the tangible latch and low mechanical weight of an earned item.
  addTransient(output, random, 0.105, "heavy", 0.42);
  addModes(output, {
    start: 0.108,
    frequencies: [92, 149, 241, 389, 631],
    amplitude: 0.23,
    decay: 0.28,
    spread: 0.52,
    detune: 0.004,
  });
  addTone(output, {
    start: 0.08,
    duration: 0.92,
    from: 82,
    to: 164,
    amplitude: 0.31,
    attack: 0.035,
    release: 0.43,
    decay: 0.4,
    partials: [
      [1, 1],
      [1.5, 0.2],
      [2.01, 0.1],
    ],
  });

  // Aurora: the upward reveal, harmonic bloom, and confident stereo opening.
  addTransient(output, random, 0.31, "glass", 0.2);
  const notes = [293.66, 369.99, 440, 587.33, 739.99, 880];
  notes.forEach((note, index) =>
    addChime(output, {
      start: 0.25 + index * 0.135,
      frequency: note,
      duration: 2.52 - index * 0.07,
      amplitude: 0.245 + index * 0.006,
      pan: -0.76 + index * 0.304,
    }),
  );
  addPad(output, [146.83, 220, 293.66, 369.99], 0.46, 2.62, 0.32, {
    shimmer: true,
  });
  addTone(output, {
    start: 0.76,
    duration: 1.9,
    from: 1_174.66,
    to: 1_185,
    amplitude: 0.085,
    attack: 0.05,
    release: 1.18,
    decay: 0.96,
    pan: -0.76,
    panTo: 0.76,
    partials: [
      [1, 1],
      [2.003, 0.16],
    ],
  });
  addDebris(output, random, {
    start: 0.48,
    duration: 1.46,
    count: 24,
    low: 4_800,
    high: 16_500,
    amplitude: 0.021,
  });
  addPingPong(output, 0.157, 0.34, 0.145);
  addReverb(output, {
    mix: 0.285,
    decay: 1.5,
    predelay: 0.028,
    width: 0.92,
  });
}

function prepareForDownsampling(output, drive) {
  for (const channel of [output.left, output.right]) {
    biquad(channel, "highpass", 27, 0.707);
    biquad(channel, "lowpass", 21_500, 0.66);
  }

  let envelopeFollower = 0;
  const attack = 1 - Math.exp(-1 / (0.0025 * RENDER_RATE));
  const release = 1 - Math.exp(-1 / (0.09 * RENDER_RATE));
  const threshold = 0.42;
  const ratio = 3.2;
  for (let index = 0; index < output.left.length; index++) {
    output.left[index] =
      Math.tanh(output.left[index] * drive) / Math.tanh(drive);
    output.right[index] =
      Math.tanh(output.right[index] * drive) / Math.tanh(drive);
    const level = Math.max(
      Math.abs(output.left[index]),
      Math.abs(output.right[index]),
    );
    envelopeFollower +=
      (level - envelopeFollower) *
      (level > envelopeFollower ? attack : release);
    const compressed =
      envelopeFollower > threshold
        ? threshold + (envelopeFollower - threshold) / ratio
        : envelopeFollower;
    const gain =
      envelopeFollower > 0 ? Math.min(1, compressed / envelopeFollower) : 1;
    output.left[index] *= gain;
    output.right[index] *= gain;
  }
}

function downsample(channel) {
  const outputLength = Math.ceil(channel.length / 2);
  const result = new Float64Array(outputLength);
  const radius = 20;
  const cutoff = 0.235;
  const kernel = [];
  let kernelSum = 0;
  for (let offset = -radius; offset <= radius; offset++) {
    const sinc =
      offset === 0
        ? 2 * cutoff
        : Math.sin(2 * Math.PI * cutoff * offset) / (Math.PI * offset);
    const window =
      0.42 +
      0.5 * Math.cos((Math.PI * offset) / radius) +
      0.08 * Math.cos((2 * Math.PI * offset) / radius);
    const coefficient = sinc * window;
    kernel.push(coefficient);
    kernelSum += coefficient;
  }
  for (let index = 0; index < outputLength; index++) {
    const center = index * 2;
    let value = 0;
    for (let tap = 0; tap < kernel.length; tap++) {
      const sourceIndex = center + tap - radius;
      if (sourceIndex >= 0 && sourceIndex < channel.length) {
        value += channel[sourceIndex] * kernel[tap];
      }
    }
    result[index] = value / kernelSum;
  }
  return result;
}

function finalize(output, targetPeak) {
  prepareForDownsampling(output, 1.08);
  const left = downsample(output.left);
  const right = downsample(output.right);
  let peak = 0;
  for (let index = 0; index < left.length; index++) {
    peak = Math.max(peak, Math.abs(left[index]), Math.abs(right[index]));
  }
  const scale = peak > 0 ? targetPeak / peak : 1;
  const fadeIn = Math.round(0.0005 * SAMPLE_RATE);
  const fadeOut = Math.round(0.024 * SAMPLE_RATE);
  let sumSquares = 0;
  let differenceSquares = 0;
  let sumLeft = 0;
  let sumRight = 0;
  peak = 0;
  for (let index = 0; index < left.length; index++) {
    const head = Math.min(1, index / Math.max(1, fadeIn));
    const tail = Math.min(
      1,
      Math.max(0, (left.length - index - 1) / Math.max(1, fadeOut)),
    );
    const gain = scale * head * tail;
    left[index] *= gain;
    right[index] *= gain;
    peak = Math.max(peak, Math.abs(left[index]), Math.abs(right[index]));
    sumSquares += left[index] ** 2 + right[index] ** 2;
    differenceSquares += (left[index] - right[index]) ** 2;
    sumLeft += left[index];
    sumRight += right[index];
  }
  return {
    left,
    right,
    peak,
    rms: Math.sqrt(sumSquares / (left.length * 2)),
    stereoDifferenceRms: Math.sqrt(differenceSquares / left.length),
    dcOffset: Math.max(
      Math.abs(sumLeft / left.length),
      Math.abs(sumRight / right.length),
    ),
  };
}

function encodeWav(audio, random) {
  const frames = audio.left.length;
  const dataBytes = frames * 2 * 2;
  const wav = Buffer.alloc(44 + dataBytes);
  wav.write("RIFF", 0);
  wav.writeUInt32LE(36 + dataBytes, 4);
  wav.write("WAVE", 8);
  wav.write("fmt ", 12);
  wav.writeUInt32LE(16, 16);
  wav.writeUInt16LE(1, 20);
  wav.writeUInt16LE(2, 22);
  wav.writeUInt32LE(SAMPLE_RATE, 24);
  wav.writeUInt32LE(SAMPLE_RATE * 4, 28);
  wav.writeUInt16LE(4, 32);
  wav.writeUInt16LE(16, 34);
  wav.write("data", 36);
  wav.writeUInt32LE(dataBytes, 40);
  for (let frame = 0; frame < frames; frame++) {
    for (let channel = 0; channel < 2; channel++) {
      const sample = channel === 0 ? audio.left[frame] : audio.right[frame];
      const dither = (random() - random()) / 65_536;
      const value = Math.max(-1, Math.min(1, sample + dither));
      wav.writeInt16LE(
        Math.max(-32_768, Math.min(32_767, Math.round(value * 32_767))),
        44 + (frame * 2 + channel) * 2,
      );
    }
  }
  return wav;
}

async function main() {
  const manifest = {
    version: 2,
    generation: "high-fidelity-vertical-slice",
    generatedBy: "scripts/generate-audio-v2-candidates.mjs",
    sampleRate: SAMPLE_RATE,
    renderSampleRate: RENDER_RATE,
    channels: 2,
    format: "pcm-s16le-wav",
    packs: [],
    experiments: [],
  };

  for (const pack of packs) {
    const directory = path.join(ROOT, "v2", "packs", pack.id);
    await mkdir(directory, { recursive: true });
    const renderedCues = [];
    for (const cue of cues) {
      const output = createStereo(cue.duration);
      const random = seeded(`${pack.id}/${cue.id}/v2`);
      pack.render(cue, output, random);
      const audio = finalize(
        output,
        cue.id === "entitlement-unlock" ? 0.84 : 0.88,
      );
      const filename = `${cue.id}.wav`;
      await writeFile(
        path.join(directory, filename),
        encodeWav(audio, seeded(`${pack.id}/${cue.id}/dither/v2`)),
      );
      renderedCues.push({
        id: cue.id,
        label: cue.label,
        category: cue.category,
        description: cue.description,
        file: `./v2/packs/${pack.id}/${filename}`,
        durationSeconds: Number((audio.left.length / SAMPLE_RATE).toFixed(3)),
        peak: Number(audio.peak.toFixed(4)),
        rms: Number(audio.rms.toFixed(4)),
        stereoDifferenceRms: Number(audio.stereoDifferenceRms.toFixed(4)),
        dcOffset: Number(audio.dcOffset.toFixed(6)),
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

  const fusionCue = cues.find((cue) => cue.id === "entitlement-unlock");
  const fusionDirectory = path.join(ROOT, "v2", "experiments", fusion.id);
  await mkdir(fusionDirectory, { recursive: true });
  const fusionOutput = createStereo(fusionCue.duration);
  renderSignatureUnlock(
    fusionOutput,
    seeded(`${fusion.id}/${fusionCue.id}/v2`),
  );
  const fusionAudio = finalize(fusionOutput, 0.84);
  const fusionFilename = `${fusionCue.id}.wav`;
  await writeFile(
    path.join(fusionDirectory, fusionFilename),
    encodeWav(
      fusionAudio,
      seeded(`${fusion.id}/${fusionCue.id}/dither/v2`),
    ),
  );
  manifest.experiments.push({
    ...fusion,
    cue: {
      id: fusionCue.id,
      label: fusionCue.label,
      category: fusionCue.category,
      description: fusionCue.description,
      file: `./v2/experiments/${fusion.id}/${fusionFilename}`,
      durationSeconds: Number(
        (fusionAudio.left.length / SAMPLE_RATE).toFixed(3),
      ),
      peak: Number(fusionAudio.peak.toFixed(4)),
      rms: Number(fusionAudio.rms.toFixed(4)),
      stereoDifferenceRms: Number(
        fusionAudio.stereoDifferenceRms.toFixed(4),
      ),
      dcOffset: Number(fusionAudio.dcOffset.toFixed(6)),
    },
  });

  const json = `${JSON.stringify(manifest, null, 2)}\n`;
  await writeFile(path.join(ROOT, "manifest-v2.json"), json);
  await writeFile(
    path.join(ROOT, "manifest-v2.js"),
    `window.SOUND_LAB_V2_MANIFEST = ${JSON.stringify(manifest)};\n`,
  );
  const bytes = manifest.packs.reduce(
    (total, pack) =>
      total +
      pack.cues.reduce(
        (packTotal, cue) =>
          packTotal +
          Math.round(cue.durationSeconds * SAMPLE_RATE * 2 * 2 + 44),
        0,
    ),
    0,
  ) + manifest.experiments.reduce(
    (total, experiment) =>
      total +
      Math.round(
        experiment.cue.durationSeconds * SAMPLE_RATE * 2 * 2 + 44,
      ),
    0,
  );
  console.log(
    `Generated ${manifest.packs.length * cues.length} V2 cues and ` +
      `${manifest.experiments.length} fusion candidate ` +
      `(${(bytes / 1_048_576).toFixed(2)} MiB) in ${ROOT}`,
  );
}

await main();
