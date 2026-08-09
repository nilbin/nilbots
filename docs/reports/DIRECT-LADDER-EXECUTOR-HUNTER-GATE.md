# Direct ladder executor — Hunter gate

Date: 2026-08-09  
Branch: `ladder-executor`  
Base: `c3710f1290e7093f1cd84813e92cbbece03fb63d`  
Implementation checkpoint: `b60556eb`  
Status: gate complete; not merged; no other sheet migrated

## Result

Hunter now links and selects behavior from the consolidated sheet DSL rather
than from compiler-generated tasks, phases, assignments, orders, formations,
and engagements. `wellwright-v1` still runs through the legacy path, so the
six-cell exhibit compares the new Hunter path against an unchanged opponent.

This is a **generic DSL executor**, not a Hunter executor:

- selection is enabled by the document shape (`conditions` plus
  `doctrines.roles`), never by `playbookId`;
- the only fixed verbs are the grammar's six verbs: `patrol`, `intercept`,
  `assault`, `recover`, `fetch`, and `follow`;
- no shipped playbook id, role id, route id, condition id, or Hunter-specific
  name occurs in the direct selector;
- `DirectLadderRuntimeTests.ArbitraryRoleAndRouteNamesRunWithoutAPlaybookSpecificBridge`
  runs a synthetic `vanguard` role on a `breach-route` and selects
  `rung-vanguard-1-assault`.

Hunter remains the deliberately narrow admission gate. The other tracked
playbooks retain ATP1 and the legacy executor until their source grammar is
migrated.

## The payload number

| package | bytes | share of 65,536 | headroom |
|---|---:|---:|---:|
| legacy expanded Hunter at `c3710f12` | 63,644 | 97.1% | 1,892 |
| direct ATL1 Hunter | **14,548** | **22.2%** | **50,988** |

Reduction: **49,096 bytes / 77.1%**.

The ATL1 payload is 10,880 bytes of normalized authored DSL, 3,495 bytes of
bound layout, and 173 bytes of envelope/hashes/lengths. Its SHA-256 is
`fe3516d64231c5ebd28625fbf14649f9478f4903eee5a47b55c6752ba7d7cee2`.
Hunter source remains 820 lines and its ten root keys remain:

```text
schema, playbookId, auditStatus, composition, layout, memory,
roles, custodyPolicies, doctrines, conditions
```

Its authored verb set is `patrol, assault, recover, fetch, follow`; the sixth
legal verb, `intercept`, is supported by the generic executor but Hunter does
not author one.

## What the compiler and executor now do

For a consolidated document the compiler emits ATL1 containing the normalized
DSL and layout. It does **not** link generated `orders`, `coordination.tasks`,
`modeRungs`, `fetchRungs`, formations, or engagements. The CLI explain view
shows each authored ladder in place and annotates it with stable rung names such
as `rung-medic-2-follow`.

During this first gate, `ExpandAuthoring` still constructs the old exhaustive
document off-path as a validation oracle. It is used to preserve the existing
strict schema/layout checks, but its bytes never enter ATL1 and the direct mind
never reads it. Removing that duplicate compile-time work is cleanup, not a
payload or runtime blocker.

The runtime reads role ladders and resolves `while`/`until`, exact-life state,
recover eligibility, follower availability, cohort admission, and list
priority directly. The legacy `TacticalTaskMachine` receives no direct-sheet
work and cannot interleave a hidden operation band with a body's rungs.

One residual is important to state plainly. After choosing an authored rung,
the direct loader projects its movement and fight words into the existing
low-level `Order`/`Engagement` records so the mature action executor can keep
handling strikes, movement, custody, repair, and signatures. It also supplies
one steady phase and role-keyed groups as compatibility scaffolding for shared
snapshot code. This is an **in-memory action adapter**, not linked legacy IR,
but it means the old record types have not vanished from the process. Replacing
those types is valid cleanup after this early gate; pretending it already
happened would overstate the result.

## Trace now speaks sheet

The replay debug line reports the selected authored rung per body:

```text
rungs=0:hunter[4]/patrol,1:medic[4]/patrol,
      2:hauler[3]/patrol,5:lancer[2]/follow
```

Command provenance uses the same rung id, for example:

```text
tp:steady:medic:rung-medic-4-patrol:repair
tp:steady:carrier:rung-carrier-2-patrol:duel-stand
```

The six replays contain Hunter-side `recover`, `assault`, both patrol rungs,
and follower-owned `follow`. All six also contain authorized fetch movement
(28–87 `custody:authorized-pickup` commands per replay). Fetch remains a
movement overlay by design, so it does not replace the selected fight rung in
the trace.

## Behavior exhibit

[Open the outcome-visible six-cell gallery](https://proc-bbs-organization-grade.trycloudflare.com/).

The gallery is served by detached `screen` sessions on local port 8981. The
URL returned HTTP 200 through Cloudflare. These are the registered six exhibit
cells, not a battery and not a balance read.

| cell | Hunter record | final score from Hunter perspective | gameplay digest |
|---|---|---|---|
| `w-9001` | win, t482 | pulses 3–2; integrity 1–0; charge 0–2 | `27f2bb7c1f6a26c6917d49aaa959f733ebef8a60485e7c6c0eb5d594d7d227a8` |
| `w-9002` | win, t383 | pulses 3–1; integrity 2–0; charge 0–1 | `60e6896c9efd39fa6259fd1093c25919aa1f7f48d4e97020e47dc0da21e78a6d` |
| `e-9005` | loss, t491 | pulses 2–3; integrity 0–1; charge 2–0 | `5b074d8b74cd0c9f48d4c615a67275181453908ba0f8dfa536b8dfc7f0c1cc42` |
| `e-9009` | loss, t377 | pulses 1–3; integrity 0–2; charge 2–0 | `e4af30de68da967a378c289753b5539344e086482bd5108a62b6288852def512` |
| `w-9012` | loss, t385 | pulses 1–3; integrity 0–2; charge 0–0 | `18992cab9f2ba0c716692dce9a6537c80eb35a2a995811132da7e01c2c8bd030` |
| `e-9004` | win, t407 | pulses 3–1; integrity 2–0; charge 0–2 | `72f0079298ddbafcc1d9159f40913bb9ae6e981b10cd288421867095ec2601cb` |

The 3–3 terminal record is disclosed only as what happened in these six
hand-picked games. It is not interpreted as balance.

Hunter-side traffic:

| cell | flaps | reversals | blockades | chokes |
|---|---:|---:|---:|---:|
| `w-9001` | 5.55% | 7.13% | 2.99% | 1 |
| `w-9002` | 6.09% | 7.68% | 1.22% | 0 |
| `e-9005` | 7.58% | 9.52% | 2.83% | 1 |
| `e-9009` | 9.77% | 11.01% | 1.50% | 1 |
| `w-9012` | 8.17% | 9.60% | 0.86% | 0 |
| `e-9004` | 6.53% | 7.65% | 1.60% | 0 |
| weighted total | **7.19%** | **8.70%** | **1.94%** | **3** |

This is mixed evidence rather than a cosmetic parity claim. Against the
`c3710f12` exhibit, flapping is lower in `w-9002`, effectively unchanged in
`w-9001`/`e-9005`, and higher in `e-9009`/`w-9012` (slightly higher in
`e-9004`). No outcome or traffic tuning was applied. The direct ladder removes
task minimum/cooldown/reintegration quirks, so behavior differences and all six
changed digests are expected.

The visible read is coherent: the ghost patrols, promotes into assault when its
condition fires, fights through the existing focus/strike path, and recovers
when its own health qualifies. Medic and Lancer follow from their own ladders;
courier roles fetch without surrendering their current fight posture. Some
conditions do not fire in every seed—`e-9009`, for example, never promotes the
ghost to assault—which is the sheet's causal behavior, not a missing executor
verb.

## Replay-found behavior retained

The direct scheduler removes the task/formation mechanisms that caused some
old failures and leaves the mature lower action primitives intact:

- formation reflow deadlock: structurally absent on the direct path; compact
  per-rung posture remains;
- cooldown reading as disengagement and hidden-lock release: existing fight
  allocation and lock code remains;
- nearest-waypoint backward rejoin and one-way route progress: existing route
  progress code remains;
- `MotionProgress` stuck detection: unchanged;
- delivery corridor re-derivation: unchanged;
- follower candidate filtering: unchanged, with nearest live named-role body
  and unit-id tie-break.

The controlled WASM artifact was rebuilt from current sources:

```text
build input  87c667daf971ca6f2f2f3461cb8dd50b8088e7d5f4ed4b57ee71cd330c7de6ec
artifact     ec8a8e0610687fa895145270f5c452013bb2cd10fba6437c0475ae600ee6070b
```

`w-9001` completed at t482 in both in-process and WASM runtimes with identical
gameplay digest `27f2bb7c…`. Full replay hashes retain runtime/provenance facts;
the gameplay digest is the registered cross-runtime identity read.

## Hard-coded IR inventory

The previous report calls this a 25-row inventory, but its actual table has
**23 rows**. The direct path emits zero of those IR fields. Looking at behavior
rather than serialization, eight rows disappear or become authored and fifteen
remain documented executor/action-adapter invariants. That is a semantic
inventory reduction of **23 to 15**, plus elimination of all 23 from linked
data.

| previous row | direct result |
|---|---|
| perspective | documented executor invariant: team-relative |
| role-group membership | temporary snapshot adapter invariant; no authored group |
| role-group state | temporary one-state snapshot adapter; no transitions |
| assignment profile | **nothing**; no assignment object |
| task activation/lifecycle | direct exact-life `while`/`until` latch; list position preempts |
| task completion | **nothing**; `until` releases the rung directly |
| reintegration | **nothing**; the next eligible rung is immediate |
| assignment cardinality | **authored** role caps / cohort `take`; no task cardinality |
| candidate selection | authored cohort selector; documented recover/follow resolution |
| carrier eligibility | **authored** by placing `fetch` in that role's ladder |
| arrival/completion | documented per-verb action invariant |
| assignment fallback | documented direct fall-through/action fallback invariant |
| movement pace | documented executor invariant: free |
| movement recovery | documented executor invariant: repath + verb thresholds |
| patrol/intercept micro | documented executor invariant: sweep/lead |
| follow | leader role/posture authored; nearest/unit-id/leash are invariants |
| formation | **nothing**; authored per-rung posture only |
| engagement tie/lock | documented focus allocator invariant |
| engagement coordination | **authored** `focus` name or inline cap |
| engagement damage | documented action invariant |
| engagement release | authored persistence where supplied; documented base release |
| condition scaffolding | only internal `always` remains; three generated sentinels gone |
| tempo transitions | **nothing**; patrol route is the beat |

The two role-group adapter rows are the clearest cleanup target. They survive
because snapshot and role allocation APIs still accept the old package shape,
not because the DSL or ladder scheduler needs groups.

## Validation

- `dotnet build BotArena.sln` — passed, 0 warnings/errors.
- `dotnet test tests/BotArena.Cli.Tests` — 318 passed.
- `dotnet test tests/BotArena.Engine.Tests --no-build --no-restore` — 1,436 passed.
- `dotnet test tests/BotArena.Runtime.Wasm.Tests --no-build --no-restore` — 68 passed.
- compiler/direct subset — 80 passed, including the synthetic non-Hunter path.
- controlled WASM `w-9001` — completed, gameplay-equal to in-process.
- `git diff --check` — passed before the implementation checkpoint.
- no `web/` source and no `docs/SHEET-AUTHORING.md` changed.

## Verdict

The direct path earns its place. Compacting the old serialized IR would have
created byte room, but it would have retained task-band priority, IR-named
traces, and the requirement that every grammar idea first impersonate an old
task/order shape. ATL1 removes those constraints and the measured payload is
now comfortably below the ceiling.

The result also identifies the next boundary cleanly: direct ladder selection
is real and generic; the lower action adapter is still typed in legacy records.
That is a smaller, evidence-backed cleanup problem and should not block this
Hunter gate or trigger bulk migration before owner review.

## Erratum — exact assault coverage

The earlier sentence saying all six replays contained Hunter assault was
incorrect. Exact command-provenance reads show assault in four of the six
direct-gate cells; `e-9009` and `w-9012` contain no Hunter assault. The report's
later statement that `e-9009` never promoted was the correct one. This erratum
changes no replay, result, or interpretation; it records the exact observed
coverage rather than silently rewriting the historical gate.
