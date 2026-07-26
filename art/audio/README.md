# nilbots audio candidates

This directory contains review-stage audio, not runtime viewer assets.

Run:

```bash
node scripts/generate-audio-candidates.mjs
node scripts/generate-audio-v2-candidates.mjs
node scripts/validate-audio-candidates.mjs
node scripts/build-audio-sound-lab-site.mjs
```

Then open `sound-lab/index.html` or serve `art/audio/sound-lab` from any static
server. Generation is deterministic: the same script revision produces the
same signed 16-bit, 44.1 kHz mono WAV files.

The build command creates the static worker package used for the owner-only
review site under the ignored `sandbox/audio-sound-lab-site` directory. It
reuses `.openai/hosting.json`; never create a second site for this review lab.

V1 contains three ten-cue procedural directions:

- **Vector Tactical** — clean, dry, and precise.
- **Foundry Signal** — mechanical, weighted, and industrial.
- **Neon Circuit** — synthetic, musical, and expressive.

V2 is a higher-fidelity vertical slice with four matched showcase moments:

- **Aegis Systems** — precise, premium, and tactical.
- **Obsidian Foundry** — physical, dense, and cinematic.
- **Aurora Core** — luminous, energetic, and modern.

V2 renders at 96 kHz before anti-aliased downsampling to 48 kHz stereo. Its
layers include separate transients, tonal bodies, modal material resonance,
debris, stereo movement, feedback-delay diffusion, and dynamics processing.
The focused **Nilbots Signature** unlock experiment combines Aegis clarity,
Obsidian physical weight, and Aurora's harmonic lift without replacing any
base candidate. V1 remains in the sound lab as a collapsed reference archive.

All candidates stay under `art/` until a direction is selected. This prevents
review sounds from entering the self-contained replay viewer. The
chosen runtime assets will be copied into a dedicated web audio package,
compressed for delivery, and covered by asset and replay-scheduling tests.
