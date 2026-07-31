# Handover: the mind-native doctrine wave

For Codex. Commissioned by the owner; run this when he says it is due.
Self-contained — read it fully, then the files it names.

OWNER AMENDMENT (2026-07-31): the originally-planned strict-port stage
(mechanical 1:1 ports + ported-vs-wrapped A/B) is SKIPPED — "I think we
can skip 1." Go straight to the doctrine pass. The recorded caveat: the
wave's deltas conflate the architecture's value with the new doctrine's
value; the null pin (DECISIONS #192, 63/63) already proved the wrapped
originals are faithful old-world baselines, which is what makes the
skip safe.

## Context in five sentences

The game moved to THE MIND (DECISIONS #190–#192): one submitted
artifact is one runtime driving every body its participant owns for the
whole match, with persistent memory — the per-life model remains
supported but superseded for this game. The mind profile is
`generic-mind-match-1`; the resolved match contract is unchanged (same
game, different driver), proven by the null pin: the wrapped wave-8
cohort played 63/63 cells outcome-identically on both profiles. The
current stack is `warpath` (legion rosters 3→8, fabricator 4→9; hull
pendulum with home respawns; channeled captures; the scrap economy with
a six-tier board; 750-tick horizon). Your job: write the MIND-NATIVE
new versions of the eight stable lineages — the owner's "new versions
of the current stable" — and run the wave read. Reading order: this
file → docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md (the API and the
wrapper) → docs/EXPERIMENTAL-FRONTLINE-CLASSES.md (the whole current
game, including "The mind" section).

## The commission

Eight lineages, from their frozen wave-8 sources at
`arena-bots/frontline-labs/classes-wave-8-2026-07-31/<name>/` (FROZEN —
copy out to scratch under `sandbox/`, never build inside, never
modify): vector-edge, still-water, arc-light (strikers); iron-root,
march-wall, gate-stone (bulwarks); spark-line, ledger-fly
(fabricators).

Per lineage, ONE doctrine pass rebuilding it as a native
`IGenericMindBot`, exploiting what only a mind can do:

- Persistent memory: scouting that survives death, enemy tracking,
  economy bookkeeping, tier-vector reading over time (`Recall` in the
  template is the working pattern).
- Match-long build orders and coherent role choreography: assignments,
  escorted channels, courier scheduling, written directly over
  `mind.Bodies` — the ~600–700 lines of common-knowledge machinery each
  lineage carries are DELETED, not ported.
- Keep each lineage's identity recognizable (pressure-duelist,
  stance-tempo, flank-and-collapse, the wall, the fast breach, the
  rotation discipline, the tempo engine, the ledger). Contract readers,
  gunnery, and doctrine RULES transfer; their scaffolding does not.
- Mechanical repairs free. Leave-one-out attribution per rule in DX,
  exactly as every prior wave.
- Honesty requirement, because one author writes all eight: no idea
  transfer between lineages; disclose in each DX where an idea's
  provenance is another lineage's public results. The read tolerates
  shared authorship; it does not tolerate eight copies of one doctrine.
- Set role tags (`SetRole`) — cosmetic, viewer-rendered, free.

The API and mental model: `templates/botarena-generic-mind/` (Roles.cs
is the front door); SDK `IGenericMindBot`/`MindContext`/`MindBody`,
XML-documented. Commands are written onto body handles; every live body
pre-fills Wait; the mind ticks every tick including with zero bodies; a
trapping mind forgets the match (fault = participant disqualification).

## Deliverables

Per lineage, frozen at `arena-bots/frontline-labs/mind-wave-2026-08/<name>/`:
full source, `out/bot.wasm`, `README.md` (role, doctrine summary,
headline results), `DX.md` (budget ledger, per-rule leave-one-out
attribution, friction list, provenance disclosures), and the final act:
`nilbots build <frozen tree> --no-cache` reproduces the shipped
artifact hash — state both hashes.

Qualification: T4 on `frontline-mind-qualification-5` (includes the
mind-native `body-handoff` and `escort-integrity` probes).

## The wave read

- Baselines: the WRAPPED wave-8 originals — prebuilt at
  `sandbox/w8-mind-0.10.11/<name>/out/bot.wasm`, or rebuild them from
  the frozen wave-8 sources with the published CLI if that directory is
  gone. A per-life artifact runs on the mind profile automatically (the
  wrapper); the null pin certifies it faithful.
- Per lineage: native vs its own wrapped self (the mirror, BOTH
  assignments via --swap) and native vs the wrapped field on its class
  pairings. Then the wave's own triangle: native vs native across the
  full class-pair matrix — the first balance read of the mind era.
- Seeds 930011, 960017, 990037 — the campaign's read seeds.
- Stack and flags:

```
sandbox/cli-publish/botarena experiment frontline-labs --profile mind \
  --bot <native>/out/bot.wasm --opponent <baseline>/out/bot.wasm \
  --classes <pair> --movement facing-locked --pendulum hull \
  --skills kit --bend universal --aim offset --stance-ground open \
  --cooldown ticking --volley salvo --capture channel --economy scrap \
  --roster legion --horizon long [--five-slots wane with a fabricator] \
  --seeds 930011,960017,990037 --runtime wasm --out <dir>
```

- The pacing diagnostic (pre-registered, #189): breach rate vs
  max-ticks rate per cell against the wave-8 coarse baseline (bvs
  +0.370, bvf −0.278, fvs +0.333; 44/63 at the wall). The question on
  record: is the wall doctrine or numbers? Minds are the doctrine-side
  answer — measure whether they convert.
- Usage stats per cell: tiers bought, casts, interrupts, couriers,
  channels completed/denied.

## Process law (the campaign's hard-won rules)

1. COUNT REPLAY FILES, never trust exit codes. A missing replay means
   the cell measured nothing — re-run it, never score it.
2. Deterministic bots: N seeds are NOT N observations. Disclose
   distinct-outcome counts beside every table.
3. Kill-fix-relaunch: a defective build's cells re-run from scratch.
   Never patch mid-sweep and splice.
4. Sweeps write no viewers (opt-in: `--viewer`). Watch disk.
5. `sandbox/` is scratch; `arena-bots/` freezes are append-only.
6. docs/DECISIONS.md is the owner's log — do not write it.

## Report format (the owner reads this directly)

Lead with `DECISION NEEDED:` (or "none"), then RESULT in plain words,
then EVIDENCE — the native-vs-wrapped table per lineage, the
native-vs-native triangle with distinct-outcome counts, the pacing
diagnostic vs baseline, per-lineage stats (T4, hash pair, attribution
headlines) — then NEXT. Codenames spelled out on first use.

## Known sharp edges

- Pre-mind artifacts (built before SDK 0.10.11) FAULT at startup on the
  mind profile — expected; rebuild from source.
- A mind-startup fault aborting document recording was a live defect at
  handover time; the pre-friction pass queued the fix. If you hit
  "Runtime fault evidence does not match its actor turn", check whether
  it landed (docs/DECISIONS.md #192 and later).
- Fuel: 250M + 200M × live bodies per tick, shared across the mind. If
  a build trips the fuel fault, suspect an accidental loop in the
  build, not the budget.
- Compositions are OUT of scope (P7 is read-gated and owner-ruled);
  allied intents remain reserved; role tags are cosmetic.
