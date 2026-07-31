# Handover: the mind port wave (P6)

For Codex. Commissioned by the owner; run this when he says it is due.
This document is self-contained — read it fully before touching anything,
then read the three files it names as authoritative.

## Context in five sentences

The game moved to THE MIND (DECISIONS #190–#192): one submitted artifact
is one runtime driving every body its participant owns for the whole
match, with persistent memory — the per-life "every bot for himself"
model remains supported but superseded for this game. The mind profile is
`generic-mind-match-1`; the resolved match contract is UNCHANGED (same
game, different driver), and the null pin proved it: the wrapped wave-8
cohort played 63/63 cells outcome-identically on both profiles. The
current game stack is `warpath` (legion rosters growing 3→8, bodies 4→9
for the fabricator; hull pendulum with home respawns; channeled captures;
the scrap economy with a six-tier board; 750-tick horizon). Your job is
P6 of the build plan in docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md: port
the eight per-life lineages to native minds and run the ported-vs-wrapped
A/B that measures what the architecture is worth. Authoritative reading
order: this file → docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md (§ on the
API, the wrapper, and the A/B) → docs/EXPERIMENTAL-FRONTLINE-CLASSES.md
(the whole current game, including "The mind" section).

## What "porting" means — and what it must NOT mean

A port is a MECHANICAL translation, not a doctrine pass. The A/B's whole
value is that the ported mind embodies the same strategy as its per-life
original, so any measured delta is the architecture (shared memory, no
same-tick blindness, coherent assignments), never new ideas.

- Source: `arena-bots/frontline-labs/classes-wave-8-2026-07-31/<name>/`.
  These directories are FROZEN — copy out to a scratch dir under
  `sandbox/`, never build inside them, never modify them.
- Delete the coordination layer (each lineage carries ~600–700 lines of
  common-knowledge machinery: shared-plan derivations, sibling-intent
  inference, per-life memory reconstruction, TeamRandom conventions).
  The mind replaces all of it: one `Think(mind)` per tick, commands
  written onto `mind.Bodies` handles, persistent fields for memory.
- Keep the contract readers, gunnery/ballistics, target selection,
  invest logic, and every doctrine RULE. Where a rule consumed the
  deleted machinery (e.g. "who is spare" derivations), re-express the
  same rule directly over `mind.Bodies` — same decision, simpler code.
- You will see all eight sources. Do NOT move ideas between lineages.
  Each port preserves its own lineage's play 1:1. If you find a bug in a
  lineage while porting, preserve the bug (the A/B compares against the
  wrapped original, which has it too) and note it in PORT-NOTES.md.
- The API and the mental model are in the template:
  `templates/botarena-generic-mind/` (Roles.cs is the front door). The
  SDK surface is `IGenericMindBot` / `MindContext` / `MindBody`
  (src/BotArena.Sdk/, XML-documented). RoleTags: set them (`SetRole`) —
  they are cosmetic, viewer-rendered, and free.

## Deliverables

Per lineage, at `arena-bots/frontline-labs/mind-port-2026-08/<name>/`:
full source, `out/bot.wasm`, `PORT-NOTES.md` (what was deleted, what was
kept, any preserved bugs, line counts before/after), and the final act
every wave performs: `nilbots build <frozen tree> --no-cache` must
reproduce the shipped artifact hash — state both hashes.

Qualification: T4 on `frontline-mind-qualification-5` (note: the mind
suites include `body-handoff` and `escort-integrity`; the wave-8 doctrine
should pass both — if a port fails a probe its original passes on the
per-life suite, that is a PORT BUG, not a doctrine gap).

Do all eight lineages. If budget forces a cut, the minimum is one per
class — vector-edge (striker), iron-root (bulwark), ledger-fly
(fabricator) — but the full eight is the commission.

## The A/B read

For each ported lineage: ported mind vs its own WRAPPED self.

- The wrapped baseline: the same lineage's wave-8 source rebuilt on the
  current toolchain — prebuilt at `sandbox/w8-mind-0.10.11/<name>/out/
  bot.wasm` (rebuild them yourself with the current published CLI if that
  directory is gone: copy each frozen wave-8 source out, `nilbots
  build`). A per-life artifact runs on the mind profile automatically
  (the wrapper) — no flags needed beyond the profile.
- Cells: the lineage's class pairings from the wave-8 matrix (a striker
  port plays bvs against the three wrapped bulwarks and fvs against the
  two wrapped fabricators, plus the mirror against its own wrapped self).
  Seeds 930011, 960017, 990037 — the campaign's read seeds.
- Stack and flags (the warpath game, mind profile):

```
sandbox/cli-publish/botarena experiment frontline-labs --profile mind \
  --bot <ported>/out/bot.wasm --opponent <wrapped>/out/bot.wasm \
  --classes <pair> --movement facing-locked --pendulum hull \
  --skills kit --bend universal --aim offset --stance-ground open \
  --cooldown ticking --volley salvo --capture channel --economy scrap \
  --roster legion --horizon long [--five-slots wane with a fabricator] \
  --seeds 930011,960017,990037 --runtime wasm --out <dir>
```

- Also run each mirror (ported vs its own wrapped self) BOTH ways
  (--swap) — the null pin guarantees the wrapped side is faithful, so
  the mirror is the cleanest architecture measurement in the study.
- The pacing diagnostic (pre-registered, #189): report breach rate vs
  max-ticks rate per cell, against the wave-8 coarse baseline (bvs
  +0.370, bvf −0.278, fvs +0.333; 44/63 at the wall). The question on
  record: is the wall doctrine or numbers? Minds with coherent
  assignments are the doctrine-side answer — measure whether they
  convert more.

## Process law (the campaign's hard-won rules — follow all of them)

1. COUNT REPLAY FILES, never trust exit codes. Every cell must show
   `<seeds>` replay.json files; a missing replay means the cell measured
   nothing and must be re-run, never scored.
2. Deterministic bots: N seeds are NOT N observations. Disclose
   distinct-outcome counts beside every table.
3. Kill-fix-relaunch: if a port has a defect mid-study, fix the port and
   re-run its cells from scratch. Never patch mid-sweep and splice.
4. Sweeps write no viewers (they are opt-in: `--viewer`). Watch disk.
5. `sandbox/` is scratch; `arena-bots/` freezes are append-only; the
   only tracked things you add are the port directories and PORT-NOTES.
6. docs/DECISIONS.md is the owner's log — do not write it. Report your
   results; the owner's session writes the record.

## Report format (the owner reads this directly)

Lead with `DECISION NEEDED:` (or "none"), then RESULT in plain words,
then EVIDENCE — the per-pair table (ported-vs-wrapped W-L-D and payoff,
with distinct-outcome counts), the mirror results, the pacing
diagnostic vs baseline, per-lineage port stats (lines deleted, hash
pair, T4 status) — then NEXT. Codenames spelled out on first use.

## Known sharp edges

- Pre-mind artifacts (anything built before SDK 0.10.11) FAULT at
  startup on the mind profile — expected; rebuild from source.
- A mind-startup fault aborting document recording was a live defect at
  handover time; the pre-friction pass queued the fix. If you hit
  "Runtime fault evidence does not match its actor turn", check whether
  that fix landed (docs/DECISIONS.md #192 and later).
- The scaffold's reservation-based movement (template ArenaBasics) is a
  reference implementation for intra-mind collision handling — ports may
  adopt it ONLY if the original lineage had equivalent collision
  handling; otherwise preserve the original behavior (see the doctrine
  rule above).
- Fuel: 250M + 200M × live bodies per tick, shared across the mind. The
  wave-8 doctrines use a tiny fraction of it; if a port trips the fuel
  fault, something is wrong with the port (an accidental loop), not the
  budget.
