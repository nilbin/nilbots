# Sheet DSL Phase 3 — Hunter one-sheet gate

Date: 2026-08-09  
Branch: `sheet-dsl`  
Phase-2 parent: `0874658325c82d5ecd4704152913c5505113fc44`  
Current main/rebase target: `f93bf61249238f5ab4909a756691b00c79605f27`

Status: **Hunter port and parity proof complete; bulk migration not approved
because the one-sheet gate exposed an inter-plane priority design gap.**

## Outcome

`hunter-v1` has been ported completely out of the authored legacy planes, and
its six standard gameplay-state digests are byte-identical to the Phase-2
source. This report is the owner-mandated stop before bulk migration. No other
playbook JSON changed.

The directory currently contains 23 JSON playbooks—Hunter plus 22 others, not
the 21 stated in the handoff—because `home-siege-v3-lib.json` is also present.
All 23 compile, but the other 22 remain on their existing source grammar. No
attempt was made to mechanically migrate them.

The port passes consistency and DRY, and it makes nearly all behavior
comprehensible:

- **Comprehensible:** every role has a doctrine; coordinated behavior is named
  as an operation; repair, traffic, aim, reach, and team tempo are visible. One
  inter-plane priority limitation remains below and prevents calling the whole
  DSL bulk-ready.
- **Consistent:** local and coordinated priority is expressed by ordered role
  lists; operations reuse `patrol`, `assault`, `fight`, custody, and hold words.
- **DRY:** role membership is stated once; shared fights and staffing are stated
  once; repeated race staffing/reach defaults were collapsed; runtime expansion
  is compiler-owned.

It exposes a genuine one-sheet gate failure before bulk migration. Coordinated
operations currently occupy the final band of a role's authored mode ladder,
but Hunter's old runtime gives some shared tasks numeric ties/interleavings with
body-local tasks. Exact observed parity therefore still uses a compiler-owned,
Hunter-shaped operation-priority bridge. The authored source does not fully
explain that tie. The grammar also cannot interleave a body-local mode between
two shared operations, and one shared runtime task cannot have a different
priority for each recruited role.

Hunter's six observed cells do not exercise a conflicting case, so its behavior
is parity-proven, but the true comprehensibility goal is not yet general. Do not
encode more sheets around this restriction. The clean extension to evaluate is
per-role operation-join rungs backed by per-assignment priority in the executor;
an alternative is an explicit, simple fixed operation band if a control exhibit
proves the behavior change acceptable. Authored numeric priorities are not an
acceptable escape hatch.

## Authored surface

The exact final top-level key list is:

```text
schema, playbookId, auditStatus, composition, layout, memory, roles,
custodyPolicies, doctrines, conditions
```

There is no `staffing` key at the Hunter gate. The approved tag-keyed shared cap
is real for `predator-v2`, but accepting and ignoring it now would violate the
DSL's comprehensibility rule. It must be implemented as capacity only when that
sheet is migrated; it may not grow orders, transitions, formation state, or
per-role overrides.

`doctrines` contains exactly:

- `roles` — 7 role-owned ordered mode lists, with base custody, fight, and
  repair policy where applicable;
- `operations` — 6 shared coordinated behaviors with staffing and variants;
- `tempo` — the two-step prowl cycle and the apex interrupt.

The authored source contains no `perspective`, `groups`, `formations`,
`engagements`, `supportPolicies`, `coordination`, `authoring`, `parameters`,
`library`, `formationStableTicks`, or `squad`. The compiler synthesizes the
exhaustive legacy-shaped runtime IR only after validating the consolidated
source.

## Size by source section

Counts are one-space pretty-printed JSON value lines; exact file totals include
the enclosing property lines/braces. “Before” is Phase 2 commit `08746583`.

| authored section | before | after |
|---|---:|---:|
| schema | 1 | 1 |
| playbookId | 1 | 1 |
| auditStatus | 4 | 4 |
| composition | 10 | 10 |
| layout | 4 | 4 |
| perspective | 1 | 0 |
| memory | 7 | 6 |
| roles | 133 | 168 |
| groups | 81 | 0 |
| formations | 662 | 0 |
| engagements | 219 | 0 |
| supportPolicies | 17 | 0 |
| custodyPolicies | 43 | 43 |
| authoring | 556 | 0 |
| coordination | 456 | 0 |
| doctrines | 149 | 568 |
| conditions | 0 | 87 |
| **exact file total** | **2,346** | **894** |

That is 1,452 fewer lines, a 61.9% reduction. The final doctrine value divides
into 214 lines of role doctrines, 331 of operations, and 21 of tempo (wrapper
lines account for the remainder).

## Where the spec was wrong or incomplete

1. **Phases were not merely mode ordering.** Removing the old phase clock
   changed transient movement. Hunter needs one explicit synchronized tempo
   cycle with an apex interrupt and role-specific fallback routes.
2. **Escort alone is not the N-body abstraction.** Well races require a shared
   cardinality request, one formation/fight policy, variant lifecycle, and
   opt-in from every eligible role. A shared doctrine operation is smaller and
   more legible than duplicating the call in seven doctrines.
3. **Repair has two questions, not one.** The medic owns how to repair; the
   assassin operation owns whether that repair channel is active. Folding only
   the policy into the medic made repair leak into unrelated movement.
4. **Two hidden assignment choices affected state.** Old assignment-profile
   right-of-way became `traffic`; old engagement rotation became `fight.aim`.
5. **Movement reach and combat leash are different.** Operation `reach`
   reproduces how far an assigned body may range from formation; fight
   `chase.leash` controls target acquisition. One `leash` could not express both.
6. **A referenced fight must remain shared.** Cloning a fight block for each
   operation reset the coordinated target lock. An operation may reference
   another operation's fight so all participants use one lock owner.
7. **The corpus count moved.** There are 22 non-Hunter JSON playbooks at this
   checkpoint. The stop rule, not the stale count, controls: none were migrated.

## Multi-body coordination read

The operation is the smallest truthful shared unit:

- each role declares consent and relative priority in its own final `muster`
  list;
- the operation selects a role or tag with `minimum / preferred / maximum`;
- all role ladders are combined as a partial order, and a cycle is a compile
  error;
- one operation variant owns movement, formation, fight, hold/release, repair
  participation, and traffic intent;
- participant loss uses deterministic replacement in the existing executor;
  bodies that are no longer leased fall through to their next doctrine rung.

Only two named shapes are shipped:

- `file`: required by Hunter's assassin run and reserve home line;
- `screen`: required by Hunter's defense, Well race, surge, and Well patrol.

Both shapes are currently justified by exactly one migrated sheet. `wedge` is
rejected. Formation offsets are compiler logic, never authored tile lists.

## What the grammar still cannot express

- a shared tag capacity such as Predator's “2 of these 4” (approved shape,
  deliberately deferred because Hunter does not use it);
- a local mode interleaved between two coordinated operation rungs, or
  per-recruit priority inside one shared operation; the current parity bridge
  also leaves old local/operation priority ties implicit;
- an operation spanning two independently paced executor cohorts (currently a
  compile error rather than hidden group tracks);
- a general arbitrary phase graph; Hunter proves only cycle + interrupt tempo;
- more than the proven `file` and `screen` formation shapes;
- an operation with multiple repair-provider policies without an explicit way
  to select one;
- author control over Core choice, abandonment, and handoff receiver—the
  unimplemented Phase 4 question;
- legacy group local-state machines whose behavior is not reducible to a named
  condition/tempo transition. Each such sheet needs an explicit design read,
  not a mechanical group rename.

## Memory, conditions, and removed wrappers

The final self-fact set remains the Phase-1 set:

```text
self-health, self-level, self-carrying, self-stuck-ticks, self-mode-ticks
```

They are exact-life, candidate-scoped, subject-free, and legal only from a
doctrine mode. No additional self fact was needed for Hunter Phase 3.

The four authored memory windows remain live and are read by the mind:
`enemyUnavailableTicks`, `lastSeenEnemyTicks`, `securedCoreTicks`, and
`objectiveProgressTicks`. `formationStableTicks` disappeared from source. A
fixed compatibility value remains in generated IR while the v1 runtime package
still carries its exhaustive formation bookkeeping; no authored condition can
read it.

`authoring.predicates` became top-level `conditions`. The otherwise-empty
`authoring` wrapper, its one-off parameter/library paths, constant
team-relative `perspective`, and standalone support plane are absent from source.

## Parity evidence

Baseline replays were captured at Phase 2 before Phase-3 authoring. The final
six-cell exhibit used the standard cells in their registered order. Every pair
has the same `arc-relay-state-digest.py` gameplay identity:

| cell | before | after | terminal record |
|---|---|---|---|
| w-9001 | `384a189642a5f0b658e1c23a49e6e9c97a525c043c2a97c35270d5fadf2c5875` | same | Hunter, reactor destroyed, t348 |
| w-9002 | `e85c3c0c3b6d75247c574f8d15161840fca3f0cd3c9d076cf874faccf3dfd06c` | same | Hunter, reactor destroyed, t403 |
| e-9005 | `6ce55a75b841eb59e95397a2d1af8c1a71d3911669ef5f386bf66642cf6aa936` | same | Hunter, reactor destroyed, t437 |
| e-9009 | `fb75acbb2b4c62510c01fca269a120c683c07bf7676710ca1d592a40095c4c35` | same | Wellwright, reactor destroyed, t476 |
| w-9012 | `0bbe86b7072d50cf18f1825e58f655ce71fad3d1568f7c7dc67fb876156dc810` | same | Hunter, reactor destroyed, t335 |
| e-9004 | `e063ef59128078d02093ce2331baf1adebe7fa5b21749063257e406b5302f359` | same | Hunter, reactor destroyed, t322 |

Public phone gallery (public and unauthenticated):
[Sheet DSL Phase 3 Hunter gate](https://substitute-fingers-happens-criterion.trycloudflare.com/).

The detached server/tunnel use port 8974. A real Chromium smoke at 390×844,
device scale 2, loaded all six index cards, opened `sample-01`, reached the
ready overlay and WebGL canvas, displayed the Arc Relay score bug, advanced the
playhead, and observed the replay JSON delivered with `Content-Encoding: gzip`.
It recorded no page, console, request, or horizontal-overflow failure.

This is an exhibit, not a battery and not a balance claim.

## Phase 4 proposal — not implemented

The proposal is recorded separately in
[`docs/design/SHEET-DSL-PHASE-4-COURIER-PROPOSAL.md`](../design/SHEET-DSL-PHASE-4-COURIER-PROPOSAL.md).
It keeps custody as the shared protocol and proposes a body-owned `courier`
policy for ordered Core preference, reach, abandonment, and handoff intent.
`pass` was rejected as ambiguous with Arc Relay's distinct ranged forward pass.
No Phase-4 compiler, mind, engine, or source behavior was built.

## Sheet-editor migration note

No `web/` file changed. The sibling editor should migrate atomically to this
source shape after owner approval:

1. render only the ten root keys above;
2. edit `roles[].tags` and `roles[].formationPosture` with the role itself;
3. replace the flat doctrine catalog with `doctrines.roles`,
   `doctrines.operations`, and `doctrines.tempo`;
4. make each role's operation-consent list ordered and show cycle/consent errors
   returned by the shared compiler on the field that caused them;
5. provide reusable operation editors for staff selectors/cardinalities,
   variants, `file | screen`, fight, repair participation, traffic, and reach;
6. move predicate editing to top-level `conditions`;
7. remove UI/round-trip support for perspective, groups, authored formations,
   engagements, support policies, coordination, authoring wrappers, parameters,
   libraries, and formation-stability memory;
8. do not expose `staffing` or Phase-4 courier controls yet.

The editor must continue sending source to the shared compiler; it should not
reimplement operation partial-order, consent, tempo, or IR expansion rules.
`docs/SHEET-AUTHORING.md` intentionally remains the currently shipped mixed
grammar contract until the owner resolves the priority gate; publishing this
provisional Hunter shape there would incorrectly tell the editor agent that the
bulk format is settled.

## Verification

| check | result |
|---|---|
| `dotnet build BotArena.sln --no-restore` | pass, 0 warnings / 0 errors |
| full `BotArena.Cli.Tests` | pass, 308 / 308 |
| full `BotArena.Engine.Tests` | pass, 1,436 / 1,436 |
| full `BotArena.Determinism.Tests` | pass, 17 / 17 |
| consolidated compiler-focused tests | pass, 73 / 73 (included above) |
| every tracked playbook compile | pass, 23 / 23 (included above) |
| migration script second run | no byte change; reports already migrated |
| standard state-digest pairs | 6 / 6 exact matches |
| phone-sized public-browser smoke | pass, no captured failures |
| `git diff --check` | pass |
| files changed under `web/` | none |
| non-Hunter playbook JSON changed | none |

The final clean-tree Phase-3 commit SHA is cited in the handoff because a commit
cannot truthfully embed its own hash in its contents.
