# Audio design and asset workflow

Status: high-fidelity in-game review. Four complete candidate directions are
temporarily active in the web and standalone replay viewer behind an explicit
audio-enable action. No direction has been selected for the product.

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
5. Lossless candidate masters stay under `art/audio`. Normally only selected
   and compressed runtime assets enter the self-contained viewer; the
   temporary review build carries all four V2 packs for same-replay A/B tests.

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

## Temporary in-game review package

Run:

```bash
node scripts/export-runtime-audio-candidates.mjs
```

This exports every complete V2 pack to
`web/src/assets/audio/candidates/<id>/` as 48 kHz stereo AAC-LC. The 16
delivery files total roughly 0.54 MiB; the production single-file viewer
inlines them. The lossless WAVs remain the source of truth.

The viewer derives projectile, impact, and destruction cues from authoritative
`Shot`, `Damage`, and `Destroyed`/`Disqualified` replay events. Its fourth
candidate cue is played after a winning terminal tick solely to audition the
unlock sound in UI context. It is labelled `REVIEW ONLY`; the final match
result needs a distinct cue. Seeking is silent, switching packs clears active
voices, playback above 2× suspends audio, and mute/volume/candidate selection
persist locally. Browser autoplay rules require `ENABLE AUDIO`.
Enabling audio and changing packs restart a completed replay automatically so
every direction is heard against the same timeline from tick zero.

The CLI continues to receive the normal self-contained `npm run build`
artifact. Hosted review deployments use `npm run build:review`, which emits
separate hashed atlases, JavaScript, and audio files. This avoids making mobile
browsers parse the complete viewer payload as one large inline HTML document.

This review UI is intentionally absent from the native hosted viewer. It
should not cross the mobile bridge until a product direction and native
controls have been selected.

## Runtime package shape after selection

Once a direction is selected:

1. Keep the selected WAV files as lossless source masters under `art/audio`.
2. Remove the three rejected runtime candidate directories, rename the chosen
   IDs from review terminology, and measure actual decoded and transferred
   size.
3. Put shared gameplay/UI cues in one runtime audio manifest.
4. Let a projectile-look manifest optionally reference a stable sound-profile
   ID. Projectile visuals and sounds remain independently replaceable assets;
   neither stores gameplay values.
5. Give map-theme manifests only subtle ambience references. A map selects its
   theme and ambience; the viewer does not.
6. Validate every manifest reference, file format, duration, peak, and package
   size in automated tests.

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
