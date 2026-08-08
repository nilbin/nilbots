# Codex handover: class skins + class as a first-class citizen (2026-07-29)

Two parallel assignments for a Codex session. Both MUST branch from
`agent/frontline-duel-depth` (NOT main — main has no class system, no
skills, no stance presentation), each in its own worktree and branch:
`codex/class-skins` and `codex/class-first-class`. The Claude session
owns `agent/frontline-duel-depth` and integrates both branches the way
it integrated today's worktree agents (merge + full gauntlet).

Ground rules for both branches (non-negotiable, learned today):

- **Do not mint DECISIONS numbers.** Main and this branch numbered
  independently once and the reconciliation was expensive. Write draft
  entries in your branch as unnumbered sections (or in this file's
  sibling notes); they are numbered at integration.
- **CLAUDE.md and web/CLAUDE.md apply**; DocDriftTests, structure test,
  TreatWarningsAsErrors, goldens-unmodified all gate integration.
- CliVersion bumps happen at integration, not on the branch (the
  session reconciles the compatibility surface).

## Assignment 1 — class skins (`codex/class-skins`)

Give the three classes (striker / bulwark / fabricator) real visual
identities, replacing catalog stand-ins. Use the
`nilbots-visual-assets` skill pipeline (.claude/skills/) — material
sources, baking, size budgets, gameplay-scale review all live there.

Current state to read first:
- `web/src/render/unitPresentation.ts` — the layered look/accent
  resolution and the class fallbacks (striker→needle,
  bulwark→aureate-warden, fabricator→mantis) plus the STANCE slot:
  volley stance currently borrows `rift-runner`, aegis shell borrows
  `mossback`. Those are explicit stand-ins awaiting this work.
- DECISIONS #162 — the presentation philosophy: **every cue states a
  rule**. The shell look must read its protected quadrant (the edge is
  the counter-play); the volley stance's three barrels predict the fan;
  emplacements and stances are distinct body classes from mobiles.
- DECISIONS #153/#154/#165 — the class identities the art must serve:
  striker = tempo duelist with the deepest curve grammar; bulwark =
  fortification (turret + breakable directional shell); fabricator =
  the numbers class (up to five bodies; envelopment).
- Forms needing distinct reads: per class prime + child mobiles, the
  bulwark turret (exists), `*-volley-stance`, `*-aegis-shell`, and the
  fabricator's crowd (five bodies must stay tellable-apart from three
  bodies of anything else at gameplay zoom, and team-tellable).

Constraints: both renderers (WebGL + Canvas2D floor); bundle size
budgets per the skill; golden frames for replays without new content
stay byte-identical; team accent must survive (class look never eats
team identity — the wave-1 review's "teams look the same" complaint is
the cautionary tale).

Land window: anytime — presentation-only, no schema. Integration is a
normal merge.

**Renderer scope note (added after the brief's first commit):** the
renderer surface (web/src/render/, render3d/, unitPresentation,
presentation/) is Codex's lane until this branch merges — the Claude
session will not touch it. Re-branch from the CURRENT
agent/frontline-duel-depth tip: since the brief was first committed,
the branch gained the follow-camera + fit toggle, arrival
materialization (condensing spawn effects), and the deflection redirect
cue — style around them, don't rediscover them. Additional item handed
over: the new transport Timeline has no mark kind for
`projectile-deflected` (it is prose-only in the event feed; the arena
cue exists). Give it a mark consistent with the design language — it
is the counter-play beat for the bulwark's adopted skill. Discipline
reminders: golden frames pin the no-camera transform (the camera is a
`frame?` option — keep it that way), frameHash tests are the precedent
for new-cue tests, and CliVersion bumps happen at integration because
the viewer rides the CLI compatibility surface.

## Assignment 2 — class as a first-class citizen (`codex/class-first-class`)

Phase B of DECISIONS #153: promote class from manifest string + form-ID
prefixes to a typed architectural concept, observable everywhere ML
training needs it.

Scope (design + implementation on the branch):
- Typed `classId` on the resolved contract per participant/team, and in
  observations for BOTH sides (a bot must see the opponent's class
  without parsing form-ID prefixes). Follow the #156 additive-canonical
  pattern exactly: inert-default omitted, both mirrors (SDK reader +
  web replayV3Normalize) reject explicitly-inert encodings, chronology
  validator taught any new authoritative fact.
- While the schema is open, carry the WHOLE accumulated observability
  ledger in the same bump (one SDK bump per phase — never trickle):
  1. `holdOwnerTeamId` + `holdRemainingTicks` (or `advancingTeamId` on
     the ModeChanged payload) — demanded independently by four
     revision-3 authors; the current derivation is
     `ControlResumesAtTick − RedeployPauseTicks` plus an ownership
     guess that fails for lives born mid-hold.
  2. `ObservedProjectile.TicksPerAdvance` and damage — the "should I
     eat this?" fields from the wave-2 forensics.
  3. Spawn-reservation observability (the long-standing Phase B bucket
     item).
- Class-aware doc surfaces: EXPERIMENTAL-FRONTLINE-CLASSES.md's
  "Reading the class from the contract" section simplifies; keep the
  guidance that stat-conditioning generalizes better than
  name-conditioning.

Hard timing constraint — **the branch must NOT merge until the
designated window**: any schema addition faults every frozen artifact
(exact-object readers), so it lands in ONE batched SDK bump between the
phase-1 factorial and the phase-2 population commissioning, when every
bot rebuilds anyway. Build it complete and green on the branch; the
Claude session pulls it in at the window. If phase-1 slips, the branch
waits — never land schema mid-phase.

Test expectations: canonical round-trips per field, mirror rejections,
fingerprint distinctness where rules bytes change (they should NOT for
pure observation additions — observation schema is versioned separately
from rules fingerprints; verify and state which moved), chronology
tests for any new causal fact, and a probe match demonstrating a bot
reading the opponent's classId and the hold fields from a real
observation.

## Coordination

The Claude session is concurrently running: the revision-3 authoring
wave (arena-bots/, sandbox/), phase-1 pre-registration and factorial
(balance/, scripts/, /tmp), and consolidation docs. Neither Codex
branch should touch arena-bots/, balance/, or scripts/ except where an
assignment explicitly requires it. Questions or conflicts: leave a note
in this file's directory as HANDOVER-CODEX-NOTES.md on the branch; the
session reads it at integration.

## Owner ask for the default-map pass (2026-07-30, from watching wave-5 deck games)

Capture visuals are weak: **the owning team's color on a captured zone
must be obvious at a glance** — current state is too subtle in the 3D
viewer. While in there: capture *progress* (the erode-then-build reclaim
arithmetic) is the game's most important invisible number; any
map-surface treatment that makes claim + progress + ratchet-hold
readable (ring fill, pulsing during holds, whatever fits the map
language) directly serves the owner's watchability bar. Renderer lane
stays yours; replay data already carries holdOwnerTeamId/holdEndsAtTick.
