# Sheet DSL consolidation — Phase 1–2 checkpoint

Date: 2026-08-09
Branch: `sheet-dsl`
Rebased base: `f93bf61249238f5ab4909a756691b00c79605f27`

This is the mandatory stop before Phase 3. Phases 1 and 2 are implemented and
parity-proven. Two measurements changed the implementation shape described in
`SPEC-SHEET-DSL.md`; the owner should approve the revised Phase 3 direction
before the squad plane is ported.

## What landed

### Phase 1 — one word, one meaning

- Doctrine `collect` is now `scavenge`; `fight.collect` alone means combat
  precedence.
- Authored `conditionSets` and `*ConditionSetId` links are gone from every live
  tracked sheet. Boolean expressions live on the behavior they gate.
- `self-health`, `self-level`, `self-carrying`, `self-stuck-ticks`, and
  `self-mode-ticks` refer to the exact body/life evaluating a doctrine mode.
- Exact-life candidate witnesses retain `while`/`until` hysteresis while a
  stronger rung temporarily leases the body.
- The byte-frozen `home-siege-v2` source is the sole exact-byte compatibility
  exception.

Phase 1 commit: `cb01c338507c55e5f17157726d65bd40579eba9c`.

### Phase 2 — explicit courier consent

- `{ "fetch": "<custodyId>" }` is an internally conditioned doctrine rung.
  A role may put different custody policies at different list positions.
- `custodyPolicies[].authorizedCarrierRoles` is gone from live source. The
  compiler derives the exhaustive runtime membership index from fetch rungs.
- Existing modes keep their numeric rung: inserting fetch at the top increases
  both list length and each old index by one.
- Roles formerly named only by the custody roster now have a thin
  `{ fetch, ..., squad }` doctrine. It states all of their modern-plane behavior
  without pretending the rest of their old squad-plane job has already moved.
- The policy that reserved a Core is remembered across pickup, delivery, and
  handoff. This is required when one role has two fetch policies.
- Authored `arbitration` is gone. The executor owns the fixed channel order;
  source attempting to restate it is rejected.
- `fetchRungs` and `modeRungs` are compiler IR, visible in `--explain` but
  rejected in source.

The controlled current tactical artifact is:

| identity | SHA-256 |
| --- | --- |
| build input | `9f2145c48d11df136a26b2d063f567910777bda27f14803d7c650f473782215d` |
| `bot.wasm` | `53183499595e7154077d6c5b53b7c2a806783f8931201b28bf7089add90b791a` |

The hosted frozen v8 stock artifact was not changed.

## Source line counts — Hunter

Counts are physical lines owned by each top-level key. The Phase 2 column is
the current checkpoint.

| section | base | Phase 1 | Phase 2 |
| --- | ---: | ---: | ---: |
| schema | 1 | 1 | 1 |
| playbookId | 1 | 1 | 1 |
| auditStatus | 4 | 4 | 4 |
| composition | 10 | 10 | 10 |
| layout | 4 | 4 | 4 |
| perspective | 1 | 1 | 1 |
| memory | 7 | 7 | 7 |
| arbitration | 13 | 13 | 0 |
| roles | 133 | 133 | 133 |
| groups | 81 | 81 | 81 |
| formations | 662 | 662 | 662 |
| engagements | 219 | 219 | 219 |
| supportPolicies | 17 | 17 | 17 |
| custodyPolicies | 50 | 50 | 43 |
| authoring | 656 | 556 | 556 |
| coordination | 462 | 456 | 456 |
| doctrines | 98 | 98 | 149 |
| **whole file** | **2,421** | **2,315** | **2,346** |

Phase 1 removed 106 lines. Phase 2 adds 51 lines of explicit thin doctrines
while deleting 20 lines of duplicate roster/arbitration configuration, for a
net 31-line increase at this intermediate boundary. The large reduction is
still Phase 3: 1,975 lines remain in groups, formations, engagements,
authoring, and coordination.

## Parity evidence

The canonical gameplay-state digest ignores renamed provenance/debug strings
and hashes per-tick actor identity, position, health, form, facing, and result.
Every current Phase 2 digest equals both the pre-change baseline and Phase 1.

| cell | end | gameplay-state SHA-256 |
| --- | ---: | --- |
| w-9001 | 348 | `384a189642a5f0b658e1c23a49e6e9c97a525c043c2a97c35270d5fadf2c5875` |
| w-9002 | 403 | `e85c3c0c3b6d75247c574f8d15161840fca3f0cd3c9d076cf874faccf3dfd06c` |
| e-9005 | 437 | `6ce55a75b841eb59e95397a2d1af8c1a71d3911669ef5f386bf66642cf6aa936` |
| e-9009 | 476 | `fb75acbb2b4c62510c01fca269a120c683c07bf7676710ca1d592a40095c4c35` |
| w-9012 | 335 | `0bbe86b7072d50cf18f1825e58f655ce71fad3d1568f7c7dc67fb876156dc810` |
| e-9004 | 322 | `e063ef59128078d02093ce2331baf1adebe7fa5b21749063257e406b5302f359` |

The rebuilt WASM artifact also completed w-9001 at tick 348 with the same
`384a…` gameplay digest. The CLI/compiler suite passes 304/304 tests. The web
production build passes; no file under `web/` was edited.

Diagnostic gallery (outcomes shown; these are exhibits, not balance evidence):

<https://apparel-greetings-log-under.trycloudflare.com>

## Where the spec was wrong

### 1. Groups cannot disappear as an isolated Phase 1 rewrite

Hunter's groups are inert, but the corpus is not. Home Siege has real local
state transitions; `predator-v2` has a meaningful shared 2-of-4 orchard cap;
several other sheets carry caps below the sum of their role maxima (most of
those happen to be redundant under an eight-body composition, but inference
would be a hidden migration rule). Deleting groups early either loses behavior
or moves it into compiler guessing. Decision 256 therefore moves tag/cap/state
migration atomically into Phase 3.

### 2. `fetch` cannot be a normal task lease

The spec says fetch “is claimed into a task exactly as muster is.” That is the
wrong runtime shape. Muster intentionally replaces the recruit's order with
the caller's whole order. Fetch answers only whether a Core displaces the
body's current march; a generic task lease would also replace its fight,
facing, and formation behavior. The implementation is therefore a
movement-only rung overlay. It still obeys the author's ordered list, but
leaves the active mode's other planes intact.

The first implementation compared fetch priority with raw task priority and
failed two of six parity cells. Measurement found legacy tasks at priorities
5–30 even though the doctrine compiler comment assumed the old plane began at
8. Those numbers are internal to a different plane: a legacy task reached
through `squad` is below the squad floor regardless of its old number. The
compiler now records only genuine doctrine task/rung links in `modeRungs`.
That corrected all six digests.

## Proposed Phase 3 shape — owner decision required

The spec's destination is right: every role gets a doctrine and the authored
squad plane disappears. The smallest truthful port is not quite “escort rows
replace formations” as currently written.

1. **Role tags replace group identity.** Conditions and selectors resolve a
   role or tag in one namespace. Group local states become ordinary latched
   doctrine conditions/modes.
2. **A tiny staffing constraint survives where measurement proves it is real.**
   `predator-v2` needs “choose two bodies across this tagged pool.” Proposed
   source: a compact staffing row keyed by tag, with preferred/maximum. It has
   one meaning—shared allocation capacity—and does not also own orders,
   transitions, or formation state. Redundant 8-of-8 caps are deleted.
3. **Multi-body calls need selectors and cardinality.** Extend an assault/patrol
   mode's existing escort call from one body per named role to a role-or-tag
   selector with `minimum` / `preferred` / `maximum` (or a single `count` where
   all three are equal). Recruits still consent with `muster`, so “race this
   Well with three from the well-team tag plus one medic” is one readable
   leader call, not four synchronized condition copies.
4. **Formation intent remains explicit, but high-level.** `trail` and `screen`
   are insufficient to reproduce the existing role-specific well screen and
   file. Give coordinated modes a small named shape (`file`, `screen`, perhaps
   `wedge`) plus spacing/posture selectors; compile it to deterministic slots.
   Do not retain authored tile offsets. If exact state parity proves a named
   shape cannot reproduce an accepted formation, that mismatch is reported
   rather than hidden.
5. **Task lifecycle is ported only when it changes observable behavior.** Stable
   trigger, minimum hold, timeout/cooldown, participant replacement, and
   reintegration each need an explicit doctrine home or deletion backed by a
   digest. Constants that never vary do not become new knobs.

This is the recommended continuation because it keeps list position as
priority, makes every coordinated participant consent, and retains the two
things the current doctrine grammar genuinely cannot yet say: heterogeneous
N-body allocation and a predictable group shape.

Approval requested: continue Phase 3 with this revised shape, or revise the
staffing/call/shape proposal first. No Phase 3 source has been authored.

## Editor migration note (Phases 1–2)

The sibling editor must make these mechanical moves before consuming this
branch:

- doctrine `collect` → `scavenge`; keep `fight.collect` unchanged;
- delete authored `conditionSets` and all `*ConditionSetId` controls; edit
  boolean strings at each behavior;
- add the five body-scoped facts and prevent subject/zone/freshness controls on
  them;
- add `fetch` to the mode-card picker, with a custody-policy picker and no
  while/until fields;
- delete `authorizedCarrierRoles` from custody editing;
- delete the arbitration editor entirely;
- never round-trip compiler-owned `fetchRungs` or `modeRungs` back into source.

No `web/` file was changed on this branch.
