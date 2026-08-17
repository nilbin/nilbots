# Arc Relay player-ready Programs report

Status: **implemented for owner review**
Date: 2026-08-17
Baseline: `50541438`
Implementation commits: `e1905a56`, `e23136ba`, `0a2a95dc`, `03b4b958`,
`ca5f3733`, plus the closing QA/report commit cited in the handoff
Rules/map used by the evidence: `arc-relay-tapline-08` /
`arc-relay-tapline-02`

## Outcome

Programs are now a usable map-first strategy-authoring alpha rather than a
thin wrapper around generated Instructions. A player can place and refine
Posts, facing, readiness, maintained deployments, shared triggers, committed
responses, signature opportunities, contact fighting, bounded Core/healing
detours, and ordinary fallbacks from the normal editor. The top-to-bottom
Program list remains the only player-facing priority order.

Every Program choice still materializes deterministic Orders v2 Conditions
and Instructions. No Program id, Post, Opportunity, reservation, phase, or
second scheduler enters ATL2, match state, replay identity, or execution.

The public review gallery is:

<https://columnists-enjoy-hampshire-dentists.trycloudflare.com>

It is the standard six-cell set, always **Running Relay** versus the named
**Running Relay Solo** control. Outcomes are records for watching, not a
balance result. Exact replay/state hashes are in
[the evidence ledger](assets/player-ready-programs/evidence.json).

## What shipped

### Runtime and compiler primitives

- Added exact owner-local and role-qualified deployment-in-zone truth,
  including suppression, destruction, expiry, replacement, map mirroring, and
  stable owner semantics.
- Added body-local legal-walking distances and bounded targets for currently
  visible loose Cores and healing tiles. Conditions and target selection share
  the same candidate/filter implementation.
- Added team-relative Core-value filters and closest/highest-value ordering,
  plus value-filtered nearest-carrier Follow.
- Added historical friendly Core-pickup evidence, Fight target areas,
  zone-qualified reachable-contact distance, and the ordinary commitment /
  not-ready staging shapes needed by Programs.
- Preserved historical unqualified `nearest-heal-zone` selection exactly;
  only the newly authored bounded form opts into legal-walking ordering.
- Every accepted action, decline, hold, and fallthrough keeps a non-empty
  diagnostic reason. Rejected higher Instructions are appended as
  `eligibility[...]` evidence without becoming the selected action.

### Map-first Program authoring

- A reusable Post owns stand point, tolerance/readiness zone, cardinal facing,
  body capacity, effect target, deployment maintenance, and arming policy.
  Stand, facing, source, and effect remain separate visible objects.
- Posts and linked geometry can be dragged, group-moved, undone/redone, or
  nudged one tile with arrow keys. Derived readiness/facing geometry follows
  atomically.
- The normal surface exposes **Rebuild when missing** and **Required before
  response** instead of hiding those policies in generated source.
- One reusable Fight editor now serves Program response and On-contact
  activation. Switching activation preserves target order, area, style, chase
  limit, and attackers-per-enemy capacity.
- One ordered Opportunity surface covers descriptor-driven signature use,
  Fight, nearby-Core recovery, health recovery, Handoff, and Drop above the
  exact durable continuation. The displayed order is the generated sibling
  order; a declined leaf reaches the next sibling in the same tick.
- Normal Core/healing cards persist visible sheet-wide one-target capacity
  defaults. Core pursuit remains distinct from physical incidental pickup, and
  a linked higher **Bank carried Core** Program is editor navigation and
  validation—not a runtime call.
- All sixteen launch signature descriptors remain the source of truth. Exact
  anchor authoring now covers Survey Flare, Falling Star, Trip Node, Hardlight
  Block, Smoke Canister, and Sentinel Seed; RouteForward remains capability-
  gated.
- Desktop and 390×844 layouts retain the map and selected context, use 44 px
  touch targets, have keyboard equivalents for map movement, and produce no
  horizontal page overflow.

### Practice explanation

- Starting Practice stores an immutable owner-only authoring snapshot with the
  exact sheet revision, workshop, Orders, and map plan. It is excluded from
  executable input, public broadcast, and gameplay/content hashes.
- The selected-body lens maps accepted and rejected Instruction source ids
  back to the Program/Post from that snapshot, shows the semantic action and
  current readiness evidence, and groups consecutive ticks into a compact
  timeline.
- Editing the draft after a run shows the revision mismatch. **Open revision
  used** restores the historical authoring view; **Rerun current draft** keeps
  the edit-run-watch loop explicit.
- Compact broadcast reasons can truncate the action suffix. In that case the
  immutable authored source mapping supplies the honest semantic label (for
  example, `Use Vector Dash`) rather than displaying a misleading generic
  wait. Exact runtime decline detail is shown only when the canonical Practice
  evidence retained it; the UI does not invent a reason.

### Complete starters

- **Mid Overload Fork**: legal 8-body / 7-class roster with duplicate Masons;
  independent Kestrel raid; 2/2 home defence and 5/5 return formation; shared
  remembered-mid trigger; exact Posts, maintained walls, committed Fight, and
  complete fallbacks.
- **Running Relay**: legal 8-body / 7-class roster with duplicate Relays;
  route-linked source/receiver pockets and ordinary role-target Arc Tosses;
  complete carrier and field fallbacks; no generated `aheadVia` or race-only
  executable state.
- Both are reproducibly generated from tracked source and exposed as editable
  App starter sheets. The deterministic controls remain named Practice
  opponents.

## Deterministic mechanism evidence

The durable [evidence ledger](assets/player-ready-programs/evidence.json)
contains the exact replay hashes and gameplay-state digests.

### Mid Overload drill

`mid-overload-fork-v1` versus `mid-overload-drill-v1`, seed 9001:

- independent raider Program moves from tick 0;
- Vector Dash accepted at tick 21;
- home and return Hardlight accepted at ticks 24 and 36;
- home response entered at tick 54;
- return Mason/Medic response entered at tick 60;
- replay hash
  `b69154b5660d1f968cfa670f9c2a592e8975e88ff382830dc74e011fff967f8c`;
- gameplay-state digest
  `65b2e137d673d6dc357a38c2ae54ad31a61b3c48e2a5e048e96e79bcf7e12b2f`.

The drill is a scripted mechanism fixture. Its deliberate formation staging
is classified as movement by the generic gallery sanity reader, so it is kept
as separate focused evidence rather than forcing it through the standard
felt-eligibility gallery.

### Running Relay race

Seed 9001 against Idle Field, using the same south-Well route:

| Variant | Accepted Toss ticks | South Core bank |
| --- | --- | ---: |
| Running Relay | 67, 100, 142 | 157 |
| Running Relay Solo | none | 167 |

The linked Program advances the staged Core by 10 ticks without `aheadVia`, a
runtime race state, or a special Toss target. This proves the authored
mechanism; it is not a combat or balance claim.

### Standard six-cell exhibit

All six cells completed and passed the current felt-degeneracy eligibility
and motion-sanity diagnostics: zero closed cycles, recovery no-progress,
false-move traces, contradictory traffic traces, local move orbits, and carrier
route trips. Running Relay lost all six combat exhibits to the solo control;
that is disclosed in the gallery and is not reinterpreted as a tuning verdict.

## Production browser QA

QA used a production web build and a real local PostgreSQL-backed App. The
same Mid Overload copy was exercised end to end on desktop and at 390×844:

- copied the starter, edited and saved it;
- moved a Mason Post and its effect target independently;
- changed cardinal facing and nudged geometry with the keyboard;
- toggled rebuild/readiness policies and changed readiness range;
- edited target order, Fight style, chase, area, and capacity;
- switched the same Fight between Program response and On contact and back,
  proving its configuration survived;
- ran seed 9001, watched it, selected bodies, and saw Program-specific action
  attribution including Hardlight and Vector Dash;
- edited revision 3 to revision 4, reopened the old run, and verified immutable
  revision-3 attribution before rerunning the current draft;
- reloaded the saved sheet; and
- completed the phone flow with `clientWidth = scrollWidth = 390`.

No browser console warnings or errors remained on either the App or public
gallery origins.

![Desktop Program Fight editor](assets/player-ready-programs/program-fight-desktop.png)

![Desktop immutable Practice lens](assets/player-ready-programs/practice-lens-desktop.png)

![Phone Program Fight editor](assets/player-ready-programs/program-fight-phone.png)

![Phone map-first Post editing](assets/player-ready-programs/program-map-phone.png)

## Validation

| Check | Result |
| --- | --- |
| `dotnet build BotArena.sln -c Release` | passed, 0 warnings / 0 errors |
| Engine tests | 1,539 passed |
| tactical-mind tests | 33 passed |
| CLI/compiler tests | 258 passed |
| App tests with disposable PostgreSQL | 303 passed, 3 environment-gated skips |
| `cd web && npm test` | 594 passed |
| `cd web && npm run build` | passed |
| `bash scripts/generate-api-clients.sh` | passed, no generated-client drift |
| `git diff --check` | passed |

The App run also exposed and fixed three stale guards rather than masking them:

- the catalog now pins every currently anchor-targetable signature;
- hosted stock seeding reads the immutable `tapline-4-stock-v1` WASM artifact,
  not the intentionally mutable fast-loop mirror; and
- the hosted execution test now changes a real executable field and creates
  unique test identities.

One pre-existing Tapline-6 movement scene required a literal `+b2` debt trace
that its current geometry no longer produces and already failed at the
baseline commit. It now keeps the owner-visible invariant—no return-lane
staging body may stall for 18 ticks—while focused governor/planner tests retain
the exact debt, right-of-way, and detour policy pins.

## Remaining limits and release posture

- This is an authoring alpha, not a balance pass. The starters are mechanism
  proofs and editable starting points; the six combat outcomes deliberately
  remain uninterpreted.
- Core normal-surface flows were manually exercised in production Chrome.
  The broader signature/host/fact matrix is pinned by automated tests rather
  than manually replaying every combination in one browser session.
- Practice explains only facts present in its immutable canonical evidence. A
  compact or legacy replay that lacks a specific rejected-gate detail is
  labelled unavailable rather than reconstructed from the current draft.
- No hosted playlist, rating era, release, deploy, or tracked tactical WASM
  rebuild was performed. The design loop intentionally used the trusted
  in-process path; the immutable hosted stock seeder remains fixed to its
  frozen artifact until a separate release checkpoint.
