# Handover: the game-redesign campaign

For Codex. Commissioned by the owner 2026-07-31 (DECISIONS #196).
This SUPERSEDES IN ORDER the mind-port wave handover
(HANDOVER-CODEX-MIND-PORT-WAVE-2026-07-31.md): the game is redesigned
FIRST; bot waves are authored after the game stabilizes.

## The commission, in the owner's frame

- **Make a good game. Figure it out yourself.** The owner is
  deliberately NOT leading you toward any existing mechanism, class,
  or mode. You may design a DRASTICALLY different game than frontline
  — the engine platform stays (deterministic tile arena, the mind
  architecture, the WASM pipeline, the harness); everything frontline
  layered on top of it is disposable.
- **15-20 classes.** Fun variety. "We are NOT committed to any of the
  existing classes." Each class some depth, but not too much — and
  **ONE signature skill per class** (owner ruling on shape).
- **A bigger map than the current frontline.**
- **Core mechanics more interesting than today's** — the owner's
  verdict on record: "frontline feels a bit too dull."
- **It must be FUN TO WATCH.** Human fun value is a first-class
  requirement, not polish: under commander mode the product largely
  IS watching replays (the morning report), and the owner's gate is
  watching games. Design for legible drama on screen — a spectator
  should see momentum, reversals, and why the winner won.
- All of it serves **commander mode** (DECISIONS #195), and — owner
  clarification (#198) — the commander-mode PLAYER LAYER largely
  STAYS: sheets, ordered gambits, per-sheet DRAWN map tactics
  (paths, zones, rally lines), the stable drawn from the roster,
  breadth-only rewards, the morning report, passive/blind play.
  These are standing requirements, not leads — you may TANGENT and
  IMPROVE on those ideas (better authoring shapes, better adaptive
  grammar), but do not discard the layer. The "disposable" latitude
  applies to the GAME under it (mode, mechanics, classes, map).
  docs/DESIGN-COMMANDER-MODE-2026-07-31.md is the vision document;
  read it before designing anything. Its class-seed examples are
  ILLUSTRATIVE ONLY — they are not a lead, and neither is anything
  else in the current game.

Reading order: this file -> DESIGN-COMMANDER-MODE-2026-07-31.md ->
DECISIONS.md #188-#196 (the recent arc) ->
EXPERIMENTAL-FRONTLINE-CLASSES.md (the current game you are judging
against) -> DESIGN-MIND-ARCHITECTURE-2026-07-31.md (the platform) ->
EVALUATION-METHODOLOGY.md + the balance-harness skill (how reads run).

## Rulings that bind

1. **2D is the renderer** (#196). Canvas2D is the primary and only
   REQUIRED presentation surface for the experimental game — cheap
   per class, and reviewable by an agent from a rendered frame. The
   3D viewer is PARKED for the experiment: keep it compiling, do not
   extend it per new mechanic. The shipped duel product keeps its 3D
   stance. Amend CLAUDE.md's renderer paragraph when your first
   presentation change lands. The #189 law stands: a mechanic that
   the viewer does not RENDER does not exist for the owner.
2. **Determinism is the product.** Same versions + artifacts + map +
   seed => identical replay hash. Nothing you design may require live
   input; commander-mode authorship is per-sheet (between matches) by
   owner ruling.
3. **The mind is the platform** (#190-#192). One runtime per
   participant drives all its bodies. Design mechanics for the mind
   profile; the per-life wrap keeps old artifacts playable but is a
   compatibility path, not a design target.
4. **DECISIONS.md is the owner's log.** Never write it. Report; the
   owner (or the coordinating agent) records rulings.
5. **Owner gates are part of the campaign contract**, not overhead:
   - Taste forks: present curated options with pre-registered
     alternatives; the owner rules in one line. Anything genuinely
     50/50 ships conservative with the alternative registered (#174).
   - The felt-experience gate: galleries the owner watches are the
     ONLY authority on "fun." Measurements verify structure — pacing,
     dominance, cycles — never fun. Build galleries early and often
     (scripts/serve-gallery.py is the convention).

## Campaign shape (adapt as evidence demands; keep the gates)

- **Phase A — the game concept + class roster.** Design the game
  first — the core loop, the win conditions, what a spectator sees —
  then the classes that live in it: candidate kits well beyond 20
  (one-page briefs: fantasy, ONE signature skill, statline band, the
  sheet-level choices it creates, counter-play, what it looks like on
  screen), culled to a recommended launch band with honest reasoning.
  Invent freely. The engine carries dormant mechanisms from earlier
  design windows; you may mine or ignore them — the game design
  comes first and mechanics serve it, never the reverse.
  **GATE: the owner rules on the concept and picks the roster.**
- **Phase B — map + core-mechanics design.** A bigger authored map
  (map generation 3 has named spawns and typed regions/tags — lanes
  and theaters are authorable today) and the mechanics brief that
  answers the dullness verdict: what creates decisions per minute at
  the sheet level and legible drama at the watch level. Everything
  frontline does today — capture channels, the scrap economy,
  rosters, the mode itself — is on the table.
  **GATE: owner rules on the mechanics brief.**
- **Phase C — build.** Implement behind flags on the gen-3+
  experimental path, additive where the contract allows, minting new
  experimental generations where it does not (frozen goldens and the
  shipped duel product stay byte-exact — that line is absolute).
  2D rendering ships WITH each mechanic, not after.
- **Phase D — stock mind v0 + depth audit.** Map-agnostic, sheet-first
  config (composition plans, priorities, policies, ordered gambits,
  drawn paths/zones); formation-keeping quality is its hardest
  requirement. Then the audit: fixed stock mind, sheet-space
  tournament, payoff matrix read for dominance vs cycles; sharpest
  question — do gambit-bearing sheets beat static ones? **GATE:
  galleries + the audit read; the owner reacts; loop C/D until the
  felt-experience gate passes.**

## Starting state (all committed; CLI 0.9.30 at sandbox/cli-publish)

The one-chassis package (#194) landed as class-agnostic
infrastructure that survives ANY roster:

- `--chassis unified` — prime dissolved: one statline and one
  lifecycle per class; upgrades all-slot-lives with `--tier-cost`
  sweepable (20; 10/30 registered); the fabricator is a headless
  network (fabricate on every body; home base as root factory at
  total loss, `spawnReason: root-factory-seed` on LifeSpawned).
- `--compositions <a>-vs-<b>` — slot-scoped chassis (mind memo §9.6
  grammar); mono tokens byte-identical to today; `spearhead`/`warden`
  registered mixed tokens; mixed requires `--chassis unified` +
  `--roster legion`. Per-slot `classId` is populated end-to-end.
- 8 new entries in balance/frontline-ablation-debt-v1.json.

## Known sharp edges

- `--chassis unified` REFUSES `--side-objective`: the dormant MUSTER
  objective's whole effect was prime-scoped and nothing prime-scoped
  survives. A technical fact, not a design lead — it only matters if
  your design happens to revive that mechanism.
- `fabricator-vs-fabricator` + `facing-locked` + a price token
  overflows the 64-char ruleset-ID budget — that sweep cell needs a
  newly registered token.
- Composition fabricability keys on the WHOLE composition, not slot 0
  (a warden army fabricates); registered as `composition-slot-chassis`
  debt.
- Under `--five-slots wane` the unified fabricator delay is the tuned
  22, not 15 — arms compose; remember when reading sweeps.
- A mixed composition moves the RULES fingerprint (catalog gains the
  third chassis), not only topology — asserted by tests; do not
  "fix" it back.
- Pre-mind artifacts (SDK < 0.10.11) fault at startup on the mind
  profile — expected; rebuild from frozen sources.
- `CandidateClassIds`/`SelectedClassId` on mind slots stay
  reserved-empty (v1 admits fixed chassis only).

## Process law (the campaign's hard-won rules)

1. COUNT REPLAY FILES, never trust exit codes. A missing replay
   measured nothing — re-run it, never score it.
2. Deterministic bots: N seeds are NOT N observations. Disclose
   distinct-outcome counts beside every table.
3. Kill-fix-relaunch: a defective build's cells re-run from scratch;
   never patch mid-sweep and splice.
4. Sweeps write no viewers (opt-in `--viewer`). Watch disk.
5. `sandbox/` is scratch; `arena-bots/` freezes are append-only.
6. Identity tokens re-mint when behavior changes; flags are UI.
7. Every rules change sweeps ALL derived surfaces (CLAUDE.md
   "Rules-change surfaces") and adds DocDrift pins for mechanical
   lists.

## Report format (the owner reads this directly)

Lead with `DECISION NEEDED:` (or "none"), then RESULT in plain words,
then EVIDENCE (tables with distinct-outcome counts, gallery links,
hashes), then NEXT. Spell out codenames on first use.
