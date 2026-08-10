# nilbots audio candidates

This directory contains review-stage audio, not runtime viewer assets.

Run:

```bash
node scripts/generate-audio-candidates.mjs
node scripts/generate-audio-v2-candidates.mjs
node scripts/generate-audio-v3-candidates.mjs
node scripts/generate-audio-v4-candidates.mjs
node scripts/validate-audio-candidates.mjs
node scripts/build-audio-sound-lab-site.mjs
node scripts/export-runtime-sound-effects.mjs
```

Then open `sound-lab/index.html` or serve `art/audio/sound-lab` from any static
server. Generation is deterministic: the same script revision produces the
same signed 16-bit PCM WAV files. V1 is 44.1 kHz mono; V2 through V4 render at
96 kHz and downsample to 48 kHz stereo.

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

V3 is the archived tactile Arc Relay reconcept. It presents three directions
with identical coverage of all nine runtime mappings and three explicit-replay
proposals:

- **Servo League** — tactile, fast, sporting miniature robotics.
- **Signal Grid** — coded, clean, intelligent machine language.
- **Field Resonance** — textured, grounded, map-friendly physical resonance.

V4 is the current space / ambient reset:

- **Halo Drift** — luminous, weightless, and extremely wide.
- **Deep Array** — dark, distant, and immense.
- **Solar Bloom** — warm, iridescent, and celestial.

The internal **projectile** cue ID is retained for runtime compatibility, but
its V4 sound and artifact label are **Beam resolve**. The full proposed attack
sentence is wind-up, then beam / fizzle / cancel. The wind-up comes from a new
public **pendingStrikes** entry; resolution is separated by the matching
authoritative traversal or its absence, while a pre-resolution disappearance
gets the deliberate cancel voice. The beam is a focused spectral field, not an
audible moving bolt. The proposed **projectile-deflected** fact is likewise
presented as a beam bend.

The proposed wind-up, fizzle, cancel, handoff, deflection, and arrival cues are
review material only.
Generating or opening the V4 artifact does not replace the approved runtime
pack and does not touch the Neon Protocol soundtrack. The ambient masters
already contain deliberate diffusion; runtime promotion should retune the
existing arena convolution rather than stack two full spaces.

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
