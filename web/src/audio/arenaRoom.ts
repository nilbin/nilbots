/**
 * The room the arena is fought in.
 *
 * Cues currently arrive completely dry, which is why they read as sounds played *at* the
 * viewer rather than events happening somewhere — a shot on the far side of a metal hall
 * should not sound identical to one at your feet. A short convolution reverb is the
 * cheapest way to place them in a space, and it composes with the stereo panning already
 * there: pan says where across, the room says how far into it.
 *
 * **The impulse response is synthesized, not shipped.** A recorded IR is another asset in
 * a payload this project has spent real effort shrinking, and a plausible metal room is a
 * few hundred lines of noise and an envelope. It is also *deterministic* — see below —
 * which a recorded file would be too, but which a naive `Math.random()` would not.
 */

/** Long enough to hear the space, short enough not to smear a five-per-second cue rate. */
const SECONDS = 1.1;

/** How much of the wet signal reaches the master. An arena, not a cathedral. */
export const ROOM_MIX = 0.16;

/**
 * Build the arena's impulse response.
 *
 * Deterministic noise rather than `Math.random()`: replays are deterministic, and two
 * people reviewing the same fight should hear the same mix. It also means a golden-frame
 * style audit of the audio graph is possible later, and that a reverb tail cannot change
 * between two runs of the same page for no reason anyone can point at.
 */
export function createArenaImpulse(context: BaseAudioContext): AudioBuffer {
  const length = Math.floor(context.sampleRate * SECONDS);
  const impulse = context.createBuffer(2, length, context.sampleRate);

  for (let channel = 0; channel < 2; channel++) {
    const samples = impulse.getChannelData(channel);
    // A different seed per channel decorrelates them, which is what makes the tail sound
    // wide instead of like one mono reverb sitting in the middle of the image.
    const random = splitMix(channel === 0 ? 0x9e3779b9 : 0x85ebca6b);
    for (let i = 0; i < length; i++) {
      const t = i / length;
      // Exponential decay with an early build: a hall does not reach full density
      // instantly, and the ramp is what stops the tail sounding like a gate.
      const envelope = (1 - Math.exp(-t * 40)) * Math.exp(-t * 5.2);
      samples[i] = (random() * 2 - 1) * envelope;
    }
  }

  return impulse;
}

/**
 * SplitMix32, the same family the engine's PRNG comes from.
 *
 * Reimplemented here rather than imported: the engine's is a gameplay surface pinned by
 * golden-value tests, and borrowing it would make a change to how the arena *sounds*
 * capable of failing a determinism test about how it *plays*.
 */
function splitMix(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x9e3779b9) >>> 0;
    let z = state;
    z = Math.imul(z ^ (z >>> 16), 0x21f0aaad) >>> 0;
    z = Math.imul(z ^ (z >>> 15), 0x735a2d97) >>> 0;
    return ((z ^ (z >>> 15)) >>> 0) / 4294967296;
  };
}
