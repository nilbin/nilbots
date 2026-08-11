#!/usr/bin/env node

/**
 * Deterministic space / ambient Arc Relay SFX candidates.
 *
 * These lossless review masters are built entirely from seeded synthesis. The
 * approved runtime package and Neon Protocol soundtrack are deliberately untouched.
 */
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
    id: "attack-windup",
    label: "Attack wind-up",
    category: "ATTACK STATE",
    status: "proposed",
    duration: 0.58,
    description: "A rising field establishes public intent and is designed to flow directly into any outcome.",
    mapping: "pendingStrikes entry appears in authoritative Arc Relay mode state",
  },
  {
    id: "projectile",
    label: "Beam resolve",
    category: "COMBAT",
    status: "mapped",
    duration: 0.72,
    description: "A focused field ignites, holds as a spectral line, then leaves a short ionized tail.",
    mapping: "Shot / attack event (legacy runtime cue ID: projectile)",
  },
  {
    id: "attack-fizzle",
    label: "Fizzle / interrupt / miss",
    category: "ATTACK STATE",
    status: "proposed",
    duration: 0.92,
    description: "The charged field loses coherence at resolution and disperses without a beam.",
    mapping: "pending strike reaches resolveAtTick without a matching attack traversal",
  },
  {
    id: "attack-cancel",
    label: "Attack cancelled",
    category: "ATTACK STATE",
    status: "proposed",
    duration: 0.64,
    description: "A deliberate, compact de-energize closes the wind-up before resolution.",
    mapping: "pending strike disappears before resolveAtTick (wind-up abandonment)",
  },
  {
    id: "impact",
    label: "Armor impact",
    category: "COMBAT",
    status: "mapped",
    duration: 0.82,
    description: "A pressure bloom confirms damage without returning to clanks or workshop materials.",
    mapping: "Damage event",
  },
  {
    id: "destroyed",
    label: "Bot destroyed",
    category: "COMBAT",
    status: "mapped",
    duration: 2.2,
    description: "Energy disperses and falls out of orbit—a spacious systems failure, not an explosion.",
    mapping: "Destroyed / disqualified event",
  },
  {
    id: "level-up",
    label: "Level up",
    category: "PROGRESSION",
    status: "proposed",
    duration: 1.16,
    description: "A compact bot-local field rises and locks after earned veterancy, clear through a destruction tail without becoming a victory sting.",
    mapping: "leveled-up Arc Relay fact (currently silent; coalesce stacked facts per actor and tick)",
  },
  {
    id: "arc-birth",
    label: "Core born",
    category: "ARC CORE",
    status: "mapped",
    duration: 1.75,
    description: "The Well opens into a rising spectral field as a neutral Core enters play.",
    mapping: "core-born fact",
  },
  {
    id: "arc-pickup",
    label: "Core pickup",
    category: "ARC CORE",
    status: "mapped",
    duration: 0.86,
    description: "A small ascending halo marks clean possession without becoming a UI confirmation beep.",
    mapping: "core-picked-up fact without an ownership reversal",
  },
  {
    id: "arc-steal",
    label: "Core stolen",
    category: "ARC CORE",
    status: "mapped",
    duration: 1.22,
    description: "Two spectral paths cross as possession reverses between teams.",
    mapping: "core-picked-up fact after opposing ownership",
  },
  {
    id: "arc-drop",
    label: "Core dropped",
    category: "ARC CORE",
    status: "mapped",
    duration: 1.02,
    description: "The possession field descends and remains deliberately unresolved.",
    mapping: "core-dropped fact",
  },
  {
    id: "arc-bank",
    label: "Core banked",
    category: "ARC CORE",
    status: "mapped",
    duration: 1.72,
    description: "An inward pull resolves into a broad reactor bloom: meaningful, but below a Pulse.",
    mapping: "core-banked fact",
  },
  {
    id: "arc-pulse",
    label: "Reactor Pulse",
    category: "ARC CORE",
    status: "mapped",
    duration: 2.9,
    description: "The full arena-scale wave: depth, radiant motion, and a long celestial release.",
    mapping: "pulse fact",
  },
  {
    id: "arc-handoff",
    label: "Ally handoff",
    category: "PROPOSED",
    status: "proposed",
    duration: 1.02,
    description: "Two soft orbits exchange sides, making authored relay play audible without fanfare.",
    mapping: "core-handed-off fact (currently silent)",
  },
  {
    id: "deflect",
    label: "Beam deflected",
    category: "PROPOSED",
    status: "proposed",
    duration: 0.82,
    description: "A fast spectral bend distinguishes a successful guard from ordinary damage.",
    mapping: "projectile-deflected event (currently visual-only)",
  },
  {
    id: "arrival",
    label: "Unit arrival",
    category: "PROPOSED",
    status: "proposed",
    duration: 1.5,
    description: "A quiet field condenses into focus for respawn or fabrication.",
    mapping: "life-spawned / respawned / fabricated event (currently silent)",
  },
];

const packs = [
  {
    id: "halo-drift",
    number: "A",
    label: "Halo Drift",
    kicker: "LUMINOUS · WEIGHTLESS · WIDE",
    accent: "#78e8ff",
    description:
      "Air, slow orbital motion, and translucent spectral wakes. The arena feels suspended inside a vast luminous field.",
    thesis: "Best if Arc Relay should feel graceful, futuristic, and almost zero-gravity.",
    watchFor: ["high-air movement", "soft Doppler contours", "near-silent stereo diffusion"],
    axes: { luminous: 96, depth: 62, motion: 88, diffusion: 98 },
    root: 196,
    subRoot: 49,
    ratios: [1, 1.498, 2.377],
    partials: [[1, 1], [2.006, 0.2], [3.711, 0.07]],
    airLow: 3_400,
    airHigh: 19_500,
    darkCutoff: 20_800,
    reverbMix: 0.34,
    reverbDecay: 1.5,
    delay: 0.173,
    identity: "halo",
  },
  {
    id: "deep-array",
    number: "B",
    label: "Deep Array",
    kicker: "DARK · DISTANT · IMMENSE",
    accent: "#9c8cff",
    description:
      "Sub pressure, eclipsed harmonics, and signals arriving across a controlled void. Darker, but never dusty or degraded.",
    thesis: "Best if the code should feel like it is operating an unknowably large deep-space system.",
    watchFor: ["low orbital pressure", "distant signal modulation", "negative space between events"],
    axes: { luminous: 38, depth: 100, motion: 64, diffusion: 86 },
    root: 82.41,
    subRoot: 41.2,
    ratios: [1, 1.414, 2.72],
    partials: [[1, 1], [1.997, 0.16], [2.718, 0.08]],
    airLow: 900,
    airHigh: 10_500,
    darkCutoff: 14_800,
    reverbMix: 0.32,
    reverbDecay: 1.78,
    delay: 0.223,
    identity: "deep",
  },
  {
    id: "solar-bloom",
    number: "C",
    label: "Solar Bloom",
    kicker: "WARM · IRIDESCENT · CELESTIAL",
    accent: "#ffc879",
    description:
      "Radiant harmonic clouds and warm plasma motion. More emotional than Halo Drift, without turning into a second soundtrack.",
    thesis: "Best if objective play should feel optimistic, wondrous, and charged by a living star.",
    watchFor: ["warm harmonic lift", "iridescent upper bloom", "bright objective hierarchy"],
    axes: { luminous: 92, depth: 76, motion: 72, diffusion: 90 },
    root: 130.81,
    subRoot: 43.65,
    ratios: [1, 1.335, 1.782, 2.378],
    partials: [[1, 1], [2.002, 0.27], [3.006, 0.11], [5.009, 0.035]],
    airLow: 2_100,
    airHigh: 17_000,
    darkCutoff: 19_500,
    reverbMix: 0.31,
    reverbDecay: 1.58,
    delay: 0.191,
    identity: "solar",
  },
];

function createStereo(duration) {
  const length = Math.ceil(duration * RENDER_RATE);
  return {
    left: new Float64Array(length),
    right: new Float64Array(length),
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

function smoothstep(value) {
  const x = Math.max(0, Math.min(1, value));
  return x * x * (3 - 2 * x);
}

function envelope(time, duration, attack, release, decay = 0) {
  const head =
    attack <= 0 ? 1 : Math.sin(Math.min(1, time / attack) * Math.PI * 0.5);
  const tail =
    release <= 0
      ? 1
      : Math.sin(
          Math.min(1, Math.max(0, (duration - time) / release)) *
            Math.PI *
            0.5,
        );
  return head * tail * (decay > 0 ? Math.exp(-time / decay) : 1);
}

function addTone(
  output,
  {
    start = 0,
    duration,
    from,
    to = from,
    amplitude = 0.1,
    attack = 0.01,
    release = 0.2,
    decay = 0,
    pan = 0,
    panTo = pan,
    orbitRate = 0,
    orbitDepth = 0,
    partials = [[1, 1]],
    vibratoRate = 0,
    vibratoDepth = 0,
    amRate = 0,
    amDepth = 0,
    detuneCents = 0,
    phase = 0,
    pitchCurve = 1,
  },
) {
  const first = Math.max(0, Math.floor(start * RENDER_RATE));
  const count = Math.min(
    Math.ceil(duration * RENDER_RATE),
    Math.max(0, output.left.length - first),
  );
  const detune = 2 ** (detuneCents / 1_200);
  const ratio = from > 0 && to > 0 ? to / from : 1;
  let oscillatorPhase = phase;
  for (let index = 0; index < count; index++) {
    const time = index / RENDER_RATE;
    const progress = Math.min(1, time / Math.max(duration, 0.0001));
    const shapedProgress = progress ** pitchCurve;
    let frequency = from * ratio ** shapedProgress * detune;
    if (vibratoRate > 0) {
      frequency *=
        1 + Math.sin(Math.PI * 2 * vibratoRate * time + phase) * vibratoDepth;
    }
    oscillatorPhase += (Math.PI * 2 * frequency) / RENDER_RATE;
    let sample = 0;
    for (const [partial, gain] of partials) {
      sample += Math.sin(oscillatorPhase * partial + phase * partial) * gain;
    }
    const motion =
      pan +
      (panTo - pan) * smoothstep(progress) +
      Math.sin(Math.PI * 2 * orbitRate * time + phase) * orbitDepth;
    const [leftGain, rightGain] = panGains(motion);
    const amplitudeModulation =
      amRate > 0
        ? 1 - amDepth * 0.5 +
          Math.sin(Math.PI * 2 * amRate * time + phase) * amDepth * 0.5
        : 1;
    const gain =
      amplitude *
      envelope(time, duration, attack, release, decay) *
      amplitudeModulation;
    output.left[first + index] += sample * gain * leftGain;
    output.right[first + index] += sample * gain * rightGain;
  }
}

function addUnison(output, options) {
  const width = options.width ?? 0.18;
  const detunes = options.detunes ?? [-6.5, 0, 5.2];
  const weights = [0.46, 0.72, 0.43];
  detunes.forEach((detune, index) => {
    addTone(output, {
      ...options,
      amplitude:
        options.amplitude *
        (weights[index] ?? 0.42) /
        Math.sqrt(detunes.length),
      detuneCents: (options.detuneCents ?? 0) + detune,
      pan: (options.pan ?? 0) + (index - 1) * width,
      panTo: (options.panTo ?? options.pan ?? 0) - (index - 1) * width * 0.45,
      phase: (options.phase ?? 0) + index * 1.731,
    });
  });
}

function addNoiseSweep(
  output,
  random,
  {
    start = 0,
    duration,
    amplitude = 0.1,
    attack = 0.04,
    release = 0.3,
    decay = 0,
    lowpassFrom = 12_000,
    lowpassTo = lowpassFrom,
    highpassFrom = 500,
    highpassTo = highpassFrom,
    pan = 0,
    panTo = pan,
    stereo = 0.8,
  },
) {
  const first = Math.max(0, Math.floor(start * RENDER_RATE));
  const count = Math.min(
    Math.ceil(duration * RENDER_RATE),
    Math.max(0, output.left.length - first),
  );
  let lowL = 0;
  let lowR = 0;
  let highReferenceL = 0;
  let highReferenceR = 0;
  for (let index = 0; index < count; index++) {
    const time = index / RENDER_RATE;
    const progress = Math.min(1, time / Math.max(duration, 0.0001));
    const lowpass =
      lowpassFrom * (Math.max(20, lowpassTo) / Math.max(20, lowpassFrom)) ** progress;
    const highpass =
      highpassFrom *
      (Math.max(20, highpassTo) / Math.max(20, highpassFrom)) ** progress;
    const lowCoefficient =
      1 - Math.exp((-Math.PI * 2 * Math.min(32_000, lowpass)) / RENDER_RATE);
    const highCoefficient =
      1 - Math.exp((-Math.PI * 2 * Math.min(24_000, highpass)) / RENDER_RATE);
    const shared = random() * 2 - 1;
    const sourceL = shared * (1 - stereo) + (random() * 2 - 1) * stereo;
    const sourceR = shared * (1 - stereo) + (random() * 2 - 1) * stereo;
    lowL += (sourceL - lowL) * lowCoefficient;
    lowR += (sourceR - lowR) * lowCoefficient;
    highReferenceL += (lowL - highReferenceL) * highCoefficient;
    highReferenceR += (lowR - highReferenceR) * highCoefficient;
    const [leftGain, rightGain] = panGains(
      pan + (panTo - pan) * smoothstep(progress),
    );
    const gain = amplitude * envelope(time, duration, attack, release, decay);
    output.left[first + index] +=
      (lowL - highReferenceL) * gain * leftGain;
    output.right[first + index] +=
      (lowR - highReferenceR) * gain * rightGain;
  }
}

function addBloom(
  output,
  theme,
  {
    start = 0,
    duration,
    from,
    to = from,
    amplitude = 0.18,
    attack = 0.12,
    release = 0.6,
    panSpread = 0.72,
    ratios = theme.ratios,
    partials = theme.partials,
    brightness = 1,
  },
) {
  ratios.forEach((ratio, index) => {
    const placement =
      ratios.length === 1
        ? 0
        : -panSpread + (index * panSpread * 2) / (ratios.length - 1);
    addUnison(output, {
      start: start + index * 0.012,
      duration: Math.max(0.08, duration - index * 0.012),
      from: from * ratio,
      to: to * ratio * (1 + index * 0.002),
      amplitude:
        (amplitude * brightness ** (index / Math.max(1, ratios.length - 1))) /
        Math.sqrt(ratios.length),
      attack: attack + index * 0.012,
      release,
      pan: placement,
      panTo: -placement * 0.38,
      orbitRate: 0.11 + index * 0.037,
      orbitDepth: 0.06,
      partials,
      vibratoRate: 0.19 + index * 0.043,
      vibratoDepth: 0.0016 + index * 0.00025,
      phase: index * 0.83,
    });
  });
}

function addSubWave(
  output,
  {
    start = 0,
    duration,
    from,
    to = from,
    amplitude = 0.16,
    attack = 0.02,
    release = 0.5,
    pan = 0,
  },
) {
  addTone(output, {
    start,
    duration,
    from,
    to,
    amplitude,
    attack,
    release,
    pan,
    partials: [[1, 1], [2.003, 0.08]],
    vibratoRate: 0.13,
    vibratoDepth: 0.001,
    pitchCurve: 0.78,
  });
}

function addSpectralDust(
  output,
  random,
  theme,
  {
    start,
    duration,
    count,
    low,
    high,
    amplitude,
    rising = false,
  },
) {
  for (let index = 0; index < count; index++) {
    const grainStart = start + random() * Math.max(0, duration * 0.72);
    const grainDuration = 0.18 + random() * Math.min(0.5, duration * 0.42);
    const base = low * (high / low) ** random();
    const direction = rising ? 1.06 + random() * 0.14 : 0.82 + random() * 0.16;
    addTone(output, {
      start: grainStart,
      duration: grainDuration,
      from: base,
      to: base * direction,
      amplitude: amplitude * (0.55 + random() * 0.65),
      attack: 0.018 + random() * 0.04,
      release: grainDuration * 0.72,
      pan: random() * 1.7 - 0.85,
      panTo: random() * 1.4 - 0.7,
      partials:
        theme.identity === "solar"
          ? [[1, 1], [2.01, 0.2]]
          : [[1, 1], [1.617, 0.1]],
      vibratoRate: 0.3 + random() * 0.5,
      vibratoDepth: 0.002,
      phase: random() * Math.PI * 2,
    });
  }
}

function renderCue(theme, cue, output, random) {
  const root = theme.root;
  const duration = cue.duration;
  switch (cue.id) {
    case "attack-windup":
      addNoiseSweep(output, random, {
        duration: 0.54,
        amplitude: 0.13,
        attack: 0.29,
        release: 0.12,
        lowpassFrom: theme.airLow * 0.48,
        lowpassTo: theme.airHigh * 0.82,
        highpassFrom: 170,
        highpassTo: theme.airLow * 0.48,
        pan: -0.24,
        panTo: 0.24,
      });
      addBloom(output, theme, {
        start: 0.015,
        duration: 0.52,
        from: root * 0.72,
        to: root * 1.54,
        amplitude: 0.21,
        attack: 0.24,
        release: 0.11,
        panSpread: 0.44,
        ratios: theme.ratios.slice(0, 2),
      });
      addTone(output, {
        start: 0.09,
        duration: 0.44,
        from: root * 3.25,
        to: root * 3.72,
        amplitude: 0.09,
        attack: 0.2,
        release: 0.09,
        pan: -0.12,
        panTo: 0.12,
        partials: [[1, 1], [1.501, 0.1]],
        amRate: 8.4,
        amDepth: 0.12,
      });
      break;

    case "projectile":
      addNoiseSweep(output, random, {
        duration: 0.58,
        amplitude: 0.19,
        attack: 0.004,
        release: 0.43,
        decay: 0.3,
        lowpassFrom: theme.airHigh,
        lowpassTo: theme.airHigh * 0.48,
        highpassFrom: theme.airLow * 0.44,
        highpassTo: theme.airLow * 0.62,
        pan: -0.08,
        panTo: 0.08,
      });
      addUnison(output, {
        start: 0.004,
        duration: 0.53,
        from: root * 5.7,
        to: root * 5.08,
        amplitude: 0.24,
        attack: 0.003,
        release: 0.34,
        decay: 0.28,
        pan: -0.12,
        panTo: 0.12,
        orbitRate: 0.24,
        orbitDepth: 0.035,
        partials: theme.partials,
        amRate: theme.identity === "deep" ? 11.7 : 16.4,
        amDepth: 0.13,
        pitchCurve: 0.88,
      });
      addTone(output, {
        start: 0.018,
        duration: 0.49,
        from: root * 9.2,
        to: root * 8.7,
        amplitude: 0.075,
        attack: 0.006,
        release: 0.38,
        pan: 0.18,
        panTo: -0.16,
        partials: [[1, 1], [1.503, 0.12]],
        amRate: 23.5,
        amDepth: 0.08,
      });
      addSubWave(output, {
        start: 0.006,
        duration: 0.48,
        from: theme.subRoot * 1.65,
        to: theme.subRoot * 1.32,
        amplitude: theme.identity === "deep" ? 0.18 : 0.085,
        attack: 0.004,
        release: 0.36,
      });
      break;

    case "attack-fizzle":
      addNoiseSweep(output, random, {
        duration: 0.83,
        amplitude: 0.17,
        attack: 0.018,
        release: 0.66,
        decay: 0.36,
        lowpassFrom: theme.airHigh * 0.76,
        lowpassTo: theme.airLow * 0.34,
        highpassFrom: theme.airLow * 0.4,
        highpassTo: 120,
        pan: -0.42,
        panTo: 0.46,
      });
      addBloom(output, theme, {
        start: 0.01,
        duration: 0.8,
        from: root * 1.55,
        to: root * 0.62,
        amplitude: 0.19,
        attack: 0.012,
        release: 0.62,
        panSpread: 0.74,
        ratios: [1, 1.287, 1.861],
      });
      addSpectralDust(output, random, theme, {
        start: 0.08,
        duration: 0.62,
        count: 6,
        low: root * 2.1,
        high: Math.min(6_800, root * 9),
        amplitude: 0.024,
      });
      break;

    case "attack-cancel":
      addNoiseSweep(output, random, {
        duration: 0.54,
        amplitude: 0.12,
        attack: 0.004,
        release: 0.43,
        decay: 0.2,
        lowpassFrom: theme.airLow * 1.2,
        lowpassTo: theme.airLow * 0.34,
        highpassFrom: 180,
        highpassTo: 80,
        pan: 0.12,
        panTo: -0.1,
      });
      addUnison(output, {
        duration: 0.53,
        from: root * 1.42,
        to: root * 0.72,
        amplitude: 0.18,
        attack: 0.004,
        release: 0.42,
        decay: 0.21,
        pan: 0.08,
        panTo: -0.08,
        partials: [[1, 1], [1.501, 0.11]],
        width: 0.1,
        pitchCurve: 0.62,
      });
      addSubWave(output, {
        start: 0.025,
        duration: 0.48,
        from: theme.subRoot * 1.18,
        to: theme.subRoot * 0.72,
        amplitude: theme.identity === "deep" ? 0.17 : 0.09,
        attack: 0.008,
        release: 0.38,
      });
      break;

    case "impact":
      addNoiseSweep(output, random, {
        duration: 0.69,
        amplitude: 0.24,
        attack: 0.005,
        release: 0.58,
        decay: 0.18,
        lowpassFrom: theme.airHigh * 0.72,
        lowpassTo: theme.airLow * 0.72,
        highpassFrom: 240,
        highpassTo: theme.airLow * 0.32,
        stereo: 0.9,
      });
      addUnison(output, {
        duration: 0.68,
        from: root * 4.1,
        to: root * 0.86,
        amplitude: 0.24,
        attack: 0.004,
        release: 0.58,
        decay: 0.16,
        partials: [[1, 1], [1.414, 0.18], [2.73, 0.07]],
        width: 0.12,
        pitchCurve: 0.55,
      });
      addSubWave(output, {
        duration: 0.73,
        from: theme.subRoot * 2.1,
        to: theme.subRoot * 0.78,
        amplitude: theme.identity === "deep" ? 0.25 : 0.16,
        attack: 0.006,
        release: 0.61,
      });
      break;

    case "destroyed":
      addNoiseSweep(output, random, {
        duration: 1.92,
        amplitude: 0.22,
        attack: 0.015,
        release: 1.35,
        decay: 0.72,
        lowpassFrom: theme.airHigh * 0.82,
        lowpassTo: theme.airLow * 0.44,
        highpassFrom: 180,
        highpassTo: theme.airLow * 0.48,
        pan: -0.38,
        panTo: 0.48,
      });
      addBloom(output, theme, {
        duration: 1.82,
        from: root * 2.8,
        to: root * 0.54,
        amplitude: 0.28,
        attack: 0.018,
        release: 1.18,
        panSpread: 0.8,
        ratios: [1, 1.273, 1.917],
      });
      addSubWave(output, {
        duration: 1.78,
        from: theme.subRoot * 2.6,
        to: theme.subRoot * 0.58,
        amplitude: theme.identity === "deep" ? 0.31 : 0.2,
        attack: 0.012,
        release: 1.28,
      });
      addSpectralDust(output, random, theme, {
        start: 0.16,
        duration: 1.72,
        count: 11,
        low: root * 2.4,
        high: Math.min(8_200, root * 13),
        amplitude: 0.035,
      });
      break;

    case "level-up":
      addNoiseSweep(output, random, {
        start: 0.015,
        duration: 1.02,
        amplitude: 0.105,
        attack: 0.075,
        release: 0.42,
        lowpassFrom: theme.airLow * 0.42,
        lowpassTo: theme.airHigh * 0.58,
        highpassFrom: 180,
        highpassTo: theme.airLow * 0.48,
        pan: -0.12,
        panTo: 0.12,
        stereo: 0.72,
      });
      addUnison(output, {
        start: 0.02,
        duration: 0.96,
        from: root * 0.78,
        to: root * 1.54,
        amplitude: 0.19,
        attack: 0.055,
        release: 0.38,
        pan: -0.08,
        panTo: 0.08,
        partials: theme.partials,
        width: 0.12,
        pitchCurve: 0.66,
      });
      addSubWave(output, {
        start: 0.025,
        duration: 0.88,
        from: theme.subRoot * 0.82,
        to: theme.subRoot * 1.34,
        amplitude: theme.identity === "deep" ? 0.15 : 0.085,
        attack: 0.05,
        release: 0.36,
      });
      addTone(output, {
        start: 0.47,
        duration: 0.57,
        from: root * 1.46,
        to: root * 1.5,
        amplitude: 0.08,
        attack: 0.08,
        release: 0.34,
        pan: 0,
        partials: [[1, 1], [2.003, 0.13]],
        amRate: 4.2,
        amDepth: 0.08,
      });
      break;

    case "arc-birth":
      addNoiseSweep(output, random, {
        start: 0.04,
        duration: 1.56,
        amplitude: 0.18,
        attack: 0.72,
        release: 0.66,
        lowpassFrom: theme.airLow * 0.55,
        lowpassTo: theme.airHigh,
        highpassFrom: 140,
        highpassTo: theme.airLow * 0.42,
        pan: -0.55,
        panTo: 0.55,
      });
      addBloom(output, theme, {
        start: 0.12,
        duration: 1.48,
        from: root * 0.62,
        to: root * 1.34,
        amplitude: 0.3,
        attack: 0.62,
        release: 0.7,
        panSpread: 0.88,
      });
      addSubWave(output, {
        start: 0.16,
        duration: 1.39,
        from: theme.subRoot * 0.72,
        to: theme.subRoot * 1.42,
        amplitude: theme.identity === "deep" ? 0.23 : 0.13,
        attack: 0.58,
        release: 0.62,
      });
      addSpectralDust(output, random, theme, {
        start: 0.62,
        duration: 0.9,
        count: 7,
        low: root * 3,
        high: Math.min(10_500, root * 16),
        amplitude: 0.027,
        rising: true,
      });
      break;

    case "arc-pickup":
      addNoiseSweep(output, random, {
        duration: 0.74,
        amplitude: 0.11,
        attack: 0.035,
        release: 0.52,
        lowpassFrom: theme.airLow,
        lowpassTo: theme.airHigh * 0.68,
        highpassFrom: theme.airLow * 0.32,
        highpassTo: theme.airLow * 0.58,
        pan: -0.26,
        panTo: 0.28,
      });
      addBloom(output, theme, {
        start: 0.035,
        duration: 0.73,
        from: root * 1.16,
        to: root * 1.68,
        amplitude: 0.25,
        attack: 0.045,
        release: 0.5,
        panSpread: 0.5,
        ratios: theme.ratios.slice(0, 2),
      });
      break;

    case "arc-steal":
      addNoiseSweep(output, random, {
        duration: 1.08,
        amplitude: 0.16,
        attack: 0.04,
        release: 0.72,
        lowpassFrom: theme.airHigh * 0.7,
        lowpassTo: theme.airLow * 0.8,
        highpassFrom: theme.airLow * 0.36,
        highpassTo: theme.airLow * 0.22,
        pan: -0.72,
        panTo: 0.72,
      });
      addUnison(output, {
        start: 0.03,
        duration: 1.02,
        from: root * 2.9,
        to: root * 1.12,
        amplitude: 0.2,
        attack: 0.035,
        release: 0.66,
        pan: -0.72,
        panTo: 0.68,
        partials: theme.partials,
        pitchCurve: 0.72,
      });
      addUnison(output, {
        start: 0.07,
        duration: 0.98,
        from: root * 1.03,
        to: root * 2.14,
        amplitude: 0.17,
        attack: 0.08,
        release: 0.6,
        pan: 0.72,
        panTo: -0.62,
        partials: [[1, 1], [1.414, 0.14], [2.01, 0.07]],
      });
      addSubWave(output, {
        start: 0.08,
        duration: 0.96,
        from: theme.subRoot * 1.55,
        to: theme.subRoot * 0.86,
        amplitude: theme.identity === "deep" ? 0.2 : 0.11,
        attack: 0.07,
        release: 0.68,
      });
      break;

    case "arc-drop":
      addNoiseSweep(output, random, {
        duration: 0.9,
        amplitude: 0.15,
        attack: 0.03,
        release: 0.66,
        lowpassFrom: theme.airHigh * 0.66,
        lowpassTo: theme.airLow * 0.42,
        highpassFrom: theme.airLow * 0.32,
        highpassTo: 130,
        pan: 0.22,
        panTo: -0.18,
      });
      addBloom(output, theme, {
        start: 0.02,
        duration: 0.87,
        from: root * 1.92,
        to: root * 0.69,
        amplitude: 0.24,
        attack: 0.03,
        release: 0.62,
        panSpread: 0.58,
        ratios: theme.ratios.slice(0, 2),
      });
      addSubWave(output, {
        start: 0.06,
        duration: 0.82,
        from: theme.subRoot * 1.5,
        to: theme.subRoot * 0.64,
        amplitude: theme.identity === "deep" ? 0.2 : 0.12,
        attack: 0.04,
        release: 0.58,
      });
      break;

    case "arc-bank":
      addNoiseSweep(output, random, {
        duration: 1.54,
        amplitude: 0.17,
        attack: 0.26,
        release: 0.82,
        lowpassFrom: theme.airLow * 0.72,
        lowpassTo: theme.airHigh * 0.8,
        highpassFrom: 160,
        highpassTo: theme.airLow * 0.4,
        pan: 0.66,
        panTo: 0,
      });
      addUnison(output, {
        start: 0.03,
        duration: 0.91,
        from: root * 2.7,
        to: root * 0.94,
        amplitude: 0.16,
        attack: 0.08,
        release: 0.48,
        pan: 0.72,
        panTo: 0,
        partials: theme.partials,
      });
      addBloom(output, theme, {
        start: 0.42,
        duration: 1.16,
        from: root * 0.82,
        to: root * 1.08,
        amplitude: 0.31,
        attack: 0.22,
        release: 0.7,
        panSpread: 0.86,
      });
      addSubWave(output, {
        start: 0.3,
        duration: 1.2,
        from: theme.subRoot * 0.72,
        to: theme.subRoot * 1.18,
        amplitude: theme.identity === "deep" ? 0.24 : 0.14,
        attack: 0.24,
        release: 0.7,
      });
      break;

    case "arc-pulse":
      addNoiseSweep(output, random, {
        duration: 2.62,
        amplitude: 0.25,
        attack: 0.13,
        release: 1.65,
        lowpassFrom: theme.airLow * 0.55,
        lowpassTo: theme.airHigh,
        highpassFrom: 90,
        highpassTo: theme.airLow * 0.5,
        pan: -0.76,
        panTo: 0.76,
        stereo: 0.95,
      });
      addSubWave(output, {
        duration: 2.46,
        from: theme.subRoot * 0.68,
        to: theme.subRoot * 1.58,
        amplitude: theme.identity === "deep" ? 0.38 : 0.27,
        attack: 0.1,
        release: 1.45,
      });
      addBloom(output, theme, {
        start: 0.08,
        duration: 2.5,
        from: root * 0.56,
        to: root * 1.42,
        amplitude: 0.41,
        attack: 0.34,
        release: 1.48,
        panSpread: 0.94,
        brightness: theme.identity === "solar" ? 1.12 : 0.94,
      });
      addSpectralDust(output, random, theme, {
        start: 0.48,
        duration: 1.82,
        count: 16,
        low: root * 2.4,
        high: Math.min(12_500, root * 18),
        amplitude: 0.034,
        rising: true,
      });
      break;

    case "arc-handoff":
      addNoiseSweep(output, random, {
        duration: 0.9,
        amplitude: 0.11,
        attack: 0.07,
        release: 0.6,
        lowpassFrom: theme.airLow,
        lowpassTo: theme.airHigh * 0.66,
        highpassFrom: theme.airLow * 0.3,
        highpassTo: theme.airLow * 0.44,
        pan: -0.65,
        panTo: 0.64,
      });
      addUnison(output, {
        start: 0.025,
        duration: 0.67,
        from: root * 1.12,
        to: root * 1.58,
        amplitude: 0.19,
        attack: 0.06,
        release: 0.43,
        pan: -0.72,
        panTo: 0.18,
        partials: theme.partials,
      });
      addUnison(output, {
        start: 0.25,
        duration: 0.65,
        from: root * 1.5,
        to: root * 1.97,
        amplitude: 0.17,
        attack: 0.07,
        release: 0.44,
        pan: 0.2,
        panTo: 0.72,
        partials: theme.partials,
      });
      break;

    case "deflect":
      addNoiseSweep(output, random, {
        duration: 0.7,
        amplitude: 0.17,
        attack: 0.006,
        release: 0.5,
        lowpassFrom: theme.airLow * 0.9,
        lowpassTo: theme.airHigh,
        highpassFrom: theme.airLow * 0.44,
        highpassTo: theme.airLow * 0.84,
        pan: 0.52,
        panTo: -0.56,
      });
      addUnison(output, {
        start: 0.008,
        duration: 0.62,
        from: root * 2.2,
        to: root * 7.8,
        amplitude: 0.2,
        attack: 0.007,
        release: 0.46,
        pan: 0.64,
        panTo: -0.52,
        orbitRate: 1.1,
        orbitDepth: 0.1,
        partials: [[1, 1], [1.503, 0.15], [2.01, 0.05]],
        pitchCurve: 0.58,
      });
      break;

    case "arrival":
      addNoiseSweep(output, random, {
        start: 0.04,
        duration: 1.3,
        amplitude: 0.13,
        attack: 0.52,
        release: 0.58,
        lowpassFrom: theme.airLow * 0.54,
        lowpassTo: theme.airHigh * 0.68,
        highpassFrom: 150,
        highpassTo: theme.airLow * 0.4,
        pan: -0.42,
        panTo: 0.42,
      });
      addBloom(output, theme, {
        start: 0.12,
        duration: 1.18,
        from: root * 0.66,
        to: root * 1.02,
        amplitude: 0.22,
        attack: 0.48,
        release: 0.54,
        panSpread: 0.64,
        ratios: theme.ratios.slice(0, 3),
      });
      addSubWave(output, {
        start: 0.14,
        duration: 1.12,
        from: theme.subRoot * 0.72,
        to: theme.subRoot,
        amplitude: theme.identity === "deep" ? 0.17 : 0.09,
        attack: 0.46,
        release: 0.5,
      });
      break;
  }

  addThemeIdentity(output, random, theme, cue, duration);
  addSpace(output, theme, cue);
}

function addThemeIdentity(output, random, theme, cue, duration) {
  const objective =
    cue.id.startsWith("arc-") && !["arc-pickup", "arc-drop", "arc-handoff"].includes(cue.id);
  if (theme.identity === "halo") {
    addTone(output, {
      start: duration * 0.08,
      duration: duration * 0.76,
      from: Math.min(6_800, theme.root * 12.4),
      to: Math.min(7_600, theme.root * 13.2),
      amplitude: objective ? 0.045 : 0.024,
      attack: duration * 0.22,
      release: duration * 0.5,
      pan: -0.82,
      panTo: 0.82,
      orbitRate: 0.17,
      orbitDepth: 0.08,
      partials: [[1, 1], [1.501, 0.11]],
      vibratoRate: 0.31,
      vibratoDepth: 0.0022,
    });
  } else if (theme.identity === "deep") {
    addTone(output, {
      start: duration * 0.06,
      duration: duration * 0.8,
      from: theme.root * 1.414,
      to: theme.root * (objective ? 1.56 : 1.31),
      amplitude: objective ? 0.13 : 0.075,
      attack: duration * 0.17,
      release: duration * 0.48,
      pan: -0.28,
      panTo: 0.32,
      orbitRate: 0.08,
      orbitDepth: 0.07,
      partials: [[1, 1], [2.718, 0.12], [4.11, 0.035]],
      vibratoRate: 0.12,
      vibratoDepth: 0.0013,
      amRate: 3.7,
      amDepth: 0.16,
    });
  } else {
    addBloom(output, theme, {
      start: duration * 0.12,
      duration: duration * 0.72,
      from: theme.root * 1.66,
      to: theme.root * (objective ? 1.93 : 1.72),
      amplitude: objective ? 0.13 : 0.07,
      attack: duration * 0.2,
      release: duration * 0.48,
      panSpread: 0.9,
      ratios: [1, 1.335, 2.006],
      partials: [[1, 1], [2.004, 0.19], [3.008, 0.06]],
      brightness: 1.08,
    });
    if (objective) {
      addSpectralDust(output, random, theme, {
        start: duration * 0.28,
        duration: duration * 0.5,
        count: cue.id === "arc-pulse" ? 8 : 4,
        low: theme.root * 5,
        high: Math.min(10_000, theme.root * 17),
        amplitude: 0.018,
        rising: true,
      });
    }
  }
}

function addCrossDelay(output, seconds, feedback, mix) {
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
    allpass(sum, Math.round(0.0063 * RENDER_RATE), 0.63),
    Math.round(0.0021 * RENDER_RATE),
    0.55,
  );
}

function addReverb(
  output,
  { mix, decay, predelay = 0.024, width = 0.94 },
) {
  const feedback = Math.max(0.5, Math.min(0.925, 0.59 + decay * 0.17));
  const leftWet = reverbChannel(
    output.left,
    [0.0437, 0.0511, 0.0593, 0.0679].map((value) =>
      Math.round(value * RENDER_RATE),
    ),
    feedback,
    0.5,
    Math.round(predelay * RENDER_RATE),
  );
  const rightWet = reverbChannel(
    output.right,
    [0.0473, 0.0547, 0.0631, 0.0719].map((value) =>
      Math.round(value * RENDER_RATE),
    ),
    feedback * 0.997,
    0.53,
    Math.round((predelay + 0.0031) * RENDER_RATE),
  );
  for (let index = 0; index < output.left.length; index++) {
    const left = leftWet[index] * width + rightWet[index] * (1 - width);
    const right = rightWet[index] * width + leftWet[index] * (1 - width);
    output.left[index] += left * mix;
    output.right[index] += right * mix;
  }
}

function addSpace(output, theme, cue) {
  const frequent = [
    "attack-windup",
    "projectile",
    "attack-fizzle",
    "attack-cancel",
    "impact",
    "level-up",
    "arc-pickup",
    "deflect",
  ].includes(cue.id);
  const peak = ["arc-bank", "arc-pulse", "arc-birth"].includes(cue.id);
  const spaceScale = frequent ? 0.68 : peak ? 1.12 : 0.9;
  addCrossDelay(
    output,
    theme.delay,
    frequent ? 0.12 : 0.22,
    (frequent ? 0.07 : 0.105) * spaceScale,
  );
  addReverb(output, {
    mix: theme.reverbMix * spaceScale,
    decay: theme.reverbDecay * (peak ? 1.08 : frequent ? 0.78 : 0.94),
    predelay: frequent ? 0.014 : 0.028,
    width: theme.identity === "deep" ? 0.9 : 0.97,
  });
}

function biquad(channel, type, frequency, q = 0.707) {
  const omega = (Math.PI * 2 * frequency) / RENDER_RATE;
  const cosine = Math.cos(omega);
  const sine = Math.sin(omega);
  const alpha = sine / (2 * q);
  let b0;
  let b1;
  let b2;
  const a0 = 1 + alpha;
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

function prepareForDownsampling(output, theme) {
  for (const channel of [output.left, output.right]) {
    biquad(channel, "highpass", theme.identity === "deep" ? 25 : 32, 0.707);
    biquad(channel, "lowpass", theme.darkCutoff, 0.66);
  }
  let envelopeFollower = 0;
  const attack = 1 - Math.exp(-1 / (0.004 * RENDER_RATE));
  const release = 1 - Math.exp(-1 / (0.14 * RENDER_RATE));
  const threshold = 0.39;
  const ratio = 2.7;
  for (let index = 0; index < output.left.length; index++) {
    output.left[index] =
      Math.tanh(output.left[index] * 0.96) / Math.tanh(0.96);
    output.right[index] =
      Math.tanh(output.right[index] * 0.96) / Math.tanh(0.96);
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
  const radius = 24;
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

function finalize(output, theme, targetPeak) {
  prepareForDownsampling(output, theme);
  const left = downsample(output.left);
  const right = downsample(output.right);
  let peak = 0;
  for (let index = 0; index < left.length; index++) {
    peak = Math.max(peak, Math.abs(left[index]), Math.abs(right[index]));
  }
  const scale = peak > 0 ? targetPeak / peak : 1;
  const fadeIn = Math.round(0.0015 * SAMPLE_RATE);
  const fadeOut = Math.round(0.052 * SAMPLE_RATE);
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
  const dataBytes = frames * 4;
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
    version: 4,
    generation: "arc-relay-space-ambient",
    generatedBy: "scripts/generate-audio-v4-candidates.mjs",
    sampleRate: SAMPLE_RATE,
    renderSampleRate: RENDER_RATE,
    channels: 2,
    format: "pcm-s16le-wav",
    reviewScope: {
      game: "Arc Relay",
      direction: "spacey / ambient",
      soundtrack: "Neon Protocol unchanged",
      runtimePack: "unchanged pending owner selection",
      mappedCueIds: cues
        .filter((cue) => cue.status === "mapped")
        .map((cue) => cue.id),
      proposedCueIds: cues
        .filter((cue) => cue.status === "proposed")
        .map((cue) => cue.id),
    },
    packs: [],
  };

  for (const pack of packs) {
    const directory = path.join(ROOT, "v4", "packs", pack.id);
    await mkdir(directory, { recursive: true });
    const renderedCues = [];
    for (const cue of cues) {
      const output = createStereo(cue.duration);
      const random = seeded(pack.id + "/" + cue.id + "/v4");
      renderCue(pack, cue, output, random);
      const targetPeak =
        cue.id === "arc-pulse"
          ? 0.88
          : cue.id === "attack-windup"
            ? 0.76
            : cue.id === "attack-fizzle"
              ? 0.74
              : cue.id === "attack-cancel"
                ? 0.7
                : cue.id === "level-up"
                  ? 0.74
                  : 0.84;
      const audio = finalize(
        output,
        pack,
        targetPeak,
      );
      const filename = cue.id + ".wav";
      await writeFile(
        path.join(directory, filename),
        encodeWav(audio, seeded(pack.id + "/" + cue.id + "/dither/v4")),
      );
      renderedCues.push({
        id: cue.id,
        label: cue.label,
        category: cue.category,
        status: cue.status,
        description: cue.description,
        mapping: cue.mapping,
        file: "./v4/packs/" + pack.id + "/" + filename,
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
      thesis: pack.thesis,
      watchFor: pack.watchFor,
      axes: pack.axes,
      cues: renderedCues,
    });
  }

  const json = JSON.stringify(manifest, null, 2) + "\n";
  await writeFile(path.join(ROOT, "manifest-v4.json"), json);
  await writeFile(
    path.join(ROOT, "manifest-v4.js"),
    "window.SOUND_LAB_V4_MANIFEST = " + JSON.stringify(manifest) + ";\n",
  );
  const bytes = manifest.packs.reduce(
    (total, pack) =>
      total +
      pack.cues.reduce(
        (packTotal, cue) =>
          packTotal + Math.round(cue.durationSeconds * SAMPLE_RATE * 4 + 44),
        0,
      ),
    0,
  );
  console.log(
    "Generated " +
      manifest.packs.length * cues.length +
      " V4 cues (" +
      (bytes / 1_048_576).toFixed(2) +
      " MiB) in " +
      ROOT,
  );
}

await main();
