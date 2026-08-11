# nilbots audio candidates

This directory contains review-stage audio, not runtime viewer assets.

Run:

```bash
node scripts/generate-audio-candidates.mjs
node scripts/generate-audio-v2-candidates.mjs
node scripts/generate-audio-v3-candidates.mjs
node scripts/generate-audio-v4-candidates.mjs
node scripts/derive-audio-v5-revisions.mjs
node scripts/master-audio-v5-energy.mjs
node scripts/build-audio-v5-candidates.mjs
node scripts/validate-audio-candidates.mjs
node scripts/build-audio-sound-lab-site.mjs
node scripts/export-runtime-sound-effects.mjs
```

Then open `sound-lab/index.html` or serve `art/audio/sound-lab` from any static
server. V1 through V4 are deterministic: the same script revision produces the
same signed 16-bit PCM WAV files. V1 is 44.1 kHz mono; V2 through V4 render at
96 kHz and downsample to 48 kHz stereo. V5 keeps saved provider lineage for
sixteen matched cues plus one optional ambient loop. The derivation script
deterministically rebuilds three current variants from their accepted source
masters without contacting SFX Engine. The safety-master script then rebuilds
four high-energy provider cues from archived, hash-pinned pre-master WAVs
without spending credits.

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

V5 adds one provider-sourced direction without replacing V4:

- **Astral Relay** — SFX Engine's pristine, spectral, cinematic reading of the
  same sixteen-cue vocabulary.

Its exact prompts, private-request setting, provider IDs, timestamps, source
and output hashes, local transformations, processing disclosure, and measured
audio facts live in `sound-lab/v5/sfx-engine-generation.json`. The reusable
workflow and saved OpenAPI 3.0 contract live under `.claude/skills/sfx-engine/`;
the API credential does not. Rebuild the three local variants with
`scripts/derive-audio-v5-revisions.mjs`, run
`scripts/master-audio-v5-energy.mjs`, then rebuild the manifest with
`scripts/build-audio-v5-candidates.mjs`. Generate or replace provider cues only
through the skill workflow because each new request spends account credits.

SFX Engine returned WAV sources for all sixteen cues. Successive owner reviews
requested provider follow-up takes for armor impact, bot destruction, Core
pickup, Core stolen, ally handoff, fizzle, and beam deflection; Core pickup
reached its third take. Every rejected prompt, request, provider result, hash,
and metric set remains archived under the cue's `attempts` array.

The current pack then applies a no-API local inference pass to three cues. Bot
destruction reverses Unit arrival and gives its dispersal a longer exit. Core
drop reverses the current Core pickup, making release and acquisition one
timbral pair. Beam deflection extracts the accepted beam body after ignition,
softens it with a 3.2 kHz zero-phase low-pass, and bends it modestly from center
to right. These are deterministic transformations of hash-pinned Engine
sources—not fresh provider generations or synthesized layers. The manifest
labels all three as `local-derivation`; the other thirteen remain
provider-sourced rather than locally derived. The attack wind-up, current
pickup, and derived drop are effectively mono; no synthetic widening is added.
All one-shot masters are
48 kHz two-channel signed 16-bit PCM with DC correction, endpoint fades, and
transparent peak normalization.

The current attack wind-up, fizzle, armor-impact, and generated level-up
provider masters are preserved byte-for-byte under
`sound-lab/v5/sources/astral-relay/`. A
deterministic local safety pass applies zero-phase low-pass filtering, endpoint
fades, linked dynamics where needed, loudness fitting, and conservative peak
ceilings. It removes the first two cues' excessive energy above 12 kHz and the
impact and level-up inter-sample risk without synthesizing a replacement or
contacting the API. The generation record pins both the archived source and mastered output hashes,
operations, loudness, approximate true peak, and upper-band energy.
The same script audits all sixteen cues, the ambient loop, and the exact three
sound-lab timelines. Review playback uses one shared compressor followed by a
fixed 3 dB safety gain; the uncompressed sequence sums must remain at or below
-1 dBTP after that fixed gain.

V5 also includes a review-only **Astral arena ambience**. SFX Engine supplied a
32-second non-musical source; the workflow applies a two-second circular
equal-power tail/head crossfade to produce the 30-second looping master. It has
its own sound-lab control, runs quietly beneath cue and sequence playback, and
does not replace or modify Neon Protocol.

The internal **projectile** cue ID is retained for runtime compatibility, but
its V4 sound and artifact label are **Beam resolve**. The full proposed attack
sentence is wind-up, then beam / fizzle / cancel. The wind-up comes from a new
public **pendingStrikes** entry; resolution is separated by the matching
authoritative traversal or its absence, while a pre-resolution disappearance
gets the deliberate cancel voice. The beam is a focused spectral field, not an
audible moving bolt. The proposed **projectile-deflected** fact is likewise
presented as a beam bend.

The proposed wind-up, fizzle, cancel, handoff, deflection, arrival, and ambient
bed are review material only. Level up is also proposed from the authoritative
`leveled-up` fact: one compact bot-local field lock, kept below Core scoring in
the hierarchy and reviewed simultaneously with the destruction tail that can
cause it. A future runtime scheduler should coalesce multiple level facts for
the same actor and tick into one cue at the highest reached level.
Generating or opening the V4/V5 artifact does not replace the approved runtime
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
