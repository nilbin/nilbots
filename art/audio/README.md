# nilbots audio candidates

This directory contains review-stage audio, not runtime viewer assets.

Run:

```bash
node scripts/generate-audio-candidates.mjs
node scripts/generate-audio-v2-candidates.mjs
node scripts/validate-audio-candidates.mjs
node scripts/build-audio-sound-lab-site.mjs
node scripts/export-runtime-sound-effects.mjs
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

V2 is a higher-fidelity vertical slice with four matched showcase moments and
four directions:

- **Aegis Systems** — precise, premium, and tactical.
- **Obsidian Foundry** — physical, dense, and cinematic.
- **Aurora Core** — luminous, energetic, and modern.
- **Nilbots Signature** — Aegis clarity, Obsidian weight, and Aurora lift.

V2 renders at 96 kHz before anti-aliased downsampling to 48 kHz stereo. Its
layers include separate transients, tonal bodies, modal material resonance,
debris, stereo movement, feedback-delay diffusion, and dynamics processing.
**Nilbots Signature** applies that combined language to the complete
projectile, impact, destruction, and unlock sample set. V1 remains in the
sound lab as a collapsed reference archive.

The lossless masters for every direction stay under `art/`. Obsidian Foundry
is the approved runtime direction; `scripts/export-runtime-sound-effects.mjs`
exports only its projectile, impact, and destruction cues to
`web/src/assets/audio/effects/obsidian-foundry/` as 48 kHz stereo AAC-LC. The
three checked-in delivery assets total roughly 0.07 MiB. The entitlement cue
remains a sound-lab master until a real entitlement notification owns it; it
is not a match-result sound.

The export requires macOS `afconvert`. Checked-in delivery assets keep normal
builds platform-independent, while their production manifest records the
generator, cleared rights, and explicit shipment approval.
