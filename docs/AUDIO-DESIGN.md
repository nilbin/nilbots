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
node scripts/validate-audio-candidates.mjs
node scripts/build-audio-sound-lab-site.mjs
```

The soundboard at `art/audio/sound-lab/index.html` works from a static host and
supports direct A/B/C comparison, full-pack demo sequences, volume control,
keyboard shortcuts, and a browser-local favorite marker.

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
Every direction must retain identical cue coverage and order for honest A/B/C
review. New synthesis primitives must receive the cue-scoped seeded generator
instead of using `Math.random()`. Repeated combat cues should keep their dry
signal prominent; richer tails belong primarily to destruction, match, and
reward events.
