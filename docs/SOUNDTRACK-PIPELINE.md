# Adaptive soundtrack pipeline

`scripts/compile_soundtrack.py` turns a ZIP of sample-aligned PCM WAV stems
into a content-aware, versioned AAC score graph under
`web/public/soundtracks/`. The source ZIP is production input, not a deployable
asset. Each soundtrack has a reviewed JSON config in `soundtracks/`.

## Build Neon Protocol

From the repository root:

```bash
# ZIP/config/PCM/analysis/graph checks; no encoder or public files required
python3 scripts/compile_soundtrack.py \
  soundtracks/neon-protocol.json --validate-only \
  --analysis-out /tmp/neon-protocol-analysis.json

# Also check which encoder would be used, without writing assets
python3 scripts/compile_soundtrack.py \
  soundtracks/neon-protocol.json --dry-run

# Build the content-addressed version and atomically update index.json
python3 scripts/compile_soundtrack.py soundtracks/neon-protocol.json
```

The compiler prefers `ffmpeg` and falls back to macOS `afconvert`. An encoder
can be selected explicitly with `--encoder ffmpeg` or
`--encoder afconvert`. With no config arguments it builds every
`soundtracks/*.json` config, so adding another soundtrack does not require a
pipeline change.

The checked-in Neon Protocol config expects
`Neon Protocol Stems (120BPM).zip` at the repository root. ZIP archives are
local compiler inputs and are excluded from both Git and Docker contexts,
regardless of filename or directory. Use `--archive PATH` to validate an
equivalent archive elsewhere without copying it into the checkout.

The WAV metadata identifies Suno Studio as the source tool. The config and
manifest deliberately record `rightsStatus: "user-supplied-unverified"` and
`shipApproval: "pending"`. Generated Neon Protocol assets are for local and
in-game audition only until the owner confirms the applicable rights and
changes both fields to their approved values. Building does not imply
permission to deploy or publish them. The canonical release workflow enforces
that boundary with `scripts/assert_soundtrack_release.py`: production image
publication is blocked until every public pack version is rights-cleared,
ship-approved, all authored loops are marked `auditioned`, and every encoded
media file is declared by a manifest.

Pipeline regression tests are dependency-free:

```bash
python3 -m unittest discover -s scripts/tests -p 'test_*.py' -v
```

## Trust boundary

Stem archives are treated as untrusted. Before any member is read, the
compiler:

- rejects absolute paths, `..`, backslashes, NULs, encryption, symlinks,
  special files, duplicate/case-colliding names, oversized members, excessive
  expansion, and suspicious compression ratios;
- requires an exact one-to-one match between ZIP files and configured stem
  mappings;
- copies each accepted member to a compiler-owned temporary filename instead
  of using ZIP extraction paths;
- reads every PCM payload and verifies CRC/declared size; and
- requires every stem to have exactly the same frame count, sample rate,
  channel count, and PCM sample width.

The config pins `gridOriginFrame`, `barFrames`, and `sourceEndFrame`. This is
important: DAW exports can contain preroll, so frame zero is not necessarily a
musical downbeat. The timing must also agree exactly with BPM, meter, and
sample rate. A mismatch is an error, not a rounded edit point.

## Analysis and authoring

The first pass measures every 50 ms window, bar, and four-bar phrase. The
detailed `analysis.json` reports RMS, peak, active-window ratio, a
dependency-free transient score, normalized energy, audible stem count,
silence trimming, phrase classifications, boundary candidates, source seam
similarity, transition compatibility, and the final encoded-file checks.

The six runtime classifications are:

`sparse | tension | pursuit | combat | climax | resolve`

Every section also declares its playback role:

- `hold` is a reviewed loop where a gameplay state can settle. Optional
  `repeat.minimumBars` records the full-cycle dwell before same-state variety.
- `bridge` is a finite connective phrase.
- `stinger` is a finite accent selected only by an explicit trigger, never as
  an ordinary adaptive-routing shortcut. Optional `cooldownSeconds` records
  how long it must remain disarmed after use.
- `resolve` is a finite ending and must use the `resolve` classification.

Roles and loop treatment must agree exactly: holds loop, all other roles are
finite, and the entry section is a hold. Neon Protocol sets every hold's
minimum repeat to eight bars and gives `lift-sting` a 32-second cooldown.

Automatic suggestions are intentionally non-looping. A production build
requires analysis-reviewed `sections`, `entrySection`, and `transitions` in
the config. This keeps harmonic phrase selection and narrative intent
human-authored while making timing and audio safety machine-verifiable. A
section may omit a stem when both its section RMS and peak are below the
configured thresholds. Stem files that are present remain sample-aligned.

Loopability is never inferred from a bar boundary. A loop needs a
`loop.approval` and the `rendered-head-crossfade` strategy. Use
`analysis-reviewed` for a provisional compiler-selected edit and reserve
`auditioned` for one a person has actually heard. For each included stem, the
compiler leaves the section tail untouched and replaces the first 62.5 ms
with a blend from the source audio immediately after the section tail into
the original section head. The first rendered sample therefore follows the
last rendered sample naturally, while the blend returns to the original loop
material without changing section duration, the musical grid, or relative
stem timing. A loop is rejected when the source has insufficient continuation
audio.

An optional `retrospectiveCue` gives completed replays one continuous authored
runway instead of assembling a climax from graph jumps. It declares a slug
`id`, an integral `startBar`/`barCount` source range, a zero-based
cue-relative `anchorBar`, and a non-empty set of known stem ids. The anchor is
the musical landmark that the whole-replay planner aligns to its primary
highlight. Every selected stem is extracted across the entire range and
encoded once without loop-head rewriting or internal section boundaries. The
manifest adds exact duration and per-stem file paths; the analysis output and
asset inventory cover the same files. Neon Protocol's `final-runway` spans
source bars 72–96 and anchors source bar 88.

An optional `straightThroughCue` is the non-adaptive control path used to
audition the authored score without runtime graph transitions. Its config
declares a slug `id`, an integral `startBar`/`barCount` source range, and a
non-empty set of known stem ids. The compiler sums those sample-aligned stems
into one continuous mix using each stem's global `gainDb`, checks that the
integer PCM does not clip, and verifies peak headroom after the runtime
`masterGainDb`. It deliberately does not bake the pack master into the file.
The manifest and analysis report expose only `id`, `startBar`, `barCount`,
`durationSeconds`, and the single `file`; the source stem selection remains a
compiler concern. Neon Protocol's `opening-passage` spans source bars 0–24 and
uses Drums, Bass, Guitar, Synth, and Other. Its effectively silent Percussion
stem is omitted.

For a finalized replay, the runtime maps the relatively ranked primary
highlight to that anchor and starts the cue on the next source beat. It reads
the replay clock again after download/decode, so network latency does not move
the musical landmark. While the runway is active, gameplay changes automate
stems and event accents but cannot request horizontal section jumps. Pause,
resume, and seek rebuild the cue at the replay-owned source offset; resolution
holds the landed peak briefly and fades the same continuous cue instead of
splicing to an unrelated outro. If a match's primary highlight lies beyond the
authored runway, playback deliberately falls back to the ordinary adaptive
graph rather than starting the cue too early.

The compiler uses an equal-power blend when its summed pack peak, including
stem/section gains and `masterGainDb`, retains the configured headroom. If
equal-power would exceed that ceiling, it reports and uses a linear blend
instead; if neither curve is safe, the build fails. It then measures every
rendered section at full stem response with its authored `stemGainsDb`, plus
every runtime-reachable equal-power overlap at every eligible quantized cut
point. Any peak above `analysis.targetPeakDbfs` fails validation and the build;
the runtime limiter is not used to excuse an unsafe pack. It also verifies the
natural source adjacency sample-for-sample. The manifest records
`rendered: true`, `approvalStatus`, `auditionRequired`, `crossfadeFrames`,
`continuationFrames`, `curve`, `seamJumpDbfs`, `packHeadPeakDbfs`,
`headroomTreatment`, rendered `boundarySimilarity`, and the unmodified
`sourceBoundarySimilarity`. These checks establish timing and signal safety,
not perceptual seamlessness. Neon Protocol remains `analysis-reviewed` and
must be auditioned in game before its loops are called final.

Natural, contiguous source edits use `timing: "section-end"`. Reviewed
adaptive cuts use `timing: "next-quantum"`, normally with a one-bar quantum and
an equal-power transition crossfade. Every directed edge is compatibility
scored. For `next-quantum` edges the compiler evaluates every eligible cut
point in the source section, records the range, and validates the worst case.
Neon Protocol retains a full-bar (two-second) authored overlap as the
headroom-checked legacy ceiling for these adaptive cuts. Its staged runtime
policy below replaces that exposed full-mix overlap with a longer
retreat/handoff/rise gesture. A bare quarter-bar cut was too abrupt; the same
short bus handoff is useful only when surrounded by the stem retreat and rise.

`adaptiveSeam` describes the pack's fallback for discontinuities that a
continuous runway cannot avoid. The supported policy is `strategy: "staged"`
with a linear curve: responsive layers retreat over `retreatBars`, the section
buses use only the short `overlapBars` crossover, and destination layers
return over `riseBars`. The compiler validates and passes this policy to both
manifest and analysis output. It does not bake the staging into source audio;
the runtime applies it consistently across stem roles.

Same-state rotation edges out of loopable holds are also `next-quantum`: the
runtime applies the hold's `repeat.minimumBars` dwell and starts the
destination on an exact full-cycle boundary, where the old loop has wrapped
to its rendered head. This lets the compiler include the actual
old-head-to-new-head overlap in its compatibility range. Same-state returns
from finite cues remain `section-end`. A reviewed hold's rendered loop is
already executable continuity when no rotation is authored. Finite cues must
instead have a non-stinger same-state successor; trigger-gated stingers never
count as ordinary continuation.

`adaptiveLatencyBudgetBars` makes responsiveness a pack-level contract. Its
defaults are two bars for gameplay-state changes and one bar for resolution.
Gameplay routes are proven from every hold to a hold in each other gameplay
classification; resolution is proven from every non-resolve section. Ordinary
routes exclude stingers, which require their separate trigger gate.

The compiler chooses the minimum-latency path with Dijkstra's algorithm. Every
edge has a conservative worst-case wait: `next-quantum` costs
`quantizeBars`; `section-end` costs the source's full `barCount`, whether the
source is finite or held. `repeat.minimumBars` constrains only same-state
rotation; gameplay and resolve exits remain responsive and do not wait out
that dwell. Route cost is the sum. All costs are positive, so a cycle cannot
fake reachability. This permits a safe two-hop bridge to beat a slower
one-hop section-end edit. The chosen path, hop count, and worst-case bars are
written to `analysis.json`; a build fails when no non-stinger route exists or
the cheapest guaranteed route exceeds its budget.

Edges below the configured floor fail unless the config records a deliberate
low-similarity exception. The graph must have no dead ends, and all nodes must
be reachable from the entry.

## Loudness and artistic balance

The compiler does not normalize stems independently. Configured stem gains are
playback metadata, and the encoded PCM keeps the source-stem relationship.
Analysis measures the combined pack mix, every selected section after
section-specific gains and rendered loop treatment, and every authored
transition overlap. All must retain the configured peak headroom after the
single `masterGainDb`. Rendered loop envelopes are identical across a
section's stems.

Neon Protocol uses a `-3 dB` pack master. Its effectively silent Percussion
export is retained in the stem descriptor for future source revisions but
omitted from the current section assets. The sparse Other stem is emitted only
for the phrases containing its stinger.

## Outputs and atomicity

The public layout is:

```text
web/public/soundtracks/
  index.json
  <track-id>/
    v<schema>-<content-hash>/
      manifest.json
      analysis.json
      sections/<section-id>/<stem-id>.m4a
      retrospective-cues/<cue-id>/<stem-id>.m4a
      straight-through-cues/<cue-id>/mix.m4a
```

Each manifest includes the timing origin, adaptive stem response metadata,
section roles and repeat/cooldown policy, latency budgets, section energy and
optional loop, adaptive-seam, retrospective-cue, and straight-through-cue
metadata, the directed transition graph, and an `assets` map with SHA-256 and
byte size for every M4A and the analysis report. AAC outputs are checked as M4A
containers and, when `ffprobe` or `afinfo` is available, verified for codec,
duration, channel count, and sample rate.
The runtime rejects malformed build provenance, a pipeline/version prefix
mismatch, an analysis report missing from the asset inventory, or a manifest
whose content-version directory disagrees with `build.version`. Repository
tests hash and size-check every declared asset, including `analysis.json`.

These packs deliberately live in `web/public/`, not `web/src/assets/`.
Vite copies them into the network-served `dist/` tree without importing the
audio into JavaScript; the version directory is already content-addressed for
immutable caching. The CLI Vite build sets `publicDir: false` and stubs the
lazy score component, so self-contained `dist-cli/<theme>/` viewers contain
neither packs nor soundtrack runtime. Website playback fetches the catalog,
manifest, and needed stems on demand after a user enables music, while the
standalone CLI viewer remains independent of network-only soundtrack content.
The mutable catalog is served with `no-cache`; files below a validated
content-version directory are one-year immutable, and missing
`/soundtracks/**` requests return 404 instead of the SPA shell.

Compilation happens in a staging directory. The complete version directory is
renamed into place atomically, then `index.json` is replaced atomically. The
catalog is merged by track id and sorted, so independently rebuilding one
soundtrack preserves other installed packs. A version id covers the source
archive, canonical config, encoder identity, output hashes, and analysis
report. The compiler zeroes only the standard non-semantic MP4
creation/modification timestamps before hashing, avoiding version churn from
container clocks. The version still covers the actual encoded bytes: a lossy
encoder that emits a different valid bitstream produces a different content
version rather than overwriting an existing one. Prefer `ffmpeg` when
reproducible encoded bytes are required; macOS `afconvert` is a compatibility
fallback and can vary between invocations.

## Runtime playback policy

The score keeps its authored tempo when replay playback is set anywhere from
0.25× to 4×. Gameplay speed changes how quickly director frames arrive, not
the Web Audio source rate. Horizontal gameplay states therefore coalesce to
the latest request and commit at most once per audio bar; the terminal
`resolve` state bypasses that latch so its still-quantized transition can meet
the one-bar resolution budget.

The `score=straight` A/B control is intentionally stricter: it is audible only
at 1×, where source time can remain identical to replay time without
pitch-shifting the authored mix. Other replay speeds suspend that cue; returning
to 1× restarts it from the matching replay position on a source beat.

Immediate director triggers are collected from every newly crossed replay
tick, deduplicated by source tick and trigger type, and applied on the audio
clock. Contact and shot briefly open response-controlled rhythm/drive stems;
damage, overtime, and destruction use stronger impulses while foundation
stems remain stable. Major triggers also arm an authored `stinger` edge for a
short window. A stinger cannot participate in ordinary routing and cannot
repeat until its manifest `cooldownSeconds` has elapsed.

Every explicit seek, step, restart, replay change, or soundtrack-pack change
starts a new presentation segment. The runtime resets its trigger cursor at
the destination without replaying historical events, cancels pending
nonterminal routing, clears the horizontal phrase latch, impulses, stinger
arming, and cooldown state, then accepts the destination state immediately.
Finite resolve cues automate each responsive stem from current intensity
toward `targetIntensity` across the full cue (three bars, about six seconds in
Neon Protocol). Because the envelope and all transition timers use the audio
clock, the score stays on its musical grid. Pausing ramps only the music bus
silent while that clock continues; resuming reveals the coherent current
phrase and immediately retargets it to the current replay frame. The score
never suspends or closes the shared AudioContext, so effects controls and
previews remain independent.

## Adding another soundtrack

1. Export all stems from the same DAW range as uncompressed PCM WAV with
   identical format and frame count.
2. Create `soundtracks/<id>.json` with exact ZIP member mappings and measured
   grid timing. Record `sourceTool`, `rightsStatus`, and `shipApproval`
   explicitly; do not infer a license from possession of a ZIP.
3. Run `--validate-only --analysis-out ...` and inspect/listen to the suggested
   phrases and source boundaries.
4. Pin short sections, classifications, reviewed loops, and graph edges in the
   config. Assign every section a role, set repeat/cooldown metadata only where
   it has playback meaning, and choose explicit latency budgets. Prefer natural
   forward edits; add responsive cuts or short bridges only when the report
   supports them.
5. Build and audition every loop and transition in game. Mark reviewed loops
   `auditioned`, then record `rightsStatus: "rights-cleared"` and
   `shipApproval: "approved"` only after the corresponding evidence exists.
   Commit the config plus generated public version. Do not commit the source
   archive.

## Runtime tuning audit

Asset validation cannot show whether the music director changes too quickly in
short matches or stays in one state too long in extended matches. Audit a
replay corpus with the same causal director used by the viewer:

```bash
node scripts/audit_soundtrack_director.mjs \
  'sandbox/0.5-v5-overtime-b/cone-active-bolt2/out/*/replay.json'
```

The command accepts replay files, directories (searched recursively for
`replay.json`), and quoted glob patterns. It reads replay data without changing
it. By default it assumes the viewer's five presentation ticks per second and
one four-beat bar at 120 BPM, then groups matches into at most 10 seconds,
10–30 seconds, and over 30 seconds. Use `--tps`, `--bpm`,
`--beats-per-bar`, `--short-seconds`, and `--medium-seconds` to audit another
pack or presentation pace, or `--json` for machine-readable output.

The report includes state occupancy, per-state run counts and medians, runs
that reach one configured musical bar, transitions per replay, non-adjacent
state-rank leaps, longest continuous dwell, non-acute climax exposure, and the
dominant-state share. Track these metrics over representative short, medium,
and long corpora when tuning director thresholds or section-rotation cadence.
They are structural pacing checks; transition compatibility and listening
review remain separate gates.
