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
node scripts/derive-audio-v5-signatures.mjs
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
seventeen matched cues plus three optional ambient loops, an exact six-source
private signature batch, and an exact seven-source V2 signature-audio addendum.
The derivation scripts deterministically rebuild three current variants and all
nineteen signature deliveries from their accepted source masters without
contacting SFX Engine. The safety-master script then rebuilds five high-energy
provider cues from archived, hash-pinned pre-master WAVs without spending
credits.

The build command creates the static worker package used for the owner-only
review site under the ignored `sandbox/audio-sound-lab-site` directory. It
reuses `.openai/hosting.json`; never create a second site for this review lab.
It also copies the approved AAC package into `runtime-audio`, allowing
`sound-lab/signatures-v2.html` to audition the exact shipped files without
duplicating them in the lossless source tree.

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
  original sixteen-cue vocabulary plus the post-promotion Healing field.

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

SFX Engine returned WAV sources for the original sixteen cues and two later
private generations for the shared ground-pad/medic Healing field; the first
healing take remains archived after owner review found that it did not read as
healing. Successive
owner reviews requested provider follow-up takes for armor impact, bot destruction, Core
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
labels all three as `local-derivation`; the other fourteen remain
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
contacting the API. The generation record pins both the archived source and
mastered output hashes, operations, loudness, approximate true peak, and
upper-band energy.
The same script audits all seventeen cues, all three ambient loops, and the exact
three sound-lab timelines against every loop choice. Review playback uses one
shared compressor followed by a fixed 3 dB safety gain; the uncompressed
sequence sums must remain at or below -1 dBTP after that fixed gain.

The signature package records are
`sound-lab/v5/signature-sfx-engine-generation.json` and
`sound-lab/v5/signature-v2-sfx-engine-generation.json`. The first pins exactly
six private provider jobs for nine deliveries; the second pins exactly seven
private jobs for ten further deliveries. Neither batch permits rerolls or
ambience. Run `scripts/derive-audio-v5-signatures.mjs` to reproduce all nineteen
short, dry, dual-mono runtime deliveries. It verifies every source hash and
applies only channel folding, resampling, low-pass filtering, endpoint fades,
and peak fitting. Every signature delivery is at most 700 ms, uses 0.16 runtime
gain, and adds no procedural or synthesized layer. The complete class/event
inventory and V1 compatibility policy live in `docs/AUDIO-DESIGN.md`.

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
gain during an earlier round. All three now remain sound-lab comparisons only:
Tapline selects no ambience loop and the runtime exporter ships none. None
replace or modify Neon Protocol.

The internal **projectile** cue ID is retained for runtime compatibility.
Runtime attacks now speak as wind-up then neutral field release: the wind-up
comes from a public **pendingStrikes** entry, and a matching authoritative
attack/traversal emits the release while WebGL carries the equipped projectile
look over the recorded line. A matured fizzle, interruption, or miss emits that
same release; only a real damage fact adds the armor-impact voice. Voluntary
cancel remains silent. The unique fizzle/cancel WAVs stay in the comparison
archive, and **projectile-deflected** retains its separate spectral bend.

Wind-up, projectile release, healing, handoff, deflection, arrival, and level up
are runtime mappings. `zone-healed` keeps the shared arena-healing cue;
`signature-repair` uses Patchbay's distinct braided transfer under signature
package V2, while historical V1 replays retain the shared mapping.
Every Core pickup, including a steal, uses the same levitation cue; drop is its
audible inverse. Level up is one compact bot-local field lock, kept below Core
scoring in the hierarchy and coalesced once per actor/tick when a bounty emits
several level facts. Exporting the V5 artifact does not touch the Neon Protocol
soundtrack. Because the masters already contain deliberate diffusion, the
runtime manifest reduces the arena convolution send to 0.06 instead of stacking
two full spaces.

The runtime exporter also applies a deterministic comfort treatment to every
frequent or spectrally sharp delivery: impact, healing, destruction, level up,
Core bank, Core Pulse, handoff, and arrival, in addition to wind-up and projectile
release. It selects only hash-pinned accepted sources and uses deterministic
resampling, low-pass filtering, endpoint fades, and conservative peak
normalization, then audits the decoded AAC that the browser actually hears.
Across all 33 one-shots, decoded energy is at most 3% above 4 kHz and
0.05% above 12 kHz, effective peaks stay at or below -9 dBFS, effective
whole-cue RMS stays at or below -25 dBFS, and any window with more than 10%
upper-mid energy must sit below -28 dBFS RMS after runtime gain. Delivery
processing makes no provider request and adds no synthesized layer. Signature
deliveries pass the tighter 1 kHz centroid, 0.5% energy-above-4-kHz,
-34 dBFS effective-RMS, and -22 dBFS effective-peak ceilings.

Non-projectile categories still coalesce once per tick. Projectile/fizzle
releases retain up to four positional voices—the observed Tapline maximum—and
start 0.04 presentation ticks apart so identical buffers do not phase-lock.
The projectile's four-voice limit and very low individual delivery keep even a
theoretical perfectly aligned stack below -15 dBFS. Signature delivery permits
at most two distinct voices per tick. The decoded 513-variant crowd audit adds
each of 171 distinct signature pairs to four real-staggered projectiles, impact,
destruction, level up, and one Core pickup/drop/handoff cue. Its worst dry
centered sum is -7.43 dBTP
against a -6 dBTP ceiling before limiter or room attenuation.

The shared healing tile is an 800 ms low-mid field seal. It pans from the healed
target with a 0.55 edge width, allows at most two overlapping tails, and
coalesces multiple same-tick heals like other non-projectile categories. Its
decoded runtime asset has a 220 Hz dominant frequency and 431 Hz spectral
centroid, carries 0.44% energy above 4 kHz and 0% above 12 kHz, and remains at
-19.56 dBFS effective peak / -33.97 dBFS effective RMS. Patchbay's separate
signature repair remains shorter, drier, and inside the stricter signature
limits.

V2 renders at 96 kHz before anti-aliased downsampling to 48 kHz stereo. Its
layers include separate transients, tonal bodies, modal material resonance,
debris, stereo movement, feedback-delay diffusion, and dynamics processing.
**Nilbots Signature** applies that combined language to the complete
projectile, impact, destruction, and unlock sample set. V1 remains in the
sound lab as a collapsed reference archive.

The lossless masters for every direction stay under `art/`. Astral Relay is the
approved runtime direction; `scripts/export-runtime-sound-effects.mjs` verifies
the V5 source hashes and exports 33 mapped one-shots to
`web/src/assets/audio/effects/astral-relay/` as 48 kHz stereo AAC-LC.
The unique fizzle, cancel, and historical special-steal takes stay review-only;
runtime fizzle aliases the shipped projectile release. The checked-in delivery
assets total 498,979 bytes. No ambience file, mapping, fetch, or playback
path ships. The entitlement cue remains a sound-lab master until a real
entitlement notification owns it; it is not a match-result sound.

The export requires macOS `afconvert`. Checked-in delivery assets keep normal
builds platform-independent, while their production manifest records SFX
Engine provenance, deterministic local processing, integrity hashes, the
checked terms basis, conservative mix limits, and explicit shipment approval.
