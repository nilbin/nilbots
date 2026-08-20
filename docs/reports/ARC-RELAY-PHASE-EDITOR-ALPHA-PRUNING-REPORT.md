# Arc Relay phase editor alpha pruning

Date: 2026-08-20
Branch: `main`

## Outcome

The Tactics phase editor now presents one deliberately small normal vocabulary:

- ground: **Posts**, **Converge**, **Occupy area**, **Patrol**;
- destination posture: **Observe**, **Hold**, **Aggressive**;
- travel: **Just march**, **Respond to contact**, **Brawl through**;
- recovery: **Fight on**, **Break for the heal tile**, **Heal up fully**.

Executable older values remain loadable and are shown as **Existing advanced**
when a saved document still uses them. They cannot be newly selected through the
normal editor. Carry-forward remains the internal `none` encoding and is not a
fifth player-facing job.

This pass did not add a Tactics scheduler, a second priority system, a new game
action, or runtime phase state outside the existing coordinator. Tactics remain
deterministic authoring sugar over ordinary ordered Instructions.

## Phase contract

- Phases advance strictly in array order. New documents no longer author a
  destination phase id; the compiler rejects arbitrary jumps.
- The old hidden 40-tick Tactic rearm is gone. Completion tick T is the required
  dark evaluation, another eligible Tactic may begin on T, and the same Tactic
  may begin again on T+1.
- **Formation is ready** accepts Everyone, All but one, or All but two. The
  readiness cohort is the set of lives present at phase entry: a death excuses
  that life and its later respawn does not re-block the phase.
- Posts remain slot-exact, with one tolerance zone per assigned body even for an
  eight-body Group.
- Recent-enemy, verified-clear, and no-local-friendly-fight rows accept one to
  three areas. Runtime evaluation uses the union of canonical tiles, so
  overlapping areas never double-count.

Verified-clear materializes Scan support per area and only for Groups whose
phase ground overlaps or touches that particular area. A distant Group is never
silently recruited to scan another Group's area.

Core custody remains latched to the pickup phase. A carrier keeps its
pickup-phase custody Instructions; an escorted plan may also retain potential
escorts from that carrier's pickup-phase Group. Unrelated Groups continue the
team's current phase and are never recruited into that custody episode.

## Editor behavior

- Switching a populated job names the geometry it will erase, defaults to
  Cancel through the browser confirmation, removes incompatible geometry and
  dependent signature marks only after confirmation, and exposes one-step
  **Undo**.
- The active phase is drawn at full strength. Its immediate previous and next
  phases remain visible but dimmed; more distant phases are hidden unless
  **Show every phase** is enabled, independently of **Show every Tactic**.
- Area-bearing conditions expose Area 1–3 with named-zone selection, redraw,
  add, and remove controls.
- Player summaries use the authored vocabulary and correct singular copy
  (for example, “one enemy was recently seen”).

## UI-authored acceptance

The complete acceptance sheet was authored and refined through the running UI,
then saved and reloaded from the persistent local PostgreSQL database:

- sheet: `Phase Pruning Acceptance`
- id: `29bbcc20-b915-45f8-bb1e-e47d50f7aa02`
- accepted saved revision: 4
- Practice match: `5232fb44-4a85-42bf-8eb6-3d129f523bed`
- Practice replay hash:
  `cddbf273ea8cbbbe1efa4336380ebf0dc9025837cbd8fd4b526c113b8b644016`

The first exhibition draft correctly failed the eligibility gate because its
five-enemy release and eight-body patrol produced formation freeze/passivity.
The fixture was corrected through the editor rather than weakening the bars:
release on one observed enemy, solo carrier banking, and a broad final area
hold. The final six cells all passed the tracked felt-degeneracy eligibility
bars and replay-visible movement sanity checks.

Final six-cell exhibit against Demolition March:

| Cell | End |
| --- | --- |
| west 9001 | reactor destroyed, tick 223 |
| west 9002 | reactor destroyed, tick 240 |
| east 9005 | reactor destroyed, tick 246 |
| east 9009 | reactor destroyed, tick 248 |
| west 9012 | reactor destroyed, tick 249 |
| east 9004 | reactor destroyed, tick 250 |

Demolition March won all six. This is an interface/runtime exhibition, not a
balance measurement or a claim that the acceptance sheet is competitive.

Public gallery (verified HTTP 200):
https://matt-ranch-joining-anatomy.trycloudflare.com/

## Verification

- `dotnet build BotArena.sln --no-restore --nologo` — passed, zero warnings.
- `dotnet test tests/ArcRelayTacticalPlaybookMind.Tests` — 90 passed.
- `dotnet test tests/BotArena.Engine.Tests` — 1,542 passed.
- `dotnet test tests/BotArena.Cli.Tests` — 330 passed.
- `dotnet test tests/BotArena.App.Tests` without PostgreSQL — 213 passed,
  91 PostgreSQL-gated tests skipped.
- PostgreSQL-required App run against the persistent local PostgreSQL service —
  301 passed, 3 external S3/toolchain cases skipped.
- `cd web && npm test` — 584 passed.
- `cd web && npm run build` — passed.
- `bash scripts/generate-api-clients.sh` — passed with no generated diff.
- `git diff --check` — passed.

The standard exhibit used the trusted in-process runtime as required by the
iteration workflow. No WASM artifact, hosted playlist, rating book, map,
gameplay rule, or API contract changed in this pass.

## Known limits

- Existing advanced values are preserved for honest editing, but there is no
  normal UI for creating more of them.
- A Patrol has no separate travel selector because its authored behavior is
  continuous route traversal; its posture governs encounters on that route.
- The acceptance fixture intentionally exercises editing and runtime wiring,
  not strategic strength.
