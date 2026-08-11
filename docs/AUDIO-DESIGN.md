# Audio design and asset workflow

Status: Astral Relay is the approved default runtime sound-effect direction.
Thirteen one-shot voices ship in the web and self-contained replay viewers;
no ambience loop ships or plays. Effects are enabled by default after the
browser's first trusted user interaction. Neon Protocol music is unchanged.

## Product principles

1. Audio communicates an event already authorized for presentation. It never
   reveals a future tick, concealed result, invisible private observation, or
   server-side state unavailable to the current viewer.
2. Audio is presentation only. A projectile sound may follow its cosmetic
   projectile look, but it cannot affect trajectory, damage, timing,
   collision, observations, matchmaking, or ratings.
3. Repeated combat cues remain short and sparse. Match and reward cues may be
   longer, but ranked sets must not become ten minutes of repeated fanfare.
4. The replay is the schedule. Playback derives audio from authoritative tick
   transitions and replay events, not from canvas animation timing.
5. Lossless candidate masters stay under `art/audio`. Only approved,
   compressed runtime assets enter the viewer.

## Candidate sets

`scripts/generate-audio-candidates.mjs` synthesizes three directions with the
same ten review cues:

- Vector Tactical: precise transients and controlled tails.
- Foundry Signal: relays, metal resonances, pneumatics, and heavier low mids.
- Neon Circuit: FM-like motion, glassy harmonics, and more melody.

The source is deterministic and uses no sampled or third-party material.
Outputs are signed 16-bit, 44.1 kHz mono WAV masters. These are review masters,
not the eventual delivery format.

The initial set established feature coverage but sounded too narrow, dry, and
oscillator-forward. It remains available as the V1 reference archive.

`scripts/generate-audio-v2-candidates.mjs` goes deeper on four representative
moments before the full set is expanded:

- signature projectile;
- armor impact;
- bot destruction;
- entitlement unlock.

Its four directions are Aegis Systems, Obsidian Foundry, Aurora Core, and
Nilbots Signature. The fourth is a complete combined direction, not a
one-cue experiment: every showcase moment uses Aegis's clean confirmation,
Obsidian's physical mass, and Aurora's energy and stereo opening in different
proportions.

V2 renders deterministically at 96 kHz, uses a windowed low-pass stage while
downsampling, and exports 48 kHz stereo PCM review masters. Each cue separates
transient, body, material resonance, debris, and spatial tail. Stereo
diffusion, DC filtering, linked dynamics, peak normalization, endpoint fades,
and deterministic TPDF dither are part of the shared mastering path.

Regenerate and validate:

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
```

The soundboard at `art/audio/sound-lab/index.html` works from a static host and
supports direct A/B/C/D comparison, context demo sequences, volume control,
keyboard shortcuts, a browser-local favorite marker, and independent playback
of the three V5 ambient alternatives.

### Arc Relay space / ambient reconcept (V4 archive)

V4 was the procedural owner-review round and remains a comparison archive. It
replaced the tactile V3 brief with three fully spacey, ambient directions while
keeping the same nine mapped effects and adding seven replay-backed proposals:

- **Halo Drift** — luminous air, weightless orbital motion, and the widest
  diffusion.
- **Deep Array** — controlled sub pressure, eclipsed harmonics, and distant
  deep-space signal.
- **Solar Bloom** — warm plasma motion, iridescent harmonics, and the brightest
  objective hierarchy.

The runtime cue ID remains **projectile** for compatibility, but V4 presents it
as the **beam resolve** inside a larger attack sentence. A new entry in public
**pendingStrikes** starts the wind-up. At resolution, a matching attack
traversal produces the focused beam; a strike that reaches its resolve tick
without one gets the fizzle / interrupt / miss cue; a strike that disappears
before its resolve tick gets the shorter cancel cue. This vocabulary follows
authoritative replay state and traversal facts rather than camera timing.
Nothing audibly travels like a bolt.

The proposed deflection follows the same rule; its underlying replay fact is
still named **projectile-deflected**, while its reviewed sound is a beam bending
rather than a ricochet.

All three directions use smooth spectral glides, dynamically filtered air,
seeded stereo micro-motion, damped diffusion, and linked mastering. V4 renders
at 96 kHz before anti-aliased downsampling to 48 kHz stereo PCM. Frequent
combat cues receive less space than objective events; Core birth, bank, and
Pulse are allowed to bloom. If a direction is promoted, retune the viewer's
existing arena convolution so the intentional master tails are not stacked
with a second full room.

The proposed wind-up, fizzle, cancel, handoff, deflection, arrival, and level-up cues
remain audition material only. V4 does not alter the approved runtime pack,
effect mappings, preferences, or Neon Protocol soundtrack.

### SFX Engine addition (V5 source and promoted runtime)

V5 adds **Astral Relay** as direction D alongside the three V4 directions. SFX
Engine supplied all sixteen matching source cues, with prompts written around
the same space / ambient brief and the same wind-up → beam / fizzle / cancel
attack grammar. Three current variants are deterministic local derivations of
accepted Engine sources. Following owner comparison and revision, Astral Relay
is the promoted runtime direction; the other V4/V5 candidates remain review
references.

The reusable workflow is `.claude/skills/sfx-engine/SKILL.md`, and its saved
OpenAPI 3.0 contract is
`.claude/skills/sfx-engine/references/openapi.yaml`. Generation reads the API
key from `SFXENGINE_API_KEY` or macOS Keychain service
`com.nilbots.sfxengine.api-key`; no credential is stored in the repository.
Every request is private. The committed generation record preserves prompts,
provider IDs and timestamps, request lengths, download types, source/output
hashes, local operations, and measurements while deliberately omitting returned
URLs and user IDs.

Provider output is decoded to 48 kHz stereo signed 16-bit PCM, DC-corrected,
endpoint-faded, and peak-normalized; no procedural layers are added. Successive
owner reviews requested targeted provider follow-up takes for armor impact, bot
destruction, Core pickup, Core stolen, ally handoff, fizzle, and beam
deflection. Core pickup reached a fourth take explicitly shaped as levitation;
wind-up and beam each received a smoother low-noise replacement. The generation
record retains each rejected prompt, request, provider result, hash, and
measurement under the cue's `attempts` array.

The current no-API pass derives three cues locally through
`scripts/derive-audio-v5-revisions.mjs`. Bot destruction reverses Unit arrival
and gives its dispersal a longer exit. Core drop reverses the current Core
pickup, making levitation and release one timbral pair. Beam deflection takes
the rounded hit from the accepted quiet beam, applies a 3.2 kHz zero-phase
low-pass, and bends it modestly from center to right. Each result pins its
source cue and SHA-256 plus every operation, carries status `derived`, and
archives the displaced provider take. These transformations add no synthesized
layer and make no provider request. The other thirteen remain SFX
Engine-sourced rather than locally derived. The quiet beam hit is effectively
mono; technical validation requires two channels but not nonzero stereo
difference for V5.

V5 also adds three ambience candidates beneath the comparison pack: **Original
dark field**, **Ion veil**, and **Signal lattice**. They are
deliberately non-musical and share an independent A/B/C sound-lab control so
effects can be judged both dry and in context. Each provider 32-second source
is converted into a 30-second loop with a two-second circular equal-power
tail/head crossfade instead of endpoint silence. The selector adjusts each
loop toward the original's review loudness but caps gain where the exact combat
sum would lose the pre-limiter safety margin. Their prompts, provider
provenance, processing, hashes, and measurements live in the same V5 generation
record.

The original remains the steadiest technical reference and leaves the least
foreground motion. Two additional private Engine calls tested a brighter ion
veil and a sparse signal lattice. The first rendered wider but darker than
requested; the second rendered substantially quieter and sparser. Owner review
briefly selected Ion veil for Tapline at 0.08 runtime gain, then removed the
ambient bed entirely. Original dark field, Ion veil, and Signal lattice are now
review-only alternatives. Neon Protocol is not regenerated, remixed, or
otherwise changed.

An energy audit originally found excessive upper-band energy in wind-up and
fizzle, while armor impact reconstructed above full scale and level-up left
only 0.52 dB of inter-sample headroom. The smooth replacement wind-up and quiet
beam are now safety-mastered to 0.01% and 0.00% measured energy above 12 kHz.
Their original standardized provider masters are retained under
`art/audio/sound-lab/v5/sources/astral-relay/` and pinned by
hash. `scripts/master-audio-v5-energy.mjs` deterministically filters,
dynamics-controls, and attenuates the five current delivery masters, records
BS.1770-style loudness, approximate four-times-oversampled true peak, and
upper-band energy, and makes no provider request. Re-run it after the local
derivation script and before the V5 manifest builder.

A forced provider replacement that feeds a derivation is the exception: master
the new provider cue once, rebuild its dependent derivation, then run the master
and audit again before building the manifest. This prevents a dependent cue
from retaining the noisier pre-master source. Ordinary checked-in rebuilds use
the documented derive, master, build order.

The sound lab routes every cue and each optional ambience comparison through one
shared Web Audio compressor and then a fixed 3 dB safety gain. The deterministic
audit checks all nineteen V5 WAVs and every ambience variant of the exact
possession, combat, and full-story sums; each sequence must remain at or below
-1 dBTP after the fixed gain even before compressor attenuation is credited.

Level up maps to the authoritative `leveled-up` Arc Relay fact. It is a
bot-local veterancy reward, not a Core or team-score event, so its cue is a
compact centered field rise that locks beneath the Core hierarchy and remains
legible when it coincides with the victim's destruction tail. If one bounty
emits several levels for the same actor on one tick, the runtime scheduler
coalesces them into one cue at the highest reached level.

### Arc Relay tactile reconcept (V3 archive)

The previous complete-vocabulary round remains available for provenance and
comparison:

- **Servo League** — compact motors, polymer armor, ceramic pucks, and positive
  relay latches; the robot-sport interpretation.
- **Signal Grid** — phase-coded packets, restrained FM motion, and glass-alloy
  transients; the submitted-machine-intelligence interpretation.
- **Field Resonance** — spring steel, composite knocks, hollow vessels, and
  charged air; the material arena-world interpretation.

The mapped set is attack, impact, destruction, Core birth, pickup, steal,
drop, bank, and Pulse. The proposed set is ally handoff
(`core-handed-off`), projectile deflection (`projectile-deflected`), and unit
arrival (`life-spawned` / historical respawn and fabrication aliases). Those
proposals are audition material only. V3 did not alter the approved runtime
pack, effect mappings, preferences, or Neon Protocol soundtrack.

V3 renders deterministically at 96 kHz and downsamples to 48 kHz stereo PCM.
The comparison artifact states the game brief, keeps identical cue order and
timing across themes, offers possession/combat context sequences, and records
a local favorite without turning that preference into product state.

## Approved runtime package

Run:

```bash
node scripts/export-runtime-sound-effects.mjs
```

This verifies the hash-pinned V5 sources and exports Astral Relay to
`web/src/assets/audio/effects/astral-relay/` as 48 kHz stereo AAC-LC: thirteen
mapped one-shots at 192 kbps, roughly 0.31 MiB together. The unique fizzle,
cancel, and historical special-steal takes remain lossless review references;
runtime fizzle aliases the projectile release instead of shipping a fourteenth
voice. The production manifest records the SFX Engine and local derivation
lineage, source/output hashes, checked licensing basis, shipment approval, and
per-cue gain/voice limits.
Automated tests reject any additional pack, missing cue, altered byte, unsafe
mix bound, or untraceable generation record. Lossless WAVs remain the source
of truth in the sound lab. The exporter also zeros the three standard M4A
creation/modification timestamps, making repeated exports byte-identical.

Delivery processing now covers wind-up, projectile, impact, destruction, level
up, Core bank, Core Pulse, handoff, and arrival. It preserves accepted sources
and uses only deterministic resampling, low-pass filtering, endpoint fades, and
conservative peak normalization—no SFX Engine request and no synthesized
layer. The export then decodes every AAC and audits what the browser will hear.
Every one-shot must remain at or below 3% energy above 4 kHz, 0.05% above 12
kHz, -9 dBFS effective sample peak, and -25 dBFS whole-cue RMS. A window with
more than 10% upper-mid energy must also fall below -28 dBFS RMS after its
runtime gain. The destruction delivery is cut past its source's 550 ms
near-silent pre-roll, compressed to 700 ms, and scheduled at the renderers'
exact 0.55 collapse boundary. It measures 0% above both 4 kHz and 12 kHz after
AAC decode; the untouched arrival remains inside the same pack-wide ceilings.

The viewer derives projectile launch, impact, destruction, deflection, arrival,
veterancy, and every Core possession/scoring cue from authoritative replay
facts. A new pending strike emits the smooth wind-up; in WebGL, a matching
resolution emits a quiet neutral field release. In WebGL, that same resolution
also shows the equipped projectile along the already-recorded traversal;
Canvas retains its prior visual fallback. A matured fizzle, interruption, or
miss emits the same release, while a concrete damage fact adds the separate
armor-impact voice and an early voluntary cancellation remains silent. The
underlying instant-ray rule, hit timing, damage, and replay facts do not change.
Every `core-picked-up` fact uses the same levitation cue even when ownership
changes teams; drop is its derived inverse. Seeking is silent, playback above
2× suspends audio, and mute/volume persist locally under production keys. The
entitlement-unlock master is not reused as a match-result cue.

Non-projectile categories continue to coalesce once per tick. The scheduler
retains up to four distinct projectile/fizzle releases, matching the maximum
observed simultaneous Tapline presentation, and separates their starts by 0.04
presentation ticks (16 ms at normal speed). Each keeps its own authoritative
pan. The four-voice hard limit bounds future formats, and even a theoretical
phase-aligned stack remains below -15 dBFS before the shared limiter.

Browser autoplay policy still requires a user gesture. The first trusted click
or non-modifier key resumes the viewer's shared audio session and arms enabled
music and effects together; there is no second sound-effects prompt. A compact
control exposes independent SFX mute and volume, and `?audio=off` remains an
explicit diagnostic opt-out. There is no ambient source to preload or loop.
The CLI includes this approved one-shot package but continues to omit the
network-fetched soundtrack.

The native hosted viewer remains silent. Giving its WebView a trusted
activation through native controls requires a deliberate mobile bridge change
and is separate from approving the web/standalone pack.

## Runtime package shape

1. Keep all candidate WAV files as lossless source masters under `art/audio`.
2. Ship only Astral Relay's thirteen approved compressed one-shots; ambience
   candidates remain sound-lab references.
3. Keep shared gameplay cues in one rights- and approval-bearing runtime
   manifest.
4. Let a projectile-look manifest optionally reference a stable sound-profile
   ID. Projectile visuals and sounds remain independently replaceable assets;
   neither stores gameplay values.
5. Keep ambience opt-in at the package level if it returns; no current map or
   viewer selects a loop.
6. Validate every runtime manifest reference, file format, approval, rights
   status, and package-size ceiling in automated tests; validate the lossless
   masters with the sound-lab validator.

A future manifest should use stable IDs rather than file paths in replays. The
viewer resolves IDs to local assets and falls back to the default profile when
an old replay or retired cosmetic lacks one.

## Replay scheduling

The mixer should consume changes between the previously presented tick and the
current presented tick:

- attack declare: a new authoritative pending strike;
- projectile presentation: an authoritative attack event/traversal;
- matured fizzle, interruption, or miss: alias the projectile release;
- voluntary cancel before the resolve tick: silent;
- bot impact: authoritative damage to a bot;
- projectile deflection: authoritative `projectile-deflected` event;
- wall impact: authoritative projectile termination against geometry;
- destruction: transition from active to terminal bot state;
- arrival: authoritative life spawn, respawn, or fabrication;
- level up: highest authoritative `leveled-up` fact per actor and tick;
- Core lifecycle: authoritative birth, pickup (including steal), handoff, drop,
  bank, and Pulse facts; all pickups share one levitation voice;
- zone shift: a meaningful control-owner transition, not every pressure tick;
- countdown/start: presentation timeline, before tick zero;
- match result: only after the public presentation reaches the terminal tick;
- entitlement unlock: durable user notification, outside match simulation.

Seeking does not replay every skipped sound. A direct seek resets mixer state
silently; normal playback emits each crossed event once. Restarting a replay
re-arms the schedule. Live viewing uses the same presentation cursor and never
runs ahead of received ticks.

## Mixer constraints

- A global mute and volume setting must persist locally.
- Cap simultaneous voices by category and globally.
- Prefer the newest high-priority combat event when a cap is reached.
- Use small deterministic pitch/gain variations keyed by replay identity and
  event index only after the base assets are approved.
- Scale scheduling with playback speed; mute or simplify long tonal cues at
  very high speeds.
- Suspend audio when the page is hidden and resume without backfilling sounds.
- Respect browser autoplay rules: audio starts only after user interaction.
- Keep accessibility visual feedback complete; no essential information may
  exist only in sound.

## Creating another direction or cue

For V2, add cue metadata once in `generate-audio-v2-candidates.mjs`, implement
the same cue ID in every candidate renderer, regenerate, and run the validator.
Every direction must retain identical cue coverage and order for honest A/B/C/D
review. New synthesis primitives must receive the cue-scoped seeded generator
instead of using `Math.random()`. Repeated combat cues should keep their dry
signal prominent; richer tails belong primarily to destruction, match, and
reward events.
