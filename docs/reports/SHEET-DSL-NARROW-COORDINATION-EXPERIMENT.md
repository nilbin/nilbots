# Sheet DSL narrow coordination experiment

Date: 2026-08-09  
Branch: `sheet-dsl`  
Experiment input: `17bc7a1e88d3334fed4a44d1783a35ba1e37a479`
Rebased onto current `main`: `2a53a13eb5e38a3fb298786a82cb7108c0294aca`
Scope: Hunter only; no bulk migration; no `web/` changes  
Runtime: in-process for exhibits, tracked WASM for parity and artifact gates

## Result

The condition-coordinated grammar now has the two small shared primitives the
previous experiment found necessary, without restoring an operation object:

- a **cohort** shares only a count and a deterministic selector;
- a **focus scope** shares only a maximum-attacker count;
- every participating role still declares its own rung, verb, conditions,
  destination, fight behavior, and priority;
- compact `front` / `core` / `rear` posture lives on the rung, not in a shared
  formation catalog; and
- `tempo` and `tempo-active` are gone. One drawn, side-bound patrol route now
  carries the north-then-south beat.

The guardrail is executable, not prose. Unknown fields fail compilation;
single-consumer shared cohorts/focus scopes fail compilation; one-off cohorts
and focus caps inline their two-/one-field object on the consuming rung.

This is a grammar and mechanism result. The six outcomes below are exhibit
context, not a balance measurement or product verdict.

## Revised Hunter

Exact top-level keys (ten):

```text
schema, playbookId, auditStatus, composition, layout, memory, roles,
custodyPolicies, doctrines, conditions
```

`doctrines` contains exactly:

```text
roles, cohorts, focus
```

Legal verb vocabulary:

```text
patrol, intercept, assault, recover, fetch, follow
```

Hunter uses five: `assault`, `fetch`, `follow`, `patrol`, `recover`.
`intercept` remains legal but this sheet does not need it.

There is no authored `operation`, `staff`, `escort`, `muster`, `group`,
`formation`, `engagement`, `supportPolicy`, `coordination`, `authoring`,
`perspective`, or `tempo` plane.

### Line counts

Counts use the same one-space pretty-printed value method as the two preceding
reports. Exact totals include the outer braces.

| authored section | operation gate | condition-only | narrow coordination |
|---|---:|---:|---:|
| schema | 1 | 1 | 1 |
| playbookId | 1 | 1 | 1 |
| auditStatus | 4 | 4 | 4 |
| composition | 10 | 10 | 10 |
| layout | 4 | 4 | 4 |
| memory | 6 | 6 | 6 |
| roles | 168 | 133 | 133 |
| custodyPolicies | 43 | 39 | 39 |
| doctrines | 568 | 427 | 512 |
| conditions | 87 | 120 | 108 |
| **exact file total** | **894** | **747** | **820** |

The final sheet is 73 lines larger than the condition-only prototype and 74
lines smaller than the operation gate. Within the final `doctrines` value,
role ladders are 474 lines, cohorts 22, and focus 14. The increase is the price
of stating every role's cohort, focus, and posture locally; no broad behavior
object returned.

The compiled compatibility payload is **63,644 bytes** against the existing
65,536-byte linked-data limit (97.1%, 1,892 bytes spare). That is a real
pre-bulk-migration constraint. Adding lane rungs to every Lancer and Eyes body
crossed the limit during implementation, so this Hunter uses three source-role
rungs per adaptive pool, each covering four possible bodies. The grammar is not
the source of the bloat; desugaring back into exhaustive legacy IR is.

## The two primitives

### Cohort

Shared source shape:

```json
"cohorts": {
  "north-runners": { "take": 3, "select": "nearest" }
}
```

Each participating body declares membership by referencing the name on its own
rung. There is no `from` list. A single-rung cap is instead written inline:

```json
{
  "patrol": "north-well-loop",
  "cohort": { "take": 1, "select": "nearest" }
}
```

The exact resolver is:

1. On every tick, each live body evaluates its own eligible ladder from top to
   bottom. Role/class/carrier eligibility and the rung's `while`/`until` state
   are applied first.
2. Reaching a non-cohort rung settles that body there.
3. Bodies that reach rungs naming the same cohort form its pool. The compiler
   requires at least two role-rung consumers, at most one reference per role,
   and one identical route or zone destination across all consumers.
4. `nearest` means minimum Chebyshev distance to the side-bound destination:
   the nearest authored waypoint for a route, or the nearest tile of a zone
   rectangle (zero while inside).
5. Sort by that distance, then stable `unitId`; the first `take` exact lives
   are admitted.
6. An admitted body executes **its own rung and order**. A rejected body
   continues down **its own ladder** to the next eligible rung or floor.
7. Membership is recalculated every tick. Movement, death, respawn, and
   condition changes can replace a member. There is no cohort lifecycle,
   timeout, sticky lease, transition, or hidden cooldown.

Direct scheduler tests pin cross-role nearest selection, unit-id tie-breaking,
fallback after rejection, and reselection when distances change.

### Focus

A shared focus record has exactly one field:

```json
"focus": {
  "well": { "maximumAttackers": 3 }
}
```

Rungs naming the same focus scope share that cap per enemy target. They do not
share a target lock, preference list, fight block, order, or lifecycle.
Identical `fight.targets.prefer` lists still converge independently on global
facts such as carrier and weakest. A one-rung cap is inline:

```json
"focus": { "maximumAttackers": 1 }
```

Every non-`fetch` rung must state a shared or inline focus cap. `fetch` has no
focus because custody is a movement overlay and its current fight comes from
the body's active local mode.

## Shared-name consumer audit

Counts below are authored references, before compiler expansion.

### Coordination names

| shared cohort | rung consumers |
|---|---:|
| `centre-runners` | 3 |
| `north-runners` | 3 |
| `south-runners` | 3 |
| `home-guard` | 3 |
| `well-surge` | 3 |

Every pool covers four possible bodies because Carrier or Lancer contributes
two bodies through one role-local rung. `take: 3` therefore restores actual
adaptive substitution rather than merely renaming three fixed participants.

| shared focus | rung consumers |
|---|---:|
| `assassin` | 4 |
| `home` | 3 |
| `patrol` | 10 |
| `well` | 12 |

There are no one-consumer shared cohort or focus names. Compiler tests reject
both shapes and prove their inline equivalents compile.

### Conditions

| condition name(s) | consumers each |
|---|---:|
| `north-birth-soon`, `north-birth-far` | 3 |
| `centre-birth-soon`, `centre-birth-far` | 3 |
| `south-birth-soon`, `south-birth-far` | 3 |
| `window-open`, `window-closed` | 3 |
| `shadow-op`, `shadow-cooled` | 3 |
| `gate-threat`, `home-threat` | 3 |
| `approach-clear`, `home-clear` | 3 |
| `self-veteran`, `self-green` | 1 |
| `ghost-hurt`, `ghost-whole` | 1 |

The four one-consumer predicates remain named for a specific reason: the
current DSL's condition string is a readable Boolean sentence while the
top-level catalog is the sole typed fact/operator/value definition. Inlining a
structured predicate into that string would introduce a second condition
dialect. Unlike a cohort/focus object, the condition name separates a sentence
from its typed fact tuple. This is reported rather than silently exempted; a
future condition-syntax pass may choose a compact typed expression.

### Custody, role, and spatial names

- `well-custody`: 12 consumers — seven doctrine defaults and five `fetch`
  rungs.
- `hunter` as a cross-role subject: two `follow` rungs and two condition
  subjects. `reserve` has seven `overflowRoleId` references. Every declared
  role has exactly one doctrine. Role and class ids are domain identities, not
  reusable behavior objects.
- route references: `centre-well-loop` 3, `north-well-loop` 3,
  `south-well-loop` 3, `stage-loop` 6, `strike-line` 2,
  `shadow-north-long` 5, `shadow-hunt-loop` 1.
- zone references: `centre-farm` 1, `south-farm` 1, `enemy-backfield` 2,
  `midfield` 2, `north-farm` 2, `north-perch` 2, `south-perch` 2,
  `own-approach` 3, `own-home` 5, `well-line` 3.

The one-consumer route/zone names are intentionally shared layout records:
spatial geometry must remain drawable, draggable, side-bound, and hash-pinned
in the layout rather than reintroducing coordinates inside a doctrine rung.
`shadow-hunt-loop` is the clearest example: one consumer, but 33 waypoints and
an east-side reverse binding that would be incomprehensible inline.

## Tempo deletion and patrol behavior

`ArcRelayConsolidatedSheetDsl.Tempo.cs` is deleted. The source has no tempo
table, phase, fact, condition, or extra prowl rung. Authoring `tempo-active`
now fails with a compiler error directing the author to a drawn patrol route.
The compatibility package contains one inertly named `steady` phase.

`shadow-hunt-loop` walks the complete north circuit, descends the west
connector, walks the south circuit, and returns up the connector. Its side
binding reverses the route for the east team. A fight/recover/assault can
interrupt it; returning to the floor rejoins at the nearest reachable waypoint
instead of consulting or resetting a 90-tick state machine. That is the
requested resumable patrol behavior, with its whole beat visible on the map.

## Compact posture and traffic

`front`, `core`, and `rear` are order-local attitudes, not formation ids.
At a shared movement destination the executor places front two team-relative
tiles toward the opponent, rear two toward home, and core on the destination;
same-posture peers spread by stable unit-id ordinal across the perpendicular
axis. No formation catalog, reflow policy, lifecycle, or role list exists.

Hunter-side traffic in the final six-cell exhibit:

| cell | flaps | reversals | blockades | chokes |
|---|---:|---:|---:|---:|
| `w-9001` | 5.51% | 7.37% | 3.50% | 2 |
| `w-9002` | 8.93% | 10.69% | 1.87% | 1 |
| `e-9005` | 7.60% | 9.18% | 1.52% | 2 |
| `e-9009` | 6.61% | 8.09% | 2.41% | 0 |
| `w-9012` | 5.54% | 7.10% | 2.27% | 0 |
| `e-9004` | 6.13% | 8.23% | 1.30% | 0 |

Two same-code, same-sheet mechanism controls isolate the posture offsets in the
registered problem cells:

| cell / posture | flaps | reversals | blockades | chokes |
|---|---:|---:|---:|---:|
| `w-9001`, inert | 7.82% | 10.42% | 1.48% | 0 |
| `w-9001`, one tile | 7.14% | 9.01% | 2.92% | 3 |
| `w-9001`, final two tiles | **5.51%** | **7.37%** | 3.50% | 2 |
| `e-9009`, inert | 8.08% | 9.91% | 1.21% | 0 |
| `e-9009`, one tile | **5.90%** | 8.05% | 2.45% | 1 |
| `e-9009`, final two tiles | 6.61% | 8.09% | 2.41% | **0** |

The compact posture materially reduces flaps and reversals. It also increases
blockades, and the two-tile version created two choke events in `w-9001`.
The one-tile alternative had weaker aggregate flap/reversal reduction and four
total choke events, so the two-tile version ships for this experiment. The
cost is disclosed; this is not a claim that posture is balanced or final.

For context only, the frozen condition-only prototype measured 8.04% / 9.51%
flaps/reversals in `w-9001` and 14.70% / 16.22% in `e-9009`. That comparison
also includes cohort, focus, and patrol changes, so it is not used as the causal
posture proof.

## Six-cell exhibit

[Open the outcome-visible phone gallery](https://distances-printable-restaurant-dale.trycloudflare.com/).

The standard registered cells ran in their registered order against unchanged
`wellwright-v1`. Cards were authored before outcomes and mechanically merged
with final pulses, reactor integrity, charge, completion reason, and tick.

| cell | Hunter result | final score from Hunter perspective | gameplay digest |
|---|---|---|---|
| `w-9001` | win t408 | pulses 3–2; integrity 1–0; charge 0–0 | `dd1e8ee1776a8aa3f88f1c6af92e4c62add8d88d21612a27f6dfdb3f2b48e64f` |
| `w-9002` | loss t397 | pulses 1–3; integrity 0–2; charge 2–0 | `cc7e3a2d475f8d73ff59da0984cdde7a56f5db813ecf613be1113e539fdd8aae` |
| `e-9005` | win t397 | pulses 3–1; integrity 2–0; charge 0–1 | `17ff29024b69f4ad8eaadd18453b36a6fcecc653d0a145caf87165903a85f748` |
| `e-9009` | loss t511 | pulses 2–3; integrity 0–1; charge 2–0 | `db877711070a49dba3e5c59b65e5087e690f075b6bb7af006c4439f649f890d6` |
| `w-9012` | win t491 | pulses 3–2; integrity 1–0; charge 0–0 | `598150d0dddf6c98c062e7ec954dd50cc37d81181125d47a0d88597d84ef2db0` |
| `e-9004` | win t500 | pulses 3–2; integrity 1–0; charge 0–1 | `fc23998d812ac61ba067ca39979b67b2fa6e04b5833c9d5bbcc845507d9a9067` |

Rebuilding the gallery after the score-card correction reproduced all six
digests byte-for-byte. Four Hunter finishes and two Wellwright finishes are
only the record of these hand-picked exhibits; they support no balance claim.

A real public Chromium smoke at 390×844, device scale 2, loaded all six cards
and all six WebGL replays. Every score bug appeared, playback advanced, no page
overflowed horizontally, and no page, console, or request errors were captured.

## What was rejected

- **A cohort `from` list:** membership is derived from role-local rung
  references. Repeating roles would recreate `groups` and
  `authorizedCarrierRoles`.
- **One shared `well-runners` across three destinations:** `nearest` would have
  no single thing to be nearest to. Lane-specific names each have three
  consumers and one exact destination.
- **Single-consumer shared cohort/focus names:** rejected by the compiler;
  inline objects express those cases.
- **Lifecycle, transitions, timeout, orders, fight blocks, formation state, or
  per-role overrides on either primitive:** rejected as unknown fields. That
  is operations returning by accretion.
- **A team-wide attacker cap:** the old 1/1/3/2/2 contexts are not one policy.
  Named focus scopes share only the relevant cap.
- **A shared formation catalog or `wedge`:** compact per-rung posture addressed
  the measured flapping mechanism without restoring 662 lines of formation
  grammar.
- **The one-tile posture:** measured as a mechanism control; it produced more
  total chokes and weaker aggregate flap/reversal reduction than two tiles.
- **A route-cycle configuration or relocated tempo table:** one visible map
  route expresses the beat with fewer concepts.
- **`most-experienced` as a cohort selector:** no Hunter pool needs it in this
  proof. `nearest` is the only selection word shipped; local experience gates
  remain `self-level` conditions. A second selector must earn itself through a
  concrete sheet later.
- **Extra Lancer/Eyes lane-race rungs:** they pushed the generated linked data
  past 64 KiB. The shipped pools still choose three of four live bodies; adding
  more source participation before compacting the IR would be dishonest.

## Remaining limitations and semantic costs

- The generated compatibility IR is almost full; bulk migration should not
  begin without measuring every sheet and likely compacting or directly
  executing role ladders.
- Cohort membership is deliberately non-sticky. A closer body can replace a
  runner every tick; traffic is acceptable in this exhibit, but the grammar has
  no authored hysteresis if another sheet proves it necessary.
- Route distance means nearest authored waypoint, not full walked path cost.
  It is deterministic and visible, but a route with very sparse waypoints can
  make selection less intuitive.
- Contextual focus restores the intended counting scope but does not guarantee
  low cap declines; these six replays contain 18–234 Hunter `cap@` reason ticks.
  That is executor evidence, not a balance finding.
- The posture improvement trades some oscillation for more blockades. A future
  executor-level collision/reflow improvement may be cleaner, but no shared
  formation grammar is justified by this experiment.
- The previous report's hard-coded multi-valued IR inventory still applies,
  except tempo transitions are deleted, cohort distance is route/zone-derived,
  focus scope is explicit, and compact posture now supplies local geometry.
  `participantLoss: replace` remains a compatibility-IR rule for multiple
  bodies of one role; it never recruits another role or decides participation.

## Verification

| check | result |
|---|---|
| `dotnet build BotArena.sln --no-restore` | pass, 0 warnings / 0 errors |
| full `BotArena.Cli.Tests` | pass, 317 / 317 |
| full `BotArena.Engine.Tests` | pass, 1,436 / 1,436 |
| full `BotArena.Determinism.Tests` | pass, 17 / 17 |
| full `BotArena.Runtime.Wasm.Tests` | pass, 68 / 68 |
| tracked tactical WASM source/receipt guard | pass |
| in-process/WASM tactical replay parity | pass |
| every tracked playbook compile | pass, 23 / 23 inside CLI suite |
| narrow primitive compiler/scheduler tests | pass |
| migration builder second run | no byte change; reports already migrated |
| six in-process exhibit matches | complete, no runtime faults |
| public phone-sized Chromium smoke | 6 / 6 WebGL replays, no captured errors |
| `git diff --check` | pass |
| files changed under `web/` | none |
| non-Hunter playbook JSON changed | none |

The tactical mind was rebuilt through the controlled Docker path. Receipt
build-input hash: `2dcdcd870a4e50d25890f50a4c7113322bccc7c66111ec9765abdc40712a3a6f`.
Tracked artifact hash:
`7a5176365371dd950d1c4039c39d1316dee532c44999f42cbf2b92ec4828e1b9`.

No other playbook was migrated. `docs/SHEET-AUTHORING.md` remains the shipped
grammar until the owner rules on this experiment. Stop here; bulk migration is
still explicitly gated.
