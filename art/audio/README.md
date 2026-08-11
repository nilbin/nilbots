# nilbots audio candidates

This directory contains lossless source and review audio. Approved delivery
copies are exported separately into the runtime viewer assets.

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
sixteen matched cues plus three optional ambient loops. The derivation script
deterministically rebuilds three current variants from their accepted source
masters without contacting SFX Engine. The safety-master script then rebuilds
five high-energy provider cues from archived, hash-pinned pre-master WAVs
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

V4 is the procedural space / ambient reset:

- **Halo Drift** — luminous, weightless, and extremely wide.
- **Deep Array** — dark, distant, and immense.
- **Solar Bloom** — warm, iridescent, and celestial.

V5 adds the provider-sourced direction selected for runtime promotion:

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
If a forced provider replacement feeds a local derivation (currently beam into
deflection), run the safety master once before deriving, then derive and run the
safety master again so both the dependent WAV and final all-cue audit use the
accepted master. The ordinary checked-in rebuild remains derive, master, build.

SFX Engine returned WAV sources for all sixteen cues. Successive owner reviews
requested provider follow-up takes for armor impact, bot destruction, Core
pickup, Core stolen, ally handoff, fizzle, and beam deflection; Core pickup
then reached a fourth, explicitly levitating take, while wind-up and beam each
received a smoother low-noise replacement. Every rejected prompt, request,
provider result, hash, and metric set remains archived under the cue's
`attempts` array.

The current pack then applies a no-API local inference pass to three cues. Bot
destruction reverses Unit arrival and gives its dispersal a longer exit. Core
drop reverses the current Core pickup, making release and acquisition one
timbral pair. Beam deflection extracts the rounded hit from the accepted quiet
beam, softens it with a 3.2 kHz zero-phase low-pass, and bends it modestly from
center to right. These are deterministic transformations of hash-pinned Engine
sources—not fresh provider generations or synthesized layers. The manifest
labels all three as `local-derivation`; the other thirteen remain
provider-sourced rather than locally derived. The quiet beam hit is effectively
mono; no synthetic widening is added to any source. All one-shot masters are
48 kHz two-channel signed 16-bit PCM with DC correction, endpoint fades, and
transparent peak normalization.

The current smooth wind-up, quiet beam, fizzle, armor-impact, and generated
level-up provider masters are preserved byte-for-byte under
`sound-lab/v5/sources/astral-relay/`. A
deterministic local safety pass applies zero-phase low-pass filtering, endpoint
fades, linked dynamics where needed, loudness fitting, and conservative peak
ceilings. The revised wind-up and beam measure 0.01% and 0.00% energy above
12 kHz after mastering; the pass also retains the fizzle correction and removes
impact and level-up inter-sample risk without synthesizing a replacement or
contacting the API. The generation record pins both the archived source and mastered output hashes,
operations, loudness, approximate true peak, and upper-band energy.
The same script audits all sixteen cues, all three ambient loops, and the exact
three sound-lab timelines against every loop choice. Review playback uses one
shared compressor followed by a fixed 3 dB safety gain; the uncompressed
sequence sums must remain at or below -1 dBTP after that fixed gain.

V5 also includes three ambience candidates: **Original dark field**, **Ion
veil**, and **Signal lattice**. SFX Engine supplied a 32-second
non-musical source for each; the workflow applies a two-second circular
equal-power tail/head crossfade to produce each 30-second looping master. The
sound lab exposes them as A/B/C alternatives and adjusts their review gains
toward A only while exact combat-overlap headroom remains safe. The original is
the steadiest technical reference; the two follow-up calls produced a wider but
still darker veil and a much quieter, sparser lattice rather than the requested
brighter spectral bridge. Owner review selected Ion veil for Tapline, but at a
near-subliminal 0.08 runtime gain rather than its 0.418 level-matched review
gain. Original dark field and Signal lattice remain review-only. None replace
or modify Neon Protocol.

The internal **projectile** cue ID is retained for runtime compatibility, but
its artifact label is **Beam resolve**. Runtime attacks now speak only as
wind-up then beam: the wind-up comes from a new public **pendingStrikes** entry
and a matching authoritative attack/traversal emits the rounded beam hit.
Fizzle, interruption, miss, and cancel remain visual-only for now; their WAVs
stay in the comparison archive. The beam is a focused field, not an audible
moving bolt. The **projectile-deflected** fact is presented as a beam bend.

Wind-up, beam, handoff, deflection, arrival, and level up are runtime mappings.
Every Core pickup, including a steal, uses the same levitation cue; drop is its
audible inverse. Level up is one compact bot-local field lock, kept below Core
scoring in the hierarchy and coalesced once per actor/tick when a bounty emits
several level facts. Exporting the V5 artifact does not touch the Neon Protocol
soundtrack. Because the masters already contain deliberate diffusion, the
runtime manifest reduces the arena convolution send to 0.06 instead of stacking
two full spaces.

V2 renders at 96 kHz before anti-aliased downsampling to 48 kHz stereo. Its
layers include separate transients, tonal bodies, modal material resonance,
debris, stereo movement, feedback-delay diffusion, and dynamics processing.
**Nilbots Signature** applies that combined language to the complete
projectile, impact, destruction, and unlock sample set. V1 remains in the
sound lab as a collapsed reference archive.

The lossless masters for every direction stay under `art/`. Astral Relay is the
approved runtime direction; `scripts/export-runtime-sound-effects.mjs` verifies
the V5 source hashes and exports thirteen mapped one-shots plus Ion veil to
`web/src/assets/audio/effects/astral-relay/` as 48 kHz stereo AAC-LC.
Fizzle, cancel, and the historical special-steal take stay review-only. The
checked-in delivery assets total roughly 0.96 MiB. The loop is selected only by
Tapline's stable `arc-relay-tapline-01` map ID. The entitlement cue remains a
sound-lab master until a real entitlement notification owns it; it is not a
match-result sound.

The export requires macOS `afconvert`. Checked-in delivery assets keep normal
builds platform-independent, while their production manifest records SFX
Engine provenance, deterministic local processing, integrity hashes, the
checked terms basis, conservative mix limits, and explicit shipment approval.
