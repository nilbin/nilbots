# Audio design and asset workflow

Status: Obsidian Foundry is the approved runtime sound-effect direction.
Projectile launch, armor impact, and bot destruction cues ship in the web and
self-contained standalone replay viewers. Effects are enabled by default after
the browser's first trusted user interaction.

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
node scripts/build-audio-v5-candidates.mjs
node scripts/validate-audio-candidates.mjs
node scripts/build-audio-sound-lab-site.mjs
```

The soundboard at `art/audio/sound-lab/index.html` works from a static host and
supports direct A/B/C/D comparison, context demo sequences, volume control,
keyboard shortcuts, a browser-local favorite marker, and independent playback
of the V5 ambient bed.

### Arc Relay space / ambient reconcept (V4 review)

V4 is the current owner-review round. It replaces the tactile V3 brief with
three fully spacey, ambient directions while keeping the same nine mapped
effects and adding six replay-backed proposals:

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

The proposed wind-up, fizzle, cancel, handoff, deflection, and arrival cues
remain audition material only. V4 does not alter the approved runtime pack,
effect mappings, preferences, or Neon Protocol soundtrack.

### SFX Engine addition (V5 review)

V5 adds **Astral Relay** as direction D alongside the three V4 directions. SFX
Engine supplied all fifteen matching source cues, with prompts written around
the same space / ambient brief and the same wind-up → beam / fizzle / cancel
attack grammar. Three current variants are deterministic local derivations of
accepted Engine sources. It is an additional comparison, not a replacement or
a runtime promotion.

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
deflection; Core pickup reached its third take. The generation record retains
each rejected prompt, request, provider result, hash, and measurement under the
cue's `attempts` array.

The current no-API pass derives three cues locally through
`scripts/derive-audio-v5-revisions.mjs`. Bot destruction reverses Unit arrival
and gives its dispersal a longer exit. Core drop reverses the current Core
pickup, making acquisition and release one timbral pair. Beam deflection takes
the accepted beam body after its ignition, applies a 3.2 kHz zero-phase
low-pass, and bends it modestly from center to right. Each result pins its
source cue and SHA-256 plus every operation, carries status `derived`, and
archives the displaced provider take. These transformations add no synthesized
layer and make no provider request. The other twelve cues remain direct SFX
Engine outputs. The attack wind-up, current pickup, and derived drop are
effectively mono; technical validation requires two channels but not nonzero
stereo difference for V5.

V5 also adds the review-only **Astral arena ambience** beneath the comparison
pack. It is deliberately non-musical and has an independent sound-lab control
so effects can be judged both dry and in context. The provider's 32-second
source is converted into a 30-second loop with a two-second circular
equal-power tail/head crossfade instead of endpoint silence. Its prompt,
provider provenance, processing, hashes, and measurements live in the same V5
generation record.

V5 and its ambient bed remain review-only. They change neither the approved
Obsidian Foundry runtime package nor the Neon Protocol soundtrack.

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

This exports Obsidian Foundry's projectile, impact, and destruction masters to
`web/src/assets/audio/effects/obsidian-foundry/` as 48 kHz stereo AAC-LC. The
three delivery files total roughly 0.07 MiB. The production manifest records
the deterministic generator, cleared rights, and shipment approval; automated
tests reject any additional or unapproved runtime pack. The lossless WAVs
remain the source of truth, and all four candidate directions remain in the
sound lab for future reference.

The viewer derives projectile, impact, and destruction cues from authoritative
`Shot`, `Damage`, and `Destroyed`/`Disqualified` replay events. Seeking is
silent, playback above 2× suspends effects, and mute/volume persist locally
under production keys so old review choices cannot alter the shipped default.
The entitlement-unlock master is not reused as a match-result cue; those are
separate product events and need separately approved mappings.

Browser autoplay policy still requires a user gesture. The first trusted click
or non-modifier key resumes the viewer's shared audio session and arms enabled
music and effects together; there is no second sound-effects prompt. A compact
control exposes independent SFX mute and volume, and `?audio=off` remains an
explicit diagnostic opt-out. The CLI includes the small approved SFX package
but continues to omit the network-fetched soundtrack.

The native hosted viewer remains silent. Giving its WebView a trusted
activation through native controls requires a deliberate mobile bridge change
and is separate from approving the web/standalone pack.

## Runtime package shape

1. Keep all candidate WAV files as lossless source masters under `art/audio`.
2. Ship only Obsidian Foundry's approved compressed combat cues.
3. Keep shared gameplay cues in one rights- and approval-bearing runtime
   manifest.
4. Let a projectile-look manifest optionally reference a stable sound-profile
   ID. Projectile visuals and sounds remain independently replaceable assets;
   neither stores gameplay values.
5. Give map-theme manifests only subtle ambience references. A map selects its
   theme and ambience; the viewer does not.
6. Validate every runtime manifest reference, file format, approval, rights
   status, and package-size ceiling in automated tests; validate the lossless
   masters with the sound-lab validator.

A future manifest should use stable IDs rather than file paths in replays. The
viewer resolves IDs to local assets and falls back to the default profile when
an old replay or retired cosmetic lacks one.

## Replay scheduling

The mixer should consume changes between the previously presented tick and the
current presented tick:

- fire: an authoritative shot/projectile-created event;
- bot impact: authoritative damage to a bot;
- wall impact: authoritative projectile termination against geometry;
- destruction: transition from active to terminal bot state;
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
