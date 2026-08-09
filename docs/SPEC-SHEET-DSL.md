# SPEC — Arc Relay sheet DSL: consolidate the second dialect away

Status: owner-approved direction, 2026-08-09. Implementation goal references
this document. Read it whole before writing code.

## 1. The true goal

A tactical sheet must be a **comprehensible DSL**. That is the objective. Every
item below is only a means to it. Judge every change against three tests:

**COMPREHENSIBLE** — A person reading a sheet cold can predict what a body will
do. No word means two things. No section exists that the reader must ignore. No
behavior happens that no line in the sheet explains.

**CONSISTENT** — Things of the same shape are said the same way. If priority is
position in an ordered list in one place, it is not an enum somewhere else. If
combat tunes with `prefer` / `leash` / `breakOff`, the ball economy tunes with
those same words.

**DRY** — A fact is stated once. A list that names roles is suspect; roles should
declare themselves. A section referenced exactly once is suspect. A constant that
never varies is not configuration. A subject you are already inside should not be
restated.

## 2. Where things stand

A sheet is currently two dialects at once. The new one — roles + custody +
doctrines(modes/fight) — governs one body out of eight. The old one governs the
other seven. Measured on the shipped sheet
`arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/hunter-v1.json`
(2421 lines):

| section | share |
|---|---|
| formations | 27% |
| authoring.* (maneuvers, assignmentProfiles, predicates, conditionSets) | 27% |
| coordination (phases, tasks) | 19% |
| engagements | 9% |
| roles | 6% |
| **doctrines — the new grammar** | **4%** |
| groups, custodyPolicies, supportPolicies | 8% |

The v3 spec scoped consolidation to the ghost's plane only, explicitly
temporarily. Ending that split is this work. The sheet should come out roughly
half its size and entirely in one voice.

## 3. Working agreement — push back

This design came out of one long conversation. Parts are well evidenced; parts
are one person's hunch. **Argue with it.** If something here is wrong, or there
is a better shape, say so with evidence and propose the alternative. Do not
implement something you believe is worse because it is written down.

- Re-run the measurements quoted here. If your numbers disagree, trust yours and
  say so.
- Two proposals in the originating conversation were retracted after measurement
  (relocating custody behavior fields into doctrines; a `carrier: true` flag on
  roles). Expect more. Retracting is cheap; shipping a bad shape is not.
- A fourth thing that violates the three tests and isn't listed here is a
  finding, not scope creep. Report it. Fix it if small and mechanical; ask first
  if it changes semantics.
- Where a decision is genuine taste (naming, mostly), pick what you think best,
  implement it, and flag it clearly so the owner can overrule.

## 4. Hard boundaries

- **You do not make calls on balance or on batteries.** Never launch grid or
  battery runs. Use exhibitions for evidence:

  ```
  BATTERY_OUT=/tmp/arc-batteries EXHIBIT_TITLE="..." \
  scripts/arc-relay-exhibit.sh NAME <sheet> <opponent> /tmp/arc-batteries/site \
    w-9001 w-9002 e-9005 e-9009 w-9012 e-9004
  ```

- **Behavior parity is the bar for phases 1–3.** These are grammar changes, not
  balance changes. Prove it with `scripts/arc-relay-state-digest.py`: identical
  digest before and after, or a difference you can explain and defend.
- Pre-alpha, only we play it. Move fast, break things in the worktree, keep it
  legible. Opacity is the only unacceptable outcome.

## 5. Phase 1 — one word, one meaning

Mechanical. Do first.

### 1a. `collect` means two things — rename the zone list to `scavenge`

`doctrine.collect` (a list of zones) collides with `fight.collect`
(`yield|first`). Two meanings, adjacent lines, unrelated. The zone list gates
opportunistic pickup — a loose core visible in a named zone, within chase leash,
grabbed by a body the custody roster never authorized. The mind's own comment at
`ArcRelayTacticalPlaybookMind.cs:~5018` says exactly that.

Rename it **`scavenge`**. `collect` then means only the fight arbitration knob.
**Owner has approved this name.**

Note this is a different axis from `custody.sourceWells`, which filters by the
well a core was *born* from and drives the coordinated pickup allocation.
Three distinct questions, three homes, keep them distinct:

| question | lives in | axis |
|---|---|---|
| which wells does the team farm? | `custody.sourceWells` | ball's birth well |
| where will I scoop a loose one? | `doctrine.scavenge` | ball's current zone |
| does scooping outrank fighting? | `fight.collect: yield\|first` | arbitration |

### 1b. Delete `authoring.conditionSets` as an authored section

The compiler already **generates** them from doctrine `while` / `until` strings
(`ArcRelayTacticalPlaybookCompiler.cs:1180-1203`). The 16 authored ones in
hunter-v1 exist only to feed legacy tasks and phases, and die with them.

`custodyPolicies[].safeConversionConditionSetId` becomes a plain condition
string, same grammar as `while` / `until`.

`authoring.predicates` **stays** — those are the named atoms and they are good.
Consider renaming the surviving concept to something a player would say
("conditions"). Your call.

### 1c. Dissolve `groups`

Evidence — re-verify it:

- All three groups in hunter-v1 have cardinality caps exactly equal to the sum
  of their roles' caps. Every cap is inert.
- All three `localStateMachine`s have one state and zero transitions.
- `membership` restates role policy (staffing, death, respawn, overflow).
- Seven predicates use a group subject. **Five of them name a group containing
  exactly one role** (`shadow-group` is just `hunter`).

What survives is a name for a set of roles. Let **roles declare their own tags**,
and let facts and custody resolve a role name **or** a tag in one namespace:

```
group-max-level shadow-group   ->  max-level hunter     (role, direct)
group-max-level well-group     ->  max-level well       (tag)
escortGroups ["well-group"]    ->  ["well"]
```

In hunter-v1 that is 81 lines collapsing to one tag on five roles. Delete the
section.

**Do not let a unit hold several roles.** Allocation is a strict
`Dictionary<unitId, roleId>` and that is correct: the doctrine binds to a role
and a body needs exactly one mind. Multi-role would require a whole new
cross-doctrine arbitration layer. Tags are the many-to-many thing; roles are the
one-to-one thing. If you think that is wrong, argue it before building it.

### 1d. Self-referential conditions

Today the only way to ask about a body's own state is to name its role:

```json
"ghost-hurt": { "fact": "role-health", "operator": "at-most",
                "value": 2, "subject": "hunter" }
```

Two problems.

**It is not DRY.** Inside the hunter's doctrine, naming `hunter` restates the
subject you are already inside.

**It is wrong for any multi-body role.** `role-health` resolves to *the weakest
live body of the role* (`ArcRelayTacticalPlaybookMind.cs:1707-1712`, defaulting
to 9999 when the role has no live body). `ghost-hurt` is correct today only
because the hunter role has `maximum: 1`. For `carrier` or `lancer` (max 2) or
`reserve` (max 8), a body at full health reads "hurt" because a rolemate is
dying. Any doctrine on a multi-body role that self-checks this way is silently
broken.

Add **self facts**: subject-free facts about the body evaluating the doctrine.
A doctrine is evaluated by one body; facts about that body should need no
subject.

At minimum:

```
self-health          this body's health
self-level           this body's veterancy level
self-carrying        1 if holding a core, else 0
self-stuck-ticks     ticks without ground made
self-mode-ticks      ticks in the current mode
```

So `ghost-hurt` becomes `self-health at-most 2`, correct for any role of any
size, and reusable verbatim across doctrines.

Design notes:

- These belong in `NO_SUBJECT_FACTS` — no subject is legal on them, and one
  should be a compile error.
- Keep `role-health` and friends. Asking about *another* role, or about my whole
  role, remains legitimate; it is just no longer the way to ask about myself.
- Decide and document what `self-*` means where a condition is evaluated for a
  group rather than a body (legacy task activation). If the answer is "self
  facts are illegal outside a doctrine," that is a fine answer — enforce it at
  compile time.
- Extend the set if the port in phase 3 needs more. Report what you added.

## 6. Phase 2 — make custody's summon symmetric with muster's

Two mechanisms summon bodies. One is well built.

**muster** — caller is a mode's `escort:` row; recruit is the role's `muster`
rung. It emits no order, no engagement, no maneuver. The compiler joins both
sides into a task at **the recruit's rung**, gated on **the caller's** condition
set, `minimumTicks: 8` for anti-flap. No open call means the task is not active
and the body falls through to its next rung — it never waits. Both directions
are compile-time errors: a `muster` answering no call throws, and an `escort:`
row naming a role with no `muster` mode throws.

**custody** — caller is `authorizedCarrierRoles`, a roster. The recruit consents
to nothing. Assigned pickup runs inside `TryMovement` **above the mode's own
march** (`~:4660` vs `~:5035`), so `patrol: shadow-north-long` in the sheet can
be a well run on screen with no line explaining it. And `custody-emergency` is
the **first** arbitration channel — above self-preservation.

### 2a. Give custody a rung verb

Proposed: **`fetch`**.

```json
"modes": [
  { "recover": "auto" },
  { "muster": "escort" },
  { "fetch": "well-custody" },
  { "patrol": "farm-loop" }
]
```

Emits no order — the body is claimed into the custody's fetch task exactly as
muster claims it into the caller's order. Fetching now ranks in the author's own
priority list instead of silently outranking the march.

### 2b. `authorizedCarrierRoles` disappears

A role opts in **by having the rung**. This is strictly more expressive than the
roster, which is why it beats the `carrier: true` role flag that was proposed and
withdrawn — a role can hold two fetch rungs at different priorities for different
policies:

```json
{ "fetch": "orchard-custody" },
{ "patrol": "orchard-loop" },
{ "fetch": "farm-outer" }
```

`ripen-harvest-v1` needs exactly this: `orchard-custody` and `farm-outer` carry
different carrier rosters.

### 2c. `custodyPolicies` otherwise stays whole

Do **not** relocate its behavior fields into doctrines. Measured across the 19
sheets holding more than one custody policy, variation *within* a sheet:

```
 18/19  sourceWells              <- the real differentiator
 18/19  authorizedCarrierRoles   <- handled by 2b
 11/19  unreachableFallback, pickupReservationTicks, transferTimeoutTicks
  1/19  baitDrop
  0/19  accidentalPickup, dropRecovery, forwardPass,
        deliveryRoutes, deliveryTimeoutTicks
```

The zero-variance fields are sheet constants, not per-body preferences; moving
them into doctrines multiplies them across three to seven copies. Custody is a
**protocol between bodies** — reservation windows bind two bodies, drop recovery
names who *else* responds — which is why it survives where groups did not.

`doctrine.custody: "<policyId>"` also stays. It says *which contract governs me*,
which is broader than volunteering to fetch: the ghost carries it and is not a
courier, and it is what makes the ghost hand off rather than run a ball home when
it stumbles on one.

Caveat on the numbers: 18 of those 19 sheets are old-grammar, so this is evidence
about how sheets have been authored so far, not proof about the future. It is the
evidence there is, and it points one way.

### 2d. Freeze `arbitration.channels`

It is authored per sheet today, so a sheet can put `movement` above
`self-preservation` and rewrite precedence silently. The standing owner ruling is
that precedence is a **hard rule, not a knob**: survival over everything; armed
recover over collect; broke-off-to-heal takes no new fight until whole;
windup and dodge over `collect: first`.

Fix the channel order in the engine and drop the section. If a sheet legitimately
needs a different order, that is a finding — report it rather than keeping the
knob.

## 7. Phase 3 — port the squad plane into doctrines

hunter-v1 has 7 roles and 3 doctrines. `hauler`, `carrier`, `eyes` and `reserve`
have none — they are driven entirely by coordination tasks, engagements and
formations. `medic` and `lancer` have thin doctrines terminating in
`squad: true`, the bridge verb meaning "the rest of me lives in the old plane."

Port them one group of behavior at a time, each with an exhibition proving the
behavior survived. Most of it is already expressible:

| legacy | new |
|---|---|
| tasks `north/centre/south-race-task` | a mode gated on `well-ticks-until-birth` |
| task `home-defense` | a mode gated on zone threat |
| task `kill-window-surge` | a mode gated on `known-enemies-unavailable` |
| task `assassin-promotion` | a mode, or muster |
| maneuvers `prowl-set`, `apex-set` | `patrol` |
| phases `prowl` / `prowl-south` / `apex` | mode ordering — that *is* what phases are |
| engagements | `fight` blocks |
| formations | escort rows with postures |

The one thing with no clear home is **multi-body coordination** — four bodies
racing a well together as a pack. The escort/muster substrate is N-ary and
posture-bearing by design and may already cover it. If it does not, propose the
smallest addition that does. **This is the genuinely open design question in this
spec and the place your judgment matters most.**

Success: every role has a doctrine; `squad: true` is referenced by nothing; and
`formations`, `engagements`, `coordination`, `authoring.maneuvers`,
`authoring.assignmentProfiles`, `authoring.standingOrders` are gone from the
authored sheet. The compiler may still synthesize whatever internal records it
needs — this is about the DSL the author writes, not the IR.

Anything in the old plane the new grammar genuinely cannot express is a
first-class deliverable. List it precisely. That list is the spec for what comes
next.

## 8. Phase 4 — deferred, do not implement

Custody does a lot of multi-body coordination the author never wrote and cannot
state: which core to fetch, who recovers a drop, how far is too far. Several
behaviors the owner flagged this month — transfer dropping on the same tile and
looping, a dropped ball near the enemy base ignored, healing and pickup
conflicting, a body picking up then standing around mid — are that policy showing
through with no line in the sheet to change it.

Mechanism belongs in the engine; policy should be authorable. That is the same
argument that turned `engagements` into the fight block.

Proposed shape, **for discussion only**, sitting beside `fight` in the doctrine
and overridable per mode, since every field states one body's willingness:

```json
"courier": {
  "prefer": ["nearest", "richest", "deepest"],
  "reach": 20,
  "abandon": { "threats": 2, "health": 2 },
  "pass": "homeward-nearest"
}
```

`prefer` mirrors `targets.prefer`; `reach` mirrors `chase.leash`; `abandon`
mirrors `breakOff`. Every field optional, current behavior as the default.

Note the deliberate three-word split — `custody` = whose rules, `courier` = how I
do it, `fetch` = when I volunteer — which exists specifically so we do not
rebuild the `collect` collision. Better naming welcome.

Write this up as a proposal in the worktree. Do not build it.

## 9. Invariants

- Behavior parity for phases 1–3: matching state digests, or an explained and
  defended difference.
- The compiler is shared (`src/BotArena.TacticalPlaybooks`) — the backend and the
  sheet editor both depend on it. Keep it compiling, keep its tests green.
- Every other sheet in the playbooks directory must still compile. Migrate them,
  or explain why one cannot be migrated.
- One commit per phase with the reasoning in the message. `docs/DECISIONS.md`
  gets an entry per semantic change.

## 10. Report back with

- Before/after line count of hunter-v1.json, by section.
- Where this spec's reasoning was wrong, and what you did instead.
- The list of legacy behaviors the new grammar cannot express.
- Your read on multi-body coordination (§7's open question).
- The self-fact set you ended up with (§1d).
- The phase 4 proposal, unimplemented.
- Exhibition URLs for the parity evidence.

## 11. Coordination

The sheet editor lives in the sibling worktree `sheet-editor-frontend` and has
its own goal, which currently includes a byte-for-byte round-trip invariant over
legacy sections. Phases 1 and 2 change the shapes it round-trips.

**Do not edit `web/`.** Land the grammar first, then finish your report with a
short migration note for that agent: which keys moved, which died, and what the
editor must now offer. The owner sequences the two.
