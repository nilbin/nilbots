# Consolidated sheet DSL — hard-five checkpoint

Date: 2026-08-09  
Branch: `sheet-bulk`  
Base: `87e4347cf42260abd8cfe24365b8d9a65818fb77`  
Implementation checkpoint: `6eb00b684d0ff727da616c6335551fb3aa913bb4`  
Status: checkpoint complete; five hard sheets migrated; seventeen sheets untouched; not merged

## Result

The five designated hard sheets now compile to ATL1 and run through the generic
direct role-ladder executor:

- `home-siege-v3`
- `predator-v2`
- `ripen-harvest-v1`
- `breakwater-v1`
- `trifold-balanced-v1`

Each has exactly the ten consolidated root keys:

```text
schema, playbookId, auditStatus, composition, layout, memory,
roles, custodyPolicies, doctrines, conditions
```

`doctrines` has exactly `roles`, `cohorts`, and `focus`. None of the five
contains `groups`, `formations`, `engagements`, `coordination`, `authoring`,
`supportPolicies`, or `tempo`. The legal executor vocabulary remains exactly
`patrol`, `intercept`, `assault`, `recover`, `fetch`, and `follow`; these five
sheets author every verb except `recover`.

This is the requested early checkpoint. The other seventeen playbooks have not
been migrated, `docs/SHEET-AUTHORING.md` has not been changed, and no legacy
path has been deleted.

## Source and payload measurements

Line counts are exact file line counts against the branch base. Package sizes
come from `nilbots experiment arc-relay-playbook` and include the normalized
sheet, bound layout, and ATL1 envelope.

| sheet | old lines | new lines | change | ATL1 bytes | limit share | headroom | ATL1 SHA-256 |
|---|---:|---:|---:|---:|---:|---:|---|
| Home Siege v3 | 2,005 | 826 | -1,179 | 13,403 | 20.5% | 52,133 | `5a077318074ff9fa09ecf4a84e755f6ec856f4b5bb866fe03518c01cd5fea0c4` |
| Predator v2 | 850 | 420 | -430 | 6,500 | 9.9% | 59,036 | `d739699859c58357fc73a8cd2f88edb24afc2756916d3bbc034f212462394323` |
| Ripen Harvest v1 | 835 | 334 | -501 | 5,670 | 8.7% | 59,866 | `c2f1e1707d7e9aad978d3b4002128a1dc75f27b4e0ec6ca325a254e0e3ad8bcf` |
| Breakwater v1 | 1,257 | 549 | -708 | 8,335 | 12.7% | 57,201 | `8c145445a903968efc62d471f40bc785e37b4ddb26e980fb93c7293375e22ffc` |
| Trifold Balanced v1 | 1,473 | 455 | -1,018 | 7,182 | 11.0% | 58,354 | `53cef753154fbb034b46047d3429fcf7880e0cb341b4b6dfd3e18d376c78644e` |

The largest package is 13,403 bytes. No migrated sheet is near the 65,536-byte
ceiling that prompted the direct-executor work.

## What each hard migration taught us

### Home Siege v3

The old `line-group` really was a two-state local state machine. It was not
mechanically renamed. The replacement makes the state legible in each body's
own ladder:

1. regroup at the forward rally while four or fewer bodies live, or line
   cohesion is low;
2. leave regroup only when two line bodies are at rally, at least five bodies
   live, and cohesion is restored;
3. rush the outer route, breach by the short route, then occupy the bank seal;
4. let one explicit interceptor cut a visible carrier or follow the unique
   runner during a safe harvest window.

This gives every participant a stated rung and removes shared transition state.
The semantic cost is that the latches are role-local and repeat the same
condition sentence. That is preferable to restoring an operation or stateful
group, but it is real duplication.

One capability remains genuinely inexpressible: “follow whichever friendly
body currently carries a Core.” `follow` names a role. Home Siege can say
`follow: runner` only because `runner.maximum` is one. A general dynamic-carrier
subject would need a separate design; this checkpoint did not add one.

### Predator v2

The shared two-of-four orchard allocation is exactly what `cohorts` was built
to express. Four specialist ladders independently offer the same orchard rung;
the `orchard` cohort admits the nearest two. Rejected bodies continue to their
centre rung. The old class-filtered bait assignment became an explicit `bait`
role, so participation is authored rather than silently inferred by the
executor.

No grammar addition was needed. This migration also exposed two generic bridge
bugs: direct sheets needed the authored role's casualty policy rather than a
hard-coded fallback, and `no-live-friendlies` needed to be available as the
documented implicit bait-reclaim condition. Both are now compiler/runtime tests.

### Ripen Harvest v1

The two custody policies are expressed by ladder position: specialists fetch
`orchard-custody`; the runner tries `orchard-custody`, then `farm-outer`, before
its patrol rungs. `orchard-worth` remains a named typed condition because it is
the safe-conversion rule of the centre policy. Removing it as “unused” caused
the exact final sheet compilation to fail and was corrected before this report.

No grammar gap was found. Fetch remains a custody movement overlay, so the
trace continues to show the active fight/movement rung while authorized pickup
reasons identify fetch execution.

### Breakwater v1

The three custody policies survive without an authorized-role list. The old
class-filtered counter-courier assignment is an explicit `interceptor` role with
its own carrier-intercept rung, approach condition, and patrol floor. Every
other role states its own home-threat and well-line priorities.

No grammar addition was needed. The compiler's legacy task-ownership lint and
its assumption that `supportPolicies` was non-empty both incorrectly leaked
onto direct sheets; those checks are now confined to the legacy path.

### Trifold Balanced v1

The old sheet depended on prototype socket-phase facts that the current
ambush-11 rules do not publish. A literal port compiled but parked all bodies at
the centre floor. That was stale source semantics, not a missing DSL word.

The current sheet states its intended three-theater behavior directly. Every
role offers a north rung and a south rung. `north-wing` and `south-wing` each
take the nearest three; bodies rejected from both fall through to centre. This
produces an adaptive 3/3/2 spread with no hidden phase or role roster inside
either cohort. It is an intentional behavior rewrite, not parity work.

## Native Warren layouts

All five prior layouts targeted a different map contract: four named the
Counterflow map and Predator named `arc-relay-ambush-warren-01`. The current
hosted line is `arc-relay-ambush-warren-06`; compiling a sheet is not permission
to geometrically project its coordinates.

Three Warren-06 layouts were authored natively:

- `warren6-home-siege-v3.json`
- `warren6-predator-v2.json`
- `warren6-ripen-harvest-v1.json`

Breakwater and Trifold use the existing native `wellwright-warren6-v1.json`.
Home's east binding rotates its route geometry by 180 degrees and aliases the
two rush paths. The west/east exhibit confirms both sides reach the intended
theaters rather than old-map coordinates.

## Shared-name consumer audit

Counts are authored references, before compiler normalization. The compiler
rejects a named cohort or focus scope with fewer than two rung consumers.

| sheet | named cohorts | named focus scopes |
|---|---|---|
| Home Siege | none | `regroup` 4, `rush` 9, `siege` 4 |
| Predator | `orchard` 4 | `trap` 4, `run` 8 |
| Ripen Harvest | none | `orchard` 4, `run` 3 |
| Breakwater | none | `choke` 4, `front` 10, `run` 3 |
| Trifold | `north-wing` 5, `south-wing` 5 | `front` 18, `run` 3 |

No one-consumer shared coordination primitive survives.

Condition consumers are:

- Home Siege — `six-in-siege` 9; `five-live`, `four-live-or-fewer`,
  `line-two-at-rally`, and `line-cohesion-restored` 8 each;
  `three-or-fewer-at-bank-seal` and `line-cohesion-low` 4 each;
  `front-attrition-safe`, `five-in-siege`, `five-at-bank-seal`, and
  `secured-core` 2 each; the other ten condition names have one consumer.
- Predator and Trifold — no named conditions.
- Ripen Harvest — `orchard-worth` 1.
- Breakwater — `approach-mass` and `approach-clear` 4 each;
  `approach-lingering`, `visible-enemy-carrier`, and
  `no-known-enemy-carrier` 1 each.

Single-consumer conditions are retained for the same explicit reason as the
Hunter gate: the top-level record is the only typed fact/operator/value form,
while a rung carries a readable Boolean condition sentence. Inlining the fact
tuple into that string would introduce a second condition dialect.

Custody-policy reference counts are:

- Home Siege — `secured-conversion` 2, `surge-harvest` 3,
  `incidental-delivery` 6.
- Predator — `farm-custody` 6, `bait-custody` 2.
- Ripen Harvest — `orchard-custody` 9, `farm-outer` 2.
- Breakwater — `farm-north` 1, `farm-south` 1,
  `incidental-delivery` 12.
- Trifold — `farm-north` 2, `farm-south` 1,
  `farm-centre` 1, `incidental-delivery` 9.

A custody policy is a domain record containing sources, reservation and
delivery clocks, and recovery rules, and is referenced by both doctrine
defaults and `fetch`. Its one-consumer instances represent different Well
contracts; inlining one would add a second custody shape.

One-consumer routes and zones also remain in their hash-pinned layout. Geometry
must stay drawable, draggable, and side-bindable rather than reappearing as
coordinates inside a rung. Across the five sheets, their sheet-side reference
counts are:

- Home Siege — `outer-rush` 4, `short-breach` 4, `bank-seal` 8,
  `forward-rally` 5, `siege-leash` 3, `enemy-bank-danger` 2; four other
  spatial names have one consumer.
- Predator — `kill-pocket` 6, `centre-farm` 5; `north-out` and `south-out`
  have one each.
- Ripen Harvest — `centre-farm` 6; `north-out` and `south-out` have one each.
- Breakwater — `well-line` 5, `own-home` 4, `own-approach` 3; `north-out`,
  `south-out`, and `home-gate` have one each.
- Trifold — `north-farm`, `centre-farm`, and `south-farm` have five each.

## Direct-executor/runtime fixes

The five-sheet pass found generic defects rather than sheet-id exceptions:

- casualty behavior is derived from each authored role;
- direct sheets receive the documented implicit `no-live-friendlies` fact;
- direct condition evaluation accepts the authored aliases `live-count`,
  `joining-count`, `in-zone-count`, `cohesion`, and `stuck-ticks`;
- the off-path legacy validator accepts a direct sheet with no support-policy
  rows; and
- legacy task-ownership warnings no longer run against direct ladders, while a
  cohort rung is not falsely diagnosed as an unconditional floor.

No playbook id or hard-sheet name was added to the selector. Regression tests
compile all five source files and pin each hard translation.

Because the tactical mind source changed, its tracked WASM was rebuilt through
the controlled Docker-backed `nilbots build --no-cache` path:

| item | SHA-256 |
|---|---|
| build input | `45413cf967e42c22377f1f238bd571ac6b1334bbfc8b269d243731d641030351` |
| WASM artifact | `7a4197c052ea20035f388a9d7efd19f2e0b4c4365295f3bfaf1cd3cc1fb88d2d` |

One Home Siege cell was run through both hosts with identical source, sheet,
layout, map, rules, and seed. Replay container hashes differ because runtime
provenance differs (`9c0c37d6…` WASM, `c89f6d17…` in-process), but the gameplay
state digest is byte-identical:

```text
4feee82cf09019555194af4270ccc29a7071afa4a90e4f35b701796a6e6ecba2
```

Both runs ended by reactor destruction at tick 339.

## Behavior exhibit

[Open the outcome-visible six-match phone gallery](https://commissioners-mainstream-host-scheduled.trycloudflare.com/).

Home Siege, Predator, and Trifold were chosen because they made the three
largest semantic moves: a shared state machine became role-local ladders, a
class-filtered staffed trap became a derived cohort plus explicit bait role,
and an obsolete socket phase became a current-rule 3/3/2 spread. Each has one
west and one east cell against unchanged `wellwright-v1`.

| card | seed/side | recorded result |
|---|---|---|
| Home Siege | 9001 west | Home 2–0 pulses; max ticks 599 |
| Home Siege | 9005 east | 2–2 pulses; max ticks 599 |
| Predator | 9002 west | Predator 3–2 pulses; reactor at 460 |
| Predator | 9009 east | Wellwright 3–0 pulses; reactor at 314 |
| Trifold | 9012 west | Trifold 3–0 pulses; reactor at 309 |
| Trifold | 9004 east | Trifold 3–1 pulses; reactor at 363 |

These are exhibits, not a battery, and the outcomes are not interpreted as a
balance result.

The authored-sheet traffic reads (west/east respectively) were:

| sheet | blockades | flaps/body-ticks | reversals/body-ticks |
|---|---:|---:|---:|
| Home Siege | 1.82% / 3.51% | 11.04% / 13.08% | 12.90% / 14.70% |
| Predator | 0.54% / 2.19% | 4.10% / 4.39% | 5.51% / 6.36% |
| Trifold | 0.52% / 0.79% | 9.75% / 6.88% | 11.97% / 9.47% |

All six sheet-side reads recorded zero own-contest blockades. Home's higher
flap/reversal rate remains visible evidence to review; it was not tuned away or
called acceptable from six cells.

Trace output names authored rungs. In the west Home cell it records regroup,
seal, breach, intercept, follow, and rush selections, including
`line[1]/patrol`, `line[4]/patrol`, `line[5]/assault`,
`interceptor[2]/intercept`, and `interceptor[3]/follow`. Predator records both
orchard occupants and all three runner floors. Trifold records north, south,
and centre rung positions across multiple roles.

The six compressed broadcasts total 952,711 bytes (0.91 MiB). The largest is
218,012 bytes (212.9 KiB), within both the 300 KiB per-match and 8 MiB gallery
budgets. A 390×844 touch-capable Chromium smoke opened all six public cards as
WebGL, found the score HUD, advanced playback, and recorded no page, console,
or request errors.

Ripen Harvest and Breakwater were also run once each for mechanism smoke, not
added to the gallery. Ripen exercised both runner collection lanes and 12
authorized pickup actions. Breakwater exercised the home/well floors and 100
authorized pickup actions. Neither recorded an own-contest blockade.

## Verification

- `dotnet build BotArena.sln` — passed, 0 warnings / 0 errors.
- `dotnet test tests/BotArena.Cli.Tests` — 326 passed.
- `dotnet test tests/BotArena.Engine.Tests --no-build --no-restore` — 1,436 passed.
- `dotnet test tests/BotArena.Runtime.Wasm.Tests --no-build --no-restore` — 68 passed.
- `dotnet test tests/BotArena.Determinism.Tests --no-build --no-restore` — 17 passed.
- `npm run build` in `web/` — passed; used to build the public gallery.
- `npm test` in `web/` — 435 passed, 2 environment-only failures: the new
  worktree does not contain the intentionally untracked Meshy normalized source
  GLBs used by the KTX2 reproducibility checks. Runtime asset/hash tests pass,
  and those exact missing inputs exist in the parent checkout. No web source is
  changed by this branch.
- Public phone WebGL smoke — six of six replays loaded and advanced with zero
  browser errors.
- `git diff --check` — passed.

## Follow-up after all 23 sheets migrate

Do not start this cleanup at this checkpoint. Once the remaining seventeen
sheets are consolidated, remove the legacy desugarer, ATP1 link path, and
`ExpandAuthoring` off-path validation oracle. The in-memory action adapter's
legacy order/engagement record types are then the final holdout and should be
scoped separately. Only after every sheet is migrated should
`docs/SHEET-AUTHORING.md` be rewritten to contain the consolidated grammar and
nothing else.
