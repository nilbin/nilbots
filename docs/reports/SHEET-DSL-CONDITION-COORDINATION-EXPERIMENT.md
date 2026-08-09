# Sheet DSL — condition-coordination experiment

Date: 2026-08-09

Branch: `sheet-dsl`

Parent Hunter gate: `74da0429`

Experiment runtime: in-process for the six-cell exhibit; WASM for the runtime-parity gate

Status: **the requested smaller Hunter grammar is built and exhibited. Do not
bulk-migrate the other 22 sheets yet.**

## Answer

The experiment validates the central direction and rejects its purest form.

- Follower-owned `follow` is better than caller-owned `escort` plus
  follower-owned `muster`. It is readable in one role, deterministic, and needs
  no consent handshake. Neither `escort` nor `muster` earns its way back.
- A full `operation` object does not earn its 331 authored lines back. Local
  ladders sharing conditions can coordinate useful movement and eliminate the
  operation/local priority-band problem entirely.
- Broad `staff` rows do not earn their old shape back. However, the ability to
  choose **N live bodies from an authored pool** is real behavior that exact
  role lists cannot reproduce. That capability needs one small, single-purpose
  allocation primitive before bulk migration.
- One team-wide focus cap is not an adequate replacement for contextual
  attacker caps. The six cells produced 49–204 Hunter-side `cap@` declines each,
  versus 2–55 in the operation control. A small named focus/cohort scope, or an
  equally explicit alternative, is still needed; deriving scope silently from
  conditions or destinations would fail comprehensibility.
- Shared formation shape is visibly absent. Before restoring a shared formation
  object, the next design should test a compact posture on each body's own rung.

So the best next grammar is not the old operation grammar and not the completely
shared-object-free prototype. It is the prototype's local ladders and
follower-owned `follow`, plus the smallest explicit primitives that restore
adaptive allocation and contextual focus. No A-vs-B operation priority ruling
is needed because there is no operation band.

This is an exhibit and a grammar result, not a balance verdict.

## The rebuilt Hunter

The exact final top-level key list remains ten keys:

```text
schema, playbookId, auditStatus, composition, layout, memory, roles,
custodyPolicies, doctrines, conditions
```

`doctrines` contains exactly:

```text
roles, focus, tempo
```

There is no authored `operations`, `staff`, `escort`, `muster`, `groups`,
`formations`, `engagements`, `supportPolicies`, `coordination`, or `authoring`.
Every role has one ordered `modes` list. Priority is list position inside that
role, with no second priority plane and no numeric priority in source.

The legal verb vocabulary is exactly:

```text
patrol, intercept, assault, recover, fetch, follow
```

Hunter uses five of the six:

```text
assault, fetch, follow, patrol, recover
```

`intercept` remains legal but this sheet does not need it.

### Size

Counts use one-space pretty-printed JSON values, the same method as the Hunter
gate. Exact file totals include property wrappers and outer braces.

| authored section | operation gate | condition prototype | delta |
|---|---:|---:|---:|
| schema | 1 | 1 | 0 |
| playbookId | 1 | 1 | 0 |
| auditStatus | 4 | 4 | 0 |
| composition | 10 | 10 | 0 |
| layout | 4 | 4 | 0 |
| memory | 6 | 6 | 0 |
| roles | 168 | 133 | -35 |
| custodyPolicies | 43 | 39 | -4 |
| doctrines | 568 | 427 | -141 |
| conditions | 87 | 120 | +33 |
| **exact file total** | **894** | **747** | **-147 (-16.4%)** |

Doctrine values moved as follows:

| doctrine subtree | operation gate | condition prototype |
|---|---:|---:|
| roles | 214 | 410 |
| operations | 331 | 0 |
| focus | 0 | 3 |
| tempo | 21 | 12 |

Deleting 331 operation lines saved only 147 total lines. Exact participation,
fight, destination, and trigger intent moved into the seven local ladders. The
result is conceptually smaller, but not dramatically smaller as text; shared
facts have become repeated statements. The `shadow-op` / `shadow-cooled` pair,
for example, appears on Hunter assault, Medic follow, and Lancer follow.

The source SHA-256 is
`11e32ee1b54a1371669b1696bba56183d69ff034ca29a6741f6ae3aab3e7522c`.
The compiler emits a 61,999-byte ATP1 package, under the unchanged 65,536-byte
limit, with SHA-256
`99a00a9c1a3f21713ed94adaef65c9ca1b49374a57ddb821a8bee396d2d3e975`.
The migration builder is idempotent: a second run makes no byte change.

## How `follow` resolves a role

`follow: "hunter"` names a role, not a particular body. The prototype's one
stated rule is:

1. consider live bodies holding the named role;
2. choose the body with the smallest Chebyshev distance from this follower;
3. break an equal-distance tie by stable unit id.

The executor may therefore decide **which** body satisfies the role reference,
but the sheet alone decides **whether** this follower participates and where
that intent ranks. A multi-body reference such as `follow: "carrier"` is thus
predictable; a compiler regression test proves it emits `selector: "nearest"`.
A role cannot follow itself. If no live leader exists, the rung is ineligible
and the body falls to its next local rung.

Followers with the same leader are placed in stable follower-unit order. Their
authored `trail | screen` posture remains local to their own rung. The leader
does not wait, consent, declare a call, or carry follower state.

## The expected breaks, checked

### 1. Cardinality from a pool — genuinely inexpressible

The operation gate's Well race could choose three available runners from a pool
whose roles could hold six bodies, plus an optional Medic. The prototype names
Hauler, both Carriers, and Medic by putting a race rung on those role ladders.
If one is dead or displaced, a Lancer or Eyes body cannot substitute.

The assassin operation likewise selected the most experienced body from the
tagged Hauler/Carrier/Lancer pool. The prototype can say “Hauler assaults while
its own level is at least 3,” but cannot say “whichever eligible body has the
most experience.” `self-level` is a correct candidate condition; it is not a
cross-candidate selection rule.

Exact naming is easier to read, but the exhibit shows that loss of substitution
matters in practice: in `w-9001` both Lancers spend 99% and 85% of sampled time
in own territory, while a control Lancer participated in Well races/escort and
spent 22% in enemy territory. A death can remove race capacity instead of
causing an authored substitute to fill it.

Verdict: keep the behavior, not old `staff`. A narrow row such as “take 3 from
these authored roles, using this authored selector” earns a design pass. It may
not grow transitions, orders, formation state, or per-role overrides.

### 2. Shared formation shape — genuinely inexpressible in this prototype

`follow` can express a follower's relation to a leader. Independent patrol or
assault rungs cannot express a shared file/screen around a destination. Bodies
with matching conditions and targets converge, but they do not reserve a common
shape, common pace, or spacing.

The traffic read makes the visual cost concrete:

| cell | source | flaps | reversals | blockades | chokes |
|---|---|---:|---:|---:|---:|
| w-9001 | operation control | 5.08% | 8.13% | 1.87% | 0 |
| w-9001 | condition prototype | 8.04% | 9.51% | 1.90% | 0 |
| e-9009 | operation control | 5.03% | 8.11% | 2.99% | 3 |
| e-9009 | condition prototype | 14.70% | 16.22% | 2.19% | 0 |

The prototype sometimes blocks less, but reverses and flaps substantially more.
This does not prove the old `file | screen` object is the right interface. It
does prove that deleting shape is a behavior loss visible in the exhibit. Test
per-rung posture before restoring a shared formation catalog.

### 3. Shared target convergence — partly better elsewhere; one global cap is worse

Global target facts do most of what the shared lock promised. Bodies with the
same ordered `targets.prefer` see the same carrier/health facts and deterministically
rank the same enemies. The executor's team-policy accounting then prevents the
same target from exceeding the cap across otherwise independent engagements.
No operation object is required merely to agree that a visible carrier matters.

The remaining cap cannot be one team constant. Hunter intentionally used
different caps for assassin, home defense, Well work, and patrol contexts. The
prototype sets `doctrines.focus.maximumAttackers: 2` globally. Hunter-side cap
declines (`cap@` in executor reasons) were:

| cell | operation control | global-cap prototype |
|---|---:|---:|
| w-9001 | 7 | 94 |
| w-9002 | 11 | 52 |
| e-9005 | 55 | 113 |
| e-9009 | 2 | 49 |
| w-9012 | 15 | 204 |
| e-9004 | 2 | 138 |

This is not a small edge effect. One team-wide number makes unrelated fights
compete for the same allocation budget. A cap needs an explicit context shared
by the relevant local rungs. That is a small residual identity problem, not a
reason to restore variants, staffing, lifecycle, movement, and fight into one
large operation object.

### 4. Co-timing — expressible but duplicated, with visible edge drift

Hunter assault, Medic follow, and Lancer follow each repeat:

```json
{ "while": "shadow-op", "until": "shadow-cooled" }
```

That is comprehensible—each role says exactly when it participates—but it is
not DRY. All three currently share the same pair, so there is no source-level
timing discrepancy. Runtime edges can still differ because recover outranks
follow on the follower's own ladder and because leader availability is an
additional follower condition. This is intended local priority, not a hidden
caller contract.

The body-follow semantic shift is large enough to see, not merely theoretical:

| cell | follow while Hunter assaults | follow while Hunter does something else |
|---|---:|---:|
| w-9001 | 12 ticks | 43 recover ticks |
| w-9012 | 98 ticks | 140 recover + 27 floor + 2 patrol ticks |
| e-9004 | 58 ticks | 26 recover + 33 floor ticks |

Following a wounded Hunter into recovery is better for Medic. Applying the same
body-follow semantics to both Lancers is overbroad: they can keep screening a
leader after the assault mode is gone. The grammar should retain body-follow,
but the author must be able to choose the follower condition precisely; no
mode-sensitive hidden stop should be reintroduced.

### 5. `participantLoss` — the thesis was not quite correct

There are no cross-role tasks in the generated prototype: each task has one
role and every decision to participate comes from that role's own rung. But a
role may hold multiple bodies—Carrier and Lancer each hold two—so a role-local
task can still have multiple participants. The compatibility IR therefore still
uses `participantLoss: "replace"` for deterministic same-role replacement.

It no longer recruits a different role or decides whether a role joins. A future
executor that evaluates ladders directly per body could remove this field. The
current bridge cannot honestly call it moot, so it was not deleted or hidden.

## Hard-coded multi-valued IR fields

The doctrine desugarer still emits an exhaustive compatibility package. The
following fields have multiple legal IR values but are currently fixed or
derived rather than authored. Listing them does not imply they all belong in
the player DSL; it is the requested opacity inventory.

| area | fixed/derived value in this prototype |
|---|---|
| perspective | `team-relative` |
| role-group membership | `stable-slot`, `hold-vacancy`, `never`, `unassigned` |
| role-group state | one `active` state, minimum 0 ticks, no transitions |
| assignment profile | priority 20, all members, active state, `repath` |
| task activation/lifecycle | `while-true`, `higher-priority`, `replace`, stable 1, minimum 8, timeout 600, cooldown 2, minimum primary 1 |
| task completion | never complete; authored `until` becomes failure; all tempo phases eligible |
| reintegration | `primary-order`, no release orders/condition, timeout 0 |
| assignment cardinality | role preferred/maximum; minimum 1 only for recover and 0 otherwise |
| candidate selection | own-reactor distance; recover uses heal-beacon distance 64 |
| carrier eligibility | `allow` for ordinary doctrine assignments |
| arrival/completion | recover radius 0, intercept radius 1, other radius 2; always `continuous` |
| assignment fallback | ordinary modes `continue-with-alternate`; standing floors hold invalid targets |
| movement pace | always `free` |
| movement recovery | `repath`; stuck threshold 10 for patrol/assault, 8 for intercept/recover/follow |
| patrol/intercept micro | patrol scan `sweep`; intercept lead 1; follow's leaderless movement is hold |
| follow | selector `nearest`, leash 2; stable unit id tie-break is executor logic |
| formation | empty id; generated compatibility `formationStableTicks` remains 3 |
| engagement tie/lock | distance, health, unit id, life id; lock 4; `urgent-carrier` preemption |
| engagement coordination | `team-policy` in the prototype; one authored team maximum |
| engagement damage | overkill 0, `damage-first`, `rotate-to-engage` |
| engagement release | outside-leash/destroyed true; hidden/unreachable default 2/3 unless fight persistence overrides |
| condition scaffolding | generated `always`, `no-live-friendlies`, `convert-freely`, and `task-never-done` |
| tempo transitions | interrupt priorities 10+, cycle priority 20, respect minimum, success into interrupt/cycle and failure out of interrupt, cycle stable 2 |

The most consequential items are task lifecycle, assignment completion/fallback,
movement micro, and engagement lock/release. The next consolidation should
decide each as either a documented executor invariant or an authored word; it
must not leave a player-significant choice as an unexplained compiler constant.

## Six-cell exhibit

The standard cells ran in the registered order against unchanged
`wellwright-v1`: `w-9001`, `w-9002`, `e-9005`, `e-9009`, `w-9012`, `e-9004`.
The operation gate is the control. Every prototype gameplay digest changed, as
expected for this semantic experiment.

| cell | terminal control → prototype | control digest | prototype digest |
|---|---|---|---|
| w-9001 | Hunter t348 → Wellwright t475 | `384a189642a5f0b658e1c23a49e6e9c97a525c043c2a97c35270d5fadf2c5875` | `bffcd1e8f4722fd84a571f27a415f679e37ebf0de78ac5f80f07457144471d77` |
| w-9002 | Hunter t403 → Wellwright t324 | `e85c3c0c3b6d75247c574f8d15161840fca3f0cd3c9d076cf874faccf3dfd06c` | `da401d2356dd8d50f30bbe8057902a92a74397cc9a830fa95fc35bc3d085872c` |
| e-9005 | Hunter t437 → Wellwright t329 | `6ce55a75b841eb59e95397a2d1af8c1a71d3911669ef5f386bf66642cf6aa936` | `fbe3c676d2cbd20e37486032902f58d0d5502618429de8c7335ab6cdce7c3f22` |
| e-9009 | Wellwright t476 → Wellwright t289 | `fb75acbb2b4c62510c01fca269a120c683c07bf7676710ca1d592a40095c4c35` | `b5d6966aa4ac260d3fe1e0387d99270050226e70a43f5152a472508fa957dffc` |
| w-9012 | Hunter t335 → Wellwright t479 | `0bbe86b7072d50cf18f1825e58f655ce71fad3d1568f7c7dc67fb876156dc810` | `edf0ec9fbdd734ea3342ad88d4b932acc9d75492bb29ff52c84a09d30f50b0a4` |
| e-9004 | Hunter t322 → Hunter t379 | `e063ef59128078d02093ce2331baf1adebe7fa5b21749063257e406b5302f359` | `43561c122201bf50010358bd9d78e8d0c81325366c1703f76752f0fb1f44d984` |

The terminal record is 1–5 in these six hand-picked games, versus 5–1 for the
control. That is important exhibit context and a warning against bulk migration;
it is not a population measurement or balance conclusion.

The first attempted `w-9001` exposed a real executor fault: a global engagement
could apply isolation to every visible enemy and then index the now-empty list.
The executor now records an `isolation` decline, clears that scope's focus lock,
and continues. All six cells were discarded and rerun clean after the fix. The
in-process/WASM parity regression exercises the repaired path.

### Public review

[Open the outcome-visible six-cell gallery](https://gotta-thru-affiliation-instance.trycloudflare.com/).

Every card states the actual opponent, final result, trigger, intended tactic,
counterplay, fallback, and a concrete watch cue. The gallery builder now derives
its intro from the sample manifest: an outcome-visible exhibit no longer falsely
claims that outcomes are hidden.

A real Chromium smoke at 390×844, device scale 2, opened all six public replay
pages. Each reached the ready overlay, loaded WebGL, showed the Arc Relay score
bug, advanced playback, used gzip replay delivery, and had zero horizontal
overflow. No page, console, or request failures were captured.

## Plain list: what got worse

- A fixed role list cannot replace a dead/displaced runner from the old pool.
- `most-experienced` across several roles became an exact Hauler self-level
  gate; it is not the same choice.
- Independent routes lost shared file/screen spacing and showed more flapping
  and reversals in the two inspected cells.
- One global two-attacker cap throttled unrelated fights and produced many more
  cap declines.
- Matching co-timing conditions are repeated across three ladders.
- Body-follow continues through leader recovery/floor/patrol. This helps the
  Medic but can over-commit screeners.
- Local role doctrines nearly doubled from 214 to 410 lines, so deleting 331
  operation lines only reduced the whole sheet by 147.
- This Hunter's terminal results changed from five control wins to one prototype
  win across the six exhibits. No causal balance claim is made from six cells.

## Verdict on the four questioned concepts

| concept | verdict |
|---|---|
| operations | **Do not restore the full object.** Shared conditions and local ladders solve priority and readable intent. |
| staff | **Do not restore the broad row.** Preserve only a narrow authored N-from-pool selector/capability. |
| escort | **Delete from the new source grammar.** Follower-owned `follow` is clearer and deterministic. |
| muster | **Delete from the new source grammar.** It exists only to answer a caller contract that no longer exists. |

The pure condition thesis is therefore a productive reduction, not the finished
DSL. The minimum next design question is how to state two small shared truths—an
adaptive cohort and its contextual focus budget—without recreating operations
under another name. If either primitive starts acquiring transitions, orders,
formation state, or per-role overrides, stop: that would be the old object
returning by accretion.

No other playbook was migrated. No `web/` file changed. `docs/SHEET-AUTHORING.md`
remains the shipped grammar until the owner rules on this experiment.

## Verification

| check | result |
|---|---|
| `dotnet build BotArena.sln --no-restore` | pass, 0 warnings / 0 errors |
| full `BotArena.Cli.Tests` | pass, 309 / 309 |
| full `BotArena.Engine.Tests` | pass, 1,436 / 1,436 |
| full `BotArena.Determinism.Tests` | pass, 17 / 17 |
| full `BotArena.Runtime.Wasm.Tests` | pass, 68 / 68 |
| tracked tactical WASM source/receipt guard | pass |
| in-process/WASM tactical replay parity | pass |
| every tracked playbook compile | pass, 23 / 23 (inside CLI suite) |
| six in-process exhibit matches | complete, no runtime faults |
| migration builder second run | no byte change; reports already migrated |
| phone-sized public-browser smoke | 6 / 6 WebGL replays, no captured failures |
| `git diff --check` | pass |
| files changed under `web/` | none |
| non-Hunter playbook JSON changed | none |

Because the executor source changed, the tracked tactical player was rebuilt
through the controlled Docker path rather than bypassing the stale-artifact
guard. Its receipt now binds build input
`9e0e0538eb62b8664d3904452fcd299c56ab34726a429b0b2767c5b968bdaa81`
to WASM artifact
`70ae5dcf3b523217bd59ee55d7232dcd3b7686a42b7086d0a26b9b9598ea27d0`.

The final clean-tree experiment commit SHA is cited in the handoff because a
commit cannot truthfully embed its own hash in its contents.
