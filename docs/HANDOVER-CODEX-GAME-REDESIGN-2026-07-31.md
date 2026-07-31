# Handover: the game-redesign campaign

For Codex. Commissioned by the owner 2026-07-31 (DECISIONS #196).
This SUPERSEDES IN ORDER the mind-port wave handover
(HANDOVER-CODEX-MIND-PORT-WAVE-2026-07-31.md): the game is redesigned
FIRST; bot waves are authored after the game stabilizes.

## The commission, in the owner's frame

- **15-20 classes.** Fun variety. "We are NOT committed to any of the
  existing classes." Each class some depth, but not too much.
- **A bigger map than the current frontline.**
- **Core mechanics more interesting than today's** — the owner's
  verdict on record: "frontline feels a bit too dull."
- All of it serves **commander mode** (DECISIONS #195): the passive
  manager layer — sheets, gambits, per-sheet drawn plans, stable of
  ~5 from the roster, breadth-only rewards, the morning report.
  docs/DESIGN-COMMANDER-MODE-2026-07-31.md is the vision document;
  read it before designing anything.

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

- **Phase A — class-kit design wave.** Generate substantially more
  than 20 candidate class kits (one-page briefs: fantasy, statline
  band, signature mechanic, sheet-level choices it creates,
  counter-play). Quarries: the dormant-mechanics shelf — anchor/
  turret forms, Split, projectile deflection, MUSTER, ground healing
  (possibly a medic kit), the optic/vision axis, team auras — plus
  the wave-8 lineage sources (frozen, copy-out only) for doctrine
  ideas, plus fresh invention. Cull to a recommended launch band with
  honest reasoning. **GATE: the owner picks the roster.**
- **Phase B — map + core-mechanics redesign.** A bigger authored map
  (map generation 3 has named spawns and typed regions/tags — lanes
  and theaters are authorable today) and a core-loop redesign brief
  that answers the dullness verdict: what creates decisions per
  minute at the sheet level and legible drama at the replay level.
  Capture channels, the scrap economy, and rosters are all on the
  table — "not committed" includes mechanics, not only classes.
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

- **MUSTER x unified is an OWNER FORK, deliberately unresolved**:
  MUSTER's whole effect was prime-scoped, so `--chassis unified`
  REFUSES `--side-objective` rather than guess. Resolve it at Phase A
  (MUSTER may return as a class kit or a re-ruled objective).
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
