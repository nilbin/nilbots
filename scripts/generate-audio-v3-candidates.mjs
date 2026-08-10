#!/usr/bin/env node

/**
 * Deterministic Arc Relay SFX theme candidates.
 *
 * The review set deliberately leaves the approved runtime pack untouched. Every sound
 * here is synthesized from seeded oscillators, noise, and physical-model primitives;
 * no sampled or third-party material enters the artifact.
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
    id: "projectile",
    label: "Projectile fired",
    category: "COMBAT",
    status: "mapped",
    duration: 0.42,
    description: "The frequent launch voice: immediate, compact, and easy to pan.",
    mapping: "Shot / attack event",
  },
  {
    id: "impact",
    label: "Armor impact",
    category: "COMBAT",
    status: "mapped",
    duration: 0.46,
    description: "Damage confirmation with a different silhouette from the launch.",
    mapping: "Damage event",
  },
  {
    id: "destroyed",
    label: "Bot destroyed",
    category: "COMBAT",
    status: "mapped",
    duration: 1.16,
    description: "A small machine fails and comes apart; weight without a film-trailer boom.",
    mapping: "Destroyed / disqualified event",
  },
  {
    id: "arc-birth",
    label: "Core born",
    category: "ARC CORE",
    status: "mapped",
    duration: 0.84,
    description: "A dangerous neutral object emerges from a Well and becomes available.",
    mapping: "core-born fact",
  },
  {
    id: "arc-pickup",
    label: "Core pickup",
    category: "ARC CORE",
    status: "mapped",
    duration: 0.44,
    description: "Clean possession acquired; positive, but smaller than a score beat.",
    mapping: "core-picked-up fact without an ownership reversal",
  },
  {
    id: "arc-steal",
    label: "Core stolen",
    category: "ARC CORE",
    status: "mapped",
    duration: 0.62,
    description: "Possession flips between teams—the sharpest non-scoring reversal.",
    mapping: "core-picked-up fact after opposing ownership",
  },
  {
    id: "arc-drop",
    label: "Core dropped",
    category: "ARC CORE",
    status: "mapped",
    duration: 0.5,
    description: "The Core becomes loose; downward and unresolved rather than catastrophic.",
    mapping: "core-dropped fact",
  },
  {
    id: "arc-bank",
    label: "Core banked",
    category: "ARC CORE",
    status: "mapped",
    duration: 0.9,
    description: "A delivery locks into the reactor and advances its visible charge.",
    mapping: "core-banked fact",
  },
  {
    id: "arc-pulse",
    label: "Reactor Pulse",
    category: "ARC CORE",
    status: "mapped",
    duration: 1.46,
    description: "The largest SFX beat: charged energy crosses the arena and hits a reactor.",
    mapping: "pulse fact",
  },
  {
    id: "arc-handoff",
    label: "Ally handoff",
    category: "PROPOSED",
    status: "proposed",
    duration: 0.48,
    description: "A two-part transfer voice makes programmed relay play audible without fanfare.",
    mapping: "core-handed-off fact (currently silent)",
  },
  {
    id: "deflect",
    label: "Projectile deflected",
    category: "PROPOSED",
    status: "proposed",
    duration: 0.42,
    description: "A bright glancing answer separates a successful guard from ordinary damage.",
    mapping: "projectile-deflected event (currently visual-only)",
  },
  {
    id: "arrival",
    label: "Unit arrival",
    category: "PROPOSED",
    status: "proposed",
    duration: 0.68,
    description: "A restrained online/clamp cue for respawn or fabrication, kept below objective beats.",
    mapping: "life-spawned / respawned / fabricated event (currently silent)",
  },
];

const packs = [
  {
    id: "servo-league",
    number: "A",
    label: "Servo League",
    kicker: "TACTILE · FAST · SPORTING",
    accent: "#ffb765",
    description:
      "Compact motors, polymer armor, ceramic pucks, and positive relay latches. It treats Arc Relay as a readable robot sport at miniature scale.",
    thesis: "Best if the match should feel physical, competitive, and immediately legible.",
    watchFor: ["short combat tails", "possession latch language", "small-machine scale"],
    axes: { physical: 92, coded: 42, sport: 94, atmospheric: 28 },
    render: renderServoLeague,
  },
  {
    id: "signal-grid",
    number: "B",
    label: "Signal Grid",
    kicker: "CODED · CLEAN · INTELLIGENT",
    accent: "#62e6ff",
    description:
      "Phase-coded packets, restrained FM motion, and glass-alloy transients. It foregrounds that every body is executing a submitted mind.",
    thesis: "Best if programming and machine intelligence should be audible in the product identity.",
    watchFor: ["coded micro-rhythms", "clean pitch contours", "wider objective cues"],
    axes: { physical: 34, coded: 98, sport: 68, atmospheric: 72 },
    render: renderSignalGrid,
  },
  {
    id: "field-resonance",
    number: "C",
    label: "Field Resonance",
    kicker: "TEXTURED · GROUNDED · ADAPTIVE",
    accent: "#a7dd77",
    description:
      "Spring steel, composite knocks, hollow vessels, and charged air. It lets the robots belong in woodland, forge, vault, or lab arenas without changing packs per map.",
    thesis: "Best if the arena world and its materials should have more character than the interface.",
    watchFor: ["physical-model plucks", "organic transients", "map-friendly restraint"],
    axes: { physical: 84, coded: 26, sport: 54, atmospheric: 88 },
    render: renderFieldResonance,
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

function envelope(time, duration, attack = 0.002, release = 0.08, decay = 0) {
  const head = attack <= 0 ? 1 : Math.sin(Math.min(1, time / attack) * Math.PI * 0.5);
  const tail =
    release <= 0
      ? 1
      : Math.sin(
          Math.min(1, Math.max(0, (duration - time) / release)) * Math.PI * 0.5,
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
    const time = index / RENDER_RATE;
    const progress = Math.min(1, time / Math.max(duration, 0.0001));
    const frequency = from * ratio ** progress;
    phase += (Math.PI * 2 * frequency) / RENDER_RATE;
    modulatorPhase +=
      (Math.PI * 2 * frequency * Math.max(0, fmRatio)) / RENDER_RATE;
    const modulation =
      fmRatio > 0
        ? Math.sin(modulatorPhase) *
          fmIndex *
          (fmDecay > 0 ? Math.exp(-time / fmDecay) : 1)
        : 0;
    let value = 0;
    for (const [partial, level] of partials)
      value += Math.sin(phase * partial + modulation) * level;
    const [leftGain, rightGain] = panGains(pan + (panTo - pan) * progress);
    const gain = amplitude * envelope(time, duration, attack, release, decay);
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
    amplitude = 0.12,
    attack = 0,
    release = 0.06,
    decay = 0.1,
    lowpass = 16_000,
    highpass = 0,
    pan = 0,
    panTo = pan,
    stereo = 0.4,
    roughness = 1,
  },
) {
  const first = Math.floor(start * RENDER_RATE);
  const count = Math.min(
    Math.ceil(duration * RENDER_RATE),
    Math.max(0, output.left.length - first),
  );
  const lowCoefficient = 1 - Math.exp((-Math.PI * 2 * lowpass) / RENDER_RATE);
  const highCoefficient =
    highpass > 0 ? 1 - Math.exp((-Math.PI * 2 * highpass) / RENDER_RATE) : 1;
  let lowLeft = 0;
  let lowRight = 0;
  let highReferenceLeft = 0;
  let highReferenceRight = 0;
  let heldLeft = 0;
  let heldRight = 0;
  const hold = Math.max(1, Math.round(roughness));
  for (let index = 0; index < count; index++) {
    if (index % hold === 0) {
      const shared = random() * 2 - 1;
      heldLeft = shared * (1 - stereo) + (random() * 2 - 1) * stereo;
      heldRight = shared * (1 - stereo) + (random() * 2 - 1) * stereo;
    }
    lowLeft += (heldLeft - lowLeft) * lowCoefficient;
    lowRight += (heldRight - lowRight) * lowCoefficient;
    highReferenceLeft += (lowLeft - highReferenceLeft) * highCoefficient;
    highReferenceRight += (lowRight - highReferenceRight) * highCoefficient;
    const time = index / RENDER_RATE;
    const progress = Math.min(1, time / Math.max(duration, 0.0001));
    const [leftGain, rightGain] = panGains(pan + (panTo - pan) * progress);
    const gain = amplitude * envelope(time, duration, attack, release, decay);
    output.left[first + index] +=
      (highpass > 0 ? lowLeft - highReferenceLeft : lowLeft) * gain * leftGain;
    output.right[first + index] +=
      (highpass > 0 ? lowRight - highReferenceRight : lowRight) * gain * rightGain;
  }
}

function addModes(
  output,
  { start = 0, frequencies, amplitude, decay, spread = 0.48, color = "metal" },
) {
  const partials =
    color === "hollow"
      ? [[1, 1], [1.5, 0.18], [2.02, 0.08]]
      : color === "glass"
        ? [[1, 1], [2.01, 0.18], [3.97, 0.07]]
        : [[1, 1], [1.61, 0.2], [2.37, 0.08]];
  frequencies.forEach((frequency, index) => {
    const side = index % 2 === 0 ? -1 : 1;
    addTone(output, {
      start,
      duration: Math.min(output.left.length / RENDER_RATE - start, decay * 5.2),
      from: frequency * (1 + side * 0.0028),
      to: frequency * (1 - side * 0.0012),
      amplitude: amplitude / (1 + index * 0.46),
      attack: 0.0007,
      release: Math.min(0.15, decay),
      decay: decay / (1 + index * 0.14),
      pan: side * spread * Math.min(1, 0.44 + index * 0.1),
      partials,
    });
  });
}

function addClick(output, random, start, material, amplitude = 0.45, pan = 0) {
  const recipes = {
    relay: { lowpass: 12_000, highpass: 1_500, frequencies: [690, 1_430, 2_870], decay: 0.045 },
    ceramic: { lowpass: 19_000, highpass: 2_300, frequencies: [1_180, 2_730, 4_910], decay: 0.07 },
    polymer: { lowpass: 5_200, highpass: 70, frequencies: [245, 490, 910], decay: 0.065 },
    steel: { lowpass: 12_500, highpass: 180, frequencies: [410, 783, 1_460, 2_610], decay: 0.13 },
    wood: { lowpass: 4_300, highpass: 45, frequencies: [176, 421, 770], decay: 0.085 },
  };
  const recipe = recipes[material];
  addNoise(output, random, {
    start,
    duration: material === "steel" ? 0.11 : 0.065,
    amplitude,
    release: 0.055,
    decay: 0.014,
    lowpass: recipe.lowpass,
    highpass: recipe.highpass,
    pan,
    stereo: 0.52,
  });
  addModes(output, {
    start,
    frequencies: recipe.frequencies,
    amplitude: amplitude * 0.3,
    decay: recipe.decay,
    spread: 0.54,
    color: material === "ceramic" ? "glass" : material === "wood" ? "hollow" : "metal",
  });
}

function addServo(
  output,
  random,
  { start, duration, from, to, amplitude, pan = 0, panTo = pan, grit = 0.06 },
) {
  addTone(output, {
    start,
    duration,
    from,
    to,
    amplitude,
    attack: Math.min(0.014, duration * 0.1),
    release: Math.min(0.12, duration * 0.32),
    decay: duration * 1.1,
    pan,
    panTo,
    partials: [[1, 1], [2, 0.17], [3.01, 0.055]],
    fmRatio: 6,
    fmIndex: 0.08,
    fmDecay: Math.max(0.03, duration * 0.35),
  });
  addNoise(output, random, {
    start,
    duration,
    amplitude: grit,
    attack: 0.003,
    release: Math.min(0.1, duration * 0.28),
    decay: duration * 0.9,
    lowpass: Math.min(11_000, Math.max(from, to) * 8),
    highpass: Math.min(from, to) * 0.55,
    pan,
    panTo,
    stereo: 0.26,
    roughness: 9,
  });
}

function addCodeBurst(
  output,
  random,
  { start, frequency, pattern, amplitude = 0.16, pan = 0, panTo = pan, step = 0.026 },
) {
  const bits = [...pattern];
  bits.forEach((bit, index) => {
    if (bit !== "1") return;
    const progress = bits.length === 1 ? 0 : index / (bits.length - 1);
    const position = pan + (panTo - pan) * progress;
    const at = start + index * step;
    addTone(output, {
      start: at,
      duration: step * 0.72,
      from: frequency * (1 + (index % 3) * 0.012),
      to: frequency * 0.97,
      amplitude,
      attack: 0.0005,
      release: step * 0.4,
      decay: step * 0.58,
      pan: position,
      partials: [[1, 1], [2.013, 0.15]],
      fmRatio: 1.414,
      fmIndex: 0.17,
      fmDecay: step,
    });
    addNoise(output, random, {
      start: at,
      duration: step * 0.45,
      amplitude: amplitude * 0.16,
      release: step * 0.3,
      decay: step * 0.24,
      lowpass: 18_000,
      highpass: 5_000,
      pan: position,
      stereo: 0.72,
    });
  });
}

function addPluck(
  output,
  random,
  { start, frequency, duration, amplitude, damping = 0.994, pan = 0, panTo = pan },
) {
  const first = Math.floor(start * RENDER_RATE);
  const count = Math.min(
    Math.ceil(duration * RENDER_RATE),
    Math.max(0, output.left.length - first),
  );
  const delayLength = Math.max(2, Math.round(RENDER_RATE / frequency));
  const buffer = new Float64Array(delayLength);
  for (let index = 0; index < buffer.length; index++) buffer[index] = random() * 2 - 1;
  let cursor = 0;
  let previous = 0;
  for (let index = 0; index < count; index++) {
    const next = (cursor + 1) % buffer.length;
    const value = buffer[cursor];
    buffer[cursor] = (value * 0.48 + buffer[next] * 0.52) * damping;
    cursor = next;
    const time = index / RENDER_RATE;
    const progress = Math.min(1, time / Math.max(duration, 0.0001));
    const bright = value - previous * 0.18;
    previous = value;
    const [leftGain, rightGain] = panGains(pan + (panTo - pan) * progress);
    const gain = amplitude * envelope(time, duration, 0.001, Math.min(0.16, duration * 0.35), duration * 0.72);
    output.left[first + index] += bright * gain * leftGain;
    output.right[first + index] += bright * gain * rightGain;
  }
}

function addRattle(
  output,
  random,
  { start, duration, count, material = "steel", amplitude = 0.11, low = 240, high = 2_800 },
) {
  for (let index = 0; index < count; index++) {
    const at = start + random() ** 0.78 * duration;
    const frequency = low * (high / low) ** random();
    const pan = random() * 1.7 - 0.85;
    if (material === "spring") {
      addPluck(output, random, {
        start: at,
        frequency,
        duration: 0.08 + random() * 0.16,
        amplitude: amplitude * (0.65 + random() * 0.55),
        damping: 0.991 + random() * 0.005,
        pan,
      });
    } else {
      addClick(output, random, at, material, amplitude * (0.55 + random() * 0.45), pan);
    }
  }
}

function addSpace(output, seconds, feedback, mix) {
  const delay = Math.max(1, Math.round(seconds * RENDER_RATE));
  const sourceLeft = output.left.slice();
  const sourceRight = output.right.slice();
  for (let index = delay; index < output.left.length; index++) {
    const leftEcho = sourceRight[index - delay];
    const rightEcho = sourceLeft[index - delay];
    output.left[index] += leftEcho * mix;
    output.right[index] += rightEcho * mix;
    if (index >= delay * 2) {
      output.left[index] += sourceLeft[index - delay * 2] * mix * feedback;
      output.right[index] += sourceRight[index - delay * 2] * mix * feedback;
    }
  }
}

function renderServoLeague(cue, output, random) {
  switch (cue.id) {
    case "projectile":
      addClick(output, random, 0, "relay", 0.48);
      addServo(output, random, { start: 0.006, duration: 0.31, from: 1_180, to: 205, amplitude: 0.32, pan: -0.12, panTo: 0.1, grit: 0.075 });
      addNoise(output, random, { start: 0.012, duration: 0.19, amplitude: 0.11, release: 0.08, decay: 0.04, lowpass: 9_000, highpass: 850, pan: -0.2, panTo: 0.2, stereo: 0.5 });
      addSpace(output, 0.047, 0.12, 0.075);
      break;
    case "impact":
      addClick(output, random, 0, "polymer", 0.68);
      addModes(output, { start: 0.004, frequencies: [318, 547, 930, 1_590], amplitude: 0.27, decay: 0.1, spread: 0.42 });
      addRattle(output, random, { start: 0.035, duration: 0.2, count: 4, material: "ceramic", amplitude: 0.055, low: 1_400, high: 4_200 });
      break;
    case "destroyed":
      addClick(output, random, 0, "steel", 0.62);
      addServo(output, random, { start: 0.018, duration: 0.72, from: 286, to: 43, amplitude: 0.42, pan: 0.08, panTo: -0.18, grit: 0.12 });
      addTone(output, { start: 0.03, duration: 0.62, from: 98, to: 39, amplitude: 0.31, attack: 0.005, release: 0.25, decay: 0.2, partials: [[1, 1], [1.51, 0.22], [2.07, 0.09]] });
      addRattle(output, random, { start: 0.08, duration: 0.72, count: 12, material: "steel", amplitude: 0.08, low: 190, high: 3_200 });
      addSpace(output, 0.071, 0.16, 0.11);
      break;
    case "arc-birth":
      addClick(output, random, 0, "relay", 0.4, -0.12);
      addClick(output, random, 0.085, "relay", 0.32, 0.12);
      addServo(output, random, { start: 0.04, duration: 0.61, from: 86, to: 680, amplitude: 0.34, pan: -0.25, panTo: 0.25, grit: 0.07 });
      [0.28, 0.405, 0.53].forEach((start, index) => addClick(output, random, start, "ceramic", 0.18 + index * 0.02, -0.34 + index * 0.34));
      addModes(output, { start: 0.22, frequencies: [392, 622, 987], amplitude: 0.13, decay: 0.16, spread: 0.62, color: "glass" });
      addSpace(output, 0.083, 0.14, 0.1);
      break;
    case "arc-pickup":
      addClick(output, random, 0, "relay", 0.5);
      addServo(output, random, { start: 0.02, duration: 0.32, from: 240, to: 920, amplitude: 0.29, grit: 0.045 });
      addClick(output, random, 0.21, "ceramic", 0.2);
      break;
    case "arc-steal":
      addClick(output, random, 0, "relay", 0.56, 0.55);
      addServo(output, random, { start: 0.008, duration: 0.36, from: 1_260, to: 180, amplitude: 0.34, pan: 0.62, panTo: -0.62, grit: 0.08 });
      addClick(output, random, 0.19, "relay", 0.42, -0.5);
      addServo(output, random, { start: 0.2, duration: 0.29, from: 210, to: 690, amplitude: 0.22, pan: -0.45, panTo: 0.05, grit: 0.04 });
      addSpace(output, 0.059, 0.1, 0.07);
      break;
    case "arc-drop":
      addClick(output, random, 0, "polymer", 0.58);
      addServo(output, random, { start: 0.01, duration: 0.34, from: 650, to: 94, amplitude: 0.28, grit: 0.05 });
      addClick(output, random, 0.22, "polymer", 0.22, 0.18);
      break;
    case "arc-bank":
      [0.015, 0.13, 0.245].forEach((start, index) => addClick(output, random, start, "relay", 0.34 + index * 0.035, -0.42 + index * 0.42));
      addClick(output, random, 0.31, "polymer", 0.62);
      addServo(output, random, { start: 0.29, duration: 0.44, from: 126, to: 510, amplitude: 0.31, pan: -0.22, panTo: 0.22, grit: 0.055 });
      addModes(output, { start: 0.36, frequencies: [220, 330, 495], amplitude: 0.15, decay: 0.19, spread: 0.5, color: "hollow" });
      addSpace(output, 0.091, 0.15, 0.095);
      break;
    case "arc-pulse":
      addServo(output, random, { start: 0, duration: 0.52, from: 54, to: 760, amplitude: 0.35, pan: -0.48, panTo: 0.48, grit: 0.11 });
      addNoise(output, random, { start: 0.05, duration: 0.46, amplitude: 0.16, attack: 0.08, release: 0.04, decay: 0, lowpass: 12_000, highpass: 400, pan: -0.72, panTo: 0.72, stereo: 0.7 });
      addClick(output, random, 0.48, "steel", 0.78);
      addTone(output, { start: 0.475, duration: 0.82, from: 112, to: 32, amplitude: 0.56, attack: 0, release: 0.44, decay: 0.24, partials: [[1, 1], [1.49, 0.27], [2.11, 0.1]] });
      addModes(output, { start: 0.49, frequencies: [164, 271, 447, 736], amplitude: 0.24, decay: 0.22, spread: 0.76 });
      addSpace(output, 0.137, 0.2, 0.16);
      break;
    case "arc-handoff":
      addClick(output, random, 0, "relay", 0.45, -0.48);
      addServo(output, random, { start: 0.015, duration: 0.3, from: 320, to: 670, amplitude: 0.22, pan: -0.52, panTo: 0.52, grit: 0.035 });
      addClick(output, random, 0.17, "relay", 0.48, 0.48);
      break;
    case "deflect":
      addClick(output, random, 0, "ceramic", 0.68, -0.2);
      addTone(output, { start: 0.004, duration: 0.31, from: 2_900, to: 1_180, amplitude: 0.21, release: 0.17, decay: 0.09, pan: -0.42, panTo: 0.62, partials: [[1, 1], [1.71, 0.2]] });
      addSpace(output, 0.041, 0.08, 0.06);
      break;
    case "arrival":
      addClick(output, random, 0.025, "relay", 0.34, -0.2);
      addClick(output, random, 0.12, "relay", 0.38, 0.2);
      addServo(output, random, { start: 0.02, duration: 0.46, from: 72, to: 430, amplitude: 0.27, pan: -0.18, panTo: 0.18, grit: 0.06 });
      addClick(output, random, 0.32, "polymer", 0.48);
      addModes(output, { start: 0.34, frequencies: [262, 392, 589], amplitude: 0.1, decay: 0.12, spread: 0.4 });
      break;
  }
}

function renderSignalGrid(cue, output, random) {
  switch (cue.id) {
    case "projectile":
      addClick(output, random, 0, "ceramic", 0.38);
      addTone(output, { start: 0.004, duration: 0.29, from: 2_180, to: 260, amplitude: 0.29, release: 0.11, decay: 0.08, pan: -0.24, panTo: 0.24, partials: [[1, 1], [2.017, 0.17]], fmRatio: 1.618, fmIndex: 0.72, fmDecay: 0.08 });
      addCodeBurst(output, random, { start: 0.07, frequency: 1_320, pattern: "10110", amplitude: 0.09, pan: 0.3, panTo: -0.25, step: 0.025 });
      addSpace(output, 0.061, 0.15, 0.09);
      break;
    case "impact":
      addClick(output, random, 0, "ceramic", 0.54);
      addModes(output, { start: 0, frequencies: [640, 1_037, 1_731, 2_887], amplitude: 0.27, decay: 0.105, spread: 0.75, color: "glass" });
      addTone(output, { start: 0.018, duration: 0.3, from: 780, to: 186, amplitude: 0.18, release: 0.13, decay: 0.075, pan: 0.36, panTo: -0.2, fmRatio: 2.414, fmIndex: 0.48, fmDecay: 0.08 });
      break;
    case "destroyed":
      addCodeBurst(output, random, { start: 0, frequency: 1_480, pattern: "111001011001", amplitude: 0.19, pan: -0.6, panTo: 0.6, step: 0.029 });
      addTone(output, { start: 0.04, duration: 0.78, from: 420, to: 38, amplitude: 0.42, release: 0.34, decay: 0.22, pan: 0.18, panTo: -0.32, partials: [[1, 1], [1.502, 0.19]], fmRatio: 3.03, fmIndex: 0.78, fmDecay: 0.32 });
      addNoise(output, random, { start: 0.08, duration: 0.72, amplitude: 0.15, release: 0.3, decay: 0.18, lowpass: 15_000, highpass: 2_600, pan: -0.7, panTo: 0.7, stereo: 0.9, roughness: 3 });
      addRattle(output, random, { start: 0.18, duration: 0.58, count: 9, material: "ceramic", amplitude: 0.045, low: 1_700, high: 7_000 });
      addSpace(output, 0.103, 0.2, 0.15);
      break;
    case "arc-birth":
      addCodeBurst(output, random, { start: 0, frequency: 720, pattern: "10100111", amplitude: 0.11, pan: -0.55, panTo: 0.55, step: 0.03 });
      [174, 261, 391, 587].forEach((frequency, index) => addTone(output, { start: 0.08 + index * 0.105, duration: 0.48, from: frequency, to: frequency * 1.08, amplitude: 0.16, attack: 0.012, release: 0.24, decay: 0.25, pan: -0.54 + index * 0.36, partials: [[1, 1], [2.006, 0.18]], fmRatio: 1.414, fmIndex: 0.14, fmDecay: 0.15 }));
      addNoise(output, random, { start: 0.06, duration: 0.5, amplitude: 0.08, attack: 0.12, release: 0.2, decay: 0.4, lowpass: 13_000, highpass: 3_300, pan: -0.5, panTo: 0.5, stereo: 0.86 });
      addSpace(output, 0.097, 0.18, 0.13);
      break;
    case "arc-pickup":
      addCodeBurst(output, random, { start: 0, frequency: 1_020, pattern: "10111", amplitude: 0.14, pan: -0.25, panTo: 0.25, step: 0.028 });
      addTone(output, { start: 0.035, duration: 0.31, from: 310, to: 980, amplitude: 0.25, release: 0.16, decay: 0.18, pan: -0.2, panTo: 0.2, partials: [[1, 1], [2.01, 0.13]], fmRatio: 2.01, fmIndex: 0.18, fmDecay: 0.12 });
      break;
    case "arc-steal":
      addCodeBurst(output, random, { start: 0, frequency: 1_520, pattern: "11101001", amplitude: 0.17, pan: 0.65, panTo: -0.65, step: 0.028 });
      addTone(output, { start: 0.01, duration: 0.42, from: 1_690, to: 205, amplitude: 0.3, release: 0.18, decay: 0.14, pan: 0.72, panTo: -0.72, partials: [[1, 1], [1.501, 0.22]], fmRatio: 1.618, fmIndex: 0.58, fmDecay: 0.17 });
      addTone(output, { start: 0.2, duration: 0.3, from: 240, to: 720, amplitude: 0.18, release: 0.15, decay: 0.14, pan: -0.5, panTo: 0.15, partials: [[1, 1], [2.013, 0.12]] });
      addSpace(output, 0.067, 0.13, 0.1);
      break;
    case "arc-drop":
      addClick(output, random, 0, "ceramic", 0.38);
      addTone(output, { start: 0, duration: 0.37, from: 780, to: 88, amplitude: 0.3, release: 0.2, decay: 0.13, partials: [[1, 1], [2.019, 0.14]], fmRatio: 1.414, fmIndex: 0.44, fmDecay: 0.14 });
      addCodeBurst(output, random, { start: 0.19, frequency: 430, pattern: "10010", amplitude: 0.09, pan: 0.3, panTo: -0.2, step: 0.032 });
      break;
    case "arc-bank":
      [0.01, 0.13, 0.25].forEach((start, index) => addCodeBurst(output, random, { start, frequency: 620 * (1 + index * 0.5), pattern: "101", amplitude: 0.13, pan: -0.5 + index * 0.5, step: 0.021 }));
      addClick(output, random, 0.31, "ceramic", 0.48);
      addModes(output, { start: 0.3, frequencies: [196, 294, 441, 661], amplitude: 0.21, decay: 0.2, spread: 0.7, color: "glass" });
      addTone(output, { start: 0.29, duration: 0.5, from: 180, to: 720, amplitude: 0.21, attack: 0.018, release: 0.25, decay: 0.24, pan: -0.4, panTo: 0.4, fmRatio: 2.414, fmIndex: 0.2, fmDecay: 0.2 });
      addSpace(output, 0.112, 0.2, 0.14);
      break;
    case "arc-pulse":
      addCodeBurst(output, random, { start: 0, frequency: 520, pattern: "101101001011", amplitude: 0.12, pan: -0.78, panTo: 0.78, step: 0.035 });
      addTone(output, { start: 0, duration: 0.5, from: 62, to: 1_080, amplitude: 0.34, attack: 0.06, release: 0.04, pan: -0.65, panTo: 0.65, partials: [[1, 1], [2.005, 0.16]], fmRatio: 1.618, fmIndex: 0.42, fmDecay: 0.4 });
      addNoise(output, random, { start: 0.08, duration: 0.45, amplitude: 0.18, attack: 0.12, release: 0.035, decay: 0, lowpass: 18_000, highpass: 2_400, pan: -0.85, panTo: 0.85, stereo: 0.95 });
      addClick(output, random, 0.48, "ceramic", 0.72);
      addTone(output, { start: 0.47, duration: 0.86, from: 128, to: 29, amplitude: 0.55, release: 0.48, decay: 0.25, partials: [[1, 1], [1.414, 0.24], [2.01, 0.1]], fmRatio: 1.618, fmIndex: 0.5, fmDecay: 0.24 });
      addModes(output, { start: 0.49, frequencies: [211, 341, 552, 893], amplitude: 0.23, decay: 0.28, spread: 0.9, color: "glass" });
      addSpace(output, 0.151, 0.24, 0.19);
      break;
    case "arc-handoff":
      addCodeBurst(output, random, { start: 0, frequency: 850, pattern: "10110", amplitude: 0.14, pan: -0.7, panTo: -0.1, step: 0.026 });
      addCodeBurst(output, random, { start: 0.15, frequency: 850, pattern: "10110", amplitude: 0.14, pan: 0.1, panTo: 0.7, step: 0.026 });
      addTone(output, { start: 0.045, duration: 0.28, from: 340, to: 680, amplitude: 0.16, release: 0.12, decay: 0.14, pan: -0.58, panTo: 0.58, fmRatio: 2.01, fmIndex: 0.14, fmDecay: 0.1 });
      break;
    case "deflect":
      addClick(output, random, 0, "ceramic", 0.52, -0.35);
      addTone(output, { start: 0.003, duration: 0.3, from: 860, to: 3_420, amplitude: 0.22, release: 0.15, decay: 0.1, pan: -0.62, panTo: 0.68, partials: [[1, 1], [1.73, 0.17]], fmRatio: 2.414, fmIndex: 0.38, fmDecay: 0.09 });
      addCodeBurst(output, random, { start: 0.11, frequency: 1_900, pattern: "101", amplitude: 0.08, pan: 0.35, step: 0.024 });
      break;
    case "arrival":
      addCodeBurst(output, random, { start: 0, frequency: 480, pattern: "110101101", amplitude: 0.11, pan: -0.5, panTo: 0.5, step: 0.026 });
      addTone(output, { start: 0.04, duration: 0.48, from: 84, to: 510, amplitude: 0.25, attack: 0.025, release: 0.2, decay: 0.26, pan: -0.3, panTo: 0.3, partials: [[1, 1], [2.007, 0.14]], fmRatio: 1.618, fmIndex: 0.2, fmDecay: 0.2 });
      addModes(output, { start: 0.31, frequencies: [262, 416, 660], amplitude: 0.12, decay: 0.14, spread: 0.58, color: "glass" });
      addSpace(output, 0.087, 0.14, 0.1);
      break;
  }
}

function renderFieldResonance(cue, output, random) {
  switch (cue.id) {
    case "projectile":
      addClick(output, random, 0, "wood", 0.45);
      addPluck(output, random, { start: 0.005, frequency: 226, duration: 0.34, amplitude: 0.5, damping: 0.995, pan: -0.22, panTo: 0.22 });
      addNoise(output, random, { start: 0.01, duration: 0.22, amplitude: 0.13, release: 0.1, decay: 0.06, lowpass: 8_500, highpass: 600, pan: -0.34, panTo: 0.34, stereo: 0.5 });
      break;
    case "impact":
      addClick(output, random, 0, "wood", 0.72);
      addModes(output, { start: 0, frequencies: [188, 397, 733, 1_212], amplitude: 0.28, decay: 0.12, spread: 0.42, color: "hollow" });
      addRattle(output, random, { start: 0.035, duration: 0.22, count: 4, material: "spring", amplitude: 0.05, low: 620, high: 2_500 });
      break;
    case "destroyed":
      addClick(output, random, 0, "wood", 0.66);
      addClick(output, random, 0.04, "steel", 0.38, 0.18);
      [172, 263, 397].forEach((frequency, index) => addPluck(output, random, { start: 0.02 + index * 0.06, frequency, duration: 0.68 - index * 0.07, amplitude: 0.38 - index * 0.045, damping: 0.994, pan: -0.5 + index * 0.5 }));
      addTone(output, { start: 0.03, duration: 0.76, from: 92, to: 34, amplitude: 0.34, release: 0.36, decay: 0.22, partials: [[1, 1], [1.48, 0.2], [2.1, 0.08]] });
      addRattle(output, random, { start: 0.1, duration: 0.7, count: 13, material: "spring", amplitude: 0.065, low: 180, high: 3_400 });
      addSpace(output, 0.079, 0.14, 0.105);
      break;
    case "arc-birth":
      addModes(output, { start: 0, frequencies: [94, 151, 244, 394], amplitude: 0.31, decay: 0.26, spread: 0.58, color: "hollow" });
      addNoise(output, random, { start: 0.02, duration: 0.55, amplitude: 0.13, attack: 0.09, release: 0.22, decay: 0.45, lowpass: 7_500, highpass: 320, pan: -0.55, panTo: 0.55, stereo: 0.78, roughness: 2 });
      [330, 495, 742].forEach((frequency, index) => addPluck(output, random, { start: 0.19 + index * 0.11, frequency, duration: 0.42, amplitude: 0.2, damping: 0.996, pan: -0.48 + index * 0.48 }));
      addSpace(output, 0.107, 0.18, 0.13);
      break;
    case "arc-pickup":
      addClick(output, random, 0, "wood", 0.48);
      addPluck(output, random, { start: 0.012, frequency: 330, duration: 0.36, amplitude: 0.4, damping: 0.996, pan: -0.18, panTo: 0.18 });
      addPluck(output, random, { start: 0.13, frequency: 660, duration: 0.24, amplitude: 0.18, damping: 0.995, pan: 0.2 });
      break;
    case "arc-steal":
      addClick(output, random, 0, "steel", 0.5, 0.5);
      addPluck(output, random, { start: 0, frequency: 510, duration: 0.5, amplitude: 0.5, damping: 0.996, pan: 0.62, panTo: -0.62 });
      addPluck(output, random, { start: 0.17, frequency: 238, duration: 0.38, amplitude: 0.38, damping: 0.994, pan: -0.52, panTo: 0.1 });
      addNoise(output, random, { start: 0.02, duration: 0.28, amplitude: 0.1, release: 0.14, decay: 0.08, lowpass: 6_400, highpass: 700, pan: 0.55, panTo: -0.55, stereo: 0.62 });
      addSpace(output, 0.073, 0.12, 0.08);
      break;
    case "arc-drop":
      addClick(output, random, 0, "wood", 0.62);
      addPluck(output, random, { start: 0.005, frequency: 190, duration: 0.39, amplitude: 0.42, damping: 0.993, pan: -0.1, panTo: 0.2 });
      addClick(output, random, 0.2, "wood", 0.24, 0.22);
      break;
    case "arc-bank":
      [0.01, 0.135, 0.26].forEach((start, index) => addClick(output, random, start, "wood", 0.42 + index * 0.04, -0.42 + index * 0.42));
      addModes(output, { start: 0.28, frequencies: [110, 178, 288, 466], amplitude: 0.31, decay: 0.23, spread: 0.56, color: "hollow" });
      addPluck(output, random, { start: 0.3, frequency: 220, duration: 0.48, amplitude: 0.35, damping: 0.996, pan: -0.3, panTo: 0.3 });
      addClick(output, random, 0.34, "steel", 0.28);
      addSpace(output, 0.119, 0.18, 0.13);
      break;
    case "arc-pulse":
      addTone(output, { start: 0, duration: 0.48, from: 48, to: 260, amplitude: 0.36, attack: 0.07, release: 0.035, pan: -0.45, panTo: 0.45, partials: [[1, 1], [1.5, 0.2], [2.01, 0.08]] });
      addNoise(output, random, { start: 0.03, duration: 0.47, amplitude: 0.17, attack: 0.1, release: 0.04, decay: 0, lowpass: 9_500, highpass: 240, pan: -0.78, panTo: 0.78, stereo: 0.9, roughness: 2 });
      addClick(output, random, 0.47, "steel", 0.72);
      addPluck(output, random, { start: 0.47, frequency: 82, duration: 0.86, amplitude: 0.58, damping: 0.998, pan: -0.15, panTo: 0.15 });
      addTone(output, { start: 0.47, duration: 0.86, from: 104, to: 31, amplitude: 0.42, release: 0.46, decay: 0.24, partials: [[1, 1], [1.49, 0.24], [2.12, 0.09]] });
      addModes(output, { start: 0.49, frequencies: [152, 251, 414, 683], amplitude: 0.23, decay: 0.27, spread: 0.78, color: "hollow" });
      addSpace(output, 0.143, 0.21, 0.17);
      break;
    case "arc-handoff":
      addClick(output, random, 0, "wood", 0.42, -0.5);
      addPluck(output, random, { start: 0.01, frequency: 294, duration: 0.34, amplitude: 0.34, damping: 0.995, pan: -0.56, panTo: 0.56 });
      addClick(output, random, 0.18, "wood", 0.44, 0.5);
      break;
    case "deflect":
      addClick(output, random, 0, "steel", 0.5, -0.3);
      addPluck(output, random, { start: 0, frequency: 1_260, duration: 0.35, amplitude: 0.46, damping: 0.997, pan: -0.55, panTo: 0.65 });
      addNoise(output, random, { start: 0.006, duration: 0.13, amplitude: 0.08, release: 0.07, decay: 0.03, lowpass: 12_000, highpass: 2_100, pan: -0.4, panTo: 0.55, stereo: 0.7 });
      break;
    case "arrival":
      [0.015, 0.095, 0.18].forEach((start, index) => addClick(output, random, start, "wood", 0.24 + index * 0.035, -0.3 + index * 0.3));
      addModes(output, { start: 0.04, frequencies: [88, 142, 230], amplitude: 0.24, decay: 0.2, spread: 0.36, color: "hollow" });
      addPluck(output, random, { start: 0.13, frequency: 196, duration: 0.43, amplitude: 0.34, damping: 0.995, pan: -0.24, panTo: 0.24 });
      addNoise(output, random, { start: 0.08, duration: 0.38, amplitude: 0.07, attack: 0.04, release: 0.16, decay: 0.2, lowpass: 5_800, highpass: 260, pan: -0.3, panTo: 0.3, stereo: 0.58 });
      addSpace(output, 0.089, 0.13, 0.09);
      break;
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
    const y = b0 * x + b1 * x1 + b2 * x2 - normalizedA1 * y1 - normalizedA2 * y2;
    channel[index] = y;
    x2 = x1;
    x1 = x;
    y2 = y1;
    y1 = y;
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
      if (sourceIndex >= 0 && sourceIndex < channel.length)
        value += channel[sourceIndex] * kernel[tap];
    }
    result[index] = value / kernelSum;
  }
  return result;
}

function finalize(output, targetPeak = 0.86) {
  for (const channel of [output.left, output.right]) {
    biquad(channel, "highpass", 30, 0.707);
    biquad(channel, "lowpass", 21_000, 0.68);
  }
  let follower = 0;
  const attack = 1 - Math.exp(-1 / (0.002 * RENDER_RATE));
  const release = 1 - Math.exp(-1 / (0.07 * RENDER_RATE));
  for (let index = 0; index < output.left.length; index++) {
    output.left[index] = Math.tanh(output.left[index] * 1.06) / Math.tanh(1.06);
    output.right[index] = Math.tanh(output.right[index] * 1.06) / Math.tanh(1.06);
    const level = Math.max(Math.abs(output.left[index]), Math.abs(output.right[index]));
    follower += (level - follower) * (level > follower ? attack : release);
    const compressed = follower > 0.46 ? 0.46 + (follower - 0.46) / 3 : follower;
    const gain = follower > 0 ? Math.min(1, compressed / follower) : 1;
    output.left[index] *= gain;
    output.right[index] *= gain;
  }
  const left = downsample(output.left);
  const right = downsample(output.right);
  let peak = 0;
  for (let index = 0; index < left.length; index++)
    peak = Math.max(peak, Math.abs(left[index]), Math.abs(right[index]));
  const scale = peak > 0 ? targetPeak / peak : 1;
  const fadeIn = Math.round(0.0005 * SAMPLE_RATE);
  const fadeOut = Math.round(0.022 * SAMPLE_RATE);
  let sumSquares = 0;
  let differenceSquares = 0;
  let sumLeft = 0;
  let sumRight = 0;
  peak = 0;
  for (let index = 0; index < left.length; index++) {
    const head = Math.min(1, index / Math.max(1, fadeIn));
    const tail = Math.min(1, Math.max(0, (left.length - index - 1) / Math.max(1, fadeOut)));
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
    dcOffset: Math.max(Math.abs(sumLeft / left.length), Math.abs(sumRight / right.length)),
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
    version: 3,
    generation: "arc-relay-full-vocabulary",
    generatedBy: "scripts/generate-audio-v3-candidates.mjs",
    sampleRate: SAMPLE_RATE,
    renderSampleRate: RENDER_RATE,
    channels: 2,
    format: "pcm-s16le-wav",
    reviewScope: {
      game: "Arc Relay",
      soundtrack: "unchanged",
      runtimePack: "unchanged pending owner selection",
      mappedCueIds: cues.filter((cue) => cue.status === "mapped").map((cue) => cue.id),
      proposedCueIds: cues.filter((cue) => cue.status === "proposed").map((cue) => cue.id),
    },
    packs: [],
  };

  for (const pack of packs) {
    const directory = path.join(ROOT, "v3", "packs", pack.id);
    await mkdir(directory, { recursive: true });
    const renderedCues = [];
    for (const cue of cues) {
      const output = createStereo(cue.duration);
      const random = seeded(`${pack.id}/${cue.id}/v3`);
      pack.render(cue, output, random);
      const audio = finalize(output, cue.id === "arc-pulse" ? 0.88 : 0.86);
      const filename = `${cue.id}.wav`;
      await writeFile(
        path.join(directory, filename),
        encodeWav(audio, seeded(`${pack.id}/${cue.id}/dither/v3`)),
      );
      renderedCues.push({
        id: cue.id,
        label: cue.label,
        category: cue.category,
        status: cue.status,
        description: cue.description,
        mapping: cue.mapping,
        file: `./v3/packs/${pack.id}/${filename}`,
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

  const json = `${JSON.stringify(manifest, null, 2)}\n`;
  await writeFile(path.join(ROOT, "manifest-v3.json"), json);
  await writeFile(
    path.join(ROOT, "manifest-v3.js"),
    `window.SOUND_LAB_V3_MANIFEST = ${JSON.stringify(manifest)};\n`,
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
    `Generated ${manifest.packs.length * cues.length} V3 cues ` +
      `(${(bytes / 1_048_576).toFixed(2)} MiB) in ${ROOT}`,
  );
}

await main();
